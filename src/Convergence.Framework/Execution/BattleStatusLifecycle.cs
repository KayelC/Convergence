using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Runtime;

namespace Convergence.Execution;

public enum BattleTurnStartOutcome
{
    CanAct,
    Skip,
    LimitedAction,
    ForcedPhysical,
    ForcedConfusion,
    FleeBattle,
    RecallToRoster
}

public enum BattleAilmentApplicationStatus
{
    Applied,
    TargetDefeated,
    GuardBlocked,
    Immune,
    Missed
}

public enum BattleStatusCleanupScope
{
    Swap,
    BattleEnd,
    FieldTransition
}

public enum BattleStatusLifecycleEventKind
{
    GuardCleared,
    TurnRestricted,
    AilmentApplied,
    AilmentBlocked,
    AilmentMissed,
    AilmentRemoved,
    AilmentRecovered,
    AilmentExpired,
    ResourceChanged,
    StatusExpired,
    StatStageChanged,
    PassiveTriggered,
    CleanupApplied,
    StatModifierChanged
}

public sealed record BattleStatusLifecycleEvent(
    BattleStatusLifecycleEventKind Kind,
    RuntimeInstanceId ActorId,
    ContentId? RelatedId = null,
    decimal? Value = null,
    string? Detail = null,
    StatModifierEvent? ModifierEvent = null);

public sealed record BattleTurnStartLifecycleRequest(
    RuntimeActorState Actor,
    bool CanRecallToRoster = false);

public sealed record BattleTurnStartRestriction
{
    public BattleTurnStartRestriction(
        BattleTurnStartOutcome outcome,
        IEnumerable<ContentId>? allowedActionIds = null,
        IEnumerable<ContentId>? sourceAilmentIds = null)
    {
        Outcome = outcome;
        AllowedActionIds = Array.AsReadOnly((allowedActionIds ?? []).Distinct().ToArray());
        SourceAilmentIds = Array.AsReadOnly((sourceAilmentIds ?? []).Distinct().ToArray());

        if (outcome == BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count == 0)
        {
            throw new ArgumentException(
                "A limited-action restriction must contain at least one allowed action.",
                nameof(allowedActionIds));
        }

        if (outcome != BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count > 0)
        {
            throw new ArgumentException(
                "Allowed action IDs are valid only for a limited-action restriction.",
                nameof(allowedActionIds));
        }
    }

    public BattleTurnStartOutcome Outcome { get; }
    public IReadOnlyList<ContentId> AllowedActionIds { get; }
    public IReadOnlyList<ContentId> SourceAilmentIds { get; }

    public static BattleTurnStartRestriction CanAct { get; } =
        new(BattleTurnStartOutcome.CanAct);
}

public sealed record BattleTurnStartLifecycleResult
{
    public BattleTurnStartLifecycleResult(
        BattleTurnStartOutcome outcome,
        IEnumerable<BattleStatusLifecycleEvent> events)
        : this(new BattleTurnStartRestriction(outcome), events)
    {
    }

    public BattleTurnStartLifecycleResult(
        BattleTurnStartRestriction restriction,
        IEnumerable<BattleStatusLifecycleEvent> events)
    {
        Restriction = restriction ?? throw new ArgumentNullException(nameof(restriction));
        Events = Array.AsReadOnly(events?.ToArray() ?? throw new ArgumentNullException(nameof(events)));
    }

    public BattleTurnStartRestriction Restriction { get; }
    public BattleTurnStartOutcome Outcome => Restriction.Outcome;
    public IReadOnlyList<ContentId> AllowedActionIds => Restriction.AllowedActionIds;
    public IReadOnlyList<BattleStatusLifecycleEvent> Events { get; }
}

public sealed record CustomAilmentTurnBehaviorRequest(
    RuntimeActorState Actor,
    AilmentDefinition Ailment,
    bool CanRecallToRoster);

public sealed record CustomAilmentTurnBehaviorResult
{
    public CustomAilmentTurnBehaviorResult(
        BattleTurnStartOutcome outcome,
        IEnumerable<ContentId>? allowedActionIds = null)
    {
        Outcome = outcome;
        AllowedActionIds = Array.AsReadOnly((allowedActionIds ?? []).Distinct().ToArray());

        if (outcome == BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count == 0)
        {
            throw new ArgumentException(
                "A custom limited-action result must contain at least one allowed action.",
                nameof(allowedActionIds));
        }

        if (outcome != BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count > 0)
        {
            throw new ArgumentException(
                "Custom allowed action IDs are valid only for a limited-action result.",
                nameof(allowedActionIds));
        }
    }

    public BattleTurnStartOutcome Outcome { get; }
    public IReadOnlyList<ContentId> AllowedActionIds { get; }
}

public interface ICustomAilmentTurnBehaviorHandler
{
    CustomAilmentTurnBehaviorResult Resolve(
        CustomAilmentTurnBehaviorDefinition behavior,
        CustomAilmentTurnBehaviorRequest request);
}

public interface IBattleTurnRestrictionPolicy
{
    BattleTurnStartRestriction Resolve(IReadOnlyList<BattleTurnStartRestriction> restrictions);
}

