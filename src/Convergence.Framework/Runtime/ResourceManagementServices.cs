using Convergence.Content;
using Convergence.Catalog;
using Convergence.Internal;

namespace Convergence.Runtime;

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
    NoRestorationNeeded,
    NumericOverflow,
    InvalidShopPricing,
    InvalidEquipmentInstanceId
}

public sealed record ResourceTransactionDiagnostic(
    ResourceTransactionCode Code,
    string Message,
    ContentId? ContentId = null,
    ContentId? SlotId = null,
    RuntimeInstanceId? EquipmentInstanceId = null);

public sealed record RuntimeEquipmentInstanceSnapshot
{
    public RuntimeEquipmentInstanceSnapshot(
        RuntimeInstanceId instanceId,
        ContentId definitionId)
    {
        if (!instanceId.IsValid)
        {
            throw new ArgumentException(
                "Equipment instance ID must be valid.",
                nameof(instanceId));
        }
        if (!definitionId.IsValid)
        {
            throw new ArgumentException(
                "Equipment definition ID must be valid.",
                nameof(definitionId));
        }

        InstanceId = instanceId;
        DefinitionId = definitionId;
    }

    public RuntimeInstanceId InstanceId { get; }
    public ContentId DefinitionId { get; }
}

public sealed record RuntimeInventorySnapshot
{
    public RuntimeInventorySnapshot(
        IEnumerable<KeyValuePair<ContentId, int>>? itemQuantities = null,
        IEnumerable<KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>>?
            ownedEquipmentInstances = null)
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

        Dictionary<ContentId, IReadOnlyList<RuntimeEquipmentInstanceSnapshot>> equipment = [];
        var seenInstanceIds = new HashSet<RuntimeInstanceId>();
        foreach ((ContentId slotId, IEnumerable<RuntimeEquipmentInstanceSnapshot> instances) in
                 ownedEquipmentInstances ?? [])
        {
            if (!slotId.IsValid)
            {
                throw new ArgumentException(
                    "Owned equipment slot IDs must be valid.",
                    nameof(ownedEquipmentInstances));
            }

            RuntimeEquipmentInstanceSnapshot[] copy = instances?.ToArray() ?? [];
            foreach (RuntimeEquipmentInstanceSnapshot instance in copy)
            {
                ArgumentNullException.ThrowIfNull(instance);
                if (!seenInstanceIds.Add(instance.InstanceId))
                {
                    throw new ArgumentException(
                        $"Equipment instance '{instance.InstanceId}' appears more than once in inventory.",
                        nameof(ownedEquipmentInstances));
                }
            }

            equipment.Add(slotId, Array.AsReadOnly(copy));
        }

        ItemQuantities = RuntimeSnapshotCollections.Dictionary(items);
        OwnedEquipmentInstances = RuntimeSnapshotCollections.Dictionary(equipment);
    }

    public IReadOnlyDictionary<ContentId, int> ItemQuantities { get; }
    public IReadOnlyDictionary<ContentId, IReadOnlyList<RuntimeEquipmentInstanceSnapshot>>
        OwnedEquipmentInstances
    { get; }

    public int GetQuantity(ContentId itemId) =>
        ItemQuantities.TryGetValue(itemId, out int quantity) ? quantity : 0;

    public IReadOnlyList<RuntimeEquipmentInstanceSnapshot> GetEquipmentInstances(
        ContentId slotId) =>
        OwnedEquipmentInstances.TryGetValue(
            slotId,
            out IReadOnlyList<RuntimeEquipmentInstanceSnapshot>? instances)
            ? instances
            : Array.AsReadOnly(Array.Empty<RuntimeEquipmentInstanceSnapshot>());

    public bool OwnsEquipment(RuntimeInstanceId instanceId, ContentId slotId) =>
        GetEquipmentInstances(slotId).Any(instance => instance.InstanceId == instanceId);

    public bool TryGetEquipmentInstance(
        RuntimeInstanceId instanceId,
        out RuntimeEquipmentInstanceSnapshot? instance,
        out ContentId slotId)
    {
        foreach ((ContentId candidateSlotId,
                  IReadOnlyList<RuntimeEquipmentInstanceSnapshot> instances) in
                 OwnedEquipmentInstances)
        {
            RuntimeEquipmentInstanceSnapshot? match =
                instances.FirstOrDefault(candidate => candidate.InstanceId == instanceId);
            if (match is not null)
            {
                instance = match;
                slotId = candidateSlotId;
                return true;
            }
        }

        instance = null;
        slotId = default;
        return false;
    }

    internal RuntimeInventorySnapshot WithItems(IEnumerable<KeyValuePair<ContentId, int>> itemQuantities) =>
        new(
            itemQuantities,
            OwnedEquipmentInstances.Select(pair =>
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    pair.Key,
                    pair.Value)));

    internal RuntimeInventorySnapshot WithEquipment(
        ContentId slotId,
        IEnumerable<RuntimeEquipmentInstanceSnapshot> instances)
    {
        Dictionary<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>> equipment =
            OwnedEquipmentInstances.ToDictionary(
                pair => pair.Key,
                pair => (IEnumerable<RuntimeEquipmentInstanceSnapshot>)pair.Value);
        equipment[slotId] = instances;
        return new RuntimeInventorySnapshot(ItemQuantities, equipment);
    }
}

