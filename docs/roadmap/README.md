# Roadmap And Capability Status

## Purpose

This section owns current priorities, capability maturity, and completed release
records. It does not define individual mechanics.

## Active Records

- [Product Roadmap](product-roadmap.md)
- [Actor Composition, Progression, Roster, And Stage Roadmap](actor-composition-progression-roster-roadmap.md)
- [Stat Modifier Policy Roadmap](stat-modifier-policy-roadmap.md)
- [Order 2 Combat Resolution Roadmap](combat-resolution-order-2-roadmap.md)
- [Order 2 Combat Resolution Corrections Roadmap](combat-resolution-order-2-corrections-roadmap.md)
- [Ordered Secondary Effects And Order 2 Correction Roadmap](ordered-secondary-effects-roadmap.md)
- [Order 2 Closure Corrections Roadmap](combat-resolution-order-2-closure-corrections-roadmap.md)
- [Order 2 Pre-Closure Audit Corrections Roadmap](combat-resolution-order-2-pre-closure-audit-corrections-roadmap.md)
- [Documentation Completion Roadmap](documentation-completion-roadmap.md)
- [Framework Capability Matrix](framework-capability-matrix.md)
- [Production-Readiness Completion Record](production-readiness-roadmap.md)

Documentation maturity is tracked separately through the
[documentation coverage matrix](../reference/documentation-coverage.md). A
framework capability may be implemented completely while its documentation still
requires owner review. The Documentation Completion Roadmap governs the ordered
collaborative review of those outstanding entries.

Order 1's mechanics and audience documentation remain owner-confirmed. Its
`typed_action_and_effect_execution` implementation state is temporarily
`partial` because O2-R24 owns the newly found duplicate-cost boundary.
Order 2, `combat_resolution`, remains owner-confirmed at the mechanics level.
O2-R23 closed its earlier bounded-hit and authored-percentage corrections at
revision `e26bdc5`. A later source-first audit found three narrower action,
host-contract, and authoring-boundary discrepancies. Order 2 is reopened under
O2-R24 through O2-R27; Order 3 waits for those corrections and a fresh closure
review.
