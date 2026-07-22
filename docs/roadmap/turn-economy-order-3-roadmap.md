# Turn Economy Order 3 Roadmap

**Status:** reopened at `7aa3467e`; event-provenance correction required

**Capability:** `turn_economy`

**Starting revision:** `e4ab7d12`

**Source review:** [Turn Economy Order 3 Source Review](../reviews/turn-economy-order-3-source-review-2026-07-22.md)

**Independent recheck:** [Turn Economy Order 3 Independent Recheck](../reviews/turn-economy-order-3-independent-recheck-2026-07-22.md)

**Post-correction closure:** [Turn Economy Order 3 Post-Correction Closure Review](../reviews/turn-economy-order-3-post-correction-closure-review-2026-07-22.md)

**Owner-closure audit:** [Turn Economy Order 3 Owner-Closure Audit](../reviews/turn-economy-order-3-owner-closure-audit-2026-07-22.md)

## Goal

Complete Documentation Order 3 by preserving the confirmed Action Token rules,
hardening the supported replacement-policy boundary, making the neutral supplied
economy authorable, and documenting the exact split between turn economy,
encounter scheduling, and host presentation.

This roadmap does not add an individual-turn or immediate-bonus-action battle
model. It makes the current boundary truthful so that later encounter-scheduler
work can add such a model without pretending `IBattleTurnEconomy` already owns
actor order.

## Confirmed Rules Preserved

- Action Token is optional; `IBattleTurnEconomy` is the generic contract.
- A phase starts with one full token per active living actor.
- Normal actions spend partial before full.
- Pass spends partial before full; only an all-full pool can convert full to
  partial.
- Weakness and Critical grant the supplied benefit.
- Miss and Null spend up to two tokens.
- Repel and Absorb terminate the supplied Action Token phase.
- `None` changes neither supplied economy.
- Liveness limits are explicit authored safety values, never hidden defaults.
- Godot or another host owns input, icons, animation, and localization.

## Checkpoints

| Checkpoint | State | Work | Commit |
|---|---|---|---|
| O3-R0 | `verified` | Establish this source review and roadmap; reopen the capability honestly. | `docs: begin turn economy order 3` |
| O3-R1 | `verified` | Validate phase-start snapshot/liveness agreement before command selection and use the accepted liveness state throughout the loop. | `battle: validate phase turn economy authority` |
| O3-R2 | `verified` | Require one valid economy ID, concrete snapshot type, and accepted state chain through phase start, every command, and phase end. | `battle: enforce stable turn economy snapshots` |
| O3-R3 | `verified` | Reject malformed turn-economy snapshots and typed event payloads at public construction boundaries. | `battle: validate turn economy event payloads` |
| O3-R4 | `verified` | Register the supplied `standard_actions` economy through authored ruleset binding with explicit liveness parameters. | `runtime: bind standard action economy` |
| O3-R5 | `verified` | Complete direct transition, pass-precedence, liveness-threshold, replacement, and malformed-extension contract tests. | `test: complete turn economy contract matrix` |
| O3-R6 | `verified` | Add the policy decision, developer guide, technical state machine, worked mechanics examples, indexes, and coverage evidence. | `docs: document turn economy policy family` |
| O3-R7 | `verified` | Re-read corrected source and documents independently, correct the accepted API baseline, run the complete release gate, and close with no unresolved reachable defect. | `docs: verify turn economy order 3` |
| O3-R8 | `verified` | Reject economy mutation across lifecycle, handler, event, synchronization, and terminal command boundaries. Make `Apply` the supplied Action Token policy's only public consumption mutation. | `battle: guard command window economy authority` |
| O3-R9 | `verified` | Enforce coherent command status, requested outcome, winning-team, fault, and turn-consumption combinations at public construction. | `battle: validate encounter command outcomes` |
| O3-R10 | `verified` | Prevent public record cloning from replacing Framework-calculated turn consumption or introducing null command costs. | `battle: seal turn consumption results` |
| O3-R11 | `verified` | Reconcile the technical sequence, developer guidance, reference content wording, executable matrices, API baseline, and fresh verification evidence. | `docs: reverify turn economy order 3` |
| O3-R12 | `verified` | Re-read the post-correction source, tests, host composition, and audience documents without treating earlier reports as authority; close only if no realistic reachable defect remains. | `docs: close turn economy order 3` |
| O3-R13 | `pending` | Prevent command and lifecycle ports from publishing runner-owned structural encounter events; retain one canonical source for phase, economy, fault, and battle-end evidence. | `battle: enforce encounter event provenance` |
| O3-R14 | `pending` | Reconcile audience guidance and independently re-run the Order 3 source, focused, and release gates. | `docs: close turn economy event authority` |

