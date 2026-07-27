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
| 18 | `authored_schema_contracts` | Schema v8 authoring workflow, discriminator coverage, semantic limits | Review developer guide; add technical reference |
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
`reviewed`. Order 2 then completed its source review, correction cycles,
ordered-secondary-effect design, and audience reconciliation. The executable
matrix recorded 17 reviewed, 34 `existing_unreviewed`, 17 missing, and 7
`not_applicable` entries after Order 2. Order 3 promotes three additional
audience entries after its source review and correction cycle.

## Reopened Order 2 Implementation Gate

The source-based
[Combat Resolution Order 2 Review](../reviews/combat-resolution-order-2-source-review-2026-07-19.md)
traced the supplied damage formula, accuracy, criticals, affinity precedence,
instant death, passive modifiers, charge state, multi-hit application, and
authored policy binding. It found four reachable implementation gaps and six
mechanics or extension decisions. The project owner confirmed those decisions
on 19 July 2026 in the normative
[Combat Resolution Policy Family](../decisions/combat-resolution-policy-family.md).
Implementation is now governed by the
[Order 2 Combat Resolution Roadmap](combat-resolution-order-2-roadmap.md). A
second independent review found five additional correction subjects; all are
now implemented and source-verified through the
[Order 2 Corrections Roadmap](combat-resolution-order-2-corrections-roadmap.md).
A broader 21 July source review found complete-action and ordered-effect gaps.
The confirmed
[Ordered Secondary Effects decision](../decisions/ordered-secondary-effects.md)
and its
[ordered roadmap](ordered-secondary-effects-roadmap.md) governed its final
closure. The final
[Ordered Effects Closure Review](../reviews/combat-resolution-order-2-ordered-effects-closure-review-2026-07-21.md)
traced current source after O2-R7 through O2-R15, corrected one assessment
parity defect, and found no remaining reachable defect in the reviewed paths.

The later
[Order 2 Closure Source Review](../reviews/combat-resolution-order-2-closure-source-review-2026-07-21.md)
found two reachable safety-boundary defects. The mechanics decisions and three
audience reviews remain owner-confirmed. Bounded hit execution and authored
percentage rejection are implemented and reconciled under the
[Order 2 Closure Corrections Roadmap](combat-resolution-order-2-closure-corrections-roadmap.md).
The first independent recheck found one schema-only resource-percentage range
omission, O2-R22 corrected it, and the O2-R23 current-source recheck found no
further reachable defect at revision `e26bdc5`. A subsequent pre-closure audit
started again from current source and found three narrower discrepancies:
order-dependent duplicate resource costs, invalid host turn-consumption shapes,
and a party-size schema/semantic mismatch. O2-R24 through O2-R27 corrected and
rechecked those paths. The post-R27 source trace found one final malformed
custom-effect result boundary; O2-R28 now rejects it before commit, and O2-R29
found no remaining reachable defect in the supported scope. Exact closure
evidence is in the
[Final Pre-Closure Corrections Review](../reviews/combat-resolution-order-2-final-pre-closure-corrections-review-2026-07-21.md).
The 22 July source review later found direct effect-backed actions did not share
skill/item registration preflight. O2-R30 through O2-R34 established one shared
validator, corrected current terminology and schema labels, documented the
preflight for all three audiences, and independently verified the result in the
[Registration-Parity Corrections Review](../reviews/combat-resolution-order-2-registration-parity-corrections-review-2026-07-22.md).
The later
[Closure-Readiness Review](../reviews/combat-resolution-order-2-closure-readiness-review-2026-07-22.md)
found a reachable charge-ordering defect and an optional-composition mismatch.
The active
[Charge Closure Corrections Roadmap](combat-resolution-order-2-charge-closure-roadmap.md)
owns O2-R35 through O2-R41.

Current checkpoints:

