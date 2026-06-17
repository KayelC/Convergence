using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.State;
using Xunit;

namespace Convergence.Tests.Host;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class ConsolePlainMenuCommandTests
{
    [Fact]
    public void ServiceInventoryAndStatusHubs_ReturnTypedCommandsWhilePreservingMenus()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var player = new Combatant("Hero")
        {
            Class = ClassType.Operator,
            CurrentHP = 80,
            MaxHP = 100,
            CurrentSP = 30,
            MaxSP = 50,
            Level = 12
        };
        var io = new ScriptedGameIO()
            .QueueMenu(4)
            .QueueMenu(5)
            .QueueMenu(3)
            .QueueMenu(2);
        var ui = new FieldUIState();
        var party = new PartyManager(player);
        var service = new ServiceUIBridge(io, ui, new EconomyManager(), party);
        var inventory = new InventoryUIBridge(io, ui, new InventoryManager(), party);
        var status = new StatusUIBridge(io, ui, party);

        Assert.Equal(FieldMainMenuCommand.OrganizeParty, service.ShowFieldMainMenuCommand(player));
        Assert.Equal(CityServicesCommand.Cathedral, service.ShowCityServicesCommand());
        Assert.Equal(InventorySubMenuCommand.Demons, inventory.ShowInventorySubMenuCommand(player));
        Assert.Equal(StatusHubCommand.DemonStock, status.ShowStatusHubCommand(player));

        Assert.Equal(4, ui.MainMenuIndex);
        Assert.Equal(5, ui.CityMenuIndex);
        Assert.Equal(3, ui.InventoryMenuIndex);
        Assert.Equal(2, ui.StatusHubIndex);
        Assert.Equal(
            ["Explore Tartarus", "City Services", "Inventory", "Status", "Organize Party", "Exit Game"],
            io.Menus[0].Options);
        Assert.Equal(
            ["Blacksmith (Weapons)", "Clothing Store (Armor/Boots)", "Jeweler (Accessories)", "Pharmacy (Items)", "Hospital (Heal)", "Cathedral of Shadows", "Back"],
            io.Menus[1].Options);
        Assert.Equal(["Use Item", "Use Skill", "Equipment", "Demons (COMP)", "Back"], io.Menus[2].Options);
        Assert.Equal(["Allocate Stats", "Change Equipment", "Demon Stock", "Back"], io.Menus[3].Options);
        io.AssertConsumed();
    }

    [Fact]
    public void DungeonAndTargetSelection_UseHostCommandSourceAndPreserveDisabledOptions()
    {
        var player = new Combatant("Hero")
        {
            Class = ClassType.Operator,
            CurrentHP = 40,
            MaxHP = 50,
            CurrentSP = 20,
            MaxSP = 30
        };
        var ally = new Combatant("Ally")
        {
            CurrentHP = 10,
            MaxHP = 30,
            CurrentSP = 5,
            MaxSP = 10
        };
        var io = new ScriptedGameIO()
            .QueueMenu(5)
            .QueueMenu(1)
            .QueueMenu(1)
            .QueueMenu(1);
        var ui = new FieldUIState();
        var party = new PartyManager(player);
        party.AddMember(ally);
        var dungeon = new DungeonUIBridge(io, ui);
        var inventory = new InventoryUIBridge(io, ui, new InventoryManager(), party);

        DungeonFloorResult lobby = new()
        {
            FloorNumber = 1,
            BlockName = "Thebel",
            Type = DungeonEventType.SafeRoom,
            Description = "The lobby.",
            HasTerminal = true
        };

        Assert.Equal(DungeonFloorActionCommand.Status, dungeon.ShowFloorActionCommand(lobby, player));
        Assert.Equal(7, io.Menus[0].Options.Count);
        Assert.Equal(5, dungeon.SelectEntryPoint([1, 5]));
        Assert.Equal(5, dungeon.SelectWarpDestination([1, 5], currentFloor: 1));
        Assert.Equal([true, false, false], io.Menus[2].DisabledOptions);
        Assert.Same(ally, inventory.SelectFieldTarget(player, "Medicine"));
        Assert.Equal(["Hero            (HP:  40/ 50 SP:  20/ 30)", "Ally            (HP:  10/ 30 SP:   5/ 10)", "Back"], io.Menus[3].Options);
        io.AssertConsumed();
    }

    [Fact]
    public void StatusProjection_RendersCurrentHumanStatusWithoutChangingText()
    {
        var player = new Combatant("Hero")
        {
            Class = ClassType.Human,
            Level = 9,
            Exp = 120,
            CurrentHP = 70,
            MaxHP = 90,
            CurrentSP = 25,
            MaxSP = 40
        };
        player.CharacterStats[StatType.St] = 7;
        player.CharacterStats[StatType.Ma] = 5;
        player.CharacterStats[StatType.Vi] = 6;
        player.CharacterStats[StatType.Ag] = 4;
        player.CharacterStats[StatType.Lu] = 3;

        string rendered = LegacyHumanStatusProjection.FromCombatant(player).Render();

        Assert.Contains("Name: Hero (Lv.9) | Class: Human", rendered, StringComparison.Ordinal);
        Assert.Contains("HP:  70/ 90 SP:  25/ 40", rendered, StringComparison.Ordinal);
        Assert.Contains("St  :   7", rendered, StringComparison.Ordinal);
        Assert.Contains("Ma  :   5", rendered, StringComparison.Ordinal);
        Assert.Equal(rendered, new StatusUIBridge(
            new ScriptedGameIO(),
            new FieldUIState(),
            new PartyManager(player)).RenderHumanStatusToString(player));
    }
}
