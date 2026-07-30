using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

/// <summary>Identifies a structural boundary selected by an encounter scheduling policy.</summary>
public enum BattleEncounterScheduleStepKind
{
    RoundStarted,
    PhaseStarted,
    CommandWindow,
    PhaseEnded,
    RoundEnded
}

/// <summary>Describes how the runner completed a scheduled structural boundary.</summary>
public enum BattleEncounterScheduleStepOutcomeKind
{
    BoundaryCompleted = 0,
    CommandCommitted = 1,
    ActorUnavailable = 2,
    TurnEconomyStarted = 3
}

/// <summary>Describes the result of starting or advancing a scheduling policy.</summary>
public enum BattleEncounterScheduleTransitionStatus
{
    Started,
    Advanced,
    Completed,
    Rejected
}

/// <summary>Stable diagnostic codes returned when a scheduling policy rejects a transition.</summary>
public enum BattleEncounterScheduleDiagnosticCode
{
    InvalidRequest = 0,
    InvalidState = 1,
    InvalidStep = 2,
    InvalidStepOutcome = 3,
    NoEligibleActor = 4,
    PolicyRejected = 5,
    MissingOrderingStat = 6,
    InvalidOrderingStat = 7,
    InvalidTieBreakOrder = 8,
    InvalidPostCommandDecision = 9,
    ImmediateRepeatLimitExceeded = 10
}

/// <summary>
/// Bounds accepted structural transitions across one encounter schedule.
/// This guard prevents a malformed custom scheduler from remaining forever in
/// round or phase boundaries without opening a command window or completing.
/// </summary>
public sealed class BattleEncounterProgressPolicy
{
    public BattleEncounterProgressPolicy(int maximumScheduleTransitions)
    {
        if (maximumScheduleTransitions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumScheduleTransitions),
                "The maximum schedule-transition count must be positive.");
        }

        MaximumScheduleTransitions = maximumScheduleTransitions;
    }

    public int MaximumScheduleTransitions { get; }
}

/// <summary>Explains why an encounter scheduling policy rejected a transition.</summary>
public sealed class BattleEncounterScheduleDiagnostic
{
    public BattleEncounterScheduleDiagnostic(
        BattleEncounterScheduleDiagnosticCode code,
        string message)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Schedule diagnostic message cannot be empty.", nameof(message));
        }

        Code = code;
        Message = message;
    }

    public BattleEncounterScheduleDiagnosticCode Code { get; }
    public string Message { get; }
}

/// <summary>
/// Immutable participant information available to scheduling policies.
/// Scheduling may inspect resolved stats, but it cannot mutate live actor state.
/// </summary>
public sealed class BattleEncounterScheduleParticipantSnapshot
{
    public BattleEncounterScheduleParticipantSnapshot(
        RuntimeInstanceId instanceId,
        ContentId teamId,
        bool isDeployed,
        bool isDefeated,
        IEnumerable<KeyValuePair<ContentId, decimal>>? effectiveStats = null)
    {
        if (!instanceId.IsValid)
        {
            throw new ArgumentException("Participant instance ID must be valid.", nameof(instanceId));
        }

        if (!teamId.IsValid)
        {
            throw new ArgumentException("Participant team ID must be valid.", nameof(teamId));
        }

        IReadOnlyDictionary<ContentId, decimal> statSnapshot =
            RuntimeSnapshotCollections.Dictionary(effectiveStats);
        foreach ((ContentId statId, _) in statSnapshot)
        {
            if (!statId.IsValid)
            {
                throw new ArgumentException(
                    "Participant effective-stat IDs must be valid.",
                    nameof(effectiveStats));
            }
        }

        InstanceId = instanceId;
        TeamId = teamId;
        IsDeployed = isDeployed;
        IsDefeated = isDefeated;
        EffectiveStats = statSnapshot;
    }

    public RuntimeInstanceId InstanceId { get; }
    public ContentId TeamId { get; }
    public bool IsDeployed { get; }
    public bool IsDefeated { get; }
    public bool IsAvailable => IsDeployed && !IsDefeated;
    public IReadOnlyDictionary<ContentId, decimal> EffectiveStats { get; }
}

