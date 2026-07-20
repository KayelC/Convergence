namespace Convergence.Execution;

/// <summary>
/// Identifies the action surface whose typed effects are being aggregated.
/// </summary>
public enum ActionOutcomeSourceKind
{
    Skill,
    BasicAttack,
    Item,
    Other
}

/// <summary>
/// Selects how the supplied standard policy prices item actions.
/// </summary>
public enum ItemActionOutcomeBehavior
{
    Normal,
    EffectDriven
}

/// <summary>
/// Supplies immutable source and effect facts to an action-outcome policy.
/// </summary>
public sealed class ActionOutcomeAggregationRequest
{
    public ActionOutcomeAggregationRequest(
        ActionOutcomeSourceKind sourceKind,
        IEnumerable<EffectExecutionResult> effects)
    {
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind));
        }

        ArgumentNullException.ThrowIfNull(effects);
        EffectExecutionResult[] snapshot = effects.ToArray();
        if (snapshot.Any(effect => effect is null))
        {
            throw new ArgumentException(
                "Action outcome collections cannot contain null results.",
                nameof(effects));
        }

        SourceKind = sourceKind;
        Effects = Array.AsReadOnly(snapshot);
    }

    public ActionOutcomeSourceKind SourceKind { get; }
    public IReadOnlyList<EffectExecutionResult> Effects { get; }
}

/// <summary>
/// Configures Convergence's supplied action-outcome policy.
/// </summary>
public sealed class StandardActionOutcomeAggregationPolicyConfig
{
    public StandardActionOutcomeAggregationPolicyConfig(
        ItemActionOutcomeBehavior itemBehavior = ItemActionOutcomeBehavior.Normal)
    {
        if (!Enum.IsDefined(itemBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(itemBehavior));
        }

        ItemBehavior = itemBehavior;
    }

    public ItemActionOutcomeBehavior ItemBehavior { get; }
}

/// <summary>
/// Derives one action-level turn-economy result from ordered per-target effect results.
/// </summary>
public interface IActionOutcomeAggregationPolicy
{
    TurnEconomyResolution Aggregate(IReadOnlyList<EffectExecutionResult> effects);

    TurnEconomyResolution Aggregate(ActionOutcomeAggregationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Aggregate(request.Effects);
    }
}

/// <summary>
/// Supplies Convergence's Action Token outcome mapping without coupling effect execution to a turn economy.
/// </summary>
public sealed class StandardActionOutcomeAggregationPolicy : IActionOutcomeAggregationPolicy
{
    public StandardActionOutcomeAggregationPolicy()
        : this(new StandardActionOutcomeAggregationPolicyConfig())
    {
    }

    public StandardActionOutcomeAggregationPolicy(
        StandardActionOutcomeAggregationPolicyConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public StandardActionOutcomeAggregationPolicyConfig Config { get; }

    public TurnEconomyResolution Aggregate(IReadOnlyList<EffectExecutionResult> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return AggregateEffects(effects);
    }

    public TurnEconomyResolution Aggregate(ActionOutcomeAggregationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.SourceKind == ActionOutcomeSourceKind.Item &&
               Config.ItemBehavior == ItemActionOutcomeBehavior.Normal
            ? new TurnEconomyResolution(TurnEconomyOutcome.Normal, false, false)
            : AggregateEffects(request.Effects);
    }

    private static TurnEconomyResolution AggregateEffects(
        IReadOnlyList<EffectExecutionResult> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (effects.Any(effect => effect is null))
        {
            throw new ArgumentException("Action outcome collections cannot contain null results.", nameof(effects));
        }

        bool anyCritical = effects.Any(effect => effect.IsCritical);
        EffectExecutionResult? interruption = effects.FirstOrDefault(effect =>
            effect.TurnEconomyOutcome is TurnEconomyOutcome.Repel or TurnEconomyOutcome.Absorb);
        if (interruption is not null)
        {
            return new TurnEconomyResolution(interruption.TurnEconomyOutcome, anyCritical, true);
        }

        bool anyNull = effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Null);
        bool anyEvadedTarget = effects.Any(IsEvadedTarget);
        bool anyWeakness = effects.Any(effect => effect.TurnEconomyOutcome == TurnEconomyOutcome.Weakness);

        TurnEconomyOutcome outcome = anyNull
            ? TurnEconomyOutcome.Null
            : anyEvadedTarget && anyCritical
                ? TurnEconomyOutcome.Normal
                : anyEvadedTarget
                    ? TurnEconomyOutcome.Miss
                    : anyWeakness
                        ? TurnEconomyOutcome.Weakness
                        : anyCritical
                            ? TurnEconomyOutcome.Critical
                            : TurnEconomyOutcome.Normal;

        return new TurnEconomyResolution(outcome, anyCritical, false);
    }

    private static bool IsEvadedTarget(EffectExecutionResult effect)
    {
        if (effect.DamageHits.Count > 0)
        {
            return effect.DamageHits.All(hit => !hit.Hit);
        }

        // Preserve custom effect compatibility while typed damage uses per-hit facts.
        return effect.TurnEconomyOutcome == TurnEconomyOutcome.Miss;
    }
}
