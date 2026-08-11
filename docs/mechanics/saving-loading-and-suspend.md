# Saving, Loading, And Suspend Saves

## Ownership Boundary

Convergence owns versioned, serializer-neutral runtime snapshots and validation. The host owns save slots, files, JSON or binary encoding, encryption, cloud storage, menus, screenshots, and platform integration.

No Framework public API exposes `System.Text.Json`, filesystem paths, Godot resources, or console types.

## Save Contents

The current runtime save contract is version `18`.

`RuntimeSaveGameSnapshot` contains:

- runtime actors, including learned skills, equipped skills, pending
  skill-choice tokens, skill revisions, and complete selected-policy
  stat-modifier state, retained charge-policy identity, and combat-profile
  source/revision identity;
- party, reserve, Active Hosted Entity, Hosted Entity Roster, and Companion Roster references;
- inventory-owned equipment instances and per-actor equipped-instance
  references;
- immutable currency balances keyed by currency content ID;
- optional navigation and dungeon traversal progress;
- Compendium entries;
- player knowledge;
- session flags and counters;
- checkpoint breadcrumbs;
- optional host context.

These fields describe the current aggregate shape; they do not activate every
listed mechanic. Hosts that use Framework persistence provide neutral snapshots
for required modules they do not use:

- an inventory with no quantities or owned equipment instances;
- actors whose equipment snapshots contain no equipped-instance references;
- an empty currency ledger, or the game's explicit currency entries with zero balances;
- an empty Compendium and knowledge snapshot;
- session progress with no counters, flags, elapsed time, or moon phase;
- a party roster that identifies the saved session owner but has empty active,
  reserve, Hosted Entity, and Companion collections when those roles are not
  used.

Navigation and dungeon state may be absent through a null `Field` value.
Checkpoint breadcrumbs and host context default to empty collections. A game
that does not use Framework persistence does not construct this aggregate at
all.

Catalog definitions are not copied into a save. Saves retain qualified content IDs and are restored against a supplied `GameDataCatalog`.

Retained charge state includes the ID of the charge policy that created it. A
host restoring an actor with active charges must supply that exact policy through
its charge-policy resolver. This prevents a split Physical/Magical charge from
being silently reinterpreted as one unified General charge, or vice versa.

The party roster is the one ownership and placement authority. Actor snapshots
do not contain duplicate owned rosters or active/reserve placement.

Passive activation limits are retained with the passive skill ID, event ID,
trigger index, count, and an optional target runtime ID. The target is present
only for per-target counting. If present, save validation requires that target
to exist in the aggregate actor list.

## Validation Before Restore

The save validator aggregates diagnostics for unsupported contract version,
duplicate runtime IDs, missing references, combat-profile source mismatch, role
collisions, capacity violations, invalid actor numeric state, invalid timed
state, missing content, malformed inventory/equipment, invalid Compendium
entries, duplicate or impossible Almighty knowledge, navigation/traversal
inconsistencies, and invalid identifiers. When an actor retains stat modifiers
or charge state, validation also requires the corresponding explicit policy
resolver and checks the complete state against its authored policy.

An invalid snapshot cannot produce a valid restore token.
`IRuntimeSessionRestoreService` first runs an explicit migration service,
validates the complete aggregate, resolves each retained stat-modifier policy
from the supplied catalog, resolves a host-supplied restore profile for
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

Save contract v8 has no built-in conversion to v9. Version 9 removes the
duplicated roster owner level and derives capacity from the saved owner actor.
A host that intentionally retains pre-release v8 or earlier data must provide
and test an explicit migration step.

Version 14 removes the obsolete actor-local Analyze field from actor snapshots.
Persistent player knowledge remains in `RuntimeKnowledgeSnapshot`; current
target analysis remains encounter-local and is discarded at encounter end.
Convergence supplies no automatic v13-to-v14 migration for these unreleased
formats.

Version 15 adds the combat-profile source actor, source entity, and revision to
each actor snapshot. This lets restore validate and reconstruct a Vessel's
derived profile before profile-sensitive systems such as Battle Knowledge use
it. Convergence supplies no automatic v14-to-v15 migration for these unreleased
formats.

Version 16 replaces equipment definition IDs used as owned copies with
immutable equipment-instance records. Inventory is the sole owner of each
instance; actor equipment snapshots reference those instance IDs. The former
root equipment snapshot is removed. Validation rejects missing instances,
duplicate instance identity, one instance assigned to multiple actors, and
equipment IDs that collide with actor runtime IDs. Convergence supplies no
automatic v15-to-v16 migration for these unreleased formats.

Version 17 replaces the fixed equipment-slot wire enum with authored
`ContentId` slot keys. Inventory instance ownership and actor loadout references
remain the sole saved equipment authorities; Defense, Evasion, granted skills,
basic attacks, and accessory modifiers are derived again from catalog
definitions after load rather than duplicated into the save. Convergence
supplies no automatic v16-to-v17 migration for these unreleased formats.
The restore profile carries both numeric contributions and granted skill IDs;
passive grants are therefore validated and restored through the same passive
snapshot integrity boundary as learned passive skills.

Version 18 replaces the unnamed wallet balance with an immutable currency
ledger keyed by qualified `ContentId`. Every transaction names its currency;
single-currency hosts may use the explicit convenience accessor only when the
ledger contains exactly one entry. Convergence supplies no automatic
v17-to-v18 migration for these unreleased formats.

## Related Guidance

- [Actors And Runtime State](../developer-guide/actors-and-runtime-state.md)
- [Runtime Actor State And Restoration](../technical/runtime-actor-state-and-restoration.md)
- [Godot Integration Contract](../godot-integration-contract.md)
