using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle.Execution;

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
            .Select(pair => new RuntimePassiveActivationSnapshot(
                pair.Key.SkillId,
                pair.Key.EventId,
                pair.Key.TriggerIndex,
                pair.Value))
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
        _activationCounts.Clear();
        foreach (RuntimePassiveActivationSnapshot activation in
                 activations ?? throw new ArgumentNullException(nameof(activations)))
        {
            if (!_entries.Any(entry => entry.Skill.Id == activation.SkillId))
            {
                throw new ArgumentException(
                    $"Passive activation references unloaded skill '{activation.SkillId}'.",
                    nameof(activations));
            }

            _activationCounts.Add(
                new PassiveActivationKey(
                    activation.SkillId,
                    activation.TriggerIndex,
                    activation.EventId),
                activation.ActivationCount);
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

    internal int GetActivationCount(ContentId skillId, int triggerIndex, ContentId eventId) =>
        _activationCounts.GetValueOrDefault(new PassiveActivationKey(skillId, triggerIndex, eventId));

    internal void RecordActivation(ContentId skillId, int triggerIndex, ContentId eventId)
    {
        var key = new PassiveActivationKey(skillId, triggerIndex, eventId);
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

    private readonly record struct PassiveActivationKey(ContentId SkillId, int TriggerIndex, ContentId EventId);
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
        NumericRuleModifierDefinition[] modifiers = EnumerateApplicable(owner, context)
            .OfType<NumericRuleModifierDefinition>()
            .Where(modifier => modifier.ModifierType == modifierType)
            .ToArray();

        return _stackingPolicies.GetRequired(modifierType).Resolve(baseValue, modifiers);
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

public sealed record PassiveEventPolicy(bool AllowReentry = false, int? ActivationLimitPerBattle = null);

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
}

public enum PassiveTriggerOutcome
{
    Executed,
    ConditionNotMet,
    RecursionSuppressed,
    ActivationLimitReached
}

public sealed record PassiveTriggerExecutionResult(
    ContentId SkillId,
    int TriggerIndex,
    ContentId EventId,
    RuntimeInstanceId TargetId,
    PassiveTriggerOutcome Outcome,
    IReadOnlyList<EffectExecutionResult> Effects);

public sealed record PassiveTriggerDispatchResult(IReadOnlyList<PassiveTriggerExecutionResult> Activations)
{
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
        ContentId? moonPhaseId)
    {
        EventId = eventId;
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Participants = Array.AsReadOnly(participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        Targets = Array.AsReadOnly(targets?.ToArray() ?? throw new ArgumentNullException(nameof(targets)));
        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
    }

    public ContentId EventId { get; }
    public RuntimeActorState Owner { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public IReadOnlyList<RuntimeActorState> Targets { get; }
    public ContentId ContextId { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
}

public interface IPassiveTriggerDispatcher
{
    PassiveTriggerDispatchResult Dispatch(
        PassiveTriggerDispatchRequest request,
        BattleExecutionServices services);
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

        var results = new List<PassiveTriggerExecutionResult>();
        HashSet<ActiveTriggerKey> activeTriggers = _activeTriggers.Value ??= [];

        foreach (SkillDefinition skill in request.Owner.Passives.EnabledSkills)
        {
            for (int triggerIndex = 0; triggerIndex < skill.Triggers.Count; triggerIndex++)
            {
                PassiveTriggerDefinition trigger = skill.Triggers[triggerIndex];
                if (trigger.EventId != request.EventId)
                {
                    continue;
                }

                PassiveEventPolicy policy = _policies.Resolve(request.EventId);
                var activeKey = new ActiveTriggerKey(request.Owner, skill.Id, triggerIndex, request.EventId);
                foreach (RuntimeActorState target in request.Targets)
                {
                    if (!policy.AllowReentry && activeTriggers.Contains(activeKey))
                    {
                        results.Add(NonExecuting(
                            skill.Id,
                            triggerIndex,
                            request.EventId,
                            target.InstanceId,
                            PassiveTriggerOutcome.RecursionSuppressed));
                        continue;
                    }

                    if (policy.ActivationLimitPerBattle is int limit &&
                        request.Owner.Passives.GetActivationCount(skill.Id, triggerIndex, request.EventId) >= limit)
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

                    request.Owner.Passives.RecordActivation(skill.Id, triggerIndex, request.EventId);
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
                            execution.Effects));
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
                dispatchRequest.MoonPhaseId),
            new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Any, true),
            [target.InstanceId],
            skill: skill);

        OrderedEffectExecution execution = new OrderedEffectExecutor(
            services,
            services.EffectExecutors).Execute(
                request,
                trigger.Effects,
                new ResolvedRuntimeTargetSet([target]));
        return new TriggerEffectExecution(execution.Effects, execution.StopsAction);
    }

    private static PassiveTriggerExecutionResult NonExecuting(
        ContentId skillId,
        int triggerIndex,
        ContentId eventId,
        RuntimeInstanceId targetId,
        PassiveTriggerOutcome outcome) =>
        new(skillId, triggerIndex, eventId, targetId, outcome, []);

    private sealed record ActiveTriggerKey(
        RuntimeActorState Owner,
        ContentId SkillId,
        int TriggerIndex,
        ContentId EventId);

    private sealed record TriggerEffectExecution(
        IReadOnlyList<EffectExecutionResult> Effects,
        bool StopsDispatch);
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
