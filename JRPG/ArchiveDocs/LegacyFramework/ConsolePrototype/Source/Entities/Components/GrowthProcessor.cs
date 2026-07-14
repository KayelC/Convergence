using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;

namespace JRPGPrototype.Entities.Components
{
    /// <summary>
    /// The Progression Engine for the Entities module.
    /// Handles Experience accumulation, Level Up triggers, randomized stat growth, 
    /// and the calculation of maximum resource pools.
    /// </summary>
    public static class GrowthProcessor
    {
        /// <summary>
        /// Calculates the total experience required to reach the next level.
        /// Formula: 1.5 * Level^3
        /// </summary>
        public static int GetExpRequired(int level) =>
            LegacyProgressionAdapter.GetExpRequired(level);

        /// <summary>
        /// Adds experience to a combatant and handles the level-up loop.
        /// </summary>
        /// <param name="c">The combatant gaining experience.</param>
        /// <param name="amount">The amount of EXP to add.</param>
        /// <param name="io">Optional IO driver to report level gains to the player.</param>
        public static void GainExp(Combatant c, int amount, IGameIO? io = null) =>
            LegacyProgressionAdapter.GainExp(c, amount, io);

        /// <summary>
        /// Synchronizes MaxHP and MaxSP based on the combatant's current level and stats.
        /// Fidelity: Maintains the hard-coded caps (666/333) and Vi/Ma scaling ratios.
        /// </summary>
        public static void RecalculateResources(Combatant c) =>
            LegacyProgressionAdapter.RecalculateResources(c);

        /// <summary>
        /// Manages the manual allocation of a stat point.
        /// </summary>
        public static bool AllocateStat(Combatant c, StatType type) =>
            LegacyProgressionAdapter.AllocateStat(c, type);

        /// <summary>
        /// Rollback method for UI cancellation. Reverts stats and points to a provided snapshot.
        /// </summary>
        public static void RollbackStats(Combatant c, Dictionary<StatType, int> statBackup, int pointBackup) =>
            LegacyProgressionAdapter.RollbackStats(c, statBackup, pointBackup);
    }
}
