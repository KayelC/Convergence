using System;
using System.Collections.Generic;
using JRPGPrototype.Core;
using JRPGPrototype.Services;
using JRPGPrototype.Entities.Components;

namespace JRPGPrototype.Entities
{
    public class Persona
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Race { get; set; } = string.Empty;
        public int Rank { get; set; }
        public string? InheritanceType { get; set; }

        // Stats & affinities
        public Dictionary<Element, Affinity> AffinityMap { get; set; } = new Dictionary<Element, Affinity>();
        public CombatDefenseProfile CombatDefenseProfile { get; set; } = CombatDefenseProfile.Empty;
        public Dictionary<StatType, int> StatModifiers { get; set; } = new Dictionary<StatType, int>();

        // Skills
        public List<string> SkillSet { get; set; } = new List<string>();
        public Dictionary<int, string> SkillsToLearn { get; set; } = new Dictionary<int, string>();

        // Growth
        public int Exp { get; set; }
        public int ExpRequired => LegacyProgressionAdapter.GetExpRequired(Level);

        // Tracks strictly experience points gained through gameplay since acquisition.
        public int LifetimeEarnedExp { get; set; } = 0;

        public Affinity GetAffinity(Element elem)
        {
            return AffinityMap.ContainsKey(elem) ? AffinityMap[elem] : Affinity.Normal;
        }

        public void GainExp(int amount, IGameIO? io = null) =>
            LegacyProgressionAdapter.GainPersonaExp(this, amount, io);

        //Force Sync for Instantiation
        // Called when creating a Demon/Persona at a specific level to ensure it has correct stats/skills
        public void ScaleToLevel(int targetLevel) =>
            LegacyProgressionAdapter.ScalePersonaToLevel(this, targetLevel);

        public void RecalculateSkills()
        {
            foreach (var kvp in SkillsToLearn)
            {
                if (kvp.Key <= Level)
                {
                    if (!SkillSet.Contains(kvp.Value))
                    {
                        SkillSet.Add(kvp.Value);
                    }
                }
            }
        }
    }
}
