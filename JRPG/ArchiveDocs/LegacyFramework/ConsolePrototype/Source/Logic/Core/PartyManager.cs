using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Entities;
using JRPGPrototype.Core;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic.Core
{
    public class PartyManager
    {
        // The 4 active combatants on the field
        public List<Combatant> ActiveParty { get; private set; } = new List<Combatant>();

        // The reserve stock (Humans/Guests/Demons not currently fighting)
        public List<Combatant> ReserveMembers { get; private set; } = new List<Combatant>();

        private const int MAX_PARTY_SIZE = 4;
        private readonly LegacyPartyStockAdapter _partyStock = LegacyPartyStockAdapter.Shared;

        public PartyManager(Combatant initialPlayer)
        {
            // The first character added is designated as the initial local player
            initialPlayer.PartySlot = 0;
            initialPlayer.Controller = ControllerType.LocalPlayer;
            ActiveParty.Add(initialPlayer);
        }

        /// <summary>
        /// Calculates max stock size based on character level.
        /// Unlocks slots at specific level thresholds.
        /// Updated: Max capacity now reaches 12.
        /// </summary>
        private int CalculateMaxStock(int level) => _partyStock.GetStockCapacity(level);

        /// <summary>
        /// Checks if a specific actor has an open slot in their Demon Stock.
        /// Note: In the unified model, active party demons occupy a stock slot.
        /// </summary>
        public bool HasOpenDemonStockSlot(Combatant actor)
        {
            return _partyStock.HasOpenDemonStockSlot(actor);
        }

        /// <summary>
        /// Checks if a specific actor has an open slot in their Persona Stock.
        /// </summary>
        public bool HasOpenPersonaStockSlot(Combatant actor)
        {
            return _partyStock.HasOpenPersonaStockSlot(actor);
        }

        /// <summary>
        /// Checks if a demon with a given SourceId is already owned by the actor,
        /// either in their active party or in their stock.
        /// </summary>
        public bool IsDemonOwned(Combatant owner, string sourceId)
        {
            // In the unified model, checking the Master Stock covers both field and reserve.
            if (owner.DemonStock.Any(d => d.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase))) return true;

            // Fallback check for active party in case of non-owner controlled demons
            if (ActiveParty.Any(c => c.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase) && c.Class == ClassType.Demon)) return true;

            return false;
        }

        /// <summary>
        /// Checks if a persona with a given Id is already owned by the actor.
        /// </summary>
        public bool IsPersonaOwned(Combatant owner, string personaId)
        {
            if (owner.ActivePersona?.Name.Equals(personaId, StringComparison.OrdinalIgnoreCase) == true) return true;
            if (owner.PersonaStock.Any(p => p.Name.Equals(personaId, StringComparison.OrdinalIgnoreCase))) return true;
            return false;
        }

        public bool AddMember(Combatant member)
        {
            return _partyStock.AddMember(this, member);
        }

        public void SwapMember(int activeIndex, int reserveIndex)
        {
            _partyStock.SwapMember(this, activeIndex, reserveIndex);
        }

        /// <summary>
        /// Robust Summoning Logic: Moves a demon from the owner's standby stock to the active party.
        /// This is an atomic transaction to prevent duplication.
        /// Demon is NOT removed from DemonStock; its reference is simply added to ActiveParty.
        /// </summary>
        public bool SummonDemon(Combatant owner, Combatant demon)
        {
            return _partyStock.SummonDemon(this, owner, demon);
        }

        /// <summary>
        /// Replaces an active demon with a standby demon in one turn.
        /// Essential for maintaining turn economy when the party is full.
        /// </summary>
        public bool SwapActiveDemon(Combatant owner, Combatant activeToRemove, Combatant standbyToAdd)
        {
            return _partyStock.SwapActiveDemon(this, owner, activeToRemove, standbyToAdd);
        }

        /// <summary>
        /// Robust Return Logic: Moves a demon from the battlefield back to the owner's standby stock.
        /// Updated: In the Unified model, the demon already exists in the DemonStock. 
        /// This simply removes the reference from the battlefield.
        /// </summary>
        public bool ReturnDemon(Combatant owner, Combatant demon)
        {
            return _partyStock.ReturnDemon(this, owner, demon);
        }

        /// Permanently removes a demon from the Master Stock and the Party.
        public bool DismissDemon(Combatant owner, Combatant demon)
        {
            return _partyStock.DismissDemon(this, owner, demon);
        }

        // Checks if the ActiveParty has been entirely eliminated.
        public bool IsPartyWiped()
        {
            return ActiveParty.All(m => m.IsDead);
        }

        // Provides a live-reactive list of currently alive members.
        public List<Combatant> GetAliveMembers()
        {
            return ActiveParty.Where(m => !m.IsDead).ToList();
        }

        /// <summary>
        /// Replaces the oldDemon with the newDemon in the player's active party or stock.
        /// Updated for the Unified model to maintain slot indexing.
        /// </summary>
        /// <param name="owner">Combatant performing the action</param>
        /// <param name="oldDemon">The demon to be replaced.</param>
        /// <param name="newDemon">The new demon replacing the old one</param>
        public void ReplaceDemon(Combatant owner, Combatant oldDemon, Combatant newDemon)
        {
            _partyStock.ReplaceDemon(this, owner, oldDemon, newDemon);
        }
    }
}
