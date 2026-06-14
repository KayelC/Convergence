using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Data.SkillSystem;

public interface ISkillSystemDocumentDeserializer
{
    ContentPackManifest DeserializeManifest(string json, string sourceName);
    DeserializedContentDocument<SkillDefinition> DeserializeSkills(string json, string sourceName);
    DeserializedContentDocument<EntityDefinition> DeserializeEntities(string json, string sourceName);
    DeserializedContentDocument<RaceDefinition> DeserializeRaces(string json, string sourceName);
    DeserializedContentDocument<AilmentDefinition> DeserializeAilments(string json, string sourceName);
}

public sealed record ContentPackDocumentReference(string Type, string Path);

public sealed record ContentPackDependency(string Id, SemanticVersion Version);

public sealed record ContentPackManifest
{
    public ContentPackManifest(
        int schemaVersion,
        string id,
        SemanticVersion version,
        string displayName,
        string? description,
        IEnumerable<ContentPackDependency>? dependencies,
        IEnumerable<ContentPackDocumentReference> documents)
    {
        SchemaVersion = schemaVersion;
        Id = id;
        Version = version;
        DisplayName = displayName;
        Description = description;
        Dependencies = Array.AsReadOnly(dependencies?.ToArray() ?? Array.Empty<ContentPackDependency>());
        Documents = Array.AsReadOnly(documents.ToArray());
    }

    public int SchemaVersion { get; }
    public string Id { get; }
    public SemanticVersion Version { get; }
    public string DisplayName { get; }
    public string? Description { get; }
    public IReadOnlyList<ContentPackDependency> Dependencies { get; }
    public IReadOnlyList<ContentPackDocumentReference> Documents { get; }
}

public sealed record DeserializedContentDocument<TDefinition>
{
    public DeserializedContentDocument(int schemaVersion, IEnumerable<TDefinition> records)
    {
        SchemaVersion = schemaVersion;
        Records = Array.AsReadOnly(records.ToArray());
    }

    public int SchemaVersion { get; }
    public IReadOnlyList<TDefinition> Records { get; }
}

public sealed class ContentDeserializationException : Exception
{
    public ContentDeserializationException(
        string sourceName,
        string message,
        string? jsonPath = null,
        long? lineNumber = null,
        long? bytePositionInLine = null,
        string? discriminator = null,
        Exception? innerException = null)
        : base(FormatMessage(sourceName, message, jsonPath), innerException)
    {
        SourceName = sourceName;
        JsonPath = jsonPath;
        LineNumber = lineNumber;
        BytePositionInLine = bytePositionInLine;
        Discriminator = discriminator;
    }

    public string SourceName { get; }
    public string? JsonPath { get; }
    public long? LineNumber { get; }
    public long? BytePositionInLine { get; }
    public string? Discriminator { get; }

    private static string FormatMessage(string sourceName, string message, string? jsonPath)
    {
        return jsonPath is null
            ? $"Failed to deserialize '{sourceName}': {message}"
            : $"Failed to deserialize '{sourceName}' at '{jsonPath}': {message}";
    }
}
