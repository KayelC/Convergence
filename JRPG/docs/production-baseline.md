# Production Baseline

## Status

This document defines the recovery baseline after Track 12. It exists to prevent architectural cleanup from removing working gameplay before an equivalent replacement is usable.

The detailed implementation sequence and parity gates are defined by the [Framework Parity Migration Plan](framework-parity-migration-plan.md).

Track A was characterized from commit `fce33a9` on `track-12-recovery`. Its executable parity ledger is `Convergence.Tests/Fixtures/Parity/recovery-baseline.json`, covering 35 protected capabilities.

## Current Boundary

The project currently contains:

- `JRPG.ConsoleHost`, a broad interactive console prototype backed by the legacy database and runtime models,
- `JRPG.Framework`, a reusable class library containing immutable definitions, strict deserialization, validation, dependency-aware catalogs, typed combat vocabulary, active effects, passive rules, item execution, battle orchestration, and fusion inheritance planning,
- deterministic clean battle and field demonstrations used as technical smoke tests.

The clean path is a framework foundation. It is not yet a feature-complete replacement for the interactive prototype.

## Measured Recovery Baseline

- 470 tests pass with 0 skipped tests, including 22 Track A baseline and workflow cases.
- A nonincremental build completes with 122 warnings and 0 errors. Track A did not increase the warning count.
- The clean battle demo ends in `Victory` for `player_team`.
- The clean field demo completes recovery, cure, revival, battle escape, and dungeon-exit request flows without input.
- Ordinary interactive startup remains executable and reaches scenario selection, field navigation, and session exit through scripted characterization.

The parity ledger records the protected owner, evidence, status, unresolved decisions, intended migration track, and possible future removal files for every capability. A listed removal file is evidence only and does not authorize deletion.

## Dataset Preservation Facts

The legacy data is intentionally unchanged. Track A records:

- 420 authored skills in 3 duplicate-name groups; the legacy dictionary exposes 417 unique names,
- 304 entities, 11 ailments, 14 items, 26 weapons, 3 armor records, 3 boots, and 3 accessories,
- 460 fusion recipes and 30 shop entries,
- 1 dungeon containing 6 blocks,
- 8 negotiation personalities, 40 questions, and 8 familiar-dialogue sets.

Known unresolved findings are 56 base-skill references, 120 learned-skill references, 1 casing-only skill reference mismatch, and 1 dungeon enemy-pool reference. Dungeon boss references, shop references, and fusion operands are otherwise resolved under the preserved legacy rules.

These findings characterize the current datasets. They neither approve the old schemas nor silently correct their anomalies.

## Track B Boundary

Track B began from `d97b244` and established a one-way assembly dependency: `JRPG.ConsoleHost` references `JRPG.Framework`; the framework never references the host.

- The framework builds independently with 0 warnings and has no external package dependency.
- The complete suite contains 479 passing tests with 0 skipped tests.
- The complete nonincremental solution build retains the existing 122 warnings, all in the console-host/legacy boundary.
- Root `dotnet run` remains the interactive executable path.
- The battle demo still ends in `Victory` for `player_team`; the field demo still completes all seven ordered events.
- Async content, command, event, and random contracts are available for future Godot and other hosts.
- The legacy interactive workflow remains on `IGameIO`; no capability is marked `clean_parity` or consumer-migrated merely because its clean code moved assemblies.

## Track C Boundary

Track C began from `46e9634` and completed the clean content catalog surface for every retained legacy JSON family without migrating runtime consumers.

- `GameDataCatalog` now has immutable repositories for equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets in addition to skills, entities, races, ailments, and items.
- The new definitions use strict `System.Text.Json` DTOs, validator-backed registrations, deterministic catalog qualification, and direct-dependency reference checks.
- `convergence.catalog_surface_sample` `0.1.0` provides one compact fixture pack: four equipment records, one shop, one negotiation set, one encounter, one dungeon, one fusion recipe, and eight ruleset policy records.
- The parity ledger marks affected content capabilities as `clean_foundation`, not `clean_parity`; no consumer-migration flag or removal authorization changed.
- Legacy datasets, `Database.LoadData`, ordinary interactive startup, and existing console workflows remain the playable source of truth until their later migration tracks.
- Three Track C catalog-surface tests bring the suite to 482 passing tests with 0 skipped tests. The nonincremental build remains at the existing 122 warnings.

## Track D Boundary

Track D began from `68175c8` and added the framework runtime-state foundation without migrating the interactive console consumers.

- `JRPGPrototype.Logic.Runtime` now owns stable runtime instance IDs, actor identity/display snapshots, controller/team/owner links, deployment state, progression, resource pools, stat blocks, skill loadouts, active form and stock references, equipment slots, battle statuses, analysis, passive activation counts, and transaction-safe mutation results.
- `RuntimeActorSnapshot` is the aggregate save/presentation/replay boundary. It references content by qualified `ContentId` and does not duplicate content definitions.
- Runtime state remains composed from focused snapshot records; it is not a new universal `Combatant` replacement.
- A narrow `RuntimeResourceTransactionService` proves before/after mutation reporting and rejection without partial state changes.
- The parity ledger now marks actor model, growth/progression state, stat/equipment state, active/reserve party state, and persona/demon stock state as `clean_foundation`. No consumer is marked migrated and no removal is authorized.
- Ten Track D runtime-state tests bring the suite to 492 passing tests with 0 skipped tests. The nonincremental build remains at the existing 122 warnings.
- The clean battle demo still ends in `Victory` for `player_team`; the clean field demo still completes all seven ordered events.

