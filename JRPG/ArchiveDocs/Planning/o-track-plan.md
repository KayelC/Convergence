# Track O Console Host Migration Plan

> **Status: Active working plan.** This file expands Track O from the framework parity plan into concrete subtracks. It exists because Track O became too broad to finish safely as one commit after O1.

## Purpose

Track O makes the interactive console application a real consumer of framework commands, results, events, catalogs, and snapshots without removing the playable legacy prototype.

The goal is not to make the console UI beautiful. The goal is to route each existing workflow through framework-owned contracts where those contracts already exist, while preserving the current visible behavior, cancellation paths, and debug/parity scenarios.

Track O is a host migration arc. It does not reauthor production data, remove legacy datasets, delete live `Combatant` or `Persona` models, or complete Godot integration.

## Current Baseline

Track O1 is complete at commit `d8fe09a`.

O1 accomplished:

- ordinary startup still calls `Database.LoadData` first;
- startup then creates `InteractiveConsoleHostContext`;
- retained clean packs load as a nonfatal sidecar catalog;
- sidecar diagnostics publish through a host event sink;
- simple field, city, inventory, status, dungeon, terminal, hospital-patient, and field-target menus route through framework host-command adapters;
- human status text is rendered from a runtime snapshot projection;
- `interactive_boot` and `console_presentation` are marked `parallel_partial` in the parity ledger;
- no gameplay rule, legacy dataset, rich bridge, battle command flow, Cathedral flow, or removal authorization changed.

O1 deliberately did not migrate:

- rich hover-preview menus;
- persona or demon stock inspection screens;
- shop presentation;
- Cathedral presentation;
- battle command presentation;
- battle narration and result rendering;
- negotiation prompts;
- reward presentation;
- production content authority away from `Database`;
- any legacy file removal.

## Migration Rules

Every O subtrack must obey these rules:

- Keep ordinary no-flag `dotnet run` interactive.
- Preserve current menu text, option order, cancellation behavior, waits, and visible messages unless a documented rule decision explicitly changes them.
- Keep legacy `Data/Jsons` unchanged.
- Keep `Database` as the production content authority until Track Q production data reauthoring.
- Keep live `Combatant`, `Persona`, `InventoryManager`, `EconomyManager`, `PartyManager`, `DungeonState`, `BattleKnowledge`, and `CompendiumRegistry` as console-owned state until their replacement consumers are complete.
- Do not move `IGameIO`, `Console`, filesystem access, Newtonsoft, or legacy DTO types into `JRPG.Framework`.
- Prefer typed command/result/event adapters at console boundaries.
- Do not infer gameplay from display names, descriptions, or prose in new framework code.
- Update tests, docs, and the parity ledger in the same subtrack as the migration.
- Keep `removalAuthorized: false` until a dedicated removal gate records clean parity and migrated consumers.

## Architecture Direction

Track O should gradually turn console bridge methods into thin adapters:

```text
IGameIO menu/input
  -> console host command adapter
  -> framework command/result/event contract
  -> console compatibility adapter when live legacy objects are still required
  -> unchanged conductor return shape until the conductor is migrated
```

The framework should own durable concepts:

- command identity;
- command availability;
- assessment and execution results;
- event ordering;
- immutable snapshots;
- diagnostics and cancellation distinction.

The console host should own presentation details:

- wording;
- colors;
- waits;
- menu cursor memory;
- hover previews;
- console-only inspection shortcuts;
- filesystem-backed content text;
- mapping framework events into readable output.

## Subtrack Overview

### O1: Startup And Plain Menu Shell

Status: Complete.

Commit: `d8fe09a host: migrate interactive console shell to framework contracts`

Scope:

- startup sidecar catalog;
- host event sink for startup diagnostics;
- command adapter for simple menus;
- plain field/status/inventory/dungeon menu routing;
- human status projection.

Remaining risk:

- The sidecar catalog is readiness evidence only. Gameplay still uses legacy `Database`.
- Rich presentation and most workflow conductors still use legacy bridge methods directly.

### O2: Status And Read-Only Presentation

Goal:

Move read-only status, stock, equipment, and inspection presentation onto framework snapshots where possible, while preserving current console text and menus.

Candidate files:

- `Logic/Field/Bridges/StatusUIBridge.cs`
- `Logic/Field/Bridges/LegacyHumanStatusProjection.cs`
- `Logic/Core/LegacyRuntimeIdentityRegistry.cs`
- `Logic/Runtime` snapshot definitions in `JRPG.Framework`
- `Convergence.Tests/Host/ConsolePlainMenuCommandTests.cs`
- `Convergence.Tests/LegacyWorkflowCharacterizationTests.cs`

Work:

- Expand status projection beyond the human summary.
- Add projection helpers for:
  - active Persona detail;
  - Persona stock entries;
  - demon stock entries;
  - active party summary;
  - equipped slot summary;
  - stat allocation preview, if it can be represented without changing commit/rollback behavior.
- Keep existing status strings and menu order.
- Keep detailed stock inspection and status shortcuts console-owned, but feed them from immutable snapshots when practical.
- Add tests proving display text remains stable before and after projection.

Do not:

- change stat formulas;
- alter stock ownership;
- migrate stat allocation mutation;
- remove existing `StatusUIBridge` methods.

Exit gate:

- Read-only status surfaces are backed by framework snapshots or documented as still legacy-only.
- Existing status workflow tests pass unchanged.
- New projection tests cover Human, Persona User, Wild Card, Operator, and Demon views.

Completion:

- Replaced the narrow human-only projection with `LegacyStatusPresentationProjection`.
- Human summaries, Persona details, demon details, Persona stock labels, demon stock labels, organize-party labels, summon labels, and equipment slot labels now render through copied projection data.
- `StatusUIBridge` keeps its public methods, menu order, cancellation behavior, status-peek flow, and legacy return values.
- O2 remains read-only presentation work. Stat allocation, equipment mutation, Persona/demon stock mutation, rich battle presentation, Cathedral presentation, and production content authority remain outside this subtrack.
- The parity ledger keeps `console_presentation` at `parallel_partial` and records `StatusPresentationProjectionTests` as O2 evidence.
- Focused O2 verification passed: status projection tests, plain menu command tests, and the status/equipment surface characterization test reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 640 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 113 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O3: Inventory And Field Effect Presentation

Goal:

Route interactive field item and field skill flows through existing framework action/effect result contracts where safe, while preserving legacy item data and visible behavior.

Candidate files:

- `Logic/Field/Bridges/InventoryUIBridge.cs`
- `Logic/Field/Engines/FieldServiceEngine.cs`
- `Logic/Battle/Execution` and field-effect executors in `JRPG.Framework`
- `Logic/Runtime/ResourceManagementServices.cs` in `JRPG.Framework`
- `Logic/Core/LegacyInventoryResourceAdapter.cs`

Work:

- Move item/skill target selection and result rendering toward typed action/effect results.
- Ensure item consumption decisions share the Track H/L assessment path.
- Preserve no-effect rollback behavior.
- Preserve Goho-M, Traesto, recovery, cure, and revive field behavior.
- Keep legacy item/skill DTO parsing in console compatibility code until production content is reauthored.
- Add tests for inventory menu cancellation, target cancellation, no-effect uses, consumption, and host requests.

Do not:

- reauthor `items.json`;
- change inventory quantities;
- alter field dungeon-exit behavior;
- move item DTOs into the framework.

Exit gate:

- Field item/skill command eligibility and execution cannot disagree for migrated flows.
- Consumption remains host-owned but is driven by framework result decisions.
- Legacy field item/skill characterization tests remain green.

Completion:

- Track O3 began from `53fbf40` on `track-12-recovery`.
- Added typed console-host field selection and field-use result contracts for item selection, skill performer selection, field skill selection, target selection, assessment, execution reasons, consumption decisions, and ordered presentation events.
- Routed `FieldConductor` item/skill flows through typed bridge results and detailed field execution results while keeping legacy-compatible wrapper methods available for existing callers.
- `FieldServiceEngine` now exposes shared assessment and detailed execution for field items and skills. Recovery, cure, Goho-M, no-effect, unsupported field item, and insufficient-SP behavior keep their previous visible outcomes and mutation order.
- Legacy `ItemData`, `SkillData`, `Database`, and string/effect parsing remain console-host compatibility concerns. No production JSON or framework public API changed.
- Focused O3 verification passed: `FieldInventoryPresentationTests`, `ConsolePlainMenuCommandTests`, and the representative field/shop characterization test reported 11 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 647 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 105 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O4: Party And Stock Organization Presentation