| Checkpoint | State | Required outcome |
|---|---|---|
| O2-H1 | `verified` | Exact receipt flow, optional composition, malformed-receipt rejection, staged rollback, and final source verification are complete. |
| O2-M1 | `verified` | Inert standard chance defaults are removed; typed content remains authoritative. |
| O2-M2 | `verified` | Authored Accuracy, Evasion, and Critical Chance modifiers reach their typed policy boundaries. |
| O2-M3 | `verified` | Authored combat binding returns a coherent neutral aggregate whose exposed authorities are the executing authorities. |
| O2-D1 | `confirmed` | Authored final-damage charge multiplier, whole-action scope, defense-attempt consumption, and mixed-category behavior. |
| O2-D2 | `confirmed` | Authored accuracy plus Agility/evasion and explicit modifiers; Luck excluded from the supplied policy. |
| O2-D3 | `confirmed` | Exact authored critical base, selectable chance and eligibility policies, no hidden Luck. |
| O2-D4 | `confirmed` | Configurable instant-defeat resistance multipliers and one explicit roll. |
| O2-D5 | `confirmed` | Sequential staged multi-hit mutation and immutable hit/target evidence. |
| O2-D6 | `confirmed` | Configurable probability bounds with neutral supplied defaults of `0..100`. |
| O2-DOC | `verified` | Mechanics, developer, and technical charge documentation agree on participation receipts, malformed-receipt rejection, and explicit disabled composition. |

The table records the first implementation cycle; O2-R7 through O2-R16 record
the complete-action and ordered-secondary-effect correction cycle. O2-R18
through O2-R20 add bounded hit execution, authored-percentage parity, and the
resulting audience reconciliation. O2-R22 and O2-R23 record the schema parity
correction and its revision-specific source closure. O2-R24 through O2-R29
record the final pre-closure audit, custom-result correction, and independent
source verification. O2-R30 through O2-R34 record registration-parity,
terminology, active-contract, audience-documentation, and final source
verification corrections. O2-R36 through O2-R38 implement exact charge
participation, supplied optional composition, and audience reconciliation. The
three `combat_resolution` audience entries are `reviewed` again. O2-R39 found
one custom-executor receipt-integrity defect, O2-R40 corrected it atomically,
and O2-R41's current-source recheck found no unresolved reachable defect.
`combat_resolution` has returned to `complete`; Order 3 is complete under the
[Turn Economy Order 3 Roadmap](turn-economy-order-3-roadmap.md). The initial
source review confirmed the supplied Action Token transition table and found
three supported-boundary defects plus one authored-composition gap. The
`turn_economy` returned to `complete` after O3-R1 through O3-R6. O3-R7 then
completed the independent source review and full release gate without finding
an unresolved reachable mechanic defect.

## Reopened Order 3

The [Order 3 source review](../reviews/turn-economy-order-3-source-review-2026-07-22.md)
derives current behavior from Framework source and tests. It records:

- phase-start snapshot/liveness validation before any command;
- stable economy identity, snapshot shape, and state continuity;
- valid immutable typed event evidence;
- authored binding for both supplied economy choices;
- complete supplied transition and liveness tests; and
- explicit separation between economy state and encounter actor scheduling.

O3-R1 through O3-R5 corrected and tested the supported runtime boundary. O3-R6
records the confirmed policy family and reconciles mechanics, developer, and
technical views. All three `turn_economy` audience entries are now `reviewed`.
The executable matrix records 20 reviewed, 32 `existing_unreviewed`, 16
missing, and 7 `not_applicable` entries. O3-R7 independently re-read current
source and documents, corrected the accepted public API baseline, and passed
the complete release gate. The
[final review](../reviews/turn-economy-order-3-final-review-2026-07-22.md)
records 1,496 passing tests, zero skipped tests, zero build warnings, coverage
above the release thresholds, valid active content, all DemoHost modes, and the
real Godot 4.7.1 headless smoke.

A later [independent recheck](../reviews/turn-economy-order-3-independent-recheck-2026-07-22.md)
reproduced three public integration defects at `e6949d7b`. O3-R8 through O3-R10
now guard retained economy authority, enforce coherent command results, and
seal Framework-calculated turn costs. O3-R11 reconciles the developer and
technical references with those contracts and returns both entries to
`reviewed`. The executable documentation matrix records 20 reviewed, 32
`existing_unreviewed`, 16 missing, and 7 `not_applicable` entries.

