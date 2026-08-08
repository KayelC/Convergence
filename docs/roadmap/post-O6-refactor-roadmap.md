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

### Stage 3 - Complete On 8 August 2026

- **Verified boundary:** before editing, baseline commit `0b174b23` lines
  `1214-1327` were compared directly with post-Stage-2 lines 909-1022. Both
  begin with duplicate-ID validation and end with schedule creation after the
  initial terminal-completion branch; no boundary discrepancy was found.
- **Baseline to destination:** baseline battle-start orchestration at
  `BattleEncounterRunner.cs:1214-1327` moved to
  `EncounterRunContext.RunBattleStartPhaseAsync` at post-stage lines
  1715-1839. `RunCoreAsync` consumes its result at post-stage lines 909-916.
  Private immutable `BattleStartPhaseResult` is at post-stage lines 2472-2489.
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
- **Hazard 1 - duplicate-ID rejection precedes every encounter port call:**
  after context construction, `RunCoreAsync` immediately awaits
  `RunBattleStartPhaseAsync`. Its first operation computes and rejects duplicate
  participant IDs through pre-start finalization. The first ordinary port
  invocation, initiative, remains after that branch; constructing the private
  context and invoker performs no port call.
- **Hazard 2 - initiative cancellation checks:** the explicit cancellation
  check remains immediately before the initiative `PortInvoker.Invoke` call.
  The invoker retains its unchanged before-operation, after-operation,
  exception-filter, and general-catch cancellation checks from Stage 1.
- **Hazard 3 - actor passive reset order:** the lifecycle transaction is still
  created before enumerating its participant snapshot. For each participant,
  cancellation is checked first, passive battle activations are reset second,
  and the matching `ActorCreated` event is awaited third, in the same
  transaction-participant order.
- **Hazard 4 - exact `BattleStarted` assignment point:** the
  `BattleStarted` event is still fully published and recorded first;
  `_runState.BattleStarted = true` remains the next statement; the
  `InitiativeRolled` event remains after the assignment.
- **Hazard 5 - lifecycle transaction commit timing:** the battle-start
  lifecycle still runs against staged participants, its returned events are
  snapshotted and ownership-validated, cancellation is checked, and only then
  is the transaction committed. The same filtered cancellation catch rethrows;
  the same general catch finalizes a typed lifecycle fault without committing.
- **Hazard 6 - initial completion before schedule creation:** committed
  lifecycle events are still added before reconciliation. Reconciliation is
  evaluated next; a complete result is finalized and returned through the
  terminal carrier branch. `StartSchedule()` is called only in the final
  nonterminal return expression, so no schedule is created for an initially
  complete encounter.
- **Event/fault/exception/cancellation evidence:** the extracted method retains
  every event argument and order, fault code and message, lifecycle request,
  catch type and filter, cancellation point, transaction scope and commit,
  reconciliation call, finalization call, and schedule-start call. Captured
  locals changed only to `EncounterRunContext` members. The private result
  carrier has a private constructor and two factories that populate exactly
  one result path; it adds no runtime guard or new fault. Existing duplicate,
  initiative, actor-creation, lifecycle rollback, immediate completion,
  event-publication, and cancellation tests passed unchanged.
- **Deferred observations:** none. `POST-O6-NOTES.md` was not created because
  Stage 3 exposed no suspicious behavior.
- **Commit:** `refactor(encounter-runner): stage 3 - move battle start phase`.

### Stage 4 - Complete On 8 August 2026

- **Verified boundary:** before editing, baseline commit `0b174b23` lines
  `1328-2090` were compared directly with post-Stage-3 lines 917-1679. Both
  are the complete outer round loop, beginning with the expected round-start
  step and ending immediately after boundary-completed schedule advancement.
  Every referenced helper is either an existing `EncounterRunContext` member
  or an existing class-level static helper; no local-helper declaration sits
  outside the approved range.
