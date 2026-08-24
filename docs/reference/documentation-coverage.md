# Documentation Coverage

## Purpose

Framework implementation maturity and documentation maturity are different.
Convergence may have a complete, tested capability whose intended rules have not
yet been reviewed collaboratively with the project owner.

The executable documentation ledger is
[`../../tests/Convergence.Framework.Tests/Fixtures/documentation-coverage-matrix.json`](../../tests/Convergence.Framework.Tests/Fixtures/documentation-coverage-matrix.json).
It covers the same 25 capability IDs as the
[Framework Capability Matrix](../roadmap/framework-capability-matrix.md).

## Current Reading

The documentation matrix currently records 75 audience entries: 33 reviewed,
22 existing_unreviewed, 13 missing, and 7 not_applicable.

The actor composition, progression, party/roster, actor-restoration, and typed
action/effect documentation has completed the collaborative workflow. The
Order 1 review includes canonical action authority, prepared targets,
exactly-one item transactions, ordered effects, and the persistent,
timed-exclusive, and timed-contribution modifier policies. Their production
execution, lifecycle, ruleset, host, and save integration was checked against
current source before the project owner confirmed the explanation on 18 July
2026. Order 2 additionally covers the supplied combat policy family,
complete-action aggregation, explicit ordered effect dependencies, staged
life-state eligibility, secondary damage contact, and Action Token integration.
Order 3 now covers neutral standard actions, Action Token transitions, pass
precedence, finite liveness, typed phase evidence, and the explicit boundary
between opportunity counting and actor scheduling. Order 5 now additionally
covers persistent entity knowledge, encounter-local team knowledge,
conservative contact discovery, policy-controlled Analyze, familiarity imports,
combat-profile source/revision identity, profile-switch invalidation,
intrinsic-Almighty enforcement, and save boundaries. Other subsystem entries
remain unreviewed until they complete the same process.
Order 7 now documents inventory-owned equipment instances, authored slot
layouts, one live equipment profile, typed currencies, resolved pricing,
policy-owned stock, atomic shops, recovery, and save v19 across player,
developer, and maintainer audiences. O7-R10 first promoted those three
documents to `reviewed`; O7-R11 then corrected live-equipment, acquisition,
extension-boundary, and save-guidance findings. A later independent audit
reopened the pages after finding an incomplete custom economy bundle, stale
review-state callouts, and an obsolete `Shop.Buy` example. O7-R12 through
O7-R14 corrected and guarded those paths. The
[O7-R15 final closure review](../reviews/inventory-equipment-economy-order-7-r15-final-closure-review-2026-08-24.md)
independently re-read the resulting source and all three pages, found no
unresolved realistic reachable defect or contradiction, and returned all three
audience entries to `reviewed`.
O3-R7 independently verified the source and audience documents at that
revision. A later source-first recheck reopened the developer and technical
entries while three command-boundary defects were corrected. O3-R8 through
O3-R11 returned all three Order 3 audiences to `reviewed`, and O3-R12 closed
that revision. The owner-closure audit at `7aa3467e` then found that
port-provided events could impersonate runner-owned structural events. O3-R13
now enforces a fail-closed port-event allow-list, and O3-R14 re-read the source
and reconciled all three audience documents. They are reviewed again.
The fresh closure audit at `6e1169b5` subsequently found that custom economies
could ignore explicit phase termination while still passing transition
validation. O3-R16 now rejects that transition before owner-turn-end lifecycle
or accepted event publication. O3-R17 re-read the corrected source and
owner-confirmed guidance, so all three Order 3 entries are reviewed again.
The project owner explicitly confirmed the final Order 3 contract on 23 July
2026.
Order 4 completed its earlier source-first correction sequence, but the
[26 July fresh closure audit](../reviews/status-passive-lifecycle-order-4-fresh-closure-audit-2026-07-26.md)
reopened all three audience entries. O4-R18 and O4-R19 corrected the runtime
paths, and O4-R20 now defines queued-trigger membership and authored passive
activation-key validation across the mechanics, developer, and technical
audiences. O4-R21 then reproduced a separate turn-start scheduling failure and
missing encounter-owned departure cleanup. O4-R22 through O4-R25 recorded and
corrected both paths and reconciled the three audience documents. O4-R26
completed its independent review but rejected closure after finding four
additional passive and restore correctness paths. The
[R26 correction audit](../reviews/status-passive-lifecycle-order-4-r26-correction-audit-2026-07-26.md)
governs O4-R27 through O4-R32. O4-R27 through O4-R30 corrected those four
paths, and O4-R31 reconciled the mechanics, developer, and technical guidance
with the implementation. The
[final O4-R32 closure review](../reviews/status-passive-lifecycle-order-4-final-closure-review-2026-07-26.md)
re-read the corrected source and all three audiences without finding an
unresolved realistic reachable defect at that revision. A
[second independent audit](../reviews/status-passive-lifecycle-order-4-second-independent-audit-2026-07-26.md)
then reopened all three entries because ailment combat-profile composition is
missing and two atomicity claims exceed current runtime behavior. O4-R33 through
O4-R36 governed correction and fresh review.
O4-R33 through O4-R35 corrected those paths and reconciled all three audience
documents. The
[O4-R36 closure review](../reviews/status-passive-lifecycle-order-4-r36-closure-review-2026-07-26.md)
re-read that implementation and promoted the three entries to reviewed. The
[third independent audit](../reviews/status-passive-lifecycle-order-4-third-independent-audit-2026-07-26.md)
subsequently reproduced a cross-target timed-modifier fault and shared
phase-event divergence. The mechanics, developer, and technical entries are
therefore `existing_unreviewed` again. O4-R38 replaced actor-local and
team-local counters with event-keyed sequence authority, and O4-R39 reconciled
all audiences with that implementation. The
[O4-R40 closure review](../reviews/status-passive-lifecycle-order-4-r40-closure-review-2026-07-26.md)
independently re-read the corrected source and documents without finding an
unresolved reachable defect at that revision. The
[fourth independent audit](../reviews/status-passive-lifecycle-order-4-fourth-independent-audit-2026-07-26.md)
subsequently found one programmatic flee-outcome validation gap and stale
save-v10 labels in two stat-modifier pages. All three entries were therefore
returned to `existing_unreviewed`. O4-R42 corrected the runtime boundary,
O4-R43 and O4-R43A reconciled the current save-v13 guidance, and the
[O4-R44 closure review](../reviews/status-passive-lifecycle-order-4-r44-closure-review-2026-07-26.md)
independently re-read current source and all three audiences before passing the
complete release gate. All three entries are `reviewed` again.

