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
using JRPGPrototype.Logic.Field.State;

namespace JRPGPrototype.Logic.Field.Bridges
{
    public enum StatusHubCommand
    {
        Back,
        AllocateStats,
        ChangeEquipment,
        PersonaStock,
        DemonStock
    }

    public enum EquipmentSlotMenuCommand
    {
        Back,
        Weapon,
        Armor,
        Boots,
        Accessory
    }

    /// <summary>
    /// Specialized UI Bridge for Status screens and Persona management.
    /// Authority for stat allocation menu rendering and confirmation dialogs.
    /// </summary>
    public class StatusUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly PartyManager _party;

        public StatusUIBridge(IGameIO io, FieldUIState uiState, PartyManager party)
        {
            _io = io;
            _uiState = uiState;
            _party = party;
        }

        #region Status Hub

        /// <summary>
        /// Renders the primary Status Hub. 
        /// Displays current human stats and provides access to specialized stocks based on class.
        /// </summary>
        public string ShowStatusHub(Combatant player)
        {
            return ShowStatusHubCommand(player) switch
            {
                StatusHubCommand.AllocateStats => "Allocate Stats",
                StatusHubCommand.ChangeEquipment => "Change Equipment",
                StatusHubCommand.PersonaStock => "Persona Stock",
                StatusHubCommand.DemonStock => "Demon Stock",
                _ => "Back"
            };
        }

        public StatusHubCommand ShowStatusHubCommand(Combatant player)
        {
            string header = RenderHumanStatusToString(player) + $"\nPoints Available: {player.StatPoints}";

            var options = new List<HostCommandOption<ConsoleMenuSelection<StatusHubCommand>>>
            {
                Option(StatusHubCommand.AllocateStats, "Allocate Stats", 0),
                Option(StatusHubCommand.ChangeEquipment, "Change Equipment", 1)
            };

            // Class-specific menu augmentation
            int nextIndex = options.Count;
            if (player.Class == ClassType.WildCard || player.Class == ClassType.PersonaUser)
            {
                options.Add(Option(StatusHubCommand.PersonaStock, "Persona Stock", nextIndex++));
            }

            if (player.Class == ClassType.Operator)
            {
                options.Add(Option(StatusHubCommand.DemonStock, "Demon Stock", nextIndex++));
            }

            options.Add(Option(StatusHubCommand.Back, "Back", nextIndex));

            HostCommandReadResult<ConsoleMenuSelection<StatusHubCommand>> result =
                ConsoleHostCommandReader.Read(_io, header, options, _uiState.StatusHubIndex);
            ConsoleMenuSelection<StatusHubCommand>? selection = result.Command;
            if (!result.IsSelected || selection is null || selection.Value.Command == StatusHubCommand.Back)
            {
                return StatusHubCommand.Back;
            }

            _uiState.StatusHubIndex = selection.Value.Index;
            return selection.Value.Command;
        }

        // Renders the equipment slot selection menu.
        public string ShowEquipSlotMenu(Combatant player)
        {
            EquipmentSlotMenuSelection selection = ShowEquipSlotCommand(player);
            return selection.IsBack ? "Back" : selection.Label;
        }

