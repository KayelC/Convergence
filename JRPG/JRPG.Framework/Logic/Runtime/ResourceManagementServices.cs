using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Runtime;

public enum ResourceTransactionCode
{
    Applied,
    InvalidQuantity,
    InvalidCurrencyAmount,
    ItemMissing,
    ItemStackExceeded,
    EquipmentMissing,
    EquipmentDuplicate,
    EquipmentNotOwned,
    EquipmentAlreadyEquipped,
    EquipmentSlotMismatch,
    EquippedItemCannotBeRemoved,
    InsufficientCurrency,
    ShopStockUnavailable,
    NoRestorationNeeded
}

public sealed record ResourceTransactionDiagnostic(
    ResourceTransactionCode Code,
    string Message,
    ContentId? ContentId = null,
    EquipmentSlot? Slot = null);

public sealed record RuntimeInventorySnapshot
{
    public RuntimeInventorySnapshot(
        IEnumerable<KeyValuePair<ContentId, int>>? itemQuantities = null,
        IEnumerable<KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>>? ownedEquipmentIds = null)
    {
        Dictionary<ContentId, int> items = [];
        foreach ((ContentId itemId, int quantity) in itemQuantities ?? [])
        {
            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(itemQuantities), "Item quantities cannot be negative.");
            }

            if (quantity > 0)
            {
                items.Add(itemId, quantity);
            }
        }

        Dictionary<EquipmentSlot, IReadOnlyList<ContentId>> equipment = [];
        foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            equipment[slot] = Array.AsReadOnly(Array.Empty<ContentId>());
        }

        foreach ((EquipmentSlot slot, IEnumerable<ContentId> ids) in ownedEquipmentIds ?? [])
        {
            ContentId[] copy = ids?.Distinct().ToArray() ?? [];
            equipment[slot] = Array.AsReadOnly(copy);
        }

        ItemQuantities = RuntimeSnapshotCollections.Dictionary(items);
        OwnedEquipmentIds = RuntimeSnapshotCollections.Dictionary(equipment);
    }

    public IReadOnlyDictionary<ContentId, int> ItemQuantities { get; }
    public IReadOnlyDictionary<EquipmentSlot, IReadOnlyList<ContentId>> OwnedEquipmentIds { get; }

    public int GetQuantity(ContentId itemId) =>
        ItemQuantities.TryGetValue(itemId, out int quantity) ? quantity : 0;

    public IReadOnlyList<ContentId> GetEquipmentIds(EquipmentSlot slot) =>
        OwnedEquipmentIds.TryGetValue(slot, out IReadOnlyList<ContentId>? ids)
            ? ids
            : Array.AsReadOnly(Array.Empty<ContentId>());

    public bool OwnsEquipment(ContentId equipmentId, EquipmentSlot slot) =>
        GetEquipmentIds(slot).Contains(equipmentId);

    internal RuntimeInventorySnapshot WithItems(IEnumerable<KeyValuePair<ContentId, int>> itemQuantities) =>
        new(itemQuantities, OwnedEquipmentIds.Select(pair => new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(pair.Key, pair.Value)));

    internal RuntimeInventorySnapshot WithEquipment(EquipmentSlot slot, IEnumerable<ContentId> ids)
    {
        Dictionary<EquipmentSlot, IEnumerable<ContentId>> equipment = OwnedEquipmentIds
            .ToDictionary(pair => pair.Key, pair => (IEnumerable<ContentId>)pair.Value);
        equipment[slot] = ids;
        return new RuntimeInventorySnapshot(ItemQuantities, equipment);
    }
}

public sealed record RuntimeWalletSnapshot
{
    public RuntimeWalletSnapshot(int macca)
    {
        if (macca < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(macca), "Macca cannot be negative.");
        }

        Macca = macca;
    }

    public int Macca { get; }
}

