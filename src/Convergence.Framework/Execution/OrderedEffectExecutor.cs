using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

internal enum OrderedEffectStopReason
{
    None,
    Target,
    Action,
    Interrupted
}

internal sealed record OrderedEffectExecution(
    IReadOnlyList<EffectExecutionResult> Effects,
    OrderedEffectStopReason StopReason)
{
    public bool Interrupted => StopReason == OrderedEffectStopReason.Interrupted;
    public bool StopsAction => StopReason is OrderedEffectStopReason.Action or OrderedEffectStopReason.Interrupted;
}

internal sealed class OrderedEffectExecutor
{
    private static readonly AsyncLocal<ActionExecutionScope?> CurrentExecutionScope = new();
    private static readonly IBattleDurationLifecycleService DurationLifecycle =
        new BattleDurationLifecycleService();

    private readonly BattleExecutionServices _services;
    private readonly EffectExecutorRegistry _effectExecutors;

    public OrderedEffectExecutor(
        BattleExecutionServices services,
        EffectExecutorRegistry effectExecutors)
    {
        _services = services;
        _effectExecutors = effectExecutors;
    }

    public OrderedEffectExecution Execute(
        EffectActionExecutionRequest request,
        IReadOnlyList<EffectDefinition> effects,
        ResolvedRuntimeTargetSet targets)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(targets);
        IReadOnlyDictionary<EffectLocalId, int> effectIndexes = ValidateSequence(effects);

        ActionExecutionScope? scope = CurrentExecutionScope.Value;
        bool ownsScope = scope is null;
        if (ownsScope)
        {
            scope = new ActionExecutionScope();
            CurrentExecutionScope.Value = scope;
        }

        scope!.Track(request.Actor);
        scope.Track(request.Participants);
        scope.Track(targets.Targets);

