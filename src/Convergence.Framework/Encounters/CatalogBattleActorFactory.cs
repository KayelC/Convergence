using Convergence.Content;
using Convergence.Catalog;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Internal;
using Convergence.Runtime;

namespace Convergence.Encounters;

/// <summary>
/// Describes one catalog-backed runtime actor that a host or encounter planner intends to create.
/// </summary>
/// <param name="EntityId">The pack-qualified entity definition to instantiate.</param>
/// <param name="InstanceId">The unique runtime identity assigned by the host or planner.</param>
/// <param name="TeamId">The battle team used by targeting and encounter rules.</param>
/// <param name="Level">The actor's initial runtime level.</param>
/// <param name="IsDeployed">Whether the actor initially participates in an encounter.</param>
/// <param name="CommandAuthorityId">
/// An opaque host-routing key identifying the command source responsible for the actor.
/// </param>
/// <param name="Progression">Optional complete progression state matching <paramref name="Level"/>.</param>
public sealed record CatalogBattleActorCreationRequest(
    ContentId EntityId,
    RuntimeInstanceId InstanceId,
    ContentId TeamId,
    int Level,
    bool IsDeployed,
    ContentId CommandAuthorityId,
    RuntimeProgressionSnapshot? Progression = null);

public sealed record CatalogBattleActorRestoreRequest
{
    public CatalogBattleActorRestoreRequest(
        RuntimeActorSnapshot snapshot,
        RuntimeStatSourceKind statSourceKind,
        MissingHostedEntityBehavior missingHostedEntityBehavior,
        RuntimePartyRosterSnapshot? partyRoster = null,
        IEnumerable<RuntimeActorState>? runtimeActors = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers = null)
        : this(
            snapshot,
            statSourceKind,
            missingHostedEntityBehavior,
            partyRoster,
            runtimeActors,
            equipmentStatModifiers,
            preserveValidatedSnapshot: false)
    {
    }

    private CatalogBattleActorRestoreRequest(
        RuntimeActorSnapshot snapshot,
        RuntimeStatSourceKind statSourceKind,
        MissingHostedEntityBehavior missingHostedEntityBehavior,
        RuntimePartyRosterSnapshot? partyRoster,
        IEnumerable<RuntimeActorState>? runtimeActors,
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers,
        bool preserveValidatedSnapshot)
    {
        if (!Enum.IsDefined(statSourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(statSourceKind), "Stat source kind is not supported.");
        }
        if (!Enum.IsDefined(missingHostedEntityBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingHostedEntityBehavior),
                "Missing hosted-entity behavior is not supported.");
        }
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        StatSourceKind = statSourceKind;
        MissingHostedEntityBehavior = missingHostedEntityBehavior;
        PartyRoster = partyRoster;
        RuntimeActors = Array.AsReadOnly((runtimeActors ?? []).ToArray());
        EquipmentStatModifiers = RuntimeSnapshotCollections.Dictionary(equipmentStatModifiers);
        PreserveValidatedSnapshot = preserveValidatedSnapshot;
    }

    public RuntimeActorSnapshot Snapshot { get; }
    public RuntimeStatSourceKind StatSourceKind { get; }
    public MissingHostedEntityBehavior MissingHostedEntityBehavior { get; }
    public RuntimePartyRosterSnapshot? PartyRoster { get; }
    public IReadOnlyList<RuntimeActorState> RuntimeActors { get; }
    public IReadOnlyDictionary<ContentId, decimal> EquipmentStatModifiers { get; }
    internal bool PreserveValidatedSnapshot { get; }

    internal static CatalogBattleActorRestoreRequest FromValidatedFrameworkSnapshot(
        RuntimeActorSnapshot snapshot) =>
        new(
            snapshot,
            RuntimeStatSourceKind.Actor,
            MissingHostedEntityBehavior.UseActorBaseStats,
            partyRoster: null,
            runtimeActors: null,
            equipmentStatModifiers: null,
            preserveValidatedSnapshot: true);
}

public sealed record BattleActorInitialization
{
    public BattleActorInitialization(
        ContentId vitalResourceId,
        IEnumerable<BattleResourceState> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null)
    {
        VitalResourceId = vitalResourceId;
        Resources = Array.AsReadOnly(
            (resources ?? throw new ArgumentNullException(nameof(resources)))
            .Select(resource => resource.Copy())
            .ToArray());
        BaseResourceValues = RuntimeSnapshotCollections.Dictionary(baseResourceValues);
    }

    public ContentId VitalResourceId { get; }
    public IReadOnlyList<BattleResourceState> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
}

