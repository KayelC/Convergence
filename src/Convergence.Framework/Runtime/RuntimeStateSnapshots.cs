using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Internal;

namespace Convergence.Runtime;

public readonly record struct RuntimeInstanceId
{
    private readonly string? _value;

    public RuntimeInstanceId(string value)
    {
        _value = Normalize(value);
    }

    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

    public bool IsValid => !IsEmpty;

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

    public override string ToString() => Value;

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

public enum RuntimeMutationStatus
{
    Applied,
    Rejected
}

public enum RuntimeMutationErrorCode
{
    MissingResource,
    ResourceValueOutOfRange,
    ProgressionMutationRejected,
    ProgressionSourceStateChanged
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

/// <summary>
/// Identifies who may issue commands for an actor and which battle team the actor belongs to.
/// </summary>
/// <param name="CommandAuthorityId">
/// An opaque host-routing key. Framework rules preserve this value but never infer behavior from its text.
/// </param>
/// <param name="TeamId">
/// The affiliation interpreted by targeting, initiative, and encounter-completion rules.
/// </param>
public sealed record RuntimeActorAffiliationSnapshot(
    ContentId CommandAuthorityId,
    ContentId TeamId);

public sealed record RuntimeEncounterPresenceSnapshot(
    bool IsDeployed,
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
        if (!resourceId.IsValid)
        {
            throw new ArgumentException("Resource ID cannot be empty.", nameof(resourceId));
        }

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
        IEnumerable<ContentId>? equippedSkillIds = null,
        IEnumerable<RuntimePendingSkillChoiceSnapshot>? pendingChoices = null,
        long revision = 0)
    {
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                "Skill-state revision cannot be negative.");
        }

        LearnedSkillIds = RuntimeSnapshotCollections.List(learnedSkillIds);
        EquippedSkillIds = RuntimeSnapshotCollections.List(equippedSkillIds);
        PendingChoices = RuntimeSnapshotCollections.List(pendingChoices);
        Revision = revision;
    }

    public IReadOnlyList<ContentId> LearnedSkillIds { get; }
    public IReadOnlyList<ContentId> EquippedSkillIds { get; }
    public IReadOnlyList<RuntimePendingSkillChoiceSnapshot> PendingChoices { get; }
    public long Revision { get; }
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

public sealed record RuntimeEquipmentSnapshot
{
    public RuntimeEquipmentSnapshot(IEnumerable<KeyValuePair<EquipmentSlot, ContentId>>? equippedItemIds = null)
    {
        KeyValuePair<EquipmentSlot, ContentId>[] entries = equippedItemIds?.ToArray() ?? [];
        foreach ((EquipmentSlot slot, _) in entries)
        {
            EnumDomain.RequireDefined(slot, nameof(equippedItemIds));
        }

        EquippedItemIds = RuntimeSnapshotCollections.Dictionary(entries);
    }

    public IReadOnlyDictionary<EquipmentSlot, ContentId> EquippedItemIds { get; }
}

public sealed record RuntimeTimedStateSnapshot
{
    public RuntimeTimedStateSnapshot(
        ContentId id,
        StatusLifetimeDefinition lifetime)
    {
        Id = id;
        Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public ContentId Id { get; }
    public StatusLifetimeDefinition Lifetime { get; }
    public DurationDefinition Duration => Lifetime.Expiration;
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
        StatusLifetimeDefinition lifetime)
    {
        EnumDomain.RequireDefined(kind, nameof(kind));
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Charge multiplier must be positive.");
        }

