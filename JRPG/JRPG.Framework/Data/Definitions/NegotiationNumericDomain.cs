namespace JRPGPrototype.Data.Definitions;

/// <summary>
/// Defines representation limits for negotiation aggregates without imposing balance-specific score caps.
/// </summary>
public static class NegotiationNumericDomain
{
    public const int MinimumMoodScore = int.MinValue;
    public const int MaximumMoodScore = int.MaxValue;
    public const int MaximumDemandWeightTotal = int.MaxValue;

    public static int AddMoodScore(int current, int adjustment)
    {
        long total = (long)current + adjustment;
        return total switch
        {
            > MaximumMoodScore => MaximumMoodScore,
            < MinimumMoodScore => MinimumMoodScore,
            _ => (int)total
        };
    }

    public static bool TrySumDemandWeights(IEnumerable<int> weights, out int total)
    {
        ArgumentNullException.ThrowIfNull(weights);

        long aggregate = 0;
        foreach (int weight in weights)
        {
            if (weight <= 0 || aggregate > MaximumDemandWeightTotal - (long)weight)
            {
                total = 0;
                return false;
            }

            aggregate += weight;
        }

        total = (int)aggregate;
        return true;
    }
}
