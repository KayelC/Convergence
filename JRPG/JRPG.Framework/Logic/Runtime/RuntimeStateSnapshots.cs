using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using JRPGPrototype.Data.Definitions;

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
    ResourceValueOutOfRange
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
    public RuntimeTimedStateSnapshot(ContentId id, int? remainingTurns = null, bool isRemovable = true)
    {
        RuntimeSnapshotCollections.ValidateOptionalPositiveTurns(remainingTurns, nameof(remainingTurns));
        Id = id;
        RemainingTurns = remainingTurns;
        IsRemovable = isRemovable;
    }

    public ContentId Id { get; }
    public int? RemainingTurns { get; }
    public bool IsRemovable { get; }
}

public sealed record RuntimeStatStageSnapshot
{
    public RuntimeStatStageSnapshot(ContentId modifierTrackId, int stage, int? remainingTurns = null)
    {
        RuntimeSnapshotCollections.ValidateOptionalPositiveTurns(remainingTurns, nameof(remainingTurns));
        ModifierTrackId = modifierTrackId;
        Stage = stage;
        RemainingTurns = remainingTurns;
    }

    public ContentId ModifierTrackId { get; }
    public int Stage { get; }
    public int? RemainingTurns { get; }
}

public sealed record RuntimeChargeSnapshot
{
    public RuntimeChargeSnapshot(ChargeKind kind, decimal multiplier, int? remainingTurns = null)
    {
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Charge multiplier must be positive.");
        }

        RuntimeSnapshotCollections.ValidateOptionalPositiveTurns(remainingTurns, nameof(remainingTurns));
        Kind = kind;
        Multiplier = multiplier;
        RemainingTurns = remainingTurns;
    }

    public ChargeKind Kind { get; }
    public decimal Multiplier { get; }
    public int? RemainingTurns { get; }
}

public sealed record RuntimeShieldSnapshot
{
    public RuntimeShieldSnapshot(ShieldKind kind, int? remainingTurns = null)
    {
        RuntimeSnapshotCollections.ValidateOptionalPositiveTurns(remainingTurns, nameof(remainingTurns));
        Kind = kind;
        RemainingTurns = remainingTurns;
    }

    public ShieldKind Kind { get; }
    public int? RemainingTurns { get; }
}

public sealed record RuntimeBreakSnapshot
{
    public RuntimeBreakSnapshot(DamageElement element, int? remainingTurns = null)
    {
        RuntimeSnapshotCollections.ValidateOptionalPositiveTurns(remainingTurns, nameof(remainingTurns));
        Element = element;
        RemainingTurns = remainingTurns;
    }

    public DamageElement Element { get; }
    public int? RemainingTurns { get; }
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
        IEnumerable<RuntimeBreakSnapshot>? breaks = null,
        bool isGuarding = false,
        IEnumerable<RuntimeAnalysisSnapshot>? analysis = null)
    {
        Ailments = RuntimeSnapshotCollections.List(ailments);
        Statuses = RuntimeSnapshotCollections.List(statuses);
        StatStages = RuntimeSnapshotCollections.List(statStages);
        Charges = RuntimeSnapshotCollections.List(charges);
        Shields = RuntimeSnapshotCollections.List(shields);
        Breaks = RuntimeSnapshotCollections.List(breaks);
        IsGuarding = isGuarding;
        Analysis = RuntimeSnapshotCollections.List(analysis);
    }

    public IReadOnlyList<RuntimeTimedStateSnapshot> Ailments { get; }
    public IReadOnlyList<RuntimeTimedStateSnapshot> Statuses { get; }
    public IReadOnlyList<RuntimeStatStageSnapshot> StatStages { get; }
    public IReadOnlyList<RuntimeChargeSnapshot> Charges { get; }
    public IReadOnlyList<RuntimeShieldSnapshot> Shields { get; }
    public IReadOnlyList<RuntimeBreakSnapshot> Breaks { get; }
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

public sealed record RuntimeBattleActivationSnapshot
{
    public RuntimeBattleActivationSnapshot(IEnumerable<RuntimePassiveActivationSnapshot>? passiveActivations = null)
    {
        PassiveActivations = RuntimeSnapshotCollections.List(passiveActivations);
    }

    public IReadOnlyList<RuntimePassiveActivationSnapshot> PassiveActivations { get; }
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
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null)
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
            BaseResourceValues);
}

public sealed class RuntimeActorStateSet
{
    private RuntimeActorSnapshot _snapshot;

    private RuntimeActorStateSet(RuntimeActorSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public RuntimeInstanceId InstanceId => _snapshot.Identity.InstanceId;

    public static RuntimeActorStateSet FromSnapshot(RuntimeActorSnapshot snapshot) => new(snapshot);

    public RuntimeActorSnapshot ToSnapshot() => _snapshot;

    internal void ReplaceSnapshot(RuntimeActorSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }
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
    public RuntimeMutationResult AddResource(RuntimeActorStateSet actor, ContentId resourceId, decimal delta)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RuntimeActorSnapshot before = actor.ToSnapshot();
        RuntimeResourceSnapshot? resource = before.Resources.FirstOrDefault(candidate => candidate.ResourceId == resourceId);
        return resource is null
            ? Rejected(before, RuntimeMutationErrorCode.MissingResource, $"Actor '{before.Identity.InstanceId}' has no resource '{resourceId}'.", "$.resources")
            : SetResource(actor, resourceId, resource.Current + delta);
    }

    public RuntimeMutationResult SetResource(RuntimeActorStateSet actor, ContentId resourceId, decimal current)
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
        RuntimeActorSnapshot after = before.WithResources(resources);
        actor.ReplaceSnapshot(after);
        return new RuntimeMutationResult(RuntimeMutationStatus.Applied, before, after);
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
