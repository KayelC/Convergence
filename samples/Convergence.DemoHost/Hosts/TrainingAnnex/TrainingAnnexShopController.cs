using Convergence.Content;
using Convergence.Catalog;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed record TrainingAnnexShopTransactionEvidence(
    ContentId ShopId,
    ContentId OfferId,
    ShopContentKind ContentKind,
    bool IsPurchase,
    ResourceTransactionCode Code,
    int Price,
    int CurrencyLedgerBefore,
    int CurrencyLedgerAfter,
    int OwnedCountBefore,
    int OwnedCountAfter,
    ContentId? EquipmentSlotId,
    RuntimeInstanceId? EquipmentInstanceId);

internal sealed record TrainingAnnexEquipmentChangeEvidence(
    RuntimeInstanceId EquipmentInstanceId,
    ContentId EquipmentId,
    ContentId SlotId,
    ResourceTransactionCode Code,
    bool Applied);

internal sealed record TrainingAnnexShopInteractionResult(
    RuntimeCurrencyLedgerSnapshot CurrencyLedger,
    RuntimeShopStockSnapshot ShopStock,
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
        IRuntimeShopOfferResolver shopOffers,
        IShopTransactionService shopTransactions,
        IEquipmentTransitionService equipmentTransitions,
        IRuntimeActorEquipmentApplicationService equipmentApplication,
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        TrainingAnnexItemActionInventory inventory,
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot shopStock,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        ShopCatalogDefinition shop = catalog.GetRequiredShop(TrainingAnnexHostSupport.TrainingSupply);
        TrainingAnnexRuntimeActor player = roster.Player;
        RuntimeActorSnapshot playerSnapshot = player.Actor.State.ToSnapshot();
        int luck = StatAsInt(playerSnapshot, StandardProgressionIds.Luck);
        TrainingAnnexShopOfferResolutionResult offerResolution =
            ResolveShopOffers(catalog, shop, shopOffers);
        IReadOnlyList<TrainingAnnexResolvedShopOffer> offers = offerResolution.Offers;
        IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> offerDiagnostics = offerResolution.Diagnostics;
        var transactionEvidence = new List<TrainingAnnexShopTransactionEvidence>();
        var equipmentEvidence = new List<TrainingAnnexEquipmentChangeEvidence>();

        await _eventSink.PublishAsync(
            $"Shop opened: {shop.DisplayName}; wallet " +
            $"{TrainingAnnexHostSupport.GetCreditsBalance(wallet)} C.",
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
                shopStock,
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
                equipmentApplication,
                roster,
                partyRoster,
                inventory,
                wallet,
                shopStock,
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
                shopStock,
                luck,
                commands,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics,
                cancellationToken).ConfigureAwait(false);
        }

        return new TrainingAnnexShopInteractionResult(
            wallet,
            shopStock,
            transactionEvidence,
            equipmentEvidence,
            offerDiagnostics);
    }

    private async ValueTask<TrainingAnnexShopInteractionResult> BuyAsync(
        GameDataCatalog catalog,
        ShopCatalogDefinition shop,
        IReadOnlyList<TrainingAnnexResolvedShopOffer> offers,
        IShopTransactionService shopTransactions,
        IEquipmentTransitionService equipmentTransitions,
        IRuntimeActorEquipmentApplicationService equipmentApplication,
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        TrainingAnnexItemActionInventory inventory,
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot shopStock,
        int luck,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        List<TrainingAnnexShopTransactionEvidence> transactionEvidence,
        List<TrainingAnnexEquipmentChangeEvidence> equipmentEvidence,
        IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> offerDiagnostics,
        CancellationToken cancellationToken)
    {
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> offerSelection =
            await _commandSource.ReadAsync(
                CreateShopBuyMenu(
                    shop,
                    offers,
                    inventory.Snapshot,
                    wallet,
                    shopStock,
                    shopTransactions,
                    luck),
                cancellationToken).ConfigureAwait(false);
        if (!offerSelection.IsSelected || offerSelection.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync("Shop purchase canceled; wallet and inventory are unchanged.", cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                shopStock,
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
                shopStock,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        RuntimeInstanceId? purchasedEquipmentInstanceId =
            offer.Runtime.ContentKind == ShopContentKind.Equipment
                ? NextEquipmentInstanceId(inventory.Snapshot)
                : null;
        ShopTransactionResult purchase = shopTransactions.Buy(
            inventory.Snapshot,
            wallet,
            shopStock,
            TrainingAnnexHostSupport.CreditsCurrency,
            offer.Runtime,
            luck,
            purchasedEquipmentInstanceId);
        transactionEvidence.Add(ToShopEvidence(
            shop.Id,
            offer.Runtime,
            purchase,
            isPurchase: true,
            purchasedEquipmentInstanceId));
        if (!purchase.Applied)
        {
            await PublishShopTransactionFailureAsync("purchase", offer, purchase, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                shopStock,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        inventory.Replace(purchase.AfterInventory);
        wallet = purchase.AfterCurrencyLedger;
        shopStock = purchase.AfterStock;
        await PublishShopTransactionSuccessAsync("Bought", offer, purchase, inventory.Snapshot, cancellationToken)
            .ConfigureAwait(false);

        if (offer.Runtime.ContentKind == ShopContentKind.Equipment &&
            offer.Runtime.EquipmentSlotId is ContentId slot)
        {
            await PromptEquipPurchasedEquipmentAsync(
                catalog,
                equipmentTransitions,
                equipmentApplication,
                roster,
                partyRoster,
                inventory,
                offer,
                purchasedEquipmentInstanceId!.Value,
                slot,
                commands,
                equipmentEvidence,
                cancellationToken).ConfigureAwait(false);
        }

        return new TrainingAnnexShopInteractionResult(
            wallet,
            shopStock,
            transactionEvidence,
            equipmentEvidence,
            offerDiagnostics);
    }

    private async ValueTask<TrainingAnnexShopInteractionResult> SellAsync(
        ShopCatalogDefinition shop,
        IReadOnlyList<TrainingAnnexResolvedShopOffer> offers,
        IShopTransactionService shopTransactions,
        TrainingAnnexRuntimeActor player,
        TrainingAnnexItemActionInventory inventory,
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot shopStock,
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
                    shopStock,
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
                shopStock,
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
                shopStock,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        RuntimeInstanceId? soldEquipmentInstanceId =
            FindSellableEquipmentInstance(
                inventory.Snapshot,
                offer.Runtime,
                player.Actor.State.ToSnapshot().Equipment);
        ShopTransactionResult sale = shopTransactions.Sell(
            inventory.Snapshot,
            wallet,
            shopStock,
            TrainingAnnexHostSupport.CreditsCurrency,
            offer.Runtime,
            luck,
            soldEquipmentInstanceId,
            [player.Actor.State.ToSnapshot().Equipment]);
        transactionEvidence.Add(ToShopEvidence(
            shop.Id,
            offer.Runtime,
            sale,
            isPurchase: false,
            soldEquipmentInstanceId));
        if (!sale.Applied)
        {
            await PublishShopTransactionFailureAsync("sale", offer, sale, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexShopInteractionResult(
                wallet,
                shopStock,
                transactionEvidence,
                equipmentEvidence,
                offerDiagnostics);
        }

        inventory.Replace(sale.AfterInventory);
        wallet = sale.AfterCurrencyLedger;
        shopStock = sale.AfterStock;
        await PublishShopTransactionSuccessAsync("Sold", offer, sale, inventory.Snapshot, cancellationToken)
            .ConfigureAwait(false);

        return new TrainingAnnexShopInteractionResult(
            wallet,
            shopStock,
            transactionEvidence,
            equipmentEvidence,
            offerDiagnostics);
    }

    private async ValueTask PromptEquipPurchasedEquipmentAsync(
        GameDataCatalog catalog,
        IEquipmentTransitionService equipmentTransitions,
        IRuntimeActorEquipmentApplicationService equipmentApplication,
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        TrainingAnnexItemActionInventory inventory,
        TrainingAnnexResolvedShopOffer offer,
        RuntimeInstanceId equipmentInstanceId,
        ContentId slot,
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
        TrainingAnnexRuntimeActor player = roster.Player;
        EquipmentTransitionResult equipResult = equipmentTransitions.Equip(
            inventory.Snapshot,
            player.Actor.State.ToSnapshot().Equipment,
            equipmentInstanceId,
            slot,
            slot,
            roster.AllActors
                .Where(actor => !ReferenceEquals(actor, player))
                .Select(actor => actor.Actor.State.ToSnapshot().Equipment));
        if (equipResult.Applied)
        {
            RuntimeActorEquipmentApplicationResult application = equipmentApplication.Apply(
                new RuntimeActorEquipmentApplicationRequest(
                    player.Actor.State,
                    inventory.Snapshot,
                    equipResult.After,
                    catalog,
                    RuntimeStatSourceKind.ActiveHostedEntity,
                    MissingHostedEntityBehavior.RejectStatResolution,
                    partyRoster,
                    roster.AllActors.Select(actor => actor.Actor.State)));
            equipmentEvidence.Add(new TrainingAnnexEquipmentChangeEvidence(
                equipmentInstanceId,
                offer.Runtime.ContentId,
                slot,
                equipResult.Code,
                application.Applied));
            if (!application.Applied)
            {
                string applicationDiagnostics = string.Join(
                    "; ",
                    application.Diagnostics.Select(diagnostic => diagnostic.Message));
                await _eventSink.PublishAsync(
                    $"Equipment equip rejected: {offer.DisplayName}; {applicationDiagnostics}",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            RuntimeEquipmentProfile profile = application.EquipmentProfile;
            string slots = string.Join(
                ", ",
                profile.EquippedDefinitions
                    .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                    .Select(pair => $"{FormatSlot(pair.Key)}: {pair.Value.DisplayName}"));
            await _eventSink.PublishAsync(
                $"Equipped {offer.DisplayName} in {FormatSlot(slot)}; equipment profile now [{slots}].",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        equipmentEvidence.Add(new TrainingAnnexEquipmentChangeEvidence(
            equipmentInstanceId,
            offer.Runtime.ContentId,
            slot,
            equipResult.Code,
            Applied: false));
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
        ShopCatalogDefinition shop,
        IRuntimeShopOfferResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var offers = new List<TrainingAnnexResolvedShopOffer>();
        var diagnostics = new List<RuntimeShopOfferResolutionDiagnostic>();
        foreach (ShopOfferDefinition offer in shop.Offers)
        {
            RuntimeShopOfferResolutionResult resolved = resolver.Resolve(
                shop.Id,
                offer,
                catalog,
                catalog);
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
            ? offers.FirstOrDefault(offer => offer.Runtime.Identity.OfferId == selected)
            : null;

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateShopSessionMenu(
        ShopCatalogDefinition shop,
        RuntimeCurrencyLedgerSnapshot wallet) =>
        new(
            $"{shop.DisplayName} - Wallet {TrainingAnnexHostSupport.GetCreditsBalance(wallet)} C",
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
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot shopStock,
        IShopTransactionService shopTransactions,
        int luck)
    {
        List<HostCommandOption<CleanTrainingAnnexPlayCommand>> options = offers
            .Select(offer =>
            {
                RuntimeInstanceId? equipmentInstanceId =
                    offer.Runtime.ContentKind == ShopContentKind.Equipment
                        ? NextEquipmentInstanceId(inventory)
                        : null;
                ShopTransactionResult assessment = shopTransactions.Buy(
                    inventory,
                    wallet,
                    shopStock,
                    TrainingAnnexHostSupport.CreditsCurrency,
                    offer.Runtime,
                    luck,
                    equipmentInstanceId);
                return new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SelectShopOffer,
                    $"{offer.DisplayName} - {assessment.Price} C{StockLabel(offer.Runtime, shopStock)}{TransactionLabel(assessment)}",
                    assessment.Applied,
                    offer.Description,
                    HostCommandSelectionIdentity.ForContent(offer.Runtime.Identity.OfferId));
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
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot shopStock,
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
                    shopStock,
                    TrainingAnnexHostSupport.CreditsCurrency,
                    offer.Runtime,
                    luck,
                    FindSellableEquipmentInstance(inventory, offer.Runtime, equipment),
                    [equipment]);
                return new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SelectSellOffer,
                    $"{offer.DisplayName} - {assessment.Price} C{OwnedLabel(inventory, offer.Runtime)}{TransactionLabel(assessment)}",
                    assessment.Applied,
                    offer.Description,
                    HostCommandSelectionIdentity.ForContent(offer.Runtime.Identity.OfferId));
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
        ContentId slot) =>
        new(
            $"Equip {offer.DisplayName}?",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.EquipPurchasedEquipment,
                    $"Equip to {FormatSlot(slot)}"),
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
            ShopContentKind.Equipment when offer.EquipmentSlotId is ContentId slot =>
                inventory.GetEquipmentInstances(slot).Any(instance =>
                    instance.DefinitionId == offer.ContentId),
            _ => false
        };

    private static string StockLabel(
        RuntimeShopOfferSnapshot offer,
        RuntimeShopStockSnapshot stock) =>
        offer.Stock.IsTracked && stock.TryGetRemainingQuantity(offer.Identity, out int remaining)
            ? $" (stock {remaining})"
            : string.Empty;

    private static string OwnedLabel(RuntimeInventorySnapshot inventory, RuntimeShopOfferSnapshot offer) =>
        offer.ContentKind switch
        {
            ShopContentKind.Item => $" (owned {inventory.GetQuantity(offer.ContentId)})",
            ShopContentKind.Equipment when offer.EquipmentSlotId is ContentId slot &&
                                           inventory.GetEquipmentInstances(slot).Any(instance =>
                                               instance.DefinitionId == offer.ContentId) =>
                $" (owned {inventory.GetEquipmentInstances(slot).Count(instance => instance.DefinitionId == offer.ContentId)})",
            _ => string.Empty
        };

    private static string TransactionLabel(ShopTransactionResult result) =>
        result.Applied
            ? string.Empty
            : $" [{TransactionReason(result.Code)}]";

    private static string TransactionReason(ResourceTransactionCode code) =>
        code switch
        {
            ResourceTransactionCode.InsufficientCurrency => "Not enough Credits",
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
        bool isPurchase,
        RuntimeInstanceId? equipmentInstanceId) =>
        new(
            shopId,
            offer.Identity.OfferId,
            offer.ContentKind,
            isPurchase,
            result.Code,
            result.Price,
            TrainingAnnexHostSupport.GetCreditsBalance(result.BeforeCurrencyLedger),
            TrainingAnnexHostSupport.GetCreditsBalance(result.AfterCurrencyLedger),
            OwnedCount(result.BeforeInventory, offer),
            OwnedCount(result.AfterInventory, offer),
            offer.EquipmentSlotId,
            equipmentInstanceId);

    private static int OwnedCount(RuntimeInventorySnapshot inventory, RuntimeShopOfferSnapshot offer) =>
        offer.ContentKind switch
        {
            ShopContentKind.Item => inventory.GetQuantity(offer.ContentId),
            ShopContentKind.Equipment when offer.EquipmentSlotId is ContentId slot =>
                inventory.GetEquipmentInstances(slot).Count(instance =>
                    instance.DefinitionId == offer.ContentId),
            _ => 0
        };

    private static string FormatSlot(ContentId slotId) =>
        slotId == StandardEquipmentSlotIds.Weapon ? "Weapon" :
        slotId == StandardEquipmentSlotIds.Armor ? "Armor" :
        slotId == StandardEquipmentSlotIds.Boots ? "Boots" :
        slotId == StandardEquipmentSlotIds.Accessory ? "Accessory" :
        slotId.ToString();

    private static RuntimeInstanceId NextEquipmentInstanceId(
        RuntimeInventorySnapshot inventory)
    {
        for (int sequence = 1; sequence < int.MaxValue; sequence++)
        {
            RuntimeInstanceId candidate =
                RuntimeInstanceId.Parse($"training-annex-shop-equipment-{sequence}");
            if (!inventory.TryGetEquipmentInstance(candidate, out _, out _))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "No equipment runtime instance ID remained available for the shop purchase.");
    }

    private static RuntimeInstanceId? FindSellableEquipmentInstance(
        RuntimeInventorySnapshot inventory,
        RuntimeShopOfferSnapshot offer,
        RuntimeEquipmentSnapshot equipment)
    {
        if (offer.ContentKind != ShopContentKind.Equipment ||
            offer.EquipmentSlotId is not ContentId slot)
        {
            return null;
        }

        RuntimeEquipmentInstanceSnapshot[] matches = inventory
            .GetEquipmentInstances(slot)
            .Where(instance => instance.DefinitionId == offer.ContentId)
            .ToArray();
        RuntimeEquipmentInstanceSnapshot? selected = matches.FirstOrDefault(instance =>
            !equipment.EquippedInstanceIds.Values.Contains(instance.InstanceId)) ??
            matches.FirstOrDefault();
        return selected?.InstanceId;
    }

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
            $"Shop transaction: {action} {offer.DisplayName} for {result.Price} C; wallet " +
            $"{TrainingAnnexHostSupport.GetCreditsBalance(result.BeforeCurrencyLedger)}->" +
            $"{TrainingAnnexHostSupport.GetCreditsBalance(result.AfterCurrencyLedger)}; {owned}.",
            cancellationToken).ConfigureAwait(false);
    }
}
