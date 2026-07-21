# Combat Resolution Order 2 Ordered Effects Closure Review

**Review date:** 21 July 2026

**Reviewed branch:** `main`

**Review method:** fresh source and test inspection after O2-R7 through O2-R15,
followed by reinspection after the correction; earlier review conclusions were
not treated as evidence

## Result

No unresolved high-, medium-, or low-severity defect remains in the reviewed
complete-action and ordered-effect paths.

The review found one low-severity assessment/execution parity defect. Validated
catalog content could not reach it, but a programmatic skill, item, or natural
basic-attack profile could contain a malformed dependency graph. Assessment
reported the action as executable and execution then rejected it atomically.
Commit `751b9ca` corrected all three assessment surfaces and added regressions.

The reviewed capability is suitable to close Order 2. This conclusion is
limited to the source paths and explicit extension boundaries below; it is not
a claim that every other Convergence subsystem has completed collaborative
documentation review.

## Source Examined

The review traced current behavior through:

- [`Effects.cs`](../../src/Convergence.Framework/Content/Effects.cs)
- [`ContentSurfaceDefinitions.cs`](../../src/Convergence.Framework/Content/ContentSurfaceDefinitions.cs)
- [`DefinitionQualifier.cs`](../../src/Convergence.Framework/Catalog/DefinitionQualifier.cs)
- [`OrderedEffectExecutor.cs`](../../src/Convergence.Framework/Execution/OrderedEffectExecutor.cs)
- [`EffectExecutors.cs`](../../src/Convergence.Framework/Execution/EffectExecutors.cs)
- [`ExecutionContracts.cs`](../../src/Convergence.Framework/Execution/ExecutionContracts.cs)
- [`SkillExecutor.cs`](../../src/Convergence.Framework/Execution/SkillExecutor.cs)
- [`ItemExecutor.cs`](../../src/Convergence.Framework/Execution/ItemExecutor.cs)
- [`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs)
- [`ActionOutcomeAggregationPolicies.cs`](../../src/Convergence.Framework/Execution/ActionOutcomeAggregationPolicies.cs)
- [`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs)
- [`ActionTokenTurnEconomy.cs`](../../src/Convergence.Framework/TurnEconomy/ActionTokenTurnEconomy.cs)
- [`SkillSystemContentValidator.cs`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs)
- [`shared.schema.json`](../../schemas/content/v5/shared.schema.json)
- [`equipment.schema.json`](../../schemas/content/v5/equipment.schema.json)

The review also inspected the focused tests for action aggregation, active skill
execution, basic attacks, charge consumption, deserialization, qualification,
semantic validation, schema validation, and public definition immutability.

## Verified Invariants

### One complete action outcome

Damage-hit evidence is grouped by target across every effect in the action. A
target is treated as having evaded only when every attempted damage hit against
that target misses. A landed component therefore cancels a miss fact for that
same target, while a different fully evasive target remains relevant.

Repel and Absorb terminate, Null takes precedence over ordinary benefits, and a
mixed Critical plus target evasion normalizes to Normal. `AnyCritical` remains
presentation evidence but `ActionTokenTurnEconomy` consumes the authoritative
aggregate `Outcome`; it cannot promote the normalized action back to Critical.

Items are source-aware. The supplied policy prices them as one normal turn by
default without rewriting truthful per-effect outcomes. Authored
`effect_driven` behavior remains available.

### Explicit ordered dependencies

Effect IDs are sequence-local, immutable, and unqualified. Validation rejects
duplicates, missing sources, forward references, non-damage sources for
positive damage, and malformed shared-contact graphs.

At runtime, dependency evaluation precedes current life-state eligibility,
which precedes the effect condition and dispatch. An unmet dependency returns a
typed skip, consumes no condition/chance randomness, performs no mutation, and
does not activate `StopTarget` or `StopAction`.

