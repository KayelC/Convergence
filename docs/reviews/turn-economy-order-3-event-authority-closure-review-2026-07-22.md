# Turn Economy Order 3 Event-Authority Closure Review

**Date:** 22 July 2026
**Implementation revision:** `87fc8ad`
**Capability:** `turn_economy`
**Verdict:** ready for formal closure

## Method

This review was performed from the corrected source, tests, sample-host
consumers, and active audience documents. Earlier review conclusions were not
used as implementation evidence. The trace followed event creation from each
encounter port through validation, sequencing, publication, economy
application, lifecycle completion, and the final result.

An actionable defect required an intended invariant, a reachable supported
path, a concrete consequence, and reproducible evidence. No such remaining
Order 3 defect was found.

## Source Findings

No actionable findings remain in the reviewed Order 3 scope.

`BattleEncounterEventOwnership` uses an explicit allow-list for the ten event
kinds a command or lifecycle port may author:

- command selection, passing, execution, and rejection;
- effect, passive, status, and resource evidence; and
- encounter-presence and host-action requests.

Every current structural kind is outside that allow-list: actor creation,
battle start, initiative, rounds, phases, turns, restrictions, accepted
turn-economy transitions, defeat, faults, and battle completion. Future event
kinds also fail closed until ownership is deliberately assigned.

The turn-handler result is validated inside the existing asynchronous port
boundary. A forbidden event becomes `TurnHandlerExecutionFailed` before event
publication, economy application, or owner-turn-end lifecycle. Battle-start,
turn-end, phase-end, and battle-end lifecycle event collections pass through
the same ownership rule before their staged transaction commits; rejection is
reported as `LifecycleExecutionFailed`.

The automated clean runner no longer returns a `BattleFaulted` event. It may
return `ActionRejected` detail with a faulted command result, after which the
encounter runner publishes the single canonical `BattleFaulted` and
`BattleEnded` sequence. The DemoHost can therefore trust the runner-owned
`TurnEconomyChanged` payload it consumes.

## Mechanics Recheck

The correction does not change any supplied economy rule:

- standard actions spend one opportunity for Normal, Pass, and effect-derived
  consumption;
- Action Token consumes partial before full for Normal and Pass;
- Pass converts full to partial only when no partial token exists;
- Weakness and Critical convert a full token or consume a lone partial;
- Miss and Null consume up to two tokens, partial first;
- Repel, Absorb, and explicit termination clear the phase; and
- `None` remains unchanged and subject to the authored liveness bounds.

Outcome aggregation, item pricing, ruleset binding, scheduling separation, and
the deliberate exclusion of mid-battle economy state from session saves also
remain unchanged.

## Documentation Recheck

The mechanics document now states that structural encounter evidence has one
runner authority. The developer guide identifies the exact port-owned
allow-list and explains how to report a fault without manufacturing a
`BattleFaulted` event. The technical reference records both ownership sets,
the fail-closed rule, validation timing, and typed fault codes. The decision
record preserves the same architectural boundary.

These descriptions agree with the current code. Actor scheduling remains an
Order 6 concern and is not being smuggled into the opportunity-counting
contract. No adjacent order needs to be pulled forward to close Order 3.

## Verification

O3-R13 established the executable baseline:

- 194 focused turn-economy, encounter, aggregation, ruleset, and automated
  runner tests passed;
- all 1,529 solution tests passed with no skips;
- the warning-as-error Release build completed with zero warnings;
- formatting and diff checks passed; and
- all four noninteractive DemoHost modes completed successfully.

After documentation reconciliation, O3-R14 passed 194 focused tests, 24
documentation and executable-matrix tests, and all 1,529 solution tests with
no failures or skips. The warning-as-error Release build completed with zero
warnings. Formatting, diff validation, and all four noninteractive DemoHost
modes also passed.

## Closure Decision

Order 3 is complete. Its supplied policies, extension boundary, canonical
runner integration, structural event provenance, tests, and three audience
documents agree. The next full review is Order 4: status, ailment, passive,
and duration lifecycle.
