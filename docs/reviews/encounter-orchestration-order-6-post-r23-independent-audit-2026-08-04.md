# Encounter Orchestration Order 6 Post-R23 Independent Audit

**Date:** 4 August 2026

**Capability:** `encounter_orchestration`

**Reviewed revision:** `0780e285`

**Review checkpoint:** O6-R24

**Result:** one runtime correction and one documentation correction required;
Order 6 is reopened

## Findings

### O6-R24-M1: successful terminal messages populate fault-only result metadata

**Severity:** Medium integration correctness

**Invariant:** `BattleEncounterResult.FaultMessage` and `FaultCode` describe a
faulted encounter. A normal `Victory`, `Defeat`, `Escape`, `Draw`, or
`Cancelled` result must not carry fault metadata merely because its completion
path supplied optional explanatory text.

**Reachable path:** every encounter that reaches its configured round limit
calls `FinishAsync(Draw, ..., "Battle ended in a draw ...")`. A replacement
`IBattleEncounterCompletionPolicy` can also provide a normal terminal
`BattleEncounterCompletion.Message` for any supported non-fault outcome.

**Current behavior:**

- [`BattleEncounterResult`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L140)
  exposes only `FaultMessage` for result-level text;
- [`BattleEncounterCompletion`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L697)
  exposes a neutral optional `Message` for normal completion;
- the round-limit branch always supplies such a normal message at
  [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2061);
- [`FinishAsync`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2383)
  correctly uses that value as optional `BattleEnded` debug text, but then
  passes the same value into the result's `faultMessage` constructor argument
  at
  [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2403);
  and
- [`AutomatedBattleRunner`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L560)
  copies the misleading value into `AutomatedBattleResult.FaultMessage`.

