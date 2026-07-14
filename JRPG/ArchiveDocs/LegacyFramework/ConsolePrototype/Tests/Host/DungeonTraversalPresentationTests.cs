using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Logic.Field.State;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Host;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class DungeonTraversalPresentationTests
{
    [Fact]
    public void DungeonSelections_ReturnTypedResultsAndPreserveOptionOrder()
    {
        var player = new Combatant("Hero", ClassType.Operator)
        {
            CurrentHP = 40,
            MaxHP = 50,
            CurrentSP = 20,
            MaxSP = 30
        };
        var io = new ScriptedGameIO()
            .QueueMenu(5)
            .QueueMenu(1)
            .QueueMenu(1)
            .QueueMenu(-1);
        var bridge = new DungeonUIBridge(io, new FieldUIState());
        DungeonFloorResult lobby = Lobby();

        DungeonFloorActionSelectionResult action = bridge.ShowFloorActionResult(lobby, player);
        DungeonFloorSelectionResult entry = bridge.SelectEntryPointResult([1, 10]);
        DungeonFloorSelectionResult warp = bridge.SelectWarpDestinationResult([1, 10], currentFloor: 1);
        DungeonFloorSelectionResult cancelled = bridge.SelectWarpDestinationResult([1, 10], currentFloor: 10);

        Assert.Equal(DungeonPresentationResultKind.Selected, action.Kind);
        Assert.Equal(DungeonFloorActionCommand.Status, action.Command);
        Assert.Equal(DungeonPresentationResultKind.Selected, entry.Kind);
        Assert.Equal(10, entry.Floor);
        Assert.Equal(DungeonPresentationResultKind.Selected, warp.Kind);
        Assert.Equal(10, warp.Floor);
        Assert.Equal(DungeonPresentationResultKind.Back, cancelled.Kind);
        Assert.Equal(
            ["Ascend Stairs", "Clock (Heal)", "Terminal (Warp)", "Return to City", "Inventory", "Status", "Organize Party"],
            io.Menus[0].Options);
        Assert.Equal(["Lobby (Entrance)", "Floor 10", "Cancel"], io.Menus[1].Options);
        Assert.Equal(["Lobby (Current)", "Floor 10", "Cancel"], io.Menus[2].Options);
        Assert.Equal([true, false, false], io.Menus[2].DisabledOptions);
        Assert.Equal(["Lobby", "Floor 10 (Current)", "Cancel"], io.Menus[3].Options);
        Assert.Equal([false, true, false], io.Menus[3].DisabledOptions);
        io.AssertConsumed();
    }

    [Fact]
    public void DungeonManagerDetailedTransitions_MapFrameworkEventsToLegacyPresentation()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var state = new DungeonState();
        var manager = new DungeonManager(state);

        DungeonTransitionPresentationResult lobby = manager.ProcessCurrentFloorDetailed();
        manager.WarpToFloor(10);
        DungeonTransitionPresentationResult safeRoom = manager.ProcessCurrentFloorDetailed();
        manager.WarpToFloor(20);
        DungeonTransitionPresentationResult blockEnd = manager.ProcessCurrentFloorDetailed();
        DungeonTransitionPresentationResult barrier = manager.InteractBarrierDetailed();
        DungeonTransitionPresentationResult invalidWarp = manager.WarpToUnlockedFloorDetailed(99);
        manager.WarpToFloor(5);
        DungeonTransitionPresentationResult boss = manager.ProcessCurrentFloorDetailed();
        DungeonTransitionPresentationResult defeated = manager.RegisterBossDefeatDetailed("chimera");
        DungeonTransitionPresentationResult exit = manager.RequestDungeonExitDetailed();

        Assert.Equal(DungeonEventType.SafeRoom, lobby.Floor?.Type);
        Assert.Contains(lobby.Events, ev => ev.EventKind == RuntimeDungeonEventKind.FloorEntered &&
                                            ev.Kind == DungeonPresentationResultKind.Suppressed);
        Assert.Equal(DungeonPresentationResultKind.Shown, Assert.Single(
            safeRoom.Events,
            ev => ev.EventKind == RuntimeDungeonEventKind.SafeRoom).Kind);
        Assert.Equal("The air here is calm.", safeRoom.Events.Single(ev => ev.EventKind == RuntimeDungeonEventKind.SafeRoom).Message);
        Assert.Equal(DungeonEventType.BlockEnd, blockEnd.Floor?.Type);
        Assert.Contains(blockEnd.Events, ev => ev.EventKind == RuntimeDungeonEventKind.BarrierBlocked);
        Assert.Equal(RuntimeDungeonTransitionCode.BarrierBlocked, barrier.Transition.Code);
        Assert.True(barrier.LegacySuccess);
        Assert.Equal("The path is sealed.", Assert.Single(barrier.Events, ev => ev.Kind == DungeonPresentationResultKind.Shown).Message);
        Assert.False(invalidWarp.LegacySuccess);
        Assert.Equal(RuntimeDungeonTransitionCode.InvalidFloor, invalidWarp.Transition.Code);
        Assert.Equal(DungeonEventType.Boss, boss.Floor?.Type);
        Assert.Equal("!!! POWERFUL SHADOW DETECTED !!!", boss.Events.Single(ev => ev.EventKind == RuntimeDungeonEventKind.BossRequested).Message);
        Assert.Equal("The Guardian has been defeated!", Assert.Single(defeated.Events, ev => ev.EventKind == RuntimeDungeonEventKind.BossDefeated).Message);
        Assert.Equal(1, state.CurrentFloor);
        Assert.Contains(exit.Events, ev => ev.EventKind == RuntimeDungeonEventKind.DungeonExited &&
                                          ev.Kind == DungeonPresentationResultKind.Suppressed);
    }

    [Fact]
    public void ExplorationProcessor_PublishesOnlyLegacyVisibleFloorEntryMessages()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var player = new Combatant("Hero", ClassType.Operator);
        var dungeon = new DungeonState();
        var manager = new DungeonManager(dungeon);
        var processor = Processor(io, player, dungeon, manager);

        DungeonFloorEntryPresentationResult lobby = processor.ProcessFloorEntryDetailed(Lobby());
        DungeonFloorEntryPresentationResult safeRoom = processor.ProcessFloorEntryDetailed(new DungeonFloorResult
        {
            FloorNumber = 10,
            BlockName = "Thebel",
            Type = DungeonEventType.SafeRoom,
            Description = "A calm room.",
            HasTerminal = true
        });
        DungeonFloorEntryPresentationResult battle = processor.ProcessFloorEntryDetailed(new DungeonFloorResult
        {
            FloorNumber = 2,
            BlockName = "Thebel",
            Type = DungeonEventType.Battle,
            Description = "Shadows lurk.",
            EnemyIds = ["pixie"]
        });
        DungeonFloorEntryPresentationResult boss = processor.ProcessFloorEntryDetailed(new DungeonFloorResult
        {
            FloorNumber = 5,
            BlockName = "Thebel",
            Type = DungeonEventType.Boss,
            Description = "A guardian waits.",
            EnemyIds = ["chimera"]
        });

        Assert.Equal(ExplorationEvent.None, lobby.LegacyEvent);
        Assert.Equal(ExplorationEvent.None, safeRoom.LegacyEvent);
        Assert.Equal(ExplorationEvent.Encounter, battle.LegacyEvent);
        Assert.Equal(ExplorationEvent.BossEncounter, boss.LegacyEvent);
        Assert.Contains(10, dungeon.UnlockedTerminals);
        Assert.Equal(["The air here is calm.", "!!! POWERFUL SHADOW DETECTED !!!"], io.Writes.Select(write => write.Text));
        Assert.Equal([ConsoleColor.Green, ConsoleColor.Red], io.Writes.Select(write => write.Color));
        Assert.Equal([800, 1000], io.Waits);
        Assert.Contains(battle.Events, ev => ev.EventKind == RuntimeDungeonEventKind.EncounterRequested &&
                                            ev.Kind == DungeonPresentationResultKind.Suppressed);
    }

    [Fact]
    public void ExplorationProcessor_NavigationPublishesMovementAndPreservesBattleHandoffOwnership()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var player = new Combatant("Hero", ClassType.Operator);
        var dungeon = new DungeonState();
        var manager = new DungeonManager(dungeon, new Random(1));
        var processor = Processor(io, player, dungeon, manager);

        DungeonTransitionPresentationResult ascended = processor.PerformAscensionDetailed();
        DungeonFloorEntryPresentationResult entry = processor.ProcessFloorEntryDetailed(ascended);
        List<Combatant> enemies = processor.PrepareEncounter(["pixie", "pixie"]);

        Assert.Equal(2, dungeon.CurrentFloor);
        Assert.Equal("Ascending...", Assert.Single(io.Writes).Text);
        Assert.Equal(ConsoleColor.White, io.Writes[0].Color);
        Assert.Equal(500, Assert.Single(io.Waits));
        Assert.Equal(DungeonEventType.Battle, ascended.Floor?.Type);
        Assert.Equal(ExplorationEvent.Encounter, entry.LegacyEvent);
        Assert.Empty(io.Writes.Skip(1));
        Assert.Equal(["Pixie A", "Pixie B"], enemies.Select(enemy => enemy.Name));
    }

    [Fact]
    public void DungeonExitAndBossDefeatPresentation_PreserveVisibleMessages()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var manager = new DungeonManager(new DungeonState());
        var bridge = new DungeonUIBridge(io, new FieldUIState());

        manager.WarpToFloor(5);
        manager.ProcessCurrentFloorDetailed();
        DungeonTransitionPresentationResult defeated = manager.RegisterBossDefeatDetailed("chimera");
        bridge.PublishPresentationEvents(
            DungeonPresentationMapper.VisibleOnly(defeated.Events, RuntimeDungeonEventKind.BossDefeated));
        DungeonTransitionPresentationResult exit = manager.RequestDungeonExitDetailed();
        bridge.PublishPresentationEvents(exit.Events);

        Assert.Equal("The Guardian has been defeated!", Assert.Single(io.Writes).Text);
        Assert.Equal(ConsoleColor.Cyan, io.Writes[0].Color);
        Assert.Equal([1500], io.Waits);
        Assert.Equal(1, manager.CurrentFloor);
        Assert.All(exit.Events, ev => Assert.Equal(DungeonPresentationResultKind.Suppressed, ev.Kind));
    }

    private static ExplorationProcessor Processor(
        ScriptedGameIO io,
        Combatant player,
        DungeonState dungeon,
        DungeonManager manager)
    {
        var messenger = new RecordingFieldMessenger();
        var party = new PartyManager(player);
        var serviceEngine = new FieldServiceEngine(
            messenger,
            io,
            new EconomyManager(),
            new InventoryManager(),
            party,
            dungeon);
        return new ExplorationProcessor(
            messenger,
            manager,
            dungeon,
            new DungeonUIBridge(io, new FieldUIState()),
            serviceEngine);
    }

    private static DungeonFloorResult Lobby() =>
        new()
        {
            FloorNumber = 1,
            BlockName = "Entrance",
            Type = DungeonEventType.SafeRoom,
            Description = "The Lobby.",
            HasTerminal = true
        };

    private sealed class RecordingFieldMessenger : IFieldMessenger
    {
        public event EventHandler<FieldMessageArgs>? OnMessagePublished;

        public void Publish(
            string? message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            OnMessagePublished?.Invoke(this, new FieldMessageArgs(message, color, delay, waitForInput, clearScreen));
        }
    }
}
