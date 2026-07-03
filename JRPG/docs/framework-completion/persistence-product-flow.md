# Problem: Persistence Product Flow

## Current State

The framework has serializer-neutral runtime save/checkpoint snapshots, validation, and save-policy contracts.

The console host has a noninteractive `--clean-save-demo` that serializes/deserializes snapshots using host-owned JSON DTOs.

Phase 3-20 adds an interactive clean-host proof in `--clean-training-annex-play`: a `Save / Load` menu creates host-owned manual and suspend save records, stores them as raw in-memory JSON, validates them on load, restores the Training Annex session, and consumes a suspend slot only after successful restore.

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

- Does the framework need save-version migration helpers, or should hosts own migrations?
- What is the minimum stable save contract for a public framework release?
- Which host metadata should never enter framework snapshots?
- Should autosaves exist as a framework policy kind, or remain entirely host-owned?
- Which future contexts, if any, allow battle saves?
- Should permanent save slots be demonstrated in the console host before a Godot save-resource proof?

## Backlog Item: Save Policy And Suspend Saves

Status: completed as Phase 3-20 for manual and suspend saves only.

Goal: add a framework-owned save policy layer over existing snapshots without adding filesystem, serializer, console, or Godot dependencies.

Expected framework ownership:

- define save kinds, including suspend saves;
- validate whether a snapshot can be saved in the current runtime context;
- expose whether a suspend save should be consumed after successful load;
- preserve host-owned storage and UI decisions outside the framework.

Implemented Training Annex proof:

- manual and suspend save records use `RuntimeSaveRecord`;
- save contexts use `RuntimeSaveContextSnapshot`;
- policy rejects unregistered contexts and pending host actions;
- manual load keeps its slot;
- suspend load consumes its slot only after successful deserialize, validation, and restore;
- malformed or invalid host JSON leaves the active session unchanged;
- restored state includes actor runtime snapshots, resources, inventory, wallet, field/dungeon state, session progress, and player battle knowledge.

Remaining non-goals:

- no file-slot manager;
- no Godot save resource;
- no autosave;
- no battle save;
- no save-version migration helpers;
- no legacy prototype save/load retrofit.

## Recommended Next Step

Use the Training Annex flow as the reference product shape while later clean features are added. Permanent filesystem slots and Godot-owned save resources should wrap the same framework snapshots and policy results rather than changing the framework into a serializer or storage layer.
