using Convergence.Content;

namespace Convergence.Runtime;

public enum RuntimeDungeonTraversalCode
{
    Applied,
    DungeonMismatch,
    SourceMismatch,
    PolicyRejected,
    InvalidRequest,
    PolicyFaulted
}

public enum RuntimeDungeonTraversalRequestField
{
    CurrentDungeonId,
    CurrentNodeId,
    TransitionId,
    TransitionDungeonId,
    SourceNodeId,
    DestinationNodeId
}

public enum RuntimeDungeonTraversalPolicyFaultKind
{
    Exception,
    NullDecision,
    MalformedDecision
}

public enum RuntimeDungeonStateChangeCode
{
    Applied,
    AlreadyRecorded,
    InvalidRequest,
    NotEligible,
    DungeonMismatch,
    AreaMismatch
}

public enum RuntimeDungeonProgressKind
{
    Checkpoint,
    Boss
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

public sealed class RuntimeDungeonProgressEligibility
{
    public RuntimeDungeonProgressEligibility(
        RuntimeDungeonProgressKind kind,
        ContentId progressId,
        ContentId dungeonId,
        IEnumerable<ContentId> allowedNodeIds)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (!progressId.IsValid || !dungeonId.IsValid)
        {
            throw new ArgumentException("Dungeon progress and dungeon IDs cannot be empty.");
        }
        ArgumentNullException.ThrowIfNull(allowedNodeIds);
        IReadOnlyList<ContentId> nodes = RuntimeSnapshotCollections.List(allowedNodeIds);
        if (nodes.Count == 0 || nodes.Any(node => !node.IsValid) || nodes.Distinct().Count() != nodes.Count)
        {
            throw new ArgumentException("Allowed dungeon areas must be nonempty, valid, and unique.", nameof(allowedNodeIds));
        }

        Kind = kind;
        ProgressId = progressId;
        DungeonId = dungeonId;
        AllowedNodeIds = nodes;
    }

    public RuntimeDungeonProgressKind Kind { get; }
    public ContentId ProgressId { get; }
    public ContentId DungeonId { get; }
    public IReadOnlyList<ContentId> AllowedNodeIds { get; }
}

public sealed class RuntimeDungeonProgressRegistry
{
    public RuntimeDungeonProgressRegistry(IEnumerable<RuntimeDungeonProgressEligibility> eligibility)
    {
        ArgumentNullException.ThrowIfNull(eligibility);
        IReadOnlyList<RuntimeDungeonProgressEligibility> entries = RuntimeSnapshotCollections.List(eligibility);
        if (entries.Any(entry => entry is null) ||
            entries.Select(entry => (entry.Kind, entry.DungeonId, entry.ProgressId)).Distinct().Count() != entries.Count)
        {
            throw new ArgumentException("Dungeon progress eligibility contains null or duplicate entries.", nameof(eligibility));
        }
        Eligibility = entries;
    }

    public IReadOnlyList<RuntimeDungeonProgressEligibility> Eligibility { get; }
}

public sealed record RuntimeDungeonTraversalEvent
{
    public RuntimeDungeonTraversalEvent(
        RuntimeDungeonTraversalEventKind kind,
        ContentId dungeonId,
        ContentId contentId,
        ContentId? sourceNodeId = null,
        ContentId? destinationNodeId = null,
        ContentId? reasonId = null,
        string? message = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (!dungeonId.IsValid || !contentId.IsValid ||
            sourceNodeId is ContentId source && !source.IsValid ||
            destinationNodeId is ContentId destination && !destination.IsValid ||
            reasonId is ContentId reason && !reason.IsValid)
        {
            throw new ArgumentException("Dungeon event identifiers cannot be empty.");
        }
        bool isTransition = kind is RuntimeDungeonTraversalEventKind.TransitionApplied or
            RuntimeDungeonTraversalEventKind.TransitionRejected;
        if (isTransition != (sourceNodeId is not null && destinationNodeId is not null) ||
            !isTransition && (reasonId is not null || message is not null) ||
            kind == RuntimeDungeonTraversalEventKind.TransitionApplied &&
            (reasonId is not null || message is not null))
        {
            throw new ArgumentException("Dungeon event details do not match its kind.");
        }

        Kind = kind;
        DungeonId = dungeonId;
        ContentId = contentId;
        SourceNodeId = sourceNodeId;
        DestinationNodeId = destinationNodeId;
        ReasonId = reasonId;
        Message = message;
    }

