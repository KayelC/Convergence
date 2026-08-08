# Post-O6 Encounter Runner Refactor Roadmap

## Status

**Approved on 8 August 2026. Stages 0 and 1 are complete; Stage 2 has not begun
and remains gated by the per-stage owner approval rule.**

This roadmap governs a mechanical decomposition of
`BattleEncounterRunner.RunCoreAsync`. It does not reopen Order 6 mechanics and
does not authorize a bug fix, rule change, API redesign, test rewrite, or
modernization.

## Verified Baseline

The supplied request named `6805ae40` as O6-R51. Repository history shows that
`6805ae40` is the earlier `docs: reconcile encounter command transactions`
commit. The actual O6-R51 closure and current `HEAD` are:

- commit: `0b174b23 docs: close encounter orchestration after r51 review`;
- branch: `main`;
- worktree before planning: clean;
- target: `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs`;
- target method at this baseline: lines 890-2530;
- baseline test gate: 1,888 passed, 0 failed, 0 skipped;
  - Framework: 1,703;
  - DemoHost: 178;
  - ContentValidator: 7.

The baseline remains `0b174b23` throughout this refactor. Each stage compares
against that source even though later stages necessarily use shifted line
numbers.

## Non-Negotiable Parity Contract

Every stage must preserve the following exactly:

- conditional branches and operation order;
- cancellation-check placement relative to each observable operation;
- exception types, messages, filters, and typed-fault conversion;
- event kinds, payloads, ordering, sequence numbers, recording order, and
  publication order;
- lifecycle transaction creation, commit, rollback, and cleanup timing;
- schedule, turn-economy, reconciliation, completion, and terminal-result
  behavior;
- public and internal framework contracts.

Stack-frame method names and source line numbers are the sole unavoidable
exception: extracting code into named methods necessarily changes that
diagnostic metadata. Stack traces are not treated as a supported behavior
contract; exception type, message, inner exception, timing, and fault evidence
remain protected.

No existing test or assertion may be modified to make a stage pass. No new
behavior test is part of this mechanical refactor. A suspicious observation is
recorded in `docs/reviews/POST-O6-NOTES.md` and left untouched.

## Extraction Design

All new implementation types remain `private sealed` nested types of
`BattleEncounterRunner`. They do not enter the public or internal API surface.

- `EncounterRunState` owns only mutable method-wide run state.
- `EncounterPortInvoker` owns the existing port-call exception and
  cancellation boundary.
- `EncounterRunContext` owns immutable run dependencies and the extracted
  private operations that currently capture them.
- Private result carriers distinguish an advanced schedule from a terminal
  `BattleEncounterResult`; they add no new validation or behavior.
- `EncounterPhaseRunState` is introduced only if the command-window extraction
  reaches its approved stage. It gives the existing phase-local counters,
  economy snapshot, and schedule cursor a direct home.

Request, services, and cancellation are immutable run dependencies, not global
state. Schedule cursors and actor-turn temporaries remain local to the narrowest
extracted operation that owns them.

## Baseline Mapping

| Baseline lines | Existing responsibility | Planned home |
|---|---|---|
| 901-913 | Method-wide mutable run state | `EncounterRunState` |
| 915-1015 | Port invocation and event publication | `EncounterPortInvoker` |
| 1017-1051 | Event creation and sequencing | `EncounterRunContext` event methods |
| 1053-1212 | Departure and schedule helpers | `EncounterRunContext` reconciliation/schedule methods |
| 1214-1327 | Validation, initiative, actor creation, battle-start lifecycle, initial completion, schedule start | `RunBattleStartPhaseAsync` |
| 1328-1351, 2027-2090 | One round's start/end shell | `RunRoundAsync` |
| 1352-2025 | One team phase, including its command-window loop | `RunPhaseAsync` |
| 1399-1934 | One scheduled command window | `RunCommandWindowAsync` |
| 2098-2528 | Reconciliation, synchronization, economy capture, port-fault and ordinary-fault finalization, cleanup, successful finalization, lifecycle failure formatting, completion validation, and lifecycle-event snapshotting | `EncounterRunContext` shared and finalization methods |
| 1328, 2090-2096 | Repeated-round control and draw completion | `RunScheduledRoundsAsync` |

