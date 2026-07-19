using Convergence.Battle;
using Convergence.Content;
using Convergence.Hosting;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class InstantDefeatResolutionPolicyTests
{
    [Theory]
    [InlineData(ResistanceLevel.Vulnerable, 60, true, InstantDefeatResolutionReason.Defeated)]
    [InlineData(ResistanceLevel.Normal, 40, true, InstantDefeatResolutionReason.Defeated)]
    [InlineData(ResistanceLevel.Resistant, 20, false, InstantDefeatResolutionReason.ProbabilityFailed)]
    [InlineData(ResistanceLevel.Immune, 0, false, InstantDefeatResolutionReason.ResistanceBlocked)]
    public void StandardPolicy_AppliesApprovedResistanceMultipliers(
        ResistanceLevel resistance,
        int expectedChance,
        bool expectedDefeated,
        InstantDefeatResolutionReason expectedReason)
    {
        var random = new CountingRandomSource([0.3m]);
        var policy = new StandardInstantDefeatResolutionPolicy(random);

        InstantDefeatResolutionResult result = policy.Resolve(
            new InstantDefeatResolutionRequest(40, resistance));

        Assert.Equal(40, result.AuthoredChance);
        Assert.Equal(expectedChance, result.FinalChance);
        Assert.Equal(expectedDefeated, result.Defeated);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Equal(resistance, result.Resistance);
        Assert.False(result.BypassedResistance);
        Assert.Equal(resistance == ResistanceLevel.Immune ? 0 : 1, random.UnitCalls);
    }

    [Theory]
    [InlineData(ResistanceLevel.Vulnerable)]
    [InlineData(ResistanceLevel.Normal)]
    [InlineData(ResistanceLevel.Resistant)]
    [InlineData(ResistanceLevel.Immune)]
    public void Bypass_IgnoresResistanceButStillUsesOneProbabilityRoll(
        ResistanceLevel resistance)
    {
        var random = new CountingRandomSource([0.39m]);
        var policy = new StandardInstantDefeatResolutionPolicy(random);

        InstantDefeatResolutionResult result = policy.Resolve(
            new InstantDefeatResolutionRequest(40, resistance, bypassesResistance: true));

        Assert.True(result.Defeated);
        Assert.Equal(40, result.FinalChance);
        Assert.Equal(1m, result.ResistanceMultiplier);
        Assert.Equal(39m, result.Roll);
        Assert.True(result.BypassedResistance);
        Assert.Equal(1, random.UnitCalls);
    }

    [Fact]
    public void ChanceBoundaries_AreExactAndDoNotConsumeRandomness()
    {
        var random = new CountingRandomSource([]);
        var policy = new StandardInstantDefeatResolutionPolicy(random);

        InstantDefeatResolutionResult never = policy.Resolve(
            new InstantDefeatResolutionRequest(0, ResistanceLevel.Normal));
        InstantDefeatResolutionResult certain = policy.Resolve(
            new InstantDefeatResolutionRequest(100, ResistanceLevel.Normal));

        Assert.False(never.Defeated);
        Assert.Null(never.Roll);
        Assert.True(certain.Defeated);
        Assert.Null(certain.Roll);
        Assert.Equal(0, random.UnitCalls);
    }

    [Fact]
    public void Configuration_CanReplaceMultipliersAndChanceBounds()
    {
        var policy = new StandardInstantDefeatResolutionPolicy(
            new CountingRandomSource([]),
            new StandardInstantDefeatResolutionPolicyConfig
            {
                VulnerableMultiplier = 2m,
                NormalMultiplier = 0.75m,
                ResistantMultiplier = 0.25m,
                ImmuneMultiplier = 0.1m,
                MinimumChance = 10,
                MaximumChance = 80
            });

        InstantDefeatResolutionResult vulnerable = policy.Resolve(
            new InstantDefeatResolutionRequest(50, ResistanceLevel.Vulnerable));
        InstantDefeatResolutionResult immune = policy.Resolve(
            new InstantDefeatResolutionRequest(50, ResistanceLevel.Immune));

        Assert.Equal(100m, vulnerable.ResolvedChance);
        Assert.Equal(80, vulnerable.FinalChance);
        Assert.Equal(5m, immune.ResolvedChance);
        Assert.Equal(10, immune.FinalChance);
        Assert.NotEqual(InstantDefeatResolutionReason.ResistanceBlocked, immune.Reason);
    }

    [Fact]
    public void ZeroImmuneMultiplier_BlocksBeforeAnOptionalMinimumChance()
    {
        var policy = new StandardInstantDefeatResolutionPolicy(
            new CountingRandomSource([]),
            new StandardInstantDefeatResolutionPolicyConfig { MinimumChance = 10 });

        InstantDefeatResolutionResult result = policy.Resolve(
            new InstantDefeatResolutionRequest(100, ResistanceLevel.Immune));

        Assert.False(result.Defeated);
        Assert.Equal(0, result.FinalChance);
        Assert.Equal(InstantDefeatResolutionReason.ResistanceBlocked, result.Reason);
    }

    [Fact]
    public void Request_RequiresTypedResistanceUnlessExplicitlyBypassed()
    {
        Assert.Throws<ArgumentException>(() => new InstantDefeatResolutionRequest(25, null));
        var bypass = new InstantDefeatResolutionRequest(25, null, bypassesResistance: true);

        Assert.True(bypass.BypassesResistance);
        Assert.Null(bypass.Resistance);
    }

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
