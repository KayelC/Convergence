# Stat Modifier Policy Feasibility Review

**Date:** 17 July 2026
**Reviewed revision:** `29a1d32` (`main`)
**Scope:** active Framework source, runtime snapshots, ruleset binding, save
validation/restoration, reference content, tests, and clean host composition

## Question

Can the current architecture support persistent staged, timed exclusive, and
independently timed contribution policies without one policy bypassing another
or losing state?

## Verdict

**Yes, after a deliberate shared-state and authority migration.**

The architecture already has useful foundations: immutable content definitions,
staged actor transactions, typed effect execution, duration definitions,
replaceable stage scaling, typed ruleset factories, and a generic turn-economy
precedent.

The three policies cannot be added safely as small strategy classes around the
current actor method. The retained state, ticking, removal, restore, events,
item applicability, and direct public mutation path currently encode one
aggregate-stage/one-duration model. Those areas must move behind one neutral
authority first.

This is a design feasibility finding, not a security vulnerability. Current
source behaves consistently with its existing tests; it simply cannot express
the newly confirmed policy family completely.

## Current Authority Map

| Concern | Current owner | Feasibility consequence |
|---|---|---|
| Authored request | `ModifyStatStageEffectDefinition` stores track IDs, delta, and one optional duration | Sufficient as one application request, subject to policy compatibility validation. |
| Effect application | `ModifyStatStageEffectExecutor` calls `RuntimeActorState.ChangeStatStage` directly | Must delegate to the selected policy authority. |
| Lifecycle application | `BattleStatusLifecycleService.ApplyStatStage` calls the same actor method directly | A second entry point must use the same policy service. |
| Retained live state | One dictionary entry containing aggregate stage and one duration | Cannot retain independent contributions. |
| Duration ticking | `RuntimeActorState.TickTimedStatuses` decrements one duration and removes the entire track | Cannot expire only one contribution. |
| Removal and cleanup | Actor methods inspect aggregate sign and remove whole tracks | Must use policy-defined removal and cleanup transitions. |
| Numeric scaling | `IStatStageScalingPolicy` consumes aggregate stage snapshots | Can remain separate and consume a policy-neutral aggregate projection. |
| Item assessment | Stat-stage effects are treated as applicable by default | Must use policy assessment rather than assume applicability. |
| Item consumption | Authored/result value is treated as meaningful | Must consume from the policy's actual `StateChanged` result. |
| Runtime snapshot | `RuntimeStatStageSnapshot` stores stage plus one duration | Requires a new retained contribution shape. |
| Restore validation | Requires unique track IDs and validates one duration per track | Must validate policy ID, contribution order, signs, bounds, and durations. |
| Ruleset binding | Stat rules bind resolution and scaling only | Needs an independently selectable modifier-policy binding. |
| Execution composition | `BattleExecutionServices` has no modifier policy | Must require the selected authority with no hidden fallback. |
| Events | `StatStageChanged` carries one numeric value | Must report actual aggregate and contribution/duration changes structurally. |

## Representational Analysis

### Persistent Staged

The current aggregate stage can represent magnitude, and a missing duration can
approximate persistence. It does not record which policy owns the state, and
public callers can still mutate it directly. Therefore the data is close but
the authority boundary is insufficient.

### Timed Exclusive

The current stage plus one duration can represent one timed modifier. Current
reapplication always installs the supplied duration while also changing the
aggregate stage, so rejection, replacement, and refresh are not explicit
policy decisions. The shape is close but the behavior is accidental.

### Timed Contributions

The current shape is insufficient. Independent contributions require an
ordered retained collection with signed magnitude and individual duration.
Removing one expired contribution must recompute the aggregate without deleting
the other contributions.

### Common State

A policy-neutral track can represent all three models as ordered immutable
contributions plus a derived aggregate:

```text
track ID
selected policy ID or compatible state kind
ordered contributions
  signed stage amount
  optional duration
derived bounded aggregate stage
```

The exact public type names belong to the shared-contract checkpoint. The
important invariant is that the aggregate is derived from retained source state
rather than replacing it.

## Authority Analysis

Only two production call sites currently apply stages, which makes
centralization practical. However, `RuntimeActorState.ChangeStatStage` is public
and would remain a bypass if a policy service were merely added beside it.

The safe direction is:

