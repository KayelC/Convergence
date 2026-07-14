using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Runtime;

namespace JRPGPrototype.Logic.Battle.Engines;

internal sealed class LegacyNegotiationSessionPolicy : INegotiationSessionPolicy
{
    internal static ContentId BlockedContextId { get; } = ContentId.Parse("legacy_full_moon_blocked");

    public int QuestionLimit => 3;
    public int PositiveMoodThreshold => 4;
    public int NeutralMoodThreshold => 1;

    public NegotiationGateDecision EvaluateGate(NegotiationSessionRequest request) =>
        request.ContextIds.Contains(BlockedContextId)
            ? new NegotiationGateDecision(false, NegotiationOutcomeReason.PolicyBlocked)
            : new NegotiationGateDecision(true);

    public bool CanBegin(NegotiationSessionRequest request, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        return request.ActiveOpponentCount switch
        {
            <= 1 => true,
            2 => random.NextInt32(0, 100) < 75,
            3 => random.NextInt32(0, 100) < 50,
            _ => random.NextInt32(0, 100) < 25
        };
    }

    public NegotiationFamiliarGift SelectFamiliarGift(
        NegotiationSessionRequest request,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        int roll = random.NextInt32(0, 100);
        if (roll < 50)
        {
            return new NegotiationFamiliarGift(
                NegotiationFamiliarGiftKind.Item,
                ItemId: "101",
                Quantity: 1);
        }
        if (roll < 80)
        {
            return new NegotiationFamiliarGift(
                NegotiationFamiliarGiftKind.Currency,
                Currency: checked(request.TargetLevel * 20));
        }

        return new NegotiationFamiliarGift(
            NegotiationFamiliarGiftKind.RestoreParty,
            RestorePercent: 0.15m);
    }

    public IReadOnlyList<NegotiationRuntimeDemand> CreateFallbackDemands(
        NegotiationSessionRequest request,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);

        double baseCost = Math.Pow(request.TargetLevel, 2) * 10;
        double luckDiscount = baseCost * (request.ActorLuck / 100.0);
        int currencyAmount = (int)Math.Max(request.TargetLevel * 5, baseCost - luckDiscount);
        var demands = new List<NegotiationRuntimeDemand>
        {
            new(ContentId.Parse("legacy_currency_demand"), NegotiationDemandKind.Currency, 1, currencyAmount)
        };

        NegotiationAvailableItem? item = request.AvailableHealingItems.FirstOrDefault();
        if (item is not null && random.NextInt32(0, 100) < 50)
        {
            demands.Add(new NegotiationRuntimeDemand(
                ContentId.Parse("legacy_item_demand"),
                NegotiationDemandKind.Item,
                1,
                item: item));
        }

        return Array.AsReadOnly(demands.ToArray());
    }

    public bool ResolveDemandlessSuccess(NegotiationSessionRequest request, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        return random.NextInt32(0, 100) < 50;
    }
}
