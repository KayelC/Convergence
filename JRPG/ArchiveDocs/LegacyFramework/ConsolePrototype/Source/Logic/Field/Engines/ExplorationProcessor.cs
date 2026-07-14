using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field.Engines
{
    /// <summary>
    /// The logic engine for dungeon traversal and environmental events.
    /// Handles floor transitions, procedural encounter generation, and grouping logic.
    /// </summary>
    public class ExplorationProcessor
    {
        private readonly IFieldMessenger _messenger;
        private readonly DungeonManager _dungeonManager;
        private readonly DungeonState _dungeonState;
        private readonly DungeonUIBridge _dungeonUI;
        private readonly FieldServiceEngine _serviceEngine;

        public ExplorationProcessor(
            IFieldMessenger messenger,
            DungeonManager dungeonManager,
            DungeonState dungeonState,
            DungeonUIBridge dungeonUI,
            FieldServiceEngine serviceEngine)
        {
            _messenger = messenger;
            _dungeonManager = dungeonManager;
            _dungeonState = dungeonState;
            _dungeonUI = dungeonUI;
            _serviceEngine = serviceEngine;
        }

        #region Navigation Logic

        /// <summary>
        /// Logic for moving to a higher floor.
        /// Automatically updates the MaxFloorReached flag via DungeonManager.
        /// </summary>
        public DungeonFloorResult PerformAscension()
        {
            return PerformAscensionDetailed().Floor!;
        }

        internal DungeonTransitionPresentationResult PerformAscensionDetailed()
        {
            DungeonTransitionPresentationResult result = _dungeonManager.AscendDetailed();
            IReadOnlyList<DungeonPresentationEvent> visibleEvents =
                result.Transition.Code == RuntimeDungeonTransitionCode.BarrierBlocked
                    ? DungeonPresentationMapper.VisibleOnly(result.Events, RuntimeDungeonEventKind.BarrierBlocked)
                    : DungeonPresentationMapper.VisibleOnly(result.Events, RuntimeDungeonEventKind.Movement);
            _dungeonUI.PublishPresentationEvents(visibleEvents);
            return result.Transition.Code == RuntimeDungeonTransitionCode.BarrierBlocked
                ? result
                : _dungeonManager.ProcessCurrentFloorDetailed();
        }

        /// <summary>
        /// Logic for moving to a lower floor.
        /// </summary>
        public DungeonFloorResult PerformDescension()
        {
            return PerformDescensionDetailed().Floor!;
        }

        internal DungeonTransitionPresentationResult PerformDescensionDetailed()
        {
            DungeonTransitionPresentationResult result = _dungeonManager.DescendDetailed();
            _dungeonUI.PublishPresentationEvents(
                DungeonPresentationMapper.VisibleOnly(result.Events, RuntimeDungeonEventKind.Movement));
            return _dungeonManager.ProcessCurrentFloorDetailed();
        }

        /// <summary>
        /// Handles the warp transaction via Terminal.
        /// </summary>
        public DungeonFloorResult PerformWarp(int floor)
        {
            return PerformWarpDetailed(floor).Floor!;
        }

        internal DungeonTransitionPresentationResult PerformWarpDetailed(int floor)
        {
            _messenger.Publish($"Warping to Floor {floor}...", delay: 1000);

            DungeonTransitionPresentationResult result = _dungeonManager.WarpToUnlockedFloorDetailed(floor);
            return result.LegacySuccess ? _dungeonManager.ProcessCurrentFloorDetailed() : result;
        }

        #endregion

        #region Floor Trigger Processing

        /// <summary>
        /// Evaluates a new floor entry and executes immediate environmental events.
        /// Logic: Handles terminal unlocks, safe-room announcements, and boss alerts.
        /// </summary>
        public ExplorationEvent ProcessFloorEntry(DungeonFloorResult floorInfo)
        {
            return ProcessFloorEntryDetailed(floorInfo).LegacyEvent;
        }

        internal DungeonFloorEntryPresentationResult ProcessFloorEntryDetailed(
            DungeonTransitionPresentationResult transition)
        {
            if (transition.Floor is null)
            {
                throw new InvalidOperationException("A floor transition is required before processing floor entry.");
            }

            return ProcessFloorEntryDetailed(
                transition.Floor,
                transition.Events);
        }

        internal DungeonFloorEntryPresentationResult ProcessFloorEntryDetailed(DungeonFloorResult floorInfo)
        {
            return ProcessFloorEntryDetailed(
                floorInfo,
                DungeonPresentationMapper.MapFloorEntry(floorInfo));
        }

        private DungeonFloorEntryPresentationResult ProcessFloorEntryDetailed(
            DungeonFloorResult floorInfo,
            IReadOnlyList<DungeonPresentationEvent> events)
        {
            // 1. Handle Persistent Terminal Unlocks
            if (floorInfo.HasTerminal)
            {
                _serviceEngine.UnlockTerminal(floorInfo.FloorNumber);
            }

            _dungeonUI.PublishPresentationEvents(
                DungeonPresentationMapper.VisibleOnly(
                    events,
                    RuntimeDungeonEventKind.SafeRoom,
                    RuntimeDungeonEventKind.BossRequested));

            ExplorationEvent legacyEvent = floorInfo.Type switch
            {
                DungeonEventType.Battle => ExplorationEvent.Encounter,
                DungeonEventType.Boss => ExplorationEvent.BossEncounter,
                _ => ExplorationEvent.None
            };

            return new DungeonFloorEntryPresentationResult(
                legacyEvent,
                floorInfo,
                floorInfo.EnemyIds,
                legacyEvent == ExplorationEvent.BossEncounter,
                events);
        }

        #endregion

        #region Encounter Preparation

        /// <summary>
        /// Translates raw Enemy IDs into a hydrated list of Combatants using the new factory.
        /// Feature: SMT Grouping Logic (Pixie A, Pixie B).
        /// </summary>
        public List<Combatant> PrepareEncounter(List<string> enemyIds)
        {
            List<Combatant> enemies = new List<Combatant>();

            // 1. Hydrate the combatants using the programmatic factory method in Combatant.cs
            foreach (string id in enemyIds)
            {
                enemies.Add(CombatantFactory.CreateEnemy(id));
            }

            // 2. High-Fidelity Naming Logic (Grouping)
            // Groups enemies by name and appends alphabetical suffixes if duplicates exist.
            var groups = enemies.GroupBy(e => e.Name);
            foreach (var group in groups)
            {
                if (group.Count() > 1)
                {
                    int counter = 0;
                    foreach (var enemy in group)
                    {
                        // Assign alphabetical suffix based on occurrence (A, B, C...)
                        enemy.Name += $" {(char)('A' + counter)}";
                        counter++;
                    }
                }
            }

            return enemies;
        }

        #endregion
    }
}
