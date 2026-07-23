using Convergence.Content;

namespace Convergence.Execution;

/// <summary>Identifies the mutation requested by an ailment transition policy.</summary>
public enum BattleAilmentTransitionOperation
{
    ApplyNew,
    RefreshExisting,
    ReplaceExclusive,
    Reject
}

/// <summary>Identifies the committed or rejected result of an ailment transition.</summary>
public enum BattleAilmentTransitionOutcome
{
    Applied,
    Refreshed,
    Replaced,
    Rejected
}

/// <summary>Provides a stable reason when an ailment transition is rejected.</summary>
public enum BattleAilmentTransitionRejectionReason
{
    None,
    SameAilmentAlreadyActive,
    ExclusiveAilmentActive,
    ReplacementProtected,
    PolicyRejected,
    InvalidPolicyDecision
}

/// <summary>Describes one individual ailment state change within a transition.</summary>
public enum BattleAilmentStateChangeKind
{
    Added,
    Refreshed,
    Removed
}

/// <summary>Immutable ailment state exposed to a transition policy.</summary>
public sealed record BattleAilmentStateSnapshot
{
    public BattleAilmentStateSnapshot(ContentId ailmentId, StatusLifetimeDefinition lifetime)
    {
        if (!ailmentId.IsValid)
        {
            throw new ArgumentException("Ailment ID must be valid.", nameof(ailmentId));
        }

        AilmentId = ailmentId;
        Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public ContentId AilmentId { get; }
    public StatusLifetimeDefinition Lifetime { get; }
}

/// <summary>Immutable policy input for one candidate ailment.</summary>
public sealed record BattleAilmentTransitionPolicyRequest
{
    public BattleAilmentTransitionPolicyRequest(
        ContentId ailmentId,
        StatusLifetimeDefinition candidateLifetime,
        BattleAilmentStateSnapshot? existingSameAilment,
        IEnumerable<BattleAilmentStateSnapshot>? exclusiveConflicts = null)
    {
        if (!ailmentId.IsValid)
        {
            throw new ArgumentException("Candidate ailment ID must be valid.", nameof(ailmentId));
        }

        BattleAilmentStateSnapshot[] conflicts = (exclusiveConflicts ?? []).ToArray();
        if (conflicts.Any(conflict => conflict is null) ||
            conflicts.Any(conflict => conflict.AilmentId == ailmentId) ||
            conflicts.Select(conflict => conflict.AilmentId).Distinct().Count() != conflicts.Length)
        {
            throw new ArgumentException(
                "Exclusive conflicts must be non-null, unique, and different from the candidate ailment.",
                nameof(exclusiveConflicts));
        }
        if (existingSameAilment is not null && existingSameAilment.AilmentId != ailmentId)
        {
            throw new ArgumentException(
                "The existing-same snapshot must identify the candidate ailment.",
                nameof(existingSameAilment));
        }

        AilmentId = ailmentId;
        CandidateLifetime = candidateLifetime ?? throw new ArgumentNullException(nameof(candidateLifetime));
        ExistingSameAilment = existingSameAilment;
        ExclusiveConflicts = Array.AsReadOnly(conflicts);
    }

    public ContentId AilmentId { get; }
    public StatusLifetimeDefinition CandidateLifetime { get; }
    public BattleAilmentStateSnapshot? ExistingSameAilment { get; }
    public IReadOnlyList<BattleAilmentStateSnapshot> ExclusiveConflicts { get; }
}

/// <summary>One policy-selected transition operation and its typed rejection reason.</summary>
public sealed record BattleAilmentTransitionDecision
{
    public BattleAilmentTransitionDecision(
        BattleAilmentTransitionOperation operation,
        BattleAilmentTransitionRejectionReason rejectionReason = BattleAilmentTransitionRejectionReason.None)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        if (!Enum.IsDefined(rejectionReason))
        {
            throw new ArgumentOutOfRangeException(nameof(rejectionReason));
        }
        if (operation == BattleAilmentTransitionOperation.Reject &&
            rejectionReason == BattleAilmentTransitionRejectionReason.None)
        {
            throw new ArgumentException("A rejected transition requires a rejection reason.", nameof(rejectionReason));
        }
        if (operation != BattleAilmentTransitionOperation.Reject &&
            rejectionReason != BattleAilmentTransitionRejectionReason.None)
        {
            throw new ArgumentException(
                "Only a rejected transition may carry a rejection reason.",
                nameof(rejectionReason));
        }

