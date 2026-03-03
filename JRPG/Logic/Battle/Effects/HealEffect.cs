using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling HP recovery for both Items and Skills.
    /// Handles flat values, percentage-based healing, and "fully" flags.
    /// </summary>
    public class HealEffect : IBattleEffect
    {
        public List<CombatResult> Apply(
            Combatant user,
            List<Combatant> targets,
            int power,
            string metadata,
            IBattleMessenger messenger,
            StatusRegistry status,
            BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                // 1. Logic: HP recovery only works on the living.
                if (target.IsDead) continue;

                int oldHP = target.CurrentHP;
                int healAmount = power;

                // 2. Power Parsing logic from the old monolithic system.
                // If the data passes 0 (common for text-defined skills), we pluck the value from the string.
                if (healAmount == 0)
                {
                    Match match = Regex.Match(metadata, @"\((\d+)\)");
                    if (match.Success)
                    {
                        healAmount = int.Parse(match.Groups[1].Value);
                    }
                }

                // 3. Logic: Handle percentage-based flags (e.g., "50%", "full")
                if (metadata.Contains("50%"))
                {
                    healAmount = target.MaxHP / 2;
                }
                else if (metadata.Contains("full") || metadata.Contains("fully") || healAmount >= 9999)
                {
                    healAmount = target.MaxHP;
                }

                // 4. State Mutation: Apply the healing capped at MaxHP
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);

                int actualHealed = target.CurrentHP - oldHP;

                // 5. UI Feedback: Restoring your specific reporting branches from the original Effects file.
                if (actualHealed > 0)
                {
                    messenger.Publish($"{target.Name} recovered {actualHealed} HP.", ConsoleColor.Green);
                }
                else
                {
                    // Fulfills the "Already at full health" feedback rule.
                    messenger.Publish($"{target.Name} is already at full health.");
                }

                // 6. Press Turn Logic: Successful healing is a neutral turn action.
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            // Fallback for empty target lists to prevent Press Turn hang
            if (results.Count == 0)
            {
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}