using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Entities.Components
{
    /// <summary>
    /// The Hydrator service for the Entities module.
    /// Responsible for creating Combatant instances and configuring them based on 
    /// Database templates (PersonaData). Handles the distinct logic for Enemy rules 
    /// versus Player-Allied Demon growth.
    /// </summary>
    public static class CombatantFactory
    {
        /// <summary>
        /// Creates a Combatant instance representing an enemy, hydrating data from PersonaData.
        /// Applies SMT-specific enemy rules: all skills available immediately, 0 base stats, 
        /// and specific enemy resource scaling.
        /// </summary>
        /// <param name="requestedId">The ID from dungeon config (e.g., "E_pixie", "pixie").</param>
        /// <returns>A configured Combatant ready for battle, or a "Glitch" fallback.</returns>
        public static Combatant CreateEnemy(string requestedId)
        {
            string canonicalId = requestedId.ToLower();

            // 1. Flexible ID Resolution: Try direct lookup, then try stripping "E_" prefix
            PersonaData templateData = null;
            if (!Database.Personas.TryGetValue(canonicalId, out templateData))
            {
                if (canonicalId.StartsWith("e_"))
                {
                    string unprefixedId = canonicalId.Substring(2);
                    Database.Personas.TryGetValue(unprefixedId, out templateData);
                }
            }

            // 2. Fallback if no template is found
            if (templateData == null)
            {
                return new Combatant("Glitch", ClassType.Demon)
                {
                    SourceId = requestedId,
                    Level = 1
                };
            }

            // 3. Create a new Combatant shell
            Combatant c = new Combatant(templateData.Name, ClassType.Demon)
            {
                SourceId = requestedId,
                Level = templateData.Level,
                Controller = ControllerType.AI,
                BattleControl = ControlState.ActFreely
            };

            // 4. IMPORTANT: Reset CharacterStats to 0 for Demons.
            // Demons rely entirely on their ActivePersona's scaled stats.
            foreach (StatType t in Enum.GetValues(typeof(StatType)))
            {
                c.CharacterStats[t] = 0;
            }

            // 5. Attach the PersonaData as the ActivePersona and scale it
            c.ActivePersona = templateData.ToPersona();
            c.ActivePersona.ScaleToLevel(c.Level);

            // 6. Give enemies ALL their potential skills immediately (Base + Learned up to level)
            c.ExtraSkills.AddRange(templateData.BaseSkills ?? new List<string>());
            if (templateData.LearnedSkillsRaw != null)
            {
                foreach (var kvp in templateData.LearnedSkillsRaw)
                {
                    if (int.TryParse(kvp.Key, out int skillLevel) && skillLevel <= c.Level)
                    {
                        c.ExtraSkills.Add(kvp.Value);
                    }
                }
            }
            c.ExtraSkills = c.ExtraSkills.Distinct().ToList();

            // 7. Calculate SMT-style Base Pools for Enemies
            // Enemies have slightly lower base pools than humans, scaling with Vi (END) and Ma (MAG)
            int vi = c.GetStat(StatType.Vi);
            int ma = c.GetStat(StatType.Ma);

            c.BaseHP = (int)((c.Level * 3.5) + (vi * 1.5));
            c.BaseSP = (int)((c.Level * 1.0) + (ma * 1.0));

            c.RecalculateResources();
            c.CurrentHP = c.MaxHP;
            c.CurrentSP = c.MaxSP;

            return c;
        }

        /// <summary>
        /// Creates a Combatant instance specifically for player-allied demons (e.g., from fusion).
        /// These demons follow progressive skill learning rules and use allied scaling formulas.
        /// </summary>
        public static Combatant CreatePlayerDemon(string personaId, int level)
        {
            if (!Database.Personas.TryGetValue(personaId.ToLower(), out var pData))
            {
                return new Combatant("Glitch", ClassType.Demon);
            }

            Combatant c = new Combatant(pData.Name, ClassType.Demon)
            {
                SourceId = personaId,
                Level = level,
                Controller = ControllerType.AI, // Allied demons are AI controlled unless commanded
                BattleControl = ControlState.ActFreely
            };

            // Reset base stats to 0 for demons so they rely solely on Persona scaling
            foreach (StatType t in Enum.GetValues(typeof(StatType)))
            {
                c.CharacterStats[t] = 0;
            }

            c.ActivePersona = pData.ToPersona();

            // Scale persona to target level to get correct stats and learned skills
            c.ActivePersona.ScaleToLevel(level);

            // SMT Logic for Allied Demon Base Pools: Scaling is higher than Enemies
            int vi = c.GetStat(StatType.Vi); // Pulls from scaled ActivePersona
            int ma = c.GetStat(StatType.Ma); // Pulls from scaled ActivePersona

            c.BaseHP = (int)((level * 4) + (vi * 2));
            c.BaseSP = (int)((level * 1.5) + (ma * 1.5));

            c.RecalculateResources();
            c.CurrentHP = c.MaxHP;
            c.CurrentSP = c.MaxSP;

            return c;
        }
    }
}