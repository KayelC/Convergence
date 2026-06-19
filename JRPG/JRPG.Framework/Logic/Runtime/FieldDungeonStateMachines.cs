using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;

namespace JRPGPrototype.Logic.Runtime;

public enum RuntimeDungeonFloorKind
{
    Empty,
    Battle,
    Boss,
    SafeRoom,
    BlockEnd
}

public enum RuntimeFieldActionKind
{
    AscendStairs,
    DescendStairs,
    Clock,
    Terminal,
    ReturnToCity,
    Inventory,
    Status,
    OrganizeParty,
    Barrier
}

public enum RuntimeDungeonTransitionCode
{
    Applied,
    Rejected,
    BarrierBlocked,
    InvalidFloor,
    MissingDungeon
}

public enum RuntimeDungeonEventKind
{
    DungeonEntered,
    DungeonExited,
    Movement,
    Warped,
    FloorEntered,
    TerminalUnlocked,
    SafeRoom,
    EncounterRequested,
    BossRequested,
    BarrierBlocked,
    BossDefeated,
    GameOverRecovered,
    HostActionRequested,
    ActionRejected
}

public sealed record RuntimeDungeonEvent
{
    public RuntimeDungeonEvent(
        RuntimeDungeonEventKind kind,
        int? floor = null,
        ContentId? contentId = null,
        string? message = null,
        IEnumerable<ContentId>? enemyIds = null)
    {
        Kind = kind;
        Floor = floor;
        ContentId = contentId;
        Message = message;
        EnemyIds = RuntimeSnapshotCollections.List(enemyIds);
    }

    public RuntimeDungeonEventKind Kind { get; }
    public int? Floor { get; }
    public ContentId? ContentId { get; }
    public string? Message { get; }
    public IReadOnlyList<ContentId> EnemyIds { get; }
}

public sealed record RuntimeFieldActionOption(
    RuntimeFieldActionKind Kind,
    string Label,
    bool IsEnabled = true);

public sealed record RuntimeDungeonProgressSnapshot
{
    public RuntimeDungeonProgressSnapshot(
        ContentId dungeonId,
        int currentFloor = 1,
        int maxFloorReached = 1,
        IEnumerable<int>? unlockedTerminals = null,
        IEnumerable<ContentId>? defeatedBossIds = null)
    {
        if (currentFloor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentFloor), "Current floor must be positive.");
        }
        if (maxFloorReached <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFloorReached), "Max floor reached must be positive.");
        }

        DungeonId = dungeonId;
        CurrentFloor = currentFloor;
        MaxFloorReached = maxFloorReached;
        UnlockedTerminals = RuntimeSnapshotCollections.List(
            (unlockedTerminals ?? [1]).Append(1).Where(floor => floor > 0).Distinct().OrderBy(floor => floor));
        DefeatedBossIds = RuntimeSnapshotCollections.List((defeatedBossIds ?? []).Distinct());
    }

    public ContentId DungeonId { get; }
    public int CurrentFloor { get; }
    public int MaxFloorReached { get; }
    public IReadOnlyList<int> UnlockedTerminals { get; }
    public IReadOnlyList<ContentId> DefeatedBossIds { get; }

    public RuntimeDungeonProgressSnapshot With(
        int? currentFloor = null,
        int? maxFloorReached = null,
        IEnumerable<int>? unlockedTerminals = null,
        IEnumerable<ContentId>? defeatedBossIds = null) =>
        new(
            DungeonId,
            currentFloor ?? CurrentFloor,
            maxFloorReached ?? MaxFloorReached,
            unlockedTerminals ?? UnlockedTerminals,
            defeatedBossIds ?? DefeatedBossIds);

    public RuntimeDungeonProgressSnapshot UnlockTerminal(int floor)
    {
        if (floor <= 0)
        {
            return this;
        }

        return With(unlockedTerminals: UnlockedTerminals.Append(floor));
    }

    public RuntimeDungeonProgressSnapshot MarkBossDefeated(ContentId bossId) =>
        With(defeatedBossIds: DefeatedBossIds.Append(bossId));

    public bool IsBossDefeated(ContentId bossId) => DefeatedBossIds.Contains(bossId);
}

public sealed record RuntimeFieldSnapshot
{
    public RuntimeFieldSnapshot(
        RuntimeNavigationSnapshot navigation,
        RuntimeDungeonTraversalSnapshot? dungeonTraversal = null)
    {
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        DungeonTraversal = dungeonTraversal;
    }

    public RuntimeNavigationSnapshot Navigation { get; }
    public RuntimeDungeonTraversalSnapshot? DungeonTraversal { get; }
}

