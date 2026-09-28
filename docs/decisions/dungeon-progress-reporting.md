# Decision: Dungeon Progress Reporting

Status: confirmed
Date: 2026-09-19

## Context

The dungeon service records an eligible checkpoint or boss ID idempotently.
It does not know whether the player touched a checkpoint, defeated a boss in
battle, solved a puzzle, or completed a story event.
Requiring only a Framework battle result would exclude legitimate
non-battle outcomes and couple optional traversal to one combat system.

The project owner approved O9-D1 through O9-D6 after the Order 9 discussion on
19 September 2026. O9-R2 through O9-R6 subsequently implemented the live
request, progress eligibility, save/host, and authored-floor boundaries. The
23 September fresh audit then found two authority gaps: malformed retained
history could reach live policy evaluation, and restored progress did not use
the live registry. O9-C1 and O9-C2 correct those gaps; O9-C3 aligns fixtures
and guidance. Post-correction independent closure and audience confirmation
remain separate gates.

## Confirmed Decision

1. A trusted game host reports that a checkpoint or boss outcome occurred.
   Reporting does not happen automatically on node entry.
2. The game supplies an immutable list of eligible checkpoint and boss IDs,
   their dungeon IDs, and the areas where they may be reported. Framework
   rejects malformed IDs and reports outside that declared context, then
   records a valid report once. Duplicate reports are no-ops. No new dungeon
   JSON shape is required merely to establish these host-owned identities.
3. Framework does not require a battle-victory receipt. Games can report a
   boss defeated by battle, puzzle, or script, subject to the same eligibility
   contract. A stricter battle-result coordinator may be supplied later only
   for a demonstrated use case.
4. The host must issue the report only after its actual success condition.
   Framework context validation cannot prove that Godot rendered a win or
   that arbitrary host code is truthful.
5. Existing authored floor metadata is optional information for the game to
   read from the catalog. It does not automatically move through nodes or
   start battles. Order 9 does not add a new fixed-floor resolver without a
   consuming need. A floor may contain zero, one, or multiple host-triggered
   visible encounters.
6. Leaving the dungeon retains progress but does not leave traversal active.
   On re-entry, the host selects an entrance or an unlocked checkpoint; the
   last visited node is never an implicit re-entry destination.
7. A host that requires an active dungeon position rejects an inside-dungeon
   save without that position before adopting it. This is a host-specific
   load failure with a clear diagnostic, not a Framework-wide prohibition of
   navigation-only saves or a silent entrance guess.
8. Invalid live dungeon IDs and faulty custom travel rules produce distinct,
   typed, non-mutating failures. A legal blocked route remains a different
   result. Cancellation and fatal failures still propagate rather than being
   disguised as ordinary gameplay rejection.

## Example

A visible scene enemy is defeated. The host observes a successful encounter,
then reports the boss ID and current dungeon context. Framework validates the
report and remembers the defeat. If the player loses, the host does not report
it. If a guardian is resolved by a puzzle instead, the host may report the
same kind of progress without inventing a battle result.

## Alternatives

- **Mandatory battle proof:** stronger coupling for battle-only games, but
  excludes puzzle/story resolutions and alternative combat implementations.
- **Unvalidated host assertion:** the former service could record arbitrary IDs
  at unrelated nodes. O9-R5 replaced that behavior with injected eligibility
  declarations and typed rejection.

## Implementation Boundary

`RuntimeDungeonProgressRegistry` declares eligible IDs, dungeons, and areas;
`RuntimeDungeonTraversalService` checks reports before idempotent recording.
Typed traversal and progress results distinguish invalid requests, wrong
context, ordinary route denial, and policy faults. These contracts do not make
Godot, a scene graph, a battle system, or a fixed-floor resolver mandatory.
`RuntimeSaveValidator.CreateWithDungeonProgressRegistry` applies the same
declaration authority to retained checkpoint and boss records before aggregate
restore. Games with no retained checkpoint or boss records do not need that
optional validator composition. Save validation does not add a battle receipt
or infer progress from visiting a node.
The broad save aggregate remains at v19 unless a separate, explicitly
approved save-contract change proves necessary; independent navigation and
dungeon nullability remains Order 13's question.

## Evidence

The [Order 9 source review](../reviews/dungeon-traversal-order-9-source-review-2026-09-19.md)
records the opening behavior, while the
[fresh closure audit](../reviews/dungeon-traversal-order-9-fresh-closure-audit-2026-09-23.md)
records the authority gaps and O9-C1 through O9-C4 correction sequence. The
[Order 9 roadmap](../roadmap/dungeon-traversal-order-9-roadmap.md) tracks
subsequent implementation and review. Current audience guidance is the
[world mechanics page](../mechanics/world-encounters-and-rewards.md),
[dungeon developer guide](../developer-guide/dungeon-traversal.md), and
[dungeon technical reference](../technical/dungeon-traversal-runtime.md).
All three remain `existing_unreviewed` until the post-correction audit and
explicit project-owner confirmation; this decision record alone does not
promote them.
