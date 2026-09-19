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
    public void Traversal_RejectsEveryEmptyRequestIdBeforeMismatchOrPolicy()
    {
        var policy = new MutableDungeonPolicy { IsAllowed = true };
        var service = new RuntimeDungeonTraversalService(policy);
        var valid = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var cases = new (RuntimeDungeonTraversalSnapshot Current, RuntimeDungeonTraversalTransition Transition, RuntimeDungeonTraversalRequestField Field)[]
        {
            (new RuntimeDungeonTraversalSnapshot(default, Id("entry")), valid, RuntimeDungeonTraversalRequestField.CurrentDungeonId),
            (new RuntimeDungeonTraversalSnapshot(Id("archive"), default), valid, RuntimeDungeonTraversalRequestField.CurrentNodeId),
            (initial, valid with { Id = default }, RuntimeDungeonTraversalRequestField.TransitionId),
            (initial, valid with { DungeonId = default }, RuntimeDungeonTraversalRequestField.TransitionDungeonId),
            (initial, valid with { SourceNodeId = default }, RuntimeDungeonTraversalRequestField.SourceNodeId),
            (initial, valid with { DestinationNodeId = default }, RuntimeDungeonTraversalRequestField.DestinationNodeId)
        };

        foreach (var testCase in cases)
        {
            RuntimeDungeonTraversalResult result = service.Traverse(testCase.Current, testCase.Transition);
            Assert.Equal(RuntimeDungeonTraversalCode.InvalidRequest, result.Code);
            Assert.Equal(testCase.Field, result.InvalidField);
            Assert.Same(testCase.Current, result.After);
            Assert.Equal(testCase.Current.VisitedNodeIds, result.After.VisitedNodeIds);
            Assert.Empty(result.Events);
        }

        Assert.Equal(0, policy.EvaluationCount);
    }

    [Fact]
    public void Traversal_DistinguishesPolicyRejectionNullAndExceptionWithoutMutation()
    {
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var transition = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));

        foreach (var (policy, code, faultKind) in new (IRuntimeDungeonTraversalPolicy, RuntimeDungeonTraversalCode, RuntimeDungeonTraversalPolicyFaultKind?)[]
        {
            (new DelegateDungeonPolicy(_ => new RuntimeDungeonTraversalPolicyDecision(false)), RuntimeDungeonTraversalCode.PolicyRejected, null),
            (new DelegateDungeonPolicy(_ => null!), RuntimeDungeonTraversalCode.PolicyFaulted, RuntimeDungeonTraversalPolicyFaultKind.NullDecision),
            (new DelegateDungeonPolicy(_ => throw new InvalidOperationException("broken")), RuntimeDungeonTraversalCode.PolicyFaulted, RuntimeDungeonTraversalPolicyFaultKind.Exception)
        })
        {
            RuntimeDungeonTraversalResult result = new RuntimeDungeonTraversalService(policy).Traverse(initial, transition);
            Assert.Equal(code, result.Code);
            Assert.Equal(faultKind, result.FaultKind);
            Assert.Same(initial, result.After);
            Assert.Equal([Id("entry")], result.After.VisitedNodeIds);
            Assert.Equal(RuntimeDungeonTraversalEventKind.TransitionRejected, Assert.Single(result.Events).Kind);
        }
    }

    [Fact]
    public void Traversal_PropagatesCancellationAndFatalPolicyFailures()
    {
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var transition = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));
        Assert.Throws<OperationCanceledException>(() =>
            new RuntimeDungeonTraversalService(new DelegateDungeonPolicy(_ => throw new OperationCanceledException()))
                .Traverse(initial, transition));
        Assert.Throws<OutOfMemoryException>(() =>
            new RuntimeDungeonTraversalService(new DelegateDungeonPolicy(_ => throw new OutOfMemoryException()))
                .Traverse(initial, transition));
        Assert.Equal([Id("entry")], initial.VisitedNodeIds);
    }

    [Fact]
    public void Traversal_ReportsMalformedPolicyDecisionAsFault()
    {
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var transition = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));
        var service = new RuntimeDungeonTraversalService(
            new DelegateDungeonPolicy(_ => new RuntimeDungeonTraversalPolicyDecision(false, (ContentId?)default(ContentId))));

        RuntimeDungeonTraversalResult result = service.Traverse(initial, transition);

        Assert.Equal(RuntimeDungeonTraversalCode.PolicyFaulted, result.Code);
        Assert.Equal(RuntimeDungeonTraversalPolicyFaultKind.MalformedDecision, result.FaultKind);
        Assert.Same(initial, result.After);
    }

    [Fact]
    public void PublicTraversalResult_RejectsContradictoryCustomServiceEvidence()
    {
        var before = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var after = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("room"));
        var transition = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));
        var applied = new RuntimeDungeonTraversalEvent(
            RuntimeDungeonTraversalEventKind.TransitionApplied, Id("archive"), Id("move"), Id("entry"), Id("room"));
        var rejected = new RuntimeDungeonTraversalEvent(
            RuntimeDungeonTraversalEventKind.TransitionRejected, Id("archive"), Id("move"), Id("entry"), Id("room"));

        Assert.Throws<ArgumentException>(() => new RuntimeDungeonTraversalResult(
            RuntimeDungeonTraversalCode.Applied, before, after, transition, [rejected]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonTraversalResult(
            RuntimeDungeonTraversalCode.PolicyRejected, before, after, transition, [rejected]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonTraversalResult(
            RuntimeDungeonTraversalCode.Applied, before, before, transition, [applied]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonTraversalResult(
            RuntimeDungeonTraversalCode.Applied, before, after, transition,
            [applied with { }], reasonId: Id("wrong")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeDungeonTraversalResult(
            (RuntimeDungeonTraversalCode)999, before, after, transition, [applied]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonTraversalResult(
            RuntimeDungeonTraversalCode.InvalidRequest, before, before, transition));
    }

    [Fact]
    public void PublicProgressResult_RejectsUnrelatedMutationAndWrongEvent()
    {
        var before = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var moved = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("room"));
        var checkpoint = new RuntimeDungeonTraversalEvent(
            RuntimeDungeonTraversalEventKind.CheckpointUnlocked, Id("archive"), Id("terminal"));
        var otherDungeon = new RuntimeDungeonTraversalEvent(
            RuntimeDungeonTraversalEventKind.CheckpointUnlocked, Id("other"), Id("terminal"));

        Assert.Throws<ArgumentException>(() => new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied, before, moved, [checkpoint]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied, before, before, [checkpoint]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied, before, before, [otherDungeon]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.AlreadyRecorded, before, moved));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeDungeonStateChangeResult(
            (RuntimeDungeonStateChangeCode)999, before, before));
    }

    [Fact]
    public void DungeonEventAndResults_DoNotPermitMutableCloneEvidence()
    {
        var before = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var transition = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));
        RuntimeDungeonTraversalResult result = new RuntimeDungeonTraversalService(
            new MutableDungeonPolicy { IsAllowed = true }).Traverse(before, transition);
        RuntimeDungeonTraversalResult clone = result with { };

        Assert.Null(typeof(RuntimeDungeonTraversalEvent).GetProperty(nameof(RuntimeDungeonTraversalEvent.Kind))!.SetMethod);
        Assert.Null(typeof(RuntimeDungeonTraversalEvent).GetProperty(nameof(RuntimeDungeonTraversalEvent.ContentId))!.SetMethod);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeDungeonTraversalEvent>)clone.Events).Clear());
        Assert.Equal(result.After.CurrentNodeId, clone.After.CurrentNodeId);
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonTraversalEvent(
            RuntimeDungeonTraversalEventKind.TransitionApplied, Id("archive"), Id("move")));
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

    private sealed class DelegateDungeonPolicy(
        Func<RuntimeDungeonTraversalPolicyRequest, RuntimeDungeonTraversalPolicyDecision> evaluate)
        : IRuntimeDungeonTraversalPolicy
    {
        public RuntimeDungeonTraversalPolicyDecision Evaluate(RuntimeDungeonTraversalPolicyRequest request) => evaluate(request);
    }
}
