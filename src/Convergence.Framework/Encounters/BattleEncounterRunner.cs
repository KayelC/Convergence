using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

public enum BattleEncounterOutcome
{
    Victory,
    Defeat,
    Escape,
    Draw,
    Faulted,
    Cancelled
}

public enum BattleEncounterCommandStatus
{
    Executed,
    Rejected,
    Faulted,
    Cancelled
}

public enum BattleEncounterFaultCode
{
    DuplicateParticipantInstanceId = 0,
    LifecycleExecutionFailed = 1,
    InitiativeExecutionFailed = 2,
    StateSynchronizationFailed = 3,
    TurnEconomyExecutionFailed = 4,
    TurnHandlerExecutionFailed = 5,
    CompletionEvaluationFailed = 6,
    EventPublicationFailed = 7,
    PhaseCommandLimitExceeded = 8,
    ConsecutiveFreeActionLimitExceeded = 9,
    TurnEconomyTransitionInvalid = 10,
    CommandExecutionFaulted = 11,
    CommandRejected = 12,
    ScheduleExecutionFailed = 13,
    ScheduleTransitionInvalid = 14,
    ScheduleTransitionLimitExceeded = 15
}

public sealed record BattleEncounterParticipant
{
    public BattleEncounterParticipant(RuntimeActorState state, string displayName)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? state.InstanceId.ToString() : displayName;
    }

    public RuntimeActorState State { get; }
    public string DisplayName { get; }
    public RuntimeInstanceId InstanceId => State.InstanceId;
    public ContentId TeamId => State.TeamId;
}

public sealed record BattleEncounterParticipantSnapshot
{
    internal BattleEncounterParticipantSnapshot(BattleEncounterParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        State = participant.State.ToSnapshot();
        DisplayName = participant.DisplayName;
    }

    public RuntimeActorSnapshot State { get; }
    public string DisplayName { get; }
    public RuntimeInstanceId InstanceId => State.Identity.InstanceId;
    public ContentId EntityId => State.Identity.EntityDefinitionId;
    public ContentId TeamId => State.Affiliation.TeamId;
    public bool IsDeployed => State.EncounterPresence.IsDeployed;
    public bool IsDefeated => State.Resources
        .Single(resource => resource.ResourceId == State.VitalResourceId)
        .Current <= 0;
}

public sealed record BattleEncounterRequest
{
    public BattleEncounterRequest(
        IEnumerable<BattleEncounterParticipant> participants,
        ContentId contextId,
        ContentId battleKindId,
        ContentId? moonPhaseId,
        int roundLimit)
    {
        BattleEncounterParticipant[] participantSnapshot =
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants));
        if (participantSnapshot.Length == 0)
        {
            throw new ArgumentException(
                "An encounter requires at least one participant.",
                nameof(participants));
        }

        if (participantSnapshot.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Encounter participants cannot contain null entries.",
                nameof(participants));
        }

        if (!contextId.IsValid)
        {
            throw new ArgumentException("Encounter context ID must be valid.", nameof(contextId));
        }

        if (!battleKindId.IsValid)
        {
            throw new ArgumentException("Battle kind ID must be valid.", nameof(battleKindId));
        }

        if (moonPhaseId is ContentId moonPhase && !moonPhase.IsValid)
        {
            throw new ArgumentException("Moon-phase ID must be valid when supplied.", nameof(moonPhaseId));
        }

        if (roundLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roundLimit),
                "Round limit must be positive.");
        }

        Participants = Array.AsReadOnly(participantSnapshot);
        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        RoundLimit = roundLimit;
    }

    public IReadOnlyList<BattleEncounterParticipant> Participants { get; }
    public ContentId ContextId { get; }
    public ContentId BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public int RoundLimit { get; }
}

public sealed record BattleEncounterResult
{
    internal BattleEncounterResult(
        BattleEncounterOutcome outcome,
        ContentId? winningTeamId,
        IEnumerable<BattleEncounterParticipant> participants,
        IEnumerable<BattleEncounterEvent> events,
        string? faultMessage = null,
        BattleEncounterFaultCode? faultCode = null)
    {
        ValidateTerminalShape(outcome, winningTeamId, faultMessage, faultCode);

        Outcome = outcome;
        WinningTeamId = winningTeamId;
        Participants = Array.AsReadOnly(
            (participants ?? throw new ArgumentNullException(nameof(participants)))
            .Select(participant => new BattleEncounterParticipantSnapshot(participant))
            .ToArray());
        Events = Array.AsReadOnly(events.ToArray());
        FaultMessage = faultMessage;
        FaultCode = faultCode;
    }

    public BattleEncounterOutcome Outcome { get; }
    public ContentId? WinningTeamId { get; }
    public IReadOnlyList<BattleEncounterParticipantSnapshot> Participants { get; }
    public IReadOnlyList<BattleEncounterEvent> Events { get; }
    public string? FaultMessage { get; }
    public BattleEncounterFaultCode? FaultCode { get; }

    private static void ValidateTerminalShape(
        BattleEncounterOutcome outcome,
        ContentId? winningTeamId,
        string? faultMessage,
        BattleEncounterFaultCode? faultCode)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        bool requiresWinner = outcome is BattleEncounterOutcome.Victory or BattleEncounterOutcome.Defeat;
        if (requiresWinner != (winningTeamId is not null))
        {
            throw new ArgumentException(
                requiresWinner
                    ? $"A {outcome} result requires a winning team."
                    : $"A {outcome} result cannot carry a winning team.",
                nameof(winningTeamId));
        }

        if (outcome == BattleEncounterOutcome.Faulted)
        {
            if (faultCode is not BattleEncounterFaultCode code || !Enum.IsDefined(code))
            {
                throw new ArgumentException(
                    "A faulted encounter result requires a defined fault code.",
                    nameof(faultCode));
            }
            if (string.IsNullOrWhiteSpace(faultMessage))
            {
                throw new ArgumentException(
                    "A faulted encounter result requires a fault message.",
                    nameof(faultMessage));
            }

            return;
        }

        if (faultCode is not null || faultMessage is not null)
        {
            throw new ArgumentException(
                $"A non-fault {outcome} result cannot carry fault metadata.",
                faultCode is not null ? nameof(faultCode) : nameof(faultMessage));
        }
    }
}

public sealed class BattleEncounterInitiativeRequest
{
    public BattleEncounterInitiativeRequest(
        IEnumerable<BattleEncounterParticipantSnapshot> participants)
    {
        Participants = SnapshotPolicyParticipants(participants, nameof(participants));
    }

    public IReadOnlyList<BattleEncounterParticipantSnapshot> Participants { get; }

    internal static IReadOnlyList<BattleEncounterParticipantSnapshot> SnapshotPolicyParticipants(
        IEnumerable<BattleEncounterParticipantSnapshot> participants,
        string parameterName)
    {
        BattleEncounterParticipantSnapshot[] snapshot =
            participants?.ToArray() ?? throw new ArgumentNullException(parameterName);
        if (snapshot.Length == 0)
        {
            throw new ArgumentException(
                "An encounter decision request requires at least one participant.",
                parameterName);
        }

        if (snapshot.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Encounter decision participants cannot contain null entries.",
                parameterName);
        }

        if (snapshot.Select(participant => participant.InstanceId).Distinct().Count() !=
            snapshot.Length)
        {
            throw new ArgumentException(
                "Encounter decision participant instance IDs must be unique.",
                parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public interface IBattleEncounterInitiativePolicy
{
    IReadOnlyList<ContentId> DetermineTeamOrder(BattleEncounterInitiativeRequest request);
}

public sealed class ParticipantOrderInitiativePolicy : IBattleEncounterInitiativePolicy
{
    public IReadOnlyList<ContentId> DetermineTeamOrder(BattleEncounterInitiativeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Array.AsReadOnly(request.Participants
            .Select(participant => participant.TeamId)
            .Distinct()
            .ToArray());
    }
}

public sealed record BattleEncounterLifecycleRequest(
    BattleEncounterRequest Encounter,
    IReadOnlyList<BattleEncounterParticipant> Participants,
    IReadOnlyList<ContentId> TeamOrder);

public sealed record BattleEncounterTurnLifecycleRequest(
    BattleEncounterRequest Encounter,
    BattleEncounterParticipant Actor,
    IReadOnlyList<BattleEncounterParticipant> Participants,
    bool CanRecallToRoster);

public sealed record BattleEncounterDepartureLifecycleRequest
{
    public BattleEncounterDepartureLifecycleRequest(
        BattleEncounterRequest encounter,
        BattleEncounterParticipant actor,
        IEnumerable<BattleEncounterParticipant> participants,
        BattleStatusDepartureReason reason)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        BattleEncounterParticipant[] participantSnapshot =
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants));
        if (participantSnapshot.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Departure lifecycle participants cannot contain null entries.",
                nameof(participants));
        }
        if (!participantSnapshot.Contains(actor, ReferenceEqualityComparer.Instance))
        {
            throw new ArgumentException(
                "The departing actor must belong to the supplied participant graph.",
                nameof(actor));
        }
        if (!encounter.Participants.SequenceEqual(
                participantSnapshot,
                ReferenceEqualityComparer.Instance))
        {
            throw new ArgumentException(
                "Departure lifecycle participants must match the encounter participant graph.",
                nameof(participants));
        }
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Participants = Array.AsReadOnly(participantSnapshot);
        Reason = reason;
    }

    public BattleEncounterRequest Encounter { get; }
    public BattleEncounterParticipant Actor { get; }
    public IReadOnlyList<BattleEncounterParticipant> Participants { get; }
    public BattleStatusDepartureReason Reason { get; }
}

public interface IBattleEncounterLifecyclePort
{
    ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
        BattleEncounterLifecycleRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
        BattleEncounterLifecycleRequest request,
        ContentId teamId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
        BattleEncounterLifecycleRequest request,
        int roundNumber,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
        BattleEncounterLifecycleRequest request,
        BattleEncounterOutcome outcome,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Extends an encounter lifecycle port with cleanup for actor departures whose
/// semantic cause is known by the encounter runner.
/// </summary>
public interface IBattleEncounterDepartureLifecyclePort
{
    ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessActorDepartureAsync(
        BattleEncounterDepartureLifecycleRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Supplies the lifecycle boundaries that are currently active while an encounter actor executes a command.
/// </summary>
/// <remarks>
/// A lifecycle port implements this interface when timed stat modifiers need to distinguish application during a
/// boundary from the later completion of that same boundary. The encounter runner snapshots the returned values and
/// passes them to the turn handler; it never creates lifecycle sequences itself.
/// </remarks>
public interface IBattleEncounterStatModifierBoundarySource
{
    IReadOnlyList<StatModifierLifecycleBoundary> GetActiveStatModifierBoundaries(
        BattleEncounterTurnLifecycleRequest request);
}

public sealed class NoopBattleEncounterLifecyclePort : IBattleEncounterLifecyclePort
{
    public static NoopBattleEncounterLifecyclePort Instance { get; } = new();

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
        BattleEncounterLifecycleRequest request,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());

    public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default) =>
        new(new BattleTurnStartLifecycleResult(BattleTurnStartOutcome.CanAct, []));

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
        BattleEncounterLifecycleRequest request,
        ContentId teamId,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
        BattleEncounterLifecycleRequest request,
        int roundNumber,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
        BattleEncounterLifecycleRequest request,
        BattleEncounterOutcome outcome,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());
}

public sealed record BattleEncounterTurnRequest
{
    public BattleEncounterTurnRequest(
        BattleEncounterRequest encounter,
        BattleEncounterParticipant actor,
        IReadOnlyList<BattleEncounterParticipant> participants,
        BattleTurnStartOutcome turnStartOutcome,
        BattleTurnEconomySnapshot turnEconomyState)
        : this(
            encounter,
            actor,
            participants,
            new BattleTurnStartRestriction(turnStartOutcome),
            turnEconomyState,
            activeStatModifierBoundaries: null)
    {
    }

    public BattleEncounterTurnRequest(
        BattleEncounterRequest encounter,
        BattleEncounterParticipant actor,
        IReadOnlyList<BattleEncounterParticipant> participants,
        BattleTurnStartOutcome turnStartOutcome,
        BattleTurnEconomySnapshot turnEconomyState,
        IEnumerable<StatModifierLifecycleBoundary>? activeStatModifierBoundaries)
        : this(
            encounter,
            actor,
            participants,
            new BattleTurnStartRestriction(turnStartOutcome),
            turnEconomyState,
            activeStatModifierBoundaries)
    {
    }

