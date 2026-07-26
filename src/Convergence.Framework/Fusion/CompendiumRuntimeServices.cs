using Convergence.Content;
using Convergence.Catalog;
using Convergence.Battle;
using Convergence.Encounters;
using Convergence.Runtime;

namespace Convergence.Fusion;

public enum CompendiumRuntimeDiagnosticCode
{
    EntityMissing,
    EntityNotEligible,
    ActorEntityMismatch,
    InvalidStatValue,
    MissingStat,
    UnknownStat,
    DuplicateLearnedSkill,
    DuplicateEquippedSkill,
    MissingSkill,
    EquippedSkillNotLearned,
    MissingEntry,
    DuplicateOwned,
    DuplicateRuntimeInstanceId,
    RosterFull,
    RecallUnavailable,
    InsufficientCurrency,
    ActorCreationFailed,
    RosterPlacementRejected,
    WalletRejected,
    InvalidRecallCost,
    InvalidIdentifier
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
    public bool Accepted => Code != CompendiumRegistrationCode.InvalidEntry;
    public CompendiumStateSnapshot Before { get; }
    public CompendiumStateSnapshot After { get; }
    public CompendiumEntrySnapshot? Entry { get; }
    public IReadOnlyList<CompendiumRuntimeDiagnostic> Diagnostics { get; }
}

public enum CompendiumRecallRosterKind
{
    Companion,
    HostedEntity
}

public enum CompendiumRecallTransactionCode
{
    Applied,
    MissingEntry,
    EntityNotEligible,
    InvalidEntry,
    DuplicateOwned,
    DuplicateRuntimeInstanceId,
    RosterFull,
    RecallUnavailable,
    InsufficientCurrency,
    ActorCreationFailed,
    RosterPlacementRejected,
    WalletRejected,
    InvalidRecallCost
}

public sealed record CompendiumRecallTransactionRequest
{
    public CompendiumRecallTransactionRequest(
        CompendiumStateSnapshot compendium,
        RuntimePartyRosterSnapshot partyRoster,
        RuntimeActorSnapshot partyOwner,
        RuntimeWalletSnapshot wallet,
        ContentId entityId,
        RuntimeInstanceId recalledInstanceId,
        ContentId commandAuthorityId,
        ContentId teamId,
        CompendiumRecallRosterKind rosterKind,
        int? basePrice = null)
    {
        if (basePrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basePrice), "Recall base price cannot be negative.");
        }

        Compendium = compendium ?? throw new ArgumentNullException(nameof(compendium));
        PartyRoster = partyRoster ?? throw new ArgumentNullException(nameof(partyRoster));
        PartyOwner = partyOwner ?? throw new ArgumentNullException(nameof(partyOwner));
        Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        EntityId = entityId;
        RecalledInstanceId = recalledInstanceId;
        CommandAuthorityId = commandAuthorityId;
        TeamId = teamId;
        RosterKind = rosterKind;
        BasePrice = basePrice;
    }

    public CompendiumStateSnapshot Compendium { get; }
    public RuntimePartyRosterSnapshot PartyRoster { get; }
    public RuntimeActorSnapshot PartyOwner { get; }
    public RuntimeWalletSnapshot Wallet { get; }
    public ContentId EntityId { get; }
    public RuntimeInstanceId RecalledInstanceId { get; }
    public ContentId CommandAuthorityId { get; }
    public ContentId TeamId { get; }
    public CompendiumRecallRosterKind RosterKind { get; }
    public int? BasePrice { get; }
}

