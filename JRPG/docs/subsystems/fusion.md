# Fusion Subsystem

> **Status: Current implementation reference.** Track N moves fusion rule decisions and Compendium state checks into framework services. Track O10 routes Cathedral presentation through typed console-host results while preserving the interactive workflow and legacy datasets. Phases 7-30 through 7-33 add clean result, planning, preview, and transaction proofs. Phase 7-34 removes embedded strategy assumptions from the framework and requires explicit host-selected policies without replacing the Cathedral flow.

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

Those listed odds and legacy race operations are now console compatibility configuration, not framework defaults. `LegacyFusionStrategyPolicies` registers the old Moon Phase accident policy, catalyst combinations, unstructured result-token interpretation, slot table, sacrifice bonus, and mutation behavior for the Cathedral. Clean hosts construct their own `FusionPolicyRegistry` and may omit or replace any optional mechanic.

For clean original content, `CatalogFusionContentRepository` adapts catalog-authored recipes into the same resolver contract. Structured recipe results preserve operations such as `create_entity` and `rank_offset` instead of relying on legacy string tokens. The Training Annex host consumes that repository for result calculation, planning, preview, and its current sample transaction path.

CodeReview-7-1 makes that adapter faithful to the authored contract. Schema v1 recipes contain exactly two typed parent selectors; each selector retains whether its ID names an entity or a race, and mixed entity/race recipes are supported in either participant order. When multiple recipes match, entity-specific selectors take precedence over race-wide selectors and authored repository order breaks ties. Structured results are authoritative. The optional compatibility result token exists only for protected legacy recipe interpretation, and malformed non-binary catalog data is rejected rather than silently omitted. Sacrificial fusion remains a separate planning input, not a third recipe parent.

Phase 7-31 extends that proof through `FusionPlanningService`: Training Annex records ordinary and sacrificial slot counts, selectable inherited skills, blocked or already-known display reason codes, and a deterministic accident inheritance sample. The sample uses generic mutation metadata (`echo_strike` tier 1 and `shell_bash` tier 2 in `training_physical`) so mutation is authored data, not a display-name rule.

Phase 7-32 adds `Preview Fusion Result` to the clean Training Annex host. The host presents inherited-skill choices for a sacrificial Echo Adept + Bramble Runner + Ashling sample, then validates the selected skills with `FusionInheritanceSelectionValidator` before creating a `FusionPreviewSnapshot`. Confirmation records the accepted preview but intentionally does not mutate party/stock, parent actors, inventory, wallet, or Compendium state.

Phase 7-33 adds `Commit Fusion Transaction` to the clean Training Annex host. CodeReview-7-3 moves its reusable algorithm into `FusionTransactionService`. Preparation accepts the validated inheritance token, actual party/stock snapshot, typed result owner, proposed identity, result ownership metadata, and an optional retained stat-boost actor snapshot. It validates distinct participants and all owned references, derives duplicate ownership and capacity, simulates consumption and placement through the injected stock service, and returns an immutable token without constructing an actor. After host confirmation, commit accepts no new construction choices: it rejects stale state or constructs/restores the catalog actor and returns one typed before/after result. Rejected commits expose planned evidence separately from applied consumption. The host retains menus, ID generation, confirmation, and application of an `Applied` result.

Phase 7-34 requires explicit strategy policies. Neutral `create_entity` and `rank_offset` results are authored operations. `stat_boost` and `special` results require registered policy handlers. Recipe accident and mutation IDs survive catalog mapping and missing runtime registrations reject with typed diagnostics. Sacrifice availability and bonus slots are decided by `IFusionSacrificePolicy`; slot scaling is decided by `IFusionInheritanceSlotPolicy`; optional host/session facts travel through `FusionPolicyContext`. The framework contains no Moon Phase, Mitama, Element-catalyst, catalyst-name, or fixed probability assumptions.

CodeReview-7-4 closes the strategy-context lifecycle: `FusionPlanningResult` retains the immutable context supplied to planning and accident inheritance passes it to every mutation policy. The original one-argument slot-count helper is explicitly context-free; hosts with context-sensitive slot policies use the overload that requires `FusionPolicyContext`.

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

Phase 7-35 implements the clean Compendium runtime separately from the protected registry. `CompendiumRuntimeService` registers catalog-identified actor progression/stat/skill snapshots and recalls them through catalog actor reconstruction, caller-selected Demon or Persona stock placement, and wallet spending as one immutable transaction result. Training Annex uses typed selection IDs and persists both entries and recalled dynamic actors.

CodeReview-7-2 closes the recall identity hole. A caller-supplied recall instance ID is checked against the complete party/stock graph before reconstruction, placement, cost assessment, or wallet spending. The recall result exposes `DuplicateRuntimeInstanceId`; stock transitions expose `RuntimeInstanceIdInUse` for cross-role collisions. Save validation rejects illegal cross-role reuse and entity/reference disagreement while deliberately allowing an active owned demon to appear in both active party and Demon stock.

`FamiliarEntityKnowledgeService` is an opt-in companion service. A host supplies the entity IDs considered familiar through recruitment, fusion, recall, registration, or another approved ownership rule. The service imports typed defenses into persistent player knowledge only; it has no reference to or side effect on encounter-local enemy AI knowledge.

## Important State And Invariants

- Interactive Cathedral fusion still requires `Database.FusionRecipes` and `Database.Personas`; the legacy content adapter is the only layer that reads them for framework fusion services.
- Clean original-content fusion result calculation, preview confirmation, and the Training Annex transaction proof can use catalog fusion recipes directly through `CatalogFusionContentRepository`.
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
- Add host-specific Compendium presentation or persistence by consuming `CompendiumRuntimeService` and serializer-neutral snapshots; do not place host storage or UI in the framework.

## Caveats

- Strategy policy extraction is complete for framework resolution/planning, but old Cathedral transaction strategy classes remain active console adapters and are not authorized for removal.
- Some strategy paths assume non-null active personas and matching database records.
- Accidents are revealed after confirmation, so previewed choices can be discarded by design.
