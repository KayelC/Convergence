using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexShopTransactionEvidence(
    ContentId ShopId,
    ContentId OfferId,
    ShopContentKind ContentKind,
    bool IsPurchase,
    ResourceTransactionCode Code,
    int Price,
    int WalletBefore,
    int WalletAfter,
    int OwnedCountBefore,
    int OwnedCountAfter,
    EquipmentSlot? EquipmentSlot);

internal sealed record TrainingAnnexEquipmentChangeEvidence(
    ContentId EquipmentId,
    EquipmentSlot Slot,
    ResourceTransactionCode Code,
    bool Applied);

internal sealed record TrainingAnnexShopInteractionResult(
    RuntimeWalletSnapshot Wallet,
    IReadOnlyList<TrainingAnnexShopTransactionEvidence> Transactions,
    IReadOnlyList<TrainingAnnexEquipmentChangeEvidence> EquipmentChanges,
    IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> OfferDiagnostics);

internal sealed record TrainingAnnexResolvedShopOffer(
    ShopOfferDefinition Definition,
    RuntimeShopOfferSnapshot Runtime,
    string DisplayName,
    string Description);

internal sealed record TrainingAnnexShopOfferResolutionResult(
    IReadOnlyList<TrainingAnnexResolvedShopOffer> Offers,
    IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> Diagnostics);

internal sealed class TrainingAnnexShopController
{
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;

    public TrainingAnnexShopController(
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
    }