## Track E Boundary

Track E began from `fab0ba5` and moved legacy stat, resource, EXP, level-growth, Persona-growth, stat-allocation, and rollback rules into framework progression services without changing the content schema or removing console gameplay code.

- `JRPGPrototype.Logic.Runtime` now owns `IStatResolutionPolicy`, `IResourceGrowthPolicy`, `IExperienceCurve`, `ILevelGrowthPolicy`, `IStatAllocationService`, standard stat/resource/actor-kind IDs, modifier-track aliases, and immutable request/result records for progression operations.
- `RuntimeActorSnapshot` now has typed base-resource values so legacy `BaseHP` and `BaseSP` have a framework snapshot home.
- `Entities/Components/LegacyProgressionAdapter.cs` adapts the current `Combatant`, `Persona`, accessory, buff, and random-source shapes into the framework services.
- `StatProcessor`, `GrowthProcessor`, and `Persona` growth methods are now thin compatibility facades over the framework policies.
- Preserved formulas include Persona contribution weights, the raw stat cap of 40 before stage multipliers, 1.4/0.6 buff and debuff multipliers, `(int)(1.5 * level^3)` EXP requirements, HP/SP caps of 666/333, level-up resource delta healing, ordinary recalculation capping, humanoid base-resource rolls, and Persona random stat growth capped at 40.
- The parity ledger marks stat composition, growth/levels, and resource recalculation as `parallel_partial` with migrated console consumers. Removal remains unauthorized because the live `Combatant`, `Persona`, DTO, factory, UI, and save/persistence ownership are still legacy.
- Track E adds 37 focused progression and adapter tests, bringing the suite to 529 passing tests with 0 skipped tests. The nonincremental build remains at the existing 122 warnings.
- The clean battle demo still ends in `Victory` for `player_team`; the clean field demo still completes all seven ordered events.

## Track F Boundary

Track F began from `e84ba29` and moved party, Persona stock, demon stock, active Persona swap, and fusion inventory consume/replace rules into framework transition services without changing menus, datasets, fusion formulas, or live actor persistence.

- `JRPGPrototype.Logic.Runtime` now owns immutable party/stock snapshots, stock capacity policy, typed transition requests, stable result codes, diagnostics, affected runtime IDs, and `IPartyStockTransitionService`.
- `Logic/Core/LegacyRuntimeIdentityRegistry.cs` assigns adapter-owned per-session `RuntimeInstanceId`s for live `Combatant` and `Persona` references.
- `Logic/Core/LegacyPartyStockAdapter.cs` builds framework snapshots, executes transitions, and applies successful results back to existing console lists and properties.
- `PartyManager` remains the public console API but now delegates add, reserve swap, summon, active demon swap, return, dismiss, replace, and capacity checks to the framework-backed adapter.
- Battle and field Persona swap paths use the same active-form stock transition while preserving existing messages, resource recalculation, and current HP/SP capping.
- `FusionInventoryTransaction` delegates consume/replace stock operations through the adapter while leaving fusion planning, inheritance, accidents, UI, and economy behavior unchanged.
- Preserved invariants include active party size four, stock capacity 3/5/7/10/12, active demons remaining owned in `DemonStock`, returned demons staying owned, dismissed/consumed demons leaving both active party and stock, and Persona stock exchange on swap.
- The parity ledger marks active/reserve party, Persona/demon stock, and party operations as `parallel_partial` with migrated console consumers. Removal remains unauthorized because `Combatant`, `Persona`, UI bridges, compendium, factories, and persistence ownership are still legacy.
- Track F adds 27 focused party/stock transition and adapter tests, bringing the suite to 556 passing tests with 0 skipped tests. The nonincremental build reports 120 warnings.
- The clean battle demo still ends in `Victory` for `player_team`; the clean field demo still completes all seven ordered events.

## Track G Boundary

Track G began from `d053ef0` and moved production combat formulas into framework policies while preserving the interactive console battle path.

- `JRPG.Framework/Logic/Battle/ProductionCombatRuleset.cs` now owns named production defaults for physical/magical damage, hit and evasion, critical chance, instant-death success, reflected damage inputs, initiative, EXP yield, Macca yield, Weak/Resist multipliers, drain recovery values, guard effects, rigid-body effects, charge, and variance.
- `Logic/Battle/LegacyCombatPolicyAdapter.cs` translates live `Combatant`/`Persona` state, legacy passives, ailments, shields, breaks, and affinities into framework policy requests.
- `CombatMath` and `DamageHandler` remain the compatibility APIs used by `DamageEffect`, `BehaviorEngine`, `BattleConductor`, and existing tests, but their rule work now delegates through the adapter.
- The production order is recorded in gameplay docs: target validity, hit, shield, Break/override/passive affinity, Null/Repel/Absorb, critical/rigid body, guard, damage modifiers/charge/drain, defeat interception, knowledge, and Press Turn.
- The parity ledger marks combat math and battle rewards as `parallel_partial` with migrated console consumers. Removal remains unauthorized because action selection, skill effects, AI, battle conductor ownership, content migration, and ruleset JSON binding are still later tracks.
- Track G adds 20 focused production-combat policy test cases, bringing the suite to 576 passing tests with 0 skipped tests. The nonincremental build reports 120 warnings.
- The clean battle demo still ends in `Victory` for `player_team`; the clean field demo still completes all seven ordered events.