    public BattleEncounterTurnRequest(
        BattleEncounterRequest encounter,
        BattleEncounterParticipant actor,
        IReadOnlyList<BattleEncounterParticipant> participants,
        BattleTurnStartRestriction turnStartRestriction,
        BattleTurnEconomySnapshot turnEconomyState)
        : this(
            encounter,
            actor,
            participants,
            turnStartRestriction,
            turnEconomyState,
            activeStatModifierBoundaries: null)
    {
    }

    public BattleEncounterTurnRequest(
        BattleEncounterRequest encounter,
        BattleEncounterParticipant actor,
        IReadOnlyList<BattleEncounterParticipant> participants,
        BattleTurnStartRestriction turnStartRestriction,
        BattleTurnEconomySnapshot turnEconomyState,
        IEnumerable<StatModifierLifecycleBoundary>? activeStatModifierBoundaries)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        TurnStartRestriction = turnStartRestriction ?? throw new ArgumentNullException(nameof(turnStartRestriction));
        TurnEconomyState = turnEconomyState ?? throw new ArgumentNullException(nameof(turnEconomyState));
        ActiveStatModifierBoundaries = SnapshotActiveStatModifierBoundaries(
            activeStatModifierBoundaries,
            nameof(activeStatModifierBoundaries));
    }

    public BattleEncounterRequest Encounter { get; }
    public BattleEncounterParticipant Actor { get; }
    public IReadOnlyList<BattleEncounterParticipant> Participants { get; }
    public BattleTurnStartRestriction TurnStartRestriction { get; }
    public BattleTurnStartOutcome TurnStartOutcome => TurnStartRestriction.Outcome;
    public IReadOnlyList<ContentId> AllowedActionIds => TurnStartRestriction.AllowedActionIds;
    public BattleTurnEconomySnapshot TurnEconomyState { get; }
    public IReadOnlyList<StatModifierLifecycleBoundary> ActiveStatModifierBoundaries { get; }

    internal static IReadOnlyList<StatModifierLifecycleBoundary> SnapshotActiveStatModifierBoundaries(
        IEnumerable<StatModifierLifecycleBoundary>? boundaries,
        string parameterName)
    {
        StatModifierLifecycleBoundary[] snapshot = (boundaries ?? []).ToArray();
        if (snapshot.Any(boundary => boundary is null) ||
            snapshot.Any(boundary => !boundary.EventId.IsValid || boundary.Sequence <= 0) ||
            snapshot.Select(boundary => boundary.EventId).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Active stat-modifier boundaries must be valid and unique by event ID.",
                parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record BattleEncounterCommandResult
{
    public BattleEncounterCommandResult(
        BattleEncounterCommandStatus status,
        ActionTurnConsumption turnConsumption,
        IEnumerable<BattleEncounterEvent>? events = null,
        BattleEncounterOutcome? requestedOutcome = null,
        ContentId? winningTeamId = null,
        string? faultMessage = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentNullException.ThrowIfNull(turnConsumption);
        if (requestedOutcome is BattleEncounterOutcome outcome && !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedOutcome));
        }

        if (winningTeamId is ContentId teamId && !teamId.IsValid)
        {
            throw new ArgumentException("Winning team ID must be valid when supplied.", nameof(winningTeamId));
        }

        ValidateCommandShape(status, turnConsumption, requestedOutcome, winningTeamId, faultMessage);

        BattleEncounterEvent[] eventSnapshot = events?.ToArray() ?? [];
        if (eventSnapshot.Any(battleEvent => battleEvent is null))
        {
            throw new ArgumentException("Encounter command events cannot contain null entries.", nameof(events));
        }

        Status = status;
        TurnConsumption = turnConsumption;
        Events = Array.AsReadOnly(eventSnapshot);
        RequestedOutcome = requestedOutcome;
        WinningTeamId = winningTeamId;
        FaultMessage = faultMessage;
    }

    public BattleEncounterCommandStatus Status { get; }
    public ActionTurnConsumption TurnConsumption { get; }
    public IReadOnlyList<BattleEncounterEvent> Events { get; }
    public BattleEncounterOutcome? RequestedOutcome { get; }
    public ContentId? WinningTeamId { get; }
    public string? FaultMessage { get; }

    public static BattleEncounterCommandResult Executed(
        ActionTurnConsumption turnConsumption,
        IEnumerable<BattleEncounterEvent>? events = null,
        BattleEncounterOutcome? requestedOutcome = null,
        ContentId? winningTeamId = null) =>
        new(BattleEncounterCommandStatus.Executed, turnConsumption, events, requestedOutcome, winningTeamId);

    public static BattleEncounterCommandResult Faulted(string message, IEnumerable<BattleEncounterEvent>? events = null) =>
        new(BattleEncounterCommandStatus.Faulted, ActionTurnConsumption.None, events, BattleEncounterOutcome.Faulted, faultMessage: message);

    public static BattleEncounterCommandResult Rejected(string message, IEnumerable<BattleEncounterEvent>? events = null) =>
        new(BattleEncounterCommandStatus.Rejected, ActionTurnConsumption.None, events, BattleEncounterOutcome.Faulted, faultMessage: message);

    public static BattleEncounterCommandResult Cancelled(IEnumerable<BattleEncounterEvent>? events = null) =>
        new(BattleEncounterCommandStatus.Cancelled, ActionTurnConsumption.None, events, BattleEncounterOutcome.Cancelled);

    private static void ValidateCommandShape(
        BattleEncounterCommandStatus status,
        ActionTurnConsumption turnConsumption,
        BattleEncounterOutcome? requestedOutcome,
        ContentId? winningTeamId,
        string? faultMessage)
    {
        if (requestedOutcome is BattleEncounterOutcome outcome &&
            !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedOutcome));
        }

        bool hasNoTurnCost = turnConsumption.Kind == ActionTurnConsumptionKind.None;
        switch (status)
        {
            case BattleEncounterCommandStatus.Executed:
                if (requestedOutcome is BattleEncounterOutcome.Faulted or BattleEncounterOutcome.Cancelled)
                {
                    throw new ArgumentException(
                        "An executed command cannot request a fault or cancellation outcome.",
                        nameof(requestedOutcome));
                }

                if (faultMessage is not null)
                {
                    throw new ArgumentException(
                        "An executed command cannot carry a fault message.",
                        nameof(faultMessage));
                }

                break;

            case BattleEncounterCommandStatus.Cancelled:
                RequireNonExecutedShape(
                    status,
                    hasNoTurnCost,
                    requestedOutcome,
                    BattleEncounterOutcome.Cancelled,
                    winningTeamId,
                    faultMessage,
                    requiresFaultMessage: false);
                break;

            case BattleEncounterCommandStatus.Rejected:
            case BattleEncounterCommandStatus.Faulted:
                RequireNonExecutedShape(
                    status,
                    hasNoTurnCost,
                    requestedOutcome,
                    BattleEncounterOutcome.Faulted,
                    winningTeamId,
                    faultMessage,
                    requiresFaultMessage: true);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (winningTeamId is not null &&
            requestedOutcome is not BattleEncounterOutcome.Victory and not BattleEncounterOutcome.Defeat)
        {
            throw new ArgumentException(
                "A winning team ID is valid only for victory or defeat outcomes.",
                nameof(winningTeamId));
        }

        if (requestedOutcome is BattleEncounterOutcome.Victory or BattleEncounterOutcome.Defeat &&
            winningTeamId is null)
        {
            throw new ArgumentException(
                "Victory and defeat outcomes require a winning team ID.",
                nameof(winningTeamId));
        }
    }

    private static void RequireNonExecutedShape(
        BattleEncounterCommandStatus status,
        bool hasNoTurnCost,
        BattleEncounterOutcome? requestedOutcome,
        BattleEncounterOutcome requiredOutcome,
        ContentId? winningTeamId,
        string? faultMessage,
        bool requiresFaultMessage)
    {
        if (!hasNoTurnCost)
        {
            throw new ArgumentException(
                $"A {status} command cannot consume a turn.",
                nameof(TurnConsumption));
        }

        if (requestedOutcome != requiredOutcome)
        {
            throw new ArgumentException(
                $"A {status} command must request the {requiredOutcome} outcome.",
                nameof(requestedOutcome));
        }

        if (winningTeamId is not null)
        {
            throw new ArgumentException(
                $"A {status} command cannot identify a winning team.",
                nameof(winningTeamId));
        }

        if (requiresFaultMessage && string.IsNullOrWhiteSpace(faultMessage))
        {
            throw new ArgumentException(
                $"A {status} command requires a fault message.",
                nameof(faultMessage));
        }

        if (!requiresFaultMessage && faultMessage is not null)
        {
            throw new ArgumentException(
                $"A {status} command cannot carry a fault message.",
                nameof(faultMessage));
        }
    }
}

public interface IBattleEncounterTurnHandler
{
    ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
        BattleEncounterTurnRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class BattleEncounterCompletionRequest
{
    public BattleEncounterCompletionRequest(
        IEnumerable<BattleEncounterParticipantSnapshot> participants,
        BattleEncounterParticipantSnapshot? lastActor = null)
    {
        Participants =
            BattleEncounterInitiativeRequest.SnapshotPolicyParticipants(
                participants,
                nameof(participants));
        if (lastActor is not null &&
            !Participants.Any(participant =>
                participant.InstanceId == lastActor.InstanceId))
        {
            throw new ArgumentException(
                "The last actor must belong to the completion participant graph.",
                nameof(lastActor));
        }

        LastActor = lastActor;
    }

    public IReadOnlyList<BattleEncounterParticipantSnapshot> Participants { get; }
    public BattleEncounterParticipantSnapshot? LastActor { get; }
}

public sealed record BattleEncounterCompletion(
    bool IsComplete,
    BattleEncounterOutcome Outcome = BattleEncounterOutcome.Draw,
    ContentId? WinningTeamId = null,
    string? Message = null);

public interface IBattleEncounterCompletionPolicy
{
    BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request);
}

public sealed class LastTeamStandingCompletionPolicy : IBattleEncounterCompletionPolicy
{
    public BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ContentId[] livingTeams = request.Participants
            .Where(participant => participant.IsDeployed && !participant.IsDefeated)
            .Select(participant => participant.TeamId)
            .Distinct()
            .ToArray();

        return livingTeams.Length switch
        {
            0 => new BattleEncounterCompletion(true, BattleEncounterOutcome.Draw),
            1 => new BattleEncounterCompletion(true, BattleEncounterOutcome.Victory, livingTeams[0]),
            _ => new BattleEncounterCompletion(false)
        };
    }
}

public interface IBattleEncounterStateSynchronizer
{
    void Synchronize(IReadOnlyList<BattleEncounterParticipant> participants);
}

public sealed class NoopBattleEncounterStateSynchronizer : IBattleEncounterStateSynchronizer
{
    public static NoopBattleEncounterStateSynchronizer Instance { get; } = new();
    public void Synchronize(IReadOnlyList<BattleEncounterParticipant> participants)
    {
    }
}

/// <summary>
/// Observes canonical encounter events after the runner records their sequence and payload.
/// A publication failure faults the encounter but cannot remove or renumber recorded evidence.
/// </summary>
public interface IBattleEncounterEventSink
{
    /// <summary>Observes one canonical event without receiving encounter mutation authority.</summary>
    ValueTask PublishAsync(BattleEncounterEvent battleEvent, CancellationToken cancellationToken = default);
}

