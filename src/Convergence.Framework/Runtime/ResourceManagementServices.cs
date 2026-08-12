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
    InvalidEquipmentInstanceId,
    InvalidCurrencyId,
    DuplicateCurrencyId,
    NegativeCurrencyBalance,
    CurrencyNotFound,
    EmptyCurrencyLedger,
    AmbiguousCurrencyLedger,
    InvalidShopStock
}

public sealed record ResourceTransactionDiagnostic(
    ResourceTransactionCode Code,
    string Message,
    ContentId? ContentId = null,
    ContentId? SlotId = null,
    RuntimeInstanceId? EquipmentInstanceId = null,
    ContentId? CurrencyId = null);

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

public sealed record RuntimeCurrencyBalanceSnapshot
{
    public RuntimeCurrencyBalanceSnapshot(ContentId currencyId, int balance)
    {
        if (!currencyId.IsValid)
        {
            throw new RuntimeCurrencyLedgerException(
                new ResourceTransactionDiagnostic(
                    ResourceTransactionCode.InvalidCurrencyId,
                    "Currency ID cannot be empty.",
                    CurrencyId: currencyId),
                nameof(currencyId));
        }
        if (balance < 0)
        {
            throw new RuntimeCurrencyLedgerException(
                new ResourceTransactionDiagnostic(
                    ResourceTransactionCode.NegativeCurrencyBalance,
                    $"Currency '{currencyId}' balance cannot be negative.",
                    CurrencyId: currencyId),
                nameof(balance));
        }

        CurrencyId = currencyId;
        Balance = balance;
    }

    public ContentId CurrencyId { get; }
    public int Balance { get; }
}

public sealed class RuntimeCurrencyLedgerException : ArgumentException
{
    public RuntimeCurrencyLedgerException(
        ResourceTransactionDiagnostic diagnostic,
        string? parameterName = null)
        : base(
            (diagnostic ?? throw new ArgumentNullException(nameof(diagnostic))).Message,
            parameterName)
    {
        Diagnostic = diagnostic;
    }

    public ResourceTransactionDiagnostic Diagnostic { get; }
}

public sealed record RuntimeCurrencyLedgerSnapshot
{
    public RuntimeCurrencyLedgerSnapshot(
        IEnumerable<KeyValuePair<ContentId, int>>? balances = null)
    {
        var resolved = new Dictionary<ContentId, int>();
        foreach ((ContentId currencyId, int balance) in balances ?? [])
        {
            if (!currencyId.IsValid)
            {
                throw InvalidLedger(
                    ResourceTransactionCode.InvalidCurrencyId,
                    "Currency ID cannot be empty.",
                    currencyId,
                    nameof(balances));
            }
            if (balance < 0)
            {
                throw InvalidLedger(
                    ResourceTransactionCode.NegativeCurrencyBalance,
                    $"Currency '{currencyId}' balance cannot be negative.",
                    currencyId,
                    nameof(balances));
            }
            if (!resolved.TryAdd(currencyId, balance))
            {
                throw InvalidLedger(
                    ResourceTransactionCode.DuplicateCurrencyId,
                    $"Currency '{currencyId}' appears more than once.",
                    currencyId,
                    nameof(balances));
            }
        }

        Balances = RuntimeSnapshotCollections.Dictionary(resolved);
    }

    public IReadOnlyDictionary<ContentId, int> Balances { get; }

    public static RuntimeCurrencyLedgerSnapshot Single(
        ContentId currencyId,
        int balance) =>
        new([new KeyValuePair<ContentId, int>(currencyId, balance)]);

    public bool TryGetBalance(ContentId currencyId, out int balance) =>
        Balances.TryGetValue(currencyId, out balance);

    public int GetRequiredBalance(ContentId currencyId)
    {
        if (!currencyId.IsValid)
        {
            throw InvalidLedger(
                ResourceTransactionCode.InvalidCurrencyId,
                "Currency ID cannot be empty.",
                currencyId,
                nameof(currencyId));
        }
        if (!Balances.TryGetValue(currencyId, out int balance))
        {
            throw InvalidLedger(
                ResourceTransactionCode.CurrencyNotFound,
                $"Currency '{currencyId}' is not present in the ledger.",
                currencyId,
                nameof(currencyId));
        }

        return balance;
    }

