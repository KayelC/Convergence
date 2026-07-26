using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Battle;
using Convergence.Runtime;

namespace Convergence.Execution;

public sealed record BattlePassiveEntry(SkillDefinition Skill, bool IsEnabled);

public sealed class BattlePassiveCollection
{
    private readonly List<BattlePassiveEntry> _entries = [];
    private readonly Dictionary<PassiveActivationKey, int> _activationCounts = [];

    public BattlePassiveCollection(IEnumerable<SkillDefinition>? skills = null)
    {
        foreach (SkillDefinition skill in skills ?? [])
        {
            Add(skill);
        }
    }

    public IReadOnlyList<BattlePassiveEntry> Entries =>
        Array.AsReadOnly(_entries.ToArray());

    public void Add(SkillDefinition skill, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (skill.Activation != SkillActivation.Passive)
        {
            throw new ArgumentException("Only passive skills may be added to a battle passive collection.", nameof(skill));
        }

        if (_entries.Any(entry => entry.Skill.Id == skill.Id))
        {
            throw new InvalidOperationException($"Passive skill '{skill.Id}' is already loaded.");
        }

        _entries.Add(new BattlePassiveEntry(skill, enabled));
    }

    public bool Remove(ContentId skillId)
    {
        int index = _entries.FindIndex(entry => entry.Skill.Id == skillId);
        if (index < 0)
        {
            return false;
        }

        _entries.RemoveAt(index);
        foreach (PassiveActivationKey key in _activationCounts.Keys
                     .Where(key => key.SkillId == skillId)
                     .ToArray())
        {
            _activationCounts.Remove(key);
        }

        return true;
    }

    public bool Enable(ContentId skillId) => SetEnabled(skillId, true);

    public bool Disable(ContentId skillId) => SetEnabled(skillId, false);

    public void ResetBattleActivations() => _activationCounts.Clear();

    internal IReadOnlyList<RuntimePassiveSkillStateSnapshot> CaptureStates() =>
        Array.AsReadOnly(_entries
            .Select(entry => new RuntimePassiveSkillStateSnapshot(entry.Skill.Id, entry.IsEnabled))
            .ToArray());

    internal IReadOnlyList<RuntimePassiveActivationSnapshot> CaptureActivations() =>
        Array.AsReadOnly(_activationCounts
            .OrderBy(pair => pair.Key.SkillId.ToString(), StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.EventId.ToString(), StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.TriggerIndex)
            .ThenBy(pair => pair.Key.TargetInstanceId?.ToString(), StringComparer.Ordinal)
            .Select(pair => new RuntimePassiveActivationSnapshot(
                pair.Key.SkillId,
                pair.Key.EventId,
                pair.Key.TriggerIndex,
                pair.Value,
                pair.Key.TargetInstanceId))
            .ToArray());

    internal void RestoreStates(IEnumerable<RuntimePassiveSkillStateSnapshot> states)
    {
        var restoredSkillIds = new HashSet<ContentId>();
        foreach (RuntimePassiveSkillStateSnapshot state in
                 states ?? throw new ArgumentNullException(nameof(states)))
        {
            if (!restoredSkillIds.Add(state.SkillId))
            {
                throw new ArgumentException(
                    $"Passive state contains duplicate skill '{state.SkillId}'.",
                    nameof(states));
            }

            if (!SetEnabled(state.SkillId, state.IsEnabled))
            {
                throw new ArgumentException(
                    $"Passive state references unloaded skill '{state.SkillId}'.",
                    nameof(states));
            }
        }
    }

    internal void RestoreActivations(IEnumerable<RuntimePassiveActivationSnapshot> activations)
    {
        RuntimePassiveActivationSnapshot[] snapshot =
            (activations ?? throw new ArgumentNullException(nameof(activations))).ToArray();
        var restored = new Dictionary<PassiveActivationKey, int>();
        foreach (RuntimePassiveActivationSnapshot activation in snapshot)
        {
            BattlePassiveEntry? entry =
                _entries.FirstOrDefault(entry => entry.Skill.Id == activation.SkillId);
            if (entry is null)
            {
                throw new ArgumentException(
                    $"Passive activation references unloaded skill '{activation.SkillId}'.",
                    nameof(activations));
            }

            if (activation.TriggerIndex >= entry.Skill.Triggers.Count)
            {
                throw new ArgumentException(
                    $"Passive activation references trigger index {activation.TriggerIndex}, " +
                    $"but skill '{activation.SkillId}' defines {entry.Skill.Triggers.Count} triggers.",
                    nameof(activations));
            }

            ContentId authoredEventId = entry.Skill.Triggers[activation.TriggerIndex].EventId;
            if (activation.EventId != authoredEventId)
            {
                throw new ArgumentException(
                    $"Passive activation event '{activation.EventId}' does not match authored " +
                    $"event '{authoredEventId}' for skill '{activation.SkillId}' trigger " +
                    $"{activation.TriggerIndex}.",
                    nameof(activations));
            }

            var key = new PassiveActivationKey(
                activation.SkillId,
                activation.TriggerIndex,
                activation.EventId,
                activation.TargetInstanceId);
            if (!restored.TryAdd(key, activation.ActivationCount))
            {
                throw new ArgumentException(
                    $"Passive activation '{activation.SkillId}/{activation.EventId}/" +
                    $"{activation.TriggerIndex}' appears more than once.",
                    nameof(activations));
            }
        }

        _activationCounts.Clear();
        foreach ((PassiveActivationKey key, int count) in restored)
        {
            _activationCounts.Add(key, count);
        }
    }

