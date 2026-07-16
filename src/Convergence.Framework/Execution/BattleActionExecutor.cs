using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

public enum BattleActionKind
{
    BasicAttack,
    Skill,
    Item,
    Guard,
    Pass,
    Analyze,
    HostedEntitySwap,
    CompanionDeploy,
    CompanionRecall,
    CompanionSwap,
    EscapeAttempt,
    TacticsChange,
    Negotiation,
    HostSpecial
}

public enum BattleActionExecutionStatus
{
    Executed,
    Rejected,
    Interrupted
}

public enum ActionTurnConsumptionKind
{
    None,
    Normal,
    Pass,
    TurnEconomy,
    TerminatePhase
}

public enum BattleActionDiagnosticCode
{
    SkillRejected,
    ItemRejected,
    ItemUnavailable,
    TargetSelectionInvalid,
    EffectExecutorMissing,
    PartyRosterRejected,
    UnsupportedAction,
    HostActionRequired,
    AssessmentInvalid,
    ExecutionFailed,
    ItemReservationFailed,
    ItemCommitFailed,
    ItemRollbackFailed
}

public enum BattleActionEventKind
{
    Assessed,
    Executed,
    Rejected,
    EffectResolved,
    ItemReserved,
    ItemCommitted,
    ItemRolledBack,
    PartyRosterTransitioned,
    HostActionRequested
}

public sealed record ActionTurnConsumption(
    ActionTurnConsumptionKind Kind,
    TurnEconomyResolution? TurnEconomy = null)
{
    public static ActionTurnConsumption None { get; } = new(ActionTurnConsumptionKind.None);
    public static ActionTurnConsumption Normal { get; } = new(ActionTurnConsumptionKind.Normal);
    public static ActionTurnConsumption Pass { get; } = new(ActionTurnConsumptionKind.Pass);
    public static ActionTurnConsumption TerminatePhase { get; } = new(ActionTurnConsumptionKind.TerminatePhase);

    public static ActionTurnConsumption FromTurnEconomy(TurnEconomyResolution resolution) =>
        new(ActionTurnConsumptionKind.TurnEconomy, resolution);
}

public sealed record BattleActionDiagnostic(
    BattleActionDiagnosticCode Code,
    string Message,
    int? EffectIndex = null,
    RuntimeInstanceId? TargetId = null);

public sealed record BattleActionEvent(
    BattleActionEventKind Kind,
    string Message,
    RuntimeInstanceId? ActorId = null,
    RuntimeInstanceId? TargetId = null,
    ContentId? SourceId = null,
    decimal? Value = null);

public abstract record BattleActionCommand
{
    private protected BattleActionCommand(BattleActionKind kind)
    {
        Kind = kind;
    }

    public BattleActionKind Kind { get; }
}

public sealed record BasicAttackBattleActionCommand : BattleActionCommand
{
    public BasicAttackBattleActionCommand(
        EquipmentBasicAttackDefinition basicAttack,
        TargetingDefinition targeting,
        IEnumerable<RuntimeInstanceId>? selectedTargetIds = null,
        ContentId? actionId = null)
        : base(BattleActionKind.BasicAttack)
    {
        BasicAttack = basicAttack;
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
        ActionId = actionId ?? ContentId.Parse("basic_attack");
    }

    public EquipmentBasicAttackDefinition BasicAttack { get; }
    public TargetingDefinition Targeting { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }
    public ContentId ActionId { get; }
}

public sealed record SkillBattleActionCommand : BattleActionCommand
{
    public SkillBattleActionCommand(SkillDefinition skill, IEnumerable<RuntimeInstanceId>? selectedTargetIds = null)
        : base(BattleActionKind.Skill)
    {
        Skill = skill ?? throw new ArgumentNullException(nameof(skill));
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
    }

    public SkillDefinition Skill { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }
}

public sealed record ItemBattleActionCommand : BattleActionCommand
{
    public ItemBattleActionCommand(ItemDefinition item, IEnumerable<RuntimeInstanceId>? selectedTargetIds = null, int quantity = 1)
        : base(BattleActionKind.Item)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Item quantity must be positive.");
        }

        Item = item ?? throw new ArgumentNullException(nameof(item));
        SelectedTargetIds = Array.AsReadOnly(selectedTargetIds?.ToArray() ?? []);
        Quantity = quantity;
    }

    public ItemDefinition Item { get; }
    public IReadOnlyList<RuntimeInstanceId> SelectedTargetIds { get; }
    public int Quantity { get; }
}

public sealed record GuardBattleActionCommand() : BattleActionCommand(BattleActionKind.Guard);

public sealed record PassBattleActionCommand() : BattleActionCommand(BattleActionKind.Pass);

public sealed record AnalyzeBattleActionCommand : BattleActionCommand
{
    public AnalyzeBattleActionCommand(RuntimeInstanceId targetId, IEnumerable<AnalysisLayer> layers)
        : base(BattleActionKind.Analyze)
    {
        TargetId = targetId;
        Layers = Array.AsReadOnly((layers ?? throw new ArgumentNullException(nameof(layers))).ToArray());
    }

    public RuntimeInstanceId TargetId { get; }
    public IReadOnlyList<AnalysisLayer> Layers { get; }
}

public sealed record EscapeAttemptBattleActionCommand : BattleActionCommand
{
    public EscapeAttemptBattleActionCommand(ContentId eligibilityRuleId, int? chance = null)
        : base(BattleActionKind.EscapeAttempt)
    {
        EligibilityRuleId = eligibilityRuleId;
        Chance = chance;
    }

