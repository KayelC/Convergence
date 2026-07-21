using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class ProductBoundaryTests
{
    private static readonly string[] ExpectedProjectPaths =
    [
        "samples/Convergence.DemoHost/Convergence.DemoHost.csproj",
        "samples/Convergence.GodotHost/Convergence.GodotHost.csproj",
        "src/Convergence.Framework/Convergence.Framework.csproj",
        "tests/Convergence.ContentValidator.Tests/Convergence.ContentValidator.Tests.csproj",
        "tests/Convergence.DemoHost.Tests/Convergence.DemoHost.Tests.csproj",
        "tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj",
        "tools/Convergence.ContentValidator/Convergence.ContentValidator.csproj"
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
        Assert.All(
            projectPaths,
            path => Assert.True(
                File.Exists(RepositoryPath(path)),
                $"Active solution project '{path}' does not exist relative to the repository root."));
    }

    [Fact]
    public void ActiveQualityWorkflow_EnforcesTheCleanProductGate()
    {
        string workflow = File.ReadAllText(RepositoryPath(".github", "workflows", "quality.yml"));
        string[] requiredTokens =
        [
            "permissions:",
            "contents: read",
            "actions/checkout@v6",
            "actions/setup-dotnet@v5",
            "dotnet-version: 8.0.x",
            "--locked-mode",
            "NuGetAudit=true",
            "dotnet format Convergence.sln --no-restore --verify-no-changes",
            "-p:TreatWarningsAsErrors=true",
            "-p:ContinuousIntegrationBuild=true",
            "--filter FullyQualifiedName~Convergence.Framework.Tests.Architecture",
            "dotnet test Convergence.sln",
            "XPlat Code Coverage",
            "Assert-CoberturaCoverage.ps1",
            "Convergence.ContentValidator.csproj",
            "--schema-root schemas/content/v6",
            "--clean-battle-demo",
            "--clean-field-demo",
            "--clean-save-demo",
            "--clean-training-annex-demo",
            "--clean-training-annex-play",
            "Godot_v4.7.1-stable_mono_linux_x86_64.zip",
            "6ca7ff0459f1b806900be683c1b0837c607a9c16834c530dc68c81b9fc3ae1f6",
            "--convergence-smoke",
            "EnableTrimAnalyzer=true"
        ];

        foreach (string token in requiredTokens)
        {
            Assert.Contains(token, workflow, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ArchiveDocs", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("continue-on-error: true", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("schemas/content/v4", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveDocumentationLinks_ResolveInsideTheProductBoundary()
    {
        string root = RepositoryRoot();
        string archiveRoot = Path.GetFullPath(Path.Combine(root, "ArchiveDocs")) + Path.DirectorySeparatorChar;
        string[] documents = Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var failures = new List<string>();
        var linkPattern = new Regex(
            @"!?\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)",
            RegexOptions.CultureInvariant);

        foreach (string document in documents)
        {
            string markdown = File.ReadAllText(document);
            foreach (Match match in linkPattern.Matches(markdown))
            {
                string target = match.Groups["target"].Value.Trim('<', '>');
                if (target.Length == 0 || target.StartsWith('#') ||
                    Uri.TryCreate(target, UriKind.Absolute, out _))
                {
                    continue;
                }

                string pathPart = Uri.UnescapeDataString(target.Split('#', '?')[0]);
                if (pathPart.Length == 0)
                {
                    continue;
                }

                string resolved = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(document)!,
                    pathPart.Replace('/', Path.DirectorySeparatorChar)));
                string location = Path.GetRelativePath(root, document).Replace('\\', '/');
                if (resolved.StartsWith(archiveRoot, StringComparisonForPaths()))
                {
                    failures.Add($"{location}: active link enters ArchiveDocs: {target}");
                }
                else if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    failures.Add($"{location}: missing local link target: {target}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void SecurityPolicy_DefinesPrivateReportingAndSupportedScope()
    {
        string policy = File.ReadAllText(RepositoryPath("SECURITY.md"));

        Assert.Contains("Report a vulnerability", policy, StringComparison.Ordinal);
        Assert.Contains("private", policy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0.1", policy, StringComparison.Ordinal);
        Assert.Contains("ArchiveDocs", policy, StringComparison.Ordinal);
        Assert.Contains("90 days", policy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActiveProductRoot_IsTheGitRepositoryRootWhenGitMetadataIsPresent()
    {
        string productRoot = RepositoryRoot();
        string? gitRoot = FindAncestorContaining(productRoot, ".git");

        if (gitRoot is not null)
        {
            Assert.Equal(
                Path.GetFullPath(gitRoot),
                Path.GetFullPath(productRoot),
                ignoreCase: OperatingSystem.IsWindows());
        }

        Assert.True(File.Exists(Path.Combine(productRoot, "README.md")));
        string readme = File.ReadAllText(Path.Combine(productRoot, "README.md"));
        string? firstHeading = readme
            .ReplaceLineEndings("\n")
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        int logoIndex = readme.IndexOf(
            "docs/assets/Convergence_Logo.png",
            StringComparison.Ordinal);
        int headingIndex = readme.IndexOf(
            "# Convergence Framework",
            StringComparison.Ordinal);

        Assert.Equal("# Convergence Framework", firstHeading);
        Assert.True(logoIndex >= 0, "The root README must display the Convergence logo.");
        Assert.True(
            logoIndex < headingIndex,
            "The Convergence logo must appear before the product heading.");
        Assert.True(File.Exists(Path.Combine(productRoot, "global.json")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "src")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "samples")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "tests")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "content")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "config")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "docs")));
        Assert.True(Directory.Exists(Path.Combine(productRoot, "tools")));

        Assert.False(File.Exists(Path.Combine(productRoot, "JRPG.sln")));
        Assert.False(File.Exists(Path.Combine(productRoot, "JRPG", "Convergence.sln")));
        Assert.False(File.Exists(Path.Combine(productRoot, "JRPG", "README.md")));
        Assert.True(File.Exists(Path.Combine(
            productRoot,
            "ArchiveDocs",
            "LegacyRepository",
            "JRPG.sln")));
        Assert.True(File.Exists(Path.Combine(
            productRoot,
            "ArchiveDocs",
            "LegacyRepository",
            "README-retired.md")));
    }

    [Fact]
    public void PublicLicensing_DefinesNoncommercialScopesAndProtectsArchiveOwnership()
    {
        string overview = File.ReadAllText(RepositoryPath("LICENSE.md"));
        string notice = File.ReadAllText(RepositoryPath("NOTICE.md"));
        string softwareLicense = File.ReadAllText(RepositoryPath(
            "LICENSES",
            "PolyForm-Noncommercial-1.0.0.md"));
        string contentLicense = File.ReadAllText(RepositoryPath(
            "LICENSES",
            "CC-BY-NC-SA-4.0.md"));
        string contributing = File.ReadAllText(RepositoryPath("CONTRIBUTING.md"));
        string contributorAgreement = File.ReadAllText(RepositoryPath("CONTRIBUTOR-AGREEMENT.md"));
        string vision = File.ReadAllText(RepositoryPath("docs", "project-vision.md"));

        Assert.Contains("Copyright (c) 2026 Kayel Calleja", overview, StringComparison.Ordinal);
        Assert.Contains("PolyForm Noncommercial License 1.0.0", softwareLicense, StringComparison.Ordinal);
        Assert.Contains("Attribution-NonCommercial-ShareAlike 4.0 International", contentLicense, StringComparison.Ordinal);
        Assert.StartsWith("Required Notice:", notice, StringComparison.Ordinal);
        Assert.Contains("`ArchiveDocs/` is historical evidence and is not licensed", overview, StringComparison.Ordinal);
        Assert.Contains("Commercial use", overview, StringComparison.Ordinal);
        Assert.Contains("External pull requests are not accepted by default", contributing, StringComparison.Ordinal);
        Assert.Contains("Copyright Assignment Agreement", contributorAgreement, StringComparison.Ordinal);
        Assert.Contains("source-available", vision, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("open-source framework", vision, StringComparison.OrdinalIgnoreCase);
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
    public void ActiveContractAssets_AreAlwaysRefreshedInBuildOutputs()
    {
        string[] projects =
        [
            "samples/Convergence.DemoHost/Convergence.DemoHost.csproj",
            "tests/Convergence.DemoHost.Tests/Convergence.DemoHost.Tests.csproj",
            "tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj"
        ];

        foreach (string project in projects)
        {
            XDocument document = XDocument.Load(RepositoryPath(project));
            XElement[] contractAssets = document
                .Descendants("Content")
                .Where(element =>
                    element.Attribute("Include")?.Value.Contains(".json", StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();

            Assert.NotEmpty(contractAssets);
            Assert.All(
                contractAssets,
                asset => Assert.Equal("Always", asset.Element("CopyToOutputDirectory")?.Value));
        }
    }

    [Fact]
    public void ActiveProductionSourcesAndProjects_DoNotReferenceTheLegacyArchive()
    {
        string[] roots = [RepositoryPath("src"), RepositoryPath("samples"), RepositoryPath("tools")];
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

    private static string RepositoryPath(params string[] segments) =>
        Path.Combine([RepositoryRoot(), .. segments]);

    private static string? FindAncestorContaining(string start, string entryName)
    {
        string? current = Path.GetFullPath(start);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, entryName)) ||
                File.Exists(Path.Combine(current, entryName)))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static StringComparison StringComparisonForPaths() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
