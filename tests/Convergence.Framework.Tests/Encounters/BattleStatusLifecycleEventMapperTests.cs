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
        var source = new List<BattleStatusLifecycleEvent>
        {
            new(BattleStatusLifecycleEventKind.ResourceChanged, actorId, hp, 8m),
            new(BattleStatusLifecycleEventKind.PassiveTriggered, actorId, passive),
            new(BattleStatusLifecycleEventKind.AilmentExpired, actorId, ContentId.Parse("poison"))
        };

        IReadOnlyList<BattleEncounterEvent> mapped = BattleStatusLifecycleEventMapper.MapAll(
            source,
            statusEvent => $"debug:{statusEvent.Kind}");
        source.Clear();

        Assert.Equal(3, mapped.Count);
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

        BattleEncounterEvent status = mapped[2];
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
    }
}
