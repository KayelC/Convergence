using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;

namespace JRPGPrototype.Logic.Field.Bridges
{
    /// <summary>
    /// Interactive UI Bridge for Shop operations.
    /// Handles menu loops, user input via IGameIO, and coordinates with the ShopEngine.
    /// </summary>
    public class ShopUIBridge
    {
        private readonly IGameIO _io;
        private readonly IFieldMessenger _messenger;
        private readonly ShopEngine _engine;
        private readonly EconomyManager _economy;
        private readonly InventoryManager _inventory;

        public ShopUIBridge(
            IGameIO io,
            IFieldMessenger messenger,
            ShopEngine engine,
            EconomyManager economy,
            InventoryManager inventory)
        {
            _io = io;
            _messenger = messenger;
            _engine = engine;
            _economy = economy;
            _inventory = inventory;
        }

        // The main entry point for a shop session.
        public void OpenShop(Combatant player, ShopType shopType)
        {
            int shopIndex = 0;
            string title = shopType.ToString().ToUpper() + " SHOP";

            while (true)
            {
                ShopSessionCommandResult command = SelectShopSessionCommand(title, shopIndex);
                if (command.Kind is ShopSessionCommandKind.Back or ShopSessionCommandKind.Exit)
                {
                    return;
                }

                shopIndex = command.SelectedIndex;

                if (command.Kind == ShopSessionCommandKind.Buy) BuyMenu(player, shopType);
                else if (command.Kind == ShopSessionCommandKind.Sell) SellMenu(player, shopType);
            }
        }

        private void BuyMenu(Combatant player, ShopType shopType)
        {
            int listIndex = 0;
            ShopCategory targetCategory = MapTypeToCategory(shopType);

            var filteredStock = Database.ShopInventory
                .Where(e => e.Category == targetCategory)
                .ToList();

            if (filteredStock.Count == 0)
            {
                _messenger.Publish("This shop has no stock.", ConsoleColor.Gray, 800);
                return;
            }

            while (true)
            {
                ShopOfferSelectionResult selection = SelectBuyOffer(player, shopType, filteredStock, listIndex);
                if (selection.Kind != ShopSelectionResultKind.Selected || selection.Offer is null)
                {
                    return;
                }

                listIndex = selection.Offer.Index;
                int finalPrice = _engine.CalculateBuyPrice(selection.Offer.Entry, player);

                if (ConfirmTransactionDetailed(selection.Offer.Name, finalPrice, isBuying: true).Kind ==
                    ShopTransactionConfirmationKind.Confirmed)
                {
                    _engine.ExecutePurchaseDetailed(selection.Offer.Entry, player);
                }
            }
        }

        private void SellMenu(Combatant player, ShopType shopType)
        {
            int listIndex = 0;
            ShopCategory targetCategory = MapTypeToCategory(shopType);

            while (true)
            {
                List<object> sellables = GetSellableObjects(targetCategory);
                if (sellables.Count == 0)
                {
                    _messenger.Publish("Nothing to sell in this category.", ConsoleColor.Gray, 1000);
                    return;
                }

                if (listIndex >= sellables.Count) listIndex = Math.Max(0, sellables.Count - 1);

                ShopOfferSelectionResult selection = SelectSellOffer(player, shopType, targetCategory, sellables, listIndex);
                if (selection.Kind != ShopSelectionResultKind.Selected || selection.Offer is null)
                {
                    return;
                }

                listIndex = selection.Offer.Index;
                if (ConfirmTransactionDetailed(selection.Offer.Name, selection.Offer.DisplayedPrice, isBuying: false).Kind ==
                    ShopTransactionConfirmationKind.Confirmed)
                {
                    _engine.ExecuteSaleDetailed(selection.Offer.ContentId, targetCategory, player);
                }
            }
        }

        #region Helpers and UI Coordination

        internal ShopSessionCommandResult SelectShopSessionCommand(string title, int initialIndex)
        {
            string header = $"--- {title} ---\nMacca: {_economy.Macca}";
            List<string> options = new List<string> { "Buy", "Sell", "Exit" };

            int choice = _io.RenderMenu(header, options, initialIndex);
            return choice switch
            {
                -1 => ShopSessionCommandResult.Back,
                0 => ShopSessionCommandResult.Buy(choice),
                1 => ShopSessionCommandResult.Sell(choice),
                2 => ShopSessionCommandResult.Exit(choice),
                _ => ShopSessionCommandResult.Unavailable
            };
        }

        internal ShopOfferSelectionResult SelectBuyOffer(
            Combatant player,
            ShopType shopType,
            List<ShopEntry> filteredStock,
            int initialIndex)
        {
            List<ShopOfferPresentation> offers = filteredStock
                .Select((entry, index) => new ShopOfferPresentation(
                    entry,
                    entry.Id,
                    entry.Name,
                    entry.Category,
                    index,
                    entry.BasePrice,
                    $"{entry.Name,-18} {entry.BasePrice,5} M"))
                .ToList();
            if (offers.Count == 0)
            {
                return ShopOfferSelectionResult.Unavailable;
            }

            List<string> options = offers.Select(offer => offer.Label).ToList();
            string header = $"--- BUY ({shopType}) ---\nMacca: {_economy.Macca}";

            int idx = _io.RenderMenu(header, options, initialIndex, null, (index) =>
            {
                if (index >= 0 && index < offers.Count)
                {
                    ShowItemInspectionDetailed(offers[index].Entry, player, isBuying: true);
                }
            });

            return idx == -1
                ? ShopOfferSelectionResult.Back
                : ShopOfferSelectionResult.Selected(offers[idx]);
        }

