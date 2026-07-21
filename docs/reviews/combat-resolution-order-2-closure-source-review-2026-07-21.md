# Combat Resolution Order 2 Closure Source Review

**Date:** 21 July 2026  
**Revision reviewed:** `7a3dd8d` (`docs: verify ordered secondary effects`)  
**Method:** current source, executable tests, active mechanics contracts, and active
design decisions only  
**Disposition:** do not formally close Order 2 until H1 and M1 are corrected  
**Correction authority:** [Order 2 Closure Corrections Roadmap](../roadmap/combat-resolution-order-2-closure-corrections-roadmap.md)

## Scope And Method

This was a new source-first review. Earlier review reports and their conclusions
were not used as implementation authority. The review traced the current code
from authored definitions and schema validation through ruleset binding,
assessment, target preparation, staged effect execution, combat arithmetic,
runtime mutation, complete-action outcome aggregation, encounter turn economy,
and DemoHost composition.

An actionable finding in this review requires all four of these elements:

1. an intended invariant established by current mechanics or public contracts;
2. a realistic path through current code;
3. a concrete consequence; and
4. source or executable evidence that reproduces the path.

Potential alternative game designs, malformed custom-policy output, and values
that cannot enter a supported path are not presented as vulnerabilities.

## Findings

### H1. Schema-valid hit counts can request unbounded allocation and execution work

**Invariant**

Authored content that passes the published schema and Framework semantic
validation must not be able to exhaust host memory or monopolize a battle turn
through one accidental numeric value. Multi-hit attacks are a supported mechanic,
but their execution cost needs an explicit, inspectable bound.

**Reachable path**

