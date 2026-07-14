using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// Strategy for handling SP recovery for Items (like Soul Food) and Skills.
    public class SpiritEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                // SP recovery typically only works on living targets
                if (target.IsDead) continue;

                int oldSP = target.CurrentSP;
                int recoveryAmount = power;

                if (actionEffect.Contains("50%"))
                {
                    recoveryAmount = target.MaxSP / 2;
                }
                else if (actionEffect.Contains("full") || actionEffect.Contains("fully") || power >= 9999)
                {
                    recoveryAmount = target.MaxSP;
                }

                // State Mutation: Apply recovery capped at MaxSP
                target.CurrentSP = Math.Min(target.MaxSP, target.CurrentSP + recoveryAmount);
                int actualRecovered = target.CurrentSP - oldSP;

                // UI Feedback: Broadcast the result via the messenger
                if (actualRecovered > 0)
                {
                    messenger.Publish($"{target.Name} recovered {actualRecovered} SP.", ConsoleColor.Cyan);
                }
                else
                {
                    messenger.Publish($"{target.Name}'s SP is already full.");
                }

                // Press Turn Logic: Neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}