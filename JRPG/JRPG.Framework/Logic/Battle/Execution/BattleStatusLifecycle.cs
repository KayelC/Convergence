using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;

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
    ContentId ActorId,
    ContentId? RelatedId = null,
    decimal? Value = null,
    string? Detail = null);

public sealed record BattleTurnStartLifecycleRequest(
    RuntimeActorState Actor,
    bool CanReturnToStock = false);

public sealed record BattleTurnStartLifecycleResult(
    BattleTurnStartOutcome Outcome,
    IReadOnlyList<BattleStatusLifecycleEvent> Events);

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

public sealed record BattleAilmentApplicationRequest(
    RuntimeActorState Actor,
    RuntimeActorState Target,
    AilmentDefinition Ailment,
    int Chance,
    DurationDefinition? Duration = null,
    bool IsRemovable = true);

public sealed record BattleAilmentApplicationResult(
    BattleAilmentApplicationStatus Status,
    IReadOnlyList<BattleStatusLifecycleEvent> Events)
{
    public bool Applied => Status == BattleAilmentApplicationStatus.Applied;
}

public sealed record BattleStatusCleanupRequest(
    RuntimeActorState Actor,
    BattleStatusCleanupScope Scope);

public sealed record BattleStatusLifecycleResult(IReadOnlyList<BattleStatusLifecycleEvent> Events);

public interface IBattleStatusLifecycleService
{
    BattleTurnStartLifecycleResult ProcessTurnStart(BattleTurnStartLifecycleRequest request);

    BattleTurnEndLifecycleResult ProcessTurnEnd(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services);

    BattleAilmentApplicationResult TryApplyAilment(BattleAilmentApplicationRequest request);

    BattleStatusLifecycleResult ApplyStatStage(
        RuntimeActorState target,
        ContentId modifierTrackId,
        int delta,
        DurationDefinition? duration = null);

    BattleStatusLifecycleResult Cleanup(BattleStatusCleanupRequest request);
}

public sealed class BattleStatusLifecycleService : IBattleStatusLifecycleService
{
    private readonly IRandomSource _random;

    public BattleStatusLifecycleService(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
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

        ActiveAilmentState? active = actor.Ailments.Values.FirstOrDefault();
        if (active is null)
        {
            return new BattleTurnStartLifecycleResult(BattleTurnStartOutcome.CanAct, events);
        }

        BattleTurnStartOutcome outcome = ResolveTurnStartOutcome(
            active.Definition.TurnBehavior,
            request.CanReturnToStock);
        if (outcome != BattleTurnStartOutcome.CanAct)
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.TurnRestricted,
                actor.InstanceId,
                active.Definition.Id,
                Detail: outcome.ToString()));
        }

        return new BattleTurnStartLifecycleResult(outcome, events);
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

    public BattleAilmentApplicationResult TryApplyAilment(BattleAilmentApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeActorState target = request.Target ?? throw new ArgumentNullException(nameof(request.Target));
        AilmentDefinition ailment = request.Ailment ?? throw new ArgumentNullException(nameof(request.Ailment));
        var events = new List<BattleStatusLifecycleEvent>();

        if (target.IsDefeated)
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.AilmentBlocked,
                target.InstanceId,
                ailment.Id,
                Detail: "target_defeated"));
            return new BattleAilmentApplicationResult(BattleAilmentApplicationStatus.TargetDefeated, events);
        }

        if (target.IsGuarding)
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.AilmentBlocked,
                target.InstanceId,
                ailment.Id,
                Detail: "guard"));
            return new BattleAilmentApplicationResult(BattleAilmentApplicationStatus.GuardBlocked, events);
        }

        if (AilmentResistanceResolver.Resolve(target.DefenseProfile, ailment.Id) == ResistanceLevel.Immune)
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.AilmentBlocked,
                target.InstanceId,
                ailment.Id,
                Detail: "immune"));
            return new BattleAilmentApplicationResult(BattleAilmentApplicationStatus.Immune, events);
        }

        int chance = Math.Clamp(request.Chance, 0, 100);
        if (_random.NextInt32(0, 100) >= chance)
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.AilmentMissed,
                target.InstanceId,
                ailment.Id,
                Detail: chance.ToString()));
            return new BattleAilmentApplicationResult(BattleAilmentApplicationStatus.Missed, events);
        }

        target.ApplyAilment(ailment, request.Duration ?? ailment.DefaultDuration, request.IsRemovable);
        events.Add(new BattleStatusLifecycleEvent(
            BattleStatusLifecycleEventKind.AilmentApplied,
            target.InstanceId,
            ailment.Id));
        return new BattleAilmentApplicationResult(BattleAilmentApplicationStatus.Applied, events);
    }

    public BattleStatusLifecycleResult ApplyStatStage(
        RuntimeActorState target,
        ContentId modifierTrackId,
        int delta,
        DurationDefinition? duration = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.ChangeStatStage(modifierTrackId, delta, duration);
        return new BattleStatusLifecycleResult(
        [
            new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.StatStageChanged,
                target.InstanceId,
                modifierTrackId,
                delta)
        ]);
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

    private BattleTurnStartOutcome ResolveTurnStartOutcome(
        AilmentTurnBehaviorDefinition behavior,
        bool canReturnToStock) =>
        behavior switch
        {
            NormalAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.CanAct,
            SkipAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.Skip,
            LimitedActionsAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.LimitedAction,
            ForcedBasicAttackAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.ForcedPhysical,
            ConfusedActionAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.ForcedConfusion,
            ChanceSkipAilmentTurnBehaviorDefinition chanceSkip =>
                Roll(chanceSkip.SkipChance) ? BattleTurnStartOutcome.Skip : BattleTurnStartOutcome.CanAct,
            ChanceSkipOrFleeAilmentTurnBehaviorDefinition fear => ResolveFearOutcome(fear, canReturnToStock),
            CustomAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.CanAct,
            _ => BattleTurnStartOutcome.CanAct
        };

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
                var actionRequest = new EffectActionExecutionRequest(
                    active.Definition.Id,
                    actor,
                    participants,
                    new EffectExecutionEnvironment(request.ContextId, request.BattleKindId, request.MoonPhaseId),
                    new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Any, true),
                    [actor.InstanceId]);

                for (int effectIndex = 0; effectIndex < trigger.Effects.Count; effectIndex++)
                {
                    EffectDefinition effect = trigger.Effects[effectIndex];
                    var context = new EffectExecutionContext(
                        actionRequest,
                        services,
                        effectIndex,
                        effect,
                        actor,
                        effect is DamageEffectDefinition damage ? damage.Element : null);
                    if (!BattleConditionEvaluator.Evaluate(effect.When, context))
                    {
                        continue;
                    }

                    EffectExecutionResult result = services.EffectExecutors.Execute(effect, context);
                    AddEffectEvent(events, actor.InstanceId, result, effect);
                    if (result.Outcome == EffectExecutionOutcome.Interrupted ||
                        result.Outcome == EffectExecutionOutcome.Failure &&
                        effect.OnFailure == EffectFailurePolicy.StopAction)
                    {
                        return;
                    }
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
        ContentId actorId,
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
        ContentId actorId,
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
