using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Encounters;

public sealed class BattleEncounterPostCommandSchedulingTests
{
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly RuntimeInstanceId PlayerA = RuntimeInstanceId.Parse("player_a");
    private static readonly RuntimeInstanceId PlayerB = RuntimeInstanceId.Parse("player_b");
    private static readonly RuntimeInstanceId PlayerC = RuntimeInstanceId.Parse("player_c");
    private static readonly RuntimeInstanceId Enemy = RuntimeInstanceId.Parse("enemy");

    [Fact]
    public void Extension_CanRetainTheSameActorForAnExistingOpportunity()
    {
        var extensionPolicy = new RecordingRetainPolicy();
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(
            new BattleEncounterPostCommandScheduleExtension(
                extensionPolicy,
                maximumConsecutiveImmediateRepeats: 2));
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = OpenFirstCommand(policy, participants);
        BattleEncounterCommandWindowScheduleStep first =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);

        cursor = Advance(
            policy,
            cursor,
            Committed(first, beforeActions: 3, afterActions: 2, hasRemaining: true),
            participants);

        BattleEncounterCommandWindowScheduleStep retained =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        Assert.Equal(PlayerA, retained.ActorId);
        BattleEncounterPostCommandScheduleRequest request =
            Assert.IsType<BattleEncounterPostCommandScheduleRequest>(extensionPolicy.LastRequest);
        Assert.Equal(PlayerA, request.ActorId);
        Assert.Equal(PlayerTeam, request.TeamId);
        Assert.Equal(ActionTurnConsumptionKind.Normal, request.TurnConsumption.Kind);
        Assert.Equal(3, request.EconomyBefore.RemainingActions);
        Assert.Equal(2, request.EconomyAfter.RemainingActions);
        Assert.True(request.HasRemainingOpportunities);
        Assert.Equal(0, request.ConsecutiveImmediateRepeats);
    }

    [Fact]
    public void Extension_CannotCreateAnOpportunityAfterTheEconomyIsExhausted()
    {
        var extensionPolicy = new RecordingRetainPolicy();
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(
            new BattleEncounterPostCommandScheduleExtension(extensionPolicy, 2));
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = OpenFirstCommand(policy, participants);
        BattleEncounterCommandWindowScheduleStep command =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);

        cursor = Advance(
            policy,
            cursor,
            Committed(command, beforeActions: 1, afterActions: 0, hasRemaining: false),
            participants);

        Assert.IsType<BattleEncounterPhaseEndedScheduleStep>(cursor.Step);
        Assert.Equal(0, extensionPolicy.Calls);
    }

    [Fact]
    public void Extension_RejectsImmediateRepeatsBeyondItsConfiguredLimit()
    {
        var extensionPolicy = new RecordingRetainPolicy();
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(
            new BattleEncounterPostCommandScheduleExtension(extensionPolicy, 1));
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = OpenFirstCommand(policy, participants);
        BattleEncounterCommandWindowScheduleStep first =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        cursor = Advance(
            policy,
            cursor,
            Committed(first, beforeActions: 3, afterActions: 2, hasRemaining: true),
            participants);
        BattleEncounterCommandWindowScheduleStep repeated =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);

        BattleEncounterScheduleTransitionResult result = policy.Advance(
            Request(
                cursor,
                Committed(repeated, beforeActions: 2, afterActions: 1, hasRemaining: true),
                participants));

        Assert.Equal(BattleEncounterScheduleTransitionStatus.Rejected, result.Status);
        Assert.Same(cursor.State, result.Before);
        Assert.Same(cursor.State, result.After);
        Assert.Equal(
            BattleEncounterScheduleDiagnosticCode.ImmediateRepeatLimitExceeded,
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal(2, extensionPolicy.Calls);
    }

    [Fact]
    public void DefaultAndFollowSchedulerDecisionsPreserveRoundRobinRotation()
    {
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        var defaultPolicy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy();
        Cursor defaultCursor = OpenFirstCommand(defaultPolicy, participants);
        BattleEncounterCommandWindowScheduleStep first =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(defaultCursor.Step);
        defaultCursor = Advance(
            defaultPolicy,
            defaultCursor,
            Committed(first, beforeActions: 3, afterActions: 2, hasRemaining: true),
            participants);

        var followPolicy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(
            new BattleEncounterPostCommandScheduleExtension(
                new FollowSchedulerPolicy(),
                maximumConsecutiveImmediateRepeats: 1));
        Cursor followCursor = OpenFirstCommand(followPolicy, participants);
        BattleEncounterCommandWindowScheduleStep followFirst =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(followCursor.Step);
        followCursor = Advance(
            followPolicy,
            followCursor,
            Committed(followFirst, beforeActions: 3, afterActions: 2, hasRemaining: true),
            participants);

        Assert.Equal(
            PlayerB,
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(defaultCursor.Step).ActorId);
        Assert.Equal(
            PlayerB,
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(followCursor.Step).ActorId);
    }

    [Fact]
    public void Extension_ImmediateRepeatResumesAtTheNextStableRingSlot()
    {
        var extensionPolicy = new RetainThenFollowPolicy();
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(
            new BattleEncounterPostCommandScheduleExtension(
                extensionPolicy,
                maximumConsecutiveImmediateRepeats: 1));
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = OpenFirstCommand(policy, participants);
        BattleEncounterCommandWindowScheduleStep first =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);

        cursor = Advance(
            policy,
            cursor,
            Committed(first, beforeActions: 3, afterActions: 2, hasRemaining: true),
            participants);
        BattleEncounterCommandWindowScheduleStep repeated =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        Assert.Equal(PlayerA, repeated.ActorId);

        cursor = Advance(
            policy,
            cursor,
            Committed(repeated, beforeActions: 2, afterActions: 1, hasRemaining: true),
            participants);

        Assert.Equal(
            PlayerB,
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step).ActorId);
        Assert.Equal(2, extensionPolicy.Calls);
    }

    [Fact]
    public void Extension_RejectsInvalidPolicyOutputWithoutAdvancingState()
    {
        var policy = new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(
            new BattleEncounterPostCommandScheduleExtension(
                new NullDecisionPolicy(),
                maximumConsecutiveImmediateRepeats: 1));
        BattleEncounterScheduleParticipantSnapshot[] participants = Participants();
        Cursor cursor = OpenFirstCommand(policy, participants);
        BattleEncounterCommandWindowScheduleStep command =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);

        BattleEncounterScheduleTransitionResult result = policy.Advance(
            Request(
                cursor,
                Committed(command, beforeActions: 3, afterActions: 2, hasRemaining: true),
                participants));

        Assert.Equal(BattleEncounterScheduleTransitionStatus.Rejected, result.Status);
        Assert.Same(cursor.State, result.Before);
        Assert.Same(cursor.State, result.After);
        Assert.Equal(
            BattleEncounterScheduleDiagnosticCode.InvalidPostCommandDecision,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Extension_RequiresAPositiveFiniteRepeatLimit()
    {
        var policy = new RecordingRetainPolicy();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterPostCommandScheduleExtension(policy, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BattleEncounterPostCommandScheduleExtension(policy, -1));
    }

    private static Cursor OpenFirstCommand(
        IBattleEncounterSchedulePolicy policy,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        Cursor cursor = Cursor.From(policy.Start(
            new BattleEncounterScheduleStartRequest(
                participants,
                [PlayerTeam, EnemyTeam],
                roundLimit: 1)));
        cursor = Boundary(policy, cursor, participants);
        cursor = Boundary(policy, cursor, participants);
        BattleEncounterCommandWindowScheduleStep command =
            Assert.IsType<BattleEncounterCommandWindowScheduleStep>(cursor.Step);
        Assert.Equal(PlayerA, command.ActorId);
        return cursor;
    }

    private static BattleEncounterScheduleStepOutcome Committed(
        BattleEncounterCommandWindowScheduleStep command,
        int beforeActions,
        int afterActions,
        bool hasRemaining) =>
        BattleEncounterScheduleStepOutcome.CommandCommitted(
            command.ActorId,
            ActionTurnConsumption.Normal,
            new StandardActionTurnEconomySnapshot(beforeActions),
            new StandardActionTurnEconomySnapshot(afterActions),
            hasRemaining);

    private static Cursor Boundary(
        IBattleEncounterSchedulePolicy policy,
        Cursor cursor,
        IReadOnlyList<BattleEncounterScheduleParticipantSnapshot> participants)
    {
        BattleEncounterScheduleStepOutcome outcome = cursor.Step.TurnEconomyStart is { } economy
            ? BattleEncounterScheduleStepOutcome.TurnEconomyStarted(
                new StandardActionTurnEconomySnapshot(economy.ActiveActorCount),
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

    private static BattleEncounterScheduleParticipantSnapshot[] Participants() =>
    [
        new(PlayerA, PlayerTeam, isDeployed: true, isDefeated: false),
        new(PlayerB, PlayerTeam, isDeployed: true, isDefeated: false),
        new(PlayerC, PlayerTeam, isDeployed: true, isDefeated: false),
        new(Enemy, EnemyTeam, isDeployed: true, isDefeated: false)
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

    private sealed class RecordingRetainPolicy : IBattleEncounterPostCommandSchedulePolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("recording_retain");
        public int Calls { get; private set; }
        public BattleEncounterPostCommandScheduleRequest? LastRequest { get; private set; }

        public BattleEncounterPostCommandScheduleDecision Decide(
            BattleEncounterPostCommandScheduleRequest request)
        {
            Calls++;
            LastRequest = request;
            return BattleEncounterPostCommandScheduleDecision.RetainActor();
        }
    }

    private sealed class FollowSchedulerPolicy : IBattleEncounterPostCommandSchedulePolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("follow_scheduler");

        public BattleEncounterPostCommandScheduleDecision Decide(
            BattleEncounterPostCommandScheduleRequest request) =>
            BattleEncounterPostCommandScheduleDecision.FollowScheduler();
    }

    private sealed class RetainThenFollowPolicy : IBattleEncounterPostCommandSchedulePolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("retain_then_follow");
        public int Calls { get; private set; }

        public BattleEncounterPostCommandScheduleDecision Decide(
            BattleEncounterPostCommandScheduleRequest request)
        {
            Calls++;
            return Calls == 1
                ? BattleEncounterPostCommandScheduleDecision.RetainActor()
                : BattleEncounterPostCommandScheduleDecision.FollowScheduler();
        }
    }

    private sealed class NullDecisionPolicy : IBattleEncounterPostCommandSchedulePolicy
    {
        public ContentId PolicyId { get; } = ContentId.Parse("null_decision");

        public BattleEncounterPostCommandScheduleDecision Decide(
            BattleEncounterPostCommandScheduleRequest request) =>
            null!;
    }
}
