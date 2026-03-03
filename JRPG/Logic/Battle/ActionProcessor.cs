using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Logic.Battle.Effects;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The authoritative coordinator of battle actions.
    /// Manages Costs, Charges, and delegates behavior to the Strategy Registry.
    /// This class serves as the 'Brain' that connects Data, Logic, and UI.
    /// </summary>
    public class ActionProcessor
    {
        private readonly StatusRegistry _status;
        private readonly BattleKnowledge _knowledge;
        private readonly IBattleMessenger _messenger;
        private readonly BattleEffectRegistry _registry;

        public ActionProcessor(StatusRegistry status, BattleKnowledge knowledge, IBattleMessenger messenger)
        {
            _status = status;
            _knowledge = knowledge;
            _messenger = messenger;

            // The Registry is our centralized toolbox of the logic patterns.
            _registry = new BattleEffectRegistry();
        }

        /// <summary>
        /// Orchestrates a standard physical weapon attack.
        /// Reuses DamageEffect to ensure melee affinities (Slash/Strike/Pierce) are discovered.
        /// </summary>
        public CombatResult ExecuteAttack(Combatant attacker, Combatant target)
        {
            if (target.IsDead) return new CombatResult { Type = HitType.Miss, DamageDealt = 0 };

            // 1. UI: Report the attempt
            _messenger.Publish($"{attacker.Name} attacks {target.Name}!");

            // 2. STRATEGY: Get the damage logic for the specific weapon element
            IBattleEffect strategy = _registry.GetEffect(attacker.WeaponElement.ToString());

            if (strategy == null) return new CombatResult { Type = HitType.Miss };

            // 3. EXECUTION: Melee attacks have a standard power of 15
            var results = strategy.Apply(attacker, new List<Combatant> { target }, 15, "", _messenger, _status, _knowledge);

            // 4. CHARGE: Rule - Any physical offensive action consumes the charge
            attacker.IsCharged = false;

            return results.FirstOrDefault() ?? new CombatResult { Type = HitType.Miss };
        }

        // Handles Skill execution: Deducts costs and delegates to the correct strategy.
        public List<CombatResult> ExecuteSkill(Combatant attacker, List<Combatant> targets, SkillData skill)
        {
            // --- 1. ENGINE LOGIC: Cost Calculation & Resource Deduction ---
            var cost = skill.ParseCost();
            int costValue = cost.value;
            var passives = attacker.GetConsolidatedSkills();

            // Determine if it's physical for cost and charge logic
            bool isPhysElement = skill.Category == "Slash" || skill.Category == "Strike" || skill.Category == "Pierce";

            // Arms Master / Spell Master Logic
            if (cost.isHP && isPhysElement && passives.Contains("Arms Master")) costValue /= 2;
            else if (!cost.isHP && !isPhysElement && passives.Contains("Spell Master")) costValue /= 2;

            if (cost.isHP)
            {
                int hpCost = (int)(attacker.MaxHP * (costValue / 100.0));
                attacker.CurrentHP = Math.Max(1, attacker.CurrentHP - hpCost);
            }
            else
            {
                attacker.CurrentSP -= costValue;
            }

            _messenger.Publish($"{attacker.Name} uses {skill.Name}!", ConsoleColor.White, 200);

            // --- 2. STRATEGY LOGIC: Behavior Delegation ---
            IBattleEffect strategy = _registry.GetEffect(skill.Category);
            List<CombatResult> results;

            if (strategy != null)
            {
                // Delegation: The 'How' is handled by the specialized Strategy class
                results = strategy.Apply(attacker, targets, skill.GetPowerVal(), skill.Effect, _messenger, _status, _knowledge);
            }
            else
            {
                _messenger.Publish($"[Error] No logic found for Category: {skill.Category}", ConsoleColor.Yellow);
                results = new List<CombatResult>();
            }

            // --- 3. ENGINE LOGIC: Charge Management ---
            // Charges are cleared regardless of hit/miss once the action is spent
            // Physical skills consume Physical Charge, Magic consumes Mind Charge
            if (isPhysElement) attacker.IsCharged = false;
            else attacker.IsMindCharged = false;

            return results;
        }

        // Handles Item execution: Delegates behavior to the Registry.
        public bool ExecuteItem(Combatant user, List<Combatant> targets, ItemData item)
        {
            _messenger.Publish($"{user.Name} used {item.Name}!", ConsoleColor.White, 200);

            // Logic branch for Traesto
            if (item.Name == "Traesto Gem")
            {
                _messenger.Publish("A blinding light creates a path to safety!", ConsoleColor.White, 800);
                return true;
            }

            // Route standard items (Healing, Cure, Revive) to their corresponding strategies
            IBattleEffect strategy = _registry.GetEffect(item.Type);

            if (strategy != null)
            {
                var results = strategy.Apply(user, targets, item.EffectValue, item.Name, _messenger, _status, _knowledge);
                return results.Any();
            }

            return false;
        }

        // Orchestrates the Analysis logic and records knowledge discovery.
        public void ExecuteAnalyze(Combatant target)
        {
            // 1. LOGIC: Force record all affinities into player memory (Discover All)
            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;
                Affinity aff = target.ActivePersona?.GetAffinity(elem) ?? Affinity.Normal;
                _knowledge.Learn(target.SourceId, elem, aff);
            }

            // 2. BROADCAST: Send the analysis signal to the Messenger. 
            // The BattleLogger sees that 'analysisTarget' is not null and renders the stat sheet.
            _messenger.Publish(message: null, analysisTarget: target);
        }

        // Utility check for the Conductor/AI to determine if a skill targets multiple people.
        public bool IsMultiTarget(SkillData skill)
        {
            string name = skill.Name.ToLower();
            string effect = skill.Effect.ToLower();

            return name.StartsWith("ma") ||
                   name.StartsWith("me") ||
                   effect.Contains("all foes") ||
                   effect.Contains("all allies") ||
                   effect.Contains("party") ||
                   name == "amrita" ||
                   name == "salvation";
        }
    }
}