public sealed class MostRestrictiveBattleTurnPolicy : IBattleTurnRestrictionPolicy
{
    public BattleTurnStartRestriction Resolve(IReadOnlyList<BattleTurnStartRestriction> restrictions)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        BattleTurnStartRestriction[] effective = restrictions
            .Where(restriction => restriction.Outcome != BattleTurnStartOutcome.CanAct)
            .ToArray();
        if (effective.Length == 0)
        {
            return BattleTurnStartRestriction.CanAct;
        }

        int highestPrecedence = effective.Max(restriction => Precedence(restriction.Outcome));
        BattleTurnStartRestriction[] strongest = effective
            .Where(restriction => Precedence(restriction.Outcome) == highestPrecedence)
            .OrderBy(SourceKey, StringComparer.Ordinal)
            .ToArray();

        if (strongest[0].Outcome != BattleTurnStartOutcome.LimitedAction)
        {
            return strongest[0];
        }

        ContentId[] allowed = strongest[0].AllowedActionIds
            .Where(actionId => strongest.Skip(1).All(restriction => restriction.AllowedActionIds.Contains(actionId)))
            .ToArray();
        ContentId[] sources = strongest.SelectMany(restriction => restriction.SourceAilmentIds).Distinct().ToArray();
        return allowed.Length == 0
            ? new BattleTurnStartRestriction(BattleTurnStartOutcome.Skip, sourceAilmentIds: sources)
            : new BattleTurnStartRestriction(BattleTurnStartOutcome.LimitedAction, allowed, sources);
    }

    private static string SourceKey(BattleTurnStartRestriction restriction) =>
        restriction.SourceAilmentIds.Count == 0
            ? string.Empty
            : restriction.SourceAilmentIds[0].ToString();

    private static int Precedence(BattleTurnStartOutcome outcome) => outcome switch
    {
        BattleTurnStartOutcome.RecallToRoster => 6,
        BattleTurnStartOutcome.FleeBattle => 6,
        BattleTurnStartOutcome.Skip => 5,
        BattleTurnStartOutcome.ForcedConfusion => 4,
        BattleTurnStartOutcome.ForcedPhysical => 3,
        BattleTurnStartOutcome.LimitedAction => 2,
        BattleTurnStartOutcome.CanAct => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}

public sealed record BattleTurnEndLifecycleRequest
{
    public BattleTurnEndLifecycleRequest(
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        ContentId contextId,
        ContentId eventId,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null,
        StatModifierLifecycleBoundary? statModifierBoundary = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        ContextId = contextId;
        EventId = eventId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        StatModifierBoundary = statModifierBoundary;
        if (statModifierBoundary is not null && statModifierBoundary.EventId != eventId)
        {
            throw new ArgumentException(
                "The stat-modifier boundary must match the turn-end event ID.",
                nameof(statModifierBoundary));
        }
    }

    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId ContextId { get; }
    public ContentId EventId { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public StatModifierLifecycleBoundary? StatModifierBoundary { get; }
}

public sealed record BattleTurnEndLifecycleResult
{
    private readonly IReadOnlyList<BattleStatusLifecycleEvent> _events =
        Array.Empty<BattleStatusLifecycleEvent>();
    private readonly IReadOnlyList<PassiveTriggerExecutionResult> _passiveActivations =
        Array.Empty<PassiveTriggerExecutionResult>();

    public BattleTurnEndLifecycleResult(
        IReadOnlyList<BattleStatusLifecycleEvent> Events,
        IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations)
    {
        this.Events = Events;
        this.PassiveActivations = PassiveActivations;
    }

    public IReadOnlyList<BattleStatusLifecycleEvent> Events
    {
        get => _events;
        init => _events = Array.AsReadOnly(value?.ToArray() ?? []);
    }

    public IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations
    {
        get => _passiveActivations;
        init => _passiveActivations = Array.AsReadOnly(value?.ToArray() ?? []);
    }

    public void Deconstruct(
        out IReadOnlyList<BattleStatusLifecycleEvent> Events,
        out IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations)
    {
        Events = this.Events;
        PassiveActivations = this.PassiveActivations;
    }
}

public sealed record BattleAilmentApplicationRequest
{
    public BattleAilmentApplicationRequest(
        RuntimeActorState actor,
        RuntimeActorState target,
        AilmentDefinition ailment,
        int chance,
        DurationDefinition? duration = null,
        bool isRemovable = true,
        IEnumerable<RuntimeActorState>? participants = null,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null,
        SkillDefinition? skill = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Ailment = ailment ?? throw new ArgumentNullException(nameof(ailment));
        Chance = chance;
        Duration = duration;
        IsRemovable = isRemovable;
        Participants = Array.AsReadOnly((participants ?? [actor, target])
            .DistinctBy(participant => participant.InstanceId)
            .ToArray());
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        Skill = skill;
    }

