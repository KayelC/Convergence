using Convergence.Catalog;
using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class ShopStockPolicyTests
{
    private static readonly ContentId Credits = Id("test.pack:credits");
    private static readonly ContentId Medicine = Id("convergence.shared_effects_demo:medicine_demo");
    private static readonly ContentId ShopA = Id("test.pack:shop_a");
    private static readonly ContentId ShopB = Id("test.pack:shop_b");
    private static readonly ContentId OfferA = Id("medicine_offer");

    [Fact]
    public void StandardPolicy_DecrementsPurchasesRejectsZeroAndDoesNotReplenishResales()
    {
        var policy = new StandardShopStockPolicy();

        ShopStockTransitionResult purchase = policy.Apply(
            new ShopStockTransitionRequest(ShopStockOperation.Purchase, 2));
        ShopStockTransitionResult unavailable = policy.Apply(
            new ShopStockTransitionRequest(ShopStockOperation.Purchase, 0));
        ShopStockTransitionResult resale = policy.Apply(
            new ShopStockTransitionRequest(ShopStockOperation.Resale, 2));
        ShopStockTransitionResult invalid = policy.Apply(
            new ShopStockTransitionRequest(ShopStockOperation.Purchase, -1));

        Assert.Equal(ShopStockTransitionCode.Applied, purchase.Code);
        Assert.Equal(1, purchase.RemainingQuantity);
        Assert.Equal(ShopStockTransitionCode.Unavailable, unavailable.Code);
        Assert.Equal(0, unavailable.RemainingQuantity);
        Assert.Equal(ShopStockTransitionCode.Applied, resale.Code);
        Assert.Equal(2, resale.RemainingQuantity);
        Assert.Equal(ShopStockTransitionCode.InvalidQuantity, invalid.Code);
        Assert.Equal(0, invalid.RemainingQuantity);
    }

    [Fact]
    public void StockSnapshot_UsesCompositeOfferIdentityAndRejectsOnlyTrueDuplicates()
    {
        RuntimeShopOfferSnapshot first = Offer(ShopA, OfferA, Medicine, quantity: 2);
        RuntimeShopOfferSnapshot secondShop = Offer(ShopB, OfferA, Medicine, quantity: 3);
        RuntimeShopOfferSnapshot secondOffer = Offer(
            ShopA,
            Id("discount_medicine_offer"),
            Medicine,
            quantity: 4);

        RuntimeShopStockSnapshot snapshot = RuntimeShopStockSnapshot.CreateInitial(
            [first, secondShop, secondOffer]);

        Assert.Equal(3, snapshot.Entries.Count);
        AssertQuantity(snapshot, first.Identity, 2);
        AssertQuantity(snapshot, secondShop.Identity, 3);
        AssertQuantity(snapshot, secondOffer.Identity, 4);
        Assert.Throws<ArgumentException>(() =>
            RuntimeShopStockSnapshot.CreateInitial([first, first]));
    }

    [Fact]
    public void Buy_CommitsInventoryCurrencyAndStockTogetherAndDepletesExactlyOnce()
    {
        var service = new ShopTransactionService();
        RuntimeShopOfferSnapshot offer = Offer(ShopA, OfferA, Medicine, quantity: 2);
        RuntimeShopStockSnapshot stock = RuntimeShopStockSnapshot.CreateInitial([offer]);
        var inventory = new RuntimeInventorySnapshot();
        RuntimeCurrencyLedgerSnapshot wallet = Ledger(30);

        ShopTransactionResult first = service.Buy(
            inventory,
            wallet,
            stock,
            Credits,
            offer,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);
        ShopTransactionResult second = service.Buy(
            first.AfterInventory,
            first.AfterCurrencyLedger,
            first.AfterStock,
            Credits,
            offer,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);
        ShopTransactionResult third = service.Buy(
            second.AfterInventory,
            second.AfterCurrencyLedger,
            second.AfterStock,
            Credits,
            offer,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);

        Assert.True(first.Applied);
        Assert.Equal(1, first.AfterInventory.GetQuantity(Medicine));
        Assert.Equal(20, Balance(first.AfterCurrencyLedger));
        AssertQuantity(first.AfterStock, offer.Identity, 1);
        Assert.True(second.Applied);
        Assert.Equal(2, second.AfterInventory.GetQuantity(Medicine));
        Assert.Equal(10, Balance(second.AfterCurrencyLedger));
        AssertQuantity(second.AfterStock, offer.Identity, 0);
        Assert.False(third.Applied);
        Assert.Equal(ResourceTransactionCode.ShopStockUnavailable, third.Code);
        Assert.Same(second.AfterInventory, third.AfterInventory);
        Assert.Same(second.AfterCurrencyLedger, third.AfterCurrencyLedger);
        Assert.Same(second.AfterStock, third.AfterStock);
    }

    [Fact]
    public void Buy_RejectionsPreserveAllThreeAuthoritiesAtomically()
    {
        var service = new ShopTransactionService();
        RuntimeShopOfferSnapshot ordinary = Offer(ShopA, OfferA, Medicine, quantity: 2);
        RuntimeShopStockSnapshot validStock = RuntimeShopStockSnapshot.CreateInitial([ordinary]);
        RuntimeCurrencyLedgerSnapshot funded = Ledger(100);
        var emptyInventory = new RuntimeInventorySnapshot();

        ShopTransactionResult insufficientCurrency = service.Buy(
            emptyInventory,
            Ledger(0),
            validStock,
            Credits,
            ordinary,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);
        var fullInventory = new RuntimeInventorySnapshot(
            [new KeyValuePair<ContentId, int>(Medicine, 1)]);
        RuntimeShopOfferSnapshot stackLimited = ordinary with { ItemStackLimit = 1 };
        ShopTransactionResult fullStack = service.Buy(
            fullInventory,
            funded,
            validStock,
            Credits,
            stackLimited,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);
        ShopTransactionResult missingStock = service.Buy(
            emptyInventory,
            funded,
            new RuntimeShopStockSnapshot(),
            Credits,
            ordinary,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);
        var duplicateStock = new RuntimeShopStockSnapshot(
        [
            new RuntimeShopStockEntrySnapshot(ordinary.Identity, 2),
            new RuntimeShopStockEntrySnapshot(ordinary.Identity, 2)
        ]);
        ShopTransactionResult duplicate = service.Buy(
            emptyInventory,
            funded,
            duplicateStock,
            Credits,
            ordinary,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);
        RuntimeShopOfferSnapshot throwing = Offer(
            ShopA,
            Id("throwing_offer"),
            Medicine,
            quantity: 2,
            policy: new ThrowingStockPolicy());
        RuntimeShopStockSnapshot throwingStock = RuntimeShopStockSnapshot.CreateInitial([throwing]);
        ShopTransactionResult policyFailure = service.Buy(
            emptyInventory,
            funded,
            throwingStock,
            Credits,
            throwing,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);
        RuntimeShopOfferSnapshot unlimited = Offer(
            ShopA,
            Id("unlimited_offer"),
            Medicine,
            quantity: null);
        var unexpectedUnlimitedStock = new RuntimeShopStockSnapshot(
        [
            new RuntimeShopStockEntrySnapshot(unlimited.Identity, 2)
        ]);
        ShopTransactionResult trackedUnlimited = service.Buy(
            emptyInventory,
            funded,
            unexpectedUnlimitedStock,
            Credits,
            unlimited,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null);

        AssertAtomicRejection(insufficientCurrency, emptyInventory, insufficientCurrency.BeforeCurrencyLedger, validStock,
            ResourceTransactionCode.InsufficientCurrency);
        AssertAtomicRejection(fullStack, fullInventory, funded, validStock,
            ResourceTransactionCode.ItemStackExceeded);
        AssertAtomicRejection(missingStock, emptyInventory, funded, missingStock.BeforeStock,
            ResourceTransactionCode.InvalidShopStock);
        AssertAtomicRejection(duplicate, emptyInventory, funded, duplicateStock,
            ResourceTransactionCode.InvalidShopStock);
        AssertAtomicRejection(policyFailure, emptyInventory, funded, throwingStock,
            ResourceTransactionCode.InvalidShopStock);
        AssertAtomicRejection(trackedUnlimited, emptyInventory, funded, unexpectedUnlimitedStock,
            ResourceTransactionCode.InvalidShopStock);
    }

    [Fact]
    public void Sell_UsesStandardNonReplenishmentOrAnExplicitCustomReplenishmentPolicy()
    {
        var service = new ShopTransactionService();
        var inventory = new RuntimeInventorySnapshot(
            [new KeyValuePair<ContentId, int>(Medicine, 1)]);
        RuntimeCurrencyLedgerSnapshot wallet = Ledger(0);
        RuntimeShopOfferSnapshot standard = Offer(ShopA, OfferA, Medicine, quantity: 2);
        RuntimeShopStockSnapshot standardStock = RuntimeShopStockSnapshot.CreateInitial([standard]);
        RuntimeShopOfferSnapshot replenishing = Offer(
            ShopA,
            Id("replenishing_offer"),
            Medicine,
            quantity: 2,
            policy: new ReplenishingStockPolicy());
        RuntimeShopStockSnapshot replenishingStock = RuntimeShopStockSnapshot.CreateInitial([replenishing]);

        ShopTransactionResult standardSale = service.Sell(
            inventory,
            wallet,
            standardStock,
            Credits,
            standard,
            sellerLuck: 0,
            soldEquipmentInstanceId: null,
            actorEquipment: []);
        ShopTransactionResult customSale = service.Sell(
            inventory,
            wallet,
            replenishingStock,
            Credits,
            replenishing,
            sellerLuck: 0,
            soldEquipmentInstanceId: null,
            actorEquipment: []);

        Assert.True(standardSale.Applied);
        AssertQuantity(standardSale.AfterStock, standard.Identity, 2);
        Assert.True(customSale.Applied);
        AssertQuantity(customSale.AfterStock, replenishing.Identity, 3);
        Assert.Equal(0, customSale.AfterInventory.GetQuantity(Medicine));
        Assert.Equal(5, Balance(customSale.AfterCurrencyLedger));
    }

    [Fact]
    public void StockPolicies_PropagateCancellationAndConvertOtherFailuresToTypedRejection()
    {
        RuntimeShopOfferSnapshot canceling = Offer(
            ShopA,
            Id("canceling_offer"),
            Medicine,
            quantity: 1,
            policy: new CancelingStockPolicy());
        RuntimeShopOfferSnapshot nullReturning = Offer(
            ShopA,
            Id("null_offer"),
            Medicine,
            quantity: 1,
            policy: new NullStockPolicy());

        Assert.Throws<OperationCanceledException>(() =>
            canceling.Stock.Apply(ShopStockOperation.Purchase, 1));
        ShopStockTransitionResult rejected = nullReturning.Stock.Apply(
            ShopStockOperation.Purchase,
            1);
        Assert.Equal(ShopStockTransitionCode.PolicyRejected, rejected.Code);
        Assert.Equal(1, rejected.RemainingQuantity);
    }

    [Fact]
    public void PolicyFactoryRegistry_BindsExplicitPolicyAndNeverFallsBackWhenItIsMissing()
    {
        var factory = new ReplenishingStockPolicyFactory();
        ShopStockPolicyFactoryRegistry registry =
            ShopStockPolicyFactoryRegistry.CreateStandard([factory]);

        ShopStockPolicyBindingResult standard = registry.Bind(
            StandardShopStockPolicyIds.Standard,
            EmptyParameters());
        ShopStockPolicyBindingResult custom = registry.Bind(
            factory.PolicyId,
            EmptyParameters());
        ShopStockPolicyBindingResult missing = registry.Bind(
            Id("missing_stock"),
            EmptyParameters());

        Assert.True(standard.IsSuccess);
        Assert.IsType<StandardShopStockPolicy>(standard.RequirePolicy().Policy);
        Assert.True(custom.IsSuccess);
        Assert.IsType<ReplenishingStockPolicy>(custom.RequirePolicy().Policy);
        Assert.False(missing.IsSuccess);
        Assert.Equal(
            ShopStockPolicyDiagnosticCode.UnsupportedPolicy,
            Assert.Single(missing.Diagnostics).Code);
        Assert.Throws<ArgumentException>(() =>
            ShopStockPolicyFactoryRegistry.CreateStandard(
                [new DuplicateStandardStockPolicyFactory()]));
    }

    [Fact]
    public void OfferResolver_BindsFixedAndExplicitStockPoliciesWithoutFallback()
    {
        GameDataCatalog catalog = RuntimePersistenceSnapshotTests.LoadCatalog();
        var factory = new ReplenishingStockPolicyFactory();
        var resolver = Resolver(factory);
        var fixedOffer = new ShopOfferDefinition(
            Id("fixed_offer"),
            ShopContentKind.Item,
            Medicine,
            new FixedShopPriceDefinition(10),
            new LimitedShopStockDefinition(2));
        var customOffer = new ShopOfferDefinition(
            Id("custom_offer"),
            ShopContentKind.Item,
            Medicine,
            new FixedShopPriceDefinition(10),
            new PolicyShopStockDefinition(factory.PolicyId, 3));
        var unsupportedOffer = new ShopOfferDefinition(
            Id("unsupported_offer"),
            ShopContentKind.Item,
            Medicine,
            new FixedShopPriceDefinition(10),
            new PolicyShopStockDefinition(Id("missing_stock"), 3));

        RuntimeShopOfferSnapshot fixedRuntime = resolver
            .Resolve(ShopA, fixedOffer, catalog, catalog)
            .RequireOffer();
        RuntimeShopOfferSnapshot customRuntime = resolver
            .Resolve(ShopA, customOffer, catalog, catalog)
            .RequireOffer();
        RuntimeShopOfferResolutionResult unsupported = resolver.Resolve(
            ShopA,
            unsupportedOffer,
            catalog,
            catalog);

        Assert.Equal(StandardShopStockPolicyIds.Standard, fixedRuntime.Stock.Policy!.PolicyId);
        Assert.Equal(factory.PolicyId, customRuntime.Stock.Policy!.PolicyId);
        Assert.Equal(2, fixedRuntime.Stock.InitialQuantity);
        Assert.Equal(3, customRuntime.Stock.InitialQuantity);
        Assert.False(unsupported.IsSuccess);
        Assert.Equal(
            RuntimeShopOfferResolutionCode.UnsupportedStockPolicy,
            Assert.Single(unsupported.Diagnostics).Code);
    }

    [Fact]
    public void SaveValidator_ReportsEveryMalformedShopStockShapeWithTypedDiagnostics()
    {
        GameDataCatalog catalog = RuntimePersistenceSnapshotTests.LoadCatalog();
        RuntimeSaveGameSnapshot baseline = RuntimePersistenceSnapshotTests.CreateSaveSnapshot();
        RuntimeShopStockEntrySnapshot valid = Assert.Single(baseline.ShopStock.Entries);
        var cases = new (RuntimeShopStockSnapshot Stock, RuntimeSaveValidationCode Code)[]
        {
            (new RuntimeShopStockSnapshot([valid, valid]),
                RuntimeSaveValidationCode.DuplicateShopStockEntry),
            (new RuntimeShopStockSnapshot([
                new RuntimeShopStockEntrySnapshot(valid.OfferIdentity, -1)]),
                RuntimeSaveValidationCode.NegativeShopStockQuantity),
            (new RuntimeShopStockSnapshot(),
                RuntimeSaveValidationCode.MissingShopStockEntry),
            (new RuntimeShopStockSnapshot([
                new RuntimeShopStockEntrySnapshot(
                    new RuntimeShopOfferIdentity(Id("missing.pack:shop"), OfferA), 1)]),
                RuntimeSaveValidationCode.MissingCatalogShop),
            (new RuntimeShopStockSnapshot([
                new RuntimeShopStockEntrySnapshot(
                    new RuntimeShopOfferIdentity(valid.OfferIdentity.ShopId, Id("missing_offer")), 1)]),
                RuntimeSaveValidationCode.MissingShopOffer),
            (new RuntimeShopStockSnapshot([
                valid,
                new RuntimeShopStockEntrySnapshot(
                    new RuntimeShopOfferIdentity(
                        valid.OfferIdentity.ShopId,
                        Id("shortsword_offer")), 1)]),
                RuntimeSaveValidationCode.UnexpectedShopStockEntry)
        };

        var validator = new RuntimeSaveValidator();
        foreach ((RuntimeShopStockSnapshot stock, RuntimeSaveValidationCode expected) in cases)
        {
            RuntimeSaveValidationResult result = validator.Validate(
                CopySave(baseline, stock),
                catalog);

            Assert.False(result.IsValid);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expected);
        }
    }

    private static RuntimeShopOfferResolver Resolver(
        params IShopStockPolicyFactory[] additionalFactories)
    {
        var standardPricing = new BoundShopPricingPolicy(
            StandardShopPricingPolicyIds.Standard,
            new StandardShopPricingPolicy());
        return new RuntimeShopOfferResolver(
            standardPricing,
            ShopPricingPolicyFactoryRegistry.CreateStandard(),
            ShopStockPolicyFactoryRegistry.CreateStandard(additionalFactories));
    }

    private static RuntimeShopOfferSnapshot Offer(
        ContentId shopId,
        ContentId offerId,
        ContentId contentId,
        int? quantity,
        IShopStockPolicy? policy = null) =>
        new(
            new RuntimeShopOfferIdentity(shopId, offerId),
            ShopContentKind.Item,
            contentId,
            new RuntimeShopPricingProfile(
                10,
                new BoundShopPricingPolicy(
                    StandardShopPricingPolicyIds.Standard,
                    new StandardShopPricingPolicy())),
            quantity is int initial
                ? new RuntimeShopStockProfile(
                    initial,
                    new BoundShopStockPolicy(
                        policy is null
                            ? StandardShopStockPolicyIds.Standard
                            : Id("test_stock"),
                        policy ?? new StandardShopStockPolicy()))
                : RuntimeShopStockProfile.Unlimited,
            ItemStackLimit: 99);

    private static RuntimeSaveGameSnapshot CopySave(
        RuntimeSaveGameSnapshot source,
        RuntimeShopStockSnapshot stock) =>
        new(
            source.FrameworkVersion,
            source.ContentPacks,
            source.Actors,
            source.PartyRoster,
            source.Inventory,
            source.CurrencyLedger,
            stock,
            source.Field,
            source.Compendium,
            source.Knowledge,
            source.Session,
            source.Checkpoints,
            source.HostContext,
            source.ContractVersion);

    private static void AssertAtomicRejection(
        ShopTransactionResult result,
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot stock,
        ResourceTransactionCode expectedCode)
    {
        Assert.False(result.Applied);
        Assert.Equal(expectedCode, result.Code);
        Assert.Same(inventory, result.BeforeInventory);
        Assert.Same(inventory, result.AfterInventory);
        Assert.Same(wallet, result.BeforeCurrencyLedger);
        Assert.Same(wallet, result.AfterCurrencyLedger);
        Assert.Same(stock, result.BeforeStock);
        Assert.Same(stock, result.AfterStock);
    }

    private static void AssertQuantity(
        RuntimeShopStockSnapshot stock,
        RuntimeShopOfferIdentity identity,
        int expected)
    {
        Assert.True(stock.TryGetRemainingQuantity(identity, out int actual));
        Assert.Equal(expected, actual);
    }

    private static RuntimeCurrencyLedgerSnapshot Ledger(int balance) =>
        RuntimeCurrencyLedgerSnapshot.Single(Credits, balance);

    private static int Balance(RuntimeCurrencyLedgerSnapshot ledger) =>
        ledger.GetRequiredBalance(Credits);

    private static IReadOnlyDictionary<string, object?> EmptyParameters() =>
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class ReplenishingStockPolicy : IShopStockPolicy
    {
        public ShopStockTransitionResult Apply(ShopStockTransitionRequest request) =>
            request.Operation == ShopStockOperation.Resale
                ? ShopStockTransitionResult.Applied(checked(request.CurrentQuantity + 1))
                : new StandardShopStockPolicy().Apply(request);
    }

    private sealed class ReplenishingStockPolicyFactory : IShopStockPolicyFactory
    {
        public ContentId PolicyId => Id("replenishing_stock");

        public ShopStockPolicyBindingResult Create(
            IReadOnlyDictionary<string, object?> parameters) =>
            parameters.Count == 0
                ? new ShopStockPolicyBindingResult(
                    new BoundShopStockPolicy(PolicyId, new ReplenishingStockPolicy()))
                : new ShopStockPolicyBindingResult(
                    null,
                    [new ShopStockPolicyDiagnostic(
                        ShopStockPolicyDiagnosticCode.UnknownParameter,
                        "Replenishing stock accepts no parameters.",
                        PolicyId: PolicyId)]);
    }

    private sealed class DuplicateStandardStockPolicyFactory : IShopStockPolicyFactory
    {
        public ContentId PolicyId => StandardShopStockPolicyIds.Standard;

        public ShopStockPolicyBindingResult Create(
            IReadOnlyDictionary<string, object?> parameters) =>
            new(new BoundShopStockPolicy(PolicyId, new StandardShopStockPolicy()));
    }

    private sealed class ThrowingStockPolicy : IShopStockPolicy
    {
        public ShopStockTransitionResult Apply(ShopStockTransitionRequest request) =>
            throw new InvalidOperationException("stock failure");
    }

    private sealed class CancelingStockPolicy : IShopStockPolicy
    {
        public ShopStockTransitionResult Apply(ShopStockTransitionRequest request) =>
            throw new OperationCanceledException("stock canceled");
    }

    private sealed class NullStockPolicy : IShopStockPolicy
    {
        public ShopStockTransitionResult Apply(ShopStockTransitionRequest request) => null!;
    }
}
