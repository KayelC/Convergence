# Framework Parity Migration Plan

## Status

This is the active implementation plan for converting the Track 12 recovery baseline into a reusable framework without losing the playable systems that exist on `main`.

This plan supersedes the archived broad refactor and cleanup plans. It does not authorize immediate removal of legacy code. Its central rule is:

> A subsystem is migrated only when the framework implementation, authored data, automated tests, and a real host consumer reproduce its required behavior.

The current console application is the behavioral reference implementation. The clean catalog-backed path is the architectural foundation. Neither is sufficient by itself.

## Purpose

The project currently has two parallel paths:

1. A broad interactive console application containing battle, field, party, stock, inventory, equipment, shops, economy, negotiation, growth, dungeons, fusion, and compendium behavior.
2. A clean framework path containing immutable definitions, strict deserialization, validation, dependency-aware catalogs, typed effects, passives, items, combat vocabulary, automated battle orchestration, and fusion inheritance policy.

The goal is to merge their strengths deliberately:

```text
working console behavior
  + typed framework contracts
  + explicit host policies
  + complete content schemas
  + deterministic tests
  + real interactive host integration
  = reusable framework with no silent feature loss
```

## Non-Goals

This plan does not:

- delete the console prototype before a replacement host is usable,
- treat the deterministic clean demos as proof of full gameplay parity,
- migrate the old datasets before their target schemas and rules are approved,
- preserve obvious bugs merely because they exist,
- silently change ambiguous behavior during architectural work,
- require Godot types in the framework,
- put filesystem, console, serializer, or presentation dependencies into public framework contracts,
- complete every future game idea before the existing prototype reaches parity.

## Definitions

### Structural Parity

The framework can represent all required state and content used by the legacy subsystem.

### Rule Parity

For equivalent inputs and deterministic random outcomes, the framework produces the same approved gameplay result.

### Workflow Parity

A host can complete the same player-facing sequence, including cancellation, failure, confirmation, and transaction behavior.

### Presentation Parity

The framework emits enough structured information for a host to present every meaningful result. Exact console wording and formatting are not framework requirements.

### Data Parity

Every retained content record can be represented, validated, loaded, and used without relying on display-name or description inference.

### Functional Parity

Structural, rule, workflow, presentation, and data parity are all satisfied for the subsystem's approved scope.

## Current Baseline

The Track 12 recovery branch currently provides:

- the original executable console application and ordinary no-flag startup,
- the static legacy `Database` and complete legacy datasets,
- the original `Combatant`, `Persona`, party, inventory, growth, dungeon, battle, negotiation, fusion, and compendium systems,
- clean skill, entity, race, ailment, and item definitions,
- strict `System.Text.Json` deserialization,
- semantic validation and explicit host registrations,
- pack dependencies, qualified IDs, and immutable catalogs,
- clean combat defense vocabulary and separate knowledge stores,
- typed active skill effects and host-owned execution policies,
- passive triggers and rule modifiers,
- clean item and field-effect execution,
- clean actor hydration and automated battle demonstrations,
- typed fusion inheritance planning and selection validation,
- 448 passing tests, with existing nullable warnings in legacy DTOs and runtime code.

The clean path is not yet connected to ordinary console gameplay.

## Protected Legacy Inventory

The following capabilities must not disappear during migration.

| Capability | Current owner | Clean replacement status |
| --- | --- | --- |
| Interactive boot and scenario selection | `Program`, `ConsoleGameHost`, `ScenarioFactory` | Demo flags only; ordinary host not migrated |
| Human, Persona User, Wild Card, Operator, Demon models | `Combatant`, `Persona`, `ClassType` | Entity templates exist; class/runtime composition missing |
| Stats, equipment influence, Persona influence | `StatProcessor` | Generic base stats exist; class/equipment formulas missing |
| EXP, levels, random growth, stat allocation | `GrowthProcessor` | Missing |
| HP/SP initialization and recalculation | `GrowthProcessor` | Host demo policy only |
| Active party and reserve party | `PartyManager` | Missing |
| Persona stock and demon stock | `Combatant`, `PartyManager` | Missing |
| Summon, return, swap, dismiss, duplicate checks | `PartyManager` | Missing |
| Legacy battle math | `CombatMath` | Interfaces exist; production policy missing |
| Press Turn | `PressTurnEngine` | Framework encounter runner now orchestrates console Press Turn phases; engine remains shared compatibility |
| Basic attack, guard, pass, skill, item, analyze, swap | `ActionProcessor`, `BattleConductor` | Action facade exists; legacy skill/item/effect semantics remain host-owned |
| Typed damage, recovery, cures, buffs, shields | Legacy effects and clean executors | Clean foundation exists; parity incomplete |
| Ailment restrictions and lifecycle | `StatusRegistry` | Framework lifecycle service with console adapter; production content migration incomplete |
| Passive startup and turn-end behavior | `StatusRegistry` | Framework lifecycle dispatch for startup/turn-end paths; production content migration incomplete |
| Enemy AI and tactics | `BehaviorEngine`, `BattleConductor` | Framework strategy boundary exists; ordinary AI still delegates to legacy heuristics |
| Affinity knowledge and analysis | `BattleKnowledge` | Clean stores exist; interactive battle still uses legacy session knowledge |
| Negotiation, demands, recruitment | `NegotiationEngine`, `BattleConductor` | Framework session and recruitment services with console adapters; legacy data remains authoritative |
| EXP and Macca battle rewards | `CombatMath`, `BattleConductor` | Framework reward result service with console adapter; ruleset JSON binding remains incomplete |
| Inventory quantities | `InventoryManager` | Framework resource services with console adapter; legacy item content remains authoritative |
| Equipment ownership and equipping | `InventoryManager`, `FieldServiceEngine` | Framework resource services with console adapter; legacy equipment content remains authoritative |
| Economy and Macca transactions | `EconomyManager` | Framework transaction service with console adapter |
| Shops and buy/sell pricing | `ShopEngine`, `ShopUIBridge` | Framework transaction service with O8 typed presentation; legacy shop data remains authoritative |
| Hospital restoration | `FieldServiceEngine` | Framework restoration service with O8 typed presentation; current hospital UI quirks preserved |
| Field skill and item use | `FieldServiceEngine` | Clean field/action presentation exists; legacy skill/item content and effect parsing remain host-owned |
| City and field navigation | `FieldConductor` | Framework dungeon state machine with O9 typed presentation; broader city/menu flow remains console-host presentation |
| Dungeon traversal and terminals | `DungeonManager`, `DungeonState`, `ExplorationProcessor` | Framework state machine with O9 typed presentation; legacy dungeon data remains authoritative |
| Encounter and boss preparation | `ExplorationProcessor` | Framework random encounter selection plus O9 host handoff; catalog-backed encounter content remains future work |
| Moon phase | `MoonPhaseSystem` | Registration vocabulary only |
| Fusion result calculation | `FusionCalculator` | Inheritance only |
| Fusion slots, mutation, accidents | `FusionCalculator` | Missing |
| Fusion preview and confirmation | `FusionPlan`, `FusionPreviewFactory`, Cathedral bridge | Missing clean workflow |
| Fusion inventory transactions | `FusionMutator`, transaction helpers | Missing |
| Sacrificial, rank, and stat-boost fusion | Fusion strategies | Missing |
| Compendium registration and recall | `CompendiumRegistry` | Missing |
| Console presentation and cancellation flows | Bridges and `IGameIO` | Must become host adapters, not disappear |

## Protected Data Inventory

The legacy content remains protected source material until equivalent records pass the new loader and drive migrated consumers.

| File | Approximate records | Required future contract |
| --- | ---: | --- |
| `skills_database.json` | 420 | Skills, Navigator separation, custom mechanics review |
| `entity_database.json` | 304 | Entities, class/race metadata, conflicting physical affinities review |
| `status_ailments.json` | 11 | Ailments and lifecycle rules |
| `items.json` | 14 | Items and host requests |
| `weapons.json` | 26 | Equipment |
| `armor.json` | 3 | Equipment |
| `boots.json` | 3 | Equipment |
| `accessories.json` | 3 | Equipment and stat modifiers |
| `fusion_table.json` | 460 | Fusion recipes and result operations |
| `shop_inventory.json` | 30 | Shops and pricing metadata |
| `tartarus.json` | 1 dungeon graph | Dungeons, blocks, fixed floors, enemy pools |
| `questions.json` | Personality question/dialogue sets | Negotiation |

Generated `*_v2.json` files are not production authority. They may be used to identify conversion problems, but they must not become the final data source automatically.

## Target Solution Shape

The eventual solution should separate reusable rules from hosts without deleting the interactive reference application prematurely.

```text
Convergence.Framework
  Definitions
  Catalog loading and validation
  Runtime state and transactions
  Battle, field, party, inventory, growth, negotiation, fusion services
  Host-neutral commands, results, events, snapshots

Convergence.ConsoleHost
  Existing interactive workflows migrated incrementally
  Console input/output and menu rendering
  File-backed content source
  Debug and parity scenarios

Convergence.DemoHost
  Deterministic technical demonstrations
  Small original content packs

Convergence.GodotAdapter or game project
  Godot Nodes, Resources, scenes, signals, animation, persistence integration

Convergence.Tests
  Unit, contract, integration, parity, and host-routing tests
```

The exact project names may change. The ownership boundaries may not.

## Global Migration Rules

1. **Characterize before porting.** Add tests around existing behavior before replacing it.
2. **Preserve by default.** An undocumented difference is a regression, not a redesign.
3. **Decide ambiguity explicitly.** Contradictory or undesirable legacy behavior requires a decision record.
4. **Use one rule implementation.** Preview, assessment, execution, AI, and UI eligibility must call the same service.
5. **Separate definitions from state.** Catalog records are immutable; save/runtime state is mutable and separately serializable.
6. **Keep hosts outside the framework.** Console, filesystem, Godot, delays, colors, and input remain adapters.
7. **Inject nondeterminism.** Randomness, clocks, ID generation, and ordering policies must be controllable in tests.
8. **Use typed behavior.** Display names, descriptions, and category strings never dispatch mechanics.
9. **Aggregate diagnostics at content boundaries.** Invalid packs must not produce partial catalogs.
10. **Commit by vertical slice.** Do not combine unrelated subsystem migration and deletion.
11. **Retain legacy data until verified.** A converted file is removable only after count, reference, behavior, and host-consumer checks pass.
12. **Keep the application runnable.** Every migration commit must preserve an executable interactive path.

