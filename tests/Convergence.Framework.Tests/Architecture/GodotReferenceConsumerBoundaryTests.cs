using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class GodotReferenceConsumerBoundaryTests
{
    [Fact]
    public void GodotConsumer_PinsTheSupportedSdkAndReferencesOnlyFramework()
    {
        XDocument project = XDocument.Load(ProjectPath());

        Assert.Equal("Godot.NET.Sdk/4.7.1", project.Root?.Attribute("Sdk")?.Value);
        Assert.Equal("net8.0", RequiredProperty(project, "TargetFramework"));
        Assert.Equal("12.0", RequiredProperty(project, "LangVersion"));
        Assert.Equal("false", RequiredProperty(project, "IsPackable"));
        Assert.Equal("true", RequiredProperty(project, "RestorePackagesWithLockFile"));
        Assert.Empty(project.Descendants("PackageReference"));

        XElement reference = Assert.Single(project.Descendants("ProjectReference"));
        Assert.Equal(
            "../../src/convergence.framework/convergence.framework.csproj",
            NormalizePath(reference.Attribute("Include")?.Value));

        using JsonDocument lockFile = JsonDocument.Parse(File.ReadAllText(
            RepositoryPath("samples", "Convergence.GodotHost", "packages.lock.json")));
        JsonElement dependencies = lockFile.RootElement.GetProperty("dependencies").GetProperty("net8.0");
        Assert.Equal("4.7.1", dependencies.GetProperty("GodotSharp").GetProperty("resolved").GetString());
        Assert.Equal("4.7.1", dependencies.GetProperty("Godot.SourceGenerators").GetProperty("resolved").GetString());
        Assert.Equal("4.7.1", dependencies.GetProperty("GodotSharpEditor").GetProperty("resolved").GetString());
    }

    [Fact]
    public void GodotConsumer_UsesCanonicalGeneratedContentWithoutMaintainingADuplicatePack()
    {
        XDocument project = XDocument.Load(ProjectPath());
        XElement content = Assert.Single(project.Descendants("TrainingAnnexContent"));
        Assert.Equal(
            "../../content/original/training-annex/*.json",
            NormalizePath(content.Attribute("Include")?.Value));
        Assert.Contains(project.Descendants("Target"), target =>
            target.Attribute("Name")?.Value == "PrepareGodotContent" &&
            target.Attribute("BeforeTargets")?.Value == "BeforeBuild");

        string ignore = File.ReadAllText(RepositoryPath("samples", "Convergence.GodotHost", ".gitignore"));
        Assert.Contains("Content/", ignore, StringComparison.Ordinal);
        Assert.Contains(".godot/", ignore, StringComparison.Ordinal);
        Assert.False(Directory.Exists(RepositoryPath("samples", "Convergence.GodotHost", "Content", ".git")));
    }

    [Fact]
    public void GodotConsumer_ExercisesTheExistingHostNeutralCompositionSurface()
    {
        string rootSource = File.ReadAllText(RepositoryPath(
            "samples",
            "Convergence.GodotHost",
            "Scripts",
            "ConvergenceSmokeRoot.cs"));
        string contentSource = File.ReadAllText(RepositoryPath(
            "samples",
            "Convergence.GodotHost",
            "Infrastructure",
            "GodotResourceContentSource.cs"));
        string ports = File.ReadAllText(RepositoryPath(
            "samples",
            "Convergence.GodotHost",
            "Infrastructure",
            "GodotHostPorts.cs"));
        string save = File.ReadAllText(RepositoryPath(
            "samples",
            "Convergence.GodotHost",
            "Infrastructure",
            "GodotSaveCodec.cs"));

        string[] rootTokens =
        [
            "SkillSystemCatalogLoader",
            "CatalogBattleActorFactory",
            "BattleActionExecutor",
            "BattleEncounterRunner",
            "RuntimeRulesetPolicyFactoryRegistry.CreateStandard()",
            "CONVERGENCE_GODOT_SMOKE_OK"
        ];
        Assert.All(rootTokens, token => Assert.Contains(token, rootSource, StringComparison.Ordinal));
        Assert.Contains("Godot.FileAccess", contentSource, StringComparison.Ordinal);
        Assert.Contains("res://", contentSource, StringComparison.Ordinal);
        Assert.Contains("IContentPackTextSource", contentSource, StringComparison.Ordinal);
        Assert.Contains("IHostCommandSource<TCommand>", ports, StringComparison.Ordinal);
        Assert.Contains("IBattleEncounterEventSink", ports, StringComparison.Ordinal);
        Assert.Contains("Dictionary<RuntimeInstanceId, Node>", ports, StringComparison.Ordinal);
        Assert.Contains("System.Text.Json", save, StringComparison.Ordinal);
        Assert.Contains("RuntimeSaveValidator", save, StringComparison.Ordinal);
        Assert.DoesNotContain("Convergence.DemoHost", rootSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GodotProject_StartsTheHeadlessSmokeScene()
    {
        string project = File.ReadAllText(RepositoryPath("samples", "Convergence.GodotHost", "project.godot"));
        string scene = File.ReadAllText(RepositoryPath("samples", "Convergence.GodotHost", "Scenes", "Main.tscn"));

        Assert.Contains("run/main_scene=\"res://Scenes/Main.tscn\"", project, StringComparison.Ordinal);
        Assert.Contains("project/assembly_name=\"Convergence.GodotHost\"", project, StringComparison.Ordinal);
        Assert.Contains("res://Scripts/ConvergenceSmokeRoot.cs", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ActorScenes\"", scene, StringComparison.Ordinal);
    }

    private static string ProjectPath() => RepositoryPath(
        "samples",
        "Convergence.GodotHost",
        "Convergence.GodotHost.csproj");

    private static string RequiredProperty(XDocument project, string name) =>
        project.Descendants(name).Single().Value;

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();

    private static string RepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Convergence.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
    }

    private static string RepositoryPath(params string[] segments) =>
        Path.Combine([RepositoryRoot(), .. segments]);
}
