using Convergence.Content;
using Convergence.Battle;
using Convergence.Runtime;

namespace Convergence.Execution;

public interface ISkillExecutor
{
    /// <summary>Prepares one immutable, single-use target and cost decision.</summary>
    SkillExecutionAssessment Assess(SkillExecutionRequest request);

    /// <summary>Assesses and executes as one operation.</summary>
    SkillExecutionResult Execute(SkillExecutionRequest request);

    /// <summary>Executes the exact decision returned by a prior assessment.</summary>
    SkillExecutionResult Execute(SkillExecutionRequest request, SkillExecutionAssessment assessment);
}

public sealed class SkillExecutor : ISkillExecutor
{
    private readonly BattleExecutionServices _services;
    private readonly EffectExecutorRegistry _effectExecutors;
    private readonly OrderedEffectExecutor _orderedEffects;
    private readonly object _assessmentAuthority = new();

    public SkillExecutor(
        BattleExecutionServices services,
        EffectExecutorRegistry? effectExecutors = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _effectExecutors = effectExecutors ?? services.EffectExecutors;
        _orderedEffects = new OrderedEffectExecutor(_services, _effectExecutors);
    }

    public SkillExecutionResult Execute(SkillExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Execute(request, Assess(request));
    }

    public SkillExecutionResult Execute(
        SkillExecutionRequest request,
        SkillExecutionAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(assessment);
        if (!assessment.CanExecute)
        {
            return SkillExecutionResult.Rejected(assessment.Diagnostics);
        }

        if (!assessment.Preparation.IsOwnedBy(_assessmentAuthority) ||
            !RequestsAreEquivalent(assessment.Preparation.Request, request))
        {
            return InvalidAssessment("The skill assessment belongs to another executor or request.");
        }

        if (!PreparedTargetResolver.TryRebind(
                request.Participants,
                assessment.TargetIds,
                assessment.IsUntargeted,
                out ResolvedRuntimeTargetSet? preparedTargets) ||
            preparedTargets is null)
        {
            return InvalidAssessment("The skill assessment targets no longer match the execution request.");
        }

        if (!assessment.Preparation.TryConsume(_assessmentAuthority, out ExecutionAssessmentTokenFailure failure))
        {
            return InvalidAssessment(failure == ExecutionAssessmentTokenFailure.AlreadyConsumed
                ? "The skill assessment has already been executed."
                : "The skill assessment was not created by this executor.");
        }

        List<SkillExecutionDiagnostic> currentStateDiagnostics;
        try
        {
            currentStateDiagnostics = ValidatePreparedState(request, assessment, preparedTargets);
        }
        catch (Exception exception)
        {
            return SkillExecutionResult.Rejected(
            [
                new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.ExecutionFailed,
                    $"Skill execution preconditions could not be revalidated: {exception.Message}")
            ]);
        }

        if (currentStateDiagnostics.Count > 0)
        {
            return SkillExecutionResult.Rejected(currentStateDiagnostics);
        }

