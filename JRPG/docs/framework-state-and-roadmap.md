# Framework State And Roadmap

> **Status: Current source of truth.** This document replaces the old active track-plan stack. Archived plans remain useful history, but this file is the first place to read when deciding what the framework is, what was built, what is missing, and what should happen next.

## Why This Document Exists

The project moved through many implementation tracks quickly. That created a real ownership problem: the codebase now contains a large framework, a still-active console prototype, many adapter layers, and several historical plans that can make the project feel like it belongs to the migration process rather than to its owner.

This document resets the map.

It does not approve removing legacy code. It does not declare the framework finished. It records the current shape honestly so future work can be chosen deliberately, in plain language, without casually inventing new tracks or silently following stale plans.

## Current Repository Shape

The branch is `track-12-recovery`.

The solution currently has two main runtime assemblies:

- `JRPG.Framework`: the reusable, engine-neutral class library.
- `JRPG.ConsoleHost`: the root executable and compatibility host.

Supporting areas:

- `Convergence.Tests`: framework, host, parity, characterization, content, and runtime tests.
- `Data/Jsons`: legacy prototype datasets plus clean reference/demo/original content packs.
- `docs`: active documentation.
- `ArchiveDocs`: historical plans, generated notes, and future legacy-code archive area.

The console host depends on the framework. The framework must not depend on the console host, filesystem, Godot, Newtonsoft.Json, the static legacy `Database`, legacy DTOs, or live `Combatant` / `Persona` objects.

Future development should be framework first. Legacy adapters may protect existing behavior, but they should not drive new framework requirements unless the same requirement is useful for an engine-neutral game.

## What Was Built

### Content And Catalog Framework

The framework has a clean content pipeline:

- immutable content IDs and SemVer support;
- domain definitions for skills, entities, races, ailments, items, equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets;
- `System.Text.Json` based deserialization isolated behind serializer-neutral public contracts;
- validation with explicit host registrations;
- manifest and dependency loading;
- qualified-ID catalog construction;
- repositories for catalog lookup.

The clean content surface is broad enough to describe the major gameplay families, but most production-scale content has not been authored yet.

### Skill, Effect, Passive, And Item Runtime

The framework contains reusable execution services for:

- active skills;
- passive triggers and rule modifiers;
- ordered effects;
- items and shared field/battle effects;
- targeting and conditions;
- resource changes, revival, cure/removal, stat stages, charge, shield, affinity override, analysis, escape, and custom host requests.

The skill system is much more coherent than the old string-driven prototype path. It separates damage elements, ailment resistances, instant-death resistance, inheritance groups, menu groups, and passive behavior.

### Battle Runtime

The framework owns a clean battle foundation:

- elemental affinity resolution;
- ailment and instant-death resistance lookup;
- combat knowledge stores;
- Press Turn resolution;
- production combat ruleset services;
- action assessment and execution;
- battle status lifecycle;
- battle encounter orchestration;
- deterministic automated battle runner;
- catalog-backed actor hydration.

The console battle still uses legacy live objects and legacy skills/items for ordinary interactive gameplay, but many choices now pass through framework-shaped command, event, presentation, or rule adapters.

### Progression, Party, Inventory, Field, Fusion, And Persistence

Framework services now exist for:

- stat resolution, resource recalculation, EXP curves, level growth, and stat allocation;
- party and stock transitions;
- inventory, equipment, wallet, shop, and hospital transactions;
- field and dungeon state machines;
- negotiation and reward services;
- fusion inheritance, fusion planning, fusion transactions, and Compendium snapshots;
- runtime save/checkpoint snapshots and validation.

These are framework foundations and adapter-backed migrations. They are not all clean production authority yet.

### Host Boundaries

The framework exposes engine-neutral contracts for:

- host-supplied content text;
- command input;
- event output;
- random sources;
- runtime IDs;
- save snapshots.

The Godot integration proof is test-only. It proves the contracts can be consumed by a Godot-shaped host without adding a Godot dependency, but there is no real Godot project or production Godot adapter yet.

### Console Host Compatibility

The console host remains the playable prototype and compatibility harness.

