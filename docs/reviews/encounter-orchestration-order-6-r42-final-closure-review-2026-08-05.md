# Encounter Orchestration Order 6 O6-R42 Final Closure Review

## Review Basis

This review was performed on 5 August 2026 against current `main` at
`cbeffc65`, before this closure record was added. Earlier reports were not used
as proof of correctness. The review began from current Framework source,
executable tests, public contracts, and the three active audience documents.

The source trace covered:

- encounter request and participant validation;
- initiative and both supplied scheduling policies;
- scheduler state, transition, outcome, and structural validation;
- post-command immediate-recipient selection;
- phase turn-economy creation, continuity, application, and liveness;
- battle-start, turn-start, turn-end, phase-end, round-end, departure, and
  battle-end lifecycle transactions;
- defeat/departure reconciliation and completion evaluation;
- cancellation, typed faults, event ownership, event sequencing, and detached
  terminal results; and
- automated encounter composition and restriction execution.

The corrected scheduler paths were then cross-examined against
`encounter-rounds-phases-and-turns.md`, `encounter-orchestration.md`, and
`encounter-orchestration-runtime.md`.

**Result:** no unresolved realistic reachable encounter-orchestration defect
was found in the current implementation.

## Findings

No High, Medium, or Low correction finding qualifies from this review.

This conclusion does not mean that arbitrary hostile host mutation can be
rolled back. It means the Framework-owned encounter contracts and the
advertised supplied policies preserve their documented invariants, and custom
policy failures covered by the public boundary are rejected before they can
cross the protected commit point.

## Corrected Invariants Rechecked

### Stable team-participant rotation

`TeamPhaseRoundRobinBattleEncounterSchedulePolicy` builds its selection ring
from every participant on the acting team in the frozen encounter order. It
applies `NextActorOffset` to that stable ring, scans unavailable slots, selects
the first available actor, and advances the cursor past the selected stable
slot. It does not apply the cursor to a compacted available-actor array.

Consequently:

- if A leaves before or after its command, B remains next rather than C;
- defeat and roster recall cannot shift later actors backward or forward;
- an actor deployed during a team phase can occupy their existing stable slot;
- retaining the current actor does not consume the next ring slot; and
- returning from immediate retention resumes at the stable successor.

Three-actor departure-before-command, departure-after-command, new-deployment,
and retain-then-follow tests exercise these paths.

### Turn-economy liveness precedes another command window

The runner creates one immutable `BattleEncounterScheduleStepOutcome` from the
accepted turn-economy state and passes that exact object through the scheduler
advance request and cursor validation. The structural validator receives the
same outcome. If a phase start or committed command reports
`HasRemainingOpportunities == false`, a proposed next `CommandWindow` is
rejected before `TurnStarted`, lifecycle processing, or the turn handler.

The runner independently checks economy identity, snapshot type, remaining
action consistency, no-cost immutability, and consumption progress before it
accepts that evidence. A custom scheduler therefore cannot reinterpret an
exhausted phase as a fresh command opportunity.

A still-live phase may close when its scheduler has no eligible recipient.
That is required for the supplied one-actor Agility schedule when its frozen
actor becomes unavailable. Closing a live phase does not create another
opportunity and remains structurally legal.

### Accepted turn-window safety bound

The pre-release `BattlePhaseProgressPolicy.MaximumCommands` property is
implemented and documented as an accepted actor turn-window safety bound:

- a scheduled actor already unavailable before `TurnStarted` is skipped and
  does not increment the counter;
- an available actor increments the counter immediately before
  `TurnStarted` and turn-start lifecycle;
- if committed turn-start lifecycle then removes that actor, the accepted
  window still counts even though no handler runs; and
- after the bound has been reached, the next scheduled command-window step
  faults before it is processed.

This behavior prevents a malformed schedule from evading liveness by repeatedly
opening lifecycle-bearing windows while retaining the existing pre-release API
name.

### Structural scheduling authority

