using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Data
{
    public sealed record DataValidationResult(
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    public static class DataValidation
    {
        [Obsolete("Legacy validation for current SkillData/PersonaData JSON only. Prefer Data.Definitions clean schema validators.")]
        public static DataValidationResult ValidateLegacyData(
            IEnumerable<SkillData> skills,
            IEnumerable<PersonaData> entities,
            IEnumerable<AilmentData> ailments)
        {
            var warnings = new List<string>();
            var errors = new List<string>();
            SkillData[] skillList = skills.ToArray();
            PersonaData[] entityList = entities.ToArray();
            AilmentData[] ailmentList = ailments.ToArray();

            foreach (SkillData skill in skillList)
            {
                SkillDefinitionMappingResult result = SkillDefinitionMapper.MapLegacySkill(skill, ailmentList);
                warnings.AddRange(result.Warnings);
                errors.AddRange(result.Errors);
            }

            AddDuplicateErrors(
                skillList.Select(s => SkillDefinitionMapper.CreateId(s.Name ?? string.Empty)),
                "skill id",
                errors);

            AddDuplicateErrors(
                skillList.Select(s => s.Name ?? string.Empty),
                "skill display name",
                errors,
                StringComparer.OrdinalIgnoreCase);

            var skillNames = skillList
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (PersonaData entity in entityList)
            {
                if (string.IsNullOrWhiteSpace(entity.InheritanceType))
                {
                    errors.Add($"Entity '{entity.Id ?? entity.Name}' is missing InheritanceType.");
                }

                foreach (string skillName in entity.BaseSkills ?? Enumerable.Empty<string>())
                {
                    if (!skillNames.Contains(skillName))
                    {
                        errors.Add($"Entity '{entity.Id ?? entity.Name}' has unresolved base skill '{skillName}'.");
                    }
                }

                foreach (string skillName in entity.LearnedSkillsRaw?.Values ?? Enumerable.Empty<string>())
                {
                    if (!skillNames.Contains(skillName))
                    {
                        errors.Add($"Entity '{entity.Id ?? entity.Name}' has unresolved learned skill '{skillName}'.");
                    }
                }
            }

            return new DataValidationResult(warnings, errors);
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