The line ranges overlap where a later stage extracts code from a method created
by an earlier stage. The final completion record will add actual destination
line numbers without replacing these baseline coordinates.

## Ordered Stages

### Stage 0 - Give Mutable Run State One Home

**Extraction:** Replace the ten method-wide mutable locals at baseline lines
901-913 with one `EncounterRunState` instance. Its constructor initializes the
event list, defeat sets, counters, flags, and empty team order in the same order
and only after the existing argument and cancellation checks.

**State required:** participants only for the existing initial
`ProcessedDefeatDepartures` projection. Request, services, cancellation token,
schedule, economy, phase, and actor-turn values do not become state members.

**Done test:** the method remains monolithic; the diff consists of state
construction, direct member substitutions, and the private nested state type.
All existing tests pass unchanged, with focused confirmation of sequence,
defeat reconciliation, round metadata, battle-end cleanup, and cancellation.

**Commit:** `refactor(encounter-runner): stage 0 - move run state`

### Stage 1 - Isolate The Existing Port Boundary

**Extraction:** Move baseline lines 915-1015 into
`EncounterPortInvoker.Invoke`, `InvokeAction`, `InvokeAsync`,
`InvokeTaskAsync`, and `PublishAndRecordAsync`.

**State required:** cancellation token, event sink, run event list, and the
existing port-failure finalization delegate.

**Parity hazards held fixed:** cancellation before and after every port call;
the cancellation exception filter; the second cancellation check in the
general catch; `BattleEncounterPortException` construction; adding an event to
the result stream before attempting publication.

**Done test:** no port call changes order or fault code. Existing event-sink,
port-exception, pre-cancel, in-flight cancellation, and primary-fault
preservation tests pass unchanged.

**Commit:** `refactor(encounter-runner): stage 1 - move port invocation`

### Stage 2 - Extract Shared Run Operations

**Extraction:** Introduce `EncounterRunContext` and move the existing event,
departure, schedule, reconciliation, synchronization, economy-capture,
lifecycle-event snapshot, finalization, lifecycle-failure-formatting, and
completion-validation local functions from baseline lines 1017-1212 and
2098-2528 into it. The main encounter flow remains in `RunCoreAsync`.

The original roadmap accidentally omitted baseline lines 2458-2514 even though
`LifecycleFailureMessage` and `ValidateCompletion` are dependencies of the
already-approved moved methods. The owner approved the corrected contiguous
2098-2528 boundary before Stage 2 source editing began.

This dependency-preparation stage is required by the actual source and was not
explicit in the suggested stage list. Battle start, rounds, phases, and command
windows all call these same closures; extracting a phase first would otherwise
require a large delegate bundle or duplicate authority.

**State required:** immutable request, services, and cancellation token;
`EncounterRunState`; and `EncounterPortInvoker`. The invoker receives the
context's same port-failure finalization method as its callback, preserving the
current `RunAsync` catch boundary.

**Parity hazards held fixed:** direct final-event recording after publication
failure; progressive disabling of publication during fault finalization;
battle-end lifecycle's once-only flag; cleanup event ordering; reconciliation
pass bounds; schedule transition budget timing.

**Done test:** main-flow statements remain in their original order and only
call context methods. All existing cleanup, rollback, scheduler, liveness,
economy-authority, completion, and event-publication tests pass unchanged.

**Commit:** `refactor(encounter-runner): stage 2 - move shared run operations`

### Stage 3 - Extract Battle Start

**Extraction:** Move baseline lines 1214-1327 to
`EncounterRunContext.RunBattleStartPhaseAsync`. A private immutable
`BattleStartPhaseResult` returns exactly one of an initialized schedule cursor
or an already-finalized terminal encounter result.

