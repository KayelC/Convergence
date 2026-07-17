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

The actor reference has completed collaborative review. Typed action/effect and
stat modifier references remain `existing_unreviewed` while the implemented
policy family receives its fresh source and documentation reviews. Other subsystem references
remain tracked as `existing_unreviewed` or `missing` in the
[documentation coverage matrix](../reference/documentation-coverage.md).

New technical pages must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
