using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Field.Engines;
using JRPGPrototype.Logic.Field.Messaging;
using JRPGPrototype.Logic.Field.Bridges;
using JRPGPrototype.Logic.Field.State;
using JRPGPrototype.Logic.Fusion;

namespace JRPGPrototype.Logic.Field
{
    /// <summary>
    /// The Root Orchestrator for the Field Sub-System.
    /// Manages the high-level state transitions between City, Dungeon, and Menus.
    /// Coordinates the specialized Bridges, Logic Engines, and Messaging Infrastructure.
    /// </summary>
    public class FieldConductor
    {
        // Infrastructure
        private readonly IGameIO _io;
        private readonly Combatant _player;
        private readonly InventoryManager _inventory;
        private readonly EconomyManager _economy;
        private readonly DungeonState _dungeonState;
        private readonly DungeonManager _dungeonManager;
        private readonly PartyManager _partyManager;
        private readonly BattleKnowledge _playerKnowledge;
        private readonly CompendiumRegistry _compendium;

        // Messaging and Observation
        private readonly IFieldMessenger _messenger;
        private readonly FieldLogger _logger;

        // Sub-Sub-System Components
        private readonly FieldUIState _uiState;
        private readonly ServiceUIBridge _serviceUI;
        private readonly DungeonUIBridge _dungeonUI;
        private readonly StatusUIBridge _statusUI;
        private readonly InventoryUIBridge _inventoryUI;
        private readonly FieldServiceEngine _logicEngine;
        private readonly ExplorationProcessor _explorationProcessor;

        // Specialized Sub-Systems
        private readonly FusionConductor _fusionConductor;

        public FieldConductor(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            DungeonState dungeonState,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium)
        {
            _player = player;
            _inventory = inventory;
            _economy = economy;
            _dungeonState = dungeonState;
            _io = io;
            _playerKnowledge = playerKnowledge;
            _compendium = compendium;

            // 1. Initialize Shared UI State
            _uiState = new FieldUIState();

            // 2. Initialize Messaging Infrastructure (Mediator/Observer)
            _messenger = new FieldMessenger();
            _logger = new FieldLogger(_io, _messenger);

            // 3. Initialize Core Logic Managers
            _partyManager = new PartyManager(_player);
            _dungeonManager = new DungeonManager(_dungeonState);

            // 4. Initialize Logic Engines
            _logicEngine = new FieldServiceEngine(
                _messenger,
                _io, // so the internal ShopUIBridge can render menus.
                _economy,
                _inventory,
                _partyManager,
                _dungeonState);

            // 5. Initialize Specialized Bridges (Interactive UI)
            _serviceUI = new ServiceUIBridge(_io, _uiState, _economy, _partyManager);
            _dungeonUI = new DungeonUIBridge(_io, _uiState);
            _statusUI = new StatusUIBridge(_io, _uiState, _partyManager);
            _inventoryUI = new InventoryUIBridge(_io, _uiState, _inventory, _partyManager);

            // 6. Initialize Exploration Processor
            _explorationProcessor = new ExplorationProcessor(
                _messenger,
                _dungeonManager,
                _dungeonState,
                _dungeonUI,
                _logicEngine);

            // 7. Initialize Fusion Sub-System
            _fusionConductor = new FusionConductor(_io, _player, _partyManager, _economy, _uiState, _compendium);
        }

        /// <summary>
        /// The primary entry point for the Field Sub-System.
        /// Orchestrates the top-level loop between the City, Dungeon, and System menus.
        /// </summary>
        public void NavigateMenus()
        {
            while (true)
            {
                string choice = _serviceUI.ShowFieldMainMenu(_player);

                if (choice == "Cancel") continue;

                switch (choice)
                {
                    case "Explore Tartarus":
                        PrepareDungeonEntry();
                        break;

                    case "City Services":
                        OpenCityMenu();
                        break;

                    case "Inventory":
                        OpenInventoryMenu(inDungeon: false);
                        break;

                    case "Status":
                        OpenSeamlessStatusMenu();
                        break;

                    case "Organize Party":
                        OpenOrganizeMenu();
                        break;

                    case "Exit Game":
                        _logger.Deactivate(); // Cleanup event subscriptions
                        return;
                }

                // If the player somehow dies in the field (future-proofing for traps/DOT)
                if (_player.CurrentHP <= 0) break;
            }
        }

        #region Dungeon Traversal Logic

