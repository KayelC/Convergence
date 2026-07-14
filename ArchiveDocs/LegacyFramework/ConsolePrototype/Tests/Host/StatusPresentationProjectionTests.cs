using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.State;
using Xunit;

namespace Convergence.Tests.Host;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class StatusPresentationProjectionTests
{
    [Fact]
    public void PersonaProjection_RendersDetailedStatusWithoutChangingText()
    {
        var persona = new Persona
        {
            Name = "Orpheus",
            Race = "Fool",
            Level = 4,
            Exp = 12
        };
        persona.StatModifiers[StatType.St] = 3;
        persona.StatModifiers[StatType.Ma] = 5;
        persona.StatModifiers[StatType.Vi] = 4;
        persona.StatModifiers[StatType.Ag] = 6;
        persona.StatModifiers[StatType.Lu] = 7;
        persona.AffinityMap[Element.Fire] = Affinity.Weak;
        persona.AffinityMap[Element.Ice] = Affinity.Null;
        persona.SkillSet.AddRange(["Bash", "Agi"]);
        persona.SkillsToLearn[5] = "Tarukaja";
        persona.SkillsToLearn[6] = "Dia";
        persona.SkillsToLearn[7] = "Patra";
        persona.SkillsToLearn[8] = "Zio";

        string expected =
            $"=== PERSONA DETAILS [EQUIPPED] ==={Environment.NewLine}" +
            $"Name: Orpheus (Lv.4) | Race: Fool{Environment.NewLine}" +
            $"EXP:     12/{persona.ExpRequired,6} Next: {persona.ExpRequired - persona.Exp,6}{Environment.NewLine}" +
            $"-----------------------------{Environment.NewLine}" +
            $"Raw Stats:{Environment.NewLine}" +
            $" St  :   3{Environment.NewLine}" +
            $" Ma  :   5{Environment.NewLine}" +
            $" Vi  :   4{Environment.NewLine}" +
            $" Ag  :   6{Environment.NewLine}" +
            $" Lu  :   7{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"RESISTANCES:{Environment.NewLine}" +
            $" Fire      : Weak{Environment.NewLine}" +
            $" Ice       : Null{Environment.NewLine}" +
            $"-----------------------------{Environment.NewLine}" +
            $"Skills:{Environment.NewLine}" +
            $" - Bash{Environment.NewLine}" +
            $" - Agi{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"Next to Learn:{Environment.NewLine}" +
            $" [Lv. 5] Tarukaja{Environment.NewLine}" +
            $" [Lv. 6] Dia{Environment.NewLine}" +
            $" [Lv. 7] Patra{Environment.NewLine}";

        var bridge = new StatusUIBridge(new ScriptedGameIO(), new FieldUIState(), new PartyManager(new Combatant("Hero")));

        Assert.Equal(expected, LegacyPersonaStatusProjection.FromPersona(persona).RenderDetails(isEquipped: true));
        Assert.Equal(expected, bridge.RenderPersonaDetailsToString(persona, isEquipped: true));
    }

    [Fact]
    public void DemonProjection_RendersDetailedStatusWithConsolidatedSkills()
    {
        var persona = new Persona
        {
            Name = "Pixie Mask",
            Race = "Fairy",
            Level = 7,
            Exp = 20
        };
        persona.StatModifiers[StatType.St] = 8;
        persona.StatModifiers[StatType.Ma] = 9;
        persona.StatModifiers[StatType.Vi] = 10;
        persona.StatModifiers[StatType.Ag] = 11;
        persona.StatModifiers[StatType.Lu] = 12;
        persona.AffinityMap[Element.Elec] = Affinity.Resist;
        persona.SkillSet.AddRange(["Dia", "Zio"]);
        persona.SkillsToLearn[8] = "Media";

        var demon = new Combatant("Pixie", ClassType.Demon)
        {
            Level = 7,
            Exp = 20,
            CurrentHP = 33,
            MaxHP = 44,
            CurrentSP = 12,
            MaxSP = 22,
            ActivePersona = persona
        };
        demon.ExtraSkills.Add("Inherited Boost");

        string expected =
            $"=== DEMON DETAILS ==={Environment.NewLine}" +
            $"Name: Pixie (Lv.7){Environment.NewLine}" +
            $"HP:  33/ 44 SP:  12/ 22{Environment.NewLine}" +
            $"EXP:     20/{demon.ExpRequired,6} Next: {demon.ExpRequired - demon.Exp,6}{Environment.NewLine}" +
            $"-----------------------------{Environment.NewLine}" +
            $"St  :   8{Environment.NewLine}" +
            $"Ma  :   9{Environment.NewLine}" +
            $"Vi  :  10{Environment.NewLine}" +
            $"Ag  :  11{Environment.NewLine}" +
            $"Lu  :  12{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"RESISTANCES:{Environment.NewLine}" +
            $" Elec      : Resist{Environment.NewLine}" +
            $"-----------------------------{Environment.NewLine}" +
            $"Skills:{Environment.NewLine}" +
            $" - Dia{Environment.NewLine}" +
            $" - Zio{Environment.NewLine}" +
            $" - Inherited Boost{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"Next to Learn:{Environment.NewLine}" +
            $" [Lv. 8] Media{Environment.NewLine}";

        var bridge = new StatusUIBridge(new ScriptedGameIO(), new FieldUIState(), new PartyManager(new Combatant("Hero")));

        Assert.Equal(expected, LegacyStatusPresentationProjection.FromCombatant(demon).RenderDemonDetails());
        Assert.Equal(expected, bridge.RenderDemonDetailsToString(demon));
    }

