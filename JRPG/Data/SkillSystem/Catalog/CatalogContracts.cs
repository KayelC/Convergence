using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Validation;

namespace JRPGPrototype.Data.SkillSystem.Catalog;

public sealed record ContentDocumentText
{
    public ContentDocumentText(string path, string sourceName, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(json);
        Path = path;
        SourceName = sourceName;
        Json = json;
    }

    public string Path { get; }
    public string SourceName { get; }
    public string Json { get; }
}

public sealed record ContentPackTextBundle
{
    public ContentPackTextBundle(
        string manifestSourceName,
        string manifestJson,
        IEnumerable<ContentDocumentText>? documents = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestSourceName);
        ArgumentNullException.ThrowIfNull(manifestJson);
        ManifestSourceName = manifestSourceName;
        ManifestJson = manifestJson;
        Documents = Array.AsReadOnly(documents?.ToArray() ?? Array.Empty<ContentDocumentText>());
    }

    public string ManifestSourceName { get; }
    public string ManifestJson { get; }
    public IReadOnlyList<ContentDocumentText> Documents { get; }
}

public sealed record SkillSystemCatalogLoadRequest
{
    public SkillSystemCatalogLoadRequest(
        SkillSystemRegistrationSnapshot registrations,
        IEnumerable<ContentPackTextBundle> bundles)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(bundles);
        Registrations = registrations;
        Bundles = Array.AsReadOnly(bundles.ToArray());
    }

    public SkillSystemRegistrationSnapshot Registrations { get; }
    public IReadOnlyList<ContentPackTextBundle> Bundles { get; }
}

public enum CatalogLoadDiagnosticCode
{
    ManifestDeserializationFailed,
    DocumentDeserializationFailed,
    DocumentPathInvalid,
    DocumentPathDuplicate,
    DocumentMissing,
    DocumentUnexpected,
    DocumentTypeUnsupported,
    ContentValidationFailed,
    PackDuplicate,
    DependencyDuplicate,
    DependencySelfReference,
    DependencyMissing,
    DependencyVersionMismatch,
    DependencyCycle,
    ExternalDependencyNotDeclared,
    ExternalReferenceMissing,
    ExternalReferenceWrongType,
    CrossPackInheritanceInvalid,
    CatalogDuplicateId
}

public sealed record CatalogLoadDiagnostic(
    CatalogLoadDiagnosticCode Code,
    string? PackId,
    string SourceName,
    string JsonPath,
    string Message,
    string? RecordType = null,
    ContentId? RecordId = null,
    ContentValidationErrorCode? ValidationCode = null,
    string? Suggestion = null);

public interface ISkillDefinitionRepository
{
    bool TryGetSkill(ContentId id, out SkillDefinition? definition);
    SkillDefinition GetRequiredSkill(ContentId id);
}

public interface IEntityDefinitionRepository
{
    bool TryGetEntity(ContentId id, out EntityDefinition? definition);
    EntityDefinition GetRequiredEntity(ContentId id);
}

public interface IRaceDefinitionRepository
{
    bool TryGetRace(ContentId id, out RaceDefinition? definition);
    RaceDefinition GetRequiredRace(ContentId id);
}

public interface IAilmentDefinitionRepository
{
    bool TryGetAilment(ContentId id, out AilmentDefinition? definition);
    AilmentDefinition GetRequiredAilment(ContentId id);
}

public sealed class GameDataCatalog :
    ISkillDefinitionRepository,
    IEntityDefinitionRepository,
    IRaceDefinitionRepository,
    IAilmentDefinitionRepository
{
    internal GameDataCatalog(
        IEnumerable<KeyValuePair<ContentId, SkillDefinition>> skills,
        IEnumerable<KeyValuePair<ContentId, EntityDefinition>> entities,
        IEnumerable<KeyValuePair<ContentId, RaceDefinition>> races,
        IEnumerable<KeyValuePair<ContentId, AilmentDefinition>> ailments)
    {
        Skills = Snapshot(skills);
        Entities = Snapshot(entities);
        Races = Snapshot(races);
        Ailments = Snapshot(ailments);
    }

    public IReadOnlyDictionary<ContentId, SkillDefinition> Skills { get; }
    public IReadOnlyDictionary<ContentId, EntityDefinition> Entities { get; }
    public IReadOnlyDictionary<ContentId, RaceDefinition> Races { get; }
    public IReadOnlyDictionary<ContentId, AilmentDefinition> Ailments { get; }

    public bool TryGetSkill(ContentId id, out SkillDefinition? definition) =>
        TryGet(Skills, id, out definition);

    public SkillDefinition GetRequiredSkill(ContentId id) => GetRequired(Skills, id, "skill");

    public bool TryGetEntity(ContentId id, out EntityDefinition? definition) =>
        TryGet(Entities, id, out definition);

    public EntityDefinition GetRequiredEntity(ContentId id) => GetRequired(Entities, id, "entity");

    public bool TryGetRace(ContentId id, out RaceDefinition? definition) =>
        TryGet(Races, id, out definition);

    public RaceDefinition GetRequiredRace(ContentId id) => GetRequired(Races, id, "race");

    public bool TryGetAilment(ContentId id, out AilmentDefinition? definition) =>
        TryGet(Ailments, id, out definition);

    public AilmentDefinition GetRequiredAilment(ContentId id) => GetRequired(Ailments, id, "ailment");

    private static IReadOnlyDictionary<ContentId, T> Snapshot<T>(
        IEnumerable<KeyValuePair<ContentId, T>> values) =>
        new ReadOnlyDictionary<ContentId, T>(values.ToDictionary(pair => pair.Key, pair => pair.Value));

    private static bool TryGet<T>(
        IReadOnlyDictionary<ContentId, T> definitions,
        ContentId id,
        out T? definition)
    {
        RequireQualified(id);
        return definitions.TryGetValue(id, out definition);
    }

    private static T GetRequired<T>(
        IReadOnlyDictionary<ContentId, T> definitions,
        ContentId id,
        string recordType)
    {
        RequireQualified(id);
        return definitions.TryGetValue(id, out T? definition)
            ? definition
            : throw new KeyNotFoundException($"No {recordType} definition exists for '{id}'.");
    }

    private static void RequireQualified(ContentId id)
    {
        if (!id.IsQualified)
        {
            throw new ArgumentException("Catalog lookups require a pack-qualified content ID.", nameof(id));
        }
    }
}

public interface ISkillSystemCatalogLoader
{
    CatalogLoadResult Load(SkillSystemCatalogLoadRequest request);
}

public sealed record CatalogLoadResult
{
    internal CatalogLoadResult(
        IEnumerable<CatalogLoadDiagnostic> diagnostics,
        GameDataCatalog? catalog)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Catalog = catalog;
    }

    public IReadOnlyList<CatalogLoadDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Diagnostics.Count == 0;
    public GameDataCatalog? Catalog { get; }

    public GameDataCatalog RequireCatalog() =>
        Catalog ?? throw new CatalogLoadException(Diagnostics);
}

public sealed class CatalogLoadException : Exception
{
    public CatalogLoadException(IEnumerable<CatalogLoadDiagnostic> diagnostics)
        : this(Array.AsReadOnly(diagnostics.ToArray()))
    {
    }

    private CatalogLoadException(IReadOnlyList<CatalogLoadDiagnostic> diagnostics)
        : base($"Skill-system catalog loading failed with {diagnostics.Count} diagnostic(s).")
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<CatalogLoadDiagnostic> Diagnostics { get; }
}
