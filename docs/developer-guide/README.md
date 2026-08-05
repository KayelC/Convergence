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
schema-v8 explicit lifetime authoring. Its mechanics, developer, and technical
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
Other subsystem guides remain tracked as
`existing_unreviewed` or `missing` in
the [documentation coverage matrix](../reference/documentation-coverage.md).

New guides must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
