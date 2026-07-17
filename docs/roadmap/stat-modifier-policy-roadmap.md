# Stat Modifier Policy Roadmap

## Summary

This roadmap expands the open typed-effect finding M1 into a controlled
policy-family migration.

The objective is not merely to stop an item being consumed at a stage cap. The
objective is to make stat-modifier application, duration, expiry, removal,
restore, and meaningful-success reporting explicitly replaceable while keeping
one Framework authority.

Three supplied policies will be implemented in separate checkpoints:

- persistent staged modifiers;
- timed exclusive modifiers;
- independently timed modifier contributions.

The reusable development method is defined by the
[Policy Family Design Pattern](../policy-family-design-pattern.md), and the
confirmed mechanic direction is recorded in
[Stat Modifier Policy Family](../decisions/stat-modifier-policy-family.md).

## Baseline

- Branch: `main`
- Starting revision: `29a1d32`
- Solution baseline: 1,054 passing tests, zero failures, zero skips
- Build baseline: zero warnings and zero errors under strict Release build
- Content schema at roadmap start: v3
- Runtime save contract: v9
- Public release state: guarded pre-release `0.1.0`

The current source remains operational but stores one aggregate stage and one
duration per track. M1 remains open until all policy checkpoints, integration,
and reviews are complete.

## Ordered Checkpoints

| Checkpoint | State | Purpose | Suggested commit |
|---|---|---|---|
| M1-0 | `complete` | Confirm the policy-family decision, audit architectural feasibility, create this roadmap, and reopen inaccurate maturity/documentation claims. | `docs: define stat modifier policy family` |
| M1-1 | `complete` | Introduce policy-neutral immutable contracts, retained contribution state, aggregate projection, diagnostics, events, and atomic Framework service ownership. Remove public direct-mutation authority. | `runtime: establish stat modifier policy contracts` |
| M1-2 | `complete` | Implement and test the persistent staged reference policy. | `runtime: add persistent staged modifiers` |
| M1-3 | `complete` | Implement and test the confirmed five-signal timed-exclusive reference policy and duration-clock boundary state. | `runtime: add timed exclusive modifiers` |
| M1-4 | `complete` | Implement and test the confirmed independently timed contribution policy. | `runtime: add timed modifier contributions` |
| M1-5 | `complete` | Route skill, item, passive, lifecycle, removal, cleanup, events, and meaningful-success decisions through the selected policy. Prove no bypass remains. | `execution: integrate stat modifier authority` |
| M1-6 | `complete` | Add typed ruleset factory registration and explicit authored selection. Decide and apply the required schema bump without hidden defaults. | `runtime: bind stat modifier policies` |
| M1-7 | `pending` | Advance the save contract, validate policy-compatible retained state, and restore contributions atomically. | `runtime: persist stat modifier policy state` |
| M1-8 | `pending` | Add cross-policy conformance, content, clean host, Godot-contract, item-consumption, and end-to-end encounter evidence. | `test: prove stat modifier policy parity` |
| M1-DOC | `pending` | Create/revise mechanics, developer, and technical documentation with diagrams and examples for all three policies. | `docs: document stat modifier policies` |
| M1-CR | `pending` | Perform a fresh source-first code review of the completed family and correct substantiated findings in isolated commits. | `review: audit stat modifier policies` |
| M1-DR | `pending` | Review documentation against corrected source, obtain owner confirmation, and only then restore `reviewed`/`complete` status. | `docs: verify stat modifier policy documentation` |

Each checkpoint must be independently green. A later checkpoint may revise the
public shape introduced by M1-1 only when source evidence demonstrates that the
shared contract cannot support a promised policy safely.

### M1-1 Completion Record

- Added immutable policy state, request, decision, result, diagnostic, and
  event contracts under `Convergence.Runtime`.
- Added one Framework service boundary that validates neutral and
  policy-specific state, contains custom-policy faults, derives ordered events,
  and preserves the original snapshot on rejection.
- Removed `RuntimeActorState.ChangeStatStage` from the public API; current
  internal execution paths remain temporarily in place until M1-5 replaces
  their storage and commit authority.
- Added 12 focused contract tests. The checkpoint gate completed with 1,067
  passing tests, zero failures, and zero skips.

## M1-1: Shared Policy-Neutral Contracts

### Required Responsibilities

Define a neutral policy authority that can:

- assess one modifier application without mutation;
- apply an accepted request to immutable track state;
- tick a named lifecycle event;
- remove positive, negative, selected, or all contributions;
- clean up at swap, actor departure, encounter end, and field transition;
- validate whether retained state belongs to the selected policy;
- report actual aggregate and contribution changes.

Candidate names are intentionally not frozen by this roadmap. The checkpoint
must choose names consistent with the public namespace and update the API
baseline deliberately.

### Canonical State Requirements

The retained state must include:

