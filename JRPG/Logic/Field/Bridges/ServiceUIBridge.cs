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
using JRPGPrototype.Logic.Field.State;

namespace JRPGPrototype.Logic.Field.Bridges
{
    public enum FieldMainMenuCommand
    {
        Cancel,
        ExploreTartarus,
        CityServices,
        Inventory,
        Status,
        OrganizeParty,
        ExitGame
    }

    public enum CityServicesCommand
    {
        Back,
        Weapons,
        Clothing,
        Accessories,
        Items,
        Hospital,
        Cathedral
    }

    /// <summary>
    /// Handles all UI interactions for City Services (Hospital and Shop) 
    /// and the primary Field Main Menu.
    /// </summary>
    public class ServiceUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly EconomyManager _economy;
        private readonly PartyManager _party;

        public ServiceUIBridge(IGameIO io, FieldUIState uiState, EconomyManager economy, PartyManager party)
        {
            _io = io;
            _uiState = uiState;
            _economy = economy;
            _party = party;
        }

        #region Main Navigation

        /// <summary>
        /// Renders the primary Field Menu.
        /// </summary>
        public string ShowFieldMainMenu(Combatant player)
        {
            return ShowFieldMainMenuCommand(player) switch
            {
                FieldMainMenuCommand.ExploreTartarus => "Explore Tartarus",
                FieldMainMenuCommand.CityServices => "City Services",
                FieldMainMenuCommand.Inventory => "Inventory",
                FieldMainMenuCommand.Status => "Status",
                FieldMainMenuCommand.OrganizeParty => "Organize Party",
                FieldMainMenuCommand.ExitGame => "Exit Game",
                _ => "Cancel"
            };
        }

        public FieldMainMenuCommand ShowFieldMainMenuCommand(Combatant player)
        {
            string header = $"=== FIELD MENU ===\n" +
                            $"Macca: {_economy.Macca}\n" +
                            $"HP: {player.CurrentHP}/{player.MaxHP} | SP: {player.CurrentSP}/{player.MaxSP}";

            var options = new List<HostCommandOption<ConsoleMenuSelection<FieldMainMenuCommand>>>
            {
                Option(FieldMainMenuCommand.ExploreTartarus, "Explore Tartarus", 0),
                Option(FieldMainMenuCommand.CityServices, "City Services", 1),
                Option(FieldMainMenuCommand.Inventory, "Inventory", 2),
                Option(FieldMainMenuCommand.Status, "Status", 3)
            };

            int nextIndex = options.Count;
            if (player.Class == ClassType.Operator)
            {
                options.Add(Option(FieldMainMenuCommand.OrganizeParty, "Organize Party", nextIndex++));
            }
            options.Add(Option(FieldMainMenuCommand.ExitGame, "Exit Game", nextIndex));

            HostCommandReadResult<ConsoleMenuSelection<FieldMainMenuCommand>> result =
                ConsoleHostCommandReader.Read(_io, header, options, _uiState.MainMenuIndex);
            ConsoleMenuSelection<FieldMainMenuCommand>? selection = result.Command;
            if (!result.IsSelected || selection is null)
            {
                return FieldMainMenuCommand.Cancel;
            }

            _uiState.MainMenuIndex = selection.Value.Index;
            return selection.Value.Command;
        }

        #endregion

        #region Hospital UI

        /// <summary>
        /// Renders the medical treatment list.
        /// Feature: Sorts injured party/stock members to the top.
        /// </summary>
        public Combatant SelectHospitalPatient(Combatant player)
        {
            // Gather all possible patients: Player + Active Party + Stock
            var patients = new List<Combatant> { player };
            patients.AddRange(_party.ActiveParty.Where(p => p != player));
            patients.AddRange(player.DemonStock);

            // SMT III Requirement: Sort injured (HP/SP < Max) to the top for convenience
            var sortedPatients = patients
                .OrderByDescending(p => (p.CurrentHP < p.MaxHP || p.CurrentSP < p.MaxSP))
                .ToList();

            string header = $"=== HOSPITAL / CLOCK ===\n" +
                            $"Current Macca: {_economy.Macca}\n" +
                            $"Select a member to treat:";

            var options = new List<HostCommandOption<HospitalPatientSelection>>();

            for (int index = 0; index < sortedPatients.Count; index++)
            {
                Combatant p = sortedPatients[index];
                int hpMissing = p.MaxHP - p.CurrentHP;
                int spMissing = p.MaxSP - p.CurrentSP;
                int cost = (hpMissing * 1) + (spMissing * 5);

                bool isHealthy = (hpMissing <= 0 && spMissing <= 0);
                string costDisplay = isHealthy ? "[HEALTHY]" : $"{cost} M";
                string label = $"{p.Name,-15} | HP: {p.CurrentHP,3}/{p.MaxHP,3} SP: {p.CurrentSP,3}/{p.MaxSP,3} | {costDisplay}";

                options.Add(new HostCommandOption<HospitalPatientSelection>(
                    new HospitalPatientSelection(p, IsLeave: false, index),
                    label,
                    IsEnabled: !isHealthy));
            }

            options.Add(new HostCommandOption<HospitalPatientSelection>(
                new HospitalPatientSelection(null, IsLeave: true, sortedPatients.Count),
                "Leave"));

            // Resetting index to 0 for hospital as urgency sorting changes the list context
            HostCommandReadResult<HospitalPatientSelection> result =
                ConsoleHostCommandReader.Read(_io, header, options, 0);

            HospitalPatientSelection? selection = result.Command;
            if (!result.IsSelected || selection is null || selection.IsLeave) return null!;
            return selection.Patient!;
        }

