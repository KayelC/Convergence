using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Fusion;

namespace JRPGPrototype.Logic.Runtime;

public enum RuntimeSaveValidationCode
{
    ContractVersionUnsupported,
    DuplicateActorInstanceId,
    MissingActorReference,
    MissingActiveFormReference,
    MissingCatalogEntity,
    MissingCatalogSkill,
    MissingCatalogItem,
    MissingCatalogEquipment,
    MissingCatalogDungeon,
    MissingCatalogAilment,
    MissingCompendiumEntity,
    KnowledgeTargetMissing,
    InvalidCheckpoint
}

public sealed record RuntimeSaveValidationDiagnostic(
    RuntimeSaveValidationCode Code,
    string Message,
    RuntimeInstanceId? InstanceId = null,
    ContentId? ContentId = null,
    string? Path = null);

public sealed class RuntimeSaveValidationException : InvalidOperationException
{
    public RuntimeSaveValidationException(IEnumerable<RuntimeSaveValidationDiagnostic> diagnostics)
        : base("Runtime save snapshot validation failed.")
    {
        Diagnostics = RuntimePersistenceCollections.List(diagnostics);
    }

    public IReadOnlyList<RuntimeSaveValidationDiagnostic> Diagnostics { get; }
}

public sealed record RuntimeSaveValidationResult
{
    public RuntimeSaveValidationResult(
        RuntimeSaveGameSnapshot snapshot,
        IEnumerable<RuntimeSaveValidationDiagnostic>? diagnostics = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Diagnostics = RuntimePersistenceCollections.List(diagnostics);
    }

    public RuntimeSaveGameSnapshot Snapshot { get; }
    public IReadOnlyList<RuntimeSaveValidationDiagnostic> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0;

    public RuntimeSaveGameSnapshot RequireValidSnapshot()
    {
        if (!IsValid)
        {
            throw new RuntimeSaveValidationException(Diagnostics);
        }

        return Snapshot;
    }
}

public interface IRuntimeSaveValidator
{
    RuntimeSaveValidationResult Validate(RuntimeSaveGameSnapshot snapshot, GameDataCatalog catalog);
}

public sealed record RuntimeKnowledgeSnapshot
{
    public RuntimeKnowledgeSnapshot(
        IEnumerable<RuntimeElementalAffinityKnowledgeSnapshot>? elementalAffinities = null,
        IEnumerable<RuntimeAilmentResistanceKnowledgeSnapshot>? ailmentResistances = null,
        IEnumerable<RuntimeInstantDeathResistanceKnowledgeSnapshot>? instantDeathResistances = null)
    {
        ElementalAffinities = RuntimePersistenceCollections.List(elementalAffinities);
        AilmentResistances = RuntimePersistenceCollections.List(ailmentResistances);
        InstantDeathResistances = RuntimePersistenceCollections.List(instantDeathResistances);
    }

    public IReadOnlyList<RuntimeElementalAffinityKnowledgeSnapshot> ElementalAffinities { get; }
    public IReadOnlyList<RuntimeAilmentResistanceKnowledgeSnapshot> AilmentResistances { get; }
    public IReadOnlyList<RuntimeInstantDeathResistanceKnowledgeSnapshot> InstantDeathResistances { get; }
}

public sealed record RuntimeElementalAffinityKnowledgeSnapshot(
    ContentId EntityId,
    DamageElement Element,
    ElementalAffinity Affinity);

public sealed record RuntimeAilmentResistanceKnowledgeSnapshot(
    ContentId EntityId,
    ContentId AilmentId,
    ResistanceLevel Resistance);

public sealed record RuntimeInstantDeathResistanceKnowledgeSnapshot(
    ContentId EntityId,
    InstantDeathChannel Channel,
    ResistanceLevel Resistance);

public sealed record RuntimeSessionProgressSnapshot
{
    public RuntimeSessionProgressSnapshot(
        ContentId? moonPhaseId = null,
        long elapsedTicks = 0,
        IEnumerable<KeyValuePair<ContentId, long>>? counters = null,
        IEnumerable<ContentId>? flags = null)
    {
        if (elapsedTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedTicks), "Elapsed ticks cannot be negative.");
        }

        MoonPhaseId = moonPhaseId;
        ElapsedTicks = elapsedTicks;
        Counters = RuntimePersistenceCollections.Dictionary(counters);
        Flags = RuntimePersistenceCollections.List(flags?.Distinct());
    }

    public ContentId? MoonPhaseId { get; }
    public long ElapsedTicks { get; }
    public IReadOnlyDictionary<ContentId, long> Counters { get; }
    public IReadOnlyList<ContentId> Flags { get; }
}

