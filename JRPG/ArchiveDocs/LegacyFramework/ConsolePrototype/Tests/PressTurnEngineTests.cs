using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using Xunit;

namespace Convergence.Tests;

public sealed class PressTurnEngineTests
{
    [Fact]
    public void StandardActionEconomy_ConsumesOneActionWithoutPressTurnRules()
    {
        var economy = new StandardActionTurnEconomy();
        economy.StartPhase(2);

        economy.Apply(ActionTurnConsumption.None);
        Assert.Equal(2, economy.CaptureSnapshot().RemainingActions);

        economy.Apply(ActionTurnConsumption.FromPressTurn(
            new PressTurnResolution(PressTurnOutcome.Weakness, false, false)));
        Assert.Equal(1, economy.CaptureSnapshot().RemainingActions);

        economy.Apply(ActionTurnConsumption.Pass);
        Assert.Equal(0, economy.CaptureSnapshot().RemainingActions);
        Assert.False(economy.HasTurnsRemaining());
    }

    [Fact]
    public void FrameworkPressTurnApi_ContainsNoLegacyHitTypeOrConsoleFormatter()
    {
        System.Reflection.MethodInfo[] methods = typeof(PressTurnEngine).GetMethods(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(methods, method => method.Name == "GetIconsDisplay");
        Assert.DoesNotContain(methods.SelectMany(method => method.GetParameters()), parameter =>
            parameter.ParameterType.FullName == "JRPGPrototype.Core.HitType");
    }

    [Fact]
    public void StartPhase_CreatesOneFullIconPerActiveMember()
    {
        var engine = new PressTurnEngine();

        engine.StartPhase(4);

        Assert.Equal(4, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
        Assert.Equal(4, engine.GetTotalIconCount());
        Assert.True(engine.HasTurnsRemaining());
        Assert.Equal("[O] [O] [O] [O]", PressTurnIconFormatter.Format(engine));
        var snapshot = Assert.IsType<PressTurnEconomySnapshot>(engine.CaptureSnapshot());
        Assert.Equal(4, snapshot.RemainingActions);
    }

    [Theory]
    [InlineData(PressTurnOutcome.Weakness, false)]
    [InlineData(PressTurnOutcome.Critical, true)]
    public void ChainOutcomes_ConvertFullIconToBlinking(PressTurnOutcome outcome, bool isCritical)
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(3);

        engine.ConsumeAction(new PressTurnResolution(outcome, isCritical, false));

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

        engine.ConsumeAction(new PressTurnResolution(PressTurnOutcome.Normal, false, false));

        Assert.Equal(1, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
    }

    [Theory]
    [InlineData(PressTurnOutcome.Miss)]
    [InlineData(PressTurnOutcome.Null)]
    public void PenaltyOutcomes_ConsumeTwoIcons(PressTurnOutcome outcome)
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(4);

        engine.ConsumeAction(new PressTurnResolution(outcome, false, false));

        Assert.Equal(2, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
        Assert.Equal(2, engine.GetTotalIconCount());
    }

    [Theory]
    [InlineData(PressTurnOutcome.Repel)]
    [InlineData(PressTurnOutcome.Absorb)]
    public void TerminalOutcomes_EndPhase(PressTurnOutcome outcome)
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(4);

        engine.ConsumeAction(new PressTurnResolution(outcome, false, true));

        Assert.Equal(0, engine.FullIcons);
        Assert.Equal(0, engine.BlinkingIcons);
        Assert.False(engine.HasTurnsRemaining());
        Assert.Equal("[EMPTY]", PressTurnIconFormatter.Format(engine));
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
