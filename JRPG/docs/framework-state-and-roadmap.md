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

The Training Annex demo is the first original clean runtime slice. It loads only the `convergence.training_annex_slice` pack and proves catalog loading, ruleset binding, actor hydration, dungeon traversal, item execution, automated battle, rewards, progression, and save validation. The interactive Training Annex play shell now also proves manual clean battle actions over a prepared encounter. The pack carries neutral reference records for ailments, equipment, shop, negotiation, additional encounters, and concept-level fusion recipes so future clean demos can grow without using legacy prototype data.

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

Phase 7-35 implements the battle-knowledge import hook as `FamiliarEntityKnowledgeService`. It is opt-in and receives explicit familiar entity IDs, allowing a host to seed persistent player knowledge after recruitment, fusion, recall, registration, or another ownership event. It does not seed ordinary enemy AI, whose knowledge remains fresh per encounter.

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

Phase 7-34 makes sacrificial fusion availability policy-gated rather than inherently Full Moon-gated. Story progress, key items, difficulty, dungeon state, moon phase, or no gate at all can be expressed by a host-supplied `IFusionSacrificePolicy` and optional `FusionPolicyContext` facts.

Phase 1-06 applies the first concrete version of this rule to the Training Annex path. The neutral Training Annex pack no longer declares a moon-phase ruleset, its clean host registrations omit moon phase IDs, its save/session snapshot stores no fake `MoonPhaseId`, and clean automated battle can run with missing moon metadata when content does not use moon-phase conditions. This is decoupling, not a replacement Moon Phase feature.

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
3. `Convergence.Tests/Fixtures/Parity/recovery-baseline.json` when checking executable capability status, ownership, evidence, and the numbered `futurePhase` for each capability;
4. the Repository Architecture Proposal when changing physical file layout;
5. the Clean Console Host Demo Plan when implementing proof-harness details for the framework-first console demo;
6. the Skill System GDD for approved skill design;
7. Architecture and Gameplay Systems for implementation reference;
8. subsystem docs for local orientation;
9. Project Vision for long-term direction;
10. archived plans only as history.

## Plan Moving Forward

Future work should use numbered, named phases or explicitly approved plan files. Do not casually invent new lettered tracks.

The active implementation spine is now [Full Parity Capability Plan](full-parity-capability-plan.md). The clean console host is the proof harness for that plan, not a separate competing roadmap.

The parity ledger uses the same numbered pass IDs through `futurePhase`. Old lettered track labels are historical only and should not be used to choose new work.

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
- `--clean-training-annex-play` starts the first clean interactive session shell. It lives under `Host/CleanConsole/TrainingAnnex/`, loads only the Training Annex pack, hydrates Echo Adept and the Training Annex enemy roster from the catalog, initializes HP/SP through the framework `standard_growth` resource policy, lets the host inspect session/actor state, previews `standard_stat` stat composition with a runtime `attack +1` stage, recalculates Echo Adept resources through the same policy, applies a clean victory EXP/level progression step, validates a startup save snapshot without moon metadata, and exits without legacy `Database` startup.
- Phase 1-07 gives that shell its first generic outer-navigation boundary: arbitrary location IDs, explicit transitions, injected access policy, live navigation inspection, and save validation over optional field state. Phase 1-08 adds a separate optional dungeon-node boundary with explicit policy-checked transitions, visited nodes, checkpoints, barrier rejection, and boss flags. Phase 1-09 adds explicit host-triggered catalog encounter preparation and ordered actor hydration without connecting it to traversal. Phase 1-10 adds clean field item/skill selection, typed execution, target cancellation, resource synchronization, and reservation-backed host inventory consumption. Phase 2-11 adds the first manual clean battle action shell: Practice Blade attack, battle skills, Annex Tonic, guard, pass, and analyze are selected by the host and executed by framework battle/action services. Phase 2-12 hardens that shell with typed-effect evidence and display-text mutation tests so behavior stays definition-driven. Console menus are only one host adapter; Godot triggers, VN hotspots, doors, enemy bodies, patrols, battle menus, or scripts can request the same framework operations.