public sealed record RuntimeDungeonFixedFloorSnapshot
{
    public RuntimeDungeonFixedFloorSnapshot(
        int floor,
        RuntimeDungeonFloorKind kind,
        ContentId? eventId = null,
        bool hasTerminal = false,
        string? description = null)
    {
        if (floor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(floor), "Fixed floor must be positive.");
        }

        Floor = floor;
        Kind = kind;
        EventId = eventId;
        HasTerminal = hasTerminal;
        Description = description ?? string.Empty;
    }

    public int Floor { get; }
    public RuntimeDungeonFloorKind Kind { get; }
    public ContentId? EventId { get; }
    public bool HasTerminal { get; }
    public string Description { get; }
}

public sealed record RuntimeDungeonBlockSnapshot
{
    public RuntimeDungeonBlockSnapshot(
        ContentId id,
        string displayName,
        int startFloor,
        int endFloor,
        IEnumerable<ContentId>? enemyPoolIds = null,
        IEnumerable<RuntimeDungeonFixedFloorSnapshot>? fixedFloors = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Block display name is required.", nameof(displayName));
        }
        if (startFloor <= 0 || endFloor <= 0 || startFloor > endFloor)
        {
            throw new ArgumentOutOfRangeException(nameof(startFloor), "Block floors must be positive and ordered.");
        }

        Id = id;
        DisplayName = displayName;
        StartFloor = startFloor;
        EndFloor = endFloor;
        EnemyPoolIds = RuntimeSnapshotCollections.List(enemyPoolIds);
        FixedFloors = RuntimeSnapshotCollections.List(fixedFloors);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public int StartFloor { get; }
    public int EndFloor { get; }
    public IReadOnlyList<ContentId> EnemyPoolIds { get; }
    public IReadOnlyList<RuntimeDungeonFixedFloorSnapshot> FixedFloors { get; }

    public bool ContainsFloor(int floor) => floor >= StartFloor && floor <= EndFloor;
}

public sealed record RuntimeDungeonContentSnapshot
{
    public RuntimeDungeonContentSnapshot(
        ContentId id,
        string displayName,
        IEnumerable<RuntimeDungeonBlockSnapshot>? blocks = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Dungeon display name is required.", nameof(displayName));
        }

        Id = id;
        DisplayName = displayName;
        Blocks = RuntimeSnapshotCollections.List(blocks);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public IReadOnlyList<RuntimeDungeonBlockSnapshot> Blocks { get; }
}

public sealed record RuntimeDungeonFloorSnapshot
{
    public RuntimeDungeonFloorSnapshot(
        int floorNumber,
        string blockName,
        RuntimeDungeonFloorKind kind,
        string description,
        bool hasTerminal = false,
        IEnumerable<ContentId>? enemyIds = null)
    {
        if (floorNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(floorNumber), "Floor number must be positive.");
        }

        FloorNumber = floorNumber;
        BlockName = string.IsNullOrWhiteSpace(blockName) ? "Unknown Block" : blockName;
        Kind = kind;
        Description = description ?? string.Empty;
        HasTerminal = hasTerminal;
        EnemyIds = RuntimeSnapshotCollections.List(enemyIds);
    }

    public int FloorNumber { get; }
    public string BlockName { get; }
    public RuntimeDungeonFloorKind Kind { get; }
    public string Description { get; }
    public bool HasTerminal { get; }
    public IReadOnlyList<ContentId> EnemyIds { get; }
}

public sealed record RuntimeDungeonTransitionResult
{
    public RuntimeDungeonTransitionResult(
        RuntimeDungeonTransitionCode code,
        RuntimeDungeonProgressSnapshot before,
        RuntimeDungeonProgressSnapshot after,
        RuntimeDungeonFloorSnapshot? floor = null,
        IEnumerable<RuntimeDungeonEvent>? events = null,
        string? message = null)
    {
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Floor = floor;
        Events = RuntimeSnapshotCollections.List(events);
        Message = message;
    }

    public RuntimeDungeonTransitionCode Code { get; }
    public bool Applied => Code == RuntimeDungeonTransitionCode.Applied;
    public RuntimeDungeonProgressSnapshot Before { get; }
    public RuntimeDungeonProgressSnapshot After { get; }
    public RuntimeDungeonFloorSnapshot? Floor { get; }
    public IReadOnlyList<RuntimeDungeonEvent> Events { get; }
    public string? Message { get; }
}

