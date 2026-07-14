using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Runtime;

public sealed record CatalogBattleActorCreationRequest(
    ContentId EntityId,
    RuntimeInstanceId InstanceId,
    ContentId TeamId,
    int Level,
    RuntimeProgressionSnapshot? Progression = null,
    ContentId? ControllerId = null,
    RuntimeActorDeployment Deployment = RuntimeActorDeployment.Deployed,
    bool IsActive = true);

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
        BaseResourceValues = new Dictionary<ContentId, decimal>(baseResourceValues ?? []);
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
    SnapshotInvalid
}

public sealed record CatalogBattleActorDiagnostic(
    CatalogBattleActorDiagnosticCode Code,
    string Message,
    ContentId? EntityId = null,
    ContentId? SkillId = null,
    ContentId? ResourceId = null);

public sealed class CatalogBattleActor
{
    internal CatalogBattleActor(
        EntityDefinition entity,
        RuntimeActorState state,
        IEnumerable<SkillDefinition> loadout)
    {
        Entity = entity;
        State = state;
        SkillLoadout = Array.AsReadOnly(loadout.ToArray());
        ActiveSkills = Array.AsReadOnly(
            SkillLoadout.Where(skill => skill.Activation == SkillActivation.Active).ToArray());
    }

    public EntityDefinition Entity { get; }
    public RuntimeActorState State { get; }
    public IReadOnlyList<SkillDefinition> SkillLoadout { get; }
    public IReadOnlyList<SkillDefinition> ActiveSkills { get; }
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
    CatalogBattleActorCreationResult Restore(RuntimeActorSnapshot snapshot);
}

public sealed class CatalogBattleActorFactory : ICatalogBattleActorFactory
{
    private readonly IEntityDefinitionRepository _entities;
    private readonly ISkillDefinitionRepository _skills;
    private readonly IAilmentDefinitionRepository? _ailments;
    private readonly IBattleActorInitializationPolicy _initialization;
    private readonly IDurationVocabularyRepository? _durationVocabulary;

    public CatalogBattleActorFactory(
        IEntityDefinitionRepository entities,
        ISkillDefinitionRepository skills,
        IBattleActorInitializationPolicy initialization,
        IAilmentDefinitionRepository? ailments = null)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _initialization = initialization ?? throw new ArgumentNullException(nameof(initialization));
        _ailments = ailments ?? entities as IAilmentDefinitionRepository;
        _durationVocabulary = entities as IDurationVocabularyRepository ??
            skills as IDurationVocabularyRepository ??
            _ailments as IDurationVocabularyRepository;
    }

    public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<CatalogBattleActorDiagnostic>();
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

        var orderedSkillIds = new List<ContentId>();
        var seenSkillIds = new HashSet<ContentId>();
        foreach (ContentId skillId in entity.BaseSkillIds)
        {
            if (seenSkillIds.Add(skillId)) orderedSkillIds.Add(skillId);
        }
        foreach (SkillUnlockDefinition unlock in entity.SkillUnlocks.Where(
                     unlock => levelIsConsistent && unlock.Level <= actorLevel))
        {
            if (seenSkillIds.Add(unlock.SkillId)) orderedSkillIds.Add(unlock.SkillId);
        }

        var loadout = new List<SkillDefinition>();
        foreach (ContentId skillId in orderedSkillIds)
        {
            if (_skills.TryGetSkill(skillId, out SkillDefinition? skill) && skill is not null)
            {
                loadout.Add(skill);
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
                entity.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
                loadout.Select(skill => skill.Id),
                capabilityIds: [],
                passiveSkills: loadout.Where(skill => skill.Activation == SkillActivation.Passive),
                isActive: request.IsActive,
                identity: new RuntimeActorIdentitySnapshot(
                    request.InstanceId,
                    entity.Id,
                    entity.EntityKindId,
                    entity.DisplayName),
                ownership: new RuntimeActorOwnershipSnapshot(
                    request.ControllerId ?? ContentId.Parse("runtime"),
                    request.TeamId),
                deployment: new RuntimeActorDeploymentSnapshot(request.Deployment, request.IsActive),
                progression: progression,
                baseResourceValues: initialization.BaseResourceValues.Select(pair =>
                    new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
                baseStats: entity.Stats.Select(pair =>
                    new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
                skillState: new RuntimeSkillStateSnapshot(
                    loadout.Select(skill => skill.Id),
                    loadout.Select(skill => skill.Id)));

            return new CatalogBattleActorCreationResult(new CatalogBattleActor(entity, state, loadout), diagnostics);
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

    public CatalogBattleActorCreationResult Restore(RuntimeActorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var diagnostics = new List<CatalogBattleActorDiagnostic>();
        ContentId entityId = snapshot.Identity.EntityDefinitionId;
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
            .Distinct()
            .ToArray();
        var resolvedSkills = new Dictionary<ContentId, SkillDefinition>();
        foreach (ContentId skillId in skillIds)
        {
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

        var ailments = new Dictionary<ContentId, AilmentDefinition>();
        foreach (RuntimeTimedStateSnapshot ailment in snapshot.BattleStatus.Ailments)
        {
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
        try
        {
            RuntimeActorState state = RuntimeActorState.Restore(
                snapshot,
                CombatDefenseProfile.FromEntityDefinition(entity),
                loadout.Where(skill => skill.Activation == SkillActivation.Passive),
                ailments.Values,
                registeredEventIds: _durationVocabulary?.RegisteredEventIds,
                registeredPhaseIds: _durationVocabulary?.RegisteredPhaseIds);
            return new CatalogBattleActorCreationResult(
                new CatalogBattleActor(entity, state, loadout),
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