public sealed record RuntimeWalletSnapshot
{
    public RuntimeWalletSnapshot(int balance)
    {
        if (balance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Currency balance cannot be negative.");
        }

        Balance = balance;
    }

    public int Balance { get; }
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
    InventoryTransitionResult AddEquipment(
        RuntimeInventorySnapshot snapshot,
        RuntimeEquipmentInstanceSnapshot equipment,
        ContentId slotId);
    InventoryTransitionResult RemoveEquipment(
        RuntimeInventorySnapshot snapshot,
        RuntimeInstanceId equipmentInstanceId,
        ContentId slotId,
        IEnumerable<RuntimeEquipmentSnapshot> actorEquipment);
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
        if (quantity > int.MaxValue - current)
        {
            return Rejected(
                snapshot,
                ResourceTransactionCode.NumericOverflow,
                $"Item '{itemId}' quantity would exceed the supported integer range.",
                itemId);
        }

        int next = current + quantity;
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

    public InventoryTransitionResult AddEquipment(
        RuntimeInventorySnapshot snapshot,
        RuntimeEquipmentInstanceSnapshot equipment,
        ContentId slotId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(equipment);
        if (!slotId.IsValid)
        {
            throw new ArgumentException("Equipment slot ID must be valid.", nameof(slotId));
        }

        if (snapshot.TryGetEquipmentInstance(equipment.InstanceId, out _, out _))
        {
            return Rejected(
                snapshot,
                ResourceTransactionCode.EquipmentDuplicate,
                $"Equipment instance '{equipment.InstanceId}' is already owned.",
                equipment.DefinitionId,
                slotId,
                equipment.InstanceId);
        }

        IReadOnlyList<RuntimeEquipmentInstanceSnapshot> current =
            snapshot.GetEquipmentInstances(slotId);
        return Applied(snapshot, snapshot.WithEquipment(slotId, current.Append(equipment)));
    }

    public InventoryTransitionResult RemoveEquipment(
        RuntimeInventorySnapshot snapshot,
        RuntimeInstanceId equipmentInstanceId,
        ContentId slotId,
        IEnumerable<RuntimeEquipmentSnapshot> actorEquipment)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(actorEquipment);
        if (!slotId.IsValid)
        {
            throw new ArgumentException("Equipment slot ID must be valid.", nameof(slotId));
        }

        IReadOnlyList<RuntimeEquipmentInstanceSnapshot> current =
            snapshot.GetEquipmentInstances(slotId);
        RuntimeEquipmentInstanceSnapshot? owned = current.FirstOrDefault(
            instance => instance.InstanceId == equipmentInstanceId);
        if (owned is null)
        {
            return Rejected(
                snapshot,
                ResourceTransactionCode.EquipmentNotOwned,
                $"Equipment instance '{equipmentInstanceId}' is not owned in slot '{slotId}'.",
                slotId: slotId,
                equipmentInstanceId: equipmentInstanceId);
        }
        if (actorEquipment.Any(equipment =>
                equipment.EquippedInstanceIds.Values.Contains(equipmentInstanceId)))
        {
            return Rejected(
                snapshot,
                ResourceTransactionCode.EquippedItemCannotBeRemoved,
                $"Equipment instance '{equipmentInstanceId}' is currently equipped.",
                owned.DefinitionId,
                slotId,
                equipmentInstanceId);
        }