    public RuntimeDungeonTraversalEventKind Kind { get; }
    public ContentId DungeonId { get; }
    public ContentId ContentId { get; }
    public ContentId? SourceNodeId { get; }
    public ContentId? DestinationNodeId { get; }
    public ContentId? ReasonId { get; }
    public string? Message { get; }

    public void Deconstruct(
        out RuntimeDungeonTraversalEventKind kind,
        out ContentId dungeonId,
        out ContentId contentId,
        out ContentId? sourceNodeId,
        out ContentId? destinationNodeId,
        out ContentId? reasonId,
        out string? message)
    {
        kind = Kind;
        dungeonId = DungeonId;
        contentId = ContentId;
        sourceNodeId = SourceNodeId;
        destinationNodeId = DestinationNodeId;
        reasonId = ReasonId;
        message = Message;
    }
}

public sealed record RuntimeDungeonTraversalResult
{
    public RuntimeDungeonTraversalResult(
        RuntimeDungeonTraversalCode code,
        RuntimeDungeonTraversalSnapshot before,
        RuntimeDungeonTraversalSnapshot after,
        RuntimeDungeonTraversalTransition transition,
        IEnumerable<RuntimeDungeonTraversalEvent>? events = null,
        ContentId? reasonId = null,
        string? message = null,
        RuntimeDungeonTraversalRequestField? invalidField = null,
        RuntimeDungeonTraversalPolicyFaultKind? faultKind = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        Events = RuntimeSnapshotCollections.List(events);
        ReasonId = reasonId;
        Message = message;
        InvalidField = invalidField;
        FaultKind = faultKind;
        ValidateOutcome();
    }

    public RuntimeDungeonTraversalCode Code { get; }
    public bool Applied => Code == RuntimeDungeonTraversalCode.Applied;
    public RuntimeDungeonTraversalSnapshot Before { get; }
    public RuntimeDungeonTraversalSnapshot After { get; }
    public RuntimeDungeonTraversalTransition Transition { get; }
    public IReadOnlyList<RuntimeDungeonTraversalEvent> Events { get; }
    public ContentId? ReasonId { get; }
    public string? Message { get; }
    public RuntimeDungeonTraversalRequestField? InvalidField { get; }
    public RuntimeDungeonTraversalPolicyFaultKind? FaultKind { get; }

    private void ValidateOutcome()
    {
        if (!Enum.IsDefined(Code) ||
            InvalidField is RuntimeDungeonTraversalRequestField field && !Enum.IsDefined(field) ||
            FaultKind is RuntimeDungeonTraversalPolicyFaultKind fault && !Enum.IsDefined(fault))
        {
            throw new ArgumentOutOfRangeException(nameof(Code));
        }

        RuntimeDungeonTraversalRequestField? firstInvalid =
            RuntimeDungeonTraversalRequestValidation.FirstInvalidField(Before, Transition);
        if (Code == RuntimeDungeonTraversalCode.InvalidRequest)
        {
            if (firstInvalid is null || InvalidField != firstInvalid ||
                !RuntimeDungeonTraversalSnapshotEquality.Same(Before, After) ||
                Events.Count != 0 || ReasonId != ContentId.Parse("invalid_dungeon_request") ||
                FaultKind is not null)
            {
                throw new ArgumentException("Invalid dungeon traversal result has inconsistent evidence.");
            }
            return;
        }

        if (firstInvalid is not null || InvalidField is not null || Events.Count != 1)
        {
            throw new ArgumentException("Dungeon traversal result has invalid request or event evidence.");
        }

        RuntimeDungeonTraversalEvent movement = Events[0];
        if (movement is null || movement.DungeonId != Before.DungeonId ||
            movement.ContentId != Transition.Id || movement.SourceNodeId != Transition.SourceNodeId ||
            movement.DestinationNodeId != Transition.DestinationNodeId ||
            ReasonId != movement.ReasonId || Message != movement.Message)
        {
            throw new ArgumentException("Dungeon traversal event does not match its transition.");
        }

        switch (Code)
        {
            case RuntimeDungeonTraversalCode.Applied:
                if (Before.DungeonId != Transition.DungeonId ||
                    Before.CurrentNodeId != Transition.SourceNodeId ||
                    !RuntimeDungeonTraversalSnapshotEquality.Same(Before.MoveTo(Transition.DestinationNodeId), After) ||
                    movement.Kind != RuntimeDungeonTraversalEventKind.TransitionApplied ||
                    ReasonId is not null || Message is not null || FaultKind is not null)
                {
                    throw new ArgumentException("Applied dungeon traversal result has inconsistent evidence.");
                }
                break;
            case RuntimeDungeonTraversalCode.DungeonMismatch:
                if (Before.DungeonId == Transition.DungeonId || FaultKind is not null ||
                    ReasonId != ContentId.Parse("dungeon_mismatch"))
                {
                    throw new ArgumentException("Dungeon-mismatch result has inconsistent evidence.");
                }
                ValidateRejection(movement);
                break;
            case RuntimeDungeonTraversalCode.SourceMismatch:
                if (Before.DungeonId != Transition.DungeonId ||
                    Before.CurrentNodeId == Transition.SourceNodeId || FaultKind is not null ||
                    ReasonId != ContentId.Parse("source_mismatch"))
                {
                    throw new ArgumentException("Source-mismatch result has inconsistent evidence.");
                }
                ValidateRejection(movement);
                break;
            case RuntimeDungeonTraversalCode.PolicyRejected:
                if (Before.DungeonId != Transition.DungeonId ||
                    Before.CurrentNodeId != Transition.SourceNodeId || FaultKind is not null)
                {
                    throw new ArgumentException("Policy rejection has inconsistent evidence.");
                }
                ValidateRejection(movement);
                break;
            case RuntimeDungeonTraversalCode.PolicyFaulted:
                if (Before.DungeonId != Transition.DungeonId ||
                    Before.CurrentNodeId != Transition.SourceNodeId || FaultKind is null ||
                    ReasonId != ContentId.Parse("dungeon_policy_faulted"))
                {
                    throw new ArgumentException("Policy fault has inconsistent evidence.");
                }
                ValidateRejection(movement);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Code));
        }
    }

    private void ValidateRejection(RuntimeDungeonTraversalEvent movement)
    {
        if (movement.Kind != RuntimeDungeonTraversalEventKind.TransitionRejected ||
            !RuntimeDungeonTraversalSnapshotEquality.Same(Before, After))
        {
            throw new ArgumentException("Rejected dungeon traversal must leave progress unchanged.");
        }
    }
}

