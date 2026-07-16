using Convergence.Catalog;
using Convergence.Content;
using Convergence.Validation;

namespace Convergence.ContentValidator;

internal static class ContentValidatorApplication
{
    private const string ManifestSchemaId = "urn:convergence:schema:content:v3:manifest";

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParseOptions(args, out ContentValidatorOptions? options, out string? optionError))
        {
            if (optionError is not null)
            {
                error.WriteLine(optionError);
            }
            WriteUsage(optionError is null ? output : error);
            return optionError is null ? 0 : 2;
        }

        try
        {
            return Validate(options!, output, error);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                ArgumentException or
                System.Text.Json.JsonException)
        {
            error.WriteLine($"[configuration_invalid] {exception.Message}");
            return 2;
        }
    }

    private static int Validate(ContentValidatorOptions options, TextWriter output, TextWriter error)
    {
        string contentRoot = Path.GetFullPath(options.ContentRoot);
        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Content root does not exist: {contentRoot}");
        }

        SchemaDocumentValidator schemaValidator = SchemaDocumentValidator.Load(options.SchemaRoot);
        SkillSystemRegistrationSnapshot registrations = RegistrationConfiguration.Load(options.RegistrationsPath);
        string[] contentFiles = Directory.GetFiles(contentRoot, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var diagnostics = new List<ValidatorDiagnostic>();
        var textByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var manifestPaths = new List<string>();
        foreach (string path in contentFiles)
        {
            string sourceName = RelativeName(contentRoot, path);
            string text = File.ReadAllText(path);
            textByPath.Add(Path.GetFullPath(path), text);
            diagnostics.AddRange(schemaValidator.Validate(sourceName, text));
            if (DeclaresManifestSchema(text))
            {
                manifestPaths.Add(Path.GetFullPath(path));
            }
        }

        if (diagnostics.Count != 0)
        {
            WriteDiagnostics(diagnostics, error);
            return 1;
        }

        IReadOnlyList<ContentPackTextBundle> bundles = BuildBundles(
            contentRoot,
            contentFiles,
            manifestPaths,
            textByPath,
            diagnostics);
        if (diagnostics.Count != 0)
        {
            WriteDiagnostics(diagnostics, error);
            return 1;
        }

        CatalogLoadResult load = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(registrations, bundles));
        if (!load.IsSuccess)
        {
            foreach (CatalogLoadDiagnostic diagnostic in load.Diagnostics)
            {
                error.WriteLine(
                    $"[catalog_{ToSnakeCase(diagnostic.Code.ToString())}] " +
                    $"{diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}");
            }
            return 1;
        }

        GameDataCatalog catalog = load.RequireCatalog();
        int definitionCount =
            catalog.Skills.Count +
            catalog.Entities.Count +
            catalog.Races.Count +
            catalog.Ailments.Count +
            catalog.Items.Count +
            catalog.Equipment.Count +
            catalog.Shops.Count +
            catalog.Negotiations.Count +
            catalog.Encounters.Count +
            catalog.Dungeons.Count +
            catalog.FusionRecipes.Count +
            catalog.Rulesets.Count;
        output.WriteLine(
            $"Validated {catalog.ContentPacks.Count} pack(s), {contentFiles.Length} document(s), " +
            $"and {definitionCount} qualified definition(s). Schema, deserialization, semantic, " +
            "dependency, registration, and catalog checks passed.");
        return 0;
    }

    private static IReadOnlyList<ContentPackTextBundle> BuildBundles(
        string contentRoot,
        IReadOnlyList<string> contentFiles,
        IReadOnlyList<string> manifestPaths,
        IReadOnlyDictionary<string, string> textByPath,
        ICollection<ValidatorDiagnostic> diagnostics)
    {
        var bundles = new List<ContentPackTextBundle>();
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ISkillSystemDocumentDeserializer deserializer = new SkillSystemJsonDeserializer();

        foreach (string manifestPath in manifestPaths.Order(StringComparer.Ordinal))
        {
            string manifestSource = RelativeName(contentRoot, manifestPath);
            owners.Add(manifestPath, manifestSource);
            ContentPackManifest manifest;
            try
            {
                manifest = deserializer.DeserializeManifest(textByPath[manifestPath], manifestSource);
            }
            catch (ContentDeserializationException exception)
            {
                diagnostics.Add(new ValidatorDiagnostic(
                    "manifest_deserialization_failed",
                    manifestSource,
                    "$",
                    exception.Message));
                continue;
            }

            var documents = new List<ContentDocumentText>();
            foreach (ContentPackDocumentReference reference in manifest.Documents)
            {
                string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, reference.Path));
                if (!IsWithinRoot(contentRoot, resolved))
                {
                    diagnostics.Add(new ValidatorDiagnostic(
                        "document_path_outside_root",
                        manifestSource,
                        "$.documents",
                        $"Document path '{reference.Path}' escapes the content root."));
                    continue;
                }
                if (!textByPath.TryGetValue(resolved, out string? json))
                {
                    diagnostics.Add(new ValidatorDiagnostic(
                        "document_missing",
                        manifestSource,
                        "$.documents",
                        $"Document path '{reference.Path}' does not exist."));
                    continue;
                }
                if (owners.TryGetValue(resolved, out string? existingOwner))
                {
                    diagnostics.Add(new ValidatorDiagnostic(
                        "document_owned_twice",
                        manifestSource,
                        "$.documents",
                        $"Document '{RelativeName(contentRoot, resolved)}' is already owned by '{existingOwner}'."));
                    continue;
                }

                owners.Add(resolved, manifestSource);
                documents.Add(new ContentDocumentText(
                    reference.Path,
                    RelativeName(contentRoot, resolved),
                    json));
            }

            bundles.Add(new ContentPackTextBundle(manifestSource, textByPath[manifestPath], documents));
        }

        foreach (string orphan in contentFiles.Select(Path.GetFullPath)
                     .Except(owners.Keys, StringComparer.OrdinalIgnoreCase)
                     .Order(StringComparer.Ordinal))
        {
            diagnostics.Add(new ValidatorDiagnostic(
                "document_orphaned",
                RelativeName(contentRoot, orphan),
                "$",
                "Active content must be a manifest or be owned by exactly one manifest."));
        }

        return bundles;
    }

    private static bool TryParseOptions(
        IReadOnlyList<string> args,
        out ContentValidatorOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        if (args.Count == 1 && args[0] is "--help" or "-h")
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count ||
                args[index] is not ("--content-root" or "--schema-root" or "--registrations"))
            {
                error = $"Unknown or incomplete option '{args[index]}'.";
                return false;
            }
            if (!values.TryAdd(args[index], args[index + 1]))
            {
                error = $"Option '{args[index]}' was supplied more than once.";
                return false;
            }
        }

        string[] required = ["--content-root", "--schema-root", "--registrations"];
        string? missing = required.FirstOrDefault(key => !values.ContainsKey(key));
        if (missing is not null)
        {
            error = $"Required option '{missing}' was not supplied.";
            return false;
        }

        options = new ContentValidatorOptions(
            values["--content-root"],
            values["--schema-root"],
            values["--registrations"]);
        return true;
    }

    private static bool DeclaresManifestSchema(string json)
    {
        try
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("$schema", out System.Text.Json.JsonElement schema) &&
                schema.ValueKind == System.Text.Json.JsonValueKind.String &&
                string.Equals(schema.GetString(), ManifestSchemaId, StringComparison.Ordinal);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static void WriteDiagnostics(IEnumerable<ValidatorDiagnostic> diagnostics, TextWriter writer)
    {
        foreach (ValidatorDiagnostic diagnostic in diagnostics
                     .OrderBy(item => item.SourceName, StringComparer.Ordinal)
                     .ThenBy(item => item.Location, StringComparer.Ordinal)
                     .ThenBy(item => item.Code, StringComparer.Ordinal))
        {
            writer.WriteLine(diagnostic);
        }
    }

    private static void WriteUsage(TextWriter writer) =>
        writer.WriteLine(
            "Usage: Convergence.ContentValidator --content-root <directory> " +
            "--schema-root <directory> --registrations <file>");

    private static bool IsWithinRoot(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }

    private static string RelativeName(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string ToSnakeCase(string value) =>
        string.Concat(value.Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? $"_{char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));
}
