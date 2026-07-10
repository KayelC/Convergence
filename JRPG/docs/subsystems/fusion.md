# Fusion Subsystem

> **Status: Current implementation reference.** Track N moves fusion rule decisions and Compendium state checks into framework services. Track O10 routes Cathedral presentation through typed console-host results while preserving the interactive workflow and legacy datasets. Phase 7-30 adds clean catalog-backed result calculation for original content, without replacing the Cathedral transaction flow.

## Purpose

`Logic/Fusion` implements Cathedral-style fusion, sacrificial fusion, fusion accidents, skill inheritance, rank mutation, Mitama stat boosts, compendium registration, and recall.

## Key Classes And Responsibilities

- `FusionConductor`: root Cathedral workflow and menu loop.
- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs`: framework contracts and services for result resolution, planning, slot calculation, mutation, accident inheritance, preview snapshots, transaction assessment, and Compendium state.
- `JRPG.Framework/Logic/Fusion/CatalogFusionContentRepository.cs`: framework adapter that feeds qualified `GameDataCatalog` fusion recipes, entities, and skills into `IFusionContentRepository` for clean original content.
- `LegacyFusionContentAdapter`: console adapter that maps `Database.FusionRecipes`, `PersonaData`, `SkillData`, and live participants into framework snapshots.
- `FusionCalculator`: compatibility facade over the framework result resolver and planning helpers.
- `FusionMutator`: dispatches committed transactions and handles compendium recall.
- `FusionContext`: transaction object passed to strategies.
- `CompendiumRegistry`: in-memory console facade over framework Compendium registration, recall-cost, and recall-assessment contracts, with detailed presentation results for registration and recall assessment.
- `CathedralUIBridge`: participant selection, inheritance selection, preview/confirmation, compendium menus, and typed presentation records for the console Cathedral surface.
- `FusionStrategyRegistry`: maps `FusionOperationType` to strategy implementations.
- `StandardFusionStrategy`: creates new demons/personas from normal fusion.
- `RankMutationStrategy`: handles Element-driven rank up/down.
- `StatBoostStrategy`: handles Mitama stat boosts.
- Fusion messaging/logging: `IFusionMessenger`, `FusionMessenger`, `FusionLogger`, and `FusionEvents`.

## Main Runtime Flows

### Cathedral Entry

`FusionConductor.EnterCathedral` loops over Cathedral menu choices:

- Binary Fusion.
- Sacrificial Fusion.
- Browse Compendium.
- Register Demon.
- Back.

Sacrificial fusion requires one extra participant and grants extra inheritance capacity.

### Participant Selection

Participant pools depend on player class:

- Operators draw demons from active party plus demon stock.
- Wild Cards draw from active persona plus persona stock.

The conductor creates transient `Combatant` wrappers for persona participants so the calculator can reason over a consistent combatant shape.

### Result Prediction

`FusionCalculator.CalculateResult` adapts parents into framework snapshots, then `FusionResultResolver` handles:

- Mitama override into `StatBoostFusion`.
- Specific ID recipe lookup.
- Race recipe lookup.
- Literal entity result IDs.
- Element rank up/down signals.
- Normal race fusion using average base level plus a random offset.
- Accident chance: 1% normally, 12% at Full Moon.

Recipe lookup is parent-order neutral. Specific ID pairs are checked before race pairs.

For clean original content, `CatalogFusionContentRepository` adapts catalog-authored recipes into the same resolver contract. Structured recipe results preserve operations such as `create_entity` and `rank_offset` instead of relying on legacy string tokens. The Training Annex host currently exposes this only as a non-mutating `Calculate Fusion Results` proof command.

Phase 7-31 extends that proof through `FusionPlanningService`: Training Annex records ordinary and sacrificial slot counts, selectable inherited skills, blocked or already-known display reason codes, and a deterministic accident inheritance sample. The sample uses generic mutation metadata (`echo_strike` tier 1 and `shell_bash` tier 2 in `training_physical`) so mutation is authored data, not a display-name rule.

### Skill Inheritance

The framework planner builds a unique parent skill pool, filters candidates through the typed Track 10 inheritance evaluator, returns ineligible skills separately for UI display, and calculates inheritance slots from legal unique skill count.

Track O10 carries the framework planner's inheritance display entries into the console `FusionPlan` as presentation evidence. The bridge still renders legacy display names and the existing "Already Known" / "Exclusive" labels, but detailed results now expose the framework reason codes for tests and future host adapters.

Slot scale:

- 1 to 6 legal skills: 1 slot.
- 7 to 9: 2 slots.
- 10 to 13: 3 slots.
- 14 to 18: 4 slots.
- 19 to 23: 5 slots.
- 24 or more: 6 slots.

Sacrificial fusion adds 2 slots, with the UI cap applied by the conductor.

### Transaction Commit

After confirmation, `FusionMutator.ExecuteFusionTransaction` dispatches to a strategy. Track O10 adds `ExecuteFusionTransactionDetailed` so the console conductor can observe applied/rejected transaction presentation results without changing the strategy-owned mutation path.

- Standard fusion consumes participants, creates a child, applies chosen skills, transfers sacrifice EXP as `LifetimeEarnedExp / 1.5`, recalculates resources, and adds/summons the result.
- Rank mutation replaces a non-Element parent with the target rank result and preserves selected skills plus stat modifiers.
- Stat boost fusion replaces the target with a boosted version based on Mitama type and caps stats at 40.

### Compendium

`CompendiumRegistry` stores demon snapshots by normalized species ID while mirroring those entries into framework `CompendiumStateSnapshot` records. Recall cost combines base shop price fallback, level premium, stat premium, and skill premium. Track O10 exposes detailed registration, recall-list, recall-assessment, and recall-transaction presentation results; `FusionMutator.FinalizeRecall` still spends Macca and adds recalled demons/personas back to the appropriate stock.

Track N intentionally fixes the previous shallow-copy behavior: registered entries deep-clone active Persona skill lists, stat modifiers, learn tables, affinities, and growth fields. Recalled clones can be modified without mutating the stored Compendium entry.

Future knowledge integration: once clean battle UI and clean Compendium ownership are connected, registered or owned familiar entities may seed the player's battle knowledge snapshot. A demon recruited, fused, recalled, or registered in the Compendium can therefore reveal its known affinities/resistances immediately when encountered later, without granting that memory to ordinary enemy AI.

## Important State And Invariants

- Interactive Cathedral fusion still requires `Database.FusionRecipes` and `Database.Personas`; the legacy content adapter is the only layer that reads them for framework fusion services.
- Clean original-content fusion result calculation can use catalog fusion recipes directly through `CatalogFusionContentRepository`.
- Operators use `DemonStock`; Wild Cards use `ActivePersona` and `PersonaStock`.
- Active demons are still owned through unified `DemonStock`.
- Mitama plus Mitama is unsupported.
- Elements cannot receive Mitama stat boosts.
- Exclusive skills can be displayed but not inherited.
- Fusion accident skill mutation only applies to skills with valid family/rank evolution data.
- Legacy transaction strategies remain compatibility code until a dedicated parity review replaces them.

## Data Dependencies

- `fusion_table.json` drives the protected legacy Cathedral specific ID, race, and rank operation mapping.
- Clean catalog packs may provide fusion recipe documents for framework-owned original content result calculation.
- `entity_database.json` drives race, rank, level, base skills, learned skills, and stat/affinity data.
- `skills_database.json` drives exclusive checks, family/rank mutation, and inheritance pool legality.
- Shop inventory may influence compendium recall costs.

## Extension Points

- Add legacy Cathedral recipes in `fusion_table.json`.
- Add clean original-content recipes through catalog fusion documents.
- Add a new fusion operation by extending `FusionOperationType`, writing an `IFusionStrategy`, and registering it.
- Add new framework-supported result behavior in `FusionRuntimeServices` and adapt console presentation only after the rule exists.
- Add new inheritance restrictions through typed skill/entity definitions and `FusionInheritanceEvaluator`.
- Add new compendium persistence by replacing or extending `CompendiumRegistry`.

## Caveats

- Full strategy removal is not authorized yet; old strategy classes remain the console transaction adapters.
- Some strategy paths assume non-null active personas and matching database records.
- Accidents are revealed after confirmation, so previewed choices can be discarded by design.
