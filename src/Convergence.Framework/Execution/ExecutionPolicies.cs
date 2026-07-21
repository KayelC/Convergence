using System.Collections.ObjectModel;
using Convergence.Battle;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Internal;
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
        AuthoredPercentage.RequireValid(
            effect.Accuracy,
            nameof(effect),
            "Authored accuracy");
        if (effect.Critical is ChanceCriticalDefinition critical)
        {
            AuthoredPercentage.RequireValid(
                critical.Chance,
                nameof(effect),
                "Authored critical chance");
        }
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

/// <summary>
/// Describes one immutable hit resolved by a damage policy before runtime state is mutated.
/// </summary>
public sealed class DamageHitResolution
{
    public DamageHitResolution(bool hit, decimal damage, bool critical = false)
        : this(
            hitIndex: 0,
            hit,
            damage,
            critical,
            authoredAccuracy: null,
            finalAccuracy: null,
            accuracyRoll: null,
            criticalEligible: null,
            criticalEligibilityReason: null,
            criticalChance: null,
            criticalRoll: null,
            resolvedAffinity: ElementalAffinity.Normal,
            chargeKind: null,
            chargeMultiplier: 1m)
    {
    }

    public DamageHitResolution(
        int hitIndex,
        bool hit,
        decimal damage,
        bool critical,
        int? authoredAccuracy,
        int? finalAccuracy,
        decimal? accuracyRoll,
        bool? criticalEligible,
        CriticalEligibilityReason? criticalEligibilityReason,
        int? criticalChance,
        decimal? criticalRoll,
        ElementalAffinity resolvedAffinity,
        ChargeKind? chargeKind,
        decimal chargeMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hitIndex);
        if (damage < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), damage, "Resolved damage cannot be negative.");
        }
        ValidateChance(authoredAccuracy, nameof(authoredAccuracy));
        ValidateChance(finalAccuracy, nameof(finalAccuracy));
        ValidateRoll(accuracyRoll, nameof(accuracyRoll));
        ValidateChance(criticalChance, nameof(criticalChance));
        ValidateRoll(criticalRoll, nameof(criticalRoll));
        if (critical && !hit)
        {
            throw new ArgumentException("A missed hit cannot be critical.", nameof(critical));
        }
        if (!hit && damage != 0m)
        {
            throw new ArgumentException("A missed hit cannot resolve damage.", nameof(damage));
        }
        if (!hit && (criticalEligible is not null || criticalEligibilityReason is not null ||
                     criticalChance is not null || criticalRoll is not null))
        {
            throw new ArgumentException("A missed hit cannot contain a critical-resolution roll.");
        }
        if (criticalEligibilityReason is CriticalEligibilityReason reason && !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(criticalEligibilityReason),
                criticalEligibilityReason,
                "Critical eligibility reason must be defined.");
        }
        if (!Enum.IsDefined(resolvedAffinity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedAffinity),
                resolvedAffinity,
                "Resolved affinity must be defined.");
        }
        if (chargeKind is ChargeKind kind && !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(chargeKind), chargeKind, "Charge kind must be defined.");
        }
        if (chargeMultiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chargeMultiplier),
                chargeMultiplier,
                "Charge multiplier must be positive.");
        }

        HitIndex = hitIndex;
        Hit = hit;
        Damage = damage;
        Critical = critical;
        AuthoredAccuracy = authoredAccuracy;
        FinalAccuracy = finalAccuracy;
        AccuracyRoll = accuracyRoll;
        CriticalEligible = criticalEligible;
        CriticalEligibilityReason = criticalEligibilityReason;
        CriticalChance = criticalChance;
        CriticalRoll = criticalRoll;
        ResolvedAffinity = resolvedAffinity;
        ChargeKind = chargeKind;
        ChargeMultiplier = chargeMultiplier;
    }

    public int HitIndex { get; }
    public bool Hit { get; }
    public decimal Damage { get; }
    public bool Critical { get; }
    public int? AuthoredAccuracy { get; }
    public int? FinalAccuracy { get; }
    public decimal? AccuracyRoll { get; }
    public bool? CriticalEligible { get; }
    public CriticalEligibilityReason? CriticalEligibilityReason { get; }
    public int? CriticalChance { get; }
    public decimal? CriticalRoll { get; }
    public ElementalAffinity ResolvedAffinity { get; }
    public ChargeKind? ChargeKind { get; }
    public decimal ChargeMultiplier { get; }

    internal DamageHitResolution WithExecutionContext(
        int hitIndex,
        ElementalAffinity resolvedAffinity) =>
        new(
            hitIndex,
            Hit,
            Damage,
            Critical,
            AuthoredAccuracy,
            FinalAccuracy,
            AccuracyRoll,
            CriticalEligible,
            CriticalEligibilityReason,
            CriticalChance,
            CriticalRoll,
            resolvedAffinity,
            ChargeKind,
            ChargeMultiplier);

    private static void ValidateChance(int? chance, string parameterName)
    {
        if (chance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, chance, "Chances must be within 0-100.");
        }
    }

    private static void ValidateRoll(decimal? roll, string parameterName)
    {
        if (roll is < 0m or >= 100m)
        {
            throw new ArgumentOutOfRangeException(parameterName, roll, "Probability rolls must be within [0, 100).");
        }
    }
}

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

        DamageHitResolution[] snapshot = hits.ToArray();
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("Damage resolutions require at least one attempted hit.", nameof(hits));
        }
        if (snapshot.Any(hit => hit is null))
        {
            throw new ArgumentException("Damage resolutions cannot contain null hits.", nameof(hits));
        }

        Hits = Array.AsReadOnly(snapshot
            .Select((hit, index) => hit.WithExecutionContext(index, resolvedAffinity))
            .ToArray());
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
    InstantDeathResistanceResolution Resistance)
{
    private InstantKillEffectDefinition _effect = ValidateEffect(Effect);

    public InstantKillEffectDefinition Effect
    {
        get => _effect;
        init => _effect = ValidateEffect(value);
    }

    private static InstantKillEffectDefinition ValidateEffect(InstantKillEffectDefinition effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        AuthoredPercentage.RequireValid(
            effect.Chance,
            nameof(Effect),
            "Authored instant-defeat chance");
        return effect;
    }
}

