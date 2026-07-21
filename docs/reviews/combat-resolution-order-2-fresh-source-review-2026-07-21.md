# Combat Resolution Order 2 Fresh Source Review

## Review Status

**Revision reviewed:** `65df101b3b2ae8093aa27a586253c9ee96259c65`

**Date:** 21 July 2026

**Disposition:** Order 2 is substantially implemented, but it is not ready for
formal closure. One high-severity and two medium-severity runtime defects remain
in complete-action outcome handling. One low-severity JSON Schema mismatch also
remains in the authoring surface.

This review was reconstructed from current source, executable tests, active
mechanics contracts, and active design decisions. Earlier Order 2 review reports
and completion summaries were deliberately not used as implementation evidence.

## Scope And Method

The review traced the complete path from authored combat data to runtime turn
cost:

1. schema and semantic validation;
2. typed damage, critical, hit-count, charge, and instant-defeat definitions;
3. ruleset binding and policy composition;
4. hit, critical, affinity, damage, ailment, and instant-defeat resolution;
5. ordered effect execution and staged actor mutation;
6. action-level outcome aggregation;
7. Action Token consumption; and
8. focused and integration tests.

The intended mechanics were cross-checked against:

- [Combat, Defenses, And Turn Economy](../mechanics/combat-defenses-and-turns.md)
- [Combat Resolution Policy Family](../decisions/combat-resolution-policy-family.md)

Disposable regression probes were added only long enough to execute the
reachable paths described below. All probes were removed afterward and did not
become product changes.

## Findings

### H1. A mixed Critical and evasion action receives a Critical token benefit after being normalized to normal cost

**Intended invariant**

A complete action containing both a committed Critical and a fully evaded
target has normal turn cost. `AnyCritical` remains useful evidence, but it must
not override the aggregate `Normal` outcome.

**Reachable path**

A multi-target Physical action critically hits one target while every attempted
hit against another target misses.

**Current behavior**

[`StandardActionOutcomeAggregationPolicy`](../../src/Convergence.Framework/Execution/ActionOutcomeAggregationPolicies.cs)
correctly returns `TurnEconomyOutcome.Normal` for the mixed result, while also
retaining `AnyCritical = true` at lines 129-153. However,
[`ActionTokenTurnEconomy`](../../src/Convergence.Framework/TurnEconomy/ActionTokenTurnEconomy.cs)
grants the benefit when either the outcome is Weak/Critical **or**
`AnyCritical` is true at lines 108-109.

**Consequence**

With one full token, the action leaves one partial token instead of consuming
the full token. This grants an extra action in precisely the mixed case that the
approved aggregation rule prices as normal.

**Reproduction evidence**

A disposable integration probe aggregated one Critical target and one fully
evaded target, asserted the aggregate was `Normal`, started a one-token Action
Token phase, and consumed the result. Expected state was `0 full / 0 partial`;
actual state was `0 full / 1 partial`.

**Correction direction**

Action Token should price the authoritative aggregate `Outcome`. Critical
evidence must remain presentation/diagnostic evidence unless a different turn
economy explicitly chooses otherwise. Add an aggregation-to-economy integration
test rather than testing the two classes only in isolation.

### M1. Complete-action evasion is grouped by effect result instead of by target

**Intended invariant**

A target has evaded an action only if every damage hit aimed at that target
misses. One landed hit means that target did not fully evade, even when the
action contains multiple authored damage effects.

**Reachable path**

A single-target composite skill contains two damage effects. The first effect
misses and the second effect hits the same target.

**Current behavior**

[`StandardActionOutcomeAggregationPolicy.IsEvadedTarget`](../../src/Convergence.Framework/Execution/ActionOutcomeAggregationPolicies.cs)
examines each `EffectExecutionResult` independently. The first all-miss effect
therefore sets `anyEvadedTarget = true`; the later hit against the same runtime
target cannot clear it.

**Consequence**

The supplied policy reports `Miss` and Action Token spends the miss penalty even
though the target was hit. The inverse form also exists: a miss effect followed
by a Critical hit on the same target is normalized as mixed Critical/evasion
instead of receiving the Critical result.

**Reproduction evidence**

A disposable probe supplied two damage-effect results for one target: one miss
and one ordinary hit. Expected aggregate outcome was `Normal`; actual outcome
was `Miss`.

**Correction direction**

Aggregate typed damage-hit evidence by `TargetId` across all damage effects,
then decide whether each target was fully evaded. Preserve the existing custom
effect fallback only for results without typed damage-hit evidence.

### M2. Later non-revival effects can revive a target defeated earlier in the same ordered action

**Intended invariant**

Defeat during an ordered action must affect later effect eligibility. Restoring
the vital resource of a defeated actor requires the explicit typed revival
effect; damage absorption and ordinary resource restoration must not silently
perform revival.

**Reachable path**

A target at 10 HP receives an authored two-effect action:

1. ordinary damage deals 10 and defeats the target;
2. a later damage element resolves as Absorb against that same target.

**Current behavior**

[`OrderedEffectExecutor`](../../src/Convergence.Framework/Execution/OrderedEffectExecutor.cs)
resolves the target set once and continues later effects unless an authored
failure explicitly stops that target. In
[`DamageEffectExecutor`](../../src/Convergence.Framework/Execution/EffectExecutors.cs),
ordinary landed damage skips a target that is already defeated, but Absorb does
not perform that check before adding HP. `RestoreResourceEffectExecutor` also
adds vital resources without excluding defeated targets, while
`ReviveEffectExecutor` separately enforces explicit revival.

**Consequence**

The target returns from 0 HP without a revival effect. The action is reported as
Absorb/interrupted, and later battle completion logic sees the target alive.
Separately, a later Weak damage effect can report a Weak action outcome even
when it commits no hit because the previous effect already defeated the target.