Each checkpoint is an isolated green commit. A later checkpoint may append its
commit and verification evidence here, but it must not rewrite an earlier
finding as though it never existed.

## Boundary With Encounter Scheduling

The current encounter runner organizes team phases and rotates active actors
after executed command windows. A turn economy decides whether action
opportunities remain and how a consumption shape changes its state. It does not
choose the next actor or interleave teams.

Therefore:

- replacing Action Token with `standard_actions` is fully supported in Order 3;
- a custom team-phase economy is supported through the same contract;
- a future immediate same-actor bonus or individual agility schedule requires a
  separate scheduler extension in Documentation Order 6; and
- Order 3 documentation must state this boundary rather than advertise a false
  drop-in replacement.

## Verification Per Checkpoint

At minimum:

- focused turn-economy, encounter-runner, and ruleset-binding tests;
- `dotnet build` for Framework with .NET 8 and zero warnings;
- `git diff --check`;
- public API baseline verification when a public contract changes; and
- documentation links and coverage tests when documentation changes.

O3-R7 additionally runs the complete solution, all clean DemoHost modes,
scripted Training Annex play, content validation, boundary checks, and the
release-quality scripts required by the active repository.

## Completion Conditions

Order 3 closes only when:

1. no command can execute from an inconsistent initial economy state;
2. one phase has one validated snapshot identity, type, and state chain;
3. malformed public turn-economy events are rejected deterministically;
4. both supplied economies are available through explicit composition;
5. every supplied transition and liveness boundary has direct tests;
6. mechanics, developer, and technical documents agree with source;
7. the scheduling boundary and future extension work are explicit; and
8. a fresh review finds no realistic reachable defect in this scope.

## Completion Record

O3-R7 completed the independent current-source review and release gate. The
review found one API-baseline bookkeeping mismatch: the newly accepted
`StandardActions` policy ID was still listed as unshipped. It now belongs to the
shipped `0.1` baseline, and the unshipped file again contains only its sentinel
line. No reachable turn-economy mechanic defect remained.

The final gate recorded 1,496 passing tests with none failed or skipped, strict
Release builds with zero warnings, 90.65% Framework line coverage and 76.07%
branch coverage, 6 valid packs containing 36 documents and 98 definitions, all
clean DemoHost modes, scripted Training Annex play, and the real Godot 4.7.1
headless smoke. Active content was unchanged.

The independent evidence and residual design boundaries are recorded in the
[Turn Economy Order 3 Final Review](../reviews/turn-economy-order-3-final-review-2026-07-22.md).

## Reopened Completion Gate

A later source-first recheck at `e6949d7b` reproduced three reachable public
integration defects: retained economy mutation can be double-applied inside a
command window, contradictory executed/cancelled results can consume a turn and
tick lifecycle, and record cloning can invalidate non-null or
Framework-calculated turn consumption. The confirmed player-facing transition
table is unchanged.

O3-R8 through O3-R11 are now required before this capability returns to
`complete`. Earlier completion records remain revision-specific history; they
are not current closure authority.

### O3-R8 Completion

The encounter runner now treats its last accepted economy snapshot and
liveness value as an authority token throughout a command window. It checks
that authority after turn-start lifecycle, boundary discovery, command
execution, command-event publication, owner-turn-end lifecycle, transition
event publication, synchronization, defeat events, and phase-end lifecycle.
Staged lifecycle changes are committed only after the corresponding check.
Any unexplained identity, type, state, or liveness change produces the existing
typed `TurnEconomyTransitionInvalid` fault before another accepted transition
or terminal result.

