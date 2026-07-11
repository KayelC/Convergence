using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Fusion;

public enum CompendiumRuntimeDiagnosticCode
{
    EntityMissing,
    EntityNotEligible,
    ActorEntityMismatch,
    InvalidStatValue,
    MissingEntry,
    DuplicateOwned,
    DuplicateRuntimeInstanceId,
    StockFull,
    InsufficientCurrency,
    ActorCreationFailed,
    StockPlacementRejected,
    WalletRejected,
    InvalidRecallCost
}

public sealed record CompendiumRuntimeDiagnostic(
    CompendiumRuntimeDiagnosticCode Code,
    string Message,
    ContentId? EntityId = null,
    RuntimeInstanceId? InstanceId = null);

public sealed record CompendiumActorRegistrationResult
{
    public CompendiumActorRegistrationResult(
        CompendiumRegistrationCode code,
        CompendiumStateSnapshot before,
        CompendiumStateSnapshot after,
        CompendiumEntrySnapshot? entry = null,
        IEnumerable<CompendiumRuntimeDiagnostic>? diagnostics = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Entry = entry;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public CompendiumRegistrationCode Code { get; }
    public bool Applied => Code is CompendiumRegistrationCode.Added or CompendiumRegistrationCode.Updated;
    public CompendiumStateSnapshot Before { get; }
    public CompendiumStateSnapshot After { get; }
    public CompendiumEntrySnapshot? Entry { get; }
    public IReadOnlyList<CompendiumRuntimeDiagnostic> Diagnostics { get; }
}

public enum CompendiumRecallStockKind
{
    Demon,
    Persona
}

public enum CompendiumRecallTransactionCode
{
    Applied,
    MissingEntry,
    EntityNotEligible,
    DuplicateOwned,
    DuplicateRuntimeInstanceId,
    StockFull,
    InsufficientCurrency,
    ActorCreationFailed,
    StockPlacementRejected,
    WalletRejected,
    InvalidRecallCost
}

public sealed record CompendiumRecallTransactionRequest
{
    public CompendiumRecallTransactionRequest(
        CompendiumStateSnapshot compendium,
        RuntimePartyStockSnapshot partyStock,
        RuntimeWalletSnapshot wallet,
        ContentId entityId,
        RuntimeInstanceId recalledInstanceId,
        ContentId controllerId,
        ContentId teamId,
        CompendiumRecallStockKind stockKind,
        int? basePrice = null)
    {
        if (basePrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basePrice), "Recall base price cannot be negative.");
        }

        Compendium = compendium ?? throw new ArgumentNullException(nameof(compendium));
        PartyStock = partyStock ?? throw new ArgumentNullException(nameof(partyStock));
        Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        EntityId = entityId;
        RecalledInstanceId = recalledInstanceId;
        ControllerId = controllerId;
        TeamId = teamId;
        StockKind = stockKind;
        BasePrice = basePrice;
    }

    public CompendiumStateSnapshot Compendium { get; }
    public RuntimePartyStockSnapshot PartyStock { get; }
    public RuntimeWalletSnapshot Wallet { get; }
    public ContentId EntityId { get; }
    public RuntimeInstanceId RecalledInstanceId { get; }
    public ContentId ControllerId { get; }
    public ContentId TeamId { get; }
    public CompendiumRecallStockKind StockKind { get; }
    public int? BasePrice { get; }
}