    public ContentId EligibilityRuleId { get; }
    public int? Chance { get; }
}

public abstract record PartyRosterBattleActionCommand : BattleActionCommand
{
    private protected PartyRosterBattleActionCommand(BattleActionKind kind, RuntimePartyRosterSnapshot snapshot)
        : base(kind)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public RuntimePartyRosterSnapshot Snapshot { get; }
}

public sealed record HostedEntitySwapBattleActionCommand : PartyRosterBattleActionCommand
{
    public HostedEntitySwapBattleActionCommand(RuntimePartyRosterSnapshot snapshot, RuntimeInstanceId hostedEntityInstanceId)
        : base(BattleActionKind.HostedEntitySwap, snapshot)
    {
        HostedEntityInstanceId = hostedEntityInstanceId;
    }

    public RuntimeInstanceId HostedEntityInstanceId { get; }
}

public sealed record CompanionDeployBattleActionCommand : PartyRosterBattleActionCommand
{
    public CompanionDeployBattleActionCommand(RuntimePartyRosterSnapshot snapshot, RuntimeInstanceId companionInstanceId)
        : base(BattleActionKind.CompanionDeploy, snapshot)
    {
        CompanionInstanceId = companionInstanceId;
    }

    public RuntimeInstanceId CompanionInstanceId { get; }
}

public sealed record CompanionRecallBattleActionCommand : PartyRosterBattleActionCommand
{
    public CompanionRecallBattleActionCommand(RuntimePartyRosterSnapshot snapshot, RuntimeInstanceId companionInstanceId)
        : base(BattleActionKind.CompanionRecall, snapshot)
    {
        CompanionInstanceId = companionInstanceId;
    }

    public RuntimeInstanceId CompanionInstanceId { get; }
}

public sealed record CompanionSwapBattleActionCommand : PartyRosterBattleActionCommand
{
    public CompanionSwapBattleActionCommand(
        RuntimePartyRosterSnapshot snapshot,
        RuntimeInstanceId activeCompanionInstanceId,
        RuntimeInstanceId standbyCompanionInstanceId)
        : base(BattleActionKind.CompanionSwap, snapshot)
    {
        ActiveCompanionInstanceId = activeCompanionInstanceId;
        StandbyCompanionInstanceId = standbyCompanionInstanceId;
    }

    public RuntimeInstanceId ActiveCompanionInstanceId { get; }
    public RuntimeInstanceId StandbyCompanionInstanceId { get; }
}

