using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using Xunit;

namespace Convergence.Tests;

public sealed class CombatMathTests
{
    [Fact]
    public void CalculateExpYield_ReturnsAtLeastOneForLowLevelEnemy()
    {
        var enemy = CreateCombatant("Slime", level: 1, statValue: 2);

        int exp = CombatMath.CalculateExpYield(enemy);

        Assert.Equal(1, exp);
    }

    [Fact]
    public void CalculateExpYield_AppliesStatDensityBonus()
    {
        var enemy = CreateCombatant("Powerful Enemy", level: 10, statValue: 20);

        int exp = CombatMath.CalculateExpYield(enemy);

        Assert.Equal(48, exp);
    }

    [Fact]
    public void CalculateExpYield_CapsStatDensityBonusAtDoubleBaseYield()
    {
        var enemy = CreateCombatant("Boss", level: 10, statValue: 100);

        int exp = CombatMath.CalculateExpYield(enemy);

        Assert.Equal(60, exp);
    }

    [Theory]
    [InlineData(Affinity.Normal)]
    [InlineData(Affinity.Weak)]
    [InlineData(Affinity.Resist)]
    [InlineData(Affinity.Null)]
    [InlineData(Affinity.Repel)]
    [InlineData(Affinity.Absorb)]
    public void GetEffectiveAffinity_ReturnsBaseAffinityWhenNoOverridesExist(Affinity baseAffinity)
    {
        var target = CreateCombatant("Target", Element.Fire, baseAffinity);

        Affinity result = CombatMath.GetEffectiveAffinity(target, Element.Fire);

        Assert.Equal(baseAffinity, result);
    }

    [Fact]
    public void GetEffectiveAffinity_PhysicalKarnRepelsPhysicalBeforeBaseAffinity()
    {
        var target = CreateCombatant("Target", Element.Slash, Affinity.Weak);
        target.PhysKarnActive = true;

        Affinity result = CombatMath.GetEffectiveAffinity(target, Element.Slash);

        Assert.Equal(Affinity.Repel, result);
    }

    [Fact]
    public void GetEffectiveAffinity_MagicKarnRepelsMagicButNotAlmighty()
    {
        var target = CreateCombatant("Target", Element.Fire, Affinity.Weak);
        target.MagicKarnActive = true;

        Affinity fireResult = CombatMath.GetEffectiveAffinity(target, Element.Fire);
        Affinity almightyResult = CombatMath.GetEffectiveAffinity(target, Element.Almighty);

        Assert.Equal(Affinity.Repel, fireResult);
        Assert.Equal(Affinity.Normal, almightyResult);
    }

    [Fact]
    public void GetEffectiveAffinity_BrokenAffinityReducesBaseImmunityToNormal()
    {
        var target = CreateCombatant("Target", Element.Fire, Affinity.Null);
        target.BrokenAffinities[Element.Fire] = 3;

        Affinity result = CombatMath.GetEffectiveAffinity(target, Element.Fire);

        Assert.Equal(Affinity.Normal, result);
    }

    [Fact]
    public void GetEffectiveAffinity_ShieldTakesPriorityOverBrokenAffinity()
    {
        var target = CreateCombatant("Target", Element.Fire, Affinity.Null);
        target.MagicKarnActive = true;
        target.BrokenAffinities[Element.Fire] = 3;

        Affinity result = CombatMath.GetEffectiveAffinity(target, Element.Fire);

        Assert.Equal(Affinity.Repel, result);
    }

    [Fact]
    public void GetEffectiveAffinity_AlmightyAndNoneIgnoreBaseAffinity()
    {
        var target = CreateCombatant("Target");
        target.ActivePersona!.AffinityMap[Element.Almighty] = Affinity.Weak;
        target.ActivePersona.AffinityMap[Element.None] = Affinity.Absorb;

        Affinity almightyResult = CombatMath.GetEffectiveAffinity(target, Element.Almighty);
        Affinity noneResult = CombatMath.GetEffectiveAffinity(target, Element.None);

        Assert.Equal(Affinity.Normal, almightyResult);
        Assert.Equal(Affinity.Normal, noneResult);
    }

    [Fact]
    public void GetEffectiveAffinity_GuardingReducesWeaknessToNormalOnly()
    {
        var weakTarget = CreateCombatant("Weak Target", Element.Fire, Affinity.Weak);
        var resistTarget = CreateCombatant("Resist Target", Element.Fire, Affinity.Resist);
        weakTarget.IsGuarding = true;
        resistTarget.IsGuarding = true;

        Affinity weakResult = CombatMath.GetEffectiveAffinity(weakTarget, Element.Fire);
        Affinity resistResult = CombatMath.GetEffectiveAffinity(resistTarget, Element.Fire);

        Assert.Equal(Affinity.Normal, weakResult);
        Assert.Equal(Affinity.Resist, resistResult);
    }

