using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Convergence.Content;
using Convergence.Hosting;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class FrameworkBoundaryTests
{
    [Fact]
    public void FrameworkAssembly_HasNoConsoleHostOrRuntimePackageDependency()
    {
        Assembly framework = typeof(ContentId).Assembly;

        Assert.Equal("Convergence.Framework", framework.GetName().Name);
        Assert.All(
            framework.GetExportedTypes(),
            type => Assert.StartsWith("Convergence.", type.Namespace, StringComparison.Ordinal));

        Assert.All(
            framework.GetReferencedAssemblies(),
            reference => Assert.True(
                reference.Name?.StartsWith("System", StringComparison.Ordinal) == true ||
                reference.Name == "netstandard",
                $"Unexpected Framework assembly dependency '{reference.FullName}'."));

        XDocument project = XDocument.Load(RepositoryPath("src", "Convergence.Framework", "Convergence.Framework.csproj"));
        Assert.Empty(project.Descendants("ProjectReference"));
        AssertPrivateBuildPackages(project);
    }

    [Fact]
    public void RepositoryProjects_TargetTheGodotCompatibleDotNet8AndCSharp12Baseline()
    {
        string[] projects =
        [
            RepositoryPath("src", "Convergence.Framework", "Convergence.Framework.csproj"),
            RepositoryPath("samples", "Convergence.DemoHost", "Convergence.DemoHost.csproj"),
            RepositoryPath("samples", "Convergence.GodotHost", "Convergence.GodotHost.csproj"),
            RepositoryPath("tests", "Convergence.Framework.Tests", "Convergence.Framework.Tests.csproj"),
            RepositoryPath("tests", "Convergence.DemoHost.Tests", "Convergence.DemoHost.Tests.csproj")
        ];

        foreach (string projectPath in projects)
        {
            XDocument project = XDocument.Load(projectPath);
            Assert.Equal("net8.0", RequiredProperty(project, "TargetFramework"));
            Assert.Equal("12.0", RequiredProperty(project, "LangVersion"));
        }

        XDocument framework = XDocument.Load(projects[0]);
        Assert.Equal("false", RequiredProperty(framework, "IsPackable"));
        AssertPrivateBuildPackages(framework);
    }

    [Fact]
    public void SolutionReleaseConfiguration_DoesNotBuildAnyProjectAsDebug()
    {
        string solution = File.ReadAllText(RepositoryPath("Convergence.sln"));

        Assert.DoesNotContain(
            ".Release|Any CPU.ActiveCfg = Debug|Any CPU",
            solution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".Release|Any CPU.Build.0 = Debug|Any CPU",
            solution,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RepositorySdkPolicy_SelectsTheDotNet8LineWithControlledRollForward()
    {
        using JsonDocument policy = JsonDocument.Parse(File.ReadAllText(RepositoryPath("global.json")));
        JsonElement sdk = policy.RootElement.GetProperty("sdk");

        Assert.StartsWith("8.0.", sdk.GetProperty("version").GetString(), StringComparison.Ordinal);
        Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void FrameworkDistribution_IsSourceProjectReferenceRatherThanPackagePublication()
    {
        XDocument framework = XDocument.Load(RepositoryPath("src", "Convergence.Framework", "Convergence.Framework.csproj"));
        XDocument demoHost = XDocument.Load(RepositoryPath("samples", "Convergence.DemoHost", "Convergence.DemoHost.csproj"));
        XDocument godotHost = XDocument.Load(RepositoryPath("samples", "Convergence.GodotHost", "Convergence.GodotHost.csproj"));
        XDocument frameworkTests = XDocument.Load(RepositoryPath(
            "tests",
            "Convergence.Framework.Tests",
            "Convergence.Framework.Tests.csproj"));
        XDocument demoTests = XDocument.Load(RepositoryPath(
            "tests",
            "Convergence.DemoHost.Tests",
            "Convergence.DemoHost.Tests.csproj"));

        Assert.Equal("false", RequiredProperty(framework, "IsPackable"));
        Assert.Contains(
            demoHost.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value) == "../../src/convergence.framework/convergence.framework.csproj");
        Assert.Contains(
            godotHost.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value) == "../../src/convergence.framework/convergence.framework.csproj");
        Assert.Single(godotHost.Descendants("ProjectReference"));
        Assert.Contains(
            frameworkTests.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value) == "../../src/convergence.framework/convergence.framework.csproj");
        Assert.Single(frameworkTests.Descendants("ProjectReference"));
        Assert.Contains(
            demoTests.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value) == "../../src/convergence.framework/convergence.framework.csproj");
        Assert.Contains(
            demoTests.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value) == "../../samples/convergence.demohost/convergence.demohost.csproj");
        Assert.Equal(2, demoTests.Descendants("ProjectReference").Count());
    }

    [Fact]
    public void FrameworkPublicApi_ExposesNoHostSerializerEngineOrLegacyTypes()
    {
        Assembly framework = typeof(IContentPackTextSource).Assembly;
        foreach (Type type in framework.GetExportedTypes())
        {
            AssertAllowed(type);
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                switch (member)
                {
                    case MethodInfo method:
                        AssertAllowed(method.ReturnType);
                        foreach (ParameterInfo parameter in method.GetParameters()) AssertAllowed(parameter.ParameterType);
                        break;
                    case PropertyInfo property:
                        AssertAllowed(property.PropertyType);
                        break;
                    case FieldInfo field:
                        AssertAllowed(field.FieldType);
                        break;
                    case EventInfo eventInfo when eventInfo.EventHandlerType is not null:
                        AssertAllowed(eventInfo.EventHandlerType);
                        break;
                }
            }
        }
    }

    [Fact]
    public void FrameworkSources_DoNotUseConsoleFilesystemSleepingOrNewtonsoft()
    {
        string frameworkRoot = RepositoryPath("src", "Convergence.Framework");
        string[] forbidden =
        [
            "Console.",
            "File.",
            "Directory.",
            "Thread.Sleep",
            "Newtonsoft",
            "Godot",
            "Legacy"
        ];

        foreach (string file in Directory.EnumerateFiles(frameworkRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            foreach (string token in forbidden)
            {
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void FrameworkRandomConsumers_UseTheSharedHostOutputGuard()
    {
        string frameworkRoot = RepositoryPath("src", "Convergence.Framework");
        string guardPath = NormalizePath(Path.Combine("Internal", "RandomSourceContract.cs"));

        foreach (string file in Directory.EnumerateFiles(frameworkRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string relativePath = NormalizePath(Path.GetRelativePath(frameworkRoot, file));
            if (relativePath == guardPath)
            {
                continue;
            }

            string source = File.ReadAllText(file)
                .Replace("RandomSourceContract.NextInt32(", string.Empty, StringComparison.Ordinal)
                .Replace("RandomSourceContract.NextUnitDecimal(", string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(".NextInt32(", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".NextUnitDecimal(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FrameworkPublicApi_IsVersionedDocumentedAndBaselineGuarded()
    {
        Assembly framework = typeof(ContentId).Assembly;
        Assert.Equal(new Version(0, 1, 0, 0), framework.GetName().Version);
        Assert.DoesNotContain(
            framework.GetExportedTypes(),
            type => type.Namespace is "Convergence.Internal" or "Convergence.Serialization");

        string projectRoot = RepositoryPath("src", "Convergence.Framework");
        XDocument project = XDocument.Load(Path.Combine(projectRoot, "Convergence.Framework.csproj"));
        Assert.Equal("0.1.0", RequiredProperty(project, "Version"));
        Assert.Equal("true", RequiredProperty(project, "GenerateDocumentationFile"));
        Assert.Equal("true", RequiredProperty(project, "CodeAnalysisTreatWarningsAsErrors"));
        Assert.Equal("true", RequiredProperty(project, "IsTrimmable"));
        Assert.Equal("true", RequiredProperty(project, "EnableTrimAnalyzer"));

        string[] shipped = File.ReadAllLines(Path.Combine(projectRoot, "PublicAPI.Shipped.txt"));
        Assert.Equal("#nullable enable", shipped[0]);
        Assert.True(shipped.Length > 1_000);
        Assert.Equal(shipped.Length, shipped.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(shipped, line => line.Contains("Convergence.Content.ContentId", StringComparison.Ordinal));
        Assert.Equal(
            ["#nullable enable"],
            File.ReadAllLines(Path.Combine(projectRoot, "PublicAPI.Unshipped.txt")));

        string documentationPath = Path.Combine(AppContext.BaseDirectory, "Convergence.Framework.xml");
        XDocument documentation = XDocument.Load(documentationPath);
        HashSet<string> documentedMembers = documentation
            .Descendants("member")
            .Select(member => member.Attribute("name")?.Value ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("T:Convergence.Content.ContentId", documentedMembers);
        Assert.Contains("T:Convergence.Catalog.SkillSystemCatalogLoader", documentedMembers);
        Assert.Contains("T:Convergence.Encounters.BattleEncounterRunner", documentedMembers);
        Assert.Contains("T:Convergence.Runtime.RuntimeSessionRestoreService", documentedMembers);

        using JsonDocument lockFile = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, "packages.lock.json")));
        JsonElement dependencies = lockFile.RootElement.GetProperty("dependencies").GetProperty("net8.0");
        Assert.Equal(
            "5.6.0",
            dependencies.GetProperty("Microsoft.CodeAnalysis.PublicApiAnalyzers").GetProperty("resolved").GetString());
        Assert.Equal(
            "4.12.0",
            dependencies.GetProperty("Microsoft.Net.Compilers.Toolset").GetProperty("resolved").GetString());
        Assert.Equal(
            "8.0.28",
            dependencies.GetProperty("Microsoft.NET.ILLink.Tasks").GetProperty("resolved").GetString());
    }

    [Fact]
    public void FrameworkFusionSources_DoNotEncodeLegacyCatalystOrMoonPhaseStrategies()
    {
        string fusionRoot = RepositoryPath("src", "Convergence.Framework", "Fusion");
        string retiredCatalystFamily = string.Concat("mita", "ma");
        string[] legacyStrategyTokens =
        [
            retiredCatalystFamily,
            $"ara_{retiredCatalystFamily}",
            $"nigi_{retiredCatalystFamily}",
            $"kusi_{retiredCatalystFamily}",
            $"saki_{retiredCatalystFamily}",
            "MoonPhase",
            string.Concat("Full", " ", "Moon")
        ];

        foreach (string file in Directory.EnumerateFiles(fusionRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            foreach (string token in legacyStrategyTokens)
            {
                Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void FrameworkCompendiumSources_DoNotOwnHostCurrencyTerminology()
    {
        string fusionRoot = RepositoryPath("src", "Convergence.Framework", "Fusion");
        string[] files =
        [
            Path.Combine(fusionRoot, "FusionRuntimeServices.cs"),
            Path.Combine(fusionRoot, "CompendiumRuntimeServices.cs"),
            Path.Combine(fusionRoot, "CompendiumRecallPricingPolicies.cs")
        ];

        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain(string.Concat("Mac", "ca"), source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertAllowed(Type type)
    {
        foreach (Type candidate in Expand(type))
        {
            string? candidateNamespace = candidate.Namespace;
            Assert.True(
                candidate.Assembly == typeof(ContentId).Assembly ||
                candidateNamespace?.StartsWith("System", StringComparison.Ordinal) == true,
                $"Public API exposes non-framework type '{candidate.FullName ?? candidate.Name}'.");
        }
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is Type element)
        {
            foreach (Type nested in Expand(element)) yield return nested;
        }
        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Expand(argument)) yield return nested;
        }
    }

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

    private static string RequiredProperty(XDocument project, string name)
    {
        XElement? property = project.Descendants(name).SingleOrDefault();
        Assert.NotNull(property);
        return property!.Value.Trim();
    }

    private static void AssertPrivateBuildPackages(XDocument project)
    {
        XElement[] packages = project.Descendants("PackageReference").ToArray();
        Assert.Equal(
            ["Microsoft.CodeAnalysis.PublicApiAnalyzers", "Microsoft.NET.ILLink.Tasks", "Microsoft.Net.Compilers.Toolset"],
            packages
                .Select(package => package.Attribute("Include")?.Value ?? string.Empty)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.All(packages, package => Assert.Equal("all", package.Element("PrivateAssets")?.Value));
    }

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
}
