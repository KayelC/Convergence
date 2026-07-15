namespace Convergence.Internal;

internal static class EnumDomain
{
    public static bool IsDefined<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value);

    public static TEnum RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value is not defined for {typeof(TEnum).Name}.");
        }

        return value;
    }
}
