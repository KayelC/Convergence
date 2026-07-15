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

The framework exposes serializer-neutral runtime snapshots and restores them against a `GameDataCatalog` through `IRuntimeSessionRestoreService`. A Godot save may wrap those snapshots with scene paths, transforms, camera state, UI state, and asset references. Godot recreates Nodes and applies host context only after the aggregate restore result succeeds, using `RuntimeInstanceId` to reconnect scene objects.

The framework does not prescribe JSON, binary data, Godot `Resource`, save slots, cloud storage, or migration UI. `Convergence.DemoHost` uses host-owned `System.Text.Json` only as a portability example.

## Verification

`GodotIntegrationContractTests` proves that a Godot-shaped host can:

- supply content using fake `res://` diagnostic paths;
- build a catalog without framework filesystem access;
- hydrate actors and run deterministic actions and encounters;
- consume ordered events through host sinks;
- map runtime IDs to host scene handles;
- round-trip actor and field snapshots through host-owned storage.

The proof intentionally uses no Godot assembly. A real Godot adapter project remains application work and is tracked as deferred in the [capability matrix](framework-capability-matrix.md).

## Forbidden Coupling

Framework public APIs and source must not depend on Godot, console APIs, filesystem APIs, host serializers, or host scene types. The reusable assembly may be consumed without `Convergence.DemoHost`.
