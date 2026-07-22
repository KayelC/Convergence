# Turn Economy Order 3 Roadmap

**Status:** corrections required

**Capability:** `turn_economy`

**Starting revision:** `e4ab7d12`

**Source review:** [Turn Economy Order 3 Source Review](../reviews/turn-economy-order-3-source-review-2026-07-22.md)

**Independent recheck:** [Turn Economy Order 3 Independent Recheck](../reviews/turn-economy-order-3-independent-recheck-2026-07-22.md)

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
| O3-R9 | `pending` | Enforce coherent command status, requested outcome, winning-team, fault, and turn-consumption combinations. | `battle: validate encounter command outcomes` |
| O3-R10 | `pending` | Prevent public record cloning from replacing Framework-calculated turn consumption or introducing null command costs. | `battle: seal turn consumption results` |
| O3-R11 | `pending` | Reconcile the technical sequence, developer guidance, reference content wording, executable matrices, API baseline, and fresh verification evidence. | `docs: reverify turn economy order 3` |

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