public sealed record InventoryTransitionResult
{
    public InventoryTransitionResult(
        ResourceTransactionCode code,
        RuntimeInventorySnapshot before,
        RuntimeInventorySnapshot after,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Applied => Code == ResourceTransactionCode.Applied;
    public RuntimeInventorySnapshot Before { get; }
    public RuntimeInventorySnapshot After { get; }
    public IReadOnlyList<ResourceTransactionDiagnostic> Diagnostics { get; }
}

public sealed record WalletTransactionResult
{
    public WalletTransactionResult(
        ResourceTransactionCode code,
        RuntimeWalletSnapshot before,
        RuntimeWalletSnapshot after,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Applied => Code == ResourceTransactionCode.Applied;
    public RuntimeWalletSnapshot Before { get; }
    public RuntimeWalletSnapshot After { get; }
    public IReadOnlyList<ResourceTransactionDiagnostic> Diagnostics { get; }
}

public interface IInventoryTransitionService
{
    InventoryTransitionResult AddItem(RuntimeInventorySnapshot snapshot, ContentId itemId, int quantity, int? stackLimit = null);
    InventoryTransitionResult RemoveItem(RuntimeInventorySnapshot snapshot, ContentId itemId, int quantity);
    InventoryReservationResult ReserveItem(RuntimeInventorySnapshot snapshot, ContentId itemId, int quantity);
    InventoryTransitionResult AddEquipment(RuntimeInventorySnapshot snapshot, ContentId equipmentId, EquipmentSlot slot);
    InventoryTransitionResult RemoveEquipment(RuntimeInventorySnapshot snapshot, ContentId equipmentId, EquipmentSlot slot, RuntimeEquipmentSnapshot? equipped = null);
}

public sealed record InventoryReservationResult
{
    public InventoryReservationResult(
        ResourceTransactionCode code,
        RuntimeInventorySnapshot snapshot,
        RuntimeItemReservation? reservation = null,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Reservation = reservation;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Reserved => Code == ResourceTransactionCode.Applied && Reservation is not null;
    public RuntimeInventorySnapshot Snapshot { get; }
    public RuntimeItemReservation? Reservation { get; }
    public IReadOnlyList<ResourceTransactionDiagnostic> Diagnostics { get; }
}

public sealed class RuntimeItemReservation
{
    private readonly IInventoryTransitionService _inventory;

    internal RuntimeItemReservation(
        IInventoryTransitionService inventory,
        RuntimeInventorySnapshot snapshot,
        ContentId itemId,
        int quantity)
    {
        _inventory = inventory;
        Snapshot = snapshot;
        ItemId = itemId;
        Quantity = quantity;
    }

    public RuntimeInventorySnapshot Snapshot { get; }
    public ContentId ItemId { get; }
    public int Quantity { get; }
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public InventoryTransitionResult Commit()
    {
        if (IsRolledBack)
        {
            throw new InvalidOperationException("Cannot commit a rolled-back reservation.");
        }
        if (IsCommitted)
        {
            throw new InvalidOperationException("Cannot commit a reservation twice.");
        }

        InventoryTransitionResult result = _inventory.RemoveItem(Snapshot, ItemId, Quantity);
        IsCommitted = result.Applied;
        return result;
    }

    public InventoryTransitionResult Rollback()
    {
        if (IsCommitted)
        {
            throw new InvalidOperationException("Cannot roll back a committed reservation.");
        }

        IsRolledBack = true;
        return new InventoryTransitionResult(ResourceTransactionCode.Applied, Snapshot, Snapshot);
    }
}

public sealed class InventoryTransitionService : IInventoryTransitionService
{
    public InventoryTransitionResult AddItem(RuntimeInventorySnapshot snapshot, ContentId itemId, int quantity, int? stackLimit = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (quantity <= 0)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidQuantity, "Item quantity must be positive.", itemId);
        }
        if (stackLimit is <= 0)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidQuantity, "Stack limit must be positive when supplied.", itemId);
        }

        int current = snapshot.GetQuantity(itemId);
        int next = checked(current + quantity);
        if (stackLimit is int limit && next > limit)
        {
            return Rejected(snapshot, ResourceTransactionCode.ItemStackExceeded, $"Item '{itemId}' would exceed stack limit {limit}.", itemId);
        }