It still owns:

- ordinary startup and scenario selection;
- legacy `Database.LoadData`;
- `Data/Jsons` legacy datasets;
- live `Combatant` and `Persona` objects;
- console menus and `IGameIO`;
- legacy battle effects and string-driven skill/item paths where not migrated;
- visible narration, colors, waits, and menu ordering.

The console host also contains clean demos:

- `--clean-battle-demo`;
- `--clean-field-demo`;
- `--clean-save-demo`;
- `--clean-training-annex-demo`.

The Training Annex demo is the first original clean runtime slice. It loads only the `convergence.training_annex_slice` pack and proves catalog loading, ruleset binding, actor hydration, dungeon traversal, item execution, automated battle, rewards, progression, and save validation. The pack now also carries neutral reference records for ailments, equipment, shop, negotiation, additional encounters, and concept-level fusion recipes so future clean demos can grow without using legacy prototype data.

## What We Achieved

The major achievement is architectural: the project now has a real reusable framework boundary.

Concrete wins:

- The old monolithic console shape was split into framework and host ownership.
- Framework APIs are mostly serializer-neutral and host-neutral.
- Clean content can be loaded, validated, qualified, and consumed without the legacy static database.
- The system has a small original clean content slice that runs end to end and a broader neutral sample catalog for future clean demos.
- The legacy prototype is still protected by characterization tests and parity ledgers.
- The archive gate prevents premature cleanup.
- The test suite is broad enough to make future changes less scary.

The framework foundation exists, but it is not yet a finished framework product.

## What Is Still Missing

Detailed problem notes live in [Framework Completion Problems](framework-completion/README.md). The sections below summarize the main gaps.

### Original Production Content

Only a small original clean slice exists. The old prototype `Data/Jsons` content is evidence, not approved shippable content and not a direct conversion queue.

Still needed:

- real original skill set;
- real original entity/race set;
- real item/equipment/shop content;
- real encounter and dungeon content;
- real fusion and Compendium content;
- balance review and naming/lore ownership.

### Clean Consumer Authority

Many systems have framework services, but ordinary interactive play still reaches them through console compatibility adapters or still relies on legacy execution.

Still needed:

- decide which clean runtime loop becomes the primary playable path;
- migrate one consumer at a time from legacy authority to clean catalog authority;
- keep legacy paths until the replacement is proven and the ledger authorizes retirement.

### Authored Ruleset Authority

Ruleset binding exists, but many systems still use named default policies rather than fully authored ruleset parameters.

Still needed:

- choose which policy knobs belong in ruleset content;
- keep behavior stable unless a gameplay change is explicitly approved;
- avoid hiding balance changes inside schema work.

### Optional Mechanics

Some systems still look more mandatory than they should because they came from the legacy prototype's inspirations.

Moon Phase is the current example. It should become optional world/session metadata, not a required framework concept. A host that wants moon phases should be able to register and supply them; a host that does not should not need fake `new_moon` data just to run unrelated systems.

Sacrificial fusion availability should eventually be policy-gated rather than inherently Full Moon-gated. Story progress, key items, difficulty, dungeon state, moon phase, or no gate at all should all be possible host/content choices.

### Interactive Clean Host

The Training Annex slice is noninteractive. It proves the clean runtime can run, not that the final player-facing loop exists.

Still needed:

- a small interactive clean loop;
- typed command menus over clean runtime actors;
- clean inventory, item, battle, dungeon, reward, and save flow in one playable path;
- eventually, Godot-facing host adapters.

### Content Authoring Tooling

The framework has validation, but authoring is still raw JSON.

Still needed:

- authoring guidelines;
- content templates;
- stronger schema/contract docs if machine-readable schemas are desired;
- reports for missing references, duplicate IDs, balance ranges, and unsupported handlers.

### Save/Load Product Flow

Save snapshot contracts exist and the host-owned JSON proof works.

Still needed:

- interactive save/load menus;
- save slot policy;
- save policy layer for manual saves, autosaves, and suspend saves;
- suspend-save consume-after-load behavior;
- migration policy for future save versions;
- host-owned save format decisions.

