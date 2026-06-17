using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Host;
using JRPGPrototype.Hosting;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.State;

namespace JRPGPrototype.Logic.Field.Bridges
{
    public enum DungeonFloorActionCommand
    {
        Cancel,
        AscendStairs,
        DescendStairs,
        Clock,
        TerminalWarp,
        TerminalReturn,
        Inventory,
        Status,
        OrganizeParty,
        ReturnToCity,
        Barrier
    }

    /// <summary>
    /// Specialized UI Bridge for Dungeon Exploration (Tartarus).
    /// Handles navigation prompts, floor selection, and environmental feedback.
    /// </summary>
    public class DungeonUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;

        public DungeonUIBridge(IGameIO io, FieldUIState uiState)
        {
            _io = io;
            _uiState = uiState;
        }

        #region Dungeon HUD and Main Actions

        /// <summary>
        /// Renders the main exploration HUD and returns the user's selected action.
        /// Logic: Conditionally adds options based on floor number, floor type, and terminal presence.
        /// </summary>
        public string ShowFloorActionMenu(DungeonFloorResult floorInfo, Combatant player)
        {
            return ShowFloorActionCommand(floorInfo, player) switch
            {
                DungeonFloorActionCommand.AscendStairs => "Ascend Stairs",
                DungeonFloorActionCommand.DescendStairs => "Descend Stairs",
                DungeonFloorActionCommand.Clock => "Clock (Heal)",
                DungeonFloorActionCommand.TerminalWarp => "Terminal (Warp)",
                DungeonFloorActionCommand.TerminalReturn => "Access Terminal (Return)",
                DungeonFloorActionCommand.Inventory => "Inventory",
                DungeonFloorActionCommand.Status => "Status",
                DungeonFloorActionCommand.OrganizeParty => "Organize Party",
                DungeonFloorActionCommand.ReturnToCity => "Return to City",
                DungeonFloorActionCommand.Barrier => "Barrier (Cannot Pass)",
                _ => "Cancel"
            };
        }

        public DungeonFloorActionCommand ShowFloorActionCommand(DungeonFloorResult floorInfo, Combatant player)
        {
            DungeonFloorActionSelectionResult result = ShowFloorActionResult(floorInfo, player);
            return result.Kind == DungeonPresentationResultKind.Selected
                ? result.Command
                : DungeonFloorActionCommand.Cancel;
        }

        internal DungeonFloorActionSelectionResult ShowFloorActionResult(DungeonFloorResult floorInfo, Combatant player)
        {
            string header = $"=== TARTARUS: {floorInfo.BlockName.ToUpper()} ===\n" +
                            $"Floor: {floorInfo.FloorNumber}\n" +
                            $"Info: {floorInfo.Description}\n" +
                            $"HP: {player.CurrentHP,3}/{player.MaxHP,3} | SP: {player.CurrentSP,3}/{player.MaxSP,3}";

            var options = new List<HostCommandOption<ConsoleMenuSelection<DungeonFloorActionCommand>>>();
            int index = 0;

            // 1. Navigation Logic
            if (floorInfo.Type != DungeonEventType.BlockEnd)
            {
                options.Add(Option(DungeonFloorActionCommand.AscendStairs, "Ascend Stairs", index++));
            }
            else
            {
                options.Add(Option(DungeonFloorActionCommand.Barrier, "Barrier (Cannot Pass)", index++));
            }

            if (floorInfo.FloorNumber > 1)
            {
                options.Add(Option(DungeonFloorActionCommand.DescendStairs, "Descend Stairs", index++));
            }

            // 2. Floor-Specific Features
            if (floorInfo.FloorNumber == 1)
            {
                options.Add(Option(DungeonFloorActionCommand.Clock, "Clock (Heal)", index++));
                options.Add(Option(DungeonFloorActionCommand.TerminalWarp, "Terminal (Warp)", index++));
                options.Add(Option(DungeonFloorActionCommand.ReturnToCity, "Return to City", index++));
            }
            else if (floorInfo.HasTerminal)
            {
                options.Add(Option(DungeonFloorActionCommand.TerminalReturn, "Access Terminal (Return)", index++));
            }

            // 3. Global Field Actions
            options.Add(Option(DungeonFloorActionCommand.Inventory, "Inventory", index++));
            options.Add(Option(DungeonFloorActionCommand.Status, "Status", index++));

            if (player.Class == ClassType.Operator)
            {
                options.Add(Option(DungeonFloorActionCommand.OrganizeParty, "Organize Party", index++));
            }

            // Ensure the cursor index doesn't exceed the newly built list size
            if (_uiState.DungeonMenuIndex >= options.Count) _uiState.DungeonMenuIndex = 0;

            HostCommandReadResult<ConsoleMenuSelection<DungeonFloorActionCommand>> result =
                ConsoleHostCommandReader.Read(_io, header, options, _uiState.DungeonMenuIndex);

            ConsoleMenuSelection<DungeonFloorActionCommand>? selection = result.Command;
            if (!result.IsSelected || selection is null) return DungeonFloorActionSelectionResult.Back;

            _uiState.DungeonMenuIndex = selection.Value.Index;
            return DungeonFloorActionSelectionResult.Selected(selection.Value.Command);
        }

