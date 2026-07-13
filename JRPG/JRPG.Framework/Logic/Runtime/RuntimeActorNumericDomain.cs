using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Runtime;

public static class RuntimeActorNumericDomain
{
    public const decimal MinimumStatValue = 0m;
    public const decimal MaximumStatValue = int.MaxValue;
    public const decimal MinimumBaseResourceValue = 0m;

    public static bool IsValidStatValue(decimal value) =>
        value is >= MinimumStatValue and <= MaximumStatValue;

    public static bool IsValidBaseResourceValue(decimal value) =>
        value >= MinimumBaseResourceValue;

    internal static void RequireValidStatValues(
        IEnumerable<KeyValuePair<ContentId, decimal>> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach ((ContentId statId, decimal value) in values)
        {
            if (!IsValidStatValue(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Stat '{statId}' must be between {MinimumStatValue} and {MaximumStatValue} inclusive.");
            }
        }
    }

    internal static void RequireValidBaseResourceValues(
        IEnumerable<KeyValuePair<ContentId, decimal>> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach ((ContentId resourceId, decimal value) in values)
        {
            if (!IsValidBaseResourceValue(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Base resource '{resourceId}' cannot be negative.");
            }
        }
    }
}
