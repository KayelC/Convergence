using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

/// <summary>Identifies the semantic boundary that advances lifecycle state.</summary>
public enum BattleLifecycleClockKind
{
    ActorTurn,
    Action,
    TeamPhase,
    Round,
    Custom
}

/// <summary>Base contract for one immutable lifecycle-clock occurrence.</summary>
public abstract record BattleLifecycleClockBoundary
{
    protected BattleLifecycleClockBoundary(BattleLifecycleClockKind kind, long sequence)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Lifecycle-clock sequence must be positive.");
        }

        Kind = kind;
        Sequence = sequence;
    }

    public BattleLifecycleClockKind Kind { get; }
    public long Sequence { get; }
    public virtual ContentId? EventId => null;
}

/// <summary>Advances state tied to one actor's completed turn.</summary>
public sealed record ActorTurnLifecycleClockBoundary : BattleLifecycleClockBoundary
{
    public ActorTurnLifecycleClockBoundary(
        ContentId eventId,
        RuntimeInstanceId actorId,
        long sequence)
        : base(BattleLifecycleClockKind.ActorTurn, sequence)
    {
        if (!eventId.IsValid)
        {
            throw new ArgumentException("Actor-turn lifecycle event ID must be valid.", nameof(eventId));
        }
        if (actorId == default)
        {
            throw new ArgumentException("Actor-turn runtime ID must be valid.", nameof(actorId));
        }

        Event = eventId;
        ActorId = actorId;
    }

    private ContentId Event { get; }
    public override ContentId? EventId => Event;
    public RuntimeInstanceId ActorId { get; }
}

/// <summary>Expires state scoped to one completed action.</summary>
public sealed record ActionLifecycleClockBoundary : BattleLifecycleClockBoundary
{
    public ActionLifecycleClockBoundary(long sequence)
        : base(BattleLifecycleClockKind.Action, sequence)
    {
    }
}

/// <summary>Advances one explicitly mapped team-phase boundary.</summary>
public sealed record TeamPhaseLifecycleClockBoundary : BattleLifecycleClockBoundary
{
    public TeamPhaseLifecycleClockBoundary(
        ContentId eventId,
        ContentId teamId,
        ContentId phaseId,
        long sequence)
        : base(BattleLifecycleClockKind.TeamPhase, sequence)
    {
        if (!eventId.IsValid)
        {
            throw new ArgumentException("Team-phase lifecycle event ID must be valid.", nameof(eventId));
        }
        if (!teamId.IsValid)
        {
            throw new ArgumentException("Team ID must be valid.", nameof(teamId));
        }
        if (!phaseId.IsValid)
        {
            throw new ArgumentException("Phase ID must be valid.", nameof(phaseId));
        }

        Event = eventId;
        TeamId = teamId;
        PhaseId = phaseId;
    }

    private ContentId Event { get; }
    public override ContentId? EventId => Event;
    public ContentId TeamId { get; }
    public ContentId PhaseId { get; }
}

/// <summary>Advances one completed encounter round.</summary>
public sealed record RoundLifecycleClockBoundary : BattleLifecycleClockBoundary
{
    public RoundLifecycleClockBoundary(ContentId eventId, int roundNumber)
        : base(BattleLifecycleClockKind.Round, roundNumber)
    {
        if (!eventId.IsValid)
        {
            throw new ArgumentException("Round lifecycle event ID must be valid.", nameof(eventId));
        }

        Event = eventId;
        RoundNumber = roundNumber;
    }

    private ContentId Event { get; }
    public override ContentId? EventId => Event;
    public int RoundNumber { get; }
}

/// <summary>Advances a host-defined lifecycle event without assigning it hidden encounter semantics.</summary>
public sealed record CustomLifecycleClockBoundary : BattleLifecycleClockBoundary
{
    public CustomLifecycleClockBoundary(ContentId eventId, long sequence)
        : base(BattleLifecycleClockKind.Custom, sequence)
    {
        if (!eventId.IsValid)
        {
            throw new ArgumentException("Custom lifecycle event ID must be valid.", nameof(eventId));
        }

        Event = eventId;
    }

    private ContentId Event { get; }
    public override ContentId? EventId => Event;
}