    internal void ReplaceFrom(BattlePassiveCollection source)
    {
        ArgumentNullException.ThrowIfNull(source);

        BattlePassiveEntry[] entries = source._entries.ToArray();
        KeyValuePair<PassiveActivationKey, int>[] activations =
            source._activationCounts.ToArray();

        _entries.Clear();
        _entries.AddRange(entries);
        _activationCounts.Clear();
        foreach ((PassiveActivationKey key, int count) in activations)
        {
            _activationCounts.Add(key, count);
        }
    }

    internal IEnumerable<SkillDefinition> EnabledSkills =>
        _entries.Where(entry => entry.IsEnabled).Select(entry => entry.Skill);

    internal int GetActivationCount(
        ContentId skillId,
        int triggerIndex,
        ContentId eventId,
        RuntimeInstanceId? targetInstanceId) =>
        _activationCounts.GetValueOrDefault(
            new PassiveActivationKey(skillId, triggerIndex, eventId, targetInstanceId));

    internal void RecordActivation(
        ContentId skillId,
        int triggerIndex,
        ContentId eventId,
        RuntimeInstanceId? targetInstanceId)
    {
        var key = new PassiveActivationKey(skillId, triggerIndex, eventId, targetInstanceId);
        _activationCounts[key] = _activationCounts.GetValueOrDefault(key) + 1;
    }

    private bool SetEnabled(ContentId skillId, bool enabled)
    {
        int index = _entries.FindIndex(entry => entry.Skill.Id == skillId);
        if (index < 0)
        {
            return false;
        }

        _entries[index] = _entries[index] with { IsEnabled = enabled };
        return true;
    }

    private readonly record struct PassiveActivationKey(
        ContentId SkillId,
        int TriggerIndex,
        ContentId EventId,
        RuntimeInstanceId? TargetInstanceId);
}

public sealed record BattleConditionContext
{
    public BattleConditionContext(
        RuntimeActorState actor,
        RuntimeActorState target,
        IEnumerable<RuntimeActorState> participants,
        ContentId? battleKindId,
        ContentId? moonPhaseId,
        BattleExecutionServices services,
        IEnumerable<DamageElement>? effectElements = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Participants = Array.AsReadOnly(participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        Services = services ?? throw new ArgumentNullException(nameof(services));
        EffectElements = new ReadOnlySet<DamageElement>(effectElements ?? []);
    }

    public RuntimeActorState Actor { get; }
    public RuntimeActorState Target { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public BattleExecutionServices Services { get; }
    public IReadOnlySet<DamageElement> EffectElements { get; }
}

public sealed record RuleModifierContext(
    BattleConditionContext Conditions,
    SkillDefinition? Skill = null,
    ContentId? ResourceId = null);

public interface INumericModifierStackingPolicy
{
    decimal Resolve(decimal baseValue, IReadOnlyList<NumericRuleModifierDefinition> modifiers);
}

public sealed class AddThenMultiplyStackingPolicy : INumericModifierStackingPolicy
{
    public decimal Resolve(decimal baseValue, IReadOnlyList<NumericRuleModifierDefinition> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        decimal additive = CombatArithmetic.SaturatingSum(modifiers
            .Where(modifier => modifier.Operation == ModifierOperation.Add)
            .Select(modifier => modifier.Value));
        decimal multiplier = modifiers
            .Where(modifier => modifier.Operation == ModifierOperation.Multiply)
            .Aggregate(
                1m,
                (product, modifier) => CombatArithmetic.SaturatingMultiply(product, modifier.Value));

        return CombatArithmetic.SaturatingMultiply(
            CombatArithmetic.SaturatingAdd(baseValue, additive),
            multiplier);
    }
}

public sealed class StackingPolicyRegistry
{
    private readonly Dictionary<NumericRuleModifierType, INumericModifierStackingPolicy> _policies = [];

    public StackingPolicyRegistry Register(
        NumericRuleModifierType modifierType,
        INumericModifierStackingPolicy policy)
    {
        _policies[modifierType] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public INumericModifierStackingPolicy GetRequired(NumericRuleModifierType modifierType) =>
        _policies.TryGetValue(modifierType, out INumericModifierStackingPolicy? policy)
            ? policy
            : throw new InvalidOperationException($"No stacking policy is registered for '{modifierType}'.");

    public static StackingPolicyRegistry CreateDefault()
    {
        var registry = new StackingPolicyRegistry();
        foreach (NumericRuleModifierType modifierType in Enum.GetValues<NumericRuleModifierType>())
        {
            registry.Register(modifierType, new AddThenMultiplyStackingPolicy());
        }

        return registry;
    }
}

public sealed class RuleModifierRegistry
{
    private readonly HashSet<Type> _supportedTypes = [];

    public RuleModifierRegistry Register<TModifier>() where TModifier : RuleModifierDefinition
    {
        _supportedTypes.Add(typeof(TModifier));
        return this;
    }

    public bool Supports(RuleModifierDefinition modifier) => _supportedTypes.Contains(modifier.GetType());

    public static RuleModifierRegistry CreateDefault() =>
        new RuleModifierRegistry()
            .Register<NumericRuleModifierDefinition>()
            .Register<ElementalAffinityRuleModifierDefinition>()
            .Register<AilmentResistanceRuleModifierDefinition>()
            .Register<BasicAttackRuleModifierDefinition>();
}

public sealed class RuleModifierResolver
{
    private readonly RuleModifierRegistry _modifierRegistry;
    private readonly StackingPolicyRegistry _stackingPolicies;
    private readonly AffinityResolutionState _affinityResolutions = new();

    public RuleModifierResolver(
        RuleModifierRegistry? modifierRegistry = null,
        StackingPolicyRegistry? stackingPolicies = null)
    {
        _modifierRegistry = modifierRegistry ?? RuleModifierRegistry.CreateDefault();
        _stackingPolicies = stackingPolicies ?? StackingPolicyRegistry.CreateDefault();
    }

    public decimal ResolveNumeric(
        RuntimeActorState owner,
        NumericRuleModifierType modifierType,
        decimal baseValue,
        RuleModifierContext context)
    {
        IReadOnlyList<NumericRuleModifierDefinition> modifiers = GetApplicableNumericModifiers(
            owner,
            modifierType,
            context);

        return _stackingPolicies.GetRequired(modifierType).Resolve(baseValue, modifiers);
    }

    public IReadOnlyList<NumericRuleModifierDefinition> GetApplicableNumericModifiers(
        RuntimeActorState owner,
        NumericRuleModifierType modifierType,
        RuleModifierContext context)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(modifierType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifierType),
                modifierType,
                "Numeric modifier type must be defined.");
        }

        return Array.AsReadOnly(EnumerateApplicable(owner, context)
            .OfType<NumericRuleModifierDefinition>()
            .Where(modifier => modifier.ModifierType == modifierType)
            .ToArray());
    }

