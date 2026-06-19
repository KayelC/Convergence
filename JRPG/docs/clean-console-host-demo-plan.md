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

## Iteration 3: Field And Dungeon Interaction Loop

### Goal

Let the player move through a tiny Training Annex flow without relying on automatic floor-forces-battle behavior.

### Work

- Add an interactive field menu for the clean demo.
- Show the current location/floor/session state.
- Let the player enter the Training Annex.
- Let the player move between known demo locations/floors.
- Represent host-owned interaction points:
  - inspect room;
  - interact with encounter trigger;
  - return to entrance;
  - exit.
- Use the framework dungeon/field state machine for legal transitions.
- Use the encounter-start planner when the host chooses a specific encounter trigger.

### Framework Use

- `RuntimeFieldDungeonService`;
- `RuntimeDungeonProgressSnapshot`;
- `CatalogEncounterStartPlanner`;
- host-owned scene/trigger IDs.

### Non-Goals

- no spatial Godot map;
- no random patrol system;
- no full dungeon editor;
- no legacy `DungeonManager`.

### Tests

- scripted player choices move through the clean dungeon loop;
- encounter trigger creates the expected actor creation requests;
- fixed battle floors preserve authored encounter IDs;
- host-owned trigger selection does not force every floor ascent into battle.

## Iteration 4: Clean Item And Field Action Loop

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
- battle lifecycle services;
- battle knowledge stores.

### Non-Goals

- no legacy `ActionProcessor`;
- no legacy `SkillData`;
- no full AI/tactics design;
- no negotiation inside battle yet.

### Tests

- scripted battle commands produce deterministic outcomes;
- invalid/unaffordable commands do not mutate state;
- item and skill execution share framework assessment;
- battle can end in victory, defeat, pass/round limit, or cancellation where applicable.

## Iteration 6: Rewards, Progression, And Session State

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

## Iteration 7: Save Policy And Suspend Save

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
- new save policy contracts if approved.

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

If this plan is approved, start with:

```text
Iteration 1: Clean Demo Entry And Session Shell
```

That pass should create the interactive entry point and session skeleton, but avoid implementing battle/shop/fusion/negotiation all at once.

The point is to build the new clean demo like a spine:

```text
boot -> menu -> session -> field -> encounter -> battle -> rewards -> save
```

Then attach one feature at a time.
