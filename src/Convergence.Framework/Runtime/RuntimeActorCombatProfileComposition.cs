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
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers = null,
        IEnumerable<ContentId>? equipmentGrantedSkillIds = null)
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
        EquipmentGrantedSkillIds = RuntimeSnapshotCollections.List(equipmentGrantedSkillIds);
    }

    public RuntimeActorState Actor { get; }
    public RuntimeStatSourceKind SourceKind { get; }
    public MissingHostedEntityBehavior MissingHostedEntityBehavior { get; }
    public RuntimePartyRosterSnapshot? PartyRoster { get; }
    public IReadOnlyList<RuntimeActorState> RuntimeActors { get; }
    public IReadOnlyDictionary<ContentId, decimal> EquipmentStatModifiers { get; }
    public IReadOnlyList<ContentId> EquipmentGrantedSkillIds { get; }
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
        IEnumerable<RuntimeActorCombatProfileCompositionDiagnostic>? diagnostics = null,
        ContentId? sourceEntityId = null)
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
        SourceEntityId = sourceEntityId ?? Before.Identity.EntityDefinitionId;
        if (!SourceEntityId.IsValid)
        {
            throw new ArgumentException("Composition source entity ID cannot be empty.", nameof(sourceEntityId));
        }
        StatResolutions = RuntimeSnapshotCollections.List(statResolutions);
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeActorCombatProfileCompositionStatus Status { get; }
    public bool Applied => Status == RuntimeActorCombatProfileCompositionStatus.Applied;
    public RuntimeActorSnapshot Before { get; }
    public RuntimeActorSnapshot After { get; }
    public RuntimeStatSourceKind ResolvedSourceKind { get; }
    public RuntimeInstanceId SourceActorId { get; }
    public ContentId SourceEntityId { get; }
    public IReadOnlyList<StatResolutionResult> StatResolutions { get; }
    public IReadOnlyList<RuntimeActorCombatProfileCompositionDiagnostic> Diagnostics { get; }
}

public interface IRuntimeActorCombatProfileCompositionService
{
    RuntimeActorCombatProfileCompositionResult Compose(
        RuntimeActorCombatProfileCompositionRequest request);
}

public enum RuntimeActorEquipmentApplicationDiagnosticCode
{
    EquipmentProfileResolutionFailed = 0,
    EquipmentProfileRejected = 1,
    CombatProfileCompositionRejected = 2,
    CommitFailed = 3,
    RuntimeActorEvidenceRejected = 4,
    EquipmentAssignedToAnotherActor = 5
}

public sealed record RuntimeActorEquipmentApplicationDiagnostic(
    RuntimeActorEquipmentApplicationDiagnosticCode Code,
    string Message,
    RuntimeEquipmentProfileDiagnosticCode? EquipmentProfileCode = null,
    RuntimeActorCombatProfileCompositionDiagnosticCode? CompositionCode = null)
{
    public RuntimeInstanceId? EquipmentInstanceId { get; init; }
    public RuntimeInstanceId? ActorInstanceId { get; init; }
}

/// <summary>
/// Describes one atomic loadout application. <see cref="RuntimeActors"/> is the
/// complete current actor map for the live session and must include <see cref="Actor"/>.
/// </summary>
public sealed record RuntimeActorEquipmentApplicationRequest
{
    public RuntimeActorEquipmentApplicationRequest(
        RuntimeActorState actor,
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        IEquipmentDefinitionRepository equipmentRepository,
        RuntimeStatSourceKind sourceKind,
        MissingHostedEntityBehavior missingHostedEntityBehavior,
        IEnumerable<RuntimeActorState> runtimeActors,
        RuntimePartyRosterSnapshot? partyRoster = null)
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
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        EquipmentRepository = equipmentRepository ??
            throw new ArgumentNullException(nameof(equipmentRepository));
        SourceKind = sourceKind;
        MissingHostedEntityBehavior = missingHostedEntityBehavior;
        PartyRoster = partyRoster;
        RuntimeActors = Array.AsReadOnly(
            (runtimeActors ?? throw new ArgumentNullException(nameof(runtimeActors))).Select(runtimeActor =>
                runtimeActor ?? throw new ArgumentException(
                    "Runtime actor maps cannot contain null entries.",
                    nameof(runtimeActors)))
            .ToArray());
    }

    public RuntimeActorState Actor { get; }
    public RuntimeInventorySnapshot Inventory { get; }
    public RuntimeEquipmentSnapshot Equipment { get; }
    public IEquipmentDefinitionRepository EquipmentRepository { get; }
    public RuntimeStatSourceKind SourceKind { get; }
    public MissingHostedEntityBehavior MissingHostedEntityBehavior { get; }
    public RuntimePartyRosterSnapshot? PartyRoster { get; }
    public IReadOnlyList<RuntimeActorState> RuntimeActors { get; }
}