        Dictionary<ContentId, int> items = new(snapshot.ItemQuantities);
        items[itemId] = next;
        return Applied(snapshot, snapshot.WithItems(items));
    }

    public InventoryTransitionResult RemoveItem(RuntimeInventorySnapshot snapshot, ContentId itemId, int quantity)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (quantity <= 0)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidQuantity, "Item quantity must be positive.", itemId);
        }

        int current = snapshot.GetQuantity(itemId);
        if (current < quantity)
        {
            return Rejected(snapshot, ResourceTransactionCode.ItemMissing, $"Item '{itemId}' is not available in the requested quantity.", itemId);
        }

        Dictionary<ContentId, int> items = new(snapshot.ItemQuantities);
        int next = current - quantity;
        if (next <= 0)
        {
            items.Remove(itemId);
        }
        else
        {
            items[itemId] = next;
        }

        return Applied(snapshot, snapshot.WithItems(items));
    }

    public InventoryReservationResult ReserveItem(RuntimeInventorySnapshot snapshot, ContentId itemId, int quantity)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (quantity <= 0)
        {
            return ReservationRejected(snapshot, ResourceTransactionCode.InvalidQuantity, "Item quantity must be positive.", itemId);
        }

        int current = snapshot.GetQuantity(itemId);
        if (current < quantity)
        {
            return ReservationRejected(snapshot, ResourceTransactionCode.ItemMissing, $"Item '{itemId}' is not available in the requested quantity.", itemId);
        }

        return new InventoryReservationResult(
            ResourceTransactionCode.Applied,
            snapshot,
            new RuntimeItemReservation(this, snapshot, itemId, quantity));
    }

    public InventoryTransitionResult AddEquipment(RuntimeInventorySnapshot snapshot, ContentId equipmentId, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        IReadOnlyList<ContentId> current = snapshot.GetEquipmentIds(slot);
        if (current.Contains(equipmentId))
        {
            return Rejected(snapshot, ResourceTransactionCode.EquipmentDuplicate, $"Equipment '{equipmentId}' is already owned.", equipmentId, slot);
        }

        return Applied(snapshot, snapshot.WithEquipment(slot, current.Append(equipmentId)));
    }

    public InventoryTransitionResult RemoveEquipment(
        RuntimeInventorySnapshot snapshot,
        ContentId equipmentId,
        EquipmentSlot slot,
        RuntimeEquipmentSnapshot? equipped = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        IReadOnlyList<ContentId> current = snapshot.GetEquipmentIds(slot);
        if (!current.Contains(equipmentId))
        {
            return Rejected(snapshot, ResourceTransactionCode.EquipmentNotOwned, $"Equipment '{equipmentId}' is not owned.", equipmentId, slot);
        }
        if (equipped?.EquippedItemIds.TryGetValue(slot, out ContentId equippedId) == true && equippedId == equipmentId)
        {
            return Rejected(snapshot, ResourceTransactionCode.EquippedItemCannotBeRemoved, $"Equipment '{equipmentId}' is currently equipped.", equipmentId, slot);
        }

        return Applied(snapshot, snapshot.WithEquipment(slot, current.Where(id => id != equipmentId)));
    }

    private static InventoryTransitionResult Applied(RuntimeInventorySnapshot before, RuntimeInventorySnapshot after) =>
        new(ResourceTransactionCode.Applied, before, after);

    private static InventoryTransitionResult Rejected(
        RuntimeInventorySnapshot before,
        ResourceTransactionCode code,
        string message,
        ContentId? contentId = null,
        EquipmentSlot? slot = null) =>
        new(code, before, before, [new ResourceTransactionDiagnostic(code, message, contentId, slot)]);

    private static InventoryReservationResult ReservationRejected(
        RuntimeInventorySnapshot before,
        ResourceTransactionCode code,
        string message,
        ContentId? contentId = null) =>
        new(code, before, diagnostics: [new ResourceTransactionDiagnostic(code, message, contentId)]);
}

