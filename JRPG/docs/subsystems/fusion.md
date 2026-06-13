# Fusion Subsystem

> **Status: Current implementation reference.** Existing fusion and skill-mutation behavior is migration evidence; approved redesign contracts define future data shape.

## Purpose

`Logic/Fusion` implements Cathedral-style fusion, sacrificial fusion, fusion accidents, skill inheritance, rank mutation, Mitama stat boosts, compendium registration, and recall.

## Key Classes And Responsibilities

- `FusionConductor`: root Cathedral workflow and menu loop.
- `FusionCalculator`: predicts operation type, target result, accidents, inheritable skills, exclusive skills, skill mutation, and inheritance slots.
- `FusionMutator`: dispatches committed transactions and handles compendium recall.
- `FusionContext`: transaction object passed to strategies.
- `CompendiumRegistry`: in-memory demon snapshot registry and recall-cost calculator.
- `CathedralUIBridge`: participant selection, inheritance selection, preview/confirmation, compendium menus.
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

`FusionCalculator.CalculateResult` handles:

- Mitama override into `StatBoostFusion`.
- Specific ID recipe lookup.
- Race recipe lookup.
- Literal entity result IDs.
- Element rank up/down signals.
- Normal race fusion using average base level plus a random offset.
- Accident chance: 1% normally, 12% at Full Moon.

Fusion table mappings are registered commutatively so parent order does not matter.

### Skill Inheritance

The calculator builds a unique parent skill pool, filters out exclusive skills for actual inheritance, returns exclusive skills separately for UI display, and calculates inheritance slots from legal unique skill count.

Slot scale:

- 1 to 6 legal skills: 1 slot.
- 7 to 9: 2 slots.
- 10 to 13: 3 slots.
- 14 to 18: 4 slots.
- 19 to 23: 5 slots.
- 24 or more: 6 slots.

Sacrificial fusion adds 2 slots, with the UI cap applied by the conductor.

### Transaction Commit

After confirmation, `FusionMutator.ExecuteFusionTransaction` dispatches to a strategy.

- Standard fusion consumes participants, creates a child, applies chosen skills, transfers sacrifice EXP as `LifetimeEarnedExp / 1.5`, recalculates resources, and adds/summons the result.
- Rank mutation replaces a non-Element parent with the target rank result and preserves selected skills plus stat modifiers.
- Stat boost fusion replaces the target with a boosted version based on Mitama type and caps stats at 40.

### Compendium

`CompendiumRegistry` stores demon snapshots by normalized species ID. Recall cost combines base shop price fallback, level premium, stat premium, and skill premium. `FusionMutator.FinalizeRecall` spends Macca and adds recalled demons/personas back to the appropriate stock.

## Important State And Invariants

- Fusion requires `Database.FusionRecipes` and `Database.Personas`.
- Operators use `DemonStock`; Wild Cards use `ActivePersona` and `PersonaStock`.
- Active demons are still owned through unified `DemonStock`.
- Mitama plus Mitama is unsupported.
- Elements cannot receive Mitama stat boosts.
- Exclusive skills can be displayed but not inherited.
- Fusion accident skill mutation only applies to skills with valid family/rank evolution data.

## Data Dependencies

- `fusion_table.json` drives specific ID, race, and rank operation mapping.
- `entity_database.json` drives race, rank, level, base skills, learned skills, and stat/affinity data.
- `skills_database.json` drives exclusive checks, family/rank mutation, and inheritance pool legality.
- Shop inventory may influence compendium recall costs.

## Extension Points

- Add new fusion recipes in `fusion_table.json`.
- Add a new fusion operation by extending `FusionOperationType`, writing an `IFusionStrategy`, and registering it.
- Add new Mitama-like behavior in `FusionCalculator` and `StatBoostStrategy`.
- Add new inheritance restrictions in `FusionCalculator.GetInheritableSkills`.
- Add new compendium persistence by replacing or extending `CompendiumRegistry`.

## Caveats

- Compendium snapshots clone combatant scalar state and skill lists, but `ActivePersona` is copied by reference.
- Some strategy paths assume non-null active personas and matching database records.
- Accidents are revealed after confirmation, so previewed choices can be discarded by design.