Goal:

Route organize-party, demon stock, Persona stock, summon, return, swap, dismiss, and replace presentation through Track F party/stock transition results.

Candidate files:

- `Logic/Core/PartyManager.cs`
- `Logic/Core/LegacyPartyStockAdapter.cs`
- `Logic/Field/Bridges/ServiceUIBridge.cs`
- `Logic/Field/Bridges/StatusUIBridge.cs`
- `Logic/Field/FieldConductor.cs`
- `JRPG.Framework/Logic/Runtime/PartyStockTransitions.cs`

Work:

- Use framework command/result codes for organize-party decisions.
- Preserve active plus owned demon stock invariant.
- Preserve active party max and stock capacity behavior.
- Preserve visible messages and cancellation paths.
- Add structured result rendering for failures such as full party, stock full, already active, not owned, invalid slot, or duplicate owned.

Do not:

- replace live party lists with framework snapshots;
- change stock capacity rules;
- change fusion stock transaction behavior.

Exit gate:

- Organize-party and stock presentation is backed by framework transition results.
- Existing `PartyManagerTests`, field organize tests, battle COMP tests, and stock characterization tests remain green.

Completion:

- Track O4 began from `c7d7c40` on `track-12-recovery`.
- Added `PartyStockPresentationResults` for typed organize-slot, demon stock, Persona stock, summon-target, and mutation presentation outcomes.
- `StatusUIBridge` now exposes typed selected/back/unavailable result methods for Persona stock, demon stock, organization slots, Persona actions, and summon targets while preserving the old wrapper return values for existing callers.
- `LegacyPartyStockAdapter` now exposes detailed Track F transition results for field-side party/stock presentation without changing the existing bool-returning `PartyManager` surface.
- `FieldServiceEngine` now returns typed mutation presentation results for Persona swap, demon summon, return, swap, and dismiss. These results carry transition codes, affected runtime IDs, and ordered field messages while preserving live `Combatant`/`Persona` mutation, active plus owned demon overlap, transient cleanup, and Persona HP/SP capping.
- `FieldConductor` now consumes typed bridge results for field party organization, demon stock, and Persona stock flows.
- Focused O4 verification passed: `PartyStockPresentationTests`, `PartyStockAdapterTests`, `PartyManagerTests`, and `StatusPresentationProjectionTests` reported 27 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 654 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O5: Battle Command Shell

Goal:

Route player battle command selection through typed framework action commands while keeping `InteractionBridge`, `ActionProcessor`, and legacy effect execution as host-owned adapters.

Candidate files:

- `Logic/Battle/Bridges/InteractionBridge.cs`
- `Logic/Battle/ActionProcessor.cs`
- `Logic/Battle/LegacyBattleActionAdapter.cs`
- `JRPG.Framework/Logic/Battle/Execution/BattleActionExecutor.cs`
- `JRPG.Framework/Logic/Battle/Runtime/BattleEncounterRunner.cs`

Work:

- Convert top-level player action choices into `BattleActionCommand` variants.
- Share assessment results between menu availability and execution where migrated.
- Preserve attack, skill, item, guard, pass, analyze, Persona swap, COMP summon/swap/return, tactics, escape, and negotiation option order.
- Keep legacy hover/targeting behavior where it has not yet been represented by framework contracts.
- Add tests proving bridge eligibility cannot disagree with execution for migrated commands.

Do not:

- rewrite the encounter loop;
- reauthor skill or item data;
- change Press Turn outcomes;
- change AI behavior.

Exit gate:

- Player command selection can produce typed framework action commands.
- Legacy `ActionProcessor` remains available as the compatibility executor.
- Battle menu characterization tests remain green.

Completion:

- Track O5 began from `f671491` on `track-12-recovery`.
- Added `BattleCommandShellResult` and payload kinds for selected/back/unavailable battle command outcomes.
- Added `LegacyBattleCommandShellAdapter`, which converts live console battle selections into framework `BattleActionCommand` objects while carrying the legacy payloads still needed by `ActionProcessor`, `PartyManager`, `NegotiationEngine`, and existing battle helper methods.
- Legacy-only attack, skill, item, escape, tactics, and negotiation surfaces use stable host-mediated action IDs. Guard, pass, analyze, Persona swap, demon summon, demon return, and demon swap use concrete framework command shapes where those contracts already exist.
- `BattleConductor` now routes player selection through the shell before legacy execution. Failed framework-backed stock/persona assessment returns to the relevant menu without mutating live legacy state.
- Legacy `SkillData`, `ItemData`, production `Data/Jsons`, AI, Press Turn outcomes, reward/recruitment logic, battle narration, and clean skill/item execution remain unchanged.
- Updated the parity ledger. `battle_actions`, `enemy_ai_and_tactics`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O5 verification passed: `BattleCommandShellTests`, `BattleBridgeResultTests`, and `ActionProcessorResultTests` reported 53 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 664 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O6: Battle Event Presentation And Lifecycle Shell

Goal:

Render framework encounter/action/lifecycle events through console presentation adapters while preserving existing battle narration and result order.

Candidate files:

- `Logic/Battle/BattleConductor.cs`
- `Logic/Battle/Messaging`
- `Logic/Battle/Bridges/InteractionBridge.cs`
- `JRPG.Framework/Logic/Battle/Runtime/BattleEncounterRunner.cs`
- `JRPG.Framework/Logic/Battle/Execution`

Work:

- Map framework battle events into console messages.
- Preserve battle-start, phase, turn-start, action, Press Turn, resource, status, passive, defeat, escape, and battle-end narration.
- Keep reward payout and recruitment side effects host-owned until O7 or later.
- Add event ordering tests around representative player and enemy turns.

Do not:

- change battle rules;
- change reward formulas;
- change negotiation/recruitment semantics;
- remove existing battle messaging classes.

Exit gate:

- The console battle can consume structured framework battle events for migrated phases.
- Existing ordinary battle characterization remains green.

Completion:

- Track O6 began from `3dae5ae` on `track-12-recovery`.
- Added `BattleEventPresentationResult` and `LegacyBattleEventPresentationAdapter` for typed `Shown`, `Suppressed`, and `HostOwned` battle-event presentation outcomes.
- `BattleConductor.RunFrameworkEncounter` now passes the event presentation sink into `BattleEncounterServices`, so framework encounter events are consumed by the console host without printing generic structural narration.
- Generic actor-created, battle-started, initiative, round, phase, turn-start, Press Turn, and phase-end events are suppressed to preserve existing visible output. Lifecycle-shell messages for skip, fear flee, return-to-COMP, enemy flee, and demon defeat return now flow through typed presentation results.
- O5 command-shell behavior remains intact. Legacy attack, skill, item, escape, tactics, negotiation, AI, rewards, recruitment, production `Data/Jsons`, and clean skill/item execution did not change.
- Updated the parity ledger. `press_turn`, `battle_actions`, `enemy_ai_and_tactics`, and `console_presentation` gained O6 evidence and remain `parallel_partial`; `battle_knowledge` did not gain new O6 evidence and was left unchanged. No removal is authorized.
- Focused O6 verification passed: `BattleEventPresentationTests`, `BattleCommandShellTests`, `BattleBridgeResultTests`, `ActionProcessorResultTests`, `BattleEncounterRunnerTests`, and the ordinary battle routing characterization reported 74 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 671 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O7: Negotiation And Reward Presentation

Goal:

Route negotiation prompts, demand presentation, recruitment outcomes, reward payout presentation, and post-battle registration prompts through Track K framework services and host command/event contracts.

Candidate files:

- `Logic/Battle/Engines/NegotiationEngine.cs`
- `Logic/Battle/BattleConductor.cs`
- `Logic/Battle/LegacyNegotiationAdapter.cs`
- `Logic/Battle/LegacyBattleRewardAdapter.cs`
- `Logic/Fusion/CompendiumRegistry.cs`
- `JRPG.Framework/Logic/Battle/NegotiationRewardServices.cs`

Work:

- Use typed prompt and response objects for negotiation questions.
- Preserve familiar dialogue, demands, failure, recruitment, and moon-phase blocking behavior.
- Present immutable reward results from the framework service.
- Preserve automatic compendium registration hooks.
- Add tests for cancellation, failed negotiation, familiar demon path, successful recruitment, reward payout, and post-battle registry evaluation.

Do not:

- reauthor `questions.json`;
- change demand rules;
- change reward formulas;
- change compendium recall rules.

Exit gate:

- Negotiation and rewards remain visibly unchanged but flow through typed prompt/result contracts.
- Existing negotiation and reward characterization tests remain green.

