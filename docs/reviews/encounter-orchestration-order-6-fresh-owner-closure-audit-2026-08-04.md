# Encounter Orchestration Order 6 Fresh Owner-Closure Audit

**Date:** 4 August 2026

**Capability:** `encounter_orchestration`

**Reviewed revision:** `1a926191`

**Review checkpoint:** O6-R14

**Result:** corrections required; Order 6 is reopened

**Post-audit correction status:** O6-R15 through O6-R19 implemented; O6-R20
found one bounded event-shape defect; O6-R21 through O6-R23 pending

## Purpose

This is a new source-first review performed before the project owner formally
closes Documentation Order 6. Earlier review conclusions and correction
summaries were not accepted as proof. The audit traced the current encounter
source, current tests, and then the current mechanics, developer, and technical
documents.

The audit distinguishes reachable framework defects from trusted host-port
limitations and hypothetical hardening. Four reachable implementation defects
and one documentation error remain. They are important enough that
`encounter_orchestration` must return to `partial` until corrected and reviewed
again.

## Scope And Method

The source trace covered:

- `BattleEncounterRunner` request validation, startup, scheduling, lifecycle
  transactions, command processing, economy transitions, reconciliation,
  completion, cancellation, fault finalization, and result snapshots;
- the team-phase and Agility scheduling policies and the bounded post-command
  extension;
- canonical encounter events and frozen-participant event ownership;
- `BattleStatusEncounterLifecyclePort` and lifecycle sequence checkpoints;
- `AutomatedBattleRunner`, its deterministic selector, and restricted-turn
  resolver; and
- the runtime action, revival, target, and skill-result contracts that the
  encounter layer consumes.

The test trace covered the encounter runner, both supplied schedulers,
post-command scheduling, event mapping, automated catalog battles, status
lifecycle, and current Godot contract coverage. The three audience documents
were read only after the source behavior had been established.

## Findings

### O6-R14-H1: Defeat bookkeeping cannot represent defeat, revival, and defeat again

**Invariant:** every distinct transition from living to defeated must receive
its configured defeat cleanup and one `ActorDefeated` event. Reconciliation may
run repeatedly while the actor remains defeated without duplicating either
operation.

**Reachable path:** an encounter has at least two actors on one team. One actor
is defeated, a teammate restores that actor through the supported typed
`ReviveEffectDefinition`, and the actor is defeated again before the encounter
ends.

**Current behavior:** the runner stores processed defeat cleanup and defeat
announcements in encounter-lifetime hash sets. An actor ID is added after the
first defeat and is never removed when the actor returns to life:

- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L845)
  creates the permanent sets;
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1022)
  suppresses cleanup when the actor ID is already present;
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1082)
  permanently records the processed ID; and
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2775)
  permanently suppresses later defeat announcements.

**Consequence:** the second defeat receives no defeat-specific lifecycle
cleanup and no structural defeat event. A later revival can therefore restore
an actor carrying state that its removal profile required the second defeat to
clear, while presentation and telemetry also miss the transition.

**Required correction:** track defeated periods or living-to-defeated edges,
not actor-lifetime membership. An actor who remains defeated must not be
processed repeatedly; an actor who becomes living again must be eligible for a
later defeat cleanup and announcement. Add a full encounter regression with
defeat, explicit revival, second defeat, repeated reconciliation, and retained
teammates so completion does not hide the lifecycle error.

### O6-R14-M1: zero surviving teams do not complete under the supplied policy

**Invariant:** the supplied last-team-standing policy must terminate once no
competitive living-team state remains. One living team is a victory; zero
living teams is an immediate draw; two or more living teams continues.

**Reachable path:** one action, reaction, reflection, passive, or lifecycle
boundary defeats the final living actors on every team. Starting an encounter
with no deployed living participant reaches the same state.

**Current behavior:** `LastTeamStandingCompletionPolicy` completes only when
the living-team count is exactly one. A count of zero returns the same
incomplete value as a contested battle:

- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L708)
  contains the supplied policy; and
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2053)
  falls back to a round-limit draw only after the scheduler finishes.

**Consequence:** the supplied schedulers advance empty phases and round clocks
until the configured round limit. That delays a terminal state, emits
misleading structural history, and can advance lifecycle durations after no
team is able to act.

**Required correction:** return an immediate complete `Draw` for zero living
teams and retain `Victory` for exactly one. Cover both supplied schedulers,
initial zero-survivor state, and mutual defeat during a command.

### O6-R14-M2: automated skill execution is incomplete for valid untargeted and terminal effects

