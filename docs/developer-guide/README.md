# Developer Guide

## Purpose

This section will explain how a game developer composes Convergence through its
public contracts. Godot is the primary reference host, but the patterns remain
engine-neutral.

Developer guides focus on:

- required services and repositories;
- host-supplied commands, events, content, randomness, and persistence;
- framework-owned versus host-owned state;
- cancellation, diagnostics, and rejected operations;
- optional modules and replaceable policies;
- focused integration examples.

## Guides

- [Actors And Runtime State](actors-and-runtime-state.md): actor creation,
  canonical party and roster ownership, Vessel composition, growth, pending
  skill choices, and aggregate restoration.
- [Typed Actions And Effects](typed-actions-and-effects.md): canonical action
  composition, authorization, assess/present/execute flow, item reservations,
  cancellation, results, and host-mediated work.
- [Stat Modifier Policies](stat-modifier-policies.md): modifier authority,
  supplied policy models, counted lifecycle clocks, boundary sequences,
  removal, and Godot responsibilities.
- [Combat Resolution Policies](combat-resolution-policies.md): authored combat
  binding, execution-service composition, typed hit evidence, random-source
  contracts, replacement policies, and retained charge state.
- [Turn Economy Policies](turn-economy-policies.md): supplied economy
  selection, authored binding, typed presentation, replacement contracts,
  liveness, and the encounter-scheduling boundary.
- [Status And Passive Lifecycle](status-passive-lifecycle.md): ailment
  application, transition policies, passive targeting, lifecycle clocks,
  cleanup, typed events, persistence, and Godot host responsibilities.
- [Battle Knowledge Integration](battle-knowledge.md): persistent and
  encounter scopes, typed execution transitions, Analyze policy, team seeds,
  familiar imports, UI queries, and save boundaries.
- [Encounter Orchestration Integration](encounter-orchestration.md):
  initiative, supplied and replacement schedulers, lifecycle, command,
  completion, cancellation, fault, event, automated-runner, and Godot
  composition.
- [Inventory, Equipment, And Economy Integration](inventory-equipment-and-economy.md):
  economy-ruleset binding, equipment instance IDs and live profiles, authored
  slot policies, atomic shop and recovery results, save authority, and Godot
  adoption.
- [Generic Navigation Integration](generic-navigation.md): host-authored
  locations, injected access policy, scene adoption, optional traversal, and
  save/restore boundaries.

The actor and typed action/effect guides have completed collaborative review.
That Order 1 review includes stat-modifier policy composition and integration.
The combat guide completed the Order 2 source review and documentation gate,
including ordered secondary effects, complete-action outcome pricing, and
validated host-custom effect results. The final pre-closure correction review
found no remaining reachable defect in that supported scope.
The turn-economy guide completed the Order 3 source and correction workflow for
Action Token, neutral standard actions, custom snapshot authority, and finite
phase liveness.
The status-lifecycle guide records the implemented Order 4 composition and
schema-v10 explicit lifetime authoring. Its mechanics, developer, and technical
documents completed independent source reconciliation through O4-R11.
The battle-knowledge guide records the owner-confirmed Order 5 distinction
between durable entity facts and encounter-instance facts. It routes DemoHost
and automated battles through framework-owned typed evidence rather than
host-side defense inspection.
The encounter-orchestration guide records the implemented Order 6 scheduler,
lifecycle, command, cancellation, completion, and canonical event contracts.
O6-R13L reconciled its composition examples and boundaries at that revision.
The later O6-R14 source audit reopened it for automated-result and completion
integration corrections. O6-R15 through O6-R19 and O6-R21 through O6-R22
corrected and reconciled those contracts. O6-R23 independently re-read the
corrected source and returned the guide to `reviewed`. O6-R24 later reopened
the guide as `existing_unreviewed` until normal completion and fault metadata
are separated and the corrected host contract is independently checked.
O6-R25 and O6-R26 completed those corrections. O6-R27 independently traced the
current integration boundary and restored the guide to `reviewed` at that
revision. O6-R33 subsequently reproduced two custom-policy validation gaps and
one cleanup-boundary wording ambiguity, returning the guide to
`existing_unreviewed`. O6-R34 and O6-R35 corrected the runtime boundaries,
O6-R36 reconciled this guide, and O6-R37 independently traced the current
composition contract and restored the guide to `reviewed`.
O6-R38 later reopened the guide after finding stable-ring and
economy-liveness defects. O6-R39 and O6-R40 correct those paths, and O6-R41
reconciles the extension and phase turn-window contracts. The guide is
`reviewed` again, and the O6-R42 independent review formally closes the
capability.
The later O6-R43 source audit reopens the guide for event-delivery authority,
primary command-fault preservation, and one incorrect interface name. It is
`existing_unreviewed` until O6-R44 through O6-R47 are complete.
O6-R44 preserves canonical event identity when optional sink publication
fails, O6-R45 preserves the primary command fault when cleanup also fails, and
O6-R46 reconciles the composition guidance and interface name. The
[O6-R47 final closure review](../reviews/encounter-orchestration-order-6-r47-final-closure-review-2026-08-05.md)
independently traces the corrected host boundary. This guide is `reviewed`, and
Order 6 is formally complete.
The Order 7 inventory/economy guide was `reviewed` after O7-R15 rechecked its
composition examples and host-adoption rules against current APIs and tests.
The
[O7-R15 final closure review](../reviews/inventory-equipment-economy-order-7-r15-final-closure-review-2026-08-24.md)
is historical closure evidence for that revision. The later
[post-R15 independent audit](../reviews/inventory-equipment-economy-order-7-post-r15-independent-audit-2026-08-31.md)
reopened the guide as `existing_unreviewed`. O7-R16 through O7-R18 correct the
repository-identity and authorization-cancellation boundaries, O7-R19
reconciles the guidance, and the
[O7-R20 independent audit](../reviews/inventory-equipment-economy-order-7-r20-independent-closure-audit-2026-09-09.md)
found one further skill-identity defect. O7-R21 corrects that defect, O7-R22
reconciles this guide, and the
[O7-R23 final closure review](../reviews/inventory-equipment-economy-order-7-r23-final-closure-review-2026-09-11.md)
independently re-read the corrected implementation and guide and completed the
retained release gate. This guide is `reviewed`, and Order 7 is formally
complete.
Other subsystem guides remain tracked as
`existing_unreviewed` or `missing` in
the [documentation coverage matrix](../reference/documentation-coverage.md).
The Order 8 navigation guide was source-reconciled and owner-confirmed, so its
coverage entry is `reviewed`. The real Godot smoke sample does not yet execute
live navigation; Order 8 remains open until its retained release gate passes.

New guides must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
