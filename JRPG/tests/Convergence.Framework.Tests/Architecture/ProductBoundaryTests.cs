using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class ProductBoundaryTests
{
    private static readonly string[] ExpectedProjectPaths =
    [
        "samples/Convergence.DemoHost/Convergence.DemoHost.csproj",
        "src/Convergence.Framework/Convergence.Framework.csproj",
        "tests/Convergence.DemoHost.Tests/Convergence.DemoHost.Tests.csproj",
        "tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj"
    ];

    [Fact]
    public void ActiveSolution_ContainsOnlyTheCleanProductProjects()
    {
        string solution = File.ReadAllText(RepositoryPath("Convergence.sln"));
        string[] projectPaths = Regex.Matches(
                solution,
                "\"(?<path>[^\"]+\\.csproj)\"",
                RegexOptions.CultureInvariant)
            .Select(match => NormalizePath(match.Groups["path"].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedProjectPaths.Order(StringComparer.Ordinal), projectPaths);
    }

    [Fact]
    public void ActiveContent_IsManifestOwnedAndSafeForFlatHostCopying()
    {
        string contentRoot = RepositoryPath("content");
        string[] allJson = Directory.EnumerateFiles(contentRoot, "*.json", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] manifests = allJson
            .Where(IsManifest)
            .ToArray();
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(manifests);
        foreach (string manifest in manifests)
        {
            Assert.True(owned.Add(manifest), $"Manifest '{manifest}' was already owned.");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
            foreach (JsonElement reference in document.RootElement.GetProperty("documents").EnumerateArray())
            {
                string logicalPath = reference.GetProperty("path").GetString()!;
                Assert.Equal(Path.GetFileName(logicalPath), logicalPath);

                string referencedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest)!, logicalPath));
                Assert.True(File.Exists(referencedPath), $"Manifest '{manifest}' references missing '{logicalPath}'.");
                Assert.True(owned.Add(referencedPath), $"Content document '{referencedPath}' has multiple owners.");
            }
        }

        Assert.Equal(allJson, owned.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(
            allJson.Length,
            allJson.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ActiveProductionSourcesAndProjects_DoNotReferenceTheLegacyArchive()
    {
        string[] roots = [RepositoryPath("src"), RepositoryPath("samples")];
        string[] forbidden =
        [
            "ArchiveDocs",
            "JRPGPrototype",
            "JRPG.ConsoleHost",
            "Newtonsoft.Json",
            "Database.",
            "SkillData",
            "ItemData",
            "IGameIO",
            "Legacy"
        ];

        foreach (string root in roots)
        {
            foreach (string file in EnumerateSourceAndProjectFiles(root))
            {
                string source = File.ReadAllText(file);
                foreach (string token in forbidden)
                {
                    Assert.DoesNotContain(token, source, StringComparison.Ordinal);
                }
            }
        }

        string solution = File.ReadAllText(RepositoryPath("Convergence.sln"));
        Assert.DoesNotContain("ArchiveDocs", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("JRPG", solution, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoHost_DoesNotRegisterBindOrRequireMoonPhase()
    {
        string demoRoot = RepositoryPath("samples", "Convergence.DemoHost");
        string[] forbidden =
        [
            "RegisterMoonPhase(",
            "BindMoonPhase",
            "\"new_moon\"",
            "\"standard_moon_phase\""
        ];

        foreach (string file in EnumerateSourceAndProjectFiles(demoRoot))
        {
            string source = File.ReadAllText(file);
            foreach (string token in forbidden)
            {
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
            }
        }
    }

    private static bool IsManifest(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("documents", out JsonElement documents) &&
            documents.ValueKind == JsonValueKind.Array;
    }

    private static IEnumerable<string> EnumerateSourceAndProjectFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file =>
                !HasPathSegment(file, "bin") &&
                !HasPathSegment(file, "obj") &&
                (Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(file).Equals(".csproj", StringComparison.OrdinalIgnoreCase)));

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string RepositoryPath(params string[] segments)
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Convergence.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        Assert.NotNull(current);
        return Path.Combine([current!, .. segments]);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');
}
