# Combat Resolution Order 2 Post-Correction Review

## Disposition

**Review date:** 20 July 2026  
**Source baseline:** `711ba83` plus documentation-only reconciliation  
**Result:** implementation complete; no remaining reachable Order 2 defect found  
**Documentation:** source-verified, pending project-owner confirmation

This review was performed from current Framework source, schemas, tests, active
content, DemoHost composition, and the Godot reference consumer. Earlier
reports supplied the scope to recheck but were not accepted as evidence that a
correction worked.

## Review Standard

A finding required all four of these:

1. a current contract or owner-confirmed invariant;
2. a realistic path through an active public or host boundary;
3. a concrete correctness, integrity, or maintenance consequence; and
4. reproducible current-source or test evidence.

Alternate game designs, impossible domain values, and work assigned to another
documentation order were not promoted into defects.

## Current Source Trace

### Host randomness

Every Framework-owned `NextUnitDecimal` and `NextInt32` call now occurs inside
[`RandomSourceContract.cs`](../../src/Convergence.Framework/Internal/RandomSourceContract.cs).
All supplied combat, negotiation, reward, lifecycle, progression, and fusion
consumers call that checked boundary. The architecture test scans active source
and fails if a new direct random draw is introduced elsewhere.

The helper enforces `[0, 1)` for unit decimals and `[minimum, maximum)` for
integers. Invalid host values fail explicitly before list indexing, state
mutation, or outcome selection. Deterministic draw order and guaranteed
zero/one-hundred behavior remain unchanged.

### Charge authoring and retention

Schema v5 accepts `physical`, `magical`, and `general` for `grant_charge`.
Programmatic semantic validation rejects undefined charge kinds. The supplied
split policy accepts Physical/Magical; the optional unified policy accepts
General. The integration path deserializes authored General charge, applies its
authored multiplier, consumes it once after a matching complete action, and
validates/restores its policy-tagged save state.

### Sequential hits and Critical authority

[`EffectExecutors.cs`](../../src/Convergence.Framework/Execution/EffectExecutors.cs)
retains evidence for every pre-resolved hit but sets the effect-level Critical
flag only when a critical hit reaches the sequential mutation loop before the
target is defeated. A later skipped attempt cannot grant a turn benefit. A
committed zero-damage critical still counts because it was a resolved attack,
not a skipped attempt.

### Combat vocabulary boundaries

Public combat requests reject undefined damage elements, affinities, hit
distributions, critical modes, and resistance values at their owning policy
boundaries. Programmatic content validation applies the same rule to typed
effects, entity defenses, targeting, modifiers, equipment attacks, and related
conditions. Strict JSON and direct C# construction therefore cannot silently
choose different fallback semantics.

### Source-aware action outcomes

[`ActionOutcomeAggregationPolicies.cs`](../../src/Convergence.Framework/Execution/ActionOutcomeAggregationPolicies.cs)
defines an immutable request containing `ActionOutcomeSourceKind` and a
defensive effect snapshot. Skills and basic attacks use effect-driven standard
aggregation. Non-escape items use Normal by default, clearing action-level
Critical reward and phase termination while preserving every effect fact.
`itemActionOutcomeBehavior: "effect_driven"` selects the standard effect-driven
item mapping through authored ruleset binding.

[`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs)
resolves the item outcome after staged effects but before inventory reservation
commit and actor-state commit. A thrown custom policy rolls back the reservation
and publishes no actor mutation. Existing custom policies implementing only the
original list-based method continue through the interface compatibility
dispatch.

## Findings

No High, Medium, or Low correctness finding remains in the reviewed Order 2
scope. In particular, the third-party report's two negotiation-index examples
were valid but represented only part of the random-boundary gap; the correction
now covers all supplied consumers rather than special-casing those two lists.

## Deliberate Boundaries

- A host-provided custom policy remains responsible for its own formula and
  external side effects.
- Inventory implementations are transactional ports; Framework validates their
  observable reservation contract but cannot inspect hidden host state.
- `Aggregate(effects)` remains effect-driven for source-unaware custom-policy
  compatibility. Canonical Framework execution uses the source-aware request.
- Escape items retain their explicit no-turn result instead of passing through
  ordinary item outcome pricing.
- Armor composition, lifecycle rules, replacement turn economies, and player
  presentation belong to later documentation capabilities, not Order 2.

## Verification

- focused outcome/action/ruleset tests: 107 passed before the complete gate;
- complete solution: 1,330 passed, 0 failed, 0 skipped (1,150 Framework, 173
  DemoHost, 7 ContentValidator);
- Framework coverage: 90.27% lines and 75.18% branches, above the 90% / 70%
  release thresholds;
- Framework and solution Release builds: 0 warnings, 0 errors;
- formatting and `git diff --check`: passed;
- active content: 6 packs, 36 documents, 98 qualified definitions passed
  schema, deserialization, semantic, dependency, registration, and catalog
  validation;
- DemoHost: all four noninteractive modes and scripted Training Annex play
  exited successfully; and
- Godot 4.7.1: the real local headless reference consumer emitted
  `CONVERGENCE_GODOT_SMOKE_OK`.

The code capability is ready to remain `complete`. The mechanics, developer,
and technical audience entries intentionally remain `existing_unreviewed`
until the project owner confirms that the final plain-language explanation
matches the intended design.
