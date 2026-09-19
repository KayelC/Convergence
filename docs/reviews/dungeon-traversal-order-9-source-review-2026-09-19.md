# Dungeon Traversal Order 9 Source Review

**Date:** 19 September 2026

**Capability:** `dungeon_traversal`

**Source baseline:** `d5f8845d` (`review: close navigation order 8`)

**Scope:** Framework traversal, authored dungeon content, encounter preparation,
Training Annex, navigation/field/save integration, and relevant tests.

This is the opening source review, not a closure review or approval to change a
public contract. The ordered work is in the
[Order 9 roadmap](../roadmap/dungeon-traversal-order-9-roadmap.md). Current
source and executable tests establish what works today; the proposed changes
below remain pending until the owner confirms their precise rules.

## Plain-English Model

- **Navigation** answers where the player is in the wider game. A host can
  return to an overworld location while remembering dungeon progress.
- **Dungeon traversal** remembers a dungeon and a current meaningful node,
  plus visited nodes, unlocked checkpoints, and defeated bosses. It does not
  move a Godot character continuously or load a scene.
- **Encounter preparation** begins only when the host explicitly reports a
  trigger and selects an authored encounter. Entering a floor or node does not
  automatically start a battle.
- **Save context** answers whether saving is allowed *here and now*. Retained
  dungeon progress alone does not mean the player is inside the dungeon.

The game may combine visual-novel overworld selection with spatial 3D dungeon
exploration. A Godot trigger, console option, or script can report a meaningful
node transition; ordinary steps in a 3D scene remain Godot-owned.

## Source-Verified Current Behavior

| Boundary | What source currently does |
|---|---|
| `RuntimeDungeonTraversalService.Traverse` | Checks dungeon and source-node equality, then calls one injected policy; an allowed transition changes the node and appends it to visited nodes. |
| `UnlockCheckpoint` / `RegisterBossDefeat` | Idempotently add the supplied ID to progress; neither calls the policy nor verifies a scene, checkpoint definition, or encounter outcome. |
| `RuntimeDungeonTraversalSnapshot` | Retains immutable lists and always includes the current node among visited nodes; its public constructor accepts default/empty IDs. |
| `DungeonDefinition` | Contains blocks, floor ranges, encounter pools, and optional fixed-floor metadata. Catalog validation checks references and floor bounds. |
| Training Annex | Uses host-authored named-node transitions and a host policy; its demo requests an encounter separately through `CatalogEncounterPreparationService`. It fetches the dungeon definition but does not interpret its fixed floors to move nodes. |
| `RuntimeFieldSnapshot` | Requires navigation when field state exists; dungeon progress is optional and can remain after travel outside. |
| Save validator and aggregate restore | Validate the logical location ID and catalog dungeon ID, then return the saved field snapshot. They do not know a host's node graph or current scene. |
| `CurrentSaveContext` | Uses the logical navigation location, not the presence of retained dungeon progress. This is the correct Order 8 rule. |

Source anchors:
[`DungeonTraversal.cs`](../../src/Convergence.Framework/Runtime/DungeonTraversal.cs),
[`ContentSurfaceDefinitions.cs`](../../src/Convergence.Framework/Content/ContentSurfaceDefinitions.cs),
[`RuntimeFieldSnapshot.cs`](../../src/Convergence.Framework/Runtime/RuntimeFieldSnapshot.cs),
[`RuntimePersistenceSnapshots.cs`](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs),
[`TrainingAnnexPersistenceController.cs`](../../samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexPersistenceController.cs),
[`CleanTrainingAnnexPlayHost.cs`](../../samples/Convergence.DemoHost/Hosts/TrainingAnnex/CleanTrainingAnnexPlayHost.cs).

Focused baseline execution: 101 Framework navigation/dungeon/persistence tests
and three Training Annex field/save-context tests passed, with zero failures or
skips. These tests prove their named paths, not the missing cross-boundary
invariants below. No full release gate was run for this opening review.

## Findings And Design Boundaries

### O9-M1: Live dungeon input and policy faults lack navigation-grade handling

**Invariant:** a malformed live request must not become an applied dungeon
transition or a later invalid save; a faulty custom policy must have a defined
failure boundary.

`ContentId` can be default-initialized. The snapshot and transition constructors
accept such IDs; when both current and source are default, equality can pass
before an allowing policy returns `Applied`. A policy that returns `null` causes
a null dereference; a throwing policy escapes directly. Navigation already has
typed invalid-request and policy-fault behavior, so this is an inconsistent
extension boundary, not a claim of remote exploitability.

**Proposed correction:** validate IDs before the policy and define typed,
non-mutating invalid-request and policy-fault outcomes, with cancellation and
fatal exceptions still propagated. Obtain owner confirmation before choosing
whether the dungeon contract exactly mirrors navigation's result vocabulary.