public sealed record RuntimeDungeonStateChangeResult
{
    public RuntimeDungeonStateChangeResult(
        RuntimeDungeonStateChangeCode code,
        RuntimeDungeonTraversalSnapshot before,
        RuntimeDungeonTraversalSnapshot after,
        RuntimeDungeonProgressKind progressKind,
        ContentId progressId,
        IEnumerable<RuntimeDungeonTraversalEvent>? events = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        ProgressKind = progressKind;
        ProgressId = progressId;
        Events = RuntimeSnapshotCollections.List(events);
        ValidateOutcome();
    }

    public RuntimeDungeonStateChangeCode Code { get; }
    public bool Applied => Code == RuntimeDungeonStateChangeCode.Applied;
    public RuntimeDungeonTraversalSnapshot Before { get; }
    public RuntimeDungeonTraversalSnapshot After { get; }
    public RuntimeDungeonProgressKind ProgressKind { get; }
    public ContentId ProgressId { get; }
    public IReadOnlyList<RuntimeDungeonTraversalEvent> Events { get; }

    private void ValidateOutcome()
    {
        if (!Enum.IsDefined(Code) || !Enum.IsDefined(ProgressKind))
        {
            throw new ArgumentOutOfRangeException(nameof(Code));
        }
        bool invalidRequest = !Before.DungeonId.IsValid || !Before.CurrentNodeId.IsValid || !ProgressId.IsValid;
        if (Code == RuntimeDungeonStateChangeCode.InvalidRequest)
        {
            if (!invalidRequest || Events.Count != 0 ||
                !RuntimeDungeonTraversalSnapshotEquality.Same(Before, After))
            {
                throw new ArgumentException("Invalid dungeon progress result has inconsistent evidence.");
            }
            return;
        }
        if (invalidRequest)
        {
            throw new ArgumentException("Dungeon progress result contains an invalid request ID.");
        }
        if (Code == RuntimeDungeonStateChangeCode.AlreadyRecorded)
        {
            bool recorded = ProgressKind == RuntimeDungeonProgressKind.Checkpoint
                ? Before.IsCheckpointUnlocked(ProgressId)
                : Before.IsBossDefeated(ProgressId);
            if (!recorded || Events.Count != 0 ||
                !RuntimeDungeonTraversalSnapshotEquality.Same(Before, After))
            {
                throw new ArgumentException("Already-recorded result must leave progress unchanged.");
            }
            return;
        }

        if (Code is RuntimeDungeonStateChangeCode.NotEligible or
            RuntimeDungeonStateChangeCode.DungeonMismatch or
            RuntimeDungeonStateChangeCode.AreaMismatch)
        {
            if (Events.Count != 0 || !RuntimeDungeonTraversalSnapshotEquality.Same(Before, After))
            {
                throw new ArgumentException("Rejected dungeon progress must leave state unchanged.");
            }
            return;
        }

        if (Events.Count != 1 || Events[0] is not RuntimeDungeonTraversalEvent progress ||
            progress.DungeonId != Before.DungeonId || progress.ContentId != ProgressId ||
            Before.DungeonId != After.DungeonId || Before.CurrentNodeId != After.CurrentNodeId ||
            !Before.VisitedNodeIds.SequenceEqual(After.VisitedNodeIds))
        {
            throw new ArgumentException("Dungeon progress result has inconsistent event or location evidence.");
        }

        RuntimeDungeonTraversalSnapshot expected = progress.Kind switch
        {
            RuntimeDungeonTraversalEventKind.CheckpointUnlocked
                when ProgressKind == RuntimeDungeonProgressKind.Checkpoint &&
                    !Before.IsCheckpointUnlocked(ProgressId) => Before.UnlockCheckpoint(ProgressId),
            RuntimeDungeonTraversalEventKind.BossDefeated
                when ProgressKind == RuntimeDungeonProgressKind.Boss &&
                    !Before.IsBossDefeated(ProgressId) => Before.MarkBossDefeated(ProgressId),
            _ => throw new ArgumentException("Dungeon progress event does not describe a new record.")
        };
        if (!RuntimeDungeonTraversalSnapshotEquality.Same(expected, After))
        {
            throw new ArgumentException("Dungeon progress result does not match its event.");
        }
    }
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
    private readonly RuntimeDungeonProgressRegistry _progressRegistry;

