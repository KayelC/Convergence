using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;

namespace JRPGPrototype.Logic.Battle
{
    internal sealed class LegacyCombatPolicyAdapter
    {
        public static LegacyCombatPolicyAdapter Shared { get; } =
            new(new ProductionCombatRuleset(new LegacyRandomSource()));

        private readonly ProductionCombatRuleset _ruleset;

        internal LegacyCombatPolicyAdapter(ProductionCombatRuleset ruleset)
        {
            _ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
        }

        public int CalculateExpYield(Combatant enemy) =>
            _ruleset.CalculateExperienceYield(ToProfile(enemy));

        public int CalculateMaccaYield(Combatant enemy) =>
            _ruleset.CalculateCurrencyYield(ToProfile(enemy));

        public int CalculateDamage(
            Combatant attacker,
            Combatant target,
            int skillPower,
            Element element,
            out bool isCritical)
        {
            DamageElement damageElement = ToDamageElementOrMagicalFallback(element);
            ProductionRawDamageResult result = _ruleset.CalculateRawDamage(
                new ProductionRawDamageRequest(
                    ToProfile(attacker, outgoingElement: element),
                    ToProfile(target),
                    skillPower,
                    damageElement));

            isCritical = result.Critical;
            return (int)result.Damage;
        }

        public bool CheckHit(
            Combatant attacker,
            Combatant target,
            Element element,
            string skillAccuracy)
        {
            ProductionHitCheckResult result = _ruleset.CheckHit(new ProductionHitCheckRequest(
                ToProfile(attacker, outgoingElement: element, includeHitPassives: true),
                ToProfile(target, incomingElement: element, includeEvasionPassives: true),
                ParsePercent(skillAccuracy, _ruleset.Config.DefaultHitAccuracy)));
            return result.Hit;
        }

        public bool CalculateInstantKill(
            Combatant attacker,
            Combatant target,
            string skillAccuracy)
        {
            if (GetEffectiveAffinity(target, Element.Curse) == Affinity.Null)
            {
                return false;
            }

            ProductionInstantDeathResult result = _ruleset.ResolveInstantDeath(
                new ProductionInstantDeathRequest(
                    ToProfile(attacker),
                    ToProfile(target),
                    ParsePercent(skillAccuracy, _ruleset.Config.DefaultInstantDeathChance),
                    ResistanceLevel.Normal));
            return result.Defeated;
        }

        public int CalculateReflectedDamage(
            Combatant originalAttacker,
            int skillPower,
            Element element)
        {
            return CalculateDamage(originalAttacker, originalAttacker, skillPower, element, out _);
        }

        public int CalculateCritChance(Combatant attacker, Combatant target) =>
            _ruleset.CalculateCriticalChance(
                ToProfile(attacker, includeCriticalPassives: true),
                ToProfile(target));