- modifier track ID;
- selected policy identity or an equivalent validated compatibility identity;
- ordered signed contributions;
- optional duration per contribution;
- a deterministic aggregate stage derived within configured bounds.

Zero-value contributions, invalid durations, invalid IDs, arithmetic overflow,
and policy-incompatible shapes are rejected before commit or restoration.

### Atomicity

Policies return immutable transitions. A Framework-owned service commits to the
staged `RuntimeActorState`. Custom policies never mutate a live actor directly.

`RuntimeActorState.ChangeStatStage` must not remain a public alternate authority.
Internal replacement methods exist only to commit validated policy state and to
restore validated snapshots.

## M1-2: Persistent Staged Policy

The supplied default will:

- use configurable signed bounds with a reference default of `-4..+4`;
- move a net stage toward the requested signed delta;
- support multi-stage applications;
- retain state for the encounter without natural turn expiry;
- remove state through explicit positive/negative removal or encounter cleanup;
- report an application at the same-direction cap as unchanged;
- preserve deterministic opposite-direction net movement.

Tests cover every stage, partial clamping, extreme deltas, multi-track effects,
explicit clearing, actor departure, encounter cleanup, snapshot reconstruction,
and unchanged transitions at both caps. Item behavior at both caps remains an
M1-5/M1-8 integration assertion because M1-2 does not yet route effect or
inventory execution through the new authority.

### M1-2 Completion Record

- Added `PersistentStagedStatModifierPolicy` with configurable signed bounds
  and the reference `-4..+4` defaults.
- Each track retains one stable contribution representing its net stage;
  opposite applications move that net value deterministically and remove the
  track when it reaches zero.
- Authored durations are not retained by this policy, lifecycle ticks are
  unchanged operations, swaps preserve state, and actor departure, encounter
  end, and field transition clear it.
- Added 16 focused cases covering bounds, overflow-safe clamping, stable
  sequences, removals, cleanup, canonical-state validation, reconstruction,
  and sequence exhaustion. The checkpoint gate completed with 1,083 passing
  tests, zero failures, and zero skips.

## M1-3: Timed Exclusive Policy

The confirmed supplied policy uses a five-signal `--`, `-`, neutral, `+`, `++`
scale. Equal same-direction application refreshes duration, a stronger signal
upgrades with a fresh duration, a weaker signal is rejected as already in
effect, and opposite signals offset arithmetically. The surviving side owns the
timer: existing duration when the existing signal remains dominant, incoming
duration when the incoming signal becomes dominant, and no timer at neutral.

The implementation proves:

- at most one contribution per track;
- one explicit duration and tick event;
- no accidental stacking;
- deterministic reapplication and expiry;
- exact applicability and meaningful-success results;
- restore compatibility and cleanup.

It also extends retained contribution state and requests with the typed
lifecycle-boundary sequence needed to prevent an application from decrementing
during the same owner turn, phase, round, or action boundary in which it was
created. The supplied clock is owner-turn completion; counted durations retain
their explicit event ID, and authored `SuspendWhileReserve` remains
authoritative.

Configuration variants belong inside this coherent policy only when they do not
change the fundamental one-contribution model.

### M1-3 Completion Record

- Added `TimedExclusiveStatModifierPolicy` with the confirmed `--`, `-`,
  neutral, `+`, and `++` scale represented by nonzero values in `-2..+2`.
- Equal same-direction signals refresh, stronger signals replace, weaker
  signals reject with `AlreadyInEffect`, and opposite signals use the confirmed
  arithmetic and dominant-effect timer rule.
- Added typed lifecycle boundaries to retained contributions, application
  requests, tick requests, and events. The retained cursor protects the
  application boundary, makes duplicate ticks idempotent, and rejects stale
  out-of-order ticks without mutation.
- Counted durations honor their explicit event ID and authored reserve
  suspension. Positive, negative, selected-track, selected-contribution, and
  complete removal share the neutral service contract.
- Added 43 focused cases covering the complete occupied-signal matrix, timer
  ownership, expiry, malformed state, boundary ordering, reserve behavior,
  cleanup scopes, immutable results, and sequence exhaustion. The checkpoint
  gate completed with 1,127 passing tests, zero failures, and zero skips.

## M1-4: Timed Contribution Policy

The confirmed core behavior is:

- each application retains an independently timed signed contribution;
- each contribution ticks from its own application point;
- expiry removes only that contribution;
- the bounded aggregate is derived from remaining contributions;
- one-stage applications on consecutive turns form the documented rolling
  duration window rather than refreshing one shared timer.

At the same-direction aggregate cap, application refreshes the oldest retained
contribution of that sign instead of adding an invisible contribution. Opposite
signs coexist and net together. A multi-stage application is one signed
contribution with one duration. Reserve ticking follows each contribution's
authored `SuspendWhileReserve` value.