## Track H Boundary

Track H began from `a9c79a4` and added a framework-owned action execution facade without replacing the interactive battle state machine.

- `JRPG.Framework/Logic/Battle/Execution/BattleActionExecutor.cs` now owns typed action commands for basic attacks, skills, items, guard, pass, analyze, escape, Persona swap, demon summon/return/swap, tactics change, negotiation, and host-special requests.
- Assessment and execution share the same target, cost, item availability, effect, Press Turn, and party-stock transition services.
- Item use now has a host-owned reservation port. The framework commits the reservation only when execution returns `ConsumeOne`; rejected, unavailable, no-effect, skipped, failed, or cancelled actions do not commit quantity changes.
- `Host/CleanFieldDemoHost.cs` now executes clean field skills and items through the action facade with an explicit inventory adapter.
- `ActionProcessor` and `BattleConductor` preserve existing visible console behavior while routing guard/pass coordination through a console-owned compatibility adapter.
- Tactics and negotiation are represented as host-mediated action commands only. Full AI, tactics policy, negotiation/recruitment, and battle orchestration remain Tracks J and K.
- The parity ledger marks battle actions, typed effects, inventory quantities, and field items/skills as `parallel_partial`; removal remains unauthorized.
- Focused Track H action/shared-effect tests passed: 27 tests, 0 failed, 0 skipped.
- Full verification passed: 585 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events through the action facade.

## Track I Boundary

Track I began from `f7dbf08` and moved strict-parity status lifecycle rules into framework services while leaving legacy status data and console battle orchestration intact.

- `JRPG.Framework/Logic/Battle/Execution/BattleStatusLifecycle.cs` now owns clean ailment application, turn-start restrictions, turn-end status effects, natural recovery, duration ticking, cleanup scopes, and battle-start or turn-end passive dispatch.
- `BattleActorState` now has duration helpers for ailments, stat stages, charges, shields, affinity overrides, transient cleanup, and encounter cleanup.
- `Logic/Battle/Engines/LegacyStatusLifecycleAdapter.cs` adapts live `Combatant` state into the framework lifecycle and copies results back for the existing `StatusRegistry` callers.
- `Data/Jsons/status_lifecycle_demo.*.json` adds a clean 11-ailment content pack for Poison, Freeze, Shock, Fear, Panic, Charm, Rage, Distress, Sleep, Bind, and Stun. The legacy `Data/Jsons/status_ailments.json` file remains unchanged.
- The preserved parity rules include one active major ailment, lethal 13% max-HP Poison, 10% HP/SP Sleep recovery, `20 + Luck / 2` natural recovery, Panic/Fear chances, Guard blocking ailment application, reserve suspension, `-4..+4` stat-stage caps, and legacy `+3/-3` redundancy thresholds.
- Ailment evasion modifiers now accept zero in validation because the legacy immobilization behaviours need explicit zero-evasion content.
- The parity ledger marks ailment lifecycle and passive lifecycle as `parallel_partial` with migrated console consumers. Removal remains unauthorized because legacy content, cure parsing, redundancy checks, full battle orchestration, and production skill/item reauthoring remain later tracks.
- Focused Track I lifecycle/status tests passed: 37 tests, 0 failed, 0 skipped.
- Full verification passed: 592 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.
- `git diff --check` passed. `Data/Jsons` has no Track J changes. The new Track J runtime files contain no console, filesystem, Godot, Newtonsoft, legacy database, DTO, `IGameIO`, `Combatant`, or `Persona` references.

## Track J Boundary

Track J began from `aa82101` and moved battle orchestration into the framework while keeping legacy battle actions, actors, content, negotiation, and rewards in the console host.

- `JRPG.Framework/Logic/Battle/Runtime/BattleEncounterRunner.cs` now owns initiative, battle-start lifecycle, team phases, actor turns, turn-start lifecycle, command orchestration, Press Turn consumption, turn-end lifecycle, phase-end cleanup, deployment refresh, completion checks, cancellation, typed faults, and ordered battle events.
- `AutomatedBattleRunner` now delegates to the encounter runner underneath while preserving the existing clean battle demo API and output shape.
- `BattleConductor.StartBattle()` now routes ordinary console battles through `BattleEncounterRunner` using a console-owned legacy adapter.
- `InteractionBridge`, `ActionProcessor`, `BehaviorEngine`, `NegotiationEngine`, `BattleKnowledge`, rewards, datasets, message rendering, and live `Combatant` state remain console-owned compatibility boundaries.
- `BehaviorEngine` gained a parity seam so framework orchestration can pass an already-computed turn-start result instead of rolling status lifecycle twice.
- The parity ledger marks Press Turn orchestration as `parallel_partial` with a migrated console consumer, and keeps battle actions, AI/tactics, battle knowledge, negotiation, and rewards as partial or later-track work. Removal remains unauthorized for every legacy file.
- Focused Track J framework and console-route tests passed: 14 tests, 0 failed, 0 skipped.
- Full verification passed: 606 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.

