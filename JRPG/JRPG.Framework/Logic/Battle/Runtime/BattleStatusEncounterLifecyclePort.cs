using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Runtime;

/// <summary>
/// Adapts the canonical status lifecycle and passive dispatcher to the encounter runner.
/// Hosts supply the event IDs because lifecycle vocabulary belongs to content registration.
/// </summary>
public sealed class BattleStatusEncounterLifecyclePort : IBattleEncounterLifecyclePort
{
    private readonly IBattleStatusLifecycleService _lifecycle;
    private readonly BattleExecutionServices _executionServices;
    private readonly ContentId _battleStartEventId;
    private readonly ContentId _ownerTurnEndEventId;

    public BattleStatusEncounterLifecyclePort(
        IBattleStatusLifecycleService lifecycle,
        BattleExecutionServices executionServices,
        ContentId battleStartEventId,
        ContentId ownerTurnEndEventId)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _executionServices = executionServices ?? throw new ArgumentNullException(nameof(executionServices));
        _battleStartEventId = battleStartEventId;
        _ownerTurnEndEventId = ownerTurnEndEventId;
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
        BattleEncounterLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        RuntimeActorState[] participants = request.Participants
            .Select(participant => participant.State)
            .ToArray();
        var statusEvents = new List<BattleStatusLifecycleEvent>();
        foreach (RuntimeActorState actor in participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PassiveTriggerDispatchResult dispatch = _executionServices.PassiveTriggers.Dispatch(
                new PassiveTriggerDispatchRequest(
                    _battleStartEventId,
                    actor,
                    participants,
                    [actor],
                    request.Encounter.ContextId,
                    request.Encounter.BattleKindId,
                    request.Encounter.MoonPhaseId),
                _executionServices);
            AddPassiveEvents(statusEvents, actor.InstanceId, dispatch);
        }

        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapStatusEvents(statusEvents));
    }

    public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<BattleTurnStartLifecycleResult>(_lifecycle.ProcessTurnStart(
            new BattleTurnStartLifecycleRequest(request.Actor.State, request.CanReturnToStock)));
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        RuntimeActorState[] participants = request.Participants
            .Select(participant => participant.State)
            .ToArray();
        BattleTurnEndLifecycleResult result = _lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                request.Actor.State,
                participants,
                request.Encounter.ContextId,
                _ownerTurnEndEventId,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId),
            _executionServices);
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapStatusEvents(result.Events));
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
        BattleEncounterLifecycleRequest request,
        ContentId teamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        BattleStatusLifecycleResult result = _lifecycle.ProcessPhaseEnd(
            new BattlePhaseEndLifecycleRequest(
                request.Participants.Select(participant => participant.State),
                teamId));
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapStatusEvents(result.Events));
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
        BattleEncounterLifecycleRequest request,
        BattleEncounterOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var statusEvents = new List<BattleStatusLifecycleEvent>();
        foreach (BattleEncounterParticipant participant in request.Participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusEvents.AddRange(_lifecycle.Cleanup(
                new BattleStatusCleanupRequest(
                    participant.State,
                    BattleStatusCleanupScope.BattleEnd)).Events);
        }

        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapStatusEvents(statusEvents));
    }

    private static IReadOnlyList<BattleEncounterEvent> MapStatusEvents(
        IEnumerable<BattleStatusLifecycleEvent> events) =>
        Array.AsReadOnly(events.Select(statusEvent => new BattleEncounterEvent(
            0,
            statusEvent.Kind switch
            {
                BattleStatusLifecycleEventKind.ResourceChanged => BattleEncounterEventKind.ResourceChanged,
                BattleStatusLifecycleEventKind.PassiveTriggered => BattleEncounterEventKind.PassiveActivated,
                _ => BattleEncounterEventKind.StatusChanged
            },
            StatusMessage(statusEvent),
            statusEvent.ActorId,
            SourceId: statusEvent.RelatedId,
            Value: statusEvent.Value)).ToArray());

    private static string StatusMessage(BattleStatusLifecycleEvent statusEvent) =>
        statusEvent.Kind switch
        {
            BattleStatusLifecycleEventKind.ResourceChanged when statusEvent.RelatedId is ContentId resourceId =>
                $"Lifecycle resource changed: {resourceId} {statusEvent.Value:+0.##;-0.##;0}.",
            BattleStatusLifecycleEventKind.AilmentRecovered =>
                $"Lifecycle ailment recovered: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.AilmentExpired =>
                $"Lifecycle ailment expired: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.AilmentRemoved =>
                $"Lifecycle ailment removed: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.PassiveTriggered =>
                $"Lifecycle passive triggered: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.StatusExpired =>
                $"Lifecycle status expired: {statusEvent.RelatedId}.",
            _ => statusEvent.Detail ?? $"Lifecycle status changed: {statusEvent.Kind}."
        };

    private static void AddPassiveEvents(
        List<BattleStatusLifecycleEvent> events,
        RuntimeInstanceId actorId,
        PassiveTriggerDispatchResult dispatch)
    {
        foreach (PassiveTriggerExecutionResult activation in dispatch.Activations
                     .Where(activation => activation.Outcome == PassiveTriggerOutcome.Executed))
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.PassiveTriggered,
                actorId,
                activation.SkillId,
                Detail: activation.EventId.ToString()));
            foreach (EffectExecutionResult effect in activation.Effects
                         .Where(effect => effect.Outcome == EffectExecutionOutcome.Success))
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
    }
}
