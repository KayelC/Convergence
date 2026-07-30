using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Encounters;

public sealed class BattleEncounterSchedulingContractTests
{
    private static readonly ContentId Policy = ContentId.Parse("test_schedule");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId Agility = ContentId.Parse("agility");
    private static readonly RuntimeInstanceId Player = RuntimeInstanceId.Parse("player");
    private static readonly RuntimeInstanceId Enemy = RuntimeInstanceId.Parse("enemy");

    [Fact]
    public void StartRequest_SnapshotsDetachedParticipantsStatsAndTeamOrder()
    {
        var stats = new Dictionary<ContentId, decimal> { [Agility] = 12m };
        var participants = new List<BattleEncounterScheduleParticipantSnapshot>
        {
            Participant(Player, PlayerTeam, stats),
            Participant(Enemy, EnemyTeam)
        };
        var teams = new List<ContentId> { PlayerTeam, EnemyTeam };

        var request = new BattleEncounterScheduleStartRequest(participants, teams, roundLimit: 8);

        stats[Agility] = 99m;
        participants.Clear();
        teams.Reverse();

        Assert.Equal([Player, Enemy], request.Participants.Select(value => value.InstanceId));
        Assert.Equal(12m, request.Participants[0].EffectiveStats[Agility]);
        Assert.Equal([PlayerTeam, EnemyTeam], request.TeamOrder);
        Assert.Equal(8, request.RoundLimit);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleEncounterScheduleParticipantSnapshot>)request.Participants)
            .Add(Participant(RuntimeInstanceId.Parse("late"), PlayerTeam)));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ContentId, decimal>)request.Participants[0].EffectiveStats)
            .Add(ContentId.Parse("late_stat"), 1m));
    }

    [Fact]
    public void StartRequest_RejectsDuplicateActorsAndIncompleteTeamOrder()
    {
        BattleEncounterScheduleParticipantSnapshot player = Participant(Player, PlayerTeam);

        Assert.Throws<ArgumentException>(() =>
            new BattleEncounterScheduleStartRequest([player, player], [PlayerTeam], 1));
        Assert.Throws<ArgumentException>(() =>
            new BattleEncounterScheduleStartRequest(
                [player, Participant(Enemy, EnemyTeam)],
                [PlayerTeam],
                1));
        Assert.Throws<ArgumentException>(() =>
            new BattleEncounterScheduleStartRequest([player], [PlayerTeam, PlayerTeam], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterScheduleStartRequest([player], [PlayerTeam], 0));
    }

    [Fact]
    public void Steps_ExpressStructuralBoundariesAndExplicitEconomyScopes()
    {
        var round = new BattleEncounterRoundStartedScheduleStep(Policy, 0, 1);
        var phase = new BattleEncounterPhaseStartedScheduleStep(
            Policy,
            1,
            1,
            PlayerTeam,
            new BattleEncounterTurnEconomyStart(2));
        var command = new BattleEncounterCommandWindowScheduleStep(
            Policy,
            2,
            1,
            Player,
            PlayerTeam);
        var actorScopedCommand = new BattleEncounterCommandWindowScheduleStep(
            Policy,
            3,
            1,
            Player,
            PlayerTeam,
            new BattleEncounterTurnEconomyStart(1));
        var phaseEnd = new BattleEncounterPhaseEndedScheduleStep(Policy, 4, 1, PlayerTeam);
        var roundEnd = new BattleEncounterRoundEndedScheduleStep(Policy, 5, 1);

        Assert.Equal(BattleEncounterScheduleStepKind.RoundStarted, round.Kind);
        Assert.Equal(2, phase.TurnEconomyStart!.ActiveActorCount);
        Assert.Null(command.TurnEconomyStart);
        Assert.Equal(1, actorScopedCommand.TurnEconomyStart!.ActiveActorCount);
        Assert.Equal(BattleEncounterScheduleStepKind.PhaseEnded, phaseEnd.Kind);
        Assert.Equal(BattleEncounterScheduleStepKind.RoundEnded, roundEnd.Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleEncounterTurnEconomyStart(0));
    }

    [Fact]
    public void AdvanceRequest_AcceptsOnlyEvidenceMatchingThePendingStep()
    {
        TestScheduleState state = State(revision: 0, sequence: 0);
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        var round = new BattleEncounterRoundStartedScheduleStep(Policy, 0, 1);

        var boundary = new BattleEncounterScheduleAdvanceRequest(
            state,
            round,
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            participants);

        Assert.Same(state, boundary.State);
        Assert.Equal(BattleEncounterScheduleStepOutcomeKind.BoundaryCompleted, boundary.Outcome.Kind);

        var command = new BattleEncounterCommandWindowScheduleStep(
            Policy,
            0,
            1,
            Player,
            PlayerTeam);
        var before = new StandardActionTurnEconomySnapshot(1);
        var after = new StandardActionTurnEconomySnapshot(0);
        var committed = new BattleEncounterScheduleAdvanceRequest(
            state,
            command,
            BattleEncounterScheduleStepOutcome.CommandCommitted(
                Player,
                ActionTurnConsumption.Normal,
                before,
                after,
                hasRemainingOpportunities: false),
            participants);

        Assert.Same(ActionTurnConsumption.Normal, committed.Outcome.TurnConsumption);
        Assert.Same(before, committed.Outcome.EconomyBefore);
        Assert.Same(after, committed.Outcome.EconomyAfter);
        Assert.False(committed.Outcome.HasRemainingOpportunities);

        Assert.Throws<ArgumentException>(() => new BattleEncounterScheduleAdvanceRequest(
            state,
            round,
            BattleEncounterScheduleStepOutcome.ActorUnavailable(Player),
            participants));
        Assert.Throws<ArgumentException>(() => new BattleEncounterScheduleAdvanceRequest(
            state,
            command,
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            participants));
        Assert.Throws<ArgumentException>(() => new BattleEncounterScheduleAdvanceRequest(
            state,
            command,
            BattleEncounterScheduleStepOutcome.ActorUnavailable(Enemy),
            participants));
    }

    [Fact]
    public void AdvanceRequest_RejectsPolicySequenceRoundAndParticipantGraphDrift()
    {
        TestScheduleState state = State(revision: 0, sequence: 4);
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();

        Assert.Throws<ArgumentException>(() => new BattleEncounterScheduleAdvanceRequest(
            state,
            new BattleEncounterRoundStartedScheduleStep(ContentId.Parse("other_policy"), 4, 1),
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            participants));
        Assert.Throws<ArgumentException>(() => new BattleEncounterScheduleAdvanceRequest(
            state,
            new BattleEncounterRoundStartedScheduleStep(Policy, 5, 1),
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            participants));
        Assert.Throws<ArgumentException>(() => new BattleEncounterScheduleAdvanceRequest(
            state,
            new BattleEncounterRoundStartedScheduleStep(Policy, 4, 2),
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            participants));
        Assert.Throws<ArgumentException>(() => new BattleEncounterScheduleAdvanceRequest(
            state,
            new BattleEncounterRoundStartedScheduleStep(Policy, 4, 1),
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            participants.Reverse()));
    }

    [Fact]
    public void TransitionResult_ValidatesStateMachineContinuity()
    {
        TestScheduleState initial = State(revision: 0, sequence: 0);
        var firstStep = new BattleEncounterRoundStartedScheduleStep(Policy, 0, 1);
        BattleEncounterScheduleTransitionResult started =
            BattleEncounterScheduleTransitionResult.Start(initial, firstStep);

        TestScheduleState advancedState = State(revision: 1, sequence: 1);
        var secondStep = new BattleEncounterPhaseStartedScheduleStep(
            Policy,
            1,
            1,
            PlayerTeam,
            new BattleEncounterTurnEconomyStart(1));
        BattleEncounterScheduleTransitionResult advanced =
            BattleEncounterScheduleTransitionResult.Advance(initial, advancedState, secondStep);

        TestScheduleState completedState = State(
            revision: 2,
            sequence: 2,
            currentRound: 1,
            completedRounds: 1);
        BattleEncounterScheduleTransitionResult completed =
            BattleEncounterScheduleTransitionResult.Complete(advancedState, completedState);

        Assert.Equal(BattleEncounterScheduleTransitionStatus.Started, started.Status);
        Assert.Equal(BattleEncounterScheduleTransitionStatus.Advanced, advanced.Status);
        Assert.Equal(BattleEncounterScheduleTransitionStatus.Completed, completed.Status);
        Assert.Null(completed.NextStep);

        Assert.Throws<ArgumentException>(() => BattleEncounterScheduleTransitionResult.Start(
            State(revision: 1, sequence: 0),
            firstStep));
        Assert.Throws<ArgumentException>(() => BattleEncounterScheduleTransitionResult.Advance(
            initial,
            State(revision: 2, sequence: 1),
            secondStep));
        Assert.Throws<ArgumentException>(() => BattleEncounterScheduleTransitionResult.Advance(
            initial,
            State(revision: 1, sequence: 2),
            new BattleEncounterPhaseStartedScheduleStep(
                Policy,
                2,
                1,
                PlayerTeam,
                new BattleEncounterTurnEconomyStart(1))));
    }

    [Fact]
    public void Rejection_PreservesStateAndSnapshotsDiagnostics()
    {
        TestScheduleState state = State(revision: 0, sequence: 0);
        var diagnostics = new List<BattleEncounterScheduleDiagnostic>
        {
            new(BattleEncounterScheduleDiagnosticCode.PolicyRejected, "The route is unavailable.")
        };

        BattleEncounterScheduleTransitionResult result =
            BattleEncounterScheduleTransitionResult.RejectAdvance(state, diagnostics);
        diagnostics.Clear();

        Assert.Equal(BattleEncounterScheduleTransitionStatus.Rejected, result.Status);
        Assert.Same(state, result.Before);
        Assert.Same(state, result.After);
        Assert.Single(result.Diagnostics);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleEncounterScheduleDiagnostic>)result.Diagnostics)
            .Add(new BattleEncounterScheduleDiagnostic(
                BattleEncounterScheduleDiagnosticCode.InvalidState,
                "Late diagnostic.")));
        Assert.Throws<ArgumentException>(() =>
            BattleEncounterScheduleTransitionResult.RejectStart([]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterScheduleDiagnostic(
                (BattleEncounterScheduleDiagnosticCode)999,
                "Invalid."));
    }

    [Fact]
    public void PolicyContract_IsStatelessAndReceivesOnlyDetachedRequests()
    {
        var policy = new RecordingPolicy();
        var start = new BattleEncounterScheduleStartRequest(
            Participants(),
            [PlayerTeam, EnemyTeam],
            3);

        BattleEncounterScheduleTransitionResult started = policy.Start(start);
        BattleEncounterScheduleStateSnapshot state = Assert.IsType<TestScheduleState>(started.After);
        BattleEncounterScheduleStep step = Assert.IsType<BattleEncounterRoundStartedScheduleStep>(
            started.NextStep);
        var advance = new BattleEncounterScheduleAdvanceRequest(
            state,
            step,
            BattleEncounterScheduleStepOutcome.BoundaryCompleted(),
            start.Participants);

        BattleEncounterScheduleTransitionResult advanced = policy.Advance(advance);

        Assert.Equal(Policy, policy.PolicyId);
        Assert.Same(start, policy.StartRequest);
        Assert.Same(advance, policy.AdvanceRequest);
        Assert.Equal(BattleEncounterScheduleTransitionStatus.Completed, advanced.Status);
    }

    private static BattleEncounterScheduleParticipantSnapshot Participant(
        RuntimeInstanceId actorId,
        ContentId teamId,
        IEnumerable<KeyValuePair<ContentId, decimal>>? stats = null) =>
        new(actorId, teamId, isDeployed: true, isDefeated: false, stats);

    private static BattleEncounterScheduleParticipantSnapshot[] Participants() =>
        [Participant(Player, PlayerTeam), Participant(Enemy, EnemyTeam)];

    private static TestScheduleState State(
        long revision,
        long sequence,
        int currentRound = 1,
        int completedRounds = 0) =>
        new(
            revision,
            currentRound,
            completedRounds,
            sequence,
            [Player, Enemy],
            [PlayerTeam, EnemyTeam],
            roundLimit: 3);

    private sealed class TestScheduleState : BattleEncounterScheduleStateSnapshot
    {
        public TestScheduleState(
            long revision,
            int currentRound,
            int completedRounds,
            long nextStepSequence,
            IEnumerable<RuntimeInstanceId> participantIds,
            IEnumerable<ContentId> teamOrder,
            int roundLimit)
            : base(
                Policy,
                revision,
                currentRound,
                completedRounds,
                nextStepSequence,
                participantIds,
                teamOrder,
                roundLimit)
        {
        }
    }

    private sealed class RecordingPolicy : IBattleEncounterSchedulePolicy
    {
        public ContentId PolicyId => Policy;
        public BattleEncounterScheduleStartRequest? StartRequest { get; private set; }
        public BattleEncounterScheduleAdvanceRequest? AdvanceRequest { get; private set; }

        public BattleEncounterScheduleTransitionResult Start(BattleEncounterScheduleStartRequest request)
        {
            StartRequest = request;
            TestScheduleState state = State(revision: 0, sequence: 0);
            return BattleEncounterScheduleTransitionResult.Start(
                state,
                new BattleEncounterRoundStartedScheduleStep(PolicyId, 0, 1));
        }

        public BattleEncounterScheduleTransitionResult Advance(BattleEncounterScheduleAdvanceRequest request)
        {
            AdvanceRequest = request;
            return BattleEncounterScheduleTransitionResult.Complete(
                request.State,
                State(
                    revision: request.State.Revision + 1,
                    sequence: request.State.NextStepSequence + 1,
                    currentRound: request.State.CurrentRound,
                    completedRounds: request.State.CurrentRound));
        }
    }
}
