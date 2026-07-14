using JRPGPrototype.Data;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field.Dungeon
{
    internal static class LegacyDungeonContentAdapter
    {
        public static RuntimeDungeonContentSnapshot FromDatabase(string dungeonId)
        {
            if (!Database.Dungeons.TryGetValue(dungeonId, out DungeonData? dungeonData))
            {
                return new RuntimeDungeonContentSnapshot(
                    LegacyContentIdCodec.Encode(dungeonId),
                    "Unknown Void");
            }

            return FromData(dungeonData);
        }

        public static RuntimeDungeonContentSnapshot FromData(DungeonData dungeonData) =>
            new(
                LegacyContentIdCodec.Encode(dungeonData.Id),
                string.IsNullOrWhiteSpace(dungeonData.Name) ? "Unknown Void" : dungeonData.Name,
                (dungeonData.Blocks ?? []).Where(HasValidRange).Select(MapBlock));

        private static RuntimeDungeonBlockSnapshot MapBlock(BlockData block) =>
            new(
                LegacyContentIdCodec.Encode(block.BlockId),
                string.IsNullOrWhiteSpace(block.Name) ? "Unknown Block" : block.Name,
                block.FloorRange[0],
                block.FloorRange[1],
                (block.EnemyPool ?? []).Select(LegacyContentIdCodec.Encode),
                (block.FixedFloors ?? []).Where(floor => floor.Floor > 0).Select(MapFixedFloor));

        private static RuntimeDungeonFixedFloorSnapshot MapFixedFloor(FixedFloorData floor) =>
            new(
                floor.Floor,
                MapFloorKind(floor.Type),
                string.IsNullOrWhiteSpace(floor.Id) ? null : LegacyContentIdCodec.Encode(floor.Id),
                floor.HasTerminal,
                floor.Description);

        private static RuntimeDungeonFloorKind MapFloorKind(string? type) => type switch
        {
            "Boss" => RuntimeDungeonFloorKind.Boss,
            "SafeRoom" => RuntimeDungeonFloorKind.SafeRoom,
            "BlockEnd" => RuntimeDungeonFloorKind.BlockEnd,
            _ => RuntimeDungeonFloorKind.Empty
        };

        private static bool HasValidRange(BlockData block) =>
            block.FloorRange is { Length: >= 2 } &&
            block.FloorRange[0] > 0 &&
            block.FloorRange[1] >= block.FloorRange[0];
    }
}
