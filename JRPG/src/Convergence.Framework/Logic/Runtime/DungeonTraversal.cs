using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Runtime;

public enum RuntimeDungeonTraversalCode
{
    Applied,
    DungeonMismatch,
    SourceMismatch,
    PolicyRejected
}

public enum RuntimeDungeonStateChangeCode
{
    Applied,
    AlreadyRecorded
}

public enum RuntimeDungeonTraversalEventKind
{
    TransitionApplied,
    TransitionRejected,
    CheckpointUnlocked,
    BossDefeated
}

public sealed record RuntimeDungeonTraversalSnapshot
{
    public RuntimeDungeonTraversalSnapshot(
        ContentId dungeonId,
        ContentId currentNodeId,
        IEnumerable<ContentId>? visitedNodeIds = null,
        IEnumerable<ContentId>? unlockedCheckpointIds = null,
        IEnumerable<ContentId>? defeatedBossIds = null)
    {
        DungeonId = dungeonId;
        CurrentNodeId = currentNodeId;
        VisitedNodeIds = RuntimeSnapshotCollections.List(
            (visitedNodeIds ?? []).Append(currentNodeId).Distinct());
        UnlockedCheckpointIds = RuntimeSnapshotCollections.List(
            (unlockedCheckpointIds ?? []).Distinct());
        DefeatedBossIds = RuntimeSnapshotCollections.List(
            (defeatedBossIds ?? []).Distinct());
    }

    public ContentId DungeonId { get; }
    public ContentId CurrentNodeId { get; }
    public IReadOnlyList<ContentId> VisitedNodeIds { get; }
    public IReadOnlyList<ContentId> UnlockedCheckpointIds { get; }
    public IReadOnlyList<ContentId> DefeatedBossIds { get; }

    public bool IsCheckpointUnlocked(ContentId checkpointId) =>
        UnlockedCheckpointIds.Contains(checkpointId);

    public bool IsBossDefeated(ContentId bossId) => DefeatedBossIds.Contains(bossId);

    internal RuntimeDungeonTraversalSnapshot MoveTo(ContentId destinationNodeId) =>
        new(
            DungeonId,
            destinationNodeId,
            VisitedNodeIds.Append(destinationNodeId),
            UnlockedCheckpointIds,
            DefeatedBossIds);

    internal RuntimeDungeonTraversalSnapshot UnlockCheckpoint(ContentId checkpointId) =>
        new(
            DungeonId,
            CurrentNodeId,
            VisitedNodeIds,
            UnlockedCheckpointIds.Append(checkpointId),
            DefeatedBossIds);

    internal RuntimeDungeonTraversalSnapshot MarkBossDefeated(ContentId bossId) =>
        new(
            DungeonId,
            CurrentNodeId,
            VisitedNodeIds,
            UnlockedCheckpointIds,
            DefeatedBossIds.Append(bossId));
}

public sealed record RuntimeDungeonTraversalTransition(
    ContentId Id,
    ContentId DungeonId,
    ContentId SourceNodeId,
    ContentId DestinationNodeId);

public sealed record RuntimeDungeonTraversalPolicyRequest(
    RuntimeDungeonTraversalSnapshot Current,
    RuntimeDungeonTraversalTransition Transition);

public sealed record RuntimeDungeonTraversalPolicyDecision(
    bool IsAllowed,
    ContentId? ReasonId = null,
    string? Message = null);

public sealed record RuntimeDungeonTraversalEvent(
    RuntimeDungeonTraversalEventKind Kind,
    ContentId DungeonId,
    ContentId ContentId,
    ContentId? SourceNodeId = null,
    ContentId? DestinationNodeId = null,
    ContentId? ReasonId = null,
    string? Message = null);

public sealed record RuntimeDungeonTraversalResult
{
    public RuntimeDungeonTraversalResult(
        RuntimeDungeonTraversalCode code,
        RuntimeDungeonTraversalSnapshot before,
        RuntimeDungeonTraversalSnapshot after,
        RuntimeDungeonTraversalTransition transition,
        IEnumerable<RuntimeDungeonTraversalEvent>? events = null,
        ContentId? reasonId = null,
        string? message = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        Events = RuntimeSnapshotCollections.List(events);
        ReasonId = reasonId;
        Message = message;
    }

    public RuntimeDungeonTraversalCode Code { get; }
    public bool Applied => Code == RuntimeDungeonTraversalCode.Applied;
    public RuntimeDungeonTraversalSnapshot Before { get; }
    public RuntimeDungeonTraversalSnapshot After { get; }
    public RuntimeDungeonTraversalTransition Transition { get; }
    public IReadOnlyList<RuntimeDungeonTraversalEvent> Events { get; }
    public ContentId? ReasonId { get; }
    public string? Message { get; }
}

public sealed record RuntimeDungeonStateChangeResult
{
    public RuntimeDungeonStateChangeResult(
        RuntimeDungeonStateChangeCode code,
        RuntimeDungeonTraversalSnapshot before,
        RuntimeDungeonTraversalSnapshot after,
        IEnumerable<RuntimeDungeonTraversalEvent>? events = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Events = RuntimeSnapshotCollections.List(events);
    }