        private void PrepareDungeonEntry()
        {
            List<int> terminals = _dungeonManager.GetUnlockedTerminals();

            // If only the entrance is unlocked, skip the warp menu
            if (terminals.Count <= 1)
            {
                _dungeonManager.WarpToFloor(1);
                ExploreDungeon();
                return;
            }

            int? selectedFloor = _dungeonUI.SelectEntryPoint(terminals);
            if (selectedFloor.HasValue)
            {
                _dungeonManager.WarpToFloor(selectedFloor.Value);
                ExploreDungeon();
            }
        }

        private void ExploreDungeon()
        {
            // Initial trigger for the floor we just arrived on
            HandleFloorChange(_dungeonManager.ProcessCurrentFloor());

            while (_player.CurrentHP > 0)
            {
                DungeonFloorResult floorInfo = _dungeonManager.ProcessCurrentFloor();
                string action = _dungeonUI.ShowFloorActionMenu(floorInfo, _player);

                if (action == "Cancel") continue;

                bool exitLoop = false;

                switch (action)
                {
                    case "Ascend Stairs":
                        HandleFloorChange(_explorationProcessor.PerformAscension());
                        break;

                    case "Descend Stairs":
                        HandleFloorChange(_explorationProcessor.PerformDescension());
                        break;

                    case "Clock (Heal)":
                        OpenHospitalMenu();
                        break;

                    case "Terminal (Warp)":
                    case "Access Terminal (Return)":
                        int? destination = _dungeonUI.SelectWarpDestination(_dungeonManager.GetUnlockedTerminals(), floorInfo.FloorNumber);
                        if (destination.HasValue)
                        {
                            HandleFloorChange(_explorationProcessor.PerformWarp(destination.Value));
                        }
                        break;

                    case "Inventory":
                        if (OpenInventoryMenu(inDungeon: true) == ItemUsageResult.RequestDungeonExit)
                        {
                            _dungeonManager.RequestDungeonExit();
                            exitLoop = true;
                        }
                        break;

                    case "Status":
                        OpenSeamlessStatusMenu();
                        break;

                    case "Organize Party":
                        OpenOrganizeMenu();
                        break;

                    case "Return to City":
                        _dungeonManager.ReturnToCity();
                        exitLoop = true;
                        break;

                    case "Barrier (Cannot Pass)":
                        _dungeonUI.ReportBarrierBlocked();
                        break;
                }

                if (exitLoop || _player.CurrentHP <= 0) return;
            }
        }

        private void HandleFloorChange(DungeonFloorResult floorInfo)
        {
            ExplorationEvent result = _explorationProcessor.ProcessFloorEntry(floorInfo);

            if (result == ExplorationEvent.Encounter || result == ExplorationEvent.BossEncounter)
            {
                bool isBoss = (result == ExplorationEvent.BossEncounter);
                List<Combatant> enemies = _explorationProcessor.PrepareEncounter(floorInfo.EnemyIds);

                // Transition to Battle Sub-System
                BattleConductor battle = new BattleConductor(
                    _partyManager,
                    enemies,
                    _inventory,
                    _economy,
                    _io,
                    _playerKnowledge,
                    _compendium,
                    isBoss);

                battle.StartBattle();

                // Post-Battle Logic
                if (isBoss && !enemies.Any(e => !e.IsDead))
                {
                    _dungeonUI.ReportBossDefeated();
                    _logicEngine.RegisterBossDefeat(floorInfo.EnemyIds.FirstOrDefault());
                }
            }
        }

        #endregion

        #region City Services Logic

        /// <summary>
        /// Manages interactions within the city.
        /// Feature: Now acts as the bridge to the Cathedral of Shadows.
        /// </summary>
        private void OpenCityMenu()
        {
            while (true)
            {
                string choice = _serviceUI.ShowCityServicesMenu();

                if (choice == "Back") return;

                switch (choice)
                {
                    case "Blacksmith (Weapons)":
                        _logicEngine.OpenShop(_player, ShopType.Weapon);
                        break;

                    case "Clothing Store (Armor/Boots)":
                        string clothingType = _io.RenderMenu("Clothing Store", new List<string> { "Armor", "Boots", "Back" }, 0) == 0 ? "Armor" : "Boots";
                        if (clothingType == "Armor") _logicEngine.OpenShop(_player, ShopType.Armor);
                        else _logicEngine.OpenShop(_player, ShopType.Boots);
                        break;

                    case "Jeweler (Accessories)":
                        _logicEngine.OpenShop(_player, ShopType.Accessory);
                        break;

                    case "Pharmacy (Items)":
                        _logicEngine.OpenShop(_player, ShopType.Item);
                        break;

                    case "Hospital (Heal)":
                        OpenHospitalMenu();
                        break;

                    case "Cathedral of Shadows":
                        _fusionConductor.EnterCathedral();
                        break;
                }
            }
        }

