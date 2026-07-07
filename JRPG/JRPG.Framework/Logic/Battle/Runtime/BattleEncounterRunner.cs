using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Runtime;

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

public enum BattleEncounterEventKind
{
    ActorCreated,
    BattleStarted,
    InitiativeRolled,
    RoundStarted,
    PhaseStarted,
    TurnStarted,
    TurnRestricted,
    CommandSelected,
    CommandPassed,
    ActionExecuted,
    ActionRejected,
    EffectResolved,
    PassiveActivated,
    StatusChanged,
    ResourceChanged,
    PressTurnChanged,
    DeploymentChanged,
    ActorDefeated,
    PhaseEnded,
    BattleFaulted,
    BattleEnded,
    HostActionRequested
}

public sealed record PressTurnStateSnapshot
{
    public PressTurnStateSnapshot(int fullIcons, int blinkingIcons)
    {
        if (fullIcons < 0) throw new ArgumentOutOfRangeException(nameof(fullIcons));
        if (blinkingIcons < 0) throw new ArgumentOutOfRangeException(nameof(blinkingIcons));
        FullIcons = fullIcons;
        BlinkingIcons = blinkingIcons;
    }

    public int FullIcons { get; }
    public int BlinkingIcons { get; }
}

public sealed record BattleEncounterEvent(
    int Sequence,
    BattleEncounterEventKind Kind,
    string Message,
    RuntimeInstanceId? ActorId = null,
    RuntimeInstanceId? TargetId = null,
    ContentId? SourceId = null,
    decimal? Value = null,
    PressTurnStateSnapshot? PressTurnState = null);

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

public sealed record BattleEncounterRequest
{
    public BattleEncounterRequest(
        IEnumerable<BattleEncounterParticipant> participants,
        ContentId contextId,
        ContentId battleKindId,
        ContentId? moonPhaseId,
        int roundLimit)
    {
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
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
        string? faultMessage = null)
    {
        Outcome = outcome;
        WinningTeamId = winningTeamId;
        Participants = Array.AsReadOnly(participants.ToArray());
        Events = Array.AsReadOnly(events.ToArray());
        FaultMessage = faultMessage;
    }

    public BattleEncounterOutcome Outcome { get; }
    public ContentId? WinningTeamId { get; }
    public IReadOnlyList<BattleEncounterParticipant> Participants { get; }
    public IReadOnlyList<BattleEncounterEvent> Events { get; }
    public string? FaultMessage { get; }
}

public sealed record BattleEncounterInitiativeRequest(IReadOnlyList<BattleEncounterParticipant> Participants);

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
    bool CanReturnToStock);

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

    ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
        BattleEncounterLifecycleRequest request,
        BattleEncounterOutcome outcome,
        CancellationToken cancellationToken = default);
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

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
        BattleEncounterLifecycleRequest request,
        BattleEncounterOutcome outcome,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());
}

public sealed record BattleEncounterTurnRequest(
    BattleEncounterRequest Encounter,
    BattleEncounterParticipant Actor,
    IReadOnlyList<BattleEncounterParticipant> Participants,
    BattleTurnStartOutcome TurnStartOutcome,
    int FullPressTurnIcons,
    int BlinkingPressTurnIcons);

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
        Status = status;
        TurnConsumption = turnConsumption;
        Events = Array.AsReadOnly(events?.ToArray() ?? []);
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
}

public interface IBattleEncounterTurnHandler
{
    ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
        BattleEncounterTurnRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record BattleEncounterCompletionRequest(
    IReadOnlyList<BattleEncounterParticipant> Participants,
    BattleEncounterParticipant? LastActor = null);

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
            .Where(participant => participant.State.IsActive && !participant.State.IsDefeated)
            .Select(participant => participant.TeamId)
            .Distinct()
            .ToArray();

        return livingTeams.Length == 1
            ? new BattleEncounterCompletion(true, BattleEncounterOutcome.Victory, livingTeams[0])
            : new BattleEncounterCompletion(false);
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

public interface IBattleEncounterEventSink
{
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
        IBattleEncounterLifecyclePort lifecycle,
        IBattleEncounterTurnHandler turnHandler,
        IBattleEncounterCompletionPolicy completion,
        IBattleEncounterStateSynchronizer? synchronizer = null,
        IBattleEncounterEventSink? events = null,
        Func<PressTurnEngine>? pressTurnFactory = null)
    {
        Initiative = initiative ?? throw new ArgumentNullException(nameof(initiative));
        Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        TurnHandler = turnHandler ?? throw new ArgumentNullException(nameof(turnHandler));
        Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        Synchronizer = synchronizer ?? NoopBattleEncounterStateSynchronizer.Instance;
        Events = events ?? NoopBattleEncounterEventSink.Instance;
        PressTurnFactory = pressTurnFactory ?? (() => new PressTurnEngine());
    }

