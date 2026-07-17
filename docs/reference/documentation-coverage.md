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
action/effect documentation completed the collaborative workflow after their
owner decisions were confirmed, implemented, inspected in current source, and
exercised by tests and DemoHost. Other subsystem entries remain unreviewed until
they complete the same process.
Their review order and promotion gates are maintained in the active
[Documentation Completion Roadmap](../roadmap/documentation-completion-roadmap.md).

The documentation matrix currently records 75 audience entries: 14 reviewed,
35 existing_unreviewed, 19 missing, and 7 not_applicable.

| State | Count |
|---|---:|
| `reviewed` | 14 |
| `existing_unreviewed` | 35 |
| `missing` | 19 |
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
