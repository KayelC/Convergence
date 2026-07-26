# Status And Passive Lifecycle Order 4 Fourth Independent Audit

Date: 26 July 2026

Reviewed revision: `8870aea3`

Verdict: **reopened for one low-severity validation defect and one documentation correction**

## Review Method

This audit treated current source, tests, schemas, content, and active audience
documents as the only implementation evidence. Earlier review reports and
roadmap summaries were not used to establish behavior. They were consulted only
after the source trace to identify which active progress records needed to be
reopened.

A concern qualified as a finding only when it had:

1. an invariant established by current contracts or confirmed active guidance;
2. a realistic path through a supported Framework boundary;
3. a concrete gameplay, state-integrity, integration, or documentation
   consequence; and
4. reproducible source or executable evidence.

## Scope Traced

The source review followed Order 4 behavior through:

- ailment definitions, exclusivity, application gates, resistance, chance,
  refresh, replacement, and removal profiles;
- active ailment, status, guard, shield, charge, affinity, stat-modifier, and
  passive-activation runtime state;
- turn-start exact-instance scheduling, restriction precedence, Guard clearing,
  forced actions, flee, and roster recall;
- turn-end passive dispatch, ailment triggers, recovery, duration advancement,
  and stat-modifier ticking;
- action, actor-turn, team-phase, round, and custom lifecycle clocks;
- reserve suspension and the optional reserve-aging policy;
- passive target freezing, activation limits, recursion control, replacement
  dispatch, and executed-activation evidence;
- encounter-owned lifecycle staging, cancellation, event sequencing, departure
  cleanup, and shared event-ID clocks;
- actor snapshot integrity, save validation, restore binding, and public result
  immutability; and
- the persistent staged, timed exclusive, and timed contribution modifier
  policies.

The primary implementation authorities were:

- [`AilmentDefinition.cs`](../../src/Convergence.Framework/Content/AilmentDefinition.cs)
- [`StatusLifetimes.cs`](../../src/Convergence.Framework/Content/StatusLifetimes.cs)
- [`BattleRuntimeState.cs`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs)
- [`BattleAilmentTransitions.cs`](../../src/Convergence.Framework/Execution/BattleAilmentTransitions.cs)
- [`BattleAilmentApplicationGates.cs`](../../src/Convergence.Framework/Execution/BattleAilmentApplicationGates.cs)
- [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs)
- [`BattleLifecycleClocks.cs`](../../src/Convergence.Framework/Execution/BattleLifecycleClocks.cs)
- [`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs)
- [`StatModifierExecution.cs`](../../src/Convergence.Framework/Execution/StatModifierExecution.cs)
- [`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs)
- [`RuntimeActorSnapshotIntegrity.cs`](../../src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs)
- [`RuntimePersistenceSnapshots.cs`](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs)
- [`SkillSystemContentValidator.cs`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs)

## Findings

### O4-L1: an undefined Companion flee outcome is accepted by programmatic content

Severity: **low**

Intended invariant:

- public programmatic definitions and host-supplied deserializers pass through
  the same semantic validator as built-in JSON content;
- enum-valued authored decisions must be one of the Framework's defined values;
  and
- malformed content must be rejected instead of silently selecting another
  gameplay outcome.

Reachable path:

1. A host supplies a `ChanceSkipOrFleeAilmentTurnBehaviorDefinition` through
   the public programmatic content path or a custom document deserializer.
2. The host value for `CompanionFleeOutcome` is an undefined enum value, such as
   `(CompanionFleeOutcome)2`.
3. `SkillSystemContentValidator.ValidateAilmentBehavior` validates both
   percentages and their combined maximum, but does not call its existing
   `RequireDefinedEnum` helper for `CompanionFleeOutcome`.
4. `BattleStatusLifecycle.ResolveFearOutcome` tests only for
   `RecallToRoster`; every other value follows the `FleeBattle` branch.

Concrete consequence:

- malformed programmatic content passes semantic validation and is silently
  interpreted as escape behavior;
- an eligible Companion can flee instead of being recalled, contrary to the
  authored decision; and
- built-in JSON is protected by schema and strict enum conversion, so the same
  logical definition receives different protection depending on the supported
  content-input path.

Reproduction:

```csharp
var behavior = new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(
    SkipChance: 0,
    FleeChance: 100,
    CompanionFleeOutcome: (CompanionFleeOutcome)2);
```

Embedding this behavior in an otherwise valid programmatic ailment produces no
`ShapeInvalid` diagnostic for `companionFleeOutcome`. Resolving its turn-start
restriction with roster recall available returns `FleeBattle`.

Required correction:

- validate `CompanionFleeOutcome` at the semantic content boundary;
- reject an undefined value at direct runtime execution as defense in depth;
- test the validator, direct lifecycle path, and both valid outcomes; and
- keep JSON schema and wire vocabulary unchanged.

