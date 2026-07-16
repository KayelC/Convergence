using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

public enum RuntimeActorCombatProfileCompositionStatus
{
    Applied,
    Rejected
}

public enum RuntimeActorCombatProfileCompositionDiagnosticCode
{
    MissingActiveHostedEntity,
    ActiveHostedEntityStateMissing,
    ActiveHostedEntityIdentityMismatch,
    DuplicateRuntimeActorState,
    PartyRosterOwnerMismatch,
    StatResolutionFailed,
    ResourceRecalculationFailed,
    SkillDefinitionMissing,
    InvalidSkillState,
    MissingPartyRoster,
    RosterInvariantViolation,
    CommitFailed
}

public sealed record RuntimeActorCombatProfileCompositionDiagnostic(
    RuntimeActorCombatProfileCompositionDiagnosticCode Code,
    string Message,
    ContentId? StatId = null,
    RuntimeInstanceId? InstanceId = null,
    ContentId? SkillId = null);

public sealed record RuntimeActorCombatProfileCompositionRequest
{
    public RuntimeActorCombatProfileCompositionRequest(
        RuntimeActorState actor,
        RuntimeStatSourceKind sourceKind,
        MissingHostedEntityBehavior missingHostedEntityBehavior,
        RuntimePartyRosterSnapshot? partyRoster = null,
        IEnumerable<RuntimeActorState>? runtimeActors = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers = null)
    {
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), "Stat source kind is not supported.");
        }
        if (!Enum.IsDefined(missingHostedEntityBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingHostedEntityBehavior),
                "Missing hosted-entity behavior is not supported.");
        }

        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        SourceKind = sourceKind;
        MissingHostedEntityBehavior = missingHostedEntityBehavior;
        PartyRoster = partyRoster;
        RuntimeActors = Array.AsReadOnly(
            (runtimeActors ?? []).Select(runtimeActor =>
                runtimeActor ?? throw new ArgumentException(
                    "Runtime actor maps cannot contain null entries.",
                    nameof(runtimeActors)))
            .ToArray());
        EquipmentStatModifiers = RuntimeSnapshotCollections.Dictionary(equipmentStatModifiers);
    }

    public RuntimeActorState Actor { get; }
    public RuntimeStatSourceKind SourceKind { get; }
    public MissingHostedEntityBehavior MissingHostedEntityBehavior { get; }
    public RuntimePartyRosterSnapshot? PartyRoster { get; }
    public IReadOnlyList<RuntimeActorState> RuntimeActors { get; }
    public IReadOnlyDictionary<ContentId, decimal> EquipmentStatModifiers { get; }
}

