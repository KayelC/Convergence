using JRPGPrototype.Entities;
using JRPGPrototype.Core;
using System;
using System.Collections.Generic;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// A "Delegator" strategy for the broad 'Recovery' skill category.
    /// It identifies the intent (Cure, Revive, or Heal) and passes the task 
    /// to the specialized atomic strategies.
    /// </summary>
    public class RecoveryEffect : IBattleEffect
    {
        // We reuse the existing strategies to prevent code duplication (DRY Principle)
        private readonly HealEffect _healer = new HealEffect();
        private readonly CureEffect _curer = new CureEffect();
        private readonly ReviveEffect _reviver = new ReviveEffect();

        public List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge)
        {
            // 1. Route to Revive logic if the skill contains the keyword
            if (actionEffect.Contains("Revive", StringComparison.OrdinalIgnoreCase))
            {
                return _reviver.Apply(user, targets, power, actionName, actionEffect, messenger, status, knowledge);
            }

            // 2. Route to Cure logic if the skill contains the keyword
            if (actionEffect.Contains("Cure", StringComparison.OrdinalIgnoreCase))
            {
                // We perform the cure first, then optionally fall through to healing
                _curer.Apply(user, targets, power, actionName, actionEffect, messenger, status, knowledge);
            }

            // Default route: Healing logic
            // Most recovery skills (Dia, Media) are just healing
            return _healer.Apply(user, targets, power, actionName, actionEffect, messenger, status, knowledge);
        }
    }
}