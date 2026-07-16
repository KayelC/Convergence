using System.Text.Json;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class FrameworkCapabilityMatrixTests
{
    private static readonly HashSet<string> States = ["complete", "partial", "deferred"];
    private static readonly HashSet<string> CoverageLevels = ["none", "focused", "end_to_end"];

    [Fact]
    public void Matrix_UsesCleanProductStatesAndAuditableEvidence()
    {
        CapabilityMatrix matrix = Load();

        Assert.Equal(1, matrix.SchemaVersion);
        Assert.Equal("Convergence.Framework", matrix.Product);
        Assert.Equal(States.Order(), matrix.States.Order());
        Assert.Equal(CoverageLevels.Order(), matrix.DemoCoverageLevels.Order());
        Assert.NotEmpty(matrix.Capabilities);
        Assert.Equal(
            matrix.Capabilities.Count,
            matrix.Capabilities.Select(capability => capability.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (CapabilityEntry capability in matrix.Capabilities)
        {
            Assert.Matches("^[a-z0-9_]+$", capability.Id);
            Assert.Contains(capability.ImplementationState, States);
            Assert.Contains(capability.DemoCoverage, CoverageLevels);
            Assert.True(capability.HostNeutral, $"Capability '{capability.Id}' is not host-neutral.");
            Assert.NotEmpty(capability.FrameworkTests);
            Assert.DoesNotContain(capability.FrameworkTests, value => string.IsNullOrWhiteSpace(value));

            if (capability.ImplementationState == "complete")
            {
                Assert.Empty(capability.KnownGaps);
            }
            else
            {
                Assert.NotEmpty(capability.KnownGaps);
            }
        }
    }

    [Fact]
    public void Matrix_DoesNotCarryLegacyParityOrRemovalAuthorityFields()
    {
        string json = File.ReadAllText(MatrixPath());

        Assert.DoesNotContain("parallel_partial", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("legacy_only", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clean_parity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("removalAuthorized", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("consumerMigrated", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActiveDocuments_ReportTotalsDerivedFromTheExecutableMatrix()
    {
        CapabilityMatrix matrix = Load();
        int complete = matrix.Capabilities.Count(capability => capability.ImplementationState == "complete");
        int partial = matrix.Capabilities.Count(capability => capability.ImplementationState == "partial");
        int deferred = matrix.Capabilities.Count(capability => capability.ImplementationState == "deferred");
        string expected =
            $"The matrix currently records {matrix.Capabilities.Count} capabilities: " +
            $"{complete} complete, {partial} partial, and {deferred} deferred.";

        Assert.Contains(
            expected,
            File.ReadAllText(RepositoryPath("docs", "framework-capability-matrix.md")),
            StringComparison.Ordinal);
        Assert.Contains(
            expected,
            File.ReadAllText(RepositoryPath("docs", "roadmap.md")),
            StringComparison.Ordinal);
    }

    private static CapabilityMatrix Load() =>
        JsonSerializer.Deserialize<CapabilityMatrix>(
            File.ReadAllText(MatrixPath()),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Capability matrix did not deserialize.");

    private static string MatrixPath() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "framework-capability-matrix.json");

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

    private sealed record CapabilityMatrix(
        int SchemaVersion,
        string Product,
        IReadOnlyList<string> States,
        IReadOnlyList<string> DemoCoverageLevels,
        IReadOnlyList<CapabilityEntry> Capabilities);

    private sealed record CapabilityEntry(
        string Id,
        string ImplementationState,
        IReadOnlyList<string> FrameworkTests,
        string DemoCoverage,
        bool HostNeutral,
        bool OptionalModule,
        IReadOnlyList<string> KnownGaps);
}