The [post-correction closure review](../reviews/turn-economy-order-3-post-correction-closure-review-2026-07-22.md)
then re-read the current implementation, tests, host composition, and audience
documents without using earlier conclusions as authority. It found no
unresolved realistic reachable defect, so O3-R12 formally closes Order 3.

The later [owner-closure audit](../reviews/turn-economy-order-3-owner-closure-audit-2026-07-22.md)
reopened that conclusion at `7aa3467e`. Port-provided command and lifecycle
events can currently impersonate runner-owned structural encounter events,
including `TurnEconomyChanged` and `BattleEnded`. O3-R13 must enforce event
provenance, and O3-R14 must reverify all three audiences before Order 3 closes.

O3-R13 now rejects every runner-owned or unclassified event kind at command
and lifecycle ingress before sequencing or publication. O3-R14 independently
re-read the corrected source, tests, host consumers, and audience documents,
then reconciled the event-ownership guidance. The three Order 3 audience
entries are reviewed and Order 3 is formally complete.

The later
[fresh closure audit](../reviews/turn-economy-order-3-fresh-closure-audit-2026-07-22.md)
reopened Order 3 at `6e1169b5`. Explicit `TerminatePhase` consumption is
absolute in the public guidance and both supplied economies, but a custom
economy could leave positive remaining actions and pass runner validation.
O3-R16 now rejects that supported extension violation before downstream
lifecycle or event commitment. O3-R17 re-read the corrected source and
owner-confirmed guidance, then promoted all three audience entries together.
The project owner explicitly confirmed the final six-rule contract on 23 July
2026. Order 3 is formally complete.

Order 4 followed that closure and reviewed status, ailment, passive, and
duration lifecycle from current source before promotion. O4-R2 through O4-R11
implemented the approved runtime, schema, persistence, host, and first
correction boundaries.

