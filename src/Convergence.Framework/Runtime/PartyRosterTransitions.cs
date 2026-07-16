using Convergence.Content;

namespace Convergence.Runtime;

public enum PartyRosterTransitionCode
{
    Applied,
    PartyFull,
    RosterFull,
    NotOwned,
    AlreadyActive,
    NotActive,
    ActiveNotFound,
    ReserveNotFound,
    RosterEntryNotFound,
    DuplicateOwned,
    RuntimeInstanceIdInUse,
    ActiveHostedEntityMissing,
    InvalidSlot,
    CapacityExceeded,
    InvalidSnapshot
}

public sealed record PartyRosterTransitionDiagnostic(
    PartyRosterTransitionCode Code,
    string Message,
    RuntimeInstanceId? SubjectInstanceId = null);

public sealed record PartyRosterTransitionResult
{
    public PartyRosterTransitionResult(
        PartyRosterTransitionCode code,
        RuntimePartyRosterSnapshot before,
        RuntimePartyRosterSnapshot after,
        IEnumerable<RuntimeInstanceId>? affectedInstanceIds = null,
        IEnumerable<PartyRosterTransitionDiagnostic>? diagnostics = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        AffectedInstanceIds = RuntimeSnapshotCollections.List(affectedInstanceIds);
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public PartyRosterTransitionCode Code { get; }
    public bool Applied => Code == PartyRosterTransitionCode.Applied;
    public RuntimePartyRosterSnapshot Before { get; }
    public RuntimePartyRosterSnapshot After { get; }
    public IReadOnlyList<RuntimeInstanceId> AffectedInstanceIds { get; }
    public IReadOnlyList<PartyRosterTransitionDiagnostic> Diagnostics { get; }
}

public sealed record RuntimePartyRosterSnapshot
{
    public RuntimePartyRosterSnapshot(
        RuntimeActorReferenceSnapshot owner,
        int ownerLevel,
        IEnumerable<RuntimeActorReferenceSnapshot>? activeParty = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? reserveMembers = null,
        RuntimeActorReferenceSnapshot? activeHostedEntity = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? hostedEntityRoster = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? companionRoster = null,
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
        ActiveHostedEntity = activeHostedEntity;
        HostedEntityRoster = RuntimeSnapshotCollections.List(hostedEntityRoster);
        CompanionRoster = RuntimeSnapshotCollections.List(companionRoster);
        MaxActivePartySize = maxActivePartySize;
    }

    public RuntimeActorReferenceSnapshot Owner { get; }
    public int OwnerLevel { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> ActiveParty { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> ReserveMembers { get; }
    public RuntimeActorReferenceSnapshot? ActiveHostedEntity { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> HostedEntityRoster { get; }
    public IReadOnlyList<RuntimeActorReferenceSnapshot> CompanionRoster { get; }
    public int MaxActivePartySize { get; }

    public RuntimePartyRosterSnapshot With(
        IEnumerable<RuntimeActorReferenceSnapshot>? activeParty = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? reserveMembers = null,
        RuntimeActorReferenceSnapshot? activeHostedEntity = null,
        bool replaceActiveHostedEntity = false,
        IEnumerable<RuntimeActorReferenceSnapshot>? hostedEntityRoster = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? companionRoster = null) =>
        new(
            Owner,
            OwnerLevel,
            activeParty ?? ActiveParty,
            reserveMembers ?? ReserveMembers,
            replaceActiveHostedEntity ? activeHostedEntity : ActiveHostedEntity,
            hostedEntityRoster ?? HostedEntityRoster,
            companionRoster ?? CompanionRoster,
            MaxActivePartySize);
}

public enum RuntimeRosterKind
{
    HostedEntity,
    Companion
}

public interface IRosterCapacityPolicy
{
    int GetCapacity(RuntimeRosterKind rosterKind, int ownerLevel);
}

public sealed class NoLimitRosterCapacityPolicy : IRosterCapacityPolicy
{
    public static NoLimitRosterCapacityPolicy Instance { get; } = new();

    private NoLimitRosterCapacityPolicy()
    {
    }

    public int GetCapacity(RuntimeRosterKind rosterKind, int ownerLevel)
    {
        if (!Enum.IsDefined(rosterKind))
        {
            throw new ArgumentOutOfRangeException(nameof(rosterKind), "Roster kind is not supported.");
        }
        if (ownerLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerLevel), "Owner level must be positive.");
        }

        return int.MaxValue;
    }
}

public sealed record RosterCapacityTier
{
    public RosterCapacityTier(RuntimeRosterKind rosterKind, int minimumLevel, int capacity)
    {
        if (!Enum.IsDefined(rosterKind))
        {
            throw new ArgumentOutOfRangeException(nameof(rosterKind), "Roster kind is not supported.");
        }
        if (minimumLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLevel), "Minimum level must be positive.");
        }
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative.");
        }

