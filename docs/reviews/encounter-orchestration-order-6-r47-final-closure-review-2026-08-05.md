# Encounter Orchestration Order 6 O6-R47 Final Closure Review

## Review Basis

This review was performed on 5 August 2026 against current `main` at
`ed830368`, before this closure record and tracking promotion were added. It did
not treat the R42 closure report, the R43 audit, or their summaries as proof.
The review began again from current Framework source, executable tests, public
contracts, and the three active audience documents.

The source trace covered:

- request, participant, service, command, completion, and result validation;
- initiative and exact participating-team permutation requirements;
- team-phase and Agility scheduling, immutable cursor identity, structural
  transitions, economy liveness, and all configured progress limits;
- battle-start, turn-start, turn-end, phase-end, round-end, departure, and
  battle-end lifecycle transactions;
- command execution, Action Token continuity, reconciliation, defeat periods,
  deployment changes, and terminal outcomes;
- event payload ownership, frozen encounter-graph correlation, canonical
  sequence authority, sink publication, and fault finalization;
- operational cancellation and typed encounter cancellation;
- automated command selection, prepared skill assessments, restrictions,
  encounter knowledge, and terminal result projection; and
- mechanics, developer, technical, and public API guidance.

**Result:** no unresolved realistic reachable encounter-orchestration defect
was found in the corrected implementation.

## Findings

No High, Medium, or Low correction finding qualifies from this review.

This conclusion does not claim that an event sink has transactional delivery to
an external UI, network, or log. A sink may perform its own side effect and then
throw. The runner cannot reverse that external side effect. The corrected
contract instead guarantees that the immutable returned result owns canonical
event identity: an event is recorded before publication, its sequence is never
reused, and later result-only fault evidence remains ordered after it.

## Corrected Invariants Rechecked

### Canonical event identity survives publication failure

`PublishAndRecordAsync` now appends the fully sequenced event to the canonical
result history before invoking `IBattleEncounterEventSink`. If publication
throws before accepting the event or after enqueueing it, fault finalization
does not remove the event and does not decrement the sequence.

The corrected regressions prove:

- a sink that throws before recording still leaves the failed event in the
  returned result;
- a sink that records and then throws observes the same event object and
  sequence retained by the result;
- every returned sequence remains positive, continuous, and unique; and
- lifecycle state committed before publication remains accompanied by its
  canonical resource evidence.

The startup transaction boundary remains deliberate. Passive activation reset
and battle-start lifecycle work stay staged until their established commits.
`BattleStarted` is canonical before its sink call, but the battle-end cleanup
boundary opens only after that publication completes. Token cancellation still
propagates as `OperationCanceledException` without synthetic terminal events.

### Primary command faults survive cleanup failure

Faulted and rejected command results now enter the same primary-fault
finalization authority used by contained port failures. The runner records one
primary `BattleFaulted` event carrying the command code, actor, team, and
`turn-handler` provenance. Battle-end lifecycle is attempted once.

If cleanup also fails:

- the cleanup transaction rolls back;
- one secondary `LifecycleExecutionFailed` event records that failure;
- the original command code remains on `BattleEncounterResult`;
- the terminal `BattleEnded` payload retains that same primary code; and
- the result message preserves the original message before appending cleanup
  detail.

Focused tests exercise this exact double failure for both `Faulted` and
`Rejected` command results. Existing port-fault, cancellation, successful
cleanup, and event-sink-failure behavior remains green.

### Scheduling and turn-economy authority remain intact

The team-phase scheduler uses one stable participant ring and skips unavailable
slots without compacting the cursor domain. The Agility scheduler freezes one
validated order per round and applies exact tie-break permutations. New
deployments and changed stats take effect only at the documented boundary.

The cursor independently validates policy, participant, team, round-limit,
revision, step sequence, legal step pairing, and round progression. Accepted
economy evidence accompanies schedule outcomes. Exhausted evidence cannot open
another command window, no-cost consumption cannot mutate the economy, and
configured transition, command-window, free-action, and immediate-repeat bounds
make malformed policies finite.

