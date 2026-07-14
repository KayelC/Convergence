using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
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
    MissingParameter,
    UnknownParameter,
    InvalidParameterType,
    InvalidParameterValue,
    InvalidIdentifier
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

public sealed record BattleTurnEconomyRuleset
{
    public BattleTurnEconomyRuleset(
        Func<IBattleTurnEconomy> createEconomy,
        BattlePhaseProgressPolicy phaseProgress)
    {
        CreateEconomy = createEconomy ?? throw new ArgumentNullException(nameof(createEconomy));
        PhaseProgress = phaseProgress ?? throw new ArgumentNullException(nameof(phaseProgress));
    }

    public Func<IBattleTurnEconomy> CreateEconomy { get; }
    public BattlePhaseProgressPolicy PhaseProgress { get; }
}

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

    RulesetBindingResult<BattleTurnEconomyRuleset> BindTurnEconomy(
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
            CreateStockCapacityPolicy);

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

    public RulesetBindingResult<BattleTurnEconomyRuleset> BindTurnEconomy(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<BattleTurnEconomyRuleset>(
            catalog,
            rulesetId,
            RulesetCategory.PressTurn,
            StandardRulesetPolicyIds.StandardPressTurn,
            (definition, diagnostics) =>
            {
                RequireNoParameters(definition, diagnostics);
                return new BattleTurnEconomyRuleset(
                    () => new PressTurnEngine(),
                    new BattlePhaseProgressPolicy(
                        maximumCommands: 256,
                        maximumConsecutiveFreeActions: 32));
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
        if (!rulesetId.IsValid)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.InvalidIdentifier,
                rulesetId,
                "Ruleset ID cannot be empty."));
            return new RulesetBindingResult<TService>(null, diagnostics);
        }

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

    private static IStockCapacityPolicy CreateStockCapacityPolicy(
        RulesetDefinition definition,
        List<RulesetBindingDiagnostic> diagnostics)
    {
        foreach (string key in definition.Parameters.Keys.Where(key => key != "tiers"))
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.UnknownParameter,
                definition.Id,
                $"Ruleset '{definition.Id}' parameter '{key}' is not supported by policy '{definition.PolicyId}'.",
                ParameterName: key,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }

        if (!definition.Parameters.TryGetValue("tiers", out object? value))
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.MissingParameter,
                definition.Id,
                $"Ruleset '{definition.Id}' requires a 'tiers' parameter.",
                ParameterName: "tiers",
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
            return NoLimitStockCapacityPolicy.Instance;
        }

        if (value is not IReadOnlyList<object?> authoredTiers || authoredTiers.Count == 0)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.InvalidParameterType,
                definition.Id,
                $"Ruleset '{definition.Id}' parameter 'tiers' must be a nonempty list.",
                ParameterName: "tiers",
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
            return NoLimitStockCapacityPolicy.Instance;
        }

        var tiers = new List<StockCapacityTier>();
        for (int index = 0; index < authoredTiers.Count; index++)
        {
            if (authoredTiers[index] is not IReadOnlyDictionary<string, object?> tier ||
                !TryReadInt(tier, "minimumLevel", out int minimumLevel) ||
                !TryReadInt(tier, "capacity", out int capacity) ||
                tier.Keys.Any(key => key is not ("minimumLevel" or "capacity")) ||
                minimumLevel <= 0 ||
                capacity < 0)
            {
                diagnostics.Add(new RulesetBindingDiagnostic(
                    RulesetBindingDiagnosticCode.InvalidParameterValue,
                    definition.Id,
                    $"Ruleset '{definition.Id}' stock-capacity tier {index} must contain only a positive 'minimumLevel' and a nonnegative 'capacity'.",
                    ParameterName: $"tiers[{index}]",
                    ActualCategory: definition.Category,
                    PolicyId: definition.PolicyId));
                continue;
            }

            tiers.Add(new StockCapacityTier(minimumLevel, capacity));
        }

        if (diagnostics.Count > 0)
        {
            return NoLimitStockCapacityPolicy.Instance;
        }

        try
        {
            return new TieredStockCapacityPolicy(tiers);
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.InvalidParameterValue,
                definition.Id,
                exception.Message,
                ParameterName: "tiers",
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
            return NoLimitStockCapacityPolicy.Instance;
        }
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out int result)
    {
        result = 0;
        if (!values.TryGetValue(key, out object? value))
        {
            return false;
        }

        return value switch
        {
            int intValue => Assign(intValue, out result),
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => Assign((int)longValue, out result),
            decimal decimalValue when decimalValue == decimal.Truncate(decimalValue) &&
                                      decimalValue is >= int.MinValue and <= int.MaxValue => Assign((int)decimalValue, out result),
            _ => false
        };
    }

    private static bool Assign(int value, out int destination)
    {
        destination = value;
        return true;
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
