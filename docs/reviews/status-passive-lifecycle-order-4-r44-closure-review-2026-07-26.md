# Status And Passive Lifecycle Order 4 R44 Closure Review

## Review Identity

- Review date: 26 July 2026
- Reviewed revision: `862f30ae`
- Branch: `main`
- Capability: `status_and_passive_lifecycle`
- Method: fresh source, test, schema, content, and documentation inspection
  followed by the complete release gate

This review did not treat an earlier report, roadmap statement, or passing test
total as proof of correctness. Those records were consulted only after the
current implementation had been traced. A concern qualified as a finding only
when it had an intended invariant, a realistic supported path, a concrete
consequence, and reproducible evidence.

## Verdict

**Order 4 is complete at the reviewed revision.**

No unresolved realistic, reachable correctness defect was found in the
reviewed status and passive lifecycle boundary. O4-R42 closes the malformed
Companion flee-outcome path. O4-R43 and O4-R43A align current documentation
with save contract v13. The capability can return from `partial` to `complete`,
and its mechanics, developer-guide, and technical documentation entries can
return from `existing_unreviewed` to `reviewed`.

This verdict does not claim that custom extension code can have its external
side effects rolled back, that mid-encounter scheduler state is persisted, or
that hosts no longer own transitions performed outside the encounter runner.
Those are explicit product boundaries rather than hidden defects.

## Source Trace

### Authored ailment validation

The semantic content validator now requires every
`ChanceSkipOrFleeAilmentTurnBehaviorDefinition.CompanionFleeOutcome` to be a
defined enum value. This protects programmatic definitions and custom
deserializers, not only the built-in JSON path. The built-in JSON converter and
schema independently accept only `recall_to_roster` and `escape_battle`.

The lifecycle service repeats the enum check before resolving Fear behavior.
That defense-in-depth check executes inside the staged actor transaction.
Consequently, an invalid direct runtime request cannot leak the earlier Guard
clear or any ailment mutation into the live actor.

Both valid outcomes retain distinct behavior:

- `RecallToRoster` recalls an eligible deployed Companion and otherwise uses
  the ordinary flee result when recall is unavailable; and
- `EscapeBattle` produces the battle-flee restriction directly.

There is no implicit "all other values mean escape" branch.

### Ailment application and active state

Application stages the complete participant graph before invoking application
or transition policies. Validation covers target life state, application
gates, resistance, chance, same-ailment refresh, exclusivity, replacement, and
custom result coherence before a successful staged graph may commit.

Active runtime state retains the authored lifetime and removal profile.
Runtime application and save restoration both enforce exclusivity groups, so a
snapshot cannot restore a combination that ordinary application would reject.

### Turn-start restrictions

Turn start runs against a staged actor. Guard clears before restrictions are
resolved, but the clear commits only with the complete successful result.

The service snapshots ordered pairs of ailment ID and exact active instance,
then verifies each pair again before it receives a slot. Removal, replacement,
or refresh invalidates the stale slot. A newly added ailment waits for the next
turn-start boundary. Custom-handler failure rolls back the entire staged
operation.

Restriction aggregation uses deterministic typed precedence. Equal limited
restrictions intersect their allowed action sets; an empty intersection becomes
Skip rather than silently granting a broader command set.

### Owner-turn end and lifecycle clocks

For a deployed actor, owner-turn end executes passives, ailment effects,
recovery, and duration advancement in the documented order. Ailment effects
also use exact-instance scheduling, preventing a removed or replaced instance
from firing later in the same boundary.

Duration authority remains explicit:

- instant state expires at the outermost action-end boundary;
- counted state advances only on its authored event ID;
- phase state advances only on its authored phase ID;
- battle state is removed by battle-end cleanup; and
- permanent state has no automatic clock.

The encounter lifecycle port provides one committed sequence stream per event
ID across actors and teams. Timed modifiers therefore observe a coherent
boundary identity during cross-target owner-turn effects, team phases, and
rounds. The supplied reserve policy suspends reserve state; the opt-in policy
advances only explicitly supported shared boundaries.

### Passive dispatch

Passive dispatch validates the participant graph and snapshots enabled
definitions and pre-mutation target eligibility before execution. It stages
all Framework actor mutations and validates the complete dispatch result before
commit.