public sealed class NoopBattleEncounterEventSink : IBattleEncounterEventSink
{
    public static NoopBattleEncounterEventSink Instance { get; } = new();
    public ValueTask PublishAsync(BattleEncounterEvent battleEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

public sealed class BattleEncounterServices
{
    public BattleEncounterServices(
        IBattleEncounterInitiativePolicy initiative,
        IBattleEncounterSchedulePolicy schedule,
        IBattleEncounterLifecyclePort lifecycle,
        IBattleEncounterTurnHandler turnHandler,
        IBattleEncounterCompletionPolicy completion,
        Func<IBattleTurnEconomy> turnEconomyFactory,
        BattlePhaseProgressPolicy phaseProgress,
        BattleEncounterProgressPolicy encounterProgress,
        IBattleEncounterStateSynchronizer? synchronizer = null,
        IBattleEncounterEventSink? events = null)
    {
        Initiative = initiative ?? throw new ArgumentNullException(nameof(initiative));
        Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        TurnHandler = turnHandler ?? throw new ArgumentNullException(nameof(turnHandler));
        Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        TurnEconomyFactory = turnEconomyFactory ?? throw new ArgumentNullException(nameof(turnEconomyFactory));
        PhaseProgress = phaseProgress ?? throw new ArgumentNullException(nameof(phaseProgress));
        EncounterProgress = encounterProgress ?? throw new ArgumentNullException(nameof(encounterProgress));
        Synchronizer = synchronizer ?? NoopBattleEncounterStateSynchronizer.Instance;
        Events = events ?? NoopBattleEncounterEventSink.Instance;
    }

    public IBattleEncounterInitiativePolicy Initiative { get; }
    public IBattleEncounterSchedulePolicy Schedule { get; }
    public IBattleEncounterLifecyclePort Lifecycle { get; }
    public IBattleEncounterTurnHandler TurnHandler { get; }
    public IBattleEncounterCompletionPolicy Completion { get; }
    public Func<IBattleTurnEconomy> TurnEconomyFactory { get; }
    public BattlePhaseProgressPolicy PhaseProgress { get; }
    public BattleEncounterProgressPolicy EncounterProgress { get; }
    public IBattleEncounterStateSynchronizer Synchronizer { get; }
    public IBattleEncounterEventSink Events { get; }
}

public interface IBattleEncounterRunner
{
    ValueTask<BattleEncounterResult> RunAsync(
        BattleEncounterRequest request,
        BattleEncounterServices services,
        CancellationToken cancellationToken = default);
}

/// <summary>Orchestrates an encounter through injected command, lifecycle, action, and event ports.</summary>
public sealed class BattleEncounterRunner : IBattleEncounterRunner
{
    /// <summary>
    /// Compatibility-only synchronous entry point for callers that do not require synchronization-context affinity.
    /// UI and engine hosts must await <see cref="RunAsync"/>.
    /// </summary>
    public BattleEncounterResult Run(BattleEncounterRequest request, BattleEncounterServices services)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        SynchronizationContext? callerContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            return RunAsync(request, services).AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(callerContext);
        }
    }

