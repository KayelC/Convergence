# Decision: Dungeon Progress Reporting

Status: confirmed
Date: 2026-09-19

## Context

The current dungeon service records a supplied checkpoint or boss ID
idempotently. It does not know whether the player touched a checkpoint,
defeated a boss in battle, solved a puzzle, or completed a story event.
Requiring only a Framework battle result would exclude legitimate
non-battle outcomes and couple optional traversal to one combat system.

The project owner approved O9-D1 through O9-D6 after the Order 9 discussion on
19 September 2026. These are intended rules for implementation, not a claim
that the current runtime already enforces them.

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
- **Unvalidated host assertion:** matches the current service, but can record
  arbitrary IDs at unrelated nodes and gives save/restore little trustworthy
  context to check.

## Implementation Boundary

The precise C# types and diagnostic names are chosen during isolated Order 9
checkpoints. They must satisfy the approved behavior above without making
Godot, a scene graph, a battle system, or a fixed-floor resolver mandatory.
The broad save aggregate remains at v19 unless a separate, explicitly
approved save-contract change proves necessary; independent navigation and
dungeon nullability remains Order 13's question.

## Evidence

The [Order 9 source review](../reviews/dungeon-traversal-order-9-source-review-2026-09-19.md)
identifies current behavior, and the [Order 9 roadmap](../roadmap/dungeon-traversal-order-9-roadmap.md)
separates approved behavior from pending implementation. Existing affected
audience guidance is the [world mechanics page](../mechanics/world-encounters-and-rewards.md),
[generic navigation developer guide](../developer-guide/generic-navigation.md),
and [navigation technical reference](../technical/generic-navigation-runtime.md).
Dedicated dungeon developer and technical pages remain scheduled under O9-R7.
No documentation coverage entry is promoted by this decision record alone.