public interface IInstantDeathExecutionPolicy
{
    bool ShouldDefeat(InstantDeathPolicyRequest request);
}

public sealed record AilmentApplicationPolicyRequest(
    RuntimeActorState Actor,
    RuntimeActorState Target,
    int Chance,
    AilmentDefinition Ailment,
    ResistanceLevel Resistance)
{
    private int _chance = ValidateChance(Chance);

    public int Chance
    {
        get => _chance;
        init => _chance = ValidateChance(value);
    }

    private static int ValidateChance(int chance)
    {
        AuthoredPercentage.RequireValid(
            chance,
            nameof(Chance),
            "Authored ailment chance");
        return chance;
    }
}

public interface IAilmentApplicationPolicy
{
    bool ShouldApply(AilmentApplicationPolicyRequest request);
}

public sealed record ChancePolicyRequest(
    int Chance,
    RuntimeActorState Actor,
    RuntimeActorState? Target,
    string Purpose)
{
    private int _chance = ValidateChance(Chance);

    public int Chance
    {
        get => _chance;
        init => _chance = ValidateChance(value);
    }

    private static int ValidateChance(int chance)
    {
        AuthoredPercentage.RequireValid(
            chance,
            nameof(Chance),
            "Authored chance");
        return chance;
    }
}

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

/// <summary>
/// Damage execution that exposes the exact hit and critical policies used by
/// its resolution pipeline.
/// </summary>
public interface ICombatDamageExecutionPolicy : IDamageExecutionPolicy
{
    IHitResolutionPolicy HitResolution { get; }
    ICriticalEligibilityPolicy CriticalEligibility { get; }
    ICriticalChancePolicy CriticalChance { get; }
}

/// <summary>
/// Instant-defeat execution that exposes the exact resistance and probability
/// policy used by its resolution pipeline.
/// </summary>
public interface ICombatInstantDefeatExecutionPolicy : IInstantDeathExecutionPolicy
{
    IInstantDefeatResolutionPolicy Resolution { get; }
}