        #endregion

        #region Shop and Service UI

        /// <summary>
        /// Renders the City Services portal menu.
        /// </summary>
        public string ShowCityServicesMenu()
        {
            return ShowCityServicesCommand() switch
            {
                CityServicesCommand.Weapons => "Blacksmith (Weapons)",
                CityServicesCommand.Clothing => "Clothing Store (Armor/Boots)",
                CityServicesCommand.Accessories => "Jeweler (Accessories)",
                CityServicesCommand.Items => "Pharmacy (Items)",
                CityServicesCommand.Hospital => "Hospital (Heal)",
                CityServicesCommand.Cathedral => "Cathedral of Shadows",
                _ => "Back"
            };
        }

        public CityServicesCommand ShowCityServicesCommand()
        {
            string header = $"=== CITY SERVICES ===\nMacca: {_economy.Macca}";
            var options = new List<HostCommandOption<ConsoleMenuSelection<CityServicesCommand>>>
            {
                Option(CityServicesCommand.Weapons, "Blacksmith (Weapons)", 0),
                Option(CityServicesCommand.Clothing, "Clothing Store (Armor/Boots)", 1),
                Option(CityServicesCommand.Accessories, "Jeweler (Accessories)", 2),
                Option(CityServicesCommand.Items, "Pharmacy (Items)", 3),
                Option(CityServicesCommand.Hospital, "Hospital (Heal)", 4),
                Option(CityServicesCommand.Cathedral, "Cathedral of Shadows", 5),
                Option(CityServicesCommand.Back, "Back", 6)
            };

            HostCommandReadResult<ConsoleMenuSelection<CityServicesCommand>> result =
                ConsoleHostCommandReader.Read(_io, header, options, _uiState.CityMenuIndex);
            ConsoleMenuSelection<CityServicesCommand>? selection = result.Command;
            if (!result.IsSelected || selection is null || selection.Value.Command == CityServicesCommand.Back)
            {
                return CityServicesCommand.Back;
            }

            _uiState.CityMenuIndex = selection.Value.Index;
            return selection.Value.Command;
        }

        /// Renders the specific list of equipment available for the player to equip.
        public string SelectEquipmentFromInventory(Combatant player, List<string> ids, ShopCategory category)
        {
            if (ids == null || ids.Count == 0)
            {
                _io.WriteLine($"No {category} available in inventory.");
                _io.Wait(800);
                return "Back";
            }

            List<string> names = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var id in ids)
            {
                string name = category switch
                {
                    ShopCategory.Weapon => Database.Weapons[id].Name,
                    ShopCategory.Armor => Database.Armors[id].Name,
                    ShopCategory.Boots => Database.Boots[id].Name,
                    _ => Database.Accessories[id].Name
                };

                bool equipped = category switch
                {
                    ShopCategory.Weapon => player.EquippedWeapon?.Id == id,
                    ShopCategory.Armor => player.EquippedArmor?.Id == id,
                    ShopCategory.Boots => player.EquippedBoots?.Id == id,
                    _ => player.EquippedAccessory?.Id == id
                };

                names.Add($"{name}{(equipped ? " [E]" : "")}");
                disabled.Add(equipped); // Cannot re-select currently equipped items
            }

            names.Add("Back");
            disabled.Add(false);

            int choice = _io.RenderMenu($"=== EQUIP {category.ToString().ToUpper()} ===", names, _uiState.EquipListIndex, disabled, (index) =>
            {
                if (index >= 0 && index < ids.Count)
                {
                    DisplayEquipmentStats(ids[index], category);
                }
            });

            if (choice == -1 || choice == names.Count - 1) return "Back";

            _uiState.EquipListIndex = choice;
            return ids[choice];
        }

        // Helper to display item stats during selection in the Equip menu.
        private void DisplayEquipmentStats(string id, ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Weapon:
                    var w = Database.Weapons[id];
                    _io.WriteLine($"Type: {w.Type} | Pow: {w.Power} Acc: {w.Accuracy}");
                    break;
                case ShopCategory.Armor:
                    var a = Database.Armors[id];
                    _io.WriteLine($"Def: {a.Defense} Eva: {a.Evasion} | {a.Description}");
                    break;
                case ShopCategory.Boots:
                    var b = Database.Boots[id];
                    _io.WriteLine($"Eva: {b.Evasion} | {b.Description}");
                    break;
                case ShopCategory.Accessory:
                    var acc = Database.Accessories[id];
                    _io.WriteLine($"Mod: {acc.ModifierStat} +{acc.ModifierValue} | {acc.Description}");
                    break;
            }
        }

        #endregion

        private static HostCommandOption<ConsoleMenuSelection<TCommand>> Option<TCommand>(
            TCommand command,
            string label,
            int index) =>
            new(new ConsoleMenuSelection<TCommand>(command, index), label);
    }

    internal sealed record HospitalPatientSelection(Combatant? Patient, bool IsLeave, int Index);
}
