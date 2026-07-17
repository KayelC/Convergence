using System.Text.Json;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class DocumentationFoundationTests
{
    private static readonly HashSet<string> States =
        ["reviewed", "existing_unreviewed", "missing", "not_applicable"];

    [Fact]
    public void CoverageMatrix_ExactlyCoversFrameworkCapabilitiesAndValidDocuments()
    {
        DocumentationMatrix documentation = LoadDocumentationMatrix();
        FrameworkCapabilityMatrix framework = LoadFrameworkCapabilityMatrix();

        Assert.Equal(1, documentation.SchemaVersion);
        Assert.Equal("Convergence.Framework", documentation.Product);
        Assert.Equal("docs/documentation-design-pattern.md", documentation.Authority);
        Assert.Equal(States.Order(StringComparer.Ordinal), documentation.States.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["developer_guide", "mechanics", "technical"],
            documentation.Audiences.Order(StringComparer.Ordinal));
        Assert.Equal(
            framework.Capabilities.Select(capability => capability.Id).Order(StringComparer.Ordinal),
            documentation.Capabilities.Select(capability => capability.Id).Order(StringComparer.Ordinal));
        Assert.Equal(
            documentation.Capabilities.Count,
            documentation.Capabilities.Select(capability => capability.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (DocumentationCapability capability in documentation.Capabilities)
        {
            Assert.Matches("^[a-z0-9_]+$", capability.Id);
            ValidateCoverage(capability.Id, "mechanics", capability.Mechanics);
            ValidateCoverage(capability.Id, "developer_guide", capability.DeveloperGuide);
            ValidateCoverage(capability.Id, "technical", capability.Technical);
        }
    }

    [Fact]
    public void CoverageReference_ReportsTotalsDerivedFromTheExecutableMatrix()
    {
        DocumentationMatrix matrix = LoadDocumentationMatrix();
        AudienceCoverage[] entries = matrix.Capabilities
            .SelectMany(capability => new[]
            {
                capability.Mechanics,
                capability.DeveloperGuide,
                capability.Technical
            })
            .ToArray();
        string expected =
            $"The documentation matrix currently records {entries.Length} audience entries: " +
            $"{entries.Count(entry => entry.State == "reviewed")} reviewed, " +
            $"{entries.Count(entry => entry.State == "existing_unreviewed")} existing_unreviewed, " +
            $"{entries.Count(entry => entry.State == "missing")} missing, and " +
            $"{entries.Count(entry => entry.State == "not_applicable")} not_applicable.";

        Assert.Contains(
            expected,
            File.ReadAllText(RepositoryPath("docs", "reference", "documentation-coverage.md"))
                .ReplaceLineEndings(" "),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationPatternsAndAgentGuide_PreserveCollaborativeDesignAuthority()
    {
        string pattern = File.ReadAllText(RepositoryPath("docs", "documentation-design-pattern.md"));
        string policyPattern = File.ReadAllText(RepositoryPath("docs", "policy-family-design-pattern.md"));
        string agents = File.ReadAllText(RepositoryPath("AGENTS.md"));

        string[] patternTokens =
        [
            "## Documentation Audiences",
            "### Mechanics",
            "### Developer Guide",
            "### Technical",
            "## Collaborative Workflow",
            "```mermaid",
            "Project owner confirms or corrects intended design",
            "## Coverage States",
            "## Definition Of Documented"
        ];
        Assert.All(patternTokens, token => Assert.Contains(token, pattern, StringComparison.Ordinal));

        string[] policyPatternTokens =
        [
            "## Required Principles",
            "### One Authority Per Scope",
            "### Immutable Decisions Before Mutation",
            "### State Must Represent Every Supplied Policy",
            "## Development Sequence",
            "## Required Conformance Tests",
            "## Definition Of Complete"
        ];
        Assert.All(
            policyPatternTokens,
            token => Assert.Contains(token, policyPattern, StringComparison.Ordinal));

        string[] agentTokens =
        [
            "docs/documentation-design-pattern.md",
            "docs/policy-family-design-pattern.md",
            "Confirmed mechanics and decision records define intended design.",
            "Do not infer an unclear rule",
            "explicit project-owner confirmation",
            "docs/developer-guide",
            "docs/technical",
            "docs/reviews",
            "docs/roadmap"
        ];
        Assert.All(agentTokens, token => Assert.Contains(token, agents, StringComparison.Ordinal));
    }

    [Fact]
    public void StatModifierDecision_PreservesConfirmedRollingDurationExample()
    {
        string decision = File.ReadAllText(
            RepositoryPath("docs", "decisions", "stat-modifier-policy-family.md"));
        string roadmap = File.ReadAllText(
            RepositoryPath("docs", "roadmap", "stat-modifier-policy-roadmap.md"));

        string[] decisionTokens =
        [
            "## Confirmed Rolling-Duration Example",
            "| 1 | first contribution: 3 turns remaining | `+1` |",
            "| 2 | first: 2; second: 3 | `+2` |",
            "| 3 | first: 1; second: 2; third: 3 | `+3` |",
            "| 4 | first expires; second: 1; third: 2; fourth: 3 | `+3` |",
            "The fourth application does not produce `+4`",
            "Stage `+4` remains reachable"
        ];
        Assert.All(
            decisionTokens,
            token => Assert.Contains(token, decision, StringComparison.Ordinal));

        Assert.Contains("`+1`, `+2`, `+3`, `+3`", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "`[3]`, `[2, 3]`, `[1, 2, 3]`, `[1, 2, 3]`",
            roadmap,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AudienceEvidenceAndRoadmapDirectories_AreIndexedAndDeclutterTheDocsRoot()
    {
        string docsRoot = RepositoryPath("docs");
        string[] requiredDirectories =
        [
            "mechanics",
            "developer-guide",
            "technical",
            "decisions",
            "reference",
            "reviews",
            "roadmap"
        ];
        Assert.All(requiredDirectories, directory =>
            Assert.True(File.Exists(Path.Combine(docsRoot, directory, "README.md"))));

        AssertIndexedDirectory(Path.Combine(docsRoot, "reviews"));
        AssertIndexedDirectory(Path.Combine(docsRoot, "roadmap"));

        string[] rootFiles = Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToArray()!;
        Assert.DoesNotContain(rootFiles, file => file.Contains("review", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("roadmap.md", rootFiles);
        Assert.DoesNotContain("production-readiness-roadmap.md", rootFiles);
        Assert.DoesNotContain("framework-capability-matrix.md", rootFiles);
    }

    private static void ValidateCoverage(
        string capabilityId,
        string audience,
        AudienceCoverage coverage)
    {
        Assert.Contains(coverage.State, States);
        Assert.False(
            string.IsNullOrWhiteSpace(coverage.Reason),
            $"Capability '{capabilityId}' audience '{audience}' requires a reason.");

        if (coverage.State is "reviewed" or "existing_unreviewed")
        {
            Assert.NotEmpty(coverage.Documents);
            foreach (string document in coverage.Documents)
            {
                Assert.StartsWith("docs/", document, StringComparison.Ordinal);
                Assert.True(
                    File.Exists(RepositoryPath(document.Split('/'))),
                    $"Capability '{capabilityId}' audience '{audience}' references missing '{document}'.");
            }
        }
        else
        {
            Assert.Empty(coverage.Documents);
        }
    }

    private static void AssertIndexedDirectory(string directory)
    {
        string index = File.ReadAllText(Path.Combine(directory, "README.md"));
        foreach (string document in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
                     .Where(path => !path.EndsWith("README.md", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.Contains(Path.GetFileName(document), index, StringComparison.Ordinal);
        }
    }

    private static DocumentationMatrix LoadDocumentationMatrix() =>
        JsonSerializer.Deserialize<DocumentationMatrix>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "documentation-coverage-matrix.json")),
            JsonOptions())
        ?? throw new InvalidOperationException("Documentation coverage matrix did not deserialize.");

    private static FrameworkCapabilityMatrix LoadFrameworkCapabilityMatrix() =>
        JsonSerializer.Deserialize<FrameworkCapabilityMatrix>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "framework-capability-matrix.json")),
            JsonOptions())
        ?? throw new InvalidOperationException("Framework capability matrix did not deserialize.");

    private static JsonSerializerOptions JsonOptions() =>
        new() { PropertyNameCaseInsensitive = true };

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

    private sealed record DocumentationMatrix(
        int SchemaVersion,
        string Product,
        string Authority,
        IReadOnlyList<string> States,
        IReadOnlyList<string> Audiences,
        IReadOnlyList<DocumentationCapability> Capabilities);

    private sealed record DocumentationCapability(
        string Id,
        AudienceCoverage Mechanics,
        AudienceCoverage DeveloperGuide,
        AudienceCoverage Technical);

    private sealed record AudienceCoverage(
        string State,
        IReadOnlyList<string> Documents,
        string Reason);

    private sealed record FrameworkCapabilityMatrix(
        IReadOnlyList<FrameworkCapability> Capabilities);

    private sealed record FrameworkCapability(string Id);
}
