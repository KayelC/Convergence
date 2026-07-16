# Saving, Loading, And Suspend Saves

## Ownership Boundary

Convergence owns versioned, serializer-neutral runtime snapshots and validation. The host owns save slots, files, JSON or binary encoding, encryption, cloud storage, menus, screenshots, and platform integration.

No Framework public API exposes `System.Text.Json`, filesystem paths, Godot resources, or console types.

## Save Contents

The current runtime save contract is version `8`.

`RuntimeSaveGameSnapshot` can include:

- runtime actors, including learned skills, equipped skills, pending
  skill-choice tokens, and skill revisions;
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

The party roster is the one ownership and placement authority. Actor snapshots
do not contain duplicate owned rosters or active/reserve placement.

## Validation Before Restore

The save validator aggregates diagnostics for unsupported contract version, duplicate runtime IDs, missing references, role collisions, capacity violations, invalid actor numeric state, invalid timed state, missing content, malformed inventory/equipment, invalid Compendium entries, duplicate knowledge, navigation/traversal inconsistencies, and invalid identifiers.

An invalid snapshot cannot produce a valid restore token.
`IRuntimeSessionRestoreService` first runs an explicit migration service,
validates the complete aggregate, resolves a host-supplied restore profile for
every actor, restores the Active Hosted Entity selected by the canonical party
roster before its Vessel, recomposes the Vessel, and returns either one
complete `RuntimeRestoredSession` or typed diagnostics with no partial session.
The normalized restored snapshot replaces stale derived Vessel combat-profile
data with the profile produced from restored source state.

The host should present diagnostics or reject the slot rather than partially
loading it.

## Manual Saves

**Configured rule:** a save policy decides which contexts permit manual saving. The host asks the policy, captures the current snapshots, validates them, serializes them, and writes the selected slot.

Loading reads the host format, reconstructs the Framework snapshot, and passes it to the aggregate restore service with the current catalog, actor factory, restore-profile resolver, capacity-aware validator, and optional migration steps. Presentation objects such as Godot Nodes and host context are applied only after Framework returns a complete session, then reattached by `RuntimeInstanceId`.

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

`IRuntimeSaveMigrationService` and ordered `IRuntimeSaveMigrationStep` contracts provide the extension seam for future released save formats. Convergence ships no fictitious migration for unreleased formats: an older or newer version is rejected unless the host explicitly supplies a valid path to the current contract.

Save contract v7 has no built-in conversion to v8. A host that intentionally
retains pre-release v7 data must provide and test an explicit migration step.

## Related Guidance

- [Actors And Runtime State](../developer-guide/actors-and-runtime-state.md)
- [Runtime Actor State And Restoration](../technical/runtime-actor-state-and-restoration.md)
- [Godot Integration Contract](../godot-integration-contract.md)