        OrderedEffectExecution execution;
        RuntimeActorExecutionTransaction transaction;
        IReadOnlyList<ExecutionResourceChange> committedCostChanges;
        TurnEconomyResolution turnEconomy;
        try
        {
            transaction = new RuntimeActorExecutionTransaction(request.Actor, request.Participants);
            var stagedRequest = new SkillExecutionRequest(
                request.Skill,
                transaction.Actor,
                transaction.Participants,
                request.Environment,
                request.SelectedTargetIds);
            committedCostChanges = CommitCosts(transaction.Actor, assessment.Costs);
            execution = _orderedEffects.Execute(
                stagedRequest.ToEffectActionRequest(),
                request.Skill.Effects,
                transaction.Map(preparedTargets));
            turnEconomy = _services.ResolveActionOutcome(
                execution.Effects,
                ActionOutcomeSourceKind.Skill);
        }
        catch (Exception exception)
        {
            return SkillExecutionResult.Rejected(
            [
                new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.ExecutionFailed,
                    $"Skill execution failed before commit: {exception.Message}")
            ]);
        }

        transaction.Commit();

        return new SkillExecutionResult(
            execution.Interrupted ? SkillExecutionStatus.Interrupted : SkillExecutionStatus.Executed,
            execution.Effects,
            costsCommitted: assessment.Costs.Count > 0,
            committedCostChanges: committedCostChanges,
            turnEconomy: turnEconomy);
    }

    public SkillExecutionAssessment Assess(SkillExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            List<SkillExecutionDiagnostic> diagnostics = Preflight(
                request,
                out ResolvedTargetSet? targets,
                out IReadOnlyList<ResolvedSkillCost> costs);
            return new SkillExecutionAssessment(
                diagnostics,
                targets,
                costs,
                _assessmentAuthority,
                request);
        }
        catch (Exception exception)
        {
            return new SkillExecutionAssessment(
            [
                new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.ExecutionFailed,
                    $"Skill assessment failed: {exception.Message}")
            ],
            targets: null,
            costs: [],
            authority: _assessmentAuthority,
            request: request);
        }
    }

    private static bool RequestsAreEquivalent(
        SkillExecutionRequest assessed,
        SkillExecutionRequest execution) =>
        ReferenceEquals(assessed.Skill, execution.Skill) &&
        assessed.Actor.InstanceId == execution.Actor.InstanceId &&
        assessed.Participants.Select(actor => actor.InstanceId)
            .SequenceEqual(execution.Participants.Select(actor => actor.InstanceId)) &&
        assessed.SelectedTargetIds.SequenceEqual(execution.SelectedTargetIds) &&
        assessed.ContextId == execution.ContextId &&
        assessed.BattleKindId == execution.BattleKindId &&
        assessed.MoonPhaseId == execution.MoonPhaseId &&
        BoundariesAreEquivalent(
            assessed.Environment.ActiveStatModifierBoundaries,
            execution.Environment.ActiveStatModifierBoundaries);

    private static SkillExecutionResult InvalidAssessment(string message) =>
        SkillExecutionResult.Rejected(
        [
            new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.AssessmentInvalid,
                message)
        ]);

    private List<SkillExecutionDiagnostic> Preflight(
        SkillExecutionRequest request,
        out ResolvedTargetSet? targets,
        out IReadOnlyList<ResolvedSkillCost> costs)
    {
        targets = null;
        costs = [];
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

        try
        {
            OrderedEffectExecutor.ValidateSequence(skill.Effects);
        }
        catch (InvalidOperationException exception)
        {
            diagnostics.Add(new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.ExecutionFailed,
                $"Skill '{skill.Id}' has an invalid effect sequence: {exception.Message}"));
        }

        ValidateAuthoredPercentages(skill.Effects, diagnostics);
        if (diagnostics.Any(diagnostic =>
                diagnostic.Code == SkillExecutionDiagnosticCode.AuthoredPercentageOutOfRange))
        {
            return diagnostics;
        }

        ValidateUniqueCostResources(skill.Costs, diagnostics);
        if (diagnostics.Any(diagnostic =>
                diagnostic.Code == SkillExecutionDiagnosticCode.DuplicateResourceCost))
        {
            return diagnostics;
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

        AddStatModifierApplicabilityDiagnostic(request, targets?.Targets, diagnostics);
        AddChargeApplicabilityDiagnostic(request, targets?.Targets, diagnostics);

        return diagnostics;
    }

    private static void ValidateUniqueCostResources(
        IReadOnlyList<SkillCostDefinition> costs,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        var seen = new HashSet<ContentId>();
        foreach (SkillCostDefinition cost in costs)
        {
            if (!seen.Add(cost.ResourceId))
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.DuplicateResourceCost,
                    $"Resource '{cost.ResourceId}' cannot be charged more than once by one skill."));
            }
        }
    }

    private IReadOnlyList<ResolvedSkillCost> ValidateCosts(
        SkillExecutionRequest request,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
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
            resolvedCosts.Add(new ResolvedSkillCost(cost.ResourceId, amount, cost.CanReduceToZero));
            decimal remaining = resource.Current - amount;
            if (remaining < 0 || (!cost.CanReduceToZero && remaining <= 0))
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.InsufficientResource,
                    $"Resource '{cost.ResourceId}' cannot pay the authored skill costs."));
            }
        }

        return Array.AsReadOnly(resolvedCosts.ToArray());
    }

    private List<SkillExecutionDiagnostic> ValidatePreparedState(
        SkillExecutionRequest request,
        SkillExecutionAssessment assessment,
        ResolvedRuntimeTargetSet preparedTargets)
    {
        var diagnostics = new List<SkillExecutionDiagnostic>();
        if (!BattleTargetResolver.TryValidatePreparedTargets(
                request,
                preparedTargets,
                out SkillExecutionDiagnostic? targetingDiagnostic) &&
            targetingDiagnostic is not null)
        {
            diagnostics.Add(targetingDiagnostic);
        }

        if (assessment.Costs.Count != request.Skill.Costs.Count)
        {
            diagnostics.Add(new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.AssessmentInvalid,
                "The prepared skill costs no longer match the authored skill costs."));
            return diagnostics;
        }

        for (int index = 0; index < assessment.Costs.Count; index++)
        {
            ResolvedSkillCost cost = assessment.Costs[index];
            SkillCostDefinition authored = request.Skill.Costs[index];
            if (cost.ResourceId != authored.ResourceId ||
                cost.CanReduceToZero != authored.CanReduceToZero ||
                cost.Amount < 0)
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.AssessmentInvalid,
                    "The prepared skill costs no longer match the authored skill costs."));
                return diagnostics;
            }

            if (!request.Actor.TryGetResource(cost.ResourceId, out BattleResourceState? resource) || resource is null)
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.ResourceMissing,
                    $"Actor '{request.Actor.InstanceId}' no longer has resource '{cost.ResourceId}'."));
                continue;
            }

            decimal remaining = resource.Current - cost.Amount;
            if (remaining < 0 || (!cost.CanReduceToZero && remaining <= 0))
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.InsufficientResource,
                    $"Resource '{cost.ResourceId}' can no longer pay the prepared skill costs."));
            }
        }

        AddStatModifierApplicabilityDiagnostic(request, preparedTargets.Targets, diagnostics);
        AddChargeApplicabilityDiagnostic(request, preparedTargets.Targets, diagnostics);

        return diagnostics;
    }

    private static void ValidateAuthoredPercentages(
        IReadOnlyList<EffectDefinition> effects,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        for (int index = 0; index < effects.Count; index++)
        {
            foreach (AuthoredPercentageIssue issue in
                     AuthoredEffectPercentageValidator.Validate(effects[index]))
            {
                diagnostics.Add(new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.AuthoredPercentageOutOfRange,
                    issue.Message,
                    index));
            }
        }
    }

    private void AddStatModifierApplicabilityDiagnostic(
        SkillExecutionRequest request,
        IEnumerable<RuntimeActorState>? targets,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        ModifyStatStageEffectDefinition[] modifierEffects = request.Skill.Effects
            .OfType<ModifyStatStageEffectDefinition>()
            .ToArray();
        if (modifierEffects.Length == 0 ||
            modifierEffects.Length != request.Skill.Effects.Count ||
            targets is null)
        {
            return;
        }

        RuntimeActorState[] targetSnapshot = targets.ToArray();
        bool applicable = modifierEffects.Any(effect => targetSnapshot.Any(target =>
            StatModifierExecution.AssessApplication(
                target,
                effect,
                request.Environment,
                _services.StatModifiers) is { Accepted: true, StateChanged: true }));
        if (!applicable)
        {
            diagnostics.Add(new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.NoApplicableEffect,
                $"Skill '{request.Skill.Id}' would not change the selected target modifier state."));
        }
    }

    private void AddChargeApplicabilityDiagnostic(
        SkillExecutionRequest request,
        IEnumerable<RuntimeActorState>? targets,
        ICollection<SkillExecutionDiagnostic> diagnostics)
    {
        GrantChargeEffectDefinition[] chargeEffects = request.Skill.Effects
            .OfType<GrantChargeEffectDefinition>()
            .ToArray();
        if (chargeEffects.Length == 0 ||
            chargeEffects.Length != request.Skill.Effects.Count ||
            targets is null)
        {
            return;
        }

        RuntimeActorState[] targetSnapshot = targets.ToArray();
        bool applicable = chargeEffects.Any(effect => targetSnapshot.Any(target =>
            _services.Charges.Assess(new ChargeApplicationRequest(
                target,
                effect.Charge,
                effect.Multiplier,
                effect.Duration)).CanApply));
        if (!applicable)
        {
            diagnostics.Add(new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.NoApplicableEffect,
                $"Skill '{request.Skill.Id}' would not change the selected target charge state."));
        }
    }

    private static bool BoundariesAreEquivalent(
        IReadOnlyList<StatModifierLifecycleBoundary> assessed,
        IReadOnlyList<StatModifierLifecycleBoundary> execution) =>
        assessed.Count == execution.Count && assessed.Zip(execution).All(pair =>
            pair.First.EventId == pair.Second.EventId &&
            pair.First.Sequence == pair.Second.Sequence);

    private static IReadOnlyList<ExecutionResourceChange> CommitCosts(
        RuntimeActorState actor,
        IEnumerable<ResolvedSkillCost> costs)
    {
        var changes = new List<ExecutionResourceChange>();
        foreach (ResolvedSkillCost cost in costs)
        {
            decimal delta = actor.AddResource(cost.ResourceId, -cost.Amount);
            if (delta != 0)
            {
                changes.Add(new ExecutionResourceChange(actor.InstanceId, cost.ResourceId, delta));
            }
        }

        return Array.AsReadOnly(changes.ToArray());
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

}
