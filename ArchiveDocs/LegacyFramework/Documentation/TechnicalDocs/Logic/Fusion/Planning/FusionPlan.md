# FusionPlan

Source: [`Logic/Fusion/Planning/FusionPlan.cs`](../../../../Logic/Fusion/Planning/FusionPlan.cs)

## Purpose

`FusionPlan` is the calculated ritual state after materials are selected but
before the ritual is committed. It keeps the conductor from recomputing target
IDs, inheritance pools, slot counts, and preview baseline data across multiple
menu loops.

## Class Shape

```csharp
public sealed class FusionPlan
{
    public FusionOperationType Operation { get; }
    public string TargetId { get; }
    public bool IsAccident { get; }
    public FusionParticipant FirstParent { get; }
    public FusionParticipant SecondParent { get; }
    public FusionParticipant? Sacrifice { get; }
    public IReadOnlyList<Combatant> CombatantMaterials { get; }
    public IReadOnlyList<string> InherentSkills { get; }
    public IReadOnlyList<string> PickableSkills { get; }
    public IReadOnlyList<string> ExclusiveSkills { get; }
    public IReadOnlyList<string> DisplaySkills { get; }
    public int MaxInheritanceSlots { get; }
    public Combatant PreviewBaseline { get; }
}
```

## Constructor

The constructor assigns every property directly:

```csharp
Operation = operation;
TargetId = targetId;
IsAccident = isAccident;
FirstParent = firstParent;
SecondParent = secondParent;
Sacrifice = sacrifice;
CombatantMaterials = combatantMaterials;
InherentSkills = inherentSkills;
PickableSkills = pickableSkills;
ExclusiveSkills = exclusiveSkills;
DisplaySkills = displaySkills;
MaxInheritanceSlots = maxInheritanceSlots;
PreviewBaseline = previewBaseline;
```

There is no behavior in this class. Its value is the shape and naming of the
ritual state.

## Important Fields

- `Operation`: selects the eventual strategy.
- `TargetId`: species/template ID used by preview and transaction.
- `IsAccident`: tells the conductor whether to replace chosen skills after
  confirmation.
- `CombatantMaterials`: combatant-shaped material list for inheritance math.
- `InherentSkills`: result skills that should be shown as already known.
- `PickableSkills`: legal inheritance pool.
- `ExclusiveSkills`: visible but disabled inheritance pool.
- `DisplaySkills`: combined pickable plus exclusive list for UI.
- `MaxInheritanceSlots`: final slot cap after sacrificial bonus.
- `PreviewBaseline`: parent used for before/after comparison.

## State And Mutation

`FusionPlan` is immutable after construction at the property level. It stores
read-only list interfaces, but callers should still treat contained objects as
live references.

## Invariants And Safety Rules

- Plans must be created only after a valid calculator result.
- `TargetId` should not be empty.
- `DisplaySkills` should include legal and disabled-visible skills.
- `PickableSkills` should be the only source for accident inheritance.
- `PreviewBaseline` should match the operation type.

## Tests And Verification

Covered by planning/preview assertions in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

## Refactor Notes

This class is close to a framework DTO, but it still carries current entity
types directly. Future extraction may split it into a pure rules plan and a
host/entity binding layer.
