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

### O9: Dungeon Traversal Presentation

Goal:

Route dungeon movement, terminal selection, floor entry, encounter request, boss request, barrier, and exit presentation through Track M field/dungeon transition events.

Candidate files:

- `Logic/Field/Dungeon/DungeonManager.cs`
- `Logic/Field/ExplorationProcessor.cs`
- `Logic/Field/FieldConductor.cs`
- `Logic/Field/Bridges/DungeonUIBridge.cs`
- `JRPG.Framework/Logic/Field/FieldDungeonRuntime.cs`

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

## Recommended Next Subtrack

Proceed with **O2: Status And Read-Only Presentation**.

Reason:

- O1 already introduced the status snapshot projection.
- O2 is low mutation risk compared with inventory, battle, dungeon, or Cathedral flows.
- It improves the host-boundary shape while preserving all gameplay rules.
- It creates reusable projection patterns for later O subtracks.

Suggested commit message:

```text
host: migrate status presentation snapshots
```
