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
between opportunity counting and actor scheduling. Other subsystem entries
remain unreviewed until they complete the same process.
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
Order 4 completed its first source-first review and correction sequence, and a
later independent audit reopened all three audience entries. O4-R12 through
O4-R16 corrected typed removal and expiry evidence, request-aware passive
extension validation, exact Instant scope, reserve-owner battle-start policy,
and audience guidance. The
[O4-R17 post-correction review](../reviews/status-passive-lifecycle-order-4-post-correction-review-2026-07-24.md)
then re-read the corrected source and prose and returned all three entries to
`reviewed`. Order 5, `battle_knowledge`, is next.
Their review order and promotion gates are maintained in the active
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

The documentation matrix currently records 75 audience entries: 24 reviewed,
28 existing_unreviewed, 16 missing, and 7 not_applicable.

| State | Count |
|---|---:|
| `reviewed` | 24 |
| `existing_unreviewed` | 28 |
| `missing` | 16 |
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