    public async ValueTask<BattleEncounterResult> RunAsync(
        BattleEncounterRequest request,
        BattleEncounterServices services,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunCoreAsync(request, services, cancellationToken).ConfigureAwait(false);
        }
        catch (BattleEncounterPortException failure)
        {
            return await failure.FinalizeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<BattleEncounterResult> RunCoreAsync(
        BattleEncounterRequest request,
        BattleEncounterServices services,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        if (request.RoundLimit <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Round limit must be positive.");
        if (request.Participants.Count == 0) throw new ArgumentException("A battle requires participants.", nameof(request));

        var runState = new EncounterRunState(request.Participants);
        var runContext = new EncounterRunContext(
            request,
            services,
            cancellationToken,
            runState);
        EncounterPortInvoker portInvoker = runContext.PortInvoker;

        BattleStartPhaseResult battleStart =
            await runContext.RunBattleStartPhaseAsync().ConfigureAwait(false);
        if (battleStart.TerminalResult is BattleEncounterResult terminalResult)
        {
            return terminalResult;
        }

        BattleEncounterScheduleCursor schedule = battleStart.Schedule!;
        while (!schedule.IsComplete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (schedule.Step is not BattleEncounterRoundStartedScheduleStep roundStarted)
            {
                return await runContext.FaultDuringBattleAsync(
                        $"Encounter schedule expected a round-start step but received " +
                        $"'{schedule.Step?.GetType().Name ?? "<none>"}'.",
                        faultCode: BattleEncounterFaultCode.ScheduleTransitionInvalid)
                    .ConfigureAwait(false);
            }

            int round = roundStarted.RoundNumber;
            runState.FinalRoundNumber = round;
            await runContext.AddAsync(
                    BattleEncounterEventKind.RoundStarted,
                    new BattleRoundStartedEventPayload(round),
                    $"Round {round} started.")
                .ConfigureAwait(false);

            runContext.Synchronize();
            schedule = runContext.AdvanceSchedule(
                schedule,
                BattleEncounterScheduleStepOutcome.BoundaryCompleted());
            while (!schedule.IsComplete &&
                   schedule.Step is BattleEncounterPhaseStartedScheduleStep phaseStarted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ContentId teamId = phaseStarted.TeamId;
                BattleEncounterTurnEconomyStart economyStart =
                    phaseStarted.TurnEconomyStart
                    ?? throw new InvalidOperationException(
                        "A team-phase start must select a turn-economy scope.");

                cancellationToken.ThrowIfCancellationRequested();
                IBattleTurnEconomy turnEconomy = portInvoker.Invoke(
                    BattleEncounterFaultCode.TurnEconomyExecutionFailed,
                    "turn-economy-factory",
                    () => services.TurnEconomyFactory()
                          ?? throw new InvalidOperationException("The turn-economy factory returned null."));
                portInvoker.InvokeAction(
                    BattleEncounterFaultCode.TurnEconomyExecutionFailed,
                    "turn-economy-start",
                    () => turnEconomy.StartPhase(economyStart.ActiveActorCount));
                BattleTurnEconomySnapshot phaseStartState = runContext.CaptureTurnEconomySnapshot(turnEconomy);
                bool hasTurnsRemaining = runContext.HasTurnsRemaining(turnEconomy);
                string? phaseStartFault = ValidateEconomyState(phaseStartState, hasTurnsRemaining);
                if (phaseStartFault is not null)
                {
                    return await runContext.FaultDuringBattleAsync(
                            phaseStartFault,
                            faultCode: BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                        .ConfigureAwait(false);
                }

                BattleTurnEconomySnapshot acceptedEconomyState = phaseStartState;
                await runContext.AddAsync(
                        BattleEncounterEventKind.PhaseStarted,
                        new BattlePhaseStartedEventPayload(teamId, phaseStartState),
                        $"Team {teamId} started a phase using {phaseStartState.EconomyId} " +
                        $"with {phaseStartState.RemainingActions} action(s).")
                    .ConfigureAwait(false);

                int acceptedTurnWindowCount = 0;
                int consecutiveFreeActions = 0;
                runContext.Synchronize();
                schedule = runContext.AdvanceSchedule(
                    schedule,
                    BattleEncounterScheduleStepOutcome.TurnEconomyStarted(
                        phaseStartState,
                        hasTurnsRemaining));
                while (!schedule.IsComplete &&
                       schedule.Step is BattleEncounterCommandWindowScheduleStep commandWindow)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (acceptedTurnWindowCount >= services.PhaseProgress.MaximumCommands)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                $"Team {teamId} exceeded the configured phase turn-window safety limit " +
                                $"of {services.PhaseProgress.MaximumCommands}.",
                                faultCode: BattleEncounterFaultCode.PhaseCommandLimitExceeded)
                            .ConfigureAwait(false);
                    }

                    BattleTurnEconomySnapshot beforeEconomy = runContext.CaptureTurnEconomySnapshot(turnEconomy);
                    bool beforeHasTurnsRemaining = runContext.HasTurnsRemaining(turnEconomy);
                    string? continuityFault = ValidateEconomyAuthority(
                        acceptedEconomyState,
                        beforeEconomy,
                        beforeHasTurnsRemaining);
                    if (continuityFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                continuityFault,
                                faultCode: BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    BattleEncounterParticipant? scheduledActor = request.Participants
                        .SingleOrDefault(participant =>
                            participant.InstanceId == commandWindow.ActorId);
                    if (scheduledActor is null ||
                        scheduledActor.TeamId != commandWindow.TeamId ||
                        commandWindow.TeamId != teamId ||
                        commandWindow.TurnEconomyStart is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                $"Encounter schedule selected actor {commandWindow.ActorId} " +
                                $"for invalid team {commandWindow.TeamId}.",
                                commandWindow.ActorId,
                                BattleEncounterFaultCode.ScheduleTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    if (!scheduledActor.State.IsDeployed || scheduledActor.State.IsDefeated)
                    {
                        schedule = runContext.AdvanceSchedule(
                            schedule,
                            BattleEncounterScheduleStepOutcome.ActorUnavailable(
                                scheduledActor.InstanceId),
                            scheduledActor.InstanceId);
                        continue;
                    }

                    BattleEncounterParticipant actor = scheduledActor;
                    acceptedTurnWindowCount++;
                    await runContext.AddAsync(
                            BattleEncounterEventKind.TurnStarted,
                            new BattleTurnStartedEventPayload(actor.InstanceId, actor.TeamId),
                            $"{actor.DisplayName}'s turn started.")
                        .ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                    BattleTurnStartLifecycleResult? stagedTurnStart = null;
                    IReadOnlyList<BattleEncounterEvent>? turnStartEvents = null;
                    Exception? turnStartFailure = null;
                    string? turnStartEconomyFault = null;
                    using (var turnStartTransaction = new BattleEncounterLifecycleTransaction(
                               request.Participants,
                               services.Lifecycle))
                    {
                        try
                        {
                            BattleEncounterParticipant stagedActor =
                                turnStartTransaction.GetStaged(actor);
                            stagedTurnStart = await services.Lifecycle.ProcessTurnStartAsync(
                                    new BattleEncounterTurnLifecycleRequest(
                                        turnStartTransaction.CreateEncounter(request),
                                        stagedActor,
                                        turnStartTransaction.Participants,
                                        CanRecallToRoster(stagedActor)),
                                    cancellationToken)
                                .ConfigureAwait(false)
                                ?? throw new InvalidOperationException(
                                    "The battle lifecycle returned a null turn-start result.");
                            turnStartEvents = MapStatusEvents(stagedTurnStart.Events);
                            BattleEncounterEventOwnership.RequirePortOwned(
                                turnStartEvents,
                                "lifecycle-turn-start",
                                turnStartTransaction.Participants);
                            turnStartEconomyFault = runContext.CurrentEconomyAuthorityFault(
                                turnEconomy,
                                beforeEconomy,
                                actor.InstanceId);
                            if (turnStartEconomyFault is null)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                turnStartTransaction.Commit();
                            }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (BattleEncounterPortException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            turnStartFailure = exception;
                        }
                    }

                    if (turnStartFailure is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                EncounterRunContext.LifecycleFailureMessage("turn-start", turnStartFailure),
                                actor.InstanceId,
                                BattleEncounterFaultCode.LifecycleExecutionFailed)
                            .ConfigureAwait(false);
                    }

                    if (turnStartEconomyFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                turnStartEconomyFault,
                                actor.InstanceId,
                                BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    BattleTurnStartLifecycleResult turnStart = stagedTurnStart
                        ?? throw new InvalidOperationException(
                            "The committed turn-start lifecycle result is missing.");
                    await runContext.AddRangeAsync(turnStartEvents
                            ?? throw new InvalidOperationException(
                                "The committed turn-start lifecycle events are missing."))
                        .ConfigureAwait(false);

                    if (turnStart.Outcome != BattleTurnStartOutcome.CanAct)
                    {
                        await runContext.AddAsync(
                                BattleEncounterEventKind.TurnRestricted,
                                new BattleTurnRestrictedEventPayload(actor.InstanceId, turnStart.Restriction),
                                $"{actor.DisplayName} turn restriction: {turnStart.Outcome}.")
                            .ConfigureAwait(false);
                    }

                    BattleStatusDepartureReason? turnStartDepartureReason =
                        turnStart.Outcome switch
                        {
                            BattleTurnStartOutcome.FleeBattle =>
                                BattleStatusDepartureReason.Flee,
                            BattleTurnStartOutcome.RecallToRoster =>
                                BattleStatusDepartureReason.RosterRecall,
                            _ => null
                        };
                    BattleEncounterCompletion turnStartCompletion = await runContext.ReconcileAsync(
                            lastActor: null,
                            explicitDepartureActor:
                                turnStartDepartureReason.HasValue ? actor : null,
                            explicitDepartureReason: turnStartDepartureReason)
                        .ConfigureAwait(false);
                    if (!actor.State.IsDeployed || actor.State.IsDefeated)
                    {
                        await runContext.AddAsync(
                                BattleEncounterEventKind.TurnEnded,
                                new BattleTurnEndedEventPayload(
                                    actor.InstanceId,
                                    actor.TeamId,
                                    BattleEncounterTurnEndReason.ActorUnavailable,
                                    beforeEconomy),
                                $"{actor.DisplayName}'s turn ended before a command was committed.")
                            .ConfigureAwait(false);
                        if (turnStartCompletion.IsComplete)
                        {
                            return await runContext.FinishAsync(
                                    turnStartCompletion.Outcome,
                                    turnStartCompletion.WinningTeamId,
                                    turnStartCompletion.Message)
                                .ConfigureAwait(false);
                        }

                        schedule = runContext.AdvanceSchedule(
                            schedule,
                            BattleEncounterScheduleStepOutcome.ActorUnavailable(
                                actor.InstanceId),
                            actor.InstanceId);
                        continue;
                    }

                    if (turnStartCompletion.IsComplete)
                    {
                        await runContext.AddAsync(
                                BattleEncounterEventKind.TurnEnded,
                                new BattleTurnEndedEventPayload(
                                    actor.InstanceId,
                                    actor.TeamId,
                                    BattleEncounterTurnEndReason.EncounterTerminated,
                                    beforeEconomy),
                                $"{actor.DisplayName}'s turn ended with the encounter.")
                            .ConfigureAwait(false);
                        return await runContext.FinishAsync(
                                turnStartCompletion.Outcome,
                                turnStartCompletion.WinningTeamId,
                                turnStartCompletion.Message)
                            .ConfigureAwait(false);
                    }

                    IReadOnlyList<StatModifierLifecycleBoundary> activeStatModifierBoundaries =
                        services.Lifecycle is IBattleEncounterStatModifierBoundarySource boundarySource
                            ? portInvoker.Invoke(
                                BattleEncounterFaultCode.LifecycleExecutionFailed,
                                "stat-modifier-boundary-source",
                                () => BattleEncounterTurnRequest.SnapshotActiveStatModifierBoundaries(
                                    boundarySource.GetActiveStatModifierBoundaries(
                                        new BattleEncounterTurnLifecycleRequest(
                                            request,
                                            actor,
                                            request.Participants,
                                            CanRecallToRoster(actor)))
                                    ?? throw new InvalidOperationException(
                                        "The lifecycle boundary source returned null."),
                                    "activeStatModifierBoundaries"),
                                actor.InstanceId)
                            : Array.Empty<StatModifierLifecycleBoundary>();
                    string? preHandlerEconomyFault = runContext.CurrentEconomyAuthorityFault(
                        turnEconomy,
                        beforeEconomy,
                        actor.InstanceId);
                    if (preHandlerEconomyFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                preHandlerEconomyFault,
                                actor.InstanceId,
                                BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    BattleEncounterCommandResult command = await portInvoker.InvokeAsync(
                            BattleEncounterFaultCode.TurnHandlerExecutionFailed,
                            "turn-handler",
                            async () =>
                            {
                                BattleEncounterCommandResult returned =
                                    await services.TurnHandler.ExecuteTurnAsync(
                                            new BattleEncounterTurnRequest(
                                                request,
                                                actor,
                                                request.Participants,
                                                turnStart.Restriction,
                                                beforeEconomy,
                                                activeStatModifierBoundaries),
                                            cancellationToken)
                                        .ConfigureAwait(false)
                                    ?? throw new InvalidOperationException(
                                        "The battle turn handler returned null.");
                                BattleEncounterEventOwnership.RequirePortOwned(
                                    returned.Events,
                                    "turn-handler",
                                    request.Participants,
                                    actor.InstanceId);
                                return returned;
                            },
                            actor.InstanceId)
                        .ConfigureAwait(false);

                    string? postHandlerEconomyFault = runContext.CurrentEconomyAuthorityFault(
                        turnEconomy,
                        beforeEconomy,
                        actor.InstanceId);
                    if (postHandlerEconomyFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                postHandlerEconomyFault,
                                actor.InstanceId,
                                BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    await runContext.AddRangeAsync(command.Events).ConfigureAwait(false);
                    string? preApplyEconomyFault = runContext.CurrentEconomyAuthorityFault(
                        turnEconomy,
                        beforeEconomy,
                        actor.InstanceId);
                    if (preApplyEconomyFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                preApplyEconomyFault,
                                actor.InstanceId,
                                BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    if (command.Status is BattleEncounterCommandStatus.Cancelled)
                    {
                        await runContext.AddAsync(
                                BattleEncounterEventKind.TurnEnded,
                                new BattleTurnEndedEventPayload(
                                    actor.InstanceId,
                                    actor.TeamId,
                                    BattleEncounterTurnEndReason.EncounterTerminated,
                                    beforeEconomy),
                                $"{actor.DisplayName}'s turn ended with encounter cancellation.")
                            .ConfigureAwait(false);
                        return await runContext.FinishAsync(BattleEncounterOutcome.Cancelled, null, null).ConfigureAwait(false);
                    }

                    if (command.Status is BattleEncounterCommandStatus.Faulted)
                    {
                        await runContext.AddAsync(
                                BattleEncounterEventKind.TurnEnded,
                                new BattleTurnEndedEventPayload(
                                    actor.InstanceId,
                                    actor.TeamId,
                                    BattleEncounterTurnEndReason.EncounterTerminated,
                                    beforeEconomy),
                                $"{actor.DisplayName}'s turn ended with a command fault.")
                            .ConfigureAwait(false);
                        return await runContext.FaultDuringBattleAsync(
                                command.FaultMessage ?? "Battle command faulted.",
                                actor.InstanceId,
                                BattleEncounterFaultCode.CommandExecutionFaulted,
                                actor.TeamId,
                                "turn-handler")
                            .ConfigureAwait(false);
                    }

                    if (command.Status is BattleEncounterCommandStatus.Rejected)
                    {
                        string rejection = command.FaultMessage ?? "Battle command was rejected.";
                        await runContext.AddAsync(
                                BattleEncounterEventKind.ActionRejected,
                                new BattleActionRejectedEventPayload(
                                    actor.InstanceId,
                                    BattleEncounterCommandStatus.Rejected),
                                rejection)
                            .ConfigureAwait(false);
                        await runContext.AddAsync(
                                BattleEncounterEventKind.TurnEnded,
                                new BattleTurnEndedEventPayload(
                                    actor.InstanceId,
                                    actor.TeamId,
                                    BattleEncounterTurnEndReason.EncounterTerminated,
                                    beforeEconomy),
                                $"{actor.DisplayName}'s turn ended with a rejected command.")
                            .ConfigureAwait(false);
                        return await runContext.FaultDuringBattleAsync(
                                rejection,
                                actor.InstanceId,
                                BattleEncounterFaultCode.CommandRejected,
                                actor.TeamId,
                                "turn-handler")
                            .ConfigureAwait(false);
                    }

                    if (command.WinningTeamId is ContentId commandWinner &&
                        !runState.TeamOrder.Contains(commandWinner))
                    {
                        return await runContext.FaultDuringBattleAsync(
                                $"Battle command selected unknown winning team {commandWinner}.",
                                actor.InstanceId,
                                BattleEncounterFaultCode.CommandExecutionFaulted)
                            .ConfigureAwait(false);
                    }

                    portInvoker.InvokeAction(
                        BattleEncounterFaultCode.TurnEconomyExecutionFailed,
                        "turn-economy-apply",
                        () => turnEconomy.Apply(command.TurnConsumption),
                        actor.InstanceId);
                    BattleTurnEconomySnapshot afterEconomy = runContext.CaptureTurnEconomySnapshot(
                        turnEconomy,
                        actor.InstanceId);
                    hasTurnsRemaining = runContext.HasTurnsRemaining(turnEconomy, actor.InstanceId);
                    string? economyFault = ValidateEconomyTransition(
                        beforeEconomy,
                        afterEconomy,
                        hasTurnsRemaining,
                        command.TurnConsumption);
                    if (economyFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                economyFault,
                                actor.InstanceId,
                                BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    acceptedEconomyState = afterEconomy;
                    bool isContinuingFreeAction =
                        command.TurnConsumption.Kind == ActionTurnConsumptionKind.None &&
                        command.RequestedOutcome is null;
                    if (isContinuingFreeAction)
                    {
                        consecutiveFreeActions++;
                        if (consecutiveFreeActions > services.PhaseProgress.MaximumConsecutiveFreeActions)
                        {
                            return await runContext.FaultDuringBattleAsync(
                                    $"Team {teamId} exceeded the configured consecutive free-action limit " +
                                    $"of {services.PhaseProgress.MaximumConsecutiveFreeActions}.",
                                    actor.InstanceId,
                                    BattleEncounterFaultCode.ConsecutiveFreeActionLimitExceeded)
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        consecutiveFreeActions = 0;
                    }

                    if (command.TurnConsumption.Kind != ActionTurnConsumptionKind.None)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        IReadOnlyList<BattleEncounterEvent> turnEndEvents;
                        using var turnEndTransaction = new BattleEncounterLifecycleTransaction(
                            request.Participants,
                            services.Lifecycle);
                        try
                        {
                            BattleEncounterParticipant stagedActor = turnEndTransaction.GetStaged(actor);
                            IReadOnlyList<BattleEncounterEvent> returnedEvents =
                                await services.Lifecycle.ProcessTurnEndAsync(
                                    new BattleEncounterTurnLifecycleRequest(
                                        turnEndTransaction.CreateEncounter(request),
                                        stagedActor,
                                        turnEndTransaction.Participants,
                                        CanRecallToRoster(stagedActor)),
                                    cancellationToken)
                                .ConfigureAwait(false);
                            turnEndEvents = runContext.SnapshotLifecycleEvents(returnedEvents, "turn-end");
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            return await runContext.FaultDuringBattleAsync(
                                    EncounterRunContext.LifecycleFailureMessage("turn-end", exception),
                                    actor.InstanceId,
                                    BattleEncounterFaultCode.LifecycleExecutionFailed)
                                .ConfigureAwait(false);
                        }

                        string? turnEndEconomyFault = runContext.CurrentEconomyAuthorityFault(
                            turnEconomy,
                            acceptedEconomyState,
                            actor.InstanceId);
                        if (turnEndEconomyFault is not null)
                        {
                            return await runContext.FaultDuringBattleAsync(
                                    turnEndEconomyFault,
                                    actor.InstanceId,
                                    BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                                .ConfigureAwait(false);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        turnEndTransaction.Commit();

                        await runContext.AddRangeAsync(turnEndEvents).ConfigureAwait(false);
                    }

                    string? preEventEconomyFault = runContext.CurrentEconomyAuthorityFault(
                        turnEconomy,
                        acceptedEconomyState,
                        actor.InstanceId);
                    if (preEventEconomyFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                preEventEconomyFault,
                                actor.InstanceId,
                                BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    await runContext.AddTurnEconomyAsync(
                            actor.InstanceId,
                            beforeEconomy,
                            afterEconomy,
                            command.TurnConsumption)
                        .ConfigureAwait(false);

                    BattleEncounterCompletion completion = await runContext.ReconcileAsync(
                            actor,
                            turnStartDepartureReason.HasValue ? actor : null,
                            turnStartDepartureReason)
                        .ConfigureAwait(false);

                    string? postCommandEconomyFault = runContext.CurrentEconomyAuthorityFault(
                        turnEconomy,
                        acceptedEconomyState,
                        actor.InstanceId);
                    if (postCommandEconomyFault is not null)
                    {
                        return await runContext.FaultDuringBattleAsync(
                                postCommandEconomyFault,
                                actor.InstanceId,
                                BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                            .ConfigureAwait(false);
                    }

                    await runContext.AddAsync(
                            BattleEncounterEventKind.TurnEnded,
                            new BattleTurnEndedEventPayload(
                                actor.InstanceId,
                                actor.TeamId,
                                BattleEncounterTurnEndReason.CommandCommitted,
                                afterEconomy,
                                command.TurnConsumption),
                            $"{actor.DisplayName}'s turn ended.")
                        .ConfigureAwait(false);

                    if (command.RequestedOutcome is BattleEncounterOutcome requestedOutcome)
                    {
                        return await runContext.FinishAsync(requestedOutcome, command.WinningTeamId, command.FaultMessage)
                            .ConfigureAwait(false);
                    }

                    if (completion.IsComplete)
                    {
                        return await runContext.FinishAsync(completion.Outcome, completion.WinningTeamId, completion.Message)
                            .ConfigureAwait(false);
                    }

                    schedule = runContext.AdvanceSchedule(
                        schedule,
                        BattleEncounterScheduleStepOutcome.CommandCommitted(
                            actor.InstanceId,
                            command.TurnConsumption,
                            beforeEconomy,
                            afterEconomy,
                            hasTurnsRemaining),
                        actor.InstanceId);
                }

                if (schedule.IsComplete ||
                    schedule.Step is not BattleEncounterPhaseEndedScheduleStep phaseEnded ||
                    phaseEnded.TeamId != teamId)
                {
                    return await runContext.FaultDuringBattleAsync(
                            $"Encounter schedule did not close team {teamId}'s phase.",
                            faultCode: BattleEncounterFaultCode.ScheduleTransitionInvalid)
                        .ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                BattleTurnEconomySnapshot phaseEndState = runContext.CaptureTurnEconomySnapshot(turnEconomy);
                bool phaseEndHasTurnsRemaining = runContext.HasTurnsRemaining(turnEconomy);
                string? phaseEndFault = ValidateEconomyAuthority(
                    acceptedEconomyState,
                    phaseEndState,
                    phaseEndHasTurnsRemaining);
                if (phaseEndFault is not null)
                {
                    return await runContext.FaultDuringBattleAsync(
                            phaseEndFault,
                            faultCode: BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                        .ConfigureAwait(false);
                }

                IReadOnlyList<BattleEncounterEvent> phaseEndEvents;
                using var phaseEndTransaction = new BattleEncounterLifecycleTransaction(
                    request.Participants,
                    services.Lifecycle);
                try
                {
                    IReadOnlyList<BattleEncounterEvent> returnedEvents =
                        await services.Lifecycle.ProcessPhaseEndAsync(
                            new BattleEncounterLifecycleRequest(
                                phaseEndTransaction.CreateEncounter(request),
                                phaseEndTransaction.Participants,
                                runState.TeamOrder),
                            teamId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    phaseEndEvents = runContext.SnapshotLifecycleEvents(returnedEvents, "phase-end");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    return await runContext.FaultDuringBattleAsync(
                            EncounterRunContext.LifecycleFailureMessage("phase-end", exception),
                            faultCode: BattleEncounterFaultCode.LifecycleExecutionFailed)
                        .ConfigureAwait(false);
                }

                string? postPhaseLifecycleEconomyFault = runContext.CurrentEconomyAuthorityFault(
                    turnEconomy,
                    acceptedEconomyState);
                if (postPhaseLifecycleEconomyFault is not null)
                {
                    return await runContext.FaultDuringBattleAsync(
                            postPhaseLifecycleEconomyFault,
                            faultCode: BattleEncounterFaultCode.TurnEconomyTransitionInvalid)
                        .ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                phaseEndTransaction.Commit();

                await runContext.AddRangeAsync(phaseEndEvents).ConfigureAwait(false);
                await runContext.AddAsync(
                        BattleEncounterEventKind.PhaseEnded,
                        new BattlePhaseEndedEventPayload(teamId, phaseEndState),
                        $"Team {teamId} phase ended.")
                    .ConfigureAwait(false);

                BattleEncounterCompletion phaseCompletion =
                    await runContext.ReconcileAsync(null).ConfigureAwait(false);
                if (phaseCompletion.IsComplete)
                {
                    return await runContext.FinishAsync(
                            phaseCompletion.Outcome,
                            phaseCompletion.WinningTeamId,
                            phaseCompletion.Message)
                        .ConfigureAwait(false);
                }

                schedule = runContext.AdvanceSchedule(
                    schedule,
                    BattleEncounterScheduleStepOutcome.BoundaryCompleted());
            }

            if (schedule.IsComplete ||
                schedule.Step is not BattleEncounterRoundEndedScheduleStep roundEnded ||
                roundEnded.RoundNumber != round)
            {
                return await runContext.FaultDuringBattleAsync(
                        $"Encounter schedule did not close round {round}.",
                        faultCode: BattleEncounterFaultCode.ScheduleTransitionInvalid)
                    .ConfigureAwait(false);
            }

            IReadOnlyList<BattleEncounterEvent> roundEndEvents;
            using var roundEndTransaction = new BattleEncounterLifecycleTransaction(
                request.Participants,
                services.Lifecycle);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<BattleEncounterEvent> returnedEvents =
                    await services.Lifecycle.ProcessRoundEndAsync(
                            new BattleEncounterLifecycleRequest(
                                roundEndTransaction.CreateEncounter(request),
                                roundEndTransaction.Participants,
                                runState.TeamOrder),
                            round,
                            cancellationToken)
                        .ConfigureAwait(false);
                roundEndEvents = runContext.SnapshotLifecycleEvents(returnedEvents, "round-end");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return await runContext.FaultDuringBattleAsync(
                        EncounterRunContext.LifecycleFailureMessage("round-end", exception),
                        faultCode: BattleEncounterFaultCode.LifecycleExecutionFailed)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            roundEndTransaction.Commit();
            await runContext.AddRangeAsync(roundEndEvents).ConfigureAwait(false);
            BattleEncounterCompletion roundCompletion =
                await runContext.ReconcileAsync(null).ConfigureAwait(false);
            runState.CompletedRounds = round;
            await runContext.AddAsync(
                    BattleEncounterEventKind.RoundEnded,
                    new BattleRoundEndedEventPayload(round),
                    $"Round {round} ended.")
                .ConfigureAwait(false);
            if (roundCompletion.IsComplete)
            {
                return await runContext.FinishAsync(
                        roundCompletion.Outcome,
                        roundCompletion.WinningTeamId,
                        roundCompletion.Message)
                    .ConfigureAwait(false);
            }

            schedule = runContext.AdvanceSchedule(
                schedule,
                BattleEncounterScheduleStepOutcome.BoundaryCompleted());
        }

        return await runContext.FinishAsync(
                BattleEncounterOutcome.Draw,
                null,
                $"Battle ended in a draw after {request.RoundLimit} round(s).")
            .ConfigureAwait(false);

    }

    private sealed class EncounterRunContext
    {
        private readonly BattleEncounterRequest _request;
        private readonly BattleEncounterServices _services;
        private readonly CancellationToken _cancellationToken;
        private readonly EncounterRunState _runState;

        public EncounterRunContext(
            BattleEncounterRequest request,
            BattleEncounterServices services,
            CancellationToken cancellationToken,
            EncounterRunState runState)
        {
            _request = request;
            _services = services;
            _cancellationToken = cancellationToken;
            _runState = runState;
            PortInvoker = new EncounterPortInvoker(
                cancellationToken,
                services.Events,
                runState.Events,
                FinalizePortFailureAsync);
        }

        public EncounterPortInvoker PortInvoker { get; }

        public async ValueTask<BattleStartPhaseResult> RunBattleStartPhaseAsync()
        {
            RuntimeInstanceId[] duplicateParticipantIds = _request.Participants
                .GroupBy(participant => participant.InstanceId)
                .Where(group => group.Skip(1).Any())
                .Select(group => group.Key)
                .ToArray();
            if (duplicateParticipantIds.Length > 0)
            {
                string duplicates = string.Join(", ", duplicateParticipantIds.Select(id => id.ToString()));
                BattleEncounterResult terminalResult = await FailBeforeStartAsync(
                        $"Encounter participant runtime instance IDs must be unique. Duplicates: [{duplicates}].",
                        BattleEncounterFaultCode.DuplicateParticipantInstanceId)
                    .ConfigureAwait(false);
                return BattleStartPhaseResult.Finalized(terminalResult);
            }

            _cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ContentId>? proposedTeamOrder = PortInvoker.Invoke(
                BattleEncounterFaultCode.InitiativeExecutionFailed,
                "initiative",
                () =>
                {
                    IReadOnlyList<ContentId>? proposed = _services.Initiative.DetermineTeamOrder(
                        new BattleEncounterInitiativeRequest(
                            CaptureParticipantSnapshots(_request.Participants)));
                    return proposed is null
                        ? null
                        : Array.AsReadOnly(proposed.ToArray());
                });
            ContentId[] participatingTeams = _request.Participants
                .Select(participant => participant.TeamId)
                .Distinct()
                .ToArray();
            if (!IsExactTeamPermutation(proposedTeamOrder, participatingTeams))
            {
                string expected = string.Join(", ", participatingTeams.Select(team => team.ToString()));
                string received = proposedTeamOrder is null
                    ? "<null>"
                    : string.Join(", ", proposedTeamOrder.Select(team => team.ToString()));
                BattleEncounterResult terminalResult = await FailBeforeStartAsync(
                        $"Initiative must return every participating team exactly once. Expected [{expected}]; received [{received}].",
                        BattleEncounterFaultCode.InitiativeExecutionFailed)
                    .ConfigureAwait(false);
                return BattleStartPhaseResult.Finalized(terminalResult);
            }

            _runState.TeamOrder = Array.AsReadOnly(proposedTeamOrder!.ToArray());
            Synchronize();
            using var battleStartTransaction = new BattleEncounterLifecycleTransaction(
                _request.Participants,
                _services.Lifecycle);
            foreach (BattleEncounterParticipant participant in battleStartTransaction.Participants)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                participant.State.Passives.ResetBattleActivations();
                await AddAsync(
                        BattleEncounterEventKind.ActorCreated,
                        new BattleActorCreatedEventPayload(
                            participant.InstanceId,
                            participant.State.EntityId,
                            participant.TeamId),
                        $"Created {participant.DisplayName} as {participant.InstanceId} on {participant.TeamId}.")
                    .ConfigureAwait(false);
            }

            await AddAsync(
                    BattleEncounterEventKind.BattleStarted,
                    new BattleStartedEventPayload(
                        _request.ContextId,
                        _request.BattleKindId,
                        _request.MoonPhaseId,
                        _request.RoundLimit,
                        _request.Participants.Select(participant => participant.InstanceId),
                        participatingTeams),
                    "Battle started.")
                .ConfigureAwait(false);
            _runState.BattleStarted = true;
            await AddAsync(
                    BattleEncounterEventKind.InitiativeRolled,
                    new BattleInitiativeRolledEventPayload(_runState.TeamOrder),
                    "Initiative order: " + string.Join(", ", _runState.TeamOrder.Select(team => team.ToString())) + ".")
                .ConfigureAwait(false);
            _cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<BattleEncounterEvent> battleStartEvents;
            try
            {
                IReadOnlyList<BattleEncounterEvent> returnedEvents =
                    await _services.Lifecycle.ProcessBattleStartAsync(
                            new BattleEncounterLifecycleRequest(
                                battleStartTransaction.CreateEncounter(_request),
                                battleStartTransaction.Participants,
                                _runState.TeamOrder),
                            _cancellationToken)
                        .ConfigureAwait(false);
                battleStartEvents = SnapshotLifecycleEvents(returnedEvents, "battle-start");
                _cancellationToken.ThrowIfCancellationRequested();
                battleStartTransaction.Commit();
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                BattleEncounterResult terminalResult = await FaultDuringBattleAsync(
                        LifecycleFailureMessage("battle-start", exception),
                        faultCode: BattleEncounterFaultCode.LifecycleExecutionFailed)
                    .ConfigureAwait(false);
                return BattleStartPhaseResult.Finalized(terminalResult);
            }

            await AddRangeAsync(battleStartEvents).ConfigureAwait(false);
            BattleEncounterCompletion initial = await ReconcileAsync(null).ConfigureAwait(false);
            if (initial.IsComplete)
            {
                BattleEncounterResult terminalResult = await FinishAsync(
                        initial.Outcome,
                        initial.WinningTeamId,
                        initial.Message)
                    .ConfigureAwait(false);
                return BattleStartPhaseResult.Finalized(terminalResult);
            }

            return BattleStartPhaseResult.ScheduleReady(StartSchedule());
        }

        public async ValueTask AddAsync(
            BattleEncounterEventKind kind,
            BattleEncounterEventPayload payload,
            string? debugText = null)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var battleEvent = new BattleEncounterEvent(++_runState.Sequence, kind, payload, debugText);
            await PortInvoker.PublishAndRecordAsync(battleEvent).ConfigureAwait(false);
        }

        public async ValueTask AddTurnEconomyAsync(
            RuntimeInstanceId actor,
            BattleTurnEconomySnapshot before,
            BattleTurnEconomySnapshot after,
            ActionTurnConsumption consumption)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var battleEvent = new BattleEncounterEvent(
                ++_runState.Sequence,
                BattleEncounterEventKind.TurnEconomyChanged,
                new BattleTurnEconomyChangedEventPayload(actor, before, after, consumption),
                $"Turn economy {after.EconomyId}: {after.RemainingActions} action(s) remaining.");
            await PortInvoker.PublishAndRecordAsync(battleEvent).ConfigureAwait(false);
        }

        public async ValueTask AddRangeAsync(IEnumerable<BattleEncounterEvent> unsequenced)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            foreach (BattleEncounterEvent battleEvent in unsequenced)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                BattleEncounterEvent sequenced = battleEvent.WithSequence(++_runState.Sequence);
                await PortInvoker.PublishAndRecordAsync(sequenced).ConfigureAwait(false);
            }
        }

        private async ValueTask<(bool HadDepartures, bool StateMutated)> ProcessPendingDeparturesAsync(
            BattleEncounterParticipant? explicitActor = null,
            BattleStatusDepartureReason? explicitReason = null)
        {
            if ((explicitActor is null) != (explicitReason is null))
            {
                throw new InvalidOperationException(
                    "An explicit encounter departure requires both an actor and a reason.");
            }

            var reasonsByActor = new Dictionary<RuntimeInstanceId, BattleStatusDepartureReason>();
            if (explicitActor is not null &&
                explicitReason is BattleStatusDepartureReason explicitDepartureReason)
            {
                reasonsByActor.Add(explicitActor.InstanceId, explicitDepartureReason);
            }

            foreach (BattleEncounterParticipant participant in _request.Participants)
            {
                if (participant.State.IsDefeated &&
                    !_runState.ProcessedDefeatDepartures.Contains(participant.InstanceId))
                {
                    reasonsByActor.TryAdd(
                        participant.InstanceId,
                        BattleStatusDepartureReason.Defeat);
                }
            }

            if (reasonsByActor.Count == 0)
            {
                return (false, false);
            }

            bool stateMutated = false;
            if (_services.Lifecycle is IBattleEncounterDepartureLifecyclePort departureLifecycle)
            {
                using var transaction = new BattleEncounterLifecycleTransaction(
                    _request.Participants,
                    _services.Lifecycle);
                var departureEvents = new List<BattleEncounterEvent>();
                foreach (BattleEncounterParticipant participant in transaction.Participants)
                {
                    if (!reasonsByActor.TryGetValue(
                            participant.InstanceId,
                            out BattleStatusDepartureReason participantDepartureReason))
                    {
                        continue;
                    }

                    IReadOnlyList<BattleEncounterEvent> returnedEvents =
                        await PortInvoker.InvokeAsync(
                                BattleEncounterFaultCode.LifecycleExecutionFailed,
                                "actor-departure-lifecycle",
                                async () =>
                                {
                                    IReadOnlyList<BattleEncounterEvent> result =
                                        await departureLifecycle.ProcessActorDepartureAsync(
                                                new BattleEncounterDepartureLifecycleRequest(
                                                    transaction.CreateEncounter(_request),
                                                    participant,
                                                    transaction.Participants,
                                                    participantDepartureReason),
                                                _cancellationToken)
                                            .ConfigureAwait(false);
                                    return SnapshotLifecycleEvents(result, "actor-departure");
                                },
                                participant.InstanceId)
                            .ConfigureAwait(false);
                    departureEvents.AddRange(returnedEvents);
                }

                _cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                stateMutated = true;
                await AddRangeAsync(departureEvents).ConfigureAwait(false);
            }

            foreach (BattleEncounterParticipant participant in _request.Participants)
            {
                if (participant.State.IsDefeated && reasonsByActor.ContainsKey(participant.InstanceId))
                {
                    _runState.ProcessedDefeatDepartures.Add(participant.InstanceId);
                }
            }

            return (true, stateMutated);
        }

        public BattleEncounterScheduleCursor StartSchedule()
        {
            ConsumeScheduleTransitionBudget();
            var scheduleRequest = new BattleEncounterScheduleStartRequest(
                CaptureScheduleParticipants(_request.Participants),
                _runState.TeamOrder,
                _request.RoundLimit);
            BattleEncounterScheduleTransitionResult transition = PortInvoker.Invoke(
                BattleEncounterFaultCode.ScheduleExecutionFailed,
                "schedule-start",
                () => _services.Schedule.Start(scheduleRequest)
                      ?? throw new InvalidOperationException(
                          "The encounter scheduling policy returned null while starting."));
            return PortInvoker.Invoke(
                BattleEncounterFaultCode.ScheduleTransitionInvalid,
                "schedule-transition-validation",
                () => BattleEncounterScheduleCursor.Start(
                    _services.Schedule,
                    scheduleRequest,
                    transition));
        }

        public BattleEncounterScheduleCursor AdvanceSchedule(
            BattleEncounterScheduleCursor cursor,
            BattleEncounterScheduleStepOutcome outcome,
            RuntimeInstanceId? actorId = null)
        {
            ArgumentNullException.ThrowIfNull(cursor);
            ArgumentNullException.ThrowIfNull(outcome);
            ConsumeScheduleTransitionBudget(actorId);
            BattleEncounterScheduleStep completedStep = cursor.Step
                ?? throw new InvalidOperationException(
                    "A completed encounter schedule cannot be advanced.");
            var advanceRequest = new BattleEncounterScheduleAdvanceRequest(
                cursor.State,
                completedStep,
                outcome,
                CaptureScheduleParticipants(_request.Participants));
            BattleEncounterScheduleTransitionResult transition = PortInvoker.Invoke(
                BattleEncounterFaultCode.ScheduleExecutionFailed,
                "schedule-advance",
                () => _services.Schedule.Advance(advanceRequest)
                      ?? throw new InvalidOperationException(
                          "The encounter scheduling policy returned null while advancing."),
                actorId);
            return PortInvoker.Invoke(
                BattleEncounterFaultCode.ScheduleTransitionInvalid,
                "schedule-transition-validation",
                () => cursor.Advance(_services.Schedule, outcome, transition),
                actorId);
        }

        private void ConsumeScheduleTransitionBudget(RuntimeInstanceId? actorId = null)
        {
            PortInvoker.InvokeAction(
                BattleEncounterFaultCode.ScheduleTransitionLimitExceeded,
                "schedule-progress",
                () =>
                {
                    if (_runState.ScheduleTransitionCount >=
                        _services.EncounterProgress.MaximumScheduleTransitions)
                    {
                        throw new InvalidOperationException(
                            "The encounter schedule exceeded the configured structural " +
                            $"transition limit of " +
                            $"{_services.EncounterProgress.MaximumScheduleTransitions}.");
                    }

                    _runState.ScheduleTransitionCount = checked(_runState.ScheduleTransitionCount + 1);
                },
                actorId);
        }

        public async ValueTask<BattleEncounterCompletion> ReconcileAsync(
            BattleEncounterParticipant? lastActor,
            BattleEncounterParticipant? explicitDepartureActor = null,
            BattleStatusDepartureReason? explicitDepartureReason = null)
        {
            Synchronize();
            ReleaseRecoveredDefeatPeriods();
            BattleEncounterParticipant? pendingExplicitActor = explicitDepartureActor;
            BattleStatusDepartureReason? pendingExplicitReason = explicitDepartureReason;
            if (pendingExplicitActor?.State.IsDeployed == true)
            {
                pendingExplicitActor = null;
                pendingExplicitReason = null;
            }

            for (int pass = 0; pass <= _request.Participants.Count; pass++)
            {
                (bool hadDepartures, bool stateMutated) =
                    await ProcessPendingDeparturesAsync(
                            pendingExplicitActor,
                            pendingExplicitReason)
                        .ConfigureAwait(false);
                pendingExplicitActor = null;
                pendingExplicitReason = null;

                if (stateMutated)
                {
                    Synchronize();
                    ReleaseRecoveredDefeatPeriods();
                }

                if (!hadDepartures)
                {
                    break;
                }

                if (pass == _request.Participants.Count)
                {
                    PortInvoker.InvokeAction(
                        BattleEncounterFaultCode.LifecycleExecutionFailed,
                        "departure-reconciliation",
                        () => throw new InvalidOperationException(
                            "Encounter departure reconciliation did not reach a stable state."),
                        lastActor?.InstanceId);
                }
            }

            await AnnounceNewDefeatsAsync(
                    _request.Participants,
                    _runState.DefeatedAnnouncements,
                    AddAsync)
                .ConfigureAwait(false);

            return PortInvoker.Invoke(
                BattleEncounterFaultCode.CompletionEvaluationFailed,
                "completion-evaluation",
                () =>
                {
                    BattleEncounterCompletion completion =
                        _services.Completion.Evaluate(
                            CreateCompletionRequest(_request.Participants, lastActor))
                        ?? throw new InvalidOperationException(
                            "The battle completion policy returned null.");
                    ValidateCompletion(completion, _runState.TeamOrder);
                    return completion;
                },
                lastActor?.InstanceId);
        }

        private void ReleaseRecoveredDefeatPeriods()
        {
            foreach (BattleEncounterParticipant participant in _request.Participants)
            {
                if (participant.State.IsDefeated)
                {
                    continue;
                }

                _runState.ProcessedDefeatDepartures.Remove(participant.InstanceId);
                _runState.DefeatedAnnouncements.Remove(participant.InstanceId);
            }
        }

        public void Synchronize()
        {
            PortInvoker.InvokeAction(
                BattleEncounterFaultCode.StateSynchronizationFailed,
                "state-synchronization",
                () => _services.Synchronizer.Synchronize(_request.Participants));
        }

        private static BattleEncounterCompletionRequest CreateCompletionRequest(
            IEnumerable<BattleEncounterParticipant> participants,
            BattleEncounterParticipant? lastActor)
        {
            IReadOnlyList<BattleEncounterParticipantSnapshot> snapshots =
                CaptureParticipantSnapshots(participants);
            BattleEncounterParticipantSnapshot? lastActorSnapshot =
                lastActor is null
                    ? null
                    : snapshots.Single(participant =>
                        participant.InstanceId == lastActor.InstanceId);
            return new BattleEncounterCompletionRequest(
                snapshots,
                lastActorSnapshot);
        }

        public BattleTurnEconomySnapshot CaptureTurnEconomySnapshot(
            IBattleTurnEconomy turnEconomy,
            RuntimeInstanceId? actorId = null) =>
            PortInvoker.Invoke(
                BattleEncounterFaultCode.TurnEconomyExecutionFailed,
                "turn-economy-snapshot",
                () => turnEconomy.CaptureSnapshot()
                      ?? throw new InvalidOperationException("The turn economy returned a null snapshot."),
                actorId);

        public bool HasTurnsRemaining(
            IBattleTurnEconomy turnEconomy,
            RuntimeInstanceId? actorId = null) =>
            PortInvoker.Invoke(
                BattleEncounterFaultCode.TurnEconomyExecutionFailed,
                "turn-economy-state",
                turnEconomy.HasTurnsRemaining,
                actorId);

        public string? CurrentEconomyAuthorityFault(
            IBattleTurnEconomy turnEconomy,
            BattleTurnEconomySnapshot expected,
            RuntimeInstanceId? actorId = null)
        {
            BattleTurnEconomySnapshot actual = CaptureTurnEconomySnapshot(turnEconomy, actorId);
            bool actualHasTurnsRemaining = HasTurnsRemaining(turnEconomy, actorId);
            return ValidateEconomyAuthority(expected, actual, actualHasTurnsRemaining);
        }

        private ValueTask<BattleEncounterResult> FinalizePortFailureAsync(
            BattleEncounterPortException failure)
        {
            string primaryMessage =
                $"Battle encounter port '{failure.PortName}' failed: {failure.InnerException?.Message ?? failure.Message}";
            return FinalizeFailureAsync(
                primaryMessage,
                failure.FaultCode,
                failure.ActorId,
                failure.FaultCode != BattleEncounterFaultCode.EventPublicationFailed);
        }

        private async ValueTask<BattleEncounterResult> FinalizeFailureAsync(
            string primaryMessage,
            BattleEncounterFaultCode? faultCode,
            RuntimeInstanceId? actorId,
            bool publishEvents,
            ContentId? teamId = null,
            string? portName = null)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            bool publishDuringFinalization = publishEvents;
            BattleEncounterFaultCode resolvedFaultCode =
                faultCode ?? BattleEncounterFaultCode.CommandExecutionFaulted;

            await AppendFinalEventAsync(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.BattleFaulted,
                    new BattleFaultedEventPayload(
                        resolvedFaultCode,
                        actorId,
                        teamId,
                        portName),
                    primaryMessage))
                .ConfigureAwait(false);

            IReadOnlyList<BattleEncounterEvent> battleEndEvents = [];
            string? cleanupFailure = null;
            if (_runState.BattleStarted && !_runState.BattleEndLifecycleAttempted)
            {
                _runState.BattleEndLifecycleAttempted = true;
                try
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    using var lifecycleTransaction = new BattleEncounterLifecycleTransaction(
                        _request.Participants,
                        _services.Lifecycle);
                    IReadOnlyList<BattleEncounterEvent> returnedEvents =
                        await _services.Lifecycle.ProcessBattleEndAsync(
                                new BattleEncounterLifecycleRequest(
                                    lifecycleTransaction.CreateEncounter(_request),
                                    lifecycleTransaction.Participants,
                                    _runState.TeamOrder),
                                BattleEncounterOutcome.Faulted,
                                _cancellationToken)
                            .ConfigureAwait(false);
                    _cancellationToken.ThrowIfCancellationRequested();
                    battleEndEvents = SnapshotLifecycleEvents(returnedEvents, "battle-end");
                    _cancellationToken.ThrowIfCancellationRequested();
                    lifecycleTransaction.Commit();
                }
                catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    cleanupFailure = LifecycleFailureMessage("battle-end", exception);
                    await AppendFinalEventAsync(new BattleEncounterEvent(
                            0,
                            BattleEncounterEventKind.BattleFaulted,
                            new BattleFaultedEventPayload(
                                BattleEncounterFaultCode.LifecycleExecutionFailed,
                                PortName: "battle-end-lifecycle"),
                            cleanupFailure))
                        .ConfigureAwait(false);
                }
            }

            string finalMessage = cleanupFailure is null
                ? primaryMessage
                : $"{primaryMessage} {cleanupFailure}";
            foreach (BattleEncounterEvent battleEndEvent in battleEndEvents)
            {
                await AppendFinalEventAsync(battleEndEvent).ConfigureAwait(false);
            }

            await AppendFinalEventAsync(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.BattleEnded,
                    new BattleEndedEventPayload(
                        BattleEncounterOutcome.Faulted,
                        null,
                        _runState.FinalRoundNumber,
                        _runState.CompletedRounds,
                        resolvedFaultCode),
                    finalMessage))
                .ConfigureAwait(false);

            return new BattleEncounterResult(
                BattleEncounterOutcome.Faulted,
                null,
                _request.Participants,
                _runState.Events,
                finalMessage,
                resolvedFaultCode);

            async ValueTask AppendFinalEventAsync(BattleEncounterEvent unsequenced)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                BattleEncounterEvent sequenced = unsequenced.WithSequence(++_runState.Sequence);
                _runState.Events.Add(sequenced);
                if (!publishDuringFinalization)
                {
                    return;
                }

                try
                {
                    await _services.Events.PublishAsync(sequenced, _cancellationToken).ConfigureAwait(false);
                    _cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    publishDuringFinalization = false;
                }
            }
        }

        public async ValueTask<BattleEncounterResult> FailBeforeStartAsync(
            string message,
            BattleEncounterFaultCode? faultCode = null) =>
            await FinalizeFailureAsync(message, faultCode, null, publishEvents: true).ConfigureAwait(false);

        public async ValueTask<BattleEncounterResult> FaultDuringBattleAsync(
            string message,
            RuntimeInstanceId? actorId = null,
            BattleEncounterFaultCode? faultCode = null,
            ContentId? teamId = null,
            string? portName = null) =>
            await FinalizeFailureAsync(
                    message,
                    faultCode,
                    actorId,
                    publishEvents: true,
                    teamId: teamId,
                    portName: portName)
                .ConfigureAwait(false);

        public async ValueTask<BattleEncounterResult> FinishAsync(
            BattleEncounterOutcome outcome,
            ContentId? winningTeamId,
            string? message,
            BattleEncounterFaultCode? faultCode = null)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            _runState.BattleEndLifecycleAttempted = true;
            IReadOnlyList<BattleEncounterEvent> battleEndEvents;
            try
            {
                using var lifecycleTransaction = new BattleEncounterLifecycleTransaction(
                    _request.Participants,
                    _services.Lifecycle);
                IReadOnlyList<BattleEncounterEvent> returnedEvents =
                    await _services.Lifecycle.ProcessBattleEndAsync(
                        new BattleEncounterLifecycleRequest(
                            lifecycleTransaction.CreateEncounter(_request),
                            lifecycleTransaction.Participants,
                            _runState.TeamOrder),
                        outcome,
                        _cancellationToken)
                    .ConfigureAwait(false);
                battleEndEvents = SnapshotLifecycleEvents(returnedEvents, "battle-end");
                _cancellationToken.ThrowIfCancellationRequested();
                lifecycleTransaction.Commit();
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return await FinalizeFailureAsync(
                        LifecycleFailureMessage("battle-end", exception),
                        BattleEncounterFaultCode.LifecycleExecutionFailed,
                        null,
                        publishEvents: true)
                    .ConfigureAwait(false);
            }

            string endMessage = message ?? (outcome == BattleEncounterOutcome.Victory && winningTeamId is ContentId team
                ? $"Team {team} won."
                : outcome == BattleEncounterOutcome.Escape
                    ? "Battle escaped."
                    : outcome == BattleEncounterOutcome.Cancelled
                        ? "Battle cancelled."
                        : outcome == BattleEncounterOutcome.Faulted
                            ? "Battle faulted."
                            : "Battle ended.");
            await AddRangeAsync(battleEndEvents).ConfigureAwait(false);
            await AddAsync(
                    BattleEncounterEventKind.BattleEnded,
                    new BattleEndedEventPayload(
                        outcome,
                        winningTeamId,
                        _runState.FinalRoundNumber,
                        _runState.CompletedRounds,
                        faultCode),
                    endMessage)
                .ConfigureAwait(false);
            return new BattleEncounterResult(
                outcome,
                winningTeamId,
                _request.Participants,
                _runState.Events,
                outcome == BattleEncounterOutcome.Faulted ? message : null,
                outcome == BattleEncounterOutcome.Faulted ? faultCode : null);
        }

        public static string LifecycleFailureMessage(string stage, Exception exception) =>
            $"Battle lifecycle step '{stage}' failed: {exception.Message}";

        private static void ValidateCompletion(
            BattleEncounterCompletion completion,
            IReadOnlyCollection<ContentId> participatingTeamIds)
        {
            if (!Enum.IsDefined(completion.Outcome))
            {
                throw new InvalidOperationException(
                    $"The battle completion policy returned undefined outcome " +
                    $"'{(int)completion.Outcome}'.");
            }

            if (completion.WinningTeamId is ContentId suppliedWinner &&
                !suppliedWinner.IsValid)
            {
                throw new InvalidOperationException(
                    "The battle completion policy returned an invalid winning team ID.");
            }

            if (!completion.IsComplete)
            {
                if (completion.Outcome != BattleEncounterOutcome.Draw ||
                    completion.WinningTeamId is not null ||
                    completion.Message is not null)
                {
                    throw new InvalidOperationException(
                        "An incomplete battle completion result cannot carry terminal outcome metadata.");
                }

                return;
            }

            bool requiresWinner =
                completion.Outcome is BattleEncounterOutcome.Victory or BattleEncounterOutcome.Defeat;
            if (requiresWinner != (completion.WinningTeamId is not null))
            {
                throw new InvalidOperationException(
                    requiresWinner
                        ? $"A complete {completion.Outcome} result requires a winning team."
                        : $"A complete {completion.Outcome} result cannot carry a winning team.");
            }

            if (completion.Outcome == BattleEncounterOutcome.Faulted)
            {
                throw new InvalidOperationException(
                    "A completion policy cannot report a fault without a typed fault code.");
            }

            if (completion.WinningTeamId is ContentId winner &&
                !participatingTeamIds.Contains(winner))
            {
                throw new InvalidOperationException(
                    $"The battle completion policy selected unknown winning team {winner}.");
            }
        }

        public IReadOnlyList<BattleEncounterEvent> SnapshotLifecycleEvents(
            IReadOnlyList<BattleEncounterEvent>? lifecycleEvents,
            string stage)
        {
            BattleEncounterEvent[] snapshot = (lifecycleEvents ?? throw new InvalidOperationException(
                    $"The battle lifecycle returned a null {stage} event collection."))
                .ToArray();
            BattleEncounterEventOwnership.RequirePortOwned(
                snapshot,
                $"lifecycle-{stage}",
                _request.Participants);
            return Array.AsReadOnly(snapshot);
        }
    }

    private sealed class BattleStartPhaseResult
    {
        private BattleStartPhaseResult(
            BattleEncounterScheduleCursor? schedule,
            BattleEncounterResult? terminalResult)
        {
            Schedule = schedule;
            TerminalResult = terminalResult;
        }

        public BattleEncounterScheduleCursor? Schedule { get; }
        public BattleEncounterResult? TerminalResult { get; }

        public static BattleStartPhaseResult ScheduleReady(BattleEncounterScheduleCursor schedule) =>
            new(schedule, null);

        public static BattleStartPhaseResult Finalized(BattleEncounterResult terminalResult) =>
            new(null, terminalResult);
    }

    private sealed class EncounterPortInvoker
    {
        private readonly CancellationToken _cancellationToken;
        private readonly IBattleEncounterEventSink _eventSink;
        private readonly List<BattleEncounterEvent> _events;
        private readonly Func<BattleEncounterPortException, ValueTask<BattleEncounterResult>> _finalizePortFailureAsync;

        public EncounterPortInvoker(
            CancellationToken cancellationToken,
            IBattleEncounterEventSink eventSink,
            List<BattleEncounterEvent> events,
            Func<BattleEncounterPortException, ValueTask<BattleEncounterResult>> finalizePortFailureAsync)
        {
            _cancellationToken = cancellationToken;
            _eventSink = eventSink;
            _events = events;
            _finalizePortFailureAsync = finalizePortFailureAsync;
        }

        public T Invoke<T>(
            BattleEncounterFaultCode faultCode,
            string portName,
            Func<T> operation,
            RuntimeInstanceId? actorId = null)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            try
            {
                T result = operation();
                _cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                throw new BattleEncounterPortException(
                    faultCode,
                    portName,
                    actorId,
                    exception,
                    _finalizePortFailureAsync);
            }
        }

        public void InvokeAction(
            BattleEncounterFaultCode faultCode,
            string portName,
            Action operation,
            RuntimeInstanceId? actorId = null) =>
            Invoke(
                faultCode,
                portName,
                () =>
                {
                    operation();
                    return true;
                },
                actorId);

        public async ValueTask<T> InvokeAsync<T>(
            BattleEncounterFaultCode faultCode,
            string portName,
            Func<ValueTask<T>> operation,
            RuntimeInstanceId? actorId = null)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            try
            {
                T result = await operation().ConfigureAwait(false);
                _cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                throw new BattleEncounterPortException(
                    faultCode,
                    portName,
                    actorId,
                    exception,
                    _finalizePortFailureAsync);
            }
        }

        public async ValueTask InvokeTaskAsync(
            BattleEncounterFaultCode faultCode,
            string portName,
            Func<ValueTask> operation,
            RuntimeInstanceId? actorId = null)
        {
            await InvokeAsync(
                    faultCode,
                    portName,
                    async () =>
                    {
                        await operation().ConfigureAwait(false);
                        return true;
                    },
                    actorId)
                .ConfigureAwait(false);
        }

        public async ValueTask PublishAndRecordAsync(BattleEncounterEvent battleEvent)
        {
            _events.Add(battleEvent);
            await InvokeTaskAsync(
                    BattleEncounterFaultCode.EventPublicationFailed,
                    "event-publication",
                    () => _eventSink.PublishAsync(battleEvent, _cancellationToken),
                    battleEvent.ActorId)
                .ConfigureAwait(false);
        }
    }