public interface IRuntimeFieldDungeonService
{
    RuntimeDungeonTransitionResult EnterDungeon(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress);
    RuntimeDungeonTransitionResult ProcessCurrentFloor(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress);
    RuntimeDungeonTransitionResult Ascend(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress);
    RuntimeDungeonTransitionResult Descend(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress);
    RuntimeDungeonTransitionResult Warp(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress, int destinationFloor);
    RuntimeDungeonTransitionResult ReturnToCity(RuntimeDungeonProgressSnapshot progress);
    RuntimeDungeonTransitionResult RequestDungeonExit(RuntimeDungeonProgressSnapshot progress);
    RuntimeDungeonTransitionResult InteractBarrier(RuntimeDungeonProgressSnapshot progress);
    RuntimeDungeonTransitionResult RegisterBossDefeat(RuntimeDungeonProgressSnapshot progress, ContentId? bossId);
    RuntimeDungeonTransitionResult RecoverFromGameOver(RuntimeDungeonProgressSnapshot progress);
    IReadOnlyList<RuntimeFieldActionOption> GetDungeonActionOptions(RuntimeDungeonFloorSnapshot floor, bool canOrganizeParty);
}

public sealed class RuntimeFieldDungeonService : IRuntimeFieldDungeonService
{
    private readonly IRandomSource _random;

