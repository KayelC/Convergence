using Convergence.Execution;

namespace Convergence.Encounters;

internal static class BattleStatusLifecycleEventMapper
{
    public static IReadOnlyList<BattleEncounterEvent> MapAll(
        IEnumerable<BattleStatusLifecycleEvent> events,
        Func<BattleStatusLifecycleEvent, string?> debugText)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(debugText);

        return Array.AsReadOnly(events
            .Select(statusEvent => Map(statusEvent, debugText(statusEvent)))
            .ToArray());
    }

    private static BattleEncounterEvent Map(
        BattleStatusLifecycleEvent statusEvent,
        string? debugText) =>
        statusEvent.Kind switch
        {
            BattleStatusLifecycleEventKind.ResourceChanged => new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.ResourceChanged,
                new BattleResourceChangedEventPayload(
                    statusEvent.ActorId,
                    statusEvent.ActorId,
                    statusEvent.Value ?? 0m,
                    statusEvent.RelatedId),
                debugText),
            BattleStatusLifecycleEventKind.PassiveTriggered => new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.PassiveActivated,
                new BattlePassiveActivatedEventPayload(
                    statusEvent.ActorId,
                    statusEvent.RelatedId ?? throw new InvalidOperationException(
                        "Passive lifecycle events require a related skill ID.")),
                debugText),
            _ => new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.StatusChanged,
                new BattleStatusChangedEventPayload(statusEvent),
                debugText)
        };
}
