# Encounter Orchestration Order 6 O6-R48 Independent Closure Audit

Date: 7 August 2026
Branch: `main`
Reviewed commit: `67cd3f9a`
Decision: **runtime behavior remains complete; formal owner closure is held for one low API cleanup and one technical-document correction**

## Review Method

This was a fresh source review. Earlier Order 6 reports and their conclusions
were not used as evidence for correctness.

The audit traced the active implementation from public request construction to
terminal result creation, including:

- participant and initiative validation;
- both supplied scheduling policies and the post-command extension;
- lifecycle staging, lifecycle clock ownership, and departure cleanup;
- turn-economy creation, continuity, liveness, and command consumption;
- command cancellation, rejection, faults, and requested terminal outcomes;
- canonical event ownership, graph validation, sequencing, and sink failure;
- completion, defeat reconciliation, battle-end cleanup, and detached results;
- automated action selection, restricted turns, skill authorization, team-local
  encounter knowledge, and canonical-runner composition.

The mechanics, developer, and technical encounter documents were then checked
against those source paths. Tests were used to reproduce boundary behavior, not
as a substitute for reading the implementation.

## Findings

### O6-R48-L1 - Low: `AutomatedBattleRunner` accepts a services object that it never uses

**Intended invariant**

A required public constructor dependency should either be the authority used by
the component or be validated against the authority that is used. Otherwise a
host cannot tell which supplied policy graph governs execution.

**Reachable path**

`AutomatedBattleRunner` requires a `BattleExecutionServices` argument and stores
it in `_services`:

- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L462)
- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L468)

The value is passed to `AutomatedBattleTurnHandler`, where it is only checked
for null. No policy or executor operation reads it:

- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L517)
- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L575)

A host can therefore construct an `ISkillExecutor` from services A, pass an
unrelated services B to `AutomatedBattleRunner`, and receive behavior governed
entirely by A without a diagnostic.

**Concrete consequence**

This does not currently change battle results or create a security boundary.
It is a public-composition defect: the signature advertises an authority that
does not exist. It invites hosts to believe changing the supplied services
changes automated execution, and it leaves two apparently competing policy
graphs in integration code.

**Required correction**

Remove the unused constructor parameter, field, and inner-handler parameter.
Update the three active call sites, the public API baseline, XML/API guidance,
and focused constructor/composition tests. Do not invent a second execution
authority merely to preserve the parameter.

**O6-R49 resolution, 8 August 2026**

`AutomatedBattleRunner` now accepts only the authorities it uses. Its
`ISkillExecutor` is explicitly documented as the sole action-execution
authority; the unused services field and both redundant parameters are gone.
DemoHost, Godot-contract, catalog-runtime, public API baseline, developer, and
technical composition evidence now use the corrected six-parameter contract.

### O6-R48-D1 - Documentation: the command transaction diagram omits valid terminal command paths

**Documented path**

The technical command diagram currently routes every valid handler result from
`ValidateHandler` directly to `ApplyEconomy`:

- [`encounter-orchestration-runtime.md`](../technical/encounter-orchestration-runtime.md#command-transaction)

**Implemented path**

The runner first publishes validated port-owned command events and then handles
three valid non-executed statuses before applying any turn economy:

- `Cancelled` emits `TurnEnded` and reaches the typed cancelled finish;
- `Faulted` emits `TurnEnded` and enters typed fault finalization;
- `Rejected` emits `ActionRejected`, emits `TurnEnded`, and enters typed fault
  finalization.

Only an `Executed` command reaches `IBattleTurnEconomy.Apply`:

- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1679)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1693)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1707)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1727)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1765)

**Concrete consequence**

The surrounding prose describes cancellation and faults correctly, so this is
not a runtime defect. The state-machine diagram is nevertheless misleading for
an integrator using it as the transaction overview: it implies that typed
cancellation, rejection, and fault pass through economy application and
owner-turn-end lifecycle.

**Required correction**

Split `ValidateHandler` into explicit `Executed`, `Cancelled`, `Rejected`, and
`Faulted` branches. Show command-event publication before the branch, show that
only `Executed` applies economy, and route the three terminal statuses to their
actual typed finalization paths.

**O6-R50 resolution, 8 August 2026**

The transaction diagram now records validated command events before an explicit
four-way status branch. `Cancelled`, `Faulted`, and `Rejected` bypass turn
economy and owner-turn-end lifecycle; only `Executed` reaches economy
application. The corrected diagram also shows the implemented consumption
branch: `None` skips owner-turn-end lifecycle, while consuming commands stage it
before publishing `TurnEconomyChanged`. Mechanics, developer, and technical
guidance were rechecked against these source paths and now agree.

## Runtime Conclusions

