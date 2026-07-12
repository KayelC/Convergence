# Clean Console Host Demo Plan

> **Status: Active proof-harness plan.** The main implementation spine is [Full Parity Capability Plan](full-parity-capability-plan.md). This document defines how the clean console host proves those capabilities through a framework-first demo. It does not approve legacy removal, namespace cleanup, broad repo declutter, production data migration, or Godot project work.

## Purpose

The current console executable contains two different worlds:

- the old interactive prototype, powered by legacy `Database`, `Combatant`, `Persona`, `SkillData`, `ItemData`, and console-specific workflows;
- several clean noninteractive demos, powered by framework catalog content, runtime snapshots, clean actions, and host-owned presentation.

The next clean console demo should grow out of the second path.

The goal is to build a small but real interactive demo where the console is only the host/presentation layer and the framework owns the gameplay rules and runtime state.

This is not a separate roadmap from capability parity. Each useful clean console iteration should prove one or more protected capabilities from the full parity plan.

In plain terms:

```text
player input -> console host command/result UI -> framework runtime/action services -> framework result -> console host presentation
```

Not:

```text
player input -> legacy Combatant/Database/effect strings -> compatibility adapter maze
```

## Design Principles

- Framework first.
- Original clean content only.
- Console owns input, output, colors, waits, and text formatting.
- Framework owns content validation, catalog lookup, runtime state, actions, battle, field/dungeon transitions, rewards, progression, inventory/resource transactions, and save snapshots.
- The demo must not require a GUI.
- The demo must remain small enough to understand.
- Add one capability at a time.
- Do not port the entire old console game in one pass.
- Do not use prototype `Data/Jsons` as production game data.
- Do not archive legacy code until a separate archive gate proves clean parity.

## Current Starting Point

The current clean demo foundation already has:

- `convergence.training_annex_slice`, an original clean content pack;
- `--clean-training-annex-demo`, a noninteractive runtime proof;
- catalog loading and validation;
- ruleset binding;
- actor hydration;
- dungeon transition proof;
- host-owned encounter trigger proof;
- clean item execution;
- automated battle;
- rewards and EXP application;
- save snapshot validation.

The missing piece is interactivity. The player cannot yet drive the clean runtime through menus.

## Success Definition

The clean console demo becomes meaningful when a player can:

1. start the clean demo without loading legacy `Database`;
2. see a clear main menu;
3. inspect actor/session state;
4. move through the Training Annex;
5. trigger an encounter through host-owned interaction;
6. choose clean battle actions;
7. use a clean item;
8. win or lose a small battle;
9. receive rewards and progression;
10. save, load, or suspend through host-owned persistence policy;
11. exit without touching the legacy prototype path.

The demo does not need every final game feature before it is useful. It needs a complete, understandable loop.

## Non-Goals

These are explicitly out of scope for the early iterations:

- no legacy prototype rewrite;
- no broad namespace migration;
- no repo-wide physical file move;
- no Godot project;
- no production save-slot UI beyond what an iteration approves;
- no direct conversion of old prototype JSON;
- no full fusion design until the owner approves what fusion should be in this game/framework;
- no broad mechanic toggle framework;
- no legacy source archive.

## Iteration Overview

| Iteration | Name | Goal | Priority |
| --- | --- | --- | --- |
| 0 | Plan And Guardrails | Approve this plan and keep it linked from active docs. | `P0` |
| 1 | Clean Demo Entry And Session Shell | Add an interactive clean-demo entry point and session object without gameplay complexity. | `P1` |
| 2 | Optional Mechanic Baseline | Stop the clean demo from requiring unused Moon Phase/session mechanics. | `P1` |
| 3 | Field And Dungeon Interaction Loop | Let the player move through the Training Annex and trigger encounters through host-owned choices. | `P1` |
| 4 | Clean Item And Field Action Loop | Let the player use clean items/field actions through framework execution and host-owned inventory. | `P1` |
| 5 | Manual Clean Battle Loop | Replace automated battle in the clean demo with player-selected clean battle commands. | `P1` |
| 6 | Rewards, Progression, And Session State | Apply rewards, EXP, resource updates, and session progress after clean encounters. | `P1` |
| 7 | Save Policy And Suspend Save | Add framework save policy and a small host-owned save/load/suspend flow for the clean demo. | `P1/P2` |
| 8 | Shop And Equipment Demo Flow | Demonstrate clean shop/equipment transactions inside the clean runtime. | `P2` |
| 9 | Negotiation Or Recruitment Demo Flow | Demonstrate clean negotiation/recruitment if the design remains wanted. | `P2` |
| 10 | Fusion And Compendium Demo Flow | Demonstrate clean fusion only after the game's fusion direction is approved. | `Blocked/P2` |
| 11 | Content Authoring Comfort Pass | Add templates/checklists/reports that make clean content easier to write. | `P2` |
| 12 | Clean Demo Maturity Review | Decide whether the old console prototype has any narrow archive candidates. | `P3` |

## Iteration 0: Plan And Guardrails

### Goal

Create and approve this plan before implementing the interactive demo.

### Work

- Keep this document as the clean demo development map.
- Link it from active documentation.
- Keep future work as numbered iterations unless the owner explicitly approves a different plan.
- Mark unclear design points instead of silently choosing defaults.

### Exit Condition

The owner can read this file and understand how the new console demo will grow.

### Verification

- `git diff --check`.

## Iteration 1: Clean Demo Entry And Session Shell

### Goal

Create the new interactive clean demo entry point and session shell without implementing the full gameplay loop yet.

### Implemented Command

Flag:

```text
--clean-training-annex-play
```

This command routes before legacy `ConsoleGameHost` construction and does not call `Database.LoadData`.

### Implemented Work

- Added a clean interactive host class separate from `CleanTrainingAnnexDemoHost`.
- Kept the new host in `Host/CleanConsole/TrainingAnnex/` so it is visibly separate from the legacy console prototype.
- Extracted Training Annex content request, registration, runtime-policy helpers, dungeon conversion, and startup snapshot support into shared clean-console support.
- Load only `convergence.training_annex_slice`.
- Do not call `Database.LoadData`.
- Hydrate Echo Adept from `GameDataCatalog` through `CatalogBattleActorFactory`.
- Add a simple main menu:
  - inspect session;
  - inspect actor;
  - validate startup snapshot;
  - exit.
- Use existing console host command/input abstractions where appropriate.

### Framework Use

- `IContentPackTextSource`;
- `SkillSystemCatalogLoader`;
- `CatalogBattleActorFactory`;
- runtime snapshots;
- `RuntimeSaveValidator`.

### Non-Goals

- no battle commands yet;
- no shop/equipment yet;
- no negotiation/fusion;
- no persistent save file yet.