/// <summary>Requests creation of an encounter schedule from a detached participant graph.</summary>
public sealed class BattleEncounterScheduleStartRequest
{
    public BattleEncounterScheduleStartRequest(
        IEnumerable<BattleEncounterScheduleParticipantSnapshot> participants,
        IEnumerable<ContentId> teamOrder,
        int roundLimit)
    {
        Participants = SnapshotParticipants(participants);
        TeamOrder = SnapshotTeamOrder(teamOrder);

        if (roundLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundLimit), "Round limit must be positive.");
        }

        ContentId[] participantTeams = Participants
            .Select(participant => participant.TeamId)
            .Distinct()
            .ToArray();
        if (participantTeams.Length != TeamOrder.Count ||
            participantTeams.Except(TeamOrder).Any())
        {
            throw new ArgumentException(
                "Team order must contain every participant team exactly once.",
                nameof(teamOrder));
        }

        RoundLimit = roundLimit;
    }

    public IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> Participants { get; }
    public IReadOnlyList<ContentId> TeamOrder { get; }
    public int RoundLimit { get; }

    internal static IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> SnapshotParticipants(
        IEnumerable<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> snapshot =
            RuntimeSnapshotCollections.List(
                participants ?? throw new ArgumentNullException(nameof(participants)));
        if (snapshot.Count == 0)
        {
            throw new ArgumentException(
                "An encounter schedule requires at least one participant.",
                nameof(participants));
        }

        if (snapshot.Select(participant => participant.InstanceId).Distinct().Count() != snapshot.Count)
        {
            throw new ArgumentException(
                "Encounter schedule participant instance IDs must be unique.",
                nameof(participants));
        }

        return snapshot;
    }

    internal static IReadOnlyList<ContentId> SnapshotTeamOrder(IEnumerable<ContentId> teamOrder)
    {
        IReadOnlyList<ContentId> snapshot =
            RuntimeSnapshotCollections.List(teamOrder ?? throw new ArgumentNullException(nameof(teamOrder)));
        if (snapshot.Count == 0)
        {
            throw new ArgumentException("Team order cannot be empty.", nameof(teamOrder));
        }

        if (snapshot.Any(teamId => !teamId.IsValid))
        {
            throw new ArgumentException("Team order IDs must be valid.", nameof(teamOrder));
        }

        if (snapshot.Distinct().Count() != snapshot.Count)
        {
            throw new ArgumentException("Team order cannot contain duplicates.", nameof(teamOrder));
        }

        return snapshot;
    }
}

/// <summary>
/// Immutable scheduler-owned state. Concrete scheduling policies may extend this
/// type with their own detached cursor data.
/// </summary>
public abstract class BattleEncounterScheduleStateSnapshot
{
    protected BattleEncounterScheduleStateSnapshot(
        ContentId policyId,
        long revision,
        int currentRound,
        int completedRounds,
        long nextStepSequence,
        IEnumerable<RuntimeInstanceId> participantIds,
        IEnumerable<ContentId> teamOrder,
        int roundLimit)
    {
        if (!policyId.IsValid)
        {
            throw new ArgumentException("Scheduling policy ID must be valid.", nameof(policyId));
        }

        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "Schedule revision cannot be negative.");
        }

        if (currentRound < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentRound), "Current round cannot be negative.");
        }

        if (completedRounds < 0 || completedRounds > currentRound)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedRounds),
                "Completed rounds must be between zero and the current round.");
        }

        if (nextStepSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextStepSequence),
                "The next schedule-step sequence cannot be negative.");
        }

        if (roundLimit <= 0 || currentRound > roundLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roundLimit),
                "Round limit must be positive and cannot precede the current round.");
        }

        IReadOnlyList<RuntimeInstanceId> participantSnapshot =
            RuntimeSnapshotCollections.List(
                participantIds ?? throw new ArgumentNullException(nameof(participantIds)));
        if (participantSnapshot.Count == 0 ||
            participantSnapshot.Any(instanceId => !instanceId.IsValid) ||
            participantSnapshot.Distinct().Count() != participantSnapshot.Count)
        {
            throw new ArgumentException(
                "Schedule participant IDs must be nonempty, valid, and unique.",
                nameof(participantIds));
        }

        PolicyId = policyId;
        Revision = revision;
        CurrentRound = currentRound;
        CompletedRounds = completedRounds;
        NextStepSequence = nextStepSequence;
        ParticipantIds = participantSnapshot;
        TeamOrder = BattleEncounterScheduleStartRequest.SnapshotTeamOrder(teamOrder);
        RoundLimit = roundLimit;
    }

    public ContentId PolicyId { get; }
    public long Revision { get; }
    public int CurrentRound { get; }
    public int CompletedRounds { get; }
    public long NextStepSequence { get; }
    public IReadOnlyList<RuntimeInstanceId> ParticipantIds { get; }
    public IReadOnlyList<ContentId> TeamOrder { get; }
    public int RoundLimit { get; }
}

/// <summary>Requests creation of a fresh turn-economy scope for a scheduled boundary.</summary>
public sealed class BattleEncounterTurnEconomyStart
{
    public BattleEncounterTurnEconomyStart(int activeActorCount)
    {
        if (activeActorCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeActorCount),
                "A scheduled turn-economy scope requires at least one active actor.");
        }

        ActiveActorCount = activeActorCount;
    }

    public int ActiveActorCount { get; }
}