## Track K Boundary

Track K began from `c3f3039` and moved negotiation session rules, recruitment validation, and battle reward calculation into framework services while preserving legacy content files and console-facing behavior.

- `JRPG.Framework/Logic/Battle/Runtime/BattleNegotiationAndRewards.cs` now owns typed negotiation prompts, answer scoring, moon/stock/ownership gates, familiar gifts, demand outcomes, recruitment transaction validation, and immutable battle reward results.
- `NegotiationEngine` remains the console compatibility adapter. It maps `questions.json`, inventory, economy, moon phase, and live party state into framework requests, then applies only the returned Macca/item/familiar-gift mutations.
- `BattleConductor` now uses `LegacyRecruitmentAdapter` for stock/compendium/enemy-list mutation after a successful negotiation and `LegacyBattleRewardAdapter` for victory EXP/Macca application.
- No production data was reauthored. `Data/Jsons` remains unchanged, and removal stays unauthorized for `NegotiationEngine`, `CombatMath`, `BattleConductor`, `Data/NegotiationData.cs`, and the legacy JSON datasets.
- The parity ledger marks negotiation/recruitment and battle rewards as `parallel_partial` with migrated console consumers. Remaining work is clean negotiation content, authored ruleset binding, and later host/content migration.
- Focused Track K framework and console-route tests passed: 13 tests, 0 failed, 0 skipped. Boundary-focused runtime API checks also pass.
- Full verification passed: 610 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 119 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.

## Migration Rule

No working subsystem is removed merely because a cleaner API exists.

A legacy subsystem may be retired only when all of the following are true:

1. Its required player-facing behavior is listed and approved.
2. The clean replacement implements that behavior.
3. Automated tests cover the replacement and important legacy regressions.
4. A real host consumes the clean replacement.
5. The interactive prototype or its successor remains usable after migration.
6. Data required by the replacement has an approved schema and authored fixtures.
7. Removal is reviewed as a dedicated change rather than bundled into unrelated work.

## Production Sequence

Future work should migrate one vertical slice at a time:

```text
characterize existing behavior
  -> approve intended rules
  -> implement clean replacement
  -> connect a real host consumer
  -> verify functional parity
  -> retire only the replaced code
```

Battle, field exploration, party management, inventory, shops, negotiation, growth, fusion, dungeons, and persistence each require their own parity decision.

## Documentation Rule

- The [Skill System GDD](skill-system-gdd.md) remains normative for approved skill-system decisions.
- Source and tests define what Track 12 currently implements.
- Historical plans and discarded proposals are stored in [ArchiveDocs](../ArchiveDocs/README.md).
- New proposals should begin as focused discussion documents and become active contracts only after approval.

## Branch Safety

The completed `skill-system-redesign` branch remains an architectural reference. It must not replace the playable line merely because its internal contracts are cleaner. Work on this recovery branch should preserve the interactive prototype until a deliberate successor exists.

Track L may begin only while the two-project build, full suite, ordinary interactive startup, clean demos, parity ledger, and dataset assertions remain green. Full live battle sessions and exhaustive console traversal remain manual checks alongside the automated representative workflows.

## Track L Boundary

Track L began from `51ab35c` and moved persistent resource-management transaction rules into the framework while preserving the interactive console host and legacy data ownership.

- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs` now owns immutable inventory snapshots, item reservations, unique equipment-ID ownership, equipment slot checks, Macca wallet transactions, Luck-based shop pricing, shop buy/sell transactions, and hospital restoration results.
- `LegacyInventoryResourceAdapter` maps legacy numeric IDs, `InventoryManager`, `EconomyManager`, live equipment, and hospital patients into framework snapshots, then applies only successful transaction results back to console-owned objects.
- `InventoryManager`, `EconomyManager`, `ShopEngine`, `FieldServiceEngine.PerformEquip`, `FieldServiceEngine.ExecuteItemUsage`, and `FieldServiceEngine.TryRestoreCombatant` now route migrated mutation decisions through the adapter.
- Legacy `Data/Jsons`, `Database`, DTOs, shop inspection metadata repair, UI bridges, field item effect parsing, and visible menu behavior remain console-host owned. Removal remains unauthorized.
- Preserved formulas: buy price is `(int)(basePrice * max(0.5, 1.0 - Luck * 0.01))`, sell price is `(int)(basePrice * (0.50 + Luck * 0.01))`, missing sell metadata falls back to `100`, and hospital cost is `missing HP + missing SP * 5`.
- Track L intentionally keeps unique equipment IDs rather than per-copy equipment instances. Duplicate equipment purchases and equipped-item sales are rejected before mutation.
- Focused Track L checks passed: 7 framework resource-management tests, the 12-test legacy workflow characterization suite, and the parity-ledger validation test all passed.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 620 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 119 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O3 Boundary

Track O3 began from `53fbf40` and migrates field inventory/effect presentation into typed console-host results without changing production data or gameplay rules.

- `FieldActionResults` now records field selection outcomes, field-use assessment, execution reasons, item-consumption decisions, and ordered presentation events.
- `InventoryUIBridge` returns typed item, skill performer, field skill, and target selection results while preserving existing wrapper methods, menu text, disabled labels, hover descriptions, and cancellation behavior.
- `FieldServiceEngine` now has shared assessment and detailed execution paths for field item and field skill use. Item consumption still goes through `LegacyInventoryResourceAdapter`; successful effects and Goho-M consume once, and no-effect or unavailable paths do not consume.
- Legacy `ItemData`, `SkillData`, `Database`, effect-string parsing, and production `Data/Jsons` remain console-host compatibility concerns.
- Focused O3 verification passed: field inventory presentation tests, plain menu command tests, and the representative field/shop characterization test reported 11 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 647 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 105 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O4 Boundary

Track O4 began from `c7d7c40` and migrates field party/stock organization presentation into typed console-host results without changing party rules, production data, fusion transactions, or battle COMP behavior.

- `PartyStockPresentationResults` records organize-slot selections, Persona stock selections/actions, demon stock selections, summon-target selections, and mutation presentation results.
- `StatusUIBridge` now exposes typed result methods for Persona stock, demon stock, organization slots, Persona action selection, and summon/replace targets. Existing wrapper methods and legacy null/string/object return behavior remain available for current callers.
- `LegacyPartyStockAdapter` exposes detailed Track F transition results for summon, return, swap, dismiss, and Persona swap while preserving the existing bool-returning `PartyManager` methods.
- `FieldServiceEngine` now returns typed mutation presentation results for field-side Persona swaps and demon summon/return/swap/dismiss paths. Results carry transition codes, affected runtime IDs, and ordered presentation events while preserving legacy messages, transient cleanup, active plus owned demon overlap, and Persona HP/SP capping.
- `FieldConductor` consumes the typed party/stock bridge results for field organization, demon stock, and Persona stock flows.
- Legacy `Combatant`, `Persona`, `PartyManager`, live stock lists, field menu text, status-peek behavior, production `Data/Jsons`, fusion stock transactions, and battle COMP flow remain protected. Removal remains unauthorized.
- Focused O4 verification passed: `PartyStockPresentationTests`, `PartyStockAdapterTests`, `PartyManagerTests`, and `StatusPresentationProjectionTests` reported 27 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 654 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O5 Boundary

Track O5 began from `f671491` and migrates player battle command selection into a typed console-host shell without changing legacy battle execution.

- `BattleCommandShellResult` records selected/back/unavailable command outcomes, payload kind, framework command, optional assessment, expected turn-consumption intent, and the legacy payload required by current executors.
- `LegacyBattleCommandShellAdapter` maps live `Combatant`/`Persona` selections into framework commands. Legacy attack, skill, item, escape, tactics, and negotiation remain host-mediated; guard, pass, analyze, Persona swap, demon summon, demon return, and demon swap use concrete framework commands.
- `BattleConductor` now asks the shell for a command before performing the existing legacy action. Framework-backed stock and Persona commands use assessment before mutation; legacy `ActionProcessor`, `PartyManager`, `NegotiationEngine`, AI behavior, Press Turn outcomes, rewards, recruitment, and battle narration remain protected.
- Production `Data/Jsons`, `SkillData`, `ItemData`, clean skill/item execution, and content schemas were not changed. Removal remains unauthorized.
- Focused O5 verification passed: `BattleCommandShellTests`, `BattleBridgeResultTests`, and `ActionProcessorResultTests` reported 53 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 664 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O6 Boundary

Track O6 began from `3dae5ae` and migrates battle event presentation into a typed console-host event sink without changing visible battle narration.

- `BattleEventPresentationResult` records `Shown`, `Suppressed`, and `HostOwned` outcomes for framework battle events.
- `LegacyBattleEventPresentationAdapter` consumes `BattleEncounterEvent` streams from `BattleEncounterRunner` through `BattleEncounterServices`.
- Generic structural events are consumed but suppressed, preserving the existing console output policy. Lifecycle-shell messages for skip, fear flee, return-to-COMP, enemy flee, and demon defeat return now flow through typed presentation results.
- `BattleConductor`, `ActionProcessor`, `StatusRegistry`, `BehaviorEngine`, `NegotiationEngine`, reward/recruitment adapters, production `Data/Jsons`, and clean skill/item execution remain protected. Removal remains unauthorized.
- Focused O6 verification passed: `BattleEventPresentationTests`, `BattleCommandShellTests`, `BattleBridgeResultTests`, `ActionProcessorResultTests`, `BattleEncounterRunnerTests`, and the ordinary battle routing characterization reported 74 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 671 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O7 Boundary

Track O7 began from `01ddcc4` and migrates negotiation/recruitment/reward presentation into typed console-host results without changing battle outcomes or legacy data.

- `NegotiationRewardPresentationResults` records answer prompts, demand prompts, negotiation events, session outcomes, mutation summaries, recruitment presentation, and battle reward presentation.
- `NegotiationEngine` keeps `StartNegotiation` as the legacy wrapper and adds an internal detailed result around `NegotiationSessionService`.
- `BattleConductor` uses one negotiation/recruitment presentation helper for both framework encounter execution and older compatibility execution.
- `LegacyBattleRewardAdapter` now presents immutable reward totals through the existing victory reward message before applying rewards.
- Legacy `questions.json`, negotiation data, reward formulas, recruitment rules, compendium recall, Cathedral presentation, production `Data/Jsons`, and framework public APIs remain protected. Removal remains unauthorized.
- Focused O7 verification passed: `NegotiationRewardPresentationTests`, `NegotiationRewardRuntimeTests`, `BattleCommandShellTests`, `BattleEventPresentationTests`, and the negotiation/ordinary battle characterizations reported 40 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 688 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O8 Boundary

Track O8 began from `713d3cb` and migrates shop/hospital presentation into typed console-host results without changing shop data, pricing, hospital formulas, or visible menu behavior.

- `ShopHospitalPresentationResults` records shop command selection, buy/sell offer selection, confirmation, inspection, transaction display, hospital patient selection, and hospital treatment presentation.
- `ShopUIBridge` keeps `OpenShop` as the legacy wrapper and routes command, offer, confirmation, and inspection surfaces through typed results.
- `ShopEngine` keeps `ExecutePurchase` and `ExecuteSale` compatible while exposing detailed transaction presentation backed by framework `ShopTransactionResult`.
- `ServiceUIBridge`, `FieldServiceEngine`, and `FieldConductor` now route hospital selection and treatment display through typed results backed by framework `HospitalRestorationResult`.
- Legacy `shop_inventory.json`, item/equipment metadata repair, Luck pricing, equipped sale blocking, hospital HP/SP eligibility labels, zero-cost ailment-only engine treatment, production `Data/Jsons`, and framework public APIs remain protected. Removal remains unauthorized.
- Focused O8 verification passed: `ShopHospitalPresentationTests`, `ResourceManagementServiceTests`, and the shop/hospital/menu characterizations reported 17 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 694 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O9 Boundary

Track O9 began from `60858c6` and migrates dungeon traversal presentation into typed console-host results without changing dungeon data, traversal rules, encounter hydration, boss battle handoff, or visible menu behavior.

- `DungeonTraversalPresentationResults` records floor action selections, entry/terminal floor selections, detailed transition presentation, floor-entry presentation, and mapped runtime events.
- `DungeonManager` keeps its legacy public surface but now exposes detailed framework-backed transition results for floor processing, movement, terminal warp, return-to-city, Goho-M/explicit dungeon exit, barrier interaction, and boss-defeat registration.
- `DungeonUIBridge` maps framework dungeon events into existing visible messages or suppressed records. Visible preserved messages include movement, safe room calm, boss alert, sealed barrier, and guardian defeat.
- `ExplorationProcessor` and `FieldConductor` now consume detailed transition/floor-entry results while keeping `CombatantFactory` enemy hydration, duplicate suffix naming, battle construction, boss battle flags, and legacy live-object state host-owned.
- Legacy `tartarus.json`, `Database.Dungeons`, framework public APIs, production content, and `Data/Jsons` remain unchanged. Removal remains unauthorized.
- Focused O9 verification passed: `DungeonTraversalPresentationTests`, `FieldDungeonStateMachineTests`, the dungeon workflow characterizations, and dungeon/target menu command tests reported 14 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 699 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O10 Boundary

Track O10 began from `4ed67ca` and migrates Cathedral fusion and Compendium presentation into typed console-host results without changing fusion data, ritual rules, recall pricing, stock/economy mutation, or visible Cathedral behavior.

- `FusionCompendiumPresentationResults` records Cathedral menu selections, ritual participant selections, inheritance rows, ritual confirmation, ritual sequence events, fusion transaction outcomes, Compendium recall selections, registration results, and recall transaction outcomes.
- `CathedralUIBridge` keeps the legacy wrapper methods while exposing detailed typed methods for O10 tests and future host adapters.
- `FusionConductor` consumes detailed presentation results while preserving the existing binary/sacrificial ritual loops, inheritance wait/cancel flow, accident reveal timing, and live-object transaction path.
- `FusionPlan` carries framework inheritance display entries as presentation evidence; the legacy pickable/exclusive display lists remain unchanged.
- `FusionMutator` and `CompendiumRegistry` now expose detailed transaction/registration/recall results while preserving their public wrappers, messages, deep snapshot behavior, and mutation authority.
- Legacy `fusion_table.json`, production `Data/Jsons`, fusion strategies, accident/mutation odds, recall pricing, framework public APIs, and removal authorization remain unchanged.
- Focused O10 verification passed: `FusionCompendiumPresentationTests`, `FusionBridgeResultTests`, `FusionCompendiumRuntimeTests`, `FusionBugRegressionTests`, and `FusionInheritanceTests` reported 63 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 706 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O2 Boundary

Track O2 began from `a9f2f87` and migrates read-only status presentation deeper into the console-host/framework adapter boundary.

- `LegacyStatusPresentationProjection` now backs Human summaries, Persona details, demon details, stock labels, organization labels, summon labels, and equipment slot labels with framework runtime snapshots plus copied display-only legacy data.
- `StatusUIBridge` keeps its existing public methods, menu text, option order, cancellation behavior, status-peek flow, and legacy return values.
- Stat allocation mutation, equipment mutation, Persona/demon stock mutation, rich battle presentation, Cathedral presentation, and production content authority remain unchanged.
- Legacy `Data/Jsons`, `Database`, live `Combatant`/`Persona` state, and all removal gates remain protected.
- Focused O2 verification passed: status projection tests, plain menu command tests, and the status/equipment surface characterization test reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 640 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 113 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track M Boundary

Track M began from `1502970` and moved field/dungeon state-machine rules into the framework while preserving the interactive console host, legacy dungeon JSON, and visible menu behavior.

- `JRPG.Framework/Logic/Runtime/FieldDungeonStateMachines.cs` now owns immutable dungeon progress/content/floor snapshots, movement and warp transitions, terminal unlocks, boss defeat state, barrier results, dungeon exits, game-over recovery transitions, random encounter selection, and ordered runtime events.
- `LegacyDungeonContentAdapter` maps `Database.Dungeons` and `tartarus.json` into framework snapshots using the same reversible `legacy_<hex>` ID strategy as the resource adapters.
- `DungeonManager` remains the public console facade for existing callers. It delegates floor processing and transitions to the framework service, then writes successful results back to `DungeonState`.
- `FieldConductor` routes dungeon entry, terminal return, explicit dungeon exit, return-to-city, and boss-defeat registration through the framework-backed manager. `ExplorationProcessor` still owns host messages, battle handoff, `CombatantFactory` hydration, and duplicate enemy display suffixes.
- Legacy `Data/Jsons`, `Database`, DTOs, `BattleConductor`, `FieldServiceEngine`, fusion entry, and menu bridges remain console-host owned. Removal remains unauthorized.
- Focused Track M checks passed: 5 framework field/dungeon state-machine tests, the 14-test legacy workflow characterization suite, and the parity-ledger validation test all passed.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 627 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 118 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track N Boundary

Track N began from `ec7d4fa` and moved fusion result resolution, inheritance planning, transaction assessment, and Compendium rule checks into the framework while preserving the interactive Cathedral, legacy fusion datasets, and visible ritual flow.

- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs` now owns recipe lookup, result operation selection, moon-phase accident checks, rank operations, Mitama stat-boost decisions, inheritance slot calculation, mutation selection, preview snapshots, transaction assessment, and immutable Compendium registration/recall assessment.
- `LegacyFusionContentAdapter` maps `Database.FusionRecipes`, `Database.Personas`, `Database.Skills`, live `Combatant`, and live `Persona` state into framework snapshots without reauthoring `fusion_table.json`, entity data, or skill data.
- `FusionCalculator`, duplicate-result guards, and `CompendiumRegistry` now delegate migrated rule decisions through framework services. `FusionConductor`, `CathedralUIBridge`, `FusionMutator`, fusion strategies, economy mutation, stock mutation, prompts, waits, and visible Cathedral text remain console-host owned compatibility surfaces.
- Compendium registration now stores deep immutable snapshots, including active Persona data, instead of sharing live references. Recall pricing remains the legacy base-price fallback plus level, stat, and skill-count formula.
- Legacy `Data/Jsons`, DTOs, live actor models, Cathedral menus, and fusion transaction strategy classes remain present. Removal remains unauthorized.
- Focused Track N checks passed: 4 framework fusion/Compendium runtime tests plus the Compendium snapshot characterization test passed; the 52-test fusion regression, bridge-result, and inheritance suite passed.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 631 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 115 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O1 Boundary

