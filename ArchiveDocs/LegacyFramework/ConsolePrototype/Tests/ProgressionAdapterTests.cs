using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using Xunit;

namespace Convergence.Tests;

public sealed class ProgressionAdapterTests
{
    [Theory]
    [InlineData(ClassType.Human, StatType.St, 18)]
    [InlineData(ClassType.Operator, StatType.St, 10)]
    [InlineData(ClassType.PersonaUser, StatType.St, 18)]
    [InlineData(ClassType.WildCard, StatType.St, 18)]
    [InlineData(ClassType.Demon, StatType.St, 20)]
    [InlineData(ClassType.WildCard, StatType.Vi, 15)]
    [InlineData(ClassType.WildCard, StatType.Lu, 20)]
    public void CombatantGetStat_PreservesLegacyClassAndPersonaComposition(
        ClassType classType,
        StatType stat,
        int expected)
    {
        Combatant actor = CreateCombatant(classType, baseStat: 10, personaStat: 20);

        Assert.Equal(expected, actor.GetStat(stat));
    }

    [Fact]
    public void CombatantGetStat_HumanWithoutActivePersonaUsesBaseStatsOnly()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 10, personaStat: 20);
        actor.ActivePersona = null;

        Assert.Equal(10, actor.GetStat(StatType.St));
    }

    [Fact]
    public void CombatantGetStat_PreservesCapBeforeBuffAndDebuffMultipliers()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 100, personaStat: 0);
        actor.Buffs["PhysAtk"] = 1;
        actor.Buffs["PhysAtkDown"] = 1;

        Assert.Equal(33, actor.GetStat(StatType.St));
    }

    [Fact]
    public void CombatantGetStat_PreservesCurrentAccessoryParsingBehavior()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 10, personaStat: 0);
        actor.EquippedAccessory = new AccessoryData
        {
            Id = "401",
            Name = "Strength Wristband",
            Description = "Legacy JSON alias sample.",
            ModifierStat = "STR",
            ModifierValue = 5
        };

        Assert.Equal(10, actor.GetStat(StatType.St));

        actor.EquippedAccessory.ModifierStat = "St";
        Assert.Equal(15, actor.GetStat(StatType.St));
    }

    [Fact]
    public void CombatantRecalculateResources_PreservesLegacyFormulaCapsAndCurrentCapping()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 10, personaStat: 0);
        actor.BaseHP = 20;
        actor.BaseSP = 6;
        actor.CurrentHP = 100;
        actor.MaxHP = 100;
        actor.CurrentSP = 20;
        actor.MaxSP = 50;

        actor.RecalculateResources();

        Assert.Equal(70, actor.MaxHP);
        Assert.Equal(70, actor.CurrentHP);
        Assert.Equal(36, actor.MaxSP);
        Assert.Equal(20, actor.CurrentSP);
    }

    [Fact]
    public void CombatantGainExp_PreservesMultiLevelLoopLifetimeAndLevelUpMessages()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 2, personaStat: 0);
        actor.BaseHP = 20;
        actor.BaseSP = 6;
        actor.RecalculateResources();
        actor.CurrentHP = 10;
        actor.CurrentSP = 5;
        var io = new ScriptedGameIO();

        GrowthProcessor.GainExp(actor, 13, io);

        Assert.Equal(3, actor.Level);
        Assert.Equal(0, actor.Exp);
        Assert.Equal(13, actor.LifetimeEarnedExp);
        Assert.Equal(2, actor.StatPoints);
        Assert.InRange(actor.BaseHP, 32, 40);
        Assert.InRange(actor.BaseSP, 12, 20);
        Assert.Contains("Hero leveled up to 2!", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Hero leveled up to 3!", io.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CombatantAllocateAndRollback_UseFrameworkPoliciesThroughLegacyEntryPoints()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 10, personaStat: 0);
        actor.BaseHP = 20;
        actor.BaseSP = 6;
        actor.StatPoints = 1;
        actor.RecalculateResources();
        Dictionary<StatType, int> backup = actor.CharacterStats.ToDictionary(pair => pair.Key, pair => pair.Value);

        bool allocated = GrowthProcessor.AllocateStat(actor, StatType.Vi);

        Assert.True(allocated);
        Assert.Equal(11, actor.CharacterStats[StatType.Vi]);
        Assert.Equal(0, actor.StatPoints);
        Assert.Equal(75, actor.MaxHP);

        GrowthProcessor.RollbackStats(actor, backup, pointBackup: 1);

        Assert.Equal(10, actor.CharacterStats[StatType.Vi]);
        Assert.Equal(1, actor.StatPoints);
        Assert.Equal(70, actor.MaxHP);
    }

    [Fact]
    public void CombatantAllocateStat_RejectsNoPointsAndCapWithoutMutation()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 40, personaStat: 0);
        actor.StatPoints = 1;

        Assert.False(GrowthProcessor.AllocateStat(actor, StatType.St));
        Assert.Equal(40, actor.CharacterStats[StatType.St]);
        Assert.Equal(1, actor.StatPoints);

        actor.CharacterStats[StatType.St] = 10;
        actor.StatPoints = 0;

        Assert.False(GrowthProcessor.AllocateStat(actor, StatType.St));
        Assert.Equal(10, actor.CharacterStats[StatType.St]);
    }

    [Fact]
    public void CombatantGainExp_RejectsNegativeExperienceWithoutMutation()
    {
        Combatant actor = CreateCombatant(ClassType.Human, baseStat: 10, personaStat: 0);
        actor.Level = 5;
        actor.Exp = 10;
        actor.LifetimeEarnedExp = 100;

        GrowthProcessor.GainExp(actor, -1);

        Assert.Equal(5, actor.Level);
        Assert.Equal(10, actor.Exp);
        Assert.Equal(100, actor.LifetimeEarnedExp);
    }

    [Fact]
    public void PersonaGainExp_UsesFrameworkGrowthWhilePreservingSkillLearningMessages()
    {
        var persona = new Persona
        {
            Name = "Orpheus",
            Level = 1,
            SkillsToLearn = { [2] = "Agi" }
        };
        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            persona.StatModifiers[stat] = 40;
        }
        var io = new ScriptedGameIO();

        persona.GainExp(1, io);

        Assert.Equal(2, persona.Level);
        Assert.Equal(0, persona.Exp);
        Assert.Equal(1, persona.LifetimeEarnedExp);
        Assert.All(Enum.GetValues<StatType>(), stat => Assert.Equal(40, persona.StatModifiers[stat]));
        Assert.Equal(["Agi"], persona.SkillSet);
        Assert.Contains("[PERSONA] Orpheus grew to Lv.2!", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("-> Orpheus learned a new skill: Agi!", io.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PersonaScaleToLevel_UsesFrameworkGrowthAndRecalculatesEligibleSkills()
    {
        var persona = new Persona
        {
            Name = "Pixie",
            Level = 1,
            SkillsToLearn = { [2] = "Dia", [3] = "Patra" }
        };
        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            persona.StatModifiers[stat] = 40;
        }

        persona.ScaleToLevel(3);

        Assert.Equal(3, persona.Level);
        Assert.Equal(0, persona.Exp);
        Assert.Equal(["Dia", "Patra"], persona.SkillSet);
        Assert.All(Enum.GetValues<StatType>(), stat => Assert.Equal(40, persona.StatModifiers[stat]));
    }

    private static Combatant CreateCombatant(ClassType classType, int baseStat, int personaStat)
    {
        var actor = new Combatant("Hero", classType)
        {
            ActivePersona = new Persona { Name = "Orpheus", Level = 1 }
        };

        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            actor.CharacterStats[stat] = baseStat;
            actor.ActivePersona.StatModifiers[stat] = personaStat;
        }

        if (classType == ClassType.Demon)
        {
            foreach (StatType stat in Enum.GetValues<StatType>())
            {
                actor.CharacterStats[stat] = 0;
            }
        }

        return actor;
    }
}
