using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

internal interface IBattleEncounterLifecycleStateCheckpointPort
{
    object CaptureLifecycleState();
    void RestoreLifecycleState(object checkpoint);
}

internal sealed class BattleEncounterLifecycleTransaction : IDisposable
{
    private readonly RuntimeActorExecutionTransaction _states;
    private readonly IReadOnlyDictionary<RuntimeInstanceId, BattleEncounterParticipant> _participantsById;
    private readonly IBattleEncounterLifecycleStateCheckpointPort? _checkpointPort;
    private readonly object? _lifecycleCheckpoint;
    private bool _committed;

    public BattleEncounterLifecycleTransaction(
        IReadOnlyList<BattleEncounterParticipant> participants,
        IBattleEncounterLifecyclePort? lifecyclePort = null)
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
        _checkpointPort = lifecyclePort as IBattleEncounterLifecycleStateCheckpointPort;
        _lifecycleCheckpoint = _checkpointPort?.CaptureLifecycleState();
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

    public void Commit()
    {
        if (_committed)
        {
            throw new InvalidOperationException(
                "The encounter lifecycle transaction has already committed.");
        }

        _states.Commit();
        _committed = true;
    }

    public void Dispose()
    {
        if (!_committed &&
            _checkpointPort is not null &&
            _lifecycleCheckpoint is not null)
        {
            _checkpointPort.RestoreLifecycleState(_lifecycleCheckpoint);
        }
    }
}
