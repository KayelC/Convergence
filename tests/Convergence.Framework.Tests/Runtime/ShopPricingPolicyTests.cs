using Convergence.Catalog;
using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class ShopPricingPolicyTests
{
    private static readonly ContentId Credits = Id("credits");
    private static readonly ContentId Medicine = Id("test.pack:medicine");
    private static readonly ContentId TestShop = Id("test.pack:test_shop");

    [Fact]
    public void StandardPolicy_PreservesPurchaseAndTruncatesConfiguredResaleTowardZero()
    {
        var configured = new StandardShopPricingPolicy(0.375m);
        var suppliedDefault = new StandardShopPricingPolicy();

        ShopPriceCalculationResult purchase = configured.Calculate(Request(
            authoredPurchasePrice: 101,
            luck: 99,
            ShopPriceOperation.Purchase));
        ShopPriceCalculationResult configuredResale = configured.Calculate(Request(
            authoredPurchasePrice: 101,
            luck: 99,
            ShopPriceOperation.Resale));
        ShopPriceCalculationResult defaultResale = suppliedDefault.Calculate(Request(
            authoredPurchasePrice: 101,
            luck: 99,
            ShopPriceOperation.Resale));

        Assert.Equal(101, purchase.Price);
        Assert.Equal(37, configuredResale.Price);
        Assert.Equal(50, defaultResale.Price);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StandardShopPricingPolicy(-0.01m));
    }

    [Fact]
    public void LuckAdjustedPolicy_PreservesTheFormerOptionalFormulaExactly()
    {
        var policy = new LuckAdjustedShopPricingPolicy();

        Assert.Equal(47, policy.Calculate(Request(50, 6, ShopPriceOperation.Purchase)).Price);
        Assert.Equal(28, policy.Calculate(Request(50, 6, ShopPriceOperation.Resale)).Price);
        Assert.Equal(50, policy.Calculate(Request(100, 100, ShopPriceOperation.Purchase)).Price);
        Assert.Equal(50, policy.Calculate(Request(100, int.MaxValue, ShopPriceOperation.Purchase)).Price);
        Assert.Equal(
            ShopPriceCalculationCode.NumericOverflow,
            policy.Calculate(Request(int.MaxValue, int.MaxValue, ShopPriceOperation.Resale)).Code);
    }

    [Fact]
    public void SuppliedPolicies_ReturnTypedNegativeInputDiagnostics()
    {
        var standard = new StandardShopPricingPolicy();
        var luckAdjusted = new LuckAdjustedShopPricingPolicy();

        Assert.Equal(
            ShopPriceCalculationCode.NegativePurchasePrice,
            standard.Calculate(Request(-1, 0, ShopPriceOperation.Purchase)).Code);
        Assert.Equal(
            ShopPriceCalculationCode.NegativeLuck,
            standard.Calculate(Request(1, -1, ShopPriceOperation.Purchase)).Code);
        Assert.Equal(
            ShopPriceCalculationCode.NegativePurchasePrice,
            luckAdjusted.Calculate(Request(-1, 0, ShopPriceOperation.Resale)).Code);
        Assert.Equal(
            ShopPriceCalculationCode.NegativeLuck,
            luckAdjusted.Calculate(Request(1, -1, ShopPriceOperation.Resale)).Code);
    }

    [Fact]
    public void StandardFactoryRegistry_BindsConfiguredPercentageAndRejectsMalformedParameters()
    {
        ShopPricingPolicyFactoryRegistry registry = ShopPricingPolicyFactoryRegistry.CreateStandard();

        BoundShopPricingPolicy configured = registry.Bind(
            StandardShopPricingPolicyIds.Standard,
            Parameters(("resalePercentage", 0.25m)))
            .RequirePolicy();
        ShopPricingPolicyBindingResult unknown = registry.Bind(
            StandardShopPricingPolicyIds.Standard,
            Parameters(("discount", 0.1m)));
        ShopPricingPolicyBindingResult wrongType = registry.Bind(
            StandardShopPricingPolicyIds.Standard,
            Parameters(("resalePercentage", "half")));
        ShopPricingPolicyBindingResult negative = registry.Bind(
            StandardShopPricingPolicyIds.Standard,
            Parameters(("resalePercentage", -0.1m)));

        Assert.Equal(
            25,
            configured.Policy.Calculate(Request(100, 0, ShopPriceOperation.Resale)).Price);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.UnknownParameter, Assert.Single(unknown.Diagnostics).Code);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.InvalidParameterType, Assert.Single(wrongType.Diagnostics).Code);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.InvalidParameterValue, Assert.Single(negative.Diagnostics).Code);
    }

    [Fact]
    public void PricingFactoryRegistry_ResolvesHostFactoryAndContainsMalformedFactories()
    {
        ContentId customId = Id("custom_pricing");
        ContentId throwingId = Id("throwing_pricing");
        ContentId mismatchedId = Id("mismatched_pricing");
        ContentId silentId = Id("silent_pricing");
        var registry = ShopPricingPolicyFactoryRegistry.CreateStandard(
        [
            new FixedPricingFactory(customId, purchasePrice: 12, resalePrice: 7),
            new ThrowingPricingFactory(throwingId),
            new MismatchedPricingFactory(mismatchedId),
            new SilentRejectingPricingFactory(silentId)
        ]);

        ShopPricingPolicyBindingResult custom = registry.Bind(customId, EmptyParameters());
        ShopPricingPolicyBindingResult unsupported = registry.Bind(Id("missing_pricing"), EmptyParameters());
        ShopPricingPolicyBindingResult throwing = registry.Bind(throwingId, EmptyParameters());
        ShopPricingPolicyBindingResult mismatched = registry.Bind(mismatchedId, EmptyParameters());
        ShopPricingPolicyBindingResult silent = registry.Bind(silentId, EmptyParameters());

        Assert.Equal(12, custom.RequirePolicy().Policy.Calculate(Request(100, 0, ShopPriceOperation.Purchase)).Price);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.UnsupportedPolicy, Assert.Single(unsupported.Diagnostics).Code);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.PolicyFactoryFailure, Assert.Single(throwing.Diagnostics).Code);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.PolicyFactoryFailure, Assert.Single(mismatched.Diagnostics).Code);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.PolicyFactoryFailure, Assert.Single(silent.Diagnostics).Code);
    }

    [Fact]
    public void PricingFactoryRegistry_PreservesCancellationAndRejectsInvalidFactoryIdentity()
    {
        ContentId cancellationId = Id("cancel_pricing");
        var registry = ShopPricingPolicyFactoryRegistry.CreateStandard(
            [new CancelingPricingFactory(cancellationId)]);

        Assert.Throws<OperationCanceledException>(() => registry.Bind(cancellationId, EmptyParameters()));
        Assert.Throws<ArgumentException>(() => new ShopPricingPolicyFactoryRegistry(
        [
            new FixedPricingFactory(Id("duplicate"), 1, 1),
            new FixedPricingFactory(Id("duplicate"), 2, 2)
        ]));
        Assert.Throws<ArgumentException>(() => new ShopPricingPolicyFactoryRegistry(
            [new FixedPricingFactory(Id("test.pack:qualified"), 1, 1)]));
    }

    [Fact]
    public void OfferResolver_UsesEconomyDefaultForFixedOffersAndExplicitFactoryForPolicyOffers()
    {
        ShopPricingPolicyFactoryRegistry factories = ShopPricingPolicyFactoryRegistry.CreateStandard();
        BoundShopPricingPolicy luckDefault = factories
            .Bind(StandardShopPricingPolicyIds.LuckAdjusted, EmptyParameters())
            .RequirePolicy();
        var resolver = new RuntimeShopOfferResolver(
            luckDefault,
            factories,
            ShopStockPolicyFactoryRegistry.CreateStandard());
        GameDataCatalog catalog = ItemCatalog();
        var fixedOffer = new ShopOfferDefinition(
            Id("fixed_offer"),
            ShopContentKind.Item,
            Medicine,
            new FixedShopPriceDefinition(100),
            new UnlimitedShopStockDefinition());
        var explicitStandardOffer = new ShopOfferDefinition(
            Id("explicit_standard_offer"),
            ShopContentKind.Item,
            Medicine,
            new PolicyShopPriceDefinition(
                StandardShopPricingPolicyIds.Standard,
                Parameters(("purchasePrice", 100), ("resalePercentage", 0.25m))),
            new UnlimitedShopStockDefinition());

        RuntimeShopOfferSnapshot fixedRuntime = resolver.Resolve(TestShop, fixedOffer, catalog, catalog).RequireOffer();
        RuntimeShopOfferSnapshot explicitRuntime = resolver.Resolve(TestShop, explicitStandardOffer, catalog, catalog).RequireOffer();
        var shop = new ShopTransactionService();

        Assert.Equal(StandardShopPricingPolicyIds.LuckAdjusted, fixedRuntime.Pricing.Policy.PolicyId);
        Assert.Equal(90, shop.CalculateBuyPrice(fixedRuntime, luck: 10));
        Assert.Equal(60, shop.CalculateSellPrice(fixedRuntime, luck: 10));
        Assert.Equal(StandardShopPricingPolicyIds.Standard, explicitRuntime.Pricing.Policy.PolicyId);
        Assert.Equal(100, shop.CalculateBuyPrice(explicitRuntime, luck: 10));
        Assert.Equal(25, shop.CalculateSellPrice(explicitRuntime, luck: 10));
    }

    [Fact]
    public void OfferResolver_DoesNotFallBackAfterAnExplicitPolicyFails()
    {
        ShopPricingPolicyFactoryRegistry factories = ShopPricingPolicyFactoryRegistry.CreateStandard();
        BoundShopPricingPolicy defaultPolicy = factories
            .Bind(StandardShopPricingPolicyIds.Standard, EmptyParameters())
            .RequirePolicy();
        var resolver = new RuntimeShopOfferResolver(
            defaultPolicy,
            factories,
            ShopStockPolicyFactoryRegistry.CreateStandard());
        GameDataCatalog catalog = ItemCatalog();
        var definition = new ShopOfferDefinition(
            Id("unsupported_offer"),
            ShopContentKind.Item,
            Medicine,
            new PolicyShopPriceDefinition(
                Id("unregistered_pricing"),
                Parameters(("purchasePrice", 100))),
            new UnlimitedShopStockDefinition());

        RuntimeShopOfferResolutionResult result = resolver.Resolve(TestShop, definition, catalog, catalog);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Offer);
        RuntimeShopOfferResolutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuntimeShopOfferResolutionCode.UnsupportedPricePolicy, diagnostic.Code);
        Assert.Equal(ShopPricingPolicyDiagnosticCode.UnsupportedPolicy, diagnostic.PricingDiagnostic?.Code);
    }

    public static TheoryData<object?> InvalidPolicyPurchasePrices =>
        new()
        {
            null,
            "not-a-number",
            -1,
            1.5m,
            2_147_483_648L
        };

    [Theory]
    [MemberData(nameof(InvalidPolicyPurchasePrices))]
    public void OfferResolver_RejectsMissingOrInvalidPolicyPurchasePrice(object? purchasePrice)
    {
        ShopPricingPolicyFactoryRegistry factories = ShopPricingPolicyFactoryRegistry.CreateStandard();
        BoundShopPricingPolicy defaultPolicy = factories
            .Bind(StandardShopPricingPolicyIds.Standard, EmptyParameters())
            .RequirePolicy();
        var resolver = new RuntimeShopOfferResolver(
            defaultPolicy,
            factories,
            ShopStockPolicyFactoryRegistry.CreateStandard());
        IEnumerable<KeyValuePair<string, object?>> parameters = purchasePrice is null
            ? []
            : Parameters(("purchasePrice", purchasePrice));
        var definition = new ShopOfferDefinition(
            Id("invalid_price_offer"),
            ShopContentKind.Item,
            Medicine,
            new PolicyShopPriceDefinition(StandardShopPricingPolicyIds.Standard, parameters),
            new UnlimitedShopStockDefinition());

        RuntimeShopOfferResolutionResult result = resolver.Resolve(
            TestShop,
            definition,
            ItemCatalog(),
            ItemCatalog());

        Assert.False(result.IsSuccess);
        Assert.Null(result.Offer);
        Assert.Equal(
            RuntimeShopOfferResolutionCode.InvalidPricePolicyConfiguration,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void RuntimePricingProfile_ContainsHostPolicyFailureBeforeAnyTransactionMutation()
    {
        var inventory = new RuntimeInventorySnapshot();
        RuntimeCurrencyLedgerSnapshot ledger = RuntimeCurrencyLedgerSnapshot.Single(Credits, 100);
        var shop = new ShopTransactionService();
        RuntimeShopOfferSnapshot throwingOffer = Offer(new ThrowingPricingPolicy());
        RuntimeShopOfferSnapshot nullOffer = Offer(new NullPricingPolicy());

        ShopTransactionResult throwing = shop.Buy(
            inventory,
            ledger,
            new RuntimeShopStockSnapshot(),
            Credits,
            throwingOffer,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null,
            equipmentAcquisitionContext: null);
        ShopTransactionResult returnedNull = shop.Buy(
            inventory,
            ledger,
            new RuntimeShopStockSnapshot(),
            Credits,
            nullOffer,
            buyerLuck: 0,
            purchasedEquipmentInstanceId: null,
            equipmentAcquisitionContext: null);

        AssertRejectedWithoutMutation(throwing, inventory, ledger, "failed");
        AssertRejectedWithoutMutation(returnedNull, inventory, ledger, "returned no result");
    }

    [Fact]
    public void RuntimePricingProfile_DoesNotConvertHostPolicyCancellationIntoPricingRejection()
    {
        RuntimeShopOfferSnapshot offer = Offer(new CancelingPricingPolicy());

        Assert.Throws<OperationCanceledException>(() =>
            new ShopTransactionService().Buy(
                new RuntimeInventorySnapshot(),
                RuntimeCurrencyLedgerSnapshot.Single(Credits, 100),
                new RuntimeShopStockSnapshot(),
                Credits,
                offer,
                buyerLuck: 0,
                purchasedEquipmentInstanceId: null,
                equipmentAcquisitionContext: null));
    }

    [Fact]
    public void EconomyRuleset_BindsSelectedPricingPolicyAndParametersWithoutFallback()
    {
        ContentId configuredId = Id("test.pack:configured_economy");
        ContentId unsupportedId = Id("test.pack:unsupported_economy");
        var configured = new RulesetDefinition(
            configuredId,
            "Configured Economy",
            "Uses standard shop pricing.",
            RulesetCategory.Economy,
            StandardRulesetPolicyIds.StandardEconomy,
            Parameters(
                ("pricingPolicyId", StandardShopPricingPolicyIds.Standard.Value),
                ("pricingParameters", Parameters(("resalePercentage", 0.2m)))));
        var unsupported = new RulesetDefinition(
            unsupportedId,
            "Unsupported Economy",
            "Names an unavailable shop pricing policy.",
            RulesetCategory.Economy,
            StandardRulesetPolicyIds.StandardEconomy,
            Parameters(("pricingPolicyId", "missing_shop_pricing")));
        var catalog = new GameDataCatalog(
            skills: [],
            entities: [],
            races: [],
            ailments: [],
            items: [],
            rulesets:
            [
                KeyValuePair.Create(configured.Id, configured),
                KeyValuePair.Create(unsupported.Id, unsupported)
            ]);
        var resolver = new RuntimeRulesetBindingResolver(
            RuntimeRulesetPolicyFactoryRegistry.CreateStandard());

        ResourceManagementRulesetServices services = resolver
            .BindResourceManagementServices(catalog, configuredId)
            .RequireService();
        RulesetBindingResult<ResourceManagementRulesetServices> rejected =
            resolver.BindResourceManagementServices(catalog, unsupportedId);
        GameDataCatalog itemCatalog = ItemCatalog();
        RuntimeShopOfferSnapshot offer = services.ShopOffers
            .Resolve(
                TestShop,
                new ShopOfferDefinition(
                    Id("configured_offer"),
                    ShopContentKind.Item,
                    Medicine,
                    new FixedShopPriceDefinition(100),
                    new UnlimitedShopStockDefinition()),
                itemCatalog,
                itemCatalog)
            .RequireOffer();

        Assert.Equal(100, services.Shop.CalculateBuyPrice(offer, luck: 99));
        Assert.Equal(20, services.Shop.CalculateSellPrice(offer, luck: 99));
        Assert.False(rejected.IsSuccess);
        RulesetBindingDiagnostic diagnostic = Assert.Single(rejected.Diagnostics);
        Assert.Equal(RulesetBindingDiagnosticCode.UnsupportedPolicy, diagnostic.Code);
        Assert.Equal("pricingPolicyId", diagnostic.ParameterName);
    }

    [Fact]
    public void EconomyRuleset_UsesAHostRegisteredPricingFactoryForItsDefaultOffers()
    {
        ContentId customPolicyId = Id("host_shop_pricing");
        ContentId economyId = Id("test.pack:host_priced_economy");
        var economy = new RulesetDefinition(
            economyId,
            "Host-priced Economy",
            "Uses a host-registered shop pricing factory.",
            RulesetCategory.Economy,
            StandardRulesetPolicyIds.StandardEconomy,
            Parameters(("pricingPolicyId", customPolicyId.Value)));
        var catalog = new GameDataCatalog(
            skills: [],
            entities: [],
            races: [],
            ailments: [],
            items: [],
            rulesets: [KeyValuePair.Create(economy.Id, economy)]);
        var resolver = new RuntimeRulesetBindingResolver(
            RuntimeRulesetPolicyFactoryRegistry.CreateStandard(
                [new FixedPricingFactory(customPolicyId, purchasePrice: 12, resalePrice: 7)]));

        ResourceManagementRulesetServices services = resolver
            .BindResourceManagementServices(catalog, economyId)
            .RequireService();
        GameDataCatalog itemCatalog = ItemCatalog();
        RuntimeShopOfferSnapshot offer = services.ShopOffers
            .Resolve(
                TestShop,
                new ShopOfferDefinition(
                    Id("host_priced_offer"),
                    ShopContentKind.Item,
                    Medicine,
                    new FixedShopPriceDefinition(100),
                    new UnlimitedShopStockDefinition()),
                itemCatalog,
                itemCatalog)
            .RequireOffer();

        Assert.Equal(customPolicyId, offer.Pricing.Policy.PolicyId);
        Assert.Equal(12, services.Shop.CalculateBuyPrice(offer, luck: 0));
        Assert.Equal(7, services.Shop.CalculateSellPrice(offer, luck: 0));
    }

    [Fact]
    public void EconomyRuleset_ReportsMalformedPricingConfigurationWithPreciseParameterPaths()
    {
        (IReadOnlyDictionary<string, object?> Parameters, RulesetBindingDiagnosticCode Code, string Parameter)[] cases =
        [
            (EmptyParameters(), RulesetBindingDiagnosticCode.MissingParameter, "pricingPolicyId"),
            (Parameters(("pricingPolicyId", 42)), RulesetBindingDiagnosticCode.InvalidParameterType, "pricingPolicyId"),
            (Parameters(("pricingPolicyId", "test.pack:qualified")), RulesetBindingDiagnosticCode.InvalidIdentifier, "pricingPolicyId"),
            (Parameters(
                ("pricingPolicyId", StandardShopPricingPolicyIds.Standard.Value),
                ("pricingParameters", "not-an-object")),
                RulesetBindingDiagnosticCode.InvalidParameterType,
                "pricingParameters"),
            (Parameters(
                ("pricingPolicyId", StandardShopPricingPolicyIds.Standard.Value),
                ("pricingParameters", Parameters(("resalePercentage", -0.1m)))),
                RulesetBindingDiagnosticCode.InvalidParameterValue,
                "pricingParameters.resalePercentage"),
            (Parameters(
                ("pricingPolicyId", StandardShopPricingPolicyIds.Standard.Value),
                ("unexpected", true)),
                RulesetBindingDiagnosticCode.UnknownParameter,
                "unexpected")
        ];

        foreach ((IReadOnlyDictionary<string, object?> parameters, RulesetBindingDiagnosticCode code, string parameter) in cases)
        {
            ContentId rulesetId = Id(
                $"test.pack:invalid_economy_{parameter.Replace('.', '_')}_{code.ToString().ToLowerInvariant()}");
            var definition = new RulesetDefinition(
                rulesetId,
                "Invalid Economy",
                "Exercises typed pricing configuration diagnostics.",
                RulesetCategory.Economy,
                StandardRulesetPolicyIds.StandardEconomy,
                parameters);
            var catalog = new GameDataCatalog(
                skills: [],
                entities: [],
                races: [],
                ailments: [],
                items: [],
                rulesets: [KeyValuePair.Create(definition.Id, definition)]);

            RulesetBindingResult<ResourceManagementRulesetServices> result =
                new RuntimeRulesetBindingResolver(RuntimeRulesetPolicyFactoryRegistry.CreateStandard())
                    .BindResourceManagementServices(catalog, rulesetId);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == code && diagnostic.ParameterName == parameter);
        }
    }

    private static ShopPriceCalculationRequest Request(
        int authoredPurchasePrice,
        int luck,
        ShopPriceOperation operation) =>
        new(authoredPurchasePrice, luck, operation);

    private static RuntimeShopOfferSnapshot Offer(IShopPricingPolicy policy) =>
        new(
            new RuntimeShopOfferIdentity(TestShop, Id("medicine_offer")),
            ShopContentKind.Item,
            Medicine,
            new RuntimeShopPricingProfile(
                10,
                new BoundShopPricingPolicy(Id("host_pricing"), policy)),
            RuntimeShopStockProfile.Unlimited);

    private static GameDataCatalog ItemCatalog() =>
        new(
            skills: [],
            entities: [],
            races: [],
            ailments: [],
            items:
            [
                KeyValuePair.Create(
                    Medicine,
                    new ItemDefinition(
                        Medicine,
                        "Medicine",
                        "Test item.",
                        ItemKind.Consumable,
                        stackLimit: 10,
                        baseValue: 100))
            ]);

    private static IReadOnlyDictionary<string, object?> EmptyParameters() =>
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, object?> Parameters(
        params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static void AssertRejectedWithoutMutation(
        ShopTransactionResult result,
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot ledger,
        string messageFragment)
    {
        Assert.False(result.Applied);
        Assert.Equal(ResourceTransactionCode.InvalidShopPricing, result.Code);
        Assert.Same(inventory, result.BeforeInventory);
        Assert.Same(inventory, result.AfterInventory);
        Assert.Same(ledger, result.BeforeCurrencyLedger);
        Assert.Same(ledger, result.AfterCurrencyLedger);
        Assert.Contains(messageFragment, Assert.Single(result.Diagnostics).Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedPricingFactory(
        ContentId policyId,
        int purchasePrice,
        int resalePrice) : IShopPricingPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public ShopPricingPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters) =>
            new(new BoundShopPricingPolicy(PolicyId, new FixedPricingPolicy(purchasePrice, resalePrice)));
    }

    private sealed class FixedPricingPolicy(int purchasePrice, int resalePrice) : IShopPricingPolicy
    {
        public ShopPriceCalculationResult Calculate(ShopPriceCalculationRequest request) =>
            ShopPriceCalculationResult.Applied(
                request.Operation == ShopPriceOperation.Purchase ? purchasePrice : resalePrice);
    }

    private sealed class ThrowingPricingFactory(ContentId policyId) : IShopPricingPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public ShopPricingPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters) =>
            throw new InvalidOperationException("factory failure");
    }

    private sealed class CancelingPricingFactory(ContentId policyId) : IShopPricingPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public ShopPricingPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters) =>
            throw new OperationCanceledException("factory canceled");
    }

    private sealed class MismatchedPricingFactory(ContentId policyId) : IShopPricingPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public ShopPricingPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters) =>
            new(new BoundShopPricingPolicy(Id("different_pricing"), new FixedPricingPolicy(1, 1)));
    }

    private sealed class SilentRejectingPricingFactory(ContentId policyId) : IShopPricingPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public ShopPricingPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters) =>
            new(null);
    }

    private sealed class ThrowingPricingPolicy : IShopPricingPolicy
    {
        public ShopPriceCalculationResult Calculate(ShopPriceCalculationRequest request) =>
            throw new InvalidOperationException("calculation failure");
    }

    private sealed class NullPricingPolicy : IShopPricingPolicy
    {
        public ShopPriceCalculationResult Calculate(ShopPriceCalculationRequest request) => null!;
    }

    private sealed class CancelingPricingPolicy : IShopPricingPolicy
    {
        public ShopPriceCalculationResult Calculate(ShopPriceCalculationRequest request) =>
            throw new OperationCanceledException("calculation canceled");
    }
}