        RosterKind = rosterKind;
        MinimumLevel = minimumLevel;
        Capacity = capacity;
    }

    public RuntimeRosterKind RosterKind { get; }
    public int MinimumLevel { get; }
    public int Capacity { get; }
}

public sealed class TieredRosterCapacityPolicy : IRosterCapacityPolicy
{
    private readonly IReadOnlyList<RosterCapacityTier> _tiers;

    public TieredRosterCapacityPolicy(IEnumerable<RosterCapacityTier> tiers)
    {
        RosterCapacityTier[] copy = (tiers ?? throw new ArgumentNullException(nameof(tiers)))
            .OrderBy(tier => tier.RosterKind)
            .ThenBy(tier => tier.MinimumLevel)
            .ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("At least one roster-capacity tier is required.", nameof(tiers));
        }
        if (copy.GroupBy(tier => tier.RosterKind).Any(group => group.First().MinimumLevel != 1))
        {
            throw new ArgumentException("Each represented roster kind must define level 1.", nameof(tiers));
        }
        if (copy.GroupBy(tier => new { tier.RosterKind, tier.MinimumLevel }).Any(group => group.Count() != 1))
        {
            throw new ArgumentException("Roster-capacity tier minimum levels must be unique within each roster kind.", nameof(tiers));
        }

        _tiers = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<RosterCapacityTier> Tiers => _tiers;

    public int GetCapacity(RuntimeRosterKind rosterKind, int ownerLevel)
    {
        if (!Enum.IsDefined(rosterKind))
        {
            throw new ArgumentOutOfRangeException(nameof(rosterKind), "Roster kind is not supported.");
        }
        if (ownerLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerLevel), "Owner level must be positive.");
        }

        return _tiers
            .Where(tier => tier.RosterKind == rosterKind && tier.MinimumLevel <= ownerLevel)
            .Select(tier => (int?)tier.Capacity)
            .LastOrDefault() ?? 0;
    }
}

public sealed record AddPartyMemberRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeActorReferenceSnapshot Member);
public sealed record SwapPartyMemberRequest(RuntimePartyRosterSnapshot Snapshot, int ActiveIndex, int ReserveIndex);
public sealed record AddCompanionToRosterRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeActorReferenceSnapshot Companion);
public sealed record AddHostedEntityToRosterRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeActorReferenceSnapshot HostedEntity);
public sealed record DeployCompanionRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId CompanionInstanceId);
public sealed record SwapDeployedCompanionRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId ActiveCompanionInstanceId, RuntimeInstanceId StandbyCompanionInstanceId);
public sealed record RecallCompanionRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId CompanionInstanceId);
public sealed record DismissCompanionRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId CompanionInstanceId);
public sealed record ReplaceCompanionRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId OldCompanionInstanceId, RuntimeActorReferenceSnapshot NewCompanion);
public sealed record ConsumeCompanionRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId CompanionInstanceId);
public sealed record SelectActiveHostedEntityRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId HostedEntityInstanceId);
public sealed record ClearActiveHostedEntityRequest(RuntimePartyRosterSnapshot Snapshot);
public sealed record ConsumeHostedEntityRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId HostedEntityInstanceId);
public sealed record ReplaceHostedEntityRequest(RuntimePartyRosterSnapshot Snapshot, RuntimeInstanceId OldHostedEntityInstanceId, RuntimeActorReferenceSnapshot NewHostedEntity);

