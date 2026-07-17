# Documentation Completion Roadmap

## Purpose

This roadmap turns Convergence's documentation debt into an active,
capability-by-capability production program. It does not treat an existing page
as reviewed merely because it reads plausibly or passes a link check.

The executable authority remains
[`documentation-coverage-matrix.json`](../../tests/Convergence.Framework.Tests/Fixtures/documentation-coverage-matrix.json).
This roadmap controls the order in which those entries complete the
[Documentation Design Pattern](../documentation-design-pattern.md).

## Starting State

At the 17 July 2026 alignment review, the matrix contained 75 audience entries:

| State | Count |
|---|---:|
| `reviewed` | 11 |
| `existing_unreviewed` | 37 |
| `missing` | 20 |
| `not_applicable` | 7 |

Three capabilities have completed collaborative review across every applicable
audience:

- `runtime_actor_state`;
- `progression_and_resources`;
- `party_and_rosters`.

`persistence_snapshots` has reviewed developer and technical guidance, but its
mechanics page still requires owner confirmation. Every other implemented
capability remains in the queue below. The implementation state in the
[Framework Capability Matrix](framework-capability-matrix.md) is independent of
this documentation state.

## Review Unit

One capability ID is the normal unit of work. A review may read adjacent
capabilities to understand interactions, but it promotes only the capability
whose complete evidence and owner decisions were examined.

Each review must:

1. inspect current source, tests, schemas, and clean host evidence;
2. explain current behavior, ownership, failures, timing, and configuration in
   plain language;
3. compare historical prototype behavior only when it helps recover a design
   decision, never as current authority;
4. present discrepancies and unresolved choices to the project owner;
5. record confirmed decisions before changing behavior;
6. write or revise every applicable mechanics, developer, and technical view;
7. add diagrams and focused examples where state or ordering is not obvious;
8. identify concrete source and test evidence;
9. run documentation links, coverage-ledger tests, relevant subsystem tests,
   and `git diff --check`;
10. promote only the reviewed audience entries in the executable matrix.

If source behavior changes during a documentation review, that correction is a
separate implementation commit with focused regression tests. The documentation
review resumes against the corrected source afterward.

## Ordered Capability Queue

The order follows the runtime dependency direction: actions first, then combat
and lifecycle, encounter composition, resources and world state, higher-level
social/fusion systems, authoring infrastructure, and host integration.

| Order | Capability ID | Primary questions to resolve | Expected audience work |
|---:|---|---|---|
| 1 | `typed_action_and_effect_execution` | Assessment/execution parity, targeting, reservations, atomic mutation, host-mediated actions | Review mechanics and technical pages; add developer guide |
| 2 | `combat_resolution` | Damage, accuracy, criticals, affinity outcomes, instant death, configured policy boundaries | Review mechanics and technical pages; add developer guide |
| 3 | `turn_economy` | Action Token outcomes, pass precedence, liveness, replacement policies | Review mechanics and technical pages; add developer guide |
| 4 | `status_and_passive_lifecycle` | Application, exclusivity, duration clocks, reserve suspension, cleanup, rollback | Review mechanics and technical pages; add developer guide |
| 5 | `battle_knowledge` | Encounter-local AI knowledge, persistent player knowledge, analysis, familiar imports | Review mechanics and technical pages; add developer guide |
| 6 | `encounter_orchestration` | Initiative, phases, commands, lifecycle ordering, cancellation, faults, typed events | Review mechanics and technical pages; add developer guide |
| 7 | `inventory_equipment_economy` | Ownership, reservation, equipment effects, pricing policies, atomic transactions | Review mechanics and technical pages; add developer guide |
| 8 | `navigation` | Generic transition authority, policy rejection, host scene ownership, persistence | Review mechanics and technical pages; add developer guide |
| 9 | `dungeon_traversal` | Optional traversal state, authored floors/events, encounter requests, host exploration | Review mechanics and technical pages; add developer guide |
| 10 | `negotiation_and_rewards` | Prompt/event ports, demands, cancellation, acquisition, reward arithmetic and application | Review mechanics and technical pages; add developer guide |
| 11 | `fusion_and_inheritance` | Recipe authority, catalyst shifts, inheritance legality, preview/commit parity, mutation | Review mechanics and technical pages; add developer guide |
| 12 | `compendium` | First acquisition, explicit overwrite, recall, pricing policy, knowledge import | Review mechanics and technical pages; add developer guide |
| 13 | `persistence_snapshots` | Player-facing save/suspend semantics and required neutral aggregates | Complete mechanics owner review; reconfirm reviewed integration references |
| 14 | `content_definitions` | Immutable domain shapes, family boundaries, runtime-state exclusion | Review developer and technical references |
| 15 | `portable_deserialization` | Host-supplied text, strict conversion, diagnostics, serializer boundary | Review developer guide; add technical reference |
| 16 | `content_validation` | Structural versus semantic authority, registrations, dependency visibility | Review developer guide; add technical reference |
| 17 | `catalog_loading` | Qualification, dependency order, repository lookup, collision handling | Review developer guide; add technical reference |
| 18 | `authored_schema_contracts` | Schema v3 authoring workflow, discriminator coverage, semantic limits | Review developer guide; add technical reference |
| 19 | `host_contracts` | Commands, events, cancellation, content sources, randomness, application ownership | Review developer and technical references |
| 20 | `godot_adapter` | `res://` loading, Node mapping, signals, save envelope, headless proof | Review developer and technical references |

## Active Order 1 Review

The source-based
[Typed Action And Effect Execution Order 1 Review](../reviews/typed-action-and-effect-execution-order-1-review-2026-07-17.md)
records the current correction and decision backlog. Order 1 remains active and
its documentation matrix entries remain unreviewed.

The project owner approved both authority decisions on 17 July 2026. Their
normative record is
[Battle Action Ownership And Inventory Authority](../decisions/battle-action-ownership-and-inventory-authority.md).

Current checkpoints:

| Checkpoint | State | Required outcome |
|---|---|---|
| O1-M1 | `open` | Make one item action consume exactly one inventory unit. |
| O1-M2 | `open` | Validate reservation identity, quantity, and lifecycle state. |
| O1-D1 | `approved_pending_implementation` | Require an inventory port and exactly-one reservation for item commands. |
| O1-D2 | `approved_pending_implementation` | Make Framework validate skill and resolved basic-attack authority. |
| O1-DOC | `blocked_by_corrections` | Correct mechanics prose and add developer and technical documents after behavior is confirmed. |

This active record does not promote implementation or documentation state. Code
corrections receive separate focused commits before the documentation review
resumes against corrected source.

## Deferred Documentation

Two capability IDs describe extension seams rather than completed mechanics:

- `save_version_migration`: document a concrete migration only when two released
  save contracts require one. Current guidance may explain rejection and the
  migration interface, but it must not invent a migration.
- `deterministic_replay`: checkpoint breadcrumbs are diagnostic only. Replay
  documentation waits for an approved deterministic replay design.

Their matrix entries remain unreviewed or missing until implementation and owner
decisions exist. Deferral is not permission to remove the entries.

## Completion Gate

Documentation completion is reached only when:

- every implemented capability has `reviewed` or justified `not_applicable`
  entries for all three audiences;
- the two deferred capabilities are either implemented and reviewed or remain
  explicitly deferred in the current product roadmap;
- no active mechanics, developer, or technical page carries an unrecorded
  discrepancy with source;
- the project owner has confirmed every player-visible rule and product-level
  extension decision;
- documentation tests, subsystem tests, links, and current-contract guards are
  green.

Until then, Convergence documentation is accurately described as structured and
partially reviewed, not complete.