Track O1 began from `e163b81` and starts the interactive console-host migration while preserving the ordinary scenario startup, legacy datasets, and visible menu behavior.

- `ConsoleGameHost` now creates an `InteractiveConsoleHostContext` after `Database.LoadData`. The context loads retained clean packs as a nonfatal sidecar catalog and publishes clean-catalog diagnostics through a host event sink.
- `ServiceUIBridge`, `InventoryUIBridge`, `StatusUIBridge`, and `DungeonUIBridge` route plain menus through framework host-command adapters while still returning the legacy strings or objects expected by existing conductors.
- Human status rendering now uses a serializer-neutral runtime snapshot projection before producing the unchanged console text.
- The sidecar catalog is readiness evidence only. Legacy `Data/Jsons`, `Database`, live `Combatant`/`Persona` objects, rich preview menus, battle commands, Cathedral prompts, shop presentation, and gameplay rule consumers remain console-host owned. Removal remains unauthorized.
- Focused Track O1 checks passed: the startup sidecar tests, plain-menu command tests, framework host-adapter tests, parity-ledger validation test, and ordinary startup characterization test reported 13 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 636 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 113 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track P Boundary

Track P began from `c3883e5` and proved Godot-style integration through test-only adapters without adding a Godot project or dependency.

- `docs/godot-integration-contract.md` now records the host boundary: Godot owns resources, nodes, scenes, input, presentation, asset IDs, scheduling, save-file format, and scene-instance handles.
- The framework remains responsible for content loading from host-supplied text, validation, catalogs, runtime rules, actor creation, transitions, ordered events, diagnostics, and serializer-neutral snapshots.
- `GodotIntegrationContractTests` load retained clean packs from fake `res://` paths, build a catalog, create actors, run deterministic clean battle execution, consume ordered events, map instance IDs to host-owned scene handles, and restore actor plus field/dungeon snapshots from a host-owned save envelope.
- No framework public API, gameplay rule, production JSON file, parity-ledger status, or removal authorization changed.
- Focused Track P checks passed: `GodotIntegrationContractTests` and `FrameworkBoundaryTests` reported 5 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 708 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Packaging verification passed after a Release framework build: `dotnet pack JRPG.Framework/JRPG.Framework.csproj --no-build` created the framework package and emitted only the NuGet readme advisory.
- Demo verification passed: the clean battle demo ended in player-team victory, and the clean field demo completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no Godot/console/filesystem/Newtonsoft/legacy public-type leakage, and `Data/Jsons` had no modified files.

