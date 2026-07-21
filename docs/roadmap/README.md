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
- [Documentation Completion Roadmap](documentation-completion-roadmap.md)
- [Framework Capability Matrix](framework-capability-matrix.md)
- [Production-Readiness Completion Record](production-readiness-roadmap.md)

Documentation maturity is tracked separately through the
[documentation coverage matrix](../reference/documentation-coverage.md). A
framework capability may be implemented completely while its documentation still
requires owner review. The Documentation Completion Roadmap governs the ordered
collaborative review of those outstanding entries.

Order 1, `typed_action_and_effect_execution`, is complete and owner-confirmed.
Order 2, `combat_resolution`, is owner-confirmed and its bounded-hit and
authored-percentage corrections are implemented and documented. Its first
independent recheck found one schema-only percentage-range omission, corrected
by O2-R22. O2-R23 then re-read the corrected source, reproduced the former
paths, and passed the complete release gate. Order 2 is closed; Order 3 may
begin.
