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
- typed action, skill, item, passive, targeting, and effect execution;
- catalog-backed actor hydration and automated battle orchestration;
- elemental, ailment, instant-death, knowledge, and Press Turn contracts;
- typed fusion inheritance evaluation, result resolution, planning, transaction assessment, and Compendium state.

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

The existing interactive prototype still uses synchronous `IGameIO`, but Track O1 adds console adapters over the clean host contracts. Those adapters are intentionally narrow: already-simple menus can return typed commands and startup can publish sidecar catalog warnings through the event sink without forcing full battle, fusion, shop, or preview-heavy screens through asynchronous host contracts yet.

### Runtime State Boundary

Track D adds `JRPGPrototype.Logic.Runtime` as the framework home for mutable actor state that must eventually survive save, presentation, replay, and host migration boundaries. It defines stable runtime instance IDs distinct from content definition IDs, actor identity/display metadata, controller/team/owner links, deployment state, progression, resources, base/effective stats, learned/equipped skills, active form and stock references, equipment slots, battle statuses, analysis, and passive activation counts.

The runtime state layer is deliberately composed from focused snapshots rather than one replacement `Combatant` class. `RuntimeActorSnapshot` exists only as the aggregate save/transaction boundary, and content definitions are always referenced by qualified `ContentId` instead of being duplicated into mutable state.

Track E adds framework progression policies for stat composition, HP/SP recalculation, EXP curves, level growth, random Persona stat growth, stat allocation, and rollback. The console `StatProcessor`, `GrowthProcessor`, and `Persona` growth methods now delegate through a console-owned compatibility adapter, preserving the existing live `Combatant` and `Persona` models while moving the rules into reusable framework services.

Track F adds framework party and stock transition services for active/reserve party membership, stock capacity, unified demon stock, active Persona swaps, and fusion inventory consume/replace operations. `PartyManager`, battle Persona swaps, field Persona swaps, and fusion inventory transactions now delegate through console-owned adapters with per-session runtime IDs. The old live lists remain the source of console object ownership until a later persistence/host migration replaces them.

Track G adds `ProductionCombatRuleset` as the framework owner for production combat formulas: damage, hit/evasion, critical chance, instant-death success, initiative, rewards, affinity multipliers, guard and rigid-body handling, charge, drain, and reflection math. `CombatMath` and `DamageHandler` remain console-host facades, but their rule work now flows through `LegacyCombatPolicyAdapter` into the framework policy.

Track H adds `BattleActionExecutor` as the framework action facade over clean basic attacks, skills, items, guard, pass, analyze, escape, Persona/demon stock transitions, and host-mediated tactics/negotiation/special actions. It provides one shared assessment/execution path, structured action events, turn-consumption results, cancellation-before-mutation, and host-owned item reservation/commit ports. The console host now routes guard/pass through a compatibility adapter, and the clean field demo uses the facade for field skills and items.

Track I adds `BattleStatusLifecycleService` as the framework owner for clean ailment application, turn-start restrictions, turn-end status effects, natural recovery, duration ticking, cleanup scopes, and battle-start or turn-end passive dispatch. `StatusRegistry` remains the console-facing compatibility facade, but ailment infliction, turn-start, turn-end, and stat-stage mutation now route through `LegacyStatusLifecycleAdapter` where strict legacy parity exists. Cure parsing and redundancy checks stay in the console host until the old skill and item content is reauthored.

Track J adds `BattleEncounterRunner` as the framework owner for the encounter state machine: initiative, battle-start lifecycle, team phases, actor turns, Press Turn consumption, command orchestration, turn-end lifecycle, deployment refresh, completion, cancellation, faults, and ordered battle events. `BattleConductor` now acts as a console adapter over that runner, while `InteractionBridge`, `ActionProcessor`, `BehaviorEngine`, legacy content, and console presentation remain host-owned compatibility systems.

Track K adds framework negotiation/reward services for the conversation state machine, typed prompts, demand outcomes, familiar gifts, recruitment validation, and immutable battle reward calculation. The console `NegotiationEngine`, `BattleConductor`, and new legacy adapters translate `IGameIO`, live `Combatant` lists, inventory, economy, and compendium mutation into those framework results. Legacy `questions.json` remains the data source until production content is reauthored.

Track L adds framework resource-management services for inventory quantities, unique equipment-ID ownership, equipment equip/sale invariants, Macca transactions, Luck-based shop pricing, shop buy/sell transactions, and hospital restoration. `InventoryManager`, `EconomyManager`, `ShopEngine`, and the field service item/equipment/hospital mutation paths now delegate through `LegacyInventoryResourceAdapter`, while `Database`, legacy DTOs, shop inspection text, and console menu presentation remain host-owned.

Track M adds framework-owned field/dungeon state-machine services for dungeon progress snapshots, floor evaluation, terminal unlocks, boss defeat state, barrier handling, deterministic encounter selection, and ordered transition events. `DungeonManager` is now a console compatibility facade over those services, using `LegacyDungeonContentAdapter` to adapt `Database.Dungeons` without changing `tartarus.json` or menu behavior.

Track N adds framework fusion runtime services for recipe lookup, result operation selection, inheritance slot calculation, skill mutation, accident inheritance replacement, preview snapshots, transaction assessment, and Compendium registration/recall assessment. `FusionCalculator`, duplicate-result guards, and `CompendiumRegistry` now adapt legacy database/live object state into those services while keeping Cathedral menus and legacy transaction strategies intact. Compendium snapshots now deep-clone active Persona data instead of sharing live references.