    public RuntimeActorState Actor { get; }
    public RuntimeActorState Target { get; }
    public AilmentDefinition Ailment { get; }
    public int Chance { get; }
    public DurationDefinition? Duration { get; }
    public bool IsRemovable { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public SkillDefinition? Skill { get; }
}

public sealed record BattleAilmentApplicationResult
{
    private readonly IReadOnlyList<BattleStatusLifecycleEvent> _events =
        Array.Empty<BattleStatusLifecycleEvent>();

    public BattleAilmentApplicationResult(
        BattleAilmentApplicationStatus Status,
        IReadOnlyList<BattleStatusLifecycleEvent> Events)
    {
        this.Status = Status;
        this.Events = Events;
    }

    public BattleAilmentApplicationStatus Status { get; init; }
    public IReadOnlyList<BattleStatusLifecycleEvent> Events
    {
        get => _events;
        init => _events = Array.AsReadOnly(value?.ToArray() ?? []);
    }

    public bool Applied => Status == BattleAilmentApplicationStatus.Applied;

    public void Deconstruct(
        out BattleAilmentApplicationStatus Status,
        out IReadOnlyList<BattleStatusLifecycleEvent> Events)
    {
        Status = this.Status;
        Events = this.Events;
    }
}

public interface IBattleAilmentApplicationService
{
    BattleAilmentApplicationResult Apply(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services);
}

public sealed class BattleAilmentApplicationService : IBattleAilmentApplicationService
{
    public BattleAilmentApplicationResult Apply(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        RuntimeActorState target = request.Target;
        AilmentDefinition ailment = request.Ailment;
        if (target.IsDefeated)
        {
            return Blocked(request, BattleAilmentApplicationStatus.TargetDefeated, "target_defeated");
        }

        if (target.IsGuarding)
        {
            return Blocked(request, BattleAilmentApplicationStatus.GuardBlocked, "guard");
        }

        ResistanceLevel resistance = AilmentResistanceResolver.Resolve(target.DefenseProfile, ailment.Id);
        var conditionContext = new BattleConditionContext(
            target,
            request.Actor,
            request.Participants,
            request.BattleKindId,
            request.MoonPhaseId,
            services);
        resistance = services.RuleModifiers.ResolveAilmentResistance(
            target,
            ailment.Id,
            resistance,
            new RuleModifierContext(conditionContext, request.Skill));
        if (resistance == ResistanceLevel.Immune)
        {
            return Blocked(request, BattleAilmentApplicationStatus.Immune, "immune");
        }

        int chance = Math.Clamp(request.Chance, 0, 100);
        if (!services.AilmentPolicy.ShouldApply(
                new AilmentApplicationPolicyRequest(
                    request.Actor,
                    target,
                    chance,
                    ailment,
                    resistance)))
        {
            return new BattleAilmentApplicationResult(
                BattleAilmentApplicationStatus.Missed,
                [
                    new BattleStatusLifecycleEvent(
                        BattleStatusLifecycleEventKind.AilmentMissed,
                        target.InstanceId,
                        ailment.Id,
                        Detail: chance.ToString())
                ]);
        }

        target.ApplyAilment(
            ailment,
            request.Duration ?? ailment.DefaultDuration,
            request.IsRemovable);
        return new BattleAilmentApplicationResult(
            BattleAilmentApplicationStatus.Applied,
            [
                new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.AilmentApplied,
                    target.InstanceId,
                    ailment.Id)
            ]);
    }

    private static BattleAilmentApplicationResult Blocked(
        BattleAilmentApplicationRequest request,
        BattleAilmentApplicationStatus status,
        string detail) =>
        new(
            status,
            [
                new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.AilmentBlocked,
                    request.Target.InstanceId,
                    request.Ailment.Id,
                    Detail: detail)
            ]);
}

public sealed record BattleStatusCleanupRequest(
    RuntimeActorState Actor,
    BattleStatusCleanupScope Scope);

