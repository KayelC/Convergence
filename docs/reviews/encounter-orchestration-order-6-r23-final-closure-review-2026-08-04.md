# Encounter Orchestration Order 6 R23 Final Closure Review

**Date:** 4 August 2026

**Capability:** `encounter_orchestration`

**Reviewed revision:** `b115dd8d`

**Review checkpoint:** O6-R23

**Result:** no unresolved realistic reachable defect found

**Closure status:** Order 6 is formally complete

## Purpose

This was a fresh source-first review of the corrected encounter subsystem. The
conclusions of earlier Order 6 reports were not treated as proof. Current
runtime source and executable contracts were traced first, then current tests,
then the mechanics, developer, and technical documentation.

The review asked whether a supported host can reach a state that violates the
approved encounter rules. It did not classify trusted host responsibilities,
unreachable malformed states, or alternate product designs as framework
vulnerabilities.

## Source Trace

The review followed the current implementation through:

1. request construction, participant identity, detached projections, and
   encounter startup;
2. initiative and both supplied scheduling policies;
3. schedule identity, revision, sequence, team, actor, and liveness checks;
4. battle-start, turn-start, owner-turn-end, phase-end, round-end, departure,
   and battle-end lifecycle transactions;
5. lifecycle-clock staging, commit, and rollback;
6. command cancellation, operational cancellation, rejection, fault, and
   accepted-command paths;
7. phase-scoped turn-economy construction, mutation, and continuity checks;
8. state synchronization, defeat/departure reconciliation, completion, and
   terminal result construction;
9. canonical event construction, sequence ownership, frozen participant graph
   validation, nested effect/status/passive evidence, and scheduled command
   ownership; and
10. automated actor selection, restriction handling, prepared skill
    execution, untargeted actions, host requests, escape, and terminal output.

## Corrected Invariants Rechecked

### Repeated defeat periods

Defeat processing is now edge-sensitive rather than encounter-lifetime
membership. An actor receives cleanup and one `ActorDefeated` event when a new
defeated period begins. Stable reconciliation does not duplicate either. If
synchronization later observes the actor living, a later defeat starts a new
period and is processed again.

### Zero surviving teams

The supplied last-team-standing policy distinguishes all competitive states:

- two or more deployed living teams continue;
- one deployed living team completes with a winner; and
- zero deployed living teams completes immediately as `Draw`.

This holds at encounter startup and after command-time mutual defeat under both
supplied schedulers.

### Automated action completeness

The automated adapter keeps an untargeted command target as `null`, executes an
authorized prepared skill through the canonical executor, preserves ordered
host-action requests, and maps successful escape to the canonical terminal
outcome. It does not silently discard those result fields or substitute a
default runtime ID.

### Command-window ownership

Every actor-owned command event must identify the scheduled actor.
`BattleActionExecutedEventPayload` requires a valid actor for every action kind
except the canonical actorless `PartyRosterTransitioned` evidence. A malformed
ordinary actorless event or an event naming another participant becomes a typed
turn-handler fault before publication.

### Public event contract

The final certification exercises every canonical event kind and its host
projections. It also verifies malformed envelopes, mismatched payload kinds,
invalid identities, undefined enum values, contradictory passive evidence,
incompatible turn-economy snapshots, and impossible battle-end combinations.
These fail at the public event boundary instead of entering the encounter
stream.

## Fresh Review Findings

No additional realistic reachable defect was found in the reviewed Order 6
scope after O6-R21.

The source trace specifically confirmed that:

- participant snapshots supplied to scheduling and completion policies are
  detached from live runtime state;
- lifecycle actor mutations and lifecycle sequence checkpoints commit or roll
  back together;
- accepted scheduler transitions are bounded and cannot silently replace the
  encounter graph or manufacture unbounded command windows;
- cancellation is checked before command reads and lifecycle commits, while a
  host-level Back selection remains distinct from encounter cancellation;
- accepted turn consumption is validated against the same phase economy before
  the next schedule step;
- reconciliation runs after every approved structural mutation boundary;
- final encounter results expose detached immutable participant and event
  snapshots; and
- automated battles use the canonical lifecycle, scheduler, action
  authorization, and bound turn-economy paths.

## Documentation Review

The three audience documents agree with current source:

- the mechanics page explains scheduler versus turn-economy authority,
  repeated defeat periods, zero-survivor draws, cancellation, and final-round
  evidence;
- the developer guide explains required policies, asynchronous host usage,
  command ownership, actorless roster evidence, untargeted automated skills,
  and trusted adapters; and
- the technical reference traces transaction order, lifecycle rollback,
  reconciliation, event ownership, completion validation, and detached final
  results.

No document grants a host port structural event authority or describes
completion-policy participants as live mutable actors.

## Trusted Boundaries And Residual Risk

The following remain explicit integration responsibilities rather than hidden
defects:

- turn-handler action mutation must be atomic within the action executor it
  uses;
- a state synchronizer is a trusted adapter over live host state;
- an event-sink failure cannot undo gameplay state that already committed; and
- one mutable status lifecycle port cannot be shared by overlapping
  encounters.

Hosts must also await `RunAsync` on UI threads. The synchronous compatibility
wrapper is not the recommended Godot composition path.

## Verification Evidence

| Gate | Result |
|---|---|
| Focused encounter, scheduler, automated, mapper, and event-contract tests | 269 passed, 0 failed, 0 skipped |
| Full solution | 1,864 passed: 1,679 Framework, 178 DemoHost, 7 ContentValidator; 0 failed, 0 skipped |
| Framework coverage | 90.80% lines, 76.74% branches; 90%/70% gate passed |
| Debug and strict Release builds | 0 warnings, 0 errors |
| Architecture and documentation boundary tests | 57 passed |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions passed |
| DemoHost | Four noninteractive modes and scripted Training Annex play exited successfully |
| Godot 4.7.1 headless smoke | `CONVERGENCE_GODOT_SMOKE_OK`, exit 0 after redirecting sandboxed user-data paths |
| Trimming analysis | 0 warnings, 0 errors |
| Formatting and diff checks | Passed |

The first sandboxed Godot launch could not write its normal Windows
`user://logs` location and crashed in native engine startup. Redirecting
`APPDATA` and `LOCALAPPDATA` into the writable repository artifacts directory
allowed the same official executable and project to complete. Godot then
printed its known nonfatal Windows root-certificate-store warning after the
successful smoke. Neither event entered Framework or Order 6 code.

The locked dependency graph restored successfully. The local environment could
not reach the NuGet vulnerability advisory endpoint, so the online advisory
lookup was not represented as locally passed. No dependency changed in this
correction sequence; the connected CI workflow remains the authoritative
online audit gate.

## Closure Decision

Order 6 is formally complete at this revision. The source, executable evidence,
and all three audience documents agree on the approved orchestration contract.
`encounter_orchestration` may return to `complete`, its known-gap list may be
cleared, and its mechanics, developer, and technical entries may return to
`reviewed`.

This conclusion does not claim that every possible battle design is built.
Convergence supplies a host-neutral encounter director with replaceable
scheduling, turn economy, lifecycle, command, completion, and presentation
ports. New battle models remain future extensions through those contracts, not
unfinished work inside Order 6.