/// <summary>Base type for one immutable scheduler-selected encounter boundary.</summary>
public abstract class BattleEncounterScheduleStep
{
    protected BattleEncounterScheduleStep(
        BattleEncounterScheduleStepKind kind,
        ContentId policyId,
        long sequence,
        int roundNumber,
        BattleEncounterTurnEconomyStart? turnEconomyStart = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!policyId.IsValid)
        {
            throw new ArgumentException("Scheduling policy ID must be valid.", nameof(policyId));
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber), "Round number must be positive.");
        }

        Kind = kind;
        PolicyId = policyId;
        Sequence = sequence;
        RoundNumber = roundNumber;
        TurnEconomyStart = turnEconomyStart;
    }

    public BattleEncounterScheduleStepKind Kind { get; }
    public ContentId PolicyId { get; }
    public long Sequence { get; }
    public int RoundNumber { get; }
    public BattleEncounterTurnEconomyStart? TurnEconomyStart { get; }
}

public sealed class BattleEncounterRoundStartedScheduleStep : BattleEncounterScheduleStep
{
    public BattleEncounterRoundStartedScheduleStep(ContentId policyId, long sequence, int roundNumber)
        : base(BattleEncounterScheduleStepKind.RoundStarted, policyId, sequence, roundNumber)
    {
    }
}

public sealed class BattleEncounterPhaseStartedScheduleStep : BattleEncounterScheduleStep
{
    public BattleEncounterPhaseStartedScheduleStep(
        ContentId policyId,
        long sequence,
        int roundNumber,
        ContentId teamId,
        BattleEncounterTurnEconomyStart turnEconomyStart)
        : base(
            BattleEncounterScheduleStepKind.PhaseStarted,
            policyId,
            sequence,
            roundNumber,
            turnEconomyStart ?? throw new ArgumentNullException(nameof(turnEconomyStart)))
    {
        if (!teamId.IsValid)
        {
            throw new ArgumentException("Phase team ID must be valid.", nameof(teamId));
        }

        TeamId = teamId;
    }

    public ContentId TeamId { get; }
}

public sealed class BattleEncounterCommandWindowScheduleStep : BattleEncounterScheduleStep
{
    public BattleEncounterCommandWindowScheduleStep(
        ContentId policyId,
        long sequence,
        int roundNumber,
        RuntimeInstanceId actorId,
        ContentId teamId,
        BattleEncounterTurnEconomyStart? turnEconomyStart = null)
        : base(
            BattleEncounterScheduleStepKind.CommandWindow,
            policyId,
            sequence,
            roundNumber,
            turnEconomyStart)
    {
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Command-window actor ID must be valid.", nameof(actorId));
        }

        if (!teamId.IsValid)
        {
            throw new ArgumentException("Command-window team ID must be valid.", nameof(teamId));
        }

        ActorId = actorId;
        TeamId = teamId;
    }

    public RuntimeInstanceId ActorId { get; }
    public ContentId TeamId { get; }
}

public sealed class BattleEncounterPhaseEndedScheduleStep : BattleEncounterScheduleStep
{
    public BattleEncounterPhaseEndedScheduleStep(
        ContentId policyId,
        long sequence,
        int roundNumber,
        ContentId teamId)
        : base(BattleEncounterScheduleStepKind.PhaseEnded, policyId, sequence, roundNumber)
    {
        if (!teamId.IsValid)
        {
            throw new ArgumentException("Phase team ID must be valid.", nameof(teamId));
        }

        TeamId = teamId;
    }

    public ContentId TeamId { get; }
}

public sealed class BattleEncounterRoundEndedScheduleStep : BattleEncounterScheduleStep
{
    public BattleEncounterRoundEndedScheduleStep(ContentId policyId, long sequence, int roundNumber)
        : base(BattleEncounterScheduleStepKind.RoundEnded, policyId, sequence, roundNumber)
    {
    }
}

/// <summary>Immutable evidence returned to a scheduler after one selected step completes.</summary>
public sealed class BattleEncounterScheduleStepOutcome
{
    private BattleEncounterScheduleStepOutcome(
        BattleEncounterScheduleStepOutcomeKind kind,
        RuntimeInstanceId? actorId,
        ActionTurnConsumption? turnConsumption,
        BattleTurnEconomySnapshot? economyBefore,
        BattleTurnEconomySnapshot? economyAfter,
        bool? hasRemainingOpportunities)
    {
        Kind = kind;
        ActorId = actorId;
        TurnConsumption = turnConsumption;
        EconomyBefore = economyBefore;
        EconomyAfter = economyAfter;
        HasRemainingOpportunities = hasRemainingOpportunities;
    }

    public BattleEncounterScheduleStepOutcomeKind Kind { get; }
    public RuntimeInstanceId? ActorId { get; }
    public ActionTurnConsumption? TurnConsumption { get; }
    public BattleTurnEconomySnapshot? EconomyBefore { get; }
    public BattleTurnEconomySnapshot? EconomyAfter { get; }
    public bool? HasRemainingOpportunities { get; }

