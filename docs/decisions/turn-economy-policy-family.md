# Decision: Turn Economy Policy Family

Status: confirmed
Date: 2026-07-22

## Context

Convergence needs to support games with different ways of counting action
opportunities without making one combat schedule mandatory. The supplied
Action Token mechanic uses full and partial tokens, while a neutral game may
want one ordinary action for each actor present at phase start. Future games
may use another economy entirely.

Three responsibilities must remain distinct:

- action resolution decides the typed cost or outcome of one committed action;
- turn economy decides how that cost changes the current opportunity pool; and
- encounter scheduling decides which team and actor receives the next command
  window.

Combining those responsibilities would make a replacement economy appear to
support actor scheduling that it does not actually own.

## Decision

`IBattleTurnEconomy` is the generic, optional, per-phase policy boundary. A
selected implementation must:

1. start from the number of active actors at phase start;
2. report whether action opportunities remain;
3. expose immutable state through one stable snapshot identity and type; and
4. apply one validated `ActionTurnConsumption` at a time.

Convergence supplies two standard implementations:

| Policy ID | Runtime policy | Purpose |
|---|---|---|
| `standard_actions` | `StandardActionTurnEconomy` | One ordinary action per phase-start actor. |
| `standard_action_token` | `ActionTokenTurnEconomy` | Full and partial Action Token accounting. |

Both policies require explicit authored `maximumCommands` and
`maximumConsecutiveFreeActions` values. These are finite safety boundaries,
not hidden balance defaults.

### Standard actions

The neutral policy starts with one action per active actor. Normal, Pass, and
effect-derived turn-economy consumption each spend one action. `None` spends
nothing. `TerminatePhase` clears the remaining actions.

The neutral policy deliberately ignores whether the effect outcome was Weak,
Critical, Miss, Null, Repel, or Absorb. A game that wants those facts to change
turn cost selects Action Token or supplies another economy.

### Action Token

Action Token starts with one full token per active actor and no partial tokens.
Its supplied transition rules are:

| Consumption or outcome | Transition |
|---|---|
| Normal | Consume one partial token first, otherwise one full token. |
| Pass | Consume one partial token first; only when none exists, convert one full token to partial. |
| Weakness or Critical | Convert one full token to partial; if only partial remains, consume one partial. |
| Miss or Null | Consume up to two tokens, partial first. |
| Repel or Absorb | Clear the phase. |
| `None` | Leave token state unchanged. |
| `TerminatePhase` | Clear the phase. |

The pass precedence is intentional. `[partial, full]` becomes `[full]`, not
`[partial, partial]`. Passing creates a partial token only when the pool has no
partial token to consume.

### Outcome and action-source policy

Turn economy consumes a completed action's typed cost; it does not inspect
skill names, effects, or presentation. The combat outcome aggregation policy
decides whether an action reports a Weakness, Miss, or other effect-derived
outcome. The supplied combat default prices non-escape items as one Normal
action, while an authored combat option may make items effect-driven.

### Scheduling boundary

Turn economy does not choose the next actor, calculate initiative, interleave
teams, or create an immediate bonus turn. The current encounter runner owns a
team-phase schedule and rotates through active actors after each executed
command window, including a `None` command.

A future agility-ordered or immediate same-actor bonus system therefore needs
an encounter-scheduling policy as well as an appropriate economy. Replacing
`IBattleTurnEconomy` alone is not represented as sufficient.

### Extension integrity

A custom economy is a supported extension, so the encounter runner validates
it rather than trusting contradictory state:

- phase-start snapshot and liveness must agree before a command is requested;
- economy ID and concrete snapshot type remain stable for the phase;
- state may change only through the accepted `Apply` transition;
- post-transition liveness must agree with remaining actions; and
- public event payloads reject invalid IDs, null state, and mixed snapshot
  contracts.

An unchanged snapshot counts toward the consecutive-free-action bound unless
the command requested an encounter outcome. Every command counts toward the
absolute phase command bound.

## Host Responsibility

Godot or another host owns command input, token icons, animation, sound,
localization, and accessibility. It reads typed phase and transition event
payloads and must not parse `DebugText` as a rule.

The host also selects and binds the ruleset. Missing or malformed authored
binding is a startup error; Convergence does not silently fall back from one
economy to another.

## Alternatives

### Make Action Token mandatory

Rejected. It would force one combat identity on every game and contradict the
framework's optional-module design.

### Put actor scheduling inside `IBattleTurnEconomy`

Rejected for the current contract. Opportunity accounting and actor order are
different policy dimensions. Combining them would make simple economies carry
unrelated encounter responsibilities and would complicate lifecycle clocks.

### Infer turn cost from effect names or affinities inside the economy

Rejected. Action execution already returns a typed consumption shape. A second
inference path would allow combat evidence and turn cost to disagree.

### Hide generous liveness defaults

Rejected. Infinite or malformed custom phases are a host integration risk, and
the acceptable bounds depend on the selected game. They remain explicit
authored parameters.

## Consequences

- Action Token remains an optional supplied mechanic.
- Neutral standard actions and Action Token use the same authored binding path.
- Custom economies have a strict snapshot and liveness contract.
- Typed encounter events are the presentation boundary.
- Individual-turn and immediate-bonus scheduling remains explicit future
  encounter-orchestration work rather than an implied turn-economy feature.
- Phase economy state is transient encounter state; it is not part of the
  current session save aggregate.

## Evidence

- `src/Convergence.Framework/TurnEconomy/BattleTurnEconomy.cs`
- `src/Convergence.Framework/TurnEconomy/ActionTokenTurnEconomy.cs`
- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs`
- `src/Convergence.Framework/Encounters/BattleEncounterEvents.cs`
- `src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs`
- `src/Convergence.Framework/Runtime/RuntimeRulesetPolicyFactories.cs`
- `tests/Convergence.Framework.Tests/Runtime/TurnEconomyContractTests.cs`
- `tests/Convergence.Framework.Tests/SkillSystem/BattleEncounterRunnerTests.cs`
- `tests/Convergence.Framework.Tests/Runtime/RuntimeRulesetBindingTests.cs`
- [Turn Economy Order 3 Roadmap](../roadmap/turn-economy-order-3-roadmap.md)
