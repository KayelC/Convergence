# Encounter Orchestration Order 6 Final Correction Roadmap

**Date:** 30 July 2026  
**Capability:** `encounter_orchestration`  
**Review basis:** fresh inspection of current active source, tests, and the
three encounter-orchestration audience documents  
**Status:** corrections required before closure

## Purpose

This record continues the independent O6-R13 closure gate after O6-R13A
through O6-R13F were implemented. It does not accept earlier reports as proof.
The review reread the current runner, scheduler contracts and supplied
policies, lifecycle transactions, canonical event contracts, automated
wrapper, focused tests, and active encounter documentation.

The pre-correction focused gate passed **238 tests, 0 failures, 0 skipped**.
That confirms the covered paths remain stable. It does not cover or invalidate
the reachable contract defects below.

These findings are integration-correctness and crash-safety defects. They are
not described as security vulnerabilities because each requires a malformed
or buggy host-supplied port rather than untrusted player data.

## Confirmed Findings

### O6-M5: Turn-Start Event Mapping Occurs After Lifecycle Commit

**Intended invariant:** a lifecycle result is completely validated before its
staged actor graph and lifecycle clock checkpoint commit. A malformed custom
lifecycle result becomes `LifecycleExecutionFailed` without changing live
state.

**Reachable path:** `BattleEncounterRunner` commits the turn-start
`BattleEncounterLifecycleTransaction`, then maps
`BattleTurnStartLifecycleResult.Events`. That public result snapshots the
collection but does not reject a null or otherwise malformed status event.
Mapping can therefore throw after commit and outside the runner's port-fault
boundary.

**Consequence:** live turn-start mutation can commit before a raw mapping
exception escapes. The caller receives neither a typed encounter fault nor the
normal battle-end cleanup guarantee.

**Correction:** map and validate all turn-start lifecycle evidence inside the
staged lifecycle try block, before commit. A mapping failure must discard the
staged graph, restore checkpointed lifecycle state, and return the existing
typed lifecycle fault.

### O6-M6: Port Events Are Not Correlated With The Encounter Graph

**Intended invariant:** every actor and target named by a canonical encounter
event belongs to the frozen participant graph. Command-selection evidence must
name the actor whose command window is open, and a presence event's team must
match that actor's encounter team.

**Reachable path:** `BattleEncounterEventOwnership.RequirePortOwned` checks
only whether an event kind belongs to a port. A turn handler or lifecycle port
can return a structurally valid event containing an unrelated valid runtime
ID, a mismatched presence team, or an `ActionRejected` payload whose status is
`Executed`.

**Consequence:** the runner accepts and publishes semantically false canonical
evidence. A Godot host can animate or label the wrong scene instance even
though the event passed framework validation.

**Correction:** validate port-event actor and target IDs against the frozen
participant graph before publication. Validate presence team correlation,
scheduled-actor command evidence, and the meaningful status domain of
`ActionRejected`. Reject malformed evidence through the existing typed port
fault without publishing it.

### O6-M7: Automated Battle Collapses Escape And Cancellation Into Draw

**Intended invariant:** the automated convenience result preserves the
canonical encounter's terminal meaning.

**Reachable path:** the supplied restricted-action resolver can execute an
escape command and request `BattleEncounterOutcome.Escape`. Custom restriction
resolvers can also return other valid terminal outcomes. The automated wrapper
currently maps only `Victory` and `Faulted` explicitly and maps every other
canonical outcome to `AutomatedBattleOutcome.Draw`.

**Consequence:** the same run reports `Escape` in its canonical
`BattleEnded` event but `Draw` in `AutomatedBattleResult.Outcome`. A host that
uses the top-level result can take the wrong post-battle route.

**Correction:** extend the automated outcome vocabulary without renumbering
its existing members and map every canonical outcome explicitly. Add
regression evidence for escape and cancellation rather than inferring the
result from debug text.

### O6-L2: Automated Battle Request Validation Lags Behind The Canonical Request

**Intended invariant:** both public encounter request types reject malformed
host input at construction with deterministic argument diagnostics.

**Reachable path:** `AutomatedBattleRequest` snapshots participants and then
dereferences them while validating knowledge seeds. It does not eagerly reject
empty collections, null entries, invalid context or battle-kind IDs, invalid
optional moon IDs, or non-positive round limits. Some values fail later in
`RunAsync`; a null entry fails as a raw `NullReferenceException` in the public
constructor.

**Consequence:** two public entry points disagree about accepted domains and
failure timing, making host recovery and diagnostics needlessly inconsistent.

**Correction:** mirror the canonical request's eager domain checks while
retaining the deliberate runner-owned typed duplicate-ID fault.

### O6-L3: Active Integration Guidance Omits A Required Liveness Policy

**Intended invariant:** the developer guide's minimal composition compiles and
names every mandatory orchestration authority.

**Reachable path:** `BattleEncounterServices` now requires
`BattleEncounterProgressPolicy`, but the required-composition table and
minimal code sample omit it.

**Consequence:** a developer following the active guide receives a compile
error and is not told that structural scheduler liveness must be bounded
independently from per-phase command liveness.

**Correction:** add the policy to the composition table and sample, explain
both liveness limits, document port-event graph rules and exact automated
outcomes, and state the concurrency scope of the mutable standard lifecycle
port.

## Reviewed Areas That Remain Sound

The current source review did not reproduce a defect in:

- schedule revision, step-sequence, participant, team, and actor identity
  continuity;
- supplied team-phase and Agility scheduling;
- bounded post-command actor retention;
- turn-economy authority and per-phase liveness checks;
- reconciliation after every mutating lifecycle boundary;
- completion and command terminal-shape validation;
- typed versus operational cancellation;
- immutable final participant snapshots; or
- cancellation rollback of canonical actor and lifecycle clock state.

## Correction Checkpoints

Each correction receives its own focused tests, review, and commit.

| Checkpoint | Work | Exit condition |
|---|---|---|
| O6-R13H | Preflight turn-start lifecycle events before commit. | Malformed status evidence returns `LifecycleExecutionFailed`, leaves actors and clock checkpoints unchanged, and does not escape as a raw exception. |
| O6-R13I | Correlate all port events with the frozen encounter graph. | Unknown actors or targets, mismatched presence teams, wrong command actors, and impossible rejection statuses are rejected before publication. |
| O6-R13J | Harden `AutomatedBattleRequest`. | Empty, null, invalid-ID, and invalid-round inputs fail at construction; duplicate IDs still reach the canonical typed runner fault. |
| O6-R13K | Preserve all automated terminal outcomes. | Escape, cancellation, draw, victory, defeat, and fault map without semantic loss while existing enum values remain stable. |
| O6-R13L | Reconcile documentation and perform one final fresh review. | Source, tests, API baseline, all three audience pages, matrices, and the full gate agree. |

## Closure Rule

Order 6 remains open until O6-R13H through O6-R13L are complete. The capability
remains `partial`, and its documentation coverage remains
`existing_unreviewed`, until a fresh post-correction read finds no realistic
reachable defect and the complete local release gate passes.
