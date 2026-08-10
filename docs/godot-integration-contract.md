# Godot Integration Contract

## Purpose

Godot is Convergence's primary host target, but `Convergence.Framework` does not depend on Godot. Integration is adapter work around engine-neutral framework contracts.

Godot owns resources, nodes, scenes, input, presentation, scheduling, assets, and save files. The framework owns content contracts, catalogs, rules, runtime state, transitions, diagnostics, and immutable results.

## Source Reference

Convergence targets .NET 8 and C# 12. It is source-distributed and deliberately non-packable. Keep the framework checkout outside the Godot project directory and add a project reference from the game:

```xml
<ItemGroup>
  <ProjectReference Include="..\Convergence\src\Convergence.Framework\Convergence.Framework.csproj" />
</ItemGroup>
```

This avoids compiling framework sources both through Godot's default source glob and through the referenced project.

## Host Adapters

A Godot host supplies adapters for these framework boundaries:

- `IContentPackTextSource`: read `res://`, imported resources, or another host source and return JSON text with diagnostic names.
- `IHostCommandSource<TCommand>`: translate signals, buttons, input actions, and cancellation into typed commands.
- `IHostEventSink<TEvent>`: map ordered events to UI, animation, audio, and scene work.
- `IRandomSource`: provide deterministic or production randomness.
- runtime instance mapping: associate `RuntimeInstanceId` values with host-owned node or scene handles.
- persistence: serialize framework snapshots inside a Godot-owned save envelope.

Await asynchronous framework operations. The framework has no engine-thread affinity, so adapters must marshal node and scene changes onto Godot's scheduler.

## Exploration And Encounters

Navigation and dungeon traversal are optional, policy-injected modules. A Godot game may use movement, doors, map selection, visual-novel hotspots, or scripts to request the same logical transitions. The framework never prescribes a menu or a scene graph.

Godot owns visible enemies, trigger volumes, patrols, spawn points, boss scenes, and despawn rules. Once the host chooses an authored encounter, framework services prepare actors and resolve battle rules. Movement does not automatically start combat.

## Save Boundary

The framework exposes serializer-neutral runtime snapshots and restores them
against a `GameDataCatalog` through `IRuntimeSessionRestoreService`. Runtime
save contract v17 stores one canonical party roster, inventory-owned equipment
instances with actor loadout references, complete source actor
progression and move-list state, complete selected-policy stat-modifier
contributions, typed status lifetimes, and optional per-target passive
activation keys. Analyze state is encounter-local knowledge and is not copied
into actor save DTOs.

A Godot save may wrap those snapshots with scene paths, transforms, camera
state, UI state, and asset references. Godot recreates Nodes and applies host
context only after the aggregate restore result succeeds, using
`RuntimeInstanceId` to reconnect scene objects.

Restoration validates the complete aggregate, explicitly binds retained
stat-modifier policies, resolves actor restore profiles,
restores an Active Hosted Entity before its dependent Vessel, and recomposes
the Vessel from restored source state. Rejection returns diagnostics and no
partial live session. Any non-current save contract requires an explicit
host-supplied migration path.

The framework does not prescribe JSON, binary data, Godot `Resource`, save slots, cloud storage, or migration UI. `Convergence.DemoHost` uses host-owned `System.Text.Json` only as a portability example.

## Verification

Two layers guard the integration boundary.

`GodotIntegrationContractTests` proves without an engine process that a Godot-shaped host can:

- supply content using fake `res://` diagnostic paths;
- build a catalog without framework filesystem access;
- hydrate actors and run deterministic actions and encounters;
- consume ordered events through host sinks;
- map runtime IDs to host scene handles;
- round-trip actor and field snapshots through host-owned storage.

`samples/Convergence.GodotHost` is the real Godot 4.7.1 .NET reference
consumer. Its noninteractive smoke scene reads the canonical Training Annex
pack through `Godot.FileAccess`, maps framework runtime IDs to actual `Node`
instances, selects and executes a typed action, consumes an ordered encounter
stream, and decodes a host-owned JSON save before handing the complete
aggregate to `IRuntimeSessionRestoreService`. It proves source-first Active
Hosted Entity restoration and proves a rejected aggregate exposes no actors.

```powershell
dotnet build samples/Convergence.GodotHost/Convergence.GodotHost.csproj
godot --headless --path samples/Convergence.GodotHost -- --convergence-smoke
```

The sample reports `CONVERGENCE_GODOT_SMOKE_OK` and exits `0` on success. Its generated `res://Content` directory is ignored; the build copies from the single canonical pack under `content/original/training-annex`.

## Forbidden Coupling

Framework public APIs and source must not depend on Godot, console APIs, filesystem APIs, host serializers, or host scene types. The reusable assembly may be consumed without `Convergence.DemoHost`.

## Related Guidance

- [Actors And Runtime State](developer-guide/actors-and-runtime-state.md)
- [Turn Economy Policies](developer-guide/turn-economy-policies.md)
- [Runtime Actor State And Restoration](technical/runtime-actor-state-and-restoration.md)
- [Turn Economy Runtime](technical/turn-economy-runtime.md)
- [Public API Contract](public-api-contract.md)
