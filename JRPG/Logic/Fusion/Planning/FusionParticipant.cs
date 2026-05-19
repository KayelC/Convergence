using JRPGPrototype.Core;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// Normalized view of a fusion material.
    /// Fusion still accepts both demon combatants and Wild Card Persona masks, but rules code
    /// should not need to repeatedly know which concrete source the player selected.
    /// </summary>
    public sealed class FusionParticipant
    {
        public object Source { get; }
        public Combatant CombatantView { get; }
        public Persona? PersonaSource { get; }

        public string Name => CombatantView.Name;
        public string SourceId => CombatantView.SourceId;
        public int Level => CombatantView.Level;
        public Persona? ActivePersona => CombatantView.ActivePersona;
        public string Race => ActivePersona?.Race ?? string.Empty;
        public int Rank => ActivePersona?.Rank ?? 0;

        private FusionParticipant(object source, Combatant combatantView, Persona? personaSource)
        {
            Source = source;
            CombatantView = combatantView;
            PersonaSource = personaSource;
        }

        public static FusionParticipant From(object source)
        {
            return source switch
            {
                Combatant combatant => new FusionParticipant(combatant, combatant, null),
                Persona persona => new FusionParticipant(persona, CreateTransientCombatant(persona), persona),
                _ => throw new ArgumentException("Fusion participants must be Combatants or Personas.", nameof(source))
            };
        }

        public static Combatant CreateTransientCombatant(Persona persona)
        {
            var transientPersona = new Persona
            {
                Name = persona.Name,
                Level = persona.Level,
                Race = persona.Race,
                Rank = persona.Rank,
                Exp = persona.Exp,
                LifetimeEarnedExp = persona.LifetimeEarnedExp
            };

            transientPersona.SkillSet.AddRange(persona.SkillSet);
            foreach (var stat in persona.StatModifiers)
            {
                transientPersona.StatModifiers[stat.Key] = stat.Value;
            }

            return new Combatant(persona.Name, ClassType.Demon)
            {
                Level = persona.Level,
                ActivePersona = transientPersona,
                SourceId = persona.Name,
                LifetimeEarnedExp = persona.LifetimeEarnedExp
            };
        }
    }
}
