using Convergence.Battle;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.TurnEconomy;

namespace Convergence.Runtime;

public interface IRuntimeCombatRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<CombatExecutionPolicySet> Create(
        RulesetDefinition definition,
        IRandomSource random,
        IStatStageScalingPolicy stageScalingPolicy);
}

public interface IRuntimeRewardRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<IBattleRewardService> Create(
        RulesetDefinition definition,
        IRandomSource random);
}

public interface IRuntimeStatRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<StatRulesetServices> Create(RulesetDefinition definition);
}

public interface IRuntimeStatModifierRulesetPolicyFactory
{
    ContentId PolicyId { get; }

    RulesetBindingResult<IStatModifierPolicyService> Create(RulesetDefinition definition);
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
    private readonly IReadOnlyDictionary<ContentId, IRuntimeCombatRulesetPolicyFactory> _combat;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeRewardRulesetPolicyFactory> _reward;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeStatRulesetPolicyFactory> _stat;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeStatModifierRulesetPolicyFactory> _statModifier;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeGrowthRulesetPolicyFactory> _growth;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeRosterCapacityRulesetPolicyFactory> _rosterCapacity;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeEconomyRulesetPolicyFactory> _economy;
    private readonly IReadOnlyDictionary<ContentId, IRuntimeTurnEconomyRulesetPolicyFactory> _turnEconomy;

    public RuntimeRulesetPolicyFactoryRegistry(
        IEnumerable<IRuntimeCombatRulesetPolicyFactory>? combat = null,
        IEnumerable<IRuntimeRewardRulesetPolicyFactory>? reward = null,
        IEnumerable<IRuntimeStatRulesetPolicyFactory>? stat = null,
        IEnumerable<IRuntimeGrowthRulesetPolicyFactory>? growth = null,
        IEnumerable<IRuntimeRosterCapacityRulesetPolicyFactory>? rosterCapacity = null,
        IEnumerable<IRuntimeEconomyRulesetPolicyFactory>? economy = null,
        IEnumerable<IRuntimeTurnEconomyRulesetPolicyFactory>? turnEconomy = null,
        IEnumerable<IRuntimeStatModifierRulesetPolicyFactory>? statModifier = null)
    {
        _combat = Snapshot(combat, factory => factory.PolicyId, nameof(combat));
        _reward = Snapshot(reward, factory => factory.PolicyId, nameof(reward));
        _stat = Snapshot(stat, factory => factory.PolicyId, nameof(stat));
        _statModifier = Snapshot(statModifier, factory => factory.PolicyId, nameof(statModifier));
        _growth = Snapshot(growth, factory => factory.PolicyId, nameof(growth));
        _rosterCapacity = Snapshot(rosterCapacity, factory => factory.PolicyId, nameof(rosterCapacity));
        _economy = Snapshot(economy, factory => factory.PolicyId, nameof(economy));
        _turnEconomy = Snapshot(turnEconomy, factory => factory.PolicyId, nameof(turnEconomy));
    }

    public IReadOnlyCollection<ContentId> CombatPolicyIds => SnapshotIds(_combat);
    public IReadOnlyCollection<ContentId> RewardPolicyIds => SnapshotIds(_reward);
    public IReadOnlyCollection<ContentId> StatPolicyIds => SnapshotIds(_stat);
    public IReadOnlyCollection<ContentId> StatModifierPolicyIds => SnapshotIds(_statModifier);
    public IReadOnlyCollection<ContentId> GrowthPolicyIds => SnapshotIds(_growth);
    public IReadOnlyCollection<ContentId> RosterCapacityPolicyIds => SnapshotIds(_rosterCapacity);
    public IReadOnlyCollection<ContentId> EconomyPolicyIds => SnapshotIds(_economy);
    public IReadOnlyCollection<ContentId> TurnEconomyPolicyIds => SnapshotIds(_turnEconomy);