Track O1 starts the interactive console-host migration without changing gameplay rules. `ConsoleGameHost` still loads the legacy `Database` first, then creates an `InteractiveConsoleHostContext` that attempts to load the retained clean content packs as a nonfatal sidecar catalog. Plain field, city, inventory, status, dungeon, terminal, hospital-patient, and field-target menus now pass through the framework host-command contracts via console adapters while preserving legacy return strings for existing conductors.

Track O2 moves read-only status presentation behind console-owned projection adapters. Human summaries, Persona details, demon details, stock rows, organization rows, summon rows, and equipment slot labels are now rendered from framework runtime snapshots plus copied legacy display data. Rich hover-preview menus outside status, battle commands, Cathedral prompts, and gameplay mutations remain legacy bridge surfaces.

Track O3 routes field item and field skill presentation through typed console-host selection and execution results. Item/skill menus, target cancellation, field-use assessment, consumption decisions, and ordered field presentation events now have explicit result contracts, while legacy `ItemData`, `SkillData`, production JSON, effect-string parsing, and visible field behavior remain console-host compatibility concerns.

Track O4 routes party organization, demon stock, Persona stock, and field-side summon/return/swap presentation through typed console-host results backed by the Track F party/stock adapter. The old `StatusUIBridge` wrappers, `PartyManager`, live `Combatant`/`Persona` lists, active plus owned demon stock invariant, messages, cancellation, and status-peek behavior remain intact.

Track O5 routes player battle command selection through a console-host command shell. The shell produces framework `BattleActionCommand` objects and assessments before handing back the legacy payloads needed by `ActionProcessor`, `PartyManager`, `NegotiationEngine`, and current battle helpers. Legacy attack, skill, item, and escape execution remain host-mediated; concrete framework commands are used for guard, pass, analyze, Persona swap, and COMP stock commands.

Track O6 routes framework battle encounter events through a console-host event presentation adapter. `BattleConductor` now supplies the runner with an event sink that records `Shown`, `Suppressed`, and `HostOwned` presentation results. Generic framework structural events stay quiet to preserve visible console narration, while migrated lifecycle-shell messages for skip, fear flee, return-to-COMP, enemy flee, and demon defeat return use typed presentation results.

Track O7 routes negotiation, recruitment, and victory reward presentation through typed console-host results. `NegotiationEngine` now exposes detailed prompt/event/outcome records around the framework session service, `BattleConductor` shares one negotiation outcome presenter across its battle paths, and reward totals are presented from immutable framework reward results before legacy mutation is applied.

Track O8 routes shop and hospital presentation through typed console-host results. `ShopUIBridge` now exposes explicit command, offer, confirmation, inspection, and transaction result shapes over the framework-backed shop transactions, while `ServiceUIBridge`, `FieldServiceEngine`, and `FieldConductor` present hospital selection and treatment from typed results over framework restoration transactions. Legacy shop data, pricing formulas, metadata repair, and hospital UI quirks remain host-owned.

Complete AI/tactics policy, full fusion strategy replacement, Compendium persistence, save/load services, authored negotiation content, legacy item/equipment/dungeon content reauthoring, remaining battle/Cathedral presentation migration, and authored ruleset binding remain later migration tracks. The Track E/F/G/H/I/J/K/L/M/N/O policies are named defaults in code, not authored ruleset JSON parameters yet.

## Layers And Patterns

### Conductors

Conductors own high-level workflows and decide which subsystem should act next.

- `FieldConductor` runs the city, dungeon, inventory, status, party organization, and fusion entry loops. Dungeon entry, terminal return, explicit dungeon exit, and boss-defeat registration now pass through the framework-backed `DungeonManager` facade.
- `BattleConductor` adapts the console battle into the framework encounter runner, applies framework reward results through a console adapter, and keeps cleanup flow host-owned.
- `FusionConductor` runs Cathedral menus, participant selection, result staging, inheritance choice, confirmation, accidents, and compendium actions.

Conductors should remain workflow coordinators. When adding new rules, prefer placing rule logic in an engine, processor, strategy, or registry.

### Engines And Processors

Engines and processors own deterministic rules or bounded state mutations.

- `CombatMath` is the console compatibility facade for framework production combat policies.
- `PressTurnEngine` owns the full/blinking turn icon state machine.
- `StatusRegistry` is the console compatibility facade for ailment application, turn-start restrictions, passive startup effects, buff/debuff handling, cures, and redundancy checks. Migrated lifecycle decisions delegate into the framework through `LegacyStatusLifecycleAdapter`.
- `ActionProcessor` executes attacks, skills, items, persona swaps, and analysis by delegating to effect strategies; migrated guard/pass coordination now passes through the framework action facade.
- `FieldServiceEngine` and `ShopEngine` are console compatibility facades over framework-backed resource-management transactions for inventory, equipment, shops, and hospital restoration. They still own legacy item/skill effects, metadata repair, messages, and dungeon traversal coordination.
- `ExplorationProcessor` remains the console host for field-side messages, battle handoff, encounter hydration, and duplicate enemy display suffixes; movement and floor evaluation are delegated through the framework-backed dungeon manager.
- `FusionCalculator`, `FusionMutator`, and fusion strategies remain console compatibility facades; fusion prediction and Compendium rule checks now route through framework services where Track N migrated them.
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

During ordinary console startup, the clean catalog is loaded as a sidecar after the legacy database. Sidecar failures are reported as clean-catalog warnings and do not stop the prototype because the retained legacy datasets remain the gameplay authority until production content is reauthored.

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
