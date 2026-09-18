# Technical Documentation

## Purpose

This section will document the internal invariants that maintainers need to
preserve across Convergence modules.

Technical pages focus on:

- state machines and legal transitions;
- mutation, transaction, and rollback boundaries;
- lifecycle and event ordering;
- identity and ownership invariants;
- save validation and restoration dependencies;
- arithmetic domains and typed failure containment;
- source and test evidence.

## References

- [Runtime Actor State And Restoration](runtime-actor-state-and-restoration.md):
  actor authority, roster invariants, Vessel composition, progression
  transactions, move-list decisions, save validation, and dependency-ordered
  restoration.
- [Typed Action And Effect Execution](typed-action-and-effect-execution.md):
  action authorization, assessment ownership, prepared targets, skill costs,
  item reservations, ordered effects, and actor transaction boundaries.
- [Stat Modifier Policy Runtime Authority](stat-modifier-policy-runtime.md):
  immutable policy state, signal transitions, contribution projection,
  lifecycle clocks, boundary ordering, and persistence requirements.
- [Combat Resolution Pipeline](combat-resolution-pipeline.md): coherent
  authored policy composition, hit/critical sequencing, standard arithmetic,
  charge lifetime, multi-hit mutation, outcome aggregation, and atomicity.
- [Turn Economy Runtime](turn-economy-runtime.md): phase snapshot authority,
  supplied transition tables, liveness counters, typed events, fault
  containment, and the boundary with actor scheduling.
- [Status And Passive Lifecycle Runtime](status-passive-lifecycle.md): lifetime
  authority, staged application, explicit clocks, cleanup, passive dispatch,
  event evidence, startup atomicity, and save-v14 restoration.
- [Battle Knowledge Runtime Authority](battle-knowledge-runtime.md): persistent
  and encounter authorities, evidence transitions, Analyze disclosure, AI team
  sharing, familiarity, atomicity, and session-save validation.
- [Encounter Orchestration Runtime](encounter-orchestration-runtime.md):
  scheduler protocol, lifecycle transactions, fixed-point reconciliation,
  command/economy authority, structural event ordering, cancellation, and
  typed fault containment.
- [Inventory, Equipment, And Economy Runtime](inventory-equipment-economy-runtime.md):
  equipment-instance and loadout authority, slot validation, live profile
  projection, typed currency, atomic shop/recovery transitions, and save v19
  restoration.
- [Generic Navigation Runtime Authority](generic-navigation-runtime.md):
  request validation, policy and result matrix, immutable event evidence,
  host adoption, and optional save-state ownership.

The actor and typed action/effect references have completed collaborative
review. That Order 1 review includes stat-modifier runtime authority and its
integration boundaries. The combat pipeline completed its owner-confirmed
Order 2 source review, including dependency gating, current life-state
eligibility, shared contact, complete-action aggregation, and custom result
validation. The final pre-closure correction review found no remaining
reachable defect in that supported scope. Order 3 source-reconciled the
turn-economy runtime authority, liveness, and scheduling boundary.
The Order 4 status-lifecycle reference is written from current source. Schema
v8 preserves explicit expiration and removal profiles from authored JSON, and
its final certification completed the independent closure gate.
The Order 5 knowledge reference traces current source through execution,
automated strategy, familiarity, and persistence boundaries. Its final
independent closure review remains separate from this documentation checkpoint.
The Order 6 encounter reference traces current scheduler, lifecycle,
turn-economy, event, cancellation, completion, and automated-runner source. It
completed independent source reconciliation in O6-R13L, but the later O6-R14
source audit reproduced four uncovered runtime paths. O6-R15 through O6-R19
and O6-R21 through O6-R22 corrected and reconciled those paths. O6-R23
independently re-read the corrected source and returned the reference to
`reviewed`. O6-R24 later reopened it as `existing_unreviewed` because returned
fault-finalization evidence is not always identical to successful sink
publication and normal completion text currently crosses the fault-result
boundary. O6-R25 and O6-R26 corrected both contracts, and O6-R27 independently
traced the current source before restoring the reference to `reviewed` at that
revision. O6-R33 subsequently reproduced missing no-cost economy and scheduler
round-continuity validation, so the reference was `existing_unreviewed`.
O6-R34 and O6-R35 corrected both runtime boundaries, O6-R36 reconciled this
reference, and O6-R37 independently traced current source and restored it to
`reviewed`.
O6-R38 later reopened the reference after finding stable-ring and
economy-liveness defects. O6-R39 and O6-R40 correct those paths, and O6-R41
reconciles this reference with the exact phase turn-window safety boundary. It
is `reviewed` again, and the O6-R42 independent review formally closes the
capability.
The later O6-R43 source audit reopens the reference for canonical event
retention and primary command-fault authority. It is `existing_unreviewed`
until O6-R44 through O6-R47 are complete.
O6-R44 preserves canonical event identity when optional sink publication
fails, O6-R45 preserves the primary command fault when cleanup also fails, and
O6-R46 reconciles the runtime contract. The
[O6-R47 final closure review](../reviews/encounter-orchestration-order-6-r47-final-closure-review-2026-08-05.md)
independently traces the corrected source. This reference is `reviewed`, and
Order 6 is formally complete.
The Order 7 inventory/economy reference was `reviewed` after O7-R15 traced its
authority graph, transaction ordering, and save v19 diagrams against current
source and tests. The
[O7-R15 final closure review](../reviews/inventory-equipment-economy-order-7-r15-final-closure-review-2026-08-24.md)
is historical closure evidence for that revision. The later
[post-R15 independent audit](../reviews/inventory-equipment-economy-order-7-post-r15-independent-audit-2026-08-31.md)
reopened the reference as `existing_unreviewed`. O7-R16 through O7-R18 correct
the repository-identity and authorization-cancellation boundaries, O7-R19
reconciles the reference, and the
[O7-R20 independent audit](../reviews/inventory-equipment-economy-order-7-r20-independent-closure-audit-2026-09-09.md)
found one further skill-identity defect. O7-R21 corrects that defect, O7-R22
reconciles this reference, and the
[O7-R23 final closure review](../reviews/inventory-equipment-economy-order-7-r23-final-closure-review-2026-09-11.md)
independently re-read the corrected implementation and reference and completed
the retained release gate. This reference is `reviewed`, and Order 7 is
formally complete.
Other subsystem references
remain tracked as `existing_unreviewed` or `missing` in the
[documentation coverage matrix](../reference/documentation-coverage.md).
The Order 8 navigation reference remains `existing_unreviewed` until its
independent closure review.

New technical pages must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