**State required:** request participants, initiative and lifecycle services,
run state, event methods, synchronizer, reconciliation, schedule start, and
finalization methods.

**Parity hazards held fixed:** duplicate-ID rejection precedes every encounter
port; initiative cancellation checks; actor passive reset order; the exact
point where `BattleStarted` becomes true; lifecycle transaction commit timing;
initial completion before schedule creation.

**Done test:** `RunCoreAsync` calls one startup method and immediately returns
its terminal result when present. Duplicate, initiative, actor-creation,
battle-start lifecycle, immediate draw/victory, and cancellation tests pass
unchanged.

**Commit:** `refactor(encounter-runner): stage 3 - move battle start phase`

### Stage 4 - Extract One Round

**Extraction:** Move the one-round body at baseline lines 1328-2090 into
`RunRoundAsync`. A private immutable `RoundRunResult` carries either the
advanced schedule or an already-finalized encounter result.

**State required:** schedule cursor, shared context, and run state. Round number,
round lifecycle events, and transaction values remain method-local.

**Parity hazards held fixed:** expected-step validation, final-round assignment,
round event order, round-end transaction commit, reconciliation before
`RoundEnded`, completed-round assignment, and schedule advancement.

**Done test:** the outer loop remains in `RunCoreAsync` and invokes exactly one
round per iteration. Round drift, lifecycle rollback, round metadata,
completion, and round-limit draw tests pass unchanged.

**Commit:** `refactor(encounter-runner): stage 4 - move round execution`

### Stage 5 - Extract One Team Phase

**Extraction:** Move baseline lines 1352-2025 from `RunRoundAsync` into
`RunPhaseAsync`. It owns economy creation/start, phase events, command-window
iteration, phase-end lifecycle, reconciliation, and phase schedule advancement.

**State required:** phase-start step, schedule cursor, team ID, turn economy,
accepted economy snapshot, phase counters, shared context, and run state.

**Parity hazards held fixed:** economy factory/start order; initial and final
authority validation; phase counter initialization; phase-end lifecycle commit;
phase completion before schedule advancement.

**Done test:** `RunRoundAsync` controls round boundaries and delegates each
phase without changing the number or order of scheduler transitions. Economy,
phase-limit, scheduler, phase-lifecycle, and event-order tests pass unchanged.

**Commit:** `refactor(encounter-runner): stage 5 - move phase execution`

### Stage 6 - Extract One Scheduled Command Window

**Extraction:** Move baseline lines 1399-1934 into
`RunCommandWindowAsync`. This is cleanly separable after Stage 5, but the
accurate unit is a command window rather than `RunTurnAsync`: an unavailable
scheduled actor can advance the schedule without an actor turn beginning.

`EncounterPhaseRunState` holds only the existing mutable phase locals:
schedule, turn economy, accepted economy state, remaining-turn evidence,
accepted-window count, and consecutive-free-action count.

**State required:** one command-window step, phase run state, shared context,
and run state. Actor, lifecycle transaction, restriction, command, and
before/after economy values remain local to one invocation.

**Parity hazards held fixed:** unavailable-actor paths do not count alike;
turn-start commit and restriction timing; handler ownership validation;
rejected/cancelled/faulted command handling; economy apply/validation order;
free-action counting; turn-end lifecycle commit; turn-economy event order;
reconciliation and terminal checks; schedule advancement.

**Done test:** one invocation handles exactly one schedule command-window step
and returns either an advanced phase state or a terminal result. All turn,
restriction, command, economy, lifecycle, departure, cancellation, ownership,
and safety-limit tests pass unchanged.

**Commit:** `refactor(encounter-runner): stage 6 - move command window execution`

### Stage 7 - Leave A Thin Orchestrator

**Extraction:** Move repeated-round control and final draw completion to
`RunScheduledRoundsAsync`. `RunCoreAsync` retains, in order: cancellation,
argument validation, state/context construction, battle-start invocation,
terminal startup return, and scheduled-round invocation.