A later
[independent audit](../reviews/status-passive-lifecycle-order-4-independent-audit-2026-07-24.md)
reopened Order 4 at `90b1fffb`. Non-ailment dispels and lifecycle-triggered
action-end expiry omit exact typed evidence, passive replacement-dispatch
results are not request-coherent, and reserve-owner battle-start eligibility
required an explicit owner decision. The project owner approved deployed-only
battle-start ownership with an all-participants opt-in and directed the
correction sequence on 24 July 2026. O4-R12 through O4-R15 corrected the
runtime findings, and O4-R16 reconciled all three audience documents.
The
[O4-R17 post-correction review](../reviews/status-passive-lifecycle-order-4-post-correction-review-2026-07-24.md)
closed that revision. A new
[fresh closure audit](../reviews/status-passive-lifecycle-order-4-fresh-closure-audit-2026-07-26.md)
then reproduced one stale ailment-trigger path and one passive activation
restore-coherence gap from current source. Order 4 remains reopened, but O4-R18
now rechecks the exact scheduled ailment instance before trigger execution and
O4-R19 now validates persisted activation keys against the equipped passive's
authored trigger index and event. O4-R20 has reconciled those rules across the
mechanics, developer, and technical audiences. O4-R21 was the only remaining
independent closure gate named by that audit, but its fresh source trace
reproduced turn-start live-enumeration failure and omitted encounter-owned
departure cleanup. The
[R21 extension audit](../reviews/status-passive-lifecycle-order-4-r21-extension-audit-2026-07-26.md)
governs the extension. O4-R22 through O4-R25 recorded and corrected turn-start
exact-instance scheduling, canonical encounter departure cleanup, and the
corresponding mechanics, developer, and technical guidance. O4-R26 completed
the independent review but rejected closure after finding pre-mutation passive
eligibility, exact passive restore coverage, ailment exclusivity restoration,
and explicit defeat-prevention policy composition gaps. The
[R26 correction audit](../reviews/status-passive-lifecycle-order-4-r26-correction-audit-2026-07-26.md)
governs O4-R27 through O4-R32. O4-R27 through O4-R30 corrected the four
runtime paths, and O4-R31 reconciled the mechanics, developer, technical,
architecture, API, roadmap, and executable-matrix guidance. Before O4-R32, the
executable documentation matrix still recorded 21 reviewed, 31
`existing_unreviewed`, 16 missing, and 7 `not_applicable` entries.
The
[final O4-R32 closure review](../reviews/status-passive-lifecycle-order-4-final-closure-review-2026-07-26.md)
subsequently re-read the corrected source, tests, schemas, content, diagrams,
and all three audiences without finding an unresolved realistic reachable
defect at that revision. A
[second independent audit](../reviews/status-passive-lifecycle-order-4-second-independent-audit-2026-07-26.md)
then reproduced two staged-commit defects and found that ailment combat-profile
composition is absent from all three audience documents. Order 4 is reopened
under O4-R33 through O4-R36. O4-R33 through O4-R35 corrected and evidenced all
three findings. The
[O4-R36 closure review](../reviews/status-passive-lifecycle-order-4-r36-closure-review-2026-07-26.md)
then reconciled current source, tests, and all three audiences. Order 4 is
complete at that revision. The
[third independent audit](../reviews/status-passive-lifecycle-order-4-third-independent-audit-2026-07-26.md)
then reproduced cross-target owner-turn sequence failure and shared phase-event
clock divergence. O4-R37 recorded the reopened state. O4-R38 corrected sequence
authority and O4-R39 reconciled the three audiences. The
[O4-R40 closure review](../reviews/status-passive-lifecycle-order-4-r40-closure-review-2026-07-26.md)
then re-read current source, tests, diagrams, and all three audiences without
finding an unresolved reachable defect at that revision. The
[fourth independent audit](../reviews/status-passive-lifecycle-order-4-fourth-independent-audit-2026-07-26.md)
then found one narrow programmatic enum-validation defect and stale save-v10
labels in two stat-modifier audience pages. O4-R41 records the reopened state;
O4-R42 has corrected the supported content/runtime boundary and O4-R43 has
reconciled all three audiences with save v13. The O4-R44 preflight found two
additional current-authority v10 labels; O4-R43A corrected those without
rewriting historical checkpoint records. The
[O4-R44 closure review](../reviews/status-passive-lifecycle-order-4-r44-closure-review-2026-07-26.md)
then independently re-read the corrected implementation and documentation,
passed the complete release gate, and closed Order 4 without an unresolved
realistic reachable defect. The executable documentation matrix at Order 4
closure recorded 24 reviewed, 28 `existing_unreviewed`, 16 missing, and 7
`not_applicable` entries.

The source-backed opening review and owner-approved discovery rules are recorded
in the
[Battle Knowledge Order 5 Source Review](../reviews/battle-knowledge-order-5-source-review-2026-07-27.md).
Order 5 has implemented and independently reviewed checkpoints O5-R1 through
O5-R8. Its confirmed scope separates
persistent entity knowledge from encounter-instance observations, prevents
temporary defenses from corrupting permanent records, keeps ordinary enemy AI
knowledge encounter-local, imports all authored defenses after approved
acquisition paths, and adds policy-controlled restricted Analyze disclosure for
bosses and other special targets. The three audience entries are now reviewed,
bringing the matrix to 27 reviewed, 26 `existing_unreviewed`, 15 missing, and 7
`not_applicable`. The
[O5-R8 Final Review](../reviews/battle-knowledge-order-5-r8-final-review-2026-07-27.md)
found and corrected four reachable integration defects, then found no remaining
realistic Battle Knowledge defect. Owner confirmation and a successful real
Godot headless run in a compatible environment remain before formal closure;
the local Godot 4.7.1 executable crashes during native project startup before
the Convergence smoke path executes.

A final bounded certification subsequently exercised 1,536 deterministic
reserve/deployment clock operations against an independent model and restored
mixed lifecycle state through the public catalog boundary at every supported
checkpoint. It found no qualifying runtime defect. It did expose and correct
one documentation omission: action-scoped `Instant` state must reach committed
outer action-end before a save is captured. The
[final certification record](../reviews/status-passive-lifecycle-order-4-final-certification-2026-07-26.md)
is the formal Order 4 closure authority.

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
