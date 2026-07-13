using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Execution;

namespace JRPGPrototype.Logic.Runtime;

public readonly record struct RuntimeInstanceId
{
    public RuntimeInstanceId(string value)
    {
        Value = Normalize(value);
    }

    public string Value { get; }

    public static RuntimeInstanceId Parse(string value) => new(value);

    public static bool TryParse(string? value, out RuntimeInstanceId instanceId)
    {
        if (value is null)
        {
            instanceId = default;
            return false;
        }

        try
        {
            instanceId = new RuntimeInstanceId(value);
            return true;
        }
        catch (ArgumentException)
        {
            instanceId = default;
            return false;
        }
    }

    public override string ToString() => Value ?? string.Empty;

    private static string Normalize([NotNull] string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Runtime instance ID cannot be empty.", nameof(value));
        }

        string normalized = value.Trim().ToLowerInvariant();
        foreach (char character in normalized)
        {
            bool valid = character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_'
                or '-'
                or '.'
                or ':';
            if (!valid)
            {
                throw new ArgumentException("Runtime instance ID contains an invalid character.", nameof(value));
            }
        }

        return normalized;
    }
}

public enum RuntimeActorDeployment
{
    Active,
    Reserve,
    Deployed
}

public enum RuntimeMutationStatus
{
    Applied,
    Rejected
}

public enum RuntimeMutationErrorCode
{
    MissingResource,
    ResourceValueOutOfRange,
    ProgressionMutationRejected
}

public sealed record RuntimeActorIdentitySnapshot
{
    public RuntimeActorIdentitySnapshot(
        RuntimeInstanceId instanceId,
        ContentId entityDefinitionId,
        ContentId actorKindId,
        string displayName,
        string? displaySubtitle = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        InstanceId = instanceId;
        EntityDefinitionId = entityDefinitionId;
        ActorKindId = actorKindId;
        DisplayName = displayName;
        DisplaySubtitle = displaySubtitle;
    }

    public RuntimeInstanceId InstanceId { get; }
    public ContentId EntityDefinitionId { get; }
    public ContentId ActorKindId { get; }
    public string DisplayName { get; }
    public string? DisplaySubtitle { get; }
}

public sealed record RuntimeActorOwnershipSnapshot(
    ContentId ControllerId,
    ContentId TeamId,
    RuntimeInstanceId? OwnerInstanceId = null);

public sealed record RuntimeActorDeploymentSnapshot(
    RuntimeActorDeployment Deployment,
    bool IsActive,
    bool HasSwappedThisTurn = false);

public sealed record RuntimeProgressionSnapshot
{
    public RuntimeProgressionSnapshot(
        int level,
        long experience,
        long lifetimeExperience,
        int unspentStatPoints)
    {
        if (level <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be positive.");
        }
        if (experience < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(experience), "Experience cannot be negative.");
        }
        if (lifetimeExperience < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetimeExperience), "Lifetime experience cannot be negative.");
        }
        if (unspentStatPoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unspentStatPoints), "Unspent stat points cannot be negative.");
        }

        Level = level;
        Experience = experience;
        LifetimeExperience = lifetimeExperience;
        UnspentStatPoints = unspentStatPoints;
    }

    public int Level { get; }
    public long Experience { get; }
    public long LifetimeExperience { get; }
    public int UnspentStatPoints { get; }
}

public sealed record RuntimeResourceSnapshot
{
    public RuntimeResourceSnapshot(ContentId resourceId, decimal current, decimal maximum)
    {
        if (maximum < 0 || current < 0 || current > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(current), "Resource values must satisfy 0 <= current <= maximum.");
        }

        ResourceId = resourceId;
        Current = current;
        Maximum = maximum;
    }

    public ContentId ResourceId { get; }
    public decimal Current { get; }
    public decimal Maximum { get; }
}

public sealed record RuntimeStatBlockSnapshot
{
    public RuntimeStatBlockSnapshot(
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseStats = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? effectiveStats = null)
    {
        BaseStats = RuntimeSnapshotCollections.Dictionary(baseStats);
        EffectiveStats = RuntimeSnapshotCollections.Dictionary(effectiveStats);
    }

