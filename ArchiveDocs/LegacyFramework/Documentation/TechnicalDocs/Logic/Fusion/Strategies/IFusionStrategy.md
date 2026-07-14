# IFusionStrategy

Source: [`Logic/Fusion/Strategies/IFusionStrategy.cs`](../../../../Logic/Fusion/Strategies/IFusionStrategy.cs)

## Purpose

`IFusionStrategy` is the common execution contract for committed Fusion
operations.

## Interface Shape

```csharp
public interface IFusionStrategy
{
    void Execute(FusionContext context);
}
```

## Runtime Role

`FusionMutator` selects a strategy from `FusionStrategyRegistry` and calls
`Execute` only after the player has confirmed the ritual and duplicate guards
have passed.

## State And Mutation

The interface itself has no state. Implementations mutate party, stock, Persona,
EXP, stats, skills, or resources according to the operation.

## Invariants And Safety Rules

- Strategies should assume the ritual is confirmed.
- Strategies should not show menus or ask for input.
- Strategies should publish feedback through `context.Messenger`.
- Shared roster mutation should use `FusionInventoryTransaction` where possible.

## Refactor Notes

Future strategies should probably return typed transaction results instead of
using mutation plus messenger output only.
