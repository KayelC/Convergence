using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JRPGPrototype.Core;

namespace JRPGPrototype.Data
{
    [Obsolete("Legacy compatibility result for SkillData conversion. Prefer Data.Schemas v2 DTOs as the source of truth.")]
    public sealed record SkillDefinitionMappingResult(
        SkillDefinition? Definition,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Errors)
    {
        public bool IsValid => Definition != null && Errors.Count == 0;
    }

    [Obsolete("Legacy compatibility adapter for current skills_database.json only. Do not build new systems on this parser.")]
    public static class SkillDefinitionMapper
    {
        public static SkillDefinitionMappingResult MapLegacySkill(
            SkillData skill,
            IEnumerable<AilmentData>? ailments = null)
        {
            var warnings = new List<string>();
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(skill.Name))
            {
                errors.Add("Skill is missing a display name.");
                return new SkillDefinitionMappingResult(null, warnings, errors);
            }

            SkillKind kind = InferKind(skill);
            SkillEffectPayload? payload = CreatePayload(skill, kind, ailments, warnings, errors);
            if (payload == null)
            {
                return new SkillDefinitionMappingResult(null, warnings, errors);
            }

            var definition = new SkillDefinition(
                CreateId(skill.Name),
                skill.Name,
                skill.Effect ?? string.Empty,
                kind,
                CreateCost(skill),
                InferTargeting(skill, kind),
                CreateInheritance(skill),
                payload);

            return new SkillDefinitionMappingResult(definition, warnings, errors);
        }

