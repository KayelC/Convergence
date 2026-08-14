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
        string publicApi = File.ReadAllText(
            RepositoryPath("docs", "public-api-contract.md"))
            .ReplaceLineEndings(" ");
        string restrictionResolverSource = File.ReadAllText(
            RepositoryPath(
                "src",
                "Convergence.Framework",
                "Encounters",
                "AutomatedBattleTurnRestrictionResolver.cs"))
            .ReplaceLineEndings(" ");
        string previousClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-final-closure-review-2026-07-30.md"))
            .ReplaceLineEndings(" ");
        string currentAudit = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-fresh-owner-closure-audit-2026-08-04.md"))
            .ReplaceLineEndings(" ");
        string currentSourceReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r20-source-closure-review-2026-08-04.md"))
            .ReplaceLineEndings(" ");
        string finalClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r23-final-closure-review-2026-08-04.md"))
            .ReplaceLineEndings(" ");
        string postR23Audit = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-post-r23-independent-audit-2026-08-04.md"))
            .ReplaceLineEndings(" ");
        string r27ClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r27-final-closure-review-2026-08-04.md"))
            .ReplaceLineEndings(" ");
        string postR27Audit = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-post-r27-independent-audit-2026-08-04.md"))
            .ReplaceLineEndings(" ");
        string r32ClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r32-final-closure-review-2026-08-04.md"))
            .ReplaceLineEndings(" ");
        string postR32Audit = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-post-r32-independent-audit-2026-08-05.md"))
            .ReplaceLineEndings(" ");
        string r37ClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r37-final-closure-review-2026-08-05.md"))
            .ReplaceLineEndings(" ");
        string postR37Audit = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-post-r37-independent-audit-2026-08-05.md"))
            .ReplaceLineEndings(" ");
        string r42ClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r42-final-closure-review-2026-08-05.md"))
            .ReplaceLineEndings(" ");
        string postR42Audit = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-post-r42-independent-audit-2026-08-05.md"))
            .ReplaceLineEndings(" ");
        string r47ClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r47-final-closure-review-2026-08-05.md"))
            .ReplaceLineEndings(" ");
        string r48Audit = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r48-independent-closure-audit-2026-08-07.md"))
            .ReplaceLineEndings(" ");
        string r51ClosureReview = File.ReadAllText(
            RepositoryPath(
                "docs",
                "reviews",
                "encounter-orchestration-order-6-r51-final-closure-review-2026-08-08.md"))
            .ReplaceLineEndings(" ");

        string[] mechanicsTokens =
        [
            "The scheduler chooses *who receives the next window*.",
            "The turn economy decides",
            "stable team-participant ring",
            "accepted turn-window safety limit",
            "TeamPhaseRoundRobinBattleEncounterSchedulePolicy",
            "AgilityOrderedBattleEncounterSchedulePolicy",
            "**Menu Back:**",
            "**Typed encounter cancellation:**",
            "**Operational cancellation:**",
            "boundary opens only after `BattleStarted` publication completes.",
            "encounter-wide structural-transition bound",
            "`None` consumption must preserve an exactly equal before/after economy snapshot",
            "Only round end may advance the round counters",
            "Defeat cleanup and announcement happen once for each uninterrupted period",
            "The selected turn handler must enact that restriction",
            "fixed commands use `guard`, `pass`, `analyze`, and `escape`",
            "explicit departure reason owns cleanup for the whole current defeat period",
            "Zero living teams produce an immediate `Draw`",
            "a completion policy cannot create `Faulted`",
            "Normal terminal outcomes always have null `FaultMessage` and `FaultCode`",
            "result owns the complete canonical sequenced history",
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
            "The committed restriction is authoritative input, not advisory metadata.",
            "## Supplied Automated Restriction Resolver",
            "IAutomatedBattleRestrictionActionSource",
            "Restricted automated selections use the typed command's canonical action ID.",
            "explicit reason owns cleanup for the complete current defeat period",
            "trusted host mutation ports",
            "The runner owns structural events.",
            "BattleEncounterProgressPolicy",
            "frozen participant graph",
            "unless its action event kind is `PartyRosterTransitioned`",
            "Actorless ordinary-action evidence is rejected",
            "preserves a null command target for valid untargeted skills",
            "maps a successful skill escape request to the canonical `Escape` outcome",
            "Every normal result has null `FaultMessage` and `FaultCode`",
            "track the sequence numbers it actually consumed",
            "publication of the structural `BattleStarted` event completes",
            "Every nonterminal `None` result advances the consecutive-free-action counter",
            "An active state must report `CompletedRounds == CurrentRound - 1`",
            "If that value is false, selecting another",
            "pre-release `MaximumCommands` property increments",
            "OperationCanceledException",
            "AutomatedBattleRunner.RunAsync",
            "RuntimeInstanceId -> Node"
        ];
        Assert.All(
            developerTokens,
            token => Assert.Contains(token, developer, StringComparison.Ordinal));
        Assert.DoesNotContain(
            "IAutomatedRestrictedActionSource",
            developer,
            StringComparison.Ordinal);
        Assert.Contains(
            "public interface IAutomatedBattleRestrictionActionSource",
            restrictionResolverSource,
            StringComparison.Ordinal);

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
            "active state satisfies `CompletedRounds == CurrentRound - 1`",
            "accepted exhausted",
            "stable team-ring offset",
            "`RoundEnded` advances exactly one round",
            "`None` consumption requires exactly equal before/after economy snapshots",
            "one defeat announcement per uninterrupted defeated period",
            "The turn handler owns restriction enactment.",
            "select at most one exact defeat, flee, or roster-recall reason per actor",
            "cannot append Defeat cleanup to the same explicit departure",
            "`ActionExecuted` must also identify that actor",
            "`PartyRosterTransitioned`",
            "no deployed living teams produces `Draw`",
            "complete canonical sequenced event history",
            "result-only terminal evidence",
            "Every non-fault outcome has null `FaultMessage` and `FaultCode`",
            "Custom turn handlers and state synchronizers are trusted mutation ports",
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
            "`FaultMessage` and `FaultCode` are present together only for `Faulted`",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "a sink failure cannot remove or renumber it",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "it cannot originate `Faulted`",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "typed command identity as authoritative",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "complete current uninterrupted defeat period",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "`ActionTurnConsumptionKind.None` is a universal no-cost contract",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "Only `RoundEnded` can advance exactly one round",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "Each event is recorded before optional sink publication",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "later cleanup failure is secondary evidence",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** no unresolved realistic reachable defect found",
            previousClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Invariants Rechecked", previousClosureReview, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** corrections required; Order 6 is reopened",
            currentAudit,
            StringComparison.Ordinal);
        Assert.Contains("## Correction Roadmap", currentAudit, StringComparison.Ordinal);
        Assert.Contains("## Correction Progress After This Audit", currentAudit, StringComparison.Ordinal);
        Assert.Contains("O6-R15 through O6-R23 complete", currentAudit, StringComparison.Ordinal);
        Assert.Contains("O6-R20 | Complete; correction required", currentAudit, StringComparison.Ordinal);
        Assert.Contains("O6-R21 | Complete", currentAudit, StringComparison.Ordinal);
        Assert.Contains("O6-R22 | Complete", currentAudit, StringComparison.Ordinal);
        Assert.Contains("O6-R23 | Complete", currentAudit, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** one bounded correction required",
            currentSourceReview,
            StringComparison.Ordinal);
        Assert.Contains("### O6-R20-M1", currentSourceReview, StringComparison.Ordinal);
        Assert.Contains("## Correction Progress", currentSourceReview, StringComparison.Ordinal);
        Assert.Contains("O6-R21 | Complete", currentSourceReview, StringComparison.Ordinal);
        Assert.Contains("O6-R23 | Complete", currentSourceReview, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** no unresolved realistic reachable defect found",
            finalClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Corrected Invariants Rechecked", finalClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Trusted Boundaries And Residual Risk", finalClosureReview, StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally complete", finalClosureReview, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** one runtime correction and one documentation correction required; Order 6 is reopened",
            postR23Audit,
            StringComparison.Ordinal);
        Assert.Contains("### O6-R24-M1", postR23Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-R24-L1", postR23Audit, StringComparison.Ordinal);
        Assert.Contains("## Correction Roadmap", postR23Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R25 | `complete`", postR23Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R26 | `complete`", postR23Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R27 | `complete`", postR23Audit, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** no unresolved realistic reachable runtime defect found",
            r27ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Corrected Invariants Rechecked", r27ClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Trusted Boundaries And Residual Risk", r27ClosureReview, StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally complete", r27ClosureReview, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** two realistic reachable runtime corrections and one documentation correction are required; Order 6 is reopened",
            postR27Audit,
            StringComparison.Ordinal);
        Assert.Contains("### O6-R28-M1", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-R28-M2", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-R28-L1", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("## Correction Roadmap", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R29 | Make typed command identity", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R30 | Preserve one explicit departure reason", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R31 | Reconcile mechanics", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R32 | Independently reread", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("## Correction Progress", postR27Audit, StringComparison.Ordinal);
        Assert.Contains("**Order 6 is not ready to close.**", postR27Audit, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** no unresolved realistic reachable encounter-orchestration defect found",
            r32ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Corrected Invariants Rechecked", r32ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### Canonical restricted-command identity", r32ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### One departure reason per defeat period", r32ClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Trusted Boundaries And Residual Risk", r32ClosureReview, StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally complete", r32ClosureReview, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** two realistic reachable runtime corrections and one documentation precision correction are required; Order 6 is reopened",
            postR32Audit,
            StringComparison.Ordinal);
        Assert.Contains("### O6-R33-M1", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-R33-M2", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-R33-L1", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("## Correction Roadmap", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R34 | Enforce no-cost", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R35 | Enforce legal scheduler", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R36 | Reconcile mechanics", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R37 | Independently reread", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R34 | `complete` |", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R35 | `complete` |", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R36 | `complete` |", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R37 | `complete` |", postR32Audit, StringComparison.Ordinal);
        Assert.Contains("O6-R37 independently reread current source", postR32Audit, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** no unresolved realistic reachable encounter-orchestration defect found",
            r37ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Corrected Invariants Rechecked", r37ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### No-cost turn-economy authority", r37ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### Scheduler structural continuity", r37ClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Trusted Boundaries And Residual Risk", r37ClosureReview, StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally complete", r37ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### O6-M1", postR37Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-M2", postR37Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-L1", postR37Audit, StringComparison.Ordinal);
        Assert.Contains("## Correction Roadmap", postR37Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R39 | `complete` |", postR37Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R40 | `complete` |", postR37Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R41 | `complete` |", postR37Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R42 | `complete` |", postR37Audit, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** no unresolved realistic reachable encounter-orchestration defect was found",
            r42ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Corrected Invariants Rechecked", r42ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### Stable team-participant rotation", r42ClosureReview, StringComparison.Ordinal);
        Assert.Contains(
            "### Turn-economy liveness precedes another command window",
            r42ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("### Accepted turn-window safety bound", r42ClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Trusted Boundaries And Residual Risk", r42ClosureReview, StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally complete after O6-R42", r42ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### O6-M1", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-M2", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-L1", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("## Confirmed Healthy Areas", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("## Documentation Alignment", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("## Correction Roadmap", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R43 | `complete` |", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R44 | `pending` |", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R45 | `pending` |", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R46 | `pending` |", postR42Audit, StringComparison.Ordinal);
        Assert.Contains("| O6-R47 | `pending` |", postR42Audit, StringComparison.Ordinal);
        Assert.Contains(
            "**Result:** no unresolved realistic reachable encounter-orchestration defect",
            r47ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Corrected Invariants Rechecked", r47ClosureReview, StringComparison.Ordinal);
        Assert.Contains(
            "### Canonical event identity survives publication failure",
            r47ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains(
            "### Primary command faults survive cleanup failure",
            r47ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Documentation Cross-Examination", r47ClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Verification", r47ClosureReview, StringComparison.Ordinal);
        Assert.Contains("CONVERGENCE_GODOT_SMOKE_OK", r47ClosureReview, StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally complete after O6-R47", r47ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### O6-R48-L1", r48Audit, StringComparison.Ordinal);
        Assert.Contains("### O6-R48-D1", r48Audit, StringComparison.Ordinal);
        Assert.Contains("## Runtime Conclusions", r48Audit, StringComparison.Ordinal);
        Assert.Contains("## Documentation Alignment", r48Audit, StringComparison.Ordinal);
        Assert.Contains("## Correction Checkpoints", r48Audit, StringComparison.Ordinal);
        Assert.Contains("**O6-R49 resolution, 8 August 2026**", r48Audit, StringComparison.Ordinal);
        Assert.Contains("**O6-R50 resolution, 8 August 2026**", r48Audit, StringComparison.Ordinal);
        Assert.Contains(
            "| O6-R49 | Remove the unused automated-runner services dependency and update public API evidence | Complete |",
            r48Audit,
            StringComparison.Ordinal);
        Assert.Contains(
            "| O6-R50 | Correct the technical command transaction diagram and revalidate all audience guidance | Complete |",
            r48Audit,
            StringComparison.Ordinal);
        Assert.Contains(
            "| O6-R51 | Perform one bounded fresh closure review over the two corrections | Complete |",
            r48Audit,
            StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally owner-closed", r48Audit, StringComparison.Ordinal);
        Assert.Contains(
            "**no unresolved realistic reachable defect found; Order 6 is formally owner-closed**",
            r51ClosureReview,
            StringComparison.Ordinal);
        Assert.Contains("## Corrected Invariants Rechecked", r51ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### One automated action-execution authority", r51ClosureReview, StringComparison.Ordinal);
        Assert.Contains("### Complete command-status transaction", r51ClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Documentation Cross-Examination", r51ClosureReview, StringComparison.Ordinal);
        Assert.Contains("## Trusted Boundaries And Residual Risk", r51ClosureReview, StringComparison.Ordinal);
        Assert.Contains("CONVERGENCE_GODOT_SMOKE_OK", r51ClosureReview, StringComparison.Ordinal);
        Assert.Contains("Order 6 is formally owner-closed after O6-R51", r51ClosureReview, StringComparison.Ordinal);

        string encounterTechnical = File.ReadAllText(
            RepositoryPath("docs", "technical", "encounter-orchestration-runtime.md"));
        Assert.Contains("CommandEvents[\"Publish validated command events\"]", encounterTechnical, StringComparison.Ordinal);
        Assert.Contains("CommandStatus{\"Returned command status\"}", encounterTechnical, StringComparison.Ordinal);
        Assert.Contains("CommandStatus -->|\"Cancelled\"| CancelledEnd", encounterTechnical, StringComparison.Ordinal);
        Assert.Contains("CommandStatus -->|\"Faulted\"| FaultedEnd", encounterTechnical, StringComparison.Ordinal);
        Assert.Contains("CommandStatus -->|\"Rejected\"| RejectedEnd", encounterTechnical, StringComparison.Ordinal);
        Assert.Contains("CommandStatus -->|\"Executed\"| ApplyEconomy", encounterTechnical, StringComparison.Ordinal);
        Assert.Contains("Consumption -->|\"Yes\"| EconomyEvent", encounterTechnical, StringComparison.Ordinal);

        DocumentationCapability encounter = LoadDocumentationMatrix().Capabilities.Single(
            capability => capability.Id == "encounter_orchestration");
        Assert.Equal("reviewed", encounter.Mechanics.State);
        Assert.Equal("reviewed", encounter.DeveloperGuide.State);
        Assert.Equal("reviewed", encounter.Technical.State);

        FrameworkCapability encounterCapability = LoadFrameworkCapabilityMatrix().Capabilities.Single(
            capability => capability.Id == "encounter_orchestration");
        Assert.Equal("complete", encounterCapability.ImplementationState);
        Assert.Empty(encounterCapability.KnownGaps);
    }

    [Fact]
    public void InventoryEquipmentEconomyDocumentation_PreservesOrder7AuthorityAndOpenClosureGate()
    {
        string mechanics = File.ReadAllText(
            RepositoryPath("docs", "mechanics", "party-inventory-and-economy.md"))
            .ReplaceLineEndings(" ");
        string developer = File.ReadAllText(
            RepositoryPath("docs", "developer-guide", "inventory-equipment-and-economy.md"))
            .ReplaceLineEndings(" ");
        string technical = File.ReadAllText(
            RepositoryPath("docs", "technical", "inventory-equipment-economy-runtime.md"))
            .ReplaceLineEndings(" ");
        string publicApi = File.ReadAllText(
            RepositoryPath("docs", "public-api-contract.md"))
            .ReplaceLineEndings(" ");

        string[] mechanicsTokens =
        [
            "Every equipment copy has its own runtime identity.",
            "Buying an equipment copy adds it to inventory; it does not silently equip it.",
            "Equipment-granted skills are temporary availability, not learned skills.",
            "A rejection updates none of them.",
            "which resources are fully restored",
            "An actor at full configured resources may still have a valid zero-cost treatment",
            "An individually protected ailment remains on the actor",
            "Current save contract v19 stores:"
        ];
        Assert.All(
            mechanicsTokens,
            token => Assert.Contains(token, mechanics, StringComparison.Ordinal));

        string[] developerTokens =
        [
            "BindResourceManagementServices",
            "IEquipmentSlotLayoutPolicy",
            "new RuntimeSaveValidator(equipmentSlotLayout: layout)",
            "RuntimeActorEquipmentProfileSource",
            "custom layout that permits multiple simultaneous weapon profiles",
            "Adopt all three after-snapshots together",
            "Serialize shop mutations per game session",
            "complete current collection of other actor loadouts",
            "Its after-ledger is hypothetical and must not be adopted",
            "Reconnect Nodes by runtime ID only after aggregate restore succeeds"
        ];
        Assert.All(
            developerTokens,
            token => Assert.Contains(token, developer, StringComparison.Ordinal));

        string[] technicalTokens =
        [
            "RuntimeInventorySnapshot` | item quantities and every owned equipment instance",
            "There is no root equipment-owner collection besides inventory.",
            "## Equip And Unequip Transition",
            "## Shop Transaction State Machine",
            "## Recovery Transaction State Machine",
            "## Save V19 Validation And Restore",
            "ID and nonnegative amount",
            "enforced by `RuntimeCurrencyLedgerSnapshot` construction",
            "Adopting one candidate makes every other same-before candidate stale.",
            "complete current loadout collection supplied by the host",
            "```mermaid"
        ];
        Assert.All(
            technicalTokens,
            token => Assert.Contains(token, technical, StringComparison.Ordinal));

        Assert.Contains(
            "Save contract v19 retains the canonical roster",
            publicApi,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Save contract v18", publicApi, StringComparison.Ordinal);

        DocumentationCapability documentation = LoadDocumentationMatrix().Capabilities.Single(
            capability => capability.Id == "inventory_equipment_economy");
        Assert.Equal("reviewed", documentation.Mechanics.State);
        Assert.Equal("reviewed", documentation.DeveloperGuide.State);
        Assert.Equal("reviewed", documentation.Technical.State);

        FrameworkCapability capability = LoadFrameworkCapabilityMatrix().Capabilities.Single(
            candidate => candidate.Id == "inventory_equipment_economy");
        Assert.Equal("partial", capability.ImplementationState);
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

    private sealed record FrameworkCapability(
        string Id,
        string ImplementationState,
        IReadOnlyList<string> KnownGaps);
}
