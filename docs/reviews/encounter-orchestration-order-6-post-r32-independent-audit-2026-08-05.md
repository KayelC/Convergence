# Encounter Orchestration Order 6 Post-R32 Independent Audit

**Date:** 5 August 2026
**Review basis:** current source, executable tests, and the three active Order 6
audience documents at `38dd731c`
**Result:** two realistic reachable runtime corrections and one documentation
precision correction are required; Order 6 is reopened

## Review Independence And Method

This audit did not accept an earlier report, roadmap status, test name, or
closure statement as implementation evidence. It traced the currently compiled
source from request validation through initiative, scheduling, lifecycle
transactions, command execution, turn-economy mutation, reconciliation,
completion, event publication, and result construction. Tests were read for
their actual assertions. The audience documents were consulted only after that
source trace, to compare their promises with the implementation found.

Primary implementation examined:

- `BattleEncounterRunner.cs`;
- `BattleEncounterScheduling.cs` and both supplied schedule policies;
- `BattleEncounterEvents.cs`;
- `BattleEncounterLifecycleTransaction.cs` and the status lifecycle port;
- `AutomatedBattleRunner.cs` and its restriction resolver;
- the turn-economy, completion, command-consumption, and event contracts used by
  those components.

Primary executable evidence examined:

- `BattleEncounterRunnerTests.cs`;
- every test under `tests/Convergence.Framework.Tests/Encounters`;
- the catalog battle runtime, lifecycle, event-contract, cancellation, and
  documentation foundation tests.

Audience documents cross-checked:

- `docs/mechanics/encounter-rounds-phases-and-turns.md`;
- `docs/developer-guide/encounter-orchestration.md`;
- `docs/technical/encounter-orchestration-runtime.md`.

Two temporary audit-only regression probes were executed and removed before
this report was written. They changed no retained source. Their permanent forms
belong with the corrections below.

## Findings

### O6-R33-M1: no-cost consumption may mutate turn economy and evade its liveness bound

**Severity:** Medium
**Invariant:** `ActionTurnConsumptionKind.None` is a free action. It must leave
the turn-economy snapshot unchanged and count against the consecutive
free-action limit.

`BattleEncounterRunner.ValidateEconomyTransition` rejects an unchanged snapshot
for every consuming result, but does not enforce the converse: a `None` result
may return a different snapshot. The runner then decides whether an action was
free by comparing the before and after snapshots. A custom public
`IBattleTurnEconomy` can therefore spend or create an opportunity for `None`;
the changed snapshot is accepted, and the consecutive-free-action counter is
reset instead of advanced.

The audit probe supplied an economy with one remaining action whose
`Apply(None)` changed that count to zero. The encounter accepted the transition,
emitted a turn-economy change carrying `None`, skipped owner-turn-end lifecycle,
and completed normally. It did not produce
`TurnEconomyTransitionInvalid`. The broader maximum-command guard still bounds
the phase, so this is not an infinite-loop or security claim. It is a reachable
turn-economy authority failure at a supported extension boundary.

The event contract has the same omission:
`BattleTurnEconomyEventPayloadValidator.ValidateTransition` checks identity and
snapshot type but permits changed before/after state beside `None`. Invalid
authoritative evidence can therefore be constructed independently of the
runner.

**Consequence:** a mistaken custom economy can silently charge a free command,
mint opportunities, and bypass the specific free-action liveness policy. This
contradicts the mechanics statement that a free action does not spend turn
economy.

**Required correction:** require exact before/after equality for `None` in both
the runner and event contract; reject the transition before it becomes accepted
evidence; count free actions from the validated consumption kind rather than
from observed snapshot movement. Add hostile spend/mint probes and exact
lifecycle/event assertions.

### O6-R33-M2: scheduler round drift can commit a command before typed rejection

**Severity:** Medium
**Invariant:** a scheduler transition must preserve the current structural
round until `RoundEnded`, and an invalid transition must not execute another
command or commit a later lifecycle boundary.

`BattleEncounterScheduleTransitionResult.ValidateAdvance` preserves policy and
encounter identity, increments revision and sequence, and validates the next
step against the returned after-state. It does not validate how
`CurrentRound` or `CompletedRounds` may change for the step just completed.
Consequently, after the runner accepts `RoundStarted(1)`, a public custom
scheduler can return an after-state and `PhaseStarted` step for round 2. The
inner runner loop accepts that phase because it checks the step shape and team,
not the captured outer round.

The audit probe continued with a round-2 command window. One command executed
and phase-end lifecycle committed. The runner produced
`ScheduleTransitionInvalid` only when the later `RoundEnded(2)` failed the
outer round-1 closing check. Premature scheduler completion is already rejected
before another command and is not part of this finding.

**Consequence:** an invalid schedule can cross a structural boundary, execute
gameplay, and commit lifecycle mutation before the promised typed rejection.
The supplied team-phase and Agility policies do not do this; the failure is in
validation of the public scheduling extension contract.

**Required correction:** validate structural continuity using the completed
step and the returned state/step before creating the next cursor. Round start,
phase start, command window, phase end, round end, and completion must each have
an explicit legal transition shape. Add round jump, rewind, completed-round
jump, and valid next-round probes that assert no command or lifecycle mutation
occurs before rejection.

### O6-R33-L1: fault-cleanup wording names the wrong battle-start boundary

**Severity:** Low, documentation precision
**Invariant:** audience documentation must identify the actual boundary that
enables fault cleanup.

