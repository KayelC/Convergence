# Decision: Dungeon Progress Reporting

Status: proposed
Date: 2026-09-19

## Context

The current dungeon service records a supplied checkpoint or boss ID
idempotently. It does not know whether the player touched a checkpoint,
defeated a boss in battle, solved a puzzle, or completed a story event.
Requiring only a Framework battle result would exclude legitimate
non-battle outcomes and couple optional traversal to one combat system.

The project owner asked for an explanation of the two approaches and then
requested Order 9 production planning. The discussion favors explicit game
reporting with Framework validation, but has not yet settled the exact
eligibility declarations or re-entry behavior. This record is therefore
**proposed**, not a claim that the correction is implemented or fully approved.

## Proposed Decision

1. A trusted game host reports that a checkpoint or boss outcome occurred.
   Reporting does not happen automatically on node entry.
2. Framework rejects malformed IDs and outcomes inconsistent with the
   selected dungeon and declared eligible context, then records a valid
   report once. Duplicate reports are no-ops.
3. Framework does not require a battle-victory receipt. Games can report a
   boss defeated by battle, puzzle, or script, subject to the same eligibility
   contract. A stricter battle-result coordinator may be supplied later only
   for a demonstrated use case.
4. The host must issue the report only after its actual success condition.
   Framework context validation cannot prove that Godot rendered a win or
   that arbitrary host code is truthful.

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

## Open Details

- Where do eligible checkpoint/boss IDs and their dungeon/node constraints
  live: a host-supplied immutable registry or a small authored content shape?
- Does re-entry resume the previous node or begin at an entrance/checkpoint
  selected by the host? Retained progress must not determine active location.
- Should a host requiring dungeon progress reject a navigation-only save from
  inside a dungeon, or initialize an explicit entrance state? The broad
  Framework save shape remains unchanged until a separate save decision.

## Evidence

The [Order 9 source review](../reviews/dungeon-traversal-order-9-source-review-2026-09-19.md)
identifies current behavior, and the [Order 9 roadmap](../roadmap/dungeon-traversal-order-9-roadmap.md)
holds implementation behind these decisions. Current player-facing text is in
the [world mechanics page](../mechanics/world-encounters-and-rewards.md);
developer and technical dungeon pages remain scheduled under O9-R7. No
documentation coverage entry is promoted by this proposed record.
