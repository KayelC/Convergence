# Turn Economy Order 3 Fresh Closure Audit

**Date:** 22 July 2026
**Revision reviewed:** `6e1169b5`
**Capability:** `turn_economy`
**Verdict:** not ready for formal closure

## Review Method

This review was performed from current Framework source, active tests, sample
host composition, active content, and the mechanics, developer, technical, and
decision documents. Earlier review reports and their conclusions were not used
as implementation evidence.

The source trace covered:

- `ActionTurnConsumption` and `TurnEconomyResolution` construction;
- `StandardActionTurnEconomy` and `ActionTokenTurnEconomy` transitions;
- complete-action outcome production for skills, basic attacks, and items;
- authored factory registration and ruleset binding;
- phase start, command windows, lifecycle boundaries, economy application,
  liveness limits, typed events, and phase completion;
- automated, DemoHost, and Godot composition paths; and
- direct economy, encounter-runner, action-outcome, binding, and host tests.

An actionable finding below identifies an intended invariant, a supported
reachable path, a concrete consequence, and reproducible source evidence.

## Findings

### M1. A replacement economy can ignore explicit phase termination

**Severity:** Medium

**Intended invariant**

`ActionTurnConsumption.TerminatePhase` is the economy-independent instruction
to end the current phase. Both supplied economies clear all remaining
opportunities, and all three active audience documents describe this as an
absolute command rather than an optional pricing hint.

**Reachable path**

1. A game supplies a custom `IBattleTurnEconomy`, which is a supported public
   extension point.
2. A phase begins with two or more remaining opportunities.
3. A valid host turn handler returns
   `BattleEncounterCommandResult.Executed(ActionTurnConsumption.TerminatePhase)`.
4. The custom economy's `Apply` implementation changes its snapshot but leaves
   `RemainingActions` greater than zero. A simple implementation that subtracts
   one for every non-`None` cost is sufficient.
5. `BattleEncounterRunner.ValidateEconomyTransition` accepts the result because
   the economy ID and snapshot type are stable, liveness agrees with the
   positive remaining count, and the snapshot changed.
6. The runner opens another command window instead of ending the phase.

**Concrete consequence**

A host command that explicitly ends a phase can allow another actor to act.
The consecutive-free-action guard does not help because the economy changed;
only the much broader absolute command limit eventually bounds a policy that
continues doing this. The supplied Standard Action and Action Token policies
are unaffected, but the advertised replacement-policy contract is not
currently enforced.

**Reproduction shape**

```csharp
public void Apply(ActionTurnConsumption consumption)
{
    if (consumption.Kind != ActionTurnConsumptionKind.None && _remaining > 0)
    {
        _remaining--;
    }
}
```

With `_remaining == 2`, applying `TerminatePhase` produces a valid-looking
snapshot with one action remaining. Current transition validation accepts it.

**Source evidence**

- `ActionTurnConsumption.TerminatePhase` is the public explicit command in
  `src/Convergence.Framework/Execution/BattleActionExecutor.cs`.
- both supplied policies clear their state in
  `src/Convergence.Framework/TurnEconomy/BattleTurnEconomy.cs` and
  `src/Convergence.Framework/TurnEconomy/ActionTokenTurnEconomy.cs`;
- the encounter applies the returned command cost in
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs` around the
  `turn-economy-apply` boundary; and
- `ValidateEconomyTransition` in the same file does not require zero remaining
  actions for `TerminatePhase`.

**Required correction**

- Require a `TerminatePhase` transition to report both zero remaining actions
  and `HasTurnsRemaining() == false`.
- Reject a violating custom policy through the existing typed
  `TurnEconomyTransitionInvalid` fault before owner-turn-end lifecycle or a
  transition event is accepted.
- Add a runner regression proving the next command does not execute.
- Retain every supplied transition exactly as it is today.

### L1. Executable documentation state contradicts the closure claim

**Severity:** Low; formal documentation gate

The Order 3 roadmap and documentation overview state that all three audience
entries are reviewed. The executable documentation matrix currently records
the mechanics entry as `existing_unreviewed`, even though its reason calls the
page reviewed. Developer and technical entries are marked `reviewed`, but M1
also means their absolute-termination statements do not yet match enforced
runtime behavior.

This does not change battle execution, but it makes formal closure ambiguous
and defeats the purpose of the executable documentation gate.

**Required correction**

- Keep all three Order 3 audience entries `existing_unreviewed` while M1 is
  open.
- After the runtime correction, re-read source and all three audience pages,
  obtain owner confirmation, and promote all three together.

## Confirmed Healthy Behavior

The review found no defect in these current rules:

- Action Token phase start grants one full token per active living actor.
- Normal and Pass consume a partial token before a full token.
- Pass converts full to partial only when no partial token exists.
- Weakness and Critical convert full to partial or consume the last partial.
- Miss and Null consume up to two tokens, partial first.
- Repel and Absorb terminate the supplied Action Token phase.
- Standard Actions prices every non-free, non-terminal command as one action.
- outcome aggregation remains separate from opportunity accounting;
- non-escape items use normal cost by default and may opt into effect-driven
  pricing through combat policy composition;
- initial state, ID, concrete snapshot type, liveness, and accepted continuity
  are validated throughout a phase;
- phase command and consecutive-free-action limits are explicit authored
  values;
- structural encounter events are runner-owned through a fail-closed port
  allow-list; and
- active DemoHost and Godot consumers use typed snapshots rather than parsing
  debug text.

## Adjacent Orders

Order 6 is closely related because actor scheduling decides who receives the
opportunities counted by Order 3. It is not appropriate to jump into Order 6
for this correction. The current defect is entirely expressible and fixable at
the existing Order 3 transition boundary. Immediate same-actor bonus turns,
agility-ordered turns, and other schedules remain deliberate Order 6 work.

Order 4 remains the next documentation order only after M1 is corrected and
Order 3 receives a final source-and-document recheck.

## Verification At Reviewed Revision

- focused economy, encounter, binding, action, and DemoHost tests: 368 passed;
- complete solution: 1,529 passed, 0 failed, 0 skipped;
- strict nonincremental Release build: 0 warnings, 0 errors.

These green results confirm the current tested behavior. They do not cover the
custom `TerminatePhase` violation described by M1.

## Closure Decision

Order 3 is reopened. The supplied Action Token and Standard Action mechanics
remain sound, but the supported replacement-policy contract needs one focused
runtime correction and one documentation reconciliation before formal closure.
