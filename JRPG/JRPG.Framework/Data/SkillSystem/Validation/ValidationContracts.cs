using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Data.SkillSystem.Validation;

public enum ContentValidationErrorCode
{
    DocumentSchemaVersionUnsupported,
    DocumentNotDeclared,
    DocumentTypeMismatch,
    DocumentMissing,
    DocumentDuplicatePath,
    RecordIdMustBeLocal,
    RecordDuplicateId,
    ListDuplicateValue,
    ReferenceMissing,
    ReferenceAmbiguous,
    RegistrationMissing,
    DefinitionTypeUnsupported,
    ParameterValidationFailed,
    ValueOutOfRange,
    ValueMustBePositive,
    ValueMustBeNonNegative,
    MinimumExceedsMaximum,
    ShapeInvalid,
    SkillActiveMenuGroupRequired,
    SkillActiveEffectsRequired,
    SkillActiveAvailabilityRequired,
    SkillActivePassiveMembersForbidden,
    SkillPassiveMenuGroupForbidden,
    SkillPassiveAvailabilityForbidden,
    SkillPassiveActiveMembersForbidden,
    SkillPassiveInheritanceGroupRequired,
    SkillPassiveBehaviorRequired,
    TriggerEffectsRequired,
    AlmightyAffinityForbidden,
    InheritanceListConflict,
    InheritanceExplicitAllowInvalid,
    EntitySkillAssignmentDuplicate,
    EntityUnlockLevelInvalid,
    MutationTierInvalid,
    MutationTierDuplicate,
    MutationTierGap
}

public sealed record ContentValidationError(
    string PackId,
    string SourceName,
    string RecordType,
    ContentId? RecordId,
    string JsonPath,
    ContentValidationErrorCode Code,
    string Message,
    string? Suggestion = null);

public sealed record ContentParameterValidationIssue(
    string? ParameterPath,
    string Message,
    string? Suggestion = null);

public interface IContentParameterValidator
{
    IReadOnlyList<ContentParameterValidationIssue> Validate(
        IReadOnlyDictionary<string, object?> parameters);
}

public sealed record SourceContentDocument<TDefinition>
{
    public SourceContentDocument(
        string manifestPath,
        string sourceName,
        DeserializedContentDocument<TDefinition> document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(document);

        ManifestPath = manifestPath;
        SourceName = sourceName;
        Document = document;
    }

    public string ManifestPath { get; }
    public string SourceName { get; }
    public DeserializedContentDocument<TDefinition> Document { get; }
}

public sealed record SkillSystemValidationRequest
{
    public SkillSystemValidationRequest(
        ContentPackManifest manifest,
        string manifestSourceName,
        SkillSystemRegistrationSnapshot registrations,
        IEnumerable<SourceContentDocument<SkillDefinition>>? skillDocuments = null,
        IEnumerable<SourceContentDocument<EntityDefinition>>? entityDocuments = null,
        IEnumerable<SourceContentDocument<RaceDefinition>>? raceDocuments = null,
        IEnumerable<SourceContentDocument<AilmentDefinition>>? ailmentDocuments = null,
        IEnumerable<SourceContentDocument<ItemDefinition>>? itemDocuments = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestSourceName);
        ArgumentNullException.ThrowIfNull(registrations);

        Manifest = manifest;
        ManifestSourceName = manifestSourceName;
        Registrations = registrations;
        SkillDocuments = Snapshot(skillDocuments);
        EntityDocuments = Snapshot(entityDocuments);
        RaceDocuments = Snapshot(raceDocuments);
        AilmentDocuments = Snapshot(ailmentDocuments);
        ItemDocuments = Snapshot(itemDocuments);
    }

    public ContentPackManifest Manifest { get; }
    public string ManifestSourceName { get; }
    public SkillSystemRegistrationSnapshot Registrations { get; }
    public IReadOnlyList<SourceContentDocument<SkillDefinition>> SkillDocuments { get; }
    public IReadOnlyList<SourceContentDocument<EntityDefinition>> EntityDocuments { get; }
    public IReadOnlyList<SourceContentDocument<RaceDefinition>> RaceDocuments { get; }
    public IReadOnlyList<SourceContentDocument<AilmentDefinition>> AilmentDocuments { get; }
    public IReadOnlyList<SourceContentDocument<ItemDefinition>> ItemDocuments { get; }

    private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly(values?.ToArray() ?? Array.Empty<T>());
}

public interface ISkillSystemContentValidator
{
    ContentValidationResult Validate(SkillSystemValidationRequest request);
}

public sealed record ContentValidationResult
{
    internal ContentValidationResult(
        IEnumerable<ContentValidationError> errors,
        ValidatedSkillSystemContentPack? validatedContent)
    {
        Errors = Array.AsReadOnly(errors.ToArray());
        ValidatedContent = validatedContent;
    }

    public IReadOnlyList<ContentValidationError> Errors { get; }
    public bool IsValid => Errors.Count == 0;
    public ValidatedSkillSystemContentPack? ValidatedContent { get; }

    public ValidatedSkillSystemContentPack RequireValidContent() =>
        ValidatedContent ?? throw new ContentValidationException(Errors);
}

public sealed class ContentValidationException : Exception
{
    public ContentValidationException(IEnumerable<ContentValidationError> errors)
        : this(Array.AsReadOnly(errors.ToArray()))
    {
    }

    private ContentValidationException(IReadOnlyList<ContentValidationError> errors)
        : base($"Content validation failed with {errors.Count} error(s).")
    {
        Errors = errors;
    }

    public IReadOnlyList<ContentValidationError> Errors { get; }
}

public sealed record ValidatedSkillSystemContentPack
{
    internal ValidatedSkillSystemContentPack(SkillSystemValidationRequest request)
    {
        Manifest = request.Manifest;
        SkillDocuments = request.SkillDocuments;
        EntityDocuments = request.EntityDocuments;
        RaceDocuments = request.RaceDocuments;
        AilmentDocuments = request.AilmentDocuments;
        ItemDocuments = request.ItemDocuments;
    }

    public ContentPackManifest Manifest { get; }
    public IReadOnlyList<SourceContentDocument<SkillDefinition>> SkillDocuments { get; }
    public IReadOnlyList<SourceContentDocument<EntityDefinition>> EntityDocuments { get; }
    public IReadOnlyList<SourceContentDocument<RaceDefinition>> RaceDocuments { get; }
    public IReadOnlyList<SourceContentDocument<AilmentDefinition>> AilmentDocuments { get; }
    public IReadOnlyList<SourceContentDocument<ItemDefinition>> ItemDocuments { get; }
}
