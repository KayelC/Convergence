using System.Text.Json;
using Convergence.Tests.TestSupport;
using Xunit;

namespace Convergence.Tests.Architecture;

public sealed class FrameworkCompletionRoadmapTests
{
    [Fact]
    public void TrackTRoadmap_RecordsBuildForwardArchiveLaterPolicy()
    {
        string roadmap = Read("docs", "t-track-plan.md");

        Assert.Contains("# Track T Framework Completion Roadmap", roadmap, StringComparison.Ordinal);
        Assert.Contains("Track T1: Framework Completion Audit", roadmap, StringComparison.Ordinal);
        Assert.Contains("Track T2: Authored Ruleset Binding", roadmap, StringComparison.Ordinal);
        Assert.Contains("Track T3: Original Clean Content Vertical Slice", roadmap, StringComparison.Ordinal);
        Assert.Contains("Track T4: Clean Runtime Consumer Slice", roadmap, StringComparison.Ordinal);
        Assert.Contains("Track T5: Archive Candidate Review", roadmap, StringComparison.Ordinal);
        Assert.Contains("The framework architecture is ready for continued production work, but the framework is not finished.", roadmap, StringComparison.Ordinal);
        Assert.Contains("Legacy `Data/Jsons` is prototype-only evidence", roadmap, StringComparison.Ordinal);
        Assert.Contains("Every protected legacy capability remains `removalAuthorized: false`.", roadmap, StringComparison.Ordinal);
        Assert.Contains("ArchiveDocs/LegacyFramework", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveDocumentationIndex_LinksTrackTRoadmap()
    {
        string index = Read("docs", "README.md");

        Assert.Contains("[Track T Framework Completion Roadmap](t-track-plan.md)", index, StringComparison.Ordinal);
        Assert.Contains("defines the active build-forward lane after the archive gate", index, StringComparison.Ordinal);
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