public enum RuntimeCheckpointKind
{
    SaveCreated,
    ContentLoaded,
    ActorRestored,
    FieldRestored,
    BattleCompleted,
    HostAction
}

public sealed record RuntimeCheckpointEntrySnapshot
{
    public RuntimeCheckpointEntrySnapshot(
        long sequence,
        RuntimeCheckpointKind kind,
        string message,
        RuntimeInstanceId? actorId = null,
        ContentId? contentId = null)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Checkpoint sequence cannot be negative.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Sequence = sequence;
        Kind = kind;
        Message = message;
        ActorId = actorId;
        ContentId = contentId;
    }

    public long Sequence { get; }
    public RuntimeCheckpointKind Kind { get; }
    public string Message { get; }
    public RuntimeInstanceId? ActorId { get; }
    public ContentId? ContentId { get; }
}

public sealed record RuntimeCheckpointLogSnapshot
{
    public RuntimeCheckpointLogSnapshot(IEnumerable<RuntimeCheckpointEntrySnapshot>? entries = null)
    {
        Entries = RuntimePersistenceCollections.List(entries);
    }

    public IReadOnlyList<RuntimeCheckpointEntrySnapshot> Entries { get; }
}

public sealed record RuntimeSaveGameSnapshot
{
    public const int CurrentContractVersion = 2;

    public RuntimeSaveGameSnapshot(
        SemanticVersion frameworkVersion,
        IEnumerable<RuntimeActorSnapshot> actors,
        RuntimePartyStockSnapshot partyStock,
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        RuntimeWalletSnapshot wallet,
        RuntimeFieldSnapshot? field,
        CompendiumStateSnapshot compendium,
        RuntimeKnowledgeSnapshot knowledge,
        RuntimeSessionProgressSnapshot session,
        RuntimeCheckpointLogSnapshot? checkpoints = null,
        IEnumerable<KeyValuePair<ContentId, string>>? hostContext = null,
        int contractVersion = CurrentContractVersion)
    {
        if (contractVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contractVersion), "Contract version must be positive.");
        }

        ContractVersion = contractVersion;
        FrameworkVersion = frameworkVersion;
        Actors = RuntimePersistenceCollections.List(actors);
        PartyStock = partyStock ?? throw new ArgumentNullException(nameof(partyStock));
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        Field = field;
        Compendium = compendium ?? throw new ArgumentNullException(nameof(compendium));
        Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Checkpoints = checkpoints ?? new RuntimeCheckpointLogSnapshot();
        HostContext = RuntimePersistenceCollections.Dictionary(hostContext);
    }

    public int ContractVersion { get; }
    public SemanticVersion FrameworkVersion { get; }
    public IReadOnlyList<RuntimeActorSnapshot> Actors { get; }
    public RuntimePartyStockSnapshot PartyStock { get; }
    public RuntimeInventorySnapshot Inventory { get; }
    public RuntimeEquipmentSnapshot Equipment { get; }
    public RuntimeWalletSnapshot Wallet { get; }
    public RuntimeFieldSnapshot? Field { get; }
    public CompendiumStateSnapshot Compendium { get; }
    public RuntimeKnowledgeSnapshot Knowledge { get; }
    public RuntimeSessionProgressSnapshot Session { get; }
    public RuntimeCheckpointLogSnapshot Checkpoints { get; }
    public IReadOnlyDictionary<ContentId, string> HostContext { get; }
}

public sealed class RuntimeSaveValidator : IRuntimeSaveValidator
{
    public RuntimeSaveValidationResult Validate(RuntimeSaveGameSnapshot snapshot, GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);

