# Combat Resolution Order 2 Source Review

**Review date:** 19 July 2026

**Reviewed branch:** `main`

**Capability:** `combat_resolution`

**Result:** implementation corrections and owner decisions required

## Purpose

This review begins Documentation Order 2. It derives the active combat model
from current Framework source, executable tests, schemas, clean content, and
host composition before consulting older documentation. Archived prototype
code was used only to identify a historical behavior worth asking about; it is
not current authority.

The review does not change combat behavior. It records what the Framework
actually does, separates reachable defects from design choices, and prevents
the documentation matrix from promoting plausible prose that has not been
confirmed against source.

## Source Traced

- `ProductionCombatRuleset`, its public requests/results, configuration, and
  saturating arithmetic;
- elemental, ailment, and instant-death defense resolution;
- runtime actor combat-profile composition and stat-stage scaling;
- damage and instant-kill effect execution, resource mutation, drain, and
  defeat-prevention dispatch;
- passive numeric and affinity modifier resolution;
- battle action basic-attack conversion and canonical authorization;
- ruleset policy factories, binding results, DemoHost composition, and the
  Godot reference consumer;
- schema-v4 damage, instant-kill, charge, passive-modifier, and equipment
  contracts;
- focused combat, stage-scaling, action, effect, passive, and ruleset tests.

## Current Supplied Damage Model

For each authored hit, the supplied policy currently performs this work:

1. Physical damage selects Strength. Every other damage element selects Magic.
2. The selected attack stat is multiplied by generic outgoing damage and the
   applicable physical or magical stat-stage multiplier.
3. A matching charge multiplies that attack value by the ruleset-level charge
   multiplier.
4. Defense is `max(1, Vitality + Defense)`.
5. Raw damage is
   `damageFormulaScalar * sqrt(power * attack / defense)`.
6. Target damage-taken state, critical, guard, Weak or Resist, and variance are
   applied in that order.
7. Each hit is floored independently.
8. The effect executor sums landed hits, then applies passive
   `damage_dealt` and `damage_taken` numeric modifiers once to the total.
9. The executor performs one aggregate HP mutation, reflection, or absorption,
   then returns one effect result and one turn-economy outcome.

Null, Repel, and Absorb do not use a damage multiplier. They retain the
calculated total so execution can suppress it, reflect it to the attacker, or
restore it to the target.

## Current Accuracy And Critical Model

Hit chance is:

`authored accuracy + 2 * (attacker agility * hit multiplier - target agility * evasion multiplier) + (attacker luck - target luck)`

The supplied default clamps the floored result to `5..99` and then rolls it.
Consequently, authored accuracy `0` may still hit and authored accuracy `100`
may still miss unless a game changes the configured range. Rigid-body state
forces a hit.

Criticals currently behave as follows:

- only Physical damage can critically hit;
- guarding prevents criticals;
- rigid-body state forces a Physical critical;
- `NeverCriticalDefinition` prevents a critical;
- otherwise the policy starts with
  `(attacker luck - target luck) / 2 + criticalChanceBase + target bonus`;
- `ChanceCriticalDefinition.Chance` is treated as a minimum for that value,
  not as an exact chance or an additive bonus;
- the attacker critical multiplier is applied, the result is floored and
  clamped to the configured range, then rolled.

The supplied default critical range is `2..40`.

## Current Defense Precedence

For non-Almighty elemental damage, resolution is:

1. matching shield becomes Repel;
2. active Clean Break becomes Normal;
3. active affinity override wins;
4. base affinity and applicable passive replacements resolve by strongest
   response: `Absorb > Repel > Null > Resist > Normal > Weak`.

Almighty always resolves to Normal before those checks. Guard converts an
otherwise resolved Weak affinity to Normal for the damage result.

Instant death uses a separate Light or Dark resistance channel, or an explicit
bypass mode. Immune blocks a channel-checked attempt. Bypass ignores the
channel but still rolls. The supplied chance is authored chance plus Luck
difference, clamped to `5..95`. Vulnerable and Resistant are currently treated
the same as Normal by the supplied instant-death calculation.

## Confirmed Healthy Boundaries

- Damage effects carry typed element, power, accuracy, critical mode, hit
  count, drain, conditions, and failure policy.
- Runtime damage policy results are immutable and require a defined resolved
  affinity.
- Hit count is selected once; every selected hit independently rolls accuracy,
  critical state, and variance.