## Required Migration Artifacts

Each subsystem track must produce:

- a behavior inventory,
- a rule decision record for discrepancies,
- framework commands and result contracts,
- immutable content definitions when content-driven,
- mutable runtime state and snapshots,
- deterministic policies for random or formula-based behavior,
- unit tests for rules,
- parity tests against characterized legacy cases,
- an integration test using a real catalog,
- a host adapter or migrated console consumer,
- documentation of retained limitations,
- a removal checklist naming the exact obsolete files.

## Rule Decision Register

The following differences cannot be resolved as incidental implementation details.

### Elements And Affinities

- Slash, Strike, and Pierce collapse to `physical` in clean content, but 51 legacy entities author conflicting values across those channels.
- Earth has no approved clean damage element.
- Mind, Nerve, and Curse are legacy ailment vectors, not damage elements.
- Guard currently normalizes Weakness.
- Rigid-body ailments currently normalize physical Resist, Null, Repel, and Absorb while preserving Weakness.
- Almighty bypasses authored affinities in the clean resolver.

Required decision: define conversion policy per legacy entity or retain optional physical subtypes as metadata/rules without restoring them as primary damage elements.

### Damage, Accuracy, Criticals, And Instant Death

- `CombatMath` owns current production formulas and random bounds.
- Clean execution deliberately delegates these calculations to host policies.
- Weak and Resist multipliers are not framework defaults.
- Vulnerable and Resistant instant-death probability effects are not yet approved.

Required decision: promote reviewed formulas into a named framework ruleset policy rather than leaving the demo formulas as accidental defaults.

### Stats And Buffs

- Character stats cap at 40 before battle multipliers.
- Persona influence varies by stat and class.
- Current Kaja/Nda calculation uses 1.4 and 0.6 multipliers.
- `StatusRegistry` describes stage limits of `-4` to `4`, while some redundancy checks use `-3` and the data model does not yet enforce runtime duration/expiration consistently.

Required decision: define the authoritative stage cap, stage-to-multiplier formula, duration ownership, and class stat composition.

### Ailments

- Legacy actors hold at most one ailment; clean runtime state can hold multiple ailments.
- Poison currently deals 13% max HP and may be lethal because a previous nonlethal clamp is commented out.
- Sleep restores HP/SP.
- Reserve actors suspend ailment decay and damage.
- Natural recovery depends on Luck.
- Guard currently blocks ailment application.
- Rigid ailments force physical critical behavior elsewhere in combat math.

Required decision: approve simultaneous-ailment policy, Poison lethality, reserve ticking, recovery formulas, guard interaction, and rigid-body effects.

### Growth And Actor Classes

- Human, Operator, Persona User, Wild Card, and Demon share one broad `Combatant` class but use different stat and stock rules.
- Level-up growth is random.
- HP/SP formulas are embedded in `GrowthProcessor`.
- The clean actor factory currently receives initialization from a host demo policy.

Required decision: define reusable class/progression profiles without introducing speculative profiles that have no concrete behavior.

### Fusion

- The existing table includes normal results and special operations.
- Accident chance depends on moon phase.
- Skill mutation and slot counts have existing formulas.
- Fusion can create, rank-shift, or stat-boost results.
- Demon and Persona ownership transactions differ.

Required decision: approve recipe/result operation vocabulary and transaction behavior before adding a fusion JSON schema.

### Host And Persistence

- The console currently owns synchronous menus and delays.
- There is no complete save-game persistence layer.
- Godot will own scenes, resources, animation, and input.

Required decision: distinguish framework session snapshots from host save files, and define asynchronous host command/event flow without embedding Godot APIs.

Until a decision is recorded, preserve legacy behavior in the interactive path and do not claim parity for that rule.

## Track A: Freeze And Characterize The Recovery Baseline

### Goal

Create an auditable list of everything the interactive prototype can do before migration changes its implementation.

### Work

- Add a machine-readable parity ledger, preferably JSON or YAML under `Convergence.Tests/Fixtures/Parity/`.
- Record each capability, legacy entry point, clean owner, status, decision dependency, tests, and removal files.
- Convert debug scenarios into deterministic characterization tests where practical.
- Add smoke coverage for ordinary startup and major menu entry points using a scripted `IGameIO`.
- Capture representative battle, field, shop, dungeon, negotiation, fusion, and compendium workflows.
- Record the baseline dataset counts and reference-error counts.
- Record all current warnings separately from failures.

### Primary Files

- `Program.cs`
- `Host/ConsoleGameHost.cs`
- `Host/ScenarioFactory.cs`
- `Host/DebugScenarioRunner.cs`
- `Services/IGameIO.cs`
- all existing `Convergence.Tests/*.cs`

### Tests

- no-argument startup reaches scenario selection,
- scripted scenario startup reaches field navigation,
- every debug scenario exits or reaches its expected state,
- current 448 tests remain green,
- baseline content counts are asserted without approving the content shape.

### Exit Gate

Every protected capability in this document has a parity-ledger entry. No production code is removed.

### Completion Record

Track A began from documentation baseline commit `fce33a9` on `track-12-recovery`.

- The machine-readable ledger is `Convergence.Tests/Fixtures/Parity/recovery-baseline.json` and protects 35 capability IDs.
- The recovery branch began with 448 passing tests. Track A added 22 characterization and baseline tests, bringing the complete suite to 470 passing tests with 0 skipped tests.
- A nonincremental solution build completes with 122 warnings and 0 errors. The warning count is unchanged from the recorded recovery baseline.
- Authored data contains 420 skill records in 3 duplicate-name groups. The legacy name-keyed loader exposes 417 skills because one record from each duplicate group is discarded.
- The remaining dataset baseline is 304 entities, 11 ailments, 14 items, 26 weapons, 3 armor records, 3 boots, 3 accessories, 460 fusion recipes, 30 shop entries, 1 dungeon with 6 blocks, 8 negotiation personalities, 40 questions, and 8 familiar-dialogue sets.
- Known integrity findings are preserved as evidence: 56 unresolved base-skill references, 120 unresolved learned-skill references, 1 casing-only skill reference mismatch, and 1 unresolved dungeon enemy-pool reference. Dungeon boss, shop, and accepted fusion-operand checks have 0 unresolved entries.
- Deterministic seams were limited to internal seeded random sources, a reduced Monte Carlo overload, and debug battle delegates. Existing constructors and commands retain their previous defaults.
- Automated coverage now exercises startup, scenario setup, debug scenarios, major menu surfaces, shop transactions, dungeon milestones, negotiation outcomes, compendium behavior, and the existing clean battle and field demos.
- Full live battle interaction, exhaustive navigation through every console branch, and long-form play sessions remain manual checks. Existing specialized tests continue to own combat, status, party, Press Turn, fusion, catalog, skill, passive, and item behavior.
- No production subsystem, public API, gameplay rule, or content record was removed or redesigned.

## Track B: Split Framework And Hosts Without Changing Behavior

### Goal

Create physical project boundaries while retaining the interactive console application.

### Work

- Extract reusable clean code into a class-library project.
- Move only console startup, file access, menu rendering, delays, and debug scenario wiring into a console-host project.
- Keep the existing interactive behavior working through project references.
- Retain deterministic demos as a separate host or explicit console-host commands.
- Add API-boundary tests preventing console, filesystem, serializer, or Godot types from leaking into framework public APIs.
- Avoid moving legacy classes merely for aesthetics; move them when their dependencies permit it.

### Required Interfaces

- host-owned content text source,
- host-owned presentation/event sink,
- host-owned input command source,
- injectable random source,
- optional clock/ID source where required.

### Exit Gate

The solution builds as separate framework and console-host projects, `dotnet run` remains interactive, both clean demos still run, and no gameplay rules have changed.

### Completion Record

Track B began from `d97b244` on `track-12-recovery`.

- `JRPG.Framework` is a package-free `net9.0` class library containing the clean definitions, content pipeline, typed execution, battle runtime, defense and knowledge vocabulary, shared Press Turn engine, and fusion inheritance services.
- `JRPG.ConsoleHost` remains the root executable and owns `Program`, filesystem access, copied content, legacy Newtonsoft loading, console presentation, debug scenarios, and all unmigrated gameplay consumers.
- The dependency is one-way: console host to framework. Existing `JRPGPrototype.*` namespaces remain unchanged.
- Cancellation-aware content-source, event-sink, command-source, and random-source contracts establish the reusable host boundary. The synchronous console prototype remains on `IGameIO` until its consumer migration track.
- The framework builds independently with 0 warnings. The complete build retains the baseline 122 legacy warnings and introduces no new warnings.
- Nine Track B boundary and adapter tests bring the suite to 479 passing tests with 0 skipped tests.
- Ordinary startup characterization, dataset preservation tests, and both clean demos remain green. The battle demo reports `Victory` for `player_team`; the field demo completes all seven ordered events.
- No gameplay rule, content record, console option, capability status, consumer-migration flag, or removal authorization changed.

## Track C: Complete The Content Catalog Surface

### Goal

Represent all retained content families without forcing runtime state into JSON definitions.

### Existing Foundation

- manifests and exact SemVer dependencies,
- skills, entities, races, ailments, and items,
- strict deserialization, validation, qualification, and immutable repositories.

### Required New Families

1. **Equipment**
   - weapon, armor, boots, accessory or a unified slot-based definition,
   - defense, evasion, long-range metadata, stat modifiers,
   - granted skills or basic-attack metadata if approved,
   - stack/ownership rules separated from item definitions.
2. **Shop Catalogs**
   - offered content IDs,
   - shop/category identity,
   - base price or registered pricing policy,
   - availability and stock policy.
3. **Negotiation**
   - personalities, questions, answers, scores, demands, familiar dialogue sets,
   - race/entity defaults and overrides.
4. **Encounters**
   - ordered/weighted enemy formations,
   - levels, boss flags, rewards, environment metadata,
   - deterministic seed/policy ownership.
5. **Dungeons**
   - dungeon/block/floor structure,
   - enemy-pool and fixed-floor references,
   - terminals, barriers, bosses, and transitions as rules rather than scene paths.
6. **Fusion**
   - recipes and operation types,
   - special/rank/stat-boost operations,
   - accident and mutation policies by registered ID where appropriate.
