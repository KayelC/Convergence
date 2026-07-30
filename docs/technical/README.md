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
completed independent source reconciliation in O6-R13L and is now `reviewed`.
Other subsystem references
remain tracked as `existing_unreviewed` or `missing` in the
[documentation coverage matrix](../reference/documentation-coverage.md).

New technical pages must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