### Tests

- `CleanTrainingAnnexPlayHostTests.CleanTrainingAnnexPlay_LoadsCleanContentHydratesActorValidatesSnapshotAndExits`;
- `CleanTrainingAnnexPlayHostTests.CleanTrainingAnnexPlay_MissingContentReportsFailureWithoutReadingCommands`;
- existing `CleanTrainingAnnexDemoHostTests` remain green after the shared-support extraction.

### Phase 1-02 Actor Roster Extension

Implemented after the initial shell:

- `TrainingAnnexHostSupport` now builds a clean actor roster from the catalog.
- The roster contains Echo Adept plus the unique enemy models required by the Training Annex encounters: Ashling, Bramble Runner, and Ward Shell.
- Enemy actor creation requests are derived from clean encounter definitions, not from legacy data or hand-authored console objects.
- `Inspect Actors` shows the clean player/enemy roster, instance IDs, levels, resources, stats, active skills, and passives.
- Startup snapshot validation includes the actor roster.

This proves the clean actor-model path for the original slice, but it does not yet replace every protected legacy actor category.

### Phase 1-03 Resource Recalculation Extension

Implemented after the actor roster extension:

- `TrainingAnnexHostSupport` now binds the clean `standard_growth` ruleset when creating the Training Annex runtime actor roster.
- Training Annex actor HP/SP initialization uses the framework `StandardResourceGrowthPolicy` through a clean resource initialization policy.
- The clean play host stores framework runtime actor snapshots for the roster instead of treating catalog battle actors as the whole session state.
- The interactive menu now includes `Recalculate Resources`, which applies a small HP transaction to Echo Adept and reruns the framework resource-growth policy with preserve-current semantics.
- Startup snapshot validation uses the current clean runtime actor snapshots, so resource changes are covered by save validation.

This proves clean resource initialization and a first resource update in the interactive Training Annex session. It does not retire legacy `GrowthProcessor` or legacy `Combatant` resource behavior.

### Phase 1-04 Stat Composition Extension

Implemented after resource initialization:

- The clean play host now binds the catalog `standard_stat` ruleset.
- The interactive menu includes `Resolve Stats`.
- `Resolve Stats` previews Echo Adept stat composition through framework `StandardStatResolutionPolicy`, using a runtime `attack +1` stat-stage sample.
- The preview shows the approved modifier-track aliases: `attack` affects Strength and Magic, not Vitality, Agility, or Luck.
- Actor inspection now prints base stats and effective runtime stats separately.

This proves that the clean shell can display and evaluate framework-owned stat composition without legacy `StatProcessor`. It does not yet prove clean equipment stat impact, because the current Training Annex actors are authored as `demon` and the standard policy intentionally resolves demon stats from active-form stats. Equipment impact remains planned for the equipment pass.

### Phase 1-05 Growth And Level Extension

Implemented after stat composition:

- The framework now includes `RuntimeProgressionTransactionService` for applying a `LevelGrowthResult` to mutable runtime actor state.
- The clean play host now includes `Apply Victory EXP`.
- `Apply Victory EXP` uses the catalog-bound `standard_growth` services to calculate the current level requirement, apply EXP, store the result back into Echo Adept's runtime snapshot, and keep startup save validation aligned with the changed state.
- The current Training Annex proof advances Echo Adept from level 3 to level 4, changes lifetime EXP from 0 to 40, and increases unspent stat points from 2 to 3.

This proves framework-owned growth progression inside the clean interactive shell. It does not retire legacy `GrowthProcessor`, and it does not demonstrate humanoid HP/SP random growth because Echo Adept is currently authored as `demon`.

## Iteration 2: Optional Mechanic Baseline

### Goal

Prevent the clean demo from making optional mechanics look mandatory.

Moon Phase is the immediate example. The clean Training Annex demo should not need fake moon data unless it is demonstrating a moon/cycle mechanic.

### Work

- Audit clean demo startup and ruleset binding for required moon-phase assumptions.
- Make missing moon/session metadata valid when the active rules do not use it.
- Keep moon/cycle conditions available for games that opt in.
- Keep legacy console Moon Phase behavior unchanged.
- Document how optional host metadata should work.

### Non-Goals

- no broad feature-toggle system;
- no legacy Moon Phase deletion;
- no fusion redesign.

### Tests

- clean demo runs with no moon phase supplied when no moon condition is used;
- content using moon-phase conditions still requires explicit registration/metadata;
- legacy Full Moon behavior remains characterized.

### Phase 1-06 Optional Moon Phase Decoupling

Implemented after the growth extension:

- The Training Annex ruleset document no longer includes `standard_moon_phase`.
- `TrainingAnnexHostSupport` no longer registers `new_moon` or the moon-phase policy for the neutral sample pack.
- Training Annex save/session snapshots now omit `MoonPhaseId` instead of writing fake `new_moon` metadata.
- Clean automated battle and encounter requests accept a missing moon phase when the active content does not use moon-phase conditions.
- Existing moon-phase condition support remains available for opt-in content, and the legacy console `MoonPhaseSystem` remains characterized.

This proves the clean Training Annex loop does not need a fake moon/cycle mechanic. It does not implement a replacement clean Moon Phase system, and it does not change legacy Full Moon negotiation or fusion behavior.

### Phase 1-07 Field Navigation Extension

Implemented as the first part of Iteration 3:

- Replaced the reviewed `City`/`Dungeon` draft with `RuntimeNavigationService`, arbitrary `ContentId` locations, explicit transitions, and an injected policy.
- The clean Training Annex play host starts at a host-owned staging-area ID and presents `Enter Training Annex` / `Return to Staging Area` as console-only controls over generic transition requests.
- The current navigation snapshot is shown by session inspection, carried into save validation, and recorded in the scripted host result.
- Source mismatches and policy rejections leave the before-state unchanged; each direction is an explicit transition.
- Save contract v2 makes field state and dungeon traversal independently optional.

This pass does not prescribe menus, scene movement, spatial maps, floor traversal, terminals, barriers, encounter triggers, or battles. Those remain host presentation or later optional capability passes.

### Phase 1-08 Dungeon Traversal Extension

Implemented as the second part of Iteration 3:

- Added `RuntimeDungeonTraversalService` with arbitrary dungeon/node IDs, immutable visited-node state, explicit transitions, and an injected access policy.
- Added optional checkpoint and defeated-boss state without assuming floors, stairs, terminals, scenes, or a particular dungeon layout.
- The clean Training Annex play host demonstrates entrance, review hall, review alcove, an unlockable checkpoint, an explicitly rejected barrier transition, and the return path.
- Console rows are presentation only. A Godot host may issue the same requests from doorways, collision areas, scene scripts, or world interactions.
- Traversal results do not select or start encounters. Encounter entities and host-owned encounter triggers remain Phase 1-09.
- The existing floor-oriented `RuntimeFieldDungeonService` remains available as an optional compatibility/sample service, not the required model for framework users.

