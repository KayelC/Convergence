using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    // Strategy for handling status ailment removal (Skills like Patra, Items like Dis-Poison).
    public class CureEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            // Combine both to ensure items ("Dis-Poison") and skills ("Cures Poison") are both caught
            string cureData = $"{actionName} {actionEffect}";

            foreach (var target in targets)
            {
                // Cure effects typically only work on the living
                if (target.IsDead) continue;

                // 1. Logic: Check if the target actually has an ailment
                if (target.CurrentAilment == null)
                {
                    messenger.Publish($"{target.Name} is not suffering from any ailments.");
                    results.Add(new CombatResult { Type = HitType.Normal });
                    continue;
                }

                if (status.CheckAndExecuteCure(target, cureData))
                {
                    messenger.Publish($"{target.Name} was cured of their ailment!", ConsoleColor.White);
                }
                else
                {
                    messenger.Publish($"The action had no effect on {target.Name}.");
                }

                // 3. Press Turn Logic: Successful use of a utility action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}