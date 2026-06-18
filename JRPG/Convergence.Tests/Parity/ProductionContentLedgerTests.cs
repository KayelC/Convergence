using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests.Parity;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class ProductionContentLedgerTests
{
    private static readonly string[] ExpectedLegacyFiles =
    [
        "Data/Jsons/accessories.json",
        "Data/Jsons/armor.json",
        "Data/Jsons/boots.json",
        "Data/Jsons/entity_database.json",
        "Data/Jsons/fusion_table.json",
        "Data/Jsons/items.json",
        "Data/Jsons/questions.json",
        "Data/Jsons/shop_inventory.json",
        "Data/Jsons/skills_database.json",
        "Data/Jsons/status_ailments.json",
        "Data/Jsons/tartarus.json",
        "Data/Jsons/weapons.json"
    ];

    private static readonly string[] ExpectedCleanSchemaFamilies =
    [
        "ailments",
        "dungeons",
        "encounters",
        "entities",
        "equipment",
        "fusionRecipes",
        "items",
        "negotiations",
        "races",
        "rulesets",
        "shops",
        "skills"
    ];

    private static readonly string[] ExpectedMandatoryReports =
    [
        "behavior_decisions",
        "conflicts",
        "id_mapping",
        "omitted_records",
        "record_counts",
        "runtime_coverage",
        "unresolved_references"
    ];

    private static readonly string[] ExpectedManualDecisionBuckets =
    [
        "demo_vs_production_content",
        "navigator_support_skills",
        "physical_affinity_conflicts",
        "special_registered_handlers"
    ];

    private static readonly string[] ExpectedHistoricalEvidence =
    [
        "ArchiveDocs/Planning/migration_report.md",
        "Data/Jsons/entity_database_v2.json",
        "Data/Jsons/skills_database_v2.json"
    ];

    [Fact]
    public void ProductionLedger_CoversProtectedLegacyFilesAndCleanSchemaFamilies()
    {
        using JsonDocument document = LoadLedger();
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Q1", RequiredString(root, "track"));
        Assert.Equal("track-12-recovery", RequiredString(root, "branch"));
        Assert.True(root.GetProperty("productionJsonUnchanged").GetBoolean());
        Assert.False(root.GetProperty("consumerSwitchAuthorized").GetBoolean());
        Assert.False(root.GetProperty("legacyRemovalAuthorized").GetBoolean());

        JsonElement[] families = Families(root);
        string[] legacyFiles = families
            .SelectMany(family => RequiredStrings(family, "legacyFiles"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedLegacyFiles, legacyFiles);
        Assert.Equal(legacyFiles.Length, legacyFiles.Distinct(StringComparer.Ordinal).Count());

        string[] cleanFamilies = families
            .SelectMany(family => RequiredStrings(family, "cleanSchemaFamilies"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedCleanSchemaFamilies, cleanFamilies);

        foreach (JsonElement family in families)
        {
            string id = RequiredString(family, "id");
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(family, "name")));
            Assert.NotEmpty(RequiredStrings(family, "legacyOwners"));
            Assert.NotEmpty(RequiredStrings(family, "cleanSchemaFamilies"));
            Assert.NotEmpty(RequiredString(family, "idStrategy"));
            Assert.NotEmpty(RequiredStrings(family, "consumerTargets"));
            Assert.NotEmpty(RequiredStrings(family, "tests"));

            string futureTrack = RequiredString(family, "futureTrack");
            Assert.True(
                futureTrack.Length == 2 && futureTrack[0] == 'Q' && futureTrack[1] is >= '2' and <= '7',
                $"Family '{id}' has invalid future Track Q owner '{futureTrack}'.");
        }
    }

    [Fact]
    public void ProductionLedger_DoesNotAuthorizeConversionConsumerSwitchOrRemovalInQ1()
    {
        using JsonDocument document = LoadLedger();

        foreach (JsonElement family in Families(document.RootElement))
        {
            string id = RequiredString(family, "id");
            Assert.NotEqual("clean_parity", RequiredString(family, "currentStatus"));
            Assert.False(family.GetProperty("productionConverted").GetBoolean(), $"{id} is marked converted in Q1.");
            Assert.False(family.GetProperty("consumerSwitched").GetBoolean(), $"{id} switched a consumer in Q1.");
            Assert.False(family.GetProperty("removalAuthorized").GetBoolean(), $"{id} authorized legacy removal in Q1.");
        }
    }

    [Fact]
    public void ProductionLedger_RecordsMandatoryReportsAndManualDecisionOwners()
    {
        using JsonDocument document = LoadLedger();
        JsonElement root = document.RootElement;

        string[] rootReports = RequiredStrings(root, "mandatoryReports")
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedMandatoryReports, rootReports);

        foreach (JsonElement family in Families(root))
        {
            string familyId = RequiredString(family, "id");
            JsonElement[] reports = family.GetProperty("reports").EnumerateArray().ToArray();
            string[] reportIds = reports
                .Select(report => RequiredString(report, "id"))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(ExpectedMandatoryReports, reportIds);

            foreach (JsonElement report in reports)
            {
                Assert.False(string.IsNullOrWhiteSpace(RequiredString(report, "status")));
                string ownerTrack = RequiredString(report, "ownerTrack");
                Assert.True(
                    ownerTrack == "Q1" ||
                    (ownerTrack.Length == 2 && ownerTrack[0] == 'Q' && ownerTrack[1] is >= '2' and <= '7'),
                    $"Family '{familyId}' report '{RequiredString(report, "id")}' has invalid owner track '{ownerTrack}'.");
            }
        }

        JsonElement[] decisions = root.GetProperty("manualDecisionBuckets").EnumerateArray().ToArray();
        string[] decisionIds = decisions
            .Select(decision => RequiredString(decision, "id"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedManualDecisionBuckets, decisionIds);

        foreach (JsonElement decision in decisions)
        {
            Assert.False(decision.GetProperty("authoritativeDefault").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(decision, "decisionOwner")));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(decision, "blockingReason")));
        }
    }

    [Fact]
    public void ProductionLedger_RecordsDatasetCountsAndKnownIntegrityFindings()
    {
        using JsonDocument document = LoadLedger();
        JsonElement root = document.RootElement;

        Assert.Equal(420, Count(root, "skills", "skills"));
        Assert.Equal(3, Count(root, "skills", "duplicateSkillNameGroups"));
        Assert.Equal(304, Count(root, "entities_and_races", "entities"));
        Assert.Equal(0, Count(root, "entities_and_races", "duplicateEntityIdGroups"));
        Assert.Equal(11, Count(root, "ailments", "ailments"));
        Assert.Equal(14, Count(root, "items", "items"));
        Assert.Equal(26, Count(root, "equipment", "weapons"));
        Assert.Equal(3, Count(root, "equipment", "armor"));
        Assert.Equal(3, Count(root, "equipment", "boots"));
        Assert.Equal(3, Count(root, "equipment", "accessories"));
        Assert.Equal(35, Count(root, "equipment", "totalEquipment"));
        Assert.Equal(30, Count(root, "shops", "shopEntries"));
        Assert.Equal(8, Count(root, "negotiations", "negotiationPersonalities"));
        Assert.Equal(40, Count(root, "negotiations", "negotiationQuestions"));
        Assert.Equal(8, Count(root, "negotiations", "familiarDialogueSets"));
        Assert.Equal(460, Count(root, "fusion_recipes", "fusionRecipes"));
        Assert.Equal(1, Count(root, "dungeons_and_encounters", "dungeons"));
        Assert.Equal(6, Count(root, "dungeons_and_encounters", "dungeonBlocks"));

        JsonElement findings = root.GetProperty("knownIntegrityFindings");
        Assert.Equal(56, findings.GetProperty("unresolvedBaseSkillReferences").GetInt32());
        Assert.Equal(120, findings.GetProperty("unresolvedLearnedSkillReferences").GetInt32());
        Assert.Equal(1, findings.GetProperty("caseMismatchedSkillReferences").GetInt32());
        Assert.Equal(1, findings.GetProperty("unresolvedDungeonPoolReferences").GetInt32());
        Assert.Equal(0, findings.GetProperty("unresolvedDungeonBossReferences").GetInt32());
        Assert.Equal(0, findings.GetProperty("unresolvedShopReferences").GetInt32());
        Assert.Equal(0, findings.GetProperty("invalidFusionOperands").GetInt32());
    }

    [Fact]
    public void ProductionLedger_TreatsOldMigrationArtifactsAsHistoricalEvidenceOnly()
    {
        using JsonDocument document = LoadLedger();
        JsonElement[] evidence = document.RootElement.GetProperty("historicalEvidence").EnumerateArray().ToArray();

        string[] paths = evidence
            .Select(entry => RequiredString(entry, "path"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedHistoricalEvidence, paths);

        foreach (JsonElement entry in evidence)
        {
            string path = RequiredString(entry, "path");
            Assert.Equal("historical_only", RequiredString(entry, "role"));
            Assert.False(entry.GetProperty("authoritative").GetBoolean(), $"{path} is marked authoritative.");
            Assert.True(File.Exists(Path.Combine(LegacyBaselineSupport.RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar))));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(entry, "note")));
        }
    }

    private static JsonDocument LoadLedger()
    {
        string path = Path.Combine(
            LegacyBaselineSupport.RepositoryRoot,
            "Convergence.Tests",
            "Fixtures",
            "ProductionContent",
            "production-content-ledger.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static JsonElement[] Families(JsonElement root) =>
        root.GetProperty("families").EnumerateArray().ToArray();

    private static int Count(JsonElement root, string familyId, string countName)
    {
        JsonElement family = Families(root).Single(value => RequiredString(value, "id") == familyId);
        return family.GetProperty("recordCounts").GetProperty(countName).GetInt32();
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
