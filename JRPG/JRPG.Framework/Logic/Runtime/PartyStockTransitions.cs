using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Runtime;

public enum PartyStockTransitionCode
{
    Applied,
    PartyFull,
    StockFull,
    NotOwned,
    AlreadyActive,
    NotActive,
    ActiveNotFound,
    ReserveNotFound,
    StockEntryNotFound,
    DuplicateOwned,
    ActiveFormMissing,
    InvalidSlot,
    CapacityExceeded
}

public sealed record PartyStockTransitionDiagnostic(
    PartyStockTransitionCode Code,
    string Message,
    RuntimeInstanceId? SubjectInstanceId = null);

public sealed record PartyStockTransitionResult
{
    public PartyStockTransitionResult(
        PartyStockTransitionCode code,
        RuntimePartyStockSnapshot before,
        RuntimePartyStockSnapshot after,
        IEnumerable<RuntimeInstanceId>? affectedInstanceIds = null,
        IEnumerable<PartyStockTransitionDiagnostic>? diagnostics = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        AffectedInstanceIds = RuntimeSnapshotCollections.List(affectedInstanceIds);
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public PartyStockTransitionCode Code { get; }
    public bool Applied => Code == PartyStockTransitionCode.Applied;
    public RuntimePartyStockSnapshot Before { get; }
    public RuntimePartyStockSnapshot After { get; }
    public IReadOnlyList<RuntimeInstanceId> AffectedInstanceIds { get; }
    public IReadOnlyList<PartyStockTransitionDiagnostic> Diagnostics { get; }
}

public sealed record RuntimePartyStockSnapshot
{
    public RuntimePartyStockSnapshot(
        RuntimeActorReferenceSnapshot owner,
        int ownerLevel,
        IEnumerable<RuntimeActorReferenceSnapshot>? activeParty = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? reserveMembers = null,
        RuntimeActorReferenceSnapshot? activeForm = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? personaStock = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? demonStock = null,
        int maxActivePartySize = 4)
    {
        if (ownerLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerLevel), "Owner level must be positive.");
        }
        if (maxActivePartySize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxActivePartySize), "Maximum active party size must be positive.");
        }

        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        OwnerLevel = ownerLevel;
        ActiveParty = RuntimeSnapshotCollections.List(activeParty);
        ReserveMembers = RuntimeSnapshotCollections.List(reserveMembers);
        ActiveForm = activeForm;
        PersonaStock = RuntimeSnapshotCollections.List(personaStock);
        DemonStock = RuntimeSnapshotCollections.List(demonStock);
        MaxActivePartySize = maxActivePartySize;
    }

    public RuntimeActorReferenceSnapshot Owner { get; }
    public int OwnerLevel { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> ActiveParty { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> ReserveMembers { get; }
    public RuntimeActorReferenceSnapshot? ActiveForm { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> PersonaStock { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> DemonStock { get; }
    public int MaxActivePartySize { get; }

    public RuntimePartyStockSnapshot With(
        IEnumerable<RuntimeActorReferenceSnapshot>? activeParty = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? reserveMembers = null,
        RuntimeActorReferenceSnapshot? activeForm = null,
        bool replaceActiveForm = false,
        IEnumerable<RuntimeActorReferenceSnapshot>? personaStock = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? demonStock = null) =>
        new(
            Owner,
            OwnerLevel,
            activeParty ?? ActiveParty,
            reserveMembers ?? ReserveMembers,
            replaceActiveForm ? activeForm : ActiveForm,
            personaStock ?? PersonaStock,
            demonStock ?? DemonStock,
            MaxActivePartySize);
}

public interface IStockCapacityPolicy
{
    int GetCapacity(int ownerLevel);
}

public sealed class LegacyStockCapacityPolicy : IStockCapacityPolicy
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

public sealed record AddPartyMemberRequest(RuntimePartyStockSnapshot Snapshot, RuntimeActorReferenceSnapshot Member);
public sealed record SwapPartyMemberRequest(RuntimePartyStockSnapshot Snapshot, int ActiveIndex, int ReserveIndex);
public sealed record AddDemonToStockRequest(RuntimePartyStockSnapshot Snapshot, RuntimeActorReferenceSnapshot Demon);
public sealed record AddPersonaToStockRequest(RuntimePartyStockSnapshot Snapshot, RuntimeActorReferenceSnapshot Persona);
public sealed record SummonDemonRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId DemonInstanceId);
public sealed record SwapActiveDemonRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId ActiveDemonInstanceId, RuntimeInstanceId StandbyDemonInstanceId);
public sealed record ReturnDemonRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId DemonInstanceId);
public sealed record DismissDemonRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId DemonInstanceId);
public sealed record ReplaceDemonRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId OldDemonInstanceId, RuntimeActorReferenceSnapshot NewDemon);
public sealed record ConsumeDemonRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId DemonInstanceId);
public sealed record SwapActivePersonaRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId PersonaInstanceId);
public sealed record ConsumePersonaRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId PersonaInstanceId);
public sealed record ReplacePersonaRequest(RuntimePartyStockSnapshot Snapshot, RuntimeInstanceId OldPersonaInstanceId, RuntimeActorReferenceSnapshot NewPersona);

