using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Execution;

internal enum ExecutionAssessmentTokenFailure
{
    None,
    WrongExecutor,
    AlreadyConsumed
}

internal sealed class ExecutionAssessmentToken<TRequest> where TRequest : class
{
    private readonly object _authority;
    private int _consumed;

    public ExecutionAssessmentToken(object authority, TRequest request)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public TRequest Request { get; }

    public bool IsOwnedBy(object authority) => ReferenceEquals(_authority, authority);

    public bool TryConsume(object authority, out ExecutionAssessmentTokenFailure failure)
    {
        if (!IsOwnedBy(authority))
        {
            failure = ExecutionAssessmentTokenFailure.WrongExecutor;
            return false;
        }

        if (Interlocked.CompareExchange(ref _consumed, 1, 0) != 0)
        {
            failure = ExecutionAssessmentTokenFailure.AlreadyConsumed;
            return false;
        }

        failure = ExecutionAssessmentTokenFailure.None;
        return true;
    }
}

internal static class PreparedTargetResolver
{
    public static bool TryRebind(
        IEnumerable<RuntimeActorState> participants,
        IReadOnlyList<RuntimeInstanceId> targetIds,
        bool isUntargeted,
        out ResolvedRuntimeTargetSet? targets)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(targetIds);

        RuntimeActorState[] snapshot = participants.ToArray();
        if (snapshot.Select(participant => participant.InstanceId).Distinct().Count() != snapshot.Length ||
            targetIds.Distinct().Count() != targetIds.Count)
        {
            targets = null;
            return false;
        }

        if (isUntargeted)
        {
            targets = targetIds.Count == 0
                ? new ResolvedRuntimeTargetSet([], isUntargeted: true)
                : null;
            return targets is not null;
        }

        var participantsById = snapshot.ToDictionary(participant => participant.InstanceId);
        var rebound = new List<RuntimeActorState>(targetIds.Count);
        foreach (RuntimeInstanceId targetId in targetIds)
        {
            if (!participantsById.TryGetValue(targetId, out RuntimeActorState? target))
            {
                targets = null;
                return false;
            }

            rebound.Add(target);
        }

        targets = new ResolvedRuntimeTargetSet(rebound);
        return true;
    }
}