        public EquipmentSlotMenuSelection ShowEquipSlotCommand(Combatant player)
        {
            string header = "=== EQUIPMENT SLOTS ===";
            var options = new List<HostCommandOption<EquipmentSlotMenuSelection>>
            {
                EquipOption(EquipmentSlotMenuCommand.Weapon, LegacyStatusPresentationProjection.EquipmentSlotLabel(EquipmentSlotMenuCommand.Weapon, ResolveName(player.EquippedWeapon?.Id, player.EquippedWeapon?.Name)), 0),
                EquipOption(EquipmentSlotMenuCommand.Armor, LegacyStatusPresentationProjection.EquipmentSlotLabel(EquipmentSlotMenuCommand.Armor, ResolveName(player.EquippedArmor?.Id, player.EquippedArmor?.Name)), 1),
                EquipOption(EquipmentSlotMenuCommand.Boots, LegacyStatusPresentationProjection.EquipmentSlotLabel(EquipmentSlotMenuCommand.Boots, ResolveName(player.EquippedBoots?.Id, player.EquippedBoots?.Name)), 2),
                EquipOption(EquipmentSlotMenuCommand.Accessory, LegacyStatusPresentationProjection.EquipmentSlotLabel(EquipmentSlotMenuCommand.Accessory, ResolveName(player.EquippedAccessory?.Id, player.EquippedAccessory?.Name)), 3),
                EquipOption(EquipmentSlotMenuCommand.Back, "Back", 4)
            };

            HostCommandReadResult<EquipmentSlotMenuSelection> result =
                ConsoleHostCommandReader.Read(_io, header, options, _uiState.EquipSlotIndex);
            EquipmentSlotMenuSelection? selection = result.Command;
            if (!result.IsSelected || selection is null || selection.IsBack)
            {
                return new EquipmentSlotMenuSelection(EquipmentSlotMenuCommand.Back, "Back", 4);
            }

            _uiState.EquipSlotIndex = selection.Index;
            return selection;
        }

        // Attempts to get the name from the object, falls back to the Shop Registry if blank.
        private string ResolveName(string? id, string? existingName)
        {
            if (string.IsNullOrEmpty(id)) return "None";
            if (!string.IsNullOrEmpty(existingName)) return existingName;

            // Absolute Source of Truth Fallback
            var metadata = Database.ShopInventory.FirstOrDefault(x => x.Id == id);
            return metadata?.Name ?? id;
        }

        #endregion

        #region Stat Allocation UI

        /// <summary>
        /// UI authority for stat allocation. Logic-less rendering.
        /// </summary>
        public StatType? PromptStatAllocation(Combatant player)
        {
            List<string> options = new List<string>();
            var stats = Enum.GetValues(typeof(StatType)).Cast<StatType>().ToList();

            foreach (StatType s in stats)
            {
                options.Add($"{s,-5}: {player.CharacterStats.GetValueOrDefault(s, 0)}");
            }
            options.Add("Back");

            // Ensure index is within bounds
            if (_uiState.StatAllocationIndex >= options.Count) _uiState.StatAllocationIndex = 0;

            int idx = _io.RenderMenu($"=== STAT ALLOCATION (Pts: {player.StatPoints}) ===", options, _uiState.StatAllocationIndex, null, (index) =>
            {
                if (index >= 0 && index < stats.Count)
                {
                    StatType s = stats[index];
                    // UPDATED: Description mapping to new StatType keys
                    string bonus = s switch
                    {
                        StatType.Vi => "Increases Max HP by 5",
                        StatType.St => "Increases Physical Damage",
                        StatType.Ma => "Increases Magic Damage and +3 Max SP",
                        StatType.Ag => "Increases Hit/Accuracy and Evasion Chance",
                        StatType.Lu => "General Purpose Stat affecting Chances and Shop Prices",
                        _ => ""
                    };
                    _io.WriteLine($"Highlight: {s} | Bonus: {bonus}");
                }
            });

            // If user cancels or hits back, reset the index for next time they enter the menu and return null
            if (idx == -1 || idx == options.Count - 1)
            {
                _uiState.StatAllocationIndex = 0;
                return null;
            }

            // Save the current index so assignment doesn't jump the cursor
            _uiState.StatAllocationIndex = idx;
            return stats[idx];
        }

