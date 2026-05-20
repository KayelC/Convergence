# Fusion Strategies

Source folder: [`Logic/Fusion/Strategies`](../../../../Logic/Fusion/Strategies)

This folder contains committed Fusion operation implementations.

Detailed file docs:

- [IFusionStrategy](IFusionStrategy.md)
- [FusionStrategyRegistry](FusionStrategyRegistry.md)
- [StandardFusionStrategy](StandardFusionStrategy.md)
- [RankMutationStrategy](RankMutationStrategy.md)
- [StatBoostStrategy](StatBoostStrategy.md)

## Current Responsibility

Strategies mutate actual gameplay state after the conductor has built a
confirmed `FusionContext` and the mutator has selected an operation.

## Review Focus

- standard create-result fusion,
- Operator versus Wild Card material consumption,
- rank mutation replacement semantics,
- Mitama stat boost semantics,
- sacrificial EXP transfer,
- skill application and resource recalculation.
