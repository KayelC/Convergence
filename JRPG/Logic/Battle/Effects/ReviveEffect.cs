using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    // Strategy for handling revival logic (e.g., Recarm, Samarecarm, Balm of Life).
    public class ReviveEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                // Revival effects only work on targets that are currently dead.
                if (!target.IsDead) continue;

                // 1. Calculate HP restoration amount
                // Samarecarm/Samarecarmdra uses "fully" or power 100
                int hpToRestore = power;
                if (actionEffect.Contains("full") || actionEffect.Contains("fully") || power >= 100)
                {
                    hpToRestore = target.MaxHP;
                }
                else
                {
                    // Recarm standard usually revives with 50% HP or a flat power value
                    hpToRestore = (power > 0) ? power : target.MaxHP / 2;
                }

                // 2. State Mutation: Set HP
                // Setting HP above 0 automatically removes the IsDead state in the Combatant class
                target.CurrentHP = Math.Min(target.MaxHP, hpToRestore);

                // 3. UI Feedback
                messenger.Publish($"{target.Name} was revived!", ConsoleColor.Green);

                // 4. Press Turn Logic: Neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            // If no one was dead and the loop finished without reviving anyone
            if (results.Count == 0)
            {
                messenger.Publish("It had no effect...");
                results.Add(new CombatResult { Type = HitType.Miss });
            }

            return results;
        }
    }
}