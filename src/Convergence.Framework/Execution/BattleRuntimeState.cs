using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Battle;
using Convergence.Internal;
using Convergence.Runtime;

namespace Convergence.Execution;

public sealed class BattleResourceState
{
    public BattleResourceState(ContentId id, decimal current, decimal maximum)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("Resource ID cannot be empty.", nameof(id));
        }

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

    internal decimal Add(decimal value) => Set(CombatArithmetic.SaturatingAdd(Current, value));

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

public static class BattleStatStageRange
{
    public const int Minimum = -4;
    public const int Maximum = 4;

    public static bool Contains(int stage) => stage is >= Minimum and <= Maximum;
    public static int Clamp(int stage) => Math.Clamp(stage, Minimum, Maximum);

    public static int ApplyDelta(int current, int delta)
    {
        long requested = (long)current + delta;
        return requested switch
        {
            < Minimum => Minimum,
            > Maximum => Maximum,
            _ => (int)requested
        };
    }
}
public sealed record BattleChargeState(decimal Multiplier, DurationDefinition? Duration);
public sealed record BattleShieldState(DurationDefinition? Duration);
public sealed record BattleAffinityBreakState(DurationDefinition Duration);
public sealed record BattleAffinityOverrideState(ElementalAffinity Affinity, DurationDefinition Duration);
public sealed record BattleOtherStatusState(DurationDefinition Duration, bool IsRemovable = true);

public enum BattleDurationStateKind
{
    Ailment,
    StatStage,
    Charge,
    Shield,
    AffinityOverride,
    AffinityBreak,
    OtherStatus
}

public sealed record BattleDurationTickResult(
    ContentId Id,
    DurationDefinition PreviousDuration,
    DurationDefinition? CurrentDuration,
    bool Expired,
    BattleDurationStateKind StateKind);

