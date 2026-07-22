# Order 2 Final Charge Closure Review

**Review date:** 22 July 2026

**Reviewed revision:** `de4cae7`

**Scope:** current supplied charge policies, damage execution, nested ordered
effects, staged actor transactions, public custom effect-executor composition,
authored policy binding, retained-state validation/restoration, active audience
documentation, and executable capability evidence.

## Method

This was a fresh current-source trace. Earlier reports supplied neither the
verdict nor implementation proof. The review followed a charge from retained
actor state through:

1. application and duplicate-slot rejection;
2. `ResolveDamageModifier` and its exact internal source receipt;
3. damage-policy input and every miss or affinity outcome;
4. `EffectExecutionResult.ParticipatingCharge` publication;
5. nested outer-action receipt aggregation;
6. identity-based completion after all effects and targets;
7. staged commit or rollback;
8. authored Disabled, Split, and Unified selection; and
9. save validation and aggregate restoration.

The public custom-executor path was exercised separately from the supplied
damage executor. Audience documents were compared with those source paths and
their focused tests.

## Findings

No unresolved realistic, reachable defect was found in the reviewed charge
scope after O2-R40.

## Verified Invariants

- Disabled retains no slots, rejects grants, and resolves neutral damage.
- Split owns independent Physical and Magical slots; Unified owns one General
  slot.
- Omitted `chargePolicy` selects Split, while invalid authored values fail
  binding without fallback.
- A charge is represented by the exact modifier issued while resolving damage.
- Multi-hit and multi-target damage reuse that receipt and consume its charge
  once at outer action completion.
- Miss, Null, Repel, and Absorb consume a charge that participated before the
  outcome was known.
- Uncharged damage followed by a grant keeps the later grant.
- Clearing a participating charge and granting a same-kind replacement keeps
  the replacement because it is a different runtime state object.
- Nested passive execution joins the same outer action scope.
- A source-less charged modifier fabricated by a custom damage executor is
  rejected before completion mutation. Skill execution reports a typed
  `ExecutionFailed` diagnostic and discards staged actor state.
- Valid supplied receipts still consume normally after the malformed-receipt
  guard.
- Retained charge state is validated against the matching explicit policy
  during save validation and aggregate restoration.

## Extension Boundary

`ChargePolicyServiceBase` now accepts only charged receipts issued by its own
`ResolveDamageModifier` implementation. This protects hosts that replace a
damage executor while retaining a supplied charge policy.

A host implementing `IChargePolicyService` directly remains the authority for
that replacement policy's modifier and completion semantics. Convergence does
not inspect arbitrary custom arithmetic or pretend an untrusted plugin
boundary exists here. Such policy implementations are trusted composition,
not player or remote input.

## Documentation Review

The mechanics, developer, technical, decision, ruleset, and public API records
agree with current source on:

- explicit Disabled, Split, and Unified composition;
- exact participation rather than end-of-action element inference;
- defensive-outcome consumption;
- later-grant and same-kind-replacement preservation;
- custom executor receipt obligations and malformed-receipt rejection; and
- retained policy ownership in saves.

No audience-level design ambiguity remains in the corrected scope.

## Verification

- focused charge, active-skill, ruleset, persistence, capability, and
  documentation tests: 237 passed;
- complete solution: 1,472 passed, 0 failed, 0 skipped
  - Framework: 1,292;
  - DemoHost: 173;
  - ContentValidator: 7;
- strict nonincremental Release build: 0 warnings, 0 errors;
- active content validator: 6 packs, 36 documents, and 98 definitions passed;
- clean battle demo: player-team victory, exit 0;
- clean field demo: shared field effects completed, exit 0;
- clean save demo: save v11 validated and restored, exit 0;
- Training Annex demo: victory, reward, and valid save, exit 0;
- scripted Training Annex play: covered by the passing DemoHost tests;
- formatting verification and `git diff --check`: clean; and
- refined Framework host, legacy, and filesystem reference search: no matches;
- active boundary, documentation-link, schema, API, and Godot contract gates:
  passed through the complete solution.

## Verdict

Order 2 is ready to close. `combat_resolution` may return to `complete`, with
no known gap in the reviewed charge correction scope. Order 3 may begin under
the documentation completion roadmap.