public interface IPartyRosterTransitionService
{
    PartyRosterTransitionResult AddPartyMember(AddPartyMemberRequest request);
    PartyRosterTransitionResult SwapPartyMember(SwapPartyMemberRequest request);
    PartyRosterTransitionResult AddCompanionToRoster(AddCompanionToRosterRequest request);
    PartyRosterTransitionResult AddHostedEntityToRoster(AddHostedEntityToRosterRequest request);
    PartyRosterTransitionResult DeployCompanion(DeployCompanionRequest request);
    PartyRosterTransitionResult SwapDeployedCompanion(SwapDeployedCompanionRequest request);
    PartyRosterTransitionResult RecallCompanion(RecallCompanionRequest request);
    PartyRosterTransitionResult DismissCompanion(DismissCompanionRequest request);
    PartyRosterTransitionResult ReplaceCompanion(ReplaceCompanionRequest request);
    PartyRosterTransitionResult ConsumeCompanion(ConsumeCompanionRequest request);
    PartyRosterTransitionResult SelectActiveHostedEntity(SelectActiveHostedEntityRequest request);
    PartyRosterTransitionResult ClearActiveHostedEntity(ClearActiveHostedEntityRequest request);
    PartyRosterTransitionResult ConsumeHostedEntity(ConsumeHostedEntityRequest request);
    PartyRosterTransitionResult ReplaceHostedEntity(ReplaceHostedEntityRequest request);
}

public sealed class PartyRosterTransitionService : IPartyRosterTransitionService
{
    private readonly IRosterCapacityPolicy _rosterCapacityPolicy;

    public PartyRosterTransitionService(IRosterCapacityPolicy? rosterCapacityPolicy = null)
    {
        _rosterCapacityPolicy = rosterCapacityPolicy ?? NoLimitRosterCapacityPolicy.Instance;
    }

