# Order 2 Charge Corrections Source Review

**Review date:** 22 July 2026

**Reviewed revision:** `75ad7f9`

**Scope:** current charge-policy source, ordered effect execution, staged actor
transactions, custom effect-executor composition, authored standard-ruleset
binding, save validation/restoration, focused tests, and all active charge
documentation.

## Method

This review reconstructed the corrected behavior from current source. Earlier
reports and roadmap completion labels were not used as implementation proof.
The trace followed:

1. `IChargePolicyService.ResolveDamageModifier` receipt creation;
2. `DamageEffectExecutor` publication through
   `EffectExecutionResult.ParticipatingCharge`;
3. nested `OrderedEffectExecutor` scope aggregation;
4. `ChargePolicyServiceBase.CompleteAction` matching and staged removal;
5. `RuntimeActorExecutionTransaction` commit/discard behavior;
6. disabled, split, and unified authored composition; and
7. charge-policy validation during aggregate save restoration.

The full 1,470-test solution and strict zero-warning build were green at the
reviewed revision. Those gates are useful evidence but do not override the
manual contract finding below.

## Finding

### O2-R39-M1: Fabricated participation receipts can consume standard charge state

**Severity:** medium contract-integrity defect for custom host integrations;
not a remote security vulnerability.

**Intended invariant:** a retained charge may be consumed only when the exact
modifier returned by the selected charge policy participated in damage
resolution.

**Reachable path:** `EffectExecutorRegistry` and
`IEffectExecutor<DamageEffectDefinition>` are public extension contracts. A
custom damage executor can return a successful `EffectExecutionResult` whose
`ParticipatingCharge` is a newly constructed
`ChargeDamageModifier(2m, ChargeKind.Physical)` rather than the modifier
returned by `IChargePolicyService.ResolveDamageModifier`.

`ChargePolicyServiceBase.CompleteAction` currently accepts that object because
its internal source state is null and the matching condition treats null as a
wildcard. If the actor retains a Physical charge, the outer action removes it
even though that retained runtime charge never supplied the modifier.

**Consequence:** a supported custom executor can silently consume charge state
that did not participate, contradicting the corrected mechanics and developer
contract. Staging prevents partial mutation on a later exception, but no
exception is currently raised on this path.

**Required correction:** the supplied policy base must accept charged
participation only when the modifier carries the internal source receipt added
by its own `ResolveDamageModifier` path. A fabricated or source-less charged
modifier is malformed integration input and must reject before mutation.
Custom `IChargePolicyService` implementations remain free to define and consume
their own modifiers because they own their own `CompleteAction` implementation.

**Required evidence:**

- direct completion rejects a source-less charged modifier without mutation;
- a public custom damage executor that fabricates participation produces a
  typed action rejection and preserves the live charge;
- a modifier returned by Split or Unified resolution still consumes normally;
- same-kind replacement, nested grant, miss, Null, Repel, Absorb, multi-hit,
  and multi-target regressions remain green; and
- public documentation continues to instruct custom executors to publish the
  actual returned modifier.

## Documentation Review

The mechanics, developer, technical, decision, ruleset, and public API
documents agree on the intended behavior:

- Disabled, Split, and Unified are explicit supplied compositions;
- `chargePolicy` defaults to `split` and rejects malformed values;
- participation is captured at modifier resolution;
- defensive outcomes consume a participating charge;
- later grants and same-kind replacements remain distinct; and
- custom damage executors must publish the returned modifier.

No documentation rewrite is required for the finding. The source must enforce
the already documented custom-executor obligation.

## Verdict

O2-R36 through O2-R38 materially correct the original ordering and optionality
findings, but Order 2 is not ready to close at revision `75ad7f9`. O2-R40 must
reject fabricated participation receipts, followed by an independent O2-R41
source and release-gate recheck.
