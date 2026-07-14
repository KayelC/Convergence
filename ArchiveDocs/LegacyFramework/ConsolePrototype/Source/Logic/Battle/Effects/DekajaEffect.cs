using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    // Strategy for handling the Dekaja effect: Nullifying all positive stat buffs on targets.
    public class DekajaEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                // Logic: Usually affects the living, but in some games it can clear buffs from corpses.
                // We will stick to living targets for standard combat logic.
                if (target.IsDead) continue;

                bool buffsRemoved = false;

                // 1. Logic: Iterate through the Buffs dictionary keys (Attack, Defense, Agility)
                var keys = target.Buffs.Keys.ToList();
                foreach (var k in keys)
                {
                    // Dekaja ONLY removes positive buffs (> 0). 
                    // Negative debuffs (< 0) remain on the target.
                    if (target.Buffs[k] > 0)
                    {
                        target.Buffs[k] = 0;
                        buffsRemoved = true;
                    }
                }

                // 2. UI Feedback: Only report if a change actually occurred
                if (buffsRemoved)
                {
                    messenger.Publish($"{target.Name}'s stat bonuses were nullified!", ConsoleColor.White);
                }
                else
                {
                    messenger.Publish($"{target.Name} had no stat bonuses to clear.");
                }

                // 3. Press Turn Logic: Neutral action
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}