- The v5 schema requires a positive hit minimum and maximum but defines no upper
  bound in
  [`shared.schema.json`](../../schemas/content/v6/shared.schema.json#L351).
- Framework semantic validation likewise checks positivity, ordering, and the
  fixed-range rule, but no maximum, in
  [`SkillSystemContentValidator.cs`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs#L1921).
- `ResolveHitCount` accepts every positive ordered `int` range in
  [`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L1000).
- `ResolveDamage` then passes that result directly to
  `new List<ProductionDamageResolutionHit>(hitCount)` and iterates exactly that
  many times in
  [`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L753).

The existing boundary test proves that `ResolveHitCount` can intentionally return
values at `int.MaxValue`, but it stops before the allocating execution path:
[`ProductionCombatRulesetTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/ProductionCombatRulesetTests.cs#L421).

**Consequence**

A typo such as `1000000` instead of `10` is valid content today. Executing it can
allocate a very large result buffer and perform a million hit, critical, damage,
variance, and evidence operations. Larger values can produce an
`OutOfMemoryException`, severe GC pressure, or a frozen host. This is a content
authoring reliability defect, not a claim that arbitrary remote players control
the value.

**Required correction**

Introduce an explicit maximum-hit policy or action work budget. The supplied
standard ruleset should have a conservative documented default, while a custom
policy may select another bound. Enforce the bound at all three layers:

1. JSON Schema for an absolute safe wire ceiling;
2. Framework semantic validation for a typed authoring diagnostic; and
3. runtime policy execution for programmatically constructed definitions.

The runtime must reject before allocating or rolling. Tests should cover the
largest accepted count, one above the limit, uniform ranges that cross the limit,
schema/catalog rejection, and unchanged actor/turn state after runtime rejection.

### M1. Invalid code-authored percentages are silently reinterpreted instead of rejected

**Invariant**

Authored probability values use the documented inclusive `0..100` domain.
Invalid values should produce a typed assessment diagnostic or a clear policy
argument failure; they must not silently become guaranteed or impossible effects.
This is already how damage accuracy, critical chance, instant defeat, JSON Schema,
and catalog semantic validation behave.

**Reachable path**

Convergence supports code-authored definitions and direct policy composition in
addition to JSON packs. These public request/definition paths currently accept
unvalidated percentages:

- `ChancePolicyRequest` and `AilmentApplicationPolicyRequest` are unvalidated
  positional records in
  [`ExecutionPolicies.cs`](../../src/Convergence.Framework/Execution/ExecutionPolicies.cs#L286).
- `BattleAilmentApplicationRequest` stores any `int` chance in
  [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs#L292),
  and the lifecycle service clamps it before policy execution at
  [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs#L409).
- Generic chance execution returns `false` for every value at or below zero and
  `true` for every value at or above one hundred in
  [`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L1030).
- Ailment execution multiplies and clamps the unvalidated base chance in
  [`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L937).
- Skill and item preflight verify handler/repository availability but do not
  diagnose invalid ailment, escape, or nested chance-condition percentages in
  [`SkillExecutor.cs`](../../src/Convergence.Framework/Execution/SkillExecutor.cs#L493)
  and
  [`ItemExecutor.cs`](../../src/Convergence.Framework/Execution/ItemExecutor.cs#L377).
- Basic attack, analyze, and escape assessment checks target resolution, executor
  presence, and effect ordering, but not effect value domains in
  [`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L702).

The canonical JSON path is protected by the schema and
[`SkillSystemContentValidator.cs`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs#L1439).
That protection does not apply when a host constructs public definitions and
commands directly.

**Executable evidence**

A review-time focused probe against `ProductionCombatRuleset` produced these
results:

| Request | Current result |
|---|---|
| `ChancePolicyRequest(101, ...)` | `true` |
| `ChancePolicyRequest(-1, ...)` | `false` |
| ailment base chance `101` | applied with resolved chance `100` |
| ailment base chance `-1` | not applied with resolved chance `0` |

The probe was removed after execution; it changed no repository source.

**Consequence**

A code-authored typo can pass assessment and quietly change a conditional,
escape attempt, or ailment rider into a guaranteed or impossible result. Because
the action is reported as valid, a host cannot distinguish the typo from an
intentional zero- or one-hundred-percent mechanic.

**Required correction**

Use one shared authored-percentage guard and apply it before derived resistance
or modifier math. Add typed preflight diagnostics for invalid ailment chances,
escape chances, and chance conditions in skill, item, and direct effect-action
assessment. Keep clamping only for a valid base chance after explicit policy
multipliers are applied. Direct public policy requests should reject invalid
values clearly. Tests should prove rejection without resource, inventory,
charge, actor-state, or turn-economy mutation.

## Mechanics Verified From Current Code

No additional reachable Order 2 defect was found in the following paths:

- **Hit and evasion:** the supplied policy combines authored accuracy, explicit
  modifiers, and attacker/target Agility with configured coefficients; Luck is
  not read. Zero and one hundred retain their exact boundary meanings.
- **Criticals:** eligibility is a replaceable policy, the supplied default is
  Physical-only, guard blocks critical eligibility, and authored critical chance
  remains exact before explicit typed modifiers.
- **Damage and affinity:** Physical uses Strength, magical elements use Magic,
  Vitality/Defense contribute to defense, and Weak/Resist/Null/Repel/Absorb flow
  through typed resolution and staged resource effects.
- **Charges:** split and unified policies exist; matching charge categories are
  resolved for the complete committed action and consumed once after all targets
  and hits, including miss and defensive-affinity outcomes. Rejected execution
  leaves live charge state unchanged.
- **Instant defeat:** the supplied policy rolls the authored chance once, applies
  configurable resistance multipliers, supports explicit bypass, and reports an
  inspectable reason and roll.
- **Ordered secondary effects:** dependency checks occur before local conditions
  and random rolls. `positive_damage` requires an actual committed negative vital
  resource delta. `same_target` and `any_target` scopes are distinct. Shared
  contact reuses the primary hit while independently owning element, affinity,
  power, charge, and critical behavior.
- **Action pricing:** complete-action aggregation groups damage evidence by
  target across all damage effects. Repeated misses do not multiply penalties,
  mixed landed/missed components are not false evasions, and the supplied item
  default consumes one normal action rather than inheriting offensive effect
  outcomes.
- **Action Token behavior:** Weak/Critical benefit, Miss/Null penalty,
  Repel/Absorb phase termination, and partial-token-first pass precedence match
  the established mechanics.
- **Atomicity:** skills, items, basic attacks, and ordered effects mutate staged
  actor copies. Live actor state is committed only after effect and action-outcome
  resolution succeeds. Item reservations are rolled back when execution does not
  commit consumption.
- **Prepared execution:** assessments are single-use, prepared targets are
  rebound and revalidated, stale skill costs are checked again, and random target
  selection is not repeated between assessment and execution.
- **Automated encounters:** automated skill selection rechecks catalog actor
  authorization, validates the prepared assessment and environment, executes the
  canonical skill executor, and submits its typed outcome to the bound encounter
  turn economy.
- **Random-source boundary:** Framework random consumers use the shared validated
  `RandomSourceContract`; negotiation list selection does not bypass it.

## Verification

Executed from repository root on .NET 8, Release configuration:

- `dotnet test Convergence.sln --no-restore --configuration Release --nologo`
  - Framework: `1227` passed
  - DemoHost: `173` passed
  - ContentValidator: `7` passed
  - total: `1407` passed, `0` failed, `0` skipped
- strict nonincremental solution build with warnings as errors:
  - `0` warnings, `0` errors
- `dotnet format Convergence.sln --no-restore --verify-no-changes`: passed
- `--clean-battle-demo`: victory, exit `0`
- `--clean-field-demo`: completed, exit `0`
- `--clean-save-demo`: validated/restored, exit `0`
- `--clean-training-annex-demo`: victory, reward, valid save, exit `0`
- `git diff --check`: passed before this report was added
- active framework/legacy boundary scan: no production legacy or archive
  dependency found; serializer usage remains confined to the Framework's internal
  content serialization layer rather than public runtime contracts

## Closure Decision

Order 2 is architecturally coherent and most established mechanics are correctly
implemented. H1 is nevertheless reachable through fully schema-valid catalog
content, and M1 causes public code-authored content to be interpreted differently
from the documented authoring domain. Both belong to combat resolution rather
than a later presentation or lifecycle order.

Therefore Order 2 should remain open. After both corrections receive focused
regression coverage and the full gate remains green, one short source recheck is
sufficient; another broad redesign or speculative hardening pass is not needed.
