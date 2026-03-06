using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for handling "Elemental Break" effects.
    /// Temporarily removes elemental immunities (Null, Repel, Absorb) from a target.
    /// </summary>
    public class BreakEffect : IBattleEffect
    {
        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            // 1. Logic: Identify which element we are breaking from the Action Name
            Element elementToBreak = Element.None;
            string skillName = actionName.ToLower();

            if (skillName.Contains("fire")) elementToBreak = Element.Fire;
            else if (skillName.Contains("ice")) elementToBreak = Element.Ice;
            else if (skillName.Contains("elec")) elementToBreak = Element.Elec;
            else if (skillName.Contains("wind")) elementToBreak = Element.Wind;
            else if (skillName.Contains("earth")) elementToBreak = Element.Earth;
            else if (skillName.Contains("light")) elementToBreak = Element.Light;
            else if (skillName.Contains("dark")) elementToBreak = Element.Dark;

            // If the element could not be determined, abort with a neutral result.
            if (elementToBreak == Element.None)
            {
                results.Add(new CombatResult { Type = HitType.Normal });
                return results;
            }

            foreach (var target in targets)
            {
                // Break effects typically do not work on the dead.
                if (target.IsDead) continue;

                // 2. State Mutation: Apply the break to the target's persistent dictionary.
                // Rule: Break lasts for 3 active turns.
                if (target.BrokenAffinities.ContainsKey(elementToBreak))
                {
                    target.BrokenAffinities[elementToBreak] = 3;
                }
                else
                {
                    target.BrokenAffinities.Add(elementToBreak, 3);
                }

                // 3. UI Feedback: Broadcast the specific break via the mediator.
                messenger.Publish($"{target.Name}'s {elementToBreak} resistance was broken!");

                // Breaks count as a successful neutral turn action.
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}