Executed evidence must identify an equipped passive, an authored trigger index
and event, an eligible target, unique activation evidence, and authored effect
indices and IDs. A non-executed result cannot carry committed effect evidence.
Recursion and activation limits remain explicit policies.

### Encounter integration and cleanup

The encounter lifecycle port routes battle start, turn start, owner-turn end,
team phase, round, battle end, and actor departure through the same lifecycle
authority. The encounter runner stages flee, roster recall, newly observed
defeat cleanup, and terminal lifecycle transitions before committing the live
participant graph.

Cancellation or lifecycle failure before commit preserves live participants.
Cleanup events remain ordered before defeat narration or terminal outcome
processing. Manual deployment and roster transitions performed outside the
runner remain host-owned and must request the corresponding cleanup explicitly.

### Persistence, restoration, and immutable evidence

Save validation and direct actor restoration validate active ailment
definitions, lifetime state, exclusivity, equipped passive state, activation
keys, trigger indices, event IDs, and aggregate per-target actor references.
Stat-modifier state is restored through the selected policy.

Public lifecycle results defensively snapshot their collections, including
record-clone inputs. Ailment transitions, duration ticks, removals, modifier
changes, passive activations, passive effects, departure reasons, and resource
changes expose typed evidence. Optional detail text is diagnostic only.

## Documentation Review

The following current-authority audience documents were re-read against the
source and executable contracts:

- mechanics: `status-passive-lifecycle.md` and
  `stat-modifier-policies.md`;
- developer guide: `status-passive-lifecycle.md` and
  `stat-modifier-policies.md`; and
- technical: `status-passive-lifecycle.md` and
  `stat-modifier-policy-runtime.md`.

They agree on application, exclusivity, restriction precedence, exact-instance
scheduling, lifecycle order, duration clocks, reserve policy, cleanup causes,
passive eligibility and evidence, stat-modifier composition, transaction
limits, and persistence behavior.

The dedicated stat-modifier pages, actor integration guide, and public API
contract now identify runtime save contract v13. Historical decision and
roadmap entries retain the save version that was current at their explicitly
named checkpoint; they are not current integration instructions.

No diagram implies that rejection is a stored state, that presentation text is
rule authority, or that reserve state ages without an injected policy.

## Findings

No unresolved finding met the review threshold.

The following deliberate boundaries remain documented and were not promoted to
defects:

- extension code may perform external side effects that Framework transactions
  cannot reverse;
- the supported save flow captures session state outside an active encounter,
  not the encounter scheduler itself;
- hosts own lifecycle calls for manual transitions performed outside the
  canonical encounter transaction; and
- field-time status aging occurs only when a host explicitly dispatches an
  applicable lifecycle clock.

## Verification Evidence

The corrected revision passed:

- focused Order 4 tests: **340 passed, 0 failed, 0 skipped**;
- full Release suite: **1,677 passed, 0 failed, 0 skipped**:
  - Framework: **1,497**;
  - ContentValidator: **7**;
  - DemoHost: **173**;
- strict nonincremental Release build: **0 warnings, 0 errors**;
- formatting verification: passed;
- content validation: **6 packs, 36 documents, 98 qualified definitions**;
- Framework coverage: **90.69% lines, 76.45% branches**;
- trimming analysis: **0 warnings, 0 errors**;
- all four noninteractive DemoHost modes: passed;
- scripted Training Annex play through a real terminal: passed;
- Godot contract tests: **6 passed**;
- Godot 4.7.1 headless smoke: `CONVERGENCE_GODOT_SMOKE_OK`;
- documentation links, API baseline, and active product-boundary guards: passed;
- `git diff --check`: passed.

The first sandboxed Godot invocation could not create its `user://logs`
directory and terminated in native code. The same repository build passed when
run with normal host filesystem access, demonstrating an execution-sandbox
restriction rather than a Framework or sample-host defect.

## Closure Decision

O4-R44 is complete. `status_and_passive_lifecycle` is promoted to `complete`,
its three documentation audiences are promoted to `reviewed`, and Order 5
`battle_knowledge` becomes the next collaborative documentation subject.