    public static RuntimeRulesetPolicyFactoryRegistry CreateStandard() =>
        new(
            combat: [new StandardCombatRulesetPolicyFactory()],
            reward: [new StandardRewardRulesetPolicyFactory()],
            stat: [new StandardStatRulesetPolicyFactory()],
            statModifier:
            [
                new PersistentStagedStatModifierRulesetPolicyFactory(),
                new TimedExclusiveStatModifierRulesetPolicyFactory(),
                new TimedContributionStatModifierRulesetPolicyFactory()
            ],
            growth: [new StandardGrowthRulesetPolicyFactory()],
            rosterCapacity: [new StandardRosterCapacityRulesetPolicyFactory()],
            economy: [new StandardEconomyRulesetPolicyFactory()],
            turnEconomy:
            [
                new StandardActionRulesetPolicyFactory(),
                new StandardActionTokenRulesetPolicyFactory()
            ]);

    internal IRuntimeCombatRulesetPolicyFactory? FindCombat(ContentId policyId) =>
        Find(_combat, policyId);

    internal IRuntimeRewardRulesetPolicyFactory? FindReward(ContentId policyId) =>
        Find(_reward, policyId);

    internal IRuntimeStatRulesetPolicyFactory? FindStat(ContentId policyId) =>
        Find(_stat, policyId);

    internal IRuntimeStatModifierRulesetPolicyFactory? FindStatModifier(ContentId policyId) =>
        Find(_statModifier, policyId);

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

internal sealed class StandardCombatRulesetPolicyFactory : IRuntimeCombatRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardDamage;

    public RulesetBindingResult<CombatExecutionPolicySet> Create(
        RulesetDefinition definition,
        IRandomSource random,
        IStatStageScalingPolicy stageScalingPolicy)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(stageScalingPolicy);

