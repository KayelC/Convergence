using Convergence.Content;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class ProductionCombatRulesetTests
{
    public static IEnumerable<object[]> ElementCases()
    {
        foreach (DamageElement element in Enum.GetValues<DamageElement>())
        {
            yield return [element];
        }
    }

    public static IEnumerable<object[]> AffinityDamageCases()
    {
        yield return [ElementalAffinity.Weak, 75m];
        yield return [ElementalAffinity.Normal, 50m];
        yield return [ElementalAffinity.Resist, 25m];
        yield return [ElementalAffinity.Null, 50m];
        yield return [ElementalAffinity.Repel, 50m];
        yield return [ElementalAffinity.Absorb, 50m];
    }

    [Theory]
    [MemberData(nameof(ElementCases))]
    public void DamagePolicy_ResolvesEveryCleanDamageElement(DamageElement element)
    {
        ProductionCombatRuleset ruleset = Rules(0.5m);

        ProductionDamageResolutionResult result = ruleset.ResolveDamage(new ProductionDamageResolutionRequest(
            Actor(),
            Actor(),
            element,
            ElementalAffinity.Normal,
            100,
            100,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1)));

        ProductionDamageResolutionHit hit = Assert.Single(result.Hits);
        Assert.True(hit.Hit);
        Assert.False(hit.Critical);
        Assert.Equal(50, hit.Damage);
    }

    [Theory]
    [MemberData(nameof(AffinityDamageCases))]
    public void DamagePolicy_UsesApprovedAffinityMultipliers(
        ElementalAffinity affinity,
        decimal expectedDamage)
    {
        ProductionCombatRuleset ruleset = Rules();

        ProductionDamageResolutionResult result = ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                Actor(),
                Actor(),
                DamageElement.Fire,
                affinity,
                100,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1)));

        Assert.Equal(expectedDamage, Assert.Single(result.Hits).Damage);
        Assert.Equal(affinity, result.ResolvedAffinity);
    }

    [Fact]
    public void AuthoredChargeMultiplier_AppliesToEveryResolvedHitAfterBaseDamage()
    {
        ProductionCombatRuleset ruleset = Rules();

        ProductionDamageResolutionResult result = ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                Actor(),
                Actor(),
                DamageElement.Physical,
                ElementalAffinity.Normal,
                100,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(3, 3),
                chargeMultiplier: 2.5m,
                chargeKind: ChargeKind.Physical));

        Assert.Equal(3, result.Hits.Count);
        Assert.All(result.Hits, hit => Assert.Equal(125m, hit.Damage));
        Assert.Equal(375m, result.TotalDamage);
    }

    [Fact]
    public void DamageResolution_PreservesOrderedHitCriticalAffinityAndChargeEvidence()
    {
        ProductionCombatRuleset ruleset = Rules(0.42m, 0.12m, 0.5m);

        ProductionDamageResolutionResult result = ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                Actor(),
                Actor(),
                DamageElement.Physical,
                ElementalAffinity.Weak,
                100,
                80,
                new ChanceCriticalDefinition(30),
                new HitCountDefinition(1, 1),
                chargeMultiplier: 2m,
                chargeKind: ChargeKind.Physical));

        ProductionDamageResolutionHit hit = Assert.Single(result.Hits);
        Assert.Equal(0, hit.HitIndex);
        Assert.True(hit.Hit);
        Assert.True(hit.Critical);
        Assert.Equal(80, hit.HitResolution.AuthoredAccuracy);
        Assert.Equal(80, hit.HitResolution.FinalChance);
        Assert.Equal(42m, hit.HitResolution.Roll);
        Assert.NotNull(hit.CriticalResolution);
        Assert.True(hit.CriticalResolution!.Eligible);
        Assert.Equal(30, hit.CriticalResolution.Chance);
        Assert.Equal(12m, hit.CriticalResolution.Roll);
        Assert.Equal(ElementalAffinity.Weak, hit.ResolvedAffinity);
        Assert.Equal(ChargeKind.Physical, hit.ChargeKind);
        Assert.Equal(2m, hit.ChargeMultiplier);
    }

    [Fact]
    public void SharedContactBypassesOnlyHitResolutionAndKeepsItsOwnDamageRules()
    {
        var hitPolicy = new RecordingHitPolicy(hit: false);
        var ruleset = new ProductionCombatRuleset(
            new SequenceRandomSource([]),
            hitPolicy: hitPolicy,
            criticalEligibilityPolicy: new AllDamageCriticalEligibilityPolicy());

        ProductionDamageResolutionResult independent = ruleset.ResolveDamage(
            DamageRequest(DamageContactMode.Independent));
        ProductionDamageResolutionResult shared = ruleset.ResolveDamage(
            DamageRequest(DamageContactMode.SharedContact));

        Assert.False(Assert.Single(independent.Hits).Hit);
        ProductionDamageResolutionHit sharedHit = Assert.Single(shared.Hits);
        Assert.True(sharedHit.Hit);
        Assert.True(sharedHit.Critical);
        Assert.Equal(ElementalAffinity.Weak, sharedHit.ResolvedAffinity);
        Assert.Equal(ChargeKind.Magical, sharedHit.ChargeKind);
        Assert.Equal(2m, sharedHit.ChargeMultiplier);
        Assert.Equal(1, hitPolicy.CallCount);

        ProductionDamageResolutionRequest DamageRequest(DamageContactMode mode) => new(
            Actor(),
            Actor(),
            DamageElement.Fire,
            ElementalAffinity.Weak,
            10,
            25,
            new ChanceCriticalDefinition(100),
            new HitCountDefinition(1, 1),
            2m,
            ChargeKind.Magical,
            accuracyModifiers: null,
            evasionModifiers: null,
            criticalChanceModifiers: null,
            mode);
    }

    [Fact]
    public void GuardHalvesDamageSuppressesCriticalAndNormalizesWeakness()
    {
        ProductionCombatRuleset ruleset = Rules();

        ProductionDamageResolutionResult result = ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                Actor(),
                Actor(status: new ProductionCombatStatus(IsGuarding: true)),
                DamageElement.Physical,
                ElementalAffinity.Weak,
                100,
                100,
                new ChanceCriticalDefinition(100),
                new HitCountDefinition(1, 1)));

        ProductionDamageResolutionHit hit = Assert.Single(result.Hits);
        Assert.Equal(25, hit.Damage);
        Assert.Equal(ElementalAffinity.Normal, result.ResolvedAffinity);
        Assert.False(hit.Critical);
    }

    [Fact]
    public void DamagePolicyResolution_IsImmutableAndRequiresADefinedAffinity()
    {
        var source = new List<DamageHitResolution>
        {
            new(true, 12m),
            new(false, 0m)
        };
        var result = new DamagePolicyResolution(source, ElementalAffinity.Resist);

        source.Add(new DamageHitResolution(true, 99m));

        Assert.Equal([0, 1], result.Hits.Select(hit => hit.HitIndex));
        Assert.All(result.Hits, hit => Assert.Equal(ElementalAffinity.Resist, hit.ResolvedAffinity));
        Assert.Equal(ElementalAffinity.Resist, result.ResolvedAffinity);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<DamageHitResolution>)result.Hits).Add(new DamageHitResolution(true, 1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DamagePolicyResolution([], (ElementalAffinity)int.MaxValue));
    }

    [Fact]
    public void RigidBodyForcesPhysicalCriticalButNotMagicalCritical()
    {
        ProductionCombatRuleset ruleset = Rules();
        ProductionCombatantProfile target = Actor(status: new ProductionCombatStatus(IsRigidBody: true));

        ProductionCriticalCheckResult physical = ruleset.CheckCritical(new ProductionCriticalCheckRequest(
            Actor(),
            target,
            DamageElement.Physical,
            new ChanceCriticalDefinition(1),
            authoredAccuracy: 100,
            finalHitChance: 100));
        ProductionCriticalCheckResult magical = ruleset.CheckCritical(new ProductionCriticalCheckRequest(
            Actor(),
            target,
            DamageElement.Fire,
            new ChanceCriticalDefinition(100),
            authoredAccuracy: 100,
            finalHitChance: 100));

        Assert.True(physical.Critical);
        Assert.Equal(100, physical.Chance);
        Assert.False(magical.Critical);
    }

    [Fact]
    public void HitCheckUsesAccuracyAgilityMultipliersAndRigidBypass()
    {
        ProductionCombatRuleset ruleset = Rules(0.5m);
        var attacker = Actor(stats: new ProductionCombatStats(20, 20, 20, 20, 20));
        var evasiveTarget = Actor(
            stats: new ProductionCombatStats(20, 20, 20, 40, 40),
            modifiers: new ProductionCombatModifiers(EvasionMultiplier: 0.6m));

        HitResolutionResult result = ruleset.CheckHit(new ProductionHitCheckRequest(
            attacker,
            evasiveTarget,
            authoredAccuracy: 80));
        HitResolutionResult rigid = ruleset.CheckHit(new ProductionHitCheckRequest(
            attacker,
            Actor(status: new ProductionCombatStatus(IsRigidBody: true)),
            authoredAccuracy: 1));

        Assert.True(result.Hit);
        Assert.Equal(72, result.FinalChance);
        Assert.True(rigid.Hit);
        Assert.Equal(100, rigid.FinalChance);
    }

    [Fact]
    public void InstantDeathBlocksImmuneChannelsButBypassModeStillUsesChance()
    {
        ProductionCombatRuleset immuneRuleset = Rules();
        ProductionInstantDeathResult immune = immuneRuleset.ResolveInstantDeath(
            new ProductionInstantDeathRequest(
                Actor(),
                Actor(),
                BaseChance: 100,
                ResistanceLevel.Immune));
        ProductionCombatRuleset bypassRuleset = Rules(0m);
        ProductionInstantDeathResult bypass = bypassRuleset.ResolveInstantDeath(
            new ProductionInstantDeathRequest(
                Actor(),
                Actor(),
                BaseChance: 100,
                ResistanceLevel.Immune,
                BypassesResistance: true));

        Assert.False(immune.Defeated);
        Assert.Equal(0, immune.Chance);
        Assert.True(bypass.Defeated);
        Assert.Equal(100, bypass.Chance);
        Assert.True(bypass.BypassedResistance);
        Assert.Equal(InstantDefeatResolutionReason.Defeated, bypass.Reason);
    }

    [Fact]
    public void InstantDefeatChance_DoesNotUseAttackerOrTargetLuck()
    {
        var lowAttackerLuck = new ProductionCombatRuleset(new SequenceRandomSource([0.3m]));
        var highAttackerLuck = new ProductionCombatRuleset(new SequenceRandomSource([0.3m]));

        ProductionInstantDeathResult first = lowAttackerLuck.ResolveInstantDeath(
            new ProductionInstantDeathRequest(
                Actor(stats: new ProductionCombatStats(10, 10, 10, 10, 1)),
                Actor(stats: new ProductionCombatStats(10, 10, 10, 10, 99)),
                BaseChance: 40,
                ResistanceLevel.Normal));
        ProductionInstantDeathResult second = highAttackerLuck.ResolveInstantDeath(
            new ProductionInstantDeathRequest(
                Actor(stats: new ProductionCombatStats(10, 10, 10, 10, 99)),
                Actor(stats: new ProductionCombatStats(10, 10, 10, 10, 1)),
                BaseChance: 40,
                ResistanceLevel.Normal));

        Assert.Equal(40, first.Chance);
        Assert.Equal(first.Chance, second.Chance);
        Assert.Equal(first.Defeated, second.Defeated);
    }

    [Fact]
    public void RewardPoliciesPreserveEstablishedExperienceAndCurrencyVectors()
    {
        IBattleRewardYieldPolicy policy = new StandardBattleRewardYieldPolicy(
            new SequenceRandomSource([0.5m]));
        var enemy = new BattleRewardEnemySnapshot(
            ContentId.Parse("reward_enemy"),
            10,
            20,
            20,
            20,
            20,
            20);

        Assert.Equal(46, policy.CalculateExperienceYield(enemy));
        Assert.Equal(125, policy.CalculateCurrencyYield(enemy));
    }

    [Fact]
    public void InitiativeUsesConfiguredAgilityVariance()
    {
        IBattleInitiativeRollPolicy policy = new StandardBattleInitiativeRollPolicy(
            new SequenceRandomSource([0.5m, 0.5m, 0.5m, 0.5m]));

        Assert.True(policy.IsPlayerFirst(playerAverageAgility: 20, enemyAverageAgility: 20));
        Assert.False(policy.IsPlayerFirst(playerAverageAgility: 1, enemyAverageAgility: 100));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void SuppliedPoliciesRejectOutOfRangeUnitRandomValues(int rawUnit)
    {
        decimal unit = rawUnit;
        var damage = new ProductionCombatRuleset(new FixedRandomSource(unit));
        var ailments = new ProductionCombatRuleset(new FixedRandomSource(unit));
        IBattleInitiativeRollPolicy initiative = new StandardBattleInitiativeRollPolicy(
            new FixedRandomSource(unit));
        IBattleRewardYieldPolicy rewards = new StandardBattleRewardYieldPolicy(
            new FixedRandomSource(unit));

        InvalidOperationException damageFailure = Assert.Throws<InvalidOperationException>(() =>
            damage.ResolveDamage(new ProductionDamageResolutionRequest(
                Actor(),
                Actor(),
                DamageElement.Physical,
                ElementalAffinity.Normal,
                100,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))));
        InvalidOperationException ailmentFailure = Assert.Throws<InvalidOperationException>(() =>
            ailments.ResolveAilmentApplication(new ProductionAilmentApplicationRequest(
                Actor(),
                Actor(),
                50,
                ResistanceLevel.Normal)));
        InvalidOperationException initiativeFailure = Assert.Throws<InvalidOperationException>(() =>
            initiative.IsPlayerFirst(20m, 20m));
        InvalidOperationException rewardFailure = Assert.Throws<InvalidOperationException>(() =>
            rewards.CalculateCurrencyYield(new BattleRewardEnemySnapshot(
                ContentId.Parse("invalid_random_reward_enemy"),
                1,
                1m,
                1m,
                1m,
                1m,
                1m)));

        Assert.Contains("[0, 1)", damageFailure.Message, StringComparison.Ordinal);
        Assert.Contains("[0, 1)", ailmentFailure.Message, StringComparison.Ordinal);
        Assert.Contains("[0, 1)", initiativeFailure.Message, StringComparison.Ordinal);
        Assert.Contains("[0, 1)", rewardFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorRejectsUnsafeConfigurationBeforeRuntimeUse()
    {
        ProductionCombatRulesetConfig[] invalidConfigurations =
        [
            new() { HitChanceMinimum = 90, HitChanceMaximum = 10 },
            new() { DamageVarianceMinimum = 1.1m, DamageVarianceMaximum = 0.9m },
            new() { GuardDamageMultiplier = -0.1m }
        ];

        Assert.All(invalidConfigurations, config =>
            Assert.ThrowsAny<ArgumentException>(() =>
                new ProductionCombatRuleset(new SequenceRandomSource([]), config)));
        Assert.ThrowsAny<ArgumentException>(() => new StandardBattleRewardYieldPolicy(
            new SequenceRandomSource([]),
            new StandardBattleRewardYieldPolicyConfig { EnemiesPerLevelForExperience = 0 }));
        Assert.ThrowsAny<ArgumentException>(() => new StandardBattleRewardYieldPolicy(
            new SequenceRandomSource([]),
            new StandardBattleRewardYieldPolicyConfig { StatDensityDivisor = 0 }));
        Assert.ThrowsAny<ArgumentException>(() => new StandardBattleInitiativeRollPolicy(
            new SequenceRandomSource([]),
            new StandardBattleInitiativeRollPolicyConfig
            {
                VarianceMinimum = 1.1m,
                VarianceMaximum = 0.9m
            }));
    }

    [Fact]
    public void UniformHitCountSupportsIntMaximumWithoutOverflowingTheExclusiveBound()
    {
        var ruleset = new ProductionCombatRuleset(new MaximumIntRandomSource());

        int result = ruleset.ResolveHitCount(new HitCountDefinition(
            int.MaxValue - 1,
            int.MaxValue,
            HitDistribution.Uniform));

        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public void UniformHitCountRejectsOutOfRangeRandomSourceResult()
    {
        var ruleset = new ProductionCombatRuleset(new OutOfRangeIntRandomSource());

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            ruleset.ResolveHitCount(new HitCountDefinition(2, 4, HitDistribution.Uniform)));

        Assert.Contains("[0, 3)", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicCombatBoundariesRejectUndefinedVocabularyBeforeResolution()
    {
        const int undefined = 999;
        ProductionCombatantProfile actor = Actor();
        var ruleset = Rules();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductionDamageResolutionRequest(
            actor,
            actor,
            (DamageElement)undefined,
            ElementalAffinity.Normal,
            10,
            0,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductionDamageResolutionRequest(
            actor,
            actor,
            DamageElement.Physical,
            (ElementalAffinity)undefined,
            10,
            0,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ruleset.ResolveHitCount(
            new HitCountDefinition(1, 2, (HitDistribution)undefined)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ruleset.ResolveAilmentApplication(
            new ProductionAilmentApplicationRequest(
                actor,
                actor,
                50,
                (ResistanceLevel)undefined)));
    }

    [Fact]
    public void RuntimeCombatProfileUsesCanonicalActorProgressionLevel()
    {
        var actor = new RuntimeActorState(
            RuntimeInstanceId.Parse("leveled_actor"),
            ContentId.Parse("leveled_entity"),
            ContentId.Parse("player_team"),
            StandardProgressionIds.Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(StandardProgressionIds.Hp, 100, 100)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), ContentId.Parse("player_team")),
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Agility, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Luck, 10)
            ],
            progression: new RuntimeProgressionSnapshot(37, 0, 0, 0));

        ProductionCombatantProfile profile = Rules().CreateCombatantProfile(actor);

        Assert.Equal(37, profile.Level);
    }

    [Fact]
    public void ExtremeRewardInputsSaturateInsteadOfThrowingOrWrapping()
    {
        IBattleRewardYieldPolicy policy = new StandardBattleRewardYieldPolicy(
            new SequenceRandomSource([0.5m]));
        var enemy = new BattleRewardEnemySnapshot(
            ContentId.Parse("maximum_reward_enemy"),
            int.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue);

        Assert.Equal(int.MaxValue, policy.CalculateExperienceYield(enemy));
        Assert.Equal(int.MaxValue, policy.CalculateCurrencyYield(enemy));
    }

    [Fact]
    public void ExtremeCombatInputsSaturateInsteadOfThrowingOrWrapping()
    {
        var config = new ProductionCombatRulesetConfig
        {
            DamageFormulaScalar = decimal.MaxValue,
            DamageVarianceMinimum = decimal.MaxValue,
            DamageVarianceMaximum = decimal.MaxValue,
            CriticalDamageMultiplier = decimal.MaxValue,
            WeakDamageMultiplier = decimal.MaxValue,
            ResistDamageMultiplier = decimal.MaxValue,
            GuardDamageMultiplier = decimal.MaxValue
        };
        var ruleset = new ProductionCombatRuleset(new SequenceRandomSource([0m, 0m, 0m]), config);
        ProductionCombatantProfile attacker = Actor(
            stats: new ProductionCombatStats(
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue),
            modifiers: new ProductionCombatModifiers(
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                int.MaxValue));
        ProductionCombatantProfile target = Actor(
            stats: new ProductionCombatStats(0m, 0m, 0m, 0m, 0m),
            modifiers: new ProductionCombatModifiers(
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                int.MaxValue));

        ProductionDamageResolutionResult damage = ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                attacker,
                target,
                DamageElement.Physical,
                ElementalAffinity.Weak,
                int.MaxValue,
                100,
                new ChanceCriticalDefinition(100),
                new HitCountDefinition(1, 1),
                decimal.MaxValue,
                ChargeKind.Physical));
        HitResolutionResult lowestHitChance = ruleset.CheckHit(
            new ProductionHitCheckRequest(target, attacker, 0));

        Assert.Equal(decimal.MaxValue, Assert.Single(damage.Hits).Damage);
        Assert.Equal(decimal.MaxValue, damage.TotalDamage);
        Assert.Equal(config.HitChanceMinimum, lowestHitChance.FinalChance);
        Assert.Equal(
            100,
            ruleset.CheckCritical(new ProductionCriticalCheckRequest(
                attacker,
                target,
                DamageElement.Physical,
                new ChanceCriticalDefinition(100),
                authoredAccuracy: 100,
                finalHitChance: 100)).Chance);
        Assert.Equal(
            decimal.MaxValue,
            new ProductionDamageResolutionResult(
                [Assert.Single(damage.Hits), Assert.Single(damage.Hits)],
                ElementalAffinity.Normal).TotalDamage);
    }

    [Fact]
    public void RuntimeCombatProfileSaturatesStackedAilmentModifiersAndBonuses()
    {
        var actor = new RuntimeActorState(
            RuntimeInstanceId.Parse("afflicted_actor"),
            ContentId.Parse("afflicted_entity"),
            ContentId.Parse("player_team"),
            StandardProgressionIds.Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(StandardProgressionIds.Hp, 100m, 100m)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), ContentId.Parse("player_team")),
            [
                new KeyValuePair<ContentId, decimal>(
                    StandardProgressionIds.Strength,
                    RuntimeActorNumericDomain.MaximumStatValue),
                new KeyValuePair<ContentId, decimal>(
                    StandardProgressionIds.Magic,
                    RuntimeActorNumericDomain.MaximumStatValue),
                new KeyValuePair<ContentId, decimal>(
                    StandardProgressionIds.Vitality,
                    RuntimeActorNumericDomain.MaximumStatValue),
                new KeyValuePair<ContentId, decimal>(
                    StandardProgressionIds.Agility,
                    RuntimeActorNumericDomain.MaximumStatValue),
                new KeyValuePair<ContentId, decimal>(
                    StandardProgressionIds.Luck,
                    RuntimeActorNumericDomain.MaximumStatValue)
            ]);
        actor.ApplyAilment(ExtremeAilment("extreme_one"), new BattleDurationDefinition());
        actor.ApplyAilment(ExtremeAilment("extreme_two"), new BattleDurationDefinition());

        ProductionCombatantProfile profile = Rules().CreateCombatantProfile(actor);

        Assert.Equal(decimal.MaxValue, profile.Modifiers.DamageDealtMultiplier);
        Assert.Equal(decimal.MaxValue, profile.Modifiers.DamageTakenMultiplier);
        Assert.Equal(decimal.MaxValue, profile.Modifiers.EvasionMultiplier);
        Assert.Equal(int.MaxValue, profile.Modifiers.CriticalChanceTakenBonus);
    }

    private static ProductionCombatRuleset Rules(params decimal[] units) =>
        new(new SequenceRandomSource(units));

    private static ProductionCombatantProfile Actor(
        int level = 1,
        ProductionCombatStats? stats = null,
        ProductionCombatStatus? status = null,
        ProductionCombatModifiers? modifiers = null) =>
        new(level, stats ?? new ProductionCombatStats(20, 20, 20, 20, 20), status, modifiers);

    private static AilmentDefinition ExtremeAilment(string id) =>
        new(
            ContentId.Parse(id),
            id,
            "Exercises saturating combat modifiers.",
            new BattleDurationDefinition(),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(
                decimal.MaxValue,
                int.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                false),
            new AilmentRecoveryDefinition());

    private sealed class SequenceRandomSource : IRandomSource
    {
        private readonly Queue<decimal> _units;

        public SequenceRandomSource(IEnumerable<decimal> units)
        {
            _units = new Queue<decimal>(units);
        }

        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => _units.Count == 0 ? 0.5m : _units.Dequeue();
    }

    private sealed class MaximumIntRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => maximumExclusive - 1;
        public decimal NextUnitDecimal() => 0.999999m;
    }

    private sealed class RecordingHitPolicy(bool hit) : IHitResolutionPolicy
    {
        public int CallCount { get; private set; }

        public HitResolutionResult Resolve(HitResolutionRequest request)
        {
            CallCount++;
            return new HitResolutionResult(
                hit,
                request.AuthoredAccuracy,
                AttackerAgilityContribution: 0m,
                TargetAgilityContribution: 0m,
                AccuracyScoreBeforeModifiers: request.AuthoredAccuracy,
                EvasionScoreBeforeModifiers: 0m,
                ResolvedAccuracyScore: request.AuthoredAccuracy,
                ResolvedEvasionScore: 0m,
                RawChance: request.AuthoredAccuracy,
                FinalChance: request.AuthoredAccuracy,
                Roll: hit ? null : request.AuthoredAccuracy);
        }
    }

    private sealed class FixedRandomSource(decimal unit) : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public decimal NextUnitDecimal() => unit;
    }

    private sealed class OutOfRangeIntRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => maximumExclusive;
        public decimal NextUnitDecimal() => 0m;
    }
}
