using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests.Parity;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class RecoveryParityLedgerTests
{
    private static readonly string[] ExpectedCapabilityIds =
    [
        "active_and_reserve_party",
        "actor_models",
        "ailment_lifecycle",
        "battle_actions",
        "battle_knowledge",
        "battle_rewards",
        "combat_math",
        "compendium",
        "console_presentation",
        "dungeon_traversal",
        "economy",
        "encounters",
        "enemy_ai_and_tactics",
        "equipment_ownership",
        "field_items_and_skills",
        "field_navigation",
        "fusion_preview_confirmation",
        "fusion_result_calculation",
        "fusion_slots_mutation_accidents",
        "fusion_strategies",
        "fusion_transactions",
        "growth_and_levels",
        "hospital",
        "interactive_boot",
        "inventory_quantities",
        "moon_phase",
        "negotiation_and_recruitment",
        "party_operations",
        "passive_lifecycle",
        "persistence_snapshots",
        "persona_and_demon_stock",
        "press_turn",
        "resource_recalculation",
        "shops",
        "stat_composition",
        "typed_effects"
    ];

    [Fact]
    public void RecoveryLedger_CoversEveryProtectedCapabilityWithValidEvidence()
    {
        using JsonDocument document = LoadLedger();
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        JsonElement baseline = root.GetProperty("baseline");
        Assert.Equal("track-12-recovery", baseline.GetProperty("branch").GetString());
        Assert.Equal("fce33a9", baseline.GetProperty("startingCommit").GetString());
        Assert.Equal(448, baseline.GetProperty("testCount").GetInt32());
        Assert.Equal(0, baseline.GetProperty("skippedTestCount").GetInt32());
        Assert.Equal(122, baseline.GetProperty("buildWarningCount").GetInt32());

        JsonElement[] capabilities = root.GetProperty("capabilities").EnumerateArray().ToArray();
        string[] actualIds = capabilities
            .Select(capability => RequiredString(capability, "id"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCapabilityIds, actualIds);
        Assert.Equal(actualIds.Length, actualIds.Distinct(StringComparer.Ordinal).Count());

        HashSet<string> validStatuses =
        [
            "legacy_only",
            "parallel_partial",
            "clean_foundation",
            "clean_parity"
        ];

        foreach (JsonElement capability in capabilities)
        {
            string id = RequiredString(capability, "id");
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(capability, "name")));
            Assert.Contains(RequiredString(capability, "status"), validStatuses);
            Assert.NotEmpty(RequiredStrings(capability, "legacyOwners"));
            Assert.NotEmpty(RequiredStrings(capability, "tests"));
            Assert.NotEmpty(RequiredStrings(capability, "removalFiles"));

            string futureTrack = RequiredString(capability, "futureTrack");
            Assert.True(
                futureTrack.Length == 1 && futureTrack[0] is >= 'B' and <= 'S',
                $"Capability '{id}' has invalid future track '{futureTrack}'.");

            bool consumerMigrated = capability.GetProperty("consumerMigrated").GetBoolean();
            bool removalAuthorized = capability.GetProperty("removalAuthorized").GetBoolean();
            if (removalAuthorized)
            {
                Assert.Equal("clean_parity", RequiredString(capability, "status"));
                Assert.True(consumerMigrated, $"Capability '{id}' authorized removal without a migrated consumer.");
            }
        }
    }

    private static JsonDocument LoadLedger()
    {
        string path = Path.Combine(
            LegacyBaselineSupport.RepositoryRoot,
            "Convergence.Tests",
            "Fixtures",
            "Parity",
            "recovery-baseline.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string RequiredString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString()
        ?? throw new InvalidDataException($"'{propertyName}' must be a string.");

    private static string[] RequiredStrings(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString() ?? throw new InvalidDataException($"'{propertyName}' contains null."))
            .ToArray();
}

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class RecoveryDatasetBaselineTests
{
    [Fact]
    public void DatasetCounts_PreserveTheRecoveryBaseline()
    {
        using JsonDocument skills = Load("skills_database.json");
        using JsonDocument entities = Load("entity_database.json");
        using JsonDocument ailments = Load("status_ailments.json");
        using JsonDocument items = Load("items.json");
        using JsonDocument weapons = Load("weapons.json");
        using JsonDocument armor = Load("armor.json");
        using JsonDocument boots = Load("boots.json");
        using JsonDocument accessories = Load("accessories.json");
        using JsonDocument fusion = Load("fusion_table.json");
        using JsonDocument shop = Load("shop_inventory.json");
        using JsonDocument dungeon = Load("tartarus.json");
        using JsonDocument negotiation = Load("questions.json");

        string[] skillNames = SkillNames(skills).ToArray();
        Assert.Equal(420, skillNames.Length);
        Assert.Equal(
            ["Feral Claw", "Life Aid", "Trafuri"],
            skillNames.GroupBy(name => name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        JsonElement[] entityRecords = entities.RootElement.EnumerateArray().ToArray();
        Assert.Equal(304, entityRecords.Length);
        Assert.DoesNotContain(
            entityRecords
                .Select(entity => entity.GetProperty("Id").GetString()!)
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase),
            group => group.Count() > 1);

        Assert.Equal(11, ailments.RootElement.GetProperty("ailments").GetArrayLength());
        Assert.Equal(14, items.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(26, weapons.RootElement.GetProperty("weapons").GetArrayLength());
        Assert.Equal(3, armor.RootElement.GetProperty("armor").GetArrayLength());
        Assert.Equal(3, boots.RootElement.GetProperty("boots").GetArrayLength());
        Assert.Equal(3, accessories.RootElement.GetProperty("accessories").GetArrayLength());
        Assert.Equal(460, fusion.RootElement.GetProperty("recipes").GetArrayLength());
        Assert.Equal(30, shop.RootElement.EnumerateObject().Sum(property => property.Value.GetArrayLength()));

        JsonElement[] dungeons = dungeon.RootElement.GetProperty("dungeons").EnumerateArray().ToArray();
        Assert.Single(dungeons);
        Assert.Equal(6, dungeons.Sum(value => value.GetProperty("blocks").GetArrayLength()));

        JsonElement questions = negotiation.RootElement.GetProperty("questions");
        JsonElement familiarDialogues = negotiation.RootElement.GetProperty("familiar_dialogue");
        Assert.Equal(8, questions.EnumerateObject().Count());
        Assert.Equal(40, questions.EnumerateObject().Sum(property => property.Value.GetArrayLength()));
        Assert.Equal(8, familiarDialogues.EnumerateObject().Count());
    }

    [Fact]
    public void DatasetReferences_PreserveKnownIntegrityFindings()
    {
        using JsonDocument skills = Load("skills_database.json");
        using JsonDocument entities = Load("entity_database.json");
        using JsonDocument dungeon = Load("tartarus.json");
        using JsonDocument shop = Load("shop_inventory.json");
        using JsonDocument fusion = Load("fusion_table.json");

        string[] authoredSkillNames = SkillNames(skills).ToArray();
        HashSet<string> skillNames = new(authoredSkillNames, StringComparer.OrdinalIgnoreCase);
        HashSet<string> exactSkillNames = new(authoredSkillNames, StringComparer.Ordinal);
        JsonElement[] entityRecords = entities.RootElement.EnumerateArray().ToArray();
        HashSet<string> entityIds = new(
            entityRecords.Select(entity => entity.GetProperty("Id").GetString()!),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> races = new(
            entityRecords.Select(entity => entity.GetProperty("Race").GetString()!),
            StringComparer.OrdinalIgnoreCase);

        List<string> missingBaseSkills = [];
        List<string> missingLearnedSkills = [];
        List<string> caseMismatchedSkills = [];
        foreach (JsonElement entity in entityRecords)
        {
            string entityId = entity.GetProperty("Id").GetString()!;
            foreach (JsonElement skill in entity.GetProperty("BaseSkills").EnumerateArray())
            {
                string? name = skill.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !skillNames.Contains(name))
                {
                    missingBaseSkills.Add($"{entityId}:{name}");
                }
                else if (!string.IsNullOrWhiteSpace(name) && !exactSkillNames.Contains(name))
                {
                    caseMismatchedSkills.Add($"{entityId}:base:{name}");
                }
            }

            foreach (JsonProperty skill in entity.GetProperty("LearnedSkills").EnumerateObject())
            {
                string? name = skill.Value.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !skillNames.Contains(name))
                {
                    missingLearnedSkills.Add($"{entityId}:{name}");
                }
                else if (!string.IsNullOrWhiteSpace(name) && !exactSkillNames.Contains(name))
                {
                    caseMismatchedSkills.Add($"{entityId}:learned:{name}");
                }
            }
        }

        AssertCount(56, missingBaseSkills, "unresolved base-skill references");
        AssertCount(120, missingLearnedSkills, "unresolved learned-skill references");
        Assert.Equal(["kudlak:base:bufula"], caseMismatchedSkills);

        List<string> missingDungeonPool = [];
        List<string> missingDungeonBosses = [];
        foreach (JsonElement dungeonRecord in dungeon.RootElement.GetProperty("dungeons").EnumerateArray())
        {
            string dungeonId = dungeonRecord.GetProperty("id").GetString()!;
            foreach (JsonElement block in dungeonRecord.GetProperty("blocks").EnumerateArray())
            {
                string blockId = block.GetProperty("block_id").GetString()!;
                foreach (JsonElement enemy in block.GetProperty("enemy_pool").EnumerateArray())
                {
                    string enemyId = enemy.GetString()!;
                    if (!entityIds.Contains(enemyId))
                    {
                        missingDungeonPool.Add($"{dungeonId}/{blockId}:{enemyId}");
                    }
                }

                foreach (JsonElement floor in block.GetProperty("fixed_floors").EnumerateArray())
                {
                    if (floor.GetProperty("type").GetString() == "Boss" &&
                        floor.TryGetProperty("id", out JsonElement bossIdElement))
                    {
                        string bossId = bossIdElement.GetString()!;
                        if (!entityIds.Contains(bossId))
                        {
                            missingDungeonBosses.Add($"{dungeonId}/{blockId}:{bossId}");
                        }
                    }
                }
            }
        }

        Assert.Equal(["tartarus/thebel:ara-mitama"], missingDungeonPool);
        Assert.Empty(missingDungeonBosses);

        Dictionary<string, HashSet<string>> shopTargets = new(StringComparer.Ordinal)
        {
            ["items"] = IdSet("items.json", "items"),
            ["weapons"] = IdSet("weapons.json", "weapons"),
            ["armor"] = IdSet("armor.json", "armor"),
            ["boots"] = IdSet("boots.json", "boots"),
            ["accessories"] = IdSet("accessories.json", "accessories")
        };
        List<string> missingShopReferences = [];
        foreach (JsonProperty category in shop.RootElement.EnumerateObject())
        {
            foreach (JsonElement entry in category.Value.EnumerateArray())
            {
                string id = entry.GetProperty("id").GetString()!;
                if (!shopTargets[category.Name].Contains(id))
                {
                    missingShopReferences.Add($"{category.Name}:{id}");
                }
            }
        }
        Assert.Empty(missingShopReferences);

        List<string> invalidFusionOperands = [];
        foreach (JsonElement recipe in fusion.RootElement.GetProperty("recipes").EnumerateArray())
        {
            foreach (string propertyName in new[] { "parentA", "parentB", "result" })
            {
                string value = recipe.GetProperty(propertyName).GetString()!;
                if (value is not "1" and not "-1" && !races.Contains(value) && !entityIds.Contains(value))
                {
                    invalidFusionOperands.Add($"{propertyName}:{value}");
                }
            }
        }
        Assert.Empty(invalidFusionOperands);
    }

    private static JsonDocument Load(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(LegacyBaselineSupport.JsonPath(fileName)));

    private static IEnumerable<string> SkillNames(JsonDocument skills) =>
        skills.RootElement.EnumerateObject()
            .SelectMany(category => category.Value.EnumerateArray())
            .Select(skill => skill.GetProperty("Skill").GetString()!);

    private static HashSet<string> IdSet(string fileName, string propertyName)
    {
        using JsonDocument document = Load(fileName);
        return new HashSet<string>(
            document.RootElement.GetProperty(propertyName)
                .EnumerateArray()
                .Select(record => record.GetProperty("id").GetString()!),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertCount(int expected, IReadOnlyCollection<string> actual, string description) =>
        Assert.True(
            actual.Count == expected,
            $"Expected {expected} {description}, found {actual.Count}:{Environment.NewLine}{string.Join(Environment.NewLine, actual)}");
}
