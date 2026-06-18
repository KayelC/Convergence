using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class RuntimeRulesetBindingTests
{
    private const string CatalogPack = "convergence.catalog_surface_sample";

    [Fact]
    public void CatalogRulesets_BindStandardPoliciesToFrameworkServices()
    {
        GameDataCatalog catalog = RuntimePersistenceSnapshotTests.LoadCatalog();
        var resolver = new RuntimeRulesetBindingResolver();

        ProductionCombatRuleset damage = resolver.BindProductionCombatRuleset(
            catalog,
            Qualified("standard_damage_sample"),
            new SequenceRandomSource(units: [0.5m]))
            .RequireService();
        Assert.Equal(1.5m, damage.Config.WeakDamageMultiplier);
        Assert.Equal(0.5m, damage.Config.ResistDamageMultiplier);

        IBattleRewardService rewards = resolver.BindBattleRewardService(
            catalog,
            Qualified("standard_reward_sample"),
            damage)
            .RequireService();
        BattleRewardResult reward = rewards.Calculate(new BattleRewardRequest(
            [new BattleRewardEnemySnapshot(Id("enemy"), 2, 10, 10, 10, 10, 10)],
            [new BattleRewardRecipientSnapshot(Id("hero"), IsAlive: true, HasActiveForm: true)]));
        Assert.True(reward.TotalExperience > 0);
        Assert.True(reward.TotalMacca > 0);
        Assert.Equal(2, reward.Applications.Count);

        IStatResolutionPolicy stats = resolver.BindStatResolutionPolicy(
            catalog,
            Qualified("standard_stat_sample"))
            .RequireService();
        StatResolutionResult stat = stats.Resolve(new StatResolutionRequest(
            StandardProgressionIds.Human,
            StandardProgressionIds.Strength,
            [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 50m)]));
        Assert.Equal(40, stat.CappedValue);

        GrowthRulesetServices growth = resolver.BindGrowthServices(
            catalog,
            Qualified("standard_growth_sample"))
            .RequireService();
        Assert.Equal(12, growth.ExperienceCurve.GetRequiredExperience(2));
        Assert.IsType<StandardResourceGrowthPolicy>(growth.ResourceGrowthPolicy);
        Assert.IsType<StandardLevelGrowthPolicy>(growth.LevelGrowthPolicy);
        Assert.IsType<StatAllocationService>(growth.StatAllocationService);

        IStockCapacityPolicy stock = resolver.BindStockCapacityPolicy(
            catalog,
            Qualified("standard_stock_capacity_sample"))
            .RequireService();
        Assert.Equal(3, stock.GetCapacity(1));
        Assert.Equal(12, stock.GetCapacity(40));

        ResourceManagementRulesetServices resources = resolver.BindResourceManagementServices(
            catalog,
            Qualified("standard_economy_sample"))
            .RequireService();
        Assert.Equal(90, resources.Shop.CalculateBuyPrice(100, luck: 10));
        Assert.Equal(60, resources.Shop.CalculateSellPrice(100, luck: 10));
        Assert.True(resources.Economy.SpendMacca(new RuntimeWalletSnapshot(10), 5).Applied);
        Assert.Equal(30, resources.Hospital.CalculateRestorationCost(new RuntimeHospitalPatientSnapshot(
            RuntimeInstanceId.Parse("patient"),
            currentHp: 5,
            maxHp: 10,
            currentSp: 5,
            maxSp: 10,
            hasAilment: false)));

        Func<JRPGPrototype.Logic.Battle.Engines.PressTurnEngine> pressTurns = resolver.BindPressTurnFactory(
            catalog,
            Qualified("standard_press_turn_sample"))
            .RequireService();
        var pressTurn = pressTurns();
        pressTurn.StartPhase(2);
        pressTurn.ConsumeAction(new PressTurnResolution(PressTurnOutcome.Weakness, false, false));
        Assert.Equal(1, pressTurn.FullIcons);
        Assert.Equal(1, pressTurn.BlinkingIcons);

        RulesetDefinition moonPhase = resolver.BindMoonPhaseRuleset(
            catalog,
            Qualified("standard_moon_phase_sample"))
            .RequireService();
        Assert.Equal(StandardRulesetPolicyIds.StandardMoonPhase, moonPhase.PolicyId);
    }

    [Fact]
    public void DamageBinding_AppliesOnlyApprovedSupportedParameters()
    {
        ContentId rulesetId = Id("test.pack:custom_damage");
        GameDataCatalog catalog = Catalog(new RulesetDefinition(
            rulesetId,
            "Custom Damage",
            "Supported parameter binding.",
            RulesetCategory.Damage,
            StandardRulesetPolicyIds.StandardDamage,
            [
                new KeyValuePair<string, object?>("weakMultiplier", 2m),
                new KeyValuePair<string, object?>("resistMultiplier", 0.25m)
            ]));

        ProductionCombatRuleset ruleset = new RuntimeRulesetBindingResolver()
            .BindProductionCombatRuleset(catalog, rulesetId, new SequenceRandomSource())
            .RequireService();

        Assert.Equal(2m, ruleset.Config.WeakDamageMultiplier);
        Assert.Equal(0.25m, ruleset.Config.ResistDamageMultiplier);
    }

    [Fact]
    public void Binding_ReportsMissingWrongCategoryUnsupportedPolicyAndBadParameters()
    {
        var resolver = new RuntimeRulesetBindingResolver();
        GameDataCatalog empty = Catalog();

        RulesetBindingResult<ProductionCombatRuleset> missing = resolver.BindProductionCombatRuleset(
            empty,
            Id("test.pack:missing"),
            new SequenceRandomSource());
        Assert.False(missing.IsSuccess);
        Assert.Equal(RulesetBindingDiagnosticCode.MissingRuleset, Assert.Single(missing.Diagnostics).Code);

        ContentId wrongCategoryId = Id("test.pack:wrong_category");
        RulesetBindingResult<ProductionCombatRuleset> wrongCategory = resolver.BindProductionCombatRuleset(
            Catalog(new RulesetDefinition(
                wrongCategoryId,
                "Wrong",
                "Wrong category.",
                RulesetCategory.Reward,
                StandardRulesetPolicyIds.StandardDamage)),
            wrongCategoryId,
            new SequenceRandomSource());
        Assert.Equal(RulesetBindingDiagnosticCode.CategoryMismatch, Assert.Single(wrongCategory.Diagnostics).Code);

        ContentId unsupportedId = Id("test.pack:unsupported");
        RulesetBindingResult<ProductionCombatRuleset> unsupported = resolver.BindProductionCombatRuleset(
            Catalog(new RulesetDefinition(
                unsupportedId,
                "Unsupported",
                "Unsupported policy.",
                RulesetCategory.Damage,
                Id("custom_damage_policy"))),
            unsupportedId,
            new SequenceRandomSource());
        Assert.Equal(RulesetBindingDiagnosticCode.UnsupportedPolicy, Assert.Single(unsupported.Diagnostics).Code);

        ContentId badParametersId = Id("test.pack:bad_parameters");
        RulesetBindingResult<ProductionCombatRuleset> badParameters = resolver.BindProductionCombatRuleset(
            Catalog(new RulesetDefinition(
                badParametersId,
                "Bad Parameters",
                "Invalid parameter binding.",
                RulesetCategory.Damage,
                StandardRulesetPolicyIds.StandardDamage,
                [
                    new KeyValuePair<string, object?>("weakMultiplier", "loud"),
                    new KeyValuePair<string, object?>("resistMultiplier", 0m),
                    new KeyValuePair<string, object?>("criticalMultiplier", 3m)
                ])),
            badParametersId,
            new SequenceRandomSource());

        Assert.False(badParameters.IsSuccess);
        Assert.Contains(badParameters.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.InvalidParameterType &&
            diagnostic.ParameterName == "weakMultiplier");
        Assert.Contains(badParameters.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.InvalidParameterValue &&
            diagnostic.ParameterName == "resistMultiplier");
        Assert.Contains(badParameters.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.UnknownParameter &&
            diagnostic.ParameterName == "criticalMultiplier");
        Assert.Throws<InvalidOperationException>(() => badParameters.RequireService());
    }

    private static GameDataCatalog Catalog(params RulesetDefinition[] rulesets) =>
        new(
            [],
            [],
            [],
            [],
            [],
            rulesets: rulesets.Select(ruleset => KeyValuePair.Create(ruleset.Id, ruleset)));

    private static ContentId Qualified(string localId) => ContentId.Parse($"{CatalogPack}:{localId}");

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class SequenceRandomSource(
        IReadOnlyList<int>? integers = null,
        IReadOnlyList<decimal>? units = null) : IRandomSource
    {
        private int _integerIndex;
        private int _unitIndex;
        private readonly IReadOnlyList<int> _integers = integers ?? [];
        private readonly IReadOnlyList<decimal> _units = units ?? [];

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            if (_integerIndex >= _integers.Count)
            {
                return minimumInclusive;
            }

            int value = _integers[_integerIndex++];
            return Math.Clamp(value, minimumInclusive, maximumExclusive - 1);
        }

        public decimal NextUnitDecimal()
        {
            if (_unitIndex >= _units.Count)
            {
                return 0m;
            }

            return _units[_unitIndex++];
        }
    }
}
