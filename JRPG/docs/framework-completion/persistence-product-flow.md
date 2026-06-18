# Problem: Persistence Product Flow

## Current State

The framework has serializer-neutral runtime save/checkpoint snapshots and validation.

The console host has a noninteractive `--clean-save-demo` that serializes/deserializes snapshots using host-owned JSON DTOs.

## Problem

Snapshot contracts are not the same as a save/load product flow.

The framework can describe state, but a host must still decide save slots, file format, migration policy, UI, autosave behavior, and what host-owned metadata belongs outside the framework.

## Needed Data

Generic persistence examples:

- actor snapshots;
- party/stock snapshots;
- inventory/equipment/wallet snapshots;
- field/dungeon progress;
- Compendium entries;
- battle knowledge;
- session flags and counters;
- host context metadata;
- checkpoint breadcrumbs.

## Decisions Still Needed

- What is the first interactive save/load UI?
- Does the framework need save-version migration helpers, or should hosts own migrations?
- What is the minimum stable save contract for a public framework release?
- Which host metadata should never enter framework snapshots?

## Recommended Next Step

After a small interactive clean loop exists, add save/load to that loop using host-owned storage.

Do not retrofit save/load into the legacy prototype before the clean loop's state model is stable.
