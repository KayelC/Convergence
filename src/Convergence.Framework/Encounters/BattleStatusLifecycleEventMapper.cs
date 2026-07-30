using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Encounters;

/// <summary>Maps typed lifecycle transitions into serializer-neutral encounter events.</summary>
public static class BattleStatusLifecycleEventMapper
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
                    statusEvent.SourceActorId ?? statusEvent.ActorId,
                    statusEvent.ActorId,
                    statusEvent.Value ?? 0m,
                    statusEvent.RelatedId,
                    statusEvent.SourceId),
                debugText),
            BattleStatusLifecycleEventKind.PassiveTriggered or
                BattleStatusLifecycleEventKind.PassiveEvaluated => MapPassive(statusEvent, debugText),
            BattleStatusLifecycleEventKind.PassiveEffectResolved => new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.EffectResolved,
                new BattleEffectResolvedEventPayload(
                    statusEvent.SourceActorId ?? throw new InvalidOperationException(
                        "Passive effect lifecycle events require a source actor ID."),
                    statusEvent.SourceId ?? throw new InvalidOperationException(
                        "Passive effect lifecycle events require a source ID."),
                    statusEvent.EffectResult ?? throw new InvalidOperationException(
                        "Passive effect lifecycle events require typed effect evidence.")),
                debugText),
            _ => new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.StatusChanged,
                new BattleStatusChangedEventPayload(statusEvent),
                debugText)
        };

    private static BattleEncounterEvent MapPassive(
        BattleStatusLifecycleEvent statusEvent,
        string? debugText)
    {
        ContentId skillId = statusEvent.RelatedId ?? throw new InvalidOperationException(
            "Passive lifecycle events require a related skill ID.");
        PassiveTriggerExecutionResult activation = statusEvent.PassiveActivation ??
            throw new InvalidOperationException(
                "Passive lifecycle events require typed activation evidence.");

        return new BattleEncounterEvent(
            0,
            BattleEncounterEventKind.PassiveActivated,
            new BattlePassiveActivatedEventPayload(
                statusEvent.ActorId,
                skillId,
                activation.Outcome,
                activation.TriggerIndex,
                activation.EventId,
                activation),
            debugText);
    }
}