    public RuntimeDungeonStateChangeCode Code { get; }
    public bool Applied => Code == RuntimeDungeonStateChangeCode.Applied;
    public RuntimeDungeonTraversalSnapshot Before { get; }
    public RuntimeDungeonTraversalSnapshot After { get; }
    public IReadOnlyList<RuntimeDungeonTraversalEvent> Events { get; }
}

public interface IRuntimeDungeonTraversalPolicy
{
    RuntimeDungeonTraversalPolicyDecision Evaluate(RuntimeDungeonTraversalPolicyRequest request);
}

public interface IRuntimeDungeonTraversalService
{
    RuntimeDungeonTraversalResult Traverse(
        RuntimeDungeonTraversalSnapshot current,
        RuntimeDungeonTraversalTransition transition);

    RuntimeDungeonStateChangeResult UnlockCheckpoint(
        RuntimeDungeonTraversalSnapshot current,
        ContentId checkpointId);

    RuntimeDungeonStateChangeResult RegisterBossDefeat(
        RuntimeDungeonTraversalSnapshot current,
        ContentId bossId);
}

public sealed class RuntimeDungeonTraversalService : IRuntimeDungeonTraversalService
{
    private readonly IRuntimeDungeonTraversalPolicy _policy;

    public RuntimeDungeonTraversalService(IRuntimeDungeonTraversalPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public RuntimeDungeonTraversalResult Traverse(
        RuntimeDungeonTraversalSnapshot current,
        RuntimeDungeonTraversalTransition transition)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(transition);

        if (current.DungeonId != transition.DungeonId)
        {
            return Rejected(
                RuntimeDungeonTraversalCode.DungeonMismatch,
                current,
                transition,
                ContentId.Parse("dungeon_mismatch"),
                $"Transition '{transition.Id}' belongs to '{transition.DungeonId}', not '{current.DungeonId}'.");
        }

        if (current.CurrentNodeId != transition.SourceNodeId)
        {
            return Rejected(
                RuntimeDungeonTraversalCode.SourceMismatch,
                current,
                transition,
                ContentId.Parse("source_mismatch"),
                $"Transition '{transition.Id}' starts at '{transition.SourceNodeId}', not '{current.CurrentNodeId}'.");
        }

        RuntimeDungeonTraversalPolicyDecision decision = _policy.Evaluate(
            new RuntimeDungeonTraversalPolicyRequest(current, transition));
        if (!decision.IsAllowed)
        {
            return Rejected(
                RuntimeDungeonTraversalCode.PolicyRejected,
                current,
                transition,
                decision.ReasonId,
                decision.Message);
        }

        RuntimeDungeonTraversalSnapshot after = current.MoveTo(transition.DestinationNodeId);
        return new RuntimeDungeonTraversalResult(
            RuntimeDungeonTraversalCode.Applied,
            current,
            after,
            transition,
            [
                new RuntimeDungeonTraversalEvent(
                    RuntimeDungeonTraversalEventKind.TransitionApplied,
                    current.DungeonId,
                    transition.Id,
                    transition.SourceNodeId,
                    transition.DestinationNodeId)
            ]);
    }

    public RuntimeDungeonStateChangeResult UnlockCheckpoint(
        RuntimeDungeonTraversalSnapshot current,
        ContentId checkpointId)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.IsCheckpointUnlocked(checkpointId))
        {
            return new RuntimeDungeonStateChangeResult(
                RuntimeDungeonStateChangeCode.AlreadyRecorded,
                current,
                current);
        }

        RuntimeDungeonTraversalSnapshot after = current.UnlockCheckpoint(checkpointId);
        return new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied,
            current,
            after,
            [
                new RuntimeDungeonTraversalEvent(
                    RuntimeDungeonTraversalEventKind.CheckpointUnlocked,
                    current.DungeonId,
                    checkpointId)
            ]);
    }

    public RuntimeDungeonStateChangeResult RegisterBossDefeat(
        RuntimeDungeonTraversalSnapshot current,
        ContentId bossId)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.IsBossDefeated(bossId))
        {
            return new RuntimeDungeonStateChangeResult(
                RuntimeDungeonStateChangeCode.AlreadyRecorded,
                current,
                current);
        }

        RuntimeDungeonTraversalSnapshot after = current.MarkBossDefeated(bossId);
        return new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied,
            current,
            after,
            [
                new RuntimeDungeonTraversalEvent(
                    RuntimeDungeonTraversalEventKind.BossDefeated,
                    current.DungeonId,
                    bossId)
            ]);
    }

    private static RuntimeDungeonTraversalResult Rejected(
        RuntimeDungeonTraversalCode code,
        RuntimeDungeonTraversalSnapshot current,
        RuntimeDungeonTraversalTransition transition,
        ContentId? reasonId,
        string? message) =>
        new(
            code,
            current,
            current,
            transition,
            [
                new RuntimeDungeonTraversalEvent(
                    RuntimeDungeonTraversalEventKind.TransitionRejected,
                    current.DungeonId,
                    transition.Id,
                    transition.SourceNodeId,
                    transition.DestinationNodeId,
                    reasonId,
                    message)
            ],
            reasonId,
            message);
}
