using JRPGPrototype.Core;
using JRPGPrototype.Logic.Battle.Engines;
using Xunit;

namespace Convergence.Tests;

public sealed class PressTurnEngineTests
{
    [Fact]
    public void StartPhase_CreatesOneFullIconPerActiveMember()
    {
        var engine = new PressTurnEngine();

        engine.StartPhase(4);

        Assert.Equal(4, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
        Assert.Equal(4, engine.GetTotalIconCount());
        Assert.True(engine.HasTurnsRemaining());
        Assert.Equal("[O] [O] [O] [O]", engine.GetIconsDisplay());
    }

    [Theory]
    [InlineData(HitType.Weakness, false)]
    [InlineData(HitType.Normal, true)]
    public void ChainOutcomes_ConvertFullIconToBlinking(HitType hitType, bool isCritical)
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(3);

        engine.ConsumeAction(hitType, isCritical);

        Assert.Equal(2, engine.FullIcons);
        Assert.Equal(1, engine.BlinkingIcons);
        Assert.Equal(3, engine.GetTotalIconCount());
    }

    [Fact]
    public void NormalAction_ConsumesBlinkingIconBeforeFullIcon()
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(2);
        engine.Pass();

        engine.ConsumeAction(HitType.Normal, false);

        Assert.Equal(1, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
    }

    [Theory]
    [InlineData(HitType.Miss)]
    [InlineData(HitType.Null)]
    public void PenaltyOutcomes_ConsumeTwoIcons(HitType hitType)
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(4);

        engine.ConsumeAction(hitType, false);

        Assert.Equal(2, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
        Assert.Equal(2, engine.GetTotalIconCount());
    }

    [Theory]
    [InlineData(HitType.Repel)]
    [InlineData(HitType.Absorb)]
    public void TerminalOutcomes_EndPhase(HitType hitType)
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(4);

        engine.ConsumeAction(hitType, false);

        Assert.Equal(0, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
        Assert.False(engine.HasTurnsRemaining());
        Assert.Equal("[EMPTY]", engine.GetIconsDisplay());
    }

    [Fact]
    public void Pass_ConvertsFullIconThenConsumesBlinkingIcon()
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(1);

        engine.Pass();

        Assert.Equal(0, engine.FullIcons);
        Assert.Equal(1, engine.BlinkingIcons);
        Assert.True(engine.HasTurnsRemaining());

        engine.Pass();

        Assert.Equal(0, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
        Assert.False(engine.HasTurnsRemaining());
    }
}
