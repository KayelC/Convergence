# Status And Passive Lifecycle Order 4 R21 Extension Audit

## Review Identity

- Review date: 26 July 2026
- Reviewed revision: `67bb0d27`
- Branch: `main`
- Scope: turn-start ailment scheduling, encounter-owned actor departures,
  lifecycle cleanup, event evidence, tests, and active Order 4 documentation
- Method: current source and tests were inspected directly. Two temporary,
  uncommitted regression probes were used to reproduce the findings and were
  removed afterward.

## Verdict

O4-R21 did not close Order 4. The corrected owner-turn-end and passive restore
paths are healthy, but the fresh source trace found two additional reachable
integration defects:

1. a successful custom turn-start handler can invalidate live ailment
   enumeration by adding or refreshing an ailment; and
2. the canonical encounter runner does not dispatch typed lifecycle cleanup
   when it owns a flee, roster-recall, or defeat transition.

Both failures are bounded gameplay and integration defects. The actor
transaction prevents partial mutation when turn-start enumeration faults, and
no security or host-neutrality issue was found. Order 5 remains paused until
the correction sequence below completes.

## Findings

### O4-M3: Turn-start custom behavior can invalidate ailment scheduling

**Severity:** Medium

**Intended invariant:** turn-start restriction evaluation uses the ailments
present at boundary start in stable order. A removed or replaced scheduled
instance is skipped before its slot, and an ailment added during the boundary
waits for the next turn-start boundary.

**Reachable path:**

1. An actor has an ailment with a registered
   `ICustomAilmentTurnBehaviorHandler`.
2. The handler receives the staged `RuntimeActorState`, applies or refreshes an
   ailment, and returns a valid typed restriction.
3. `ProcessTurnStartCore` is still enumerating `actor.Ailments.Values`.
4. The dictionary write invalidates that live enumeration.
5. The lifecycle throws `InvalidOperationException` instead of accepting the
   valid custom result.

**Source evidence:**

- [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs)
  evaluates `actor.Ailments.Values.Select(...).ToArray()` while each selected
  custom handler can mutate the same staged actor.
- `CustomAilmentTurnBehaviorRequest` deliberately exposes the staged
  `RuntimeActorState`; existing rollback tests confirm that handler mutation is
  part of the supported extension boundary.
- `RuntimeActorState.ApplyAilment` writes the active ailment dictionary.

**Reproduction evidence:** a temporary custom handler added one valid ailment
and returned `CanAct`. `ProcessTurnStart` threw
`Collection was modified; enumeration operation may not execute.` The
transaction correctly rolled back the addition, but the valid extension could
not execute.

**Consequence:** a host-authored custom ailment behavior can fault the
encounter merely by applying or refreshing status through the staged actor.
The outcome depends on dictionary-enumerator behavior rather than the
documented lifecycle schedule.

**Required correction:**

- snapshot ordered `(ailment ID, exact active instance)` pairs at boundary
  start;
- re-resolve and identity-check each scheduled pair before invoking it;
- skip removed, refreshed, or replaced instances;
- defer additions until the next boundary;
- preserve restriction ordering and complete transaction rollback; and
- add focused addition, removal, refresh, order, and failure tests.

### O4-M4: Canonical encounter departures omit typed cleanup

**Severity:** Medium

**Intended invariant:** when the framework encounter path commits a typed
defeat, flee, or roster-recall transition, it also invokes lifecycle cleanup
with that exact departure reason before encounter processing continues or
ends.

**Reachable path:**

1. An actor owns a status whose removal profile allows `Flee` but not
   `BattleEnd`.
2. A turn-start ailment resolves to `FleeBattle`.
3. `AutomatedBattleTurnRestrictionResolver.LeaveBattle` sets
   `IsDeployed` to false and emits `EncounterPresenceChanged`.
4. `BattleEncounterRunner` later calls lifecycle cleanup only through
   `ProcessBattleEndAsync`.
5. Battle-end cleanup uses `BattleEnd`, so the flee-only status remains.

The same orchestration omission applies to `RecallToRoster`. Newly defeated
actors are announced by the runner, but no `Defeat` cleanup is dispatched
before battle-end processing.

**Source evidence:**

