using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities.Components;

namespace JRPGPrototype.Logic.Battle.Execution;

public sealed class BattleResourceState
{
    public BattleResourceState(ContentId id, decimal current, decimal maximum)
    {
        if (maximum < 0 || current < 0 || current > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(current), "Resource values must satisfy 0 <= current <= maximum.");
        }

        Id = id;
        Current = current;
        Maximum = maximum;
    }

    public ContentId Id { get; }
    public decimal Current { get; private set; }
    public decimal Maximum { get; }

    internal decimal Set(decimal value)
    {
        decimal previous = Current;
        Current = Math.Clamp(value, 0, Maximum);
        return Current - previous;
    }

    internal decimal Add(decimal value) => Set(Current + value);

    internal BattleResourceState Copy() => new(Id, Current, Maximum);
}

public sealed record ActiveAilmentState(
    AilmentDefinition Definition,
    DurationDefinition Duration,
    bool IsRemovable = true);

public sealed record BattleStatStageState(int Stage, DurationDefinition? Duration);
public sealed record BattleChargeState(decimal Multiplier, DurationDefinition? Duration);
public sealed record BattleShieldState(DurationDefinition? Duration);
public sealed record BattleAffinityOverrideState(ElementalAffinity Affinity, DurationDefinition Duration);
public sealed record BattleDurationTickResult(
    ContentId Id,
    DurationDefinition PreviousDuration,
    DurationDefinition? CurrentDuration,
    bool Expired);

public class RuntimeActorState
{
    private readonly Dictionary<ContentId, BattleResourceState> _resources;
    private readonly Dictionary<ContentId, ActiveAilmentState> _ailments = [];
    private readonly Dictionary<ContentId, BattleStatStageState> _statStages = [];
    private readonly Dictionary<ChargeKind, BattleChargeState> _charges = [];
    private readonly Dictionary<ShieldKind, BattleShieldState> _shields = [];
    private readonly Dictionary<DamageElement, BattleAffinityOverrideState> _affinityOverrides = [];
    private readonly HashSet<ContentId> _otherStatuses = [];
    private readonly HashSet<ContentId> _skillIds;
    private readonly HashSet<ContentId> _capabilityIds;
    private readonly Dictionary<ContentId, HashSet<AnalysisLayer>> _analysis = [];

    public RuntimeActorState(
        ContentId instanceId,
        ContentId entityId,
        ContentId teamId,
        ContentId vitalResourceId,
        CombatDefenseProfile defenseProfile,
        IEnumerable<BattleResourceState> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>>? stats = null,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<ContentId>? capabilityIds = null,
        IEnumerable<SkillDefinition>? passiveSkills = null,
        bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(defenseProfile);
        ArgumentNullException.ThrowIfNull(resources);

        InstanceId = instanceId;
        EntityId = entityId;
        TeamId = teamId;
        VitalResourceId = vitalResourceId;
        DefenseProfile = defenseProfile;
        _resources = resources.ToDictionary(resource => resource.Id, resource => resource.Copy());
        if (!_resources.ContainsKey(vitalResourceId))
        {
            throw new ArgumentException("The vital resource must be present in the resource collection.", nameof(resources));
        }

        Stats = Snapshot(stats);
        _skillIds = new HashSet<ContentId>(skillIds ?? []);
        _capabilityIds = new HashSet<ContentId>(capabilityIds ?? []);
        Passives = new BattlePassiveCollection(passiveSkills);
        IsActive = isActive;
    }

