# Status And Passive Lifecycle Order 4 R26 Correction Audit

## Review Identity

- Review date: 26 July 2026
- Reviewed revision: `367f6c63f4f587c000521413f52f314f5ede6c1d`
- Branch: `main`
- Scope: passive dispatch validation, passive event policy composition,
  ailment and passive persistence, actor restoration, tests, and active
  lifecycle documentation
- Method: current source, tests, schemas, and documentation were read directly.
  Earlier review conclusions were not used as implementation evidence.

## Verdict

O4-R26 did not close Order 4. The exact-instance ailment schedules and
encounter-owned departure cleanup added by O4-R23 and O4-R24 are sound, but the
fresh trace found four additional reachable correctness defects:

1. passive target eligibility is recomputed after passive effects mutate the
   staged actor graph;
2. a save may omit an equipped passive's disabled state and restore it enabled;
3. a save may restore multiple ailments from the same exclusivity group; and
4. the supplied defeat-prevention default silently overwrites an explicitly
   registered host policy.

These are gameplay, restore-parity, and extension-composition defects. They are
not security vulnerabilities, and no host dependency leak or general
transaction failure was found. Order 5 remains paused while the correction
sequence below is completed.

## Findings

### O4-R26-M1: Passive eligibility is validated after effects change it

**Severity:** Medium

**Intended invariant:** a passive activation may target exactly the actors that
were eligible when dispatch began. Result validation must not reinterpret that
eligibility after the passive mutates health, deployment, team, or other
targeting state.

**Reachable path:**

1. An enabled passive has an event trigger targeting an alive actor.
2. Its typed effects reduce that target to zero HP.
3. The inner dispatcher correctly selects and mutates the target.
4. `ValidatingPassiveTriggerDispatcher` validates the returned activation after
   dispatch.
5. Validation calls `PassiveTriggerTargetResolver.Resolve` against the already
   mutated staged actor graph.
6. The now-dead target is no longer considered eligible, so a legitimate
   activation is rejected and the enclosing lifecycle transaction rolls back.

The reverse path also exists for a dead-target passive that revives its target.
A replacement dispatcher could additionally make an initially ineligible
target eligible before reporting it, causing validation to accept evidence that
was invalid at dispatch start.

**Source evidence:**

- [`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs)
  captures live owner, participant, and event-target references in
  `PassiveDispatchContract.Capture`.
- The same contract calls `PassiveTriggerTargetResolver.Resolve` from
  `RequireValid`, after `_inner.Dispatch` has returned and may have mutated
  those actors.
- Typed passive effects deliberately execute against the staged participant
  graph, so life-state mutation is a supported path rather than an impossible
  custom-host condition.

**Consequence:** supported defeat or revival passives can fail solely because
their own successful effect changed target eligibility. Replacement dispatcher
validation can also observe a different eligibility set than the request that
it is meant to guard.

**Required correction:**

- capture eligible runtime IDs for every enabled trigger matching the requested
  event before dispatch;
- validate activation evidence against those immutable sets;
- retain participant membership, trigger, event, duplicate-evidence, and
  outcome-shape validation; and
- add regressions for legitimate life-state changes and for a replacement
  dispatcher that makes an initially ineligible target eligible.

### O4-R26-M2: Passive restore state does not require complete coverage

**Severity:** Medium

**Intended invariant:** restoration preserves the enabled or disabled state of
every equipped passive exactly. A save cannot silently replace an omitted state
with a constructor default.

**Reachable path:**

1. An actor has an equipped passive that was disabled before capture.
2. A malformed or incomplete save omits that passive from
   `passiveSkillStates`.
3. `RuntimeActorSnapshotIntegrity.ValidatePassives` checks duplicates and
   references to unloaded skills, but does not require one state for each
   loaded passive.
4. `RuntimeActorState.Restore` constructs every supplied passive enabled.
5. `BattlePassiveCollection.RestoreStates` applies only the entries present in
   the save.
6. Validation succeeds and the omitted passive is restored enabled.

**Source evidence:**

- [`RuntimeActorSnapshotIntegrity.cs`](../../src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs)
  compares supplied states to loaded passives in one direction only.
- [`BattleRuntimeState.cs`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs)
  constructs `BattlePassiveCollection` from passive definitions before
  restoring activation state.
- [`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs)
  defaults `BattlePassiveCollection.Add` to `enabled: true` and does not reject
  missing state entries during `RestoreStates`.

**Consequence:** an incomplete save can reactivate a passive and change battle
behavior after restoration even though aggregate validation reported the
snapshot as valid.

**Required correction:**

- require exactly one passive-state entry for every loaded equipped passive;
- expose a stable actor-integrity and save-validation diagnostic for missing
  state;
- reject the same malformed state through direct actor restoration; and
- cover enabled and disabled round trips plus omitted-state rejection.

### O4-R26-M3: Restore accepts mutually exclusive active ailments

**Severity:** Medium

**Intended invariant:** actor restoration cannot create an active ailment set
that live `ApplyAilment` would prevent. At most one ailment from a shared
exclusivity group may be active.

**Reachable path:**

1. Two catalog ailments share one `ExclusivityGroupId`.
2. A malformed save lists both ailments as active on one actor.
3. Snapshot integrity receives only the set of available ailment IDs and
   confirms both definitions exist.