This is not a security vulnerability and does not affect valid authored
content. It is a real supported-boundary correctness gap, not theoretical
hardening.

### O4-D1: two stat-modifier guides advertise obsolete save contract v10

Severity: **low documentation defect**

Current authority:

- `RuntimeSaveGameSnapshot.CurrentContractVersion` is `13`;
- the status lifecycle developer and technical pages correctly describe save
  contract v13; and
- the active terminology and product guidance also identify save v13.

Mismatch:

- [`stat-modifier-policies.md`](../developer-guide/stat-modifier-policies.md)
  says save contract v10 stores modifier state;
- [`stat-modifier-policy-runtime.md`](../technical/stat-modifier-policy-runtime.md)
  repeats v10 in prose and in its persistence diagram.

Consequence:

A host implementer reading the dedicated stat-modifier integration guide can
encode an obsolete aggregate version that current save validation rejects. The
modifier-state shape described by those pages is otherwise accurate.

Required correction:

- replace the three stale v10 labels with v13;
- recheck the surrounding persistence claims against current snapshot source;
  and
- rerun documentation links, synchronization guards, and audience review.

## Verified Strengths

No other concern met the finding threshold in the reviewed scope.

### Mutation and rollback

- Ailment application stages source, target, and participant graphs before
  committing.
- Turn-start, turn-end, cleanup, passive dispatch, and encounter lifecycle
  operations mutate staged state and expose no partial live commit on rejection,
  cancellation, or extension failure.
- Replacement passive dispatch must provide coherent executed-activation
  evidence before its mutations may commit.

### Scheduling and timing

- Turn-start uses an exact-instance ailment schedule, so refresh, replacement,
  and removal cannot execute a stale slot.
- Turn-end checks the scheduled ailment instance again before each trigger.
- The encounter lifecycle port owns one committed sequence stream per event ID.
  Cross-target owner-turn modifiers and shared phase events therefore observe
  coherent clock identity.
- Timed policies consume one observed occurrence, not the arithmetic distance
  between sequence numbers, and reject repeated or regressing boundaries.

### Passive lifecycle

- Eligibility is frozen before mutation for each event dispatch.
- Target sets are frozen per trigger and respect life state, team relation,
  deployment, and reserve inclusion.
- Activation limits, recursion protection, event/trigger identity, and
  replacement-dispatch receipts are enforced.
- Battle-start reset is staged with encounter startup and cannot leak through a
  cancelled startup transition.

### Persistence and restoration

- Active ailments retain authored lifetime and removal semantics.
- Passive activation keys must match every equipped passive and its authored
  trigger index/event.
- Ailment exclusivity is revalidated across restored state.
- Retained stat modifiers are restored through the explicitly selected policy.
- Runtime status collections and lifecycle result collections are immutable at
  their public boundaries.

### Documentation alignment

Apart from O4-D1, the three audience layers match current code on:

- application and exclusivity;
- turn-start and turn-end ordering;
- reserve suspension and optional reserve aging;
- removal causes and field/encounter persistence;
- passive targeting and activation evidence;
- combat-profile modifier composition;
- stat-modifier policy selection and timing; and
- event-keyed sequence authority.

## Verification

At reviewed revision `8870aea3`:

- focused Order 4 tests: **309 passed, 0 failed, 0 skipped**;
- full solution: **1,673 passed, 0 failed, 0 skipped**
  (`1,493` Framework, `7` ContentValidator, `173` DemoHost);
- nonincremental solution build: **0 warnings, 0 errors**;
- `dotnet format --verify-no-changes`: passed;
- `git diff --check`: passed; and
- the refined Framework forbidden-reference search found no active engine,
  console, filesystem, serializer, legacy adapter, or archive dependency. The
  intentional `InternalsVisibleTo` declaration for DemoHost tests is not a
  runtime dependency.

## Correction Roadmap

| Checkpoint | State | Required outcome |
|---|---|---|
| O4-R41 | `complete` | Record this source-first audit and reopen the capability honestly. |
| O4-R42 | `pending` | Reject undefined Companion flee outcomes in semantic validation and direct lifecycle execution, with focused regression tests. |
| O4-R43 | `pending` | Correct save-v13 guidance and independently reconcile all three Order 4 audience layers. |
| O4-R44 | `pending` | Re-read corrected source and documentation, run the complete release gate, and decide closure without using this report as proof. |

## Closure Decision

The implementation is healthy in its principal state-machine, rollback,
persistence, and encounter-integration paths. O4-L1 is narrow and affects only
malformed programmatic definitions; O4-D1 is documentation drift. Neither
justifies redesigning the lifecycle architecture.

Order 4 should nevertheless remain formally open until O4-R42 through O4-R44
are complete. Order 5 remains queued so the project does not carry a knowingly
inconsistent public content boundary or stale persistence instruction forward.