public interface IEconomyTransactionService
{
    WalletTransactionResult AddMacca(RuntimeWalletSnapshot snapshot, int amount);
    WalletTransactionResult SpendMacca(RuntimeWalletSnapshot snapshot, int amount);
}

public sealed class EconomyTransactionService : IEconomyTransactionService
{
    public WalletTransactionResult AddMacca(RuntimeWalletSnapshot snapshot, int amount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (amount < 0)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidCurrencyAmount, "Macca amount cannot be negative.");
        }

        return new WalletTransactionResult(
            ResourceTransactionCode.Applied,
            snapshot,
            new RuntimeWalletSnapshot(checked(snapshot.Macca + amount)));
    }

    public WalletTransactionResult SpendMacca(RuntimeWalletSnapshot snapshot, int amount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (amount < 0)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidCurrencyAmount, "Macca amount cannot be negative.");
        }
        if (snapshot.Macca < amount)
        {
            return Rejected(snapshot, ResourceTransactionCode.InsufficientCurrency, "Not enough Macca.");
        }

        return new WalletTransactionResult(
            ResourceTransactionCode.Applied,
            snapshot,
            new RuntimeWalletSnapshot(snapshot.Macca - amount));
    }

    private static WalletTransactionResult Rejected(
        RuntimeWalletSnapshot before,
        ResourceTransactionCode code,
        string message) =>
        new(code, before, before, [new ResourceTransactionDiagnostic(code, message)]);
}

public sealed record EquipmentTransitionResult
{
    public EquipmentTransitionResult(
        ResourceTransactionCode code,
        RuntimeEquipmentSnapshot before,
        RuntimeEquipmentSnapshot after,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Applied => Code == ResourceTransactionCode.Applied;
    public RuntimeEquipmentSnapshot Before { get; }
    public RuntimeEquipmentSnapshot After { get; }
    public IReadOnlyList<ResourceTransactionDiagnostic> Diagnostics { get; }
}

public interface IEquipmentTransitionService
{
    EquipmentTransitionResult Equip(
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        ContentId equipmentId,
        EquipmentSlot ownedSlot,
        EquipmentSlot targetSlot);

    EquipmentTransitionResult Unequip(RuntimeEquipmentSnapshot equipment, EquipmentSlot slot);
}

public sealed class EquipmentTransitionService : IEquipmentTransitionService
{
    public EquipmentTransitionResult Equip(
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        ContentId equipmentId,
        EquipmentSlot ownedSlot,
        EquipmentSlot targetSlot)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(equipment);
        if (ownedSlot != targetSlot)
        {
            return Rejected(equipment, ResourceTransactionCode.EquipmentSlotMismatch, $"Equipment '{equipmentId}' cannot be equipped in slot '{targetSlot}'.", equipmentId, targetSlot);
        }
        if (!inventory.OwnsEquipment(equipmentId, ownedSlot))
        {
            return Rejected(equipment, ResourceTransactionCode.EquipmentNotOwned, $"Equipment '{equipmentId}' is not owned.", equipmentId, ownedSlot);
        }

        Dictionary<EquipmentSlot, ContentId> equipped = new(equipment.EquippedItemIds);
        equipped[targetSlot] = equipmentId;
        return new EquipmentTransitionResult(
            ResourceTransactionCode.Applied,
            equipment,
            new RuntimeEquipmentSnapshot(equipped));
    }

    public EquipmentTransitionResult Unequip(RuntimeEquipmentSnapshot equipment, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        Dictionary<EquipmentSlot, ContentId> equipped = new(equipment.EquippedItemIds);
        equipped.Remove(slot);
        return new EquipmentTransitionResult(
            ResourceTransactionCode.Applied,
            equipment,
            new RuntimeEquipmentSnapshot(equipped));
    }

