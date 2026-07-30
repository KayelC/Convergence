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
            "use state nodes only for values that the runtime actually retains",
            "avoid rejected-command self-transitions",
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
    public void StatModifierDocumentation_PreservesConfirmedSignalsAndClockEdges()
    {
        string decision = File.ReadAllText(
            RepositoryPath("docs", "decisions", "stat-modifier-policy-family.md"));
        string mechanics = File.ReadAllText(
            RepositoryPath("docs", "mechanics", "stat-modifier-policies.md"));
        string technical = File.ReadAllText(
            RepositoryPath("docs", "technical", "stat-modifier-policy-runtime.md"));

        string[] exclusiveTokens =
        [
            "| `-2` | `--` | strong negative change |",
            "| `+2` | `++` | strong positive change |",
            "| `++` | `+` | rejected | unchanged; reason is already in effect |",
            "| `++` | `-` | `+` | retain the stronger existing effect's remaining duration |",
            "| `+` | `--` | `-` | use the stronger incoming effect's full duration |",
            "must not consume a cost, item, action, or turn"
        ];
        Assert.All(exclusiveTokens, token => Assert.Contains(token, decision, StringComparison.Ordinal));

        string[] contributionTokens =
        [
            "refreshes the oldest retained contribution",
            "Positive and negative contributions otherwise coexist and net",
            "application such as `+2` creates one `+2` contribution"
        ];
        Assert.All(contributionTokens, token => Assert.Contains(token, decision, StringComparison.Ordinal));

        string[] clockTokens =
        [
            "A committed attack, skill, item, guard, pass, forced action, or skipped action",
            "Cancelling command selection before commitment does not.",
            "A bonus action that continues the current turn window does not advance",
            "The same turn completes -> modifier remains at 3",
            "SuspendWhileReserve"
        ];
        Assert.All(clockTokens, token =>
            Assert.True(
                mechanics.Contains(token, StringComparison.Ordinal) ||
                decision.Contains(token, StringComparison.Ordinal),
                $"Missing confirmed clock rule '{token}'."));

        Assert.Contains("event ID + positive monotonic boundary sequence", technical, StringComparison.Ordinal);
        Assert.Contains("Applied in this exact boundary", technical, StringComparison.Ordinal);
        Assert.Contains("Which sign survives?", technical, StringComparison.Ordinal);
        Assert.DoesNotContain("StrongPositive --> StrongPositive", technical, StringComparison.Ordinal);
    }

    [Fact]
    public void CombatResolutionDocumentation_PreservesConfirmedPolicyAndExecutionBoundaries()
    {
        string decision = File.ReadAllText(
            RepositoryPath("docs", "decisions", "combat-resolution-policy-family.md"));
        string mechanics = File.ReadAllText(
            RepositoryPath("docs", "mechanics", "combat-defenses-and-turns.md"));
        string developer = File.ReadAllText(
            RepositoryPath("docs", "developer-guide", "combat-resolution-policies.md"));
        string technical = File.ReadAllText(
            RepositoryPath("docs", "technical", "combat-resolution-pipeline.md"));
        string roadmap = File.ReadAllText(
            RepositoryPath("docs", "roadmap", "combat-resolution-order-2-roadmap.md"));

        string[] designTokens =
        [
            "Luck has no hidden combat-probability role",
            "DisabledChargePolicy",
            "SplitChargePolicy",
            "UnifiedChargePolicy",
            "40` resolves to `60`, `40`, `20`, and `0",
            "Null, Repel, and Absorb are elemental damage affinities, not",
            "mixed Critical and evasion: normal cost",
            "Passing remains owned by the Action Token economy itself"
        ];
        Assert.All(designTokens, token => Assert.Contains(token, decision, StringComparison.Ordinal));
        Assert.DoesNotContain("Null or Absorb channel defenses", decision, StringComparison.Ordinal);

        string[] mechanicsTokens =
        [
            "base damage = damage formula scalar * sqrt(power * effective attack / effective defense)",
            "accuracy score = authored accuracy + attacker Agility * 2",
            "Damage never reads Luck.",
            "Turn cost is decided once for the complete action, not once per hit."
        ];
        Assert.All(mechanicsTokens, token => Assert.Contains(token, mechanics, StringComparison.Ordinal));

        string[] integrationTokens =
        [
            "ICombatDamageExecutionPolicy",
            "ICombatInstantDefeatExecutionPolicy",
            "NextUnitDecimal()",
            "ChargePolicyRegistry.CreateStandard()",
            "EffectExecutionResult.ParticipatingCharge",
            "chargePolicy"
        ];
        Assert.All(integrationTokens, token => Assert.Contains(token, developer, StringComparison.Ordinal));

        string[] technicalTokens =
        [
            "## Composition Boundary",
            "## Damage Sequence",
            "## Charge State Machine",
            "## Atomicity And Failure",
            "```mermaid"
        ];
        Assert.All(technicalTokens, token => Assert.Contains(token, technical, StringComparison.Ordinal));
        Assert.Contains("collection of typed charge slots", technical, StringComparison.Ordinal);
        Assert.Contains("Record exact modifier receipt", technical, StringComparison.Ordinal);
        Assert.Contains("same runtime charge that participated", technical, StringComparison.Ordinal);
        Assert.Contains("Before = After", technical, StringComparison.Ordinal);
        Assert.DoesNotContain("Charged --> Charged", technical, StringComparison.Ordinal);
        Assert.DoesNotContain("Record each distinct damage category", technical, StringComparison.Ordinal);

        Assert.Contains("| O2-C7 | `verified`", roadmap, StringComparison.Ordinal);
        Assert.Contains("revision `e26bdc5`", roadmap, StringComparison.Ordinal);
        Assert.Contains("O2-R24 through O2-R29", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "combat-resolution-order-2-pre-closure-audit-corrections-roadmap.md",
            roadmap,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StatusLifecycleDocumentation_PreservesDispatchAndRestoreAuthority()
    {
        string mechanics = File.ReadAllText(
            RepositoryPath("docs", "mechanics", "status-passive-lifecycle.md"))
            .ReplaceLineEndings(" ");
        string developer = File.ReadAllText(
            RepositoryPath("docs", "developer-guide", "status-passive-lifecycle.md"))
            .ReplaceLineEndings(" ");
        string technical = File.ReadAllText(
            RepositoryPath("docs", "technical", "status-passive-lifecycle.md"))
            .ReplaceLineEndings(" ");

        Assert.Contains("target set is fixed when dispatch begins", mechanics, StringComparison.Ordinal);
        Assert.Contains("does not retroactively remove or add", mechanics, StringComparison.Ordinal);
        Assert.Contains("only when that event is absent", developer, StringComparison.Ordinal);
        Assert.Contains("MissingPassiveSkillState", developer, StringComparison.Ordinal);
        Assert.Contains("ConflictingActorAilmentExclusivityGroup", developer, StringComparison.Ordinal);
        Assert.Contains("eligible runtime IDs", technical, StringComparison.Ordinal);
        Assert.Contains("before the inner dispatcher runs", technical, StringComparison.Ordinal);
        Assert.Contains("same register-if-absent rule", technical, StringComparison.Ordinal);
    }

    [Fact]
    public void EncounterOrchestrationDocumentation_PreservesSchedulingAndTerminationAuthority()
    {
        string mechanics = File.ReadAllText(
            RepositoryPath("docs", "mechanics", "encounter-rounds-phases-and-turns.md"))
            .ReplaceLineEndings(" ");
        string developer = File.ReadAllText(
            RepositoryPath("docs", "developer-guide", "encounter-orchestration.md"))
            .ReplaceLineEndings(" ");
        string technical = File.ReadAllText(
            RepositoryPath("docs", "technical", "encounter-orchestration-runtime.md"))
            .ReplaceLineEndings(" ");
        string closureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-final-closure-review-2026-07-30.md"))
            .ReplaceLineEndings(" ");

        string[] mechanicsTokens =
        [
            "The scheduler chooses *who receives the next window*.",
            "The turn economy decides",
            "TeamPhaseRoundRobinBattleEncounterSchedulePolicy",
            "AgilityOrderedBattleEncounterSchedulePolicy",
            "**Menu Back:**",
            "**Typed encounter cancellation:**",
            "**Operational cancellation:**",
            "encounter-wide structural-transition bound",
            "last round that was reached",
            "number of rounds whose round-end lifecycle fully committed"
        ];
        Assert.All(
            mechanicsTokens,
            token => Assert.Contains(token, mechanics, StringComparison.Ordinal));

        string[] developerTokens =
        [
            "## Required Composition",
            "## Choosing A Scheduler",
            "## Implementing The Turn Handler",
            "Do not return `Cancelled` for submenu Back.",
            "The handler must not mutate the encounter economy.",
            "The runner owns structural events.",
            "BattleEncounterProgressPolicy",
            "frozen participant graph",
            "OperationCanceledException",
            "AutomatedBattleRunner.RunAsync",
            "RuntimeInstanceId -> Node"
        ];
        Assert.All(
            developerTokens,
            token => Assert.Contains(token, developer, StringComparison.Ordinal));

        string[] technicalTokens =
        [
            "## Authority Map",
            "## Outer State Machine",
            "## Scheduler Protocol",
            "## Command Transaction",
            "## Reconciliation Fixed Point",
            "## Canonical Event Authority",
            "BattleEncounterProgressPolicy",
            "frozen participant graph",
            "PhaseEnded` follows committed phase-end lifecycle events; reconciliation then",
            "RoundEnded` follows committed round-end lifecycle events and reconciliation",
            "OperationCanceledException",
            "BattleEncounterResult` does not expose live participants",
            "```mermaid"
        ];
        Assert.All(
            technicalTokens,
            token => Assert.Contains(token, technical, StringComparison.Ordinal));
        Assert.Contains(
            "**Result:** no unresolved realistic reachable defect found",
            closureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Invariants Rechecked", closureReview, StringComparison.Ordinal);

        DocumentationCapability encounter = LoadDocumentationMatrix().Capabilities.Single(
            capability => capability.Id == "encounter_orchestration");
        Assert.Equal("reviewed", encounter.Mechanics.State);
        Assert.Equal("reviewed", encounter.DeveloperGuide.State);
        Assert.Equal("reviewed", encounter.Technical.State);
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