public sealed record BattleActionEndLifecycleRequest
{
    public BattleActionEndLifecycleRequest(
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<StatModifierLifecycleBoundary>? statModifierBoundaries = null)
    {
        ArgumentNullException.ThrowIfNull(participants);
        RuntimeActorState[] snapshot = participants.ToArray();
        if (snapshot.Any(actor => actor is null))
        {
            throw new ArgumentException("Duration lifecycle participants cannot contain null actors.", nameof(participants));
        }

        Participants = Array.AsReadOnly(
            snapshot.Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance).ToArray());
        StatModifierBoundaries = SnapshotBoundaries(statModifierBoundaries);
    }

    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public IReadOnlyList<StatModifierLifecycleBoundary> StatModifierBoundaries { get; }

    private static IReadOnlyList<StatModifierLifecycleBoundary> SnapshotBoundaries(
        IEnumerable<StatModifierLifecycleBoundary>? boundaries)
    {
        StatModifierLifecycleBoundary[] snapshot = (boundaries ?? []).ToArray();
        if (snapshot.Any(boundary => boundary is null) ||
            snapshot.Select(boundary => boundary.EventId).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Stat-modifier action boundaries must be non-null and unique by event ID.",
                nameof(boundaries));
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record BattlePhaseEndLifecycleRequest
{
    public BattlePhaseEndLifecycleRequest(
        IEnumerable<RuntimeActorState> participants,
        ContentId phaseId,
        IEnumerable<StatModifierLifecycleBoundary>? statModifierBoundaries = null)
    {
        ArgumentNullException.ThrowIfNull(participants);
        RuntimeActorState[] snapshot = participants.ToArray();
        if (snapshot.Any(actor => actor is null))
        {
            throw new ArgumentException("Duration lifecycle participants cannot contain null actors.", nameof(participants));
        }

        Participants = Array.AsReadOnly(
            snapshot.Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance).ToArray());
        PhaseId = phaseId;
        StatModifierBoundaries = Array.AsReadOnly((statModifierBoundaries ?? []).ToArray());
        if (StatModifierBoundaries.Any(boundary => boundary is null) ||
            StatModifierBoundaries.Select(boundary => boundary.EventId).Distinct().Count() !=
            StatModifierBoundaries.Count)
        {
            throw new ArgumentException(
                "Stat-modifier phase boundaries must be non-null and unique by event ID.",
                nameof(statModifierBoundaries));
        }
    }

    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId PhaseId { get; }
    public IReadOnlyList<StatModifierLifecycleBoundary> StatModifierBoundaries { get; }
}

public sealed record BattleStatusLifecycleResult
{
    public BattleStatusLifecycleResult(IEnumerable<BattleStatusLifecycleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Events = Array.AsReadOnly(events.ToArray());
    }

    public IReadOnlyList<BattleStatusLifecycleEvent> Events { get; }
}

public interface IBattleDurationLifecycleService
{
    BattleStatusLifecycleResult ProcessActionEnd(
        BattleActionEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers);

    BattleStatusLifecycleResult ProcessPhaseEnd(
        BattlePhaseEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers);

    BattleStatusLifecycleResult Cleanup(
        BattleStatusCleanupRequest request,
        IStatModifierPolicyService statModifiers);
}

public interface IBattleStatusLifecycleService : IBattleDurationLifecycleService
{
    BattleTurnStartLifecycleResult ProcessTurnStart(BattleTurnStartLifecycleRequest request);

    BattleTurnEndLifecycleResult ProcessTurnEnd(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services);

    BattleAilmentApplicationResult TryApplyAilment(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services);

    BattleStatusLifecycleResult ApplyStatStage(
        RuntimeActorState target,
        ContentId modifierTrackId,
        int delta,
        BattleExecutionServices services,
        DurationDefinition? duration = null,
        StatModifierLifecycleBoundary? activeBoundary = null);

}

public sealed class BattleDurationLifecycleService : IBattleDurationLifecycleService
{
    public BattleStatusLifecycleResult ProcessActionEnd(
        BattleActionEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(statModifiers);
        return ProcessParticipants(
            request.Participants,
            request.StatModifierBoundaries,
            statModifiers,
            actor => actor.ExpireInstantDurations());
    }

    public BattleStatusLifecycleResult ProcessPhaseEnd(
        BattlePhaseEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(statModifiers);
        return ProcessParticipants(
            request.Participants,
            request.StatModifierBoundaries,
            statModifiers,
            actor => actor.ExpirePhaseDurations(request.PhaseId));
    }

    public BattleStatusLifecycleResult Cleanup(
        BattleStatusCleanupRequest request,
        IStatModifierPolicyService statModifiers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(statModifiers);
        RuntimeActorState actor = request.Actor ?? throw new ArgumentNullException(nameof(request.Actor));
        var transaction = new RuntimeActorExecutionTransaction(actor, [actor]);
        RuntimeActorState staged = transaction.Actor;
        staged.ClearTransientStatuses();
        if (request.Scope is BattleStatusCleanupScope.BattleEnd or BattleStatusCleanupScope.FieldTransition)
        {
            staged.ClearEncounterStatuses();
        }

        RuntimeStatModifierStateSnapshot state = staged.ResolveStatModifierState(statModifiers);
        StatModifierTransitionResult modifierResult = statModifiers.Cleanup(
            new StatModifierCleanupRequest(state, MapCleanupScope(request.Scope)));
        RequireAccepted(modifierResult);
        if (modifierResult.StateChanged)
        {
            staged.ReplaceStatModifierState(statModifiers, modifierResult.After);
        }

        var events = new List<BattleStatusLifecycleEvent>();
        events.AddRange(MapModifierEvents(staged.InstanceId, modifierResult));
        events.Add(new BattleStatusLifecycleEvent(
            BattleStatusLifecycleEventKind.CleanupApplied,
            staged.InstanceId,
            Detail: request.Scope.ToString()));
        transaction.Commit();

        return new BattleStatusLifecycleResult(events);
    }

