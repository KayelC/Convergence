# Problem: Clean Consumer Independence

## Current State

The framework owns many rule services, but ordinary interactive gameplay still runs through the console prototype.

The console host uses compatibility adapters to call framework services while keeping live legacy objects such as `Combatant`, `Persona`, `SkillData`, `ItemData`, and `Database`.

## Problem

An adapter-backed feature is not fully independent.

The framework becomes independent only when a real consumer runs directly on clean catalog definitions, runtime snapshots, and framework commands without translating legacy live objects back and forth.

## Examples

Adapter-backed:

```text
Console Combatant -> adapter -> framework service -> adapter -> Console Combatant
```

Independent:

```text
Catalog definition -> framework runtime state -> framework command -> framework result
```

## Needed Work

- Define one small clean gameplay loop as the primary independence proof.
- Keep it separate from the legacy scenario startup.
- Use clean catalog actors, clean items, clean skills, clean encounters, clean dungeon state, clean rewards, and clean save snapshots.
- Only after that should individual legacy consumers be considered for retirement.

## Decisions Still Needed

- Should the first interactive clean consumer be console-based or Godot-facing?
- Should it extend `--clean-training-annex-demo` or become a separate clean-play mode?
- How much interactivity is needed before it counts as a real consumer?

## Recommended Next Step

Create a small interactive clean loop after the Training Annex has more content variety.

Do not attempt to migrate the entire old console game at once.