Phase 2-13 advances the interactive shell from temporary demo combat policies to catalog-bound framework combat math. Its summary now exposes resolved hit, critical, affinity, damage/recovery, effect, and Press Turn outcomes, plus a reward preview from the bound reward service.

Phase 2-14 binds the interactive shell to catalog-authored `standard_press_turn` before startup, injects the bound factory into `BattleEncounterRunner`, and records host-visible Press Turn evidence for committed actions. The clean console host now presents current and updated icon counts while leaving the icon economy inside framework `PressTurnEngine`.

Phase 2-14 verification: focused Training Annex tests passed `29/29`, the full suite passed `786/786`, the framework build stayed at `0` warnings, the solution build stayed at `98` existing legacy warnings, and clean battle/field/save/Training Annex demos all completed successfully.

Phase 2-15 implementation note: the Training Annex manual battle now routes through a host lifecycle port backed by `BattleStatusLifecycleService`, records lifecycle evidence for ailment application, cures, poison ticks, turn restrictions, recovery, removal, and expiry, and recognizes Toxin Touch / Clear Toxin when the actor knows those skills. Passive dispatch is intentionally suppressed until Phase 2-16.

Phase 2-15 verification: focused Training Annex tests passed `32/32`, lifecycle-focused tests passed `39/39`, the full suite passed `789/789`, the framework build stayed at `0` warnings, the solution build stayed at `98` existing legacy warnings, and clean battle/field/save/Training Annex demos all completed successfully.

Phase 2-16 implementation note: the clean Training Annex lifecycle port now uses the real passive dispatcher for battle-start and owner-turn-end events. The authored `Steady Breath` passive restores HP after committed actions, passive activations are exposed through typed lifecycle/encounter events, and a test-only renamed passive proves Physical-only rule modifiers are definition-driven rather than text-driven. Cancellation remains mutation-free. This capability stays `parallel_partial` until broader passive content and the remaining clean battle capabilities are completed.

Phase 2-16 verification: focused Training Annex/passive/lifecycle tests passed `53/53`, the full suite passed `791/791`, the framework build stayed at `0` warnings, the solution build stayed at `98` existing legacy warnings, and clean battle/field/save/Training Annex demos all completed successfully.

Phase 2-17 implementation note: the Training Annex enemy path now delegates typed skill selection to framework `DeterministicBattleActionSelector`. Shared assessment filters illegal candidates, equal scores preserve authored order, Pass is returned when nothing can execute, lifecycle Skip bypasses strategy selection, and immutable AI decision evidence is retained by the clean session. Knowledge remains empty/session-local until Phase 2-18, and configurable tactics/direct-control switching remains unfinished.

Phase 2-17 verification: focused Training Annex/framework-selector tests passed `38/38`, the full suite passed `794/794`, the framework build stayed at `0` warnings, the solution build stayed at `98` existing legacy warnings, and clean battle/field/save/Training Annex demos all completed successfully.

Phase 2-18 implementation note: the Training Annex play session now owns framework elemental, ailment, and instant-death knowledge stores with explicit scopes. Player battle knowledge is persistent and save-facing; player damage and Analyze discoveries update that store for later UI hints. Enemy/AI knowledge is created fresh for each manual battle, feeds framework `DeterministicBattleActionSelector` only inside that battle, and is discarded after ordinary encounters. This remains `parallel_partial` because legacy battle consumers still keep their protected knowledge path.

Future design hook: persistent player knowledge may also be seeded from clean ownership/Compendium state. If the player has owned, recruited, fused, recalled, or registered a familiar entity, the framework should be able to import that entity's known defenses into the player's knowledge base before battle presentation. This remains separate from encounter AI knowledge.

Phase 2-19 implementation note: the Training Annex play session now commits the bound battle reward after player victory. EXP is applied through `standard_growth`, Macca is applied through framework economy transactions, and the save-facing session snapshot records victory/EXP/Macca counters plus an Ashling drill completion flag. Cancellation and non-victory outcomes leave rewards untouched. This remains `parallel_partial` because ordinary legacy battle rewards and broader production reward content are still protected.

