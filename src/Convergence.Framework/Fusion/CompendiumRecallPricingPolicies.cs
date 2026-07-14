namespace Convergence.Fusion;

public sealed record CompendiumRecallPricingRequest
{
    public CompendiumRecallPricingRequest(
        CompendiumEntrySnapshot entry,
        int? basePrice = null)
    {
        if (basePrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basePrice), "Recall base price cannot be negative.");
        }

        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        BasePrice = basePrice;
    }

    public CompendiumEntrySnapshot Entry { get; }
    public int? BasePrice { get; }
}

public sealed record CompendiumRecallPricingDecision
{
    public CompendiumRecallPricingDecision(
        bool isAvailable,
        int cost = 0,
        string? rejectionMessage = null)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Recall cost cannot be negative.");
        }
        if (!isAvailable && cost != 0)
        {
            throw new ArgumentException("Unavailable recall pricing cannot carry a cost.", nameof(cost));
        }

        IsAvailable = isAvailable;
        Cost = cost;
        RejectionMessage = string.IsNullOrWhiteSpace(rejectionMessage) ? null : rejectionMessage;
    }

    public bool IsAvailable { get; }
    public int Cost { get; }
    public string? RejectionMessage { get; }

    public static CompendiumRecallPricingDecision Available(int cost) => new(true, cost);

    public static CompendiumRecallPricingDecision Unavailable(string? message = null) =>
        new(false, rejectionMessage: message);
}

public interface ICompendiumRecallPricingPolicy
{
    CompendiumRecallPricingDecision GetPricing(CompendiumRecallPricingRequest request);
}

public sealed class FixedCompendiumRecallPricingPolicy : ICompendiumRecallPricingPolicy
{
    private readonly int _cost;

    public FixedCompendiumRecallPricingPolicy(int cost)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Recall cost cannot be negative.");
        }

        _cost = cost;
    }

    public CompendiumRecallPricingDecision GetPricing(CompendiumRecallPricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompendiumRecallPricingDecision.Available(_cost);
    }
}

public sealed class LinearCompendiumRecallPricingPolicy : ICompendiumRecallPricingPolicy
{
    private readonly int _defaultBasePrice;
    private readonly int _levelFactor;
    private readonly int _statPointFactor;
    private readonly int _skillFactor;

    public LinearCompendiumRecallPricingPolicy(
        int defaultBasePrice,
        int levelFactor,
        int statPointFactor,
        int skillFactor)
    {
        if (defaultBasePrice < 0) throw new ArgumentOutOfRangeException(nameof(defaultBasePrice));
        if (levelFactor < 0) throw new ArgumentOutOfRangeException(nameof(levelFactor));
        if (statPointFactor < 0) throw new ArgumentOutOfRangeException(nameof(statPointFactor));
        if (skillFactor < 0) throw new ArgumentOutOfRangeException(nameof(skillFactor));

        _defaultBasePrice = defaultBasePrice;
        _levelFactor = levelFactor;
        _statPointFactor = statPointFactor;
        _skillFactor = skillFactor;
    }

    public CompendiumRecallPricingDecision GetPricing(CompendiumRecallPricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        long total = request.BasePrice ?? _defaultBasePrice;
        total = checked(total + ((long)request.Entry.Level * _levelFactor));
        total = checked(total + (request.Entry.Stats.Values.Sum(value => (long)value) * _statPointFactor));
        total = checked(total + ((long)request.Entry.SkillIds.Count * _skillFactor));
        if (total is < 0 or > int.MaxValue)
        {
            throw new OverflowException("Compendium recall cost exceeds the supported integer range.");
        }

        return CompendiumRecallPricingDecision.Available((int)total);
    }
}
