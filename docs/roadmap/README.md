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
- [Order 2 Post-Closure Corrections Roadmap](combat-resolution-order-2-post-closure-corrections-roadmap.md)
- [Order 2 Charge Closure Corrections Roadmap](combat-resolution-order-2-charge-closure-roadmap.md)
- [Turn Economy Order 3 Roadmap](turn-economy-order-3-roadmap.md)
- [Documentation Completion Roadmap](documentation-completion-roadmap.md)
- [Framework Capability Matrix](framework-capability-matrix.md)
- [Production-Readiness Completion Record](production-readiness-roadmap.md)

Documentation maturity is tracked separately through the
[documentation coverage matrix](../reference/documentation-coverage.md). A
framework capability may be implemented completely while its documentation still
requires owner review. The Documentation Completion Roadmap governs the ordered
collaborative review of those outstanding entries.

Order 1's mechanics, audience documentation, and typed execution capability
remain owner-confirmed and complete. Order 2, `combat_resolution`, is also
complete: O2-R35 through O2-R40 corrected exact charge participation, optional
composition, and malformed custom receipts; O2-R41 completed the fresh source
and release-gate recheck. Order 3 is complete under its source-first correction
roadmap. Its final O3-R16 guard enforces explicit termination for replacement
economies, and O3-R17 independently reconciled the implementation, audience
documentation, and full release gate. Order 4, status and passive lifecycle,
is reopened under the
[26 July fresh closure audit](../reviews/status-passive-lifecycle-order-4-fresh-closure-audit-2026-07-26.md).
O4-R18 and O4-R19 corrected the two runtime findings, O4-R20 reconciled the
audience guidance, and O4-R21 then reproduced two additional integration
defects under the
[R21 extension audit](../reviews/status-passive-lifecycle-order-4-r21-extension-audit-2026-07-26.md).
O4-R22 recorded the extension, O4-R23 stabilized turn-start exact-instance
scheduling, O4-R24 integrated encounter-owned departure cleanup, and O4-R25
reconciled all three documentation audiences. O4-R26 completed its fresh review
but rejected closure after finding four additional passive and restore
correctness paths. The
[R26 correction audit](../reviews/status-passive-lifecycle-order-4-r26-correction-audit-2026-07-26.md)
governs O4-R27 through O4-R32. O4-R27 through O4-R30 corrected all four
runtime paths, O4-R31 reconciled active guidance, and the
[final O4-R32 closure review](../reviews/status-passive-lifecycle-order-4-final-closure-review-2026-07-26.md)
found no unresolved realistic reachable defect in that corrected revision. A
[second independent audit](../reviews/status-passive-lifecycle-order-4-second-independent-audit-2026-07-26.md)
reopened Order 4 under O4-R33 through O4-R36 for two staged-commit
defects and one audience-documentation omission. O4-R33 through O4-R35
corrected those findings, and the
[O4-R36 closure review](../reviews/status-passive-lifecycle-order-4-r36-closure-review-2026-07-26.md)
closed that source revision. The
[third independent audit](../reviews/status-passive-lifecycle-order-4-third-independent-audit-2026-07-26.md)
then reproduced cross-target owner-turn sequence failure and shared phase-event
clock divergence. O4-R38 corrected the runtime with one sequence stream per
lifecycle event ID, and O4-R39 reconciled the three documentation audiences.
The
[O4-R40 closure review](../reviews/status-passive-lifecycle-order-4-r40-closure-review-2026-07-26.md)
found no unresolved reachable defect at that revision. The
[fourth independent audit](../reviews/status-passive-lifecycle-order-4-fourth-independent-audit-2026-07-26.md)
then found one narrow programmatic flee-outcome validation defect and stale
save-version guidance. O4-R42 corrected the runtime, O4-R43 and O4-R43A
corrected current save-v13 guidance, and the
[O4-R44 closure review](../reviews/status-passive-lifecycle-order-4-r44-closure-review-2026-07-26.md)
independently passed the source, documentation, and complete release gate.
The
[bounded final certification](../reviews/status-passive-lifecycle-order-4-final-certification-2026-07-26.md)
then added independent sequence-model and public-restore evidence, corrected
the committed action-end save-checkpoint guidance, and found no qualifying
runtime defect. Order 4 is formally closed. Order 5, `battle_knowledge`, has
implemented O5-R1 through O5-R8, but a later fresh source audit reopened one
custom-effect provenance boundary. O5-R10 through O5-R13 now govern authority
validation, regressions, audience reconciliation, and a new closure review.