### Phase 1-09 Host-Owned Encounter Trigger Extension

Implemented as the third part of Iteration 3:

- Added `CatalogEncounterPreparationService`, which combines catalog encounter planning with ordered runtime actor hydration.
- Added a generic `RuntimeEncounterTriggerRequest` carrying only logical IDs and an optional explicit formation index.
- Added a Review Hall Ashling trigger to the clean Training Annex host. Moving into the hall does not activate it; the player must select the trigger explicitly.
- Successful preparation yields an Ashling actor with a trigger-specific runtime ID and disables that sample trigger. The host still owns whether triggers are one-shot, repeatable, respawned, scripted, or randomly selected.
- The console prints the prepared formation but does not begin an interactive battle yet. Battle command/execution integration remains the later battle-loop capability.
- Godot may invoke the same service from an enemy body, patrol, `Area3D`, interaction prompt, or script without passing a Node or scene path into the framework.

## Iteration 3: Field And Dungeon Interaction Loop

### Goal

Let the player move through a tiny Training Annex flow without relying on automatic floor-forces-battle behavior.

### Work

- Add an interactive field menu for the clean demo.
- Show the current outer location and optional dungeon-node/session state.
- Let the player enter the Training Annex.
- Let the player move between known demo locations/nodes.
- Represent host-owned interaction points:
  - inspect room;
  - interact with encounter trigger;
  - return to entrance;
  - exit.
- Use generic navigation and dungeon traversal services for legal transitions.
- Use the encounter-start planner when the host chooses a specific encounter trigger.
- Hydrate the selected formation through `CatalogEncounterPreparationService` only after that explicit host request.

### Framework Use

- `RuntimeNavigationService`;
- `RuntimeDungeonTraversalService`;
- `RuntimeDungeonTraversalSnapshot`;
- optional `RuntimeFieldDungeonService` for authored floor samples;
- `CatalogEncounterStartPlanner`;
- `CatalogEncounterPreparationService`;
- host-owned scene/trigger IDs.

### Non-Goals

- no spatial Godot map;
- no random patrol system;
- no full dungeon editor;
- no legacy `DungeonManager`.

### Tests

- scripted player choices move through the clean dungeon loop;
- encounter trigger creates the expected actor creation requests;
- optional fixed battle floors preserve authored encounter IDs when the floor module is used;
- host-owned trigger selection does not force every floor ascent into battle.
- explicit trigger preparation creates ordered catalog actors with trigger-specific runtime IDs.

## Iteration 4: Clean Item And Field Action Loop

Status: Phase 1-10 implemented.

### Goal

Let the player use clean items or field actions through the framework.

### Work

- Add a clean inventory view for the demo session.
- Add item selection and target selection.
- Execute `annex_tonic` and any approved field recovery action through clean executors.
- Commit host-owned inventory consumption only after framework execution succeeds.
- Show simple success/failure messages.

### Framework Use

- `ItemExecutor`;
- `BattleActionExecutor` where appropriate;
- resource-management services;
- runtime actor state.

### Phase 1-10 Result

- The interactive Training Annex host now has clean Inventory and Field Skills surfaces with explicit item/skill and target selection.
- Annex Tonic and Mend execute through the shared `BattleActionExecutor` with `EffectExecutionEnvironment("field")`.
- The host owns a `RuntimeInventorySnapshot`; its `IItemActionInventory` adapter uses framework reservation/commit/rollback transitions.
- Target cancellation occurs before action assessment or reservation.
- Full-HP tonic use is rejected without consumption, successful tonic use consumes one, and Mend commits its SP cost.
- Runtime resource state is synchronized around execution so progression/save snapshots remain the persistent clean state.

### Phase 4-21 Result

- The clean field inventory is no longer Annex-Tonic-specific. It enumerates usable field items from the current `RuntimeInventorySnapshot`, filters out zero-quantity and non-usable key records, and attaches each row's catalog `ContentId` as the selection identity.
- Selecting Focus Tea, Annex Tonic, or any future field-usable clean item now resolves the chosen `ItemDefinition` from the catalog and executes that item through the same shared field action path.
- Successful meaningful item execution consumes only the selected item. No-effect use, rejected use, failed execution, and target cancellation leave the framework inventory snapshot unchanged.
- Field skills still do not mutate inventory and no longer print a misleading hardcoded item quantity.

### Non-Goals

- no shop yet;
- no equipment menu yet;
- no legacy `ItemData`;
- no old field item parser.

### Tests

- item use restores HP;
- no-effect use does not consume inventory;
- successful use consumes exactly one;
- target cancellation does not mutate state.

## Iteration 5: Manual Clean Battle Loop

Status: Phases 2-11 through 2-19 are implemented; CodeReview-1 through CodeReview-4 stabilization are complete and ready.

### Goal

Replace the Training Annex automated battle with a player-driven clean battle loop.

### Work

- Add battle command menu:
  - attack or skill;
  - item;
  - guard;
  - pass;
  - inspect;
  - flee/exit if approved.
- Use clean actor loadouts and clean action assessment.
- Use framework battle encounter orchestration.
- Keep enemy decisions deterministic at first.
- Present only minimal text needed to understand the battle.

### Framework Use

- `BattleEncounterRunner`;
- `BattleActionExecutor`;
- `SkillExecutor`;
- `ItemExecutor`;
- `PressTurnEngine`;
- no-op battle lifecycle port for this first slice.

### Phase 2-11 Result

- `--clean-training-annex-play` now offers `Start Prepared Battle` after the Review Hall Ashling encounter trigger prepares actors.
- Battle command menus are host presentation only. They select clean framework commands for Practice Blade attack, battle skills, Annex Tonic, guard, pass, and analyze.
- The battle itself runs through `BattleEncounterRunner`; selected actions execute through `BattleActionExecutor`, `SkillExecutor`, and `ItemExecutor`.
- Enemy behavior is deliberately deterministic for this slice: first executable authored battle skill, otherwise pass.
- Annex Tonic uses the same reservation-backed host inventory path as field items, and Back/cancel paths perform no mutation.
- The host synchronizes clean runtime actor resources before and after battle so the session summary reflects battle damage, skill costs, item healing, and inventory consumption.
- This is not the final battle loop: lifecycle/passives, battle knowledge, AI/tactics, escape, swaps, and reward application still remain later iterations at this point in the history.

### Phase 2-12 Result