Order 5 completed O5-R1 through O5-R8. The
[R8 Final Review](../reviews/battle-knowledge-order-5-r8-final-review-2026-07-27.md)
independently traced current Battle Knowledge source and all three audience
documents. Four reachable integration defects were corrected in isolated
commits, and the final source trace found no unresolved realistic Battle
Knowledge defect at that revision. A later fresh source audit found one bounded
custom-effect provenance defect that the developer and technical pages currently
overstate. Mechanics remains `reviewed`; developer and technical return to
`existing_unreviewed` until that boundary is corrected. O5-R10 now requires an
immutable action, actor, and target authority envelope and preflights the
complete evidence batch before any lower transition. O5-R11 covers every
mismatch, whole-batch rollback, and honest/hostile registered custom handlers.
O5-R12 reconciles the developer and technical pages with that public contract,
so all three Battle Knowledge audiences are `reviewed` again. The
[O5-R13 closure review](../reviews/battle-knowledge-order-5-provenance-closure-review-2026-07-27.md)
independently re-read the corrected source and documents and passed the complete
release gate. Order 5 was formally complete at that revision. The later
[post-closure independent audit](../reviews/battle-knowledge-order-5-post-closure-independent-audit-2026-07-27.md)
found that the mechanics remain aligned, but two exported competing authorities
and an incomplete standalone enum boundary make the developer and technical
surfaces incomplete. Mechanics remains `reviewed`; developer and technical
return to `existing_unreviewed`. O5-R15 removed actor-local Analyze state,
O5-R16 removed the disconnected mutable stores, and O5-R17 closed the
standalone enum boundary. O5-R18 closed the instant-defeat evidence-shape
defect, and O5-R19 reconciled the public contract and all three audience pages.
O5-R20 independently rechecked that wording against source, found and corrected
one sibling ailment-evidence coherence defect, and passed every locally
executable release gate. The online dependency lookup was unavailable in the
restricted local environment; connected CI remains authoritative for that
release check. Developer and technical return to `reviewed`. The matrix records
27 reviewed, 26 `existing_unreviewed`, 15 missing, and 7 `not_applicable`
entries.

The later
[post-R20 independent audit](../reviews/battle-knowledge-order-5-post-r20-independent-audit-2026-07-29.md)
traced current source across combat-profile composition and found that all three
audiences omit profile identity and replacement invalidation. It also found the
documented Almighty-storage invariant unenforced by public snapshots and save
validation. All three Battle Knowledge audiences return to
`existing_unreviewed`; the matrix now records 24 reviewed, 29
`existing_unreviewed`, 15 missing, and 7 `not_applicable` entries.