7. **Rulesets**
   - named damage, growth, stat, Press Turn, stock-capacity, reward, economy, and moon-phase policies.

### Validation Requirements

- strict structural schemas,
- local record IDs and qualified external references,
- direct-dependency visibility,
- complete graph validation,
- deterministic document and diagnostic ordering,
- no display-text inference,
- no runtime/save state in content definitions.

### Exit Gate

Every protected legacy JSON family has an approved target schema and one small original valid fixture. Legacy datasets remain loaded by the interactive path.

### Completion Record

Track C began from `46e9634` on `track-12-recovery`.

- Added immutable framework definitions for equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets.
- Extended strict deserialization, validation requests, validated-content tokens, catalog qualification, catalog repositories, and loader cross-pack reference checks for all new families.
- Added explicit host registrations for shop categories, negotiation demands, encounter environments, and generic policy IDs; no hidden defaults were introduced.
- Added `convergence.catalog_surface_sample` `0.1.0` with seven new document families and compact original-inspired records.
- Updated the parity ledger so affected capabilities are `clean_foundation` only. No legacy consumer migrated, no runtime state moved into JSON, and no removal was authorized.
- Added three Track C catalog-surface tests. The complete suite now contains 482 passing tests with 0 skipped tests, and the nonincremental build remains at 122 warnings.

## Track D: Runtime Identity, State, Transactions, And Snapshots

### Goal

Replace the broad mutable `Combatant` shell with composable framework runtime state without losing class-specific behavior.

### Required Runtime Concepts

- stable instance ID distinct from entity definition ID,
- actor identity and display metadata,
- controller/team/owner relationships,
- active/reserve/deployed state,
- level, EXP, lifetime EXP, and unspent stat points,
- current/max resources,
- base and effective stats,
- learned and equipped skills,
- active Persona or equivalent form,
- Persona stock and demon stock references,
- equipment slots,
- ailments, statuses, stages, charges, shields, breaks, guarding, and analysis,
- per-battle activation state,
- transaction-safe mutation results,
- immutable snapshots for save/presentation/replay.

### Design Constraint

Do not create one new universal actor type that merely reproduces `Combatant` with different names. Prefer focused state components and services, with one aggregate only where transaction boundaries require it.

### Save Boundary

Define serializer-neutral save snapshots for mutable state. Content definitions are referenced by qualified ID and are not duplicated into saves.

### Exit Gate

All `Combatant` and `Persona` state used by protected workflows has a typed framework home, round-trip snapshot tests pass, and no host type appears in runtime contracts.

### Track D Completion

Track D began from `68175c8` on `track-12-recovery`.

- Added `JRPGPrototype.Logic.Runtime` with stable runtime instance IDs, focused immutable actor-state snapshots, the aggregate `RuntimeActorSnapshot` save/presentation/replay boundary, and transaction-safe resource mutation results.
- Covered identity, display metadata, controller/team/owner links, active/reserve/deployed state, level/EXP/stat points, current/max resources, base/effective stats, learned/equipped skills, active form references, persona/demon stock references, equipment slots, ailments, statuses, stages, charges, shields, breaks, guarding, analysis, and per-battle passive activation counts.
- Preserved the existing Track 12 `RuntimeActorState` and `BattleActorState` execution path. No console workflow, `Combatant`, `Persona`, `PartyManager`, inventory, fusion, or persistence consumer migrated in this track.
- Updated the parity ledger to mark newly covered runtime-state capabilities as `clean_foundation` only. No consumer migration flag or removal authorization changed.
- Added 10 focused runtime-state tests covering instance-ID normalization/rejection, full actor snapshot round-trip coverage, defensive collection snapshots, transaction before/after results, rejected mutation behavior, and public runtime API boundaries.

Verification:

- `dotnet test JRPG.sln --no-restore --filter FullyQualifiedName~RuntimeStateSnapshotTests`: 10 passed, 0 skipped.
- `dotnet test JRPG.sln --no-restore`: 492 passed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 122 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.
- `git diff --check`: passed.
- Framework forbidden-reference searches found no console, filesystem, sleep, Newtonsoft, `Database`, legacy DTO, `Combatant`, `Persona`, or `IGameIO` references.

## Track E: Stats, Classes, Resources, And Growth

### Goal

Port `StatProcessor` and `GrowthProcessor` into explicit ruleset services.

### Behavior To Preserve Or Decide

- Human, Operator, Persona User, Wild Card, and Demon stat composition,
- Persona contribution percentages by stat,
- accessory stat modifiers,
- stat cap of 40,
- HP/SP formulas,
- EXP requirement curve,
- level-up loops for large EXP awards,
- random stat growth,
- stat-point awards and manual allocation,
- rollback used by confirmation UI,
- resource preservation policy when maximum values change.

### Required Services

- `IStatResolutionPolicy`,
- `IResourceGrowthPolicy`,
- `IExperienceCurve`,
- `ILevelGrowthPolicy`,
- `IStatAllocationService`,
- deterministic random source.

### Tests

- parity vectors for every class and stat,
- level boundary and multi-level gains,
- resource recalculation,
- cap behavior,
- deterministic growth with fixed random sequences,
- allocation and rollback transactions.

### Exit Gate

Console status screens and level-up flows use framework services; legacy processors become thin adapters before removal.

### Track E Completion

Track E began from `fab0ba5` on `track-12-recovery`.

- Added framework progression services in `JRPGPrototype.Logic.Runtime` for stat resolution, resource recalculation, EXP curves, level growth, Persona stat growth, stat allocation, and rollback.
- Added standard framework IDs for Strength, Magic, Vitality, Agility, Luck, HP, SP, actor kinds, and legacy/clean modifier-track aliases. Generic attack maps to Strength and Magic; Defense maps to Vitality; Agility maps to Agility; Luck still has no buff/debuff alias.
- Extended `RuntimeActorSnapshot` with base resource values so `BaseHP` and `BaseSP` can be represented without depending on legacy `Combatant`.
- Added `LegacyProgressionAdapter` so `StatProcessor`, `GrowthProcessor`, and `Persona` delegate to the framework while keeping existing console state, messages, skill-name unlocks, and random behavior.
- Preserved exact legacy formulas for Persona contribution weights, raw stat cap before stage multipliers, EXP requirements, HP/SP maximums, level-up base-resource rolls, current-resource preservation, level-up healing-by-delta, stat allocation, rollback, and Persona random stat growth.
- The parity ledger now marks stat composition, growth/levels, and resource recalculation as `parallel_partial` with console consumers migrated through adapters. No legacy files are marked removable.
- Added framework and adapter tests for stat parity, accessory behavior, resource policies, EXP and multi-level gains, deterministic random growth, stat allocation, rollback, Persona skill learning/scaling, and public API boundaries.
- Focused Track E plus combat parity checks passed: 66 tests, 0 failed, 0 skipped.
- Full verification passed: 529 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 122 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.
- `git diff --check`: passed.
- Framework forbidden-reference searches found no console, filesystem, sleep, Newtonsoft, `Database`, legacy DTO, `Combatant`, `Persona`, or `IGameIO` references.

## Track F: Party, Persona Stock, And Demon Stock

### Goal

Port party and ownership behavior as reusable state transitions.

### Behavior Inventory

- maximum active party size of four,
- reserve membership and slot reassignment,
- level-based stock capacity of 3/5/7/10/12,
- unified demon stock where active demons remain owned in stock,
- Persona stock and active Persona rules,
- summon, return, active swap, reserve swap, dismiss, and replace,
- ownership and duplicate checks,
- controller and battle-control state,
- transient-state cleanup when leaving battle,
- party-wipe and alive-member queries.

### Required Contracts

- party snapshot,
- stock snapshot,
- typed party commands,
- transition result with stable failure codes,
- stock-capacity policy,
- atomic transaction service.

### Tests

- every current `PartyManagerTests` case,
- full-party and full-stock failures,
- no duplicate references after swaps,
- slot consistency,
- demon active/stock ownership invariant,
- Persona activation and replacement,
- rollback on failed transactions.

### Exit Gate

The interactive organize, summon, return, swap, dismiss, Persona stock, and demon stock menus use framework commands and results.

### Track F Completion

Track F began from `e84ba29` on `track-12-recovery`.

- Added framework party/stock transition services in `JRPGPrototype.Logic.Runtime` with immutable snapshots, stock capacity policy, typed command requests, stable result codes, diagnostics, affected runtime IDs, and unchanged before/after snapshots on rejection.
- Added adapter-owned per-session runtime identity mapping for live `Combatant` and `Persona` objects.
- Added `LegacyPartyStockAdapter` so the console host can build framework snapshots and apply successful transition results back onto existing live lists and properties.
- `PartyManager` now delegates active/reserve party operations, demon summon/return/swap/dismiss/replace, and stock capacity checks through the framework-backed adapter.
- Battle and field Persona swap paths use the same active-form stock transition and preserve existing messages, resource recalculation, and HP/SP capping behavior.
- `FusionInventoryTransaction` now delegates demon/persona consume and replace operations through the adapter while fusion planning, inheritance, accidents, UI, and economy behavior remain unchanged.
- Preserved active party capacity four, stock thresholds 3/5/7/10/12, active+owned demon overlap, Persona active/stock exchange, direct-control summon/swap behavior, and legacy party wipe/alive-member queries.
- The parity ledger now marks active/reserve party, Persona/demon stock, and party operations as `parallel_partial` with console consumers migrated through adapters. No legacy files are marked removable.
- Focused Track F tests passed: 32 tests, 0 failed, 0 skipped.
- Full verification passed: 556 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.
- `git diff --check` passed. `Data/Jsons` has no Track J changes. The new Track J runtime files contain no console, filesystem, Godot, Newtonsoft, legacy database, DTO, `IGameIO`, `Combatant`, or `Persona` references.

## Track G: Production Combat Ruleset

### Goal

Turn the legacy battle formulas into reviewed, named framework policies while retaining the typed execution boundary.

### Required Policies

- physical and magical damage,
- hit and evasion,
- critical chance,
- instant-death success,
- reflected damage,
- initiative,
- EXP yield,
- Macca yield,
- Weak and Resist multipliers,
- drain recovery,
- guard effects,
- rigid-body effects,
- difficulty or ruleset tuning if needed.

### Required Resolution Order

Document and test the order of:

1. target validity,
2. hit check,
3. shield,
4. Break/override/passive affinity resolution,
5. Repel/Absorb/Null handling,
6. critical and rigid-body behavior,
7. guarding,
8. damage modifiers and charge consumption,
9. drain,
10. defeat interception,
11. knowledge and Press Turn outcome.

### Tests

- port all `CombatMathTests`, `BattleEffectTests`, and `CombatVocabularyTests` as deterministic policy tests,
- add table-driven coverage for all eight clean elements and six affinities,
- test every unresolved-rule decision,
- compare legacy and framework results for approved parity vectors.

### Exit Gate

The production console battle path uses the clean resolver and named production policies. Demo policies remain examples only.

### Track G Completion

Track G began from `d053ef0` on `track-12-recovery`.

- Added `ProductionCombatRuleset` to the framework with named defaults for damage, hit/evasion, critical chance, instant death, initiative, reward yields, Weak/Resist, guard, rigid-body, charge, drain, reflection inputs, and variance.
- Added `LegacyCombatPolicyAdapter` so existing console `CombatMath` and `DamageHandler` callers use the framework policy without exposing `Combatant`, `Persona`, `IGameIO`, console, filesystem, or Newtonsoft to the framework.
- Preserved the existing `CombatMath`, `DamageHandler`, `DamageEffect`, `BehaviorEngine`, and `BattleConductor` call surfaces; no datasets, skill records, battle menus, AI heuristics, or effect text parsing were removed.
- Documented the production resolution order in gameplay docs and updated the parity ledger. Combat math and battle rewards are `parallel_partial`; removal remains unauthorized.
- Focused Track G combat tests passed: 127 tests, 0 failed, 0 skipped.
- Full verification passed: 576 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.

## Track H: Complete Action And Effect Execution

### Goal

Make the clean executor capable of every ordinary battle and field action.

### Existing Foundation

- typed skill costs, targeting, conditions, ordered effects,
- all approved effect variants,
- item execution and consumption decisions,
- passive triggers and rule modifiers.

### Missing Actions

- basic attack,
- guard,
- pass,
- Persona swap,
- demon summon/return/swap,
- analysis,
- tactics change,
- negotiation action,
- escape attempt,
- any special action currently dispatched outside typed effects.

### Required Contracts

- one action command union,
- assessment and execution APIs sharing the same rule path,
- atomic cost and inventory transactions,
- structured events suitable for console and Godot presentation,
- cancellation before mutation,
- explicit partial-success semantics,
- Press Turn result independent from presentation.

### Exit Gate

`ActionProcessor` delegates all migrated actions to framework services, and bridge eligibility cannot disagree with execution.

### Track H Completion

Track H began from `a9c79a4` on `track-12-recovery`.

- Added `BattleActionExecutor` with typed commands for basic attack, skill, item, guard, pass, analyze, escape, Persona swap, demon summon/return/swap, tactics change, negotiation, and host-special actions.
- Added shared assessment/execution results, stable diagnostics, ordered action events, explicit turn consumption, item reservation/commit ports, and cancellation checks before mutation or host-special dispatch.
- Reused the existing clean skill, item, ordered-effect, Press Turn, party-stock, escape, and combat-policy services rather than adding a second execution path.
- Updated the clean field demo so field recovery skills, recovery/cure/revival items, Traesto, and Goho-M execute through the action facade with host-owned inventory quantities.
- Added `LegacyBattleActionAdapter` and routed console guard/pass through it while preserving current `ActionProcessor`, `BattleConductor`, `SkillData`/`ItemData`, basic-attack Slash/Strike/Pierce, bridge, and effect-strategy behavior.
- Represented tactics and negotiation as typed host-mediated action commands only. Full battle orchestration, AI/tactics policy, negotiation/recruitment, production content reauthoring, inventory ownership, and legacy effect retirement remain later tracks.
- Updated the parity ledger. `battle_actions`, `typed_effects`, `inventory_quantities`, and `field_items_and_skills` remain `parallel_partial`; no removal is authorized.
- Focused Track H action/shared-effect tests passed: 27 tests, 0 failed, 0 skipped.
- Full verification passed: 585 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events through the action facade.
- `git diff --check`: clean. Data/Jsons had no diff.
- Framework boundary search found no production references to host/legacy actor or DTO types, `Database`, `IGameIO`, console, filesystem, or Newtonsoft in the new action facade. Existing clean `CombatDefenseProfile` still lives under the preserved `JRPGPrototype.Entities.Components` namespace from Track B compatibility.

## Track I: Ailment, Status, Duration, And Passive Lifecycle

### Goal

Port the strict-parity status lifecycle rules into framework services while keeping legacy display/effect-string compatibility isolated in the console host.

### Required Behavior

- ailment application and resistance,
- exclusivity or simultaneous-ailment policy,
- skip, limited action, chance skip, flee, return-to-stock, forced attack, and confused action,
- Poison and other turn-end effects,
- Sleep recovery,
- immediate and natural recovery,
- duration ticking and reserve suspension,
- buff/debuff caps and expiry,
- Break expiry,
- shield expiry/consumption,
- auto-passives,
- HP/SP regeneration passives,
- ailment protection and resistance passives,
- Endure-style defeat interception,
- cleanup at swap, battle end, and field transition.

### Data Work

Reauthor the 11 ailment records against the approved ailment schema with explicit triggers, modifiers, recovery, and turn behavior. The clean content pack must not infer behaviour from names or descriptions. Legacy `status_ailments.json` remains untouched until an original-content replacement and consumer switch are explicitly approved.

### Tests

- port every `StatusRegistryTests` case,
- add lifecycle order tests,
- reserve/deployment suspension,
- cleanup scope tests,
- multiple-passive ordering and activation limits,
- deterministic recovery and flee outcomes.

### Exit Gate

Framework lifecycle rules do not read skill or ailment display names. The interactive battle path uses the clean lifecycle dispatcher through a console compatibility adapter. Any remaining name or effect-string parsing is adapter-owned legacy compatibility and must be removed only after production skills, items, and ailments are reauthored.

### Track I Completion

Track I began from `f7dbf08` on `track-12-recovery`.

- Added `BattleStatusLifecycleService` for clean ailment application, turn-start restrictions, turn-end ailment/passive effects, natural recovery, duration ticking, cleanup scopes, and battle-start or turn-end passive dispatch.
- Extended `BattleActorState` with duration helpers for ailments, stat stages, charges, shields, affinity overrides, transient cleanup, and encounter cleanup.
- Routed `StatusRegistry.TryInflict`, `ProcessTurnStart`, `ProcessTurnEnd`, and stat-stage mutation through `LegacyStatusLifecycleAdapter` while preserving public method signatures and visible console behavior.
- Added `convergence.status_lifecycle_demo` `0.1.0`, a clean 11-ailment pack covering Poison, Freeze, Shock, Fear, Panic, Charm, Rage, Distress, Sleep, Bind, and Stun.
- Preserved strict parity: one active major ailment, lethal 13% max-HP Poison, 10% HP/SP Sleep recovery, `20 + Luck / 2` natural recovery, Panic and Fear chance rules, Guard blocking ailment application, reserve suspension, `-4..+4` stage caps, and `+3/-3` redundancy thresholds.
- Relaxed clean validation so ailment evasion multipliers may be zero, matching legacy immobilization-style content.
- Updated the parity ledger. `ailment_lifecycle` and `passive_lifecycle` are `parallel_partial` with migrated console consumers; no legacy file removal is authorized.
- Focused Track I lifecycle/status tests passed: 37 tests, 0 failed, 0 skipped.
- Full verification passed: 592 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.
- Legacy `Data/Jsons/status_ailments.json` is unchanged. Data/Jsons changes are limited to the new status lifecycle demo pack.
- Framework boundary search found no serializer, console, filesystem, Godot, Newtonsoft, legacy database, DTO, `IGameIO`, `Combatant`, or `Persona` dependency in the new lifecycle service. The console adapter remains the only Track I code that touches legacy runtime types.

## Track J: Battle Orchestration, AI, Knowledge, And Tactics

### Goal

Replace the demonstration battle runner with a host-neutral encounter state machine capable of supporting the interactive game.

### Required Lifecycle

- encounter creation and participant ordering,
- initiative,
- battle-start passives,
- team phases and icon initialization,
- turn-start restrictions,
- command request and validation,
- action execution,
- turn-end triggers,
- swaps and deployment changes,
- phase-end cleanup,
- victory, defeat, escape, recruitment, and fault outcomes,
- battle-end rewards and cleanup.

### AI Requirements

- deterministic strategy interface,
- knowledge-aware weakness preference,
- avoid known Null/Repel/Absorb,
- healing and redundancy logic,
- ailment and buff/debuff decisions,
- forced-action handling,
- tactics such as direct control and act freely,
- injectable randomness for equivalent choices.

### Presentation Requirements

Events must cover everything currently rendered by `BattleMessenger`, `BattleLogger`, and `InteractionBridge`, without storing console colors or delays in framework events.

### Tests

- deterministic complete battles,
- player-first and enemy-first initiative,
- every Press Turn outcome,
- swap and tactics effects,
- AI knowledge progression,
- victory/defeat/escape/fault,
- event ordering,
- scripted interactive-host battle.

### Exit Gate

The ordinary console battle uses the framework encounter state machine. `BattleConductor` is either a host adapter or removable after parity review.

### Track J Completion

Track J began from `aa82101` on `track-12-recovery`.

- Added `BattleEncounterRunner`, `BattleEncounterRequest`, `BattleEncounterResult`, `BattleEncounterParticipant`, initiative/lifecycle/turn/completion/event/synchronization ports, typed outcomes, typed command results, and ordered serializer-neutral battle events.
- Reworked `AutomatedBattleRunner` to use the encounter runner internally while preserving its public API and existing clean battle demo event stream.
- Routed ordinary `BattleConductor.StartBattle()` through the framework encounter state machine with `LegacyEncounterAdapter`, keeping live `Combatant` state, `InteractionBridge`, `ActionProcessor`, `BehaviorEngine`, `NegotiationEngine`, rewards, and console messages host-owned.
- Preserved legacy battle action execution and content. Track J does not reauthor `SkillData`, `ItemData`, negotiation rules, recruitment, rewards, inventory/equipment ownership, or production AI policies.
- Added a turn-start parity seam to `BehaviorEngine` so the framework lifecycle result is consumed once and not rerolled by AI.
- Added focused framework tests for initiative, event ordering, Press Turn outcomes, skip/turn-end dispatch, state refresh after defeat, victory, escape, cancellation, and typed faults.
- Added a console characterization proving `BattleConductor.StartBattle()` routes a real ordinary battle through the framework runner while preserving menu ordering, attack narration, victory, and reward text.
- Updated the parity ledger. Press Turn orchestration is `parallel_partial` with a migrated console consumer; battle actions, AI/tactics, battle knowledge, negotiation, and rewards remain partial or later-track work. No removal is authorized.
- Focused Track J framework and console-route tests passed: 14 tests, 0 failed, 0 skipped.
- Full verification passed: 606 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 120 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.

