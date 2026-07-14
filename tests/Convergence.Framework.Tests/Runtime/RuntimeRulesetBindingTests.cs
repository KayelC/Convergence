using Convergence.Content;
using Convergence.Catalog;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

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
            [new BattleRewardRecipientSnapshot(Id("hero"), IsAlive: true, HasActiveHostedEntity: true)]));
        Assert.True(reward.TotalExperience > 0);
        Assert.True(reward.TotalCurrency > 0);
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

        IRosterCapacityPolicy stock = resolver.BindRosterCapacityPolicy(
            catalog,
            Qualified("standard_roster_capacity_sample"))
            .RequireService();
        Assert.Equal(3, stock.GetCapacity(RuntimeRosterKind.HostedEntity, 1));
        Assert.Equal(12, stock.GetCapacity(RuntimeRosterKind.HostedEntity, 40));
        Assert.Equal(3, stock.GetCapacity(RuntimeRosterKind.Companion, 1));
        Assert.Equal(12, stock.GetCapacity(RuntimeRosterKind.Companion, 40));

        ResourceManagementRulesetServices resources = resolver.BindResourceManagementServices(
            catalog,
            Qualified("standard_economy_sample"))
            .RequireService();
        Assert.Equal(90, resources.Shop.CalculateBuyPrice(100, luck: 10));
        Assert.Equal(60, resources.Shop.CalculateSellPrice(100, luck: 10));
        Assert.True(resources.Economy.Debit(new RuntimeWalletSnapshot(10), 5).Applied);
        Assert.Equal(30, resources.Hospital.CalculateRestorationCost(new RuntimeHospitalPatientSnapshot(
            RuntimeInstanceId.Parse("patient"),
            currentHp: 5,
            maxHp: 10,
            currentSp: 5,
            maxSp: 10,
            hasAilment: false)));

        BattleTurnEconomyRuleset turnEconomyRuleset = resolver.BindTurnEconomy(
            catalog,
            Qualified("standard_action_token_sample"))
            .RequireService();
        var turnEconomy = Assert.IsType<Convergence.TurnEconomy.ActionTokenTurnEconomy>(
            turnEconomyRuleset.CreateEconomy());
        turnEconomy.StartPhase(2);
        turnEconomy.ConsumeAction(new TurnEconomyResolution(TurnEconomyOutcome.Weakness, false, false));
        Assert.Equal(1, turnEconomy.FullTokens);
        Assert.Equal(1, turnEconomy.PartialTokens);
        Assert.True(turnEconomyRuleset.PhaseProgress.MaximumCommands > 0);

    }

    [Fact]
    public void MoonPhaseBinding_RemainsAvailableAsAnOptionalExtension()
    {
        ContentId rulesetId = Id("test.pack:optional_moon_phase");
        GameDataCatalog catalog = Catalog(new RulesetDefinition(
            rulesetId,
            "Optional Moon Phase",
            "An opt-in host policy.",
            RulesetCategory.MoonPhase,
            StandardRulesetPolicyIds.StandardMoonPhase));

        RulesetDefinition ruleset = new RuntimeRulesetBindingResolver()
            .BindMoonPhaseRuleset(catalog, rulesetId)
            .RequireService();

        Assert.Equal(StandardRulesetPolicyIds.StandardMoonPhase, ruleset.PolicyId);
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
    public void DamageBinding_AllowsExtremeAuthoredMultipliersWithoutCreatingAnOverflowPath()
    {
        ContentId rulesetId = Id("test.pack:extreme_damage");
        GameDataCatalog catalog = Catalog(new RulesetDefinition(
            rulesetId,
            "Extreme Damage",
            "Proves the runtime saturation boundary without imposing a balance ceiling.",
            RulesetCategory.Damage,
            StandardRulesetPolicyIds.StandardDamage,
            [
                new KeyValuePair<string, object?>("weakMultiplier", decimal.MaxValue),
                new KeyValuePair<string, object?>("resistMultiplier", decimal.MaxValue)
            ]));

        ProductionCombatRuleset ruleset = new RuntimeRulesetBindingResolver()
            .BindProductionCombatRuleset(catalog, rulesetId, new SequenceRandomSource())
            .RequireService();
        var target = new ProductionCombatantProfile(
            1,
            new ProductionCombatStats(1m, 1m, 1m, 1m, 1m));

        ProductionDamageApplicationResult result = ruleset.ApplyDamage(
            new ProductionDamageApplicationRequest(
                target,
                2m,
                DamageElement.Fire,
                ElementalAffinity.Weak,
                Critical: false));

        Assert.Equal(decimal.MaxValue, ruleset.Config.WeakDamageMultiplier);
        Assert.Equal(decimal.MaxValue, result.DamageDealt);
    }

    [Fact]
    public void RosterCapacityBinding_RequiresAuthoredTiersInsteadOfSupplyingAHiddenCurve()
    {
        ContentId missingTiersId = Id("test.pack:missing_tiers");
        ContentId malformedTiersId = Id("test.pack:malformed_tiers");
        var resolver = new RuntimeRulesetBindingResolver();

        RulesetBindingResult<IRosterCapacityPolicy> missing = resolver.BindRosterCapacityPolicy(
            Catalog(new RulesetDefinition(
                missingTiersId,
                "Missing tiers",
                "No implicit capacity curve is allowed.",
                RulesetCategory.RosterCapacity,
                StandardRulesetPolicyIds.StandardRosterCapacity)),
            missingTiersId);
        RulesetBindingResult<IRosterCapacityPolicy> malformed = resolver.BindRosterCapacityPolicy(
            Catalog(new RulesetDefinition(
                malformedTiersId,
                "Malformed tiers",
                "Invalid authored policy.",
                RulesetCategory.RosterCapacity,
                StandardRulesetPolicyIds.StandardRosterCapacity,
                [new KeyValuePair<string, object?>("tiers", "legacy defaults")])),
            malformedTiersId);

        Assert.False(missing.IsSuccess);
        Assert.Null(missing.Service);
        Assert.Equal(RulesetBindingDiagnosticCode.MissingParameter, Assert.Single(missing.Diagnostics).Code);
        Assert.False(malformed.IsSuccess);
        Assert.Null(malformed.Service);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidParameterType, Assert.Single(malformed.Diagnostics).Code);
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

    [Fact]
    public void Binding_DefaultRulesetIdReturnsTypedDiagnosticBeforeCatalogLookup()
    {
        RulesetBindingResult<ProductionCombatRuleset> result =
            new RuntimeRulesetBindingResolver().BindProductionCombatRuleset(
                Catalog(),
                default,
                new SequenceRandomSource());

        Assert.False(result.IsSuccess);
        Assert.Null(result.Service);
        RulesetBindingDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidIdentifier, diagnostic.Code);
        Assert.True(diagnostic.RulesetId.IsEmpty);
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
