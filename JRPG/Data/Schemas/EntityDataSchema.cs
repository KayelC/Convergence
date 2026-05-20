using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;

namespace JRPGPrototype.Data.Definitions.Schemas
{
    public sealed record EntityDataSchema(IReadOnlyList<EntitySchemaEntry> Entities);

    public sealed record EntitySchemaEntry(
        string Id,
        string DisplayName,
        string Race,
        int Rank,
        int Level,
        string InheritanceType,
        IReadOnlyDictionary<string, int> Stats,
        IReadOnlyDictionary<string, string> Affinities,
        IReadOnlyList<string> BaseSkillIds,
        IReadOnlyDictionary<int, string> LearnedSkillIds)
    {
        public EntityDefinition ToDefinition()
        {
            var stats = new Dictionary<StatType, int>();
            foreach (KeyValuePair<string, int> stat in Stats)
            {
                if (!Enum.TryParse(stat.Key, true, out StatType statType))
                {
                    throw new InvalidOperationException($"Entity '{Id}' has unknown stat '{stat.Key}'.");
                }

                stats[statType] = stat.Value;
            }

            var affinities = new Dictionary<Element, Affinity>();
            foreach (KeyValuePair<string, string> affinity in Affinities)
            {
                Element element = ElementHelper.ParseElement(affinity.Key);
                if (element == Element.None)
                {
                    throw new InvalidOperationException($"Entity '{Id}' has unknown affinity element '{affinity.Key}'.");
                }

                affinities[element] = ElementHelper.ParseAffinity(affinity.Value);
            }

            return new EntityDefinition(
                Id,
                DisplayName,
                Race,
                Rank,
                Level,
                InheritanceType,
                stats,
                affinities,
                BaseSkillIds,
                LearnedSkillIds);
        }
    }

    public static class EntityDataSchemaValidator
    {
        public static SchemaValidationResult Validate(EntityDataSchema schema, IReadOnlyCollection<string> knownSkillIds)
        {
            var errors = new List<string>();

            foreach (EntitySchemaEntry entity in schema.Entities)
            {
                ValidateEntity(entity, knownSkillIds, errors);
            }

            AddDuplicateErrors(schema.Entities.Select(e => e.Id), "entity id", errors);
            AddDuplicateErrors(schema.Entities.Select(e => e.DisplayName), "entity display name", errors, StringComparer.OrdinalIgnoreCase);

            return new SchemaValidationResult(Array.Empty<string>(), errors);
        }

        private static void ValidateEntity(
            EntitySchemaEntry entity,
            IReadOnlyCollection<string> knownSkillIds,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(entity.Id)) errors.Add("Entity is missing Id.");
            if (string.IsNullOrWhiteSpace(entity.DisplayName)) errors.Add($"Entity '{entity.Id}' is missing DisplayName.");
            if (string.IsNullOrWhiteSpace(entity.InheritanceType)) errors.Add($"Entity '{entity.Id}' is missing InheritanceType.");

            foreach (string skillId in entity.BaseSkillIds)
            {
                if (!knownSkillIds.Contains(skillId))
                {
                    errors.Add($"Entity '{entity.Id}' has unresolved base skill '{skillId}'.");
                }
            }

            foreach (string skillId in entity.LearnedSkillIds.Values)
            {
                if (!knownSkillIds.Contains(skillId))
                {
                    errors.Add($"Entity '{entity.Id}' has unresolved learned skill '{skillId}'.");
                }
            }

            try
            {
                _ = entity.ToDefinition();
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