public sealed class RuntimeActorState
{
    private readonly Dictionary<ContentId, BattleResourceState> _resources;
    private readonly Dictionary<ContentId, ActiveAilmentState> _ailments = [];
    private readonly Dictionary<ContentId, BattleStatStageState> _statStages = [];
    private readonly Dictionary<ChargeKind, BattleChargeState> _charges = [];
    private readonly Dictionary<ShieldKind, BattleShieldState> _shields = [];
    private readonly Dictionary<DamageElement, BattleAffinityBreakState> _affinityBreaks = [];
    private readonly Dictionary<DamageElement, BattleAffinityOverrideState> _affinityOverrides = [];
    private readonly Dictionary<ContentId, BattleOtherStatusState> _otherStatuses = [];
    private readonly HashSet<ContentId> _skillIds;
    private readonly HashSet<ContentId> _capabilityIds;
    private readonly Dictionary<RuntimeInstanceId, HashSet<AnalysisLayer>> _analysis = [];
    private IReadOnlyDictionary<ContentId, decimal> _baseStats;
    private IReadOnlyDictionary<ContentId, decimal> _effectiveStats;
    private IReadOnlyDictionary<ContentId, decimal> _baseResourceValues;
    public RuntimeActorState(
        RuntimeInstanceId instanceId,
        ContentId entityId,
        ContentId teamId,
        ContentId vitalResourceId,
        CombatDefenseProfile defenseProfile,
        IEnumerable<BattleResourceState> resources,
        RuntimeEncounterPresenceSnapshot encounterPresence,
        RuntimeActorAffiliationSnapshot affiliation,
        IEnumerable<KeyValuePair<ContentId, decimal>>? stats = null,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<ContentId>? capabilityIds = null,
        IEnumerable<SkillDefinition>? passiveSkills = null,
        RuntimeActorIdentitySnapshot? identity = null,
        RuntimeProgressionSnapshot? progression = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseStats = null,
        RuntimeSkillStateSnapshot? skillState = null,
        RuntimeEquipmentSnapshot? equipment = null)
    {
        ArgumentNullException.ThrowIfNull(defenseProfile);
        ArgumentNullException.ThrowIfNull(resources);

        RequireValid(instanceId, nameof(instanceId));
        RequireValid(entityId, nameof(entityId));
        RequireValid(teamId, nameof(teamId));
        RequireValid(vitalResourceId, nameof(vitalResourceId));

        Identity = identity ?? new RuntimeActorIdentitySnapshot(
            instanceId,
            entityId,
            ContentId.Parse("actor"),
            entityId.ToString());
        Affiliation = affiliation ?? throw new ArgumentNullException(nameof(affiliation));
        EncounterPresence = encounterPresence ?? throw new ArgumentNullException(nameof(encounterPresence));
        Progression = progression ?? new RuntimeProgressionSnapshot(1, 0, 0, 0);
        RequireValid(Identity.InstanceId, nameof(identity));
        RequireValid(Identity.EntityDefinitionId, nameof(identity));
        RequireValid(Identity.ActorKindId, nameof(identity));
        RequireValid(Affiliation.CommandAuthorityId, nameof(affiliation));
        RequireValid(Affiliation.TeamId, nameof(affiliation));

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
        RequireValid(_effectiveStats.Keys, nameof(stats));
        RequireValid(_baseStats.Keys, baseStats is null ? nameof(stats) : nameof(baseStats));
        RequireValid(_baseResourceValues.Keys, nameof(baseResourceValues));
        RuntimeActorNumericDomain.RequireValidStatValues(
            _baseStats,
            baseStats is null ? nameof(stats) : nameof(baseStats));
        RuntimeActorNumericDomain.RequireValidStatValues(_effectiveStats, nameof(stats));
        RuntimeActorNumericDomain.RequireValidBaseResourceValues(
            _baseResourceValues,
            nameof(baseResourceValues));
        Skills = skillState ?? new RuntimeSkillStateSnapshot(skillIds, skillIds);
        _skillIds = new HashSet<ContentId>(Skills.EquippedSkillIds);
        if (skillIds is not null && !_skillIds.SetEquals(skillIds))
        {
            throw new ArgumentException(
                "Runtime executable skill IDs must match the equipped skill state.",
                nameof(skillIds));
        }
        _capabilityIds = new HashSet<ContentId>(capabilityIds ?? []);
        RequireValid(_skillIds, nameof(skillIds));
        RequireValid(_capabilityIds, nameof(capabilityIds));
        Equipment = equipment ?? new RuntimeEquipmentSnapshot();
        RequireValid(Skills.LearnedSkillIds, nameof(skillState));
        RequireValid(Skills.EquippedSkillIds, nameof(skillState));
        if (Skills.LearnedSkillIds.Distinct().Count() != Skills.LearnedSkillIds.Count ||
            Skills.EquippedSkillIds.Distinct().Count() != Skills.EquippedSkillIds.Count ||
            Skills.EquippedSkillIds.Except(Skills.LearnedSkillIds).Any() ||
            Skills.PendingChoices.Any(choice =>
                !choice.Token.IsValid || !choice.SkillId.IsValid) ||
            Skills.PendingChoices.Select(choice => choice.Token).Distinct().Count() !=
            Skills.PendingChoices.Count ||
            Skills.PendingChoices.Select(choice => choice.SkillId).Distinct().Count() !=
            Skills.PendingChoices.Count ||
            Skills.PendingChoices.Any(choice =>
                Skills.LearnedSkillIds.Contains(choice.SkillId)))
        {
            throw new ArgumentException(
                "Runtime skill state contains duplicate, unlearned, or invalid pending entries.",
                nameof(skillState));
        }
        RequireValid(Equipment.EquippedItemIds.Values, nameof(equipment));
        Passives = new BattlePassiveCollection(passiveSkills);
    }

    internal static RuntimeActorState Restore(
        RuntimeActorSnapshot snapshot,
        CombatDefenseProfile defenseProfile,
        IEnumerable<SkillDefinition>? passiveSkills = null,
        IEnumerable<AilmentDefinition>? ailments = null,
        IEnumerable<ContentId>? capabilityIds = null,
        IReadOnlySet<ContentId>? registeredEventIds = null,
        IReadOnlySet<ContentId>? registeredPhaseIds = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(defenseProfile);
        SkillDefinition[] passiveDefinitions = (passiveSkills ?? []).ToArray();
        AilmentDefinition[] ailmentDefinitions = (ailments ?? []).ToArray();
        IReadOnlyList<RuntimeActorSnapshotIntegrityDiagnostic> integrityDiagnostics =
            RuntimeActorSnapshotIntegrity.ValidateForRestore(
                snapshot,
                passiveDefinitions.Select(skill => skill.Id),
                ailmentDefinitions.Select(ailment => ailment.Id),
                registeredEventIds,
                registeredPhaseIds);
        if (integrityDiagnostics.Count > 0)
        {
            RuntimeActorSnapshotIntegrityDiagnostic first = integrityDiagnostics[0];
            throw new ArgumentException(
                $"Runtime actor snapshot '{snapshot.Identity.InstanceId}' is invalid at '{first.Path}': {first.Message}",
                nameof(snapshot));
        }

        var state = new RuntimeActorState(
            snapshot.Identity.InstanceId,
            snapshot.Identity.EntityDefinitionId,
            snapshot.Affiliation.TeamId,
            snapshot.VitalResourceId,
            defenseProfile,
            snapshot.Resources.Select(resource => new BattleResourceState(
                resource.ResourceId,
                resource.Current,
                resource.Maximum)),
            snapshot.EncounterPresence,
            snapshot.Affiliation,
            snapshot.Stats.EffectiveStats,
            snapshot.Skills.EquippedSkillIds,
            capabilityIds ?? snapshot.CapabilityIds,
            passiveDefinitions,
            snapshot.Identity,
            snapshot.Progression,
            snapshot.BaseResourceValues,
            snapshot.Stats.BaseStats,
            snapshot.Skills,
            snapshot.Equipment);
        state.RestoreBattleStatus(
            snapshot.BattleStatus,
            ailmentDefinitions.ToDictionary(ailment => ailment.Id));
        state.RestoreBattleActivations(snapshot.BattleActivations);
        return state;
    }