        /// <summary>
        /// Confirmation Window For Stat Alloc
        /// Displays the differential between original and newly allocated stats.
        /// Implemented using the onHighlight callback to prevent text clearing.
        /// Displays the new stats with altered stats in Yellow.
        /// </summary>
        public bool ShowStatConfirmation(Combatant player, Dictionary<StatType, int> initialStats)
        {
            List<string> options = new List<string> { "Yes, Apply Changes", "No, Revert Changes" };

            // We use the onHighlight action to draw our comparison window below the menu prompt.
            // This ensures it survives the screen clear.
            int choice = _io.RenderMenu("STAT ALLOCATION COMPLETE", options, 0, null, (idx) =>
            {
                _io.WriteLine("\n=== REVIEW CHANGES ===");

                foreach (StatType st in Enum.GetValues(typeof(StatType)))
                {
                    int current = player.CharacterStats.GetValueOrDefault(st, 0);
                    int original = initialStats.GetValueOrDefault(st, 0);

                    if (current > original)
                    {
                        // Highlight altered stats in Yellow
                        _io.Write($"{st,-5}: {current}", ConsoleColor.Yellow);
                        _io.WriteLine($" (+{current - original})", ConsoleColor.Yellow);
                    }
                    else
                    {
                        // Unaltered stats in default color
                        _io.WriteLine($"{st,-5}: {current}");
                    }
                }
                _io.WriteLine("=======================");
            });

            return choice == 0;
        }

        #endregion

        #region Persona Stock

        public Persona SelectPersonaFromStock(Combatant player)
            => SelectPersonaFromStockResult(player).Persona!;

        public PersonaStockSelectionResult SelectPersonaFromStockResult(Combatant player)
        {
            var allPersonas = new List<Persona>();
            if (player.ActivePersona != null) allPersonas.Add(player.ActivePersona);
            allPersonas.AddRange(player.PersonaStock);

            if (allPersonas.Count == 0)
            {
                _io.WriteLine("No Personas available.", ConsoleColor.Red);
                _io.Wait(800);
                return PersonaStockSelectionResult.Unavailable;
            }

            int lastIdx = 0;
            while (true)
            {
                List<string> options = allPersonas
                    .Select(p => LegacyPersonaStatusProjection.FromPersona(p).StockLabel(p == player.ActivePersona))
                    .ToList();
                options.Add("Back");

                int idx = _io.RenderMenu("=== PERSONA STOCK ===", options, lastIdx, null, null, true);

                if (idx == -1 || idx == options.Count - 1) return PersonaStockSelectionResult.Back;

                // Handle Status Peek
                if (idx <= -10)
                {
                    int inspectIdx = Math.Abs(idx) - 10;
                    ShowEntityStatus(allPersonas[inspectIdx]);
                    lastIdx = inspectIdx;
                    continue;
                }

                return PersonaStockSelectionResult.Selected(allPersonas[idx]);
            }
        }

        /// <summary>
        /// Renders the detailed stat sheet for a specific Persona.
        /// </summary>
        public string ShowPersonaDetails(Persona p, bool isEquipped)
            => ShowPersonaDetailsResult(p, isEquipped).Kind == PersonaStockActionKind.Equip
                ? "Equip Persona"
                : "Back";

        public PersonaStockActionResult ShowPersonaDetailsResult(Persona p, bool isEquipped)
        {
            string header = RenderPersonaDetailsToString(p, isEquipped);
            List<string> options = new List<string>();

            if (!isEquipped) options.Add("Equip Persona");
            options.Add("Back");

            int choice = _io.RenderMenu(header, options, 0);
            if (choice == -1 || choice == options.Count - 1) return PersonaStockActionResult.Back;

            return PersonaStockActionResult.Equip;
        }

        #endregion

        #region Demon Stock and COMP

        /// <summary>
        /// Renders the list of Demons in the party and stock.
        /// Marks their current location (Field vs Stock).
        /// </summary>
        public Combatant SelectDemonFromStock(Combatant player)
            => SelectDemonFromStockResult(player).Demon!;