    public IReadOnlyDictionary<ContentId, decimal> BaseStats { get; }
    public IReadOnlyDictionary<ContentId, decimal> EffectiveStats { get; }
}

public sealed record RuntimeSkillStateSnapshot
{
    public RuntimeSkillStateSnapshot(
        IEnumerable<ContentId>? learnedSkillIds = null,
        IEnumerable<ContentId>? equippedSkillIds = null)
    {
        LearnedSkillIds = RuntimeSnapshotCollections.List(learnedSkillIds);
        EquippedSkillIds = RuntimeSnapshotCollections.List(equippedSkillIds);
    }

    public IReadOnlyList<ContentId> LearnedSkillIds { get; }
    public IReadOnlyList<ContentId> EquippedSkillIds { get; }
}

public sealed record RuntimeActorReferenceSnapshot
{
    public RuntimeActorReferenceSnapshot(
        RuntimeInstanceId instanceId,
        ContentId entityDefinitionId,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        InstanceId = instanceId;
        EntityDefinitionId = entityDefinitionId;
        DisplayName = displayName;
    }

    public RuntimeInstanceId InstanceId { get; }
    public ContentId EntityDefinitionId { get; }
    public string DisplayName { get; }
}

public sealed record RuntimeFormStockSnapshot
{
    public RuntimeFormStockSnapshot(
        RuntimeActorReferenceSnapshot? activeForm = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? personaStock = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? demonStock = null)
    {
        ActiveForm = activeForm;
        PersonaStock = RuntimeSnapshotCollections.List(personaStock);
        DemonStock = RuntimeSnapshotCollections.List(demonStock);
    }

    public RuntimeActorReferenceSnapshot? ActiveForm { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> PersonaStock { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> DemonStock { get; }
}

public sealed record RuntimeEquipmentSnapshot
{
    public RuntimeEquipmentSnapshot(IEnumerable<KeyValuePair<EquipmentSlot, ContentId>>? equippedItemIds = null)
    {
        EquippedItemIds = RuntimeSnapshotCollections.Dictionary(equippedItemIds);
    }

    public IReadOnlyDictionary<EquipmentSlot, ContentId> EquippedItemIds { get; }
}

public sealed record RuntimeTimedStateSnapshot
{
    public RuntimeTimedStateSnapshot(
        ContentId id,
        DurationDefinition duration,
        bool isRemovable = true)
    {
        Id = id;
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));
        IsRemovable = isRemovable;
    }

    public ContentId Id { get; }
    public DurationDefinition Duration { get; }
    public bool IsRemovable { get; }
}

public sealed record RuntimeStatStageSnapshot
{
    public RuntimeStatStageSnapshot(
        ContentId modifierTrackId,
        int stage,
        DurationDefinition? duration = null)
    {
        ModifierTrackId = modifierTrackId;
        Stage = stage;
        Duration = duration;
    }

    public ContentId ModifierTrackId { get; }
    public int Stage { get; }
    public DurationDefinition? Duration { get; }
}

public sealed record RuntimeChargeSnapshot
{
    public RuntimeChargeSnapshot(
        ChargeKind kind,
        decimal multiplier,
        DurationDefinition? duration = null)
    {
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Charge multiplier must be positive.");
        }

        Kind = kind;
        Multiplier = multiplier;
        Duration = duration;
    }

    public ChargeKind Kind { get; }
    public decimal Multiplier { get; }
    public DurationDefinition? Duration { get; }
}

public sealed record RuntimeShieldSnapshot
{
    public RuntimeShieldSnapshot(ShieldKind kind, DurationDefinition? duration = null)
    {
        Kind = kind;
        Duration = duration;
    }

    public ShieldKind Kind { get; }
    public DurationDefinition? Duration { get; }
}

public sealed record RuntimeAffinityOverrideSnapshot
{
    public RuntimeAffinityOverrideSnapshot(
        DamageElement element,
        ElementalAffinity affinity,
        DurationDefinition duration)
    {
        Element = element;
        Affinity = affinity;
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));
    }

    public DamageElement Element { get; }
    public ElementalAffinity Affinity { get; }
    public DurationDefinition Duration { get; }
}

