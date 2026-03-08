using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Entities.Components
{
    /// <summary>
    /// The Progression Engine for the Entities module.
    /// Handles Experience accumulation, Level Up triggers, randomized stat growth, 
    /// and the calculation of maximum resource pools.
    /// </summary>
    public static class GrowthProcessor
    {
        private static readonly Random _rnd = new Random();

        /// <summary>
        /// Calculates the total experience required to reach the next level.
        /// Formula: 1.5 * Level^3
        /// </summary>
        public static int GetExpRequired(int level)
        {
            return (int)(1.5 * Math.Pow(level, 3));
        }

        /// <summary>
        /// Adds experience to a combatant and handles the level-up loop.
        /// </summary>
        /// <param name="c">The combatant gaining experience.</param>
        /// <param name="amount">The amount of EXP to add.</param>
        /// <param name="io">Optional IO driver to report level gains to the player.</param>
        public static void GainExp(Combatant c, int amount, IGameIO? io = null)
        {
            // 1. Update the lifetime earn tally (used for Fusion transfer math)
            c.LifetimeEarnedExp += amount;

            // 2. Accumulate current EXP
            c.Exp += amount;

            // 3. Level-up loop (supports multi-leveling from high EXP gains)
            while (c.Exp >= GetExpRequired(c.Level))
            {
                c.Exp -= GetExpRequired(c.Level);
                PerformLevelUp(c, io);
            }
        }

        /// <summary>
        /// Logic for a single level increment, including randomized growth and stat point awards.
        /// </summary>
        private static void PerformLevelUp(Combatant c, IGameIO? io = null)
        {
            c.Level++;
            c.StatPoints += 1;

            int oldMaxHP = c.MaxHP;
            int oldMaxSP = c.MaxSP;

            // Humanoid-specific base growth logic
            if (c.Class != ClassType.Demon)
            {
                // SMT-Standard randomized growth increments
                c.BaseHP += _rnd.Next(6, 11);
                c.BaseSP += _rnd.Next(3, 8);
            }

            // Sync the resource pools to reflect new levels/base stats
            RecalculateResources(c);

            // Heal the amount gained during the level up
            int hpGain = c.MaxHP - oldMaxHP;
            int spGain = c.MaxSP - oldMaxSP;
            c.CurrentHP += hpGain;
            c.CurrentSP += spGain;

            if (io != null)
            {
                io.WriteLine($"{c.Name} leveled up to {c.Level}!", ConsoleColor.Cyan);
                if (hpGain > 0 || spGain > 0)
                {
                    io.WriteLine($"+{hpGain} Max HP / +{spGain} Max SP", ConsoleColor.Green);
                }
            }
        }

        /// <summary>
        /// Synchronizes MaxHP and MaxSP based on the combatant's current level and stats.
        /// Fidelity: Maintains the hard-coded caps (666/333) and Vi/Ma scaling ratios.
        /// </summary>
        public static void RecalculateResources(Combatant c)
        {
            // Pull final derived stats from the StatProcessor
            int totalVi = StatProcessor.GetStat(c, StatType.Vi);
            int totalMa = StatProcessor.GetStat(c, StatType.Ma);

            // Formula: BasePool + (ScalingStat * Multiplier)
            // Caps: 666 HP / 333 SP (SMT III Fidelity)
            c.MaxHP = Math.Min(666, c.BaseHP + (totalVi * 5));
            c.MaxSP = Math.Min(333, c.BaseSP + (totalMa * 3));

            // Ensure current values do not exceed newly calculated maximums
            c.CurrentHP = Math.Min(c.CurrentHP, c.MaxHP);
            c.CurrentSP = Math.Min(c.CurrentSP, c.MaxSP);
        }

        /// <summary>
        /// Manages the manual allocation of a stat point.
        /// </summary>
        public static bool AllocateStat(Combatant c, StatType type)
        {
            if (c.StatPoints <= 0) return false;

            // Hard Cap of 40 for base stat allocation
            if (c.CharacterStats.ContainsKey(type) && c.CharacterStats[type] >= 40)
            {
                return false;
            }

            // Increment the base dictionary
            if (c.CharacterStats.ContainsKey(type))
            {
                c.CharacterStats[type]++;
            }
            else
            {
                c.CharacterStats[type] = 1;
            }

            c.StatPoints--;

            // Crucial: Changing Vi or Ma affects Max pools instantly
            RecalculateResources(c);

            return true;
        }

        /// <summary>
        /// Rollback method for UI cancellation. Reverts stats and points to a provided snapshot.
        /// </summary>
        public static void RollbackStats(Combatant c, Dictionary<StatType, int> statBackup, int pointBackup)
        {
            foreach (var kvp in statBackup)
            {
                c.CharacterStats[kvp.Key] = kvp.Value;
            }
            c.StatPoints = pointBackup;

            RecalculateResources(c);
        }
    }
}