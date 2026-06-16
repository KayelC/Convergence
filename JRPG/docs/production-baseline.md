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

Track J may begin only while the two-project build, full suite, ordinary interactive startup, clean demos, parity ledger, and dataset assertions remain green. Full live battle sessions and exhaustive console traversal remain manual checks alongside the automated representative workflows.
