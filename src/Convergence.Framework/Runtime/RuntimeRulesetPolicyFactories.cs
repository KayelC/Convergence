using Convergence.Battle;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.TurnEconomy;

namespace Convergence.Runtime;

public interface IRuntimeDamageRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<ProductionCombatRuleset> Create(
        RulesetDefinition definition,
        IRandomSource random);
}

public interface IRuntimeRewardRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<IBattleRewardService> Create(
        RulesetDefinition definition,
        ProductionCombatRuleset combatRuleset);
}

public interface IRuntimeStatRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<IStatResolutionPolicy> Create(RulesetDefinition definition);
}

public interface IRuntimeGrowthRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<GrowthRulesetServices> Create(RulesetDefinition definition);
}

public interface IRuntimeRosterCapacityRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<IRosterCapacityPolicy> Create(RulesetDefinition definition);
}

public interface IRuntimeEconomyRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<ResourceManagementRulesetServices> Create(RulesetDefinition definition);
}

public interface IRuntimeTurnEconomyRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<BattleTurnEconomyRuleset> Create(RulesetDefinition definition);
}

/// <summary>
/// Host-supplied map from authored policy IDs to typed runtime factories.
/// Categories remain separate so a policy cannot be resolved as the wrong
/// service type. Moon phase is intentionally not a built-in runtime category.
/// </summary>
public sealed class RuntimeRulesetPolicyFactoryRegistry
{
    private readonly IReadOnlyDictionary<ContentId, IRuntimeDamageRulesetPolicyFactory> _damage;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeRewardRulesetPolicyFactory> _reward;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeStatRulesetPolicyFactory> _stat;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeGrowthRulesetPolicyFactory> _growth;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeRosterCapacityRulesetPolicyFactory> _rosterCapacity;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeEconomyRulesetPolicyFactory> _economy;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeTurnEconomyRulesetPolicyFactory> _turnEconomy;

    public RuntimeRulesetPolicyFactoryRegistry(
        IEnumerable<IRuntimeDamageRulesetPolicyFactory>? damage = null,
        IEnumerable<IRuntimeRewardRulesetPolicyFactory>? reward = null,
        IEnumerable<IRuntimeStatRulesetPolicyFactory>? stat = null,
        IEnumerable<IRuntimeGrowthRulesetPolicyFactory>? growth = null,
        IEnumerable<IRuntimeRosterCapacityRulesetPolicyFactory>? rosterCapacity = null,
        IEnumerable<IRuntimeEconomyRulesetPolicyFactory>? economy = null,
        IEnumerable<IRuntimeTurnEconomyRulesetPolicyFactory>? turnEconomy = null)
    {
        _damage = Snapshot(damage, factory => factory.PolicyId, nameof(damage));
        _reward = Snapshot(reward, factory => factory.PolicyId, nameof(reward));
        _stat = Snapshot(stat, factory => factory.PolicyId, nameof(stat));
        _growth = Snapshot(growth, factory => factory.PolicyId, nameof(growth));
        _rosterCapacity = Snapshot(rosterCapacity, factory => factory.PolicyId, nameof(rosterCapacity));
        _economy = Snapshot(economy, factory => factory.PolicyId, nameof(economy));
        _turnEconomy = Snapshot(turnEconomy, factory => factory.PolicyId, nameof(turnEconomy));
    }

    public IReadOnlyCollection<ContentId> DamagePolicyIds => SnapshotIds(_damage);
    public IReadOnlyCollection<ContentId> RewardPolicyIds => SnapshotIds(_reward);
    public IReadOnlyCollection<ContentId> StatPolicyIds => SnapshotIds(_stat);
    public IReadOnlyCollection<ContentId> GrowthPolicyIds => SnapshotIds(_growth);
    public IReadOnlyCollection<ContentId> RosterCapacityPolicyIds => SnapshotIds(_rosterCapacity);
    public IReadOnlyCollection<ContentId> EconomyPolicyIds => SnapshotIds(_economy);
    public IReadOnlyCollection<ContentId> TurnEconomyPolicyIds => SnapshotIds(_turnEconomy);