    public RuntimeCurrencyBalanceSnapshot GetSingleCurrency()
    {
        if (Balances.Count == 0)
        {
            throw InvalidLedger(
                ResourceTransactionCode.EmptyCurrencyLedger,
                "The currency ledger is empty and has no single currency.");
        }
        if (Balances.Count != 1)
        {
            throw InvalidLedger(
                ResourceTransactionCode.AmbiguousCurrencyLedger,
                "The currency ledger contains multiple currencies and has no unambiguous single currency.");
        }

        KeyValuePair<ContentId, int> balance = Balances.Single();
        return new RuntimeCurrencyBalanceSnapshot(balance.Key, balance.Value);
    }

    internal RuntimeCurrencyLedgerSnapshot WithBalance(
        ContentId currencyId,
        int balance)
    {
        Dictionary<ContentId, int> next = Balances.ToDictionary();
        next[currencyId] = balance;
        return new RuntimeCurrencyLedgerSnapshot(next);
    }

    private static RuntimeCurrencyLedgerException InvalidLedger(
        ResourceTransactionCode code,
        string message,
        ContentId? currencyId = null,
        string? parameterName = null) =>
        new(
            new ResourceTransactionDiagnostic(
                code,
                message,
                CurrencyId: currencyId),
            parameterName);
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

public sealed record CurrencyTransactionResult
{
    public CurrencyTransactionResult(
        ResourceTransactionCode code,
        RuntimeCurrencyLedgerSnapshot before,
        RuntimeCurrencyLedgerSnapshot after,
        ContentId currencyId,
        int amount,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        CurrencyId = currencyId;
        Amount = amount;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Applied => Code == ResourceTransactionCode.Applied;
    public RuntimeCurrencyLedgerSnapshot Before { get; }
    public RuntimeCurrencyLedgerSnapshot After { get; }
    public ContentId CurrencyId { get; }
    public int Amount { get; }
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
    CurrencyTransactionResult Credit(
        RuntimeCurrencyLedgerSnapshot ledger,
        ContentId currencyId,
        int amount);
    CurrencyTransactionResult Debit(
        RuntimeCurrencyLedgerSnapshot ledger,
        ContentId currencyId,
        int amount);
}

public sealed class EconomyTransactionService : IEconomyTransactionService
{
    public CurrencyTransactionResult Credit(
        RuntimeCurrencyLedgerSnapshot ledger,
        ContentId currencyId,
        int amount)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (!currencyId.IsValid)
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.InvalidCurrencyId,
                "Currency ID cannot be empty.");
        }
        if (!ledger.TryGetBalance(currencyId, out int balance))
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.CurrencyNotFound,
                $"Currency '{currencyId}' is not present in the ledger.");
        }
        if (amount < 0)
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.InvalidCurrencyAmount,
                "Currency amount cannot be negative.");
        }

        if (amount > int.MaxValue - balance)
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.NumericOverflow,
                $"Currency '{currencyId}' balance cannot exceed the supported integer range.");
        }

        return new CurrencyTransactionResult(
            ResourceTransactionCode.Applied,
            ledger,
            ledger.WithBalance(currencyId, balance + amount),
            currencyId,
            amount);
    }

    public CurrencyTransactionResult Debit(
        RuntimeCurrencyLedgerSnapshot ledger,
        ContentId currencyId,
        int amount)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (!currencyId.IsValid)
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.InvalidCurrencyId,
                "Currency ID cannot be empty.");
        }
        if (!ledger.TryGetBalance(currencyId, out int balance))
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.CurrencyNotFound,
                $"Currency '{currencyId}' is not present in the ledger.");
        }
        if (amount < 0)
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.InvalidCurrencyAmount,
                "Currency amount cannot be negative.");
        }
        if (balance < amount)
        {
            return Rejected(
                ledger,
                currencyId,
                amount,
                ResourceTransactionCode.InsufficientCurrency,
                $"Insufficient currency '{currencyId}'.");
        }

        return new CurrencyTransactionResult(
            ResourceTransactionCode.Applied,
            ledger,
            ledger.WithBalance(currencyId, balance - amount),
            currencyId,
            amount);
    }

    private static CurrencyTransactionResult Rejected(
        RuntimeCurrencyLedgerSnapshot before,
        ContentId currencyId,
        int amount,
        ResourceTransactionCode code,
        string message) =>
        new(
            code,
            before,
            before,
            currencyId,
            amount,
            [new ResourceTransactionDiagnostic(
                code,
                message,
                CurrencyId: currencyId)]);
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
    private readonly RuntimeShopOfferIdentity _identity;
    private readonly RuntimeShopPricingProfile _pricing;
    private readonly RuntimeShopStockProfile _stock;

    public RuntimeShopOfferSnapshot(
        RuntimeShopOfferIdentity Identity,
        ShopContentKind ContentKind,
        ContentId ContentId,
        RuntimeShopPricingProfile Pricing,
        RuntimeShopStockProfile Stock,
        ContentId? EquipmentSlotId = null,
        int? ItemStackLimit = null)
    {
        _identity = Identity ?? throw new ArgumentNullException(nameof(Identity));
        this.ContentKind = ContentKind;
        this.ContentId = ContentId;
        _pricing = Pricing ?? throw new ArgumentNullException(nameof(Pricing));
        _stock = Stock ?? throw new ArgumentNullException(nameof(Stock));
        if (EquipmentSlotId is ContentId slotId && !slotId.IsValid)
        {
            throw new ArgumentException(
                "Equipment slot ID must be valid when supplied.",
                nameof(EquipmentSlotId));
        }

        this.EquipmentSlotId = EquipmentSlotId;
        this.ItemStackLimit = ItemStackLimit;
    }

    public RuntimeShopOfferIdentity Identity
    {
        get => _identity;
        init => _identity = value ?? throw new ArgumentNullException(nameof(Identity));
    }
    public ShopContentKind ContentKind { get; init; }
    public ContentId ContentId { get; init; }
    public RuntimeShopPricingProfile Pricing
    {
        get => _pricing;
        init => _pricing = value ?? throw new ArgumentNullException(nameof(Pricing));
    }
    public RuntimeShopStockProfile Stock
    {
        get => _stock;
        init => _stock = value ?? throw new ArgumentNullException(nameof(Stock));
    }
    public ContentId? EquipmentSlotId { get; init; }
    public int? ItemStackLimit { get; init; }

    public void Deconstruct(
        out RuntimeShopOfferIdentity Identity,
        out ShopContentKind ContentKind,
        out ContentId ContentId,
        out RuntimeShopPricingProfile Pricing,
        out RuntimeShopStockProfile Stock,
        out ContentId? EquipmentSlotId,
        out int? ItemStackLimit)
    {
        Identity = this.Identity;
        ContentKind = this.ContentKind;
        ContentId = this.ContentId;
        Pricing = this.Pricing;
        Stock = this.Stock;
        EquipmentSlotId = this.EquipmentSlotId;
        ItemStackLimit = this.ItemStackLimit;
    }
}

