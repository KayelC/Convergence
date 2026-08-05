# Encounter Orchestration Order 6 Post-R42 Independent Audit

**Date:** 5 August 2026

**Reviewed revision:** `0e3beb6f`

**Capability:** `encounter_orchestration`

**Review checkpoint:** O6-R43

**Decision:** Reopen Order 6. Two supported fault paths remain incorrect, and
one developer-facing contract name is stale.

## Verdict

Order 6 is healthy on its normal execution paths, but it is not ready for
formal closure at this revision.

This audit found:

- no high-severity finding;
- two medium-severity runtime findings; and
- one low-severity documentation finding.

The findings are not speculative gameplay alternatives. Each begins at a
publicly supported encounter port, follows code that is reachable with valid
framework inputs, and produces incorrect typed evidence or fault authority.
The capability returns to `partial` until O6-R44 through O6-R47 are complete.

## Review Method

This review reconstructed Order 6 from current source rather than treating an
earlier review or closure summary as evidence. The source trace covered:

- encounter request, participant, service, command, completion, and result
  contracts;
- both supplied scheduling policies and their immutable cursor state;
- scheduler identity, structure, economy-liveness, and transition budgets;
- turn-start, turn-end, phase-end, round-end, departure, and battle-end
  lifecycle transactions;
- command execution, Action Token mutation, completion reconciliation, and
  terminal outcomes;
- typed event construction, port-event ownership, event publication, and fault
  finalization;
- token cancellation and typed command cancellation;
- automated selection, restriction execution, knowledge integration, and
  result projection; and
- mechanics, developer, and technical encounter documentation.

The focused test gate selected 201 current encounter and automated-battle tests;
all 201 passed with zero skips. Those tests confirm the healthy paths listed
below, but their current assertions do not cover the two combined failure paths
reported here.

## Findings

### O6-M1: Failed event publication can erase canonical evidence and reuse its sequence

**Severity:** Medium

**Area:** Event authority and fault containment

#### Intended invariant

`BattleEncounterResult.Events` is the complete canonical event history. The
event sink is an observer of that history. A sink failure may prevent terminal
evidence from reaching the failed sink, but it must not change the identity or
contents of the canonical result history.

#### Reachable path

1. A lifecycle or command operation commits live state and returns event
   evidence.
2. `PublishAndRecordAsync` assigns event sequence `N` and asks the host sink to
   publish the event.
3. The sink accepts or records the event, then throws while completing its
   `ValueTask`. This is a valid failure mode for an asynchronous host adapter.
4. The runner decrements the sequence and never adds the failed event to its
   result list.
5. Fault finalization assigns the reused sequence `N` to `BattleFaulted`.

The same omission occurs when the sink throws before accepting the event. In a
post-commit lifecycle path, the final participant snapshot can therefore show a
committed mutation while the canonical result omits the event that explains it.

#### Evidence

- `BattleEncounterRunner.PublishAndRecordAsync` publishes first, decrements the
  sequence on every failure, and records only after successful publication.
- Turn-start, turn-end, departure, phase-end, round-end, and battle-end
  transactions commit before their returned events enter
  `PublishAndRecordAsync`.
- Fault finalization correctly uses the opposite ordering: it records an event
  before attempting publication and never reuses its sequence.
- `Runner_ContainsEventSinkExceptionsAtPreStartAndActiveBattleStages` verifies
  a typed fault and continuous returned sequences, but its sink throws before
  recording the failed event and the test never asserts that the failed event
  remains in the canonical result.

#### Consequence

A Godot or other event-driven host can observe one meaning for sequence `N`
while the returned result assigns a different meaning to `N`. Save, replay,
debug, and recovery code cannot reconcile those histories reliably. Even when
the sink received nothing, committed state may lack its corresponding canonical
event evidence.

#### Required correction

- Give the returned result sole sequence authority.
- Never decrement or reuse an assigned canonical sequence after publication
  begins.
- Preserve the event whose publication failed in the returned history while
  still returning `EventPublicationFailed`.
- Preserve the existing cancellation and structural battle-start cleanup
  boundaries explicitly rather than changing them accidentally.
- Add regressions for failures before sink acceptance, after sink acceptance,
  and after committed lifecycle/action state.

### O6-M2: Battle-end cleanup failure replaces the primary command fault

**Severity:** Medium

**Area:** Terminal fault authority

#### Intended invariant

When an encounter has already faulted, a later battle-end cleanup failure is
secondary evidence. It must not replace the original fault code and message in
the terminal result.

#### Reachable path

1. A valid turn handler returns `BattleEncounterCommandResult.Faulted(...)` or
   `Rejected(...)`.
2. The runner records the corresponding command fault and calls `FinishAsync`
   with `CommandExecutionFaulted` or `CommandRejected`.
3. `ProcessBattleEndAsync` throws while performing cleanup.
4. `FinishAsync` starts a new failure finalization whose primary code is
   `LifecycleExecutionFailed`.
5. The returned `BattleEncounterResult` and terminal `BattleEnded` payload now
   identify cleanup as the primary fault, while an earlier `BattleFaulted` event
   identifies the command as the primary fault.

#### Evidence