## Track K: Negotiation, Recruitment, And Battle Rewards

### Goal

Port the systems currently embedded across `NegotiationEngine` and `BattleConductor`.

### Required Negotiation Behavior

- personality-based question pools,
- answer scoring,
- moon-phase restrictions,
- repeated-negotiation restrictions,
- familiar dialogue,
- Macca and item demands,
- anger/failure outcomes,
- recruitment eligibility,
- duplicate ownership and stock-capacity checks,
- session recruitment tracking,
- demon versus non-demon behavior.

### Required Reward Behavior

- EXP and Macca yields,
- distribution rules,
- growth invocation,
- recruitment versus defeat distinction,
- boss completion callbacks,
- compendium auto-registration where intended.

### Required Contracts

- negotiation session state machine,
- typed prompts and response commands,
- demand and outcome unions,
- recruitment transaction service,
- reward calculation/result service.

### Exit Gate

An interactive host can complete negotiation and recruitment without `IGameIO` entering framework rules, and battle rewards match approved legacy vectors.

### Track K Completion

Track K began from `c3f3039` on `track-12-recovery`.

- Added `NegotiationSessionService`, typed negotiation prompts, demand selections, outcomes, familiar gifts, `RecruitmentTransactionService`, and `BattleRewardService` under the framework runtime namespace.
- Kept `questions.json` and `Data/NegotiationData.cs` unchanged. `NegotiationEngine` now maps legacy data and `IGameIO` interactions into framework requests, then applies returned Macca, item, or familiar-gift mutations.
- Added `LegacyRecruitmentAdapter` so `BattleConductor` validates recruitment before mutating demon stock, session recruitment IDs, enemy lists, and compendium registration.
- Added `LegacyBattleRewardAdapter` so victory rewards are calculated as immutable framework results and then applied to live console actors, active Personas, and economy.
- Preserved legacy battle orchestration and data ownership. Track K does not reauthor production negotiation content, bind rulesets from JSON, migrate boss completion callbacks, or authorize deletion of legacy files.
- Added focused framework tests for demand flow, familiar gifts, recruitment validation, reward applications, and immutable results. Strengthened console characterization for recruitment and victory reward state.
- Focused Track K tests passed: 13 tests, 0 failed, 0 skipped. Runtime public API boundary tests also pass.
- Full verification passed: 610 tests, 0 failed, 0 skipped.
- `dotnet build JRPG.sln --no-restore --no-incremental`: 119 warnings, 0 errors.
- `dotnet run --no-build -- --clean-battle-demo`: completed with `Victory` for `player_team`.
- `dotnet run --no-build -- --clean-field-demo`: completed all seven ordered field-effect events.
- `git diff --check` passed. `Data/Jsons` has no Track K changes. The new framework runtime file contains no console, filesystem, Godot, Newtonsoft, legacy database, DTO, `IGameIO`, `Combatant`, `Persona`, `SkillData`, `PersonaData`, or `ItemData` references.

## Track L: Inventory, Equipment, Economy, Shops, And Hospital

### Goal

Port persistent resource-management systems as atomic framework services.

### Inventory

- item quantities and stack limits,
- equipment ownership,
- add/remove validation,
- consumption based on executor results,
- immutable inventory snapshots.

### Equipment

- slot compatibility,
- equip/unequip and ownership invariants,
- weapon range/type metadata,
- defense/evasion/stat modifiers,
- basic-attack interaction,
- prevention of selling equipped items.

### Economy And Shops

- Macca balance,
- atomic spend/add,
- buy/sell prices,
- category filters,
- stock and availability,
- item inspection data,
- transaction diagnostics and rollback.

### Hospital

- restoration cost,
- eligible patient selection,
- HP/SP and ailment restoration,
- payment and mutation atomicity.

### Tests

- insufficient currency,
- stack and ownership limits,
- equipped-item sale rejection,
- buy/sell round trips,
- inventory mutation only after successful item execution,
- hospital full-health and ailment cases,
- snapshot immutability.

### Exit Gate

Interactive inventory, equip, shop, and hospital menus consume framework services. Legacy managers can then be adapted or removed independently.

### Track L Completion

Track L began from `51ab35c` on `track-12-recovery`.

- Added `RuntimeInventorySnapshot`, `RuntimeWalletSnapshot`, item reservation/commit/rollback support, inventory/equipment transitions, Macca transactions, shop buy/sell transactions, and hospital restoration results under `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs`.
- Preserved the current adapter-first boundary. `InventoryManager`, `EconomyManager`, `ShopEngine`, and the field equipment/item/hospital mutation paths now delegate through `LegacyInventoryResourceAdapter`, while UI bridges, legacy DTOs, `Database`, and `Data/Jsons` remain console-host owned.
- Preserved exact resource formulas: unique equipment IDs, unbounded legacy item stacks unless a clean stack limit is supplied, Luck-based buy/sell prices, base-price `100` sell fallback, and hospital cost `missing HP + missing SP * 5`.
- Kept the existing hospital UI healthy/full-resource behavior while allowing engine-level ailment-only treatment at zero cost.
- Added focused framework tests for inventory, reservations, equipment, economy, shop, and hospital services. Strengthened console characterization for managers, shop duplicate/equipped rejection, equipment ownership checks, hospital restoration, and field item consumption.
- Updated the parity ledger. `inventory_quantities`, `equipment_ownership`, `economy`, `shops`, and `hospital` are now `parallel_partial` with adapter-routed consumers. `field_items_and_skills` remains partial because legacy item/skill effect parsing still exists. No removal is authorized.
- Focused Track L checks passed: 7 framework resource-management tests, the 12-test legacy workflow characterization suite, and the parity-ledger validation test all passed.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 620 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 119 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track M: Field And Dungeon State Machines

### Goal

Port `FieldConductor`, dungeon progression, and exploration into reusable commands and transitions.

### Field Requirements

- city/field/dungeon location state,
- available actions by context,
- inventory and field-skill entry,
- status and stock entry,
- shops, hospital, Cathedral, and dungeon entry,
- dungeon-exit host requests.

### Dungeon Requirements

- current dungeon, block, and floor,
- ascending, descending, and warping,
- unlocked terminals,
- barriers and block completion,
- safe rooms,
- fixed boss floors,
- boss-defeat state,
- enemy pools and encounter preparation,
- game-over recovery to entrance,
- deterministic exploration policy.

### Host Boundary

The framework returns transitions and events. Godot maps them to scenes; the console maps them to menus. No scene path, console prompt, or blocking wait belongs in dungeon rules.

### Tests

- all legal and illegal floor transitions,
- terminal unlock and warp rules,
- boss gating and completion,
- safe room behavior,
- encounter generation with deterministic random input,
- dungeon exit and game-over reset,
- complete scripted dungeon session.

### Exit Gate

The interactive console field loop runs through the framework state machine, and a headless test can traverse the same dungeon without UI.

### Track M Completion

Track M began from `1502970` on `track-12-recovery`.

- Added `RuntimeFieldDungeonService` and immutable runtime snapshots/events for field location, dungeon progress, dungeon content, floor results, available dungeon actions, transitions, terminal unlocks, barriers, boss defeat state, dungeon exits, game-over recovery, and deterministic encounter generation.
- Added `LegacyDungeonContentAdapter` for adapting `Database.Dungeons` without reauthoring `tartarus.json`; legacy IDs are encoded as reversible `legacy_<hex>` content IDs and decoded before host encounter hydration.
- Converted `DungeonManager` into the compatibility facade over the framework service. `FieldConductor` now routes dungeon entry, terminal return, Goho-M/explicit exits, return-to-city, and boss-defeat registration through that facade.
- Preserved visible console ownership: menus, battle handoff, item/skill parsing, shops, hospital, Cathedral, `CombatantFactory`, duplicate enemy suffix naming, legacy DTOs, and datasets remain host-owned.
- Updated the parity ledger. `field_navigation`, `dungeon_traversal`, and `encounters` are now `parallel_partial`; no removal is authorized.
- Focused Track M checks passed: 5 framework field/dungeon state-machine tests, the 14-test legacy workflow characterization suite, and the parity-ledger validation test all passed.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 627 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 118 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track N: Complete Fusion And Compendium

### Goal

Build the full catalog-backed fusion workflow while retaining the approved inheritance evaluator.

### Existing Foundation

- typed inheritance decisions,
- deterministic candidate ordering,
- selection-limit validation,
- passive fusion-fodder behavior.

### Required Fusion Behavior

- recipe lookup and pair normalization,
- result operation selection,
- level restrictions,
- duplicate-result restrictions,
- natural result skills,
- inheritance slot calculation,
- preview/commit parity,
- mutation families and mutation chance,
- moon-phase accidents,
- sacrificial fusion,
- rank-up/rank-down operations,
- stat-boost fusion,
- Persona and demon transaction differences,
- parent/sacrifice consumption,
- cancellation and rollback.

### Compendium Requirements

- registration keyed by species/content ID,
- immutable snapshots rather than live references,
- overwrite/update policy,
- recall cost,
- duplicate and stock-capacity checks,
- recall transaction and ownership,
- automatic registration rules.

### Tests

- port all fusion regression and bridge-result tests,
- every fusion operation type,
- preview exactly matches committed result,
- no mutation before confirmation,
- cancel/wait/forbidden paths,
- accident determinism,
- compendium clone isolation,
- recall pricing and rollback,
- passive Ice Boost inheritance scenario.

### Exit Gate

The interactive Cathedral and compendium flows use catalog-backed services. Legacy calculators, mutators, strategies, and transaction helpers are removed only after a dedicated parity review.

### Track N Completion

Track N began from `ec7d4fa` on `track-12-recovery`.

