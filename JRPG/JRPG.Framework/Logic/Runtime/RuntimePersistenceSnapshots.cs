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
    DuplicateContentPack,
    MissingContentPack,
    ContentPackVersionMismatch,
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
    DuplicateCompendiumEntity,
    CompendiumEntityNotEligible,
    DuplicateCompendiumLearnedSkill,
    DuplicateCompendiumEquippedSkill,
    InvalidCompendiumStatValue,
    MissingCompendiumStat,
    UnknownCompendiumStat,
    CompendiumEquippedSkillNotLearned,
    KnowledgeTargetMissing,
    InvalidCheckpoint,
    DuplicatePartyStockReference,
    ActivePartyCapacityExceeded,
    DemonStockCapacityExceeded,
    ActiveFormDuplicatedInPersonaStock,
    PartyStockIdentityCollision,
    ActorReferenceEntityMismatch,
    PersonaStockCapacityExceeded,
    DuplicateElementalAffinityKnowledge,
    DuplicateAilmentResistanceKnowledge,
    DuplicateInstantDeathResistanceKnowledge,
    ActorKindMismatch,
    DuplicateActorResource,
    DuplicateActorLearnedSkill,
    DuplicateActorEquippedSkill,
    ActorEquippedSkillNotLearned,
    DuplicateActorCapability,
    DuplicateActorAilment,
    DuplicateActorStatus,
    DuplicateActorStatStage,
    DuplicateActorCharge,
    DuplicateActorShield,
    DuplicateActorAffinityBreak,
    InvalidActorAffinityBreakElement,
    DuplicateActorAffinityOverride,
    DuplicateActorAnalysisTarget,
    DuplicateActorAnalysisLayer,
    DuplicatePassiveSkillState,
    PassiveStateSkillNotLoaded,
    DuplicatePassiveActivation,
    PassiveActivationSkillNotLoaded,
    DuplicateActorFormReference,
    EquippedEquipmentNotOwned,
    EquipmentSlotMismatch,
    EquipmentAssignedToMultipleActors,
    ActorStatStageOutOfRange
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
    public const int CurrentContractVersion = 6;

    public RuntimeSaveGameSnapshot(
        SemanticVersion frameworkVersion,
        IEnumerable<ContentPackIdentity> contentPacks,
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
        ContentPacks = RuntimePersistenceCollections.List(contentPacks);
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
    public IReadOnlyList<ContentPackIdentity> ContentPacks { get; }
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
    private readonly IStockCapacityPolicy _stockCapacityPolicy;

    public RuntimeSaveValidator(IStockCapacityPolicy? stockCapacityPolicy = null)
    {
        _stockCapacityPolicy = stockCapacityPolicy ?? NoLimitStockCapacityPolicy.Instance;
    }

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

        ValidateContentPacks(snapshot.ContentPacks, catalog, diagnostics);

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

            if (!catalog.Entities.TryGetValue(actor.Identity.EntityDefinitionId, out EntityDefinition? entity))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogEntity,
                    $"Entity '{actor.Identity.EntityDefinitionId}' is not present in the catalog.",
                    instanceId,
                    actor.Identity.EntityDefinitionId,
                    $"$.actors[{index}].identity.entityDefinitionId"));
            }
            else if (actor.Identity.ActorKindId != entity.EntityKindId)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ActorKindMismatch,
                    $"Actor kind '{actor.Identity.ActorKindId}' does not match entity kind '{entity.EntityKindId}'.",
                    instanceId,
                    actor.Identity.EntityDefinitionId,
                    $"$.actors[{index}].identity.actorKindId"));
            }

            ValidateActorRestoreContract(actor, catalog, diagnostics, index);
        }

        for (int index = 0; index < snapshot.Actors.Count; index++)
        {
            ValidateActorFormReferences(snapshot.Actors[index], actors, diagnostics, index);
        }

        ValidatePartyReferences(snapshot.PartyStock, actors, _stockCapacityPolicy, diagnostics);
        ValidateInventory(snapshot.Inventory, catalog, diagnostics);
        ValidateEquipment(
            snapshot.Equipment,
            snapshot.Inventory,
            catalog,
            diagnostics,
            "$.equipment",
            null);
        ValidateActorEquipment(snapshot.Actors, snapshot.Inventory, catalog, diagnostics);
        if (snapshot.Field is not null)
        {
            ValidateField(snapshot.Field, catalog, diagnostics);
        }
        ValidateCompendium(snapshot.Compendium, catalog, diagnostics);
        ValidateKnowledge(snapshot.Knowledge, actors, catalog, diagnostics);
        ValidateCheckpoints(snapshot.Checkpoints, actors, diagnostics);

        return new RuntimeSaveValidationResult(snapshot, diagnostics);
    }

    private static void ValidateContentPacks(
        IReadOnlyList<ContentPackIdentity> contentPacks,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        var catalogPacks = catalog.ContentPacks.ToDictionary(pack => pack.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < contentPacks.Count; index++)
        {
            ContentPackIdentity pack = contentPacks[index];
            string path = $"$.contentPacks[{index}]";
            if (!seen.Add(pack.Id))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.DuplicateContentPack,
                    $"Content pack '{pack.Id}' appears more than once.",
                    Path: path + ".id"));
                continue;
            }

            if (!catalogPacks.TryGetValue(pack.Id, out ContentPackIdentity? catalogPack))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingContentPack,
                    $"Content pack '{pack.Id}' is not loaded in the current catalog.",
                    Path: path + ".id"));
                continue;
            }

            if (catalogPack.Version != pack.Version)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ContentPackVersionMismatch,
                    $"Content pack '{pack.Id}' was saved with version {pack.Version}, but the current catalog loaded version {catalogPack.Version}.",
                    Path: path + ".version"));
            }
        }

        for (int index = 0; index < catalog.ContentPacks.Count; index++)
        {
            ContentPackIdentity catalogPack = catalog.ContentPacks[index];
            if (seen.Contains(catalogPack.Id))
            {
                continue;
            }

            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.MissingContentPack,
                $"Current catalog pack '{catalogPack.Id}' is not recorded by the save.",
                Path: "$.contentPacks"));
        }
    }

    private static void ValidateActorRestoreContract(
        RuntimeActorSnapshot actor,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        int actorIndex)
    {
        HashSet<ContentId> equippedPassiveSkillIds = actor.Skills.EquippedSkillIds
            .Where(skillId =>
                catalog.Skills.TryGetValue(skillId, out SkillDefinition? skill) &&
                skill.Activation == SkillActivation.Passive)
            .ToHashSet();
        IReadOnlyList<RuntimeActorSnapshotIntegrityDiagnostic> integrityDiagnostics =
            RuntimeActorSnapshotIntegrity.ValidateForRestore(
                actor,
                equippedPassiveSkillIds,
                catalog.Ailments.Keys);
        foreach (RuntimeActorSnapshotIntegrityDiagnostic issue in integrityDiagnostics)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                SaveCode(issue.Code),
                issue.Message,
                actor.Identity.InstanceId,
                issue.ContentId,
                ActorPath(actorIndex, issue.Path)));
        }

        ValidateActorSkillCatalogReferences(
            actor.Skills.LearnedSkillIds,
            catalog,
            diagnostics,
            actor.Identity.InstanceId,
            $"$.actors[{actorIndex}].skills.learnedSkillIds");
        ValidateActorSkillCatalogReferences(
            actor.Skills.EquippedSkillIds,
            catalog,
            diagnostics,
            actor.Identity.InstanceId,
            $"$.actors[{actorIndex}].skills.equippedSkillIds");
    }

    private static void ValidateActorSkillCatalogReferences(
        IReadOnlyList<ContentId> skillIds,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        RuntimeInstanceId instanceId,
        string path)
    {
        for (int index = 0; index < skillIds.Count; index++)
        {
            ContentId skillId = skillIds[index];
            if (!catalog.Skills.ContainsKey(skillId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogSkill,
                    $"Skill '{skillId}' referenced by actor '{instanceId}' is not present in the catalog.",
                    instanceId,
                    skillId,
                    $"{path}[{index}]"));
            }
        }
    }

    private static RuntimeSaveValidationCode SaveCode(RuntimeActorSnapshotIntegrityCode code) =>
        code switch
        {
            RuntimeActorSnapshotIntegrityCode.DuplicateResource => RuntimeSaveValidationCode.DuplicateActorResource,
            RuntimeActorSnapshotIntegrityCode.DuplicateLearnedSkill => RuntimeSaveValidationCode.DuplicateActorLearnedSkill,
            RuntimeActorSnapshotIntegrityCode.DuplicateEquippedSkill => RuntimeSaveValidationCode.DuplicateActorEquippedSkill,
            RuntimeActorSnapshotIntegrityCode.EquippedSkillNotLearned => RuntimeSaveValidationCode.ActorEquippedSkillNotLearned,
            RuntimeActorSnapshotIntegrityCode.DuplicateCapability => RuntimeSaveValidationCode.DuplicateActorCapability,
            RuntimeActorSnapshotIntegrityCode.DuplicateAilment => RuntimeSaveValidationCode.DuplicateActorAilment,
            RuntimeActorSnapshotIntegrityCode.MissingAilmentDefinition => RuntimeSaveValidationCode.MissingCatalogAilment,
            RuntimeActorSnapshotIntegrityCode.DuplicateStatus => RuntimeSaveValidationCode.DuplicateActorStatus,
            RuntimeActorSnapshotIntegrityCode.DuplicateStatStage => RuntimeSaveValidationCode.DuplicateActorStatStage,
            RuntimeActorSnapshotIntegrityCode.DuplicateCharge => RuntimeSaveValidationCode.DuplicateActorCharge,
            RuntimeActorSnapshotIntegrityCode.DuplicateShield => RuntimeSaveValidationCode.DuplicateActorShield,
            RuntimeActorSnapshotIntegrityCode.DuplicateAffinityBreak => RuntimeSaveValidationCode.DuplicateActorAffinityBreak,
            RuntimeActorSnapshotIntegrityCode.InvalidAffinityBreakElement => RuntimeSaveValidationCode.InvalidActorAffinityBreakElement,
            RuntimeActorSnapshotIntegrityCode.DuplicateAffinityOverride => RuntimeSaveValidationCode.DuplicateActorAffinityOverride,
            RuntimeActorSnapshotIntegrityCode.DuplicateAnalysisTarget => RuntimeSaveValidationCode.DuplicateActorAnalysisTarget,
            RuntimeActorSnapshotIntegrityCode.DuplicateAnalysisLayer => RuntimeSaveValidationCode.DuplicateActorAnalysisLayer,
            RuntimeActorSnapshotIntegrityCode.DuplicatePassiveSkillState => RuntimeSaveValidationCode.DuplicatePassiveSkillState,
            RuntimeActorSnapshotIntegrityCode.PassiveSkillStateNotLoaded => RuntimeSaveValidationCode.PassiveStateSkillNotLoaded,
            RuntimeActorSnapshotIntegrityCode.DuplicatePassiveActivation => RuntimeSaveValidationCode.DuplicatePassiveActivation,
            RuntimeActorSnapshotIntegrityCode.PassiveActivationSkillNotLoaded => RuntimeSaveValidationCode.PassiveActivationSkillNotLoaded,
            RuntimeActorSnapshotIntegrityCode.StatStageOutOfRange => RuntimeSaveValidationCode.ActorStatStageOutOfRange,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown actor snapshot integrity code.")
        };

    private static string ActorPath(int actorIndex, string relativePath) =>
        $"$.actors[{actorIndex}]" + relativePath[1..];

    private static void ValidateActorFormReferences(
        RuntimeActorSnapshot actor,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        int actorIndex)
    {
        string formsPath = $"$.actors[{actorIndex}].forms";
        if (actor.Forms.ActiveForm is RuntimeActorReferenceSnapshot activeForm)
        {
            ValidateActorReference(
                activeForm,
                actors,
                diagnostics,
                formsPath + ".activeForm",
                RuntimeSaveValidationCode.MissingActiveFormReference,
                $"Actor '{actor.Identity.InstanceId}' active form");
        }

        HashSet<RuntimeInstanceId> personaIds = ValidateActorFormReferenceList(
            actor,
            actor.Forms.PersonaStock,
            actors,
            diagnostics,
            formsPath + ".personaStock");
        ValidateActorFormReferenceList(
            actor,
            actor.Forms.DemonStock,
            actors,
            diagnostics,
            formsPath + ".demonStock",
            personaIds);
    }

    private static HashSet<RuntimeInstanceId> ValidateActorFormReferenceList(
        RuntimeActorSnapshot owner,
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path,
        ISet<RuntimeInstanceId>? conflictingStockIds = null)
    {
        var seen = new HashSet<RuntimeInstanceId>();
        for (int index = 0; index < references.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = references[index];
            string referencePath = $"{path}[{index}]";
            ValidateActorReference(
                reference,
                actors,
                diagnostics,
                referencePath,
                RuntimeSaveValidationCode.MissingActorReference,
                $"Actor '{owner.Identity.InstanceId}' form-stock reference");

            if (!seen.Add(reference.InstanceId) || conflictingStockIds?.Contains(reference.InstanceId) == true)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.DuplicateActorFormReference,
                    $"Actor form reference '{reference.InstanceId}' appears in more than one position or stock role.",
                    reference.InstanceId,
                    reference.EntityDefinitionId,
                    referencePath));
            }
        }

        return seen;
    }

    private static void ValidatePartyReferences(
        RuntimePartyStockSnapshot partyStock,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        IStockCapacityPolicy stockCapacityPolicy,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        ValidateActorReference(
            partyStock.Owner,
            actors,
            diagnostics,
            "$.partyStock.owner",
            RuntimeSaveValidationCode.MissingActorReference,
            "Party-stock owner");

        if (partyStock.ActiveParty.Count > partyStock.MaxActivePartySize)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.ActivePartyCapacityExceeded,
                $"Active party has {partyStock.ActiveParty.Count} members, exceeding the maximum of {partyStock.MaxActivePartySize}.",
                Path: "$.partyStock.activeParty"));
        }

        int stockCapacity = stockCapacityPolicy.GetCapacity(partyStock.OwnerLevel);
        if (partyStock.DemonStock.Count > stockCapacity)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.DemonStockCapacityExceeded,
                $"Demon stock has {partyStock.DemonStock.Count} entries, exceeding the capacity of {stockCapacity}.",
                Path: "$.partyStock.demonStock"));
        }

        if (partyStock.PersonaStock.Count > stockCapacity)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.PersonaStockCapacityExceeded,
                $"Persona stock has {partyStock.PersonaStock.Count} entries, exceeding the capacity of {stockCapacity}.",
                Path: "$.partyStock.personaStock"));
        }

        ValidateActorReferenceList(partyStock.ActiveParty, actors, diagnostics, "$.partyStock.activeParty");
        ValidateActorReferenceList(partyStock.ReserveMembers, actors, diagnostics, "$.partyStock.reserveMembers");
        ValidateActorReferenceList(partyStock.PersonaStock, actors, diagnostics, "$.partyStock.personaStock");
        ValidateActorReferenceList(partyStock.DemonStock, actors, diagnostics, "$.partyStock.demonStock");
        ValidateNoOverlap(
            partyStock.ActiveParty,
            partyStock.ReserveMembers,
            diagnostics,
            "$.partyStock.reserveMembers",
            "Active party and reserve party cannot contain the same actor.");

        if (partyStock.ActiveForm is not null)
        {
            ValidateActorReference(
                partyStock.ActiveForm,
                actors,
                diagnostics,
                "$.partyStock.activeForm",
                RuntimeSaveValidationCode.MissingActiveFormReference,
                "Active form");

            for (int index = 0; index < partyStock.PersonaStock.Count; index++)
            {
                RuntimeActorReferenceSnapshot persona = partyStock.PersonaStock[index];
                if (persona.InstanceId != partyStock.ActiveForm.InstanceId)
                {
                    continue;
                }

                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ActiveFormDuplicatedInPersonaStock,
                    $"Active form '{persona.InstanceId}' also appears in Persona stock.",
                    persona.InstanceId,
                    Path: $"$.partyStock.personaStock[{index}]"));
            }
        }

        ValidatePartyStockIdentityOverlaps(partyStock, diagnostics);
    }

    private static void ValidateActorReferenceList(
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path)
    {
        var seen = new HashSet<RuntimeInstanceId>();
        for (int index = 0; index < references.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = references[index];
            ValidateActorReference(
                reference,
                actors,
                diagnostics,
                $"{path}[{index}]",
                RuntimeSaveValidationCode.MissingActorReference,
                "Actor reference");

            if (!seen.Add(reference.InstanceId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.DuplicatePartyStockReference,
                    $"Actor reference '{reference.InstanceId}' appears more than once in '{path}'.",
                    reference.InstanceId,
                    Path: $"{path}[{index}]"));
            }
        }
    }

    private static void ValidateActorReference(
        RuntimeActorReferenceSnapshot reference,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path,
        RuntimeSaveValidationCode missingCode,
        string referenceLabel)
    {
        if (!actors.TryGetValue(reference.InstanceId, out RuntimeActorSnapshot? actor))
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                missingCode,
                $"{referenceLabel} '{reference.InstanceId}' is not present in actors.",
                reference.InstanceId,
                Path: path));
            return;
        }

        if (actor.Identity.EntityDefinitionId == reference.EntityDefinitionId)
        {
            return;
        }

        diagnostics.Add(new RuntimeSaveValidationDiagnostic(
            RuntimeSaveValidationCode.ActorReferenceEntityMismatch,
            $"{referenceLabel} '{reference.InstanceId}' identifies entity '{reference.EntityDefinitionId}', " +
            $"but its actor snapshot identifies '{actor.Identity.EntityDefinitionId}'.",
            reference.InstanceId,
            reference.EntityDefinitionId,
            path + ".entityDefinitionId"));
    }

    private static void ValidateNoOverlap(
        IReadOnlyList<RuntimeActorReferenceSnapshot> first,
        IReadOnlyList<RuntimeActorReferenceSnapshot> second,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string secondPath,
        string message)
    {
        HashSet<RuntimeInstanceId> firstIds = first
            .Select(reference => reference.InstanceId)
            .ToHashSet();
        for (int index = 0; index < second.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = second[index];
            if (!firstIds.Contains(reference.InstanceId))
            {
                continue;
            }

            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.DuplicatePartyStockReference,
                message,
                reference.InstanceId,
                Path: $"{secondPath}[{index}]"));
        }
    }

    private static void ValidatePartyStockIdentityOverlaps(
        RuntimePartyStockSnapshot partyStock,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        foreach (IGrouping<RuntimeInstanceId, RuntimePartyStockReferenceOccurrence> group in
                 RuntimePartyStockIdentityRules.Enumerate(partyStock)
                     .GroupBy(occurrence => occurrence.Reference.InstanceId))
        {
            RuntimePartyStockReferenceOccurrence[] occurrences = group.ToArray();
            if (occurrences.Length < 2)
            {
                continue;
            }

            HashSet<RuntimePartyStockReferenceRole> roles = occurrences
                .Select(occurrence => occurrence.Role)
                .ToHashSet();
            for (int currentIndex = 1; currentIndex < occurrences.Length; currentIndex++)
            {
                RuntimePartyStockReferenceOccurrence current = occurrences[currentIndex];
                RuntimePartyStockReferenceOccurrence? conflict = null;
                for (int previousIndex = 0; previousIndex < currentIndex; previousIndex++)
                {
                    RuntimePartyStockReferenceOccurrence previous = occurrences[previousIndex];
                    if (HasDedicatedOverlapDiagnostic(previous.Role, current.Role) ||
                        RuntimePartyStockIdentityRules.IsIntentionalOverlap(previous.Role, current.Role, roles))
                    {
                        continue;
                    }

                    conflict = previous;
                    break;
                }

                if (conflict is not RuntimePartyStockReferenceOccurrence conflicting)
                {
                    continue;
                }

                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.PartyStockIdentityCollision,
                    $"Runtime instance '{group.Key}' is referenced as both '{conflicting.Role}' and " +
                    $"'{current.Role}', which is not an allowed party/stock overlap.",
                    group.Key,
                    Path: current.Path));
            }
        }
    }

    private static bool HasDedicatedOverlapDiagnostic(
        RuntimePartyStockReferenceRole first,
        RuntimePartyStockReferenceRole second) =>
        first == second ||
        IsRolePair(first, second, RuntimePartyStockReferenceRole.ActiveParty, RuntimePartyStockReferenceRole.ReserveMember) ||
        IsRolePair(first, second, RuntimePartyStockReferenceRole.ActiveForm, RuntimePartyStockReferenceRole.PersonaStock);

    private static bool IsRolePair(
        RuntimePartyStockReferenceRole first,
        RuntimePartyStockReferenceRole second,
        RuntimePartyStockReferenceRole expectedFirst,
        RuntimePartyStockReferenceRole expectedSecond) =>
        (first == expectedFirst && second == expectedSecond) ||
        (first == expectedSecond && second == expectedFirst);

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

        foreach ((EquipmentSlot slot, IReadOnlyList<ContentId> equipmentIds) in
                 inventory.OwnedEquipmentIds.OrderBy(pair => pair.Key))
        {
            for (int index = 0; index < equipmentIds.Count; index++)
            {
                ContentId equipmentId = equipmentIds[index];
                string path = $"$.inventory.ownedEquipmentIds.{SlotPath(slot)}[{index}]";
                if (!catalog.Equipment.TryGetValue(equipmentId, out EquipmentDefinition? definition))
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.MissingCatalogEquipment,
                        $"Equipment '{equipmentId}' is not present in the catalog.",
                        ContentId: equipmentId,
                        Path: path));
                }
                else if (definition.Slot != slot)
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.EquipmentSlotMismatch,
                        $"Equipment '{equipmentId}' is stored as '{slot}', but its catalog slot is '{definition.Slot}'.",
                        ContentId: equipmentId,
                        Path: path));
                }
            }
        }
    }

    private static void ValidateEquipment(
        RuntimeEquipmentSnapshot equipment,
        RuntimeInventorySnapshot inventory,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path,
        RuntimeInstanceId? actorInstanceId)
    {
        foreach ((EquipmentSlot slot, ContentId equipmentId) in equipment.EquippedItemIds.OrderBy(pair => pair.Key))
        {
            string equipmentPath = $"{path}.equippedItemIds.{SlotPath(slot)}";
            if (!catalog.Equipment.TryGetValue(equipmentId, out EquipmentDefinition? definition))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogEquipment,
                    $"Equipped item '{equipmentId}' is not present in the catalog.",
                    actorInstanceId,
                    ContentId: equipmentId,
                    Path: equipmentPath));
            }
            else if (definition.Slot != slot)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.EquipmentSlotMismatch,
                    $"Equipped item '{equipmentId}' is assigned to '{slot}', but its catalog slot is '{definition.Slot}'.",
                    actorInstanceId,
                    equipmentId,
                    equipmentPath));
            }

            if (!inventory.OwnsEquipment(equipmentId, slot))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.EquippedEquipmentNotOwned,
                    $"Equipped item '{equipmentId}' is not owned in slot '{slot}'.",
                    actorInstanceId,
                    equipmentId,
                    equipmentPath));
            }
        }
    }

    private static void ValidateActorEquipment(
        IReadOnlyList<RuntimeActorSnapshot> actors,
        RuntimeInventorySnapshot inventory,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        var assignments = new Dictionary<ContentId, RuntimeInstanceId>();
        for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
        {
            RuntimeActorSnapshot actor = actors[actorIndex];
            string equipmentPath = $"$.actors[{actorIndex}].equipment";
            ValidateEquipment(
                actor.Equipment,
                inventory,
                catalog,
                diagnostics,
                equipmentPath,
                actor.Identity.InstanceId);

            foreach ((EquipmentSlot slot, ContentId equipmentId) in actor.Equipment.EquippedItemIds.OrderBy(pair => pair.Key))
            {
                if (assignments.TryAdd(equipmentId, actor.Identity.InstanceId) ||
                    assignments[equipmentId] == actor.Identity.InstanceId)
                {
                    continue;
                }

                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.EquipmentAssignedToMultipleActors,
                    $"Equipment '{equipmentId}' is assigned to more than one actor.",
                    actor.Identity.InstanceId,
                    equipmentId,
                    $"{equipmentPath}.equippedItemIds.{SlotPath(slot)}"));
            }
        }
    }

    private static string SlotPath(EquipmentSlot slot) => slot.ToString().ToLowerInvariant();

    private static void ValidateField(
        RuntimeFieldSnapshot field,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        if (field.DungeonTraversal is not null &&
            !catalog.Dungeons.ContainsKey(field.DungeonTraversal.DungeonId))
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.MissingCatalogDungeon,
                $"Dungeon '{field.DungeonTraversal.DungeonId}' is not present in the catalog.",
                ContentId: field.DungeonTraversal.DungeonId,
                Path: "$.field.dungeonTraversal.dungeonId"));
        }
    }

    private static void ValidateCompendium(
        CompendiumStateSnapshot compendium,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        var seenEntityIds = new HashSet<ContentId>();
        for (int index = 0; index < compendium.Entries.Count; index++)
        {
            CompendiumEntrySnapshot entry = compendium.Entries[index];
            string entryPath = $"$.compendium.entries[{index}]";
            if (!seenEntityIds.Add(entry.EntityId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.DuplicateCompendiumEntity,
                    $"Compendium entity '{entry.EntityId}' appears more than once.",
                    ContentId: entry.EntityId,
                    Path: entryPath + ".entityId"));
            }

            if (!catalog.Entities.TryGetValue(entry.EntityId, out EntityDefinition? entity))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCompendiumEntity,
                    $"Compendium entity '{entry.EntityId}' is not present in the catalog.",
                    ContentId: entry.EntityId,
                    Path: entryPath + ".entityId"));
            }
            else if (!entity.Capabilities.CompendiumEligible)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.CompendiumEntityNotEligible,
                    $"Entity '{entry.EntityId}' is not eligible for Compendium storage.",
                    ContentId: entry.EntityId,
                    Path: entryPath + ".entityId"));
            }

            IReadOnlyList<CompendiumEntryIntegrityDiagnostic> entryDiagnostics =
                CompendiumEntryIntegrity.Validate(entry, entity, catalog);
            foreach (CompendiumEntryIntegrityDiagnostic issue in entryDiagnostics)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    SaveCode(issue.Code),
                    issue.Message,
                    ContentId: issue.ContentId,
                    Path: CompendiumPath(entryPath, issue)));
            }
        }
    }

    private static RuntimeSaveValidationCode SaveCode(CompendiumEntryIntegrityCode code) =>
        code switch
        {
            CompendiumEntryIntegrityCode.DuplicateLearnedSkill =>
                RuntimeSaveValidationCode.DuplicateCompendiumLearnedSkill,
            CompendiumEntryIntegrityCode.DuplicateEquippedSkill =>
                RuntimeSaveValidationCode.DuplicateCompendiumEquippedSkill,
            CompendiumEntryIntegrityCode.InvalidStatValue =>
                RuntimeSaveValidationCode.InvalidCompendiumStatValue,
            CompendiumEntryIntegrityCode.MissingStat =>
                RuntimeSaveValidationCode.MissingCompendiumStat,
            CompendiumEntryIntegrityCode.UnknownStat =>
                RuntimeSaveValidationCode.UnknownCompendiumStat,
            CompendiumEntryIntegrityCode.MissingSkill =>
                RuntimeSaveValidationCode.MissingCatalogSkill,
            CompendiumEntryIntegrityCode.EquippedSkillNotLearned =>
                RuntimeSaveValidationCode.CompendiumEquippedSkillNotLearned,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown Compendium integrity code.")
        };

    private static string CompendiumPath(
        string entryPath,
        CompendiumEntryIntegrityDiagnostic diagnostic) =>
        diagnostic.Field switch
        {
            CompendiumEntryIntegrityField.Stats when
                diagnostic.Code == CompendiumEntryIntegrityCode.MissingStat => entryPath + ".stats",
            CompendiumEntryIntegrityField.Stats =>
                $"{entryPath}.stats['{diagnostic.ContentId}']",
            CompendiumEntryIntegrityField.LearnedSkills =>
                $"{entryPath}.skillIds[{diagnostic.Index}]",
            CompendiumEntryIntegrityField.EquippedSkills =>
                $"{entryPath}.equippedSkillIds[{diagnostic.Index}]",
            _ => entryPath
        };

    private static void ValidateKnowledge(
        RuntimeKnowledgeSnapshot knowledge,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        HashSet<ContentId> knownActorEntities = actors.Values
            .Select(actor => actor.Identity.EntityDefinitionId)
            .ToHashSet();

        foreach (RuntimeKnowledgeDuplicate duplicate in RuntimeKnowledgeIntegrity.FindDuplicates(knowledge))
        {
            RuntimeSaveValidationCode code = duplicate.Collection switch
            {
                RuntimeKnowledgeCollection.ElementalAffinities =>
                    RuntimeSaveValidationCode.DuplicateElementalAffinityKnowledge,
                RuntimeKnowledgeCollection.AilmentResistances =>
                    RuntimeSaveValidationCode.DuplicateAilmentResistanceKnowledge,
                RuntimeKnowledgeCollection.InstantDeathResistances =>
                    RuntimeSaveValidationCode.DuplicateInstantDeathResistanceKnowledge,
                _ => throw new InvalidOperationException(
                    $"Unsupported knowledge collection '{duplicate.Collection}'.")
            };
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                code,
                $"Knowledge contains a duplicate key for {duplicate.KeyDescription}.",
                ContentId: duplicate.EntityId,
                Path: duplicate.SavePath));
        }

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