Tests include the exact three-turn rolling example, multiple applications in
one phase, stronger multi-stage applications, both signs, both bounds, expiry
ordering, removal scopes, and save/restore in the middle of staggered timers.
The regression must assert the complete post-application sequence from the
[confirmed decision](../decisions/stat-modifier-policy-family.md#confirmed-rolling-duration-example):

- resolved stages: `+1`, `+2`, `+3`, `+3`;
- remaining-duration sets: `[3]`, `[2, 3]`, `[1, 2, 3]`, `[1, 2, 3]` after the
  oldest contribution expires and the fourth is applied;
- no shared-timer refresh;
- `+4` remains reachable when enough contributions are active concurrently.

### M1-4 Completion Record

- Added `TimedContributionStatModifierPolicy` with configurable signed bounds
  and the reference `-4..+4` defaults.
- Each accepted application retains one independently timed signed
  contribution. Opposite signs coexist, expiry removes only the due
  contribution, and the bounded aggregate is recomputed from retained state.
- At a same-direction cap, reapplication refreshes the oldest contribution of
  that sign without adding hidden state. Multi-stage applications remain one
  contribution with one timer.
- Lifecycle boundaries are tracked per contribution. Matching clocks tick
  independently, same-boundary delivery is idempotent, stale delivery rejects
  atomically, and reserve suspension advances the observation cursor without
  consuming duration.
- Added focused coverage for the exact rolling-duration example, both caps,
  cap refresh, opposite-sign reveal, multi-stage contributions, independent
  clocks, reserve behavior, removal and cleanup scopes, malformed state,
  immutable assessment parity, and cross-track lifecycle ordering.
- Added 22 timed-contribution cases. The checkpoint gate completed with 1,150
  passing tests, zero failures, zero skips, and zero compiler warnings.

## M1-5: Canonical Runtime Integration

Route all production mutation and lifecycle paths through one selected policy:

- `ModifyStatStageEffectExecutor`;
- item assessment and execution;
- skill and passive ordered effects;
- `BattleStatusLifecycleService`;
- duration ticking;
- status removal and cleanup;
- actor cloning and transaction commit;
- encounter lifecycle event mapping.

Effect results must distinguish:

- request accepted or rejected;
- aggregate stage delta;
- contributions added, refreshed, removed, or expired;
- duration changes;
- whether canonical state changed.

### M1-5 Completion Record

- `BattleExecutionServices` now requires one explicit
  `IStatModifierPolicyService`; clean console, Godot, and test composition roots
  cannot silently select a stage model.
- Skills, items, passives, lifecycle helpers, positive/negative removal, swap
  cleanup, encounter cleanup, and field cleanup all execute through that one
  selected authority. The former direct actor-stage mutation and status-removal
  bypasses are gone.
- `RuntimeActorState` retains canonical immutable modifier state while exposing
  an aggregate read projection to combat scaling. The old save projection is
  deliberately temporary and remains scheduled for M1-7.
- Assessments use `AssessApplication`; committed effects use `Apply`.
  Multi-track applications stage every transition and commit only when all
  tracks are accepted. Timer refresh is meaningful success even when the
  aggregate stage does not change.
- Effect and lifecycle results carry ordered typed modifier transitions.
  Aggregate changes, contribution changes, and expiry are distinguishable,
  rather than being flattened into one stage-change message.
- Owner-turn lifecycle ports provide monotonic boundaries. Nested passive
  transactions retain the outer staged actor for action-end cleanup, preventing
  legitimate transactional clones from being mistaken for duplicate actors.
- Added 7 focused integration cases. The checkpoint gate completed with 1,157
  passing tests, zero failures, zero skips, and zero compiler warnings.

Inventory consumption uses `StateChanged`. Turn consumption continues to come
from the action command result and selected turn economy.

Boundary searches must find no production caller that can apply, tick, or clear
modifier state outside the policy service.

## M1-6: Ruleset Binding And Authoring

Add a typed host-supplied modifier-policy factory category rather than burying
lifecycle inside stat scaling.

The binding must:

- select a policy by explicit authored ID;
- validate category and parameters;
- expose the three supplied policies through the standard registry;
- allow custom host-registered factories;
- reject missing/unknown/wrong-category policies;
- never infer policy from effect duration or display text.

Adding a first-class category changes the current schema-v3 ruleset enum. The
checkpoint must decide the next schema version, update every active pack and
strict schema together, and document the pre-release break. It must not change
schema merely because runtime snapshot types changed.

### M1-6 Completion Record

- Added `stat_modifier` as an eighth typed ruleset-policy category, with
  explicit binding through `IRuntimeRulesetBindingResolver` and no inferred or
  fallback policy selection.
- Registered the persistent-staged, timed-exclusive, and timed-contribution
  supplied factories. Persistent and contribution policies require authored
  `minimumStage` and `maximumStage` parameters; exclusive accepts no hidden
  configuration. Host applications may register their own typed factory.
- Bound services retain the qualified ruleset definition ID as their policy
  identity. Two differently configured authored rulesets therefore cannot
  restore or operate on one another's retained state accidentally.
- Advanced the pre-release content contract to schema v4 and all six active
  packs to `0.4.0`. Versions 1 through 3 are unsupported; all 36 active
  documents pass both strict JSON Schema and Framework catalog validation.
- Training Annex, its recovery facility, and the Godot reference consumer use
  the same explicitly bound modifier service. Missing, unknown, malformed, or
  wrong-category bindings fail before gameplay instead of selecting a default.
- Added focused ruleset, schema, content, host-failure, and shared-service
  evidence. The checkpoint gate completed with 1,163 passing tests, zero
  failures, zero skips, and zero compiler warnings. Content validation loaded
  6 packs, 36 documents, and 98 qualified definitions; every clean demo and
  the real Godot 4.7.1 headless smoke passed.

## M1-7: Persistence And Restore

The current save-v9 stat-stage shape cannot retain independent contributions.
This checkpoint must:

- advance `RuntimeSaveGameSnapshot.CurrentContractVersion`;
- capture policy identity and ordered contribution state;
- validate IDs, signs, bounds, duration domains, ordering, and aggregate
  consistency;
- reject incompatible policy restoration with typed diagnostics;
- restore complete sessions atomically;
- update DemoHost and Godot host-owned JSON envelopes;
- retain the migration extension seam without inventing an unnecessary
  automatic migration for unreleased saves.

## M1-8: Cross-Policy Evidence

Shared conformance tests run equivalent actions under each policy. They prove
that authorization, targeting, transaction atomicity, effect ordering, item
reservations, and host neutrality do not change when policy behavior changes.

Required end-to-end scenarios include:

- skill application and cost commitment;
- item application, rollback, and exactly-one consumption;
- passive application;
- owner-turn and phase lifecycle boundaries;
- status removal;
- reserve/deployment behavior according to selected configuration;
- encounter cleanup;
- save and aggregate restore;
- Godot host-supplied policy selection;
- no console, Godot, filesystem, or serializer type in Framework.

## Documentation Checkpoint

Following `docs/documentation-design-pattern.md`, create or revise:

- mechanics: observable stage, duration, reapplication, expiry, removal, and
  examples for every supplied policy;
- developer guide: ruleset registration, selection, custom policy contract,
  host presentation, and persistence compatibility;
- technical: authority, immutable transition, state diagrams, tick ordering,
  transaction commit, event payloads, and restore validation.

Do not promote these entries while implementation or owner defaults remain
unresolved.

## Code Review Checkpoint

The fresh review must inspect current source rather than roadmap claims and
trace:

1. every public and internal mutation entry;
2. all three policy implementations;
3. item applicability and consumption;
4. lifecycle ticking and cleanup;
5. stage scaling projections;
6. runtime capture and restore;
7. ruleset binding and custom factories;
8. event completeness and immutability;
9. host neutrality and API boundaries;
10. adversarial and cross-policy tests.

Each finding must identify an intended invariant, reachable path, concrete
consequence, and reproducible evidence.

## Documentation Review Checkpoint

Render and inspect every Mermaid diagram, verify examples against executable
tests, check links and terminology, and compare formulas/defaults directly with
source. The project owner confirms the final mechanics before the documentation
matrix returns to `reviewed`.

## Quality Gate Per Checkpoint

Run focused tests followed by:

```powershell
dotnet test Convergence.sln --no-restore --configuration Release
dotnet build Convergence.sln --configuration Release --no-restore --no-incremental -warnaserror
dotnet format Convergence.sln --verify-no-changes --no-restore
git diff --check
```

Also run affected DemoHost modes, content validation, Godot contract/smoke
coverage, documentation links, API checks, schema checks, terminology checks,
and Framework forbidden-reference searches when the checkpoint touches them.

## Completion Criteria

M1 closes only when:

- all three reference policies are implemented and selectable;
- no direct mutation or lifecycle bypass remains;
- item applicability and consumption follow actual policy transitions;
- content and save contracts are versioned consistently;
- clean hosts demonstrate policy selection without owning rules;
- all tests pass with zero skips and strict builds have zero warnings;
- the fresh code review has no unresolved correctness finding;
- all three audience documents pass source review;
- the project owner confirms the final documented defaults;
- capability and documentation matrices are promoted honestly.

## Non-Goals

- Implementing a bonus-action turn economy is not part of M1.
- Changing existing stage multiplier tables is not part of M1.
- Adding presentation animations or battle UI is not part of M1.
- Reintroducing archived prototype code or vocabulary is not part of M1.
- Treating every policy option as one universal class is explicitly rejected.
