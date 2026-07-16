using System.Text.Json.Nodes;
using Xunit;

namespace Convergence.ContentValidator.Tests;

public sealed class ContentValidatorApplicationTests
{
    [Fact]
    public void ActiveContent_PassesEveryAuthoringAndCatalogLayer()
    {
        InvocationResult result = Invoke(ContentRoot());

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("Validated 6 pack(s), 36 document(s)", result.Output, StringComparison.Ordinal);
        Assert.Contains("Schema, deserialization, semantic, dependency, registration, and catalog checks passed.",
            result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownContentField_FailsSchemaBeforeCatalogConstruction()
    {
        using TemporaryDirectory temporary = CopyTrainingAnnex();
        string skillsPath = Path.Combine(temporary.Path, "training_annex_slice.skills.json");
        JsonObject skills = JsonNode.Parse(File.ReadAllText(skillsPath))!.AsObject();
        skills["unexpected"] = true;
        File.WriteAllText(skillsPath, skills.ToJsonString());

        InvocationResult result = Invoke(temporary.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("[schema_invalid]", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("[catalog_", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingHostRegistration_FailsSemanticValidation()
    {
        using TemporaryDirectory temporary = CopyTrainingAnnex();
        using TemporaryFile registrationsFile = new();
        string registrationsPath = registrationsFile.Path;
        JsonObject registrations = JsonNode.Parse(File.ReadAllText(RegistrationsPath()))!.AsObject();
        JsonArray categories = registrations["registrations"]!["shopCategories"]!.AsArray();
        int trainingSupplyIndex = categories
            .Select((node, index) => new { Value = node!.GetValue<string>(), Index = index })
            .Single(item => item.Value == "training_supply")
            .Index;
        categories.RemoveAt(trainingSupplyIndex);
        File.WriteAllText(registrationsPath, registrations.ToJsonString());

        InvocationResult result = Invoke(temporary.Path, registrationsPath);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("[catalog_content_validation_failed]", result.Error, StringComparison.Ordinal);
        Assert.Contains("training_supply", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidButUnownedDocument_IsRejectedAsOrphaned()
    {
        using TemporaryDirectory temporary = CopyTrainingAnnex();
        File.Copy(
            Path.Combine(temporary.Path, "training_annex_slice.items.json"),
            Path.Combine(temporary.Path, "unowned.items.json"));

        InvocationResult result = Invoke(temporary.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("[document_orphaned] unowned.items.json", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingManifestDocument_IsReportedWithoutEscapingThroughIo()
    {
        using TemporaryDirectory temporary = CopyTrainingAnnex();
        File.Delete(Path.Combine(temporary.Path, "training_annex_slice.items.json"));

        InvocationResult result = Invoke(temporary.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("[document_missing]", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageAndInvalidArguments_HaveStableExitCodes()
    {
        var helpOutput = new StringWriter();
        var helpError = new StringWriter();
        int help = global::Convergence.ContentValidator.ContentValidatorApplication.Run(
            ["--help"],
            helpOutput,
            helpError);

        var invalidOutput = new StringWriter();
        var invalidError = new StringWriter();
        int invalid = global::Convergence.ContentValidator.ContentValidatorApplication.Run(
            ["--content-root"],
            invalidOutput,
            invalidError);

        Assert.Equal(0, help);
        Assert.Contains("Usage:", helpOutput.ToString(), StringComparison.Ordinal);
        Assert.Empty(helpError.ToString());
        Assert.Equal(2, invalid);
        Assert.Empty(invalidOutput.ToString());
        Assert.Contains("Unknown or incomplete option", invalidError.ToString(), StringComparison.Ordinal);
        Assert.Contains("Usage:", invalidError.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedRegistrationProfile_ReturnsConfigurationDiagnosticInsteadOfThrowing()
    {
        using TemporaryFile registrationsFile = new();
        File.WriteAllText(registrationsFile.Path, "{");

        InvocationResult result = Invoke(ContentRoot(), registrationsFile.Path);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("[configuration_invalid]", result.Error, StringComparison.Ordinal);
    }

    private static InvocationResult Invoke(string contentRoot, string? registrationsPath = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = global::Convergence.ContentValidator.ContentValidatorApplication.Run(
            [
                "--content-root", contentRoot,
                "--schema-root", SchemaRoot(),
                "--registrations", registrationsPath ?? RegistrationsPath()
            ],
            output,
            error);
        return new InvocationResult(exitCode, output.ToString(), error.ToString());
    }

    private static TemporaryDirectory CopyTrainingAnnex()
    {
        var temporary = new TemporaryDirectory();
        foreach (string source in Directory.GetFiles(
                     Path.Combine(ContentRoot(), "original", "training-annex"),
                     "*.json"))
        {
            File.Copy(source, Path.Combine(temporary.Path, Path.GetFileName(source)));
        }

        return temporary;
    }

    private static string ContentRoot() => RepositoryPath("content");

    private static string SchemaRoot() => RepositoryPath("schemas", "content", "v3");

    private static string RegistrationsPath() =>
        RepositoryPath("config", "content-validator", "active-samples.registrations.json");

    private static string RepositoryPath(params string[] segments) =>
        Path.Combine([RepositoryRoot(), .. segments]);

    private static string RepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Convergence.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Convergence.sln.");
    }

    private sealed record InvocationResult(int ExitCode, string Output, string Error);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"convergence-validator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"convergence-registrations-{Guid.NewGuid():N}.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
