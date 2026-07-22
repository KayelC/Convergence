# Combat Resolution Order 2 Code And Documentation Review

**Review date:** 22 July 2026  
**Reviewed revision:** `4d4830893a7033b7920524f6da430826c3255363`  
**Disposition:** one runtime correction and documentation reconciliation required

## Review Method

This was a fresh trace of the current implementation. Earlier review reports and
their conclusions were not used as evidence. The review reconstructed intended
mechanics from the active confirmed decision and audience documents, then traced
the current source, focused executable tests, public content contracts, ruleset
binding, and complete-action orchestration.

The principal source paths examined were:

- production damage, hit, critical, instant-defeat, affinity, and arithmetic
  policies;
- charge application, action-scoped consumption, and retained-state validation;
- skill, item, basic-attack, analyze, escape, and host-mediated action assessment
  and execution;
- ordered effects, explicit dependencies, secondary contact modes, life-state
  rechecks, and per-hit evidence;
- action-outcome aggregation and Action Token consumption;
- target preparation, canonical action authorization, staged actor transactions,
  passive participation, ailment application, and stat-modifier integration;
- schema-v6 ruleset parameters and runtime policy-factory binding; and
- mechanics, developer, technical, decision, roadmap, and API documentation that
  currently describes those paths.

A finding below is actionable only where the current code provides a realistic
reachable path, a concrete consequence, and reproducible evidence. Harmless
implementation quirks and alternative game designs are not presented as
vulnerabilities.

## Findings

### M1. Direct effect-backed commands omit runtime-registration preflight

**Severity:** Medium  
**Area:** action assessment and turn economy

**Intended invariant**

A host should be able to assess an action, present it as available, and execute
that same prepared assessment without discovering that required Framework
registrations were absent. Missing formula, custom-condition, custom-effect,
ailment, or escape handlers are composition errors, not failed gameplay rolls.

**Current source**

