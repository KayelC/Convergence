using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling HP recovery for both Items and Skills.
    /// Handles flat values, percentage-based healing, and "fully" flags.
    /// </summary>
    public class HealEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                // Logic: HP recovery only works on the living.
                if (target.IsDead) continue;

                int oldHP = target.CurrentHP;
                int healAmount = power;

                // Power Parsing logic from the old monolithic system.
                // If the data passes 0 (common for text-defined skills), we pluck the value from the string.
                if (healAmount == 0)
                {
                    Match match = Regex.Match(actionEffect, @"\((\d+)\)");
                    if (match.Success)
                    {
                        healAmount = int.Parse(match.Groups[1].Value);
                    }
                }

                if (actionEffect.Contains("50%"))
                {
                    healAmount = target.MaxHP / 2;
                }
                else if (actionEffect.Contains("full") || actionEffect.Contains("fully") || healAmount >= 9999)
                {
                    healAmount = target.MaxHP;
                }

                // State Mutation: Apply the healing capped at MaxHP
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + healAmount);
                int actualHealed = target.CurrentHP - oldHP;

                // UI Feedback: Restoring your specific reporting branches from the original Effects file.
                if (actualHealed > 0)
                {
                    messenger.Publish($"{target.Name} recovered {actualHealed} HP.", ConsoleColor.Green);
                }
                else
                {
                    // Fulfills the "Already at full health" feedback rule.
                    messenger.Publish($"{target.Name} is already at full health.");
                }

                // Press Turn Logic: Successful healing is a neutral turn action.
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