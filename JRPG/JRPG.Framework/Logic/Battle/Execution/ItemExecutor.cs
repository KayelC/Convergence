using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Execution;

public enum ItemExecutionStatus
{
    Executed,
    Rejected,
    Interrupted
}

public enum ItemConsumptionDecision
{
    None,
    ConsumeOne
}

public enum ItemExecutionDiagnosticCode
{
    ItemMustBeConsumable,
    UsageMissing,
    ContextUnavailable,
    TargetSelectionInvalid,
    EffectExecutorMissing,
    FormulaHandlerMissing,
    CustomEffectHandlerMissing,
    CustomConditionHandlerMissing,
    EscapeRuleHandlerMissing,
    AilmentMissing,
    NoApplicableEffect,
    ExecutionFailed
}

public sealed record ItemExecutionDiagnostic(
    ItemExecutionDiagnosticCode Code,
    string Message,
    int? EffectIndex = null,
    RuntimeInstanceId? TargetId = null);

public sealed record ItemExecutionRequest
{
    public ItemExecutionRequest(
        ItemDefinition item,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        EffectExecutionEnvironment environment,
        IEnumerable<RuntimeInstanceId>? selectedTargetIds = null)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
    }

    public ItemDefinition Item { get; }
    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public EffectExecutionEnvironment Environment { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }
}

public sealed record ItemExecutionAssessment
{
    internal ItemExecutionAssessment(
        IEnumerable<ItemExecutionDiagnostic> diagnostics,
        ResolvedRuntimeTargetSet? targets)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        TargetIds = Array.AsReadOnly(targets?.Targets.Select(target => target.InstanceId).ToArray() ?? []);
        Targets = targets;
    }

    public bool CanExecute => Diagnostics.Count == 0 && Targets is not null;
    public IReadOnlyList<ItemExecutionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<RuntimeInstanceId> TargetIds { get; }
    internal ResolvedRuntimeTargetSet? Targets { get; }
}

public sealed record ItemExecutionResult
{
    internal ItemExecutionResult(
        ItemExecutionStatus status,
        IEnumerable<EffectExecutionResult> effects,
        ItemConsumptionDecision consumption,
        IEnumerable<ItemExecutionDiagnostic>? diagnostics = null)
    {
        Status = status;
        Effects = Array.AsReadOnly(effects.ToArray());
        Consumption = consumption;
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
        EscapeRequested = Effects.Any(effect => effect.EscapeRequested);
        HostActionRequestIds = Array.AsReadOnly(
            Effects.SelectMany(effect => effect.HostActionRequestIds ?? []).ToArray());
    }

    public ItemExecutionStatus Status { get; }
    public IReadOnlyList<EffectExecutionResult> Effects { get; }
    public ItemConsumptionDecision Consumption { get; }
    public IReadOnlyList<ItemExecutionDiagnostic> Diagnostics { get; }
    public bool EscapeRequested { get; }
    public IReadOnlyList<ContentId> HostActionRequestIds { get; }

    internal static ItemExecutionResult Rejected(IEnumerable<ItemExecutionDiagnostic> diagnostics) =>
        new(ItemExecutionStatus.Rejected, [], ItemConsumptionDecision.None, diagnostics);
}

public interface IItemExecutor
{
    ItemExecutionAssessment Assess(ItemExecutionRequest request);
    ItemExecutionResult Execute(ItemExecutionRequest request);
}

public sealed class ItemExecutor : IItemExecutor
{
    private readonly BattleExecutionServices _services;
    private readonly EffectExecutorRegistry _effectExecutors;
    private readonly OrderedEffectExecutor _orderedEffects;

    public ItemExecutor(
        BattleExecutionServices services,
        EffectExecutorRegistry? effectExecutors = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _effectExecutors = effectExecutors ?? services.EffectExecutors;
        _orderedEffects = new OrderedEffectExecutor(_services, _effectExecutors);
    }

