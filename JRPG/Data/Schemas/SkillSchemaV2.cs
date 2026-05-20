using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;

namespace JRPGPrototype.Data.Schemas
{
    public sealed record SkillDatabaseV2(IReadOnlyList<SkillDefinitionDto> Skills);

    public sealed record SkillDefinitionDto(
        string Id,
        string DisplayName,
        string Description,
        SkillKind Kind,
        SkillCostDto Cost,
        SkillTargeting Targeting,
        SkillInheritanceDto Inheritance,
        DamagePayloadDto? Damage = null,
        HealingPayloadDto? Healing = null,
        RevivePayloadDto? Revive = null,
        AilmentPayloadDto? Ailment = null,
        BuffDebuffPayloadDto? BuffDebuff = null,
        ChargePayloadDto? Charge = null,
        BreakPayloadDto? Break = null,
        ShieldPayloadDto? Shield = null,
        PassivePayloadDto? Passive = null,
        SpecialPayloadDto? Special = null)
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

    public sealed record SkillCostDto(
        SkillCostResource Resource,
        int Amount,
        bool IsPercent = false)
    {
        public SkillCostDefinition ToDefinition()
        {
            return new SkillCostDefinition(Resource, Amount, IsPercent);
        }
    }

    public sealed record SkillInheritanceDto(
        bool IsInheritable,
        string? Family = null,
        int? Rank = null,
        bool IsExclusive = false);

    public static class SkillInheritanceDtoExtensions
    {
        public static SkillInheritanceDefinition ToDefinition(this SkillInheritanceDto dto)
        {
            return new SkillInheritanceDefinition(dto.IsInheritable, dto.Family, dto.Rank, dto.IsExclusive);
        }
    }

    public sealed record DamagePayloadDto(
        Element Element,
        int Power,
        int Accuracy,
        int? CriticalChance = null,
        bool DrainsHp = false,
        bool DrainsSp = false,
        bool IsInstantKill = false,
        SecondaryAilmentDto? SecondaryAilment = null)
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

    public sealed record SecondaryAilmentDto(string AilmentId, int Chance)
    {
        public SecondaryAilmentDefinition ToDefinition()
        {
            return new SecondaryAilmentDefinition(AilmentId, Chance);
        }
    }

    public sealed record HealingPayloadDto(
        RecoveryResource Resource,
        RecoveryAmountKind AmountKind,
        int Amount)
    {
        public HealingSkillPayload ToPayload()
        {
            return new HealingSkillPayload(Resource, AmountKind, Amount);
        }
    }

    public sealed record RevivePayloadDto(RecoveryAmountKind AmountKind, int Amount)
    {
        public ReviveSkillPayload ToPayload()
        {
            return new ReviveSkillPayload(AmountKind, Amount);
        }
    }

    public sealed record AilmentPayloadDto(string AilmentId, int Chance)
    {
        public AilmentSkillPayload ToPayload()
        {
            return new AilmentSkillPayload(AilmentId, Chance);
        }
    }

    public sealed record BuffDebuffPayloadDto(IReadOnlyList<StatModifierTrack> Tracks, int StageDelta)
    {
        public BuffDebuffSkillPayload ToPayload()
        {
            return new BuffDebuffSkillPayload(Tracks, StageDelta);
        }
    }

    public sealed record ChargePayloadDto(ChargeKind Kind, double Multiplier)
    {
        public ChargeSkillPayload ToPayload()
        {
            return new ChargeSkillPayload(Kind, Multiplier);
        }
    }

    public sealed record BreakPayloadDto(Element Element, int Duration)
    {
        public BreakSkillPayload ToPayload()
        {
            return new BreakSkillPayload(Element, Duration);
        }
    }

    public sealed record ShieldPayloadDto(ShieldKind Kind)
    {
        public ShieldSkillPayload ToPayload()
        {
            return new ShieldSkillPayload(Kind);
        }
    }

    public sealed record PassivePayloadDto(string PassiveKind)
    {
        public PassiveSkillPayload ToPayload()
        {
            return new PassiveSkillPayload(PassiveKind);
        }
    }

    public sealed record SpecialPayloadDto(string SpecialKind)
    {
        public SpecialSkillPayload ToPayload()
        {
            return new SpecialSkillPayload(SpecialKind);
        }
    }

    public static class SkillSchemaV2Validator
    {
        public static DataValidationResult Validate(SkillDatabaseV2 database)
        {
            var warnings = new List<string>();
            var errors = new List<string>();

            foreach (SkillDefinitionDto skill in database.Skills)
            {
                ValidateSkill(skill, errors);
            }

            AddDuplicateErrors(database.Skills.Select(s => s.Id), "skill id", errors);
            AddDuplicateErrors(database.Skills.Select(s => s.DisplayName), "skill display name", errors, StringComparer.OrdinalIgnoreCase);

            return new DataValidationResult(warnings, errors);
        }

        private static void ValidateSkill(SkillDefinitionDto skill, List<string> errors)
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