### Archive Eligibility

No active legacy source is eligible for archive.

The Track T5 review recorded:

- 36 protected capabilities reviewed;
- 0 `clean_parity` capabilities;
- 0 archive candidates;
- 0 removal authorizations.

Legacy code stays active until a specific capability reaches clean parity, has a migrated consumer, has tests, and has explicit removal authorization.

## Documentation Cleanup Decision

The old active track plans are now archived as historical records:

- `ArchiveDocs/Planning/framework-parity-migration-plan.md`;
- `ArchiveDocs/Planning/o-track-plan.md`;
- `ArchiveDocs/Planning/production-baseline.md`;
- `ArchiveDocs/Planning/q-track-plan.md`;
- `ArchiveDocs/Planning/t-track-plan.md`.

They are not deleted. They can explain how the project got here, but they should not be used as the current roadmap.

The active documentation order is now:

1. this document;
2. the Full Parity Capability Plan when choosing implementation work;
3. the Repository Architecture Proposal when changing physical file layout;
4. the Clean Console Host Demo Plan when implementing proof-harness details for the framework-first console demo;
5. the Skill System GDD for approved skill design;
6. Architecture and Gameplay Systems for implementation reference;
7. subsystem docs for local orientation;
8. Project Vision for long-term direction;
9. archived plans only as history.

## Plan Moving Forward

Future work should use numbered, named phases or explicitly approved plan files. Do not casually invent new lettered tracks.

The active implementation spine is now [Full Parity Capability Plan](full-parity-capability-plan.md). The clean console host is the proof harness for that plan, not a separate competing roadmap.

### Phase 1: Rebuild Ownership

Goal: make the project understandable again.

Work:

- keep this document current;
- use plain-language summaries before large implementation passes;
- record why a choice is being made, not only what code changed;
- avoid defaulting design decisions without showing the tradeoff.

Exit condition:

- the owner can explain the framework shape, content boundary, and next goal without reading old track history.

### Phase 2: Expand Original Clean Content Carefully

Goal: grow the Training Annex from a technical demo into a small owned testbed.

Status: initial content-pack expansion completed.

Work:

- added a few original skills, enemies, items, and encounters;
- added neutral ailment, equipment, shop, negotiation, dungeon, ruleset, and concept-level fusion records;
- keep the content small enough to review;
- prove each addition through catalog tests and runtime demo coverage;
- avoid converting legacy content directly.

Exit condition:

- the clean slice has enough content variety to exercise ailments, buffs/debuffs, fusion or recruitment decisions, rewards, and dungeon traversal without relying on prototype data.

Current result:

- `convergence.training_annex_slice` now contains three races, five entities, ten skills, three ailments, five items, four equipment records, one shop, one negotiation set, three encounters, one dungeon, two concept-level fusion recipes, and standard ruleset bindings.
- Catalog and runtime tests prove the expanded pack loads, qualifies IDs, rejects local lookups, binds standard rulesets, exercises additional skills/items, carries fixed battle encounter IDs through the dungeon state machine, and can build battle actor requests from a host-owned encounter trigger.
- `--clean-training-annex-demo` still runs without GUI or legacy fallback. It now uses a host trigger to select `ashling_drill`, but it still only exercises part of the expanded pack at runtime.
- `--clean-training-annex-play` starts the first clean interactive session shell. It lives under `Host/CleanConsole/TrainingAnnex/`, loads only the Training Annex pack, hydrates Echo Adept and the Training Annex enemy roster from the catalog, initializes HP/SP through the framework `standard_growth` resource policy, lets the host inspect session/actor state, previews `standard_stat` stat composition with a runtime `attack +1` stage, recalculates Echo Adept resources through the same policy, validates a startup save snapshot, and exits without legacy `Database` startup.

Design note:

- The current floor-triggered encounter flow is a deterministic console/demo convenience.
- The production-facing dungeon model should support host-scene/entity-triggered encounters. Godot should be able to own placed enemies, patrols, trigger volumes, and scene objects, then ask the framework to resolve a chosen encounter when contact or interaction occurs.
- Fixed encounter floors remain valid for scripted events, bosses, tutorials, and tests, but they are not the required exploration model.