    public PartyRosterTransitionResult AddPartyMember(AddPartyMemberRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (Contains(before.ActiveParty, request.Member.InstanceId) || Contains(before.ReserveMembers, request.Member.InstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.DuplicateOwned, "Party member is already present.", request.Member.InstanceId);
        }

        bool hasOpenActiveSlot = before.ActiveParty.Count < before.MaxActivePartySize;
        bool isOwnerEnteringActiveParty = hasOpenActiveSlot && before.Owner == request.Member;
        if (!isOwnerEnteringActiveParty &&
            RuntimePartyRosterIdentityRules.ContainsInstanceId(before, request.Member.InstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.RuntimeInstanceIdInUse,
                "Party member runtime instance ID is already used by another party or roster reference.",
                request.Member.InstanceId);
        }

        if (hasOpenActiveSlot)
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

    public PartyRosterTransitionResult SwapPartyMember(SwapPartyMemberRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (request.ActiveIndex < 0 || request.ActiveIndex >= before.ActiveParty.Count)
        {
            return Rejected(before, PartyRosterTransitionCode.ActiveNotFound, "Active party index is out of range.");
        }
        if (request.ReserveIndex < 0 || request.ReserveIndex >= before.ReserveMembers.Count)
        {
            return Rejected(before, PartyRosterTransitionCode.ReserveNotFound, "Reserve party index is out of range.");
        }

        RuntimeActorReferenceSnapshot[] active = before.ActiveParty.ToArray();
        RuntimeActorReferenceSnapshot[] reserve = before.ReserveMembers.ToArray();
        RuntimeActorReferenceSnapshot oldActive = active[request.ActiveIndex];
        RuntimeActorReferenceSnapshot oldReserve = reserve[request.ReserveIndex];
        active[request.ActiveIndex] = oldReserve;
        reserve[request.ReserveIndex] = oldActive;
        return Applied(before, before.With(activeParty: active, reserveMembers: reserve), oldActive.InstanceId, oldReserve.InstanceId);
    }

    public PartyRosterTransitionResult AddCompanionToRoster(AddCompanionToRosterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (Contains(before.ActiveParty, request.Companion.InstanceId) || Contains(before.CompanionRoster, request.Companion.InstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.DuplicateOwned, "Companion is already present.", request.Companion.InstanceId);
        }
        if (RuntimePartyRosterIdentityRules.ContainsInstanceId(before, request.Companion.InstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.RuntimeInstanceIdInUse,
                "Companion runtime instance ID is already used by another party or roster reference.",
                request.Companion.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] companionRoster = before.CompanionRoster.Append(request.Companion).ToArray();
        if (companionRoster.Length > _rosterCapacityPolicy.GetCapacity(RuntimeRosterKind.Companion, before.OwnerLevel))
        {
            return Rejected(before, PartyRosterTransitionCode.RosterFull, "Companion roster is full.", request.Companion.InstanceId);
        }

        return Applied(before, before.With(companionRoster: companionRoster), request.Companion.InstanceId);
    }

    public PartyRosterTransitionResult AddHostedEntityToRoster(AddHostedEntityToRosterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (Contains(before.HostedEntityRoster, request.HostedEntity.InstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.DuplicateOwned,
                "HostedEntity is already present.",
                request.HostedEntity.InstanceId);
        }
        if (RuntimePartyRosterIdentityRules.ContainsInstanceId(before, request.HostedEntity.InstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.RuntimeInstanceIdInUse,
                "HostedEntity runtime instance ID is already used by another party or roster reference.",
                request.HostedEntity.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] hostedEntityRoster = before.HostedEntityRoster.Append(request.HostedEntity).ToArray();
        if (hostedEntityRoster.Length > _rosterCapacityPolicy.GetCapacity(RuntimeRosterKind.HostedEntity, before.OwnerLevel))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.RosterFull,
                "HostedEntity roster is full.",
                request.HostedEntity.InstanceId);
        }

        return Applied(before, before.With(hostedEntityRoster: hostedEntityRoster), request.HostedEntity.InstanceId);
    }

    public PartyRosterTransitionResult DeployCompanion(DeployCompanionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        RuntimeActorReferenceSnapshot? companion = Find(before.CompanionRoster, request.CompanionInstanceId);
        if (companion is null)
        {
            return Rejected(before, PartyRosterTransitionCode.NotOwned, "Companion is not owned.", request.CompanionInstanceId);
        }
        if (Contains(before.ActiveParty, request.CompanionInstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.AlreadyActive, "Companion is already active.", request.CompanionInstanceId);
        }
        if (before.ActiveParty.Count >= before.MaxActivePartySize)
        {
            return Rejected(before, PartyRosterTransitionCode.PartyFull, "Active party is full.", request.CompanionInstanceId);
        }

        return Applied(before, before.With(activeParty: before.ActiveParty.Append(companion)), request.CompanionInstanceId);
    }

    public PartyRosterTransitionResult SwapDeployedCompanion(SwapDeployedCompanionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (!Contains(before.CompanionRoster, request.ActiveCompanionInstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.NotOwned,
                "Active companion is not owned in Companion roster.",
                request.ActiveCompanionInstanceId);
        }

        int activeIndex = IndexOf(before.ActiveParty, request.ActiveCompanionInstanceId);
        if (activeIndex < 0)
        {
            return Rejected(before, PartyRosterTransitionCode.NotActive, "Active companion is not in the party.", request.ActiveCompanionInstanceId);
        }

        RuntimeActorReferenceSnapshot? standby = Find(before.CompanionRoster, request.StandbyCompanionInstanceId);
        if (standby is null)
        {
            return Rejected(before, PartyRosterTransitionCode.NotOwned, "Standby companion is not owned.", request.StandbyCompanionInstanceId);
        }
        if (Contains(before.ActiveParty, request.StandbyCompanionInstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.AlreadyActive, "Standby companion is already active.", request.StandbyCompanionInstanceId);
        }

        RuntimeActorReferenceSnapshot[] active = before.ActiveParty.ToArray();
        active[activeIndex] = standby;
        return Applied(before, before.With(activeParty: active), request.ActiveCompanionInstanceId, request.StandbyCompanionInstanceId);
    }

    public PartyRosterTransitionResult RecallCompanion(RecallCompanionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (!Contains(before.CompanionRoster, request.CompanionInstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.NotOwned,
                "Companion is not owned in Companion roster.",
                request.CompanionInstanceId);
        }

        if (!Contains(before.ActiveParty, request.CompanionInstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.NotActive, "Companion is not active.", request.CompanionInstanceId);
        }

        return Applied(
            before,
            before.With(activeParty: before.ActiveParty.Where(actor => actor.InstanceId != request.CompanionInstanceId)),
            request.CompanionInstanceId);
    }

    public PartyRosterTransitionResult DismissCompanion(DismissCompanionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (!Contains(before.CompanionRoster, request.CompanionInstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.NotOwned, "Companion is not owned.", request.CompanionInstanceId);
        }

        RuntimePartyRosterSnapshot after = before.With(
            activeParty: before.ActiveParty.Where(actor => actor.InstanceId != request.CompanionInstanceId),
            companionRoster: before.CompanionRoster.Where(actor => actor.InstanceId != request.CompanionInstanceId));
        return Applied(before, after, request.CompanionInstanceId);
    }

    public PartyRosterTransitionResult ReplaceCompanion(ReplaceCompanionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (!Contains(before.CompanionRoster, request.OldCompanionInstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.NotOwned,
                "Companion to replace is not owned in Companion roster.",
                request.OldCompanionInstanceId);
        }

        if (Contains(before.ActiveParty, request.NewCompanion.InstanceId) || Contains(before.CompanionRoster, request.NewCompanion.InstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.DuplicateOwned, "Replacement companion is already present.", request.NewCompanion.InstanceId);
        }
        if (RuntimePartyRosterIdentityRules.ContainsInstanceId(before, request.NewCompanion.InstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.RuntimeInstanceIdInUse,
                "Replacement companion runtime instance ID is already used by another party or roster reference.",
                request.NewCompanion.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] active = before.ActiveParty
            .Select(actor => actor.InstanceId == request.OldCompanionInstanceId ? request.NewCompanion : actor)
            .ToArray();
        RuntimeActorReferenceSnapshot[] companionRoster = before.CompanionRoster
            .Select(actor => actor.InstanceId == request.OldCompanionInstanceId ? request.NewCompanion : actor)
            .ToArray();
        if (companionRoster.Length > _rosterCapacityPolicy.GetCapacity(RuntimeRosterKind.Companion, before.OwnerLevel))
        {
            return Rejected(before, PartyRosterTransitionCode.RosterFull, "Companion roster is full.", request.NewCompanion.InstanceId);
        }

        return Applied(before, before.With(activeParty: active, companionRoster: companionRoster), request.OldCompanionInstanceId, request.NewCompanion.InstanceId);
    }

    public PartyRosterTransitionResult ConsumeCompanion(ConsumeCompanionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (!Contains(before.CompanionRoster, request.CompanionInstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.NotOwned,
                "Companion is not owned in Companion roster.",
                request.CompanionInstanceId);
        }

        RuntimePartyRosterSnapshot after = before.With(
            activeParty: before.ActiveParty.Where(actor => actor.InstanceId != request.CompanionInstanceId),
            companionRoster: before.CompanionRoster.Where(actor => actor.InstanceId != request.CompanionInstanceId));
        return Applied(before, after, request.CompanionInstanceId);
    }

    public PartyRosterTransitionResult SelectActiveHostedEntity(SelectActiveHostedEntityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        RuntimeActorReferenceSnapshot? selected = Find(
            before.HostedEntityRoster,
            request.HostedEntityInstanceId);
        if (selected is null)
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.RosterEntryNotFound,
                "Hosted Entity is not owned in its roster.",
                request.HostedEntityInstanceId);
        }

        if (before.ActiveHostedEntity?.InstanceId == request.HostedEntityInstanceId)
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.AlreadyActive,
                "Hosted Entity is already active.",
                request.HostedEntityInstanceId);
        }

        RuntimePartyRosterSnapshot after = before.With(
            activeHostedEntity: selected,
            replaceActiveHostedEntity: true);
        return Applied(before, after, selected.InstanceId);
    }

    public PartyRosterTransitionResult ClearActiveHostedEntity(ClearActiveHostedEntityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        if (before.ActiveHostedEntity is null)
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.ActiveHostedEntityMissing,
                "No active Hosted Entity is selected.");
        }

        RuntimeInstanceId cleared = before.ActiveHostedEntity.InstanceId;
        return Applied(
            before,
            before.With(activeHostedEntity: null, replaceActiveHostedEntity: true),
            cleared);
    }

    public PartyRosterTransitionResult ConsumeHostedEntity(ConsumeHostedEntityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        bool activeMatch = before.ActiveHostedEntity?.InstanceId == request.HostedEntityInstanceId;
        if (!Contains(before.HostedEntityRoster, request.HostedEntityInstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.NotOwned, "Hosted Entity is not owned.", request.HostedEntityInstanceId);
        }

        RuntimePartyRosterSnapshot after = before.With(
            activeHostedEntity: null,
            replaceActiveHostedEntity: activeMatch,
            hostedEntityRoster: before.HostedEntityRoster.Where(hostedEntity => hostedEntity.InstanceId != request.HostedEntityInstanceId));
        return Applied(before, after, request.HostedEntityInstanceId);
    }

    public PartyRosterTransitionResult ReplaceHostedEntity(ReplaceHostedEntityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimePartyRosterSnapshot before = request.Snapshot;
        if (RejectInvalid(before) is { } invalid) return invalid;
        bool activeMatch = before.ActiveHostedEntity?.InstanceId == request.OldHostedEntityInstanceId;
        int rosterIndex = IndexOf(before.HostedEntityRoster, request.OldHostedEntityInstanceId);
        if (rosterIndex < 0)
        {
            return Rejected(before, PartyRosterTransitionCode.NotOwned, "Hosted Entity to replace is not owned.", request.OldHostedEntityInstanceId);
        }
        if (Contains(before.HostedEntityRoster, request.NewHostedEntity.InstanceId))
        {
            return Rejected(before, PartyRosterTransitionCode.DuplicateOwned, "Replacement HostedEntity is already present.", request.NewHostedEntity.InstanceId);
        }
        if (RuntimePartyRosterIdentityRules.ContainsInstanceId(before, request.NewHostedEntity.InstanceId))
        {
            return Rejected(
                before,
                PartyRosterTransitionCode.RuntimeInstanceIdInUse,
                "Replacement HostedEntity runtime instance ID is already used by another party or roster reference.",
                request.NewHostedEntity.InstanceId);
        }

        RuntimeActorReferenceSnapshot[] hostedEntityRoster = before.HostedEntityRoster.ToArray();
        hostedEntityRoster[rosterIndex] = request.NewHostedEntity;
        return Applied(
            before,
            before.With(
                activeHostedEntity: activeMatch ? request.NewHostedEntity : null,
                replaceActiveHostedEntity: activeMatch,
                hostedEntityRoster: hostedEntityRoster),
            request.OldHostedEntityInstanceId,
            request.NewHostedEntity.InstanceId);
    }

    private static PartyRosterTransitionResult Applied(
        RuntimePartyRosterSnapshot before,
        RuntimePartyRosterSnapshot after,
        params RuntimeInstanceId[] affectedInstanceIds) =>
        new(PartyRosterTransitionCode.Applied, before, after, affectedInstanceIds);

    private static PartyRosterTransitionResult Rejected(
        RuntimePartyRosterSnapshot before,
        PartyRosterTransitionCode code,
        string message,
        RuntimeInstanceId? subjectInstanceId = null) =>
        new(
            code,
            before,
            before,
            diagnostics: [new PartyRosterTransitionDiagnostic(code, message, subjectInstanceId)]);

    private static PartyRosterTransitionResult? RejectInvalid(RuntimePartyRosterSnapshot snapshot)
    {
        RuntimePartyRosterInvariantDiagnostic? first =
            RuntimePartyRosterInvariantRules.Validate(snapshot).FirstOrDefault();
        return first is null
            ? null
            : Rejected(
                snapshot,
                PartyRosterTransitionCode.InvalidSnapshot,
                $"Party roster is invalid at '{first.Path}': {first.Message}",
                first.InstanceId);
    }

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
