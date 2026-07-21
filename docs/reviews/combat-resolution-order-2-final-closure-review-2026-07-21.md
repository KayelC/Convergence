# Combat Resolution Order 2 Final Closure Review

**Review date:** 21 July 2026

**Reviewed branch:** `main`

**Starting revision:** `69a0964` (`schema: bound resource percentage conditions`)

**Method:** fresh source, schema, and executable-path inspection after O2-R18
through O2-R22; earlier reports were used only to identify former paths to
reproduce, not as evidence that current code was correct

## Result

No unresolved high-, medium-, or low-severity defect remains in the reviewed
Order 2 combat-resolution paths.

This conclusion followed two independent rechecks. The first recheck confirmed
the runtime corrections but found that schema v6 did not constrain
`resourcePercentageCondition.value` to the same `0..100` range enforced by
semantic validation and runtime assessment. Commit `69a0964` corrected that
authoring-contract discrepancy and added independent Draft 2020-12 regressions.
The second recheck then started again from current source and found no further
realistic reachable defect in this closure scope.

Order 2 is therefore suitable for formal closure. This does not declare every
Convergence subsystem complete; it closes the combat-resolution capability and
its approved integration contracts.

## Source Examined

The final review traced current behavior through:

- [`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs)
- [`CombatExecutionLimits.cs`](../../src/Convergence.Framework/Battle/CombatExecutionLimits.cs)
- [`CombatArithmetic.cs`](../../src/Convergence.Framework/Battle/CombatArithmetic.cs)
- [`HitResolutionPolicies.cs`](../../src/Convergence.Framework/Battle/HitResolutionPolicies.cs)
- [`CriticalResolutionPolicies.cs`](../../src/Convergence.Framework/Battle/CriticalResolutionPolicies.cs)
- [`InstantDefeatResolutionPolicies.cs`](../../src/Convergence.Framework/Battle/InstantDefeatResolutionPolicies.cs)
- [`SkillExecutor.cs`](../../src/Convergence.Framework/Execution/SkillExecutor.cs)
- [`ItemExecutor.cs`](../../src/Convergence.Framework/Execution/ItemExecutor.cs)
- [`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs)
- [`OrderedEffectExecutor.cs`](../../src/Convergence.Framework/Execution/OrderedEffectExecutor.cs)
- [`EffectExecutors.cs`](../../src/Convergence.Framework/Execution/EffectExecutors.cs)
- [`ExecutionPolicies.cs`](../../src/Convergence.Framework/Execution/ExecutionPolicies.cs)
- [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs)
- [`ChargePolicies.cs`](../../src/Convergence.Framework/Execution/ChargePolicies.cs)
- [`ActionOutcomeAggregationPolicies.cs`](../../src/Convergence.Framework/Execution/ActionOutcomeAggregationPolicies.cs)
- [`ActionTokenTurnEconomy.cs`](../../src/Convergence.Framework/TurnEconomy/ActionTokenTurnEconomy.cs)
- [`RuntimeRulesetPolicyFactories.cs`](../../src/Convergence.Framework/Runtime/RuntimeRulesetPolicyFactories.cs)
- [`SkillSystemContentValidator.cs`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs)
- all schema-v6 effect, equipment, status, and shared condition declarations
- focused Framework tests for rulesets, actions, lifecycle, schema, semantic
  validation, charges, outcome aggregation, and Action Token behavior

## Verified Invariants

### Authored work and probability boundaries

Active schema and semantic validation accept hit counts only within `1..1024`.
The supplied standard policy has an independently authored maximum, defaults to
`64`, and rejects an effect above that selected maximum before random selection,
list allocation, hit resolution, or actor mutation.

Every authored combat percentage enters one inclusive `0..100` domain. This
includes damage accuracy, critical chance, instant defeat, ailment application,
escape, lifecycle recovery/restriction chances, nested chance conditions, and
actor/target resource-percentage conditions. Invalid code-authored values fail
assessment or request construction before targets, costs, inventory,
randomness, mutation, or turn use. Derived policy results may clamp only after
the authored base is valid. Exact zero and one hundred do not consume random
input.

### Hit, critical, affinity, and arithmetic authority

The supplied hit policy combines authored accuracy, explicit modifiers, and
configured attacker/target Agility coefficients. Luck is not hidden in the
formula. Critical eligibility and critical chance remain separate replaceable
policies; the supplied eligibility default is Physical-only and guard blocks a
critical result.

