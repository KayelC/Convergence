# Order 2 Closure Corrections Roadmap

**Status:** complete; O2-R17 through O2-R23 verified

**Started:** 21 July 2026

**Capability:** `combat_resolution`

## Purpose

The source-first
[Order 2 Closure Review](../reviews/combat-resolution-order-2-closure-source-review-2026-07-21.md)
found two reachable reliability defects after the ordered-secondary-effect
cycle. This roadmap keeps those findings active until implementation,
documentation, and an independent source recheck are complete.

This is a narrow correction cycle. It does not redesign damage, Action Token,
charges, affinities, criticals, ordered effects, ailments, or host presentation.

## Confirmed Findings

### O2-H2: authored hit counts have no execution bound

Schema-v5 content and Framework semantic validation accept every positive
`int` hit count. The supplied damage policy then uses the resolved count as a
`List<T>` capacity and loop bound. A content typo can therefore request
unbounded memory and work through the canonical catalog path.

### O2-M4: code-authored percentages do not share catalog validation semantics

JSON Schema and semantic validation reject chances outside `0..100`, but public
code-authored chance conditions, ailment effects, escape effects, lifecycle
requests, and supplied policy requests can silently clamp or short-circuit the
same invalid values.

## Decisions

### Bounded multi-hit execution

- The supplied standard damage policy exposes
  `maximumHitsPerDamageEffect`, defaulting to `64`.
- The published content contract has an absolute safety ceiling of `1024` hits
  per damage effect. This leaves substantial room for unusual games while
  preventing one content record from requesting effectively unbounded work.
- Standard ruleset configuration must remain within `1..1024`.
- The standard runtime policy rejects a hit range whose authored maximum exceeds
  its selected limit before random selection, allocation, hit resolution, or
  actor mutation.
- A custom damage implementation remains replaceable through
  `IDamageExecutionPolicy`, but content loaded through the published JSON
  contract remains subject to the absolute authoring ceiling.

Adding a maximum changes which previously valid documents are accepted.
Therefore the clean content contract advances to schema version `6`, active
packs advance to `0.6.0`, and exact pack dependencies advance together. Schema
v5 becomes unsupported; no automatic conversion is introduced before the first
stable release.

### Authored probability validation

- Authored percentages are inclusive `0..100` values.
- Invalid base values are rejected before resistance or modifier math.
- Clamping remains valid only for a derived chance produced from a valid base
  value and explicit policy multipliers.
- Skill, item, equipment basic-attack, and escape assessment return typed
  diagnostics for invalid probability values before mutation or turn use.
- Direct supplied-policy and lifecycle requests reject invalid values with clear
  argument errors rather than reinterpreting them.
- Zero and one hundred retain their deterministic no-random-draw behavior.

## Ordered Checkpoints

### O2-R17: Reopen the closure authority

**State:** complete in this documentation checkpoint

- Preserve the new source review and its reproducible evidence.
- Record the correction decisions and ordered implementation sequence.
- Reopen `combat_resolution` as `partial` in the executable capability matrix.
- Keep the earlier closure reviews as historical evidence rather than rewriting
  their revision-specific conclusions.

**Commit:** `docs: reopen combat resolution closure gate`

### O2-R18: Bound authored and standard-policy hit counts

**State:** complete

- Add the absolute content ceiling and configurable standard-policy maximum.
- Advance schemas and active content to version 6 / pack version `0.6.0`.
- Enforce the absolute limit in schema and semantic validation.
- Enforce the selected standard-policy limit before random selection or
  allocation.
- Add schema, deserialization, semantic, binding, direct-policy, action
  rollback, active-pack, and API-baseline tests.

Verification completed with 1,414 solution tests, zero failures or skips, a
zero-warning strict Release build, formatting verification, validation of all
6 active packs / 36 documents / 98 definitions, and all four noninteractive
DemoHost modes.

**Commit:** `battle: bound authored hit counts`

### O2-R19: Unify authored-percentage boundaries

**State:** complete

- Add one internal authored-percentage validator.
- Validate public supplied-policy and lifecycle request boundaries.
- Validate ailment, escape, instant-defeat, damage/critical, and recursively
  nested chance-condition values during skill, item, and direct effect-action
  assessment.
- Return stable typed diagnostics from action assessment.
- Prove invalid values consume no randomness, resources, inventory, charges,
  actor state, or turn economy.

Implementation now uses one internal inclusive `0..100` domain guard. Skill,
item, equipment basic-attack, escape, and recursively nested condition
assessment return the appended `AuthoredPercentageOutOfRange` diagnostic before
target selection, cost resolution, inventory reservation, or turn use. Supplied
damage, critical, instant-defeat, ailment, chance, turn-restriction, and natural
recovery paths reject malformed code-authored values; canonical request records
also preserve that invariant through `with` cloning. Zero and one hundred do not
draw randomness.

