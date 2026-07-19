# Order 2 Combat Resolution Roadmap

**Status:** active

**Started:** 19 July 2026

**Capability:** `combat_resolution`

## Goal

Complete combat resolution as a framework-owned, policy-composed capability and
then document it for players, integrating developers, and maintainers. The work
corrects the reachable gaps found by the source-first Order 2 audit while
preserving action atomicity, host neutrality, and explicit authored data.

The normative design is the confirmed
[Combat Resolution Policy Family](../decisions/combat-resolution-policy-family.md).
This roadmap controls implementation order and verification state; it does not
override that decision record.

## Baseline

- Branch: `main`
- Starting commit: `0aa6c53`
- Save contract: version `10`
- Content contract: schema version `4`
- Active pack versions: `0.4.0`
- Order 2 focused source-audit gate: `185` passing tests
- Full-suite baseline inherited from Order 1: `1,214` passing tests, zero skipped
- Framework and solution builds: zero warnings

The exact totals are re-recorded at every checkpoint because new tests will
increase them.

## Checkpoint Rules

Each checkpoint must:

1. begin from a clean committed predecessor;
2. alter one coherent authority boundary;
3. add focused success, rejection, deterministic-randomness, immutability, and
   rollback coverage appropriate to that boundary;
4. preserve active host, schema, save, API, and Godot boundaries;
5. run focused tests, the complete solution, strict builds, documentation links,
   terminology checks, and `git diff --check`; and
6. end in its own green commit.

No checkpoint is promoted merely because code compiles. A checkpoint is
`verified` only when its behavior and integration tests pass. Order 2 remains
open until the post-implementation code review and three-audience documentation
review are complete.

## Ordered Checkpoints

| ID | State | Scope | Intended commit |
|---|---|---|---|
| O2-C0 | `in_progress` | Record confirmed formulas, policy choices, Luck boundary, examples, and this implementation sequence. | `docs: define combat resolution policies` |
| O2-C1 | `planned` | Add split and unified charge policies, explicit state identity, duplicate rejection, authored final-damage multipliers, and once-per-action consumption. | `runtime: implement charge policy family` |
| O2-C2 | `planned` | Add standard hit/evasion policy, consume passive Accuracy/Evasion modifiers, remove Luck and inert defaults, and use explicit `0..100` bounds. | `battle: implement hit and evasion policies` |
| O2-C3 | `planned` | Add critical chance and eligibility policies, consume passive Critical Chance modifiers, add basic-attack critical metadata, and advance clean schema/packs. | `battle: implement critical policy family` |
| O2-C4 | `planned` | Add configurable instant-defeat resistance multipliers, explicit bypass behavior, one roll, no hidden Luck, and typed no-effect failures. | `battle: complete instant defeat policy` |
| O2-C5 | `planned` | Execute landed hits sequentially in staged state, publish immutable per-hit/per-target evidence, and move outcome aggregation behind a policy. | `battle: expose combat resolution evidence` |
| O2-C6 | `planned` | Replace concrete authored damage binding with a neutral combat-policy aggregate; decouple reward and initiative interfaces from the standard implementation. | `runtime: compose authored combat policies` |
| O2-C7 | `planned` | Perform a fresh source-first review, correct substantiated findings in isolated commits, write all three documentation views, and obtain owner confirmation. | `docs: complete combat resolution order 2` |

## O2-C1: Charge Policy Family

### Implementation

- Add explicit Split and Unified charge-policy services.
- Add General charge vocabulary for the unified policy while Split accepts only
  Physical and Magical slots.
- Route `grant_charge` through the selected policy instead of mutating actor
  state directly.
- Reject an occupied slot without changing its multiplier or duration.
- Preserve the authored multiplier and apply it outside the square-root portion
  of standard damage.
- Track every matching damage category actually resolved by one accepted
  action, then consume each matching slot once after all effects and targets.
- Consume on hit, evasion, Null, Repel, and Absorb; do not consume on assessment
  rejection, cancellation, false conditions, or pre-execution failure.
- Store the selected charge-policy ID with retained charge state and reject
  incompatible restoration.
- Advance the pre-release save contract because retained state meaning changes.

### Required tests

- split Physical and Magical application, rejection, use, and consumption;
- unified General application, rejection, use, and consumption;
- mixed-element action with neither, one, or both split charges;
- multi-hit and multi-target whole-action scope;
- authored multipliers produce distinct final damage;
- miss, Null, Repel, and Absorb consume; cancellation and rejection do not;
- duration expiry remains valid;
- snapshot round-trip, duplicate-state rejection, and policy mismatch rejection;
- no mutation escapes a rejected staged action.

## O2-C2: Hit, Evasion, And Probability

### Implementation

- Introduce a neutral hit-policy request/result with authored and resolved
  contributions.
- Supply configurable attacker-Agility and defender-Agility coefficients.
- Resolve passive Accuracy on the attacker score and passive Evasion on the
  target score using the existing add-then-multiply modifier semantics.
- Exclude Luck from the supplied hit policy.
- Remove inert `DefaultHitAccuracy` and `DefaultInstantDeathChance` parameters;
  typed content continues requiring explicit accuracy/chance.
- Supply `0..100` bounds and exact zero/one-hundred roll behavior.
- Keep deterministic random injection and explicit rigid-state behavior.

### Required tests

- authored skill and basic-attack accuracy sources;
- attacker and target Agility contributions;
- additive and multiplicative passive Accuracy/Evasion modifiers;
- Luck changes do not change supplied hit chance;
- zero never hits and one hundred always hits;
- configurable coefficients and bounds;
- deterministic hit/evasion evidence;
- ruleset binding rejects unknown, malformed, or removed parameters.