        Operation = operation;
        RejectionReason = rejectionReason;
    }

    public BattleAilmentTransitionOperation Operation { get; }
    public BattleAilmentTransitionRejectionReason RejectionReason { get; }
}

/// <summary>Selects how same-ailment and exclusivity conflicts are handled.</summary>
public interface IBattleAilmentTransitionPolicy
{
    BattleAilmentTransitionDecision Resolve(BattleAilmentTransitionPolicyRequest request);
}

/// <summary>Rejects any reapplication or exclusivity conflict.</summary>
public sealed class RejectExistingAilmentTransitionPolicy : IBattleAilmentTransitionPolicy
{
    public static RejectExistingAilmentTransitionPolicy Instance { get; } = new();

    public BattleAilmentTransitionDecision Resolve(BattleAilmentTransitionPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExistingSameAilment is not null)
        {
            return new(
                BattleAilmentTransitionOperation.Reject,
                BattleAilmentTransitionRejectionReason.SameAilmentAlreadyActive);
        }

        return request.ExclusiveConflicts.Count > 0
            ? new(
                BattleAilmentTransitionOperation.Reject,
                BattleAilmentTransitionRejectionReason.ExclusiveAilmentActive)
            : new(BattleAilmentTransitionOperation.ApplyNew);
    }
}

/// <summary>Refreshes the same ailment but rejects a different exclusive ailment.</summary>
public sealed class RefreshExistingAilmentTransitionPolicy : IBattleAilmentTransitionPolicy
{
    public static RefreshExistingAilmentTransitionPolicy Instance { get; } = new();

    public BattleAilmentTransitionDecision Resolve(BattleAilmentTransitionPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExistingSameAilment is not null)
        {
            return new(BattleAilmentTransitionOperation.RefreshExisting);
        }

        return request.ExclusiveConflicts.Count > 0
            ? new(
                BattleAilmentTransitionOperation.Reject,
                BattleAilmentTransitionRejectionReason.ExclusiveAilmentActive)
            : new(BattleAilmentTransitionOperation.ApplyNew);
    }
}

/// <summary>Refreshes the same ailment and replaces different ailments in its exclusivity group.</summary>
public sealed class ReplaceExclusiveAilmentTransitionPolicy : IBattleAilmentTransitionPolicy
{
    public static ReplaceExclusiveAilmentTransitionPolicy Instance { get; } = new();

    public BattleAilmentTransitionDecision Resolve(BattleAilmentTransitionPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExclusiveConflicts.Count > 0)
        {
            return new(BattleAilmentTransitionOperation.ReplaceExclusive);
        }
        if (request.ExistingSameAilment is not null)
        {
            return new(BattleAilmentTransitionOperation.RefreshExisting);
        }

        return new(BattleAilmentTransitionOperation.ApplyNew);
    }
}

/// <summary>Supplied default retaining refresh-same and replace-exclusive behavior.</summary>
public sealed class StandardBattleAilmentTransitionPolicy : IBattleAilmentTransitionPolicy
{
    public static StandardBattleAilmentTransitionPolicy Instance { get; } = new();

    public BattleAilmentTransitionDecision Resolve(BattleAilmentTransitionPolicyRequest request) =>
        ReplaceExclusiveAilmentTransitionPolicy.Instance.Resolve(request);
}

/// <summary>Immutable evidence for one added, refreshed, or removed ailment state.</summary>
public sealed record BattleAilmentStateChange
{
    public BattleAilmentStateChange(
        BattleAilmentStateChangeKind kind,
        ContentId ailmentId,
        StatusLifetimeDefinition? before,
        StatusLifetimeDefinition? after,
        StatusRemovalCause? removalCause = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (!ailmentId.IsValid)
        {
            throw new ArgumentException("Ailment ID must be valid.", nameof(ailmentId));
        }
        if (removalCause is StatusRemovalCause cause && !Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(removalCause));
        }
        if ((kind == BattleAilmentStateChangeKind.Added && (before is not null || after is null)) ||
            (kind == BattleAilmentStateChangeKind.Refreshed && (before is null || after is null)) ||
            (kind == BattleAilmentStateChangeKind.Removed &&
             (before is null || after is not null || removalCause is null)))
        {
            throw new ArgumentException("Ailment state-change before/after values do not match its kind.");
        }
        if (kind != BattleAilmentStateChangeKind.Removed && removalCause is not null)
        {
            throw new ArgumentException("Only an ailment removal may carry a removal cause.", nameof(removalCause));
        }

