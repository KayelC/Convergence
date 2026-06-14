using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Execution;

namespace JRPGPrototype.Logic.Battle.Runtime;

public sealed record CatalogBattleActorCreationRequest(
    ContentId EntityId,
    ContentId InstanceId,
    ContentId TeamId,
    int Level);

public sealed record BattleActorInitialization
{
    public BattleActorInitialization(ContentId vitalResourceId, IEnumerable<BattleResourceState> resources)
    {
        VitalResourceId = vitalResourceId;
        Resources = Array.AsReadOnly(
            (resources ?? throw new ArgumentNullException(nameof(resources)))
            .Select(resource => resource.Copy())
            .ToArray());
    }

    public ContentId VitalResourceId { get; }
    public IReadOnlyList<BattleResourceState> Resources { get; }
}

public interface IBattleActorInitializationPolicy
{
    BattleActorInitialization Initialize(EntityDefinition entity, int level);
}

public enum CatalogBattleActorDiagnosticCode
{
    InvalidLevel,
    EntityIdNotQualified,
    EntityMissing,
    SkillMissing,
    InitializationFailed,
    VitalResourceMissing
}

public sealed record CatalogBattleActorDiagnostic(
    CatalogBattleActorDiagnosticCode Code,
    string Message,
    ContentId? EntityId = null,
    ContentId? SkillId = null);

public sealed class CatalogBattleActor
{
    internal CatalogBattleActor(
        EntityDefinition entity,
        BattleActorState state,
        IEnumerable<SkillDefinition> loadout)
    {
        Entity = entity;
        State = state;
        SkillLoadout = Array.AsReadOnly(loadout.ToArray());
        ActiveSkills = Array.AsReadOnly(
            SkillLoadout.Where(skill => skill.Activation == SkillActivation.Active).ToArray());
    }

    public EntityDefinition Entity { get; }
    public BattleActorState State { get; }
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
}

public sealed class CatalogBattleActorFactory : ICatalogBattleActorFactory
{
    private readonly IEntityDefinitionRepository _entities;
    private readonly ISkillDefinitionRepository _skills;
    private readonly IBattleActorInitializationPolicy _initialization;

    public CatalogBattleActorFactory(
        IEntityDefinitionRepository entities,
        ISkillDefinitionRepository skills,
        IBattleActorInitializationPolicy initialization)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _initialization = initialization ?? throw new ArgumentNullException(nameof(initialization));
    }

    public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request)
    {
        var diagnostics = new List<CatalogBattleActorDiagnostic>();
        if (request.Level <= 0)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.InvalidLevel,
                "Runtime actor level must be positive.",
                request.EntityId));
        }

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
        foreach (SkillUnlockDefinition unlock in entity.SkillUnlocks.Where(unlock => unlock.Level <= request.Level))
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

        BattleActorInitialization initialization;
        try
        {
            initialization = _initialization.Initialize(entity, request.Level);
        }
        catch (Exception exception)
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.InitializationFailed,
                exception.Message,
                entity.Id));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        if (!initialization.Resources.Any(resource => resource.Id == initialization.VitalResourceId))
        {
            diagnostics.Add(new CatalogBattleActorDiagnostic(
                CatalogBattleActorDiagnosticCode.VitalResourceMissing,
                $"Initialization did not provide vital resource '{initialization.VitalResourceId}'.",
                entity.Id));
            return new CatalogBattleActorCreationResult(null, diagnostics);
        }

        var state = new BattleActorState(
            request.InstanceId,
            entity.Id,
            request.TeamId,
            initialization.VitalResourceId,
            CombatDefenseProfile.FromEntityDefinition(entity),
            initialization.Resources,
            entity.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
            loadout.Select(skill => skill.Id),
            capabilityIds: [],
            passiveSkills: loadout.Where(skill => skill.Activation == SkillActivation.Passive));

        return new CatalogBattleActorCreationResult(new CatalogBattleActor(entity, state, loadout), diagnostics);
    }
}
