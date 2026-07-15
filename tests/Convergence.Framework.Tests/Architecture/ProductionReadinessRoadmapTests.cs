using System.Text.Json;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class ProductionReadinessRoadmapTests
{
    private static readonly string[] OriginalAuditFindingIds =
    [
        "PR-B1", "PR-B2", "PR-B3", "PR-B4", "PR-B5",
        "PR-H1", "PR-H2",
        "PR-M1", "PR-M2", "PR-M3", "PR-M4", "PR-M5", "PR-M6",
        "PR-L1", "PR-L2"
    ];

    [Fact]
    public void Ledger_CoversEveryOriginalAuditFindingAndTheCatalystCorrection()
    {
        ProductionReadinessLedger ledger = Load();

        Assert.Equal(1, ledger.SchemaVersion);
        Assert.Equal("Convergence.Framework", ledger.Product);
        Assert.Equal("main", ledger.Branch);
        Assert.Equal("8db20fe", ledger.StartingCommit);
        Assert.Equal(ledger.Findings.Count, ledger.Findings.Select(finding => finding.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            OriginalAuditFindingIds.Order(StringComparer.Ordinal),
            ledger.Findings
                .Where(finding => finding.Id.StartsWith("PR-", StringComparison.Ordinal))
                .Select(finding => finding.Id)
                .Order(StringComparer.Ordinal));
        Assert.Contains(ledger.Findings, finding => finding.Id == "CR-M6");
    }

    [Fact]
    public void Ledger_RequiresEvidenceWorkOwnershipAndHonestArchiveState()
    {
        ProductionReadinessLedger ledger = Load();
        HashSet<string> allowedStatuses = ledger.AllowedStatuses.ToHashSet(StringComparer.Ordinal);

        Assert.Equal(["implemented_pending_review", "open", "verified"], allowedStatuses.Order(StringComparer.Ordinal));
        foreach (ProductionReadinessFinding finding in ledger.Findings)
        {
            Assert.Contains(finding.Status, allowedStatuses);
            Assert.False(string.IsNullOrWhiteSpace(finding.Title));
            Assert.False(string.IsNullOrWhiteSpace(finding.Severity));
            Assert.True(finding.PlannedCheckpoint >= 0);
            Assert.False(string.IsNullOrWhiteSpace(finding.PlannedCommit));
            Assert.NotEmpty(finding.Evidence);
            Assert.All(finding.Evidence, item => Assert.False(string.IsNullOrWhiteSpace(item)));
            Assert.False(finding.ArchiveEligible);

            if (finding.Status == "verified")
            {
                Assert.Empty(finding.RemainingWork);
            }
            else
            {
                Assert.NotEmpty(finding.RemainingWork);
            }
        }
    }

    [Fact]
    public void ActiveDocumentation_KeepsUnfinishedRoadmapAuthoritative()
    {
        string roadmap = File.ReadAllText(RepositoryPath("docs", "production-readiness-roadmap.md"));
        string index = File.ReadAllText(RepositoryPath("docs", "README.md"));
        string productRoadmap = File.ReadAllText(RepositoryPath("docs", "roadmap.md"));

        Assert.Contains("This roadmap remains active", roadmap, StringComparison.Ordinal);
        Assert.Contains("production-readiness-roadmap.md", index, StringComparison.Ordinal);
        Assert.Contains("production-readiness-roadmap.md", productRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Completed restructuring, migration, and recovery plans are preserved",
            index,
            StringComparison.Ordinal);
    }

    private static ProductionReadinessLedger Load() =>
        JsonSerializer.Deserialize<ProductionReadinessLedger>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "production-readiness-roadmap.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Production-readiness roadmap fixture could not be loaded.");

    private static string RepositoryPath(params string[] segments) =>
        Path.Combine([RepositoryRoot(), .. segments]);

    private static string RepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Convergence.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        Assert.NotNull(current);
        return current!;
    }

    private sealed record ProductionReadinessLedger(
        int SchemaVersion,
        string Product,
        string Branch,
        string StartingCommit,
        IReadOnlyList<string> AllowedStatuses,
        IReadOnlyList<ProductionReadinessFinding> Findings);

    private sealed record ProductionReadinessFinding(
        string Id,
        string Title,
        string Severity,
        string Status,
        int PlannedCheckpoint,
        string PlannedCommit,
        IReadOnlyList<string> Evidence,
        IReadOnlyList<string> RemainingWork,
        bool ArchiveEligible);
}
