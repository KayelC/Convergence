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
    IBattleEncounterDepartureLifecyclePort,
    IBattleEncounterStatModifierBoundarySource,
    IBattleEncounterLifecycleStateCheckpointPort
{
    private readonly IBattleStatusLifecycleService _lifecycle;
    private readonly BattleExecutionServices _executionServices;
    private readonly ContentId _battleStartEventId;
    private readonly ContentId _ownerTurnEndEventId;
    private readonly IBattleEncounterLifecycleClockPolicy _clockPolicy;
    private readonly Dictionary<ContentId, long> _lifecycleEventSequences = [];

    public BattleStatusEncounterLifecyclePort(
        IBattleStatusLifecycleService lifecycle,
        BattleExecutionServices executionServices,
        ContentId battleStartEventId,
        ContentId ownerTurnEndEventId,
        IBattleEncounterLifecycleClockPolicy clockPolicy)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _executionServices = executionServices ?? throw new ArgumentNullException(nameof(executionServices));
        _battleStartEventId = battleStartEventId;
        _ownerTurnEndEventId = ownerTurnEndEventId;
        _clockPolicy = clockPolicy ?? throw new ArgumentNullException(nameof(clockPolicy));
        _executionServices.PassiveEventPolicies.RegisterIfAbsent(
            _battleStartEventId,
            new PassiveEventPolicy(PassiveOwnerEligibility.DeployedOnly));
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

        using var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
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
            BattleStatusLifecycleService.AddPassiveEvents(statusEvents, actor.InstanceId, dispatch);
        }

        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(statusEvents);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
    }

    public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        BattleEncounterParticipant actor = transaction.GetStaged(request.Actor);
        BattleTurnStartLifecycleResult result = _lifecycle.ProcessTurnStart(
            new BattleTurnStartLifecycleRequest(actor.State, request.CanRecallToRoster));
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return new ValueTask<BattleTurnStartLifecycleResult>(result);
    }

    public IReadOnlyList<StatModifierLifecycleBoundary> GetActiveStatModifierBoundaries(
        BattleEncounterTurnLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        long sequence = GetNextLifecycleEventSequence(_ownerTurnEndEventId);
        return Array.AsReadOnly<StatModifierLifecycleBoundary>(
            [new StatModifierLifecycleBoundary(_ownerTurnEndEventId, sequence)]);
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        BattleEncounterParticipant actor = transaction.GetStaged(request.Actor);
        RuntimeActorState[] participants = transaction.Participants
            .Select(participant => participant.State)
            .ToArray();
        long sequence = GetNextLifecycleEventSequence(_ownerTurnEndEventId);
        BattleTurnEndLifecycleResult result = _lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                actor.State,
                participants,
                request.Encounter.ContextId,
                _ownerTurnEndEventId,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId,
                new StatModifierLifecycleBoundary(_ownerTurnEndEventId, sequence)),
            _executionServices);
        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(result.Events);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLifecycleEventSequence(_ownerTurnEndEventId, sequence);
        transaction.Commit();
        SetLifecycleEventSequence(_ownerTurnEndEventId, sequence);
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
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

        using var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        BattleTeamPhaseClockDefinition definition = _clockPolicy.ResolveTeamPhase(teamId);
        if (definition.TeamId != teamId)
        {
            throw new InvalidOperationException(
                $"Lifecycle clock policy resolved team '{teamId}' as '{definition.TeamId}'.");
        }
        long sequence = GetNextLifecycleEventSequence(definition.EventId);
        var boundary = new TeamPhaseLifecycleClockBoundary(
            definition.EventId,
            definition.TeamId,
            definition.PhaseId,
            sequence);
        BattleStatusLifecycleResult result = _lifecycle.ProcessClock(
            new BattleLifecycleClockRequest(
                transaction.Participants.Select(participant => participant.State),
                boundary,
                [new StatModifierLifecycleBoundary(definition.EventId, sequence)]),
            _executionServices.StatModifiers);
        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(result.Events);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLifecycleEventSequence(definition.EventId, sequence);
        transaction.Commit();
        SetLifecycleEventSequence(definition.EventId, sequence);
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
        BattleEncounterLifecycleRequest request,
        int roundNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Participants.Count == 0)
        {
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        using var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        ContentId eventId = _clockPolicy.RoundEndEventId;
        long sequence = GetNextLifecycleEventSequence(eventId);
        var boundary = new RoundLifecycleClockBoundary(eventId, roundNumber);
        BattleStatusLifecycleResult result = _lifecycle.ProcessClock(
            new BattleLifecycleClockRequest(
                transaction.Participants.Select(participant => participant.State),
                boundary,
                [new StatModifierLifecycleBoundary(eventId, sequence)]),
            _executionServices.StatModifiers);
        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(result.Events);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLifecycleEventSequence(eventId, sequence);
        transaction.Commit();
        SetLifecycleEventSequence(eventId, sequence);
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

        using var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        var statusEvents = new List<BattleStatusLifecycleEvent>();
        foreach (BattleEncounterParticipant participant in transaction.Participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusEvents.AddRange(_lifecycle.Cleanup(
                new BattleStatusCleanupRequest(
                    participant.State,
                    BattleStatusDepartureReason.BattleEnd),
                _executionServices.StatModifiers).Events);
        }

        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(statusEvents);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessActorDepartureAsync(
        BattleEncounterDepartureLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = new BattleEncounterLifecycleTransaction(request.Participants);
        BattleEncounterParticipant actor = transaction.GetStaged(request.Actor);
        BattleStatusLifecycleResult result = _lifecycle.Cleanup(
            new BattleStatusCleanupRequest(
                actor.State,
                request.Reason),
            _executionServices.StatModifiers);
        IReadOnlyList<BattleEncounterEvent> mappedEvents = MapStatusEvents(result.Events);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(mappedEvents);
    }

    private static IReadOnlyList<BattleEncounterEvent> MapStatusEvents(
        IEnumerable<BattleStatusLifecycleEvent> events) =>
        BattleStatusLifecycleEventMapper.MapAll(events, StatusMessage);

    private long GetNextLifecycleEventSequence(ContentId eventId) =>
        checked(_lifecycleEventSequences.GetValueOrDefault(eventId) + 1);

    private void SetLifecycleEventSequence(ContentId eventId, long sequence) =>
        _lifecycleEventSequences[eventId] = sequence;

    private void ValidateLifecycleEventSequence(ContentId eventId, long sequence)
    {
        long expected = GetNextLifecycleEventSequence(eventId);
        if (sequence != expected)
        {
            throw new InvalidOperationException(
                $"Lifecycle event '{eventId}' expected sequence {expected}, but received {sequence}.");
        }
    }

    object IBattleEncounterLifecycleStateCheckpointPort.CaptureLifecycleState() =>
        new Dictionary<ContentId, long>(_lifecycleEventSequences);

    void IBattleEncounterLifecycleStateCheckpointPort.RestoreLifecycleState(
        object checkpoint)
    {
        if (checkpoint is not IReadOnlyDictionary<ContentId, long> sequences)
        {
            throw new ArgumentException(
                "Lifecycle checkpoint has an incompatible shape.",
                nameof(checkpoint));
        }

        _lifecycleEventSequences.Clear();
        foreach ((ContentId eventId, long sequence) in sequences)
        {
            _lifecycleEventSequences.Add(eventId, sequence);
        }
    }

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
            BattleStatusLifecycleEventKind.PassiveEvaluated =>
                $"Lifecycle passive evaluated: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.PassiveEffectResolved =>
                $"Lifecycle passive effect resolved: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.DurationAdvanced =>
                $"Lifecycle duration advanced: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.StatusExpired =>
                $"Lifecycle status expired: {statusEvent.RelatedId}.",
            _ => statusEvent.Detail ?? $"Lifecycle status changed: {statusEvent.Kind}."
        };
}