Verification completed with 1,437 solution tests, zero failures or skips, a
zero-warning strict Release build, formatting verification, validation of all 6
active packs / 36 documents / 98 definitions, and all four noninteractive
DemoHost modes.

**Commit:** `execution: reject invalid authored percentages`

### O2-R20: Reconcile active documentation

**State:** complete

- Update mechanics, developer, technical, ruleset, content-contract, API, and
  roadmap documentation from corrected source.
- Explain the standard `64` limit, absolute `1024` ceiling, schema-v6 break, and
  `0..100` authored/derived distinction.
- Promote the capability matrix only after focused and complete gates pass.

Mechanics, developer, technical, ruleset, content-contract, API, decision,
quality-gate, and roadmap documents now describe schema v6, the absolute
`1..1024` authoring range, the supplied standard ceiling of `64`, and the
authored-versus-derived percentage boundary. The executable capability matrix
returns `combat_resolution` to `complete` with no hidden implementation gap;
O2-R21 was scheduled as an independent source verification rather than an
unfinished runtime feature. That verification subsequently found the isolated
schema discrepancy corrected by O2-R22.

**Commit:** `docs: reconcile combat safety contracts`

### O2-R21: Independent closure re-evaluation

**State:** complete; one isolated schema-layer discrepancy found

- Re-read corrected source rather than relying on this roadmap or earlier
  reports.
- Reproduce both former paths and verify their rejection boundaries.
- Compare every semantic authored-percentage field with its independent schema
  declaration.

The source trace reproduced the corrected runtime paths and found no remaining
runtime mutation or turn-economy defect. It did find that schema v6 left
`resourcePercentageCondition.value` unconstrained even though semantic
validation and runtime assessment already enforced `0..100`. Malformed content
could not enter a catalog, but schema-only authoring tools did not report the
same error. Closure therefore moved to O2-R22 and O2-R23.

**Commit:** included with `schema: bound resource percentage conditions`

### O2-R22: Align resource-percentage conditions with schema v6

**State:** complete

- Add the missing inclusive `0..100` JSON Schema range.
- Prove both lower and upper violations fail independent Draft 2020-12
  validation.
- Preserve the already-correct semantic, programmatic-assessment, rollback,
  and runtime behavior.

**Commit:** `schema: bound resource percentage conditions`

### O2-R23: Final independent closure re-evaluation

**State:** complete

- Re-read the corrected source and schema without treating earlier reports as
  authority.
- Reproduce the former hit-count, code-authored percentage, and schema-only
  percentage paths.
- Run the full release gate, active content validation, DemoHost modes, scripted
  play, Godot contract/headless smoke where available, API checks, documentation
  links, formatting, forbidden references, and `git diff --check`.
- Record exact evidence and close Order 2 only if no realistic reachable defect
  remains in this correction scope.

The post-O2-R22 recheck started again from the current implementation and
schema. It traced policy binding, hit/critical/instant-defeat resolution,
ordered execution, lifecycle probabilities, charge consumption, complete-action
aggregation, Action Token application, semantic validation, and every v6
percentage declaration. It reproduced the former hit-count and percentage
paths and found no further realistic reachable defect.

The final gate passed 552 focused tests and 1,439 solution tests with zero
failures or skips, a zero-warning strict Release build, formatting, all active
content, all DemoHost modes, scripted play, Framework coverage thresholds,
boundary checks, and the Godot 4.7.1 headless smoke. The local Godot invocation
used ignored workspace paths for its user-data directories because the Codex
process environment could not supply the engine's normal user-data path. Exact
evidence and review limits are recorded in the
[Final Closure Review](../reviews/combat-resolution-order-2-final-closure-review-2026-07-21.md).

**Commit:** `docs: verify combat resolution closure corrections`

## Quality Gate Per Checkpoint

Every source checkpoint requires focused tests, the complete Release suite with
zero skips, strict nonincremental builds with zero warnings, formatting
verification, relevant schema/content validation, and `git diff --check` before
its isolated commit.

## Definition Of Corrected

This cycle is complete only when:

- no schema-valid active document can exceed the absolute hit ceiling;
- the standard policy rejects its selected maximum before random or allocation;
- every authored percentage entry path agrees on `0..100`;
- derived policy chances may still clamp after valid authored input;
- rejected actions preserve all live state and turn economy;
- active schema, content, API, and all three documentation audiences agree; and
- a fresh source re-evaluation finds no remaining reachable defect in these
  paths.