        Kind = kind;
        AilmentId = ailmentId;
        Before = before;
        After = after;
        RemovalCause = removalCause;
    }

    public BattleAilmentStateChangeKind Kind { get; }
    public ContentId AilmentId { get; }
    public StatusLifetimeDefinition? Before { get; }
    public StatusLifetimeDefinition? After { get; }
    public StatusRemovalCause? RemovalCause { get; }
}

/// <summary>Ordered, immutable evidence for an ailment transition decision.</summary>
public sealed record BattleAilmentTransitionResult
{
    public BattleAilmentTransitionResult(
        BattleAilmentTransitionOutcome outcome,
        ContentId ailmentId,
        IEnumerable<BattleAilmentStateChange>? stateChanges = null,
        BattleAilmentTransitionRejectionReason rejectionReason = BattleAilmentTransitionRejectionReason.None)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }
        if (!ailmentId.IsValid)
        {
            throw new ArgumentException("Ailment ID must be valid.", nameof(ailmentId));
        }
        if (!Enum.IsDefined(rejectionReason))
        {
            throw new ArgumentOutOfRangeException(nameof(rejectionReason));
        }

        BattleAilmentStateChange[] changes = (stateChanges ?? []).ToArray();
        if (changes.Any(change => change is null))
        {
            throw new ArgumentException("Ailment state changes cannot contain null values.", nameof(stateChanges));
        }
        if (outcome == BattleAilmentTransitionOutcome.Rejected)
        {
            if (changes.Length != 0 || rejectionReason == BattleAilmentTransitionRejectionReason.None)
            {
                throw new ArgumentException("A rejected transition requires a reason and cannot mutate state.");
            }
        }
        else if (changes.Length == 0 || rejectionReason != BattleAilmentTransitionRejectionReason.None)
        {
            throw new ArgumentException("An accepted transition requires state changes and no rejection reason.");
        }
        else
        {
            ValidateAcceptedShape(outcome, ailmentId, changes);
        }

        Outcome = outcome;
        AilmentId = ailmentId;
        StateChanges = Array.AsReadOnly(changes);
        AffectedAilmentIds = Array.AsReadOnly(changes.Select(change => change.AilmentId).Distinct().ToArray());
        RejectionReason = rejectionReason;
    }

    public BattleAilmentTransitionOutcome Outcome { get; }
    public ContentId AilmentId { get; }
    public IReadOnlyList<BattleAilmentStateChange> StateChanges { get; }
    public IReadOnlyList<ContentId> AffectedAilmentIds { get; }
    public BattleAilmentTransitionRejectionReason RejectionReason { get; }
    public bool Applied => Outcome != BattleAilmentTransitionOutcome.Rejected;

    private static void ValidateAcceptedShape(
        BattleAilmentTransitionOutcome outcome,
        ContentId ailmentId,
        IReadOnlyList<BattleAilmentStateChange> changes)
    {
        bool valid = outcome switch
        {
            BattleAilmentTransitionOutcome.Applied =>
                changes.Count == 1 &&
                changes[0].Kind == BattleAilmentStateChangeKind.Added &&
                changes[0].AilmentId == ailmentId,
            BattleAilmentTransitionOutcome.Refreshed =>
                changes.Count == 1 &&
                changes[0].Kind == BattleAilmentStateChangeKind.Refreshed &&
                changes[0].AilmentId == ailmentId,
            BattleAilmentTransitionOutcome.Replaced =>
                changes.Count >= 2 &&
                changes.Take(changes.Count - 1).All(change =>
                    change.Kind == BattleAilmentStateChangeKind.Removed &&
                    change.AilmentId != ailmentId) &&
                changes[^1].AilmentId == ailmentId &&
                changes[^1].Kind is BattleAilmentStateChangeKind.Added or
                    BattleAilmentStateChangeKind.Refreshed,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException(
                $"Ailment state changes do not match transition outcome '{outcome}'.",
                nameof(changes));
        }
    }
}
