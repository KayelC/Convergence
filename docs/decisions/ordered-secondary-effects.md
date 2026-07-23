# Decision: Ordered Secondary Effects

**Status:** confirmed

**Decision date:** 21 July 2026

**Implementation state:** verified through O2-R34

## Context

Convergence already executes skill, item, passive, and basic-attack effects in
authored order. Effect order alone, however, does not say whether a later effect
is independent or requires an earlier effect to affect the same target.

This distinction matters for actions such as a Physical needle strike followed
by a Poison chance. The Poison chance must not run when the strike misses, is
nullified, is repelled, is absorbed, deals no positive damage, or defeats the
target. Conversely, an independently authored recoil or actor buff may still
need to run after a failed strike.

## Decision

### Dependencies are explicit

Effect position does not create an implicit dependency. An authored secondary
effect may reference a unique local ID belonging to an earlier effect in the
same sequence. Effects without dependency metadata remain independent.

The supplied dependency requirements distinguish general source success from
positive damage dealt. The standard on-hit rider uses the positive-damage
requirement.

### Positive damage gates standard on-hit riders

For each intended target, a positive-damage dependency is satisfied only when
the referenced source effect commits at least one hit that removes a positive
amount of that target's vital resource.

- Miss and Null do not satisfy it.
- Repel and Absorb do not satisfy it and continue to interrupt the ordered
  sequence under the standard damage rules.
- A zero-damage hit does not satisfy it.
- Normal, Resist, or Weak damage satisfies it only when positive damage is
  committed.
- A source effect that defeats the target may satisfy the damage fact, but
  ordinary hostile or restorative riders skip because the target is no longer
  life-state eligible. Explicit revival remains the only ordinary resurrection
  path.

Dependency evaluation occurs before the secondary effect's condition, random
chance, or executor. A skipped rider consumes no random draw and causes no
mutation or turn-economy fact.

### Multi-hit and multi-target cardinality

Dependencies are evaluated independently per target. A secondary rider is
attempted once for a target when at least one source hit deals positive damage
to that target. Several landed hits do not repeat the rider. One target's landed
hit cannot make another target's rider eligible.

### Secondary damage modes

Convergence supplies two explicit models for dependent secondary damage:

- **Independent:** after its dependency succeeds, the secondary damage performs
  its own hit and critical resolution.
- **Shared contact:** after its dependency succeeds, the secondary damage does
  not roll accuracy again. It still resolves its own authored element,
  affinity, power, charge category, and critical policy.

Neither mode inherits the primary effect's critical result. Critical behavior
must be authored for the secondary damage itself.

### Ordered life-state eligibility

Each effect evaluates current staged actor state when its turn in the sequence
arrives. Damage absorption and ordinary vital-resource restoration cannot
revive a target defeated by an earlier effect. Later hostile effects skip a
defeated target. A typed revival effect may restore it, after which later
effects evaluate the revived state normally.

### Equipment uses the same composition model

A basic-attack profile retains one primary damage definition and may add an
ordered secondary effect sequence. It does not infer behavior from an equipment
name or description.

This represents three distinct weapon designs honestly:

1. a Fire weapon whose primary attack deals Fire damage;
2. a Physical weapon with a dependent Burn rider; and
3. a Physical weapon with dependent secondary Fire damage, authored as either
   independent or shared contact.

## Worked Example

```text
needle_hit:
  Physical damage, power 50

poison_rider:
  requires needle_hit to deal positive damage to this target
  then rolls the authored Poison chance once
```

| Needle result | Poison rider |
|---|---|
| Miss, Null, Repel, or Absorb | Skipped without a Poison roll |
| Zero damage | Skipped without a Poison roll |
| Positive Normal, Resist, or Weak damage | Poison chance is rolled once |
| Target defeated | Skipped because the target is no longer eligible |

## Alternatives

- **Make every later effect depend on the previous effect:** rejected because
  it prevents independent recoil, self-buffs, cleanup, and deliberate separate
  attacks.
- **Treat any successful contact as landed damage:** rejected for the supplied
  on-hit rule because it permits zero-damage and nullified attacks to apply
  riders.
- **Use `accuracy: 100` for shared contact:** rejected because standard Agility
  and Evasion can still make a one-hundred-accuracy effect miss.
- **Repeat riders for every landed hit:** deferred until a concrete mechanic
  defines its stacking, animation, and random-draw semantics.

## Consequences

- Existing effects remain independent because dependency metadata is optional.
- Content received additive local effect IDs, dependencies, and secondary
  damage mode fields under schema v5; the same contract remains present in
  active schema v7.
- Execution results expose typed skipped/dependency evidence for engine hosts.
- Complete-action aggregation and Action Token pricing must use committed facts
  from the whole action rather than effect-local shortcuts.
- Save contract v13 remains unaffected because definitions and transient execution
  evidence are not persisted runtime state.

## Evidence And Affected Documentation

- [Ordered Secondary Effects Roadmap](../roadmap/ordered-secondary-effects-roadmap.md)
- [Combat Resolution Policy Family](combat-resolution-policy-family.md)
- [Combat, Defenses, And Turn Economy](../mechanics/combat-defenses-and-turns.md)
- [Combat Resolution Policies](../developer-guide/combat-resolution-policies.md)
- [Combat Resolution Pipeline](../technical/combat-resolution-pipeline.md)
- [Fresh Order 2 Source Review](../reviews/combat-resolution-order-2-fresh-source-review-2026-07-21.md)
- [Ordered Effects Closure Review](../reviews/combat-resolution-order-2-ordered-effects-closure-review-2026-07-21.md)

O2-R16 completed and verified the original ordered-secondary-effects scope.
Later Order 2 reviews expanded its complete-action and validation coverage.
O2-R30 through O2-R34 preserve the current correction and independent
re-verification chain without changing this decision's approved mechanics.
