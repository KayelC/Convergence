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

            if (capability.ImplementationState == "implemented")
            {
                Assert.Empty(capability.KnownGaps);
            }
            else
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
        Assert.Equal(7, ordered.Count(capability => capability.OrderState == "closed"));
        Assert.DoesNotContain(ordered, capability => capability.OrderState == "open");
        Assert.Equal(13, ordered.Count(capability => capability.OrderState == "not_started"));

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