The existing public round-limit regression at
[`CatalogBattleRuntimeTests.cs`](../../tests/Convergence.Framework.Tests/SkillSystem/CatalogBattleRuntimeTests.cs#L1454)
asserts `Draw` and no winner, but does not assert that fault fields are absent.

**Consequence:** a Godot or other host can correctly receive `Outcome == Draw`
while also receiving a non-null `FaultMessage`. A host that logs, displays, or
telemeters the explicitly named fault property will falsely report an ordinary
draw or custom normal completion as a failure. Gameplay state remains valid,
but the public result contract gives contradictory integration evidence.

**Required correction:** keep ordinary completion text in the optional
non-authoritative `BattleEnded` debug text, or add a clearly neutral completion
message contract if hosts need result-level access. Populate `FaultMessage` and
`FaultCode` only for `Faulted`, enforce coherent terminal result shapes, and add
direct canonical and automated regressions for round-limit and custom-message
normal completions.

This is not a security vulnerability. It is a realistic host-integration bug
on a standard, supported path.

### O6-R24-L1: two audience statements overstate publication and cleanup

**Severity:** Low documentation correctness

**Invariant:** the audience documents must distinguish returned event history
from successful sink publication and must describe cleanup timing with the
same battle-start condition enforced by the runner.

**Current behavior and discrepancy:**

1. [`encounter-orchestration-runtime.md`](../technical/encounter-orchestration-runtime.md#L261)
   says that the result retains the exact events published to the sink. The
   same page correctly explains later that final fault evidence can remain in
   the result without publication when the sink itself fails. The absolute
   earlier sentence is therefore false for that supported fault path.
2. [`encounter-rounds-phases-and-turns.md`](../mechanics/encounter-rounds-phases-and-turns.md#L152)
   says every port fault attempts battle-end cleanup once. The runner attempts
   that cleanup only after battle start occurred, which the technical page
   states correctly.

**Consequence:** an integrator can assume that every result event was observed
by its sink, or that a startup-port failure invoked battle-end lifecycle even
though battle-start ownership was never established. Neither error changes
runtime state, but both blur important host recovery boundaries.

**Required correction:** state that the result owns the canonical sequenced
event history; normally it matches successful sink publication, while
sink-failure finalization may append result-only evidence. Qualify fault cleanup
as occurring once only after battle start was accepted.

## Source-First Method

This audit did not use earlier review conclusions or completion summaries as
proof. It established behavior from current source first, then read executable
tests, and only then compared the three audience documents.

The source trace covered:

- request construction, duplicate runtime-ID rejection, and frozen participant
  identity;
- initiative, team-phase and Agility schedule contracts, schedule revision
  continuity, post-command follow-ups, and liveness budgets;
- battle-start, turn-start, action-end, owner-turn-end, phase, round, departure,
  and battle-end lifecycle transactions;
- command status and outcome shapes, turn-economy authority, event provenance,
  reconciliation, revival-aware defeat periods, and completion;
- typed command cancellation, operational cancellation, port containment, and
  failed-sink finalization;
- detached completion inputs, detached result participants, typed immutable
  events, and host synchronization boundaries; and
- `AutomatedBattleRunner`, action authorization, restrictions, team-local
  knowledge, canonical action execution, and terminal mapping.

## Confirmed Healthy Invariants

The current source and focused tests support the following conclusions:

- both supplied schedulers consume detached inputs and validate their returned
  policy, participant, team, sequence, and revision identities;
- Agility order is frozen for one round while deployment and defeat eligibility
  are refreshed before command windows;
- the runner, not a scheduler or port, remains the sole structural event and
  accepted turn-economy authority;
- lifecycle work stages actor state and lifecycle sequence counters together,
  then commits only after cancellation and event validation checks;
- reconciliation reaches a bounded fixed point, processes one cleanup and one
  announcement per uninterrupted defeat period, and permits a later defeat
  after revival;
- zero surviving teams complete as `Draw`, while one surviving team completes
  as `Victory`;
- typed command cancellation and `CancellationToken` cancellation remain
  distinct contracts;
- port exceptions are contained as typed encounter faults, subject to the
  documented limitation that committed external side effects cannot be
  generically rolled back; and
- the automated runner composes `BattleEncounterRunner` rather than maintaining
  a second encounter loop.

No scheduler deadlock, lifecycle rollback defect, duplicate structural-event
authority, mutable result participant, or cancellation-state corruption was
reproduced in the reviewed supported paths.

## Test-Gap Assessment

The encounter tests are broad and adversarial. They cover both schedulers,
schedule identity, free-action and command liveness, economy drift, lifecycle
rollback, cancellation stages, port exceptions, event ownership, frozen actor
identity, mutual defeat, revival and re-defeat, terminal mappings, and automated
authorization.

The material gap is narrow: successful completion paths are not required to
have null fault metadata. Add explicit assertions for:

- canonical round-limit `Draw`;
- automated round-limit `Draw`;
- a custom normal completion carrying optional debug text; and
- a true fault retaining both fault code and fault message.

## Trusted Boundaries And Residual Risk

These are deliberate boundaries, not findings:

- command handlers and state synchronizers are trusted host ports over live
  encounter participants;
- an event-sink failure cannot undo a gameplay transition that committed before
  publication;
- custom lifecycle, scheduler, economy, and completion implementations must
  avoid external side effects that the framework cannot transactionally own;
- `BattleStatusEncounterLifecyclePort` owns mutable lifecycle-clock sequence
  state and must not be shared by overlapping encounters; and
- synchronous wrappers clear the current synchronization context defensively,
  but engine and UI hosts should use the asynchronous APIs.

## Correction Roadmap

| Checkpoint | State | Required work |
|---|---|---|
| O6-R24 | `complete` | Perform this source-first code and documentation audit and record reproducible evidence. |
| O6-R25 | `complete` | Separate ordinary completion text from fault metadata and add canonical plus automated result-shape regressions. |
| O6-R26 | `complete` | Correct event-publication and conditional battle-end-cleanup wording across all three audiences and executable documentation evidence. |
| O6-R27 | `complete` | Re-read corrected source and documents independently, run the complete gate, and restore formal closure. |

Each correction should remain an isolated green commit. Findings become
verified only after O6-R27.

## Correction Progress After This Audit

| Checkpoint | Evidence |
|---|---|
| O6-R25 | Commit `ea1fb240` keeps normal completion text on `BattleEnded.DebugText`, enforces winner and fault-field result shapes, and adds canonical plus automated round-limit and custom-message regressions. The checkpoint passed 276 focused tests, 1,865 full-suite tests, a zero-warning strict build, and formatting verification. |
| O6-R26 | The three audience documents now distinguish canonical result history from successful sink delivery, condition fault cleanup on accepted battle start, and state that fault fields are fault-only. Public API guidance and executable documentation evidence carry the same contract. |
| O6-R27 | The [final closure review](encounter-orchestration-order-6-r27-final-closure-review-2026-08-04.md) independently traced current source and all audiences, corrected completion-policy fault wording, recorded trusted host-port atomicity, and found no unresolved realistic reachable runtime defect. |

## Verification At This Audit

- focused encounter, scheduler, and automated tests: **275 passed**, 0 failed,
  0 skipped;
- focused encounter plus documentation-ledger gate after recording the audit:
  **287 passed**, 0 failed, 0 skipped;
- full Release solution tests: **1,864 passed**, 0 failed, 0 skipped;
  - Framework: 1,679;
  - DemoHost: 178;
  - ContentValidator: 7;
- strict nonincremental Release solution build: **0 warnings, 0 errors**; and
- `dotnet format --verify-no-changes`: passed.

Documentation link and executable ledger checks are included in the green
Framework suite. Green tests establish a strong baseline; they do not
contradict the uncovered result-shape path.

## Closure Decision

Order 6 is **not ready for formal closure at revision `0780e285`**.
`encounter_orchestration` returns to `partial`, and its three documentation
audiences return to `existing_unreviewed` until O6-R25 through O6-R27 are
complete.

The architecture itself remains healthy. The correction is bounded to one
public terminal-result invariant and two precise audience statements; it does
not require redesigning scheduling, lifecycle, event provenance, automated
execution, or cancellation.

## Post-Correction Status

O6-R25 through O6-R27 are complete. The final closure review supersedes this
audit's revision-specific rejection after verifying the corrected terminal
shape and documentation contracts. `encounter_orchestration` is `complete`,
and all three audience entries are `reviewed` again.
