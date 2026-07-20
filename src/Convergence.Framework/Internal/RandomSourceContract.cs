using Convergence.Hosting;

namespace Convergence.Internal;

internal static class RandomSourceContract
{
    public static int NextInt32(
        IRandomSource random,
        int minimumInclusive,
        int maximumExclusive)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (minimumInclusive >= maximumExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExclusive),
                maximumExclusive,
                "The exclusive maximum must be greater than the inclusive minimum.");
        }

        int value = random.NextInt32(minimumInclusive, maximumExclusive);
        if (value < minimumInclusive || value >= maximumExclusive)
        {
            throw new InvalidOperationException(
                $"Random sources must return integers within [{minimumInclusive}, {maximumExclusive}); received '{value}'.");
        }

        return value;
    }

    public static decimal NextUnitDecimal(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        decimal value = random.NextUnitDecimal();
        if (value is < 0m or >= 1m)
        {
            throw new InvalidOperationException(
                $"Random sources must return unit decimals within [0, 1); received '{value}'.");
        }

        return value;
    }
}