    public static BattleEncounterScheduleStepOutcome BoundaryCompleted() =>
        new(
            BattleEncounterScheduleStepOutcomeKind.BoundaryCompleted,
            null,
            null,
            null,
            null,
            null);

    public static BattleEncounterScheduleStepOutcome TurnEconomyStarted(
        BattleTurnEconomySnapshot economyState,
        bool hasRemainingOpportunities) =>
        new(
            BattleEncounterScheduleStepOutcomeKind.TurnEconomyStarted,
            null,
            null,
            null,
            economyState ?? throw new ArgumentNullException(nameof(economyState)),
            hasRemainingOpportunities);

    public static BattleEncounterScheduleStepOutcome CommandCommitted(
        RuntimeInstanceId actorId,
        ActionTurnConsumption turnConsumption,
        BattleTurnEconomySnapshot economyBefore,
        BattleTurnEconomySnapshot economyAfter,
        bool hasRemainingOpportunities)
    {
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Committed command actor ID must be valid.", nameof(actorId));
        }

        return new(
            BattleEncounterScheduleStepOutcomeKind.CommandCommitted,
            actorId,
            turnConsumption ?? throw new ArgumentNullException(nameof(turnConsumption)),
            economyBefore ?? throw new ArgumentNullException(nameof(economyBefore)),
            economyAfter ?? throw new ArgumentNullException(nameof(economyAfter)),
            hasRemainingOpportunities);
    }

    public static BattleEncounterScheduleStepOutcome ActorUnavailable(RuntimeInstanceId actorId)
    {
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Unavailable actor ID must be valid.", nameof(actorId));
        }

        return new(
            BattleEncounterScheduleStepOutcomeKind.ActorUnavailable,
            actorId,
            null,
            null,
            null,
            null);
    }
}

/// <summary>Requests a scheduler transition after one previously selected step completes.</summary>
public sealed class BattleEncounterScheduleAdvanceRequest
{
    public BattleEncounterScheduleAdvanceRequest(
        BattleEncounterScheduleStateSnapshot state,
        BattleEncounterScheduleStep completedStep,
        BattleEncounterScheduleStepOutcome outcome,
        IEnumerable<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        CompletedStep = completedStep ?? throw new ArgumentNullException(nameof(completedStep));
        Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        Participants = BattleEncounterScheduleStartRequest.SnapshotParticipants(participants);

        if (State.PolicyId != CompletedStep.PolicyId)
        {
            throw new ArgumentException(
                "Completed step must belong to the schedule state's policy.",
                nameof(completedStep));
        }

        if (State.NextStepSequence != CompletedStep.Sequence)
        {
            throw new ArgumentException(
                "Completed step sequence does not match the schedule state's pending sequence.",
                nameof(completedStep));
        }

        if (State.CurrentRound != CompletedStep.RoundNumber)
        {
            throw new ArgumentException(
                "Completed step round does not match the schedule state's current round.",
                nameof(completedStep));
        }

        if (!State.ParticipantIds.SequenceEqual(Participants.Select(participant => participant.InstanceId)))
        {
            throw new ArgumentException(
                "Scheduling transitions cannot replace or reorder the encounter participant graph.",
                nameof(participants));
        }

        ValidateOutcome(CompletedStep, Outcome);
    }

    public BattleEncounterScheduleStateSnapshot State { get; }
    public BattleEncounterScheduleStep CompletedStep { get; }
    public BattleEncounterScheduleStepOutcome Outcome { get; }
    public IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> Participants { get; }

    private static void ValidateOutcome(
        BattleEncounterScheduleStep completedStep,
        BattleEncounterScheduleStepOutcome outcome)
    {
        if (completedStep is not BattleEncounterCommandWindowScheduleStep commandWindow)
        {
            BattleEncounterScheduleStepOutcomeKind expectedKind =
                completedStep.TurnEconomyStart is null
                    ? BattleEncounterScheduleStepOutcomeKind.BoundaryCompleted
                    : BattleEncounterScheduleStepOutcomeKind.TurnEconomyStarted;
            if (outcome.Kind != expectedKind)
            {
                throw new ArgumentException(
                    $"Schedule step '{completedStep.Kind}' requires a '{expectedKind}' outcome.",
                    nameof(outcome));
            }

            return;
        }

        if (outcome.Kind is BattleEncounterScheduleStepOutcomeKind.BoundaryCompleted
            or BattleEncounterScheduleStepOutcomeKind.TurnEconomyStarted)
        {
            throw new ArgumentException(
                "Command-window steps require a committed-command or actor-unavailable outcome.",
                nameof(outcome));
        }

        if (outcome.ActorId != commandWindow.ActorId)
        {
            throw new ArgumentException(
                "Command-window outcome actor does not match the scheduled actor.",
                nameof(outcome));
        }
    }
}

