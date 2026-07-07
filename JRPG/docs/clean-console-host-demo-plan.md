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

Status: Phases 2-11 through 2-19 are implemented; CodeReview-1 and CodeReview-2 stabilization are complete. Phase 3 restore hardening is next.

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

### Work

- Add a small shop interaction in Training Annex.
- List clean item/equipment offers.
- Buy one item.
- Equip one piece of equipment.
- Show updated actor stats/basic attack profile if relevant.
- Keep economy and inventory mutations framework-backed.

### Framework Use

- resource-management services;
- shop transaction service;
- equipment transition service;
- clean catalog item/equipment/shop definitions.

### Non-Goals

- no legacy `ShopEngine`;
- no old shop inventory JSON;
- no broad economy rebalance.

### Tests

- buy/sell/equip behavior is deterministic;
- insufficient funds rejects without mutation;
- equipped-sale rules behave as defined;
- equipment changes are visible in runtime state.

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

## Iteration 10: Fusion And Compendium Demo Flow

### Goal

Demonstrate clean fusion only after the game's fusion direction is approved.

### Why Blocked

Fusion is not just a technical feature. It defines the identity of the game loop. The current framework has fusion concepts, but the owner has not settled what fusion should mean for the final game.

### Possible Work After Approval

- Add a tiny fusion menu.
- Show two owned entities.
- Preview a result.
- Select inherited skills.
- Commit transaction.
- Register/recall a simple Compendium entry if approved.
- Demonstrate optional player-knowledge import from a registered/owned familiar entity, so later target selection can show known weakness/resistance hints without a fresh Analyze action.

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
| Negotiation/recruitment | `P2` | Requires owner confirmation. |
| Fusion/Compendium | `Blocked/P2` | Requires owner design decision. |
| Content authoring tooling | `P2` | Useful after content shape stabilizes. |
| Godot adapter/project | `P3` | Later, after console proves the clean loop. |
| Legacy archive review | `P3` | Only after clean parity evidence. |

## Recommended Immediate Next Iteration

Iterations 1-7 and CodeReview-1/2 are implemented. The next approved stabilization work is Phase 3 restore hardening from `docs/phase-1-3-code-review.md`: validate actor identity mappings and saved contexts, define dungeon host-state validation and content provenance, and plan restore atomically before Phase 4 begins.

Then attach one feature at a time.