        Kind = kind;
        Multiplier = multiplier;
        Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public ChargeKind Kind { get; }
    public decimal Multiplier { get; }
    public StatusLifetimeDefinition Lifetime { get; }
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record RuntimeChargeStateSnapshot
{
    public RuntimeChargeStateSnapshot(
        ContentId policyId,
        IEnumerable<RuntimeChargeSnapshot>? charges = null)
    {
        if (!policyId.IsValid)
        {
            throw new ArgumentException("Charge policy ID cannot be empty.", nameof(policyId));
        }

        PolicyId = policyId;
        IReadOnlyList<RuntimeChargeSnapshot> snapshot = RuntimeSnapshotCollections.List(charges);
        Charges = RuntimeSnapshotCollections.List(snapshot.OrderBy(charge => charge.Kind));
    }

    public ContentId PolicyId { get; }
    public IReadOnlyList<RuntimeChargeSnapshot> Charges { get; }
}

public sealed record RuntimeShieldSnapshot
{
    public RuntimeShieldSnapshot(ShieldKind kind, StatusLifetimeDefinition lifetime)
    {
        EnumDomain.RequireDefined(kind, nameof(kind));
        Kind = kind;
        Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public ShieldKind Kind { get; }
    public StatusLifetimeDefinition Lifetime { get; }
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record RuntimeAffinityOverrideSnapshot
{
    public RuntimeAffinityOverrideSnapshot(
        DamageElement element,
        ElementalAffinity affinity,
        StatusLifetimeDefinition lifetime)
    {
        EnumDomain.RequireDefined(element, nameof(element));
        EnumDomain.RequireDefined(affinity, nameof(affinity));
        Element = element;
        Affinity = affinity;
        Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public DamageElement Element { get; }
    public ElementalAffinity Affinity { get; }
    public StatusLifetimeDefinition Lifetime { get; }
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record RuntimeAffinityBreakSnapshot
{
    public RuntimeAffinityBreakSnapshot(DamageElement element, StatusLifetimeDefinition lifetime)
    {
        EnumDomain.RequireDefined(element, nameof(element));
        Element = element;
        Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public DamageElement Element { get; }
    public StatusLifetimeDefinition Lifetime { get; }
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record RuntimeBattleStatusSnapshot
{
    public RuntimeBattleStatusSnapshot(
        IEnumerable<RuntimeTimedStateSnapshot>? ailments = null,
        IEnumerable<RuntimeTimedStateSnapshot>? statuses = null,
        RuntimeStatModifierStateSnapshot? statModifiers = null,
        RuntimeChargeStateSnapshot? chargeState = null,
        IEnumerable<RuntimeShieldSnapshot>? shields = null,
        IEnumerable<RuntimeAffinityOverrideSnapshot>? affinityOverrides = null,
        bool isGuarding = false,
        IEnumerable<RuntimeAffinityBreakSnapshot>? affinityBreaks = null)
    {
        Ailments = RuntimeSnapshotCollections.List(ailments);
        Statuses = RuntimeSnapshotCollections.List(statuses);
        StatModifiers = statModifiers;
        ChargeState = chargeState;
        Shields = RuntimeSnapshotCollections.List(shields);
        AffinityOverrides = RuntimeSnapshotCollections.List(affinityOverrides);
        AffinityBreaks = RuntimeSnapshotCollections.List(affinityBreaks);
        IsGuarding = isGuarding;
    }

    public IReadOnlyList<RuntimeTimedStateSnapshot> Ailments { get; }
    public IReadOnlyList<RuntimeTimedStateSnapshot> Statuses { get; }
    public RuntimeStatModifierStateSnapshot? StatModifiers { get; }
    public RuntimeChargeStateSnapshot? ChargeState { get; }
    public IReadOnlyList<RuntimeChargeSnapshot> Charges =>
        ChargeState?.Charges ?? Array.Empty<RuntimeChargeSnapshot>();
    public IReadOnlyList<RuntimeShieldSnapshot> Shields { get; }
    public IReadOnlyList<RuntimeAffinityOverrideSnapshot> AffinityOverrides { get; }
    public IReadOnlyList<RuntimeAffinityBreakSnapshot> AffinityBreaks { get; }
    public bool IsGuarding { get; }
}

public sealed record RuntimePassiveActivationSnapshot
{
    public RuntimePassiveActivationSnapshot(
        ContentId skillId,
        ContentId eventId,
        int triggerIndex,
        int activationCount)
        : this(skillId, eventId, triggerIndex, activationCount, targetInstanceId: null)
    {
    }

    public RuntimePassiveActivationSnapshot(
        ContentId skillId,
        ContentId eventId,
        int triggerIndex,
        int activationCount,
        RuntimeInstanceId? targetInstanceId)
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
        TargetInstanceId = targetInstanceId;
    }

    public ContentId SkillId { get; }
    public ContentId EventId { get; }
    public int TriggerIndex { get; }
    public int ActivationCount { get; }
    public RuntimeInstanceId? TargetInstanceId { get; }
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
        RuntimeActorAffiliationSnapshot affiliation,
        RuntimeEncounterPresenceSnapshot encounterPresence,
        RuntimeProgressionSnapshot progression,
        IEnumerable<RuntimeResourceSnapshot> resources,
        RuntimeStatBlockSnapshot stats,
        RuntimeSkillStateSnapshot skills,
        RuntimeEquipmentSnapshot equipment,
        RuntimeBattleStatusSnapshot battleStatus,
        RuntimeBattleActivationSnapshot battleActivations,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues,
        ContentId vitalResourceId,
        IEnumerable<ContentId>? capabilityIds = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Affiliation = affiliation ?? throw new ArgumentNullException(nameof(affiliation));
        EncounterPresence = encounterPresence ?? throw new ArgumentNullException(nameof(encounterPresence));
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Resources = RuntimeSnapshotCollections.List(resources);
        BaseResourceValues = RuntimeSnapshotCollections.Dictionary(baseResourceValues);
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Skills = skills ?? throw new ArgumentNullException(nameof(skills));
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
    public RuntimeActorAffiliationSnapshot Affiliation { get; }
    public RuntimeEncounterPresenceSnapshot EncounterPresence { get; }
    public RuntimeProgressionSnapshot Progression { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public RuntimeSkillStateSnapshot Skills { get; }
    public RuntimeEquipmentSnapshot Equipment { get; }
    public RuntimeBattleStatusSnapshot BattleStatus { get; }
    public RuntimeBattleActivationSnapshot BattleActivations { get; }
    public IReadOnlyList<ContentId> CapabilityIds { get; }
    public ContentId VitalResourceId { get; }

    public RuntimeActorSnapshot WithResources(IEnumerable<RuntimeResourceSnapshot> resources) =>
        new(
            Identity,
            Affiliation,
            EncounterPresence,
            Progression,
            resources,
            Stats,
            Skills,
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
            Affiliation,
            EncounterPresence,
            progression,
            resources ?? Resources,
            stats,
            Skills,
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

        RuntimeResourceSnapshot resource = resources[index];
        if (!CombatArithmetic.TryAdd(resource.Current, delta, out decimal requested))
        {
            return Rejected(
                before,
                RuntimeMutationErrorCode.ResourceValueOutOfRange,
                $"Resource '{resourceId}' cannot represent current value {resource.Current} plus delta {delta}.",
                $"$.resources[{index}].current");
        }

        return SetResource(actor, resourceId, requested);
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

        if (!MatchesGrowthSource(before, growth.Source))
        {
            return new RuntimeMutationResult(
                RuntimeMutationStatus.Rejected,
                before,
                before,
                [
                    new RuntimeMutationDiagnostic(
                        RuntimeMutationErrorCode.ProgressionSourceStateChanged,
                        "Actor progression, stats, resources, or base-resource values changed " +
                        "after level growth was prepared.",
                        "$")
                ]);
        }

        actor.ApplyProgression(
            growth.Progression,
            growth.Stats,
            growth.Resources.Count > 0 ? growth.Resources : before.Resources,
            growth.BaseResourceValues.Count > 0 ? growth.BaseResourceValues : before.BaseResourceValues);
        RuntimeActorSnapshot after = actor.ToSnapshot();
        return new RuntimeMutationResult(RuntimeMutationStatus.Applied, before, after);
    }

    private static bool MatchesGrowthSource(
        RuntimeActorSnapshot actor,
        LevelGrowthSourceSnapshot source) =>
        actor.Progression == source.Progression &&
        DictionaryEqual(actor.Stats.BaseStats, source.Stats.BaseStats) &&
        DictionaryEqual(actor.Stats.EffectiveStats, source.Stats.EffectiveStats) &&
        ResourcesEqual(actor.Resources, source.Resources) &&
        DictionaryEqual(actor.BaseResourceValues, source.BaseResourceValues);

    private static bool ResourcesEqual(
        IReadOnlyList<RuntimeResourceSnapshot> first,
        IReadOnlyList<RuntimeResourceSnapshot> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }
        if (second.Select(resource => resource.ResourceId).Distinct().Count() != second.Count)
        {
            return false;
        }

        Dictionary<ContentId, RuntimeResourceSnapshot> secondById = second
            .ToDictionary(resource => resource.ResourceId);
        return first.All(resource =>
            secondById.TryGetValue(resource.ResourceId, out RuntimeResourceSnapshot? candidate) &&
            candidate.Current == resource.Current &&
            candidate.Maximum == resource.Maximum);
    }

    private static bool DictionaryEqual(
        IReadOnlyDictionary<ContentId, decimal> first,
        IReadOnlyDictionary<ContentId, decimal> second) =>
        first.Count == second.Count &&
        first.All(pair =>
            second.TryGetValue(pair.Key, out decimal value) &&
            value == pair.Value);
}

internal static class RuntimeSnapshotCollections
{
    public static IReadOnlyList<T> List<T>(IEnumerable<T>? values = null)
    {
        T[] snapshot = (values ?? []).ToArray();
        if (snapshot.Any(static value => value is null))
        {
            throw new ArgumentException("Snapshot collections cannot contain null entries.", nameof(values));
        }

        return Array.AsReadOnly(snapshot);
    }

    public static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values = null)
        where TKey : notnull
    {
        System.Collections.Generic.Dictionary<TKey, TValue> copy = [];
        foreach ((TKey key, TValue value) in values ?? [])
        {
            if (key is null || value is null)
            {
                throw new ArgumentException(
                    "Snapshot dictionaries cannot contain null keys or values.",
                    nameof(values));
            }

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