/// <summary>
/// Immutable selection of the independently replaceable policies required by
/// typed combat execution. Authored ruleset binding returns this neutral
/// composition instead of requiring hosts to depend on a supplied concrete
/// ruleset implementation.
/// </summary>
public sealed class CombatExecutionPolicySet
{
    public CombatExecutionPolicySet(
        ContentId rulesetId,
        ContentId policyId,
        ICombatDamageExecutionPolicy damage,
        IChargePolicyService charges,
        ICombatInstantDefeatExecutionPolicy instantDefeat,
        IAilmentApplicationPolicy ailments,
        IChanceExecutionPolicy chance,
        IPowerAmountPolicy amounts,
        IActionOutcomeAggregationPolicy actionOutcomes,
        IEnumerable<KeyValuePair<string, object?>>? authoredParameters = null,
        IEnumerable<KeyValuePair<string, object?>>? effectiveConfiguration = null)
    {
        if (!rulesetId.IsValid)
        {
            throw new ArgumentException("Ruleset ID must be valid.", nameof(rulesetId));
        }
        if (!policyId.IsValid || policyId.IsQualified)
        {
            throw new ArgumentException("Combat policy ID must be a valid local ID.", nameof(policyId));
        }

        RulesetId = rulesetId;
        PolicyId = policyId;
        Damage = damage ?? throw new ArgumentNullException(nameof(damage));
        Charges = charges ?? throw new ArgumentNullException(nameof(charges));
        InstantDefeat = instantDefeat ?? throw new ArgumentNullException(nameof(instantDefeat));
        Ailments = ailments ?? throw new ArgumentNullException(nameof(ailments));
        Chance = chance ?? throw new ArgumentNullException(nameof(chance));
        Amounts = amounts ?? throw new ArgumentNullException(nameof(amounts));
        ActionOutcomes = actionOutcomes ?? throw new ArgumentNullException(nameof(actionOutcomes));
        AuthoredParameters = DefinitionCollections.SnapshotParameters(authoredParameters);
        EffectiveConfiguration = DefinitionCollections.SnapshotParameters(
            effectiveConfiguration ?? authoredParameters);
    }

    public ContentId RulesetId { get; }
    public ContentId PolicyId { get; }
    public IReadOnlyDictionary<string, object?> AuthoredParameters { get; }
    public IReadOnlyDictionary<string, object?> EffectiveConfiguration { get; }
    public ICombatDamageExecutionPolicy Damage { get; }
    public IHitResolutionPolicy HitResolution => Damage.HitResolution;
    public ICriticalEligibilityPolicy CriticalEligibility => Damage.CriticalEligibility;
    public ICriticalChancePolicy CriticalChance => Damage.CriticalChance;
    public IChargePolicyService Charges { get; }
    public ICombatInstantDefeatExecutionPolicy InstantDefeat { get; }
    public IInstantDefeatResolutionPolicy InstantDefeatResolution => InstantDefeat.Resolution;
    public IAilmentApplicationPolicy Ailments { get; }
    public IChanceExecutionPolicy Chance { get; }
    public IPowerAmountPolicy Amounts { get; }
    public IActionOutcomeAggregationPolicy ActionOutcomes { get; }
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
        IBattleAilmentApplicationService? ailmentApplications = null,
        IActionOutcomeAggregationPolicy? actionOutcomes = null)
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
        ActionOutcomes = actionOutcomes ?? new StandardActionOutcomeAggregationPolicy();
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
    public IActionOutcomeAggregationPolicy ActionOutcomes { get; }

    internal TurnEconomyResolution ResolveActionOutcome(
        IReadOnlyList<EffectExecutionResult> effects,
        ActionOutcomeSourceKind sourceKind)
    {
        var request = new ActionOutcomeAggregationRequest(sourceKind, effects);
        TurnEconomyResolution resolution = ActionOutcomes.Aggregate(request)
            ?? throw new InvalidOperationException("The action-outcome policy returned no resolution.");
        if (!Enum.IsDefined(resolution.Outcome))
        {
            throw new InvalidOperationException(
                $"The action-outcome policy returned undefined outcome '{resolution.Outcome}'.");
        }

        return resolution;
    }

    private static IReadOnlyDictionary<ContentId, T> Snapshot<T>(
        IEnumerable<KeyValuePair<ContentId, T>>? values) where T : notnull =>
        new ReadOnlyDictionary<ContentId, T>((values ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));
}
