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
    StatusLifetimeDefinition Lifetime)
{
    public DurationDefinition Duration => Lifetime.Expiration;
}

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
public sealed record BattleChargeState(decimal Multiplier, StatusLifetimeDefinition Lifetime)
{
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record BattleShieldState(StatusLifetimeDefinition Lifetime)
{
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record BattleAffinityBreakState(StatusLifetimeDefinition Lifetime)
{
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record BattleAffinityOverrideState(ElementalAffinity Affinity, StatusLifetimeDefinition Lifetime)
{
    public DurationDefinition Duration => Lifetime.Expiration;
}

public sealed record BattleOtherStatusState(StatusLifetimeDefinition Lifetime)
{
    public DurationDefinition Duration => Lifetime.Expiration;
}

internal static class RuntimeStatusLifetimeDomain
{
    public static void RequireValid(StatusLifetimeDefinition lifetime, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(lifetime, parameterName);
        string? error = lifetime.Expiration switch
        {
            InstantDurationDefinition => null,
            TurnDurationDefinition turns when turns.Value <= 0 =>
                "Turn duration must contain at least one remaining tick.",
            TurnDurationDefinition turns when !turns.TickEventId.IsValid =>
                "Turn duration tick event ID cannot be empty.",
            TurnDurationDefinition => null,
            PhaseDurationDefinition phase when !phase.PhaseId.IsValid =>
                "Phase duration ID cannot be empty.",
            PhaseDurationDefinition => null,
            BattleDurationDefinition => null,
            PermanentDurationDefinition => null,
            _ => "Duration kind is not supported by the runtime."
        };
        if (error is not null)
        {
            throw new ArgumentException(error, parameterName);
        }
    }

    public static bool IsValid(StatusLifetimeDefinition lifetime)
    {
        try
        {
            RequireValid(lifetime, nameof(lifetime));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

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

public sealed record BattleStatusRemovalResult(
    ContentId Id,
    BattleDurationStateKind StateKind,
    StatusRemovalCause Cause);

public sealed class RuntimeActorState
{
    private readonly Dictionary<ContentId, BattleResourceState> _resources;
    private readonly Dictionary<ContentId, ActiveAilmentState> _ailments = [];
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
    private RuntimeStatModifierStateSnapshot? _statModifierState;
    private ContentId? _chargePolicyId;
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
        IReadOnlySet<ContentId>? registeredPhaseIds = null,
        IStatModifierPolicyService? statModifierPolicy = null,
        IChargePolicyService? chargePolicy = null)
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
            ailmentDefinitions.ToDictionary(ailment => ailment.Id),
            statModifierPolicy,
            chargePolicy);
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
    /// <summary>
    /// Gets the selected policy's canonical modifier state, or <see langword="null"/>
    /// until a policy first owns this actor's modifier state.
    /// </summary>
    public RuntimeStatModifierStateSnapshot? StatModifierState => _statModifierState;
    /// <summary>
    /// Gets the selected policy state's aggregate projection for combat scaling.
    /// Mutation is owned exclusively by the selected stat-modifier policy.
    /// </summary>
    public IReadOnlyDictionary<ContentId, BattleStatStageState> StatStages =>
        new ReadOnlyDictionary<ContentId, BattleStatStageState>(ProjectStatStages());
    public IReadOnlyDictionary<ChargeKind, BattleChargeState> Charges =>
        new ReadOnlyDictionary<ChargeKind, BattleChargeState>(_charges);
    public ContentId? ChargePolicyId => _chargePolicyId;
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
    /// <summary>
    /// Reports whether the requested modifier track has a positive resolved stage.
    /// </summary>
    public bool HasBuff(ContentId modifierTrackId) =>
        StatStages.TryGetValue(modifierTrackId, out BattleStatStageState? state) && state.Stage > 0;

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

    public void ApplyAilment(AilmentDefinition definition, StatusLifetimeDefinition lifetime)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.Id.IsValid)
        {
            throw new ArgumentException("Ailment ID cannot be empty.", nameof(definition));
        }
        if (definition.ExclusivityGroupId is ContentId groupId && !groupId.IsValid)
        {
            throw new ArgumentException("Ailment exclusivity-group ID cannot be empty.", nameof(definition));
        }
        RuntimeStatusLifetimeDomain.RequireValid(lifetime, nameof(lifetime));
        if (definition.ExclusivityGroupId is ContentId exclusivityGroup)
        {
            ActiveAilmentState[] existing = _ailments.Values
                .Where(active => active.Definition.ExclusivityGroupId == exclusivityGroup)
                .ToArray();
            if (existing.Any(active =>
                    active.Definition.Id != definition.Id &&
                    !active.Lifetime.Allows(StatusRemovalCause.ExclusivityReplacement)))
            {
                throw new InvalidOperationException(
                    $"A protected ailment in exclusivity group '{exclusivityGroup}' cannot be replaced.");
            }

            foreach (ContentId existingId in existing
                         .Where(active => active.Definition.Id != definition.Id)
                         .Select(active => active.Definition.Id))
            {
                _ailments.Remove(existingId);
            }
        }

        _ailments[definition.Id] = new ActiveAilmentState(definition, lifetime);
    }

    public IReadOnlyList<ContentId> RemoveAilments(
        StatusRemovalCause cause,
        Func<ActiveAilmentState, bool> predicate)
    {
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause));
        }
        ArgumentNullException.ThrowIfNull(predicate);
        ContentId[] removed = _ailments
            .Where(pair => pair.Value.Lifetime.Allows(cause) && predicate(pair.Value))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (ContentId id in removed)
        {
            _ailments.Remove(id);
        }

