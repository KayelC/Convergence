using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Validation;

namespace Convergence.Catalog;

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

public interface IItemDefinitionRepository
{
    bool TryGetItem(ContentId id, out ItemDefinition? definition);
    ItemDefinition GetRequiredItem(ContentId id);
}

public interface IEquipmentDefinitionRepository
{
    bool TryGetEquipment(ContentId id, out EquipmentDefinition? definition);
    EquipmentDefinition GetRequiredEquipment(ContentId id);
}

public interface IShopCatalogDefinitionRepository
{
    bool TryGetShop(ContentId id, out ShopCatalogDefinition? definition);
    ShopCatalogDefinition GetRequiredShop(ContentId id);
}

public interface INegotiationDefinitionRepository
{
    bool TryGetNegotiation(ContentId id, out NegotiationDefinition? definition);
    NegotiationDefinition GetRequiredNegotiation(ContentId id);
}

public interface IEncounterDefinitionRepository
{
    bool TryGetEncounter(ContentId id, out EncounterDefinition? definition);
    EncounterDefinition GetRequiredEncounter(ContentId id);
}

public interface IDungeonDefinitionRepository
{
    bool TryGetDungeon(ContentId id, out DungeonDefinition? definition);
    DungeonDefinition GetRequiredDungeon(ContentId id);
}

public interface IFusionRecipeDefinitionRepository
{
    bool TryGetFusionRecipe(ContentId id, out FusionRecipeDefinition? definition);
    FusionRecipeDefinition GetRequiredFusionRecipe(ContentId id);
}

public interface IRulesetDefinitionRepository
{
    bool TryGetRuleset(ContentId id, out RulesetDefinition? definition);
    RulesetDefinition GetRequiredRuleset(ContentId id);
}

public interface IDurationVocabularyRepository
{
    IReadOnlySet<ContentId> RegisteredEventIds { get; }
    IReadOnlySet<ContentId> RegisteredPhaseIds { get; }
}