public enum RuntimeShopOfferResolutionCode
{
    Applied,
    MissingItemDefinition,
    MissingEquipmentDefinition,
    UnsupportedPricePolicy,
    InvalidFixedPrice,
    InvalidPricePolicyConfiguration,
    UnsupportedStockPolicy,
    InvalidStockPolicyConfiguration,
    InvalidOfferIdentity,
    EquipmentSlotProfileMismatch
}

public sealed record RuntimeShopOfferResolutionDiagnostic(
    RuntimeShopOfferResolutionCode Code,
    ContentId ContentId,
    string Message,
    ShopPricingPolicyDiagnostic? PricingDiagnostic = null,
    ShopStockPolicyDiagnostic? StockDiagnostic = null);

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
        ContentId shopId,
        ShopOfferDefinition offer,
        IItemDefinitionRepository itemRepository,
        IEquipmentDefinitionRepository equipmentRepository);
}

public sealed class RuntimeShopOfferResolver : IRuntimeShopOfferResolver
{
    private readonly IEquipmentSlotLayoutPolicy _slotLayout;
    private readonly BoundShopPricingPolicy _defaultPricing;
    private readonly ShopPricingPolicyFactoryRegistry _pricingFactories;
    private readonly ShopStockPolicyFactoryRegistry _stockFactories;

