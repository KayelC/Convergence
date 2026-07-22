# Turn Economy Order 3 Final Review

**Review date:** 22 July 2026

**Reviewed revision:** `b793e777` plus the O3-R7 public-API baseline correction

**Capability:** `turn_economy`

**Conclusion:** complete; no unresolved realistic reachable defect found in the
supported turn-economy scope

## Method

This review re-read the corrected implementation and tests rather than treating
the Order 3 roadmap or earlier review as proof. The trace covered:

- the generic `IBattleTurnEconomy` boundary;
- `StandardActionTurnEconomy` and `ActionTokenTurnEconomy`;
- authored ruleset factories for `standard_actions` and
  `standard_action_token`;
- phase setup, command execution, lifecycle dispatch, liveness limits, and phase
  closure in `BattleEncounterRunner`;
- typed phase and turn-economy event payloads;
- every framework action-consumption producer;
- automated-battle, DemoHost, and Godot reference-consumer composition; and
- malformed extension implementations exercised by focused contract tests.

The review asked whether a realistic host or supported extension could execute
a command from contradictory economy state, change economy identity or snapshot
shape mid-phase, publish malformed typed evidence, evade finite liveness, or
receive behavior different from the confirmed mechanics.

## Confirmed Runtime Behavior

### Phase authority

The encounter runner captures the phase-start snapshot and liveness result,
checks that they agree, and rejects the phase before lifecycle or command work
when they do not. The accepted snapshot then forms a continuous state chain:
the state immediately before a command must equal the last accepted state, and
the state returned by `Apply` becomes the next accepted state.

The economy ID and concrete snapshot type must remain stable for the entire
phase. Phase-end lifecycle cannot run against state that changed outside the
accepted `Apply` transition.

### Supplied policies

`standard_actions` spends one opportunity for `Normal`, `Pass`, and explicit
turn-economy consumption. `None` leaves the count unchanged. Explicit
termination ends the phase.

`standard_action_token` preserves the confirmed Action Token table. Normal
actions consume partial before full. Pass consumes an existing partial token;
only an all-full pool converts one full token into one partial token. Weakness
and Critical convert a full token to partial before consuming partial; Miss and
Null spend up to two tokens partial-first; Repel and Absorb terminate the
phase. These transitions are covered directly, including
`[partial, full] -> [full]` on pass.

Both policies require authored `maximumCommands` and
`maximumConsecutiveFreeActions` values. The exact threshold behavior and reset
after a consuming action are tested. No hidden liveness default remains in the
standard ruleset registry.

### Typed evidence

Public snapshots and phase/economy event payloads reject missing IDs,
contradictory before/after identities, and mismatched concrete snapshot types.
`BattleEncounterEvent` revalidates nested payloads so malformed record clones
cannot bypass their original construction boundary.

## Finding Corrected During The Gate

The strict API test initially rejected the new
`StandardRulesetPolicyIds.StandardActions` member because it had been recorded
in `PublicAPI.Unshipped.txt`. This repository deliberately requires that file
to remain empty after the pre-release API is intentionally accepted. O3-R7
moves the entry into `PublicAPI.Shipped.txt` and restores the unshipped file to
its required sentinel line.

This was release-contract bookkeeping, not a runtime or mechanic failure. The
focused API gate and complete test suite passed after the correction.

## Deliberate Boundaries

- Turn economy owns available action opportunities and their consumption. The
  encounter runner still owns team phases and actor rotation.
- The current runner rotates after each executed command window, including a
  free action. An immediate same-actor bonus or an agility-interleaved schedule
  requires the separate encounter-scheduler design assigned to Documentation
  Order 6.
- A `None` consumption does not dispatch owner-turn-end lifecycle because no
  owner turn was committed.
- Framework can fault a malformed custom economy after `Apply`, but it cannot
  roll back unrelated arbitrary mutations performed inside a host-supplied
  custom command handler. Custom handlers remain responsible for transactional
  behavior at that extension boundary.
- Phase-local economy state is intentionally ephemeral and is not represented
  as a persisted battle-resume snapshot in save contract v11.

These are documented product boundaries, not hidden claims of completed
functionality.

## Verification Evidence

- focused Order 3 runtime and documentation tests: 145 passed;
- complete solution: 1,496 passed, 0 failed, 0 skipped;
- strict Release solution build: 0 warnings, 0 errors;
- formatting verification: passed;
- locked restore and NuGet vulnerability audit: passed;
- Framework coverage: 90.65% lines and 76.07% branches;
- active content validation: 6 packs, 36 documents, 98 qualified definitions;
- clean battle, field, save, and Training Annex runtime demos: exit 0;
- scripted Training Annex play: exit 0 without interactive input;
- Godot 4.7.1 .NET sample build: 0 warnings, 0 errors;
- local Godot headless smoke: exit 0 with
  `CONVERGENCE_GODOT_SMOKE_OK` after granting its required `user://` write
  access;
- architecture, documentation-link, API, and forbidden-reference tests:
  passed; and
- active content files: unchanged by Order 3.

## Closure Decision

Order 3 is closed. The implementation, executable contract matrix, mechanics
page, developer composition guide, technical state machine, decision record,
and capability evidence agree on the supported behavior. The next collaborative
documentation subject is Order 4: status, ailment, passive, and duration
lifecycle.
