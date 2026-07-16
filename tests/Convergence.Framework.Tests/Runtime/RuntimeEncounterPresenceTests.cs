using System.Reflection;
using Convergence.Battle;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeEncounterPresenceTests
{
    [Fact]
    public void PartyReorganization_DoesNotImplicitlyDeployTheMovedActor()
    {
        RuntimeActorState owner = Actor("owner", isDeployed: true);
        RuntimeActorState reserve = Actor("reserve", isDeployed: false);
        RuntimeActorReferenceSnapshot ownerReference = Reference(owner);
        RuntimeActorReferenceSnapshot reserveReference = Reference(reserve);
        var party = new RuntimePartyRosterSnapshot(
            ownerReference,
            ownerLevel: 1,
            activeParty: [ownerReference],
            reserveMembers: [reserveReference]);

        PartyRosterTransitionResult result = new PartyRosterTransitionService().SwapPartyMember(
            new SwapPartyMemberRequest(party, ActiveIndex: 0, ReserveIndex: 0));

        Assert.True(result.Applied);
        Assert.Equal(reserveReference, Assert.Single(result.After.ActiveParty));
        Assert.False(reserve.IsDeployed);

        reserve.SetEncounterPresence(isDeployed: true);

        Assert.True(reserve.IsDeployed);
        Assert.Equal(reserveReference, Assert.Single(result.After.ActiveParty));
    }

    [Fact]
    public void EncounterPresence_IsTheOnlyActorLevelParticipationContract()
    {
        Type assemblyMarker = typeof(RuntimeActorState);
        Type[] exported = assemblyMarker.Assembly.GetExportedTypes();

        Assert.DoesNotContain(exported, type => type.Name == "RuntimeActorDeployment");
        Assert.DoesNotContain(exported, type => type.Name == "RuntimeActorDeploymentSnapshot");
        Assert.Null(typeof(RuntimeActorSnapshot).GetProperty("Deployment"));
        Assert.Null(typeof(RuntimeActorState).GetProperty("IsActive"));
        Assert.NotNull(typeof(RuntimeActorSnapshot).GetProperty("EncounterPresence"));
        Assert.NotNull(typeof(RuntimeActorState).GetProperty("IsDeployed"));

        ParameterInfo requestPresence = Assert.Single(
            typeof(CatalogBattleActorCreationRequest)
                .GetConstructors()
                .Single()
                .GetParameters(),
            parameter => parameter.Name == "IsDeployed");
        Assert.Equal(typeof(bool), requestPresence.ParameterType);
        Assert.False(requestPresence.HasDefaultValue);
    }

    [Fact]
    public void NondeployedActor_SuspendsConfiguredTurnDurationUntilExplicitlyDeployed()
    {
        ContentId eventId = ContentId.Parse("owner_turn_end");
        RuntimeActorState actor = Actor("reserve", isDeployed: false);
        actor.AddOtherStatus(
            ContentId.Parse("focus"),
            new TurnDurationDefinition(1, eventId, SuspendWhileReserve: true));

        Assert.Empty(actor.TickTimedStatuses(eventId));

        actor.SetEncounterPresence(isDeployed: true);
        BattleDurationTickResult tick = Assert.Single(actor.TickTimedStatuses(eventId));

        Assert.True(tick.Expired);
        Assert.Empty(actor.OtherStatuses);
    }

    private static RuntimeActorState Actor(string id, bool isDeployed) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"test.pack:{id}"),
            ContentId.Parse("player_team"),
            ContentId.Parse("hp"),
            CombatDefenseProfile.Empty,
            [new BattleResourceState(ContentId.Parse("hp"), 10m, 10m)],
            encounterPresence: new RuntimeEncounterPresenceSnapshot(isDeployed));

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorState actor) =>
        new(actor.InstanceId, actor.EntityId, actor.Identity.DisplayName);
}