public sealed record RuntimeAffinityBreakSnapshot
{
    public RuntimeAffinityBreakSnapshot(DamageElement element, DurationDefinition duration)
    {
        Element = element;
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));
    }

    public DamageElement Element { get; }
    public DurationDefinition Duration { get; }
}

public sealed record RuntimeAnalysisSnapshot
{
    public RuntimeAnalysisSnapshot(RuntimeInstanceId targetInstanceId, IEnumerable<AnalysisLayer> layers)
    {
        TargetInstanceId = targetInstanceId;
        Layers = RuntimeSnapshotCollections.List(layers);
    }

    public RuntimeInstanceId TargetInstanceId { get; }
    public IReadOnlyList<AnalysisLayer> Layers { get; }
}

public sealed record RuntimeBattleStatusSnapshot
{
    public RuntimeBattleStatusSnapshot(
        IEnumerable<RuntimeTimedStateSnapshot>? ailments = null,
        IEnumerable<RuntimeTimedStateSnapshot>? statuses = null,
        IEnumerable<RuntimeStatStageSnapshot>? statStages = null,
        IEnumerable<RuntimeChargeSnapshot>? charges = null,
        IEnumerable<RuntimeShieldSnapshot>? shields = null,
        IEnumerable<RuntimeAffinityOverrideSnapshot>? affinityOverrides = null,
        bool isGuarding = false,
        IEnumerable<RuntimeAnalysisSnapshot>? analysis = null,
        IEnumerable<RuntimeAffinityBreakSnapshot>? affinityBreaks = null)
    {
        Ailments = RuntimeSnapshotCollections.List(ailments);
        Statuses = RuntimeSnapshotCollections.List(statuses);
        StatStages = RuntimeSnapshotCollections.List(statStages);
        Charges = RuntimeSnapshotCollections.List(charges);
        Shields = RuntimeSnapshotCollections.List(shields);
        AffinityOverrides = RuntimeSnapshotCollections.List(affinityOverrides);
        AffinityBreaks = RuntimeSnapshotCollections.List(affinityBreaks);
        IsGuarding = isGuarding;
        Analysis = RuntimeSnapshotCollections.List(analysis);
    }

    public IReadOnlyList<RuntimeTimedStateSnapshot> Ailments { get; }
    public IReadOnlyList<RuntimeTimedStateSnapshot> Statuses { get; }
    public IReadOnlyList<RuntimeStatStageSnapshot> StatStages { get; }
    public IReadOnlyList<RuntimeChargeSnapshot> Charges { get; }
    public IReadOnlyList<RuntimeShieldSnapshot> Shields { get; }
    public IReadOnlyList<RuntimeAffinityOverrideSnapshot> AffinityOverrides { get; }
    public IReadOnlyList<RuntimeAffinityBreakSnapshot> AffinityBreaks { get; }
    public bool IsGuarding { get; }
    public IReadOnlyList<RuntimeAnalysisSnapshot> Analysis { get; }
}

public sealed record RuntimePassiveActivationSnapshot
{
    public RuntimePassiveActivationSnapshot(ContentId skillId, ContentId eventId, int triggerIndex, int activationCount)
    {
        if (triggerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(triggerIndex), "Trigger index cannot be negative.");
        }
        if (activationCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activationCount), "Activation count cannot be negative.");
        }

        SkillId = skillId;
        EventId = eventId;
        TriggerIndex = triggerIndex;
        ActivationCount = activationCount;
    }

    public ContentId SkillId { get; }
    public ContentId EventId { get; }
    public int TriggerIndex { get; }
    public int ActivationCount { get; }
}

public sealed record RuntimePassiveSkillStateSnapshot(ContentId SkillId, bool IsEnabled);

public sealed record RuntimeBattleActivationSnapshot
{
    public RuntimeBattleActivationSnapshot(
        IEnumerable<RuntimePassiveActivationSnapshot>? passiveActivations = null,
        IEnumerable<RuntimePassiveSkillStateSnapshot>? passiveSkillStates = null)
    {
        PassiveActivations = RuntimeSnapshotCollections.List(passiveActivations);
        PassiveSkillStates = RuntimeSnapshotCollections.List(passiveSkillStates);
    }

    public IReadOnlyList<RuntimePassiveActivationSnapshot> PassiveActivations { get; }
    public IReadOnlyList<RuntimePassiveSkillStateSnapshot> PassiveSkillStates { get; }
}

