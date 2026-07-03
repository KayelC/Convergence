using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Execution;

public enum BattleActionKind
{
    BasicAttack,
    Skill,
    Item,
    Guard,
    Pass,
    Analyze,
    PersonaSwap,
    DemonSummon,
    DemonReturn,
    DemonSwap,
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
    PressTurn,
    TerminatePhase
}

public enum BattleActionDiagnosticCode
{
    SkillRejected,
    ItemRejected,
    ItemUnavailable,
    TargetSelectionInvalid,
    EffectExecutorMissing,
    PartyStockRejected,
    UnsupportedAction,
    HostActionRequired
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
    PartyStockTransitioned,
    HostActionRequested
}

public sealed record ActionTurnConsumption(
    ActionTurnConsumptionKind Kind,
    PressTurnResolution? PressTurn = null)
{
    public static ActionTurnConsumption None { get; } = new(ActionTurnConsumptionKind.None);
    public static ActionTurnConsumption Normal { get; } = new(ActionTurnConsumptionKind.Normal);
    public static ActionTurnConsumption Pass { get; } = new(ActionTurnConsumptionKind.Pass);
    public static ActionTurnConsumption TerminatePhase { get; } = new(ActionTurnConsumptionKind.TerminatePhase);

    public static ActionTurnConsumption FromPressTurn(PressTurnResolution resolution) =>
        new(ActionTurnConsumptionKind.PressTurn, resolution);
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

public abstract record PartyStockBattleActionCommand : BattleActionCommand
{
    private protected PartyStockBattleActionCommand(BattleActionKind kind, RuntimePartyStockSnapshot snapshot)
        : base(kind)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public RuntimePartyStockSnapshot Snapshot { get; }
}

public sealed record PersonaSwapBattleActionCommand : PartyStockBattleActionCommand
{
    public PersonaSwapBattleActionCommand(RuntimePartyStockSnapshot snapshot, RuntimeInstanceId personaInstanceId)
        : base(BattleActionKind.PersonaSwap, snapshot)
    {
        PersonaInstanceId = personaInstanceId;
    }

    public RuntimeInstanceId PersonaInstanceId { get; }
}

public sealed record DemonSummonBattleActionCommand : PartyStockBattleActionCommand
{
    public DemonSummonBattleActionCommand(RuntimePartyStockSnapshot snapshot, RuntimeInstanceId demonInstanceId)
        : base(BattleActionKind.DemonSummon, snapshot)
    {
        DemonInstanceId = demonInstanceId;
    }

    public RuntimeInstanceId DemonInstanceId { get; }
}

public sealed record DemonReturnBattleActionCommand : PartyStockBattleActionCommand
{
    public DemonReturnBattleActionCommand(RuntimePartyStockSnapshot snapshot, RuntimeInstanceId demonInstanceId)
        : base(BattleActionKind.DemonReturn, snapshot)
    {
        DemonInstanceId = demonInstanceId;
    }

    public RuntimeInstanceId DemonInstanceId { get; }
}

public sealed record DemonSwapBattleActionCommand : PartyStockBattleActionCommand
{
    public DemonSwapBattleActionCommand(
        RuntimePartyStockSnapshot snapshot,
        RuntimeInstanceId activeDemonInstanceId,
        RuntimeInstanceId standbyDemonInstanceId)
        : base(BattleActionKind.DemonSwap, snapshot)
    {
        ActiveDemonInstanceId = activeDemonInstanceId;
        StandbyDemonInstanceId = standbyDemonInstanceId;
    }

    public RuntimeInstanceId ActiveDemonInstanceId { get; }
    public RuntimeInstanceId StandbyDemonInstanceId { get; }
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
        BattleActionKind kind,
        IEnumerable<BattleActionDiagnostic>? diagnostics = null,
        IEnumerable<RuntimeInstanceId>? targetIds = null,
        ActionTurnConsumption? turnConsumption = null,
        SkillExecutionAssessment? skillAssessment = null,
        ItemExecutionAssessment? itemAssessment = null,
        PartyStockTransitionResult? partyStockTransition = null)
    {
        Kind = kind;
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
        TargetIds = Array.AsReadOnly(targetIds?.ToArray() ?? []);
        TurnConsumption = turnConsumption ?? ActionTurnConsumption.Normal;
        SkillAssessment = skillAssessment;
        ItemAssessment = itemAssessment;
        PartyStockTransition = partyStockTransition;
    }

    public BattleActionKind Kind { get; }
    public bool CanExecute => Diagnostics.Count == 0;
    public IReadOnlyList<BattleActionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<RuntimeInstanceId> TargetIds { get; }
    public ActionTurnConsumption TurnConsumption { get; init; }
    public SkillExecutionAssessment? SkillAssessment { get; }
    public ItemExecutionAssessment? ItemAssessment { get; }
    public PartyStockTransitionResult? PartyStockTransition { get; }
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
        PartyStockTransitionResult? partyStockTransition = null,
        IEnumerable<ContentId>? hostActionRequestIds = null)
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
        PartyStockTransition = partyStockTransition;
        HostActionRequestIds = Array.AsReadOnly(
            (hostActionRequestIds ?? Effects.SelectMany(effect => effect.HostActionRequestIds)).ToArray());
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
    public PartyStockTransitionResult? PartyStockTransition { get; }
    public IReadOnlyList<ContentId> HostActionRequestIds { get; }
}