    public RuntimeFieldDungeonService(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public RuntimeDungeonTransitionResult EnterDungeon(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress)
    {
        RuntimeDungeonProgressSnapshot after = progress.With(currentFloor: 1);
        RuntimeDungeonTransitionResult processed = ProcessCurrentFloor(content, after);
        return new RuntimeDungeonTransitionResult(
            processed.Code,
            progress,
            processed.After,
            processed.Floor,
            new[] { new RuntimeDungeonEvent(RuntimeDungeonEventKind.DungeonEntered, floor: 1) }.Concat(processed.Events));
    }

    public RuntimeDungeonTransitionResult ProcessCurrentFloor(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress)
    {
        RuntimeDungeonProgressSnapshot after = progress;
        RuntimeDungeonFloorSnapshot floor = EvaluateFloor(content, progress);
        var events = new List<RuntimeDungeonEvent>
        {
            new(RuntimeDungeonEventKind.FloorEntered, floor: floor.FloorNumber)
        };

        if (floor.HasTerminal)
        {
            bool wasUnlocked = after.UnlockedTerminals.Contains(floor.FloorNumber);
            after = after.UnlockTerminal(floor.FloorNumber);
            if (!wasUnlocked)
            {
                events.Add(new RuntimeDungeonEvent(RuntimeDungeonEventKind.TerminalUnlocked, floor: floor.FloorNumber));
            }
        }

        switch (floor.Kind)
        {
            case RuntimeDungeonFloorKind.SafeRoom:
                events.Add(new RuntimeDungeonEvent(RuntimeDungeonEventKind.SafeRoom, floor: floor.FloorNumber));
                break;
            case RuntimeDungeonFloorKind.Battle:
                events.Add(new RuntimeDungeonEvent(RuntimeDungeonEventKind.EncounterRequested, floor: floor.FloorNumber, enemyIds: floor.EnemyIds));
                break;
            case RuntimeDungeonFloorKind.Boss:
                events.Add(new RuntimeDungeonEvent(RuntimeDungeonEventKind.BossRequested, floor: floor.FloorNumber, enemyIds: floor.EnemyIds));
                break;
            case RuntimeDungeonFloorKind.BlockEnd:
                events.Add(new RuntimeDungeonEvent(RuntimeDungeonEventKind.BarrierBlocked, floor: floor.FloorNumber));
                break;
        }

        return new RuntimeDungeonTransitionResult(RuntimeDungeonTransitionCode.Applied, progress, after, floor, events);
    }

    public RuntimeDungeonTransitionResult Ascend(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress)
    {
        RuntimeDungeonFloorSnapshot current = EvaluateFloor(content, progress);
        if (current.Kind == RuntimeDungeonFloorKind.BlockEnd)
        {
            return new RuntimeDungeonTransitionResult(
                RuntimeDungeonTransitionCode.BarrierBlocked,
                progress,
                progress,
                current,
                [new RuntimeDungeonEvent(RuntimeDungeonEventKind.BarrierBlocked, floor: progress.CurrentFloor)],
                "Barrier blocks forward progress.");
        }

        RuntimeDungeonProgressSnapshot moved = progress.With(
            currentFloor: progress.CurrentFloor + 1,
            maxFloorReached: Math.Max(progress.MaxFloorReached, progress.CurrentFloor + 1));
        RuntimeDungeonTransitionResult processed = ProcessCurrentFloor(content, moved);
        return new RuntimeDungeonTransitionResult(
            processed.Code,
            progress,
            processed.After,
            processed.Floor,
            new[] { new RuntimeDungeonEvent(RuntimeDungeonEventKind.Movement, floor: moved.CurrentFloor, message: "ascend") }.Concat(processed.Events));
    }

    public RuntimeDungeonTransitionResult Descend(RuntimeDungeonContentSnapshot content, RuntimeDungeonProgressSnapshot progress)
    {
        int destination = progress.CurrentFloor > 1 ? progress.CurrentFloor - 1 : 1;
        RuntimeDungeonProgressSnapshot moved = progress.With(currentFloor: destination);
        RuntimeDungeonTransitionResult processed = ProcessCurrentFloor(content, moved);
        return new RuntimeDungeonTransitionResult(
            processed.Code,
            progress,
            processed.After,
            processed.Floor,
            new[] { new RuntimeDungeonEvent(RuntimeDungeonEventKind.Movement, floor: destination, message: "descend") }.Concat(processed.Events));
    }

    public RuntimeDungeonTransitionResult Warp(
        RuntimeDungeonContentSnapshot content,
        RuntimeDungeonProgressSnapshot progress,
        int destinationFloor)
    {
        if (destinationFloor <= 0 || !progress.UnlockedTerminals.Contains(destinationFloor))
        {
            return new RuntimeDungeonTransitionResult(
                RuntimeDungeonTransitionCode.InvalidFloor,
                progress,
                progress,
                EvaluateFloor(content, progress),
                [new RuntimeDungeonEvent(RuntimeDungeonEventKind.ActionRejected, floor: destinationFloor)],
                "Destination floor is not an unlocked terminal.");
        }

        RuntimeDungeonProgressSnapshot moved = progress.With(currentFloor: destinationFloor);
        RuntimeDungeonTransitionResult processed = ProcessCurrentFloor(content, moved);
        return new RuntimeDungeonTransitionResult(
            processed.Code,
            progress,
            processed.After,
            processed.Floor,
            new[] { new RuntimeDungeonEvent(RuntimeDungeonEventKind.Warped, floor: destinationFloor) }.Concat(processed.Events));
    }

    public RuntimeDungeonTransitionResult ReturnToCity(RuntimeDungeonProgressSnapshot progress) =>
        ResetToEntry(progress, RuntimeDungeonEventKind.DungeonExited);

    public RuntimeDungeonTransitionResult RequestDungeonExit(RuntimeDungeonProgressSnapshot progress) =>
        ResetToEntry(progress, RuntimeDungeonEventKind.DungeonExited);

    public RuntimeDungeonTransitionResult InteractBarrier(RuntimeDungeonProgressSnapshot progress) =>
        new(
            RuntimeDungeonTransitionCode.BarrierBlocked,
            progress,
            progress,
            events: [new RuntimeDungeonEvent(RuntimeDungeonEventKind.BarrierBlocked, floor: progress.CurrentFloor)],
            message: "Barrier blocks forward progress.");

    public RuntimeDungeonTransitionResult RegisterBossDefeat(RuntimeDungeonProgressSnapshot progress, ContentId? bossId)
    {
        if (bossId is null)
        {
            return new RuntimeDungeonTransitionResult(
                RuntimeDungeonTransitionCode.Rejected,
                progress,
                progress,
                events: [new RuntimeDungeonEvent(RuntimeDungeonEventKind.ActionRejected, floor: progress.CurrentFloor)]);
        }

        RuntimeDungeonProgressSnapshot after = progress.MarkBossDefeated(bossId.Value);
        return new RuntimeDungeonTransitionResult(
            RuntimeDungeonTransitionCode.Applied,
            progress,
            after,
            events: [new RuntimeDungeonEvent(RuntimeDungeonEventKind.BossDefeated, floor: progress.CurrentFloor, contentId: bossId)]);
    }

    public RuntimeDungeonTransitionResult RecoverFromGameOver(RuntimeDungeonProgressSnapshot progress) =>
        ResetToEntry(progress, RuntimeDungeonEventKind.GameOverRecovered);

    public IReadOnlyList<RuntimeFieldActionOption> GetDungeonActionOptions(
        RuntimeDungeonFloorSnapshot floor,
        bool canOrganizeParty)
    {
        var options = new List<RuntimeFieldActionOption>
        {
            floor.Kind == RuntimeDungeonFloorKind.BlockEnd
                ? new RuntimeFieldActionOption(RuntimeFieldActionKind.Barrier, "Barrier (Cannot Pass)")
                : new RuntimeFieldActionOption(RuntimeFieldActionKind.AscendStairs, "Ascend Stairs")
        };

        if (floor.FloorNumber > 1)
        {
            options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.DescendStairs, "Descend Stairs"));
        }

