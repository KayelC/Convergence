# Combat Resolution Order 2 Post-R27 Source Review

**Review date:** 21 July 2026

**Reviewed branch:** `main`

**Reviewed revision:** `15a084c` (`docs: reconcile order 2 boundary contracts`)

**Method:** fresh inspection of current Framework source and executable tests.
Earlier review conclusions were not treated as proof. The review traced authored
combat definitions through assessment, target preparation, staged execution,
custom extension dispatch, action-outcome aggregation, encounter command
handling, and supplied turn economies.

## Result

O2-R24 through O2-R27 are implemented correctly at this revision:

- repeated skill costs for one resource are rejected independent of list order;
- action-turn and encounter-command values enforce their legal shapes;
- `party_size = 0` has one schema, semantic, runtime, and documentation meaning;
- prepared skill costs use the documented single-use quote-lock contract.

One additional medium-severity host-extension defect remains. It does not
change the approved combat design, but Order 2 should not close while a
supported custom effect can silently become a different effect-flow and turn
economy result.

No security vulnerability was found. The finding is a rule-integrity and
integration-robustness defect caused by malformed output from trusted host
extension code.

## Finding

### O2-M3. Custom effect results can carry undefined execution and turn outcomes

**Intended invariant:** every `EffectExecutionResult` entering ordered effect
execution has a defined execution outcome and a defined turn-economy outcome.
Invalid extension output must reject the action before live actor mutation or
turn consumption; it must not be interpreted as a different legal result.

**Reachable path:** `ICustomEffectHandler` is a public supported extension port.
Its handler returns the public `EffectExecutionResult` record. The record's
`Outcome`, `TurnEconomyOutcome`, and related enum properties are freely
settable through construction or record cloning and do not validate defined
enum values.

`CustomEffectExecutor` clones the returned record into the canonical effect
pipeline. `OrderedEffectExecutor` only branches explicitly for `Interrupted`
and `Failure`, so an undefined execution outcome continues like a successful
effect. `StandardActionOutcomeAggregationPolicy` only recognizes the defined
turn outcomes, so an undefined turn outcome falls through to `Normal`.

**Concrete consequence:** a host integration error can bypass an authored
`StopAction` failure policy, allow later effects to execute, and spend a normal
turn instead of surfacing a typed execution rejection. Because execution uses
staged actors, constructor or port rejection can preserve atomicity; silent
reinterpretation cannot.

**Reproducible evidence:**

- `ExecutionContracts.cs`: `EffectExecutionResult` exposes unvalidated scalar
  `init` properties;
- `EffectExecutors.cs`: `CustomEffectExecutor` accepts and clones the host
  result;
- `OrderedEffectExecutor.cs`: unknown execution values are neither failure nor
  interruption;
- `ActionOutcomeAggregationPolicies.cs`: unknown turn values match no explicit
  outcome and resolve to `Normal`.

**Required correction:** validate the complete public result boundary,
including record-clone assignments. Require nonnegative effect indexes,
defined optional enum values, valid optional runtime/content IDs, and non-null
entries in result collections. A malformed custom result must become the
existing typed pre-commit execution rejection, with unchanged actors,
resources, charges, inventory, later effects, and turn economy.

## Healthy Paths Rechecked

- Damage policy outputs use `DamagePolicyResolution` and
  `DamageHitResolution`, which validate affinity, hit evidence, chance domains,
  charge metadata, and non-null hit collections.
- Random target policy output is checked against the eligible candidate set,
  required count, and duplicate runtime IDs.
- Action-turn and economy-resolution records now reject undefined values and
  impossible payload combinations.
- Encounter command results reject undefined statuses/outcomes and preserve a
  typed fault boundary for malformed host turn results.
- Skill cost assessment rejects repeated resource IDs before formula or random
  resolution and execution consumes a single-use prepared quote.
- Action execution stages actor mutations and only publishes them after effect
  execution, outcome aggregation, and inventory commitment succeed.

## Closure Decision

Keep `typed_action_and_effect_execution`, `combat_resolution`, and
`host_contracts` partial until O2-M3 is corrected and independently rechecked.
O2-R24 through O2-R27 remain valid implemented checkpoints; they are not
reopened by this finding.

The active correction sequence is maintained in the
[Order 2 Pre-Closure Audit Corrections Roadmap](../roadmap/combat-resolution-order-2-pre-closure-audit-corrections-roadmap.md).