- Added `FusionRuntimeServices` as the framework home for fusion recipe lookup, result operation selection, moon-phase accident checks, standard race results, rank up/down operations, Mitama stat-boost decisions, typed inheritance planning, slot calculation, accident inheritance replacement, mutation selection, immutable previews, transaction assessment, and Compendium registration/recall assessment.
- Added `LegacyFusionContentAdapter` to map `Database.FusionRecipes`, `Database.Personas`, `Database.Skills`, live `Combatant`, and live `Persona` data into framework snapshots. Legacy JSON files and DTOs remain console-host owned and unchanged.
- Converted `FusionCalculator`, duplicate-result checks, and `CompendiumRegistry` into compatibility facades where Track N migrated rules. Cathedral menus, prompts, waits, ritual confirmation, fusion strategies, economy mutation, and stock mutation remain host-owned compatibility surfaces.
- Fixed Compendium snapshot isolation: registered entries deep-clone active Persona state instead of retaining live references.
- Updated the parity ledger. Fusion result calculation, slots/mutation/accidents, fusion transactions, and Compendium are now `parallel_partial` where console consumers delegate through adapters; no removal is authorized.
- Focused Track N checks passed: 4 framework fusion/Compendium runtime tests plus the Compendium snapshot characterization test passed; the 52-test fusion regression, bridge-result, and inheritance suite passed.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 631 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 115 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track O: Console Host Migration

Track O is now split into subtracks after O1. Use [Track O Console Host Migration Plan](o-track-plan.md) as the active working checklist for O2 and later passes.

### Goal

Make the interactive console application a real consumer of framework commands, results, events, catalogs, and snapshots.

### Work

- retain current menu structure initially,
- replace direct `Database`, `Combatant`, and manager mutation one workflow at a time,
- translate `IGameIO` input into typed commands,
- render structured framework results,
- preserve back/cancel/confirm behavior,
- maintain debug scenarios as parity tools,
- keep ordinary `dotnet run` interactive throughout.

### Migration Order

1. catalog-backed startup,
2. status and read-only screens,
3. inventory and field effects,
4. party/stock organization,
5. battle actions,
6. complete battle lifecycle,
7. negotiation and rewards,
8. shops/hospital,
9. dungeon traversal,
10. fusion and compendium.

### Exit Gate

Every protected workflow is reachable through the console host using framework services. Clean demos remain separate technical tools.

### Track O1 Completion

Track O1 began from `e163b81` on `track-12-recovery`.

- Added `InteractiveConsoleHostContext` and console host adapters for framework content-text, event-sink, and command-source contracts.
- Ordinary startup still calls `Database.LoadData` first, then attempts to load the retained clean reference, battle-demo, shared-effects, catalog-surface, and status-lifecycle packs as a nonfatal sidecar catalog. Catalog diagnostics are printed as warnings and do not block legacy gameplay.
- Migrated plain field, city, inventory, status, dungeon, terminal, hospital-patient, and field-target menu choices through the host-command contract while preserving legacy return values and menu ordering.
- Added a human-status projection through runtime snapshots before rendering the same console status text.
- Updated the parity ledger. `interactive_boot` and `console_presentation` are now `parallel_partial`; no gameplay rule, legacy dataset, rich presentation bridge, battle command flow, Cathedral flow, or removal authorization changed.
- Focused Track O1 checks passed: the startup sidecar tests, plain-menu command tests, framework host-adapter tests, parity-ledger validation test, and ordinary startup characterization test reported 13 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 636 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 113 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O2 Completion

Track O2 began from `a9f2f87` on `track-12-recovery`.

- Replaced the O1 human-only status projection with `LegacyStatusPresentationProjection`, a console-owned adapter that copies live legacy actor and Persona data into framework runtime snapshots plus immutable display details.
- Routed Human status, Persona details, demon details, stock rows, organization rows, summon rows, and equipment-slot labels through projection helpers while preserving existing `StatusUIBridge` public methods and return values.
- Kept stat allocation, stock mutation, equipment mutation, battle presentation, Cathedral presentation, and production content authority unchanged.
- Updated the parity ledger. `console_presentation` remains `parallel_partial`; no removal is authorized.
- Focused O2 verification passed: status projection tests, plain menu command tests, and the status/equipment surface characterization test reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 640 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 113 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O3 Completion

Track O3 began from `53fbf40` on `track-12-recovery`.

- Added typed console-host results for field item selection, skill performer selection, field skill selection, target selection, field-use assessment, execution reasons, consumption decisions, and ordered presentation events.
- Routed field item and field skill conductor flows through those results while preserving existing nullable compatibility wrappers, menu labels, disabled options, hover text, saved menu indices, messages, delays, and dungeon-exit signaling.
- `FieldServiceEngine` now exposes assessment and detailed execution for field item/skill use. Consumption stays host-owned through `LegacyInventoryResourceAdapter`; successful effects or Goho-M consume once, while cancellation, unavailable, no-effect, unsupported field items, and insufficient-SP paths do not consume or spend.
- Legacy item/skill DTO parsing remains console-host compatibility code. Production JSON, framework public APIs, rich battle presentation, shops, hospital menus, party organization, and Cathedral presentation remain unchanged.
- Updated the parity ledger. `field_items_and_skills` and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O3 verification passed: field inventory presentation tests, plain menu command tests, and the representative field/shop characterization test reported 11 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 647 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 105 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed the shared field effects demo successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O4 Completion

Track O4 began from `c7d7c40` on `track-12-recovery`.

- Added typed console-host results for organization slot selection, demon stock selection, Persona stock selection/action, summon target selection, and party/stock mutation presentation.
- `StatusUIBridge` now exposes typed result methods while keeping its legacy wrapper methods and return values for existing conductors.
- `LegacyPartyStockAdapter` exposes detailed Track F transition results for field-side presentation; existing bool-returning `PartyManager` mutation methods still behave the same.
- `FieldServiceEngine` now returns typed presentation results for Persona swap, demon summon, demon return, demon swap, and dismiss. Results include transition codes, affected runtime IDs, and ordered field messages.
- `FieldConductor` consumes the typed bridge results for field organization, demon stock, and Persona stock flows while preserving menu order, cancellation, status peek, active plus owned demon overlap, transient cleanup, Persona HP/SP capping, and visible messages.
- Updated the parity ledger. `party_operations`, `persona_and_demon_stock`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O4 verification passed: `PartyStockPresentationTests`, `PartyStockAdapterTests`, `PartyManagerTests`, and `StatusPresentationProjectionTests` reported 27 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 654 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O5 Completion

Track O5 began from `f671491` on `track-12-recovery`.

- Added a console-host battle command shell that turns player battle selections into framework `BattleActionCommand` objects while preserving the legacy execution payloads required by `ActionProcessor`, `PartyManager`, `NegotiationEngine`, and existing battle helpers.
- Legacy attack, skill, item, escape, tactics, and negotiation paths are represented as host-mediated commands with stable IDs. Guard, pass, analyze, Persona swap, demon summon, demon return, and demon swap use concrete framework command shapes and assessment.
- `BattleConductor` now routes player command selection through the shell before continuing through legacy execution. Legacy skill/item DTOs, production `Data/Jsons`, battle narration, AI, rewards, recruitment, Press Turn outcomes, and clean skill/item execution remain unchanged.
- Updated the parity ledger. `battle_actions`, `enemy_ai_and_tactics`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O5 verification passed: `BattleCommandShellTests`, `BattleBridgeResultTests`, and `ActionProcessorResultTests` reported 53 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 664 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O6 Completion

Track O6 began from `3dae5ae` on `track-12-recovery`.

- Added a console-host battle event presentation layer with typed `Shown`, `Suppressed`, and `HostOwned` results.
- `BattleConductor` now provides a `LegacyBattleEventPresentationAdapter` to `BattleEncounterRunner`, allowing the console host to consume ordered framework events while preserving existing visible narration.
- Generic framework structural events are suppressed instead of printed. Skip, fear flee, return-to-COMP, enemy flee, and demon defeat return messages now pass through typed lifecycle-shell presentation results.
- Legacy attack, skill, item, escape, tactics, negotiation, AI, rewards, recruitment, Press Turn outcomes, production `Data/Jsons`, and clean skill/item execution remain unchanged.
- Updated the parity ledger. `press_turn`, `battle_actions`, `enemy_ai_and_tactics`, and `console_presentation` remain `parallel_partial`; `battle_knowledge` did not receive new O6 evidence; no removal is authorized.
- Focused O6 verification passed: `BattleEventPresentationTests`, `BattleCommandShellTests`, `BattleBridgeResultTests`, `ActionProcessorResultTests`, `BattleEncounterRunnerTests`, and the ordinary battle routing characterization reported 74 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 671 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O7 Completion

Track O7 began from `01ddcc4` on `track-12-recovery`.

- Added typed console-host presentation records for negotiation prompts, demands, events, final outcomes, recruitment outcomes, and battle reward display.
- `NegotiationEngine` now exposes an internal detailed result while keeping the public `StartNegotiation` wrapper unchanged. The detailed result records the framework session result, mapped legacy result, prompt/event presentation records, and mutation summary.
- `BattleConductor` now uses one shared negotiation/recruitment presentation helper for both the framework encounter path and older compatibility method.
- `LegacyBattleRewardAdapter` now converts immutable reward totals into the existing victory reward message before applying EXP/Macca mutations.
- Legacy negotiation data, reward formulas, recruitment rules, production `Data/Jsons`, compendium recall, Cathedral presentation, and framework public APIs remain unchanged.
- Updated the parity ledger. `negotiation_and_recruitment`, `battle_rewards`, `battle_actions`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O7 verification passed: `NegotiationRewardPresentationTests`, `NegotiationRewardRuntimeTests`, `BattleCommandShellTests`, `BattleEventPresentationTests`, and the negotiation/ordinary battle characterizations reported 40 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 688 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O8 Completion

Track O8 began from `713d3cb` on `track-12-recovery`.

- Added typed console-host presentation records for shop command selection, buy/sell offers, transaction confirmation, transaction display, hospital patient selection, and hospital treatment display.
- `ShopUIBridge` keeps `OpenShop` unchanged and now routes command selection, offer selection, confirmation, and inspection through typed results.
- `ShopEngine` keeps `ExecutePurchase` and `ExecuteSale` unchanged and now exposes detailed framework-backed transaction presentation results.
- `ServiceUIBridge`, `FieldServiceEngine`, and `FieldConductor` now present hospital selection and treatment from typed results backed by `HospitalRestorationResult`.
- Legacy shop data, Luck pricing, metadata repair, equipment ownership, hospital UI quirks, production `Data/Jsons`, and framework public APIs remain unchanged.
- Updated the parity ledger. `shops`, `hospital`, `economy`, `equipment_ownership`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O8 verification passed: `ShopHospitalPresentationTests`, `ResourceManagementServiceTests`, and the shop/hospital/menu characterizations reported 17 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 694 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 99 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O9 Completion

