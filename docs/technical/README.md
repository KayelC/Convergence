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

Other subsystem references remain tracked as `existing_unreviewed` or `missing`
in the [documentation coverage matrix](../reference/documentation-coverage.md).

New technical pages must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
