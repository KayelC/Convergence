using Convergence.Content;
using Convergence.Hosting;
using Convergence.Battle;
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
        ProductionCombatRuleset ruleset = Rules(0m, 0.5m);

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
            new(true, 12m)
        };
        var result = new DamagePolicyResolution(source, ElementalAffinity.Resist);

        source.Add(new DamageHitResolution(true, 99m));

        Assert.Single(result.Hits);
        Assert.Equal(ElementalAffinity.Resist, result.ResolvedAffinity);
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
            new ChanceCriticalDefinition(1)));
        ProductionCriticalCheckResult magical = ruleset.CheckCritical(new ProductionCriticalCheckRequest(
            Actor(),
            target,
            DamageElement.Fire,
            new ChanceCriticalDefinition(100)));

        Assert.True(physical.Critical);
        Assert.Equal(100, physical.Chance);
        Assert.False(magical.Critical);
    }

    [Fact]
    public void HitCheckUsesAccuracyAgilityLuckMultipliersAndRigidBypass()
    {
        ProductionCombatRuleset ruleset = Rules(0.5m);
        var attacker = Actor(stats: new ProductionCombatStats(20, 20, 20, 20, 20));
        var evasiveTarget = Actor(
            stats: new ProductionCombatStats(20, 20, 20, 40, 40),
            modifiers: new ProductionCombatModifiers(EvasionMultiplier: 0.6m));

        ProductionHitCheckResult result = ruleset.CheckHit(new ProductionHitCheckRequest(
            attacker,
            evasiveTarget,
            BaseAccuracy: 80));
        ProductionHitCheckResult rigid = ruleset.CheckHit(new ProductionHitCheckRequest(
            attacker,
            Actor(status: new ProductionCombatStatus(IsRigidBody: true)),
            BaseAccuracy: 1));

        Assert.True(result.Hit);
        Assert.Equal(52, result.Chance);
        Assert.True(rigid.Hit);
        Assert.Equal(99, rigid.Chance);
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
        Assert.Equal(95, bypass.Chance);
    }

    [Fact]
    public void RewardPoliciesPreserveEstablishedExperienceAndCurrencyVectors()
    {
        ProductionCombatRuleset ruleset = Rules(0.5m);
        ProductionCombatantProfile enemy = Actor(
            level: 10,
            stats: new ProductionCombatStats(20, 20, 20, 20, 20));

        Assert.Equal(46, ruleset.CalculateExperienceYield(enemy));
        Assert.Equal(125, ruleset.CalculateCurrencyYield(enemy));
    }

    [Fact]
    public void InitiativeUsesConfiguredAgilityVariance()
    {
        ProductionCombatRuleset ruleset = Rules(0.5m, 0.5m);

        Assert.True(ruleset.RollInitiative(playerAverageAgility: 20, enemyAverageAgility: 20));
        Assert.False(ruleset.RollInitiative(playerAverageAgility: 1, enemyAverageAgility: 100));
    }

    [Fact]
    public void ConstructorRejectsUnsafeConfigurationBeforeRuntimeUse()
    {
        ProductionCombatRulesetConfig[] invalidConfigurations =
        [
            new() { EnemiesPerLevelForExperience = 0 },
            new() { StatDensityDivisor = 0 },
            new() { HitChanceMinimum = 90, HitChanceMaximum = 10 },
            new() { DamageVarianceMinimum = 1.1m, DamageVarianceMaximum = 0.9m },
            new() { CriticalChanceMaximum = 101 },
            new() { GuardDamageMultiplier = -0.1m }
        ];

        Assert.All(invalidConfigurations, config =>
            Assert.ThrowsAny<ArgumentException>(() =>
                new ProductionCombatRuleset(new SequenceRandomSource([]), config)));
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
    public void RuntimeCombatProfileUsesCanonicalActorProgressionLevel()
    {
        var actor = new RuntimeActorState(
            RuntimeInstanceId.Parse("leveled_actor"),
            ContentId.Parse("leveled_entity"),
            ContentId.Parse("player_team"),
            StandardProgressionIds.Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(StandardProgressionIds.Hp, 100, 100)],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Agility, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Luck, 10)
            ],
            progression: new RuntimeProgressionSnapshot(37, 0, 0, 0));

        ProductionCombatantProfile profile = ProductionCombatRuleset.FromRuntimeActor(actor);

        Assert.Equal(37, profile.Level);
    }

    [Fact]
    public void ExtremeRewardInputsSaturateInsteadOfThrowingOrWrapping()
    {
        ProductionCombatRuleset ruleset = Rules(0.5m);
        ProductionCombatantProfile enemy = Actor(
            level: int.MaxValue,
            stats: new ProductionCombatStats(
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue,
                decimal.MaxValue));

        Assert.Equal(int.MaxValue, ruleset.CalculateExperienceYield(enemy));
        Assert.Equal(int.MaxValue, ruleset.CalculateCurrencyYield(enemy));
    }

    [Fact]
    public void ExtremeCombatInputsSaturateInsteadOfThrowingOrWrapping()
    {
        var config = new ProductionCombatRulesetConfig
        {
            DamageFormulaScalar = decimal.MaxValue,
            DamageVarianceMinimum = decimal.MaxValue,
            DamageVarianceMaximum = decimal.MaxValue,
            ChargeMultiplier = decimal.MaxValue,
            CriticalDamageMultiplier = decimal.MaxValue,
            WeakDamageMultiplier = decimal.MaxValue,
            ResistDamageMultiplier = decimal.MaxValue,
            GuardDamageMultiplier = decimal.MaxValue,
            InitiativeVarianceMinimum = decimal.MaxValue,
            InitiativeVarianceMaximum = decimal.MaxValue
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
            status: new ProductionCombatStatus(HasPhysicalCharge: true),
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
                new HitCountDefinition(1, 1)));
        ProductionHitCheckResult lowestHitChance = ruleset.CheckHit(
            new ProductionHitCheckRequest(target, attacker, 0));

        Assert.Equal(decimal.MaxValue, Assert.Single(damage.Hits).Damage);
        Assert.Equal(decimal.MaxValue, damage.TotalDamage);
        Assert.Equal(config.HitChanceMinimum, lowestHitChance.Chance);
        Assert.Equal(
            config.CriticalChanceMaximum,
            ruleset.CalculateCriticalChance(attacker, target));
        Assert.True(ruleset.RollInitiative(decimal.MaxValue, decimal.MaxValue));
        Assert.Equal(
            decimal.MaxValue,
            new ProductionDamageResolutionResult(
                [
                    new ProductionDamageResolutionHit(true, decimal.MaxValue, false, 100, 0),
                    new ProductionDamageResolutionHit(true, decimal.MaxValue, false, 100, 0)
                ],
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

        ProductionCombatantProfile profile = ProductionCombatRuleset.FromRuntimeActor(actor);

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
}
