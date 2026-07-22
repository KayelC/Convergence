# Turn Economy Order 3 Source Review

**Date:** 22 July 2026

**Revision reviewed:** `e4ab7d12` (`docs: verify order 2 charge closure`)

**Capability:** `turn_economy`

**Method:** current Framework source, executable tests, authored rulesets, and active audience documents

**Disposition:** reopen as `partial` until O3-R1 through O3-R7 are complete

**Correction authority:** [Turn Economy Order 3 Roadmap](../roadmap/turn-economy-order-3-roadmap.md)

## Scope And Standard

This review starts Documentation Order 3 from the implementation rather than
from earlier reports. It traces:

1. action and effect outcomes into `ActionTurnConsumption`;
2. generic and Action Token economy state transitions;
3. authored ruleset binding and replacement factories;
4. encounter phase initialization, command application, liveness, and events;
5. direct contract, encounter, ruleset, DemoHost, and Godot evidence; and
6. the current mechanics, developer, and technical documentation surface.

An actionable finding requires an intended invariant, a supported path, a
concrete consequence, and reproducible source or test evidence. A hypothetical
game design or a deliberately hostile replacement implementation is not called
a vulnerability. The supported custom-policy boundary is nevertheless part of
the product: Framework must reject malformed output before it lets that output
make live encounter state misleading.

The focused starting gate passed **110 tests**, with zero failures and zero
skips, across `TurnEconomyContractTests`, `BattleEncounterRunnerTests`, and
`RuntimeRulesetBindingTests`.

## Current Implemented Model

### Generic economy contract

`IBattleTurnEconomy` is a stateful, per-phase extension point. A policy starts a
phase from an active-actor count, reports whether actions remain, captures an
immutable `BattleTurnEconomySnapshot`, and applies a validated
`ActionTurnConsumption`.

`StandardActionTurnEconomy` supplies the neutral model: one action per actor
present at phase start, with one action spent by Normal, Pass, or effect-derived
consumption. `TerminatePhase` clears the phase and `None` changes nothing.

`BattlePhaseProgressPolicy` adds two independent safety limits:

- an absolute command count for every phase; and
- a consecutive unchanged-snapshot limit for free actions.

These are safety bounds, not balance formulas.

### Supplied Action Token model

`ActionTokenTurnEconomy` matches the owner-confirmed behavior:

- one full token per active living actor at phase start;
- Normal consumes a partial token first, otherwise a full token;
- Pass consumes a partial token first, otherwise converts one full token into
  one partial token;
- Weakness and Critical convert a full token to partial, or consume a partial
  token when no full token remains;
- Miss and Null consume up to two tokens, partial first;
- Repel, Absorb, and explicit phase termination clear the phase; and
- `None` does not alter the supplied token state.

This means `[partial, full]` becomes `[full]` when passing. Passing cannot create
a second partial token while an existing partial token remains.

### Encounter ownership

The encounter runner owns when a phase starts, which actor receives each command
window, when the economy is applied, and when typed before/after events are
published. The economy owns only action-opportunity state and interpretation of
the supplied consumption shape.

The current runner rotates through active actors after every executed command,
including a command whose consumption is `None`. Replacing the economy does not
replace team initiative, phase layout, or actor scheduling. A future
individual-turn or immediate-bonus-action system therefore also needs the
encounter-scheduling extension work tracked for Documentation Order 6. That is
an explicit boundary, not an unimplemented Action Token rule.

## Findings

### O3-M1: inconsistent phase-start state is detected only after a command can commit

**Invariant:** a custom economy's snapshot and `HasTurnsRemaining()` report must
agree before the runner asks a host for a command.

**Reachable path:** `BattleEncounterRunner` captures the phase-start snapshot,
publishes `PhaseStarted`, and enters its loop by calling
`HasTurnsRemaining()`. It does not compare those two values. The first
consistency check occurs only after the turn handler has executed and the
economy has applied the returned consumption.

A registered custom economy can therefore return a snapshot with zero remaining
actions while reporting `true`. The runner calls the turn handler, whose action
may mutate live actors, and only then returns a typed turn-economy fault.

**Consequence:** Framework contains the faulty extension as an encounter fault,
but it does so after one command can commit. This violates validation-before-
mutation at a supported public extension boundary.

