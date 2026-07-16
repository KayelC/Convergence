using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Convergence.Content;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class FrameworkSourceInventoryTests
{
    private static readonly Regex PublicTypeDeclaration = new(
        @"(?m)^\s*public\s+(?:(?:sealed|abstract|static|readonly|partial|ref)\s+)*(?:(?:record)(?:\s+(?:class|struct))?|class|interface|enum|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant);
    private static readonly Regex NamespaceDeclaration = new(
        @"(?m)^\s*namespace\s+(?<name>[A-Za-z_][A-Za-z0-9_.]*)\s*;",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Inventory_AccountsForEveryFrameworkSourceAndPublicTypeOwner()
    {
        FrameworkSourceInventory inventory = Load();
        Assert.Equal(1, inventory.SchemaVersion);
        Assert.Equal("Convergence.Framework", inventory.Product);
        Assert.Equal(["exported", "internal_only"], inventory.PublicSurfaceStates.Order());

        Dictionary<string, SourceOwner> owners = inventory.Owners.ToDictionary(owner => owner.Id);
        Assert.Equal(inventory.Owners.Count, owners.Count);
        Assert.All(inventory.Owners, owner =>
        {
            Assert.Matches("^[a-z0-9_]+$", owner.Id);
            Assert.EndsWith("/", owner.PathPrefix, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(owner.Responsibility));
        });

        string sourceRoot = RepositoryPath("src", "Convergence.Framework");
        string[] actualPaths = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"))
            .Select(path => NormalizePath(Path.GetRelativePath(sourceRoot, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] inventoriedPaths = inventory.Sources
            .Select(source => source.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(actualPaths, inventoriedPaths);
        Assert.Equal(inventory.Sources.Count, inventory.Sources.Select(source => source.Path).Distinct().Count());
        Assert.All(inventory.Owners, owner => Assert.Contains(inventory.Sources, source => source.Owner == owner.Id));

        var declaredTypes = new List<DeclaredPublicType>();
        foreach (SourceEntry entry in inventory.Sources)
        {
            Assert.Contains(entry.PublicSurface, inventory.PublicSurfaceStates);
            SourceOwner owner = Assert.Contains(entry.Owner, owners);
            Assert.StartsWith(owner.PathPrefix, entry.Path, StringComparison.Ordinal);

            string source = File.ReadAllText(Path.Combine(sourceRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
            MatchCollection declarations = PublicTypeDeclaration.Matches(source);
            string expectedSurface = declarations.Count == 0 ? "internal_only" : "exported";
            Assert.Equal(expectedSurface, entry.PublicSurface);

            if (declarations.Count > 0)
            {
                Match namespaceMatch = NamespaceDeclaration.Match(source);
                Assert.True(namespaceMatch.Success, $"Exported source '{entry.Path}' has no file-scoped namespace.");
                string declaredNamespace = namespaceMatch.Groups["name"].Value;
                Assert.Contains(declaredNamespace, owner.NamespacePrefixes);
                foreach (Match declaration in declarations)
                {
                    declaredTypes.Add(new DeclaredPublicType(
                        declaredNamespace,
                        declaration.Groups["name"].Value,
                        entry.Path,
                        owner.Id));
                }
            }
        }

        foreach (Type type in typeof(ContentId).Assembly.GetExportedTypes())
        {
            string sourceName = type.Name.Split('`')[0];
            DeclaredPublicType declaration = Assert.Single(
                declaredTypes,
                candidate => candidate.Namespace == type.Namespace && candidate.Name == sourceName);
            Assert.Contains(declaration.Owner, owners.Keys);
        }
    }

    [Fact]
    public void RepositoryGuidance_StatesTheCuratedDocumentationBoundary()
    {
        string guidance = File.ReadAllText(RepositoryPath("AGENTS.md"));
        string apiContract = File.ReadAllText(RepositoryPath("docs", "public-api-contract.md"));
        string ownership = File.ReadAllText(RepositoryPath("docs", "reference", "framework-source-ownership.md"));

        Assert.Contains("src/Convergence.Framework", guidance, StringComparison.Ordinal);
        Assert.Contains("ArchiveDocs", guidance, StringComparison.Ordinal);
        Assert.Contains("unsupported history", guidance, StringComparison.Ordinal);
        Assert.Contains("XML documentation is curated and intentionally incomplete", apiContract, StringComparison.Ordinal);
        Assert.Contains(
            $"It currently accounts for {Load().Sources.Count} active Framework C# files.",
            ownership,
            StringComparison.Ordinal);
    }

    private static bool ContainsDirectory(string path, string directory) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(directory, StringComparer.OrdinalIgnoreCase);

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static FrameworkSourceInventory Load() =>
        JsonSerializer.Deserialize<FrameworkSourceInventory>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "framework-source-inventory.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Framework source inventory did not deserialize.");

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

    private sealed record FrameworkSourceInventory(
        int SchemaVersion,
        string Product,
        IReadOnlyList<string> PublicSurfaceStates,
        IReadOnlyList<SourceOwner> Owners,
        IReadOnlyList<SourceEntry> Sources);

    private sealed record SourceOwner(
        string Id,
        string PathPrefix,
        IReadOnlyList<string> NamespacePrefixes,
        string Responsibility);

    private sealed record SourceEntry(
        string Path,
        string Owner,
        string PublicSurface);

    private sealed record DeclaredPublicType(
        string Namespace,
        string Name,
        string Path,
        string Owner);
}