public sealed record CompendiumRecallTransactionResult
{
    public CompendiumRecallTransactionResult(
        CompendiumRecallTransactionCode code,
        CompendiumStateSnapshot compendium,
        RuntimePartyStockSnapshot beforePartyStock,
        RuntimePartyStockSnapshot afterPartyStock,
        RuntimeWalletSnapshot beforeWallet,
        RuntimeWalletSnapshot afterWallet,
        int cost = 0,
        CompendiumEntrySnapshot? entry = null,
        CatalogBattleActor? actor = null,
        IEnumerable<CompendiumRuntimeDiagnostic>? diagnostics = null)
    {
        Code = code;
        Compendium = compendium ?? throw new ArgumentNullException(nameof(compendium));
        BeforePartyStock = beforePartyStock ?? throw new ArgumentNullException(nameof(beforePartyStock));
        AfterPartyStock = afterPartyStock ?? throw new ArgumentNullException(nameof(afterPartyStock));
        BeforeWallet = beforeWallet ?? throw new ArgumentNullException(nameof(beforeWallet));
        AfterWallet = afterWallet ?? throw new ArgumentNullException(nameof(afterWallet));
        Cost = cost;
        Entry = entry;
        Actor = actor;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public CompendiumRecallTransactionCode Code { get; }
    public bool Applied => Code == CompendiumRecallTransactionCode.Applied;
    public CompendiumStateSnapshot Compendium { get; }
    public RuntimePartyStockSnapshot BeforePartyStock { get; }
    public RuntimePartyStockSnapshot AfterPartyStock { get; }
    public RuntimeWalletSnapshot BeforeWallet { get; }
    public RuntimeWalletSnapshot AfterWallet { get; }
    public int Cost { get; }
    public CompendiumEntrySnapshot? Entry { get; }
    public CatalogBattleActor? Actor { get; }
    public IReadOnlyList<CompendiumRuntimeDiagnostic> Diagnostics { get; }
}

public interface ICompendiumRuntimeService
{
    int CalculateRecallCost(CompendiumEntrySnapshot entry, int? basePrice = null);

    CompendiumActorRegistrationResult RegisterActor(
        CompendiumStateSnapshot state,
        RuntimeActorSnapshot actor);

    CompendiumRecallTransactionResult Recall(CompendiumRecallTransactionRequest request);
}

public sealed class CompendiumRuntimeService : ICompendiumRuntimeService
{
    private readonly IEntityDefinitionRepository _entities;
    private readonly ICatalogBattleActorFactory _actors;
    private readonly IResourceGrowthPolicy _resourceGrowth;
    private readonly ICompendiumService _compendium;
    private readonly IPartyStockTransitionService _partyStock;
    private readonly IEconomyTransactionService _economy;

    public CompendiumRuntimeService(
        IEntityDefinitionRepository entities,
        ICatalogBattleActorFactory actors,
        IResourceGrowthPolicy resourceGrowth,
        ICompendiumService? compendium = null,
        IPartyStockTransitionService? partyStock = null,
        IEconomyTransactionService? economy = null)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        _resourceGrowth = resourceGrowth ?? throw new ArgumentNullException(nameof(resourceGrowth));
        _compendium = compendium ?? new CompendiumService();
        _partyStock = partyStock ?? new PartyStockTransitionService();
        _economy = economy ?? new EconomyTransactionService();
    }

    public int CalculateRecallCost(CompendiumEntrySnapshot entry, int? basePrice = null) =>
        _compendium.CalculateRecallCost(entry, basePrice);

