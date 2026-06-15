using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Messaging;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests;

public sealed class PartyStockAdapterTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(10, 5)]
    [InlineData(20, 7)]
    [InlineData(30, 10)]
    [InlineData(40, 12)]
    public void PartyManager_StockCapacityDelegatesToFrameworkPolicy(int level, int capacity)
    {
        Combatant owner = new("Hero", ClassType.Operator) { Level = level };
        var party = new PartyManager(owner);

        for (int index = 0; index < capacity; index++)
        {
            owner.DemonStock.Add(Demon($"demon_{index}"));
            owner.PersonaStock.Add(new Persona { Name = $"Persona {index}", Level = 1 });
        }

        Assert.False(party.HasOpenDemonStockSlot(owner));
        Assert.False(party.HasOpenPersonaStockSlot(owner));
    }

    [Fact]
    public void PartyManager_ActiveReserveAndDemonCommandsPreserveLegacyBehavior()
    {
        Combatant owner = new("Hero", ClassType.Operator);
        var party = new PartyManager(owner);
        var second = new Combatant("Second");
        var third = new Combatant("Third");
        var fourth = new Combatant("Fourth");
        var reserve = new Combatant("Reserve");

        Assert.True(party.AddMember(second));
        Assert.True(party.AddMember(third));
        Assert.True(party.AddMember(fourth));
        Assert.False(party.AddMember(reserve));
        Assert.Equal(4, party.ActiveParty.Count);
        Assert.Single(party.ReserveMembers);

        party.SwapMember(activeIndex: 2, reserveIndex: 0);
        Assert.Same(reserve, party.ActiveParty[2]);
        Assert.Same(third, Assert.Single(party.ReserveMembers));
        Assert.Equal(2, reserve.PartySlot);
        Assert.Equal(-1, third.PartySlot);

        Combatant pixie = Demon("pixie");
        Combatant jack = Demon("jack");
        owner.DemonStock.AddRange([pixie, jack]);

        Assert.False(party.SummonDemon(owner, pixie));

        party.ReturnDemon(owner, reserve);
        Assert.True(party.SummonDemon(owner, pixie));
        Assert.Contains(pixie, party.ActiveParty);
        Assert.Contains(pixie, owner.DemonStock);
        Assert.Equal(ControlState.DirectControl, pixie.BattleControl);

        Assert.True(party.SwapActiveDemon(owner, pixie, jack));
        Assert.DoesNotContain(pixie, party.ActiveParty);
        Assert.Contains(jack, party.ActiveParty);
        Assert.Contains(pixie, owner.DemonStock);
        Assert.Contains(jack, owner.DemonStock);

        Assert.True(party.ReturnDemon(owner, jack));
        Assert.DoesNotContain(jack, party.ActiveParty);
        Assert.Contains(jack, owner.DemonStock);

        Assert.True(party.DismissDemon(owner, jack));
        Assert.DoesNotContain(jack, owner.DemonStock);
    }

    [Fact]
    public void PartyManager_ReplaceDemonPreservesActiveSlotAndOwnedStock()
    {
        Combatant owner = new("Hero", ClassType.Operator);
        Combatant oldDemon = Demon("old_demon");
        Combatant newDemon = Demon("new_demon");
        owner.DemonStock.Add(oldDemon);
        var party = new PartyManager(owner);
        Assert.True(party.SummonDemon(owner, oldDemon));

        party.ReplaceDemon(owner, oldDemon, newDemon);

        Assert.DoesNotContain(oldDemon, party.ActiveParty);
        Assert.DoesNotContain(oldDemon, owner.DemonStock);
        Assert.Same(newDemon, party.ActiveParty[1]);
        Assert.Same(newDemon, Assert.Single(owner.DemonStock));
        Assert.Equal(1, newDemon.PartySlot);
        Assert.Equal(-1, oldDemon.PartySlot);
    }

    [Fact]
    public void BattlePersonaSwap_UsesFrameworkCommandAndPreservesFlatResourceCapping()
    {
        Combatant actor = PersonaUserWithActiveAndStock(out Persona oldActive, out Persona newPersona);
        var messenger = new RecordingBattleMessenger();
        var processor = new ActionProcessor(new StatusRegistry(), new BattleKnowledge(), messenger);
        actor.RecalculateResources();
        actor.CurrentHP = actor.MaxHP;
        actor.CurrentSP = actor.MaxSP;

        processor.ExecutePersonaSwap(actor, newPersona);

        Assert.Same(newPersona, actor.ActivePersona);
        Assert.Same(oldActive, Assert.Single(actor.PersonaStock));
        Assert.Equal(actor.MaxHP, actor.CurrentHP);
        Assert.Equal(actor.MaxSP, actor.CurrentSP);
        Assert.Contains("Hero switched to Pixie!", messenger.Messages);
    }

    [Fact]
    public void FieldPersonaSwap_UsesSameCommandAndPreservesMessageAndCapping()
    {
        Combatant actor = PersonaUserWithActiveAndStock(out Persona oldActive, out Persona newPersona);
        var messenger = new RecordingFieldMessenger();
        var party = new PartyManager(actor);
        var engine = new FieldServiceEngine(
            messenger,
            new ScriptedGameIO(),
            new EconomyManager(),
            new InventoryManager(),
            party,
            new DungeonState());
        actor.RecalculateResources();
        actor.CurrentHP = actor.MaxHP;
        actor.CurrentSP = actor.MaxSP;

        engine.PerformPersonaSwap(actor, newPersona);

        Assert.Same(newPersona, actor.ActivePersona);
        Assert.Same(oldActive, Assert.Single(actor.PersonaStock));
        Assert.Equal(actor.MaxHP, actor.CurrentHP);
        Assert.Equal(actor.MaxSP, actor.CurrentSP);
        Assert.Contains("Equipped Pixie!", messenger.Messages);
    }

    [Fact]
    public void FusionInventoryTransactions_ConsumeAndReplaceThroughFrameworkAdapter()
    {
        Combatant owner = new("Hero", ClassType.Operator);
        Combatant oldDemon = Demon("old_demon");
        Combatant newDemon = Demon("new_demon");
        owner.DemonStock.Add(oldDemon);
        var party = new PartyManager(owner);
        Assert.True(party.SummonDemon(owner, oldDemon));
        var context = new FusionContext(owner, [], null, [], "new_demon", new RecordingFusionMessenger(), party);

        FusionInventoryTransaction.ReplaceDemon(context, oldDemon, newDemon);

        Assert.Same(newDemon, party.ActiveParty[1]);
        Assert.Same(newDemon, Assert.Single(owner.DemonStock));
        Assert.Equal(oldDemon.OwnerId, newDemon.OwnerId);
        Assert.Equal(oldDemon.Controller, newDemon.Controller);

        FusionInventoryTransaction.ConsumeDemon(context, newDemon);

        Assert.DoesNotContain(newDemon, party.ActiveParty);
        Assert.Empty(owner.DemonStock);
    }

    [Fact]
    public void FusionInventoryTransactions_ConsumeAndReplacePersonasThroughFrameworkAdapter()
    {
        Persona active = new() { Name = "Orpheus", Level = 1 };
        Combatant owner = new("Hero", ClassType.WildCard)
        {
            ActivePersona = active
        };
        Persona stockPersona = new() { Name = "Pixie", Level = 1 };
        Persona replacement = new() { Name = "Jack Frost", Level = 1 };
        owner.PersonaStock.Add(stockPersona);

        FusionInventoryTransaction.ReplacePersona(owner, stockPersona, replacement);

        Assert.Same(active, owner.ActivePersona);
        Assert.Same(replacement, Assert.Single(owner.PersonaStock));

        FusionInventoryTransaction.ConsumePersona(owner, owner.ActivePersona);

        Assert.Null(owner.ActivePersona);
        Assert.Same(replacement, Assert.Single(owner.PersonaStock));
    }

    private static Combatant PersonaUserWithActiveAndStock(out Persona oldActive, out Persona newPersona)
    {
        oldActive = new Persona { Name = "Orpheus", Level = 1 };
        newPersona = new Persona { Name = "Pixie", Level = 1 };
        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            oldActive.StatModifiers[stat] = 40;
            newPersona.StatModifiers[stat] = 1;
        }

        var actor = new Combatant("Hero", ClassType.WildCard)
        {
            ActivePersona = oldActive
        };
        actor.PersonaStock.Add(newPersona);
        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            actor.CharacterStats[stat] = 10;
        }

        return actor;
    }

    private static Combatant Demon(string sourceId) =>
        new(sourceId, ClassType.Demon) { SourceId = sourceId };

    private sealed class RecordingBattleMessenger : IBattleMessenger
    {
        public event EventHandler<BattleMessageArgs>? OnMessagePublished;
        public List<string> Messages { get; } = [];

        public void Publish(
            string message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            Combatant? analysisTarget = null,
            bool clearScreen = false)
        {
            Messages.Add(message);

            OnMessagePublished?.Invoke(this, new BattleMessageArgs(message, color, delay, waitForInput, analysisTarget, clearScreen));
        }
    }

    private sealed class RecordingFieldMessenger : IFieldMessenger
    {
        public event EventHandler<FieldMessageArgs>? OnMessagePublished;
        public List<string> Messages { get; } = [];

        public void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            if (message is not null)
            {
                Messages.Add(message);
            }

            OnMessagePublished?.Invoke(this, new FieldMessageArgs(message, color, delay, waitForInput, clearScreen));
        }
    }

    private sealed class RecordingFusionMessenger : IFusionMessenger
    {
        public event EventHandler<FusionMessageArgs>? OnMessagePublished;

        public void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false) =>
            OnMessagePublished?.Invoke(this, new FusionMessageArgs(message, color, delay, waitForInput, clearScreen));
    }
}