**State required:** initialized schedule and shared context.

**Done test:** `RunCoreAsync` is a short readable orchestration path with no
rule logic and no captured local functions. The mapping table is updated with
actual destinations; `POST-O6-NOTES.md` is finalized; every test remains
unchanged and passes.

**Commit:** `refactor(encounter-runner): stage 7 - move scheduled round loop`

## Per-Stage Verification And Stop Rule

Each stage is one isolated commit and ends the work turn. The next stage does
not begin without explicit owner instruction.

Before each commit:

1. Confirm only the approved stage, this roadmap's completion entry, and any
   observation-only note changed.
2. Confirm no test source or assertion changed.
3. Review the diff against the baseline mapping and account for every moved
   statement, cancellation check, catch/filter, message, fault code, and event
   construction.
4. Run the 168 existing `BattleEncounterRunnerTests` cases.
5. Run `dotnet test Convergence.sln --no-restore`; no baseline test may be lost,
   skipped, or changed.
6. Run a strict nonincremental solution build, formatting verification,
   `git diff --check`, and the existing public/API boundary tests.
7. Add a short stage completion entry below recording:
   - baseline range to actual destination;
   - changed files;
   - focused and full test totals;
   - explicit evidence for event, fault, exception, and cancellation parity;
   - any suspicious observation deferred to `POST-O6-NOTES.md`.
8. Commit with the exact stage message above, then stop.

Passing tests alone are not the parity proof. The proof is the combination of
unchanged assertions, 1:1 source mapping, a reviewed control-flow diff, targeted
contract tests, and the full unchanged suite.

## Scope Guard

The implementation may change only:

- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs`;
- this roadmap and its required roadmap index entry;
- `docs/reviews/POST-O6-NOTES.md` and its required reviews index entry.

No call-site change is currently required: all extracted types and methods are
private implementation details and `Run`, `RunAsync`, and `IBattleEncounterRunner`
remain unchanged. If compilation later proves an external file must change,
that stage stops before editing and records the proposed exception for owner
approval.

## Completion Record

### Stage 0 - Complete On 8 August 2026

- **Baseline to destination:** baseline mutable declarations at
  `BattleEncounterRunner.cs:901-913` moved to private nested
  `EncounterRunState` at post-stage lines 2519-2548. The replacement
  construction remains at post-stage line 901, after the unchanged checks at
  lines 895-899.
- **Changed files:**
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs` and this
  roadmap. No test, assertion, API baseline, call site, or other source file
  changed.
- **Focused verification:** 168 passed, 0 failed, 0 skipped in the unchanged
  `BattleEncounterRunnerTests` suite.
- **Full verification:** 1,888 passed, 0 failed, 0 skipped: Framework 1,703,
  DemoHost 178, ContentValidator 7.
- **Additional gates:** strict nonincremental solution build succeeded with
  0 warnings and 0 errors; `dotnet format --verify-no-changes` succeeded; all
  6 public-API boundary tests passed; `git diff --check` succeeded.
- **Event parity:** the bodies and call sites of `AddAsync`,
  `AddTurnEconomyAsync`, `AddRangeAsync`, `PublishAndRecordAsync`, and final
  event append remain in place. Their four sequence increments and two event
  list writes changed only from a captured local to the corresponding state
  member. Existing typed-event ordering, sequence, successful cleanup ordering,
  and event-publication-failure tests passed unchanged.
- **Fault parity:** no condition, fault code, payload, message, catch, or
  finalization statement moved. `BattleStarted`,
  `BattleEndLifecycleAttempted`, team order, defeat-period sets, and round
  counters are read and written at their original statement positions through
  direct state members. Existing primary-fault preservation, cleanup-failure,
  scheduler, completion, and economy-authority tests passed unchanged.
- **Exception parity:** `EncounterRunState` adds no guard or conversion. It
  performs the original allocations, defeated-participant projection, and
  explicit initial assignments in their original order. The existing
  validation and port/lifecycle exception tests passed unchanged.
