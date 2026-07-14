# StandardFusionStrategy

Source: [`Logic/Fusion/Strategies/StandardFusionStrategy.cs`](../../../../Logic/Fusion/Strategies/StandardFusionStrategy.cs)

## Purpose

`StandardFusionStrategy` executes create-result Fusion operations. It consumes
the selected parents, creates a new demon or Persona from the target template,
applies chosen skills, transfers sacrifice EXP when present, and adds the result
to the owner's roster.

## Class Shape

```csharp
public class StandardFusionStrategy : IFusionStrategy
{
    public void Execute(FusionContext context)

    private void ExecuteOperatorFusion(FusionContext context)
    private void ExecuteWildCardFusion(FusionContext context)
}
```

## `Execute`

Branches by owner class:

```csharp
if (context.Owner.Class == ClassType.Operator)
{
    ExecuteOperatorFusion(context);
}
else if (context.Owner.Class == ClassType.WildCard)
{
    ExecuteWildCardFusion(context);
}
```

Operators produce demon combatants. Wild Cards produce Persona masks.

## Operator Flow

Gather and consume materials:

```csharp
List<Combatant> allParticipants = context.Materials.Cast<Combatant>().ToList();
if (context.Sacrifice is Combatant sacrificialCom)
    allParticipants.Add(sacrificialCom);

foreach (var participant in allParticipants)
{
    FusionInventoryTransaction.ConsumeDemon(context, participant);
}
```

Create child from template level:

```csharp
Combatant child = CombatantFactory.CreatePlayerDemon(context.ResultId,
    Database.Personas[context.ResultId.ToLower()].Level);
```

Apply chosen inherited skills:

```csharp
child.ExtraSkills.Clear();
child.ExtraSkills.AddRange(context.ChosenSkills);
```

Apply sacrificial EXP:

```csharp
if (context.Sacrifice is Combatant offer)
{
    int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
    child.GainExp(transferXP);
}
```

Finalize resources and placement:

```csharp
child.RecalculateResources();
child.CurrentHP = child.MaxHP;
child.CurrentSP = child.MaxSP;

if (!context.Party.SummonDemon(context.Owner, child))
    context.Owner.DemonStock.Add(child);
```

The child is summoned if active party has space; otherwise it enters stock.

## Wild Card Flow

Consume Persona materials:

```csharp
List<Persona> materials = context.Materials.Cast<Persona>().ToList();
foreach (var persona in materials)
{
    FusionInventoryTransaction.ConsumePersona(context.Owner, persona);
}

if (context.Sacrifice is Persona sacrificialPer)
{
    FusionInventoryTransaction.ConsumePersona(context.Owner, sacrificialPer);
}
```

Create and skill the child:

```csharp
Persona child = Database.Personas[context.ResultId.ToLower()].ToPersona();
child.SkillSet.Clear();
child.SkillSet.AddRange(context.ChosenSkills);
```

Apply sacrificial EXP and add to stock:

```csharp
if (context.Sacrifice is Persona offer)
{
    int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
    child.GainExp(transferXP);
}

context.Owner.PersonaStock.Add(child);
if (context.Owner.ActivePersona == null) context.Owner.ActivePersona = child;
context.Owner.RecalculateResources();
```

## State And Mutation

Mutates:

- consumed demon or Persona materials,
- active party through transaction helper,
- demon stock or Persona stock,
- child skills and EXP,
- owner resources.

## Invariants And Safety Rules

- Duplicate create-result guard must run before this strategy.
- Operator materials must be combatants.
- Wild Card materials must be Personas.
- Sacrifice is consumed before child creation.
- Child HP/SP should be restored after recalculation.

## Tests And Verification

Covered by standard fusion transaction tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

## Refactor Notes

The strategy still performs direct `Database.Personas[...]` lookups. Future
framework extraction should inject target templates through the plan or a data
repository.
