using Convergence.Battle;
using Convergence.Content;
using Convergence.Hosting;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class HitResolutionPolicyTests
{
    [Fact]
    public void StandardPolicy_ExposesAuthoredAgilityModifierAndRollEvidence()
    {
        var policy = new StandardHitResolutionPolicy(new SequenceRandomSource(0.73m));
        var request = new HitResolutionRequest(
            authoredAccuracy: 60,
            attackerAgility: 10,
            targetAgility: 20,
            accuracyMultiplier: 0.8m,
            evasionMultiplier: 0.5m,
            accuracyModifiers:
            [
                Modifier(NumericRuleModifierType.Accuracy, ModifierOperation.Add, 10m),
                Modifier(NumericRuleModifierType.Accuracy, ModifierOperation.Multiply, 1.5m)
            ],
            evasionModifiers:
            [
                Modifier(NumericRuleModifierType.Evasion, ModifierOperation.Add, 5m),
                Modifier(NumericRuleModifierType.Evasion, ModifierOperation.Multiply, 1.5m)
            ]);

        HitResolutionResult result = policy.Resolve(request);

        Assert.True(result.Hit);
        Assert.Equal(60, result.AuthoredAccuracy);
        Assert.Equal(20m, result.AttackerAgilityContribution);
        Assert.Equal(40m, result.TargetAgilityContribution);
        Assert.Equal(80m, result.AccuracyScoreBeforeModifiers);
        Assert.Equal(40m, result.EvasionScoreBeforeModifiers);
        Assert.Equal(108m, result.ResolvedAccuracyScore);
        Assert.Equal(33.75m, result.ResolvedEvasionScore);
        Assert.Equal(74.25m, result.RawChance);
        Assert.Equal(74, result.FinalChance);
        Assert.Equal(73m, result.Roll);
        Assert.False(result.GuaranteedByRigidState);
    }

    [Fact]
    public void StandardPolicy_UsesConfigurableCoefficientsAndBounds()
    {
        var policy = new StandardHitResolutionPolicy(
            new SequenceRandomSource(0.5m, 0.5m),
            new StandardHitResolutionPolicyConfig
            {
                AttackerAgilityCoefficient = 0.5m,
                TargetAgilityCoefficient = 1.5m,
                MinimumChance = 20,
                MaximumChance = 80
            });

        HitResolutionResult minimum = policy.Resolve(new HitResolutionRequest(0, 0, 100));
        HitResolutionResult maximum = policy.Resolve(new HitResolutionRequest(100, 100, 0));

        Assert.Equal(20, minimum.FinalChance);
        Assert.False(minimum.Hit);
        Assert.Equal(80, maximum.FinalChance);
        Assert.True(maximum.Hit);
    }

    [Fact]
    public void StandardPolicy_ZeroNeverHitsAndOneHundredAlwaysHitsWithoutRandomRolls()
    {
        var random = new ThrowingRandomSource();
        var policy = new StandardHitResolutionPolicy(random);

        HitResolutionResult impossible = policy.Resolve(new HitResolutionRequest(0, 0, 0));
        HitResolutionResult guaranteed = policy.Resolve(new HitResolutionRequest(100, 0, 0));

        Assert.False(impossible.Hit);
        Assert.Equal(0, impossible.FinalChance);
        Assert.Null(impossible.Roll);
        Assert.True(guaranteed.Hit);
        Assert.Equal(100, guaranteed.FinalChance);
        Assert.Null(guaranteed.Roll);
        Assert.Equal(0, random.UnitCalls);
    }

    [Fact]
    public void StandardPolicy_UsesStrictLessThanProbabilityBoundary()
    {
        var policy = new StandardHitResolutionPolicy(new SequenceRandomSource(0.49m, 0.50m));
        var request = new HitResolutionRequest(50, 0, 0);

        HitResolutionResult below = policy.Resolve(request);
        HitResolutionResult exact = policy.Resolve(request);

        Assert.True(below.Hit);
        Assert.False(exact.Hit);
        Assert.Equal(49m, below.Roll);
        Assert.Equal(50m, exact.Roll);
    }

    [Fact]
    public void StandardPolicy_RigidTargetGuaranteesTheConfiguredMaximumWithoutRolling()
    {
        var random = new ThrowingRandomSource();
        var policy = new StandardHitResolutionPolicy(
            random,
            new StandardHitResolutionPolicyConfig { MaximumChance = 80 });

        HitResolutionResult result = policy.Resolve(new HitResolutionRequest(
            authoredAccuracy: 0,
            attackerAgility: 0,
            targetAgility: 100,
            targetIsRigid: true));

        Assert.True(result.Hit);
        Assert.Equal(100, result.FinalChance);
        Assert.True(result.GuaranteedByRigidState);
        Assert.Null(result.Roll);
        Assert.Equal(0, random.UnitCalls);
    }

    [Fact]
    public void Request_SnapshotsAndTypeChecksModifierCollections()
    {
        var source = new List<NumericRuleModifierDefinition>
        {
            Modifier(NumericRuleModifierType.Accuracy, ModifierOperation.Add, 5m)
        };
        var request = new HitResolutionRequest(50, 10, 10, accuracyModifiers: source);

        source.Clear();

        Assert.Single(request.AccuracyModifiers);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<NumericRuleModifierDefinition>)request.AccuracyModifiers).Clear());
        Assert.Throws<ArgumentException>(() => new HitResolutionRequest(
            50,
            10,
            10,
            accuracyModifiers:
            [
                Modifier(NumericRuleModifierType.Evasion, ModifierOperation.Add, 5m)
            ]));
    }

    [Fact]
    public void ProductionRuleset_HitChanceDoesNotReadLuck()
    {
        ProductionCombatantProfile lowLuck = Actor(agility: 20, luck: 0);
        ProductionCombatantProfile highLuck = Actor(agility: 20, luck: 999);
        ProductionCombatantProfile target = Actor(agility: 20, luck: 500);
        var first = new ProductionCombatRuleset(new SequenceRandomSource(0.5m));
        var second = new ProductionCombatRuleset(new SequenceRandomSource(0.5m));

        HitResolutionResult low = first.CheckHit(new ProductionHitCheckRequest(lowLuck, target, 70));
        HitResolutionResult high = second.CheckHit(new ProductionHitCheckRequest(highLuck, target, 70));

        Assert.Equal(low.FinalChance, high.FinalChance);
        Assert.Equal(low.ResolvedAccuracyScore, high.ResolvedAccuracyScore);
        Assert.Equal(low.ResolvedEvasionScore, high.ResolvedEvasionScore);
        Assert.Equal(low.Hit, high.Hit);
    }

    private static NumericRuleModifierDefinition Modifier(
        NumericRuleModifierType type,
        ModifierOperation operation,
        decimal value) =>
        new(type, operation, value);

    private static ProductionCombatantProfile Actor(decimal agility, decimal luck) =>
        new(
            10,
            new ProductionCombatStats(
                Strength: 10,
                Magic: 10,
                Vitality: 10,
                Agility: agility,
                Luck: luck));

    private sealed class SequenceRandomSource(params decimal[] units) : IRandomSource
    {
        private readonly Queue<decimal> _units = new(units);

        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => _units.Dequeue();
    }

    private sealed class ThrowingRandomSource : IRandomSource
    {
        public int UnitCalls { get; private set; }

        public int NextInt32(int minimumInclusive, int maximumExclusive) =>
            throw new InvalidOperationException("The hit policy should not request an integer roll.");

        public decimal NextUnitDecimal()
        {
            UnitCalls++;
            throw new InvalidOperationException("The hit policy should not roll for guaranteed outcomes.");
        }
    }
}
