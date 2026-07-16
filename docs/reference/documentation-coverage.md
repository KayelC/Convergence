# Documentation Coverage

## Purpose

Framework implementation maturity and documentation maturity are different.
Convergence may have a complete, tested capability whose intended rules have not
yet been reviewed collaboratively with the project owner.

The executable documentation ledger is
[`../../tests/Convergence.Framework.Tests/Fixtures/documentation-coverage-matrix.json`](../../tests/Convergence.Framework.Tests/Fixtures/documentation-coverage-matrix.json).
It covers the same 25 capability IDs as the
[Framework Capability Matrix](../roadmap/framework-capability-matrix.md).

## Baseline

This foundation deliberately marks no capability audience as `reviewed`.
Existing pages are retained as `existing_unreviewed` until source inspection,
plain-language discussion, diagram review, and explicit owner confirmation are
complete.

Across 25 capabilities and three audiences, the initial 75 coverage entries are:

The documentation matrix currently records 75 audience entries: 0 reviewed,
44 existing_unreviewed, 24 missing, and 7 not_applicable.

| State | Count |
|---|---:|
| `reviewed` | 0 |
| `existing_unreviewed` | 44 |
| `missing` | 24 |
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