        var diagnostics = new List<RulesetBindingDiagnostic>();
        var config = new ProductionCombatRulesetConfig();
        var actionOutcomeConfig = new StandardActionOutcomeAggregationPolicyConfig();
        IChargePolicyService chargePolicy = new SplitChargePolicy();
        string chargePolicyName = "split";
        foreach ((string key, object? value) in definition.Parameters)
        {
            switch (key)
            {
                case "damageFormulaScalar":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { DamageFormulaScalar = parsed });
                    break;
                case "maximumHitsPerDamageEffect":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { MaximumHitsPerDamageEffect = parsed });
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
                    RulesetPolicyFactoryDiagnostics.UnknownParameter(
                        definition,
                        key,
                        diagnostics);
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
                    RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
                    break;
                case "hitAttackerAgilityCoefficient":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { HitAttackerAgilityCoefficient = parsed });
                    break;
                case "hitTargetAgilityCoefficient":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { HitTargetAgilityCoefficient = parsed });
                    break;
                case "hitChanceMinimum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { HitChanceMinimum = parsed });
                    break;
                case "hitChanceMaximum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { HitChanceMaximum = parsed });
                    break;
                case "instantDeathChanceMinimum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathChanceMinimum = parsed });
                    break;
                case "instantDeathChanceMaximum":
                    config = ReadInt(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathChanceMaximum = parsed });
                    break;
                case "instantDeathVulnerableMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathVulnerableMultiplier = parsed });
                    break;
                case "instantDeathNormalMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathNormalMultiplier = parsed });
                    break;
                case "instantDeathResistantMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathResistantMultiplier = parsed });
                    break;
                case "instantDeathImmuneMultiplier":
                    config = ReadDecimal(definition, key, value, diagnostics, config,
                        (current, parsed) => current with { InstantDeathImmuneMultiplier = parsed });
                    break;
                case "defaultInstantDeathChance":
                    RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
                    break;
                case "itemActionOutcomeBehavior":
                    if (value is not string behavior)
                    {
                        RulesetPolicyFactoryDiagnostics.InvalidType(
                            definition,
                            key,
                            "a string",
                            diagnostics);
                        break;
                    }

                    ItemActionOutcomeBehavior? itemBehavior = behavior switch
                    {
                        "normal" => ItemActionOutcomeBehavior.Normal,
                        "effect_driven" => ItemActionOutcomeBehavior.EffectDriven,
                        _ => null
                    };
                    if (itemBehavior is null)
                    {
                        RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                            definition,
                            $"Parameter '{key}' must be 'normal' or 'effect_driven'.",
                            diagnostics,
                            key);
                        break;
                    }

                    actionOutcomeConfig = new StandardActionOutcomeAggregationPolicyConfig(
                        itemBehavior.Value);
                    break;
                case "chargePolicy":
                    if (value is not string selectedChargePolicy)
                    {
                        RulesetPolicyFactoryDiagnostics.InvalidType(
                            definition,
                            key,
                            "a string",
                            diagnostics);
                        break;
                    }

                    (IChargePolicyService Service, string Name)? selected = selectedChargePolicy switch
                    {
                        "split" => (new SplitChargePolicy(), "split"),
                        "unified" => (new UnifiedChargePolicy(), "unified"),
                        "disabled" => (new DisabledChargePolicy(), "disabled"),
                        _ => null
                    };
                    if (selected is null)
                    {
                        RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                            definition,
                            $"Parameter '{key}' must be 'split', 'unified', or 'disabled'.",
                            diagnostics,
                            key);
                        break;
                    }

                    chargePolicy = selected.Value.Service;
                    chargePolicyName = selected.Value.Name;
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
            return new RulesetBindingResult<CombatExecutionPolicySet>(null, diagnostics);
        }

        try
        {
            var combat = new ProductionCombatRuleset(random, config, stageScalingPolicy);
            return new RulesetBindingResult<CombatExecutionPolicySet>(
                new CombatExecutionPolicySet(
                    definition.Id,
                    definition.PolicyId,
                    combat,
                    chargePolicy,
                    combat,
                    combat,
                    combat,
                    combat,
                    new StandardActionOutcomeAggregationPolicy(actionOutcomeConfig),
                    definition.Parameters,
                    EffectiveConfiguration(config, actionOutcomeConfig, chargePolicyName)));
        }
        catch (ArgumentException exception)
        {
            RulesetPolicyFactoryDiagnostics.InvalidConfiguration(definition, exception.Message, diagnostics);
            return new RulesetBindingResult<CombatExecutionPolicySet>(null, diagnostics);
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> EffectiveConfiguration(
        ProductionCombatRulesetConfig config,
        StandardActionOutcomeAggregationPolicyConfig actionOutcomeConfig,
        string chargePolicyName) =>
    [
        KeyValuePair.Create<string, object?>(
            "maximumHitsPerDamageEffect",
            config.MaximumHitsPerDamageEffect),
        KeyValuePair.Create<string, object?>("damageFormulaScalar", config.DamageFormulaScalar),
        KeyValuePair.Create<string, object?>("damageVarianceMinimum", config.DamageVarianceMinimum),
        KeyValuePair.Create<string, object?>("damageVarianceMaximum", config.DamageVarianceMaximum),
        KeyValuePair.Create<string, object?>("criticalDamageMultiplier", config.CriticalDamageMultiplier),
        KeyValuePair.Create<string, object?>("weakDamageMultiplier", config.WeakDamageMultiplier),
        KeyValuePair.Create<string, object?>("resistDamageMultiplier", config.ResistDamageMultiplier),
        KeyValuePair.Create<string, object?>("guardDamageMultiplier", config.GuardDamageMultiplier),
        KeyValuePair.Create<string, object?>(
            "hitAttackerAgilityCoefficient",
            config.HitAttackerAgilityCoefficient),
        KeyValuePair.Create<string, object?>(
            "hitTargetAgilityCoefficient",
            config.HitTargetAgilityCoefficient),
        KeyValuePair.Create<string, object?>("hitChanceMinimum", config.HitChanceMinimum),
        KeyValuePair.Create<string, object?>("hitChanceMaximum", config.HitChanceMaximum),
        KeyValuePair.Create<string, object?>(
            "instantDeathChanceMinimum",
            config.InstantDeathChanceMinimum),
        KeyValuePair.Create<string, object?>(
            "instantDeathChanceMaximum",
            config.InstantDeathChanceMaximum),
        KeyValuePair.Create<string, object?>(
            "instantDeathVulnerableMultiplier",
            config.InstantDeathVulnerableMultiplier),
        KeyValuePair.Create<string, object?>(
            "instantDeathNormalMultiplier",
            config.InstantDeathNormalMultiplier),
        KeyValuePair.Create<string, object?>(
            "instantDeathResistantMultiplier",
            config.InstantDeathResistantMultiplier),
        KeyValuePair.Create<string, object?>(
            "instantDeathImmuneMultiplier",
            config.InstantDeathImmuneMultiplier),
        KeyValuePair.Create<string, object?>(
            "itemActionOutcomeBehavior",
            actionOutcomeConfig.ItemBehavior == ItemActionOutcomeBehavior.Normal
                ? "normal"
                : "effect_driven"),
        KeyValuePair.Create<string, object?>("chargePolicy", chargePolicyName)
    ];

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
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(random);

        var diagnostics = new List<RulesetBindingDiagnostic>();
        var config = new StandardBattleRewardYieldPolicyConfig();
        foreach ((string key, object? value) in definition.Parameters)
        {
            if (key is not (
                "enemiesPerLevelForExperience" or
                "expectedStatLevelMultiplier" or
                "expectedStatBase" or
                "statDensityDivisor" or
                "maximumStatDensityMultiplier" or
                "currencyBaseMultiplier" or
                "currencyLuckMultiplier" or
                "currencyVarianceMinimum" or
                "currencyVarianceMaximum"))
            {
                RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
                continue;
            }

            if (!RulesetPolicyFactoryParameters.TryReadDecimal(value, out decimal parsed))
            {
                RulesetPolicyFactoryDiagnostics.InvalidType(definition, key, "numeric", diagnostics);
                continue;
            }

            config = key switch
            {
                "enemiesPerLevelForExperience" => config with { EnemiesPerLevelForExperience = parsed },
                "expectedStatLevelMultiplier" => config with { ExpectedStatLevelMultiplier = parsed },
                "expectedStatBase" => config with { ExpectedStatBase = parsed },
                "statDensityDivisor" => config with { StatDensityDivisor = parsed },
                "maximumStatDensityMultiplier" => config with { MaximumStatDensityMultiplier = parsed },
                "currencyBaseMultiplier" => config with { CurrencyBaseMultiplier = parsed },
                "currencyLuckMultiplier" => config with { CurrencyLuckMultiplier = parsed },
                "currencyVarianceMinimum" => config with { CurrencyVarianceMinimum = parsed },
                "currencyVarianceMaximum" => config with { CurrencyVarianceMaximum = parsed },
                _ => config
            };
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
            return new RulesetBindingResult<IBattleRewardService>(null, diagnostics);
        }

        return new RulesetBindingResult<IBattleRewardService>(
            new BattleRewardService(new StandardBattleRewardYieldPolicy(random, config)));
    }

}