        public Affinity GetEffectiveAffinity(Combatant target, Element element)
        {
            bool isPhysical = IsPhysical(element);
            if (isPhysical && target.PhysKarnActive)
            {
                return Affinity.Repel;
            }
            if (!isPhysical && target.MagicKarnActive && element != Element.Almighty)
            {
                return Affinity.Repel;
            }
            if (target.BrokenAffinities.ContainsKey(element))
            {
                return Affinity.Normal;
            }
            if (element == Element.Almighty || element == Element.None)
            {
                return Affinity.Normal;
            }

            Affinity baseAffinity = target.ActivePersona?.GetAffinity(element) ?? Affinity.Normal;
            if (target.IsGuarding && baseAffinity == Affinity.Weak)
            {
                baseAffinity = Affinity.Normal;
            }
            if (target.IsRigidBody && isPhysical &&
                baseAffinity is Affinity.Resist or Affinity.Null or Affinity.Repel or Affinity.Absorb)
            {
                baseAffinity = Affinity.Normal;
            }

            if (!LegacyCombatVocabularyAdapter.TryToDamageElement(element, out DamageElement damageElement))
            {
                return baseAffinity;
            }

            var profile = new JRPGPrototype.Entities.Components.CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(
                    damageElement,
                    LegacyCombatVocabularyAdapter.ToElementalAffinity(baseAffinity))]);
            ElementalAffinity resolved = ElementalAffinityResolver.Resolve(
                profile,
                damageElement,
                activeShields: ActiveShields(target),
                isBroken: target.BrokenAffinities.ContainsKey(element));
            return LegacyCombatVocabularyAdapter.ToLegacyAffinity(resolved);
        }

        public CombatResult ApplyDamage(
            Combatant target,
            int damage,
            Element element,
            bool isCritical)
        {
            ElementalAffinity affinity = LegacyCombatVocabularyAdapter.ToElementalAffinity(
                GetEffectiveAffinity(target, element));
            ProductionDamageApplicationResult applied = _ruleset.ApplyDamage(
                new ProductionDamageApplicationRequest(
                    ToProfile(target),
                    damage,
                    ToDamageElementOrMagicalFallback(element),
                    affinity,
                    isCritical));

            var result = new CombatResult
            {
                DamageDealt = (int)applied.DamageDealt,
                Type = ToLegacyHitType(applied.Outcome),
                IsCritical = applied.Critical,
                Message = applied.Message
            };

            if (applied.Affinity == ElementalAffinity.Repel)
            {
                return result;
            }
            if (applied.Affinity == ElementalAffinity.Absorb)
            {
                target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + (int)applied.Recovered);
                return result;
            }

            target.CurrentHP = Math.Max(0, target.CurrentHP - result.DamageDealt);
            RemoveAilmentOnHit(target, result);
            return result;
        }

        public bool RollInitiative(double playerAverageAgility, double enemyAverageAgility) =>
            _ruleset.RollInitiative((decimal)playerAverageAgility, (decimal)enemyAverageAgility);

        private static ProductionCombatantProfile ToProfile(
            Combatant combatant,
            Element? outgoingElement = null,
            Element? incomingElement = null,
            bool includeHitPassives = false,
            bool includeEvasionPassives = false,
            bool includeCriticalPassives = false)
        {
            decimal damageDealtMultiplier = combatant.CurrentAilment?.DamageDealMult is double dealt
                ? (decimal)dealt
                : 1m;
            if (outgoingElement is Element damageElement)
            {
                damageDealtMultiplier *= GetPassiveDamageMultiplier(combatant, damageElement);
            }

            decimal damageTakenMultiplier = combatant.CurrentAilment?.DamageTakenMult is double taken
                ? (decimal)taken
                : 1m;
            decimal hitMultiplier = includeHitPassives && combatant.GetConsolidatedSkills()
                .Contains("Vidyaraja's Blessing")
                    ? 1.15m
                    : 1m;
            decimal evasionMultiplier = includeEvasionPassives && incomingElement is Element evasionElement
                ? GetPassiveEvasionMultiplier(combatant, evasionElement)
                : 1m;
            decimal criticalMultiplier = includeCriticalPassives
                ? GetPassiveCriticalMultiplier(combatant)
                : 1m;

            return new ProductionCombatantProfile(
                Math.Max(1, combatant.Level),
                new ProductionCombatStats(
                    combatant.GetStat(StatType.St),
                    combatant.GetStat(StatType.Ma),
                    combatant.GetStat(StatType.Vi),
                    combatant.GetStat(StatType.Ag),
                    combatant.GetStat(StatType.Lu),
                    combatant.GetDefense()),
                new ProductionCombatStatus(
                    IsGuarding: combatant.IsGuarding,
                    IsRigidBody: combatant.IsRigidBody,
                    HasPhysicalCharge: combatant.IsCharged,
                    HasMagicalCharge: combatant.IsMindCharged),
                new ProductionCombatModifiers(
                    DamageDealtMultiplier: damageDealtMultiplier,
                    DamageTakenMultiplier: damageTakenMultiplier,
                    HitMultiplier: hitMultiplier,
                    EvasionMultiplier: evasionMultiplier,
                    CriticalChanceMultiplier: criticalMultiplier));
        }

        private static void RemoveAilmentOnHit(Combatant target, CombatResult result)
        {
            if (result.DamageDealt <= 0 || target.CurrentAilment?.RemovalTriggers is null ||
                !target.CurrentAilment.RemovalTriggers.Contains("OnHit"))
            {
                return;
            }

            string oldAilment = target.CurrentAilment.Name;
            target.RemoveAilment();
            result.Message = string.IsNullOrEmpty(result.Message)
                ? $"{target.Name} recovered from {oldAilment}!"
                : $"{result.Message} {target.Name} woke up!";
        }

        private static int ParsePercent(string value, int fallback)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Equals("-", StringComparison.Ordinal) ||
                value.Equals("NaN", StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            string clean = new(value.Where(char.IsDigit).ToArray());
            return int.TryParse(clean, out int parsed) ? parsed : fallback;
        }

        private static decimal GetPassiveDamageMultiplier(Combatant attacker, Element element)
        {
            decimal multiplier = 1m;
            List<string> skills = attacker.GetConsolidatedSkills();
            string elementName = element.ToString();

            if (skills.Any(skill => skill.Contains(elementName) && skill.Contains("Boost"))) multiplier *= 1.25m;
            if (skills.Any(skill => skill.Contains(elementName) && skill.Contains("Amp"))) multiplier *= 1.5m;
            if (skills.Any(skill => skill.Contains(elementName) && skill.Contains("Driver"))) multiplier *= 1.75m;

            bool isMagic = !IsPhysical(element) && element != Element.Almighty;
            if (isMagic && skills.Contains("Magic Ability")) multiplier *= 1.25m;

            return multiplier;
        }

        private static decimal GetPassiveEvasionMultiplier(Combatant target, Element element)
        {
            decimal multiplier = 1m;
            List<string> skills = target.GetConsolidatedSkills();
            string elementName = element.ToString();
            if (skills.Any(skill => skill.Contains("Dodge") && skill.Contains(elementName))) multiplier *= 0.85m;
            if (skills.Any(skill => skill.Contains("Evade") && skill.Contains(elementName))) multiplier *= 0.60m;
            return multiplier;
        }

        private static decimal GetPassiveCriticalMultiplier(Combatant attacker)
        {
            decimal multiplier = 1m;
            List<string> skills = attacker.GetConsolidatedSkills();
            if (skills.Contains("Apt Pupil")) multiplier *= 2m;
            if (skills.Contains("Rebellion")) multiplier *= 1.2m;
            return multiplier;
        }

        private static IEnumerable<ShieldKind> ActiveShields(Combatant target)
        {
            if (target.PhysKarnActive)
            {
                yield return ShieldKind.Physical;
            }
            if (target.MagicKarnActive)
            {
                yield return ShieldKind.Magical;
            }
        }

        private static DamageElement ToDamageElementOrMagicalFallback(Element element) =>
            LegacyCombatVocabularyAdapter.TryToDamageElement(element, out DamageElement damageElement)
                ? damageElement
                : DamageElement.Almighty;

        private static bool IsPhysical(Element element) =>
            element is Element.Slash or Element.Strike or Element.Pierce;

        private static HitType ToLegacyHitType(PressTurnOutcome outcome) => outcome switch
        {
            PressTurnOutcome.Weakness => HitType.Weakness,
            PressTurnOutcome.Miss => HitType.Miss,
            PressTurnOutcome.Null => HitType.Null,
            PressTurnOutcome.Repel => HitType.Repel,
            PressTurnOutcome.Absorb => HitType.Absorb,
            _ => HitType.Normal
        };

        private sealed class LegacyRandomSource : IRandomSource
        {
            private readonly Random _random = new();

            public int NextInt32(int minimumInclusive, int maximumExclusive) =>
                _random.Next(minimumInclusive, maximumExclusive);

            public decimal NextUnitDecimal() => (decimal)_random.NextDouble();
        }
    }
}
