using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Hosting;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class CriticalResolutionPolicyTests
{
    [Fact]
    public void AuthoredPolicy_UsesExactChanceThenExplicitModifiersWithoutLuck()
    {
        var random = new CountingRandomSource([0.53m]);
        var policy = new AuthoredCriticalChancePolicy(random);
        var request = new CriticalChanceRequest(
            new ChanceCriticalDefinition(20),
            authoredAccuracy: 80,
            finalHitChance: 60,
            criticalChanceMultiplier: 1.5m,
            targetCriticalChanceBonus: 5,
            criticalChanceModifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.CriticalChance,
                    ModifierOperation.Add,
                    5m),
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.CriticalChance,
                    ModifierOperation.Multiply,
                    1.2m)
            ]);

        CriticalChanceResult result = policy.Resolve(request);

        Assert.Equal(20, result.AuthoredChance);
        Assert.Equal(20m, result.PolicyBaseChance);
        Assert.Equal(54m, result.ResolvedChance);
        Assert.Equal(54, result.FinalChance);
        Assert.Equal(53m, result.Roll);
        Assert.True(result.Critical);
        Assert.Equal(1, random.UnitCalls);
    }

    [Fact]
    public void AccuracyScaledPolicy_UsesFinalToAuthoredAccuracyRatio()
    {
        var policy = new AccuracyScaledCriticalChancePolicy(
            new CountingRandomSource([0.15m]));

        CriticalChanceResult reduced = policy.Resolve(new CriticalChanceRequest(
            new ChanceCriticalDefinition(20),
            authoredAccuracy: 80,
            finalHitChance: 60));
        CriticalChanceResult increased = policy.Resolve(new CriticalChanceRequest(
            new ChanceCriticalDefinition(20),
            authoredAccuracy: 50,
            finalHitChance: 75));

        Assert.Equal(15m, reduced.PolicyBaseChance);
        Assert.Equal(15, reduced.FinalChance);
        Assert.False(reduced.Critical);
        Assert.Equal(30m, increased.PolicyBaseChance);
        Assert.Equal(30, increased.FinalChance);
        Assert.True(increased.Critical);
    }

    [Fact]
    public void ChanceBoundaries_AreExactAndDoNotConsumeRandomness()
    {
        var random = new CountingRandomSource([]);
        var policy = new AuthoredCriticalChancePolicy(random);

        CriticalChanceResult never = policy.Resolve(Request(0));
        CriticalChanceResult certain = policy.Resolve(Request(100));

        Assert.False(never.Critical);
        Assert.Null(never.Roll);
        Assert.True(certain.Critical);
        Assert.Null(certain.Roll);
        Assert.Equal(0, random.UnitCalls);
    }

    [Fact]
    public void EligibilityPolicies_SeparateDamageKindFromCriticalChance()
    {
        var physicalOnly = new PhysicalOnlyCriticalEligibilityPolicy();
        var allDamage = new AllDamageCriticalEligibilityPolicy();
        var magical = new CriticalEligibilityRequest(
            DamageElement.Fire,
            new ChanceCriticalDefinition(25));

        CriticalEligibilityResult rejected = physicalOnly.Assess(magical);
        CriticalEligibilityResult accepted = allDamage.Assess(magical);
        CriticalEligibilityResult never = allDamage.Assess(new CriticalEligibilityRequest(
            DamageElement.Physical,
            new NeverCriticalDefinition()));

        Assert.False(rejected.Eligible);
        Assert.Equal(CriticalEligibilityReason.DamageElementIneligible, rejected.Reason);
        Assert.True(accepted.Eligible);
        Assert.False(never.Eligible);
        Assert.Equal(CriticalEligibilityReason.DefinitionDisallowsCritical, never.Reason);
    }

    [Fact]
    public void GuardRejectsCriticalWhileRigidGuaranteesOnlyAnEligibleAttack()
    {
        var policy = new PhysicalOnlyCriticalEligibilityPolicy();
        var critical = new ChanceCriticalDefinition(1);

        CriticalEligibilityResult guarding = policy.Assess(new CriticalEligibilityRequest(
            DamageElement.Physical,
            critical,
            TargetIsGuarding: true,
            TargetIsRigid: true));
        CriticalEligibilityResult rigidPhysical = policy.Assess(new CriticalEligibilityRequest(
            DamageElement.Physical,
            critical,
            TargetIsRigid: true));
        CriticalEligibilityResult rigidMagic = policy.Assess(new CriticalEligibilityRequest(
            DamageElement.Ice,
            critical,
            TargetIsRigid: true));

        Assert.Equal(CriticalEligibilityReason.TargetGuarding, guarding.Reason);
        Assert.False(guarding.Eligible);
        Assert.True(rigidPhysical.Eligible);
        Assert.True(rigidPhysical.GuaranteedByRigidState);
        Assert.False(rigidMagic.Eligible);
    }

    [Fact]
    public void ProductionRuleset_LuckDoesNotChangeCriticalChance()
    {
        ProductionCriticalCheckResult lowLuck = ResolveWithLuck(1m, 99m);
        ProductionCriticalCheckResult highLuck = ResolveWithLuck(99m, 1m);

        Assert.Equal(25, lowLuck.Chance);
        Assert.Equal(lowLuck.Chance, highLuck.Chance);
        Assert.Equal(lowLuck.Critical, highLuck.Critical);
    }

    [Fact]
    public void MissDoesNotInvokeCriticalPolicy()
    {
        var hitRandom = new CountingRandomSource([0.99m]);
        var criticalRandom = new CountingRandomSource([0m]);
        var ruleset = new ProductionCombatRuleset(
            new CountingRandomSource([]),
            hitPolicy: new StandardHitResolutionPolicy(
                hitRandom,
                new StandardHitResolutionPolicyConfig
                {
                    AttackerAgilityCoefficient = 0m,
                    TargetAgilityCoefficient = 0m
                }),
            criticalChancePolicy: new AuthoredCriticalChancePolicy(criticalRandom));

        ProductionDamageResolutionResult result = ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                Actor(1m),
                Actor(1m),
                DamageElement.Physical,
                ElementalAffinity.Normal,
                power: 20,
                accuracy: 50,
                new ChanceCriticalDefinition(100),
                new HitCountDefinition(1, 1)));

        ProductionDamageResolutionHit miss = Assert.Single(result.Hits);
        Assert.False(miss.Hit);
        Assert.False(miss.Critical);
        Assert.Equal(1, hitRandom.UnitCalls);
        Assert.Equal(0, criticalRandom.UnitCalls);
    }

    [Fact]
    public void CriticalRequest_DefensivelySnapshotsTypedModifiers()
    {
        var source = new List<NumericRuleModifierDefinition>
        {
            new(NumericRuleModifierType.CriticalChance, ModifierOperation.Add, 5m)
        };
        var request = new CriticalChanceRequest(
            new ChanceCriticalDefinition(10),
            100,
            100,
            criticalChanceModifiers: source);

        source.Clear();

        Assert.Single(request.CriticalChanceModifiers);
        Assert.Throws<ArgumentException>(() => new CriticalChanceRequest(
            new ChanceCriticalDefinition(10),
            100,
            100,
            criticalChanceModifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.Accuracy,
                    ModifierOperation.Add,
                    5m)
            ]));
    }

    private static CriticalChanceRequest Request(int chance) =>
        new(new ChanceCriticalDefinition(chance), 100, 100);

    private static ProductionCriticalCheckResult ResolveWithLuck(
        decimal attackerLuck,
        decimal targetLuck)
    {
        var ruleset = new ProductionCombatRuleset(new CountingRandomSource([0.2m]));
        return ruleset.CheckCritical(new ProductionCriticalCheckRequest(
            Actor(attackerLuck),
            Actor(targetLuck),
            DamageElement.Physical,
            new ChanceCriticalDefinition(25),
            authoredAccuracy: 100,
            finalHitChance: 100));
    }

    private static ProductionCombatantProfile Actor(decimal luck) =>
        new(1, new ProductionCombatStats(10, 10, 10, 10, luck));

    private sealed class CountingRandomSource(IEnumerable<decimal> units) : IRandomSource
    {
        private readonly Queue<decimal> _units = new(units);

        public int UnitCalls { get; private set; }

        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal()
        {
            UnitCalls++;
            return _units.Count == 0 ? 0m : _units.Dequeue();
        }
    }
}
