using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
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
public sealed class ShopHospitalPresentationTests
{
    [Fact]
    public void ShopSessionCommand_ReturnsTypedCommandsAndPreservesMenuSurface()
    {
        var economy = new EconomyManager();
        economy.AddMacca(123);
        var io = new ScriptedGameIO().QueueMenu(0, 1, 2, -1);
        var bridge = ShopBridge(io, new InventoryManager(), economy);

        ShopSessionCommandResult buy = bridge.SelectShopSessionCommand("ITEM SHOP", 0);
        ShopSessionCommandResult sell = bridge.SelectShopSessionCommand("ITEM SHOP", 0);
        ShopSessionCommandResult exit = bridge.SelectShopSessionCommand("ITEM SHOP", 0);
        ShopSessionCommandResult back = bridge.SelectShopSessionCommand("ITEM SHOP", 0);

        Assert.Equal(ShopSessionCommandKind.Buy, buy.Kind);
        Assert.Equal(ShopSessionCommandKind.Sell, sell.Kind);
        Assert.Equal(ShopSessionCommandKind.Exit, exit.Kind);
        Assert.Equal(ShopSessionCommandKind.Back, back.Kind);
        Assert.All(io.Menus, menu =>
        {
            Assert.Equal("--- ITEM SHOP ---\nMacca: 123", menu.Header);
            Assert.Equal(["Buy", "Sell", "Exit"], menu.Options);
        });
        io.AssertConsumed();
    }

    [Fact]
    public void BuyPresentation_MapsSelectionInspectionConfirmationAndSuccessfulPurchase()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var inventory = new InventoryManager();
        var economy = new EconomyManager();
        var messenger = new RecordingFieldMessenger();
        var player = PlayerWithLuck(10);
        ShopEntry medicine = Medicine();
        int finalPrice = LegacyInventoryResourceAdapter.Shared.CalculateBuyPrice(medicine, 10);
        economy.AddMacca(finalPrice);
        var io = new ScriptedGameIO().QueueMenu(0, 0);
        var bridge = ShopBridge(io, inventory, economy, messenger);
        var engine = new ShopEngine(inventory, economy, messenger);

        ShopOfferSelectionResult selected = bridge.SelectBuyOffer(player, ShopType.Item, [medicine], 0);
        ShopInspectionPresentationResult inspection = bridge.ShowItemInspectionDetailed(medicine, player, isBuying: true);
        ShopTransactionConfirmationResult confirmation =
            bridge.ConfirmTransactionDetailed(medicine.Name, finalPrice, isBuying: true);
        ShopTransactionPresentationResult purchase = engine.ExecutePurchaseDetailed(medicine, player);

