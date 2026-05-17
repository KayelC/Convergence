# Core Subsystem

## Purpose

`Core` defines shared gameplay vocabulary and small value helpers used by every major subsystem. It is the lowest-level gameplay module: battle, field, fusion, entities, data, and services all rely on these enums and result types.

## Key Classes And Responsibilities

- `Enums.cs`: shared enums for elements, affinities, stats, hit results, dungeon event types, class types, controller states, personalities, exploration events, item usage signals, shop types, fusion operation types, and turn-start results.
- `ElementHelper.cs`: converts skill/equipment category strings into `Element` values and parses element/affinity strings from data.
- `CombatResult.cs`: carries the result of battle interactions, including damage, hit type, critical state, messages, and reflected damage context.

## Main Runtime Flows

- Data files use strings for categories, elements, and affinities; `ElementHelper` maps those strings into strongly typed enum values.
- Battle math and effects return `CombatResult` so `BattleConductor` and `PressTurnEngine` can decide how turn icons should change.
- `ItemUsageResult` lets field item execution tell `FieldConductor` whether an item merely applied, failed, or requested a dungeon exit.
- `FusionOperationType` lets `FusionCalculator` return an operation signal that `FusionMutator` can dispatch to the proper strategy.

## Important State And Invariants

- `Element.None` and `Element.Almighty` are special cases in battle affinity logic. `CombatMath.GetEffectiveAffinity` treats them as unresistable normal interactions.
- Physical elements are `Slash`, `Strike`, and `Pierce`; many calculations branch on this set.
- `Affinity` values affect Press Turn results: weak and critical chain, null and miss penalize, repel and absorb terminate.
- `TurnStartResult` is the contract between status processing and battle phase execution.

## Data Dependencies

Core itself does not load JSON, but JSON-backed systems depend on core names. Skill categories, weapon types, persona affinities, dungeon event strings, and fusion operation outcomes must remain consistent with the enum vocabulary.

## Extension Points

- Add a new element only after updating `ElementHelper`, affinity parsing, battle effect registration, passive-skill matching, JSON content, and UI display code.
- Add a new dungeon event only after updating `DungeonManager`, `ExplorationProcessor`, and `DungeonUIBridge`.
- Add a new fusion operation only after adding a strategy and registering it in `FusionStrategyRegistry`.

## Caveats

- Several systems still compare string names from JSON. New enum values do not automatically become supported behavior.
- Physical-element checks are repeated in multiple places; if the physical set changes, search for `Slash`, `Strike`, and `Pierce` branches.