1. policies assess immutable requests against immutable track state;
2. policies return immutable before/after transitions and typed diagnostics;
3. a Framework service validates policy results;
4. effect/lifecycle code commits accepted state to the staged actor transaction;
5. raw actor replacement becomes internal infrastructure;
6. public callers use the policy service.

This preserves the existing action transaction guarantee and prevents a custom
policy from partially mutating a live actor before rejection.

## Ruleset And Content Analysis

`RuntimeRulesetPolicyFactoryRegistry` already demonstrates typed host-supplied
factory registration. `RulesetCategory` does not currently contain an
independent stat-modifier category, and `StatRulesetServices` combines only stat
resolution and stage scaling.

The cleanest composition is an independent modifier-policy ruleset category and
factory registry entry. This avoids coupling lifecycle selection to scaling
tables or actor stat composition. Adding that category changes the authored
ruleset wire contract and therefore requires a deliberate schema-version
decision during the binding checkpoint.

The existing `modify_stat_stage` effect can continue to describe one requested
application if policy compatibility is validated. A schema change is not
required merely to retain multiple runtime contributions; it is required if
the authored ruleset selection/category changes.

## Persistence Analysis

Save contract v9 stores the same one-stage/one-duration snapshot used at
runtime. Independent contributions and policy compatibility cannot round-trip
through that shape.

The persistence checkpoint must:

- advance the save contract;
- retain ordered contributions and policy identity;
- validate aggregate bounds and contribution numeric domains;
- reject restoration under an incompatible selected policy;
- restore all actors atomically through aggregate session restoration;
- add a migration extension point without inventing a migration for an
  unreleased external save population.

## Turn-Economy Precedent

`IBattleTurnEconomy` proves that encounter orchestration can consume a generic
policy with Standard Action and Action Token implementations. It also exposes a
useful warning: actor rotation currently lives in the encounter runner, while
the turn economy reports only remaining actions.

A future bonus-action economy may need actor-specific scheduling. It must not be
forced into the current counter contract merely because a policy interface
already exists. The reusable policy-family pattern therefore requires a fresh
responsibility audit for every new family or major implementation.

## Risks To Control

### Public API Break

Replacing `RuntimeStatStageSnapshot` and removing direct stage mutation changes
the pre-release public API and textual API baseline. The shared checkpoint must
update source ownership, API evidence, and migration notes together.

### Dual Authority

Leaving `ChangeStatStage` public or allowing duration lifecycle code to tick
entries independently would negate the policy abstraction. Boundary searches
and adversarial tests must prove there is one path.

### Policy-Specific State Leakage

The common snapshot must preserve enough data without exposing one policy's
private algorithm as a universal contract. Reference policies may derive views,
but retained fields must have neutral meanings.

### Ambiguous Edge Rules

Timed reapplication, opposite signs, cap refresh, and reserve suspension are
not interchangeable details. They remain owner decisions scheduled before the
corresponding implementation checkpoint.

### False Meaningful Success

The original M1 defect remains reachable until integration is complete. A
temporary one-line clamp-result fix would reduce one symptom but would risk
becoming another assumed universal rule. The final correction must consume the
selected policy's typed transition result.

## Healthy Foundations

- Actor execution transactions already stage and atomically commit effects.
- Duration definitions support instant, turn-event, phase, battle, and
  permanent lifetimes.
- Stage scaling is independently replaceable and already table-driven.
- Ruleset factories reject missing, wrong-category, and invalid configuration.
- Runtime snapshot validation and aggregate session restoration provide clear
  places for the new retained-state checks.
- Effect and lifecycle event streams are typed and can be expanded without
  presentation ownership entering Framework.
- Framework boundaries remain neutral to Godot, console, filesystem, and save
  serialization.

## Required Outcome

Proceed through the isolated checkpoints in the
[Stat Modifier Policy Roadmap](../roadmap/stat-modifier-policy-roadmap.md).
Do not implement only the capped-item symptom, and do not claim all three
policies are supported until each has passed the shared contract and end-to-end
integration tests.

## Review Method

This review traced current production source directly from authored effect
definitions through item/skill assessment, ordered effect execution, actor
mutation, duration ticking, cleanup, stat scaling, runtime capture, save
validation, restoration, ruleset binding, encounter orchestration, clean hosts,
and focused tests. Earlier summaries were used only to locate the active M1
finding, not as evidence of current behavior.