**Invariant:** the automated convenience runner must either execute an
authorized equipped skill through its complete typed result or reject that
skill explicitly before mutation. A valid untargeted skill must not fault merely
because it has no target, and terminal or host-request output must not be lost.

**Reachable path:** an automated actor equips a valid untargeted active skill,
such as one containing `EscapeEffectDefinition` or a registered custom effect.
Untargeted active effects are supported by the execution contract.

**Current behavior:** command evidence uses
`SelectedTargetIds.FirstOrDefault()`. An empty target collection therefore
becomes a present but invalid default runtime ID and faults event construction.
After execution, the adapter maps effects and turn consumption but ignores the
`SkillExecutionResult.EscapeRequested` and `HostActionRequestIds` values that
the canonical action facade preserves:

- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L682)
  constructs the invalid untargeted command payload;
- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L691)
  executes the prepared skill; and
- [`ExecutionContracts.cs`](../../src/Convergence.Framework/Execution/ExecutionContracts.cs#L745)
  exposes the omitted terminal and host-request evidence.

**Consequence:** valid authored automated skills can produce a typed encounter
fault, or their requested terminal/host action can be silently discarded. This
contradicts the developer guide's statement that the automated runner executes
catalog-authorized skills and preserves canonical terminal outcomes.

**Required correction:** represent no selected target as `null`, map all
`SkillExecutionResult` terminal and host-request evidence, and add focused tests
for an untargeted ordinary custom result, successful escape, and a host-action
request. If a category is intentionally unsupported by automation, reject it
before execution with a typed diagnostic instead of partially consuming it.

### O6-R14-M3: `ActionExecuted` evidence can name another encounter actor

**Invariant:** an action event that identifies an actor must identify the actor
who owns the current command window. Events without an actor remain valid for
action evidence such as a roster transition.

**Reachable path:** a buggy custom turn-handler adapter executes the scheduled
actor's command but returns a legal `ActionExecuted` payload containing another
participant's runtime ID.

**Current behavior:** frozen-graph validation confirms that the supplied actor
exists, but `CommandEvidenceActor` does not classify
`BattleActionExecutedEventPayload` as command evidence:

- [`BattleEncounterEvents.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterEvents.cs#L977)
  performs scheduled-actor correlation; and
- [`BattleEncounterEvents.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterEvents.cs#L1000)
  omits the action payload from the correlation switch.

**Consequence:** the canonical event stream can attribute an executed action to
the wrong participant even though the runner claims to enforce command-window
identity. A Godot host can animate or label the wrong scene actor without
receiving a typed integration fault.

**Required correction:** correlate a non-null
`BattleActionExecutedEventPayload.ActorId` with the scheduled actor. Preserve
the existing valid actor-less payload case. Add acceptance and rejection tests
for both shapes.

### O6-R14-L1: the developer guide describes completion snapshots as live actors

The integration guide says `IBattleEncounterCompletionPolicy.Evaluate`
receives live participants:

- [`encounter-orchestration.md`](../developer-guide/encounter-orchestration.md#L227)

The runner actually builds detached `BattleEncounterParticipantSnapshot`
values before invoking the policy:

- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2139)

This is a documentation error rather than a runtime defect. It may nevertheless
lead an integrator to expect mutation authority that the framework deliberately
removed. Replace "live participants" with "detached participant snapshots" and
document the corrected zero-team and repeated-defeat semantics after the code
changes land.

## Test-Gap Assessment

The current tests deeply cover ordinary scheduling, lifecycle rollback,
cancellation, economy continuity, scheduler identity, frozen-graph event IDs,
terminal shapes, and automated targeted skills. They do not currently cover:

- defeat, explicit revival, and a second defeat in one encounter;
- zero living teams before or during the supplied completion policy;
- an automated valid untargeted skill;
- automated propagation of skill-level escape or host-action requests; or
- wrong-actor `ActionExecuted` evidence.

The green suite therefore does not invalidate the findings. It confirms that
the defects sit in uncovered supported combinations rather than in already
characterized paths.

## Documentation Review

The three audience pages are otherwise materially aligned with current source:

- mechanics correctly separates scheduling from turn economy, explains both
  supplied schedulers, and distinguishes Back, typed cancellation, operational
  cancellation, rejection, and faults;
- developer guidance correctly requires both liveness policies, asynchronous
  host execution, typed event consumption, and exclusive active ownership of a
  lifecycle port; and
- technical guidance correctly traces transaction ordering, lifecycle staging,
  event sequence authority, frozen participant validation, and immutable final
  snapshots.

The repeated-defeat defect violates the mechanics page's promise that an actor
newly observed as defeated receives cleanup. The automated defect exceeds the
documented skill-execution promise. The completion-policy defect exposes an
undefined zero-team branch. The direct live-participant statement is simply
wrong. For those reasons all three audience entries return to
`existing_unreviewed` until correction and another source-first review.

## Trusted Boundaries That Are Not Findings

The following are deliberate, documented integration boundaries and were not
inflated into vulnerabilities:

- a turn handler executes action mutation before returning; action-level
  atomicity belongs to `BattleActionExecutor`;
- a state synchronizer is a trusted host adapter over live participants;
- an event-sink failure after a committed transition cannot undo that already
  committed gameplay transition; and
- `BattleStatusEncounterLifecyclePort` is mutable clock authority and must not
  be shared by overlapping encounters.

These remain important host obligations, but the current documents state them
and the runner cannot generically roll back arbitrary external side effects.

## Correction Roadmap

| Checkpoint | Work | Required evidence |
|---|---|---|
| O6-R15 | Replace permanent defeat membership with transition-aware defeat reconciliation. | Defeat, revive, second defeat, repeated reconcile, cleanup count, and event count tests. |
| O6-R16 | Complete zero living teams immediately as a draw. | Initial and command-time mutual defeat under both supplied schedulers. |
| O6-R17 | Preserve complete automated skill terminal and host-request evidence and nullable untargeted selection. | Untargeted, escape, host-request, mutation, outcome, and event tests. |
| O6-R18 | Correlate non-null `ActionExecuted` actors with the command window. | Wrong actor rejected; scheduled actor and actor-less evidence accepted. |
| O6-R19 | Reconcile mechanics, developer, technical, API, matrices, and roadmap language. | Documentation contract and link tests. |
| O6-R20 | Perform another source-first closure review and complete local gate. | No unresolved reachable defect and all verification commands green. |

Each correction should be isolated in its own green commit. Order 6 must remain
open while any checkpoint is incomplete.

## Correction Progress After This Audit

| Checkpoint | State | Evidence |
|---|---|---|
| O6-R15 | Complete | Commit `dffdbbfd` releases defeat bookkeeping when synchronization observes recovery, then processes a later defeat as a new period. Focused coverage proves two cleanups and two announcements without duplicates during stable defeat. |
| O6-R16 | Complete | Commit `16302c18` makes zero deployed living teams an immediate draw. Initial and command-time mutual defeat pass under both supplied schedulers. |
| O6-R17 | Complete | Commit `4ab46b44` preserves null untargeted selection, ordered host-action requests, and successful escape as a canonical automated terminal outcome. |
| O6-R18 | Complete | Commit `4dad93fb` correlates every non-null `ActionExecuted` actor with the scheduled command window while preserving actor-less roster evidence. |
| O6-R19 | Complete | Mechanics, developer, technical, public API, architecture, gameplay, matrices, roadmaps, and executable documentation assertions now state the corrected contracts. Audience entries intentionally remain `existing_unreviewed`. |
| O6-R20 | Complete; correction required | The [source-closure review](encounter-orchestration-order-6-r20-source-closure-review-2026-08-04.md) confirmed the prior corrections and found that actorless ordinary `ActionExecuted` evidence can still bypass command ownership. |
| O6-R21 | Pending | Require an actor for every executed-action kind except a genuine `PartyRosterTransitioned` event. |
| O6-R22 | Pending | Reconcile event-ownership guidance and maturity evidence after O6-R21. |
| O6-R23 | Pending | Perform another fresh source-first closure review and complete the local gate. |

## Verification At This Audit

- focused encounter and automated tests: **258 passed**, 0 failed, 0 skipped;
- full solution: **1,845 passed**, 0 failed, 0 skipped;
  - Framework: 1,660;
  - DemoHost: 178;
  - ContentValidator: 7;
- worktree was clean at review start on `main` revision `1a926191`.

The focused and full gates establish a healthy baseline. They do not certify
the uncovered paths listed above.

## Closure Decision

Order 6 is **not ready for formal closure** at this revision.
`encounter_orchestration` returns to `partial`, and its mechanics, developer,
and technical documentation entries return to `existing_unreviewed`.

This is not a rejection of the architecture. The scheduler protocol, lifecycle
transactions, cancellation model, liveness guards, immutable results, and
typed event foundation are strong. The remaining work is bounded to supported
state transitions and adapter evidence that need exact completion before the
capability can honestly return to `complete`.