        List<RuntimeSaveValidationDiagnostic> diagnostics = [];
        if (snapshot.ContractVersion != RuntimeSaveGameSnapshot.CurrentContractVersion)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.ContractVersionUnsupported,
                $"Runtime save contract version {snapshot.ContractVersion} is not supported.",
                Path: "$.contractVersion"));
        }

        Dictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors = [];
        for (int index = 0; index < snapshot.Actors.Count; index++)
        {
            RuntimeActorSnapshot actor = snapshot.Actors[index];
            RuntimeInstanceId instanceId = actor.Identity.InstanceId;
            if (!actors.TryAdd(instanceId, actor))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.DuplicateActorInstanceId,
                    $"Actor instance '{instanceId}' appears more than once.",
                    instanceId,
                    Path: $"$.actors[{index}].identity.instanceId"));
            }

            if (!catalog.Entities.ContainsKey(actor.Identity.EntityDefinitionId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogEntity,
                    $"Entity '{actor.Identity.EntityDefinitionId}' is not present in the catalog.",
                    instanceId,
                    actor.Identity.EntityDefinitionId,
                    $"$.actors[{index}].identity.entityDefinitionId"));
            }

            ValidateActorCatalogReferences(actor, catalog, diagnostics, index);
        }

        ValidatePartyReferences(snapshot.PartyStock, actors, diagnostics);
        ValidateInventory(snapshot.Inventory, catalog, diagnostics);
        ValidateEquipment(snapshot.Equipment, catalog, diagnostics);
        if (snapshot.Field is not null)
        {
            ValidateField(snapshot.Field, catalog, diagnostics);
        }
        ValidateCompendium(snapshot.Compendium, catalog, diagnostics);
        ValidateKnowledge(snapshot.Knowledge, actors, catalog, diagnostics);
        ValidateCheckpoints(snapshot.Checkpoints, actors, diagnostics);

        return new RuntimeSaveValidationResult(snapshot, diagnostics);
    }

    private static void ValidateActorCatalogReferences(
        RuntimeActorSnapshot actor,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        int actorIndex)
    {
        foreach (ContentId skillId in actor.Skills.LearnedSkillIds.Concat(actor.Skills.EquippedSkillIds).Distinct())
        {
            if (!catalog.Skills.ContainsKey(skillId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogSkill,
                    $"Skill '{skillId}' referenced by actor '{actor.Identity.InstanceId}' is not present in the catalog.",
                    actor.Identity.InstanceId,
                    skillId,
                    $"$.actors[{actorIndex}].skills"));
            }
        }

        foreach (RuntimeTimedStateSnapshot ailment in actor.BattleStatus.Ailments)
        {
            if (!catalog.Ailments.ContainsKey(ailment.Id))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogAilment,
                    $"Ailment '{ailment.Id}' referenced by actor '{actor.Identity.InstanceId}' is not present in the catalog.",
                    actor.Identity.InstanceId,
                    ailment.Id,
                    $"$.actors[{actorIndex}].battleStatus.ailments"));
            }
        }
    }

    private static void ValidatePartyReferences(
        RuntimePartyStockSnapshot partyStock,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        if (!actors.ContainsKey(partyStock.Owner.InstanceId))
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.MissingActorReference,
                $"Party-stock owner '{partyStock.Owner.InstanceId}' is not present in actors.",
                partyStock.Owner.InstanceId,
                Path: "$.partyStock.owner"));
        }

        ValidateActorReferenceList(partyStock.ActiveParty, actors, diagnostics, "$.partyStock.activeParty");
        ValidateActorReferenceList(partyStock.ReserveMembers, actors, diagnostics, "$.partyStock.reserveMembers");
        ValidateActorReferenceList(partyStock.PersonaStock, actors, diagnostics, "$.partyStock.personaStock");
        ValidateActorReferenceList(partyStock.DemonStock, actors, diagnostics, "$.partyStock.demonStock");

        if (partyStock.ActiveForm is not null && !actors.ContainsKey(partyStock.ActiveForm.InstanceId))
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.MissingActiveFormReference,
                $"Active form '{partyStock.ActiveForm.InstanceId}' is not present in actors.",
                partyStock.ActiveForm.InstanceId,
                Path: "$.partyStock.activeForm"));
        }
    }

    private static void ValidateActorReferenceList(
        IEnumerable<RuntimeActorReferenceSnapshot> references,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path)
    {
        int index = 0;
        foreach (RuntimeActorReferenceSnapshot reference in references)
        {
            if (!actors.ContainsKey(reference.InstanceId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingActorReference,
                    $"Actor reference '{reference.InstanceId}' is not present in actors.",
                    reference.InstanceId,
                    Path: $"{path}[{index}]"));
            }
            index++;
        }
    }

    private static void ValidateInventory(
        RuntimeInventorySnapshot inventory,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        foreach (ContentId itemId in inventory.ItemQuantities.Keys)
        {
            if (!catalog.Items.ContainsKey(itemId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogItem,
                    $"Item '{itemId}' is not present in the catalog.",
                    ContentId: itemId,
                    Path: "$.inventory.itemQuantities"));
            }
        }

        foreach (ContentId equipmentId in inventory.OwnedEquipmentIds.SelectMany(slot => slot.Value))
        {
            if (!catalog.Equipment.ContainsKey(equipmentId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogEquipment,
                    $"Equipment '{equipmentId}' is not present in the catalog.",
                    ContentId: equipmentId,
                    Path: "$.inventory.ownedEquipmentIds"));
            }
        }
    }

    private static void ValidateEquipment(
        RuntimeEquipmentSnapshot equipment,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        foreach (ContentId equipmentId in equipment.EquippedItemIds.Values)
        {
            if (!catalog.Equipment.ContainsKey(equipmentId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogEquipment,
                    $"Equipped item '{equipmentId}' is not present in the catalog.",
                    ContentId: equipmentId,
                    Path: "$.equipment.equippedItemIds"));
            }
        }
    }

    private static void ValidateField(
        RuntimeFieldSnapshot field,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        if (field.DungeonProgress is not null &&
            !catalog.Dungeons.ContainsKey(field.DungeonProgress.DungeonId))
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.MissingCatalogDungeon,
                $"Dungeon '{field.DungeonProgress.DungeonId}' is not present in the catalog.",
                ContentId: field.DungeonProgress.DungeonId,
                Path: "$.field.dungeonProgress.dungeonId"));
        }
    }

    private static void ValidateCompendium(
        CompendiumStateSnapshot compendium,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        for (int index = 0; index < compendium.Entries.Count; index++)
        {
            CompendiumEntrySnapshot entry = compendium.Entries[index];
            if (!catalog.Entities.ContainsKey(entry.SpeciesId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCompendiumEntity,
                    $"Compendium species '{entry.SpeciesId}' is not present in the catalog.",
                    ContentId: entry.SpeciesId,
                    Path: $"$.compendium.entries[{index}].speciesId"));
            }

            foreach (ContentId skillId in entry.SkillIds)
            {
                if (!catalog.Skills.ContainsKey(skillId))
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.MissingCatalogSkill,
                        $"Compendium skill '{skillId}' is not present in the catalog.",
                        ContentId: skillId,
                        Path: $"$.compendium.entries[{index}].skillIds"));
                }
            }
        }
    }

    private static void ValidateKnowledge(
        RuntimeKnowledgeSnapshot knowledge,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        HashSet<ContentId> knownActorEntities = actors.Values
            .Select(actor => actor.Identity.EntityDefinitionId)
            .ToHashSet();

        foreach (RuntimeElementalAffinityKnowledgeSnapshot entry in knowledge.ElementalAffinities)
        {
            ValidateKnowledgeEntity(entry.EntityId, knownActorEntities, catalog, diagnostics, "$.knowledge.elementalAffinities");
        }

        foreach (RuntimeAilmentResistanceKnowledgeSnapshot entry in knowledge.AilmentResistances)
        {
            ValidateKnowledgeEntity(entry.EntityId, knownActorEntities, catalog, diagnostics, "$.knowledge.ailmentResistances");
            if (!catalog.Ailments.ContainsKey(entry.AilmentId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogAilment,
                    $"Knowledge ailment '{entry.AilmentId}' is not present in the catalog.",
                    ContentId: entry.AilmentId,
                    Path: "$.knowledge.ailmentResistances"));
            }
        }

        foreach (RuntimeInstantDeathResistanceKnowledgeSnapshot entry in knowledge.InstantDeathResistances)
        {
            ValidateKnowledgeEntity(entry.EntityId, knownActorEntities, catalog, diagnostics, "$.knowledge.instantDeathResistances");
        }
    }

    private static void ValidateKnowledgeEntity(
        ContentId entityId,
        ISet<ContentId> knownActorEntities,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path)
    {
        if (!knownActorEntities.Contains(entityId) && !catalog.Entities.ContainsKey(entityId))
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.KnowledgeTargetMissing,
                $"Knowledge target entity '{entityId}' is not present in actors or catalog.",
                ContentId: entityId,
                Path: path));
        }
    }

    private static void ValidateCheckpoints(
        RuntimeCheckpointLogSnapshot checkpoints,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        long previous = -1;
        for (int index = 0; index < checkpoints.Entries.Count; index++)
        {
            RuntimeCheckpointEntrySnapshot entry = checkpoints.Entries[index];
            if (entry.Sequence < previous)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.InvalidCheckpoint,
                    "Checkpoint sequence must be nondecreasing.",
                    Path: $"$.checkpoints.entries[{index}].sequence"));
            }
            previous = entry.Sequence;

            if (entry.ActorId is RuntimeInstanceId actorId && !actors.ContainsKey(actorId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingActorReference,
                    $"Checkpoint actor '{actorId}' is not present in actors.",
                    actorId,
                    Path: $"$.checkpoints.entries[{index}].actorId"));
            }
        }
    }
}

internal static class RuntimePersistenceCollections
{
    public static IReadOnlyList<T> List<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly(values?.ToArray() ?? Array.Empty<T>());

    public static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(values ?? []));
}
