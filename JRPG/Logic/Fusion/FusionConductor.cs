using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field.State;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The Root Orchestrator for the Fusion Sub-System.
    /// Manages the high-level sequences for Binary Fusion, Sacrificial Fusion, 
    /// Compendium registration, and Recall.
    /// Decoupled narration via IFusionMessenger and logic via FusionMutator strategies.
    /// </summary>
    public class FusionConductor
    {
        private readonly IGameIO _io;
        private readonly Combatant _player;
        private readonly PartyManager _partyManager;
        private readonly EconomyManager _economy;
        private readonly FieldUIState _uiState;

        // Rule collaborators: calculator predicts outcomes, mutator applies confirmed transactions.
        private readonly FusionCalculator _calculator;
        private readonly FusionMutator _mutator;
        private readonly CompendiumRegistry _compendium;
        private readonly CathedralUIBridge _uiBridge;
        private readonly FusionPlanFactory _planFactory;
        private readonly FusionPreviewFactory _previewFactory;
        private readonly FusionOwnershipRules _ownershipRules;

        // Fusion messages are published as domain events and rendered by the subscribed logger.
        private readonly IFusionMessenger _messenger;
        private readonly FusionLogger _logger;

        public FusionConductor(
            IGameIO io,
            Combatant player,
            PartyManager partyManager,
            EconomyManager economy,
            FieldUIState uiState,
            CompendiumRegistry compendium)
        {
            _io = io;
            _player = player;
            _partyManager = partyManager;
            _economy = economy;
            _uiState = uiState;
            _compendium = compendium;

            // Keep fusion narration behind the messenger so future hosts can subscribe their own presentation layer.
            _messenger = new FusionMessenger();
            _logger = new FusionLogger(_io);
            _logger.Subscribe(_messenger);

            // The conductor wires the current console bridge to reusable fusion rules; later adapters should replace the bridge, not the rules.
            _calculator = new FusionCalculator(_io, _messenger);
            _mutator = new FusionMutator(_partyManager, _economy, _messenger);
            _uiBridge = new CathedralUIBridge(_io, _uiState, _compendium);
            _planFactory = new FusionPlanFactory(_calculator);
            _previewFactory = new FusionPreviewFactory();
            _ownershipRules = new FusionOwnershipRules(_partyManager);
        }

        /// <summary>
        /// Public entry point for the Cathedral of Shadows.
        /// Runs the primary interaction loop.
        /// </summary>
        public void EnterCathedral()
        {
            while (true)
            {
                // Dispatch from typed Cathedral intent so menu labels remain presentation-only.
                FusionMainMenuResult choice = _uiBridge.ShowCathedralMainMenu(MoonPhaseSystem.CurrentPhase);

                if (choice.Kind == FusionMenuResultKind.Back) return;

                switch (choice.Action)
                {
                    case FusionMainMenuAction.BinaryFusion: PerformFusionRitual(isSacrificial: false); break;
                    case FusionMainMenuAction.SacrificialFusion: PerformFusionRitual(isSacrificial: true); break;
                    case FusionMainMenuAction.BrowseCompendium: HandleCompendiumRecall(); break;
                    case FusionMainMenuAction.RegisterDemon: HandleRegistration(); break;
                }
            }
        }

        #region Fusion Ritual Sequence

        /// <summary>
        /// Coordinates a fusion attempt from participant selection through preview and final transaction.
        /// The method is intentionally conservative: every cancel path exits before the mutator can touch
        /// party, stock, economy, or Compendium state.
        /// </summary>
        private void PerformFusionRitual(bool isSacrificial)
        {
            List<object> participantPool = new List<object>();

            while (true) // Participant-selection loop; canceled previews return here with no transaction committed.
            {
                // Rebuild the candidate pool each pass so retries reflect current active party, stock, and Persona state.
                if (_player.Class == ClassType.Operator)
                {
                    // Operators can fuse demons currently active in battle formation plus owned demons in stock.
                    var demons = _partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                    demons.AddRange(_player.DemonStock);
                    participantPool = demons.Distinct().Cast<object>().ToList();
                }
                else if (_player.Class == ClassType.WildCard)
                {
                    // Wild Cards fuse Persona masks, including the equipped Persona if one exists.
                    var personas = new List<Persona>();
                    if (_player.ActivePersona != null) personas.Add(_player.ActivePersona);
                    personas.AddRange(_player.PersonaStock);
                    participantPool = personas.Distinct().Cast<object>().ToList();
                }

                if (participantPool.Count < (isSacrificial ? 3 : 2))
                {
                    _messenger.Publish($"You need at least {(isSacrificial ? "three" : "two")} participants.", ConsoleColor.Red, 1000);
                    return;
                }

                List<object> parents = new List<object>();

                // Backing out on the first parent leaves this ritual attempt and returns to the Cathedral menu.
                RitualParticipantSelectionResult<object> p1Result =
                    _uiBridge.SelectRitualParticipant<object>(participantPool, "CHOOSE THE FIRST PARTICIPANT:", parents);
                if (p1Result.Kind != RitualParticipantSelectionKind.Selected || p1Result.Participant == null) return;
                object p1 = p1Result.Participant;
                parents.Add(p1);

                // Backing out on the second parent restarts the parent pair, because parent one may be the mistake.
                Dictionary<object, string> p2DisabledReasons =
                    BuildOwnedDuplicateResultReasons(participantPool, p1, parents);
                RitualParticipantSelectionResult<object> p2Result =
                    _uiBridge.SelectRitualParticipant<object>(participantPool, "CHOOSE THE SECOND PARTICIPANT:", parents, p2DisabledReasons);
                if (p2Result.Kind != RitualParticipantSelectionKind.Selected || p2Result.Participant == null) continue; // Go back to start of parent selection
                object p2 = p2Result.Participant;
                parents.Add(p2);

                object? sacrifice = null;
                if (isSacrificial)
                {
                    // Sacrificial fusion consumes a third eligible participant, drawn from the same class-specific pool.
                    List<object> sacrificePool = participantPool.Where(x => !parents.Contains(x)).ToList();
                    RitualParticipantSelectionResult<object> sacrificeResult =
                        _uiBridge.SelectRitualParticipant<object>(sacrificePool, "CHOOSE THE SACRIFICIAL OFFERING:", parents);

                    // Sacrifice is part of the staged recipe, so canceling it discards the current parent pair.
                    if (sacrificeResult.Kind != RitualParticipantSelectionKind.Selected || sacrificeResult.Participant == null) continue;
                    sacrifice = sacrificeResult.Participant;
                }

                FusionParticipant parentA = FusionParticipant.From(p1);
                FusionParticipant parentB = FusionParticipant.From(p2);
                FusionParticipant? sacrificeParticipant = sacrifice != null ? FusionParticipant.From(sacrifice) : null;

                if (!_planFactory.TryCreate(parentA, parentB, sacrificeParticipant, isSacrificial, MoonPhaseSystem.CurrentPhase, out FusionPlan? plan) || plan == null)
                {
                    _messenger.Publish("The spirits remain silent. This combination yields no result.", ConsoleColor.Red, 1000);
                    continue;
                }

                while (true) // Skill-selection/preview loop; Wait returns here without changing selected parents.
                {
                    // The bridge owns labeling, but the conductor supplies the rule sets that make entries unavailable.
                    SkillInheritanceSelectionResult inheritanceResult = _uiBridge.SelectInheritedSkills(
                        plan.DisplaySkills.ToList(),
                        plan.MaxInheritanceSlots,
                        plan.InherentSkills.ToList(), // Will be labeled "Already Known"
                        plan.ExclusiveSkills.ToList()  // Will be labeled "Exclusive"
                    );

                    if (inheritanceResult.Kind == SkillInheritanceSelectionKind.Aborted) break;
                    List<string> chosenSkills = inheritanceResult.Skills.ToList();

                    // The staged demon is a preview clone: it must match execution math without mutating party or stock.
                    Combatant? staged = _previewFactory.CreatePreview(plan, chosenSkills);

                    if (staged == null) { _messenger.Publish("Error staging fusion result.", ConsoleColor.Red); break; }

                    RitualConfirmationResult confirm = _uiBridge.ConfirmRitual(staged,
                        plan.PreviewBaseline, chosenSkills,
                        _player.Level, plan.Operation);

                    // Wait preserves the chosen participants and loops back to inheritance.
                    // Cancel and Forbidden both abandon the staged preview; Forbidden is produced by the level gate.
                    if (confirm.Kind == RitualConfirmationKind.Wait) continue; // Back to Skills
                    if (confirm.Kind == RitualConfirmationKind.Cancel ||
                        confirm.Kind == RitualConfirmationKind.Forbidden) break; // Back to Selection

                    // The accident is revealed only after the player has confirmed their plan.
                    if (plan.IsAccident)
                    {
                        // An accident invalidates deliberate inheritance, replacing it with a randomized legal kit.
                        chosenSkills.Clear();

                        Random rnd = new Random();
                        var accidentPool = plan.PickableSkills.OrderBy(x => rnd.Next()).Take(plan.MaxInheritanceSlots).ToList();

                        // Each inherited skill has a small chance to mutate up or down before the transaction executes.
                        for (int i = 0; i < accidentPool.Count; i++)
                        {
                            if (rnd.Next(0, 100) < 20)
                            {
                                accidentPool[i] = _calculator.GetMutatedSkill(accidentPool[i]);
                            }
                        }

                        chosenSkills = accidentPool;
                    }

                    _uiBridge.DisplayRitualSequence(plan.IsAccident);

                    // Build the transaction context after accidents so the mutator receives the final inherited kit.
                    var context = new FusionContext(_player, parents, sacrifice, chosenSkills, plan.TargetId, _messenger, _partyManager);
                    _mutator.ExecuteFusionTransaction(context, plan.Operation);

                    _messenger.Publish(null, delay: 1500, waitForInput: true);
                    return; // Exit to Cathedral menu after a successful fusion; return to participant selection on any cancel or retry path.
                }
            }
        }

        private Dictionary<object, string> BuildOwnedDuplicateResultReasons(List<object> pool, object firstParent, List<object> exclusions)
        {
            return _ownershipRules.BuildOwnedDuplicateResultReasons(_player, pool, firstParent, exclusions);
        }

        #endregion

        #region Compendium and Helpers

        /// <summary>
        /// Handles Compendium recall and validates that the current player class has somewhere to place the soul.
        /// Operators may use active party or demon stock capacity; Wild Cards recall into Persona stock.
        /// </summary>
        private void HandleCompendiumRecall()
        {
            CompendiumRecallResult recall = _uiBridge.ShowCompendiumRecallMenu();
            if (recall.Kind != CompendiumRecallResultKind.Selected || recall.Entry == null) return;

            Combatant entry = recall.Entry;

            int cost = _compendium.CalculateRecallCost(entry.SourceId);

            bool canRecall = _player.Class switch
            {
                ClassType.Operator => _partyManager.ActiveParty.Count < 4 || _partyManager.HasOpenDemonStockSlot(_player),
                ClassType.WildCard => _partyManager.HasOpenPersonaStockSlot(_player),
                _ => false
            };

            if (!canRecall) { _messenger.Publish("You have no vessel capable of containing this soul.", ConsoleColor.Red, 1000); return; }

            Combatant? snapshot = _compendium.GetRecallEntry(entry.SourceId);
            if (snapshot != null && _mutator.FinalizeRecall(_player, snapshot, cost))
            {
                _messenger.Publish($"{snapshot.Name} has been materialized.", ConsoleColor.Cyan, 800);
            }
        }

        /// <summary>
        /// Records the player's current demon or Persona state into the Compendium.
        /// Operators register demon combatants directly; Wild Cards register Persona masks through
        /// transient combatants so the registry can keep one snapshot shape.
        /// </summary>
        private void HandleRegistration()
        {
            if (_player.Class == ClassType.Operator)
            {
                // Operators pool all demons at their disposal (Active Party + DemonStock)
                var pool = _partyManager.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                pool.AddRange(_player.DemonStock);
                CompendiumRegistrationSelectionResult result = _uiBridge.SelectDemonToRegister(pool.Distinct().ToList());
                if (result.Kind == CompendiumRegistrationSelectionKind.Selected && result.Demon != null)
                {
                    _compendium.RegisterDemon(result.Demon);
                }
            }
            else if (_player.Class == ClassType.WildCard)
            {
                // Registration source for WildCards is their PersonaStock
                RitualParticipantSelectionResult<Persona> result =
                    _uiBridge.SelectRitualParticipant<Persona>(_player.PersonaStock, "SELECT PERSONA TO RECORD:", new List<Persona>());
                // Registration is optional. Canceled and Unavailable both mean no compendium write occurs.
                if (result.Kind == RitualParticipantSelectionKind.Selected && result.Participant != null)
                {
                    _compendium.RegisterDemon(FusionParticipant.CreateTransientCombatant(result.Participant));
                }
            }
        }

        #endregion
    }
}