**Reproduction evidence**

A disposable `SkillExecutor` probe used a 10-HP target, a 10-damage Physical
effect, and a 10-damage Fire effect against Fire Absorb. The first effect reduced
HP to zero; the second restored HP to 10. The assertion that the target remained
defeated failed.

**Correction direction**

Define and enforce the per-effect life-state invariant at execution time. At a
minimum, damage absorption and ordinary vital-resource restoration must not
revive defeated actors, and effects that commit no hit due to defeat must not
grant affinity or Critical turn benefits. Add composite-effect tests for defeat,
Absorb, restore, Weak, and explicit revival.

### L1. Schema v5 accepts combat numeric values that semantic validation rejects

**Intended invariant**

The published JSON Schema owns basic numeric ranges. Framework semantic
validation should add graph and policy checks, not contradict the schema on
simple local ranges.

**Current behavior**

[`shared.schema.json`](../../schemas/content/v5/shared.schema.json) declares
several combat fields only as `integer` or `number`, including:

- amount values and power at lines 70 and 85;
- resource-percentage condition values at line 206;
- damage power at line 336;
- stat-stage delta at line 426; and
- charge multiplier at line 441.

[`SkillSystemContentValidator`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs)
then rejects negative amounts/power, out-of-range resource percentages, zero
stage deltas, and non-positive charge multipliers. Independent schema contract
tests do not currently exercise those rejected ranges.

**Consequence**

An editor or third-party authoring tool using the published schema alone can
report invalid combat content as valid. The official validator still rejects
it later, so this is an authoring-contract inconsistency rather than a runtime
corruption path.

**Correction direction**

Encode the basic numeric constraints in schema v5 and add independent invalid
schema cases. Retain semantic checks as defense in depth and for constraints
that JSON Schema cannot express cleanly.

## Independently Verified Mechanics

The following behavior is present in source and supported by focused tests:

| Mechanic | Verified implementation |
|---|---|
| Damage formula | Strength drives Physical, Magic drives other damage, target Vitality plus Defense forms defense, configurable scalar/variance and saturating arithmetic are used. |
| Hit and evasion | Authored accuracy plus attacker Agility contribution minus target Agility contribution; add-then-multiply modifiers; configurable clamp; one validated random roll per attempted hit. |
| Luck | The supplied hit, Critical, and instant-defeat policies do not read Luck. |
| Critical chance | Exact authored chance is the supplied default; eligibility and probability are separate; Physical-only and all-damage eligibility policies exist; Guard blocks Critical. |
| Affinity | Shield, Break, active override, base/passive resolution, Almighty normalization, Weak/Resist multipliers, Null, Repel, and Absorb are typed. |
| Instant defeat | Vulnerable/Normal/Resistant/Immune multipliers are configurable; bypass ignores resistance but still rolls; no hidden guaranteed success. |
| Charge | Split Physical/Magical and unified General policies exist; duplicate charge is rejected; multipliers are authored; matching charges affect the whole action and are consumed once after matching damage resolves. |
| Offensive item turn cost | Supplied default is one normal turn; effect-driven pricing is an authored ruleset option. Effect facts remain intact either way. |
| Action Token pass | Existing partial tokens are consumed first; only a pass with no partial token converts one full token into one partial token. |
| Random contract | Framework random consumers route through validated host-random helpers, including negotiation question/dialogue indexing. |
| Mutation boundary | Skill/basic/item actor mutations are staged and committed atomically; item reservation decisions occur before live actor publication. |
| Host neutrality | Combat behavior uses typed definitions and IDs, not display names, descriptions, console, Godot, filesystem, or legacy DTO inference. |

## Owner Clarification After Review

The project owner confirmed that a secondary effect may require the primary
damage effect to deal positive damage. The canonical example is a Physical
needle attack whose Poison chance is only attempted when the needle commits
positive damage to that same target. A miss, Null, Repel, Absorb, or zero-damage
result does not open the rider gate.

The clarification also identified three distinct weapon models:

- one Fire damage effect, already supported by the current basic-attack profile;
- Physical damage followed by an on-hit Burn rider; and
- Physical damage followed by a secondary Fire damage component.

Effect order alone will not create a hidden dependency. The correction will add
explicit typed dependency metadata so independent effect sequences remain
authorable. Independent and shared-contact secondary damage also remain
distinct because the latter must not silently perform a second accuracy roll or
inherit the primary effect's Critical result.
The active implementation sequence is recorded in the
[Ordered Secondary Effects Roadmap](../roadmap/ordered-secondary-effects-roadmap.md).

## Test Evidence

- Focused current tests: **330 passed, 0 failed, 0 skipped** across combat
  ruleset, hit, Critical, instant defeat, charge, action aggregation, active skill,
  battle action, schema, and semantic validation suites.
- Disposable review probes: **three expected assertions failed**, reproducing
  H1, M1, and M2. They were removed after execution.
- Full solution: **1,330 passed, 0 failed, 0 skipped** across 1,150 Framework,
  173 DemoHost, and 7 ContentValidator tests.
- Nonincremental .NET 8 solution build: **0 warnings, 0 errors**.
- `dotnet format --verify-no-changes`: passed.
- `git diff --check`: passed.

## Health Assessment

The combat policy layer itself is coherent, configurable, host-neutral, and
well defended at numeric and random boundaries. The problems are concentrated
where per-hit facts become a complete-action result and where one authored
effect changes eligibility for the next effect.

That concentration is good news architecturally: Order 2 does not require a
combat rewrite. It requires focused corrections to aggregation, Action Token
pricing, ordered-effect life-state handling, and schema range coverage. Until
H1-M2 are corrected and regression-tested together, however, Order 2 should not
be described as formally complete.
