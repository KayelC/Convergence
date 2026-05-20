using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.Definitions.Catalogs;
using JRPGPrototype.Data.Definitions.Schemas;
using Newtonsoft.Json;
using Xunit;

namespace Convergence.Tests;

public sealed class CleanDataMigrationTests
{
    [Fact]
    public void SkillDataSchema_LoadsValidDamagePayloadWithoutLegacyInference()
    {
        SkillDataSchema schema = LoadJson<SkillDataSchema>("skill_data.sample.json");

        SchemaValidationResult validation = SkillDataSchemaValidator.Validate(schema);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));

        SkillDefinition venomNeedle = schema.Skills.Single(s => s.Id == "venom_needle").ToDefinition();
        var damage = Assert.IsType<DamageSkillPayload>(venomNeedle.Payload);

        Assert.Equal(SkillKind.Damage, venomNeedle.Kind);
        Assert.Equal(SkillTargeting.SingleEnemy, venomNeedle.Targeting);
        Assert.Equal(new SkillCostDefinition(SkillCostResource.HP, 7, true), venomNeedle.Cost);
        Assert.Equal(Element.Pierce, damage.Element);
        Assert.Equal(62, damage.Power);
        Assert.Equal(76, damage.Accuracy);
        Assert.Equal(24, damage.CriticalChance);
        Assert.Equal(new SecondaryAilmentDefinition("poison", 40), damage.SecondaryAilment);
    }

    [Fact]
    public void SkillDataSchema_DisplayTextDoesNotDriveTypedBehavior()
    {
        var schema = new SkillDataSchema([
            new SkillSchemaEntry(
                Id: "renamed_focus",
                DisplayName: "Any Display Name",
                Description: "Any localized description.",
                Kind: SkillKind.Charge,
                Cost: new SkillCostSchema(SkillCostResource.SP, 15),
                Targeting: SkillTargeting.Self,
                Inheritance: new SkillInheritanceSchema(true),
                Charge: new ChargePayloadSchema(ChargeKind.Magical, 1.9))
        ]);

        SkillDefinition definition = schema.Skills.Single().ToDefinition();
        var charge = Assert.IsType<ChargeSkillPayload>(definition.Payload);

        Assert.Equal("Any Display Name", definition.DisplayName);
        Assert.Equal("Any localized description.", definition.Description);
        Assert.Equal(ChargeKind.Magical, charge.Kind);
        Assert.Equal(1.9, charge.Multiplier);
    }

    [Fact]
    public void SkillDataSchemaValidator_RejectsMissingRequiredPayload()
    {
        var schema = new SkillDataSchema([
            new SkillSchemaEntry(
                Id: "broken_damage",
                DisplayName: "Broken Damage",
                Description: "Invalid clean schema fixture.",
                Kind: SkillKind.Damage,
                Cost: new SkillCostSchema(SkillCostResource.SP, 1),
                Targeting: SkillTargeting.SingleEnemy,
                Inheritance: new SkillInheritanceSchema(true),
                Healing: new HealingPayloadSchema(RecoveryResource.HP, RecoveryAmountKind.Flat, 50))
        ]);

        SchemaValidationResult validation = SkillDataSchemaValidator.Validate(schema);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("is Damage but does not define a Damage payload"));
    }

    [Fact]
    public void EntityDataSchema_LoadsTypedEntityDefinitionAndResolvesSkillIds()
    {
        SkillDataSchema skills = LoadJson<SkillDataSchema>("skill_data.sample.json");
        EntityDataSchema entities = LoadJson<EntityDataSchema>("entity_data.sample.json");
        string[] knownSkillIds = skills.Skills.Select(s => s.Id).ToArray();

        SchemaValidationResult validation = EntityDataSchemaValidator.Validate(entities, knownSkillIds);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));

        EntityDefinition ashWisp = entities.Entities.Single(e => e.Id == "ash_wisp").ToDefinition();

        Assert.Equal("Ash Wisp", ashWisp.DisplayName);
        Assert.Equal("Fire", ashWisp.InheritanceType);
        Assert.Equal(7, ashWisp.Stats[StatType.Ma]);
        Assert.Equal(Affinity.Weak, ashWisp.Affinities[Element.Ice]);
        Assert.Contains("ember_flicker", ashWisp.BaseSkillIds);
        Assert.Equal("flame_instinct", ashWisp.LearnedSkillIds[6]);
    }

    [Fact]
    public void EntityDataSchemaValidator_RejectsUnknownSkillIds()
    {
        var entities = new EntityDataSchema([
            new EntitySchemaEntry(
                Id: "broken_entity",
                DisplayName: "Broken Entity",
                Race: "Spirit",
                Rank: 1,
                Level: 1,
                InheritanceType: "Fire",
                Stats: new Dictionary<string, int> { ["St"] = 1 },
                Affinities: new Dictionary<string, string> { ["Fire"] = "Normal" },
                BaseSkillIds: ["missing_skill"],
                LearnedSkillIds: new Dictionary<int, string>())
        ]);

        SchemaValidationResult validation = EntityDataSchemaValidator.Validate(entities, Array.Empty<string>());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("unresolved base skill 'missing_skill'"));
    }

    [Fact]
    public void CleanGameDataCatalog_OnlyExposesExplicitCleanDefinitions()
    {
        SkillDataSchema skills = LoadJson<SkillDataSchema>("skill_data.sample.json");
        EntityDataSchema entities = LoadJson<EntityDataSchema>("entity_data.sample.json");

        var catalog = GameDataCatalog.FromSchemas(skills, entities);
        ISkillDefinitionRepository skillRepository = catalog;
        IEntityDefinitionRepository entityRepository = catalog;

        Assert.NotNull(skillRepository.GetById("ember_flicker"));
        Assert.NotNull(entityRepository.GetById("ash_wisp"));
        Assert.Null(skillRepository.GetById("Agi"));
        Assert.Null(entityRepository.GetById("Jack Frost"));
        Assert.Single(entityRepository.GetByRaceAndRank("spirit", 1));
    }

    private static T LoadJson<T>(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "Jsons", fileName);
        string json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<T>(json)
               ?? throw new InvalidOperationException($"Could not load {fileName}.");
    }
}