    public CompendiumActorRegistrationResult RegisterActor(
        CompendiumStateSnapshot state,
        RuntimeActorSnapshot actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        ContentId entityId = actor.Identity.EntityDefinitionId;
        if (!_entities.TryGetEntity(entityId, out EntityDefinition? entity) || entity is null)
        {
            return RegistrationRejected(
                state,
                CompendiumRuntimeDiagnosticCode.EntityMissing,
                $"Compendium registration entity '{entityId}' is not present in the catalog.",
                entityId,
                actor.Identity.InstanceId);
        }

        if (entity.Id != entityId)
        {
            return RegistrationRejected(
                state,
                CompendiumRuntimeDiagnosticCode.ActorEntityMismatch,
                $"Actor entity '{entityId}' resolved to mismatched catalog entity '{entity.Id}'.",
                entityId,
                actor.Identity.InstanceId);
        }

        if (!entity.Capabilities.CompendiumEligible)
        {
            return RegistrationRejected(
                state,
                CompendiumRuntimeDiagnosticCode.EntityNotEligible,
                $"Entity '{entityId}' is not eligible for Compendium registration.",
                entityId,
                actor.Identity.InstanceId);
        }

        if (!TrySnapshotStats(actor.Stats.BaseStats, out IReadOnlyList<KeyValuePair<ContentId, int>> stats))
        {
            return RegistrationRejected(
                state,
                CompendiumRuntimeDiagnosticCode.InvalidStatValue,
                $"Actor '{actor.Identity.InstanceId}' has a non-integral or out-of-range base stat.",
                entityId,
                actor.Identity.InstanceId);
        }

        var entry = new CompendiumEntrySnapshot(
            entityId,
            actor.Identity.DisplayName,
            actor.Progression.Level,
            stats,
            actor.Skills.LearnedSkillIds,
            actor.Progression.Experience,
            actor.Progression.LifetimeExperience,
            actor.Progression.UnspentStatPoints,
            actor.Skills.EquippedSkillIds);
        CompendiumRegistrationResult registration = _compendium.Register(state, entry);
        return new CompendiumActorRegistrationResult(
            registration.Code,
            registration.Before,
            registration.After,
            registration.Entry);
    }

