using System.Collections.ObjectModel;
using Convergence.Encounters;
using Convergence.Hosting;
using Convergence.Runtime;
using Godot;

namespace Convergence.GodotHost.Infrastructure;

internal sealed class GodotCommandSource<TCommand> : IHostCommandSource<TCommand>
{
    private readonly Queue<HostCommandReadResult<TCommand>> _pending = new();

    public void Submit(TCommand command, HostCommandSelectionIdentity? selectionIdentity = null) =>
        _pending.Enqueue(HostCommandReadResult<TCommand>.Selected(command, selectionIdentity));

    public void Cancel() => _pending.Enqueue(HostCommandReadResult<TCommand>.Cancelled());

    public ValueTask<HostCommandReadResult<TCommand>> ReadAsync(
        HostCommandRequest<TCommand> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_pending.Count == 0)
        {
            throw new InvalidOperationException("No Godot command signal is waiting for this request.");
        }

        HostCommandReadResult<TCommand> result = _pending.Dequeue();
        if (result.IsSelected)
        {
            HostCommandOption<TCommand>? option = request.Options.FirstOrDefault(candidate =>
                EqualityComparer<TCommand>.Default.Equals(candidate.Command, result.Command));
            if (option is null || !option.IsEnabled)
            {
                throw new InvalidOperationException("The signaled command is not an enabled request option.");
            }
        }

        return ValueTask.FromResult(result);
    }
}

internal sealed class GodotSceneInstanceRegistry
{
    private readonly Dictionary<RuntimeInstanceId, Node> _nodes = [];

    public void Attach(RuntimeInstanceId instanceId, Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.TryAdd(instanceId, node))
        {
            throw new InvalidOperationException($"Runtime instance '{instanceId}' already has a Godot Node.");
        }
    }

    public bool TryGet(RuntimeInstanceId instanceId, out Node? node) =>
        _nodes.TryGetValue(instanceId, out node);

    public IReadOnlyDictionary<RuntimeInstanceId, Node> Snapshot() =>
        new ReadOnlyDictionary<RuntimeInstanceId, Node>(new Dictionary<RuntimeInstanceId, Node>(_nodes));
}

internal sealed record GodotMappedEncounterEvent(
    int Sequence,
    BattleEncounterEventKind Kind,
    RuntimeInstanceId? ActorId,
    Node? ActorNode,
    BattleEncounterEventPayload Payload);

internal sealed class GodotEncounterEventSink(GodotSceneInstanceRegistry sceneInstances)
    : IBattleEncounterEventSink
{
    private readonly List<GodotMappedEncounterEvent> _events = [];

    public IReadOnlyList<GodotMappedEncounterEvent> Events => _events.AsReadOnly();

    public ValueTask PublishAsync(
        BattleEncounterEvent battleEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(battleEvent);
        cancellationToken.ThrowIfCancellationRequested();
        if (_events.Count > 0 && battleEvent.Sequence <= _events[^1].Sequence)
        {
            throw new InvalidOperationException("Framework encounter events arrived out of sequence.");
        }

        Node? actorNode = null;
        if (battleEvent.ActorId is RuntimeInstanceId actorId)
        {
            sceneInstances.TryGet(actorId, out actorNode);
        }

        _events.Add(new GodotMappedEncounterEvent(
            battleEvent.Sequence,
            battleEvent.Kind,
            battleEvent.ActorId,
            actorNode,
            battleEvent.Payload));
        return ValueTask.CompletedTask;
    }
}