    public IReadOnlyList<ElementalAffinity> ResolveElementalAffinityReplacements(
        RuntimeActorState owner,
        DamageElement element,
        RuleModifierContext context)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(context);
        var key = (Environment.CurrentManagedThreadId, owner.InstanceId, element);
        if (!_affinityResolutions.TryEnter(key))
        {
            return Array.Empty<ElementalAffinity>();
        }

        try
        {
            return Array.AsReadOnly(EnumerateApplicable(owner, context)
                .OfType<ElementalAffinityRuleModifierDefinition>()
                .Where(modifier => modifier.Element == element)
                .Select(modifier => modifier.Affinity)
                .ToArray());
        }
        finally
        {
            _affinityResolutions.Exit(key);
        }
    }

    public ElementalAffinity ResolveElementalAffinity(
        RuntimeActorState owner,
        DamageElement element,
        RuleModifierContext context) =>
        owner.GetElementalAffinity(
            element,
            ResolveElementalAffinityReplacements(owner, element, context));

    public ResistanceLevel ResolveAilmentResistance(
        RuntimeActorState owner,
        ContentId ailmentId,
        ResistanceLevel baseResistance,
        RuleModifierContext context) =>
        EnumerateApplicable(owner, context)
            .OfType<AilmentResistanceRuleModifierDefinition>()
            .Where(modifier => modifier.AilmentId == ailmentId)
            .Select(modifier => modifier.Resistance)
            .Append(baseResistance)
            .MaxBy(GetAilmentResistancePrecedence);

    private IEnumerable<RuleModifierDefinition> EnumerateApplicable(
        RuntimeActorState owner,
        RuleModifierContext context)
    {
        foreach (SkillDefinition skill in owner.Passives.EnabledSkills)
        {
            foreach (RuleModifierDefinition modifier in skill.Modifiers)
            {
                if (!_modifierRegistry.Supports(modifier))
                {
                    throw new InvalidOperationException(
                        $"Rule modifier '{modifier.GetType().Name}' is not registered for runtime resolution.");
                }

                if (modifier.When is null || BattleConditionEvaluator.Evaluate(modifier.When, context.Conditions))
                {
                    yield return modifier;
                }
            }
        }
    }

    private static int GetAilmentResistancePrecedence(ResistanceLevel resistance) => resistance switch
    {
        ResistanceLevel.Immune => 3,
        ResistanceLevel.Resistant => 2,
        ResistanceLevel.Normal => 1,
        ResistanceLevel.Vulnerable => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(resistance), resistance, null)
    };

    private sealed class AffinityResolutionState
    {
        private readonly object _gate = new();
        private readonly HashSet<(int ThreadId, RuntimeInstanceId ActorId, DamageElement Element)> _active = [];

        public bool TryEnter((int ThreadId, RuntimeInstanceId ActorId, DamageElement Element) key)
        {
            lock (_gate)
            {
                return _active.Add(key);
            }
        }

        public void Exit((int ThreadId, RuntimeInstanceId ActorId, DamageElement Element) key)
        {
            lock (_gate)
            {
                _active.Remove(key);
            }
        }
    }
}

public enum PassiveActivationCountingScope
{
    PerDispatch,
    PerTarget
}

public enum PassiveOwnerEligibility
{
    DeployedOnly,
    AllParticipants
}

public sealed record PassiveEventPolicy
{
    public PassiveEventPolicy(
        bool AllowReentry = false,
        int? ActivationLimitPerBattle = null)
        : this(AllowReentry, ActivationLimitPerBattle, PassiveActivationCountingScope.PerDispatch)
    {
    }

    public PassiveEventPolicy(PassiveOwnerEligibility OwnerEligibility)
        : this(
            AllowReentry: false,
            ActivationLimitPerBattle: null,
            PassiveActivationCountingScope.PerDispatch,
            OwnerEligibility)
    {
    }

    public PassiveEventPolicy(
        bool AllowReentry,
        int? ActivationLimitPerBattle,
        PassiveActivationCountingScope ActivationCountingScope)
        : this(
            AllowReentry,
            ActivationLimitPerBattle,
            ActivationCountingScope,
            PassiveOwnerEligibility.AllParticipants)
    {
    }

