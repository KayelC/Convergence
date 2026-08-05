# Encounter Orchestration Order 6 O6-R37 Final Closure Review

**Review date:** 5 August 2026
**Source revision reviewed:** `81cdda00`, including the committed O6-R34 and
O6-R35 runtime corrections and the O6-R36 documentation reconciliation
**Review method:** current source and executable tests first; active documents
were checked only after the runtime trace
**Result:** no unresolved realistic reachable encounter-orchestration defect found

## Scope And Independence

This review completes O6-R37. It did not treat the conclusions in the O6-R33
audit or any earlier closure report as proof. The current implementation was
traced from the public encounter request through scheduling, lifecycle,
commands, turn economy, reconciliation, completion, event publication, and
detached results. The three audience documents were then checked against that
trace.

The primary source inspected was:

- `BattleEncounterRunner.cs`;
- `BattleEncounterScheduling.cs`;
- `AgilityOrderedBattleEncounterScheduling.cs`;
- `BattleEncounterPostCommandScheduling.cs`;
- `BattleEncounterLifecycleTransaction.cs`;
- `BattleEncounterEvents.cs`;
- `AutomatedBattleRunner.cs`;
- `AutomatedBattleTurnRestrictionResolver.cs`;
- `BattleTurnEconomy.cs`; and
- `ActionTokenTurnEconomy.cs`.

The review also read the hostile custom-policy tests and the supplied team-phase
and Agility scheduler tests rather than inferring behavior from their names.

## Corrected Invariants Rechecked

### Request graph and initiative

- A request requires valid, unique runtime participant IDs and a stable set of
  participating teams.
- Initiative must return an exact permutation of those teams.
- Scheduler state and steps cannot replace participant, team, policy, or round
  limit identity after the encounter starts.

### No-cost turn-economy authority

- `ActionTurnConsumptionKind.None` requires an exactly equal before/after
  economy snapshot.
- The same rule is enforced both at runner execution and by standalone
  `BattleTurnEconomyChanged` event payload validation.
- Every nonterminal no-cost command advances the consecutive-free-action
  liveness counter based on its typed consumption kind. A custom economy cannot
  spend or mint state to disguise a free action.
- Hostile spending and minting economies fault before turn-end lifecycle,
  accepted economy evidence, or a later command window.

### Scheduler structural continuity

- A fresh schedule starts at revision zero, round one, zero completed rounds,
  and step sequence zero.
- Every accepted transition advances revision and step sequence exactly once.
- Active state satisfies `CompletedRounds == CurrentRound - 1`.
- Non-round-end transitions preserve both round counters.
- Legal structural pairings are enforced before a new cursor is accepted.
  Only `RoundEnded` may complete the current round, start exactly the next
  round, or complete the schedule at the configured limit.
- Round jumps, completed-round jumps, rewinds, illegal pairings, and premature
  completion fault before another command or later lifecycle commit.
- Both supplied schedulers produce transitions accepted by the same validator.

### Lifecycle and cancellation

- Battle-start, turn-start, turn-end, phase-end, round-end, departure, and
  battle-end lifecycle work is staged on detached actor state.
- Actor mutations and lifecycle sequence checkpoints commit only after returned
  evidence, cancellation, and relevant economy authority checks succeed.
- Operational cancellation remains an exception boundary; typed encounter
  cancellation remains a normal terminal outcome.
- The fault-cleanup boundary opens only after the structural `BattleStarted`
  event has been accepted. An event-publication failure before that point does
  not pretend that battle-end cleanup is required.

### Commands, restrictions, and automated execution

- Turn-start restriction decisions are committed once and supplied to the turn
  handler as authoritative input.
- The supplied automated resolver validates canonical typed command identity,
  assessment authority, target shape, and restricted-action eligibility before
  execution.
- Free actions, ordinary consumption, phase termination, and terminal command
  outcomes follow the same encounter runner path.
- Automated battles compose the canonical lifecycle, scheduler, turn economy,
  action executor, and encounter-local knowledge services.

### Reconciliation, completion, events, and results

- Defeat, flee, and roster-recall departures reconcile to a fixed point with one
  explicit reason owning each uninterrupted defeat period.
- Completion results validate outcome and winner shape against the frozen team
  graph; normal terminal results cannot carry fault-only metadata.
- Runner-owned structural events and port-owned gameplay events are separated,
  sequenced, graph-validated, and stored as immutable result evidence.
- Encounter results contain detached participant snapshots rather than live
  actor objects.

## Documentation Cross-Check

The mechanics, developer, and technical encounter documents now agree with the
source trace on:

- scheduler authority versus turn-economy authority;
- no-cost economy immutability and free-action liveness;
- legal round and step continuity;
- transactional lifecycle mutation;
- restriction decision versus enactment ownership;
- reconciliation and departure-reason ownership;
- cancellation and fault behavior;
- event ownership and detached result evidence; and
- the exact structural `BattleStarted` cleanup boundary.

No active audience statement was found that depends on debug text as rule
authority or promises rollback beyond the framework's actual transaction
boundary.

## Trusted Boundaries And Residual Risk

These are explicit integration boundaries, not unresolved Order 6 defects:

- Custom turn handlers and state synchronizers are trusted mutation ports. A
  host that mutates unrelated live state before returning cannot be made
  transactional by the encounter runner.
- An event sink can perform an external side effect before throwing. Framework
  event history remains internally consistent, but external sink effects cannot
  be rolled back.
- Operational cancellation cannot undo mutations already committed by an
  external trusted port before that port observes cancellation.
- Hosts select liveness limits. The framework validates and enforces those
  limits but does not choose product-specific pacing.
- The synchronous `Run` wrappers are compatibility conveniences. UI and engine
  hosts should await the asynchronous APIs.

## Verification

- hostile and supplied-scheduler R37 focus: **197 passed**, 0 failed, 0 skipped;
- full solution: **1,876 passed**, 0 failed, 0 skipped;
- strict .NET 8 Release build: **0 warnings**, **0 errors**;
- Framework coverage: **90.76% lines**, **76.74% branches**;
- formatting verification: passed;
- clean DemoHost modes and scripted Training Annex coverage: passed;
- content validation and framework boundary checks: passed;
- real Godot 4.7.1 .NET headless smoke: passed;
- Framework trimming analysis: passed with 0 warnings;
- `git diff --check`: passed.

Locked restore succeeded. The local machine could not reach NuGet's advisory
service and emitted `NU1900`, so connected CI remains authoritative for the
online vulnerability-data lookup. No package-version or lock-file change was
made.

## Closure Decision

**Order 6 is formally complete.** O6-R34 closes no-cost turn-economy authority,
O6-R35 closes scheduler structural continuity, O6-R36 reconciles the active
contract, and this source-first O6-R37 review found no remaining realistic
reachable defect in the approved encounter-orchestration scope.

`encounter_orchestration` may return to `complete`, and its mechanics,
developer, and technical documentation entries may return to `reviewed`.
