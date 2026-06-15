# Architecture

> **Status: Current implementation reference.** This document describes the Track B framework and console-host boundary. Approved GDD and parity documents remain authoritative for target behavior and migration gates.

The solution is organized around gameplay subsystems with a physical host boundary. Existing `JRPGPrototype.*` namespaces are retained for source compatibility, but assembly ownership is explicit.

## Project Boundaries

### JRPG.Framework

`JRPG.Framework` is a `net9.0` class library containing the reusable clean path:

- immutable content definitions;
- serializer-neutral deserialization contracts, validation, and catalog construction;
- catalog-surface definitions for equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets;
- runtime identity, actor-state snapshots, and transaction-safe mutation result contracts;
- typed skill, item, passive, targeting, and effect execution;
- catalog-backed actor hydration and automated battle orchestration;
- elemental, ailment, instant-death, knowledge, and Press Turn contracts;
- typed fusion inheritance evaluation and selection.

The framework has no package references and does not access the console, filesystem, Godot, or the legacy static database. JSON implementation details remain internal to the content-loading subsystem.

### JRPG.ConsoleHost

`JRPG.ConsoleHost` remains the executable at the repository root. It owns:

- `Program` and ordinary interactive startup;
- the legacy database, runtime actors, gameplay conductors, and console workflows;
- `IGameIO`, menu rendering, colors, waits, and debug scenarios;
- filesystem-backed content acquisition and copied `Data/Jsons` content;
- the clean battle and field demo policies and presentation.

The console host references the framework. The framework never references the console host.

### Host Contracts

Future clean hosts use cancellation-aware asynchronous contracts for content text, commands, events, and randomness. A host supplies JSON text through `IContentPackTextSource`, consumes or publishes ordered output through `IHostEventSink<TEvent>`, obtains typed choices through `IHostCommandSource<TCommand>`, and owns nondeterminism through `IRandomSource`.

The existing interactive prototype still uses synchronous `IGameIO`. Moving that consumer onto the new contracts is deliberately deferred; Track B establishes the boundary without rewriting gameplay.

### Runtime State Boundary

Track D adds `JRPGPrototype.Logic.Runtime` as the framework home for mutable actor state that must eventually survive save, presentation, replay, and host migration boundaries. It defines stable runtime instance IDs distinct from content definition IDs, actor identity/display metadata, controller/team/owner links, deployment state, progression, resources, base/effective stats, learned/equipped skills, active form and stock references, equipment slots, battle statuses, analysis, and passive activation counts.

The runtime state layer is deliberately composed from focused snapshots rather than one replacement `Combatant` class. `RuntimeActorSnapshot` exists only as the aggregate save/transaction boundary, and content definitions are always referenced by qualified `ContentId` instead of being duplicated into mutable state.

Track E adds framework progression policies for stat composition, HP/SP recalculation, EXP curves, level growth, random Persona stat growth, stat allocation, and rollback. The console `StatProcessor`, `GrowthProcessor`, and `Persona` growth methods now delegate through a console-owned compatibility adapter, preserving the existing live `Combatant` and `Persona` models while moving the rules into reusable framework services.

Track F adds framework party and stock transition services for active/reserve party membership, stock capacity, unified demon stock, active Persona swaps, and fusion inventory consume/replace operations. `PartyManager`, battle Persona swaps, field Persona swaps, and fusion inventory transactions now delegate through console-owned adapters with per-session runtime IDs. The old live lists remain the source of console object ownership until a later persistence/host migration replaces them.

Inventory quantities, full fusion transaction ownership, compendium persistence, and save/load services remain later migration tracks. The Track E/F policies are named defaults in code, not authored ruleset JSON parameters yet.

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

`Database` is the console host's legacy content registry. It loads JSON files from `Data/Jsons` into static dictionaries and lists:

- skills, entities/personas/demons, ailments, items, dungeons, equipment, fusion recipes, negotiation questions, and shop inventory.

Legacy runtime systems assume `Database.LoadData(io)` has completed before factories, shops, battle effects, dungeon traversal, or fusion logic are used. Framework services instead receive immutable definitions or a validated `GameDataCatalog`.

The clean catalog now has a definition surface for every retained legacy content family. That surface is not a data migration: the legacy datasets still load through `Database`, while clean fixtures prove target authoring contracts for later consumer migration tracks.

## Runtime Dependency Shape

`Program.cs` belongs to `JRPG.ConsoleHost` and creates the initial legacy services:

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
- The console UI is abstracted, but the current interactive workflow remains menu-driven and synchronous.
- Filesystem, console, delays, and legacy Newtonsoft loading are host-only concerns.
- Framework public APIs expose no console, filesystem, serializer, Godot, or legacy runtime types.
- Host cancellation is distinct from an ordinary menu cancellation in the async command contract.
- Runtime snapshots are serializer-neutral contracts. A host may persist them, but the framework does not prescribe a save file format in Track D.

## Caveats

- Nullable warnings are present across DTOs, events, and some return paths. Many come from JSON-populated classes without required constructors.
- `Database` is global mutable state. This is simple for a prototype but makes test isolation harder.
- Some systems compare names or string IDs directly. Normalize IDs to lowercase where possible and be careful when adding new content.
- The automated suite covers framework contracts, legacy characterization, datasets, host adapters, and deterministic demos. Full live battles and exhaustive long-form console traversal remain manual checks.
