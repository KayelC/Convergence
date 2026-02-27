using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The state-mutation authority for the Fusion Sub-System.
    /// Handles the atomic transactions for participant consumption, child instantiation,
    /// and class-specific stock management (DemonStock vs PersonaStock).
    /// </summary>
    public class FusionMutator
    {
        private readonly PartyManager _partyManager;
        private readonly EconomyManager _economy;
        private readonly IGameIO _io;

        public FusionMutator(PartyManager partyManager, EconomyManager economy, IGameIO io)
        {
            _partyManager = partyManager;
            _economy = economy;
            _io = io;
        }

        #region Stock Access Management

        /// <summary>
        /// Retrieves the list of fusible entities for an Operator.
        /// Sources: Active Party (Demons only) and the digital DemonStock.
        /// </summary>
        public List<Combatant> GetFusibleDemonPool(Combatant owner)
        {
            List<Combatant> pool = new List<Combatant>();

            // 1. Add demons currently active in the battle party
            var activeDemons = _partyManager.ActiveParty
                .Where(c => c.Class == ClassType.Demon)
                .ToList();

            pool.AddRange(activeDemons);

            // 2. Add demons stored in the owner's stock
            if (owner.DemonStock != null)
            {
                pool.AddRange(owner.DemonStock);
            }

            return pool.Distinct().ToList();
        }

        /// <summary>
        /// Retrieves the list of fusible entities for a Persona User or WildCard.
        /// Sources: The currently manifested ActivePersona and the internal PersonaStock.
        /// </summary>
        public List<Persona> GetFusiblePersonaPool(Combatant owner)
        {
            List<Persona> pool = new List<Persona>();

            // 1. Add the currently equipped persona
            if (owner.ActivePersona != null)
            {
                pool.Add(owner.ActivePersona);
            }

            // 2. Add personas stored in the owner's internal stock
            if (owner.PersonaStock != null)
            {
                pool.AddRange(owner.PersonaStock);
            }

            return pool.Distinct().ToList();
        }

        #endregion

        #region Fusion Execution (Atomic Transactions)

        /// <summary>
        /// Commits the fusion ritual to the game state.
        /// Dispatches the transaction to specific logic paths based on the owner's ClassType.
        /// </summary>
        public void ExecuteFusion(Combatant owner, List<object> materials, string resultId, List<string> chosenSkills, Combatant sacrifice = null)
        {
            switch (owner.Class)
            {
                case ClassType.Operator:
                    // Operators perform Demon-to-Demon fusion
                    List<Combatant> demonMaterials = materials.Cast<Combatant>().ToList();
                    ExecuteDemonToDemonFusion(owner, demonMaterials, resultId, chosenSkills, sacrifice);
                    break;

                case ClassType.WildCard:
                    // Wild Cards perform Persona-to-Persona fusion
                    List<Persona> personaMaterials = materials.Cast<Persona>().ToList();
                    ExecutePersonaToPersonaFusion(owner, personaMaterials, resultId, chosenSkills);
                    break;

                default:
                    _io.WriteLine($"Ritual Aborted: The {owner.Class} class is not authorized for this synthesis.", ConsoleColor.Red);
                    break;
            }
        }

        /// <summary>
        /// Logic for consuming biological Demon entities to create a new Allied Combatant.
        /// Allies follow progression rules (start with base skills).
        /// </summary>
        private void ExecuteDemonToDemonFusion(Combatant owner, List<Combatant> materials, string resultId, List<string> chosenSkills, Combatant sacrifice)
        {
            // 1. Transaction Start: Remove all materials from the world
            List<Combatant> allParticipants = new List<Combatant>(materials);
            if (sacrifice != null) allParticipants.Add(sacrifice);

            foreach (var participant in allParticipants)
            {
                // Remove from active battlefield if present
                if (_partyManager.ActiveParty.Contains(participant))
                {
                    _partyManager.ReturnDemon(owner, participant);
                }

                // Ensure removal from stock
                owner.DemonStock.Remove(participant);
            }

            // 2. Transaction Phase: Instantiate Allied Child
            // Use CreatePlayerDemon to ensure progression rules are followed.
            Combatant child = Combatant.CreatePlayerDemon(resultId, Database.Personas[resultId.ToLower()].Level);

            // 3. Chosen Skill Injection (Inheritance)
            foreach (var skill in chosenSkills)
            {
                if (!child.ExtraSkills.Contains(skill))
                {
                    child.ExtraSkills.Add(skill);
                }
            }

            // 4. Sacrifice Logic: Grant EXP bonus based on sacrifice power
            if (sacrifice != null)
            {
                int expBonus = (int)(sacrifice.Level * 250);
                child.GainExp(expBonus);
            }

            // 5. Finalize Child Entity
            child.RecalculateResources();
            child.CurrentHP = child.MaxHP;
            child.CurrentSP = child.MaxSP;

            // 6. Transaction End: Placement
            if (!_partyManager.SummonDemon(owner, child))
            {
                // Fallback to stock if party is full
                owner.DemonStock.Add(child);
                _io.WriteLine($"{child.Name} has been manifested and sent to stock.", ConsoleColor.Cyan);
            }
            else
            {
                _io.WriteLine($"{child.Name} has joined your active party!", ConsoleColor.Green);
            }
        }

        /// <summary>
        /// Logic for consuming spiritual Persona masks to create a new Persona.
        /// </summary>
        private void ExecutePersonaToPersonaFusion(Combatant owner, List<Persona> materials, string resultId, List<string> chosenSkills)
        {
            // 1. Transaction Start: Remove parent personas
            foreach (var persona in materials)
            {
                // If the parent was equipped, unequip it
                if (owner.ActivePersona == persona)
                {
                    owner.ActivePersona = null;
                }
                owner.PersonaStock.Remove(persona);
            }

            // 2. Transaction Phase: Create new Persona essence from template
            PersonaData template = Database.Personas[resultId.ToLower()];
            Persona child = template.ToPersona();

            // 3. Chosen Skill Injection (Inheritance)
            foreach (var skill in chosenSkills)
            {
                if (!child.SkillSet.Contains(skill))
                {
                    child.SkillSet.Add(skill);
                }
            }

            // 4. Transaction End: Placement
            owner.PersonaStock.Add(child);

            // Auto-equip for UX convenience if current slot is vacant
            if (owner.ActivePersona == null)
            {
                owner.ActivePersona = child;
                _io.WriteLine($"{child.Name} has been manifested and equipped.", ConsoleColor.Green);
            }
            else
            {
                _io.WriteLine($"{child.Name} has been added to your Persona stock.", ConsoleColor.Cyan);
            }

            owner.RecalculateResources();
        }

        /// <summary>
        /// Executes a "Rank Up" fusion, replacing an existing demon with its next higher rank counterpart.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="parentToModify">The specific combatant demon (not elemental) to rank up.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        public void ExecuteRankUpFusion(Combatant owner, Combatant parentToModify, Combatant sacrifice)
        {
            ExecuteRankChange(owner, parentToModify, 1, sacrifice);
        }

        /// <summary>
        /// Executes a "Rank Down" fusion, replacing an existing demon with its next lower rank counterpart.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="parentToModify">The specific combatant demon (not elemental) to rank down.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        public void ExecuteRankDownFusion(Combatant owner, Combatant parentToModify, Combatant sacrifice)
        {
            ExecuteRankChange(owner, parentToModify, -1, sacrifice);
        }

        /// <summary>
        /// Handles the core logic for Rank Up/Down fusions, replacing the parent with a new demon of target rank.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="parentToModify">The original combatant demon undergoing the rank change.</param>
        /// <param name="rankDirection">+1 for Rank Up, -1 for Rank Down.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        private void ExecuteRankChange(Combatant owner, Combatant parentToModify, int rankDirection, Combatant sacrifice)
        {
            // The other parent (the elemental) has already been consumed by FusionConductor/SelectRitualParticipant
            // Here, we only consume the sacrifice.
            if (sacrifice != null)
            {
                if (_partyManager.ActiveParty.Contains(sacrifice)) _partyManager.ReturnDemon(owner, sacrifice);
                owner.DemonStock.Remove(sacrifice);
            }

            string parentRace = parentToModify.ActivePersona.Race;
            int currentRank = parentToModify.ActivePersona.Rank;
            int targetRank = currentRank + rankDirection;

            // Find the PersonaData for the next/previous rank within the same Race
            PersonaData nextRankDemonData = Database.Personas.Values
                .Where(p => p.Race == parentRace && p.Rank == targetRank)
                .OrderBy(p => p.Level) // If multiple with same rank (unlikely), order by level
                .FirstOrDefault();

            if (nextRankDemonData == null)
            {
                // This scenario should ideally be caught by FusionCalculator so player knows in preview
                _io.WriteLine($"{parentToModify.Name} is already the {(rankDirection > 0 ? "highest" : "lowest")} rank of the {parentRace} race. Fusion is not possible.", ConsoleColor.Yellow);
                _io.Wait(1000);
                return;
            }

            // Create the new demon combatant for the player
            Combatant newDemon = Combatant.CreatePlayerDemon(nextRankDemonData.Id, nextRankDemonData.Level);

            // Apply sacrifice EXP bonus
            if (sacrifice != null)
            {
                int expBonus = (int)(sacrifice.Level * 250);
                newDemon.GainExp(expBonus);
            }

            _io.WriteLine($"{parentToModify.Name} has transformed into {newDemon.Name}!", ConsoleColor.Magenta);

            // Atomically replace the old demon with the new one
            ReplaceDemonInState(owner, parentToModify, newDemon);
        }

        /// <summary>
        /// Executes a Mitama fusion, boosting specific stats of the target demon.
        /// </summary>
        /// <param name="owner">The player combatant performing the fusion.</param>
        /// <param name="demonToBoost">The demon whose stats will be boosted.</param>
        /// <param name="mitamaParent">The Mitama demon being consumed for the boost.</param>
        /// <param name="sacrifice">An optional third demon sacrificed for bonus XP.</param>
        public void ExecuteStatBoostFusion(Combatant owner, Combatant demonToBoost, Combatant mitamaParent, Combatant sacrifice)
        {
            // Consume the Mitama parent and any sacrifice
            if (_partyManager.ActiveParty.Contains(mitamaParent)) _partyManager.ReturnDemon(owner, mitamaParent);
            owner.DemonStock.Remove(mitamaParent);
            if (sacrifice != null)
            {
                if (_partyManager.ActiveParty.Contains(sacrifice)) _partyManager.ReturnDemon(owner, sacrifice);
                owner.DemonStock.Remove(sacrifice);
            }

            // Create a new Combatant instance to apply boosts to.
            // This is crucial because `demonToBoost` might be in the active party.
            // We deep-copy its current state (Level, Skills, etc.) but update its Persona's stats.
            Combatant boostedDemon = Combatant.CreatePlayerDemon(demonToBoost.SourceId, demonToBoost.Level);
            // Copy current EXP
            boostedDemon.Exp = demonToBoost.Exp;

            // Ensure the copied persona has the same modifiers as the demonToBoost's active persona
            foreach (var statMod in demonToBoost.ActivePersona.StatModifiers)
            {
                boostedDemon.ActivePersona.StatModifiers[statMod.Key] = statMod.Value;
            }

            // Apply stat boosts based on Mitama name
            Dictionary<StatType, int> boosts = new Dictionary<StatType, int>();
            switch (mitamaParent.ActivePersona.Name) // Use ActivePersona.Name for Mitama type
            {
                case "Ara Mitama":
                    boosts.Add(StatType.St, 2); boosts.Add(StatType.Ag, 1);
                    break;
                case "Nigi Mitama":
                    boosts.Add(StatType.Ma, 2); boosts.Add(StatType.Lu, 1);
                    break;
                case "Kusi Mitama":
                    boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Ag, 1);
                    break;
                case "Saki Mitama":
                    boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Lu, 1);
                    break;
            }

            foreach (var entry in boosts)
            {
                ApplyStatBoost(boostedDemon, entry.Key, entry.Value);
            }

            boostedDemon.RecalculateResources(); // Recalculate MaxHP/SP after stat changes

            // Apply sacrifice EXP bonus
            if (sacrifice != null)
            {
                int expBonus = (int)(sacrifice.Level * 250);
                boostedDemon.GainExp(expBonus);
            }

            _io.WriteLine($"{demonToBoost.Name}'s stats have been enhanced!", ConsoleColor.Magenta);
            ReplaceDemonInState(owner, demonToBoost, boostedDemon);
        }

        /// <summary>
        /// Applies a specific stat boost to a demon's active persona's modifiers, respecting the cap.
        /// </summary>
        private void ApplyStatBoost(Combatant demon, StatType stat, int amount)
        {
            if (amount <= 0) return;

            var personaStatModifiers = demon.ActivePersona.StatModifiers;
            int currentStatValue = personaStatModifiers.GetValueOrDefault(stat, 0);

            if (currentStatValue >= 40)
            {
                _io.WriteLine($" -> {stat} is already at its maximum!", ConsoleColor.Yellow);
                return;
            }

            personaStatModifiers[stat] = Math.Min(40, currentStatValue + amount);
            _io.WriteLine($" -> {stat} increased by {amount}!", ConsoleColor.Cyan); // Feedback for individual stat changes
        }

        /// <summary>
        /// Atomically replaces an old demon with a new one in the player's active party or stock.
        /// Preserves party slot if applicable.
        /// </summary>
        private void ReplaceDemonInState(Combatant owner, Combatant oldDemon, Combatant newDemon)
        {
            // Transfer essential live state from old to new.
            newDemon.OwnerId = oldDemon.OwnerId;
            newDemon.Controller = oldDemon.Controller;
            newDemon.BattleControl = oldDemon.BattleControl;

            // If the demon was in the active party, replace it directly in its slot
            if (_partyManager.ActiveParty.Contains(oldDemon))
            {
                int slot = oldDemon.PartySlot;
                _partyManager.ActiveParty[slot] = newDemon;
                newDemon.PartySlot = slot;

                // Also remove from stock if it was also there (e.g. active persona for wild card)
                owner.DemonStock.Remove(oldDemon);
            }
            else // It was in the stock
            {
                owner.DemonStock.Remove(oldDemon);
                owner.DemonStock.Add(newDemon);
            }

            // Clean up the old demon's active party slot if it was in the party (should be -1 after returnDemon)
            oldDemon.PartySlot = -1;
            owner.RecalculateResources(); // Recalculate owner's resources in case Persona changed
        }

        #endregion

        #region Compendium Recall Logic

        /// <summary>
        /// Finalizes the recall transaction from the Compendium.
        /// Feature: Correctly forks logic to populate DemonStock or PersonaStock.
        /// </summary>
        public bool FinalizeRecall(Combatant owner, Combatant snapshot, int cost)
        {
            if (_economy.Macca < cost)
            {
                _io.WriteLine("Recall Aborted: Insufficient Macca.", ConsoleColor.Red);
                return false;
            }

            if (_economy.SpendMacca(cost))
            {
                if (owner.Class == ClassType.Operator)
                {
                    // Operators receive the Demon entity (Combatant)
                    if (!_partyManager.SummonDemon(owner, snapshot))
                    {
                        owner.DemonStock.Add(snapshot);
                    }
                }
                else // WildCard
                {
                    // WildCards receive the spiritual essence (Persona)
                    Persona essence = snapshot.ActivePersona;

                    // Fidelity Requirement: Deep-copy skills from the combatant back to the persona
                    // This ensures learned skills from the registration snapshot are preserved.
                    var combinedSkills = snapshot.GetConsolidatedSkills();
                    essence.SkillSet.Clear();
                    foreach (var s in combinedSkills)
                    {
                        essence.SkillSet.Add(s);
                    }

                    owner.PersonaStock.Add(essence);
                }

                return true;
            }

            return false;
        }

        #endregion
    }
}