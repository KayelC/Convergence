# Encounter Orchestration Order 6 Independent Closure Audit

**Date:** 30 July 2026  
**Capability:** `encounter_orchestration`  
**Review scope:** current active Framework source, Framework tests, DemoHost
integration, and the three active encounter-orchestration documents  
**Closure status:** corrections required; Order 6 remains open

## Purpose

This is the independent O6-R13 closure audit required by the approved
[Order 6 source review and roadmap](encounter-orchestration-order-6-source-review-2026-07-30.md).
It records findings from a fresh read of the current implementation after
O6-R1 through O6-R12. Earlier conclusions were not accepted as proof.

The audit inspected:

- scheduling contracts and both supplied scheduling policies;
- post-command scheduling and Action Token opportunity ownership;
- the canonical encounter runner;
- lifecycle transactions and the status-lifecycle encounter port;
- completion, cancellation, rejection, fault, and event boundaries;
- the automated encounter convenience runner;
- DemoHost and Godot-shaped host integration;
- current encounter tests and the mechanics, developer, and technical pages.

A focused pre-correction gate passed **278 tests, 0 failures, 0 skipped**.
That result proves the covered paths remain stable, but it does not invalidate
the reachable defects below.

## Confirmed Findings

### O6-H1: A Custom Scheduler Can Prevent Encounter Progress Forever

**Intended invariant:** every supplied or custom schedule must either advance
to a command opportunity, complete a round, terminate, or produce a bounded
typed fault.

**Reachable path:** an injected `IBattleEncounterSchedulePolicy` can repeatedly
return empty `PhaseStarted` and `PhaseEnded` steps. Existing
`BattlePhaseProgressPolicy` bounds commands and consecutive free actions only
after a command window opens. The runner has no encounter-wide limit for
structural schedule transitions.

**Consequence:** `RunAsync` can loop forever without reading a command,
consuming an Action Token, reaching the round limit, or producing a fault.
Only external token cancellation can stop it.

**Correction:** add an explicit encounter progress policy with a positive
maximum schedule-transition count. Require it through encounter composition,
count every accepted schedule advance, and return a stable typed fault before
the limit can be exceeded. This policy is a liveness guard, not a replacement
for scheduler semantics or the existing per-phase command guard.

### O6-M1: Schedule Steps Can Name A Team Outside The Encounter

**Intended invariant:** every phase and command step belongs to the exact team
graph established by the scheduler at encounter start.

**Reachable path:** transition validation checks policy identity, sequence,
round continuity, and step shape, but a custom policy can emit
`PhaseStarted`/`PhaseEnded` with a non-empty team ID that is not in
`BattleEncounterScheduleState.TeamOrder`.

**Consequence:** the runner can publish structural phase events and invoke
phase lifecycle callbacks for a team that does not participate in the
encounter. A malformed empty phase can then continue without a command-window
identity check catching it.

**Correction:** validate every phase team and command actor/team pair against
the frozen schedule graph before accepting the transition. Rejection must
preserve the previous schedule state and become the existing typed scheduling
fault.

### O6-M2: Read-Only Decision Policies Receive Live Mutable Actors

**Intended invariant:** initiative and completion policies decide order or
terminal state; they do not mutate encounter actors.

**Reachable path:** `BattleEncounterInitiativeRequest` and
`BattleEncounterCompletionRequest` currently expose live
`BattleEncounterParticipant` objects and therefore live `RuntimeActorState`
instances.

**Consequence:** a custom initiative or completion policy can change HP,
resources, deployment, or runtime state without going through lifecycle,
commands, synchronization, reconciliation, or ordered events.

**Correction:** expose detached immutable `BattleEncounterParticipantSnapshot`
values to both policy boundaries, including the last-command actor evidence.
The runner retains live participants internally and correlates policy outputs
by stable runtime/team IDs.

### O6-M3: Record Cloning Can Bypass Encounter Event Invariants

**Intended invariant:** every public encounter event and payload remains valid
and immutable after construction, including when retained by an asynchronous
host sink.

**Reachable path:** `BattleEncounterEvent.Sequence` and several payload
properties use public `init` setters. Record `with` expressions can therefore
replace validated constructor values with negative sequences, mismatched team
or actor IDs, undefined enums, or null result evidence. Port-owned events are
then resequenced by another `with` expression without central payload
validation.

**Consequence:** malformed evidence can enter the canonical ordered result and
host event stream even though direct constructors enforce only part of the
contract.

