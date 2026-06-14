using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Battle.Execution;

public interface ISkillExecutor
{
    SkillExecutionResult Execute(SkillExecutionRequest request);
}

public sealed class SkillExecutor : ISkillExecutor
{
    private readonly BattleExecutionServices _services;
    private readonly EffectExecutorRegistry _effectExecutors;

    public SkillExecutor(
        BattleExecutionServices services,
        EffectExecutorRegistry? effectExecutors = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _effectExecutors = effectExecutors ?? services.EffectExecutors;
    }

    public SkillExecutionResult Execute(SkillExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<SkillExecutionDiagnostic> diagnostics = Preflight(
            request,
            out ResolvedTargetSet? targets,
            out IReadOnlyList<ResolvedSkillCost> costs);
        if (diagnostics.Count > 0 || targets is null)
        {
            return SkillExecutionResult.Rejected(diagnostics);
        }

        CommitCosts(request.Actor, costs);
        var results = new List<EffectExecutionResult>();
        var stoppedTargets = new HashSet<ContentId>();
        IReadOnlyList<BattleActorState?> executionTargets = targets.IsUntargeted
            ? Array.AsReadOnly<BattleActorState?>([null])
            : Array.AsReadOnly(targets.Targets.Cast<BattleActorState?>().ToArray());

        for (int effectIndex = 0; effectIndex < request.Skill.Effects.Count; effectIndex++)
        {
            EffectDefinition effect = request.Skill.Effects[effectIndex];
            DamageElement? effectElement = effect is DamageEffectDefinition damage ? damage.Element : null;

            foreach (BattleActorState? target in executionTargets)
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
                    return new SkillExecutionResult(
                        SkillExecutionStatus.Interrupted,
                        results,
                        costsCommitted: request.Skill.Costs.Count > 0);
                }

                if (result.Outcome != EffectExecutionOutcome.Failure)
                {
                    continue;
                }

                if (effect.OnFailure == EffectFailurePolicy.StopAction)
                {
                    return new SkillExecutionResult(
                        SkillExecutionStatus.Executed,
                        results,
                        costsCommitted: request.Skill.Costs.Count > 0);
                }

                if (effect.OnFailure == EffectFailurePolicy.StopTarget)
                {
                    if (target is null)
                    {
                        return new SkillExecutionResult(
                            SkillExecutionStatus.Executed,
                            results,
                            costsCommitted: request.Skill.Costs.Count > 0);
                    }

                    stoppedTargets.Add(target.InstanceId);
                }
            }
        }

        return new SkillExecutionResult(
            SkillExecutionStatus.Executed,
            results,
            costsCommitted: request.Skill.Costs.Count > 0);
    }

    private List<SkillExecutionDiagnostic> Preflight(
        SkillExecutionRequest request,
        out ResolvedTargetSet? targets,
        out IReadOnlyList<ResolvedSkillCost> costs)
    {
        var diagnostics = new List<SkillExecutionDiagnostic>();
        SkillDefinition skill = request.Skill;

        if (skill.Activation != SkillActivation.Active)
        {
            diagnostics.Add(new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.SkillMustBeActive,
                $"Skill '{skill.Id}' is not active."));
        }

        if (skill.Effects.Count == 0)
        {
            diagnostics.Add(new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.SkillHasNoEffects,
                $"Active skill '{skill.Id}' has no effects to execute."));
        }

        if (skill.Availability is null || !skill.Availability.ContextIds.Contains(request.ContextId))
        {
            diagnostics.Add(new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.ContextUnavailable,
                $"Skill '{skill.Id}' is unavailable in context '{request.ContextId}'."));
        }

        if (!BattleTargetResolver.TryResolve(request, _services, out targets, out SkillExecutionDiagnostic? targetingDiagnostic) &&
            targetingDiagnostic is not null)
        {
            diagnostics.Add(targetingDiagnostic);
        }

        costs = ValidateCosts(request, diagnostics);
        for (int effectIndex = 0; effectIndex < skill.Effects.Count; effectIndex++)
        {
            EffectDefinition effect = skill.Effects[effectIndex];
            if (!_effectExecutors.Supports(effect.GetType()))
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.EffectExecutorMissing,
                    $"No executor is registered for '{effect.GetType().Name}'.",
                    effectIndex));
                continue;
            }

            ValidateEffectConfiguration(effect, effectIndex, diagnostics);
            ValidateConditionConfiguration(effect.When, effectIndex, diagnostics);

            if (targets?.IsUntargeted == true && RequiresTarget(effect))
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.TargetingInvalid,
                    $"Effect '{effect.GetType().Name}' requires a target.",
                    effectIndex));
            }
        }

        return diagnostics;
    }

    private IReadOnlyList<ResolvedSkillCost> ValidateCosts(
        SkillExecutionRequest request,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        var requiredByResource = new Dictionary<ContentId, decimal>();
        var resolvedCosts = new List<ResolvedSkillCost>();
        foreach (SkillCostDefinition cost in request.Skill.Costs)
        {
            if (!request.Actor.TryGetResource(cost.ResourceId, out BattleResourceState? resource) || resource is null)
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.ResourceMissing,
                    $"Actor '{request.Actor.InstanceId}' has no resource '{cost.ResourceId}'."));
                continue;
            }

            if (!ValidateAmountConfiguration(cost.Amount, null, diagnostics))
            {
                continue;
            }

            decimal amount = BattleAmountResolver.Resolve(
                cost.Amount,
                new AmountResolutionContext(request.Actor, request.Actor, cost.ResourceId, "skill_cost"),
                _services);
            DamageElement[] skillElements = request.Skill.Effects
                .OfType<DamageEffectDefinition>()
                .Select(effect => effect.Element)
                .Distinct()
                .ToArray();
            var conditionContext = new BattleConditionContext(
                request.Actor,
                request.Actor,
                request.Participants,
                request.BattleKindId,
                request.MoonPhaseId,
                _services,
                skillElements);
            amount = Math.Max(0, _services.RuleModifiers.ResolveNumeric(
                request.Actor,
                NumericRuleModifierType.ResourceCost,
                amount,
                new RuleModifierContext(conditionContext, request.Skill, cost.ResourceId)));
            resolvedCosts.Add(new ResolvedSkillCost(cost.ResourceId, amount));
            requiredByResource[cost.ResourceId] = requiredByResource.GetValueOrDefault(cost.ResourceId) + amount;
            decimal remaining = resource.Current - requiredByResource[cost.ResourceId];
            if (remaining < 0 || (!cost.CanReduceToZero && remaining <= 0))
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.InsufficientResource,
                    $"Resource '{cost.ResourceId}' cannot pay the authored skill costs."));
            }
        }

        return Array.AsReadOnly(resolvedCosts.ToArray());
    }

    private static void CommitCosts(BattleActorState actor, IEnumerable<ResolvedSkillCost> costs)
    {
        foreach (ResolvedSkillCost cost in costs)
        {
            actor.AddResource(cost.ResourceId, -cost.Amount);
        }
    }

    private static bool RequiresTarget(EffectDefinition effect) =>
        effect is not EscapeEffectDefinition and not CustomEffectDefinition;

    private void ValidateEffectConfiguration(
        EffectDefinition effect,
        int effectIndex,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        switch (effect)
        {
            case ApplyAilmentEffectDefinition ailment when !_services.Ailments.TryGetAilment(ailment.AilmentId, out _):
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.AilmentMissing,
                    $"Ailment '{ailment.AilmentId}' is unavailable at runtime.",
                    effectIndex));
                break;
            case RestoreResourceEffectDefinition restore:
                ValidateAmountConfiguration(restore.Amount, effectIndex, diagnostics);
                break;
            case ReviveEffectDefinition revive:
                ValidateAmountConfiguration(revive.Amount, effectIndex, diagnostics);
                break;
            case ReduceResourceEffectDefinition reduce:
                ValidateAmountConfiguration(reduce.Amount, effectIndex, diagnostics);
                break;
            case SetResourceEffectDefinition set:
                ValidateAmountConfiguration(set.Amount, effectIndex, diagnostics);
                break;
            case EscapeEffectDefinition escape when !_services.EscapeRuleHandlers.ContainsKey(escape.EligibilityRuleId):
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.EscapeRuleHandlerMissing,
                    $"No escape rule handler is registered for '{escape.EligibilityRuleId}'.",
                    effectIndex));
                break;
            case CustomEffectDefinition custom when !_services.CustomEffectHandlers.ContainsKey(custom.HandlerId):
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.CustomEffectHandlerMissing,
                    $"No custom effect handler is registered for '{custom.HandlerId}'.",
                    effectIndex));
                break;
        }
    }

    private bool ValidateAmountConfiguration(
        AmountDefinition amount,
        int? effectIndex,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        if (amount is not FormulaAmountDefinition formula || _services.FormulaHandlers.ContainsKey(formula.FormulaId))
        {
            return true;
        }

        diagnostics.Add(new SkillExecutionDiagnostic(
            SkillExecutionDiagnosticCode.FormulaHandlerMissing,
            $"No formula handler is registered for '{formula.FormulaId}'.",
            effectIndex));
        return false;
    }

    private void ValidateConditionConfiguration(
        ConditionDefinition? condition,
        int effectIndex,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        switch (condition)
        {
            case null:
                return;
            case AllConditionDefinition all:
                foreach (ConditionDefinition child in all.Conditions)
                {
                    ValidateConditionConfiguration(child, effectIndex, diagnostics);
                }
                return;
            case AnyConditionDefinition any:
                foreach (ConditionDefinition child in any.Conditions)
                {
                    ValidateConditionConfiguration(child, effectIndex, diagnostics);
                }
                return;
            case NotConditionDefinition not:
                ValidateConditionConfiguration(not.Condition, effectIndex, diagnostics);
                return;
            case CustomConditionDefinition custom when !_services.CustomConditionHandlers.ContainsKey(custom.HandlerId):
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.CustomConditionHandlerMissing,
                    $"No custom condition handler is registered for '{custom.HandlerId}'.",
                    effectIndex));
                return;
        }
    }

    private sealed record ResolvedSkillCost(ContentId ResourceId, decimal Amount);
}
