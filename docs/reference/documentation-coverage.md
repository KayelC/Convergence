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
Other subsystem entries remain unreviewed until they complete the same process.
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

The subsequent 22 July closure-readiness review returned all three
`combat_resolution` audience entries to `existing_unreviewed`. Their confirmed
mechanics remain authoritative, but charge participation and disabled
composition must be reconciled through O2-R35 through O2-R39 before the entries
return to `reviewed`.

The documentation matrix currently records 75 audience entries: 14 reviewed,
37 existing_unreviewed, 17 missing, and 7 not_applicable.

| State | Count |
|---|---:|
| `reviewed` | 14 |
| `existing_unreviewed` | 37 |
| `missing` | 17 |
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