- **Baseline to destination:** baseline round orchestration at
  `BattleEncounterRunner.cs:1328-2090` moved to
  `EncounterRunContext.RunRoundAsync` at post-stage lines 1088-1852.
  `RunCoreAsync` retains the outer loop and consumes one round result at
  post-stage lines 915-925. Private immutable `RoundRunResult` is at
  post-stage lines 2484-2502.
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
- **Hazard 1 - expected-step validation:** `RunRoundAsync` retains the
  cancellation check followed immediately by the same
  `BattleEncounterRoundStartedScheduleStep` pattern check. Failure still uses
  the same message and `ScheduleTransitionInvalid` typed fault before any
  round number or event mutation.
- **Hazard 2 - final-round assignment:** `round` is still read from the
  accepted round-start step and `_runState.FinalRoundNumber = round` remains
  the immediately following statement, before `RoundStarted` is created.
- **Hazard 3 - round event order:** `RoundStarted` remains after final-round
  assignment and before synchronization or the first schedule advancement.
  At the closing boundary, lifecycle-returned round-end events remain before
  reconciliation, while `RoundEnded` remains after reconciliation and the
  completed-round assignment but before terminal completion or advancement.
- **Hazard 4 - round-end transaction commit timing:** the transaction is still
  created after the round-ended schedule-step validation. Its lifecycle call
  and event snapshot remain inside the same try/catch boundary; the same
  filtered cancellation catch rethrows, the same general catch finalizes a
  typed lifecycle fault, and the explicit cancellation check still occurs
  immediately before `roundEndTransaction.Commit()`.
- **Hazard 5 - reconciliation before `RoundEnded`:** after commit, returned
  lifecycle events are still added first and `ReconcileAsync(null)` is awaited
  second. Only after reconciliation returns are `CompletedRounds` assigned and
  the structural `RoundEnded` event recorded.
- **Hazard 6 - completed-round assignment and schedule advancement:**
  `_runState.CompletedRounds = round` remains before `RoundEnded`. The terminal
  completion branch still follows that event and finalizes without advancing.
  Only the nonterminal path advances the schedule with
  `BoundaryCompleted()`, after which the carrier returns that advanced cursor
  to the unchanged outer loop.
- **Event/fault/exception/cancellation evidence:** a direct mechanical source
  comparison against parent commit `9a444f72` matched all 760 moved body lines
  after only captured-local-to-context-member substitutions. Exactly 32
  terminal returns gained only the `RoundRunResult.Finalized` wrapper. Event
  arguments and order, fault messages and codes, port names, catches and
  filters, cancellation checks, transaction scopes and commits,
  reconciliation, round counters, and schedule transitions otherwise remain
  statement-for-statement identical. The private carrier has a private
  constructor and two either/or factories and adds no validation or fault.
  Existing scheduler drift, lifecycle rollback, event ordering, completion,
  round metadata, round-limit draw, port-fault, and cancellation tests passed
  unchanged.
- **Deferred observations:** none. `POST-O6-NOTES.md` was not created because
  Stage 4 exposed no suspicious behavior.
- **Commit:** `refactor(encounter-runner): stage 4 - move round execution`.

Later completion entries will be appended below this one and will not rewrite
the Stage 0 through Stage 4 evidence.

### Stage 5 - Complete On 8 August 2026

- **Verified boundary:** before editing, baseline commit `0b174b23` lines
  `1352-2025` were compared directly with post-Stage-4 lines `1113-1786`.
  Both are the complete team-phase loop, beginning with the phase-start
  schedule step and ending immediately after boundary-completed phase schedule
  advancement. Every referenced helper is either an existing
  `EncounterRunContext` member or an existing class-level static helper; no
  local-helper declaration sits outside the approved range.
- **Baseline to destination:** baseline team-phase orchestration at
  `BattleEncounterRunner.cs:1352-2025` moved to
  `EncounterRunContext.RunPhaseAsync` at post-stage lines `1192-1867`.
  `RunRoundAsync` consumes one phase result at post-stage lines `1113-1124`.
  Private immutable `PhaseRunResult` is at post-stage lines `2499-2517`.
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
- **Hazard 1 - economy factory/start order:** after the unchanged cancellation
  check, `PortInvoker.Invoke` creates the turn economy through the existing
  factory. The immediately following `PortInvoker.InvokeAction` starts that
  same economy. No snapshot, event, validation, or other operation was inserted
  between factory creation and `StartPhase`.