        Assert.Equal(ShopSelectionResultKind.Selected, selected.Kind);
        Assert.Equal($"{medicine.Name,-18} {medicine.BasePrice,5} M", selected.Offer?.Label);
        Assert.Equal(finalPrice, inspection.Price);
        Assert.Contains($"Price: {finalPrice} M (Base: {medicine.BasePrice})", messenger.Messages);
        Assert.Equal(ShopTransactionConfirmationKind.Confirmed, confirmation.Kind);
        Assert.True(purchase.LegacySuccess);
        Assert.Equal(ResourceTransactionCode.Applied, purchase.Transaction.Code);
        Assert.Equal("\nBought!", purchase.Message);
        Assert.Equal(finalPrice, purchase.DisplayedPrice);
        Assert.Equal(1, inventory.GetQuantity(medicine.Id));
        Assert.Equal(0, economy.Macca);
        Assert.Contains("\nBought!", messenger.Messages);
        io.AssertConsumed();
    }

    [Fact]
    public void PurchasePresentation_PreservesInsufficientFundsAndDuplicateEquipmentRejection()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var player = PlayerWithLuck(0);
        ShopEntry medicine = Medicine();
        ShopEntry sword = Sword();

        var poorInventory = new InventoryManager();
        var poorEconomy = new EconomyManager();
        var poorMessenger = new RecordingFieldMessenger();
        var poorEngine = new ShopEngine(poorInventory, poorEconomy, poorMessenger);

        ShopTransactionPresentationResult insufficient = poorEngine.ExecutePurchaseDetailed(medicine, player);

        Assert.False(insufficient.LegacySuccess);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, insufficient.Transaction.Code);
        Assert.Equal("\nNot enough Macca!", insufficient.Message);
        Assert.Equal(0, poorInventory.GetQuantity(medicine.Id));
        Assert.Equal(["\nNot enough Macca!"], poorMessenger.Messages);

        var duplicateInventory = new InventoryManager();
        duplicateInventory.AddEquipment(sword.Id, ShopCategory.Weapon);
        var duplicateEconomy = new EconomyManager();
        duplicateEconomy.AddMacca(1_000);
        var duplicateMessenger = new RecordingFieldMessenger();
        var duplicateEngine = new ShopEngine(duplicateInventory, duplicateEconomy, duplicateMessenger);

        ShopTransactionPresentationResult duplicate = duplicateEngine.ExecutePurchaseDetailed(sword, player);

        Assert.False(duplicate.LegacySuccess);
        Assert.Equal(ResourceTransactionCode.EquipmentDuplicate, duplicate.Transaction.Code);
        Assert.Equal(1_000, duplicateEconomy.Macca);
        Assert.Equal([sword.Id], duplicateInventory.OwnedWeapons);
        Assert.NotNull(duplicate.Message);
        Assert.Equal(duplicate.Message, Assert.Single(duplicateMessenger.Messages));
    }

    [Fact]
    public void SellPresentation_PreservesLabelsEquippedBlockingSaleAndFallbackPrice()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var player = PlayerWithLuck(10);
        var inventory = new InventoryManager();
        var economy = new EconomyManager();
        var messenger = new RecordingFieldMessenger();
        ItemData medicineData = Database.Items["101"];
        ShopEntry medicine = Medicine();
        inventory.AddItem(medicine.Id, 1);
        var bridge = ShopBridge(new ScriptedGameIO().QueueMenu(0), inventory, economy, messenger);

        ShopOfferSelectionResult selected = bridge.SelectSellOffer(
            player,
            ShopType.Item,
            ShopCategory.Item,
            [medicineData],
            0);
        var engine = new ShopEngine(inventory, economy, messenger);
        ShopTransactionPresentationResult sale =
            engine.ExecuteSaleDetailed(selected.Offer!.ContentId, ShopCategory.Item, player);

        Assert.Equal(ShopSelectionResultKind.Selected, selected.Kind);
        Assert.Equal($"{medicineData.Name,-15} ({sale.DisplayedPrice} M)", selected.Offer.Label);
        Assert.True(sale.LegacySuccess);
        Assert.Equal("\nSold!", sale.Message);
        Assert.Equal(0, inventory.GetQuantity(medicine.Id));
        Assert.True(economy.Macca > 0);
        Assert.Contains("\nSold!", messenger.Messages);

        var equippedInventory = new InventoryManager();
        equippedInventory.AddEquipment("1", ShopCategory.Weapon);
        player.EquippedWeapon = Database.Weapons["1"];
        var equippedIo = new ScriptedGameIO().QueueMenu(-1);
        var equippedBridge = ShopBridge(equippedIo, equippedInventory, new EconomyManager());

        ShopOfferSelectionResult equippedBack = equippedBridge.SelectSellOffer(
            player,
            ShopType.Weapon,
            ShopCategory.Weapon,
            [Database.Weapons["1"]],
            0);

        Assert.Equal(ShopSelectionResultKind.Back, equippedBack.Kind);
        Assert.Equal([$"{Database.Weapons["1"].Name,-15} [E] ({equippedBridgePrice(player)} M)"], equippedIo.Menus[0].Options);
        Assert.Equal([true], equippedIo.Menus[0].DisabledOptions);

        int fallbackPrice = engine.CalculateSellPrice("missing", ShopCategory.Item, player);
        ShopTransactionPresentationResult missing = engine.ExecuteSaleDetailed("missing", ShopCategory.Item, player);
        Assert.Equal(60, fallbackPrice);
        Assert.False(missing.LegacySuccess);
        Assert.Equal(60, missing.DisplayedPrice);
        Assert.Null(missing.Message);
    }

    [Fact]
    public void HospitalSelection_ReturnsTypedRowsAndPreservesHealthyDisabledLeave()
    {
        var player = new Combatant("Hero", ClassType.Operator)
        {
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50
        };
        var ally = new Combatant("Ally")
        {
            MaxHP = 80,
            CurrentHP = 20,
            MaxSP = 20,
            CurrentSP = 10
        };
        var party = new PartyManager(player);
        party.AddMember(ally);
        var io = new ScriptedGameIO().QueueMenu(0, -1);
        var bridge = new ServiceUIBridge(io, new FieldUIState(), new EconomyManager(), party);

        HospitalPatientSelectionResult selected = bridge.SelectHospitalPatientResult(player);
        HospitalPatientSelectionResult back = bridge.SelectHospitalPatientResult(player);

        Assert.Equal(HospitalSelectionResultKind.Selected, selected.Kind);
        Assert.Same(ally, selected.Patient);
        Assert.Equal(110, selected.Presentation?.Cost);
        Assert.Equal(HospitalSelectionResultKind.Back, back.Kind);
        Assert.Equal("=== HOSPITAL / CLOCK ===\nCurrent Macca: 0\nSelect a member to treat:", io.Menus[0].Header);
        Assert.Equal(
            [
                $"{ally.Name,-15} | HP: {ally.CurrentHP,3}/{ally.MaxHP,3} SP: {ally.CurrentSP,3}/{ally.MaxSP,3} | 110 M",
                $"{player.Name,-15} | HP: {player.CurrentHP,3}/{player.MaxHP,3} SP: {player.CurrentSP,3}/{player.MaxSP,3} | [HEALTHY]",
                "Leave"
            ],
            io.Menus[0].Options);
        Assert.Equal([false, true, false], io.Menus[0].DisabledOptions);
        io.AssertConsumed();
    }

    [Fact]
    public void HospitalTreatment_ReturnsTypedPresentationAndPreservesEngineQuirks()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var inventory = new InventoryManager();
        var economy = new EconomyManager();
        var patient = new Combatant("Hero")
        {
            MaxHP = 100,
            CurrentHP = 40,
            MaxSP = 30,
            CurrentSP = 10
        };
        patient.InflictAilment(new AilmentData { Name = "Poison", CureKeyword = "Poison" });
        patient.AddBuff("PhysAtk", 3);
        var engine = FieldEngine(inventory, economy);
        economy.AddMacca(engine.CalculateRestorationCost(patient));

        HospitalTreatmentPresentationResult restored = engine.TryRestoreCombatantDetailed(patient);

        Assert.True(restored.LegacySuccess);
        Assert.Equal(ResourceTransactionCode.Applied, restored.Transaction.Code);
        Assert.Equal("Hero has been fully restored!", restored.Message);
        Assert.Equal(ConsoleColor.Green, restored.Color);
        Assert.Equal(800, restored.Delay);
        Assert.Equal(patient.MaxHP, patient.CurrentHP);
        Assert.Equal(patient.MaxSP, patient.CurrentSP);
        Assert.Null(patient.CurrentAilment);
        Assert.Empty(patient.Buffs);
        Assert.Equal(0, economy.Macca);

        var insufficient = new Combatant("Broke")
        {
            MaxHP = 100,
            CurrentHP = 1,
            MaxSP = 30,
            CurrentSP = 1
        };
        HospitalTreatmentPresentationResult failed =
            FieldEngine(new InventoryManager(), new EconomyManager()).TryRestoreCombatantDetailed(insufficient);

        Assert.False(failed.LegacySuccess);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, failed.Transaction.Code);
        Assert.Equal("Could not complete treatment.", failed.Message);
        Assert.Equal(ConsoleColor.Red, failed.Color);
        Assert.Equal(1000, failed.Delay);

        var ailmentOnlyEconomy = new EconomyManager();
        var ailmentOnly = new Combatant("Poisoned")
        {
            MaxHP = 50,
            CurrentHP = 50,
            MaxSP = 10,
            CurrentSP = 10
        };
        ailmentOnly.InflictAilment(new AilmentData { Name = "Poison", CureKeyword = "Poison" });

        HospitalTreatmentPresentationResult zeroCost =
            FieldEngine(new InventoryManager(), ailmentOnlyEconomy).TryRestoreCombatantDetailed(ailmentOnly);

        Assert.True(zeroCost.LegacySuccess);
        Assert.Equal(0, zeroCost.Cost);
        Assert.Null(ailmentOnly.CurrentAilment);
        Assert.Equal(0, ailmentOnlyEconomy.Macca);
    }

    private static ShopUIBridge ShopBridge(
        ScriptedGameIO io,
        InventoryManager inventory,
        EconomyManager economy,
        IFieldMessenger? messenger = null) =>
        new(
            io,
            messenger ?? new RecordingFieldMessenger(),
            new ShopEngine(inventory, economy, messenger ?? new RecordingFieldMessenger()),
            economy,
            inventory);

    private static FieldServiceEngine FieldEngine(InventoryManager inventory, EconomyManager economy) =>
        new(
            new RecordingFieldMessenger(),
            new ScriptedGameIO(),
            economy,
            inventory,
            new PartyManager(new Combatant("Hero")),
            new DungeonState());

    private static Combatant PlayerWithLuck(int luck)
    {
        var player = new Combatant("Hero");
        player.CharacterStats[StatType.Lu] = luck;
        return player;
    }

    private static ShopEntry Medicine() =>
        Assert.Single(Database.ShopInventory, entry => entry.Id == "101" && entry.Category == ShopCategory.Item);

    private static ShopEntry Sword() =>
        Assert.Single(Database.ShopInventory, entry => entry.Id == "1" && entry.Category == ShopCategory.Weapon);

    private static int equippedBridgePrice(Combatant player)
    {
        ShopEntry sword = Sword();
        return LegacyInventoryResourceAdapter.Shared.CalculateSellPrice(sword.BasePrice, player.GetStat(StatType.Lu));
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
