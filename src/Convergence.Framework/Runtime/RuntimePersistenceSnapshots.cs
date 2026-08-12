using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Fusion;
using Convergence.Internal;
using Convergence.Execution;

namespace Convergence.Runtime;

public enum RuntimeSaveValidationCode
{
    ContractVersionUnsupported,
    DuplicateContentPack,
    MissingContentPack,
    ContentPackVersionMismatch,
    DuplicateActorInstanceId,
    MissingActorReference,
    MissingActiveHostedEntityReference,
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
    DuplicatePartyRosterReference,
    ActivePartyCapacityExceeded,
    CompanionRosterCapacityExceeded,
    ActiveHostedEntityNotOwned,
    ActiveHostedEntityReferenceMismatch,
    PartyRosterIdentityCollision,
    ActorReferenceEntityMismatch,
    HostedEntityRosterCapacityExceeded,
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
    DuplicateActorCharge = 44,
    DuplicateActorShield,
    DuplicateActorAffinityBreak,
    InvalidActorAffinityBreakElement,
    DuplicateActorAffinityOverride,
    DuplicatePassiveSkillState = 51,
    PassiveStateSkillNotLoaded,
    DuplicatePassiveActivation,
    PassiveActivationSkillNotLoaded,
    EquippedEquipmentNotOwned,
    EquipmentSlotMismatch,
    EquipmentAssignedToMultipleActors,
    ActorBaseStatOutOfRange = 59,
    ActorEffectiveStatOutOfRange,
    ActorBaseResourceValueOutOfRange,
    ActorRetainedDurationKindInvalid,
    ActorTurnDurationValueOutOfRange,
    ActorTurnDurationTickEventIdInvalid,
    ActorPhaseDurationPhaseIdInvalid,
    InvalidRuntimeInstanceId,
    InvalidContentId,
    UndefinedEnumValue,
    DuplicateActorPendingSkillChoiceToken,
    DuplicateActorPendingSkill,
    ActorPendingSkillAlreadyLearned,
    ActorPendingSkillUnlockMismatch,
    ActorPendingSkillLevelUnavailable,
    ActorMoveListCapacityRejected,
    ActorStatModifierPolicyResolverMissing,
    ActorStatModifierPolicyBindingRejected,
    ActorStatModifierStateInvalid,
    ActorChargePolicyResolverMissing,
    ActorChargePolicyBindingRejected,
    ActorChargeStateInvalid,
    PassiveActivationTriggerIndexInvalid,
    PassiveActivationEventMismatch,
    MissingPassiveSkillState = 83,
    ConflictingActorAilmentExclusivityGroup = 84,
    DuplicateAnalyzedDefenseKnowledge = 85,
    InvalidAnalyzedDefenseField = 86,
    CombatProfileSourceMissing = 87,
    CombatProfileSourceEntityMismatch = 88,
    IntrinsicElementKnowledgeNotStorable = 89,
    DuplicateEquipmentInstanceId = 90,
    EquipmentInstanceIdCollidesWithActor = 91,
    MissingCatalogShop = 92,
    MissingShopOffer = 93,
    DuplicateShopStockEntry = 94,
    NegativeShopStockQuantity = 95,
    MissingShopStockEntry = 96,
    UnexpectedShopStockEntry = 97
}

public sealed record RuntimeSaveValidationDiagnostic(
    RuntimeSaveValidationCode Code,
    string Message,
    RuntimeInstanceId? InstanceId = null,
    ContentId? ContentId = null,
    string? Path = null,
    StatModifierDiagnosticCode? StatModifierCode = null,
    ChargePolicyDiagnosticCode? ChargePolicyCode = null);

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
        : this(elementalAffinities, ailmentResistances, instantDeathResistances, analyzedDefenses: null)
    {
    }

    public RuntimeKnowledgeSnapshot(
        IEnumerable<RuntimeElementalAffinityKnowledgeSnapshot>? elementalAffinities,
        IEnumerable<RuntimeAilmentResistanceKnowledgeSnapshot>? ailmentResistances,
        IEnumerable<RuntimeInstantDeathResistanceKnowledgeSnapshot>? instantDeathResistances,
        IEnumerable<RuntimeAnalyzedDefenseKnowledgeSnapshot>? analyzedDefenses)
    {
        ElementalAffinities = RuntimePersistenceCollections.List(elementalAffinities);
        AilmentResistances = RuntimePersistenceCollections.List(ailmentResistances);
        InstantDeathResistances = RuntimePersistenceCollections.List(instantDeathResistances);
        AnalyzedDefenses = RuntimePersistenceCollections.List(analyzedDefenses);
    }

    public IReadOnlyList<RuntimeElementalAffinityKnowledgeSnapshot> ElementalAffinities { get; }
    public IReadOnlyList<RuntimeAilmentResistanceKnowledgeSnapshot> AilmentResistances { get; }
    public IReadOnlyList<RuntimeInstantDeathResistanceKnowledgeSnapshot> InstantDeathResistances { get; }
    public IReadOnlyList<RuntimeAnalyzedDefenseKnowledgeSnapshot> AnalyzedDefenses { get; }
}

public sealed record RuntimeAnalyzedDefenseKnowledgeSnapshot
{
    public RuntimeAnalyzedDefenseKnowledgeSnapshot(
        ContentId entityId,
        IEnumerable<BattleAnalysisField> disclosedFields)
    {
        if (!entityId.IsValid)
        {
            throw new ArgumentException("Analyzed defense knowledge requires a valid entity ID.", nameof(entityId));
        }

        BattleAnalysisField[] fields = (disclosedFields ??
            throw new ArgumentNullException(nameof(disclosedFields))).ToArray();
        if (fields.Length == 0 ||
            fields.Any(field => !IsDefenseField(field)) ||
            fields.Distinct().Count() != fields.Length)
        {
            throw new ArgumentException(
                "Analyzed defense knowledge requires unique defense disclosure fields.",
                nameof(disclosedFields));
        }

        EntityId = entityId;
        DisclosedFields = RuntimePersistenceCollections.List(fields.Order());
    }

    public ContentId EntityId { get; }
    public IReadOnlyList<BattleAnalysisField> DisclosedFields { get; }

    private static bool IsDefenseField(BattleAnalysisField field) => field is
        BattleAnalysisField.ElementalAffinities or
        BattleAnalysisField.AilmentResistances or
        BattleAnalysisField.InstantDeathResistances;
}

public sealed record RuntimeElementalAffinityKnowledgeSnapshot(
    ContentId EntityId,
    DamageElement Element,
    ElementalAffinity Affinity)
{
    public DamageElement Element { get; init; } =
        RequireStorableElement(Element);
    public ElementalAffinity Affinity { get; init; } =
        EnumDomain.RequireDefined(Affinity, nameof(Affinity));

    private static DamageElement RequireStorableElement(DamageElement element)
    {
        EnumDomain.RequireDefined(element, nameof(Element));
        return element == DamageElement.Almighty
            ? throw new ArgumentOutOfRangeException(
                nameof(Element),
                element,
                "Almighty always has Normal affinity and cannot be stored as affinity knowledge.")
            : element;
    }
}

