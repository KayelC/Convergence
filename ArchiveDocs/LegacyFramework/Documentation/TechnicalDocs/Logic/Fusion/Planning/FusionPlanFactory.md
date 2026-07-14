# FusionPlanFactory

Source: [`Logic/Fusion/Planning/FusionPlanFactory.cs`](../../../../Logic/Fusion/Planning/FusionPlanFactory.cs)

## Purpose

`FusionPlanFactory` builds a complete non-mutating `FusionPlan` from selected
participants. It pulls together calculator output, inheritance pools,
sacrificial slot bonus, inherent skills, and preview baseline selection.

## Class Shape

```csharp
public sealed class FusionPlanFactory
{
    private readonly FusionCalculator _calculator;

    public bool TryCreate(
        FusionParticipant firstParent,
        FusionParticipant secondParent,
        FusionParticipant? sacrifice,
        bool isSacrificial,
        int moonPhase,
        out FusionPlan? plan)
}
```

## `TryCreate`

First, the factory asks the calculator for the operation:

```csharp
var (operation, targetId, isAccident) = _calculator.CalculateResult(
    firstParent.CombatantView,
    secondParent.CombatantView,
    moonPhase);

if (operation == FusionOperationType.NoFusionPossible || string.IsNullOrEmpty(targetId))
{
    return false;
}
```

Then it builds the combatant-shaped material list:

```csharp
List<Combatant> combatantMaterials = new List<Combatant>
{
    firstParent.CombatantView,
    secondParent.CombatantView
};

if (sacrifice != null)
{
    combatantMaterials.Add(sacrifice.CombatantView);
}
```

Inheritance data is calculated once:

```csharp
List<string> inherentSkills = GetInherentSkills(operation, targetId, firstParent.CombatantView, secondParent.CombatantView);
List<string> pickableSkills = _calculator.GetInheritableSkills(combatantMaterials.ToArray());
List<string> exclusiveSkills = _calculator.GetExclusiveSkills(combatantMaterials.ToArray());
List<string> displaySkills = pickableSkills.Union(exclusiveSkills).ToList();
```

Slot count includes sacrificial bonus and hard cap:

```csharp
int maxSlots = Math.Min(
    8,
    _calculator.GetInheritanceSlotCount(combatantMaterials.ToArray()) +
    (isSacrificial ? 2 : 0));
```

The cap reflects the current maximum skill list target of eight skills.

Preview baseline selection:

```csharp
Combatant previewBaseline = operation == FusionOperationType.StatBoostFusion
    ? FusionPreviewFactory.GetStatBoostTarget(firstParent.CombatantView, secondParent.CombatantView)
    : (firstParent.Race != "Element" ? firstParent.CombatantView : secondParent.CombatantView);
```

Stat boost compares against the non-Mitama target. Rank mutation compares
against the non-Element parent.

## `GetInherentSkills`

Stat boost uses the target's current consolidated skills:

```csharp
if (operation == FusionOperationType.StatBoostFusion)
{
    return FusionPreviewFactory.GetStatBoostTarget(parentA, parentB).GetConsolidatedSkills();
}
```

Create and rank operations use the target template's base skills:

```csharp
return Database.Personas.TryGetValue(targetId.ToLower(), out PersonaData? resultTemplate)
    ? resultTemplate.BaseSkills
    : new List<string>();
```

## State And Mutation

This factory does not mutate participants or game state. It creates lists and a
plan object.

## Invariants And Safety Rules

- Return `false` for no-fusion combinations.
- Do not call preview or mutator code here.
- Slot count must remain capped at eight.
- `DisplaySkills` should include exclusive skills for disabled UI display.
- `PickableSkills` must exclude exclusive skills.

## Tests And Verification

Covered by Fusion regression tests for preview and inheritance behavior in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

## Refactor Notes

This is a strong framework-core candidate after `FusionCalculator` gains data
repository and RNG seams.
