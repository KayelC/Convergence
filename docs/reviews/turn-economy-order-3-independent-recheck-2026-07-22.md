# Turn Economy Order 3 Independent Recheck

**Date:** 22 July 2026

**Revision reviewed:** `e6949d7b`

**Scope:** current Framework source, active Order 3 mechanics/developer/technical
documentation, active content, and executable tests. Earlier review reports were
not used as implementation evidence.

**Decision:** corrections required before Order 3 closes.

## Findings

### Medium O3-F1: Economy state can change inside a command window without detection

**Intended invariant:** one encounter phase has one accepted immutable economy
state chain. During runner ownership, state changes only through the runner's
accepted `IBattleTurnEconomy.Apply` call.

**Reachable path:** `BattleEncounterRunner` captures and validates the accepted
snapshot before turn-start lifecycle and command execution. It does not capture
again after either port returns. A host can retain the economy instance returned
by its factory. The supplied `ActionTokenTurnEconomy` publicly exposes
`ConsumeAction`, `Pass`, and `TerminatePhase`, so a turn handler can mutate that
retained instance and then return the same priced command.

For one full token:

```text
accepted state             [full]
handler calls Pass()       [partial]
handler returns Pass
runner calls Apply(Pass)   []
```

The runner compares `[full]` directly with `[]`, accepts the transition, and
publishes one Pass consumption. One visible pass therefore spends twice and the
event stream cannot explain the first mutation.

**Code evidence:**

- pre-command capture and continuity check:
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:872`
- lifecycle and handler run after that check:
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:899`
- runner applies the returned consumption without a second continuity check:
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:1018`
- supplied direct mutators:
  `src/Convergence.Framework/TurnEconomy/ActionTokenTurnEconomy.cs:90`,
  `:147`, and `:160`

**Executable reproduction:** an audit-only test retained the factory-created
Action Token economy, called `Pass()` in the handler, and returned
`Executed(ActionTurnConsumption.Pass)`. The emitted transition was
`(full: 1, partial: 0) -> (0, 0)`, rather than the canonical single-pass result
`(0, 1)`. The reproduction passed against the reviewed source and was removed
afterward.

**Required correction:** capture and require economy continuity immediately
after turn-start lifecycle and immediately after the handler returns, before
status handling or `Apply`. Reconcile the supplied direct mutation methods with
the documented single-authority contract, and add retained-instance regression
tests.

### Medium O3-F2: Contradictory command status and outcome values are accepted

**Intended invariant:** cancellation and faults are non-executed encounter
results. They must not consume an action or run owner-turn-end lifecycle.

**Reachable path:** the public `BattleEncounterCommandResult` constructor
validates each value separately but does not validate their relationship. It
accepts an `Executed` command with `Normal` consumption and a requested
`Cancelled` outcome. The runner branches only on `Status`, so it applies the
normal cost, runs owner-turn-end lifecycle, and only then finishes as cancelled.

**Consequence:** an ordinary host integration error can spend a turn and tick
status/passive clocks for an action the host reports as cancelled. The same
constructor also permits other contradictory status/outcome/consumption
combinations even though the static factories produce coherent ones.

**Code evidence:**

- incomplete public constructor validation:
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:329`
- status handling before economy application:
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:975`
- outcome handling after economy and turn-end lifecycle:
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:1018` and
  `:1108`
- developer guide calls this result validated:
  `docs/developer-guide/turn-economy-policies.md:104`

**Executable reproduction:** an audit-only test returned
`Executed + Normal + requested Cancelled`. The final outcome was `Cancelled`,
but one `TurnEconomyChanged` event was emitted and `ProcessTurnEndAsync` ran
once. The reproduction passed against the reviewed source and was removed.

**Required correction:** enforce complete construction invariants. Cancelled,
rejected, and faulted statuses must carry `None` and their matching terminal
shape. Executed commands must reject cancellation/fault outcomes. Explicitly
define and test the gameplay outcomes an executed command may request.

### Low O3-F3: Public record cloning can invalidate turn-consumption contracts

**Intended invariant:** commands, prepared assessments, and execution results
are immutable typed contracts with non-null turn consumption.

