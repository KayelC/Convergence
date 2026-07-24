# Status And Passive Lifecycle Order 4 Independent Audit

**Review date:** 24 July 2026

**Reviewed revision:** `90b1fffb`

**Capability:** `status_and_passive_lifecycle`

**Verdict:** reopened; the core lifecycle rules are coherent, but three
reachable contract gaps and three documentation or design gaps remain before
Order 4 can close honestly

## Review Method

This audit did not use an earlier review conclusion, roadmap summary, or commit
message as proof. It traced the current implementation in this order:

1. ailment, passive, duration, lifetime, and removal definitions;
2. mutable actor status state and its public mutation boundaries;
3. ailment application, transition, turn-start, turn-end, clock, and cleanup
   execution;
4. passive target resolution, activation accounting, recursion control, and
   ordered effects;
5. encounter lifecycle adaptation and event mapping;
6. persistence validation, restoration, schema-v8 mapping, and active content;
7. focused tests and the complete solution; and
8. the mechanics, developer, and technical Order 4 documents.

The audit also used a temporary, noncommitted regression probe. It authored an
Instant shield in an owner-turn-end passive. The live shield expired at the end
of that outer effect scope, but the lifecycle result contained no matching
`StatusExpired` event. The probe was removed before this document was written.

## Result Summary

- **High findings:** 0
- **Medium findings:** 3
- **Low documentation findings:** 2
- **Open design decisions:** 1
- **Security vulnerabilities:** none identified

The result is not a rejection of the Order 4 architecture. Canonical ailment
application, status lifetime ownership, reserve-aware clocks, cleanup,
transactional actor mutation, persistence, and schema mapping are all in good
health. The remaining defects are concentrated at typed evidence and public
extension boundaries.

## Findings

### O4-R12-M1: Non-ailment dispels collapse exact removals into a count

**Severity:** Medium

**Invariant:** when a typed effect removes runtime status state, its immutable
result must identify each committed removal by status ID, state family, and
removal cause. A host should not have to diff mutable actor state or parse a
message to update status presentation.

**Reachable path:** `RemoveStatusEffectExecutor` asks
`RuntimeActorState.RemoveNonModifierStatuses` to remove charges, shields,
affinity Breaks, affinity overrides, or named other statuses. The actor method
returns only an integer count. The executor publishes that count in
`EffectExecutionResult.Value` and does not attach
`BattleStatusRemovalResult` or lifecycle events.

**Consequence:** an effect that removes several statuses commits the correct
state, but an event-driven host cannot determine which statuses disappeared.
The host can display a stale shield, charge, or affinity indicator unless it
performs an out-of-band actor snapshot diff. Ailment cures do not have this
problem because `RemoveAilmentEffectExecutor` emits one typed removal transition
for each ailment.

**Source evidence:**