    public RuntimeInstanceId InstanceId => Identity.InstanceId;
    public ContentId EntityId => Identity.EntityDefinitionId;
    public ContentId TeamId => Affiliation.TeamId;
    public RuntimeActorIdentitySnapshot Identity { get; }
    public RuntimeActorAffiliationSnapshot Affiliation { get; }
    public RuntimeEncounterPresenceSnapshot EncounterPresence { get; private set; }
    public RuntimeProgressionSnapshot Progression { get; private set; }
    public RuntimeSkillStateSnapshot Skills { get; private set; }
    public RuntimeEquipmentSnapshot Equipment { get; private set; }
    public ContentId VitalResourceId { get; }
    public CombatDefenseProfile DefenseProfile { get; private set; }
    public BattlePassiveCollection Passives { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseStats => _baseStats;
    public IReadOnlyDictionary<ContentId, decimal> Stats => _effectiveStats;
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues => _baseResourceValues;
    public bool IsDeployed => EncounterPresence.IsDeployed;

    public void SetEncounterPresence(bool isDeployed, bool hasSwappedThisTurn = false) =>
        EncounterPresence = new RuntimeEncounterPresenceSnapshot(
            isDeployed,
            hasSwappedThisTurn);

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
    public IReadOnlyDictionary<DamageElement, BattleAffinityBreakState> AffinityBreaks =>
        new ReadOnlyDictionary<DamageElement, BattleAffinityBreakState>(_affinityBreaks);
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
        IEnumerable<ElementalAffinity>? passiveReplacements = null)
    {
        _affinityOverrides.TryGetValue(element, out BattleAffinityOverrideState? activeOverride);
        return ElementalAffinityResolver.Resolve(
            DefenseProfile,
            element,
            passiveReplacements,
            activeShields: _shields.Keys,
            isBroken: _affinityBreaks.ContainsKey(element),
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

    public void ReplaceEquipment(RuntimeEquipmentSnapshot equipment)
    {
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
    }

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

    public int ChangeStatStage(ContentId id, int delta, DurationDefinition? duration)
    {
        int current = _statStages.TryGetValue(id, out BattleStatStageState? state) ? state.Stage : 0;
        int next = BattleStatStageRange.ApplyDelta(current, delta);
        _statStages[id] = new BattleStatStageState(next, duration ?? state?.Duration);
        return next - current;
    }

    public void GrantCharge(ChargeKind kind, decimal multiplier, DurationDefinition? duration)
    {
        EnumDomain.RequireDefined(kind, nameof(kind));
        _charges[kind] = new BattleChargeState(multiplier, duration);
    }

    public void GrantShield(ShieldKind kind, DurationDefinition? duration)
    {
        EnumDomain.RequireDefined(kind, nameof(kind));
        _shields[kind] = new BattleShieldState(duration);
    }

    public void BreakAffinity(DamageElement element, DurationDefinition duration)
    {
        EnumDomain.RequireDefined(element, nameof(element));
        if (element == DamageElement.Almighty)
        {
            throw new ArgumentException("Almighty cannot receive an affinity Break.", nameof(element));
        }

        _affinityBreaks[element] = new BattleAffinityBreakState(
            duration ?? throw new ArgumentNullException(nameof(duration)));
    }

    public void SetGuarding(bool isGuarding) => IsGuarding = isGuarding;

    public void OverrideAffinity(DamageElement element, ElementalAffinity affinity, DurationDefinition duration)
    {
        EnumDomain.RequireDefined(element, nameof(element));
        EnumDomain.RequireDefined(affinity, nameof(affinity));
        _affinityOverrides[element] = new BattleAffinityOverrideState(affinity, duration);
    }

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
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            results.Add(new BattleDurationTickResult(
                id,
                state.Duration,
                current,
                expired,
                BattleDurationStateKind.Ailment));
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
                !TryTickDuration(state.Duration, eventId, IsDeployed, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            results.Add(new BattleDurationTickResult(
                id,
                state.Duration,
                current,
                expired,
                BattleDurationStateKind.StatStage));
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
                !TryTickDuration(state.Duration, eventId, IsDeployed, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            ContentId id = ContentId.Parse("charge_" + kind.ToString().ToLowerInvariant());
            results.Add(new BattleDurationTickResult(
                id,
                state.Duration,
                current,
                expired,
                BattleDurationStateKind.Charge));
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
                !TryTickDuration(state.Duration, eventId, IsDeployed, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            ContentId id = ContentId.Parse("shield_" + kind.ToString().ToLowerInvariant());
            results.Add(new BattleDurationTickResult(
                id,
                state.Duration,
                current,
                expired,
                BattleDurationStateKind.Shield));
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
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            ContentId id = ContentId.Parse("affinity_override_" + element.ToString().ToLowerInvariant());
            results.Add(new BattleDurationTickResult(
                id,
                state.Duration,
                current,
                expired,
                BattleDurationStateKind.AffinityOverride));
            if (expired)
            {
                _affinityOverrides.Remove(element);
            }
            else if (current is not null)
            {
                _affinityOverrides[element] = state with { Duration = current };
            }
        }

        foreach ((DamageElement element, BattleAffinityBreakState state) in _affinityBreaks.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            ContentId id = ContentId.Parse("affinity_break_" + element.ToString().ToLowerInvariant());
            results.Add(new BattleDurationTickResult(
                id,
                state.Duration,
                current,
                expired,
                BattleDurationStateKind.AffinityBreak));
            if (expired)
            {
                _affinityBreaks.Remove(element);
            }
            else if (current is not null)
            {
                _affinityBreaks[element] = state with { Duration = current };
            }
        }

        foreach ((ContentId id, BattleOtherStatusState state) in _otherStatuses.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, out DurationDefinition? current, out bool expired))
            {
                continue;
            }

            results.Add(new BattleDurationTickResult(
                id,
                state.Duration,
                current,
                expired,
                BattleDurationStateKind.OtherStatus));
            if (expired)
            {
                _otherStatuses.Remove(id);
            }
            else if (current is not null)
            {
                _otherStatuses[id] = state with { Duration = current };
            }
        }

        return Array.AsReadOnly(results.ToArray());
    }

    public IReadOnlyList<BattleDurationTickResult> ExpireInstantDurations() =>
        ExpireDurations(duration => duration is InstantDurationDefinition);

    public IReadOnlyList<BattleDurationTickResult> ExpirePhaseDurations(ContentId phaseId) =>
        ExpireDurations(duration =>
            duration is PhaseDurationDefinition phase && phase.PhaseId == phaseId);

    public void ClearTransientStatuses()
    {
        IsGuarding = false;
        foreach (ChargeKind kind in _charges
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _charges.Remove(kind);
        }

        foreach (ShieldKind kind in _shields
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _shields.Remove(kind);
        }
    }

    public void ClearEncounterStatuses()
    {
        foreach (ContentId id in _ailments
                     .Where(pair => pair.Value.Duration is
                         InstantDurationDefinition or PhaseDurationDefinition or BattleDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _ailments.Remove(id);
        }

        foreach (ContentId id in _statStages
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _statStages.Remove(id);
        }

        foreach (ChargeKind kind in _charges
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _charges.Remove(kind);
        }

        foreach (ShieldKind kind in _shields
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _shields.Remove(kind);
        }

        foreach (DamageElement element in _affinityOverrides
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _affinityOverrides.Remove(element);
        }

        foreach (DamageElement element in _affinityBreaks
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _affinityBreaks.Remove(element);
        }

        foreach (ContentId id in _otherStatuses
                     .Where(pair => pair.Value.Duration is not PermanentDurationDefinition)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _otherStatuses.Remove(id);
        }
    }

    public int RemoveStatuses(IEnumerable<StatusEffectKind> kinds, IEnumerable<ContentId> statusIds)
    {
        int before = _statStages.Count + _charges.Count + _shields.Count + _affinityBreaks.Count +
            _affinityOverrides.Count + _otherStatuses.Count;
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
        if (requested.Contains(StatusEffectKind.AffinityBreak))
        {
            _affinityBreaks.Clear();
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

        int after = _statStages.Count + _charges.Count + _shields.Count + _affinityBreaks.Count +
            _affinityOverrides.Count + _otherStatuses.Count;
        return before - after;
    }

    public void Reveal(RuntimeInstanceId targetInstanceId, IEnumerable<AnalysisLayer> layers)
    {
        AnalysisLayer[] requestedLayers =
            (layers ?? throw new ArgumentNullException(nameof(layers))).ToArray();
        foreach (AnalysisLayer layer in requestedLayers)
        {
            EnumDomain.RequireDefined(layer, nameof(layers));
        }

        if (!_analysis.TryGetValue(targetInstanceId, out HashSet<AnalysisLayer>? known))
        {
            known = [];
            _analysis.Add(targetInstanceId, known);
        }

        foreach (AnalysisLayer layer in requestedLayers)
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
            Affiliation,
            EncounterPresence,
            Progression,
            _resources.Values.Select(resource => new RuntimeResourceSnapshot(
                resource.Id,
                resource.Current,
                resource.Maximum)),
            new RuntimeStatBlockSnapshot(_baseStats, _effectiveStats),
            Skills,
            Equipment,
            CaptureBattleStatus(),
            new RuntimeBattleActivationSnapshot(
                Passives.CaptureActivations(),
                Passives.CaptureStates()),
            _baseResourceValues,
            VitalResourceId,
            _capabilityIds.OrderBy(id => id.ToString(), StringComparer.Ordinal));

    internal RuntimeActorState CreateExecutionClone()
    {
        var clone = new RuntimeActorState(
            InstanceId,
            EntityId,
            TeamId,
            VitalResourceId,
            DefenseProfile,
            _resources.Values,
            EncounterPresence,
            Affiliation,
            _effectiveStats,
            _skillIds,
            _capabilityIds,
            Passives.Entries.Select(entry => entry.Skill),
            Identity,
            Progression,
            _baseResourceValues,
            _baseStats,
            Skills,
            Equipment);
        clone.ApplyExecutionStateFrom(this);
        return clone;
    }

    internal void ApplyExecutionStateFrom(RuntimeActorState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.InstanceId != InstanceId ||
            source.EntityId != EntityId ||
            source.TeamId != TeamId ||
            source.VitalResourceId != VitalResourceId)
        {
            throw new ArgumentException(
                "Execution state can only be committed to the actor it was staged from.",
                nameof(source));
        }

        Dictionary<ContentId, BattleResourceState> resources = source._resources
            .ToDictionary(pair => pair.Key, pair => pair.Value.Copy());
        Dictionary<ContentId, ActiveAilmentState> ailments = new(source._ailments);
        Dictionary<ContentId, BattleStatStageState> statStages = new(source._statStages);
        Dictionary<ChargeKind, BattleChargeState> charges = new(source._charges);
        Dictionary<ShieldKind, BattleShieldState> shields = new(source._shields);
        Dictionary<DamageElement, BattleAffinityBreakState> affinityBreaks =
            new(source._affinityBreaks);
        Dictionary<DamageElement, BattleAffinityOverrideState> affinityOverrides =
            new(source._affinityOverrides);
        Dictionary<ContentId, BattleOtherStatusState> otherStatuses =
            new(source._otherStatuses);
        ContentId[] skillIds = source._skillIds.ToArray();
        ContentId[] capabilityIds = source._capabilityIds.ToArray();
        Dictionary<RuntimeInstanceId, HashSet<AnalysisLayer>> analysis = source._analysis
            .ToDictionary(pair => pair.Key, pair => new HashSet<AnalysisLayer>(pair.Value));
        IReadOnlyDictionary<ContentId, decimal> baseStats = Snapshot(source._baseStats);
        IReadOnlyDictionary<ContentId, decimal> effectiveStats = Snapshot(source._effectiveStats);
        IReadOnlyDictionary<ContentId, decimal> baseResourceValues =
            Snapshot(source._baseResourceValues);

        _resources.Clear();
        foreach ((ContentId id, BattleResourceState resource) in resources)
        {
            _resources.Add(id, resource);
        }

        ReplaceDictionary(_ailments, ailments);
        ReplaceDictionary(_statStages, statStages);
        ReplaceDictionary(_charges, charges);
        ReplaceDictionary(_shields, shields);
        ReplaceDictionary(_affinityBreaks, affinityBreaks);
        ReplaceDictionary(_affinityOverrides, affinityOverrides);
        ReplaceDictionary(_otherStatuses, otherStatuses);

        _skillIds.Clear();
        _skillIds.UnionWith(skillIds);
        _capabilityIds.Clear();
        _capabilityIds.UnionWith(capabilityIds);
        ReplaceDictionary(_analysis, analysis);

        _baseStats = baseStats;
        _effectiveStats = effectiveStats;
        _baseResourceValues = baseResourceValues;
        EncounterPresence = source.EncounterPresence;
        Progression = source.Progression;
        Skills = source.Skills;
        Equipment = source.Equipment;
        DefenseProfile = source.DefenseProfile;
        IsGuarding = source.IsGuarding;
        Passives.ReplaceFrom(source.Passives);
    }

    internal void ApplyProgression(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IEnumerable<RuntimeResourceSnapshot> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>> baseResourceValues)
    {
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(stats);
        IReadOnlyDictionary<ContentId, decimal> nextBaseStats = Snapshot(stats.BaseStats);
        IReadOnlyDictionary<ContentId, decimal> nextEffectiveStats = Snapshot(stats.EffectiveStats);
        IReadOnlyDictionary<ContentId, decimal> nextBaseResourceValues = Snapshot(baseResourceValues);
        RuntimeActorNumericDomain.RequireValidStatValues(nextBaseStats, nameof(stats));
        RuntimeActorNumericDomain.RequireValidStatValues(nextEffectiveStats, nameof(stats));
        RuntimeActorNumericDomain.RequireValidBaseResourceValues(
            nextBaseResourceValues,
            nameof(baseResourceValues));
        ReplaceResources(resources);
        Progression = progression;
        _baseStats = nextBaseStats;
        _effectiveStats = nextEffectiveStats;
        _baseResourceValues = nextBaseResourceValues;
    }

    internal void ApplyCombatProfile(
        IEnumerable<KeyValuePair<ContentId, decimal>> effectiveStats,
        IEnumerable<RuntimeResourceSnapshot> resources,
        CombatDefenseProfile defenseProfile,
        RuntimeSkillStateSnapshot skills,
        IEnumerable<SkillDefinition> equippedSkillDefinitions)
    {
        ArgumentNullException.ThrowIfNull(defenseProfile);
        ArgumentNullException.ThrowIfNull(skills);
        IReadOnlyDictionary<ContentId, decimal> nextEffectiveStats = Snapshot(effectiveStats);
        RuntimeActorNumericDomain.RequireValidStatValues(nextEffectiveStats, nameof(effectiveStats));

        RuntimeResourceSnapshot[] resourceSnapshots =
            (resources ?? throw new ArgumentNullException(nameof(resources))).ToArray();
        if (resourceSnapshots.Select(resource => resource.ResourceId).Distinct().Count() != resourceSnapshots.Length ||
            !resourceSnapshots.Any(resource => resource.ResourceId == VitalResourceId))
        {
            throw new ArgumentException(
                "Composed resources must be unique and contain the vital resource.",
                nameof(resources));
        }

        BattleResourceState[] nextResources = resourceSnapshots
            .Select(resource => new BattleResourceState(
                resource.ResourceId,
                resource.Current,
                resource.Maximum))
            .ToArray();
        PrepareSkillState(
            skills,
            equippedSkillDefinitions,
            out RuntimeSkillStateSnapshot nextSkillState,
            out ContentId[] equippedSkillIds,
            out BattlePassiveCollection nextPassives);

        _resources.Clear();
        foreach (BattleResourceState resource in nextResources)
        {
            _resources.Add(resource.Id, resource);
        }

        _effectiveStats = nextEffectiveStats;
        DefenseProfile = defenseProfile;
        Skills = nextSkillState;
        _skillIds.Clear();
        _skillIds.UnionWith(equippedSkillIds);
        Passives.ReplaceFrom(nextPassives);
    }

    internal void ApplySkillState(
        RuntimeSkillStateSnapshot skills,
        IEnumerable<SkillDefinition> equippedSkillDefinitions)
    {
        PrepareSkillState(
            skills,
            equippedSkillDefinitions,
            out RuntimeSkillStateSnapshot nextSkillState,
            out ContentId[] equippedSkillIds,
            out BattlePassiveCollection nextPassives);

        Skills = nextSkillState;
        _skillIds.Clear();
        _skillIds.UnionWith(equippedSkillIds);
        Passives.ReplaceFrom(nextPassives);
    }

    internal void ReplaceResources(IEnumerable<RuntimeResourceSnapshot> resources)
    {
        RuntimeResourceSnapshot[] replacements =
            (resources ?? throw new ArgumentNullException(nameof(resources))).ToArray();
        if (replacements.Any(resource => resource is null))
        {
            throw new ArgumentException("Replacement resources cannot contain null entries.", nameof(resources));
        }

        BattleResourceState[] nextResources = replacements
            .Select(resource => new BattleResourceState(
                resource.ResourceId,
                resource.Current,
                resource.Maximum))
            .ToArray();
        var replacementIds = nextResources.Select(resource => resource.Id).ToHashSet();
        if (!replacementIds.Contains(VitalResourceId) || replacementIds.Count != replacements.Length)
        {
            throw new ArgumentException("Replacement resources must be unique and contain the vital resource.", nameof(resources));
        }

        _resources.Clear();
        foreach (BattleResourceState resource in nextResources)
        {
            _resources.Add(resource.Id, resource);
        }
    }

    private static void PrepareSkillState(
        RuntimeSkillStateSnapshot skills,
        IEnumerable<SkillDefinition> equippedSkillDefinitions,
        out RuntimeSkillStateSnapshot nextSkillState,
        out ContentId[] equippedSkillIds,
        out BattlePassiveCollection nextPassives)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ContentId[] learnedSkillIds = skills.LearnedSkillIds.ToArray();
        equippedSkillIds = skills.EquippedSkillIds.ToArray();
        RuntimePendingSkillChoiceSnapshot[] pendingChoices =
            skills.PendingChoices.ToArray();
        RequireValid(learnedSkillIds, nameof(skills));
        RequireValid(equippedSkillIds, nameof(skills));
        if (learnedSkillIds.Distinct().Count() != learnedSkillIds.Length ||
            equippedSkillIds.Distinct().Count() != equippedSkillIds.Length ||
            equippedSkillIds.Except(learnedSkillIds).Any() ||
            pendingChoices.Any(choice => !choice.Token.IsValid || !choice.SkillId.IsValid) ||
            pendingChoices.Select(choice => choice.Token).Distinct().Count() !=
            pendingChoices.Length ||
            pendingChoices.Select(choice => choice.SkillId).Distinct().Count() !=
            pendingChoices.Length ||
            pendingChoices.Any(choice => learnedSkillIds.Contains(choice.SkillId)))
        {
            throw new ArgumentException(
                "Skill state must contain unique learned and equipped skills, equipped skills " +
                "must be learned, and pending choices must be unique and unlearned.",
                nameof(skills));
        }

        SkillDefinition[] definitions =
            (equippedSkillDefinitions ??
             throw new ArgumentNullException(nameof(equippedSkillDefinitions)))
            .ToArray();
        if (definitions.Any(definition => definition is null) ||
            !definitions.Select(definition => definition.Id).SequenceEqual(equippedSkillIds))
        {
            throw new ArgumentException(
                "Equipped skill definitions must match the equipped-skill order.",
                nameof(equippedSkillDefinitions));
        }

        nextSkillState = new RuntimeSkillStateSnapshot(
            learnedSkillIds,
            equippedSkillIds,
            pendingChoices,
            skills.Revision);
        nextPassives = new BattlePassiveCollection(
            definitions.Where(definition => definition.Activation == SkillActivation.Passive));
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

        _affinityBreaks.Clear();
        foreach (RuntimeAffinityBreakSnapshot affinityBreak in status.AffinityBreaks)
        {
            _affinityBreaks.Add(
                affinityBreak.Element,
                new BattleAffinityBreakState(affinityBreak.Duration));
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
            _analysis.Select(pair => new RuntimeAnalysisSnapshot(pair.Key, pair.Value)),
            _affinityBreaks.Select(pair => new RuntimeAffinityBreakSnapshot(
                pair.Key,
                pair.Value.Duration)));

    private static void ReplaceDictionary<TKey, TValue>(
        IDictionary<TKey, TValue> destination,
        IEnumerable<KeyValuePair<TKey, TValue>> source)
        where TKey : notnull
    {
        destination.Clear();
        foreach ((TKey key, TValue value) in source)
        {
            destination.Add(key, value);
        }
    }

    private void RemoveStatStages(Func<int, bool> predicate)
    {
        foreach (ContentId id in _statStages.Where(pair => predicate(pair.Value.Stage)).Select(pair => pair.Key).ToArray())
        {
            _statStages.Remove(id);
        }
    }

    private IReadOnlyList<BattleDurationTickResult> ExpireDurations(
        Func<DurationDefinition, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var results = new List<BattleDurationTickResult>();

        foreach ((ContentId id, ActiveAilmentState state) in _ailments.ToArray())
        {
            if (!predicate(state.Duration))
            {
                continue;
            }

            _ailments.Remove(id);
            results.Add(Expired(id, state.Duration, BattleDurationStateKind.Ailment));
        }

        foreach ((ContentId id, BattleStatStageState state) in _statStages.ToArray())
        {
            if (state.Duration is null || !predicate(state.Duration))
            {
                continue;
            }

            _statStages.Remove(id);
            results.Add(Expired(id, state.Duration, BattleDurationStateKind.StatStage));
        }

        foreach ((ChargeKind kind, BattleChargeState state) in _charges.ToArray())
        {
            if (state.Duration is null || !predicate(state.Duration))
            {
                continue;
            }

            _charges.Remove(kind);
            results.Add(Expired(
                ContentId.Parse("charge_" + kind.ToString().ToLowerInvariant()),
                state.Duration,
                BattleDurationStateKind.Charge));
        }

        foreach ((ShieldKind kind, BattleShieldState state) in _shields.ToArray())
        {
            if (state.Duration is null || !predicate(state.Duration))
            {
                continue;
            }

            _shields.Remove(kind);
            results.Add(Expired(
                ContentId.Parse("shield_" + kind.ToString().ToLowerInvariant()),
                state.Duration,
                BattleDurationStateKind.Shield));
        }

        foreach ((DamageElement element, BattleAffinityOverrideState state) in _affinityOverrides.ToArray())
        {
            if (!predicate(state.Duration))
            {
                continue;
            }

            _affinityOverrides.Remove(element);
            results.Add(Expired(
                ContentId.Parse("affinity_override_" + element.ToString().ToLowerInvariant()),
                state.Duration,
                BattleDurationStateKind.AffinityOverride));
        }

        foreach ((DamageElement element, BattleAffinityBreakState state) in _affinityBreaks.ToArray())
        {
            if (!predicate(state.Duration))
            {
                continue;
            }

            _affinityBreaks.Remove(element);
            results.Add(Expired(
                ContentId.Parse("affinity_break_" + element.ToString().ToLowerInvariant()),
                state.Duration,
                BattleDurationStateKind.AffinityBreak));
        }

        foreach ((ContentId id, BattleOtherStatusState state) in _otherStatuses.ToArray())
        {
            if (!predicate(state.Duration))
            {
                continue;
            }

            _otherStatuses.Remove(id);
            results.Add(Expired(id, state.Duration, BattleDurationStateKind.OtherStatus));
        }

        return Array.AsReadOnly(results.ToArray());
    }

    private static BattleDurationTickResult Expired(
        ContentId id,
        DurationDefinition duration,
        BattleDurationStateKind stateKind) =>
        new(id, duration, CurrentDuration: null, Expired: true, StateKind: stateKind);

    private static bool TryTickDuration(
        DurationDefinition duration,
        ContentId eventId,
        bool isDeployed,
        out DurationDefinition? current,
        out bool expired)
    {
        current = duration;
        expired = false;
        if (duration is not TurnDurationDefinition turns ||
            turns.TickEventId != eventId ||
            turns.SuspendWhileReserve && !isDeployed)
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

    private static void RequireValid(RuntimeInstanceId id, string parameterName)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("Runtime instance ID cannot be empty.", parameterName);
        }
    }

    private static void RequireValid(ContentId id, string parameterName)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("Content ID cannot be empty.", parameterName);
        }
    }

    private static void RequireValid(IEnumerable<ContentId> ids, string parameterName)
    {
        if (ids.Any(id => !id.IsValid))
        {
            throw new ArgumentException("Content ID collections cannot contain an empty ID.", parameterName);
        }
    }

    private static void RequireValid(RuntimeActorReferenceSnapshot? reference, string parameterName)
    {
        if (reference is null)
        {
            return;
        }

        RequireValid(reference.InstanceId, parameterName);
        RequireValid(reference.EntityDefinitionId, parameterName);
    }

    private static void RequireValid(
        IEnumerable<RuntimeActorReferenceSnapshot> references,
        string parameterName)
    {
        foreach (RuntimeActorReferenceSnapshot reference in references)
        {
            RequireValid(reference, parameterName);
        }
    }

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
