using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JRPGPrototype.Data
{
    public interface ISkillRepository
    {
        SkillDefinition? GetById(string id);
        SkillDefinition? GetByDisplayName(string displayName);
        IReadOnlyList<SkillDefinition> GetAll();
    }

    public interface IEntityRepository
    {
        PersonaData? GetById(string id);
        IReadOnlyList<PersonaData> GetByRaceAndRank(string race, int rank);
        IReadOnlyList<PersonaData> GetAll();
    }

    public interface IAilmentRepository
    {
        AilmentData? GetByIdOrName(string idOrName);
        IReadOnlyList<AilmentData> GetAll();
    }

    public sealed class GameDataCatalog : ISkillRepository, IEntityRepository, IAilmentRepository
    {
        private readonly Dictionary<string, SkillDefinition> _skillsById;
        private readonly Dictionary<string, SkillDefinition> _skillsByDisplayName;
        private readonly Dictionary<string, PersonaData> _entitiesById;
        private readonly Dictionary<string, AilmentData> _ailmentsByIdOrName;
        private readonly IReadOnlyList<PersonaData> _entities;
        private readonly IReadOnlyList<AilmentData> _ailments;

        public GameDataCatalog(
            IEnumerable<SkillDefinition> skills,
            IEnumerable<PersonaData> entities,
            IEnumerable<AilmentData> ailments)
        {
            _skillsById = skills.ToDictionary(s => s.Id, s => s);
            _skillsByDisplayName = skills
                .GroupBy(s => s.DisplayName, System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), System.StringComparer.OrdinalIgnoreCase);

            _entities = entities.ToList();
            _entitiesById = _entities
                .Where(e => !string.IsNullOrWhiteSpace(e.Id))
                .ToDictionary(e => e.Id.ToLowerInvariant(), e => e);

            _ailments = ailments.ToList();
            _ailmentsByIdOrName = _ailments
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .ToDictionary(a => CreateStableId(a.Name), a => a);

            foreach (AilmentData ailment in _ailments.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
            {
                _ailmentsByIdOrName.TryAdd(ailment.Name, ailment);
            }
        }

        [Obsolete("Legacy Database bridge. Prefer JRPGPrototype.Data.Definitions.Catalogs.GameDataCatalog with clean schemas.")]
        public static GameDataCatalog FromDatabase()
        {
            AilmentData[] ailments = Database.Ailments.Values.ToArray();
#pragma warning disable CS0618
            SkillDefinition[] skills = Database.Skills.Values
                .Select(s => SkillDefinitionMapper.MapLegacySkill(s, ailments))
                .Where(r => r.IsValid && r.Definition != null)
                .Select(r => r.Definition!)
                .ToArray();
#pragma warning restore CS0618

            return new GameDataCatalog(skills, Database.Personas.Values, ailments);
        }

        SkillDefinition? ISkillRepository.GetById(string id)
        {
            return _skillsById.TryGetValue(id, out SkillDefinition? skill) ? skill : null;
        }

        SkillDefinition? ISkillRepository.GetByDisplayName(string displayName)
        {
            return _skillsByDisplayName.TryGetValue(displayName, out SkillDefinition? skill) ? skill : null;
        }

        IReadOnlyList<SkillDefinition> ISkillRepository.GetAll()
        {
            return _skillsById.Values.ToList();
        }

        PersonaData? IEntityRepository.GetById(string id)
        {
            return _entitiesById.TryGetValue(id.ToLowerInvariant(), out PersonaData? entity) ? entity : null;
        }

        IReadOnlyList<PersonaData> IEntityRepository.GetByRaceAndRank(string race, int rank)
        {
            return _entities
                .Where(e => e.Rank == rank && string.Equals(e.Race, race, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        IReadOnlyList<PersonaData> IEntityRepository.GetAll()
        {
            return _entities;
        }

        AilmentData? IAilmentRepository.GetByIdOrName(string idOrName)
        {
            return _ailmentsByIdOrName.TryGetValue(idOrName, out AilmentData? ailment) ? ailment : null;
        }

        IReadOnlyList<AilmentData> IAilmentRepository.GetAll()
        {
            return _ailments;
        }

        private static string CreateStableId(string displayName)
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
    }
}