The current cursor validates policy identity, participant identity and order,
team order, round limit, state reference continuity, one-step revision and
sequence increments, active-round counters, legal step pairs, actor/team
membership, and exact round-end progression. The encounter-wide transition
limit bounds schedules that loop only through structural boundaries or
unavailable actors.

The supplied team scheduler and Agility scheduler both obey this protocol. The
Agility policy freezes a validated descending-stat order per round, validates
tie-break permutations, gives each actor a one-actor phase, skips actors who
become unavailable, and resolves changed stats or new deployments only at the
next round.

### Lifecycle, cancellation, faults, and result evidence

Lifecycle work executes against staged participant graphs and commits only
after returned events and turn-economy authority are validated. Departure
reconciliation reaches a bounded fixed point, assigns one explicit or defeat
reason per uninterrupted defeat period, and evaluates completion from detached
participant snapshots.

Typed encounter cancellation, operational token cancellation, command
rejection, port failure, event-sink failure, and normal completion remain
distinct. Results contain detached participant snapshots and continuous typed
events. Normal outcomes cannot carry fault metadata; faulted outcomes require a
defined fault code and nonblank message.

## Documentation Cross-Examination

The mechanics page correctly describes player-visible rounds, phases, turn
windows, stable team rotation, Agility ordering, restrictions, completion, and
outcomes. The developer guide correctly describes service composition,
replacement-scheduler obligations, the live-phase early-close exception,
accepted turn-window counting, cancellation, and event ownership. The
technical reference matches the current structural validator, runner order,
transaction boundaries, reconciliation loop, and terminal result shape.

No current statement depends on optional debug text as rule authority. The
three documentation entries remain `reviewed`.

## Verification

| Gate | Result |
|---|---|
| Focused encounter selection | 290 passed, 0 failed, 0 skipped |
| Complete solution | 1,883 passed, 0 failed, 0 skipped |
| Framework tests | 1,698 passed |
| DemoHost tests | 178 passed |
| Content validator tests | 7 passed |
| Architecture/documentation tests | 57 passed |
| Strict nonincremental Release solution build | 0 warnings, 0 errors |
| Strict Framework and trimming-aware builds | 0 warnings, 0 errors |
| Formatting verification | Passed |
| Framework coverage | 90.77% lines, 76.75% branches |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions passed |
| DemoHost smoke | Four noninteractive modes and scripted Training Annex play exited 0 |
| Godot 4.7.1 headless smoke | `CONVERGENCE_GODOT_SMOKE_OK`, exit 0 |
| Diff, architecture, links, and active-boundary guards | Passed |

The connected NuGet vulnerability endpoint was unreachable in this local
environment and returned `NU1900`, including after an approved network retry.
Locked restore with auditing disabled succeeded, and all compile, test,
coverage, content, host, and engine gates then passed. The checked-in CI gate
retains the mandatory connected audit with `NU1901` through `NU1904` treated as
errors; this environmental limitation is not represented as a successful local
advisory audit.

## Trusted Boundaries And Residual Risk

- Turn handlers and state synchronizers are trusted host mutation ports. Their
  external side effects are not generally reversible by the encounter runner.
- A replacement scheduler defines game-specific recipient semantics inside the
  validated structural and economy-liveness envelope. The Framework does not
  pretend all scheduler designs are equivalent.
- `MaximumCommands` remains a pre-release naming compromise. Its exact
  turn-window meaning is now documented and tested; a later breaking release
  may rename it without changing behavior.
- UI and engine hosts must await `RunAsync`. The synchronous wrapper remains a
  compatibility convenience for callers that do not require synchronization-
  context affinity.

These are explicit composition boundaries or naming debt, not unresolved
Order 6 correctness defects.

## Closure Decision

Order 6 is formally complete after O6-R42. The two reachable runtime findings
from O6-R38 were corrected by O6-R39 and O6-R40, O6-R41 reconciled every active
audience and API statement, and this independent current-source review found no
remaining qualifying defect. `encounter_orchestration` returns to `complete`;
all three audience entries remain `reviewed`.