        return Applied(
            snapshot,
            snapshot.WithEquipment(
                slotId,
                current.Where(instance => instance.InstanceId != equipmentInstanceId)));
    }

    private static InventoryTransitionResult Applied(RuntimeInventorySnapshot before, RuntimeInventorySnapshot after) =>
        new(ResourceTransactionCode.Applied, before, after);

    private static InventoryTransitionResult Rejected(
        RuntimeInventorySnapshot before,
        ResourceTransactionCode code,
        string message,
        ContentId? contentId = null,
        ContentId? slotId = null,
        RuntimeInstanceId? equipmentInstanceId = null) =>
        new(
            code,
            before,
            before,
            [new ResourceTransactionDiagnostic(
                code,
                message,
                contentId,
                slotId,
                equipmentInstanceId)]);

    private static InventoryReservationResult ReservationRejected(
        RuntimeInventorySnapshot before,
        ResourceTransactionCode code,
        string message,
        ContentId? contentId = null) =>
        new(code, before, diagnostics: [new ResourceTransactionDiagnostic(code, message, contentId)]);
}

public interface IEconomyTransactionService
{
    WalletTransactionResult Credit(RuntimeWalletSnapshot snapshot, int amount);
    WalletTransactionResult Debit(RuntimeWalletSnapshot snapshot, int amount);
}

public sealed class EconomyTransactionService : IEconomyTransactionService
{
    public WalletTransactionResult Credit(RuntimeWalletSnapshot snapshot, int amount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (amount < 0)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidCurrencyAmount, "Currency amount cannot be negative.");
        }

        if (amount > int.MaxValue - snapshot.Balance)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidCurrencyAmount, "Currency balance cannot exceed the supported integer range.");
        }

        return new WalletTransactionResult(
            ResourceTransactionCode.Applied,
            snapshot,
            new RuntimeWalletSnapshot(snapshot.Balance + amount));
    }

    public WalletTransactionResult Debit(RuntimeWalletSnapshot snapshot, int amount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (amount < 0)
        {
            return Rejected(snapshot, ResourceTransactionCode.InvalidCurrencyAmount, "Currency amount cannot be negative.");
        }
        if (snapshot.Balance < amount)
        {
            return Rejected(snapshot, ResourceTransactionCode.InsufficientCurrency, "Insufficient currency.");
        }

        return new WalletTransactionResult(
            ResourceTransactionCode.Applied,
            snapshot,
            new RuntimeWalletSnapshot(snapshot.Balance - amount));
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
        RuntimeInstanceId equipmentInstanceId,
        ContentId ownedSlotId,
        ContentId targetSlotId,
        IEnumerable<RuntimeEquipmentSnapshot> otherActorEquipment);

    EquipmentTransitionResult Unequip(RuntimeEquipmentSnapshot equipment, ContentId slotId);
}

public sealed class EquipmentTransitionService : IEquipmentTransitionService
{
    private readonly IEquipmentSlotLayoutPolicy _slotLayout;

    public EquipmentTransitionService(IEquipmentSlotLayoutPolicy? slotLayout = null)
    {
        _slotLayout = slotLayout ?? StandardEquipmentSlotLayoutPolicy.Instance;
    }

    public EquipmentTransitionResult Equip(
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        RuntimeInstanceId equipmentInstanceId,
        ContentId ownedSlotId,
        ContentId targetSlotId,
        IEnumerable<RuntimeEquipmentSnapshot> otherActorEquipment)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(otherActorEquipment);
        if (!ownedSlotId.IsValid)
        {
            throw new ArgumentException("Owned equipment slot ID must be valid.", nameof(ownedSlotId));
        }
        if (!targetSlotId.IsValid)
        {
            throw new ArgumentException("Target equipment slot ID must be valid.", nameof(targetSlotId));
        }