public sealed record CompendiumRecallTransactionResult
{
    public CompendiumRecallTransactionResult(
        CompendiumRecallTransactionCode code,
        CompendiumStateSnapshot compendium,
        RuntimePartyRosterSnapshot beforePartyRoster,
        RuntimePartyRosterSnapshot afterPartyRoster,
        RuntimeWalletSnapshot beforeWallet,
        RuntimeWalletSnapshot afterWallet,
        int cost = 0,
        CompendiumEntrySnapshot? entry = null,
        CatalogBattleActor? actor = null,
        IEnumerable<CompendiumRuntimeDiagnostic>? diagnostics = null)
    {
        Code = code;
        Compendium = compendium ?? throw new ArgumentNullException(nameof(compendium));
        BeforePartyRoster = beforePartyRoster ?? throw new ArgumentNullException(nameof(beforePartyRoster));
        AfterPartyRoster = afterPartyRoster ?? throw new ArgumentNullException(nameof(afterPartyRoster));
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
    public RuntimePartyRosterSnapshot BeforePartyRoster { get; }
    public RuntimePartyRosterSnapshot AfterPartyRoster { get; }
    public RuntimeWalletSnapshot BeforeWallet { get; }
    public RuntimeWalletSnapshot AfterWallet { get; }
    public int Cost { get; }
    public CompendiumEntrySnapshot? Entry { get; }
    public CatalogBattleActor? Actor { get; }
    public IReadOnlyList<CompendiumRuntimeDiagnostic> Diagnostics { get; }
}

public interface ICompendiumRuntimeService
{
    CompendiumRecallPricingDecision GetRecallPricing(
        CompendiumEntrySnapshot entry,
        int? basePrice = null);

    CompendiumActorRegistrationResult RegisterActor(
        CompendiumStateSnapshot state,
        RuntimeActorSnapshot actor);

    CompendiumActorRegistrationResult RecordAcquisition(
        CompendiumStateSnapshot state,
        RuntimeActorSnapshot actor);

    CompendiumRecallTransactionResult Recall(CompendiumRecallTransactionRequest request);
}

public sealed class CompendiumRuntimeService : ICompendiumRuntimeService
{
    private readonly IEntityDefinitionRepository _entities;
    private readonly ISkillDefinitionRepository _skills;
    private readonly ICatalogBattleActorFactory _actors;
    private readonly IResourceGrowthPolicy _resourceGrowth;
    private readonly ICompendiumService _compendium;
    private readonly IPartyRosterTransitionService _partyRoster;
    private readonly IEconomyTransactionService _economy;

    public CompendiumRuntimeService(
        IEntityDefinitionRepository entities,
        ISkillDefinitionRepository skills,
        ICatalogBattleActorFactory actors,
        IResourceGrowthPolicy resourceGrowth,
        ICompendiumService? compendium = null,
        IPartyRosterTransitionService? partyRoster = null,
        IEconomyTransactionService? economy = null)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        _resourceGrowth = resourceGrowth ?? throw new ArgumentNullException(nameof(resourceGrowth));
        _compendium = compendium ?? new CompendiumService();
        _partyRoster = partyRoster ?? new PartyRosterTransitionService();
        _economy = economy ?? new EconomyTransactionService();
    }

    public CompendiumRecallPricingDecision GetRecallPricing(
        CompendiumEntrySnapshot entry,
        int? basePrice = null) =>
        _compendium.GetRecallPricing(entry, basePrice);

    public CompendiumActorRegistrationResult RegisterActor(
        CompendiumStateSnapshot state,
        RuntimeActorSnapshot actor) =>
        RegisterActor(state, actor, preserveExistingEntry: false);

    public CompendiumActorRegistrationResult RecordAcquisition(
        CompendiumStateSnapshot state,
        RuntimeActorSnapshot actor) =>
        RegisterActor(state, actor, preserveExistingEntry: true);

    private CompendiumActorRegistrationResult RegisterActor(
        CompendiumStateSnapshot state,
        RuntimeActorSnapshot actor,
        bool preserveExistingEntry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        ContentId entityId = actor.Identity.EntityDefinitionId;
        if (!entityId.IsValid || !actor.Identity.InstanceId.IsValid)
        {
            return RegistrationRejected(
                state,
                CompendiumRuntimeDiagnosticCode.InvalidIdentifier,
                "Compendium registration requires non-empty entity and runtime instance IDs.",
                entityId,
                actor.Identity.InstanceId);
        }

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
        IReadOnlyList<CompendiumEntryIntegrityDiagnostic> entryDiagnostics =
            CompendiumEntryIntegrity.Validate(entry, entity, _skills);
        if (entryDiagnostics.Count > 0)
        {
            return RegistrationRejected(
                state,
                entry,
                actor.Identity.InstanceId,
                entryDiagnostics);
        }

        CompendiumRegistrationResult registration = preserveExistingEntry
            ? _compendium.RecordAcquisition(state, entry)
            : _compendium.Register(state, entry);
        return new CompendiumActorRegistrationResult(
            registration.Code,
            registration.Before,
            registration.After,
            registration.Entry);
    }