    [Fact]
    public void StatusProjectionLabels_PreserveStockOrganizationAndSummonText()
    {
        var activePersona = new Persona { Name = "Orpheus", Race = "Fool", Level = 5 };
        var stockPersona = new Persona { Name = "Pixie", Race = "Fairy", Level = 3 };
        var leader = new Combatant("Hero") { Level = 11 };
        var demon = new Combatant("Jack Frost", ClassType.Demon)
        {
            Level = 6,
            CurrentHP = 1,
            MaxHP = 20
        };
        var defeated = new Combatant("Slime", ClassType.Demon)
        {
            Level = 2,
            CurrentHP = 0,
            MaxHP = 10
        };

        Assert.Equal("Orpheus         (Lv.5) Fool       [E]", LegacyPersonaStatusProjection.FromPersona(activePersona).StockLabel(isEquipped: true));
        Assert.Equal("Pixie           (Lv.3) Fairy      ", LegacyPersonaStatusProjection.FromPersona(stockPersona).StockLabel(isEquipped: false));
        Assert.Equal("Leader: Hero            (Lv.11)", LegacyStatusPresentationProjection.FromCombatant(leader).OrganizationSlotLabel(0));
        Assert.Equal("Slot 3: [EMPTY]", LegacyStatusPresentationProjection.EmptyOrganizationSlotLabel(2));
        Assert.Equal("Jack Frost      (Lv.6) [PARTY]", LegacyStatusPresentationProjection.FromCombatant(demon).DemonStockLabel(isInParty: true));
        Assert.Equal("Jack Frost      (Lv.6) [IN PARTY]", LegacyStatusPresentationProjection.FromCombatant(demon).SummonTargetLabel(isInParty: true));
        Assert.Equal("Slime           (Lv.2) [DEAD]", LegacyStatusPresentationProjection.FromCombatant(defeated).SummonTargetLabel(isInParty: false));
        Assert.Equal("[ RETURN JACK FROST TO COMP ]", LegacyStatusPresentationProjection.ReturnToCompLabel(demon));
        Assert.Equal("Weapon:    Practice Sword", LegacyStatusPresentationProjection.EquipmentSlotLabel(EquipmentSlotMenuCommand.Weapon, "Practice Sword"));
    }

    [Fact]
    public void Projections_DefensivelyCopyMutableLegacyCollections()
    {
        var persona = new Persona
        {
            Name = "Pixie",
            Race = "Fairy",
            Level = 3
        };
        persona.SkillSet.Add("Dia");
        persona.AffinityMap[Element.Fire] = Affinity.Weak;
        persona.SkillsToLearn[4] = "Patra";

        LegacyPersonaStatusProjection personaProjection = LegacyPersonaStatusProjection.FromPersona(persona);
        persona.SkillSet.Add("Mutated Skill");
        persona.AffinityMap[Element.Ice] = Affinity.Null;
        persona.SkillsToLearn[5] = "Media";

        string renderedPersona = personaProjection.RenderDetails(isEquipped: false);
        Assert.Contains(" - Dia", renderedPersona, StringComparison.Ordinal);
        Assert.DoesNotContain("Mutated Skill", renderedPersona, StringComparison.Ordinal);
        Assert.Contains(" Fire      : Weak", renderedPersona, StringComparison.Ordinal);
        Assert.DoesNotContain(" Ice       : Null", renderedPersona, StringComparison.Ordinal);
        Assert.Contains("[Lv. 4] Patra", renderedPersona, StringComparison.Ordinal);
        Assert.DoesNotContain("[Lv. 5] Media", renderedPersona, StringComparison.Ordinal);

        var actor = new Combatant("Hero")
        {
            Level = 5,
            CurrentHP = 10,
            MaxHP = 20,
            ActivePersona = persona
        };
        actor.ExtraSkills.Add("Original");
        LegacyStatusPresentationProjection actorProjection = LegacyStatusPresentationProjection.FromCombatant(actor);
        actor.CurrentHP = 1;
        actor.ExtraSkills.Add("Later");

        string renderedActor = actorProjection.RenderHumanStatus();
        Assert.Contains("HP:  10/ 20", renderedActor, StringComparison.Ordinal);
        Assert.Equal(["Dia", "Mutated Skill", "Original"], actorProjection.DisplaySkills);
    }
}
