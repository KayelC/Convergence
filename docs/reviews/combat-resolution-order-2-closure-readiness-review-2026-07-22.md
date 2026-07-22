# Combat Resolution Order 2 Closure-Readiness Review

**Review date:** 22 July 2026

**Reviewed revision:** `7e98726`

**Verdict:** not ready for formal closure; one reachable gameplay-correctness
defect requires correction, and one lower-severity modularity claim requires an
explicit resolution

## Findings

### Medium: a charge granted after damage can be consumed by damage it never powered

**Intended invariant**

A matching charge is consumed only when it participates in a committed damage
attempt. Miss, Null, Repel, and Absorb still count because the retained charge
was resolved for that attempt. A newly granted charge that did not modify an
earlier damage effect belongs to a later action and must remain active.

**Reachable path**

1. An actor begins with no Physical charge.
2. A valid self-targeted typed action authors Physical damage followed by
   `grant_charge(physical)`, or an after-damage passive grants that charge to the
   acting actor.
3. `DamageEffectExecutor` resolves the damage with no charge modifier.
4. The later charge effect successfully stores a new Physical charge.
5. The outer action finalizer removes that new charge because it remembers only
   that Physical damage occurred, not whether a Physical charge participated.

**Source evidence**

- [`DamageEffectExecutor`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L102)
  asks the selected charge policy for the modifier before damage and carries the
  resulting charge kind into hit evidence.