    public ContentId InstanceId { get; }
    public ContentId EntityId { get; }
    public ContentId TeamId { get; }
    public ContentId VitalResourceId { get; }
    public CombatDefenseProfile DefenseProfile { get; }
    public BattlePassiveCollection Passives { get; }
    public IReadOnlyDictionary<ContentId, decimal> Stats { get; }
    public bool IsActive { get; set; }
    public bool IsGuarding { get; private set; }
    public bool IsDefeated => GetRequiredResource(VitalResourceId).Current <= 0;
    public IReadOnlyDictionary<ContentId, BattleResourceState> Resources =>
        new ReadOnlyDictionary<ContentId, BattleResourceState>(_resources);
    public IReadOnlyDictionary<ContentId, ActiveAilmentState> Ailments =>
        new ReadOnlyDictionary<ContentId, ActiveAilmentState>(_ailments);
    public IReadOnlyDictionary<ContentId, BattleStatStageState> StatStages =>
        new ReadOnlyDictionary<ContentId, BattleStatStageState>(_statStages);
    public IReadOnlyDictionary<ChargeKind, BattleChargeState> Charges =>
        new ReadOnlyDictionary<ChargeKind, BattleChargeState>(_charges);
    public IReadOnlyDictionary<ShieldKind, BattleShieldState> Shields =>
        new ReadOnlyDictionary<ShieldKind, BattleShieldState>(_shields);
    public IReadOnlyDictionary<DamageElement, BattleAffinityOverrideState> AffinityOverrides =>
        new ReadOnlyDictionary<DamageElement, BattleAffinityOverrideState>(_affinityOverrides);
    public IReadOnlySet<ContentId> OtherStatuses => new ReadOnlySet<ContentId>(_otherStatuses);
    public IReadOnlySet<ContentId> SkillIds => new ReadOnlySet<ContentId>(_skillIds);
    public IReadOnlySet<ContentId> CapabilityIds => new ReadOnlySet<ContentId>(_capabilityIds);

    public bool TryGetResource(ContentId id, out BattleResourceState? resource) =>
        _resources.TryGetValue(id, out resource);

    public BattleResourceState GetRequiredResource(ContentId id) =>
        _resources.TryGetValue(id, out BattleResourceState? resource)
            ? resource
            : throw new KeyNotFoundException($"Actor '{InstanceId}' has no resource '{id}'.");

    public bool HasAilment(ContentId id) => _ailments.ContainsKey(id);
    public bool HasSkill(ContentId id) => _skillIds.Contains(id);
    public bool HasCapability(ContentId id) => _capabilityIds.Contains(id);
    public bool HasBuff(ContentId modifierTrackId) =>
        _statStages.TryGetValue(modifierTrackId, out BattleStatStageState? state) && state.Stage != 0;

    public ElementalAffinity GetElementalAffinity(
        DamageElement element,
        IEnumerable<ElementalAffinity>? passiveReplacements = null,
        bool isBroken = false)
    {
        _affinityOverrides.TryGetValue(element, out BattleAffinityOverrideState? activeOverride);
        return ElementalAffinityResolver.Resolve(
            DefenseProfile,
            element,
            passiveReplacements,
            activeShields: _shields.Keys,
            isBroken: isBroken,
            activeOverride: activeOverride?.Affinity);
    }

    public IReadOnlySet<AnalysisLayer> GetAnalysis(ContentId targetInstanceId)
    {
        return _analysis.TryGetValue(targetInstanceId, out HashSet<AnalysisLayer>? layers)
            ? new ReadOnlySet<AnalysisLayer>(layers)
            : new ReadOnlySet<AnalysisLayer>([]);
    }

    public decimal SetResource(ContentId id, decimal value) => GetRequiredResource(id).Set(value);
    public decimal AddResource(ContentId id, decimal value) => GetRequiredResource(id).Add(value);

    public void ApplyAilment(AilmentDefinition definition, DurationDefinition duration)
    {
        if (definition.ExclusivityGroupId is ContentId exclusivityGroup)
        {
            foreach (ContentId existingId in _ailments
                         .Where(pair => pair.Value.Definition.ExclusivityGroupId == exclusivityGroup)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _ailments.Remove(existingId);
            }
        }

        _ailments[definition.Id] = new ActiveAilmentState(definition, duration);
    }

    public void ApplyAilment(AilmentDefinition definition, DurationDefinition duration, bool isRemovable)
    {
        ApplyAilment(definition, duration);
        _ailments[definition.Id] = _ailments[definition.Id] with { IsRemovable = isRemovable };
    }