- **Cancellation parity:** every cancellation call remains at its baseline
  control-flow position. In particular, the pre-cancel check still precedes
  argument validation and state construction, and construction still precedes
  the first port invocation. All focused pre-start, actor-creation,
  turn-economy, handler, round-end, and battle-end cancellation tests passed
  unchanged.
- **Deferred observations:** none. `POST-O6-NOTES.md` was not created because
  Stage 0 exposed no suspicious behavior.
- **Commit:** `refactor(encounter-runner): stage 0 - move run state`.

### Stage 1 - Complete On 8 August 2026

- **Verified base:** before editing, the independently rerun unchanged Stage 0
  tree reproduced 168 focused passes and all 1,888 solution passes with no
  failures or skips.
- **Baseline to destination:** baseline port-boundary functions at
  `BattleEncounterRunner.cs:915-1015`, corresponding to post-Stage-0 lines
  903-1003, moved to private nested `EncounterPortInvoker` at post-stage lines
  2422-2542. Existing call sites now use the `portInvoker` instance.
- **Changed files:**
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs` and this
  roadmap. No test, assertion, API baseline, call site outside the target
  method, or other source file changed.
- **Focused verification:** 168 passed, 0 failed, 0 skipped in the unchanged
  `BattleEncounterRunnerTests` suite.
- **Full verification:** 1,888 passed, 0 failed, 0 skipped: Framework 1,703,
  DemoHost 178, ContentValidator 7.
- **Additional gates:** strict nonincremental solution build succeeded with
  0 warnings and 0 errors; `dotnet format --verify-no-changes` succeeded; all
  6 public-API boundary tests passed; `git diff --check` succeeded.
- **Hazard 1 - cancellation checks before and after every port call:**
  `Invoke` and `InvokeAsync` retain the cancellation check immediately before
  their `try`, execute the supplied operation once, then retain the check
  immediately after that operation. `InvokeAction` still delegates to
  `Invoke`; `InvokeTaskAsync` still delegates to `InvokeAsync`.
- **Hazard 2 - cancellation exception filter:** both invocation paths retain
  `catch (OperationCanceledException) when
  (_cancellationToken.IsCancellationRequested)` and rethrow unchanged. No
  cancellation-shaped exception classification changed.
- **Hazard 3 - second cancellation check in the general catch:** both general
  `catch (Exception exception)` blocks still call
  `_cancellationToken.ThrowIfCancellationRequested()` before constructing a
  typed port exception.
- **Hazard 4 - `BattleEncounterPortException` construction:** both paths pass
  the same ordered arguments: caller-supplied fault code, caller-supplied port
  name, optional actor ID, caught inner exception, and the same
  `FinalizePortFailureAsync` method captured once as the invoker's finalizer
  delegate. No port name, fault code, message, or wrapping path changed.
- **Hazard 5 - record before publication:**
  `PublishAndRecordAsync` retains `_events.Add(battleEvent)` as its first
  statement and only then awaits `InvokeTaskAsync` for `event-publication`.
  The event therefore remains in the encounter result if publication fails.
- **Event/fault/exception/cancellation evidence:** the control-flow diff is a
  1:1 move of all five bodies plus receiver-only call-site changes. Existing
  pre-start and active-battle event-sink failure, canonical-event retention,
  primary-fault preservation, cleanup-failure, port exception, unsignalled
  cancellation, and every focused cancellation test passed unchanged.
- **Deferred observations:** none. `POST-O6-NOTES.md` was not created because
  Stage 1 exposed no suspicious behavior.
- **Commit:** `refactor(encounter-runner): stage 1 - move port invocation`.

### Stage 2 - Complete On 8 August 2026

- **Verified base:** before editing, the independently rerun unchanged Stage 1
  tree reproduced 168 focused passes and all 1,888 solution passes with no
  failures or skips.
- **Baseline to destination:** baseline shared-operation functions at
  `BattleEncounterRunner.cs:1017-1212` and the owner-approved corrected
  contiguous range `2098-2528` moved to private nested `EncounterRunContext`
  at post-stage lines 1821-2449. `RunCoreAsync` constructs that context at
  post-stage lines 901-907 and retains the battle-start and repeated-round
  control flow.
- **Changed files:**
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs` and this
  roadmap. No test, assertion, API baseline, external call site, notes file, or
  other source file changed.
