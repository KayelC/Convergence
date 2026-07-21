# Ordered Secondary Effects And Order 2 Correction Roadmap

## Purpose

This roadmap reopens Combat Resolution Order 2 after the fresh source review of
21 July 2026 and records the project owner's secondary-effect design intent.
It governs the corrections for complete-action outcome aggregation, Action
Token pricing, ordered-effect life-state changes, schema ranges, and the new
typed dependency model needed by on-hit riders and secondary damage.

The goal is not to hardcode one game's skill behavior. Convergence must support
both independent ordered effects and effects that only become eligible after a
specific earlier effect lands.

## Source Facts At Reopening

Current source provides:

- ordered effect lists for skills, items, and passive triggers;
- one immutable result per effect and target;
- per-hit damage evidence;
- conditions based on actor, target, battle, affinity, and chance state;
- one damage effect generated from an equipment basic-attack profile; and
- no typed way for an effect to depend on an earlier effect result.

The current `when` condition cannot express prior-effect state because the
condition evaluator receives actor and battle state, not the ordered result
history. `onFailure` controls what happens after an effect fails; it does not
decide whether a later effect was eligible to begin.

Equipment basic attacks currently author one element, power, accuracy, Critical
definition, and range flag. Therefore:

- a weapon that attacks as Fire instead of Physical is already possible;
- a weapon that adds an ailment after a landed attack is not yet possible; and
- a weapon that deals both Physical and Fire damage is not yet representable as
  one clean basic-attack profile.

## Confirmed Design Intent

### Primary and secondary effects

A secondary effect may declare that it requires a specific earlier primary
effect to land against the same target.

For the supplied on-hit dependency:

- a primary miss does not attempt the secondary effect;
- Null does not count as dealt damage;
- Repel or Absorb already interrupts the remaining ordered sequence;
- Normal, Resist, or Weak damage satisfies the dependency only when at least
  one committed hit deals positive damage to the intended target;
- multi-target actions evaluate the dependency separately for each target;
- a defeated target cannot be revived by damage absorption or ordinary HP
  restoration; and
- explicit `revive` remains the normal way to restore a defeated actor.

The dependency gate runs before the secondary effect's own chance or other
condition. Randomness is therefore not consumed for a rider that was never
eligible.

### Independence remains authorable

Effect position alone does not create a hidden dependency. An effect with no
dependency remains independent and executes in authored order under its normal
condition and failure policy.

This distinction is necessary for actions such as:

- attack, then apply recoil regardless of hit;
- damage one target, then buff the actor;
- perform two deliberately independent attacks; or
- run cleanup or host-special behavior after an attempted effect.

Convergence will make dependency explicit rather than infer it from array
position, names, descriptions, elements, or effect text.

## Worked Examples

### Poison Needle

Conceptual effect sequence:

```text
needle_hit:
  Physical damage, power 50

poison_rider:
  requires needle_hit to land against this target
  then rolls its authored Poison chance
```

Outcomes:

| Primary result | Poison rider |
|---|---|
| Miss | Skipped without rolling Poison chance |
| Null | Skipped without rolling Poison chance |
| Zero damage | Skipped without rolling Poison chance |
| Positive Normal, Resist, or Weak damage | Poison chance is rolled |
| Target defeated by the hit | Skipped because ailments do not apply to defeated targets |
| Repel or Absorb | Remaining sequence is interrupted |

### Fire Sword: Fire basic attack

The simplest Fire Sword uses one Fire damage effect. It does not deal Physical
damage first and requires no secondary-effect feature:

```text
basic attack element: Fire
```

This is already supported by `EquipmentBasicAttackDefinition`.

### Fire Sword: Physical hit with Burn rider

This requires an equipment basic-attack effect sequence:

```text
sword_hit:
  Physical damage

burn_rider:
  requires sword_hit to land against this target
  then rolls its authored Burn chance
```

The rider is attempted once per affected target, not once merely because some
other target was hit.

### Fire Sword: Physical plus Fire damage

Two mechanically different versions must remain distinguishable:

1. **Independent follow-up:** Physical must land first, then Fire performs its
   own accuracy and Critical resolution.
2. **Shared-contact component:** Physical must land first, then Fire reuses that
   landed contact and does not perform a second accuracy roll.

