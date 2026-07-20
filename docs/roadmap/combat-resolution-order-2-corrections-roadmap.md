# Combat Resolution Order 2 Corrections Roadmap

## Purpose

This roadmap reopens Order 2 after the independent source review of 20 July
2026. It converts each confirmed finding into one narrow, independently green
checkpoint and records the project owner's decision for offensive-item turn
behavior.

The current implementation remains usable, but Order 2 is not considered
verified again until every checkpoint and the final source-first review pass.

## Authority

- Current source and executable tests define implemented behavior.
- [Combat Resolution Policy Family](../decisions/combat-resolution-policy-family.md)
  defines confirmed mechanics.
- [Order 2 Independent Review](../reviews/combat-resolution-order-2-independent-review-2026-07-20.md)
  supplies correction evidence.
- This roadmap owns correction order and completion tracking only.

## Confirmed Item Decision

Offensive item turn behavior is policy-owned.

The action-outcome policy receives the action source kind as well as immutable
effect results. The supplied standard policy uses this default:

- Skill and basic-attack damage outcomes are effect-driven.
- Item actions spend one normal turn, even when a typed item effect resolves
  Weak, Critical, Miss, Null, Repel, or Absorb.
- A developer may select or implement an outcome policy that makes offensive
  items effect-driven.

This prevents the current Action Token rules from becoming a hidden assumption
for a future bonus-action or other turn economy. The effect result remains
truthful for presentation and custom policy use even when the supplied item
turn cost is fixed.

## Checkpoints

### O2-R0: Correction authority and owner decision

**State:** in progress

- Preserve the independent review under `docs/reviews`.
- Record the confirmed offensive-item decision in the combat policy decision.
- Add this roadmap and index it.
- Mark the previous Order 2 completion claim as reopened pending corrections.

**Commit:** `docs: plan combat resolution corrections`

### O2-R1: Validate every Framework-supplied random draw

**State:** pending

- Add one internal host-random contract helper for unit and half-open integer
  draws.
- Route every Framework-owned `IRandomSource` call through that helper.
- Preserve deterministic draw order and zero/one-hundred short circuits.
- Add a source-boundary regression preventing future raw random calls outside
  the helper and interface declaration.
- Add focused failures for negotiation, lifecycle, progression, and fusion so
  invalid host output cannot index, mutate, or choose an outcome silently.

**Commit:** `runtime: enforce host random boundaries`

### O2-R2: Complete unified-charge authoring

**State:** pending

- Add the neutral `general` charge value to schema v5.
- Reject undefined programmatic charge kinds during semantic validation.
- Prove schema, deserialization, semantic validation, application, complete
  action consumption, save validation, and restoration under
  `UnifiedChargePolicy`.
- Do not alter active content packs merely to demonstrate the optional policy.

**Commit:** `schema: author unified charge state`

### O2-R3: Derive Critical from committed sequential hits

**State:** pending

- Keep ordered evidence for every pre-resolved hit.
- Exclude hits skipped after target defeat from the action Critical flag.
- Preserve Critical for a hit actually processed by the sequential mutation
  loop, including a zero-damage committed hit.
- Add the defeating-first-hit regression and committed-critical control.

**Commit:** `battle: bind critical outcome to committed hits`

### O2-R4: Close public combat vocabulary boundaries

**State:** pending

- Reject undefined damage element, affinity, hit distribution, critical mode,
  charge kind, and resistance values at the earliest owning boundary.
- Make programmatic semantic validation agree with strict JSON behavior.
- Preserve all valid schema-v5 and public request behavior.

**Commit:** `battle: validate combat vocabulary boundaries`

### O2-R5: Make item outcome behavior policy-owned

**State:** pending

- Add an immutable action-outcome request carrying source kind and effects.
- Preserve the existing aggregation method as a compatibility path for current
  custom implementations.
- Route skills, basic attacks, and items through the source-aware policy path.
- Configure the supplied standard policy to use normal item cost by default.
- Expose an explicit effect-driven item option through standard policy
  configuration and authored combat-ruleset binding.
- Resolve item outcome before reservation and actor-state commit so policy
  failure remains atomic.
- Add default, effect-driven, binding, interruption, rollback, and custom-policy
  tests.

**Commit:** `battle: make item outcomes policy controlled`

### O2-R6: Verification and documentation reconciliation

**State:** pending

- Conduct a fresh source-first review of the corrected paths.
- Update the independent and original completion reviews with exact correction
  evidence rather than erasing their historical claims.
- Reconcile mechanics, developer, technical, content-contract, ruleset, and
  roadmap documents with corrected source.
- Record exact test, build, schema, host, and documentation results.

**Commit:** `docs: verify combat resolution corrections`

## Quality Gate Per Checkpoint

Each source checkpoint requires:

1. focused success, rejection, immutability, and rollback tests;
2. the complete Release test suite with zero skips;
3. strict nonincremental Release build with zero warnings;
4. `dotnet format --verify-no-changes`;
5. relevant schema/content or DemoHost checks;
6. `git diff --check`; and
7. one narrow commit before the next checkpoint begins.

The final checkpoint additionally runs all DemoHost modes, the Godot contract
and headless smoke checks where locally available, active-content validation,
documentation links, API compatibility, and forbidden-reference searches.

## Contract Impact

- Schema remains version 5 because adding `general` is a backward-compatible
  completion of an existing enum contract.
- Save contract remains version 11 because retained charge state already
  represents `ChargeKind.General` and policy identity.
- Source-aware action-outcome contracts are additive. Existing custom
  aggregation implementations continue compiling through the old method.
- No active content pack needs a version bump.
- No host presentation or legacy archive work is introduced.

## Definition Of Corrected

Order 2 is corrected only when:

- every supplied random draw uses the shared validated boundary;
- unified charge is authorable and restorable from clean content;
- skipped post-defeat hits cannot change turn economy;
- undefined combat vocabulary is rejected consistently;
- item turn behavior is selected by policy with normal cost as the supplied
  default;
- all quality gates pass; and
- a fresh review finds no remaining reachable correctness defect in these
  paths.