    public PassiveEventPolicy(
        bool AllowReentry,
        int? ActivationLimitPerBattle,
        PassiveActivationCountingScope ActivationCountingScope,
        PassiveOwnerEligibility OwnerEligibility)
    {
        if (ActivationLimitPerBattle is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ActivationLimitPerBattle),
                "A passive activation limit must be positive when supplied.");
        }
        if (AllowReentry && ActivationLimitPerBattle is null)
        {
            throw new ArgumentException(
                "A reentrant passive event requires a finite activation limit.",
                nameof(ActivationLimitPerBattle));
        }
        if (!Enum.IsDefined(ActivationCountingScope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ActivationCountingScope),
                "Passive activation counting scope is not supported.");
        }
        if (!Enum.IsDefined(OwnerEligibility))
        {
            throw new ArgumentOutOfRangeException(
                nameof(OwnerEligibility),
                "Passive owner eligibility is not supported.");
        }

        this.AllowReentry = AllowReentry;
        this.ActivationLimitPerBattle = ActivationLimitPerBattle;
        this.ActivationCountingScope = ActivationCountingScope;
        this.OwnerEligibility = OwnerEligibility;
    }

    public bool AllowReentry { get; }
    public int? ActivationLimitPerBattle { get; }
    public PassiveActivationCountingScope ActivationCountingScope { get; }
    public PassiveOwnerEligibility OwnerEligibility { get; }

    internal bool AllowsOwner(RuntimeActorState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return OwnerEligibility switch
        {
            PassiveOwnerEligibility.DeployedOnly => owner.IsDeployed,
            PassiveOwnerEligibility.AllParticipants => true,
            _ => throw new InvalidOperationException(
                $"Passive owner eligibility '{OwnerEligibility}' is not supported.")
        };
    }

    public void Deconstruct(out bool AllowReentry, out int? ActivationLimitPerBattle)
    {
        AllowReentry = this.AllowReentry;
        ActivationLimitPerBattle = this.ActivationLimitPerBattle;
    }
}

public sealed class PassiveEventPolicyRegistry
{
    private readonly Dictionary<ContentId, PassiveEventPolicy> _policies = [];

    public PassiveEventPolicyRegistry Register(ContentId eventId, PassiveEventPolicy policy)
    {
        _policies[eventId] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public PassiveEventPolicy Resolve(ContentId eventId) =>
        _policies.TryGetValue(eventId, out PassiveEventPolicy? policy)
            ? policy
            : new PassiveEventPolicy();

    internal void RegisterIfAbsent(ContentId eventId, PassiveEventPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policies.TryAdd(eventId, policy);
    }
}

public enum PassiveTriggerOutcome
{
    Executed,
    ConditionNotMet,
    RecursionSuppressed,
    ActivationLimitReached
}

public sealed record PassiveTriggerExecutionResult
{
    private ContentId _skillId;
    private int _triggerIndex;
    private ContentId _eventId;
    private RuntimeInstanceId _targetId;
    private PassiveTriggerOutcome _outcome;
    private readonly IReadOnlyList<EffectExecutionResult> _effects =
        Array.Empty<EffectExecutionResult>();
    private readonly IReadOnlyList<BattleStatusLifecycleEvent> _completionLifecycleEvents =
        Array.Empty<BattleStatusLifecycleEvent>();

    public PassiveTriggerExecutionResult(
        ContentId SkillId,
        int TriggerIndex,
        ContentId EventId,
        RuntimeInstanceId TargetId,
        PassiveTriggerOutcome Outcome,
        IReadOnlyList<EffectExecutionResult> Effects)
        : this(
            SkillId,
            TriggerIndex,
            EventId,
            TargetId,
            Outcome,
            Effects,
            [])
    {
    }

    public PassiveTriggerExecutionResult(
        ContentId SkillId,
        int TriggerIndex,
        ContentId EventId,
        RuntimeInstanceId TargetId,
        PassiveTriggerOutcome Outcome,
        IReadOnlyList<EffectExecutionResult> Effects,
        IReadOnlyList<BattleStatusLifecycleEvent> CompletionLifecycleEvents)
    {
        this.SkillId = SkillId;
        this.TriggerIndex = TriggerIndex;
        this.EventId = EventId;
        this.TargetId = TargetId;
        this.Outcome = Outcome;
        this.Effects = Effects;
        this.CompletionLifecycleEvents = CompletionLifecycleEvents;
    }

    public ContentId SkillId
    {
        get => _skillId;
        init
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Passive skill ID must be valid.", nameof(value));
            }

            _skillId = value;
        }
    }

    public int TriggerIndex
    {
        get => _triggerIndex;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Passive trigger index cannot be negative.");
            }

            _triggerIndex = value;
        }
    }

    public ContentId EventId
    {
        get => _eventId;
        init
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Passive event ID must be valid.", nameof(value));
            }

            _eventId = value;
        }
    }

    public RuntimeInstanceId TargetId
    {
        get => _targetId;
        init
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Passive target ID must be valid.", nameof(value));
            }

            _targetId = value;
        }
    }

    public PassiveTriggerOutcome Outcome
    {
        get => _outcome;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Passive trigger outcome is not supported.");
            }

            _outcome = value;
        }
    }

    public IReadOnlyList<EffectExecutionResult> Effects
    {
        get => _effects;
        init
        {
            EffectExecutionResult[] snapshot = value?.ToArray() ?? [];
            if (snapshot.Any(effect => effect is null))
            {
                throw new ArgumentException("Passive effect results cannot contain null entries.", nameof(value));
            }

            _effects = Array.AsReadOnly(snapshot);
        }
    }

    public IReadOnlyList<BattleStatusLifecycleEvent> CompletionLifecycleEvents
    {
        get => _completionLifecycleEvents;
        init
        {
            BattleStatusLifecycleEvent[] snapshot = value?.ToArray() ?? [];
            if (snapshot.Any(@event => @event is null))
            {
                throw new ArgumentException(
                    "Passive completion lifecycle events cannot contain null entries.",
                    nameof(value));
            }

            _completionLifecycleEvents = Array.AsReadOnly(snapshot);
        }
    }

    public void Deconstruct(
        out ContentId SkillId,
        out int TriggerIndex,
        out ContentId EventId,
        out RuntimeInstanceId TargetId,
        out PassiveTriggerOutcome Outcome,
        out IReadOnlyList<EffectExecutionResult> Effects)
    {
        SkillId = this.SkillId;
        TriggerIndex = this.TriggerIndex;
        EventId = this.EventId;
        TargetId = this.TargetId;
        Outcome = this.Outcome;
        Effects = this.Effects;
    }
}