/// <summary>Immutable result returned by an encounter scheduling policy.</summary>
public sealed class BattleEncounterScheduleTransitionResult
{
    private BattleEncounterScheduleTransitionResult(
        BattleEncounterScheduleTransitionStatus status,
        BattleEncounterScheduleStateSnapshot? before,
        BattleEncounterScheduleStateSnapshot? after,
        BattleEncounterScheduleStep? nextStep,
        IEnumerable<BattleEncounterScheduleDiagnostic>? diagnostics)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        IReadOnlyList<BattleEncounterScheduleDiagnostic> diagnosticSnapshot =
            RuntimeSnapshotCollections.List(diagnostics);

        switch (status)
        {
            case BattleEncounterScheduleTransitionStatus.Started:
                if (before is not null || after is null || nextStep is null)
                {
                    throw new ArgumentException(
                        "A started schedule requires after-state and next-step values only.");
                }

                ValidateStart(after);
                ValidateReady(after, nextStep);
                break;
            case BattleEncounterScheduleTransitionStatus.Advanced:
                if (before is null || after is null || nextStep is null)
                {
                    throw new ArgumentException(
                        "An advanced schedule requires before-state, after-state, and next-step values.");
                }

                ValidateAdvance(before, after);
                ValidateReady(after, nextStep);
                break;
            case BattleEncounterScheduleTransitionStatus.Completed:
                if (before is null || after is null || nextStep is not null)
                {
                    throw new ArgumentException(
                        "A completed schedule requires before-state and after-state without a next step.");
                }

                ValidateAdvance(before, after);
                break;
            case BattleEncounterScheduleTransitionStatus.Rejected:
                if (nextStep is not null || diagnosticSnapshot.Count == 0)
                {
                    throw new ArgumentException(
                        "A rejected schedule requires diagnostics and cannot select a next step.");
                }

                if (before is null != (after is null))
                {
                    throw new ArgumentException(
                        "A rejected schedule must preserve both state references or have no state.");
                }

                if (before is not null && !ReferenceEquals(before, after))
                {
                    throw new ArgumentException(
                        "A rejected schedule transition must preserve its exact state snapshot.");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status != BattleEncounterScheduleTransitionStatus.Rejected &&
            diagnosticSnapshot.Count != 0)
        {
            throw new ArgumentException(
                "Successful schedule transitions cannot carry rejection diagnostics.",
                nameof(diagnostics));
        }

        Status = status;
        Before = before;
        After = after;
        NextStep = nextStep;
        Diagnostics = diagnosticSnapshot;
    }

    public BattleEncounterScheduleTransitionStatus Status { get; }
    public BattleEncounterScheduleStateSnapshot? Before { get; }
    public BattleEncounterScheduleStateSnapshot? After { get; }
    public BattleEncounterScheduleStep? NextStep { get; }
    public IReadOnlyList<BattleEncounterScheduleDiagnostic> Diagnostics { get; }

    public static BattleEncounterScheduleTransitionResult Start(
        BattleEncounterScheduleStateSnapshot state,
        BattleEncounterScheduleStep nextStep) =>
        new(
            BattleEncounterScheduleTransitionStatus.Started,
            null,
            state ?? throw new ArgumentNullException(nameof(state)),
            nextStep ?? throw new ArgumentNullException(nameof(nextStep)),
            null);

    public static BattleEncounterScheduleTransitionResult Advance(
        BattleEncounterScheduleStateSnapshot before,
        BattleEncounterScheduleStateSnapshot after,
        BattleEncounterScheduleStep nextStep) =>
        new(
            BattleEncounterScheduleTransitionStatus.Advanced,
            before ?? throw new ArgumentNullException(nameof(before)),
            after ?? throw new ArgumentNullException(nameof(after)),
            nextStep ?? throw new ArgumentNullException(nameof(nextStep)),
            null);

    public static BattleEncounterScheduleTransitionResult Complete(
        BattleEncounterScheduleStateSnapshot before,
        BattleEncounterScheduleStateSnapshot after) =>
        new(
            BattleEncounterScheduleTransitionStatus.Completed,
            before ?? throw new ArgumentNullException(nameof(before)),
            after ?? throw new ArgumentNullException(nameof(after)),
            null,
            null);

