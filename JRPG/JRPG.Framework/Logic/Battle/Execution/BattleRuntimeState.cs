using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Runtime;

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
    public decimal Maximum { get; private set; }

    internal decimal Set(decimal value)
    {
        decimal previous = Current;
        Current = Math.Clamp(value, 0, Maximum);
        return Current - previous;
    }

    internal decimal Add(decimal value) => Set(Current + value);

    internal void Replace(decimal current, decimal maximum)
    {
        if (maximum < 0 || current < 0 || current > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(current), "Resource values must satisfy 0 <= current <= maximum.");
        }

        Current = current;
        Maximum = maximum;
    }

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
public sealed record BattleOtherStatusState(DurationDefinition Duration, bool IsRemovable = true);
public sealed record BattleDurationTickResult(
    ContentId Id,
    DurationDefinition PreviousDuration,
    DurationDefinition? CurrentDuration,
    bool Expired);

public sealed class RuntimeActorState
{
    private readonly Dictionary<ContentId, BattleResourceState> _resources;
    private readonly Dictionary<ContentId, ActiveAilmentState> _ailments = [];
    private readonly Dictionary<ContentId, BattleStatStageState> _statStages = [];
    private readonly Dictionary<ChargeKind, BattleChargeState> _charges = [];
    private readonly Dictionary<ShieldKind, BattleShieldState> _shields = [];
    private readonly Dictionary<DamageElement, BattleAffinityOverrideState> _affinityOverrides = [];
    private readonly Dictionary<ContentId, BattleOtherStatusState> _otherStatuses = [];
    private readonly HashSet<ContentId> _skillIds;
    private readonly HashSet<ContentId> _capabilityIds;
    private readonly Dictionary<RuntimeInstanceId, HashSet<AnalysisLayer>> _analysis = [];
    private IReadOnlyDictionary<ContentId, decimal> _baseStats;
    private IReadOnlyDictionary<ContentId, decimal> _effectiveStats;
    private IReadOnlyDictionary<ContentId, decimal> _baseResourceValues;
    private bool _isActive;

    public RuntimeActorState(
        RuntimeInstanceId instanceId,
        ContentId entityId,
        ContentId teamId,
        ContentId vitalResourceId,
        CombatDefenseProfile defenseProfile,
        IEnumerable<BattleResourceState> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>>? stats = null,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<ContentId>? capabilityIds = null,
        IEnumerable<SkillDefinition>? passiveSkills = null,
        bool isActive = true,
        RuntimeActorIdentitySnapshot? identity = null,
        RuntimeActorOwnershipSnapshot? ownership = null,
        RuntimeActorDeploymentSnapshot? deployment = null,
        RuntimeProgressionSnapshot? progression = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseStats = null,
        RuntimeSkillStateSnapshot? skillState = null,
        RuntimeFormStockSnapshot? forms = null,
        RuntimeEquipmentSnapshot? equipment = null)
    {
        ArgumentNullException.ThrowIfNull(defenseProfile);
        ArgumentNullException.ThrowIfNull(resources);

        Identity = identity ?? new RuntimeActorIdentitySnapshot(
            instanceId,
            entityId,
            ContentId.Parse("actor"),
            entityId.ToString());
        Ownership = ownership ?? new RuntimeActorOwnershipSnapshot(
            ContentId.Parse("runtime"),
            teamId);
        Deployment = deployment ?? new RuntimeActorDeploymentSnapshot(
            isActive ? RuntimeActorDeployment.Active : RuntimeActorDeployment.Reserve,
            isActive);
        Progression = progression ?? new RuntimeProgressionSnapshot(1, 0, 0, 0);
        VitalResourceId = vitalResourceId;
        DefenseProfile = defenseProfile;
        _resources = resources.ToDictionary(resource => resource.Id, resource => resource.Copy());
        if (!_resources.ContainsKey(vitalResourceId))
        {
            throw new ArgumentException("The vital resource must be present in the resource collection.", nameof(resources));
        }

        _effectiveStats = Snapshot(stats);
        _baseStats = Snapshot(baseStats ?? stats);
        _baseResourceValues = Snapshot(baseResourceValues);
        _skillIds = new HashSet<ContentId>(skillIds ?? []);
        _capabilityIds = new HashSet<ContentId>(capabilityIds ?? []);
        Skills = skillState ?? new RuntimeSkillStateSnapshot(_skillIds, _skillIds);
        Forms = forms ?? new RuntimeFormStockSnapshot();
        Equipment = equipment ?? new RuntimeEquipmentSnapshot();
        Passives = new BattlePassiveCollection(passiveSkills);
        _isActive = Deployment.IsActive;
    }

