using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using Xunit;

namespace Convergence.Tests.Architecture;

public sealed class FrameworkBoundaryTests
{
    private static readonly string[] ForbiddenPublicTypeFragments =
    [
        "System.Console",
        "System.IO.File",
        "System.IO.Directory",
        "System.Text.Json",
        "Newtonsoft",
        "Godot",
        "JRPGPrototype.Data.Database",
        "JRPGPrototype.Data.SkillData",
        "JRPGPrototype.Data.PersonaData",
        "JRPGPrototype.Data.ItemData",
        "JRPGPrototype.Entities.Combatant",
        "JRPGPrototype.Entities.Persona",
        "JRPGPrototype.Services.IGameIO"
    ];

    [Fact]
    public void FrameworkAssembly_HasNoConsoleHostDependencyOrExternalPackageReference()
    {
        Assembly framework = typeof(ContentId).Assembly;

        Assert.Equal("Convergence.Framework", framework.GetName().Name);
        Assert.DoesNotContain(
            framework.GetReferencedAssemblies(),
            reference => reference.Name == "JRPG.ConsoleHost");

        string project = File.ReadAllText(RepositoryPath("src", "Convergence.Framework", "Convergence.Framework.csproj"));
        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("JRPG.ConsoleHost", project, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryProjects_TargetTheGodotCompatibleDotNet8AndCSharp12Baseline()
    {
        string[] projects =
        [
            RepositoryPath("src", "Convergence.Framework", "Convergence.Framework.csproj"),
            RepositoryPath("samples", "Convergence.DemoHost", "Convergence.DemoHost.csproj"),
            RepositoryPath("JRPG.ConsoleHost.csproj"),
            RepositoryPath("Convergence.Tests", "Convergence.Tests.csproj")
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
        XDocument tests = XDocument.Load(RepositoryPath("Convergence.Tests", "Convergence.Tests.csproj"));

        Assert.Equal("false", RequiredProperty(framework, "IsPackable"));
        Assert.Contains(
            demoHost.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value) == "../../src/convergence.framework/convergence.framework.csproj");
        Assert.Contains(
            tests.Descendants("ProjectReference"),
            reference => NormalizePath(reference.Attribute("Include")?.Value) == "../src/convergence.framework/convergence.framework.csproj");
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
        string[] forbidden = ["Console.", "File.", "Directory.", "Thread.Sleep", "Newtonsoft", "Godot"];

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
        string fusionRoot = RepositoryPath("src", "Convergence.Framework", "Logic", "Fusion");
        string[] legacyStrategyTokens =
        [
            "mitama",
            "ara_mitama",
            "nigi_mitama",
            "kusi_mitama",
            "saki_mitama",
            "MoonPhase",
            "Full Moon"
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
        string fusionRoot = RepositoryPath("src", "Convergence.Framework", "Logic", "Fusion");
        string[] files =
        [
            Path.Combine(fusionRoot, "FusionRuntimeServices.cs"),
            Path.Combine(fusionRoot, "CompendiumRuntimeServices.cs"),
            Path.Combine(fusionRoot, "CompendiumRecallPricingPolicies.cs")
        ];

        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain("Macca", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertAllowed(Type type)
    {
        foreach (Type candidate in Expand(type))
        {
            string identity = candidate.FullName ?? candidate.Name;
            Assert.DoesNotContain(
                ForbiddenPublicTypeFragments,
                forbidden => identity.Contains(forbidden, StringComparison.Ordinal));
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
