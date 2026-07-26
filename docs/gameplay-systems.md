# Gameplay Systems

This document summarizes the reusable systems currently implemented by `Convergence.Framework`. Presentation and game-specific composition belong to the host.

## Content And Catalog

The framework provides immutable definitions for skills, entities, races, ailments, items, equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets. Strict `System.Text.Json` DTOs are mapped into serializer-neutral definitions, semantically validated with explicit registrations, dependency-checked, qualified, and exposed through immutable repositories.

Runtime rulesets resolve through a host-supplied typed policy-factory registry.
The standard composition is explicit and replaceable; unknown policy IDs fail
without selecting a fallback. See [Ruleset Policy Contracts](ruleset-policy-contracts.md).

## Actors, Stats, Resources, And Growth

Catalog actor factories hydrate identity, level, stats, defenses, resources,
learned skills, active skills, and passives. Runtime snapshots preserve
canonical actor state.

The standard Vessel composition service takes core stats, defenses, active
skills, and passives from the Active Hosted Entity selected by the canonical
party roster. The Vessel retains its own progression, current resources,
equipment, status, affiliation, and encounter presence. Composition rejects
atomically when source identity, ownership, skills, stats, or resources are
invalid.

Progression services own stat resolution, stage scaling, experience curves,
level growth, resource recalculation, allocation, and rollback through injected
policies. Live level growth processes authored skill unlocks. A full move list
creates a persisted pending replace-or-forget choice rather than dropping the
skill or cancelling the level.

## Actions And Effects

Typed commands cover basic attacks, skills, items, guard, pass, analyze, escape, Hosted Entity swaps, Companion deployment/recall, and host-mediated actions. Skills and items share targeting, condition evaluation, ordered effects, diagnostics, and transaction-safe inventory decisions. Behavior comes from typed definitions, not display text.

The canonical battle-action facade now requires an inventory reservation for
one item use, validates the returned reservation before effect execution, and
publishes staged actor changes only after the required inventory transition
succeeds. It also authorizes equipped canonical skills and resolved
basic-attack profiles during assessment and immediately before execution.
Action-outcome policy receives the source kind and immutable effect facts:
skills and basic attacks are effect-driven, while the supplied item default is
one normal turn with an authored effect-driven option.
Lower-level `SkillExecutor` and `ItemExecutor` services remain available for
callers that deliberately own the omitted loadout or inventory boundary.

## Combat And Turn Economy

Combat rules resolve damage, accuracy, criticals, elemental affinity, ailment
resistance, instant-death channels, chance, and power through bound policies.
Every supplied random draw validates the host's promised range before becoming
authoritative. Almighty, shields, Break, affinity replacement, and separated
resistance channels are explicit typed rules.

Turn economy is a separate optional policy family. The standard registry binds
either neutral `standard_actions` or `standard_action_token`, with explicit
finite phase limits. The encounter runner validates one immutable economy
snapshot chain and emits typed phase and before/after transition payloads.
Economies count action opportunities; encounter orchestration still owns team
and actor scheduling, while hosts own token presentation.

## Status And Passives

The optional lifecycle service handles battle start, turn restrictions, turn
end, explicit action/actor/phase/round clocks, reserve suspension or configured
reserve advancement, typed departure cleanup, staged ailment application, and
passive trigger dispatch. Turn-start behavior and owner-turn-end triggers both
use boundary-start exact-instance schedules, so custom status mutation cannot
execute stale or newly added slots. The canonical runner composed with the
supplied lifecycle port dispatches flee, roster-recall, and newly observed
defeat cleanup before completion.
Expiration and permitted removal causes are separate. Custom-handler execution
is transactional. Passive targeting and activation counting are explicit, and
target eligibility is frozen before passive effects can change the staged actor
graph. Supplied passive event policies never replace an explicit host
registration. Committed lifecycle changes reach hosts as typed events. Save
restore requires one enabled/disabled state per equipped passive and rejects
active ailments that conflict within one exclusivity group. See
[Status And Passive Lifecycle](mechanics/status-passive-lifecycle.md) and its
[integration guide](developer-guide/status-passive-lifecycle.md).

The stat-modifier policy family separately supplies persistent staged,
timed-exclusive signal, and independently timed contribution policies. Timed
state retains explicit lifecycle-boundary cursors for same-boundary protection
and idempotent ticking. Skill, item, passive, removal, lifecycle, cleanup,
ruleset binding, save validation, and aggregate restore paths now use that one
immutable policy authority. The combat stage-scaling policy remains a separate
replaceable choice.

## Encounters, AI, Knowledge, And Rewards

The encounter runner owns initiative, phases, turns, lifecycle dispatch, command execution, liveness, cancellation, outcomes, and ordered events. Its events expose immutable typed payloads instead of making debug messages authoritative, so Godot, console, and test hosts can map the same event to different presentation. Strategy ports allow deterministic or host-defined action selection. Player knowledge can persist through snapshots, while encounter AI knowledge may be scoped to one battle. Negotiation and reward services return immutable outcomes without owning presentation.

## Party, Rosters, Inventory, And Economy

The party aggregate is the only authority for active/reserve placement, Hosted
Entity ownership, Companion ownership, and Active Hosted Entity selection. An
active Hosted Entity remains in its roster; a deployed Companion remains owned
while also occupying an active-party slot. Encounter presence remains separate
actor state.

Transition services enforce runtime-ID uniqueness, approved overlap roles,
roster capacity, deploy/recall/swap/consume behavior, item stacks, unique
equipment ownership, equip compatibility, wallet arithmetic, shop
transactions, and restoration transactions. Hosts own UI and durable inventory
storage.

## Navigation, Traversal, And Encounter Preparation

Generic navigation uses arbitrary `ContentId` locations and injected access policy. Optional dungeon traversal uses arbitrary node IDs and injected traversal policy. Neither service prescribes scenes, menus, floors, or automatic battles. Hosts explicitly trigger authored encounters; preparation services hydrate ordered runtime actors from catalog formations.

## Fusion, Inheritance, And Compendium

Fusion services resolve typed recipes and strategy policies, build deterministic candidate plans, validate inherited skill selections, construct previews, and assess transactions. Inheritance precedence is typed and shared between preview and commit. Compendium services distinguish first acquisition from explicit updates: `RecordAcquisition` adds a missing entry but preserves an existing snapshot, while `RegisterActor` is the deliberate add-or-update operation. Recall pricing and familiar-knowledge import remain separately configurable.

## Persistence

Versioned snapshots cover actors, party and rosters, inventory, equipment, wallet, optional field/traversal state, Compendium, knowledge, session progress, and checkpoint breadcrumbs. Validation rejects inconsistent IDs, references, numeric domains, timed state, capacities, and catalog provenance before restore. Aggregate restoration resolves actor profiles, restores Hosted Entity dependencies before Vessels, and exposes no partial session on rejection. Hosts own serialization, slots, suspend-save storage, scene reconstruction, and UI.

## Demonstration Coverage

DemoHost provides focused battle, field, and save demonstrations plus the original Training Annex end-to-end slice. The [capability matrix](roadmap/framework-capability-matrix.md) records whether each framework area is complete, partial, or deferred independently from demo breadth.

Detailed actor integration is documented in
[Actors And Runtime State](developer-guide/actors-and-runtime-state.md), with
maintainer invariants in
[Runtime Actor State And Restoration](technical/runtime-actor-state-and-restoration.md).
