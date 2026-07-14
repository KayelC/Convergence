# Gameplay Systems

This document summarizes the reusable systems currently implemented by `Convergence.Framework`. Presentation and game-specific composition belong to the host.

## Content And Catalog

The framework provides immutable definitions for skills, entities, races, ailments, items, equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets. Strict `System.Text.Json` DTOs are mapped into serializer-neutral definitions, semantically validated with explicit registrations, dependency-checked, qualified, and exposed through immutable repositories.

## Actors, Stats, Resources, And Growth

Catalog actor factories hydrate identity, level, stats, defenses, resources, learned skills, active skills, and passives. Runtime snapshots preserve canonical actor state. Progression services own stat resolution, experience curves, level growth, resource recalculation, allocation, and rollback through injected policies.

## Actions And Effects

Typed commands cover basic attacks, skills, items, guard, pass, analyze, escape, stock/form transitions, and host-mediated actions. Skills and items share targeting, condition evaluation, ordered effects, diagnostics, and transaction-safe inventory decisions. Behavior comes from typed definitions, not display text.

## Combat And Turn Economy

Combat rules resolve damage, accuracy, criticals, elemental affinity, ailment resistance, instant-death channels, chance, and power through bound policies. Action Token is one optional `IBattleTurnEconomy`; games may supply another economy. Almighty, shields, Break, affinity replacement, and separated resistance channels are explicit typed rules.

## Status And Passives

The lifecycle service handles battle start, turn restrictions, turn end, duration ticking, reserve suspension, cleanup, ailment application, and passive trigger dispatch. Custom-handler execution is transactional. Passive modifiers support deterministic stacking and typed affinity or ailment replacements.

## Encounters, AI, Knowledge, And Rewards

The encounter runner owns initiative, phases, turns, lifecycle dispatch, command execution, liveness, cancellation, outcomes, and ordered events. Strategy ports allow deterministic or host-defined action selection. Player knowledge can persist through snapshots, while encounter AI knowledge may be scoped to one battle. Negotiation and reward services return immutable outcomes without owning presentation.

## Party, Stock, Inventory, And Economy

Transition services enforce runtime-ID uniqueness, active/reserve roles, form and stock capacity, summon/return/swap/consume behavior, item stacks, unique equipment ownership, equip compatibility, wallet arithmetic, shop transactions, and restoration transactions. Hosts own UI and durable inventory storage.

## Navigation, Traversal, And Encounter Preparation

Generic navigation uses arbitrary `ContentId` locations and injected access policy. Optional dungeon traversal uses arbitrary node IDs and injected traversal policy. Neither service prescribes scenes, menus, floors, or automatic battles. Hosts explicitly trigger authored encounters; preparation services hydrate ordered runtime actors from catalog formations.

## Fusion, Inheritance, And Compendium

Fusion services resolve typed recipes and strategy policies, build deterministic candidate plans, validate inherited skill selections, construct previews, and assess transactions. Inheritance precedence is typed and shared between preview and commit. Compendium services distinguish first acquisition from explicit updates: `RecordAcquisition` adds a missing entry but preserves an existing snapshot, while `RegisterActor` is the deliberate add-or-update operation. Recall pricing and familiar-knowledge import remain separately configurable.

## Persistence

Versioned snapshots cover actors, party/stock, inventory, equipment, wallet, optional field/traversal state, Compendium, knowledge, session progress, and checkpoint breadcrumbs. Validation rejects inconsistent IDs, references, numeric domains, timed state, capacities, and catalog provenance before restore. Hosts own serialization, slots, suspend-save storage, and UI.

## Demonstration Coverage

DemoHost provides focused battle, field, and save demonstrations plus the original Training Annex end-to-end slice. The [capability matrix](framework-capability-matrix.md) records whether each framework area is complete, partial, or deferred independently from demo breadth.