    public static RuntimeRulesetPolicyFactoryRegistry CreateStandard() =>
        new(
            damage: [new StandardDamageRulesetPolicyFactory()],
            reward: [new StandardRewardRulesetPolicyFactory()],
            stat: [new StandardStatRulesetPolicyFactory()],
            growth: [new StandardGrowthRulesetPolicyFactory()],
            rosterCapacity: [new StandardRosterCapacityRulesetPolicyFactory()],
            economy: [new StandardEconomyRulesetPolicyFactory()],
            turnEconomy: [new StandardActionTokenRulesetPolicyFactory()]);

    internal IRuntimeDamageRulesetPolicyFactory? FindDamage(ContentId policyId) =>
        Find(_damage, policyId);

    internal IRuntimeRewardRulesetPolicyFactory? FindReward(ContentId policyId) =>
        Find(_reward, policyId);

    internal IRuntimeStatRulesetPolicyFactory? FindStat(ContentId policyId) =>
        Find(_stat, policyId);

    internal IRuntimeGrowthRulesetPolicyFactory? FindGrowth(ContentId policyId) =>
        Find(_growth, policyId);

    internal IRuntimeRosterCapacityRulesetPolicyFactory? FindRosterCapacity(ContentId policyId) =>
        Find(_rosterCapacity, policyId);

    internal IRuntimeEconomyRulesetPolicyFactory? FindEconomy(ContentId policyId) =>
        Find(_economy, policyId);

    internal IRuntimeTurnEconomyRulesetPolicyFactory? FindTurnEconomy(ContentId policyId) =>
        Find(_turnEconomy, policyId);

    private static IReadOnlyDictionary<ContentId, TFactory> Snapshot<TFactory>(
        IEnumerable<TFactory>? factories,
        Func<TFactory, ContentId> getPolicyId,
        string parameterName)
        where TFactory : class
    {
        var result = new Dictionary<ContentId, TFactory>();
        foreach (TFactory factory in factories ?? [])
        {
            ArgumentNullException.ThrowIfNull(factory);
            ContentId policyId = getPolicyId(factory);
            if (!policyId.IsValid || policyId.IsQualified)
            {
                throw new ArgumentException(
                    "Ruleset policy factory IDs must be valid unqualified IDs.",
                    parameterName);
            }

            if (!result.TryAdd(policyId, factory))
            {
                throw new ArgumentException(
                    $"Ruleset policy factory ID '{policyId}' is registered more than once for one category.",
                    parameterName);
            }
        }

        return new System.Collections.ObjectModel.ReadOnlyDictionary<ContentId, TFactory>(result);
    }

    private static IReadOnlyCollection<ContentId> SnapshotIds<TFactory>(
        IReadOnlyDictionary<ContentId, TFactory> factories) =>
        Array.AsReadOnly(factories.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray());

    private static TFactory? Find<TFactory>(
        IReadOnlyDictionary<ContentId, TFactory> factories,
        ContentId policyId)
        where TFactory : class =>
        factories.TryGetValue(policyId, out TFactory? factory) ? factory : null;
}

