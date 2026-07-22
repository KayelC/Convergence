# Order 2 Charge Closure Corrections Roadmap

**Status:** active; O2-R35 through O2-R38 complete, O2-R39 pending

**Started:** 22 July 2026

**Capability:** `combat_resolution`

## Purpose

The fresh
[Order 2 Closure-Readiness Review](../reviews/combat-resolution-order-2-closure-readiness-review-2026-07-22.md)
reconstructed combat resolution from current source at `7e98726`. It found one
reachable charge-ordering defect and one lower-severity mismatch between the
modular product promise and the required combat composition surface.

This roadmap keeps both findings visible until each correction has an isolated
green commit, all affected audience documents agree with the public contracts,
and a new source-first review verifies the resulting implementation. It does
not redesign damage, hit/evasion, Critical, affinity, instant defeat, ordered
effects, item outcomes, or Action Token behavior.

## Confirmed Correction Direction

Charge consumption follows participation, not damage category alone. A retained
charge participates when the selected charge policy supplies the modifier for a
damage attempt. Hit, miss, Null, Repel, and Absorb consume that participating
charge. A charge granted after an uncharged damage attempt remains available for
a later action.

Charge gameplay is genuinely optional. Convergence will supply a disabled
charge policy and let the standard authored combat composition select `split`,
`unified`, or `disabled`. The supplied default remains `split`; games that do
not want charge mechanics select `disabled` instead of carrying a hidden
workaround or writing a private no-op implementation.

## Ordered Checkpoints

### O2-R35: Preserve correction authority and reopen closure

**State:** complete

- Preserve the source-based review as evidence.
- Record the intended invariant, correction boundaries, test requirements, and
  commit order in this active roadmap.
- Reopen `combat_resolution` as `partial` while either finding remains.
- Return its mechanics, developer, and technical documentation entries to
  `existing_unreviewed` until source and prose are reconciled.
- Keep historical reviews and completed checkpoint records unchanged as evidence
  about their reviewed revisions.

**Commit:** `docs: reopen order 2 charge closure`

### O2-R36: Consume only participating charge state

**State:** complete

- Replace damage-element-based finalization with explicit participating charge
  kinds captured when the damage modifier is resolved.
- Preserve whole-action use: one retained slot may boost every matching hit,
  target, and damage component before it is removed once.
- Preserve consumption on miss, Null, Repel, and Absorb.
- Preserve no consumption on skipped, rejected, cancelled, or rolled-back
  execution.
- Do not remove a charge first granted after an uncharged damage attempt.
- Preserve staged transaction behavior for nested passive execution.
- Update the public charge-policy contract and API baseline deliberately.

Required regressions:

- damage then charge grant retains the new charge;
- grant then damage applies and consumes the new charge;
- a nested after-damage grant remains after the triggering attack;
- existing split and unified multi-hit/multi-target behavior remains unchanged;
- hit, miss, Null, Repel, and Absorb consume a participating charge; and
- rejection and rollback publish no charge mutation.

**Commit:** `execution: consume participating charge state`

**Result:** Damage execution publishes the actual `ChargeDamageModifier`
receipt that participated. The outer action submits those receipts once, and
the supplied policy base removes only the same retained runtime charge. Tests
cover damage/grant order, same-kind replacement, nested defeat-prevention
grants, multi-target use, and defensive outcomes.

### O2-R37: Supply optional disabled charge composition

**State:** complete

- Add `DisabledChargePolicy` with an explicit stable policy ID.
- Make charge application a typed unsupported rejection under that policy.
- Resolve every damage modifier as neutral and complete every action without
  retained charge mutation.
- Validate empty disabled state and reject incompatible retained charge state.
- Register split, unified, and disabled policies in the supplied registry.
- Add standard combat-ruleset parameter `chargePolicy` with accepted values
  `split`, `unified`, and `disabled`; preserve `split` as the default.