The supplied Action Token implementation now exposes only
`IBattleTurnEconomy.Apply` for consumption. Focused R8 verification covers
retained-instance mutation from lifecycle ports, handlers, and event sinks,
including rollback of staged lifecycle state and protection against a hidden
second spend. The checkpoint gate recorded 177 focused tests and 1,502 full
solution tests passing with none skipped, a strict Release build with zero
warnings, and clean format and diff checks.

### O3-R9 Completion

`BattleEncounterCommandResult` now validates each complete command shape, not
only its individual enum and ID values. Executed commands may continue the
encounter or request the gameplay outcomes `Victory`, `Defeat`, `Escape`, or
`Draw`; they cannot masquerade as cancellation or faults. Cancelled, rejected,
and faulted commands carry no turn cost and require their matching outcome.
Faulted and rejected commands require a nonblank diagnostic, cancellation
cannot carry one, and winning-team IDs are accepted only for victory or defeat.

Construction failures raised inside a host turn handler remain within the
runner's typed port-fault boundary. Regression coverage proves that an
`Executed + Normal + Cancelled` contradiction cannot spend an action or run
owner-turn-end lifecycle. The checkpoint gate recorded 94 focused tests and
1,504 full solution tests passing with none skipped, a strict Release build
with zero warnings, and clean format and diff checks.

### O3-R10 Completion

Host-mediated commands retain a public `init` setter because hosts may clone a
command to another valid cost, but that setter now rejects null. Assessment and
execution-result turn costs are Framework-calculated facts and are getter-only,
so record cloning cannot rewrite them. Escape execution now creates a complete
validated result with its final cost instead of using a mutable `with`
assignment. The accepted pre-release API baseline records this intentional
removal of two result setters. The checkpoint gate recorded 83 focused tests
and 1,505 full solution tests passing with none skipped, a strict Release build
with zero warnings, and clean format and diff checks.

### O3-R11 Completion

The technical sequence now places authority checks before staged lifecycle
commit and after every host-observable command boundary. The developer guide
defines the complete legal command-result matrix and the ownership difference
between host-authored commands and Framework-calculated results. Reference
content now describes Action Token as Framework-supplied and host-selected.

The executable capability matrix records 23 complete, 0 partial, and 2
deferred capabilities. The documentation matrix records 20 reviewed, 32
`existing_unreviewed`, 16 missing, and 7 `not_applicable` audience entries.

The R11 release gate passed locked restore and vulnerability auditing, format
verification, 1,505 tests with none skipped, strict Release builds with zero
warnings, 90.65% Framework line coverage and 76.16% branch coverage, 6 valid
packs containing 36 documents and 98 definitions, all four noninteractive
DemoHost modes, scripted Training Annex play, trim analysis, and the real Godot
4.7.1 headless smoke. The first sandboxed Godot launch could not write its
normal `user://logs` path; the identical command passed when allowed to use the
engine's user-data directory. No engine binary or generated artifact is
tracked.

### O3-R12 Completion

The post-correction closure review independently traced the current economy
implementations, consumption contracts, action-result production, encounter
callbacks, liveness limits, ruleset binding, typed events, DemoHost wiring,
Godot wiring, focused tests, and all three audience documents. It found no
unresolved realistic reachable defect in the Order 3 scope.

The review confirms that the remaining individual-turn, immediate-bonus, and
mid-battle-suspend work belongs to explicit future scheduler and persistence
designs. Those are documented product boundaries rather than incomplete Action
Token transitions. Order 3 is therefore complete at `4c6dde7` with the R11
release evidence retained as its executable gate.

O3-R12 reran 156 focused tests and the complete 1,505-test solution with no
failures or skips. The warning-as-error Release build completed with zero
warnings, and format and diff verification passed.

## Owner-Closure Audit Reopening

The source-first owner-closure audit at `7aa3467e` did not find a defect in the
supplied Action Token or neutral transition tables, authored binding, or
liveness guards. It did find that command and lifecycle ports can return the
same structural event kinds that the runner uses for accepted phase,
turn-economy, fault, and battle-end state. Those events are resequenced and
published without provenance validation, so an event sink can receive a false
`TurnEconomyChanged` or `BattleEnded` event before the canonical runner event.

O3-R13 and O3-R14 are therefore required before formal closure. The correction
is shared with Order 6's broader event-contract review, but no scheduler or
Action Token balance redesign is part of this reopening.
