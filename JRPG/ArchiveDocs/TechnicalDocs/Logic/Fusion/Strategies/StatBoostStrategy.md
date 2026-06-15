# StatBoostStrategy

Source: [`Logic/Fusion/Strategies/StatBoostStrategy.cs`](../../../../Logic/Fusion/Strategies/StatBoostStrategy.cs)

## Purpose

`StatBoostStrategy` executes Mitama stat boost fusion. It consumes the Mitama
catalyst, optionally consumes a sacrifice, creates a boosted replacement for the
target, applies Mitama stat modifiers, and replaces the target in roster state.

## Class Shape

```csharp
public class StatBoostStrategy : IFusionStrategy
{
    public void Execute(FusionContext context)

    private void ApplyBoosts(Persona target, string mitamaName, IFusionMessenger messenger)
    private void ReplaceDemon(FusionContext context, Combatant oldD, Combatant newD)
    private void ReplacePersona(FusionContext context, Persona oldP, Persona newP)
}
```

## Operator Flow

Identify target and Mitama:

```csharp
Combatant target = (Combatant)context.Materials.First(m =>
    ((Combatant)m).ActivePersona.Race != "Mitama");

Combatant mitama = (Combatant)context.Materials.First(m =>
    ((Combatant)m).ActivePersona.Race == "Mitama");

mitamaName = mitama.ActivePersona.Name;
```

Consume Mitama catalyst:

```csharp
FusionInventoryTransaction.ConsumeDemon(context, mitama);
```

This was one of the important bug fixes: Mitama is consumed separately from the
target and optional sacrifice.

Create boosted replacement at target level:

```csharp
Combatant boosted = CombatantFactory.CreatePlayerDemon(target.SourceId, target.Level);
boosted.Exp = target.Exp;
boosted.ExtraSkills.Clear();
boosted.ExtraSkills.AddRange(context.ChosenSkills);
```

Carry existing stat modifiers:

```csharp
foreach (var mod in target.ActivePersona.StatModifiers)
    boosted.ActivePersona.StatModifiers[mod.Key] = mod.Value;
```

Apply boost and replace:

```csharp
ApplyBoosts(boosted.ActivePersona, mitamaName, context.Messenger);
boosted.RecalculateResources();
FusionInventoryTransaction.ReplaceDemon(context, target, boosted);
```

## Wild Card Flow

Identifies target and Mitama Persona masks:

```csharp
Persona target = (Persona)context.Materials.First(m => ((Persona)m).Race != "Mitama");
Persona mitama = (Persona)context.Materials.First(m => ((Persona)m).Race == "Mitama");
```

Consumes the Mitama:

```csharp
FusionInventoryTransaction.ConsumePersona(context.Owner, mitama);
```

Rebuilds the target Persona from its template and restores current level/EXP:

```csharp
var template = Database.Personas.Values.First(p => p.Name == target.Name);
Persona newP = template.ToPersona();
newP.Level = target.Level;
newP.Exp = target.Exp;
newP.SkillSet.Clear();
newP.SkillSet.AddRange(context.ChosenSkills);
```

Carries modifiers and applies boost:

```csharp
foreach (var mod in target.StatModifiers) newP.StatModifiers[mod.Key] = mod.Value;
ApplyBoosts(newP, mitamaName, context.Messenger);
```

## `ApplyBoosts`

Mitama boost table:

```csharp
case "Ara Mitama": boosts.Add(StatType.St, 2); boosts.Add(StatType.Ag, 1); break;
case "Nigi Mitama": boosts.Add(StatType.Ma, 2); boosts.Add(StatType.Lu, 1); break;
case "Kusi Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Ag, 1); break;
case "Saki Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Lu, 1); break;
```

Cap and message logic:

```csharp
int current = target.StatModifiers.GetValueOrDefault(entry.Key, 0);
if (current < 40)
{
    target.StatModifiers[entry.Key] = Math.Min(40, current + entry.Value);
    messenger.Publish($" -> {entry.Key} increased by {entry.Value}!", ConsoleColor.Cyan);
}
else
{
    messenger.Publish($" -> {entry.Key} is already at its maximum!", ConsoleColor.Yellow);
}
```

## State And Mutation

Mutates Mitama ownership, optional sacrifice ownership, replacement target stats,
skills, EXP, active party/stock references, Persona stock, and owner resources.

## Invariants And Safety Rules

- The Mitama catalyst must be consumed.
- The target must remain owned as the boosted replacement.
- Boosted demon level should match target current level.
- Existing stat modifiers should carry forward before boost is applied.
- Stats are capped at `40`.

## Tests And Verification

Covered by Mitama preview and transaction tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

Manual smoke checks:

- target + Mitama previews target level,
- confirming consumes Mitama,
- target remains owned and boosted,
- optional sacrifice transfers EXP without preserving the sacrifice.

## Refactor Notes

Mitama boost data is duplicated with `FusionPreviewFactory`. Extract a shared
rule table before adding more boost/catalyst mechanics.