- Publish selected charge policy in effective configuration.
- Reject unknown values and types during ruleset binding.
- Update the public API baseline deliberately.

Required regressions:

- direct disabled-policy application rejection is immutable;
- disabled damage is unmodified and never consumes state;
- standard binding selects all three supplied policies;
- omitted configuration still selects split;
- malformed selection rejects binding without fallback; and
- save validation/restoration recognizes the disabled policy without accepting
  incompatible retained charges.

**Commit:** `runtime: add disabled charge composition`

**Result:** `DisabledChargePolicy` is registered beside Split and Unified.
`standard_damage` accepts `chargePolicy` values `split`, `unified`, and
`disabled`, exposes the effective selection, rejects malformed values, and
preserves Split when omitted. Empty disabled state validates and restores;
retained slots do not.

### O2-R38: Reconcile the three documentation audiences

**State:** complete

- Update mechanics to distinguish a charge that participated from a later
  charge grant.
- Redraw the technical charge flow around participating charge receipts rather
  than end-of-action damage-category lookup.
- Document disabled, split, and unified host composition, authored selection,
  default behavior, save implications, and extension boundaries.
- Update ruleset contracts, API guidance, capability records, and documentation
  synchronization tests.
- Return the three `combat_resolution` audience entries to `reviewed` only after
  source verification and owner-confirmed semantics agree.

**Commit:** `docs: reconcile optional charge execution`

**Result:** mechanics, developer, technical, decision, ruleset, and public API
documents now describe participation receipts and explicit disabled
composition. The three audience entries are reviewed again; formal capability
closure remains pending the independent O2-R39 source review.

### O2-R39: Independently re-evaluate Order 2

**State:** pending

- Re-read the corrected source without using this roadmap or earlier verdicts as
  implementation proof.
- Reproduce every O2-R36 and O2-R37 regression at public boundaries.
- Cross-check mechanics, developer, technical, decision, ruleset, API, and
  capability records against current code.
- Run focused tests, the complete solution, strict nonincremental builds,
  formatting, content validation, relevant demos, documentation links, boundary
  searches, and `git diff --check`.
- Return `combat_resolution` to `complete` only if no realistic reachable defect
  remains in the corrected scope.

**Commit:** `docs: verify order 2 charge closure`

## Contract Impact

- `IChargePolicyService.CompleteAction` consumes explicit participating
  `ChargeDamageModifier` receipts instead of inferring charge state from damage
  elements. This is an
  intentional pre-release public API correction and requires an API baseline
  update.
- `DisabledChargePolicy` and its policy ID are additive public contracts.
- `chargePolicy` is an additive standard ruleset parameter. Existing content
  omitting it retains the split default.
- Schema v6 remains current because ruleset parameters already use an open typed
  parameter object and semantic binding owns accepted keys and values.
- Save contract v11 remains current. Existing split/unified snapshots retain
  their meaning; disabled composition stores no charge slots.
- No active content pack, DemoHost presentation, or Godot API must change merely
  to demonstrate the optional policy.

## Quality Gate Per Checkpoint

Each checkpoint requires focused tests, the complete Release suite with zero
skips, a strict nonincremental Release build with zero warnings, formatting
verification, relevant documentation or content checks, `git diff --check`, and
one narrow commit before the next checkpoint begins.

O2-R39 additionally runs all noninteractive DemoHost modes and validates all
active packs. The review standard requires an intended invariant, a realistic
reachable path, a concrete consequence, and source or executable evidence.
Theoretical malformed values and alternative product designs remain labeled
separately.

## Definition Of Corrected

Order 2 may close only when:

- charge consumption follows the charge that actually participated in damage;
- a later grant cannot be consumed by an earlier uncharged attempt;
- whole-action split and unified behavior remains intact;
- games can explicitly select disabled charge gameplay through the supplied
  standard composition;
- mechanics, developer, and technical documents describe the same behavior;
- capability and documentation matrices report current truth; and
- O2-R39 finds no remaining reachable defect in these corrected paths.
