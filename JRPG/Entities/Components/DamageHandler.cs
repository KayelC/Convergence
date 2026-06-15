using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;

namespace JRPGPrototype.Entities.Components
{
    /// <summary>
    /// The Interaction Engine for the Entities module.
    /// Responsible for calculating the final outcome of an offensive action against a target.
    /// Processes affinities, critical modifiers, status-driven technicals, and resource updates.
    /// </summary>
    public static class DamageHandler
    {
        /// <summary>
        /// Processes damage application against a combatant and returns a CombatResult.
        /// Maintains 100% accuracy with original SMT-hybrid damage logic and affinity messaging.
        /// </summary>
        /// <param name="target">The combatant receiving the action.</param>
        /// <param name="damage">The raw damage value calculated by the Battle Engine.</param>
        /// <param name="element">The elemental type of the attack.</param>
        /// <param name="isCritical">Whether the attack was initially determined to be a critical hit.</param>
        /// <returns>A CombatResult containing damage dealt, hit type, and UI feedback strings.</returns>
        public static CombatResult ApplyDamage(Combatant target, int damage, Element element, bool isCritical) =>
            LegacyCombatPolicyAdapter.Shared.ApplyDamage(target, damage, element, isCritical);
    }
}