    public RuntimeDungeonTraversalService(
        IRuntimeDungeonTraversalPolicy policy,
        RuntimeDungeonProgressRegistry progressRegistry)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _progressRegistry = progressRegistry ?? throw new ArgumentNullException(nameof(progressRegistry));
    }

    public RuntimeDungeonTraversalResult Traverse(
        RuntimeDungeonTraversalSnapshot current,
        RuntimeDungeonTraversalTransition transition)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(transition);

        RuntimeDungeonTraversalRequestField? invalidField =
            RuntimeDungeonTraversalRequestValidation.FirstInvalidField(current, transition);
        if (invalidField is not null)
        {
            return new RuntimeDungeonTraversalResult(
                RuntimeDungeonTraversalCode.InvalidRequest,
                current,
                current,
                transition,
                reasonId: ContentId.Parse("invalid_dungeon_request"),
                message: $"Dungeon traversal request field '{invalidField}' cannot be empty.",
                invalidField: invalidField);
        }

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

        RuntimeDungeonTraversalPolicyDecision? decision;
        try
        {
            decision = _policy.Evaluate(new RuntimeDungeonTraversalPolicyRequest(current, transition));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PolicyFaulted(
                current,
                transition,
                RuntimeDungeonTraversalPolicyFaultKind.Exception,
                $"Dungeon traversal policy faulted: {exception.GetType().Name}: {exception.Message}");
        }

        if (decision is null)
        {
            return PolicyFaulted(
                current,
                transition,
                RuntimeDungeonTraversalPolicyFaultKind.NullDecision,
                "Dungeon traversal policy returned no decision.");
        }
        if (decision.ReasonId is ContentId reason && !reason.IsValid)
        {
            return PolicyFaulted(
                current,
                transition,
                RuntimeDungeonTraversalPolicyFaultKind.MalformedDecision,
                "Dungeon traversal policy returned an empty reason ID.");
        }
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
        ContentId checkpointId) =>
        RecordProgress(current, RuntimeDungeonProgressKind.Checkpoint, checkpointId);

    public RuntimeDungeonStateChangeResult RegisterBossDefeat(
        RuntimeDungeonTraversalSnapshot current,
        ContentId bossId) =>
        RecordProgress(current, RuntimeDungeonProgressKind.Boss, bossId);

    private RuntimeDungeonStateChangeResult RecordProgress(
        RuntimeDungeonTraversalSnapshot current,
        RuntimeDungeonProgressKind kind,
        ContentId progressId)
    {
        ArgumentNullException.ThrowIfNull(current);
        RuntimeDungeonStateChangeResult Rejected(RuntimeDungeonStateChangeCode code) =>
            new(code, current, current, kind, progressId);

        if (!current.DungeonId.IsValid || !current.CurrentNodeId.IsValid || !progressId.IsValid)
        {
            return Rejected(RuntimeDungeonStateChangeCode.InvalidRequest);
        }

        RuntimeDungeonProgressEligibility[] candidates = _progressRegistry.Eligibility
            .Where(entry => entry.Kind == kind && entry.ProgressId == progressId)
            .ToArray();
        if (candidates.Length == 0)
        {
            return Rejected(RuntimeDungeonStateChangeCode.NotEligible);
        }

        RuntimeDungeonProgressEligibility? eligible = candidates.FirstOrDefault(entry =>
            entry.DungeonId == current.DungeonId);
        if (eligible is null)
        {
            return Rejected(RuntimeDungeonStateChangeCode.DungeonMismatch);
        }
        if (!eligible.AllowedNodeIds.Contains(current.CurrentNodeId))
        {
            return Rejected(RuntimeDungeonStateChangeCode.AreaMismatch);
        }

        bool alreadyRecorded = kind == RuntimeDungeonProgressKind.Checkpoint
            ? current.IsCheckpointUnlocked(progressId)
            : current.IsBossDefeated(progressId);
        if (alreadyRecorded)
        {
            return new RuntimeDungeonStateChangeResult(
                RuntimeDungeonStateChangeCode.AlreadyRecorded,
                current,
                current,
                kind,
                progressId);
        }

        RuntimeDungeonTraversalSnapshot after = kind == RuntimeDungeonProgressKind.Checkpoint
            ? current.UnlockCheckpoint(progressId)
            : current.MarkBossDefeated(progressId);
        return new RuntimeDungeonStateChangeResult(
            RuntimeDungeonStateChangeCode.Applied,
            current,
            after,
            kind,
            progressId,
            [
                new RuntimeDungeonTraversalEvent(
                    kind == RuntimeDungeonProgressKind.Checkpoint
                        ? RuntimeDungeonTraversalEventKind.CheckpointUnlocked
                        : RuntimeDungeonTraversalEventKind.BossDefeated,
                    current.DungeonId,
                    progressId)
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

    private static RuntimeDungeonTraversalResult PolicyFaulted(
        RuntimeDungeonTraversalSnapshot current,
        RuntimeDungeonTraversalTransition transition,
        RuntimeDungeonTraversalPolicyFaultKind faultKind,
        string message)
    {
        ContentId reasonId = ContentId.Parse("dungeon_policy_faulted");
        return new RuntimeDungeonTraversalResult(
            RuntimeDungeonTraversalCode.PolicyFaulted,
            current,
            current,
            transition,
            [new RuntimeDungeonTraversalEvent(
                RuntimeDungeonTraversalEventKind.TransitionRejected,
                current.DungeonId,
                transition.Id,
                transition.SourceNodeId,
                transition.DestinationNodeId,
                reasonId,
                message)],
            reasonId,
            message,
            faultKind: faultKind);
    }

}

internal static class RuntimeDungeonTraversalRequestValidation
{
    public static RuntimeDungeonTraversalRequestField? FirstInvalidField(
        RuntimeDungeonTraversalSnapshot current,
        RuntimeDungeonTraversalTransition transition)
    {
        if (!current.DungeonId.IsValid)
        {
            return RuntimeDungeonTraversalRequestField.CurrentDungeonId;
        }
        if (!current.CurrentNodeId.IsValid)
        {
            return RuntimeDungeonTraversalRequestField.CurrentNodeId;
        }
        if (!transition.Id.IsValid)
        {
            return RuntimeDungeonTraversalRequestField.TransitionId;
        }
        if (!transition.DungeonId.IsValid)
        {
            return RuntimeDungeonTraversalRequestField.TransitionDungeonId;
        }
        if (!transition.SourceNodeId.IsValid)
        {
            return RuntimeDungeonTraversalRequestField.SourceNodeId;
        }
        if (!transition.DestinationNodeId.IsValid)
        {
            return RuntimeDungeonTraversalRequestField.DestinationNodeId;
        }

        return null;
    }
}

internal static class RuntimeDungeonTraversalSnapshotEquality
{
    public static bool Same(
        RuntimeDungeonTraversalSnapshot before,
        RuntimeDungeonTraversalSnapshot after) =>
        before.DungeonId == after.DungeonId &&
        before.CurrentNodeId == after.CurrentNodeId &&
        before.VisitedNodeIds.SequenceEqual(after.VisitedNodeIds) &&
        before.UnlockedCheckpointIds.SequenceEqual(after.UnlockedCheckpointIds) &&
        before.DefeatedBossIds.SequenceEqual(after.DefeatedBossIds);
}