/// <summary>Immutable input used to decide whether one reserve actor advances.</summary>
public sealed record BattleReserveLifecycleRequest
{
    public BattleReserveLifecycleRequest(
        RuntimeInstanceId actorId,
        ContentId teamId,
        BattleLifecycleClockBoundary boundary)
    {
        if (actorId == default)
        {
            throw new ArgumentException("Reserve actor runtime ID must be valid.", nameof(actorId));
        }
        if (!teamId.IsValid)
        {
            throw new ArgumentException("Reserve actor team ID must be valid.", nameof(teamId));
        }

        ActorId = actorId;
        TeamId = teamId;
        Boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
    }

    public RuntimeInstanceId ActorId { get; }
    public ContentId TeamId { get; }
    public BattleLifecycleClockBoundary Boundary { get; }
}

/// <summary>Selects the explicit encounter boundary, if any, that advances reserve state.</summary>
public interface IBattleReserveLifecyclePolicy
{
    bool ShouldAdvance(BattleReserveLifecycleRequest request);
}

/// <summary>Supplied default: reserve state retains its exact remaining lifetime.</summary>
public sealed class SuspendReserveLifecyclePolicy : IBattleReserveLifecyclePolicy
{
    public static SuspendReserveLifecyclePolicy Instance { get; } = new();

    public bool ShouldAdvance(BattleReserveLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return false;
    }
}

/// <summary>
/// Advances reserve state on one exact owning-team phase or round event. Per-action
/// and actor-turn clocks are deliberately rejected to prevent action-count-based aging.
/// </summary>
public sealed class AdvanceReserveOnEncounterClockPolicy : IBattleReserveLifecyclePolicy
{
    public AdvanceReserveOnEncounterClockPolicy(
        BattleLifecycleClockKind clockKind,
        ContentId eventId)
    {
        if (clockKind is not BattleLifecycleClockKind.TeamPhase and not BattleLifecycleClockKind.Round)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clockKind),
                "Reserve advancement may use only a team-phase or round encounter clock.");
        }
        if (!eventId.IsValid)
        {
            throw new ArgumentException("Reserve lifecycle event ID must be valid.", nameof(eventId));
        }

        ClockKind = clockKind;
        EventId = eventId;
    }

    public BattleLifecycleClockKind ClockKind { get; }
    public ContentId EventId { get; }

    public bool ShouldAdvance(BattleReserveLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Boundary.Kind != ClockKind || request.Boundary.EventId != EventId)
        {
            return false;
        }

        return request.Boundary is not TeamPhaseLifecycleClockBoundary phase ||
               phase.TeamId == request.TeamId;
    }
}

/// <summary>Requests one atomic lifecycle-clock transition over a fixed actor set.</summary>
public sealed record BattleLifecycleClockRequest
{
    public BattleLifecycleClockRequest(
        IEnumerable<RuntimeActorState> participants,
        BattleLifecycleClockBoundary boundary,
        IEnumerable<StatModifierLifecycleBoundary>? statModifierBoundaries = null)
    {
        ArgumentNullException.ThrowIfNull(participants);
        RuntimeActorState[] snapshot = participants.ToArray();
        if (snapshot.Any(actor => actor is null))
        {
            throw new ArgumentException("Lifecycle-clock participants cannot contain null actors.", nameof(participants));
        }

        Participants = Array.AsReadOnly(
            snapshot.Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance).ToArray());
        Boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));

        StatModifierLifecycleBoundary[] modifierSnapshot = (statModifierBoundaries ?? []).ToArray();
        if (modifierSnapshot.Any(value => value is null) ||
            modifierSnapshot.Select(value => value.EventId).Distinct().Count() != modifierSnapshot.Length)
        {
            throw new ArgumentException(
                "Stat-modifier clock boundaries must be non-null and unique by event ID.",
                nameof(statModifierBoundaries));
        }

        StatModifierBoundaries = Array.AsReadOnly(modifierSnapshot);
    }

    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public BattleLifecycleClockBoundary Boundary { get; }
    public IReadOnlyList<StatModifierLifecycleBoundary> StatModifierBoundaries { get; }
}