public interface IBattleActorInitializationPolicy
{
    BattleActorInitialization Initialize(EntityDefinition entity, int level);
}

public enum CatalogBattleActorDiagnosticCode
{
    InvalidLevel,
    ProgressionLevelMismatch,
    EntityIdNotQualified,
    EntityMissing,
    SkillMissing,
    InitializationFailed,
    InitializationReturnedNull,
    InitializationResourceDuplicate,
    VitalResourceMissing,
    RuntimeStateConstructionFailed,
    SnapshotActorKindMismatch,
    SnapshotSkillMissing,
    SnapshotAilmentMissing,
    SnapshotCombatProfileCompositionFailed,
    SnapshotInvalid,
    IdentifierInvalid,
    MoveListCapacityRejected,
    SkillUnlockPlanningFailed,
    SnapshotMoveListCapacityRejected,
    SnapshotPendingSkillUnlockMismatch,
    SnapshotPendingSkillLevelUnavailable,
}

public sealed record CatalogBattleActorDiagnostic(
    CatalogBattleActorDiagnosticCode Code,
    string Message,
    ContentId? EntityId = null,
    ContentId? SkillId = null,
    ContentId? ResourceId = null);

public sealed class CatalogBattleActor
{
    private readonly ISkillDefinitionRepository _skills;

    internal CatalogBattleActor(
        EntityDefinition entity,
        RuntimeActorState state,
        ISkillDefinitionRepository skills)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        State = state ?? throw new ArgumentNullException(nameof(state));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    }

    public EntityDefinition Entity { get; }
    public RuntimeActorState State { get; }
    public IReadOnlyList<SkillDefinition> SkillLoadout => Array.AsReadOnly(
        State.Skills.EquippedSkillIds.Select(_skills.GetRequiredSkill).ToArray());
    public IReadOnlyList<SkillDefinition> ActiveSkills => Array.AsReadOnly(
        SkillLoadout.Where(skill => skill.Activation == SkillActivation.Active).ToArray());
}

public sealed class CatalogBattleActorCreationResult
{
    internal CatalogBattleActorCreationResult(
        CatalogBattleActor? actor,
        IEnumerable<CatalogBattleActorDiagnostic> diagnostics)
    {
        Actor = actor;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public CatalogBattleActor? Actor { get; }
    public IReadOnlyList<CatalogBattleActorDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Actor is not null && Diagnostics.Count == 0;

    public CatalogBattleActor RequireActor() => Actor ?? throw new CatalogBattleActorCreationException(Diagnostics);
}

public sealed class CatalogBattleActorCreationException : Exception
{
    public CatalogBattleActorCreationException(IEnumerable<CatalogBattleActorDiagnostic> diagnostics)
        : base("Catalog battle actor creation failed.")
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<CatalogBattleActorDiagnostic> Diagnostics { get; }
}

public interface ICatalogBattleActorFactory
{
    CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request);
    CatalogBattleActorCreationResult Restore(CatalogBattleActorRestoreRequest request);
}

/// <summary>Creates or restores runtime battle actors from qualified catalog definitions.</summary>
public sealed class CatalogBattleActorFactory : ICatalogBattleActorFactory
{
    private readonly IEntityDefinitionRepository _entities;
    private readonly ISkillDefinitionRepository _skills;
    private readonly IAilmentDefinitionRepository? _ailments;
    private readonly IBattleActorInitializationPolicy _initialization;
    private readonly IDurationVocabularyRepository? _durationVocabulary;
    private readonly IRuntimeActorCombatProfileCompositionService _combatProfileComposition;
    private readonly IRuntimeMoveListCapacityPolicy _moveListCapacityPolicy;

