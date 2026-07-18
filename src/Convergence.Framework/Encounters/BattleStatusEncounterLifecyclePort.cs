using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

/// <summary>
/// Adapts the canonical status lifecycle and passive dispatcher to the encounter runner.
/// Hosts supply the event IDs because lifecycle vocabulary belongs to content registration.
/// </summary>
public sealed class BattleStatusEncounterLifecyclePort :
    IBattleEncounterLifecyclePort,
    IBattleEncounterStatModifierBoundarySource
{
    private readonly IBattleStatusLifecycleService _lifecycle;
    private readonly BattleExecutionServices _executionServices;
    private readonly ContentId _battleStartEventId;
    private readonly ContentId _ownerTurnEndEventId;
    private readonly Dictionary<RuntimeInstanceId, long> _ownerTurnSequences = [];

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

        if (request.Participants.Count == 0)
        {
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        RuntimeActorState[] participants = transaction.Participants
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

        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(statusEvents);
        transaction.Commit();
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
    }

    public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<BattleTurnStartLifecycleResult>(_lifecycle.ProcessTurnStart(
            new BattleTurnStartLifecycleRequest(request.Actor.State, request.CanRecallToRoster)));
    }

    public IReadOnlyList<StatModifierLifecycleBoundary> GetActiveStatModifierBoundaries(
        BattleEncounterTurnLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        long sequence = checked(_ownerTurnSequences.GetValueOrDefault(request.Actor.InstanceId) + 1);
        return Array.AsReadOnly<StatModifierLifecycleBoundary>(
            [new StatModifierLifecycleBoundary(_ownerTurnEndEventId, sequence)]);
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
        long sequence = checked(_ownerTurnSequences.GetValueOrDefault(request.Actor.InstanceId) + 1);
        BattleTurnEndLifecycleResult result = _lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                request.Actor.State,
                participants,
                request.Encounter.ContextId,
                _ownerTurnEndEventId,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId,
                new StatModifierLifecycleBoundary(_ownerTurnEndEventId, sequence)),
            _executionServices);
        _ownerTurnSequences[request.Actor.InstanceId] = sequence;
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapStatusEvents(result.Events));
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
        BattleEncounterLifecycleRequest request,
        ContentId teamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Participants.Count == 0)
        {
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        BattleStatusLifecycleResult result = _lifecycle.ProcessPhaseEnd(
            new BattlePhaseEndLifecycleRequest(
                transaction.Participants.Select(participant => participant.State),
                teamId),
            _executionServices.StatModifiers);
        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(result.Events);
        transaction.Commit();
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
        BattleEncounterLifecycleRequest request,
        BattleEncounterOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Participants.Count == 0)
        {
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        var statusEvents = new List<BattleStatusLifecycleEvent>();
        foreach (BattleEncounterParticipant participant in transaction.Participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusEvents.AddRange(_lifecycle.Cleanup(
                new BattleStatusCleanupRequest(
                    participant.State,
                    BattleStatusCleanupScope.BattleEnd),
                _executionServices.StatModifiers).Events);
        }

        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(statusEvents);
        transaction.Commit();
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
    }

    private static IReadOnlyList<BattleEncounterEvent> MapStatusEvents(
        IEnumerable<BattleStatusLifecycleEvent> events) =>
        BattleStatusLifecycleEventMapper.MapAll(events, StatusMessage);

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
            foreach (EffectExecutionResult effect in activation.Effects)
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
}
