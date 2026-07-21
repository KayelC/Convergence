namespace Convergence.Internal;

internal static class AuthoredPercentage
{
    public const int Minimum = 0;
    public const int Maximum = 100;

    public static bool IsValid(decimal value) =>
        value is >= Minimum and <= Maximum;

    public static void RequireValid(
        decimal value,
        string parameterName,
        string description)
    {
        if (!IsValid(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{description} must be within {Minimum}-{Maximum}.");
        }
    }

    public static void RequireCombinedMaximum(
        int first,
        int second,
        string parameterName,
        string description)
    {
        RequireValid(first, parameterName, description);
        RequireValid(second, parameterName, description);
        if ((long)first + second > Maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"{description} cannot total more than {Maximum}.");
        }
    }
}
