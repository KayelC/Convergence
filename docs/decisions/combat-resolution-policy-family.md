# Decision: Combat Resolution Policy Family

**Status:** confirmed

**Decision date:** 19 July 2026

**Implementation state:** confirmed mechanics and pre-closure corrections
implemented and source-verified through O2-R29; all three audiences
owner-confirmed

## Context

The Order 2 source review found that Convergence already exposes several combat
extension interfaces, but its supplied implementation still mixes independent
rules inside one concrete class. It also retains values that are either ignored
or interpreted differently from their public names:

- authored charge multipliers are stored but a hidden ruleset multiplier is
  used instead;
- charges are not consumed by attacks;
- authored Accuracy, Evasion, and Critical Chance passive modifiers load but do
  not affect combat;
- authored critical chance is treated as a minimum rather than an exact base;
- Luck silently changes hit, critical, and instant-defeat chances;
- Vulnerable and Resistant instant-defeat channels behave like Normal;
- probability floors make zero-percent events possible and one-hundred-percent
  events fallible; and
- one aggregate effect result cannot explain each hit to a presentation host.

These are not merely formula defects. They expose a design boundary: a reusable
framework must supply useful defaults without making one game's combat model
mandatory. The project owner therefore approved a family of explicit,
replaceable policies and the supplied defaults below.

## Decision

### One authority per rule

Each variable combat rule has one selected authority:

- charge application and consumption;
- hit and evasion chance;
- critical eligibility;
- critical chance;
- instant-defeat chance;
- probability bounds and rolls;
- per-target action-outcome aggregation; and
- the mapping from combat outcomes to a turn-economy result.

The supplied policy aggregate composes those authorities. A host may bind the
supplied aggregate from authored ruleset content or inject replacements. A
custom policy must not require subclassing a sealed standard ruleset.

### Luck has no hidden combat-probability role

The supplied Order 2 combat policies do not use Luck for hit chance, evasion,
critical chance, instant defeat, or damage. This keeps the meaning of authored
accuracy and critical values exact and prevents an undocumented statistic from
changing several unrelated rolls.

Luck remains a normal framework stat that another policy may consume
explicitly. Existing reward and shop policies may also use it because those are
separate rule authorities. Removing Luck from standard combat probability does
not remove the stat or prohibit a game-specific Luck-based combat policy.

### Hit and evasion

The supplied hit policy uses this shape:

```text
attacker accuracy score =
    authored action accuracy
    + attacker Agility * configured attacker coefficient
    + explicit attacker Accuracy modifiers

target evasion score =
    target Agility * configured target coefficient
    + explicit target Evasion modifiers

final hit chance = clamp(
    attacker accuracy score - target evasion score,
    configured minimum,
    configured maximum)
```

The standard bounds are `0..100`. Zero is impossible and one hundred is
guaranteed. The roll is successful when a normalized random value is strictly
less than `chance / 100`.

Skills use their authored accuracy. A basic attack uses its weapon or supplied
basic-attack profile accuracy. A future weapon-dependent skill must explicitly
select its source; the framework does not guess from names or descriptions.

The supplied modifier resolver retains its documented add-then-multiply
stacking. Accuracy modifiers apply to the attacker score and Evasion modifiers
apply to the target score. The two contributions remain inspectable rather than
being collapsed into an unexplained scalar.

### Critical chance and eligibility

`ChanceCriticalDefinition.Chance` is the exact base chance before explicit
modifiers. It is not a minimum, a guaranteed floor, or a hidden bonus.

Convergence supplies two chance policies:

1. `AuthoredCriticalChancePolicy`, the standard policy, begins with the exact
   authored chance and then applies explicit modifiers.
2. `AccuracyScaledCriticalChancePolicy`, an optional reference policy, scales
   authored critical chance by the ratio between final hit chance and authored
   accuracy before applying explicit modifiers.

Convergence also separates critical eligibility from critical chance:

1. `PhysicalOnlyCriticalEligibilityPolicy` is the supplied default.
2. `AllDamageCriticalEligibilityPolicy` permits any damage element whose effect
   explicitly authors a critical chance.

Critical chance is rolled only after the hit succeeds. Guard and other explicit
runtime restrictions may reject eligibility. No implicit Luck bonus or forced
minimum is applied.

Example: an action with accuracy `80` and critical chance `20` has a twenty
percent critical chance on each successful hit under the standard chance
policy. An explicit `1.5` Critical Chance multiplier produces `30`. The same
magical action has zero chance under physical-only eligibility and `20` under
all-damage eligibility. Under the optional accuracy-scaled policy, a final hit
chance of `60` scales the base to `15` before other modifiers.

