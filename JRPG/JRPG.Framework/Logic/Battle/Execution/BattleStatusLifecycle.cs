using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Execution;

public enum BattleTurnStartOutcome
{
    CanAct,
    Skip,
    LimitedAction,
    ForcedPhysical,
    ForcedConfusion,
    FleeBattle,
    ReturnToStock
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
    CleanupApplied
}

public sealed record BattleStatusLifecycleEvent(
    BattleStatusLifecycleEventKind Kind,
    RuntimeInstanceId ActorId,
    ContentId? RelatedId = null,
    decimal? Value = null,
    string? Detail = null);

public sealed record BattleTurnStartLifecycleRequest(
    RuntimeActorState Actor,
    bool CanReturnToStock = false);

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
    bool CanReturnToStock);

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
        BattleTurnStartOutcome.ReturnToStock => 6,
        BattleTurnStartOutcome.FleeBattle => 6,
        BattleTurnStartOutcome.Skip => 5,
        BattleTurnStartOutcome.ForcedConfusion => 4,
        BattleTurnStartOutcome.ForcedPhysical => 3,
        BattleTurnStartOutcome.LimitedAction => 2,
        BattleTurnStartOutcome.CanAct => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}

public sealed record BattleTurnEndLifecycleRequest(
    RuntimeActorState Actor,
    IEnumerable<RuntimeActorState> Participants,
    ContentId ContextId,
    ContentId EventId,
    ContentId? BattleKindId = null,
    ContentId? MoonPhaseId = null);

public sealed record BattleTurnEndLifecycleResult(
    IReadOnlyList<BattleStatusLifecycleEvent> Events,
    IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations);

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

public sealed record BattleAilmentApplicationResult(
    BattleAilmentApplicationStatus Status,
    IReadOnlyList<BattleStatusLifecycleEvent> Events)
{
    public bool Applied => Status == BattleAilmentApplicationStatus.Applied;
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
    public BattleActionEndLifecycleRequest(IEnumerable<RuntimeActorState> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        RuntimeActorState[] snapshot = participants.ToArray();
        if (snapshot.Any(actor => actor is null))
        {
            throw new ArgumentException("Duration lifecycle participants cannot contain null actors.", nameof(participants));
        }

        Participants = Array.AsReadOnly(
            snapshot.Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance).ToArray());
    }

    public IReadOnlyList<RuntimeActorState> Participants { get; }
}

public sealed record BattlePhaseEndLifecycleRequest
{
    public BattlePhaseEndLifecycleRequest(
        IEnumerable<RuntimeActorState> participants,
        ContentId phaseId)
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
    }

    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId PhaseId { get; }
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
    BattleStatusLifecycleResult ProcessActionEnd(BattleActionEndLifecycleRequest request);

    BattleStatusLifecycleResult ProcessPhaseEnd(BattlePhaseEndLifecycleRequest request);

    BattleStatusLifecycleResult Cleanup(BattleStatusCleanupRequest request);
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
        DurationDefinition? duration = null);

}

public sealed class BattleDurationLifecycleService : IBattleDurationLifecycleService
{
    public BattleStatusLifecycleResult ProcessActionEnd(BattleActionEndLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Expire(
            request.Participants,
            actor => actor.ExpireInstantDurations());
    }

    public BattleStatusLifecycleResult ProcessPhaseEnd(BattlePhaseEndLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Expire(
            request.Participants,
            actor => actor.ExpirePhaseDurations(request.PhaseId));
    }

