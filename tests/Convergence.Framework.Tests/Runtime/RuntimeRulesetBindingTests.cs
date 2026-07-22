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

        CombatExecutionPolicySet combat = resolver.BindCombatPolicies(
            catalog,
            Qualified("standard_damage_sample"),
            new SequenceRandomSource(units: [0.5m]),
            statServices.StageScalingPolicy)
            .RequireService();
        var damage = Assert.IsType<ProductionCombatRuleset>(combat.Damage);
        Assert.Equal(1.5m, damage.Config.WeakDamageMultiplier);
        Assert.Equal(0.5m, damage.Config.ResistDamageMultiplier);
        Assert.Same(statServices.StageScalingPolicy, damage.StageScalingPolicy);
        Assert.Equal(Qualified("standard_damage_sample"), combat.RulesetId);
        Assert.Equal(StandardRulesetPolicyIds.StandardDamage, combat.PolicyId);
        Assert.Equal(5m, combat.EffectiveConfiguration["damageFormulaScalar"]);
        Assert.Equal(64L, Assert.IsType<long>(combat.EffectiveConfiguration["maximumHitsPerDamageEffect"]));
        Assert.Equal(1.5m, combat.EffectiveConfiguration["weakDamageMultiplier"]);
        Assert.Same(damage, combat.Damage);
        Assert.Same(damage.HitPolicy, combat.HitResolution);
        Assert.Same(damage.CriticalEligibilityPolicy, combat.CriticalEligibility);
        Assert.Same(damage.CriticalChancePolicy, combat.CriticalChance);
        Assert.IsType<SplitChargePolicy>(combat.Charges);
        Assert.Equal("split", combat.EffectiveConfiguration["chargePolicy"]);
        Assert.Same(damage, combat.InstantDefeat);
        Assert.Same(damage.InstantDefeatPolicy, combat.InstantDefeatResolution);
        Assert.Same(damage, combat.Ailments);
        Assert.Same(damage, combat.Chance);
        Assert.Same(damage, combat.Amounts);
        var actionOutcomes = Assert.IsType<StandardActionOutcomeAggregationPolicy>(combat.ActionOutcomes);
        Assert.Equal(ItemActionOutcomeBehavior.Normal, actionOutcomes.Config.ItemBehavior);
        Assert.Equal("normal", combat.EffectiveConfiguration["itemActionOutcomeBehavior"]);

        IBattleRewardService rewards = resolver.BindBattleRewardService(
            catalog,
            Qualified("standard_reward_sample"),
            new SequenceRandomSource(units: [0.5m]))
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
        ContentId[] policyIds = registry.CombatPolicyIds
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

        var standardCombat = new ProductionCombatRuleset(new SequenceRandomSource());
        CombatExecutionPolicySet combat = Policies(standardCombat, damagePolicyId);
        IBattleRewardService reward = new BattleRewardService(
            new StandardBattleRewardYieldPolicy(new SequenceRandomSource()));
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
            combat: [new FixedCombatFactory(damagePolicyId, combat)],
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

        Assert.Same(combat, resolver.BindCombatPolicies(
            catalog,
            Id("test.pack:damage"),
            new SequenceRandomSource(),
            stat.StageScalingPolicy).RequireService());
        Assert.Same(reward, resolver.BindBattleRewardService(
            catalog, Id("test.pack:reward"), new SequenceRandomSource()).RequireService());
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
    public void CombatPolicySet_ExposesTheAuthoritiesUsedByItsComposedExecutors()
    {
        var hit = new RecordingMissHitPolicy();
        var criticalEligibility = new AllDamageCriticalEligibilityPolicy();
        var criticalChance = new AccuracyScaledCriticalChancePolicy(new SequenceRandomSource());
        var instantResolution = new StandardInstantDefeatResolutionPolicy(new SequenceRandomSource());
        var damage = new ProductionCombatRuleset(
            new SequenceRandomSource(),
            hitPolicy: hit,
            criticalEligibilityPolicy: criticalEligibility,
            criticalChancePolicy: criticalChance,
            instantDefeatPolicy: instantResolution);
        var charges = new UnifiedChargePolicy();
        var ailments = new ProductionCombatRuleset(new SequenceRandomSource());
        var chance = new ProductionCombatRuleset(new SequenceRandomSource());
        var amounts = new ProductionCombatRuleset(new SequenceRandomSource());
        var outcomes = new StandardActionOutcomeAggregationPolicy();
        var mutableParameters = new Dictionary<string, object?>
        {
            ["mode"] = "custom",
            ["weights"] = new List<object?> { 1m, 2m }
        };

        var policies = new CombatExecutionPolicySet(
            Id("test.pack:custom_combat"),
            Id("custom_combat"),
            damage,
            charges,
            damage,
            ailments,
            chance,
            amounts,
            outcomes,
            mutableParameters);
        mutableParameters["mode"] = "changed";
        ((List<object?>)mutableParameters["weights"]!).Add(3m);

        Assert.Same(damage, policies.Damage);
        Assert.Same(hit, policies.HitResolution);
        Assert.Same(criticalEligibility, policies.CriticalEligibility);
        Assert.Same(criticalChance, policies.CriticalChance);
        Assert.Same(charges, policies.Charges);
        Assert.Same(damage, policies.InstantDefeat);
        Assert.Same(instantResolution, policies.InstantDefeatResolution);
        Assert.Same(ailments, policies.Ailments);
        Assert.Same(chance, policies.Chance);
        Assert.Same(amounts, policies.Amounts);
        Assert.Same(outcomes, policies.ActionOutcomes);
        Assert.Equal("custom", policies.AuthoredParameters["mode"]);
        Assert.Equal("custom", policies.EffectiveConfiguration["mode"]);
        Assert.Equal(
            [1m, 2m],
            Assert.IsAssignableFrom<IReadOnlyList<object?>>(policies.AuthoredParameters["weights"]));

        ProductionDamageResolutionResult resolution = damage.ResolveDamage(
            new ProductionDamageResolutionRequest(
                new ProductionCombatantProfile(1, new ProductionCombatStats(10, 10, 10, 10, 10)),
                new ProductionCombatantProfile(1, new ProductionCombatStats(10, 10, 10, 10, 10)),
                DamageElement.Physical,
                ElementalAffinity.Normal,
                10,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1)));

        Assert.False(Assert.Single(resolution.Hits).Hit);
        Assert.Equal(1, hit.CallCount);
    }

    [Fact]
    public void HostRegistry_SnapshotsFactoriesAndRejectsDuplicateOrQualifiedPolicyIds()
    {
        ContentId policyId = Id("host_damage");
        var combat = Policies(new ProductionCombatRuleset(new SequenceRandomSource()), policyId);
        var factories = new List<IRuntimeCombatRulesetPolicyFactory>
        {
            new FixedCombatFactory(policyId, combat)
        };
        var registry = new RuntimeRulesetPolicyFactoryRegistry(combat: factories);
        factories.Clear();

        Assert.Equal([policyId], registry.CombatPolicyIds);
        Assert.Throws<ArgumentException>(() => new RuntimeRulesetPolicyFactoryRegistry(
            combat:
            [
                new FixedCombatFactory(policyId, combat),
                new FixedCombatFactory(policyId, combat)
            ]));
        Assert.Throws<ArgumentException>(() => new RuntimeRulesetPolicyFactoryRegistry(
            combat: [new FixedCombatFactory(Id("test.pack:qualified"), combat)]));
    }

    [Fact]
    public void Resolver_DoesNotExposeAFactoryServiceWhenTheFactoryReportsDiagnostics()
    {
        ContentId policyId = Id("host_rejected_damage");
        ContentId rulesetId = Id("test.pack:rejected_damage");
        var factory = new DiagnosticCombatFactory(
            policyId,
            Policies(new ProductionCombatRuleset(new SequenceRandomSource()), policyId));
        var resolver = new RuntimeRulesetBindingResolver(
            new RuntimeRulesetPolicyFactoryRegistry(combat: [factory]));

        RulesetBindingResult<CombatExecutionPolicySet> result = resolver.BindCombatPolicies(
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
            combat: [factory],
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
            RulesetCategory.Damage => Assert.Single(resolver.BindCombatPolicies(
                catalog,
                definition.Id,
                new SequenceRandomSource(),
                StagePolicy()).Diagnostics),
            RulesetCategory.Reward => Assert.Single(resolver.BindBattleRewardService(
                catalog,
                definition.Id,
                new SequenceRandomSource()).Diagnostics),
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
            combat: [new CancelingCombatFactory(policyId)]));

        Assert.Throws<OperationCanceledException>(() => resolver.BindCombatPolicies(
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

        CombatExecutionPolicySet policies = CreateResolver()
            .BindCombatPolicies(catalog, rulesetId, new SequenceRandomSource(), StagePolicy())
            .RequireService();
        var ruleset = Assert.IsType<ProductionCombatRuleset>(policies.Damage);

        Assert.Equal(2m, ruleset.Config.WeakDamageMultiplier);
        Assert.Equal(0.25m, ruleset.Config.ResistDamageMultiplier);
        Assert.Equal(2m, policies.AuthoredParameters["weakDamageMultiplier"]);
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
                ("maximumHitsPerDamageEffect", 128),
                ("damageFormulaScalar", 6m),
                ("damageVarianceMinimum", 0.8m),
                ("damageVarianceMaximum", 1.2m),
                ("criticalDamageMultiplier", 1.7m),
                ("weakDamageMultiplier", 1.8m),
                ("resistDamageMultiplier", 0.4m),
                ("guardDamageMultiplier", 0.3m),
                ("hitAttackerAgilityCoefficient", 1.25m),
                ("hitTargetAgilityCoefficient", 1.75m),
                ("hitChanceMinimum", 3),
                ("hitChanceMaximum", 97),
                ("instantDeathChanceMinimum", 4),
                ("instantDeathChanceMaximum", 90),
                ("instantDeathVulnerableMultiplier", 1.75m),
                ("instantDeathNormalMultiplier", 0.9m),
                ("instantDeathResistantMultiplier", 0.4m),
                ("instantDeathImmuneMultiplier", 0.05m))));

        CombatExecutionPolicySet policies = CreateResolver()
            .BindCombatPolicies(catalog, rulesetId, new SequenceRandomSource(), StagePolicy())
            .RequireService();
        ProductionCombatRulesetConfig config =
            Assert.IsType<ProductionCombatRuleset>(policies.Damage).Config;

        Assert.Equal(128, config.MaximumHitsPerDamageEffect);
        Assert.Equal(128L, Assert.IsType<long>(policies.EffectiveConfiguration["maximumHitsPerDamageEffect"]));
        Assert.Equal(6m, config.DamageFormulaScalar);
        Assert.Equal(0.8m, config.DamageVarianceMinimum);
        Assert.Equal(1.2m, config.DamageVarianceMaximum);
        Assert.Equal(1.7m, config.CriticalDamageMultiplier);
        Assert.Equal(1.8m, config.WeakDamageMultiplier);
        Assert.Equal(0.4m, config.ResistDamageMultiplier);
        Assert.Equal(0.3m, config.GuardDamageMultiplier);
        Assert.Equal(1.25m, config.HitAttackerAgilityCoefficient);
        Assert.Equal(1.75m, config.HitTargetAgilityCoefficient);
        Assert.Equal(3, config.HitChanceMinimum);
        Assert.Equal(97, config.HitChanceMaximum);
        Assert.Equal(4, config.InstantDeathChanceMinimum);
        Assert.Equal(90, config.InstantDeathChanceMaximum);
        Assert.Equal(1.75m, config.InstantDeathVulnerableMultiplier);
        Assert.Equal(0.9m, config.InstantDeathNormalMultiplier);
        Assert.Equal(0.4m, config.InstantDeathResistantMultiplier);
        Assert.Equal(0.05m, config.InstantDeathImmuneMultiplier);
    }

    [Fact]
    public void DamageBinding_ConfiguresItemActionOutcomesAndRejectsInvalidValues()
    {
        ContentId configuredId = Id("test.pack:item_outcomes");
        GameDataCatalog configuredCatalog = Catalog(new RulesetDefinition(
            configuredId,
            "Item Outcomes",
            "Effect-driven offensive items.",
            RulesetCategory.Damage,
            StandardRulesetPolicyIds.StandardDamage,
            Parameters(("itemActionOutcomeBehavior", "effect_driven"))));

        CombatExecutionPolicySet configured = CreateResolver()
            .BindCombatPolicies(configuredCatalog, configuredId, new SequenceRandomSource(), StagePolicy())
            .RequireService();
        var policy = Assert.IsType<StandardActionOutcomeAggregationPolicy>(configured.ActionOutcomes);

        Assert.Equal(ItemActionOutcomeBehavior.EffectDriven, policy.Config.ItemBehavior);
        Assert.Equal("effect_driven", configured.EffectiveConfiguration["itemActionOutcomeBehavior"]);

        foreach ((object value, RulesetBindingDiagnosticCode expectedCode) in new[]
                 {
                     ((object)42, RulesetBindingDiagnosticCode.InvalidParameterType),
                     ((object)"surprising", RulesetBindingDiagnosticCode.InvalidParameterValue)
                 })
        {
            ContentId invalidId = Id($"test.pack:invalid_item_outcome_{expectedCode}");
            RulesetBindingResult<CombatExecutionPolicySet> invalid = CreateResolver().BindCombatPolicies(
                Catalog(new RulesetDefinition(
                    invalidId,
                    "Invalid Item Outcomes",
                    "Invalid item outcome configuration.",
                    RulesetCategory.Damage,
                    StandardRulesetPolicyIds.StandardDamage,
                    Parameters(("itemActionOutcomeBehavior", value)))),
                invalidId,
                new SequenceRandomSource(),
                StagePolicy());

            RulesetBindingDiagnostic diagnostic = Assert.Single(invalid.Diagnostics);
            Assert.Equal(expectedCode, diagnostic.Code);
            Assert.Equal("itemActionOutcomeBehavior", diagnostic.ParameterName);
        }
    }

    [Theory]
    [InlineData("split", typeof(SplitChargePolicy))]
    [InlineData("unified", typeof(UnifiedChargePolicy))]
    [InlineData("disabled", typeof(DisabledChargePolicy))]
    public void DamageBinding_SelectsAuthoredChargeComposition(
        string authoredValue,
        Type expectedPolicyType)
    {
        ContentId rulesetId = Id($"test.pack:{authoredValue}_charges");
        GameDataCatalog catalog = Catalog(new RulesetDefinition(
            rulesetId,
            "Charge Composition",
            "Selects one supplied charge policy.",
            RulesetCategory.Damage,
            StandardRulesetPolicyIds.StandardDamage,
            Parameters(("chargePolicy", authoredValue))));

        CombatExecutionPolicySet policies = CreateResolver()
            .BindCombatPolicies(catalog, rulesetId, new SequenceRandomSource(), StagePolicy())
            .RequireService();

        Assert.IsType(expectedPolicyType, policies.Charges);
        Assert.Equal(authoredValue, policies.EffectiveConfiguration["chargePolicy"]);
    }

    [Theory]
    [InlineData(42, RulesetBindingDiagnosticCode.InvalidParameterType)]
    [InlineData("unknown", RulesetBindingDiagnosticCode.InvalidParameterValue)]
    public void DamageBinding_RejectsInvalidChargeComposition(
        object authoredValue,
        RulesetBindingDiagnosticCode expectedCode)
    {
        ContentId rulesetId = Id($"test.pack:invalid_charge_{expectedCode}");
        RulesetBindingResult<CombatExecutionPolicySet> result = CreateResolver().BindCombatPolicies(
            Catalog(new RulesetDefinition(
                rulesetId,
                "Invalid Charge Composition",
                "Rejects an unsupported charge policy selection.",
                RulesetCategory.Damage,
                StandardRulesetPolicyIds.StandardDamage,
                Parameters(("chargePolicy", authoredValue)))),
            rulesetId,
            new SequenceRandomSource(),
            StagePolicy());

        RulesetBindingDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal("chargePolicy", diagnostic.ParameterName);
        Assert.Null(result.Service);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1025)]
    public void DamageBinding_RejectsHitCountLimitsOutsideThePublishedContentDomain(int maximumHits)
    {
        ContentId rulesetId = Id($"test.pack:invalid_hit_limit_{maximumHits}");
        RulesetBindingResult<CombatExecutionPolicySet> result = CreateResolver().BindCombatPolicies(
            Catalog(new RulesetDefinition(
                rulesetId,
                "Invalid Hit Limit",
                "The supplied combat policy rejects unsafe hit ceilings.",
                RulesetCategory.Damage,
                StandardRulesetPolicyIds.StandardDamage,
                Parameters(("maximumHitsPerDamageEffect", maximumHits)))),
            rulesetId,
            new SequenceRandomSource(),
            StagePolicy());

        Assert.False(result.IsSuccess);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidParameterValue, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void RewardBinding_OwnsRewardConfigurationWithoutACombatRulesetDependency()
    {
        ContentId rulesetId = Id("test.pack:configured_reward");
        GameDataCatalog catalog = Catalog(new RulesetDefinition(
            rulesetId,
            "Configured Reward",
            "Every standard reward input belongs to the reward category.",
            RulesetCategory.Reward,
            StandardRulesetPolicyIds.StandardReward,
            Parameters(
                ("enemiesPerLevelForExperience", 60m),
                ("expectedStatLevelMultiplier", 4m),
                ("expectedStatBase", 20m),
                ("statDensityDivisor", 120m),
                ("maximumStatDensityMultiplier", 3m),
                ("currencyBaseMultiplier", 0.3m),
                ("currencyLuckMultiplier", 6m),
                ("currencyVarianceMinimum", 0.85m),
                ("currencyVarianceMaximum", 1.15m))));

        IBattleRewardService bound = CreateResolver().BindBattleRewardService(
            catalog,
            rulesetId,
            new SequenceRandomSource(units: [0.5m])).RequireService();
        var service = Assert.IsType<BattleRewardService>(bound);
        var policy = Assert.IsType<StandardBattleRewardYieldPolicy>(service.YieldPolicy);

        Assert.Equal(60m, policy.Config.EnemiesPerLevelForExperience);
        Assert.Equal(4m, policy.Config.ExpectedStatLevelMultiplier);
        Assert.Equal(20m, policy.Config.ExpectedStatBase);
        Assert.Equal(120m, policy.Config.StatDensityDivisor);
        Assert.Equal(3m, policy.Config.MaximumStatDensityMultiplier);
        Assert.Equal(0.3m, policy.Config.CurrencyBaseMultiplier);
        Assert.Equal(6m, policy.Config.CurrencyLuckMultiplier);
        Assert.Equal(0.85m, policy.Config.CurrencyVarianceMinimum);
        Assert.Equal(1.15m, policy.Config.CurrencyVarianceMaximum);
    }

    [Theory]
    [InlineData("enemiesPerLevelForExperience")]
    [InlineData("initiativeVarianceMinimum")]
    public void DamageBinding_RejectsParametersOwnedByOtherPolicyBoundaries(string parameterName)
    {
        ContentId rulesetId = Id("test.pack:mixed_authority");
        RulesetBindingResult<CombatExecutionPolicySet> result = CreateResolver().BindCombatPolicies(
            Catalog(new RulesetDefinition(
                rulesetId,
                "Mixed Authority",
                "Damage cannot configure rewards or initiative.",
                RulesetCategory.Damage,
                StandardRulesetPolicyIds.StandardDamage,
                Parameters((parameterName, 1m)))),
            rulesetId,
            new SequenceRandomSource(),
            StagePolicy());

        Assert.False(result.IsSuccess);
        RulesetBindingDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RulesetBindingDiagnosticCode.UnknownParameter, diagnostic.Code);
        Assert.Equal(parameterName, diagnostic.ParameterName);
    }

    [Fact]
    public void RewardBinding_RejectsUnknownInvalidAndWrongCategoryDefinitionsBeforeUse()
    {
        ContentId unknownId = Id("test.pack:unknown_reward_parameter");
        ContentId invalidId = Id("test.pack:invalid_reward_parameter");
        ContentId wrongCategoryId = Id("test.pack:wrong_reward_category");
        RuntimeRulesetBindingResolver resolver = CreateResolver();

        RulesetBindingResult<IBattleRewardService> unknown = resolver.BindBattleRewardService(
            Catalog(new RulesetDefinition(
                unknownId,
                "Unknown Reward Parameter",
                "Unknown fields must not be ignored.",
                RulesetCategory.Reward,
                StandardRulesetPolicyIds.StandardReward,
                Parameters(("mystery", "value")))),
            unknownId,
            new SequenceRandomSource());
        RulesetBindingResult<IBattleRewardService> invalid = resolver.BindBattleRewardService(
            Catalog(new RulesetDefinition(
                invalidId,
                "Invalid Reward Parameter",
                "Invalid configuration cannot produce a service.",
                RulesetCategory.Reward,
                StandardRulesetPolicyIds.StandardReward,
                Parameters(("statDensityDivisor", 0m)))),
            invalidId,
            new SequenceRandomSource());
        RulesetBindingResult<IBattleRewardService> wrongCategory = resolver.BindBattleRewardService(
            Catalog(new RulesetDefinition(
                wrongCategoryId,
                "Wrong Reward Category",
                "Damage records cannot bind as rewards.",
                RulesetCategory.Damage,
                StandardRulesetPolicyIds.StandardReward)),
            wrongCategoryId,
            new SequenceRandomSource());

        Assert.Equal(RulesetBindingDiagnosticCode.UnknownParameter, Assert.Single(unknown.Diagnostics).Code);
        Assert.Equal(RulesetBindingDiagnosticCode.InvalidParameterValue, Assert.Single(invalid.Diagnostics).Code);
        Assert.Equal(RulesetBindingDiagnosticCode.CategoryMismatch, Assert.Single(wrongCategory.Diagnostics).Code);
        Assert.Null(unknown.Service);
        Assert.Null(invalid.Service);
        Assert.Null(wrongCategory.Service);
    }

    [Fact]
    public void Initiative_IsReplaceableThroughANeutralPolicyInterface()
    {
        IBattleInitiativeRollPolicy policy = new StandardBattleInitiativeRollPolicy(
            new SequenceRandomSource(units: [0.5m, 0.5m]),
            new StandardBattleInitiativeRollPolicyConfig
            {
                VarianceMinimum = 0.8m,
                VarianceMaximum = 1.2m
            });

        Assert.True(policy.IsPlayerFirst(20m, 20m));
        Assert.False(policy.IsPlayerFirst(1m, 100m));
    }

    [Theory]
    [InlineData("defaultHitAccuracy")]
    [InlineData("defaultInstantDeathChance")]
    [InlineData("criticalChanceMinimum")]
    [InlineData("criticalChanceMaximum")]
    [InlineData("criticalChanceBase")]
    public void DamageBinding_RejectsRemovedProbabilityDefaults(string parameterName)
    {
        ContentId rulesetId = Id("test.pack:removed_probability_default");
        GameDataCatalog catalog = Catalog(new RulesetDefinition(
            rulesetId,
            "Removed Probability Default",
            "Explicitly authored effects do not use fallback probability values.",
            RulesetCategory.Damage,
            StandardRulesetPolicyIds.StandardDamage,
            Parameters((parameterName, 50))));

        RulesetBindingResult<CombatExecutionPolicySet> result = CreateResolver()
            .BindCombatPolicies(
                catalog,
                rulesetId,
                new SequenceRandomSource(),
                StagePolicy());

        Assert.False(result.IsSuccess);
        RulesetBindingDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RulesetBindingDiagnosticCode.UnknownParameter, diagnostic.Code);
        Assert.Equal(parameterName, diagnostic.ParameterName);
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

        CombatExecutionPolicySet policies = CreateResolver()
            .BindCombatPolicies(catalog, rulesetId, new SequenceRandomSource(), StagePolicy())
            .RequireService();
        var ruleset = Assert.IsType<ProductionCombatRuleset>(policies.Damage);
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

        RulesetBindingResult<CombatExecutionPolicySet> missing = resolver.BindCombatPolicies(
            empty,
            Id("test.pack:missing"),
            new SequenceRandomSource(),
            StagePolicy());
        Assert.False(missing.IsSuccess);
        Assert.Equal(RulesetBindingDiagnosticCode.MissingRuleset, Assert.Single(missing.Diagnostics).Code);

        ContentId wrongCategoryId = Id("test.pack:wrong_category");
        RulesetBindingResult<CombatExecutionPolicySet> wrongCategory = resolver.BindCombatPolicies(
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
        RulesetBindingResult<CombatExecutionPolicySet> unsupported = resolver.BindCombatPolicies(
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
        RulesetBindingResult<CombatExecutionPolicySet> badParameters = resolver.BindCombatPolicies(
            Catalog(new RulesetDefinition(
                badParametersId,
                "Bad Parameters",
                "Invalid parameter binding.",
                RulesetCategory.Damage,
                StandardRulesetPolicyIds.StandardDamage,
                [
                    new KeyValuePair<string, object?>("weakDamageMultiplier", "loud"),
                    new KeyValuePair<string, object?>("resistDamageMultiplier", -1m),
                    new KeyValuePair<string, object?>("hitAttackerAgilityCoefficient", "fast"),
                    new KeyValuePair<string, object?>("hitTargetAgilityCoefficient", -1m),
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
            diagnostic.Code == RulesetBindingDiagnosticCode.InvalidParameterType &&
            diagnostic.ParameterName == "hitAttackerAgilityCoefficient");
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
        RulesetBindingResult<CombatExecutionPolicySet> result =
            CreateResolver().BindCombatPolicies(
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

    private static CombatExecutionPolicySet Policies(
        ProductionCombatRuleset standard,
        ContentId policyId) =>
        new(
            Id("test.pack:combat"),
            policyId,
            standard,
            new SplitChargePolicy(),
            standard,
            standard,
            standard,
            standard,
            new StandardActionOutcomeAggregationPolicy());

    private sealed class RecordingMissHitPolicy : IHitResolutionPolicy
    {
        public int CallCount { get; private set; }

        public HitResolutionResult Resolve(HitResolutionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            CallCount++;
            return new HitResolutionResult(
                false,
                request.AuthoredAccuracy,
                0m,
                0m,
                request.AuthoredAccuracy,
                0m,
                request.AuthoredAccuracy,
                0m,
                request.AuthoredAccuracy,
                request.AuthoredAccuracy,
                100m);
        }
    }

    private sealed class FixedCombatFactory(
        ContentId policyId,
        CombatExecutionPolicySet service) : IRuntimeCombatRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<CombatExecutionPolicySet> Create(
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
            IRandomSource random) =>
            new(service);
    }

    private sealed class DiagnosticCombatFactory(
        ContentId policyId,
        CombatExecutionPolicySet service) : IRuntimeCombatRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<CombatExecutionPolicySet> Create(
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
        IRuntimeCombatRulesetPolicyFactory,
        IRuntimeRewardRulesetPolicyFactory,
        IRuntimeStatRulesetPolicyFactory,
        IRuntimeStatModifierRulesetPolicyFactory,
        IRuntimeGrowthRulesetPolicyFactory,
        IRuntimeRosterCapacityRulesetPolicyFactory,
        IRuntimeEconomyRulesetPolicyFactory,
        IRuntimeTurnEconomyRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        RulesetBindingResult<CombatExecutionPolicySet> IRuntimeCombatRulesetPolicyFactory.Create(
            RulesetDefinition definition,
            IRandomSource random,
            IStatStageScalingPolicy stageScalingPolicy) =>
            Fail<RulesetBindingResult<CombatExecutionPolicySet>>();

        RulesetBindingResult<IBattleRewardService> IRuntimeRewardRulesetPolicyFactory.Create(
            RulesetDefinition definition,
            IRandomSource random) => Fail<RulesetBindingResult<IBattleRewardService>>();

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

    private sealed class CancelingCombatFactory(ContentId policyId) : IRuntimeCombatRulesetPolicyFactory
    {
        public ContentId PolicyId { get; } = policyId;

        public RulesetBindingResult<CombatExecutionPolicySet> Create(
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