**Reachable path:** `HostMediatedBattleActionCommand`,
`BattleActionAssessment`, and `BattleActionExecutionResult` expose public
`init` setters for `TurnConsumption`. Their constructors establish valid
values, but record cloning bypasses those constructors. A host can create
`command with { TurnConsumption = null! }`; assessment and execution-result
clones can likewise replace the Framework-calculated value.

**Consequence:** a malformed host command can return a null value from the
non-null action API. A cloned assessment can advertise a cost that execution
does not use, and a cloned result can lie about the cost already calculated.
Built-in hosts do not take this path, so this is a public-integration robustness
defect rather than a current gameplay failure.

**Code evidence:**

- host-mediated command setter:
  `src/Convergence.Framework/Execution/BattleActionExecutor.cs:298`
- prepared assessment setter:
  `src/Convergence.Framework/Execution/BattleActionExecutor.cs:362`
- execution-result setter:
  `src/Convergence.Framework/Execution/BattleActionExecutor.cs:404`

**Executable reproduction:** an audit-only test cloned a valid host-mediated
command with null turn consumption and observed the null public property. The
reproduction passed against the reviewed source and was removed.

**Required correction:** use a validating initializer where host-authored
command cloning is intentionally supported. Make Framework-calculated
assessment and result values getter-only, replacing the internal escape-result
clone with a validated construction path. Update the accepted API baseline
deliberately.

## Documentation Alignment

The confirmed player-facing rules match the implementation:

- Action Token is optional.
- phase start grants one full token per active actor;
- Normal spends partial before full;
- Pass spends an existing partial first, otherwise converts one full to partial;
- Weakness and Critical convert full to partial or consume partial;
- Miss and Null spend up to two tokens, partial first;
- Repel, Absorb, and explicit termination end the phase; and
- the neutral standard-actions economy spends one action for every priced
  command.

The ruleset factories require explicit finite liveness values, reject unknown
or malformed parameters, and do not silently fall back. The automated runner
uses the canonical encounter runner and bound economy.

Two active documentation statements describe the intended authority more
strongly than the source currently guarantees:

- `docs/technical/turn-economy-runtime.md:272` requires state changes only
  through `Apply`, while the supplied Action Token type exposes public direct
  mutators and the runner misses intra-window drift;
- `docs/developer-guide/turn-economy-policies.md:104` calls command results
  validated, while contradictory status/outcome shapes are accepted.

The reference catalog description at
`content/reference/catalog-surface/catalog_surface_sample.rulesets.json:69`
also calls Action Token "host-owned". The host selects and presents the policy,
but the standard Action Token implementation is Framework-owned. This is a
wording correction, not a rule defect.

## Closely Related Orders

Order 6 (`encounter_orchestration`) is closely related because it owns command
status, cancellation, actor scheduling, and terminal outcomes. O3-F2 sits on
that boundary, but it directly determines whether Order 3 spends an action, so
it should be corrected before Order 3 closes rather than deferred.

Do not jump directly to Order 6. Order 4 (`status_and_passive_lifecycle`) is the
next dependency because priced commands run owner-turn-end lifecycle while
`None` does not. Its duration clocks must be confirmed before a later scheduler
policy changes what constitutes an actor turn. The recommended sequence remains:

1. correct and re-review Order 3;
2. review Order 4;
3. retain Order 5 in the established sequence; and
4. review Order 6 with the confirmed economy and lifecycle contracts.

## Verification

- Focused Order 3 and adjacent action/encounter tests: **225 passed**, 0 failed,
  0 skipped.
- Full solution: **1,496 passed**, 0 failed, 0 skipped.
  - Framework: 1,316
  - Content Validator: 7
  - DemoHost: 173
- Release nonincremental solution build: **0 warnings, 0 errors**.
- `dotnet format --verify-no-changes`: passed.
- Three audit-only executable reproductions confirmed O3-F1 through O3-F3 and
  were removed; the tracked test source was restored byte-for-byte.

## Health Decision

The supplied mechanics, arithmetic, policy binding, liveness limits, and core
test matrix are healthy. No conventional security vulnerability was found.
The three findings are deterministic host-integration contract defects with
real turn-cost or lifecycle consequences. Order 3 is therefore reopened as
`partial` until those boundaries are corrected and independently rechecked.