### O9-M2: Public dungeon results can contradict their evidence

`RuntimeDungeonTraversalResult` and `RuntimeDungeonStateChangeResult` have public
constructors without checks tying code, before/after state, transition, and
events together. A custom service can report `Applied` with a rejection event
or a different dungeon ID, or report rejection with a changed snapshot. This
is reachable through the public host extension surface even though the
supplied service constructs coherent results.

**Proposed correction:** enforce coherent result and event shapes at the public
boundary, including undefined enums and record-clone paths; keep legitimate
custom service implementations possible.

### O9-M3: Progress registration is not yet bounded by dungeon context

`UnlockCheckpoint` and `RegisterBossDefeat` accept any ID at any node; they do
not consult the traversal policy or require a boss battle. They are currently
host assertions, not independently verified victories. The existing dungeon
content has no general checkpoint/boss ID registry tied to named host nodes, so
the Framework cannot honestly claim catalog eligibility checks today.

**Direction from owner dialogue:** the game reports a completed interaction,
victory, puzzle, or story event; battle-result proof is not mandatory. The
Framework should reject malformed or context-ineligible progress reports and
record valid ones idempotently. The precise declaration of eligible IDs and
nodes remains an owner decision; do not invent a policy or a catalog schema
solely to make the current implementation appear validated.

### O9-M4: Accepted field saves can strand the Training Annex menu

`RuntimeFieldSnapshot` deliberately permits navigation without dungeon
progress. A field at the Training Annex entrance with `DungeonTraversal ==
null` passes the Framework validator. The Training Annex restore check also
accepts it. However, its menu offers the leave and move options only when the
optional dungeon snapshot has a matching current node. After loading this
otherwise accepted save, the player has no travel option from that menu.

Normal `EnterTrainingAnnex` initializes dungeon progress, so this is an
accepted-save/consumer integration path, not an ordinary entry-flow failure.

**Proposed correction:** preserve Framework's optional field shape, but make
the Training Annex either reject this host-incompatible save before adoption
or initialize its host-owned entrance state by an explicit, tested rule. Do not
silently change the generic v19 save shape or infer active location from stale
dungeon progress.

### O9-Q1: Authored floors and runtime nodes currently have no shared meaning

Training Annex content describes floors 2-5 and fixed events. Its interactive
host traverses named nodes such as `review_hall`; no runtime service maps one
model to the other. This is an **unfinished integration/design choice**, not
proof that every step through a scene ought to be a Framework transition.

**Recommended direction:** floor/block definitions remain optional authored
metadata. Host-owned spatial triggers select meaningful nodes and encounter
IDs. An authored battle floor is not a compulsory one-battle-per-entry rule;
the game may place multiple visible enemies, respawns, or no enemy on a visit.
Decide whether Order 9 needs a read-only resolver for fixed-floor metadata or
whether the current content family should stay independent until a real host
uses it. Do not add a mandatory node/scene schema without that use case.

### O9-DOC1: Existing mechanics text overstates policy authority

The [world mechanics page](../mechanics/world-encounters-and-rewards.md) says
the injected dungeon policy governs checkpoints, barriers, and progress. In
source, the policy is called only by `Traverse`; checkpoint and boss recording
bypass it. The page must describe current behavior until a correction is
implemented and owner-confirmed.

## Navigation/Dungeon/Save Boundary

The following four cases must be considered together, not separately reviewed
as navigation, dungeon, and persistence features:

| Logical location | Retained dungeon progress | Intended interpretation |
|---|---|---|
| Outside | Absent | Ordinary non-dungeon field state |
| Outside | Present | Progress remembered; dungeon actions inactive |
| Inside | Present | Host may offer traversal after validating its node/context |
| Inside | Absent | Valid for generic Framework composition, but a host requiring an active dungeon must initialize or reject it explicitly |

`CurrentSaveContext` correctly follows the first column. A separate
host-specific compatibility check must protect the last row. This does not
require `RuntimeFieldSnapshot` to make dungeon state mandatory. Independent
navigation/dungeon nullability and any v20 save break remain Order 13 decisions.

## Evidence And Closure Standard

The next work must test malformed requests and custom policies, custom result
construction, idempotent progress recording, host-compatible restore of all
four table cases, scene-success adoption, authored floor/encounter selection
without automatic battles, and one-or-many entity encounter triggers. The
Framework must remain independent of Godot, filesystem, console, and host
presentation. The Order closes only after approved decisions, separately green
checkpoints, reviewed mechanics/developer/technical pages, an independent
source-and-doc recheck, and a retained full release gate.