public interface IPartyStockTransitionService
{
    PartyStockTransitionResult AddPartyMember(AddPartyMemberRequest request);
    PartyStockTransitionResult SwapPartyMember(SwapPartyMemberRequest request);
    PartyStockTransitionResult AddDemonToStock(AddDemonToStockRequest request);
    PartyStockTransitionResult AddPersonaToStock(AddPersonaToStockRequest request);
    PartyStockTransitionResult SummonDemon(SummonDemonRequest request);
    PartyStockTransitionResult SwapActiveDemon(SwapActiveDemonRequest request);
    PartyStockTransitionResult ReturnDemon(ReturnDemonRequest request);
    PartyStockTransitionResult DismissDemon(DismissDemonRequest request);
    PartyStockTransitionResult ReplaceDemon(ReplaceDemonRequest request);
    PartyStockTransitionResult ConsumeDemon(ConsumeDemonRequest request);
    PartyStockTransitionResult SwapActivePersona(SwapActivePersonaRequest request);
    PartyStockTransitionResult ConsumePersona(ConsumePersonaRequest request);
    PartyStockTransitionResult ReplacePersona(ReplacePersonaRequest request);
}

public sealed class PartyStockTransitionService : IPartyStockTransitionService
{
    private readonly IStockCapacityPolicy _stockCapacityPolicy;

    public PartyStockTransitionService(IStockCapacityPolicy? stockCapacityPolicy = null)
    {
        _stockCapacityPolicy = stockCapacityPolicy ?? new LegacyStockCapacityPolicy();
    }

    public PartyStockTransitionResult AddPartyMember(AddPartyMemberRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (Contains(before.ActiveParty, request.Member.InstanceId) || Contains(before.ReserveMembers, request.Member.InstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.DuplicateOwned, "Party member is already present.", request.Member.InstanceId);
        }

        if (before.ActiveParty.Count < before.MaxActivePartySize)
        {
            return Applied(
                before,
                before.With(activeParty: before.ActiveParty.Append(request.Member)),
                request.Member.InstanceId);
        }

        return Applied(
            before,
            before.With(reserveMembers: before.ReserveMembers.Append(request.Member)),
            request.Member.InstanceId);
    }

