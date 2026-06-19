using System.Text.Json;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests.Parity;

public sealed class ArchiveCandidateReviewTests
{
    [Fact]
    public void TrackT5Review_CoversEveryRecoveryCapability()
    {
        using JsonDocument review = Load("archive-candidate-review.t5.json");
        using JsonDocument recovery = Load("recovery-baseline.json");

        JsonElement reviewRoot = review.RootElement;
        Assert.Equal(1, reviewRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("T5", RequiredString(reviewRoot, "track"));
        Assert.Equal("track-12-recovery", RequiredString(reviewRoot, "branch"));
        Assert.Equal("bbede42", RequiredString(reviewRoot, "startingCommit"));
        Assert.Equal("no_archive_candidates", RequiredString(reviewRoot, "decision"));

        string[] reviewedIds = reviewRoot.GetProperty("reviewedCapabilityIds")
            .EnumerateArray()
            .Select(RequiredString)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] recoveryIds = recovery.RootElement.GetProperty("capabilities")
            .EnumerateArray()
            .Select(capability => RequiredString(capability, "id"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(36, reviewRoot.GetProperty("reviewedCapabilityCount").GetInt32());
        Assert.Equal(recoveryIds, reviewedIds);
        Assert.Equal(reviewedIds.Length, reviewedIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TrackT5Review_MatchesCurrentLedgerStatusSummary()
    {
        using JsonDocument review = Load("archive-candidate-review.t5.json");
        using JsonDocument recovery = Load("recovery-baseline.json");

        Dictionary<string, int> actualStatusCounts = recovery.RootElement.GetProperty("capabilities")
            .EnumerateArray()
            .GroupBy(capability => RequiredString(capability, "status"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        JsonElement summary = review.RootElement.GetProperty("statusSummary");

        Assert.Equal(2, summary.GetProperty("clean_foundation").GetInt32());
        Assert.Equal(33, summary.GetProperty("parallel_partial").GetInt32());
        Assert.Equal(1, summary.GetProperty("legacy_only").GetInt32());
        Assert.Equal(0, summary.GetProperty("clean_parity").GetInt32());
        foreach (JsonProperty property in summary.EnumerateObject())
        {
            actualStatusCounts.TryGetValue(property.Name, out int actualCount);
            Assert.Equal(property.Value.GetInt32(), actualCount);
        }

        int authorizedRemovals = recovery.RootElement.GetProperty("capabilities")
            .EnumerateArray()
            .Count(capability => capability.GetProperty("removalAuthorized").GetBoolean());
        int cleanParity = recovery.RootElement.GetProperty("capabilities")
            .EnumerateArray()
            .Count(capability => RequiredString(capability, "status") == "clean_parity");

        Assert.Equal(0, cleanParity);
        Assert.Equal(0, authorizedRemovals);
        Assert.Equal(cleanParity, review.RootElement.GetProperty("archiveCandidateCount").GetInt32());
        Assert.Equal(authorizedRemovals, review.RootElement.GetProperty("removalAuthorizationCount").GetInt32());
        Assert.Empty(review.RootElement.GetProperty("archiveActions").EnumerateArray());
    }

    [Fact]
    public void ArchiveEligibility_RequiresCleanParityMigratedConsumerAndRemovalAuthorization()
    {
        using JsonDocument recovery = Load("recovery-baseline.json");

        foreach (JsonElement capability in recovery.RootElement.GetProperty("capabilities").EnumerateArray())
        {
            string id = RequiredString(capability, "id");
            string status = RequiredString(capability, "status");
            bool consumerMigrated = capability.GetProperty("consumerMigrated").GetBoolean();
            bool removalAuthorized = capability.GetProperty("removalAuthorized").GetBoolean();
            bool eligible = status == "clean_parity" && consumerMigrated && removalAuthorized;

            Assert.False(eligible, $"{id} unexpectedly became an archive candidate.");
            if (removalAuthorized)
            {
                Assert.Equal("clean_parity", status);
                Assert.True(consumerMigrated, $"{id} authorized removal without consumer migration.");
            }
        }
    }

    [Fact]
    public void LegacyFrameworkArchive_RemainsPolicyOnly()
    {
        string archiveRoot = Path.Combine(LegacyBaselineSupport.RepositoryRoot, "ArchiveDocs", "LegacyFramework");
        string[] files = Directory.GetFiles(archiveRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(archiveRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["README.md"], files);
        Assert.DoesNotContain(files, path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrainingAnnexAndCleanDemos_AreNotRecordedAsLegacyArchiveActions()
    {
        using JsonDocument review = Load("archive-candidate-review.t5.json");

        string[] notes = review.RootElement.GetProperty("decisionNotes")
            .EnumerateArray()
            .Select(RequiredString)
            .ToArray();

        Assert.Contains("Clean demos and the Training Annex runtime slice are original-content proofs, not migrated legacy consumers.", notes);
        Assert.Empty(review.RootElement.GetProperty("archiveActions").EnumerateArray());
    }

    private static JsonDocument Load(string fileName)
    {
        string path = Path.Combine(
            LegacyBaselineSupport.RepositoryRoot,
            "Convergence.Tests",
            "Fixtures",
            "Parity",
            fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string RequiredString(JsonElement element) =>
        element.GetString() ?? throw new InvalidDataException("Expected a non-null string.");

    private static string RequiredString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString()
        ?? throw new InvalidDataException($"'{propertyName}' must be a string.");
}
