using System;
using System.Collections.Generic;
using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Logic.Field.State;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Host;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class PartyStockPresentationTests
{
    [Fact]
    public void OrganizationSlots_ReturnTypedSelectionAndPreserveLabelsAndCancellation()
    {
        Combatant player = new("Hero") { Level = 11 };
        Combatant jack = Demon("Jack Frost", level: 6);
        var party = new PartyManager(player);
        party.SummonDemon(player, Owned(player, jack));
        var io = new ScriptedGameIO().QueueMenu(1).QueueMenu(-1);
        var bridge = new StatusUIBridge(io, new FieldUIState(), party);

        OrganizationSlotSelectionResult selected = bridge.ShowOrganizationSlotsResult();
        OrganizationSlotSelectionResult back = bridge.ShowOrganizationSlotsResult();

        Assert.Equal(PartyStockSelectionResultKind.Selected, selected.Kind);
        Assert.Equal(1, selected.SlotIndex);
        Assert.Equal(PartyStockSelectionResultKind.Back, back.Kind);
        Assert.Equal(
            [
                "Leader: Hero            (Lv.11)",
                "Slot 2: Jack Frost      (Lv.6)",
                "Slot 3: [EMPTY]",
                "Slot 4: [EMPTY]",
                "Back"
            ],
            io.Menus[0].Options);
        Assert.True(io.Menus[0].SupportsStatusInspect);
        io.AssertConsumed();
    }

    [Fact]
    public void PersonaStock_ReturnsTypedSelectionActionAndLegacyWrapperParity()
    {
        Persona active = Persona("Orpheus", "Fool", level: 5);
        Persona pixie = Persona("Pixie", "Fairy", level: 3);
        Combatant player = new("Hero", ClassType.WildCard) { ActivePersona = active };
        player.PersonaStock.Add(pixie);
        var party = new PartyManager(player);
        var io = new ScriptedGameIO()
            .QueueMenu(1)
            .QueueMenu(0)
            .QueueMenu(1);
        var bridge = new StatusUIBridge(io, new FieldUIState(), party);

        PersonaStockSelectionResult selection = bridge.SelectPersonaFromStockResult(player);
        PersonaStockActionResult action = bridge.ShowPersonaDetailsResult(pixie, isEquipped: false);
        Persona wrappedSelection = bridge.SelectPersonaFromStock(player);

        Assert.Equal(PartyStockSelectionResultKind.Selected, selection.Kind);
        Assert.Same(pixie, selection.Persona);
        Assert.Equal(PersonaStockActionKind.Equip, action.Kind);
        Assert.Same(pixie, wrappedSelection);
        Assert.Equal(
            [
                "Orpheus         (Lv.5) Fool       [E]",
                "Pixie           (Lv.3) Fairy      ",
                "Back"
            ],
            io.Menus[0].Options);
        Assert.Equal(["Equip Persona", "Back"], io.Menus[1].Options);
        io.AssertConsumed();
    }

    [Fact]
    public void DemonStock_ReturnsUnavailableOrTypedSelectionWithDuplicateActiveLabels()
    {
        Combatant emptyOwner = new("Hero", ClassType.Operator);
        var emptyIo = new ScriptedGameIO();
        var emptyBridge = new StatusUIBridge(emptyIo, new FieldUIState(), new PartyManager(emptyOwner));

        DemonStockSelectionResult unavailable = emptyBridge.SelectDemonFromStockResult(emptyOwner);

        Assert.Equal(PartyStockSelectionResultKind.Unavailable, unavailable.Kind);
        Assert.Equal("No demons found.", Assert.Single(emptyIo.Writes).Text);
        Assert.Equal(800, Assert.Single(emptyIo.Waits));

        Combatant owner = new("Hero", ClassType.Operator);
        Combatant jack = Owned(owner, Demon("Jack Frost", level: 6));
        var party = new PartyManager(owner);
        party.SummonDemon(owner, jack);
        var io = new ScriptedGameIO().QueueMenu(1);
        var bridge = new StatusUIBridge(io, new FieldUIState(), party);

        DemonStockSelectionResult selected = bridge.SelectDemonFromStockResult(owner);

        Assert.Equal(PartyStockSelectionResultKind.Selected, selected.Kind);
        Assert.Same(jack, selected.Demon);
        Assert.Equal(
            [
                "Jack Frost      (Lv.6) [PARTY]",
                "Jack Frost      (Lv.6) [PARTY]",
                "Back"
            ],
            Assert.Single(io.Menus).Options);
        io.AssertConsumed();
    }

    [Fact]
    public void SummonTarget_ReturnsTypedReturnBackAndDemonResultsWithDisabledLabels()
    {
        Combatant owner = new("Hero", ClassType.Operator);
        Combatant jack = Owned(owner, Demon("Jack Frost", level: 6));
        Combatant slime = Owned(owner, Demon("Slime", level: 2, currentHp: 0));
        Combatant pixie = Owned(owner, Demon("Pixie", level: 4));
        var party = new PartyManager(owner);
        party.SummonDemon(owner, jack);
        var io = new ScriptedGameIO()
            .QueueMenu(0)
            .QueueMenu(4)
            .QueueMenu(3);
        var bridge = new StatusUIBridge(io, new FieldUIState(), party);

        SummonTargetSelectionResult returnSelection = bridge.SelectSummonTargetResult(owner, jack);
        SummonTargetSelectionResult back = bridge.SelectSummonTargetResult(owner, jack);
        object wrappedSelection = bridge.SelectSummonTarget(owner, jack);

        Assert.Equal(SummonTargetSelectionKind.ReturnToComp, returnSelection.Kind);
        Assert.Equal(SummonTargetSelectionKind.Back, back.Kind);
        Assert.Same(pixie, wrappedSelection);
        Assert.Equal(
            [
                "[ RETURN JACK FROST TO COMP ]",
                "Jack Frost      (Lv.6) [IN PARTY]",
                "Slime           (Lv.2) [DEAD]",
                "Pixie           (Lv.4) ",
                "Cancel"
            ],
            io.Menus[0].Options);
        Assert.Equal([false, true, true, false, false], io.Menus[0].DisabledOptions);
        io.AssertConsumed();
    }

    [Fact]
    public void FieldPartyStockMutationResultsCarryEventsAndPreserveLegacyState()
    {
        Combatant owner = new("Hero", ClassType.Operator);
        Combatant jack = Owned(owner, Demon("Jack Frost", level: 6));
        Combatant pixie = Owned(owner, Demon("Pixie", level: 4));
        var party = new PartyManager(owner);
        var messenger = new RecordingFieldMessenger();
        FieldServiceEngine engine = Engine(party, messenger);

        PartyStockPresentationResult summon = engine.SummonDemonDetailed(owner, jack);

        Assert.True(summon.Applied);
        Assert.Equal(PartyStockTransitionCode.Applied, summon.Code);
        Assert.NotEmpty(summon.AffectedInstanceIds);
        Assert.Contains(jack, party.ActiveParty);
        Assert.Contains(jack, owner.DemonStock);
        Assert.Equal("Jack Frost joined the party!", Assert.Single(summon.PresentationEvents).Message);
        Assert.Equal("Jack Frost joined the party!", Assert.Single(messenger.Messages));

        jack.IsGuarding = true;
        PartyStockPresentationResult swap = engine.SwapActiveDemonDetailed(owner, jack, pixie);

        Assert.True(swap.Applied);
        Assert.False(jack.IsGuarding);
        Assert.DoesNotContain(jack, party.ActiveParty);
        Assert.Contains(pixie, party.ActiveParty);
        Assert.Contains(jack, owner.DemonStock);
        Assert.Contains(pixie, owner.DemonStock);
        Assert.Equal("Jack Frost swapped for Pixie!", swap.PresentationEvents[0].Message);

        pixie.IsCharged = true;
        PartyStockPresentationResult returned = engine.ReturnDemonDetailed(owner, pixie);

        Assert.True(returned.Applied);
        Assert.False(pixie.IsCharged);
        Assert.DoesNotContain(pixie, party.ActiveParty);
        Assert.Contains(pixie, owner.DemonStock);
        Assert.Equal("Pixie returned to stock.", returned.PresentationEvents[0].Message);

        Assert.Equal(
            ["Jack Frost joined the party!", "Jack Frost swapped for Pixie!", "Pixie returned to stock."],
            messenger.Messages);
    }

    [Fact]
    public void FieldPartyStockRejectedMutationPreservesStateAndEmitsNoPresentationEvent()
    {
        Combatant owner = new("Hero", ClassType.Operator);
        var party = new PartyManager(owner);
        party.AddMember(new Combatant("Ally 1"));
        party.AddMember(new Combatant("Ally 2"));
        party.AddMember(new Combatant("Ally 3"));
        Combatant pixie = Owned(owner, Demon("Pixie", level: 4));
        var messenger = new RecordingFieldMessenger();
        FieldServiceEngine engine = Engine(party, messenger);

        PartyStockPresentationResult rejected = engine.SummonDemonDetailed(owner, pixie);

        Assert.False(rejected.Applied);
        Assert.Equal(PartyStockTransitionCode.PartyFull, rejected.Code);
        Assert.DoesNotContain(pixie, party.ActiveParty);
        Assert.Contains(pixie, owner.DemonStock);
        Assert.Empty(rejected.PresentationEvents);
        Assert.Empty(messenger.Messages);
    }

    [Fact]
    public void PersonaSwapPresentationPreservesHpSpCappingAndAffectedIds()
    {
        Persona active = Persona("Orpheus", "Fool", level: 5, stat: 40);
        Persona pixie = Persona("Pixie", "Fairy", level: 3, stat: 1);
        Combatant owner = new("Hero", ClassType.WildCard)
        {
            ActivePersona = active
        };
        owner.PersonaStock.Add(pixie);
        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            owner.CharacterStats[stat] = 10;
        }

        owner.RecalculateResources();
        owner.CurrentHP = owner.MaxHP;
        owner.CurrentSP = owner.MaxSP;
        var party = new PartyManager(owner);
        var messenger = new RecordingFieldMessenger();
        FieldServiceEngine engine = Engine(party, messenger);

        PartyStockPresentationResult result = engine.PerformPersonaSwapDetailed(owner, pixie);

        Assert.True(result.Applied);
        Assert.Equal(PartyStockPresentationOperation.SwapActivePersona, result.Operation);
        Assert.NotEmpty(result.AffectedInstanceIds);
        Assert.Same(pixie, owner.ActivePersona);
        Assert.Same(active, Assert.Single(owner.PersonaStock));
        Assert.Equal(owner.MaxHP, owner.CurrentHP);
        Assert.Equal(owner.MaxSP, owner.CurrentSP);
        Assert.Equal("Equipped Pixie!", Assert.Single(result.PresentationEvents).Message);
        Assert.Equal("Equipped Pixie!", Assert.Single(messenger.Messages));
    }

    private static FieldServiceEngine Engine(PartyManager party, IFieldMessenger messenger) =>
        new(
            messenger,
            new ScriptedGameIO(),
            new EconomyManager(),
            new InventoryManager(),
            party,
            new DungeonState());

    private static Combatant Demon(string name, int level, int currentHp = 10) =>
        new(name, ClassType.Demon)
        {
            SourceId = name.ToLowerInvariant().Replace(' ', '_'),
            Level = level,
            CurrentHP = currentHp,
            MaxHP = 10
        };

    private static Combatant Owned(Combatant owner, Combatant demon)
    {
        owner.DemonStock.Add(demon);
        return demon;
    }

    private static Persona Persona(string name, string race, int level, int stat = 2)
    {
        var persona = new Persona
        {
            Name = name,
            Race = race,
            Level = level
        };
        foreach (StatType type in Enum.GetValues<StatType>())
        {
            persona.StatModifiers[type] = stat;
        }

        return persona;
    }

    private sealed class RecordingFieldMessenger : IFieldMessenger
    {
        public event EventHandler<FieldMessageArgs>? OnMessagePublished;
        public List<string?> Messages { get; } = [];

        public void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            Messages.Add(message);
            OnMessagePublished?.Invoke(this, new FieldMessageArgs(message, color, delay, waitForInput, clearScreen));
        }
    }
}