    public static BattleEncounterScheduleTransitionResult RejectStart(
        IEnumerable<BattleEncounterScheduleDiagnostic> diagnostics) =>
        new(
            BattleEncounterScheduleTransitionStatus.Rejected,
            null,
            null,
            null,
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));

    public static BattleEncounterScheduleTransitionResult RejectAdvance(
        BattleEncounterScheduleStateSnapshot state,
        IEnumerable<BattleEncounterScheduleDiagnostic> diagnostics) =>
        new(
            BattleEncounterScheduleTransitionStatus.Rejected,
            state ?? throw new ArgumentNullException(nameof(state)),
            state,
            null,
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));

    private static void ValidateReady(
        BattleEncounterScheduleStateSnapshot state,
        BattleEncounterScheduleStep nextStep)
    {
        if (state.PolicyId != nextStep.PolicyId ||
            state.NextStepSequence != nextStep.Sequence ||
            state.CurrentRound != nextStep.RoundNumber)
        {
            throw new ArgumentException(
                "Selected schedule step must match the after-state policy, sequence, and current round.",
                nameof(nextStep));
        }
    }

    private static void ValidateStart(BattleEncounterScheduleStateSnapshot state)
    {
        if (state.Revision != 0 ||
            state.CurrentRound != 1 ||
            state.CompletedRounds != 0 ||
            state.NextStepSequence != 0)
        {
            throw new ArgumentException(
                "A fresh schedule must start at revision zero, round one, zero completed rounds, and step zero.",
                nameof(state));
        }
    }

    private static void ValidateAdvance(
        BattleEncounterScheduleStateSnapshot before,
        BattleEncounterScheduleStateSnapshot after)
    {
        if (before.PolicyId != after.PolicyId ||
            after.Revision != checked(before.Revision + 1) ||
            !before.ParticipantIds.SequenceEqual(after.ParticipantIds) ||
            !before.TeamOrder.SequenceEqual(after.TeamOrder) ||
            before.RoundLimit != after.RoundLimit)
        {
            throw new ArgumentException(
                "Schedule advancement must increment revision once while preserving policy and encounter identity.");
        }

        if (after.NextStepSequence != checked(before.NextStepSequence + 1))
        {
            throw new ArgumentException(
                "Schedule advancement must increment the step sequence exactly once.");
        }
    }
}

/// <summary>
/// Selects structural encounter boundaries and command recipients without
/// executing actions or mutating a turn economy.
/// </summary>
public interface IBattleEncounterSchedulePolicy
{
    ContentId PolicyId { get; }

    BattleEncounterScheduleTransitionResult Start(BattleEncounterScheduleStartRequest request);

    BattleEncounterScheduleTransitionResult Advance(BattleEncounterScheduleAdvanceRequest request);
}