        private void OpenHospitalMenu()
        {
            while (true)
            {
                HospitalPatientSelectionResult selection = _serviceUI.SelectHospitalPatientResult(_player);
                if (selection.Kind != HospitalSelectionResultKind.Selected || selection.Patient is null) return;

                HospitalTreatmentPresentationResult treatment =
                    _logicEngine.TryRestoreCombatantDetailed(selection.Patient);
                if (treatment.Message is not null)
                {
                    _messenger.Publish(treatment.Message, treatment.Color, treatment.Delay);
                }
            }
        }

        #endregion

        #region System Menus (Inventory/Status)

        // Now returns ItemUsageResult to signal explicit dungeon exits to the Conductor.
        private ItemUsageResult OpenInventoryMenu(bool inDungeon)
        {
            while (true)
            {
                string choice = _inventoryUI.ShowInventorySubMenu(_player);
                if (choice == "Back") return ItemUsageResult.None;

                switch (choice)
                {
                    case "Use Item":
                        var res = ShowItemMenu(inDungeon);
                        if (res == ItemUsageResult.RequestDungeonExit) return res;
                        break;
                    case "Use Skill":
                        ShowSkillMenu();
                        break;
                    case "Equipment":
                        ShowEquipSlotMenu();
                        break;
                    case "Demons (COMP)":
                        OpenDemonStockMenu();
                        break;
                }
            }
        }

        private ItemUsageResult ShowItemMenu(bool inDungeon)
        {
            FieldItemSelectionResult itemResult = _inventoryUI.SelectItemResult(_player, inDungeon);
            if (itemResult.Kind != FieldSelectionResultKind.Selected || itemResult.Item == null)
            {
                return ItemUsageResult.None;
            }

            FieldTargetSelectionResult targetResult = _inventoryUI.SelectFieldTargetResult(_player, itemResult.Item.Name);
            if (targetResult.Kind != FieldSelectionResultKind.Selected || targetResult.Target == null)
            {
                return ItemUsageResult.None;
            }

            return _logicEngine.ExecuteItemUsageDetailed(itemResult.Item, _player, targetResult.Target).LegacyResult;
        }

        #endregion

        private void ShowSkillMenu()
        {
            FieldSkillPerformerSelectionResult performerResult = _inventoryUI.SelectSkillPerformerResult(_player);
            if (performerResult.Kind != FieldSelectionResultKind.Selected || performerResult.Performer == null)
            {
                return;
            }

            FieldSkillSelectionResult skillResult = _inventoryUI.SelectFieldSkillResult(performerResult.Performer);
            if (skillResult.Kind != FieldSelectionResultKind.Selected || skillResult.Skill == null)
            {
                return;
            }

            FieldTargetSelectionResult targetResult = _inventoryUI.SelectFieldTargetResult(_player, skillResult.Skill.Name);
            if (targetResult.Kind != FieldSelectionResultKind.Selected || targetResult.Target == null)
            {
                return;
            }

            _logicEngine.ExecuteSkillUsageDetailed(skillResult.Skill, performerResult.Performer, targetResult.Target);
        }

        private void OpenSeamlessStatusMenu()
        {
            while (true)
            {
                string choice = _statusUI.ShowStatusHub(_player);
                if (choice == "Back") return;

                switch (choice)
                {
                    case "Allocate Stats":
                        OpenStatAllocation();
                        break;
                    case "Change Equipment":
                        ShowEquipSlotMenu();
                        break;
                    case "Persona Stock":
                        OpenPersonaStockMenu();
                        break;
                    case "Demon Stock":
                        OpenDemonStockMenu();
                        break;
                }
            }
        }