[`SkillExecutor`](../../src/Convergence.Framework/Execution/SkillExecutor.cs#L519)
and
[`ItemExecutor`](../../src/Convergence.Framework/Execution/ItemExecutor.cs#L410)
preflight required runtime registrations for every effect and nested condition.
By contrast,
[`BattleActionExecutor.AssessEffectAction`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L737)
checks authored percentages, target resolution, executor type support, and the
ordered-effect graph, but not those registrations.

This less-complete path is used by programmatic basic attacks and escape
commands. Programmatic basic attacks are a supported public integration path and
may contain typed secondary effects. Escape commands always synthesize an
`EscapeEffectDefinition` from the supplied rule ID.

**Reachable reproduction**

1. Compose `BattleExecutionServices` without an escape handler for
   `sample_escape`.
2. Assess a valid `EscapeAttemptBattleActionCommand(sample_escape, 100)`.
3. Assessment reports `CanExecute == true` because the effect executor exists.
4. Execute the prepared assessment.
5. [`EscapeEffectExecutor`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L803)
   returns an ordinary failed effect because the handler is missing.
6. [`ExecuteEscape`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L1197)
   converts every non-escape result to normal turn consumption.

The same assessment mismatch is reachable with a natural basic-attack secondary
that references an unregistered ailment, formula, custom condition, or custom
effect. Formula and custom-condition misses throw inside staged execution;
custom-effect and escape misses return ordinary failed effects.

**Consequence**

The actor transaction prevents partial actor-state mutation, so this is not a
state-corruption defect. It can nevertheless make a host display an impossible
action as legal. More seriously, missing escape/custom/ailment registration can
be misreported as an in-game failure and spend a turn even though the failure is
host composition, not player chance.

**Required correction**

Use one shared recursive effect-configuration validator for skills, items, and
direct effect-backed battle commands. Assessment should return stable diagnostics
and no turn consumption before targets are presented as executable. Add direct
basic-attack and escape regression tests for every missing registration class,
including nested custom conditions and formula amounts.

### L1. The confirmed instant-defeat decision mixes two defense vocabularies

**Severity:** Low documentation defect  
**Area:** confirmed design authority

[`combat-resolution-policy-family.md`](../decisions/combat-resolution-policy-family.md#L174)
correctly defines the instant-defeat channel as `Vulnerable`, `Normal`,
`Resistant`, or `Immune`, then states that "Null or Absorb channel defenses"
block the effect. The current runtime has no Null or Absorb state in that
channel. Those terms belong to elemental damage affinities.

[`StandardInstantDefeatResolutionPolicy`](../../src/Convergence.Framework/Battle/InstantDefeatResolutionPolicies.cs#L123)
uses the configured resistance multiplier. Its supplied `Immune` multiplier is
zero, while explicit bypass uses multiplier one and still rolls the authored
chance. The mechanics audience page describes this correctly.

The decision record should refer to `Immune` and the selected policy's configured
multiplier, while preserving the documented bypass behavior. Leaving the current
sentence risks a host incorrectly mapping elemental Null/Absorb into the separate
instant-defeat resistance channel.

### L2. Active implementation/version labels have drifted

**Severity:** Low documentation defect  
**Area:** active roadmap and integration guidance

Current active content is schema v6 and the Order 2 implementation is tracked
through O2-R29, but several active statements still present older milestones:

- [`ordered-secondary-effects.md`](../decisions/ordered-secondary-effects.md#L7)
  says implementation is complete only through O2-R16 and later calls that
  checkpoint the Order 2 closure;
- [`stat-modifier-policies.md`](../developer-guide/stat-modifier-policies.md#L16)
  tells developers they are authoring schema v5 content; and
- [`product-roadmap.md`](../roadmap/product-roadmap.md#L21) and its release
  foundations section still label current contracts as schema v5.

Historical checkpoint descriptions may retain the version that was true at that
checkpoint. These statements are written as current authority, so they should be
updated to schema v6 and the current reviewed checkpoint without rewriting
historical evidence.

## Mechanics Revalidated From Source

The following behavior matches the established mechanics and its focused tests:

- physical damage sources Strength; non-physical damage sources Magic;
- defense uses Vitality plus Defense with a minimum effective value of one;
- authored accuracy and Agility-derived evasion resolve before critical chance;
- the supplied combat policy has no hidden Luck contribution;
- critical eligibility is physical-only by default and replaceable by policy;
- Weak, Normal, Resist, Null, Repel, and Absorb preserve their typed outcomes;
- Almighty resolves as Normal rather than consulting authored affinities;
- instant defeat uses one authored-chance roll with configurable resistance
  multipliers and explicit resistance bypass;
- split and unified charge policies reject duplicate charge state and consume
  matching charge once at complete-action scope;
- multi-hit damage resolves and applies each hit sequentially, stopping when the
  target is defeated;
- ordered position does not imply dependency; dependencies reference an earlier
  local effect ID explicitly;
- `positive_damage` uses an actual negative vital-resource delta for the same
  target, not merely a calculated or displayed damage value;
- independent secondary damage rolls its own contact, while shared-contact
  damage reuses contact but resolves its own element, affinity, power, charge,
  critical policy, and hits;
- staged target life state is checked before each effect;
- complete-action outcome precedence preserves terminating Repel/Absorb, Null,
  mixed-target cancellation, Miss, Weakness, Critical, then Normal;
- ordinary items spend one normal turn by default, with effect-driven item
  outcomes available through the authored policy;
- Action Token passing consumes an existing partial token first and converts a
  full token only when no partial token exists;
- prepared random targets, skill costs, canonical definitions, and action
  authorization are revalidated without rerolling or requoting; and
- actor mutation, skill costs, and item reservations are protected by staged
  commit/rollback boundaries on rejection and exceptions.

## Test Quality Assessment

The focused tests assert state, diagnostics, random-call counts, immutable
evidence, resource/inventory changes, and exact turn consumption. They are not
merely checking that methods return without throwing. The reviewed suite includes
meaningful coverage for all affinity outcomes, hit/miss/critical policy,
multi-hit sequencing, charge scope, ordered dependencies, shared contact,
life-state changes, malformed custom results, stale assessments, random target
preparation, and transaction rollback.

The material coverage hole is M1: registered direct effects are tested, and
missing registrations are tested through skill/item executors, but missing
registrations are not tested through `BattleActionExecutor`'s direct
basic-attack/escape assessment path.

## Concerns Considered And Not Promoted

- Custom handlers may perform external host side effects that Framework cannot
  roll back. They are trusted extensions, receive staged actors, and the
  limitation is explicitly documented. This is an integration boundary, not a
  hidden atomicity defect.
- `BattleActionExecutionRequest` stores the acting actor separately from the
  participant target pool. Requiring the actor to appear in that pool would be a
  new public contract decision; current targeted actions already reject
  ineligible selections. No current gameplay failure was demonstrated.
- The runtime target resolver contains a redundant type check over an already
  typed actor collection. It has no behavioral consequence and is not elevated
  into a correctness finding.
- Different hit, critical, charge, item-outcome, stat-modifier, or turn-economy
  formulas are valid product alternatives. The current supplied defaults are
  explicit policies and match the approved mechanics.

## Verification

| Gate | Result |
|---|---|
| Focused combat/action/encounter tests | 329 passed, 0 failed, 0 skipped |
| Complete solution tests | 1,450 passed, 0 failed, 0 skipped |
| Framework tests | 1,270 passed |
| DemoHost tests | 173 passed |
| ContentValidator tests | 7 passed |
| Nonincremental .NET 8 solution build | passed, 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | passed |
| `git diff --check` before this record | passed |

## Health Assessment

Order 2's combat implementation is structurally strong. Policy ownership is
explicit, arithmetic and probability boundaries are guarded, complete actions
are staged atomically, evidence is detailed enough for a neutral host, and the
tests exercise the difficult cross-policy combinations rather than only happy
paths.

The review does not support formal closure at this exact revision because M1 is
a reachable assessment/turn-cost inconsistency on a public integration path.
After shared preflight validation, focused regression coverage, and the three
documentation corrections, another source check can close the order without a
new mechanics redesign.