- [`OrderedEffectExecutor`](../../src/Convergence.Framework/Execution/OrderedEffectExecutor.cs#L191)
  records every non-skipped damage element without checking whether the resolved
  modifier was charged.
- Its outer finalizer then passes those elements to
  [`CompleteAction`](../../src/Convergence.Framework/Execution/OrderedEffectExecutor.cs#L71).
- [`ChargePolicyServiceBase.CompleteAction`](../../src/Convergence.Framework/Execution/ChargePolicies.cs#L380)
  maps each remembered element to the actor's charge slots as they exist at the
  end of the action. It cannot distinguish an earlier participating charge from
  a later grant.
- [`GrantChargeEffectExecutor`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L655)
  applies the new state immediately to the staged target, so it is present when
  finalization runs.

**Consequence**

The action succeeds and commits, but the newly authored charge silently
disappears. The same ordering also makes an after-hit passive that prepares the
actor's next attack ineffective. This contradicts the player-facing rule that a
matching attack consumes the charge it used in
[`combat-defenses-and-turns.md`](../mechanics/combat-defenses-and-turns.md#guard-charge-shields-overrides-and-break).

**Coverage gap**

[`ChargePolicyTests`](../../tests/Convergence.Framework.Tests/Runtime/ChargePolicyTests.cs)
cover pre-existing charge use, mixed damage, shared contact, multi-target use,
misses, defensive affinities, and rejection rollback. They do not cover damage
followed by a new charge grant or a nested after-damage charge grant.

**Required correction**

Track the charge receipt that actually participated in damage resolution, not
only the damage element. Complete-action consumption should remove only those
participating slots. Add regressions proving:

- damage then grant retains the new charge when no prior charge participated;
- grant then damage applies and consumes that grant;
- a pre-existing charge still consumes on hit, miss, Null, Repel, and Absorb;
- nested after-damage grants are not consumed by the triggering attack; and
- rejected or rolled-back actions publish neither charge grants nor removals.

### Low: the supplied composition cannot actually omit the charge module

**Intended invariant**

Convergence is modular. The active developer guide says a game may omit charges
rather than carrying an unused gameplay feature.

**Current contract**

- [`CombatExecutionPolicySet`](../../src/Convergence.Framework/Execution/ExecutionPolicies.cs#L462)
  requires a non-null `IChargePolicyService`.
- [`BattleExecutionServices`](../../src/Convergence.Framework/Execution/ExecutionPolicies.cs#L517)
  also requires one and every damage action consults it.
- The supplied standard factory always installs
  [`SplitChargePolicy`](../../src/Convergence.Framework/Runtime/RuntimeRulesetPolicyFactories.cs#L350).
- No supplied disabled or no-charge implementation exists.
- [`combat-resolution-policies.md`](../developer-guide/combat-resolution-policies.md#configuration-ownership)
  nevertheless says a developer may "omit charges."

**Consequence**

A game can omit charge content and thereby avoid visible charge behavior, but it
cannot omit the charge policy from composition. It must carry `SplitChargePolicy`
or write a custom no-op implementation. This is not a crash or security issue;
it is a mismatch between the public modularity promise and the supported
composition surface.

**Required decision**

Choose one explicit contract before closure:

1. supply a typed disabled charge policy and let standard/custom composition
   select it; or
2. document that charge *content* is optional while the neutral charge-policy
   slot remains mandatory infrastructure.

The first option better matches the repository's stated modular-framework goal.

## Verified Implementation Behavior

The fresh source trace found the rest of the reviewed Order 2 surface coherent:

| Area | Current source behavior | Review result |
|---|---|---|
| Damage math | Physical uses Strength; other damage uses Magic; Vitality plus Defense supplies the denominator; variance, Critical, Guard, affinity, and charge are explicit multipliers with saturating boundary arithmetic. | aligned |
| Hit and evasion | Authored Accuracy and both Agility contributions are explicit; modifiers use the configured stack; final chance is bounded; zero and one hundred avoid random draws. | aligned |
| Criticals | Hit resolves first; eligibility and chance are separate; the supplied default is Physical-only; Guard blocks and rigid state guarantees only an otherwise eligible Critical. | aligned |
| Affinities | Shield, Break, override, passive/base precedence is typed; Almighty resolves Normal; Weak, Resist, Null, Repel, and Absorb retain distinct outcomes. | aligned |
| Instant defeat | Resistance and bypass are explicit, the supplied multipliers match the active mechanics, and at most one validated probability roll occurs. | aligned |
| Host randomness | All Framework-owned random calls pass through `RandomSourceContract`; invalid half-open integer or unit-decimal values fail before indexing or gameplay mutation. | aligned |
| Prepared execution | Assessments are executor-owned, single-use, target-stable, and revalidated against live actor, catalog, cost, and targeting state before mutation. | aligned |
| Atomicity | Skill, item, basic-attack, and direct-effect actor changes execute against staged runtime clones; item reservation decisions are completed before actor state is published. | aligned |
| Ordered effects | Dependencies, shared contact, life-state checks, failure policies, and complete-action aggregation operate on typed results rather than display text. | aligned |
| Turn outcome | The supplied policy aggregates once per action; mixed target evidence, Null penalties, Repel/Absorb termination, and default normal-cost items match the active mechanics. | aligned |
| Action Token | Weak/Critical, Miss/Null, Repel/Absorb, and pass precedence use the documented token transitions. | aligned |
| Ruleset binding | Unknown parameters reject binding; effective configuration is inspectable; damage, charge, ailment, chance, amount, and outcome authorities are returned as a neutral aggregate. | aligned except for optional-charge composition above |

## Documentation Cross-Examination

The mechanics, technical, and developer documents agree with current source for
damage order, hit and Critical policy, affinity resolution, probability ranges,
instant defeat, multi-hit evidence, target-level action aggregation, item
outcome configuration, and Action Token effects.

The charge documents expose the one disagreement precisely:

- the mechanics page says a matching attack consumes the charge it used;
- the technical page accurately describes the implementation as recording
  damage categories and removing matching end-of-action slots; and
- those statements diverge when a charge is granted after damage.

After the implementation is corrected, the charge state-machine diagram should
describe participating charge receipts rather than damage categories alone.
The optional-charge sentence must also be reconciled with the chosen composition
contract.

## Verification

Executed against revision `7e98726` before this review document was added:

- focused Order 2 slice: **279 passed**, 0 failed, 0 skipped;
- complete solution: **1,458 passed**, 0 failed, 0 skipped;
  - Framework: 1,278;
  - DemoHost: 173;
  - ContentValidator: 7;
- strict nonincremental Release build with warnings as errors: **0 warnings, 0 errors**;
- `dotnet format --verify-no-changes`: passed;
- content validation: **6 packs, 36 documents, 98 definitions**;
- clean battle, field, save, and Training Annex demos: exited 0.

The green suite demonstrates broad regression health. It does not disprove the
charge-ordering finding because no current test authors that sequence.

## Security And Residual Boundaries

No realistic external security vulnerability was found in the reviewed Order 2
surface. Convergence is an in-process rules library whose host-supplied policies,
handlers, random source, and inventory ports are trusted extension code. The
Framework validates returned domains and stages actor mutations, but it cannot
roll back unrelated filesystem, scene, network, or service side effects made by
a host callback. That is a documented ownership boundary, not a hidden combat
defect.

## Closure Recommendation

Do not mark Order 2 formally closed yet. Correct and regression-test the
participating-charge consumption path, resolve the optional-charge composition
claim, reconcile the charge documentation, then perform one focused source
recheck of those changes. No wider rewrite of combat resolution is indicated by
this review.