    private static EquipmentTransitionResult Rejected(
        RuntimeEquipmentSnapshot before,
        ResourceTransactionCode code,
        string message,
        ContentId? contentId = null,
        EquipmentSlot? slot = null) =>
        new(code, before, before, [new ResourceTransactionDiagnostic(code, message, contentId, slot)]);
}

public sealed record RuntimeShopOfferSnapshot(
    ShopContentKind ContentKind,
    ContentId ContentId,
    int BasePrice,
    EquipmentSlot? EquipmentSlot = null,
    int? ItemStackLimit = null,
    int? StockAvailable = null);

public sealed record ShopTransactionResult
{
    public ShopTransactionResult(
        ResourceTransactionCode code,
        RuntimeInventorySnapshot beforeInventory,
        RuntimeInventorySnapshot afterInventory,
        RuntimeWalletSnapshot beforeWallet,
        RuntimeWalletSnapshot afterWallet,
        int price,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        BeforeInventory = beforeInventory ?? throw new ArgumentNullException(nameof(beforeInventory));
        AfterInventory = afterInventory ?? throw new ArgumentNullException(nameof(afterInventory));
        BeforeWallet = beforeWallet ?? throw new ArgumentNullException(nameof(beforeWallet));
        AfterWallet = afterWallet ?? throw new ArgumentNullException(nameof(afterWallet));
        Price = price;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Applied => Code == ResourceTransactionCode.Applied;
    public RuntimeInventorySnapshot BeforeInventory { get; }
    public RuntimeInventorySnapshot AfterInventory { get; }
    public RuntimeWalletSnapshot BeforeWallet { get; }
    public RuntimeWalletSnapshot AfterWallet { get; }
    public int Price { get; }
    public IReadOnlyList<ResourceTransactionDiagnostic> Diagnostics { get; }
}

public interface IShopTransactionService
{
    int CalculateBuyPrice(int basePrice, int luck);
    int CalculateSellPrice(int basePrice, int luck);
    ShopTransactionResult Buy(RuntimeInventorySnapshot inventory, RuntimeWalletSnapshot wallet, RuntimeShopOfferSnapshot offer, int buyerLuck);
    ShopTransactionResult Sell(RuntimeInventorySnapshot inventory, RuntimeWalletSnapshot wallet, RuntimeShopOfferSnapshot offer, int sellerLuck, RuntimeEquipmentSnapshot? equipped = null);
}

public sealed class ShopTransactionService : IShopTransactionService
{
    private readonly IInventoryTransitionService _inventory;
    private readonly IEconomyTransactionService _economy;

    public ShopTransactionService(
        IInventoryTransitionService? inventory = null,
        IEconomyTransactionService? economy = null)
    {
        _inventory = inventory ?? new InventoryTransitionService();
        _economy = economy ?? new EconomyTransactionService();
    }

    public int CalculateBuyPrice(int basePrice, int luck)
    {
        double discountMult = Math.Max(0.5, 1.0 - (luck * 0.01));
        return (int)(basePrice * discountMult);
    }

    public int CalculateSellPrice(int basePrice, int luck)
    {
        double sellMult = 0.50 + (luck * 0.01);
        return (int)(basePrice * sellMult);
    }

    public ShopTransactionResult Buy(
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeShopOfferSnapshot offer,
        int buyerLuck)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(wallet);
        if (offer.StockAvailable is <= 0)
        {
            return Rejected(ResourceTransactionCode.ShopStockUnavailable, inventory, wallet, CalculateBuyPrice(offer.BasePrice, buyerLuck), "Shop stock is unavailable.", offer.ContentId, offer.EquipmentSlot);
        }

        int price = CalculateBuyPrice(offer.BasePrice, buyerLuck);
        InventoryTransitionResult inventoryResult = AddPurchasedContent(inventory, offer);
        if (!inventoryResult.Applied)
        {
            return FromInventory(inventoryResult, wallet, price);
        }

        WalletTransactionResult walletResult = _economy.SpendMacca(wallet, price);
        if (!walletResult.Applied)
        {
            return FromWallet(walletResult, inventory, price);
        }