        try
        {
            return ExecuteCore(request, effects, targets, effectIndexes);
        }
        finally
        {
            if (ownsScope)
            {
                try
                {
                    foreach ((RuntimeActorState actor, IReadOnlyList<DamageElement> elements) in
                             scope.ResolvedDamageElements)
                    {
                        _services.Charges.CompleteAction(actor, elements);
                    }

                    DurationLifecycle.ProcessActionEnd(
                        new BattleActionEndLifecycleRequest(scope.Actors),
                        _services.StatModifiers);
                }
                finally
                {
                    CurrentExecutionScope.Value = null;
                }
            }
        }
    }

    private OrderedEffectExecution ExecuteCore(
        EffectActionExecutionRequest request,
        IReadOnlyList<EffectDefinition> effects,
        ResolvedRuntimeTargetSet targets,
        IReadOnlyDictionary<EffectLocalId, int> effectIndexes)
    {
        var results = new List<EffectExecutionResult>();
        var stoppedTargets = new HashSet<RuntimeInstanceId>();
        bool targetStopped = false;
        IReadOnlyList<RuntimeActorState?> executionTargets = targets.IsUntargeted
            ? Array.AsReadOnly<RuntimeActorState?>([null])
            : Array.AsReadOnly(targets.Targets.Cast<RuntimeActorState?>().ToArray());

        for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
        {
            EffectDefinition effect = effects[effectIndex];
            DamageElement? effectElement = effect is DamageEffectDefinition damage ? damage.Element : null;

            foreach (RuntimeActorState? target in executionTargets)
            {
                if (target is not null && stoppedTargets.Contains(target.InstanceId))
                {
                    continue;
                }

                var context = new EffectExecutionContext(
                    request,
                    _services,
                    effectIndex,
                    effect,
                    target,
                    effectElement);

                EffectDependencyEvaluation? dependencyEvaluation = effect.Dependency is null
                    ? null
                    : EvaluateDependency(
                        effect.Dependency,
                        effectIndexes[effect.Dependency.SourceEffectId],
                        target,
                        results);
                if (dependencyEvaluation is { Satisfied: false })
                {
                    results.Add(new EffectExecutionResult(
                        effectIndex,
                        target?.InstanceId,
                        EffectExecutionOutcome.Skipped,
                        Detail: $"Effect dependency was not satisfied: {dependencyEvaluation.Reason}.")
                    {
                        EffectId = effect.EffectId,
                        DependencyEvaluation = dependencyEvaluation,
                        SkipReason = EffectExecutionSkipReason.DependencyUnsatisfied
                    });
                    continue;
                }

                context = context with
                {
                    DependencyEvaluation = dependencyEvaluation
                };

                TargetLifeState? requiredLifeState = RequiredTargetLifeState(
                    effect,
                    target,
                    request.Purpose);
                if (requiredLifeState is TargetLifeState required &&
                    !LifeStateMatches(target!, required))
                {
                    results.Add(new EffectExecutionResult(
                        effectIndex,
                        target!.InstanceId,
                        EffectExecutionOutcome.Skipped,
                        Detail: $"The effect requires a target whose life state is {required}.")
                    {
                        EffectId = effect.EffectId,
                        DependencyEvaluation = dependencyEvaluation,
                        SkipReason = EffectExecutionSkipReason.TargetLifeStateIneligible,
                        RequiredTargetLifeState = required
                    });
                    continue;
                }

                if (!BattleConditionEvaluator.Evaluate(effect.When, context))
                {
                    results.Add(new EffectExecutionResult(
                        effectIndex,
                        target?.InstanceId,
                        EffectExecutionOutcome.Skipped,
                        Detail: "The effect condition was false.")
                    {
                        EffectId = effect.EffectId,
                        DependencyEvaluation = dependencyEvaluation,
                        SkipReason = EffectExecutionSkipReason.ConditionUnsatisfied
                    });
                    continue;
                }

                EffectExecutionResult result = _effectExecutors.Execute(effect, context) with
                {
                    EffectId = effect.EffectId,
                    DependencyEvaluation = dependencyEvaluation
                };
                results.Add(result);
                if (effect is DamageEffectDefinition damageEffect &&
                    result.Outcome != EffectExecutionOutcome.Skipped)
                {
                    CurrentExecutionScope.Value!.TrackResolvedDamage(request.Actor, damageEffect.Element);
                }

                if (result.Outcome == EffectExecutionOutcome.Interrupted)
                {
                    return new OrderedEffectExecution(
                        Array.AsReadOnly(results.ToArray()),
                        OrderedEffectStopReason.Interrupted);
                }

                if (result.Outcome != EffectExecutionOutcome.Failure)
                {
                    continue;
                }

                if (effect.OnFailure == EffectFailurePolicy.StopAction)
                {
                    return new OrderedEffectExecution(
                        Array.AsReadOnly(results.ToArray()),
                        OrderedEffectStopReason.Action);
                }

                if (effect.OnFailure == EffectFailurePolicy.StopTarget)
                {
                    if (target is null)
                    {
                        return new OrderedEffectExecution(
                            Array.AsReadOnly(results.ToArray()),
                            OrderedEffectStopReason.Target);
                    }

                    stoppedTargets.Add(target.InstanceId);
                    targetStopped = true;
                }
            }
        }

        return new OrderedEffectExecution(
            Array.AsReadOnly(results.ToArray()),
            targetStopped ? OrderedEffectStopReason.Target : OrderedEffectStopReason.None);
    }

    internal static IReadOnlyDictionary<EffectLocalId, int> ValidateSequence(
        IReadOnlyList<EffectDefinition> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var indexes = new Dictionary<EffectLocalId, int>();
        for (int index = 0; index < effects.Count; index++)
        {
            if (effects[index].EffectId is EffectLocalId effectId && !indexes.TryAdd(effectId, index))
            {
                throw new InvalidOperationException(
                    $"Effect ID '{effectId}' is duplicated in one effect sequence.");
            }
        }

        for (int index = 0; index < effects.Count; index++)
        {
            bool sharedContact = effects[index] is DamageEffectDefinition
            {
                ContactMode: DamageContactMode.SharedContact
            };
            EffectDependencyDefinition? dependency = effects[index].Dependency;
            if (dependency is null)
            {
                if (sharedContact)
                {
                    throw new InvalidOperationException(
                        "Shared-contact damage requires a same-target positive-damage dependency.");
                }

                continue;
            }

            if (!indexes.TryGetValue(dependency.SourceEffectId, out int sourceIndex))
            {
                throw new InvalidOperationException(
                    $"Effect dependency source '{dependency.SourceEffectId}' does not exist in this sequence.");
            }

            if (sourceIndex >= index)
            {
                throw new InvalidOperationException(
                    $"Effect dependency source '{dependency.SourceEffectId}' must precede its dependent effect.");
            }

            if (dependency.Requirement == EffectDependencyRequirement.PositiveDamage &&
                effects[sourceIndex] is not DamageEffectDefinition)
            {
                throw new InvalidOperationException(
                    $"Positive-damage dependency source '{dependency.SourceEffectId}' is not a damage effect.");
            }

            if (sharedContact &&
                (dependency.Requirement != EffectDependencyRequirement.PositiveDamage ||
                 dependency.Scope != EffectDependencyScope.SameTarget))
            {
                throw new InvalidOperationException(
                    "Shared-contact damage requires a same-target positive-damage dependency.");
            }
        }

        return new System.Collections.ObjectModel.ReadOnlyDictionary<EffectLocalId, int>(indexes);
    }

    private static TargetLifeState? RequiredTargetLifeState(
        EffectDefinition effect,
        RuntimeActorState? target,
        EffectExecutionPurpose purpose)
    {
        if (target is null)
        {
            return null;
        }

        return effect switch
        {
            DamageEffectDefinition => TargetLifeState.Alive,
            InstantKillEffectDefinition => TargetLifeState.Alive,
            ApplyAilmentEffectDefinition => TargetLifeState.Alive,
            RestoreResourceEffectDefinition restore
                when restore.ResourceId == target.VitalResourceId &&
                     purpose != EffectExecutionPurpose.DefeatPrevention =>
                TargetLifeState.Alive,
            SetResourceEffectDefinition set
                when set.ResourceId == target.VitalResourceId &&
                     purpose != EffectExecutionPurpose.DefeatPrevention =>
                TargetLifeState.Alive,
            ReviveEffectDefinition => TargetLifeState.Dead,
            _ => null
        };
    }

    private static bool LifeStateMatches(RuntimeActorState target, TargetLifeState required) => required switch
    {
        TargetLifeState.Alive => !target.IsDefeated,
        TargetLifeState.Dead => target.IsDefeated,
        TargetLifeState.Any => true,
        _ => throw new InvalidOperationException($"Unsupported target life state '{required}'.")
    };

    private static EffectDependencyEvaluation EvaluateDependency(
        EffectDependencyDefinition dependency,
        int sourceEffectIndex,
        RuntimeActorState? target,
        IReadOnlyList<EffectExecutionResult> priorResults)
    {
        RuntimeInstanceId? targetId = target?.InstanceId;
        EffectExecutionResult[] sourceResults = priorResults
            .Where(result =>
                result.EffectIndex == sourceEffectIndex &&
                (dependency.Scope == EffectDependencyScope.AnyTarget || result.TargetId == targetId))
            .ToArray();

        if (sourceResults.Length == 0)
        {
            return Evaluation(false, EffectDependencyEvaluationReason.SourceResultMissing);
        }

        bool satisfied = dependency.Requirement switch
        {
            EffectDependencyRequirement.Succeeded =>
                sourceResults.Any(result => result.Outcome == EffectExecutionOutcome.Success),
            EffectDependencyRequirement.PositiveDamage =>
                sourceResults.SelectMany(result => result.DamageHits).Any(hit =>
                    hit.Hit &&
                    hit.AppliedResourceDelta < 0m &&
                    hit.AffectedActorId == hit.TargetId),
            _ => throw new InvalidOperationException(
                $"Unsupported effect dependency requirement '{dependency.Requirement}'.")
        };

        return satisfied
            ? Evaluation(true, EffectDependencyEvaluationReason.Satisfied)
            : Evaluation(
                false,
                dependency.Requirement == EffectDependencyRequirement.Succeeded
                    ? EffectDependencyEvaluationReason.SourceNotSuccessful
                    : EffectDependencyEvaluationReason.PositiveDamageNotDealt);

        EffectDependencyEvaluation Evaluation(
            bool isSatisfied,
            EffectDependencyEvaluationReason reason) =>
            new(
                dependency.SourceEffectId,
                sourceEffectIndex,
                dependency.Requirement,
                dependency.Scope,
                targetId,
                isSatisfied,
                reason);
    }

    private sealed class ActionExecutionScope
    {
        private readonly List<RuntimeActorState> _actors = [];
        private readonly HashSet<RuntimeInstanceId> _knownActorIds = [];
        private readonly Dictionary<RuntimeActorState, HashSet<DamageElement>> _resolvedDamageElements = [];

        public IReadOnlyList<RuntimeActorState> Actors => _actors;
        public IReadOnlyDictionary<RuntimeActorState, IReadOnlyList<DamageElement>> ResolvedDamageElements =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<RuntimeActorState, IReadOnlyList<DamageElement>>(
                _resolvedDamageElements.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<DamageElement>)Array.AsReadOnly(pair.Value.Order().ToArray())));

        public void Track(RuntimeActorState actor)
        {
            if (_knownActorIds.Add(actor.InstanceId))
            {
                _actors.Add(actor);
            }
        }

        public void Track(IEnumerable<RuntimeActorState> actors)
        {
            foreach (RuntimeActorState actor in actors)
            {
                Track(actor);
            }
        }

        public void TrackResolvedDamage(RuntimeActorState actor, DamageElement element)
        {
            Track(actor);
            if (!_resolvedDamageElements.TryGetValue(actor, out HashSet<DamageElement>? elements))
            {
                elements = [];
                _resolvedDamageElements.Add(actor, elements);
            }

            elements.Add(element);
        }
    }
}