public sealed record PassiveTriggerDispatchResult
{
    private readonly IReadOnlyList<PassiveTriggerExecutionResult> _activations =
        Array.Empty<PassiveTriggerExecutionResult>();

    public PassiveTriggerDispatchResult(IReadOnlyList<PassiveTriggerExecutionResult> Activations)
    {
        this.Activations = Activations;
    }

    public IReadOnlyList<PassiveTriggerExecutionResult> Activations
    {
        get => _activations;
        init
        {
            PassiveTriggerExecutionResult[] snapshot = value?.ToArray() ?? [];
            if (snapshot.Any(activation => activation is null))
            {
                throw new ArgumentException("Passive activations cannot contain null entries.", nameof(value));
            }

            _activations = Array.AsReadOnly(snapshot);
        }
    }

    public void Deconstruct(out IReadOnlyList<PassiveTriggerExecutionResult> Activations) =>
        Activations = this.Activations;

    public static PassiveTriggerDispatchResult Empty { get; } = new([]);
}

public sealed record PassiveTriggerDispatchRequest
{
    public PassiveTriggerDispatchRequest(
        ContentId eventId,
        RuntimeActorState owner,
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<RuntimeActorState> targets,
        ContentId contextId,
        ContentId? battleKindId,
        ContentId? moonPhaseId,
        IEnumerable<StatModifierLifecycleBoundary>? activeStatModifierBoundaries = null)
    {
        EventId = eventId;
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Participants = Array.AsReadOnly(participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        Targets = Array.AsReadOnly(targets?.ToArray() ?? throw new ArgumentNullException(nameof(targets)));
        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        StatModifierLifecycleBoundary[] boundaries =
            (activeStatModifierBoundaries ?? []).ToArray();
        if (boundaries.Any(boundary => boundary is null) ||
            boundaries.Any(boundary => !boundary.EventId.IsValid || boundary.Sequence <= 0) ||
            boundaries.Select(boundary => boundary.EventId).Distinct().Count() != boundaries.Length)
        {
            throw new ArgumentException(
                "Active stat-modifier boundaries must be valid and unique by event ID.",
                nameof(activeStatModifierBoundaries));
        }

        ActiveStatModifierBoundaries = Array.AsReadOnly(boundaries);
    }

    public ContentId EventId { get; }
    public RuntimeActorState Owner { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public IReadOnlyList<RuntimeActorState> Targets { get; }
    public ContentId ContextId { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public IReadOnlyList<StatModifierLifecycleBoundary> ActiveStatModifierBoundaries { get; }
}

public interface IPassiveTriggerDispatcher
{
    PassiveTriggerDispatchResult Dispatch(
        PassiveTriggerDispatchRequest request,
        BattleExecutionServices services);
}

internal sealed class ValidatingPassiveTriggerDispatcher : IPassiveTriggerDispatcher
{
    private readonly IPassiveTriggerDispatcher _inner;

    public ValidatingPassiveTriggerDispatcher(IPassiveTriggerDispatcher inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public PassiveTriggerDispatchResult Dispatch(
        PassiveTriggerDispatchRequest request,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        RequireValidRequestGraph(request);
        if (!services.PassiveEventPolicies.Resolve(request.EventId).AllowsOwner(request.Owner))
        {
            return PassiveTriggerDispatchResult.Empty;
        }

        RuntimeActorState[] transactionActors = request.Participants
            .Concat(request.Targets)
            .Append(request.Owner)
            .Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance)
            .ToArray();
        var transaction = new RuntimeActorExecutionTransaction(request.Owner, transactionActors);
        var stagedRequest = new PassiveTriggerDispatchRequest(
            request.EventId,
            transaction.GetStaged(request.Owner),
            request.Participants.Select(transaction.GetStaged),
            request.Targets.Select(transaction.GetStaged),
            request.ContextId,
            request.BattleKindId,
            request.MoonPhaseId,
            request.ActiveStatModifierBoundaries);
        PassiveDispatchContract contract = PassiveDispatchContract.Capture(stagedRequest);

        PassiveTriggerDispatchResult result = _inner.Dispatch(stagedRequest, services)
            ?? throw new InvalidOperationException("The passive trigger dispatcher returned no result.");
        contract.RequireValid(result);
        transaction.Commit();
        return result;
    }

    private static void RequireValidRequestGraph(PassiveTriggerDispatchRequest request)
    {
        if (!request.EventId.IsValid)
        {
            throw new ArgumentException("Passive dispatch event ID must be valid.", nameof(request));
        }

        if (!request.ContextId.IsValid)
        {
            throw new ArgumentException("Passive dispatch context ID must be valid.", nameof(request));
        }

        RuntimeActorState[] participants = request.Participants.ToArray();
        if (participants.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Passive dispatch participants cannot contain null entries.",
                nameof(request));
        }

        var actorsById = new Dictionary<RuntimeInstanceId, RuntimeActorState>();
        foreach (RuntimeActorState participant in participants)
        {
            if (actorsById.TryGetValue(participant.InstanceId, out RuntimeActorState? existing) &&
                !ReferenceEquals(existing, participant))
            {
                throw new ArgumentException(
                    $"Passive dispatch actor ID '{participant.InstanceId}' belongs to multiple actor objects.",
                    nameof(request));
            }

            actorsById[participant.InstanceId] = participant;
        }

        if (!actorsById.TryGetValue(request.Owner.InstanceId, out RuntimeActorState? owner) ||
            !ReferenceEquals(owner, request.Owner))
        {
            throw new ArgumentException(
                "The passive owner must belong to the participant graph.",
                nameof(request));
        }

        foreach (RuntimeActorState target in request.Targets)
        {
            if (target is null ||
                !actorsById.TryGetValue(target.InstanceId, out RuntimeActorState? participant) ||
                !ReferenceEquals(participant, target))
            {
                throw new ArgumentException(
                    "Every passive event target must belong to the participant graph.",
                    nameof(request));
            }
        }
    }

    private sealed class PassiveDispatchContract
    {
        private readonly ContentId _eventId;
        private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _skills;
        private readonly IReadOnlySet<RuntimeInstanceId> _participantIds;
        private readonly RuntimeActorState _owner;
        private readonly IReadOnlyList<RuntimeActorState> _participants;
        private readonly IReadOnlyList<RuntimeActorState> _eventTargets;

        private PassiveDispatchContract(
            ContentId eventId,
            IReadOnlyDictionary<ContentId, SkillDefinition> skills,
            IReadOnlySet<RuntimeInstanceId> participantIds,
            RuntimeActorState owner,
            IReadOnlyList<RuntimeActorState> participants,
            IReadOnlyList<RuntimeActorState> eventTargets)
        {
            _eventId = eventId;
            _skills = skills;
            _participantIds = participantIds;
            _owner = owner;
            _participants = participants;
            _eventTargets = eventTargets;
        }

        public static PassiveDispatchContract Capture(PassiveTriggerDispatchRequest request)
        {
            var enabledSkills = new ReadOnlyDictionary<ContentId, SkillDefinition>(
                request.Owner.Passives.Entries
                    .Where(entry => entry.IsEnabled)
                    .ToDictionary(entry => entry.Skill.Id, entry => entry.Skill));
            return new PassiveDispatchContract(
                request.EventId,
                enabledSkills,
                new ReadOnlySet<RuntimeInstanceId>(
                    request.Participants.Select(participant => participant.InstanceId)),
                request.Owner,
                request.Participants,
                request.Targets);
        }

        public void RequireValid(PassiveTriggerDispatchResult result)
        {
            var activationKeys =
                new HashSet<(ContentId SkillId, int TriggerIndex, RuntimeInstanceId TargetId)>();
            foreach (PassiveTriggerExecutionResult activation in result.Activations)
            {
                if (activation.EventId != _eventId)
                {
                    throw Invalid(
                        $"reported event '{activation.EventId}' instead of requested event '{_eventId}'.");
                }

                if (!_skills.TryGetValue(activation.SkillId, out SkillDefinition? skill))
                {
                    throw Invalid(
                        $"reported passive '{activation.SkillId}', which is not enabled on the owner.");
                }

                if (activation.TriggerIndex >= skill.Triggers.Count)
                {
                    throw Invalid(
                        $"reported trigger index {activation.TriggerIndex} outside passive '{skill.Id}'.");
                }

                PassiveTriggerDefinition trigger = skill.Triggers[activation.TriggerIndex];
                if (trigger.EventId != _eventId)
                {
                    throw Invalid(
                        $"reported trigger {activation.TriggerIndex} from passive '{skill.Id}' for the wrong event.");
                }

                IReadOnlySet<RuntimeInstanceId> eligibleTargetIds =
                    new ReadOnlySet<RuntimeInstanceId>(PassiveTriggerTargetResolver.Resolve(
                        trigger.Targeting,
                        _owner,
                        _participants,
                        _eventTargets).Select(target => target.InstanceId));
                if (!_participantIds.Contains(activation.TargetId) ||
                    !eligibleTargetIds.Contains(activation.TargetId))
                {
                    throw Invalid(
                        $"reported ineligible target '{activation.TargetId}' for passive '{skill.Id}'.");
                }

                if (!activationKeys.Add(
                        (activation.SkillId, activation.TriggerIndex, activation.TargetId)))
                {
                    throw Invalid(
                        $"reported duplicate activation evidence for passive '{skill.Id}', " +
                        $"trigger {activation.TriggerIndex}, target '{activation.TargetId}'.");
                }

                RequireValidOutcomeShape(activation, trigger);
            }
        }

        private void RequireValidOutcomeShape(
            PassiveTriggerExecutionResult activation,
            PassiveTriggerDefinition trigger)
        {
            if (activation.Outcome != PassiveTriggerOutcome.Executed)
            {
                if (activation.Effects.Count > 0 ||
                    activation.CompletionLifecycleEvents.Count > 0)
                {
                    throw Invalid(
                        $"reported committed effect evidence for non-executed outcome '{activation.Outcome}'.");
                }

                return;
            }

            var effectIndexes = new HashSet<int>();
            foreach (EffectExecutionResult effect in activation.Effects)
            {
                if (effect.EffectIndex >= trigger.Effects.Count)
                {
                    throw Invalid(
                        $"reported effect index {effect.EffectIndex} outside trigger " +
                        $"{activation.TriggerIndex} of passive '{activation.SkillId}'.");
                }

                if (!effectIndexes.Add(effect.EffectIndex))
                {
                    throw Invalid(
                        $"reported duplicate effect index {effect.EffectIndex} for one passive target.");
                }

                EffectLocalId? authoredEffectId = trigger.Effects[effect.EffectIndex].EffectId;
                if (effect.EffectId != authoredEffectId)
                {
                    throw Invalid(
                        $"reported effect ID '{effect.EffectId}' instead of authored ID " +
                        $"'{authoredEffectId}' at index {effect.EffectIndex}.");
                }

                if (effect.TargetId is RuntimeInstanceId targetId &&
                    !_participantIds.Contains(targetId))
                {
                    throw Invalid(
                        $"reported effect target '{targetId}' outside the participant graph.");
                }
            }

            foreach (BattleStatusLifecycleEvent @event in
                     activation.Effects.SelectMany(effect => effect.LifecycleEvents)
                         .Concat(activation.CompletionLifecycleEvents))
            {
                if (!_participantIds.Contains(@event.ActorId) ||
                    (@event.SourceActorId is RuntimeInstanceId sourceActorId &&
                     !_participantIds.Contains(sourceActorId)))
                {
                    throw Invalid(
                        "reported lifecycle evidence for an actor outside the participant graph.");
                }
            }
        }

        private static InvalidOperationException Invalid(string detail) =>
            new($"The passive trigger dispatcher returned incoherent activation evidence: {detail}");
    }
}

public sealed class PassiveTriggerDispatcher : IPassiveTriggerDispatcher
{
    private readonly PassiveEventPolicyRegistry _policies;
    private readonly AsyncLocal<HashSet<ActiveTriggerKey>?> _activeTriggers = new();

    public PassiveTriggerDispatcher(PassiveEventPolicyRegistry policies)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
    }

    public PassiveTriggerDispatchResult Dispatch(
        PassiveTriggerDispatchRequest request,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        RuntimeActorState[] transactionActors = request.Participants
            .Concat(request.Targets)
            .Append(request.Owner)
            .Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance)
            .ToArray();
        var transaction = new RuntimeActorExecutionTransaction(request.Owner, transactionActors);
        var stagedRequest = new PassiveTriggerDispatchRequest(
            request.EventId,
            transaction.GetStaged(request.Owner),
            request.Participants.Select(transaction.GetStaged),
            request.Targets.Select(transaction.GetStaged),
            request.ContextId,
            request.BattleKindId,
            request.MoonPhaseId,
            request.ActiveStatModifierBoundaries);

        try
        {
            PassiveTriggerDispatchResult result = DispatchCore(stagedRequest, services);
            transaction.Commit();
            return result;
        }
        finally
        {
            if (_activeTriggers.Value is { Count: 0 })
            {
                _activeTriggers.Value = null;
            }
        }
    }

