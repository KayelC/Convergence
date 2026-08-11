using Convergence.Content;
using Convergence.Catalog;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class ResourceManagementServiceTests
{
    private static readonly ContentId CreditsCurrency = Id("credits");

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
    public void InventoryService_RejectsQuantityOverflowWithTypedUnchangedResult()
    {
        var service = new InventoryTransitionService();
        ContentId medicine = Id("medicine");
        var maximum = new RuntimeInventorySnapshot(
            [new KeyValuePair<ContentId, int>(medicine, int.MaxValue)]);

        InventoryTransitionResult result = service.AddItem(maximum, medicine, 1);

        Assert.False(result.Applied);
        Assert.Equal(ResourceTransactionCode.NumericOverflow, result.Code);
        Assert.Same(maximum, result.Before);
        Assert.Same(maximum, result.After);
        Assert.Equal(int.MaxValue, result.After.GetQuantity(medicine));
    }

    [Fact]
    public void InventoryService_AllowsDuplicateDefinitionsButRejectsDuplicateInstancesAndEquippedRemoval()
    {
        var service = new InventoryTransitionService();
        ContentId shortsword = Id("shortsword");
        var first = new RuntimeEquipmentInstanceSnapshot(Instance("sword-001"), shortsword);
        var second = new RuntimeEquipmentInstanceSnapshot(Instance("sword-002"), shortsword);
        var empty = new RuntimeInventorySnapshot();

        InventoryTransitionResult added = service.AddEquipment(empty, first, StandardEquipmentSlotIds.Weapon);
        InventoryTransitionResult secondCopy = service.AddEquipment(
            added.After,
            second,
            StandardEquipmentSlotIds.Weapon);
        InventoryTransitionResult duplicate = service.AddEquipment(
            secondCopy.After,
            first,
            StandardEquipmentSlotIds.Weapon);
        var equipped = new RuntimeEquipmentSnapshot(
            [new KeyValuePair<ContentId, RuntimeInstanceId>(StandardEquipmentSlotIds.Weapon, first.InstanceId)]);
        InventoryTransitionResult removeEquipped = service.RemoveEquipment(
            secondCopy.After,
            first.InstanceId,
            StandardEquipmentSlotIds.Weapon,
            [equipped]);

        Assert.True(added.Applied);
        Assert.True(secondCopy.Applied);
        Assert.Equal(2, secondCopy.After.GetEquipmentInstances(StandardEquipmentSlotIds.Weapon).Count);
        Assert.All(
            secondCopy.After.GetEquipmentInstances(StandardEquipmentSlotIds.Weapon),
            instance => Assert.Equal(shortsword, instance.DefinitionId));
        Assert.False(duplicate.Applied);
        Assert.Equal(ResourceTransactionCode.EquipmentDuplicate, duplicate.Code);
        Assert.Same(secondCopy.After, duplicate.After);
        Assert.False(removeEquipped.Applied);
        Assert.Equal(ResourceTransactionCode.EquippedItemCannotBeRemoved, removeEquipped.Code);
        Assert.Same(secondCopy.After, removeEquipped.After);
    }

    [Fact]
    public void EquipmentService_RequiresOwnershipAndSlotCompatibility()
    {
        ContentId shortsword = Id("shortsword");
        RuntimeInstanceId shortswordInstanceId = Instance("sword-001");
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Weapon,
                    [new RuntimeEquipmentInstanceSnapshot(shortswordInstanceId, shortsword)])
            ]);
        var equipment = new RuntimeEquipmentSnapshot();
        var service = new EquipmentTransitionService();

        EquipmentTransitionResult equipped = service.Equip(
            inventory,
            equipment,
            shortswordInstanceId,
            StandardEquipmentSlotIds.Weapon,
            StandardEquipmentSlotIds.Weapon,
            []);
        EquipmentTransitionResult wrongSlot = service.Equip(
            inventory,
            equipment,
            shortswordInstanceId,
            StandardEquipmentSlotIds.Weapon,
            StandardEquipmentSlotIds.Accessory,
            []);
        EquipmentTransitionResult notOwned = service.Equip(
            inventory,
            equipment,
            Instance("longsword-001"),
            StandardEquipmentSlotIds.Weapon,
            StandardEquipmentSlotIds.Weapon,
            []);
        EquipmentTransitionResult alreadyEquipped = service.Equip(
            inventory,
            equipped.After,
            shortswordInstanceId,
            StandardEquipmentSlotIds.Weapon,
            StandardEquipmentSlotIds.Weapon,
            []);

        Assert.True(equipped.Applied);
        Assert.Equal(
            shortswordInstanceId,
            equipped.After.EquippedInstanceIds[StandardEquipmentSlotIds.Weapon]);
        Assert.Equal(ResourceTransactionCode.EquipmentSlotMismatch, wrongSlot.Code);
        Assert.Equal(ResourceTransactionCode.EquipmentNotOwned, notOwned.Code);
        Assert.Equal(ResourceTransactionCode.EquipmentAlreadyEquipped, alreadyEquipped.Code);
        Assert.Equal(equipped.After, alreadyEquipped.After);
    }

    [Fact]
    public void EquipmentProfileResolver_ResolvesBasicAttackAndAccessoryStatModifiers()
    {
        ContentId sword = Id("shortsword");
        ContentId charm = Id("focus_charm");
        ContentId magic = Id("magic");
        RuntimeInstanceId swordInstanceId = Instance("sword-001");
        RuntimeInstanceId charmInstanceId = Instance("charm-001");
        var repository = new TestEquipmentRepository(
            Weapon(sword, power: 12, accuracy: 95),
            Accessory(
                charm,
                new StatModifierDefinition(magic, 1),
                new StatModifierDefinition(magic, 2)));
        var equipment = new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<ContentId, RuntimeInstanceId>(StandardEquipmentSlotIds.Weapon, swordInstanceId),
                new KeyValuePair<ContentId, RuntimeInstanceId>(StandardEquipmentSlotIds.Accessory, charmInstanceId)
            ]);
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Weapon,
                    [new RuntimeEquipmentInstanceSnapshot(swordInstanceId, sword)]),
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Accessory,
                    [new RuntimeEquipmentInstanceSnapshot(charmInstanceId, charm)])
            ]);

        RuntimeEquipmentProfile profile = new RuntimeEquipmentProfileResolver().Resolve(
            inventory,
            equipment,
            repository);
        var statPolicy = new StandardStatResolutionPolicy();
        StatResolutionResult resolvedMagic = statPolicy.Resolve(new StatResolutionRequest(
            RuntimeStatSourceKind.Actor,
            magic,
            [new KeyValuePair<ContentId, decimal>(magic, 4)],
            equipmentStatModifiers: profile.StatModifiers));

        Assert.Empty(profile.Diagnostics);
        Assert.NotNull(profile.BasicAttack);
        Assert.Equal(sword, profile.BasicAttack!.EquipmentId);
        Assert.Equal(DamageElement.Physical, profile.BasicAttack.BasicAttack.Element);
        Assert.Equal(12, profile.BasicAttack.BasicAttack.Power);
        Assert.Equal(95, profile.BasicAttack.BasicAttack.Accuracy);
        Assert.Equal(3, profile.StatModifiers[magic]);
        Assert.Equal(7, resolvedMagic.FinalValue);
    }

    [Fact]
    public void Order7R4_EquipmentProfileResolvesGrantedSkillsAndCombatContributionsTogether()
    {
        ContentId sword = Id("shortsword");
        ContentId armor = Id("padded_armor");
        ContentId boots = Id("trail_boots");
        ContentId charm = Id("focus_charm");
        ContentId armorSkill = Id("armor_skill");
        ContentId bootsSkill = Id("boots_skill");
        RuntimeInstanceId swordInstanceId = Instance("sword-001");
        RuntimeInstanceId armorInstanceId = Instance("armor-001");
        RuntimeInstanceId bootsInstanceId = Instance("boots-001");
        RuntimeInstanceId charmInstanceId = Instance("charm-001");
        var repository = new TestEquipmentRepository(
            Weapon(sword, power: 12, accuracy: 95),
            Armor(armor, defense: 6, evasion: 1, armorSkill),
            Boots(boots, evasion: 4, armorSkill, bootsSkill),
            Accessory(charm, new StatModifierDefinition(StandardProgressionIds.Magic, 3)));
        var equipment = new RuntimeEquipmentSnapshot(
        [
            new(StandardEquipmentSlotIds.Weapon, swordInstanceId),
            new(StandardEquipmentSlotIds.Armor, armorInstanceId),
            new(StandardEquipmentSlotIds.Boots, bootsInstanceId),
            new(StandardEquipmentSlotIds.Accessory, charmInstanceId)
        ]);
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                Owned(StandardEquipmentSlotIds.Weapon, swordInstanceId, sword),
                Owned(StandardEquipmentSlotIds.Armor, armorInstanceId, armor),
                Owned(StandardEquipmentSlotIds.Boots, bootsInstanceId, boots),
                Owned(StandardEquipmentSlotIds.Accessory, charmInstanceId, charm)
            ]);

        RuntimeEquipmentProfile profile = new RuntimeEquipmentProfileResolver().Resolve(
            inventory,
            equipment,
            repository);

        Assert.Empty(profile.Diagnostics);
        Assert.Equal(sword, profile.BasicAttack?.EquipmentId);
        Assert.Equal(12, profile.BasicAttack?.BasicAttack.Power);
        Assert.Equal(3, profile.StatModifiers[StandardProgressionIds.Magic]);
        Assert.Equal(6, profile.StatModifiers[StandardProgressionIds.Defense]);
        Assert.Equal(5, profile.StatModifiers[StandardProgressionIds.Evasion]);
        Assert.Equal([armorSkill, bootsSkill], profile.GrantedSkillIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContentId>)profile.GrantedSkillIds).Add(Id("unexpected")));
    }

    [Fact]
    public void EquipmentProfileResolver_ReportsMissingAndSlotMismatchedDefinitions()
    {
        ContentId sword = Id("shortsword");
        ContentId missing = Id("missing_blade");
        RuntimeInstanceId missingInstanceId = Instance("missing-001");
        RuntimeInstanceId swordInstanceId = Instance("sword-001");
        var repository = new TestEquipmentRepository(Weapon(sword, power: 8, accuracy: 90));
        var equipment = new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<ContentId, RuntimeInstanceId>(StandardEquipmentSlotIds.Weapon, missingInstanceId),
                new KeyValuePair<ContentId, RuntimeInstanceId>(StandardEquipmentSlotIds.Accessory, swordInstanceId)
            ]);
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Weapon,
                    [
                        new RuntimeEquipmentInstanceSnapshot(missingInstanceId, missing),
                        new RuntimeEquipmentInstanceSnapshot(swordInstanceId, sword)
                    ])
            ]);

        RuntimeEquipmentProfile profile = new RuntimeEquipmentProfileResolver().Resolve(
            inventory,
            equipment,
            repository);

        Assert.Null(profile.BasicAttack);
        Assert.Empty(profile.EquippedDefinitions);
        Assert.Contains(profile.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeEquipmentProfileDiagnosticCode.MissingEquipmentDefinition &&
            diagnostic.EquipmentId == missing);
        Assert.Contains(profile.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeEquipmentProfileDiagnosticCode.SlotProfileMismatch &&
            diagnostic.EquipmentId == sword &&
            diagnostic.SlotId == StandardEquipmentSlotIds.Accessory);
    }

    [Fact]
    public void EquipmentSnapshots_RejectInvalidInstanceIdentifiersAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeEquipmentInstanceSnapshot(
            default,
            Id("shortsword")));
        Assert.Throws<ArgumentException>(() => new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<ContentId, RuntimeInstanceId>(StandardEquipmentSlotIds.Weapon, default)
        ]));
    }

    [Fact]
    public void EconomyService_AppliesAtomicCreditTransactions()
    {
        var service = new EconomyTransactionService();
        RuntimeCurrencyLedgerSnapshot empty = Ledger(0);

        CurrencyTransactionResult added = service.Credit(empty, CreditsCurrency, 100);
        CurrencyTransactionResult spent = service.Debit(added.After, CreditsCurrency, 40);
        CurrencyTransactionResult insufficient = service.Debit(spent.After, CreditsCurrency, 100);
        CurrencyTransactionResult negative = service.Credit(spent.After, CreditsCurrency, -1);
        RuntimeCurrencyLedgerSnapshot maximum = Ledger(int.MaxValue);
        CurrencyTransactionResult overflow = service.Credit(maximum, CreditsCurrency, 1);

        Assert.Equal(100, Balance(added.After));
        Assert.Equal(60, Balance(spent.After));
        Assert.False(insufficient.Applied);
        Assert.Same(spent.After, insufficient.Before);
        Assert.Same(insufficient.Before, insufficient.After);
        Assert.Equal(60, Balance(insufficient.After));
        Assert.Equal(ResourceTransactionCode.InvalidCurrencyAmount, negative.Code);
        Assert.Same(spent.After, negative.After);
        Assert.Equal(ResourceTransactionCode.NumericOverflow, overflow.Code);
        Assert.Same(maximum, overflow.Before);
        Assert.Same(maximum, overflow.After);
        Assert.Contains(overflow.Diagnostics, diagnostic =>
            diagnostic.Code == ResourceTransactionCode.NumericOverflow &&
            diagnostic.Message.Contains("integer range", StringComparison.Ordinal));
    }

    [Fact]
    public void CurrencyLedger_RequiresAnUnambiguousSingleCurrency()
    {
        RuntimeCurrencyLedgerSnapshot single = Ledger(75);
        var empty = new RuntimeCurrencyLedgerSnapshot();
        var multiple = new RuntimeCurrencyLedgerSnapshot(
        [
            new KeyValuePair<ContentId, int>(CreditsCurrency, 75),
            new KeyValuePair<ContentId, int>(Id("tokens"), 3)
        ]);

        RuntimeCurrencyBalanceSnapshot selected = single.GetSingleCurrency();
        RuntimeCurrencyLedgerException emptyFailure =
            Assert.Throws<RuntimeCurrencyLedgerException>(() => empty.GetSingleCurrency());
        RuntimeCurrencyLedgerException multipleFailure =
            Assert.Throws<RuntimeCurrencyLedgerException>(() => multiple.GetSingleCurrency());

        Assert.Equal(CreditsCurrency, selected.CurrencyId);
        Assert.Equal(75, selected.Balance);
        Assert.Equal(ResourceTransactionCode.EmptyCurrencyLedger, emptyFailure.Diagnostic.Code);
        Assert.Equal(ResourceTransactionCode.AmbiguousCurrencyLedger, multipleFailure.Diagnostic.Code);
    }

    [Fact]
    public void CurrencyBalanceSnapshot_ProtectsItsValidatedImmutableShape()
    {
        RuntimeCurrencyLedgerException invalidId =
            Assert.Throws<RuntimeCurrencyLedgerException>(() =>
                new RuntimeCurrencyBalanceSnapshot(default, 1));
        RuntimeCurrencyLedgerException negative =
            Assert.Throws<RuntimeCurrencyLedgerException>(() =>
                new RuntimeCurrencyBalanceSnapshot(CreditsCurrency, -1));

        Assert.Equal(ResourceTransactionCode.InvalidCurrencyId, invalidId.Diagnostic.Code);
        Assert.Equal(ResourceTransactionCode.NegativeCurrencyBalance, negative.Diagnostic.Code);
        Assert.False(typeof(RuntimeCurrencyBalanceSnapshot)
            .GetProperty(nameof(RuntimeCurrencyBalanceSnapshot.CurrencyId))!
            .CanWrite);
        Assert.False(typeof(RuntimeCurrencyBalanceSnapshot)
            .GetProperty(nameof(RuntimeCurrencyBalanceSnapshot.Balance))!
            .CanWrite);
    }

    [Fact]
    public void CurrencyLedger_RejectsDuplicateAndNegativeBalancesWithTypedDiagnostics()
    {
        RuntimeCurrencyLedgerException duplicate = Assert.Throws<RuntimeCurrencyLedgerException>(() =>
            new RuntimeCurrencyLedgerSnapshot(
            [
                new KeyValuePair<ContentId, int>(CreditsCurrency, 1),
                new KeyValuePair<ContentId, int>(CreditsCurrency, 2)
            ]));
        RuntimeCurrencyLedgerException negative = Assert.Throws<RuntimeCurrencyLedgerException>(() =>
            RuntimeCurrencyLedgerSnapshot.Single(CreditsCurrency, -1));

        Assert.Equal(ResourceTransactionCode.DuplicateCurrencyId, duplicate.Diagnostic.Code);
        Assert.Equal(CreditsCurrency, duplicate.Diagnostic.CurrencyId);
        Assert.Equal(ResourceTransactionCode.NegativeCurrencyBalance, negative.Diagnostic.Code);
        Assert.Equal(CreditsCurrency, negative.Diagnostic.CurrencyId);
    }

    [Fact]
    public void EconomyService_RejectsMissingCurrencyWithoutChangingOtherBalances()
    {
        var service = new EconomyTransactionService();
        RuntimeCurrencyLedgerSnapshot ledger = Ledger(50);
        ContentId missing = Id("tokens");

        CurrencyTransactionResult result = service.Debit(ledger, missing, 1);

        Assert.False(result.Applied);
        Assert.Equal(ResourceTransactionCode.CurrencyNotFound, result.Code);
        Assert.Equal(missing, result.CurrencyId);
        Assert.Same(ledger, result.Before);
        Assert.Same(ledger, result.After);
        ResourceTransactionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ResourceTransactionCode.CurrencyNotFound, diagnostic.Code);
        Assert.Equal(missing, diagnostic.CurrencyId);
        Assert.Equal(50, Balance(result.After));
    }

    [Fact]
    public void EconomyService_ChangesOnlyTheExplicitlyNamedCurrency()
    {
        ContentId tokens = Id("tokens");
        var service = new EconomyTransactionService();
        var ledger = new RuntimeCurrencyLedgerSnapshot(
        [
            new KeyValuePair<ContentId, int>(CreditsCurrency, 50),
            new KeyValuePair<ContentId, int>(tokens, 3)
        ]);

        CurrencyTransactionResult result = service.Debit(ledger, CreditsCurrency, 10);

        Assert.True(result.Applied);
        Assert.Equal(CreditsCurrency, result.CurrencyId);
        Assert.Equal(40, result.After.GetRequiredBalance(CreditsCurrency));
        Assert.Equal(3, result.After.GetRequiredBalance(tokens));
        Assert.Equal(50, ledger.GetRequiredBalance(CreditsCurrency));
        Assert.Equal(3, ledger.GetRequiredBalance(tokens));
    }

    [Fact]
    public void EconomyContract_ExposesOnlyExplicitCurrencyTransactions()
    {
        Assert.All(typeof(IEconomyTransactionService).GetMethods(), method =>
        {
            Assert.Contains(method.Name, new[] { nameof(IEconomyTransactionService.Credit), nameof(IEconomyTransactionService.Debit) });
            Assert.Equal(3, method.GetParameters().Length);
            Assert.Equal(typeof(RuntimeCurrencyLedgerSnapshot), method.GetParameters()[0].ParameterType);
            Assert.Equal(typeof(ContentId), method.GetParameters()[1].ParameterType);
            Assert.Equal(typeof(int), method.GetParameters()[2].ParameterType);
        });
    }

    [Fact]
    public void ShopService_PreservesLuckPricingAndRollsBackRejectedTransactions()
    {
        var inventoryService = new InventoryTransitionService();
        var shop = new ShopTransactionService(inventoryService, new EconomyTransactionService());
        var inventory = new RuntimeInventorySnapshot();
        RuntimeCurrencyLedgerSnapshot wallet = Ledger(90);
        var medicine = new RuntimeShopOfferSnapshot(ShopContentKind.Item, Id("medicine"), 100);

        ShopTransactionResult bought = shop.Buy(
            inventory,
            wallet,
            CreditsCurrency,
            medicine,
            buyerLuck: 10,
            purchasedEquipmentInstanceId: null);
        ShopTransactionResult sold = shop.Sell(
            bought.AfterInventory,
            bought.AfterCurrencyLedger,
            CreditsCurrency,
            medicine,
            sellerLuck: 10,
            soldEquipmentInstanceId: null,
            actorEquipment: []);

        Assert.Equal(90, shop.CalculateBuyPrice(100, 10));
        Assert.Equal(60, shop.CalculateSellPrice(100, 10));
        Assert.True(bought.Applied);
        Assert.Equal(0, Balance(bought.AfterCurrencyLedger));
        Assert.Equal(1, bought.AfterInventory.GetQuantity(Id("medicine")));
        Assert.True(sold.Applied);
        Assert.Equal(60, Balance(sold.AfterCurrencyLedger));
        Assert.Equal(0, sold.AfterInventory.GetQuantity(Id("medicine")));
    }

    [Fact]
    public void RuntimeShopOfferSnapshot_RejectsNegativeBasePriceAtConstructionAndCloneBoundaries()
    {
        ArgumentOutOfRangeException construction = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeShopOfferSnapshot(ShopContentKind.Item, Id("invalid"), -1));
        var valid = new RuntimeShopOfferSnapshot(ShopContentKind.Item, Id("medicine"), 100);

        ArgumentOutOfRangeException cloning = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = valid with { BasePrice = -1 });

        Assert.Equal("BasePrice", construction.ParamName);
        Assert.Equal("BasePrice", cloning.ParamName);
        Assert.Equal(100, valid.BasePrice);

        var (kind, contentId, basePrice, slot, stackLimit, stock) = valid;
        Assert.Equal(ShopContentKind.Item, kind);
        Assert.Equal(Id("medicine"), contentId);
        Assert.Equal(100, basePrice);
        Assert.Null(slot);
        Assert.Null(stackLimit);
        Assert.Null(stock);
    }

    [Fact]
    public void ShopService_RejectsInvalidOrOverflowPricingWithoutMutation()
    {
        var shop = new ShopTransactionService();
        ContentId medicine = Id("medicine");
        var inventory = new RuntimeInventorySnapshot(
            [new KeyValuePair<ContentId, int>(medicine, 1)]);
        RuntimeCurrencyLedgerSnapshot wallet = Ledger(100);
        var ordinaryOffer = new RuntimeShopOfferSnapshot(ShopContentKind.Item, medicine, 100);
        var extremeOffer = new RuntimeShopOfferSnapshot(ShopContentKind.Item, medicine, int.MaxValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => shop.CalculateBuyPrice(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => shop.CalculateBuyPrice(100, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => shop.CalculateSellPrice(100, -1));
        Assert.Throws<OverflowException>(() => shop.CalculateSellPrice(int.MaxValue, int.MaxValue));
        Assert.Equal(13, shop.CalculateBuyPrice(20, 35));
        Assert.Equal(29, shop.CalculateSellPrice(50, 8));
        Assert.Equal(1_073_741_823, shop.CalculateBuyPrice(int.MaxValue, int.MaxValue));

        ShopTransactionResult invalidBuy = shop.Buy(
            inventory,
            wallet,
            CreditsCurrency,
            ordinaryOffer,
            buyerLuck: -1,
            purchasedEquipmentInstanceId: null);
        ShopTransactionResult overflowingSell = shop.Sell(
            inventory,
            wallet,
            CreditsCurrency,
            extremeOffer,
            sellerLuck: int.MaxValue,
            soldEquipmentInstanceId: null,
            actorEquipment: []);

        AssertPricingRejectedWithoutMutation(invalidBuy, inventory, wallet, "cannot be negative");
        AssertPricingRejectedWithoutMutation(overflowingSell, inventory, wallet, "integer range");
    }

    [Fact]
    public void ShopService_AllowsDuplicateDefinitionsAndRejectsDuplicateInstanceInsufficientCurrencyAndMissingStock()
    {
        var shop = new ShopTransactionService();
        ContentId sword = Id("shortsword");
        RuntimeInstanceId ownedInstanceId = Instance("sword-001");
        var ownedSword = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Weapon,
                    [new RuntimeEquipmentInstanceSnapshot(ownedInstanceId, sword)])
            ]);
        RuntimeCurrencyLedgerSnapshot wallet = Ledger(1_000);
        var swordOffer = new RuntimeShopOfferSnapshot(
            ShopContentKind.Equipment,
            sword,
            100,
            StandardEquipmentSlotIds.Weapon);
        var emptyStock = swordOffer with { StockAvailable = 0 };
        var priceyItem = new RuntimeShopOfferSnapshot(ShopContentKind.Item, Id("bead"), 2_000);

        ShopTransactionResult anotherCopy = shop.Buy(
            ownedSword,
            wallet,
            CreditsCurrency,
            swordOffer,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: Instance("sword-002"));
        ShopTransactionResult duplicate = shop.Buy(
            ownedSword,
            wallet,
            CreditsCurrency,
            swordOffer,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: ownedInstanceId);
        ShopTransactionResult stock = shop.Buy(
            new RuntimeInventorySnapshot(),
            wallet,
            CreditsCurrency,
            emptyStock,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: Instance("sword-003"));
        ShopTransactionResult insufficient = shop.Buy(
            new RuntimeInventorySnapshot(),
            wallet,
            CreditsCurrency,
            priceyItem,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);

        Assert.True(anotherCopy.Applied);
        Assert.Equal(2, anotherCopy.AfterInventory.GetEquipmentInstances(StandardEquipmentSlotIds.Weapon).Count);
        Assert.Equal(ResourceTransactionCode.EquipmentDuplicate, duplicate.Code);
        Assert.Equal(1_000, Balance(duplicate.AfterCurrencyLedger));
        Assert.Equal(ResourceTransactionCode.ShopStockUnavailable, stock.Code);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, insufficient.Code);
    }

    [Fact]
    public void ShopOfferResolver_MapsAuthoredItemAndEquipmentOffersIntoRuntimeOffers()
    {
        var resolver = new RuntimeShopOfferResolver();
        ContentId medicine = Q("medicine");
        ContentId blade = Q("blade");
        var catalog = new GameDataCatalog(
            skills: [],
            entities: [],
            races: [],
            ailments: [],
            items:
            [
                new KeyValuePair<ContentId, ItemDefinition>(
                    medicine,
                    new ItemDefinition(
                        medicine,
                        "Medicine",
                        "Restores HP.",
                        ItemKind.Consumable,
                        stackLimit: 10,
                        baseValue: 50))
            ],
            equipment:
            [
                new KeyValuePair<ContentId, EquipmentDefinition>(
                    blade,
                    new EquipmentDefinition(
                        blade,
                        "Blade",
                        "A weapon.",
                        StandardEquipmentSlotIds.Weapon,
                        baseValue: 100,
                        weapon: new EquipmentWeaponProfileDefinition(
                            new EquipmentBasicAttackDefinition(
                                DamageElement.Physical,
                                10,
                                95,
                                new NeverCriticalDefinition(),
                                false))))
            ]);
        var itemOffer = new ShopOfferDefinition(
            ShopContentKind.Item,
            medicine,
            new FixedShopPriceDefinition(25),
            new LimitedShopStockDefinition(3));
        var equipmentOffer = new ShopOfferDefinition(
            ShopContentKind.Equipment,
            blade,
            new FixedShopPriceDefinition(100),
            new UnlimitedShopStockDefinition());

        RuntimeShopOfferResolutionResult item = resolver.Resolve(itemOffer, catalog, catalog);
        RuntimeShopOfferResolutionResult equipment = resolver.Resolve(equipmentOffer, catalog, catalog);

        RuntimeShopOfferSnapshot itemSnapshot = item.RequireOffer();
        RuntimeShopOfferSnapshot equipmentSnapshot = equipment.RequireOffer();
        Assert.Equal(ShopContentKind.Item, itemSnapshot.ContentKind);
        Assert.Equal(medicine, itemSnapshot.ContentId);
        Assert.Equal(25, itemSnapshot.BasePrice);
        Assert.Equal(10, itemSnapshot.ItemStackLimit);
        Assert.Equal(3, itemSnapshot.StockAvailable);
        Assert.Null(itemSnapshot.EquipmentSlotId);
        Assert.Equal(ShopContentKind.Equipment, equipmentSnapshot.ContentKind);
        Assert.Equal(blade, equipmentSnapshot.ContentId);
        Assert.Equal(100, equipmentSnapshot.BasePrice);
        Assert.Equal(StandardEquipmentSlotIds.Weapon, equipmentSnapshot.EquipmentSlotId);
        Assert.Null(equipmentSnapshot.ItemStackLimit);
        Assert.Null(equipmentSnapshot.StockAvailable);
    }

    [Fact]
    public void ShopOfferResolver_RejectsUnsupportedOrMalformedOffersWithoutRuntimeFallbacks()
    {
        var resolver = new RuntimeShopOfferResolver();
        ContentId medicine = Q("medicine");
        ContentId missingBlade = Q("missing_blade");
        var catalog = new GameDataCatalog(
            skills: [],
            entities: [],
            races: [],
            ailments: [],
            items:
            [
                new KeyValuePair<ContentId, ItemDefinition>(
                    medicine,
                    new ItemDefinition(
                        medicine,
                        "Medicine",
                        "Restores HP.",
                        ItemKind.Consumable,
                        stackLimit: 10,
                        baseValue: 50))
            ]);
        var missing = new ShopOfferDefinition(
            ShopContentKind.Equipment,
            missingBlade,
            new FixedShopPriceDefinition(100),
            new UnlimitedShopStockDefinition());
        var policyPrice = new ShopOfferDefinition(
            ShopContentKind.Item,
            medicine,
            new PolicyShopPriceDefinition(Id("dynamic_price")),
            new UnlimitedShopStockDefinition());
        var fractionalPrice = new ShopOfferDefinition(
            ShopContentKind.Item,
            medicine,
            new FixedShopPriceDefinition(12.5m),
            new UnlimitedShopStockDefinition());
        var policyStock = new ShopOfferDefinition(
            ShopContentKind.Item,
            medicine,
            new FixedShopPriceDefinition(12),
            new PolicyShopStockDefinition(Id("dynamic_stock")));

        RuntimeShopOfferResolutionResult missingResult = resolver.Resolve(missing, catalog, catalog);
        RuntimeShopOfferResolutionResult policyPriceResult = resolver.Resolve(policyPrice, catalog, catalog);
        RuntimeShopOfferResolutionResult fractionalPriceResult = resolver.Resolve(fractionalPrice, catalog, catalog);
        RuntimeShopOfferResolutionResult policyStockResult = resolver.Resolve(policyStock, catalog, catalog);

        Assert.False(missingResult.IsSuccess);
        Assert.Contains(missingResult.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeShopOfferResolutionCode.MissingEquipmentDefinition);
        Assert.False(policyPriceResult.IsSuccess);
        Assert.Contains(policyPriceResult.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeShopOfferResolutionCode.UnsupportedPricePolicy);
        Assert.False(fractionalPriceResult.IsSuccess);
        Assert.Contains(fractionalPriceResult.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeShopOfferResolutionCode.InvalidFixedPrice);
        Assert.False(policyStockResult.IsSuccess);
        Assert.Contains(policyStockResult.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeShopOfferResolutionCode.UnsupportedStockPolicy);
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
        RuntimeCurrencyLedgerSnapshot wallet = Ledger(120);

        HospitalRestorationResult restored = service.Restore(patient, wallet, CreditsCurrency);
        HospitalRestorationResult fullHealth = service.Restore(
            restored.AfterPatient,
            restored.AfterCurrencyLedger,
            CreditsCurrency);
        HospitalRestorationResult insufficient = service.Restore(
            patient,
            Ledger(1),
            CreditsCurrency);

        Assert.True(restored.Applied);
        Assert.Equal(120, restored.Cost);
        Assert.Equal(0, Balance(restored.AfterCurrencyLedger));
        Assert.Equal(restored.AfterPatient.MaxHp, restored.AfterPatient.CurrentHp);
        Assert.Equal(restored.AfterPatient.MaxSp, restored.AfterPatient.CurrentSp);
        Assert.False(restored.AfterPatient.HasAilment);
        Assert.False(restored.AfterPatient.HasEncounterPersistence);
        Assert.Equal(ResourceTransactionCode.NoRestorationNeeded, fullHealth.Code);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, insufficient.Code);
        Assert.Equal(1, Balance(insufficient.AfterCurrencyLedger));
    }

    [Fact]
    public void HospitalService_SaturatesExtremeCostWithoutArithmeticOverflow()
    {
        var service = new HospitalRestorationService();
        var patient = new RuntimeHospitalPatientSnapshot(
            RuntimeInstanceId.Parse("patient"),
            currentHp: 0,
            maxHp: int.MaxValue,
            currentSp: 0,
            maxSp: int.MaxValue,
            hasAilment: false);
        RuntimeCurrencyLedgerSnapshot wallet = Ledger(0);

        int cost = service.CalculateRestorationCost(patient);
        HospitalRestorationResult result = service.Restore(patient, wallet, CreditsCurrency);

        Assert.Equal(int.MaxValue, cost);
        Assert.False(result.Applied);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, result.Code);
        Assert.Same(patient, result.BeforePatient);
        Assert.Same(patient, result.AfterPatient);
        Assert.Same(wallet, result.BeforeCurrencyLedger);
        Assert.Same(wallet, result.AfterCurrencyLedger);
    }

    private static ContentId Id(string value) => ContentId.Parse(value);
    private static RuntimeInstanceId Instance(string value) => RuntimeInstanceId.Parse(value);
    private static RuntimeCurrencyLedgerSnapshot Ledger(int balance) =>
        RuntimeCurrencyLedgerSnapshot.Single(CreditsCurrency, balance);
    private static int Balance(RuntimeCurrencyLedgerSnapshot ledger) =>
        ledger.GetRequiredBalance(CreditsCurrency);

    private static void AssertPricingRejectedWithoutMutation(
        ShopTransactionResult result,
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot wallet,
        string expectedMessage)
    {
        Assert.False(result.Applied);
        Assert.Equal(ResourceTransactionCode.InvalidShopPricing, result.Code);
        Assert.Equal(0, result.Price);
        Assert.Same(inventory, result.BeforeInventory);
        Assert.Same(inventory, result.AfterInventory);
        Assert.Same(wallet, result.BeforeCurrencyLedger);
        Assert.Same(wallet, result.AfterCurrencyLedger);
        ResourceTransactionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ResourceTransactionCode.InvalidShopPricing, diagnostic.Code);
        Assert.Contains(expectedMessage, diagnostic.Message, StringComparison.Ordinal);
    }

    private static ContentId Q(string localId) => ContentId.Parse($"test.pack:{localId}");

    private static EquipmentDefinition Weapon(ContentId id, int power, int accuracy) =>
        new(
            id,
            id.ToString(),
            "test weapon",
            StandardEquipmentSlotIds.Weapon,
            baseValue: 10,
            weapon: new EquipmentWeaponProfileDefinition(
                new EquipmentBasicAttackDefinition(
                    DamageElement.Physical,
                    power,
                    accuracy,
                    new NeverCriticalDefinition(),
                    IsLongRange: false)));

    private static EquipmentDefinition Accessory(ContentId id, params StatModifierDefinition[] modifiers) =>
        new(
            id,
            id.ToString(),
            "test accessory",
            StandardEquipmentSlotIds.Accessory,
            baseValue: 10,
            accessory: new EquipmentAccessoryProfileDefinition(modifiers));

    private static EquipmentDefinition Armor(
        ContentId id,
        int defense,
        int evasion,
        params ContentId[] grantedSkillIds) =>
        new(
            id,
            id.ToString(),
            "test armor",
            StandardEquipmentSlotIds.Armor,
            baseValue: 10,
            grantedSkillIds: grantedSkillIds,
            armor: new EquipmentArmorProfileDefinition(defense, evasion));

    private static EquipmentDefinition Boots(
        ContentId id,
        int evasion,
        params ContentId[] grantedSkillIds) =>
        new(
            id,
            id.ToString(),
            "test boots",
            StandardEquipmentSlotIds.Boots,
            baseValue: 10,
            grantedSkillIds: grantedSkillIds,
            boots: new EquipmentBootsProfileDefinition(evasion));

    private static KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>> Owned(
        ContentId slotId,
        RuntimeInstanceId instanceId,
        ContentId definitionId) =>
        new(slotId, [new RuntimeEquipmentInstanceSnapshot(instanceId, definitionId)]);

    private sealed class TestEquipmentRepository(params EquipmentDefinition[] definitions) : IEquipmentDefinitionRepository
    {
        private readonly Dictionary<ContentId, EquipmentDefinition> _definitions =
            definitions.ToDictionary(definition => definition.Id);

        public bool TryGetEquipment(ContentId id, out EquipmentDefinition? equipment) =>
            _definitions.TryGetValue(id, out equipment);

        public EquipmentDefinition GetRequiredEquipment(ContentId id) =>
            TryGetEquipment(id, out EquipmentDefinition? equipment)
                ? equipment!
                : throw new KeyNotFoundException(id.ToString());
    }
}
