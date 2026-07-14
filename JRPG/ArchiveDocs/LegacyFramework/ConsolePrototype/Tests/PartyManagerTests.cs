using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using Xunit;

namespace Convergence.Tests;

public sealed class PartyManagerTests
{
    [Fact]
    public void Constructor_SetsInitialPlayerAsLocalLeader()
    {
        var player = new Combatant("Hero");

        var party = new PartyManager(player);

        Assert.Single(party.ActiveParty);
        Assert.Same(player, party.ActiveParty[0]);
        Assert.Equal(0, player.PartySlot);
        Assert.Equal(ControllerType.LocalPlayer, player.Controller);
    }

    [Fact]
    public void AddMember_CapsActivePartyAtFourAndOverflowsToReserve()
    {
        var party = new PartyManager(new Combatant("Hero"));
        var second = new Combatant("Second");
        var third = new Combatant("Third");
        var fourth = new Combatant("Fourth");
        var fifth = new Combatant("Fifth");

        Assert.True(party.AddMember(second));
        Assert.True(party.AddMember(third));
        Assert.True(party.AddMember(fourth));
        Assert.False(party.AddMember(fifth));

        Assert.Equal(4, party.ActiveParty.Count);
        Assert.Single(party.ReserveMembers);
        Assert.Same(fifth, party.ReserveMembers[0]);
        Assert.Equal(-1, fifth.PartySlot);
    }

    [Fact]
    public void SummonDemon_AddsActiveReferenceWithoutRemovingOwnership()
    {
        var owner = new Combatant("Hero", ClassType.Operator);
        var demon = new Combatant("Pixie", ClassType.Demon) { SourceId = "pixie" };
        owner.DemonStock.Add(demon);
        var party = new PartyManager(owner);

        bool summoned = party.SummonDemon(owner, demon);

        Assert.True(summoned);
        Assert.Contains(demon, party.ActiveParty);
        Assert.Contains(demon, owner.DemonStock);
        Assert.Equal(1, demon.PartySlot);
        Assert.Equal(ControlState.DirectControl, demon.BattleControl);
    }

    [Fact]
    public void ReturnDemon_RemovesActiveReferenceWithoutRemovingOwnership()
    {
        var owner = new Combatant("Hero", ClassType.Operator);
        var demon = new Combatant("Pixie", ClassType.Demon) { SourceId = "pixie" };
        owner.DemonStock.Add(demon);
        var party = new PartyManager(owner);
        party.SummonDemon(owner, demon);

        bool returned = party.ReturnDemon(owner, demon);

        Assert.True(returned);
        Assert.DoesNotContain(demon, party.ActiveParty);
        Assert.Contains(demon, owner.DemonStock);
        Assert.Equal(-1, demon.PartySlot);
    }

    [Fact]
    public void DismissDemon_RemovesFromActivePartyAndStock()
    {
        var owner = new Combatant("Hero", ClassType.Operator);
        var demon = new Combatant("Pixie", ClassType.Demon) { SourceId = "pixie" };
        owner.DemonStock.Add(demon);
        var party = new PartyManager(owner);
        party.SummonDemon(owner, demon);

        bool dismissed = party.DismissDemon(owner, demon);

        Assert.True(dismissed);
        Assert.DoesNotContain(demon, party.ActiveParty);
        Assert.DoesNotContain(demon, owner.DemonStock);
        Assert.Equal(-1, demon.PartySlot);
    }
}