Phase 3-20 implementation note: the Training Annex play session now has a clean `Save / Load` menu over framework save policy contracts. `RuntimeSavePolicyService` allows manual and suspend saves only in registered field/dungeon menu contexts, rejects pending host actions such as an unresolved prepared encounter, and marks suspend loads for consumption after successful restore. The console host owns JSON serialization and one in-memory manual/suspend slot pair. CodeReview-1 removes the former HP/SP synchronization step: load now restores complete catalog-backed canonical actors, plus inventory, wallet, field/dungeon state, session counters/flags, host-owned prepared-battle state, and persistent player battle knowledge. CodeReview-3 advances the prerelease save contract to version `5`: saves record exact content-pack IDs/versions, load policy validates the saved creation context as well as the current load context, Training Annex restore checks expected actor/entity/team mappings and host-owned dungeon state, and battle reward application avoids EXP/wallet half-commits. Malformed, invalid, or host-incompatible records leave the current session untouched. This is still `clean_foundation`: permanent slots, autosaves, battle saves, production save migrations, Godot save resources, and legacy prototype save/load are not implemented.

Phase 2-18 verification: focused Training Annex tests passed `39/39`, the full suite passed `796/796`, the framework build stayed at `0` warnings, the solution build stayed at `98` existing legacy warnings, and clean battle/field/save/Training Annex demos all completed successfully.

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
- confirm the capability's `futurePhase` in `recovery-baseline.json`;
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
| Negotiation clean host flow | Training Annex proof exists for prompt outcome, recruitment validation, Macca spend, and Demon-stock addition; authored demand policy binding remains unfinished | TBD |
| Fusion clean host flow | Phases 7-30 through 7-35 prove result, planning, preview, sample transaction, explicit strategies, and Compendium paths; CodeReview-7-1 fixed recipe fidelity, while runtime identity, transaction ownership, policy context, and save validation remain | CodeReview-7-2 through CodeReview-7-5 |
| Optional mechanic decoupling, especially Moon Phase | Training Annex no longer requires fake moon metadata; broader optional-mechanic policy remains unfinished | TBD |
| Save policy and suspend saves | Snapshot/validation contracts exist; save kind, suspend rules, and consume-after-load policy are not implemented | TBD |
| Content authoring tooling/templates | Validation exists; authoring remains raw JSON | TBD |
| Real Godot adapter/project | Contract proof exists; no Godot project or production adapter yet | TBD |
| Legacy retirement gate | Guardrails exist; no capability currently authorized for archive/removal | TBD |

## Immediate Next Recommendation

CodeReview-1, CodeReview-2, CodeReview-3, and CodeReview-4 are complete and ready. CodeReview-3 implements the already-reviewed Phase 3 restore-hardening checkpoint: expected actor/entity mapping and saved creation context are validated, host-owned dungeon-state validation is explicit for Training Annex, content provenance is recorded, and restore planning completes before mutating the live session. CodeReview-4 splits the Training Annex host's persistence, field-presentation, and reward-application seams into dedicated collaborators. Phase 4-21 is implemented: Training Annex field inventory selection is driven by the clean inventory snapshot and selected catalog content IDs, and selected-item consumption commits through framework inventory reservations only after meaningful execution. Phase 4-22 is implemented: clean equipment ownership/equip state is seeded through framework transactions, equipped weapon basic attacks resolve through `RuntimeEquipmentProfileResolver`, and framework tests prove accessory stat modifiers feed stat resolution for eligible actor kinds. Phase 4-23 is implemented: the Training Annex requires `standard_economy`, uses its bound resource-management services, applies reward income to an authoritative host-owned wallet, records typed wallet transaction evidence, and rejects invalid or overflowing mutations without changing state. Phase 4-24 is implemented: Training Annex exposes a clean `Training Supply` shop over authored catalog offers, resolves those offers into runtime transaction snapshots, buys/sells through the bound shop/economy services, and can equip purchased equipment through the bound equipment transition service. Phase 4-25 is implemented: Training Annex exposes a clean `Recovery Facility`, assesses and executes treatment through the bound hospital service, spends Macca, restores HP/SP, and keeps failure states non-mutating. Phase 5-26 is implemented: Training Annex now owns active/reserve party state through `RuntimePartyStockSnapshot`, adds Annex Mentor as a reserve support actor via `PartyStockTransitionService`, exposes `Inspect Party`, and saves/restores the live party snapshot. Phase 5-27 is implemented: Training Annex now hydrates an active-form actor plus Persona-stock and Demon-stock actors, stores them in the same `RuntimePartyStockSnapshot`, exposes `Inspect Stock`, and validates those stock references during save restore. Phase 5-28 is implemented: Training Annex exposes `Party / Stock Operations` and executes active-form swap, Demon-stock summon, active demon swap, return, replace, dismiss, and consume through framework party/stock transitions with non-mutating rejection diagnostics.