- [`AutomatedBattleTurnRestrictionResolver.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleTurnRestrictionResolver.cs)
  commits encounter-presence changes for flee and recall.
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs)
  announces newly defeated actors and invokes the lifecycle port at battle
  start, turn, phase, round, and battle end, but exposes no actor-departure
  lifecycle call.
- [`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs)
  maps only battle-end cleanup into the encounter runner.
- [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs)
  already distinguishes `Defeat`, `Flee`, `RosterRecall`, and `BattleEnd`, so
  substituting battle-end cleanup loses authored meaning.

**Reproduction evidence:** a temporary automated-battle probe applied a
permanent ailment removable only by `Flee`. The actor fled and the opposing
team won, but the ailment remained active after the encounter.

**Consequence:** authored departure-specific status behavior is bypassed in the
canonical automated encounter. A recalled actor can retain deployment state
that should have cleared, and defeat/flee-only removal profiles do not receive
their declared cause or typed cleanup evidence.

**Required correction:**

- add an optional, typed encounter departure-lifecycle port;
- have `BattleStatusEncounterLifecyclePort` adapt it to canonical cleanup;
- dispatch framework-known `Flee`, `RosterRecall`, and newly observed
  `Defeat` causes through a staged participant graph;
- publish cleanup events before defeat announcements or battle completion;
- keep manually applied deployment swaps host-owned until their typed roster
  command is integrated with encounter state; and
- add focused flee, recall, defeat, event-order, cancellation, and rollback
  coverage.

## Confirmed Corrections

The source-first trace rechecked O4-R18 and O4-R19:

- owner-turn-end ailment triggers use a boundary-start exact-instance schedule;
- removed, refreshed, and replaced scheduled ailments are skipped;
- newly added ailments wait for the next owner-turn-end boundary;
- passive activation restore keys resolve an equipped passive, valid trigger
  index, and exact authored event; and
- malformed activation restoration is atomic.

No regression was found in those corrected paths.

## Correction Roadmap

| Checkpoint | Work | Completion evidence |
|---|---|---|
| O4-R22 | Record the R21 extension findings and keep active maturity records reopened. | This report, roadmap links, and executable matrix reasons. |
| O4-R23 | Give turn-start ailment resolution the same exact-instance scheduling discipline as owner turn end. | Focused custom addition, removal, refresh, order, and rollback tests. |
| O4-R24 | Integrate typed defeat, flee, and roster-recall cleanup into the canonical encounter path. | Focused lifecycle-port and automated-encounter departure tests. |
| O4-R25 | Reconcile mechanics, developer, technical, roadmap, and matrix guidance. | Documentation tests and active link validation. |
| O4-R26 | Re-read corrected source and documentation independently and run the complete release gate. | A new closure report with no unresolved reachable Order 4 defect. |

## Implementation Progress

| Checkpoint | State | Evidence |
|---|---|---|
| O4-R22 | Complete | Commit `60e72305` records this extension audit and reopens the active matrices. |
| O4-R23 | Complete | Commit `7c5a5a6c` uses a boundary-start exact-instance schedule for turn-start ailment behavior and covers addition, removal, refresh, order, and rollback. |
| O4-R24 | Complete | Commit `e5ddb4b` adds the optional typed departure port and canonical flee, roster-recall, and newly observed defeat cleanup with cancellation, rollback, and event-order coverage. |
| O4-R25 | Complete | The mechanics, developer, technical, roadmap, and executable-matrix guidance now describe the corrected source while retaining pending review state. |
| O4-R26 | Complete, closure rejected | The [R26 correction audit](status-passive-lifecycle-order-4-r26-correction-audit-2026-07-26.md) re-read current source and found four additional reachable correction paths. O4-R27 through O4-R32 govern the extension. |

## Scope Boundary

O4-R24 covers departures the framework can identify authoritatively:
turn-restriction flee, turn-restriction roster recall, and newly observed
defeat. A host that applies a manual deployment swap or roster command remains
responsible for supplying the matching typed cleanup call until that command is
part of the canonical encounter transaction. Hosted Entity selection is not
actor departure and must not trigger cleanup.

## Closure Decision

Order 4 remains `partial`. Its three audience documents remain
`existing_unreviewed`. O4-R18 through O4-R25 stay accepted. O4-R26 completed
its independent review but rejected closure; O4-R27 through O4-R32 now govern
the correction sequence.