    public CompendiumRecallTransactionResult Recall(CompendiumRecallTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.EntityId.IsValid || !request.RecalledInstanceId.IsValid ||
            !request.CommandAuthorityId.IsValid || !request.TeamId.IsValid)
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.InvalidEntry,
                CompendiumRuntimeDiagnosticCode.InvalidIdentifier,
                "Compendium recall requires non-empty entity, runtime instance, command-authority, and team IDs.");
        }

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

        IReadOnlyList<CompendiumEntryIntegrityDiagnostic> entryDiagnostics =
            CompendiumEntryIntegrity.Validate(entry, entity, _skills);
        if (entryDiagnostics.Count > 0)
        {
            return RecallInvalidEntry(request, entry, entryDiagnostics);
        }

        if (RuntimePartyRosterIdentityRules.ContainsInstanceId(
                request.PartyRoster,
                request.RecalledInstanceId))
        {
            return RecallRejected(
                request,
                CompendiumRecallTransactionCode.DuplicateRuntimeInstanceId,
                CompendiumRuntimeDiagnosticCode.DuplicateRuntimeInstanceId,
                $"Runtime instance ID '{request.RecalledInstanceId}' is already used by the party or roster graph.",
                entry);
        }

        bool alreadyOwned = OwnedActorReferences(request.PartyRoster)
            .Any(reference => reference.EntityDefinitionId == entry.EntityId);
        var recalledReference = new RuntimeActorReferenceSnapshot(
            request.RecalledInstanceId,
            entry.EntityId,
            entry.DisplayName);
        PartyRosterTransitionResult placement = request.RosterKind switch
        {
            CompendiumRecallRosterKind.Companion => _partyRoster.AddCompanionToRoster(
                new AddCompanionToRosterRequest(
                    request.PartyRoster,
                    request.PartyOwner,
                    recalledReference)),
            CompendiumRecallRosterKind.HostedEntity => _partyRoster.AddHostedEntityToRoster(
                new AddHostedEntityToRosterRequest(
                    request.PartyRoster,
                    request.PartyOwner,
                    recalledReference)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unknown Compendium recall roster kind.")
        };

        CompendiumRecallAssessment assessment;
        try
        {
            assessment = _compendium.AssessRecall(
                request.Compendium,
                entry.EntityId,
                request.Wallet.Balance,
                alreadyOwned,
                placement.Applied || placement.Code != PartyRosterTransitionCode.RosterFull,
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
                CompendiumRecallCode.RosterFull => RecallRejected(
                    request,
                    CompendiumRecallTransactionCode.RosterFull,
                    CompendiumRuntimeDiagnosticCode.RosterFull,
                    assessment.Diagnostics.FirstOrDefault()?.Message ?? "The destination roster is full.",
                    entry,
                    assessment.Cost),
                CompendiumRecallCode.RecallUnavailable => RecallRejected(
                    request,
                    CompendiumRecallTransactionCode.RecallUnavailable,
                    CompendiumRuntimeDiagnosticCode.RecallUnavailable,
                    assessment.Diagnostics.FirstOrDefault()?.Message ?? "Compendium recall is not available.",
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
                CompendiumRecallTransactionCode.RosterPlacementRejected,
                CompendiumRuntimeDiagnosticCode.RosterPlacementRejected,
                placement.Diagnostics.FirstOrDefault()?.Message ?? "The recalled actor could not be placed in the selected roster.",
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

        RuntimeWalletSnapshot afterWallet = request.Wallet;
        if (assessment.Cost > 0)
        {
            WalletTransactionResult payment = _economy.Debit(request.Wallet, assessment.Cost);
            if (!payment.Applied)
            {
                return RecallRejected(
                    request,
                    CompendiumRecallTransactionCode.WalletRejected,
                    CompendiumRuntimeDiagnosticCode.WalletRejected,
                    "The configured recall payment was rejected.",
                    entry,
                    assessment.Cost);
            }

            afterWallet = payment.After;
        }

        return new CompendiumRecallTransactionResult(
            CompendiumRecallTransactionCode.Applied,
            request.Compendium,
            request.PartyRoster,
            placement.After,
            request.Wallet,
            afterWallet,
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
            IsDeployed: false,
            request.CommandAuthorityId,
            progression));
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
        RuntimePassiveSkillStateSnapshot[] passiveStates = equippedSkills
            .Select(_skills.GetRequiredSkill)
            .Where(skill => skill.Activation == SkillActivation.Passive)
            .Select(skill => new RuntimePassiveSkillStateSnapshot(
                skill.Id,
                IsEnabled: true))
            .ToArray();
        var snapshot = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                request.RecalledInstanceId,
                entry.EntityId,
                fresh.Identity.ActorKindId,
                entry.DisplayName,
                fresh.Identity.DisplaySubtitle),
            new RuntimeActorAffiliationSnapshot(request.CommandAuthorityId, request.TeamId),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            progression,
            fullResources,
            statBlock,
            new RuntimeSkillStateSnapshot(learnedSkills, equippedSkills),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(passiveSkillStates: passiveStates),
            fresh.BaseResourceValues,
            fresh.VitalResourceId,
            fresh.CapabilityIds);
        return _actors.Restore(
            CatalogBattleActorRestoreRequest.FromValidatedFrameworkSnapshot(snapshot));
    }

    private static IEnumerable<RuntimeActorReferenceSnapshot> OwnedActorReferences(
        RuntimePartyRosterSnapshot partyRoster) =>
        RuntimePartyRosterIdentityRules.Enumerate(partyRoster)
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

    private static CompendiumActorRegistrationResult RegistrationRejected(
        CompendiumStateSnapshot state,
        CompendiumEntrySnapshot entry,
        RuntimeInstanceId instanceId,
        IEnumerable<CompendiumEntryIntegrityDiagnostic> diagnostics) =>
        new(
            CompendiumRegistrationCode.InvalidEntry,
            state,
            state,
            diagnostics: RuntimeDiagnostics(diagnostics, entry.EntityId, instanceId));

    private static CompendiumRecallTransactionResult RecallInvalidEntry(
        CompendiumRecallTransactionRequest request,
        CompendiumEntrySnapshot entry,
        IEnumerable<CompendiumEntryIntegrityDiagnostic> diagnostics) =>
        new(
            CompendiumRecallTransactionCode.InvalidEntry,
            request.Compendium,
            request.PartyRoster,
            request.PartyRoster,
            request.Wallet,
            request.Wallet,
            entry: entry,
            diagnostics: RuntimeDiagnostics(diagnostics, entry.EntityId, request.RecalledInstanceId));

    private static IEnumerable<CompendiumRuntimeDiagnostic> RuntimeDiagnostics(
        IEnumerable<CompendiumEntryIntegrityDiagnostic> diagnostics,
        ContentId entityId,
        RuntimeInstanceId instanceId) =>
        diagnostics.Select(diagnostic => new CompendiumRuntimeDiagnostic(
            RuntimeCode(diagnostic.Code),
            diagnostic.Message,
            entityId,
            instanceId));

    private static CompendiumRuntimeDiagnosticCode RuntimeCode(CompendiumEntryIntegrityCode code) =>
        code switch
        {
            CompendiumEntryIntegrityCode.InvalidContentId =>
                CompendiumRuntimeDiagnosticCode.InvalidIdentifier,
            CompendiumEntryIntegrityCode.DuplicateLearnedSkill =>
                CompendiumRuntimeDiagnosticCode.DuplicateLearnedSkill,
            CompendiumEntryIntegrityCode.DuplicateEquippedSkill =>
                CompendiumRuntimeDiagnosticCode.DuplicateEquippedSkill,
            CompendiumEntryIntegrityCode.InvalidStatValue =>
                CompendiumRuntimeDiagnosticCode.InvalidStatValue,
            CompendiumEntryIntegrityCode.MissingStat =>
                CompendiumRuntimeDiagnosticCode.MissingStat,
            CompendiumEntryIntegrityCode.UnknownStat =>
                CompendiumRuntimeDiagnosticCode.UnknownStat,
            CompendiumEntryIntegrityCode.MissingSkill =>
                CompendiumRuntimeDiagnosticCode.MissingSkill,
            CompendiumEntryIntegrityCode.EquippedSkillNotLearned =>
                CompendiumRuntimeDiagnosticCode.EquippedSkillNotLearned,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown Compendium integrity code.")
        };

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
            request.PartyRoster,
            request.PartyRoster,
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
    EntityMissing,
    DuplicateElementalAffinityKnowledge,
    DuplicateAilmentResistanceKnowledge,
    DuplicateInstantDeathResistanceKnowledge,
    InvalidIdentifier
}