        public static string CreateId(string displayName)
        {
            var builder = new StringBuilder();
            bool previousWasSeparator = false;

            foreach (char c in displayName.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }

        private static SkillKind InferKind(SkillData skill)
        {
            string name = skill.Name ?? string.Empty;
            string effect = skill.Effect ?? string.Empty;
            string category = skill.Category ?? string.Empty;
            string nameLower = name.ToLowerInvariant();
            string effectLower = effect.ToLowerInvariant();
            string categoryLower = category.ToLowerInvariant();
            bool hasPower = TryParsePositiveInt(skill.Power, out _);

            if (categoryLower.Contains("passive")) return SkillKind.Passive;
            if (nameLower.Contains("charge")) return SkillKind.Charge;
            if (nameLower.Contains("break")) return SkillKind.Break;
            if (nameLower.Contains("tetra") || nameLower.Contains("makara")) return SkillKind.Shield;
            if (effectLower.Contains("revive")) return SkillKind.Revive;
            if (IsBuffOrDebuffName(nameLower)) return SkillKind.BuffDebuff;
            if (hasPower) return SkillKind.Damage;
            if (categoryLower.Contains("recovery")) return SkillKind.Healing;
            if (categoryLower.Contains("mind") || categoryLower.Contains("nerve") || categoryLower.Contains("curse")) return SkillKind.Ailment;

            return SkillKind.Special;
        }

        private static SkillEffectPayload? CreatePayload(
            SkillData skill,
            SkillKind kind,
            IEnumerable<AilmentData>? ailments,
            List<string> warnings,
            List<string> errors)
        {
            return kind switch
            {
                SkillKind.Damage => CreateDamagePayload(skill, ailments, warnings, errors),
                SkillKind.Healing => CreateHealingPayload(skill, errors),
                SkillKind.Revive => CreateRevivePayload(skill, errors),
                SkillKind.Ailment => CreateAilmentPayload(skill, ailments, errors),
                SkillKind.BuffDebuff => CreateBuffDebuffPayload(skill),
                SkillKind.Charge => CreateChargePayload(skill),
                SkillKind.Break => CreateBreakPayload(skill, errors),
                SkillKind.Shield => CreateShieldPayload(skill),
                SkillKind.Passive => new PassiveSkillPayload(NormalizeOptional(skill.Family) ?? CreateId(skill.Name)),
                _ => new SpecialSkillPayload(CreateId(skill.Name))
            };
        }

        private static SkillEffectPayload? CreateDamagePayload(
            SkillData skill,
            IEnumerable<AilmentData>? ailments,
            List<string> warnings,
            List<string> errors)
        {
            if (!TryParsePositiveInt(skill.Power, out int power))
            {
                errors.Add($"Damage skill '{skill.Name}' is missing numeric power.");
                return null;
            }

            if (!TryParsePercent(skill.Accuracy, out int accuracy))
            {
                errors.Add($"Damage skill '{skill.Name}' is missing numeric accuracy.");
                return null;
            }

            int? critical = TryParsePercent(skill.Critical, out int parsedCritical) ? parsedCritical : null;
            string effect = skill.Effect ?? string.Empty;

            return new DamageSkillPayload(
                ElementHelper.FromCategory(skill.Category),
                power,
                accuracy,
                critical,
                effect.Contains("Drains HP", StringComparison.OrdinalIgnoreCase),
                effect.Contains("Drains SP", StringComparison.OrdinalIgnoreCase),
                effect.Contains("instant kill", StringComparison.OrdinalIgnoreCase),
                TryCreateSecondaryAilment(effect, ailments, warnings));
        }

        private static SkillEffectPayload? CreateHealingPayload(SkillData skill, List<string> errors)
        {
            string effect = skill.Effect ?? string.Empty;
            RecoveryResource resource = effect.Contains("SP", StringComparison.OrdinalIgnoreCase) ||
                                        effect.Contains("Spirit", StringComparison.OrdinalIgnoreCase)
                ? RecoveryResource.SP
                : RecoveryResource.HP;

            if (TryCreateRecoveryAmount(effect, skill.GetPowerVal(), out var amountKind, out int amount))
            {
                return new HealingSkillPayload(resource, amountKind, amount);
            }

            errors.Add($"Healing skill '{skill.Name}' has no inferable recovery amount.");
            return null;
        }

        private static SkillEffectPayload? CreateRevivePayload(SkillData skill, List<string> errors)
        {
            if (TryCreateRecoveryAmount(skill.Effect ?? string.Empty, skill.GetPowerVal(), out var amountKind, out int amount))
            {
                return new ReviveSkillPayload(amountKind, amount);
            }

            errors.Add($"Revive skill '{skill.Name}' has no inferable recovery amount.");
            return null;
        }

        private static SkillEffectPayload? CreateAilmentPayload(
            SkillData skill,
            IEnumerable<AilmentData>? ailments,
            List<string> errors)
        {
            SecondaryAilmentDefinition? ailment = TryCreateSecondaryAilment(skill.Effect ?? string.Empty, ailments, new List<string>());
            if (ailment != null)
            {
                return new AilmentSkillPayload(ailment.AilmentId, ailment.Chance);
            }

            errors.Add($"Ailment skill '{skill.Name}' has no inferable ailment/chance.");
            return null;
        }

        private static SkillEffectPayload CreateBuffDebuffPayload(SkillData skill)
        {
            string nameLower = (skill.Name ?? string.Empty).ToLowerInvariant();
            int delta = nameLower.EndsWith("nda") || nameLower == "debilitate" ? -1 : 1;
            var tracks = new List<StatModifierTrack>();

            if (nameLower.Contains("taru") || nameLower == "heat riser" || nameLower == "debilitate")
            {
                tracks.Add(StatModifierTrack.PhysAtk);
            }

            if (nameLower.Contains("maka") || nameLower == "heat riser" || nameLower == "debilitate")
            {
                tracks.Add(StatModifierTrack.MagAtk);
            }

            if (nameLower.Contains("raku") || nameLower == "heat riser" || nameLower == "debilitate")
            {
                tracks.Add(StatModifierTrack.Defense);
            }

            if (nameLower.Contains("suku") || nameLower == "heat riser" || nameLower == "debilitate")
            {
                tracks.Add(StatModifierTrack.Agility);
            }

            return new BuffDebuffSkillPayload(tracks, delta);
        }

        private static SkillEffectPayload CreateChargePayload(SkillData skill)
        {
            string nameLower = (skill.Name ?? string.Empty).ToLowerInvariant();
            ChargeKind kind = nameLower.Contains("mind") ? ChargeKind.Magical : ChargeKind.Physical;
            return new ChargeSkillPayload(kind, 1.9);
        }

        private static SkillEffectPayload? CreateBreakPayload(SkillData skill, List<string> errors)
        {
            Element element = ElementHelper.ParseElement((skill.Name ?? string.Empty).Replace(" Break", "", StringComparison.OrdinalIgnoreCase));
            if (element == Element.None)
            {
                errors.Add($"Break skill '{skill.Name}' has no inferable element.");
                return null;
            }

            return new BreakSkillPayload(element, 3);
        }

        private static SkillEffectPayload CreateShieldPayload(SkillData skill)
        {
            string nameLower = (skill.Name ?? string.Empty).ToLowerInvariant();
            ShieldKind kind = nameLower.Contains("makara") ? ShieldKind.Magical : ShieldKind.Physical;
            return new ShieldSkillPayload(kind);
        }

        private static SkillCostDefinition CreateCost(SkillData skill)
        {
            string costText = skill.Cost ?? string.Empty;
            if (string.IsNullOrWhiteSpace(costText) || costText.Trim() == "-")
            {
                return SkillCostDefinition.None;
            }

            var parsed = skill.ParseCost();
            SkillCostResource resource = parsed.isHP ? SkillCostResource.HP : SkillCostResource.SP;
            return new SkillCostDefinition(resource, parsed.value, parsed.isPercentage);
        }

        private static SkillInheritanceDefinition CreateInheritance(SkillData skill)
        {
            int? rank = int.TryParse(skill.Rank, out int parsedRank) ? parsedRank : null;
            return new SkillInheritanceDefinition(
                skill.IsInheritable,
                NormalizeOptional(skill.Family),
                rank,
                skill.IsExclusive());
        }

        private static SkillTargeting InferTargeting(SkillData skill, SkillKind kind)
        {
            if (kind == SkillKind.Charge) return SkillTargeting.Self;

            string nameLower = (skill.Name ?? string.Empty).ToLowerInvariant();
            string effectLower = (skill.Effect ?? string.Empty).ToLowerInvariant();
            bool isAll = nameLower.StartsWith("ma") ||
                         nameLower.StartsWith("me") ||
                         effectLower.Contains("all foes") ||
                         effectLower.Contains("all allies") ||
                         effectLower.Contains("party") ||
                         nameLower == "debilitate";

            bool targetsAllies = kind is SkillKind.Healing or SkillKind.Revive or SkillKind.Charge or SkillKind.Shield ||
                                  nameLower.EndsWith("kaja") ||
                                  effectLower.Contains("ally") ||
                                  effectLower.Contains("allies") ||
                                  effectLower.Contains("party");

            if (kind == SkillKind.Revive)
            {
                return isAll ? SkillTargeting.AllDeadAllies : SkillTargeting.DeadAlly;
            }

            return targetsAllies
                ? (isAll ? SkillTargeting.AllAllies : SkillTargeting.SingleAlly)
                : (isAll ? SkillTargeting.AllEnemies : SkillTargeting.SingleEnemy);
        }

        private static SecondaryAilmentDefinition? TryCreateSecondaryAilment(
            string effect,
            IEnumerable<AilmentData>? ailments,
            List<string> warnings)
        {
            if (ailments == null) return null;

            AilmentData? match = ailments.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name) &&
                effect.Contains(a.Name, StringComparison.OrdinalIgnoreCase));