        public DemonStockSelectionResult SelectDemonFromStockResult(Combatant player)
        {
            var allDemons = _party.ActiveParty.Where(m => m.Class == ClassType.Demon).ToList();
            allDemons.AddRange(player.DemonStock);

            if (allDemons.Count == 0)
            {
                _io.WriteLine("No demons found.", ConsoleColor.Red);
                _io.Wait(800);
                return DemonStockSelectionResult.Unavailable;
            }

            int lastIdx = 0;
            while (true)
            {
                List<string> options = allDemons
                    .Select(d => LegacyStatusPresentationProjection.FromCombatant(d).DemonStockLabel(_party.ActiveParty.Contains(d)))
                    .ToList();
                options.Add("Back");

                int idx = _io.RenderMenu("=== DEMON OVERVIEW ===", options, lastIdx, null, null, true);

                if (idx == -1 || idx == options.Count - 1) return DemonStockSelectionResult.Back;

                // Handle Status Peek
                if (idx <= -10)
                {
                    int inspectIdx = Math.Abs(idx) - 10;
                    ShowEntityStatus(allDemons[inspectIdx]);
                    lastIdx = inspectIdx;
                    continue;
                }

                return DemonStockSelectionResult.Selected(allDemons[idx]);
            }
        }

        /// <summary>
        /// Renders the Organize Party screen with 4 fixed slots.
        /// Hero Guardrail - Selecting Slot 1 triggers Status Peek.
        /// Status Screens available to view in Organize Party Menu.
        /// </summary>
        public int ShowOrganizationSlots()
        {
            OrganizationSlotSelectionResult result = ShowOrganizationSlotsResult();
            return result.Kind == PartyStockSelectionResultKind.Selected ? result.SlotIndex : -1;
        }

        public OrganizationSlotSelectionResult ShowOrganizationSlotsResult()
        {
            int lastIdx = 0;
            while (true)
            {
                string header = "=== ORGANIZE PARTY ===\nSelect a slot to manage:";
                List<string> options = new List<string>();

                for (int i = 0; i < 4; i++)
                {
                    if (i < _party.ActiveParty.Count)
                    {
                        var member = _party.ActiveParty[i];
                        options.Add(LegacyStatusPresentationProjection.FromCombatant(member).OrganizationSlotLabel(i));
                    }
                    else
                    {
                        options.Add(LegacyStatusPresentationProjection.EmptyOrganizationSlotLabel(i));
                    }
                }
                options.Add("Back");

                // supportStatusInspect: true
                int choice = _io.RenderMenu(header, options, lastIdx, null, null, true);

                if (choice == -1 || choice == options.Count - 1) return OrganizationSlotSelectionResult.Back;

                // Handle Peek Logic
                if (choice <= -10)
                {
                    int inspectIdx = Math.Abs(choice) - 10;
                    if (inspectIdx < _party.ActiveParty.Count)
                    {
                        ShowEntityStatus(_party.ActiveParty[inspectIdx]);
                    }
                    lastIdx = inspectIdx;
                    continue;
                }

                // Hero Guardrail - Selection triggers status instead of management
                if (choice == 0)
                {
                    ShowEntityStatus(_party.ActiveParty[0]);
                    lastIdx = 0;
                    continue;
                }

                return OrganizationSlotSelectionResult.Selected(choice);
            }
        }

        /// <summary>
        /// UI for summoning a demon from the Master Stock into a specific party slot.
        /// Status Screens available in Summon Menu.
        /// </summary>
        public object SelectSummonTarget(Combatant player, Combatant? occupantBeingReplaced)
        {
            SummonTargetSelectionResult result = SelectSummonTargetResult(player, occupantBeingReplaced);
            return result.Kind switch
            {
                SummonTargetSelectionKind.ReturnToComp => "RETURN_SIGNAL",
                SummonTargetSelectionKind.SelectedDemon => result.Demon!,
                _ => null!
            };
        }