internal sealed class StandardStatRulesetPolicyFactory : IRuntimeStatRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardStat;

    public RulesetBindingResult<StatRulesetServices> Create(RulesetDefinition definition)
    {
        var diagnostics = new List<RulesetBindingDiagnostic>();
        foreach (string key in definition.Parameters.Keys.Where(key => key != "stageTables"))
        {
            RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
        }

        var overrides = new List<StatStageScalingTable>();
        if (definition.Parameters.TryGetValue("stageTables", out object? rawTables))
        {
            ReadStageTables(definition, rawTables, overrides, diagnostics);
        }

        if (diagnostics.Count > 0)
        {
            return new RulesetBindingResult<StatRulesetServices>(null, diagnostics);
        }

        try
        {
            return new RulesetBindingResult<StatRulesetServices>(new StatRulesetServices(
                new StandardStatResolutionPolicy(),
                new StandardStatStageScalingPolicy(overrides)));
        }
        catch (ArgumentException exception)
        {
            RulesetPolicyFactoryDiagnostics.InvalidConfiguration(definition, exception.Message, diagnostics);
            return new RulesetBindingResult<StatRulesetServices>(null, diagnostics);
        }
    }

    private static void ReadStageTables(
        RulesetDefinition definition,
        object? rawTables,
        ICollection<StatStageScalingTable> tables,
        ICollection<RulesetBindingDiagnostic> diagnostics)
    {
        if (rawTables is not IReadOnlyList<object?> authoredTables || authoredTables.Count == 0)
        {
            RulesetPolicyFactoryDiagnostics.InvalidType(
                definition,
                "stageTables",
                "nonempty list",
                diagnostics);
            return;
        }

        var seen = new HashSet<(ContentId TrackId, StatStageScalingChannel Channel)>();
        for (int tableIndex = 0; tableIndex < authoredTables.Count; tableIndex++)
        {
            string path = $"stageTables[{tableIndex}]";
            if (authoredTables[tableIndex] is not IReadOnlyDictionary<string, object?> table ||
                table.Keys.Any(key => key is not ("trackId" or "channel" or "multipliers")) ||
                !TryReadTrackId(table, out ContentId trackId) ||
                !TryReadChannel(table, out StatStageScalingChannel channel) ||
                !table.TryGetValue("multipliers", out object? rawRows) ||
                rawRows is not IReadOnlyList<object?> rows)
            {
                RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                    definition,
                    $"Parameter '{path}' must contain only a valid unqualified 'trackId', supported " +
                    "'channel', and 'multipliers' list.",
                    diagnostics,
                    "stageTables");
                continue;
            }

            if (!seen.Add((trackId, channel)))
            {
                RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                    definition,
                    $"Parameter '{path}' duplicates track '{trackId}' and channel '{channel}'.",
                    diagnostics,
                    "stageTables");
                continue;
            }

            var multipliers = new List<StatStageMultiplier>();
            bool valid = true;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (rows[rowIndex] is not IReadOnlyDictionary<string, object?> row ||
                    row.Keys.Any(key => key is not ("stage" or "multiplier")) ||
                    !RulesetPolicyFactoryParameters.TryReadInt(row, "stage", out int stage) ||
                    !row.TryGetValue("multiplier", out object? rawMultiplier) ||
                    !RulesetPolicyFactoryParameters.TryReadDecimal(rawMultiplier, out decimal multiplier))
                {
                    RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                        definition,
                        $"Parameter '{path}.multipliers[{rowIndex}]' must contain an integer 'stage' " +
                        "and numeric 'multiplier'.",
                        diagnostics,
                        "stageTables");
                    valid = false;
                    continue;
                }

                try
                {
                    multipliers.Add(new StatStageMultiplier(stage, multiplier));
                }
                catch (ArgumentException exception)
                {
                    RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                        definition,
                        $"Parameter '{path}.multipliers[{rowIndex}]' is invalid: {exception.Message}",
                        diagnostics,
                        "stageTables");
                    valid = false;
                }
            }

            if (!valid)
            {
                continue;
            }

            try
            {
                tables.Add(new StatStageScalingTable(trackId, channel, multipliers));
            }
            catch (ArgumentException exception)
            {
                RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                    definition,
                    $"Parameter '{path}' is invalid: {exception.Message}",
                    diagnostics,
                    "stageTables");
            }
        }
    }

    private static bool TryReadTrackId(
        IReadOnlyDictionary<string, object?> values,
        out ContentId trackId)
    {
        trackId = default;
        return values.TryGetValue("trackId", out object? value) &&
               value is string text &&
               ContentId.TryParse(text, out trackId) &&
               !trackId.IsQualified;
    }

    private static bool TryReadChannel(
        IReadOnlyDictionary<string, object?> values,
        out StatStageScalingChannel channel)
    {
        channel = default;
        if (!values.TryGetValue("channel", out object? value) || value is not string text)
        {
            return false;
        }

        channel = text switch
        {
            "physical_damage_dealt" => StatStageScalingChannel.PhysicalDamageDealt,
            "magical_damage_dealt" => StatStageScalingChannel.MagicalDamageDealt,
            "damage_taken" => StatStageScalingChannel.DamageTaken,
            "hit_chance" => StatStageScalingChannel.HitChance,
            "evasion" => StatStageScalingChannel.Evasion,
            _ => default
        };
        return text is
            "physical_damage_dealt" or
            "magical_damage_dealt" or
            "damage_taken" or
            "hit_chance" or
            "evasion";
    }
}

