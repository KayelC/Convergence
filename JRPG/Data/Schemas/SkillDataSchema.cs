using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;

namespace JRPGPrototype.Data.Definitions.Schemas
{
    public sealed record SkillDataSchema(IReadOnlyList<SkillSchemaEntry> Skills);

    public sealed record SkillSchemaEntry(
        string Id,
        string DisplayName,
        string Description,
        SkillKind Kind,
        SkillCostSchema Cost,
        SkillTargeting Targeting,
        SkillInheritanceSchema Inheritance,
        DamagePayloadSchema? Damage = null,
        HealingPayloadSchema? Healing = null,
        RevivePayloadSchema? Revive = null,
        AilmentPayloadSchema? Ailment = null,
        BuffDebuffPayloadSchema? BuffDebuff = null,
        ChargePayloadSchema? Charge = null,
        BreakPayloadSchema? Break = null,
        ShieldPayloadSchema? Shield = null,
        PassivePayloadSchema? Passive = null,
        SpecialPayloadSchema? Special = null)
    {
        public SkillDefinition ToDefinition()
        {
            SkillEffectPayload payload = Kind switch
            {
                SkillKind.Damage => Require(Damage, nameof(Damage)).ToPayload(),
                SkillKind.Healing => Require(Healing, nameof(Healing)).ToPayload(),
                SkillKind.Revive => Require(Revive, nameof(Revive)).ToPayload(),
                SkillKind.Ailment => Require(Ailment, nameof(Ailment)).ToPayload(),
                SkillKind.BuffDebuff => Require(BuffDebuff, nameof(BuffDebuff)).ToPayload(),
                SkillKind.Charge => Require(Charge, nameof(Charge)).ToPayload(),
                SkillKind.Break => Require(Break, nameof(Break)).ToPayload(),
                SkillKind.Shield => Require(Shield, nameof(Shield)).ToPayload(),
                SkillKind.Passive => Require(Passive, nameof(Passive)).ToPayload(),
                SkillKind.Special => Require(Special, nameof(Special)).ToPayload(),
                _ => throw new InvalidOperationException($"Unsupported skill kind '{Kind}'.")
            };

            return new SkillDefinition(
                Id,
                DisplayName,
                Description,
                Kind,
                Cost.ToDefinition(),
                Targeting,
                Inheritance.ToDefinition(),
                payload);
        }

        private T Require<T>(T? value, string propertyName) where T : class
        {
            return value ?? throw new InvalidOperationException(
                $"Skill '{Id}' is {Kind} but does not define a {propertyName} payload.");
        }
    }

    public sealed record SkillCostSchema(
        SkillCostResource Resource,
        int Amount,
        bool IsPercent = false)
    {
        public SkillCostDefinition ToDefinition()
        {
            return new SkillCostDefinition(Resource, Amount, IsPercent);
        }
    }

    public sealed record SkillInheritanceSchema(
        bool IsInheritable,
        string? Family = null,
        int? Rank = null,
        bool IsExclusive = false)
    {
        public SkillInheritanceDefinition ToDefinition()
        {
            return new SkillInheritanceDefinition(IsInheritable, Family, Rank, IsExclusive);
        }
    }

    public sealed record DamagePayloadSchema(
        Element Element,
        int Power,
        int Accuracy,
        int? CriticalChance = null,
        bool DrainsHp = false,
        bool DrainsSp = false,
        bool IsInstantKill = false,
        SecondaryAilmentSchema? SecondaryAilment = null)
    {
        public DamageSkillPayload ToPayload()
        {
            return new DamageSkillPayload(
                Element,
                Power,
                Accuracy,
                CriticalChance,
                DrainsHp,
                DrainsSp,
                IsInstantKill,
                SecondaryAilment?.ToDefinition());
        }
    }

    public sealed record SecondaryAilmentSchema(string AilmentId, int Chance)
    {
        public SecondaryAilmentDefinition ToDefinition()
        {
            return new SecondaryAilmentDefinition(AilmentId, Chance);
        }
    }