        public SummonTargetSelectionResult SelectSummonTargetResult(Combatant player, Combatant? occupantBeingReplaced)
        {
            var masterStock = player.DemonStock;

            if (!masterStock.Any())
            {
                _io.WriteLine("No demons in stock.", ConsoleColor.Red);
                _io.Wait(800);
                return SummonTargetSelectionResult.Unavailable;
            }

            int lastIdx = 0;
            while (true)
            {
                List<string> options = new List<string>();
                List<bool> disabledList = new List<bool>();
                List<SummonTargetSelectionResult> mapping = new List<SummonTargetSelectionResult>();

                // 1. Optional Return Entry
                if (occupantBeingReplaced != null)
                {
                    options.Add(LegacyStatusPresentationProjection.ReturnToCompLabel(occupantBeingReplaced));
                    disabledList.Add(false);
                    mapping.Add(SummonTargetSelectionResult.ReturnToComp);
                }

                // 2. Master Stock List
                foreach (var d in masterStock)
                {
                    bool inParty = _party.ActiveParty.Contains(d);
                    options.Add(LegacyStatusPresentationProjection.FromCombatant(d).SummonTargetLabel(inParty));
                    disabledList.Add(inParty || d.IsDead);
                    mapping.Add(SummonTargetSelectionResult.SelectedDemon(d));
                }

                options.Add("Cancel");
                disabledList.Add(false);

                // supportStatusInspect: true
                int choice = _io.RenderMenu("=== SUMMON / REPLACE ===", options, lastIdx, disabledList, null, true);

                if (choice == -1 || choice == options.Count - 1) return SummonTargetSelectionResult.Back;

                // Handle Peek Logic
                if (choice <= -10)
                {
                    int inspectIdx = Math.Abs(choice) - 10;
                    if (mapping[inspectIdx].Demon is Combatant d)
                    {
                        ShowEntityStatus(d);
                    }
                    lastIdx = inspectIdx;
                    continue;
                }

                return mapping[choice];
            }
        }

        #endregion

        #region Helper Renderers

        /// <summary>
        /// Unified Entity Inspector
        /// Re-used for both Persona masks and Demon combatants.
        /// </summary>
        private void ShowEntityStatus(object entity)
        {
            _io.Clear();
            string statusString = entity is Combatant c ? RenderDemonDetailsToString(c) : RenderPersonaDetailsToString((Persona)entity, false);

            _io.WriteLine(statusString);
            _io.WriteLine("\n--------------------------------------------------");
            _io.WriteLine("Press any key to return...", ConsoleColor.Gray);
            _io.ReadKey();
        }

        /// <summary>
        /// Displays detailed stat sheet for a demon Combatant.
        /// </summary>
        public void ShowDemonDetails(Combatant demon)
        {
            string header = RenderDemonDetailsToString(demon);
            List<string> options = new List<string> { "Back" };
            _io.RenderMenu(header, options, 0);
        }

        public string RenderHumanStatusToString(Combatant entity)
            => LegacyStatusPresentationProjection.FromCombatant(entity).RenderHumanStatus();

        public string RenderPersonaDetailsToString(Persona persona, bool isEquipped)
            => LegacyPersonaStatusProjection.FromPersona(persona).RenderDetails(isEquipped);

        public string RenderDemonDetailsToString(Combatant demon)
            => LegacyStatusPresentationProjection.FromCombatant(demon).RenderDemonDetails();

        #endregion

        private static HostCommandOption<ConsoleMenuSelection<TCommand>> Option<TCommand>(
            TCommand command,
            string label,
            int index) =>
            new(new ConsoleMenuSelection<TCommand>(command, index), label);

        private static HostCommandOption<EquipmentSlotMenuSelection> EquipOption(
            EquipmentSlotMenuCommand command,
            string label,
            int index) =>
            new(new EquipmentSlotMenuSelection(command, label, index), label);
    }

    public sealed record EquipmentSlotMenuSelection(EquipmentSlotMenuCommand Command, string Label, int Index)
    {
        public bool IsBack => Command == EquipmentSlotMenuCommand.Back;
    }
}
