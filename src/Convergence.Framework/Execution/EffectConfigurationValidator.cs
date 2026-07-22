using Convergence.Content;

namespace Convergence.Execution;

internal enum EffectConfigurationIssueCode
{
    AilmentMissing,
    FormulaHandlerMissing,
    EscapeRuleHandlerMissing,
    CustomEffectHandlerMissing,
    CustomConditionHandlerMissing
}

internal sealed record EffectConfigurationIssue(
    EffectConfigurationIssueCode Code,
    string Message);

internal static class EffectConfigurationValidator
{
    public static IReadOnlyList<EffectConfigurationIssue> Validate(
        EffectDefinition effect,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(services);

        var issues = new List<EffectConfigurationIssue>();
        ValidateEffect(effect, services, issues);
        ValidateCondition(effect.When, services, issues);
        return Array.AsReadOnly(issues.ToArray());
    }

    private static void ValidateEffect(
        EffectDefinition effect,
        BattleExecutionServices services,
        ICollection<EffectConfigurationIssue> issues)
    {
        switch (effect)
        {
            case ApplyAilmentEffectDefinition ailment
                when !services.Ailments.TryGetAilment(ailment.AilmentId, out _):
                Add(
                    EffectConfigurationIssueCode.AilmentMissing,
                    $"Ailment '{ailment.AilmentId}' is unavailable at runtime.");
                break;
            case RestoreResourceEffectDefinition restore:
                ValidateAmount(restore.Amount);
                break;
            case ReviveEffectDefinition revive:
                ValidateAmount(revive.Amount);
                break;
            case ReduceResourceEffectDefinition reduce:
                ValidateAmount(reduce.Amount);
                break;
            case SetResourceEffectDefinition set:
                ValidateAmount(set.Amount);
                break;
            case EscapeEffectDefinition escape
                when !services.EscapeRuleHandlers.ContainsKey(escape.EligibilityRuleId):
                Add(
                    EffectConfigurationIssueCode.EscapeRuleHandlerMissing,
                    $"No escape rule handler is registered for '{escape.EligibilityRuleId}'.");
                break;
            case CustomEffectDefinition custom
                when !services.CustomEffectHandlers.ContainsKey(custom.HandlerId):
                Add(
                    EffectConfigurationIssueCode.CustomEffectHandlerMissing,
                    $"No custom effect handler is registered for '{custom.HandlerId}'.");
                break;
        }

        return;

        void ValidateAmount(AmountDefinition amount)
        {
            if (amount is FormulaAmountDefinition formula &&
                !services.FormulaHandlers.ContainsKey(formula.FormulaId))
            {
                Add(
                    EffectConfigurationIssueCode.FormulaHandlerMissing,
                    $"No formula handler is registered for '{formula.FormulaId}'.");
            }
        }

        void Add(EffectConfigurationIssueCode code, string message) =>
            issues.Add(new EffectConfigurationIssue(code, message));
    }

    private static void ValidateCondition(
        ConditionDefinition? condition,
        BattleExecutionServices services,
        ICollection<EffectConfigurationIssue> issues)
    {
        switch (condition)
        {
            case null:
                return;
            case AllConditionDefinition all:
                foreach (ConditionDefinition child in all.Conditions)
                {
                    ValidateCondition(child, services, issues);
                }
                return;
            case AnyConditionDefinition any:
                foreach (ConditionDefinition child in any.Conditions)
                {
                    ValidateCondition(child, services, issues);
                }
                return;
            case NotConditionDefinition not:
                ValidateCondition(not.Condition, services, issues);
                return;
            case CustomConditionDefinition custom
                when !services.CustomConditionHandlers.ContainsKey(custom.HandlerId):
                issues.Add(new EffectConfigurationIssue(
                    EffectConfigurationIssueCode.CustomConditionHandlerMissing,
                    $"No custom condition handler is registered for '{custom.HandlerId}'."));
                return;
        }
    }
}
