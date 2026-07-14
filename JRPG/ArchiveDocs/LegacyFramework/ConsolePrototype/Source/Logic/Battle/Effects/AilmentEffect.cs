using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using System;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// Strategy for non-damaging skills that attempt to inflict status ailments.
    /// This class implements IBattleEffect as defined in the project contract.
    /// </summary>
    public class AilmentEffect : IBattleEffect
    {
        /// <summary>
        /// Applies the ailment infliction logic using the ActionEffect string for probability.
        /// </summary>
        /// <param name="user">The combatant attempting to inflict the status.</param>
        /// <param name="targets">The targets of the ailment skill.</param>
        /// <param name="power">Numerical value (often 0 for pure ailment skills).</param>
        /// <param name="actionName">The name of the skill (e.g., "Dormina").</param>
        /// <param name="actionEffect">The effect string containing probability (e.g., "Sleep 40%").</param>
        /// <param name="messenger">The mediator for battle logs.</param>
        /// <param name="status">The authority engine for status management.</param>
        /// <param name="knowledge">The player's affinity memory.</param>
        public List<CombatResult> Apply(
            Combatant user,
            List<Combatant> targets,
            int power,
            string actionName,
            string actionEffect,
            IBattleMessenger messenger,
            StatusRegistry status,
            BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            foreach (var target in targets)
            {
                // Status ailments cannot be inflicted on fallen combatants.
                if (target.IsDead) continue;

                // 1. Logic: Delegate to the StatusRegistry. 
                // In this context, 'actionEffect' is the Source of Truth for the ailment type and % chance.
                bool success = status.TryInflict(user, target, actionEffect);

                // 2. Feedback: Report resistance if the ailment fails.
                // Note: Success messages are broadcasted internally by the StatusRegistry via messenger.
                if (!success)
                {
                    messenger.Publish($"{target.Name} resisted the effect.");
                }

                // 3. Press Turn Logic: 
                // Pure ailment skills are considered neutral actions in this engine's iteration.
                // They consume a standard icon but do not trigger weaknesses or misses.
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            // Defensive check: Ensure the conductor always receives at least one result.
            if (results.Count == 0)
            {
                results.Add(new CombatResult { Type = HitType.Normal });
            }

            return results;
        }
    }
}