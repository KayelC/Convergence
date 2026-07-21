# Order 2 Closure Corrections Roadmap

**Status:** active; O2-R17 complete, O2-R18 through O2-R21 pending

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

**State:** pending

- Add the absolute content ceiling and configurable standard-policy maximum.
- Advance schemas and active content to version 6 / pack version `0.6.0`.
- Enforce the absolute limit in schema and semantic validation.
- Enforce the selected standard-policy limit before random selection or
  allocation.
- Add schema, deserialization, semantic, binding, direct-policy, action
  rollback, active-pack, and API-baseline tests.

**Commit:** `battle: bound authored hit counts`

### O2-R19: Unify authored-percentage boundaries

**State:** pending

- Add one internal authored-percentage validator.
- Validate public supplied-policy and lifecycle request boundaries.
- Validate ailment, escape, instant-defeat, damage/critical, and recursively
  nested chance-condition values during skill, item, and direct effect-action
  assessment.
- Return stable typed diagnostics from action assessment.
- Prove invalid values consume no randomness, resources, inventory, charges,
  actor state, or turn economy.

**Commit:** `execution: reject invalid authored percentages`

### O2-R20: Reconcile active documentation

**State:** pending

- Update mechanics, developer, technical, ruleset, content-contract, API, and
  roadmap documentation from corrected source.
- Explain the standard `64` limit, absolute `1024` ceiling, schema-v6 break, and
  `0..100` authored/derived distinction.
- Promote the capability matrix only after focused and complete gates pass.

**Commit:** `docs: reconcile combat safety contracts`

### O2-R21: Independent closure re-evaluation

**State:** pending

- Re-read corrected source rather than relying on this roadmap or earlier
  reports.
- Reproduce both former paths and verify their rejection boundaries.
- Run the full release gate, active content validation, DemoHost modes, scripted
  play, Godot contract/headless smoke where available, API checks, documentation
  links, formatting, forbidden references, and `git diff --check`.
- Record exact evidence and close Order 2 only if no realistic reachable defect
  remains in this correction scope.

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