/// <summary>
/// Supplied scheduler that preserves team phases and rotates through the
/// currently available actors on the active team.
/// </summary>
public sealed class TeamPhaseRoundRobinBattleEncounterSchedulePolicy :
    IBattleEncounterSchedulePolicy
{
    public static ContentId ScheduleId { get; } = ContentId.Parse("team_phase_round_robin");

    public TeamPhaseRoundRobinBattleEncounterSchedulePolicy()
    {
    }

    public TeamPhaseRoundRobinBattleEncounterSchedulePolicy(
        BattleEncounterPostCommandScheduleExtension postCommandExtension)
    {
        PostCommandExtension = postCommandExtension
            ?? throw new ArgumentNullException(nameof(postCommandExtension));
    }

    public ContentId PolicyId => ScheduleId;
    public BattleEncounterPostCommandScheduleExtension? PostCommandExtension { get; }

    public BattleEncounterScheduleTransitionResult Start(
        BattleEncounterScheduleStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var state = new TeamPhaseRoundRobinScheduleState(
            revision: 0,
            currentRound: 1,
            completedRounds: 0,
            nextStepSequence: 0,
            request.Participants.Select(participant => participant.InstanceId),
            request.TeamOrder,
            request.RoundLimit,
            currentTeamIndex: -1,
            nextActorOffset: 0,
            consecutiveImmediateRepeats: 0);
        return BattleEncounterScheduleTransitionResult.Start(
            state,
            new BattleEncounterRoundStartedScheduleStep(PolicyId, 0, 1));
    }

    public BattleEncounterScheduleTransitionResult Advance(
        BattleEncounterScheduleAdvanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.State is not TeamPhaseRoundRobinScheduleState state)
        {
            return BattleEncounterScheduleTransitionResult.RejectAdvance(
                request.State,
                [new BattleEncounterScheduleDiagnostic(
                    BattleEncounterScheduleDiagnosticCode.InvalidState,
                    $"Scheduling policy '{PolicyId}' cannot advance state type " +
                    $"'{request.State.GetType().Name}'.")]);
        }

        return request.CompletedStep switch
        {
            BattleEncounterRoundStartedScheduleStep =>
                SelectNextPhaseOrRoundEnd(state, request.Participants, firstTeamIndex: 0),
            BattleEncounterPhaseStartedScheduleStep phase =>
                request.Outcome.HasRemainingOpportunities == true
                    ? SelectCommandOrPhaseEnd(state, phase.TeamId, request.Participants)
                    : SelectPhaseEnd(state, phase.TeamId),
            BattleEncounterCommandWindowScheduleStep command =>
                AdvanceCommand(state, command, request.Outcome, request.Participants),
            BattleEncounterPhaseEndedScheduleStep =>
                SelectNextPhaseOrRoundEnd(
                    state,
                    request.Participants,
                    firstTeamIndex: checked(state.CurrentTeamIndex + 1)),
            BattleEncounterRoundEndedScheduleStep =>
                AdvanceRound(state),
            _ => BattleEncounterScheduleTransitionResult.RejectAdvance(
                state,
                [new BattleEncounterScheduleDiagnostic(
                    BattleEncounterScheduleDiagnosticCode.InvalidStep,
                    $"Scheduling policy '{PolicyId}' does not support step type " +
                    $"'{request.CompletedStep.GetType().Name}'.")])
        };
    }

    private BattleEncounterScheduleTransitionResult AdvanceCommand(
        TeamPhaseRoundRobinScheduleState state,
        BattleEncounterCommandWindowScheduleStep command,
        BattleEncounterScheduleStepOutcome outcome,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        if (command.TeamId != state.TeamOrder[state.CurrentTeamIndex])
        {
            return Reject(
                state,
                BattleEncounterScheduleDiagnosticCode.InvalidStep,
                "The command-window team does not match the active team-phase cursor.");
        }

        bool continuePhase = outcome.Kind switch
        {
            BattleEncounterScheduleStepOutcomeKind.CommandCommitted =>
                outcome.HasRemainingOpportunities == true,
            BattleEncounterScheduleStepOutcomeKind.ActorUnavailable => true,
            _ => false
        };
        if (!continuePhase)
        {
            return SelectPhaseEnd(state, command.TeamId);
        }

        PostCommandScheduleEvaluation extension = BattleEncounterPostCommandScheduleEvaluator.Evaluate(
            PostCommandExtension,
            command,
            outcome,
            state.ConsecutiveImmediateRepeats);
        if (extension.IsRejected)
        {
            return Reject(
                state,
                extension.RejectionCode!.Value,
                extension.RejectionMessage!);
        }

        BattleEncounterScheduleParticipantSnapshot? actor = participants
            .SingleOrDefault(participant =>
                participant.InstanceId == command.ActorId &&
                participant.TeamId == command.TeamId &&
                participant.IsAvailable);
        if (extension.RetainActor && actor is not null)
        {
            TeamPhaseRoundRobinScheduleState retained = state.Advance(
                currentTeamIndex: state.CurrentTeamIndex,
                nextActorOffset: state.NextActorOffset,
                consecutiveImmediateRepeats:
                    checked(state.ConsecutiveImmediateRepeats + 1));
            return BattleEncounterScheduleTransitionResult.Advance(
                state,
                retained,
                new BattleEncounterCommandWindowScheduleStep(
                    ScheduleId,
                    retained.NextStepSequence,
                    retained.CurrentRound,
                    actor.InstanceId,
                    actor.TeamId));
        }

        return SelectCommandOrPhaseEnd(state, command.TeamId, participants);
    }

    private static BattleEncounterScheduleTransitionResult SelectCommandOrPhaseEnd(
        TeamPhaseRoundRobinScheduleState state,
        ContentId teamId,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        BattleEncounterScheduleParticipantSnapshot[] activeActors =
            ActiveTeam(participants, teamId);
        if (activeActors.Length == 0)
        {
            return SelectPhaseEnd(state, teamId);
        }

        int actorIndex = (int)(state.NextActorOffset % activeActors.Length);
        BattleEncounterScheduleParticipantSnapshot actor = activeActors[actorIndex];
        TeamPhaseRoundRobinScheduleState after = state.Advance(
            currentTeamIndex: state.CurrentTeamIndex,
            nextActorOffset: checked(state.NextActorOffset + 1),
            consecutiveImmediateRepeats: 0);
        return BattleEncounterScheduleTransitionResult.Advance(
            state,
            after,
            new BattleEncounterCommandWindowScheduleStep(
                ScheduleId,
                after.NextStepSequence,
                after.CurrentRound,
                actor.InstanceId,
                actor.TeamId));
    }

    private static BattleEncounterScheduleTransitionResult SelectPhaseEnd(
        TeamPhaseRoundRobinScheduleState state,
        ContentId teamId)
    {
        TeamPhaseRoundRobinScheduleState after = state.Advance(
            currentTeamIndex: state.CurrentTeamIndex,
            nextActorOffset: state.NextActorOffset,
            consecutiveImmediateRepeats: 0);
        return BattleEncounterScheduleTransitionResult.Advance(
            state,
            after,
            new BattleEncounterPhaseEndedScheduleStep(
                ScheduleId,
                after.NextStepSequence,
                after.CurrentRound,
                teamId));
    }

    private static BattleEncounterScheduleTransitionResult SelectNextPhaseOrRoundEnd(
        TeamPhaseRoundRobinScheduleState state,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        int firstTeamIndex)
    {
        for (int teamIndex = firstTeamIndex; teamIndex < state.TeamOrder.Count; teamIndex++)
        {
            ContentId teamId = state.TeamOrder[teamIndex];
            BattleEncounterScheduleParticipantSnapshot[] activeActors =
                ActiveTeam(participants, teamId);
            if (activeActors.Length == 0)
            {
                continue;
            }

            TeamPhaseRoundRobinScheduleState phaseState = state.Advance(
                currentTeamIndex: teamIndex,
                nextActorOffset: 0,
                consecutiveImmediateRepeats: 0);
            return BattleEncounterScheduleTransitionResult.Advance(
                state,
                phaseState,
                new BattleEncounterPhaseStartedScheduleStep(
                    ScheduleId,
                    phaseState.NextStepSequence,
                    phaseState.CurrentRound,
                    teamId,
                    new BattleEncounterTurnEconomyStart(activeActors.Length)));
        }

        TeamPhaseRoundRobinScheduleState roundEndState = state.Advance(
            currentTeamIndex: -1,
            nextActorOffset: 0,
            consecutiveImmediateRepeats: 0);
        return BattleEncounterScheduleTransitionResult.Advance(
            state,
            roundEndState,
            new BattleEncounterRoundEndedScheduleStep(
                ScheduleId,
                roundEndState.NextStepSequence,
                roundEndState.CurrentRound));
    }

    private static BattleEncounterScheduleTransitionResult AdvanceRound(
        TeamPhaseRoundRobinScheduleState state)
    {
        if (state.CurrentRound >= state.RoundLimit)
        {
            TeamPhaseRoundRobinScheduleState completed = state.Advance(
                currentRound: state.CurrentRound,
                completedRounds: state.CurrentRound,
                currentTeamIndex: -1,
                nextActorOffset: 0,
                consecutiveImmediateRepeats: 0);
            return BattleEncounterScheduleTransitionResult.Complete(state, completed);
        }

        TeamPhaseRoundRobinScheduleState nextRound = state.Advance(
            currentRound: checked(state.CurrentRound + 1),
            completedRounds: state.CurrentRound,
            currentTeamIndex: -1,
            nextActorOffset: 0,
            consecutiveImmediateRepeats: 0);
        return BattleEncounterScheduleTransitionResult.Advance(
            state,
            nextRound,
            new BattleEncounterRoundStartedScheduleStep(
                ScheduleId,
                nextRound.NextStepSequence,
                nextRound.CurrentRound));
    }

    private static BattleEncounterScheduleTransitionResult Reject(
        TeamPhaseRoundRobinScheduleState state,
        BattleEncounterScheduleDiagnosticCode code,
        string message) =>
        BattleEncounterScheduleTransitionResult.RejectAdvance(
            state,
            [new BattleEncounterScheduleDiagnostic(code, message)]);

    private static BattleEncounterScheduleParticipantSnapshot[] ActiveTeam(
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        ContentId teamId) =>
        participants
            .Where(participant => participant.TeamId == teamId && participant.IsAvailable)
            .ToArray();

    private sealed class TeamPhaseRoundRobinScheduleState :
        BattleEncounterScheduleStateSnapshot
    {
        public TeamPhaseRoundRobinScheduleState(
            long revision,
            int currentRound,
            int completedRounds,
            long nextStepSequence,
            IEnumerable<RuntimeInstanceId> participantIds,
            IEnumerable<ContentId> teamOrder,
            int roundLimit,
            int currentTeamIndex,
            long nextActorOffset,
            int consecutiveImmediateRepeats)
            : base(
                ScheduleId,
                revision,
                currentRound,
                completedRounds,
                nextStepSequence,
                participantIds,
                teamOrder,
                roundLimit)
        {
            if (currentTeamIndex < -1 || currentTeamIndex >= TeamOrder.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTeamIndex));
            }

            if (nextActorOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextActorOffset));
            }

            if (consecutiveImmediateRepeats < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(consecutiveImmediateRepeats));
            }

            CurrentTeamIndex = currentTeamIndex;
            NextActorOffset = nextActorOffset;
            ConsecutiveImmediateRepeats = consecutiveImmediateRepeats;
        }

        public int CurrentTeamIndex { get; }
        public long NextActorOffset { get; }
        public int ConsecutiveImmediateRepeats { get; }

        public TeamPhaseRoundRobinScheduleState Advance(
            int? currentRound = null,
            int? completedRounds = null,
            int? currentTeamIndex = null,
            long? nextActorOffset = null,
            int? consecutiveImmediateRepeats = null) =>
            new(
                checked(Revision + 1),
                currentRound ?? CurrentRound,
                completedRounds ?? CompletedRounds,
                checked(NextStepSequence + 1),
                ParticipantIds,
                TeamOrder,
                RoundLimit,
                currentTeamIndex ?? CurrentTeamIndex,
                nextActorOffset ?? NextActorOffset,
                consecutiveImmediateRepeats ?? ConsecutiveImmediateRepeats);
    }
}