        #endregion

        #region Entry Point and Warp UI

        /// <summary>
        /// Renders the menu to select a starting floor from unlocked terminals.
        /// Feature: Distinct labeling for the Lobby (Floor 1).
        /// </summary>
        public int? SelectEntryPoint(List<int> unlockedTerminals)
        {
            DungeonFloorSelectionResult result = SelectEntryPointResult(unlockedTerminals);
            return result.Kind == DungeonPresentationResultKind.Selected ? result.Floor : null;
        }

        internal DungeonFloorSelectionResult SelectEntryPointResult(List<int> unlockedTerminals)
        {
            var options = new List<HostCommandOption<DungeonFloorSelection>>();
            for (int index = 0; index < unlockedTerminals.Count; index++)
            {
                int floor = unlockedTerminals[index];
                options.Add(new HostCommandOption<DungeonFloorSelection>(
                    new DungeonFloorSelection(floor, IsCancel: false, index),
                    floor == 1 ? "Lobby (Entrance)" : $"Floor {floor}"));
            }
            options.Add(new HostCommandOption<DungeonFloorSelection>(
                new DungeonFloorSelection(null, IsCancel: true, unlockedTerminals.Count),
                "Cancel"));

            HostCommandReadResult<DungeonFloorSelection> result =
                ConsoleHostCommandReader.Read(_io, "=== SELECT ENTRY POINT ===", options, 0);

            DungeonFloorSelection? selection = result.Command;
            if (!result.IsSelected || selection is null || selection.IsCancel) return DungeonFloorSelectionResult.Back;

            return selection.Floor.HasValue
                ? DungeonFloorSelectionResult.Selected(selection.Floor.Value)
                : DungeonFloorSelectionResult.Unavailable;
        }

        /// <summary>
        /// Specialized menu for the Terminal System (Warping).
        /// Identifies the current floor as a disabled option to prevent redundant warps.
        /// </summary>
        public int? SelectWarpDestination(List<int> unlockedTerminals, int currentFloor)
        {
            DungeonFloorSelectionResult result = SelectWarpDestinationResult(unlockedTerminals, currentFloor);
            return result.Kind == DungeonPresentationResultKind.Selected ? result.Floor : null;
        }

        internal DungeonFloorSelectionResult SelectWarpDestinationResult(List<int> unlockedTerminals, int currentFloor)
        {
            var options = new List<HostCommandOption<DungeonFloorSelection>>();
            for (int index = 0; index < unlockedTerminals.Count; index++)
            {
                int f = unlockedTerminals[index];
                string name = (f == 1) ? "Lobby" : $"Floor {f}";
                bool isCurrent = (f == currentFloor);

                options.Add(new HostCommandOption<DungeonFloorSelection>(
                    new DungeonFloorSelection(f, IsCancel: false, index),
                    isCurrent ? $"{name} (Current)" : name,
                    IsEnabled: !isCurrent));
            }

            options.Add(new HostCommandOption<DungeonFloorSelection>(
                new DungeonFloorSelection(null, IsCancel: true, unlockedTerminals.Count),
                "Cancel"));

            HostCommandReadResult<DungeonFloorSelection> result =
                ConsoleHostCommandReader.Read(_io, "=== TERMINAL SYSTEM ===", options, 0);

            DungeonFloorSelection? selection = result.Command;
            if (!result.IsSelected || selection is null || selection.IsCancel) return DungeonFloorSelectionResult.Back;

            return selection.Floor.HasValue
                ? DungeonFloorSelectionResult.Selected(selection.Floor.Value)
                : DungeonFloorSelectionResult.Unavailable;
        }

