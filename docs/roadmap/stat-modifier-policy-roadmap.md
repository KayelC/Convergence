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
- Content schema: v3
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
| M1-2 | `pending` | Implement and test the persistent staged reference policy. | `runtime: add persistent staged modifiers` |
| M1-3 | `pending` | Confirm reapplication defaults, then implement and test the timed exclusive reference policy. | `runtime: add timed exclusive modifiers` |
| M1-4 | `pending` | Confirm cap/opposition defaults, then implement and test independently timed contributions. | `runtime: add timed modifier contributions` |
| M1-5 | `pending` | Route skill, item, passive, lifecycle, removal, cleanup, events, and meaningful-success decisions through the selected policy. Prove no bypass remains. | `execution: integrate stat modifier authority` |
| M1-6 | `pending` | Add typed ruleset factory registration and explicit authored selection. Decide and apply the required schema bump without hidden defaults. | `runtime: bind stat modifier policies` |
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
explicit clearing, actor departure, encounter cleanup, snapshot round-trip, and
item behavior at both caps.

## M1-3: Timed Exclusive Policy

Before implementation, the project owner confirms:

- same-direction reapplication behavior: reject, refresh, or replace;
- opposite-direction behavior;
- whether the supplied default always resolves to one magnitude or honors an
  authored multi-stage magnitude;
- reserve suspension default.

The implementation then proves:

- at most one contribution per track;
- one explicit duration and tick event;
- no accidental stacking;
- deterministic reapplication and expiry;
- exact applicability and meaningful-success results;
- restore compatibility and cleanup.

Configuration variants belong inside this coherent policy only when they do not
change the fundamental one-contribution model.

## M1-4: Timed Contribution Policy

The confirmed core behavior is:

- each application retains an independently timed signed contribution;
- each contribution ticks from its own application point;
- expiry removes only that contribution;
- the bounded aggregate is derived from remaining contributions;
- one-stage applications on consecutive turns form the documented rolling
  duration window rather than refreshing one shared timer.

Before implementation, the project owner confirms:

- capped reapplication behavior;
- opposite-sign coexistence or cancellation order;
- multi-stage contribution representation;
- reserve suspension default.

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
