using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;

namespace JRPGPrototype.Logic.Battle.Execution;

public sealed record DamagePolicyRequest(
    RuntimeActorState Actor,
    RuntimeActorState Target,
    DamageEffectDefinition Effect,
    ElementalAffinity Affinity);

public sealed record DamageHitResolution(bool Hit, decimal Damage, bool Critical = false);

public interface IDamageExecutionPolicy
{
    IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request);
}

public sealed record InstantDeathPolicyRequest(
    RuntimeActorState Actor,
    RuntimeActorState Target,
    InstantKillEffectDefinition Effect,
    InstantDeathResistanceResolution Resistance);

public interface IInstantDeathExecutionPolicy
{
    bool ShouldDefeat(InstantDeathPolicyRequest request);
}

public sealed record AilmentApplicationPolicyRequest(
    RuntimeActorState Actor,
    RuntimeActorState Target,
    ApplyAilmentEffectDefinition Effect,
    AilmentDefinition Ailment,
    ResistanceLevel Resistance);

public interface IAilmentApplicationPolicy
{
    bool ShouldApply(AilmentApplicationPolicyRequest request);
}

public sealed record ChancePolicyRequest(
    int Chance,
    RuntimeActorState Actor,
    RuntimeActorState? Target,
    string Purpose);

public interface IChanceExecutionPolicy
{
    bool Roll(ChancePolicyRequest request);
}

public sealed record AmountResolutionContext(
    RuntimeActorState Actor,
    RuntimeActorState Target,
    ContentId ResourceId,
    string Purpose);

public interface IPowerAmountPolicy
{
    decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context);
}

public interface IFormulaAmountHandler
{
    decimal Resolve(FormulaAmountDefinition amount, AmountResolutionContext context);
}

public interface IRandomTargetSelectionPolicy
{
    IReadOnlyList<RuntimeActorState> Select(
        IReadOnlyList<RuntimeActorState> candidates,
        TargetCountDefinition count,
        SkillExecutionRequest request);
}

public interface IRuntimeRandomTargetSelectionPolicy
{
    IReadOnlyList<RuntimeActorState> Select(
        IReadOnlyList<RuntimeActorState> candidates,
        TargetCountDefinition count,
        EffectActionExecutionRequest request);
}

public sealed class OrderedRuntimeTargetSelectionPolicy : IRuntimeRandomTargetSelectionPolicy
{
    public IReadOnlyList<RuntimeActorState> Select(
        IReadOnlyList<RuntimeActorState> candidates,
        TargetCountDefinition count,
        EffectActionExecutionRequest request) =>
        Array.AsReadOnly(candidates.Take(count.Maximum).ToArray());
}

public interface IEscapeRuleHandler
{
    bool CanEscape(EscapeEffectDefinition effect, EffectExecutionContext context);
}

public interface ICustomConditionHandler
{
    bool Evaluate(CustomConditionDefinition condition, BattleConditionContext context);
}

public interface ICustomEffectHandler
{
    EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context);
}