- The command-fault and command-rejection branches publish their own
  `BattleFaulted` event and then enter `FinishAsync`.
- The `FinishAsync` cleanup catch calls `FinalizeFailureAsync` with a new
  lifecycle fault instead of preserving the supplied terminal fault.
- The separate port-exception finalization path already implements the desired
  behavior: it retains the primary fault and appends cleanup failure as
  secondary evidence.
- Existing tests cover command faults and cleanup faults separately, and cover
  primary-port-fault preservation, but not command fault plus cleanup fault.

#### Consequence

Callers that inspect the typed terminal result receive the wrong root cause.
The result metadata and its own event history can disagree, making diagnostics,
telemetry, and host recovery decisions unreliable during a supported double
failure.

#### Required correction

- Route command faults and rejections through the same primary-fault
  finalization authority used by contained port failures.
- Record one primary command fault, append cleanup failure separately, and keep
  the original command fault code and message on `BattleEncounterResult` and
  `BattleEnded`.
- Add focused regressions for both `Faulted` and `Rejected` command results
  combined with failing battle-end cleanup.

### O6-L1: The developer guide names a public interface that does not exist

**Severity:** Low

**Area:** Developer integration documentation

The restricted-action section names `IAutomatedRestrictedActionSource`. The
exported framework contract is `IAutomatedBattleRestrictionActionSource`.
A developer following the guide cannot locate or implement the documented
interface.

Correct the guide and add a documentation/source-name guard for this contract.

## Confirmed Healthy Areas

The following behavior was traced from source and supported by current tests:

- encounter requests reject duplicate runtime IDs through a typed pre-start
  fault;
- initiative must return an exact permutation of participating teams;
- scheduler state preserves policy, participant, team, round-limit, revision,
  and step-sequence identity;
- scheduler steps cannot introduce unknown teams or command actors outside the
  frozen encounter graph;
- team-phase round-robin uses stable participant slots while skipping actors
  that are unavailable at selection time;
- Agility scheduling freezes one resolved-stat order per round, validates exact
  tie-break permutations, and skips actors that become unavailable;
- command windows cannot continue after accepted economy evidence reports no
  remaining opportunities;
- encounter-wide schedule transitions, per-phase accepted turn windows,
  consecutive free actions, and immediate repeats are finitely bounded;
- no-cost commands must preserve the exact economy state;
- lifecycle mutations are staged and rolled back on lifecycle failure or token
  cancellation before commit;
- departure cleanup distinguishes Flee, Roster Recall, and Defeat and runs once
  per current departure/defeat period;
- completion policy outputs are shape-checked and cannot fabricate a fault;
- returned participants are detached immutable snapshots;
- token cancellation propagates as `OperationCanceledException`, while typed
  command cancellation returns `Cancelled` with one cleanup pass;
- port events cannot impersonate runner-owned structure or reference actors
  outside the frozen participant graph; and
- automated battles use the canonical runner, authorized catalog skills,
  prepared target identity, typed restrictions, encounter-local team knowledge,
  and asynchronous execution.

## Documentation Alignment

| Contract | Code | Documentation | Result |
|---|---|---|---|
| Team-phase and Agility scheduling | Matches | Matches | Aligned |
| Action Token ownership and liveness | Matches | Matches | Aligned |
| Lifecycle ordering and staged rollback | Matches | Matches | Aligned |
| Completion, cancellation, and detached results | Matches | Matches | Aligned |
| Complete canonical event history on sink failure | Does not preserve failed event | Claims complete result history | Misaligned |
| Primary fault preservation when cleanup also fails | Port faults preserve it; command faults do not | Describes cleanup as separate fault evidence | Misaligned |
| Automated restriction action source name | `IAutomatedBattleRestrictionActionSource` | `IAutomatedRestrictedActionSource` | Misaligned |

## Correction Roadmap

| Checkpoint | State | Work |
|---|---|---|
| O6-R43 | `complete` | Record this independent source and documentation audit; reopen the capability and three audience entries. |
| O6-R44 | `pending` | Preserve canonical event identity and failed-publication evidence without changing approved cancellation or battle-start cleanup semantics. |
| O6-R45 | `pending` | Preserve primary command fault authority when battle-end cleanup also fails. |
| O6-R46 | `pending` | Correct the interface name and reconcile event-delivery and fault-finalization guidance across all three audiences and public API documentation. |
| O6-R47 | `pending` | Independently reread corrected source and documentation, run the complete release gate, and decide formal closure. |

## Verification

- Focused encounter and automated-battle tests: 201 passed, 0 failed, 0
  skipped.
- Full solution tests: 1,883 passed, 0 failed, 0 skipped (1,698 Framework,
  178 DemoHost, and 7 ContentValidator).
- Nonincremental .NET 8 solution build: succeeded with 0 warnings and 0 errors.
- `dotnet format --verify-no-changes`: passed.
- Documentation matrix, capability matrix, documentation-link, product-boundary,
  framework-boundary, and terminology guards: passed as part of the full suite.
- `git diff --check`: passed.

## Closure Decision

Order 6 remains open. `encounter_orchestration` is `partial`, and its mechanics,
developer, and technical documentation entries are `existing_unreviewed` until
the correction roadmap is implemented and independently rechecked.
