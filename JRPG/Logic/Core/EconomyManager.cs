using System;

namespace JRPGPrototype.Logic.Core
{
    public class EconomyManager
    {
        public int Macca { get; private set; } = 0;

        public void AddMacca(int amount)
        {
            LegacyInventoryResourceAdapter.Shared.AddMacca(this, amount);
        }

        public bool SpendMacca(int amount)
        {
            return LegacyInventoryResourceAdapter.Shared.SpendMacca(this, amount);
        }

        internal void ReplaceMacca(int macca)
        {
            Macca = macca;
        }
    }
}
