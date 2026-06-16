using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Logic.Field.State;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Bridges;
using Xunit;

namespace Convergence.Tests.Parity;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class LegacyWorkflowCharacterizationTests
{
    [Fact]
    public void FieldAndCityMenus_PreserveCurrentOptionOrdering()
    {
        var io = new ScriptedGameIO().QueueMenu(-1, -1);
        var player = new Combatant("Hero", ClassType.Operator);
        var party = new PartyManager(player);
        var state = new FieldUIState();
        var bridge = new ServiceUIBridge(io, state, new EconomyManager(), party);

        Assert.Equal("Cancel", bridge.ShowFieldMainMenu(player));
        Assert.Equal("Back", bridge.ShowCityServicesMenu());

        Assert.Equal(
            ["Explore Tartarus", "City Services", "Inventory", "Status", "Organize Party", "Exit Game"],
            io.Menus[0].Options);
        Assert.Equal(
            [
                "Blacksmith (Weapons)", "Clothing Store (Armor/Boots)", "Jeweler (Accessories)",
                "Pharmacy (Items)", "Hospital (Heal)", "Cathedral of Shadows", "Back"
            ],
            io.Menus[1].Options);
        io.AssertConsumed();
    }

    [Fact]
    public void InventoryMenu_PreservesClassSpecificOptions()
    {
        var io = new ScriptedGameIO().QueueMenu(-1, -1);
        var state = new FieldUIState();

        var human = new Combatant("Human", ClassType.Human);
        var humanBridge = new InventoryUIBridge(
            io,
            state,
            new InventoryManager(),
            new PartyManager(human));
        Assert.Equal("Back", humanBridge.ShowInventorySubMenu(human));

        var operatorActor = new Combatant("Operator", ClassType.Operator);
        var operatorBridge = new InventoryUIBridge(
            io,
            new FieldUIState(),
            new InventoryManager(),
            new PartyManager(operatorActor));
        Assert.Equal("Back", operatorBridge.ShowInventorySubMenu(operatorActor));

        Assert.Equal(["Use Item", "Use Skill", "Equipment", "Back"], io.Menus[0].Options);
        Assert.Equal(["Use Item", "Use Skill", "Equipment", "Demons (COMP)", "Back"], io.Menus[1].Options);
    }

    [Fact]
    public void EquipmentAndStatusMenus_PreserveCurrentSurfaces()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO().QueueMenu(-1, -1);
        var player = new Combatant("Hero", ClassType.WildCard);
        var bridge = new StatusUIBridge(io, new FieldUIState(), new PartyManager(player));

        Assert.Equal("Back", bridge.ShowStatusHub(player));
        Assert.Equal("Back", bridge.ShowEquipSlotMenu(player));