    public RuntimeShopOfferResolver(
        BoundShopPricingPolicy defaultPricing,
        ShopPricingPolicyFactoryRegistry pricingFactories,
        ShopStockPolicyFactoryRegistry stockFactories,
        IEquipmentSlotLayoutPolicy? slotLayout = null)
    {
        _defaultPricing = defaultPricing ?? throw new ArgumentNullException(nameof(defaultPricing));
        _pricingFactories = pricingFactories ?? throw new ArgumentNullException(nameof(pricingFactories));
        _stockFactories = stockFactories ?? throw new ArgumentNullException(nameof(stockFactories));
        _slotLayout = slotLayout ?? StandardEquipmentSlotLayoutPolicy.Instance;
    }

    public RuntimeShopOfferResolutionResult Resolve(
        ContentId shopId,
        ShopOfferDefinition offer,
        IItemDefinitionRepository itemRepository,
        IEquipmentDefinitionRepository equipmentRepository)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(itemRepository);
        ArgumentNullException.ThrowIfNull(equipmentRepository);

        var diagnostics = new List<RuntimeShopOfferResolutionDiagnostic>();
        RuntimeShopOfferIdentity? identity = ResolveIdentity(shopId, offer, diagnostics);
        RuntimeShopPricingProfile? pricing = ResolvePrice(offer, diagnostics);
        RuntimeShopStockProfile? stock = ResolveStock(offer, diagnostics);
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

        if (diagnostics.Count > 0 || identity is null || pricing is null || stock is null)
        {
            return new RuntimeShopOfferResolutionResult(null, diagnostics);
        }

