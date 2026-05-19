using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
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
                RitualParticipantSelectionResult<object> p2Result =
                    _uiBridge.SelectRitualParticipant<object>(participantPool, "CHOOSE THE SECOND PARTICIPANT:", parents);
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

                // Personas are wrapped as transient combatants because the current calculator is demon-shaped.
                // This is a framework boundary candidate: future rules should accept a common fusion participant model.
                Combatant parentA = (p1 is Combatant c1) ? c1 : CreateTransientCombatant((Persona)p1);
                Combatant parentB = (p2 is Combatant c2) ? c2 : CreateTransientCombatant((Persona)p2);

                var (operation, targetId, isAccident) = _calculator.CalculateResult(parentA, parentB, MoonPhaseSystem.CurrentPhase);

                // No result is a recoverable recipe failure; keep the player inside participant selection.
                if (operation == FusionOperationType.NoFusionPossible || string.IsNullOrEmpty(targetId))
                {
                    _messenger.Publish("The spirits remain silent. This combination yields no result.", ConsoleColor.Red, 1000);
                    continue;
                }

                // Inherent skills are computed before inheritance so the bridge can label blocked choices accurately.
                List<string> inherentSkills = new List<string>();
                PersonaData? resultTemplate = null;

                if (operation == FusionOperationType.CreateNewDemon)
                {
                    Database.Personas.TryGetValue(targetId.ToLower(), out resultTemplate);
                    inherentSkills = resultTemplate?.BaseSkills ?? new List<string>();
                }
                else if (operation == FusionOperationType.StatBoostFusion)
                {
                    // Stat-boost fusion modifies an existing demon, so its current kit blocks duplicate inheritance.
                    Combatant boostTarget = (parentA.ActivePersona.Race == "Mitama") ? parentB : parentA;
                    inherentSkills = boostTarget.GetConsolidatedSkills();
                }
                else if (operation == FusionOperationType.RankUpParent || operation == FusionOperationType.RankDownParent)
                {
                    // Rank mutations inherit the base kit of the destination tier, not the source parent's kit.
                    Database.Personas.TryGetValue(targetId.ToLower(), out resultTemplate);
                    inherentSkills = resultTemplate?.BaseSkills ?? new List<string>();
                }

                while (true) // Skill-selection/preview loop; Wait returns here without changing selected parents.
                {
                    var parentList = new List<Combatant> { parentA, parentB };
                    if (sacrifice != null) parentList.Add((sacrifice is Combatant sc) ? sc : CreateTransientCombatant((Persona)sacrifice));

                    // Pickable skills are legal inheritance candidates and determine the normal slot count.
                    var pickablePool = _calculator.GetInheritableSkills(parentList.ToArray());

                    // Exclusive skills are displayed for transparency but disabled by the bridge.
                    var exclusivePool = _calculator.GetExclusiveSkills(parentList.ToArray());

                    // The display pool intentionally includes blocked exclusive skills so the player can see the rule reason.
                    var displayPool = pickablePool.Union(exclusivePool).ToList();

                    // Sacrificial fusion grants two extra inheritance opportunities on top of the calculator's base slots.
                    int maxSlots = _calculator.GetInheritanceSlotCount(parentList.ToArray()) + (isSacrificial ? 2 : 0);

                    // The bridge owns labeling, but the conductor supplies the rule sets that make entries unavailable.
                    List<string>? chosenSkills = _uiBridge.SelectInheritedSkills(
                        displayPool,
                        Math.Min(8, maxSlots),
                        inherentSkills, // Will be labeled "Already Known"
                        exclusivePool   // Will be labeled "Exclusive"
                    );

                    if (chosenSkills == null) break;

                    // The staged demon is a preview clone: it must match execution math without mutating party or stock.
                    Combatant? staged = CreateStagedDemon(operation, targetId, p1, p2, sacrifice, chosenSkills);

                    if (staged == null) { _messenger.Publish("Error staging fusion result.", ConsoleColor.Red); break; }

                    RitualConfirmationResult confirm = _uiBridge.ConfirmRitual(staged,
                        (parentA.ActivePersona.Race != "Element") ? parentA : parentB, chosenSkills,
                        _player.Level, operation);

                    // Wait preserves the chosen participants and loops back to inheritance.
                    // Cancel and Forbidden both abandon the staged preview; Forbidden is produced by the level gate.
                    if (confirm.Kind == RitualConfirmationKind.Wait) continue; // Back to Skills
                    if (confirm.Kind == RitualConfirmationKind.Cancel ||
                        confirm.Kind == RitualConfirmationKind.Forbidden) break; // Back to Selection

                    // The accident is revealed only after the player has confirmed their plan.
                    if (isAccident)
                    {
                        // An accident invalidates deliberate inheritance, replacing it with a randomized legal kit.
                        chosenSkills.Clear();

                        Random rnd = new Random();
                        var accidentPool = pickablePool.OrderBy(x => rnd.Next()).Take(maxSlots).ToList();

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

                    _uiBridge.DisplayRitualSequence(isAccident);

                    // Build the transaction context after accidents so the mutator receives the final inherited kit.
                    var context = new FusionContext(_player, parents, sacrifice, chosenSkills, targetId, _messenger, _partyManager);
                    _mutator.ExecuteFusionTransaction(context, operation);

                    _messenger.Publish(null, delay: 1500, waitForInput: true);
                    return; // Exit to Cathedral menu after a successful fusion; return to participant selection on any cancel or retry path.
                }
            }
        }

        /// <summary>
        /// Creates a high-fidelity dummy combatant for the UI confirmation screen.
        /// Simulates the exact results of the fusion strategy before it is executed.
        /// </summary>
        private Combatant? CreateStagedDemon(FusionOperationType op, string id, object p1, object p2, object? sacrifice, List<string> skills)
        {
            if (!Database.Personas.TryGetValue(id.ToLower(), out var template)) return null;

            // 1. Initialize the base result from the template
            Combatant staged = CombatantFactory.CreatePlayerDemon(id, template.Level);

            // 2. Apply the manually selected inherited skills
            staged.ExtraSkills.Clear();
            staged.ExtraSkills.AddRange(skills);

            // 3. Logic Branching: Match the Strategy math exactly
            if (op == FusionOperationType.StatBoostFusion)
            {
                // Identify which parent is the 'Target' and which is the 'Mitama'
                Combatant targetCom = (p1 is Combatant c1 && c1.ActivePersona.Race != "Mitama") ? c1 : (Combatant)p2;
                Combatant mitamaCom = (p1 is Combatant m1 && m1.ActivePersona.Race == "Mitama") ? m1 : (Combatant)p2;

                // Copy the target's actual current state to the dummy
                staged.Exp = targetCom.Exp;
                foreach (var st in targetCom.CharacterStats) staged.CharacterStats[st.Key] = st.Value;
                foreach (var mod in targetCom.ActivePersona.StatModifiers) staged.ActivePersona.StatModifiers[mod.Key] = mod.Value;

                // Simulate the Mitama boost on the dummy
                ApplyPreviewBoost(staged, mitamaCom.ActivePersona!.Name);
                staged.RecalculateResources();
            }
            else if (op == FusionOperationType.RankUpParent || op == FusionOperationType.RankDownParent)
            {
                // Identify the target undergoing the rank change
                Combatant original = (p1 is Combatant c1 && c1.ActivePersona.Race != "Element") ? c1 : (Combatant)p2;

                // Carry over modifiers to the higher/lower tier version
                foreach (var mod in original.ActivePersona.StatModifiers) staged.ActivePersona.StatModifiers[mod.Key] = mod.Value;
                staged.RecalculateResources();
            }

            // Apply sacrificial XP breakthrough math to the preview dummy for honest UI feedback
            if (sacrifice != null)
            {
                int earnedXP = (sacrifice is Combatant com) ? com.LifetimeEarnedExp : ((Persona)sacrifice).LifetimeEarnedExp;
                int transferXP = (int)(earnedXP / 1.5);
                staged.GainExp(transferXP);
            }

            return staged;
        }

        private void ApplyPreviewBoost(Combatant demon, string mitamaName)
        {
            Dictionary<StatType, int> boosts = new Dictionary<StatType, int>();
            switch (mitamaName)
            {
                case "Ara Mitama": boosts.Add(StatType.St, 2); boosts.Add(StatType.Ag, 1); break;
                case "Nigi Mitama": boosts.Add(StatType.Ma, 2); boosts.Add(StatType.Lu, 1); break;
                case "Kusi Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Ag, 1); break;
                case "Saki Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Lu, 1); break;
            }

            foreach (var entry in boosts)
            {
                var mods = demon.ActivePersona!.StatModifiers;
                int current = mods.GetValueOrDefault(entry.Key, 0);
                mods[entry.Key] = Math.Min(40, current + entry.Value);
            }
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
                Combatant? selected = _uiBridge.SelectDemonToRegister(pool.Distinct().ToList());
                if (selected != null) _compendium.RegisterDemon(selected);
            }
            else if (_player.Class == ClassType.WildCard)
            {
                // Registration source for WildCards is their PersonaStock
                RitualParticipantSelectionResult<Persona> result =
                    _uiBridge.SelectRitualParticipant<Persona>(_player.PersonaStock, "SELECT PERSONA TO RECORD:", new List<Persona>());
                // Registration is optional. Canceled and Unavailable both mean no compendium write occurs.
                if (result.Kind == RitualParticipantSelectionKind.Selected && result.Participant != null)
                {
                    _compendium.RegisterDemon(CreateTransientCombatant(result.Participant));
                }
            }
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Converts a Persona into a transient Combatant object.
        /// This allows spiritual masks to be processed by the Demon-centric logic of the Calculator and Registry.
        /// </summary>
        private Combatant CreateTransientCombatant(Persona p)
        {
            var transient = new Persona
            {
                Name = p.Name,
                Level = p.Level,
                Race = p.Race,
                Rank = p.Rank,
                Exp = p.Exp,
                LifetimeEarnedExp = p.LifetimeEarnedExp
            };
            transient.SkillSet.AddRange(p.SkillSet);
            foreach (var stat in p.StatModifiers) transient.StatModifiers[stat.Key] = stat.Value;

            return new Combatant(p.Name, ClassType.Demon)
            {
                Level = p.Level,
                ActivePersona = transient,
                SourceId = p.Name,
                LifetimeEarnedExp = p.LifetimeEarnedExp
            };
        }

        #endregion
    }
}
