using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Battle;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class BattleStatusLifecycleTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Luck = ContentId.Parse("luck");
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId BattleStart = ContentId.Parse("battle_start");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");
    private static readonly ContentId PoisonFormula = ContentId.Parse("reference_poison_damage");
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
        var fear = new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(40, 15, CompanionFleeOutcome.RecallToRoster);
        RuntimeActorState companionFear = Actor("companion_fear");
        companionFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState humanFear = Actor("human_fear");
        humanFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState skipFear = Actor("skip_fear");
        skipFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState actFear = Actor("act_fear");
        actFear.ApplyAilment(Ailment("fear", fear), Turns(3));

        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(panicSkip)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.CanAct, service.ProcessTurnStart(new(panicAct)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.RecallToRoster, service.ProcessTurnStart(new(companionFear, true)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.FleeBattle, service.ProcessTurnStart(new(humanFear)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(skipFear, true)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.CanAct, service.ProcessTurnStart(new(actFear, true)).Outcome);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void TurnStart_RejectsInvalidAuthoredChanceBeforeRandomnessAndRollsBack(int chance)
    {
        var service = new BattleStatusLifecycleService(new ThrowingRandomSource());
        RuntimeActorState actor = Actor("invalid_chance");
        actor.SetGuarding(true);
        actor.ApplyAilment(
            Ailment("invalid_chance_ailment", new ChanceSkipAilmentTurnBehaviorDefinition(chance)),
            Turns(3));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ProcessTurnStart(new(actor)));

        Assert.True(actor.IsGuarding);
        Assert.True(actor.HasAilment(ContentId.Parse("invalid_chance_ailment")));
    }

    [Fact]
    public void TurnStart_RejectsCombinedFleeAndSkipChanceAboveOneHundred()
    {
        var service = new BattleStatusLifecycleService(new ThrowingRandomSource());
        RuntimeActorState actor = Actor("invalid_combined_chance");
        actor.ApplyAilment(
            Ailment(
                "invalid_combined_chance_ailment",
                new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(
                    60,
                    50,
                    CompanionFleeOutcome.RecallToRoster)),
            Turns(3));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ProcessTurnStart(new(actor)));

        Assert.True(actor.HasAilment(ContentId.Parse("invalid_combined_chance_ailment")));
    }

    [Fact]
    public void TurnStart_ZeroAndOneHundredPercentDoNotDrawRandomness()
    {
        var service = new BattleStatusLifecycleService(new ThrowingRandomSource());
        RuntimeActorState zero = Actor("zero_chance");
        zero.ApplyAilment(
            Ailment("zero_chance_ailment", new ChanceSkipAilmentTurnBehaviorDefinition(0)),
            Turns(3));
        RuntimeActorState guaranteed = Actor("guaranteed_chance");
        guaranteed.ApplyAilment(
            Ailment("guaranteed_chance_ailment", new ChanceSkipAilmentTurnBehaviorDefinition(100)),
            Turns(3));

        Assert.Equal(BattleTurnStartOutcome.CanAct, service.ProcessTurnStart(new(zero)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(guaranteed)).Outcome);
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
    public void AilmentApplication_DefaultPolicyReportsNewRefreshAndExclusiveReplacement()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        BattleExecutionServices services = Services();
        RuntimeActorState attacker = Actor("transition_attacker");
        RuntimeActorState target = Actor("transition_target");
        AilmentDefinition poison = Ailment("poison", new NormalAilmentTurnBehaviorDefinition());
        AilmentDefinition sleep = Ailment("sleep", new SkipAilmentTurnBehaviorDefinition());
        ContentId sourceId = ContentId.Parse("status_skill");

        BattleAilmentApplicationResult applied = service.TryApplyAilment(
            new(attacker, target, poison, 100, Turns(2)) { SourceId = sourceId },
            services);
        BattleAilmentApplicationResult refreshed = service.TryApplyAilment(
            new(attacker, target, poison, 100, Turns(4)) { SourceId = sourceId },
            services);
        BattleAilmentApplicationResult replaced = service.TryApplyAilment(
            new(attacker, target, sleep, 100, Turns(3)) { SourceId = sourceId },
            services);

        Assert.Equal(BattleAilmentTransitionOutcome.Applied, applied.Transition!.Outcome);
        BattleAilmentStateChange added = Assert.Single(applied.Transition.StateChanges);
        Assert.Equal(BattleAilmentStateChangeKind.Added, added.Kind);
        Assert.Null(added.Before);
        Assert.Equal(Turns(2), added.After);
        BattleStatusLifecycleEvent appliedEvent = Assert.Single(applied.Events);
        Assert.Equal(BattleStatusLifecycleEventKind.AilmentApplied, appliedEvent.Kind);
        Assert.Equal(attacker.InstanceId, appliedEvent.SourceActorId);
        Assert.Equal(sourceId, appliedEvent.SourceId);

        Assert.Equal(BattleAilmentTransitionOutcome.Refreshed, refreshed.Transition!.Outcome);
        BattleAilmentStateChange refresh = Assert.Single(refreshed.Transition.StateChanges);
        Assert.Equal(BattleAilmentStateChangeKind.Refreshed, refresh.Kind);
        Assert.Equal(Turns(2), refresh.Before);
        Assert.Equal(Turns(4), refresh.After);
        Assert.Equal(
            BattleStatusLifecycleEventKind.AilmentRefreshed,
            Assert.Single(refreshed.Events).Kind);

        Assert.Equal(BattleAilmentTransitionOutcome.Replaced, replaced.Transition!.Outcome);
        Assert.Equal([Poison, Sleep], replaced.Transition.AffectedAilmentIds);
        Assert.Equal(
            [BattleAilmentStateChangeKind.Removed, BattleAilmentStateChangeKind.Added],
            replaced.Transition.StateChanges.Select(change => change.Kind));
        Assert.Equal(
            [BattleStatusLifecycleEventKind.AilmentRemoved, BattleStatusLifecycleEventKind.AilmentReplaced],
            replaced.Events.Select(statusEvent => statusEvent.Kind));
        Assert.Equal(
            StatusRemovalCause.ExclusivityReplacement,
            replaced.Events[0].RemovalTransition!.Cause);
        Assert.All(replaced.Events, statusEvent =>
        {
            Assert.Same(replaced.Transition, statusEvent.AilmentTransition);
            Assert.Equal(attacker.InstanceId, statusEvent.SourceActorId);
            Assert.Equal(sourceId, statusEvent.SourceId);
        });
        Assert.False(target.HasAilment(Poison));
        Assert.True(target.HasAilment(Sleep));
    }

    [Fact]
    public void AilmentApplication_SuppliedPoliciesRejectWithoutMutationAndProtectReplacement()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState attacker = Actor("policy_attacker");
        AilmentDefinition poison = Ailment("poison", new NormalAilmentTurnBehaviorDefinition());
        AilmentDefinition sleep = Ailment("sleep", new SkipAilmentTurnBehaviorDefinition());

        RuntimeActorState rejectSame = Actor("reject_same");
        rejectSame.ApplyAilment(poison, Turns(2));
        BattleAilmentApplicationResult sameResult = service.TryApplyAilment(
            new(attacker, rejectSame, poison, 100, Turns(5)),
            Services(ailmentTransitions: RejectExistingAilmentTransitionPolicy.Instance));

        RuntimeActorState rejectExclusive = Actor("reject_exclusive");
        rejectExclusive.ApplyAilment(poison, Turns(2));
        BattleAilmentApplicationResult exclusiveResult = service.TryApplyAilment(
            new(attacker, rejectExclusive, sleep, 100, Turns(5)),
            Services(ailmentTransitions: RefreshExistingAilmentTransitionPolicy.Instance));

        RuntimeActorState protectedTarget = Actor("protected_replacement");
        protectedTarget.ApplyAilment(
            poison,
            new StatusLifetimeDefinition(TurnClock(2), StatusRemovalProfiles.Protected));
        BattleAilmentApplicationResult protectedResult = service.TryApplyAilment(
            new(attacker, protectedTarget, sleep, 100, Turns(5)),
            Services(ailmentTransitions: ReplaceExclusiveAilmentTransitionPolicy.Instance));

        AssertRejected(
            sameResult,
            BattleAilmentTransitionRejectionReason.SameAilmentAlreadyActive,
            rejectSame,
            Poison,
            Turns(2));
        AssertRejected(
            exclusiveResult,
            BattleAilmentTransitionRejectionReason.ExclusiveAilmentActive,
            rejectExclusive,
            Poison,
            Turns(2));
        AssertRejected(
            protectedResult,
            BattleAilmentTransitionRejectionReason.ReplacementProtected,
            protectedTarget,
            Poison,
            new StatusLifetimeDefinition(TurnClock(2), StatusRemovalProfiles.Protected));

        static void AssertRejected(
            BattleAilmentApplicationResult result,
            BattleAilmentTransitionRejectionReason reason,
            RuntimeActorState target,
            ContentId retainedId,
            StatusLifetimeDefinition retainedLifetime)
        {
            Assert.Equal(BattleAilmentApplicationStatus.TransitionRejected, result.Status);
            Assert.Equal(BattleAilmentTransitionOutcome.Rejected, result.Transition!.Outcome);
            Assert.Equal(reason, result.Transition.RejectionReason);
            Assert.Empty(result.Transition.StateChanges);
            Assert.Equal(retainedLifetime, target.Ailments[retainedId].Lifetime);
            Assert.Single(target.Ailments);
        }
    }

    [Fact]
    public void AilmentApplication_InvalidPolicyDecisionIsTypedAndLeavesStateUnchanged()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState attacker = Actor("invalid_policy_attacker");
        RuntimeActorState target = Actor("invalid_policy_target");
        AilmentDefinition poison = Ailment("poison", new NormalAilmentTurnBehaviorDefinition());
        target.ApplyAilment(poison, Turns(2));

        BattleAilmentApplicationResult result = service.TryApplyAilment(
            new(attacker, target, poison, 100, Turns(5)),
            Services(ailmentTransitions: new AlwaysApplyNewTransitionPolicy()));

        Assert.Equal(BattleAilmentApplicationStatus.TransitionRejected, result.Status);
        Assert.Equal(
            BattleAilmentTransitionRejectionReason.InvalidPolicyDecision,
            result.Transition!.RejectionReason);
        Assert.Equal(Turns(2), target.Ailments[Poison].Lifetime);
    }

    [Fact]
    public void TimedStateMutations_RejectInvalidLifetimesBeforeChangingLiveState()
    {
        StatusLifetimeDefinition valid = Turns(2);
        AilmentDefinition poison = Ailment("poison", new NormalAilmentTurnBehaviorDefinition());
        RuntimeActorState actor = Actor("duration_boundary");
        actor.ApplyAilment(poison, valid);
        actor.GrantShield(ShieldKind.Physical, valid);
        actor.BreakAffinity(DamageElement.Fire, valid);
        actor.OverrideAffinity(DamageElement.Ice, ElementalAffinity.Resist, valid);
        ContentId statusId = ContentId.Parse("marked");
        actor.AddOtherStatus(statusId, valid);

        StatusLifetimeDefinition[] invalidLifetimes =
        [
            new(new TurnDurationDefinition(0, OwnerTurnEnd, true), StatusRemovalProfiles.Standard),
            new(new TurnDurationDefinition(1, default, true), StatusRemovalProfiles.Standard),
            new(new PhaseDurationDefinition(default), StatusRemovalProfiles.Standard),
            new(new UnsupportedDurationDefinition(), StatusRemovalProfiles.Standard)
        ];

        foreach (StatusLifetimeDefinition invalid in invalidLifetimes)
        {
            Assert.Throws<ArgumentException>(() => actor.ApplyAilment(poison, invalid));
            Assert.Throws<ArgumentException>(() => actor.GrantShield(ShieldKind.Physical, invalid));
            Assert.Throws<ArgumentException>(() => actor.BreakAffinity(DamageElement.Fire, invalid));
            Assert.Throws<ArgumentException>(() => actor.OverrideAffinity(
                DamageElement.Ice,
                ElementalAffinity.Null,
                invalid));
            Assert.Throws<ArgumentException>(() => actor.AddOtherStatus(statusId, invalid));
            Assert.False(new SplitChargePolicy().Assess(new ChargeApplicationRequest(
                actor,
                ChargeKind.Physical,
                2m,
                invalid)).CanApply);

            Assert.Equal(valid, actor.Ailments[Poison].Lifetime);
            Assert.Equal(valid, actor.Shields[ShieldKind.Physical].Lifetime);
            Assert.Equal(valid, actor.AffinityBreaks[DamageElement.Fire].Lifetime);
            Assert.Equal(ElementalAffinity.Resist, actor.AffinityOverrides[DamageElement.Ice].Affinity);
            Assert.Equal(valid, actor.AffinityOverrides[DamageElement.Ice].Lifetime);
            Assert.Equal(
                valid,
                Assert.Single(actor.ToSnapshot().BattleStatus.Statuses, status => status.Id == statusId).Lifetime);
            Assert.Empty(actor.Charges);
        }

        Assert.Throws<ArgumentException>(() => actor.AddOtherStatus(default, valid));
        Assert.DoesNotContain(default(ContentId), actor.OtherStatuses);
    }

    [Fact]
    public void AilmentApplication_GuardBehaviorIsSelectedByAnInjectedGatePolicy()
    {
        var lifecycle = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState attacker = Actor("gate_attacker");
        RuntimeActorState target = Actor("gate_target");
        target.SetGuarding(true);
        AilmentDefinition poison = Ailment("poison", new NormalAilmentTurnBehaviorDefinition());

        BattleAilmentApplicationResult blocked = lifecycle.TryApplyAilment(
            new(attacker, target, poison, 100),
            Services());
        BattleAilmentApplicationResult allowed = lifecycle.TryApplyAilment(
            new(attacker, target, poison, 100),
            Services(ailmentGate: AllowAilmentsApplicationGatePolicy.Instance));

        Assert.Equal(BattleAilmentApplicationStatus.GuardBlocked, blocked.Status);
        Assert.Equal(BattleAilmentApplicationGateReason.Guarding, blocked.GateDecision!.Reason);
        Assert.Same(blocked.GateDecision, Assert.Single(blocked.Events).AilmentGateDecision);
        Assert.True(allowed.Applied);
        Assert.True(target.IsGuarding);
        Assert.True(target.HasAilment(Poison));
    }

    [Fact]
    public void AilmentApplication_CustomGateRejectionIsTypedAndRollsBackPolicyMutation()
    {
        var lifecycle = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState attacker = Actor("rejecting_gate_attacker", hp: 80);
        RuntimeActorState target = Actor("rejecting_gate_target", hp: 70);
        RuntimeActorState observer = Actor("rejecting_gate_observer", hp: 60);
        var gate = new MutatingRejectingAilmentGate();
        var chance = new CountingAilmentPolicy();

        BattleAilmentApplicationResult result = lifecycle.TryApplyAilment(
            new(
                attacker,
                target,
                Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
                100,
                participants: [attacker, target, observer]),
            Services(chance, ailmentGate: gate));

        Assert.Equal(BattleAilmentApplicationStatus.ApplicationGateRejected, result.Status);
        Assert.Equal(BattleAilmentApplicationGateReason.PolicyRejected, result.GateDecision!.Reason);
        Assert.Equal(0, chance.CallCount);
        Assert.Equal(80, attacker.GetRequiredResource(Hp).Current);
        Assert.Equal(70, target.GetRequiredResource(Hp).Current);
        Assert.Equal(60, observer.GetRequiredResource(Hp).Current);
        Assert.NotSame(attacker, gate.ReceivedActor);
        Assert.NotSame(target, gate.ReceivedTarget);
    }

    [Fact]
    public void AilmentApplication_RejectedAndThrowingChancePoliciesCannotLeakMutations()
    {
        var lifecycle = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState attacker = Actor("atomic_ailment_attacker", hp: 80);
        RuntimeActorState target = Actor("atomic_ailment_target", hp: 70);
        AilmentDefinition poison = Ailment("poison", new NormalAilmentTurnBehaviorDefinition());

        var rejectedPolicy = new MutatingAilmentPolicy(applies: false, throws: false);
        BattleAilmentApplicationResult rejected = lifecycle.TryApplyAilment(
            new(attacker, target, poison, 100),
            Services(rejectedPolicy));

        Assert.Equal(BattleAilmentApplicationStatus.Missed, rejected.Status);
        Assert.Equal(80, attacker.GetRequiredResource(Hp).Current);
        Assert.Equal(70, target.GetRequiredResource(Hp).Current);
        Assert.False(target.HasAilment(Poison));
        Assert.NotSame(attacker, rejectedPolicy.ReceivedActor);

        var throwingPolicy = new MutatingAilmentPolicy(applies: true, throws: true);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            lifecycle.TryApplyAilment(
                new(attacker, target, poison, 100),
                Services(throwingPolicy)));

        Assert.Equal("Deliberate ailment-policy failure.", exception.Message);
        Assert.Equal(80, attacker.GetRequiredResource(Hp).Current);
        Assert.Equal(70, target.GetRequiredResource(Hp).Current);
        Assert.False(target.HasAilment(Poison));
    }

    [Fact]
    public void AilmentApplication_AcceptedPolicyCommitsStagedMutationsAndTransitionTogether()
    {
        var lifecycle = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState attacker = Actor("accepted_ailment_attacker", hp: 80);
        RuntimeActorState target = Actor("accepted_ailment_target", hp: 70);
        var policy = new MutatingAilmentPolicy(applies: true, throws: false);

        BattleAilmentApplicationResult result = lifecycle.TryApplyAilment(
            new(
                attacker,
                target,
                Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
                100),
            Services(policy));

        Assert.True(result.Applied);
        Assert.Equal(1, attacker.GetRequiredResource(Hp).Current);
        Assert.Equal(2, target.GetRequiredResource(Hp).Current);
        Assert.True(target.HasAilment(Poison));
    }

    [Fact]
    public void AilmentApplication_InjectedServiceCannotMutateParticipantsOnRejection()
    {
        var lifecycle = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState attacker = Actor("custom_service_attacker", hp: 80);
        RuntimeActorState target = Actor("custom_service_target", hp: 70);
        RuntimeActorState observer = Actor("custom_service_observer", hp: 60);
        var applicationService = new MutatingRejectingAilmentApplicationService();

        BattleAilmentApplicationResult result = lifecycle.TryApplyAilment(
            new(
                attacker,
                target,
                Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
                100,
                participants: [attacker, target, observer]),
            Services(ailmentApplications: applicationService));

        Assert.Equal(BattleAilmentApplicationStatus.Missed, result.Status);
        Assert.Equal(80, attacker.GetRequiredResource(Hp).Current);
        Assert.Equal(70, target.GetRequiredResource(Hp).Current);
        Assert.Equal(60, observer.GetRequiredResource(Hp).Current);
        Assert.All(applicationService.ReceivedParticipants, participant =>
            Assert.DoesNotContain(participant, new[] { attacker, target, observer }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AilmentApplicationRequestRejectsInvalidAuthoredChance(int chance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleAilmentApplicationRequest(
            Actor("attacker"),
            Actor("target"),
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            chance));
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
    public void TurnEnd_ZeroNaturalRecoveryMultiplierUsesOnlyTheFixedBaseChance()
    {
        RuntimeActorState actor = Actor("fixed_recovery", luck: 40);
        ContentId ailmentId = ContentId.Parse("fixed_recovery_ailment");
        actor.ApplyAilment(
            Ailment(
                ailmentId.ToString(),
                new NormalAilmentTurnBehaviorDefinition(),
                recovery: new AilmentRecoveryDefinition(
                    new NaturalAilmentRecoveryDefinition(25, Luck, 0m))),
            Turns(3));

        BattleTurnEndLifecycleResult result = new BattleStatusLifecycleService(
            new SequenceRandomSource(24)).ProcessTurnEnd(
                new(actor, [actor], Battle, OwnerTurnEnd),
                Services());

        Assert.False(actor.HasAilment(ailmentId));
        Assert.Equal(
            25m,
            Assert.Single(result.Events, item =>
                item.Kind == BattleStatusLifecycleEventKind.AilmentRecovered).Value);
    }

    [Fact]
    public void TurnEnd_NegativeNaturalRecoveryMultiplierRejectsWithoutMutation()
    {
        RuntimeActorState actor = Actor("negative_recovery", hp: 50, luck: 40);
        ContentId ailmentId = ContentId.Parse("negative_recovery_ailment");
        actor.ApplyAilment(
            Ailment(
                ailmentId.ToString(),
                new NormalAilmentTurnBehaviorDefinition(),
                recovery: new AilmentRecoveryDefinition(
                    new NaturalAilmentRecoveryDefinition(25, Luck, -0.5m))),
            Turns(3));

        Assert.Throws<InvalidOperationException>(() =>
            new BattleStatusLifecycleService(new ThrowingRandomSource()).ProcessTurnEnd(
                new(actor, [actor], Battle, OwnerTurnEnd),
                Services()));

        Assert.True(actor.HasAilment(ailmentId));
        Assert.Equal(50, actor.GetRequiredResource(Hp).Current);
        Assert.Equal(3, Assert.IsType<TurnDurationDefinition>(
            Assert.Single(actor.Ailments).Value.Duration).Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void TurnEnd_RejectsInvalidNaturalRecoveryChanceWithoutMutation(int chance)
    {
        RuntimeActorState actor = Actor("invalid_recovery", hp: 50, luck: 40);
        ContentId ailmentId = ContentId.Parse("invalid_recovery_ailment");
        actor.ApplyAilment(
            Ailment(
                ailmentId.ToString(),
                new NormalAilmentTurnBehaviorDefinition(),
                recovery: new AilmentRecoveryDefinition(
                    new NaturalAilmentRecoveryDefinition(chance, Luck, 0.5m))),
            Turns(3));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleStatusLifecycleService(new ThrowingRandomSource()).ProcessTurnEnd(
                new(actor, [actor], Battle, OwnerTurnEnd),
                Services()));

        Assert.True(actor.HasAilment(ailmentId));
        Assert.Equal(50, actor.GetRequiredResource(Hp).Current);
        Assert.Equal(3, Assert.IsType<TurnDurationDefinition>(
            Assert.Single(actor.Ailments).Value.Duration).Value);
    }

    [Fact]
    public void TurnEnd_SuspendsReserveActorTicksDamageAndRecovery()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(0));
        RuntimeActorState reserve = Actor("reserve", hp: 50, isDeployed: false);
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
    public void TurnEnd_AilmentTriggerUsesTheSameTypedPartyTargetingAsPassives()
    {
        RuntimeActorState owner = Actor("ailment_owner", hp: 50);
        RuntimeActorState ally = Actor("ailment_ally", hp: 50);
        RuntimeActorState reserve = Actor("ailment_reserve", hp: 50, isDeployed: false);
        owner.ApplyAilment(
            IndependentAilment(
                "party_recovery_ailment",
                new NormalAilmentTurnBehaviorDefinition(),
                [new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))],
                    targeting: StandardPassiveTriggerTargeting.LivingOwnerTeam)]),
            Turns(3));

        BattleTurnEndLifecycleResult result = new BattleStatusLifecycleService(new SequenceRandomSource())
            .ProcessTurnEnd(
                new(owner, [owner, ally, reserve], Battle, OwnerTurnEnd),
                Services());

        Assert.Equal(60, owner.GetRequiredResource(Hp).Current);
        Assert.Equal(60, ally.GetRequiredResource(Hp).Current);
        Assert.Equal(50, reserve.GetRequiredResource(Hp).Current);
        Assert.Equal(
            [owner.InstanceId, ally.InstanceId],
            result.Events
                .Where(item => item.Kind == BattleStatusLifecycleEventKind.ResourceChanged)
                .Select(item => item.ActorId));
    }

    [Fact]
    public void TurnEnd_PassiveEventsPreserveTriggerOutcomeIndexEventAndEffects()
    {
        SkillDefinition passive = PassiveSkill(
            "turn_end_passive",
            [
                new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(30))],
                    new ResourcePercentageConditionDefinition(
                        ConditionSubject.Actor,
                        Hp,
                        NumericComparison.GreaterThan,
                        80)),
                new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [
                        new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(15)),
                        new GrantShieldEffectDefinition(ShieldKind.Magical)
                    ],
                    new ResourcePercentageConditionDefinition(
                        ConditionSubject.Actor,
                        Hp,
                        NumericComparison.LessThan,
                        80))
            ]);
        RuntimeActorState actor = Actor("passive_event_actor", hp: 50, passiveSkills: [passive]);

        BattleTurnEndLifecycleResult result = new BattleStatusLifecycleService(new SequenceRandomSource())
            .ProcessTurnEnd(new(actor, [actor], Battle, OwnerTurnEnd), Services());

        Assert.Equal(65, actor.GetRequiredResource(Hp).Current);
        BattleStatusLifecycleEvent evaluated = Assert.Single(result.Events, item =>
            item.Kind == BattleStatusLifecycleEventKind.PassiveEvaluated);
        Assert.Equal(PassiveTriggerOutcome.ConditionNotMet, evaluated.PassiveActivation!.Outcome);
        Assert.Equal(0, evaluated.PassiveActivation.TriggerIndex);
        Assert.Equal(OwnerTurnEnd, evaluated.PassiveActivation.EventId);
        BattleStatusLifecycleEvent triggered = Assert.Single(result.Events, item =>
            item.Kind == BattleStatusLifecycleEventKind.PassiveTriggered);
        Assert.Equal(PassiveTriggerOutcome.Executed, triggered.PassiveActivation!.Outcome);
        Assert.Equal(1, triggered.PassiveActivation.TriggerIndex);
        Assert.Equal(OwnerTurnEnd, triggered.PassiveActivation.EventId);
        BattleStatusLifecycleEvent[] effects = result.Events
            .Where(item => item.Kind == BattleStatusLifecycleEventKind.PassiveEffectResolved)
            .ToArray();
        Assert.Equal(2, effects.Length);
        Assert.Equal(15, effects[0].EffectResult!.Value);
        EffectExecutionResult shieldEffect = Assert.IsType<EffectExecutionResult>(effects[1].EffectResult);
        Assert.Equal(1, shieldEffect.EffectIndex);
        Assert.Equal(nameof(ShieldKind.Magical), shieldEffect.Detail);
        Assert.Contains(ShieldKind.Magical, actor.Shields.Keys);
        Assert.All(effects, effect =>
        {
            Assert.Equal(passive.Id, effect.SourceId);
            Assert.Equal(actor.InstanceId, effect.SourceActorId);
        });
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
        var port = new BattleStatusEncounterLifecyclePort(
            lifecycle,
            services,
            BattleStart,
            OwnerTurnEnd,
            TestEncounterClocks.Standard(PlayerTeam, ContentId.Parse("enemy_team")));
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
        BattleExecutionServices execution = Services();
        RuntimeActorState actor = Actor("actor");
        ContentId attack = ContentId.Parse("attack");

        BattleStatusLifecycleResult raised = service.ApplyStatStage(actor, attack, int.MaxValue, execution);
        BattleStatusLifecycleResult unchanged = service.ApplyStatStage(actor, attack, int.MaxValue, execution);
        BattleStatusLifecycleResult lowered = service.ApplyStatStage(actor, attack, int.MinValue, execution);

        Assert.Equal(BattleStatStageRange.Minimum, actor.StatStages[attack].Stage);
        Assert.Equal(4, Assert.Single(raised.Events, IsAggregateChange).Value);
        Assert.Empty(unchanged.Events);
        Assert.Equal(-8, Assert.Single(lowered.Events, IsAggregateChange).Value);

        static bool IsAggregateChange(BattleStatusLifecycleEvent value) =>
            value.ModifierEvent?.Kind == StatModifierEventKind.AggregateStageChanged;
    }

    [Fact]
    public void Cleanup_ClearsTransientAndEncounterStatusesWithoutRemovingAilments()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        IStatModifierPolicyService statModifiers = TestStatModifierPolicy.CreatePersistent();
        RuntimeActorState actor = Actor("actor");
        actor.SetGuarding(true);
        actor.GrantShield(ShieldKind.Physical, DeploymentTurns(1));
        Assert.True(new SplitChargePolicy().Apply(new ChargeApplicationRequest(
            actor,
            ChargeKind.Physical,
            2,
            DeploymentTurns(1))).Applied);
        TestStatModifierPolicy.ApplyPersistent(actor, ContentId.Parse("attack"), 1);
        actor.BreakAffinity(DamageElement.Fire, EncounterTurns(1));
        actor.OverrideAffinity(DamageElement.Fire, ElementalAffinity.Null, EncounterTurns(1));
        actor.AddOtherStatus(ContentId.Parse("marked"), EncounterTurns(1));
        actor.ApplyAilment(PoisonAilment(), Turns(3));

        BattleStatusLifecycleResult deploymentCleanup = service.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.DeploymentSwap),
            statModifiers);

        Assert.False(actor.IsGuarding);
        Assert.Empty(actor.Shields);
        Assert.Empty(actor.Charges);
        Assert.NotEmpty(actor.StatStages);
        Assert.NotEmpty(actor.AffinityBreaks);
        Assert.NotEmpty(actor.AffinityOverrides);
        Assert.NotEmpty(actor.OtherStatuses);
        Assert.True(actor.HasAilment(Poison));
        Assert.Equal(BattleStatusDepartureReason.DeploymentSwap, deploymentCleanup.Events[^1].DepartureReason);
        Assert.Equal(
            [BattleDurationStateKind.Charge, BattleDurationStateKind.Shield],
            deploymentCleanup.Events
                .Where(statusEvent => statusEvent.RemovalTransition is not null)
                .Select(statusEvent => statusEvent.RemovalTransition!.StateKind)
                .Order());

        BattleStatusLifecycleResult battleCleanup = service.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.BattleEnd),
            statModifiers);

        Assert.Empty(actor.StatStages);
        Assert.Empty(actor.AffinityBreaks);
        Assert.Empty(actor.AffinityOverrides);
        Assert.Empty(actor.OtherStatuses);
        Assert.True(actor.HasAilment(Poison));
        Assert.Equal(BattleStatusDepartureReason.BattleEnd, battleCleanup.Events[^1].DepartureReason);
        Assert.Equal(
            [BattleDurationStateKind.AffinityOverride, BattleDurationStateKind.AffinityBreak, BattleDurationStateKind.OtherStatus],
            battleCleanup.Events
                .Where(statusEvent => statusEvent.RemovalTransition is not null)
                .Select(statusEvent => statusEvent.RemovalTransition!.StateKind)
                .Order());
    }

    [Fact]
    public void ActionEnd_ExpiresInstantDurationsAcrossNonModifierStateFamilies()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        SeedDurationStates(actor, new InstantDurationDefinition(), "instant");
        var participants = new List<RuntimeActorState> { actor, actor };
        var request = new BattleActionEndLifecycleRequest(participants);
        participants.Clear();

        BattleStatusLifecycleResult result = service.ProcessActionEnd(
            request,
            TestStatModifierPolicy.CreatePersistent());

        Assert.Single(request.Participants);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeActorState>)request.Participants).Add(actor));
        AssertNoDurationStates(actor, "instant");
        Assert.Equal(6, result.Events.Count);
        Assert.Equal(1, result.Events.Count(status =>
            status.Kind == BattleStatusLifecycleEventKind.AilmentExpired));
        Assert.Equal(5, result.Events.Count(status =>
            status.Kind == BattleStatusLifecycleEventKind.StatusExpired));
        Assert.All(result.Events, status => Assert.Contains("Instant", status.Detail));
        Assert.All(result.Events, status =>
        {
            Assert.True(status.DurationTransition!.Expired);
            Assert.Null(status.DurationTransition.CurrentDuration);
        });
    }

    [Fact]
    public void TurnDurations_TickEveryNonModifierStateFamilyOnlyOnTheirAuthoredEvent()
    {
        RuntimeActorState actor = Actor("actor");
        SeedDurationStates(actor, new TurnDurationDefinition(1, OwnerTurnEnd, false), "turn");

        Assert.Empty(actor.TickAilmentDurations(ContentId.Parse("other_event")));
        Assert.Empty(actor.TickTimedStatuses(ContentId.Parse("other_event")));

        BattleDurationTickResult[] ticks = actor.TickAilmentDurations(OwnerTurnEnd)
            .Concat(actor.TickTimedStatuses(OwnerTurnEnd))
            .ToArray();

        AssertNoDurationStates(actor, "turn");
        Assert.Equal(6, ticks.Length);
        Assert.Equal(Enum.GetValues<BattleDurationStateKind>()
                .Where(kind => kind != BattleDurationStateKind.StatStage),
            ticks.Select(tick => tick.StateKind).Order().ToArray());
        Assert.All(ticks, tick => Assert.True(tick.Expired));
    }

    [Fact]
    public void PhaseEnd_ExpiresOnlyTheMatchingNonModifierStateFamilies()
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
            new BattlePhaseEndLifecycleRequest([matching, other, permanent], playerPhase),
            TestStatModifierPolicy.CreatePersistent());

        AssertNoDurationStates(matching, "matching");
        AssertAllDurationStatesPresent(other, "other");
        AssertAllDurationStatesPresent(permanent, "permanent");
        Assert.Equal(6, result.Events.Count);
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

        IStatModifierPolicyService statModifiers = TestStatModifierPolicy.CreatePersistent();
        service.Cleanup(
            new BattleStatusCleanupRequest(battle, BattleStatusDepartureReason.BattleEnd),
            statModifiers);
        service.Cleanup(
            new BattleStatusCleanupRequest(permanent, BattleStatusDepartureReason.BattleEnd),
            statModifiers);

        AssertNoDurationStates(battle, "battle");
        AssertAllDurationStatesPresent(permanent, "permanent");
        Assert.True(permanent.HasAilment(turnAilment.Id));
    }

    [Fact]
    public void EncounterLifetime_ExpiresAfterItsClockOrBattleEndWhicheverComesFirst()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState clockExpired = Actor("clock_expired");
        RuntimeActorState encounterEnded = Actor("encounter_ended");
        ContentId clockId = ContentId.Parse("clock_status");
        ContentId encounterId = ContentId.Parse("encounter_status");
        clockExpired.AddOtherStatus(clockId, EncounterTurns(1));
        encounterEnded.AddOtherStatus(encounterId, EncounterTurns(3));

        BattleDurationTickResult clockTick = Assert.Single(
            clockExpired.TickTimedStatuses(OwnerTurnEnd));
        service.Cleanup(
            new BattleStatusCleanupRequest(encounterEnded, BattleStatusDepartureReason.BattleEnd),
            TestStatModifierPolicy.CreatePersistent());

        Assert.True(clockTick.Expired);
        Assert.DoesNotContain(clockId, clockExpired.OtherStatuses);
        Assert.DoesNotContain(encounterId, encounterEnded.OtherStatuses);
    }

    [Fact]
    public void BattleExpiration_RemainsIndependentFromCleanupRemovalPermissions()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("protected_battle_expiration");
        ContentId statusId = ContentId.Parse("protected_battle_status");
        actor.AddOtherStatus(
            statusId,
            new StatusLifetimeDefinition(
                new BattleDurationDefinition(),
                StatusRemovalProfiles.Protected));

        service.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.BattleEnd),
            TestStatModifierPolicy.CreatePersistent());

        Assert.DoesNotContain(statusId, actor.OtherStatuses);
    }

    [Fact]
    public void UncurableRemovalProfile_BlocksRecoveryButStillAllowsScriptedRemoval()
    {
        RuntimeActorState actor = Actor("uncurable");
        AilmentDefinition ailment = IndependentAilment(
            "uncurable_ailment",
            new NormalAilmentTurnBehaviorDefinition());
        actor.ApplyAilment(
            ailment,
            new StatusLifetimeDefinition(TurnClock(3), StatusRemovalProfiles.Uncurable));

        Assert.Empty(actor.RemoveAilments(StatusRemovalCause.CureEffect, _ => true));
        Assert.Empty(actor.RemoveAilments(StatusRemovalCause.NaturalRecovery, _ => true));
        Assert.Empty(actor.RemoveAilments(StatusRemovalCause.RecoveryEvent, _ => true));
        Assert.True(actor.HasAilment(ailment.Id));

        Assert.Equal(
            [ailment.Id],
            actor.RemoveAilments(StatusRemovalCause.ScriptedRemoval, _ => true));
        Assert.False(actor.HasAilment(ailment.Id));
    }

    [Theory]
    [InlineData(BattleStatusDepartureReason.DeploymentSwap, true)]
    [InlineData(BattleStatusDepartureReason.RosterRecall, true)]
    [InlineData(BattleStatusDepartureReason.Defeat, false)]
    [InlineData(BattleStatusDepartureReason.Flee, false)]
    [InlineData(BattleStatusDepartureReason.BattleEnd, false)]
    [InlineData(BattleStatusDepartureReason.FieldTransition, false)]
    public void Cleanup_UsesTypedDepartureReasonsAndNeverTurnsRecallIntoAFreeCure(
        BattleStatusDepartureReason reason,
        bool encounterStateSurvives)
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("departure_actor");
        ContentId encounterStatusId = ContentId.Parse("encounter_mark");
        actor.SetGuarding(true);
        actor.ApplyAilment(PoisonAilment(), Turns(3));
        actor.GrantShield(ShieldKind.Physical, StandardStatusLifetimes.DeploymentTransient);
        actor.AddOtherStatus(
            encounterStatusId,
            StandardStatusLifetimes.Encounter(new PermanentDurationDefinition()));

        service.Cleanup(
            new BattleStatusCleanupRequest(actor, reason),
            TestStatModifierPolicy.CreatePersistent());

        Assert.False(actor.IsGuarding);
        Assert.Empty(actor.Shields);
        Assert.True(actor.HasAilment(Poison));
        Assert.Equal(encounterStateSurvives, actor.OtherStatuses.Contains(encounterStatusId));
    }

    [Fact]
    public void SwapCleanup_PreservesPermanentChargesAndShields()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        actor.SetGuarding(true);
        Assert.True(new SplitChargePolicy().Apply(new ChargeApplicationRequest(
            actor,
            ChargeKind.Physical,
            2m,
            StandardStatusLifetimes.Persistent)).Applied);
        actor.GrantShield(ShieldKind.Physical, StandardStatusLifetimes.Persistent);

        service.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.DeploymentSwap),
            TestStatModifierPolicy.CreatePersistent());

        Assert.False(actor.IsGuarding);
        Assert.Contains(ChargeKind.Physical, actor.Charges.Keys);
        Assert.Contains(ShieldKind.Physical, actor.Shields.Keys);
    }

    [Fact]
    public void AffinityBreakDuration_SuspendsInReserveAndExpiresOnItsAuthoredTick()
    {
        RuntimeActorState actor = Actor("actor", isDeployed: false);
        actor.BreakAffinity(DamageElement.Ice, Turns(1));

        Assert.Empty(actor.TickTimedStatuses(OwnerTurnEnd));
        Assert.True(actor.AffinityBreaks.ContainsKey(DamageElement.Ice));

        actor.SetEncounterPresence(isDeployed: true);
        BattleDurationTickResult tick = Assert.Single(actor.TickTimedStatuses(OwnerTurnEnd));

        Assert.True(tick.Expired);
        Assert.Empty(actor.AffinityBreaks);
    }

    [Fact]
    public void StatusLifecycleDemoPack_LoadsTheElevenReferenceAilments()
    {
        string jsonRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        string manifestName = "status_lifecycle_demo.manifest.json";
        string ailmentName = "status_lifecycle_demo.ailments.json";
        var bundle = new ContentPackTextBundle(
            manifestName,
            File.ReadAllText(TestContentPath.Resolve(jsonRoot, manifestName)),
            [new ContentDocumentText(ailmentName, ailmentName, File.ReadAllText(TestContentPath.Resolve(jsonRoot, ailmentName)))]);

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
        bool isDeployed = true,
        IEnumerable<SkillDefinition>? passiveSkills = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            PlayerTeam,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 100)],
            new RuntimeEncounterPresenceSnapshot(isDeployed),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), PlayerTeam),
            [new KeyValuePair<ContentId, decimal>(Luck, luck)],
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

    private static StatusLifetimeDefinition Turns(int value) =>
        StandardStatusLifetimes.Field(TurnClock(value));

    private static StatusLifetimeDefinition DeploymentTurns(int value) =>
        StandardStatusLifetimes.Deployment(TurnClock(value));

    private static StatusLifetimeDefinition EncounterTurns(int value) =>
        StandardStatusLifetimes.Encounter(TurnClock(value));

    private static TurnDurationDefinition TurnClock(int value) =>
        new(value, OwnerTurnEnd, true);

    private static void SeedDurationStates(
        RuntimeActorState actor,
        DurationDefinition expiration,
        string suffix)
    {
        StatusLifetimeDefinition ailmentLifetime = expiration switch
        {
            InstantDurationDefinition or PhaseDurationDefinition or BattleDurationDefinition =>
                StandardStatusLifetimes.Encounter(expiration),
            TurnDurationDefinition => StandardStatusLifetimes.Field(expiration),
            PermanentDurationDefinition => StandardStatusLifetimes.Persistent,
            _ => throw new InvalidOperationException()
        };
        actor.ApplyAilment(
            IndependentAilment(
                $"ailment_{suffix}",
                new NormalAilmentTurnBehaviorDefinition()),
            ailmentLifetime);
        Assert.True(new SplitChargePolicy().Apply(new ChargeApplicationRequest(
            actor,
            ChargeKind.Physical,
            2m,
            DeploymentOrPersistent(expiration))).Applied);
        actor.GrantShield(ShieldKind.Physical, DeploymentOrPersistent(expiration));
        actor.OverrideAffinity(
            DamageElement.Ice,
            ElementalAffinity.Resist,
            EncounterOrPersistent(expiration));
        actor.BreakAffinity(DamageElement.Fire, EncounterOrPersistent(expiration));
        actor.AddOtherStatus(
            ContentId.Parse($"status_{suffix}"),
            EncounterOrPersistent(expiration));
    }

    private static StatusLifetimeDefinition DeploymentOrPersistent(DurationDefinition expiration) =>
        expiration is PermanentDurationDefinition
            ? StandardStatusLifetimes.Persistent
            : StandardStatusLifetimes.Deployment(expiration);

    private static StatusLifetimeDefinition EncounterOrPersistent(DurationDefinition expiration) =>
        expiration is PermanentDurationDefinition
            ? StandardStatusLifetimes.Persistent
            : StandardStatusLifetimes.Encounter(expiration);

    private static void AssertAllDurationStatesPresent(RuntimeActorState actor, string suffix)
    {
        Assert.True(actor.HasAilment(ContentId.Parse($"ailment_{suffix}")));
        Assert.Contains(ChargeKind.Physical, actor.Charges.Keys);
        Assert.Contains(ShieldKind.Physical, actor.Shields.Keys);
        Assert.Contains(DamageElement.Ice, actor.AffinityOverrides.Keys);
        Assert.Contains(DamageElement.Fire, actor.AffinityBreaks.Keys);
        Assert.Contains(ContentId.Parse($"status_{suffix}"), actor.OtherStatuses);
    }

    private static void AssertNoDurationStates(RuntimeActorState actor, string suffix)
    {
        Assert.False(actor.HasAilment(ContentId.Parse($"ailment_{suffix}")));
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
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffectHandlers = null,
        IBattleAilmentTransitionPolicy? ailmentTransitions = null,
        IBattleAilmentApplicationGatePolicy? ailmentGate = null,
        IBattleAilmentApplicationService? ailmentApplications = null) =>
        new(
            new EmptyAilments(),
            new NoDamagePolicy(),
            new NoInstantDeathPolicy(),
            ailmentPolicy ?? new AlwaysAilmentPolicy(),
            new AlwaysChancePolicy(),
            new ZeroPowerPolicy(),
            new FirstTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            TestStatModifierPolicy.CreatePersistent(),
            new SplitChargePolicy(),
            formulaHandlers:
            [
                new KeyValuePair<ContentId, IFormulaAmountHandler>(
                    PoisonFormula,
                    new PoisonFormulaHandler())
            ],
            customEffectHandlers: customEffectHandlers,
            ailmentApplications: ailmentApplications)
        {
            AilmentApplicationGate = ailmentGate ?? GuardBlocksAilmentsApplicationGatePolicy.Instance,
            AilmentTransitions = ailmentTransitions ?? StandardBattleAilmentTransitionPolicy.Instance
        };

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterResource("hp", "sp")
            .RegisterStat("luck")
            .RegisterEvent("owner_turn_end")
            .RegisterAction("basic_attack", "guard", "pass")
            .RegisterAilmentGroup("major_ailment", "poison", "immobilize", "mental")
            .RegisterFormula("reference_poison_damage", new AcceptingParameterValidator())
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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Convergence.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
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

    private sealed class ThrowingRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) =>
            throw new InvalidOperationException("Random selection must not occur.");

        public decimal NextUnitDecimal() =>
            throw new InvalidOperationException("Random selection must not occur.");
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
        public DamagePolicyResolution Resolve(DamagePolicyRequest request) =>
            new([], request.Affinity);
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

    private sealed class CountingAilmentPolicy : IAilmentApplicationPolicy
    {
        public int CallCount { get; private set; }

        public bool ShouldApply(AilmentApplicationPolicyRequest request)
        {
            CallCount++;
            return true;
        }
    }

    private sealed class MutatingAilmentPolicy(bool applies, bool throws) : IAilmentApplicationPolicy
    {
        public RuntimeActorState? ReceivedActor { get; private set; }

        public bool ShouldApply(AilmentApplicationPolicyRequest request)
        {
            ReceivedActor = request.Actor;
            request.Actor.SetResource(Hp, 1);
            request.Target.SetResource(Hp, 2);
            if (throws)
            {
                throw new InvalidOperationException("Deliberate ailment-policy failure.");
            }

            return applies;
        }
    }

    private sealed class MutatingRejectingAilmentGate : IBattleAilmentApplicationGatePolicy
    {
        public RuntimeActorState? ReceivedActor { get; private set; }
        public RuntimeActorState? ReceivedTarget { get; private set; }

        public BattleAilmentApplicationGateDecision Evaluate(BattleAilmentApplicationGateRequest request)
        {
            ReceivedActor = request.Actor;
            ReceivedTarget = request.Target;
            foreach (RuntimeActorState participant in request.Participants)
            {
                participant.SetResource(Hp, 1);
            }

            return new BattleAilmentApplicationGateDecision(
                BattleAilmentApplicationGateOutcome.Blocked,
                BattleAilmentApplicationGateReason.PolicyRejected);
        }
    }

    private sealed class MutatingRejectingAilmentApplicationService : IBattleAilmentApplicationService
    {
        public IReadOnlyList<RuntimeActorState> ReceivedParticipants { get; private set; } = [];

        public BattleAilmentApplicationResult Apply(
            BattleAilmentApplicationRequest request,
            BattleExecutionServices services)
        {
            ReceivedParticipants = request.Participants;
            foreach (RuntimeActorState participant in request.Participants)
            {
                participant.SetResource(Hp, 1);
            }

            return new BattleAilmentApplicationResult(BattleAilmentApplicationStatus.Missed, []);
        }
    }

    private sealed record UnsupportedDurationDefinition()
        : DurationDefinition((DurationKind)int.MaxValue);

    private sealed class AlwaysApplyNewTransitionPolicy : IBattleAilmentTransitionPolicy
    {
        public BattleAilmentTransitionDecision Resolve(BattleAilmentTransitionPolicyRequest request) =>
            new(BattleAilmentTransitionOperation.ApplyNew);
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