public sealed record RuntimeAilmentResistanceKnowledgeSnapshot(
    ContentId EntityId,
    ContentId AilmentId,
    ResistanceLevel Resistance)
{
    public ResistanceLevel Resistance { get; init; } =
        EnumDomain.RequireDefined(Resistance, nameof(Resistance));
}

public sealed record RuntimeInstantDeathResistanceKnowledgeSnapshot(
    ContentId EntityId,
    InstantDeathChannel Channel,
    ResistanceLevel Resistance)
{
    public InstantDeathChannel Channel { get; init; } =
        EnumDomain.RequireDefined(Channel, nameof(Channel));
    public ResistanceLevel Resistance { get; init; } =
        EnumDomain.RequireDefined(Resistance, nameof(Resistance));
}

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
        EnumDomain.RequireDefined(kind, nameof(kind));
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
    public const int CurrentContractVersion = 19;

    public RuntimeSaveGameSnapshot(
        SemanticVersion frameworkVersion,
        IEnumerable<ContentPackIdentity> contentPacks,
        IEnumerable<RuntimeActorSnapshot> actors,
        RuntimePartyRosterSnapshot partyRoster,
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot shopStock,
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
        PartyRoster = partyRoster ?? throw new ArgumentNullException(nameof(partyRoster));
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        CurrencyLedger = currencyLedger ?? throw new ArgumentNullException(nameof(currencyLedger));
        ShopStock = shopStock ?? throw new ArgumentNullException(nameof(shopStock));
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
    public RuntimePartyRosterSnapshot PartyRoster { get; }
    public RuntimeInventorySnapshot Inventory { get; }
    public RuntimeCurrencyLedgerSnapshot CurrencyLedger { get; }
    public RuntimeShopStockSnapshot ShopStock { get; }
    public RuntimeFieldSnapshot? Field { get; }
    public CompendiumStateSnapshot Compendium { get; }
    public RuntimeKnowledgeSnapshot Knowledge { get; }
    public RuntimeSessionProgressSnapshot Session { get; }
    public RuntimeCheckpointLogSnapshot Checkpoints { get; }
    public IReadOnlyDictionary<ContentId, string> HostContext { get; }
}

public sealed class RuntimeSaveValidator : IRuntimeSaveValidator
{
    private readonly IRosterCapacityPolicy _rosterCapacityPolicy;
    private readonly IRuntimeMoveListCapacityPolicy _moveListCapacityPolicy;
    private readonly IRuntimeRulesetBindingResolver? _rulesetBindings;
    private readonly IChargePolicyResolver? _chargePolicies;
    private readonly IEquipmentSlotLayoutPolicy _equipmentSlotLayout;

