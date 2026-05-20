using JRPGPrototype.Core;
using JRPGPrototype.Data;
using Xunit;

#pragma warning disable CS0618 // Legacy adapter tests are intentionally retained as compatibility coverage.

namespace Convergence.Tests;

public sealed class SkillDefinitionMapperTests
{
    private static readonly AilmentData[] Ailments =
    [
        new AilmentData { Name = "Poison" },
        new AilmentData { Name = "Bind" }
    ];

    [Fact]
    public void MapLegacySkill_DamageSkillUsesTypedPowerAccuracyAndElement()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Agi",
            effect: "Deals light Fire damage to one foe.",
            power: "50",
            accuracy: "95%",
            category: "Fire Skills"));

        var payload = Assert.IsType<DamageSkillPayload>(definition.Payload);
        Assert.Equal("agi", definition.Id);
        Assert.Equal(SkillKind.Damage, definition.Kind);
        Assert.Equal(SkillTargeting.SingleEnemy, definition.Targeting);
        Assert.Equal(Element.Fire, payload.Element);
        Assert.Equal(50, payload.Power);
        Assert.Equal(95, payload.Accuracy);
    }

    [Fact]
    public void MapLegacySkill_DamageAilmentSkillSeparatesAttackAccuracyFromAilmentChance()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Toxic Sting",
            effect: "Deals light Pierce damage to one foe, with a chance to Poison (40% Poison)",
            power: "62",
            accuracy: "76%",
            critical: "24%",
            cost: "7% HP",
            category: "Pierce Skills"));

        var payload = Assert.IsType<DamageSkillPayload>(definition.Payload);
        Assert.Equal(76, payload.Accuracy);
        Assert.Equal(24, payload.CriticalChance);
        Assert.Equal(new SecondaryAilmentDefinition("poison", 40), payload.SecondaryAilment);
        Assert.Equal(new SkillCostDefinition(SkillCostResource.HP, 7, true), definition.Cost);
    }

    [Fact]
    public void MapLegacySkill_HealingSkillInfersFlatHealAmount()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Dia",
            effect: "Slightly (50) restores 1 ally's HP.",
            category: "Recovery Skills",
            family: "Heal_ST"));

        var payload = Assert.IsType<HealingSkillPayload>(definition.Payload);
        Assert.Equal(SkillKind.Healing, definition.Kind);
        Assert.Equal(SkillTargeting.SingleAlly, definition.Targeting);
        Assert.Equal(RecoveryResource.HP, payload.Resource);
        Assert.Equal(RecoveryAmountKind.Flat, payload.AmountKind);
        Assert.Equal(50, payload.Amount);
    }

    [Fact]
    public void MapLegacySkill_ReviveSkillInfersPercentRestore()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Recarm",
            effect: "Revives an ally, restoring 50% of HP.",
            category: "Recovery Skills",
            family: "Revive"));

        var payload = Assert.IsType<ReviveSkillPayload>(definition.Payload);
        Assert.Equal(SkillKind.Revive, definition.Kind);
        Assert.Equal(SkillTargeting.DeadAlly, definition.Targeting);
        Assert.Equal(RecoveryAmountKind.Percent, payload.AmountKind);
        Assert.Equal(50, payload.Amount);
    }

    [Fact]
    public void MapLegacySkill_BuffSkillMapsAffectedStatTrack()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Tarukaja",
            effect: "Increases 1 ally's Physical Attack by 25%*.",
            category: "Enhance Skills",
            family: "Tarukaja",
            rank: "1"));

        var payload = Assert.IsType<BuffDebuffSkillPayload>(definition.Payload);
        Assert.Equal(SkillKind.BuffDebuff, definition.Kind);
        Assert.Equal(SkillTargeting.SingleAlly, definition.Targeting);
        Assert.Equal(1, payload.StageDelta);
        Assert.Equal([StatModifierTrack.PhysAtk], payload.Tracks);
        Assert.Equal("Tarukaja", definition.Inheritance.Family);
        Assert.Equal(1, definition.Inheritance.Rank);
    }

    [Fact]
    public void MapLegacySkill_MindChargeUsesTypedChargeKind()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Mind Charge",
            effect: "Display copy can change without affecting behavior.",
            category: "Enhance Skills"));

        var payload = Assert.IsType<ChargeSkillPayload>(definition.Payload);
        Assert.Equal(SkillKind.Charge, definition.Kind);
        Assert.Equal(SkillTargeting.Self, definition.Targeting);
        Assert.Equal(ChargeKind.Magical, payload.Kind);
        Assert.Equal(1.9, payload.Multiplier);
    }

    [Fact]
    public void MapLegacySkill_BreakSkillUsesTypedElement()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Fire Break",
            effect: "Reduces 1 foe's Fire resistance to average.",
            cost: "40 SP*",
            category: "Enhance Skills"));

        var payload = Assert.IsType<BreakSkillPayload>(definition.Payload);
        Assert.Equal(SkillKind.Break, definition.Kind);
        Assert.Equal(Element.Fire, payload.Element);
        Assert.Equal(3, payload.Duration);
    }

    [Fact]
    public void MapLegacySkill_ShieldSkillUsesTypedShieldKind()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Tetrakarn",
            effect: "Barrier that reflects physical damage 1x per ally.",
            category: "Enhance Skills"));

        var payload = Assert.IsType<ShieldSkillPayload>(definition.Payload);
        Assert.Equal(SkillKind.Shield, definition.Kind);
        Assert.Equal(SkillTargeting.SingleAlly, definition.Targeting);
        Assert.Equal(ShieldKind.Physical, payload.Kind);
    }

    [Fact]
    public void MapLegacySkill_AilmentSkillMapsAilmentAndChance()
    {
        SkillDefinition definition = MapValid(CreateSkill(
            name: "Poisma",
            effect: "Poisons 1 foe. (40% chance)",
            category: "Nerve Skills",
            family: "Poison",
            rank: "1"));

        var payload = Assert.IsType<AilmentSkillPayload>(definition.Payload);
        Assert.Equal(SkillKind.Ailment, definition.Kind);
        Assert.Equal(SkillTargeting.SingleEnemy, definition.Targeting);
        Assert.Equal("poison", payload.AilmentId);
        Assert.Equal(40, payload.Chance);
    }

    [Fact]
    public void ValidateLegacyData_ReportsMissingEntityInheritanceTypeAndUnresolvedSkills()
    {
        var skills = new[]
        {
            CreateSkill(
                name: "Dia",
                effect: "Slightly (50) restores 1 ally's HP.",
                category: "Recovery Skills")
        };
        var entities = new[]
        {
            new PersonaData
            {
                Id = "test_entity",
                Name = "Test Entity",
                BaseSkills = ["Dia", "Missing Skill"],
                LearnedSkillsRaw = new Dictionary<string, string> { ["2"] = "Other Missing" }
            }
        };

        DataValidationResult result = DataValidation.ValidateLegacyData(skills, entities, Ailments);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("missing InheritanceType"));
        Assert.Contains(result.Errors, e => e.Contains("Missing Skill"));
        Assert.Contains(result.Errors, e => e.Contains("Other Missing"));
    }

    [Fact]
    public void GameDataCatalog_ProvidesRepositoryLookups()
    {
        SkillDefinition skill = MapValid(CreateSkill(
            name: "Agi",
            effect: "Deals light Fire damage to one foe.",
            power: "50",
            accuracy: "95%",
            category: "Fire Skills"));
        var entity = new PersonaData { Id = "jack_frost", Name = "Jack Frost", Race = "Fairy", Rank = 1, InheritanceType = "Ice" };
        var ailment = new AilmentData { Name = "Poison" };
        var catalog = new GameDataCatalog([skill], [entity], [ailment]);

        ISkillRepository skills = catalog;
        IEntityRepository entities = catalog;
        IAilmentRepository ailments = catalog;

        Assert.Equal(skill, skills.GetById("agi"));
        Assert.Equal(skill, skills.GetByDisplayName("Agi"));
        Assert.Equal(entity, entities.GetById("JACK_FROST"));
        Assert.Equal(entity, Assert.Single(entities.GetByRaceAndRank("fairy", 1)));
        Assert.Equal(ailment, ailments.GetByIdOrName("poison"));
        Assert.Equal(ailment, ailments.GetByIdOrName("Poison"));
    }

    private static SkillDefinition MapValid(SkillData skill)
    {
        SkillDefinitionMappingResult result = SkillDefinitionMapper.MapLegacySkill(skill, Ailments);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return result.Definition!;
    }

    private static SkillData CreateSkill(
        string name,
        string effect,
        string power = "-",
        string accuracy = "-",
        string critical = "-",
        string cost = "5 SP",
        string category = "Recovery Skills",
        string family = "-",
        string rank = "-")
    {
        return new SkillData
        {
            Name = name,
            Effect = effect,
            Power = power,
            Accuracy = accuracy,
            Critical = critical,
            Cost = cost,
            Category = category,
            Family = family,
            Rank = rank
        };
    }
}

#pragma warning restore CS0618
