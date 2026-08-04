# Encounter Orchestration Order 6 Post-R27 Independent Audit

**Date:** 4 August 2026
**Review basis:** current source, executable tests, active content, and the three
Order 6 audience documents at `4c543ab1`
**Result:** two realistic reachable runtime corrections and one documentation
correction are required; Order 6 is reopened

## Review Independence And Method

This audit did not accept an earlier review, roadmap entry, test total, or
closure statement as evidence. It traced the currently compiled implementation
from public encounter construction through request validation, initiative,
scheduling, lifecycle transactions, turn restrictions, command execution,
turn-economy application, reconciliation, completion, event publication, and
immutable result construction. Existing tests were read to determine what they
actually assert rather than what their names imply.

Primary source examined:

- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs`;
- `src/Convergence.Framework/Encounters/BattleEncounterEvents.cs`;
- `src/Convergence.Framework/Encounters/BattleEncounterScheduling.cs`;
- `src/Convergence.Framework/Encounters/AgilityOrderedBattleEncounterScheduling.cs`;
- `src/Convergence.Framework/Encounters/BattleEncounterPostCommandScheduling.cs`;
- `src/Convergence.Framework/Encounters/BattleEncounterLifecycleTransaction.cs`;
- `src/Convergence.Framework/Encounters/BattleEncounterLifecycleClocks.cs`;
- `src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs`;
- `src/Convergence.Framework/Encounters/BattleStatusLifecycleEventMapper.cs`;
- `src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs`;
- `src/Convergence.Framework/Encounters/AutomatedBattleTurnRestrictionResolver.cs`;
- the action, status, runtime-state, and turn-economy contracts those files call.

Primary executable evidence examined:

- `tests/Convergence.Framework.Tests/SkillSystem/BattleEncounterRunnerTests.cs`;
- `tests/Convergence.Framework.Tests/SkillSystem/CatalogBattleRuntimeTests.cs`;
- every test under `tests/Convergence.Framework.Tests/Encounters`;
- the architecture and documentation contract tests;
- active limited-action content under
  `content/reference/status-lifecycle/status_lifecycle_demo.ailments.json`.

Audience documents cross-checked:

- `docs/mechanics/encounter-rounds-phases-and-turns.md`;
- `docs/developer-guide/encounter-orchestration.md`;
- `docs/technical/encounter-orchestration-runtime.md`;
- the departure contract in the status/passive developer and technical pages.

Audit-only regression probes were added, executed, and removed before this
report was written. They changed no committed source and are described below so
their permanent replacements can be added with the corrections.

## Findings

### O6-R28-M1: limited-action authorization can trust a mismatched action label

**Severity:** Medium
**Invariant:** a limited-action restriction must authorize the typed command
that will execute, not an unrelated label supplied beside it.

`AutomatedBattleTurnRestrictionResolver.ValidateCommand` receives both a
`ContentId actionId` and a `BattleActionCommand`. It correlates that ID for
skills, items, and basic attacks at
`AutomatedBattleTurnRestrictionResolver.cs:342-370`, but does not derive or
correlate the identity of Guard, Pass, Analyze, or Escape commands. The
limited-action check at lines 385-388 then tests only the detached `actionId`.

This creates a reachable mismatch through the public restricted-action source:

1. lifecycle permits only `guard`;
2. the source returns action ID `guard` beside an
   `AnalyzeBattleActionCommand`;
3. validation accepts `guard` because the label is allowed;
4. the executor runs Analyze because that is the typed command.

The audit probe expected a typed fault and observed a normal `Draw`, confirming
that the disallowed command executed. Active content makes the contract
meaningful rather than hypothetical: the supplied limited-action ailment
allows `basic_attack`, `guard`, and `pass`.

Existing coverage proves that an allowed skill executes and a disallowed skill
is rejected, but both tests use a skill whose typed definition and detached ID
agree. They do not exercise a mismatched non-definition command.

**Consequence:** a supplied framework resolver can violate an authored ailment
restriction. A hostile or simply mistaken host adapter can present one allowed
ID while executing another supported command without receiving the promised
typed fault.

**Required correction:** establish one canonical action identity for every
supported typed command. Derive it from the command where possible and reject
any detached-label mismatch before assessment or mutation. Add a table-driven
test for every supported command kind plus deliberate mismatch pairs.

### O6-R28-M2: explicit departure reason does not remain authoritative for one defeat period

**Severity:** Medium
**Invariant:** one actor departure has one authoritative cleanup reason. If an
explicit Flee or RosterRecall wins while the actor is also defeated, the same
uninterrupted defeat period must not receive a second Defeat cleanup.

`ProcessPendingDeparturesAsync` inserts an explicit reason first and uses
`TryAdd` for discovered defeat, so the explicit reason wins the first
reconciliation pass (`BattleEncounterRunner.cs:1067-1082`). After processing,
however, the actor is recorded in `processedDefeatDepartures` only when the
chosen reason itself was `Defeat` (`BattleEncounterRunner.cs:1134-1139`).

`ReconcileAsync` clears the explicit reason after that pass and iterates again
to reach a fixed point (`BattleEncounterRunner.cs:2131-2149`). If the same actor
remains defeated, the next pass discovers an unprocessed defeat and dispatches
a second cleanup with `Defeat`.

The audit probe supplied a `FleeBattle` turn restriction whose handler committed
both undeployment and zero HP. The lifecycle port recorded, in order:

```text
(actor, Flee)
(actor, Defeat)
```

The expected sequence contained only `(actor, Flee)`. The same state machine
applies to RosterRecall.

This matters because status cleanup maps Flee and Defeat to distinct typed
`StatusRemovalCause` values. A status configured to survive Flee but not Defeat
can be removed by the unintended second pass, and reason-sensitive passives or
events can execute twice. It directly contradicts the technical lifecycle
statement that an explicit flee or recall reason wins when the actor is also
defeated during that command.

**Required correction:** retain the chosen departure reason for the complete
uninterrupted defeat period, or mark that defeat period processed whenever an
explicit reason wins for an actor that is already defeated. Add permanent Flee
plus Defeat and RosterRecall plus Defeat tests that assert one cleanup, the exact
reason, reason-sensitive status retention, and stable fixed-point completion.

### O6-R28-L1: the audience documents blur lifecycle decision and command enactment

**Severity:** Low documentation precision
**Invariant:** documentation must distinguish who decides a turn restriction,
who enacts it, and when the runner reconciles the resulting state.

The mechanics sequence says restrictions, flee, and roster recall are
reconciled before the host or AI selects and executes a command. In current
source, turn-start lifecycle supplies the restriction, but an explicit Flee or
RosterRecall reason is discarded while the actor remains deployed. The turn
handler or supplied automated restriction resolver must enact the restriction;
the runner reconciles cleanup only after the corresponding committed state
change.

The status/passive developer page describes that deployment condition
correctly, while the encounter mechanics sequence is broad enough to imply the
runner automatically performs the departure. This can lead a host author to
return an ordinary command under a forced restriction and expect the runner to
replace it.

**Required correction:** state consistently that lifecycle decides the typed
restriction, the selected handler/resolver enacts forced, limited, skip, flee,
or recall behavior, and the runner validates/reconciles the committed result.
The correction must not claim that arbitrary host command ports are
transactional or automatically rewritten.

## Areas Rechecked Without A Qualifying Finding

The following current paths were traced and did not expose another realistic
reachable Order 6 defect:

- duplicate runtime-ID and malformed participant/request rejection;
- exact initiative-team permutation validation;
- supplied team-phase and Agility scheduler identity, revision, sequence, and
  legal-transition validation;
- phase command/free-action liveness and encounter-wide structural-transition
  liveness;
- turn-economy authority before and after host and lifecycle boundaries;
- cancellation before lifecycle commit and typed containment of port failures;
- port-event allow-list, frozen participant graph, nested identity, and
  scheduled-actor correlation;
- zero-survivor draw and repeated defeat-period completion behavior outside the
  explicit-reason conflict above;
- normal versus fault terminal-result invariants;
- immutable final participant and event snapshots;
- canonical asynchronous execution in `AutomatedBattleRunner`.

Trusted host ports can still perform irreversible external side effects before
throwing. Event sinks can consume only a prefix before failing. Synchronous
wrappers remain inappropriate for a single-threaded UI host. Those are
documented extension boundaries, not new defects from this audit.

## Documentation Alignment Decision

The mechanics, developer, and technical documents are substantial and mostly
match the implementation, but they cannot remain `reviewed` while O6-R28-M1,
O6-R28-M2, and O6-R28-L1 are unresolved. All three
`encounter_orchestration` audience entries return to `existing_unreviewed`.

The framework capability returns from `complete` to `partial`. This does not
discard the scheduler, lifecycle, event, cancellation, or automated execution
work already completed; it records two bounded unfinished contract paths.

## Correction Roadmap

| Checkpoint | Work | State |
|---|---|---|
| O6-R28 | Record this independent audit, reopen executable tracking, and preserve the two reproductions as explicit correction requirements. | `complete` |
| O6-R29 | Make typed command identity authoritative for limited-action validation and add exhaustive supported-command mismatch tests. | `complete` |
| O6-R30 | Preserve one explicit departure reason per uninterrupted defeat period and add Flee/Recall plus Defeat lifecycle tests. | `complete` |
| O6-R31 | Reconcile mechanics, developer, technical, public integration wording, matrices, and API evidence with the corrected runtime. | `complete` |
| O6-R32 | Independently reread the corrected source and documents, rerun adversarial probes and the complete release gate, and decide closure. | `pending` |

Each implementation checkpoint must be an isolated green commit. Order 6 may
return to `complete` and its audience entries may return to `reviewed` only
after O6-R32 finds no unresolved realistic reachable defect.

## Correction Progress

- O6-R29 derives the canonical identity from each supported typed command and
  rejects any detached selection label mismatch before assessment or mutation.
- O6-R30 preserves an explicit Flee or Roster Recall cleanup reason for the
  complete uninterrupted defeat period while keeping defeat announcement as a
  separate once-per-period authority.
- O6-R31 reconciles all three audience documents, the public integration
  contract, lifecycle cross-references, executable matrices, and documentation
  evidence while deliberately retaining `partial` and `existing_unreviewed`.
- O6-R32 remains the only closure checkpoint. It must reread current source and
  documents independently and pass the complete release gate before promotion.

## Verification Evidence

| Gate | Result |
|---|---|
| Focused encounter, scheduler, lifecycle-event, and automated runtime tests | 278 passed, 0 failed, 0 skipped |
| Full clean solution | 1,865 passed: 1,680 Framework, 178 DemoHost, 7 ContentValidator; 0 failed, 0 skipped |
| Framework coverage | 90.77% lines, 76.70% branches; 90%/70% gate passed |
| Strict Release solution build | 0 warnings, 0 errors |
| Framework trimming analysis | 0 warnings, 0 errors |
| Architecture and documentation boundary tests | 57 passed |
| Formatting | `dotnet format --verify-no-changes` passed |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions passed |
| DemoHost | Four noninteractive modes and scripted Training Annex play exited 0 |
| Godot integration contracts | Passed as part of the Framework suite |
| Local real Godot smoke | Inconclusive: the repository-local Windows engine crashed before project execution while opening `user://` logging; no Framework failure was observed |

The two defect reproductions are intentionally not hidden by the green totals:
the existing suite did not contain those adversarial combinations. The local
Godot executable failure is tracked as a host-tooling limitation for this audit
and is not evidence for or against the encounter runtime.

## Closure Decision

**Order 6 is not ready to close.** Its architecture is healthy and the defects
are bounded, but both can change legal battle behavior through public supplied
components. O6-R29 through O6-R32 must complete before formal closure.