        Assert.Equal(["Allocate Stats", "Change Equipment", "Persona Stock", "Back"], io.Menus[0].Options);
        Assert.Equal(
            ["Weapon:    None", "Armor:     None", "Boots:     None", "Accessory: None", "Back"],
            io.Menus[1].Options);
    }

    [Fact]
    public void MenuSurfaces_PreserveOptionOrderingAndCancellation()
    {
        var io = new ScriptedGameIO().QueueMenu(-1, -1);
        var player = new Combatant("Hero", ClassType.Operator)
        {
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50
        };

        var dungeonBridge = new DungeonUIBridge(io, new FieldUIState());
        var lobby = new DungeonFloorResult
        {
            FloorNumber = 1,
            BlockName = "Entrance",
            Type = DungeonEventType.SafeRoom,
            Description = "The Lobby.",
            HasTerminal = true
        };
        Assert.Equal("Cancel", dungeonBridge.ShowFloorActionMenu(lobby, player));

        var cathedral = new CathedralUIBridge(io, new FieldUIState(), new CompendiumRegistry(io));
        FusionMainMenuResult result = cathedral.ShowCathedralMainMenu(8);
        Assert.Equal(FusionMenuResultKind.Back, result.Kind);

        Assert.Equal(
            [
                "Ascend Stairs", "Clock (Heal)", "Terminal (Warp)", "Return to City",
                "Inventory", "Status", "Organize Party"
            ],
            io.Menus[0].Options);
        Assert.Equal(
            ["Binary Fusion", "Sacrificial Fusion", "Browse Compendium", "Register Demon", "Back"],
            io.Menus[1].Options);
    }

    [Fact]
    public void ShopTransactions_PreservePricingInventoryAndFailureBehavior()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var inventory = new InventoryManager();
        var economy = new EconomyManager();
        var engine = new ShopEngine(inventory, economy, new FieldMessenger());
        var player = new Combatant("Hero");
        player.CharacterStats[StatType.Lu] = 10;
        ShopEntry medicine = Assert.Single(Database.ShopInventory, entry =>
            entry.Id == "101" && entry.Category == ShopCategory.Item);

        int buyPrice = engine.CalculateBuyPrice(medicine, player);
        economy.AddMacca(buyPrice);
        Assert.True(engine.ExecutePurchase(medicine, player));
        Assert.Equal(0, economy.Macca);
        Assert.Equal(1, inventory.GetQuantity("101"));

        Assert.False(engine.ExecutePurchase(medicine, player));
        Assert.Equal(1, inventory.GetQuantity("101"));

        int sellPrice = engine.CalculateSellPrice("101", ShopCategory.Item, player);
        engine.ExecuteSale("101", ShopCategory.Item, player);
        Assert.Equal(sellPrice, economy.Macca);
        Assert.Equal(0, inventory.GetQuantity("101"));
    }

    [Fact]
    public void InventoryAndEconomyManagers_DelegateThroughFrameworkResourceServices()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var inventory = new InventoryManager();
        var economy = new EconomyManager();

        inventory.AddItem("101", 2);
        inventory.AddItem("missing", 9);
        inventory.RemoveItem("101", 1);
        inventory.RemoveItem("101", 9);
        inventory.AddEquipment("1", ShopCategory.Weapon);
        inventory.AddEquipment("1", ShopCategory.Weapon);
        inventory.AddEquipment("missing", ShopCategory.Weapon);

        economy.AddMacca(100);
        Assert.False(economy.SpendMacca(150));
        Assert.True(economy.SpendMacca(40));

        Assert.Equal(1, inventory.GetQuantity("101"));
        Assert.Equal(0, inventory.GetQuantity("missing"));
        Assert.Equal(["1"], inventory.OwnedWeapons);
        Assert.Equal(60, economy.Macca);
    }

    [Fact]
    public void ShopTransactions_RejectDuplicateAndEquippedEquipmentWithoutMutation()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var inventory = new InventoryManager();
        var economy = new EconomyManager();
        var engine = new ShopEngine(inventory, economy, new FieldMessenger());
        var player = new Combatant("Hero");
        player.CharacterStats[StatType.Lu] = 0;

        ShopEntry sword = Assert.Single(Database.ShopInventory, entry =>
            entry.Id == "1" && entry.Category == ShopCategory.Weapon);
        inventory.AddEquipment("1", ShopCategory.Weapon);
        player.EquippedWeapon = Database.Weapons["1"];
        int beforeMacca = 1_000;
        economy.AddMacca(beforeMacca);

        Assert.False(engine.ExecutePurchase(sword, player));
        Assert.Equal(beforeMacca, economy.Macca);
        Assert.Equal(["1"], inventory.OwnedWeapons);

        engine.ExecuteSale("1", ShopCategory.Weapon, player);
        Assert.Equal(beforeMacca, economy.Macca);
        Assert.Equal(["1"], inventory.OwnedWeapons);
    }

    [Fact]
    public void FieldServiceEquipmentHospitalAndItems_UseFrameworkBackedTransactions()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var inventory = new InventoryManager();
        var economy = new EconomyManager();
        var player = new Combatant("Hero") { MaxHP = 100, CurrentHP = 40, MaxSP = 30, CurrentSP = 10 };
        var party = new PartyManager(player);
        var dungeon = new DungeonState { CurrentFloor = 7 };
        var engine = new FieldServiceEngine(
            new FieldMessenger(),
            io,
            economy,
            inventory,
            party,
            dungeon);

        engine.PerformEquip(player, "1", ShopCategory.Weapon);
        Assert.Null(player.EquippedWeapon);

        inventory.AddEquipment("1", ShopCategory.Weapon);
        engine.PerformEquip(player, "1", ShopCategory.Weapon);
        Assert.Equal("1", player.EquippedWeapon?.Id);

        player.InflictAilment(new AilmentData { Name = "Poison", CureKeyword = "Poison" });
        player.AddBuff("PhysAtk", 3);
        economy.AddMacca(engine.CalculateRestorationCost(player));
        Assert.True(engine.TryRestoreCombatant(player));
        Assert.Equal(player.MaxHP, player.CurrentHP);
        Assert.Equal(player.MaxSP, player.CurrentSP);
        Assert.Null(player.CurrentAilment);
        Assert.Empty(player.Buffs);
        Assert.Equal(0, economy.Macca);

        var itemUser = new Combatant("Medic") { MaxHP = 100, CurrentHP = 100, MaxSP = 20, CurrentSP = 20 };
        ItemData medicine = Database.Items["101"];
        inventory.AddItem("101", 1);
        Assert.Equal(ItemUsageResult.Failed, engine.ExecuteItemUsage(medicine, itemUser, itemUser));
        Assert.Equal(1, inventory.GetQuantity("101"));

        itemUser.CurrentHP = 50;
        Assert.Equal(ItemUsageResult.Applied, engine.ExecuteItemUsage(medicine, itemUser, itemUser));
        Assert.Equal(0, inventory.GetQuantity("101"));
        Assert.Equal(100, itemUser.CurrentHP);

        ItemData goho = Database.Items["114"];
        inventory.AddItem("114", 1);
        Assert.Equal(ItemUsageResult.RequestDungeonExit, engine.ExecuteItemUsage(goho, player, player));
        Assert.Equal(0, inventory.GetQuantity("114"));
        Assert.Equal(1, dungeon.CurrentFloor);
    }

    [Fact]
    public void DungeonNavigation_PreservesLobbyFixedFloorsTerminalsAndBossDefeat()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var state = new DungeonState();
        var manager = new DungeonManager(state);

        DungeonFloorResult lobby = manager.ProcessCurrentFloor();
        Assert.Equal(1, lobby.FloorNumber);
        Assert.Equal(DungeonEventType.SafeRoom, lobby.Type);
        Assert.True(lobby.HasTerminal);

        manager.WarpToFloor(10);
        DungeonFloorResult safeRoom = manager.ProcessCurrentFloor();
        Assert.Equal(DungeonEventType.SafeRoom, safeRoom.Type);
        Assert.Contains(10, manager.GetUnlockedTerminals());

        manager.WarpToFloor(5);
        DungeonFloorResult boss = manager.ProcessCurrentFloor();
        Assert.Equal(DungeonEventType.Boss, boss.Type);
        Assert.Equal(["chimera"], boss.EnemyIds);

        manager.RegisterBossDefeat("chimera");
        DungeonFloorResult defeated = manager.ProcessCurrentFloor();
        Assert.Equal(DungeonEventType.Empty, defeated.Type);
        Assert.Empty(defeated.EnemyIds);

        manager.WarpToFloor(20);
        Assert.Equal(DungeonEventType.BlockEnd, manager.ProcessCurrentFloor().Type);
        manager.Descend();
        Assert.Equal(19, manager.CurrentFloor);
        manager.Ascend();
        Assert.Equal(20, manager.CurrentFloor);
    }

    [Fact]
    public void FieldConductor_RoutesDungeonEntryTerminalWarpAndReturnThroughFrameworkState()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO().QueueMenu(
            0, // main field: Explore Tartarus
            1, // entry point: Floor 10
            2, // floor 10: Access Terminal
            0, // terminal: Lobby
            3, // lobby: Return to City
            5); // main field: Exit Game
        var player = new Combatant("Hero", ClassType.Operator)
        {
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50
        };
        var dungeon = new DungeonState();
        dungeon.UnlockTerminal(10);
        var conductor = new FieldConductor(
            player,
            new InventoryManager(),
            new EconomyManager(),
            dungeon,
            io,
            new BattleKnowledge(),
            new CompendiumRegistry(io));

        conductor.NavigateMenus();

        Assert.Equal(1, dungeon.CurrentFloor);
        Assert.Contains(10, dungeon.UnlockedTerminals);
        Assert.Equal("=== SELECT ENTRY POINT ===", io.Menus[1].Header);
        Assert.Contains("Floor 10", io.Menus[1].Options);
        Assert.Contains("Access Terminal (Return)", io.Menus[2].Options);
        Assert.Equal("=== TERMINAL SYSTEM ===", io.Menus[3].Header);
        io.AssertConsumed();
    }

    [Fact]
    public void ExplorationProcessor_PrepareEncounterPreservesHydrationAndDuplicateSuffixes()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var player = new Combatant("Hero", ClassType.Operator);
        var dungeon = new DungeonState();
        var manager = new DungeonManager(dungeon);
        var messenger = new FieldMessenger();
        var serviceEngine = new FieldServiceEngine(
            messenger,
            io,
            new EconomyManager(),
            new InventoryManager(),
            new PartyManager(player),
            dungeon);
        var processor = new ExplorationProcessor(
            messenger,
            manager,
            dungeon,
            new DungeonUIBridge(io, new FieldUIState()),
            serviceEngine);

        List<Combatant> enemies = processor.PrepareEncounter(["pixie", "pixie"]);

        Assert.Equal(["Pixie A", "Pixie B"], enemies.Select(enemy => enemy.Name));
        Assert.All(enemies, enemy => Assert.Equal(ClassType.Demon, enemy.Class));
    }

    [Fact]
    public void Negotiation_PreservesBlockedFamiliarFailureAndRecruitmentOutcomes()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();

        var blockedIo = new ScriptedGameIO();
        var blockedActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var blockedParty = new PartyManager(blockedActor);
        Combatant blockedTarget = CombatantFactory.CreateEnemy("pixie");
        for (int i = 0; i < 8; i++) MoonPhaseSystem.Advance();
        NegotiationResult blocked = new NegotiationEngine(
            blockedIo,
            blockedParty,
            new InventoryManager(),
            new EconomyManager(),
            new Random(1)).StartNegotiation(blockedActor, blockedTarget, [blockedTarget]);
        Assert.Equal(NegotiationResult.Failure, blocked);
        Assert.Contains("Full Moon", blockedIo.CombinedOutput, StringComparison.Ordinal);

        MoonPhaseSystem.ResetForTests();
        var familiarIo = new ScriptedGameIO();
        var familiarInventory = new InventoryManager();
        var familiarEconomy = new EconomyManager();
        var familiarActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        Combatant ownedPixie = CombatantFactory.CreatePlayerDemon("pixie", 10);
        familiarActor.DemonStock.Add(ownedPixie);
        var familiarParty = new PartyManager(familiarActor);
        Combatant familiarTarget = CombatantFactory.CreateEnemy("pixie");
        NegotiationResult familiar = new NegotiationEngine(
            familiarIo,
            familiarParty,
            familiarInventory,
            familiarEconomy,
            new Random(2)).StartNegotiation(familiarActor, familiarTarget, [familiarTarget]);
        Assert.Equal(NegotiationResult.FamiliarFlee, familiar);

        var refusalIo = new ScriptedGameIO().QueueMenu(0, 0, 0, 1);
        var refusalEconomy = new EconomyManager();
        refusalEconomy.AddMacca(100_000);
        var refusalActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var refusalTarget = CombatantFactory.CreateEnemy("pixie");
        int maccaBeforeRefusal = refusalEconomy.Macca;
        NegotiationResult refusal = new NegotiationEngine(
            refusalIo,
            new PartyManager(refusalActor),
            new InventoryManager(),
            refusalEconomy,
            new Random(3)).StartNegotiation(refusalActor, refusalTarget, [refusalTarget]);
        Assert.Equal(NegotiationResult.Failure, refusal);
        Assert.Equal(maccaBeforeRefusal, refusalEconomy.Macca);

        var successIo = new ScriptedGameIO().QueueMenu(0, 0, 0, 0);
        var successEconomy = new EconomyManager();
        successEconomy.AddMacca(100_000);
        var successActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var successTarget = CombatantFactory.CreateEnemy("pixie");
        NegotiationResult success = new NegotiationEngine(
            successIo,
            new PartyManager(successActor),
            new InventoryManager(),
            successEconomy,
            new Random(3)).StartNegotiation(successActor, successTarget, [successTarget]);
        Assert.Equal(NegotiationResult.Success, success);
        Assert.True(successEconomy.Macca < 100_000);

        var recruitmentIo = new ScriptedGameIO();
        var recruitmentActor = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var recruitmentParty = new PartyManager(recruitmentActor);
        var recruitmentTarget = CombatantFactory.CreateEnemy("pixie");
        var recruitmentEnemies = new List<Combatant> { recruitmentTarget };
        var recruitmentSession = new HashSet<string>();
        var compendium = new CompendiumRegistry(recruitmentIo);
        LegacyRecruitmentResult recruited = LegacyRecruitmentAdapter.Shared.TryRecruit(
            recruitmentActor,
            recruitmentTarget,
            recruitmentSession,
            recruitmentEnemies,
            recruitmentParty,
            compendium);
        Assert.True(recruited.Applied);
        Assert.Empty(recruitmentEnemies);
        Assert.Contains("pixie", recruitmentSession);
        Assert.Contains(recruitmentActor.DemonStock, demon => demon.SourceId.Equals("pixie", StringComparison.OrdinalIgnoreCase));
        Assert.True(compendium.HasEntry("pixie"));
    }

    [Fact]
    public void BattleConductor_StartBattle_RoutesOrdinaryBattleThroughFrameworkEncounterRunner()
    {
        var io = new ScriptedGameIO()
            .QueueMenu(0, 0)
            .QueueKey('x', ConsoleKey.X);
        var economy = new EconomyManager();
        var player = new Combatant("Hero", ClassType.Human)
        {
            Level = 99,
            MaxHP = 999,
            CurrentHP = 999,
            MaxSP = 99,
            CurrentSP = 99
        };
        player.CharacterStats[StatType.St] = 40;
        player.CharacterStats[StatType.Ag] = 40;
        player.CharacterStats[StatType.Lu] = 40;
        var party = new PartyManager(player);

        var enemy = new Combatant("Training Dummy", ClassType.Demon)
        {
            SourceId = "training_dummy",
            Level = 10,
            MaxHP = 1,
            CurrentHP = 1,
            MaxSP = 1,
            CurrentSP = 1
        };
        enemy.CharacterStats[StatType.Vi] = 1;
        enemy.CharacterStats[StatType.Ag] = 1;
        enemy.CharacterStats[StatType.Lu] = 1;

        var conductor = new BattleConductor(
            party,
            [enemy],
            new InventoryManager(),
            economy,
            io,
            new BattleKnowledge(),
            new CompendiumRegistry(io));

        conductor.StartBattle();

        Assert.True(conductor.BattleEnded);
        Assert.True(conductor.PlayerWon);
        Assert.False(conductor.Escaped);
        Assert.True(enemy.IsDead);
        Assert.True(player.LifetimeEarnedExp > 0);
        Assert.True(economy.Macca > 0);
        Assert.Contains("=== ENEMY ENCOUNTER ===", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Appeared: Training Dummy (Lv.10)", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Hero attacks Training Dummy!", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("VICTORY!", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Gained", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Equal(["Attack", "Guard", "Skill", "Item", "Tactics", "Pass"], io.Menus[0].Options);
        Assert.Equal(["Training Dummy (HP: 1/1)", "Back"], io.Menus[1].Options);
        io.AssertConsumed();
    }

    [Fact]
    public void Compendium_PreservesRegistrationSnapshotReplacementAndRecall()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var compendium = new CompendiumRegistry(io);
        Combatant demon = CombatantFactory.CreatePlayerDemon("pixie", 10);
        demon.ExtraSkills.Add("Dia");
        demon.CharacterStats[StatType.Ma] = 7;

        compendium.RegisterDemon(demon);
        demon.Level = 99;
        demon.ExtraSkills.Add("Agi");
        demon.CharacterStats[StatType.Ma] = 40;

        Combatant firstRecall = compendium.GetRecallEntry("PIXIE");
        Assert.NotSame(demon, firstRecall);
        Assert.Equal(10, firstRecall.Level);
        Assert.Equal(["Dia"], firstRecall.ExtraSkills);
        Assert.Equal(7, firstRecall.CharacterStats[StatType.Ma]);

        firstRecall.Level = 1;
        Assert.Equal(10, compendium.GetRecallEntry("pixie").Level);

        compendium.RegisterDemon(demon);
        Combatant replacement = compendium.GetRecallEntry("pixie");
        Assert.Equal(99, replacement.Level);
        Assert.Equal(["Dia", "Agi"], replacement.ExtraSkills);
        Assert.Single(compendium.GetAllRegisteredDemons());
        Assert.True(compendium.CalculateRecallCost("pixie") > 0);
    }
}
