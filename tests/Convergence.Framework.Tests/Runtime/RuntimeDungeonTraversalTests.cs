using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeDungeonTraversalTests
{
    private static readonly RuntimeDungeonProgressRegistry EmptyRegistry = new([]);

    [Fact]
    public void Traversal_UsesArbitraryNodesAndRequiresExplicitReverseTransitions()
    {
        var policy = new MutableDungeonPolicy { IsAllowed = true };
        var service = new RuntimeDungeonTraversalService(policy, EmptyRegistry);
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
        var service = new RuntimeDungeonTraversalService(policy, EmptyRegistry);
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
            new RuntimeDungeonTraversalService(policy, EmptyRegistry).Traverse(current, wrongDungeon);

        Assert.Equal(RuntimeDungeonTraversalCode.DungeonMismatch, result.Code);
        Assert.Same(current, result.After);
        Assert.Equal(0, policy.EvaluationCount);
    }

    [Fact]
    public void Traversal_RejectsEveryEmptyRequestIdBeforeMismatchOrPolicy()
    {
        var policy = new MutableDungeonPolicy { IsAllowed = true };
        var service = new RuntimeDungeonTraversalService(policy, EmptyRegistry);
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
            RuntimeDungeonTraversalResult result = new RuntimeDungeonTraversalService(policy, EmptyRegistry).Traverse(initial, transition);
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
            new RuntimeDungeonTraversalService(new DelegateDungeonPolicy(_ => throw new OperationCanceledException()), EmptyRegistry)
                .Traverse(initial, transition));
        Assert.Throws<OutOfMemoryException>(() =>
            new RuntimeDungeonTraversalService(new DelegateDungeonPolicy(_ => throw new OutOfMemoryException()), EmptyRegistry)
                .Traverse(initial, transition));
        Assert.Equal([Id("entry")], initial.VisitedNodeIds);
    }

    [Fact]
    public void Traversal_ReportsMalformedPolicyDecisionAsFault()
    {
        var initial = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var transition = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));
        var service = new RuntimeDungeonTraversalService(
            new DelegateDungeonPolicy(_ => new RuntimeDungeonTraversalPolicyDecision(false, (ContentId?)default(ContentId))),
            EmptyRegistry);

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
            RuntimeDungeonStateChangeCode.Applied, before, moved, RuntimeDungeonProgressKind.Checkpoint, Id("terminal"), [checkpoint]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied, before, before, RuntimeDungeonProgressKind.Checkpoint, Id("terminal"), [checkpoint]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied, before, before, RuntimeDungeonProgressKind.Checkpoint, Id("terminal"), [otherDungeon]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.AlreadyRecorded, before, moved, RuntimeDungeonProgressKind.Checkpoint, Id("terminal")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeDungeonStateChangeResult(
            (RuntimeDungeonStateChangeCode)999, before, before, RuntimeDungeonProgressKind.Checkpoint, Id("terminal")));
    }

    [Fact]
    public void DungeonEventAndResults_DoNotPermitMutableCloneEvidence()
    {
        var before = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var transition = new RuntimeDungeonTraversalTransition(Id("move"), Id("archive"), Id("entry"), Id("room"));
        RuntimeDungeonTraversalResult result = new RuntimeDungeonTraversalService(
            new MutableDungeonPolicy { IsAllowed = true }, EmptyRegistry).Traverse(before, transition);
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
            new MutableDungeonPolicy { IsAllowed = true },
            new RuntimeDungeonProgressRegistry(
            [
                new RuntimeDungeonProgressEligibility(
                    RuntimeDungeonProgressKind.Checkpoint, Id("reading_room_terminal"), Id("archive"), [Id("entry")]),
                new RuntimeDungeonProgressEligibility(
                    RuntimeDungeonProgressKind.Boss, Id("paper_guardian"), Id("archive"), [Id("entry")])
            ]));
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

    [Fact]
    public void ProgressRegistry_DefensivelyCopiesAndRejectsAmbiguousDeclarations()
    {
        var areas = new List<ContentId> { Id("entry") };
        var declarations = new List<RuntimeDungeonProgressEligibility>
        {
            new(RuntimeDungeonProgressKind.Checkpoint, Id("terminal"), Id("archive"), areas)
        };
        var registry = new RuntimeDungeonProgressRegistry(declarations);
        areas.Add(Id("hall"));
        declarations.Clear();

        RuntimeDungeonProgressEligibility entry = Assert.Single(registry.Eligibility);
        Assert.Equal([Id("entry")], entry.AllowedNodeIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeDungeonProgressEligibility>)registry.Eligibility).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContentId>)entry.AllowedNodeIds).Add(Id("hall")));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonProgressRegistry([entry, entry]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonProgressEligibility(
            RuntimeDungeonProgressKind.Boss, default, Id("archive"), [Id("entry")]));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonProgressEligibility(
            RuntimeDungeonProgressKind.Boss, Id("guardian"), Id("archive"), []));
        Assert.Throws<ArgumentException>(() => new RuntimeDungeonProgressEligibility(
            RuntimeDungeonProgressKind.Boss, Id("guardian"), Id("archive"), [Id("entry"), Id("entry")]));
    }

    [Fact]
    public void ProgressReports_RejectInvalidUnknownWrongDungeonAndWrongAreaWithoutMutation()
    {
        var registry = new RuntimeDungeonProgressRegistry(
        [
            new RuntimeDungeonProgressEligibility(
                RuntimeDungeonProgressKind.Checkpoint, Id("terminal"), Id("archive"), [Id("entry")]),
            new RuntimeDungeonProgressEligibility(
                RuntimeDungeonProgressKind.Boss, Id("guardian"), Id("archive"), [Id("room")])
        ]);
        var service = new RuntimeDungeonTraversalService(new MutableDungeonPolicy { IsAllowed = true }, registry);
        var entry = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        var room = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("room"));
        var other = new RuntimeDungeonTraversalSnapshot(Id("other"), Id("entry"));
        var cases = new (RuntimeDungeonStateChangeResult Result, RuntimeDungeonStateChangeCode Code)[]
        {
            (service.UnlockCheckpoint(entry, default), RuntimeDungeonStateChangeCode.InvalidRequest),
            (service.UnlockCheckpoint(entry, Id("unknown")), RuntimeDungeonStateChangeCode.NotEligible),
            (service.UnlockCheckpoint(other, Id("terminal")), RuntimeDungeonStateChangeCode.DungeonMismatch),
            (service.UnlockCheckpoint(room, Id("terminal")), RuntimeDungeonStateChangeCode.AreaMismatch),
            (service.RegisterBossDefeat(entry, Id("guardian")), RuntimeDungeonStateChangeCode.AreaMismatch),
            (service.UnlockCheckpoint(new RuntimeDungeonTraversalSnapshot(default, Id("entry")), Id("terminal")),
                RuntimeDungeonStateChangeCode.InvalidRequest)
        };

        foreach (var (result, code) in cases)
        {
            Assert.Equal(code, result.Code);
            Assert.Same(result.Before, result.After);
            Assert.Empty(result.Events);
            Assert.Empty(result.After.UnlockedCheckpointIds);
            Assert.Empty(result.After.DefeatedBossIds);
        }

        RuntimeDungeonStateChangeResult first = service.UnlockCheckpoint(entry, Id("terminal"));
        RuntimeDungeonStateChangeResult wrongAreaDuplicate = service.UnlockCheckpoint(
            new RuntimeDungeonTraversalSnapshot(
                Id("archive"), Id("room"), unlockedCheckpointIds: first.After.UnlockedCheckpointIds),
            Id("terminal"));
        Assert.Equal(RuntimeDungeonStateChangeCode.AreaMismatch, wrongAreaDuplicate.Code);
    }

    [Fact]
    public void BossProgress_IsHostReportedAfterBattleOrPuzzleNotInferredFromTraversalOrLoss()
    {
        var registry = new RuntimeDungeonProgressRegistry(
        [
            new RuntimeDungeonProgressEligibility(
                RuntimeDungeonProgressKind.Boss, Id("battle_guardian"), Id("archive"), [Id("room")]),
            new RuntimeDungeonProgressEligibility(
                RuntimeDungeonProgressKind.Boss, Id("puzzle_guardian"), Id("archive"), [Id("room")])
        ]);
        var service = new RuntimeDungeonTraversalService(new MutableDungeonPolicy { IsAllowed = true }, registry);
        var entrance = new RuntimeDungeonTraversalSnapshot(Id("archive"), Id("entry"));
        RuntimeDungeonTraversalResult entered = service.Traverse(
            entrance, new RuntimeDungeonTraversalTransition(Id("door"), Id("archive"), Id("entry"), Id("room")));

        // A lost battle issues no success report, so traversal alone leaves both boss records absent.
        Assert.True(entered.Applied);
        Assert.Empty(entered.After.DefeatedBossIds);
        RuntimeDungeonStateChangeResult battleSuccess = service.RegisterBossDefeat(entered.After, Id("battle_guardian"));
        RuntimeDungeonStateChangeResult puzzleSuccess = service.RegisterBossDefeat(battleSuccess.After, Id("puzzle_guardian"));
        RuntimeDungeonStateChangeResult duplicate = service.RegisterBossDefeat(puzzleSuccess.After, Id("battle_guardian"));

        Assert.Equal(RuntimeDungeonStateChangeCode.Applied, battleSuccess.Code);
        Assert.Equal(RuntimeDungeonStateChangeCode.Applied, puzzleSuccess.Code);
        Assert.Equal([Id("battle_guardian"), Id("puzzle_guardian")], puzzleSuccess.After.DefeatedBossIds);
        Assert.Equal(RuntimeDungeonStateChangeCode.AlreadyRecorded, duplicate.Code);
        Assert.Same(puzzleSuccess.After, duplicate.After);
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