        RuntimeEquipmentInstanceSnapshot? owned = null;
        if (inventory.TryGetEquipmentInstance(
                equipmentInstanceId,
                out RuntimeEquipmentInstanceSnapshot? candidate,
                out ContentId actualSlotId) &&
            candidate is not null)
        {
            owned = candidate;
            if (actualSlotId != ownedSlotId)
            {
                return Rejected(
                    equipment,
                    ResourceTransactionCode.EquipmentSlotMismatch,
                    $"Equipment instance '{equipmentInstanceId}' is owned in slot '{actualSlotId}', not '{ownedSlotId}'.",
                    owned.DefinitionId,
                    ownedSlotId,
                    equipmentInstanceId);
            }
        }

        EquipmentSlotLayoutResult assignment =
            _slotLayout.ValidateAssignment(ownedSlotId, targetSlotId);
        if (!assignment.IsCompatible)
        {
            return Rejected(
                equipment,
                ResourceTransactionCode.EquipmentSlotMismatch,
                assignment.Message ??
                $"Equipment instance '{equipmentInstanceId}' cannot be equipped in slot '{targetSlotId}'.",
                owned?.DefinitionId,
                targetSlotId,
                equipmentInstanceId);
        }
        if (owned is null)
        {
            return Rejected(
                equipment,
                ResourceTransactionCode.EquipmentNotOwned,
                $"Equipment instance '{equipmentInstanceId}' is not owned.",
                slotId: ownedSlotId,
                equipmentInstanceId: equipmentInstanceId);
        }
        if (equipment.EquippedInstanceIds.Values.Contains(equipmentInstanceId) ||
            otherActorEquipment.Any(other =>
                other.EquippedInstanceIds.Values.Contains(equipmentInstanceId)))
        {
            return Rejected(
                equipment,
                ResourceTransactionCode.EquipmentAlreadyEquipped,
                $"Equipment instance '{equipmentInstanceId}' is already equipped.",
                owned.DefinitionId,
                targetSlotId,
                equipmentInstanceId);
        }

        Dictionary<ContentId, RuntimeInstanceId> equipped =
            new(equipment.EquippedInstanceIds);
        equipped[targetSlotId] = equipmentInstanceId;
        return new EquipmentTransitionResult(
            ResourceTransactionCode.Applied,
            equipment,
            new RuntimeEquipmentSnapshot(equipped));
    }

    public EquipmentTransitionResult Unequip(RuntimeEquipmentSnapshot equipment, ContentId slotId)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (!slotId.IsValid)
        {
            throw new ArgumentException("Equipment slot ID must be valid.", nameof(slotId));
        }

        EquipmentSlotLayoutResult assignment = _slotLayout.ValidateAssignment(slotId, slotId);
        if (!assignment.IsCompatible)
        {
            return Rejected(
                equipment,
                ResourceTransactionCode.EquipmentSlotMismatch,
                assignment.Message ?? $"Equipment slot '{slotId}' is not supported.",
                slotId: slotId);
        }

        Dictionary<ContentId, RuntimeInstanceId> equipped =
            new(equipment.EquippedInstanceIds);
        equipped.Remove(slotId);
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
        ContentId? slotId = null,
        RuntimeInstanceId? equipmentInstanceId = null) =>
        new(
            code,
            before,
            before,
            [new ResourceTransactionDiagnostic(
                code,
                message,
                contentId,
                slotId,
                equipmentInstanceId)]);
}

public sealed record RuntimeShopOfferSnapshot
{
    private readonly int _basePrice;

    public RuntimeShopOfferSnapshot(
        ShopContentKind ContentKind,
        ContentId ContentId,
        int BasePrice,
        ContentId? EquipmentSlotId = null,
        int? ItemStackLimit = null,
        int? StockAvailable = null)
    {
        this.ContentKind = ContentKind;
        this.ContentId = ContentId;
        this.BasePrice = BasePrice;
        if (EquipmentSlotId is ContentId slotId && !slotId.IsValid)
        {
            throw new ArgumentException(
                "Equipment slot ID must be valid when supplied.",
                nameof(EquipmentSlotId));
        }

        this.EquipmentSlotId = EquipmentSlotId;
        this.ItemStackLimit = ItemStackLimit;
        this.StockAvailable = StockAvailable;
    }

