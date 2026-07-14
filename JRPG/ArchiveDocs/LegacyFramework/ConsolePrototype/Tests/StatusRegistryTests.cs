using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using Xunit;

namespace Convergence.Tests;

public sealed class StatusRegistryTests
{
    [Fact]
    public void LegacyTurnStartRestrictionAdapter_PreservesLimitedActionIds()
    {
        BattleTurnStartRestriction restriction =
            LegacyTurnStartRestrictionAdapter.ToFramework(TurnStartResult.LimitedAction);

        Assert.Equal(BattleTurnStartOutcome.LimitedAction, restriction.Outcome);
        Assert.Equal(
            [ContentId.Parse("basic_attack"), ContentId.Parse("guard"), ContentId.Parse("pass")],
            restriction.AllowedActionIds);
    }

    [Fact]
    public void IsActionRedundant_ReturnsFalseForDamagingSkillEvenWhenSecondaryAilmentAlreadyExists()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        var poison = AddAilment("Poison");
        target.InflictAilment(poison);
        var skill = CreateSkill("Toxic Sting", "Ailment", power: "50", effect: "May inflict Poison.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.False(result);
    }

    [Fact]
    public void IsActionRedundant_ReturnsTrueWhenAllTargetsAlreadyHaveRequestedAilment()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var targetA = CreateCombatant("Target A");
        var targetB = CreateCombatant("Target B");
        var panic = AddAilment("Panic");
        targetA.InflictAilment(panic);
        targetB.InflictAilment(panic);
        var skill = CreateSkill("Pulinpa", "Mind Skills", effect: "Chance to inflict Panic.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { targetA, targetB });

        Assert.True(result);
    }

    [Fact]
    public void IsActionRedundant_ReturnsFalseWhenAnyTargetCanStillReceiveRequestedAilment()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var afflicted = CreateCombatant("Afflicted");
        var openTarget = CreateCombatant("Open Target");
        afflicted.InflictAilment(AddAilment("Panic"));
        var skill = CreateSkill("Pulinpa", "Mind Skills", effect: "Chance to inflict Panic.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { afflicted, openTarget });

        Assert.False(result);
    }

    [Theory]
    [InlineData(70, true)]
    [InlineData(69, false)]
    public void IsActionRedundant_Characterization_HpRecoveryUsesSeventyPercentThreshold(
        int currentHp,
        bool expected)
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        target.CurrentHP = currentHp;
        var skill = CreateSkill("Dia", "Recovery", effect: "Restores HP to one ally.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(40, true)]
    [InlineData(39, false)]
    public void IsActionRedundant_Characterization_SpRecoveryUsesEightyPercentThreshold(
        int currentSp,
        bool expected)
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        target.CurrentSP = currentSp;
        var skill = CreateSkill("Spirit Drain", "Recovery", effect: "Restores SP to one ally.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsActionRedundant_ReturnsTrueForCureWhenNoTargetsHaveAilments()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        var skill = CreateSkill("Patra", "Recovery", effect: "Cure Panic.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.True(result);
    }

    [Fact]
    public void IsActionRedundant_ReturnsFalseForCureWhenAnyTargetHasAilment()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        target.InflictAilment(AddAilment("Panic"));
        var skill = CreateSkill("Patra", "Recovery", effect: "Cure Panic.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.False(result);
    }

    [Fact]
    public void IsActionRedundant_ReturnsTrueWhenAllTargetsHaveRelevantBuffAtPlusThree()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        target.Buffs["PhysAtk"] = 3;
        var skill = CreateSkill("Tarukaja", "Enhance", effect: "Raises physical attack.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.True(result);
    }

    [Fact]
    public void IsActionRedundant_ReturnsFalseWhenRelevantBuffIsBelowPlusThree()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        target.Buffs["PhysAtk"] = 2;
        var skill = CreateSkill("Tarukaja", "Enhance", effect: "Raises physical attack.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.False(result);
    }

    [Fact]
    public void IsActionRedundant_ReturnsTrueWhenAllTargetsHaveRelevantDebuffAtMinusThree()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var target = CreateCombatant("Target");
        target.Buffs["Defense"] = -3;
        var skill = CreateSkill("Rakunda", "Enhance", effect: "Lowers defense.");

        bool result = registry.IsActionRedundant(actor, skill, new List<Combatant> { target });

        Assert.True(result);
    }