public sealed record RuntimeActorSnapshot
{
    public RuntimeActorSnapshot(
        RuntimeActorIdentitySnapshot identity,
        RuntimeActorOwnershipSnapshot ownership,
        RuntimeActorDeploymentSnapshot deployment,
        RuntimeProgressionSnapshot progression,
        IEnumerable<RuntimeResourceSnapshot> resources,
        RuntimeStatBlockSnapshot stats,
        RuntimeSkillStateSnapshot skills,
        RuntimeFormStockSnapshot forms,
        RuntimeEquipmentSnapshot equipment,
        RuntimeBattleStatusSnapshot battleStatus,
        RuntimeBattleActivationSnapshot battleActivations,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues,
        ContentId vitalResourceId,
        IEnumerable<ContentId>? capabilityIds = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        Deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Resources = RuntimeSnapshotCollections.List(resources);
        BaseResourceValues = RuntimeSnapshotCollections.Dictionary(baseResourceValues);
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Skills = skills ?? throw new ArgumentNullException(nameof(skills));
        Forms = forms ?? throw new ArgumentNullException(nameof(forms));
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        BattleStatus = battleStatus ?? throw new ArgumentNullException(nameof(battleStatus));
        BattleActivations = battleActivations ?? throw new ArgumentNullException(nameof(battleActivations));
        CapabilityIds = RuntimeSnapshotCollections.List(capabilityIds);
        VitalResourceId = vitalResourceId;
        if (!Resources.Any(resource => resource.ResourceId == vitalResourceId))
        {
            throw new ArgumentException("The vital resource must exist in the actor resources.", nameof(vitalResourceId));
        }
    }

    public RuntimeActorIdentitySnapshot Identity { get; }
    public RuntimeActorOwnershipSnapshot Ownership { get; }
    public RuntimeActorDeploymentSnapshot Deployment { get; }
    public RuntimeProgressionSnapshot Progression { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public RuntimeSkillStateSnapshot Skills { get; }
    public RuntimeFormStockSnapshot Forms { get; }
    public RuntimeEquipmentSnapshot Equipment { get; }
    public RuntimeBattleStatusSnapshot BattleStatus { get; }
    public RuntimeBattleActivationSnapshot BattleActivations { get; }
    public IReadOnlyList<ContentId> CapabilityIds { get; }
    public ContentId VitalResourceId { get; }

    public RuntimeActorSnapshot WithResources(IEnumerable<RuntimeResourceSnapshot> resources) =>
        new(
            Identity,
            Ownership,
            Deployment,
            Progression,
            resources,
            Stats,
            Skills,
            Forms,
            Equipment,
            BattleStatus,
            BattleActivations,
            BaseResourceValues,
            VitalResourceId,
            CapabilityIds);

    public RuntimeActorSnapshot WithProgression(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null) =>
        new(
            Identity,
            Ownership,
            Deployment,
            progression,
            resources ?? Resources,
            stats,
            Skills,
            Forms,
            Equipment,
            BattleStatus,
            BattleActivations,
            baseResourceValues ?? BaseResourceValues,
            VitalResourceId,
            CapabilityIds);
}

public sealed record RuntimeMutationDiagnostic(
    RuntimeMutationErrorCode Code,
    string Message,
    string? Path = null);

public sealed record RuntimeMutationResult
{
    public RuntimeMutationResult(
        RuntimeMutationStatus status,
        RuntimeActorSnapshot before,
        RuntimeActorSnapshot after,
        IEnumerable<RuntimeMutationDiagnostic>? diagnostics = null)
    {
        Status = status;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeMutationStatus Status { get; }
    public bool Applied => Status == RuntimeMutationStatus.Applied;
    public RuntimeActorSnapshot Before { get; }
    public RuntimeActorSnapshot After { get; }
    public IReadOnlyList<RuntimeMutationDiagnostic> Diagnostics { get; }
}

public sealed class RuntimeResourceTransactionService
{
    public RuntimeMutationResult AddResource(RuntimeActorState actor, ContentId resourceId, decimal delta)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RuntimeActorSnapshot before = actor.ToSnapshot();
        RuntimeResourceSnapshot? resource = before.Resources.FirstOrDefault(candidate => candidate.ResourceId == resourceId);
        return resource is null
            ? Rejected(before, RuntimeMutationErrorCode.MissingResource, $"Actor '{before.Identity.InstanceId}' has no resource '{resourceId}'.", "$.resources")
            : SetResource(actor, resourceId, resource.Current + delta);
    }

