# Encounter Orchestration Order 6 O6-R32 Final Closure Review

**Date:** 4 August 2026
**Scope:** current encounter source, tests, public contract, and all three
audience documents after O6-R29 through O6-R31
**Method:** fresh source-first review; earlier reports were used only to identify
the two correction claims that required adversarial reproduction
**Result:** no unresolved realistic reachable encounter-orchestration defect
found

## Review Standard

A qualifying finding required all four of these elements:

1. an intended encounter invariant expressed by current contracts or approved
   mechanics;
2. a reachable path through a supported public or supplied component;
3. a concrete incorrect mutation, command, cleanup, economy, event, or outcome;
4. reproducible source or test evidence.

Alternative game design, impossible malformed state, and unavoidable side
effects inside explicitly trusted host ports were not promoted into runtime
defects.

## Source Read Directly

The review traced current implementations rather than treating previous
closure claims as evidence:

- `BattleEncounterRunner` request validation, lifecycle transactions, command
  windows, turn economy, reconciliation, completion, fault finalization, and
  immutable results;
- `AutomatedBattleTurnRestrictionResolver` selection construction, canonical
  command identity, restriction validation, assessment, execution, and event
  mapping;
- `AutomatedBattleRunner` canonical runner composition, restricted-turn
  delegation, prepared skill authority, knowledge transitions, and terminal
  result mapping;
- `BattleStatusEncounterLifecyclePort` staged lifecycle and departure cleanup;
- the encounter runner, scheduler, event-contract, lifecycle-mapper, automated
  runtime, preparation, and Godot contract tests;
- mechanics, developer, technical, public API, capability, and documentation
  coverage contracts.

## Corrected Invariants Rechecked

### Canonical restricted-command identity

For every command kind supported by the supplied automated restriction
resolver, action identity comes from the typed command:

| Command | Authoritative identity |
|---|---|
| Basic Attack | command `ActionId` |
| Skill | `Skill.Id` |
| Item | `Item.Id` |
| Guard | `guard` |
| Pass | `pass` |
| Analyze | `analyze` |
| Escape | `escape` |

`AutomatedRestrictedActionSelection` rejects a mismatched detached ID during
construction. `AutomatedBattleTurnRestrictionResolver` independently derives
and compares the ID again before restriction validation or action assessment.
`LimitedAction` compares its allow-list with that derived value. An unsupported
command kind faults with a typed command result and requires a custom resolver.

The original hostile path, an allowed `guard` label beside an Analyze command,
now faults before command selection, effect evidence, resources, knowledge, or
turn economy can mutate. Table-driven coverage repeats the identity and
mismatch check for all seven supplied command kinds.

### One departure reason per defeat period

Turn-start lifecycle decides a Flee or Roster Recall restriction. While the
actor remains deployed, `ReconcileAsync` discards the pending departure reason;
the selected handler or supplied resolver must first commit undeployment.

After that commit, reconciliation inserts the explicit reason before inferred
Defeat and stages departure cleanup against one participant graph. If the actor
is also defeated, the committed selected reason marks that current defeat
period as processed. The bounded fixed-point scan therefore cannot append a
second Defeat cleanup. Defeat announcement remains separately tracked and may
occur once. Recovery removes both period markers so a later defeat is handled
normally.

The permanent adversarial tests cover Flee plus Defeat and Roster Recall plus
Defeat through the real status lifecycle adapter. Each observes exactly one
cleanup reason, preserves a Defeat-only status, emits one defeat announcement,
and reaches stable completion.

## Wider Encounter Invariants Rechecked

- Participant runtime IDs are unique before battle start.
- Initiative returns an exact participating-team permutation.
- Schedule identity, revision, sequence, actor/team ownership, and legal step
  transitions are validated at every boundary.
- Phase command, consecutive free-action, post-command repeat, and
  encounter-wide structural progress are independently bounded.
- Turn-start and turn-end lifecycle changes commit from staged participant
  graphs only after cancellation, event ownership, and turn-economy authority
  checks.
- The runner applies one typed turn consumption; ports cannot silently spend
  the retained economy without being detected.
- Port events are allow-listed and correlated with the frozen participant
  graph and current command actor.
- Completion policies cannot originate faults, identify unknown winners, or
  attach terminal metadata to incomplete results.
- Zero living teams produce a draw; one living team produces victory.
- Cancellation, port exceptions, event-sink failure, and battle-end cleanup use
  distinct typed paths.
- Final participant and event collections are detached immutable snapshots.
- Automated battles use the same asynchronous encounter runner, lifecycle,
  scheduling, turn economy, events, and terminal shapes.

No source path reviewed above reproduced a further qualifying defect.

## Documentation Alignment

The three audience documents now agree on the actual order and ownership:

1. lifecycle decides and commits the restriction;
2. reconciliation observes lifecycle mutation and terminal state;
3. the turn handler or restriction resolver enacts the restriction;
4. the runner validates the command transaction and applies turn economy;
5. reconciliation performs one reason-sensitive departure cleanup and defeat
   announcement per uninterrupted defeat period.

The developer and public API guidance documents the seven canonical IDs and
the custom-resolver requirement for unsupported commands. The technical page
documents both the command transaction and explicit-reason fixed point. The
status lifecycle cross-references use the same departure-period rule.

## Trusted Boundaries And Residual Risk

These are intentional integration boundaries, not unresolved Order 6 defects:

- A custom turn handler is trusted to enact the committed restriction. The
  runner validates its result but does not invent a replacement command.
- Custom handlers and state synchronizers may perform external side effects
  that Framework cannot roll back after they throw.
- An event sink may observe only a prefix before failing; the immutable result
  remains canonical evidence.
- The synchronous wrappers are compatibility conveniences for non-UI callers.
  Godot and other single-threaded UI hosts must await `RunAsync`.
- Rewards, recruitment, and scene transitions remain separate modules after
  the encounter outcome.

## Verification Evidence

| Gate | Result |
|---|---|
| Focused encounter and automated adversarial tests | 281 passed, 0 failed, 0 skipped |
| Full clean solution | 1,869 passed, 0 failed, 0 skipped |
| Framework coverage | 90.78% lines, 76.75% branches; 90%/70% gate passed |
| Strict Release solution build | 0 warnings, 0 errors |
| Framework trimming analysis | 0 warnings, 0 errors |
| Architecture and documentation boundary tests | 57 passed, 0 failed, 0 skipped |
| Formatting, diff, and forbidden-reference checks | Passed |
| Active content validation | 6 packs, 36 documents, and 98 qualified definitions passed |
| DemoHost modes and scripted Training Annex play | Four noninteractive modes and the scripted interactive mode exited successfully |
| Godot contracts and local headless smoke | Contract tests passed; Godot 4.7.1 emitted `CONVERGENCE_GODOT_SMOKE_OK` and exited 0 |

Godot used repository-local `APPDATA` and `LOCALAPPDATA` directories because
the managed Windows environment cannot write the normal engine user-data
location. It printed the known nonfatal Windows root-certificate-store warning
after the successful marker; the process still exited `0`.

## Closure Decision

The current source review found no unresolved realistic reachable defect in the
implemented encounter contract. The capability and its three audience entries
have therefore returned to `complete` and `reviewed`, respectively, subject to
the measured final verification evidence recorded above.

**Order 6 is formally complete.**