- The manual battle summary now exposes typed-effect evidence for executed battle commands, recording source action ID, effect index, effect kind, and typed operands.
- Tests prove Practice Blade, Frost Tip, Echo Strike, Ash Spark, Annex Tonic, and Analyze are driven by typed framework definitions. Guard and Pass intentionally produce no effect evidence.
- A test-only content source renames display text and descriptions while preserving battle behavior and typed-effect evidence, protecting against name/description-driven action logic.
- No Training Annex content JSON changed.

### Phase 2-13 Result

- The manual and noninteractive Training Annex battle paths now bind the catalog `standard_damage` record to `ProductionCombatRuleset`; they no longer use the temporary demo damage, instant-death, ailment, chance, or power policies.
- The same combat ruleset instance resolves damage, accuracy, criticals, affinities, ailments, instant death, chance checks, power amounts, and the `standard_reward` preview.
- Combat-resolution evidence exposes authored power/accuracy/critical mode and resolved hit, critical, affinity, value, effect, and Press Turn outcomes for host presentation and tests.
- Victory calculates a reward preview. Phase 2-19 later turned that preview into committed runtime progression, wallet, and session state.
- Invalid combat or reward binding stops the session without legacy or demo fallback. No Training Annex JSON changed.

### Phase 2-14 Result

- The manual Training Annex battle now binds `standard_press_turn` from the catalog before startup and passes the bound factory into `BattleEncounterRunner`.
- Missing, wrong-category, or unsupported Press Turn rulesets stop the session with `[press_turn:...]` diagnostics instead of silently constructing a default engine.
- The battle summary records Press Turn evidence for committed actions: actor, action, before icons, consumption kind, resolved outcome, and after icons.
- The clean console host prints current icons before player command selection and updated icons after committed turns. This is presentation only; framework `PressTurnEngine` and `BattleEncounterRunner` still own the rules.
- No Training Annex JSON changed.
- Verification: focused Training Annex tests passed `29/29`; full suite passed `786/786`; framework build stayed at `0` warnings and solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all exited successfully.

### CodeReview-2 Stabilization Result

- Battle target, skill, and item rows now carry typed selection identities instead of collapsing dynamic rows into fixed enum values.
- The second enemy in a multi-member encounter can be selected correctly.
- Skills and owned battle items are discovered from runtime/catalog state, so newly exposed valid content requires no host enum case.
- Press Turn updates carry typed icon counts in `BattleEncounterEvent`; console wording is presentation only.
- This stabilization changes no Training Annex JSON and adds no new gameplay rule.

### Phase 2-15 Implementation Note

- The manual Training Annex battle now uses a host lifecycle port backed by framework `BattleStatusLifecycleService` instead of the previous no-op lifecycle port.
- The battle summary records lifecycle evidence for action-applied ailments, action cures, poison turn-end resource ticks, stun/skip restrictions, natural recovery, removal, and expiry.
- Toxin Touch and Clear Toxin are available in the clean battle skill shell when the actor knows them. The checked-in Training Annex JSON is unchanged; focused tests use in-memory content variants to prove the lifecycle paths.
- Passive trigger dispatch is deliberately suppressed in this pass so passive lifecycle remains Iteration/Phase 2-16 work.
- Verification: focused Training Annex tests passed `32/32`; lifecycle-focused tests passed `39/39`; full suite passed `789/789`; framework build stayed at `0` warnings and solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all exited successfully.

### Phase 2-16 Result

- The Training Annex battle lifecycle port now uses the framework `PassiveTriggerDispatcher`; the temporary no-op passive boundary from 2-15 is gone.
- `Steady Breath` executes its authored `owner_turn_end` trigger through the shared typed effect pipeline and restores HP after committed actions.
- The lifecycle port also dispatches authored `battle_start` passive events, while `BattleEncounterRunner` resets per-battle activation counts before dispatch.
- Passive activations are recorded in lifecycle evidence and published as `PassiveActivated` encounter events. Canceled command and target selections still perform no mutation, turn consumption, or passive dispatch.
- Test-only content proves typed Physical damage modifiers still apply when passive display text is unrelated, guarding against text-driven behavior.
- No Training Annex JSON or framework public API changed.
- Verification: focused Training Annex/passive/lifecycle tests passed `53/53`; the full suite passed `791/791`; the framework build stayed at `0` warnings and the solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all exited successfully.

### Phase 2-17 Result

- Enemy turns in `--clean-training-annex-play` now use framework `IBattleActionSelector` and `DeterministicBattleActionSelector`; the previous host-owned first-executable-skill loop has been removed.
- Candidate legality comes from shared `SkillExecutor.Assess`, targeting is typed, equal scores retain authored loadout order, and no legal skill produces a typed Pass action.
- The session summary exposes immutable AI decision evidence for tests and future presentation adapters.
- Turn-start lifecycle restrictions still precede strategy selection, so Skip prevents both decision and execution.
- The host currently gives the selector an empty per-battle affinity-knowledge store. Phase 2-18 will own learning and persistence.
- No Training Annex JSON or framework public API changed.
- Verification: focused Training Annex/framework-selector tests passed `38/38`; the full suite passed `794/794`; the framework build stayed at `0` warnings and the solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all exited successfully.

### Phase 2-18 Result

- The Training Annex play session now keeps persistent player battle knowledge for player-facing discoveries and save validation.
- Each clean manual battle creates fresh encounter AI knowledge. Enemy observations update that encounter-local store only, so ordinary random-encounter AI learning is discarded after battle.
- Executed player typed actions can update persistent player knowledge: damage records elemental affinity, Analyze records elemental/ailment/instant-death channels, and attempted ailment or instant-death effects can record their typed resistance channels when present.
- Enemy AI receives encounter-local elemental knowledge. A regression test makes Ashling discover Fire resistance, change its next selected skill through framework `DeterministicBattleActionSelector`, and leave that discovery out of the saved player knowledge snapshot.
- The session summary includes player knowledge evidence, encounter AI knowledge evidence, a player `RuntimeKnowledgeSnapshot`, and a last-encounter AI snapshot for tests. Snapshot validation includes player knowledge only.
- No Training Annex content JSON or framework public API changed.
- Verification: focused Training Annex tests passed `39/39`; the full suite passed `796/796`; the framework build stayed at `0` warnings and the solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all exited successfully.

### Future Knowledge Import

Later, after clean ownership/recruitment/fusion/Compendium flows are approved, the player knowledge base should be able to import known defenses from familiar entities. If the player has owned, recruited, fused, recalled, or registered a species/entity, a later encounter with that familiar enemy can immediately show known affinity/resistance hints in the battle UI. This imports into persistent player knowledge only; ordinary enemy AI still receives fresh encounter-local knowledge unless a host deliberately creates a special boss/scripted memory source.

