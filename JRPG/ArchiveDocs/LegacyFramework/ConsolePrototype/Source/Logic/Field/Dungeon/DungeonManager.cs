using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field.Dungeon
{
    public class DungeonFloorResult
    {
        public int FloorNumber { get; set; }
        public string BlockName { get; set; }
        public DungeonEventType Type { get; set; }
        public string Description { get; set; }
        public List<string> EnemyIds { get; set; } = new List<string>();
        public bool HasTerminal { get; set; }
    }

    public class DungeonManager
    {
        private readonly DungeonState _state;
        private readonly IRuntimeFieldDungeonService _service;
        private RuntimeDungeonContentSnapshot _content;

        public DungeonManager(DungeonState state)
            : this(state, new Random())
        {
        }

        internal DungeonManager(DungeonState state, Random random)
            : this(state, new RuntimeFieldDungeonService(new LegacyDungeonRandomSource(random)))
        {
        }

        internal DungeonManager(DungeonState state, IRuntimeFieldDungeonService service)
        {
            _state = state;
            _service = service;
            _content = LegacyDungeonContentAdapter.FromDatabase(_state.CurrentDungeonId);
        }

        public int CurrentFloor => _state.CurrentFloor;

        // --- NAVIGATION ---
        public void Ascend()
        {
            AscendDetailed();
        }

        public void Descend()
        {
            DescendDetailed();
        }

        public void WarpToFloor(int floor)
        {
            _state.CurrentFloor = floor;
        }

        internal bool TryWarpToUnlockedFloor(int floor)
        {
            return WarpToUnlockedFloorDetailed(floor).LegacySuccess;
        }

        public void ReturnToCity()
        {
            ReturnToCityDetailed();
        }

        public void RequestDungeonExit()
        {
            RequestDungeonExitDetailed();
        }

        public IReadOnlyList<RuntimeFieldActionOption> GetActionOptions(DungeonFloorResult floorInfo, bool canOrganizeParty) =>
            _service.GetDungeonActionOptions(ToRuntimeFloor(floorInfo), canOrganizeParty);

        // --- CORE LOGIC ---
        public DungeonFloorResult ProcessCurrentFloor()
        {
            return ProcessCurrentFloorDetailed().Floor!;
        }

        public List<int> GetUnlockedTerminals()
        {
            return _state.UnlockedTerminals.OrderBy(x => x).ToList();
        }

        public void RegisterBossDefeat(string bossId)
        {
            RegisterBossDefeatDetailed(bossId);
        }

        internal DungeonTransitionPresentationResult ProcessCurrentFloorDetailed() =>
            Present(_service.ProcessCurrentFloor(CurrentContent(), Snapshot()));

        internal DungeonTransitionPresentationResult AscendDetailed() =>
            Present(_service.Ascend(CurrentContent(), Snapshot()));

        internal DungeonTransitionPresentationResult DescendDetailed() =>
            Present(_service.Descend(CurrentContent(), Snapshot()));

        internal DungeonTransitionPresentationResult WarpToUnlockedFloorDetailed(int floor) =>
            Present(_service.Warp(CurrentContent(), Snapshot(), floor));

        internal DungeonTransitionPresentationResult ReturnToCityDetailed() =>
            Present(_service.ReturnToCity(Snapshot()));

        internal DungeonTransitionPresentationResult RequestDungeonExitDetailed() =>
            Present(_service.RequestDungeonExit(Snapshot()));

        internal DungeonTransitionPresentationResult InteractBarrierDetailed() =>
            Present(_service.InteractBarrier(Snapshot()));

        internal DungeonTransitionPresentationResult RegisterBossDefeatDetailed(string? bossId)
        {
            ContentId? cleanBossId = string.IsNullOrWhiteSpace(bossId)
                ? null
                : LegacyContentIdCodec.Encode(bossId);
            return Present(_service.RegisterBossDefeat(Snapshot(), cleanBossId));
        }

        private RuntimeDungeonProgressSnapshot Snapshot() =>
            new(
                LegacyContentIdCodec.Encode(_state.CurrentDungeonId),
                _state.CurrentFloor,
                _state.MaxFloorReached,
                _state.UnlockedTerminals,
                _state.DefeatedBosses.Select(LegacyContentIdCodec.Encode));

        private RuntimeDungeonContentSnapshot CurrentContent()
        {
            if (_content.Id != LegacyContentIdCodec.Encode(_state.CurrentDungeonId))
            {
                _content = LegacyDungeonContentAdapter.FromDatabase(_state.CurrentDungeonId);
            }

            return _content;
        }

        private void Apply(RuntimeDungeonTransitionResult result)
        {
            if (!result.Applied && result.Code != RuntimeDungeonTransitionCode.BarrierBlocked)
            {
                return;
            }

            _state.CurrentFloor = result.After.CurrentFloor;
            _state.MaxFloorReached = result.After.MaxFloorReached;
            _state.UnlockedTerminals = result.After.UnlockedTerminals.ToHashSet();
            _state.DefeatedBosses = result.After.DefeatedBossIds.Select(LegacyContentIdCodec.Decode).ToHashSet();
        }

        private DungeonTransitionPresentationResult Present(RuntimeDungeonTransitionResult result)
        {
            Apply(result);
            DungeonFloorResult? floor = result.Floor is null ? null : ToLegacyFloor(result.Floor);
            return new DungeonTransitionPresentationResult(
                result,
                floor,
                DungeonPresentationMapper.MapTransitionEvents(result.Events, floor));
        }

        private static DungeonFloorResult ToLegacyFloor(RuntimeDungeonFloorSnapshot floor) =>
            new()
            {
                FloorNumber = floor.FloorNumber,
                BlockName = floor.BlockName,
                Type = ToLegacyFloorKind(floor.Kind),
                Description = floor.Description,
                EnemyIds = floor.EnemyIds.Select(LegacyContentIdCodec.Decode).ToList(),
                HasTerminal = floor.HasTerminal
            };

        private static RuntimeDungeonFloorSnapshot ToRuntimeFloor(DungeonFloorResult floor) =>
            new(
                floor.FloorNumber,
                floor.BlockName,
                ToRuntimeFloorKind(floor.Type),
                floor.Description,
                floor.HasTerminal,
                floor.EnemyIds.Select(LegacyContentIdCodec.Encode));

        private static DungeonEventType ToLegacyFloorKind(RuntimeDungeonFloorKind kind) => kind switch
        {
            RuntimeDungeonFloorKind.Battle => DungeonEventType.Battle,
            RuntimeDungeonFloorKind.Boss => DungeonEventType.Boss,
            RuntimeDungeonFloorKind.SafeRoom => DungeonEventType.SafeRoom,
            RuntimeDungeonFloorKind.BlockEnd => DungeonEventType.BlockEnd,
            _ => DungeonEventType.Empty
        };

        private static RuntimeDungeonFloorKind ToRuntimeFloorKind(DungeonEventType kind) => kind switch
        {
            DungeonEventType.Battle => RuntimeDungeonFloorKind.Battle,
            DungeonEventType.Boss => RuntimeDungeonFloorKind.Boss,
            DungeonEventType.SafeRoom => RuntimeDungeonFloorKind.SafeRoom,
            DungeonEventType.BlockEnd => RuntimeDungeonFloorKind.BlockEnd,
            _ => RuntimeDungeonFloorKind.Empty
        };

        private sealed class LegacyDungeonRandomSource : IRandomSource
        {
            private readonly Random _random;

            public LegacyDungeonRandomSource(Random random)
            {
                _random = random ?? throw new ArgumentNullException(nameof(random));
            }

            public int NextInt32(int minimumInclusive, int maximumExclusive) =>
                _random.Next(minimumInclusive, maximumExclusive);

            public decimal NextUnitDecimal() => (decimal)_random.NextDouble();
        }
    }
}