        return new RuntimeShopOfferResolutionResult(
            new RuntimeShopOfferSnapshot(
                identity,
                offer.ContentKind,
                offer.ContentId,
                pricing,
                stock,
                equipmentSlotId,
                itemStackLimit));
    }

    private static RuntimeShopOfferIdentity? ResolveIdentity(
        ContentId shopId,
        ShopOfferDefinition offer,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        try
        {
            return new RuntimeShopOfferIdentity(shopId, offer.Id);
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                RuntimeShopOfferResolutionCode.InvalidOfferIdentity,
                offer.ContentId,
                $"Shop offer identity '{shopId}/{offer.Id}' is invalid: {exception.Message}"));
            return null;
        }
    }

    private RuntimeShopPricingProfile? ResolvePrice(
        ShopOfferDefinition offer,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        if (offer.Price is FixedShopPriceDefinition fixedPrice)
        {
            int? fixedPurchasePrice = ResolveWholePurchasePrice(
                offer,
                fixedPrice.BasePrice,
                RuntimeShopOfferResolutionCode.InvalidFixedPrice,
                "fixed price",
                diagnostics);
            return fixedPurchasePrice is int price
                ? new RuntimeShopPricingProfile(price, _defaultPricing)
                : null;
        }

        if (offer.Price is not PolicyShopPriceDefinition policyPrice)
        {
            diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                RuntimeShopOfferResolutionCode.UnsupportedPricePolicy,
                offer.ContentId,
                $"Shop offer '{offer.ContentId}' uses unsupported pricing kind '{offer.Price.Kind}'."));
            return null;
        }

        if (!policyPrice.Parameters.TryGetValue("purchasePrice", out object? rawPurchasePrice) ||
            !RulesetPolicyFactoryParameters.TryReadDecimal(rawPurchasePrice, out decimal authoredPurchasePrice))
        {
            diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                RuntimeShopOfferResolutionCode.InvalidPricePolicyConfiguration,
                offer.ContentId,
                $"Shop offer '{offer.ContentId}' policy price requires a decimal 'purchasePrice' parameter.",
                new ShopPricingPolicyDiagnostic(
                    policyPrice.Parameters.ContainsKey("purchasePrice")
                        ? ShopPricingPolicyDiagnosticCode.InvalidParameterType
                        : ShopPricingPolicyDiagnosticCode.MissingParameter,
                    $"Shop pricing policy '{policyPrice.PricingPolicyId}' requires a decimal 'purchasePrice' parameter.",
                    "purchasePrice",
                    policyPrice.PricingPolicyId)));
            return null;
        }

        int? purchasePrice = ResolveWholePurchasePrice(
            offer,
            authoredPurchasePrice,
            RuntimeShopOfferResolutionCode.InvalidPricePolicyConfiguration,
            "policy purchase price",
            diagnostics);
        if (purchasePrice is null)
        {
            return null;
        }

        var policyParameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string key, object? value) in policyPrice.Parameters)
        {
            if (!string.Equals(key, "purchasePrice", StringComparison.Ordinal))
            {
                policyParameters.Add(key, value);
            }
        }

        ShopPricingPolicyBindingResult binding =
            _pricingFactories.Bind(policyPrice.PricingPolicyId, policyParameters);
        if (!binding.IsSuccess || binding.Policy is null)
        {
            foreach (ShopPricingPolicyDiagnostic diagnostic in binding.Diagnostics)
            {
                diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                    diagnostic.Code == ShopPricingPolicyDiagnosticCode.UnsupportedPolicy
                        ? RuntimeShopOfferResolutionCode.UnsupportedPricePolicy
                        : RuntimeShopOfferResolutionCode.InvalidPricePolicyConfiguration,
                    offer.ContentId,
                    $"Shop offer '{offer.ContentId}' pricing rejected: {diagnostic.Message}",
                    diagnostic));
            }

            return null;
        }

        return new RuntimeShopPricingProfile(purchasePrice.Value, binding.Policy);
    }

    private static int? ResolveWholePurchasePrice(
        ShopOfferDefinition offer,
        decimal value,
        RuntimeShopOfferResolutionCode code,
        string description,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        if (value < 0 || value > int.MaxValue || decimal.Truncate(value) != value)
        {
            diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                code,
                offer.ContentId,
                $"Shop offer '{offer.ContentId}' has {description} '{value}', which must be a nonnegative whole integer."));
            return null;
        }

        return (int)value;
    }

    private RuntimeShopStockProfile? ResolveStock(
        ShopOfferDefinition offer,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        return offer.Stock switch
        {
            UnlimitedShopStockDefinition => RuntimeShopStockProfile.Unlimited,
            LimitedShopStockDefinition limited => BindStock(
                offer,
                limited.Quantity,
                StandardShopStockPolicyIds.Standard,
                new Dictionary<string, object?>(StringComparer.Ordinal),
                diagnostics),
            PolicyShopStockDefinition policy => BindStock(
                offer,
                policy.Quantity,
                policy.StockPolicyId,
                policy.Parameters,
                diagnostics),
            _ => AddUnsupportedStock(offer, diagnostics)
        };
    }

    private RuntimeShopStockProfile? BindStock(
        ShopOfferDefinition offer,
        int initialQuantity,
        ContentId policyId,
        IReadOnlyDictionary<string, object?> parameters,
        ICollection<RuntimeShopOfferResolutionDiagnostic> diagnostics)
    {
        if (initialQuantity <= 0)
        {
            diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                RuntimeShopOfferResolutionCode.InvalidStockPolicyConfiguration,
                offer.ContentId,
                $"Shop offer '{offer.Id}' requires a positive initial stock quantity."));
            return null;
        }

        ShopStockPolicyBindingResult binding = _stockFactories.Bind(policyId, parameters);
        if (!binding.IsSuccess || binding.Policy is null)
        {
            foreach (ShopStockPolicyDiagnostic diagnostic in binding.Diagnostics)
            {
                diagnostics.Add(new RuntimeShopOfferResolutionDiagnostic(
                    diagnostic.Code == ShopStockPolicyDiagnosticCode.UnsupportedPolicy
                        ? RuntimeShopOfferResolutionCode.UnsupportedStockPolicy
                        : RuntimeShopOfferResolutionCode.InvalidStockPolicyConfiguration,
                    offer.ContentId,
                    $"Shop offer '{offer.Id}' stock policy rejected: {diagnostic.Message}",
                    StockDiagnostic: diagnostic));
            }

            return null;
        }

        return new RuntimeShopStockProfile(initialQuantity, binding.Policy);
    }

    private static RuntimeShopStockProfile? AddUnsupportedStock(
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
        RuntimeCurrencyLedgerSnapshot beforeCurrencyLedger,
        RuntimeCurrencyLedgerSnapshot afterCurrencyLedger,
        RuntimeShopStockSnapshot beforeStock,
        RuntimeShopStockSnapshot afterStock,
        ContentId currencyId,
        int price,
        IEnumerable<ResourceTransactionDiagnostic>? diagnostics = null)
    {
        Code = code;
        BeforeInventory = beforeInventory ?? throw new ArgumentNullException(nameof(beforeInventory));
        AfterInventory = afterInventory ?? throw new ArgumentNullException(nameof(afterInventory));
        BeforeCurrencyLedger = beforeCurrencyLedger ??
            throw new ArgumentNullException(nameof(beforeCurrencyLedger));
        AfterCurrencyLedger = afterCurrencyLedger ??
            throw new ArgumentNullException(nameof(afterCurrencyLedger));
        BeforeStock = beforeStock ?? throw new ArgumentNullException(nameof(beforeStock));
        AfterStock = afterStock ?? throw new ArgumentNullException(nameof(afterStock));
        CurrencyId = currencyId;
        Price = price;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public ResourceTransactionCode Code { get; }
    public bool Applied => Code == ResourceTransactionCode.Applied;
    public RuntimeInventorySnapshot BeforeInventory { get; }
    public RuntimeInventorySnapshot AfterInventory { get; }
    public RuntimeCurrencyLedgerSnapshot BeforeCurrencyLedger { get; }
    public RuntimeCurrencyLedgerSnapshot AfterCurrencyLedger { get; }
    public RuntimeShopStockSnapshot BeforeStock { get; }
    public RuntimeShopStockSnapshot AfterStock { get; }
    public ContentId CurrencyId { get; }
    public int Price { get; }
    public IReadOnlyList<ResourceTransactionDiagnostic> Diagnostics { get; }
}

