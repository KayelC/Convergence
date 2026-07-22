# Turn Economy Order 3 Termination Contract Closure Review

**Date:** 22 July 2026
**Implementation revision reviewed:** `b16da99`
**Capability:** `turn_economy`
**Verdict:** ready for formal closure

## Review Method

This review re-read the corrected implementation rather than treating prior
reports as authority. The trace covered:

- validated construction of `ActionTurnConsumption` and
  `TurnEconomyResolution`;
- every transition in `StandardActionTurnEconomy` and
  `ActionTokenTurnEconomy`;
- authored policy-factory registration and liveness binding;
- phase start, command-window continuity, economy application, owner-turn-end
  lifecycle, transition publication, synchronization, and phase end in
  `BattleEncounterRunner`;
- runner-owned structural event provenance;
- adversarial replacement-economy tests; and
- the mechanics, developer, technical, decision, roadmap, and executable
  matrix records.

The review also searched active Framework, sample, and test consumers of
`TerminatePhase` and `TerminatesPhase` to ensure the two concepts were not
silently collapsed.

## Corrected Invariant

`ActionTurnConsumption.TerminatePhase` is a policy-independent command. After
the selected economy applies it, the encounter runner now requires:

```text
After.RemainingActions == 0
HasTurnsRemaining() == false
```

The existing economy-state validation proves those two facts agree. The new
transition validation then rejects any positive remainder through
`TurnEconomyTransitionInvalid` before:

- owner-turn-end lifecycle is called;
- `TurnEconomyChanged` is published; or
- another actor receives a command window.

The regression uses the exact previously reachable shape: a valid custom
economy begins with two actions and subtracts one for every non-free command.
After explicit termination it retains one action. The corrected runner faults
after the first command, records one `Apply` call, performs no owner-turn-end
lifecycle, and publishes no accepted economy transition.

## Policy-Specific Distinction

The review confirmed that `TurnEconomyResolution.TerminatesPhase` is different.
It belongs to an effect-derived resolution carried by
`ActionTurnConsumptionKind.TurnEconomy`, so the selected economy interprets it:

- Action Token honors terminating effect outcomes such as Repel and Absorb;
- Standard Actions deliberately prices effect-derived outcomes as one ordinary
  action; and
- a host that must end every possible economy returns the explicit
  `ActionTurnConsumption.TerminatePhase` command instead.

This preserves the documented neutrality of Standard Actions and does not make
Action Token outcome pricing mandatory for replacement policies.

## Confirmed Behavior

Current source and direct tests agree on all reviewed Order 3 rules:

- Action Token starts with one full token per active phase actor.
- Normal and Pass consume a partial token before a full token.
- Pass converts full to partial only when no partial token exists.
- Weakness and Critical convert a full token or consume a lone partial token.
- Miss and Null consume up to two tokens, partial first.
- Repel and Absorb terminate the supplied Action Token phase.
- Standard Actions spends one action for every non-free, non-terminal command.
- `None` does not require a state change, but finite free-action and absolute
  command limits still guard liveness.
- Economy identity, concrete snapshot type, complete accepted state, and
  liveness remain authoritative throughout the phase.
- Lifecycle work is staged and discarded when retained economy authority
  changes across a callback.
- Command and lifecycle ports cannot publish runner-owned structural events.
- Turn economy counts opportunities; the current runner, not the economy,
  owns team phases and actor rotation.

## Findings

No realistic reachable defect remains in the reviewed Order 3 scope.

The public event payload constructor can represent host-created diagnostic
transitions, but command and lifecycle ports cannot inject a
`TurnEconomyChanged` event into the encounter. Canonical runner events are
created only after the corrected transition guard. Expanding standalone event
construction into a second policy executor would duplicate runtime authority
and is not required for closure.

Individual agility scheduling, immediate same-actor bonus windows, and
mid-battle suspend restoration remain explicitly separate scheduler and
persistence work. They are not unfinished transitions in either supplied turn
economy.

## Documentation Reconciliation

All three active audiences now state the same contract:

- mechanics explains that explicit termination applies to replacement
  policies as well as supplied ones;
- the developer guide gives the required custom-policy postcondition and typed
  fault;
- the technical state machine places validation before lifecycle and event
  acceptance; and
- the policy decision distinguishes universal explicit termination from
  policy-specific effect-derived outcomes.

An executable documentation synchronization test protects those statements.
The capability matrix records `turn_economy` as `complete` with no known gap,
and all three documentation entries are `reviewed`.

## Verification

- focused turn-economy, encounter, binding, capability, and documentation
  tests: 182 passed;
- complete solution: 1,531 passed, 0 failed, 0 skipped;
- strict nonincremental Release build: 0 warnings, 0 errors;
- formatting and `git diff --check`: passed; and
- clean battle, field, save, and Training Annex demos: exited successfully.

## Closure Decision

O3-R16 and O3-R17 satisfy the reopened correction cycle. Order 3 is formally
complete. Order 4, status and passive lifecycle, is the next collaborative
documentation order.