    private PassiveTriggerDispatchResult DispatchCore(
        PassiveTriggerDispatchRequest request,
        BattleExecutionServices services)
    {
        PassiveEventPolicy policy = _policies.Resolve(request.EventId);
        if (!policy.AllowsOwner(request.Owner))
        {
            return PassiveTriggerDispatchResult.Empty;
        }

        var results = new List<PassiveTriggerExecutionResult>();
        HashSet<ActiveTriggerKey> activeTriggers = _activeTriggers.Value ??= [];
        SkillDefinition[] enabledSkills = request.Owner.Passives.EnabledSkills.ToArray();

        foreach (SkillDefinition skill in enabledSkills)
        {
            for (int triggerIndex = 0; triggerIndex < skill.Triggers.Count; triggerIndex++)
            {
                PassiveTriggerDefinition trigger = skill.Triggers[triggerIndex];
                if (trigger.EventId != request.EventId)
                {
                    continue;
                }

                var activeKey = new ActiveTriggerKey(
                    request.Owner.InstanceId,
                    skill.Id,
                    triggerIndex,
                    request.EventId);
                IReadOnlyList<RuntimeActorState> targets = PassiveTriggerTargetResolver.Resolve(
                    trigger.Targeting,
                    request.Owner,
                    request.Participants,
                    request.Targets);
                if (!policy.AllowReentry && activeTriggers.Contains(activeKey))
                {
                    foreach (RuntimeActorState target in targets)
                    {
                        results.Add(NonExecuting(
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            target.InstanceId,
                            PassiveTriggerOutcome.RecursionSuppressed));
                    }
                    continue;
                }

                if (policy.ActivationCountingScope == PassiveActivationCountingScope.PerDispatch &&
                    HasReachedActivationLimit(
                        request.Owner.Passives,
                        skill.Id,
                        triggerIndex,
                        request.EventId,
                        targetInstanceId: null,
                        policy.ActivationLimitPerBattle))
                {
                    foreach (RuntimeActorState target in targets)
                    {
                        results.Add(NonExecuting(
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            target.InstanceId,
                            PassiveTriggerOutcome.ActivationLimitReached));
                    }
                    continue;
                }

                bool dispatchActivationRecorded = false;
                foreach (RuntimeActorState target in targets)
                {
                    RuntimeInstanceId? activationTargetId =
                        policy.ActivationCountingScope == PassiveActivationCountingScope.PerTarget
                            ? target.InstanceId
                            : null;
                    if (policy.ActivationCountingScope == PassiveActivationCountingScope.PerTarget &&
                        HasReachedActivationLimit(
                            request.Owner.Passives,
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            activationTargetId,
                            policy.ActivationLimitPerBattle))
                    {
                        results.Add(NonExecuting(
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            target.InstanceId,
                            PassiveTriggerOutcome.ActivationLimitReached));
                        continue;
                    }

                    var conditionContext = new BattleConditionContext(
                        request.Owner,
                        target,
                        request.Participants,
                        request.BattleKindId,
                        request.MoonPhaseId,
                        services);
                    if (!BattleConditionEvaluator.Evaluate(trigger.When, conditionContext))
                    {
                        results.Add(NonExecuting(
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            target.InstanceId,
                            PassiveTriggerOutcome.ConditionNotMet));
                        continue;
                    }

                    if (policy.ActivationCountingScope == PassiveActivationCountingScope.PerTarget ||
                        !dispatchActivationRecorded)
                    {
                        request.Owner.Passives.RecordActivation(
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            activationTargetId);
                        dispatchActivationRecorded = true;
                    }
                    activeTriggers.Add(activeKey);
                    TriggerEffectExecution execution;
                    try
                    {
                        execution = ExecuteEffects(
                            skill,
                            trigger,
                            request,
                            target,
                            services);
                        results.Add(new PassiveTriggerExecutionResult(
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            target.InstanceId,
                            PassiveTriggerOutcome.Executed,
                            execution.Effects,
                            execution.CompletionLifecycleEvents));
                    }
                    finally
                    {
                        activeTriggers.Remove(activeKey);
                    }

                    if (execution.StopsDispatch)
                    {
                        if (activeTriggers.Count == 0)
                        {
                            _activeTriggers.Value = null;
                        }

                        return new PassiveTriggerDispatchResult(Array.AsReadOnly(results.ToArray()));
                    }
                }
            }
        }

        if (activeTriggers.Count == 0)
        {
            _activeTriggers.Value = null;
        }

        return new PassiveTriggerDispatchResult(Array.AsReadOnly(results.ToArray()));
    }