    public ShopContentKind ContentKind { get; init; }
    public ContentId ContentId { get; init; }
    public int BasePrice
    {
        get => _basePrice;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(BasePrice), value, "Shop base price cannot be negative.");
            }

            _basePrice = value;
        }
    }
    public ContentId? EquipmentSlotId { get; init; }
    public int? ItemStackLimit { get; init; }
    public int? StockAvailable { get; init; }

    public void Deconstruct(
        out ShopContentKind ContentKind,
        out ContentId ContentId,
        out int BasePrice,
        out ContentId? EquipmentSlotId,
        out int? ItemStackLimit,
        out int? StockAvailable)
    {
        ContentKind = this.ContentKind;
        ContentId = this.ContentId;
        BasePrice = this.BasePrice;
        EquipmentSlotId = this.EquipmentSlotId;
        ItemStackLimit = this.ItemStackLimit;
        StockAvailable = this.StockAvailable;
    }
}

public enum RuntimeShopOfferResolutionCode
{
    Applied,
    MissingItemDefinition,
    MissingEquipmentDefinition,
    UnsupportedPricePolicy,
    InvalidFixedPrice,
    UnsupportedStockPolicy,
    EquipmentSlotProfileMismatch
}

public sealed record RuntimeShopOfferResolutionDiagnostic(
    RuntimeShopOfferResolutionCode Code,
    ContentId ContentId,
    string Message);

public sealed record RuntimeShopOfferResolutionResult
{
    public RuntimeShopOfferResolutionResult(
        RuntimeShopOfferSnapshot? offer,
        IEnumerable<RuntimeShopOfferResolutionDiagnostic>? diagnostics = null)
    {
        Offer = offer;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeShopOfferSnapshot? Offer { get; }
    public IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Offer is not null && Diagnostics.Count == 0;

    public RuntimeShopOfferSnapshot RequireOffer() =>
        IsSuccess && Offer is not null
            ? Offer
            : throw new InvalidOperationException(
                "Shop offer resolution failed: " +
                string.Join("; ", Diagnostics.Select(diagnostic => diagnostic.Message)));
}

public interface IRuntimeShopOfferResolver
{
    RuntimeShopOfferResolutionResult Resolve(
        ShopOfferDefinition offer,
        IItemDefinitionRepository itemRepository,
        IEquipmentDefinitionRepository equipmentRepository);
}

public sealed class RuntimeShopOfferResolver : IRuntimeShopOfferResolver
{
    private readonly IEquipmentSlotLayoutPolicy _slotLayout;

    public RuntimeShopOfferResolver(IEquipmentSlotLayoutPolicy? slotLayout = null)
    {
        _slotLayout = slotLayout ?? StandardEquipmentSlotLayoutPolicy.Instance;
    }

    public RuntimeShopOfferResolutionResult Resolve(
        ShopOfferDefinition offer,
        IItemDefinitionRepository itemRepository,
        IEquipmentDefinitionRepository equipmentRepository)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(itemRepository);
        ArgumentNullException.ThrowIfNull(equipmentRepository);

        var diagnostics = new List<RuntimeShopOfferResolutionDiagnostic>();
        int? basePrice = ResolvePrice(offer, diagnostics);
        int? stock = ResolveStock(offer, diagnostics);
        ContentId? equipmentSlotId = null;
        int? itemStackLimit = null;

