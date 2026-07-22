# Turn Economy Order 3 Roadmap

**Status:** active

**Capability:** `turn_economy`

**Starting revision:** `e4ab7d12`

**Source review:** [Turn Economy Order 3 Source Review](../reviews/turn-economy-order-3-source-review-2026-07-22.md)

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
| O3-R7 | `pending` | Re-read corrected source and documents independently, run the complete release gate, and close only if no reachable defect remains. | `docs: verify turn economy order 3` |

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