        if (floor.FloorNumber == 1)
        {
            options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.Clock, "Clock (Heal)"));
            options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.Terminal, "Terminal (Warp)"));
            options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.ReturnToCity, "Return to City"));
        }
        else if (floor.HasTerminal)
        {
            options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.Terminal, "Access Terminal (Return)"));
        }

        options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.Inventory, "Inventory"));
        options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.Status, "Status"));

        if (canOrganizeParty)
        {
            options.Add(new RuntimeFieldActionOption(RuntimeFieldActionKind.OrganizeParty, "Organize Party"));
        }

        return RuntimeSnapshotCollections.List(options);
    }

    private RuntimeDungeonTransitionResult ResetToEntry(
        RuntimeDungeonProgressSnapshot progress,
        RuntimeDungeonEventKind eventKind)
    {
        RuntimeDungeonProgressSnapshot after = progress.With(currentFloor: 1);
        return new RuntimeDungeonTransitionResult(
            RuntimeDungeonTransitionCode.Applied,
            progress,
            after,
            events: [new RuntimeDungeonEvent(eventKind, floor: 1)]);
    }

    private RuntimeDungeonFloorSnapshot EvaluateFloor(
        RuntimeDungeonContentSnapshot content,
        RuntimeDungeonProgressSnapshot progress)
    {
        if (progress.CurrentFloor == 1)
        {
            return new RuntimeDungeonFloorSnapshot(
                1,
                "Entrance",
                RuntimeDungeonFloorKind.SafeRoom,
                "The Lobby. A large clock ticks quietly.",
                hasTerminal: true);
        }

        RuntimeDungeonBlockSnapshot? block = content.Blocks.FirstOrDefault(block => block.ContainsFloor(progress.CurrentFloor));
        if (block is null)
        {
            return new RuntimeDungeonFloorSnapshot(
                progress.CurrentFloor,
                "Unknown Block",
                RuntimeDungeonFloorKind.Empty,
                "You are outside the known map.");
        }

        RuntimeDungeonFixedFloorSnapshot? fixedFloor =
            block.FixedFloors.FirstOrDefault(floor => floor.Floor == progress.CurrentFloor);
        if (fixedFloor is not null)
        {
            if (fixedFloor.Kind == RuntimeDungeonFloorKind.Boss && fixedFloor.EventId is ContentId bossId)
            {
                if (progress.IsBossDefeated(bossId))
                {
                    return new RuntimeDungeonFloorSnapshot(
                        progress.CurrentFloor,
                        block.DisplayName,
                        RuntimeDungeonFloorKind.Empty,
                        "The area is quiet. The guardian has been defeated.",
                        fixedFloor.HasTerminal);
                }

                return new RuntimeDungeonFloorSnapshot(
                    progress.CurrentFloor,
                    block.DisplayName,
                    RuntimeDungeonFloorKind.Boss,
                    fixedFloor.Description,
                    fixedFloor.HasTerminal,
                    [bossId]);
            }

            if (fixedFloor.Kind == RuntimeDungeonFloorKind.Battle && fixedFloor.EventId is ContentId encounterId)
            {
                return new RuntimeDungeonFloorSnapshot(
                    progress.CurrentFloor,
                    block.DisplayName,
                    RuntimeDungeonFloorKind.Battle,
                    fixedFloor.Description,
                    fixedFloor.HasTerminal,
                    [encounterId]);
            }

            return new RuntimeDungeonFloorSnapshot(
                progress.CurrentFloor,
                block.DisplayName,
                fixedFloor.Kind,
                fixedFloor.Description,
                fixedFloor.HasTerminal);
        }

        return new RuntimeDungeonFloorSnapshot(
            progress.CurrentFloor,
            block.DisplayName,
            RuntimeDungeonFloorKind.Battle,
            "Shadows lurk in the darkness...",
            enemyIds: GenerateRandomEncounter(block));
    }

    private IReadOnlyList<ContentId> GenerateRandomEncounter(RuntimeDungeonBlockSnapshot block)
    {
        if (block.EnemyPoolIds.Count == 0)
        {
            return RuntimeSnapshotCollections.List([ContentId.Parse("legacy_455f736c696d65")]);
        }

        int count = _random.NextInt32(1, 4);
        var encounter = new List<ContentId>();
        for (int i = 0; i < count; i++)
        {
            int index = _random.NextInt32(0, block.EnemyPoolIds.Count);
            encounter.Add(block.EnemyPoolIds[index]);
        }

        return RuntimeSnapshotCollections.List(encounter);
    }
}