public sealed record HostMediatedBattleActionCommand : BattleActionCommand
{
    public HostMediatedBattleActionCommand(
        BattleActionKind kind,
        ContentId hostActionId,
        ActionTurnConsumption? turnConsumption = null,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : base(ValidateKind(kind))
    {
        HostActionId = hostActionId;
        TurnConsumption = turnConsumption ?? ActionTurnConsumption.Normal;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId HostActionId { get; }
    public ActionTurnConsumption TurnConsumption { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    private static BattleActionKind ValidateKind(BattleActionKind kind) =>
        kind is BattleActionKind.TacticsChange or BattleActionKind.Negotiation or BattleActionKind.HostSpecial
            ? kind
            : throw new ArgumentException("Only tactics, negotiation, and host-special commands may be host-mediated.", nameof(kind));
}

public sealed record BattleActionExecutionRequest
{
    public BattleActionExecutionRequest(
        BattleActionCommand command,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        EffectExecutionEnvironment environment,
        IItemActionInventory? itemInventory = null)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ItemInventory = itemInventory;
    }

    public BattleActionCommand Command { get; }
    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public EffectExecutionEnvironment Environment { get; }
    public IItemActionInventory? ItemInventory { get; }
}

public sealed record BattleActionAssessment
{
    internal BattleActionAssessment(
        object authority,
        BattleActionExecutionRequest request,
        BattleActionKind kind,
        IEnumerable<BattleActionDiagnostic>? diagnostics = null,
        IEnumerable<RuntimeInstanceId>? targetIds = null,
        bool hasResolvedTargets = false,
        bool isUntargeted = false,
        ActionTurnConsumption? turnConsumption = null,
        SkillExecutionAssessment? skillAssessment = null,
        ItemExecutionAssessment? itemAssessment = null,
        PartyRosterTransitionResult? partyRosterTransition = null)
    {
        Kind = kind;
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
        TargetIds = Array.AsReadOnly(targetIds?.ToArray() ?? []);
        HasResolvedTargets = hasResolvedTargets;
        IsUntargeted = isUntargeted;
        TurnConsumption = turnConsumption ?? ActionTurnConsumption.Normal;
        SkillAssessment = skillAssessment;
        ItemAssessment = itemAssessment;
        PartyRosterTransition = partyRosterTransition;
        Preparation = new ExecutionAssessmentToken<BattleActionExecutionRequest>(authority, request);
    }

    public BattleActionKind Kind { get; }
    public bool CanExecute => Diagnostics.Count == 0;
    public IReadOnlyList<BattleActionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<RuntimeInstanceId> TargetIds { get; }
    public ActionTurnConsumption TurnConsumption { get; init; }
    public SkillExecutionAssessment? SkillAssessment { get; }
    public ItemExecutionAssessment? ItemAssessment { get; }
    public PartyRosterTransitionResult? PartyRosterTransition { get; }
    internal bool HasResolvedTargets { get; }
    internal bool IsUntargeted { get; }
    internal ExecutionAssessmentToken<BattleActionExecutionRequest> Preparation { get; }
}

public sealed record BattleActionExecutionResult
{
    internal BattleActionExecutionResult(
        BattleActionExecutionStatus status,
        BattleActionKind kind,
        ActionTurnConsumption turnConsumption,
        IEnumerable<EffectExecutionResult>? effects = null,
        IEnumerable<BattleActionDiagnostic>? diagnostics = null,
        IEnumerable<BattleActionEvent>? events = null,
        ItemConsumptionDecision itemConsumption = ItemConsumptionDecision.None,
        bool itemConsumptionCommitted = false,
        bool escapeRequested = false,
        PartyRosterTransitionResult? partyRosterTransition = null,
        IEnumerable<ContentId>? hostActionRequestIds = null,
        IEnumerable<ExecutionResourceChange>? committedCostChanges = null)
    {
        Status = status;
        Kind = kind;
        TurnConsumption = turnConsumption;
        Effects = Array.AsReadOnly(effects?.ToArray() ?? []);
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
        Events = Array.AsReadOnly(events?.ToArray() ?? []);
        ItemConsumption = itemConsumption;
        ItemConsumptionCommitted = itemConsumptionCommitted;
        EscapeRequested = escapeRequested || Effects.Any(effect => effect.EscapeRequested);
        PartyRosterTransition = partyRosterTransition;
        HostActionRequestIds = Array.AsReadOnly(
            (hostActionRequestIds ?? Effects.SelectMany(effect => effect.HostActionRequestIds)).ToArray());
        CommittedCostChanges = Array.AsReadOnly(committedCostChanges?.ToArray() ?? []);
    }

    public BattleActionExecutionStatus Status { get; }
    public BattleActionKind Kind { get; }
    public ActionTurnConsumption TurnConsumption { get; init; }
    public IReadOnlyList<EffectExecutionResult> Effects { get; }
    public IReadOnlyList<BattleActionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<BattleActionEvent> Events { get; }
    public ItemConsumptionDecision ItemConsumption { get; }
    public bool ItemConsumptionCommitted { get; }
    public bool EscapeRequested { get; }
    public PartyRosterTransitionResult? PartyRosterTransition { get; }
    public IReadOnlyList<ContentId> HostActionRequestIds { get; }
    /// <summary>Gets resource mutations committed as action costs before effect execution.</summary>
    public IReadOnlyList<ExecutionResourceChange> CommittedCostChanges { get; }
}

public interface IItemActionReservation
{
    ContentId ItemId { get; }
    int Quantity { get; }
    bool IsCommitted { get; }
    bool IsRolledBack { get; }

    // Host implementations must apply each transition atomically and report rejection without mutation.
    ItemActionReservationTransitionResult Commit();
    ItemActionReservationTransitionResult Rollback();
}

public sealed record ItemActionReservationTransitionResult(
    bool Applied,
    string? Message = null)
{
    public static ItemActionReservationTransitionResult Success { get; } = new(true);

    public static ItemActionReservationTransitionResult Rejected(string message) =>
        new(false, string.IsNullOrWhiteSpace(message) ? "Item reservation transition was rejected." : message);
}

public interface IItemActionInventory
{
    bool HasAvailable(ContentId itemId, int quantity);

    // Reserve must either return a live reservation or fail without changing inventory state.
    IItemActionReservation Reserve(ContentId itemId, int quantity);
}

public interface IBattleActionExecutor
{
    /// <summary>Prepares one immutable, single-use action decision for host presentation.</summary>
    BattleActionAssessment Assess(BattleActionExecutionRequest request);

    /// <summary>Assesses and executes as one operation without exposing an intermediate decision.</summary>
    ValueTask<BattleActionExecutionResult> ExecuteAsync(
        BattleActionExecutionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Executes the exact decision previously returned for this request.</summary>
    ValueTask<BattleActionExecutionResult> ExecuteAsync(
        BattleActionExecutionRequest request,
        BattleActionAssessment assessment,
        CancellationToken cancellationToken = default);
}

/// <summary>Assesses and atomically executes typed battle commands through shared effect services.</summary>
public sealed class BattleActionExecutor : IBattleActionExecutor
{
    private readonly ISkillExecutor _skills;
    private readonly IItemExecutor _items;
    private readonly BattleExecutionServices _services;
    private readonly IPartyRosterTransitionService _partyRoster;
    private readonly OrderedEffectExecutor _orderedEffects;
    private readonly object _assessmentAuthority = new();

    public BattleActionExecutor(
        ISkillExecutor skills,
        IItemExecutor items,
        BattleExecutionServices services,
        IPartyRosterTransitionService? partyRoster = null)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _partyRoster = partyRoster ?? new PartyRosterTransitionService();
        _orderedEffects = new OrderedEffectExecutor(_services, _services.EffectExecutors);
    }

    public BattleActionAssessment Assess(BattleActionExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return request.Command switch
            {
                SkillBattleActionCommand skill => AssessSkill(request, skill),
                ItemBattleActionCommand item => AssessItem(request, item),
                BasicAttackBattleActionCommand attack => AssessEffectAction(
                    request,
                    attack.Kind,
                    attack.ActionId,
                    attack.Targeting,
                    attack.SelectedTargetIds,
                    [BasicAttackEffect(attack.BasicAttack)]),
                AnalyzeBattleActionCommand analyze => AssessEffectAction(
                    request,
                    analyze.Kind,
                    ContentId.Parse("analyze"),
                    SingleAnyTargeting(),
                    [analyze.TargetId],
                    [new AnalyzeEffectDefinition(analyze.Layers)]),
                EscapeAttemptBattleActionCommand escape => AssessEffectAction(
                    request,
                    escape.Kind,
                    escape.EligibilityRuleId,
                    Untargeted(),
                    [],
                    [new EscapeEffectDefinition(escape.EligibilityRuleId, escape.Chance)]),
                GuardBattleActionCommand => CreateAssessment(
                    request,
                    BattleActionKind.Guard,
                    turnConsumption: ActionTurnConsumption.Normal),
                PassBattleActionCommand => CreateAssessment(
                    request,
                    BattleActionKind.Pass,
                    turnConsumption: ActionTurnConsumption.Pass),
                HostedEntitySwapBattleActionCommand hostedEntity => AssessPartyRoster(request, hostedEntity.Kind, _partyRoster.SwapActiveHostedEntity(
                    new SwapActiveHostedEntityRequest(hostedEntity.Snapshot, hostedEntity.HostedEntityInstanceId))),
                CompanionDeployBattleActionCommand deploy => AssessPartyRoster(request, deploy.Kind, _partyRoster.DeployCompanion(
                    new DeployCompanionRequest(deploy.Snapshot, deploy.CompanionInstanceId))),
                CompanionRecallBattleActionCommand returned => AssessPartyRoster(request, returned.Kind, _partyRoster.RecallCompanion(
                    new RecallCompanionRequest(returned.Snapshot, returned.CompanionInstanceId))),
                CompanionSwapBattleActionCommand swap => AssessPartyRoster(request, swap.Kind, _partyRoster.SwapDeployedCompanion(
                    new SwapDeployedCompanionRequest(swap.Snapshot, swap.ActiveCompanionInstanceId, swap.StandbyCompanionInstanceId))),
                HostMediatedBattleActionCommand mediated => CreateAssessment(
                    request,
                    mediated.Kind,
                    targetIds: [],
                    turnConsumption: mediated.TurnConsumption),
                _ => CreateAssessment(
                    request,
                    request.Command.Kind,
                    [new BattleActionDiagnostic(BattleActionDiagnosticCode.UnsupportedAction, "The action command is not supported.")],
                    turnConsumption: ActionTurnConsumption.None)
            };
        }
        catch (Exception exception)
        {
            return CreateAssessment(
                request,
                request.Command.Kind,
                [new BattleActionDiagnostic(
                    BattleActionDiagnosticCode.ExecutionFailed,
                    $"Action assessment failed: {exception.Message}")],
                turnConsumption: ActionTurnConsumption.None);
        }
    }

    public ValueTask<BattleActionExecutionResult> ExecuteAsync(
        BattleActionExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ExecuteAsync(request, Assess(request), cancellationToken);
    }

    public ValueTask<BattleActionExecutionResult> ExecuteAsync(
        BattleActionExecutionRequest request,
        BattleActionAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(assessment);
        cancellationToken.ThrowIfCancellationRequested();

        if (!assessment.Preparation.IsOwnedBy(_assessmentAuthority) ||
            !ReferenceEquals(assessment.Preparation.Request, request))
        {
            return new ValueTask<BattleActionExecutionResult>(InvalidAssessment(
                request.Command.Kind,
                "The battle-action assessment belongs to another executor or request."));
        }

        if (!assessment.CanExecute)
        {
            return new ValueTask<BattleActionExecutionResult>(Rejected(request.Command.Kind, assessment.Diagnostics));
        }

        if (!assessment.Preparation.TryConsume(_assessmentAuthority, out ExecutionAssessmentTokenFailure failure))
        {
            return new ValueTask<BattleActionExecutionResult>(InvalidAssessment(
                request.Command.Kind,
                failure == ExecutionAssessmentTokenFailure.AlreadyConsumed
                    ? "The battle-action assessment has already been executed."
                    : "The battle-action assessment was not created by this executor."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<BattleActionExecutionResult>(request.Command switch
        {
            SkillBattleActionCommand skill => ExecuteSkill(request, skill, assessment),
            ItemBattleActionCommand item => ExecuteItem(request, item, assessment, cancellationToken),
            BasicAttackBattleActionCommand attack => ExecuteEffects(
                request,
                attack.Kind,
                attack.ActionId,
                attack.Targeting,
                attack.SelectedTargetIds,
                [BasicAttackEffect(attack.BasicAttack)],
                ActionTurnConsumptionKind.TurnEconomy,
                assessment),
            AnalyzeBattleActionCommand analyze => ExecuteEffects(
                request,
                analyze.Kind,
                ContentId.Parse("analyze"),
                SingleAnyTargeting(),
                [analyze.TargetId],
                [new AnalyzeEffectDefinition(analyze.Layers)],
                ActionTurnConsumptionKind.Normal,
                assessment),
            EscapeAttemptBattleActionCommand escape => ExecuteEscape(request, escape, assessment),
            GuardBattleActionCommand => ExecuteGuard(request),
            PassBattleActionCommand => Executed(
                BattleActionKind.Pass,
                ActionTurnConsumption.Pass,
                events: [new BattleActionEvent(BattleActionEventKind.Executed, "Action passed.", request.Actor.InstanceId)]),
            HostedEntitySwapBattleActionCommand hostedEntity => ExecutePartyRoster(hostedEntity.Kind, assessment.PartyRosterTransition),
            CompanionDeployBattleActionCommand deploy => ExecutePartyRoster(deploy.Kind, assessment.PartyRosterTransition),
            CompanionRecallBattleActionCommand returned => ExecutePartyRoster(returned.Kind, assessment.PartyRosterTransition),
            CompanionSwapBattleActionCommand swap => ExecutePartyRoster(swap.Kind, assessment.PartyRosterTransition),
            HostMediatedBattleActionCommand mediated => ExecuteHostMediated(request, mediated),
            _ => Rejected(request.Command.Kind, [new BattleActionDiagnostic(BattleActionDiagnosticCode.UnsupportedAction, "The action command is not supported.")])
        });
    }

    private BattleActionAssessment AssessSkill(BattleActionExecutionRequest request, SkillBattleActionCommand command)
    {
        SkillExecutionAssessment skill = _skills.Assess(new SkillExecutionRequest(
            command.Skill,
            request.Actor,
            request.Participants,
            request.Environment,
            command.SelectedTargetIds));
        return CreateAssessment(
            request,
            command.Kind,
            skill.Diagnostics.Select(ToActionDiagnostic),
            skill.TargetIds,
            hasResolvedTargets: skill.HasResolvedTargets,
            isUntargeted: skill.IsUntargeted,
            turnConsumption: skill.CanExecute ? ActionTurnConsumption.Normal : ActionTurnConsumption.None,
            skillAssessment: skill);
    }

    private BattleActionAssessment AssessItem(BattleActionExecutionRequest request, ItemBattleActionCommand command)
    {
        ItemExecutionAssessment item = _items.Assess(new ItemExecutionRequest(
            command.Item,
            request.Actor,
            request.Participants,
            request.Environment,
            command.SelectedTargetIds));
        List<BattleActionDiagnostic> diagnostics = item.Diagnostics.Select(ToActionDiagnostic).ToList();
        if (request.ItemInventory is not null &&
            !request.ItemInventory.HasAvailable(command.Item.Id, command.Quantity))
        {
            diagnostics.Add(new BattleActionDiagnostic(
                BattleActionDiagnosticCode.ItemUnavailable,
                $"Item '{command.Item.Id}' is not available in the requested quantity."));
        }

        return CreateAssessment(
            request,
            command.Kind,
            diagnostics,
            item.TargetIds,
            hasResolvedTargets: item.HasResolvedTargets,
            isUntargeted: item.IsUntargeted,
            turnConsumption: item.CanExecute && diagnostics.Count == 0
                ? ActionTurnConsumption.Normal
                : ActionTurnConsumption.None,
            itemAssessment: item);
    }

    private BattleActionAssessment AssessEffectAction(
        BattleActionExecutionRequest request,
        BattleActionKind kind,
        ContentId sourceId,
        TargetingDefinition targeting,
        IEnumerable<RuntimeInstanceId> selectedTargetIds,
        IReadOnlyList<EffectDefinition> effects)
    {
        var action = new EffectActionExecutionRequest(
            sourceId,
            request.Actor,
            request.Participants,
            request.Environment,
            targeting,
            selectedTargetIds);
        List<BattleActionDiagnostic> diagnostics = [];
        bool resolved = RuntimeTargetResolver.TryResolve(
            action,
            _services,
            out ResolvedRuntimeTargetSet? targets,
            out string? diagnostic);
        if (!resolved || targets is null)
        {
            diagnostics.Add(new BattleActionDiagnostic(
                BattleActionDiagnosticCode.TargetSelectionInvalid,
                diagnostic ?? "Action target selection failed."));
        }

        foreach (EffectDefinition effect in effects)
        {
            if (!_services.EffectExecutors.Supports(effect.GetType()))
            {
                diagnostics.Add(new BattleActionDiagnostic(
                    BattleActionDiagnosticCode.EffectExecutorMissing,
                    $"No executor is registered for '{effect.GetType().Name}'."));
            }
        }

        return diagnostics.Count == 0
            ? CreateAssessment(
                request,
                kind,
                targetIds: targets!.Targets.Select(target => target.InstanceId),
                hasResolvedTargets: true,
                isUntargeted: targets.IsUntargeted)
            : CreateAssessment(request, kind, diagnostics);
    }

    private BattleActionAssessment AssessPartyRoster(
        BattleActionExecutionRequest request,
        BattleActionKind kind,
        PartyRosterTransitionResult transition) =>
        transition.Applied
            ? CreateAssessment(
                request,
                kind,
                targetIds: transition.AffectedInstanceIds,
                partyRosterTransition: transition)
            : CreateAssessment(
                request,
                kind,
                transition.Diagnostics.Select(diagnostic => new BattleActionDiagnostic(
                    BattleActionDiagnosticCode.PartyRosterRejected,
                    diagnostic.Message)),
                partyRosterTransition: transition);

    private BattleActionExecutionResult ExecuteSkill(
        BattleActionExecutionRequest request,
        SkillBattleActionCommand command,
        BattleActionAssessment assessment)
    {
        if (assessment.SkillAssessment is not SkillExecutionAssessment prepared)
        {
            return InvalidAssessment(command.Kind, "The prepared skill assessment is missing.");
        }

        SkillExecutionRequest skillRequest = prepared.Preparation.Request;
        SkillExecutionResult skill = _skills.Execute(skillRequest, prepared);
        if (skill.Status == SkillExecutionStatus.Rejected)
        {
            return Rejected(command.Kind, skill.Diagnostics.Select(ToActionDiagnostic));
        }

        return new BattleActionExecutionResult(
            skill.Status == SkillExecutionStatus.Interrupted
                ? BattleActionExecutionStatus.Interrupted
                : BattleActionExecutionStatus.Executed,
            command.Kind,
            ActionTurnConsumption.FromTurnEconomy(skill.TurnEconomy),
            skill.Effects,
            events: EffectEvents(request.Actor.InstanceId, command.Skill.Id, skill.Effects),
            escapeRequested: skill.EscapeRequested,
            hostActionRequestIds: skill.HostActionRequestIds,
            committedCostChanges: skill.CommittedCostChanges);
    }

    private BattleActionExecutionResult ExecuteItem(
        BattleActionExecutionRequest request,
        ItemBattleActionCommand command,
        BattleActionAssessment assessment,
        CancellationToken cancellationToken)
    {
        if (assessment.ItemAssessment is not ItemExecutionAssessment prepared)
        {
            return InvalidAssessment(command.Kind, "The prepared item assessment is missing.");
        }

        IItemActionReservation? reservation = null;
        List<BattleActionEvent> events = [];
        RuntimeActorExecutionTransaction transaction;
        try
        {
            transaction = new RuntimeActorExecutionTransaction(request.Actor, request.Participants);
        }
        catch (Exception exception)
        {
            return Rejected(command.Kind,
            [
                new BattleActionDiagnostic(
                    BattleActionDiagnosticCode.ExecutionFailed,
                    $"Item execution could not stage actor state: {exception.Message}")
            ]);
        }

        try
        {
            if (request.ItemInventory is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    reservation = request.ItemInventory.Reserve(command.Item.Id, command.Quantity);
                }
                catch (Exception exception)
                {
                    return Rejected(command.Kind,
                    [
                        new BattleActionDiagnostic(
                            BattleActionDiagnosticCode.ItemReservationFailed,
                            $"Item reservation failed: {exception.Message}")
                    ]);
                }

                events.Add(new BattleActionEvent(
                    BattleActionEventKind.ItemReserved,
                    $"Reserved item '{command.Item.Id}'.",
                    request.Actor.InstanceId,
                    SourceId: command.Item.Id));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ItemExecutionResult item = _items.Execute(new ItemExecutionRequest(
                command.Item,
                transaction.Actor,
                transaction.Participants,
                request.Environment,
                command.SelectedTargetIds),
                prepared);
            if (item.Status == ItemExecutionStatus.Rejected)
            {
                List<BattleActionDiagnostic> diagnostics =
                    item.Diagnostics.Select(ToActionDiagnostic).ToList();
                if (reservation is not null &&
                    !TryRollbackReservation(reservation, out BattleActionDiagnostic? rollbackDiagnostic))
                {
                    diagnostics.Add(rollbackDiagnostic!);
                }
                else if (reservation is not null)
                {
                    events.Add(new BattleActionEvent(
                        BattleActionEventKind.ItemRolledBack,
                        $"Rolled back item '{command.Item.Id}'.",
                        request.Actor.InstanceId,
                        SourceId: command.Item.Id));
                }

                return Rejected(command.Kind, diagnostics, events);
            }

            cancellationToken.ThrowIfCancellationRequested();
            bool committed = false;
            if (item.Consumption == ItemConsumptionDecision.ConsumeOne)
            {
                if (reservation is not null)
                {
                    if (!TryCommitReservation(reservation, out BattleActionDiagnostic? commitDiagnostic))
                    {
                        var diagnostics = new List<BattleActionDiagnostic> { commitDiagnostic! };
                        if (!TryRollbackReservation(reservation, out BattleActionDiagnostic? rollbackDiagnostic))
                        {
                            diagnostics.Add(rollbackDiagnostic!);
                        }
                        else
                        {
                            events.Add(new BattleActionEvent(
                                BattleActionEventKind.ItemRolledBack,
                                $"Rolled back item '{command.Item.Id}'.",
                                request.Actor.InstanceId,
                                SourceId: command.Item.Id));
                        }

                        return Rejected(command.Kind, diagnostics, events);
                    }

                    committed = true;
                    events.Add(new BattleActionEvent(
                        BattleActionEventKind.ItemCommitted,
                        $"Committed item '{command.Item.Id}'.",
                        request.Actor.InstanceId,
                        SourceId: command.Item.Id));
                }
            }
            else
            {
                if (reservation is not null)
                {
                    if (!TryRollbackReservation(reservation, out BattleActionDiagnostic? rollbackDiagnostic))
                    {
                        return Rejected(command.Kind, [rollbackDiagnostic!], events);
                    }

                    events.Add(new BattleActionEvent(
                        BattleActionEventKind.ItemRolledBack,
                        $"Rolled back item '{command.Item.Id}'.",
                        request.Actor.InstanceId,
                        SourceId: command.Item.Id));
                }
            }

            transaction.Commit();
            events.AddRange(EffectEvents(request.Actor.InstanceId, command.Item.Id, item.Effects));
            return new BattleActionExecutionResult(
                item.Status == ItemExecutionStatus.Interrupted
                    ? BattleActionExecutionStatus.Interrupted
                    : BattleActionExecutionStatus.Executed,
                command.Kind,
                item.EscapeRequested ? ActionTurnConsumption.None : ActionTurnConsumption.Normal,
                item.Effects,
                events: events,
                itemConsumption: item.Consumption,
                itemConsumptionCommitted: committed,
                escapeRequested: item.EscapeRequested,
                hostActionRequestIds: item.HostActionRequestIds);
        }
        catch (OperationCanceledException)
        {
            if (reservation is not null && !reservation.IsCommitted && !reservation.IsRolledBack)
            {
                TryRollbackReservation(reservation, out _);
            }

            throw;
        }
        catch (Exception exception)
        {
            var diagnostics = new List<BattleActionDiagnostic>
            {
                new(
                    BattleActionDiagnosticCode.ExecutionFailed,
                    $"Item action failed before actor-state commit: {exception.Message}")
            };
            if (reservation is not null && !reservation.IsCommitted && !reservation.IsRolledBack &&
                !TryRollbackReservation(reservation, out BattleActionDiagnostic? rollbackDiagnostic))
            {
                diagnostics.Add(rollbackDiagnostic!);
            }

            return Rejected(command.Kind, diagnostics, events);
        }
    }

    private BattleActionExecutionResult ExecuteEffects(
        BattleActionExecutionRequest request,
        BattleActionKind kind,
        ContentId sourceId,
        TargetingDefinition targeting,
        IEnumerable<RuntimeInstanceId> selectedTargetIds,
        IReadOnlyList<EffectDefinition> effects,
        ActionTurnConsumptionKind defaultTurnKind,
        BattleActionAssessment assessment)
    {
        if (!assessment.HasResolvedTargets ||
            !PreparedTargetResolver.TryRebind(
                request.Participants,
                assessment.TargetIds,
                assessment.IsUntargeted,
                out ResolvedRuntimeTargetSet? targets) ||
            targets is null)
        {
            return InvalidAssessment(kind, "The prepared action targets no longer match the execution request.");
        }

        OrderedEffectExecution execution;
        RuntimeActorExecutionTransaction transaction;
        try
        {
            transaction = new RuntimeActorExecutionTransaction(request.Actor, request.Participants);
            var stagedAction = new EffectActionExecutionRequest(
                sourceId,
                transaction.Actor,
                transaction.Participants,
                request.Environment,
                targeting,
                selectedTargetIds);
            execution = _orderedEffects.Execute(stagedAction, effects, transaction.Map(targets));
        }
        catch (Exception exception)
        {
            return Rejected(kind,
            [
                new BattleActionDiagnostic(
                    BattleActionDiagnosticCode.ExecutionFailed,
                    $"Action execution failed before commit: {exception.Message}")
            ]);
        }

        transaction.Commit();
        TurnEconomyResolution turnEconomy = AggregateTurnEconomy(execution.Effects);
        ActionTurnConsumption turn = defaultTurnKind == ActionTurnConsumptionKind.TurnEconomy
            ? ActionTurnConsumption.FromTurnEconomy(turnEconomy)
            : new ActionTurnConsumption(defaultTurnKind);
        return new BattleActionExecutionResult(
            execution.Interrupted ? BattleActionExecutionStatus.Interrupted : BattleActionExecutionStatus.Executed,
            kind,
            turn,
            execution.Effects,
            events: EffectEvents(request.Actor.InstanceId, sourceId, execution.Effects),
            escapeRequested: execution.Effects.Any(effect => effect.EscapeRequested));
    }

    private BattleActionExecutionResult ExecuteEscape(
        BattleActionExecutionRequest request,
        EscapeAttemptBattleActionCommand command,
        BattleActionAssessment assessment)
    {
        BattleActionExecutionResult result = ExecuteEffects(
            request,
            command.Kind,
            command.EligibilityRuleId,
            Untargeted(),
            [],
            [new EscapeEffectDefinition(command.EligibilityRuleId, command.Chance)],
            ActionTurnConsumptionKind.Normal,
            assessment);
        return result.EscapeRequested
            ? result with { TurnConsumption = ActionTurnConsumption.None }
            : result with { TurnConsumption = ActionTurnConsumption.Normal };
    }

    private static BattleActionExecutionResult ExecuteGuard(BattleActionExecutionRequest request)
    {
        request.Actor.SetGuarding(true);
        return Executed(
            BattleActionKind.Guard,
            ActionTurnConsumption.Normal,
            events: [new BattleActionEvent(BattleActionEventKind.Executed, "Actor is guarding.", request.Actor.InstanceId)]);
    }

    private static BattleActionExecutionResult ExecutePartyRoster(
        BattleActionKind kind,
        PartyRosterTransitionResult? transition)
    {
        if (transition is null)
        {
            return InvalidAssessment(kind, "The prepared party/roster transition is missing.");
        }

        if (!transition.Applied)
        {
            return Rejected(kind, transition.Diagnostics.Select(diagnostic => new BattleActionDiagnostic(
                BattleActionDiagnosticCode.PartyRosterRejected,
                diagnostic.Message)));
        }

        return new BattleActionExecutionResult(
            BattleActionExecutionStatus.Executed,
            kind,
            ActionTurnConsumption.Normal,
            partyRosterTransition: transition,
            events: [new BattleActionEvent(
                BattleActionEventKind.PartyRosterTransitioned,
                $"Party roster transition applied: {kind}.")]);
    }

    private static BattleActionExecutionResult ExecuteHostMediated(
        BattleActionExecutionRequest request,
        HostMediatedBattleActionCommand command) =>
        new(
            BattleActionExecutionStatus.Executed,
            command.Kind,
            command.TurnConsumption,
            events: [new BattleActionEvent(
                BattleActionEventKind.HostActionRequested,
                $"Host action '{command.HostActionId}' requested.",
                request.Actor.InstanceId,
                SourceId: command.HostActionId)],
            hostActionRequestIds: [command.HostActionId]);

    private static BattleActionExecutionResult Executed(
        BattleActionKind kind,
        ActionTurnConsumption turnConsumption,
        IEnumerable<BattleActionEvent>? events = null) =>
        new(BattleActionExecutionStatus.Executed, kind, turnConsumption, events: events);

    private static BattleActionExecutionResult Rejected(
        BattleActionKind kind,
        IEnumerable<BattleActionDiagnostic> diagnostics,
        IEnumerable<BattleActionEvent>? events = null) =>
        new(BattleActionExecutionStatus.Rejected, kind, ActionTurnConsumption.None, diagnostics: diagnostics, events: events);

    private static BattleActionExecutionResult InvalidAssessment(
        BattleActionKind kind,
        string message) =>
        Rejected(
            kind,
            [new BattleActionDiagnostic(BattleActionDiagnosticCode.AssessmentInvalid, message)]);

    private BattleActionAssessment CreateAssessment(
        BattleActionExecutionRequest request,
        BattleActionKind kind,
        IEnumerable<BattleActionDiagnostic>? diagnostics = null,
        IEnumerable<RuntimeInstanceId>? targetIds = null,
        bool hasResolvedTargets = false,
        bool isUntargeted = false,
        ActionTurnConsumption? turnConsumption = null,
        SkillExecutionAssessment? skillAssessment = null,
        ItemExecutionAssessment? itemAssessment = null,
        PartyRosterTransitionResult? partyRosterTransition = null) =>
        new(
            _assessmentAuthority,
            request,
            kind,
            diagnostics,
            targetIds,
            hasResolvedTargets,
            isUntargeted,
            turnConsumption,
            skillAssessment,
            itemAssessment,
            partyRosterTransition);

    private static BattleActionDiagnostic ToActionDiagnostic(SkillExecutionDiagnostic diagnostic) =>
        new(
            BattleActionDiagnosticCode.SkillRejected,
            diagnostic.Message,
            diagnostic.EffectIndex,
            diagnostic.TargetId);

    private static BattleActionDiagnostic ToActionDiagnostic(ItemExecutionDiagnostic diagnostic) =>
        new(
            BattleActionDiagnosticCode.ItemRejected,
            diagnostic.Message,
            diagnostic.EffectIndex,
            diagnostic.TargetId);

    private static bool TryCommitReservation(
        IItemActionReservation reservation,
        out BattleActionDiagnostic? diagnostic)
    {
        try
        {
            ItemActionReservationTransitionResult result = reservation.Commit();
            if (result.Applied && reservation.IsCommitted)
            {
                diagnostic = null;
                return true;
            }

            diagnostic = new BattleActionDiagnostic(
                BattleActionDiagnosticCode.ItemCommitFailed,
                result.Message ?? "Item reservation commit was rejected.");
            return false;
        }
        catch (Exception exception)
        {
            diagnostic = new BattleActionDiagnostic(
                BattleActionDiagnosticCode.ItemCommitFailed,
                $"Item reservation commit failed: {exception.Message}");
            return false;
        }
    }

    private static bool TryRollbackReservation(
        IItemActionReservation reservation,
        out BattleActionDiagnostic? diagnostic)
    {
        try
        {
            ItemActionReservationTransitionResult result = reservation.Rollback();
            if (result.Applied && reservation.IsRolledBack)
            {
                diagnostic = null;
                return true;
            }

            diagnostic = new BattleActionDiagnostic(
                BattleActionDiagnosticCode.ItemRollbackFailed,
                result.Message ?? "Item reservation rollback was rejected.");
            return false;
        }
        catch (Exception exception)
        {
            diagnostic = new BattleActionDiagnostic(
                BattleActionDiagnosticCode.ItemRollbackFailed,
                $"Item reservation rollback failed: {exception.Message}");
            return false;
        }
    }

    private static IReadOnlyList<BattleActionEvent> EffectEvents(
        RuntimeInstanceId actorId,
        ContentId sourceId,
        IEnumerable<EffectExecutionResult> effects) =>
        Array.AsReadOnly(effects.Select(effect => new BattleActionEvent(
            BattleActionEventKind.EffectResolved,
            $"Effect {effect.EffectIndex} resolved as {effect.Outcome}.",
            actorId,
            effect.TargetId,
            sourceId,
            effect.Value)).ToArray());

    private static TurnEconomyResolution AggregateTurnEconomy(IReadOnlyList<EffectExecutionResult> effects)
    {
        EffectExecutionResult? interruption = effects.FirstOrDefault(effect =>
            effect.TurnEconomyOutcome is TurnEconomyOutcome.Repel or TurnEconomyOutcome.Absorb);
        if (interruption is not null)
        {
            return new TurnEconomyResolution(interruption.TurnEconomyOutcome, effects.Any(effect => effect.IsCritical), true);
        }

        TurnEconomyOutcome outcome = effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Null)
            ? TurnEconomyOutcome.Null
            : effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Miss)
                ? TurnEconomyOutcome.Miss
                : effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Weakness)
                    ? TurnEconomyOutcome.Weakness
                    : effects.Any(effect => effect.IsCritical)
                        ? TurnEconomyOutcome.Critical
                        : TurnEconomyOutcome.Normal;

        return new TurnEconomyResolution(outcome, effects.Any(effect => effect.IsCritical), false);
    }

    private static TargetingDefinition SingleAnyTargeting() =>
        new(TargetRelation.Any, TargetSelection.Single, TargetLifeState.Any, true);

    private static TargetingDefinition Untargeted() =>
        new(TargetRelation.None, TargetSelection.None, TargetLifeState.Any, true);

    private static DamageEffectDefinition BasicAttackEffect(EquipmentBasicAttackDefinition basicAttack) =>
        new(
            basicAttack.Element,
            basicAttack.Power,
            basicAttack.Accuracy,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1));
}