    private sealed class EncounterRunState
    {
        public EncounterRunState(IReadOnlyList<BattleEncounterParticipant> participants)
        {
            Events = new List<BattleEncounterEvent>();
            DefeatedAnnouncements = new HashSet<RuntimeInstanceId>();
            ProcessedDefeatDepartures = new HashSet<RuntimeInstanceId>(
                participants
                    .Where(participant => participant.State.IsDefeated)
                    .Select(participant => participant.InstanceId));
            Sequence = 0;
            FinalRoundNumber = null;
            CompletedRounds = 0;
            BattleStarted = false;
            BattleEndLifecycleAttempted = false;
            ScheduleTransitionCount = 0;
            TeamOrder = Array.Empty<ContentId>();
        }

        public List<BattleEncounterEvent> Events { get; }
        public HashSet<RuntimeInstanceId> DefeatedAnnouncements { get; }
        public HashSet<RuntimeInstanceId> ProcessedDefeatDepartures { get; }
        public int Sequence { get; set; }
        public int? FinalRoundNumber { get; set; }
        public int CompletedRounds { get; set; }
        public bool BattleStarted { get; set; }
        public bool BattleEndLifecycleAttempted { get; set; }
        public int ScheduleTransitionCount { get; set; }
        public IReadOnlyList<ContentId> TeamOrder { get; set; }
    }

