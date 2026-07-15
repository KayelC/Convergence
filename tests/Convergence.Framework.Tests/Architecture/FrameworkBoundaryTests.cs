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
    public void FrameworkAssembly_HasNoConsoleHostDependencyOrExternalPackageReference()
    {
        Assembly framework = typeof(ContentId).Assembly;

        Assert.Equal("Convergence.Framework", framework.GetName().Name);
        Assert.All(
            framework.GetExportedTypes(),
            type => Assert.StartsWith("Convergence.", type.Namespace, StringComparison.Ordinal));

        string project = File.ReadAllText(RepositoryPath("src", "Convergence.Framework", "Convergence.Framework.csproj"));
        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryProjects_TargetTheGodotCompatibleDotNet8AndCSharp12Baseline()
    {
        string[] projects =
        [
            RepositoryPath("src", "Convergence.Framework", "Convergence.Framework.csproj"),
            RepositoryPath("samples", "Convergence.DemoHost", "Convergence.DemoHost.csproj"),
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
        Assert.Empty(framework.Descendants("PackageReference"));
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

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
}