    public PartyStockTransitionResult SwapPartyMember(SwapPartyMemberRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (request.ActiveIndex < 0 || request.ActiveIndex >= before.ActiveParty.Count)
        {
            return Rejected(before, PartyStockTransitionCode.ActiveNotFound, "Active party index is out of range.");
        }
        if (request.ReserveIndex < 0 || request.ReserveIndex >= before.ReserveMembers.Count)
        {
            return Rejected(before, PartyStockTransitionCode.ReserveNotFound, "Reserve party index is out of range.");
        }

        RuntimeActorReferenceSnapshot[] active = before.ActiveParty.ToArray();
        RuntimeActorReferenceSnapshot[] reserve = before.ReserveMembers.ToArray();
        RuntimeActorReferenceSnapshot oldActive = active[request.ActiveIndex];
        RuntimeActorReferenceSnapshot oldReserve = reserve[request.ReserveIndex];
        active[request.ActiveIndex] = oldReserve;
        reserve[request.ReserveIndex] = oldActive;
        return Applied(before, before.With(activeParty: active, reserveMembers: reserve), oldActive.InstanceId, oldReserve.InstanceId);
    }

    public PartyStockTransitionResult AddDemonToStock(AddDemonToStockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (Contains(before.ActiveParty, request.Demon.InstanceId) || Contains(before.DemonStock, request.Demon.InstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.DuplicateOwned, "Demon is already present.", request.Demon.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] demonStock = before.DemonStock.Append(request.Demon).ToArray();
        if (demonStock.Length > _stockCapacityPolicy.GetCapacity(before.OwnerLevel))
        {
            return Rejected(before, PartyStockTransitionCode.StockFull, "Demon stock is full.", request.Demon.InstanceId);
        }

        return Applied(before, before.With(demonStock: demonStock), request.Demon.InstanceId);
    }

    public PartyStockTransitionResult AddPersonaToStock(AddPersonaToStockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (before.ActiveForm?.InstanceId == request.Persona.InstanceId ||
            Contains(before.PersonaStock, request.Persona.InstanceId))
        {
            return Rejected(
                before,
                PartyStockTransitionCode.DuplicateOwned,
                "Persona is already present.",
                request.Persona.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] personaStock = before.PersonaStock.Append(request.Persona).ToArray();
        if (personaStock.Length > _stockCapacityPolicy.GetCapacity(before.OwnerLevel))
        {
            return Rejected(
                before,
                PartyStockTransitionCode.StockFull,
                "Persona stock is full.",
                request.Persona.InstanceId);
        }

        return Applied(before, before.With(personaStock: personaStock), request.Persona.InstanceId);
    }

    public PartyStockTransitionResult SummonDemon(SummonDemonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        RuntimeActorReferenceSnapshot? demon = Find(before.DemonStock, request.DemonInstanceId);
        if (demon is null)
        {
            return Rejected(before, PartyStockTransitionCode.NotOwned, "Demon is not owned.", request.DemonInstanceId);
        }
        if (Contains(before.ActiveParty, request.DemonInstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.AlreadyActive, "Demon is already active.", request.DemonInstanceId);
        }
        if (before.ActiveParty.Count >= before.MaxActivePartySize)
        {
            return Rejected(before, PartyStockTransitionCode.PartyFull, "Active party is full.", request.DemonInstanceId);
        }

        return Applied(before, before.With(activeParty: before.ActiveParty.Append(demon)), request.DemonInstanceId);
    }

    public PartyStockTransitionResult SwapActiveDemon(SwapActiveDemonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        int activeIndex = IndexOf(before.ActiveParty, request.ActiveDemonInstanceId);
        if (activeIndex < 0)
        {
            return Rejected(before, PartyStockTransitionCode.NotActive, "Active demon is not in the party.", request.ActiveDemonInstanceId);
        }

        RuntimeActorReferenceSnapshot? standby = Find(before.DemonStock, request.StandbyDemonInstanceId);
        if (standby is null)
        {
            return Rejected(before, PartyStockTransitionCode.NotOwned, "Standby demon is not owned.", request.StandbyDemonInstanceId);
        }
        if (Contains(before.ActiveParty, request.StandbyDemonInstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.AlreadyActive, "Standby demon is already active.", request.StandbyDemonInstanceId);
        }

        RuntimeActorReferenceSnapshot[] active = before.ActiveParty.ToArray();
        active[activeIndex] = standby;
        return Applied(before, before.With(activeParty: active), request.ActiveDemonInstanceId, request.StandbyDemonInstanceId);
    }

    public PartyStockTransitionResult ReturnDemon(ReturnDemonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (!Contains(before.ActiveParty, request.DemonInstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.NotActive, "Demon is not active.", request.DemonInstanceId);
        }

        return Applied(
            before,
            before.With(activeParty: before.ActiveParty.Where(actor => actor.InstanceId != request.DemonInstanceId)),
            request.DemonInstanceId);
    }

    public PartyStockTransitionResult DismissDemon(DismissDemonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (!Contains(before.DemonStock, request.DemonInstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.NotOwned, "Demon is not owned.", request.DemonInstanceId);
        }

        RuntimePartyStockSnapshot after = before.With(
            activeParty: before.ActiveParty.Where(actor => actor.InstanceId != request.DemonInstanceId),
            demonStock: before.DemonStock.Where(actor => actor.InstanceId != request.DemonInstanceId));
        return Applied(before, after, request.DemonInstanceId);
    }

    public PartyStockTransitionResult ReplaceDemon(ReplaceDemonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        bool inActive = Contains(before.ActiveParty, request.OldDemonInstanceId);
        bool inStock = Contains(before.DemonStock, request.OldDemonInstanceId);
        if (!inActive && !inStock)
        {
            return Rejected(before, PartyStockTransitionCode.NotOwned, "Demon to replace is not present.", request.OldDemonInstanceId);
        }
        if (Contains(before.ActiveParty, request.NewDemon.InstanceId) || Contains(before.DemonStock, request.NewDemon.InstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.DuplicateOwned, "Replacement demon is already present.", request.NewDemon.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] active = before.ActiveParty
            .Select(actor => actor.InstanceId == request.OldDemonInstanceId ? request.NewDemon : actor)
            .ToArray();
        RuntimeActorReferenceSnapshot[] demonStock = ReplaceOrAppendDemon(before, request);
        if (demonStock.Length > _stockCapacityPolicy.GetCapacity(before.OwnerLevel))
        {
            return Rejected(before, PartyStockTransitionCode.StockFull, "Demon stock is full.", request.NewDemon.InstanceId);
        }

        return Applied(before, before.With(activeParty: active, demonStock: demonStock), request.OldDemonInstanceId, request.NewDemon.InstanceId);
    }

    public PartyStockTransitionResult ConsumeDemon(ConsumeDemonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (!Contains(before.ActiveParty, request.DemonInstanceId) && !Contains(before.DemonStock, request.DemonInstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.NotOwned, "Demon is not present.", request.DemonInstanceId);
        }

        RuntimePartyStockSnapshot after = before.With(
            activeParty: before.ActiveParty.Where(actor => actor.InstanceId != request.DemonInstanceId),
            demonStock: before.DemonStock.Where(actor => actor.InstanceId != request.DemonInstanceId));
        return Applied(before, after, request.DemonInstanceId);
    }

    public PartyStockTransitionResult SwapActivePersona(SwapActivePersonaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        if (before.ActiveForm is null)
        {
            return Rejected(before, PartyStockTransitionCode.ActiveFormMissing, "No active Persona is equipped.", request.PersonaInstanceId);
        }

        int stockIndex = IndexOf(before.PersonaStock, request.PersonaInstanceId);
        if (stockIndex < 0)
        {
            return Rejected(before, PartyStockTransitionCode.StockEntryNotFound, "Persona is not in stock.", request.PersonaInstanceId);
        }

        RuntimeActorReferenceSnapshot[] personaStock = before.PersonaStock.ToArray();
        RuntimeActorReferenceSnapshot newActive = personaStock[stockIndex];
        personaStock[stockIndex] = before.ActiveForm;
        RuntimePartyStockSnapshot after = before.With(
            activeForm: newActive,
            replaceActiveForm: true,
            personaStock: personaStock);
        return Applied(before, after, before.ActiveForm.InstanceId, newActive.InstanceId);
    }

    public PartyStockTransitionResult ConsumePersona(ConsumePersonaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        bool activeMatch = before.ActiveForm?.InstanceId == request.PersonaInstanceId;
        bool stockMatch = Contains(before.PersonaStock, request.PersonaInstanceId);
        if (!activeMatch && !stockMatch)
        {
            return Rejected(before, PartyStockTransitionCode.NotOwned, "Persona is not present.", request.PersonaInstanceId);
        }

        RuntimePartyStockSnapshot after = before.With(
            activeForm: null,
            replaceActiveForm: activeMatch,
            personaStock: before.PersonaStock.Where(persona => persona.InstanceId != request.PersonaInstanceId));
        return Applied(before, after, request.PersonaInstanceId);
    }

    public PartyStockTransitionResult ReplacePersona(ReplacePersonaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyStockSnapshot before = request.Snapshot;
        bool activeMatch = before.ActiveForm?.InstanceId == request.OldPersonaInstanceId;
        int stockIndex = IndexOf(before.PersonaStock, request.OldPersonaInstanceId);
        if (!activeMatch && stockIndex < 0)
        {
            return Rejected(before, PartyStockTransitionCode.NotOwned, "Persona to replace is not present.", request.OldPersonaInstanceId);
        }
        if (before.ActiveForm?.InstanceId == request.NewPersona.InstanceId || Contains(before.PersonaStock, request.NewPersona.InstanceId))
        {
            return Rejected(before, PartyStockTransitionCode.DuplicateOwned, "Replacement Persona is already present.", request.NewPersona.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] personaStock = before.PersonaStock.ToArray();
        if (activeMatch)
        {
            return Applied(
                before,
                before.With(activeForm: request.NewPersona, replaceActiveForm: true),
                request.OldPersonaInstanceId,
                request.NewPersona.InstanceId);
        }

        personaStock[stockIndex] = request.NewPersona;
        return Applied(
            before,
            before.With(personaStock: personaStock),
            request.OldPersonaInstanceId,
            request.NewPersona.InstanceId);
    }

    private RuntimeActorReferenceSnapshot[] ReplaceOrAppendDemon(
        RuntimePartyStockSnapshot before,
        ReplaceDemonRequest request)
    {
        if (Contains(before.DemonStock, request.OldDemonInstanceId))
        {
            return before.DemonStock
                .Select(actor => actor.InstanceId == request.OldDemonInstanceId ? request.NewDemon : actor)
                .ToArray();
        }

        return before.DemonStock.Append(request.NewDemon).ToArray();
    }

    private static PartyStockTransitionResult Applied(
        RuntimePartyStockSnapshot before,
        RuntimePartyStockSnapshot after,
        params RuntimeInstanceId[] affectedInstanceIds) =>
        new(PartyStockTransitionCode.Applied, before, after, affectedInstanceIds);

    private static PartyStockTransitionResult Rejected(
        RuntimePartyStockSnapshot before,
        PartyStockTransitionCode code,
        string message,
        RuntimeInstanceId? subjectInstanceId = null) =>
        new(
            code,
            before,
            before,
            diagnostics: [new PartyStockTransitionDiagnostic(code, message, subjectInstanceId)]);

    private static bool Contains(IEnumerable<RuntimeActorReferenceSnapshot> actors, RuntimeInstanceId instanceId) =>
        actors.Any(actor => actor.InstanceId == instanceId);

    private static RuntimeActorReferenceSnapshot? Find(
        IEnumerable<RuntimeActorReferenceSnapshot> actors,
        RuntimeInstanceId instanceId) =>
        actors.FirstOrDefault(actor => actor.InstanceId == instanceId);

    private static int IndexOf(IReadOnlyList<RuntimeActorReferenceSnapshot> actors, RuntimeInstanceId instanceId)
    {
        for (int index = 0; index < actors.Count; index++)
        {
            if (actors[index].InstanceId == instanceId)
            {
                return index;
            }
        }

        return -1;
    }
}
