using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeDungeonTraversalTests
{
    [Fact]
    public void Traversal_UsesArbitraryNodesAndRequiresExplicitReverseTransitions()
    {
        var policy = new MutableDungeonPolicy { IsAllowed = true };
        var service = new RuntimeDungeonTraversalService(policy);
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry_scene"));
        var enterRoom = new RuntimeDungeonTraversalTransition(
            Id("enter_reading_room"),
            Id("archive"),
            Id("entry_scene"),
            Id("reading_room"));
        var leaveRoom = new RuntimeDungeonTraversalTransition(
            Id("leave_reading_room"),
            Id("archive"),
            Id("reading_room"),
            Id("entry_scene"));

        RuntimeDungeonTraversalResult entered = service.Traverse(initial, enterRoom);
        RuntimeDungeonTraversalResult wrongDirection = service.Traverse(entered.After, enterRoom);

        Assert.True(entered.Applied);
        Assert.Equal(Id("reading_room"), entered.After.CurrentNodeId);
        Assert.Equal([Id("entry_scene"), Id("reading_room")], entered.After.VisitedNodeIds);
        RuntimeDungeonTraversalEvent movement = Assert.Single(entered.Events);
        Assert.Equal(RuntimeDungeonTraversalEventKind.TransitionApplied, movement.Kind);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeDungeonTraversalEvent>)entered.Events).Add(movement));

        Assert.False(wrongDirection.Applied);
        Assert.Equal(RuntimeDungeonTraversalCode.SourceMismatch, wrongDirection.Code);
        Assert.Same(entered.After, wrongDirection.After);
        Assert.Equal(1, policy.EvaluationCount);

        RuntimeDungeonTraversalResult returned = service.Traverse(entered.After, leaveRoom);
        Assert.True(returned.Applied);
        Assert.Equal(initial.CurrentNodeId, returned.After.CurrentNodeId);
    }

    [Fact]
    public void Traversal_UsesInjectedPolicyForBarriersWithoutStartingEncounters()
    {
        var policy = new MutableDungeonPolicy
        {
            IsAllowed = false,
            ReasonId = Id("sealed_door"),
            Message = "The route is sealed."
        };
        var service = new RuntimeDungeonTraversalService(policy);
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("reading_room"));
        var transition = new RuntimeDungeonTraversalTransition(
            Id("open_restricted_stacks"),
            Id("archive"),
            Id("reading_room"),
            Id("restricted_stacks"));

        RuntimeDungeonTraversalResult blocked = service.Traverse(initial, transition);

        Assert.False(blocked.Applied);
        Assert.Equal(RuntimeDungeonTraversalCode.PolicyRejected, blocked.Code);
        Assert.Same(initial, blocked.After);
        Assert.Equal(Id("sealed_door"), blocked.ReasonId);
        Assert.Equal(RuntimeDungeonTraversalEventKind.TransitionRejected, Assert.Single(blocked.Events).Kind);
    }

    [Fact]
    public void Traversal_RejectsWrongDungeonBeforeCallingPolicy()
    {
        var policy = new MutableDungeonPolicy { IsAllowed = true };
        var current = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var wrongDungeon = new RuntimeDungeonTraversalTransition(
            Id("move"),
            Id("other_dungeon"),
            Id("entry"),
            Id("room"));

        RuntimeDungeonTraversalResult result =
            new RuntimeDungeonTraversalService(policy).Traverse(current, wrongDungeon);

        Assert.Equal(RuntimeDungeonTraversalCode.DungeonMismatch, result.Code);
        Assert.Same(current, result.After);
        Assert.Equal(0, policy.EvaluationCount);
    }

    [Fact]
    public void DungeonTraversal_RecordsCheckpointsAndBossesIdempotently()
    {
        var service = new RuntimeDungeonTraversalService(
            new MutableDungeonPolicy { IsAllowed = true });
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));

        RuntimeDungeonStateChangeResult checkpoint =
            service.UnlockCheckpoint(initial, Id("reading_room_terminal"));
        RuntimeDungeonStateChangeResult duplicateCheckpoint =
            service.UnlockCheckpoint(checkpoint.After, Id("reading_room_terminal"));
        RuntimeDungeonStateChangeResult boss =
            service.RegisterBossDefeat(checkpoint.After, Id("paper_guardian"));
        RuntimeDungeonStateChangeResult duplicateBoss =
            service.RegisterBossDefeat(boss.After, Id("paper_guardian"));

        Assert.True(checkpoint.Applied);
        Assert.Equal([Id("reading_room_terminal")], checkpoint.After.UnlockedCheckpointIds);
        Assert.Equal(RuntimeDungeonTraversalEventKind.CheckpointUnlocked, Assert.Single(checkpoint.Events).Kind);
        Assert.Equal(RuntimeDungeonStateChangeCode.AlreadyRecorded, duplicateCheckpoint.Code);
        Assert.Same(checkpoint.After, duplicateCheckpoint.After);

        Assert.True(boss.Applied);
        Assert.Equal([Id("paper_guardian")], boss.After.DefeatedBossIds);
        Assert.Equal(RuntimeDungeonTraversalEventKind.BossDefeated, Assert.Single(boss.Events).Kind);
        Assert.Equal(RuntimeDungeonStateChangeCode.AlreadyRecorded, duplicateBoss.Code);
        Assert.Same(boss.After, duplicateBoss.After);
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class MutableDungeonPolicy : IRuntimeDungeonTraversalPolicy
    {
        public bool IsAllowed { get; set; }
        public ContentId? ReasonId { get; init; }
        public string? Message { get; init; }
        public int EvaluationCount { get; private set; }

        public RuntimeDungeonTraversalPolicyDecision Evaluate(RuntimeDungeonTraversalPolicyRequest request)
        {
            EvaluationCount++;
            return new RuntimeDungeonTraversalPolicyDecision(IsAllowed, ReasonId, Message);
        }
    }
}
