# FusionParticipant

Source: [`Logic/Fusion/Planning/FusionParticipant.cs`](../../../../Logic/Fusion/Planning/FusionParticipant.cs)

## Purpose

`FusionParticipant` normalizes a selected Fusion material. The current Fusion
surface accepts both Operator demons (`Combatant`) and Wild Card Persona masks
(`Persona`). This wrapper lets planning and preview code read a consistent
combatant-shaped view without repeating casts everywhere.

## Class Shape

```csharp
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
}
```

## Important Methods

### `From`

```csharp
public static FusionParticipant From(object source)
{
    return source switch
    {
        Combatant combatant => new FusionParticipant(combatant, combatant, null),
        Persona persona => new FusionParticipant(persona, CreateTransientCombatant(persona), persona),
        _ => throw new ArgumentException("Fusion participants must be Combatants or Personas.", nameof(source))
    };
}
```

For demons, the combatant is already the source and the combatant view.

For Personas, the source remains the original `Persona`, but the combatant view
is a transient demon-shaped wrapper.

### `CreateTransientCombatant`

```csharp
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
```

The transient combatant exists so shared calculator and preview logic can read
`ActivePersona`, race, rank, skills, level, and lifetime EXP consistently.

## State And Mutation

This class does not mutate the source participant. Persona conversion creates a
new transient Persona and Combatant.

## Invariants And Safety Rules

- `Source` must remain the original selected object.
- `CombatantView` must be safe for rule reads.
- Persona stat modifiers and skills must be copied into the transient view.
- Unsupported source types should fail loudly.

## Tests And Verification

Covered indirectly by Fusion planning and transaction tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

## Refactor Notes

This is a bridge toward typed framework commands. Eventually `object Source`
should be replaced by a formal participant union or separate Operator/Wild Card
command models.
