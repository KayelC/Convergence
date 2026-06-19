using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class FieldDungeonStateMachineTests
{
    [Fact]
    public void DungeonSnapshots_DefensivelyCopyCollectionsAndExposeImmutableResults()
    {
        List<ContentId> pool = [Id("pixie")];
        List<RuntimeDungeonFixedFloorSnapshot> fixedFloors =
        [
            new(10, RuntimeDungeonFloorKind.SafeRoom, hasTerminal: true, description: "Calm air.")
        ];
        var block = new RuntimeDungeonBlockSnapshot(Id("thebel"), "Thebel", 2, 20, pool, fixedFloors);
        var content = new RuntimeDungeonContentSnapshot(Id("tartarus"), "Tartarus", [block]);
        var progress = new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 10);

        pool.Add(Id("slime"));
        fixedFloors.Clear();
        RuntimeDungeonTransitionResult result = Service().ProcessCurrentFloor(content, progress);

        Assert.Single(block.EnemyPoolIds);
        Assert.Single(block.FixedFloors);
        Assert.Equal(RuntimeDungeonFloorKind.SafeRoom, result.Floor!.Kind);
        Assert.Equal([1], progress.UnlockedTerminals);
        Assert.Equal([1, 10], result.After.UnlockedTerminals);
        Assert.Throws<NotSupportedException>(() => ((IList<RuntimeDungeonEvent>)result.Events).Add(
            new RuntimeDungeonEvent(RuntimeDungeonEventKind.DungeonExited)));
    }

    [Fact]
    public void DungeonTransitions_HandleMovementBarrierWarpReturnAndRecovery()
    {
        RuntimeDungeonContentSnapshot content = Content(
            new RuntimeDungeonBlockSnapshot(
                Id("thebel"),
                "Thebel",
                2,
                5,
                [Id("pixie")],
                [new RuntimeDungeonFixedFloorSnapshot(5, RuntimeDungeonFloorKind.BlockEnd, description: "A barrier blocks the way.")]));
        var progress = new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 4);
        RuntimeFieldDungeonService service = Service();

        RuntimeDungeonTransitionResult ascended = service.Ascend(content, progress);
        RuntimeDungeonTransitionResult blocked = service.Ascend(content, ascended.After);
        RuntimeDungeonTransitionResult descended = service.Descend(content, new RuntimeDungeonProgressSnapshot(Id("tartarus")));
        RuntimeDungeonTransitionResult rejectedWarp = service.Warp(content, progress, 3);
        RuntimeDungeonTransitionResult returned = service.ReturnToCity(ascended.After);
        RuntimeDungeonTransitionResult recovered = service.RecoverFromGameOver(ascended.After);

        Assert.Equal(5, ascended.After.CurrentFloor);
        Assert.Equal(RuntimeDungeonFloorKind.BlockEnd, ascended.Floor!.Kind);
        Assert.Equal(RuntimeDungeonTransitionCode.BarrierBlocked, blocked.Code);
        Assert.Equal(5, blocked.After.CurrentFloor);
        Assert.Equal(1, descended.After.CurrentFloor);
        Assert.Equal(RuntimeDungeonTransitionCode.InvalidFloor, rejectedWarp.Code);
        Assert.Equal(1, returned.After.CurrentFloor);
        Assert.Contains(returned.Events, ev => ev.Kind == RuntimeDungeonEventKind.DungeonExited);
        Assert.Equal(1, recovered.After.CurrentFloor);
        Assert.Contains(recovered.Events, ev => ev.Kind == RuntimeDungeonEventKind.GameOverRecovered);
    }

    [Fact]
    public void FloorEvaluation_PreservesLobbyFixedFloorsBossDefeatAndUnknownMap()
    {
        ContentId chimera = Id("chimera");
        ContentId fixedEncounter = Id("fixed_drill");
        RuntimeDungeonContentSnapshot content = Content(
            new RuntimeDungeonBlockSnapshot(
                Id("thebel"),
                "Thebel",
                2,
                20,
                [Id("pixie")],
                [
                    new RuntimeDungeonFixedFloorSnapshot(5, RuntimeDungeonFloorKind.Boss, chimera, description: "A guardian waits."),
                    new RuntimeDungeonFixedFloorSnapshot(6, RuntimeDungeonFloorKind.Battle, fixedEncounter, description: "A fixed drill starts."),
                    new RuntimeDungeonFixedFloorSnapshot(10, RuntimeDungeonFloorKind.SafeRoom, hasTerminal: true, description: "The air here is calm.")
                ]));
        RuntimeFieldDungeonService service = Service();

        RuntimeDungeonTransitionResult lobby = service.ProcessCurrentFloor(content, new RuntimeDungeonProgressSnapshot(Id("tartarus")));
        RuntimeDungeonTransitionResult terminal = service.ProcessCurrentFloor(content, new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 10));
        RuntimeDungeonTransitionResult boss = service.ProcessCurrentFloor(content, new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 5));
        RuntimeDungeonTransitionResult fixedBattle = service.ProcessCurrentFloor(content, new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 6));
        RuntimeDungeonTransitionResult defeated = service.ProcessCurrentFloor(
            content,
            boss.After.MarkBossDefeated(chimera));
        RuntimeDungeonTransitionResult unknown = service.ProcessCurrentFloor(content, new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 99));

        Assert.Equal(RuntimeDungeonFloorKind.SafeRoom, lobby.Floor!.Kind);
        Assert.Equal("Entrance", lobby.Floor.BlockName);
        Assert.Equal([1, 10], terminal.After.UnlockedTerminals);
        Assert.Contains(terminal.Events, ev => ev.Kind == RuntimeDungeonEventKind.TerminalUnlocked);
        Assert.Equal(RuntimeDungeonFloorKind.Boss, boss.Floor!.Kind);
        Assert.Equal([chimera], boss.Floor.EnemyIds);
        Assert.Equal(RuntimeDungeonFloorKind.Battle, fixedBattle.Floor!.Kind);
        Assert.Equal([fixedEncounter], fixedBattle.Floor.EnemyIds);
        Assert.Contains(fixedBattle.Events, ev =>
            ev.Kind == RuntimeDungeonEventKind.EncounterRequested &&
            ev.EnemyIds.SequenceEqual(fixedBattle.Floor.EnemyIds));
        Assert.Equal(RuntimeDungeonFloorKind.Empty, defeated.Floor!.Kind);
        Assert.Contains("guardian has been defeated", defeated.Floor.Description, StringComparison.Ordinal);
        Assert.Equal(RuntimeDungeonFloorKind.Empty, unknown.Floor!.Kind);
        Assert.Equal("You are outside the known map.", unknown.Floor.Description);
    }

    [Fact]
    public void RandomEncounters_UseDeterministicCountPoolSelectionAndEmptyFallback()
    {
        RuntimeDungeonContentSnapshot content = Content(
            new RuntimeDungeonBlockSnapshot(
                Id("thebel"),
                "Thebel",
                2,
                20,
                [Id("pixie"), Id("slime")]));
        RuntimeFieldDungeonService service = Service(3, 1, 0, 1);

        RuntimeDungeonTransitionResult battle = service.ProcessCurrentFloor(
            content,
            new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 2));
        RuntimeDungeonTransitionResult fallback = Service().ProcessCurrentFloor(
            Content(new RuntimeDungeonBlockSnapshot(Id("empty"), "Empty", 2, 2)),
            new RuntimeDungeonProgressSnapshot(Id("tartarus"), currentFloor: 2));

        Assert.Equal(RuntimeDungeonFloorKind.Battle, battle.Floor!.Kind);
        Assert.Equal([Id("slime"), Id("pixie"), Id("slime")], battle.Floor.EnemyIds);
        Assert.Contains(battle.Events, ev =>
            ev.Kind == RuntimeDungeonEventKind.EncounterRequested &&
            ev.EnemyIds.SequenceEqual(battle.Floor.EnemyIds));
        Assert.Equal([ContentId.Parse("legacy_455f736c696d65")], fallback.Floor!.EnemyIds);
    }

    [Fact]
    public void DungeonActions_PreserveCurrentConsoleOrdering()
    {
        RuntimeFieldDungeonService service = Service();
        var lobby = new RuntimeDungeonFloorSnapshot(1, "Entrance", RuntimeDungeonFloorKind.SafeRoom, "Lobby", hasTerminal: true);
        var terminal = new RuntimeDungeonFloorSnapshot(10, "Thebel", RuntimeDungeonFloorKind.SafeRoom, "Safe", hasTerminal: true);
        var blockEnd = new RuntimeDungeonFloorSnapshot(20, "Thebel", RuntimeDungeonFloorKind.BlockEnd, "Barrier");

        Assert.Equal(
            [
                RuntimeFieldActionKind.AscendStairs,
                RuntimeFieldActionKind.Clock,
                RuntimeFieldActionKind.Terminal,
                RuntimeFieldActionKind.ReturnToCity,
                RuntimeFieldActionKind.Inventory,
                RuntimeFieldActionKind.Status,
                RuntimeFieldActionKind.OrganizeParty
            ],
            service.GetDungeonActionOptions(lobby, canOrganizeParty: true).Select(option => option.Kind));
        Assert.Equal(
            [
                RuntimeFieldActionKind.AscendStairs,
                RuntimeFieldActionKind.DescendStairs,
                RuntimeFieldActionKind.Terminal,
                RuntimeFieldActionKind.Inventory,
                RuntimeFieldActionKind.Status
            ],
            service.GetDungeonActionOptions(terminal, canOrganizeParty: false).Select(option => option.Kind));
        Assert.Equal(RuntimeFieldActionKind.Barrier, service.GetDungeonActionOptions(blockEnd, canOrganizeParty: false)[0].Kind);
    }

    private static RuntimeFieldDungeonService Service(params int[] randomValues) =>
        new(new SequenceRandomSource(randomValues));

    private static RuntimeDungeonContentSnapshot Content(params RuntimeDungeonBlockSnapshot[] blocks) =>
        new(Id("tartarus"), "Tartarus", blocks);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class SequenceRandomSource(params int[] values) : IRandomSource
    {
        private readonly Queue<int> _values = new(values);

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            if (_values.Count == 0)
            {
                return minimumInclusive;
            }

            int value = _values.Dequeue();
            Assert.InRange(value, minimumInclusive, maximumExclusive - 1);
            return value;
        }

        public decimal NextUnitDecimal() => 0m;
    }

}
