using Convergence.Battle;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeActorAffiliationTests
{
    [Fact]
    public void ActorAffiliation_ContainsOnlyCommandAuthorityAndTeam()
    {
        Type[] exported = typeof(RuntimeActorSnapshot).Assembly.GetExportedTypes();

        Assert.DoesNotContain(exported, type => type.Name == "RuntimeActorOwnershipSnapshot");
        Assert.Null(typeof(RuntimeActorSnapshot).GetProperty("Ownership"));
        Assert.Null(typeof(RuntimeActorAffiliationSnapshot).GetProperty("OwnerInstanceId"));
        Assert.Null(typeof(RuntimeActorAffiliationSnapshot).GetProperty("ControllerId"));
        Assert.NotNull(typeof(RuntimeActorAffiliationSnapshot).GetProperty("CommandAuthorityId"));
        Assert.NotNull(typeof(RuntimeActorAffiliationSnapshot).GetProperty("TeamId"));

        Assert.False(typeof(CatalogBattleActorCreationRequest)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(parameter => parameter.Name == "CommandAuthorityId")
            .HasDefaultValue);
        Assert.False(typeof(RuntimeActorState)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(parameter => parameter.Name == "affiliation")
            .HasDefaultValue);
    }

    [Fact]
    public void PartyRosterAggregate_IsTheOnlyActorOwnershipContract()
    {
        RuntimeActorState owner = Actor("owner", "authority_one", "player_team");
        RuntimeActorState hosted = Actor("hosted", "authority_two", "player_team");
        RuntimeActorReferenceSnapshot ownerReference = Reference(owner);
        RuntimeActorReferenceSnapshot hostedReference = Reference(hosted);
        var roster = new RuntimePartyRosterSnapshot(
            ownerReference,
            activeParty: [ownerReference],
            activeHostedEntity: hostedReference,
            hostedEntityRoster: [hostedReference]);

        Assert.Equal(ownerReference, roster.Owner);
        Assert.Equal(hostedReference, roster.ActiveHostedEntity);
        Assert.Contains(hostedReference, roster.HostedEntityRoster);
        Assert.Equal(ContentId.Parse("authority_one"), owner.Affiliation.CommandAuthorityId);
        Assert.Equal(ContentId.Parse("authority_two"), hosted.Affiliation.CommandAuthorityId);
    }

    private static RuntimeActorState Actor(
        string id,
        string commandAuthorityId,
        string teamId) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"test.pack:{id}"),
            ContentId.Parse(teamId),
            ContentId.Parse("hp"),
            CombatDefenseProfile.Empty,
            [new BattleResourceState(ContentId.Parse("hp"), 10m, 10m)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            affiliation: new RuntimeActorAffiliationSnapshot(
                ContentId.Parse(commandAuthorityId),
                ContentId.Parse(teamId)));

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorState actor) =>
        new(actor.InstanceId, actor.EntityId, actor.Identity.DisplayName);
}