public sealed record RuntimeActorEquipmentApplicationResult
{
    public RuntimeActorEquipmentApplicationResult(
        RuntimeActorSnapshot before,
        RuntimeActorSnapshot after,
        RuntimeEquipmentProfile equipmentProfile,
        RuntimeActorCombatProfileCompositionResult? composition,
        IEnumerable<RuntimeActorEquipmentApplicationDiagnostic>? diagnostics = null)
    {
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        EquipmentProfile = equipmentProfile ?? throw new ArgumentNullException(nameof(equipmentProfile));
        Composition = composition;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public bool Applied => Composition?.Applied == true && Diagnostics.Count == 0;
    public RuntimeActorSnapshot Before { get; }
    public RuntimeActorSnapshot After { get; }
    public RuntimeEquipmentProfile EquipmentProfile { get; }
    public RuntimeActorCombatProfileCompositionResult? Composition { get; }
    public IReadOnlyList<RuntimeActorEquipmentApplicationDiagnostic> Diagnostics { get; }
}

public interface IRuntimeActorEquipmentApplicationService
{
    RuntimeActorEquipmentApplicationResult Apply(
        RuntimeActorEquipmentApplicationRequest request);
}

/// <summary>
/// Atomically applies one candidate loadout and its canonically derived actor combat profile.
/// </summary>
public sealed class RuntimeActorEquipmentApplicationService :
    IRuntimeActorEquipmentApplicationService
{
    private readonly IRuntimeEquipmentProfileResolver _equipmentProfiles;
    private readonly IRuntimeActorCombatProfileCompositionService _composition;

    public RuntimeActorEquipmentApplicationService(
        IRuntimeActorCombatProfileCompositionService composition,
        IRuntimeEquipmentProfileResolver? equipmentProfiles = null)
    {
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        _equipmentProfiles = equipmentProfiles ?? new RuntimeEquipmentProfileResolver();
    }

    public RuntimeActorEquipmentApplicationResult Apply(
        RuntimeActorEquipmentApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeActorSnapshot before = request.Actor.ToSnapshot();

        RuntimeActorEquipmentApplicationDiagnostic? assignmentDiagnostic =
            ValidateAssignmentEvidence(request);
        if (assignmentDiagnostic is not null)
        {
            return new RuntimeActorEquipmentApplicationResult(
                before,
                before,
                RuntimeEquipmentProfile.Empty,
                composition: null,
                [assignmentDiagnostic]);
        }

        RuntimeEquipmentProfile equipmentProfile;
        try
        {
            equipmentProfile = _equipmentProfiles.Resolve(
                request.Inventory,
                request.Equipment,
                request.EquipmentRepository) ??
                throw new InvalidOperationException(
                    "The equipment-profile resolver returned no profile.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Rejected(
                before,
                RuntimeEquipmentProfile.Empty,
                RuntimeActorEquipmentApplicationDiagnosticCode.EquipmentProfileResolutionFailed,
                $"Equipment profile resolution failed: {exception.Message}");
        }

        if (equipmentProfile.Diagnostics.Count > 0)
        {
            return new RuntimeActorEquipmentApplicationResult(
                before,
                before,
                equipmentProfile,
                composition: null,
                equipmentProfile.Diagnostics.Select(diagnostic =>
                    new RuntimeActorEquipmentApplicationDiagnostic(
                        RuntimeActorEquipmentApplicationDiagnosticCode.EquipmentProfileRejected,
                        diagnostic.Message,
                        EquipmentProfileCode: diagnostic.Code)));
        }

        RuntimeActorState stagedActor = request.Actor.CreateExecutionClone();
        stagedActor.ReplaceEquipmentForComposition(request.Equipment);
        RuntimeActorState[] stagedRuntimeActors = request.RuntimeActors
            .Select(runtimeActor => ReferenceEquals(runtimeActor, request.Actor)
                ? stagedActor
                : runtimeActor)
            .ToArray();

        RuntimeActorCombatProfileCompositionResult composition;
        try
        {
            composition = _composition.Compose(
                new RuntimeActorCombatProfileCompositionRequest(
                    stagedActor,
                    request.SourceKind,
                    request.MissingHostedEntityBehavior,
                    request.PartyRoster,
                    stagedRuntimeActors,
                    equipmentProfile.StatModifiers,
                    equipmentProfile.GrantedSkillIds)) ??
                throw new InvalidOperationException(
                    "The actor-composition service returned no result.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Rejected(
                before,
                equipmentProfile,
                RuntimeActorEquipmentApplicationDiagnosticCode.CombatProfileCompositionRejected,
                $"Actor combat-profile composition failed: {exception.Message}");
        }

        if (!composition.Applied)
        {
            return new RuntimeActorEquipmentApplicationResult(
                before,
                before,
                equipmentProfile,
                composition,
                composition.Diagnostics.Select(diagnostic =>
                    new RuntimeActorEquipmentApplicationDiagnostic(
                        RuntimeActorEquipmentApplicationDiagnosticCode.CombatProfileCompositionRejected,
                        diagnostic.Message,
                        CompositionCode: diagnostic.Code)));
        }

        try
        {
            request.Actor.ApplyExecutionStateFrom(stagedActor);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Rejected(
                before,
                equipmentProfile,
                RuntimeActorEquipmentApplicationDiagnosticCode.CommitFailed,
                $"Composed equipment state could not be committed: {exception.Message}",
                composition);
        }

        return new RuntimeActorEquipmentApplicationResult(
            before,
            request.Actor.ToSnapshot(),
            equipmentProfile,
            composition);
    }

    private static RuntimeActorEquipmentApplicationDiagnostic? ValidateAssignmentEvidence(
        RuntimeActorEquipmentApplicationRequest request)
    {
        int subjectOccurrences = request.RuntimeActors.Count(actor =>
            ReferenceEquals(actor, request.Actor));
        if (subjectOccurrences != 1)
        {
            return new RuntimeActorEquipmentApplicationDiagnostic(
                RuntimeActorEquipmentApplicationDiagnosticCode.RuntimeActorEvidenceRejected,
                "Equipment application requires the complete current runtime-actor map, " +
                "including the exact actor being changed once.")
            {
                ActorInstanceId = request.Actor.InstanceId
            };
        }

        IGrouping<RuntimeInstanceId, RuntimeActorState>? duplicateActor = request.RuntimeActors
            .GroupBy(actor => actor.InstanceId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateActor is not null)
        {
            return new RuntimeActorEquipmentApplicationDiagnostic(
                RuntimeActorEquipmentApplicationDiagnosticCode.RuntimeActorEvidenceRejected,
                $"Runtime actor evidence contains duplicate instance ID '{duplicateActor.Key}'.")
            {
                ActorInstanceId = duplicateActor.Key
            };
        }

        if (request.PartyRoster is not null)
        {
            var rosterReferences = new List<RuntimeActorReferenceSnapshot>
            {
                request.PartyRoster.Owner
            };
            rosterReferences.AddRange(request.PartyRoster.ActiveParty);
            rosterReferences.AddRange(request.PartyRoster.ReserveMembers);
            if (request.PartyRoster.ActiveHostedEntity is not null)
            {
                rosterReferences.Add(request.PartyRoster.ActiveHostedEntity);
            }
            rosterReferences.AddRange(request.PartyRoster.HostedEntityRoster);
            rosterReferences.AddRange(request.PartyRoster.CompanionRoster);
            foreach (RuntimeActorReferenceSnapshot rosterReference in rosterReferences
                         .GroupBy(reference => reference.InstanceId)
                         .Select(group => group.First()))
            {
                RuntimeActorState? runtimeActor = request.RuntimeActors.SingleOrDefault(actor =>
                    actor.InstanceId == rosterReference.InstanceId);
                if (runtimeActor is null || runtimeActor.EntityId != rosterReference.EntityDefinitionId)
                {
                    return new RuntimeActorEquipmentApplicationDiagnostic(
                        RuntimeActorEquipmentApplicationDiagnosticCode.RuntimeActorEvidenceRejected,
                        $"Runtime actor evidence does not contain roster actor " +
                        $"'{rosterReference.InstanceId}' with entity " +
                        $"'{rosterReference.EntityDefinitionId}'.")
                    {
                        ActorInstanceId = rosterReference.InstanceId
                    };
                }
            }
        }

        HashSet<RuntimeInstanceId> candidateInstanceIds = request.Equipment
            .EquippedInstanceIds
            .Values
            .ToHashSet();
        foreach (RuntimeActorState otherActor in request.RuntimeActors.Where(actor =>
                     !ReferenceEquals(actor, request.Actor)))
        {
            RuntimeInstanceId conflictingInstanceId = otherActor.Equipment
                .EquippedInstanceIds
                .Values
                .FirstOrDefault(candidateInstanceIds.Contains);
            if (conflictingInstanceId.IsValid)
            {
                return new RuntimeActorEquipmentApplicationDiagnostic(
                    RuntimeActorEquipmentApplicationDiagnosticCode.EquipmentAssignedToAnotherActor,
                    $"Equipment instance '{conflictingInstanceId}' is already assigned to actor " +
                    $"'{otherActor.InstanceId}'.")
                {
                    EquipmentInstanceId = conflictingInstanceId,
                    ActorInstanceId = otherActor.InstanceId
                };
            }
        }

        return null;
    }

    private static RuntimeActorEquipmentApplicationResult Rejected(
        RuntimeActorSnapshot before,
        RuntimeEquipmentProfile equipmentProfile,
        RuntimeActorEquipmentApplicationDiagnosticCode code,
        string message,
        RuntimeActorCombatProfileCompositionResult? composition = null) =>
        new(
            before,
            before,
            equipmentProfile,
            composition,
            [new RuntimeActorEquipmentApplicationDiagnostic(code, message)]);
}

public sealed class RuntimeActorCombatProfileCompositionService :
    IRuntimeActorCombatProfileCompositionService
{
    private readonly IStatResolutionPolicy _statResolution;
    private readonly IResourceGrowthPolicy _resourceGrowth;
    private readonly ISkillDefinitionRepository _skills;
    private readonly IRosterCapacityPolicy _rosterCapacityPolicy;

    public RuntimeActorCombatProfileCompositionService(ISkillDefinitionRepository skills)
        : this(
            new StandardStatResolutionPolicy(),
            new StandardResourceGrowthPolicy(),
            skills,
            NoLimitRosterCapacityPolicy.Instance)
    {
    }

    public RuntimeActorCombatProfileCompositionService(
        IStatResolutionPolicy statResolution,
        IResourceGrowthPolicy resourceGrowth,
        ISkillDefinitionRepository skills)
        : this(
            statResolution,
            resourceGrowth,
            skills,
            NoLimitRosterCapacityPolicy.Instance)
    {
    }

    public RuntimeActorCombatProfileCompositionService(
        IStatResolutionPolicy statResolution,
        IResourceGrowthPolicy resourceGrowth,
        ISkillDefinitionRepository skills,
        IRosterCapacityPolicy rosterCapacityPolicy)
    {
        _statResolution = statResolution ?? throw new ArgumentNullException(nameof(statResolution));
        _resourceGrowth = resourceGrowth ?? throw new ArgumentNullException(nameof(resourceGrowth));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _rosterCapacityPolicy = rosterCapacityPolicy ??
            throw new ArgumentNullException(nameof(rosterCapacityPolicy));
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
            RuntimeActorState? rosterOwnerState = request.RuntimeActors
                .FirstOrDefault(candidate =>
                    candidate.InstanceId == partyRoster.Owner.InstanceId);
            RuntimeActorSnapshot? rosterOwner = before.Identity.InstanceId ==
                partyRoster.Owner.InstanceId
                ? before
                : rosterOwnerState?.ToSnapshot();
            IReadOnlyList<RuntimePartyRosterInvariantDiagnostic> rosterDiagnostics =
                RuntimePartyRosterInvariantRules.Validate(
                    partyRoster,
                    rosterOwner,
                    _rosterCapacityPolicy);
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

        ContentId[] equipmentCombatStatIds =
        [
            StandardProgressionIds.Defense,
            StandardProgressionIds.Evasion
        ];
        IReadOnlyDictionary<ContentId, decimal> combatStatSource =
            resolvedSource == RuntimeStatSourceKind.ActiveHostedEntity
                ? hostedStats
                : actor.BaseStats;
        ContentId[] equipmentCombatStatsToResolve = equipmentCombatStatIds
            .Where(statId =>
                combatStatSource.ContainsKey(statId) ||
                request.EquipmentStatModifiers.ContainsKey(statId))
            .ToArray();
        var resolutions = new List<StatResolutionResult>(
            StandardProgressionIds.CoreStats.Count + equipmentCombatStatsToResolve.Length);
        HashSet<ContentId> composedStatIds =
        [
            .. StandardProgressionIds.CoreStats,
            .. equipmentCombatStatIds
        ];
        var effectiveStats = before.Stats.EffectiveStats
            .Where(pair => !composedStatIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (ContentId statId in StandardProgressionIds.CoreStats.Concat(equipmentCombatStatsToResolve))
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

        if (request.EquipmentGrantedSkillIds.Any(skillId => !skillId.IsValid) ||
            request.EquipmentGrantedSkillIds.Distinct().Count() !=
            request.EquipmentGrantedSkillIds.Count)
        {
            return Rejected(
                before,
                resolvedSource,
                sourceActor.InstanceId,
                RuntimeActorCombatProfileCompositionDiagnosticCode.InvalidSkillState,
                "Equipment-granted skill IDs must be valid and unique.");
        }

        var resolvedEquipmentGrantedSkills =
            new List<SkillDefinition>(request.EquipmentGrantedSkillIds.Count);
        foreach (ContentId skillId in request.EquipmentGrantedSkillIds)
        {
            if (!_skills.TryGetSkill(skillId, out SkillDefinition? skill) || skill is null)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    sourceActor.InstanceId,
                    RuntimeActorCombatProfileCompositionDiagnosticCode.SkillDefinitionMissing,
                    $"Equipment profile references missing granted skill '{skillId}'.",
                    skillId: skillId);
            }

            resolvedEquipmentGrantedSkills.Add(skill);
        }

        RuntimeSkillStateSnapshot composedSkills = sourceActor.InstanceId == actor.InstanceId
            ? sourceSkills
            : new RuntimeSkillStateSnapshot(
                sourceSkills.LearnedSkillIds,
                sourceSkills.EquippedSkillIds,
                revision: sourceSkills.Revision);

        try
        {
            actor.ApplyCombatProfile(
                effectiveStats,
                resources.Resources,
                sourceActor.DefenseProfile,
                composedSkills,
                resolvedSkills,
                sourceActor.InstanceId,
                sourceActor.EntityId,
                resolvedEquipmentGrantedSkills);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
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
            resolutions,
            sourceEntityId: sourceActor.EntityId);
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

        var pendingTokens = new HashSet<RuntimeSkillChoiceToken>();
        var pendingSkills = new HashSet<ContentId>();
        for (int index = 0; index < skills.PendingChoices.Count; index++)
        {
            RuntimePendingSkillChoiceSnapshot choice = skills.PendingChoices[index];
            string? message = !choice.Token.IsValid
                ? $"Pending choice at index {index} has an invalid token."
                : !choice.SkillId.IsValid
                    ? $"Pending choice at index {index} has an empty skill ID."
                    : !pendingTokens.Add(choice.Token)
                        ? $"Pending choice token '{choice.Token}' appears more than once."
                        : !pendingSkills.Add(choice.SkillId)
                            ? $"Pending skill '{choice.SkillId}' appears more than once."
                            : learned.Contains(choice.SkillId)
                                ? $"Pending skill '{choice.SkillId}' is already learned."
                                : null;
            if (message is not null)
            {
                diagnostics.Add(new RuntimeActorCombatProfileCompositionDiagnostic(
                    RuntimeActorCombatProfileCompositionDiagnosticCode.InvalidSkillState,
                    message,
                    InstanceId: sourceActorId,
                    SkillId: choice.SkillId));
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
            ],
            sourceEntityId: before.Identity.EntityDefinitionId);
}
