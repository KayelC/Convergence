using System.Text.Json;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests.Architecture;

public sealed class FrameworkCompletionRoadmapTests
{
    [Fact]
    public void FrameworkStateDocument_RecordsCurrentOwnershipMapAndForwardPlan()
    {
        string roadmap = Read("docs", "framework-state-and-roadmap.md");

        Assert.Contains("# Framework State And Roadmap", roadmap, StringComparison.Ordinal);
        Assert.Contains("This document resets the map.", roadmap, StringComparison.Ordinal);
        Assert.Contains("The framework foundation exists, but it is not yet a finished framework product.", roadmap, StringComparison.Ordinal);
        Assert.Contains("The Training Annex demo is the first original clean runtime slice.", roadmap, StringComparison.Ordinal);
        Assert.Contains("0 archive candidates", roadmap, StringComparison.Ordinal);
        Assert.Contains("Do not casually invent new lettered tracks.", roadmap, StringComparison.Ordinal);
        Assert.Contains("No direct conversion of prototype `Data/Jsons` into production clean content.", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveDocumentationIndex_UsesFrameworkStateAsPrimaryAuthority()
    {
        string index = Read("docs", "README.md");

        Assert.Contains("[Framework State And Roadmap](framework-state-and-roadmap.md)", index, StringComparison.Ordinal);
        Assert.Contains("is the current project map and forward plan", index, StringComparison.Ordinal);
        Assert.Contains("[Framework Completion Problems](framework-completion/README.md)", index, StringComparison.Ordinal);
        Assert.DoesNotContain("[Track T Framework Completion Roadmap](t-track-plan.md)", index, StringComparison.Ordinal);
        Assert.DoesNotContain("[Framework Parity Migration Plan](framework-parity-migration-plan.md)", index, StringComparison.Ordinal);
    }

    [Fact]
    public void SupersededPlanningDocs_AreArchivedInsteadOfActive()
    {
        string[] archivedPlans =
        [
            "framework-parity-migration-plan.md",
            "o-track-plan.md",
            "production-baseline.md",
            "q-track-plan.md",
            "t-track-plan.md"
        ];

        foreach (string plan in archivedPlans)
        {
            Assert.False(File.Exists(Path.Combine(LegacyBaselineSupport.RepositoryRoot, "docs", plan)), $"{plan} should no longer be active.");
            Assert.True(File.Exists(Path.Combine(LegacyBaselineSupport.RepositoryRoot, "ArchiveDocs", "Planning", plan)), $"{plan} should be archived.");
        }
    }

    [Fact]
    public void LegacyFrameworkArchive_RemainsPolicyOnlyUntilRemovalIsAuthorized()
    {
        string archiveRoot = Path.Combine(LegacyBaselineSupport.RepositoryRoot, "ArchiveDocs", "LegacyFramework");
        string[] files = Directory.GetFiles(archiveRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(archiveRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["README.md"], files);
    }

    [Fact]
    public void RecoveryLedger_DoesNotAuthorizeLegacyRemovalBeforeCleanParity()
    {
        using JsonDocument document = JsonDocument.Parse(Read("Convergence.Tests", "Fixtures", "Parity", "recovery-baseline.json"));

        foreach (JsonElement capability in document.RootElement.GetProperty("capabilities").EnumerateArray())
        {
            string id = capability.GetProperty("id").GetString()
                ?? throw new InvalidDataException("Capability id is required.");
            Assert.False(capability.GetProperty("removalAuthorized").GetBoolean(), $"{id} unexpectedly authorized removal.");
            Assert.NotEqual("clean_parity", capability.GetProperty("status").GetString());
        }
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([LegacyBaselineSupport.RepositoryRoot, .. parts]));
}
