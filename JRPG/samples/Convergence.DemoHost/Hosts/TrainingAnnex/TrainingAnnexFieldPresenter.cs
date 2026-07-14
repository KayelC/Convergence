using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed class TrainingAnnexFieldPresenter
{
    private readonly IHostEventSink<string> _eventSink;

    public TrainingAnnexFieldPresenter(IHostEventSink<string> eventSink)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public ValueTask PrintSessionAsync(
        GameDataCatalog catalog,
        RuntimeFieldSnapshot field,
        CancellationToken cancellationToken) =>
        _eventSink.PublishAsync(
            $"Session: {TrainingAnnexHostSupport.PackId}; {catalog.Entities.Count} entities, {catalog.Skills.Count} skills, {catalog.Items.Count} items, {catalog.Encounters.Count} encounters, {catalog.Dungeons.Count} dungeons. Location: {FieldLabel(field.Navigation.CurrentLocationId)} ({field.Navigation.CurrentLocationId}); dungeon state: {(field.DungeonTraversal is null ? "not active" : field.DungeonTraversal.CurrentNodeId.ToString())}.",
            cancellationToken);

    public async ValueTask<RuntimeFieldSnapshot> ApplyNavigationAsync(
        RuntimeFieldSnapshot field,
        RuntimeNavigationResult navigation,
        string appliedDescription,
        CancellationToken cancellationToken)
    {
        if (!navigation.Applied)
        {
            await _eventSink.PublishAsync(
                $"Field navigation rejected: {navigation.Message}",
                cancellationToken).ConfigureAwait(false);
            return field;
        }

        await _eventSink.PublishAsync(
            $"Field navigation: {appliedDescription}; location {FieldLabel(navigation.After.CurrentLocationId)} ({navigation.After.CurrentLocationId}).",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeFieldSnapshot(navigation.After, field.DungeonTraversal);
    }

    public async ValueTask<RuntimeFieldSnapshot> ApplyDungeonTraversalAsync(
        RuntimeFieldSnapshot field,
        RuntimeDungeonTraversalResult traversal,
        CancellationToken cancellationToken)
    {
        if (!traversal.Applied)
        {
            await _eventSink.PublishAsync(
                $"Dungeon traversal rejected: {traversal.Message}",
                cancellationToken).ConfigureAwait(false);
            return field;
        }

        await _eventSink.PublishAsync(
            $"Dungeon traversal: {DungeonNodeLabel(traversal.Before.CurrentNodeId)} -> {DungeonNodeLabel(traversal.After.CurrentNodeId)}.",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeFieldSnapshot(field.Navigation, traversal.After);
    }

    public async ValueTask<RuntimeFieldSnapshot> ApplyDungeonStateChangeAsync(
        RuntimeFieldSnapshot field,
        RuntimeDungeonStateChangeResult change,
        CancellationToken cancellationToken)
    {
        if (!change.Applied)
        {
            await _eventSink.PublishAsync(
                "Dungeon state unchanged: checkpoint was already unlocked.",
                cancellationToken).ConfigureAwait(false);
            return field;
        }

        RuntimeDungeonTraversalEvent dungeonEvent = RequireSingleEvent(change.Events);
        await _eventSink.PublishAsync(
            $"Dungeon checkpoint unlocked: {dungeonEvent.ContentId}.",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeFieldSnapshot(field.Navigation, change.After);
    }

    public static RuntimeDungeonTraversalSnapshot RequireDungeonTraversal(RuntimeFieldSnapshot field) =>
        field.DungeonTraversal ?? throw new InvalidOperationException(
            "The Training Annex dungeon traversal state is not active.");

    public static string FieldLabel(ContentId locationId) =>
        locationId == TrainingAnnexHostSupport.StagingArea
            ? "Staging Area"
            : locationId == TrainingAnnexHostSupport.TrainingAnnexEntrance
                ? "Training Annex Entrance"
                : locationId.ToString();

    public static string DungeonNodeLabel(ContentId? nodeId) =>
        nodeId == TrainingAnnexHostSupport.TrainingAnnexEntrance
            ? "Training Annex Entrance"
            : nodeId == TrainingAnnexHostSupport.ReviewHall
                ? "Review Hall"
                : nodeId == TrainingAnnexHostSupport.ReviewAlcove
                    ? "Review Alcove"
                    : nodeId?.ToString() ?? "Unknown Dungeon Node";

    private static RuntimeDungeonTraversalEvent RequireSingleEvent(
        IReadOnlyList<RuntimeDungeonTraversalEvent> events) =>
        events.Count == 1
            ? events[0]
            : throw new InvalidOperationException("Expected one dungeon state event.");
}