        // Now passes initialStats to the confirmation bridge to show change visualization.
        private void OpenStatAllocation()
        {
            // Transactional Stat Allocation with Rollback support.
            // 1. Take snapshot of initial state
            int initialPoints = _player.StatPoints;
            var initialStats = new Dictionary<StatType, int>();
            foreach (StatType st in Enum.GetValues(typeof(StatType)))
            {
                initialStats[st] = _player.CharacterStats.GetValueOrDefault(st, 0);
            }

            // 2. Allocation Loop
            while (_player.StatPoints > 0)
            {
                StatType? selected = _statusUI.PromptStatAllocation(_player);
                if (selected == null) break; // User hit Back or Cancel
                _logicEngine.AllocateStatPoint(_player, selected.Value);
            }

            // 3. Finalization logic
            // Only prompt if points were actually spent
            if (_player.StatPoints < initialPoints)
            {
                // Pass the snapshot to the bridge for visual differential
                bool save = _statusUI.ShowStatConfirmation(_player, initialStats);
                if (!save)
                {
                    // Perform Rollback
                    _logicEngine.RollbackStats(_player, initialStats, initialPoints);
                    _messenger.Publish("Changes discarded.", ConsoleColor.Yellow, 800);
                }
                else
                {
                    _messenger.Publish("Stats permanently increased.", ConsoleColor.Green, 800);
                }
            }
        }

        private void ShowEquipSlotMenu()
        {
            while (true)
            {
                string slot = _statusUI.ShowEquipSlotMenu(_player);
                if (slot == "Back") return;

                ShopCategory category = slot switch
                {
                    string s when s.Contains("Weapon") => ShopCategory.Weapon,
                    string s when s.Contains("Armor") => ShopCategory.Armor,
                    string s when s.Contains("Boots") => ShopCategory.Boots,
                    _ => ShopCategory.Accessory
                };

                List<string> ids = category switch
                {
                    ShopCategory.Weapon => _inventory.OwnedWeapons,
                    ShopCategory.Armor => _inventory.OwnedArmor,
                    ShopCategory.Boots => _inventory.OwnedBoots,
                    _ => _inventory.OwnedAccessories
                };

                string selectedId = _serviceUI.SelectEquipmentFromInventory(_player, ids, category);
                if (selectedId != "Back")
                {
                    _logicEngine.PerformEquip(_player, selectedId, category);
                }
            }
        }

        private void OpenPersonaStockMenu()
        {
            while (true)
            {
                PersonaStockSelectionResult selection = _statusUI.SelectPersonaFromStockResult(_player);
                if (selection.Kind != PartyStockSelectionResultKind.Selected || selection.Persona is null) return;

                Persona selected = selection.Persona;
                bool isEquipped = selected == _player.ActivePersona;
                PersonaStockActionResult action = _statusUI.ShowPersonaDetailsResult(selected, isEquipped);

                if (action.Kind == PersonaStockActionKind.Equip)
                {
                    _logicEngine.PerformPersonaSwapDetailed(_player, selected);
                }
            }
        }

        private void OpenDemonStockMenu()
        {
            while (true)
            {
                DemonStockSelectionResult selection = _statusUI.SelectDemonFromStockResult(_player);
                if (selection.Kind != PartyStockSelectionResultKind.Selected || selection.Demon is null) return;

                _statusUI.ShowDemonDetails(selection.Demon);
            }
        }

        /// <summary>
        /// Refactored Organization Menu logic.
        /// Immediately opts for Summon/Replace target.
        /// Hero Guardrail - Handled by the bridge (peek-only).
        /// </summary>
        private void OpenOrganizeMenu()
        {
            while (true)
            {
                // Bridge returns Back or 1, 2, 3 for demon slots.
                // Slot 0 (Hero) peeks status and loops internally in the bridge.
                OrganizationSlotSelectionResult slotSelection = _statusUI.ShowOrganizationSlotsResult();
                if (slotSelection.Kind != PartyStockSelectionResultKind.Selected) return;

                // Identify if the slot is currently occupied
                Combatant? occupant = null;
                int slotIndex = slotSelection.SlotIndex;
                if (slotIndex < _partyManager.ActiveParty.Count)
                {
                    occupant = _partyManager.ActiveParty[slotIndex];
                }

                // Immediately open Summon/Replace target menu
                SummonTargetSelectionResult result = _statusUI.SelectSummonTargetResult(_player, occupant);

                if (result.Kind is SummonTargetSelectionKind.Back or SummonTargetSelectionKind.Unavailable) continue;

                // Logic Branching based on selection
                if (result.Kind == SummonTargetSelectionKind.ReturnToComp)
                {
                    if (occupant != null) _logicEngine.ReturnDemonDetailed(_player, occupant);
                }
                else if (result.Kind == SummonTargetSelectionKind.SelectedDemon && result.Demon is Combatant newDemon)
                {
                    if (occupant != null)
                    {
                        _logicEngine.SwapActiveDemonDetailed(_player, occupant, newDemon);
                    }
                    else
                    {
                        // Empty slot Summon
                        _logicEngine.SummonDemonDetailed(_player, newDemon);
                    }
                }
            }
        }
    }
}