No high- or medium-severity runtime defect was reproduced.

The following invariants were confirmed directly from current source and
adversarial tests:

1. Duplicate runtime instance IDs fault before initiative, lifecycle, or turn
   handling receives authority.
2. Initiative must return an exact permutation of participating teams.
3. Scheduler identity, participant identity, team ownership, rounds, revisions,
   step sequence, and legal structural edges are validated on every transition.
4. Both phase command windows and total schedule transitions have explicit
   liveness bounds.
5. Turn economy is runner-owned. Identity, concrete snapshot type, equality,
   remaining-opportunity truth, no-cost stability, and explicit termination are
   checked around every externally supplied boundary.
6. Battle-start, turn-start, owner-turn-end, phase-end, round-end, departure,
   and battle-end lifecycle work is staged over a cloned actor graph. Rejection,
   cancellation, and failure discard staged actor and lifecycle-sequence state.
7. The exact typed turn-start restriction reaches the handler. The supplied
   automated resolver supports skip, flee, roster recall, limited actions,
   forced basic attacks, and forced confusion through explicit action sources.
8. Port-owned events cannot claim runner-owned structural kinds, unknown actor
   IDs, foreign target IDs, a different command-window actor, or inconsistent
   nested execution evidence.
9. Canonical events are recorded before sink observation. A sink failure cannot
   erase or reuse the recorded sequence, and primary command faults remain the
   terminal fault authority if cleanup also fails.
10. Completion policies receive detached snapshots, terminal shapes are
    validated, departure reconciliation is bounded, and final participants are
    detached snapshots rather than live actor objects.
11. `AutomatedBattleRunner` delegates to `BattleEncounterRunner`, validates
    prepared assessments and catalog skill authority, keeps knowledge scoped by
    team and encounter, and preserves canonical terminal outcomes and events.
12. The synchronous wrappers clear and restore the caller synchronization
    context; engine and UI guidance correctly requires asynchronous use.

## Deliberately Rejected Candidate Findings

The audit considered but did not inflate the following into defects:

- Command events are published before command status is branched. This is
  intentional evidence retention, and faulted action execution can legitimately
  carry command or effect evidence produced before the later fault.
- Custom handlers can mutate actors before returning a terminal result. The
  public contract explicitly assigns action mutation to that trusted port;
  lifecycle staging does not claim to make arbitrary host action code
  transactional.
- Different schedulers may define a phase differently. That is the purpose of
  the injected scheduler; duration semantics that require owner-turn or round
  clocks already have distinct lifecycle clocks.

These are extension-boundary responsibilities or design choices, not realistic
framework vulnerabilities under the current contract.

## Documentation Alignment

| Audience | Result | Reason |
|---|---|---|
| Mechanics | `reviewed` | The player-facing round, phase, command, cancellation, Action Token, and outcome descriptions match source. |
| Developer guide | `reviewed` | Required ports, async usage, scheduler/economy authority, event observation, restrictions, and fault behavior match source. |
| Technical | `existing_unreviewed` | The prose is accurate, but the command transaction state machine omits three valid terminal command branches. |

The executable `encounter_orchestration` capability remains `complete`. The
technical documentation state is reopened because documentation review status
must describe the page as a whole, not merely its prose.

## Verification Evidence

- Focused encounter, automated-battle, and catalog-runtime tests: **289 passed**.
- Full Debug solution: **1,887 passed**, **0 failed**, **0 skipped**.
- Full Release solution: **1,887 passed**, **0 failed**, **0 skipped**.
- Strict nonincremental Release build: **0 warnings**, **0 errors**.
- Architecture boundary suite: **57 passed**.
- Framework coverage: **90.77% line**, **76.74% branch**.
- Formatting verification: passed.
- Content validator: **6 packs**, **36 documents**, **98 definitions**; all
  schema, deserialization, semantic, dependency, registration, and catalog
  checks passed.
- Clean battle, field, save, Training Annex noninteractive, and scripted
  Training Annex exit modes: passed without input fallback or legacy loading.
- `git diff --check`: passed.
- Active content tree: unchanged.

## Correction Checkpoints

| Checkpoint | Work | State |
|---|---|---|
| O6-R48 | Fresh source, test, and documentation audit; reopen only affected tracking | Complete |
| O6-R49 | Remove the unused automated-runner services dependency and update public API evidence | Complete |
| O6-R50 | Correct the technical command transaction diagram and revalidate all audience guidance | Complete |
| O6-R51 | Perform one bounded fresh closure review over the two corrections | Pending |

Order 6 should not be marked formally owner-closed until O6-R51 is complete.
The remaining hold is only the bounded closure review; no encounter mechanic
redesign or additional presentation work is required.