internal sealed class StandardDamageRulesetPolicyFactory : IRuntimeDamageRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardDamage;

    public RulesetBindingResult<ProductionCombatRuleset> Create(
        RulesetDefinition definition,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(random);

        var diagnostics = new List<RulesetBindingDiagnostic>();
        var config = new ProductionCombatRulesetConfig();
        foreach ((string key, object? value) in definition.Parameters)
        {
            switch (key)
            {
                case "damageFormulaScalar":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { DamageFormulaScalar = parsed });
                    break;
                case "damageVarianceMinimum":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { DamageVarianceMinimum = parsed });
                    break;
                case "damageVarianceMaximum":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { DamageVarianceMaximum = parsed });
                    break;
                case "chargeMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { ChargeMultiplier = parsed });
                    break;
                case "criticalDamageMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CriticalDamageMultiplier = parsed });
                    break;
                case "weakDamageMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { WeakDamageMultiplier = parsed });
                    break;
                case "resistDamageMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { ResistDamageMultiplier = parsed });
                    break;
                case "guardDamageMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { GuardDamageMultiplier = parsed });
                    break;
                case "defaultHitAccuracy":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { DefaultHitAccuracy = parsed });
                    break;
                case "hitChanceMinimum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { HitChanceMinimum = parsed });
                    break;
                case "hitChanceMaximum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { HitChanceMaximum = parsed });
                    break;
                case "criticalChanceMinimum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CriticalChanceMinimum = parsed });
                    break;
                case "criticalChanceMaximum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CriticalChanceMaximum = parsed });
                    break;
                case "criticalChanceBase":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CriticalChanceBase = parsed });
                    break;
                case "instantDeathChanceMinimum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathChanceMinimum = parsed });
                    break;
                case "instantDeathChanceMaximum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathChanceMaximum = parsed });
                    break;
                case "defaultInstantDeathChance":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { DefaultInstantDeathChance = parsed });
                    break;
                case "enemiesPerLevelForExperience":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { EnemiesPerLevelForExperience = parsed });
                    break;
                case "expectedStatLevelMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { ExpectedStatLevelMultiplier = parsed });
                    break;
                case "expectedStatBase":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { ExpectedStatBase = parsed });
                    break;
                case "statDensityDivisor":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { StatDensityDivisor = parsed });
                    break;
                case "maximumStatDensityMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { MaximumStatDensityMultiplier = parsed });
                    break;
                case "currencyBaseMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CurrencyBaseMultiplier = parsed });
                    break;
                case "currencyLuckMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CurrencyLuckMultiplier = parsed });
                    break;
                case "currencyVarianceMinimum":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CurrencyVarianceMinimum = parsed });
                    break;
                case "currencyVarianceMaximum":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { CurrencyVarianceMaximum = parsed });
                    break;
                case "initiativeVarianceMinimum":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InitiativeVarianceMinimum = parsed });
                    break;
                case "initiativeVarianceMaximum":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InitiativeVarianceMaximum = parsed });
                    break;
                default:
                    RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
                    break;
            }
        }

        try
        {
            config.Validate();
        }
        catch (ArgumentException exception)
        {
            RulesetPolicyFactoryDiagnostics.InvalidConfiguration(definition, exception.Message, diagnostics);
        }

        if (diagnostics.Count > 0)
        {
            return new RulesetBindingResult<ProductionCombatRuleset>(null, diagnostics);
        }

        try
        {
            return new RulesetBindingResult<ProductionCombatRuleset>(
                new ProductionCombatRuleset(random, config));
        }
        catch (ArgumentException exception)
        {
            RulesetPolicyFactoryDiagnostics.InvalidConfiguration(definition, exception.Message, diagnostics);
            return new RulesetBindingResult<ProductionCombatRuleset>(null, diagnostics);
        }
    }

    private static ProductionCombatRulesetConfig ReadDecimal(
        RulesetDefinition definition,
        string key,
        object? value,
        List<RulesetBindingDiagnostic> diagnostics,
        ProductionCombatRulesetConfig config,
        Func<ProductionCombatRulesetConfig, decimal, ProductionCombatRulesetConfig> apply)
    {
        if (!RulesetPolicyFactoryParameters.TryReadDecimal(value, out decimal parsed))
        {
            RulesetPolicyFactoryDiagnostics.InvalidType(definition, key, "numeric", diagnostics);
            return config;
        }

        return apply(config, parsed);
    }

    private static ProductionCombatRulesetConfig ReadInt(
        RulesetDefinition definition,
        string key,
        object? value,
        List<RulesetBindingDiagnostic> diagnostics,
        ProductionCombatRulesetConfig config,
        Func<ProductionCombatRulesetConfig, int, ProductionCombatRulesetConfig> apply)
    {
        if (!RulesetPolicyFactoryParameters.TryReadInt(value, out int parsed))
        {
            RulesetPolicyFactoryDiagnostics.InvalidType(definition, key, "integer", diagnostics);
            return config;
        }

        return apply(config, parsed);
    }
}

