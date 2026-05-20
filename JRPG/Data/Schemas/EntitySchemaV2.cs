using System.Collections.Generic;

namespace JRPGPrototype.Data.Schemas
{
    public sealed record EntityDatabaseV2(IReadOnlyList<EntityDefinitionDto> Entities);

    public sealed record EntityDefinitionDto(
        string Id,
        string DisplayName,
        string Race,
        int Rank,
        int Level,
        string InheritanceType,
        IReadOnlyDictionary<string, int> Stats,
        IReadOnlyDictionary<string, string> Affinities,
        IReadOnlyList<string> BaseSkills,
        IReadOnlyDictionary<int, string> LearnedSkills);

    public static class EntitySchemaV2Validator
    {
        public static DataValidationResult Validate(EntityDatabaseV2 database, IReadOnlyCollection<string> knownSkillIds)
        {
            var errors = new List<string>();

            foreach (EntityDefinitionDto entity in database.Entities)
            {
                if (string.IsNullOrWhiteSpace(entity.Id)) errors.Add("Entity is missing Id.");
                if (string.IsNullOrWhiteSpace(entity.DisplayName)) errors.Add($"Entity '{entity.Id}' is missing DisplayName.");
                if (string.IsNullOrWhiteSpace(entity.InheritanceType)) errors.Add($"Entity '{entity.Id}' is missing InheritanceType.");

                foreach (string skillId in entity.BaseSkills)
                {
                    if (!knownSkillIds.Contains(skillId))
                    {
                        errors.Add($"Entity '{entity.Id}' has unresolved base skill '{skillId}'.");
                    }
                }

                foreach (string skillId in entity.LearnedSkills.Values)
                {
                    if (!knownSkillIds.Contains(skillId))
                    {
                        errors.Add($"Entity '{entity.Id}' has unresolved learned skill '{skillId}'.");
                    }
                }
            }

            return new DataValidationResult(System.Array.Empty<string>(), errors);
        }
    }
}