**Correction:** validate the initial snapshot and liveness report before
publishing the phase or reading a command. Cache the validated liveness value
and update it only after a successful economy application.

### O3-M2: one phase does not retain a stable snapshot authority

**Invariant:** one economy instance must expose one stable economy ID and one
stable snapshot shape for the lifetime of a phase.

**Reachable path:** transition validation compares only the snapshot immediately
before and after one `Apply` call. It does not compare the first pre-command
snapshot to `PhaseStarted`, does not compare snapshot runtime types, and does
not verify `PhaseEnded` against the last accepted state.

A custom economy can therefore publish economy A at phase start, economy B
before its first command, or alternate snapshot subtypes while retaining the
same ID. The current runner can accept those sequences.

**Consequence:** typed event consumers can receive a contradictory phase stream
or fail a concrete snapshot cast after accepting the phase-start payload.

**Correction:** treat the validated phase-start snapshot as the authority.
Require every pre-command and phase-end capture to equal the last accepted
snapshot, and require every post-application snapshot to retain the same valid
economy ID and concrete snapshot type.

### O3-L1: turn-economy event data accepts malformed public values

**Invariant:** public typed event payloads must not carry default IDs, null
snapshots, null consumption, or before/after snapshots from different economy
contracts.

**Reachable path:** `BattleTurnEconomySnapshot` validates only remaining actions,
not `EconomyId`. The three turn-economy event payloads are positional records,
and `BattleEncounterEvent` checks only that the payload type matches its kind.

**Consequence:** a host or extension can construct a structurally typed event
that later fails when presentation reads its snapshot or that reports an
impossible economy transition.

**Correction:** reject invalid economy IDs at snapshot construction and validate
turn-economy payload IDs, nullability, identity, and shape when an encounter
event is constructed.

### O3-C1: the supplied neutral economy is not available through supplied authored binding

**Invariant:** Convergence's supplied replacement economy should be selectable
through the same ruleset path as Action Token, without requiring a game to write
a custom factory merely to use a Framework-supplied class.

**Current path:** `StandardActionTurnEconomy` is public and works when injected
directly into `BattleEncounterServices`, but the standard ruleset registry
registers only `standard_action_token`.

**Consequence:** direct composition proves replacement at the encounter layer,
while catalog-backed games do not receive equivalent supplied composition.

**Correction:** register a `standard_actions` turn-economy factory using the
same explicit liveness parameters. This changes no Action Token behavior and
adds no hidden fallback.

## Test And Documentation Gaps

- The direct contract suite does not enumerate every Action Token state
  transition, especially mixed partial/full pass precedence.
- Existing encounter tests cover liveness limits, but not inconsistent initial
  state, phase identity drift, or snapshot-shape drift.
- The mechanics page states the correct outcome table but lacks worked state
  transitions and the precise boundary between economy and scheduling.
- There is no task-oriented developer guide for binding, injecting, replacing,
  or presenting a turn economy.
- There is no dedicated technical page for phase-state authority, event order,
  liveness, and fault containment.

## Not Findings

- The checked token total cannot overflow through supplied Action Token
  transitions: phase start is the maximum total and every supplied transition
  preserves or decreases it.
- The use of two safety limits is intentional. The unchanged-snapshot limit
  catches free-action loops quickly; the absolute command limit also bounds a
  custom economy that continually expands or changes state.
- `None` means no Framework-required cost. Both supplied economies leave their
  state unchanged. A replacement may retain its own typed bookkeeping, but the
  encounter command limit remains authoritative.
- Action outcome aggregation belongs to Order 2. Order 3 consumes the resulting
  typed shape and does not recalculate hit, affinity, or Critical facts.
- Individual agility order and immediate same-actor bonus scheduling are not
  silently promised by `IBattleTurnEconomy`; they cross into encounter
  orchestration and must be designed there explicitly.

## Disposition

The supplied Action Token mechanic is correct, but the custom-policy and
authored-replacement surface is not ready to call fully reviewed. Reopen
`turn_economy` as `partial`, implement the isolated checkpoints in the Order 3
roadmap, write all three audience views, and perform a fresh source review before
restoring `complete`.
