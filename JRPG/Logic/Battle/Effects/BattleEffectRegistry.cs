using System;
using System.Collections.Generic;
using JRPGPrototype.Core;
using JRPGPrototype.Logic.Battle;           // For CombatMath and CombatResult
using JRPGPrototype.Logic.Battle.Engines;   // For StatusRegistry and BattleKnowledge
using JRPGPrototype.Logic.Battle.Messaging; // For IBattleMessenger

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// The Central Strategy Hub.
    /// Maps JSON string identifiers (Type/Category) to concrete IBattleEffect implementations.
    /// This pattern ensures the ActionProcessor remains blind to specific logic implementations.
    /// </summary>
    public class BattleEffectRegistry
    {
        private readonly Dictionary<string, IBattleEffect> _effects = new Dictionary<string, IBattleEffect>(StringComparer.OrdinalIgnoreCase);

        public BattleEffectRegistry()
        {
            InitializeRegistry();
        }

        private void InitializeRegistry()
        {
            // 1. Recovery & Utility Strategies (Mapping Item 'Type' and Skill 'Category')
            _effects["Recovery"] = new RecoveryEffect();
            _effects["Healing"] = new HealEffect();
            _effects["Healing_All"] = new HealEffect();
            _effects["Revive"] = new ReviveEffect();
            _effects["Cure"] = new CureEffect();
            _effects["Spirit"] = new SpiritEffect();
            _effects["Enhance"] = new BuffEffect();
            _effects["Dekaja"] = new DekajaEffect();
            _effects["Dekunda"] = new DekundaEffect();
            _effects["Charge"] = new ChargeEffect();
            _effects["Shield"] = new ShieldEffect();
            _effects["Break"] = new BreakEffect();
            _effects["Ailment"] = new AilmentEffect();

            // 2. Damage Elements (Mapping Skill 'Category' to specific DamageEffect instances)
            // We pass the Element into the constructor so one class can handle all types.
            _effects["Slash"] = new DamageEffect(Element.Slash);
            _effects["Strike"] = new DamageEffect(Element.Strike);
            _effects["Pierce"] = new DamageEffect(Element.Pierce);
            _effects["Fire"] = new DamageEffect(Element.Fire);
            _effects["Ice"] = new DamageEffect(Element.Ice);
            _effects["Elec"] = new DamageEffect(Element.Elec);
            _effects["Wind"] = new DamageEffect(Element.Wind);
            _effects["Earth"] = new DamageEffect(Element.Earth);
            _effects["Light"] = new DamageEffect(Element.Light);
            _effects["Dark"] = new DamageEffect(Element.Dark);
            _effects["Almighty"] = new DamageEffect(Element.Almighty);
        }

        /// <summary>
        /// Retrieves the logic strategy associated with a data key.
        /// Performs string cleaning and fuzzy matching to maintain legacy data compatibility.
        /// </summary>
        public IBattleEffect? GetEffect(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey)) return null;

            // 1. Try direct match
            if (_effects.TryGetValue(effectKey, out var strategy))
            {
                return strategy;
            }

            // 2. Clean the key (e.g., "Ailment Skills" -> "Ailment") and try again
            string cleanKey = CleanKey(effectKey);
            if (_effects.TryGetValue(cleanKey, out strategy))
            {
                return strategy;
            }

            // 3. Final fallback: Manual search for substring
            foreach (var key in _effects.Keys)
            {
                if (effectKey.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return _effects[key];
                }
            }

            return null;
        }

        // Removes common suffixes from JSON data strings to allow better dictionary mapping.
        private string CleanKey(string input)
        {
            return input.Replace("Skills", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("Skill", "", StringComparison.OrdinalIgnoreCase)
                        .Trim();
        }
    }
}