    public RuntimeSaveValidator(
        IRosterCapacityPolicy? rosterCapacityPolicy = null,
        IRuntimeMoveListCapacityPolicy? moveListCapacityPolicy = null,
        IRuntimeRulesetBindingResolver? rulesetBindings = null,
        IChargePolicyResolver? chargePolicies = null,
        IEquipmentSlotLayoutPolicy? equipmentSlotLayout = null)
    {
        _rosterCapacityPolicy = rosterCapacityPolicy ?? NoLimitRosterCapacityPolicy.Instance;
        _moveListCapacityPolicy = moveListCapacityPolicy ??
            new SharedRuntimeMoveListCapacityPolicy();
        _rulesetBindings = rulesetBindings;
        _chargePolicies = chargePolicies;
        _equipmentSlotLayout = equipmentSlotLayout ??
            StandardEquipmentSlotLayoutPolicy.Instance;
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
        ValidateAggregateIdentifiers(snapshot, diagnostics);
        ValidateAggregateEnumValues(snapshot, diagnostics);

        Dictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors = [];
        for (int index = 0; index < snapshot.Actors.Count; index++)
        {
            RuntimeActorSnapshot actor = snapshot.Actors[index];
            RuntimeInstanceId instanceId = actor.Identity.InstanceId;
            if (instanceId.IsValid && !actors.TryAdd(instanceId, actor))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.DuplicateActorInstanceId,
                    $"Actor instance '{instanceId}' appears more than once.",
                    instanceId,
                    Path: $"$.actors[{index}].identity.instanceId"));
            }

            EntityDefinition? entity = null;
            if (actor.Identity.EntityDefinitionId.IsValid &&
                !catalog.Entities.TryGetValue(actor.Identity.EntityDefinitionId, out entity))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogEntity,
                    $"Entity '{actor.Identity.EntityDefinitionId}' is not present in the catalog.",
                    instanceId,
                    actor.Identity.EntityDefinitionId,
                    $"$.actors[{index}].identity.entityDefinitionId"));
            }
            else if (entity is not null && actor.Identity.ActorKindId.IsValid &&
                     actor.Identity.ActorKindId != entity.EntityKindId)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ActorKindMismatch,
                    $"Actor kind '{actor.Identity.ActorKindId}' does not match entity kind '{entity.EntityKindId}'.",
                    instanceId,
                    actor.Identity.EntityDefinitionId,
                    $"$.actors[{index}].identity.actorKindId"));
            }

            ValidateActorRestoreContract(actor, snapshot.Inventory, catalog, diagnostics, index);
        }

        ValidatePartyReferences(snapshot.PartyRoster, actors, _rosterCapacityPolicy, diagnostics);
        ValidateCombatProfileReferences(snapshot.Actors, actors, diagnostics);
        ValidatePassiveActivationReferences(snapshot.Actors, actors, diagnostics);
        ValidateInventory(snapshot.Inventory, actors.Keys, catalog, diagnostics);
        ValidateActorEquipment(snapshot.Actors, snapshot.Inventory, catalog, diagnostics);
        ValidateShopStock(snapshot.ShopStock, catalog, diagnostics);
        if (snapshot.Field is not null)
        {
            ValidateField(snapshot.Field, catalog, diagnostics);
        }
        ValidateCompendium(snapshot.Compendium, catalog, diagnostics);
        ValidateKnowledge(snapshot.Knowledge, actors, catalog, diagnostics);
        ValidateCheckpoints(snapshot.Checkpoints, actors, diagnostics);

        return new RuntimeSaveValidationResult(snapshot, diagnostics);
    }

    private static void ValidateCombatProfileReferences(
        IReadOnlyList<RuntimeActorSnapshot> actorSnapshots,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        for (int index = 0; index < actorSnapshots.Count; index++)
        {
            RuntimeActorSnapshot actor = actorSnapshots[index];
            RuntimeCombatProfileIdentitySnapshot profile = actor.CombatProfileIdentity;
            string root = $"$.actors[{index}].combatProfileIdentity";
            if (!profile.SourceActorInstanceId.IsValid ||
                !profile.SourceEntityDefinitionId.IsValid)
            {
                continue;
            }

            if (!actors.TryGetValue(profile.SourceActorInstanceId, out RuntimeActorSnapshot? source))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.CombatProfileSourceMissing,
                    $"Combat-profile source actor '{profile.SourceActorInstanceId}' is not " +
                    "present in actors.",
                    actor.Identity.InstanceId,
                    profile.SourceEntityDefinitionId,
                    root + ".sourceActorInstanceId"));
                continue;
            }

            if (source.Identity.EntityDefinitionId != profile.SourceEntityDefinitionId)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.CombatProfileSourceEntityMismatch,
                    $"Combat-profile source actor '{profile.SourceActorInstanceId}' is entity " +
                    $"'{source.Identity.EntityDefinitionId}', not " +
                    $"'{profile.SourceEntityDefinitionId}'.",
                    actor.Identity.InstanceId,
                    profile.SourceEntityDefinitionId,
                    root + ".sourceEntityDefinitionId"));
            }
        }
    }

    private static void ValidatePassiveActivationReferences(
        IReadOnlyList<RuntimeActorSnapshot> actorSnapshots,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        for (int actorIndex = 0; actorIndex < actorSnapshots.Count; actorIndex++)
        {
            RuntimeActorSnapshot actor = actorSnapshots[actorIndex];
            for (int activationIndex = 0;
                 activationIndex < actor.BattleActivations.PassiveActivations.Count;
                 activationIndex++)
            {
                RuntimeInstanceId? targetId =
                    actor.BattleActivations.PassiveActivations[activationIndex].TargetInstanceId;
                if (targetId is not RuntimeInstanceId target || !target.IsValid || actors.ContainsKey(target))
                {
                    continue;
                }

                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingActorReference,
                    $"Passive activation target '{target}' is not present in actors.",
                    target,
                    Path: $"$.actors[{actorIndex}].battleActivations.passiveActivations" +
                          $"[{activationIndex}].targetInstanceId"));
            }
        }
    }

    private static void ValidateAggregateIdentifiers(
        RuntimeSaveGameSnapshot snapshot,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        ValidateContentIdKeys(snapshot.Inventory.ItemQuantities.Keys, "$.inventory.itemQuantities", diagnostics);
        foreach ((ContentId slotId,
                  IReadOnlyList<RuntimeEquipmentInstanceSnapshot> instances) in
                 snapshot.Inventory.OwnedEquipmentInstances.OrderBy(
                     pair => pair.Key.Value,
                     StringComparer.Ordinal))
        {
            ValidateContentId(
                slotId,
                $"$.inventory.ownedEquipmentInstances.{SlotPath(slotId)}",
                diagnostics);
            for (int index = 0; index < instances.Count; index++)
            {
                RuntimeEquipmentInstanceSnapshot instance = instances[index];
                string path =
                    $"$.inventory.ownedEquipmentInstances.{SlotPath(slotId)}[{index}]";
                ValidateRuntimeInstanceId(
                    instance.InstanceId,
                    path + ".instanceId",
                    diagnostics);
                ValidateContentId(
                    instance.DefinitionId,
                    path + ".definitionId",
                    diagnostics);
            }
        }
        for (int actorIndex = 0; actorIndex < snapshot.Actors.Count; actorIndex++)
        {
            foreach ((ContentId slotId, RuntimeInstanceId equipmentInstanceId) in
                     snapshot.Actors[actorIndex].Equipment.EquippedInstanceIds)
            {
                ValidateContentId(
                    slotId,
                    $"$.actors[{actorIndex}].equipment.equippedInstanceIds.{SlotPath(slotId)}",
                    diagnostics);
                ValidateRuntimeInstanceId(
                    equipmentInstanceId,
                    $"$.actors[{actorIndex}].equipment.equippedInstanceIds.{SlotPath(slotId)}",
                    diagnostics);
            }
        }

        for (int stockIndex = 0; stockIndex < snapshot.ShopStock.Entries.Count; stockIndex++)
        {
            RuntimeShopStockEntrySnapshot entry = snapshot.ShopStock.Entries[stockIndex];
            string root = $"$.shopStock.entries[{stockIndex}].offerIdentity";
            if (entry.OfferIdentity is null)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.InvalidContentId,
                    "Shop stock offer identity cannot be null.",
                    Path: root));
                continue;
            }

            ValidateContentId(entry.OfferIdentity.ShopId, root + ".shopId", diagnostics);
            ValidateContentId(entry.OfferIdentity.OfferId, root + ".offerId", diagnostics);
        }

        if (snapshot.Field is not null)
        {
            ValidateContentId(snapshot.Field.Navigation.CurrentLocationId,
                "$.field.navigation.currentLocationId", diagnostics);
            if (snapshot.Field.DungeonTraversal is RuntimeDungeonTraversalSnapshot dungeon)
            {
                ValidateContentId(dungeon.DungeonId, "$.field.dungeonTraversal.dungeonId", diagnostics);
                ValidateContentId(dungeon.CurrentNodeId, "$.field.dungeonTraversal.currentNodeId", diagnostics);
                ValidateContentIds(dungeon.VisitedNodeIds,
                    "$.field.dungeonTraversal.visitedNodeIds", diagnostics);
                ValidateContentIds(dungeon.UnlockedCheckpointIds,
                    "$.field.dungeonTraversal.unlockedCheckpointIds", diagnostics);
                ValidateContentIds(dungeon.DefeatedBossIds,
                    "$.field.dungeonTraversal.defeatedBossIds", diagnostics);
            }
        }

        for (int index = 0; index < snapshot.Compendium.Entries.Count; index++)
        {
            CompendiumEntrySnapshot entry = snapshot.Compendium.Entries[index];
            string path = $"$.compendium.entries[{index}]";
            ValidateContentId(entry.EntityId, path + ".entityId", diagnostics);
            ValidateContentIdKeys(entry.Stats.Keys, path + ".stats", diagnostics);
            ValidateContentIds(entry.SkillIds, path + ".skillIds", diagnostics);
            ValidateContentIds(entry.EquippedSkillIds, path + ".equippedSkillIds", diagnostics);
        }

        for (int index = 0; index < snapshot.Knowledge.ElementalAffinities.Count; index++)
        {
            ValidateContentId(snapshot.Knowledge.ElementalAffinities[index].EntityId,
                $"$.knowledge.elementalAffinities[{index}].entityId", diagnostics);
        }
        for (int index = 0; index < snapshot.Knowledge.AilmentResistances.Count; index++)
        {
            RuntimeAilmentResistanceKnowledgeSnapshot entry = snapshot.Knowledge.AilmentResistances[index];
            ValidateContentId(entry.EntityId,
                $"$.knowledge.ailmentResistances[{index}].entityId", diagnostics);
            ValidateContentId(entry.AilmentId,
                $"$.knowledge.ailmentResistances[{index}].ailmentId", diagnostics);
        }
        for (int index = 0; index < snapshot.Knowledge.InstantDeathResistances.Count; index++)
        {
            ValidateContentId(snapshot.Knowledge.InstantDeathResistances[index].EntityId,
                $"$.knowledge.instantDeathResistances[{index}].entityId", diagnostics);
        }
        for (int index = 0; index < snapshot.Knowledge.AnalyzedDefenses.Count; index++)
        {
            ValidateContentId(snapshot.Knowledge.AnalyzedDefenses[index].EntityId,
                $"$.knowledge.analyzedDefenses[{index}].entityId", diagnostics);
        }

        if (snapshot.Session.MoonPhaseId is ContentId moonPhaseId)
        {
            ValidateContentId(moonPhaseId, "$.session.moonPhaseId", diagnostics);
        }
        ValidateContentIdKeys(snapshot.Session.Counters.Keys, "$.session.counters", diagnostics);
        ValidateContentIds(snapshot.Session.Flags, "$.session.flags", diagnostics);
        ValidateContentIdKeys(snapshot.HostContext.Keys, "$.hostContext", diagnostics);

        for (int index = 0; index < snapshot.Checkpoints.Entries.Count; index++)
        {
            RuntimeCheckpointEntrySnapshot entry = snapshot.Checkpoints.Entries[index];
            if (entry.ActorId is RuntimeInstanceId actorId)
            {
                ValidateRuntimeInstanceId(actorId,
                    $"$.checkpoints.entries[{index}].actorId", diagnostics);
            }
            if (entry.ContentId is ContentId contentId)
            {
                ValidateContentId(contentId,
                    $"$.checkpoints.entries[{index}].contentId", diagnostics);
            }
        }
    }

    private static void ValidateAggregateEnumValues(
        RuntimeSaveGameSnapshot snapshot,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        for (int index = 0; index < snapshot.Knowledge.ElementalAffinities.Count; index++)
        {
            RuntimeElementalAffinityKnowledgeSnapshot entry = snapshot.Knowledge.ElementalAffinities[index];
            ValidateEnumValue(
                entry.Element,
                $"$.knowledge.elementalAffinities[{index}].element",
                diagnostics);
            if (EnumDomain.IsDefined(entry.Element) && entry.Element == DamageElement.Almighty)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.IntrinsicElementKnowledgeNotStorable,
                    "Almighty always has Normal affinity and cannot be stored as affinity knowledge.",
                    ContentId: entry.EntityId,
                    Path: $"$.knowledge.elementalAffinities[{index}].element"));
            }
            ValidateEnumValue(
                entry.Affinity,
                $"$.knowledge.elementalAffinities[{index}].affinity",
                diagnostics);
        }

        for (int index = 0; index < snapshot.Knowledge.AilmentResistances.Count; index++)
        {
            ValidateEnumValue(
                snapshot.Knowledge.AilmentResistances[index].Resistance,
                $"$.knowledge.ailmentResistances[{index}].resistance",
                diagnostics);
        }

        for (int index = 0; index < snapshot.Knowledge.InstantDeathResistances.Count; index++)
        {
            RuntimeInstantDeathResistanceKnowledgeSnapshot entry =
                snapshot.Knowledge.InstantDeathResistances[index];
            ValidateEnumValue(
                entry.Channel,
                $"$.knowledge.instantDeathResistances[{index}].channel",
                diagnostics);
            ValidateEnumValue(
                entry.Resistance,
                $"$.knowledge.instantDeathResistances[{index}].resistance",
                diagnostics);
        }

        for (int index = 0; index < snapshot.Knowledge.AnalyzedDefenses.Count; index++)
        {
            RuntimeAnalyzedDefenseKnowledgeSnapshot entry = snapshot.Knowledge.AnalyzedDefenses[index];
            for (int fieldIndex = 0; fieldIndex < entry.DisclosedFields.Count; fieldIndex++)
            {
                BattleAnalysisField field = entry.DisclosedFields[fieldIndex];
                string path = $"$.knowledge.analyzedDefenses[{index}].disclosedFields[{fieldIndex}]";
                ValidateEnumValue(field, path, diagnostics);
                if (EnumDomain.IsDefined(field) && !IsPersistentDefenseField(field))
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.InvalidAnalyzedDefenseField,
                        $"Analysis field '{field}' is not persistent defense knowledge.",
                        Path: path));
                }
            }
        }

        for (int index = 0; index < snapshot.Checkpoints.Entries.Count; index++)
        {
            ValidateEnumValue(
                snapshot.Checkpoints.Entries[index].Kind,
                $"$.checkpoints.entries[{index}].kind",
                diagnostics);
        }
    }

    private static void ValidateEnumValue<TEnum>(
        TEnum value,
        string path,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
        where TEnum : struct, Enum
    {
        if (EnumDomain.IsDefined(value))
        {
            return;
        }

        diagnostics.Add(new RuntimeSaveValidationDiagnostic(
            RuntimeSaveValidationCode.UndefinedEnumValue,
            $"Value '{value}' is not defined for {typeof(TEnum).Name}.",
            Path: path));
    }

    private static bool IsPersistentDefenseField(BattleAnalysisField field) => field is
        BattleAnalysisField.ElementalAffinities or
        BattleAnalysisField.AilmentResistances or
        BattleAnalysisField.InstantDeathResistances;

    private static void ValidateActorReferenceIdentifiers(
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        string path,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        for (int index = 0; index < references.Count; index++)
        {
            ValidateActorReferenceIdentifiers(references[index], $"{path}[{index}]", diagnostics);
        }
    }

    private static void ValidateActorReferenceIdentifiers(
        RuntimeActorReferenceSnapshot reference,
        string path,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        ValidateRuntimeInstanceId(reference.InstanceId, path + ".instanceId", diagnostics);
        ValidateContentId(reference.EntityDefinitionId, path + ".entityDefinitionId", diagnostics);
    }

    private static void ValidateContentIdKeys(
        IEnumerable<ContentId> ids,
        string path,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        int index = 0;
        foreach (ContentId id in ids)
        {
            ValidateContentId(id, $"{path}.keys[{index}]", diagnostics);
            index++;
        }
    }

    private static void ValidateContentIds(
        IReadOnlyList<ContentId> ids,
        string path,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        for (int index = 0; index < ids.Count; index++)
        {
            ValidateContentId(ids[index], $"{path}[{index}]", diagnostics);
        }
    }

    private static void ValidateContentId(
        ContentId id,
        string path,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        if (id.IsValid)
        {
            return;
        }

        diagnostics.Add(new RuntimeSaveValidationDiagnostic(
            RuntimeSaveValidationCode.InvalidContentId,
            "Content ID cannot be empty.",
            ContentId: id,
            Path: path));
    }

    private static void ValidateRuntimeInstanceId(
        RuntimeInstanceId id,
        string path,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        if (id.IsValid)
        {
            return;
        }

        diagnostics.Add(new RuntimeSaveValidationDiagnostic(
            RuntimeSaveValidationCode.InvalidRuntimeInstanceId,
            "Runtime instance ID cannot be empty.",
            id,
            Path: path));
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

    private void ValidateActorRestoreContract(
        RuntimeActorSnapshot actor,
        RuntimeInventorySnapshot inventory,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        int actorIndex)
    {
        IEnumerable<SkillDefinition> learnedPassiveSkills = actor.Skills.EquippedSkillIds
            .Where(skillId =>
                catalog.Skills.TryGetValue(skillId, out SkillDefinition? skill) &&
                skill.Activation == SkillActivation.Passive)
            .Select(skillId => catalog.Skills[skillId]);
        RuntimeEquipmentProfile equipmentProfile = new RuntimeEquipmentProfileResolver(
                _equipmentSlotLayout)
            .Resolve(inventory, actor.Equipment, catalog);
        IEnumerable<SkillDefinition> equipmentPassiveSkills = equipmentProfile.Diagnostics.Count == 0
            ? equipmentProfile.GrantedSkillIds
                .Where(skillId =>
                    catalog.Skills.TryGetValue(skillId, out SkillDefinition? skill) &&
                    skill.Activation == SkillActivation.Passive)
                .Select(skillId => catalog.Skills[skillId])
            : [];
        SkillDefinition[] equippedPassiveSkills = learnedPassiveSkills
            .Concat(equipmentPassiveSkills)
            .GroupBy(skill => skill.Id)
            .Select(group => group.First())
            .ToArray();
        IReadOnlyList<RuntimeActorSnapshotIntegrityDiagnostic> integrityDiagnostics =
            RuntimeActorSnapshotIntegrity.ValidateForRestore(
                actor,
                equippedPassiveSkills,
                catalog.Ailments.Values,
                catalog.RegisteredEventIds,
                catalog.RegisteredPhaseIds);
        foreach (RuntimeActorSnapshotIntegrityDiagnostic issue in integrityDiagnostics)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                SaveCode(issue.Code),
                issue.Message,
                actor.Identity.InstanceId,
                issue.ContentId,
                ActorPath(actorIndex, issue.Path)));
        }

        ValidateActorStatModifiers(actor, catalog, diagnostics, actorIndex);
        ValidateActorCharges(actor, diagnostics, actorIndex);

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
        SkillDefinition[] equippedDefinitions = actor.Skills.EquippedSkillIds
            .Where(skillId => catalog.Skills.ContainsKey(skillId))
            .Select(skillId => catalog.Skills[skillId])
            .ToArray();
        if (equippedDefinitions.Length == actor.Skills.EquippedSkillIds.Count &&
            actor.Identity.InstanceId.IsValid &&
            actor.Identity.EntityDefinitionId.IsValid &&
            actor.Identity.ActorKindId.IsValid)
        {
            RuntimeMoveListCapacityViolation? violation =
                RuntimeMoveListCapacityValidation.ValidateCurrent(
                    actor.Identity,
                    equippedDefinitions,
                    _moveListCapacityPolicy);
            if (violation is not null)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ActorMoveListCapacityRejected,
                    violation.Message,
                    actor.Identity.InstanceId,
                    violation.SkillId,
                    $"$.actors[{actorIndex}].skills.equippedSkillIds"));
            }
        }
        ValidateActorPendingSkillChoices(
            actor,
            catalog,
            diagnostics,
            actorIndex);
    }

    private static void ValidateActorPendingSkillChoices(
        RuntimeActorSnapshot actor,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        int actorIndex)
    {
        if (!catalog.Entities.TryGetValue(
                actor.Identity.EntityDefinitionId,
                out EntityDefinition? entity) ||
            entity is null)
        {
            return;
        }

        for (int index = 0; index < actor.Skills.PendingChoices.Count; index++)
        {
            RuntimePendingSkillChoiceSnapshot choice = actor.Skills.PendingChoices[index];
            string path = $"$.actors[{actorIndex}].skills.pendingChoices[{index}]";
            if (!catalog.Skills.ContainsKey(choice.SkillId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogSkill,
                    $"Pending skill '{choice.SkillId}' referenced by actor " +
                    $"'{actor.Identity.InstanceId}' is not present in the catalog.",
                    actor.Identity.InstanceId,
                    choice.SkillId,
                    path + ".skillId"));
                continue;
            }

            if (!entity.SkillUnlocks.Any(unlock =>
                    unlock.Level == choice.UnlockLevel &&
                    unlock.SkillId == choice.SkillId))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ActorPendingSkillUnlockMismatch,
                    $"Pending skill '{choice.SkillId}' at level {choice.UnlockLevel} is not an " +
                    $"authored unlock for entity '{entity.Id}'.",
                    actor.Identity.InstanceId,
                    choice.SkillId,
                    path));
            }
            if (choice.UnlockLevel > actor.Progression.Level)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ActorPendingSkillLevelUnavailable,
                    $"Pending skill '{choice.SkillId}' unlocks at level {choice.UnlockLevel}, " +
                    $"but actor '{actor.Identity.InstanceId}' is level " +
                    $"{actor.Progression.Level}.",
                    actor.Identity.InstanceId,
                    choice.SkillId,
                    path + ".unlockLevel"));
            }
        }
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
            if (!skillId.IsValid)
            {
                continue;
            }

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
            RuntimeActorSnapshotIntegrityCode.InvalidRuntimeInstanceId => RuntimeSaveValidationCode.InvalidRuntimeInstanceId,
            RuntimeActorSnapshotIntegrityCode.InvalidContentId => RuntimeSaveValidationCode.InvalidContentId,
            RuntimeActorSnapshotIntegrityCode.DuplicateResource => RuntimeSaveValidationCode.DuplicateActorResource,
            RuntimeActorSnapshotIntegrityCode.DuplicateLearnedSkill => RuntimeSaveValidationCode.DuplicateActorLearnedSkill,
            RuntimeActorSnapshotIntegrityCode.DuplicateEquippedSkill => RuntimeSaveValidationCode.DuplicateActorEquippedSkill,
            RuntimeActorSnapshotIntegrityCode.EquippedSkillNotLearned => RuntimeSaveValidationCode.ActorEquippedSkillNotLearned,
            RuntimeActorSnapshotIntegrityCode.DuplicatePendingSkillChoiceToken => RuntimeSaveValidationCode.DuplicateActorPendingSkillChoiceToken,
            RuntimeActorSnapshotIntegrityCode.DuplicatePendingSkill => RuntimeSaveValidationCode.DuplicateActorPendingSkill,
            RuntimeActorSnapshotIntegrityCode.PendingSkillAlreadyLearned => RuntimeSaveValidationCode.ActorPendingSkillAlreadyLearned,
            RuntimeActorSnapshotIntegrityCode.DuplicateCapability => RuntimeSaveValidationCode.DuplicateActorCapability,
            RuntimeActorSnapshotIntegrityCode.DuplicateAilment => RuntimeSaveValidationCode.DuplicateActorAilment,
            RuntimeActorSnapshotIntegrityCode.MissingAilmentDefinition => RuntimeSaveValidationCode.MissingCatalogAilment,
            RuntimeActorSnapshotIntegrityCode.ConflictingAilmentExclusivityGroup => RuntimeSaveValidationCode.ConflictingActorAilmentExclusivityGroup,
            RuntimeActorSnapshotIntegrityCode.DuplicateStatus => RuntimeSaveValidationCode.DuplicateActorStatus,
            RuntimeActorSnapshotIntegrityCode.DuplicateCharge => RuntimeSaveValidationCode.DuplicateActorCharge,
            RuntimeActorSnapshotIntegrityCode.DuplicateShield => RuntimeSaveValidationCode.DuplicateActorShield,
            RuntimeActorSnapshotIntegrityCode.DuplicateAffinityBreak => RuntimeSaveValidationCode.DuplicateActorAffinityBreak,
            RuntimeActorSnapshotIntegrityCode.InvalidAffinityBreakElement => RuntimeSaveValidationCode.InvalidActorAffinityBreakElement,
            RuntimeActorSnapshotIntegrityCode.DuplicateAffinityOverride => RuntimeSaveValidationCode.DuplicateActorAffinityOverride,
            RuntimeActorSnapshotIntegrityCode.DuplicatePassiveSkillState => RuntimeSaveValidationCode.DuplicatePassiveSkillState,
            RuntimeActorSnapshotIntegrityCode.MissingPassiveSkillState => RuntimeSaveValidationCode.MissingPassiveSkillState,
            RuntimeActorSnapshotIntegrityCode.PassiveSkillStateNotLoaded => RuntimeSaveValidationCode.PassiveStateSkillNotLoaded,
            RuntimeActorSnapshotIntegrityCode.DuplicatePassiveActivation => RuntimeSaveValidationCode.DuplicatePassiveActivation,
            RuntimeActorSnapshotIntegrityCode.PassiveActivationSkillNotLoaded => RuntimeSaveValidationCode.PassiveActivationSkillNotLoaded,
            RuntimeActorSnapshotIntegrityCode.PassiveActivationTriggerIndexInvalid => RuntimeSaveValidationCode.PassiveActivationTriggerIndexInvalid,
            RuntimeActorSnapshotIntegrityCode.PassiveActivationEventMismatch => RuntimeSaveValidationCode.PassiveActivationEventMismatch,
            RuntimeActorSnapshotIntegrityCode.BaseStatOutOfRange => RuntimeSaveValidationCode.ActorBaseStatOutOfRange,
            RuntimeActorSnapshotIntegrityCode.EffectiveStatOutOfRange => RuntimeSaveValidationCode.ActorEffectiveStatOutOfRange,
            RuntimeActorSnapshotIntegrityCode.BaseResourceValueOutOfRange => RuntimeSaveValidationCode.ActorBaseResourceValueOutOfRange,
            RuntimeActorSnapshotIntegrityCode.RetainedDurationKindInvalid => RuntimeSaveValidationCode.ActorRetainedDurationKindInvalid,
            RuntimeActorSnapshotIntegrityCode.TurnDurationValueOutOfRange => RuntimeSaveValidationCode.ActorTurnDurationValueOutOfRange,
            RuntimeActorSnapshotIntegrityCode.TurnDurationTickEventIdInvalid => RuntimeSaveValidationCode.ActorTurnDurationTickEventIdInvalid,
            RuntimeActorSnapshotIntegrityCode.PhaseDurationPhaseIdInvalid => RuntimeSaveValidationCode.ActorPhaseDurationPhaseIdInvalid,
            RuntimeActorSnapshotIntegrityCode.UndefinedEnumValue => RuntimeSaveValidationCode.UndefinedEnumValue,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown actor snapshot integrity code.")
        };

    private void ValidateActorStatModifiers(
        RuntimeActorSnapshot actor,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        int actorIndex)
    {
        RuntimeStatModifierStateSnapshot? state = actor.BattleStatus.StatModifiers;
        if (state is null)
        {
            return;
        }

        string rootPath = $"$.actors[{actorIndex}].battleStatus.statModifiers";
        if (_rulesetBindings is null)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.ActorStatModifierPolicyResolverMissing,
                "Retained stat modifiers require an explicit ruleset binding resolver during save validation.",
                actor.Identity.InstanceId,
                state.PolicyId,
                rootPath + ".policyId"));
            return;
        }

        RulesetBindingResult<IStatModifierPolicyService> binding =
            _rulesetBindings.BindStatModifierPolicy(catalog, state.PolicyId);
        if (!binding.IsSuccess || binding.Service is null)
        {
            foreach (RulesetBindingDiagnostic issue in binding.Diagnostics)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.ActorStatModifierPolicyBindingRejected,
                    issue.Message,
                    actor.Identity.InstanceId,
                    state.PolicyId,
                    rootPath + ".policyId"));
            }
            return;
        }

        StatModifierValidationResult validation = binding.Service.ValidateState(state);
        foreach (StatModifierDiagnostic issue in validation.Diagnostics)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.ActorStatModifierStateInvalid,
                issue.Message,
                actor.Identity.InstanceId,
                issue.ModifierTrackId ?? state.PolicyId,
                StatModifierPath(state, rootPath, issue),
                issue.Code));
        }
    }

    private static string StatModifierPath(
        RuntimeStatModifierStateSnapshot state,
        string rootPath,
        StatModifierDiagnostic diagnostic)
    {
        if (diagnostic.ModifierTrackId is not ContentId trackId)
        {
            return rootPath;
        }

        int trackIndex = state.Tracks
            .Select((track, index) => new { track, index })
            .Where(value => value.track.ModifierTrackId == trackId)
            .Select(value => value.index)
            .DefaultIfEmpty(-1)
            .First();
        if (trackIndex < 0)
        {
            return rootPath + ".tracks";
        }

        string trackPath = $"{rootPath}.tracks[{trackIndex}]";
        if (diagnostic.ContributionSequence is not long sequence)
        {
            return trackPath;
        }

        int contributionIndex = state.Tracks[trackIndex].Contributions
            .Select((contribution, index) => new { contribution, index })
            .Where(value => value.contribution.Sequence == sequence)
            .Select(value => value.index)
            .DefaultIfEmpty(-1)
            .First();
        return contributionIndex < 0
            ? trackPath + ".contributions"
            : $"{trackPath}.contributions[{contributionIndex}]";
    }

    private void ValidateActorCharges(
        RuntimeActorSnapshot actor,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        int actorIndex)
    {
        RuntimeChargeStateSnapshot? state = actor.BattleStatus.ChargeState;
        if (state is null)
        {
            return;
        }

        string rootPath = $"$.actors[{actorIndex}].battleStatus.chargeState";
        if (_chargePolicies is null)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.ActorChargePolicyResolverMissing,
                "Retained charge state requires an explicit charge-policy resolver during save validation.",
                actor.Identity.InstanceId,
                state.PolicyId,
                rootPath + ".policyId"));
            return;
        }

        if (!_chargePolicies.TryResolve(state.PolicyId, out IChargePolicyService? policy) ||
            policy is null)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.ActorChargePolicyBindingRejected,
                $"No charge policy is registered for '{state.PolicyId}'.",
                actor.Identity.InstanceId,
                state.PolicyId,
                rootPath + ".policyId"));
            return;
        }

        ChargePolicyValidationResult validation = policy.ValidateState(state);
        foreach (ChargePolicyDiagnostic issue in validation.Diagnostics)
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.ActorChargeStateInvalid,
                issue.Message,
                actor.Identity.InstanceId,
                state.PolicyId,
                rootPath,
                ChargePolicyCode: issue.Code));
        }
    }

    private static string ActorPath(int actorIndex, string relativePath) =>
        $"$.actors[{actorIndex}]" + relativePath[1..];

    private static void ValidatePartyReferences(
        RuntimePartyRosterSnapshot partyRoster,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        IRosterCapacityPolicy rosterCapacityPolicy,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        actors.TryGetValue(
            partyRoster.Owner.InstanceId,
            out RuntimeActorSnapshot? ownerActor);
        foreach (RuntimePartyRosterInvariantDiagnostic rosterDiagnostic in
                 RuntimePartyRosterInvariantRules.Validate(
                     partyRoster,
                     ownerActor,
                     rosterCapacityPolicy))
        {
            if (rosterDiagnostic.Code == RuntimePartyRosterInvariantCode.PartyRosterOwnerMismatch)
            {
                continue;
            }

            RuntimeSaveValidationCode code = rosterDiagnostic.Code switch
            {
                RuntimePartyRosterInvariantCode.InvalidReferenceInstanceId =>
                    RuntimeSaveValidationCode.InvalidRuntimeInstanceId,
                RuntimePartyRosterInvariantCode.InvalidReferenceEntityDefinitionId =>
                    RuntimeSaveValidationCode.InvalidContentId,
                RuntimePartyRosterInvariantCode.DuplicateActivePartyReference or
                RuntimePartyRosterInvariantCode.DuplicateReserveReference or
                RuntimePartyRosterInvariantCode.DuplicateHostedEntityReference or
                RuntimePartyRosterInvariantCode.DuplicateCompanionReference or
                RuntimePartyRosterInvariantCode.ActiveReserveRoleCollision =>
                    RuntimeSaveValidationCode.DuplicatePartyRosterReference,
                RuntimePartyRosterInvariantCode.ActiveHostedEntityNotOwned =>
                    RuntimeSaveValidationCode.ActiveHostedEntityNotOwned,
                RuntimePartyRosterInvariantCode.ActiveHostedEntityReferenceMismatch =>
                    RuntimeSaveValidationCode.ActiveHostedEntityReferenceMismatch,
                RuntimePartyRosterInvariantCode.ActivePartyCapacityExceeded =>
                    RuntimeSaveValidationCode.ActivePartyCapacityExceeded,
                RuntimePartyRosterInvariantCode.HostedEntityRosterCapacityExceeded =>
                    RuntimeSaveValidationCode.HostedEntityRosterCapacityExceeded,
                RuntimePartyRosterInvariantCode.CompanionRosterCapacityExceeded =>
                    RuntimeSaveValidationCode.CompanionRosterCapacityExceeded,
                _ => RuntimeSaveValidationCode.PartyRosterIdentityCollision
            };
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                code,
                rosterDiagnostic.Message,
                rosterDiagnostic.InstanceId,
                Path: "$.partyRoster" + rosterDiagnostic.Path[1..]));
        }

        ValidateActorReference(
            partyRoster.Owner,
            actors,
            diagnostics,
            "$.partyRoster.owner",
            RuntimeSaveValidationCode.MissingActorReference,
            "Party roster owner");

        ValidateActorReferenceList(partyRoster.ActiveParty, actors, diagnostics, "$.partyRoster.activeParty");
        ValidateActorReferenceList(partyRoster.ReserveMembers, actors, diagnostics, "$.partyRoster.reserveMembers");
        ValidateActorReferenceList(partyRoster.HostedEntityRoster, actors, diagnostics, "$.partyRoster.hostedEntityRoster");
        ValidateActorReferenceList(partyRoster.CompanionRoster, actors, diagnostics, "$.partyRoster.companionRoster");

        if (partyRoster.ActiveHostedEntity is not null)
        {
            ValidateActorReference(
                partyRoster.ActiveHostedEntity,
                actors,
                diagnostics,
                "$.partyRoster.activeHostedEntity",
                RuntimeSaveValidationCode.MissingActiveHostedEntityReference,
                "Active Hosted Entity");

        }
    }

    private static void ValidateActorReferenceList(
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path)
    {
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

    private void ValidateInventory(
        RuntimeInventorySnapshot inventory,
        IEnumerable<RuntimeInstanceId> actorInstanceIds,
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

        var seenInstanceIds = new HashSet<RuntimeInstanceId>();
        var actorIds = new HashSet<RuntimeInstanceId>(actorInstanceIds);
        foreach ((ContentId slotId,
                  IReadOnlyList<RuntimeEquipmentInstanceSnapshot> instances) in
                 inventory.OwnedEquipmentInstances.OrderBy(
                     pair => pair.Key.Value,
                     StringComparer.Ordinal))
        {
            for (int index = 0; index < instances.Count; index++)
            {
                RuntimeEquipmentInstanceSnapshot instance = instances[index];
                string path =
                    $"$.inventory.ownedEquipmentInstances.{SlotPath(slotId)}[{index}]";
                if (!seenInstanceIds.Add(instance.InstanceId))
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.DuplicateEquipmentInstanceId,
                        $"Equipment instance '{instance.InstanceId}' appears more than once in inventory.",
                        instance.InstanceId,
                        instance.DefinitionId,
                        path + ".instanceId"));
                }
                if (actorIds.Contains(instance.InstanceId))
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.EquipmentInstanceIdCollidesWithActor,
                        $"Equipment instance '{instance.InstanceId}' collides with an actor runtime instance ID.",
                        instance.InstanceId,
                        instance.DefinitionId,
                        path + ".instanceId"));
                }

                ContentId equipmentId = instance.DefinitionId;
                if (!catalog.Equipment.TryGetValue(
                        equipmentId,
                        out EquipmentDefinition? definition))
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.MissingCatalogEquipment,
                        $"Equipment '{equipmentId}' is not present in the catalog.",
                        ContentId: equipmentId,
                        Path: path + ".definitionId"));
                }
                else
                {
                    EquipmentSlotLayoutResult definitionLayout =
                        _equipmentSlotLayout.ValidateDefinition(definition);
                    EquipmentSlotLayoutResult assignment =
                        _equipmentSlotLayout.ValidateAssignment(definition.SlotId, slotId);
                    if (!definitionLayout.IsCompatible || !assignment.IsCompatible)
                    {
                        diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                            RuntimeSaveValidationCode.EquipmentSlotMismatch,
                            definitionLayout.Message ??
                            assignment.Message ??
                            $"Equipment '{equipmentId}' is not compatible with inventory slot '{slotId}'.",
                            ContentId: equipmentId,
                            Path: path + ".definitionId"));
                    }
                }
            }
        }
    }

    private static void ValidateShopStock(
        RuntimeShopStockSnapshot stock,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        var expected = new HashSet<RuntimeShopOfferIdentity>();
        foreach (ShopCatalogDefinition shop in catalog.Shops.Values)
        {
            foreach (ShopOfferDefinition offer in shop.Offers)
            {
                if (offer.Stock is not UnlimitedShopStockDefinition)
                {
                    expected.Add(new RuntimeShopOfferIdentity(shop.Id, offer.Id));
                }
            }
        }

        var seen = new HashSet<RuntimeShopOfferIdentity>();
        for (int index = 0; index < stock.Entries.Count; index++)
        {
            RuntimeShopStockEntrySnapshot entry = stock.Entries[index];
            string root = $"$.shopStock.entries[{index}]";
            if (entry.OfferIdentity is null)
            {
                continue;
            }

            RuntimeShopOfferIdentity identity = entry.OfferIdentity;
            if (!seen.Add(identity))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.DuplicateShopStockEntry,
                    $"Shop stock identity '{identity.ShopId}/{identity.OfferId}' appears more than once.",
                    ContentId: identity.ShopId,
                    Path: root + ".offerIdentity"));
            }
            if (entry.RemainingQuantity < 0)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.NegativeShopStockQuantity,
                    $"Shop stock identity '{identity.ShopId}/{identity.OfferId}' cannot have a negative quantity.",
                    ContentId: identity.ShopId,
                    Path: root + ".remainingQuantity"));
            }

            if (!catalog.Shops.TryGetValue(identity.ShopId, out ShopCatalogDefinition? shop))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogShop,
                    $"Shop '{identity.ShopId}' is not present in the catalog.",
                    ContentId: identity.ShopId,
                    Path: root + ".offerIdentity.shopId"));
                continue;
            }

            ShopOfferDefinition? offer = shop.Offers.FirstOrDefault(candidate =>
                candidate.Id == identity.OfferId);
            if (offer is null)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingShopOffer,
                    $"Shop offer '{identity.ShopId}/{identity.OfferId}' is not present in the catalog.",
                    ContentId: identity.ShopId,
                    Path: root + ".offerIdentity.offerId"));
                continue;
            }
            if (offer.Stock is UnlimitedShopStockDefinition)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.UnexpectedShopStockEntry,
                    $"Unlimited shop offer '{identity.ShopId}/{identity.OfferId}' must not have durable stock state.",
                    ContentId: identity.ShopId,
                    Path: root));
            }
        }

        foreach (RuntimeShopOfferIdentity missing in expected.Except(seen))
        {
            diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                RuntimeSaveValidationCode.MissingShopStockEntry,
                $"Tracked shop offer '{missing.ShopId}/{missing.OfferId}' is missing durable stock state.",
                ContentId: missing.ShopId,
                Path: "$.shopStock.entries"));
        }
    }

    private void ValidateEquipment(
        RuntimeEquipmentSnapshot equipment,
        RuntimeInventorySnapshot inventory,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics,
        string path,
        RuntimeInstanceId? actorInstanceId)
    {
        foreach ((ContentId slotId, RuntimeInstanceId equipmentInstanceId) in
                 equipment.EquippedInstanceIds.OrderBy(
                     pair => pair.Key.Value,
                     StringComparer.Ordinal))
        {
            string equipmentPath =
                $"{path}.equippedInstanceIds.{SlotPath(slotId)}";
            if (!inventory.TryGetEquipmentInstance(
                    equipmentInstanceId,
                    out RuntimeEquipmentInstanceSnapshot? instance,
                    out ContentId ownedSlotId) ||
                instance is null)
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.EquippedEquipmentNotOwned,
                    $"Equipped equipment instance '{equipmentInstanceId}' is not owned.",
                    equipmentInstanceId,
                    Path: equipmentPath));
                continue;
            }

            ContentId equipmentId = instance.DefinitionId;
            if (!catalog.Equipment.TryGetValue(
                    equipmentId,
                    out EquipmentDefinition? definition))
            {
                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.MissingCatalogEquipment,
                    $"Equipped item definition '{equipmentId}' is not present in the catalog.",
                    actorInstanceId,
                    ContentId: equipmentId,
                    Path: equipmentPath));
            }
            else
            {
                EquipmentSlotLayoutResult definitionLayout =
                    _equipmentSlotLayout.ValidateDefinition(definition);
                EquipmentSlotLayoutResult inventoryAssignment =
                    _equipmentSlotLayout.ValidateAssignment(definition.SlotId, ownedSlotId);
                EquipmentSlotLayoutResult actorAssignment =
                    _equipmentSlotLayout.ValidateAssignment(ownedSlotId, slotId);
                if (!definitionLayout.IsCompatible ||
                    !inventoryAssignment.IsCompatible ||
                    !actorAssignment.IsCompatible)
                {
                    diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                        RuntimeSaveValidationCode.EquipmentSlotMismatch,
                        definitionLayout.Message ??
                        inventoryAssignment.Message ??
                        actorAssignment.Message ??
                        $"Equipment instance '{equipmentInstanceId}' is not compatible with slot '{slotId}'.",
                        actorInstanceId,
                        equipmentId,
                        equipmentPath));
                }
            }
        }
    }

    private void ValidateActorEquipment(
        IReadOnlyList<RuntimeActorSnapshot> actors,
        RuntimeInventorySnapshot inventory,
        GameDataCatalog catalog,
        ICollection<RuntimeSaveValidationDiagnostic> diagnostics)
    {
        var assignments = new Dictionary<RuntimeInstanceId, RuntimeInstanceId>();
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

            foreach ((ContentId slotId, RuntimeInstanceId equipmentInstanceId) in
                     actor.Equipment.EquippedInstanceIds.OrderBy(
                         pair => pair.Key.Value,
                         StringComparer.Ordinal))
            {
                if (assignments.TryAdd(
                        equipmentInstanceId,
                        actor.Identity.InstanceId))
                {
                    continue;
                }

                diagnostics.Add(new RuntimeSaveValidationDiagnostic(
                    RuntimeSaveValidationCode.EquipmentAssignedToMultipleActors,
                    $"Equipment instance '{equipmentInstanceId}' is assigned to more than one actor or slot.",
                    actor.Identity.InstanceId,
                    Path:
                        $"{equipmentPath}.equippedInstanceIds.{SlotPath(slotId)}"));
            }
        }
    }

    private static string SlotPath(ContentId slotId) => slotId.ToString();

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
            CompendiumEntryIntegrityCode.InvalidContentId =>
                RuntimeSaveValidationCode.InvalidContentId,
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
                RuntimeKnowledgeCollection.AnalyzedDefenses =>
                    RuntimeSaveValidationCode.DuplicateAnalyzedDefenseKnowledge,
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

        foreach (RuntimeAnalyzedDefenseKnowledgeSnapshot entry in knowledge.AnalyzedDefenses)
        {
            ValidateKnowledgeEntity(entry.EntityId, knownActorEntities, catalog, diagnostics, "$.knowledge.analyzedDefenses");
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
    public static IReadOnlyList<T> List<T>(IEnumerable<T>? values)
    {
        T[] snapshot = values?.ToArray() ?? [];
        if (snapshot.Any(static value => value is null))
        {
            throw new ArgumentException("Persistence collections cannot contain null entries.", nameof(values));
        }

        return Array.AsReadOnly(snapshot);
    }

    public static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values)
        where TKey : notnull
    {
        var snapshot = new Dictionary<TKey, TValue>();
        foreach ((TKey key, TValue value) in values ?? [])
        {
            if (key is null || value is null)
            {
                throw new ArgumentException(
                    "Persistence dictionaries cannot contain null keys or values.",
                    nameof(values));
            }

            snapshot.Add(key, value);
        }

        return new ReadOnlyDictionary<TKey, TValue>(snapshot);
    }
}
