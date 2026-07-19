using System.Collections.ObjectModel;
using Convergence.Battle;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Runtime;

namespace Convergence.Execution;

public sealed class DamagePolicyRequest
{
    public DamagePolicyRequest(
        RuntimeActorState actor,
        RuntimeActorState target,
        DamageEffectDefinition effect,
        ElementalAffinity affinity,
        decimal chargeMultiplier = 1m,
        ChargeKind? chargeKind = null,
        IEnumerable<NumericRuleModifierDefinition>? accuracyModifiers = null,
        IEnumerable<NumericRuleModifierDefinition>? evasionModifiers = null,
        IEnumerable<NumericRuleModifierDefinition>? criticalChanceModifiers = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        if (!Enum.IsDefined(affinity))
        {
            throw new ArgumentOutOfRangeException(nameof(affinity), affinity, "Affinity must be defined.");
        }
        if (chargeMultiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chargeMultiplier),
                chargeMultiplier,
                "Charge multiplier must be positive.");
        }
        if (chargeKind is ChargeKind kind && !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(chargeKind), chargeKind, "Charge kind must be defined.");
        }

        Affinity = affinity;
        ChargeMultiplier = chargeMultiplier;
        ChargeKind = chargeKind;
        AccuracyModifiers = SnapshotModifiers(accuracyModifiers, nameof(accuracyModifiers));
        EvasionModifiers = SnapshotModifiers(evasionModifiers, nameof(evasionModifiers));
        CriticalChanceModifiers = SnapshotModifiers(
            criticalChanceModifiers,
            nameof(criticalChanceModifiers));
    }

    public RuntimeActorState Actor { get; }
    public RuntimeActorState Target { get; }
    public DamageEffectDefinition Effect { get; }
    public ElementalAffinity Affinity { get; }
    public decimal ChargeMultiplier { get; }
    public ChargeKind? ChargeKind { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> AccuracyModifiers { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> EvasionModifiers { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> CriticalChanceModifiers { get; }

    private static IReadOnlyList<NumericRuleModifierDefinition> SnapshotModifiers(
        IEnumerable<NumericRuleModifierDefinition>? modifiers,
        string parameterName)
    {
        NumericRuleModifierDefinition[] snapshot = modifiers?.ToArray() ?? [];
        if (snapshot.Any(modifier => modifier is null))
        {
            throw new ArgumentException("Damage modifier collections cannot contain null entries.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record DamageHitResolution(bool Hit, decimal Damage, bool Critical = false);

/// <summary>
/// Contains the complete authoritative result of resolving one typed damage effect.
/// </summary>
public sealed class DamagePolicyResolution
{
    public DamagePolicyResolution(
        IEnumerable<DamageHitResolution> hits,
        ElementalAffinity resolvedAffinity)
    {
        ArgumentNullException.ThrowIfNull(hits);
        if (!Enum.IsDefined(resolvedAffinity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedAffinity),
                resolvedAffinity,
                "The resolved affinity must be a defined affinity value.");
        }

        Hits = Array.AsReadOnly(hits.ToArray());
        ResolvedAffinity = resolvedAffinity;
    }

    public IReadOnlyList<DamageHitResolution> Hits { get; }
    public ElementalAffinity ResolvedAffinity { get; }
}

public interface IDamageExecutionPolicy
{
    DamagePolicyResolution Resolve(DamagePolicyRequest request);
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
    int Chance,
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
    // Formula evaluation must not mutate host-owned state. Execution may be retried or discarded.
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
    // Conditions are assessments and must not mutate host-owned state.
    bool Evaluate(CustomConditionDefinition condition, BattleConditionContext context);
}

public interface ICustomEffectHandler
{
    // Runtime actors in the context are staged. Host-side work must be represented by result requests,
    // not performed directly, because a later effect or inventory commit may reject the action.
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
        IRuntimeRandomTargetSelectionPolicy runtimeRandomTargetPolicy,
        IStatModifierPolicyService statModifiers,
        IChargePolicyService charges,
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
        IBattleAilmentApplicationService? ailmentApplications = null)
    {
        Ailments = ailments ?? throw new ArgumentNullException(nameof(ailments));
        DamagePolicy = damagePolicy ?? throw new ArgumentNullException(nameof(damagePolicy));
        InstantDeathPolicy = instantDeathPolicy ?? throw new ArgumentNullException(nameof(instantDeathPolicy));
        AilmentPolicy = ailmentPolicy ?? throw new ArgumentNullException(nameof(ailmentPolicy));
        ChancePolicy = chancePolicy ?? throw new ArgumentNullException(nameof(chancePolicy));
        PowerAmountPolicy = powerAmountPolicy ?? throw new ArgumentNullException(nameof(powerAmountPolicy));
        RandomTargetPolicy = randomTargetPolicy ?? throw new ArgumentNullException(nameof(randomTargetPolicy));
        RuntimeRandomTargetPolicy = runtimeRandomTargetPolicy ?? throw new ArgumentNullException(nameof(runtimeRandomTargetPolicy));
        StatModifiers = statModifiers ?? throw new ArgumentNullException(nameof(statModifiers));
        Charges = charges ?? throw new ArgumentNullException(nameof(charges));
        FormulaHandlers = Snapshot(formulaHandlers);
        EscapeRuleHandlers = Snapshot(escapeRuleHandlers);
        CustomConditionHandlers = Snapshot(customConditionHandlers);
        CustomEffectHandlers = Snapshot(customEffectHandlers);
        HpResourceId = hpResourceId ?? ContentId.Parse("hp");
        SpResourceId = spResourceId ?? ContentId.Parse("sp");
        EffectExecutors = effectExecutors ?? EffectExecutorRegistry.CreateDefault();
        RuleModifiers = ruleModifiers ?? new RuleModifierResolver();
        AilmentApplications = ailmentApplications ?? new BattleAilmentApplicationService();
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
    public IStatModifierPolicyService StatModifiers { get; }
    public IChargePolicyService Charges { get; }
    public IReadOnlyDictionary<ContentId, IFormulaAmountHandler> FormulaHandlers { get; }
    public IReadOnlyDictionary<ContentId, IEscapeRuleHandler> EscapeRuleHandlers { get; }
    public IReadOnlyDictionary<ContentId, ICustomConditionHandler> CustomConditionHandlers { get; }
    public IReadOnlyDictionary<ContentId, ICustomEffectHandler> CustomEffectHandlers { get; }
    public ContentId HpResourceId { get; }
    public ContentId SpResourceId { get; }
    public EffectExecutorRegistry EffectExecutors { get; }
    public RuleModifierResolver RuleModifiers { get; }
    public IBattleAilmentApplicationService AilmentApplications { get; }
    public PassiveEventPolicyRegistry PassiveEventPolicies { get; }
    public IPassiveTriggerDispatcher PassiveTriggers { get; }
    public ContentId OwnerWouldBeDefeatedEventId { get; }

    private static IReadOnlyDictionary<ContentId, T> Snapshot<T>(
        IEnumerable<KeyValuePair<ContentId, T>>? values) where T : notnull =>
        new ReadOnlyDictionary<ContentId, T>((values ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));
}
