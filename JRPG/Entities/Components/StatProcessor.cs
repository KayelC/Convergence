using JRPGPrototype.Core;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Entities.Components
{
    /// <summary>
    /// The Math Engine for the Entities module.
    /// Decouples the complex calculation of final stat values from the Combatant entity.
    /// Handles class-specific logic, weighted Persona influence, and Battle Buff/Debuff multipliers.
    /// </summary>
    public static class StatProcessor
    {
        /// <summary>
        /// Calculates the final usable value of a specific stat, incorporating all modifiers.
        /// Fidelity: Maintains 100% accuracy with original SMT-hybrid formulas and hard caps.
        /// </summary>
        /// <param name="c">The Combatant whose stat is being calculated.</param>
        /// <param name="type">The specific StatType to retrieve.</param>
        /// <returns>The floored integer result after all multipliers and caps.</returns>
        public static int GetStat(Combatant c, StatType type) =>
            LegacyProgressionAdapter.GetStat(c, type);
    }
}