public sealed record RuntimeActorCombatProfileCompositionResult
{
    public RuntimeActorCombatProfileCompositionResult(
        RuntimeActorCombatProfileCompositionStatus status,
        RuntimeActorSnapshot before,
        RuntimeActorSnapshot after,
        RuntimeStatSourceKind resolvedSourceKind,
        RuntimeInstanceId sourceActorId,
        IEnumerable<StatResolutionResult>? statResolutions = null,
        IEnumerable<RuntimeActorCombatProfileCompositionDiagnostic>? diagnostics = null)
    {
        if (!sourceActorId.IsValid)
        {
            throw new ArgumentException("Composition source actor ID cannot be empty.", nameof(sourceActorId));
        }

        Status = status;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        ResolvedSourceKind = resolvedSourceKind;
        SourceActorId = sourceActorId;
        StatResolutions = RuntimeSnapshotCollections.List(statResolutions);
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeActorCombatProfileCompositionStatus Status { get; }
    public bool Applied => Status == RuntimeActorCombatProfileCompositionStatus.Applied;
    public RuntimeActorSnapshot Before { get; }
    public RuntimeActorSnapshot After { get; }
    public RuntimeStatSourceKind ResolvedSourceKind { get; }
    public RuntimeInstanceId SourceActorId { get; }
    public IReadOnlyList<StatResolutionResult> StatResolutions { get; }
    public IReadOnlyList<RuntimeActorCombatProfileCompositionDiagnostic> Diagnostics { get; }
}

public interface IRuntimeActorCombatProfileCompositionService
{
    RuntimeActorCombatProfileCompositionResult Compose(
        RuntimeActorCombatProfileCompositionRequest request);
}

public sealed class RuntimeActorCombatProfileCompositionService :
    IRuntimeActorCombatProfileCompositionService
{
    private readonly IStatResolutionPolicy _statResolution;
    private readonly IResourceGrowthPolicy _resourceGrowth;
    private readonly ISkillDefinitionRepository _skills;

    public RuntimeActorCombatProfileCompositionService(ISkillDefinitionRepository skills)
        : this(new StandardStatResolutionPolicy(), new StandardResourceGrowthPolicy(), skills)
    {
    }

    public RuntimeActorCombatProfileCompositionService(
        IStatResolutionPolicy statResolution,
        IResourceGrowthPolicy resourceGrowth,
        ISkillDefinitionRepository skills)
    {
        _statResolution = statResolution ?? throw new ArgumentNullException(nameof(statResolution));
        _resourceGrowth = resourceGrowth ?? throw new ArgumentNullException(nameof(resourceGrowth));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    }

    public RuntimeActorCombatProfileCompositionResult Compose(
        RuntimeActorCombatProfileCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuntimeActorState actor = request.Actor;
        RuntimeActorSnapshot before = actor.ToSnapshot();
        RuntimeStatSourceKind resolvedSource = request.SourceKind;
        RuntimeActorState sourceActor = actor;
        IReadOnlyDictionary<ContentId, decimal> hostedStats =
            RuntimeSnapshotCollections.Dictionary<ContentId, decimal>();

        RuntimePartyRosterSnapshot? partyRoster = request.PartyRoster;
        if (partyRoster is not null)
        {
            IReadOnlyList<RuntimePartyRosterInvariantDiagnostic> rosterDiagnostics =
                RuntimePartyRosterInvariantRules.Validate(partyRoster);
            if (rosterDiagnostics.Count > 0)
            {
                RuntimePartyRosterInvariantDiagnostic first = rosterDiagnostics[0];
                return Rejected(
                    before,
                    resolvedSource,
                    actor.InstanceId,
                    RuntimeActorCombatProfileCompositionDiagnosticCode.RosterInvariantViolation,
                    $"Party roster is invalid at '{first.Path}': {first.Message}",
                    instanceId: first.InstanceId);
            }

        }

        if (request.SourceKind == RuntimeStatSourceKind.ActiveHostedEntity)
        {
            if (partyRoster is null)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    actor.InstanceId,
                    RuntimeActorCombatProfileCompositionDiagnosticCode.MissingPartyRoster,
                    "Active Hosted Entity combat-profile composition requires the canonical party roster.",
                    instanceId: actor.InstanceId);
            }

            if (partyRoster.Owner.InstanceId != actor.InstanceId ||
                partyRoster.Owner.EntityDefinitionId != actor.EntityId)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    actor.InstanceId,
                    RuntimeActorCombatProfileCompositionDiagnosticCode.PartyRosterOwnerMismatch,
                    $"Party roster owner '{partyRoster.Owner.InstanceId}' does not match actor " +
                    $"'{actor.InstanceId}'.",
                    instanceId: partyRoster.Owner.InstanceId);
            }

            RuntimeActorReferenceSnapshot? activeReference = partyRoster.ActiveHostedEntity;
            if (activeReference is null)
            {
                if (request.MissingHostedEntityBehavior == MissingHostedEntityBehavior.RejectStatResolution)
                {
                    return Rejected(
                        before,
                        resolvedSource,
                        actor.InstanceId,
                        RuntimeActorCombatProfileCompositionDiagnosticCode.MissingActiveHostedEntity,
                        $"Vessel '{actor.InstanceId}' has no active hosted entity.",
                        instanceId: actor.InstanceId);
                }

                resolvedSource = RuntimeStatSourceKind.Actor;
            }
            else
            {
                RuntimeActorState[] matchingStates = request.RuntimeActors
                    .Where(candidate => candidate.InstanceId == activeReference.InstanceId)
                    .ToArray();
                if (matchingStates.Length == 0)
                {
                    return Rejected(
                        before,
                        resolvedSource,
                        actor.InstanceId,
                        RuntimeActorCombatProfileCompositionDiagnosticCode.ActiveHostedEntityStateMissing,
                        $"Active Hosted Entity '{activeReference.InstanceId}' has no runtime state.",
                        instanceId: activeReference.InstanceId);
                }
                if (matchingStates.Length > 1)
                {
                    return Rejected(
                        before,
                        resolvedSource,
                        actor.InstanceId,
                        RuntimeActorCombatProfileCompositionDiagnosticCode.DuplicateRuntimeActorState,
                        $"Runtime actor map contains more than one state for " +
                        $"'{activeReference.InstanceId}'.",
                        instanceId: activeReference.InstanceId);
                }

                sourceActor = matchingStates[0];
                if (sourceActor.EntityId != activeReference.EntityDefinitionId)
                {
                    return Rejected(
                        before,
                        resolvedSource,
                        actor.InstanceId,
                        RuntimeActorCombatProfileCompositionDiagnosticCode.ActiveHostedEntityIdentityMismatch,
                        $"Runtime state '{sourceActor.InstanceId}' has entity '{sourceActor.EntityId}', " +
                        $"but the roster references '{activeReference.EntityDefinitionId}'.",
                        instanceId: activeReference.InstanceId);
                }

                hostedStats = sourceActor.BaseStats;
            }
        }

        var resolutions = new List<StatResolutionResult>(StandardProgressionIds.CoreStats.Count);
        HashSet<ContentId> composedStatIds = [.. StandardProgressionIds.CoreStats];
        var effectiveStats = before.Stats.EffectiveStats
            .Where(pair => !composedStatIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (ContentId statId in StandardProgressionIds.CoreStats)
        {
            StatResolutionResult resolution;
            try
            {
                resolution = _statResolution.Resolve(new StatResolutionRequest(
                    resolvedSource,
                    statId,
                    actor.BaseStats,
                    hostedStats,
                    request.EquipmentStatModifiers));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    sourceActor.InstanceId,
                    RuntimeActorCombatProfileCompositionDiagnosticCode.StatResolutionFailed,
                    $"Stat '{statId}' could not be resolved: {exception.Message}",
                    statId);
            }

            resolutions.Add(resolution);
            effectiveStats.Add(statId, resolution.FinalValue);
        }

        ResourceRecalculationResult resources;
        try
        {
            resources = _resourceGrowth.Recalculate(new ResourceRecalculationRequest(
                before.Resources,
                before.BaseResourceValues,
                effectiveStats,
                ResourceCurrentAdjustmentMode.PreserveCurrent));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Rejected(
                before,
                resolvedSource,
                sourceActor.InstanceId,
                RuntimeActorCombatProfileCompositionDiagnosticCode.ResourceRecalculationFailed,
                $"Resources could not be recalculated: {exception.Message}");
        }

        RuntimeSkillStateSnapshot sourceSkills = sourceActor.Skills;
        IReadOnlyList<RuntimeActorCombatProfileCompositionDiagnostic> skillDiagnostics =
            ValidateSkillState(sourceSkills, sourceActor.InstanceId);
        if (skillDiagnostics.Count > 0)
        {
            return new RuntimeActorCombatProfileCompositionResult(
                RuntimeActorCombatProfileCompositionStatus.Rejected,
                before,
                before,
                resolvedSource,
                sourceActor.InstanceId,
                diagnostics: skillDiagnostics);
        }

        var resolvedSkills = new List<SkillDefinition>(sourceSkills.EquippedSkillIds.Count);
        foreach (ContentId skillId in sourceSkills.LearnedSkillIds)
        {
            if (!_skills.TryGetSkill(skillId, out SkillDefinition? skill) || skill is null)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    sourceActor.InstanceId,
                    RuntimeActorCombatProfileCompositionDiagnosticCode.SkillDefinitionMissing,
                    $"Combat-profile source '{sourceActor.InstanceId}' references missing learned skill " +
                    $"'{skillId}'.",
                    skillId: skillId);
            }
        }
        foreach (ContentId skillId in sourceSkills.EquippedSkillIds)
        {
            if (!_skills.TryGetSkill(skillId, out SkillDefinition? skill) ||
                skill is null)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    sourceActor.InstanceId,
                    RuntimeActorCombatProfileCompositionDiagnosticCode.SkillDefinitionMissing,
                    $"Combat-profile source '{sourceActor.InstanceId}' references missing equipped skill " +
                    $"'{skillId}'.",
                    skillId: skillId);
            }

            resolvedSkills.Add(skill);
        }

        try
        {
            actor.ApplyCombatProfile(
                effectiveStats,
                resources.Resources,
                sourceActor.DefenseProfile,
                sourceSkills,
                resolvedSkills);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Rejected(
                before,
                resolvedSource,
                sourceActor.InstanceId,
                RuntimeActorCombatProfileCompositionDiagnosticCode.CommitFailed,
                $"Composed actor state could not be committed: {exception.Message}");
        }

        return new RuntimeActorCombatProfileCompositionResult(
            RuntimeActorCombatProfileCompositionStatus.Applied,
            before,
            actor.ToSnapshot(),
            resolvedSource,
            sourceActor.InstanceId,
            resolutions);
    }

    private IReadOnlyList<RuntimeActorCombatProfileCompositionDiagnostic> ValidateSkillState(
        RuntimeSkillStateSnapshot skills,
        RuntimeInstanceId sourceActorId)
    {
        var diagnostics = new List<RuntimeActorCombatProfileCompositionDiagnostic>();
        var learned = new HashSet<ContentId>();
        for (int index = 0; index < skills.LearnedSkillIds.Count; index++)
        {
            ContentId skillId = skills.LearnedSkillIds[index];
            if (!skillId.IsValid || !learned.Add(skillId))
            {
                diagnostics.Add(new RuntimeActorCombatProfileCompositionDiagnostic(
                    RuntimeActorCombatProfileCompositionDiagnosticCode.InvalidSkillState,
                    !skillId.IsValid
                        ? $"Learned skill at index {index} has an empty ID."
                        : $"Learned skill '{skillId}' appears more than once.",
                    InstanceId: sourceActorId,
                    SkillId: skillId));
            }
        }

        var equipped = new HashSet<ContentId>();
        for (int index = 0; index < skills.EquippedSkillIds.Count; index++)
        {
            ContentId skillId = skills.EquippedSkillIds[index];
            string? message = !skillId.IsValid
                ? $"Equipped skill at index {index} has an empty ID."
                : !equipped.Add(skillId)
                    ? $"Equipped skill '{skillId}' appears more than once."
                    : !learned.Contains(skillId)
                        ? $"Equipped skill '{skillId}' is not learned."
                        : null;
            if (message is not null)
            {
                diagnostics.Add(new RuntimeActorCombatProfileCompositionDiagnostic(
                    RuntimeActorCombatProfileCompositionDiagnosticCode.InvalidSkillState,
                    message,
                    InstanceId: sourceActorId,
                    SkillId: skillId));
            }
        }

        return RuntimeSnapshotCollections.List(diagnostics);
    }

    private static RuntimeActorCombatProfileCompositionResult Rejected(
        RuntimeActorSnapshot before,
        RuntimeStatSourceKind resolvedSource,
        RuntimeInstanceId sourceActorId,
        RuntimeActorCombatProfileCompositionDiagnosticCode code,
        string message,
        ContentId? statId = null,
        RuntimeInstanceId? instanceId = null,
        ContentId? skillId = null) =>
        new(
            RuntimeActorCombatProfileCompositionStatus.Rejected,
            before,
            before,
            resolvedSource,
            sourceActorId,
            diagnostics:
            [
                new RuntimeActorCombatProfileCompositionDiagnostic(
                    code,
                    message,
                    statId,
                    instanceId,
                    skillId)
            ]);
}