    private static BattleStatusLifecycleResult ProcessParticipants(
        IReadOnlyList<RuntimeActorState> participants,
        IReadOnlyList<StatModifierLifecycleBoundary> boundaries,
        IStatModifierPolicyService statModifiers,
        Func<RuntimeActorState, IReadOnlyList<BattleDurationTickResult>> expire)
    {
        if (participants.Count == 0)
        {
            return new BattleStatusLifecycleResult([]);
        }

        var transaction = new RuntimeActorExecutionTransaction(participants[0], participants);
        RuntimeActorState[] staged = participants.Select(transaction.GetStaged).ToArray();
        var events = new List<BattleStatusLifecycleEvent>();
        events.AddRange(Expire(staged, expire).Events);
        events.AddRange(TickModifiers(staged, boundaries, statModifiers));
        transaction.Commit();
        return new BattleStatusLifecycleResult(events);
    }

    private static BattleStatusLifecycleResult Expire(
        IEnumerable<RuntimeActorState> participants,
        Func<RuntimeActorState, IReadOnlyList<BattleDurationTickResult>> expire)
    {
        var events = new List<BattleStatusLifecycleEvent>();
        foreach (RuntimeActorState actor in participants)
        {
            foreach (BattleDurationTickResult duration in expire(actor))
            {
                events.Add(new BattleStatusLifecycleEvent(
                    duration.StateKind == BattleDurationStateKind.Ailment
                        ? BattleStatusLifecycleEventKind.AilmentExpired
                        : BattleStatusLifecycleEventKind.StatusExpired,
                    actor.InstanceId,
                    duration.Id,
                    Detail: $"{duration.StateKind}:{duration.PreviousDuration.Kind}"));
            }
        }

        return new BattleStatusLifecycleResult(events);
    }

    internal static IReadOnlyList<BattleStatusLifecycleEvent> TickModifiers(
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<StatModifierLifecycleBoundary> boundaries,
        IStatModifierPolicyService statModifiers)
    {
        var events = new List<BattleStatusLifecycleEvent>();
        foreach (RuntimeActorState actor in participants)
        {
            RuntimeStatModifierStateSnapshot state = actor.ResolveStatModifierState(statModifiers);
            foreach (StatModifierLifecycleBoundary boundary in boundaries)
            {
                StatModifierTransitionResult result = statModifiers.Tick(
                    new StatModifierTickRequest(state, boundary, actor.IsDeployed));
                RequireAccepted(result);
                state = result.After;
                events.AddRange(MapModifierEvents(actor.InstanceId, result));
            }

            if (!ReferenceEquals(state, actor.StatModifierState) &&
                (actor.StatModifierState is not null || state.Tracks.Count > 0))
            {
                actor.ReplaceStatModifierState(statModifiers, state);
            }
        }

        return Array.AsReadOnly(events.ToArray());
    }

    internal static IReadOnlyList<BattleStatusLifecycleEvent> MapModifierEvents(
        RuntimeInstanceId actorId,
        StatModifierTransitionResult result) =>
        Array.AsReadOnly(result.Events.Select(@event => new BattleStatusLifecycleEvent(
            @event.Kind == StatModifierEventKind.AggregateStageChanged
                ? BattleStatusLifecycleEventKind.StatStageChanged
                : @event.Kind == StatModifierEventKind.ContributionExpired
                    ? BattleStatusLifecycleEventKind.StatusExpired
                    : BattleStatusLifecycleEventKind.StatModifierChanged,
            actorId,
            @event.ModifierTrackId,
            @event.CurrentStage - @event.PreviousStage,
            @event.Kind.ToString(),
            @event)).ToArray());

    private static StatModifierCleanupScope MapCleanupScope(BattleStatusCleanupScope scope) =>
        scope switch
        {
            BattleStatusCleanupScope.Swap => StatModifierCleanupScope.Swap,
            BattleStatusCleanupScope.BattleEnd => StatModifierCleanupScope.EncounterEnd,
            BattleStatusCleanupScope.FieldTransition => StatModifierCleanupScope.FieldTransition,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };

    private static void RequireAccepted(StatModifierTransitionResult result)
    {
        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                "Stat-modifier lifecycle transition was rejected: " +
                string.Join("; ", result.Diagnostics.Select(value => value.Message)));
        }
    }
}

public sealed class BattleStatusLifecycleService : IBattleStatusLifecycleService
{
    private readonly IRandomSource _random;
    private readonly IReadOnlyDictionary<ContentId, ICustomAilmentTurnBehaviorHandler> _customTurnBehaviorHandlers;
    private readonly IBattleTurnRestrictionPolicy _turnRestrictionPolicy;
    private readonly IBattleDurationLifecycleService _durationLifecycle =
        new BattleDurationLifecycleService();

    public BattleStatusLifecycleService(
        IRandomSource random,
        IEnumerable<KeyValuePair<ContentId, ICustomAilmentTurnBehaviorHandler>>? customTurnBehaviorHandlers = null,
        IBattleTurnRestrictionPolicy? turnRestrictionPolicy = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _customTurnBehaviorHandlers = new ReadOnlyDictionary<ContentId, ICustomAilmentTurnBehaviorHandler>(
            (customTurnBehaviorHandlers ?? []).ToDictionary(
                pair => pair.Key,
                pair => pair.Value ?? throw new ArgumentException(
                    $"Custom ailment turn-behavior handler '{pair.Key}' cannot be null.",
                    nameof(customTurnBehaviorHandlers))));
        _turnRestrictionPolicy = turnRestrictionPolicy ?? new MostRestrictiveBattleTurnPolicy();
    }