    public IBattleEncounterInitiativePolicy Initiative { get; }
    public IBattleEncounterLifecyclePort Lifecycle { get; }
    public IBattleEncounterTurnHandler TurnHandler { get; }
    public IBattleEncounterCompletionPolicy Completion { get; }
    public IBattleEncounterStateSynchronizer Synchronizer { get; }
    public IBattleEncounterEventSink Events { get; }
    public Func<PressTurnEngine> PressTurnFactory { get; }
}

public interface IBattleEncounterRunner
{
    ValueTask<BattleEncounterResult> RunAsync(
        BattleEncounterRequest request,
        BattleEncounterServices services,
        CancellationToken cancellationToken = default);
}

public sealed class BattleEncounterRunner : IBattleEncounterRunner
{
    public BattleEncounterResult Run(BattleEncounterRequest request, BattleEncounterServices services) =>
        RunAsync(request, services).AsTask().GetAwaiter().GetResult();

    public async ValueTask<BattleEncounterResult> RunAsync(
        BattleEncounterRequest request,
        BattleEncounterServices services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        if (request.RoundLimit <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Round limit must be positive.");
        if (request.Participants.Count == 0) throw new ArgumentException("A battle requires participants.", nameof(request));

        var events = new List<BattleEncounterEvent>();
        var defeatedAnnouncements = new HashSet<RuntimeInstanceId>();
        int sequence = 0;
        async ValueTask AddAsync(
            BattleEncounterEventKind kind,
            string message,
            RuntimeInstanceId? actor = null,
            RuntimeInstanceId? target = null,
            ContentId? source = null,
            decimal? value = null)
        {
            var battleEvent = new BattleEncounterEvent(++sequence, kind, message, actor, target, source, value);
            events.Add(battleEvent);
            await services.Events.PublishAsync(battleEvent, cancellationToken);
        }

        async ValueTask AddPressTurnAsync(RuntimeInstanceId actor, PressTurnEngine pressTurn)
        {
            var battleEvent = new BattleEncounterEvent(
                ++sequence,
                BattleEncounterEventKind.PressTurnChanged,
                $"Press Turn: {pressTurn.FullIcons} full, {pressTurn.BlinkingIcons} blinking.",
                actor,
                PressTurnState: new PressTurnStateSnapshot(
                    pressTurn.FullIcons,
                    pressTurn.BlinkingIcons));
            events.Add(battleEvent);
            await services.Events.PublishAsync(battleEvent, cancellationToken);
        }

        async ValueTask AddRangeAsync(IEnumerable<BattleEncounterEvent> unsequenced)
        {
            foreach (BattleEncounterEvent battleEvent in unsequenced)
            {
                var sequenced = battleEvent with { Sequence = ++sequence };
                events.Add(sequenced);
                await services.Events.PublishAsync(sequenced, cancellationToken);
            }
        }

        services.Synchronizer.Synchronize(request.Participants);
        foreach (BattleEncounterParticipant participant in request.Participants)
        {
            participant.State.Passives.ResetBattleActivations();
            await AddAsync(
                BattleEncounterEventKind.ActorCreated,
                $"Created {participant.DisplayName} as {participant.InstanceId} on {participant.TeamId}.",
                participant.InstanceId);
        }

        IReadOnlyList<ContentId> teamOrder = services.Initiative.DetermineTeamOrder(
            new BattleEncounterInitiativeRequest(request.Participants));
        if (teamOrder.Count == 0)
        {
            return await FinishAsync(BattleEncounterOutcome.Faulted, null, "Initiative produced no team order.");
        }

        await AddAsync(BattleEncounterEventKind.BattleStarted, "Battle started.");
        await AddAsync(
            BattleEncounterEventKind.InitiativeRolled,
            "Initiative order: " + string.Join(", ", teamOrder.Select(team => team.ToString())) + ".");
        await AddRangeAsync(await services.Lifecycle.ProcessBattleStartAsync(
            new BattleEncounterLifecycleRequest(request, request.Participants, teamOrder),
            cancellationToken));

        BattleEncounterCompletion initial = EvaluateCompletion(null);
        if (initial.IsComplete)
        {
            return await FinishAsync(initial.Outcome, initial.WinningTeamId, initial.Message);
        }

        for (int round = 1; round <= request.RoundLimit; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AddAsync(BattleEncounterEventKind.RoundStarted, $"Round {round} started.");

            foreach (ContentId teamId in teamOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                services.Synchronizer.Synchronize(request.Participants);
                BattleEncounterParticipant[] phaseActors = ActiveTeam(request.Participants, teamId);
                if (phaseActors.Length == 0)
                {
                    continue;
                }

                PressTurnEngine pressTurn = services.PressTurnFactory();
                pressTurn.StartPhase(phaseActors.Length);
                await AddAsync(
                    BattleEncounterEventKind.PhaseStarted,
                    $"Team {teamId} started a phase with {pressTurn.GetTotalIconCount()} icon(s).");

                int actorIndex = 0;
                while (pressTurn.HasTurnsRemaining())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    services.Synchronizer.Synchronize(request.Participants);
                    phaseActors = ActiveTeam(request.Participants, teamId);
                    if (phaseActors.Length == 0)
                    {
                        break;
                    }

                    BattleEncounterParticipant actor = phaseActors[actorIndex++ % phaseActors.Length];
                    await AddAsync(BattleEncounterEventKind.TurnStarted, $"{actor.DisplayName}'s turn started.", actor.InstanceId);

                    BattleTurnStartLifecycleResult turnStart = await services.Lifecycle.ProcessTurnStartAsync(
                        new BattleEncounterTurnLifecycleRequest(
                            request,
                            actor,
                            request.Participants,
                            CanReturnToStock(actor)),
                        cancellationToken);
                    await AddRangeAsync(MapStatusEvents(turnStart.Events));

                    if (turnStart.Outcome != BattleTurnStartOutcome.CanAct)
                    {
                        await AddAsync(
                            BattleEncounterEventKind.TurnRestricted,
                            $"{actor.DisplayName} turn restriction: {turnStart.Outcome}.",
                            actor.InstanceId);
                    }

                    BattleEncounterCommandResult command = await services.TurnHandler.ExecuteTurnAsync(
                        new BattleEncounterTurnRequest(
                            request,
                            actor,
                            request.Participants,
                            turnStart.Outcome,
                            pressTurn.FullIcons,
                            pressTurn.BlinkingIcons),
                        cancellationToken);

                    await AddRangeAsync(command.Events);
                    if (command.Status is BattleEncounterCommandStatus.Cancelled)
                    {
                        return await FinishAsync(BattleEncounterOutcome.Cancelled, null, null);
                    }

                    if (command.Status is BattleEncounterCommandStatus.Faulted)
                    {
                        await AddAsync(
                            BattleEncounterEventKind.BattleFaulted,
                            command.FaultMessage ?? "Battle command faulted.",
                            actor.InstanceId);
                        return await FinishAsync(BattleEncounterOutcome.Faulted, null, command.FaultMessage);
                    }

                    if (command.Status is BattleEncounterCommandStatus.Rejected)
                    {
                        string rejection = command.FaultMessage ?? "Battle command was rejected.";
                        await AddAsync(
                            BattleEncounterEventKind.ActionRejected,
                            rejection,
                            actor.InstanceId);
                        return await FinishAsync(BattleEncounterOutcome.Faulted, null, rejection);
                    }

                    ApplyTurnConsumption(pressTurn, command.TurnConsumption);
                    if (command.TurnConsumption.Kind != ActionTurnConsumptionKind.None)
                    {
                        await AddRangeAsync(await services.Lifecycle.ProcessTurnEndAsync(
                            new BattleEncounterTurnLifecycleRequest(
                                request,
                                actor,
                                request.Participants,
                                CanReturnToStock(actor)),
                            cancellationToken));
                    }

                    await AddPressTurnAsync(actor.InstanceId, pressTurn);

                    services.Synchronizer.Synchronize(request.Participants);
                    await AnnounceNewDefeatsAsync(request.Participants, defeatedAnnouncements, AddAsync);

                    if (command.RequestedOutcome is BattleEncounterOutcome requestedOutcome)
                    {
                        return await FinishAsync(requestedOutcome, command.WinningTeamId, command.FaultMessage);
                    }

                    BattleEncounterCompletion completion = EvaluateCompletion(actor);
                    if (completion.IsComplete)
                    {
                        return await FinishAsync(completion.Outcome, completion.WinningTeamId, completion.Message);
                    }
                }

                await AddRangeAsync(await services.Lifecycle.ProcessPhaseEndAsync(
                    new BattleEncounterLifecycleRequest(request, request.Participants, teamOrder),
                    teamId,
                    cancellationToken));
                await AddAsync(BattleEncounterEventKind.PhaseEnded, $"Team {teamId} phase ended.");
            }
        }

        return await FinishAsync(
            BattleEncounterOutcome.Draw,
            null,
            $"Battle ended in a draw after {request.RoundLimit} round(s).");

        BattleEncounterCompletion EvaluateCompletion(BattleEncounterParticipant? lastActor)
        {
            services.Synchronizer.Synchronize(request.Participants);
            return services.Completion.Evaluate(new BattleEncounterCompletionRequest(request.Participants, lastActor));
        }

        async ValueTask<BattleEncounterResult> FinishAsync(
            BattleEncounterOutcome outcome,
            ContentId? winningTeamId,
            string? message)
        {
            string endMessage = message ?? (outcome == BattleEncounterOutcome.Victory && winningTeamId is ContentId team
                ? $"Team {team} won."
                : outcome == BattleEncounterOutcome.Escape
                    ? "Battle escaped."
                    : outcome == BattleEncounterOutcome.Cancelled
                        ? "Battle cancelled."
                        : outcome == BattleEncounterOutcome.Faulted
                            ? "Battle faulted."
                            : "Battle ended.");
            await AddAsync(BattleEncounterEventKind.BattleEnded, endMessage, source: winningTeamId);
            await AddRangeAsync(await services.Lifecycle.ProcessBattleEndAsync(
                new BattleEncounterLifecycleRequest(request, request.Participants, teamOrder),
                outcome,
                cancellationToken));
            return new BattleEncounterResult(outcome, winningTeamId, request.Participants, events, message);
        }
    }

