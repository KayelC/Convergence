using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class BattleStatusLifecycleTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Luck = ContentId.Parse("luck");
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId BattleStart = ContentId.Parse("battle_start");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");
    private static readonly ContentId PoisonFormula = ContentId.Parse("legacy_poison_damage");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId Poison = ContentId.Parse("poison");
    private static readonly ContentId Sleep = ContentId.Parse("sleep");

    [Fact]
    public void TurnStart_MapsDeterministicBehavioursAndClearsGuard()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState skip = Actor("skip");
        skip.SetGuarding(true);
        skip.ApplyAilment(Ailment("skip", new SkipAilmentTurnBehaviorDefinition()), Turns(3));
        RuntimeActorState limited = Actor("limited");
        limited.ApplyAilment(Ailment(
            "bind",
            new LimitedActionsAilmentTurnBehaviorDefinition([ContentId.Parse("basic_attack")])), Turns(3));
        RuntimeActorState forced = Actor("forced");
        forced.ApplyAilment(Ailment("rage", new ForcedBasicAttackAilmentTurnBehaviorDefinition()), Turns(3));
        RuntimeActorState confused = Actor("confused");
        confused.ApplyAilment(Ailment("charm", new ConfusedActionAilmentTurnBehaviorDefinition()), Turns(3));

        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(skip)).Outcome);
        Assert.False(skip.IsGuarding);
        Assert.Equal(BattleTurnStartOutcome.LimitedAction, service.ProcessTurnStart(new(limited)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.ForcedPhysical, service.ProcessTurnStart(new(forced)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.ForcedConfusion, service.ProcessTurnStart(new(confused)).Outcome);
    }

    [Fact]
    public void TurnStart_UsesDeterministicChanceSkipAndFearRolls()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(49, 50, 10, 10, 20, 60));
        RuntimeActorState panicSkip = Actor("panic_skip");
        panicSkip.ApplyAilment(Ailment("panic", new ChanceSkipAilmentTurnBehaviorDefinition(50)), Turns(3));
        RuntimeActorState panicAct = Actor("panic_act");
        panicAct.ApplyAilment(Ailment("panic", new ChanceSkipAilmentTurnBehaviorDefinition(50)), Turns(3));
        var fear = new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(40, 15, DemonFleeOutcome.ReturnToStock);
        RuntimeActorState demonFear = Actor("demon_fear");
        demonFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState humanFear = Actor("human_fear");
        humanFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState skipFear = Actor("skip_fear");
        skipFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState actFear = Actor("act_fear");
        actFear.ApplyAilment(Ailment("fear", fear), Turns(3));

        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(panicSkip)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.CanAct, service.ProcessTurnStart(new(panicAct)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.ReturnToStock, service.ProcessTurnStart(new(demonFear, true)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.FleeBattle, service.ProcessTurnStart(new(humanFear)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(skipFear, true)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.CanAct, service.ProcessTurnStart(new(actFear, true)).Outcome);
    }

    [Fact]
    public void TurnStart_CombinesAllAilmentsAndPreservesAllowedActionIds()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        ContentId attack = ContentId.Parse("basic_attack");
        ContentId skill = ContentId.Parse("skill");
        ContentId item = ContentId.Parse("item");
        var firstAllowed = new List<ContentId> { attack, skill };
        actor.ApplyAilment(
            IndependentAilment(
                "bind_a",
                new LimitedActionsAilmentTurnBehaviorDefinition(firstAllowed)),
            Turns(3));
        actor.ApplyAilment(
            IndependentAilment(
                "bind_b",
                new LimitedActionsAilmentTurnBehaviorDefinition([skill, item])),
            Turns(3));
        firstAllowed.Clear();

        BattleTurnStartLifecycleResult limited = service.ProcessTurnStart(new(actor));

        Assert.Equal(BattleTurnStartOutcome.LimitedAction, limited.Outcome);
        Assert.Equal([skill], limited.AllowedActionIds);
        Assert.Equal(
            [ContentId.Parse("bind_a"), ContentId.Parse("bind_b")],
            limited.Restriction.SourceAilmentIds);

        actor.ApplyAilment(
            IndependentAilment("stun", new SkipAilmentTurnBehaviorDefinition()),
            Turns(3));
        BattleTurnStartLifecycleResult skipped = service.ProcessTurnStart(new(actor));

        Assert.Equal(BattleTurnStartOutcome.Skip, skipped.Outcome);
        Assert.Empty(skipped.AllowedActionIds);
        Assert.Equal([ContentId.Parse("stun")], skipped.Restriction.SourceAilmentIds);
    }

    [Fact]
    public void TurnStart_CustomBehaviorUsesRegisteredHandlerAndNeverSilentlyFallsBack()
    {
        ContentId handlerId = ContentId.Parse("custom_restriction");
        ContentId actionId = ContentId.Parse("focus");
        var handler = new FixedCustomTurnBehaviorHandler(
            new CustomAilmentTurnBehaviorResult(BattleTurnStartOutcome.LimitedAction, [actionId]));
        var service = new BattleStatusLifecycleService(
            new SequenceRandomSource(),
            [new KeyValuePair<ContentId, ICustomAilmentTurnBehaviorHandler>(handlerId, handler)]);
        RuntimeActorState actor = Actor("actor");
        actor.ApplyAilment(
            IndependentAilment("custom", new CustomAilmentTurnBehaviorDefinition(handlerId)),
            Turns(3));

        BattleTurnStartLifecycleResult result = service.ProcessTurnStart(new(actor));

        Assert.Equal(BattleTurnStartOutcome.LimitedAction, result.Outcome);
        Assert.Equal([actionId], result.AllowedActionIds);
        Assert.Equal(1, handler.CallCount);

        var missingHandlerService = new BattleStatusLifecycleService(new SequenceRandomSource());
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => missingHandlerService.ProcessTurnStart(new(actor)));
        Assert.Contains(handlerId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("custom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TurnStart_ThrowingCustomBehaviorRollsBackGuardAndHandlerMutations()
    {
        ContentId handlerId = ContentId.Parse("mutate_then_throw");
        var handler = new MutatingThrowingTurnBehaviorHandler();
        var service = new BattleStatusLifecycleService(
            new SequenceRandomSource(),
            [new KeyValuePair<ContentId, ICustomAilmentTurnBehaviorHandler>(handlerId, handler)]);
        RuntimeActorState actor = Actor("atomic_turn_start", hp: 50);
        actor.SetGuarding(true);
        actor.ApplyAilment(
            IndependentAilment(
                "custom_atomic",
                new CustomAilmentTurnBehaviorDefinition(handlerId)),
            Turns(3));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => service.ProcessTurnStart(new BattleTurnStartLifecycleRequest(actor)));

        Assert.Equal("Deliberate turn-behavior failure.", exception.Message);
        Assert.True(actor.IsGuarding);
        Assert.Equal(50, actor.GetRequiredResource(Hp).Current);
        Assert.NotSame(actor, handler.ReceivedActor);
    }

    [Fact]
    public void AilmentApplication_EnforcesGuardImmunityChanceAndMajorExclusivity()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        BattleExecutionServices services = Services(new SequenceAilmentPolicy(false, true));
        RuntimeActorState guarded = Actor("guarded");
        guarded.SetGuarding(true);
        BattleAilmentApplicationResult guardedResult = service.TryApplyAilment(new(
            Actor("attacker"),
            guarded,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            100), services);
        var immuneDefense = new CombatDefenseProfile(
            ailmentResistances: [new KeyValuePair<ContentId, ResistanceLevel>(Poison, ResistanceLevel.Immune)]);
        RuntimeActorState immune = Actor("immune", defense: immuneDefense);
        BattleAilmentApplicationResult immuneResult = service.TryApplyAilment(new(
            Actor("attacker"),
            immune,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            100), services);
        RuntimeActorState target = Actor("target");
        target.ApplyAilment(Ailment("sleep", new SkipAilmentTurnBehaviorDefinition()), Turns(3));
        BattleAilmentApplicationResult missed = service.TryApplyAilment(new(
            Actor("attacker"),
            target,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            50), services);
        BattleAilmentApplicationResult applied = service.TryApplyAilment(new(
            Actor("attacker"),
            target,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            50), services);

        Assert.Equal(BattleAilmentApplicationStatus.GuardBlocked, guardedResult.Status);
        Assert.Equal(BattleAilmentApplicationStatus.Immune, immuneResult.Status);
        Assert.Equal(BattleAilmentApplicationStatus.Missed, missed.Status);
        Assert.True(applied.Applied);
        Assert.False(target.HasAilment(Sleep));
        Assert.True(target.HasAilment(Poison));
    }

    [Fact]
    public void TurnEnd_AppliesLethalPoisonSleepRecoveryNaturalRecoveryAndDurationTicks()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(0));
        BattleExecutionServices services = Services();
        RuntimeActorState poisoned = Actor("poisoned", hp: 1);
        poisoned.ApplyAilment(PoisonAilment(), Turns(3));
        RuntimeActorState sleeping = Actor("sleeping", hp: 50, sp: 40);
        sleeping.ApplyAilment(SleepAilment(), Turns(3));
        RuntimeActorState recovering = Actor("recovering", luck: 40);
        recovering.ApplyAilment(Ailment(
            "fear",
            new NormalAilmentTurnBehaviorDefinition(),
            recovery: new AilmentRecoveryDefinition(new NaturalAilmentRecoveryDefinition(20, Luck, 0.5m))),
            Turns(3));

        BattleTurnEndLifecycleResult poisonResult = service.ProcessTurnEnd(
            new(poisoned, [poisoned], Battle, OwnerTurnEnd),
            services);
        BattleTurnEndLifecycleResult sleepResult = service.ProcessTurnEnd(
            new(sleeping, [sleeping], Battle, OwnerTurnEnd),
            services);
        service.ProcessTurnEnd(new(recovering, [recovering], Battle, OwnerTurnEnd), services);

        Assert.Equal(0, poisoned.GetRequiredResource(Hp).Current);
        Assert.True(poisoned.IsDefeated);
        Assert.Equal(2, Assert.Single(poisoned.Ailments).Value.Duration is TurnDurationDefinition turns ? turns.Value : 0);
        Assert.Contains(poisonResult.Events, ev => ev.Kind == BattleStatusLifecycleEventKind.ResourceChanged && ev.Value < 0);
        Assert.Equal(60, sleeping.GetRequiredResource(Hp).Current);
        Assert.Equal(50, sleeping.GetRequiredResource(Sp).Current);
        Assert.Contains(sleepResult.Events, ev => ev.Kind == BattleStatusLifecycleEventKind.ResourceChanged && ev.RelatedId == Hp);
        Assert.False(recovering.HasAilment(ContentId.Parse("fear")));
    }

    [Fact]
    public void TurnEnd_NaturalRecoverySaturatesAtTheRuntimeStatBoundary()
    {
        RuntimeActorState actor = Actor(
            "boundary_recovery",
            luck: RuntimeActorNumericDomain.MaximumStatValue);
        ContentId ailmentId = ContentId.Parse("boundary_ailment");
        actor.ApplyAilment(
            Ailment(
                ailmentId.ToString(),
                new NormalAilmentTurnBehaviorDefinition(),
                recovery: new AilmentRecoveryDefinition(
                    new NaturalAilmentRecoveryDefinition(20, Luck, decimal.MaxValue))),
            Turns(3));

        BattleTurnEndLifecycleResult result = new BattleStatusLifecycleService(
            new SequenceRandomSource(99)).ProcessTurnEnd(
                new(actor, [actor], Battle, OwnerTurnEnd),
                Services());

        Assert.False(actor.HasAilment(ailmentId));
        BattleStatusLifecycleEvent recovered = Assert.Single(
            result.Events,
            item => item.Kind == BattleStatusLifecycleEventKind.AilmentRecovered);
        Assert.Equal(100m, recovered.Value);
    }

    [Fact]
    public void TurnEnd_SuspendsReserveActorTicksDamageAndRecovery()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(0));
        RuntimeActorState reserve = Actor("reserve", hp: 50, isActive: false);
        reserve.ApplyAilment(PoisonAilment(), Turns(3));

        BattleTurnEndLifecycleResult result = service.ProcessTurnEnd(
            new(reserve, [reserve], Battle, OwnerTurnEnd),
            Services());

        Assert.Empty(result.Events);
        Assert.Equal(50, reserve.GetRequiredResource(Hp).Current);
        Assert.Equal(3, Assert.Single(reserve.Ailments).Value.Duration is TurnDurationDefinition turns ? turns.Value : 0);
    }

    [Fact]
    public void TurnEnd_EvaluatesAilmentTriggerConditions()
    {
        RuntimeActorState actor = Actor("actor", hp: 50);
        actor.ApplyAilment(
            IndependentAilment(
                "false_trigger",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(30))],
                    new ResourcePercentageConditionDefinition(
                        ConditionSubject.Actor,
                        Hp,
                        NumericComparison.GreaterThan,
                        80))]),
            Turns(3));
        actor.ApplyAilment(
            IndependentAilment(
                "true_trigger",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(15))],
                    new ResourcePercentageConditionDefinition(
                        ConditionSubject.Actor,
                        Hp,
                        NumericComparison.LessThan,
                        80))]),
            Turns(3));

        BattleTurnEndLifecycleResult result = new BattleStatusLifecycleService(new SequenceRandomSource())
            .ProcessTurnEnd(new(actor, [actor], Battle, OwnerTurnEnd), Services());

        Assert.Equal(65, actor.GetRequiredResource(Hp).Current);
        Assert.Single(result.Events, item =>
            item.Kind == BattleStatusLifecycleEventKind.ResourceChanged && item.Value == 15);
    }

    [Fact]
    public void TurnEnd_UsesSharedStopTargetAndStopActionSemantics()
    {
        ContentId failingHandlerId = ContentId.Parse("always_fail");
        BattleExecutionServices services = Services(
            customEffectHandlers:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    failingHandlerId,
                    new FailingCustomEffectHandler())
            ]);
        RuntimeActorState stopTarget = Actor("stop_target", hp: 50);
        stopTarget.ApplyAilment(
            IndependentAilment(
                "first",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [
                        new CustomEffectDefinition(
                            failingHandlerId,
                            onFailure: EffectFailurePolicy.StopTarget),
                        new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(30))
                    ])]),
            Turns(3));
        stopTarget.ApplyAilment(
            IndependentAilment(
                "second",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))])]),
            Turns(3));

        new BattleStatusLifecycleService(new SequenceRandomSource()).ProcessTurnEnd(
            new(stopTarget, [stopTarget], Battle, OwnerTurnEnd),
            services);

        Assert.Equal(60, stopTarget.GetRequiredResource(Hp).Current);

        RuntimeActorState stopAction = Actor("stop_action", hp: 50);
        stopAction.ApplyAilment(
            IndependentAilment(
                "first",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new CustomEffectDefinition(
                        failingHandlerId,
                        onFailure: EffectFailurePolicy.StopAction)])]),
            Turns(3));
        stopAction.ApplyAilment(
            IndependentAilment(
                "second",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))])]),
            Turns(3));

        new BattleStatusLifecycleService(new SequenceRandomSource()).ProcessTurnEnd(
            new(stopAction, [stopAction], Battle, OwnerTurnEnd),
            services);

        Assert.Equal(50, stopAction.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void TurnEnd_ThrowingAilmentHandlerRollsBackEffectsAndDurationTicks()
    {
        ContentId handlerId = ContentId.Parse("mutate_then_throw");
        RuntimeActorState actor = Actor("atomic_turn_end", hp: 50);
        actor.ApplyAilment(
            IndependentAilment(
                "atomic_trigger",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [
                        new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10)),
                        new CustomEffectDefinition(handlerId)
                    ])]),
            Turns(3));
        BattleExecutionServices services = Services(
            customEffectHandlers:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    handlerId,
                    new MutatingThrowingCustomEffectHandler())
            ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new BattleStatusLifecycleService(new SequenceRandomSource()).ProcessTurnEnd(
                new BattleTurnEndLifecycleRequest(actor, [actor], Battle, OwnerTurnEnd),
                services));

        Assert.Equal("Deliberate lifecycle-effect failure.", exception.Message);
        Assert.Equal(50, actor.GetRequiredResource(Hp).Current);
        ActiveAilmentState active = Assert.Single(actor.Ailments).Value;
        Assert.Equal(3, Assert.IsType<TurnDurationDefinition>(active.Duration).Value);
    }

    [Fact]
    public async Task BattleStartPort_ThrowingLaterHandlerRollsBackEarlierActorsAndActivations()
    {
        ContentId handlerId = ContentId.Parse("mutate_then_throw");
        SkillDefinition restore = PassiveSkill(
            "battle_start_restore",
            [new PassiveTriggerDefinition(
                BattleStart,
                [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))])]);
        SkillDefinition fail = PassiveSkill(
            "battle_start_failure",
            [new PassiveTriggerDefinition(
                BattleStart,
                [new CustomEffectDefinition(handlerId)])]);
        RuntimeActorState first = Actor("first_atomic_start", hp: 50, passiveSkills: [restore]);
        RuntimeActorState second = Actor("second_atomic_start", hp: 50, passiveSkills: [fail]);
        BattleExecutionServices services = Services(
            customEffectHandlers:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    handlerId,
                    new MutatingThrowingCustomEffectHandler())
            ]);
        var lifecycle = new BattleStatusLifecycleService(new SequenceRandomSource());
        var port = new BattleStatusEncounterLifecyclePort(lifecycle, services, BattleStart, OwnerTurnEnd);
        BattleEncounterParticipant[] participants =
        [
            new(first, "First"),
            new(second, "Second")
        ];
        var encounter = new BattleEncounterRequest(participants, Battle, NormalBattle, null, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => port.ProcessBattleStartAsync(
                new BattleEncounterLifecycleRequest(encounter, participants, [PlayerTeam]))
            .AsTask());

        Assert.Equal(50, first.GetRequiredResource(Hp).Current);
        Assert.Equal(50, second.GetRequiredResource(Hp).Current);
        Assert.Empty(first.ToSnapshot().BattleActivations.PassiveActivations);
        Assert.Empty(second.ToSnapshot().BattleActivations.PassiveActivations);
    }

    [Fact]
    public void StatStages_SaturateAtApprovedBoundsWithoutOverflow()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        ContentId attack = ContentId.Parse("attack");

        BattleStatusLifecycleResult raised = service.ApplyStatStage(actor, attack, int.MaxValue);
        BattleStatusLifecycleResult unchanged = service.ApplyStatStage(actor, attack, int.MaxValue);
        BattleStatusLifecycleResult lowered = service.ApplyStatStage(actor, attack, int.MinValue);

        Assert.Equal(BattleStatStageRange.Minimum, actor.StatStages[attack].Stage);
        Assert.Equal(4, Assert.Single(raised.Events).Value);
        Assert.Equal(0, Assert.Single(unchanged.Events).Value);
        Assert.Equal(-8, Assert.Single(lowered.Events).Value);
    }

    [Fact]
    public void Cleanup_ClearsTransientAndEncounterStatusesWithoutRemovingAilments()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        actor.SetGuarding(true);
        actor.GrantShield(ShieldKind.Physical, Turns(1));
        actor.GrantCharge(ChargeKind.Physical, 2, Turns(1));
        actor.ChangeStatStage(ContentId.Parse("attack"), 1, Turns(1));
        actor.BreakAffinity(DamageElement.Fire, Turns(1));
        actor.OverrideAffinity(DamageElement.Fire, ElementalAffinity.Null, Turns(1));
        actor.AddOtherStatus(ContentId.Parse("marked"), Turns(1));
        actor.ApplyAilment(PoisonAilment(), Turns(3));

        service.Cleanup(new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.Swap));

        Assert.False(actor.IsGuarding);
        Assert.Empty(actor.Shields);
        Assert.Empty(actor.Charges);
        Assert.NotEmpty(actor.StatStages);
        Assert.NotEmpty(actor.AffinityBreaks);
        Assert.NotEmpty(actor.AffinityOverrides);
        Assert.NotEmpty(actor.OtherStatuses);
        Assert.True(actor.HasAilment(Poison));

        service.Cleanup(new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.BattleEnd));

        Assert.Empty(actor.StatStages);
        Assert.Empty(actor.AffinityBreaks);
        Assert.Empty(actor.AffinityOverrides);
        Assert.Empty(actor.OtherStatuses);
        Assert.True(actor.HasAilment(Poison));
    }

    [Fact]
    public void ActionEnd_ExpiresInstantDurationsAcrossEveryStateFamily()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        SeedDurationStates(actor, new InstantDurationDefinition(), "instant");
        var participants = new List<RuntimeActorState> { actor, actor };
        var request = new BattleActionEndLifecycleRequest(participants);
        participants.Clear();

        BattleStatusLifecycleResult result = service.ProcessActionEnd(request);

        Assert.Single(request.Participants);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeActorState>)request.Participants).Add(actor));
        AssertNoDurationStates(actor, "instant");
        Assert.Equal(7, result.Events.Count);
        Assert.Equal(1, result.Events.Count(status =>
            status.Kind == BattleStatusLifecycleEventKind.AilmentExpired));
        Assert.Equal(6, result.Events.Count(status =>
            status.Kind == BattleStatusLifecycleEventKind.StatusExpired));
        Assert.All(result.Events, status => Assert.Contains("Instant", status.Detail));
    }

    [Fact]
    public void TurnDurations_TickEveryStateFamilyOnlyOnTheirAuthoredEvent()
    {
        RuntimeActorState actor = Actor("actor");
        SeedDurationStates(actor, new TurnDurationDefinition(1, OwnerTurnEnd, false), "turn");

        Assert.Empty(actor.TickAilmentDurations(ContentId.Parse("other_event")));
        Assert.Empty(actor.TickTimedStatuses(ContentId.Parse("other_event")));

        BattleDurationTickResult[] ticks = actor.TickAilmentDurations(OwnerTurnEnd)
            .Concat(actor.TickTimedStatuses(OwnerTurnEnd))
            .ToArray();

        AssertNoDurationStates(actor, "turn");
        Assert.Equal(7, ticks.Length);
        Assert.Equal(Enum.GetValues<BattleDurationStateKind>(),
            ticks.Select(tick => tick.StateKind).Order().ToArray());
        Assert.All(ticks, tick => Assert.True(tick.Expired));
    }

    [Fact]
    public void PhaseEnd_ExpiresOnlyTheMatchingPhaseAcrossEveryStateFamily()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        ContentId playerPhase = ContentId.Parse("player_phase");
        RuntimeActorState matching = Actor("matching");
        RuntimeActorState other = Actor("other");
        RuntimeActorState permanent = Actor("permanent");
        SeedDurationStates(matching, new PhaseDurationDefinition(playerPhase), "matching");
        SeedDurationStates(other, new PhaseDurationDefinition(ContentId.Parse("enemy_phase")), "other");
        SeedDurationStates(permanent, new PermanentDurationDefinition(), "permanent");

        BattleStatusLifecycleResult result = service.ProcessPhaseEnd(
            new BattlePhaseEndLifecycleRequest([matching, other, permanent], playerPhase));

        AssertNoDurationStates(matching, "matching");
        AssertAllDurationStatesPresent(other, "other");
        AssertAllDurationStatesPresent(permanent, "permanent");
        Assert.Equal(7, result.Events.Count);
        Assert.All(result.Events, status => Assert.Contains("Phase", status.Detail));
    }

    [Fact]
    public void BattleCleanup_ExpiresBattleStateAndPreservesEveryPermanentStateFamily()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState battle = Actor("battle_actor");
        RuntimeActorState permanent = Actor("permanent_actor");
        SeedDurationStates(battle, new BattleDurationDefinition(), "battle");
        SeedDurationStates(permanent, new PermanentDurationDefinition(), "permanent");
        AilmentDefinition turnAilment = IndependentAilment(
            "turn_ailment",
            new NormalAilmentTurnBehaviorDefinition());
        permanent.ApplyAilment(turnAilment, Turns(2));

        service.Cleanup(new BattleStatusCleanupRequest(battle, BattleStatusCleanupScope.BattleEnd));
        service.Cleanup(new BattleStatusCleanupRequest(permanent, BattleStatusCleanupScope.BattleEnd));

        AssertNoDurationStates(battle, "battle");
        AssertAllDurationStatesPresent(permanent, "permanent");
        Assert.True(permanent.HasAilment(turnAilment.Id));
    }

    [Fact]
    public void SwapCleanup_PreservesPermanentChargesAndShields()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        actor.SetGuarding(true);
        actor.GrantCharge(ChargeKind.Physical, 2m, new PermanentDurationDefinition());
        actor.GrantShield(ShieldKind.Physical, new PermanentDurationDefinition());

        service.Cleanup(new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.Swap));

        Assert.False(actor.IsGuarding);
        Assert.Contains(ChargeKind.Physical, actor.Charges.Keys);
        Assert.Contains(ShieldKind.Physical, actor.Shields.Keys);
    }

    [Fact]
    public void AffinityBreakDuration_SuspendsInReserveAndExpiresOnItsAuthoredTick()
    {
        RuntimeActorState actor = Actor("actor", isActive: false);
        actor.BreakAffinity(DamageElement.Ice, Turns(1));

        Assert.Empty(actor.TickTimedStatuses(OwnerTurnEnd));
        Assert.True(actor.AffinityBreaks.ContainsKey(DamageElement.Ice));

        actor.IsActive = true;
        BattleDurationTickResult tick = Assert.Single(actor.TickTimedStatuses(OwnerTurnEnd));

        Assert.True(tick.Expired);
        Assert.Empty(actor.AffinityBreaks);
    }

    [Fact]
    public void StatusLifecycleDemoPack_LoadsTheElevenLegacyAilments()
    {
        string root = FindRepositoryRoot();
        string jsonRoot = Path.Combine(root, "Data", "Jsons");
        string manifestName = "status_lifecycle_demo.manifest.json";
        string ailmentName = "status_lifecycle_demo.ailments.json";
        var bundle = new ContentPackTextBundle(
            manifestName,
            File.ReadAllText(Path.Combine(jsonRoot, manifestName)),
            [new ContentDocumentText(ailmentName, ailmentName, File.ReadAllText(Path.Combine(jsonRoot, ailmentName)))]);

        GameDataCatalog catalog = new SkillSystemCatalogLoader()
            .Load(new SkillSystemCatalogLoadRequest(Registrations(), [bundle]))
            .RequireCatalog();

        Assert.Equal(11, catalog.Ailments.Count);
        Assert.Contains(ContentId.Parse("convergence.status_lifecycle_demo:poison"), catalog.Ailments.Keys);
        Assert.IsType<ChanceSkipOrFleeAilmentTurnBehaviorDefinition>(
            catalog.GetRequiredAilment(ContentId.Parse("convergence.status_lifecycle_demo:fear")).TurnBehavior);
        Assert.IsType<LimitedActionsAilmentTurnBehaviorDefinition>(
            catalog.GetRequiredAilment(ContentId.Parse("convergence.status_lifecycle_demo:bind")).TurnBehavior);
        Assert.Contains(
            catalog.GetRequiredAilment(ContentId.Parse("convergence.status_lifecycle_demo:sleep")).Triggers,
            trigger => trigger.EventId == OwnerTurnEnd);
    }

    private static RuntimeActorState Actor(
        string id,
        decimal hp = 100,
        decimal sp = 100,
        decimal luck = 10,
        CombatDefenseProfile? defense = null,
        bool isActive = true,
        IEnumerable<SkillDefinition>? passiveSkills = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            PlayerTeam,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 100)],
            [new KeyValuePair<ContentId, decimal>(Luck, luck)],
            isActive: isActive,
            passiveSkills: passiveSkills);

    private static SkillDefinition PassiveSkill(
        string id,
        IEnumerable<PassiveTriggerDefinition> triggers) =>
        new(
            ContentId.Parse(id),
            id,
            "Test passive.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers: triggers);

    private static TurnDurationDefinition Turns(int value) =>
        new(value, OwnerTurnEnd, true);

    private static void SeedDurationStates(
        RuntimeActorState actor,
        DurationDefinition duration,
        string suffix)
    {
        actor.ApplyAilment(
            IndependentAilment(
                $"ailment_{suffix}",
                new NormalAilmentTurnBehaviorDefinition()),
            duration);
        actor.ChangeStatStage(ContentId.Parse($"stat_{suffix}"), 1, duration);
        actor.GrantCharge(ChargeKind.Physical, 2m, duration);
        actor.GrantShield(ShieldKind.Physical, duration);
        actor.OverrideAffinity(DamageElement.Ice, ElementalAffinity.Resist, duration);
        actor.BreakAffinity(DamageElement.Fire, duration);
        actor.AddOtherStatus(ContentId.Parse($"status_{suffix}"), duration);
    }

    private static void AssertAllDurationStatesPresent(RuntimeActorState actor, string suffix)
    {
        Assert.True(actor.HasAilment(ContentId.Parse($"ailment_{suffix}")));
        Assert.Contains(ContentId.Parse($"stat_{suffix}"), actor.StatStages.Keys);
        Assert.Contains(ChargeKind.Physical, actor.Charges.Keys);
        Assert.Contains(ShieldKind.Physical, actor.Shields.Keys);
        Assert.Contains(DamageElement.Ice, actor.AffinityOverrides.Keys);
        Assert.Contains(DamageElement.Fire, actor.AffinityBreaks.Keys);
        Assert.Contains(ContentId.Parse($"status_{suffix}"), actor.OtherStatuses);
    }

    private static void AssertNoDurationStates(RuntimeActorState actor, string suffix)
    {
        Assert.False(actor.HasAilment(ContentId.Parse($"ailment_{suffix}")));
        Assert.DoesNotContain(ContentId.Parse($"stat_{suffix}"), actor.StatStages.Keys);
        Assert.DoesNotContain(ChargeKind.Physical, actor.Charges.Keys);
        Assert.DoesNotContain(ShieldKind.Physical, actor.Shields.Keys);
        Assert.DoesNotContain(DamageElement.Ice, actor.AffinityOverrides.Keys);
        Assert.DoesNotContain(DamageElement.Fire, actor.AffinityBreaks.Keys);
        Assert.DoesNotContain(ContentId.Parse($"status_{suffix}"), actor.OtherStatuses);
    }

    private static AilmentDefinition Ailment(
        string id,
        AilmentTurnBehaviorDefinition behavior,
        AilmentRecoveryDefinition? recovery = null,
        IEnumerable<PassiveTriggerDefinition>? triggers = null) =>
        new(
            ContentId.Parse(id),
            id,
            "Test ailment.",
            Turns(3),
            behavior,
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            recovery ?? new AilmentRecoveryDefinition(),
            [ContentId.Parse("major_ailment")],
            ContentId.Parse("major_ailment"),
            triggers);

    private static AilmentDefinition PoisonAilment() =>
        Ailment(
            "poison",
            new NormalAilmentTurnBehaviorDefinition(),
            triggers:
            [
                new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new ReduceResourceEffectDefinition(Hp, new FormulaAmountDefinition(PoisonFormula), true)])
            ]);

    private static AilmentDefinition SleepAilment() =>
        Ailment(
            "sleep",
            new SkipAilmentTurnBehaviorDefinition(),
            triggers:
            [
                new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [
                        new RestoreResourceEffectDefinition(Hp, new PercentMaximumAmountDefinition(10)),
                        new RestoreResourceEffectDefinition(Sp, new PercentMaximumAmountDefinition(10))
                    ])
            ]);

    private static AilmentDefinition IndependentAilment(
        string id,
        AilmentTurnBehaviorDefinition behavior,
        IEnumerable<PassiveTriggerDefinition>? triggers = null) =>
        new(
            ContentId.Parse(id),
            id,
            "Independent test ailment.",
            Turns(3),
            behavior,
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            new AilmentRecoveryDefinition(),
            triggers: triggers);

    private static BattleExecutionServices Services(
        IAilmentApplicationPolicy? ailmentPolicy = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffectHandlers = null) =>
        new(
            new EmptyAilments(),
            new NoDamagePolicy(),
            new NoInstantDeathPolicy(),
            ailmentPolicy ?? new AlwaysAilmentPolicy(),
            new AlwaysChancePolicy(),
            new ZeroPowerPolicy(),
            new FirstTargetPolicy(),
            formulaHandlers:
            [
                new KeyValuePair<ContentId, IFormulaAmountHandler>(
                    PoisonFormula,
                    new PoisonFormulaHandler())
            ],
            customEffectHandlers: customEffectHandlers);

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterResource("hp", "sp")
            .RegisterStat("luck")
            .RegisterEvent("owner_turn_end")
            .RegisterAction("basic_attack", "guard", "pass")
            .RegisterAilmentGroup("major_ailment", "poison", "immobilize", "mental")
            .RegisterFormula("legacy_poison_damage", new AcceptingParameterValidator())
            .SupportEffect<ReduceResourceEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<LimitedActionsAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipOrFleeAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ForcedBasicAttackAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ConfusedActionAilmentTurnBehaviorDefinition>()
            .Build();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
    }

    private sealed class SequenceRandomSource(params int[] values) : IRandomSource
    {
        private readonly Queue<int> _values = new(values);

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            int value = _values.Count == 0 ? minimumInclusive : _values.Dequeue();
            return Math.Clamp(value, minimumInclusive, maximumExclusive - 1);
        }

        public decimal NextUnitDecimal() => NextInt32(0, 100) / 100m;
    }

    private sealed class PoisonFormulaHandler : IFormulaAmountHandler
    {
        public decimal Resolve(FormulaAmountDefinition amount, AmountResolutionContext context) =>
            Math.Max(1, Math.Floor(context.Target.GetRequiredResource(Hp).Maximum * 0.13m));
    }

    private sealed class EmptyAilments : IAilmentDefinitionRepository
    {
        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) => throw new KeyNotFoundException();
    }

    private sealed class NoDamagePolicy : IDamageExecutionPolicy
    {
        public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request) => [];
    }

    private sealed class NoInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
    }

    private sealed class AlwaysAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => true;
    }

    private sealed class SequenceAilmentPolicy(params bool[] results) : IAilmentApplicationPolicy
    {
        private readonly Queue<bool> _results = new(results);

        public bool ShouldApply(AilmentApplicationPolicyRequest request) =>
            _results.Count > 0 && _results.Dequeue();
    }

    private sealed class FixedCustomTurnBehaviorHandler(CustomAilmentTurnBehaviorResult result)
        : ICustomAilmentTurnBehaviorHandler
    {
        public int CallCount { get; private set; }

        public CustomAilmentTurnBehaviorResult Resolve(
            CustomAilmentTurnBehaviorDefinition behavior,
            CustomAilmentTurnBehaviorRequest request)
        {
            CallCount++;
            return result;
        }
    }

    private sealed class MutatingThrowingTurnBehaviorHandler : ICustomAilmentTurnBehaviorHandler
    {
        public RuntimeActorState? ReceivedActor { get; private set; }

        public CustomAilmentTurnBehaviorResult Resolve(
            CustomAilmentTurnBehaviorDefinition behavior,
            CustomAilmentTurnBehaviorRequest request)
        {
            ReceivedActor = request.Actor;
            request.Actor.SetResource(Hp, 1);
            throw new InvalidOperationException("Deliberate turn-behavior failure.");
        }
    }

    private sealed class FailingCustomEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(
            CustomEffectDefinition effect,
            EffectExecutionContext context) =>
            new(context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Failure);
    }

    private sealed class MutatingThrowingCustomEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(
            CustomEffectDefinition effect,
            EffectExecutionContext context)
        {
            (context.Target ?? context.Actor).SetResource(Hp, 1);
            throw new InvalidOperationException("Deliberate lifecycle-effect failure.");
        }
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => true;
    }

    private sealed class ZeroPowerPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => 0;
    }

    private sealed class FirstTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            candidates.Take(count.Maximum).ToArray();
    }

    private sealed class AcceptingParameterValidator : IContentParameterValidator
    {
        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) => [];
    }
}