internal sealed class StandardRewardRulesetPolicyFactory : IRuntimeRewardRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardReward;

    public RulesetBindingResult<IBattleRewardService> Create(
        RulesetDefinition definition,
        ProductionCombatRuleset combatRuleset)
    {
        ArgumentNullException.ThrowIfNull(combatRuleset);
        List<RulesetBindingDiagnostic> diagnostics = RulesetPolicyFactoryDiagnostics.RequireNoParameters(definition);
        return diagnostics.Count == 0
            ? new RulesetBindingResult<IBattleRewardService>(new BattleRewardService(combatRuleset))
            : new RulesetBindingResult<IBattleRewardService>(null, diagnostics);
    }
}

internal sealed class StandardStatRulesetPolicyFactory : IRuntimeStatRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardStat;

    public RulesetBindingResult<IStatResolutionPolicy> Create(RulesetDefinition definition)
    {
        List<RulesetBindingDiagnostic> diagnostics = RulesetPolicyFactoryDiagnostics.RequireNoParameters(definition);
        return diagnostics.Count == 0
            ? new RulesetBindingResult<IStatResolutionPolicy>(new StandardStatResolutionPolicy())
            : new RulesetBindingResult<IStatResolutionPolicy>(null, diagnostics);
    }
}

internal sealed class StandardGrowthRulesetPolicyFactory : IRuntimeGrowthRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardGrowth;

    public RulesetBindingResult<GrowthRulesetServices> Create(RulesetDefinition definition)
    {
        List<RulesetBindingDiagnostic> diagnostics = RulesetPolicyFactoryDiagnostics.RequireNoParameters(definition);
        if (diagnostics.Count > 0)
        {
            return new RulesetBindingResult<GrowthRulesetServices>(null, diagnostics);
        }

        var resourceGrowth = new StandardResourceGrowthPolicy();
        var experienceCurve = new CubicExperienceCurve();
        return new RulesetBindingResult<GrowthRulesetServices>(new GrowthRulesetServices(
            resourceGrowth,
            experienceCurve,
            new StandardLevelGrowthPolicy(experienceCurve, resourceGrowth),
            new StatAllocationService(resourceGrowth)));
    }
}