    [Theory]
    [InlineData(Affinity.Resist)]
    [InlineData(Affinity.Null)]
    [InlineData(Affinity.Repel)]
    [InlineData(Affinity.Absorb)]
    public void GetEffectiveAffinity_RigidBodyNormalizesPhysicalResistance(Affinity baseAffinity)
    {
        var target = CreateCombatant("Frozen Target", Element.Slash, baseAffinity);
        target.InflictAilment(CreateAilment("Freeze"));

        Affinity result = CombatMath.GetEffectiveAffinity(target, Element.Slash);

        Assert.Equal(Affinity.Normal, result);
    }

    [Fact]
    public void GetEffectiveAffinity_RigidBodyKeepsPhysicalWeakness()
    {
        var target = CreateCombatant("Frozen Target", Element.Slash, Affinity.Weak);
        target.InflictAilment(CreateAilment("Freeze"));

        Affinity result = CombatMath.GetEffectiveAffinity(target, Element.Slash);

        Assert.Equal(Affinity.Weak, result);
    }

    [Fact]
    public void GetEffectiveAffinity_RigidBodyDoesNotNormalizeMagicResistance()
    {
        var target = CreateCombatant("Frozen Target", Element.Fire, Affinity.Null);
        target.InflictAilment(CreateAilment("Freeze"));

        Affinity result = CombatMath.GetEffectiveAffinity(target, Element.Fire);

        Assert.Equal(Affinity.Null, result);
    }

    [Theory]
    [InlineData(2, 40, 2)]
    [InlineData(20, 20, 5)]
    [InlineData(40, 2, 23)]
    public void CalculateCritChance_UsesLuckDeltaAndClampsToMinimum(
        int attackerLuck,
        int targetLuck,
        int expectedChance)
    {
        var attacker = CreateCombatant("Attacker", luck: attackerLuck);
        var target = CreateCombatant("Target", luck: targetLuck);

        int result = CombatMath.CalculateCritChance(attacker, target);

        Assert.Equal(expectedChance, result);
    }

    [Fact]
    public void CalculateCritChance_AptPupilMultiplierIsCappedAtForty()
    {
        var attacker = CreateCombatant("Attacker", luck: 40);
        attacker.ExtraSkills.Add("Apt Pupil");
        var target = CreateCombatant("Target", luck: 2);

        int result = CombatMath.CalculateCritChance(attacker, target);

        Assert.Equal(40, result);
    }

    [Fact]
    public void CheckHit_RigidBodyAlwaysHits()
    {
        var attacker = CreateCombatant("Attacker", agility: 2, luck: 2);
        var target = CreateCombatant("Target", agility: 40, luck: 40);
        target.InflictAilment(CreateAilment("Shock"));

        bool result = CombatMath.CheckHit(attacker, target, Element.Slash, "1%");

        Assert.True(result);
    }

    [Fact]
    public void CalculateInstantKill_ReturnsFalseWhenTargetNullsCurse()
    {
        var attacker = CreateCombatant("Attacker", luck: 40);
        var target = CreateCombatant("Target", Element.Curse, Affinity.Null, luck: 2);

        bool result = CombatMath.CalculateInstantKill(attacker, target, "100%");

        Assert.False(result);
    }

    [Fact]
    public void RollInitiative_ReturnsTrueWhenPlayerRollCannotLose()
    {
        bool result = CombatMath.RollInitiative(playerAvgAg: 100, enemyAvgAg: 1);

        Assert.True(result);
    }

    [Fact]
    public void RollInitiative_ReturnsFalseWhenPlayerRollCannotWin()
    {
        bool result = CombatMath.RollInitiative(playerAvgAg: 1, enemyAvgAg: 100);

        Assert.False(result);
    }

    private static Combatant CreateCombatant(
        string name,
        Element affinityElement = Element.Fire,
        Affinity affinity = Affinity.Normal,
        int level = 1,
        int statValue = 10,
        int agility = 10,
        int luck = 10)
    {
        var combatant = new Combatant(name)
        {
            SourceId = name,
            Level = level,
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50,
            ActivePersona = new Persona
            {
                Name = $"{name} Persona",
                Level = level,
                AffinityMap = { [affinityElement] = affinity }
            }
        };

        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            combatant.CharacterStats[stat] = statValue;
            combatant.ActivePersona.StatModifiers[stat] = statValue;
        }

        combatant.CharacterStats[StatType.Ag] = agility;
        combatant.ActivePersona.StatModifiers[StatType.Ag] = agility;
        combatant.CharacterStats[StatType.Lu] = luck;
        combatant.ActivePersona.StatModifiers[StatType.Lu] = luck;

        return combatant;
    }

    private static AilmentData CreateAilment(string name)
    {
        return new AilmentData
        {
            Name = name,
            ActionRestriction = "None",
            DamageDealMult = 1.0,
            DamageTakenMult = 1.0,
            RemovalTriggers = new List<string>(),
            CureKeyword = string.Empty,
            Description = $"{name} test ailment."
        };
    }
}
