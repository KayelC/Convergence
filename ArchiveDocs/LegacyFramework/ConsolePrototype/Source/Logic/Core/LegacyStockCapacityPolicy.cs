using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Core;

internal sealed class LegacyStockCapacityPolicy : IStockCapacityPolicy
{
    public int GetCapacity(int ownerLevel)
    {
        if (ownerLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerLevel), "Owner level must be positive.");
        }

        if (ownerLevel < 10) return 3;
        if (ownerLevel < 20) return 5;
        if (ownerLevel < 30) return 7;
        if (ownerLevel < 40) return 10;
        return 12;
    }
}