            if (match == null) return null;

            int chance = 100;
            Match chanceWithAilment = Regex.Match(effect, @$"(\d+)%\s+{Regex.Escape(match.Name)}", RegexOptions.IgnoreCase);
            Match genericChance = Regex.Match(effect, @"\((\d+)%");

            if (chanceWithAilment.Success)
            {
                chance = int.Parse(chanceWithAilment.Groups[1].Value);
            }
            else if (genericChance.Success)
            {
                chance = int.Parse(genericChance.Groups[1].Value);
            }
            else
            {
                warnings.Add($"Ailment '{match.Name}' was inferred without an explicit chance; defaulting to 100.");
            }

            return new SecondaryAilmentDefinition(CreateId(match.Name), chance);
        }

        private static bool TryCreateRecoveryAmount(
            string effect,
            int power,
            out RecoveryAmountKind kind,
            out int amount)
        {
            kind = RecoveryAmountKind.Flat;
            amount = 0;

            if (effect.Contains("full", StringComparison.OrdinalIgnoreCase) ||
                effect.Contains("fully", StringComparison.OrdinalIgnoreCase) ||
                power >= 9999)
            {
                kind = RecoveryAmountKind.Full;
                amount = 100;
                return true;
            }

            Match percent = Regex.Match(effect, @"(\d+)%");
            if (percent.Success)
            {
                kind = RecoveryAmountKind.Percent;
                amount = int.Parse(percent.Groups[1].Value);
                return true;
            }

            Match flat = Regex.Match(effect, @"\((\d+)\)");
            if (flat.Success)
            {
                amount = int.Parse(flat.Groups[1].Value);
                return true;
            }

            if (power > 0)
            {
                amount = power;
                return true;
            }

            return false;
        }

        private static bool IsBuffOrDebuffName(string nameLower)
        {
            return nameLower.EndsWith("kaja") ||
                   nameLower.EndsWith("nda") ||
                   nameLower == "heat riser" ||
                   nameLower == "debilitate";
        }

        private static bool TryParsePositiveInt(string? value, out int result)
        {
            return int.TryParse(value, out result) && result > 0;
        }

        private static bool TryParsePercent(string? value, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value) || value == "-") return false;

            string digits = new string(value.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out result);
        }

        private static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "-") return null;
            return value;
        }
    }
}
