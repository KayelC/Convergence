# Convergence Production-Readiness Consolidated Review

Date: 2026-07-16

Branch: `main`

Reviewed range: `8db20fed67f1c3b520e143fae5b947c578a0d274..4c07ac490b83e84fe387943cda6a9b8c7a7c4580`

## Mandate

This review closes the active
[Production-Readiness Completion Roadmap](production-readiness-roadmap.md).
It was performed from the current source, tests, content, schemas, projects,
and automation rather than from earlier review conclusions.

The review covered:

- every Framework source changed by checkpoints 1 through 9;
- DemoHost content loading and every functional demo composition path;
- the real Godot 4.7.1 source-reference consumer;
- all active schema-v3 documents and the authoring validator;
- public API baselines, assembly metadata, and XML documentation;
- aggregate save restoration and the host-owned save examples;
- CI, coverage, trimming, dependency auditing, documentation, and forbidden
  reference gates.

The historical archive was excluded because it is neither built nor referenced
by the active product.

## Review Method

For each checkpoint, the review traced the supported request from its public
entry point through validation, mutation, immutable result construction, host
integration, and focused tests. Cross-cutting searches checked exception
boundaries, hidden randomness and time, unfinished production paths, framework
dependencies, active archive references, and presentation-only inference.

A finding was treated as a product defect only when it had an intended
invariant, a reachable supported path, a concrete consequence, and reproducible
evidence. Optional hardening and alternative product choices are listed
separately.

## Finding

### Corrected: encounter resource events did not describe exact mutations

Severity: Medium

The typed encounter-event checkpoint originally mapped a generic positive
effect `Value` into `BattleResourceChangedEventPayload.Delta`. That made damage
look like a positive resource change, could attribute reflected damage to the
wrong actor, treated non-resource effects such as stat-stage changes as resource
mutations, and omitted committed skill costs. A Godot or other event-driven host
could therefore animate or mirror state incorrectly despite the underlying
battle state being correct.

Commit `4c07ac4` corrected the contract by introducing immutable signed
`ExecutionResourceChange` records. Built-in effects now report their exact
committed actor, resource, and delta; skill costs are reported separately; and
encounter, lifecycle, automated-battle, and Training Annex adapters publish
events from those records rather than inferred display values. Regressions cover
costs, normal damage, reflection, absorption, restoration, reduction, set,
revival, non-resource effects, ordering, invalid identities, and collection
immutability.

No other reachable correctness, state-integrity, host-neutrality, or release-gate
defect was found in the reviewed range.

## Checkpoint Verdicts

| Checkpoint | Source-based verdict |
|---|---|
| Explicit random targeting | Verified. Skill and runtime-effect random targeting both require injected policies; deterministic ordering is available only through explicit composition. |
| Catalyst rank shifting | Verified. Authored catalyst and target roles select the operand; exact same-race rank movement rejects missing destinations and stale participant data without clamping. |
| Ruleset policy factories | Verified. Hosts supply category-specific factories; all combat parameters, roster tiers, and Action Token liveness values are validated; fixed supplied policies remain replaceable. |
| Structured encounter events | Verified after `4c07ac4`. Every event kind has a typed immutable payload, debug text is non-authoritative, and resource events now carry exact signed mutations. |
| Aggregate session restoration | Verified. Migration is explicit, validation precedes construction, actor profiles resolve before mutation, Hosted Entity dependencies are ordered, cycles reject, and no partial session is returned. |
| Pack-relative content paths | Verified. Documents resolve relative to their manifest, remain confined to the configured content root, and identical basenames in separate packs do not collide. |
| Public API contract | Verified. The `0.1.0` assembly/API baseline, XML documentation, analyzers, .NET 8 target, non-packable source distribution, and zero runtime dependencies are enforced. |
| Content schema v3 | Verified. Fourteen strict Draft 2020-12 artifacts cover all implemented document families, while graph and registration rules remain authoritative in Framework validation. |
| Content authoring validator | Verified. The CLI performs schema, ownership, path, strict-deserialization, semantic, dependency, registration, and final catalog checks without adding filesystem APIs to Framework. |
| Godot 4.7.1 consumer | Verified. A real Godot project loads `res://` content, maps runtime IDs to Nodes, exchanges commands and ordered events, executes action and encounter paths, and round-trips host-owned save data. |
| Repository release gate | Verified. Locked audited restore, formatting, warning-free builds, API/schema/content gates, tests, coverage, demos, Godot smoke, trimming, documentation, and security checks are mandatory. |

## Residual Product Constraints

These are declared `0.1` boundaries, not defects found by the review:

- Convergence is a pre-release, source-distributed, non-packable .NET 8
  framework. The API baseline guards accidental changes but is not a `1.0`
  compatibility promise.
- Save migration is an extension seam. No fictional migration exists for
  unreleased save versions.
- `Convergence.GodotHost` is a focused integration proof, not a complete game or
  a production save-file specification. Godot projects own scenes, UI,
  scheduling, assets, and serialization.
- DemoHost is reference software. Its output is not a framework presentation
  contract.
- New gameplay breadth and richer original examples remain roadmap work only
  when they prove a reusable framework rule or a real host integration need.

## Verification

The post-correction release gate completed with:

- 930 tests passed: 760 Framework, 163 DemoHost, and 7 content-validator;
- 0 failed and 0 skipped tests;
- strict .NET 8 Release builds with 0 warnings and 0 errors;
- Framework coverage measured 91.17% lines and 75.22% branches, above the
  enforced 90% and 70% thresholds;
- 6 packs, 36 documents, and 94 definitions accepted by schema and catalog
  validation;
- all four noninteractive DemoHost modes and scripted Training Annex play
  successful;
- official Godot 4.7.1 .NET headless smoke successful;
- locked vulnerability audit, format, API, documentation-link,
  forbidden-reference, trimming, and `git diff --check` gates successful.

## Final Verdict

Every finding in the active production-readiness ledger is implemented and
verified. No unresolved release blocker remains in the reviewed product
boundary. Convergence is ready to be treated as a guarded `0.1.0` pre-release
framework and to continue feature development from the normal
[Product Roadmap](roadmap.md).
