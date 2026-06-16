using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class ResourceManagementServiceTests
{
    [Fact]
    public void InventoryService_AddsRemovesAndReservesItemsWithImmutableSnapshots()
    {
        var service = new InventoryTransitionService();
        var empty = new RuntimeInventorySnapshot();
        ContentId medicine = Id("medicine");

        InventoryTransitionResult added = service.AddItem(empty, medicine, 2, stackLimit: 3);

        Assert.True(added.Applied);
        Assert.Equal(0, empty.GetQuantity(medicine));
        Assert.Equal(2, added.After.GetQuantity(medicine));

        InventoryTransitionResult overStack = service.AddItem(added.After, medicine, 2, stackLimit: 3);
        Assert.False(overStack.Applied);
        Assert.Equal(ResourceTransactionCode.ItemStackExceeded, overStack.Code);
        Assert.Equal(2, overStack.After.GetQuantity(medicine));

        InventoryReservationResult reserved = service.ReserveItem(added.After, medicine, 1);
        Assert.True(reserved.Reserved);
        InventoryTransitionResult rolledBack = reserved.Reservation!.Rollback();
        Assert.True(rolledBack.Applied);
        Assert.Equal(2, rolledBack.After.GetQuantity(medicine));

        RuntimeItemReservation committedReservation = service.ReserveItem(added.After, medicine, 1).Reservation!;
        InventoryTransitionResult committed = committedReservation.Commit();
        Assert.True(committed.Applied);
        Assert.True(committedReservation.IsCommitted);
        Assert.Equal(1, committed.After.GetQuantity(medicine));
    }

    [Fact]
    public void InventoryService_EnforcesUniqueEquipmentOwnershipAndEquippedSaleBlock()
    {
        var service = new InventoryTransitionService();
        ContentId shortsword = Id("shortsword");
        var empty = new RuntimeInventorySnapshot();

        InventoryTransitionResult added = service.AddEquipment(empty, shortsword, EquipmentSlot.Weapon);
        InventoryTransitionResult duplicate = service.AddEquipment(added.After, shortsword, EquipmentSlot.Weapon);
        var equipped = new RuntimeEquipmentSnapshot(
            [new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Weapon, shortsword)]);
        InventoryTransitionResult removeEquipped = service.RemoveEquipment(
            added.After,
            shortsword,
            EquipmentSlot.Weapon,
            equipped);

        Assert.True(added.Applied);
        Assert.Contains(shortsword, added.After.GetEquipmentIds(EquipmentSlot.Weapon));
        Assert.False(duplicate.Applied);
        Assert.Equal(ResourceTransactionCode.EquipmentDuplicate, duplicate.Code);
        Assert.False(removeEquipped.Applied);
        Assert.Equal(ResourceTransactionCode.EquippedItemCannotBeRemoved, removeEquipped.Code);
    }

    [Fact]
    public void EquipmentService_RequiresOwnershipAndSlotCompatibility()
    {
        ContentId shortsword = Id("shortsword");
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentIds:
            [
                new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(EquipmentSlot.Weapon, [shortsword])
            ]);
        var equipment = new RuntimeEquipmentSnapshot();
        var service = new EquipmentTransitionService();

        EquipmentTransitionResult equipped = service.Equip(
            inventory,
            equipment,
            shortsword,
            EquipmentSlot.Weapon,
            EquipmentSlot.Weapon);
        EquipmentTransitionResult wrongSlot = service.Equip(
            inventory,
            equipment,
            shortsword,
            EquipmentSlot.Weapon,
            EquipmentSlot.Accessory);
        EquipmentTransitionResult notOwned = service.Equip(
            inventory,
            equipment,
            Id("longsword"),
            EquipmentSlot.Weapon,
            EquipmentSlot.Weapon);

        Assert.True(equipped.Applied);
        Assert.Equal(shortsword, equipped.After.EquippedItemIds[EquipmentSlot.Weapon]);
        Assert.Equal(ResourceTransactionCode.EquipmentSlotMismatch, wrongSlot.Code);
        Assert.Equal(ResourceTransactionCode.EquipmentNotOwned, notOwned.Code);
    }

    [Fact]
    public void EconomyService_AppliesAtomicMaccaTransactions()
    {
        var service = new EconomyTransactionService();
        var empty = new RuntimeWalletSnapshot(0);

        WalletTransactionResult added = service.AddMacca(empty, 100);
        WalletTransactionResult spent = service.SpendMacca(added.After, 40);
        WalletTransactionResult insufficient = service.SpendMacca(spent.After, 100);
        WalletTransactionResult negative = service.AddMacca(spent.After, -1);

        Assert.Equal(100, added.After.Macca);
        Assert.Equal(60, spent.After.Macca);
        Assert.False(insufficient.Applied);
        Assert.Equal(60, insufficient.After.Macca);
        Assert.Equal(ResourceTransactionCode.InvalidCurrencyAmount, negative.Code);
    }

    [Fact]
    public void ShopService_PreservesLuckPricingAndRollsBackRejectedTransactions()
    {
        var inventoryService = new InventoryTransitionService();
        var shop = new ShopTransactionService(inventoryService, new EconomyTransactionService());
        var inventory = new RuntimeInventorySnapshot();
        var wallet = new RuntimeWalletSnapshot(90);
        var medicine = new RuntimeShopOfferSnapshot(ShopContentKind.Item, Id("medicine"), 100);

        ShopTransactionResult bought = shop.Buy(inventory, wallet, medicine, buyerLuck: 10);
        ShopTransactionResult sold = shop.Sell(bought.AfterInventory, bought.AfterWallet, medicine, sellerLuck: 10);

        Assert.Equal(90, shop.CalculateBuyPrice(100, 10));
        Assert.Equal(60, shop.CalculateSellPrice(100, 10));
        Assert.True(bought.Applied);
        Assert.Equal(0, bought.AfterWallet.Macca);
        Assert.Equal(1, bought.AfterInventory.GetQuantity(Id("medicine")));
        Assert.True(sold.Applied);
        Assert.Equal(60, sold.AfterWallet.Macca);
        Assert.Equal(0, sold.AfterInventory.GetQuantity(Id("medicine")));
    }

    [Fact]
    public void ShopService_RejectsInsufficientCurrencyDuplicateEquipmentAndMissingStock()
    {
        var shop = new ShopTransactionService();
        ContentId sword = Id("shortsword");
        var ownedSword = new RuntimeInventorySnapshot(
            ownedEquipmentIds:
            [
                new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(EquipmentSlot.Weapon, [sword])
            ]);
        var wallet = new RuntimeWalletSnapshot(1_000);
        var swordOffer = new RuntimeShopOfferSnapshot(
            ShopContentKind.Equipment,
            sword,
            100,
            EquipmentSlot.Weapon);
        var emptyStock = swordOffer with { StockAvailable = 0 };
        var priceyItem = new RuntimeShopOfferSnapshot(ShopContentKind.Item, Id("bead"), 2_000);

        ShopTransactionResult duplicate = shop.Buy(ownedSword, wallet, swordOffer, buyerLuck: 0);
        ShopTransactionResult stock = shop.Buy(new RuntimeInventorySnapshot(), wallet, emptyStock, buyerLuck: 0);
        ShopTransactionResult insufficient = shop.Buy(new RuntimeInventorySnapshot(), wallet, priceyItem, buyerLuck: 0);

        Assert.Equal(ResourceTransactionCode.EquipmentDuplicate, duplicate.Code);
        Assert.Equal(1_000, duplicate.AfterWallet.Macca);
        Assert.Equal(ResourceTransactionCode.ShopStockUnavailable, stock.Code);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, insufficient.Code);
    }

    [Fact]
    public void HospitalService_RestoresResourcesAilmentsAndSpendsAtomically()
    {
        var service = new HospitalRestorationService();
        var patient = new RuntimeHospitalPatientSnapshot(
            RuntimeInstanceId.Parse("actor_1"),
            currentHp: 30,
            maxHp: 100,
            currentSp: 10,
            maxSp: 20,
            hasAilment: true,
            hasEncounterPersistence: true);
        var wallet = new RuntimeWalletSnapshot(120);

        HospitalRestorationResult restored = service.Restore(patient, wallet);
        HospitalRestorationResult fullHealth = service.Restore(restored.AfterPatient, restored.AfterWallet);
        HospitalRestorationResult insufficient = service.Restore(patient, new RuntimeWalletSnapshot(1));

        Assert.True(restored.Applied);
        Assert.Equal(120, restored.Cost);
        Assert.Equal(0, restored.AfterWallet.Macca);
        Assert.Equal(restored.AfterPatient.MaxHp, restored.AfterPatient.CurrentHp);
        Assert.Equal(restored.AfterPatient.MaxSp, restored.AfterPatient.CurrentSp);
        Assert.False(restored.AfterPatient.HasAilment);
        Assert.False(restored.AfterPatient.HasEncounterPersistence);
        Assert.Equal(ResourceTransactionCode.NoRestorationNeeded, fullHealth.Code);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, insufficient.Code);
        Assert.Equal(1, insufficient.AfterWallet.Macca);
    }

    private static ContentId Id(string value) => ContentId.Parse(value);
}