Completion:

- Track O7 began from `01ddcc4` on `track-12-recovery`.
- Added `NegotiationRewardPresentationResults` for typed negotiation prompt, demand, event, outcome, recruitment, and reward presentation records.
- `NegotiationEngine` now keeps `StartNegotiation` as the public legacy wrapper and exposes an internal detailed result containing the framework `NegotiationSessionResult`, mapped legacy result, prompt records, event records, and mutation summary.
- The old private negotiation command/event classes were replaced with `LegacyNegotiationPresentationAdapter`, still backed by `IGameIO` and preserving current prompt headers, options, colors, waits, and message text.
- `BattleConductor` now shares one negotiation/recruitment presentation helper between the framework encounter path and older compatibility method, preserving already-spoken, joined-party, failed, trick/flee, and familiar-flee turn effects.
- `LegacyBattleRewardAdapter` now exposes typed reward presentation for the existing victory reward message before applying EXP/Macca mutations.
- Production `questions.json`, negotiation data, reward formulas, recruitment rules, compendium recall, Cathedral presentation, production `Data/Jsons`, and framework public APIs did not change.
- Updated the parity ledger. `negotiation_and_recruitment`, `battle_rewards`, `battle_actions`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O7 verification passed: `NegotiationRewardPresentationTests`, `NegotiationRewardRuntimeTests`, `BattleCommandShellTests`, `BattleEventPresentationTests`, and the negotiation/ordinary battle characterizations reported 40 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 688 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O8: Shops And Hospital Presentation

Goal:

Route shop browsing, buy/sell, equipment sale blocking, hospital treatment, and failure presentation through Track L framework transaction results.

Candidate files:

- `Logic/Field/Engines/ShopEngine.cs`
- `Logic/Field/Engines/FieldServiceEngine.cs`
- `Logic/Field/Bridges/ShopUIBridge.cs`
- `Logic/Field/Bridges/ServiceUIBridge.cs`
- `Logic/Core/LegacyInventoryResourceAdapter.cs`
- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs`

Work:

- Use transaction assessment results to drive disabled/enabled menu rows where practical.
- Preserve Luck pricing, missing metadata fallback, equipped-item sale rejection, and hospital UI quirks.
- Preserve hospital HP/SP eligibility behavior, including engine-level zero-cost ailment treatment where already supported.
- Add tests for shop option ordering, price display, insufficient funds, duplicate equipment, equipped sale block, hospital success/failure, and cancellation.

Do not:

- change shop inventory data;
- add equipment instances;
- alter economy formulas;
- remove metadata repair.

Exit gate:

- Shop/hospital mutation decisions and visible failures are driven by framework transaction results.
- Existing shop and hospital characterization tests remain green.

Completion:

- Track O8 began from `713d3cb` on `track-12-recovery`.
- Added `ShopHospitalPresentationResults` for typed shop command, offer selection, confirmation, inspection, transaction, hospital patient, and hospital treatment presentation records.
- `ShopUIBridge` now keeps `OpenShop` as the public legacy wrapper while routing shop command selection, buy/sell selection, confirmation, and inspection through typed results.
- `ShopEngine` now keeps `ExecutePurchase` and `ExecuteSale` as legacy wrappers while exposing detailed transaction presentation results backed by framework `ShopTransactionResult`.
- `ServiceUIBridge`, `FieldServiceEngine`, and `FieldConductor` now route hospital patient selection and treatment display through typed results backed by framework `HospitalRestorationResult`.
- Luck pricing, missing metadata fallback, equipped sale blocking, duplicate equipment rejection, hospital HP/SP eligibility labels, zero-cost ailment-only engine treatment, production `Data/Jsons`, and framework public APIs did not change.
- Updated the parity ledger. `shops`, `hospital`, `economy`, `equipment_ownership`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O8 verification passed: `ShopHospitalPresentationTests`, `ResourceManagementServiceTests`, and the shop/hospital/menu characterizations reported 17 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 694 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O9: Dungeon Traversal Presentation

Goal:

Route dungeon movement, terminal selection, floor entry, encounter request, boss request, barrier, and exit presentation through Track M field/dungeon transition events.

Candidate files:

- `Logic/Field/Dungeon/DungeonManager.cs`
- `Logic/Field/ExplorationProcessor.cs`
- `Logic/Field/FieldConductor.cs`
- `Logic/Field/Bridges/DungeonUIBridge.cs`
- `JRPG.Framework/Logic/Runtime/FieldDungeonStateMachines.cs`

Work:

- Present ordered field/dungeon events from framework transition results.
- Preserve lobby, safe room, terminal, fixed floor, boss, defeated boss, barrier, fallback empty floor, and random encounter messages.
- Keep enemy hydration and duplicate suffix naming host-owned.
- Add tests for terminal warp, return to city, Goho-M exit, boss defeat registration, blocked barrier, and random encounter handoff.

Do not:

- reauthor `tartarus.json`;
- change encounter generation;
- change boss battle preparation;
- migrate save/load.

Exit gate:

- Dungeon traversal presentation consumes framework transition events where migrated.
- Existing dungeon characterization tests remain green.

Completion:

- Track O9 began from `60858c6` on `track-12-recovery`.
- Added `DungeonTraversalPresentationResults` for typed dungeon action selection, floor selection, transition presentation, floor-entry presentation, and mapped shown/suppressed/host-owned event records.
- `DungeonManager` now exposes detailed transition presentation methods while keeping `Ascend`, `Descend`, `ProcessCurrentFloor`, `TryWarpToUnlockedFloor`, `ReturnToCity`, `RequestDungeonExit`, and `RegisterBossDefeat` behavior-compatible for existing callers.
- `DungeonUIBridge` now exposes typed selection methods for floor actions, entry floors, and terminal destinations, plus a presentation-event publisher that preserves the existing visible text, colors, waits, and cancellation behavior.
- `ExplorationProcessor` and `FieldConductor` consume detailed transition/floor-entry results for movement, terminal warp, Goho-M/explicit exits, barriers, boss requests, boss defeat registration, and encounter handoff. Enemy hydration and duplicate suffix naming remain host-owned.
- Production `tartarus.json`, framework public APIs, `BattleConductor`, `CombatantFactory`, and legacy live-object behavior did not change.
- Updated the parity ledger. `field_navigation`, `dungeon_traversal`, `encounters`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O9 verification passed: `DungeonTraversalPresentationTests`, `FieldDungeonStateMachineTests`, the dungeon workflow characterizations, and dungeon/target menu command tests reported 14 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 699 passed, 0 failed, 0 skipped.
- The nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### O10: Fusion And Compendium Presentation

Goal:

Route Cathedral menus, fusion preview presentation, inheritance selection, accident/mutation messages, transaction confirmation, compendium browse/register/recall, and cancellation through Track N framework fusion/Compendium services.

Candidate files:

- `Logic/Fusion/FusionConductor.cs`
- `Logic/Fusion/Bridges/CathedralUIBridge.cs`
- `Logic/Fusion/FusionCalculator.cs`
- `Logic/Fusion/FusionMutator.cs`
- `Logic/Fusion/CompendiumRegistry.cs`
- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs`

Work:

- Use immutable preview and transaction results to drive Cathedral UI.
- Preserve binary, sacrificial, rank, Mitama, accident, mutation, duplicate-result, and cancellation behavior.
- Present inheritance eligibility and rejection reasons from framework planning results.
- Preserve compendium deep snapshot behavior introduced in Track N.
- Add tests for menu ordering, preview parity, inheritance selection, accident replacement, transaction rejection, recall failure/success, and cancellation.

Do not:

- reauthor `fusion_table.json`;
- remove fusion strategy classes;
- change accident or mutation probabilities;
- change recall pricing;
- change stock/economy mutations.

Exit gate:

- Cathedral presentation is backed by framework fusion/Compendium results where migrated.
- Existing fusion and compendium characterization tests remain green.

Completion:

- Track O10 began from `4ed67ca` on `track-12-recovery`.
- Added `FusionCompendiumPresentationResults` for typed Cathedral menu, ritual participant, inheritance row, ritual confirmation, ritual sequence, transaction, Compendium recall, Compendium registration, and recall transaction presentation records.
- `CathedralUIBridge` keeps its existing public wrapper methods while exposing detailed presentation methods for main menu selection, participant selection, inheritance selection, ritual confirmation, ritual animation, Compendium recall, and registration selection.
- `FusionConductor` now consumes the detailed bridge results while preserving the existing binary/sacrificial ritual loops, wait/cancel behavior, accident reveal timing, legacy live-object mutation, and post-ritual return flow.
- `FusionPlan` now carries framework inheritance display entries from `FusionPlanningService` as presentation evidence; legacy display names and existing pickable/exclusive behavior remain unchanged.
- `FusionMutator` and `CompendiumRegistry` now expose detailed transaction/registration/recall presentation results while preserving their legacy public wrappers, messages, recall pricing, stock/economy mutation, and deep snapshot behavior.
- Production `fusion_table.json`, production `Data/Jsons`, fusion strategy classes, accident/mutation probabilities, recall pricing, and framework public APIs did not change.
- Focused O10 verification passed: `FusionCompendiumPresentationTests`, `FusionBridgeResultTests`, `FusionCompendiumRuntimeTests`, `FusionBugRegressionTests`, and `FusionInheritanceTests` reported 63 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 706 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O Final Exit Gate

Track O is complete only when:

- ordinary no-flag startup remains interactive;
- every protected workflow is reachable through the console host using framework services where those services exist;
- all migrated menus distinguish host cancellation from ordinary menu cancellation where the framework contract supports it;
- startup sidecar catalog diagnostics remain nonfatal until production content is reauthored;
- rich console presentation surfaces either use framework snapshots/results or are explicitly documented as deferred;
- debug scenarios remain usable as parity tools;
- clean battle and clean field demos still pass;
- full solution tests pass with no skipped tests;
- nonincremental build warnings do not increase from the current recorded baseline;
- `git diff --check` passes;
- framework forbidden-reference searches remain clean;
- `Data/Jsons` remains unchanged;
- parity ledger statuses accurately reflect partial versus complete migration;
- every `removalAuthorized` flag remains `false` unless a later dedicated removal gate proves clean parity.

## Standard Verification For Each O Subtrack

Run the following unless the subtrack document narrows it for a justified reason:

```powershell
dotnet test Convergence.Tests\Convergence.Tests.csproj --no-restore --filter "<focused filter>"
dotnet test JRPG.sln --no-restore
dotnet build JRPG.sln --no-restore --no-incremental /clp:Summary
dotnet run --no-build -- --clean-battle-demo
dotnet run --no-build -- --clean-field-demo
git diff --check
rg -n "JRPGPrototype\.Data\.(Models|Jsons)|Newtonsoft|IGameIO|Console\.|System\.Console|System\.IO|\bDatabase\b|\bSkillData\b|\bPersonaData\b|\bItemData\b|\bWeaponData\b|\bArmorData\b|\bBootData\b|\bAccessoryData\b|\bShopData\b|\bDungeonData\b" JRPG.Framework
git status --short -- Data\Jsons
```

Expected results:

- focused tests pass;
- full suite passes;
- build succeeds;
- warning count is recorded;
- both demos complete;
- whitespace check passes;
- framework forbidden-reference search returns no matches;
- `Data/Jsons` has no modified files.

## Documentation And Ledger Updates

Each O subtrack must update:

- `docs/o-track-plan.md` with completion notes;
- `docs/framework-parity-migration-plan.md` with a concise completion record;
- `docs/production-baseline.md` with the exact verification totals;
- `docs/architecture.md` or `docs/gameplay-systems.md` when ownership changes;
- `Convergence.Tests/Fixtures/Parity/recovery-baseline.json` when a capability moves from `legacy_only` or `clean_foundation` to `parallel_partial`.

Ledger rules:

- Promote only capabilities that actually gained a real console consumer.
- Keep `clean_parity` for a capability only when framework implementation, data, tests, and consumer migration all satisfy the protected behavior.
- Keep `removalAuthorized: false` throughout Track O unless a separate removal track is approved.

## Recommended Next Track

Track O presentation migration is complete after O10 once the full quality gate has passed and the completion commit is pushed.

Reason:

- O1 through O10 migrate the planned console-host presentation shells for startup, field/status/inventory/party, battle command/event, negotiation/reward, shop/hospital, dungeon traversal, and Cathedral fusion/Compendium.
- All Track O capabilities remain protected as `parallel_partial`; no removal is authorized.
- The next documented migration lane is **Track P: Godot Integration Contract** in `docs/framework-parity-migration-plan.md`, but it should begin only after O10 verification is recorded.

Suggested commit message:

```text
host: migrate fusion compendium presentation
```
