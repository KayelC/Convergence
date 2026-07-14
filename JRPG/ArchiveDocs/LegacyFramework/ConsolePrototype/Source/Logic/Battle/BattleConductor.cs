using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Battle.Effects;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Results;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using JRPGPrototype.Logic.Fusion;
using EncounterActionTurnConsumption = JRPGPrototype.Logic.Battle.Execution.ActionTurnConsumption;
using EncounterBattleResourceState = JRPGPrototype.Logic.Battle.Execution.BattleResourceState;
using EncounterBattlePhaseProgressPolicy = JRPGPrototype.Logic.Battle.Execution.BattlePhaseProgressPolicy;
using EncounterBattleTurnStartLifecycleResult = JRPGPrototype.Logic.Battle.Execution.BattleTurnStartLifecycleResult;
using EncounterBattleTurnStartOutcome = JRPGPrototype.Logic.Battle.Execution.BattleTurnStartOutcome;
using EncounterPressTurnOutcome = JRPGPrototype.Logic.Battle.Execution.PressTurnOutcome;
using EncounterPressTurnResolution = JRPGPrototype.Logic.Battle.Execution.PressTurnResolution;
using EncounterRuntimeActorState = JRPGPrototype.Logic.Battle.Execution.RuntimeActorState;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Root Orchestrator of the Battle Sub-System.
    /// Manages the high-level flow of the Press Turn battle loop.
    /// Delegates specific logic to the Math, Turn, Status, AI, and UI sub-modules.
    /// Utilizes the IBattleMessenger mediator to decouple logic from presentation.
    /// </summary>
    public class BattleConductor
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly List<Combatant> _enemies;
        private readonly InventoryManager _inv;
        private readonly EconomyManager _eco;

        // Shared Communication Mediator
        private readonly IBattleMessenger _messenger;

        // Sub-System Engines
        private readonly PressTurnEngine _turnEngine;
        private readonly StatusRegistry _statusRegistry;
        private readonly ActionProcessor _processor;
        private readonly BattleLogger _logger;
        private readonly BehaviorEngine _ai;
        private readonly InteractionBridge _ui;
        private readonly NegotiationEngine _negotiationEngine;
        private readonly BattleKnowledge _playerKnowledge;
        private readonly CompendiumRegistry _compendium;
        private readonly LegacyRecruitmentAdapter _recruitmentAdapter;
        private readonly LegacyBattleRewardAdapter _rewardAdapter;
        private readonly LegacyBattleCommandShellAdapter _commandShell;

        // Added session-specific list to prevent re-recruiting in same battle
        private readonly HashSet<string> _sessionRecruitedIds = new HashSet<string>();

        private readonly bool _isBossBattle;

        // Battle State Flags
        public bool BattleEnded { get; private set; }
        public bool PlayerWon { get; private set; }
        public bool Escaped { get; private set; }
        public bool TraestoUsed { get; private set; }

        public BattleConductor(
            PartyManager party,
            List<Combatant> enemies,
            InventoryManager inv,
            EconomyManager eco,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium,
            bool isBoss = false)
        {
            _io = io;
            _party = party;
            _enemies = enemies;
            _inv = inv;
            _eco = eco;
            _playerKnowledge = playerKnowledge;
            _compendium = compendium;
            _isBossBattle = isBoss;

            // 1. Initialize the Mediator (The Transmission Tower)
            _messenger = new BattleMessenger();

            // 2. Initialize Sub-Systems
            _turnEngine = new PressTurnEngine();
            _statusRegistry = new StatusRegistry();
            _statusRegistry.SetMessenger(_messenger);

            // Pass the messenger into the logic processor
            _processor = new ActionProcessor(_statusRegistry, _playerKnowledge, _messenger);

            // 3. Initialize the Observer
            _logger = new BattleLogger(_io);
            _logger.Subscribe(_messenger);

            _ai = new BehaviorEngine(_statusRegistry);
            _ui = new InteractionBridge(_io, _party, _inv, _enemies, _turnEngine, _playerKnowledge);
            _negotiationEngine = new NegotiationEngine(_io, _party, _inv, _eco);
            _recruitmentAdapter = LegacyRecruitmentAdapter.Shared;
            _rewardAdapter = LegacyBattleRewardAdapter.Shared;
            _commandShell = new LegacyBattleCommandShellAdapter();
        }

        // Entry point for the encounter. Handles initiative and the phase loop.
        public void StartBattle()
        {
            _messenger.Publish("=== ENEMY ENCOUNTER ===", ConsoleColor.White, 1200, clearScreen: true);

            foreach (var e in _enemies)
            {
                _messenger.Publish($"Appeared: {e.Name} (Lv.{e.Level})");
            }

            RunFrameworkEncounter();

            ResolveBattleEnd();

            // Cleanup: Always unsubscribe when leaving battle to prevent memory leaks
            _logger.Unsubscribe(_messenger);
        }

        private void RunFrameworkEncounter()
        {
            var eventPresentation = new LegacyBattleEventPresentationAdapter(_messenger);
            var adapter = new LegacyEncounterAdapter(this, eventPresentation);
            BattleEncounterResult result = new BattleEncounterRunner().Run(
                new BattleEncounterRequest(
                    adapter.Participants,
                    ContentId.Parse("battle"),
                    ContentId.Parse("legacy_battle"),
                    ContentId.Parse("legacy_moon_phase"),
                    1000),
                new BattleEncounterServices(
                    adapter,
                    adapter,
                    adapter,
                    adapter,
                    () => _turnEngine,
                    new EncounterBattlePhaseProgressPolicy(
                        maximumCommands: 256,
                        maximumConsecutiveFreeActions: 32),
                    adapter,
                    eventPresentation));

            BattleEnded = true;
            if (result.Outcome == BattleEncounterOutcome.Escape)
            {
                Escaped = true;
            }
            else if (result.Outcome == BattleEncounterOutcome.Victory)
            {
                PlayerWon = true;
            }
            else if (result.Outcome == BattleEncounterOutcome.Defeat)
            {
                PlayerWon = false;
            }
        }

        // Orchestrates a single side's phase (Player or Enemy).
        private void ExecutePhase(bool isPlayerSide)
        {
            var activeSide = isPlayerSide ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
            if (activeSide.Count == 0) return;

            // Clear the swap state for everyone acting this phase (Once per Turn Rule)
            foreach (var member in activeSide)
            {
                member.HasSwappedThisTurn = false;
            }

            // Initialize Icons for the phase
            _turnEngine.StartPhase(activeSide.Count);
            int actorIndex = 0;

            while (_turnEngine.HasTurnsRemaining() && !BattleEnded)
            {
                // Refresh live members list (Live-Reactive Iteration)
                var currentLiveActors = isPlayerSide ? _party.GetAliveMembers() : _enemies.Where(e => !e.IsDead).ToList();
                if (currentLiveActors.Count == 0) break;

                // Loop back to start if index is out of bounds
                if (actorIndex >= currentLiveActors.Count) actorIndex = 0;
                Combatant actor = currentLiveActors[actorIndex];

                // --- 1. TURN START (Ailments & Restrictions) ---
                TurnStartResult turnState = _statusRegistry.ProcessTurnStart(actor);
                bool actorRemoved = false; // Prevent index shifting bug

                if (turnState == TurnStartResult.Skip)
                {
                    _messenger.Publish($"{actor.Name} is unable to move!", ConsoleColor.Magenta, 800);
                    _turnEngine.Apply(EncounterActionTurnConsumption.Normal); // Losing turn consumes one action.
                }
                else if (turnState == TurnStartResult.FleeBattle)
                {
                    _messenger.Publish($"{actor.Name} fled in fear!", ConsoleColor.Red, 1000);
                    Escaped = true;
                    BattleEnded = true;
                    return;
                }
                else if (turnState == TurnStartResult.ReturnToCOMP)
                {
                    // Differentiate between Player Demon and Enemy Demon fleeing
                    if (isPlayerSide)
                    {
                        _messenger.Publish($"{actor.Name} returned to COMP in terror!", ConsoleColor.Red, 400);
                        _party.ReturnDemon(actor, actor); // Self-return logic
                    }
                    else
                    {
                        _messenger.Publish($"{actor.Name} has fled!", ConsoleColor.Yellow, 400);
                        _enemies.Remove(actor);
                        actorRemoved = true;
                    }
                    _turnEngine.Apply(EncounterActionTurnConsumption.Normal);
                }
                else
                {
                    // Actor is able to perform an action
                    ExecuteAction(actor, isPlayerSide, turnState);
                }

                // Real-time HUD refresh after every action or skip
                _ui.ForceRefreshHUD();

                // --- 2. TURN END (Recovery & Decay) ---
                // StatusRegistry now handles its own publishing directly to the messenger.
                _statusRegistry.ProcessTurnEnd(actor);

                // Handle demons dying and returning to stock
                foreach (var p in _party.ActiveParty.ToList())
                {
                    if (p.IsDead && p.Class == ClassType.Demon)
                    {
                        _messenger.Publish($"{p.Name} faded away and returned to stock...");
                        // Use actor as owner assuming player owns all party demons in current build
                        _party.ReturnDemon(actor, p);
                    }
                }

                // Check for protagonist death immediately after action
                if (CheckEncounterCompletion()) return;

                if (!actorRemoved) actorIndex++;
            }

            // At phase end, dissolve any unused Karn shields
            var sideToEnd = isPlayerSide ? _party.ActiveParty : _enemies;
            foreach (var combatant in sideToEnd) combatant.DissolveShields();
        }

        /// <summary>
        /// Orchestrates the selection and execution of a specific action.
        /// Handles the fork between Manual Control, AI, and Forced Ailment actions.
        /// </summary>
        private void ExecuteAction(Combatant actor, bool isPlayerSide, TurnStartResult turnState)
        {
            BattleEncounterCommandResult result = ExecuteActionForFramework(actor, isPlayerSide, turnState);
            ApplyFrameworkTurnConsumption(result.TurnConsumption);
            if (result.RequestedOutcome == BattleEncounterOutcome.Escape)
            {
                Escaped = true;
                BattleEnded = true;
            }
        }

        private BattleEncounterCommandResult ExecuteActionForFramework(
            Combatant actor,
            bool isPlayerSide,
            TurnStartResult turnState)
        {
            SkillData? skill = null;
            ItemData? item = null;
            List<Combatant>? targets = null;
            bool actionCommitted = false;

            // --- A. ACTION SELECTION LOOP ---
            while (!actionCommitted && !BattleEnded)
            {
                // Reset temporary selection state
                skill = null;
                item = null;
                targets = null;

                // 1. Forced Behaviors (Ailments)
                if (turnState == TurnStartResult.ForcedPhysical || turnState == TurnStartResult.ForcedConfusion)
                {
                    var forced = _ai.DetermineBestAction(actor, _party.ActiveParty, _enemies, _playerKnowledge,
                        _turnEngine.FullIcons, _turnEngine.BlinkingIcons, turnState);
                    skill = forced.skill;
                    targets = forced.targets;
                    actionCommitted = true;
                }
                // 2. Manual Control
                else if (isPlayerSide && (actor.Controller == ControllerType.LocalPlayer || actor.BattleControl == ControlState.DirectControl))
                {
                    BattleMainMenuResult menuResult = _ui.ShowMainMenu(actor);
                    if (menuResult.Kind == BattleMenuResultKind.Back) continue; // Re-render main menu

                    if (menuResult.Action == BattleMainMenuAction.Attack)
                    {
                        BattleTargetSelectionResult targetResult = _ui.SelectTarget(actor);
                        if (targetResult.Kind != BattleSelectionResultKind.Selected) continue; // Back to Menu
                        BattleCommandShellResult command = _commandShell.CreateBasicAttack(actor, targetResult.Targets);
                        if (!command.CanExecute) continue;
                        targets = command.Targets.ToList();
                        actionCommitted = true;
                    }
                    else if (menuResult.Action == BattleMainMenuAction.Guard)
                    {
                        BattleCommandShellResult command = _commandShell.CreateGuard(actor);
                        if (!command.CanExecute) continue;
                        _processor.ExecuteGuard(actor);
                        return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal);
                    }
                    // Task 3: Seamless Integrated Persona Logic (P3R Style)
                    else if (menuResult.Action == BattleMainMenuAction.Persona)
                    {
                        bool selectingPersonaAction = true;
                        while (selectingPersonaAction)
                        {
                            BattlePersonaActionResult personaResult = _ui.SelectPersonaAction(actor);

                            if (personaResult.Kind == BattlePersonaActionKind.Back)
                            {
                                selectingPersonaAction = false;
                                // Loop will restart Main Menu
                            }
                            else if (personaResult.Kind == BattlePersonaActionKind.RequestSwap)
                            {
                                BattlePersonaSelectionResult personaSelection = _ui.SelectPersona(actor);
                                if (personaSelection.Kind == BattleSelectionResultKind.Selected &&
                                    personaSelection.Persona != null)
                                {
                                    BattleCommandShellResult command = _commandShell.CreatePersonaSwap(actor, personaSelection.Persona);
                                    if (!command.CanExecute) continue;
                                    _processor.ExecutePersonaSwap(actor, personaSelection.Persona);
                                    actor.HasSwappedThisTurn = true;
                                    // FREE ACTION: Logic remains in this inner loop.
                                    // Player can now see NEW skills for the swapped Persona immediately.
                                }
                            }
                            else if (personaResult.Kind == BattlePersonaActionKind.SelectedSkill &&
                                personaResult.SelectedSkill != null)
                            {
                                skill = personaResult.SelectedSkill;
                                BattleTargetSelectionResult targetResult = _ui.SelectTarget(actor, skill);

                                if (targetResult.Kind == BattleSelectionResultKind.Selected)
                                {
                                    BattleCommandShellResult command = _commandShell.CreateLegacySkill(actor, skill, targetResult.Targets);
                                    if (!command.CanExecute) continue;
                                    targets = command.Targets.ToList();
                                    skill = command.Skill;
                                    actionCommitted = true;
                                    selectingPersonaAction = false;
                                }
                                // If target selection does not complete, loop back to Persona Action list.
                            }
                        }

                        if (!actionCommitted && !BattleEnded) continue;
                    }
                    else if (menuResult.Action == BattleMainMenuAction.UseSkill)
                    {
                        BattleSkillSelectionResult skillResult = _ui.SelectSkill(actor, "");
                        if (skillResult.Kind != BattleSelectionResultKind.Selected || skillResult.Skill == null) continue; // Back to Menu
                        skill = skillResult.Skill;

                        BattleTargetSelectionResult targetResult = _ui.SelectTarget(actor, skill);
                        if (targetResult.Kind != BattleSelectionResultKind.Selected) continue; // Back to Menu
                        BattleCommandShellResult command = _commandShell.CreateLegacySkill(actor, skill, targetResult.Targets);
                        if (!command.CanExecute) continue;
                        targets = command.Targets.ToList();
                        skill = command.Skill;
                        actionCommitted = true;
                    }
                    else if (menuResult.Action == BattleMainMenuAction.Comp)
                    {
                        BattleCompActionResult comp = _ui.OpenCOMPMenu(actor);
                        if (comp.Kind == BattleCompActionKind.Back) continue; // Back to Menu

                        if (comp.Kind == BattleCompActionKind.Summon)
                        {
                            // ATOMIC TRANSACTION: PartyManager handles stock and party state
                            BattleCommandShellResult command = comp.Standby is null
                                ? BattleCommandShellResult.Unavailable
                                : _commandShell.CreateDemonSummon(_party, actor, comp.Standby);
                            if (command.CanExecute && comp.Standby != null && _party.SummonDemon(actor, comp.Standby))
                            {
                                _messenger.Publish($"{actor.Name} summoned {comp.Standby.Name}!");
                                return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal);
                            }
                        }
                        else if (comp.Kind == BattleCompActionKind.Swap)
                        {
                            // ATOMIC TRANSACTION: Exchange an active member for a standby member
                            if (comp.Standby != null && comp.Active != null)
                            {
                                BattleCommandShellResult command = _commandShell.CreateDemonSwap(_party, actor, comp.Active, comp.Standby);
                                if (command.CanExecute)
                                {
                                    // Clear Transient state (Guard/Shields/Charge) for the demon leaving
                                    comp.Active.ClearTransientBattleState();

                                    if (_party.SwapActiveDemon(actor, comp.Active, comp.Standby))
                                    {
                                        _messenger.Publish($"{actor.Name} swapped {comp.Active.Name} for {comp.Standby.Name}!");
                                        return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal);
                                    }
                                }
                            }
                        }
                        else if (comp.Kind == BattleCompActionKind.Return)
                        {
                            // ATOMIC TRANSACTION: PartyManager handles stock and party state
                            if (comp.Active != null)
                            {
                                BattleCommandShellResult command = _commandShell.CreateDemonReturn(_party, actor, comp.Active);
                                if (command.CanExecute)
                                {
                                    // Clear Transient state for the demon leaving
                                    comp.Active.ClearTransientBattleState();

                                    if (_party.ReturnDemon(actor, comp.Active))
                                    {
                                        _messenger.Publish($"{actor.Name} returned {comp.Active.Name} to stock.");
                                        return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal);
                                    }
                                }
                            }
                        }
                        else if (comp.Kind == BattleCompActionKind.Analyze)
                        {
                            if (comp.AnalyzeTarget != null)
                            {
                                BattleCommandShellResult command = _commandShell.CreateAnalyze(actor, comp.AnalyzeTarget);
                                if (!command.CanExecute) continue;
                                _processor.ExecuteAnalyze(comp.AnalyzeTarget);
                                return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal);
                            }
                        }
                    }
                    else if (menuResult.Action == BattleMainMenuAction.Pass)
                    {
                        BattleCommandShellResult command = _commandShell.CreatePass(actor);
                        if (!command.CanExecute) continue;
                        _processor.ExecutePass(actor);
                        return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Pass);
                    }
                    else if (menuResult.Action == BattleMainMenuAction.UseItem)
                    {
                        BattleItemSelectionResult itemResult = _ui.SelectItem(actor);
                        if (itemResult.Kind != BattleSelectionResultKind.Selected || itemResult.Item == null) continue; // Back to Menu
                        item = itemResult.Item;

                        // Traesto Gem should not prompt for targets
                        if (item.Name == "Traesto Gem")
                        {
                            BattleCommandShellResult command = _commandShell.CreateLegacyItem(actor, item, []);
                            if (!command.CanExecute) continue;
                            actionCommitted = true;
                        }
                        else
                        {
                            BattleTargetSelectionResult targetResult = _ui.SelectTarget(actor, null, item);
                            if (targetResult.Kind != BattleSelectionResultKind.Selected) continue; // Back to Menu
                            BattleCommandShellResult command = _commandShell.CreateLegacyItem(actor, item, targetResult.Targets);
                            if (!command.CanExecute) continue;
                            targets = command.Targets.ToList();
                            item = command.Item;
                            actionCommitted = true;
                        }
                    }
                    else if (menuResult.Action == BattleMainMenuAction.Talk)
                    {
                        BattleTargetSelectionResult targetResult = _ui.SelectTarget(actor, null, null, true);

                        // FIX: If the user cancels out of the target selection for Talk,
                        // continue the loop to allow them to pick a different action.
                        if (targetResult.Kind != BattleSelectionResultKind.Selected) continue;
                        targets = targetResult.Targets.ToList();
                        BattleCommandShellResult command = _commandShell.CreateNegotiation(actor, targets[0]);
                        if (!command.CanExecute) continue;

                        // Proceed to end the turn after negotiation attempt
                        return HandleNegotiationForFramework(actor, command);
                    }
                    else if (menuResult.Action == BattleMainMenuAction.Tactics)
                    {
                        BattleEncounterCommandResult tacticResult = HandleTacticsForFramework(actor);
                        if (tacticResult.TurnConsumption.Kind == JRPGPrototype.Logic.Battle.Execution.ActionTurnConsumptionKind.None &&
                            tacticResult.RequestedOutcome is null)
                        {
                            continue;
                        }

                        return tacticResult;
                    }
                }
                // 3. Heuristic AI
                else
                {
                    var sideKnowledge = isPlayerSide ? _playerKnowledge : new BattleKnowledge();
                    // Passing current turn engine state to AI
                    var decision = _ai.DetermineBestAction(actor,
                        isPlayerSide ? _party.ActiveParty : _enemies,
                        isPlayerSide ? _enemies : _party.ActiveParty,
                        sideKnowledge,
                        _turnEngine.FullIcons,
                        _turnEngine.BlinkingIcons,
                        turnState);

                    skill = decision.skill;
                    targets = decision.targets;
                    actionCommitted = true;
                }
            }

            // --- B. EXECUTION ---
            if (actionCommitted && !BattleEnded)
            {
                // Handle Pass (represented by AI returning null skill and empty targets)
                if (targets != null && targets.Count == 0 && skill == null)
                {
                    _processor.ExecutePass(actor);
                    return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Pass);
                }

                if (item != null)
                {
                    BattleActionExecutionResult itemResult = _processor.ExecuteItem(actor, targets ?? new List<Combatant>(), item);

                    if (itemResult.Kind == BattleActionExecutionKind.Escaped)
                    {
                        _inv.RemoveItem(item.Id, 1);
                        return BattleEncounterCommandResult.Executed(
                            EncounterActionTurnConsumption.None,
                            requestedOutcome: BattleEncounterOutcome.Escape);
                    }

                    // Defensive check: Only consume and use icon if the item actually worked.
                    if (itemResult.Kind == BattleActionExecutionKind.Executed)
                    {
                        _inv.RemoveItem(item.Id, 1);
                        return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal);
                    }
                    else
                    {
                        // Reprompt if the item had no effect
                        return ExecuteActionForFramework(actor, isPlayerSide, turnState);
                    }
                }
                else if (skill == null && targets != null && targets.Count > 0)
                {
                    var res = _processor.ExecuteAttack(actor, targets[0]);
                    return BattleEncounterCommandResult.Executed(ToFrameworkPressTurn(res.Type, res.IsCritical));
                }
                else if (skill != null && targets != null)
                {
                    BattleActionExecutionResult skillResult = _processor.ExecuteSkill(actor, targets, skill);
                    if (skillResult.Kind == BattleActionExecutionKind.Executed)
                    {
                        IReadOnlyList<CombatResult> results = skillResult.CombatResults;
                        HitType worst = results.Max(r => r.Type);
                        return BattleEncounterCommandResult.Executed(
                            ToFrameworkPressTurn(worst, results.Any(r => r.IsCritical)));
                    }
                    else
                    {
                        // Rejected actions preserve the turn and return to action selection.
                        return ExecuteActionForFramework(actor, isPlayerSide, turnState);
                    }
                }

                _messenger.Publish(string.Empty, delay: 1000);
            }

            return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.None);
        }

        private static EncounterActionTurnConsumption ToFrameworkPressTurn(HitType hitType, bool isCritical)
        {
            EncounterPressTurnOutcome outcome = hitType switch
            {
                HitType.Weakness => EncounterPressTurnOutcome.Weakness,
                HitType.Miss => EncounterPressTurnOutcome.Miss,
                HitType.Null => EncounterPressTurnOutcome.Null,
                HitType.Repel => EncounterPressTurnOutcome.Repel,
                HitType.Absorb => EncounterPressTurnOutcome.Absorb,
                _ => isCritical ? EncounterPressTurnOutcome.Critical : EncounterPressTurnOutcome.Normal
            };
            return EncounterActionTurnConsumption.FromPressTurn(
                new EncounterPressTurnResolution(
                    outcome,
                    isCritical,
                    outcome is EncounterPressTurnOutcome.Repel or EncounterPressTurnOutcome.Absorb));
        }

        private void ApplyFrameworkTurnConsumption(EncounterActionTurnConsumption consumption)
        {
            _turnEngine.Apply(consumption);
        }

        private BattleEncounterCommandResult HandleTacticsForFramework(Combatant actor)
        {
            BattleTacticsResult tactic = _ui.GetTacticsChoice(_isBossBattle, actor.Class == ClassType.Operator);
            if (tactic.Kind == BattleMenuResultKind.Back) return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.None);

            if (tactic.Action == BattleTacticsAction.Escape)
            {
                BattleCommandShellResult command = _commandShell.CreateTacticsEscape(actor);
                if (!command.CanExecute) return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.None);

                int pAgi = actor.GetStat(StatType.Ag);
                double eAvgAgi = _enemies.Any() ? _enemies.Average(e => e.GetStat(StatType.Ag)) : 1;

                if (new Random().Next(0, 100) < Math.Clamp(10.0 + 40.0 * (pAgi / eAvgAgi), 5.0, 95.0))
                {
                    _messenger.Publish("Escaped safely!", ConsoleColor.Cyan, 1000);
                    return BattleEncounterCommandResult.Executed(
                        EncounterActionTurnConsumption.None,
                        requestedOutcome: BattleEncounterOutcome.Escape);
                }

                _messenger.Publish("Failed to escape!", ConsoleColor.Yellow, 1000);
                return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal);
            }

            if (tactic.Action == BattleTacticsAction.Strategy)
            {
                BattleStrategyTargetSelectionResult stratTarget = _ui.SelectStrategyTarget();
                if (stratTarget.Kind == BattleSelectionResultKind.Selected &&
                    stratTarget.Target != null)
                {
                    BattleCommandShellResult command = _commandShell.CreateTacticsStrategy(actor, stratTarget.Target);
                    if (!command.CanExecute) return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.None);

                    stratTarget.Target.BattleControl = (stratTarget.Target.BattleControl == ControlState.ActFreely)
                        ? ControlState.DirectControl
                        : ControlState.ActFreely;
                    _messenger.Publish($"{stratTarget.Target.Name} is now set to {stratTarget.Target.BattleControl}.", ConsoleColor.Gray, 800);
                }
            }

            return BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.None);
        }

        private BattleEncounterCommandResult HandleNegotiationForFramework(Combatant actor, BattleCommandShellResult command)
        {
            Combatant target = command.Targets.Single();
            BattleNegotiationPresentationResult presentation = ResolveNegotiationPresentation(actor, target);
            PublishNegotiationPresentation(presentation);
            if (presentation.RemoveTarget)
            {
                _enemies.Remove(target);
            }

            return BattleEncounterCommandResult.Executed(ToFrameworkTurnConsumption(presentation.TurnEffect));
        }

        private BattleNegotiationPresentationResult ResolveNegotiationPresentation(Combatant actor, Combatant target)
        {
            if (_sessionRecruitedIds.Contains(target.SourceId))
            {
                return BattleNegotiationPresentationResult.AlreadySpoken(target);
            }

            NegotiationSessionPresentationResult result =
                _negotiationEngine.StartNegotiationDetailed(actor, target, _enemies);
            switch (result.LegacyResult)
            {
                case NegotiationResult.Success:
                    LegacyRecruitmentResult recruitment = _recruitmentAdapter.TryRecruit(
                        actor,
                        target,
                        _sessionRecruitedIds,
                        _enemies,
                        _party,
                        _compendium);
                    if (recruitment.Applied)
                    {
                        return BattleNegotiationPresentationResult.Joined(target);
                    }

                    return BattleNegotiationPresentationResult.FailedEndsTurn();

                case NegotiationResult.Failure:
                    return BattleNegotiationPresentationResult.FailedEndsTurn();

                case NegotiationResult.Trick:
                case NegotiationResult.Flee:
                case NegotiationResult.FamiliarFlee:
                    return BattleNegotiationPresentationResult.LeftBattle(target);

                default:
                    return new BattleNegotiationPresentationResult(
                        NegotiationPresentationKind.Suppressed,
                        null,
                        ConsoleColor.Gray,
                        0,
                        BattleNegotiationTurnEffect.Normal);
            }
        }

        private void PublishNegotiationPresentation(BattleNegotiationPresentationResult presentation)
        {
            if (presentation.Kind == NegotiationPresentationKind.Shown &&
                !string.IsNullOrEmpty(presentation.Message))
            {
                _messenger.Publish(presentation.Message, presentation.Color, presentation.Delay);
            }
        }

        private static EncounterActionTurnConsumption ToFrameworkTurnConsumption(BattleNegotiationTurnEffect effect) =>
            effect switch
            {
                BattleNegotiationTurnEffect.None => EncounterActionTurnConsumption.None,
                BattleNegotiationTurnEffect.TerminatePhase => EncounterActionTurnConsumption.TerminatePhase,
                BattleNegotiationTurnEffect.Miss => ToFrameworkPressTurn(HitType.Miss, false),
                _ => EncounterActionTurnConsumption.Normal
            };

        private void HandleTactics(Combatant actor)
        {
            BattleTacticsResult tactic = _ui.GetTacticsChoice(_isBossBattle, actor.Class == ClassType.Operator);
            if (tactic.Kind == BattleMenuResultKind.Back) return;

            if (tactic.Action == BattleTacticsAction.Escape)
            {
                int pAgi = actor.GetStat(StatType.Ag);
                double eAvgAgi = _enemies.Any() ? _enemies.Average(e => e.GetStat(StatType.Ag)) : 1;

                if (new Random().Next(0, 100) < Math.Clamp(10.0 + 40.0 * (pAgi / eAvgAgi), 5.0, 95.0))
                {
                    Escaped = true;
                    BattleEnded = true;
                    _messenger.Publish("Escaped safely!", ConsoleColor.Cyan, 1000);
                }
                else
                {
                    _messenger.Publish("Failed to escape!", ConsoleColor.Yellow, 1000);
                    _turnEngine.Apply(EncounterActionTurnConsumption.Normal);
                }
            }
            else if (tactic.Action == BattleTacticsAction.Strategy)
            {
                BattleStrategyTargetSelectionResult stratTarget = _ui.SelectStrategyTarget();
                if (stratTarget.Kind == BattleSelectionResultKind.Selected &&
                    stratTarget.Target != null)
                {
                    stratTarget.Target.BattleControl = (stratTarget.Target.BattleControl == ControlState.ActFreely) ? ControlState.DirectControl : ControlState.ActFreely;
                    _messenger.Publish($"{stratTarget.Target.Name} is now set to {stratTarget.Target.BattleControl}.", ConsoleColor.Gray, 800);
                }
            }
        }

        private void HandleNegotiation(Combatant actor, Combatant target)
        {
            BattleNegotiationPresentationResult presentation = ResolveNegotiationPresentation(actor, target);
            PublishNegotiationPresentation(presentation);
            if (presentation.RemoveTarget)
            {
                _enemies.Remove(target);
            }

            switch (presentation.TurnEffect)
            {
                case BattleNegotiationTurnEffect.None:
                    break;
                case BattleNegotiationTurnEffect.TerminatePhase:
                    _turnEngine.TerminatePhase();
                    break;
                case BattleNegotiationTurnEffect.Miss:
                    _turnEngine.Apply(EncounterActionTurnConsumption.FromPressTurn(
                        new EncounterPressTurnResolution(EncounterPressTurnOutcome.Miss, false, false)));
                    break;
                default:
                    _turnEngine.Apply(EncounterActionTurnConsumption.Normal);
                    break;
            }
        }

        private sealed class LegacyEncounterAdapter :
            IBattleEncounterInitiativePolicy,
            IBattleEncounterLifecyclePort,
            IBattleEncounterTurnHandler,
            IBattleEncounterCompletionPolicy,
            IBattleEncounterStateSynchronizer
        {
            private static readonly ContentId PlayerTeam = ContentId.Parse("player_party");
            private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_party");
            private static readonly ContentId Hp = StandardProgressionIds.Hp;
            private static readonly ContentId Sp = StandardProgressionIds.Sp;
            private static readonly ContentId ReturnToStock = ContentId.Parse("return_to_stock");
            private readonly BattleConductor _owner;
            private readonly LegacyBattleEventPresentationAdapter _events;
            private readonly Dictionary<RuntimeInstanceId, Combatant> _actors = [];

            public LegacyEncounterAdapter(
                BattleConductor owner,
                LegacyBattleEventPresentationAdapter events)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _events = events ?? throw new ArgumentNullException(nameof(events));
                Participants = BuildParticipants();
            }

            public IReadOnlyList<BattleEncounterParticipant> Participants { get; }

            public IReadOnlyList<ContentId> DetermineTeamOrder(BattleEncounterInitiativeRequest request)
            {
                double pAvgAgi = _owner._party.GetAliveMembers().Any()
                    ? _owner._party.GetAliveMembers().Average(c => c.GetStat(StatType.Ag))
                    : 0;
                double eAvgAgi = _owner._enemies.Any(e => !e.IsDead)
                    ? _owner._enemies.Where(e => !e.IsDead).Average(c => c.GetStat(StatType.Ag))
                    : 0;

                bool playerFirst = CombatMath.RollInitiative(pAvgAgi, eAvgAgi);
                _owner._messenger.Publish(
                    playerFirst ? "Player Party attacks first!" : "Enemy Party attacks first!",
                    playerFirst ? ConsoleColor.Cyan : ConsoleColor.Red,
                    1000);

                return playerFirst
                    ? [PlayerTeam, EnemyTeam]
                    : [EnemyTeam, PlayerTeam];
            }

            public void Synchronize(IReadOnlyList<BattleEncounterParticipant> participants)
            {
                foreach (BattleEncounterParticipant participant in participants)
                {
                    if (!_actors.TryGetValue(participant.InstanceId, out Combatant? actor))
                    {
                        participant.State.IsActive = false;
                        continue;
                    }

                    participant.State.IsActive = IsActive(actor);
                    participant.State.SetResource(Hp, Math.Clamp(actor.CurrentHP, 0, Math.Max(actor.MaxHP, actor.CurrentHP)));
                    participant.State.SetResource(Sp, Math.Clamp(actor.CurrentSP, 0, Math.Max(actor.MaxSP, actor.CurrentSP)));
                    participant.State.SetGuarding(actor.IsGuarding);
                }
            }

            public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
                BattleEncounterLifecycleRequest request,
                CancellationToken cancellationToken = default)
            {
                ContentId firstTeam = request.TeamOrder.FirstOrDefault();
                if (firstTeam == PlayerTeam)
                {
                    List<Combatant> allies = _owner._party.GetAliveMembers();
                    foreach (Combatant actor in allies)
                    {
                        _owner._statusRegistry.ProcessInitialPassives(actor, allies);
                    }
                }
                else if (firstTeam == EnemyTeam)
                {
                    List<Combatant> enemies = _owner._enemies.Where(e => !e.IsDead).ToList();
                    foreach (Combatant actor in enemies)
                    {
                        _owner._statusRegistry.ProcessInitialPassives(actor, enemies);
                    }
                }

                _owner._ui.ForceRefreshHUD();
                _owner._messenger.Publish(string.Empty, delay: 800);
                return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
            }

            public ValueTask<EncounterBattleTurnStartLifecycleResult> ProcessTurnStartAsync(
                BattleEncounterTurnLifecycleRequest request,
                CancellationToken cancellationToken = default)
            {
                Combatant actor = Actor(request.Actor);
                TurnStartResult result = _owner._statusRegistry.ProcessTurnStart(actor);
                return new ValueTask<EncounterBattleTurnStartLifecycleResult>(
                    new EncounterBattleTurnStartLifecycleResult(
                        LegacyTurnStartRestrictionAdapter.ToFramework(result),
                        []));
            }

            public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
                BattleEncounterTurnLifecycleRequest request,
                CancellationToken cancellationToken = default)
            {
                Combatant actor = Actor(request.Actor);
                _owner._ui.ForceRefreshHUD();
                _owner._statusRegistry.ProcessTurnEnd(actor);

                foreach (Combatant member in _owner._party.ActiveParty.ToList())
                {
                    if (member.IsDead && member.Class == ClassType.Demon)
                    {
                        _events.Publish(_events.PresentDemonReturnedToStock(member));
                        _owner._party.ReturnDemon(actor, member);
                    }
                }

                return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
            }

            public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
                BattleEncounterLifecycleRequest request,
                ContentId teamId,
                CancellationToken cancellationToken = default)
            {
                List<Combatant> side = teamId == PlayerTeam ? _owner._party.ActiveParty : _owner._enemies;
                foreach (Combatant combatant in side)
                {
                    combatant.DissolveShields();
                }

                return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
            }

            public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
                BattleEncounterLifecycleRequest request,
                BattleEncounterOutcome outcome,
                CancellationToken cancellationToken = default) =>
                new(Array.Empty<BattleEncounterEvent>());

            public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
                BattleEncounterTurnRequest request,
                CancellationToken cancellationToken = default)
            {
                Combatant actor = Actor(request.Actor);
                bool isPlayerSide = request.Actor.TeamId == PlayerTeam;
                TurnStartResult legacyTurn = ToLegacyTurnStart(request.TurnStartOutcome);

                if (legacyTurn == TurnStartResult.Skip)
                {
                    _events.Publish(_events.PresentTurnRestriction(actor, legacyTurn, isPlayerSide));
                    return new ValueTask<BattleEncounterCommandResult>(
                        BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal));
                }

                if (legacyTurn == TurnStartResult.FleeBattle)
                {
                    _events.Publish(_events.PresentTurnRestriction(actor, legacyTurn, isPlayerSide));
                    return new ValueTask<BattleEncounterCommandResult>(
                        BattleEncounterCommandResult.Executed(
                            EncounterActionTurnConsumption.None,
                            requestedOutcome: BattleEncounterOutcome.Escape));
                }

                if (legacyTurn == TurnStartResult.ReturnToCOMP)
                {
                    if (isPlayerSide)
                    {
                        _events.Publish(_events.PresentTurnRestriction(actor, legacyTurn, isPlayerSide));
                        _owner._party.ReturnDemon(actor, actor);
                    }
                    else
                    {
                        _events.Publish(_events.PresentTurnRestriction(actor, legacyTurn, isPlayerSide));
                        _owner._enemies.Remove(actor);
                    }

                    return new ValueTask<BattleEncounterCommandResult>(
                        BattleEncounterCommandResult.Executed(EncounterActionTurnConsumption.Normal));
                }

                return new ValueTask<BattleEncounterCommandResult>(
                    _owner.ExecuteActionForFramework(actor, isPlayerSide, legacyTurn));
            }

            public BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request)
            {
                if (_owner.Escaped)
                {
                    return new BattleEncounterCompletion(true, BattleEncounterOutcome.Escape);
                }

                if (!_owner.CheckEncounterCompletion())
                {
                    return new BattleEncounterCompletion(false);
                }

                return _owner.PlayerWon
                    ? new BattleEncounterCompletion(true, BattleEncounterOutcome.Victory, PlayerTeam, $"Team {PlayerTeam} won.")
                    : new BattleEncounterCompletion(true, BattleEncounterOutcome.Defeat, EnemyTeam, "Player party was defeated.");
            }

            private IReadOnlyList<BattleEncounterParticipant> BuildParticipants()
            {
                var combatants = new List<Combatant>();
                AddUnique(combatants, _owner._party.ActiveParty);
                if (_owner._party.ActiveParty.FirstOrDefault() is Combatant owner)
                {
                    AddUnique(combatants, owner.DemonStock);
                }

                AddUnique(combatants, _owner._enemies);

                var participants = new List<BattleEncounterParticipant>();
                foreach (Combatant combatant in combatants)
                {
                    EncounterRuntimeActorState state = ToRuntimeState(combatant);
                    _actors[state.InstanceId] = combatant;
                    participants.Add(new BattleEncounterParticipant(state, combatant.Name));
                }

                return participants;
            }

            private EncounterRuntimeActorState ToRuntimeState(Combatant actor)
            {
                RuntimeInstanceId id = LegacyRuntimeIdentityRegistry.Shared.GetActorId(actor);
                ContentId team = _owner._enemies.Contains(actor) ? EnemyTeam : PlayerTeam;
                List<ContentId> capabilities = [];
                if (team == PlayerTeam && actor.Class == ClassType.Demon)
                {
                    capabilities.Add(ReturnToStock);
                }

                return new EncounterRuntimeActorState(
                    id,
                    ToContentId(actor.SourceId, actor.Name, "legacy_actor"),
                    team,
                    Hp,
                    CombatDefenseProfile.Empty,
                    [
                        new EncounterBattleResourceState(Hp, Math.Clamp(actor.CurrentHP, 0, Math.Max(actor.MaxHP, actor.CurrentHP)), Math.Max(actor.MaxHP, actor.CurrentHP)),
                        new EncounterBattleResourceState(Sp, Math.Clamp(actor.CurrentSP, 0, Math.Max(actor.MaxSP, actor.CurrentSP)), Math.Max(actor.MaxSP, actor.CurrentSP))
                    ],
                    [
                        new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, actor.GetStat(StatType.St)),
                        new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, actor.GetStat(StatType.Ma)),
                        new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, actor.GetStat(StatType.Vi)),
                        new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Agility, actor.GetStat(StatType.Ag)),
                        new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Luck, actor.GetStat(StatType.Lu))
                    ],
                    capabilityIds: capabilities,
                    isActive: IsActive(actor));
            }

            private Combatant Actor(BattleEncounterParticipant participant) => _actors[participant.InstanceId];

            private bool IsActive(Combatant actor) =>
                _owner._party.ActiveParty.Contains(actor) || _owner._enemies.Contains(actor);

            private static void AddUnique(List<Combatant> target, IEnumerable<Combatant> source)
            {
                foreach (Combatant combatant in source)
                {
                    if (!target.Contains(combatant))
                    {
                        target.Add(combatant);
                    }
                }
            }

            private static TurnStartResult ToLegacyTurnStart(EncounterBattleTurnStartOutcome result) => result switch
            {
                EncounterBattleTurnStartOutcome.Skip => TurnStartResult.Skip,
                EncounterBattleTurnStartOutcome.LimitedAction => TurnStartResult.LimitedAction,
                EncounterBattleTurnStartOutcome.ForcedPhysical => TurnStartResult.ForcedPhysical,
                EncounterBattleTurnStartOutcome.ForcedConfusion => TurnStartResult.ForcedConfusion,
                EncounterBattleTurnStartOutcome.FleeBattle => TurnStartResult.FleeBattle,
                EncounterBattleTurnStartOutcome.ReturnToStock => TurnStartResult.ReturnToCOMP,
                _ => TurnStartResult.CanAct
            };

            private static ContentId ToContentId(string? preferred, string? fallback, string defaultValue)
            {
                string raw = !string.IsNullOrWhiteSpace(preferred)
                    ? preferred
                    : !string.IsNullOrWhiteSpace(fallback)
                        ? fallback
                        : defaultValue;
                string normalized = new string(raw.Trim().ToLowerInvariant()
                    .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                    .ToArray())
                    .Trim('_');
                return ContentId.Parse(string.IsNullOrWhiteSpace(normalized) ? defaultValue : normalized);
            }
        }

        /// <summary>
        /// If the local player (protagonist) dies, 
        /// the battle ends immediately in defeat.
        /// </summary>
        private bool CheckEncounterCompletion()
        {
            // 1. High Priority: Protagonist Check
            if (_party.ActiveParty.Any(p => p.Controller == ControllerType.LocalPlayer && p.IsDead))
            {
                PlayerWon = false;
                BattleEnded = true;
                return true;
            }

            // 2. Enemy Side Check
            if (_enemies.All(e => e.IsDead)) { PlayerWon = true; BattleEnded = true; return true; }

            // 3. Full Party Wipe Check
            if (_party.IsPartyWiped()) { PlayerWon = false; BattleEnded = true; return true; }
            return false;
        }

        private void ResolveBattleEnd()
        {
            if (PlayerWon)
            {
                _messenger.Publish("\nVICTORY!", ConsoleColor.Green, 500);

                LegacyBattleRewardCalculation rewards = _rewardAdapter.Calculate(
                    _enemies,
                    _party.GetAliveMembers());

                BattleRewardPresentationResult rewardPresentation = _rewardAdapter.Present(rewards);
                if (rewardPresentation.Kind == NegotiationPresentationKind.Shown)
                {
                    _messenger.Publish(
                        rewardPresentation.Message,
                        rewardPresentation.Color,
                        rewardPresentation.Delay);
                }

                _rewardAdapter.Apply(rewards, _eco, _io);
            }
            else if (!Escaped && !TraestoUsed)
            {
                _messenger.Publish("\nDEFEAT...", ConsoleColor.Red, 1000);
            }

            // Tiered Cleanup: Loop through active party and everyone in the master stock.
            // This ensures all owned demons have their buffs cleared but keep their ailments.
            foreach (var member in _party.ActiveParty)
            {
                member.CleanupBattleState();
            }

            // Process all demons currently in standby stock
            foreach (var demon in _party.ActiveParty.First().DemonStock)
            {
                demon.CleanupBattleState();
            }

            _messenger.Publish("Press any key to exit battle...", ConsoleColor.Gray, waitForInput: true);
        }
    }
}