        if (offer.ContentKind == ShopContentKind.Item)
        {
            if (!itemRepository.TryGetItem(offer.ContentId, out ItemDefinition? item) || item is null)
            {
                diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                    RuntimeShopOfferResolutionCode.MissingItemDefinition,
                    offer.ContentId,
                    $"Shop item offer '{offer.ContentId}' does not resolve to an item definition."));
            }
            else
            {
                itemStackLimit = item.StackLimit;
            }
        }
        else if (offer.ContentKind == ShopContentKind.Equipment)
        {
            if (!equipmentRepository.TryGetEquipment(offer.ContentId, out EquipmentDefinition? equipment) ||
                equipment is null)
            {
                diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                    RuntimeShopOfferResolutionCode.MissingEquipmentDefinition,
                    offer.ContentId,
                    $"Shop equipment offer '{offer.ContentId}' does not resolve to an equipment definition."));
            }
            else
            {
                EquipmentSlotLayoutResult layout =
                    _slotLayout.ValidateDefinition(equipment);
                if (!layout.IsCompatible)
                {
                    diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                        RuntimeShopOfferResolutionCode.EquipmentSlotProfileMismatch,
                        offer.ContentId,
                        layout.Message ??
                        $"Shop equipment offer '{offer.ContentId}' has an incompatible slot profile."));
                }
                else
                {
                    equipmentSlotId = equipment.SlotId;
                }
            }
        }

        if (diagnostics.Count > 0 || basePrice is null)
        {
            return new RuntimeShopOfferResolutionResult(null, diagnostics);
        }

        return new RuntimeShopOfferResolutionResult(
            new RuntimeShopOfferSnapshot(
                offer.ContentKind,
                offer.ContentId,
                basePrice.Value,
                equipmentSlotId,
                itemStackLimit,
                stock));
    }

    private static int? ResolvePrice(
        ShopOfferDefinition offer,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        if (offer.Price is not FixedShopPriceDefinition fixedPrice)
        {
            diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                RuntimeShopOfferResolutionCode.UnsupportedPricePolicy,
                offer.ContentId,
                $"Shop offer '{offer.ContentId}' uses pricing kind '{offer.Price.Kind}', which is not supported by the standard runtime shop resolver yet."));
            return null;
        }

        if (fixedPrice.BasePrice < 0 ||
            fixedPrice.BasePrice > int.MaxValue ||
            decimal.Truncate(fixedPrice.BasePrice) != fixedPrice.BasePrice)
        {
            diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                RuntimeShopOfferResolutionCode.InvalidFixedPrice,
                offer.ContentId,
                $"Shop offer '{offer.ContentId}' has fixed price '{fixedPrice.BasePrice}', which must be a nonnegative whole integer."));
            return null;
        }

        return (int)fixedPrice.BasePrice;
    }

    private static int? ResolveStock(
        ShopOfferDefinition offer,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        return offer.Stock switch
        {
            UnlimitedShopStockDefinition => null,
            LimitedShopStockDefinition limited => limited.Quantity,
            _ => AddUnsupportedStock(offer, diagnostics)
        };
    }

    private static int? AddUnsupportedStock(
        ShopOfferDefinition offer,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
            RuntimeShopOfferResolutionCode.UnsupportedStockPolicy,
            offer.ContentId,
            $"Shop offer '{offer.ContentId}' uses stock kind '{offer.Stock.Kind}', which is not supported by the standard runtime shop resolver yet."));
        return null;
    }
}

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
    ShopTransactionResult Buy(
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeShopOfferSnapshot offer,
        int buyerLuck,
        RuntimeInstanceId? purchasedEquipmentInstanceId);
    ShopTransactionResult Sell(
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeShopOfferSnapshot offer,
        int sellerLuck,
        RuntimeInstanceId? soldEquipmentInstanceId,
        IEnumerable<RuntimeEquipmentSnapshot> actorEquipment);
}

public sealed class ShopTransactionService : IShopTransactionService
{
    private const decimal MinimumBuyMultiplier = 0.5m;
    private const decimal BaseSellMultiplier = 0.50m;
    private const decimal LuckPriceStep = 0.01m;

    private readonly IInventoryTransitionService _inventory;
    private readonly IEconomyTransactionService _economy;

    public ShopTransactionService(
        IInventoryTransitionService? inventory = null,
        IEconomyTransactionService? economy = null)
    {
        _inventory = inventory ?? new InventoryTransitionService();
        _economy = economy ?? new EconomyTransactionService();
    }

    public int CalculateBuyPrice(int basePrice, int luck) =>
        RequirePrice(CalculatePrice(basePrice, luck, isBuying: true), basePrice, luck);

    public int CalculateSellPrice(int basePrice, int luck) =>
        RequirePrice(CalculatePrice(basePrice, luck, isBuying: false), basePrice, luck);

    public ShopTransactionResult Buy(
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeShopOfferSnapshot offer,
        int buyerLuck,
        RuntimeInstanceId? purchasedEquipmentInstanceId)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(wallet);
        ShopPriceCalculation pricing = CalculatePrice(offer.BasePrice, buyerLuck, isBuying: true);
        if (!pricing.IsValid)
        {
            return PricingRejected(inventory, wallet, offer, pricing);
        }