### Non-Goals

- no legacy `ActionProcessor`;
- no legacy `SkillData`;
- no configurable tactics/direct-control switching yet;
- no negotiation inside battle yet.

### Tests

- scripted battle commands produce deterministic outcomes;
- invalid/unaffordable commands do not mutate state;
- item and skill execution share framework assessment;
- battle can end in victory, defeat, pass/round limit, or cancellation where applicable.

## Iteration 6: Rewards, Progression, And Session State

Status: Phase 2-19 implemented for the Training Annex original-content loop.

### Goal

Make the loop feel complete after battle.

### Work

- Calculate rewards from bound clean rulesets.
- Apply EXP to the player actor through framework progression policies.
- Update session counters/flags.
- Update resources and battle knowledge.
- Return the player to the field loop after victory.
- Handle defeat through a simple clean-demo policy.

### Framework Use

- reward service;
- progression policies;
- runtime session progress snapshot;
- runtime save snapshot validation.

### Non-Goals

- no economy shop loop yet;
- no legacy reward payout;
- no complex game-over UI.

### Tests

- reward totals are nonzero for the Training Annex encounter;
- EXP changes session/progression state;
- defeat policy is deterministic;
- post-battle snapshot validates.

### Phase 2-19 Result

- The manual Training Annex battle applies the already-bound `standard_reward` result after player victory.
- EXP is committed through framework growth policies, Macca is committed through framework economy transactions, and the session progress snapshot records victory, EXP, Macca, and the cleared Ashling drill flag.
- The summary exposes both the previewed reward and the applied reward so tests can prove the framework result was not merely printed.
- Post-battle save validation receives live inventory, wallet, session progress, field state, and persistent player battle knowledge.
- Defeat, cancellation, and non-victory outcomes leave rewards unapplied.

## Iteration 7: Save Policy And Suspend Save

Status: Phase 3-20 implemented for the Training Annex clean play host.

### Goal

Add a framework-owned save policy layer and a small host-owned persistence flow for the clean demo.

The framework already has snapshots and validation. This iteration decides when and how the host may save.

### Work

- Add save kinds:
  - manual;
  - autosave/checkpoint if approved;
  - suspend.
- Define allowed save contexts:
  - field;
  - dungeon;
  - battle if approved;
  - menu/checkpoint-only if preferred.
- Define suspend-save behavior:
  - consume/delete after successful load;
  - or configurable host policy if approved.
- Add clean demo save/load menu.
- Keep actual serialization/storage host-owned.

### Framework Use

- `RuntimeSaveGameSnapshot`;
- `IRuntimeSaveValidator`;
- `RuntimeSaveKind`, `RuntimeSaveContextSnapshot`, `RuntimeSavePolicyOptions`, `RuntimeSaveRecord`, and `RuntimeSavePolicyService`.

### Non-Goals

- no cloud save;
- no Godot save resource;
- no save migration system beyond version validation;
- no legacy prototype save retrofit.

### Tests

- manual save allowed in approved contexts;
- suspend save records correct policy;
- loading a suspend save consumes or invalidates it according to policy;
- invalid snapshots fail validation;
- host-owned JSON remains outside framework public APIs.

### Phase 3-20 Result

- The framework now exposes manual and suspend save policy concepts without serializer, filesystem, console, or Godot dependencies.
- `--clean-training-annex-play` has a `Save / Load` menu with `Manual Save`, `Manual Load`, `Suspend Save`, `Suspend Load`, and `Back`.
- Demo storage is intentionally in-memory: one manual slot and one suspend slot. The host stores raw JSON records to prove the restore path really deserializes before validation.
- Manual load preserves its slot. Suspend load consumes its slot only after JSON read, policy assessment, snapshot validation, and session restore all succeed.
- Saving or loading while a prepared encounter is pending is rejected so the demo cannot persist a half-handoff battle state.
- Restore rebuilds the clean Training Annex session from the snapshot and host context: actor runtime snapshots, resources, inventory, wallet, field/dungeon state, session counters/flags, persistent player knowledge, and the resolved Ashling drill menu state.
- This is still a clean-demo product flow, not a legacy save/load retrofit and not a permanent filesystem slot system.

## Iteration 8: Shop And Equipment Demo Flow

### Goal

Demonstrate clean shop/equipment services in the clean runtime.

### Phase 4-22 Equipment Result

- The clean Training Annex host now owns and equips sample equipment through framework inventory/equipment transactions at startup.
- The host summary records the live `RuntimeEquipmentSnapshot` and resolved `RuntimeEquipmentProfile`.
- Manual clean basic attack uses the actor's equipped weapon profile, so replacing `practice_blade` with a test-only `weighted_club` changes the action ID, label, power, and accuracy without editing production JSON.
- Accessory stat modifiers are resolved by the framework profile resolver and tested against `StandardStatResolutionPolicy`; the Training Annex actor does not show visible accessory stat changes because it is authored as `demon`, and the standard policy intentionally ignores equipment modifiers for demons.
- Shop/economy presentation is not part of this checkpoint.

### Phase 4-23 Economy Result

- Training Annex startup now requires the authored `standard_economy` binding and reports typed diagnostics without falling back to directly constructed resource services.
- One bound resource-management bundle supplies inventory, equipment, and wallet transactions to the clean session.
- Victory Macca mutates the live immutable wallet through `IEconomyTransactionService`; the host summary retains the exact before/after `WalletTransactionResult`, and save/load continues to carry the resulting balance.
- Tests inject a nonzero starting wallet and prove reward income is additive. Overflow, negative values, and insufficient funds remain typed non-mutating failures.
- Actual clean spending remains part of the shop interaction below; the console host does not add a fake economy-only command.
- Verification passed `73/73` focused tests and `830/830` full-suite tests; framework warnings stayed at `0`, solution warnings stayed at the existing `98`, and all noninteractive clean demos passed.

### Phase 4-24 Shop Result

- Training Annex play now includes a `Training Supply` clean shop option.
- Shop rows come from the `training_annex_slice.shops.json` catalog, then pass through the framework `RuntimeShopOfferResolver` before they can be displayed or executed.
- Buy/sell row availability is assessed through the same bound `IShopTransactionService` used for execution, so disabled rows such as insufficient funds, duplicate equipment, and equipped-sale blocks are not guessed by the console host.
- Buying mutates the clean runtime inventory and wallet through `standard_economy`; buying equipment can immediately route into `IEquipmentTransitionService` and update the actor's equipment profile.
- Selling an authored shop item adds Macca through the same shop/economy service and blocks selling equipped gear.
- The default demo wallet remains `0`; funded shop tests inject a starting wallet through the host boundary so the shop can prove real spending without adding fake currency.
- Shop/economy verification passed `84/84` focused tests and `836/836` full-suite tests with no skips.

