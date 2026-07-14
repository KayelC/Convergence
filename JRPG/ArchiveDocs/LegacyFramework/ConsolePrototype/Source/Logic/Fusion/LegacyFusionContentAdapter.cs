using System.Globalization;
using System.Text;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Fusion
{
    internal sealed class LegacyFusionContentAdapter : IFusionContentRepository
    {
        public static LegacyFusionContentAdapter Shared { get; } = new();

        private static readonly ContentId Strength = ContentId.Parse("strength");
        private static readonly ContentId Magic = ContentId.Parse("magic");
        private static readonly ContentId Vitality = ContentId.Parse("vitality");
        private static readonly ContentId Agility = ContentId.Parse("agility");
        private static readonly ContentId Luck = ContentId.Parse("luck");

        public IEnumerable<FusionRecipeSnapshot> GetRecipes() =>
            Database.FusionRecipes.Select(recipe => new FusionRecipeSnapshot(
                ToParentSelector(recipe.ParentA),
                ToParentSelector(recipe.ParentB),
                AccidentPolicyId: LegacyFusionStrategyPolicies.AccidentPolicyId,
                MutationPolicyId: LegacyFusionStrategyPolicies.MutationPolicyId,
                CompatibilityResultToken: ToResultToken(recipe.Result)));

        public bool TryGetEntity(ContentId entityId, out FusionEntitySnapshot? entity)
        {
            PersonaData? data = Database.Personas.Values.FirstOrDefault(persona =>
                ToContentId(persona.Id) == entityId ||
                ToContentId(persona.Name) == entityId);
            entity = data is null ? null : ToEntity(data);
            return entity is not null;
        }

        public IReadOnlyList<FusionEntitySnapshot> GetEntitiesByRace(ContentId raceId) =>
            Database.Personas.Values
                .Where(persona => ToContentId(persona.Race) == raceId)
                .OrderBy(persona => persona.Level)
                .Select(ToEntity)
                .ToArray();

        public bool TryGetSkill(ContentId skillId, out SkillDefinition? skill)
        {
            SkillData? data = Database.Skills.Values.FirstOrDefault(candidate => ToContentId(candidate.Name) == skillId);
            skill = data is null ? null : ToSkill(data);
            return skill is not null;
        }

        public IReadOnlyList<SkillDefinition> GetSkills() =>
            Database.Skills.Values.Select(ToSkill).ToArray();

        public FusionParticipantSnapshot ToParticipant(FusionParticipant participant)
        {
            ArgumentNullException.ThrowIfNull(participant);
            return participant.PersonaSource is not null
                ? ToParticipant(participant.PersonaSource)
                : ToParticipant(participant.CombatantView);
        }

        public FusionParticipantSnapshot ToParticipant(Combatant combatant)
        {
            ArgumentNullException.ThrowIfNull(combatant);
            Persona? form = combatant.ActivePersona;
            ContentId entityId = ResolveEntityId(combatant.SourceId, form?.Name);
            return new FusionParticipantSnapshot(
                LegacyRuntimeIdentityRegistry.Shared.GetActorId(combatant),
                entityId,
                combatant.Name,
                ToContentId(form?.Race ?? "unknown"),
                Math.Max(0, form?.Rank ?? 0),
                Math.Max(1, combatant.Level),
                combatant.GetConsolidatedSkills().Select(ToContentId),
                ToStats(combatant.CharacterStats, form?.StatModifiers),
                combatant.Exp,
                combatant.LifetimeEarnedExp);
        }

        public FusionParticipantSnapshot ToParticipant(Persona persona)
        {
            ArgumentNullException.ThrowIfNull(persona);
            ContentId entityId = ResolveEntityId(persona.Name, persona.Race);
            return new FusionParticipantSnapshot(
                LegacyRuntimeIdentityRegistry.Shared.GetPersonaId(persona),
                entityId,
                persona.Name,
                ToContentId(persona.Race),
                Math.Max(0, persona.Rank),
                Math.Max(1, persona.Level),
                persona.SkillSet.Select(ToContentId),
                ToStats(null, persona.StatModifiers),
                persona.Exp,
                persona.LifetimeEarnedExp);
        }

        public string SkillName(ContentId skillId)
        {
            SkillData? data = Database.Skills.Values.FirstOrDefault(candidate => ToContentId(candidate.Name) == skillId);
            return data?.Name ?? LegacyContentIdCodec.Decode(skillId);
        }

        public string EntityId(ContentId entityId)
        {
            PersonaData? data = Database.Personas.Values.FirstOrDefault(persona =>
                ToContentId(persona.Id) == entityId ||
                ToContentId(persona.Name) == entityId);
            return data?.Id ?? LegacyContentIdCodec.Decode(entityId);
        }

        public ContentId ContentIdForSkill(string skillName) => ToContentId(skillName);
        public ContentId ContentIdForEntity(string entityIdOrName) => ResolveEntityId(entityIdOrName, null);

        public static ContentId ToContentId(string value)
        {
            string normalized = Normalize(value);
            return ContentId.TryParse(normalized, out ContentId id)
                ? id
                : LegacyContentIdCodec.Encode(value);
        }

        private static string ToResultToken(string result) =>
            result is "1" or "-1" ? result : ToContentId(result).ToString();

        private static FusionRecipeParentSelectorSnapshot ToParentSelector(string value)
        {
            PersonaData? entity = Database.Personas.Values.FirstOrDefault(persona =>
                persona.Id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                persona.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
            return entity is null
                ? new FusionRecipeParentSelectorSnapshot(
                    FusionParentSelectorKind.Race,
                    ToContentId(value))
                : new FusionRecipeParentSelectorSnapshot(
                    FusionParentSelectorKind.Entity,
                    ToContentId(entity.Id));
        }

        private static ContentId ResolveEntityId(string? preferred, string? fallback)
        {
            string raw = !string.IsNullOrWhiteSpace(preferred)
                ? preferred
                : !string.IsNullOrWhiteSpace(fallback)
                    ? fallback
                    : "unknown_entity";

            PersonaData? data = Database.Personas.Values.FirstOrDefault(persona =>
                persona.Id.Equals(raw, StringComparison.OrdinalIgnoreCase) ||
                persona.Name.Equals(raw, StringComparison.OrdinalIgnoreCase));
            return data is null ? ToContentId(raw) : ToContentId(data.Id);
        }

        private static FusionEntitySnapshot ToEntity(PersonaData data)
        {
            var stats = new List<KeyValuePair<ContentId, int>>();
            if (data.RawStats is not null)
            {
                foreach ((string key, int value) in data.RawStats)
                {
                    stats.Add(new KeyValuePair<ContentId, int>(ToStatId(key), value));
                }
            }

            var definition = new EntityDefinition(
                ToContentId(data.Id),
                data.Name,
                string.Empty,
                ContentId.Parse("demon"),
                ToContentId(data.Race),
                data.Rank,
                Math.Max(1, data.Level),
                new EntityCapabilitiesDefinition(true, true, true),
                new EntityInheritanceRulesDefinition(new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
                stats,
                baseSkillIds: (data.BaseSkills ?? []).Select(ToContentId));
            return new FusionEntitySnapshot(definition);
        }

        private static SkillDefinition ToSkill(SkillData data)
        {
            bool passive = IsPassive(data);
            bool inheritable = data.IsInheritable && !data.IsExclusive();
            SkillMutationDefinition? mutation = null;
            if (data.CanEvolve() && int.TryParse(data.Rank, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tier))
            {
                mutation = new SkillMutationDefinition(ToContentId(data.Family), tier);
            }

            return new SkillDefinition(
                ToContentId(data.Name),
                data.Name,
                data.Effect ?? string.Empty,
                passive ? SkillActivation.Passive : SkillActivation.Active,
                passive ? null : ToMenuGroup(data),
                passive ? InheritanceGroup.Passive : ToInheritanceGroup(data),
                new SkillInheritanceDefinition(inheritable),
                mutation);
        }

        private static bool IsPassive(SkillData data) =>
            data.Category?.Contains("passive", StringComparison.OrdinalIgnoreCase) == true ||
            data.Effect?.Contains("passive", StringComparison.OrdinalIgnoreCase) == true ||
            data.Name?.Contains("Boost", StringComparison.OrdinalIgnoreCase) == true ||
            data.Name?.Contains("Master", StringComparison.OrdinalIgnoreCase) == true ||
            data.Name?.Contains("Resist", StringComparison.OrdinalIgnoreCase) == true ||
            data.Name?.Contains("Null", StringComparison.OrdinalIgnoreCase) == true ||
            data.Name?.Contains("Repel", StringComparison.OrdinalIgnoreCase) == true ||
            data.Name?.Contains("Absorb", StringComparison.OrdinalIgnoreCase) == true;

        private static SkillMenuGroup ToMenuGroup(SkillData data)
        {
            string text = $"{data.Category} {data.Effect} {data.Name}";
            if (text.Contains("heal", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("restore", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("revive", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("cure", StringComparison.OrdinalIgnoreCase))
            {
                return SkillMenuGroup.Recovery;
            }

            if (text.Contains("buff", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("taru", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("raku", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("suku", StringComparison.OrdinalIgnoreCase))
            {
                return SkillMenuGroup.Buff;
            }

            if (text.Contains("debuff", StringComparison.OrdinalIgnoreCase))
            {
                return SkillMenuGroup.Debuff;
            }

            if (text.Contains("poison", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("panic", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("fear", StringComparison.OrdinalIgnoreCase))
            {
                return SkillMenuGroup.Ailment;
            }

            return SkillMenuGroup.Offense;
        }

        private static InheritanceGroup ToInheritanceGroup(SkillData data)
        {
            string text = $"{data.Category} {data.Effect} {data.Name}";
            if (text.Contains("fire", StringComparison.OrdinalIgnoreCase) || text.Contains("agi", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Fire;
            if (text.Contains("ice", StringComparison.OrdinalIgnoreCase) || text.Contains("bufu", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Ice;
            if (text.Contains("elec", StringComparison.OrdinalIgnoreCase) || text.Contains("zio", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Electric;
            if (text.Contains("wind", StringComparison.OrdinalIgnoreCase) || text.Contains("garu", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Wind;
            if (text.Contains("light", StringComparison.OrdinalIgnoreCase) || text.Contains("hama", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Light;
            if (text.Contains("dark", StringComparison.OrdinalIgnoreCase) || text.Contains("mudo", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Dark;
            if (text.Contains("almighty", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Almighty;
            if (text.Contains("heal", StringComparison.OrdinalIgnoreCase) || text.Contains("dia", StringComparison.OrdinalIgnoreCase) || text.Contains("patra", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Recovery;
            if (text.Contains("poison", StringComparison.OrdinalIgnoreCase) || text.Contains("ailment", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Ailment;
            if (text.Contains("buff", StringComparison.OrdinalIgnoreCase) || text.Contains("debuff", StringComparison.OrdinalIgnoreCase)) return InheritanceGroup.Support;
            return InheritanceGroup.Physical;
        }

        private static IEnumerable<KeyValuePair<ContentId, int>> ToStats(
            Dictionary<StatType, int>? characterStats,
            Dictionary<StatType, int>? formStats)
        {
            Dictionary<ContentId, int> stats = [];
            foreach ((StatType stat, int value) in characterStats ?? [])
            {
                stats[ToStatId(stat)] = value;
            }

            foreach ((StatType stat, int value) in formStats ?? [])
            {
                stats[ToStatId(stat)] = value;
            }

            return stats;
        }

        private static ContentId ToStatId(string key) =>
            Enum.TryParse(key, true, out StatType stat) ? ToStatId(stat) : ToContentId(key);

        private static ContentId ToStatId(StatType stat) => stat switch
        {
            StatType.St => Strength,
            StatType.Ma => Magic,
            StatType.Vi => Vitality,
            StatType.Ag => Agility,
            StatType.Lu => Luck,
            _ => ToContentId(stat.ToString())
        };

        private static string Normalize(string value)
        {
            var builder = new StringBuilder((value ?? string.Empty).Length);
            bool previousUnderscore = false;
            foreach (char character in (value ?? string.Empty).Trim().ToLowerInvariant())
            {
                if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    builder.Append(character);
                    previousUnderscore = false;
                }
                else if (!previousUnderscore)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }
            }

            string normalized = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(normalized) ? "legacy_unknown" : normalized;
        }
    }

    internal sealed class LegacyFusionRandomSource : IRandomSource
    {
        private readonly Random _random;

        public LegacyFusionRandomSource(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int NextInt32(int minimumInclusive, int maximumExclusive) =>
            _random.Next(minimumInclusive, maximumExclusive);

        public decimal NextUnitDecimal() => (decimal)_random.NextDouble();
    }
}
