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
| PR-B3 | Public and authoring contracts are uncontrolled | `implemented_pending_review` | The 0.1 API, schema-v3 contracts, and complete authoring validator CLI are guarded; final review remains. |
| PR-B4 | Godot compatibility is only simulated | `implemented_pending_review` | The real Godot 4.7.1 .NET sample executes content, commands, actions, encounters, Nodes, events, and host-owned saves; final review remains. |
| PR-B5 | No complete repository release gate | `open` | Extend the existing CI foundation after all other findings land. |
| PR-H1 | Encounter cancellation/publication can split a turn | `verified` | Encounter atomicity, cancellation safe points, port containment, and cleanup have direct regression coverage. |
| PR-H2 | Party/stock contracts hardcode retired roles | `verified` | Active contracts use generic party, Vessel, Hosted Entity, Companion, and roster roles. |
| PR-M1 | Resource addition can escape typed diagnostics | `verified` | Checked arithmetic returns typed rejection without live mutation. |
| PR-M2 | Stat allocation can exceed runtime domains | `verified` | Allocation validates inputs and calculated snapshots before atomic commit. |
| PR-M3 | Navigation/traversal can apply invalid IDs | `verified` | Runtime services reject invalid IDs before policy evaluation or mutation. |
| PR-M4 | Authored rulesets are mostly fixed labels | `implemented_pending_review` | Hosts supply a typed factory registry; standard damage exposes all combat settings; roster tiers and Action Token liveness are authored; fixed supplied policies remain replaceable. |
| PR-M5 | Encounter events leak presentation and omit data | `implemented_pending_review` | Every event kind carries an immutable typed payload; optional debug text is never the sole data source. |
| PR-M6 | Aggregate save restoration is host-reimplemented | `implemented_pending_review` | Framework now owns validated, dependency-ordered, all-or-nothing session restoration and an explicit migration-step seam. |
| PR-L1 | Runtime random targeting has an ordered fallback | `implemented_pending_review` | Both targeting policies are mandatory; ordered behavior requires explicit injection. |
| PR-L2 | DemoHost flattens content filenames | `implemented_pending_review` | Build output preserves pack directories; documents resolve relative to their manifest under the confined content root. |
| CR-M6 | Catalyst rank shifting implements the wrong rule | `implemented_pending_review` | Schema v3 uses authored catalyst/target roles, exact same-race rank lookup, and typed rejection without clamping. |

The later code review added `CR-M6`: catalyst rank shifting implemented the
wrong rule. Its implementation is complete and awaits the same consolidated
review as the original audit findings.

## Ordered Completion Checkpoints

| Order | IDs | Required result | Commit |
|---|---|---|---|
| 1 | PR-L1 | Explicit skill and runtime-effect random-target policies; no hidden fallback. | `runtime: require explicit random targeting` |
| 2 | CR-M6 | Exact catalyst rank shifting in the target's own race; schema v3 runtime contract. | `fusion: enforce catalyst rank shifts` |
| 3 | PR-M4 | Typed policy-factory registry and documented standard parameter contracts. | `runtime: register authored ruleset policies` |
| 4 | PR-M5 | Immutable typed encounter payloads with optional non-authoritative debug text. | `battle: structure encounter event contracts` |
| 5 | PR-M6 | Validated, dependency-ordered aggregate restoration and migration extension seam. | `runtime: add aggregate session restoration` |
| 6 | PR-L2 | Collision-safe pack-relative content deployment and loading. | `host: preserve content pack paths` |
| 7 | PR-B3 API | Supported 0.1 API, XML documentation, and textual compatibility baseline. | `api: establish convergence 0.1 contract` |
| 8 | PR-B3 Schema | Strict Draft 2020-12 schemas for every implemented content family. | `schema: publish content schema v3` |
| 9 | PR-B3 Tooling | Host-side content authoring validator CLI. | `tool: add content authoring validator` |
| 10 | PR-B4 | Real Godot 4.7.1 source-reference sample and headless smoke run. | `host: add godot 4.7 reference consumer` |
| 11 | PR-B5 | Repeatable release gate covering every supported product boundary. | `quality: complete production release gate` |

Every checkpoint receives focused tests, the full solution gate, strict
nonincremental builds, formatting verification, applicable demos, documentation
checks, and its own green commit. Implemented findings remain
`implemented_pending_review` until the final consolidated review and any
resulting corrections are complete.

## Checkpoint Evidence

| Checkpoint | Commit/result | Verification |
|---|---|---|
| 1 | `44960bc` (`runtime: require explicit random targeting`) | 789 tests passed; 0 skipped; strict build produced 0 warnings; all noninteractive demos passed. |
| 2 | `d8047e7` (`fusion: enforce catalyst rank shifts`) | 794 tests passed; 0 skipped; schema v3 active packs at `0.3.0`; rank up/down, parent order, stale rank, both boundaries, ambiguity, and retired-shape rejection covered. |
| 3 | `4d2f8db` (`runtime: register authored ruleset policies`) | 799 tests passed; 0 skipped; all seven typed factory categories, 28 combat settings, authored roster tiers, authored Action Token liveness, custom replacement, and absent standard moon-phase composition covered. |
| 4 | `battle: structure encounter event contracts` | 800 tests passed; 0 skipped; all event kinds are payload-checked, structural collections are immutable, turn-economy changes include before/after state and consumption, and final outcomes/faults are typed. |
| 5 | `runtime: add aggregate session restoration` | 805 tests passed; 0 skipped; current-version restore, missing migration paths, explicit migration steps, Hosted Entity ordering, missing dependencies, dependency cycles, immutable aggregate views, and actor failure atomicity covered. |
| 6 | `host: preserve content pack paths` | 807 tests passed; 0 skipped; all active content retains its pack-relative output path, document lookup is manifest-relative and root-confined, and identical document basenames in separate packs remain isolated. |
| 7 | `api: establish convergence 0.1 contract` | Assembly/API version `0.1.0`; 9,531 shipped API signatures guarded by PublicApiAnalyzers 5.6.0; XML documentation emitted for supported composition entry points; implementation namespaces export no public types. |
| 8 | `schema: publish content schema v3` | 14 Draft 2020-12 schema artifacts cover 13 document families plus shared definitions; all 36 active documents declare the matching URN and pass independent JsonSchema.Net 9.2.2 evaluation; 106 focused contract cases cover valid unions and structural rejection; 914 full-suite tests passed with 0 skipped and 0 build warnings. |
| 9 | `tool: add content authoring validator` | The .NET 8 host-side CLI validates schema, manifest ownership, root confinement, strict deserialization, semantic registrations, dependencies, and catalog construction; active content resolves to 6 packs, 36 documents, and 94 definitions; 921 full-suite tests passed with 0 skipped and 0 build warnings. |
| 10 | `host: add godot 4.7 reference consumer` | `Godot.NET.Sdk` 4.7.1 builds with zero warnings; the official 4.7.1 .NET engine headlessly loads one `res://` pack, maps two runtime actors to Nodes, executes a typed skill, consumes 18 ordered encounter events, restores a two-actor host-owned save, and exits 0; 925 full-suite tests passed with 0 skipped and 0 build warnings. |

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