public interface IItemActionReservation
{
    ContentId ItemId { get; }
    int Quantity { get; }
    bool IsCommitted { get; }
    bool IsRolledBack { get; }
    void Commit();
    void Rollback();
}

public interface IItemActionInventory
{
    bool HasAvailable(ContentId itemId, int quantity);
    IItemActionReservation Reserve(ContentId itemId, int quantity);
}

public interface IBattleActionExecutor
{
    BattleActionAssessment Assess(BattleActionExecutionRequest request);
    ValueTask<BattleActionExecutionResult> ExecuteAsync(
        BattleActionExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class BattleActionExecutor : IBattleActionExecutor
{
    private readonly ISkillExecutor _skills;
    private readonly IItemExecutor _items;
    private readonly BattleExecutionServices _services;
    private readonly IPartyStockTransitionService _partyStock;
    private readonly OrderedEffectExecutor _orderedEffects;

    public BattleActionExecutor(
        ISkillExecutor skills,
        IItemExecutor items,
        BattleExecutionServices services,
        IPartyStockTransitionService? partyStock = null)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _partyStock = partyStock ?? new PartyStockTransitionService();
        _orderedEffects = new OrderedEffectExecutor(_services, _services.EffectExecutors);
    }

    public BattleActionAssessment Assess(BattleActionExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
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
            GuardBattleActionCommand => new BattleActionAssessment(BattleActionKind.Guard, turnConsumption: ActionTurnConsumption.Normal),
            PassBattleActionCommand => new BattleActionAssessment(BattleActionKind.Pass, turnConsumption: ActionTurnConsumption.Pass),
            PersonaSwapBattleActionCommand persona => AssessPartyStock(persona.Kind, _partyStock.SwapActivePersona(
                new SwapActivePersonaRequest(persona.Snapshot, persona.PersonaInstanceId))),
            DemonSummonBattleActionCommand summon => AssessPartyStock(summon.Kind, _partyStock.SummonDemon(
                new SummonDemonRequest(summon.Snapshot, summon.DemonInstanceId))),
            DemonReturnBattleActionCommand returned => AssessPartyStock(returned.Kind, _partyStock.ReturnDemon(
                new ReturnDemonRequest(returned.Snapshot, returned.DemonInstanceId))),
            DemonSwapBattleActionCommand swap => AssessPartyStock(swap.Kind, _partyStock.SwapActiveDemon(
                new SwapActiveDemonRequest(swap.Snapshot, swap.ActiveDemonInstanceId, swap.StandbyDemonInstanceId))),
            HostMediatedBattleActionCommand mediated => new BattleActionAssessment(
                mediated.Kind,
                targetIds: [],
                turnConsumption: mediated.TurnConsumption),
            _ => new BattleActionAssessment(
                request.Command.Kind,
                [new BattleActionDiagnostic(BattleActionDiagnosticCode.UnsupportedAction, "The action command is not supported.")],
                turnConsumption: ActionTurnConsumption.None)
        };
    }