- [`RuntimeActorState.RemoveNonModifierStatuses`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs#L920-L985)
  returns `before - after` after discarding exact removed IDs and state kinds;
- [`RemoveStatusEffectExecutor`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L758-L792)
  publishes only the aggregate count and stat-modifier transitions; and
- [`RemoveAilmentEffectExecutor`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L590-L623)
  demonstrates the existing typed per-removal pattern.

**Required correction:** make non-modifier status removal return an immutable
ordered collection of `BattleStatusRemovalResult`; attach matching lifecycle
events to the effect result; preserve protected-state behavior; and test every
supported status family, mixed removal, selected named state, and no-effect
rejection.

### O4-R12-M2: Lifecycle-triggered action-end events are discarded

**Severity:** Medium

**Invariant:** every mutation made by an outer ordered-effect scope must expose
the corresponding typed lifecycle evidence, including Instant expiration at
that scope's action boundary.

**Reachable path:** `OrderedEffectExecutor` correctly calls
`ProcessActionEnd` once for the outermost effect scope and appends those events
to `OrderedEffectExecution.LifecycleEvents`. Active skill and item paths retain
that collection. The passive dispatcher keeps only `Effects` and
`StopsAction`, while ailment trigger execution adds only effect-result events.
Both paths discard the executor's appended action-end events.

**Consequence:** a passive or ailment trigger can grant an Instant shield,
charge, affinity state, ailment, or other status. The state expires correctly,
but the enclosing lifecycle result reports the grant without the expiry. A
Godot or console host consuming events can therefore animate or display state
that no longer exists.

**Reproduced evidence:**

- an owner-turn-end passive granted an Instant magical shield;
- the actor had no shield after dispatch, proving the action boundary ran; and
- `BattleTurnEndLifecycleResult.Events` had no matching `StatusExpired` event.

**Source evidence:**

- [`OrderedEffectExecutor.Execute`](../../src/Convergence.Framework/Execution/OrderedEffectExecutor.cs#L40-L97)
  appends action-end lifecycle events at the outer scope;
- [`PassiveTriggerDispatcher.ExecuteEffects`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs#L954-L984)
  drops `OrderedEffectExecution.LifecycleEvents`; and
- [`BattleStatusLifecycleService.ExecuteAilmentTriggers`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs#L1578-L1638)
  likewise ignores that collection.

**Required correction:** propagate ordered-effect lifecycle events through
passive and ailment-trigger dispatch without duplicating effect-owned events.
Add battle-start, owner-turn-end, and ailment-trigger regressions that compare
the final actor state with the complete ordered event stream.

### O4-R12-M3: Passive dispatcher results are scalar-valid but not request-coherent

**Severity:** Medium

**Invariant:** a replacement `IPassiveTriggerDispatcher` may choose how to
evaluate passives, but accepted activation evidence must still describe the
dispatch request and the owner's loaded passive definitions.

**Reachable path:** `PassiveTriggerExecutionResult` now rejects undefined
outcomes, negative trigger indexes, invalid IDs, and null effect entries.
However, a custom dispatcher may return:

- a valid skill ID that the owner does not have enabled;
- a trigger index that does not exist for that skill;
- a valid event ID different from the requested event;
- a valid target ID outside the participant graph; or
- a non-executed outcome carrying effect results.

Those values pass the record constructors. Turn-end and battle-start lifecycle
then map the evidence and commit the surrounding staged actor graph.

**Consequence:** a defective host extension can commit actor mutation while
publishing a structurally valid event for the wrong skill, trigger, event, or
actor. Scene-instance lookup and passive UI can then disagree with the
committed Framework state.

**Source evidence:**

- [`PassiveTriggerExecutionResult`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs#L526-L682)
  validates scalar domains but has no request-aware validation;
- [`BattleStatusLifecycleService.ProcessTurnEndCore`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs#L1371-L1408)
  trusts the returned activations; and
- [`BattleStatusEncounterLifecyclePort.ProcessBattleStartAsync`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs#L37-L74)
  does the same for every encounter participant.

**Required correction:** add one canonical request-aware passive dispatch
validator and route all three ingress points through it: defeat prevention,
owner-turn-end, and battle start. Validate loaded skill, trigger index, event,
eligible participant target, and outcome/effect shape before any surrounding
transaction commits.

## Documentation Findings

### O4-R12-L1: The developer guide names a result property that does not exist

The [developer guide](../developer-guide/status-passive-lifecycle.md#L177-L179)
tells developers to inspect the diagnostics on
`BattleAilmentApplicationResult`. The
[public result](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs#L401-L432)
exposes `Status`, `GateDecision`, `Transition`, and `Events`; it has no
`Diagnostics` property.

**Consequence:** sample integration code written from the guide will not
compile, and developers may overlook that rejection detail currently lives in
typed gate, transition, and event payloads.

**Correction:** name only the actual public properties and explain where each
rejection reason is represented.

### O4-R12-L2: Action-boundary and event-mapper wording is too broad

The mechanics page describes Instant state as expiring at "the next action
boundary." The technical execution contract currently defines that boundary as
the end of the outermost ordered-effect scope, including a passive or ailment
trigger scope. That distinction is important: an Instant state granted by a
passive can affect later effects in that same trigger, but it does not survive
for the next player-selected command.

The technical lifecycle page also says
`BattleStatusLifecycleEventMapper` validates required payload combinations
without limiting that claim. The mapper enforces specialized passive and effect
payloads; generic status events are wrapped rather than comprehensively
validated.

**Correction:** define the exact Instant scope for all three audiences and
narrow the mapper statement to the payload combinations it actually checks.

## Open Design Decision

### O4-R12-D1: May an undeployed owner fire a battle-start passive?

The encounter runner supplies every participant to battle-start lifecycle.
[`BattleStatusEncounterLifecyclePort`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs#L50-L69)
dispatches once for every participant without checking deployment. Targeting
can include or exclude reserve *targets*, but neither
[`PassiveEventPolicy`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs#L447-L515)
nor the [trigger targeting definition](../../src/Convergence.Framework/Content/Passives.cs#L12-L75)
controls whether a reserve *owner* may initiate the event.

The current behavior is therefore:

- a reserve owner can fire a self battle-start passive; and
- a reserve owner can fire a party-wide battle-start passive at deployed
  allies.

This is deterministic but not documented, and it is not currently selectable
as a policy. The recommended framework direction is to add explicit owner
deployment eligibility to `PassiveEventPolicy`, with a supplied
`DeployedOnly` battle-start policy and an opt-in `AllParticipants` policy. The
project owner must confirm that default before implementation.

## Verified Strengths

The fresh trace found no unresolved defect in these supported areas:

- lifetime construction separates expiration from permitted removal causes;
- finite Instant, Turn, Phase, and Battle state must allow duration expiry;
- public live-state mutators reject invalid retained durations;
- ailment application is staged across actor, target, and participants;
- guard, resistance, chance, same-ailment refresh, exclusivity replacement, and
  replacement protection are explicit policy decisions;
- turn-start combines all active restrictions deterministically;
- owner-turn-end order is passive effects, ailment effects, recovery, then
  duration and stat-modifier advancement;
- reserve advancement uses an injected policy and never infers time from action
  count;
- cleanup uses typed departure reasons and removal permissions;
- passive execution is deterministic, recursion-bounded, activation-limited,
  and actor-state transactional;
- encounter lifecycle stages complete actor graphs before commit;
- runtime save v13 preserves status lifetimes, passive activation state, and
  policy-owned modifier state; and
- schema v8 and active content preserve authored lifetime/removal policy.

## Correction Roadmap

| Checkpoint | Work | Completion evidence |
|---|---|---|
| O4-R12 | Return typed non-ailment removal transitions and lifecycle events. | Focused effect and lifecycle tests for every status family. |
| O4-R13 | Preserve action-end lifecycle events from passive and ailment trigger scopes. | State/event parity tests for battle start and turn end. |
| O4-R14 | Validate passive dispatch results against their request and loaded definitions. | Malformed custom dispatcher tests at all three ingress points. |
| O4-R15 | Implement the owner-approved battle-start owner eligibility policy. | Deployed and reserve owner tests for both supplied policies. |
| O4-R16 | Correct all three audience documents and executable ledgers. | Documentation contract tests and owner confirmation. |
| O4-R17 | Re-read corrected source and documentation and run the complete release gate. | No unresolved realistic finding and all gates green. |

Each runtime checkpoint should be an isolated commit. Order 4 must remain
`partial` until O4-R17 completes.

## Correction Progress

The project owner approved `DeployedOnly` as the supplied battle-start owner
policy, with explicit `AllParticipants` opt-in, and directed this roadmap on
24 July 2026.

| Checkpoint | State | Evidence |
|---|---|---|
| O4-R12 | Implemented pending final review | `2982fd10`; typed transitions and events for every non-modifier status family |
| O4-R13 | Implemented pending final review | `2bb2cf7f`; outer-scope completion events retained through passive and ailment triggers |
| O4-R14 | Implemented pending final review | `e4fc6fd`; request-aware transactional validation at defeat, turn-end, and battle-start ingress |
| O4-R15 | Implemented pending final review | `97643c9`; deployed-only battle-start default and all-participants opt-in |
| O4-R16 | In progress in this documentation checkpoint | Three audience documents and executable ledgers reconciled |
| O4-R17 | Pending | Fresh source/documentation review and complete release gate |

## Verification Record

The unmodified reviewed revision passed:

- focused lifecycle, clock, passive, immutability, mapper, persistence, and
  schema coverage: 319 passed, 0 failed, 0 skipped;
- complete solution: 1,618 passed, 0 failed, 0 skipped;
  - Framework: 1,438;
  - DemoHost: 173;
  - ContentValidator: 7.

Passing tests do not invalidate the findings. The focused suite does not
currently assert exact non-ailment removal events, lifecycle-triggered
action-end evidence, request-coherent custom passive results, or reserve-owner
battle-start behavior.

## Closure Decision

Order 4 is not ready for formal closure at `90b1fffb`.

The implementation remains a strong foundation, and no security vulnerability
or broad state-authority failure was found. Closure requires the bounded O4-R12
through O4-R17 sequence above, followed by explicit project-owner confirmation
of O4-R12-D1.