    public IReadOnlyList<ContentId> RemoveAilments(Func<ActiveAilmentState, bool> predicate)
    {
        ContentId[] removed = _ailments
            .Where(pair => pair.Value.IsRemovable && predicate(pair.Value))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (ContentId id in removed)
        {
            _ailments.Remove(id);
        }

        return Array.AsReadOnly(removed);
    }

    public void ChangeStatStage(ContentId id, int delta, DurationDefinition? duration)
    {
        int current = _statStages.TryGetValue(id, out BattleStatStageState? state) ? state.Stage : 0;
        _statStages[id] = new BattleStatStageState(current + delta, duration ?? state?.Duration);
    }

    public void GrantCharge(ChargeKind kind, decimal multiplier, DurationDefinition? duration) =>
        _charges[kind] = new BattleChargeState(multiplier, duration);

    public void GrantShield(ShieldKind kind, DurationDefinition? duration) =>
        _shields[kind] = new BattleShieldState(duration);

    public void SetGuarding(bool isGuarding) => IsGuarding = isGuarding;

    public void OverrideAffinity(DamageElement element, ElementalAffinity affinity, DurationDefinition duration) =>
        _affinityOverrides[element] = new BattleAffinityOverrideState(affinity, duration);

    public void AddOtherStatus(ContentId statusId) => _otherStatuses.Add(statusId);

    public IReadOnlyList<BattleDurationTickResult> TickAilmentDurations(ContentId eventId)
    {
        var results = new List<BattleDurationTickResult>();
        foreach ((ContentId id, ActiveAilmentState state) in _ailments.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsActive, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            results.Add(new BattleDurationTickResult(id, state.Duration, current, expired));
            if (expired)
            {
                _ailments.Remove(id);
            }
            else if (current is not null)
            {
                _ailments[id] = state with { Duration = current };
            }
        }

        return Array.AsReadOnly(results.ToArray());
    }