    public CatalogBattleActorFactory(
        IEntityDefinitionRepository entities,
        ISkillDefinitionRepository skills,
        IBattleActorInitializationPolicy initialization,
        IAilmentDefinitionRepository? ailments = null,
        IRuntimeActorCombatProfileCompositionService? combatProfileComposition = null,
        IRuntimeMoveListCapacityPolicy? moveListCapacityPolicy = null)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _initialization = initialization ?? throw new ArgumentNullException(nameof(initialization));
        _ailments = ailments ?? entities as IAilmentDefinitionRepository;
        _durationVocabulary = entities as IDurationVocabularyRepository ??
            skills as IDurationVocabularyRepository ??
            _ailments as IDurationVocabularyRepository;
        _combatProfileComposition = combatProfileComposition ??
            new RuntimeActorCombatProfileCompositionService(skills);
        _moveListCapacityPolicy = moveListCapacityPolicy ??
            new SharedRuntimeMoveListCapacityPolicy();
    }

    public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<CatalogBattleActorDiagnostic>();
        if (!request.EntityId.IsValid)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.IdentifierInvalid,
                "Catalog actor entity ID cannot be empty.",
                request.EntityId));
        }
        if (!request.InstanceId.IsValid)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.IdentifierInvalid,
                "Runtime actor instance ID cannot be empty.",
                request.EntityId));
        }
        if (!request.TeamId.IsValid)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.IdentifierInvalid,
                "Runtime actor team ID cannot be empty.",
                request.EntityId));
        }
        if (!request.CommandAuthorityId.IsValid)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.IdentifierInvalid,
                "Runtime actor command-authority ID cannot be empty.",
                request.EntityId));
        }
        if (diagnostics.Count > 0)
        {
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        bool levelIsConsistent = true;
        if (request.Level <= 0)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.InvalidLevel,
                "Runtime actor level must be positive.",
                request.EntityId));
            levelIsConsistent = false;
        }

        if (request.Progression is not null && request.Progression.Level != request.Level)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.ProgressionLevelMismatch,
                $"Requested level '{request.Level}' does not match progression level '{request.Progression.Level}'.",
                request.EntityId));
            levelIsConsistent = false;
        }

        int actorLevel = request.Progression?.Level ?? request.Level;

        if (!request.EntityId.IsQualified)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.EntityIdNotQualified,
                "Catalog actor creation requires a pack-qualified entity ID.",
                request.EntityId));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        if (!_entities.TryGetEntity(request.EntityId, out EntityDefinition? entity) || entity is null)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.EntityMissing,
                $"No entity definition exists for '{request.EntityId}'.",
                request.EntityId));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        var baseSkillIds = new List<ContentId>();
        var orderedSkillIds = new List<ContentId>();
        var seenSkillIds = new HashSet<ContentId>();
        foreach (ContentId skillId in entity.BaseSkillIds)
        {
            if (!seenSkillIds.Add(skillId))
            {
                continue;
            }

            baseSkillIds.Add(skillId);
            orderedSkillIds.Add(skillId);
        }
        foreach (SkillUnlockDefinition unlock in entity.SkillUnlocks.Where(
                     unlock => levelIsConsistent && unlock.Level <= actorLevel))
        {
            if (seenSkillIds.Add(unlock.SkillId)) orderedSkillIds.Add(unlock.SkillId);
        }

        var resolvedSkills = new Dictionary<ContentId, SkillDefinition>();
        foreach (ContentId skillId in orderedSkillIds)
        {
            if (_skills.TryGetSkill(skillId, out SkillDefinition? skill) && skill is not null)
            {
                resolvedSkills.Add(skillId, skill);
            }
            else
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.SkillMissing,
                    $"Entity '{entity.Id}' references missing skill '{skillId}'.",
                    entity.Id,
                    skillId));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        BattleActorInitialization? initialization;
        try
        {
            initialization = _initialization.Initialize(entity, actorLevel);
        }
        catch (Exception exception)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.InitializationFailed,
                exception.Message,
                entity.Id));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        if (initialization is null)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.InitializationReturnedNull,
                "The actor initialization policy returned no initialization state.",
                entity.Id));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        if (!initialization.VitalResourceId.IsValid)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.IdentifierInvalid,
                "Initialization vital resource ID cannot be empty.",
                entity.Id,
                ResourceId: initialization.VitalResourceId));
        }
        for (int index = 0; index < initialization.Resources.Count; index++)
        {
            BattleResourceState resource = initialization.Resources[index];
            if (!resource.Id.IsValid)
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.IdentifierInvalid,
                    $"Initialization resource at index {index} has an empty ID.",
                    entity.Id,
                    ResourceId: resource.Id));
            }
        }
        foreach (ContentId resourceId in initialization.BaseResourceValues.Keys.Where(id => !id.IsValid))
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.IdentifierInvalid,
                "Initialization base resource ID cannot be empty.",
                entity.Id,
                ResourceId: resourceId));
        }

        foreach (ContentId duplicateResourceId in initialization.Resources
                     .GroupBy(resource => resource.Id)
                     .Where(group => group.Skip(1).Any())
                     .Select(group => group.Key))
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.InitializationResourceDuplicate,
                $"Initialization provided resource '{duplicateResourceId}' more than once.",
                entity.Id,
                ResourceId: duplicateResourceId));
        }

        if (!initialization.Resources.Any(resource => resource.Id == initialization.VitalResourceId))
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.VitalResourceMissing,
                $"Initialization did not provide vital resource '{initialization.VitalResourceId}'.",
                entity.Id));
        }

        if (diagnostics.Count > 0)
        {
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        var identity = new RuntimeActorIdentitySnapshot(
            request.InstanceId,
            entity.Id,
            entity.EntityKindId,
            entity.DisplayName);
        var loadout = new List<SkillDefinition>(baseSkillIds.Count);
        foreach (ContentId skillId in baseSkillIds)
        {
            SkillDefinition skill = resolvedSkills[skillId];
            RuntimeMoveListCapacityViolation? violation =
                RuntimeMoveListCapacityValidation.ValidateAddition(
                    identity,
                    loadout,
                    skill,
                    _moveListCapacityPolicy);
            if (violation is not null)
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.MoveListCapacityRejected,
                    violation.Message,
                    entity.Id,
                    violation.SkillId));
                return new CatalogBattleActorCreationResult(null, diagnostics);
            }

            loadout.Add(skill);
        }

        RuntimeProgressionSnapshot progression = request.Progression ??
            new RuntimeProgressionSnapshot(actorLevel, 0, 0, 0);
        try
        {
            var state = new RuntimeActorState(
                request.InstanceId,
                entity.Id,
                request.TeamId,
                initialization.VitalResourceId,
                CombatDefenseProfile.FromEntityDefinition(entity),
                initialization.Resources,
                new RuntimeEncounterPresenceSnapshot(request.IsDeployed),
                new RuntimeActorAffiliationSnapshot(
                    request.CommandAuthorityId,
                    request.TeamId),
                stats: entity.Stats.Select(pair =>
                    new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
                skillIds: loadout.Select(skill => skill.Id),
                capabilityIds: [],
                passiveSkills: loadout.Where(skill => skill.Activation == SkillActivation.Passive),
                identity: identity,
                progression: progression,
                baseResourceValues: initialization.BaseResourceValues.Select(pair =>
                    new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
                baseStats: entity.Stats.Select(pair =>
                    new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
                skillState: new RuntimeSkillStateSnapshot(
                    loadout.Select(skill => skill.Id),
                    loadout.Select(skill => skill.Id)));

            RuntimeSkillUnlockPlanResult unlockPlan = new RuntimeSkillUnlockPlanner(_skills).Plan(
                new RuntimeSkillUnlockPlanRequest(
                    state.ToSnapshot(),
                    entity,
                    previousLevel: 0,
                    _moveListCapacityPolicy));
            if (!unlockPlan.Planned)
            {
                diagnostics.AddRange(unlockPlan.Diagnostics.Select(diagnostic =>
                    new CatalogBattleActorDiagnostic(
                        CatalogBattleActorDiagnosticCode.SkillUnlockPlanningFailed,
                        diagnostic.Message,
                        entity.Id,
                        diagnostic.SkillId)));
                return new CatalogBattleActorCreationResult(null, diagnostics);
            }

            SkillDefinition[] equippedDefinitions = unlockPlan.After.EquippedSkillIds
                .Select(_skills.GetRequiredSkill)
                .ToArray();
            state.ApplySkillState(unlockPlan.After, equippedDefinitions);

            return new CatalogBattleActorCreationResult(
                new CatalogBattleActor(entity, state, _skills),
                diagnostics);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.RuntimeStateConstructionFailed,
                $"Initialization produced invalid runtime actor state: {exception.Message}",
                entity.Id));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }
    }

    public CatalogBattleActorCreationResult Restore(CatalogBattleActorRestoreRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeActorSnapshot snapshot = request.Snapshot;
        var diagnostics = new List<CatalogBattleActorDiagnostic>();
        ContentId entityId = snapshot.Identity.EntityDefinitionId;
        if (!entityId.IsValid || !snapshot.Identity.InstanceId.IsValid ||
            !snapshot.Identity.ActorKindId.IsValid ||
            !snapshot.Affiliation.CommandAuthorityId.IsValid ||
            !snapshot.Affiliation.TeamId.IsValid ||
            !snapshot.VitalResourceId.IsValid)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.SnapshotInvalid,
                "Saved actor identity and affiliation IDs must be non-empty.",
                entityId));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        if (!_entities.TryGetEntity(entityId, out EntityDefinition? entity) || entity is null)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.EntityMissing,
                $"No entity definition exists for '{entityId}'.",
                entityId));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        if (snapshot.Identity.ActorKindId != entity.EntityKindId)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.SnapshotActorKindMismatch,
                $"Saved actor kind '{snapshot.Identity.ActorKindId}' does not match entity kind '{entity.EntityKindId}'.",
                entityId));
        }

        ContentId[] skillIds = snapshot.Skills.LearnedSkillIds
            .Concat(snapshot.Skills.EquippedSkillIds)
            .Concat(snapshot.Skills.PendingChoices.Select(choice => choice.SkillId))
            .Distinct()
            .ToArray();
        var resolvedSkills = new Dictionary<ContentId, SkillDefinition>();
        foreach (ContentId skillId in skillIds)
        {
            if (!skillId.IsValid)
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.SnapshotInvalid,
                    "Saved actor skill ID cannot be empty.",
                    entityId,
                    skillId));
                continue;
            }

            if (_skills.TryGetSkill(skillId, out SkillDefinition? skill) && skill is not null)
            {
                resolvedSkills.Add(skillId, skill);
            }
            else
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.SnapshotSkillMissing,
                    $"Saved actor references missing skill '{skillId}'.",
                    entityId,
                    skillId));
            }
        }

        foreach (RuntimePendingSkillChoiceSnapshot choice in snapshot.Skills.PendingChoices)
        {
            if (!choice.SkillId.IsValid || !resolvedSkills.ContainsKey(choice.SkillId))
            {
                continue;
            }

            if (!entity.SkillUnlocks.Any(unlock =>
                    unlock.Level == choice.UnlockLevel &&
                    unlock.SkillId == choice.SkillId))
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.SnapshotPendingSkillUnlockMismatch,
                    $"Pending skill '{choice.SkillId}' at level {choice.UnlockLevel} is not an " +
                    $"authored unlock for entity '{entity.Id}'.",
                    entityId,
                    choice.SkillId));
            }
            if (choice.UnlockLevel > snapshot.Progression.Level)
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.SnapshotPendingSkillLevelUnavailable,
                    $"Pending skill '{choice.SkillId}' unlocks at level {choice.UnlockLevel}, " +
                    $"but actor '{snapshot.Identity.InstanceId}' is level " +
                    $"{snapshot.Progression.Level}.",
                    entityId,
                    choice.SkillId));
            }
        }

        var ailments = new Dictionary<ContentId, AilmentDefinition>();
        foreach (RuntimeTimedStateSnapshot ailment in snapshot.BattleStatus.Ailments)
        {
            if (!ailment.Id.IsValid)
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.SnapshotInvalid,
                    "Saved actor ailment ID cannot be empty.",
                    entityId));
                continue;
            }

            if (_ailments?.TryGetAilment(ailment.Id, out AilmentDefinition? definition) == true && definition is not null)
            {
                ailments.TryAdd(ailment.Id, definition);
            }
            else
            {
                diagnostics.Add(new CatalogBattleActorDiagnostic(
                    CatalogBattleActorDiagnosticCode.SnapshotAilmentMissing,
                    $"Saved actor references missing ailment '{ailment.Id}'.",
                    entityId));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        SkillDefinition[] loadout = snapshot.Skills.EquippedSkillIds
            .Select(skillId => resolvedSkills[skillId])
            .ToArray();
        RuntimeMoveListCapacityViolation? capacityViolation =
            RuntimeMoveListCapacityValidation.ValidateCurrent(
                snapshot.Identity,
                loadout,
                _moveListCapacityPolicy);
        if (capacityViolation is not null)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.SnapshotMoveListCapacityRejected,
                capacityViolation.Message,
                entityId,
                capacityViolation.SkillId));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        try
        {
            RuntimeActorState state = RuntimeActorState.Restore(
                snapshot,
                CombatDefenseProfile.FromEntityDefinition(entity),
                loadout.Where(skill => skill.Activation == SkillActivation.Passive),
                ailments.Values,
                registeredEventIds: _durationVocabulary?.RegisteredEventIds,
                registeredPhaseIds: _durationVocabulary?.RegisteredPhaseIds);

            if (!request.PreserveValidatedSnapshot)
            {
                RuntimeActorCombatProfileCompositionResult composition =
                    _combatProfileComposition.Compose(
                    new RuntimeActorCombatProfileCompositionRequest(
                        state,
                        request.StatSourceKind,
                        request.MissingHostedEntityBehavior,
                        request.PartyRoster,
                        request.RuntimeActors,
                        request.EquipmentStatModifiers));
                if (!composition.Applied)
                {
                    diagnostics.AddRange(composition.Diagnostics.Select(diagnostic =>
                        new CatalogBattleActorDiagnostic(
                            CatalogBattleActorDiagnosticCode.SnapshotCombatProfileCompositionFailed,
                            diagnostic.Message,
                            entityId)));
                    return new CatalogBattleActorCreationResult(null, diagnostics);
                }
            }

            return new CatalogBattleActorCreationResult(
                new CatalogBattleActor(entity, state, _skills),
                diagnostics);
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.SnapshotInvalid,
                exception.Message,
                entityId));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }
    }
}