    public CompendiumRecallTransactionResult Recall(CompendiumRecallTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Compendium.TryGet(request.EntityId, out CompendiumEntrySnapshot? entry) || entry is null)
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.MissingEntry,
                CompendiumRuntimeDiagnosticCode.MissingEntry,
                $"Compendium entry '{request.EntityId}' does not exist.");
        }

        if (!_entities.TryGetEntity(entry.EntityId, out EntityDefinition? entity) || entity is null)
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.MissingEntry,
                CompendiumRuntimeDiagnosticCode.EntityMissing,
                $"Compendium entity '{entry.EntityId}' is not present in the catalog.",
                entry);
        }

        if (!entity.Capabilities.CompendiumEligible)
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.EntityNotEligible,
                CompendiumRuntimeDiagnosticCode.EntityNotEligible,
                $"Entity '{entry.EntityId}' is not eligible for Compendium recall.",
                entry);
        }

        if (RuntimePartyStockIdentityRules.ContainsInstanceId(
                request.PartyStock,
                request.RecalledInstanceId))
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.DuplicateRuntimeInstanceId,
                CompendiumRuntimeDiagnosticCode.DuplicateRuntimeInstanceId,
                $"Runtime instance ID '{request.RecalledInstanceId}' is already used by the party or stock graph.",
                entry);
        }

        bool alreadyOwned = OwnedActorReferences(request.PartyStock)
            .Any(reference => reference.EntityDefinitionId == entry.EntityId);
        var recalledReference = new RuntimeActorReferenceSnapshot(
            request.RecalledInstanceId,
            entry.EntityId,
            entry.DisplayName);
        PartyStockTransitionResult placement = request.StockKind switch
        {
            CompendiumRecallStockKind.Demon => _partyStock.AddDemonToStock(
                new AddDemonToStockRequest(request.PartyStock, recalledReference)),
            CompendiumRecallStockKind.Persona => _partyStock.AddPersonaToStock(
                new AddPersonaToStockRequest(request.PartyStock, recalledReference)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unknown Compendium recall stock kind.")
        };

        CompendiumRecallAssessment assessment;
        try
        {
            assessment = _compendium.AssessRecall(
                request.Compendium,
                entry.EntityId,
                request.Wallet.Macca,
                alreadyOwned,
                placement.Applied || placement.Code != PartyStockTransitionCode.StockFull,
                request.BasePrice);
        }
        catch (OverflowException exception)
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.InvalidRecallCost,
                CompendiumRuntimeDiagnosticCode.InvalidRecallCost,
                exception.Message,
                entry);
        }
        if (!assessment.CanRecall)
        {
            return assessment.Code switch
            {
                CompendiumRecallCode.DuplicateOwned => RecallRejected(
                    request,
                    CompendiumRecallTransactionCode.DuplicateOwned,
                    CompendiumRuntimeDiagnosticCode.DuplicateOwned,
                    assessment.Diagnostics.FirstOrDefault()?.Message ?? "The Compendium entity is already owned.",
                    entry,
                    assessment.Cost),
                CompendiumRecallCode.StockFull => RecallRejected(
                    request,
                    CompendiumRecallTransactionCode.StockFull,
                    CompendiumRuntimeDiagnosticCode.StockFull,
                    assessment.Diagnostics.FirstOrDefault()?.Message ?? "The destination stock is full.",
                    entry,
                    assessment.Cost),
                CompendiumRecallCode.InsufficientCurrency => RecallRejected(
                    request,
                    CompendiumRecallTransactionCode.InsufficientCurrency,
                    CompendiumRuntimeDiagnosticCode.InsufficientCurrency,
                    assessment.Diagnostics.FirstOrDefault()?.Message ?? "There is not enough currency to recall this entry.",
                    entry,
                    assessment.Cost),
                _ => RecallRejected(
                    request,
                    CompendiumRecallTransactionCode.MissingEntry,
                    CompendiumRuntimeDiagnosticCode.MissingEntry,
                    assessment.Diagnostics.FirstOrDefault()?.Message ?? "The Compendium entry cannot be recalled.",
                    entry,
                    assessment.Cost)
            };
        }

        if (!placement.Applied)
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.StockPlacementRejected,
                CompendiumRuntimeDiagnosticCode.StockPlacementRejected,
                placement.Diagnostics.FirstOrDefault()?.Message ?? "The recalled actor could not be placed in stock.",
                entry,
                assessment.Cost);
        }

        CatalogBattleActorCreationResult materialized = Materialize(request, entry);
        if (!materialized.IsSuccess)
        {
            string message = string.Join("; ", materialized.Diagnostics.Select(diagnostic => diagnostic.Message));
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.ActorCreationFailed,
                CompendiumRuntimeDiagnosticCode.ActorCreationFailed,
                string.IsNullOrWhiteSpace(message) ? "The recalled actor could not be created." : message,
                entry,
                assessment.Cost);
        }

        WalletTransactionResult payment = _economy.SpendMacca(request.Wallet, assessment.Cost);
        if (!payment.Applied)
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.WalletRejected,
                CompendiumRuntimeDiagnosticCode.WalletRejected,
                payment.Diagnostics.FirstOrDefault()?.Message ?? "The recall payment was rejected.",
                entry,
                assessment.Cost);
        }

        return new CompendiumRecallTransactionResult(
            CompendiumRecallTransactionCode.Applied,
            request.Compendium,
            request.PartyStock,
            placement.After,
            request.Wallet,
            payment.After,
            assessment.Cost,
            entry,
            materialized.RequireActor());
    }

    private CatalogBattleActorCreationResult Materialize(
        CompendiumRecallTransactionRequest request,
        CompendiumEntrySnapshot entry)
    {
        RuntimeProgressionSnapshot progression = new(
            entry.Level,
            entry.Experience,
            entry.LifetimeExperience,
            entry.UnspentStatPoints);
        CatalogBattleActorCreationResult initialized = _actors.Create(new CatalogBattleActorCreationRequest(
            entry.EntityId,
            request.RecalledInstanceId,
            request.TeamId,
            entry.Level,
            progression,
            request.ControllerId,
            RuntimeActorDeployment.Reserve,
            IsActive: false));
        if (!initialized.IsSuccess)
        {
            return initialized;
        }

        RuntimeActorSnapshot fresh = initialized.RequireActor().State.ToSnapshot();
        IEnumerable<KeyValuePair<ContentId, decimal>> stats = entry.Stats.Count == 0
            ? fresh.Stats.BaseStats
            : entry.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value));
        var statBlock = new RuntimeStatBlockSnapshot(stats, stats);
        ResourceRecalculationResult recalculated = _resourceGrowth.Recalculate(new ResourceRecalculationRequest(
            fresh.Resources,
            fresh.BaseResourceValues,
            statBlock.EffectiveStats));
        RuntimeResourceSnapshot[] fullResources = recalculated.Resources
            .Select(resource => new RuntimeResourceSnapshot(
                resource.ResourceId,
                resource.Maximum,
                resource.Maximum))
            .ToArray();
        IReadOnlyList<ContentId> learnedSkills = entry.SkillIds.Count == 0
            ? fresh.Skills.LearnedSkillIds
            : entry.SkillIds;
        IReadOnlyList<ContentId> equippedSkills = entry.EquippedSkillIds.Count == 0
            ? learnedSkills
            : entry.EquippedSkillIds;
        var snapshot = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                request.RecalledInstanceId,
                entry.EntityId,
                fresh.Identity.ActorKindId,
                entry.DisplayName,
                fresh.Identity.DisplaySubtitle),
            new RuntimeActorOwnershipSnapshot(request.ControllerId, request.TeamId),
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Reserve, IsActive: false),
            progression,
            fullResources,
            statBlock,
            new RuntimeSkillStateSnapshot(learnedSkills, equippedSkills),
            new RuntimeFormStockSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            fresh.BaseResourceValues,
            fresh.VitalResourceId,
            fresh.CapabilityIds);
        return _actors.Restore(snapshot);
    }

    private static IEnumerable<RuntimeActorReferenceSnapshot> OwnedActorReferences(
        RuntimePartyStockSnapshot partyStock) =>
        RuntimePartyStockIdentityRules.Enumerate(partyStock)
            .Select(occurrence => occurrence.Reference);

    private static bool TrySnapshotStats(
        IEnumerable<KeyValuePair<ContentId, decimal>> values,
        out IReadOnlyList<KeyValuePair<ContentId, int>> stats)
    {
        var result = new List<KeyValuePair<ContentId, int>>();
        foreach ((ContentId statId, decimal value) in values)
        {
            if (decimal.Truncate(value) != value || value < 0 || value > int.MaxValue)
            {
                stats = [];
                return false;
            }

            result.Add(new KeyValuePair<ContentId, int>(statId, decimal.ToInt32(value)));
        }

        stats = Array.AsReadOnly(result.ToArray());
        return true;
    }

    private static CompendiumActorRegistrationResult RegistrationRejected(
        CompendiumStateSnapshot state,
        CompendiumRuntimeDiagnosticCode code,
        string message,
        ContentId entityId,
        RuntimeInstanceId instanceId) =>
        new(
            CompendiumRegistrationCode.InvalidEntry,
            state,
            state,
            diagnostics: [new CompendiumRuntimeDiagnostic(code, message, entityId, instanceId)]);

    private static CompendiumRecallTransactionResult RecallRejected(
        CompendiumRecallTransactionRequest request,
        CompendiumRecallTransactionCode code,
        CompendiumRuntimeDiagnosticCode diagnosticCode,
        string message,
        CompendiumEntrySnapshot? entry = null,
        int cost = 0) =>
        new(
            code,
            request.Compendium,
            request.PartyStock,
            request.PartyStock,
            request.Wallet,
            request.Wallet,
            cost,
            entry,
            diagnostics:
            [
                new CompendiumRuntimeDiagnostic(
                    diagnosticCode,
                    message,
                    entry?.EntityId ?? request.EntityId,
                    request.RecalledInstanceId)
            ]);
}