## Track Q Boundary

Track Q1 began as a production-content audit and planning pass. Q2 amends that plan so the legacy datasets are treated as prototype-only evidence, not a conversion queue.

- `docs/q-track-plan.md` now defines the Track Q original-content policy and legacy dataset boundary.
- `Convergence.Tests/Fixtures/ProductionContent/production-content-ledger.json` records every protected legacy content family, every clean schema family, future Q subtracks, required reports, manual-decision buckets, current record counts, and removal gates.
- Old `skills_database_v2.json`, `entity_database_v2.json`, and `ArchiveDocs/Planning/migration_report.md` are historical evidence only. They are not authoritative production conversion output.
- Production `Data/Jsons` remains unchanged, no gameplay consumer switches to clean production content, and removal remains unauthorized.
- Legacy `Data/Jsons` records are prototype-only and not approved as commercial/shippable framework content.
- Direct conversion from legacy data into clean production packs is paused. Future clean production content must be original authored content with its own schema validation, runtime coverage, and consumer switch.
- Ledger coverage: 10 production families, 12 protected legacy content files, 12 clean schema families, 7 mandatory report types, 4 manual-decision buckets, and 3 historical-only migration artifacts.
- Known unresolved-reference findings remain recorded: 56 unresolved base-skill references, 120 unresolved learned-skill references, 1 casing-only skill reference, 1 unresolved dungeon enemy-pool reference, 0 unresolved dungeon boss references, 0 unresolved shop references, and 0 invalid fusion operands.
- Focused Q1 verification passed: `ProductionContentLedgerTests`, `RecoveryParityLedgerTests`, and `RecoveryDatasetBaselineTests` reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 713 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: the clean battle demo ended in player-team victory, and the clean field demo completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no Godot/console/filesystem/Newtonsoft/legacy DTO/static database leaks, and `Data/Jsons` had no modified files.
- Q2 verification passed with the same 8 focused checks, 713-test full suite, 98-warning nonincremental build, successful clean battle/field demos, clean whitespace and framework boundary checks, and no `Data/Jsons` modifications.

