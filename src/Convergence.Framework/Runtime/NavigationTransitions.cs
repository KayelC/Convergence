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

public sealed record RuntimeNavigationPolicyDecision
{
    public RuntimeNavigationPolicyDecision(
        bool isAllowed,
        ContentId? reasonId = null,
        string? message = null)
    {
        if (reasonId is ContentId id && !id.IsValid)
        {
            throw new ArgumentException("Policy reason ID cannot be empty.", nameof(reasonId));
        }
        IsAllowed = isAllowed;
        ReasonId = reasonId;
        Message = message;
    }

    public bool IsAllowed { get; }
    public ContentId? ReasonId { get; }
    public string? Message { get; }

    public void Deconstruct(out bool isAllowed, out ContentId? reasonId, out string? message)
    {
        isAllowed = IsAllowed;
        reasonId = ReasonId;
        message = Message;
    }
}

public sealed record RuntimeNavigationEvent
{
    public RuntimeNavigationEvent(
        RuntimeNavigationEventKind kind,
        ContentId transitionId,
        ContentId sourceLocationId,
        ContentId destinationLocationId,
        ContentId? reasonId = null,
        string? message = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (!transitionId.IsValid || !sourceLocationId.IsValid || !destinationLocationId.IsValid)
        {
            throw new ArgumentException("Navigation event identifiers cannot be empty.");
        }
        if (reasonId is ContentId id && !id.IsValid)
        {
            throw new ArgumentException("Navigation event reason ID cannot be empty.", nameof(reasonId));
        }
        if (kind == RuntimeNavigationEventKind.TransitionApplied &&
            (reasonId is not null || message is not null))
        {
            throw new ArgumentException("An applied navigation event cannot carry rejection details.");
        }

        Kind = kind;
        TransitionId = transitionId;
        SourceLocationId = sourceLocationId;
        DestinationLocationId = destinationLocationId;
        ReasonId = reasonId;
        Message = message;
    }

    public RuntimeNavigationEventKind Kind { get; }
    public ContentId TransitionId { get; }
    public ContentId SourceLocationId { get; }
    public ContentId DestinationLocationId { get; }
    public ContentId? ReasonId { get; }
    public string? Message { get; }

    public void Deconstruct(
        out RuntimeNavigationEventKind kind,
        out ContentId transitionId,
        out ContentId sourceLocationId,
        out ContentId destinationLocationId,
        out ContentId? reasonId,
        out string? message)
    {
        kind = Kind;
        transitionId = TransitionId;
        sourceLocationId = SourceLocationId;
        destinationLocationId = DestinationLocationId;
        reasonId = ReasonId;
        message = Message;
    }
}

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
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }
        if (invalidField is RuntimeNavigationRequestField field && !Enum.IsDefined(field))
        {
            throw new ArgumentOutOfRangeException(nameof(invalidField));
        }
        if (reasonId is ContentId id && !id.IsValid)
        {
            throw new ArgumentException("Navigation result reason ID cannot be empty.", nameof(reasonId));
        }

        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        Events = RuntimeSnapshotCollections.List(events);
        ReasonId = reasonId;
        Message = message;
        InvalidField = invalidField;
        ValidateOutcome();
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

    private void ValidateOutcome()
    {
        RuntimeNavigationRequestField? firstInvalid = RuntimeNavigationRequestValidation.FirstInvalidField(
            Before,
            Transition);
        if (Code == RuntimeNavigationTransitionCode.InvalidRequest)
        {
            if (firstInvalid is null || InvalidField != firstInvalid || After != Before ||
                Events.Count != 0 || ReasonId != ContentId.Parse("invalid_navigation_request"))
            {
                throw new ArgumentException("Invalid navigation request result has inconsistent evidence.");
            }
            return;
        }

        if (firstInvalid is not null || InvalidField is not null || Events.Count != 1)
        {
            throw new ArgumentException("Navigation result has invalid request or event evidence.");
        }

        RuntimeNavigationEvent navigationEvent = Events[0];
        if (navigationEvent.TransitionId != Transition.Id ||
            navigationEvent.SourceLocationId != Transition.SourceLocationId ||
            navigationEvent.DestinationLocationId != Transition.DestinationLocationId)
        {
            throw new ArgumentException("Navigation event does not match the requested transition.");
        }

        switch (Code)
        {
            case RuntimeNavigationTransitionCode.Applied:
                if (Before.CurrentLocationId != Transition.SourceLocationId ||
                    After.CurrentLocationId != Transition.DestinationLocationId ||
                    ReasonId is not null || Message is not null ||
                    navigationEvent.Kind != RuntimeNavigationEventKind.TransitionApplied)
                {
                    throw new ArgumentException("Applied navigation result has inconsistent state or event evidence.");
                }
                break;
            case RuntimeNavigationTransitionCode.SourceMismatch:
                if (Before.CurrentLocationId == Transition.SourceLocationId ||
                    After != Before ||
                    navigationEvent.Kind != RuntimeNavigationEventKind.TransitionRejected ||
                    ReasonId != navigationEvent.ReasonId || Message != navigationEvent.Message)
                {
                    throw new ArgumentException("Source-mismatch result has inconsistent state or event evidence.");
                }
                break;
            case RuntimeNavigationTransitionCode.PolicyRejected:
                if (Before.CurrentLocationId != Transition.SourceLocationId ||
                    After != Before ||
                    navigationEvent.Kind != RuntimeNavigationEventKind.TransitionRejected ||
                    ReasonId != navigationEvent.ReasonId || Message != navigationEvent.Message)
                {
                    throw new ArgumentException("Policy-rejected result has inconsistent state or event evidence.");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Code));
        }
    }
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

        RuntimeNavigationRequestField? invalidField = RuntimeNavigationRequestValidation.FirstInvalidField(
            current,
            transition);
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

internal static class RuntimeNavigationRequestValidation
{
    public static RuntimeNavigationRequestField? FirstInvalidField(
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
}
