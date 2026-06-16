using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle; // For CombatMath
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Engines
{
    /// <summary>
    /// The authoritative logic engine for status ailments and stat modifications.
    /// Manages application, turn-start restrictions, and turn-end recovery/damage.
    /// Handles the lifecycle of Passive Skills including Auto-Kaja and Regenerates.
    /// </summary>
    public class StatusRegistry
    {
        private readonly LegacyStatusLifecycleAdapter _lifecycle = new LegacyStatusLifecycleAdapter();
        private IBattleMessenger? _messenger;

        // Allows the conductor to inject the shared communication mediator.
        public void SetMessenger(IBattleMessenger messenger)
        {
            _messenger = messenger;
        }

        /// <summary>
        /// Centralized "Effectiveness Gate" logic.
        /// Returns true if the action would result in zero (or negligible) change to the targets.
        /// </summary>
        public bool IsActionRedundant(Combatant actor, SkillData skill, List<Combatant> targets)
        {
            if (skill == null) return false;

            string effect = skill.Effect.ToLower();
            string category = skill.Category.ToLower();
            string name = skill.Name.ToLower();

            // --- RULE 0: Damaging Skills are NEVER Redundant ---
            // If the skill has a power value, the primary intent is damage. 
            // For example : "Toxic Sting" isn't blocked just because the target is already poisoned.
            if (skill.Power != "-" && skill.Power != "NaN")
            {
                return false;
            }

            // 1. Cure Redundancy
            if (effect.Contains("cure") || effect.Contains("dispel") || effect.Contains("patra"))
            {
                // Redundant if none of the targets have an ailment to remove
                if (targets.All(t => t.CurrentAilment == null)) return true;
                return false;
            }

            // 2. Ailment Redundancy
            // Search if we are trying to inflict an ailment the target already has
            foreach (var ailment in Database.Ailments.Values)
            {
                if (effect.Contains(ailment.Name.ToLower()))
                {
                    // If ALL targets already have this specific ailment, it's redundant.
                    if (targets.All(t => t.CurrentAilment?.Name == ailment.Name))
                    {
                        return true;
                    }
                }
            }

            // 3. Recovery Redundancy (HP/SP)
            // AI Logic: Redundant if targets are already above 70% HP (unless it's a Revive/Cure/Dispel)
            if (category.Contains("recovery") && !effect.Contains("revive") && !effect.Contains("cure") && !effect.Contains("dispel"))
            {
                bool isSpHeal = effect.Contains("sp") || effect.Contains("spirit");
                if (isSpHeal)
                {
                    if (targets.All(t => (double)t.CurrentSP / t.MaxSP >= 0.80)) return true;
                }
                else
                {
                    if (targets.All(t => (double)t.CurrentHP / t.MaxHP >= 0.70)) return true;
                }
            }

            // 4. Stat Change Redundancy (Buff/Debuff Caps)
            bool isBuff = name.EndsWith("kaja") || name == "heat riser";
            bool isDebuff = name.EndsWith("nda") || name == "debilitate";

            if (isBuff)
            {
                // Redundant if all targets are already at +3 or higher in the relevant stats
                bool pAtk = name.Contains("taru") || name == "heat riser";
                bool mAtk = name.Contains("maka") || name == "heat riser";
                bool raku = name.Contains("raku") || name == "heat riser";
                bool suku = name.Contains("suku") || name == "heat riser";

                return targets.All(t =>
                    (!pAtk || t.Buffs.GetValueOrDefault("PhysAtk", 0) >= 3) &&
                    (!mAtk || t.Buffs.GetValueOrDefault("MagAtk", 0) >= 3) &&
                    (!raku || t.Buffs.GetValueOrDefault("Defense", 0) >= 3) &&
                    (!suku || t.Buffs.GetValueOrDefault("Agility", 0) >= 3)
                );
            }

            if (isDebuff)
            {
                // Redundant if all targets are already at -3 or lower
                bool pAtk = name.Contains("taru") || name == "debilitate";
                bool mAtk = name.Contains("maka") || name == "debilitate";
                bool raku = name.Contains("raku") || name == "debilitate";
                bool suku = name.Contains("suku") || name == "debilitate";

                return targets.All(t =>
                    (!pAtk || t.Buffs.GetValueOrDefault("PhysAtk", 0) <= -3) &&
                    (!mAtk || t.Buffs.GetValueOrDefault("MagAtk", 0) <= -3) &&
                    (!raku || t.Buffs.GetValueOrDefault("Defense", 0) <= -3) &&
                    (!suku || t.Buffs.GetValueOrDefault("Agility", 0) <= -3)
                );
            }

            return false;
        }

        /// <summary>
        /// Attempts to inflict an ailment on a target.
        /// Parses probability using Regex and matches against the status_ailments.json library.
        /// </summary>
        public bool TryInflict(Combatant attacker, Combatant target, string skillEffect)
        {
            return _lifecycle.TryInflict(attacker, target, skillEffect, _messenger);
        }

        /// <summary>
        /// Curing Logic. 
        /// Checks if the skill effect explicitly lists the target's current ailment or uses "Cure all".
        /// </summary>
        public bool CheckAndExecuteCure(Combatant target, string skillEffect)
        {
            if (target.CurrentAilment == null) return false;

            string effectLower = skillEffect.ToLower();
            bool curesAll = effectLower.Contains("cure all") ||
                           effectLower.Contains("cures all") ||
                           effectLower.Contains("amrita") ||
                           effectLower.Contains("salvation");

            if (curesAll || effectLower.Contains(target.CurrentAilment.Name.ToLower()) ||
                effectLower.Contains("dispel") || effectLower.Contains("dispels"))
            {
                target.RemoveAilment();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Executes Auto-Kaja passives at the start of battle.
        /// Correctly distinguishes between Single-Target and Party-Wide (Ma) variants.
        /// </summary>
        /// <param name="actor">The owner of the passive skill.</param>
        /// <param name="allies">The list of all living allies on the actor's side.</param>
        public void ProcessInitialPassives(Combatant actor, List<Combatant> allies)
        {
            var skills = actor.GetConsolidatedSkills();

            // 1. Single-Target Auto-Skills (User Only)
            if (skills.Contains("Auto-Tarukaja")) ApplyStatChange("Tarukaja", actor);
            if (skills.Contains("Auto-Makakaja")) ApplyStatChange("Makakaja", actor);
            if (skills.Contains("Auto-Rakukaja")) ApplyStatChange("Rakukaja", actor);
            if (skills.Contains("Auto-Sukukaja")) ApplyStatChange("Sukukaja", actor);

            // 2. Party-Wide Auto-Skills (Maha Variants)
            // Iterate through the provided ally list to apply the buff to everyone.
            if (skills.Contains("Auto-Mataru") || skills.Contains("Auto-Maka") || skills.Contains("Auto-Maraku") || skills.Contains("Auto-Masuku"))
            {
                foreach (var ally in allies)
                {
                    if (ally.IsDead) continue;
                    if (skills.Contains("Auto-Mataru")) ApplyStatChange("Matarukaja", ally);
                    if (skills.Contains("Auto-Maka")) ApplyStatChange("Mamakakaja", ally);
                    if (skills.Contains("Auto-Maraku")) ApplyStatChange("Marakukaja", ally);
                    if (skills.Contains("Auto-Masuku")) ApplyStatChange("Masukukaja", ally);
                }
            }
        }

        /// <summary>
        /// Called at the start of a combatant's action phase.
        /// Implements forced behaviors and movement restrictions.
        /// </summary>
        public TurnStartResult ProcessTurnStart(Combatant actor)
        {
            return _lifecycle.ProcessTurnStart(actor);
        }

        /// <summary>
        /// Handles turn-end logic including Poison damage, Recovery rolls, and Passive Restoration.
        /// Distressed, Weak, etc., are handled by CombatMath, but this manages their duration.
        /// Ailment decay and DOT only trigger if the combatant is in the ActiveParty (on the field).
        /// </summary>
        public void ProcessTurnEnd(Combatant actor)
        {
            _lifecycle.ProcessTurnEnd(actor, _messenger);
        }

        /// <summary>
        /// Applies stat changes with a strict [-4, 4] stacking cap.
        /// Parses word-by-word to handle Omni-buffs.
        /// </summary>
        public void ApplyStatChange(string skillName, Combatant target)
        {
            _lifecycle.ApplyStatChange(skillName, target);
        }
    }
}