internal sealed class StandardRosterCapacityRulesetPolicyFactory : IRuntimeRosterCapacityRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardRosterCapacity;

    public RulesetBindingResult<IRosterCapacityPolicy> Create(RulesetDefinition definition)
    {
        var diagnostics = new List<RulesetBindingDiagnostic>();
        foreach (string key in definition.Parameters.Keys.Where(key => key != "tiers"))
        {
            RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
        }

        if (!definition.Parameters.TryGetValue("tiers", out object? value))
        {
            RulesetPolicyFactoryDiagnostics.MissingParameter(definition, "tiers", diagnostics);
            return new RulesetBindingResult<IRosterCapacityPolicy>(null, diagnostics);
        }

        if (value is not IReadOnlyList<object?> authoredTiers || authoredTiers.Count == 0)
        {
            RulesetPolicyFactoryDiagnostics.InvalidType(definition, "tiers", "nonempty list", diagnostics);
            return new RulesetBindingResult<IRosterCapacityPolicy>(null, diagnostics);
        }

        var tiers = new List<RosterCapacityTier>();
        for (int index = 0; index < authoredTiers.Count; index++)
        {
            if (authoredTiers[index] is not IReadOnlyDictionary<string, object?> tier ||
                !TryReadRosterKind(tier, "rosterKind", out RuntimeRosterKind rosterKind) ||
                !RulesetPolicyFactoryParameters.TryReadInt(tier, "minimumLevel", out int minimumLevel) ||
                !RulesetPolicyFactoryParameters.TryReadInt(tier, "capacity", out int capacity) ||
                tier.Keys.Any(key => key is not ("rosterKind" or "minimumLevel" or "capacity")) ||
                minimumLevel <= 0 ||
                capacity < 0)
            {
                diagnostics.Add(RulesetPolicyFactoryDiagnostics.Create(
                    definition,
                    RulesetBindingDiagnosticCode.InvalidParameterValue,
                    $"Ruleset '{definition.Id}' roster-capacity tier {index} must contain a supported 'rosterKind', a positive 'minimumLevel', and a nonnegative 'capacity'.",
                    $"tiers[{index}]"));
                continue;
            }

            tiers.Add(new RosterCapacityTier(rosterKind, minimumLevel, capacity));
        }

        if (diagnostics.Count > 0)
        {
            return new RulesetBindingResult<IRosterCapacityPolicy>(null, diagnostics);
        }

        try
        {
            return new RulesetBindingResult<IRosterCapacityPolicy>(new TieredRosterCapacityPolicy(tiers));
        }
        catch (ArgumentException exception)
        {
            RulesetPolicyFactoryDiagnostics.InvalidConfiguration(definition, exception.Message, diagnostics, "tiers");
            return new RulesetBindingResult<IRosterCapacityPolicy>(null, diagnostics);
        }
    }

    private static bool TryReadRosterKind(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out RuntimeRosterKind rosterKind)
    {
        rosterKind = default;
        if (!values.TryGetValue(key, out object? value) || value is not string text)
        {
            return false;
        }

        rosterKind = text switch
        {
            "hosted_entity" => RuntimeRosterKind.HostedEntity,
            "companion" => RuntimeRosterKind.Companion,
            _ => default
        };
        return text is "hosted_entity" or "companion";
    }
}

internal sealed class StandardEconomyRulesetPolicyFactory : IRuntimeEconomyRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardEconomy;

    public RulesetBindingResult<ResourceManagementRulesetServices> Create(RulesetDefinition definition)
    {
        List<RulesetBindingDiagnostic> diagnostics = RulesetPolicyFactoryDiagnostics.RequireNoParameters(definition);
        if (diagnostics.Count > 0)
        {
            return new RulesetBindingResult<ResourceManagementRulesetServices>(null, diagnostics);
        }

        var inventory = new InventoryTransitionService();
        var equipment = new EquipmentTransitionService();
        var economy = new EconomyTransactionService();
        return new RulesetBindingResult<ResourceManagementRulesetServices>(new ResourceManagementRulesetServices(
            inventory,
            equipment,
            economy,
            new ShopTransactionService(inventory, economy),
            new HospitalRestorationService(economy)));
    }
}

internal sealed class StandardActionTokenRulesetPolicyFactory : IRuntimeTurnEconomyRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardActionToken;

    public RulesetBindingResult<BattleTurnEconomyRuleset> Create(RulesetDefinition definition)
    {
        var diagnostics = new List<RulesetBindingDiagnostic>();
        foreach (string key in definition.Parameters.Keys.Where(
                     key => key is not ("maximumCommands" or "maximumConsecutiveFreeActions")))
        {
            RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
        }

        bool hasMaximumCommands = TryReadRequiredInt(
            definition,
            "maximumCommands",
            diagnostics,
            out int maximumCommands);
        bool hasFreeActionLimit = TryReadRequiredInt(
            definition,
            "maximumConsecutiveFreeActions",
            diagnostics,
            out int maximumConsecutiveFreeActions);
        if (!hasMaximumCommands || !hasFreeActionLimit || diagnostics.Count > 0)
        {
            return new RulesetBindingResult<BattleTurnEconomyRuleset>(null, diagnostics);
        }

        try
        {
            return new RulesetBindingResult<BattleTurnEconomyRuleset>(new BattleTurnEconomyRuleset(
                () => new ActionTokenTurnEconomy(),
                new BattlePhaseProgressPolicy(maximumCommands, maximumConsecutiveFreeActions)));
        }
        catch (ArgumentException exception)
        {
            RulesetPolicyFactoryDiagnostics.InvalidConfiguration(definition, exception.Message, diagnostics);
            return new RulesetBindingResult<BattleTurnEconomyRuleset>(null, diagnostics);
        }
    }

    private static bool TryReadRequiredInt(
        RulesetDefinition definition,
        string key,
        List<RulesetBindingDiagnostic> diagnostics,
        out int value)
    {
        value = 0;
        if (!definition.Parameters.TryGetValue(key, out object? raw))
        {
            RulesetPolicyFactoryDiagnostics.MissingParameter(definition, key, diagnostics);
            return false;
        }

        if (!RulesetPolicyFactoryParameters.TryReadInt(raw, out value))
        {
            RulesetPolicyFactoryDiagnostics.InvalidType(definition, key, "integer", diagnostics);
            return false;
        }

        return true;
    }
}