The dependency contract can gate both versions. Shared-contact damage also
requires a typed damage-resolution mode; it must not be simulated through
`accuracy: 100`, because Agility and explicit Evasion can still make such an
effect miss under the standard policy.

## Approved Typed Contract Requirements

The exact public names receive API review during implementation, but the
contract must contain these concepts:

```text
EffectDefinition
  optional local effectId
  optional dependency

EffectDependencyDefinition
  sourceEffectId
  requirement: succeeded | positive_damage
  scope: same_target | any_target

EffectDependencyEvaluation
  source effect ID and index
  current target ID when applicable
  satisfied flag
  typed reason
```

Local effect IDs are preferred over raw array indexes. Reordering unrelated
effects must not silently redirect a dependency. They are unique within one
effect sequence and are not catalog-qualified content IDs.

An unmet dependency produces a typed `Skipped` result. It does not invoke the
secondary executor, consume condition randomness, activate `onFailure`, or
change turn economy by itself.

For secondary damage, the supplied execution modes are planned as:

- `independent`: the dependency opens the gate, then the damage effect performs
  its own hit and Critical checks; and
- `shared_contact`: the dependency supplies landed contact, so no second
  accuracy roll occurs. The secondary component still resolves its own element,
  affinity, power, charge category, and explicitly authored Critical policy.

Shared contact initially applies once per target when at least one source hit
deals positive damage. It does not inherit the source effect's Critical result;
its own authored Critical policy remains authoritative. Per-landed-hit riders
are deferred until a concrete mechanic requires their additional cardinality
and animation rules.

## Review Findings Being Corrected

### O2-H1: mixed Critical and evasion receives a token benefit

Action Token currently inspects `AnyCritical` after the aggregation policy has
already normalized the complete action to `Normal`. Action Token must consume
the authoritative aggregate outcome while retaining Critical evidence for
presentation and replaceable policies.

### O2-M1: evasion is aggregated per effect instead of per target

All typed damage-hit evidence for one target must be combined across the whole
action. A target only evades when every attempted damage hit against that target
misses.

### O2-M2: later effects can bypass current life state

Ordered execution must account for a target becoming defeated between effects.
Absorb and ordinary restoration cannot revive implicitly, and an effect that
commits no hit because the target is already defeated cannot grant a Weak or
Critical turn benefit.

### O2-L1: schema numeric ranges disagree with semantic validation

Schema v5 must reject the same basic invalid local values already rejected by
Framework validation, including negative power/amount, invalid percentages,
zero stage delta, and non-positive charge multipliers.

## Ordered Checkpoints

### O2-R7: Reopen authority and record secondary-effect semantics

**State:** complete in this documentation checkpoint

- Preserve the fresh source review as evidence.
- Record the owner's primary-hit dependency rule and the three Fire Sword
  representations.
- Preserve the decision as confirmed design authority.
- Reopen Order 2 in the active roadmap indexes.
- Do not alter runtime behavior in this checkpoint.

**Planned commit:** `docs: plan ordered secondary effects`

### O2-R8: Aggregate complete actions by target

**State:** complete in commit `65b0380`

- Group damage-hit evidence by target across all effect results.
- Preserve multi-hit and multi-target precedence.
- Retain custom-effect compatibility where no typed hit evidence exists.
- Add hit-plus-miss, Critical-plus-miss, Weak-plus-miss, and repeated-miss
  regressions across one and multiple effects.

**Planned commit:** `battle: aggregate action outcomes by target`

### O2-R9: Make aggregate outcome authoritative to Action Token

**State:** complete in commit `2eb05bf`

- Remove `AnyCritical` as an implicit Action Token pricing override.
- Preserve it as immutable event/result evidence.
- Prove every aggregate outcome through the actual Action Token economy.

**Planned commit:** `battle: honor aggregate action token outcomes`

### O2-R10: Add typed effect dependencies

**State:** complete in this checkpoint

- Add immutable effect IDs, dependency definitions, scopes, requirements, and
  typed evaluation evidence.
- Validate uniqueness, backward-only references, source effect compatibility,
  and same-target applicability.
- Evaluate dependencies before `when` conditions and effect dispatch.
- Preserve independent effects when no dependency is authored.
- Extend DTO mapping, source metadata, API baseline, schema, deserialization,
  validation, qualification, and immutability tests.

