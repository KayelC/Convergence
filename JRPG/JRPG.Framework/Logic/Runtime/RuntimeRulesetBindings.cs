using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Runtime;

namespace JRPGPrototype.Logic.Runtime;

public static class StandardRulesetPolicyIds
{
    public static ContentId StandardDamage { get; } = ContentId.Parse("standard_damage");
    public static ContentId StandardReward { get; } = ContentId.Parse("standard_reward");
    public static ContentId StandardGrowth { get; } = ContentId.Parse("standard_growth");
    public static ContentId StandardStat { get; } = ContentId.Parse("standard_stat");
    public static ContentId StandardPressTurn { get; } = ContentId.Parse("standard_press_turn");
    public static ContentId StandardStockCapacity { get; } = ContentId.Parse("standard_stock_capacity");
    public static ContentId StandardEconomy { get; } = ContentId.Parse("standard_economy");
    public static ContentId StandardMoonPhase { get; } = ContentId.Parse("standard_moon_phase");
}

public enum RulesetBindingDiagnosticCode
{
    MissingRuleset,
    CategoryMismatch,
    UnsupportedPolicy,
    UnknownParameter,
    InvalidParameterType,
    InvalidParameterValue
}

public sealed record RulesetBindingDiagnostic(
    RulesetBindingDiagnosticCode Code,
    ContentId RulesetId,
    string Message,
    string? ParameterName = null,
    RulesetCategory? ExpectedCategory = null,
    RulesetCategory? ActualCategory = null,
    ContentId? PolicyId = null);

