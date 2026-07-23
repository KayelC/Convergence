using Convergence.Content;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class BattleLifecycleClockTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId ActorTurnEnd = ContentId.Parse("owner_turn_end");
    private static readonly ContentId PlayerPhaseEnd = ContentId.Parse("player_phase_end");
    private static readonly ContentId PlayerPhase = ContentId.Parse("player_phase");
    private static readonly ContentId RoundEnd = ContentId.Parse("round_end");

    [Fact]
    public void ActorTurnClock_AdvancesOnlyTheIdentifiedActor()
    {
        RuntimeActorState first = Actor("first", PlayerTeam, isDeployed: true);
        RuntimeActorState second = Actor("second", PlayerTeam, isDeployed: true);
        ContentId firstStatus = AddCountedStatus(first, "first_clock", ActorTurnEnd, 1, false);
        ContentId secondStatus = AddCountedStatus(second, "second_clock", ActorTurnEnd, 1, false);
        var service = new BattleDurationLifecycleService();

        service.ProcessClock(
            new BattleLifecycleClockRequest(
                [first, second],
                new ActorTurnLifecycleClockBoundary(ActorTurnEnd, first.InstanceId, 1)),
            TestStatModifierPolicy.CreatePersistent());

        Assert.DoesNotContain(firstStatus, first.OtherStatuses);
        Assert.Contains(secondStatus, second.OtherStatuses);
    }

    [Fact]
    public void ActorTurnClock_RejectsAnActorOutsideTheParticipantSetWithoutMutation()
    {
        RuntimeActorState actor = Actor("included", PlayerTeam, isDeployed: true);
        ContentId status = AddCountedStatus(actor, "included_clock", ActorTurnEnd, 1, false);
        var service = new BattleDurationLifecycleService();

        Assert.Throws<InvalidOperationException>(() => service.ProcessClock(
            new BattleLifecycleClockRequest(
                [actor],
                new ActorTurnLifecycleClockBoundary(
                    ActorTurnEnd,
                    RuntimeInstanceId.Parse("missing"),
                    1)),
            TestStatModifierPolicy.CreatePersistent()));

        Assert.Contains(status, actor.OtherStatuses);
    }

    [Fact]
    public void ActionClock_ExpiresOnlyActionScopedStateWithoutAHostEventId()
    {
        RuntimeActorState reserve = Actor("reserve_action", PlayerTeam, isDeployed: false);
        ContentId status = ContentId.Parse("action_marker");
        reserve.AddOtherStatus(
            status,
            StandardStatusLifetimes.Encounter(new InstantDurationDefinition()));

        new BattleDurationLifecycleService().ProcessClock(
            new BattleLifecycleClockRequest(
                [reserve],
                new ActionLifecycleClockBoundary(1)),
            TestStatModifierPolicy.CreatePersistent());

        Assert.DoesNotContain(status, reserve.OtherStatuses);
    }

    [Fact]
    public void TeamPhaseClock_UsesItsExplicitPhaseIdAndCountedEventId()
    {
        RuntimeActorState actor = Actor("phase_actor", PlayerTeam, isDeployed: true);
        ContentId phaseStatus = ContentId.Parse("phase_status");
        ContentId countedStatus = AddCountedStatus(
            actor,
            "phase_counted",
            PlayerPhaseEnd,
            1,
            false);
        actor.AddOtherStatus(
            phaseStatus,
            StandardStatusLifetimes.Encounter(new PhaseDurationDefinition(PlayerPhase)));

        new BattleDurationLifecycleService().ProcessClock(
            new BattleLifecycleClockRequest(
                [actor],
                new TeamPhaseLifecycleClockBoundary(
                    PlayerPhaseEnd,
                    PlayerTeam,
                    PlayerPhase,
                    1)),
            TestStatModifierPolicy.CreatePersistent());

        Assert.DoesNotContain(phaseStatus, actor.OtherStatuses);
        Assert.DoesNotContain(countedStatus, actor.OtherStatuses);
        Assert.NotEqual(PlayerTeam, PlayerPhase);
    }

    [Fact]
    public void DefaultReservePolicy_SuspendsExactRemainingStateAtPhaseAndRoundClocks()
    {
        RuntimeActorState reserve = Actor("reserve_suspended", PlayerTeam, isDeployed: false);
        ContentId phaseStatus = AddCountedStatus(reserve, "phase_suspended", PlayerPhaseEnd, 2, false);
        ContentId roundStatus = AddCountedStatus(reserve, "round_suspended", RoundEnd, 2, false);
        var service = new BattleDurationLifecycleService();
        IStatModifierPolicyService modifiers = TestStatModifierPolicy.CreatePersistent();

        service.ProcessClock(
            new BattleLifecycleClockRequest(
                [reserve],
                new TeamPhaseLifecycleClockBoundary(
                    PlayerPhaseEnd,
                    PlayerTeam,
                    PlayerPhase,
                    1)),
            modifiers);
        service.ProcessClock(
            new BattleLifecycleClockRequest(
                [reserve],
                new RoundLifecycleClockBoundary(RoundEnd, 1)),
            modifiers);

        Assert.Equal(2, Remaining(reserve, phaseStatus));
        Assert.Equal(2, Remaining(reserve, roundStatus));
    }

    [Fact]
    public void RoundReservePolicy_AdvancesOnlyOptedInStateOncePerRound()
    {
        RuntimeActorState reserve = Actor("reserve_round", PlayerTeam, isDeployed: false);
        ContentId advancing = AddCountedStatus(reserve, "round_advancing", RoundEnd, 2, false);
        ContentId authoredSuspension = AddCountedStatus(reserve, "round_authored_suspend", RoundEnd, 2, true);
        var service = new BattleDurationLifecycleService(
            new AdvanceReserveOnEncounterClockPolicy(BattleLifecycleClockKind.Round, RoundEnd));

        service.ProcessClock(
            new BattleLifecycleClockRequest(
                [reserve],
                new RoundLifecycleClockBoundary(RoundEnd, 1)),
            TestStatModifierPolicy.CreatePersistent());

        Assert.Equal(1, Remaining(reserve, advancing));
        Assert.Equal(2, Remaining(reserve, authoredSuspension));
    }

    [Fact]
    public void TeamPhaseReservePolicy_AdvancesOnlyAtTheOwningTeamPhase()
    {
        RuntimeActorState reserve = Actor("reserve_phase", PlayerTeam, isDeployed: false);
        ContentId status = AddCountedStatus(reserve, "owning_phase", PlayerPhaseEnd, 2, false);
        var service = new BattleDurationLifecycleService(
            new AdvanceReserveOnEncounterClockPolicy(
                BattleLifecycleClockKind.TeamPhase,
                PlayerPhaseEnd));
        IStatModifierPolicyService modifiers = TestStatModifierPolicy.CreatePersistent();

        service.ProcessClock(
            new BattleLifecycleClockRequest(
                [reserve],
                new TeamPhaseLifecycleClockBoundary(
                    PlayerPhaseEnd,
                    EnemyTeam,
                    PlayerPhase,
                    1)),
            modifiers);
        Assert.Equal(2, Remaining(reserve, status));

        service.ProcessClock(
            new BattleLifecycleClockRequest(
                [reserve],
                new TeamPhaseLifecycleClockBoundary(
                    PlayerPhaseEnd,
                    PlayerTeam,
                    PlayerPhase,
                    2)),
            modifiers);
        Assert.Equal(1, Remaining(reserve, status));
    }

    [Fact]
    public void CustomClock_AdvancesDeployedStateButCannotAgeReserveState()
    {
        ContentId custom = ContentId.Parse("scripted_beat");
        RuntimeActorState deployed = Actor("custom_deployed", PlayerTeam, isDeployed: true);
        RuntimeActorState reserve = Actor("custom_reserve", PlayerTeam, isDeployed: false);
        ContentId deployedStatus = AddCountedStatus(deployed, "custom_deployed_status", custom, 1, false);
        ContentId reserveStatus = AddCountedStatus(reserve, "custom_reserve_status", custom, 1, false);
        var service = new BattleDurationLifecycleService(
            new ThrowingReservePolicy());

        service.ProcessClock(
            new BattleLifecycleClockRequest(
                [deployed, reserve],
                new CustomLifecycleClockBoundary(custom, 1)),
            TestStatModifierPolicy.CreatePersistent());

        Assert.DoesNotContain(deployedStatus, deployed.OtherStatuses);
        Assert.Contains(reserveStatus, reserve.OtherStatuses);
    }

    [Theory]
    [InlineData(BattleLifecycleClockKind.ActorTurn)]
    [InlineData(BattleLifecycleClockKind.Action)]
    [InlineData(BattleLifecycleClockKind.Custom)]
    public void SuppliedAdvancingPolicy_RejectsPerActionAndNonEncounterClocks(
        BattleLifecycleClockKind kind) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AdvanceReserveOnEncounterClockPolicy(kind, RoundEnd));

    [Fact]
    public void ClockRequests_DefensivelySnapshotParticipantsAndModifierBoundaries()
    {
        RuntimeActorState actor = Actor("snapshot_actor", PlayerTeam, isDeployed: true);
        var participants = new List<RuntimeActorState> { actor, actor };
        var boundaries = new List<StatModifierLifecycleBoundary>
        {
            new(RoundEnd, 1)
        };
        var request = new BattleLifecycleClockRequest(
            participants,
            new RoundLifecycleClockBoundary(RoundEnd, 1),
            boundaries);

        participants.Clear();
        boundaries.Clear();

        Assert.Single(request.Participants);
        Assert.Single(request.StatModifierBoundaries);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeActorState>)request.Participants).Add(actor));
    }

    [Fact]
    public void ExplicitEncounterClockPolicy_PreservesDistinctTeamPhaseAndEventIds()
    {
        var expected = new BattleTeamPhaseClockDefinition(
            PlayerTeam,
            PlayerPhase,
            PlayerPhaseEnd);
        var policy = new ExplicitBattleEncounterLifecycleClockPolicy(
            [expected],
            RoundEnd);

        BattleTeamPhaseClockDefinition actual = policy.ResolveTeamPhase(PlayerTeam);

        Assert.Same(expected, actual);
        Assert.NotEqual(actual.TeamId, actual.PhaseId);
        Assert.NotEqual(actual.TeamId, actual.EventId);
        Assert.Equal(RoundEnd, policy.RoundEndEventId);
        Assert.Throws<InvalidOperationException>(() => policy.ResolveTeamPhase(EnemyTeam));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ContentId, BattleTeamPhaseClockDefinition>)policy.TeamPhases)
            .Add(EnemyTeam, expected));
    }

    private static RuntimeActorState Actor(
        string id,
        ContentId teamId,
        bool isDeployed) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            teamId,
            Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, 100, 100)],
            new RuntimeEncounterPresenceSnapshot(isDeployed),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_owner"), teamId));

    private static ContentId AddCountedStatus(
        RuntimeActorState actor,
        string id,
        ContentId eventId,
        int remaining,
        bool suspendWhileReserve)
    {
        ContentId statusId = ContentId.Parse(id);
        actor.AddOtherStatus(
            statusId,
            StandardStatusLifetimes.Encounter(
                new TurnDurationDefinition(remaining, eventId, suspendWhileReserve)));
        return statusId;
    }

    private static int Remaining(RuntimeActorState actor, ContentId statusId) =>
        Assert.IsType<TurnDurationDefinition>(
            actor.ToSnapshot().BattleStatus.Statuses.Single(status => status.Id == statusId).Duration).Value;

    private sealed class ThrowingReservePolicy : IBattleReserveLifecyclePolicy
    {
        public bool ShouldAdvance(BattleReserveLifecycleRequest request) =>
            throw new InvalidOperationException("Custom and actor clocks must not consult reserve advancement.");
    }
}
