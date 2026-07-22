# Turn Economy Order 3 Post-Correction Closure Review

**Review date:** 22 July 2026

**Reviewed revision:** `4c6dde7`

**Verdict:** complete; no unresolved realistic reachable defect was found in
the Order 3 scope.

## Review Method

This review was performed from the current implementation rather than from the
earlier review conclusions. The inspection traced:

- the two supplied economy implementations and their immutable snapshots;
- validated action-consumption and action-outcome contracts;
- action assessment and execution paths that produce turn costs;
- complete-action outcome aggregation, including item behavior;
- encounter phase creation, command windows, lifecycle boundaries, event
  publication, liveness limits, terminal outcomes, and fault containment;
- authored ruleset registration and binding;
- automated-battle, Training Annex, DemoHost, and Godot composition;
- direct transition, malformed-extension, authority-drift, liveness, and
  command-shape tests; and
- mechanics, developer, technical, decision, content-contract, and roadmap
  statements that describe the capability.

Earlier reports were used only as revision history after the current source
trace was complete.

## Findings

No High, Medium, or Low actionable finding remains in the reviewed Order 3
surface.

The review standard required an intended invariant, a realistic reachable
path, a concrete consequence, and reproducible evidence. No inspected concern
met all four conditions after O3-R8 through O3-R11.

## Confirmed Runtime Behavior

### Consumption authority

`ActionTurnConsumption` and `TurnEconomyResolution` validate their enum and
payload combinations at construction. Framework-calculated assessment and
execution-result costs are getter-only. Host-mediated commands may select a
different valid cost through their validating initializer, which is the
intentional host extension surface.

`ActionTokenTurnEconomy` exposes `Apply` as its only public consumption
mutation. Its transitions are:

| Input | Result |
|---|---|
| Normal | Consume partial first, otherwise full. |
| Pass | Consume partial first; only an all-full pool converts full to partial. |
| Weakness or Critical | Convert full to partial, otherwise consume partial. |
| Miss or Null | Consume up to two tokens, partial first. |
| Repel, Absorb, or explicit termination | Clear the phase. |
| None | Preserve the supplied state. |

The strategic pass case is therefore `[partial, full] -> [full]`, while
`[full] -> [partial]` remains the useful all-full pass conversion.

`StandardActionTurnEconomy` is genuinely neutral: Normal, Pass, and an
effect-derived outcome each spend one ordinary opportunity. It does not
reinterpret affinity or critical evidence.

### Authored selection and replacement

`RuntimeRulesetPolicyFactoryRegistry.CreateStandard()` registers both
`standard_actions` and `standard_action_token`. Each binding requires explicit
positive `maximumCommands` and a nonnegative
`maximumConsecutiveFreeActions` lower than that command limit. Unknown or
malformed parameters reject binding; there is no silent economy fallback.

The resulting `BattleTurnEconomyRuleset` supplies both the economy factory and
the matching liveness policy. Training Annex and the Godot reference consumer
bind `standard_action_token` from catalog content and pass both values to the
encounter runner.

### Encounter authority and liveness

For every team phase, `BattleEncounterRunner`:

1. creates and starts one economy;
2. validates the initial immutable snapshot against reported liveness;
3. retains the accepted snapshot as the phase authority;
4. rejects identity, concrete type, state, or liveness drift across lifecycle,
   handler, event, and synchronization callbacks;
5. applies exactly one validated command cost;
6. accepts the resulting state only when the transition is coherent; and
7. revalidates the final state before phase-end lifecycle commits.

Turn-start, owner-turn-end, and phase-end actor changes are staged while their
corresponding lifecycle callback is checked. Command and free-action limits
bound custom economies that fail to make progress. Port exceptions and
contradictory command results become typed encounter faults rather than a
second spend or an escaped exception.

### Command and event contracts

Executed commands may continue or request Victory, Defeat, Escape, or Draw.
Cancelled, rejected, and faulted commands must be free and carry their matching
outcome; fault/rejection requires a diagnostic, and only Victory or Defeat may
name a winning team.

Phase-start, transition, and phase-end events carry typed immutable economy
snapshots. Their constructors reject invalid IDs, null state, null
consumption, or mixed economy identities and snapshot types. Event
construction revalidates record-cloned payloads. Presentation can therefore
use typed payloads without parsing debug text.

## Documentation Cross-Check

| Confirmed statement | Source agreement |
|---|---|
| Action Token is optional | `IBattleTurnEconomy` plus two registered supplied policies. |
| Pass consumes partial before converting full | Direct implementation and transition tests agree. |
| Items spend a normal action by default | Standard action-outcome aggregation uses `ItemActionOutcomeBehavior.Normal`; effect-driven pricing remains selectable. |
| Economy does not schedule actors | The encounter runner owns team phases and actor rotation. |
| Host owns presentation | Godot and DemoHost consume typed events; Framework contains no engine UI. |
| Liveness limits are authored | Both supplied factories require the two finite parameters. |
| Mid-battle economy state is not saved | The current save aggregate represents session checkpoints, not suspended command windows. |

The mechanics, decision, developer, and technical documents all describe the
same implemented transition table and responsibility split.

## Residual Boundaries, Not Defects

- The current encounter scheduler uses ordered team phases and rotating active
  actors. Agility-interleaved turns and immediate same-actor bonus windows need
  the separate scheduler work assigned to Documentation Order 6.
- Turn-economy phase state is intentionally absent from current session saves.
  Suspend-inside-battle requires a versioned encounter checkpoint design.
- Custom economies must keep `Apply`, snapshot capture, and liveness reporting
  exception-safe. Custom turn handlers remain responsible for the atomicity of
  their own external mutations.
- `CleanBattleDemoHost` directly composes an Action Token ruleset as a focused
  code sample. The original-content Training Annex and Godot reference paths
  demonstrate authored catalog binding.

These limits are documented and do not contradict the completed Order 3
contract.

## Verification Evidence

The post-correction release gate at `4c6dde7` passed:

- 1,505 tests: 1,325 Framework, 173 DemoHost, and 7 Content Validator;
- zero failed or skipped tests;
- strict .NET 8 Release builds with zero warnings;
- 90.65% Framework line coverage and 76.16% branch coverage;
- all 6 active packs, 36 documents, and 98 definitions;
- all four noninteractive DemoHost modes and scripted Training Annex play;
- trimming analysis;
- the real Godot 4.7.1 headless smoke; and
- format, diff, boundary, dependency, and active-content checks.

The initial sandboxed Godot invocation could not write its normal user-data
log path. Running the identical smoke command with access to that engine-owned
directory passed all integration markers. No engine binary or generated file
is tracked.

The closure review itself reran 156 focused turn-economy, encounter, ruleset,
and documentation tests, followed by the complete 1,505-test solution. Both
passed with zero failures or skips. A nonincremental warning-as-error Release
build completed with zero warnings, `dotnet format --verify-no-changes` passed,
and `git diff --check` reported no whitespace error.

## Closure

Order 3 is ready to remain `complete`. Its three audience documents remain
`reviewed`, and no further turn-economy correction checkpoint is justified by
the current source. Order 4, status and passive lifecycle, is the next planned
documentation subject. Closely related actor-scheduling work remains assigned
to Order 6 rather than being pulled into this closure.