## Track R Boundary

Track R adds framework-owned save/checkpoint contracts and a host-owned JSON proof demo. It does not add interactive save menus, switch production consumers to clean content authority, convert legacy datasets, or authorize removal.

- `JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs` now owns `RuntimeSaveGameSnapshot` contract version `1`, typed knowledge snapshots, session progress, checkpoint logs, save validation diagnostics, and `IRuntimeSaveValidator`.
- Save snapshots aggregate actors, party/stock, inventory, equipped items, wallet, field/dungeon progress, Compendium entries, battle knowledge, session counters/flags, optional host context, and ordered checkpoint breadcrumbs.
- Validation uses a `GameDataCatalog` to check duplicate runtime IDs, missing actor references, active-form references, catalog content references, inventory/equipment IDs, dungeon IDs, Compendium species/skills, knowledge targets, and checkpoint ordering. Catalog definitions are never copied into the save.
- `Host/CleanSaveDemoHost.cs` adds `--clean-save-demo`, which loads retained clean packs, creates a representative snapshot, serializes/deserializes it with console-host-owned `System.Text.Json` DTOs, validates the restored snapshot, rebuilds actor runtime state, prints ordered proof events, and exits without input.
- The parity ledger records `persistence_snapshots` as `clean_foundation`. Legacy `Combatant`, `Persona`, `PartyManager`, `CompendiumRegistry`, `ConsoleGameHost`, and production JSON remain protected; `removalAuthorized` stays `false`.
- Focused Track R verification passed: 9 focused persistence/host/ledger checks passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 721 passed, 0 failed, 0 skipped; the nonincremental build passed with 98 warnings and 0 errors.
- Demo verification passed: clean battle ended in player-team victory, clean field effects completed successfully, and clean save restored 2 actors, 1 item stack, and dungeon floor 5.
- Quality gates passed: `git diff --check` reported no whitespace errors, Track R runtime contracts had no forbidden host/serializer/legacy references, and `Data/Jsons` had no modified files.