    private static bool HasReachedActivationLimit(
        BattlePassiveCollection passives,
        ContentId skillId,
        int triggerIndex,
        ContentId eventId,
        RuntimeInstanceId? targetInstanceId,
        int? activationLimit) =>
        activationLimit is int limit &&
        passives.GetActivationCount(skillId, triggerIndex, eventId, targetInstanceId) >= limit;

    private static TriggerEffectExecution ExecuteEffects(
        SkillDefinition skill,
        PassiveTriggerDefinition trigger,
        PassiveTriggerDispatchRequest dispatchRequest,
        RuntimeActorState target,
        BattleExecutionServices services)
    {
        var request = new EffectActionExecutionRequest(
            skill.Id,
            dispatchRequest.Owner,
            dispatchRequest.Participants,
            new EffectExecutionEnvironment(
                dispatchRequest.ContextId,
                dispatchRequest.BattleKindId,
                dispatchRequest.MoonPhaseId,
                dispatchRequest.ActiveStatModifierBoundaries),
            new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Any, true),
            [target.InstanceId],
            skill,
            item: null,
            purpose: dispatchRequest.EventId == services.OwnerWouldBeDefeatedEventId
                ? EffectExecutionPurpose.DefeatPrevention
                : EffectExecutionPurpose.Standard);