        return Array.AsReadOnly(removed);
    }

    internal RuntimeStatModifierStateSnapshot ResolveStatModifierState(
        IStatModifierPolicyService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        RuntimeStatModifierStateSnapshot candidate = _statModifierState ??
            new RuntimeStatModifierStateSnapshot(service.PolicyId);
        StatModifierValidationResult validation = service.ValidateState(candidate);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Actor '{InstanceId}' modifier state is incompatible with selected policy " +
                $"'{service.PolicyId}': {string.Join("; ", validation.Diagnostics.Select(value => value.Message))}");
        }

        return candidate;
    }

    internal void ReplaceStatModifierState(
        IStatModifierPolicyService service,
        RuntimeStatModifierStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(state);
        StatModifierValidationResult validation = service.ValidateState(state);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"Replacement modifier state is incompatible with selected policy '{service.PolicyId}': " +
                string.Join("; ", validation.Diagnostics.Select(value => value.Message)),
                nameof(state));
        }

        _statModifierState = state;
    }

    internal RuntimeChargeStateSnapshot? CaptureChargeState()
    {
        return _chargePolicyId is ContentId policyId
            ? new RuntimeChargeStateSnapshot(
            policyId,
             _charges.Select(pair => new RuntimeChargeSnapshot(
                 pair.Key,
                 pair.Value.Multiplier,
                 pair.Value.Lifetime)))
            : null;
    }

    internal void AddCharge(ContentId policyId, ChargeKind kind, BattleChargeState state)
    {
        if (!policyId.IsValid)
        {
            throw new ArgumentException("Charge policy ID cannot be empty.", nameof(policyId));
        }
        EnumDomain.RequireDefined(kind, nameof(kind));
        ArgumentNullException.ThrowIfNull(state);
        if (_chargePolicyId is ContentId active && active != policyId)
        {
            throw new InvalidOperationException(
                $"Actor '{InstanceId}' charge state belongs to policy '{active}', not '{policyId}'.");
        }
        if (_charges.ContainsKey(kind))
        {
            throw new InvalidOperationException($"Charge '{kind}' is already in effect.");
        }

        _chargePolicyId = policyId;
        _charges.Add(kind, state);
    }

    internal bool RemoveCharge(
        ContentId policyId,
        ChargeKind kind,
        StatusRemovalCause cause)
    {
        if (_chargePolicyId is ContentId active && active != policyId)
        {
            throw new InvalidOperationException(
                $"Actor '{InstanceId}' charge state belongs to policy '{active}', not '{policyId}'.");
        }

        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause));
        }

        return _charges.TryGetValue(kind, out BattleChargeState? state) &&
            state.Lifetime.Allows(cause) &&
            _charges.Remove(kind);
    }

    public void GrantShield(ShieldKind kind, StatusLifetimeDefinition lifetime)
    {
        EnumDomain.RequireDefined(kind, nameof(kind));
        RuntimeStatusLifetimeDomain.RequireValid(lifetime, nameof(lifetime));
        _shields[kind] = new BattleShieldState(lifetime);
    }

    public void BreakAffinity(DamageElement element, StatusLifetimeDefinition lifetime)
    {
        EnumDomain.RequireDefined(element, nameof(element));
        RuntimeStatusLifetimeDomain.RequireValid(lifetime, nameof(lifetime));
        if (element == DamageElement.Almighty)
        {
            throw new ArgumentException("Almighty cannot receive an affinity Break.", nameof(element));
        }

        _affinityBreaks[element] = new BattleAffinityBreakState(lifetime);
    }

    public void SetGuarding(bool isGuarding) => IsGuarding = isGuarding;

    public void OverrideAffinity(
        DamageElement element,
        ElementalAffinity affinity,
        StatusLifetimeDefinition lifetime)
    {
        EnumDomain.RequireDefined(element, nameof(element));
        EnumDomain.RequireDefined(affinity, nameof(affinity));
        RuntimeStatusLifetimeDomain.RequireValid(lifetime, nameof(lifetime));
        _affinityOverrides[element] = new BattleAffinityOverrideState(
            affinity,
            lifetime);
    }

    public void AddOtherStatus(ContentId statusId) =>
        AddOtherStatus(statusId, StandardStatusLifetimes.Persistent);

    public void AddOtherStatus(
        ContentId statusId,
        StatusLifetimeDefinition lifetime)
    {
        if (!statusId.IsValid)
        {
            throw new ArgumentException("Other-status ID cannot be empty.", nameof(statusId));
        }
        RuntimeStatusLifetimeDomain.RequireValid(lifetime, nameof(lifetime));
        _otherStatuses[statusId] = new BattleOtherStatusState(lifetime);
    }

    public IReadOnlyList<BattleDurationTickResult> TickAilmentDurations(ContentId eventId) =>
        TickAilmentDurations(eventId, advanceReserveState: false);

    internal IReadOnlyList<BattleDurationTickResult> TickAilmentDurations(
        ContentId eventId,
        bool advanceReserveState)
    {
        var results = new List<BattleDurationTickResult>();
        foreach ((ContentId id, ActiveAilmentState state) in _ailments.ToArray())
        {
            if (!TryTickDuration(
                    state.Duration,
                    eventId,
                    IsDeployed,
                    advanceReserveState,
                    out DurationDefinition? current,
                    out bool expired))
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
                _ailments[id] = state with
                {
                    Lifetime = state.Lifetime.WithExpiration(current!)
                };
            }
        }

        return Array.AsReadOnly(results.ToArray());
    }

    public IReadOnlyList<BattleDurationTickResult> TickTimedStatuses(ContentId eventId) =>
        TickTimedStatuses(eventId, advanceReserveState: false);

    internal IReadOnlyList<BattleDurationTickResult> TickTimedStatuses(
        ContentId eventId,
        bool advanceReserveState)
    {
        var results = new List<BattleDurationTickResult>();

        foreach ((ChargeKind kind, BattleChargeState state) in _charges.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, advanceReserveState,
                    out DurationDefinition? current, out bool expired))
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
                _charges[kind] = state with
                {
                    Lifetime = state.Lifetime.WithExpiration(current!)
                };
            }
        }

        foreach ((ShieldKind kind, BattleShieldState state) in _shields.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, advanceReserveState,
                    out DurationDefinition? current, out bool expired))
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
                _shields[kind] = state with
                {
                    Lifetime = state.Lifetime.WithExpiration(current!)
                };
            }
        }

        foreach ((DamageElement element, BattleAffinityOverrideState state) in _affinityOverrides.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, advanceReserveState,
                    out DurationDefinition? current, out bool expired))
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
                _affinityOverrides[element] = state with
                {
                    Lifetime = new StatusLifetimeDefinition(current, state.Lifetime.RemovalProfile)
                };
            }
        }

        foreach ((DamageElement element, BattleAffinityBreakState state) in _affinityBreaks.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, advanceReserveState,
                    out DurationDefinition? current, out bool expired))
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
                _affinityBreaks[element] = state with
                {
                    Lifetime = new StatusLifetimeDefinition(current, state.Lifetime.RemovalProfile)
                };
            }
        }

        foreach ((ContentId id, BattleOtherStatusState state) in _otherStatuses.ToArray())
        {
            if (!TryTickDuration(state.Duration, eventId, IsDeployed, advanceReserveState,
                    out DurationDefinition? current, out bool expired))
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
                _otherStatuses[id] = state with
                {
                    Lifetime = new StatusLifetimeDefinition(current, state.Lifetime.RemovalProfile)
                };
            }
        }

        return Array.AsReadOnly(results.ToArray());
    }

    public IReadOnlyList<BattleDurationTickResult> ExpireInstantDurations() =>
        ExpireDurations(duration => duration is InstantDurationDefinition);

    public IReadOnlyList<BattleDurationTickResult> ExpirePhaseDurations(ContentId phaseId) =>
        ExpireDurations(duration =>
            duration is PhaseDurationDefinition phase && phase.PhaseId == phaseId);

    public IReadOnlyList<BattleDurationTickResult> ExpireBattleDurations() =>
        ExpireDurations(duration => duration is BattleDurationDefinition);

    internal IReadOnlyList<BattleStatusRemovalResult> RemoveStatuses(StatusRemovalCause cause)
    {
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause));
        }

        var removed = new List<BattleStatusRemovalResult>();
        foreach (ContentId id in _ailments
                     .Where(pair => pair.Value.Lifetime.Allows(cause))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _ailments.Remove(id);
            removed.Add(new BattleStatusRemovalResult(id, BattleDurationStateKind.Ailment, cause));
        }

        foreach (ChargeKind kind in _charges
                     .Where(pair => pair.Value.Lifetime.Allows(cause))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _charges.Remove(kind);
            removed.Add(new BattleStatusRemovalResult(
                ContentId.Parse("charge_" + kind.ToString().ToLowerInvariant()),
                BattleDurationStateKind.Charge,
                cause));
        }

        foreach (ShieldKind kind in _shields
                     .Where(pair => pair.Value.Lifetime.Allows(cause))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _shields.Remove(kind);
            removed.Add(new BattleStatusRemovalResult(
                ContentId.Parse("shield_" + kind.ToString().ToLowerInvariant()),
                BattleDurationStateKind.Shield,
                cause));
        }

        foreach (DamageElement element in _affinityOverrides
                     .Where(pair => pair.Value.Lifetime.Allows(cause))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _affinityOverrides.Remove(element);
            removed.Add(new BattleStatusRemovalResult(
                ContentId.Parse("affinity_override_" + element.ToString().ToLowerInvariant()),
                BattleDurationStateKind.AffinityOverride,
                cause));
        }

        foreach (DamageElement element in _affinityBreaks
                     .Where(pair => pair.Value.Lifetime.Allows(cause))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _affinityBreaks.Remove(element);
            removed.Add(new BattleStatusRemovalResult(
                ContentId.Parse("affinity_break_" + element.ToString().ToLowerInvariant()),
                BattleDurationStateKind.AffinityBreak,
                cause));
        }

        foreach (ContentId id in _otherStatuses
                     .Where(pair => pair.Value.Lifetime.Allows(cause))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _otherStatuses.Remove(id);
            removed.Add(new BattleStatusRemovalResult(id, BattleDurationStateKind.OtherStatus, cause));
        }

        return Array.AsReadOnly(removed.ToArray());
    }

    internal IReadOnlyList<BattleStatusRemovalResult> RemoveNonModifierStatuses(
        IReadOnlySet<StatusEffectKind> kinds,
        IEnumerable<ContentId> statusIds,
        StatusRemovalCause cause)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(statusIds);
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause));
        }

        var removed = new List<BattleStatusRemovalResult>();
        if (kinds.Contains(StatusEffectKind.Charge))
        {
            foreach (ChargeKind kind in _charges
                         .Where(pair => pair.Value.Lifetime.Allows(cause))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _charges.Remove(kind);
                removed.Add(new BattleStatusRemovalResult(
                    ContentId.Parse("charge_" + kind.ToString().ToLowerInvariant()),
                    BattleDurationStateKind.Charge,
                    cause));
            }
        }
        if (kinds.Contains(StatusEffectKind.Shield))
        {
            foreach (ShieldKind kind in _shields
                         .Where(pair => pair.Value.Lifetime.Allows(cause))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _shields.Remove(kind);
                removed.Add(new BattleStatusRemovalResult(
                    ContentId.Parse("shield_" + kind.ToString().ToLowerInvariant()),
                    BattleDurationStateKind.Shield,
                    cause));
            }
        }
        if (kinds.Contains(StatusEffectKind.AffinityBreak))
        {
            foreach (DamageElement element in _affinityBreaks
                         .Where(pair => pair.Value.Lifetime.Allows(cause))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _affinityBreaks.Remove(element);
                removed.Add(new BattleStatusRemovalResult(
                    ContentId.Parse("affinity_break_" + element.ToString().ToLowerInvariant()),
                    BattleDurationStateKind.AffinityBreak,
                    cause));
            }
        }
        if (kinds.Contains(StatusEffectKind.AffinityOverride))
        {
            foreach (DamageElement element in _affinityOverrides
                         .Where(pair => pair.Value.Lifetime.Allows(cause))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _affinityOverrides.Remove(element);
                removed.Add(new BattleStatusRemovalResult(
                    ContentId.Parse("affinity_override_" + element.ToString().ToLowerInvariant()),
                    BattleDurationStateKind.AffinityOverride,
                    cause));
            }
        }
        if (kinds.Contains(StatusEffectKind.Other))
        {
            foreach (ContentId statusId in statusIds)
            {
                if (_otherStatuses.TryGetValue(statusId, out BattleOtherStatusState? state) &&
                    state.Lifetime.Allows(cause))
                {
                    _otherStatuses.Remove(statusId);
                    removed.Add(new BattleStatusRemovalResult(
                        statusId,
                        BattleDurationStateKind.OtherStatus,
                        cause));
                }
            }
        }

        return Array.AsReadOnly(removed.ToArray());
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
        RuntimeStatModifierStateSnapshot? statModifierState = source._statModifierState;
        ContentId? chargePolicyId = source._chargePolicyId;
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
        _statModifierState = statModifierState;
        _chargePolicyId = chargePolicyId;
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
        PreservePassiveRuntimeState(Passives, nextPassives);

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
        PreservePassiveRuntimeState(Passives, nextPassives);

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

    private static void PreservePassiveRuntimeState(
        BattlePassiveCollection current,
        BattlePassiveCollection replacement)
    {
        HashSet<ContentId> replacementSkillIds = replacement.Entries
            .Select(entry => entry.Skill.Id)
            .ToHashSet();
        replacement.RestoreStates(current.CaptureStates().Where(state =>
            replacementSkillIds.Contains(state.SkillId)));
        replacement.RestoreActivations(current.CaptureActivations().Where(activation =>
            replacementSkillIds.Contains(activation.SkillId)));
    }

    internal void RestoreBattleStatus(
        RuntimeBattleStatusSnapshot status,
        IReadOnlyDictionary<ContentId, AilmentDefinition> ailments,
        IStatModifierPolicyService? statModifierPolicy = null,
        IChargePolicyService? chargePolicy = null)
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
                    ailment.Lifetime));
        }

        _otherStatuses.Clear();
        foreach (RuntimeTimedStateSnapshot other in status.Statuses)
        {
            _otherStatuses.Add(other.Id, new BattleOtherStatusState(other.Lifetime));
        }

        if (status.StatModifiers is RuntimeStatModifierStateSnapshot modifiers)
        {
            if (statModifierPolicy is null)
            {
                throw new ArgumentException(
                    "Restoring retained stat modifiers requires the matching policy service.",
                    nameof(statModifierPolicy));
            }

            StatModifierValidationResult validation = statModifierPolicy.ValidateState(modifiers);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"Retained stat modifiers are incompatible with policy '{statModifierPolicy.PolicyId}': " +
                    string.Join("; ", validation.Diagnostics.Select(value => value.Message)),
                    nameof(status));
            }
        }
        _statModifierState = status.StatModifiers;

        if (status.ChargeState is RuntimeChargeStateSnapshot chargeState)
        {
            if (chargePolicy is null)
            {
                throw new ArgumentException(
                    "Restoring retained charge state requires the matching charge policy service.",
                    nameof(chargePolicy));
            }

            ChargePolicyValidationResult validation = chargePolicy.ValidateState(chargeState);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"Retained charge state is incompatible with policy '{chargePolicy.PolicyId}': " +
                    string.Join("; ", validation.Diagnostics.Select(value => value.Message)),
                    nameof(status));
            }
        }

        _charges.Clear();
        _chargePolicyId = status.ChargeState?.PolicyId;
        foreach (RuntimeChargeSnapshot charge in status.Charges)
        {
            _charges.Add(charge.Kind, new BattleChargeState(charge.Multiplier, charge.Lifetime));
        }

        _shields.Clear();
        foreach (RuntimeShieldSnapshot shield in status.Shields)
        {
            _shields.Add(shield.Kind, new BattleShieldState(shield.Lifetime));
        }

        _affinityBreaks.Clear();
        foreach (RuntimeAffinityBreakSnapshot affinityBreak in status.AffinityBreaks)
        {
            _affinityBreaks.Add(
                affinityBreak.Element,
                new BattleAffinityBreakState(affinityBreak.Lifetime));
        }

        _affinityOverrides.Clear();
        foreach (RuntimeAffinityOverrideSnapshot affinity in status.AffinityOverrides)
        {
            _affinityOverrides.Add(
                affinity.Element,
                new BattleAffinityOverrideState(affinity.Affinity, affinity.Lifetime));
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
                pair.Value.Lifetime)),
            _otherStatuses.Select(pair => new RuntimeTimedStateSnapshot(
                pair.Key,
                pair.Value.Lifetime)),
            _statModifierState,
            _chargePolicyId is ContentId chargePolicyId
                ? new RuntimeChargeStateSnapshot(
                    chargePolicyId,
                    _charges.Select(pair => new RuntimeChargeSnapshot(
                        pair.Key,
                        pair.Value.Multiplier,
                        pair.Value.Lifetime)))
                : null,
            _shields.Select(pair => new RuntimeShieldSnapshot(pair.Key, pair.Value.Lifetime)),
            _affinityOverrides.Select(pair => new RuntimeAffinityOverrideSnapshot(
                pair.Key,
                pair.Value.Affinity,
                pair.Value.Lifetime)),
            IsGuarding,
            _analysis.Select(pair => new RuntimeAnalysisSnapshot(pair.Key, pair.Value)),
            _affinityBreaks.Select(pair => new RuntimeAffinityBreakSnapshot(
                pair.Key,
                pair.Value.Lifetime)));

    private Dictionary<ContentId, BattleStatStageState> ProjectStatStages()
    {
        if (_statModifierState is null)
        {
            return [];
        }

        return _statModifierState.Tracks.ToDictionary(
            track => track.ModifierTrackId,
            track => new BattleStatStageState(
                track.ResolvedStage,
                track.Contributions.Count == 1
                    ? track.Contributions[0].Duration
                    : null));
    }

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

    private IReadOnlyList<BattleDurationTickResult> ExpireDurations(
        Func<DurationDefinition, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var results = new List<BattleDurationTickResult>();

        foreach ((ContentId id, ActiveAilmentState state) in _ailments.ToArray())
        {
            if (!predicate(state.Duration) || !state.Lifetime.Allows(StatusRemovalCause.DurationExpired))
            {
                continue;
            }

            _ailments.Remove(id);
            results.Add(Expired(id, state.Duration, BattleDurationStateKind.Ailment));
        }

        foreach ((ChargeKind kind, BattleChargeState state) in _charges.ToArray())
        {
            if (!predicate(state.Duration) || !state.Lifetime.Allows(StatusRemovalCause.DurationExpired))
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
            if (!predicate(state.Duration) || !state.Lifetime.Allows(StatusRemovalCause.DurationExpired))
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
            if (!predicate(state.Duration) || !state.Lifetime.Allows(StatusRemovalCause.DurationExpired))
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
            if (!predicate(state.Duration) || !state.Lifetime.Allows(StatusRemovalCause.DurationExpired))
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
            if (!predicate(state.Duration) || !state.Lifetime.Allows(StatusRemovalCause.DurationExpired))
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
        bool advanceReserveState,
        out DurationDefinition? current,
        out bool expired)
    {
        current = duration;
        expired = false;
        if (duration is not TurnDurationDefinition turns ||
            turns.TickEventId != eventId ||
            !isDeployed && (turns.SuspendWhileReserve || !advanceReserveState))
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