Track O9 began from `60858c6` on `track-12-recovery`.

- Added typed console-host dungeon traversal presentation records for floor action selection, entry/terminal floor selection, transition presentation, floor-entry presentation, and mapped runtime event presentation.
- `DungeonManager` now exposes detailed transition presentation methods over `RuntimeFieldDungeonService` while preserving the old public wrappers for existing callers.
- `DungeonUIBridge`, `ExplorationProcessor`, and `FieldConductor` now consume typed dungeon results for movement, terminal warp, Goho-M/explicit exits, barriers, safe rooms, boss requests, boss defeat registration, and encounter handoff.
- Structural framework events such as floor entry, terminal unlock, encounter request, dungeon exit, and action rejection are recorded but suppressed unless they replace an existing legacy message.
- Legacy `tartarus.json`, enemy hydration through `CombatantFactory`, duplicate suffix naming, battle handoff, menu order, visible text, and framework public APIs remain unchanged.
- Updated the parity ledger. `field_navigation`, `dungeon_traversal`, `encounters`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O9 verification passed: `DungeonTraversalPresentationTests`, `FieldDungeonStateMachineTests`, the dungeon workflow characterizations, and dungeon/target menu command tests reported 14 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 699 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

### Track O10 Completion

Track O10 began from `4ed67ca` on `track-12-recovery`.

- Added typed console-host presentation records for Cathedral menu selection, ritual participant selection, inheritance rows, ritual confirmation, ritual sequence events, fusion transaction outcomes, Compendium recall selection, Compendium registration, and recall transaction outcomes.
- `CathedralUIBridge` keeps the existing public wrappers and now exposes detailed methods for the full Cathedral and Compendium presentation surface.
- `FusionConductor` consumes the detailed results while preserving binary/sacrificial ritual loops, cancellation, wait, forbidden preview, accident reveal, legacy transaction strategies, stock/economy mutation, and visible text.
- `FusionPlan` carries framework inheritance display entries from `FusionPlanningService` as presentation evidence; legacy display names and pickable/exclusive behavior remain unchanged.
- `FusionMutator` and `CompendiumRegistry` now expose detailed transaction/registration/recall results while preserving legacy wrappers, recall pricing, deep snapshot behavior, and mutation ownership.
- Production fusion data, production `Data/Jsons`, framework public APIs, accident/mutation probabilities, recall pricing, and fusion strategy classes remain unchanged.
- Updated the parity ledger. `fusion_preview_confirmation`, `fusion_transactions`, `compendium`, and `console_presentation` remain `parallel_partial`; no removal is authorized.
- Focused O10 verification passed: `FusionCompendiumPresentationTests`, `FusionBridgeResultTests`, `FusionCompendiumRuntimeTests`, `FusionBugRegressionTests`, and `FusionInheritanceTests` reported 63 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 706 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no legacy DTO/host/Newtonsoft/filesystem dependencies, and `Data/Jsons` had no modified files.

## Track P: Godot Integration Contract

### Goal

Prove that the framework is reusable from Godot without redesigning gameplay around Godot classes.

### Required Adapter Responsibilities

- load JSON or authored resources and supply text/bundles,
- map player input and signals into framework commands,
- translate framework events into animation, audio, and UI,
- own Nodes, Resources, scene transitions, and asset IDs,
- persist framework snapshots in the game's save format,
- schedule asynchronous presentation without blocking framework state,
- maintain instance-ID mappings between framework actors and scene objects.

### AOT And Packaging

- preserve source-generated serializer metadata,
- test trimming/AOT-compatible paths where practical,
- expose no `JsonElement`, filesystem, console, or Godot types publicly,
- document dependency injection and lifecycle ownership,
- package the framework as a project reference or NuGet-compatible library.

### Proof

Build a contract-only Godot-facing adapter test. Track P does not add a GodotSharp package or Godot project.

The proof must:

- load retained clean packs from fake `res://` resources while preserving logical manifest and document paths,
- build a `GameDataCatalog` with explicit registrations,
- create catalog-backed actors,
- execute a deterministic clean battle/action path,
- consume ordered framework events through a host-owned sink,
- map runtime actor IDs to host-owned scene handles,
- save and restore framework snapshots inside a host-owned save envelope.

### Exit Gate

Godot integration requires adapter code, not changes to core rules or domain definitions. No protected capability moves to `clean_parity`, no legacy removal is authorized, and production `Data/Jsons` remains unchanged.

### Track P Completion

Track P began from `c3883e5` on `track-12-recovery`.

- Added [Godot Integration Contract](godot-integration-contract.md) as the active host-boundary reference for Godot-style adapters.
- Added `GodotIntegrationContractTests`, proving fake `res://` resource loading, signal-style command input, event consumption, scene-instance mapping, deterministic clean battle execution, and host-owned snapshot save/restore.
- Tightened framework boundary tests so framework source checks also reject `Godot` references.
- No GodotSharp package, Godot project, framework public API change, gameplay rule change, production JSON edit, parity promotion, or removal authorization was introduced.
- Focused Track P checks passed: `GodotIntegrationContractTests` plus `FrameworkBoundaryTests` reported 5 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 708 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Packaging verification passed after building the Release framework artifact: `dotnet pack JRPG.Framework/JRPG.Framework.csproj --no-build` created `JRPG.Framework.1.0.0.nupkg` and emitted only the NuGet readme advisory.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, framework forbidden-reference searches found no Godot/console/filesystem/Newtonsoft/legacy public-type leakage, and `Data/Jsons` had no modified files.

## Track Q: Original Content Policy And Legacy Dataset Boundary

### Goal

Prevent prototype-only legacy content from becoming clean catalog authority by accident.

Track Q is governed by [Track Q Original Content Policy And Legacy Dataset Boundary](q-track-plan.md). Q1 created the audit ledger. Q2 amends the policy: the retained `Data/Jsons` records are prototype characterization data, not approved production content and not a conversion queue.

### Boundary Procedure

1. Freeze legacy record count and identifiers.
2. Treat every legacy family as prototype-only unless an explicit future decision says otherwise.
3. Preserve Q1 counts, reference findings, and conflict buckets as audit evidence.
4. Author future clean packs as original content directly in approved schemas.
5. Validate original packs structurally and semantically.
6. Run representative runtime tests using the original packs.
7. Switch a real consumer only after original content and runtime coverage exist.
8. Keep legacy sources protected until the console prototype no longer needs them and removal is explicitly authorized.

### Mandatory Reports

- record counts before and after,
- ID mapping,
- omitted records with reasons,
- unresolved references,
- behavior decisions,
- conflict list,
- runtime coverage list.

For Q2, these reports remain obligations for future original-content work. They do not authorize direct legacy-to-clean conversion.

### Special Cases

- physical affinity conflicts remain prototype audit evidence, not automatic entity decisions,
- Navigator skills require their own support-system contract before original production authoring,
- special skills require explicit typed behavior or registered handlers before use in original packs,
- franchise-derived demonstration or prototype content must stay separate from framework-required examples and future commercial content.

### Track Q1: Audit Ledger And Conversion Rules

Create the Track Q control plane:

- `docs/q-track-plan.md` records the original Q audit plan, later amended by Q2.
- `Convergence.Tests/Fixtures/ProductionContent/production-content-ledger.json` records each protected legacy content family, clean schema target, future subtrack, report obligation, manual decision bucket, and removal gate.
- `ProductionContentLedgerTests` makes the ledger executable by requiring exact legacy-file coverage, exact clean-schema-family coverage, historical-only handling for old v2 migration artifacts, and no Q1 conversion or consumer switch.

Q1 performs no production JSON conversion, switches no gameplay consumer, and keeps removal authorization false.

### Track Q1 Completion

Track Q1 completed on `track-12-recovery`.

- Added [Track Q Original Content Policy And Legacy Dataset Boundary](q-track-plan.md) as the active Track Q reference.
- Added `Convergence.Tests/Fixtures/ProductionContent/production-content-ledger.json`, covering 10 production families, 12 protected legacy content files, all 12 clean schema families, 7 mandatory report types, 4 manual-decision buckets, and 3 historical-only migration artifacts.
- Added `ProductionContentLedgerTests`, proving exact legacy-file coverage, clean-schema-family coverage, historical-only handling for old v2 artifacts, and no Q1 conversion, consumer switch, `clean_parity`, or removal authorization.
- Recorded known unresolved-reference findings: 56 unresolved base-skill references, 120 unresolved learned-skill references, 1 casing-only skill reference, 1 unresolved dungeon enemy-pool reference, 0 unresolved dungeon boss references, 0 unresolved shop references, and 0 invalid fusion operands.
- Focused Q1 checks passed: `ProductionContentLedgerTests`, `RecoveryParityLedgerTests`, and `RecoveryDatasetBaselineTests` reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 713 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no Godot/console/filesystem/Newtonsoft/legacy DTO/static database leaks, and `Data/Jsons` had no modified files.

### Track Q2: Legacy Content Boundary Amendment

Q2 completed on `track-12-recovery` and supersedes the old family-by-family conversion interpretation without changing runtime code or production JSON.

- The production-content ledger now marks legacy dataset families as `prototype_only_legacy_authority`.
- The root and per-family policy records state that legacy content is not commercially approved, direct conversion is paused, original replacement content is required, clean catalog authority is not allowed, and removal remains unauthorized.
- Future content work is grouped under `future_original_content` until a dedicated plan authors original packs and switches a real consumer.
- Old v2 migration artifacts and Q1 integrity counts remain useful evidence, but they are not approved source material for clean production content.
- No production JSON file, framework API, gameplay consumer, or `Data/Jsons` record changed.
- Focused Q2 checks passed: `ProductionContentLedgerTests`, `RecoveryParityLedgerTests`, and `RecoveryDatasetBaselineTests` reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 713 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no Godot/console/filesystem/Newtonsoft/legacy DTO/static database leaks, and `Data/Jsons` had no modified files.

### Exit Gate

