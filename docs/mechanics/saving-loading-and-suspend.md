# Saving, Loading, And Suspend Saves

## Ownership Boundary

Convergence owns versioned, serializer-neutral runtime snapshots and validation. The host owns save slots, files, JSON or binary encoding, encryption, cloud storage, menus, screenshots, and platform integration.

No Framework public API exposes `System.Text.Json`, filesystem paths, Godot resources, or console types.

## Save Contents

`RuntimeSaveGameSnapshot` can include:

- runtime actors;
- party, reserve, Active Hosted Entity, Hosted Entity Roster, and Companion Roster references;
- inventory and equipped items;
- wallet;
- optional navigation and dungeon traversal progress;
- Compendium entries;
- player knowledge;
- session flags and counters;
- checkpoint breadcrumbs;
- optional host context.

Catalog definitions are not copied into a save. Saves retain qualified content IDs and are restored against a supplied `GameDataCatalog`.

## Validation Before Restore

The save validator aggregates diagnostics for unsupported contract version, duplicate runtime IDs, missing references, role collisions, capacity violations, invalid actor numeric state, invalid timed state, missing content, malformed inventory/equipment, invalid Compendium entries, duplicate knowledge, navigation/traversal inconsistencies, and invalid identifiers.

An invalid snapshot cannot produce a valid restore token. The host should present diagnostics or reject the slot rather than partially loading it.

## Manual Saves

**Configured rule:** a save policy decides which contexts permit manual saving. The host asks the policy, captures the current snapshots, validates them, serializes them, and writes the selected slot.

Loading reads the host format, reconstructs the Framework snapshot, validates it against the current catalog and policies, then rebuilds runtime state. Presentation objects such as Godot Nodes are reattached by `RuntimeInstanceId` after restore.

## Suspend Saves

A suspend save uses the same validated snapshot contract but a different host-owned slot and lifecycle policy.

A common one-use flow is:

1. The host confirms suspend saving is allowed in the current context.
2. The host captures, validates, serializes, and stores a suspend snapshot.
3. The session may exit or return to a title screen.
4. Suspend load validates and restores the snapshot.
5. After successful restore, the host deletes or marks the suspend slot consumed.

**Host responsibility:** one-use enforcement is storage behavior. Framework provides the save kind, policy decision, snapshots, and validation; it does not delete files.

## Checkpoints And Replay

Checkpoint breadcrumbs are ordered diagnostic entries. They can help identify where a session snapshot was created, but they are not a deterministic replay log.

Cross-version save migration is deferred until a released save contract requires it. The current contract version is an active-development contract and should not be treated as a permanent public wire guarantee yet.
