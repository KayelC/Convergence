using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;

namespace JRPGPrototype.Logic.Battle
{
    /// <summary>
    /// The Mathematical Kernel of the Battle Sub-System.
    /// Provides pure, stateless functions for damage, accuracy, and initiative calculations.
    /// </summary>
    public static class CombatMath
    {
        /// <summary>
        /// Calculates EXP based on the Cubic progression curve: 1.5 * Level^3.
        /// Adjusted for group encounters (approx 3-4 enemies per battle) and normalized stat bonus.
        /// </summary>
        public static int CalculateExpYield(Combatant enemy) =>
            LegacyCombatPolicyAdapter.Shared.CalculateExpYield(enemy);

        /// <summary>
        /// Calculates Macca based on a Quadratic curve (Level^2).
        /// Scaled down to account for higher kill counts in group battles, and adjusted to hit 3-6M by Lv99.
        /// </summary>
        public static int CalculateMaccaYield(Combatant enemy) =>
            LegacyCombatPolicyAdapter.Shared.CalculateMaccaYield(enemy);

        // --- SMT III Damage Formula: 5.0 * sqrt(Power * (Atk/Def)) ---
        /// <summary>
        /// Calculates the raw potency an attacker deals to a target.
        /// Also handles Critical multipliers and status-based modifiers.
        /// </summary>
        /// <param name="attacker">The entity performing the action.</param>
        /// <param name="target">The entity receiving the action.</param>
        /// <param name="skillPower">The base power of the skill or weapon.</param>
        /// <param name="element">The elemental type of the attack.</param>
        /// <param name="isCritical">Output parameter indicating if the hit was a critical.</param>
        /// <returns>
        /// Positive Value: Damage dealt to target.
        /// Zero: Attack was Nulled or Repelled (Caller must check affinity to trigger reflection).
        /// Negative Value: Amount the target is healed (Absorb).
        /// </returns>

        /// <summary>
        /// Square-root Damage Formula: 5.0 * sqrt(Power * (Atk/Def))
        /// Also handles Critical multipliers and status-based modifiers.
        /// </summary>
        public static int CalculateDamage(Combatant attacker, Combatant target, int skillPower, Element element, out bool isCritical) =>
            LegacyCombatPolicyAdapter.Shared.CalculateDamage(attacker, target, skillPower, element, out isCritical);

        /// <summary>
        /// Hit/Evasion check.
        /// Formula: SkillAccuracy + (AttackerAg - TargetAg) * 2 + (AttackerLu - TargetLu)
        /// </summary>
        public static bool CheckHit(Combatant attacker, Combatant target, Element element, string skillAccuracy) =>
            LegacyCombatPolicyAdapter.Shared.CheckHit(attacker, target, element, skillAccuracy);

        public static bool CalculateInstantKill(Combatant attacker, Combatant target, string skillAccuracy) =>
            LegacyCombatPolicyAdapter.Shared.CalculateInstantKill(attacker, target, skillAccuracy);

        public static int CalculateReflectedDamage(Combatant originalAttacker, int skillPower, Element element) =>
            LegacyCombatPolicyAdapter.Shared.CalculateReflectedDamage(originalAttacker, skillPower, element);

        // Calculates the probability of a physical critical hit based on Luck.
        public static int CalculateCritChance(Combatant attacker, Combatant target) =>
            LegacyCombatPolicyAdapter.Shared.CalculateCritChance(attacker, target);

        /// <summary>
        /// Prioritizes Shields > Breaks > Base Persona Affinities.
        /// </summary>
        public static Affinity GetEffectiveAffinity(Combatant target, Element element) =>
            LegacyCombatPolicyAdapter.Shared.GetEffectiveAffinity(target, element);

        // Initiative Roll: Weighted Agility variance.
        public static bool RollInitiative(double playerAvgAg, double enemyAvgAg) =>
            LegacyCombatPolicyAdapter.Shared.RollInitiative(playerAvgAg, enemyAvgAg);
    }
}