internal static class RulesetPolicyFactoryParameters
{
    public static bool TryReadInt(object? value, out int result)
    {
        result = 0;
        return value switch
        {
            int intValue => Assign(intValue, out result),
            long longValue when longValue is >= int.MinValue and <= int.MaxValue =>
                Assign((int)longValue, out result),
            decimal decimalValue when decimalValue == decimal.Truncate(decimalValue) &&
                                      decimalValue is >= int.MinValue and <= int.MaxValue =>
                Assign((int)decimalValue, out result),
            _ => false
        };
    }

    public static bool TryReadInt(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out int result)
    {
        result = 0;
        return values.TryGetValue(key, out object? value) && TryReadInt(value, out result);
    }

    public static bool TryReadDecimal(object? value, out decimal number)
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

    private static bool Assign(int value, out int destination)
    {
        destination = value;
        return true;
    }
}

internal static class RulesetPolicyFactoryDiagnostics
{
    public static List<RulesetBindingDiagnostic> RequireNoParameters(RulesetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var diagnostics = new List<RulesetBindingDiagnostic>();
        foreach (string key in definition.Parameters.Keys)
        {
            UnknownParameter(definition, key, diagnostics);
        }

        return diagnostics;
    }

    public static void MissingParameter(
        RulesetDefinition definition,
        string key,
        ICollection<RulesetBindingDiagnostic> diagnostics) =>
        diagnostics.Add(Create(
            definition,
            RulesetBindingDiagnosticCode.MissingParameter,
            $"Ruleset '{definition.Id}' requires a '{key}' parameter.",
            key));

    public static void UnknownParameter(
        RulesetDefinition definition,
        string key,
        ICollection<RulesetBindingDiagnostic> diagnostics) =>
        diagnostics.Add(Create(
            definition,
            RulesetBindingDiagnosticCode.UnknownParameter,
            $"Ruleset '{definition.Id}' parameter '{key}' is not supported by policy '{definition.PolicyId}'.",
            key));

    public static void InvalidType(
        RulesetDefinition definition,
        string key,
        string expected,
        ICollection<RulesetBindingDiagnostic> diagnostics) =>
        diagnostics.Add(Create(
            definition,
            RulesetBindingDiagnosticCode.InvalidParameterType,
            $"Ruleset '{definition.Id}' parameter '{key}' must be {expected}.",
            key));

    public static void InvalidConfiguration(
        RulesetDefinition definition,
        string message,
        ICollection<RulesetBindingDiagnostic> diagnostics,
        string? parameterName = null) =>
        diagnostics.Add(Create(
            definition,
            RulesetBindingDiagnosticCode.InvalidParameterValue,
            $"Ruleset '{definition.Id}' configuration is invalid: {message}",
            parameterName));

    public static RulesetBindingDiagnostic Create(
        RulesetDefinition definition,
        RulesetBindingDiagnosticCode code,
        string message,
        string? parameterName = null) =>
        new(
            code,
            definition.Id,
            message,
            parameterName,
            ActualCategory: definition.Category,
            PolicyId: definition.PolicyId);
}