    public async ValueTask<TrainingAnnexShopInteractionResult> OpenTrainingSupplyAsync(
        GameDataCatalog catalog,
        IShopTransactionService shopTransactions,
        IEquipmentTransitionService equipmentTransitions,
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        TrainingAnnexRuntimeActor player,
        TrainingAnnexItemActionInventory inventory,
        RuntimeWalletSnapshot wallet,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        ShopCatalogDefinition shop = catalog.GetRequiredShop(TrainingAnnexHostSupport.TrainingSupply);
        RuntimeActorSnapshot playerSnapshot = player.Actor.State.ToSnapshot();
        int luck = StatAsInt(playerSnapshot, StandardProgressionIds.Luck);
        TrainingAnnexShopOfferResolutionResult offerResolution = ResolveShopOffers(catalog, shop);
        IReadOnlyList<TrainingAnnexResolvedShopOffer> offers = offerResolution.Offers;
        IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> offerDiagnostics = offerResolution.Diagnostics;
        var transactionEvidence = new List<TrainingAnnexShopTransactionEvidence>();
        var equipmentEvidence = new List<TrainingAnnexEquipmentChangeEvidence>();

        await _eventSink.PublishAsync(
            $"Shop opened: {shop.DisplayName}; wallet {wallet.Macca} M.",
            cancellationToken).ConfigureAwait(false);
        foreach (RuntimeShopOfferResolutionDiagnostic diagnostic in offerDiagnostics)
        {
            await _eventSink.PublishAsync(
                $"Shop offer diagnostic: [{diagnostic.Code}] {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }

        HostCommandReadResult<CleanTrainingAnnexPlayCommand> shopCommand =
            await _commandSource.ReadAsync(
                CreateShopSessionMenu(shop, wallet),
                cancellationToken).ConfigureAwait(false);
        if (!shopCommand.IsSelected || shopCommand.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync("Shop closed without transaction.", cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        commands.Add(shopCommand.Command);
        if (shopCommand.Command == CleanTrainingAnnexPlayCommand.ShopBuy)
        {
            return await BuyAsync(
                catalog,
                shop,
                offers,
                shopTransactions,
                equipmentTransitions,
                equipmentProfileResolver,
                player,
                inventory,
                wallet,
                luck,
                commands,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics,
                cancellationToken).ConfigureAwait(false);
        }

        if (shopCommand.Command == CleanTrainingAnnexPlayCommand.ShopSell)
        {
            return await SellAsync(
                shop,
                offers,
                shopTransactions,
                player,
                inventory,
                wallet,
                luck,
                commands,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics,
                cancellationToken).ConfigureAwait(false);
        }

        return new TrainingAnnexShopInteractionResult(wallet, transactionEvidence, equipmentEvidence, offerDiagnostics);
    }

    private async ValueTask<TrainingAnnexShopInteractionResult> BuyAsync(
        GameDataCatalog catalog,
        ShopCatalogDefinition shop,
        IReadOnlyList<TrainingAnnexResolvedShopOffer> offers,
        IShopTransactionService shopTransactions,
        IEquipmentTransitionService equipmentTransitions,
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        TrainingAnnexRuntimeActor player,
        TrainingAnnexItemActionInventory inventory,
        RuntimeWalletSnapshot wallet,
        int luck,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        List<TrainingAnnexShopTransactionEvidence> transactionEvidence,
        List<TrainingAnnexEquipmentChangeEvidence> equipmentEvidence,
        IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> offerDiagnostics,
        CancellationToken cancellationToken)
    {
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> offerSelection =
            await _commandSource.ReadAsync(
                CreateShopBuyMenu(shop, offers, inventory.Snapshot, wallet, shopTransactions, luck),
                cancellationToken).ConfigureAwait(false);
        if (!offerSelection.IsSelected || offerSelection.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync("Shop purchase canceled; wallet and inventory are unchanged.", cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        commands.Add(offerSelection.Command);
        TrainingAnnexResolvedShopOffer? offer = ResolveSelectedOffer(offers, offerSelection);
        if (offer is null)
        {
            await _eventSink.PublishAsync("Shop purchase rejected; selected offer was not available.", cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        ShopTransactionResult purchase = shopTransactions.Buy(
            inventory.Snapshot,
            wallet,
            offer.Runtime,
            luck);
        transactionEvidence.Add(ToShopEvidence(shop.Id, offer.Runtime, purchase, isPurchase: true));
        if (!purchase.Applied)
        {
            await PublishShopTransactionFailureAsync("purchase", offer, purchase, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        inventory.Replace(purchase.AfterInventory);
        wallet = purchase.AfterWallet;
        await PublishShopTransactionSuccessAsync("Bought", offer, purchase, inventory.Snapshot, cancellationToken)
            .ConfigureAwait(false);

        if (offer.Runtime.ContentKind == ShopContentKind.Equipment &&
            offer.Runtime.EquipmentSlot is EquipmentSlot slot)
        {
            await PromptEquipPurchasedEquipmentAsync(
                catalog,
                equipmentTransitions,
                equipmentProfileResolver,
                player,
                inventory,
                offer,
                slot,
                commands,
                equipmentEvidence,
                cancellationToken).ConfigureAwait(false);
        }

        return new TrainingAnnexShopInteractionResult(wallet, transactionEvidence, equipmentEvidence, offerDiagnostics);
    }

    private async ValueTask<TrainingAnnexShopInteractionResult> SellAsync(
        ShopCatalogDefinition shop,
        IReadOnlyList<TrainingAnnexResolvedShopOffer> offers,
        IShopTransactionService shopTransactions,
        TrainingAnnexRuntimeActor player,
        TrainingAnnexItemActionInventory inventory,
        RuntimeWalletSnapshot wallet,
        int luck,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        List<TrainingAnnexShopTransactionEvidence> transactionEvidence,
        List<TrainingAnnexEquipmentChangeEvidence> equipmentEvidence,
        IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> offerDiagnostics,
        CancellationToken cancellationToken)
    {
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> offerSelection =
            await _commandSource.ReadAsync(
                CreateShopSellMenu(
                    shop,
                    offers,
                    inventory.Snapshot,
                    wallet,
                    player.Actor.State.ToSnapshot().Equipment,
                    shopTransactions,
                    luck),
                cancellationToken).ConfigureAwait(false);
        if (!offerSelection.IsSelected || offerSelection.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync("Shop sale canceled; wallet and inventory are unchanged.", cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        commands.Add(offerSelection.Command);
        TrainingAnnexResolvedShopOffer? offer = ResolveSelectedOffer(offers, offerSelection);
        if (offer is null)
        {
            await _eventSink.PublishAsync("Shop sale rejected; selected offer was not available.", cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        ShopTransactionResult sale = shopTransactions.Sell(
            inventory.Snapshot,
            wallet,
            offer.Runtime,
            luck,
            player.Actor.State.ToSnapshot().Equipment);
        transactionEvidence.Add(ToShopEvidence(shop.Id, offer.Runtime, sale, isPurchase: false));
        if (!sale.Applied)
        {
            await PublishShopTransactionFailureAsync("sale", offer, sale, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        inventory.Replace(sale.AfterInventory);
        wallet = sale.AfterWallet;
        await PublishShopTransactionSuccessAsync("Sold", offer, sale, inventory.Snapshot, cancellationToken)
            .ConfigureAwait(false);

        return new TrainingAnnexShopInteractionResult(wallet, transactionEvidence, equipmentEvidence, offerDiagnostics);
    }

    private async ValueTask PromptEquipPurchasedEquipmentAsync(
        GameDataCatalog catalog,
        IEquipmentTransitionService equipmentTransitions,
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        TrainingAnnexRuntimeActor player,
        TrainingAnnexItemActionInventory inventory,
        TrainingAnnexResolvedShopOffer offer,
        EquipmentSlot slot,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        List<TrainingAnnexEquipmentChangeEvidence> equipmentEvidence,
        CancellationToken cancellationToken)
    {
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> equipSelection =
            await _commandSource.ReadAsync(
                CreateEquipPurchasedEquipmentMenu(offer, slot),
                cancellationToken).ConfigureAwait(false);
        if (!equipSelection.IsSelected || equipSelection.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync(
                $"Equipment purchase kept in inventory: {offer.DisplayName}.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        commands.Add(equipSelection.Command);
        EquipmentTransitionResult equipResult = equipmentTransitions.Equip(
            inventory.Snapshot,
            player.Actor.State.ToSnapshot().Equipment,
            offer.Runtime.ContentId,
            slot,
            slot);
        equipmentEvidence.Add(new TrainingAnnexEquipmentChangeEvidence(
            offer.Runtime.ContentId,
            slot,
            equipResult.Code,
            equipResult.Applied));
        if (equipResult.Applied)
        {
            player.Actor.State.ReplaceEquipment(equipResult.After);
            RuntimeEquipmentProfile profile = equipmentProfileResolver.Resolve(
                equipResult.After,
                catalog);
            string slots = string.Join(
                ", ",
                profile.EquippedDefinitions
                    .OrderBy(pair => pair.Key)
                    .Select(pair => $"{pair.Key}: {pair.Value.DisplayName}"));
            await _eventSink.PublishAsync(
                $"Equipped {offer.DisplayName} in {slot}; equipment profile now [{slots}].",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string diagnostics = string.Join(
            "; ",
            equipResult.Diagnostics.Select(diagnostic => diagnostic.Message));
        await _eventSink.PublishAsync(
            $"Equipment equip rejected: {offer.DisplayName}; {diagnostics}",
            cancellationToken).ConfigureAwait(false);
    }

    private static int StatAsInt(RuntimeActorSnapshot snapshot, ContentId statId)
    {
        decimal value = snapshot.Stats.EffectiveStats.TryGetValue(statId, out decimal effective)
            ? effective
            : snapshot.Stats.BaseStats.GetValueOrDefault(statId);
        return (int)Math.Floor(value);
    }

    private static TrainingAnnexShopOfferResolutionResult ResolveShopOffers(
        GameDataCatalog catalog,
        ShopCatalogDefinition shop)
    {
        var resolver = new RuntimeShopOfferResolver();
        var offers = new List<TrainingAnnexResolvedShopOffer>();
        var diagnostics = new List<RuntimeShopOfferResolutionDiagnostic>();
        foreach (ShopOfferDefinition offer in shop.Offers)
        {
            RuntimeShopOfferResolutionResult resolved = resolver.Resolve(offer, catalog, catalog);
            if (!resolved.IsSuccess || resolved.Offer is null)
            {
                diagnostics.AddRange(resolved.Diagnostics);
                continue;
            }

            (string displayName, string description) = ResolveShopOfferText(catalog, offer);
            offers.Add(new TrainingAnnexResolvedShopOffer(
                offer,
                resolved.Offer,
                displayName,
                description));
        }

        return new TrainingAnnexShopOfferResolutionResult(offers, diagnostics);
    }

    private static (string DisplayName, string Description) ResolveShopOfferText(
        GameDataCatalog catalog,
        ShopOfferDefinition offer)
    {
        if (offer.ContentKind == ShopContentKind.Item &&
            catalog.TryGetItem(offer.ContentId, out ItemDefinition? item) &&
            item is not null)
        {
            return (item.DisplayName, item.Description);
        }

        if (offer.ContentKind == ShopContentKind.Equipment &&
            catalog.TryGetEquipment(offer.ContentId, out EquipmentDefinition? equipment) &&
            equipment is not null)
        {
            return (equipment.DisplayName, equipment.Description);
        }

        return (offer.ContentId.ToString(), string.Empty);
    }

    private static TrainingAnnexResolvedShopOffer? ResolveSelectedOffer(
        IEnumerable<TrainingAnnexResolvedShopOffer> offers,
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection) =>
        selection.SelectionIdentity?.ContentId is ContentId selected
            ? offers.FirstOrDefault(offer => offer.Runtime.ContentId == selected)
            : null;

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateShopSessionMenu(
        ShopCatalogDefinition shop,
        RuntimeWalletSnapshot wallet) =>
        new(
            $"{shop.DisplayName} - Wallet {wallet.Macca} M",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ShopBuy,
                    "Buy"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ShopSell,
                    "Sell"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateShopBuyMenu(
        ShopCatalogDefinition shop,
        IEnumerable<TrainingAnnexResolvedShopOffer> offers,
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        IShopTransactionService shopTransactions,
        int luck)
    {
        List<HostCommandOption<CleanTrainingAnnexPlayCommand>> options = offers
            .Select(offer =>
            {
                ShopTransactionResult assessment = shopTransactions.Buy(inventory, wallet, offer.Runtime, luck);
                int price = shopTransactions.CalculateBuyPrice(offer.Runtime.BasePrice, luck);
                return new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SelectShopOffer,
                    $"{offer.DisplayName} - {price} M{StockLabel(offer.Runtime)}{TransactionLabel(assessment)}",
                    assessment.Applied,
                    offer.Description,
                    HostCommandSelectionIdentity.ForContent(offer.Runtime.ContentId));
            })
            .ToList();

        if (options.Count == 0)
        {
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.SelectShopOffer,
                "No shop offers",
                false));
        }

        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.Back,
            "Back"));
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            $"{shop.DisplayName} - Buy",
            options);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateShopSellMenu(
        ShopCatalogDefinition shop,
        IEnumerable<TrainingAnnexResolvedShopOffer> offers,
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeEquipmentSnapshot equipment,
        IShopTransactionService shopTransactions,
        int luck)
    {
        List<HostCommandOption<CleanTrainingAnnexPlayCommand>> options = offers
            .Where(offer => PlayerHasSellableContent(inventory, offer.Runtime))
            .Select(offer =>
            {
                ShopTransactionResult assessment = shopTransactions.Sell(
                    inventory,
                    wallet,
                    offer.Runtime,
                    luck,
                    equipment);
                int price = shopTransactions.CalculateSellPrice(offer.Runtime.BasePrice, luck);
                return new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SelectSellOffer,
                    $"{offer.DisplayName} - {price} M{OwnedLabel(inventory, offer.Runtime)}{TransactionLabel(assessment)}",
                    assessment.Applied,
                    offer.Description,
                    HostCommandSelectionIdentity.ForContent(offer.Runtime.ContentId));
            })
            .ToList();

        if (options.Count == 0)
        {
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.SelectSellOffer,
                "Nothing to sell",
                false));
        }

        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.Back,
            "Back"));
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            $"{shop.DisplayName} - Sell",
            options);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateEquipPurchasedEquipmentMenu(
        TrainingAnnexResolvedShopOffer offer,
        EquipmentSlot slot) =>
        new(
            $"Equip {offer.DisplayName}?",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.EquipPurchasedEquipment,
                    $"Equip to {slot}"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Keep in Inventory")
            ]);

    private static bool PlayerHasSellableContent(
        RuntimeInventorySnapshot inventory,
        RuntimeShopOfferSnapshot offer) =>
        offer.ContentKind switch
        {
            ShopContentKind.Item => inventory.GetQuantity(offer.ContentId) > 0,
            ShopContentKind.Equipment when offer.EquipmentSlot is EquipmentSlot slot =>
                inventory.OwnsEquipment(offer.ContentId, slot),
            _ => false
        };

    private static string StockLabel(RuntimeShopOfferSnapshot offer) =>
        offer.StockAvailable is int stock ? $" (stock {stock})" : string.Empty;

    private static string OwnedLabel(RuntimeInventorySnapshot inventory, RuntimeShopOfferSnapshot offer) =>
        offer.ContentKind switch
        {
            ShopContentKind.Item => $" (owned {inventory.GetQuantity(offer.ContentId)})",
            ShopContentKind.Equipment when offer.EquipmentSlot is EquipmentSlot slot &&
                                           inventory.OwnsEquipment(offer.ContentId, slot) => " (owned)",
            _ => string.Empty
        };

    private static string TransactionLabel(ShopTransactionResult result) =>
        result.Applied
            ? string.Empty
            : $" [{TransactionReason(result.Code)}]";

    private static string TransactionReason(ResourceTransactionCode code) =>
        code switch
        {
            ResourceTransactionCode.InsufficientCurrency => "Not enough Macca",
            ResourceTransactionCode.EquipmentDuplicate => "Already owned",
            ResourceTransactionCode.EquippedItemCannotBeRemoved => "Equipped",
            ResourceTransactionCode.ItemStackExceeded => "Stack full",
            ResourceTransactionCode.ShopStockUnavailable => "Out of stock",
            ResourceTransactionCode.ItemMissing or ResourceTransactionCode.EquipmentNotOwned => "Not owned",
            _ => code.ToString()
        };

    private static TrainingAnnexShopTransactionEvidence ToShopEvidence(
        ContentId shopId,
        RuntimeShopOfferSnapshot offer,
        ShopTransactionResult result,
        bool isPurchase) =>
        new(
            shopId,
            offer.ContentId,
            offer.ContentKind,
            isPurchase,
            result.Code,
            result.Price,
            result.BeforeWallet.Macca,
            result.AfterWallet.Macca,
            OwnedCount(result.BeforeInventory, offer),
            OwnedCount(result.AfterInventory, offer),
            offer.EquipmentSlot);

    private static int OwnedCount(RuntimeInventorySnapshot inventory, RuntimeShopOfferSnapshot offer) =>
        offer.ContentKind switch
        {
            ShopContentKind.Item => inventory.GetQuantity(offer.ContentId),
            ShopContentKind.Equipment when offer.EquipmentSlot is EquipmentSlot slot =>
                inventory.OwnsEquipment(offer.ContentId, slot) ? 1 : 0,
            _ => 0
        };

    private async ValueTask PublishShopTransactionFailureAsync(
        string action,
        TrainingAnnexResolvedShopOffer offer,
        ShopTransactionResult result,
        CancellationToken cancellationToken)
    {
        string diagnostics = string.Join(
            "; ",
            result.Diagnostics.Select(diagnostic => diagnostic.Message));
        await _eventSink.PublishAsync(
            $"Shop {action} rejected: {offer.DisplayName}; {diagnostics}",
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask PublishShopTransactionSuccessAsync(
        string action,
        TrainingAnnexResolvedShopOffer offer,
        ShopTransactionResult result,
        RuntimeInventorySnapshot inventory,
        CancellationToken cancellationToken)
    {
        string owned = offer.Runtime.ContentKind == ShopContentKind.Item
            ? $"quantity {result.BeforeInventory.GetQuantity(offer.Runtime.ContentId)}->{inventory.GetQuantity(offer.Runtime.ContentId)}"
            : $"owned {OwnedCount(result.BeforeInventory, offer.Runtime)}->{OwnedCount(inventory, offer.Runtime)}";
        await _eventSink.PublishAsync(
            $"Shop transaction: {action} {offer.DisplayName} for {result.Price} M; wallet {result.BeforeWallet.Macca}->{result.AfterWallet.Macca}; {owned}.",
            cancellationToken).ConfigureAwait(false);
    }
}