Basic-attack profiles must author their critical definition just as skills do.
Adding that field requires a clean content-schema revision rather than a hidden
runtime default.

### Charge policies

Convergence supplies three mutually exclusive charge-state policies:

1. `SplitChargePolicy` retains independent Physical and Magical charge slots.
2. `UnifiedChargePolicy` retains one General charge slot for all eligible
   damage.
3. `DisabledChargePolicy` retains no slots, rejects charge grants, and leaves
   damage unmodified.

The supplied `standard_damage` factory accepts `chargePolicy` values `split`,
`unified`, and `disabled`; omission retains the `split` default. This is an
explicit composition choice rather than a nullable service or a charge state
that a game must work around.

Applying a charge to an already occupied slot is rejected as already in effect.
It does not silently replace or extend the current charge. Each authored charge
multiplier is authoritative and multiplies resolved damage outside the square
root portion of the standard damage formula. The standard examples may author
`2.5`, but the framework does not hide a global charge multiplier.

A matching charge produces an immutable participation receipt when its damage
modifier is resolved. The exact retained runtime charge represented by that
receipt is consumed once after every eligible target and hit in the action has
received the multiplier. A miss, Null, Repel, or Absorb still consumes it
because participation occurred before that outcome was known. Assessment
rejection, target cancellation, or any other path that does not resolve a
charged modifier consumes nothing. Duration remains an expiry fallback.

Participation is not inferred from damage category at action end. A charge
granted after an uncharged damage attempt remains available. If a participating
slot is cleared and replaced by the same kind later in the action, that
replacement is a different runtime charge and remains unless it subsequently
participates itself.

Under the split policy, a mixed Physical and magical action behaves component
by component:

- a Physical charge boosts and consumes only the Physical portion;
- a Magical charge boosts and consumes only the magical portion;
- when both exist, each portion is boosted and both charges are consumed; and
- a missing category leaves only that category unboosted.

Under the unified policy, one General charge boosts every eligible damage
effect in the accepted action and is consumed once after the complete action.

This whole-action scope is an intentional Convergence rule. External reference
implementations have used first-hit-only behavior for some multi-hit actions;
that quirk is not adopted as the supplied default because it conflicts with the
approved host-neutral action boundary.

Retained charge state records its policy identity. A save or runtime snapshot
created under one charge policy cannot be restored under an incompatible one
without an explicit migration.

### Instant defeat

The supplied instant-defeat policy begins with the authored chance. It does not
add Luck or perform a second ordinary hit roll. Channel resistance then applies
these configurable defaults:

| Resistance | Multiplier |
|---|---:|
| Vulnerable | `1.5` |
| Normal | `1.0` |
| Resistant | `0.5` |
| Immune | `0.0` |

The result is clamped to the configured probability range, `0..100` by default,
and rolled once. An explicit bypass ignores the resistance multiplier but still
rolls the authored chance. The supplied `Immune` multiplier is zero, so Immune
blocks the effect without a roll. A replacement configuration may choose a
different non-negative Immune multiplier, in which case that selected policy is
authoritative. Null, Repel, and Absorb are elemental damage affinities, not
instant-defeat resistance states; they are never inferred for this channel.

Example: an authored chance of `40` resolves to `60`, `40`, `20`, and `0`
against Vulnerable, Normal, Resistant, and Immune respectively.

Failure to win an instant-defeat or ailment roll is a typed no-effect result,
not an evaded damage hit. It therefore does not automatically receive a miss
penalty from the supplied turn-economy mapper.

### Per-hit evidence and action outcomes

Combat resolution emits immutable evidence for every attempted hit, including:

- effect index and hit index;
- source action and target runtime IDs;
- authored and final hit chance;
- hit or evasion result;
- critical eligibility, chance, and result;
- resolved affinity;
- resolved damage; and
- relevant charge category and multiplier.

The staged runtime applies landed hits sequentially, so defeat prevention occurs
at the hit that would defeat the target. The outer action transaction remains
atomic: rejected execution publishes none of those staged mutations.

Turn economy is derived separately. Repeated hits against one target count as
one target outcome: if at least one hit lands, that target was hit; only an
all-hit evasion counts as an evasion. Per-hit facts remain available for
animation and custom policies.

The supplied Action Token aggregation is:

- ordinary landed action: normal cost;
- Weak or Critical with no conflicting miss: benefit;
- actual evasion or Null: two-token penalty;
- Repel or Absorb: phase termination;
- mixed Critical and evasion: normal cost;
- landed hits plus Critical and no evasion: Critical benefit;
- otherwise the policy's strongest applicable target outcome; and
- repeated misses do not stack additional penalties.

Passing remains owned by the Action Token economy itself: consume an existing
partial token first; otherwise convert one full token to one partial token.

