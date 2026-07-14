using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Core
{
    internal sealed class LegacyInventoryResourceAdapter
    {
        public static LegacyInventoryResourceAdapter Shared { get; } = new(
            LegacyRuntimeIdentityRegistry.Shared,
            new InventoryTransitionService(),
            new EquipmentTransitionService(),
            new EconomyTransactionService(),
            new ShopTransactionService(),
            new HospitalRestorationService());

        private readonly LegacyRuntimeIdentityRegistry _ids;
        private readonly IInventoryTransitionService _inventory;
        private readonly IEquipmentTransitionService _equipment;
        private readonly IEconomyTransactionService _economy;
        private readonly IShopTransactionService _shop;
        private readonly IHospitalRestorationService _hospital;

        public LegacyInventoryResourceAdapter(
            LegacyRuntimeIdentityRegistry ids,
            IInventoryTransitionService inventory,
            IEquipmentTransitionService equipment,
            IEconomyTransactionService economy,
            IShopTransactionService shop,
            IHospitalRestorationService hospital)
        {
            _ids = ids;
            _inventory = inventory;
            _equipment = equipment;
            _economy = economy;
            _shop = shop;
            _hospital = hospital;
        }

        public RuntimeInventorySnapshot Snapshot(InventoryManager inventory) =>
            new(
                inventory.ItemQuantities.Select(pair => new KeyValuePair<ContentId, int>(Id(pair.Key), pair.Value)),
                [
                    Slot(EquipmentSlot.Weapon, inventory.OwnedWeapons),
                    Slot(EquipmentSlot.Armor, inventory.OwnedArmor),
                    Slot(EquipmentSlot.Boots, inventory.OwnedBoots),
                    Slot(EquipmentSlot.Accessory, inventory.OwnedAccessories)
                ]);

        public RuntimeWalletSnapshot Snapshot(EconomyManager economy) => new(economy.Macca);

        public RuntimeEquipmentSnapshot SnapshotEquipment(Combatant actor)
        {
            List<KeyValuePair<EquipmentSlot, ContentId>> equipped = [];
            if (!string.IsNullOrWhiteSpace(actor.EquippedWeapon?.Id))
            {
                equipped.Add(new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Weapon, Id(actor.EquippedWeapon.Id)));
            }
            if (!string.IsNullOrWhiteSpace(actor.EquippedArmor?.Id))
            {
                equipped.Add(new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Armor, Id(actor.EquippedArmor.Id)));
            }
            if (!string.IsNullOrWhiteSpace(actor.EquippedBoots?.Id))
            {
                equipped.Add(new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Boots, Id(actor.EquippedBoots.Id)));
            }
            if (!string.IsNullOrWhiteSpace(actor.EquippedAccessory?.Id))
            {
                equipped.Add(new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Accessory, Id(actor.EquippedAccessory.Id)));
            }

            return new RuntimeEquipmentSnapshot(equipped);
        }

        public int CalculateBuyPrice(ShopEntry entry, int luck) =>
            _shop.CalculateBuyPrice(entry.BasePrice, luck);

        public int CalculateSellPrice(int basePrice, int luck) =>
            _shop.CalculateSellPrice(basePrice, luck);

        public bool AddItem(InventoryManager inventory, string itemId, int quantity, int? stackLimit = null)
        {
            if (!Database.Items.ContainsKey(itemId))
            {
                return false;
            }

            InventoryTransitionResult result = _inventory.AddItem(Snapshot(inventory), Id(itemId), quantity, stackLimit);
            return Apply(inventory, result);
        }

        public bool RemoveItem(InventoryManager inventory, string itemId, int quantity)
        {
            InventoryTransitionResult result = _inventory.RemoveItem(Snapshot(inventory), Id(itemId), quantity);
            return Apply(inventory, result);
        }

        public bool AddEquipment(InventoryManager inventory, string equipmentId, ShopCategory category)
        {
            if (!EquipmentExists(equipmentId, category) || !TryMapSlot(category, out EquipmentSlot slot))
            {
                return false;
            }

            InventoryTransitionResult result = _inventory.AddEquipment(Snapshot(inventory), Id(equipmentId), slot);
            return Apply(inventory, result);
        }

        public bool RemoveEquipment(InventoryManager inventory, string equipmentId, ShopCategory category, Combatant? owner = null)
        {
            if (!TryMapSlot(category, out EquipmentSlot slot))
            {
                return false;
            }

            RuntimeEquipmentSnapshot? equipped = owner is null ? null : SnapshotEquipment(owner);
            InventoryTransitionResult result = _inventory.RemoveEquipment(Snapshot(inventory), Id(equipmentId), slot, equipped);
            return Apply(inventory, result);
        }

        public bool AddMacca(EconomyManager economy, int amount)
        {
            WalletTransactionResult result = _economy.Credit(Snapshot(economy), amount);
            return Apply(economy, result);
        }

        public bool SpendMacca(EconomyManager economy, int amount)
        {
            WalletTransactionResult result = _economy.Debit(Snapshot(economy), amount);
            return Apply(economy, result);
        }

        public ShopTransactionResult ExecutePurchase(
            InventoryManager inventory,
            EconomyManager economy,
            ShopEntry entry,
            int buyerLuck)
        {
            ShopTransactionResult result = _shop.Buy(
                Snapshot(inventory),
                Snapshot(economy),
                Offer(entry),
                buyerLuck);
            Apply(inventory, result.AfterInventory);
            Apply(economy, result.AfterWallet);
            return result;
        }

        public ShopTransactionResult ExecuteSale(
            InventoryManager inventory,
            EconomyManager economy,
            ShopEntry entry,
            int sellerLuck,
            Combatant owner)
        {
            ShopTransactionResult result = _shop.Sell(
                Snapshot(inventory),
                Snapshot(economy),
                Offer(entry),
                sellerLuck,
                SnapshotEquipment(owner));
            Apply(inventory, result.AfterInventory);
            Apply(economy, result.AfterWallet);
            return result;
        }

        public bool Equip(InventoryManager inventory, Combatant actor, string equipmentId, ShopCategory category)
        {
            if (!TryMapSlot(category, out EquipmentSlot slot))
            {
                return false;
            }

            EquipmentTransitionResult result = _equipment.Equip(
                Snapshot(inventory),
                SnapshotEquipment(actor),
                Id(equipmentId),
                slot,
                slot);
            if (!result.Applied)
            {
                return false;
            }

            return true;
        }

        public int CalculateRestorationCost(Combatant patient) =>
            _hospital.CalculateRestorationCost(Patient(patient));

        public HospitalRestorationResult Restore(EconomyManager economy, Combatant patient)
        {
            HospitalRestorationResult result = _hospital.Restore(Patient(patient), Snapshot(economy));
            if (!result.Applied)
            {
                return result;
            }

            Apply(economy, result.AfterWallet);
            patient.CurrentHP = result.AfterPatient.CurrentHp;
            patient.CurrentSP = result.AfterPatient.CurrentSp;
            patient.RemoveAilment();
            patient.ClearEncounterPersistence();
            return result;
        }

        private bool Apply(InventoryManager inventory, InventoryTransitionResult result)
        {
            if (!result.Applied)
            {
                return false;
            }

            Apply(inventory, result.After);
            return true;
        }

        private static bool Apply(EconomyManager economy, WalletTransactionResult result)
        {
            if (!result.Applied)
            {
                return false;
            }

            Apply(economy, result.After);
            return true;
        }

        private static void Apply(InventoryManager inventory, RuntimeInventorySnapshot snapshot)
        {
            Dictionary<string, int> items = snapshot.ItemQuantities.ToDictionary(
                pair => LegacyId(pair.Key),
                pair => pair.Value,
                StringComparer.Ordinal);

            inventory.ReplaceState(
                items,
                snapshot.GetEquipmentIds(EquipmentSlot.Weapon).Select(LegacyId).ToList(),
                snapshot.GetEquipmentIds(EquipmentSlot.Armor).Select(LegacyId).ToList(),
                snapshot.GetEquipmentIds(EquipmentSlot.Boots).Select(LegacyId).ToList(),
                snapshot.GetEquipmentIds(EquipmentSlot.Accessory).Select(LegacyId).ToList());
        }

        private static void Apply(EconomyManager economy, RuntimeWalletSnapshot snapshot) =>
            economy.ReplaceMacca(snapshot.Balance);

        private static RuntimeShopOfferSnapshot Offer(ShopEntry entry) =>
            new(
                entry.Category == ShopCategory.Item ? ShopContentKind.Item : ShopContentKind.Equipment,
                Id(entry.Id),
                entry.BasePrice,
                TryMapSlot(entry.Category, out EquipmentSlot slot) ? slot : null);

        private RuntimeHospitalPatientSnapshot Patient(Combatant patient) =>
            new(
                _ids.GetActorId(patient),
                patient.CurrentHP,
                patient.MaxHP,
                patient.CurrentSP,
                patient.MaxSP,
                patient.CurrentAilment is not null,
                patient.Buffs.Count > 0 || patient.BrokenAffinities.Count > 0 || patient.HasSwappedThisTurn);

        private static KeyValuePair<EquipmentSlot, IEnumerable<ContentId>> Slot(
            EquipmentSlot slot,
            IEnumerable<string> ids) =>
            new(slot, ids.Select(Id));

        private static ContentId Id(string value) => LegacyContentIdCodec.Encode(value);

        private static string LegacyId(ContentId id) => LegacyContentIdCodec.Decode(id);

        private static bool TryMapSlot(ShopCategory category, out EquipmentSlot slot)
        {
            switch (category)
            {
                case ShopCategory.Weapon:
                    slot = EquipmentSlot.Weapon;
                    return true;
                case ShopCategory.Armor:
                    slot = EquipmentSlot.Armor;
                    return true;
                case ShopCategory.Boots:
                    slot = EquipmentSlot.Boots;
                    return true;
                case ShopCategory.Accessory:
                    slot = EquipmentSlot.Accessory;
                    return true;
                default:
                    slot = default;
                    return false;
            }
        }

        private static bool EquipmentExists(string equipmentId, ShopCategory category) => category switch
        {
            ShopCategory.Weapon => Database.Weapons.ContainsKey(equipmentId),
            ShopCategory.Armor => Database.Armors.ContainsKey(equipmentId),
            ShopCategory.Boots => Database.Boots.ContainsKey(equipmentId),
            ShopCategory.Accessory => Database.Accessories.ContainsKey(equipmentId),
            _ => false
        };
    }
}
