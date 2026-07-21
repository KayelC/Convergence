# Combat Resolution Order 2 Corrections Roadmap

## Purpose

This roadmap reopens Order 2 after the independent source review of 20 July
2026. It converts each confirmed finding into one narrow, independently green
checkpoint and records the project owner's decision for offensive-item turn
behavior.

The first correction cycle, O2-R0 through O2-R6, is implemented and preserved
below as historical evidence. A fresh source review on 21 July 2026 found three
reachable complete-action defects and one schema mismatch. The project owner
also clarified that secondary effects such as an ailment rider may depend on a
primary hit. Order 2 is therefore reopened under the
[Ordered Secondary Effects Roadmap](ordered-secondary-effects-roadmap.md).

## Authority

- Current source and executable tests define implemented behavior.
- [Combat Resolution Policy Family](../decisions/combat-resolution-policy-family.md)
  defines confirmed mechanics.
- [Order 2 Independent Review](../reviews/combat-resolution-order-2-independent-review-2026-07-20.md)
  supplies correction evidence.
- [Order 2 Fresh Source Review](../reviews/combat-resolution-order-2-fresh-source-review-2026-07-21.md)
  supplies the second-cycle findings.
- [Ordered Secondary Effects Roadmap](ordered-secondary-effects-roadmap.md)
  owns O2-R7 through O2-R16 and the confirmed primary-hit dependency model.
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

**State:** verified

- Preserve the independent review under `docs/reviews`.
- Record the confirmed offensive-item decision in the combat policy decision.
- Add this roadmap and index it.
- Mark the previous Order 2 completion claim as reopened pending corrections.

**Commit:** `docs: plan combat resolution corrections`

### O2-R1: Validate every Framework-supplied random draw

**State:** verified

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

**State:** verified

- Add the neutral `general` charge value to schema v5.
- Reject undefined programmatic charge kinds during semantic validation.
- Prove schema, deserialization, semantic validation, application, complete
  action consumption, save validation, and restoration under
  `UnifiedChargePolicy`.
- Do not alter active content packs merely to demonstrate the optional policy.

**Commit:** `schema: author unified charge state`

### O2-R3: Derive Critical from committed sequential hits

**State:** verified

- Keep ordered evidence for every pre-resolved hit.
- Exclude hits skipped after target defeat from the action Critical flag.
- Preserve Critical for a hit actually processed by the sequential mutation
  loop, including a zero-damage committed hit.
- Add the defeating-first-hit regression and committed-critical control.

**Commit:** `battle: bind critical outcome to committed hits`

### O2-R4: Close public combat vocabulary boundaries

**State:** verified

- Reject undefined damage element, affinity, hit distribution, critical mode,
  charge kind, and resistance values at the earliest owning boundary.
- Make programmatic semantic validation agree with strict JSON behavior.
- Preserve all valid schema-v5 and public request behavior.

**Commit:** `battle: validate combat vocabulary boundaries`

### O2-R5: Make item outcome behavior policy-owned

**State:** verified

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

**State:** verified

- Conduct a fresh source-first review of the corrected paths.
- Update the independent and original completion reviews with exact correction
  evidence rather than erasing their historical claims.
- Reconcile mechanics, developer, technical, content-contract, ruleset, and
  roadmap documents with corrected source.
- Record exact test, build, schema, host, and documentation results.

**Commit:** `docs: verify combat resolution corrections`

## Completion Record

| Checkpoint | Commit | Full-suite result after checkpoint |
|---|---|---:|
| O2-R0 | `4a91a70` | planning authority established |
| O2-R1 | `88d30bc` | 1,308 passed |
| O2-R2 | `c18c18d` | 1,311 passed |
| O2-R3 | `905aaf1` | 1,314 passed |
| O2-R4 | `3d16406` | 1,316 passed |
| O2-R5 | `711ba83` | 1,330 passed |
| O2-R6 | this documentation checkpoint | 1,330 passed |

At that historical checkpoint, the post-correction source trace found no
remaining reachable defect in the reviewed paths. The later 21 July review
expanded the tested combinations and supersedes that closure decision. The
first-cycle final gate reported 1,150 Framework tests, 173
DemoHost tests, and 7 ContentValidator tests, with zero failures or skips.
Framework and complete-solution Release builds reported zero warnings. All
five DemoHost modes passed, the content validator accepted 6 packs, 36
documents, and 98 qualified definitions, Framework coverage measured 90.27%
lines / 75.18% branches, and the real local Godot 4.7.1 headless smoke emitted
`CONVERGENCE_GODOT_SMOKE_OK`.

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

The first correction cycle established the following requirements:

- every supplied random draw uses the shared validated boundary;
- unified charge is authorable and restorable from clean content;
- skipped post-defeat hits cannot change turn economy;
- undefined combat vocabulary is rejected consistently;
- item turn behavior is selected by policy with normal cost as the supplied
  default;
- all quality gates pass; and
- a fresh review finds no remaining reachable correctness defect in these
  paths.

Order 2 now additionally requires every O2-R7 through O2-R16 completion
condition in the Ordered Secondary Effects Roadmap. Until then, it remains
reopened rather than complete.