    public ValueTask<BattleActionExecutionResult> ExecuteAsync(
        BattleActionExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        BattleActionAssessment assessment = Assess(request);
        if (!assessment.CanExecute)
        {
            return new ValueTask<BattleActionExecutionResult>(Rejected(request.Command.Kind, assessment.Diagnostics));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<BattleActionExecutionResult>(request.Command switch
        {
            SkillBattleActionCommand skill => ExecuteSkill(request, skill),
            ItemBattleActionCommand item => ExecuteItem(request, item, cancellationToken),
            BasicAttackBattleActionCommand attack => ExecuteEffects(
                request,
                attack.Kind,
                attack.ActionId,
                attack.Targeting,
                attack.SelectedTargetIds,
                [BasicAttackEffect(attack.BasicAttack)],
                ActionTurnConsumptionKind.PressTurn),
            AnalyzeBattleActionCommand analyze => ExecuteEffects(
                request,
                analyze.Kind,
                ContentId.Parse("analyze"),
                SingleAnyTargeting(),
                [analyze.TargetId],
                [new AnalyzeEffectDefinition(analyze.Layers)],
                ActionTurnConsumptionKind.Normal),
            EscapeAttemptBattleActionCommand escape => ExecuteEscape(request, escape),
            GuardBattleActionCommand => ExecuteGuard(request),
            PassBattleActionCommand => Executed(
                BattleActionKind.Pass,
                ActionTurnConsumption.Pass,
                events: [new BattleActionEvent(BattleActionEventKind.Executed, "Action passed.", request.Actor.InstanceId)]),
            PersonaSwapBattleActionCommand persona => ExecutePartyStock(
                persona.Kind,
                _partyStock.SwapActivePersona(new SwapActivePersonaRequest(persona.Snapshot, persona.PersonaInstanceId))),
            DemonSummonBattleActionCommand summon => ExecutePartyStock(
                summon.Kind,
                _partyStock.SummonDemon(new SummonDemonRequest(summon.Snapshot, summon.DemonInstanceId))),
            DemonReturnBattleActionCommand returned => ExecutePartyStock(
                returned.Kind,
                _partyStock.ReturnDemon(new ReturnDemonRequest(returned.Snapshot, returned.DemonInstanceId))),
            DemonSwapBattleActionCommand swap => ExecutePartyStock(
                swap.Kind,
                _partyStock.SwapActiveDemon(new SwapActiveDemonRequest(
                    swap.Snapshot,
                    swap.ActiveDemonInstanceId,
                    swap.StandbyDemonInstanceId))),
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
        return new BattleActionAssessment(
            command.Kind,
            skill.Diagnostics.Select(ToActionDiagnostic),
            skill.TargetIds,
            skill.CanExecute ? ActionTurnConsumption.Normal : ActionTurnConsumption.None,
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

        return new BattleActionAssessment(
            command.Kind,
            diagnostics,
            item.TargetIds,
            item.CanExecute && diagnostics.Count == 0 ? ActionTurnConsumption.Normal : ActionTurnConsumption.None,
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
            ? new BattleActionAssessment(kind, targetIds: targets!.Targets.Select(target => target.InstanceId))
            : new BattleActionAssessment(kind, diagnostics);
    }

    private static BattleActionAssessment AssessPartyStock(
        BattleActionKind kind,
        PartyStockTransitionResult transition) =>
        transition.Applied
            ? new BattleActionAssessment(
                kind,
                targetIds: transition.AffectedInstanceIds,
                partyStockTransition: transition)
            : new BattleActionAssessment(
                kind,
                transition.Diagnostics.Select(diagnostic => new BattleActionDiagnostic(
                    BattleActionDiagnosticCode.PartyStockRejected,
                    diagnostic.Message)),
                partyStockTransition: transition);

    private BattleActionExecutionResult ExecuteSkill(BattleActionExecutionRequest request, SkillBattleActionCommand command)
    {
        SkillExecutionResult skill = _skills.Execute(new SkillExecutionRequest(
            command.Skill,
            request.Actor,
            request.Participants,
            request.Environment,
            command.SelectedTargetIds));
        if (skill.Status == SkillExecutionStatus.Rejected)
        {
            return Rejected(command.Kind, skill.Diagnostics.Select(ToActionDiagnostic));
        }

        return new BattleActionExecutionResult(
            skill.Status == SkillExecutionStatus.Interrupted
                ? BattleActionExecutionStatus.Interrupted
                : BattleActionExecutionStatus.Executed,
            command.Kind,
            ActionTurnConsumption.FromPressTurn(skill.PressTurn),
            skill.Effects,
            events: EffectEvents(request.Actor.InstanceId, command.Skill.Id, skill.Effects),
            escapeRequested: skill.EscapeRequested,
            hostActionRequestIds: skill.HostActionRequestIds);
    }

    private BattleActionExecutionResult ExecuteItem(
        BattleActionExecutionRequest request,
        ItemBattleActionCommand command,
        CancellationToken cancellationToken)
    {
        IItemActionReservation? reservation = null;
        List<BattleActionEvent> events = [];
        try
        {
            if (request.ItemInventory is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                reservation = request.ItemInventory.Reserve(command.Item.Id, command.Quantity);
                events.Add(new BattleActionEvent(
                    BattleActionEventKind.ItemReserved,
                    $"Reserved item '{command.Item.Id}'.",
                    request.Actor.InstanceId,
                    SourceId: command.Item.Id));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ItemExecutionResult item = _items.Execute(new ItemExecutionRequest(
                command.Item,
                request.Actor,
                request.Participants,
                request.Environment,
                command.SelectedTargetIds));
            if (item.Status == ItemExecutionStatus.Rejected)
            {
                reservation?.Rollback();
                if (reservation is not null)
                {
                    events.Add(new BattleActionEvent(
                        BattleActionEventKind.ItemRolledBack,
                        $"Rolled back item '{command.Item.Id}'.",
                        request.Actor.InstanceId,
                        SourceId: command.Item.Id));
                }

                return Rejected(command.Kind, item.Diagnostics.Select(ToActionDiagnostic), events);
            }

            bool committed = false;
            if (item.Consumption == ItemConsumptionDecision.ConsumeOne)
            {
                reservation?.Commit();
                committed = reservation is not null;
                if (reservation is not null)
                {
                    events.Add(new BattleActionEvent(
                        BattleActionEventKind.ItemCommitted,
                        $"Committed item '{command.Item.Id}'.",
                        request.Actor.InstanceId,
                        SourceId: command.Item.Id));
                }
            }
            else
            {
                reservation?.Rollback();
                if (reservation is not null)
                {
                    events.Add(new BattleActionEvent(
                        BattleActionEventKind.ItemRolledBack,
                        $"Rolled back item '{command.Item.Id}'.",
                        request.Actor.InstanceId,
                        SourceId: command.Item.Id));
                }
            }

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
        catch
        {
            reservation?.Rollback();
            throw;
        }
    }

    private BattleActionExecutionResult ExecuteEffects(
        BattleActionExecutionRequest request,
        BattleActionKind kind,
        ContentId sourceId,
        TargetingDefinition targeting,
        IEnumerable<RuntimeInstanceId> selectedTargetIds,
        IReadOnlyList<EffectDefinition> effects,
        ActionTurnConsumptionKind defaultTurnKind)
    {
        var action = new EffectActionExecutionRequest(
            sourceId,
            request.Actor,
            request.Participants,
            request.Environment,
            targeting,
            selectedTargetIds);
        if (!RuntimeTargetResolver.TryResolve(action, _services, out ResolvedRuntimeTargetSet? targets, out string? diagnostic) ||
            targets is null)
        {
            return Rejected(kind, [new BattleActionDiagnostic(
                BattleActionDiagnosticCode.TargetSelectionInvalid,
                diagnostic ?? "Action target selection failed.")]);
        }

        foreach (EffectDefinition effect in effects)
        {
            if (!_services.EffectExecutors.Supports(effect.GetType()))
            {
                return Rejected(kind, [new BattleActionDiagnostic(
                    BattleActionDiagnosticCode.EffectExecutorMissing,
                    $"No executor is registered for '{effect.GetType().Name}'.")]);
            }
        }

        OrderedEffectExecution execution = _orderedEffects.Execute(action, effects, targets);
        PressTurnResolution pressTurn = AggregatePressTurn(execution.Effects);
        ActionTurnConsumption turn = defaultTurnKind == ActionTurnConsumptionKind.PressTurn
            ? ActionTurnConsumption.FromPressTurn(pressTurn)
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
        EscapeAttemptBattleActionCommand command)
    {
        BattleActionExecutionResult result = ExecuteEffects(
            request,
            command.Kind,
            command.EligibilityRuleId,
            Untargeted(),
            [],
            [new EscapeEffectDefinition(command.EligibilityRuleId, command.Chance)],
            ActionTurnConsumptionKind.Normal);
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

    private static BattleActionExecutionResult ExecutePartyStock(
        BattleActionKind kind,
        PartyStockTransitionResult transition)
    {
        if (!transition.Applied)
        {
            return Rejected(kind, transition.Diagnostics.Select(diagnostic => new BattleActionDiagnostic(
                BattleActionDiagnosticCode.PartyStockRejected,
                diagnostic.Message)));
        }

        return new BattleActionExecutionResult(
            BattleActionExecutionStatus.Executed,
            kind,
            ActionTurnConsumption.Normal,
            partyStockTransition: transition,
            events: [new BattleActionEvent(
                BattleActionEventKind.PartyStockTransitioned,
                $"Party stock transition applied: {kind}.")]);
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

    private static PressTurnResolution AggregatePressTurn(IReadOnlyList<EffectExecutionResult> effects)
    {
        EffectExecutionResult? interruption = effects.FirstOrDefault(effect =>
            effect.PressTurnOutcome is PressTurnOutcome.Repel or PressTurnOutcome.Absorb);
        if (interruption is not null)
        {
            return new PressTurnResolution(interruption.PressTurnOutcome, effects.Any(effect => effect.IsCritical), true);
        }

        PressTurnOutcome outcome = effects.Any(effect => effect.PressTurnOutcome == PressTurnOutcome.Null)
            ? PressTurnOutcome.Null
            : effects.Any(effect => effect.PressTurnOutcome == PressTurnOutcome.Miss)
                ? PressTurnOutcome.Miss
                : effects.Any(effect => effect.PressTurnOutcome == PressTurnOutcome.Weakness)
                    ? PressTurnOutcome.Weakness
                    : effects.Any(effect => effect.IsCritical)
                        ? PressTurnOutcome.Critical
                        : PressTurnOutcome.Normal;

        return new PressTurnResolution(outcome, effects.Any(effect => effect.IsCritical), false);
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