Phase 5-28 verification passed `76/76` focused Training Annex host tests and `847/847` full-suite tests with no skips. Phase 6-29 is implemented: the Training Annex clean host now demonstrates negotiation/recruitment through framework session, recruitment, party-stock, and economy services, including success, refusal, and repeated familiar paths. Authored demand-policy binding remains future work. Phase 6-29 verification passed `104/104` focused Training Annex, party-stock, parity-ledger, and original-content tests and `851/851` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all four noninteractive clean demos pass, `git diff --check` passes, and the framework forbidden-reference search returns no matches. `Data/Jsons` changed only for the clean Training Annex negotiation sample.

Phase 7-30 is implemented: clean fusion result calculation now uses `CatalogFusionContentRepository` over the original Training Annex pack and records non-mutating result evidence in `--clean-training-annex-play`. Phase 7-31 is implemented: the same command now records clean planning evidence for inheritance slots, passive/explicit-allowance filtering, already-known and blocked reason codes, and a deterministic mutation/accident sample from `Echo Strike` to `Shell Bash`. Phase 7-32 is implemented: `Preview Fusion Result` now validates inherited-skill selection through the framework inheritance validator, creates a Ward Shell preview through `FusionPreviewService`, and confirms that no runtime state mutates. Phase 7-33 is implemented: `Commit Fusion Transaction` now rejects duplicate results before mutation and can atomically consume parent demons/add a fused Ward Shell through framework party-stock transitions. Phase 7-34 is implemented: fusion resolution/planning require explicit strategy policies, clean requests no longer carry mandatory Moon Phase data, typed catalyst boosts no longer inspect names, and legacy assumptions live only in the console compatibility policy. Phase 7-35 is implemented: clean Compendium registration, recall, stock/wallet transaction ownership, persistence, and opt-in familiar-player-knowledge import now run through framework contracts. CodeReview-7-1 is complete: runtime recipes preserve entity/race selector kinds, mixed selectors resolve in either parent order, schema v1 rejects non-binary cardinality, and clean structured results no longer carry fabricated legacy tokens. The next work item is CodeReview-7-2; Phase 8-36 waits until CodeReview-7-2 through CodeReview-7-5 are complete.

Phase 7-35 verification passed `148/148` focused capability, boundary, and protected-legacy tests and `893/893` full-suite tests with no failures or skips. The framework build remained at `0` warnings, the solution retained `98` pre-existing legacy-host warnings, all clean demos passed, boundary and diff checks passed, and `Data/Jsons` remained unchanged.

CodeReview-7-1 verification passed `33/33` focused typed-recipe/catalog tests, `113/113` broad fusion/Compendium tests, and `899/899` full-suite tests. The framework build remained at `0` warnings, the solution retained `98` protected legacy-host warnings, all four clean demos passed, boundary and diff checks passed, and `Data/Jsons` remained unchanged.

The source-derived [Phase 7 Code Review And Readiness](phase-7-code-review.md) concludes that Phase 7 is implemented but not closed. CodeReview-7-1 has preserved typed recipe selectors/cardinality and made structured results authoritative. CodeReview-7-2 through CodeReview-7-5 must still enforce global recall identity, move fusion commit coordination into the framework, retain policy context during accident mutation, and harden Compendium save validation before Phase 8 begins.

## Ground Rules For Future Work

- No new lettered tracks unless a plan file is explicitly created and approved.
- No legacy source moves to the archive without ledger authorization.
- No direct conversion of prototype `Data/Jsons` into production clean content.
- No gameplay-rule change hidden inside refactoring.
- No design-impact default chosen silently.
- Prefer small original clean slices over broad mechanical migrations.
- Keep the framework reusable, but keep the owner's ability to understand it as a first-class requirement.