Each attempted hit records immutable accuracy, critical, affinity, charge,
damage, and resource evidence. Landed hits apply sequentially to staged actors;
later hits do not act after defeat. Weak, Resist, Null, Repel, and Absorb remain
typed outcomes. Combat arithmetic uses checked or saturating operations at
public numeric boundaries rather than allowing decimal or integer wraparound.

### Whole-action execution and atomicity

Skills, items, basic attacks, and ordered effects execute against staged actor
state. Prepared assessments are executor- and request-bound, revalidate stale
cost and environment state, and can be consumed once. Random targets are
prepared once and are not selected again during execution.

Ordered dependencies evaluate before local conditions and dispatch. Shared
contact requires committed positive damage from the declared earlier source.
Later effects read current staged life state. Exceptions or rejected action
outcomes discard staged actor changes and roll back item reservations.

Charge application and consumption happen once per complete committed action.
Split and unified policies remain explicit, occupied charge slots reject rather
than refresh, and matching mixed-category actions consume each matching slot
once.

### Turn economy

The action-outcome policy aggregates all damage evidence once for the complete
action. Repel and Absorb terminate, Null applies its penalty, target evasion is
not falsely inferred when another component lands, and Weak/Critical benefits
remain typed. Items spend one normal action by supplied default; authored
`effect_driven` behavior is opt-in.

Action Token is one optional `IBattleTurnEconomy` implementation. Passing
consumes an existing partial token before touching a full token. Only when no
partial token exists does pass convert one full token into one partial token;
therefore `[partial, full]` becomes `[full]`.

### Authoring and composition parity

The standard combat ruleset factory binds the same policy instances exposed to
execution and records their effective configuration. Schema v6 and semantic
validation agree on all reviewed probability and hit-count domains. Active
content can be validated independently before catalog construction, while
programmatic definitions retain runtime preflight protection.

## Former Paths Reproduced

- a schema-valid or programmatic hit range above its selected maximum is
  rejected before allocation and mutation;
- invalid code-authored percentages return typed action diagnostics or clear
  request-boundary exceptions without consuming state;
- schema-only validation rejects resource percentage values below zero and
  above one hundred;
- exact boundary values remain accepted and deterministic; and
- Action Token pass precedence consumes an existing partial token first.

## Deliberate Limits

- Custom combat, random, outcome, and turn-economy policies remain trusted
  extension points and must obey their public contracts.
- The `1024` schema ceiling is an authoring safety boundary, not a recommended
  balance value; the supplied executable default remains `64`.
- Order 2 does not complete equipment defense/evasion, equipment-granted
  skills, ailment lifecycle documentation, battle knowledge, encounter
  presentation, or another supplied turn economy.
- This review evaluates realistic supported paths. Alternative game designs
  and impossible domain values are not presented as vulnerabilities.

## Verification

- focused combat/schema closure gate: `552` passed, `0` failed, `0` skipped;
- complete solution: `1,439` passed, `0` failed, `0` skipped;
- strict nonincremental Release build: `0` warnings, `0` errors;
- locked dependency restore and NuGet vulnerability audit: passed;
- Framework trimming analysis: `0` warnings, `0` errors;
- formatting verification: passed;
- active content: `6` packs, `36` documents, and `98` definitions passed schema,
  deserialization, semantic, dependency, registration, and catalog validation;
- DemoHost battle, field, save, and Training Annex modes: passed;
- scripted Training Annex play: passed;
- Framework architecture, API, documentation-link, terminology, and forbidden
  reference guards: passed;
- Godot contract tests and local 4.7.1 headless smoke: passed; the Codex process
  environment required `APPDATA` and `LOCALAPPDATA` to be routed to the ignored
  `artifacts` directory because the engine otherwise crashed while creating its
  own user-data path before loading any project code;
- Framework coverage: `90.53%` lines and `75.62%` branches, above the enforced
  `90%` line and `70%` branch thresholds;
- `git diff --check`: passed.

## Closure

O2-R17 through O2-R23 are complete. The first independent recheck found and
corrected a real schema-layer discrepancy; the post-correction source recheck
found no remaining reachable Order 2 defect. The implementation, active schema,
ruleset composition, tests, and all three documentation audiences now agree.
Order 3 may begin from this reviewed baseline.
