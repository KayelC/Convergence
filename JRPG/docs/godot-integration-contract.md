# Godot Integration Contract

> **Status: Track P/R contract proof.** This document defines how a Godot host consumes `JRPG.Framework` without making the framework depend on Godot.

## Purpose

Track P proves that Godot integration is adapter work. The framework remains an engine-neutral class library that owns content validation, catalogs, runtime rules, transitions, and immutable results. A Godot project owns resource acquisition, nodes, scenes, input, presentation scheduling, asset IDs, and save-file format.

No GodotSharp package or Godot project is required in this repository for Track P. The proof lives in test-only Godot-shaped adapters that use fake `res://` paths, signal-style commands, event sinks, scene-instance handles, and host-owned save snapshots. Track R extends the same boundary with framework-owned save snapshot contracts and a console-host JSON proof.

## Host Responsibilities

A Godot host must provide these adapters around the existing framework contracts:

- Content acquisition: read JSON or imported resources from Godot-owned locations such as `res://`, then supply JSON text and diagnostic source names through `IContentPackTextSource`.
- Input: translate player input, UI button signals, and menu cancellation into `IHostCommandSource<TCommand>` results.
- Presentation: consume framework events through event sinks and map them to UI, animation, audio, waits, and scene transitions.
- Randomness: provide an `IRandomSource` seeded or unseeded according to the host's run mode.
- Scene identity: map framework `RuntimeInstanceId` or battle instance IDs to host-owned node/scene handles.
- Navigation input: translate doorway triggers, map selections, VN hotspots, or scripts into generic `RuntimeNavigationTransition` requests. Godot still owns movement and scene changes.
- Encounter triggers: own placed enemy scenes, patrols, touch/attack triggers, spawn points, and scripted battle triggers; when an encounter actually begins, pass the chosen encounter or formation into the framework for battle resolution.
- Persistence: store framework snapshots inside the Godot save format alongside host-owned scene, asset, and UI state.

The framework must not know about `Node`, `Resource`, `PackedScene`, `SceneTree`, `res://`, animation players, save-file layout, or Godot signals.

Godot exploration should not be forced into the console demo's floor-transition battle model. Floor-triggered encounters are useful for deterministic tests and text demos, while production Godot scenes should be free to use visible enemy entities, trigger volumes, scripted bosses, or other host-owned encounter-start rules.

## Framework Responsibilities

`JRPG.Framework` owns the reusable logic:

- content definitions, validation, catalog loading, and qualified IDs;
- actor hydration from catalog definitions;
- skill, item, passive, status, action, battle, dungeon, party/stock, economy, fusion, and Compendium rule services;
- encounter resolution once the host has selected an encounter, including battle setup, outcome, rewards, and state updates;
- ordered events and diagnostics expressed as serializer-neutral records;
- runtime snapshots such as `RuntimeActorSnapshot` and `RuntimeDungeonProgressSnapshot`;
- optional generic navigation through `ContentId` locations, explicit transitions, and a host-supplied `IRuntimeNavigationPolicy`;
- versioned persistence snapshots such as `RuntimeSaveGameSnapshot`, `RuntimeKnowledgeSnapshot`, `RuntimeSessionProgressSnapshot`, and checkpoint logs.

Framework APIs remain plain .NET contracts. JSON DTOs, `JsonElement`, console types, filesystem access, Newtonsoft, legacy DTOs, and Godot types must not appear in public framework signatures.

## Track P Proof

`GodotIntegrationContractTests` proves the current adapter boundary by:

- loading the retained reference and clean battle demo packs from fake `res://` resources while preserving logical document paths;
- building a `GameDataCatalog` through explicit registrations;
- creating clean battle actors through `CatalogBattleActorFactory`;
- planning host-triggered encounters through `CatalogEncounterStartPlanner` without passing Godot scene handles into the framework;
- mapping actor instance IDs to host-owned scene handles;
- reading selected and cancelled signal-style commands through `IHostCommandSource<TCommand>`;
- running deterministic clean battle execution and consuming ordered framework events;
- restoring actor and field/dungeon snapshots through a host-owned save store.

This is not a gameplay migration track. It proves that a Godot project can stand beside the console host and consume the same framework without core rule changes.

## Save Boundary

The framework exposes serializer-neutral snapshots. Phase 1-07 advances the pre-release aggregate save boundary to `RuntimeSaveGameSnapshot` contract version `2`: field state may be absent, generic navigation may exist without a dungeon, and dungeon progress may be attached only when that optional module is used. A Godot save file should wrap that snapshot with host-owned information such as scene paths, node handles, current scene, camera state, UI state, asset references, and any engine-specific metadata.

The framework does not prescribe JSON, binary, Godot `Resource`, or any other save format. It only provides stable state objects and `IRuntimeSaveValidator`, which checks restored snapshots against a `GameDataCatalog` without duplicating catalog definitions into the save.

`--clean-save-demo` is the console-host proof: it serializes a representative `RuntimeSaveGameSnapshot` using host-owned `System.Text.Json` DTOs, deserializes it, validates it, rebuilds runtime actor state, and exits without input. A Godot host would replace those DTOs with its own save envelope while preserving the same framework snapshot and validation boundary.

## Non-Goals

- No GodotSharp dependency is added.
- No Godot project or scene is checked in.
- No production content is reauthored.
- No legacy console file is removed.
- No parity-ledger capability moves to `clean_parity`.
- No framework public API is changed for Godot-specific concepts.
- No save-slot UI, cloud-save policy, or save-version migration system is added.