- **Focused verification:** 168 passed, 0 failed, 0 skipped in the unchanged
  `BattleEncounterRunnerTests` suite.
- **Full verification:** 1,888 passed, 0 failed, 0 skipped: Framework 1,703,
  DemoHost 178, ContentValidator 7.
- **Additional gates:** strict nonincremental solution build succeeded with
  0 warnings and 0 errors; `dotnet format --verify-no-changes` succeeded; all
  6 public-API boundary tests passed; `git diff --check` succeeded.
- **Hazard 1 - direct final-event recording after publication failure:**
  `AppendFinalEventAsync` still increments the shared sequence and appends the
  sequenced event to `_runState.Events` before checking whether publication is
  enabled and before awaiting the event sink. Fault and terminal events remain
  present in the result even after publication fails.
- **Hazard 2 - progressive publication disable during fault finalization:**
  `publishDuringFinalization` still begins with the caller's `publishEvents`
  value. A non-cancellation publication exception still performs the second
  cancellation check and then changes only that local flag to `false`, so all
  later final events are recorded without another publication attempt.
- **Hazard 3 - once-only `BattleEndLifecycleAttempted`:** successful
  `FinishAsync` still sets the flag immediately before its one battle-end
  lifecycle attempt. Fault finalization still tests `BattleStarted &&
  !BattleEndLifecycleAttempted` and sets the flag before invoking cleanup.
  A failed successful-finalization lifecycle therefore cannot be invoked a
  second time by `FinalizeFailureAsync`.
- **Hazard 4 - cleanup event ordering:** fault finalization still records the
  primary `BattleFaulted` event first, then any cleanup-failure
  `BattleFaulted` event, then lifecycle-returned battle-end events in their
  supplied order, and finally the terminal `BattleEnded` event. Successful
  finalization still records lifecycle-returned events before `BattleEnded`.
- **Hazard 5 - reconciliation pass bounds:** reconciliation retains
  `for (int pass = 0; pass <= _request.Participants.Count; pass++)`, clears the
  one-shot explicit departure after the first pass, breaks on no departures,
  and raises the stability fault only when departures remain on
  `pass == _request.Participants.Count`.
- **Hazard 6 - schedule transition budget timing:** `StartSchedule` still
  consumes the budget before constructing or invoking schedule start.
  `AdvanceSchedule` still validates its arguments, consumes the budget, and
  only then reads the completed step and invokes schedule advance. The budget
  body still checks `>= MaximumScheduleTransitions` before the same checked
  increment.
- **Event/fault/exception/cancellation evidence:** every moved body is a 1:1
  extraction whose captured `request`, `services`, `cancellationToken`,
  `runState`, and `portInvoker` references changed only to context members.
  Main-flow call sites changed only by adding the `runContext` receiver (or the
  enclosing context type for the static lifecycle message formatter). Event
  sequence increments, event writes, fault codes, port names, finalizer
  arguments, catch types and filters, cancellation checks, lifecycle
  transactions, commit points, and returned result construction remain in the
  same statement order. Existing event-publication, cleanup, fault-authority,
  exception-containment, cancellation, reconciliation, scheduler, lifecycle,
  and completion tests passed unchanged.
- **Deferred observations:** none. `POST-O6-NOTES.md` was not created because
  Stage 2 exposed no suspicious behavior.
- **Commit:** `refactor(encounter-runner): stage 2 - move shared run operations`.

Later completion entries will be appended below this one and will not rewrite
the Stage 0, Stage 1, or Stage 2 evidence.