### Phase 4-25 Recovery Facility Result

- Training Annex play now includes a `Recovery Facility` clean option.
- The recovery row is assessed through the bound `IHospitalRestorationService`, then executed through the same service if selected.
- Successful treatment spends Macca, restores HP/SP, clears removable ailments, and clears encounter-persistent clean battle state.
- Insufficient funds and no-restoration-needed states are disabled without mutating wallet or actor state.
- Recovery/shop/economy verification passed `87/87` focused tests and `839/839` full-suite tests with no skips.

### Phase 5-26 Active/Reserve Party Result

- Training Annex play now hydrates Annex Mentor as a reserve support actor beside Echo Adept.
- The live party is created through `RuntimePartyStockSnapshot` and `PartyStockTransitionService.AddPartyMember`, not through host-only list mutation.
- The `Inspect Party` command presents the framework-owned active/reserve snapshot while keeping host presentation separate.
- Manual and suspend save snapshots include the live active/reserve party state and restore it with the clean actor roster.
- This remains `parallel_partial`: Persona stock, Demon stock, summon/return/dismiss, and broader party operations remain later capability work.
- Phase 5-26 verification passed `88/88` focused tests and `843/843` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all four noninteractive clean demos pass, and `Data/Jsons` is unchanged.

### Phase 5-27 Persona/Demon Stock Result

- Training Annex play now hydrates framework-owned stock actors without changing clean content files: an active-form Annex Mentor, a Persona-stock Bramble Runner, and Demon-stock Ashling/Ward Shell entries.
- The live `RuntimePartyStockSnapshot` carries `ActiveForm`, `PersonaStock`, and `DemonStock` references in addition to active/reserve party members.
- The `Inspect Stock` command presents those stock references from the framework snapshot while the console host remains presentation-only.
- Manual and suspend save snapshots persist and restore the active form and stock references. Host restore validation now accepts valid same-team stock and rejects corrupted party/stock references that point at enemy-team actors before live state changes.
- This checkpoint remained `parallel_partial`: clean owned stock existed, but summon, return, swap, dismiss, replace, consume, recruitment, and fusion-driven stock mutation were not part of Phase 5-27.
- Phase 5-27 verification passed `74/74` focused Training Annex host tests and `845/845` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all four noninteractive clean demos pass, and `Data/Jsons` is unchanged.

### Phase 5-28 Party Operations Result

- Training Annex play now exposes `Party / Stock Operations` beside `Inspect Party` and `Inspect Stock`.
- The operation menu is console-host presentation over the same clean `RuntimePartyStockSnapshot`; the framework `PartyStockTransitionService` still owns the rules and diagnostics.
- The demo proves clean active-form swap, Demon-stock summon, active demon swap, return, replace, dismiss, and consume operations without touching legacy `PartyManager` or prototype data.
- Applied and rejected results record before/after evidence for active party size, reserve size, active form, Persona stock count, Demon stock count, affected runtime IDs, and stable transition code.
- This remains `parallel_partial`: the clean host can now mutate party/stock state manually, but recruitment, fusion transactions, battle COMP-style usage, and Godot presentation are still separate capability work.
- Phase 5-28 verification passed `76/76` focused Training Annex host tests and `847/847` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all four noninteractive clean demos pass, and `Data/Jsons` is unchanged.

## Iteration 9: Negotiation Or Recruitment Demo Flow

### Goal

Demonstrate clean negotiation/recruitment if the owner still wants this mechanic in the framework direction.

### Work

- Choose whether negotiation belongs in the Training Annex demo.
- Add a small neutral prompt flow if approved.
- Keep prompts host-owned and outcomes framework-owned.
- Apply recruitment/reward/compendium side effects only if the design is approved.

### Framework Use

- negotiation service;
- reward/recruitment validation;
- runtime party/stock transitions if recruitment adds actors.

### Decisions Needed

- Is negotiation a core framework feature for this project, or optional sample coverage?
- Should recruitment add a demon/entity to stock?
- Should refusal/flee/trick outcomes exist in the clean sample?

### Tests

- prompt choices map to deterministic outcomes;
- recruitment success/failure mutates or preserves state correctly;
- cancellation does not consume unintended resources.

### Phase 6-29 Result

- `--clean-training-annex-play` now exposes `Negotiate / Recruit`.
- The host presents prompts and records command choices, but the outcome comes from framework negotiation and recruitment services.
- Successful negotiation recruits the Training Annex `bramble_runner` into Demon stock through `PartyStockTransitionService.AddDemonToStock` and spends Macca through the bound economy service.
- Refusal and repeated familiar encounters preserve wallet/stock state and are covered by focused tests.
- Authored demand records are not yet the source of the Macca demand amount. The framework service still uses its existing formula, so content-bound demand policy remains a future refinement.

## Iteration 10: Fusion And Compendium Demo Flow

### Goal

Demonstrate clean fusion only after the game's fusion direction is approved. The first safe slice, Phase 7-30, is limited to non-mutating result calculation over original clean content.

### Design Boundary

Fusion is not just a technical feature. It defines the identity of the game loop. Phases 7-30 through 7-34 therefore establish generic operations and opt-in policies without treating any legacy-inspired mechanic as mandatory. Phase 7-35 adds the separately reviewed Compendium transaction and familiar-knowledge boundary.

### Phase 7-30 Result

- `--clean-training-annex-play` now has a `Calculate Fusion Results` proof command.
- The command uses `CatalogFusionContentRepository` and `FusionResultResolver` against the original `convergence.training_annex_slice` pack.
- It proves two catalog-authored results without mutating runtime state:
  - `Ashling + Bramble Runner -> Ward Shell` through an explicit `create_entity` recipe.
  - `Echo Adept + Bramble Runner -> Ward Shell` through a race/rank-offset recipe.
- The host records typed evidence for parent instance IDs, parent entity IDs, operation, result entity ID, accident flag, and diagnostics.
- This is not yet a fusion menu. It is a framework authority proof that removes legacy fusion-table dependency from result calculation.

### Phase 7-31 Result

