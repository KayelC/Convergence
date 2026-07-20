# Combat Resolution Order 2 Independent Review

## Review Status

**Date:** 20 July 2026  
**Revision reviewed:** `7a06320` (`docs: complete combat resolution order 2`)  
**Disposition:** reopened for focused corrections; owner decision confirmed

This review was performed from the current source, tests, schemas, and active
content. Earlier Order 2 reports were used only to identify the claimed scope;
they were not treated as proof that the implementation was correct.

The review found no security vulnerability and no evidence of corrupted active
content or broken standard-host execution. It did find three medium
implementation defects, one low public-boundary defect, and one unresolved
source-kind design decision. Order 2 should therefore not remain formally
closed until the confirmed findings are corrected and the owner decision is
recorded.

## Scope And Method

The review traced these paths directly:

- authored damage, critical, hit-count, charge, and ruleset records;
- schema v5, strict DTO mapping, semantic validation, and catalog binding;
- standard and replaceable hit, critical, instant-defeat, charge, and outcome
  policies;
- skill, item, basic-attack, ordered-effect, and per-hit execution;
- Action Token outcome aggregation and encounter consumption;
- staged actor mutation, charge consumption, save validation, and restoration;
- DemoHost and Godot policy composition;
- every `IRandomSource` call in active Framework source; and
- focused and full automated-test evidence.

A reportable defect had to identify an intended invariant, a realistic path,
a concrete consequence, and source evidence. Pure product alternatives and
impossible-domain hardening are separated from confirmed defects.

## External Review Reconciliation

The third-party report is **correct that two negotiation selections fail to
validate host-supplied integer random values**:

- question selection in
  [`BattleNegotiationAndRewards.cs`](../../src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs#L435);
- familiar-dialogue selection in the same file at line 509.

Its impact description needs two corrections:

1. `ArgumentOutOfRangeException` is not uncatchable. It unwinds the negotiation
   call and can terminate a host that does not catch it, but it is still an
   ordinary exception. The framework currently fails to translate it into its
   intended clear random-contract failure.
2. The report is incomplete. Eleven additional framework-owned integer draws
   also trust a contract-violating host source. Some throw, while others
   silently force or suppress gameplay outcomes. The issue is therefore a
   shared random-boundary gap, not only two unsafe list indexes.

This is a robustness and crash-safety problem, not a traditional security
vulnerability. It requires a buggy or deliberately contract-violating host
implementation. It nevertheless aligns with Convergence's vision because
`IRandomSource` is a public host boundary and the active technical contract
explicitly requires each authoritative consumer to reject out-of-range output.

## Findings

### M1. Host-random integer range validation remains incomplete

**Invariant:** `IRandomSource.NextInt32(min, max)` promises a value in the
half-open interval `[min, max)`. Framework-owned consumers must reject a host
violation at the authority boundary instead of indexing, mutating, or choosing
an outcome with that value.

**Realistic path:** a Godot adapter or third-party deterministic source uses an
inclusive upper bound, returns a negative sentinel, or contains an off-by-one
error.

**Consequence:** depending on the consumer, execution can throw an incidental
index exception, silently choose the first or last demand, force or suppress an
ailment restriction, produce unsupported growth values, or force/suppress a
fusion accident or mutation direction.

**Source evidence:** unchecked framework-owned draws remain at:

- [`BattleNegotiationAndRewards.cs:435`](../../src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs#L435),
  question index;
- [`BattleNegotiationAndRewards.cs:509`](../../src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs#L509),
  familiar-dialogue index;
- [`BattleNegotiationAndRewards.cs:786`](../../src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs#L786),
  authored demand weight roll;
- [`BattleStatusLifecycle.cs:1018`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs#L1018)
  and line 1031, turn-restriction and chance rolls;
- [`ProgressionPolicies.cs:624`](../../src/Convergence.Framework/Runtime/ProgressionPolicies.cs#L624),
  random stat selection, plus HP/SP growth at lines 641-642;
- [`FusionStrategyPolicies.cs:179`](../../src/Convergence.Framework/Fusion/FusionStrategyPolicies.cs#L179),
  with further accident/mutation draws at lines 217, 251, and 258; and
- [`FusionRuntimeServices.cs:1141`](../../src/Convergence.Framework/Fusion/FusionRuntimeServices.cs#L1141),
  accident-inheritance random ordering.

The combat-critical unit-decimal consumers and variable hit-count selection do
validate correctly. The defect is the incomplete cross-framework application
of the same boundary rule.

**Required correction:** centralize validated unit and integer draws in one
internal hosting-boundary helper or validating source wrapper, route every
supplied Framework consumer through it, and add focused lower-bound and
exclusive-upper-bound failure tests for each affected subsystem. Custom policy
behavior remains host/developer-owned, but Framework-supplied policies must not
silently trust invalid values.

### M2. The supplied unified charge policy cannot be authored in schema v5

**Invariant:** a supplied policy advertised as usable by developers must be
constructible through the clean content contract, not only through direct C#
test objects.

**Realistic path:** a developer selects `UnifiedChargePolicy` and authors a
`grant_charge` effect for its required `General` charge state.

**Consequence:** schema validation rejects the only charge kind accepted by the
unified policy. Authoring `physical` or `magical` passes schema validation but
is then rejected by `UnifiedChargePolicy`. The supplied policy therefore has no
valid JSON authoring path.

**Source evidence:**

- [`shared.schema.json:440`](../../schemas/content/v5/shared.schema.json#L440)
  permits only `physical` and `magical`;
- [`ChargePolicies.cs:283`](../../src/Convergence.Framework/Execution/ChargePolicies.cs#L283)
  defines `UnifiedChargePolicy`;
- [`ChargePolicies.cs:291`](../../src/Convergence.Framework/Execution/ChargePolicies.cs#L291)
  accepts only `ChargeKind.General`; and
- current unified-policy tests construct `ChargeKind.General` directly, so
  they do not exercise schema, deserialization, validation, and execution as
  one path.

**Required correction:** add the neutral wire value for `General` to the
schema, validate defined charge kinds semantically, and add a schema-to-runtime
integration test that authors, loads, applies, consumes, saves, and restores a
unified charge under a custom registered combat composition.

### M3. A critical on a skipped later hit can grant an Action Token benefit

**Invariant:** only a hit actually committed before the target is defeated
should contribute Critical to the action-level outcome. Immutable evidence may
record later pre-resolved attempts, but unapplied attempts must not alter turn
economy.

**Realistic path:** a two-hit action targets an actor with one HP. The first
non-critical hit defeats it. A later pre-resolved hit is critical.

**Consequence:** the later hit is correctly skipped during sequential mutation,
but the action still reports Critical and can grant the player an Action Token
benefit that no committed critical hit earned.

**Source evidence:**

- [`EffectExecutors.cs:171`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L171)
  calculates one `critical` flag from every pre-resolved hit;
- [`EffectExecutors.cs:227`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L227)
  skips later hits once the target is defeated; and
- [`EffectExecutors.cs:259`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L259)
  still uses the earlier aggregate flag for the action outcome.

Existing tests prove that later hits receive no resource mutation after
defeat, but do not make such a later hit critical.

**Required correction:** derive Critical from the hits actually processed by
the sequential mutation loop, while retaining ordered evidence for skipped
hits. Add a regression with a defeating first hit and critical second hit, plus
a control proving a committed critical still grants the configured benefit.

### L1. Some direct public combat inputs accept undefined enum values

**Invariant:** public typed combat boundaries must either reject undefined enum
values or document a deliberate fallback. They must not reinterpret undefined
values according to whichever switch branch happens to run.

**Realistic path:** a custom host constructs public combat requests directly
from its own data or serializer and casts an integer that is not a defined enum
member.

**Consequence:** an undefined `HitDistribution` is treated like Uniform, an
undefined ailment `ResistanceLevel` is treated like Normal, and an undefined
damage element can survive a resolution whose hits all miss. This produces
silent policy behavior instead of a stable rejection.

**Source evidence:**

- [`ProductionCombatRuleset.cs:913`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L913)
  distinguishes only Fixed from every other hit distribution;
- [`SkillSystemContentValidator.cs:1758`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs#L1758)
  has the same programmatic-content gap; and
- [`ProductionCombatRuleset.cs:852`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L852)
  defaults an unrecognized ailment resistance to the Normal multiplier.

Strict JSON converters already protect ordinary authored content, so this is a
low public-API consistency issue rather than an active-pack failure.

**Required correction:** reject undefined damage element, affinity,
hit-distribution, and resistance values at request or resolver boundaries and
add direct-public-API plus programmatic-content validation tests.

## Owner Decision

### D1. Offensive item outcomes are policy-owned

**Confirmed by the project owner on 20 July 2026.**

The clean item schema permits every shared typed effect, including damage.
`ItemExecutor` correctly executes those effects and can report Miss, Weak,
Critical, Null, Repel, or Absorb. However,
[`BattleActionExecutor.cs:994`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L994)
hardcodes every non-escape item action to normal turn consumption and never
calls `IActionOutcomeAggregationPolicy`.

Therefore an offensive item currently:

- receives no Weak or Critical benefit;
- receives no Miss or Null penalty; and
- does not terminate the phase after Repel or Absorb, despite its item
  execution being marked interrupted.

The older typed-action documentation explicitly describes items as having a
command-specific normal turn contract, while the confirmed Order 2 decision
describes action-level damage aggregation without clearly excluding items.
Those two authorities are ambiguous rather than safely reconcilable.

Source-kind behavior will be policy-owned. The supplied default makes every
non-escape item spend one normal turn, regardless of typed damage outcome. A
developer may select an effect-driven item option or supply another outcome
policy without modifying `BattleActionExecutor`. Typed item effect facts remain
unchanged and available to presentation and custom policies.

## Verified Healthy Areas

The review found the following implementation areas aligned with the confirmed
Order 2 design:

- hit chance uses authored accuracy, attacker Agility, target Agility, typed
  modifiers, configured bounds, and no hidden Luck;
- critical eligibility and critical chance are separate and replaceable;
- the supplied default uses exact authored physical critical chance, while the
  optional all-damage eligibility and accuracy-scaled chance policies remain
  explicit alternatives;
- instant defeat uses the authored chance, explicit resistance multipliers,
  bypass semantics, and no hidden Luck;
- split charge rejects duplicates, applies authored multipliers, and consumes
  each matching category once after the complete action;
- action conditions that skip damage do not consume the corresponding charge;
- rejected staged execution commits neither actor state nor charge removal;
- per-hit result and evidence collections are defensive immutable snapshots;
- multi-hit resource mutation and defeat-prevention dispatch are sequential;
- skill and basic-attack Action Token outcomes use the selected aggregation
  policy;
- combat policy composition exposes the same hit, critical, and instant-defeat
  authorities that its executors call;
- retained charge state records policy identity and validates/restores through
  a matching resolver; and
- active schema-v5 packs all pass schema, deserialization, semantic,
  dependency, registration, and catalog validation.

## Verification Results

Commands run against `7a06320`:

- focused combat/action/ruleset/persistence suite: **273 passed, 0 failed,
  0 skipped**;
- full solution: **1,302 passed, 0 failed, 0 skipped**
  (`1,122` Framework, `173` DemoHost, `7` ContentValidator);
- strict nonincremental Release build: **0 warnings, 0 errors**;
- `dotnet format --verify-no-changes`: passed; and
- active content validation: **6 packs, 36 documents, 98 qualified
  definitions**, all checks passed.

The green suite establishes a strong baseline but does not invalidate the
findings: the missing cases are not represented by current tests.

## Recommended Correction Order

1. Correct the shared host-random boundary and add subsystem-wide regressions.
2. Complete the unified-charge authoring contract and integration test.
3. Make action Critical depend on committed sequential hits.
4. Close the direct public enum boundaries.
5. Record the offensive-item decision and implement its policy-owned form.
6. Rerun the full quality gate, then update the Order 2 completion review and
   roadmap from "complete" to a new verified post-correction state.

Until those steps are complete, the source remains usable and the supplied
standard demos remain stable, but Order 2 should be described as
**implemented with focused corrections pending**, not fully closed.