        internal ShopOfferSelectionResult SelectSellOffer(
            Combatant player,
            ShopType shopType,
            ShopCategory targetCategory,
            List<object> sellables,
            int initialIndex)
        {
            List<ShopOfferPresentation> offers = BuildSellOffers(player, targetCategory, sellables);
            if (offers.Count == 0)
            {
                return ShopOfferSelectionResult.Unavailable;
            }

            List<string> options = offers.Select(offer => offer.Label).ToList();
            List<bool> disabled = offers.Select(offer => offer.IsEquipped).ToList();
            string header = $"--- SELL ({shopType}) ---\nMacca: {_economy.Macca}";

            int idx = _io.RenderMenu(header, options, initialIndex, disabled, (index) =>
            {
                _messenger.Publish("Selling gives 50% value + Luck Bonus.");
            });

            return idx == -1
                ? ShopOfferSelectionResult.Back
                : ShopOfferSelectionResult.Selected(offers[idx]);
        }

        internal ShopTransactionConfirmationResult ConfirmTransactionDetailed(string name, int price, bool isBuying)
        {
            string verb = isBuying ? "Buy" : "Sell";
            _messenger.Publish($"\n{verb} {name} for {price} M?");

            int choice = _io.RenderMenu("Confirm?", new List<string> { "Yes", "No" }, 0);
            return choice switch
            {
                0 => ShopTransactionConfirmationResult.Confirmed,
                1 => ShopTransactionConfirmationResult.Declined,
                _ => ShopTransactionConfirmationResult.Back
            };
        }

        internal ShopInspectionPresentationResult ShowItemInspectionDetailed(ShopEntry entry, Combatant player, bool isBuying)
        {
            var (desc, stats) = _engine.GetItemDetails(entry);
            int? price = isBuying ? _engine.CalculateBuyPrice(entry, player) : null;
            List<ShopHospitalPresentationEvent> events = new()
            {
                PublishShopEvent($"Info: {desc}"),
                PublishShopEvent($"Stats: {stats}")
            };

            if (isBuying)
            {
                events.Add(PublishShopEvent($"Price: {price} M (Base: {entry.BasePrice})"));
            }

            return new ShopInspectionPresentationResult(desc, stats, price, events);
        }

        private List<ShopOfferPresentation> BuildSellOffers(
            Combatant player,
            ShopCategory category,
            List<object> sellables)
        {
            List<ShopOfferPresentation> offers = new List<ShopOfferPresentation>();
            for (int index = 0; index < sellables.Count; index++)
            {
                object obj = sellables[index];
                string id = GetIdFromObject(obj);
                string name = GetNameFromObject(obj);
                bool equipped = IsEquipped(obj, player);
                int price = _engine.CalculateSellPrice(id, category, player);
                var entry = Database.ShopInventory.FirstOrDefault(e => e.Id == id && e.Category == category)
                    ?? new ShopEntry { Id = id, Name = id, BasePrice = 100, Category = category };

                offers.Add(new ShopOfferPresentation(
                    entry,
                    id,
                    name,
                    category,
                    index,
                    price,
                    $"{name,-15}{(equipped ? " [E]" : "")} ({price} M)",
                    equipped));
            }

            return offers;
        }

        private ShopHospitalPresentationEvent PublishShopEvent(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            _messenger.Publish(message, color, delay, waitForInput, clearScreen);
            return new ShopHospitalPresentationEvent(message, color, delay, waitForInput, clearScreen);
        }

        private List<object> GetSellableObjects(ShopCategory category)
        {
            List<object> list = new List<object>();
            switch (category)
            {
                case ShopCategory.Weapon:
                    foreach (var id in _inventory.OwnedWeapons)
                        if (Database.Weapons.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Armor:
                    foreach (var id in _inventory.OwnedArmor)
                        if (Database.Armors.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Boots:
                    foreach (var id in _inventory.OwnedBoots)
                        if (Database.Boots.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Accessory:
                    foreach (var id in _inventory.OwnedAccessories)
                        if (Database.Accessories.TryGetValue(id, out var o)) list.Add(o);
                    break;
                case ShopCategory.Item:
                    foreach (var id in _inventory.GetAllItemIds())
                        if (Database.Items.TryGetValue(id, out var o)) list.Add(o);
                    break;
            }
            return list;
        }

        private string GetIdFromObject(object obj) => obj switch
        {
            WeaponData w => w.Id,
            ArmorData a => a.Id,
            BootData b => b.Id,
            AccessoryData acc => acc.Id,
            ItemData i => i.Id,
            _ => ""
        };

        private string GetNameFromObject(object obj) => obj switch
        {
            WeaponData w => w.Name,
            ArmorData a => a.Name,
            BootData b => b.Name,
            AccessoryData acc => acc.Name,
            ItemData i => i.Name,
            _ => ""
        };

        private bool IsEquipped(object obj, Combatant p) => obj switch
        {
            WeaponData w => p.EquippedWeapon?.Id == w.Id,
            ArmorData a => p.EquippedArmor?.Id == a.Id,
            BootData b => p.EquippedBoots?.Id == b.Id,
            AccessoryData acc => p.EquippedAccessory?.Id == acc.Id,
            _ => false
        };

        private ShopCategory MapTypeToCategory(ShopType type) => type switch
        {
            ShopType.Weapon => ShopCategory.Weapon,
            ShopType.Item => ShopCategory.Item,
            ShopType.Armor => ShopCategory.Armor,
            ShopType.Boots => ShopCategory.Boots,
            ShopType.Accessory => ShopCategory.Accessory,
            _ => ShopCategory.Item
        };

        #endregion
    }
}
