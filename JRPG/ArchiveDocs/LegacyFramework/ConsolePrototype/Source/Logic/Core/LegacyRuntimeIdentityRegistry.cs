using System.Text;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Core
{
    internal sealed class LegacyRuntimeIdentityRegistry
    {
        public static LegacyRuntimeIdentityRegistry Shared { get; } = new();

        private readonly Dictionary<object, RuntimeInstanceId> _ids = new(ReferenceEqualityComparer.Instance);
        private readonly object _sync = new();
        private int _nextActor = 1;
        private int _nextForm = 1;

        public RuntimeInstanceId GetActorId(Combatant actor) =>
            GetOrCreate(actor, "legacy:actor_", ref _nextActor);

        public RuntimeInstanceId GetPersonaId(Persona persona) =>
            GetOrCreate(persona, "legacy:form_", ref _nextForm);

        public RuntimeActorReferenceSnapshot ActorReference(Combatant actor) =>
            new(GetActorId(actor), ToContentId(actor.SourceId, actor.Name, "runtime_actor"), Display(actor.Name, actor.SourceId, "Actor"));

        public RuntimeActorReferenceSnapshot PersonaReference(Persona persona) =>
            new(GetPersonaId(persona), ToContentId(persona.Name, persona.Race, "runtime_form"), Display(persona.Name, persona.Race, "Persona"));

        private RuntimeInstanceId GetOrCreate(object instance, string prefix, ref int nextValue)
        {
            lock (_sync)
            {
                if (_ids.TryGetValue(instance, out RuntimeInstanceId existing))
                {
                    return existing;
                }

                RuntimeInstanceId created = RuntimeInstanceId.Parse(prefix + nextValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                nextValue++;
                _ids.Add(instance, created);
                return created;
            }
        }

        private static ContentId ToContentId(string? preferred, string? fallback, string defaultValue)
        {
            string raw = !string.IsNullOrWhiteSpace(preferred)
                ? preferred
                : !string.IsNullOrWhiteSpace(fallback)
                    ? fallback
                    : defaultValue;

            var builder = new StringBuilder(raw.Length);
            bool previousUnderscore = false;
            foreach (char character in raw.Trim().ToLowerInvariant())
            {
                bool valid = character is >= 'a' and <= 'z' or >= '0' and <= '9';
                if (valid)
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
            if (string.IsNullOrEmpty(normalized))
            {
                normalized = defaultValue;
            }

            return ContentId.Parse(normalized);
        }

        private static string Display(string? preferred, string? fallback, string defaultValue) =>
            !string.IsNullOrWhiteSpace(preferred)
                ? preferred
                : !string.IsNullOrWhiteSpace(fallback)
                    ? fallback
                    : defaultValue;
    }
}