`positive_damage` reads committed hit evidence. Miss, Null, zero resource
delta, reflection, and absorption cannot satisfy it. The check is per target by
default and one qualifying source hit dispatches the rider once for that
target, even when the source is multi-hit.

### Current staged life state

Later effects read the actor transaction's current staged state. Damage,
instant defeat, ailments, and ordinary vital-resource restoration cannot act
as implicit revival. Explicit revival can restore a defeated target, and later
effects then evaluate the newly living state. A skipped later hit contributes
no false Weak or Critical turn benefit.

### Secondary damage contact

Independent secondary damage resolves its own hit. Shared-contact damage
requires same-target positive damage and performs no second accuracy roll. It
still resolves its own authored element, affinity, power, hit count, charge
category, and Critical policy. It never inherits the source Critical result.

Shared-contact evidence carries its mode and source effect ID/index. All six
affinity outcomes are covered. Split and unified charges are resolved by the
secondary component's own damage category and consumed once when the complete
staged action commits.

### Equipment composition

`EquipmentBasicAttackDefinition` retains the existing primary definition and
adds an immutable secondary sequence. The primary can expose a local ID. The
composed sequence is used identically by assessment and execution, passes
through qualification and semantic validation, and remains part of canonical
basic-attack authorization.

Fire-only, Physical-plus-ailment, and Physical-plus-Fire profiles are
representable without display-name inference. This does not claim completion
of armor defense/evasion, granted equipment skills, or unrelated equipment
effects.

### Schema and semantic defense

Schema v5 now rejects negative local amount/power values, invalid chance and
accuracy percentages, zero stage deltas, non-positive charge multipliers, and
non-positive turn duration values. Semantic validation independently retains
the same checks and owns graph/reference rules that JSON Schema cannot express.

All active content remains unchanged and valid.

## Corrected Finding

### L1. Programmatic dependency graphs passed assessment

**Reachable path:** a host supplies a programmatic skill, item usage, or natural
basic-attack profile rather than a catalog definition, and the sequence has a
duplicate/missing/forward/incompatible dependency.

**Consequence before correction:** assessment could enable the command, while
execution returned a typed failure. Actor and inventory state remained safe,
but host eligibility and execution disagreed.

**Correction:** `OrderedEffectExecutor.ValidateSequence` is now reused by skill,
item, and direct effect-action assessment and by execution. Malformed graphs
are disabled before selection and remain revalidated before mutation.

**Evidence:**

- `AssessAndExecute_InvalidProgrammaticDependencySequenceRejectsAtomically`
- `ItemExecutor_AssessmentRejectsInvalidProgrammaticEffectSequence`
- `BasicAttackAssessmentRejectsInvalidProgrammaticEffectSequence`

## Deliberate Limits

- Existing effects remain independent unless a dependency is authored.
- Rider cardinality is once per qualifying target, not once per landed source
  hit.
- Custom policies and custom effect executors are trusted extension points. They
  must return truthful typed evidence and preserve their documented contracts.
- Runtime sequence assessment protects dependency structure; it does not
  replace schema, semantic, registration, and catalog validation for authored
  packs.
- Action Token is an optional consumer of neutral turn-economy results, not a
  mandatory combat model.

## Verification

- focused action/effect tests: passed;
- full solution: 1,407 tests passed, 0 failed, 0 skipped;
- Framework coverage: 90.46% lines and 75.38% branches;
- strict Release build: 0 warnings, 0 errors;
- format verification: passed;
- active content: 6 packs, 36 documents, 98 qualified definitions passed all
  schema, deserialization, semantic, dependency, registration, and catalog
  checks;
- DemoHost battle, field, save, and Training Annex modes: passed;
- scripted Training Annex play: passed;
- Framework boundary and terminology guards: passed;
- Godot headless smoke: passed;
- `git diff --check`: passed.

## Closure

Order 2 is implemented, source-reviewed, documented for all three audiences,
and owner-confirmed. Its documentation coverage entries may be promoted to
`reviewed`.
