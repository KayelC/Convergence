using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System.Collections.Generic;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    public interface IBattleEffect
    {
        /// <summary>
        /// Applies the logic for a specific skill or item.
        /// </summary>
        /// <param name="user">The user of the action.</param>
        /// <param name="targets">The targets of the action.</param>
        /// <param name="power">Numerical value (Power/EffectValue).</param>
        /// <param name="actionName">The name of the Skill or Item (e.g. "Tarukaja", "Fire Break").</param>
        /// <param name="actionEffect">The effect description string (e.g. "50%", "instant kill").</param>
        /// <param name="messenger">The mediator for logs.</param>
        /// <param name="status">Authority for ailments/buffs.</param>
        /// <param name="knowledge">Authority for affinities.</param>
        List<CombatResult> Apply(Combatant user, List<Combatant> targets, int power, string actionName, string actionEffect, IBattleMessenger messenger, StatusRegistry status, BattleKnowledge knowledge);
    }
}