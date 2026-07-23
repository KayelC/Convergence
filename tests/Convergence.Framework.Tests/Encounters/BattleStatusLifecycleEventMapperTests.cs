using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Encounters;

public sealed class BattleStatusLifecycleEventMapperTests
{
    [Fact]
    public void MapAll_UsesOneTypedMappingWhilePreservingCallerOwnedDebugText()
    {
        RuntimeInstanceId actorId = RuntimeInstanceId.Parse("mapper_actor");
        ContentId hp = ContentId.Parse("hp");
        ContentId passive = ContentId.Parse("recovery_passive");
        ContentId eventId = ContentId.Parse("owner_turn_end");
        var effect = new EffectExecutionResult(
            0,
            actorId,
            EffectExecutionOutcome.Success,
            Value: 8m);
        var activation = new PassiveTriggerExecutionResult(
            passive,
            2,
            eventId,
            actorId,
            PassiveTriggerOutcome.Executed,
            [effect]);
        var source = new List<BattleStatusLifecycleEvent>
        {
            new(BattleStatusLifecycleEventKind.ResourceChanged, actorId, hp, 8m),
            new(
                BattleStatusLifecycleEventKind.PassiveTriggered,
                actorId,
                passive)
            {
                PassiveActivation = activation
            },
            new(
                BattleStatusLifecycleEventKind.PassiveEffectResolved,
                actorId,
                passive,
                8m)
            {
                SourceActorId = actorId,
                SourceId = passive,
                EffectResult = effect
            },
            new(BattleStatusLifecycleEventKind.AilmentExpired, actorId, ContentId.Parse("poison"))
        };

        IReadOnlyList<BattleEncounterEvent> mapped = BattleStatusLifecycleEventMapper.MapAll(
            source,
            statusEvent => $"debug:{statusEvent.Kind}");
        source.Clear();

        Assert.Equal(4, mapped.Count);
        Assert.All(mapped, battleEvent => Assert.Equal(0, battleEvent.Sequence));

        BattleEncounterEvent resource = mapped[0];
        Assert.Equal(BattleEncounterEventKind.ResourceChanged, resource.Kind);
        Assert.Equal("debug:ResourceChanged", resource.DebugText);
        var resourcePayload = Assert.IsType<BattleResourceChangedEventPayload>(resource.Payload);
        Assert.Equal(actorId, resourcePayload.SourceActorId);
        Assert.Equal(actorId, resourcePayload.AffectedActorId);
        Assert.Equal(hp, resourcePayload.ResourceId);
        Assert.Equal(8m, resourcePayload.Delta);

        BattleEncounterEvent passiveEvent = mapped[1];
        Assert.Equal(BattleEncounterEventKind.PassiveActivated, passiveEvent.Kind);
        var passivePayload = Assert.IsType<BattlePassiveActivatedEventPayload>(passiveEvent.Payload);
        Assert.Equal(actorId, passivePayload.ActorId);
        Assert.Equal(passive, passivePayload.SkillId);
        Assert.Equal(PassiveTriggerOutcome.Executed, passivePayload.Outcome);
        Assert.Equal(2, passivePayload.TriggerIndex);
        Assert.Equal(eventId, passivePayload.EventId);
        Assert.Same(activation, passivePayload.Result);

        BattleEncounterEvent passiveEffect = mapped[2];
        Assert.Equal(BattleEncounterEventKind.EffectResolved, passiveEffect.Kind);
        var effectPayload = Assert.IsType<BattleEffectResolvedEventPayload>(passiveEffect.Payload);
        Assert.Equal(actorId, effectPayload.SourceActorId);
        Assert.Equal(passive, effectPayload.SourceId);
        Assert.Same(effect, effectPayload.Result);

        BattleEncounterEvent status = mapped[3];
        Assert.Equal(BattleEncounterEventKind.StatusChanged, status.Kind);
        var statusPayload = Assert.IsType<BattleStatusChangedEventPayload>(status.Payload);
        Assert.Equal(BattleStatusLifecycleEventKind.AilmentExpired, statusPayload.StatusEvent.Kind);
    }

    [Fact]
    public void MapAll_RejectsMalformedPassiveLifecycleEvents()
    {
        BattleStatusLifecycleEvent malformed = new(
            BattleStatusLifecycleEventKind.PassiveTriggered,
            RuntimeInstanceId.Parse("mapper_actor"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            BattleStatusLifecycleEventMapper.MapAll([malformed], _ => null));

        Assert.Contains("related skill ID", exception.Message, StringComparison.Ordinal);

        BattleStatusLifecycleEvent missingActivation = new(
            BattleStatusLifecycleEventKind.PassiveTriggered,
            RuntimeInstanceId.Parse("mapper_actor"),
            ContentId.Parse("recovery_passive"));

        exception = Assert.Throws<InvalidOperationException>(() =>
            BattleStatusLifecycleEventMapper.MapAll([missingActivation], _ => null));

        Assert.Contains("typed activation evidence", exception.Message, StringComparison.Ordinal);
    }
}