4. `RestoreBattleStatus` inserts both definitions directly into the actor's
   ailment dictionary.
5. The snapshot validates and restores even though applying the second ailment
   through the live API would replace or reject the first.

**Source evidence:**

- [`RuntimeActorSnapshotIntegrity.cs`](../../src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs)
  accepts `availableAilmentIds` rather than definitions and therefore cannot
  compare exclusivity groups.
- [`BattleRuntimeState.cs`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs)
  restores active ailments directly, while its public `ApplyAilment` method
  enforces exclusivity and protected replacement.

**Consequence:** restoration can create impossible battle state and permit two
major or otherwise exclusive ailments to apply restrictions, modifiers, and
triggers together.

**Required correction:**

- validate against available `AilmentDefinition` values;
- reject two distinct active ailments sharing one valid exclusivity group;
- expose a stable actor-integrity and save-validation diagnostic;
- preserve independent nonexclusive ailments; and
- cover aggregate validation and direct actor restoration.

### O4-R26-M4: Supplied defeat-prevention policy overwrites host policy

**Severity:** Medium

**Intended invariant:** a host-authored passive event policy remains
authoritative. Framework defaults are installed only when a host has not
registered that event.

**Reachable path:**

1. A host registers a custom policy for `owner_would_be_defeated`, such as two
   activations per battle.
2. The host passes that registry into `BattleExecutionServices`.
3. The constructor unconditionally calls `Register` for the same event with the
   supplied one-activation default.
4. The host's explicit policy is silently replaced.

**Source evidence:**

- [`ExecutionPolicies.cs`](../../src/Convergence.Framework/Execution/ExecutionPolicies.cs)
  unconditionally registers `ActivationLimitPerBattle: 1`.
- [`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs)
  defines `Register` as replacement.
- [`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs)
  already demonstrates the intended composition rule by using
  `RegisterIfAbsent` for the supplied battle-start policy.

**Consequence:** a developer's explicit defeat-prevention configuration is
ignored without a diagnostic, contradicting Convergence's policy-first
extension model.

**Required correction:**

- install the one-use defeat-prevention policy only when the event is absent;
- retain the supplied one-use behavior for unconfigured registries; and
- prove an explicit host policy survives composition and controls activation
  limits.

## Confirmed Healthy Boundaries

The fresh trace also confirmed:

- turn-start and owner-turn-end ailment schedules capture exact active
  instances, skip removed or replaced instances, and defer additions;
- framework-owned defeat, flee, and roster-recall departures invoke typed
  cleanup through an atomic participant graph;
- departure cleanup is cancellable before mutation and faults through the typed
  encounter boundary;
- passive dispatch output still validates event, enabled skill, trigger index,
  target membership, duplicate evidence, and outcome shape;
- passive activation restore keys validate equipped skill, trigger index, and
  authored event;
- lifecycle result collections remain immutable;
- reserve aging and cleanup causes remain explicit policies; and
- Framework remains free of console, filesystem, serializer, Godot, and
  archived product dependencies.

## Correction Roadmap

| Checkpoint | Work | Completion evidence |
|---|---|---|
| O4-R27 | Freeze passive target eligibility before dispatch mutation. | Life-state mutation and initially-ineligible replacement-dispatch tests. |
| O4-R28 | Require exact passive-state coverage during restore. | Enabled/disabled round trips and aggregate/direct missing-state rejection tests. |
| O4-R29 | Reject restored ailment exclusivity conflicts. | Aggregate/direct conflict rejection and independent-ailment acceptance tests. |
| O4-R30 | Preserve explicit defeat-prevention event policies. | Explicit-host-policy and supplied-default activation-limit tests. |
| O4-R31 | Reconcile mechanics, developer, technical, roadmap, API, and matrix guidance. | Documentation tests and active link validation. |
| O4-R32 | Re-read corrected source and documentation independently and run the complete release gate. | New closure report with no unresolved reachable Order 4 defect. |

## Implementation Progress

| Checkpoint | State | Evidence |
|---|---|---|
| O4-R27 | Complete | Commit `6845e367` freezes trigger eligibility before passive dispatch mutation and covers valid defeat plus fabricated post-mutation eligibility. |
| O4-R28 | Complete | Commit `4c32a8ff` requires one enabled/disabled state for every equipped passive across aggregate validation, direct restore, fusion, and Compendium recall. |
| O4-R29 | Complete | Commit `9c3e4af6` rejects restored active ailments that share one exclusivity group while retaining independent ailments. |
| O4-R30 | Complete | Commit `a5699379` preserves explicit host defeat-prevention policies while retaining the supplied one-use default when absent. |
| O4-R31 | Complete | Mechanics, developer, technical, architecture, API, roadmap, and executable-matrix guidance now describe the corrected contracts while retaining pending-review status. |
| O4-R32 | Pending | Requires a fresh source/documentation trace and complete release gate. |

## Closure Decision

Order 4 remains `partial`. Its three documentation audience entries remain
`existing_unreviewed`. O4-R27 through O4-R32 extend the closure sequence.
Formal closure and Order 5 remain blocked until the fresh O4-R32 review passes.