    public RuntimeMutationResult SetResource(RuntimeActorState actor, ContentId resourceId, decimal current)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RuntimeActorSnapshot before = actor.ToSnapshot();
        RuntimeResourceSnapshot[] resources = before.Resources.ToArray();
        int index = Array.FindIndex(resources, candidate => candidate.ResourceId == resourceId);
        if (index < 0)
        {
            return Rejected(
                before,
                RuntimeMutationErrorCode.MissingResource,
                $"Actor '{before.Identity.InstanceId}' has no resource '{resourceId}'.",
                "$.resources");
        }

        RuntimeResourceSnapshot existing = resources[index];
        if (current < 0 || current > existing.Maximum)
        {
            return Rejected(
                before,
                RuntimeMutationErrorCode.ResourceValueOutOfRange,
                $"Resource '{resourceId}' value must satisfy 0 <= current <= {existing.Maximum}.",
                $"$.resources[{index}].current");
        }

        resources[index] = new RuntimeResourceSnapshot(resourceId, current, existing.Maximum);
        actor.ReplaceResources(resources);
        RuntimeActorSnapshot after = actor.ToSnapshot();
        return new RuntimeMutationResult(RuntimeMutationStatus.Applied, before, after);
    }

    public RuntimeMutationResult ApplyRecalculation(
        RuntimeActorState actor,
        ResourceRecalculationResult recalculation)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(recalculation);

        RuntimeActorSnapshot before = actor.ToSnapshot();
        try
        {
            actor.ReplaceResources(recalculation.Resources);
        }
        catch (ArgumentException exception)
        {
            return Rejected(
                before,
                RuntimeMutationErrorCode.ResourceValueOutOfRange,
                exception.Message,
                "$.resources");
        }

        return new RuntimeMutationResult(
            RuntimeMutationStatus.Applied,
            before,
            actor.ToSnapshot());
    }

    private static RuntimeMutationResult Rejected(
        RuntimeActorSnapshot before,
        RuntimeMutationErrorCode code,
        string message,
        string path) =>
        new(
            RuntimeMutationStatus.Rejected,
            before,
            before,
            [new RuntimeMutationDiagnostic(code, message, path)]);
}

public sealed class RuntimeProgressionTransactionService
{
    public RuntimeMutationResult ApplyLevelGrowth(RuntimeActorState actor, LevelGrowthResult growth)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(growth);

        RuntimeActorSnapshot before = actor.ToSnapshot();
        if (!growth.Applied)
        {
            return new RuntimeMutationResult(
                RuntimeMutationStatus.Rejected,
                before,
                before,
                growth.Diagnostics.Select(diagnostic => new RuntimeMutationDiagnostic(
                    RuntimeMutationErrorCode.ProgressionMutationRejected,
                    diagnostic.Message,
                    "$.progression")));
        }

        actor.ApplyProgression(
            growth.Progression,
            growth.Stats,
            growth.Resources.Count > 0 ? growth.Resources : before.Resources,
            growth.BaseResourceValues.Count > 0 ? growth.BaseResourceValues : before.BaseResourceValues);
        RuntimeActorSnapshot after = actor.ToSnapshot();
        return new RuntimeMutationResult(RuntimeMutationStatus.Applied, before, after);
    }
}

internal static class RuntimeSnapshotCollections
{
    public static IReadOnlyList<T> List<T>(IEnumerable<T>? values = null) =>
        Array.AsReadOnly((values ?? []).ToArray());

    public static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values = null)
        where TKey : notnull
    {
        System.Collections.Generic.Dictionary<TKey, TValue> copy = [];
        foreach ((TKey key, TValue value) in values ?? [])
        {
            copy.Add(key, value);
        }

        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }

    public static void ValidateOptionalPositiveTurns(int? turns, string parameterName)
    {
        if (turns is <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Remaining turns must be positive when present.");
        }
    }
}