public sealed class BattleExecutionServices
{
    public BattleExecutionServices(
        IAilmentDefinitionRepository ailments,
        IDamageExecutionPolicy damagePolicy,
        IInstantDeathExecutionPolicy instantDeathPolicy,
        IAilmentApplicationPolicy ailmentPolicy,
        IChanceExecutionPolicy chancePolicy,
        IPowerAmountPolicy powerAmountPolicy,
        IRandomTargetSelectionPolicy randomTargetPolicy,
        IEnumerable<KeyValuePair<ContentId, IFormulaAmountHandler>>? formulaHandlers = null,
        IEnumerable<KeyValuePair<ContentId, IEscapeRuleHandler>>? escapeRuleHandlers = null,
        IEnumerable<KeyValuePair<ContentId, ICustomConditionHandler>>? customConditionHandlers = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffectHandlers = null,
        ContentId? hpResourceId = null,
        ContentId? spResourceId = null,
        EffectExecutorRegistry? effectExecutors = null,
        RuleModifierResolver? ruleModifiers = null,
        PassiveEventPolicyRegistry? passiveEventPolicies = null,
        IPassiveTriggerDispatcher? passiveTriggers = null,
        ContentId? ownerWouldBeDefeatedEventId = null,
        IRuntimeRandomTargetSelectionPolicy? runtimeRandomTargetPolicy = null)
    {
        Ailments = ailments ?? throw new ArgumentNullException(nameof(ailments));
        DamagePolicy = damagePolicy ?? throw new ArgumentNullException(nameof(damagePolicy));
        InstantDeathPolicy = instantDeathPolicy ?? throw new ArgumentNullException(nameof(instantDeathPolicy));
        AilmentPolicy = ailmentPolicy ?? throw new ArgumentNullException(nameof(ailmentPolicy));
        ChancePolicy = chancePolicy ?? throw new ArgumentNullException(nameof(chancePolicy));
        PowerAmountPolicy = powerAmountPolicy ?? throw new ArgumentNullException(nameof(powerAmountPolicy));
        RandomTargetPolicy = randomTargetPolicy ?? throw new ArgumentNullException(nameof(randomTargetPolicy));
        RuntimeRandomTargetPolicy = runtimeRandomTargetPolicy ?? new OrderedRuntimeTargetSelectionPolicy();
        FormulaHandlers = Snapshot(formulaHandlers);
        EscapeRuleHandlers = Snapshot(escapeRuleHandlers);
        CustomConditionHandlers = Snapshot(customConditionHandlers);
        CustomEffectHandlers = Snapshot(customEffectHandlers);
        HpResourceId = hpResourceId ?? ContentId.Parse("hp");
        SpResourceId = spResourceId ?? ContentId.Parse("sp");
        EffectExecutors = effectExecutors ?? EffectExecutorRegistry.CreateDefault();
        RuleModifiers = ruleModifiers ?? new RuleModifierResolver();
        OwnerWouldBeDefeatedEventId = ownerWouldBeDefeatedEventId ?? ContentId.Parse("owner_would_be_defeated");
        PassiveEventPolicies = passiveEventPolicies ?? new PassiveEventPolicyRegistry();
        PassiveEventPolicies.Register(
            OwnerWouldBeDefeatedEventId,
            new PassiveEventPolicy(ActivationLimitPerBattle: 1));
        PassiveTriggers = passiveTriggers ?? new PassiveTriggerDispatcher(PassiveEventPolicies);
    }

    public IAilmentDefinitionRepository Ailments { get; }
    public IDamageExecutionPolicy DamagePolicy { get; }
    public IInstantDeathExecutionPolicy InstantDeathPolicy { get; }
    public IAilmentApplicationPolicy AilmentPolicy { get; }
    public IChanceExecutionPolicy ChancePolicy { get; }
    public IPowerAmountPolicy PowerAmountPolicy { get; }
    public IRandomTargetSelectionPolicy RandomTargetPolicy { get; }
    public IRuntimeRandomTargetSelectionPolicy RuntimeRandomTargetPolicy { get; }
    public IReadOnlyDictionary<ContentId, IFormulaAmountHandler> FormulaHandlers { get; }
    public IReadOnlyDictionary<ContentId, IEscapeRuleHandler> EscapeRuleHandlers { get; }
    public IReadOnlyDictionary<ContentId, ICustomConditionHandler> CustomConditionHandlers { get; }
    public IReadOnlyDictionary<ContentId, ICustomEffectHandler> CustomEffectHandlers { get; }
    public ContentId HpResourceId { get; }
    public ContentId SpResourceId { get; }
    public EffectExecutorRegistry EffectExecutors { get; }
    public RuleModifierResolver RuleModifiers { get; }
    public PassiveEventPolicyRegistry PassiveEventPolicies { get; }
    public IPassiveTriggerDispatcher PassiveTriggers { get; }
    public ContentId OwnerWouldBeDefeatedEventId { get; }

    private static IReadOnlyDictionary<ContentId, T> Snapshot<T>(
        IEnumerable<KeyValuePair<ContentId, T>>? values) where T : notnull =>
        new ReadOnlyDictionary<ContentId, T>((values ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));
}