**Planned commit:** `execution: add typed effect dependencies`

### O2-R11: Enforce ordered life-state eligibility

**State:** pending

- Prevent damage, Absorb, and ordinary vital-resource restoration from reviving
  defeated actors.
- Preserve explicit revival and defeat-prevention passives.
- Define typed skipped evidence for targets made ineligible by earlier effects.
- Prove no false Weak/Critical benefit after defeat.

**Planned commit:** `execution: enforce effect life state transitions`

### O2-R12: Execute same-target on-hit riders

**State:** pending

- Implement `damage_landed` dependency evaluation per target.
- Roll ailment or condition chance only after the dependency succeeds.
- Cover Poison Needle behavior for Miss, Null, Resist, Weak, defeat, Repel,
  Absorb, multi-hit, and multi-target actions.
- Prove unmet riders do not mutate, consume randomness, or alter turn economy.

**Planned commit:** `execution: add landed hit effect riders`

### O2-R13: Add secondary damage resolution modes

**State:** pending; shared-contact behavior owner-confirmed

- Preserve ordinary independent secondary damage.
- Add a typed shared-contact path that avoids a second accuracy roll.
- Resolve the secondary element, affinity, power, charge, and Critical policy
  explicitly rather than inheriting hidden values.
- Keep the initial rider cardinality once per target.
- Publish source-effect linkage in damage evidence for host animation.

**Planned commit:** `battle: add secondary damage contact modes`

### O2-R14: Compose equipment basic-attack effects

**State:** pending

- Retain the current single-element basic attack as the minimal profile.
- Add optional ordered secondary effects to the clean equipment profile.
- Route weapon riders through the same dependency and action-outcome paths as
  skills.
- Prove Fire-only, Physical-plus-Burn, and Physical-plus-Fire profiles.
- Do not add equipment behavior based on names or descriptions.

**Planned commit:** `equipment: compose basic attack effects`

### O2-R15: Align schema numeric ranges

**State:** pending

- Encode basic local numeric ranges in schema v5.
- Add independent invalid schema cases and preserve semantic validation as
  defense in depth.
- Confirm every active pack remains valid without content edits.

**Planned commit:** `schema: align combat numeric ranges`

### O2-R16: Documentation, verification, and fresh review

**State:** pending

- Reconcile mechanics, developer, technical, content, and API documentation.
- Add Mermaid flows for dependency gating and shared-contact damage.
- Run the complete quality gate and record exact results.
- Conduct a new source-first Order 2 review before marking it complete.

**Planned commit:** `docs: verify ordered secondary effects`

## Test Matrix

The implementation must cover:

- independent later effects still executing after a primary miss;
- same-target riders skipping after Miss or Null;
- one target hit and another target missed in the same action;
- multi-hit primary attacks with zero, one, or several positive-damage hits;
- dependency gating before random chance consumption;
- primary defeat preventing ordinary secondary effects;
- explicit revival after defeat;
- independent and shared-contact secondary damage;
- Weak, Resist, Null, Repel, and Absorb on each damage component;
- split and unified charge interaction with Physical-plus-elemental damage;
- correct complete-action Action Token cost; and
- immutable result/dependency evidence suitable for Godot presentation.

## Contract Impact

- Existing effects remain independent because dependency metadata is optional.
- Existing active content remains valid and requires no mechanical rewrite.
- Schema v5 can be tightened and extended without a version bump because the
  official semantic pipeline already rejects the newly constrained values and
  optional dependency members do not invalidate existing documents.
- Save contract version 11 remains unchanged because effect definitions and
  transient execution evidence are not saved runtime state.
- Public API additions require an intentional `0.1` baseline update and XML
  documentation.
- No console or Godot presentation dependency enters Framework.

## Definition Of Complete

This correction cycle is complete only when:

- complete-action outcomes are target-correct;
- Action Token obeys the final aggregate outcome;
- dependencies are explicit, typed, local, and per-target;
- Poison Needle behavior is executable without display-text inference;
- both Fire-only and approved Physical-plus-Fire weapon models are represented
  honestly;
- defeated targets cannot be revived by non-revival effects;
- schema and semantic numeric ranges agree;
- all focused and complete gates pass with zero warnings and skips; and
- a fresh review finds no remaining reachable defect in these paths.
