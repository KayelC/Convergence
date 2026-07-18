using Convergence.Content;
using Convergence.Catalog;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Convergence.TurnEconomy;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeRulesetBindingTests
{
    private const string CatalogPack = "convergence.catalog_surface_sample";

    [Fact]
    public void CatalogRulesets_BindStandardPoliciesToFrameworkServices()
    {
        GameDataCatalog catalog = RuntimePersistenceSnapshotTests.LoadCatalog();
        var resolver = CreateResolver();
        StatRulesetServices statServices = resolver.BindStatServices(
            catalog,
            Qualified("standard_stat_sample"))
            .RequireService();

        ProductionCombatRuleset damage = resolver.BindProductionCombatRuleset(
            catalog,
            Qualified("standard_damage_sample"),
            new SequenceRandomSource(units: [0.5m]),
            statServices.StageScalingPolicy)
            .RequireService();
        Assert.Equal(1.5m, damage.Config.WeakDamageMultiplier);
        Assert.Equal(0.5m, damage.Config.ResistDamageMultiplier);
        Assert.Same(statServices.StageScalingPolicy, damage.StageScalingPolicy);

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

        IStatResolutionPolicy stats = statServices.StatResolutionPolicy;
        StatResolutionResult stat = stats.Resolve(new StatResolutionRequest(
            RuntimeStatSourceKind.Actor,
            StandardProgressionIds.Strength,
            [new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 50m)]));
        Assert.Equal(40, stat.CappedValue);

        IStatModifierPolicyService statModifiers = resolver.BindStatModifierPolicy(
            catalog,
            Qualified("persistent_staged_modifiers_sample"))
            .RequireService();
        Assert.Equal(
            Qualified("persistent_staged_modifiers_sample"),
            statModifiers.PolicyId);
        StatModifierTransitionResult modifierResult = statModifiers.Apply(new StatModifierApplicationRequest(
            new RuntimeStatModifierStateSnapshot(statModifiers.PolicyId),
            Id("attack"),
            1));
        Assert.True(modifierResult.StateChanged);
        Assert.Equal(1, Assert.Single(modifierResult.After.Tracks).ResolvedStage);

        GrowthRulesetServices growth = resolver.BindGrowthServices(
            catalog,
            Qualified("standard_growth_sample"))
            .RequireService();
        Assert.Equal(12, growth.ExperienceCurve.GetRequiredExperience(2));
        Assert.IsType<StandardResourceGrowthPolicy>(growth.ResourceGrowthPolicy);
        Assert.IsType<StandardLevelGrowthPolicy>(growth.LevelGrowthPolicy);
        Assert.IsType<StatAllocationService>(growth.StatAllocationService);

        IRosterCapacityPolicy roster = resolver.BindRosterCapacityPolicy(
            catalog,
            Qualified("standard_roster_capacity_sample"))
            .RequireService();
        Assert.Equal(3, roster.GetCapacity(RuntimeRosterKind.HostedEntity, 1));
        Assert.Equal(12, roster.GetCapacity(RuntimeRosterKind.HostedEntity, 40));
        Assert.Equal(3, roster.GetCapacity(RuntimeRosterKind.Companion, 1));
        Assert.Equal(12, roster.GetCapacity(RuntimeRosterKind.Companion, 40));

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
        Assert.Equal(256, turnEconomyRuleset.PhaseProgress.MaximumCommands);
        Assert.Equal(32, turnEconomyRuleset.PhaseProgress.MaximumConsecutiveFreeActions);
    }

    [Fact]
    public void StandardFactoryRegistry_DoesNotRegisterMoonPhasePolicy()
    {
        RuntimeRulesetPolicyFactoryRegistry registry = RuntimeRulesetPolicyFactoryRegistry.CreateStandard();
        ContentId[] policyIds = registry.DamagePolicyIds
            .Concat(registry.RewardPolicyIds)
            .Concat(registry.StatPolicyIds)
            .Concat(registry.StatModifierPolicyIds)
            .Concat(registry.GrowthPolicyIds)
            .Concat(registry.RosterCapacityPolicyIds)
            .Concat(registry.EconomyPolicyIds)
            .Concat(registry.TurnEconomyPolicyIds)
            .ToArray();

        Assert.Equal(10, policyIds.Length);
        Assert.DoesNotContain(policyIds, id => id.Value.Contains("moon_phase", StringComparison.Ordinal));
    }

    [Fact]
    public void HostRegistry_ReplacesEveryRuntimeRulesetCategoryWithTypedFactories()
    {
        ContentId damagePolicyId = Id("host_damage");
        ContentId rewardPolicyId = Id("host_reward");
        ContentId statPolicyId = Id("host_stat");
        ContentId statModifierPolicyId = Id("host_stat_modifier");
        ContentId growthPolicyId = Id("host_growth");
        ContentId rosterPolicyId = Id("host_roster");
        ContentId economyPolicyId = Id("host_economy");
        ContentId turnPolicyId = Id("host_turn");

        var combat = new ProductionCombatRuleset(new SequenceRandomSource());
        IBattleRewardService reward = new BattleRewardService(combat);
        var stat = new StatRulesetServices(
            new StandardStatResolutionPolicy(),
            new StandardStatStageScalingPolicy());
        IStatModifierPolicyService statModifiers = new StatModifierPolicyService(
            new PersistentStagedStatModifierPolicy(Id("test.pack:host_stat_modifier_state")));
        var resourceGrowth = new StandardResourceGrowthPolicy();
        var experience = new CubicExperienceCurve();
        var growth = new GrowthRulesetServices(
            resourceGrowth,
            experience,
            new StandardLevelGrowthPolicy(experience, resourceGrowth),
            new StatAllocationService(resourceGrowth));
        IRosterCapacityPolicy roster = NoLimitRosterCapacityPolicy.Instance;
        var inventory = new InventoryTransitionService();
        var equipment = new EquipmentTransitionService();
        var economyTransactions = new EconomyTransactionService();
        var economy = new ResourceManagementRulesetServices(
            inventory,
            equipment,
            economyTransactions,
            new ShopTransactionService(inventory, economyTransactions),
            new HospitalRestorationService(economyTransactions));
        var turn = new BattleTurnEconomyRuleset(
            () => new Convergence.TurnEconomy.ActionTokenTurnEconomy(),
            new BattlePhaseProgressPolicy(20, 4));

        var registry = new RuntimeRulesetPolicyFactoryRegistry(
            damage: [new FixedDamageFactory(damagePolicyId, combat)],
            reward: [new FixedRewardFactory(rewardPolicyId, reward)],
            stat: [new FixedStatFactory(statPolicyId, stat)],
            statModifier: [new FixedStatModifierFactory(statModifierPolicyId, statModifiers)],
            growth: [new FixedGrowthFactory(growthPolicyId, growth)],
            rosterCapacity: [new FixedRosterFactory(rosterPolicyId, roster)],
            economy: [new FixedEconomyFactory(economyPolicyId, economy)],
            turnEconomy: [new FixedTurnFactory(turnPolicyId, turn)]);
        var resolver = new RuntimeRulesetBindingResolver(registry);
        GameDataCatalog catalog = Catalog(
            Ruleset("damage", RulesetCategory.Damage, damagePolicyId),
            Ruleset("reward", RulesetCategory.Reward, rewardPolicyId),
            Ruleset("stat", RulesetCategory.Stat, statPolicyId),
            Ruleset("stat_modifier", RulesetCategory.StatModifier, statModifierPolicyId),
            Ruleset("growth", RulesetCategory.Growth, growthPolicyId),
            Ruleset("roster", RulesetCategory.RosterCapacity, rosterPolicyId),
            Ruleset("economy", RulesetCategory.Economy, economyPolicyId),
            Ruleset("turn", RulesetCategory.TurnEconomy, turnPolicyId));

        Assert.Same(combat, resolver.BindProductionCombatRuleset(
            catalog,
            Id("test.pack:damage"),
            new SequenceRandomSource(),
            stat.StageScalingPolicy).RequireService());
        Assert.Same(reward, resolver.BindBattleRewardService(
            catalog, Id("test.pack:reward"), combat).RequireService());
        Assert.Same(stat, resolver.BindStatServices(
            catalog, Id("test.pack:stat")).RequireService());
        Assert.Same(statModifiers, resolver.BindStatModifierPolicy(
            catalog, Id("test.pack:stat_modifier")).RequireService());
        Assert.Same(growth, resolver.BindGrowthServices(
            catalog, Id("test.pack:growth")).RequireService());
        Assert.Same(roster, resolver.BindRosterCapacityPolicy(
            catalog, Id("test.pack:roster")).RequireService());
        Assert.Same(economy, resolver.BindResourceManagementServices(
            catalog, Id("test.pack:economy")).RequireService());
        Assert.Same(turn, resolver.BindTurnEconomy(
            catalog, Id("test.pack:turn")).RequireService());
    }

    [Fact]
    public void HostRegistry_SnapshotsFactoriesAndRejectsDuplicateOrQualifiedPolicyIds()
    {
        ContentId policyId = Id("host_damage");
        var combat = new ProductionCombatRuleset(new SequenceRandomSource());
        var factories = new List<IRuntimeDamageRulesetPolicyFactory>
        {
            new FixedDamageFactory(policyId, combat)
        };
        var registry = new RuntimeRulesetPolicyFactoryRegistry(damage: factories);
        factories.Clear();

        Assert.Equal([policyId], registry.DamagePolicyIds);
        Assert.Throws<ArgumentException>(() => new RuntimeRulesetPolicyFactoryRegistry(
            damage:
            [
                new FixedDamageFactory(policyId, combat),
                new FixedDamageFactory(policyId, combat)
            ]));
        Assert.Throws<ArgumentException>(() => new RuntimeRulesetPolicyFactoryRegistry(
            damage: [new FixedDamageFactory(Id("test.pack:qualified"), combat)]));
    }

    [Fact]
    public void Resolver_DoesNotExposeAFactoryServiceWhenTheFactoryReportsDiagnostics()
    {
        ContentId policyId = Id("host_rejected_damage");
        ContentId rulesetId = Id("test.pack:rejected_damage");
        var factory = new DiagnosticDamageFactory(
            policyId,
            new ProductionCombatRuleset(new SequenceRandomSource()));
        var resolver = new RuntimeRulesetBindingResolver(
            new RuntimeRulesetPolicyFactoryRegistry(damage: [factory]));

        RulesetBindingResult<ProductionCombatRuleset> result = resolver.BindProductionCombatRuleset(
            Catalog(new RulesetDefinition(
                rulesetId,
                "Rejected Damage",
                "The host factory rejects this authored policy.",
                RulesetCategory.Damage,
                policyId)),
            rulesetId,
            new SequenceRandomSource(),
            StagePolicy());

        Assert.False(result.IsSuccess);
        Assert.Null(result.Service);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidParameterValue, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData(RulesetCategory.Damage)]
    [InlineData(RulesetCategory.Reward)]
    [InlineData(RulesetCategory.Stat)]
    [InlineData(RulesetCategory.StatModifier)]
    [InlineData(RulesetCategory.Growth)]
    [InlineData(RulesetCategory.RosterCapacity)]
    [InlineData(RulesetCategory.Economy)]
    [InlineData(RulesetCategory.TurnEconomy)]
    public void Resolver_ContainsExceptionsFromEveryHostFactoryCategory(RulesetCategory category)
    {
        ContentId policyId = Id("throwing_policy");
        var factory = new ThrowingRulesetFactory(policyId);
        var resolver = new RuntimeRulesetBindingResolver(new RuntimeRulesetPolicyFactoryRegistry(
            damage: [factory],
            reward: [factory],
            stat: [factory],
            statModifier: [factory],
            growth: [factory],
            rosterCapacity: [factory],
            economy: [factory],
            turnEconomy: [factory]));
        RulesetDefinition definition = Ruleset("throwing", category, policyId);
        GameDataCatalog catalog = Catalog(definition);

        RulesetBindingDiagnostic diagnostic = category switch
        {
            RulesetCategory.Damage => Assert.Single(resolver.BindProductionCombatRuleset(
                catalog,
                definition.Id,
                new SequenceRandomSource(),
                StagePolicy()).Diagnostics),
            RulesetCategory.Reward => Assert.Single(resolver.BindBattleRewardService(
                catalog,
                definition.Id,
                new ProductionCombatRuleset(new SequenceRandomSource())).Diagnostics),
            RulesetCategory.Stat => Assert.Single(resolver.BindStatServices(
                catalog,
                definition.Id).Diagnostics),
            RulesetCategory.StatModifier => Assert.Single(resolver.BindStatModifierPolicy(
                catalog,
                definition.Id).Diagnostics),
            RulesetCategory.Growth => Assert.Single(resolver.BindGrowthServices(
                catalog,
                definition.Id).Diagnostics),
            RulesetCategory.RosterCapacity => Assert.Single(resolver.BindRosterCapacityPolicy(
                catalog,
                definition.Id).Diagnostics),
            RulesetCategory.Economy => Assert.Single(resolver.BindResourceManagementServices(
                catalog,
                definition.Id).Diagnostics),
            RulesetCategory.TurnEconomy => Assert.Single(resolver.BindTurnEconomy(
                catalog,
                definition.Id).Diagnostics),
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

        Assert.Equal(RulesetBindingDiagnosticCode.PolicyFactoryFailure, diagnostic.Code);
        Assert.Equal(definition.Id, diagnostic.RulesetId);
        Assert.Equal(category, diagnostic.ExpectedCategory);
        Assert.Equal(category, diagnostic.ActualCategory);
        Assert.Equal(policyId, diagnostic.PolicyId);
        Assert.Contains("Host policy factory failed.", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolver_DoesNotConvertHostFactoryCancellationIntoAConfigurationDiagnostic()
    {
        ContentId policyId = Id("canceling_policy");
        RulesetDefinition definition = Ruleset("canceling", RulesetCategory.Damage, policyId);
        var resolver = new RuntimeRulesetBindingResolver(new RuntimeRulesetPolicyFactoryRegistry(
            damage: [new CancelingDamageFactory(policyId)]));

        Assert.Throws<OperationCanceledException>(() => resolver.BindProductionCombatRuleset(
            Catalog(definition),
            definition.Id,
            new SequenceRandomSource(),
            StagePolicy()));
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
                new KeyValuePair<string, object?>("weakDamageMultiplier", 2m),
                new KeyValuePair<string, object?>("resistDamageMultiplier", 0.25m)
            ]));

        ProductionCombatRuleset ruleset = CreateResolver()
            .BindProductionCombatRuleset(catalog, rulesetId, new SequenceRandomSource(), StagePolicy())
            .RequireService();

        Assert.Equal(2m, ruleset.Config.WeakDamageMultiplier);
        Assert.Equal(0.25m, ruleset.Config.ResistDamageMultiplier);
    }

    [Fact]
    public void DamageBinding_ExposesEveryProductionCombatConfigurationValue()
    {
        ContentId rulesetId = Id("test.pack:complete_damage");
        GameDataCatalog catalog = Catalog(new RulesetDefinition(
            rulesetId,
            "Complete Damage",
            "Every standard combat input is authored.",
            RulesetCategory.Damage,
            StandardRulesetPolicyIds.StandardDamage,
            Parameters(
                ("damageFormulaScalar", 6m),
                ("damageVarianceMinimum", 0.8m),
                ("damageVarianceMaximum", 1.2m),
                ("chargeMultiplier", 2m),
                ("criticalDamageMultiplier", 1.7m),
                ("weakDamageMultiplier", 1.8m),
                ("resistDamageMultiplier", 0.4m),
                ("guardDamageMultiplier", 0.3m),
                ("defaultHitAccuracy", 88),
                ("hitChanceMinimum", 3),
                ("hitChanceMaximum", 97),
                ("criticalChanceMinimum", 1),
                ("criticalChanceMaximum", 45),
                ("criticalChanceBase", 7),
                ("instantDeathChanceMinimum", 4),
                ("instantDeathChanceMaximum", 90),
                ("defaultInstantDeathChance", 35),
                ("enemiesPerLevelForExperience", 60m),
                ("expectedStatLevelMultiplier", 4m),
                ("expectedStatBase", 20m),
                ("statDensityDivisor", 120m),
                ("maximumStatDensityMultiplier", 3m),
                ("currencyBaseMultiplier", 0.3m),
                ("currencyLuckMultiplier", 6m),
                ("currencyVarianceMinimum", 0.85m),
                ("currencyVarianceMaximum", 1.15m),
                ("initiativeVarianceMinimum", 0.8m),
                ("initiativeVarianceMaximum", 1.2m))));

        ProductionCombatRulesetConfig config = CreateResolver()
            .BindProductionCombatRuleset(catalog, rulesetId, new SequenceRandomSource(), StagePolicy())
            .RequireService()
            .Config;

        Assert.Equal(6m, config.DamageFormulaScalar);
        Assert.Equal(0.8m, config.DamageVarianceMinimum);
        Assert.Equal(1.2m, config.DamageVarianceMaximum);
        Assert.Equal(2m, config.ChargeMultiplier);
        Assert.Equal(1.7m, config.CriticalDamageMultiplier);
        Assert.Equal(1.8m, config.WeakDamageMultiplier);
        Assert.Equal(0.4m, config.ResistDamageMultiplier);
        Assert.Equal(0.3m, config.GuardDamageMultiplier);
        Assert.Equal(88, config.DefaultHitAccuracy);
        Assert.Equal(3, config.HitChanceMinimum);
        Assert.Equal(97, config.HitChanceMaximum);
        Assert.Equal(1, config.CriticalChanceMinimum);
        Assert.Equal(45, config.CriticalChanceMaximum);
        Assert.Equal(7, config.CriticalChanceBase);
        Assert.Equal(4, config.InstantDeathChanceMinimum);
        Assert.Equal(90, config.InstantDeathChanceMaximum);
        Assert.Equal(35, config.DefaultInstantDeathChance);
        Assert.Equal(60m, config.EnemiesPerLevelForExperience);
        Assert.Equal(4m, config.ExpectedStatLevelMultiplier);
        Assert.Equal(20m, config.ExpectedStatBase);
        Assert.Equal(120m, config.StatDensityDivisor);
        Assert.Equal(3m, config.MaximumStatDensityMultiplier);
        Assert.Equal(0.3m, config.CurrencyBaseMultiplier);
        Assert.Equal(6m, config.CurrencyLuckMultiplier);
        Assert.Equal(0.85m, config.CurrencyVarianceMinimum);
        Assert.Equal(1.15m, config.CurrencyVarianceMaximum);
        Assert.Equal(0.8m, config.InitiativeVarianceMinimum);
        Assert.Equal(1.2m, config.InitiativeVarianceMaximum);
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
                new KeyValuePair<string, object?>("weakDamageMultiplier", decimal.MaxValue),
                new KeyValuePair<string, object?>("resistDamageMultiplier", decimal.MaxValue),
                new KeyValuePair<string, object?>("damageVarianceMinimum", 1m),
                new KeyValuePair<string, object?>("damageVarianceMaximum", 1m)
            ]));

        ProductionCombatRuleset ruleset = CreateResolver()
            .BindProductionCombatRuleset(catalog, rulesetId, new SequenceRandomSource(), StagePolicy())
            .RequireService();
        var target = new ProductionCombatantProfile(
            1,
            new ProductionCombatStats(1m, 1m, 1m, 1m, 1m));

        ProductionDamageResolutionResult result = ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                target,
                target,
                DamageElement.Fire,
                ElementalAffinity.Weak,
                1,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1)));

        Assert.Equal(decimal.MaxValue, ruleset.Config.WeakDamageMultiplier);
        Assert.Equal(decimal.MaxValue, Assert.Single(result.Hits).Damage);
    }

    [Fact]
    public void RosterCapacityBinding_RequiresAuthoredTiersInsteadOfSupplyingAHiddenCurve()
    {
        ContentId missingTiersId = Id("test.pack:missing_tiers");
        ContentId malformedTiersId = Id("test.pack:malformed_tiers");
        var resolver = CreateResolver();

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
                [new KeyValuePair<string, object?>("tiers", "reference defaults")])),
            malformedTiersId);

        Assert.False(missing.IsSuccess);
        Assert.Null(missing.Service);
        Assert.Equal(RulesetBindingDiagnosticCode.MissingParameter, Assert.Single(missing.Diagnostics).Code);
        Assert.False(malformed.IsSuccess);
        Assert.Null(malformed.Service);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidParameterType, Assert.Single(malformed.Diagnostics).Code);
    }

    [Fact]
    public void ActionTokenBinding_RequiresAuthoredFiniteLivenessLimits()
    {
        ContentId missingId = Id("test.pack:missing_action_limits");
        ContentId invalidId = Id("test.pack:invalid_action_limits");
        RuntimeRulesetBindingResolver resolver = CreateResolver();

        RulesetBindingResult<BattleTurnEconomyRuleset> missing = resolver.BindTurnEconomy(
            Catalog(new RulesetDefinition(
                missingId,
                "Missing Limits",
                "No hidden liveness defaults.",
                RulesetCategory.TurnEconomy,
                StandardRulesetPolicyIds.StandardActionToken)),
            missingId);
        RulesetBindingResult<BattleTurnEconomyRuleset> invalid = resolver.BindTurnEconomy(
            Catalog(new RulesetDefinition(
                invalidId,
                "Invalid Limits",
                "The free-action limit cannot consume the entire command budget.",
                RulesetCategory.TurnEconomy,
                StandardRulesetPolicyIds.StandardActionToken,
                Parameters(
                    ("maximumCommands", 8),
                    ("maximumConsecutiveFreeActions", 8)))),
            invalidId);

        Assert.False(missing.IsSuccess);
        Assert.Equal(2, missing.Diagnostics.Count(diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.MissingParameter));
        Assert.False(invalid.IsSuccess);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidParameterValue, Assert.Single(invalid.Diagnostics).Code);
    }

    [Fact]
    public void StatModifierBinding_ConstructsAllSuppliedPoliciesFromAuthoredRulesets()
    {
        ContentId persistentId = Id("test.pack:persistent");
        ContentId exclusiveId = Id("test.pack:exclusive");
        ContentId contributionId = Id("test.pack:contribution");
        GameDataCatalog catalog = Catalog(
            new RulesetDefinition(
                persistentId,
                "Persistent",
                "Bounded encounter-persistent stages.",
                RulesetCategory.StatModifier,
                StandardRulesetPolicyIds.PersistentStagedStatModifier,
                Parameters(("minimumStage", -2), ("maximumStage", 2))),
            new RulesetDefinition(
                exclusiveId,
                "Exclusive",
                "One timed signal.",
                RulesetCategory.StatModifier,
                StandardRulesetPolicyIds.TimedExclusiveStatModifier),
            new RulesetDefinition(
                contributionId,
                "Contributions",
                "Independently timed contributions.",
                RulesetCategory.StatModifier,
                StandardRulesetPolicyIds.TimedContributionStatModifier,
                Parameters(("minimumStage", -3), ("maximumStage", 3))));
        RuntimeRulesetBindingResolver resolver = CreateResolver();

        IStatModifierPolicyService persistent = resolver
            .BindStatModifierPolicy(catalog, persistentId)
            .RequireService();
        StatModifierTransitionResult persistentResult = persistent.Apply(new StatModifierApplicationRequest(
            new RuntimeStatModifierStateSnapshot(persistent.PolicyId),
            Id("attack"),
            4));
        Assert.Equal(persistentId, persistent.PolicyId);
        Assert.Equal(2, Assert.Single(persistentResult.After.Tracks).ResolvedStage);

        var duration = new TurnDurationDefinition(3, Id("owner_turn_end"), true);
        IStatModifierPolicyService exclusive = resolver
            .BindStatModifierPolicy(catalog, exclusiveId)
            .RequireService();
        StatModifierTransitionResult exclusiveResult = exclusive.Apply(new StatModifierApplicationRequest(
            new RuntimeStatModifierStateSnapshot(exclusive.PolicyId),
            Id("defense"),
            2,
            duration));
        Assert.Equal(exclusiveId, exclusive.PolicyId);
        Assert.Equal(2, Assert.Single(exclusiveResult.After.Tracks).ResolvedStage);

        IStatModifierPolicyService contributions = resolver
            .BindStatModifierPolicy(catalog, contributionId)
            .RequireService();
        RuntimeStatModifierStateSnapshot contributionState = contributions.Apply(
            new StatModifierApplicationRequest(
                new RuntimeStatModifierStateSnapshot(contributions.PolicyId),
                Id("agility"),
                1,
                duration)).After;
        contributionState = contributions.Apply(new StatModifierApplicationRequest(
            contributionState,
            Id("agility"),
            1,
            duration)).After;
        RuntimeStatModifierTrackSnapshot contributionTrack = Assert.Single(contributionState.Tracks);
        Assert.Equal(contributionId, contributions.PolicyId);
        Assert.Equal(2, contributionTrack.ResolvedStage);
        Assert.Equal(2, contributionTrack.Contributions.Count);
    }

    [Fact]
    public void StatModifierBinding_RejectsMissingBoundsUnknownParametersAndWrongCategory()
    {
        ContentId missingBoundsId = Id("test.pack:missing_bounds");
        ContentId unknownParameterId = Id("test.pack:unknown_parameter");
        ContentId wrongCategoryId = Id("test.pack:wrong_modifier_category");
        RuntimeRulesetBindingResolver resolver = CreateResolver();

        RulesetBindingResult<IStatModifierPolicyService> missingBounds = resolver.BindStatModifierPolicy(
            Catalog(new RulesetDefinition(
                missingBoundsId,
                "Missing Bounds",
                "Bounds are intentionally absent.",
                RulesetCategory.StatModifier,
                StandardRulesetPolicyIds.PersistentStagedStatModifier)),
            missingBoundsId);
        RulesetBindingResult<IStatModifierPolicyService> unknownParameter = resolver.BindStatModifierPolicy(
            Catalog(new RulesetDefinition(
                unknownParameterId,
                "Unknown Parameter",
                "Exclusive signals have no policy parameters.",
                RulesetCategory.StatModifier,
                StandardRulesetPolicyIds.TimedExclusiveStatModifier,
                Parameters(("duration", 3)))),
            unknownParameterId);
        RulesetBindingResult<IStatModifierPolicyService> wrongCategory = resolver.BindStatModifierPolicy(
            Catalog(new RulesetDefinition(
                wrongCategoryId,
                "Wrong Category",
                "A stat ruleset cannot bind as modifier lifecycle.",
                RulesetCategory.Stat,
                StandardRulesetPolicyIds.PersistentStagedStatModifier,
                Parameters(("minimumStage", -4), ("maximumStage", 4)))),
            wrongCategoryId);

        Assert.False(missingBounds.IsSuccess);
        Assert.Equal(2, missingBounds.Diagnostics.Count(diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.MissingParameter));
        Assert.False(unknownParameter.IsSuccess);
        Assert.Equal(RulesetBindingDiagnosticCode.UnknownParameter, Assert.Single(unknownParameter.Diagnostics).Code);
        Assert.False(wrongCategory.IsSuccess);
        Assert.Equal(RulesetBindingDiagnosticCode.CategoryMismatch, Assert.Single(wrongCategory.Diagnostics).Code);
    }

    [Theory]
    [InlineData("persistent_staged", -5, 4)]
    [InlineData("persistent_staged", -4, 5)]
    [InlineData("timed_contribution", -5, 4)]
    [InlineData("timed_contribution", -4, 5)]
    public void StatModifierBinding_RejectsBoundsOutsideTheSuppliedScalingDomain(
        string policyId,
        int minimumStage,
        int maximumStage)
    {
        ContentId rulesetId = Id("test.pack:unsupported_bounds");
        RulesetBindingResult<IStatModifierPolicyService> result = CreateResolver().BindStatModifierPolicy(
            Catalog(new RulesetDefinition(
                rulesetId,
                "Unsupported Bounds",
                "Bounds exceed the supplied four-stage scaling domain.",
                RulesetCategory.StatModifier,
                Id(policyId),
                Parameters(("minimumStage", minimumStage), ("maximumStage", maximumStage)))),
            rulesetId);

        Assert.False(result.IsSuccess);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidParameterValue, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Binding_ReportsMissingWrongCategoryUnsupportedPolicyAndBadParameters()
    {
        var resolver = CreateResolver();
        GameDataCatalog empty = Catalog();

        RulesetBindingResult<ProductionCombatRuleset> missing = resolver.BindProductionCombatRuleset(
            empty,
            Id("test.pack:missing"),
            new SequenceRandomSource(),
            StagePolicy());
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
            new SequenceRandomSource(),
            StagePolicy());
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
            new SequenceRandomSource(),
            StagePolicy());
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
                    new KeyValuePair<string, object?>("weakDamageMultiplier", "loud"),
                    new KeyValuePair<string, object?>("resistDamageMultiplier", -1m),
                    new KeyValuePair<string, object?>("criticalMultiplier", 3m)
                ])),
            badParametersId,
            new SequenceRandomSource(),
            StagePolicy());

        Assert.False(badParameters.IsSuccess);
        Assert.Contains(badParameters.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.InvalidParameterType &&
            diagnostic.ParameterName == "weakDamageMultiplier");
        Assert.Contains(badParameters.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.InvalidParameterValue &&
            diagnostic.ParameterName is null);
        Assert.Contains(badParameters.Diagnostics, diagnostic =>
            diagnostic.Code == RulesetBindingDiagnosticCode.UnknownParameter &&
            diagnostic.ParameterName == "criticalMultiplier");
        Assert.Throws<InvalidOperationException>(() => badParameters.RequireService());
    }

    [Fact]
    public void Binding_DefaultRulesetIdReturnsTypedDiagnosticBeforeCatalogLookup()
    {
        RulesetBindingResult<ProductionCombatRuleset> result =
            CreateResolver().BindProductionCombatRuleset(
                Catalog(),
                default,
                new SequenceRandomSource(),
                StagePolicy());

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

    private static IEnumerable<KeyValuePair<string, object?>> Parameters(
        params (string Key, object? Value)[] parameters) =>
        parameters.Select(parameter => KeyValuePair.Create(parameter.Key, parameter.Value));

    private static RulesetDefinition Ruleset(
        string localId,
        RulesetCategory category,
        ContentId policyId) =>
        new(
            Id($"test.pack:{localId}"),
            localId,
            "Host-registered policy fixture.",
            category,
            policyId);

    private static RuntimeRulesetBindingResolver CreateResolver() =>
        new(RuntimeRulesetPolicyFactoryRegistry.CreateStandard());

    private static IStatStageScalingPolicy StagePolicy() =>
        new StandardStatStageScalingPolicy();

    private sealed class FixedDamageFactory(
        ContentId policyId,
        ProductionCombatRuleset service) : IRuntimeDamageRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<ProductionCombatRuleset> Create(
            RulesetDefinition definition,
            IRandomSource random,
            IStatStageScalingPolicy stageScalingPolicy) =>
            new(service);
    }

    private sealed class FixedRewardFactory(
        ContentId policyId,
        IBattleRewardService service) : IRuntimeRewardRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<IBattleRewardService> Create(
            RulesetDefinition definition,
            ProductionCombatRuleset combatRuleset) =>
            new(service);
    }

    private sealed class DiagnosticDamageFactory(
        ContentId policyId,
        ProductionCombatRuleset service) : IRuntimeDamageRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<ProductionCombatRuleset> Create(
            RulesetDefinition definition,
            IRandomSource random,
            IStatStageScalingPolicy stageScalingPolicy) =>
            new(
                service,
                [
                    new RulesetBindingDiagnostic(
                        RulesetBindingDiagnosticCode.InvalidParameterValue,
                        definition.Id,
                        "Rejected by the host factory.",
                        ActualCategory: definition.Category,
                        PolicyId: definition.PolicyId)
                ]);
    }

    private sealed class ThrowingRulesetFactory(ContentId policyId) :
        IRuntimeDamageRulesetPolicyFactory,
        IRuntimeRewardRulesetPolicyFactory,
        IRuntimeStatRulesetPolicyFactory,
        IRuntimeStatModifierRulesetPolicyFactory,
        IRuntimeGrowthRulesetPolicyFactory,
        IRuntimeRosterCapacityRulesetPolicyFactory,
        IRuntimeEconomyRulesetPolicyFactory,
        IRuntimeTurnEconomyRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        RulesetBindingResult<ProductionCombatRuleset> IRuntimeDamageRulesetPolicyFactory.Create(
            RulesetDefinition definition,
            IRandomSource random,
            IStatStageScalingPolicy stageScalingPolicy) =>
            Fail<RulesetBindingResult<ProductionCombatRuleset>>();

        RulesetBindingResult<IBattleRewardService> IRuntimeRewardRulesetPolicyFactory.Create(
            RulesetDefinition definition,
            ProductionCombatRuleset combatRuleset) => Fail<RulesetBindingResult<IBattleRewardService>>();

        RulesetBindingResult<StatRulesetServices> IRuntimeStatRulesetPolicyFactory.Create(
            RulesetDefinition definition) => Fail<RulesetBindingResult<StatRulesetServices>>();

        RulesetBindingResult<IStatModifierPolicyService> IRuntimeStatModifierRulesetPolicyFactory.Create(
            RulesetDefinition definition) => Fail<RulesetBindingResult<IStatModifierPolicyService>>();

        RulesetBindingResult<GrowthRulesetServices> IRuntimeGrowthRulesetPolicyFactory.Create(
            RulesetDefinition definition) => Fail<RulesetBindingResult<GrowthRulesetServices>>();

        RulesetBindingResult<IRosterCapacityPolicy> IRuntimeRosterCapacityRulesetPolicyFactory.Create(
            RulesetDefinition definition) => Fail<RulesetBindingResult<IRosterCapacityPolicy>>();

        RulesetBindingResult<ResourceManagementRulesetServices> IRuntimeEconomyRulesetPolicyFactory.Create(
            RulesetDefinition definition) => Fail<RulesetBindingResult<ResourceManagementRulesetServices>>();

        RulesetBindingResult<BattleTurnEconomyRuleset> IRuntimeTurnEconomyRulesetPolicyFactory.Create(
            RulesetDefinition definition) => Fail<RulesetBindingResult<BattleTurnEconomyRuleset>>();

        private static T Fail<T>() => throw new InvalidOperationException("Host policy factory failed.");
    }

    private sealed class CancelingDamageFactory(ContentId policyId) : IRuntimeDamageRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<ProductionCombatRuleset> Create(
            RulesetDefinition definition,
            IRandomSource random,
            IStatStageScalingPolicy stageScalingPolicy) =>
            throw new OperationCanceledException("Host policy factory cancelled.");
    }

    private sealed class FixedStatFactory(
        ContentId policyId,
        StatRulesetServices service) : IRuntimeStatRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<StatRulesetServices> Create(RulesetDefinition definition) =>
            new(service);
    }

    private sealed class FixedStatModifierFactory(
        ContentId policyId,
        IStatModifierPolicyService service) : IRuntimeStatModifierRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<IStatModifierPolicyService> Create(RulesetDefinition definition) =>
            new(service);
    }

    private sealed class FixedGrowthFactory(
        ContentId policyId,
        GrowthRulesetServices service) : IRuntimeGrowthRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<GrowthRulesetServices> Create(RulesetDefinition definition) =>
            new(service);
    }

    private sealed class FixedRosterFactory(
        ContentId policyId,
        IRosterCapacityPolicy service) : IRuntimeRosterCapacityRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<IRosterCapacityPolicy> Create(RulesetDefinition definition) =>
            new(service);
    }

    private sealed class FixedEconomyFactory(
        ContentId policyId,
        ResourceManagementRulesetServices service) : IRuntimeEconomyRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<ResourceManagementRulesetServices> Create(RulesetDefinition definition) =>
            new(service);
    }

    private sealed class FixedTurnFactory(
        ContentId policyId,
        BattleTurnEconomyRuleset service) : IRuntimeTurnEconomyRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<BattleTurnEconomyRuleset> Create(RulesetDefinition definition) =>
            new(service);
    }

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