- **Hazard 2 - initial and final authority validation:** at phase start, the
  economy snapshot, remaining-turn evidence, and `ValidateEconomyState` call
  remain after `StartPhase` and before the accepted state or `PhaseStarted`
  event. At phase end, snapshot capture, remaining-turn evidence, and
  `ValidateEconomyAuthority` remain before lifecycle processing; the
  post-lifecycle `CurrentEconomyAuthorityFault` check remains before
  cancellation and commit. Conditions, fault codes, and terminal paths are
  unchanged.
- **Hazard 3 - phase counter initialization:** `acceptedTurnWindowCount = 0`
  and `consecutiveFreeActions = 0` remain adjacent and in that order,
  immediately after the `PhaseStarted` event and before synchronization or
  schedule advancement.
- **Hazard 4 - phase-end lifecycle commit timing:** the transaction is still
  created after final economy-authority validation. The lifecycle call and
  event snapshot remain inside the same try/catch; the same filtered
  cancellation catch rethrows and the same general catch finalizes a typed
  lifecycle fault. The post-lifecycle economy-authority check remains next,
  followed by the explicit cancellation check and then transaction commit.
  Returned lifecycle events and `PhaseEnded` are recorded only after commit.
- **Hazard 5 - phase completion before schedule advancement:** after phase-end
  events, reconciliation is still evaluated first. A complete result is
  finalized and returned without advancing the schedule. Only the nonterminal
  branch calls `AdvanceSchedule(BoundaryCompleted())`, and that advanced cursor
  is then returned to `RunRoundAsync`.
- **Event/fault/exception/cancellation evidence:** a direct mechanical source
  comparison against parent commit `1aa1c6cb` matched all 670 moved body lines
  after only four-space dedenting. Exactly 28 terminal returns gained only the
  `PhaseRunResult.Finalized` wrapper. Event arguments and order, fault messages
  and codes, port names, catches and filters, cancellation checks, transaction
  scopes and commits, reconciliation, economy snapshots and counters, and
  schedule transitions otherwise remain statement-for-statement identical.
  The private carrier has a private constructor and two either/or factories and
  adds no validation, exception, cancellation, or fault path. Existing economy,
  phase-limit, scheduler, lifecycle rollback, event-order, port-fault, and
  cancellation tests passed unchanged.
- **Deferred observations:** none. `POST-O6-NOTES.md` was not created because
  Stage 5 exposed no suspicious behavior.
- **Commit:** `refactor(encounter-runner): stage 5 - move phase execution`.

Later completion entries will be appended below this one and will not rewrite
the Stage 0 through Stage 5 evidence.

### Stage 6 - Complete On 8 August 2026

- **Verified boundary:** before editing, baseline commit `0b174b23` lines
  `1399-1934` were compared directly with post-Stage-5 lines `1240-1775`.
  Both are the complete scheduled-command-window loop, beginning with the
  command-window schedule step and ending immediately after its final
  `CommandCommitted` schedule advancement. All called helpers are existing
  `EncounterRunContext` members or class-level static helpers; no referenced
  local-helper declaration sits outside the approved range. The immutable
  phase `teamId` remains a separate argument so the existing command-window
  team mismatch validation is preserved rather than reduced to a
  self-comparison.
- **Baseline to destination:** baseline command-window orchestration at
  `BattleEncounterRunner.cs:1399-1934` moved to
  `EncounterRunContext.RunCommandWindowAsync` at post-stage lines `1349-1888`.
  `RunPhaseAsync` owns `EncounterPhaseRunState` and consumes one command-window
  result at post-stage lines `1232-1255`. Private phase state is at post-stage
  lines `2520-2542`; private immutable `CommandWindowRunResult` is at
  post-stage lines `2544-2562`.
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
- **Hazard 1 - unavailable-actor paths do not count alike:** a scheduled actor
  already unavailable still advances with `ActorUnavailable` before the
  accepted-window counter or `TurnStarted` event and returns the advanced phase
  state. An actor made unavailable during turn-start processing still retains
  the prior accepted-window increment, lifecycle work, reconciliation, and
  `TurnEnded(ActorUnavailable)` event before the same schedule outcome. Each
  former `continue` now returns the same mutated phase state to the outer loop;
  neither becomes a committed command.
