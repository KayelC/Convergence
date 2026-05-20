using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Data.Definitions.Schemas;

namespace JRPGPrototype.Data.Definitions.Catalogs
{
    public interface ISkillDefinitionRepository
    {
        SkillDefinition? GetById(string id);
        IReadOnlyList<SkillDefinition> GetAll();
    }

    public interface IEntityDefinitionRepository
    {
        EntityDefinition? GetById(string id);
        IReadOnlyList<EntityDefinition> GetByRaceAndRank(string race, int rank);
        IReadOnlyList<EntityDefinition> GetAll();
    }

    public sealed class GameDataCatalog : ISkillDefinitionRepository, IEntityDefinitionRepository
    {
        private readonly Dictionary<string, SkillDefinition> _skillsById;
        private readonly Dictionary<string, EntityDefinition> _entitiesById;
        private readonly IReadOnlyList<SkillDefinition> _skills;
        private readonly IReadOnlyList<EntityDefinition> _entities;

        private GameDataCatalog(
            IEnumerable<SkillDefinition> skills,
            IEnumerable<EntityDefinition> entities)
        {
            _skills = skills.ToList();
            _entities = entities.ToList();
            _skillsById = _skills.ToDictionary(s => s.Id, s => s);
            _entitiesById = _entities.ToDictionary(e => e.Id, e => e);
        }

        public static GameDataCatalog FromSchemas(SkillDataSchema skills, EntityDataSchema entities)
        {
            SchemaValidationResult skillValidation = SkillDataSchemaValidator.Validate(skills);
            if (!skillValidation.IsValid)
            {
                throw new InvalidOperationException(BuildValidationMessage("Skill schema is invalid", skillValidation));
            }

            string[] knownSkillIds = skills.Skills.Select(s => s.Id).ToArray();
            SchemaValidationResult entityValidation = EntityDataSchemaValidator.Validate(entities, knownSkillIds);
            if (!entityValidation.IsValid)
            {
                throw new InvalidOperationException(BuildValidationMessage("Entity schema is invalid", entityValidation));
            }

            return new GameDataCatalog(
                skills.Skills.Select(s => s.ToDefinition()),
                entities.Entities.Select(e => e.ToDefinition()));
        }

        SkillDefinition? ISkillDefinitionRepository.GetById(string id)
        {
            return _skillsById.TryGetValue(id, out SkillDefinition? skill) ? skill : null;
        }

        IReadOnlyList<SkillDefinition> ISkillDefinitionRepository.GetAll()
        {
            return _skills;
        }

        EntityDefinition? IEntityDefinitionRepository.GetById(string id)
        {
            return _entitiesById.TryGetValue(id, out EntityDefinition? entity) ? entity : null;
        }

        IReadOnlyList<EntityDefinition> IEntityDefinitionRepository.GetByRaceAndRank(string race, int rank)
        {
            return _entities
                .Where(e => e.Rank == rank && string.Equals(e.Race, race, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        IReadOnlyList<EntityDefinition> IEntityDefinitionRepository.GetAll()
        {
            return _entities;
        }

        private static string BuildValidationMessage(string header, SchemaValidationResult validation)
        {
            return $"{header}:{Environment.NewLine}{string.Join(Environment.NewLine, validation.Errors)}";
        }
    }
}
