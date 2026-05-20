# FusionContext

Source: [`Logic/Fusion/FusionContext.cs`](../../../Logic/Fusion/FusionContext.cs)

## Purpose

`FusionContext` is the transaction payload passed from `FusionConductor` to
`FusionMutator` and then into the selected `IFusionStrategy`.

It packages the owner, selected materials, optional sacrifice, inherited skills,
target result ID, messenger, and party manager into one object so strategy
implementations receive the same transaction shape.

## Runtime Role

The context is created only after the player confirms a ritual and after
accident skill replacement has been applied. At that point, the ritual has moved
from preview/planning into transaction execution.

Strategies use the context to decide what to consume, what to create or replace,
what messages to publish, and how to update party/stock state.

## Key Collaborators

- `FusionConductor`: creates the context.
- `FusionMutator`: receives the context and dispatches it to a strategy.
- `IFusionStrategy`: consumes the context to perform a specific transaction.
- `FusionInventoryTransaction`: helper used by strategies to consume or replace
  roster entries.
- `IFusionMessenger`: publishes transaction feedback.
- `PartyManager`: updates active party and stock state.

## Important Members

Current full implementation:

```csharp
public class FusionContext
{
    public Combatant Owner { get; }
    public List<object> Materials { get; }
    public object? Sacrifice { get; }
    public List<string> ChosenSkills { get; }
    public string ResultId { get; }
    public IFusionMessenger Messenger { get; }
    public PartyManager Party { get; }

    public FusionContext(
        Combatant owner,
        List<object> materials,
        object? sacrifice,
        List<string> chosenSkills,
        string resultId,
        IFusionMessenger messenger,
        PartyManager party)
    {
        Owner = owner;
        Materials = materials;
        Sacrifice = sacrifice;
        ChosenSkills = chosenSkills;
        ResultId = resultId;
        Messenger = messenger;
        Party = party;
    }
}
```

This is intentionally simple, but it also exposes the current type-safety
problem: `Materials` and `Sacrifice` are still `object` because Fusion supports
both demon `Combatant` values and Wild Card `Persona` values.

### `Owner`

The player or owning combatant performing the ritual. Strategies inspect the
owner class to distinguish Operator and Wild Card behavior.

### `Materials`

The selected ritual parents. This list uses `object` because the current Fusion
surface still supports both `Combatant` demons and `Persona` masks.

For Operators, materials are expected to be `Combatant` instances. For Wild
Cards, materials are expected to be `Persona` instances.

### `Sacrifice`

Optional third participant for sacrificial fusion. The type follows the same
Operator/Wild Card pattern as `Materials`.

### `ChosenSkills`

The final inherited skill list passed to execution. This may differ from the
player-selected list when a fusion accident occurs.

### `ResultId`

The target species/template ID calculated during planning. Strategies use this
to instantiate or replace the result.

### `Messenger`

The transaction feedback channel. Strategies publish messages here instead of
writing directly to the console.

### `Party`

The active party/stock manager used by strategy and transaction helpers.

## State And Mutation

`FusionContext` itself is a data carrier. It does not mutate state.

The objects referenced by the context can be mutated by strategy execution. This
is why the context should only be created after confirmation, not during preview.

## Invariants And Safety Rules

- Context creation means the player has committed the ritual.
- `ChosenSkills` should already reflect accident replacement when applicable.
- `ResultId` should be non-empty and should come from a valid `FusionPlan`.
- Material object types must match the owner class.

## Data Dependencies

`FusionContext` has no direct database dependency. Database lookups happen in
the planner, preview factory, or strategies.

## Failure / Cancel / Edge Behavior

Cancel and unavailable states should be resolved before a context is created.
If a strategy receives an invalid context, it generally fails by missing casts,
missing templates, or no-op behavior depending on the strategy.

This is an area for future hardening.

## Tests And Verification

The context is covered indirectly by Fusion transaction tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../Convergence.Tests/FusionBugRegressionTests.cs).

Important coverage paths:

- duplicate create-result does not consume parents,
- standard fusion consumes allowed parents,
- stat boost consumes Mitama catalyst,
- rank mutation replaces active and stock references.

## Refactor Notes

This type is a likely future replacement target. Once Fusion contracts are more
explicit, `Materials` and `Sacrifice` should stop using `object` and become a
typed transaction request built from `FusionParticipant` or a more formal
framework command.
