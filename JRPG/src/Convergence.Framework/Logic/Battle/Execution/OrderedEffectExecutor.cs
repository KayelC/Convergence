using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Execution;

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
    private static readonly AsyncLocal<ActionDurationScope?> CurrentDurationScope = new();
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

        ActionDurationScope? scope = CurrentDurationScope.Value;
        bool ownsScope = scope is null;
        if (ownsScope)
        {
            scope = new ActionDurationScope();
            CurrentDurationScope.Value = scope;
        }

        scope!.Track(request.Actor);
        scope.Track(request.Participants);
        scope.Track(targets.Targets);

        try
        {
            return ExecuteCore(request, effects, targets);
        }
        finally
        {
            if (ownsScope)
            {
                try
                {
                    DurationLifecycle.ProcessActionEnd(
                        new BattleActionEndLifecycleRequest(scope.Actors));
                }
                finally
                {
                    CurrentDurationScope.Value = null;
                }
            }
        }
    }

    private OrderedEffectExecution ExecuteCore(
        EffectActionExecutionRequest request,
        IReadOnlyList<EffectDefinition> effects,
        ResolvedRuntimeTargetSet targets)
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

                if (!BattleConditionEvaluator.Evaluate(effect.When, context))
                {
                    results.Add(new EffectExecutionResult(
                        effectIndex,
                        target?.InstanceId,
                        EffectExecutionOutcome.Skipped,
                        Detail: "The effect condition was false."));
                    continue;
                }

                EffectExecutionResult result = _effectExecutors.Execute(effect, context);
                results.Add(result);

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

    private sealed class ActionDurationScope
    {
        private readonly List<RuntimeActorState> _actors = [];
        private readonly HashSet<RuntimeActorState> _known = new(ReferenceEqualityComparer.Instance);

        public IReadOnlyList<RuntimeActorState> Actors => _actors;

        public void Track(RuntimeActorState actor)
        {
            if (_known.Add(actor))
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
    }
}