    private sealed class BattleEncounterPortException : Exception
    {
        private readonly Func<BattleEncounterPortException, ValueTask<BattleEncounterResult>> _finalize;

        public BattleEncounterPortException(
            BattleEncounterFaultCode faultCode,
            string portName,
            RuntimeInstanceId? actorId,
            Exception innerException,
            Func<BattleEncounterPortException, ValueTask<BattleEncounterResult>> finalize)
            : base($"Battle encounter port '{portName}' failed.", innerException)
        {
            FaultCode = faultCode;
            PortName = string.IsNullOrWhiteSpace(portName)
                ? throw new ArgumentException("A port name is required.", nameof(portName))
                : portName;
            ActorId = actorId;
            _finalize = finalize ?? throw new ArgumentNullException(nameof(finalize));
        }

        public BattleEncounterFaultCode FaultCode { get; }
        public string PortName { get; }
        public RuntimeInstanceId? ActorId { get; }

        public ValueTask<BattleEncounterResult> FinalizeAsync() => _finalize(this);
    }

    private sealed class BattleEncounterScheduleCursor
    {
        private BattleEncounterScheduleCursor(
            BattleEncounterScheduleStateSnapshot state,
            BattleEncounterScheduleStep? step,
            bool isComplete,
            IReadOnlyDictionary<RuntimeInstanceId, ContentId> participantTeams)
        {
            State = state;
            Step = step;
            IsComplete = isComplete;
            ParticipantTeams = participantTeams;
        }

