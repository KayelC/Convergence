using Newtonsoft.Json;
using System;

namespace JRPGPrototype.Data
{
    public class SkillData
    {
        [JsonProperty("Skill")]
        public string Name { get; set; }
        public string Effect { get; set; }
        public string Power { get; set; }
        public string Accuracy { get; set; }
        public string Cost { get; set; }
        public string Category { get; set; }

        [JsonProperty("is_inheritable")]
        public bool IsInheritable { get; set; } = true;

        [JsonProperty("family")]
        public string Family { get; set; } = "-";

        [JsonProperty("rank")]
        public string Rank { get; set; } = "1";

        public int GetPowerVal()
        {
            if (int.TryParse(Power, out int v)) return v;
            return 0;
        }

        public (int value, bool isPercentage, bool isHP) ParseCost()
        {
            if (string.IsNullOrEmpty(Cost)) return (0, false, false);
            bool isHp = Cost.Contains("HP");
            bool isPercent = Cost.Contains("%");
            string numPart = Cost.Replace("SP", "").Replace("HP", "").Replace("%", "").Trim();
            int.TryParse(numPart, out int val);
            return (val, isPercent, isHp);
        }

        /// <summary>
        /// Logic for identifying exclusive skills. 
        /// Note: Non-evolvable skills (Rank "-") are not necessarily exclusive.
        /// </summary>
        public bool IsExclusive()
        {
            return Effect.Contains("exclusive", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the skill is eligible for Rank Up/Down mutation.
        /// </summary>
        public bool CanEvolve()
        {
            return Rank != "-" && Family != "-" && !IsExclusive();
        }
    }
}