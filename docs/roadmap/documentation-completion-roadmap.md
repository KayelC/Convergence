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

At that alignment review, three capabilities had completed collaborative
review across every applicable audience:

- `runtime_actor_state`;
- `progression_and_resources`;
- `party_and_rosters`.

`persistence_snapshots` has reviewed developer and technical guidance, but its
mechanics page still requires owner confirmation. At that starting point, every
other implemented capability remained in the queue below. The implementation
state in the [Framework Capability Matrix](framework-capability-matrix.md) is
independent of this documentation state.

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
| 18 | `authored_schema_contracts` | Schema v4 authoring workflow, discriminator coverage, semantic limits | Review developer guide; add technical reference |
| 19 | `host_contracts` | Commands, events, cancellation, content sources, randomness, application ownership | Review developer and technical references |
| 20 | `godot_adapter` | `res://` loading, Node mapping, signals, save envelope, headless proof | Review developer and technical references |

## Completed Order 1

The source-based
[Typed Action And Effect Execution Order 1 Review](../reviews/typed-action-and-effect-execution-order-1-review-2026-07-17.md)
records the original correction and decision history. Those corrections remain
valid, and the three audience documents completed the collaborative workflow
that was known at the time.

A later source-first
[independent review](../reviews/typed-action-and-effect-execution-order-1-independent-review-2026-07-17.md)
found two additional reachable paths. Automated skill authorization was
corrected. The stat-modifier finding exposed a broader design decision:
Convergence would supply three selectable modifier lifecycle policies rather
than preserve the accidental aggregate-duration hybrid. That work was
completed through the
[Stat Modifier Policy Roadmap](stat-modifier-policy-roadmap.md).

The project owner approved both authority decisions on 17 July 2026. Their
normative record is
[Battle Action Ownership And Inventory Authority](../decisions/battle-action-ownership-and-inventory-authority.md).

Current checkpoints:

| Checkpoint | State | Required outcome |
|---|---|---|
| O1-M1 | `verified` | One item action reserves and commits exactly one inventory unit. |
| O1-M2 | `verified` | Reservation identity, quantity, and lifecycle state are validated before effects. |
| O1-D1 | `verified` | Item commands require an inventory port and an exactly-one reservation. |
| O1-D2 | `verified` | Framework validates equipped canonical skills, canonical catalog items, and resolved basic attacks. |
| O1-IR-H1 | `verified` | Automated battles use the canonical skill-authorization policy. |
| O1-IR-M1 | `verified` | M1-1 through M1-8, the fresh source review, and its substantiated corrections are complete. |
| O1-DOC | `verified` | All three audience documents match corrected source and the project owner confirmed the explanation. |

The owner confirmed the timed-exclusive signal arithmetic, rejection behavior,
dominant-duration rule, independently timed rolling example, cap refresh,
explicit lifecycle clocks, same-boundary protection, bonus-action handling,
cancellation behavior, and reserve suspension on 17 July 2026. A final
source-first comparison then traced authorization, prepared assessments,
targeting, skill costs, item reservations, actor transactions, every supplied
modifier policy, lifecycle integration, ruleset binding, and persistence. The
owner confirmed that resulting Order 1 explanation on 18 July 2026.

The earlier
[post-correction review](../reviews/typed-action-and-effect-execution-order-1-post-correction-review-2026-07-17.md)
remains evidence for the original checkpoints. The expanded scope is closed by
the [final Order 1 closure review](../reviews/order-1-final-closure-review-2026-07-18.md).
All three `typed_action_and_effect_execution` audience entries are now
`reviewed`. The executable matrix therefore records 14 reviewed, 36
`existing_unreviewed`, 18 missing, and 7 `not_applicable` entries. Order 2,
`combat_resolution`, is next.

## Active Order 2

The source-based
[Combat Resolution Order 2 Review](../reviews/combat-resolution-order-2-source-review-2026-07-19.md)
traced the supplied damage formula, accuracy, criticals, affinity precedence,
instant death, passive modifiers, charge state, multi-hit application, and
authored policy binding. It found four reachable implementation gaps and six
mechanics or extension decisions. The project owner confirmed those decisions
on 19 July 2026 in the normative
[Combat Resolution Policy Family](../decisions/combat-resolution-policy-family.md).
Implementation is now governed by the
[Order 2 Combat Resolution Roadmap](combat-resolution-order-2-roadmap.md).

Current checkpoints:

| Checkpoint | State | Required outcome |
|---|---|---|
| O2-H1 | `implemented_pending_review` | Split and Unified policies now use authored charge multipliers, reject occupied slots, consume once per committed matching action, and persist policy identity in save v11. |
| O2-M1 | `planned` | Remove the two inert standard chance defaults while retaining explicit authored values. |
| O2-M2 | `planned` | Consume authored Accuracy, Evasion, and Critical Chance modifiers at typed policy boundaries. |
| O2-M3 | `planned` | Make authored combat-policy binding return a genuinely replaceable neutral aggregate. |
| O2-D1 | `confirmed` | Authored final-damage charge multiplier, whole-action scope, defense-attempt consumption, and mixed-category behavior. |
| O2-D2 | `confirmed` | Authored accuracy plus Agility/evasion and explicit modifiers; Luck excluded from the supplied policy. |
| O2-D3 | `confirmed` | Exact authored critical base, selectable chance and eligibility policies, no hidden Luck. |
| O2-D4 | `confirmed` | Configurable instant-defeat resistance multipliers and one explicit roll. |
| O2-D5 | `confirmed` | Sequential staged multi-hit mutation and immutable hit/target evidence. |
| O2-D6 | `confirmed` | Configurable probability bounds with neutral supplied defaults of `0..100`. |
| O2-DOC | `blocked_by_corrections` | Complete and confirm all three audience documents against corrected source. |

Until these checkpoints close, `combat_resolution` remains `partial` and its
documentation entries remain unreviewed or missing. The active overview pages
are evidence to revise, not confirmed combat authority.

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
