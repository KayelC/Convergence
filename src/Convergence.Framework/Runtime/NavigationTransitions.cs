using Convergence.Content;

namespace Convergence.Runtime;

public enum RuntimeNavigationTransitionCode
{
    Applied,
    SourceMismatch,
    PolicyRejected,
    InvalidRequest
}

public enum RuntimeNavigationRequestField
{
    CurrentLocationId,
    TransitionId,
    SourceLocationId,
    DestinationLocationId
}

public enum RuntimeNavigationEventKind
{
    TransitionApplied,
    TransitionRejected
}

public sealed record RuntimeNavigationSnapshot(ContentId CurrentLocationId);

public sealed record RuntimeNavigationTransition(
    ContentId Id,
    ContentId SourceLocationId,
    ContentId DestinationLocationId);

public sealed record RuntimeNavigationPolicyRequest(
    RuntimeNavigationSnapshot Current,
    RuntimeNavigationTransition Transition);

public sealed record RuntimeNavigationPolicyDecision(
    bool IsAllowed,
    ContentId? ReasonId = null,
    string? Message = null);

public sealed record RuntimeNavigationEvent(
    RuntimeNavigationEventKind Kind,
    ContentId TransitionId,
    ContentId SourceLocationId,
    ContentId DestinationLocationId,
    ContentId? ReasonId = null,
    string? Message = null);

public sealed record RuntimeNavigationResult
{
    public RuntimeNavigationResult(
        RuntimeNavigationTransitionCode code,
        RuntimeNavigationSnapshot before,
        RuntimeNavigationSnapshot after,
        RuntimeNavigationTransition transition,
        IEnumerable<RuntimeNavigationEvent>? events = null,
        ContentId? reasonId = null,
        string? message = null,
        RuntimeNavigationRequestField? invalidField = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        Events = RuntimeSnapshotCollections.List(events);
        ReasonId = reasonId;
        Message = message;
        InvalidField = invalidField;
    }

    public RuntimeNavigationTransitionCode Code { get; }
    public bool Applied => Code == RuntimeNavigationTransitionCode.Applied;
    public RuntimeNavigationSnapshot Before { get; }
    public RuntimeNavigationSnapshot After { get; }
    public RuntimeNavigationTransition Transition { get; }
    public IReadOnlyList<RuntimeNavigationEvent> Events { get; }
    public ContentId? ReasonId { get; }
    public string? Message { get; }
    public RuntimeNavigationRequestField? InvalidField { get; }
}

public interface IRuntimeNavigationPolicy
{
    RuntimeNavigationPolicyDecision Evaluate(RuntimeNavigationPolicyRequest request);
}

public interface IRuntimeNavigationService
{
    RuntimeNavigationResult Navigate(
        RuntimeNavigationSnapshot current,
        RuntimeNavigationTransition transition);
}

public sealed class RuntimeNavigationService : IRuntimeNavigationService
{
    private readonly IRuntimeNavigationPolicy _policy;

    public RuntimeNavigationService(IRuntimeNavigationPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public RuntimeNavigationResult Navigate(
        RuntimeNavigationSnapshot current,
        RuntimeNavigationTransition transition)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(transition);

        RuntimeNavigationRequestField? invalidField = InvalidField(current, transition);
        if (invalidField is not null)
        {
            return new RuntimeNavigationResult(
                RuntimeNavigationTransitionCode.InvalidRequest,
                current,
                current,
                transition,
                reasonId: ContentId.Parse("invalid_navigation_request"),
                message: $"Navigation request field '{invalidField}' cannot be empty.",
                invalidField: invalidField);
        }

        if (current.CurrentLocationId != transition.SourceLocationId)
        {
            return Rejected(
                RuntimeNavigationTransitionCode.SourceMismatch,
                current,
                transition,
                reasonId: ContentId.Parse("source_mismatch"),
                message: $"Transition '{transition.Id}' starts at '{transition.SourceLocationId}', not '{current.CurrentLocationId}'.");
        }

        RuntimeNavigationPolicyDecision decision = _policy.Evaluate(
            new RuntimeNavigationPolicyRequest(current, transition));
        if (!decision.IsAllowed)
        {
            return Rejected(
                RuntimeNavigationTransitionCode.PolicyRejected,
                current,
                transition,
                decision.ReasonId,
                decision.Message);
        }

        RuntimeNavigationSnapshot after = new(transition.DestinationLocationId);
        return new RuntimeNavigationResult(
            RuntimeNavigationTransitionCode.Applied,
            current,
            after,
            transition,
            [
                new RuntimeNavigationEvent(
                    RuntimeNavigationEventKind.TransitionApplied,
                    transition.Id,
                    transition.SourceLocationId,
                    transition.DestinationLocationId)
            ]);
    }

    private static RuntimeNavigationRequestField? InvalidField(
        RuntimeNavigationSnapshot current,
        RuntimeNavigationTransition transition)
    {
        if (!current.CurrentLocationId.IsValid)
        {
            return RuntimeNavigationRequestField.CurrentLocationId;
        }
        if (!transition.Id.IsValid)
        {
            return RuntimeNavigationRequestField.TransitionId;
        }
        if (!transition.SourceLocationId.IsValid)
        {
            return RuntimeNavigationRequestField.SourceLocationId;
        }
        if (!transition.DestinationLocationId.IsValid)
        {
            return RuntimeNavigationRequestField.DestinationLocationId;
        }

        return null;
    }

    private static RuntimeNavigationResult Rejected(
        RuntimeNavigationTransitionCode code,
        RuntimeNavigationSnapshot current,
        RuntimeNavigationTransition transition,
        ContentId? reasonId,
        string? message) =>
        new(
            code,
            current,
            current,
            transition,
            [
                new RuntimeNavigationEvent(
                    RuntimeNavigationEventKind.TransitionRejected,
                    transition.Id,
                    transition.SourceLocationId,
                    transition.DestinationLocationId,
                    reasonId,
                    message)
            ],
            reasonId,
            message);
}