        public BattleEncounterScheduleStateSnapshot State { get; }
        public BattleEncounterScheduleStep? Step { get; }
        public bool IsComplete { get; }
        private IReadOnlyDictionary<RuntimeInstanceId, ContentId> ParticipantTeams { get; }

        public static BattleEncounterScheduleCursor Start(
            IBattleEncounterSchedulePolicy policy,
            BattleEncounterScheduleStartRequest request,
            BattleEncounterScheduleTransitionResult transition)
        {
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(transition);
            if (!policy.PolicyId.IsValid)
            {
                throw new InvalidOperationException("The encounter scheduling policy returned an invalid policy ID.");
            }

            if (transition.Status != BattleEncounterScheduleTransitionStatus.Started ||
                transition.After is not { } state ||
                transition.NextStep is not { } step)
            {
                throw new InvalidOperationException(
                    "The encounter scheduling policy did not return a valid started transition.");
            }

            RequireScheduleIdentity(policy, request, state);
            if (step.PolicyId != policy.PolicyId)
            {
                throw new InvalidOperationException(
                    "The initial schedule step does not belong to the injected scheduling policy.");
            }

            IReadOnlyDictionary<RuntimeInstanceId, ContentId> participantTeams =
                new System.Collections.ObjectModel.ReadOnlyDictionary<RuntimeInstanceId, ContentId>(
                    request.Participants.ToDictionary(
                        participant => participant.InstanceId,
                        participant => participant.TeamId));
            RequireStepIdentity(state, step, participantTeams);
            return new BattleEncounterScheduleCursor(
                state,
                step,
                isComplete: false,
                participantTeams);
        }