**Correction:** make event state get-only, add a framework-owned validated
resequencing method, validate all payload identity and enum shapes at the event
boundary, and prove record cloning cannot manufacture invalid canonical
evidence.

### O6-M4: Cancellation Can Advance Lifecycle Clock State

**Intended invariant:** operational cancellation before a lifecycle boundary
commits preserves both actor state and the lifecycle sequence that identifies
that boundary.

**Reachable path:** `BattleStatusEncounterLifecyclePort` checks cancellation at
method entry. A supplied lifecycle behavior or custom handler can cancel the
token during synchronous processing, after which the port can still commit
actor mutation and `_lifecycleEventSequences`.

**Consequence:** a cancelled attempt may leave actor state changed or consume
the sequence intended for a later retry. The next successful attempt can
observe a different lifecycle identity despite the cancelled encounter
transition never committing.

**Correction:** stage every lifecycle boundary through the existing
transaction mechanism, check cancellation again immediately before actor and
clock commits, and commit both authorities together. Regression tests must
prove cancelled turn, phase, round, and battle boundaries leave snapshots and
sequence evidence unchanged.

### O6-L1: Malformed Encounter Requests Can Escape Through Raw Null Failures

**Intended invariant:** malformed host input is rejected at its public
constructor boundary or converted into a stable typed encounter fault before
the runner dereferences it.

**Reachable path:** encounter and schedule-start requests currently admit some
null participant entries, empty teams/IDs, undefined context values, and
non-positive round numbers. Detached snapshot construction can then
dereference invalid entries.

**Consequence:** a host integration error can escape as a raw
`NullReferenceException` or fail later than the contract that accepted it,
making diagnostics inconsistent and preventing reliable host recovery.

**Correction:** validate public request domains eagerly with clear argument
diagnostics. Duplicate runtime instance IDs remain runner-validated because
the encounter contract deliberately reports those as
`DuplicateParticipantInstanceId`.

## Reviewed Behaviors That Remain Sound

The audit did not find a defect in these implemented decisions:

- team-phase scheduling preserves the prior team and active-actor rotation;
- agility scheduling freezes one descending effective-Agility order per round;
- the post-command policy can retain only an existing actor opportunity and is
  already bounded locally;
- Action Token state remains owned by `IBattleTurnEconomy`, not schedulers;
- reconciliation occurs after the implemented lifecycle boundaries and
  prevents unavailable actors from reaching command execution;
- completion-policy and command-terminal result shapes receive typed
  validation;
- typed encounter cancellation remains distinct from operational token
  cancellation;
- rejected commands remain host-contract faults;
- automated execution delegates to the canonical asynchronous runner and
  preserves its typed event stream.

## Correction Checkpoints

Each checkpoint receives its own tested commit.

| Checkpoint | Work | Exit condition |
|---|---|---|
| O6-R13A | Add the encounter-wide schedule-transition liveness policy. | An empty structural scheduler faults at the configured bound without reading commands or hanging. |
| O6-R13B | Enforce frozen schedule team and actor identity. | Unknown phase teams and mismatched command actors are rejected without state advancement. |
| O6-R13C | Isolate initiative and completion policies from live actors. | Both policies receive detached immutable snapshots and cannot mutate encounter state. |
| O6-R13D | Close event cloning and payload-validation gaps. | Canonical events cannot be cloned or port-published into invalid shapes. |
| O6-R13E | Make lifecycle actor and sequence commits cancellation-atomic. | Cancellation during lifecycle processing preserves actors and sequence clocks. |
| O6-R13F | Harden encounter request construction. | Invalid public inputs fail at the accepting boundary with deterministic diagnostics. |
| O6-R13G | Reconcile documentation and perform a fresh closure review. | Source, tests, all three audience documents, matrices, and the complete gate agree. |

## Closure Rule

Order 6 remains open while any checkpoint above is incomplete.
`encounter_orchestration` remains `partial`, and its mechanics, developer, and
technical documentation entries remain `existing_unreviewed`.

The capability may return to `complete` and the three documentation entries
may become `reviewed` only after:

1. every confirmed finding is corrected and independently regression-tested;
2. a fresh source read finds no remaining realistic reachable defect in the
   reviewed contract;
3. the documentation describes the corrected policy boundaries precisely; and
4. the complete .NET 8 release gate passes with zero failures, warnings, or
   skipped tests.