O5-R21 through O5-R23 correct that later audit: actor state now carries exact
combat-profile identity, durable facts use the source entity, encounter facts
invalidate on source or revision replacement, and every public ingestion path
rejects stored Almighty affinity knowledge. O5-R24 reconciles all three
audience pages and save-v15 guidance, so the Battle Knowledge entries return to
`reviewed` and the current matrix totals return to 27 reviewed and 26
`existing_unreviewed`. The
[O5-R25 independent closure review](../reviews/battle-knowledge-order-5-r25-independent-closure-review-2026-07-29.md)
subsequently re-read the corrected implementation and all three audience
documents, found no unresolved realistic reachable defect, and closed the
capability. At that revision, Order 6 became the next collaborative
documentation subject.

The review order and promotion gates for all capabilities are maintained in
the active
[Documentation Completion Roadmap](../roadmap/documentation-completion-roadmap.md).

Order 2 documentation now also reflects O2-R18 bounded hit execution and
O2-R19 authored-percentage rejection. O2-R20 reconciled those changes across
the three audience documents. O2-R22 corrected the schema-only range omission
found by the first independent recheck. O2-R23 completed a new current-source
trace and the full release gate without finding another reachable defect at
revision `e26bdc5`. A later pre-closure audit reopened the implementation gate
for three narrower cross-contract corrections. O2-R24 through O2-R27 corrected
those paths, O2-R28 closed the final custom-effect result boundary found by the
post-R27 source trace, and O2-R29 completed independent source and release-gate
verification. The confirmed audience entries remain reviewed and reconciled.
O2-R30 through O2-R34 subsequently unified runtime-registration preflight for
skills, items, and direct effect-backed actions, corrected current terminology
and version labels, documented that boundary for all three audiences, and
completed another independent source and release-gate verification.

The subsequent 22 July closure-readiness review temporarily returned all three
`combat_resolution` audience entries to `existing_unreviewed`. O2-R36 through
O2-R38 corrected exact charge participation, supplied disabled composition,
and reconciled all three audiences. O2-R39 then found one supported custom
executor could fabricate a source-less participation receipt. O2-R40 rejects
that receipt before mutation, and O2-R41's fresh source and release-gate review
found no unresolved reachable defect. The audience entries remain reviewed and
`combat_resolution` is complete.

Order 6 now has dedicated mechanics, developer, and technical encounter
orchestration pages. They trace the implemented team-phase and Agility
schedulers, lifecycle transactions, reconciliation, cancellation, terminal
validation, structural events, frozen-graph event identity, and canonical
automated runner. O6-R13L independently reread current source, tests, and all
three pages after the correction sequence. The
[final closure review](../reviews/encounter-orchestration-order-6-final-closure-review-2026-07-30.md)
found no unresolved realistic reachable defect and promoted all three entries
to `reviewed` at that revision. The later
[O6-R14 fresh owner-closure audit](../reviews/encounter-orchestration-order-6-fresh-owner-closure-audit-2026-08-04.md)
reproduced four supported paths that conflict with the current audience claims.
O6-R15 through O6-R18 corrected those paths and O6-R19 reconciled all three
pages. O6-R20 then found one actorless ordinary-action event shape; O6-R21
closed it and O6-R22 reconciled this guidance. The
[O6-R23 final closure review](../reviews/encounter-orchestration-order-6-r23-final-closure-review-2026-08-04.md)
independently re-read the corrected source and all three audiences, found no
unresolved realistic reachable defect, and returned the entries to `reviewed`.

The later
[O6-R24 post-R23 independent audit](../reviews/encounter-orchestration-order-6-post-r23-independent-audit-2026-08-04.md)
found that normal completion text can populate fault-only result metadata and
that two audience statements overstate event publication and fault cleanup.
The three encounter entries therefore return to `existing_unreviewed` until
O6-R25 through O6-R27 correct and independently recheck those contracts.
O6-R25 and O6-R26 completed the implementation and audience corrections. The
[O6-R27 final closure review](../reviews/encounter-orchestration-order-6-r27-final-closure-review-2026-08-04.md)
independently traced current source and all audiences, corrected the remaining
completion-policy ambiguity, and restored the three entries to `reviewed`.

The subsequent
[O6-R28 post-R27 independent audit](../reviews/encounter-orchestration-order-6-post-r27-independent-audit-2026-08-04.md)
reproduced a limited-action identity bypass and duplicate
explicit-plus-defeat departure cleanup. It also found that restriction
decision, enactment, and reconciliation ownership needs clearer audience
wording. The three encounter entries therefore return to
`existing_unreviewed` until O6-R29 through O6-R32 correct and independently
recheck those contracts.