internal abstract class BoundedStatModifierRulesetPolicyFactory : IRuntimeStatModifierRulesetPolicyFactory
{
    public abstract ContentId PolicyId { get; }

    public RulesetBindingResult<IStatModifierPolicyService> Create(RulesetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var diagnostics = new List<RulesetBindingDiagnostic>();
        foreach (string key in definition.Parameters.Keys.Where(
                     key => key is not ("minimumStage" or "maximumStage")))
        {
            RulesetPolicyFactoryDiagnostics.UnknownParameter(definition, key, diagnostics);
        }

        bool hasMinimum = TryReadRequiredInt(definition, "minimumStage", diagnostics, out int minimumStage);
        bool hasMaximum = TryReadRequiredInt(definition, "maximumStage", diagnostics, out int maximumStage);
        if (!hasMinimum || !hasMaximum || diagnostics.Count > 0)
        {
            return new RulesetBindingResult<IStatModifierPolicyService>(null, diagnostics);
        }

        try
        {
            return new RulesetBindingResult<IStatModifierPolicyService>(
                new StatModifierPolicyService(CreatePolicy(definition.Id, minimumStage, maximumStage)));
        }
        catch (ArgumentException exception)
        {
            RulesetPolicyFactoryDiagnostics.InvalidConfiguration(
                definition,
                exception.Message,
                diagnostics);
            return new RulesetBindingResult<IStatModifierPolicyService>(null, diagnostics);
        }
    }