    public IReadOnlyList<BattleDurationTickResult> TickTimedStatuses(ContentId eventId)
    {
        var results = new List<BattleDurationTickResult>();

        foreach ((ContentId id, BattleStatStageState state) in _statStages.ToArray())
        {
            if (state.Duration is null ||
                !TryTickDuration(state.Duration, eventId, IsActive, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            results.Add(new BattleDurationTickResult(id, state.Duration, current, expired));
            if (expired)
            {
                _statStages.Remove(id);
            }
            else
            {
                _statStages[id] = state with { Duration = current };
            }
        }

        foreach ((ChargeKind kind, BattleChargeState state) in _charges.ToArray())
        {
            if (state.Duration is null ||
                !TryTickDuration(state.Duration, eventId, IsActive, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            ContentId id = ContentId.Parse("charge_" + kind.ToString().ToLowerInvariant());
            results.Add(new BattleDurationTickResult(id, state.Duration, current, expired));
            if (expired)
            {
                _charges.Remove(kind);
            }
            else
            {
                _charges[kind] = state with { Duration = current };
            }
        }

        foreach ((ShieldKind kind, BattleShieldState state) in _shields.ToArray())
        {
            if (state.Duration is null ||
                !TryTickDuration(state.Duration, eventId, IsActive, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            ContentId id = ContentId.Parse("shield_" + kind.ToString().ToLowerInvariant());
            results.Add(new BattleDurationTickResult(id, state.Duration, current, expired));
            if (expired)
            {
                _shields.Remove(kind);
            }
            else
            {
                _shields[kind] = state with { Duration = current };
            }
        }

        foreach ((DamageElement element, BattleAffinityOverrideState state) in _affinityOverrides.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsActive, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            ContentId id = ContentId.Parse("affinity_override_" + element.ToString().ToLowerInvariant());
            results.Add(new BattleDurationTickResult(id, state.Duration, current, expired));
            if (expired)
            {
                _affinityOverrides.Remove(element);
            }
            else if (current is not null)
            {
                _affinityOverrides[element] = state with { Duration = current };
            }
        }

        return Array.AsReadOnly(results.ToArray());
    }

    public void ClearTransientStatuses()
    {
        IsGuarding = false;
        _charges.Clear();
        _shields.Clear();
    }

    public void ClearEncounterStatuses()
    {
        _statStages.Clear();
        _affinityOverrides.Clear();
    }

    public int RemoveStatuses(IEnumerable<StatusEffectKind> kinds, IEnumerable<ContentId> statusIds)
    {
        int before = _statStages.Count + _charges.Count + _shields.Count + _affinityOverrides.Count + _otherStatuses.Count;
        HashSet<StatusEffectKind> requested = new(kinds);
        if (requested.Contains(StatusEffectKind.Buff))
        {
            RemoveStatStages(stage => stage > 0);
        }
        if (requested.Contains(StatusEffectKind.Debuff))
        {
            RemoveStatStages(stage => stage < 0);
        }
        if (requested.Contains(StatusEffectKind.Charge))
        {
            _charges.Clear();
        }
        if (requested.Contains(StatusEffectKind.Shield))
        {
            _shields.Clear();
        }
        if (requested.Contains(StatusEffectKind.AffinityOverride))
        {
            _affinityOverrides.Clear();
        }
        if (requested.Contains(StatusEffectKind.Other))
        {
            foreach (ContentId statusId in statusIds)
            {
                _otherStatuses.Remove(statusId);
            }
        }

        int after = _statStages.Count + _charges.Count + _shields.Count + _affinityOverrides.Count + _otherStatuses.Count;
        return before - after;
    }

    public void Reveal(ContentId targetInstanceId, IEnumerable<AnalysisLayer> layers)
    {
        if (!_analysis.TryGetValue(targetInstanceId, out HashSet<AnalysisLayer>? known))
        {
            known = [];
            _analysis.Add(targetInstanceId, known);
        }

        foreach (AnalysisLayer layer in layers)
        {
            if (layer == AnalysisLayer.Full)
            {
                known.UnionWith(Enum.GetValues<AnalysisLayer>());
            }
            else
            {
                known.Add(layer);
            }
        }
    }

    private void RemoveStatStages(Func<int, bool> predicate)
    {
        foreach (ContentId id in _statStages.Where(pair => predicate(pair.Value.Stage)).Select(pair => pair.Key).ToArray())
        {
            _statStages.Remove(id);
        }
    }

    private static bool TryTickDuration(
        DurationDefinition duration,
        ContentId eventId,
        bool isActive,
        out DurationDefinition? current,
        out bool expired)
    {
        current = duration;
        expired = false;
        if (duration is not TurnDurationDefinition turns ||
            turns.TickEventId != eventId ||
            turns.SuspendWhileReserve && !isActive)
        {
            return false;
        }

        int remaining = turns.Value - 1;
        if (remaining <= 0)
        {
            current = null;
            expired = true;
        }
        else
        {
            current = turns with { Value = remaining };
        }

        return true;
    }

    private static IReadOnlyDictionary<ContentId, decimal> Snapshot(
        IEnumerable<KeyValuePair<ContentId, decimal>>? values) =>
        new ReadOnlyDictionary<ContentId, decimal>((values ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));

    private sealed class ReadOnlySet<T>(IEnumerable<T> values) : IReadOnlySet<T>
    {
        private readonly HashSet<T> _values = new(values);
        public int Count => _values.Count;
        public bool Contains(T item) => _values.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => _values.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => _values.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => _values.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => _values.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => _values.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => _values.SetEquals(other);
        public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public sealed class BattleActorState : RuntimeActorState
{
    public BattleActorState(
        ContentId instanceId,
        ContentId entityId,
        ContentId teamId,
        ContentId vitalResourceId,
        CombatDefenseProfile defenseProfile,
        IEnumerable<BattleResourceState> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>>? stats = null,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<ContentId>? capabilityIds = null,
        IEnumerable<SkillDefinition>? passiveSkills = null,
        bool isActive = true)
        : base(
            instanceId,
            entityId,
            teamId,
            vitalResourceId,
            defenseProfile,
            resources,
            stats,
            skillIds,
            capabilityIds,
            passiveSkills,
            isActive)
    {
    }
}