O6-R29 and O6-R30 correct the two runtime contracts. O6-R31 reconciles all
three audience pages and their public integration evidence. Their state remains
`existing_unreviewed` until O6-R32 independently rereads the corrected source
and documentation and passes the complete release gate.

The
[O6-R32 final closure review](../reviews/encounter-orchestration-order-6-r32-final-closure-review-2026-08-04.md)
completed that independent source and document trace without finding an
unresolved realistic reachable defect. The three encounter entries return to
`reviewed` at that revision.

The later
[O6-R33 post-R32 independent audit](../reviews/encounter-orchestration-order-6-post-r32-independent-audit-2026-08-05.md)
reproduced two extension-contract defects and one cleanup-boundary wording
ambiguity. The three encounter entries return to `existing_unreviewed` until
O6-R34 through O6-R37 correct and independently recheck those contracts.

O6-R34 and O6-R35 correct no-cost economy authority and scheduler structural
continuity. O6-R36 reconciles the mechanics, developer, technical, and public
integration statements with those changes and the exact structural
`BattleStarted` cleanup boundary. The
[O6-R37 final closure review](../reviews/encounter-orchestration-order-6-r37-final-closure-review-2026-08-05.md)
independently rereads current source and all three audiences without finding an
unresolved realistic reachable defect. The encounter entries return to
`reviewed`.

The subsequent
[O6-R38 post-R37 independent audit](../reviews/encounter-orchestration-order-6-post-r37-independent-audit-2026-08-05.md)
reproduced stable round-robin and scheduler/economy-liveness defects and found
one phase-window safety-limit terminology ambiguity. The three encounter
entries return to `existing_unreviewed` until O6-R39 through O6-R42 correct and
independently reconcile those contracts.

O6-R39 and O6-R40 correct stable team-ring rotation and exhausted-economy
schedule validation. O6-R41 reconciles those contracts and the exact accepted
turn-window meaning of `MaximumCommands` across all three audiences. Their
entries return to `reviewed`. The
[O6-R42 final closure review](../reviews/encounter-orchestration-order-6-r42-final-closure-review-2026-08-05.md)
independently verifies those documents against current source and tests and
formally closes Order 6 without changing the audience totals.

The later
[O6-R43 post-R42 independent audit](../reviews/encounter-orchestration-order-6-post-r42-independent-audit-2026-08-05.md)
found two supported runtime/documentation disagreements in event and fault
authority plus one incorrect developer-guide interface name. All three
encounter entries return to `existing_unreviewed` until O6-R44 through O6-R47
correct and independently recheck the contracts.

O6-R44 and O6-R45 correct canonical event retention and primary command-fault
authority. O6-R46 reconciles all three audiences and the public API guidance.
The
[O6-R47 final closure review](../reviews/encounter-orchestration-order-6-r47-final-closure-review-2026-08-05.md)
independently rechecks current source and documentation without finding an
unresolved realistic reachable defect. The three encounter entries return to
`reviewed`.

The
[O6-R48 independent closure audit](../reviews/encounter-orchestration-order-6-r48-independent-closure-audit-2026-08-07.md)
finds no new encounter runtime defect, but reopens the technical entry because
its command transaction diagram omits the valid cancelled, rejected, and
faulted command-result branches. Mechanics and developer guidance remain
`reviewed` while O6-R50 owns the technical correction.

O6-R50 corrects that diagram directly from the runner's command-status and
consumption branches. It now records validated command events before status
interpretation, sends only `Executed` commands through turn economy, and skips
owner-turn-end lifecycle only for `None` consumption. The technical entry
returns to `reviewed`; O6-R51 remains the bounded closure review.

The
[O6-R51 final closure review](../reviews/encounter-orchestration-order-6-r51-final-closure-review-2026-08-08.md)
independently rechecks automated execution authority and every command-status
transaction branch without finding an unresolved realistic reachable defect.
Order 6 is formally owner-closed; the documentation totals do not change.

At the O6-R51 revision, the documentation matrix recorded 75 audience entries:
30 reviewed, 24 existing_unreviewed, 14 missing, and 7 not_applicable.

| State | Count |
|---|---:|
| `reviewed` | 30 |
| `existing_unreviewed` | 24 |
| `missing` | 14 |
| `not_applicable` | 7 |

These totals describe documentation only. They do not reduce the implementation
state recorded by the framework capability matrix.

## Promotion Rule

An entry becomes `reviewed` only when:

1. current source and tests have been inspected;
2. current behavior has been explained in plain language;
3. discrepancies and assumptions have been presented;
4. the project owner has confirmed the intended rule;
5. all applicable audience documents, diagrams, examples, and evidence agree.

The complete process is defined by the
[Documentation Design Pattern](../documentation-design-pattern.md).
