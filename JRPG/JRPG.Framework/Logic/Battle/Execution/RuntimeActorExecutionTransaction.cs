using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Execution;

internal sealed class RuntimeActorExecutionTransaction
{
    private readonly IReadOnlyList<ActorPair> _actors;
    private readonly IReadOnlyDictionary<RuntimeInstanceId, ActorPair> _actorsById;

    public RuntimeActorExecutionTransaction(
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RuntimeActorState[] participantArray =
            (participants ?? throw new ArgumentNullException(nameof(participants))).ToArray();

        var pairs = new List<ActorPair>();
        var byId = new Dictionary<RuntimeInstanceId, ActorPair>();
        foreach (RuntimeActorState participant in participantArray.Append(actor))
        {
            if (byId.TryGetValue(participant.InstanceId, out ActorPair? existing))
            {
                if (!ReferenceEquals(existing.Original, participant))
                {
                    throw new ArgumentException(
                        $"Runtime actor ID '{participant.InstanceId}' belongs to multiple actor objects.",
                        nameof(participants));
                }

                continue;
            }

            var pair = new ActorPair(participant, participant.CreateExecutionClone());
            pairs.Add(pair);
            byId.Add(participant.InstanceId, pair);
        }

        _actors = Array.AsReadOnly(pairs.ToArray());
        _actorsById = new System.Collections.ObjectModel.ReadOnlyDictionary<RuntimeInstanceId, ActorPair>(byId);
        Actor = GetStaged(actor);
        Participants = Array.AsReadOnly(
            participantArray.Select(GetStaged).ToArray());
    }

    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }

    public RuntimeActorState GetStaged(RuntimeActorState original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!_actorsById.TryGetValue(original.InstanceId, out ActorPair? pair) ||
            !ReferenceEquals(pair.Original, original))
        {
            throw new ArgumentException(
                $"Actor '{original.InstanceId}' is not part of this execution transaction.",
                nameof(original));
        }

        return pair.Staged;
    }

    public ResolvedTargetSet Map(ResolvedTargetSet targets) =>
        new(targets.Targets.Select(GetStaged), targets.IsUntargeted);

    public ResolvedRuntimeTargetSet Map(ResolvedRuntimeTargetSet targets) =>
        new(targets.Targets.Select(GetStaged), targets.IsUntargeted);

    public void Commit()
    {
        foreach (ActorPair actor in _actors)
        {
            actor.Original.ApplyExecutionStateFrom(actor.Staged);
        }
    }

    private sealed record ActorPair(
        RuntimeActorState Original,
        RuntimeActorState Staged);
}