- Arithmetic uses saturating helpers at extreme numeric boundaries rather than
  wrapping into negative damage or rewards.
- Affinity, ailment resistance, and instant-death resistance are separate typed
  channels.
- Stat-stage magnitude comes from the independently bound stage-scaling policy.
- The canonical action transaction stages actor mutations and only publishes
  accepted execution state.
- Framework combat APIs remain free of console, Godot, filesystem, and
  serializer types.

These strengths are covered by the focused combat and execution suites. They
do not close the findings below.

## Implementation Findings

### O2-H1: Authored charges are not authoritative or consumed

**Invariant:** an authored charge should use its retained multiplier and have a
defined one-use or duration-based lifecycle.

**Reachable path:** `GrantChargeEffectDefinition` requires a multiplier and
`RuntimeActorState` stores that multiplier in `BattleChargeState`. Combat
profile composition reduces the state to `HasPhysicalCharge` or
`HasMagicalCharge`, and `ProductionCombatRuleset` uses its separate global
`ChargeMultiplier`. No offensive execution path removes the matching charge.

**Consequence:** a `2.5` authored charge and a `1.1` authored charge resolve
identically under the same ruleset. With the supplied configuration, both use
`1.9` on the attack value before the square root. The charge can affect every
matching attack until a duration tick or cleanup removes it.

**Historical evidence only:** the archived prototype consumed a matching
charge after the complete offensive action, including a miss, and did so after
all targets were processed. That behavior is not automatically authoritative
for Convergence, but it demonstrates that the current persistent behavior was
not the only represented intent.

**Recommended direction:** make the retained authored multiplier authoritative
and consume a matching charge once after one committed offensive action, after
all effects and targets have benefited from it. Duration remains an expiry
fallback. Consumption on miss, Null, Repel, and Absorb requires owner
confirmation. The multiplier should describe final damage rather than being
hidden under the square root unless the project owner explicitly chooses the
current attack-stat interpretation.

### O2-M1: Two standard configuration values are inert

`DefaultHitAccuracy` and `DefaultInstantDeathChance` are public, validated,
authorable ruleset parameters. Neither is read during execution. Schema v4
requires every damage effect to author `accuracy` and every instant-kill effect
to author `chance`, so there is no omission path on which either default can
operate.

Changing these ruleset parameters therefore changes no battle result. Current
binding tests only prove that the values were stored in the configuration.

**Recommended direction:** remove both values from the public configuration,
standard factory, API baseline, and active documentation. Making authored
accuracy/chance optional would weaken explicit content without providing a
clear benefit.

### O2-M2: Three authored passive modifier kinds are inert in combat

The public content vocabulary and schema accept numeric passive modifiers for
`accuracy`, `evasion`, and `critical_chance`. Validation, deserialization, and
the generic modifier registry preserve them. Canonical combat resolution never
queries those three modifier kinds. Only damage dealt/taken, healing, and
resource-cost modifiers are currently consumed by the relevant execution
paths.

A valid passive that claims to alter accuracy, evasion, or critical chance can
therefore load successfully while changing no combat outcome.

**Required decision:** either define and implement their exact place in the
standard formulas, or remove the unsupported vocabulary before release. The
review recommends implementation because all three are ordinary combat
extension concepts, but their additive/multiplicative semantics must be
confirmed before code is changed.

### O2-M3: Authored damage-policy replacement is not actually neutral

Direct execution is modular: `BattleExecutionServices` accepts
`IDamageExecutionPolicy`, `IInstantDeathExecutionPolicy`,
`IAilmentApplicationPolicy`, `IChanceExecutionPolicy`, and
`IPowerAmountPolicy` independently.

Authored ruleset binding is narrower. `IRuntimeDamageRulesetPolicyFactory`
must return the sealed concrete `ProductionCombatRuleset`, and
`BindProductionCombatRuleset` exposes that same type. A custom authored damage
policy ID therefore cannot bind a different implementation through the stated
factory extension point. It can only construct another standard ruleset
instance. A host can inject custom policies manually, but then it bypasses
authored ruleset selection.

This contradicts the active claim that a game may bind another implementation.

**Recommended direction:** introduce a neutral combat-policy aggregate returned
by the ruleset factory and binder. The supplied standard factory can populate
the aggregate with `ProductionCombatRuleset`; custom factories can supply other
implementations. Reward and initiative coupling must be separated or represented
by explicit interfaces rather than requiring the concrete standard class.