    [Fact]
    public void CheckAndExecuteCure_RemovesAilmentWhenEffectMatchesCurrentAilment()
    {
        var registry = new StatusRegistry();
        var target = CreateCombatant("Target");
        target.InflictAilment(AddAilment("Panic"));

        bool result = registry.CheckAndExecuteCure(target, "Cures Panic.");

        Assert.True(result);
        Assert.Null(target.CurrentAilment);
    }

    [Fact]
    public void CheckAndExecuteCure_ReturnsFalseWhenEffectDoesNotMatchCurrentAilment()
    {
        var registry = new StatusRegistry();
        var target = CreateCombatant("Target");
        target.InflictAilment(AddAilment("Panic"));

        bool result = registry.CheckAndExecuteCure(target, "Cures Poison.");

        Assert.False(result);
        Assert.Equal("Panic", target.CurrentAilment?.Name);
    }

    [Fact]
    public void ApplyStatChange_RoutesSingleBuffsToExpectedTracks()
    {
        var registry = new StatusRegistry();
        var target = CreateCombatant("Target");

        registry.ApplyStatChange("Tarukaja", target);
        registry.ApplyStatChange("Makakaja", target);
        registry.ApplyStatChange("Rakukaja", target);
        registry.ApplyStatChange("Sukukaja", target);

        Assert.Equal(1, target.Buffs["PhysAtk"]);
        Assert.Equal(1, target.Buffs["MagAtk"]);
        Assert.Equal(1, target.Buffs["Defense"]);
        Assert.Equal(1, target.Buffs["Agility"]);
    }

    [Fact]
    public void ApplyStatChange_HeatRiserBuffsAllTracks()
    {
        var registry = new StatusRegistry();
        var target = CreateCombatant("Target");

        registry.ApplyStatChange("Heat Riser", target);

        Assert.Equal(1, target.Buffs["PhysAtk"]);
        Assert.Equal(1, target.Buffs["MagAtk"]);
        Assert.Equal(1, target.Buffs["Defense"]);
        Assert.Equal(1, target.Buffs["Agility"]);
    }

    [Fact]
    public void ApplyStatChange_DebilitateDebuffsAllTracks()
    {
        var registry = new StatusRegistry();
        var target = CreateCombatant("Target");

        registry.ApplyStatChange("Debilitate", target);

        Assert.Equal(-1, target.Buffs["PhysAtk"]);
        Assert.Equal(-1, target.Buffs["MagAtk"]);
        Assert.Equal(-1, target.Buffs["Defense"]);
        Assert.Equal(-1, target.Buffs["Agility"]);
    }

    [Theory]
    [InlineData("Tarukaja", "PhysAtk", 4)]
    [InlineData("Rakunda", "Defense", -4)]
    public void ApplyStatChange_Characterization_ClampsBuffTracksAtPlusOrMinusFour(
        string skillName,
        string track,
        int expected)
    {
        var registry = new StatusRegistry();
        var target = CreateCombatant("Target");

        for (int i = 0; i < 8; i++)
        {
            registry.ApplyStatChange(skillName, target);
        }

        Assert.Equal(expected, target.Buffs[track]);
    }

    [Fact]
    public void ProcessInitialPassives_AppliesSingleTargetAutoKajaToActor()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        actor.ExtraSkills.Add("Auto-Tarukaja");

        registry.ProcessInitialPassives(actor, new List<Combatant> { actor });