### Lifecycle, reconciliation, and result boundaries remain intact

Lifecycle operations execute against staged actor graphs and commit only after
returned evidence and current economy authority validate. Cancellation before a
commit restores both actor state and checkpoint-capable lifecycle state.

Departure reconciliation reaches a bounded fixed point, distinguishes Flee,
Roster Recall, and Defeat, and applies one reason for each uninterrupted defeat
period. Completion evaluates detached participant snapshots. Returned encounter
participants are immutable snapshots rather than live actors.

### Automated execution remains canonical

`AutomatedBattleRunner` still composes `BattleEncounterRunner`; it does not own
a second loop. It uses catalog-authorized skills, prepared target identity,
canonical restriction action IDs, the injected lifecycle and turn-economy
rules, team-local encounter knowledge, and asynchronous execution. Victory,
Defeat, Escape, Draw, Faulted, and Cancelled remain distinct projections.

## Documentation Cross-Examination

The three active audience documents now match current source:

- mechanics explains scheduling versus opportunity authority, lifecycle
  timing, cancellation, cleanup, canonical evidence, and primary faults;
- the developer guide names
  `IAutomatedBattleRestrictionActionSource` exactly and describes the sink as a
  fallible observer rather than state authority; and
- the technical reference records event-before-publication ordering, the exact
  `BattleStarted` cleanup boundary, and secondary cleanup-fault behavior.

The public API guide makes the same distinction. No current rule depends on
`DebugText`, display names, host scene objects, or archived implementation.

## Verification

| Gate | Result |
|---|---|
| Focused encounter, automated battle, catalog runtime, preparation, and Godot-contract selection | 296 passed, 0 failed, 0 skipped |
| Complete solution | 1,887 passed, 0 failed, 0 skipped |
| Framework tests | 1,702 passed |
| DemoHost tests | 178 passed |
| Content validator tests | 7 passed |
| Architecture/documentation tests | 57 passed |
| Strict nonincremental Release solution build | 0 warnings, 0 errors |
| Framework trimming-aware build | 0 warnings, 0 errors |
| Formatting verification | Passed |
| Framework coverage | 90.77% lines, 76.74% branches |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions passed |
| DemoHost smoke | Four noninteractive modes and scripted Training Annex play exited 0 |
| Godot 4.7.1 headless smoke | `CONVERGENCE_GODOT_SMOKE_OK`, exit 0 |
| Diff, architecture, links, and active-boundary guards | Passed |

The repository-local Godot executable first crashed inside the restricted file
sandbox because it could not create `user://logs`. Running the same official
4.7.1 console executable with normal user-directory access completed every
Convergence smoke assertion and exited zero. That first engine-environment
failure is not represented as a Framework success or failure.

No dependency or lock file changed in O6-R44 through O6-R47. The connected
NuGet advisory audit remains mandatory in CI; it was not represented as a
successful local online audit in this review.

## Trusted Boundaries And Residual Risk

- Event sinks are fallible observers. A host requiring delivery acknowledgement
  tracks the sequence numbers it actually consumed and reconciles against the
  returned canonical history.
- Turn handlers and state synchronizers remain trusted host mutation ports.
  Their external scene, network, and filesystem effects are not generally
  reversible by the encounter runner.
- Replacement schedulers define recipient semantics inside the validated
  structural and economy-liveness envelope. The Framework does not claim that
  all game-specific schedules are equivalent.
- UI and engine hosts must await `RunAsync`. The synchronous wrapper remains a
  compatibility convenience for callers without synchronization-context
  affinity.

These are explicit integration boundaries, not unresolved Order 6 defects.

## Closure Decision

Order 6 is formally complete after O6-R47. O6-R44 preserves canonical event
identity, O6-R45 preserves primary command-fault authority, O6-R46 reconciles
all active guidance and the public interface name, and this independent
current-source review found no remaining qualifying defect.

`encounter_orchestration` returns to `complete` with no known gap. Its mechanics,
developer, and technical documentation entries return to `reviewed`.