        return new ShopTransactionResult(
            ResourceTransactionCode.Applied,
            inventory,
            inventoryResult.After,
            wallet,
            walletResult.After,
            price);
    }

    public ShopTransactionResult Sell(
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeShopOfferSnapshot offer,
        int sellerLuck,
        RuntimeEquipmentSnapshot? equipped = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(wallet);
        int price = CalculateSellPrice(offer.BasePrice, sellerLuck);
        InventoryTransitionResult inventoryResult = offer.ContentKind switch
        {
            ShopContentKind.Item => _inventory.RemoveItem(inventory, offer.ContentId, 1),
            ShopContentKind.Equipment when offer.EquipmentSlot is EquipmentSlot slot =>
                _inventory.RemoveEquipment(inventory, offer.ContentId, slot, equipped),
            _ => InventoryRejected(inventory, ResourceTransactionCode.EquipmentSlotMismatch, "Equipment offers require a slot.", offer.ContentId, offer.EquipmentSlot)
        };

        if (!inventoryResult.Applied)
        {
            return FromInventory(inventoryResult, wallet, price);
        }

        WalletTransactionResult walletResult = _economy.AddMacca(wallet, price);
        if (!walletResult.Applied)
        {
            return FromWallet(walletResult, inventory, price);
        }

        return new ShopTransactionResult(
            ResourceTransactionCode.Applied,
            inventory,
            inventoryResult.After,
            wallet,
            walletResult.After,
            price);
    }

    private InventoryTransitionResult AddPurchasedContent(
        RuntimeInventorySnapshot inventory,
        RuntimeShopOfferSnapshot offer) =>
        offer.ContentKind switch
        {
            ShopContentKind.Item => _inventory.AddItem(inventory, offer.ContentId, 1, offer.ItemStackLimit),
            ShopContentKind.Equipment when offer.EquipmentSlot is EquipmentSlot slot =>
                _inventory.AddEquipment(inventory, offer.ContentId, slot),
            _ => InventoryRejected(inventory, ResourceTransactionCode.EquipmentSlotMismatch, "Equipment offers require a slot.", offer.ContentId, offer.EquipmentSlot)
        };

    private static ShopTransactionResult FromInventory(
        InventoryTransitionResult result,
        RuntimeWalletSnapshot wallet,
        int price) =>
        new(result.Code, result.Before, result.After, wallet, wallet, price, result.Diagnostics);

    private static ShopTransactionResult FromWallet(
        WalletTransactionResult result,
        RuntimeInventorySnapshot inventory,
        int price) =>
        new(result.Code, inventory, inventory, result.Before, result.After, price, result.Diagnostics);

    private static InventoryTransitionResult InventoryRejected(
        RuntimeInventorySnapshot before,
        ResourceTransactionCode code,
        string message,
        ContentId? contentId,
        EquipmentSlot? slot) =>
        new(code, before, before, [new ResourceTransactionDiagnostic(code, message, contentId, slot)]);

    private static ShopTransactionResult Rejected(
        ResourceTransactionCode code,
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        int price,
        string message,
        ContentId? contentId = null,
        EquipmentSlot? slot = null) =>
        new(code, inventory, inventory, wallet, wallet, price, [new ResourceTransactionDiagnostic(code, message, contentId, slot)]);
}

public sealed record RuntimeHospitalPatientSnapshot
{
    public RuntimeHospitalPatientSnapshot(
        RuntimeInstanceId patientId,
        int currentHp,
        int maxHp,
        int currentSp,
        int maxSp,
        bool hasAilment,
        bool hasEncounterPersistence = false)
    {
        if (maxHp < 0 || maxSp < 0 || currentHp < 0 || currentSp < 0 || currentHp > maxHp || currentSp > maxSp)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHp), "Patient resources must satisfy 0 <= current <= max.");
        }

        PatientId = patientId;
        CurrentHp = currentHp;
        MaxHp = maxHp;
        CurrentSp = currentSp;
        MaxSp = maxSp;
        HasAilment = hasAilment;
        HasEncounterPersistence = hasEncounterPersistence;
    }

    public RuntimeInstanceId PatientId { get; }
    public int CurrentHp { get; }
    public int MaxHp { get; }
    public int CurrentSp { get; }
    public int MaxSp { get; }
    public bool HasAilment { get; }
    public bool HasEncounterPersistence { get; }
    public int MissingHp => MaxHp - CurrentHp;
    public int MissingSp => MaxSp - CurrentSp;
}