    public static RuntimeActorState Restore(
        RuntimeActorSnapshot snapshot,
        CombatDefenseProfile defenseProfile,
        IEnumerable<SkillDefinition>? passiveSkills = null,
        IEnumerable<AilmentDefinition>? ailments = null,
        IEnumerable<ContentId>? capabilityIds = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(defenseProfile);
        var state = new RuntimeActorState(
            snapshot.Identity.InstanceId,
            snapshot.Identity.EntityDefinitionId,
            snapshot.Ownership.TeamId,
            snapshot.VitalResourceId,
            defenseProfile,
            snapshot.Resources.Select(resource => new BattleResourceState(
                resource.ResourceId,
                resource.Current,
                resource.Maximum)),
            snapshot.Stats.EffectiveStats,
            snapshot.Skills.LearnedSkillIds,
            capabilityIds ?? snapshot.CapabilityIds,
            passiveSkills,
            snapshot.Deployment.IsActive,
            snapshot.Identity,
            snapshot.Ownership,
            snapshot.Deployment,
            snapshot.Progression,
            snapshot.BaseResourceValues,
            snapshot.Stats.BaseStats,
            snapshot.Skills,
            snapshot.Forms,
            snapshot.Equipment);
        state.RestoreBattleStatus(
            snapshot.BattleStatus,
            (ailments ?? []).ToDictionary(ailment => ailment.Id));
        state.RestoreBattleActivations(snapshot.BattleActivations);
        return state;
    }