        Assert.Equal(1, actor.Buffs["PhysAtk"]);
    }

    [Fact]
    public void ProcessInitialPassives_AppliesPartyWideAutoKajaToLivingAllies()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        var ally = CreateCombatant("Ally");
        var deadAlly = CreateCombatant("Dead Ally");
        actor.ExtraSkills.Add("Auto-Mataru");
        deadAlly.CurrentHP = 0;

        registry.ProcessInitialPassives(actor, new List<Combatant> { actor, ally, deadAlly });

        Assert.Equal(1, actor.Buffs["PhysAtk"]);
        Assert.Equal(1, ally.Buffs["PhysAtk"]);
        Assert.False(deadAlly.Buffs.ContainsKey("PhysAtk"));
    }

    [Theory]
    [InlineData("SkipTurn", TurnStartResult.Skip)]
    [InlineData("LimitedAction", TurnStartResult.LimitedAction)]
    [InlineData("ConfusedAction", TurnStartResult.ForcedConfusion)]
    [InlineData("ForceAttack", TurnStartResult.ForcedPhysical)]
    [InlineData("None", TurnStartResult.CanAct)]
    public void ProcessTurnStart_ReturnsDeterministicRestrictionResult(
        string restriction,
        TurnStartResult expected)
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        actor.InflictAilment(AddAilment($"{restriction} Ailment", restriction));
        actor.IsGuarding = true;

        TurnStartResult result = registry.ProcessTurnStart(actor);

        Assert.Equal(expected, result);
        Assert.False(actor.IsGuarding);
    }

    [Fact]
    public void ProcessTurnEnd_DoesNothingForStandbyCombatant()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        actor.PartySlot = -1;
        actor.CurrentHP = 100;
        actor.InflictAilment(AddAilment("Poison", dotPercent: 13));

        registry.ProcessTurnEnd(actor);

        Assert.Equal(100, actor.CurrentHP);
        Assert.Equal("Poison", actor.CurrentAilment?.Name);
    }

    [Fact]
    public void ProcessTurnEnd_Characterization_PoisonDealsThirteenPercentMaxHpDamage()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        actor.PartySlot = 0;
        actor.CurrentHP = 100;
        actor.InflictAilment(AddAilment("Poison", dotPercent: 13), duration: 3);

        registry.ProcessTurnEnd(actor);

        Assert.Equal(87, actor.CurrentHP);
        Assert.Equal(2, actor.AilmentDuration);
    }

    [Fact]
    public void ProcessTurnEnd_RemovesOneTurnAilmentsImmediately()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        actor.PartySlot = 0;
        actor.InflictAilment(AddAilment("Stun", removalTriggers: new List<string> { "OneTurn" }), duration: 3);

        registry.ProcessTurnEnd(actor);

        Assert.Null(actor.CurrentAilment);
        Assert.Equal(0, actor.AilmentDuration);
    }

    [Fact]
    public void ProcessTurnEnd_RemovesAilmentWhenDurationExpires()
    {
        var registry = new StatusRegistry();
        var actor = CreateCombatant("Actor");
        actor.PartySlot = 0;
        actor.InflictAilment(AddAilment("Bind"), duration: 1);

        registry.ProcessTurnEnd(actor);

        Assert.Null(actor.CurrentAilment);
        Assert.Equal(0, actor.AilmentDuration);
    }

    private static Combatant CreateCombatant(string name)
    {
        var combatant = new Combatant(name)
        {
            SourceId = name,
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50,
            ActivePersona = new Persona { Name = $"{name} Persona" }
        };

        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            combatant.CharacterStats[stat] = 10;
            combatant.ActivePersona.StatModifiers[stat] = 10;
        }

        return combatant;
    }

    private static SkillData CreateSkill(
        string name,
        string category,
        string power = "-",
        string effect = "Test effect.")
    {
        return new SkillData
        {
            Name = name,
            Category = category,
            Power = power,
            Effect = effect,
            Cost = "0 SP",
            Accuracy = "100%"
        };
    }

    private static AilmentData AddAilment(
        string name,
        string actionRestriction = "None",
        double dotPercent = 0,
        List<string>? removalTriggers = null)
    {
        var ailment = new AilmentData
        {
            Name = name,
            ActionRestriction = actionRestriction,
            DotPercent = dotPercent,
            DamageDealMult = 1.0,
            DamageTakenMult = 1.0,
            RemovalTriggers = removalTriggers ?? new List<string>(),
            CureKeyword = string.Empty,
            Description = $"{name} test ailment."
        };

        Database.Ailments[name] = ailment;

        return ailment;
    }
}
