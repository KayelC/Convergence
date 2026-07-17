using System.Text.Json;
using System.Text.RegularExpressions;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class DocumentationContractSynchronizationTests
{
    [Fact]
    public void CurrentAuthorityDocuments_UseCompiledRuntimeSaveContractVersion()
    {
        int expectedVersion = RuntimeSaveGameSnapshot.CurrentContractVersion;

        AssertSingleCurrentVersionClaim(
            File.ReadAllText(RepositoryPath("docs", "architecture.md")),
            @"runtime save contract version `(?<version>\d+)`",
            expectedVersion,
            "architecture");
        AssertSingleCurrentVersionClaim(
            File.ReadAllText(RepositoryPath("docs", "terminology-boundary.md")),
            @"runtime snapshots use save contract version `(?<version>\d+)`",
            expectedVersion,
            "terminology boundary");
        AssertSingleCurrentVersionClaim(
            File.ReadAllText(RepositoryPath("docs", "mechanics", "saving-loading-and-suspend.md")),
            @"current runtime save contract is version `(?<version>\d+)`",
            expectedVersion,
            "save mechanics");

        using JsonDocument matrix = JsonDocument.Parse(File.ReadAllText(RepositoryPath(
            "tests",
            "Convergence.Framework.Tests",
            "Fixtures",
            "documentation-coverage-matrix.json")));
        JsonElement persistence = matrix.RootElement
            .GetProperty("capabilities")
            .EnumerateArray()
            .Single(capability => capability.GetProperty("id").GetString() == "persistence_snapshots");
        string reason = persistence
            .GetProperty("developerGuide")
            .GetProperty("reason")
            .GetString()
            ?? string.Empty;

        Assert.Contains($"save v{expectedVersion} actor state", reason, StringComparison.Ordinal);
    }

    private static void AssertSingleCurrentVersionClaim(
        string document,
        string pattern,
        int expectedVersion,
        string documentName)
    {
        MatchCollection matches = Regex.Matches(
            document,
            pattern,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        Match match = Assert.Single(matches);
        Assert.True(
            int.TryParse(match.Groups["version"].Value, out int documentedVersion),
            $"The {documentName} version claim was not numeric.");
        Assert.Equal(expectedVersion, documentedVersion);
    }

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
}