public interface IShopTransactionService
{
    int CalculateBuyPrice(RuntimeShopOfferSnapshot offer, int luck);
    int CalculateSellPrice(RuntimeShopOfferSnapshot offer, int luck);
    ShopTransactionResult Buy(
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot stock,
        ContentId currencyId,
        RuntimeShopOfferSnapshot offer,
        int buyerLuck,
        RuntimeInstanceId? purchasedEquipmentInstanceId);
    ShopTransactionResult Sell(
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot stock,
        ContentId currencyId,
        RuntimeShopOfferSnapshot offer,
        int sellerLuck,
        RuntimeInstanceId? soldEquipmentInstanceId,
        IEnumerable<RuntimeEquipmentSnapshot> actorEquipment);
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

    public int CalculateBuyPrice(RuntimeShopOfferSnapshot offer, int luck)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return RequirePrice(
            offer.Pricing.Calculate(ShopPriceOperation.Purchase, luck),
            offer,
            luck);
    }

    public int CalculateSellPrice(RuntimeShopOfferSnapshot offer, int luck)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return RequirePrice(
            offer.Pricing.Calculate(ShopPriceOperation.Resale, luck),
            offer,
            luck);
    }

    public ShopTransactionResult Buy(
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot stock,
        ContentId currencyId,
        RuntimeShopOfferSnapshot offer,
        int buyerLuck,
        RuntimeInstanceId? purchasedEquipmentInstanceId)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(currencyLedger);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(offer);
        ShopPriceCalculationResult pricing =
            offer.Pricing.Calculate(ShopPriceOperation.Purchase, buyerLuck);
        if (!pricing.IsSuccess)
        {
            return PricingRejected(inventory, currencyLedger, stock, currencyId, offer, pricing);
        }

        StockTransitionCandidate stockResult = TransitionStock(
            stock,
            offer,
            ShopStockOperation.Purchase);
        if (!stockResult.Applied)
        {
            return Rejected(
                stockResult.Code,
                inventory,
                currencyLedger,
                stock,
                currencyId,
                pricing.Price,
                stockResult.Message,
                offer.ContentId,
                offer.EquipmentSlotId);
        }

        int price = pricing.Price;
        InventoryTransitionResult inventoryResult = AddPurchasedContent(
            inventory,
            offer,
            purchasedEquipmentInstanceId);
        if (!inventoryResult.Applied)
        {
            return FromInventory(inventoryResult, currencyLedger, stock, currencyId, price);
        }

        CurrencyTransactionResult currencyResult =
            _economy.Debit(currencyLedger, currencyId, price);
        if (!currencyResult.Applied)
        {
            return FromCurrency(currencyResult, inventory, stock, price);
        }

        return new ShopTransactionResult(
            ResourceTransactionCode.Applied,
            inventory,
            inventoryResult.After,
            currencyLedger,
            currencyResult.After,
            stock,
            stockResult.After,
            currencyId,
            price);
    }

    public ShopTransactionResult Sell(
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot stock,
        ContentId currencyId,
        RuntimeShopOfferSnapshot offer,
        int sellerLuck,
        RuntimeInstanceId? soldEquipmentInstanceId,
        IEnumerable<RuntimeEquipmentSnapshot> actorEquipment)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(currencyLedger);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(actorEquipment);
        ArgumentNullException.ThrowIfNull(offer);
        ShopPriceCalculationResult pricing =
            offer.Pricing.Calculate(ShopPriceOperation.Resale, sellerLuck);
        if (!pricing.IsSuccess)
        {
            return PricingRejected(inventory, currencyLedger, stock, currencyId, offer, pricing);
        }

        StockTransitionCandidate stockResult = TransitionStock(
            stock,
            offer,
            ShopStockOperation.Resale);
        if (!stockResult.Applied)
        {
            return Rejected(
                stockResult.Code,
                inventory,
                currencyLedger,
                stock,
                currencyId,
                pricing.Price,
                stockResult.Message,
                offer.ContentId,
                offer.EquipmentSlotId);
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
            return FromInventory(inventoryResult, currencyLedger, stock, currencyId, price);
        }

        CurrencyTransactionResult currencyResult =
            _economy.Credit(currencyLedger, currencyId, price);
        if (!currencyResult.Applied)
        {
            return FromCurrency(currencyResult, inventory, stock, price);
        }

        return new ShopTransactionResult(
            ResourceTransactionCode.Applied,
            inventory,
            inventoryResult.After,
            currencyLedger,
            currencyResult.After,
            stock,
            stockResult.After,
            currencyId,
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

    private static StockTransitionCandidate TransitionStock(
        RuntimeShopStockSnapshot stock,
        RuntimeShopOfferSnapshot offer,
        ShopStockOperation operation)
    {
        if (!offer.Stock.IsTracked)
        {
            if (stock.Entries.Any(entry => entry.OfferIdentity == offer.Identity))
            {
                return StockTransitionCandidate.Rejected(
                    stock,
                    ResourceTransactionCode.InvalidShopStock,
                    $"Unlimited shop offer '{offer.Identity.ShopId}/{offer.Identity.OfferId}' must not have durable stock state.");
            }

            return StockTransitionCandidate.Success(stock);
        }

        RuntimeShopStockEntrySnapshot[] matches = stock.Entries
            .Where(entry => entry.OfferIdentity == offer.Identity)
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            return StockTransitionCandidate.Rejected(
                stock,
                ResourceTransactionCode.InvalidShopStock,
                $"Tracked shop offer '{offer.Identity.ShopId}/{offer.Identity.OfferId}' must have exactly one stock entry.");
        }

        int currentQuantity = matches[0].RemainingQuantity;
        if (currentQuantity < 0)
        {
            return StockTransitionCandidate.Rejected(
                stock,
                ResourceTransactionCode.InvalidShopStock,
                $"Tracked shop offer '{offer.Identity.ShopId}/{offer.Identity.OfferId}' has a negative stock quantity.");
        }

        ShopStockTransitionResult transition = offer.Stock.Apply(operation, currentQuantity);
        if (!transition.IsSuccess)
        {
            return StockTransitionCandidate.Rejected(
                stock,
                transition.Code == ShopStockTransitionCode.Unavailable
                    ? ResourceTransactionCode.ShopStockUnavailable
                    : ResourceTransactionCode.InvalidShopStock,
                transition.Message ?? "Shop stock policy rejected the transaction.");
        }

        return StockTransitionCandidate.Success(
            stock.WithRemainingQuantity(offer.Identity, transition.RemainingQuantity));
    }

    private sealed record StockTransitionCandidate(
        bool Applied,
        ResourceTransactionCode Code,
        RuntimeShopStockSnapshot After,
        string Message)
    {
        public static StockTransitionCandidate Success(RuntimeShopStockSnapshot after) =>
            new(true, ResourceTransactionCode.Applied, after, string.Empty);

        public static StockTransitionCandidate Rejected(
            RuntimeShopStockSnapshot before,
            ResourceTransactionCode code,
            string message) =>
            new(false, code, before, message);
    }

    private static ShopTransactionResult FromInventory(
        InventoryTransitionResult result,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot stock,
        ContentId currencyId,
        int price) =>
        new(
            result.Code,
            result.Before,
            result.Before,
            currencyLedger,
            currencyLedger,
            stock,
            stock,
            currencyId,
            price,
            result.Diagnostics);

    private static ShopTransactionResult FromCurrency(
        CurrencyTransactionResult result,
        RuntimeInventorySnapshot inventory,
        RuntimeShopStockSnapshot stock,
        int price) =>
        new(
            result.Code,
            inventory,
            inventory,
            result.Before,
            result.Before,
            stock,
            stock,
            result.CurrencyId,
            price,
            result.Diagnostics);

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
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot stock,
        ContentId currencyId,
        int price,
        string message,
        ContentId? contentId = null,
        ContentId? slotId = null) =>
        new(
            code,
            inventory,
            inventory,
            currencyLedger,
            currencyLedger,
            stock,
            stock,
            currencyId,
            price,
            [new ResourceTransactionDiagnostic(
                code,
                message,
                contentId,
                slotId,
                CurrencyId: currencyId)]);

    private static ShopTransactionResult PricingRejected(
        RuntimeInventorySnapshot inventory,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        RuntimeShopStockSnapshot stock,
        ContentId currencyId,
        RuntimeShopOfferSnapshot offer,
        ShopPriceCalculationResult pricing) =>
        Rejected(
            ResourceTransactionCode.InvalidShopPricing,
            inventory,
            currencyLedger,
            stock,
            currencyId,
            price: 0,
            pricing.Message ?? "Shop pricing policy rejected the transaction.",
            offer.ContentId,
            offer.EquipmentSlotId);

    private static int RequirePrice(
        ShopPriceCalculationResult pricing,
        RuntimeShopOfferSnapshot offer,
        int luck) =>
        pricing.Code switch
        {
            ShopPriceCalculationCode.Applied => pricing.Price,
            ShopPriceCalculationCode.NegativePurchasePrice => throw new ArgumentOutOfRangeException(
                nameof(offer),
                offer.Pricing.AuthoredPurchasePrice,
                pricing.Message),
            ShopPriceCalculationCode.NegativeLuck => throw new ArgumentOutOfRangeException(
                nameof(luck),
                luck,
                pricing.Message),
            ShopPriceCalculationCode.NumericOverflow => throw new OverflowException(pricing.Message),
            ShopPriceCalculationCode.PolicyRejected => throw new InvalidOperationException(pricing.Message),
            _ => throw new InvalidOperationException("Unknown shop pricing result code.")
        };
}