        public BattleEncounterScheduleCursor Advance(
            IBattleEncounterSchedulePolicy policy,
            BattleEncounterScheduleStepOutcome completedOutcome,
            BattleEncounterScheduleTransitionResult transition)
        {
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(completedOutcome);
            ArgumentNullException.ThrowIfNull(transition);
            if (transition.Status == BattleEncounterScheduleTransitionStatus.Rejected)
            {
                string diagnostics = string.Join(
                    "; ",
                    transition.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"));
                throw new InvalidOperationException(
                    "The encounter scheduling policy rejected its transition: " + diagnostics);
            }

            if (!ReferenceEquals(transition.Before, State) ||
                transition.After is not { } after)
            {
                throw new InvalidOperationException(
                    "The encounter scheduling policy did not advance the current schedule state.");
            }

            if (after.PolicyId != policy.PolicyId ||
                !State.ParticipantIds.SequenceEqual(after.ParticipantIds) ||
                !State.TeamOrder.SequenceEqual(after.TeamOrder) ||
                State.RoundLimit != after.RoundLimit)
            {
                throw new InvalidOperationException(
                    "The encounter scheduling policy changed encounter identity while advancing.");
            }

            BattleEncounterScheduleStructuralValidator.ValidateAdvance(
                State,
                Step ?? throw new InvalidOperationException(
                    "A completed encounter schedule cannot accept another transition."),
                completedOutcome,
                transition);

            return transition.Status switch
            {
                BattleEncounterScheduleTransitionStatus.Advanced
                    when transition.NextStep is { } step &&
                         step.PolicyId == policy.PolicyId =>
                    CreateAdvanced(after, step),
                BattleEncounterScheduleTransitionStatus.Completed
                    when transition.NextStep is null =>
                    new BattleEncounterScheduleCursor(
                        after,
                        null,
                        isComplete: true,
                        ParticipantTeams),
                _ => throw new InvalidOperationException(
                    "The encounter scheduling policy returned an invalid advance transition.")
            };
        }

        private BattleEncounterScheduleCursor CreateAdvanced(
            BattleEncounterScheduleStateSnapshot state,
            BattleEncounterScheduleStep step)
        {
            RequireStepIdentity(state, step, ParticipantTeams);
            return new BattleEncounterScheduleCursor(
                state,
                step,
                isComplete: false,
                ParticipantTeams);
        }

        private static void RequireScheduleIdentity(
            IBattleEncounterSchedulePolicy policy,
            BattleEncounterScheduleStartRequest request,
            BattleEncounterScheduleStateSnapshot state)
        {
            if (state.PolicyId != policy.PolicyId ||
                !state.ParticipantIds.SequenceEqual(
                    request.Participants.Select(participant => participant.InstanceId)) ||
                !state.TeamOrder.SequenceEqual(request.TeamOrder) ||
                state.RoundLimit != request.RoundLimit)
            {
                throw new InvalidOperationException(
                    "The encounter scheduling policy changed encounter identity while starting.");
            }
        }

        private static void RequireStepIdentity(
            BattleEncounterScheduleStateSnapshot state,
            BattleEncounterScheduleStep step,
            IReadOnlyDictionary<RuntimeInstanceId, ContentId> participantTeams)
        {
            ContentId? stepTeamId = step switch
            {
                BattleEncounterPhaseStartedScheduleStep phaseStarted =>
                    phaseStarted.TeamId,
                BattleEncounterCommandWindowScheduleStep commandWindow =>
                    commandWindow.TeamId,
                BattleEncounterPhaseEndedScheduleStep phaseEnded =>
                    phaseEnded.TeamId,
                _ => null
            };
            if (stepTeamId is ContentId teamId &&
                !state.TeamOrder.Contains(teamId))
            {
                throw new InvalidOperationException(
                    $"Schedule step team {teamId} is outside the frozen encounter graph.");
            }

            if (step is BattleEncounterCommandWindowScheduleStep command &&
                (!participantTeams.TryGetValue(command.ActorId, out ContentId actorTeamId) ||
                 actorTeamId != command.TeamId))
            {
                throw new InvalidOperationException(
                    $"Schedule command actor {command.ActorId} does not belong to " +
                    $"team {command.TeamId} in the frozen encounter graph.");
            }
        }
    }

    private static IReadOnlyList<BattleEncounterScheduleParticipantSnapshot>
        CaptureScheduleParticipants(IEnumerable<BattleEncounterParticipant> participants) =>
        Array.AsReadOnly(participants.Select(participant =>
        {
            RuntimeActorSnapshot actor = participant.State.ToSnapshot();
            return new BattleEncounterScheduleParticipantSnapshot(
                participant.InstanceId,
                participant.TeamId,
                participant.State.IsDeployed,
                participant.State.IsDefeated,
                actor.Stats.EffectiveStats);
        }).ToArray());

    private static IReadOnlyList<BattleEncounterParticipantSnapshot>
        CaptureParticipantSnapshots(IEnumerable<BattleEncounterParticipant> participants) =>
        Array.AsReadOnly(
            participants
                .Select(participant => new BattleEncounterParticipantSnapshot(participant))
                .ToArray());

    private static bool CanRecallToRoster(BattleEncounterParticipant participant) =>
        participant.State.HasCapability(ContentId.Parse("recall_to_roster"));

    private static bool IsExactTeamPermutation(
        IReadOnlyList<ContentId>? proposed,
        IReadOnlyList<ContentId> expected)
    {
        if (proposed is null || proposed.Count != expected.Count || proposed.Distinct().Count() != proposed.Count)
        {
            return false;
        }

        return proposed.All(expected.Contains);
    }

    private static string? ValidateEconomyTransition(
        BattleTurnEconomySnapshot before,
        BattleTurnEconomySnapshot after,
        bool hasTurnsRemaining,
        ActionTurnConsumption consumption)
    {
        if (before.EconomyId != after.EconomyId)
        {
            return $"Turn economy changed identity from {before.EconomyId} to {after.EconomyId} during a phase.";
        }

        if (before.GetType() != after.GetType())
        {
            return $"Turn economy {after.EconomyId} changed snapshot type from " +
                   $"{before.GetType().Name} to {after.GetType().Name} during a phase.";
        }

        string? stateFault = ValidateEconomyState(after, hasTurnsRemaining);
        if (stateFault is not null)
        {
            return stateFault;
        }

        if (consumption.Kind == ActionTurnConsumptionKind.TerminatePhase && hasTurnsRemaining)
        {
            return $"Turn economy {after.EconomyId} retained {after.RemainingActions} action(s) " +
                   "after explicit phase termination.";
        }

        bool economyChanged = !Equals(before, after);
        if (consumption.Kind == ActionTurnConsumptionKind.None && economyChanged)
        {
            return $"Turn economy {after.EconomyId} changed state for no-cost consumption.";
        }

        if (consumption.Kind != ActionTurnConsumptionKind.None && !economyChanged)
        {
            return $"Turn economy {after.EconomyId} did not advance for {consumption.Kind} consumption.";
        }

        return null;
    }

    private static string? ValidateEconomyContinuity(
        BattleTurnEconomySnapshot expected,
        BattleTurnEconomySnapshot actual)
    {
        if (expected.EconomyId != actual.EconomyId)
        {
            return $"Turn economy changed identity from {expected.EconomyId} to {actual.EconomyId} " +
                   "outside an accepted transition.";
        }

        if (expected.GetType() != actual.GetType())
        {
            return $"Turn economy {actual.EconomyId} changed snapshot type from " +
                   $"{expected.GetType().Name} to {actual.GetType().Name} outside an accepted transition.";
        }

        return !Equals(expected, actual)
            ? $"Turn economy {actual.EconomyId} changed state outside an accepted transition."
            : null;
    }

    private static string? ValidateEconomyAuthority(
        BattleTurnEconomySnapshot expected,
        BattleTurnEconomySnapshot actual,
        bool hasTurnsRemaining) =>
        ValidateEconomyContinuity(expected, actual) ??
        ValidateEconomyState(actual, hasTurnsRemaining);

    private static string? ValidateEconomyState(
        BattleTurnEconomySnapshot snapshot,
        bool hasTurnsRemaining) =>
        hasTurnsRemaining != (snapshot.RemainingActions > 0)
            ? $"Turn economy {snapshot.EconomyId} reported inconsistent remaining-action state."
            : null;

    private static IReadOnlyList<BattleEncounterEvent> MapStatusEvents(
        IEnumerable<BattleStatusLifecycleEvent> events) =>
        BattleStatusLifecycleEventMapper.MapAll(events, statusEvent => statusEvent.Detail);

    private static async ValueTask AnnounceNewDefeatsAsync(
        IEnumerable<BattleEncounterParticipant> participants,
        HashSet<RuntimeInstanceId> announced,
        Func<BattleEncounterEventKind, BattleEncounterEventPayload, string?, ValueTask> add)
    {
        foreach (BattleEncounterParticipant participant in participants.Where(participant =>
                     participant.State.IsDefeated && announced.Add(participant.InstanceId)))
        {
            await add(
                    BattleEncounterEventKind.ActorDefeated,
                    new BattleActorDefeatedEventPayload(participant.InstanceId, participant.TeamId),
                    $"{participant.InstanceId} was defeated.")
                .ConfigureAwait(false);
        }
    }
}