public sealed record FamiliarKnowledgeImportDiagnostic(
    FamiliarKnowledgeImportDiagnosticCode Code,
    string Message,
    ContentId EntityId,
    int? Index = null);

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

        var currentDiagnostics = new List<FamiliarKnowledgeImportDiagnostic>();
        ValidateKnowledgeIdentifiers(current, currentDiagnostics);
        currentDiagnostics.AddRange(RuntimeKnowledgeIntegrity.FindDuplicates(current)
            .Select(duplicate => new FamiliarKnowledgeImportDiagnostic(
                duplicate.Collection switch
                {
                    RuntimeKnowledgeCollection.ElementalAffinities =>
                        FamiliarKnowledgeImportDiagnosticCode.DuplicateElementalAffinityKnowledge,
                    RuntimeKnowledgeCollection.AilmentResistances =>
                        FamiliarKnowledgeImportDiagnosticCode.DuplicateAilmentResistanceKnowledge,
                    RuntimeKnowledgeCollection.InstantDeathResistances =>
                        FamiliarKnowledgeImportDiagnosticCode.DuplicateInstantDeathResistanceKnowledge,
                    _ => throw new InvalidOperationException(
                        $"Unsupported knowledge collection '{duplicate.Collection}'.")
                },
                $"Current knowledge contains a duplicate key for {duplicate.KeyDescription}.",
                duplicate.EntityId,
                duplicate.Index)));
        if (currentDiagnostics.Count > 0)
        {
            return new FamiliarKnowledgeImportResult(
                current,
                current,
                diagnostics: currentDiagnostics);
        }

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
        var seenEntityIds = new HashSet<ContentId>();
        ContentId[] requestedEntityIds = familiarEntityIds.ToArray();

        for (int index = 0; index < requestedEntityIds.Length; index++)
        {
            ContentId entityId = requestedEntityIds[index];
            if (!seenEntityIds.Add(entityId))
            {
                continue;
            }
            if (!entityId.IsValid)
            {
                diagnostics.Add(new FamiliarKnowledgeImportDiagnostic(
                    FamiliarKnowledgeImportDiagnosticCode.InvalidIdentifier,
                    "Familiar entity ID cannot be empty.",
                    entityId,
                    index));
                continue;
            }

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

    private static void ValidateKnowledgeIdentifiers(
        RuntimeKnowledgeSnapshot knowledge,
        ICollection<FamiliarKnowledgeImportDiagnostic> diagnostics)
    {
        for (int index = 0; index < knowledge.ElementalAffinities.Count; index++)
        {
            ContentId entityId = knowledge.ElementalAffinities[index].EntityId;
            if (!entityId.IsValid)
            {
                diagnostics.Add(new FamiliarKnowledgeImportDiagnostic(
                    FamiliarKnowledgeImportDiagnosticCode.InvalidIdentifier,
                    "Elemental-affinity knowledge entity ID cannot be empty.",
                    entityId,
                    index));
            }
        }

        for (int index = 0; index < knowledge.AilmentResistances.Count; index++)
        {
            RuntimeAilmentResistanceKnowledgeSnapshot entry = knowledge.AilmentResistances[index];
            if (!entry.EntityId.IsValid || !entry.AilmentId.IsValid)
            {
                diagnostics.Add(new FamiliarKnowledgeImportDiagnostic(
                    FamiliarKnowledgeImportDiagnosticCode.InvalidIdentifier,
                    "Ailment-resistance knowledge entity and ailment IDs cannot be empty.",
                    entry.EntityId,
                    index));
            }
        }

        for (int index = 0; index < knowledge.InstantDeathResistances.Count; index++)
        {
            ContentId entityId = knowledge.InstantDeathResistances[index].EntityId;
            if (!entityId.IsValid)
            {
                diagnostics.Add(new FamiliarKnowledgeImportDiagnostic(
                    FamiliarKnowledgeImportDiagnosticCode.InvalidIdentifier,
                    "Instant-death knowledge entity ID cannot be empty.",
                    entityId,
                    index));
            }
        }
    }
}