- `Calculate Fusion Results` now also publishes fusion planning evidence from the clean catalog path.
- The Training Annex content includes generic mutation metadata for `echo_strike` tier 1 and `shell_bash` tier 2 in the `training_physical` family.
- The proof records ordinary and sacrificial inheritance slot counts, natural skills, selectable inherited skills, blocked/known display reason codes, and a deterministic accident sample.
- The current sample proves `Frost Tip` is selectable through explicit allowance, `Steady Breath` remains selectable as passive fusion fodder, `Shell Bash` is shown as already known, `Toxin Touch` is rejected by typed group policy, and accident inheritance can mutate `Echo Strike -> Shell Bash`.
- This is still non-mutating. It does not yet select inherited skills, commit a fusion transaction, modify party/stock state, or touch Compendium state.
- Verification: focused fusion tests passed `3/3`, parity/roadmap guard tests passed `6/6`, and the full suite passed `862/862` with no skips. Framework build stayed at `0` warnings, solution build stayed at `98` legacy warnings, clean battle/field/save/Training Annex demos passed, and protected legacy JSON remained unchanged.

### Phase 7-32 Result

- `--clean-training-annex-play` now has a `Preview Fusion Result` proof command.
- The command uses the same clean catalog fusion path as 7-30 and 7-31, then opens a tiny inherited-skill selection menu for the sacrificial Echo Adept + Bramble Runner + Ashling sample.
- The host can select `Frost Tip`, `Echo Strike`, and passive `Steady Breath`; blocked or already-known rows remain visible with reason labels such as `already_known` and `group_not_allowed`.
- Selected skills are validated through `FusionPlanningService` against the exact retained inheritance plan. `FusionPreviewService` accepts only the resulting validated selection token before creating the Ward Shell preview.
- Confirmation records the preview as accepted, but deliberately mutates no runtime state. Party/stock, Compendium, inventory, wallet, and parent actors remain unchanged.
- This is still non-mutating. Phase 7-33 owns committed clean fusion transactions and rollback behavior.
- Verification: focused 7-32 tests passed `3/3`; parity/roadmap guard tests passed within the `13/13` focused guard run; full suite passed `864/864` with no skips. Framework build stayed at `0` warnings, solution build stayed at `98` legacy warnings, clean battle/field/save/Training Annex demos passed, and protected legacy JSON remained unchanged. Direct redirected CLI smoke for `--clean-training-annex-play` still hits the known `ConsoleIO` cursor-handle limitation; scripted interactive coverage remains test-owned through `ScriptedGameIO`.

### Phase 7-33 Result

- `--clean-training-annex-play` now has a `Commit Fusion Transaction` proof command.
- The proof uses the direct clean catalog recipe `Ashling + Bramble Runner -> Ward Shell`.
- If Ward Shell is already owned, framework transaction assessment rejects the commit with `DuplicateResult` before any stock, actor, wallet, inventory, or Compendium state mutates.
- If the host frees the Ward Shell slot first, the transaction consumes Ashling and the prepared Bramble Runner through framework party/stock transitions, hydrates a new `fusion_ward_shell_1` runtime actor from the catalog, applies the preview skill snapshot, adds the fused result to Demon stock, and validates the resulting save snapshot.
- This remains `parallel_partial`. The clean host now proves atomic transaction mutation and rollback, but Compendium integration and broader strategy approval are still later Phase 7 work.
- Verification: focused 7-33 and guard tests passed `14/14`, focused transaction tests passed `3/3`, and the full suite passed `867/867` with no skips. Framework build stayed at `0` warnings, solution build stayed at `98` pre-existing legacy-host warnings, clean battle/field/save/Training Annex demos passed, `git diff --check` reported only line-ending normalization warnings, framework forbidden-reference search returned no matches, and protected `Data/Jsons` content stayed unchanged.

### Phase 7-34 Result

- Fusion resolution and planning now require an explicit `FusionPolicyRegistry`; there is no framework-owned default policy set.
- The policy boundary covers inheritance slot calculation, sacrificial availability/bonus, accidents, mutation, typed result handlers, optional combination handlers, and the unstructured-token compatibility hook.
- Fusion requests carry optional `FusionPolicyContext` facts instead of a required Moon Phase number. A game may use progression, difficulty, a custom cycle, or no contextual mechanic.
- Training Annex explicitly opts into its sample values and reports the accident/mutation policy IDs plus sacrifice bonus in its summary. Display names do not determine behavior.
- Legacy Moon Phase, result-token, Element, and Mitama-style behavior is configured in `LegacyFusionStrategyPolicies` under the console host. The framework contains no knowledge of those names or conditions.
- Missing authored policy registrations reject with typed diagnostics. No silent fallback selects legacy or sample behavior.
- This remains `parallel_partial`: the clean path is strategy-policy driven, but protected Cathedral transaction strategies remain active and Compendium remains 7-35.
- Verification passed `9/9` focused strategy-policy tests, `63/63` protected fusion compatibility tests, `86/86` Training Annex host tests, and `877/877` full-suite tests with no skips. Framework build warnings remained `0`; solution warnings remained the existing `98`. All four clean demos and boundary/content-preservation gates passed.

### Phase 7-35 Result

- `CompendiumRuntimeService` registers clean actor state by qualified catalog entity ID and recalls it through catalog reconstruction, caller-selected Demon/Persona stock placement, and an atomic wallet result.
- The clean entry preserves level, EXP, lifetime EXP, unspent stat points, integral base stats, learned skills, and equipped skills. Recall restores those durable values, refills recalculated resources, and omits transient status, activation, forms, and equipment.
- `FamiliarEntityKnowledgeService` is an opt-in framework service. Training Annex invokes it after recruitment, committed fusion, Compendium registration, and recall; it updates persistent player knowledge only. Ordinary enemy AI still starts every encounter with a fresh knowledge store.
- The `Compendium` menu discovers eligible owned actors from typed party/stock references and uses typed runtime/content selection identities. It does not infer behavior from row text or fixed entity names.
- Manual and suspend saves now carry clean Compendium state and can restore dynamically recalled actors. Invalid duplicate/ineligible entries and malformed learned/equipped skill relationships are rejected before restore.
- This remains `parallel_partial` because the protected Cathedral/legacy Compendium path is still active. No removal is authorized.

### Remaining Work

- Replace protected Cathedral/legacy Compendium consumers only after a separate consumer-switch review proves they are unreachable.

### Framework Use

- fusion planning;
- fusion transactions;
- inheritance evaluator;
- Compendium service;
- party/stock transitions.

### Non-Goals

- no ATLUS-style data assumptions;
- no legacy Cathedral dependency;
- no full recipe migration.

### Tests

- preview and commit agree;
- inheritance policy is respected;
- transaction is atomic;
- Compendium snapshots remain immutable.
- player battle knowledge can be seeded from owned/registered familiar entity data without seeding ordinary enemy AI knowledge.

## Iteration 11: Content Authoring Comfort Pass

### Goal

Make clean content easier to write and review.

### Work

- Add generic templates for each clean content document family.
- Add a content authoring checklist.
- Add a report command or test helper for:
  - missing references;
  - duplicate IDs;
  - unsupported handler IDs;
  - balance ranges;
  - unused records.