    protected abstract IStatModifierPolicy CreatePolicy(
        ContentId rulesetId,
        int minimumStage,
        int maximumStage);

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

internal sealed class PersistentStagedStatModifierRulesetPolicyFactory :
    BoundedStatModifierRulesetPolicyFactory
{
    public override ContentId PolicyId => StandardRulesetPolicyIds.PersistentStagedStatModifier;

    protected override IStatModifierPolicy CreatePolicy(
        ContentId rulesetId,
        int minimumStage,
        int maximumStage) =>
        new PersistentStagedStatModifierPolicy(rulesetId, minimumStage, maximumStage);
}

internal sealed class TimedContributionStatModifierRulesetPolicyFactory :
    BoundedStatModifierRulesetPolicyFactory
{
    public override ContentId PolicyId => StandardRulesetPolicyIds.TimedContributionStatModifier;

    protected override IStatModifierPolicy CreatePolicy(
        ContentId rulesetId,
        int minimumStage,
        int maximumStage) =>
        new TimedContributionStatModifierPolicy(rulesetId, minimumStage, maximumStage);
}

internal sealed class TimedExclusiveStatModifierRulesetPolicyFactory :
    IRuntimeStatModifierRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.TimedExclusiveStatModifier;

    public RulesetBindingResult<IStatModifierPolicyService> Create(RulesetDefinition definition)
    {
        List<RulesetBindingDiagnostic> diagnostics =
            RulesetPolicyFactoryDiagnostics.RequireNoParameters(definition);
        return diagnostics.Count == 0
            ? new RulesetBindingResult<IStatModifierPolicyService>(
                new StatModifierPolicyService(new TimedExclusiveStatModifierPolicy(definition.Id)))
            : new RulesetBindingResult<IStatModifierPolicyService>(null, diagnostics);
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

internal sealed class StandardActionRulesetPolicyFactory : IRuntimeTurnEconomyRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardActions;

    public RulesetBindingResult<BattleTurnEconomyRuleset> Create(RulesetDefinition definition) =>
        StandardTurnEconomyRulesetPolicyFactory.Create(
            definition,
            () => new StandardActionTurnEconomy());
}

internal sealed class StandardActionTokenRulesetPolicyFactory : IRuntimeTurnEconomyRulesetPolicyFactory
{
    public ContentId PolicyId => StandardRulesetPolicyIds.StandardActionToken;

    public RulesetBindingResult<BattleTurnEconomyRuleset> Create(RulesetDefinition definition) =>
        StandardTurnEconomyRulesetPolicyFactory.Create(
            definition,
            () => new ActionTokenTurnEconomy());
}

internal static class StandardTurnEconomyRulesetPolicyFactory
{
    public static RulesetBindingResult<BattleTurnEconomyRuleset> Create(
        RulesetDefinition definition,
        Func<IBattleTurnEconomy> createEconomy)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(createEconomy);

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
                createEconomy,
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
