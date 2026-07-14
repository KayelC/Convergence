using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class BattleEncounterRunnerTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId Kind = Id("normal_battle");
    private static readonly ContentId Moon = Id("new_moon");
    private static readonly ContentId Hp = Id("hp");
    private static readonly ContentId Sp = Id("sp");
    private static readonly ContentId PlayerTeam = Id("player_team");
    private static readonly ContentId EnemyTeam = Id("enemy_team");

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
        Assert.Null(result.Events[1].FaultCode);
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
    [InlineData(PressTurnOutcome.Normal, false, false, 1, 0)]
    [InlineData(PressTurnOutcome.Weakness, false, false, 1, 1)]
    [InlineData(PressTurnOutcome.Critical, true, false, 1, 1)]
    [InlineData(PressTurnOutcome.Miss, false, false, 0, 0)]
    [InlineData(PressTurnOutcome.Null, false, false, 0, 0)]
    [InlineData(PressTurnOutcome.Repel, false, true, 0, 0)]
    [InlineData(PressTurnOutcome.Absorb, false, true, 0, 0)]
    public void Runner_AppliesEveryPressTurnOutcome(
        PressTurnOutcome outcome,
        bool critical,
        bool terminates,
        int expectedFullIcons,
        int expectedBlinkingIcons)
    {
        BattleEncounterParticipant first = Participant("first", PlayerTeam);
        BattleEncounterParticipant second = Participant("second", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var handler = new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
            ActionTurnConsumption.FromPressTurn(new PressTurnResolution(outcome, critical, terminates))));

        BattleEncounterResult result = Run(
            [first, second, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(1),
            () => new PressTurnEngine());

        BattleEncounterEvent changed = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
        var pressTurn = Assert.IsType<PressTurnEconomySnapshot>(changed.TurnEconomyState);
        Assert.Equal(expectedFullIcons, pressTurn.FullIcons);
        Assert.Equal(expectedBlinkingIcons, pressTurn.BlinkingIcons);
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
            battleEvent.Message == "selection became invalid");
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged);
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
    public void Runner_UsesStandardActionEconomyWithoutPressTurnState()
    {
        BattleEncounterResult result = Run(
            [Participant("standard_player", PlayerTeam), Participant("standard_enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.FromPressTurn(
                    new PressTurnResolution(PressTurnOutcome.Weakness, false, false)))),
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
            .Where(type => type.IsPublic && type.Namespace == "JRPGPrototype.Logic.Battle.Runtime")
            .ToArray();
        string[] forbidden =
        [
            "Newtonsoft", "System.Text.Json", "Godot", "System.IO.File", "Database",
            "Combatant", "SkillData", "PersonaData", "ItemData", "IGameIO"
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
        IBattleEncounterEventSink? events = null) =>
        new BattleEncounterRunner().Run(
            new BattleEncounterRequest(participants, Battle, Kind, Moon, 5),
            new BattleEncounterServices(
                initiative,
                lifecycle,
                handler,
                completion,
                turnEconomyFactory ?? (() => new StandardActionTurnEconomy()),
                phaseProgress ?? new BattlePhaseProgressPolicy(32, 4),
                synchronizer,
                events));

    private static int Index(BattleEncounterResult result, BattleEncounterEventKind kind) =>
        result.Events.First(battleEvent => battleEvent.Kind == kind).Sequence;

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
            isActive: true);
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

    private sealed class RecordingLifecycle : IBattleEncounterLifecyclePort
    {
        public IReadOnlyList<ContentId> BattleStartTeamOrder { get; private set; } = [];
        public BattleTurnStartOutcome TurnStartOutcome { get; init; } = BattleTurnStartOutcome.CanAct;
        public BattleTurnStartRestriction? Restriction { get; init; }
        public Action<BattleEncounterLifecycleRequest>? BattleEndAction { get; init; }
        public int BattleStartCalls { get; private set; }
        public int TurnStartCalls { get; private set; }
        public int TurnEndCalls { get; private set; }
        public int BattleEndCalls { get; private set; }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            BattleStartCalls++;
            BattleStartTeamOrder = request.TeamOrder;
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            TurnStartCalls++;
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
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default) =>
            new(Array.Empty<BattleEncounterEvent>());

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            BattleEndCalls++;
            BattleEndAction?.Invoke(request);
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
        }
    }

    public enum ThrowingLifecycleStage
    {
        BattleStart,
        TurnStart,
        TurnEnd,
        PhaseEnd,
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
        ThrowingLifecycleStage.BattleEnd => "battle-end",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };

    private sealed class RecordingSynchronizer : IBattleEncounterStateSynchronizer
    {
        public int Calls { get; private set; }

        public void Synchronize(IReadOnlyList<BattleEncounterParticipant> participants) => Calls++;
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