    public BattleTurnStartLifecycleResult ProcessTurnStart(BattleTurnStartLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeActorState actor = request.Actor ?? throw new ArgumentNullException(nameof(request.Actor));
        var transaction = new RuntimeActorExecutionTransaction(actor, [actor]);
        BattleTurnStartLifecycleResult result = ProcessTurnStartCore(
            new BattleTurnStartLifecycleRequest(transaction.Actor, request.CanRecallToRoster));
        transaction.Commit();
        return result;
    }

    private BattleTurnStartLifecycleResult ProcessTurnStartCore(BattleTurnStartLifecycleRequest request)
    {
        RuntimeActorState actor = request.Actor;
        var events = new List<BattleStatusLifecycleEvent>();

        if (actor.IsGuarding)
        {
            actor.SetGuarding(false);
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.GuardCleared,
                actor.InstanceId));
        }

        BattleTurnStartRestriction restriction = _turnRestrictionPolicy.Resolve(
                actor.Ailments.Values
                    .Select(active => ResolveTurnStartRestriction(
                        actor,
                        active.Definition,
                        request.CanRecallToRoster))
                    .ToArray())
            ?? throw new InvalidOperationException("The battle turn-restriction policy returned null.");
        if (restriction.Outcome != BattleTurnStartOutcome.CanAct)
        {
            ContentId? sourceAilmentId = restriction.SourceAilmentIds.Count > 0
                ? restriction.SourceAilmentIds[0]
                : null;
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.TurnRestricted,
                actor.InstanceId,
                sourceAilmentId,
                Detail: RestrictionDetail(restriction)));
        }

        return new BattleTurnStartLifecycleResult(restriction, events);
    }

    public BattleTurnEndLifecycleResult ProcessTurnEnd(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        RuntimeActorState actor = request.Actor ?? throw new ArgumentNullException(nameof(request.Actor));
        RuntimeActorState[] participants = request.Participants?.ToArray()
            ?? throw new ArgumentNullException(nameof(request.Participants));
        RuntimeActorState[] transactionActors = participants
            .Append(actor)
            .Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance)
            .ToArray();
        var transaction = new RuntimeActorExecutionTransaction(actor, transactionActors);
        var stagedRequest = new BattleTurnEndLifecycleRequest(
            transaction.Actor,
            participants.Select(transaction.GetStaged),
            request.ContextId,
            request.EventId,
            request.BattleKindId,
            request.MoonPhaseId,
            request.StatModifierBoundary);
        BattleTurnEndLifecycleResult result = ProcessTurnEndCore(stagedRequest, services);
        transaction.Commit();
        return result;
    }

    private BattleTurnEndLifecycleResult ProcessTurnEndCore(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services)
    {
        RuntimeActorState actor = request.Actor;
        RuntimeActorState[] participants = request.Participants.ToArray();
        var events = new List<BattleStatusLifecycleEvent>();
        var passiveActivations = new List<PassiveTriggerExecutionResult>();

        if (!actor.IsDeployed)
        {
            return new BattleTurnEndLifecycleResult(events, passiveActivations);
        }

        PassiveTriggerDispatchResult dispatch = services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                request.EventId,
                actor,
                participants,
                [actor],
                request.ContextId,
                request.BattleKindId,
                request.MoonPhaseId,
                request.StatModifierBoundary is null ? [] : [request.StatModifierBoundary]),
            services);
        passiveActivations.AddRange(dispatch.Activations);
        foreach (PassiveTriggerExecutionResult activation in dispatch.Activations
                     .Where(activation => activation.Outcome == PassiveTriggerOutcome.Executed))
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.PassiveTriggered,
                actor.InstanceId,
                activation.SkillId,
                Detail: activation.EventId.ToString()));
            AddEffectEvents(events, activation.Effects);
        }

        ExecuteAilmentTriggers(request, services, participants, events);
        ProcessAilmentRecovery(actor, request.EventId, events);
        ProcessDurationTicks(
            actor,
            request.EventId,
            request.StatModifierBoundary,
            services.StatModifiers,
            events);

        return new BattleTurnEndLifecycleResult(events, passiveActivations);
    }

    public BattleAilmentApplicationResult TryApplyAilment(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services) =>
        (services ?? throw new ArgumentNullException(nameof(services)))
            .AilmentApplications.Apply(request, services);

    public BattleStatusLifecycleResult ProcessActionEnd(
        BattleActionEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers) =>
        _durationLifecycle.ProcessActionEnd(request, statModifiers);

    public BattleStatusLifecycleResult ProcessPhaseEnd(
        BattlePhaseEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers) =>
        _durationLifecycle.ProcessPhaseEnd(request, statModifiers);

    public BattleStatusLifecycleResult ApplyStatStage(
        RuntimeActorState target,
        ContentId modifierTrackId,
        int delta,
        BattleExecutionServices services,
        DurationDefinition? duration = null,
        StatModifierLifecycleBoundary? activeBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(services);
        var definition = new ModifyStatStageEffectDefinition(
            [modifierTrackId],
            delta,
            duration);
        var environment = new EffectExecutionEnvironment(
            ContentId.Parse("stat_modifier_lifecycle"),
            activeStatModifierBoundaries: activeBoundary is null ? [] : [activeBoundary]);
        StatModifierApplicationEvaluation evaluation = StatModifierExecution.Apply(
            target,
            definition,
            environment,
            services.StatModifiers);
        if (!evaluation.Accepted)
        {
            throw new InvalidOperationException(
                "Stat-modifier lifecycle application was rejected: " + evaluation.RejectionDetail);
        }

        return new BattleStatusLifecycleResult(evaluation.Transitions
            .SelectMany(transition =>
                BattleDurationLifecycleService.MapModifierEvents(target.InstanceId, transition)));
    }

    public BattleStatusLifecycleResult Cleanup(
        BattleStatusCleanupRequest request,
        IStatModifierPolicyService statModifiers) =>
        _durationLifecycle.Cleanup(request, statModifiers);

    private BattleTurnStartRestriction ResolveTurnStartRestriction(
        RuntimeActorState actor,
        AilmentDefinition ailment,
        bool canRecallToRoster)
    {
        AilmentTurnBehaviorDefinition behavior = ailment.TurnBehavior;
        CustomAilmentTurnBehaviorResult? customResult = behavior is CustomAilmentTurnBehaviorDefinition custom
            ? ResolveCustomBehavior(custom, actor, ailment, canRecallToRoster)
            : null;
        BattleTurnStartOutcome outcome = behavior switch
        {
            NormalAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.CanAct,
            SkipAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.Skip,
            LimitedActionsAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.LimitedAction,
            ForcedBasicAttackAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.ForcedPhysical,
            ConfusedActionAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.ForcedConfusion,
            ChanceSkipAilmentTurnBehaviorDefinition chanceSkip =>
                Roll(chanceSkip.SkipChance) ? BattleTurnStartOutcome.Skip : BattleTurnStartOutcome.CanAct,
            ChanceSkipOrFleeAilmentTurnBehaviorDefinition fear => ResolveFearOutcome(fear, canRecallToRoster),
            CustomAilmentTurnBehaviorDefinition => customResult!.Outcome,
            _ => throw new ArgumentOutOfRangeException(
                nameof(behavior),
                behavior,
                $"Unsupported ailment turn behavior '{behavior.GetType().Name}'.")
        };

        IReadOnlyList<ContentId> allowedActionIds = behavior switch
        {
            LimitedActionsAilmentTurnBehaviorDefinition limited => limited.AllowedActionIds,
            CustomAilmentTurnBehaviorDefinition => customResult!.AllowedActionIds,
            _ => []
        };

        return new BattleTurnStartRestriction(outcome, allowedActionIds, [ailment.Id]);
    }

    private CustomAilmentTurnBehaviorResult ResolveCustomBehavior(
        CustomAilmentTurnBehaviorDefinition behavior,
        RuntimeActorState actor,
        AilmentDefinition ailment,
        bool canRecallToRoster)
    {
        if (!_customTurnBehaviorHandlers.TryGetValue(
                behavior.HandlerId,
                out ICustomAilmentTurnBehaviorHandler? handler))
        {
            throw new InvalidOperationException(
                $"No custom ailment turn-behavior handler is registered for '{behavior.HandlerId}' " +
                $"while resolving ailment '{ailment.Id}'.");
        }

        return handler.Resolve(
            behavior,
            new CustomAilmentTurnBehaviorRequest(actor, ailment, canRecallToRoster))
            ?? throw new InvalidOperationException(
                $"Custom ailment turn-behavior handler '{behavior.HandlerId}' returned null.");
    }

    private static string RestrictionDetail(BattleTurnStartRestriction restriction)
    {
        string allowed = restriction.AllowedActionIds.Count == 0
            ? string.Empty
            : ":" + string.Join(",", restriction.AllowedActionIds);
        return restriction.Outcome + allowed;
    }

    private BattleTurnStartOutcome ResolveFearOutcome(
        ChanceSkipOrFleeAilmentTurnBehaviorDefinition fear,
        bool canRecallToRoster)
    {
        int roll = _random.NextInt32(0, 100);
        if (roll < fear.FleeChance)
        {
            return canRecallToRoster && fear.CompanionFleeOutcome == CompanionFleeOutcome.RecallToRoster
                ? BattleTurnStartOutcome.RecallToRoster
                : BattleTurnStartOutcome.FleeBattle;
        }

        return roll < fear.FleeChance + fear.SkipChance
            ? BattleTurnStartOutcome.Skip
            : BattleTurnStartOutcome.CanAct;
    }

    private bool Roll(int chance) => _random.NextInt32(0, 100) < Math.Clamp(chance, 0, 100);

    private static void ExecuteAilmentTriggers(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services,
        IReadOnlyList<RuntimeActorState> participants,
        List<BattleStatusLifecycleEvent> events)
    {
        RuntimeActorState actor = request.Actor;
        foreach (ActiveAilmentState active in actor.Ailments.Values.ToArray())
        {
            foreach (PassiveTriggerDefinition trigger in active.Definition.Triggers.Where(trigger => trigger.EventId == request.EventId))
            {
                var conditionContext = new BattleConditionContext(
                    actor,
                    actor,
                    participants,
                    request.BattleKindId,
                    request.MoonPhaseId,
                    services);
                if (!BattleConditionEvaluator.Evaluate(trigger.When, conditionContext))
                {
                    continue;
                }

                var actionRequest = new EffectActionExecutionRequest(
                    active.Definition.Id,
                    actor,
                    participants,
                    new EffectExecutionEnvironment(
                        request.ContextId,
                        request.BattleKindId,
                        request.MoonPhaseId,
                        request.StatModifierBoundary is null ? [] : [request.StatModifierBoundary]),
                    new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Any, true),
                    [actor.InstanceId]);

                OrderedEffectExecution execution = new OrderedEffectExecutor(
                    services,
                    services.EffectExecutors).Execute(
                        actionRequest,
                        trigger.Effects,
                        new ResolvedRuntimeTargetSet([actor]));
                AddEffectEvents(events, execution.Effects);

                if (execution.StopsAction)
                {
                    return;
                }
            }
        }
    }

    private void ProcessAilmentRecovery(
        RuntimeActorState actor,
        ContentId eventId,
        List<BattleStatusLifecycleEvent> events)
    {
        foreach (ActiveAilmentState active in actor.Ailments.Values.ToArray())
        {
            if (active.Definition.Recovery.RemoveOnEventIds.Contains(eventId))
            {
                IReadOnlyList<ContentId> removed = actor.RemoveAilments(candidate =>
                    candidate.Definition.Id == active.Definition.Id);
                if (removed.Count > 0)
                {
                    events.Add(new BattleStatusLifecycleEvent(
                        BattleStatusLifecycleEventKind.AilmentRemoved,
                        actor.InstanceId,
                        active.Definition.Id,
                        Detail: "event"));
                }

                continue;
            }

            NaturalAilmentRecoveryDefinition? natural = active.Definition.Recovery.Natural;
            if (natural is null)
            {
                continue;
            }

            decimal stat = actor.Stats.GetValueOrDefault(natural.StatId);
            int chance = ResolveNaturalRecoveryChance(natural, stat);
            if (!Roll(chance))
            {
                continue;
            }

            IReadOnlyList<ContentId> naturallyRemoved = actor.RemoveAilments(candidate =>
                candidate.Definition.Id == active.Definition.Id);
            if (naturallyRemoved.Count > 0)
            {
                events.Add(new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.AilmentRecovered,
                    actor.InstanceId,
                    active.Definition.Id,
                    chance,
                    "natural"));
            }
        }
    }

    private static int ResolveNaturalRecoveryChance(
        NaturalAilmentRecoveryDefinition recovery,
        decimal stat)
    {
        decimal baseChance = Math.Clamp(recovery.BaseChance, 0m, 100m);
        if (baseChance >= 100m || stat <= 0m || recovery.StatMultiplier <= 0m)
        {
            return decimal.ToInt32(Math.Floor(baseChance));
        }

        try
        {
            decimal chance = checked(baseChance + checked(stat * recovery.StatMultiplier));
            return decimal.ToInt32(Math.Clamp(Math.Floor(chance), 0m, 100m));
        }
        catch (OverflowException)
        {
            return 100;
        }
    }

    private static void ProcessDurationTicks(
        RuntimeActorState actor,
        ContentId eventId,
        StatModifierLifecycleBoundary? statModifierBoundary,
        IStatModifierPolicyService statModifiers,
        List<BattleStatusLifecycleEvent> events)
    {
        foreach (BattleDurationTickResult tick in actor.TickAilmentDurations(eventId))
        {
            if (tick.Expired)
            {
                events.Add(new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.AilmentExpired,
                    actor.InstanceId,
                    tick.Id));
            }
        }

        foreach (BattleDurationTickResult tick in actor.TickTimedStatuses(eventId))
        {
            if (tick.Expired)
            {
                events.Add(new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.StatusExpired,
                    actor.InstanceId,
                    tick.Id));
            }
        }

        if (statModifierBoundary is not null)
        {
            events.AddRange(BattleDurationLifecycleService.TickModifiers(
                [actor],
                [statModifierBoundary],
                statModifiers));
        }
    }

    private static void AddEffectEvents(
        List<BattleStatusLifecycleEvent> events,
        IReadOnlyList<EffectExecutionResult> effects)
    {
        foreach (EffectExecutionResult effect in effects)
        {
            foreach (ExecutionResourceChange change in effect.ResourceChanges)
            {
                events.Add(new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.ResourceChanged,
                    change.ActorId,
                    change.ResourceId,
                    change.Delta,
                    effect.Detail));
            }
        }
    }
}