/// <summary>Provides immutable, qualified lookup for every validated content family.</summary>
public sealed class GameDataCatalog :
    ISkillDefinitionRepository,
    IEntityDefinitionRepository,
    IRaceDefinitionRepository,
    IAilmentDefinitionRepository,
    IItemDefinitionRepository,
    IEquipmentDefinitionRepository,
    IShopCatalogDefinitionRepository,
    INegotiationDefinitionRepository,
    IEncounterDefinitionRepository,
    IDungeonDefinitionRepository,
    IFusionRecipeDefinitionRepository,
    IRulesetDefinitionRepository,
    IDurationVocabularyRepository
{
    internal GameDataCatalog(
        IEnumerable<KeyValuePair<ContentId, SkillDefinition>> skills,
        IEnumerable<KeyValuePair<ContentId, EntityDefinition>> entities,
        IEnumerable<KeyValuePair<ContentId, RaceDefinition>> races,
        IEnumerable<KeyValuePair<ContentId, AilmentDefinition>> ailments,
        IEnumerable<KeyValuePair<ContentId, ItemDefinition>> items,
        IEnumerable<KeyValuePair<ContentId, EquipmentDefinition>>? equipment = null,
        IEnumerable<KeyValuePair<ContentId, ShopCatalogDefinition>>? shops = null,
        IEnumerable<KeyValuePair<ContentId, NegotiationDefinition>>? negotiations = null,
        IEnumerable<KeyValuePair<ContentId, EncounterDefinition>>? encounters = null,
        IEnumerable<KeyValuePair<ContentId, DungeonDefinition>>? dungeons = null,
        IEnumerable<KeyValuePair<ContentId, FusionRecipeDefinition>>? fusionRecipes = null,
        IEnumerable<KeyValuePair<ContentId, RulesetDefinition>>? rulesets = null)
        : this(
            [],
            skills,
            entities,
            races,
            ailments,
            items,
            equipment,
            shops,
            negotiations,
            encounters,
            dungeons,
            fusionRecipes,
            rulesets)
    {
    }

    internal GameDataCatalog(
        IEnumerable<ContentPackIdentity> contentPacks,
        IEnumerable<KeyValuePair<ContentId, SkillDefinition>> skills,
        IEnumerable<KeyValuePair<ContentId, EntityDefinition>> entities,
        IEnumerable<KeyValuePair<ContentId, RaceDefinition>> races,
        IEnumerable<KeyValuePair<ContentId, AilmentDefinition>> ailments,
        IEnumerable<KeyValuePair<ContentId, ItemDefinition>> items,
        IEnumerable<KeyValuePair<ContentId, EquipmentDefinition>>? equipment = null,
        IEnumerable<KeyValuePair<ContentId, ShopCatalogDefinition>>? shops = null,
        IEnumerable<KeyValuePair<ContentId, NegotiationDefinition>>? negotiations = null,
        IEnumerable<KeyValuePair<ContentId, EncounterDefinition>>? encounters = null,
        IEnumerable<KeyValuePair<ContentId, DungeonDefinition>>? dungeons = null,
        IEnumerable<KeyValuePair<ContentId, FusionRecipeDefinition>>? fusionRecipes = null,
        IEnumerable<KeyValuePair<ContentId, RulesetDefinition>>? rulesets = null,
        SkillSystemRegistrationSnapshot? registrations = null)
    {
        SkillSystemRegistrationSnapshot vocabulary =
            registrations ?? new SkillSystemRegistrationBuilder().Build();
        ContentPacks = Array.AsReadOnly((contentPacks ?? throw new ArgumentNullException(nameof(contentPacks))).ToArray());
        RegisteredEventIds = vocabulary.EventIds;
        RegisteredPhaseIds = vocabulary.PhaseIds;
        Skills = Snapshot(skills);
        Entities = Snapshot(entities);
        Races = Snapshot(races);
        Ailments = Snapshot(ailments);
        Items = Snapshot(items);
        Equipment = Snapshot(equipment ?? []);
        Shops = Snapshot(shops ?? []);
        Negotiations = Snapshot(negotiations ?? []);
        Encounters = Snapshot(encounters ?? []);
        Dungeons = Snapshot(dungeons ?? []);
        FusionRecipes = Snapshot(fusionRecipes ?? []);
        Rulesets = Snapshot(rulesets ?? []);
    }

    public IReadOnlyList<ContentPackIdentity> ContentPacks { get; }
    public IReadOnlySet<ContentId> RegisteredEventIds { get; }
    public IReadOnlySet<ContentId> RegisteredPhaseIds { get; }
    public IReadOnlyDictionary<ContentId, SkillDefinition> Skills { get; }
    public IReadOnlyDictionary<ContentId, EntityDefinition> Entities { get; }
    public IReadOnlyDictionary<ContentId, RaceDefinition> Races { get; }
    public IReadOnlyDictionary<ContentId, AilmentDefinition> Ailments { get; }
    public IReadOnlyDictionary<ContentId, ItemDefinition> Items { get; }
    public IReadOnlyDictionary<ContentId, EquipmentDefinition> Equipment { get; }
    public IReadOnlyDictionary<ContentId, ShopCatalogDefinition> Shops { get; }
    public IReadOnlyDictionary<ContentId, NegotiationDefinition> Negotiations { get; }
    public IReadOnlyDictionary<ContentId, EncounterDefinition> Encounters { get; }
    public IReadOnlyDictionary<ContentId, DungeonDefinition> Dungeons { get; }
    public IReadOnlyDictionary<ContentId, FusionRecipeDefinition> FusionRecipes { get; }
    public IReadOnlyDictionary<ContentId, RulesetDefinition> Rulesets { get; }

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

    public bool TryGetItem(ContentId id, out ItemDefinition? definition) =>
        TryGet(Items, id, out definition);

    public ItemDefinition GetRequiredItem(ContentId id) => GetRequired(Items, id, "item");

    public bool TryGetEquipment(ContentId id, out EquipmentDefinition? definition) =>
        TryGet(Equipment, id, out definition);

    public EquipmentDefinition GetRequiredEquipment(ContentId id) => GetRequired(Equipment, id, "equipment");

    public bool TryGetShop(ContentId id, out ShopCatalogDefinition? definition) =>
        TryGet(Shops, id, out definition);

    public ShopCatalogDefinition GetRequiredShop(ContentId id) => GetRequired(Shops, id, "shop");

    public bool TryGetNegotiation(ContentId id, out NegotiationDefinition? definition) =>
        TryGet(Negotiations, id, out definition);

    public NegotiationDefinition GetRequiredNegotiation(ContentId id) => GetRequired(Negotiations, id, "negotiation");

    public bool TryGetEncounter(ContentId id, out EncounterDefinition? definition) =>
        TryGet(Encounters, id, out definition);

    public EncounterDefinition GetRequiredEncounter(ContentId id) => GetRequired(Encounters, id, "encounter");

    public bool TryGetDungeon(ContentId id, out DungeonDefinition? definition) =>
        TryGet(Dungeons, id, out definition);

    public DungeonDefinition GetRequiredDungeon(ContentId id) => GetRequired(Dungeons, id, "dungeon");

    public bool TryGetFusionRecipe(ContentId id, out FusionRecipeDefinition? definition) =>
        TryGet(FusionRecipes, id, out definition);

    public FusionRecipeDefinition GetRequiredFusionRecipe(ContentId id) =>
        GetRequired(FusionRecipes, id, "fusion recipe");

    public bool TryGetRuleset(ContentId id, out RulesetDefinition? definition) =>
        TryGet(Rulesets, id, out definition);

    public RulesetDefinition GetRequiredRuleset(ContentId id) => GetRequired(Rulesets, id, "ruleset");

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
        if (!id.IsValid)
        {
            throw new ArgumentException("Catalog lookup content ID cannot be empty.", nameof(id));
        }

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