### Action source and offensive items

**Confirmed by the project owner on 20 July 2026:** action-outcome aggregation
is source-aware and policy-owned.

The supplied standard outcome policy derives Skill and basic-attack turn
results from their typed effects. Its default for Item actions is one normal
turn regardless of Weak, Critical, Miss, Null, Repel, or Absorb effect facts.
Those effect facts remain available for presentation and replacement policies;
the normal item cost does not rewrite or hide the effect result.

A developer may select an effect-driven item configuration or provide another
action-outcome policy. This distinction belongs to policy composition rather
than `BattleActionExecutor`, allowing a future bonus-action economy or another
combat model to interpret offensive items without inheriting Action Token
assumptions.

## Alternatives Rejected

- Retaining one sealed `ProductionCombatRuleset` as every authored policy
  authority was rejected because it makes extension claims misleading.
- Keeping Luck in several hidden formulas was rejected because it obscures
  authored percentages and makes the stat's role impossible to explain.
- Treating authored critical chance as a minimum was rejected because the
  schema name communicates an exact chance.
- Keeping forced `5..99`, `2..40`, or `5..95` standard bounds was rejected
  because they invalidate explicit zero and one-hundred values.
- Consuming charge per hit was rejected because multi-hit and multi-target
  actions would produce inconsistent action-level behavior.
- Treating every failed effect roll as an evasion was rejected because it
  conflates accuracy with resistance and effect probability.

## Consequences

- The damage ruleset binding contract becomes a neutral combat-policy
  aggregate, while the supplied factory still composes standard policies.
- Charge state gains policy identity and the runtime save contract advances.
- Basic-attack critical metadata advances the clean content schema and active
  clean packs together.
- Inert default hit and instant-defeat values are removed because content must
  author both values explicitly.
- Existing tests that assert hidden Luck bonuses, forced chance floors, or
  persistent charges are replaced with assertions for the approved behavior.
- Reward and shop uses of Luck are unaffected.
- Hosts receive sufficient typed facts to present combat without parsing debug
  messages or rerunning random resolution.

## Implementation Result

Order 2 implements the confirmed policy family. Convergence supplies Disabled,
Split, and Unified charge policies; the standard authored combat factory
selects all three through `chargePolicy` and defaults to Split, while a host may
still select another implementation through a custom factory or direct
composition.
The family also includes explicit hit/evasion, separate critical eligibility
and chance, resistance-aware instant defeat,
immutable per-hit evidence, sequential staged hit application, and replaceable
action-outcome aggregation. Authored ruleset binding returns a neutral coherent
`CombatExecutionPolicySet`; the hit, critical, and instant-defeat authorities it
advertises are the exact authorities its executors call.

Content is currently schema v10 and active packs are version `0.10.0`. Weapon
basic attacks must declare critical behavior. One authored damage effect is
limited to `1..1024` hits; the supplied standard policy defaults to a stricter
`64`-hit execution ceiling. Authored probabilities are inclusive `0..100` and
are rejected before execution, while only policy-derived chances may clamp.
Save contract v18 retains charge policy identity and rejects restoration under
incompatible charge semantics.

The final source-first review corrected active schema validation, normalized
host-random boundaries, combat-policy composition integrity, and stale copied
contract assets. Its evidence is recorded in the
[Order 2 Completion Review](../reviews/combat-resolution-order-2-completion-review-2026-07-19.md).
The three documentation audiences are owner-confirmed and reviewed. The later
closure-safety correction cycle is governed by the
[Order 2 Closure Corrections Roadmap](../roadmap/combat-resolution-order-2-closure-corrections-roadmap.md).

## Evidence And References

Implementation and verification are governed by the
[Order 2 Combat Resolution Roadmap](../roadmap/combat-resolution-order-2-roadmap.md).
The discrepancy that prompted this decision remains recorded in the
[Order 2 Source Review](../reviews/combat-resolution-order-2-source-review-2026-07-19.md).
Current audience documentation is:

- [Combat, Defenses, And Turn Economy](../mechanics/combat-defenses-and-turns.md);
- [Combat Resolution Policies](../developer-guide/combat-resolution-policies.md); and
- [Combat Resolution Pipeline](../technical/combat-resolution-pipeline.md).

External formula guides were used as comparative design evidence, not copied as
source or treated as Convergence authority:

- [community combat formula guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2503470293);
- [community charge-state reference](https://megatenwiki.com/wiki/Focus);
- [community critical-hit reference](https://megatenwiki.com/wiki/Critical_Hit); and
- [independent formula analysis](https://zombero.wordpress.com/2017/02/28/whats-in-a-formula/).

The approved rules in this record are authoritative even where they differ from
those references.
