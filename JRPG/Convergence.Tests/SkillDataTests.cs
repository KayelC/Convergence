using JRPGPrototype.Data;
using Xunit;

namespace Convergence.Tests;

public sealed class SkillDataTests
{
    [Fact]
    public void GetPowerVal_ReturnsNumericPower()
    {
        var skill = CreateSkill(power: "45");

        Assert.Equal(45, skill.GetPowerVal());
    }

    [Theory]
    [InlineData("-")]
    [InlineData("NaN")]
    [InlineData("")]
    public void GetPowerVal_ReturnsZeroForNonnumericPower(string power)
    {
        var skill = CreateSkill(power: power);

        Assert.Equal(0, skill.GetPowerVal());
    }

    [Theory]
    [InlineData("10 SP", 10, false, false)]
    [InlineData("25% HP", 25, true, true)]
    [InlineData("", 0, false, false)]
    public void ParseCost_ReturnsCurrentTupleBehavior(string cost, int value, bool isPercentage, bool isHp)
    {
        var skill = CreateSkill(cost: cost);

        Assert.Equal((value, isPercentage, isHp), skill.ParseCost());
    }

    [Fact]
    public void ParseCost_TreatsNullLikeCostAsFreeNonPercentageSpCost()
    {
        var skill = CreateSkill(cost: null!);

        Assert.Equal((0, false, false), skill.ParseCost());
    }

    [Fact]
    public void IsExclusive_UsesEffectTextCaseInsensitively()
    {
        var skill = CreateSkill(effect: "A unique EXCLUSIVE technique.");

        Assert.True(skill.IsExclusive());
    }

    [Theory]
    [InlineData("Slash", "1", "Normal effect", true)]
    [InlineData("-", "1", "Normal effect", false)]
    [InlineData("Slash", "-", "Normal effect", false)]
    [InlineData("Slash", "1", "exclusive skill", false)]
    public void CanEvolve_RequiresFamilyRankAndNonExclusiveEffect(string family, string rank, string effect, bool expected)
    {
        var skill = CreateSkill(family: family, rank: rank, effect: effect);

        Assert.Equal(expected, skill.CanEvolve());
    }

    private static SkillData CreateSkill(
        string power = "0",
        string cost = "0 SP",
        string effect = "Normal effect",
        string family = "Test",
        string rank = "1")
    {
        return new SkillData
        {
            Name = "Test Skill",
            Effect = effect,
            Power = power,
            Accuracy = "100%",
            Cost = cost,
            Category = "Fire",
            Family = family,
            Rank = rank
        };
    }
}
