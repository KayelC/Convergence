using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimePartyRosterInvariantIntegrationTests
{
    [Fact]
    public void StatComposition_RejectsActiveHostedEntityThatIsNotOwned()
    {
        RuntimeActorState vessel = Actor("vessel", "test.pack:vessel");
        RuntimeActorState hostedEntity = Actor("hosted_entity", "test.pack:hosted_entity");
        RuntimeActorReferenceSnapshot owner = Reference(vessel);
        var invalidRoster = new RuntimePartyRosterSnapshot(
            owner,
            ownerLevel: 1,
            activeParty: [owner],
            activeHostedEntity: Reference(hostedEntity));
        RuntimeActorSnapshot before = vessel.ToSnapshot();

        RuntimeActorStatCompositionResult result =
            new RuntimeActorStatCompositionService().Compose(
                new RuntimeActorStatCompositionRequest(
                    vessel,
                    RuntimeStatSourceKind.ActiveHostedEntity,
                    MissingHostedEntityBehavior.RejectStatResolution,
                    hostedEntity,
                    invalidRoster));

        Assert.False(result.Applied);
        RuntimeActorStatCompositionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            RuntimeActorStatCompositionDiagnosticCode.RosterInvariantViolation,
            diagnostic.Code);
        Assert.Contains("must exist in the hosted-entity roster", diagnostic.Message, StringComparison.Ordinal);
        Assert.Same(result.Before, result.After);
        RuntimeActorSnapshot after = vessel.ToSnapshot();
        Assert.Equal(before.Progression, after.Progression);
        Assert.Equal(before.Resources.ToArray(), after.Resources.ToArray());
        Assert.Equal(
            before.Stats.EffectiveStats.OrderBy(pair => pair.Key.ToString()).ToArray(),
            after.Stats.EffectiveStats.OrderBy(pair => pair.Key.ToString()).ToArray());
    }

    private static RuntimeActorState Actor(string instanceId, string entityId) =>
        new(
            RuntimeInstanceId.Parse(instanceId),
            ContentId.Parse(entityId),
            ContentId.Parse("player_team"),
            ContentId.Parse("hp"),
            CombatDefenseProfile.Empty,
            [new BattleResourceState(ContentId.Parse("hp"), 10m, 10m)],
            StandardProgressionIds.CoreStats.Select(stat =>
                new KeyValuePair<ContentId, decimal>(stat, 5m)),
            identity: new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse(instanceId),
                ContentId.Parse(entityId),
                StandardProgressionIds.Vessel,
                instanceId),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(ContentId.Parse("hp"), 10m)
            ],
            baseStats: StandardProgressionIds.CoreStats.Select(stat =>
                new KeyValuePair<ContentId, decimal>(stat, 5m)));

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorState actor) =>
        new(actor.InstanceId, actor.EntityId, actor.Identity.DisplayName);
}