public enum FamiliarKnowledgeImportDiagnosticCode
{
    EntityMissing
}

public sealed record FamiliarKnowledgeImportDiagnostic(
    FamiliarKnowledgeImportDiagnosticCode Code,
    string Message,
    ContentId EntityId);

public sealed record FamiliarKnowledgeImportResult
{
    public FamiliarKnowledgeImportResult(
        RuntimeKnowledgeSnapshot before,
        RuntimeKnowledgeSnapshot after,
        IEnumerable<ContentId>? importedEntityIds = null,
        IEnumerable<FamiliarKnowledgeImportDiagnostic>? diagnostics = null)
    {
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        ImportedEntityIds = Array.AsReadOnly((importedEntityIds ?? []).ToArray());
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public RuntimeKnowledgeSnapshot Before { get; }
    public RuntimeKnowledgeSnapshot After { get; }
    public IReadOnlyList<ContentId> ImportedEntityIds { get; }
    public IReadOnlyList<FamiliarKnowledgeImportDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Diagnostics.Count == 0;
}

public interface IFamiliarEntityKnowledgeService
{
    FamiliarKnowledgeImportResult Import(
        RuntimeKnowledgeSnapshot current,
        IEnumerable<ContentId> familiarEntityIds);

    FamiliarKnowledgeImportResult ImportRegistered(
        RuntimeKnowledgeSnapshot current,
        CompendiumStateSnapshot compendium);
}

public sealed class FamiliarEntityKnowledgeService : IFamiliarEntityKnowledgeService
{
    private readonly GameDataCatalog _catalog;