    public BattleStatusLifecycleResult Cleanup(BattleStatusCleanupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeActorState actor = request.Actor ?? throw new ArgumentNullException(nameof(request.Actor));
        actor.ClearTransientStatuses();
        if (request.Scope is BattleStatusCleanupScope.BattleEnd or BattleStatusCleanupScope.FieldTransition)
        {
            actor.ClearEncounterStatuses();
        }

        return new BattleStatusLifecycleResult(
        [
            new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.CleanupApplied,
                actor.InstanceId,
                Detail: request.Scope.ToString())
        ]);
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
                        request.CanReturnToStock))
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
        var events = new List<BattleStatusLifecycleEvent>();
        var passiveActivations = new List<PassiveTriggerExecutionResult>();

        if (!actor.IsActive)
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
                request.MoonPhaseId),
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
            AddEffectEvents(events, actor.InstanceId, activation.Effects);
        }

        ExecuteAilmentTriggers(request, services, participants, events);
        ProcessAilmentRecovery(actor, request.EventId, events);
        ProcessDurationTicks(actor, request.EventId, events);

        return new BattleTurnEndLifecycleResult(events, passiveActivations);
    }

    public BattleAilmentApplicationResult TryApplyAilment(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services) =>
        (services ?? throw new ArgumentNullException(nameof(services)))
            .AilmentApplications.Apply(request, services);

    public BattleStatusLifecycleResult ProcessActionEnd(BattleActionEndLifecycleRequest request) =>
        _durationLifecycle.ProcessActionEnd(request);

    public BattleStatusLifecycleResult ProcessPhaseEnd(BattlePhaseEndLifecycleRequest request) =>
        _durationLifecycle.ProcessPhaseEnd(request);

    public BattleStatusLifecycleResult ApplyStatStage(
        RuntimeActorState target,
        ContentId modifierTrackId,
        int delta,
        DurationDefinition? duration = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        int appliedDelta = target.ChangeStatStage(modifierTrackId, delta, duration);
        return new BattleStatusLifecycleResult(
        [
            new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.StatStageChanged,
                target.InstanceId,
                modifierTrackId,
                appliedDelta)
        ]);
    }

    public BattleStatusLifecycleResult Cleanup(BattleStatusCleanupRequest request) =>
        _durationLifecycle.Cleanup(request);

    private BattleTurnStartRestriction ResolveTurnStartRestriction(
        RuntimeActorState actor,
        AilmentDefinition ailment,
        bool canReturnToStock)
    {
        AilmentTurnBehaviorDefinition behavior = ailment.TurnBehavior;
        CustomAilmentTurnBehaviorResult? customResult = behavior is CustomAilmentTurnBehaviorDefinition custom
            ? ResolveCustomBehavior(custom, actor, ailment, canReturnToStock)
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
            ChanceSkipOrFleeAilmentTurnBehaviorDefinition fear => ResolveFearOutcome(fear, canReturnToStock),
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
        bool canReturnToStock)
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
            new CustomAilmentTurnBehaviorRequest(actor, ailment, canReturnToStock))
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
        bool canReturnToStock)
    {
        int roll = _random.NextInt32(0, 100);
        if (roll < fear.FleeChance)
        {
            return canReturnToStock && fear.DemonFleeOutcome == DemonFleeOutcome.ReturnToStock
                ? BattleTurnStartOutcome.ReturnToStock
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
                    new EffectExecutionEnvironment(request.ContextId, request.BattleKindId, request.MoonPhaseId),
                    new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Any, true),
                    [actor.InstanceId]);

                OrderedEffectExecution execution = new OrderedEffectExecutor(
                    services,
                    services.EffectExecutors).Execute(
                        actionRequest,
                        trigger.Effects,
                        new ResolvedRuntimeTargetSet([actor]));
                foreach (EffectExecutionResult result in execution.Effects)
                {
                    if (result.EffectIndex >= 0 && result.EffectIndex < trigger.Effects.Count)
                    {
                        AddEffectEvent(
                            events,
                            actor.InstanceId,
                            result,
                            trigger.Effects[result.EffectIndex]);
                    }
                }

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
            int chance = Math.Clamp((int)(natural.BaseChance + stat * natural.StatMultiplier), 0, 100);
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

    private static void ProcessDurationTicks(
        RuntimeActorState actor,
        ContentId eventId,
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
    }

    private static void AddEffectEvents(
        List<BattleStatusLifecycleEvent> events,
        RuntimeInstanceId actorId,
        IReadOnlyList<EffectExecutionResult> effects)
    {
        foreach (EffectExecutionResult effect in effects.Where(effect => effect.Outcome == EffectExecutionOutcome.Success))
        {
            if (effect.RelatedId is ContentId relatedId && effect.Value is decimal value)
            {
                events.Add(new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.ResourceChanged,
                    effect.TargetId ?? actorId,
                    relatedId,
                    value,
                    effect.Detail));
            }
        }
    }

    private static void AddEffectEvent(
        List<BattleStatusLifecycleEvent> events,
        RuntimeInstanceId actorId,
        EffectExecutionResult result,
        EffectDefinition definition)
    {
        if (result.Outcome != EffectExecutionOutcome.Success ||
            result.RelatedId is not ContentId relatedId ||
            result.Value is not decimal value)
        {
            return;
        }

        decimal signedValue = definition is ReduceResourceEffectDefinition ? -Math.Abs(value) : value;
        events.Add(new BattleStatusLifecycleEvent(
            BattleStatusLifecycleEventKind.ResourceChanged,
            result.TargetId ?? actorId,
            relatedId,
            signedValue,
            result.Detail));
    }
}
