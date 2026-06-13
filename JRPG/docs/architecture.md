# Architecture

> **Status: Current implementation reference.** This document describes the console prototype. Approved redesign documents override it when defining target behavior.

JRPGPrototype is organized around gameplay subsystems rather than a generic application framework. The important architectural rule is separation of orchestration, rules, interaction, and output.

## Layers And Patterns

### Conductors

Conductors own high-level workflows and decide which subsystem should act next.

- `FieldConductor` runs the city, dungeon, inventory, status, party organization, and fusion entry loops.
- `BattleConductor` runs the encounter lifecycle, phase loop, actor turns, completion checks, and reward/cleanup flow.
- `FusionConductor` runs Cathedral menus, participant selection, result staging, inheritance choice, confirmation, accidents, and compendium actions.

Conductors should remain workflow coordinators. When adding new rules, prefer placing rule logic in an engine, processor, strategy, or registry.

### Engines And Processors

Engines and processors own deterministic rules or bounded state mutations.

- `CombatMath` calculates damage, accuracy, crit chance, initiative, EXP, and Macca yields.
- `PressTurnEngine` owns the full/blinking turn icon state machine.
- `StatusRegistry` owns ailment application, turn-start restrictions, passive startup effects, buff/debuff handling, cures, and redundancy checks.
- `ActionProcessor` executes attacks, skills, items, persona swaps, and analysis by delegating to effect strategies.
- `FieldServiceEngine`, `ShopEngine`, and `ExplorationProcessor` own field-side service, shop, item, skill, equipment, and dungeon traversal rules.
- `FusionCalculator`, `FusionMutator`, and fusion strategies own fusion prediction and state mutation.
- `StatProcessor`, `GrowthProcessor`, `DamageHandler`, and `CombatantFactory` keep entity logic outside the `Combatant` data shell.

### Bridges

Bridges are interactive console UI adapters. They turn game state into menus and return user choices to conductors.

- Battle: `InteractionBridge`.
- Field: `ServiceUIBridge`, `DungeonUIBridge`, `InventoryUIBridge`, `StatusUIBridge`, `ShopUIBridge`.
- Fusion: `CathedralUIBridge`.

Bridge code is allowed to know about menu layout and display strings. Rule code should not depend on bridge behavior except through returned choices.

### Messengers And Loggers

Battle, field, and fusion each use a small event-style messaging layer.

- Messengers publish structured message args.
- Loggers subscribe and render to `IGameIO`.
- Conductors unsubscribe loggers when leaving long-lived loops to avoid duplicate messages and leaks.

This keeps engines mostly independent from console rendering while still allowing narration, pauses, colors, and analysis displays.

### Static Database

`Database` is the content registry. It loads JSON files from `Data/Jsons` into static dictionaries and lists:

- skills, entities/personas/demons, ailments, items, dungeons, equipment, fusion recipes, negotiation questions, and shop inventory.

Runtime systems assume `Database.LoadData(io)` has completed before factories, shops, battle effects, dungeon traversal, or fusion logic are used.

## Runtime Dependency Shape

`Program.cs` creates the initial shared services:

- `IGameIO` for console access.
- `InventoryManager` and `EconomyManager` for persistent player resources.
- `DungeonState` for Tartarus progress.
- `CompendiumRegistry` for demon snapshots.
- `BattleKnowledge` for affinity discovery memory.
- `Combatant` for the player and test scenario state.

The field subsystem then creates the party manager, dungeon manager, bridges, service engines, exploration processor, and fusion conductor. Battle conductors are created as encounters occur, using the current party, enemies, inventory, economy, knowledge, and compendium.

## Data Flow

1. JSON files define content templates.
2. `Database` hydrates templates into static registries.
3. Factories convert templates into live `Combatant` or `Persona` instances.
4. Managers hold persistent state such as stock, inventory, economy, dungeon progress, and compendium entries.
5. Conductors move state through gameplay loops.
6. Engines and processors calculate outcomes and mutate state.
7. Bridges and loggers present results through `IGameIO`.

## Design Constraints

- `Combatant` is intentionally broad because it represents humans, operators, demons, enemies, and party members.
- Demons use `ActivePersona` as their stat and affinity source; their own character stats are reset to zero by the factory.
- Operators use demon stock and active party references; Wild Cards use active persona plus persona stock.
- The project currently has no persistence layer beyond in-memory managers and runtime JSON loading.
- The console UI is abstracted, but the current UI is still menu-driven and synchronous.

## Caveats

- Nullable warnings are present across DTOs, events, and some return paths. Many come from JSON-populated classes without required constructors.
- `Database` is global mutable state. This is simple for a prototype but makes test isolation harder.
- Some systems compare names or string IDs directly. Normalize IDs to lowercase where possible and be careful when adding new content.
- There is no automated test suite. Build success catches compilation issues, but behavior changes need manual scenario validation or future tests.