- Keep raw JSON valid and understandable.

### Non-Goals

- no custom editor;
- no Godot plugin;
- no content generator that invents design decisions.

### Tests

- templates parse and validate;
- reports produce deterministic output;
- original content pack remains valid.

## Iteration 12: Clean Demo Maturity Review

### Goal

Review whether the clean console demo has replaced any narrow legacy capability enough to consider archive eligibility.

### Work

- Compare clean demo capability coverage against the parity ledger.
- Identify any capability with:
  - framework rule ownership;
  - migrated clean consumer;
  - tests;
  - owner-approved behavior;
  - no hidden legacy dependency.
- If none qualify, record zero archive candidates.

### Non-Goals

- no archive by default;
- no deletion by default;
- no broad cleanup.

### Tests

- archive gate tests remain green;
- no `removalAuthorized` flag changes unless explicitly approved.

## Feature Priority Board

This board should be updated before each implementation prompt.

| Feature | Priority | Current Decision |
| --- | --- | --- |
| Interactive clean console entry | `P1` | Needed first. |
| Optional Moon Phase decoupling | `P1` | Needed before deepening clean runtime assumptions. |
| Field/dungeon interaction | `P1` | Needed for the demo loop. |
| Manual clean battle | `P1` | Needed for independence proof. |
| Rewards/progression | `P1` | Needed to complete the loop. |
| Save policy and suspend saves | `P1/P2` | Important once the loop has state worth saving. |
| Shop/equipment | `P2` | Add after the base loop. |
| Negotiation/recruitment | `P2` | Training Annex proof implemented; demand-policy binding remains future work. |
| Fusion/Compendium | `P2` | Original clean result/planning/transaction/Compendium proof implemented; protected Cathedral consumers remain active. |
| Content authoring tooling | `P2` | Useful after content shape stabilizes. |
| Godot adapter/project | `P3` | Later, after console proves the clean loop. |
| Legacy archive review | `P3` | Only after clean parity evidence. |

## Recommended Immediate Next Iteration

Iterations 1-7 and CodeReview-1/2 are implemented. CodeReview-3 completes the Phase 3 restore-hardening checkpoint from `docs/phase-1-3-code-review.md`: saved actor identity mappings and saved contexts are validated, Training Annex dungeon host-state validation is explicit, content-pack provenance is stored in save contract v5, and restore is planned before the live session is replaced. CodeReview-4 splits the Training Annex host seams for persistence, field presentation, and reward application while preserving behavior. Phase 4-21 completes the clean field-inventory quantity proof by using selected catalog item IDs and framework inventory reservations instead of a hardcoded item path. Phase 4-22 makes clean equipment ownership and basic attacks equipment-driven. Phase 4-23 binds the live Training Annex wallet and all resource transactions to authored `standard_economy`, with typed transaction evidence and no fallback. Phase 4-24 adds the clean Training Supply shop proof over original catalog offers, bound shop/economy transactions, and immediate equipment transitions. Phase 4-25 adds a clean Recovery Facility proof over the framework hospital service. Phase 5-26 adds clean active/reserve party ownership: Annex Mentor is hydrated as a reserve support actor, the session creates a `RuntimePartyStockSnapshot` through `PartyStockTransitionService`, `Inspect Party` presents that snapshot, and saves restore the live party state. Phase 5-27 adds inspectable clean active-form, Persona-stock, and Demon-stock ownership to that same snapshot, with save/restore validation for same-team stock references. Phase 5-28 adds manual clean party/stock operations over framework transitions: active-form swap, Demon-stock summon, active demon swap, return, replace, dismiss, and consume.

Phase 6-29 adds the clean negotiation/recruitment proof: `Negotiate / Recruit` uses framework negotiation, recruitment, party-stock, and economy services to recruit Bramble Runner into Demon stock, while refusal and familiar repeat paths remain non-mutating.

Phase 6-29 verification passed `104/104` focused Training Annex, party-stock, parity-ledger, and original-content tests and `851/851` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, the clean battle/field/save/Training Annex demos pass, `git diff --check` passes, and the framework forbidden-reference search returns no matches. `Data/Jsons` changed only for `training_annex_slice.negotiations.json`, the clean Training Annex sample content.

Phase 7-30 adds clean catalog fusion result calculation to `Calculate Fusion Results`. Phase 7-31 extends that same command with non-mutating planning evidence for inheritance slots, passive/explicit-allowance filtering, already-known and blocked reason codes, and deterministic mutation/accident evidence. Phase 7-32 adds `Preview Fusion Result`, a non-mutating inherited-skill selection and preview-confirmation proof. Phase 7-33 adds `Commit Fusion Transaction`, a duplicate-result rejection and atomic parent-consume/result-add proof. Phase 7-34 replaces embedded strategy assumptions with required host-selected fusion policies and isolates legacy-inspired rules in the console compatibility layer. Phase 7-35 adds framework-owned clean Compendium registration/recall, persistence, and opt-in familiar-knowledge import.

Phase 7-35 verification passed `148/148` focused capability, boundary, and protected-legacy tests and `893/893` full-suite tests with no failures or skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all clean demos pass, the framework boundary search returns no matches, and `Data/Jsons` is unchanged.

CodeReview-7-1 through CodeReview-7-5 are implemented and improved recipe fidelity, runtime identity, transaction ownership, policy-context propagation, and Compendium entry/save validation. The fresh 2026-07-11 source audit reopened the Phase 7 gate; all four medium findings were corrected by 2026-07-12. Parent-order-neutral rank state, framework-owned preview authority, symmetric Demon/Persona save capacity, duplicate-knowledge rejection, and global runtime-ID enforcement for direct party additions are now covered. The intentional active-party plus Demon-stock overlap remains available through the typed summon command. Phase 7 is review-closed and Phase 8-36 may proceed, without inventing additional console-only presentation work or implying `clean_parity`.

CodeReview-7-4 verification passed `27/27` focused strategy/transaction tests, `200/200` broad Phase 7 tests, and `932/932` full-suite tests. The framework build remained at `0` warnings, all four clean demos passed, and no content data changed.

CodeReview-7-5 verification passed `49/49` focused Compendium/persistence/host JSON tests, `233/233` expanded Phase 7 tests, and `937/937` full-suite tests. Later fresh-audit corrections supersede that historical gate: party-addition identity passes `23/23` focused transition tests, Compendium policy/host/boundary compatibility passes `143/143`, and the complete suite passes `951/951`. Registration-only, free, and explicitly priced recall are now separate framework configurations; existing host costs remain unchanged. The review queue is closed for Phase 8 with no source finding deferred.

Then attach one feature at a time.
