using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field
{
    public enum DungeonPresentationResultKind
    {
        Selected,
        Back,
        Unavailable,
        Shown,
        Suppressed,
        HostOwned
    }

    public sealed record DungeonPresentationEvent(
        RuntimeDungeonEventKind EventKind,
        DungeonPresentationResultKind Kind,
        string? Message = null,
        ConsoleColor Color = ConsoleColor.Gray,
        int Delay = 0,
        bool WaitForInput = false,
        bool ClearScreen = false,
        int? Floor = null);

    public sealed record DungeonFloorActionSelectionResult(
        DungeonPresentationResultKind Kind,
        DungeonFloorActionCommand Command = DungeonFloorActionCommand.Cancel)
    {
        public static DungeonFloorActionSelectionResult Back { get; } =
            new(DungeonPresentationResultKind.Back);

        public static DungeonFloorActionSelectionResult Unavailable { get; } =
            new(DungeonPresentationResultKind.Unavailable);

        public static DungeonFloorActionSelectionResult Selected(DungeonFloorActionCommand command) =>
            new(DungeonPresentationResultKind.Selected, command);
    }

    public sealed record DungeonFloorSelectionResult(
        DungeonPresentationResultKind Kind,
        int? Floor = null)
    {
        public static DungeonFloorSelectionResult Back { get; } =
            new(DungeonPresentationResultKind.Back);

        public static DungeonFloorSelectionResult Unavailable { get; } =
            new(DungeonPresentationResultKind.Unavailable);

        public static DungeonFloorSelectionResult Selected(int floor) =>
            new(DungeonPresentationResultKind.Selected, floor);
    }

    public sealed record DungeonTransitionPresentationResult
    {
        public DungeonTransitionPresentationResult(
            RuntimeDungeonTransitionResult transition,
            DungeonFloorResult? floor,
            IEnumerable<DungeonPresentationEvent>? events = null)
        {
            Transition = transition ?? throw new ArgumentNullException(nameof(transition));
            Floor = floor;
            Events = new ReadOnlyCollection<DungeonPresentationEvent>(
                new List<DungeonPresentationEvent>(events ?? Array.Empty<DungeonPresentationEvent>()));
        }

        public RuntimeDungeonTransitionResult Transition { get; }
        public DungeonFloorResult? Floor { get; }
        public IReadOnlyList<DungeonPresentationEvent> Events { get; }
        public bool LegacySuccess =>
            Transition.Applied || Transition.Code == RuntimeDungeonTransitionCode.BarrierBlocked;
    }

    public sealed record DungeonFloorEntryPresentationResult
    {
        public DungeonFloorEntryPresentationResult(
            ExplorationEvent legacyEvent,
            DungeonFloorResult floor,
            IEnumerable<string>? enemyIds = null,
            bool isBoss = false,
            IEnumerable<DungeonPresentationEvent>? events = null)
        {
            LegacyEvent = legacyEvent;
            Floor = floor ?? throw new ArgumentNullException(nameof(floor));
            EnemyIds = new ReadOnlyCollection<string>(new List<string>(enemyIds ?? Array.Empty<string>()));
            IsBoss = isBoss;
            Events = new ReadOnlyCollection<DungeonPresentationEvent>(
                new List<DungeonPresentationEvent>(events ?? Array.Empty<DungeonPresentationEvent>()));
        }

        public ExplorationEvent LegacyEvent { get; }
        public DungeonFloorResult Floor { get; }
        public IReadOnlyList<string> EnemyIds { get; }
        public bool IsBoss { get; }
        public IReadOnlyList<DungeonPresentationEvent> Events { get; }
        public bool RequiresBattle =>
            LegacyEvent is ExplorationEvent.Encounter or ExplorationEvent.BossEncounter;
    }

    internal static class DungeonPresentationMapper
    {
        public static IReadOnlyList<DungeonPresentationEvent> MapTransitionEvents(
            IEnumerable<RuntimeDungeonEvent> events,
            DungeonFloorResult? floor = null)
        {
            var mapped = new List<DungeonPresentationEvent>();
            foreach (RuntimeDungeonEvent runtimeEvent in events)
            {
                mapped.Add(Map(runtimeEvent, floor));
            }

            return new ReadOnlyCollection<DungeonPresentationEvent>(mapped);
        }

        public static IReadOnlyList<DungeonPresentationEvent> MapFloorEntry(DungeonFloorResult floor)
        {
            ArgumentNullException.ThrowIfNull(floor);

            return floor.Type switch
            {
                DungeonEventType.SafeRoom => new[]
                {
                    Map(new RuntimeDungeonEvent(RuntimeDungeonEventKind.SafeRoom, floor: floor.FloorNumber), floor)
                },
                DungeonEventType.Battle => new[]
                {
                    Map(new RuntimeDungeonEvent(RuntimeDungeonEventKind.EncounterRequested, floor: floor.FloorNumber), floor)
                },
                DungeonEventType.Boss => new[]
                {
                    Map(new RuntimeDungeonEvent(RuntimeDungeonEventKind.BossRequested, floor: floor.FloorNumber), floor)
                },
                DungeonEventType.BlockEnd => new[]
                {
                    Map(new RuntimeDungeonEvent(RuntimeDungeonEventKind.BarrierBlocked, floor: floor.FloorNumber), floor)
                },
                _ => Array.Empty<DungeonPresentationEvent>()
            };
        }

        public static IReadOnlyList<DungeonPresentationEvent> VisibleOnly(
            IEnumerable<DungeonPresentationEvent> events,
            params RuntimeDungeonEventKind[] eventKinds)
        {
            HashSet<RuntimeDungeonEventKind> allowed = eventKinds.ToHashSet();
            return new ReadOnlyCollection<DungeonPresentationEvent>(
                events.Where(ev => ev.Kind == DungeonPresentationResultKind.Shown &&
                                   allowed.Contains(ev.EventKind))
                    .ToList());
        }

        private static DungeonPresentationEvent Map(RuntimeDungeonEvent runtimeEvent, DungeonFloorResult? floor)
        {
            int? eventFloor = runtimeEvent.Floor ?? floor?.FloorNumber;
            return runtimeEvent.Kind switch
            {
                RuntimeDungeonEventKind.Movement when string.Equals(runtimeEvent.Message, "ascend", StringComparison.Ordinal) =>
                    new(runtimeEvent.Kind, DungeonPresentationResultKind.Shown, "Ascending...", ConsoleColor.White, 500, Floor: eventFloor),

                RuntimeDungeonEventKind.Movement when string.Equals(runtimeEvent.Message, "descend", StringComparison.Ordinal) =>
                    new(runtimeEvent.Kind, DungeonPresentationResultKind.Shown, "Descending...", ConsoleColor.White, 500, Floor: eventFloor),

                RuntimeDungeonEventKind.SafeRoom when eventFloor != 1 =>
                    new(runtimeEvent.Kind, DungeonPresentationResultKind.Shown, "The air here is calm.", ConsoleColor.Green, 800, Floor: eventFloor),

                RuntimeDungeonEventKind.BossRequested =>
                    new(runtimeEvent.Kind, DungeonPresentationResultKind.Shown, "!!! POWERFUL SHADOW DETECTED !!!", ConsoleColor.Red, 1000, Floor: eventFloor),

                RuntimeDungeonEventKind.BarrierBlocked =>
                    new(runtimeEvent.Kind, DungeonPresentationResultKind.Shown, "The path is sealed.", ConsoleColor.Gray, 1000, Floor: eventFloor),

                RuntimeDungeonEventKind.BossDefeated =>
                    new(runtimeEvent.Kind, DungeonPresentationResultKind.Shown, "The Guardian has been defeated!", ConsoleColor.Cyan, 1500, Floor: eventFloor),

                _ => new(runtimeEvent.Kind, DungeonPresentationResultKind.Suppressed, Floor: eventFloor)
            };
        }
    }
}