## Owner Decisions Required

### O2-D1: Charge application and consumption

Confirm all of the following:

1. the effect-authored multiplier is authoritative;
2. it multiplies final damage rather than the pre-square-root attack stat;
3. one matching charge is consumed once per committed offensive action, after
   all targets and hits;
4. it is consumed on miss, Null, Repel, and Absorb as well as ordinary damage;
5. an action containing both Physical and magical damage may consume both
   matching charges.

### O2-D2: Passive accuracy, evasion, and critical semantics

The review recommends the following neutral meanings:

- attacker Accuracy modifies the authored base accuracy before Agility/Luck;
- target Evasion modifies the target evasion contribution;
- attacker Critical Chance modifies the calculated critical chance before the
  configured clamp;
- existing add-then-multiply stacking remains the supplied stacking policy.

The exact ordering must be owner-confirmed because it changes combat numbers.

### O2-D3: Authored critical chance

Choose whether `ChanceCriticalDefinition.Chance` means:

- an exact base chance before actor modifiers;
- a minimum chance, matching current code but requiring clearer naming; or
- an additive bonus to the actor-derived chance.

The current minimum behavior is not explained by the public type or schema.

### O2-D4: Instant-death resistance levels

Choose whether Vulnerable and Resistant modify instant-death chance. The
review recommends configurable standard multipliers, parallel to ailment
resistance, while retaining Immune and explicit bypass behavior.

### O2-D5: Multi-hit application and host evidence

The policy resolves per-hit outcomes, but the executor sums landed hits before
resource mutation, passive defeat prevention, drain, and public effect output.
The canonical `EffectExecutionResult` does not retain per-hit chance, damage,
or critical evidence.

Confirm whether Convergence should:

- apply each hit sequentially to staged runtime state and dispatch defeat
  prevention at the hit that would defeat the target; and
- expose immutable per-hit evidence so Godot can animate and present each hit
  without rerunning combat math.

The review recommends both. Atomic action commit can remain intact because the
sequential work occurs inside staged actor state.

### O2-D6: Supplied chance floors and ceilings

Confirm the supplied defaults:

- hit chance `5..99`;
- critical chance `2..40`;
- instant-death chance `5..95`.

These are replaceable parameters, but they define surprising baseline behavior
such as a nominal `100` accuracy attack still being able to miss.

## Explicitly Deferred Adjacent Work

These are real current boundaries, but this review does not inflate them into
new Order 2 defects:

- canonical basic attacks are always one-hit and never-critical;
- `IsLongRange` and passive basic-attack rule modifiers are authored but not
  consumed;
- armor Defense/Evasion is not yet composed into the canonical runtime combat
  profile;
- equipment-granted skills and typed secondary equipment effects remain
  unfinished.

Those items belong to the already acknowledged equipment/basic-attack
completion work. Order 2 documentation must state the boundary and must not
claim that those profiles affect combat today.

## Review Gate

`combat_resolution` is reopened as `partial`. Documentation remains
`existing_unreviewed` or `missing`. Promotion requires:

1. owner confirmation of O2-D1 through O2-D6;
2. isolated implementation commits and focused regressions for O2-H1 through
   O2-M3 and any approved design changes;
3. a fresh source-first post-correction review;
4. mechanics, developer, and technical documentation with formulas, diagrams,
   composition examples, and host evidence guidance;
5. owner confirmation of the plain-language behavior;
6. focused and full tests, strict builds, documentation links, and diff checks.

## Owner Resolution

The project owner confirmed O2-D1 through O2-D6 on 19 July 2026. The decisions
are recorded normatively in
[Combat Resolution Policy Family](../decisions/combat-resolution-policy-family.md),
and their isolated implementation sequence is tracked by the
[Order 2 Combat Resolution Roadmap](../roadmap/combat-resolution-order-2-roadmap.md).

This review remains the source-derived discrepancy record. It is not amended to
describe unimplemented behavior as current behavior. Its findings advance from
`source_confirmed` to `planned`; the combat capability remains `partial` until
the roadmap implementation, fresh source review, and three-audience
documentation gate are complete.

## Baseline Verification

The initial focused Order 2 filter passed 185 tests covering production combat,
stage scaling, active execution, action execution, ruleset binding, and passive
runtime behavior. No failure exposed the findings because the missing charge,
inert parameter, inert passive-modifier, and custom-factory cases do not yet
have assertions for their promised behavior.
