namespace Convergence.Execution;

/// <summary>
/// Derives one action-level turn-economy result from ordered per-target effect results.
/// </summary>
public interface IActionOutcomeAggregationPolicy
{
    TurnEconomyResolution Aggregate(IReadOnlyList<EffectExecutionResult> effects);
}

/// <summary>
/// Supplies Convergence's Action Token outcome mapping without coupling effect execution to a turn economy.
/// </summary>
public sealed class StandardActionOutcomeAggregationPolicy : IActionOutcomeAggregationPolicy
{
    public TurnEconomyResolution Aggregate(IReadOnlyList<EffectExecutionResult> effects)
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
