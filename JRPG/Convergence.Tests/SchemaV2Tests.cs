using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Schemas;
using Newtonsoft.Json;
using Xunit;

namespace Convergence.Tests;

public sealed class SchemaV2Tests
{
    [Fact]
    public void SkillDatabaseV2Sample_LoadsValidTypedDefinitions()
    {
        SkillDatabaseV2 database = LoadJson<SkillDatabaseV2>("skills_database_v2.sample.json");

        DataValidationResult validation = SkillSchemaV2Validator.Validate(database);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));

        SkillDefinition venomNeedle = database.Skills.Single(s => s.Id == "venom_needle").ToDefinition();
        var damage = Assert.IsType<DamageSkillPayload>(venomNeedle.Payload);

        Assert.Equal("Venom Needle", venomNeedle.DisplayName);
        Assert.Equal(SkillKind.Damage, venomNeedle.Kind);
        Assert.Equal(SkillTargeting.SingleEnemy, venomNeedle.Targeting);
        Assert.Equal(new SkillCostDefinition(SkillCostResource.HP, 7, true), venomNeedle.Cost);
        Assert.Equal(Element.Pierce, damage.Element);
        Assert.Equal(76, damage.Accuracy);
        Assert.Equal(new SecondaryAilmentDefinition("poison", 40), damage.SecondaryAilment);
    }

    [Fact]
    public void SkillDatabaseV2Sample_UsesTypedPayloadsInsteadOfDisplayTextForBehavior()
    {
        SkillDatabaseV2 database = LoadJson<SkillDatabaseV2>("skills_database_v2.sample.json");

        SkillDefinition astralFocus = database.Skills.Single(s => s.Id == "astral_focus").ToDefinition();
        var charge = Assert.IsType<ChargeSkillPayload>(astralFocus.Payload);

        Assert.Equal("Astral Focus", astralFocus.DisplayName);
        Assert.Equal("Focuses the user's next magical attack.", astralFocus.Description);
        Assert.Equal(ChargeKind.Magical, charge.Kind);
        Assert.Equal(1.9, charge.Multiplier);
    }

    [Fact]
    public void SkillSchemaV2Validator_RejectsMissingOrMismatchedPayloads()
    {
        var database = new SkillDatabaseV2([
            new SkillDefinitionDto(
                Id: "broken_skill",
                DisplayName: "Broken Skill",
                Description: "Invalid schema fixture.",
                Kind: SkillKind.Damage,
                Cost: new SkillCostDto(SkillCostResource.SP, 1),
                Targeting: SkillTargeting.SingleEnemy,
                Inheritance: new SkillInheritanceDto(true),
                Healing: new HealingPayloadDto(RecoveryResource.HP, RecoveryAmountKind.Flat, 50))
        ]);

        DataValidationResult validation = SkillSchemaV2Validator.Validate(database);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("is Damage but does not define a Damage payload"));
    }

    [Fact]
    public void EntityDatabaseV2Sample_LoadsInheritanceTypesAndResolvesSkillIds()
    {
        SkillDatabaseV2 skills = LoadJson<SkillDatabaseV2>("skills_database_v2.sample.json");
        EntityDatabaseV2 entities = LoadJson<EntityDatabaseV2>("entity_database_v2.sample.json");
        string[] knownSkillIds = skills.Skills.Select(s => s.Id).ToArray();

        DataValidationResult validation = EntitySchemaV2Validator.Validate(entities, knownSkillIds);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));

        EntityDefinitionDto ashWisp = entities.Entities.Single(e => e.Id == "ash_wisp");
        Assert.Equal("Fire", ashWisp.InheritanceType);
        Assert.Contains("ember_flicker", ashWisp.BaseSkills);
        Assert.Equal("flame_instinct", ashWisp.LearnedSkills[6]);
    }

    private static T LoadJson<T>(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "Jsons", fileName);
        string json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<T>(json)
               ?? throw new InvalidOperationException($"Could not load {fileName}.");
    }
}
