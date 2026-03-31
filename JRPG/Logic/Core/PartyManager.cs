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
        private int CalculateMaxStock(int level)
        {
            if (level < 10) return 3;
            if (level < 20) return 5;
            if (level < 30) return 7;
            if (level < 40) return 10;
            return 12;
        }

        /// <summary>
        /// Checks if a specific actor has an open slot in their Demon Stock.
        /// Note: In the unified model, active party demons occupy a stock slot.
        /// </summary>
        public bool HasOpenDemonStockSlot(Combatant actor)
        {
            int maxStock = CalculateMaxStock(actor.Level);
            return actor.DemonStock.Count < maxStock;
        }

        /// <summary>
        /// Checks if a specific actor has an open slot in their Persona Stock.
        /// </summary>
        public bool HasOpenPersonaStockSlot(Combatant actor)
        {
            int maxStock = CalculateMaxStock(actor.Level);
            return actor.PersonaStock.Count < maxStock;
        }

        /// <summary>
        /// Checks if a demon with a given SourceId is already owned by the actor,
        /// either in their active party or in their stock.
        /// </summary>
        public bool IsDemonOwned(Combatant owner, string sourceId)
        {
            // In the unified model, checking the Master Stock covers both field and reserve.
            if (owner.DemonStock.Any(d => d.SourceId == sourceId)) return true;

            // Fallback check for active party in case of non-owner controlled demons
            if (ActiveParty.Any(c => c.SourceId == sourceId && c.Class == ClassType.Demon)) return true;

            return false;
        }

        /// <summary>
        /// Checks if a persona with a given Id is already owned by the actor.
        /// </summary>
        public bool IsPersonaOwned(Combatant owner, string personaId)
        {
            if (owner.ActivePersona?.Name == personaId) return true;
            if (owner.PersonaStock.Any(p => p.Name == personaId)) return true;
            return false;
        }

        public bool AddMember(Combatant member)
        {
            if (ActiveParty.Count < MAX_PARTY_SIZE)
            {
                member.PartySlot = ActiveParty.Count;
                ActiveParty.Add(member);
                return true;
            }
            else
            {
                member.PartySlot = -1;
                ReserveMembers.Add(member);
                return false;
            }
        }

        public void SwapMember(int activeIndex, int reserveIndex)
        {
            if (activeIndex < 0 || activeIndex >= ActiveParty.Count) return;
            if (reserveIndex < 0 || reserveIndex >= ReserveMembers.Count) return;

            Combatant active = ActiveParty[activeIndex];
            Combatant reserve = ReserveMembers[reserveIndex];

            // Perform Swap
            ActiveParty[activeIndex] = reserve;
            ReserveMembers[reserveIndex] = active;

            // Update Indices
            reserve.PartySlot = activeIndex;
            active.PartySlot = -1;
        }

        /// <summary>
        /// Robust Summoning Logic: Moves a demon from the owner's standby stock to the active party.
        /// This is an atomic transaction to prevent duplication.
        /// Demon is NOT removed from DemonStock; its reference is simply added to ActiveParty.
        /// </summary>
        public bool SummonDemon(Combatant owner, Combatant demon)
        {
            if (ActiveParty.Count < MAX_PARTY_SIZE)
            {
                // Ensure the demon is actually owned by the actor and not already on the field
                if (owner.DemonStock.Contains(demon) && !ActiveParty.Contains(demon))
                {
                    demon.PartySlot = ActiveParty.Count;
                    demon.BattleControl = ControlState.DirectControl; // Should default to Direct Control upon summon
                    ActiveParty.Add(demon);
                    return true; // Party Full
                }
            }
            return false; // Party full or not owned
        }

        /// <summary>
        /// Replaces an active demon with a standby demon in one turn.
        /// Essential for maintaining turn economy when the party is full.
        /// </summary>
        public bool SwapActiveDemon(Combatant owner, Combatant activeToRemove, Combatant standbyToAdd)
        {
            if (!ActiveParty.Contains(activeToRemove) || !owner.DemonStock.Contains(standbyToAdd))
                return false;

            // Ensure the standby demon isn't already active (redundancy check)
            if (ActiveParty.Contains(standbyToAdd)) return false;

            int slot = activeToRemove.PartySlot;
            int listIdx = ActiveParty.IndexOf(activeToRemove);

            // Deactivate old
            activeToRemove.PartySlot = -1;

            // Activate new at the same position/index
            standbyToAdd.PartySlot = slot;
            standbyToAdd.BattleControl = ControlState.DirectControl;

            ActiveParty[listIdx] = standbyToAdd;

            return true;
        }

        /// <summary>
        /// Robust Return Logic: Moves a demon from the battlefield back to the owner's standby stock.
        /// Updated: In the Unified model, the demon already exists in the DemonStock. 
        /// This simply removes the reference from the battlefield.
        /// </summary>
        public bool ReturnDemon(Combatant owner, Combatant demon)
        {
            if (ActiveParty.Contains(demon))
            {
                // 1. Remove from battlefield
                demon.PartySlot = -1;
                ActiveParty.Remove(demon);
                return true;
            }
            return false;
        }

        /// Permanently removes a demon from the Master Stock and the Party.
        public bool DismissDemon(Combatant owner, Combatant demon)
        {
            if (ActiveParty.Contains(demon))
            {
                demon.PartySlot = -1;
                ActiveParty.Remove(demon);
            }

            if (owner.DemonStock.Contains(demon))
            {
                owner.DemonStock.Remove(demon);
                return true;
            }
            return false;
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
            // 1. Handle Active Party replacement
            if (ActiveParty.Contains(oldDemon))
            {
                int partyIdx = ActiveParty.IndexOf(oldDemon);
                int slot = oldDemon.PartySlot;
                ActiveParty[partyIdx] = newDemon;
                newDemon.PartySlot = slot; // Assign the new demon to that spot
            }

            // 2. Handle Master Stock replacement
            int stockIdx = owner.DemonStock.IndexOf(oldDemon);
            if (stockIdx != -1)
            {
                owner.DemonStock[stockIdx] = newDemon;
            }
            else if (owner.DemonStock.Count < CalculateMaxStock(owner.Level))
            {
                // Fallback for edge cases where the old demon wasn't in stock for some reason
                owner.DemonStock.Add(newDemon);
            }
        }
    }
}