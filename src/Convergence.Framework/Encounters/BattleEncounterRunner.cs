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
    DuplicateParticipantInstanceId,
    LifecycleExecutionFailed
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
    TurnEconomyChanged,
    DeploymentChanged,
    ActorDefeated,
    PhaseEnded,
    BattleFaulted,
    BattleEnded,
    HostActionRequested
}

public sealed record BattleEncounterEvent(
    int Sequence,
    BattleEncounterEventKind Kind,
    string Message,
    RuntimeInstanceId? ActorId = null,
    RuntimeInstanceId? TargetId = null,
    ContentId? SourceId = null,
    decimal? Value = null,
    BattleTurnEconomySnapshot? TurnEconomyState = null)
{
    public BattleEncounterFaultCode? FaultCode { get; internal init; }
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
    public ContentId TeamId => State.Ownership.TeamId;
    public bool IsActive => State.Deployment.IsActive;
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
        string? faultMessage = null,
        BattleEncounterFaultCode? faultCode = null)
    {
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
    bool CanRecallToRoster);

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
            turnEconomyState)
    {
    }

    public BattleEncounterTurnRequest(
        BattleEncounterRequest encounter,
        BattleEncounterParticipant actor,
        IReadOnlyList<BattleEncounterParticipant> participants,
        BattleTurnStartRestriction turnStartRestriction,
        BattleTurnEconomySnapshot turnEconomyState)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        TurnStartRestriction = turnStartRestriction ?? throw new ArgumentNullException(nameof(turnStartRestriction));
        TurnEconomyState = turnEconomyState ?? throw new ArgumentNullException(nameof(turnEconomyState));
    }

    public BattleEncounterRequest Encounter { get; }
    public BattleEncounterParticipant Actor { get; }
    public IReadOnlyList<BattleEncounterParticipant> Participants { get; }
    public BattleTurnStartRestriction TurnStartRestriction { get; }
    public BattleTurnStartOutcome TurnStartOutcome => TurnStartRestriction.Outcome;
    public IReadOnlyList<ContentId> AllowedActionIds => TurnStartRestriction.AllowedActionIds;
    public BattleTurnEconomySnapshot TurnEconomyState { get; }
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
        Func<IBattleTurnEconomy> turnEconomyFactory,
        BattlePhaseProgressPolicy phaseProgress,
        IBattleEncounterStateSynchronizer? synchronizer = null,
        IBattleEncounterEventSink? events = null)
    {
        Initiative = initiative ?? throw new ArgumentNullException(nameof(initiative));
        Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        TurnHandler = turnHandler ?? throw new ArgumentNullException(nameof(turnHandler));
        Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        TurnEconomyFactory = turnEconomyFactory ?? throw new ArgumentNullException(nameof(turnEconomyFactory));
        PhaseProgress = phaseProgress ?? throw new ArgumentNullException(nameof(phaseProgress));
        Synchronizer = synchronizer ?? NoopBattleEncounterStateSynchronizer.Instance;
        Events = events ?? NoopBattleEncounterEventSink.Instance;
    }

    public IBattleEncounterInitiativePolicy Initiative { get; }
    public IBattleEncounterLifecyclePort Lifecycle { get; }
    public IBattleEncounterTurnHandler TurnHandler { get; }
    public IBattleEncounterCompletionPolicy Completion { get; }
    public Func<IBattleTurnEconomy> TurnEconomyFactory { get; }
    public BattlePhaseProgressPolicy PhaseProgress { get; }
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
        cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            var battleEvent = new BattleEncounterEvent(++sequence, kind, message, actor, target, source, value);
            events.Add(battleEvent);
            await services.Events.PublishAsync(battleEvent, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask AddFaultAsync(
            string message,
            BattleEncounterFaultCode? faultCode,
            RuntimeInstanceId? actorId = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var battleEvent = new BattleEncounterEvent(
                ++sequence,
                BattleEncounterEventKind.BattleFaulted,
                message,
                actorId)
            {
                FaultCode = faultCode
            };
            events.Add(battleEvent);
            await services.Events.PublishAsync(battleEvent, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask AddTurnEconomyAsync(
            RuntimeInstanceId actor,
            BattleTurnEconomySnapshot state)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var battleEvent = new BattleEncounterEvent(
                ++sequence,
                BattleEncounterEventKind.TurnEconomyChanged,
                $"Turn economy {state.EconomyId}: {state.RemainingActions} action(s) remaining.",
                actor,
                TurnEconomyState: state);
            events.Add(battleEvent);
            await services.Events.PublishAsync(battleEvent, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask AddRangeAsync(IEnumerable<BattleEncounterEvent> unsequenced)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (BattleEncounterEvent battleEvent in unsequenced)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sequenced = battleEvent with { Sequence = ++sequence };
                events.Add(sequenced);
                await services.Events.PublishAsync(sequenced, cancellationToken).ConfigureAwait(false);
            }
        }

        RuntimeInstanceId[] duplicateParticipantIds = request.Participants
            .GroupBy(participant => participant.InstanceId)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToArray();
        if (duplicateParticipantIds.Length > 0)
        {
            string duplicates = string.Join(", ", duplicateParticipantIds.Select(id => id.ToString()));
            return await FailBeforeStartAsync(
                    $"Encounter participant runtime instance IDs must be unique. Duplicates: [{duplicates}].",
                    BattleEncounterFaultCode.DuplicateParticipantInstanceId)
                .ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ContentId>? proposedTeamOrder = services.Initiative.DetermineTeamOrder(
            new BattleEncounterInitiativeRequest(request.Participants));
        ContentId[] participatingTeams = request.Participants
            .Select(participant => participant.TeamId)
            .Distinct()
            .ToArray();
        if (!IsExactTeamPermutation(proposedTeamOrder, participatingTeams))
        {
            string expected = string.Join(", ", participatingTeams.Select(team => team.ToString()));
            string received = proposedTeamOrder is null
                ? "<null>"
                : string.Join(", ", proposedTeamOrder.Select(team => team.ToString()));
            return await FailBeforeStartAsync(
                    $"Initiative must return every participating team exactly once. Expected [{expected}]; received [{received}].")
                .ConfigureAwait(false);
        }

        IReadOnlyList<ContentId> teamOrder = Array.AsReadOnly(proposedTeamOrder!.ToArray());
        Synchronize();
        foreach (BattleEncounterParticipant participant in request.Participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            participant.State.Passives.ResetBattleActivations();
            await AddAsync(
                    BattleEncounterEventKind.ActorCreated,
                    $"Created {participant.DisplayName} as {participant.InstanceId} on {participant.TeamId}.",
                    participant.InstanceId)
                .ConfigureAwait(false);
        }

        await AddAsync(BattleEncounterEventKind.BattleStarted, "Battle started.").ConfigureAwait(false);
        await AddAsync(
                BattleEncounterEventKind.InitiativeRolled,
                "Initiative order: " + string.Join(", ", teamOrder.Select(team => team.ToString())) + ".")
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<BattleEncounterEvent> battleStartEvents;
        try
        {
            var lifecycleTransaction = new BattleEncounterLifecycleTransaction(request.Participants);
            IReadOnlyList<BattleEncounterEvent> returnedEvents = await services.Lifecycle.ProcessBattleStartAsync(
                    new BattleEncounterLifecycleRequest(
                        lifecycleTransaction.CreateEncounter(request),
                        lifecycleTransaction.Participants,
                        teamOrder),
                    cancellationToken)
                .ConfigureAwait(false);
            battleStartEvents = SnapshotLifecycleEvents(returnedEvents, "battle-start");
            lifecycleTransaction.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return await FaultDuringBattleAsync(
                    LifecycleFailureMessage("battle-start", exception),
                    faultCode: BattleEncounterFaultCode.LifecycleExecutionFailed)
                .ConfigureAwait(false);
        }

        await AddRangeAsync(battleStartEvents).ConfigureAwait(false);

        BattleEncounterCompletion initial = EvaluateCompletion(null);
        if (initial.IsComplete)
        {
            return await FinishAsync(initial.Outcome, initial.WinningTeamId, initial.Message).ConfigureAwait(false);
        }

        for (int round = 1; round <= request.RoundLimit; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AddAsync(BattleEncounterEventKind.RoundStarted, $"Round {round} started.").ConfigureAwait(false);

            foreach (ContentId teamId in teamOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Synchronize();
                BattleEncounterParticipant[] phaseActors = ActiveTeam(request.Participants, teamId);
                if (phaseActors.Length == 0)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                IBattleTurnEconomy turnEconomy = services.TurnEconomyFactory()
                    ?? throw new InvalidOperationException("The turn-economy factory returned null.");
                cancellationToken.ThrowIfCancellationRequested();
                turnEconomy.StartPhase(phaseActors.Length);
                BattleTurnEconomySnapshot phaseStartState = turnEconomy.CaptureSnapshot();
                await AddAsync(
                        BattleEncounterEventKind.PhaseStarted,
                        $"Team {teamId} started a phase using {phaseStartState.EconomyId} " +
                        $"with {phaseStartState.RemainingActions} action(s).")
                    .ConfigureAwait(false);

                int actorIndex = 0;
                int commandCount = 0;
                int consecutiveFreeActions = 0;
                while (turnEconomy.HasTurnsRemaining())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (commandCount >= services.PhaseProgress.MaximumCommands)
                    {
                        return await FaultDuringBattleAsync(
                                $"Team {teamId} exceeded the configured phase command limit " +
                                $"of {services.PhaseProgress.MaximumCommands}.")
                            .ConfigureAwait(false);
                    }

                    Synchronize();
                    phaseActors = ActiveTeam(request.Participants, teamId);
                    if (phaseActors.Length == 0)
                    {
                        break;
                    }

                    BattleEncounterParticipant actor = phaseActors[actorIndex++ % phaseActors.Length];
                    commandCount++;
                    await AddAsync(
                            BattleEncounterEventKind.TurnStarted,
                            $"{actor.DisplayName}'s turn started.",
                            actor.InstanceId)
                        .ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                    BattleTurnStartLifecycleResult turnStart;
                    try
                    {
                        var lifecycleTransaction = new BattleEncounterLifecycleTransaction(request.Participants);
                        BattleEncounterParticipant stagedActor = lifecycleTransaction.GetStaged(actor);
                        turnStart = await services.Lifecycle.ProcessTurnStartAsync(
                                new BattleEncounterTurnLifecycleRequest(
                                    lifecycleTransaction.CreateEncounter(request),
                                    stagedActor,
                                    lifecycleTransaction.Participants,
                                    CanRecallToRoster(stagedActor)),
                                cancellationToken)
                            .ConfigureAwait(false)
                            ?? throw new InvalidOperationException(
                                "The battle lifecycle returned a null turn-start result.");
                        lifecycleTransaction.Commit();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        return await FaultDuringBattleAsync(
                                LifecycleFailureMessage("turn-start", exception),
                                actor.InstanceId,
                                BattleEncounterFaultCode.LifecycleExecutionFailed)
                            .ConfigureAwait(false);
                    }

                    await AddRangeAsync(MapStatusEvents(turnStart.Events)).ConfigureAwait(false);

                    if (turnStart.Outcome != BattleTurnStartOutcome.CanAct)
                    {
                        await AddAsync(
                                BattleEncounterEventKind.TurnRestricted,
                                $"{actor.DisplayName} turn restriction: {turnStart.Outcome}.",
                                actor.InstanceId)
                            .ConfigureAwait(false);
                    }

                    BattleTurnEconomySnapshot beforeEconomy = turnEconomy.CaptureSnapshot();
                    cancellationToken.ThrowIfCancellationRequested();
                    BattleEncounterCommandResult command = await services.TurnHandler.ExecuteTurnAsync(
                            new BattleEncounterTurnRequest(
                                request,
                                actor,
                                request.Participants,
                                turnStart.Restriction,
                                beforeEconomy),
                            cancellationToken)
                        .ConfigureAwait(false);

                    await AddRangeAsync(command.Events).ConfigureAwait(false);
                    if (command.Status is BattleEncounterCommandStatus.Cancelled)
                    {
                        return await FinishAsync(BattleEncounterOutcome.Cancelled, null, null).ConfigureAwait(false);
                    }

                    if (command.Status is BattleEncounterCommandStatus.Faulted)
                    {
                        await AddAsync(
                                BattleEncounterEventKind.BattleFaulted,
                                command.FaultMessage ?? "Battle command faulted.",
                                actor.InstanceId)
                            .ConfigureAwait(false);
                        return await FinishAsync(BattleEncounterOutcome.Faulted, null, command.FaultMessage)
                            .ConfigureAwait(false);
                    }

                    if (command.Status is BattleEncounterCommandStatus.Rejected)
                    {
                        string rejection = command.FaultMessage ?? "Battle command was rejected.";
                        await AddAsync(
                                BattleEncounterEventKind.ActionRejected,
                                rejection,
                                actor.InstanceId)
                            .ConfigureAwait(false);
                        return await FinishAsync(BattleEncounterOutcome.Faulted, null, rejection).ConfigureAwait(false);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    turnEconomy.Apply(command.TurnConsumption);
                    BattleTurnEconomySnapshot afterEconomy = turnEconomy.CaptureSnapshot();
                    string? economyFault = ValidateEconomyTransition(
                        beforeEconomy,
                        afterEconomy,
                        turnEconomy.HasTurnsRemaining(),
                        command.TurnConsumption);
                    if (economyFault is not null)
                    {
                        return await FaultDuringBattleAsync(economyFault, actor.InstanceId).ConfigureAwait(false);
                    }

                    bool economyAdvanced = !Equals(beforeEconomy, afterEconomy);
                    if (!economyAdvanced && command.RequestedOutcome is null)
                    {
                        consecutiveFreeActions++;
                        if (consecutiveFreeActions > services.PhaseProgress.MaximumConsecutiveFreeActions)
                        {
                            return await FaultDuringBattleAsync(
                                    $"Team {teamId} exceeded the configured consecutive free-action limit " +
                                    $"of {services.PhaseProgress.MaximumConsecutiveFreeActions}.",
                                    actor.InstanceId)
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
                        try
                        {
                            var lifecycleTransaction = new BattleEncounterLifecycleTransaction(request.Participants);
                            BattleEncounterParticipant stagedActor = lifecycleTransaction.GetStaged(actor);
                            IReadOnlyList<BattleEncounterEvent> returnedEvents =
                                await services.Lifecycle.ProcessTurnEndAsync(
                                    new BattleEncounterTurnLifecycleRequest(
                                        lifecycleTransaction.CreateEncounter(request),
                                        stagedActor,
                                        lifecycleTransaction.Participants,
                                        CanRecallToRoster(stagedActor)),
                                    cancellationToken)
                                .ConfigureAwait(false);
                            turnEndEvents = SnapshotLifecycleEvents(returnedEvents, "turn-end");
                            lifecycleTransaction.Commit();
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            return await FaultDuringBattleAsync(
                                    LifecycleFailureMessage("turn-end", exception),
                                    actor.InstanceId,
                                    BattleEncounterFaultCode.LifecycleExecutionFailed)
                                .ConfigureAwait(false);
                        }

                        await AddRangeAsync(turnEndEvents).ConfigureAwait(false);
                    }

                    await AddTurnEconomyAsync(actor.InstanceId, afterEconomy).ConfigureAwait(false);

                    Synchronize();
                    await AnnounceNewDefeatsAsync(request.Participants, defeatedAnnouncements, AddAsync)
                        .ConfigureAwait(false);

                    if (command.RequestedOutcome is BattleEncounterOutcome requestedOutcome)
                    {
                        return await FinishAsync(requestedOutcome, command.WinningTeamId, command.FaultMessage)
                            .ConfigureAwait(false);
                    }

                    BattleEncounterCompletion completion = EvaluateCompletion(actor);
                    if (completion.IsComplete)
                    {
                        return await FinishAsync(completion.Outcome, completion.WinningTeamId, completion.Message)
                            .ConfigureAwait(false);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<BattleEncounterEvent> phaseEndEvents;
                try
                {
                    var lifecycleTransaction = new BattleEncounterLifecycleTransaction(request.Participants);
                    IReadOnlyList<BattleEncounterEvent> returnedEvents =
                        await services.Lifecycle.ProcessPhaseEndAsync(
                            new BattleEncounterLifecycleRequest(
                                lifecycleTransaction.CreateEncounter(request),
                                lifecycleTransaction.Participants,
                                teamOrder),
                            teamId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    phaseEndEvents = SnapshotLifecycleEvents(returnedEvents, "phase-end");
                    lifecycleTransaction.Commit();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    return await FaultDuringBattleAsync(
                            LifecycleFailureMessage("phase-end", exception),
                            faultCode: BattleEncounterFaultCode.LifecycleExecutionFailed)
                        .ConfigureAwait(false);
                }

                await AddRangeAsync(phaseEndEvents).ConfigureAwait(false);
                await AddAsync(BattleEncounterEventKind.PhaseEnded, $"Team {teamId} phase ended.")
                    .ConfigureAwait(false);
            }
        }

        return await FinishAsync(
                BattleEncounterOutcome.Draw,
                null,
                $"Battle ended in a draw after {request.RoundLimit} round(s).")
            .ConfigureAwait(false);

        BattleEncounterCompletion EvaluateCompletion(BattleEncounterParticipant? lastActor)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Synchronize();
            cancellationToken.ThrowIfCancellationRequested();
            return services.Completion.Evaluate(new BattleEncounterCompletionRequest(request.Participants, lastActor));
        }

        void Synchronize()
        {
            cancellationToken.ThrowIfCancellationRequested();
            services.Synchronizer.Synchronize(request.Participants);
        }

        async ValueTask<BattleEncounterResult> FailBeforeStartAsync(
            string message,
            BattleEncounterFaultCode? faultCode = null)
        {
            await AddFaultAsync(message, faultCode).ConfigureAwait(false);
            await AddAsync(BattleEncounterEventKind.BattleEnded, "Battle faulted.").ConfigureAwait(false);
            return new BattleEncounterResult(
                BattleEncounterOutcome.Faulted,
                null,
                request.Participants,
                events,
                message,
                faultCode);
        }

        async ValueTask<BattleEncounterResult> FaultDuringBattleAsync(
            string message,
            RuntimeInstanceId? actorId = null,
            BattleEncounterFaultCode? faultCode = null)
        {
            await AddFaultAsync(message, faultCode, actorId).ConfigureAwait(false);
            return await FinishAsync(BattleEncounterOutcome.Faulted, null, message, faultCode)
                .ConfigureAwait(false);
        }

        async ValueTask<BattleEncounterResult> FinishAsync(
            BattleEncounterOutcome outcome,
            ContentId? winningTeamId,
            string? message,
            BattleEncounterFaultCode? faultCode = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<BattleEncounterEvent> battleEndEvents;
            try
            {
                var lifecycleTransaction = new BattleEncounterLifecycleTransaction(request.Participants);
                IReadOnlyList<BattleEncounterEvent> returnedEvents =
                    await services.Lifecycle.ProcessBattleEndAsync(
                        new BattleEncounterLifecycleRequest(
                            lifecycleTransaction.CreateEncounter(request),
                            lifecycleTransaction.Participants,
                            teamOrder),
                        outcome,
                        cancellationToken)
                    .ConfigureAwait(false);
                battleEndEvents = SnapshotLifecycleEvents(returnedEvents, "battle-end");
                lifecycleTransaction.Commit();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                outcome = BattleEncounterOutcome.Faulted;
                winningTeamId = null;
                message = LifecycleFailureMessage("battle-end", exception);
                faultCode = BattleEncounterFaultCode.LifecycleExecutionFailed;
                battleEndEvents = [];
                await AddFaultAsync(message, faultCode).ConfigureAwait(false);
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
            await AddAsync(BattleEncounterEventKind.BattleEnded, endMessage, source: winningTeamId)
                .ConfigureAwait(false);
            await AddRangeAsync(battleEndEvents).ConfigureAwait(false);
            return new BattleEncounterResult(
                outcome,
                winningTeamId,
                request.Participants,
                events,
                message,
                faultCode);
        }

        static string LifecycleFailureMessage(string stage, Exception exception) =>
            $"Battle lifecycle step '{stage}' failed: {exception.Message}";

        static IReadOnlyList<BattleEncounterEvent> SnapshotLifecycleEvents(
            IReadOnlyList<BattleEncounterEvent>? lifecycleEvents,
            string stage) =>
            Array.AsReadOnly((lifecycleEvents ?? throw new InvalidOperationException(
                    $"The battle lifecycle returned a null {stage} event collection."))
                .ToArray());
    }

    private static BattleEncounterParticipant[] ActiveTeam(
        IEnumerable<BattleEncounterParticipant> participants,
        ContentId teamId) =>
        participants
            .Where(participant => participant.TeamId == teamId &&
                                  participant.State.IsActive &&
                                  !participant.State.IsDefeated)
            .ToArray();

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

        if (hasTurnsRemaining != (after.RemainingActions > 0))
        {
            return $"Turn economy {after.EconomyId} reported inconsistent remaining-action state.";
        }

        if (consumption.Kind != ActionTurnConsumptionKind.None && Equals(before, after))
        {
            return $"Turn economy {after.EconomyId} did not advance for {consumption.Kind} consumption.";
        }

        return null;
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
                    null)
                .ConfigureAwait(false);
        }
    }
}