    public FamiliarEntityKnowledgeService(GameDataCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public FamiliarKnowledgeImportResult ImportRegistered(
        RuntimeKnowledgeSnapshot current,
        CompendiumStateSnapshot compendium)
    {
        ArgumentNullException.ThrowIfNull(compendium);
        return Import(current, compendium.Entries.Select(entry => entry.EntityId));
    }

    public FamiliarKnowledgeImportResult Import(
        RuntimeKnowledgeSnapshot current,
        IEnumerable<ContentId> familiarEntityIds)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(familiarEntityIds);

        var elemental = current.ElementalAffinities.ToDictionary(
            entry => (entry.EntityId, entry.Element),
            entry => entry.Affinity);
        var ailments = current.AilmentResistances.ToDictionary(
            entry => (entry.EntityId, entry.AilmentId),
            entry => entry.Resistance);
        var instantDeath = current.InstantDeathResistances.ToDictionary(
            entry => (entry.EntityId, entry.Channel),
            entry => entry.Resistance);
        var imported = new List<ContentId>();
        var diagnostics = new List<FamiliarKnowledgeImportDiagnostic>();

        foreach (ContentId entityId in familiarEntityIds.Distinct())
        {
            if (!_catalog.TryGetEntity(entityId, out EntityDefinition? entity) || entity is null)
            {
                diagnostics.Add(new FamiliarKnowledgeImportDiagnostic(
                    FamiliarKnowledgeImportDiagnosticCode.EntityMissing,
                    $"Familiar entity '{entityId}' is not present in the catalog.",
                    entityId));
                continue;
            }

            CombatDefenseProfile defenses = CombatDefenseProfile.FromEntityDefinition(entity);
            foreach (DamageElement element in Enum.GetValues<DamageElement>().Where(element => element != DamageElement.Almighty))
            {
                elemental[(entityId, element)] = defenses.GetElementalAffinity(element);
            }

            foreach (ContentId ailmentId in _catalog.Ailments.Keys)
            {
                ailments[(entityId, ailmentId)] = defenses.GetAilmentResistance(ailmentId);
            }

            foreach (InstantDeathChannel channel in Enum.GetValues<InstantDeathChannel>())
            {
                instantDeath[(entityId, channel)] = defenses.GetInstantDeathResistance(channel);
            }

            imported.Add(entityId);
        }

        var after = new RuntimeKnowledgeSnapshot(
            elemental
                .OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.Element)
                .Select(entry => new RuntimeElementalAffinityKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.Element,
                    entry.Value)),
            ailments
                .OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.AilmentId.ToString(), StringComparer.Ordinal)
                .Select(entry => new RuntimeAilmentResistanceKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.AilmentId,
                    entry.Value)),
            instantDeath
                .OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.Channel)
                .Select(entry => new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.Channel,
                    entry.Value)));
        return new FamiliarKnowledgeImportResult(current, after, imported, diagnostics);
    }
}
