using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Encounters;

public sealed class AgilityOrderedBattleEncounterScheduleTests
{
    private static readonly ContentId Agility = ContentId.Parse("agility");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly RuntimeInstanceId Player = RuntimeInstanceId.Parse("player");
    private static readonly RuntimeInstanceId FastEnemy = RuntimeInstanceId.Parse("fast_enemy");
    private static readonly RuntimeInstanceId SlowEnemy = RuntimeInstanceId.Parse("slow_enemy");

    [Fact]
    public void TieBreakRequest_DetachesItsParticipantCollection()
    {
        var source = new List<BattleEncounterScheduleParticipantSnapshot>
        {
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 10m)
        };
        var request = new BattleEncounterScheduleTieBreakRequest(
            source,
            Agility,
            orderingStatValue: 10m,
            roundNumber: 1);

        source.Clear();

        Assert.Equal([Player, FastEnemy], request.Participants.Select(item => item.InstanceId));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleEncounterScheduleParticipantSnapshot>)request.Participants).Clear());
    }

    [Fact]
    public void Schedule_OrdersAvailableActorsAcrossTeamsByDescendingAgility()
    {
        var policy = Policy();
        BattleEncounterScheduleParticipantSnapshot[] participants =
        [
            Participant(Player, PlayerTeam, agility: 7m),
            Participant(FastEnemy, EnemyTeam, agility: 12m),
            Participant(SlowEnemy, EnemyTeam, agility: 3m)
        ];
        Cursor cursor = Start(policy, participants, roundLimit: 1);

        cursor = Boundary(policy, cursor, participants);
        cursor = CompleteActor(policy, cursor, participants, FastEnemy, EnemyTeam);
        cursor = CompleteActor(policy, cursor, participants, Player, PlayerTeam);
        cursor = CompleteActor(policy, cursor, participants, SlowEnemy, EnemyTeam);

        Assert.IsType<BattleEncounterRoundEndedScheduleStep>(cursor.Step);
    }

    [Fact]
    public void Schedule_DelegatesEqualAgilityOrderToTheInjectedTieBreakPolicy()
    {
        var tieBreak = new ReverseTieBreakPolicy();
        var policy = new AgilityOrderedBattleEncounterSchedulePolicy(Agility, tieBreak);
        BattleEncounterScheduleParticipantSnapshot[] participants =
        [
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 10m)
        ];
        Cursor cursor = Start(policy, participants, roundLimit: 1);

        cursor = Boundary(policy, cursor, participants);
        cursor = OpenCommand(policy, cursor, participants, FastEnemy, EnemyTeam);

        Assert.Equal(1, tieBreak.Calls);
        Assert.Equal(Agility, tieBreak.LastRequest!.OrderingStatId);
        Assert.Equal(10m, tieBreak.LastRequest.OrderingStatValue);
        Assert.Equal(1, tieBreak.LastRequest.RoundNumber);
    }

    [Fact]
    public void Schedule_RejectsMissingNegativeAndInvalidTieBreakInputsWithoutStateMutation()
    {
        var policy = Policy();
        BattleEncounterScheduleParticipantSnapshot[] missing =
        [
            new(Player, PlayerTeam, isDeployed: true, isDefeated: false),
            Participant(FastEnemy, EnemyTeam, agility: 4m)
        ];
        Cursor missingCursor = Start(policy, missing, roundLimit: 1);

        BattleEncounterScheduleTransitionResult missingResult = policy.Advance(
            Request(
                missingCursor,
                BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
                missing));

        Assert.Equal(BattleEncounterScheduleTransitionStatus.Rejected, missingResult.Status);
        Assert.Same(missingCursor.State, missingResult.Before);
        Assert.Same(missingCursor.State, missingResult.After);
        Assert.Equal(
            BattleEncounterScheduleDiagnosticCode.MissingOrderingStat,
            Assert.Single(missingResult.Diagnostics).Code);

        BattleEncounterScheduleParticipantSnapshot[] negative =
        [
            Participant(Player, PlayerTeam, agility: -1m),
            Participant(FastEnemy, EnemyTeam, agility: 4m)
        ];
        Cursor negativeCursor = Start(policy, negative, roundLimit: 1);
        BattleEncounterScheduleTransitionResult negativeResult = policy.Advance(
            Request(
                negativeCursor,
                BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
                negative));
        Assert.Equal(
            BattleEncounterScheduleDiagnosticCode.InvalidOrderingStat,
            Assert.Single(negativeResult.Diagnostics).Code);

        var invalidTiePolicy = new AgilityOrderedBattleEncounterSchedulePolicy(
            Agility,
            new DuplicateTieBreakPolicy());
        BattleEncounterScheduleParticipantSnapshot[] tied =
        [
            Participant(Player, PlayerTeam, agility: 4m),
            Participant(FastEnemy, EnemyTeam, agility: 4m)
        ];
        Cursor invalidTieCursor = Start(invalidTiePolicy, tied, roundLimit: 1);
        BattleEncounterScheduleTransitionResult invalidTie = invalidTiePolicy.Advance(
            Request(
                invalidTieCursor,
                BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
                tied));
        Assert.Equal(
            BattleEncounterScheduleDiagnosticCode.InvalidTieBreakOrder,
            Assert.Single(invalidTie.Diagnostics).Code);
    }

    [Fact]
    public void Schedule_SkipsAnActorWhoBecomesUnavailableBeforeItsCommand()
    {
        var policy = Policy();
        BattleEncounterScheduleParticipantSnapshot[] participants =
        [
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 5m)
        ];
        Cursor cursor = Start(policy, participants, roundLimit: 1);
        cursor = Boundary(policy, cursor, participants);
        cursor = OpenCommand(policy, cursor, participants, Player, PlayerTeam);
        BattleEncounterCommandWindowScheduleStep command =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);

        participants =
        [
            Participant(Player, PlayerTeam, agility: 10m, deployed: false),
            Participant(FastEnemy, EnemyTeam, agility: 5m)
        ];
        cursor = Advance(
            policy,
            cursor,
            BattleEncounterScheduleStepOutcome.ActorUnavailable(command.ActorId),
            participants);
        Assert.IsType<BattleEncounterPhaseEndedScheduleStep>(cursor.Step);
        cursor = Boundary(policy, cursor, participants);
        _ = OpenCommand(policy, cursor, participants, FastEnemy, EnemyTeam);
    }

    [Fact]
    public void Schedule_NewDeploymentWaitsUntilTheNextRound()
    {
        var policy = Policy();
        BattleEncounterScheduleParticipantSnapshot[] participants =
        [
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 20m, deployed: false),
            Participant(SlowEnemy, EnemyTeam, agility: 5m)
        ];
        Cursor cursor = Start(policy, participants, roundLimit: 2);
        cursor = Boundary(policy, cursor, participants);

        participants =
        [
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 20m),
            Participant(SlowEnemy, EnemyTeam, agility: 5m)
        ];
        cursor = CompleteActor(policy, cursor, participants, Player, PlayerTeam);
        cursor = CompleteActor(policy, cursor, participants, SlowEnemy, EnemyTeam);
        Assert.IsType<BattleEncounterRoundEndedScheduleStep>(cursor.Step);

        cursor = Boundary(policy, cursor, participants);
        Assert.Equal(2, Assert.IsType<BattleEncounterRoundStartedScheduleStep>(cursor.Step).RoundNumber);
        cursor = Boundary(policy, cursor, participants);
        _ = OpenCommand(policy, cursor, participants, FastEnemy, EnemyTeam);
    }

    [Fact]
    public void Schedule_ReevaluatesAgilityAtEachRoundBoundary()
    {
        var policy = Policy();
        BattleEncounterScheduleParticipantSnapshot[] firstRound =
        [
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 5m)
        ];
        Cursor cursor = Start(policy, firstRound, roundLimit: 2);
        cursor = Boundary(policy, cursor, firstRound);
        cursor = CompleteActor(policy, cursor, firstRound, Player, PlayerTeam);
        cursor = CompleteActor(policy, cursor, firstRound, FastEnemy, EnemyTeam);

        BattleEncounterScheduleParticipantSnapshot[] secondRound =
        [
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 15m)
        ];
        cursor = Boundary(policy, cursor, secondRound);
        cursor = Boundary(policy, cursor, secondRound);
        _ = OpenCommand(policy, cursor, secondRound, FastEnemy, EnemyTeam);
    }

    [Fact]
    public void Schedule_RetainsTheCurrentActorWhileItsEconomyHasAnOpportunity()
    {
        var policy = Policy();
        BattleEncounterScheduleParticipantSnapshot[] participants =
        [
            Participant(Player, PlayerTeam, agility: 10m),
            Participant(FastEnemy, EnemyTeam, agility: 5m)
        ];
        Cursor cursor = Start(policy, participants, roundLimit: 1);
        cursor = Boundary(policy, cursor, participants);
        cursor = OpenCommand(policy, cursor, participants, Player, PlayerTeam);
        BattleEncounterCommandWindowScheduleStep first =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);

        cursor = Advance(
            policy,
            cursor,
            BattleEncounterScheduleStepOutcome.CommandCommitted(
                first.ActorId,
                ActionTurnConsumption.Pass,
                new StandardActionTurnEconomySnapshot(1),
                new StandardActionTurnEconomySnapshot(1),
                hasRemainingOpportunities: true),
            participants);

        BattleEncounterCommandWindowScheduleStep retained =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        Assert.Equal(first.ActorId, retained.ActorId);
        Assert.Equal(first.TeamId, retained.TeamId);
    }

    private static AgilityOrderedBattleEncounterSchedulePolicy Policy() =>
        new(Agility, new EncounterOrderBattleEncounterScheduleTieBreakPolicy());

    private static Cursor Start(
        IBattleEncounterSchedulePolicy policy,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        int roundLimit) =>
        Cursor.From(policy.Start(
            new BattleEncounterScheduleStartRequest(
                participants,
                [PlayerTeam, EnemyTeam],
                roundLimit)));

    private static Cursor CompleteActor(
        IBattleEncounterSchedulePolicy policy,
        Cursor cursor,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        RuntimeInstanceId expectedActor,
        ContentId expectedTeam)
    {
        cursor = OpenCommand(
            policy,
            cursor,
            participants,
            expectedActor,
            expectedTeam);
        BattleEncounterCommandWindowScheduleStep command =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        cursor = Advance(
            policy,
            cursor,
            BattleEncounterScheduleStepOutcome.CommandCommitted(
                command.ActorId,
                ActionTurnConsumption.Normal,
                new StandardActionTurnEconomySnapshot(1),
                new StandardActionTurnEconomySnapshot(0),
                hasRemainingOpportunities: false),
            participants);
        Assert.IsType<BattleEncounterPhaseEndedScheduleStep>(cursor.Step);
        return Boundary(policy, cursor, participants);
    }

    private static Cursor OpenCommand(
        IBattleEncounterSchedulePolicy policy,
        Cursor cursor,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants,
        RuntimeInstanceId expectedActor,
        ContentId expectedTeam)
    {
        BattleEncounterPhaseStartedScheduleStep phase =
            Assert.IsType<BattleEncounterPhaseStartedScheduleStep>(cursor.Step);
        Assert.Equal(expectedTeam, phase.TeamId);
        Assert.Equal(1, phase.TurnEconomyStart!.ActiveActorCount);

        cursor = Boundary(policy, cursor, participants);
        BattleEncounterCommandWindowScheduleStep command =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        Assert.Equal(expectedActor, command.ActorId);
        Assert.Equal(expectedTeam, command.TeamId);
        return cursor;
    }

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
        return Advance(policy, cursor, outcome, participants);
    }

    private static Cursor Advance(
        IBattleEncounterSchedulePolicy policy,
        Cursor cursor,
        BattleEncounterScheduleStepOutcome outcome,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants) =>
        Cursor.From(policy.Advance(Request(cursor, outcome, participants)));

    private static BattleEncounterScheduleAdvanceRequest Request(
        Cursor cursor,
        BattleEncounterScheduleStepOutcome outcome,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants) =>
        new(cursor.State, cursor.Step, outcome, participants);

    private static BattleEncounterScheduleParticipantSnapshot Participant(
        RuntimeInstanceId instanceId,
        ContentId teamId,
        decimal agility,
        bool deployed = true,
        bool defeated = false) =>
        new(
            instanceId,
            teamId,
            deployed,
            defeated,
            [new KeyValuePair<ContentId, decimal>(Agility, agility)]);

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

    private sealed class ReverseTieBreakPolicy : IBattleEncounterScheduleTieBreakPolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("reverse_tie_break");
        public int Calls { get; private set; }
        public BattleEncounterScheduleTieBreakRequest? LastRequest { get; private set; }

        public IReadOnlyList<RuntimeInstanceId> Order(
            BattleEncounterScheduleTieBreakRequest request)
        {
            Calls++;
            LastRequest = request;
            return request.Participants
                .Select(participant => participant.InstanceId)
                .Reverse()
                .ToArray();
        }
    }

    private sealed class DuplicateTieBreakPolicy : IBattleEncounterScheduleTieBreakPolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("duplicate_tie_break");

        public IReadOnlyList<RuntimeInstanceId> Order(
            BattleEncounterScheduleTieBreakRequest request) =>
            Enumerable.Repeat(request.Participants[0].InstanceId, request.Participants.Count)
                .ToArray();
    }
}