    public sealed record HealingPayloadSchema(
        RecoveryResource Resource,
        RecoveryAmountKind AmountKind,
        int Amount)
    {
        public HealingSkillPayload ToPayload()
        {
            return new HealingSkillPayload(Resource, AmountKind, Amount);
        }
    }

    public sealed record RevivePayloadSchema(RecoveryAmountKind AmountKind, int Amount)
    {
        public ReviveSkillPayload ToPayload()
        {
            return new ReviveSkillPayload(AmountKind, Amount);
        }
    }

    public sealed record AilmentPayloadSchema(string AilmentId, int Chance)
    {
        public AilmentSkillPayload ToPayload()
        {
            return new AilmentSkillPayload(AilmentId, Chance);
        }
    }

    public sealed record BuffDebuffPayloadSchema(IReadOnlyList<StatModifierTrack> Tracks, int StageDelta)
    {
        public BuffDebuffSkillPayload ToPayload()
        {
            return new BuffDebuffSkillPayload(Tracks, StageDelta);
        }
    }

    public sealed record ChargePayloadSchema(ChargeKind Kind, double Multiplier)
    {
        public ChargeSkillPayload ToPayload()
        {
            return new ChargeSkillPayload(Kind, Multiplier);
        }
    }

    public sealed record BreakPayloadSchema(Element Element, int Duration)
    {
        public BreakSkillPayload ToPayload()
        {
            return new BreakSkillPayload(Element, Duration);
        }
    }

    public sealed record ShieldPayloadSchema(ShieldKind Kind)
    {
        public ShieldSkillPayload ToPayload()
        {
            return new ShieldSkillPayload(Kind);
        }
    }

    public sealed record PassivePayloadSchema(string PassiveKind)
    {
        public PassiveSkillPayload ToPayload()
        {
            return new PassiveSkillPayload(PassiveKind);
        }
    }

    public sealed record SpecialPayloadSchema(string SpecialKind)
    {
        public SpecialSkillPayload ToPayload()
        {
            return new SpecialSkillPayload(SpecialKind);
        }
    }

    public static class SkillDataSchemaValidator
    {
        public static SchemaValidationResult Validate(SkillDataSchema schema)
        {
            var errors = new List<string>();

            foreach (SkillSchemaEntry skill in schema.Skills)
            {
                ValidateSkill(skill, errors);
            }

            AddDuplicateErrors(schema.Skills.Select(s => s.Id), "skill id", errors);
            AddDuplicateErrors(schema.Skills.Select(s => s.DisplayName), "skill display name", errors, StringComparer.OrdinalIgnoreCase);

            return new SchemaValidationResult(Array.Empty<string>(), errors);
        }

        private static void ValidateSkill(SkillSchemaEntry skill, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(skill.Id)) errors.Add("Skill is missing Id.");
            if (string.IsNullOrWhiteSpace(skill.DisplayName)) errors.Add($"Skill '{skill.Id}' is missing DisplayName.");

            int payloadCount = new object?[]
            {
                skill.Damage,
                skill.Healing,
                skill.Revive,
                skill.Ailment,
                skill.BuffDebuff,
                skill.Charge,
                skill.Break,
                skill.Shield,
                skill.Passive,
                skill.Special
            }.Count(payload => payload != null);

            if (payloadCount != 1)
            {
                errors.Add($"Skill '{skill.Id}' must define exactly one behavior payload.");
                return;
            }

            try
            {
                _ = skill.ToDefinition();
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
            }
        }

        private static void AddDuplicateErrors(
            IEnumerable<string> values,
            string label,
            List<string> errors,
            StringComparer? comparer = null)
        {
            comparer ??= StringComparer.Ordinal;
            foreach (var group in values.Where(v => !string.IsNullOrWhiteSpace(v)).GroupBy(v => v, comparer))
            {
                if (group.Count() > 1)
                {
                    errors.Add($"Duplicate {label}: '{group.Key}'.");
                }
            }
        }
    }
}
