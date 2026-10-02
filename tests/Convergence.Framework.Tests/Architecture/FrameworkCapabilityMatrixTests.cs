using System.Text.Json;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class FrameworkCapabilityMatrixTests
{
    private static readonly HashSet<string> States = ["implemented", "partial", "deferred"];
    private static readonly HashSet<string> OrderStates = ["not_applicable", "not_started", "open", "closed"];
    private static readonly HashSet<string> CoverageLevels = ["none", "focused", "end_to_end"];
    private static readonly string[] OrderedCapabilityIds =
    [
        "typed_action_and_effect_execution",
        "combat_resolution",
        "turn_economy",
        "status_and_passive_lifecycle",
        "battle_knowledge",
        "encounter_orchestration",
        "inventory_equipment_economy",
        "navigation",
        "dungeon_traversal",
        "negotiation_and_rewards",
        "fusion_and_inheritance",
        "compendium",
        "persistence_snapshots",
        "content_definitions",
        "portable_deserialization",
        "content_validation",
        "catalog_loading",
        "authored_schema_contracts",
        "host_contracts",
        "godot_adapter"
    ];

    [Fact]
    public void Matrix_UsesCleanProductStatesAndAuditableEvidence()
    {
        CapabilityMatrix matrix = Load();

        Assert.Equal(2, matrix.SchemaVersion);
        Assert.Equal("Convergence.Framework", matrix.Product);
        Assert.Equal(States.Order(), matrix.States.Order());
        Assert.Equal(OrderStates.Order(), matrix.OrderStates.Order());
        Assert.Equal(CoverageLevels.Order(), matrix.DemoCoverageLevels.Order());
        Assert.NotEmpty(matrix.Capabilities);
        Assert.Equal(
            matrix.Capabilities.Count,
            matrix.Capabilities.Select(capability => capability.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (CapabilityEntry capability in matrix.Capabilities)
        {
            Assert.Matches("^[a-z0-9_]+$", capability.Id);
            Assert.Contains(capability.ImplementationState, States);
            Assert.Contains(capability.OrderState, OrderStates);
            Assert.Contains(capability.DemoCoverage, CoverageLevels);
            Assert.True(capability.HostNeutral, $"Capability '{capability.Id}' is not host-neutral.");
            Assert.NotEmpty(capability.FrameworkTests);
            Assert.DoesNotContain(capability.FrameworkTests, value => string.IsNullOrWhiteSpace(value));

            if (capability.ImplementationState != "implemented")
            {
                Assert.NotEmpty(capability.KnownGaps);
            }

            if (capability.OrderNumber is null)
            {
                Assert.Equal("not_applicable", capability.OrderState);
            }
            else
            {
                Assert.InRange(capability.OrderNumber.Value, 1, OrderedCapabilityIds.Length);
                Assert.NotEqual("not_applicable", capability.OrderState);
            }
        }

        CapabilityEntry[] ordered = matrix.Capabilities
            .Where(capability => capability.OrderNumber is not null)
            .OrderBy(capability => capability.OrderNumber)
            .ToArray();
        Assert.Equal(Enumerable.Range(1, OrderedCapabilityIds.Length), ordered.Select(capability => capability.OrderNumber!.Value));
        Assert.Equal(OrderedCapabilityIds, ordered.Select(capability => capability.Id));
        Assert.Equal(9, ordered.Count(capability => capability.OrderState == "closed"));
        Assert.Equal(0, ordered.Count(capability => capability.OrderState == "open"));
        Assert.Equal(11, ordered.Count(capability => capability.OrderState == "not_started"));

        using JsonDocument documentation = JsonDocument.Parse(File.ReadAllText(DocumentationMatrixPath()));
        foreach (CapabilityEntry capability in ordered.Where(capability => capability.OrderState == "closed"))
        {
            Assert.Equal("implemented", capability.ImplementationState);
            Assert.Empty(capability.KnownGaps);
            JsonElement documentationCapability = documentation.RootElement
                .GetProperty("capabilities")
                .EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == capability.Id);
            Assert.All(
                new[] { "mechanics", "developerGuide", "technical" },
                audience => Assert.Contains(
                    documentationCapability.GetProperty(audience).GetProperty("state").GetString(),
                    new[] { "reviewed", "not_applicable" }));
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
    public void GodotAdapterOrder20_TracksRealFieldPersistenceCarryForward()
    {
        CapabilityEntry godot = Load().Capabilities.Single(capability => capability.Id == "godot_adapter");

        Assert.Equal("implemented", godot.ImplementationState);
        Assert.Equal(20, godot.OrderNumber);
        Assert.Equal("not_started", godot.OrderState);
        string gap = Assert.Single(godot.KnownGaps);
        Assert.Contains("GodotSaveCodec.Serialize accepts no RuntimeFieldSnapshot", gap, StringComparison.Ordinal);
        Assert.Contains("field: null", gap, StringComparison.Ordinal);
        Assert.Contains("RuntimeSaveValidator.CreateWithDungeonProgressRegistry", gap, StringComparison.Ordinal);
        Assert.Contains("DungeonProgressRegistryMissing", gap, StringComparison.Ordinal);
        Assert.Contains("test-only in-memory GodotSaveSnapshotStore", gap, StringComparison.Ordinal);

        string capabilityRoadmap = File.ReadAllText(RepositoryPath(
            "docs", "roadmap", "framework-capability-matrix.md"));
        string documentationRoadmap = File.ReadAllText(RepositoryPath(
            "docs", "roadmap", "documentation-completion-roadmap.md"));
        foreach (string document in new[] { capabilityRoadmap, documentationRoadmap })
        {
            Assert.Contains("GodotSaveCodec.Serialize", document, StringComparison.Ordinal);
            Assert.Contains("field: null", document, StringComparison.Ordinal);
            Assert.Contains("RuntimeSaveValidator.CreateWithDungeonProgressRegistry", document, StringComparison.Ordinal);
            Assert.Contains("DungeonProgressRegistryMissing", document, StringComparison.Ordinal);
            Assert.Contains("GodotSaveSnapshotStore", document, StringComparison.Ordinal);
            Assert.Contains("Order 9 owner-closure audit", document, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ActiveDocuments_ReportTotalsDerivedFromTheExecutableMatrix()
    {
        CapabilityMatrix matrix = Load();
        int implemented = matrix.Capabilities.Count(capability => capability.ImplementationState == "implemented");
        int partial = matrix.Capabilities.Count(capability => capability.ImplementationState == "partial");
        int deferred = matrix.Capabilities.Count(capability => capability.ImplementationState == "deferred");
        string expectedImplementation =
            $"The matrix currently records {matrix.Capabilities.Count} capabilities: " +
            $"{implemented} implemented, {partial} partial, and {deferred} deferred.";
        CapabilityEntry[] ordered = matrix.Capabilities
            .Where(capability => capability.OrderNumber is not null)
            .ToArray();
        string expectedOrders =
            $"The ordered review queue currently records {ordered.Length} Orders: " +
            $"{ordered.Count(capability => capability.OrderState == "closed")} closed, " +
            $"{ordered.Count(capability => capability.OrderState == "open")} open, and " +
            $"{ordered.Count(capability => capability.OrderState == "not_started")} not_started.";

        foreach (string document in new[]
        {
            RepositoryPath("docs", "roadmap", "framework-capability-matrix.md"),
            RepositoryPath("docs", "roadmap", "product-roadmap.md")
        })
        {
            string text = File.ReadAllText(document);
            Assert.Contains(expectedImplementation, text, StringComparison.Ordinal);
            Assert.Contains(expectedOrders, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NavigationOrder8Review_RecordsReviewedEvidenceForClosedOrder()
    {
        CapabilityEntry navigation = Load().Capabilities.Single(capability => capability.Id == "navigation");

        Assert.Equal("implemented", navigation.ImplementationState);
        Assert.Equal(8, navigation.OrderNumber);
        Assert.Equal("closed", navigation.OrderState);
        Assert.Empty(navigation.KnownGaps);

        string review = File.ReadAllText(RepositoryPath(
            "docs",
            "reviews",
            "navigation-order-8-source-review-2026-09-14.md"));
        string[] requiredEvidence =
        [
            "**Source baseline:** `a1f91e68`",
            "### O8-M1: Live navigation accepts empty identifiers",
            "### O8-M2: The public result contract permits contradictory states",
            "### O8-M3: The custom-policy failure contract is undefined",
            "### O8-L1: Training Annex confuses retained dungeon progress with active location",
            "### O8-L2: Direct evidence does not cover the full public boundary",
            "### O8-D6: Retain or change the combined field aggregate",
            "| O8-R8 | `complete` |",
            "Order 9 remains responsible for dungeon traversal."
        ];
        Assert.All(requiredEvidence, token => Assert.Contains(token, review, StringComparison.Ordinal));

        string closure = File.ReadAllText(RepositoryPath(
            "docs", "reviews", "navigation-order-8-final-closure-review-2026-09-19.md"));
        Assert.Contains("fd0ef334", closure, StringComparison.Ordinal);
        Assert.Contains("navigation-order-8-owner-closure", closure, StringComparison.Ordinal);

        using JsonDocument documentation = JsonDocument.Parse(File.ReadAllText(DocumentationMatrixPath()));
        JsonElement navigationDocumentation = documentation.RootElement
            .GetProperty("capabilities")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("id").GetString() == "navigation");
        Assert.All(
            new[] { "mechanics", "developerGuide", "technical" },
            audience => Assert.Equal(
                "reviewed",
                navigationDocumentation.GetProperty(audience).GetProperty("state").GetString()));
    }

    [Fact]
    public void DungeonOrder9Review_RecordsOwnerApprovedClosure()
    {
        CapabilityEntry dungeon = Load().Capabilities.Single(capability => capability.Id == "dungeon_traversal");

        Assert.Equal("implemented", dungeon.ImplementationState);
        Assert.Equal(9, dungeon.OrderNumber);
        Assert.Equal("closed", dungeon.OrderState);
        Assert.Empty(dungeon.KnownGaps);

        string review = File.ReadAllText(RepositoryPath(
            "docs", "reviews", "dungeon-traversal-order-9-source-review-2026-09-19.md"));
        Assert.All(new[]
        {
            "### O9-M1:",
            "### O9-M2:",
            "### O9-M3:",
            "### O9-M4:",
            "## Navigation/Dungeon/Save Boundary"
        }, token => Assert.Contains(token, review, StringComparison.Ordinal));

        string roadmap = File.ReadAllText(RepositoryPath(
            "docs", "roadmap", "dungeon-traversal-order-9-roadmap.md"));
        Assert.Contains("| O9-R1: opening review | complete |", roadmap, StringComparison.Ordinal);
        Assert.Contains("| O9-R7: audience documentation | complete |", roadmap, StringComparison.Ordinal);
        Assert.Contains("| O9-R8: independent closure | complete |", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "| O9-C4: post-correction independent closure | complete |",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "| O9-C5: owner-closure source audit | complete |",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "| O9-C6: audience and tracking truth correction | complete |",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "| O9-C7: retained owner-closure gate | complete |",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("CurrentSaveContext", review, StringComparison.Ordinal);

        string capabilityNarrative = File.ReadAllText(RepositoryPath(
            "docs", "roadmap", "framework-capability-matrix.md"));
        Assert.Contains("23 September fresh audit", capabilityNarrative, StringComparison.Ordinal);
        Assert.All(Enumerable.Range(1, 4), number => Assert.Contains(
            $"O9-C{number}", capabilityNarrative, StringComparison.Ordinal));
        Assert.Contains("23-command retained gate passed on `299cbc15`", capabilityNarrative, StringComparison.Ordinal);
        Assert.Contains("1 October owner-closure audit", capabilityNarrative, StringComparison.Ordinal);
        Assert.Contains("O9-C7's 23-command retained gate passed", capabilityNarrative, StringComparison.Ordinal);
        Assert.Contains("formally closed Order 9 on 2 October 2026", capabilityNarrative, StringComparison.Ordinal);

        string decision = File.ReadAllText(RepositoryPath(
            "docs", "decisions", "dungeon-progress-reporting.md"));
        Assert.Contains("Status: confirmed", decision, StringComparison.Ordinal);
        Assert.Contains("O9-R2 through O9-R6 subsequently implemented", decision, StringComparison.Ordinal);
        Assert.Contains("O9-C1 and O9-C2 correct those gaps", decision, StringComparison.Ordinal);
        Assert.Contains("`RuntimeDungeonProgressRegistry` declares eligible IDs", decision, StringComparison.Ordinal);
        Assert.Contains("`RuntimeSaveValidator.CreateWithDungeonProgressRegistry`", decision, StringComparison.Ordinal);
        Assert.Contains("structural", decision, StringComparison.Ordinal);
        Assert.Contains("does not prove provenance", decision, StringComparison.Ordinal);
        Assert.Contains("23-command gate are complete at `13456815`", decision, StringComparison.Ordinal);
        Assert.Contains("formally closed Order 9 on 2 October 2026", decision, StringComparison.Ordinal);
        Assert.DoesNotContain("matches the current service", decision, StringComparison.Ordinal);
        Assert.All(Enumerable.Range(1, 8), number => Assert.Contains(
            $"O9-D{number}:", roadmap, StringComparison.Ordinal));

        string mechanics = File.ReadAllText(RepositoryPath(
            "docs", "mechanics", "world-encounters-and-rewards.md"));
        string developer = File.ReadAllText(RepositoryPath(
            "docs", "developer-guide", "dungeon-traversal.md"));
        string technical = File.ReadAllText(RepositoryPath(
            "docs", "technical", "dungeon-traversal-runtime.md"));
        Assert.Contains("internally plausible", mechanics, StringComparison.Ordinal);
        Assert.Contains("does not prove that live play produced the history", mechanics, StringComparison.Ordinal);
        Assert.Contains("DungeonProgressRegistryMissing` instead of trusting them", developer, StringComparison.Ordinal);
        Assert.Contains("not an anti-tamper receipt", developer, StringComparison.Ordinal);
        Assert.Contains("## Godot Evidence Boundary", technical, StringComparison.Ordinal);
        Assert.Contains("does not prove Order 8/9 persistence", technical, StringComparison.Ordinal);

        using JsonDocument documentation = JsonDocument.Parse(File.ReadAllText(DocumentationMatrixPath()));
        JsonElement dungeonDocumentation = documentation.RootElement
            .GetProperty("capabilities")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("id").GetString() == "dungeon_traversal");
        Assert.All(
            new[] { "mechanics", "developerGuide", "technical" },
            audience => Assert.Equal(
                "reviewed",
                dungeonDocumentation.GetProperty(audience).GetProperty("state").GetString()));
    }

    private static CapabilityMatrix Load() =>
        JsonSerializer.Deserialize<CapabilityMatrix>(
            File.ReadAllText(MatrixPath()),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Capability matrix did not deserialize.");

    private static string MatrixPath() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "framework-capability-matrix.json");

    private static string DocumentationMatrixPath() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "documentation-coverage-matrix.json");

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
        IReadOnlyList<string> OrderStates,
        IReadOnlyList<string> DemoCoverageLevels,
        IReadOnlyList<CapabilityEntry> Capabilities);

    private sealed record CapabilityEntry(
        string Id,
        string ImplementationState,
        int? OrderNumber,
        string OrderState,
        IReadOnlyList<string> FrameworkTests,
        string DemoCoverage,
        bool HostNeutral,
        bool OptionalModule,
        IReadOnlyList<string> KnownGaps);
}
