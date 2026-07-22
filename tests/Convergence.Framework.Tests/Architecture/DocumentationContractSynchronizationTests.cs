using System.Text.Json;
using System.Text.RegularExpressions;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class DocumentationContractSynchronizationTests
{
    [Fact]
    public void ChargeGuidance_UsesParticipationReceiptsAndExplicitOptionalComposition()
    {
        string mechanics = File.ReadAllText(
            RepositoryPath("docs", "mechanics", "combat-defenses-and-turns.md"));
        string developer = File.ReadAllText(
            RepositoryPath("docs", "developer-guide", "combat-resolution-policies.md"));
        string technical = File.ReadAllText(
            RepositoryPath("docs", "technical", "combat-resolution-pipeline.md"));
        string contracts = File.ReadAllText(
            RepositoryPath("docs", "ruleset-policy-contracts.md"));

        Assert.Contains("later grant", mechanics, StringComparison.Ordinal);
        Assert.Contains("same-kind replacement", mechanics, StringComparison.Ordinal);
        Assert.Contains("DisabledChargePolicy", mechanics, StringComparison.Ordinal);
        Assert.Contains("EffectExecutionResult.ParticipatingCharge", developer, StringComparison.Ordinal);
        Assert.Contains("Record exact modifier receipt", technical, StringComparison.Ordinal);
        Assert.Contains("same runtime charge that participated", technical, StringComparison.Ordinal);
        Assert.DoesNotContain("Record each distinct damage category", technical, StringComparison.Ordinal);
        Assert.Contains("| `chargePolicy` | string | `split` |", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void EffectConfigurationGuidance_PreservesSharedPreflightBoundary()
    {
        string mechanics = File.ReadAllText(
            RepositoryPath("docs", "mechanics", "actions-targeting-and-effects.md"))
            .ReplaceLineEndings(" ");
        string developer = File.ReadAllText(
            RepositoryPath("docs", "developer-guide", "typed-actions-and-effects.md"))
            .ReplaceLineEndings(" ");
        string technical = File.ReadAllText(
            RepositoryPath("docs", "technical", "typed-action-and-effect-execution.md"))
            .ReplaceLineEndings(" ");

        Assert.Contains("before targets are selected", mechanics, StringComparison.Ordinal);
        Assert.Contains("consumes no turn", mechanics, StringComparison.Ordinal);
        Assert.Contains("share one recursive", developer, StringComparison.Ordinal);
        Assert.Contains("EffectConfigurationInvalid", developer, StringComparison.Ordinal);
        Assert.Contains("## Effect Composition Preflight", technical, StringComparison.Ordinal);
        Assert.Contains("Only a configuration-clean action may resolve targets", technical, StringComparison.Ordinal);
        Assert.Contains("EffectConfigurationValidator.cs", technical, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentContentGuidance_UsesActiveSchemaAndOrderTwoCorrectionChain()
    {
        int expectedVersion = Directory
            .EnumerateFiles(RepositoryPath("content"), "*.manifest.json", SearchOption.AllDirectories)
            .Select(path =>
            {
                using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(path));
                return manifest.RootElement.GetProperty("schemaVersion").GetInt32();
            })
            .Distinct()
            .Single();

        string statModifierGuide = File.ReadAllText(
            RepositoryPath("docs", "developer-guide", "stat-modifier-policies.md"));
        string productRoadmap = File.ReadAllText(
            RepositoryPath("docs", "roadmap", "product-roadmap.md"));
        string orderedEffectsDecision = File.ReadAllText(
            RepositoryPath("docs", "decisions", "ordered-secondary-effects.md"));

        AssertSingleCurrentVersionClaim(
            statModifierGuide,
            @"Schema-v(?<version>\d+) content selects",
            expectedVersion,
            "stat-modifier developer guide");
        AssertSingleCurrentVersionClaim(
            productRoadmap,
            @"Active contracts use[^.\r\n]*schema-v(?<version>\d+)",
            expectedVersion,
            "product-roadmap active vocabulary");
        AssertSingleCurrentVersionClaim(
            productRoadmap,
            @"strict Draft 2020-12 schema-v(?<version>\d+) set",
            expectedVersion,
            "product-roadmap release foundations");
        Assert.DoesNotContain(
            "**Implementation state:** complete through O2-R16",
            orderedEffectsDecision,
            StringComparison.Ordinal);
        Assert.Contains("O2-R30 through O2-R34", orderedEffectsDecision, StringComparison.Ordinal);
        Assert.Contains("O2-R30 through O2-R34", productRoadmap, StringComparison.Ordinal);
    }

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