    public RuntimeInstanceId InstanceId => Identity.InstanceId;
    public ContentId EntityId => Identity.EntityDefinitionId;
    public ContentId TeamId => Ownership.TeamId;
    public RuntimeActorIdentitySnapshot Identity { get; }
    public RuntimeActorOwnershipSnapshot Ownership { get; }
    public RuntimeActorDeploymentSnapshot Deployment { get; private set; }
    public RuntimeProgressionSnapshot Progression { get; private set; }
    public RuntimeSkillStateSnapshot Skills { get; private set; }
    public RuntimeFormStockSnapshot Forms { get; private set; }
    public RuntimeEquipmentSnapshot Equipment { get; private set; }
    public ContentId VitalResourceId { get; }
    public CombatDefenseProfile DefenseProfile { get; }
    public BattlePassiveCollection Passives { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseStats => _baseStats;
    public IReadOnlyDictionary<ContentId, decimal> Stats => _effectiveStats;
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues => _baseResourceValues;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            Deployment = Deployment with { IsActive = value };
        }
    }
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
    public IReadOnlySet<ContentId> OtherStatuses => new ReadOnlySet<ContentId>(_otherStatuses.Keys);
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

    public IReadOnlySet<AnalysisLayer> GetAnalysis(RuntimeInstanceId targetInstanceId)
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

    public void AddOtherStatus(ContentId statusId) =>
        AddOtherStatus(statusId, new PermanentDurationDefinition());

    public void AddOtherStatus(
        ContentId statusId,
        DurationDefinition duration,
        bool isRemovable = true) =>
        _otherStatuses[statusId] = new BattleOtherStatusState(
            duration ?? throw new ArgumentNullException(nameof(duration)),
            isRemovable);

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
                if (_otherStatuses.TryGetValue(statusId, out BattleOtherStatusState? state) && state.IsRemovable)
                {
                    _otherStatuses.Remove(statusId);
                }
            }
        }

        int after = _statStages.Count + _charges.Count + _shields.Count + _affinityOverrides.Count + _otherStatuses.Count;
        return before - after;
    }

    public void Reveal(RuntimeInstanceId targetInstanceId, IEnumerable<AnalysisLayer> layers)
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

    public RuntimeActorSnapshot ToSnapshot() =>
        new(
            Identity,
            Ownership,
            Deployment with { IsActive = IsActive },
            Progression,
            _resources.Values.Select(resource => new RuntimeResourceSnapshot(
                resource.Id,
                resource.Current,
                resource.Maximum)),
            new RuntimeStatBlockSnapshot(_baseStats, _effectiveStats),
            Skills,
            Forms,
            Equipment,
            CaptureBattleStatus(),
            new RuntimeBattleActivationSnapshot(
                Passives.CaptureActivations(),
                Passives.CaptureStates()),
            _baseResourceValues,
            VitalResourceId,
            _capabilityIds.OrderBy(id => id.ToString(), StringComparer.Ordinal));

    internal void ApplyProgression(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IEnumerable<RuntimeResourceSnapshot> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>> baseResourceValues)
    {
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(stats);
        ReplaceResources(resources);
        Progression = progression;
        _baseStats = Snapshot(stats.BaseStats);
        _effectiveStats = Snapshot(stats.EffectiveStats);
        _baseResourceValues = Snapshot(baseResourceValues);
    }

    internal void ReplaceResources(IEnumerable<RuntimeResourceSnapshot> resources)
    {
        RuntimeResourceSnapshot[] replacements =
            (resources ?? throw new ArgumentNullException(nameof(resources))).ToArray();
        var replacementIds = replacements.Select(resource => resource.ResourceId).ToHashSet();
        if (!replacementIds.Contains(VitalResourceId) || replacementIds.Count != replacements.Length)
        {
            throw new ArgumentException("Replacement resources must be unique and contain the vital resource.", nameof(resources));
        }

        _resources.Clear();
        foreach (RuntimeResourceSnapshot resource in replacements)
        {
            _resources.Add(
                resource.ResourceId,
                new BattleResourceState(resource.ResourceId, resource.Current, resource.Maximum));
        }
    }

    internal void RestoreBattleStatus(
        RuntimeBattleStatusSnapshot status,
        IReadOnlyDictionary<ContentId, AilmentDefinition> ailments)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(ailments);

        _ailments.Clear();
        foreach (RuntimeTimedStateSnapshot ailment in status.Ailments)
        {
            _ailments.Add(
                ailment.Id,
                new ActiveAilmentState(
                    ailments[ailment.Id],
                    ailment.Duration,
                    ailment.IsRemovable));
        }

        _otherStatuses.Clear();
        foreach (RuntimeTimedStateSnapshot other in status.Statuses)
        {
            _otherStatuses.Add(other.Id, new BattleOtherStatusState(other.Duration, other.IsRemovable));
        }

        _statStages.Clear();
        foreach (RuntimeStatStageSnapshot stage in status.StatStages)
        {
            _statStages.Add(stage.ModifierTrackId, new BattleStatStageState(stage.Stage, stage.Duration));
        }

        _charges.Clear();
        foreach (RuntimeChargeSnapshot charge in status.Charges)
        {
            _charges.Add(charge.Kind, new BattleChargeState(charge.Multiplier, charge.Duration));
        }

        _shields.Clear();
        foreach (RuntimeShieldSnapshot shield in status.Shields)
        {
            _shields.Add(shield.Kind, new BattleShieldState(shield.Duration));
        }

        _affinityOverrides.Clear();
        foreach (RuntimeAffinityOverrideSnapshot affinity in status.AffinityOverrides)
        {
            _affinityOverrides.Add(
                affinity.Element,
                new BattleAffinityOverrideState(affinity.Affinity, affinity.Duration));
        }

        IsGuarding = status.IsGuarding;
        _analysis.Clear();
        foreach (RuntimeAnalysisSnapshot analysis in status.Analysis)
        {
            _analysis.Add(analysis.TargetInstanceId, new HashSet<AnalysisLayer>(analysis.Layers));
        }
    }

    internal void RestoreBattleActivations(RuntimeBattleActivationSnapshot activations)
    {
        ArgumentNullException.ThrowIfNull(activations);
        Passives.RestoreStates(activations.PassiveSkillStates);
        Passives.RestoreActivations(activations.PassiveActivations);
    }

    private RuntimeBattleStatusSnapshot CaptureBattleStatus() =>
        new(
            _ailments.Select(pair => new RuntimeTimedStateSnapshot(
                pair.Key,
                pair.Value.Duration,
                pair.Value.IsRemovable)),
            _otherStatuses.Select(pair => new RuntimeTimedStateSnapshot(
                pair.Key,
                pair.Value.Duration,
                pair.Value.IsRemovable)),
            _statStages.Select(pair => new RuntimeStatStageSnapshot(
                pair.Key,
                pair.Value.Stage,
                pair.Value.Duration)),
            _charges.Select(pair => new RuntimeChargeSnapshot(
                pair.Key,
                pair.Value.Multiplier,
                pair.Value.Duration)),
            _shields.Select(pair => new RuntimeShieldSnapshot(pair.Key, pair.Value.Duration)),
            _affinityOverrides.Select(pair => new RuntimeAffinityOverrideSnapshot(
                pair.Key,
                pair.Value.Affinity,
                pair.Value.Duration)),
            IsGuarding,
            _analysis.Select(pair => new RuntimeAnalysisSnapshot(pair.Key, pair.Value)));

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