### Phase 3: Build A Small Interactive Clean Loop

Goal: let a player interact with clean framework content without entering the legacy prototype path.

Work:

- follow the numbered [Clean Console Host Demo Plan](clean-console-host-demo-plan.md);
- create a clean-host flow over original clean content;
- use framework commands and snapshots directly;
- keep the console host as the first simple host, not the final Godot UI;
- preserve ordinary legacy startup until the clean loop is mature.

Exit condition:

- a player can start the clean slice, choose actions, use items, resolve battles, gain rewards, move through the tiny dungeon, and save/load through a host-owned flow.

### Phase 4: Promote One Capability At A Time

Goal: replace compatibility adapters with clean consumer authority only where proven.

Work:

- follow the [Full Parity Capability Plan](full-parity-capability-plan.md);
- choose one capability;
- define the intended behavior in active docs;
- add original clean content if needed;
- migrate the consumer;
- compare against legacy behavior where preservation matters;
- update the parity ledger only when evidence exists.

Exit condition:

- the capability has framework rule ownership, migrated consumer, tests, and no hidden dependency on legacy data.

### Phase 5: Archive Only Proven Obsolete Code

Goal: reduce the active codebase without losing history or behavior.

Work:

- review one narrow surface at a time;
- require `clean_parity`, `consumerMigrated: true`, and `removalAuthorized: true`;
- move retired files into `ArchiveDocs/LegacyFramework/<gate>/<original-path>`;
- remove them from active build/runtime references;
- run the full quality gate.

Exit condition:

- archived code is truly unreachable, preserved for history, and no longer competing with the framework.

## Priority Review Backlog

This section is for ranking before implementation. Do not treat `TBD` items as approved next work. The point is to stop introducing new work invisibly and make the owner choose what matters first.

| Feature / Problem | Current Status | Priority |
| --- | --- | --- |
| Host-owned encounter-start proof made more scene-like | First framework bridge exists through `CatalogEncounterStartPlanner`; still scripted in the demo | TBD |
| Interactive clean runtime loop | Noninteractive demos exist; no player-driven clean loop yet | TBD |
| Shop/equipment clean host flow | Content and framework services exist; sample records are not yet demonstrated in a clean host flow | TBD |
| Negotiation clean host flow | Content and framework services exist; sample record is not yet demonstrated in a clean host flow | TBD |
| Fusion clean host flow | Concept recipes and services exist; sample flow is not yet demonstrated independently of legacy Cathedral presentation | TBD |
| Optional mechanic decoupling, especially Moon Phase | Design issue documented; implementation not started | TBD |
| Save policy and suspend saves | Snapshot/validation contracts exist; save kind, suspend rules, and consume-after-load policy are not implemented | TBD |
| Content authoring tooling/templates | Validation exists; authoring remains raw JSON | TBD |
| Real Godot adapter/project | Contract proof exists; no Godot project or production adapter yet | TBD |
| Legacy retirement gate | Guardrails exist; no capability currently authorized for archive/removal | TBD |

## Immediate Next Recommendation

The next implementation pass should not be another broad migration.

Recommended next work:

1. make the host-owned encounter-start proof more scene-like, with explicit host scene/trigger state instead of a scripted demo shortcut;
2. prove shop/equipment/negotiation/fusion sample records through tiny clean host flows when each is ready;
3. keep optional-mechanic cleanup, especially Moon Phase decoupling, in view before deepening sample rulesets;
4. grow the interactive clean loop only after the encounter-start boundary is clear.

That keeps momentum inside the new architecture while restoring ownership of the content and design.

## Ground Rules For Future Work

- No new lettered tracks unless a plan file is explicitly created and approved.
- No legacy source moves to the archive without ledger authorization.
- No direct conversion of prototype `Data/Jsons` into production clean content.
- No gameplay-rule change hidden inside refactoring.
- No design-impact default chosen silently.
- Prefer small original clean slices over broad mechanical migrations.
- Keep the framework reusable, but keep the owner's ability to understand it as a first-class requirement.
