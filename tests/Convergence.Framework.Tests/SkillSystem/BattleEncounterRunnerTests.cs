using Convergence.Content;
using Convergence.Battle;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class BattleEncounterRunnerTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId Kind = Id("normal_battle");
    private static readonly ContentId Moon = Id("new_moon");
    private static readonly ContentId Hp = Id("hp");
    private static readonly ContentId Sp = Id("sp");
    private static readonly ContentId PlayerTeam = Id("player_team");
    private static readonly ContentId EnemyTeam = Id("enemy_team");
    private static readonly ContentId OwnerTurnEnd = Id("owner_turn_end");
    private static readonly ContentId PhaseEnd = Id("phase_end");

    public static TheoryData<BattleEncounterEventKind> RunnerOwnedStructuralEventKinds { get; } = new()
    {
        BattleEncounterEventKind.ActorCreated,
        BattleEncounterEventKind.BattleStarted,
        BattleEncounterEventKind.InitiativeRolled,
        BattleEncounterEventKind.RoundStarted,
        BattleEncounterEventKind.PhaseStarted,
        BattleEncounterEventKind.TurnStarted,
        BattleEncounterEventKind.TurnRestricted,
        BattleEncounterEventKind.TurnEconomyChanged,
        BattleEncounterEventKind.TurnEnded,
        BattleEncounterEventKind.ActorDefeated,
        BattleEncounterEventKind.PhaseEnded,
        BattleEncounterEventKind.RoundEnded,
        BattleEncounterEventKind.BattleFaulted,
        BattleEncounterEventKind.BattleEnded
    };

    [Fact]
    public void Runner_UsesInitiativeAndOrdersLifecycleEvents()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(EnemyTeam, PlayerTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal([EnemyTeam, PlayerTeam], lifecycle.BattleStartTeamOrder);
        Assert.True(Index(result, BattleEncounterEventKind.BattleStarted) <
                    Index(result, BattleEncounterEventKind.RoundStarted));
        Assert.True(Index(result, BattleEncounterEventKind.RoundStarted) <
                    Index(result, BattleEncounterEventKind.PhaseStarted));
        Assert.Equal(enemy.InstanceId, handler.Requests.Single().Actor.InstanceId);
    }

    [Fact]
    public void Runner_UsesTheInjectedSchedulerAsActorOrderAuthority()
    {
        BattleEncounterParticipant player = Participant("scheduled_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("scheduled_enemy", EnemyTeam);
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(1),
            schedule: new EnemyFirstSchedulePolicy(enemy.InstanceId));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal(enemy.InstanceId, Assert.Single(handler.Requests).Actor.InstanceId);
    }

    [Fact]
    public void Runner_ExecutesCrossTeamAgilityOrderFromTheInjectedScheduler()
    {
        BattleEncounterParticipant player = ParticipantWithAgility(
            "agility_player",
            PlayerTeam,
            agility: 7m);
        BattleEncounterParticipant ally = ParticipantWithAgility(
            "agility_ally",
            PlayerTeam,
            agility: 3m);
        BattleEncounterParticipant enemy = ParticipantWithAgility(
            "agility_enemy",
            EnemyTeam,
            agility: 11m);
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, ally, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(3),
            schedule: new AgilityOrderedBattleEncounterSchedulePolicy(
                Id("agility"),
                new EncounterOrderBattleEncounterScheduleTieBreakPolicy()));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal(
            [enemy.InstanceId, player.InstanceId, ally.InstanceId],
            handler.Requests.Select(request => request.Actor.InstanceId));
    }

    [Fact]
    public void Runner_ConvertsSchedulerExceptionsAndRejectedStartsToTypedFaults()
    {
        BattleEncounterParticipant player = Participant("schedule_fault_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("schedule_fault_enemy", EnemyTeam);

        BattleEncounterResult exception = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ =>
                BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(99),
            schedule: new ThrowingSchedulePolicy());
        BattleEncounterResult rejected = Run(
            [Participant("schedule_reject_player", PlayerTeam),
             Participant("schedule_reject_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ =>
                BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(99),
            schedule: new RejectingSchedulePolicy());

        Assert.Equal(BattleEncounterOutcome.Faulted, exception.Outcome);
        Assert.Equal(BattleEncounterFaultCode.ScheduleExecutionFailed, exception.FaultCode);
        Assert.Equal(BattleEncounterOutcome.Faulted, rejected.Outcome);
        Assert.Equal(BattleEncounterFaultCode.ScheduleTransitionInvalid, rejected.FaultCode);
    }

    [Fact]
    public void Runner_DispatchesOneRoundClockAfterEveryTeamPhaseNotAfterEveryAction()
    {
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [Participant("round_player", PlayerTeam), Participant("round_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal(10, lifecycle.PhaseEndCalls);
        Assert.Equal(5, lifecycle.RoundEndCalls);
        Assert.Equal(10, handler.Requests.Count);
    }

    [Fact]
    public void Runner_PublishesImmutableTypedEncounterPayloadsWithoutRequiringDebugText()
    {
        BattleEncounterParticipant player = Participant("typed_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("typed_enemy", EnemyTeam);

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Pass)),
            new CompleteAfterTurnsPolicy(1));

        var started = Assert.IsType<BattleStartedEventPayload>(
            result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.BattleStarted).Payload);
        Assert.Equal(Battle, started.ContextId);
        Assert.Equal(Kind, started.BattleKindId);
        Assert.Equal(Moon, started.MoonPhaseId);
        Assert.Equal([player.InstanceId, enemy.InstanceId], started.ActorIds);
        Assert.Equal([PlayerTeam, EnemyTeam], started.TeamIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeInstanceId>)started.ActorIds).Add(RuntimeInstanceId.Parse("late_actor")));

        var initiative = Assert.IsType<BattleInitiativeRolledEventPayload>(
            result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.InitiativeRolled).Payload);
        Assert.Equal([PlayerTeam, EnemyTeam], initiative.TeamOrder);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContentId>)initiative.TeamOrder).Add(Id("late_team")));

        Assert.Equal(
            1,
            Assert.IsType<BattleRoundStartedEventPayload>(
                result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.RoundStarted).Payload)
                .RoundNumber);
        Assert.Equal(
            PlayerTeam,
            Assert.IsType<BattlePhaseStartedEventPayload>(
                result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.PhaseStarted).Payload)
                .TeamId);
        Assert.Equal(
            player.InstanceId,
            Assert.IsType<BattleTurnStartedEventPayload>(
                result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnStarted).Payload)
                .ActorId);

        var economy = Assert.IsType<BattleTurnEconomyChangedEventPayload>(
            result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged).Payload);
        Assert.Equal(1, economy.Before.RemainingActions);
        Assert.Equal(0, economy.After.RemainingActions);
        Assert.Equal(ActionTurnConsumptionKind.Pass, economy.Consumption.Kind);

        var turnEnded = Assert.IsType<BattleTurnEndedEventPayload>(
            result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnEnded).Payload);
        Assert.Equal(player.InstanceId, turnEnded.ActorId);
        Assert.Equal(PlayerTeam, turnEnded.TeamId);
        Assert.Equal(BattleEncounterTurnEndReason.CommandCommitted, turnEnded.Reason);
        Assert.Equal(ActionTurnConsumptionKind.Pass, turnEnded.TurnConsumption?.Kind);
        Assert.Equal(0, turnEnded.TurnEconomyState.RemainingActions);
        Assert.True(
            result.Events.Single(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged).Sequence <
            result.Events.Single(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.TurnEnded).Sequence);

        var ended = Assert.IsType<BattleEndedEventPayload>(
            result.Events.Single(battleEvent => battleEvent.Kind == BattleEncounterEventKind.BattleEnded).Payload);
        Assert.Equal(result.Outcome, ended.Outcome);
        Assert.Equal(1, ended.FinalRoundNumber);
        Assert.Equal(0, ended.CompletedRounds);

        var eventWithoutDebugText = new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.CommandPassed,
            new BattleCommandPassedEventPayload(player.InstanceId));
        Assert.Null(eventWithoutDebugText.DebugText);
        Assert.Throws<ArgumentException>(() => new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.CommandPassed,
            new BattleTurnStartedEventPayload(player.InstanceId, PlayerTeam)));
    }

    [Fact]
    public void Runner_ReportsFinalRoundSeparatelyFromFullyCompletedRounds()
    {
        BattleEncounterParticipant player = Participant("round_count_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("round_count_enemy", EnemyTeam);

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ =>
                BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(3));

        BattleEndedEventPayload ended = Assert.IsType<BattleEndedEventPayload>(
            result.Events.Single(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.BattleEnded).Payload);
        BattleRoundEndedEventPayload completedRound = Assert.IsType<BattleRoundEndedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.RoundEnded).Payload);

        Assert.Equal(2, ended.FinalRoundNumber);
        Assert.Equal(1, ended.CompletedRounds);
        Assert.Equal(1, completedRound.RoundNumber);
        Assert.True(
            result.Events.Single(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.RoundEnded).Sequence <
            result.Events.Last(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.RoundStarted).Sequence);
    }

    [Fact]
    public void EncounterEndPayloads_RejectContradictoryStructuralState()
    {
        var economy = new StandardActionTurnEconomySnapshot(1);
        RuntimeInstanceId actorId = RuntimeInstanceId.Parse("event_actor");

        Assert.Throws<ArgumentException>(() => new BattleTurnEndedEventPayload(
            actorId,
            PlayerTeam,
            BattleEncounterTurnEndReason.CommandCommitted,
            economy));
        Assert.Throws<ArgumentException>(() => new BattleTurnEndedEventPayload(
            actorId,
            PlayerTeam,
            BattleEncounterTurnEndReason.ActorUnavailable,
            economy,
            ActionTurnConsumption.Normal));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleRoundEndedEventPayload(0));
        Assert.Throws<ArgumentException>(() => new BattleEndedEventPayload(
            BattleEncounterOutcome.Draw,
            null,
            finalRoundNumber: null,
            completedRounds: 1));
        Assert.Throws<ArgumentException>(() => new BattleEndedEventPayload(
            BattleEncounterOutcome.Draw,
            null,
            finalRoundNumber: 1,
            completedRounds: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleEndedEventPayload(
            (BattleEncounterOutcome)int.MaxValue,
            null,
            completedRounds: 0));
        Assert.Throws<ArgumentException>(() => new BattleEndedEventPayload(
            BattleEncounterOutcome.Victory,
            null,
            completedRounds: 0));
        Assert.Throws<ArgumentException>(() => new BattleEndedEventPayload(
            BattleEncounterOutcome.Draw,
            PlayerTeam,
            completedRounds: 0));
        Assert.Throws<ArgumentException>(() => new BattleEndedEventPayload(
            BattleEncounterOutcome.Faulted,
            null,
            completedRounds: 0));
        Assert.Throws<ArgumentException>(() => new BattleEndedEventPayload(
            BattleEncounterOutcome.Draw,
            null,
            completedRounds: 0,
            faultCode: BattleEncounterFaultCode.CommandRejected));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleEndedEventPayload(
            BattleEncounterOutcome.Faulted,
            null,
            completedRounds: 0,
            faultCode: (BattleEncounterFaultCode)int.MaxValue));
    }

    [Fact]
    public void Runner_ResultCapturesImmutableFinalParticipantSnapshots()
    {
        BattleEncounterParticipant player = Participant("snapshot_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("snapshot_enemy", EnemyTeam);
        var handler = new QueueTurnHandler(_ =>
        {
            player.State.SetResource(Hp, 4);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal([player.InstanceId, enemy.InstanceId], result.Participants.Select(item => item.InstanceId));
        BattleEncounterParticipantSnapshot playerResult = result.Participants[0];
        Assert.Equal("snapshot_player", playerResult.DisplayName);
        Assert.Equal(4, playerResult.State.Resources.Single(resource => resource.ResourceId == Hp).Current);

        player.State.SetResource(Hp, 1);

        Assert.Equal(4, playerResult.State.Resources.Single(resource => resource.ResourceId == Hp).Current);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleEncounterParticipantSnapshot>)result.Participants).Add(playerResult));
    }

    [Fact]
    public void DepartureLifecycleRequest_RequiresAndSnapshotsOneConsistentParticipantGraph()
    {
        BattleEncounterParticipant player = Participant("departure_request_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("departure_request_enemy", EnemyTeam);
        var participants = new List<BattleEncounterParticipant> { player, enemy };
        var encounter = new BattleEncounterRequest(participants, Battle, Kind, Moon, 1);
        var request = new BattleEncounterDepartureLifecycleRequest(
            encounter,
            player,
            participants,
            BattleStatusDepartureReason.Flee);

        participants.Clear();

        Assert.Equal([player, enemy], request.Participants);
        Assert.Equal(BattleStatusDepartureReason.Flee, request.Reason);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleEncounterParticipant>)request.Participants).Add(player));
        Assert.Throws<ArgumentException>(() => new BattleEncounterDepartureLifecycleRequest(
            encounter,
            Participant("departure_request_outsider", PlayerTeam),
            encounter.Participants,
            BattleStatusDepartureReason.Flee));
        Assert.Throws<ArgumentException>(() => new BattleEncounterDepartureLifecycleRequest(
            encounter,
            player,
            [player],
            BattleStatusDepartureReason.Flee));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleEncounterDepartureLifecycleRequest(
            encounter,
            player,
            encounter.Participants,
            (BattleStatusDepartureReason)int.MaxValue));
    }

    [Fact]
    public async Task Runner_RejectsDuplicateParticipantInstanceIdsBeforeEncounterPortsOrMutation()
    {
        BattleEncounterParticipant firstAlpha = Participant("duplicate_alpha", PlayerTeam);
        BattleEncounterParticipant firstBeta = Participant("duplicate_beta", EnemyTeam);
        BattleEncounterParticipant secondAlpha = Participant("duplicate_alpha", EnemyTeam);
        BattleEncounterParticipant secondBeta = Participant("duplicate_beta", PlayerTeam);
        BattleEncounterParticipant[] participants =
        [
            firstAlpha,
            firstBeta,
            secondAlpha,
            secondBeta
        ];
        var initiative = new CountingInitiative(PlayerTeam, EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
        var synchronizer = new RecordingSynchronizer();
        var eventSink = new RecordingEventSink();
        int economyCreations = 0;

        BattleEncounterResult result = await new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest(participants, Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                initiative,
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                handler,
                new CompleteAfterTurnsPolicy(1),
                () =>
                {
                    economyCreations++;
                    return new StandardActionTurnEconomy();
                },
                new BattlePhaseProgressPolicy(8, 1),
                synchronizer,
                eventSink));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.DuplicateParticipantInstanceId, result.FaultCode);
        Assert.Equal(
            "Encounter participant runtime instance IDs must be unique. " +
            "Duplicates: [duplicate_alpha, duplicate_beta].",
            result.FaultMessage);
        Assert.Equal(
            participants.Select(participant => participant.InstanceId),
            result.Participants.Select(participant => participant.InstanceId));
        Assert.Equal(
            [BattleEncounterEventKind.BattleFaulted, BattleEncounterEventKind.BattleEnded],
            result.Events.Select(battleEvent => battleEvent.Kind));
        BattleEncounterEvent fault = result.Events[0];
        Assert.Equal(BattleEncounterFaultCode.DuplicateParticipantInstanceId, fault.FaultCode);
        Assert.Equal(BattleEncounterFaultCode.DuplicateParticipantInstanceId, result.Events[1].FaultCode);
        Assert.Equal(result.Events, eventSink.Events);

        Assert.Equal(0, initiative.Calls);
        Assert.Equal(0, economyCreations);
        Assert.Equal(0, synchronizer.Calls);
        Assert.Equal(0, lifecycle.BattleStartCalls);
        Assert.Equal(0, lifecycle.TurnStartCalls);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.Equal(0, lifecycle.BattleEndCalls);
        Assert.Empty(handler.Requests);
        Assert.All(participants, participant =>
            Assert.Equal(10, participant.State.GetRequiredResource(Hp).Current));
    }

    [Fact]
    public void Runner_SynchronousCompatibilityWrapperDoesNotDeadlockSingleThreadedContext()
    {
        BattleEncounterParticipant player = Participant("sync_context_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("sync_context_enemy", EnemyTeam);
        var initiative = new CountingInitiative(PlayerTeam, EnemyTeam);
        var ports = new AsynchronousEncounterPorts();
        var context = new NonPumpingSynchronizationContext();
        BattleEncounterResult? result = null;
        Exception? failure = null;
        bool contextWasRestored = false;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                result = new BattleEncounterRunner().Run(
                    new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 1),
                    new BattleEncounterServices(
                        initiative,
                        new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                        ports,
                        ports,
                        new CompleteAfterTurnsPolicy(1),
                        () => new StandardActionTurnEconomy(),
                        new BattlePhaseProgressPolicy(8, 1),
                        events: ports));
                contextWasRestored = ReferenceEquals(SynchronizationContext.Current, context);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true
        };

        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The synchronous encounter wrapper deadlocked.");
        Assert.Null(failure);
        Assert.NotNull(result);
        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal(1, initiative.Calls);
        Assert.Equal(0, context.PostCount);
        Assert.True(contextWasRestored);
        Assert.True(ports.PublishCalls > 0);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(10, enemy.State.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task Runner_AsyncPathDoesNotCaptureCallerSynchronizationContext()
    {
        BattleEncounterParticipant player = Participant("async_context_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("async_context_enemy", EnemyTeam);
        var ports = new AsynchronousEncounterPorts();
        var context = new NonPumpingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task<BattleEncounterResult> run;

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            run = new BattleEncounterRunner().RunAsync(
                new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 1),
                new BattleEncounterServices(
                    new FixedInitiative(PlayerTeam, EnemyTeam),
                    new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                    ports,
                    ports,
                    new CompleteAfterTurnsPolicy(1),
                    () => new StandardActionTurnEconomy(),
                    new BattlePhaseProgressPolicy(8, 1),
                    events: ports)).AsTask();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        BattleEncounterResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal(0, context.PostCount);
        Assert.True(ports.PublishCalls > 0);
        Assert.Equal(1, ports.BattleStartCalls);
        Assert.Equal(1, ports.TurnStartCalls);
        Assert.Equal(1, ports.TurnHandlerCalls);
        Assert.Equal(1, ports.TurnEndCalls);
        Assert.Equal(1, ports.BattleEndCalls);
    }

    [Fact]
    public void Runner_ResultCapturesStateAfterBattleEndLifecycle()
    {
        BattleEncounterParticipant player = Participant("cleanup_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            BattleEndAction = request => request.Participants[0].State.SetResource(Hp, 7)
        };

        BattleEncounterResult result = Run(
            [player, Participant("cleanup_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(7, result.Participants[0].State.Resources.Single(resource => resource.ResourceId == Hp).Current);
    }

    [Theory]
    [InlineData(ThrowingLifecycleStage.BattleStart, 0, null)]
    [InlineData(ThrowingLifecycleStage.TurnStart, 0, "lifecycle_player")]
    [InlineData(ThrowingLifecycleStage.TurnEnd, 1, "lifecycle_player")]
    [InlineData(ThrowingLifecycleStage.PhaseEnd, 1, null)]
    [InlineData(ThrowingLifecycleStage.RoundEnd, 2, null)]
    [InlineData(ThrowingLifecycleStage.BattleEnd, 1, null)]
    public void Runner_ConvertsLifecycleExceptionsToTypedFaultsAndRollsBackTheStep(
        ThrowingLifecycleStage stage,
        int expectedCommandCount,
        string? expectedActorId)
    {
        BattleEncounterParticipant player = Participant("lifecycle_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("lifecycle_enemy", EnemyTeam);
        var lifecycle = new MutatingThrowingLifecycle(stage);
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
        IBattleEncounterCompletionPolicy completion = stage == ThrowingLifecycleStage.BattleEnd
            ? new CompleteAfterTurnsPolicy(1)
            : new CompleteAfterTurnsPolicy(99);

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            completion);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.LifecycleExecutionFailed, result.FaultCode);
        Assert.Contains(DiagnosticName(stage), result.FaultMessage, StringComparison.Ordinal);
        Assert.Equal(expectedCommandCount, handler.Requests.Count);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(10, enemy.State.GetRequiredResource(Hp).Current);
        Assert.All(result.Participants, participant =>
            Assert.Equal(10, participant.State.Resources.Single(resource => resource.ResourceId == Hp).Current));
        BattleEncounterEvent fault = Assert.Single(
            result.Events,
            battleEvent => battleEvent.Kind == BattleEncounterEventKind.BattleFaulted);
        Assert.Equal(BattleEncounterFaultCode.LifecycleExecutionFailed, fault.FaultCode);
        Assert.Equal(
            expectedActorId is null ? null : RuntimeInstanceId.Parse(expectedActorId),
            fault.ActorId);
    }

    [Fact]
    public void Runner_ContainsDepartureLifecycleFailureAndRollsBackOnlyThatLifecycleStep()
    {
        BattleEncounterParticipant player = Participant("departure_fault_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("departure_fault_enemy", EnemyTeam);
        var lifecycle = new MutatingThrowingDepartureLifecycle(player.InstanceId);
        var handler = new QueueTurnHandler(request =>
        {
            request.Actor.State.SetEncounterPresence(isDeployed: false);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99));

        AssertPortFault(
            result,
            BattleEncounterFaultCode.LifecycleExecutionFailed,
            "actor-departure-lifecycle");
        Assert.Equal(BattleStatusDepartureReason.Flee, lifecycle.DepartureReason);
        Assert.False(player.State.IsDeployed);
        Assert.Equal(10m, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(
            10m,
            result.Participants
                .Single(participant => participant.InstanceId == player.InstanceId)
                .State.Resources.Single(resource => resource.ResourceId == Hp)
                .Current);
    }

    [Theory]
    [InlineData(BoundarySourceFailure.Throw)]
    [InlineData(BoundarySourceFailure.NullResult)]
    [InlineData(BoundarySourceFailure.InvalidBoundary)]
    [InlineData(BoundarySourceFailure.DuplicateEvent)]
    public void Runner_ContainsMalformedStatModifierBoundarySourcesAsLifecycleFaults(
        BoundarySourceFailure failure)
    {
        var lifecycle = new BoundarySourceLifecycle(failure);
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [Participant("boundary_fault_player", PlayerTeam), Participant("boundary_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99));

        AssertPortFault(
            result,
            BattleEncounterFaultCode.LifecycleExecutionFailed,
            "stat-modifier-boundary-source");
        Assert.Empty(handler.Requests);
        Assert.Equal(1, lifecycle.BattleEndCalls);
    }

    [Fact]
    public void Runner_DefensivelyCopiesStatModifierBoundariesBeforeCallingTheTurnHandler()
    {
        var mutableBoundaries = new List<StatModifierLifecycleBoundary>
        {
            new(OwnerTurnEnd, 1)
        };
        var lifecycle = new BoundarySourceLifecycle(_ => mutableBoundaries);
        BattleEncounterTurnRequest? captured = null;
        var handler = new QueueTurnHandler(request =>
        {
            mutableBoundaries.Add(new StatModifierLifecycleBoundary(PhaseEnd, 1));
            captured = request;
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [Participant("boundary_copy_player", PlayerTeam), Participant("boundary_copy_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        BattleEncounterTurnRequest request = Assert.IsType<BattleEncounterTurnRequest>(captured);
        StatModifierLifecycleBoundary boundary = Assert.Single(request.ActiveStatModifierBoundaries);
        Assert.Equal(OwnerTurnEnd, boundary.EventId);
        Assert.Equal(1, boundary.Sequence);
        Assert.Equal(2, mutableBoundaries.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StatModifierLifecycleBoundary>)request.ActiveStatModifierBoundaries)
            .Add(new StatModifierLifecycleBoundary(PhaseEnd, 2)));
    }

    [Fact]
    public void Runner_ContainsInitiativeExceptionsBeforeBattleStart()
    {
        var lifecycle = new RecordingLifecycle();

        BattleEncounterResult result = Run(
            [Participant("initiative_fault_player", PlayerTeam), Participant("initiative_fault_enemy", EnemyTeam)],
            new ThrowingInitiative(),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(1));

        AssertPortFault(result, BattleEncounterFaultCode.InitiativeExecutionFailed, "initiative");
        Assert.Equal(0, lifecycle.BattleStartCalls);
        Assert.Equal(0, lifecycle.BattleEndCalls);
        Assert.Equal(
            [BattleEncounterEventKind.BattleFaulted, BattleEncounterEventKind.BattleEnded],
            result.Events.Select(battleEvent => battleEvent.Kind));
    }

    [Fact]
    public void Runner_ContainsStateSynchronizationExceptionsAndRunsBattleEndOnce()
    {
        BattleEncounterParticipant player = Participant("synchronizer_fault_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            BattleEndAction = request => request.Participants[0].State.SetResource(Hp, 7)
        };
        var synchronizer = new ThrowingSynchronizer(throwOnCall: 2);

        BattleEncounterResult result = Run(
            [player, Participant("synchronizer_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(1),
            synchronizer: synchronizer);

        AssertPortFault(result, BattleEncounterFaultCode.StateSynchronizationFailed, "state-synchronization");
        Assert.Equal(2, synchronizer.Calls);
        Assert.Equal(1, lifecycle.BattleStartCalls);
        Assert.Equal(1, lifecycle.BattleEndCalls);
        Assert.Equal(7, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(7, result.Participants[0].State.Resources.Single(resource => resource.ResourceId == Hp).Current);
    }

    [Theory]
    [InlineData(ThrowingTurnEconomyStage.Factory, 0)]
    [InlineData(ThrowingTurnEconomyStage.NullFactory, 0)]
    [InlineData(ThrowingTurnEconomyStage.StartPhase, 0)]
    [InlineData(ThrowingTurnEconomyStage.CaptureSnapshot, 0)]
    [InlineData(ThrowingTurnEconomyStage.NullSnapshot, 0)]
    [InlineData(ThrowingTurnEconomyStage.HasTurnsRemaining, 0)]
    [InlineData(ThrowingTurnEconomyStage.Apply, 1)]
    public void Runner_ContainsEveryTurnEconomyPortException(
        ThrowingTurnEconomyStage stage,
        int expectedTurnHandlerCalls)
    {
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
        Func<IBattleTurnEconomy> factory = stage switch
        {
            ThrowingTurnEconomyStage.Factory =>
                () => throw new InvalidOperationException("Deliberate turn-economy-factory failure."),
            ThrowingTurnEconomyStage.NullFactory => () => null!,
            _ => () => new ThrowingTurnEconomy(stage)
        };

        BattleEncounterResult result = Run(
            [Participant("economy_fault_player", PlayerTeam), Participant("economy_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99),
            factory);

        AssertPortFault(result, BattleEncounterFaultCode.TurnEconomyExecutionFailed, "turn-economy-");
        Assert.Equal(expectedTurnHandlerCalls, handler.Requests.Count);
        Assert.Equal(1, lifecycle.BattleEndCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Runner_ContainsTurnHandlerExceptionsIncludingUnsignalledCancellation(bool cancellationShaped)
    {
        BattleEncounterParticipant player = Participant("handler_fault_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle();
        Exception failure = cancellationShaped
            ? new OperationCanceledException("Cancellation without the supplied token.")
            : new InvalidOperationException("Deliberate turn-handler failure.");
        var handler = new ThrowingTurnHandler(failure);

        BattleEncounterResult result = Run(
            [player, Participant("handler_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99));

        AssertPortFault(result, BattleEncounterFaultCode.TurnHandlerExecutionFailed, "turn-handler");
        Assert.Equal(player.InstanceId, result.Events.Single(battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.BattleFaulted).ActorId);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.Equal(1, lifecycle.BattleEndCalls);
    }

    [Fact]
    public void Runner_ContainsMalformedNullTurnHandlerResult()
    {
        var lifecycle = new RecordingLifecycle();

        BattleEncounterResult result = Run(
            [Participant("null_handler_player", PlayerTeam), Participant("null_handler_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new NullTurnHandler(),
            new CompleteAfterTurnsPolicy(99));

        AssertPortFault(result, BattleEncounterFaultCode.TurnHandlerExecutionFailed, "turn-handler");
        Assert.Equal(1, lifecycle.BattleEndCalls);
    }

    [Fact]
    public void Runner_ContainsMalformedTurnConsumptionBeforeEconomyOrTurnEndMutation()
    {
        BattleEncounterParticipant player = Participant("malformed_consumption_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(
                new ActionTurnConsumption(ActionTurnConsumptionKind.TurnEconomy)));

        BattleEncounterResult result = Run(
            [player, Participant("malformed_consumption_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99));

        AssertPortFault(result, BattleEncounterFaultCode.TurnHandlerExecutionFailed, "turn-handler");
        Assert.Single(handler.Requests);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.Equal(1, lifecycle.BattleEndCalls);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Runner_ContainsThrowingAndMalformedCompletionPolicies(bool returnNull)
    {
        var lifecycle = new RecordingLifecycle();
        var completion = new FailingCompletionPolicy(returnNull);

        BattleEncounterResult result = Run(
            [Participant("completion_fault_player", PlayerTeam), Participant("completion_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            completion);

        AssertPortFault(result, BattleEncounterFaultCode.CompletionEvaluationFailed, "completion-evaluation");
        Assert.Equal(1, completion.Calls);
        Assert.Equal(1, lifecycle.BattleEndCalls);
    }

    [Fact]
    public void Runner_RejectsContradictoryCompletionPolicyResultsAsTypedFaults()
    {
        BattleEncounterCompletion[] invalidResults =
        [
            new(false, BattleEncounterOutcome.Victory, PlayerTeam),
            new(false, Message: "Not terminal."),
            new(true, (BattleEncounterOutcome)int.MaxValue),
            new(true, BattleEncounterOutcome.Victory),
            new(true, BattleEncounterOutcome.Defeat),
            new(true, BattleEncounterOutcome.Draw, PlayerTeam),
            new(true, BattleEncounterOutcome.Escape, PlayerTeam),
            new(true, BattleEncounterOutcome.Faulted),
            new(true, BattleEncounterOutcome.Victory, Id("unknown_team")),
            new(true, BattleEncounterOutcome.Victory, default(ContentId))
        ];

        foreach (BattleEncounterCompletion invalid in invalidResults)
        {
            var handler = new QueueTurnHandler(_ =>
                BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
            BattleEncounterResult result = Run(
                [Participant("invalid_completion_player", PlayerTeam),
                 Participant("invalid_completion_enemy", EnemyTeam)],
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new RecordingLifecycle(),
                handler,
                new FixedCompletionPolicy(invalid));

            Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
            Assert.Equal(BattleEncounterFaultCode.CompletionEvaluationFailed, result.FaultCode);
            Assert.Empty(handler.Requests);
        }
    }

    [Theory]
    [InlineData(BattleEncounterOutcome.Victory, true)]
    [InlineData(BattleEncounterOutcome.Defeat, true)]
    [InlineData(BattleEncounterOutcome.Escape, false)]
    [InlineData(BattleEncounterOutcome.Draw, false)]
    [InlineData(BattleEncounterOutcome.Cancelled, false)]
    public void Runner_AcceptsCoherentCompletionPolicyTerminalShapes(
        BattleEncounterOutcome outcome,
        bool requiresWinner)
    {
        ContentId? winner = requiresWinner ? PlayerTeam : null;

        BattleEncounterResult result = Run(
            [Participant("valid_completion_player", PlayerTeam),
             Participant("valid_completion_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ =>
                BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new FixedCompletionPolicy(new BattleEncounterCompletion(true, outcome, winner)));

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(winner, result.WinningTeamId);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.RoundStarted);
    }

    [Theory]
    [InlineData(BattleEncounterEventKind.ActorCreated, 0)]
    [InlineData(BattleEncounterEventKind.RoundStarted, 1)]
    public void Runner_ContainsEventSinkExceptionsAtPreStartAndActiveBattleStages(
        BattleEncounterEventKind failingKind,
        int expectedBattleEndCalls)
    {
        var lifecycle = new RecordingLifecycle();
        var eventSink = new ThrowingEventSink(failingKind);

        BattleEncounterResult result = Run(
            [Participant("event_fault_player", PlayerTeam), Participant("event_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(99),
            events: eventSink);

        AssertPortFault(result, BattleEncounterFaultCode.EventPublicationFailed, "event-publication");
        Assert.Equal(expectedBattleEndCalls, lifecycle.BattleEndCalls);
        Assert.Equal(
            Enumerable.Range(1, result.Events.Count),
            result.Events.Select(battleEvent => battleEvent.Sequence));
        Assert.Equal(BattleEncounterEventKind.BattleFaulted, result.Events[^2].Kind);
        Assert.Equal(BattleEncounterEventKind.BattleEnded, result.Events[^1].Kind);
        Assert.DoesNotContain(eventSink.Events, battleEvent =>
            battleEvent.Kind is BattleEncounterEventKind.BattleFaulted or BattleEncounterEventKind.BattleEnded);
    }

    [Fact]
    public void Runner_PreservesPrimaryPortFaultWhenFaultEventPublicationAlsoFails()
    {
        var lifecycle = new RecordingLifecycle();
        var eventSink = new ThrowingEventSink(BattleEncounterEventKind.BattleFaulted);

        BattleEncounterResult result = Run(
            [Participant("secondary_sink_player", PlayerTeam), Participant("secondary_sink_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new ThrowingTurnHandler(new InvalidOperationException("Primary turn-handler failure.")),
            new CompleteAfterTurnsPolicy(99),
            events: eventSink);

        AssertPortFault(result, BattleEncounterFaultCode.TurnHandlerExecutionFailed, "turn-handler");
        Assert.Equal(1, lifecycle.BattleEndCalls);
        Assert.Equal(BattleEncounterEventKind.BattleEnded, result.Events[^1].Kind);
    }

    [Fact]
    public void Runner_PreservesLifecycleFaultWhenFaultEventPublicationAlsoFails()
    {
        BattleEncounterParticipant player = Participant("lifecycle_sink_player", PlayerTeam);
        var eventSink = new ThrowingEventSink(BattleEncounterEventKind.BattleFaulted);

        BattleEncounterResult result = Run(
            [player, Participant("lifecycle_sink_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new MutatingThrowingLifecycle(ThrowingLifecycleStage.TurnStart),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(99),
            events: eventSink);

        AssertPortFault(result, BattleEncounterFaultCode.LifecycleExecutionFailed, "turn-start");
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(10, result.Participants[0].State.Resources.Single(resource => resource.ResourceId == Hp).Current);
        Assert.Equal(BattleEncounterEventKind.BattleEnded, result.Events[^1].Kind);
    }

    [Fact]
    public void Runner_PreservesPrimaryPortFaultAndRollsBackFailingBattleEndCleanup()
    {
        BattleEncounterParticipant player = Participant("cleanup_fault_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            BattleEndAction = request =>
            {
                request.Participants[0].State.SetResource(Hp, 1);
                throw new InvalidOperationException("Deliberate battle-end cleanup failure.");
            }
        };

        BattleEncounterResult result = Run(
            [player, Participant("cleanup_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new ThrowingTurnHandler(new InvalidOperationException("Primary turn-handler failure.")),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnHandlerExecutionFailed, result.FaultCode);
        Assert.Contains("turn-handler", result.FaultMessage, StringComparison.Ordinal);
        Assert.Contains("battle-end", result.FaultMessage, StringComparison.Ordinal);
        Assert.Equal(1, lifecycle.BattleEndCalls);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(10, result.Participants[0].State.Resources.Single(resource => resource.ResourceId == Hp).Current);
        Assert.Equal(
            [BattleEncounterFaultCode.TurnHandlerExecutionFailed, BattleEncounterFaultCode.LifecycleExecutionFailed],
            result.Events
                .Where(battleEvent => battleEvent.Kind == BattleEncounterEventKind.BattleFaulted)
                .Select(battleEvent => battleEvent.FaultCode));
        Assert.Equal(BattleEncounterEventKind.BattleEnded, result.Events[^1].Kind);
    }

    [Fact]
    public void Runner_PublishesSuccessfulBattleEndLifecycleEventsBeforeTheTerminalEvent()
    {
        BattleEncounterParticipant player = Participant("terminal_success_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            BattleEndEvents = [LifecycleCleanupEvent(player.InstanceId)]
        };

        BattleEncounterResult result = Run(
            [player, Participant("terminal_success_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(BattleEncounterEventKind.ResourceChanged, result.Events[^2].Kind);
        Assert.Equal(BattleEncounterEventKind.BattleEnded, result.Events[^1].Kind);
        Assert.Equal(result.Events.Count, result.Events[^1].Sequence);
    }

    [Fact]
    public void Runner_PublishesFaultCleanupEventsBeforeTheTerminalEvent()
    {
        BattleEncounterParticipant player = Participant("terminal_fault_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            BattleEndEvents = [LifecycleCleanupEvent(player.InstanceId)]
        };

        BattleEncounterResult result = Run(
            [player, Participant("terminal_fault_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new ThrowingTurnHandler(new InvalidOperationException("Deliberate turn-handler failure.")),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterEventKind.ResourceChanged, result.Events[^2].Kind);
        Assert.Equal(BattleEncounterEventKind.BattleEnded, result.Events[^1].Kind);
        Assert.Equal(result.Events.Count, result.Events[^1].Sequence);
    }

    [Fact]
    public void Runner_FaultBeforeStartAlsoReturnsDetachedParticipantSnapshots()
    {
        BattleEncounterParticipant player = Participant("fault_snapshot_player", PlayerTeam);

        BattleEncounterResult result = Run(
            [player, Participant("fault_snapshot_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(10, result.Participants[0].State.Resources.Single(resource => resource.ResourceId == Hp).Current);

        player.State.SetResource(Hp, 2);

        Assert.Equal(10, result.Participants[0].State.Resources.Single(resource => resource.ResourceId == Hp).Current);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Normal, false, false, 1, 0)]
    [InlineData(TurnEconomyOutcome.Weakness, false, false, 1, 1)]
    [InlineData(TurnEconomyOutcome.Critical, true, false, 1, 1)]
    [InlineData(TurnEconomyOutcome.Miss, false, false, 0, 0)]
    [InlineData(TurnEconomyOutcome.Null, false, false, 0, 0)]
    [InlineData(TurnEconomyOutcome.Repel, false, true, 0, 0)]
    [InlineData(TurnEconomyOutcome.Absorb, false, true, 0, 0)]
    public void Runner_AppliesEveryTurnEconomyOutcome(
        TurnEconomyOutcome outcome,
        bool critical,
        bool terminates,
        int expectedFullTokens,
        int expectedPartialTokens)
    {
        BattleEncounterParticipant first = Participant("first", PlayerTeam);
        BattleEncounterParticipant second = Participant("second", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var handler = new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
            ActionTurnConsumption.FromTurnEconomy(new TurnEconomyResolution(outcome, critical, terminates))));

        BattleEncounterResult result = Run(
            [first, second, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(1),
            () => new ActionTokenTurnEconomy());

        BattleEncounterEvent changed = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
        var turnEconomy = Assert.IsType<ActionTokenTurnEconomySnapshot>(changed.TurnEconomyState);
        Assert.Equal(expectedFullTokens, turnEconomy.FullTokens);
        Assert.Equal(expectedPartialTokens, turnEconomy.PartialTokens);
    }

    [Fact]
    public void Runner_DispatchesTurnEndAfterCommittedRestrictionsAndActions()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            TurnStartOutcome = BattleTurnStartOutcome.Skip
        };
        var handler = new QueueTurnHandler(request =>
        {
            Assert.Equal(BattleTurnStartOutcome.Skip, request.TurnStartOutcome);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        Run(
            [player, Participant("enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(1, lifecycle.TurnStartCalls);
        Assert.Equal(1, lifecycle.TurnEndCalls);
    }

    [Fact]
    public void Runner_PreservesTypedLimitedActionRestrictionForTurnHandler()
    {
        ContentId skillAction = Id("skill");
        var lifecycle = new RecordingLifecycle
        {
            Restriction = new BattleTurnStartRestriction(
                BattleTurnStartOutcome.LimitedAction,
                [skillAction],
                [Id("bind")])
        };
        var handler = new QueueTurnHandler(request =>
        {
            Assert.Equal(BattleTurnStartOutcome.LimitedAction, request.TurnStartOutcome);
            Assert.Equal([skillAction], request.AllowedActionIds);
            Assert.Equal([Id("bind")], request.TurnStartRestriction.SourceAilmentIds);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        Run(
            [Participant("player", PlayerTeam), Participant("enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Runner_RefreshesStateAndCompletesWhenAStandingTeamRemains()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var handler = new QueueTurnHandler(_ =>
        {
            enemy.State.SetResource(Hp, 0);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new LastTeamStandingCompletionPolicy());

        Assert.Equal(BattleEncounterOutcome.Victory, result.Outcome);
        Assert.Equal(PlayerTeam, result.WinningTeamId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ActorDefeated &&
            battleEvent.ActorId == enemy.InstanceId);
    }

    [Fact]
    public void Runner_ReconcilesBattleStartMutationBeforeOpeningARound()
    {
        BattleEncounterParticipant player = Participant("battle_start_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("battle_start_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle
        {
            BattleStartAction = request =>
                request.Participants
                    .Single(participant => participant.InstanceId == enemy.InstanceId)
                    .State.SetResource(Hp, 0m)
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new LastTeamStandingCompletionPolicy());

        Assert.Equal(BattleEncounterOutcome.Victory, result.Outcome);
        Assert.Equal(PlayerTeam, result.WinningTeamId);
        Assert.Empty(handler.Requests);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.RoundStarted);
        AssertDefeatPrecedesBattleEnd(result, enemy.InstanceId);
    }

    [Fact]
    public void Runner_ReconcilesTurnStartMutationBeforeCallingTheTurnHandler()
    {
        BattleEncounterParticipant player = Participant("turn_start_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("turn_start_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle
        {
            TurnStartAction = request =>
                request.Participants
                    .Single(participant => participant.InstanceId == enemy.InstanceId)
                    .State.SetResource(Hp, 0m)
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new LastTeamStandingCompletionPolicy());

        Assert.Equal(BattleEncounterOutcome.Victory, result.Outcome);
        Assert.Empty(handler.Requests);
        AssertDefeatPrecedesBattleEnd(result, enemy.InstanceId);
    }

    [Fact]
    public void Runner_SkipsAnActorUndeployedByTurnStartLifecycle()
    {
        BattleEncounterParticipant first = Participant("turn_start_first", PlayerTeam);
        BattleEncounterParticipant second = Participant("turn_start_second", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("turn_start_enemy_skip", EnemyTeam);
        var lifecycle = new RecordingLifecycle
        {
            TurnStartAction = request =>
            {
                if (request.Actor.InstanceId == first.InstanceId)
                {
                    request.Actor.State.SetEncounterPresence(isDeployed: false);
                }
            }
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [first, second, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(second.InstanceId, Assert.Single(handler.Requests).Actor.InstanceId);
        BattleTurnEndedEventPayload firstTurnEnd = Assert.IsType<BattleTurnEndedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.TurnEnded &&
                battleEvent.ActorId == first.InstanceId).Payload);
        Assert.Equal(BattleEncounterTurnEndReason.ActorUnavailable, firstTurnEnd.Reason);
        Assert.Null(firstTurnEnd.TurnConsumption);
    }

    [Fact]
    public void Runner_ReconcilesTurnEndMutationBeforeSchedulingAnotherCommand()
    {
        BattleEncounterParticipant player = Participant("turn_end_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("turn_end_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle
        {
            TurnEndAction = request =>
                request.Participants
                    .Single(participant => participant.InstanceId == enemy.InstanceId)
                    .State.SetResource(Hp, 0m)
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new LastTeamStandingCompletionPolicy());

        Assert.Equal(BattleEncounterOutcome.Victory, result.Outcome);
        Assert.Single(handler.Requests);
        AssertDefeatPrecedesBattleEnd(result, enemy.InstanceId);
    }

    [Fact]
    public void Runner_ReconcilesPhaseEndMutationBeforeStartingTheNextPhase()
    {
        BattleEncounterParticipant player = Participant("phase_end_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("phase_end_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle
        {
            PhaseEndAction = request =>
                request.Participants
                    .Single(participant => participant.InstanceId == enemy.InstanceId)
                    .State.SetResource(Hp, 0m)
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new LastTeamStandingCompletionPolicy());

        Assert.Equal(BattleEncounterOutcome.Victory, result.Outcome);
        Assert.Equal(player.InstanceId, Assert.Single(handler.Requests).Actor.InstanceId);
        AssertDefeatPrecedesBattleEnd(result, enemy.InstanceId);
    }

    [Fact]
    public void Runner_ReconcilesRoundEndMutationBeforeStartingAnotherRound()
    {
        BattleEncounterParticipant player = Participant("round_end_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("round_end_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle
        {
            RoundEndAction = request =>
                request.Participants
                    .Single(participant => participant.InstanceId == enemy.InstanceId)
                    .State.SetResource(Hp, 0m)
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new LastTeamStandingCompletionPolicy());

        Assert.Equal(BattleEncounterOutcome.Victory, result.Outcome);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(1, lifecycle.RoundEndCalls);
        Assert.Equal(
            1,
            result.Events.Count(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.RoundStarted));
        BattleRoundEndedEventPayload roundEnded = Assert.IsType<BattleRoundEndedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.RoundEnded).Payload);
        Assert.Equal(1, roundEnded.RoundNumber);
        BattleEndedEventPayload battleEnded = Assert.IsType<BattleEndedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.BattleEnded).Payload);
        Assert.Equal(1, battleEnded.FinalRoundNumber);
        Assert.Equal(1, battleEnded.CompletedRounds);
        AssertDefeatPrecedesBattleEnd(result, enemy.InstanceId);
    }

    [Fact]
    public void Runner_StopsOnFaultCancellationAndEscape()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);

        BattleEncounterResult fault = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Faulted("bad command")),
            new CompleteAfterTurnsPolicy(99));
        BattleEncounterResult cancelled = Run(
            [Participant("player_cancel", PlayerTeam), Participant("enemy_cancel", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Cancelled()),
            new CompleteAfterTurnsPolicy(99));
        BattleEncounterResult escaped = Run(
            [Participant("player_escape", PlayerTeam), Participant("enemy_escape", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.None,
                requestedOutcome: BattleEncounterOutcome.Escape)),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, fault.Outcome);
        Assert.Equal("bad command", fault.FaultMessage);
        Assert.Equal(BattleEncounterOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(BattleEncounterOutcome.Escape, escaped.Outcome);
    }

    [Fact]
    public void Runner_TreatsCommandRejectionAsFaultWithoutTurnConsumption()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Rejected("selection became invalid")),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal("selection became invalid", result.FaultMessage);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ActionRejected &&
            battleEvent.DebugText == "selection became invalid");
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_RejectsACommandWinnerThatIsNotAnEncounterTeam()
    {
        var lifecycle = new RecordingLifecycle();

        BattleEncounterResult result = Run(
            [Participant("unknown_winner_player", PlayerTeam),
             Participant("unknown_winner_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.Normal,
                requestedOutcome: BattleEncounterOutcome.Victory,
                winningTeamId: Id("unknown_team"))),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.CommandExecutionFaulted, result.FaultCode);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_ContainsContradictoryCommandConstructionAsAPortFaultWithoutSpendingATurn()
    {
        var lifecycle = new RecordingLifecycle();

        BattleEncounterResult result = Run(
            [Participant("invalid_command_player", PlayerTeam), Participant("invalid_command_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => new BattleEncounterCommandResult(
                BattleEncounterCommandStatus.Executed,
                ActionTurnConsumption.Normal,
                requestedOutcome: BattleEncounterOutcome.Cancelled)),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnHandlerExecutionFailed, result.FaultCode);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Theory]
    [MemberData(nameof(RunnerOwnedStructuralEventKinds))]
    public void Runner_RejectsRunnerOwnedStructuralEventsFromTurnHandlers(
        BattleEncounterEventKind eventKind)
    {
        BattleEncounterParticipant player = Participant("command_event_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("command_event_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        string forgedMarker = $"forged-command-{eventKind}";

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.Normal,
                [RunnerOwnedEvent(eventKind, player, forgedMarker)])),
            new CompleteAfterTurnsPolicy(99));

        AssertPortFault(
            result,
            BattleEncounterFaultCode.TurnHandlerExecutionFailed,
            "turn-handler");
        Assert.Contains(eventKind.ToString(), result.FaultMessage, StringComparison.Ordinal);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.DoesNotContain(result.Events, battleEvent => battleEvent.DebugText == forgedMarker);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Theory]
    [MemberData(nameof(RunnerOwnedStructuralEventKinds))]
    public void Runner_RejectsRunnerOwnedStructuralEventsFromLifecyclePorts(
        BattleEncounterEventKind eventKind)
    {
        BattleEncounterParticipant player = Participant("lifecycle_event_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("lifecycle_event_enemy", EnemyTeam);
        string forgedMarker = $"forged-lifecycle-{eventKind}";
        var lifecycle = new RecordingLifecycle
        {
            BattleStartEvents = [RunnerOwnedEvent(eventKind, player, forgedMarker)]
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.LifecycleExecutionFailed, result.FaultCode);
        Assert.Contains("battle-start", result.FaultMessage, StringComparison.Ordinal);
        Assert.Contains(eventKind.ToString(), result.FaultMessage, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
        Assert.DoesNotContain(result.Events, battleEvent => battleEvent.DebugText == forgedMarker);
        Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.BattleFaulted);
        Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.BattleEnded);
    }

    [Fact]
    public async Task Runner_PreCancelledTokenTouchesNoEncounterPortOrActorState()
    {
        BattleEncounterParticipant player = Participant("cancelled_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("cancelled_enemy", EnemyTeam);
        var initiative = new CountingInitiative(PlayerTeam, EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
        var synchronizer = new RecordingSynchronizer();
        var eventSink = new RecordingEventSink();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ValueTask<BattleEncounterResult> run = new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                initiative,
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                handler,
                new CompleteAfterTurnsPolicy(1),
                () => new StandardActionTurnEconomy(),
                new BattlePhaseProgressPolicy(8, 1),
                synchronizer,
                eventSink),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.AsTask());
        Assert.Equal(0, initiative.Calls);
        Assert.Equal(0, synchronizer.Calls);
        Assert.Equal(0, lifecycle.BattleStartCalls);
        Assert.Empty(handler.Requests);
        Assert.Empty(eventSink.Events);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(10, enemy.State.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task Runner_CancellationAfterStartupEventPreventsLaterLifecycleMutation()
    {
        BattleEncounterParticipant player = Participant("cancel_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("cancel_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        using var cancellation = new CancellationTokenSource();
        var eventSink = new CancellingEventSink(cancellation, BattleEncounterEventKind.BattleStarted);

        ValueTask<BattleEncounterResult> run = new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
                new CompleteAfterTurnsPolicy(1),
                () => new StandardActionTurnEconomy(),
                new BattlePhaseProgressPolicy(8, 1),
                events: eventSink),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.AsTask());
        Assert.Equal(0, lifecycle.BattleStartCalls);
        Assert.Contains(eventSink.Events, battleEvent => battleEvent.Kind == BattleEncounterEventKind.BattleStarted);
        Assert.DoesNotContain(eventSink.Events, battleEvent => battleEvent.Kind == BattleEncounterEventKind.InitiativeRolled);
    }

    [Fact]
    public async Task Runner_CancellationDuringActorCreationPreservesEveryPassiveActivationCount()
    {
        ContentId eventId = Id("prior_battle_event");
        SkillDefinition passive = new(
            Id("prior_battle_passive"),
            "Prior Battle Passive",
            "Seeds activation bookkeeping before a cancelled encounter startup.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers:
            [
                new PassiveTriggerDefinition(
                    eventId,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(1))])
            ]);
        BattleEncounterParticipant player = Participant("reset_cancel_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("reset_cancel_enemy", EnemyTeam);
        foreach (BattleEncounterParticipant participant in new[] { player, enemy })
        {
            participant.State.Passives.Add(passive);
            participant.State.Passives.RecordActivation(
                passive.Id,
                triggerIndex: 0,
                eventId,
                targetInstanceId: null);
        }

        using var cancellation = new CancellationTokenSource();
        var eventSink = new CancellingEventSink(cancellation, BattleEncounterEventKind.ActorCreated);
        ValueTask<BattleEncounterResult> run = new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                new RecordingLifecycle(),
                new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
                new CompleteAfterTurnsPolicy(1),
                () => new StandardActionTurnEconomy(),
                new BattlePhaseProgressPolicy(8, 1),
                events: eventSink),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.AsTask());
        Assert.All(new[] { player, enemy }, participant =>
        {
            RuntimePassiveActivationSnapshot activation = Assert.Single(
                participant.State.ToSnapshot().BattleActivations.PassiveActivations);
            Assert.Equal(1, activation.ActivationCount);
        });
    }

    [Fact]
    public void Runner_EventFaultDuringActorCreationPreservesEveryPassiveActivationCount()
    {
        ContentId eventId = Id("prior_fault_event");
        SkillDefinition passive = new(
            Id("prior_fault_passive"),
            "Prior Fault Passive",
            "Seeds activation bookkeeping before a faulted encounter startup.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers:
            [
                new PassiveTriggerDefinition(
                    eventId,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(1))])
            ]);
        BattleEncounterParticipant player = Participant("reset_fault_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("reset_fault_enemy", EnemyTeam);
        foreach (BattleEncounterParticipant participant in new[] { player, enemy })
        {
            participant.State.Passives.Add(passive);
            participant.State.Passives.RecordActivation(
                passive.Id,
                triggerIndex: 0,
                eventId,
                targetInstanceId: null);
        }

        BattleEncounterResult result = new BattleEncounterRunner().Run(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                new RecordingLifecycle(),
                new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
                new CompleteAfterTurnsPolicy(1),
                () => new StandardActionTurnEconomy(),
                new BattlePhaseProgressPolicy(8, 1),
                events: new ThrowingEventSink(BattleEncounterEventKind.ActorCreated)));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.EventPublicationFailed, result.FaultCode);
        Assert.All(new[] { player, enemy }, participant =>
            Assert.Equal(
                1,
                Assert.Single(participant.State.ToSnapshot().BattleActivations.PassiveActivations)
                    .ActivationCount));
    }

    [Fact]
    public async Task Runner_CancellationFromTurnEconomyFactoryPreventsEconomyInitialization()
    {
        BattleEncounterParticipant player = Participant("factory_cancel_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("factory_cancel_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
        var economy = new RecordingTurnEconomy();
        using var cancellation = new CancellationTokenSource();

        ValueTask<BattleEncounterResult> run = new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                handler,
                new CompleteAfterTurnsPolicy(1),
                () =>
                {
                    cancellation.Cancel();
                    return economy;
                },
                new BattlePhaseProgressPolicy(8, 1)),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.AsTask());
        Assert.Equal(0, economy.StartPhaseCalls);
        Assert.Equal(0, economy.ApplyCalls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Runner_CancellationDuringTurnHandlerPreventsEconomyAndTurnEndMutation()
    {
        BattleEncounterParticipant player = Participant("handler_cancel_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("handler_cancel_enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        var economy = new RecordingTurnEconomy();
        using var cancellation = new CancellationTokenSource();
        var handler = new QueueTurnHandler(_ =>
        {
            cancellation.Cancel();
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        ValueTask<BattleEncounterResult> run = new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                handler,
                new CompleteAfterTurnsPolicy(1),
                () => economy,
                new BattlePhaseProgressPolicy(8, 1)),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.AsTask());
        Assert.Equal(1, economy.StartPhaseCalls);
        Assert.Equal(0, economy.ApplyCalls);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Runner_CancellationFromRoundEndLifecycleRollsBackStagedMutation()
    {
        BattleEncounterParticipant player = Participant("round_end_cancel_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("round_end_cancel_enemy", EnemyTeam);
        using var cancellation = new CancellationTokenSource();
        var lifecycle = new RecordingLifecycle
        {
            RoundEndAction = request =>
            {
                request.Participants[0].State.SetResource(Hp, 1);
                cancellation.Cancel();
            }
        };

        ValueTask<BattleEncounterResult> run = new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 1),
            new BattleEncounterServices(
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
                new CompleteAfterTurnsPolicy(99),
                () => new StandardActionTurnEconomy(),
                new BattlePhaseProgressPolicy(8, 1)),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.AsTask());
        Assert.Equal(1, lifecycle.RoundEndCalls);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(10, enemy.State.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task Runner_CancellationFromSuccessfulBattleEndLifecycleRollsBackStagedMutation()
    {
        BattleEncounterParticipant player = Participant("battle_end_cancel_player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("battle_end_cancel_enemy", EnemyTeam);
        using var cancellation = new CancellationTokenSource();
        var lifecycle = new RecordingLifecycle
        {
            BattleEndAction = request =>
            {
                request.Participants[0].State.SetResource(Hp, 1);
                cancellation.Cancel();
            }
        };

        ValueTask<BattleEncounterResult> run = new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([player, enemy], Battle, Kind, Moon, 1),
            new BattleEncounterServices(
                new FixedInitiative(PlayerTeam, EnemyTeam),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
                new CompleteAfterTurnsPolicy(1),
                () => new StandardActionTurnEconomy(),
                new BattlePhaseProgressPolicy(8, 1)),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run.AsTask());
        Assert.Equal(1, lifecycle.BattleEndCalls);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(10, enemy.State.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void Runner_RejectsInitiativeUnlessItIsAnExactTeamPermutation()
    {
        ContentId[][] invalidOrders =
        [
            [PlayerTeam],
            [PlayerTeam, PlayerTeam],
            [PlayerTeam, Id("unknown_team")],
            [PlayerTeam, EnemyTeam, EnemyTeam]
        ];

        foreach (ContentId[] invalidOrder in invalidOrders)
        {
            var lifecycle = new RecordingLifecycle();
            var handler = new QueueTurnHandler(_ =>
                BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));
            var synchronizer = new RecordingSynchronizer();
            BattleEncounterResult result = Run(
                [Participant("initiative_player", PlayerTeam), Participant("initiative_enemy", EnemyTeam)],
                new FixedInitiative(invalidOrder),
                lifecycle,
                handler,
                new CompleteAfterTurnsPolicy(1),
                synchronizer: synchronizer);

            Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
            Assert.Contains("Initiative must return every participating team exactly once", result.FaultMessage);
            Assert.Equal(0, synchronizer.Calls);
            Assert.Equal(0, lifecycle.BattleStartCalls);
            Assert.Empty(handler.Requests);
            Assert.Equal(
                [BattleEncounterEventKind.BattleFaulted, BattleEncounterEventKind.BattleEnded],
                result.Events.Select(battleEvent => battleEvent.Kind));
        }
    }

    [Fact]
    public void Runner_FaultsDeterministicallyWhenFreeActionsDoNotAdvanceThePhase()
    {
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.None));

        BattleEncounterResult result = Run(
            [Participant("free_player", PlayerTeam), Participant("free_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(99),
            phaseProgress: new BattlePhaseProgressPolicy(8, 2));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("consecutive free-action limit of 2", result.FaultMessage);
    }

    [Fact]
    public void Runner_AllowsExactlyTheConfiguredFreeActionLimit()
    {
        int commandIndex = 0;
        var handler = new QueueTurnHandler(_ =>
        {
            commandIndex++;
            return BattleEncounterCommandResult.Executed(
                commandIndex <= 2 ? ActionTurnConsumption.None : ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [Participant("bounded_free_player", PlayerTeam), Participant("bounded_free_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(3),
            phaseProgress: new BattlePhaseProgressPolicy(8, 2));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public void Runner_ResetsConsecutiveFreeActionCountAfterEconomyAdvances()
    {
        int commandIndex = 0;
        var handler = new QueueTurnHandler(_ =>
        {
            commandIndex++;
            return BattleEncounterCommandResult.Executed(
                commandIndex is 1 or 3
                    ? ActionTurnConsumption.None
                    : ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [Participant("reset_free_player", PlayerTeam), Participant("reset_free_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(4),
            () => new ExpandingTurnEconomy(),
            new BattlePhaseProgressPolicy(8, 1));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Runner_RejectsInconsistentInitialEconomyStateBeforeAnyCommand(
        int reportedRemainingActions,
        bool reportsTurnsRemaining)
    {
        BattleEncounterParticipant player = Participant("invalid_economy_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(request =>
        {
            request.Actor.State.SetResource(Hp, 0);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [player, Participant("invalid_economy_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => new InitialStateMismatchTurnEconomy(reportedRemainingActions, reportsTurnsRemaining));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Contains("inconsistent remaining-action state", result.FaultMessage);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, lifecycle.TurnStartCalls);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.PhaseStarted);
    }

    [Theory]
    [InlineData(SnapshotDriftKind.Identity)]
    [InlineData(SnapshotDriftKind.Type)]
    public void Runner_RejectsSnapshotAuthorityDriftBeforeTurnLifecycle(SnapshotDriftKind driftKind)
    {
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [Participant("drift_player", PlayerTeam), Participant("drift_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => new SnapshotDriftTurnEconomy(SnapshotDriftStage.BeforeCommand, driftKind));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Contains(
            driftKind == SnapshotDriftKind.Identity ? "changed identity" : "changed snapshot type",
            result.FaultMessage);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, lifecycle.TurnStartCalls);
    }

    [Fact]
    public void Runner_RejectsSnapshotTypeChangeProducedByEconomyApplication()
    {
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [Participant("apply_drift_player", PlayerTeam), Participant("apply_drift_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => new SnapshotDriftTurnEconomy(SnapshotDriftStage.AfterApply, SnapshotDriftKind.Type));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Contains("changed snapshot type", result.FaultMessage);
        Assert.Single(handler.Requests);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_RejectsEconomyThatRetainsActionsAfterExplicitPhaseTermination()
    {
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.TerminatePhase));
        var economy = new RecordingTurnEconomy();

        BattleEncounterResult result = Run(
            [
                Participant("termination_first_player", PlayerTeam),
                Participant("termination_second_player", PlayerTeam),
                Participant("termination_enemy", EnemyTeam)
            ],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => economy);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Contains("after explicit phase termination", result.FaultMessage);
        Assert.Single(handler.Requests);
        Assert.Equal(1, economy.ApplyCalls);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_RejectsSnapshotStateDriftDuringTurnEconomyEventPublication()
    {
        var lifecycle = new RecordingLifecycle();
        var economy = new SnapshotDriftTurnEconomy(
            SnapshotDriftStage.ExternallyArmed,
            SnapshotDriftKind.State);
        var sink = new MutatingEventSink(
            BattleEncounterEventKind.TurnEconomyChanged,
            economy.ActivateDrift);

        BattleEncounterResult result = Run(
            [Participant("phase_drift_player", PlayerTeam), Participant("phase_drift_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(99),
            () => economy,
            events: sink);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Contains("changed state outside an accepted transition", result.FaultMessage);
        Assert.Equal(1, lifecycle.TurnEndCalls);
        Assert.Equal(0, lifecycle.PhaseEndCalls);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.PhaseEnded);
    }

    [Fact]
    public void Runner_RejectsRetainedEconomyMutationBeforeCommittingTurnStartLifecycle()
    {
        var economy = new ActionTokenTurnEconomy();
        BattleEncounterParticipant player = Participant("turn_start_authority_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            TurnStartAction = request =>
            {
                request.Actor.State.SetResource(Hp, 1);
                economy.Apply(ActionTurnConsumption.Pass);
            }
        };
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Pass));

        BattleEncounterResult result = Run(
            [player, Participant("turn_start_authority_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => economy);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Contains("outside an accepted transition", result.FaultMessage);
        Assert.Empty(handler.Requests);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.Equal(new ActionTokenTurnEconomySnapshot(0, 1), economy.CaptureSnapshot());
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_RejectsRetainedEconomyMutationBeforeApplyingHandlerConsumption()
    {
        var economy = new ActionTokenTurnEconomy();
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ =>
        {
            economy.Apply(ActionTurnConsumption.Pass);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Pass);
        });

        BattleEncounterResult result = Run(
            [Participant("handler_authority_player", PlayerTeam),
                Participant("handler_authority_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => economy);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Single(handler.Requests);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.Equal(new ActionTokenTurnEconomySnapshot(0, 1), economy.CaptureSnapshot());
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_RejectsEconomyMutationDuringCommandEventPublicationBeforeApply()
    {
        var economy = new ActionTokenTurnEconomy();
        BattleEncounterParticipant player = Participant("event_authority_player", PlayerTeam);
        var sink = new MutatingEventSink(
            BattleEncounterEventKind.CommandPassed,
            () => economy.Apply(ActionTurnConsumption.Pass));
        var handler = new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
            ActionTurnConsumption.Pass,
            [new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.CommandPassed,
                new BattleCommandPassedEventPayload(player.InstanceId))]));

        BattleEncounterResult result = Run(
            [player, Participant("event_authority_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => economy,
            events: sink);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Equal(new ActionTokenTurnEconomySnapshot(0, 1), economy.CaptureSnapshot());
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_RejectsRetainedEconomyMutationBeforeCommittingTurnEndLifecycle()
    {
        var economy = new ActionTokenTurnEconomy();
        BattleEncounterParticipant player = Participant("turn_end_authority_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            TurnEndAction = request =>
            {
                request.Actor.State.SetResource(Hp, 1);
                economy.StartPhase(1);
            }
        };

        BattleEncounterResult result = Run(
            [player, Participant("turn_end_authority_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(99),
            () => economy);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Fact]
    public void Runner_RejectsRetainedEconomyMutationBeforeCommittingPhaseEndLifecycle()
    {
        var economy = new ActionTokenTurnEconomy();
        BattleEncounterParticipant player = Participant("phase_end_authority_player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            PhaseEndAction = request =>
            {
                request.Participants[0].State.SetResource(Hp, 1);
                economy.StartPhase(1);
            }
        };

        BattleEncounterResult result = Run(
            [player, Participant("phase_end_authority_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal)),
            new CompleteAfterTurnsPolicy(99),
            () => economy);

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.TurnEconomyTransitionInvalid, result.FaultCode);
        Assert.Equal(10, player.State.GetRequiredResource(Hp).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.PhaseEnded);
    }

    [Fact]
    public void Runner_CommandLimitBoundsAnEconomyThatContinuouslyAddsTurns()
    {
        var handler = new QueueTurnHandler(_ =>
            BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [Participant("expanding_player", PlayerTeam), Participant("expanding_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(99),
            () => new ExpandingTurnEconomy(),
            new BattlePhaseProgressPolicy(3, 1));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("phase command limit of 3", result.FaultMessage);
    }

    [Fact]
    public void Runner_UsesStandardActionEconomyWithoutTurnEconomyState()
    {
        BattleEncounterResult result = Run(
            [Participant("standard_player", PlayerTeam), Participant("standard_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.FromTurnEconomy(
                    new TurnEconomyResolution(TurnEconomyOutcome.Weakness, false, false)))),
            new CompleteAfterTurnsPolicy(1));

        BattleEncounterEvent changed = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
        var state = Assert.IsType<StandardActionTurnEconomySnapshot>(changed.TurnEconomyState);
        Assert.Equal(StandardActionTurnEconomy.EconomyId, state.EconomyId);
        Assert.Equal(0, state.RemainingActions);
    }

    [Fact]
    public void RuntimePublicApi_DoesNotExposeHostSerializerFilesystemOrLegacyTypes()
    {
        Type[] publicTypes = typeof(BattleEncounterRunner).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "Convergence.Encounters")
            .ToArray();
        string[] forbidden =
        [
            "Newtonsoft", "System.Text.Json", "Godot", "System.IO.File", "Database",
            "Combatant", "SkillData", string.Concat("Per", "sona", "Data"), "ItemData", "IGameIO"
        ];

        IEnumerable<Type> signatures = publicTypes.SelectMany(PublicSignatureTypes);

        Assert.DoesNotContain(signatures, type =>
            forbidden.Any(token => (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal)));
    }

    private static BattleEncounterResult Run(
        IReadOnlyList<BattleEncounterParticipant> participants,
        IBattleEncounterInitiativePolicy initiative,
        IBattleEncounterLifecyclePort lifecycle,
        IBattleEncounterTurnHandler handler,
        IBattleEncounterCompletionPolicy completion,
        Func<IBattleTurnEconomy>? turnEconomyFactory = null,
        BattlePhaseProgressPolicy? phaseProgress = null,
        IBattleEncounterStateSynchronizer? synchronizer = null,
        IBattleEncounterEventSink? events = null,
        IBattleEncounterSchedulePolicy? schedule = null) =>
        new BattleEncounterRunner().Run(
            new BattleEncounterRequest(participants, Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                initiative,
                schedule ?? new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                handler,
                completion,
                turnEconomyFactory ?? (() => new StandardActionTurnEconomy()),
                phaseProgress ?? new BattlePhaseProgressPolicy(32, 4),
                synchronizer,
                events));

    private static int Index(BattleEncounterResult result, BattleEncounterEventKind kind) =>
        result.Events.First(battleEvent => battleEvent.Kind == kind).Sequence;

    private static BattleEncounterEvent LifecycleCleanupEvent(RuntimeInstanceId actorId) =>
        new(
            0,
            BattleEncounterEventKind.ResourceChanged,
            new BattleResourceChangedEventPayload(actorId, actorId, 0m, Hp),
            "Battle-end cleanup completed.");

    private static BattleEncounterEvent RunnerOwnedEvent(
        BattleEncounterEventKind kind,
        BattleEncounterParticipant actor,
        string debugText)
    {
        var before = new StandardActionTurnEconomySnapshot(1);
        var after = new StandardActionTurnEconomySnapshot(0);
        BattleEncounterEventPayload payload = kind switch
        {
            BattleEncounterEventKind.ActorCreated => new BattleActorCreatedEventPayload(
                actor.InstanceId,
                actor.State.EntityId,
                actor.TeamId),
            BattleEncounterEventKind.BattleStarted => new BattleStartedEventPayload(
                Battle,
                Kind,
                Moon,
                1,
                [actor.InstanceId],
                [actor.TeamId]),
            BattleEncounterEventKind.InitiativeRolled => new BattleInitiativeRolledEventPayload(
                [actor.TeamId]),
            BattleEncounterEventKind.RoundStarted => new BattleRoundStartedEventPayload(1),
            BattleEncounterEventKind.PhaseStarted => new BattlePhaseStartedEventPayload(
                actor.TeamId,
                before),
            BattleEncounterEventKind.TurnStarted => new BattleTurnStartedEventPayload(
                actor.InstanceId,
                actor.TeamId),
            BattleEncounterEventKind.TurnRestricted => new BattleTurnRestrictedEventPayload(
                actor.InstanceId,
                new BattleTurnStartRestriction(BattleTurnStartOutcome.Skip)),
            BattleEncounterEventKind.TurnEconomyChanged => new BattleTurnEconomyChangedEventPayload(
                actor.InstanceId,
                before,
                after,
                ActionTurnConsumption.Normal),
            BattleEncounterEventKind.TurnEnded => new BattleTurnEndedEventPayload(
                actor.InstanceId,
                actor.TeamId,
                BattleEncounterTurnEndReason.CommandCommitted,
                after,
                ActionTurnConsumption.Normal),
            BattleEncounterEventKind.ActorDefeated => new BattleActorDefeatedEventPayload(
                actor.InstanceId,
                actor.TeamId),
            BattleEncounterEventKind.PhaseEnded => new BattlePhaseEndedEventPayload(
                actor.TeamId,
                after),
            BattleEncounterEventKind.RoundEnded => new BattleRoundEndedEventPayload(1),
            BattleEncounterEventKind.BattleFaulted => new BattleFaultedEventPayload(
                BattleEncounterFaultCode.TurnHandlerExecutionFailed,
                actor.InstanceId,
                actor.TeamId,
                "forged-port"),
            BattleEncounterEventKind.BattleEnded => new BattleEndedEventPayload(
                BattleEncounterOutcome.Draw,
                null,
                0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        return new BattleEncounterEvent(0, kind, payload, debugText);
    }

    private static void AssertPortFault(
        BattleEncounterResult result,
        BattleEncounterFaultCode expectedCode,
        string expectedPortName)
    {
        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal(expectedCode, result.FaultCode);
        Assert.Contains(expectedPortName, result.FaultMessage, StringComparison.Ordinal);
        BattleEncounterEvent fault = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.BattleFaulted);
        Assert.Equal(expectedCode, fault.FaultCode);
    }

    private static void AssertDefeatPrecedesBattleEnd(
        BattleEncounterResult result,
        RuntimeInstanceId actorId)
    {
        BattleEncounterEvent defeat = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ActorDefeated &&
            battleEvent.ActorId == actorId);
        BattleEncounterEvent battleEnd = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.BattleEnded);
        Assert.True(defeat.Sequence < battleEnd.Sequence);
    }

    private static BattleEncounterParticipant Participant(string id, ContentId teamId)
    {
        var state = new RuntimeActorState(
            RuntimeInstanceId.Parse(id),
            Id(id + "_entity"),
            teamId,
            Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(Hp, 10, 10),
                new BattleResourceState(Sp, 5, 5)
            ],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), teamId));
        return new BattleEncounterParticipant(state, id);
    }

    private static BattleEncounterParticipant ParticipantWithAgility(
        string id,
        ContentId teamId,
        decimal agility)
    {
        var state = new RuntimeActorState(
            RuntimeInstanceId.Parse(id),
            Id(id + "_entity"),
            teamId,
            Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(Hp, 10, 10),
                new BattleResourceState(Sp, 5, 5)
            ],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), teamId),
            [new KeyValuePair<ContentId, decimal>(Id("agility"), agility)]);
        return new BattleEncounterParticipant(state, id);
    }

    private static IEnumerable<Type> PublicSignatureTypes(Type type)
    {
        yield return type;
        foreach (System.Reflection.PropertyInfo property in type.GetProperties(
                     System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.Instance |
                     System.Reflection.BindingFlags.Static))
        {
            foreach (Type nested in Flatten(property.PropertyType)) yield return nested;
        }

        foreach (System.Reflection.MethodInfo method in type.GetMethods(
                     System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.Instance |
                     System.Reflection.BindingFlags.Static |
                     System.Reflection.BindingFlags.DeclaredOnly))
        {
            foreach (Type nested in Flatten(method.ReturnType)) yield return nested;
            foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
            {
                foreach (Type nested in Flatten(parameter.ParameterType)) yield return nested;
            }
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.IsArray)
        {
            foreach (Type nested in Flatten(type.GetElementType()!)) yield return nested;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Flatten(argument)) yield return nested;
        }
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class FixedInitiative(params ContentId[] teamOrder) : IBattleEncounterInitiativePolicy
    {
        public IReadOnlyList<ContentId> DetermineTeamOrder(BattleEncounterInitiativeRequest request) => teamOrder;
    }

    private sealed class EnemyFirstSchedulePolicy(RuntimeInstanceId enemyId) :
        IBattleEncounterSchedulePolicy
    {
        private static readonly ContentId Id = ContentId.Parse("enemy_first_test_schedule");

        public ContentId PolicyId => Id;

        public BattleEncounterScheduleTransitionResult Start(
            BattleEncounterScheduleStartRequest request)
        {
            var state = new ScriptedScheduleState(
                PolicyId,
                revision: 0,
                nextStepSequence: 0,
                completedRounds: 0,
                request.Participants.Select(participant => participant.InstanceId),
                request.TeamOrder,
                request.RoundLimit);
            return BattleEncounterScheduleTransitionResult.Start(
                state,
                new BattleEncounterRoundStartedScheduleStep(PolicyId, 0, 1));
        }

        public BattleEncounterScheduleTransitionResult Advance(
            BattleEncounterScheduleAdvanceRequest request)
        {
            ScriptedScheduleState state = Assert.IsType<ScriptedScheduleState>(request.State);
            BattleEncounterScheduleStateSnapshot after = state.Advance();
            return request.CompletedStep switch
            {
                BattleEncounterRoundStartedScheduleStep =>
                    BattleEncounterScheduleTransitionResult.Advance(
                        state,
                        after,
                        new BattleEncounterPhaseStartedScheduleStep(
                            PolicyId,
                            after.NextStepSequence,
                            1,
                            EnemyTeam,
                            new BattleEncounterTurnEconomyStart(1))),
                BattleEncounterPhaseStartedScheduleStep =>
                    BattleEncounterScheduleTransitionResult.Advance(
                        state,
                        after,
                        new BattleEncounterCommandWindowScheduleStep(
                            PolicyId,
                            after.NextStepSequence,
                            1,
                            enemyId,
                            EnemyTeam)),
                BattleEncounterCommandWindowScheduleStep =>
                    BattleEncounterScheduleTransitionResult.Complete(state, after),
                _ => throw new InvalidOperationException("Unexpected scripted schedule step.")
            };
        }
    }

    private sealed class ThrowingSchedulePolicy : IBattleEncounterSchedulePolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("throwing_test_schedule");

        public BattleEncounterScheduleTransitionResult Start(
            BattleEncounterScheduleStartRequest request) =>
            throw new InvalidOperationException("Deliberate schedule failure.");

        public BattleEncounterScheduleTransitionResult Advance(
            BattleEncounterScheduleAdvanceRequest request) =>
            throw new InvalidOperationException("Deliberate schedule failure.");
    }

    private sealed class RejectingSchedulePolicy : IBattleEncounterSchedulePolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("rejecting_test_schedule");

        public BattleEncounterScheduleTransitionResult Start(
            BattleEncounterScheduleStartRequest request) =>
            BattleEncounterScheduleTransitionResult.RejectStart(
                [new BattleEncounterScheduleDiagnostic(
                    BattleEncounterScheduleDiagnosticCode.PolicyRejected,
                    "Deliberate schedule rejection.")]);

        public BattleEncounterScheduleTransitionResult Advance(
            BattleEncounterScheduleAdvanceRequest request) =>
            BattleEncounterScheduleTransitionResult.RejectAdvance(
                request.State,
                [new BattleEncounterScheduleDiagnostic(
                    BattleEncounterScheduleDiagnosticCode.PolicyRejected,
                    "Deliberate schedule rejection.")]);
    }

    private sealed class ScriptedScheduleState : BattleEncounterScheduleStateSnapshot
    {
        public ScriptedScheduleState(
            ContentId policyId,
            long revision,
            long nextStepSequence,
            int completedRounds,
            IEnumerable<RuntimeInstanceId> participantIds,
            IEnumerable<ContentId> teamOrder,
            int roundLimit)
            : base(
                policyId,
                revision,
                currentRound: 1,
                completedRounds,
                nextStepSequence,
                participantIds,
                teamOrder,
                roundLimit)
        {
        }

        public ScriptedScheduleState Advance() =>
            new(
                PolicyId,
                checked(Revision + 1),
                checked(NextStepSequence + 1),
                CompletedRounds,
                ParticipantIds,
                TeamOrder,
                RoundLimit);
    }

    private sealed class ThrowingInitiative : IBattleEncounterInitiativePolicy
    {
        public IReadOnlyList<ContentId> DetermineTeamOrder(BattleEncounterInitiativeRequest request) =>
            throw new InvalidOperationException("Deliberate initiative failure.");
    }

    private sealed class CountingInitiative(params ContentId[] teamOrder) : IBattleEncounterInitiativePolicy
    {
        public int Calls { get; private set; }

        public IReadOnlyList<ContentId> DetermineTeamOrder(BattleEncounterInitiativeRequest request)
        {
            Calls++;
            return teamOrder;
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state) =>
            Interlocked.Increment(ref _postCount);
    }

    private sealed class AsynchronousEncounterPorts :
        IBattleEncounterLifecyclePort,
        IBattleEncounterTurnHandler,
        IBattleEncounterEventSink
    {
        public int PublishCalls { get; private set; }
        public int BattleStartCalls { get; private set; }
        public int TurnStartCalls { get; private set; }
        public int TurnHandlerCalls { get; private set; }
        public int TurnEndCalls { get; private set; }
        public int BattleEndCalls { get; private set; }

        public async ValueTask PublishAsync(
            BattleEncounterEvent battleEvent,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            PublishCalls++;
        }

        public async ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            BattleStartCalls++;
            return [];
        }

        public async ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            TurnStartCalls++;
            return new BattleTurnStartLifecycleResult(BattleTurnStartOutcome.CanAct, []);
        }

        public async ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            TurnHandlerCalls++;
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        }

        public async ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            TurnEndCalls++;
            return [];
        }

        public async ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return [];
        }

        public async ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
            BattleEncounterLifecycleRequest request,
            int roundNumber,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return [];
        }

        public async ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            BattleEndCalls++;
            return [];
        }
    }

    private sealed class QueueTurnHandler(Func<BattleEncounterTurnRequest, BattleEncounterCommandResult> handler)
        : IBattleEncounterTurnHandler
    {
        public List<BattleEncounterTurnRequest> Requests { get; } = [];

        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return new ValueTask<BattleEncounterCommandResult>(handler(request));
        }
    }

    private sealed class ThrowingTurnHandler(Exception failure) : IBattleEncounterTurnHandler
    {
        public int Calls { get; private set; }

        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return new ValueTask<BattleEncounterCommandResult>(
                Task.FromException<BattleEncounterCommandResult>(failure));
        }
    }

    private sealed class NullTurnHandler : IBattleEncounterTurnHandler
    {
        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default) =>
            new((BattleEncounterCommandResult)null!);
    }

    private sealed class CompleteAfterTurnsPolicy(int count) : IBattleEncounterCompletionPolicy
    {
        private int _turns;

        public BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request)
        {
            if (request.LastActor is null)
            {
                return new BattleEncounterCompletion(false);
            }

            _turns++;
            return _turns >= count
                ? new BattleEncounterCompletion(true, BattleEncounterOutcome.Draw)
                : new BattleEncounterCompletion(false);
        }
    }

    private sealed class FailingCompletionPolicy(bool returnNull) : IBattleEncounterCompletionPolicy
    {
        public int Calls { get; private set; }

        public BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request)
        {
            Calls++;
            return returnNull
                ? null!
                : throw new InvalidOperationException("Deliberate completion failure.");
        }
    }

    private sealed class FixedCompletionPolicy(BattleEncounterCompletion result)
        : IBattleEncounterCompletionPolicy
    {
        public BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request) =>
            result;
    }

    private sealed class RecordingLifecycle : IBattleEncounterLifecyclePort
    {
        public IReadOnlyList<ContentId> BattleStartTeamOrder { get; private set; } = [];
        public BattleTurnStartOutcome TurnStartOutcome { get; init; } = BattleTurnStartOutcome.CanAct;
        public BattleTurnStartRestriction? Restriction { get; init; }
        public Action<BattleEncounterLifecycleRequest>? BattleStartAction { get; init; }
        public Action<BattleEncounterTurnLifecycleRequest>? TurnStartAction { get; init; }
        public Action<BattleEncounterTurnLifecycleRequest>? TurnEndAction { get; init; }
        public Action<BattleEncounterLifecycleRequest>? PhaseEndAction { get; init; }
        public Action<BattleEncounterLifecycleRequest>? RoundEndAction { get; init; }
        public Action<BattleEncounterLifecycleRequest>? BattleEndAction { get; init; }
        public IReadOnlyList<BattleEncounterEvent> BattleStartEvents { get; init; } = [];
        public IReadOnlyList<BattleEncounterEvent> BattleEndEvents { get; init; } = [];
        public int BattleStartCalls { get; private set; }
        public int TurnStartCalls { get; private set; }
        public int TurnEndCalls { get; private set; }
        public int PhaseEndCalls { get; private set; }
        public int RoundEndCalls { get; private set; }
        public int BattleEndCalls { get; private set; }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            BattleStartCalls++;
            BattleStartTeamOrder = request.TeamOrder;
            BattleStartAction?.Invoke(request);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(BattleStartEvents);
        }

        public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            TurnStartCalls++;
            TurnStartAction?.Invoke(request);
            return new ValueTask<BattleTurnStartLifecycleResult>(
                Restriction is null
                    ? new BattleTurnStartLifecycleResult(TurnStartOutcome, [])
                    : new BattleTurnStartLifecycleResult(Restriction, []));
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            TurnEndCalls++;
            TurnEndAction?.Invoke(request);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default)
        {
            PhaseEndCalls++;
            PhaseEndAction?.Invoke(request);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
            BattleEncounterLifecycleRequest request,
            int roundNumber,
            CancellationToken cancellationToken = default)
        {
            RoundEndCalls++;
            RoundEndAction?.Invoke(request);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            BattleEndCalls++;
            BattleEndAction?.Invoke(request);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(BattleEndEvents);
        }
    }

    private sealed class MutatingThrowingDepartureLifecycle(RuntimeInstanceId departingActorId) :
        IBattleEncounterLifecyclePort,
        IBattleEncounterDepartureLifecyclePort
    {
        public BattleStatusDepartureReason? DepartureReason { get; private set; }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            new(Array.Empty<BattleEncounterEvent>());

        public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            new(new BattleTurnStartLifecycleResult(
                request.Actor.InstanceId == departingActorId
                    ? BattleTurnStartOutcome.FleeBattle
                    : BattleTurnStartOutcome.CanAct,
                []));

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            new(Array.Empty<BattleEncounterEvent>());

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default) =>
            new(Array.Empty<BattleEncounterEvent>());

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
            BattleEncounterLifecycleRequest request,
            int roundNumber,
            CancellationToken cancellationToken = default) =>
            new(Array.Empty<BattleEncounterEvent>());

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default) =>
            new(Array.Empty<BattleEncounterEvent>());

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessActorDepartureAsync(
            BattleEncounterDepartureLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            DepartureReason = request.Reason;
            request.Actor.State.SetResource(Hp, 1m);
            throw new InvalidOperationException("Deliberate actor-departure failure.");
        }
    }

    public enum BoundarySourceFailure
    {
        Throw,
        NullResult,
        InvalidBoundary,
        DuplicateEvent
    }

    private sealed class BoundarySourceLifecycle :
        IBattleEncounterLifecyclePort,
        IBattleEncounterStatModifierBoundarySource
    {
        private readonly Func<BattleEncounterTurnLifecycleRequest,
            IReadOnlyList<StatModifierLifecycleBoundary>> _boundaries;

        public BoundarySourceLifecycle(BoundarySourceFailure failure)
            : this(_ => ResolveFailure(failure))
        {
        }

        public BoundarySourceLifecycle(
            Func<BattleEncounterTurnLifecycleRequest,
                IReadOnlyList<StatModifierLifecycleBoundary>> boundaries)
        {
            _boundaries = boundaries;
        }

        public int BattleEndCalls { get; private set; }

        public IReadOnlyList<StatModifierLifecycleBoundary> GetActiveStatModifierBoundaries(
            BattleEncounterTurnLifecycleRequest request) =>
            _boundaries(request);

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

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
            BattleEncounterLifecycleRequest request,
            int roundNumber,
            CancellationToken cancellationToken = default) =>
            new(Array.Empty<BattleEncounterEvent>());

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            BattleEndCalls++;
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        private static IReadOnlyList<StatModifierLifecycleBoundary> ResolveFailure(
            BoundarySourceFailure failure) => failure switch
            {
                BoundarySourceFailure.Throw => throw new InvalidOperationException(
                    "Deliberate stat-modifier-boundary-source failure."),
                BoundarySourceFailure.NullResult => null!,
                BoundarySourceFailure.InvalidBoundary =>
                    [new StatModifierLifecycleBoundary(default, 0)],
                BoundarySourceFailure.DuplicateEvent =>
                    [
                        new StatModifierLifecycleBoundary(OwnerTurnEnd, 1),
                        new StatModifierLifecycleBoundary(OwnerTurnEnd, 2)
                    ],
                _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
            };
    }

    public enum ThrowingLifecycleStage
    {
        BattleStart,
        TurnStart,
        TurnEnd,
        PhaseEnd,
        RoundEnd,
        BattleEnd
    }

    private sealed class MutatingThrowingLifecycle(ThrowingLifecycleStage stage)
        : IBattleEncounterLifecyclePort
    {
        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            FailIf(ThrowingLifecycleStage.BattleStart, request.Participants[0].State);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>([]);
        }

        public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            FailIf(ThrowingLifecycleStage.TurnStart, request.Actor.State);
            return new ValueTask<BattleTurnStartLifecycleResult>(
                new BattleTurnStartLifecycleResult(BattleTurnStartOutcome.CanAct, []));
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            FailIf(ThrowingLifecycleStage.TurnEnd, request.Actor.State);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>([]);
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default)
        {
            FailIf(ThrowingLifecycleStage.PhaseEnd, request.Participants[0].State);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>([]);
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
            BattleEncounterLifecycleRequest request,
            int roundNumber,
            CancellationToken cancellationToken = default)
        {
            FailIf(ThrowingLifecycleStage.RoundEnd, request.Participants[0].State);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>([]);
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            FailIf(ThrowingLifecycleStage.BattleEnd, request.Participants[0].State);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>([]);
        }

        private void FailIf(ThrowingLifecycleStage candidate, RuntimeActorState actor)
        {
            if (stage != candidate)
            {
                return;
            }

            actor.SetResource(Hp, 1);
            throw new InvalidOperationException($"Deliberate {DiagnosticName(candidate)} failure.");
        }
    }

    private static string DiagnosticName(ThrowingLifecycleStage stage) => stage switch
    {
        ThrowingLifecycleStage.BattleStart => "battle-start",
        ThrowingLifecycleStage.TurnStart => "turn-start",
        ThrowingLifecycleStage.TurnEnd => "turn-end",
        ThrowingLifecycleStage.PhaseEnd => "phase-end",
        ThrowingLifecycleStage.RoundEnd => "round-end",
        ThrowingLifecycleStage.BattleEnd => "battle-end",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };

    private sealed class RecordingSynchronizer : IBattleEncounterStateSynchronizer
    {
        public int Calls { get; private set; }

        public void Synchronize(IReadOnlyList<BattleEncounterParticipant> participants) => Calls++;
    }

    private sealed class ThrowingSynchronizer(int throwOnCall) : IBattleEncounterStateSynchronizer
    {
        public int Calls { get; private set; }

        public void Synchronize(IReadOnlyList<BattleEncounterParticipant> participants)
        {
            Calls++;
            if (Calls == throwOnCall)
            {
                throw new InvalidOperationException("Deliberate state-synchronization failure.");
            }
        }
    }

    private sealed class RecordingEventSink : IBattleEncounterEventSink
    {
        public List<BattleEncounterEvent> Events { get; } = [];

        public ValueTask PublishAsync(
            BattleEncounterEvent battleEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(battleEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MutatingEventSink(
        BattleEncounterEventKind trigger,
        Action mutation) : IBattleEncounterEventSink
    {
        private bool _mutated;

        public ValueTask PublishAsync(
            BattleEncounterEvent battleEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_mutated && battleEvent.Kind == trigger)
            {
                _mutated = true;
                mutation();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellingEventSink(
        CancellationTokenSource cancellation,
        BattleEncounterEventKind cancelAfter) : IBattleEncounterEventSink
    {
        public List<BattleEncounterEvent> Events { get; } = [];

        public ValueTask PublishAsync(
            BattleEncounterEvent battleEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(battleEvent);
            if (battleEvent.Kind == cancelAfter)
            {
                cancellation.Cancel();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingEventSink(BattleEncounterEventKind failingKind) : IBattleEncounterEventSink
    {
        private bool _failed;

        public List<BattleEncounterEvent> Events { get; } = [];

        public ValueTask PublishAsync(
            BattleEncounterEvent battleEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_failed || battleEvent.Kind == failingKind)
            {
                _failed = true;
                return new ValueTask(Task.FromException(
                    new InvalidOperationException("Deliberate event-publication failure.")));
            }

            Events.Add(battleEvent);
            return ValueTask.CompletedTask;
        }
    }

    public enum ThrowingTurnEconomyStage
    {
        Factory,
        NullFactory,
        StartPhase,
        CaptureSnapshot,
        NullSnapshot,
        HasTurnsRemaining,
        Apply
    }

    private sealed class ThrowingTurnEconomy(ThrowingTurnEconomyStage stage) : IBattleTurnEconomy
    {
        private int _remaining;

        public void StartPhase(int activeActorCount)
        {
            FailIf(ThrowingTurnEconomyStage.StartPhase);
            _remaining = activeActorCount;
        }

        public bool HasTurnsRemaining()
        {
            FailIf(ThrowingTurnEconomyStage.HasTurnsRemaining);
            return _remaining > 0;
        }

        public BattleTurnEconomySnapshot CaptureSnapshot()
        {
            FailIf(ThrowingTurnEconomyStage.CaptureSnapshot);
            if (stage == ThrowingTurnEconomyStage.NullSnapshot)
            {
                return null!;
            }

            return new StandardActionTurnEconomySnapshot(_remaining);
        }

        public void Apply(ActionTurnConsumption consumption)
        {
            FailIf(ThrowingTurnEconomyStage.Apply);
            if (consumption.Kind != ActionTurnConsumptionKind.None && _remaining > 0)
            {
                _remaining--;
            }
        }

        private void FailIf(ThrowingTurnEconomyStage candidate)
        {
            if (stage == candidate)
            {
                throw new InvalidOperationException($"Deliberate turn-economy-{candidate} failure.");
            }
        }
    }

    private sealed class ExpandingTurnEconomy : IBattleTurnEconomy
    {
        private static readonly ContentId Economy = Id("expanding_turns");
        private int _remaining;

        public void StartPhase(int activeActorCount) => _remaining = activeActorCount;
        public bool HasTurnsRemaining() => _remaining > 0;
        public BattleTurnEconomySnapshot CaptureSnapshot() => new ExpandingTurnEconomySnapshot(_remaining);

        public void Apply(ActionTurnConsumption consumption)
        {
            ArgumentNullException.ThrowIfNull(consumption);
            if (consumption.Kind != ActionTurnConsumptionKind.None)
            {
                _remaining++;
            }
        }

        private sealed record ExpandingTurnEconomySnapshot : BattleTurnEconomySnapshot
        {
            public ExpandingTurnEconomySnapshot(int remainingActions)
                : base(Economy, remainingActions)
            {
            }
        }
    }

    private sealed class InitialStateMismatchTurnEconomy(
        int remainingActions,
        bool hasTurnsRemaining) : IBattleTurnEconomy
    {
        public void StartPhase(int activeActorCount)
        {
        }

        public bool HasTurnsRemaining() => hasTurnsRemaining;

        public BattleTurnEconomySnapshot CaptureSnapshot() =>
            new StandardActionTurnEconomySnapshot(remainingActions);

        public void Apply(ActionTurnConsumption consumption) =>
            throw new InvalidOperationException("An inconsistent initial economy must never receive a command.");
    }

    public enum SnapshotDriftStage
    {
        BeforeCommand,
        AfterApply,
        ExternallyArmed
    }

    public enum SnapshotDriftKind
    {
        Identity,
        Type,
        State
    }

    private sealed class SnapshotDriftTurnEconomy(
        SnapshotDriftStage driftStage,
        SnapshotDriftKind driftKind) : IBattleTurnEconomy
    {
        private static readonly ContentId StableEconomyId = Id("stable_scripted_economy");
        private static readonly ContentId DriftedEconomyId = Id("drifted_scripted_economy");
        private bool _capturedInitialSnapshot;
        private bool _applied;
        private bool _externalDriftActive;
        private int _remaining;

        public void StartPhase(int activeActorCount) => _remaining = 1;

        public bool HasTurnsRemaining() => _remaining > 0;

        public BattleTurnEconomySnapshot CaptureSnapshot()
        {
            bool shouldDrift = driftStage switch
            {
                SnapshotDriftStage.BeforeCommand => _capturedInitialSnapshot,
                SnapshotDriftStage.AfterApply => _applied,
                SnapshotDriftStage.ExternallyArmed => _externalDriftActive,
                _ => false
            };
            _capturedInitialSnapshot = true;

            if (!shouldDrift)
            {
                return new ScriptedTurnEconomySnapshot(StableEconomyId, _remaining, revision: 0);
            }

            return driftKind switch
            {
                SnapshotDriftKind.Identity =>
                    new ScriptedTurnEconomySnapshot(DriftedEconomyId, _remaining, revision: 0),
                SnapshotDriftKind.Type =>
                    new AlternateScriptedTurnEconomySnapshot(StableEconomyId, _remaining),
                SnapshotDriftKind.State =>
                    new ScriptedTurnEconomySnapshot(StableEconomyId, _remaining, revision: 1),
                _ => throw new ArgumentOutOfRangeException(nameof(driftKind))
            };
        }

        public void Apply(ActionTurnConsumption consumption)
        {
            ArgumentNullException.ThrowIfNull(consumption);
            _remaining = 0;
            _applied = true;
        }

        public void ActivateDrift() => _externalDriftActive = true;
    }

    private sealed record ScriptedTurnEconomySnapshot : BattleTurnEconomySnapshot
    {
        public ScriptedTurnEconomySnapshot(ContentId economyId, int remainingActions, int revision)
            : base(economyId, remainingActions)
        {
            Revision = revision;
        }

        public int Revision { get; }
    }

    private sealed record AlternateScriptedTurnEconomySnapshot : BattleTurnEconomySnapshot
    {
        public AlternateScriptedTurnEconomySnapshot(ContentId economyId, int remainingActions)
            : base(economyId, remainingActions)
        {
        }
    }

    private sealed class RecordingTurnEconomy : IBattleTurnEconomy
    {
        private int _remaining;

        public int StartPhaseCalls { get; private set; }
        public int ApplyCalls { get; private set; }

        public void StartPhase(int activeActorCount)
        {
            StartPhaseCalls++;
            _remaining = activeActorCount;
        }

        public bool HasTurnsRemaining() => _remaining > 0;

        public BattleTurnEconomySnapshot CaptureSnapshot() =>
            new StandardActionTurnEconomySnapshot(_remaining);

        public void Apply(ActionTurnConsumption consumption)
        {
            ApplyCalls++;
            if (consumption.Kind != ActionTurnConsumptionKind.None && _remaining > 0)
            {
                _remaining--;
            }
        }
    }
}
