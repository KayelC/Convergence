using System.Text.Json;
using Json.Schema;

namespace Convergence.ContentValidator;

internal sealed class SchemaDocumentValidator
{
    private readonly IReadOnlyDictionary<string, JsonSchema> _schemas;

    private SchemaDocumentValidator(IReadOnlyDictionary<string, JsonSchema> schemas)
    {
        _schemas = schemas;
    }

    public static SchemaDocumentValidator Load(string schemaRoot)
    {
        string root = Path.GetFullPath(schemaRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Schema root does not exist: {root}");
        }

        SchemaRegistry registry = new();
        BuildOptions buildOptions = new() { SchemaRegistry = registry };
        Dictionary<string, JsonSchema> schemas = new(StringComparer.Ordinal);

        foreach (string path in Directory.GetFiles(root, "*.schema.json", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.Ordinal))
        {
            string text = File.ReadAllText(path);
            using JsonDocument document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("$id", out JsonElement idElement) ||
                idElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                throw new InvalidDataException($"Schema '{path}' does not declare a nonempty $id.");
            }

            string id = idElement.GetString()!;
            JsonSchema schema = JsonSchema.FromText(text, buildOptions, new Uri(path));
            registry.Register(schema);
            if (!schemas.TryAdd(id, schema))
            {
                throw new InvalidDataException($"Schema ID '{id}' is declared more than once.");
            }
        }

        if (schemas.Count == 0)
        {
            throw new InvalidDataException($"Schema root '{root}' contains no .schema.json files.");
        }

        return new SchemaDocumentValidator(schemas);
    }

    public IReadOnlyList<ValidatorDiagnostic> Validate(string sourceName, string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return
            [
                new ValidatorDiagnostic(
                    "json_invalid",
                    sourceName,
                    "$",
                    exception.Message)
            ];
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("$schema", out JsonElement schemaElement) ||
                schemaElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(schemaElement.GetString()))
            {
                return
                [
                    new ValidatorDiagnostic(
                        "schema_missing",
                        sourceName,
                        "$.$schema",
                        "The document must declare its Convergence schema ID.")
                ];
            }

            string schemaId = schemaElement.GetString()!;
            if (!_schemas.TryGetValue(schemaId, out JsonSchema? schema))
            {
                return
                [
                    new ValidatorDiagnostic(
                        "schema_unknown",
                        sourceName,
                        "$.$schema",
                        $"No loaded schema has ID '{schemaId}'.")
                ];
            }

            EvaluationResults result = schema.Evaluate(document.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });
            if (result.IsValid)
            {
                return [];
            }

            result.ToList();
            string location = (result.Details ?? [])
                .FirstOrDefault(detail => !detail.IsValid)?.InstanceLocation.ToString() ?? "$";
            return
            [
                new ValidatorDiagnostic(
                    "schema_invalid",
                    sourceName,
                    location,
                    $"Document does not satisfy '{schemaId}'.")
            ];
        }
    }
}