- **Hazard 2 - turn-start commit and restriction timing:** the accepted-window
  increment and `TurnStarted` event remain before turn-start lifecycle work.
  Staged lifecycle execution, event mapping and ownership validation, economy
  authority validation, explicit cancellation, and transaction commit remain
  in that order. Lifecycle events are recorded after commit; `TurnRestricted`
  and departure reconciliation remain after those events.
- **Hazard 3 - handler ownership validation:** turn-start lifecycle events
  still pass `RequirePortOwned` against staged participants before commit.
  Turn-handler events still pass `RequirePortOwned` inside the invoker callback
  against the request participants and current actor before the callback
  returns. Conditions, owner arguments, port names, and fault boundaries are
  unchanged.
- **Hazard 4 - rejected/cancelled/faulted command handling:** command events
  remain recorded before status dispatch. Cancellation still records
  `TurnEnded(EncounterTerminated)` and finishes as `Cancelled`; fault still
  records that turn end and finalizes `CommandExecutionFaulted`; rejection
  still records `ActionRejected`, then the terminating turn end, then finalizes
  `CommandRejected`. Messages, actor/team payloads, port name, and terminal
  behavior are unchanged.
- **Hazard 5 - economy apply/validation order:** continuity is still checked
  before actor selection. Economy authority remains checked before and after
  handler execution and once more after handler events but before apply. Only
  an accepted command reaches `Apply`; the after snapshot and remaining-turn
  evidence are then captured, and `ValidateEconomyTransition` runs before the
  accepted economy state or counters are updated.
- **Hazard 6 - free-action counting:** both counters are still initialized to
  zero after `PhaseStarted` and before synchronization. Accepted windows still
  increment only after a deployable scheduled actor is accepted. The
  consecutive-free counter increments only for `None` consumption without a
  requested terminal outcome, retains the same limit check, and resets to zero
  for every other accepted command.
- **Hazard 7 - turn-end lifecycle commit timing:** turn-end lifecycle remains
  conditional on non-`None` consumption. Its lifecycle call and event snapshot
  remain inside the same try/catch; filtered cancellation still rethrows and
  ordinary failure still finalizes a lifecycle fault. Economy authority is
  checked after lifecycle execution, followed by explicit cancellation,
  transaction commit, and only then lifecycle-event recording.
- **Hazard 8 - turn-economy event order, reconciliation, and terminal checks:**
  post-lifecycle economy authority remains before `TurnEconomyChanged`.
  Reconciliation remains after that event, followed by another authority
  check and `TurnEnded(CommandCommitted)`. A requested outcome is still tested
  before reconciled completion; both terminal branches remain before schedule
  advancement.
- **Hazard 9 - schedule advancement:** the two unavailable paths still advance
  with `ActorUnavailable` at their original points. The normal path advances
  with `CommandCommitted` only after economy application, lifecycle, events,
  reconciliation, authority checks, turn end, and terminal checks. The returned
  phase state is reevaluated by the unchanged command-window loop; phase
  boundary advancement remains in `RunPhaseAsync` after phase completion.
- **Event/fault/exception/cancellation evidence:** a direct mechanical source
  comparison against parent commit `e5e1fb2d` matched all 532 moved body lines
  after reversing only four-space dedenting, six phase-state member
  categories, 22 terminal carrier wrappers, and the two continuation
  carrier returns. Event constructions and ordering, fault messages and codes,
  port names, catches and filters, explicit cancellation checks, lifecycle
  transaction scopes and commits, economy operations, reconciliation calls,
  counters, and scheduler outcomes otherwise remain statement-for-statement
  identical. The private state and result carrier add no validation, exception,
  cancellation, event, or fault path. Existing restriction, ownership,
  command-status, economy, free-action, lifecycle, departure, scheduler,
  port-fault, and cancellation tests passed unchanged.
- **Deferred observations:** none. `POST-O6-NOTES.md` was not created because
  Stage 6 exposed no suspicious behavior.
- **Commit:** `refactor(encounter-runner): stage 6 - move command window execution`.

Later completion entries will be appended below this one and will not rewrite
the Stage 0 through Stage 6 evidence.