    private static BattleEncounterParticipant[] ActiveTeam(
        IEnumerable<BattleEncounterParticipant> participants,
        ContentId teamId) =>
        participants
            .Where(participant => participant.TeamId == teamId &&
                                  participant.State.IsActive &&
                                  !participant.State.IsDefeated)
            .ToArray();

    private static bool CanReturnToStock(BattleEncounterParticipant participant) =>
        participant.State.HasCapability(ContentId.Parse("return_to_stock"));

    private static void ApplyTurnConsumption(PressTurnEngine pressTurn, ActionTurnConsumption consumption)
    {
        switch (consumption.Kind)
        {
            case ActionTurnConsumptionKind.None:
                break;
            case ActionTurnConsumptionKind.Pass:
                pressTurn.Pass();
                break;
            case ActionTurnConsumptionKind.PressTurn when consumption.PressTurn is not null:
                pressTurn.ConsumeAction(consumption.PressTurn);
                break;
            case ActionTurnConsumptionKind.TerminatePhase:
                pressTurn.TerminatePhase();
                break;
            default:
                pressTurn.ConsumeAction(new PressTurnResolution(PressTurnOutcome.Normal, false, false));
                break;
        }
    }

    private static IEnumerable<BattleEncounterEvent> MapStatusEvents(
        IEnumerable<BattleStatusLifecycleEvent> events) =>
        events.Select(statusEvent => new BattleEncounterEvent(
            0,
            statusEvent.Kind is BattleStatusLifecycleEventKind.ResourceChanged
                ? BattleEncounterEventKind.ResourceChanged
                : BattleEncounterEventKind.StatusChanged,
            statusEvent.Detail ?? statusEvent.Kind.ToString(),
            statusEvent.ActorId,
            SourceId: statusEvent.RelatedId,
            Value: statusEvent.Value));

    private static async ValueTask AnnounceNewDefeatsAsync(
        IEnumerable<BattleEncounterParticipant> participants,
        HashSet<RuntimeInstanceId> announced,
        Func<BattleEncounterEventKind, string, RuntimeInstanceId?, RuntimeInstanceId?, ContentId?, decimal?, ValueTask> add)
    {
        foreach (BattleEncounterParticipant participant in participants.Where(participant =>
                     participant.State.IsDefeated && announced.Add(participant.InstanceId)))
        {
            await add(
                BattleEncounterEventKind.ActorDefeated,
                $"{participant.InstanceId} was defeated.",
                participant.InstanceId,
                null,
                null,
                null);
        }
    }
}
