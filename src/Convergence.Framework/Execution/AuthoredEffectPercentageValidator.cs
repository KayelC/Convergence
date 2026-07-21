using Convergence.Content;
using Convergence.Internal;

namespace Convergence.Execution;

internal sealed record AuthoredPercentageIssue(
    string Path,
    decimal Value)
{
    public string Message =>
        $"Authored percentage '{Path}' must be within " +
        $"{AuthoredPercentage.Minimum}-{AuthoredPercentage.Maximum}; received {Value}.";
}

internal static class AuthoredEffectPercentageValidator
{
    public static IReadOnlyList<AuthoredPercentageIssue> Validate(EffectDefinition effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var issues = new List<AuthoredPercentageIssue>();

        switch (effect)
        {
            case DamageEffectDefinition damage:
                Add(issues, "accuracy", damage.Accuracy);
                if (damage.Critical is ChanceCriticalDefinition critical)
                {
                    Add(issues, "critical.chance", critical.Chance);
                }
                break;
            case InstantKillEffectDefinition instantDefeat:
                Add(issues, "chance", instantDefeat.Chance);
                break;
            case ApplyAilmentEffectDefinition ailment:
                Add(issues, "chance", ailment.Chance);
                break;
            case EscapeEffectDefinition { Chance: int chance }:
                Add(issues, "chance", chance);
                break;
        }

        ValidateCondition(effect.When, "when", issues);
        return Array.AsReadOnly(issues.ToArray());
    }

    private static void ValidateCondition(
        ConditionDefinition? condition,
        string path,
        ICollection<AuthoredPercentageIssue> issues)
    {
        switch (condition)
        {
            case null:
                return;
            case AllConditionDefinition all:
                for (int index = 0; index < all.Conditions.Count; index++)
                {
                    ValidateCondition(all.Conditions[index], $"{path}.all[{index}]", issues);
                }
                return;
            case AnyConditionDefinition any:
                for (int index = 0; index < any.Conditions.Count; index++)
                {
                    ValidateCondition(any.Conditions[index], $"{path}.any[{index}]", issues);
                }
                return;
            case NotConditionDefinition not:
                ValidateCondition(not.Condition, $"{path}.not", issues);
                return;
            case ChanceConditionDefinition chance:
                Add(issues, $"{path}.chance", chance.Chance);
                return;
            case ResourcePercentageConditionDefinition resource:
                Add(issues, $"{path}.value", resource.Value);
                return;
        }
    }

    private static void Add(
        ICollection<AuthoredPercentageIssue> issues,
        string path,
        decimal value)
    {
        if (!AuthoredPercentage.IsValid(value))
        {
            issues.Add(new AuthoredPercentageIssue(path, value));
        }
    }
}