## O2-C3: Critical Policy Family And Schema Revision

### Implementation

- Supply exact-authored and accuracy-scaled critical-chance policies.
- Supply physical-only and all-damage eligibility policies.
- Apply attacker Critical Chance modifiers through the existing modifier
  resolver after policy base-chance calculation.
- Exclude Luck and hidden minimums from the supplied policies.
- Preserve explicit target critical-vulnerability modifiers and guard/rigid
  restrictions as typed inputs.
- Add a required critical definition to equipment basic-attack profiles.
- Advance content to the next schema and active pack version together. Older
  pre-release documents are rejected rather than silently defaulted.

### Required tests

- exact authored chance, explicit modifiers, and no Luck effect;
- physical-only and all-damage eligibility;
- accuracy-scaled examples above and below authored accuracy;
- hit is rolled before critical and a miss never rolls a critical;
- basic attack authors and uses `never` or `chance` explicitly;
- strict schema rejection of the old equipment shape and successful loading of
  every migrated active pack;
- Godot and DemoHost content loading remains green.

## O2-C4: Instant-Defeat Completion

### Implementation

- Supply configurable Vulnerable, Normal, Resistant, and Immune multipliers.
- Use authored chance as the only standard base and exclude Luck.
- Resolve one probability roll after resistance.
- Make bypass ignore resistance while preserving the roll.
- Treat blocked or failed resolution as typed no effect, not damage evasion.
- Remove inert standard chance defaults from authored binding.

### Required tests

- the `40 -> 60/40/20/0` standard example;
- configurable multipliers and bounds;
- bypass against every resistance;
- zero and one hundred guarantees;
- no Luck effect and no second hit roll;
- failed/blocked attempts do not receive an evasion token penalty;
- defeat prevention still receives the staged lethal mutation.

## O2-C5: Hit Evidence And Turn-Outcome Mapping

### Implementation

- Extend damage-policy evidence with hit index, authored/final accuracy,
  critical eligibility/chance/result, affinity, damage, and charge data.
- Apply landed hits sequentially to staged actor state while retaining one
  outer action transaction.
- Dispatch defeat prevention at the lethal hit.
- Preserve per-target aggregation independently from per-hit animation facts.
- Add a replaceable action-outcome aggregation policy.
- Supply the confirmed Action Token mapping, including mixed Critical/evasion
  normalization and non-stacking repeated misses.
- Remove duplicate hardcoded aggregation functions from skill and action
  results.

### Required tests

- ordered immutable per-hit facts for fixed and variable multi-hit actions;
- sequential HP changes, drain, reflection, absorption, and defeat prevention;
- multiple targets and mixed affinity outcomes;
- some-hit versus all-evaded target aggregation;
- Critical plus evasion normalizes to normal cost;
- Weak/Critical benefit, evasion/Null penalty, and Repel/Absorb termination;
- failed ailment/instant-defeat probability remains no effect;
- action rollback publishes no staged resource changes or evidence as committed
  state.

## O2-C6: Neutral Authored Combat Composition

### Implementation

- Add an immutable combat-policy aggregate containing the selected damage,
  hit/evasion, critical, charge, instant-defeat, ailment, chance, amount, and
  outcome-mapping authorities.
- Change authored policy factories and `RuntimeRulesetBindingResolver` to return
  the neutral aggregate rather than sealed `ProductionCombatRuleset`.
- Keep the supplied standard factory as one aggregate composition.
- Introduce explicit reward-yield and initiative interfaces so those services
  do not require the concrete combat class.
- Preserve manual host injection of individual execution policies.
- Make policy IDs and selected configuration inspectable for diagnostics and
  retained-state compatibility.

### Required tests

- custom authored factory returns a non-standard aggregate;
- each sub-policy can be replaced independently;
- supplied binding produces all required authorities;
- missing or incompatible sub-policy binding fails before execution;
- reward and initiative work through neutral interfaces;
- DemoHost and Godot sample bind without concrete-type assumptions;
- Framework public APIs remain serializer, filesystem, console, and engine
  neutral.

## O2-C7: Review And Documentation

After O2-C1 through O2-C6 are green:

1. review current source without using this roadmap as proof;
2. trace every public request/result, policy composition, mutation boundary,
   save shape, schema shape, and host path;
3. classify only realistic reachable defects as findings;
4. fix each substantiated finding in its own tested commit;
5. write:
   - a consumer mechanics page with formulas and examples;
   - a developer integration page with policy-selection examples; and
   - a technical page with execution and state diagrams;
6. update the documentation coverage matrix only after the pages match code;
7. ask the project owner to confirm the plain-language behavior.

## Explicit Boundaries

Order 2 does not complete:

- armor defense/evasion composition;
- equipment-granted skills or secondary equipment effects;
- replacement turn economies beyond their interaction contract;
- ailment lifecycle timing;
- battle knowledge persistence;
- encounter orchestration presentation; or
- player-facing battle UI.

Those capabilities remain separate roadmap orders. Order 2 may add the typed
facts they consume, but it must not absorb their rule ownership.

## Completion Gate

Order 2 is complete only when:

- O2-H1 and O2-M1 through O2-M3 are corrected;
- every confirmed decision in the policy record has executable evidence;
- all checkpoints are `verified` with commit and test evidence recorded here;
- active content and saves use only their current contract versions;
- Framework and solution builds report zero warnings;
- all tests pass with zero skips;
- every DemoHost mode and the Godot contract/smoke path remain green;
- documentation links, terminology, API baseline, schema validation, content
  validation, forbidden references, and `git diff --check` pass; and
- the project owner confirms the final three-audience explanation.
