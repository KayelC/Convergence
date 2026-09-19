# Decision: Generic Navigation And Host Adoption

Status: confirmed
Date: 2026-09-18

## Context

Convergence supplies logical location transitions, not Godot scene movement.
The intended game can use visual-novel-style overworld selection and spatial
3D dungeon exploration in one session. The host supplies both presentation
styles and invokes the corresponding framework service only at meaningful
logical boundaries.

The source-first Order 8 review found invalid live identifiers, inconsistent
public results, unclear custom-policy failures, and a DemoHost context error.
It also identified a separate choice about how optional navigation and dungeon
progress appear in the broad save aggregate.

## Decisions

1. **D1-D2, logical and host-authored:** a host explicitly requests travel
   using its own valid location and transition IDs. An injected policy decides
   access. Framework does not define scenes, menus, spatial movement, route
   content, or automatic reverse transitions.
2. **D3, invalid request:** reject an empty current location or transition,
   source, or destination ID before source matching or policy evaluation.
   Return a typed, non-mutating result identifying the offending field.
3. **D4, policy fault:** a throwing or null-returning custom policy produces a
   typed non-mutating fault result. Navigation does not gain an asynchronous
   policy or scene-loading responsibility. Operational cancellation and
   out-of-memory failure are not policy programming faults and still propagate.
4. **D5, result authority:** public navigation results must have coherent
   code, before/after state, reason, and event evidence. Undefined enum values
   and contradictory results are rejected even when supplied by a custom
   service implementation.
5. **D6, save shape:** retain save contract v19 in Order 8. `Field` is optional;
   when present, navigation is required and dungeon progress is optional.
   Runtime modules remain independently composable. A dungeon-only game using
   this broad save aggregate supplies a stable neutral logical location. The
   broader independent-nullability question belongs to Order 13. No v20
   migration is introduced here.
6. **D7, same-location travel:** the framework does not prohibit it. A game
   policy may approve re-entry, refresh, or a scripted threshold.
7. **D8, presentation authority:** typed codes and reason IDs drive host
   behavior. English messages are diagnostic, not game logic or required UI.
8. **D9, host adoption:** `Applied` is a logical destination candidate. A
   Godot host adopts `After` only after its own scene work succeeds. On scene
   failure it retains `Before`; no framework event claims a scene was loaded.
9. **D10, active context:** retained dungeon progress does not establish the
   player's present location. DemoHost save/menu context follows the current
   logical navigation location rather than the presence of dungeon progress.

## Example

In the visual-novel overworld, a location button can request a transition to
`dungeon_entrance`. Godot presents or loads the destination, then adopts the
approved snapshot. Inside the 3D dungeon, Godot owns continuous movement and
uses the separate dungeon traversal service for meaningful node/checkpoint
changes. Returning to an overworld location retains dungeon progress for a
future visit without labelling the player as still inside the dungeon.

## Consequences

- Order 8 corrects navigation and host adoption without changing the save wire
  shape or implementing Order 9 dungeon rules.
- Framework remains independent of Godot and any visual-novel plugin.
- The [world mechanics page](../mechanics/world-encounters-and-rewards.md),
  [developer guide](../developer-guide/generic-navigation.md), and
  [technical reference](../technical/generic-navigation-runtime.md) were
  reconciled with the implementation, owner-confirmed, and reviewed in Order 8.
- The Order 8 review and checkpoint sequence remain in
  [Navigation Order 8 Source Review](../reviews/navigation-order-8-source-review-2026-09-14.md).

## Alternative Considered

Making navigation and dungeon traversal independently nullable inside
`RuntimeFieldSnapshot` would represent dungeon-only saves without a neutral
location, but it would require a v20 wire break before the wider Order 13 save
review. The owner selected the current v19 aggregate for Order 8 instead.

## Evidence

Each O8-R2 through O8-R8 checkpoint adds focused source and test evidence under
the linked review. This decision record defines approved intent, not a claim
that all corrections are already implemented.