    public ItemExecutionAssessment Assess(ItemExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return AssessCore(request);
        }
        catch (Exception exception)
        {
            return new ItemExecutionAssessment(
            [
                new ItemExecutionDiagnostic(
                    ItemExecutionDiagnosticCode.ExecutionFailed,
                    $"Item assessment failed: {exception.Message}")
            ],
            targets: null);
        }
    }

    private ItemExecutionAssessment AssessCore(ItemExecutionRequest request)
    {
        var diagnostics = new List<ItemExecutionDiagnostic>();
        ItemUsageDefinition? usage = request.Item.Usage;

        if (request.Item.ItemKind != ItemKind.Consumable)
        {
            diagnostics.Add(new ItemExecutionDiagnostic(
                ItemExecutionDiagnosticCode.ItemMustBeConsumable,
                $"Item '{request.Item.Id}' is not consumable."));
        }

        if (usage is null)
        {
            diagnostics.Add(new ItemExecutionDiagnostic(
                ItemExecutionDiagnosticCode.UsageMissing,
                $"Item '{request.Item.Id}' has no usage definition."));
            return new ItemExecutionAssessment(diagnostics, null);
        }

        if (!usage.ContextIds.Contains(request.Environment.ContextId))
        {
            diagnostics.Add(new ItemExecutionDiagnostic(
                ItemExecutionDiagnosticCode.ContextUnavailable,
                $"Item '{request.Item.Id}' is unavailable in context '{request.Environment.ContextId}'."));
        }

        EffectActionExecutionRequest actionRequest = CreateActionRequest(request, usage);
        if (!RuntimeTargetResolver.TryResolve(
                actionRequest,
                _services,
                out ResolvedRuntimeTargetSet? targets,
                out string? targetingDiagnostic))
        {
            diagnostics.Add(new ItemExecutionDiagnostic(
                ItemExecutionDiagnosticCode.TargetSelectionInvalid,
                targetingDiagnostic ?? "Item targeting could not be resolved."));
        }

        ValidateEffects(usage.Effects, diagnostics);
        if (targets is not null &&
            !usage.Effects.Any(effect => IsApplicable(effect, targets, request.Actor)))
        {
            diagnostics.Add(new ItemExecutionDiagnostic(
                ItemExecutionDiagnosticCode.NoApplicableEffect,
                $"Item '{request.Item.Id}' would have no effect on the selected target(s)."));
        }

        return new ItemExecutionAssessment(diagnostics, targets);
    }

    public ItemExecutionResult Execute(ItemExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ItemExecutionAssessment assessment = Assess(request);
        if (!assessment.CanExecute || assessment.Targets is null || request.Item.Usage is null)
        {
            return ItemExecutionResult.Rejected(assessment.Diagnostics);
        }

        OrderedEffectExecution execution;
        RuntimeActorExecutionTransaction transaction;
        try
        {
            transaction = new RuntimeActorExecutionTransaction(request.Actor, request.Participants);
            var stagedRequest = new ItemExecutionRequest(
                request.Item,
                transaction.Actor,
                transaction.Participants,
                request.Environment,
                request.SelectedTargetIds);
            execution = _orderedEffects.Execute(
                CreateActionRequest(stagedRequest, request.Item.Usage),
                request.Item.Usage.Effects,
                transaction.Map(assessment.Targets));
        }
        catch (Exception exception)
        {
            return ItemExecutionResult.Rejected(
            [
                new ItemExecutionDiagnostic(
                    ItemExecutionDiagnosticCode.ExecutionFailed,
                    $"Item execution failed before commit: {exception.Message}")
            ]);
        }

        transaction.Commit();
        bool meaningful = execution.Effects.Any(IsMeaningfulSuccess);
        ItemConsumptionDecision consumption = meaningful
            ? ItemConsumptionDecision.ConsumeOne
            : ItemConsumptionDecision.None;

        return new ItemExecutionResult(
            execution.Interrupted ? ItemExecutionStatus.Interrupted : ItemExecutionStatus.Executed,
            execution.Effects,
            consumption);
    }

    private static EffectActionExecutionRequest CreateActionRequest(
        ItemExecutionRequest request,
        ItemUsageDefinition usage) =>
        new(
            request.Item.Id,
            request.Actor,
            request.Participants,
            request.Environment,
            usage.Targeting,
            request.SelectedTargetIds,
            item: request.Item);

    private void ValidateEffects(
        IReadOnlyList<EffectDefinition> effects,
        ICollection<ItemExecutionDiagnostic> diagnostics)
    {
        for (int index = 0; index < effects.Count; index++)
        {
            EffectDefinition effect = effects[index];
            if (!_effectExecutors.Supports(effect.GetType()))
            {
                diagnostics.Add(new ItemExecutionDiagnostic(
                    ItemExecutionDiagnosticCode.EffectExecutorMissing,
                    $"No executor is registered for '{effect.GetType().Name}'.",
                    index));
                continue;
            }

            ValidateEffectConfiguration(effect, index, diagnostics);
            ValidateConditionConfiguration(effect.When, index, diagnostics);
        }
    }

    private void ValidateEffectConfiguration(
        EffectDefinition effect,
        int effectIndex,
        ICollection<ItemExecutionDiagnostic> diagnostics)
    {
        switch (effect)
        {
            case ApplyAilmentEffectDefinition ailment when !_services.Ailments.TryGetAilment(ailment.AilmentId, out _):
                Add(ItemExecutionDiagnosticCode.AilmentMissing, $"Ailment '{ailment.AilmentId}' is unavailable at runtime.");
                break;
            case RestoreResourceEffectDefinition restore:
                ValidateAmount(restore.Amount);
                break;
            case ReviveEffectDefinition revive:
                ValidateAmount(revive.Amount);
                break;
            case ReduceResourceEffectDefinition reduce:
                ValidateAmount(reduce.Amount);
                break;
            case SetResourceEffectDefinition set:
                ValidateAmount(set.Amount);
                break;
            case EscapeEffectDefinition escape when !_services.EscapeRuleHandlers.ContainsKey(escape.EligibilityRuleId):
                Add(ItemExecutionDiagnosticCode.EscapeRuleHandlerMissing, $"No escape rule handler is registered for '{escape.EligibilityRuleId}'.");
                break;
            case CustomEffectDefinition custom when !_services.CustomEffectHandlers.ContainsKey(custom.HandlerId):
                Add(ItemExecutionDiagnosticCode.CustomEffectHandlerMissing, $"No custom effect handler is registered for '{custom.HandlerId}'.");
                break;
        }

        return;

        void ValidateAmount(AmountDefinition amount)
        {
            if (amount is FormulaAmountDefinition formula && !_services.FormulaHandlers.ContainsKey(formula.FormulaId))
            {
                Add(ItemExecutionDiagnosticCode.FormulaHandlerMissing, $"No formula handler is registered for '{formula.FormulaId}'.");
            }
        }

        void Add(ItemExecutionDiagnosticCode code, string message) =>
            diagnostics.Add(new ItemExecutionDiagnostic(code, message, effectIndex));
    }

    private void ValidateConditionConfiguration(
        ConditionDefinition? condition,
        int effectIndex,
        ICollection<ItemExecutionDiagnostic> diagnostics)
    {
        switch (condition)
        {
            case null:
                return;
            case AllConditionDefinition all:
                foreach (ConditionDefinition child in all.Conditions) ValidateConditionConfiguration(child, effectIndex, diagnostics);
                return;
            case AnyConditionDefinition any:
                foreach (ConditionDefinition child in any.Conditions) ValidateConditionConfiguration(child, effectIndex, diagnostics);
                return;
            case NotConditionDefinition not:
                ValidateConditionConfiguration(not.Condition, effectIndex, diagnostics);
                return;
            case CustomConditionDefinition custom when !_services.CustomConditionHandlers.ContainsKey(custom.HandlerId):
                diagnostics.Add(new ItemExecutionDiagnostic(
                    ItemExecutionDiagnosticCode.CustomConditionHandlerMissing,
                    $"No custom condition handler is registered for '{custom.HandlerId}'.",
                    effectIndex));
                return;
        }
    }

    private bool IsApplicable(
        EffectDefinition effect,
        ResolvedRuntimeTargetSet targets,
        RuntimeActorState actor)
    {
        if (effect is EscapeEffectDefinition or CustomEffectDefinition)
        {
            return true;
        }

        return targets.Targets.Any(target => IsApplicable(effect, actor, target));
    }

    private bool IsApplicable(
        EffectDefinition effect,
        RuntimeActorState actor,
        RuntimeActorState target) => effect switch
    {
        RestoreResourceEffectDefinition restore =>
            target.TryGetResource(restore.ResourceId, out BattleResourceState? resource) &&
            resource is not null && resource.Current < resource.Maximum,
        RemoveAilmentEffectDefinition remove => target.Ailments.Values.Any(active =>
            active.IsRemovable &&
            (remove.Scope == AilmentRemovalScope.AllRemovable ||
             remove.AilmentIds.Contains(active.Definition.Id) ||
             active.Definition.GroupIds.Any(remove.AilmentGroupIds.Contains))),
        ReviveEffectDefinition revive => target.IsDefeated && revive.ResourceId == target.VitalResourceId,
        SetResourceEffectDefinition set => IsSetResourceApplicable(set, actor, target),
        ReduceResourceEffectDefinition reduce =>
            target.TryGetResource(reduce.ResourceId, out BattleResourceState? resource) &&
            resource is not null && resource.Current > (reduce.CanReduceToZero ? 0 : Math.Min(1, resource.Maximum)),
        RemoveStatusEffectDefinition remove => HasRemovableStatus(remove, target),
        _ => true
    };

    private bool IsSetResourceApplicable(
        SetResourceEffectDefinition effect,
        RuntimeActorState actor,
        RuntimeActorState target)
    {
        if (!target.TryGetResource(effect.ResourceId, out BattleResourceState? resource) || resource is null)
        {
            return false;
        }

        if (effect.Amount is FormulaAmountDefinition formula && !_services.FormulaHandlers.ContainsKey(formula.FormulaId))
        {
            return true;
        }

        decimal desired = BattleAmountResolver.Resolve(
            effect.Amount,
            new AmountResolutionContext(actor, target, effect.ResourceId, "set_resource_assessment"),
            _services);
        return Math.Clamp(desired, 0, resource.Maximum) != resource.Current;
    }

    private static bool HasRemovableStatus(RemoveStatusEffectDefinition effect, RuntimeActorState target)
    {
        HashSet<StatusEffectKind> kinds = new(effect.StatusKinds);
        return kinds.Contains(StatusEffectKind.Buff) && target.StatStages.Values.Any(stage => stage.Stage > 0) ||
               kinds.Contains(StatusEffectKind.Debuff) && target.StatStages.Values.Any(stage => stage.Stage < 0) ||
               kinds.Contains(StatusEffectKind.Charge) && target.Charges.Count > 0 ||
               kinds.Contains(StatusEffectKind.Shield) && target.Shields.Count > 0 ||
               kinds.Contains(StatusEffectKind.AffinityOverride) && target.AffinityOverrides.Count > 0 ||
               kinds.Contains(StatusEffectKind.Other) && effect.StatusIds.Any(target.OtherStatuses.Contains);
    }

    private static bool IsMeaningfulSuccess(EffectExecutionResult result)
    {
        if (result.Outcome is not (EffectExecutionOutcome.Success or EffectExecutionOutcome.Interrupted))
        {
            return false;
        }

        if (result.EscapeRequested || (result.HostActionRequestIds?.Count ?? 0) > 0)
        {
            return true;
        }

        return result.Value is null || result.Value != 0;
    }
}