        if (offer.StockAvailable is <= 0)
        {
            return Rejected(ResourceTransactionCode.ShopStockUnavailable, inventory, wallet, pricing.Price, "Shop stock is unavailable.", offer.ContentId, offer.EquipmentSlotId);
        }

        int price = pricing.Price;
        InventoryTransitionResult inventoryResult = AddPurchasedContent(
            inventory,
            offer,
            purchasedEquipmentInstanceId);
        if (!inventoryResult.Applied)
        {
            return FromInventory(inventoryResult, wallet, price);
        }

        WalletTransactionResult walletResult = _economy.Debit(wallet, price);
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
        RuntimeInstanceId? soldEquipmentInstanceId,
        IEnumerable<RuntimeEquipmentSnapshot> actorEquipment)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(actorEquipment);
        ShopPriceCalculation pricing = CalculatePrice(offer.BasePrice, sellerLuck, isBuying: false);
        if (!pricing.IsValid)
        {
            return PricingRejected(inventory, wallet, offer, pricing);
        }

        int price = pricing.Price;
        InventoryTransitionResult inventoryResult = offer.ContentKind switch
        {
            ShopContentKind.Item => _inventory.RemoveItem(inventory, offer.ContentId, 1),
            ShopContentKind.Equipment when offer.EquipmentSlotId is ContentId slotId =>
                RemoveSoldEquipment(
                    inventory,
                    offer,
                    slotId,
                    soldEquipmentInstanceId,
                    actorEquipment),
            _ => InventoryRejected(inventory, ResourceTransactionCode.EquipmentSlotMismatch, "Equipment offers require a slot.", offer.ContentId, offer.EquipmentSlotId)
        };

        if (!inventoryResult.Applied)
        {
            return FromInventory(inventoryResult, wallet, price);
        }

        WalletTransactionResult walletResult = _economy.Credit(wallet, price);
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
        RuntimeShopOfferSnapshot offer,
        RuntimeInstanceId? purchasedEquipmentInstanceId) =>
        offer.ContentKind switch
        {
            ShopContentKind.Item => _inventory.AddItem(inventory, offer.ContentId, 1, offer.ItemStackLimit),
            ShopContentKind.Equipment when offer.EquipmentSlotId is ContentId slotId &&
                                           purchasedEquipmentInstanceId is RuntimeInstanceId instanceId &&
                                           instanceId.IsValid =>
                _inventory.AddEquipment(
                    inventory,
                    new RuntimeEquipmentInstanceSnapshot(instanceId, offer.ContentId),
                    slotId),
            ShopContentKind.Equipment => InventoryRejected(
                inventory,
                ResourceTransactionCode.InvalidEquipmentInstanceId,
                "Equipment purchases require a valid host-supplied runtime instance ID.",
                offer.ContentId,
                offer.EquipmentSlotId),
            _ => InventoryRejected(inventory, ResourceTransactionCode.EquipmentSlotMismatch, "Equipment offers require a slot.", offer.ContentId, offer.EquipmentSlotId)
        };

    private InventoryTransitionResult RemoveSoldEquipment(
        RuntimeInventorySnapshot inventory,
        RuntimeShopOfferSnapshot offer,
        ContentId slotId,
        RuntimeInstanceId? soldEquipmentInstanceId,
        IEnumerable<RuntimeEquipmentSnapshot> actorEquipment)
    {
        if (soldEquipmentInstanceId is not RuntimeInstanceId instanceId ||
            !instanceId.IsValid)
        {
            return InventoryRejected(
                inventory,
                ResourceTransactionCode.InvalidEquipmentInstanceId,
                "Equipment sales require a valid runtime instance ID.",
                offer.ContentId,
                slotId);
        }

        if (!inventory.TryGetEquipmentInstance(
                instanceId,
                out RuntimeEquipmentInstanceSnapshot? instance,
                out ContentId ownedSlotId) ||
            instance is null)
        {
            return InventoryRejected(
                inventory,
                ResourceTransactionCode.EquipmentNotOwned,
                $"Equipment instance '{instanceId}' is not owned.",
                offer.ContentId,
                slotId,
                instanceId);
        }

        if (instance.DefinitionId != offer.ContentId || ownedSlotId != slotId)
        {
            return InventoryRejected(
                inventory,
                ResourceTransactionCode.EquipmentSlotMismatch,
                $"Equipment instance '{instanceId}' does not match shop offer '{offer.ContentId}'.",
                offer.ContentId,
                slotId,
                instanceId);
        }

        return _inventory.RemoveEquipment(
            inventory,
            instanceId,
            slotId,
            actorEquipment);
    }

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
        ContentId? slotId,
        RuntimeInstanceId? equipmentInstanceId = null) =>
        new(
            code,
            before,
            before,
            [new ResourceTransactionDiagnostic(
                code,
                message,
                contentId,
                slotId,
                equipmentInstanceId)]);

    private static ShopTransactionResult Rejected(
        ResourceTransactionCode code,
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        int price,
        string message,
        ContentId? contentId = null,
        ContentId? slotId = null) =>
        new(code, inventory, inventory, wallet, wallet, price, [new ResourceTransactionDiagnostic(code, message, contentId, slotId)]);

    private static ShopTransactionResult PricingRejected(
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeShopOfferSnapshot offer,
        ShopPriceCalculation pricing) =>
        Rejected(
            ResourceTransactionCode.InvalidShopPricing,
            inventory,
            wallet,
            price: 0,
            pricing.Message,
            offer.ContentId,
            offer.EquipmentSlotId);

    private static ShopPriceCalculation CalculatePrice(int basePrice, int luck, bool isBuying)
    {
        string operation = isBuying ? "buy" : "sell";
        if (basePrice < 0)
        {
            return ShopPriceCalculation.Invalid(
                ShopPriceFailure.NegativeBasePrice,
                $"Shop {operation} base price cannot be negative (received {basePrice}).");
        }

        if (luck < 0)
        {
            return ShopPriceCalculation.Invalid(
                ShopPriceFailure.NegativeLuck,
                $"Shop {operation} Luck cannot be negative (received {luck}).");
        }

        decimal multiplier = isBuying
            ? Math.Max(MinimumBuyMultiplier, 1m - (luck * LuckPriceStep))
            : BaseSellMultiplier + (luck * LuckPriceStep);
        decimal calculated = decimal.Truncate(checked(basePrice * multiplier));
        if (calculated > int.MaxValue)
        {
            return ShopPriceCalculation.Invalid(
                ShopPriceFailure.ExceedsIntegerRange,
                $"Shop {operation} price for base price {basePrice} and Luck {luck} exceeds the supported integer range.");
        }

        return ShopPriceCalculation.Valid(decimal.ToInt32(calculated));
    }

    private static int RequirePrice(
        ShopPriceCalculation pricing,
        int basePrice,
        int luck) =>
        pricing.Failure switch
        {
            ShopPriceFailure.None => pricing.Price,
            ShopPriceFailure.NegativeBasePrice => throw new ArgumentOutOfRangeException(
                nameof(basePrice),
                basePrice,
                pricing.Message),
            ShopPriceFailure.NegativeLuck => throw new ArgumentOutOfRangeException(
                nameof(luck),
                luck,
                pricing.Message),
            ShopPriceFailure.ExceedsIntegerRange => throw new OverflowException(pricing.Message),
            _ => throw new InvalidOperationException("Unknown shop pricing failure.")
        };

    private enum ShopPriceFailure
    {
        None,
        NegativeBasePrice,
        NegativeLuck,
        ExceedsIntegerRange
    }

    private readonly record struct ShopPriceCalculation(
        int Price,
        ShopPriceFailure Failure,
        string Message)
    {
        public bool IsValid => Failure == ShopPriceFailure.None;

        public static ShopPriceCalculation Valid(int price) =>
            new(price, ShopPriceFailure.None, string.Empty);

        public static ShopPriceCalculation Invalid(ShopPriceFailure failure, string message) =>
            new(0, failure, message);
    }
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
        long cost = (long)patient.MissingHp + ((long)patient.MissingSp * 5L);
        return cost >= int.MaxValue ? int.MaxValue : (int)cost;
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

        WalletTransactionResult spend = _economy.Debit(wallet, cost);
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
