using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Encounters;

public sealed class TeamPhaseRoundRobinScheduleTests
{
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly RuntimeInstanceId PlayerA = RuntimeInstanceId.Parse("player_a");
    private static readonly RuntimeInstanceId PlayerB = RuntimeInstanceId.Parse("player_b");
    private static readonly RuntimeInstanceId Enemy = RuntimeInstanceId.Parse("enemy");

    [Fact]
    public void Schedule_PreservesRoundTeamPhaseAndRoundRobinOrder()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = Start(policy, participants, roundLimit: 1);

        AssertStep<BattleEncounterRoundStartedScheduleStep>(cursor, round: 1);
        cursor = Boundary(policy, cursor, participants);

        BattleEncounterPhaseStartedScheduleStep playerPhase =
            AssertStep<BattleEncounterPhaseStartedScheduleStep>(cursor, round: 1);
        Assert.Equal(PlayerTeam, playerPhase.TeamId);
        Assert.Equal(2, playerPhase.TurnEconomyStart!.ActiveActorCount);
        cursor = Boundary(policy, cursor, participants);

        Assert.Equal(
            PlayerA,
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, round: 1).ActorId);
        cursor = Command(policy, cursor, participants, hasRemainingOpportunities: true);
        Assert.Equal(
            PlayerB,
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, round: 1).ActorId);
        cursor = Command(policy, cursor, participants, hasRemainingOpportunities: false);

        Assert.Equal(
            PlayerTeam,
            AssertStep<BattleEncounterPhaseEndedScheduleStep>(cursor, round: 1).TeamId);
        cursor = Boundary(policy, cursor, participants);

        BattleEncounterPhaseStartedScheduleStep enemyPhase =
            AssertStep<BattleEncounterPhaseStartedScheduleStep>(cursor, round: 1);
        Assert.Equal(EnemyTeam, enemyPhase.TeamId);
        Assert.Equal(1, enemyPhase.TurnEconomyStart!.ActiveActorCount);
        cursor = Boundary(policy, cursor, participants);

        Assert.Equal(
            Enemy,
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, round: 1).ActorId);
        cursor = Command(policy, cursor, participants, hasRemainingOpportunities: false);
        AssertStep<BattleEncounterPhaseEndedScheduleStep>(cursor, round: 1);
        cursor = Boundary(policy, cursor, participants);
        AssertStep<BattleEncounterRoundEndedScheduleStep>(cursor, round: 1);

        BattleEncounterScheduleTransitionResult completed =
            policy.Advance(Request(cursor, BattleEncounterScheduleStepOutcome.BoundaryCompleted(), participants));
        Assert.Equal(BattleEncounterScheduleTransitionStatus.Completed, completed.Status);
        Assert.Equal(1, completed.After!.CompletedRounds);
        Assert.Null(completed.NextStep);
    }

    [Fact]
    public void Schedule_RefreshesAvailabilityAndKeepsTheExistingOpportunityPool()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = Start(policy, participants, roundLimit: 1);
        cursor = Boundary(policy, cursor, participants);
        cursor = Boundary(policy, cursor, participants);
        Assert.Equal(
            PlayerA,
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, 1).ActorId);

        participants =
        [
            Participant(PlayerA, PlayerTeam, deployed: false),
            Participant(PlayerB, PlayerTeam),
            Participant(Enemy, EnemyTeam)
        ];
        cursor = Command(policy, cursor, participants, hasRemainingOpportunities: true);

        Assert.Equal(
            PlayerB,
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, 1).ActorId);
        Assert.Null(cursor.Step.TurnEconomyStart);
    }

    [Fact]
    public void Schedule_AllowsNewDeploymentToJoinAnExistingTeamPhase()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        BattleEncounterScheduleParticipantSnapshot[] participants =
        [
            Participant(PlayerA, PlayerTeam),
            Participant(PlayerB, PlayerTeam, deployed: false),
            Participant(Enemy, EnemyTeam)
        ];
        Cursor cursor = Start(policy, participants, roundLimit: 1);
        cursor = Boundary(policy, cursor, participants);
        Assert.Equal(
            1,
            AssertStep<BattleEncounterPhaseStartedScheduleStep>(cursor, 1)
                .TurnEconomyStart!.ActiveActorCount);
        cursor = Boundary(policy, cursor, participants);

        participants =
        [
            Participant(PlayerA, PlayerTeam),
            Participant(PlayerB, PlayerTeam),
            Participant(Enemy, EnemyTeam)
        ];
        cursor = Command(policy, cursor, participants, hasRemainingOpportunities: true);

        Assert.Equal(
            PlayerB,
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, 1).ActorId);
    }

    [Fact]
    public void Schedule_SkipsUnavailableActorsAndEmptyTeamsWithoutInventingOpportunities()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        BattleEncounterScheduleParticipantSnapshot[] participants =
        [
            Participant(PlayerA, PlayerTeam, defeated: true),
            Participant(PlayerB, PlayerTeam, deployed: false),
            Participant(Enemy, EnemyTeam)
        ];
        Cursor cursor = Start(policy, participants, roundLimit: 1);
        cursor = Boundary(policy, cursor, participants);

        BattleEncounterPhaseStartedScheduleStep phase =
            AssertStep<BattleEncounterPhaseStartedScheduleStep>(cursor, 1);
        Assert.Equal(EnemyTeam, phase.TeamId);
        Assert.Equal(1, phase.TurnEconomyStart!.ActiveActorCount);
    }

    [Fact]
    public void Schedule_ActorUnavailableEvidenceAdvancesWithoutConsumingAnOpportunity()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = Start(policy, participants, roundLimit: 1);
        cursor = Boundary(policy, cursor, participants);
        cursor = Boundary(policy, cursor, participants);
        BattleEncounterCommandWindowScheduleStep command =
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, 1);

        participants =
        [
            Participant(PlayerA, PlayerTeam, deployed: false),
            Participant(PlayerB, PlayerTeam),
            Participant(Enemy, EnemyTeam)
        ];
        BattleEncounterScheduleTransitionResult transition = policy.Advance(
            Request(
                cursor,
                BattleEncounterScheduleStepOutcome.ActorUnavailable(command.ActorId),
                participants));
        cursor = Cursor.From(transition);

        Assert.Equal(
            PlayerB,
            AssertStep<BattleEncounterCommandWindowScheduleStep>(cursor, 1).ActorId);
    }

    [Fact]
    public void Schedule_OpensSubsequentRoundsOnlyAfterRoundEnd()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = Start(policy, participants, roundLimit: 2);

        while (cursor.Step is not BattleEncounterRoundEndedScheduleStep)
        {
            cursor = cursor.Step is BattleEncounterCommandWindowScheduleStep
                ? Command(policy, cursor, participants, hasRemainingOpportunities: false)
                : Boundary(policy, cursor, participants);
        }

        cursor = Boundary(policy, cursor, participants);

        AssertStep<BattleEncounterRoundStartedScheduleStep>(cursor, round: 2);
        Assert.Equal(1, cursor.State.CompletedRounds);
    }

    [Fact]
    public void Schedule_RejectsForeignStateWithoutMutatingIt()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        var foreignPolicy = new ForeignPolicy();
        BattleEncounterScheduleTransitionResult foreign = foreignPolicy.Start(
            new BattleEncounterScheduleStartRequest(
                participants,
                [PlayerTeam, EnemyTeam],
                1));
        var request = new BattleEncounterScheduleAdvanceRequest(
            foreign.After!,
            foreign.NextStep!,
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            participants);

        BattleEncounterScheduleTransitionResult result = policy.Advance(request);

        Assert.Equal(BattleEncounterScheduleTransitionStatus.Rejected, result.Status);
        Assert.Same(foreign.After, result.Before);
        Assert.Same(foreign.After, result.After);
        Assert.Equal(
            BattleEncounterScheduleDiagnosticCode.InvalidState,
            Assert.Single(result.Diagnostics).Code);
    }

    private static Cursor Start(
        IBattleEncounterSchedulePolicy policy,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        int roundLimit) =>
        Cursor.From(policy.Start(
            new BattleEncounterScheduleStartRequest(
                participants,
                [PlayerTeam, EnemyTeam],
                roundLimit)));

    private static Cursor Boundary(
        IBattleEncounterSchedulePolicy policy,
        Cursor cursor,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        BattleEncounterScheduleStepOutcome outcome = cursor.Step.TurnEconomyStart is { } economyStart
            ? BattleEncounterScheduleStepOutcome.TurnEconomyStarted(
                new StandardActionTurnEconomySnapshot(economyStart.ActiveActorCount),
                hasRemainingOpportunities: true)
            : BattleEncounterScheduleStepOutcome.BoundaryCompleted();
        return Cursor.From(policy.Advance(Request(cursor, outcome, participants)));
    }

    private static Cursor Command(
        IBattleEncounterSchedulePolicy policy,
        Cursor cursor,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        bool hasRemainingOpportunities)
    {
        BattleEncounterCommandWindowScheduleStep command =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        return Cursor.From(policy.Advance(
            Request(
                cursor,
                BattleEncounterScheduleStepOutcome.CommandCommitted(
                    command.ActorId,
                    ActionTurnConsumption.Normal,
                    new StandardActionTurnEconomySnapshot(hasRemainingOpportunities ? 2 : 1),
                    new StandardActionTurnEconomySnapshot(hasRemainingOpportunities ? 1 : 0),
                    hasRemainingOpportunities),
                participants)));
    }

    private static BattleEncounterScheduleAdvanceRequest Request(
        Cursor cursor,
        BattleEncounterScheduleStepOutcome outcome,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants) =>
        new(cursor.State, cursor.Step, outcome, participants);

    private static T AssertStep<T>(Cursor cursor, int round)
        where T : BattleEncounterScheduleStep
    {
        T step = Assert.IsType<T>(cursor.Step);
        Assert.Equal(round, step.RoundNumber);
        Assert.Equal(
            TeamPhaseRoundRobinBattleEncounterSchedulePolicy.ScheduleId,
            step.PolicyId);
        return step;
    }

    private static BattleEncounterScheduleParticipantSnapshot Participant(
        RuntimeInstanceId instanceId,
        ContentId teamId,
        bool deployed = true,
        bool defeated = false) =>
        new(instanceId, teamId, deployed, defeated);

    private static BattleEncounterScheduleParticipantSnapshot[] Participants() =>
    [
        Participant(PlayerA, PlayerTeam),
        Participant(PlayerB, PlayerTeam),
        Participant(Enemy, EnemyTeam)
    ];

    private sealed record Cursor(
        BattleEncounterScheduleStateSnapshot State,
        BattleEncounterScheduleStep Step)
    {
        public static Cursor From(BattleEncounterScheduleTransitionResult transition)
        {
            Assert.True(
                transition.Status is BattleEncounterScheduleTransitionStatus.Started
                    or BattleEncounterScheduleTransitionStatus.Advanced);
            return new Cursor(
                Assert.IsAssignableFrom<BattleEncounterScheduleStateSnapshot>(transition.After),
                Assert.IsAssignableFrom<BattleEncounterScheduleStep>(transition.NextStep));
        }
    }

    private sealed class ForeignPolicy : IBattleEncounterSchedulePolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("foreign");

        public BattleEncounterScheduleTransitionResult Start(
            BattleEncounterScheduleStartRequest request)
        {
            var state = new ForeignState(request, PolicyId);
            return BattleEncounterScheduleTransitionResult.Start(
                state,
                new BattleEncounterRoundStartedScheduleStep(PolicyId, 0, 1));
        }

        public BattleEncounterScheduleTransitionResult Advance(
            BattleEncounterScheduleAdvanceRequest request) =>
            throw new NotSupportedException();

        private sealed class ForeignState : BattleEncounterScheduleStateSnapshot
        {
            public ForeignState(BattleEncounterScheduleStartRequest request, ContentId policyId)
                : base(
                    policyId,
                    revision: 0,
                    currentRound: 1,
                    completedRounds: 0,
                    nextStepSequence: 0,
                    request.Participants.Select(participant => participant.InstanceId),
                    request.TeamOrder,
                    request.RoundLimit)
            {
            }
        }
    }
}
