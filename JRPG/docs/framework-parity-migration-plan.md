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
| Press Turn | `PressTurnEngine` | Typed overload exists; full interactive orchestration not migrated |
| Basic attack, guard, pass, skill, item, analyze, swap | `ActionProcessor`, `BattleConductor` | Skills/items partial; remaining actions missing |
| Typed damage, recovery, cures, buffs, shields | Legacy effects and clean executors | Clean foundation exists; parity incomplete |
| Ailment restrictions and lifecycle | `StatusRegistry` | Definitions exist; lifecycle consumer incomplete |
| Passive startup and turn-end behavior | `StatusRegistry` | Trigger system exists; complete content/rules missing |
| Enemy AI and tactics | `BehaviorEngine`, `BattleConductor` | Minimal deterministic selector only |
| Affinity knowledge and analysis | `BattleKnowledge` | Clean stores exist; interactive use incomplete |
| Negotiation, demands, recruitment | `NegotiationEngine`, `BattleConductor` | Missing |
| EXP and Macca battle rewards | `CombatMath`, `BattleConductor` | Missing |
| Inventory quantities | `InventoryManager` | Item executor reports consumption only |
| Equipment ownership and equipping | `InventoryManager`, `FieldServiceEngine` | Missing clean definitions and runtime |
| Economy and Macca transactions | `EconomyManager` | Missing |
| Shops and buy/sell pricing | `ShopEngine`, `ShopUIBridge` | Missing |
| Hospital restoration | `FieldServiceEngine` | Missing |
| Field skill and item use | `FieldServiceEngine` | Clean effect execution exists; interactive workflow not migrated |
| City and field navigation | `FieldConductor` | Missing framework state machine |
| Dungeon traversal and terminals | `DungeonManager`, `DungeonState`, `ExplorationProcessor` | Missing |
| Encounter and boss preparation | `ExplorationProcessor` | Missing encounter schema/runtime |
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

## Track I: Ailment, Status, Duration, And Passive Lifecycle

### Goal

Port all `StatusRegistry` behavior without retaining skill-name parsing.

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

Reauthor the 11 ailment records against the approved ailment schema and replace name/description inference with explicit triggers, modifiers, recovery, and turn behavior.

### Tests

- port every `StatusRegistryTests` case,
- add lifecycle order tests,
- reserve/deployment suspension,
- cleanup scope tests,
- multiple-passive ordering and activation limits,
- deterministic recovery and flee outcomes.

### Exit Gate

No migrated status rule reads a skill or ailment display name. The interactive battle path uses the clean lifecycle dispatcher.

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

## Track O: Console Host Migration

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

Build a small Godot-facing integration sample or adapter test that loads content, creates actors, executes an action, consumes events, and restores a snapshot.

### Exit Gate

Godot integration requires adapter code, not changes to core rules or domain definitions.

## Track Q: Production Data Reauthoring And Verification

### Goal

Move retained content into approved schemas only after corresponding consumers exist.

### Per-Family Procedure

1. Freeze legacy record count and identifiers.
2. Classify every record as retain, split, replace, defer, or intentionally omit.
3. Resolve ambiguous mechanics manually; do not infer from prose where the result changes behavior.
4. Author or convert into the approved schema.
5. Validate structurally and semantically.
6. Compare reference graphs and report every omission.
7. Run representative runtime tests using the converted records.
8. Switch one real consumer to the new catalog.
9. Keep the legacy source until the consumer migration is complete.

### Mandatory Reports

- record counts before and after,
- ID mapping,
- omitted records with reasons,
- unresolved references,
- behavior decisions,
- conflict list,
- runtime coverage list.

### Special Cases

- physical affinity conflicts require manual entity decisions,
- Navigator skills require their own support-system contract,
- special skills require explicit typed behavior or registered handlers,
- franchise-derived demonstration content should eventually be separated from framework-required examples.

### Exit Gate

All retained production content loads through the new catalog and drives the migrated host. No silent drops or inferred defaults remain.

## Track R: Persistence, Replay, And Compatibility

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
- optional command/event logs for replay tests.

### Exit Gate

A representative complete session can save, reload, and continue in both headless integration tests and the interactive host.

## Track S: Legacy Retirement

### Goal

Remove only code and data proven obsolete by completed consumer migrations.

### Per-File Removal Checklist

Before deleting a legacy file:

1. Name every behavior it owns.
2. Link the framework replacement.
3. Link parity and integration tests.
4. Confirm no production references remain.
5. Confirm no retained dataset depends on its shape.
6. Run the interactive host workflow it previously supported.
7. Review the deletion as a focused change.
8. Update active documentation and archive useful historical notes.

### Removal Order

Remove leaf adapters and duplicate implementations before central models:

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
- mixing several subsystem retirements into one commit.

### Exit Gate

The framework and host retain all approved capabilities, all parity tests pass, legacy searches are clean, and the old path is unreachable because every consumer has migrated, not because files were deleted first.

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
