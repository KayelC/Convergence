using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Runtime;

internal sealed class BattleEncounterLifecycleTransaction
{
    private readonly RuntimeActorExecutionTransaction _states;
    private readonly IReadOnlyDictionary<RuntimeInstanceId, BattleEncounterParticipant> _participantsById;

    public BattleEncounterLifecycleTransaction(IReadOnlyList<BattleEncounterParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Count == 0)
        {
            throw new ArgumentException("A lifecycle transaction requires participants.", nameof(participants));
        }

        RuntimeActorState[] states = participants.Select(participant => participant.State).ToArray();
        _states = new RuntimeActorExecutionTransaction(states[0], states);
        BattleEncounterParticipant[] staged = participants
            .Select(participant => new BattleEncounterParticipant(
                _states.GetStaged(participant.State),
                participant.DisplayName))
            .ToArray();
        Participants = Array.AsReadOnly(staged);
        _participantsById = new System.Collections.ObjectModel.ReadOnlyDictionary<
            RuntimeInstanceId,
            BattleEncounterParticipant>(staged.ToDictionary(participant => participant.InstanceId));
    }

    public IReadOnlyList<BattleEncounterParticipant> Participants { get; }

    public BattleEncounterRequest CreateEncounter(BattleEncounterRequest encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        return new BattleEncounterRequest(
            Participants,
            encounter.ContextId,
            encounter.BattleKindId,
            encounter.MoonPhaseId,
            encounter.RoundLimit);
    }

    public BattleEncounterParticipant GetStaged(BattleEncounterParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (!_participantsById.TryGetValue(participant.InstanceId, out BattleEncounterParticipant? staged))
        {
            throw new ArgumentException(
                $"Participant '{participant.InstanceId}' is not part of this lifecycle transaction.",
                nameof(participant));
        }

        return staged;
    }

    public void Commit() => _states.Commit();
}
