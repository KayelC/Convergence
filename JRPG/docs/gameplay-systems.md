# Gameplay Systems

> **Status: Current implementation reference.** This document describes the console prototype and may include systems scheduled for migration.

This document explains the main player-facing systems and the code that implements them.

## Boot And Scenario Selection

`Program.cs` starts the application, loads all JSON data, creates shared state managers, and asks the player to select a scenario. Scenarios configure the player's class, active persona, persona stock, demon stock, level, resources, and debug paths.

Important scenario concepts:

- Human: basic character with no persona or demon management.
- Persona User: has one active persona.
- Wild Card: has an active persona plus persona stock.
- Operator: commands demons through COMP-style demon stock and active party deployment.
- Debug scenarios jump directly into battle, stress fusion, test compendium registration, or verify unified stock behavior.

## Character Classes

`ClassType` in `Core/Enums.cs` controls major behavior branches.

- `Human`: baseline humanoid progression.
- `PersonaUser` and `WildCard`: humanoids with persona stat influence.
- `Operator`: humanoid with no persona stat influence, but access to demon stock and COMP party management.
- `Demon`: combatant whose stats and affinities come from its active persona template.

The distinction matters in `StatProcessor`, `PartyManager`, `FusionConductor`, `FusionMutator`, and field/battle UI bridges.

## Personas And Demons

`PersonaData` is the JSON-backed template. `Persona` is the live mask/entity data used at runtime. `CombatantFactory` turns templates into enemy or allied demon combatants.

Player-facing behavior:

- Personas supply affinities, stat modifiers, base skills, and learned skills.
- Demons are represented as `Combatant` instances with `ClassType.Demon` and an `ActivePersona`.
- Enemies receive all eligible skills immediately.
- Allied demons follow player-allied scaling and can be stored, summoned, fused, dismissed, recalled, or registered.

## Battle

Battles use an SMT-style Press Turn loop.

Core flow:

1. `BattleConductor.StartBattle` announces enemies and rolls initiative through `CombatMath.RollInitiative`.
2. Initial passive buffs are applied by `StatusRegistry.ProcessInitialPassives`.
3. Each side begins a phase with one full turn icon per alive actor through `PressTurnEngine.StartPhase`.
4. Actors process turn-start restrictions through `StatusRegistry.ProcessTurnStart`.
5. Player choices are collected by `InteractionBridge`; enemy choices are selected by `BehaviorEngine`.
6. `ActionProcessor` executes attacks, skills, items, swaps, and analysis.
7. Effects are resolved by `BattleEffectRegistry` strategies and `CombatMath`.
8. `PressTurnEngine` consumes, chains, or terminates icons according to hit outcome.
9. Battle completion resolves rewards, recruitment, cleanup, and compendium registration.

Important battle concepts:

- Weakness and critical hits convert full icons to blinking icons when available.
- Miss and null consume two icons.
- Repel and absorb terminate the phase.
- Guarding halves damage, blocks critical hits, and suppresses weakness.
- Rigid-body ailments such as Freeze, Shock, Bind, and Stun make physical hits critical.
- `BattleKnowledge` records discovered affinities and powers analysis/AI decisions.

## Negotiation And Recruitment

Negotiation is part of the battle subsystem through `NegotiationEngine`, using negotiation questions loaded from `questions.json`.

Player-facing behavior:

- Moon phase can block negotiation at Full Moon through `MoonPhaseSystem.IsNegotiationBlocked`.
- Demons can make demands involving resources or items.
- Recruitment interacts with party/stock limits through `PartyManager`.
- Familiar demons can use alternate dialogue paths.
- Successful recruitment can feed compendium registration after battle.

## Field, City, And Dungeon

`FieldConductor` owns non-combat navigation. It coordinates city services, inventory, status, party organization, dungeon entry, and fusion access.

Field gameplay is split between:

- `FieldServiceEngine`: hospital restoration, field items, field skills, equipment, stat allocation, and progression side effects.
- `DungeonManager`: current floor evaluation, fixed floor handling, random encounter generation, terminals, and boss state.
- `ExplorationProcessor`: floor transitions, entry triggers, terminal unlocks, and enemy hydration.
- Field bridges: menu rendering and choice collection.

Dungeon state is stored in `DungeonState`: current dungeon ID, current floor, max floor reached, unlocked terminals, and defeated bosses.

## Inventory, Equipment, Shops, And Economy

`InventoryManager` stores consumable quantities and owned equipment IDs. `EconomyManager` stores Macca. `ShopEngine` calculates buy/sell prices and executes transactions.

Important rules:

- Items are quantity-based.
- Equipment ownership is stored as ID lists by category.
- Buy prices decrease with Luck down to a 50% multiplier.
- Sell prices start at 50% and increase with Luck.
- Equipment metadata can be patched from shop entries if JSON-loaded objects are missing names or IDs.
- Equipping recalculates resources because accessories and defensive equipment can affect derived stats and pools.

## Fusion And Compendium

`FusionConductor` runs the Cathedral workflow. `FusionCalculator` predicts results, `FusionMutator` commits transactions, and strategies implement operation-specific mutation.

Supported fusion concepts:

- Binary fusion.
- Sacrificial fusion with EXP transfer.
- Race-table fusion from `fusion_table.json`.
- Specific-ID fusion recipes.
- Element rank up/down mutations.
- Mitama stat-boost fusion.
- Fusion accidents, with higher accident chance at Full Moon.
- Skill inheritance with exclusive-skill filtering and slot scaling.
- Compendium registration and recall.

Operators fuse demons from demon stock and active party references. Wild Cards fuse personas from active persona and persona stock.

## Clean Catalog Surface

Track C adds framework definitions, strict deserialization, validation, catalog qualification, repositories, and one small fixture pack for the content families that were still legacy-only: equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets.

This is a schema and catalog foundation only. It does not migrate `Database`, shop transactions, dungeon traversal, negotiation sessions, equipment ownership, fusion calculation, or economy state. Those systems remain protected by the legacy characterization tests until their dedicated runtime tracks connect real consumers to the clean definitions.

## Extension Mindset

When adding new gameplay content:

- Add raw data to JSON when the system is already data-driven.
- Add enum values only when new behavior branches are truly needed.
- Add new battle effect strategies when a skill category requires new rule logic.
- Add field/fusion/battle bridge options only after the engine behavior exists.
- Keep state mutation centralized in managers, engines, processors, or strategies rather than UI bridges.
