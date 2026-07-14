using JRPGPrototype.Logic.Core;
using Xunit;

namespace Convergence.Tests;

public sealed class MoonPhaseSystemTests
{
    public MoonPhaseSystemTests()
    {
        MoonPhaseSystem.ResetForTests();
    }

    [Fact]
    public void ResetState_StartsAtNewMoon()
    {
        Assert.Equal(0, MoonPhaseSystem.CurrentPhase);
        Assert.Equal("New Moon", MoonPhaseSystem.GetPhaseName());
        Assert.False(MoonPhaseSystem.IsNegotiationBlocked());
    }

    [Fact]
    public void Advance_CyclesThroughEightAndWrapsToZero()
    {
        for (int i = 0; i < 8; i++)
        {
            MoonPhaseSystem.Advance();
        }

        Assert.Equal(8, MoonPhaseSystem.CurrentPhase);
        Assert.Equal("Full Moon", MoonPhaseSystem.GetPhaseName());
        Assert.True(MoonPhaseSystem.IsNegotiationBlocked());

        MoonPhaseSystem.Advance();

        Assert.Equal(0, MoonPhaseSystem.CurrentPhase);
        Assert.Equal("New Moon", MoonPhaseSystem.GetPhaseName());
        Assert.False(MoonPhaseSystem.IsNegotiationBlocked());
    }

    [Theory]
    [InlineData(0, "New Moon")]
    [InlineData(1, "Waxing Crescent 1/8")]
    [InlineData(2, "Waxing Crescent 2/8")]
    [InlineData(3, "Waxing Crescent 3/8")]
    [InlineData(4, "Half Moon")]
    [InlineData(5, "Waxing Gibbous 5/8")]
    [InlineData(6, "Waxing Gibbous 6/8")]
    [InlineData(7, "Waxing Gibbous 7/8")]
    [InlineData(8, "Full Moon")]
    public void GetPhaseName_ReturnsExpectedName(int phase, string expectedName)
    {
        for (int i = 0; i < phase; i++)
        {
            MoonPhaseSystem.Advance();
        }

        Assert.Equal(expectedName, MoonPhaseSystem.GetPhaseName());
    }
}
