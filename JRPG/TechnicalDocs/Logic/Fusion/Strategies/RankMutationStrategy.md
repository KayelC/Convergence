# RankMutationStrategy

Source: [`Logic/Fusion/Strategies/RankMutationStrategy.cs`](../../../../Logic/Fusion/Strategies/RankMutationStrategy.cs)

## Purpose

`RankMutationStrategy` executes Element-driven rank up/down operations. The
calculator already chooses the target result ID, so this strategy focuses on
consuming optional sacrifice, creating the new form, carrying over selected
state, and replacing the original parent.

## Class Shape

```csharp
public class RankMutationStrategy : IFusionStrategy
{
    public void Execute(FusionContext context)

    private void ReplaceDemon(FusionContext context, Combatant oldD, Combatant newD)
    private void ReplacePersona(FusionContext context, Persona oldP, Persona newP)
}
```

## Operator Flow

Find the non-Element parent:

```csharp
Combatant original = (Combatant)context.Materials.First(m =>
    ((Combatant)m).ActivePersona.Race != "Element");
```

Consume optional sacrifice:

```csharp
if (context.Sacrifice is Combatant sacrificialCom)
{
    FusionInventoryTransaction.ConsumeDemon(context, sacrificialCom);
}
```

Create new ranked form:

```csharp
Combatant newD = CombatantFactory.CreatePlayerDemon(context.ResultId,
    Database.Personas[context.ResultId.ToLower()].Level);
```

Apply chosen skills and dedupe:

```csharp
newD.ExtraSkills.Clear();
newD.ExtraSkills.AddRange(context.ChosenSkills);
newD.ExtraSkills = newD.ExtraSkills.Distinct().ToList();
```

Carry stat modifiers:

```csharp
foreach (var mod in original.ActivePersona.StatModifiers)
{
    newD.ActivePersona.StatModifiers[mod.Key] = mod.Value;
}
```

Apply sacrifice EXP:

```csharp
if (context.Sacrifice is Combatant offer)
{
    int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
    newD.GainExp(transferXP);
}
```

Replace roster references:

```csharp
FusionInventoryTransaction.ReplaceDemon(context, oldD, newD);
```

## Wild Card Flow

Find non-Element Persona:

```csharp
Persona original = (Persona)context.Materials.First(m => ((Persona)m).Race != "Element");
```

Create new Persona:

```csharp
Persona newP = Database.Personas[context.ResultId.ToLower()].ToPersona();
```

Carry selected skills and modifiers:

```csharp
newP.SkillSet.Clear();
newP.SkillSet.AddRange(context.ChosenSkills);
newP.SkillSet = newP.SkillSet.Distinct().ToList();

foreach (var mod in original.StatModifiers) newP.StatModifiers[mod.Key] = mod.Value;
```

Replace active/stock Persona:

```csharp
FusionInventoryTransaction.ReplacePersona(context.Owner, oldP, newP);
```

## State And Mutation

Mutates optional sacrifice ownership, new form skill/modifier state, active party
or stock references, Persona stock, and owner resources through transaction
helpers.

## Invariants And Safety Rules

- The Element catalyst is not the transformed target.
- Stat modifiers carry over from original to new form.
- Rank mutation replaces the original; it does not add a second owned copy.
- Active and stock references must remain aligned.

## Tests And Verification

Covered by rank mutation replacement tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

## Refactor Notes

The strategy assumes valid material types and non-null active Persona data.
Future command validation should guarantee these before strategy execution.
