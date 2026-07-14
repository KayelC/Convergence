using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic.Core
{
    public class InventoryManager
    {
        private Dictionary<string, int> _inventory = new Dictionary<string, int>();
        public List<string> OwnedWeapons { get; private set; } = new List<string>();
        public List<string> OwnedArmor { get; private set; } = new List<string>();
        public List<string> OwnedBoots { get; private set; } = new List<string>();
        public List<string> OwnedAccessories { get; private set; } = new List<string>();
        internal IReadOnlyDictionary<string, int> ItemQuantities => _inventory;

        public void AddItem(string itemId, int quantity)
        {
            LegacyInventoryResourceAdapter.Shared.AddItem(this, itemId, quantity);
        }

        public int GetQuantity(string itemId) => _inventory.ContainsKey(itemId) ? _inventory[itemId] : 0;

        public void RemoveItem(string itemId, int quantity)
        {
            LegacyInventoryResourceAdapter.Shared.RemoveItem(this, itemId, quantity);
        }

        public bool HasItem(string itemId) => GetQuantity(itemId) > 0;
        public List<string> GetAllItemIds() => _inventory.Keys.ToList();

        public void AddEquipment(string id, ShopCategory category)
        {
            LegacyInventoryResourceAdapter.Shared.AddEquipment(this, id, category);
        }

        public void RemoveEquipment(string id, ShopCategory category)
        {
            LegacyInventoryResourceAdapter.Shared.RemoveEquipment(this, id, category);
        }

        internal void ReplaceState(
            Dictionary<string, int> itemQuantities,
            List<string> ownedWeapons,
            List<string> ownedArmor,
            List<string> ownedBoots,
            List<string> ownedAccessories)
        {
            _inventory = itemQuantities;
            OwnedWeapons = ownedWeapons;
            OwnedArmor = ownedArmor;
            OwnedBoots = ownedBoots;
            OwnedAccessories = ownedAccessories;
        }
    }
}