Original clean content loads through the catalog and drives migrated hosts. No prototype-only legacy content is silently copied into production authority, and no legacy source is removed before an explicit removal gate.

## Track R: Persistence Snapshot Contracts And Host-Owned Save Demo

### Goal

Make mutable game state portable across hosts and future framework versions.

### Required Snapshot Families

- player and actor progression,
- current resources and persistent ailments,
- party and stocks,
- inventory and equipment,
- economy,
- dungeon and terminal progress,
- compendium,
- knowledge,
- moon phase and session progression,
- host-owned presentation/location reference where needed.

### Requirements

- versioned save contracts,
- qualified content references,
- migration diagnostics for missing content,
- no catalog definition duplication,
- deterministic restore,
- transaction-safe save points,
- optional checkpoint breadcrumbs for diagnostics.

### Exit Gate

A representative clean session can be snapshotted, serialized by a host, restored, validated against a catalog, and resumed into equivalent framework runtime state without placing a save-file format inside the framework.

### Track R Completion

Track R adds serializer-neutral persistence contracts to `JRPG.Framework` and a noninteractive console-host proof command, not an interactive save menu.

- Added `RuntimeSaveGameSnapshot` contract version `1`, aggregating actor snapshots, party/stock state, inventory, equipped items, wallet, field/dungeon progress, Compendium state, typed battle knowledge, session progress, optional host context, and ordered checkpoint entries.
- Added `RuntimeKnowledgeSnapshot`, `RuntimeSessionProgressSnapshot`, `RuntimeCheckpointLogSnapshot`, `IRuntimeSaveValidator`, aggregated diagnostics, and `RequireValidSnapshot()`.
- Validation checks duplicate runtime IDs, missing party/stock/form/checkpoint actor references, catalog-backed actor/entity/skill/item/equipment/dungeon/ailment/Compendium references, and malformed checkpoint ordering. Catalog definitions are referenced by qualified ID and are not duplicated into saves.
- Added `--clean-save-demo` in the console host. The demo loads retained clean packs, builds a representative save snapshot, serializes/deserializes it through host-owned `System.Text.Json` DTOs, validates the restored snapshot, rebuilds actor runtime state, prints ordered proof events, and exits without input.
- The framework persistence contracts expose no `System.Text.Json`, filesystem, console, Godot, Newtonsoft, `Database`, `Combatant`, `Persona`, `SkillData`, or `ItemData` types. Existing internal schema deserialization remains the only framework JSON implementation surface.
- The parity ledger now has `persistence_snapshots` as `clean_foundation`; no legacy runtime file is removable, no interactive save/load consumer is migrated, and production `Data/Jsons` remains unchanged.
- Focused Track R verification passed: `RuntimePersistenceSnapshotTests`, `CleanSaveDemoHostTests`, and the parity-ledger check reported 9 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 721 passed, 0 failed, 0 skipped; `dotnet build JRPG.sln --no-restore --no-incremental /clp:Summary` passed with 98 warnings and 0 errors.
- Demo verification passed: clean battle ended in player-team victory, clean field effects completed successfully, and clean save restored 2 actors, 1 item stack, and dungeon floor 5.
- Quality gates passed: `git diff --check` reported no whitespace errors, Track R runtime persistence contracts had no forbidden host/serializer/legacy references, and `Data/Jsons` had no modified files.

## Track S: Legacy Retirement And Archive Gate

### Goal

Move only code and data proven obsolete by completed consumer migrations out of the active runtime and into `ArchiveDocs/LegacyFramework`.

Track S is not a declaration that the framework is finished. The architecture is ready to continue production on, but the framework still has incomplete gameplay authority in areas such as full AI/tactics policy, production content authority, authored ruleset binding, interactive save/load, and later feature work. Track S only retires specific legacy files when their replacement is already proven.

### Archive-First Policy

Old code should not be deleted outright. When a file is eligible for retirement, preserve it under `ArchiveDocs/LegacyFramework/<track-or-gate>/<original-relative-path>`, then remove it from active project compilation, runtime loading, tests, and documentation references.

No active legacy source is archived merely because a framework service exists. Adapter-backed systems remain active until the consumer has migrated and the parity ledger authorizes retirement.

### Per-File Archive Checklist

Before archiving a legacy file:

1. Name every behavior it owns.
2. Link the framework replacement.
3. Link parity and integration tests.
4. Confirm no production references remain.
5. Confirm no retained dataset depends on its shape.
6. Run the interactive host workflow it previously supported.
7. Promote the matching parity-ledger capability to `clean_parity`, with `consumerMigrated: true` and `removalAuthorized: true`.
8. Move the file into `ArchiveDocs/LegacyFramework` with its original relative path preserved.
9. Review the archive/removal from active code as a focused change.
10. Update active documentation and archive useful historical notes.

### Archive Order

Archive leaf adapters and duplicate implementations before central models:

1. name/string inference helpers,
2. duplicate effect implementations,
3. direct static database reads in migrated consumers,
4. migrated managers/processors,
5. broad legacy actor models,
6. legacy DTOs and loaders,
7. legacy datasets.

### Prohibited Cleanup

- deleting a subsystem because a demo covers one example,
- deleting datasets because schemas exist,
- deleting tests solely to make a new architecture pass,
- converting the application into a library before an interactive host replaces it,
- mixing several subsystem retirements into one commit,
- moving active compatibility code into the archive while `removalAuthorized` is still `false`.

### Exit Gate

The framework and host retain all approved capabilities, all parity tests pass, legacy searches are clean for the retired surface, and the old path is unreachable because every consumer has migrated. Retired files are preserved under `ArchiveDocs/LegacyFramework` instead of being deleted outright.

## Track T: Framework Completion Roadmap And First Production Vertical Slice

### Goal

Continue production on the new architecture without pretending the framework is finished and without archiving active compatibility code.

Track T is governed by [Track T Framework Completion Roadmap](t-track-plan.md). It starts with a framework completion audit, then moves toward authored ruleset binding, a small original clean content pack, a clean runtime consumer slice, and only then a narrow archive-candidate review.

### Required Boundaries

- Legacy `Data/Jsons` remains prototype-only evidence, not a clean production conversion queue.
- `ArchiveDocs/LegacyFramework` remains policy-only until a specific capability reaches `clean_parity`, `consumerMigrated: true`, and `removalAuthorized: true`.
- Framework completion work must advance reusable rules, state, content, or host contracts.
- The console host remains the compatibility/demo host while clean production slices mature.

### Track T1 Completion Target

The roadmap exists, identifies the current framework gaps, and records the next sequence without modifying production JSON, archiving source, or changing runtime behavior.

### Track T1 Completion

Track T1 establishes the build-forward lane without changing runtime behavior or moving legacy source.

- `docs/t-track-plan.md` now records the active T1-T5 sequence: completion audit, authored ruleset binding, original clean content vertical slice, clean runtime consumer slice, and archive candidate review.
- The active documentation index, architecture overview, and production baseline link to the Track T roadmap.
- `FrameworkCompletionRoadmapTests` enforce the key guardrails: the Track T policy exists, `ArchiveDocs/LegacyFramework` remains policy-only, and the recovery parity ledger authorizes no removals before clean parity.
- Focused Track T tests passed: 4 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 725 passed, 0 failed, 0 skipped.
- Build verification passed: the framework project built with 0 warnings and 0 errors; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: clean battle ended in player-team victory, clean field effects completed successfully, and clean save restored 2 actors, 1 item stack, and dungeon floor 5.
- Quality gates passed: `git diff --check`, framework forbidden-reference search, and `git status --short -- Data/Jsons`.
- No production JSON, gameplay rule, parity-ledger removal authorization, or archived source changed.

## Test Strategy

### Unit Tests

Pure formulas, policies, resolvers, validators, state transitions, and transaction rules.

### Characterization Tests

Freeze legacy behavior before replacement. These may be deleted only after equivalent parity tests exist and the behavior disposition is documented.

### Parity Tests

Execute legacy and framework implementations with equivalent input and deterministic random sequences, then compare approved outputs.

### Contract Tests

Schemas, strict deserialization, validation codes, public API boundaries, immutability, and host neutrality.

### Integration Tests

Load real content packs, hydrate runtime state, execute complete workflows, and inspect snapshots/events.

### Host Tests

Script `IGameIO` for console workflows and add adapter tests for Godot-facing integration.

### End-To-End Gates

- ordinary interactive startup,
- representative full battle,
- field item and skill use,
- party and stock organization,
- shop and hospital transaction,
- dungeon traversal and boss completion,
- negotiation and recruitment,
- fusion and compendium recall,
- save and reload,
- clean technical demos.

## Branch And Commit Strategy

- Keep the recovery branch as the integration line until the migration approach is proven.
- Create focused feature branches from the recovery baseline.
- Merge one parity track or vertical slice at a time.
- Avoid rebasing away historical redesign commits that explain current clean APIs.
- Tag or record known-good interactive milestones.
- Require a clean worktree, full tests, interactive smoke test, and relevant parity workflow before each merge.

Recommended commit categories:

```text
test: characterize <subsystem>
docs: approve <rule or contract>
data: add <content family> contract
runtime: add <framework service>
host: migrate <interactive workflow>
cleanup: retire replaced <specific component>
```

## Progress Reporting

Track progress by capability, not by lines deleted or number of clean classes added.

Each progress report should state:

- capabilities newly represented,
- capabilities newly executable,
- real host consumers migrated,
- parity tests added,
- unresolved rule decisions,
- legacy files now eligible for removal,
- behavior still available only through the legacy path.

## Definition Of Done

The framework migration is complete only when:

1. Every protected capability has a final disposition: preserved, deliberately changed, or deliberately retired.
2. Every deliberate change has an approved decision and tests.
3. The framework contains host-neutral contracts and rules for all preserved capabilities.
4. The interactive host consumes the framework for all gameplay systems.
5. A Godot adapter can consume the same framework without core changes.
6. Production content uses approved schemas and passes complete graph validation.
7. Mutable session state can be saved and restored.
8. Full battle, field, party, inventory, shop, dungeon, negotiation, growth, fusion, and compendium workflows pass end-to-end tests.
9. Legacy code is removed only where no consumer remains.
10. Documentation describes the implemented system and links archived history separately.

The success metric is not that the repository looks cleaner. The success metric is that it becomes reusable while remaining at least as capable, understandable, testable, and playable as the prototype it replaces.