public sealed record HospitalRestorationResult
{
    public HospitalRestorationResult(
        ResourceTransactionCode code,
        RuntimeHospitalPatientSnapshot beforePatient,
        RuntimeHospitalPatientSnapshot afterPatient,
        RuntimeWalletSnapshot beforeWallet,
        RuntimeWalletSnapshot afterWallet,
        int cost,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        BeforePatient = beforePatient ?? throw new ArgumentNullException(nameof(beforePatient));
        AfterPatient = afterPatient ?? throw new ArgumentNullException(nameof(afterPatient));
        BeforeWallet = beforeWallet ?? throw new ArgumentNullException(nameof(beforeWallet));
        AfterWallet = afterWallet ?? throw new ArgumentNullException(nameof(afterWallet));
        Cost = cost;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Applied => Code == ResourceTransactionCode.Applied;
    public RuntimeHospitalPatientSnapshot BeforePatient { get; }
    public RuntimeHospitalPatientSnapshot AfterPatient { get; }
    public RuntimeWalletSnapshot BeforeWallet { get; }
    public RuntimeWalletSnapshot AfterWallet { get; }
    public int Cost { get; }
    public IReadOnlyList<ResourceTransactionDiagnostic> Diagnostics { get; }
}

public interface IHospitalRestorationService
{
    int CalculateRestorationCost(RuntimeHospitalPatientSnapshot patient);
    HospitalRestorationResult Restore(RuntimeHospitalPatientSnapshot patient, RuntimeWalletSnapshot wallet);
}

public sealed class HospitalRestorationService : IHospitalRestorationService
{
    private readonly IEconomyTransactionService _economy;

    public HospitalRestorationService(IEconomyTransactionService? economy = null)
    {
        _economy = economy ?? new EconomyTransactionService();
    }

    public int CalculateRestorationCost(RuntimeHospitalPatientSnapshot patient)
    {
        ArgumentNullException.ThrowIfNull(patient);
        return patient.MissingHp + (patient.MissingSp * 5);
    }

    public HospitalRestorationResult Restore(RuntimeHospitalPatientSnapshot patient, RuntimeWalletSnapshot wallet)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(wallet);
        int cost = CalculateRestorationCost(patient);
        if (cost <= 0 && !patient.HasAilment && !patient.HasEncounterPersistence)
        {
            return Rejected(ResourceTransactionCode.NoRestorationNeeded, patient, wallet, cost, "The patient does not need restoration.");
        }

        WalletTransactionResult spend = _economy.SpendMacca(wallet, cost);
        if (!spend.Applied)
        {
            return new HospitalRestorationResult(
                spend.Code,
                patient,
                patient,
                spend.Before,
                spend.After,
                cost,
                spend.Diagnostics);
        }

        var after = new RuntimeHospitalPatientSnapshot(
            patient.PatientId,
            patient.MaxHp,
            patient.MaxHp,
            patient.MaxSp,
            patient.MaxSp,
            hasAilment: false,
            hasEncounterPersistence: false);
        return new HospitalRestorationResult(
            ResourceTransactionCode.Applied,
            patient,
            after,
            wallet,
            spend.After,
            cost);
    }

    private static HospitalRestorationResult Rejected(
        ResourceTransactionCode code,
        RuntimeHospitalPatientSnapshot patient,
        RuntimeWalletSnapshot wallet,
        int cost,
        string message) =>
        new(code, patient, patient, wallet, wallet, cost, [new ResourceTransactionDiagnostic(code, message)]);
}