public sealed record RulesetBindingResult<TService>
    where TService : class
{
    public RulesetBindingResult(
        TService? service,
        IEnumerable<RulesetBindingDiagnostic>? diagnostics = null)
    {
        Service = service;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public TService? Service { get; }
    public IReadOnlyList<RulesetBindingDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Service is not null && Diagnostics.Count == 0;

    public TService RequireService() =>
        IsSuccess && Service is not null
            ? Service
            : throw new InvalidOperationException(
                "Ruleset binding failed: " + string.Join("; ", Diagnostics.Select(diagnostic => diagnostic.Message)));
}

public sealed record GrowthRulesetServices(
    IResourceGrowthPolicy ResourceGrowthPolicy,
    IExperienceCurve ExperienceCurve,
    ILevelGrowthPolicy LevelGrowthPolicy,
    IStatAllocationService StatAllocationService);

public sealed record ResourceManagementRulesetServices(
    IInventoryTransitionService Inventory,
    IEquipmentTransitionService Equipment,
    IEconomyTransactionService Economy,
    IShopTransactionService Shop,
    IHospitalRestorationService Hospital);

public interface IRuntimeRulesetBindingResolver
{
    RulesetBindingResult<ProductionCombatRuleset> BindProductionCombatRuleset(
        GameDataCatalog catalog,
        ContentId rulesetId,
        IRandomSource random);

    RulesetBindingResult<IBattleRewardService> BindBattleRewardService(
        GameDataCatalog catalog,
        ContentId rulesetId,
        ProductionCombatRuleset combatRuleset);

    RulesetBindingResult<IStatResolutionPolicy> BindStatResolutionPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<GrowthRulesetServices> BindGrowthServices(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<IStockCapacityPolicy> BindStockCapacityPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<ResourceManagementRulesetServices> BindResourceManagementServices(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<Func<PressTurnEngine>> BindPressTurnFactory(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<RulesetDefinition> BindMoonPhaseRuleset(
        GameDataCatalog catalog,
        ContentId rulesetId);
}

public sealed class RuntimeRulesetBindingResolver : IRuntimeRulesetBindingResolver
{
    public RulesetBindingResult<ProductionCombatRuleset> BindProductionCombatRuleset(
        GameDataCatalog catalog,
        ContentId rulesetId,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Bind(
            catalog,
            rulesetId,
            RulesetCategory.Damage,
            StandardRulesetPolicyIds.StandardDamage,
            (definition, diagnostics) =>
                new ProductionCombatRuleset(random, CreateCombatConfig(definition, diagnostics)));
    }

    public RulesetBindingResult<IBattleRewardService> BindBattleRewardService(
        GameDataCatalog catalog,
        ContentId rulesetId,
        ProductionCombatRuleset combatRuleset)
    {
        ArgumentNullException.ThrowIfNull(combatRuleset);
        return Bind<IBattleRewardService>(
            catalog,
            rulesetId,
            RulesetCategory.Reward,
            StandardRulesetPolicyIds.StandardReward,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                return new BattleRewardService(combatRuleset);
            });
    }

    public RulesetBindingResult<IStatResolutionPolicy> BindStatResolutionPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IStatResolutionPolicy>(
            catalog,
            rulesetId,
            RulesetCategory.Stat,
            StandardRulesetPolicyIds.StandardStat,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                return new StandardStatResolutionPolicy();
            });

    public RulesetBindingResult<GrowthRulesetServices> BindGrowthServices(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<GrowthRulesetServices>(
            catalog,
            rulesetId,
            RulesetCategory.Growth,
            StandardRulesetPolicyIds.StandardGrowth,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                var resourceGrowth = new StandardResourceGrowthPolicy();
                var experienceCurve = new CubicExperienceCurve();
                return new GrowthRulesetServices(
                    resourceGrowth,
                    experienceCurve,
                    new StandardLevelGrowthPolicy(experienceCurve, resourceGrowth),
                    new StatAllocationService(resourceGrowth));
            });

    public RulesetBindingResult<IStockCapacityPolicy> BindStockCapacityPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IStockCapacityPolicy>(
            catalog,
            rulesetId,
            RulesetCategory.StockCapacity,
            StandardRulesetPolicyIds.StandardStockCapacity,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                return new LegacyStockCapacityPolicy();
            });

    public RulesetBindingResult<ResourceManagementRulesetServices> BindResourceManagementServices(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<ResourceManagementRulesetServices>(
            catalog,
            rulesetId,
            RulesetCategory.Economy,
            StandardRulesetPolicyIds.StandardEconomy,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                var inventory = new InventoryTransitionService();
                var equipment = new EquipmentTransitionService();
                var economy = new EconomyTransactionService();
                return new ResourceManagementRulesetServices(
                    inventory,
                    equipment,
                    economy,
                    new ShopTransactionService(inventory, economy),
                    new HospitalRestorationService(economy));
            });

    public RulesetBindingResult<Func<PressTurnEngine>> BindPressTurnFactory(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<Func<PressTurnEngine>>(
            catalog,
            rulesetId,
            RulesetCategory.PressTurn,
            StandardRulesetPolicyIds.StandardPressTurn,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                return () => new PressTurnEngine();
            });

    public RulesetBindingResult<RulesetDefinition> BindMoonPhaseRuleset(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind(
            catalog,
            rulesetId,
            RulesetCategory.MoonPhase,
            StandardRulesetPolicyIds.StandardMoonPhase,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                return definition;
            });

    private static RulesetBindingResult<TService> Bind<TService>(
        GameDataCatalog catalog,
        ContentId rulesetId,
        RulesetCategory expectedCategory,
        ContentId expectedPolicyId,
        Func<RulesetDefinition, List<RulesetBindingDiagnostic>, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factory);

        var diagnostics = new List<RulesetBindingDiagnostic>();
        if (!catalog.TryGetRuleset(rulesetId, out RulesetDefinition? definition) || definition is null)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.MissingRuleset,
                rulesetId,
                $"Ruleset '{rulesetId}' was not found."));
            return new RulesetBindingResult<TService>(null, diagnostics);
        }

        if (definition.Category != expectedCategory)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.CategoryMismatch,
                rulesetId,
                $"Ruleset '{rulesetId}' has category '{definition.Category}', but '{expectedCategory}' was required.",
                ExpectedCategory: expectedCategory,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }

        if (definition.PolicyId != expectedPolicyId)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.UnsupportedPolicy,
                rulesetId,
                $"Ruleset '{rulesetId}' uses unsupported policy '{definition.PolicyId}'. Expected '{expectedPolicyId}'.",
                ExpectedCategory: expectedCategory,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }

        if (diagnostics.Count > 0)
        {
            return new RulesetBindingResult<TService>(null, diagnostics);
        }

        TService service = factory(definition, diagnostics);
        return diagnostics.Count == 0
            ? new RulesetBindingResult<TService>(service)
            : new RulesetBindingResult<TService>(null, diagnostics);
    }

    private static ProductionCombatRulesetConfig CreateCombatConfig(
        RulesetDefinition definition,
        List<RulesetBindingDiagnostic> diagnostics)
    {
        var config = new ProductionCombatRulesetConfig();
        foreach ((string key, object? value) in definition.Parameters)
        {
            switch (key)
            {
                case "weakMultiplier":
                    if (TryReadPositiveDecimal(definition, key, value, diagnostics, out decimal weak))
                    {
                        config = config with { WeakDamageMultiplier = weak };
                    }
                    break;
                case "resistMultiplier":
                    if (TryReadPositiveDecimal(definition, key, value, diagnostics, out decimal resist))
                    {
                        config = config with { ResistDamageMultiplier = resist };
                    }
                    break;
                default:
                    diagnostics.Add(new RulesetBindingDiagnostic(
                        RulesetBindingDiagnosticCode.UnknownParameter,
                        definition.Id,
                        $"Ruleset '{definition.Id}' parameter '{key}' is not supported by policy '{definition.PolicyId}'.",
                        ParameterName: key,
                        ActualCategory: definition.Category,
                        PolicyId: definition.PolicyId));
                    break;
            }
        }

        return config;
    }

    private static void RequireNoParameters(
        RulesetDefinition definition,
        List<RulesetBindingDiagnostic> diagnostics)
    {
        foreach (string key in definition.Parameters.Keys)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.UnknownParameter,
                definition.Id,
                $"Ruleset '{definition.Id}' parameter '{key}' is not supported by policy '{definition.PolicyId}'.",
                ParameterName: key,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }
    }

    private static bool TryReadPositiveDecimal(
        RulesetDefinition definition,
        string key,
        object? value,
        List<RulesetBindingDiagnostic> diagnostics,
        out decimal number)
    {
        if (!TryReadDecimal(value, out number))
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.InvalidParameterType,
                definition.Id,
                $"Ruleset '{definition.Id}' parameter '{key}' must be numeric.",
                ParameterName: key,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
            return false;
        }

        if (number <= 0m)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.InvalidParameterValue,
                definition.Id,
                $"Ruleset '{definition.Id}' parameter '{key}' must be positive.",
                ParameterName: key,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
            return false;
        }

        return true;
    }

    private static bool TryReadDecimal(object? value, out decimal number)
    {
        switch (value)
        {
            case decimal decimalValue:
                number = decimalValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case int intValue:
                number = intValue;
                return true;
            default:
                number = 0m;
                return false;
        }
    }
}
