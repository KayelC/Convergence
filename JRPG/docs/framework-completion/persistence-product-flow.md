# Problem: Persistence Product Flow

## Current State

The framework has serializer-neutral runtime save/checkpoint snapshots and validation.

The console host has a noninteractive `--clean-save-demo` that serializes/deserializes snapshots using host-owned JSON DTOs.

The framework does not yet have a save policy layer. Manual saves, autosaves, suspend saves, and post-load behavior are still design decisions layered on top of the existing snapshot contract.

## Problem

Snapshot contracts are not the same as a save/load product flow.

The framework can describe state, but a host must still decide save slots, file format, migration policy, UI, autosave behavior, and what host-owned metadata belongs outside the framework.

Suspend saves need explicit policy because they are not just another save slot. A suspend save usually means "resume once, then consume/delete or mark invalid." The framework should describe and validate that policy, while the host owns where the file lives and how the player sees it.

## Needed Data

Generic persistence examples:

- actor snapshots;
- party/stock snapshots;
- inventory/equipment/wallet snapshots;
- optional generic navigation and dungeon traversal state;
- Compendium entries;
- battle knowledge;
- session flags and counters;
- host context metadata;
- checkpoint breadcrumbs.

Potential save policy examples:

- save kind: `manual`, `autosave`, `suspend`;
- suspend load behavior: consume after successful restore, keep until overwritten, or host-defined;
- allowed save contexts: field, dungeon, battle, menu, checkpoint-only;
- reason/checkpoint metadata for suspend saves;
- host-owned slot or file metadata outside the framework snapshot.

## Decisions Still Needed

- What is the first interactive save/load UI?
- Does the framework need save-version migration helpers, or should hosts own migrations?
- What is the minimum stable save contract for a public framework release?
- Which host metadata should never enter framework snapshots?
- Should suspend saves be a required framework policy feature for the first public framework release, or a later host policy?
- Which contexts allow suspend saves?
- Should loading a suspend save always consume it, or should that be configurable by the host/ruleset?

## Backlog Item: Save Policy And Suspend Saves

Priority: TBD during the roadmap priority review.

Goal: add a framework-owned save policy layer over existing snapshots without adding filesystem, serializer, console, or Godot dependencies.

Expected framework ownership:

- define save kinds, including suspend saves;
- validate whether a snapshot can be saved in the current runtime context;
- expose whether a suspend save should be consumed after successful load;
- preserve host-owned storage and UI decisions outside the framework.

Non-goals:

- no save menu;
- no file-slot manager;
- no Godot save resource;
- no legacy prototype save/load retrofit.

## Recommended Next Step

After a small interactive clean loop exists, add save/load to that loop using host-owned storage.

Do not retrofit save/load into the legacy prototype before the clean loop's state model is stable.