        #endregion

        #region Environmental Feedback

        /// <summary>
        /// Visual/Audio feedback for entering a SafeRoom.
        /// </summary>
        public void ReportSafeRoom()
        {
            PublishPresentationEvents(
                [new DungeonPresentationEvent(
                    JRPGPrototype.Logic.Runtime.RuntimeDungeonEventKind.SafeRoom,
                    DungeonPresentationResultKind.Shown,
                    "The air here is calm.",
                    ConsoleColor.Green,
                    800)]);
        }

        /// <summary>
        /// High-alert feedback for approaching a Boss room.
        /// </summary>
        public void ReportBossRoom()
        {
            PublishPresentationEvents(
                [new DungeonPresentationEvent(
                    JRPGPrototype.Logic.Runtime.RuntimeDungeonEventKind.BossRequested,
                    DungeonPresentationResultKind.Shown,
                    "!!! POWERFUL SHADOW DETECTED !!!",
                    ConsoleColor.Red,
                    1000)]);
        }

        /// <summary>
        /// Feedback for a successful escape or boss defeat.
        /// </summary>
        public void ReportBossDefeated()
        {
            PublishPresentationEvents(
                [new DungeonPresentationEvent(
                    JRPGPrototype.Logic.Runtime.RuntimeDungeonEventKind.BossDefeated,
                    DungeonPresentationResultKind.Shown,
                    "The Guardian has been defeated!",
                    ConsoleColor.Cyan,
                    1500)]);
        }

        /// <summary>
        /// Feedback for a navigation action.
        /// </summary>
        public void ReportMovement(bool ascending)
        {
            PublishPresentationEvents(
                [new DungeonPresentationEvent(
                    JRPGPrototype.Logic.Runtime.RuntimeDungeonEventKind.Movement,
                    DungeonPresentationResultKind.Shown,
                    ascending ? "Ascending..." : "Descending...",
                    ConsoleColor.White,
                    Delay: 500)]);
        }

        /// <summary>
        /// Feedback for being blocked by a block barrier.
        /// </summary>
        public void ReportBarrierBlocked()
        {
            PublishPresentationEvents(
                [new DungeonPresentationEvent(
                    JRPGPrototype.Logic.Runtime.RuntimeDungeonEventKind.BarrierBlocked,
                    DungeonPresentationResultKind.Shown,
                    "The path is sealed.",
                    ConsoleColor.Gray,
                    1000)]);
        }

        #endregion

        internal IReadOnlyList<DungeonPresentationEvent> PublishPresentationEvents(
            IEnumerable<DungeonPresentationEvent> events)
        {
            var published = new List<DungeonPresentationEvent>();
            foreach (DungeonPresentationEvent presentationEvent in events)
            {
                if (presentationEvent.Kind != DungeonPresentationResultKind.Shown ||
                    string.IsNullOrEmpty(presentationEvent.Message))
                {
                    continue;
                }

                if (presentationEvent.ClearScreen)
                {
                    _io.Clear();
                }

                _io.WriteLine(presentationEvent.Message, presentationEvent.Color);

                if (presentationEvent.Delay > 0)
                {
                    _io.Wait(presentationEvent.Delay);
                }

                if (presentationEvent.WaitForInput)
                {
                    _io.ReadKey(intercept: true);
                }

                published.Add(presentationEvent);
            }

            return published;
        }

        private static HostCommandOption<ConsoleMenuSelection<TCommand>> Option<TCommand>(
            TCommand command,
            string label,
            int index) =>
            new(new ConsoleMenuSelection<TCommand>(command, index), label);
    }

    internal sealed record DungeonFloorSelection(int? Floor, bool IsCancel, int Index);
}
