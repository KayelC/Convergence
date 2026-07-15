# Production-Readiness Completion Roadmap

## Authority

This document is the active completion authority for the findings first recorded
in the 2026-07-14 production-readiness audit. That audit was moved to the legacy
archive before all of its findings were completed. Archiving its terminology did
not complete its work.

The machine-readable source is
[`../tests/Convergence.Framework.Tests/Fixtures/production-readiness-roadmap.json`](../tests/Convergence.Framework.Tests/Fixtures/production-readiness-roadmap.json).
Tests require exact finding coverage and prohibit archive eligibility until a
finding is verified.

Statuses mean:

- `open`: required implementation remains.
- `implemented_pending_review`: the planned commit and gates are complete, but
  the final consolidated review has not accepted the result.
- `verified`: implementation and the final review are complete.

This roadmap remains active while any finding is `open` or
`implemented_pending_review`.

## Baseline

- Branch: `main`
- Starting commit: `8db20fe`
- Framework: .NET 8, C# 12, source-distributed, non-packable
- Tests: 785 passed, 0 failed, 0 skipped
- Framework tests: 624
- DemoHost tests: 161
- Active runtime dependencies: none

## Original Audit Crosswalk

| ID | Finding | Status | Current disposition |
|---|---|---|---|
| PR-B1 | Repository opened as the retired product | `verified` | The Git root contains the clean Convergence solution and product boundary tests. |
| PR-B2 | License conflicted with the intended audience | `verified` | Active software and content have explicit source-available license scopes and contribution rules. |
| PR-B3 | Public and authoring contracts are uncontrolled | `open` | Establish the 0.1 API baseline, XML documentation, schema v3 contracts, and validator CLI. |
| PR-B4 | Godot compatibility is only simulated | `open` | Add and execute a real Godot 4.7.1 .NET sample. |
| PR-B5 | No complete repository release gate | `open` | Extend the existing CI foundation after all other findings land. |
| PR-H1 | Encounter cancellation/publication can split a turn | `verified` | Encounter atomicity, cancellation safe points, port containment, and cleanup have direct regression coverage. |
| PR-H2 | Party/stock contracts hardcode retired roles | `verified` | Active contracts use generic party, Vessel, Hosted Entity, Companion, and roster roles. |
| PR-M1 | Resource addition can escape typed diagnostics | `verified` | Checked arithmetic returns typed rejection without live mutation. |
| PR-M2 | Stat allocation can exceed runtime domains | `verified` | Allocation validates inputs and calculated snapshots before atomic commit. |
| PR-M3 | Navigation/traversal can apply invalid IDs | `verified` | Runtime services reject invalid IDs before policy evaluation or mutation. |
| PR-M4 | Authored rulesets are mostly fixed labels | `open` | Add host-registered typed policy factories and complete supported built-in parameters. |
| PR-M5 | Encounter events leak presentation and omit data | `open` | Replace message-authoritative events with typed payloads. |
| PR-M6 | Aggregate save restoration is host-reimplemented | `open` | Add framework session restoration and a version-migration seam. |
| PR-L1 | Runtime random targeting has an ordered fallback | `implemented_pending_review` | Both targeting policies are mandatory; ordered behavior requires explicit injection. |
| PR-L2 | DemoHost flattens content filenames | `open` | Preserve pack-relative output and resolution paths. |

The later code review added `CR-M6`: catalyst rank shifting currently implements
the wrong rule. It is tracked here because it changes the public fusion and
content contract and must be corrected before the API baseline is frozen.

## Ordered Completion Checkpoints

| Order | IDs | Required result | Commit |
|---|---|---|---|
| 1 | PR-L1 | Explicit skill and runtime-effect random-target policies; no hidden fallback. | `runtime: require explicit random targeting` |
| 2 | CR-M6 | Exact catalyst rank shifting in the target's own race; schema v3 runtime contract. | `fusion: enforce catalyst rank shifts` |
| 3 | PR-M4 | Typed policy-factory registry and documented standard parameter contracts. | `runtime: register authored ruleset policies` |
| 4 | PR-M5 | Immutable typed encounter payloads with optional non-authoritative debug text. | `battle: structure encounter event contracts` |
| 5 | PR-M6 | Validated, dependency-ordered aggregate restoration and migration extension seam. | `runtime: add aggregate session restoration` |
| 6 | PR-L2 | Collision-safe pack-relative content deployment and loading. | `host: preserve content pack paths` |
| 7 | PR-B3 | Supported 0.1 API, XML docs, API baseline, schema v3, and validator tool. | `api: establish convergence 0.1 contract`; `schema: publish content schema v3`; `tool: add content authoring validator` |
| 8 | PR-B4 | Real Godot 4.7.1 source-reference sample and headless smoke run. | `host: add godot 4.7 reference consumer` |
| 9 | PR-B5 | Repeatable release gate covering every supported product boundary. | `quality: complete production release gate` |

Every checkpoint receives focused tests, the full solution gate, strict
nonincremental builds, formatting verification, applicable demos, documentation
checks, and its own green commit. Implemented findings remain
`implemented_pending_review` until the final consolidated review and any
resulting corrections are complete.

## Review Evidence Standard

A release-blocking defect must identify:

1. the intended invariant or public contract;
2. a realistic path through supported content or host APIs;
3. a concrete behavioral, state-integrity, or integration consequence; and
4. reproducible code or test evidence.

Impossible domain values, speculative hardening, and alternative product
designs are recorded separately and are not inflated into vulnerabilities.

## Completion Gate

The roadmap is complete only when every ledger entry is `verified`, the full
release gate is green, and the final source-based review finds no unresolved
release blocker. The roadmap is not archived merely because its implementation
commits exist.
