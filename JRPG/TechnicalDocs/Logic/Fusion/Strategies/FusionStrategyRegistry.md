# FusionStrategyRegistry

Source: [`Logic/Fusion/Strategies/FusionStrategyRegistry.cs`](../../../../Logic/Fusion/Strategies/FusionStrategyRegistry.cs)

## Purpose

`FusionStrategyRegistry` maps `FusionOperationType` values to concrete
`IFusionStrategy` implementations.

## Class Shape

```csharp
public class FusionStrategyRegistry
{
    private readonly Dictionary<FusionOperationType, IFusionStrategy> _strategies = new();

    public FusionStrategyRegistry()
    public IFusionStrategy? GetStrategy(FusionOperationType type)
}
```

## Constructor

```csharp
_strategies[FusionOperationType.CreateNewDemon] = new StandardFusionStrategy();
_strategies[FusionOperationType.RankUpParent] = new RankMutationStrategy();
_strategies[FusionOperationType.RankDownParent] = new RankMutationStrategy();
_strategies[FusionOperationType.StatBoostFusion] = new StatBoostStrategy();
```

Rank up and rank down share the same strategy because the target result has
already been calculated by `FusionCalculator`.

## `GetStrategy`

```csharp
public IFusionStrategy? GetStrategy(FusionOperationType type)
{
    return _strategies.GetValueOrDefault(type);
}
```

Missing strategies return `null`; the mutator publishes the error.

## State And Mutation

The registry owns strategy instances but does not mutate gameplay state.

## Invariants And Safety Rules

- Every executable operation should have a registered strategy.
- `NoFusionPossible` should not need a strategy because it should be filtered
  before transaction execution.

## Refactor Notes

Ruleset modularity may eventually move this mapping into a Press Turn / One More
or game-mode configuration layer.
