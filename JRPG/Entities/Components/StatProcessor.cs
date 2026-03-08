using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Entities.Components
{
    /// <summary>
    /// The Math Engine for the Entities module.
    /// Decouples the complex calculation of final stat values from the Combatant entity.
    /// Handles class-specific logic, weighted Persona influence, and Battle Buff/Debuff multipliers.
    /// </summary>
    public static class StatProcessor
    {
        /// <summary>
        /// Calculates the final usable value of a specific stat, incorporating all modifiers.
        /// Fidelity: Maintains 100% accuracy with original SMT-hybrid formulas and hard caps.
        /// </summary>
        /// <param name="c">The Combatant whose stat is being calculated.</param>
        /// <param name="type">The specific StatType to retrieve.</param>
        /// <returns>The floored integer result after all multipliers and caps.</returns>
        public static int GetStat(Combatant c, StatType type)
        {
            int rawStat = 0;

            // --- 1. CORE LOGIC BRANCHING ---

            // DEMON LOGIC: Physical manifestations of Personas.
            // They do not have "Base Stats" of their own; they use Persona stats at 100%.
            if (c.Class == ClassType.Demon)
            {
                if (c.ActivePersona == null) return 0;
                rawStat = c.ActivePersona.StatModifiers.GetValueOrDefault(type, 0);
            }
            // HUMANOID LOGIC (Human, Operator, PersonaUser, WildCard)
            else
            {
                // Start with the base value from the combatant's progression
                int charVal = c.CharacterStats.GetValueOrDefault(type, 0);

                // Apply Accessory Modifiers
                if (c.EquippedAccessory != null)
                {
                    if (Enum.TryParse(c.EquippedAccessory.ModifierStat, true, out StatType accStat))
                    {
                        if (accStat == type)
                        {
                            charVal += c.EquippedAccessory.ModifierValue;
                        }
                    }
                }

                // OPERATOR LOGIC: No Persona influence on stats.
                if (c.Class == ClassType.Operator)
                {
                    rawStat = charVal;
                }
                // PERSONA USER / WILDCARD LOGIC: Base Stats + Weighted Persona Stats
                else if (c.ActivePersona == null || !c.ActivePersona.StatModifiers.ContainsKey(type))
                {
                    rawStat = charVal;
                }
                else
                {
                    int personaVal = c.ActivePersona.StatModifiers.GetValueOrDefault(type, 0);

                    // SMT-Hybrid Weighting Formulas:
                    // St/Ma receive 40% influence from Persona.
                    // Vi/Ag receive 25% influence from Persona.
                    // Lu receives 50% influence from Persona.
                    rawStat = (int)Math.Floor(type switch
                    {
                        StatType.St => charVal + (personaVal * 0.4),
                        StatType.Ma => charVal + (personaVal * 0.4),
                        StatType.Vi => charVal + (personaVal * 0.25),
                        StatType.Ag => charVal + (personaVal * 0.25),
                        StatType.Lu => charVal + (personaVal * 0.5),
                        _ => charVal
                    });
                }
            }

            // --- 2. GLOBAL HARD CAP ---
            // SMT progression balance requires a hard cap of 40 for base stat lookups.
            int cappedStat = Math.Min(40, rawStat);

            // --- 3. BATTLE BUFFS & DEBUFFS (Kaja/Nda) ---
            // Multipliers apply to the capped value.
            // Buff (Kaja): 1.4x multiplier.
            // Debuff (Nda): 0.6x multiplier.
            double finalValue = cappedStat;

            switch (type)
            {
                case StatType.St:
                case StatType.Ma:
                    // Attacks are governed by "Attack" / "AttackDown"
                    if (c.Buffs.TryGetValue("Attack", out int atkBuff) && atkBuff > 0) finalValue *= 1.4;
                    if (c.Buffs.TryGetValue("AttackDown", out int atkDebuff) && atkDebuff > 0) finalValue *= 0.6;
                    break;

                case StatType.Vi:
                    // Vitality is governed by "Defense" / "DefenseDown"
                    if (c.Buffs.TryGetValue("Defense", out int defBuff) && defBuff > 0) finalValue *= 1.4;
                    if (c.Buffs.TryGetValue("DefenseDown", out int defDebuff) && defDebuff > 0) finalValue *= 0.6;
                    break;

                case StatType.Ag:
                    // Agility is governed by "Agility" / "AgilityDown"
                    if (c.Buffs.TryGetValue("Agility", out int agiBuff) && agiBuff > 0) finalValue *= 1.4;
                    if (c.Buffs.TryGetValue("AgilityDown", out int agiDebuff) && agiDebuff > 0) finalValue *= 0.6;
                    break;
            }

            return (int)Math.Floor(finalValue);
        }
    }
}