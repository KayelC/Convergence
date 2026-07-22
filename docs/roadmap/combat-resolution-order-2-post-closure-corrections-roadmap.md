# Order 2 Post-Closure Corrections Roadmap

**Status:** active; O2-R31 complete, documentation corrections pending

**Started:** 22 July 2026

**Capability:** `combat_resolution`

## Purpose

The fresh
[Order 2 Code And Documentation Review](../reviews/combat-resolution-order-2-code-and-documentation-review-2026-07-22.md)
reconstructed the current combat implementation from source and executable
tests after revision `4d48308`. It found one reachable assessment and
turn-economy defect plus two classes of active documentation drift.

This roadmap keeps those findings open until each has an isolated green commit
and a final source-first re-evaluation. It does not redesign the approved damage,
hit, critical, affinity, instant-defeat, charge, ordered-effect, item-outcome, or
Action Token mechanics.

## Ordered Checkpoints

### O2-R30: Preserve review evidence and correction authority

**State:** complete

- Preserve the 22 July fresh source and documentation review.
- Record every finding, correction boundary, required test, and commit order in
  this active roadmap.
- Reopen Order 2 in the roadmap index while correction or re-evaluation remains
  incomplete.
- Keep review evidence separate from intended mechanics and current source.

**Commit:** `docs: reopen order 2 registration parity gate`

Implemented by `6f8cb31` after 22 focused documentation and boundary tests.

### O2-R31: Unify effect-configuration preflight

**State:** complete

- Extract one internal recursive validator for runtime effect configuration.
- Use it from `SkillExecutor`, `ItemExecutor`, and
  `BattleActionExecutor.AssessEffectAction`.
- Validate ailment definitions, formula handlers, escape handlers, custom-effect
  handlers, and recursively nested custom-condition handlers.
- Preserve each public executor's stable diagnostic vocabulary while removing
  duplicated validation logic.
- Reject impossible direct basic attacks and escape commands during assessment,
  before target presentation, randomness, mutation, or turn consumption.
- Add focused tests for missing ailment, formula, custom-condition,
  custom-effect, and escape registrations, including an escape request that
  would otherwise consume a normal turn.

**Commit:** `execution: unify effect configuration preflight`

`EffectConfigurationValidator` now supplies one recursive internal authority for
ailment, formula, escape, custom-effect, and nested custom-condition
registrations. Skill and item assessment preserve their specific diagnostics;
direct effect-backed actions return `EffectConfigurationInvalid`. All three
reject before random target selection when required composition is missing.
Focused action coverage passed 163 tests. The complete Release gate passed
1,456 tests with zero failures or skips, a strict nonincremental build with zero
warnings or errors, formatting verification, and `git diff --check`.

### O2-R32: Correct instant-defeat defense terminology

**State:** pending

- Keep instant-defeat resistance distinct from elemental damage affinity.
- Replace the incorrect Null/Absorb statement in the confirmed decision record
  with the implemented `Immune` and configured-multiplier behavior.
- Preserve explicit resistance bypass: multiplier one, authored probability
  still rolled.
- Add or adjust documentation-contract coverage so the two defense vocabularies
  cannot silently merge again.

**Commit:** `docs: clarify instant defeat resistance`

### O2-R33: Reconcile active contract and milestone labels

**State:** pending

- Update current-authority references from schema v5 to schema v6.
- Keep historical checkpoint statements at their historical versions.
- Update the ordered-secondary-effects decision from O2-R16 to the current
  reviewed checkpoint chain without erasing its original implementation
  history.
- Search active non-review documentation for additional statements presented as
  current schema or Order 2 state and reconcile only those that are stale.
- Extend documentation synchronization tests where a durable invariant can be
  checked without hardcoding historical prose.

**Commit:** `docs: reconcile order 2 contract state`

### O2-R34: Independently re-evaluate Order 2

**State:** pending

- Re-read the corrected action-assessment and effect-configuration paths without
  treating this roadmap or earlier reports as proof.
- Reproduce every O2-R31 regression at the public action boundary.
- Cross-check mechanics, developer, technical, decision, roadmap, schema, and
  API guidance against current source.
- Run focused tests, complete solution tests, strict nonincremental Release
  builds, formatting, content validation, relevant DemoHost modes, documentation
  links, boundary searches, and `git diff --check`.
- Record exact evidence and close Order 2 only if no realistic reachable defect
  remains in the reviewed paths.

**Commit:** `docs: verify order 2 registration parity corrections`

## Review Standard

Each runtime finding must retain an intended invariant, realistic public path,
concrete consequence, and executable regression. Documentation is corrected
from source and confirmed decisions, not from an earlier review conclusion.
Alternative game designs and impossible malformed-domain scenarios remain
separate from product defects.

## Completion Conditions

Order 2 may return to verified only when:

- direct actions, skills, and items use the same runtime-registration preflight;
- missing host composition cannot masquerade as a normal failed action or spend
  a turn;
- current active documentation names schema v6 and the current Order 2 state;
- instant-defeat and elemental-affinity vocabularies are unambiguous; and
- O2-R34 independently verifies the corrected source with every required gate
  green.