The runner sets its `battleStarted` authority immediately after successfully
publishing the structural `BattleStarted` event. If the later battle-start
lifecycle port faults, fault finalization still attempts battle-end lifecycle.
The mechanics and developer pages instead say cleanup occurs only when battle
start "had already succeeded," which can reasonably be read as successful
completion of battle-start lifecycle. The technical page's wording, "if battle
start occurred," is closer but does not name the event-publication boundary.

**Consequence:** an integrator implementing lifecycle cleanup from the prose may
expect no battle-end call after battle-start lifecycle failure, while the
runner deliberately calls it once.

**Required correction:** state that successful acceptance/publication of the
structural `BattleStarted` event opens the cleanup boundary; a fault before
that point receives no battle-end lifecycle; a fault during subsequent
battle-start lifecycle does.

## What Is Healthy

The review did not find a qualifying defect in these areas:

- request graph and duplicate runtime-ID validation;
- initiative permutation validation;
- supplied team-phase and Agility scheduling behavior;
- schedule-transition and command-count liveness ceilings;
- lifecycle checkpointing, cancellation-before-commit, and rollback;
- turn-start restriction ownership and canonical restricted-command identity;
- one explicit departure reason per uninterrupted defeat period;
- completion-shape validation and fault-only metadata;
- frozen event identity, actor correlation, and immutable result snapshots;
- operational cancellation versus typed gameplay cancellation;
- canonical asynchronous automated-runner composition.

Custom turn handlers and state synchronizers remain documented trusted mutation
ports. Event-sink side effects also remain host-owned and cannot be rolled back
by the framework. Neither is being reclassified as a defect.

## Documentation Alignment

| Contract | Current source | Current prose | Status |
|---|---|---|---|
| Free action | `None` skips owner-turn-end lifecycle, but changed economy state is accepted | Says free action does not spend economy and is bounded | Misaligned |
| Schedule transition | Identity/revision/sequence are checked; structural round continuity is incomplete | Promises legal step pairings and no command after invalid transition | Misaligned |
| Fault cleanup boundary | Opens after `BattleStarted` publication | Says battle start "succeeded" or "occurred" | Imprecise |
| Supplied schedulers | Team-phase and Agility policies stay within legal rounds | Descriptions match | Aligned |
| Cancellation and results | Typed and operational cancellation remain distinct; results are detached | Descriptions match | Aligned |

All three Order 6 audience entries return to `existing_unreviewed` until the
runtime corrections and wording reconciliation are complete.

## Correction Roadmap

| Checkpoint | Required outcome |
|---|---|
| O6-R33 | Record this source-first audit and reopen executable tracking. |
| O6-R34 | Enforce no-cost turn-economy immutability, event validity, and kind-based free-action liveness with hostile regression tests. |
| O6-R35 | Enforce legal scheduler round/completed-round/step continuity before any next command or lifecycle mutation. |
| O6-R36 | Reconcile mechanics, developer, technical, public API, matrices, and roadmap wording with the corrected contracts. |
| O6-R37 | Independently reread current source and docs, rerun hostile paths and the complete gate, and decide closure without treating this audit as proof. |

## Correction Progress

| Checkpoint | State |
|---|---|
| O6-R33 | `complete` |
| O6-R34 | `complete` |
| O6-R35 | `complete` |
| O6-R36 | `complete` |
| O6-R37 | `pending` |

O6-R34 now requires an unchanged economy snapshot for `None` in both runner
execution and standalone event evidence, and derives nonterminal free-action
liveness from the validated consumption kind. Hostile spending and minting
economies fault before turn-end lifecycle or economy evidence is accepted.

O6-R35 now validates the completed scheduler step against the returned state
and next step before constructing the next cursor. Round jumps,
completed-round jumps, rewinds, illegal step pairings, and premature completion
are rejected before another command or later lifecycle commit. Valid next-round
transitions remain accepted.

O6-R36 reconciles the mechanics, developer, technical, public API, executable
matrix, and roadmap guidance with both corrected contracts and the exact
structural `BattleStarted` cleanup boundary. Formal closure remains reserved
for the independent O6-R37 reread.

## Verification At The Audit Revision

- focused encounter and orchestration tests: **272 passed**, 0 failed, 0 skipped;
- full solution tests: **1,869 passed**, 0 failed, 0 skipped;
- hostile no-cost economy probe: reproduced accepted state mutation;
- hostile scheduler round-drift probe: reproduced command and lifecycle commit
  before typed schedule rejection;
- premature-completion control probe: rejected before command execution;
- audit probes removed; retained worktree restored before documentation edits.

The green suite at the audit revision was meaningful but did not cover the two
reproduced public-extension paths.

## Correction Verification Through O6-R36

- O6-R34 focused encounter and event-contract tests: **164 passed**, 0 failed,
  0 skipped;
- O6-R35 focused runner and scheduler tests: **186 passed**, 0 failed, 0
  skipped;
- full solution after O6-R35: **1,876 passed**, 0 failed, 0 skipped;
- strict .NET 8 Release build: **0 warnings**, **0 errors**;
- format verification and `git diff --check`: passed.

## Closure Decision

**Order 6 is not ready to close.** Its architecture remains sound and the two
reproduced runtime paths are corrected and documented. O6-R37 remains the
required independent source-and-document reread before those corrections are
accepted as formal closure evidence.