        OrderedEffectExecution execution = new OrderedEffectExecutor(
            services,
            services.EffectExecutors).Execute(
                request,
                trigger.Effects,
                new ResolvedRuntimeTargetSet([target]));
        return new TriggerEffectExecution(
            execution.Effects,
            execution.CompletionLifecycleEvents,
            execution.StopsAction);
    }

    private static PassiveTriggerExecutionResult NonExecuting(
        ContentId skillId,
        int triggerIndex,
        ContentId eventId,
        RuntimeInstanceId targetId,
        PassiveTriggerOutcome outcome) =>
        new(skillId, triggerIndex, eventId, targetId, outcome, []);

    private sealed record ActiveTriggerKey(
        RuntimeInstanceId OwnerId,
        ContentId SkillId,
        int TriggerIndex,
        ContentId EventId);

    private sealed record TriggerEffectExecution(
        IReadOnlyList<EffectExecutionResult> Effects,
        IReadOnlyList<BattleStatusLifecycleEvent> CompletionLifecycleEvents,
        bool StopsDispatch);
}

internal static class PassiveTriggerTargetResolver
{
    public static IReadOnlyList<RuntimeActorState> Resolve(
        PassiveTriggerTargetingDefinition targeting,
        RuntimeActorState owner,
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<RuntimeActorState> eventTargets)
    {
        ArgumentNullException.ThrowIfNull(targeting);
        ArgumentNullException.ThrowIfNull(owner);
        RuntimeActorState[] participantSnapshot =
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants));
        RuntimeActorState[] eventTargetSnapshot =
            eventTargets?.ToArray() ?? throw new ArgumentNullException(nameof(eventTargets));

        IEnumerable<RuntimeActorState> candidates = targeting.Scope switch
        {
            PassiveTriggerTargetScope.Owner => [owner],
            PassiveTriggerTargetScope.EventTargets => eventTargetSnapshot,
            PassiveTriggerTargetScope.OwnerTeam =>
                participantSnapshot.Where(candidate => candidate.TeamId == owner.TeamId),
            PassiveTriggerTargetScope.OpposingTeams =>
                participantSnapshot.Where(candidate => candidate.TeamId != owner.TeamId),
            PassiveTriggerTargetScope.AllParticipants => participantSnapshot,
            _ => throw new InvalidOperationException(
                $"Passive trigger target scope '{targeting.Scope}' is not supported.")
        };

        RuntimeActorState[] targets = candidates
            .Where(candidate => targeting.IncludeReserveActors || candidate.IsDeployed)
            .Where(candidate => targeting.LifeState switch
            {
                TargetLifeState.Alive => !candidate.IsDefeated,
                TargetLifeState.Dead => candidate.IsDefeated,
                TargetLifeState.Any => true,
                _ => false
            })
            .DistinctBy(candidate => candidate.InstanceId)
            .ToArray();
        return Array.AsReadOnly(targets);
    }
}

internal sealed class ReadOnlySet<T>(IEnumerable<T> values) : IReadOnlySet<T>
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
