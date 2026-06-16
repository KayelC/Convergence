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

The distinction matters in the framework progression policies used by `StatProcessor`, `PartyManager`, `FusionConductor`, `FusionMutator`, and field/battle UI bridges.

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

## Clean Runtime State Foundation

Track D adds a framework runtime-state surface beside the legacy `Combatant` and `Persona` models. It gives actor identity, controller/team/owner relationships, active/reserve/deployed state, progression, resources, stats, skill loadouts, active form references, persona/demon stock references, equipment slots, battle statuses, analysis, and passive activation counts typed snapshot homes.

This is not a gameplay migration yet. The interactive console still mutates `Combatant`, `Persona`, `InventoryManager`, `PartyManager`, `StatusRegistry`, and the Cathedral services. The new runtime snapshots are save/presentation/replay contracts for later tracks, and only resource mutation has a transaction result service so far.

## Clean Stat And Growth Foundation

Track E moves the stat, resource, EXP, level-up, Persona growth, stat allocation, and rollback formulas into framework progression policies. The console `StatProcessor`, `GrowthProcessor`, and `Persona` methods now delegate through a compatibility adapter, so existing status screens, battle math, field stat allocation, reward level-ups, fusion EXP transfer, compendium recall, and factory scaling keep the same behavior while sharing the clean rules.

The preserved default formulas are:

- Demons use active-form stats at full value.
- Operators use character stats plus accessory modifiers, with no active-form contribution.
- Persona Users and Wild Cards use character stats plus active-form weights: 40% Strength/Magic, 25% Vitality/Agility, and 50% Luck.
- Raw stats cap at 40 before stage multipliers.
- Buff and debuff multipliers remain 1.4 and 0.6, and matching aliases stack multiplicatively.
- EXP required is `(int)(1.5 * level^3)`.
- Maximum HP is `min(666, baseHP + vitality * 5)` and maximum SP is `min(333, baseSP + magic * 3)`.
- Level-up applies the max-resource delta to current HP/SP; ordinary recalculation caps current values without healing.

Ruleset JSON does not own these parameters yet. Track E uses named default policy/config records until a later ruleset migration approves authored progression profiles.

## Clean Party And Stock Foundation

Track F moves active/reserve party transitions, stock capacity, unified demon stock, active Persona swaps, and fusion inventory consume/replace steps into framework transition services. `PartyManager` remains the public console API, but its mutation methods now delegate through a console adapter that builds framework snapshots, applies successful results, and preserves the same live `Combatant`/`Persona` references.

The preserved defaults are:

- active party capacity remains four;
- stock capacity remains 3/5/7/10/12 by owner level;
- active demons remain in `DemonStock` while also appearing in `ActiveParty`;
- returned demons leave `ActiveParty` but remain owned;
- dismissed or consumed demons leave both active party and stock;
- Persona swaps exchange the active Persona with a stock entry and then use existing HP/SP recalculation and capping;
- adapter-owned per-session runtime IDs bridge legacy object references into framework commands without exposing legacy types to the framework.

This is still not legacy removal. Field menus, battle COMP menus, compendium, fusion conductors, factories, and save/persistence ownership remain console-host systems until their later migration tracks.

## Production Combat Ruleset Foundation

Track G moves damage, hit/evasion, critical, instant-death, initiative, EXP, Macca, Weak/Resist, guard, rigid-body, charge, drain, and reflection formulas into `ProductionCombatRuleset` in the framework. The console `CombatMath` and `DamageHandler` APIs remain in place for existing battle callers, but now delegate through a console-owned adapter that translates live `Combatant` state into clean policy inputs.

The production damage order is:

1. target validity remains owned by the action/effect caller;
2. hit/evasion is resolved from accuracy, agility, luck, and adapter-supplied modifiers;
3. shields are resolved before base affinity;
4. Break, temporary override, passive replacement, and base affinity resolve before damage application;
5. Null, Repel, and Absorb retain their Press Turn severity;
6. critical and rigid-body behavior are resolved from typed status flags;
7. guard halves damage, suppresses critical, and normalizes Weak to Normal;
8. damage modifiers, charge, variance, Weak/Resist, and drain use named policy values;
9. defeat interception, knowledge recording, and Press Turn aggregation remain owned by the existing action/effect paths.

Legacy skill-name checks for Boost/Amp/Driver, Dodge/Evade, Vidyaraja's Blessing, Apt Pupil, Rebellion, Arms Master, and Spell Master remain adapter or console-effect concerns until Track H/I content migration gives them fully typed definitions. Ruleset JSON still does not author these combat constants; Track G creates named code defaults and parity tests first.

## Clean Action Execution Foundation

Track H adds a framework-owned action facade for the clean path. `BattleActionExecutor` accepts typed commands for basic attack, skill, item, guard, pass, analyze, escape, Persona swap, demon summon/return/swap, tactics change, negotiation, and host-special actions. Assessment and execution share the same services, so eligibility, target resolution, cost checks, item availability, and party-stock transitions cannot drift apart inside the clean API.

The framework action result reports:

- ordered action events for presentation adapters;
- effect results and host-action request IDs;
- item consumption decisions and whether a host reservation committed;
- party-stock transition results;
- Press Turn consumption, normal consumption, pass consumption, phase termination, or free/no-turn actions.

Field recovery skills and items in the clean field demo now use this action facade with an explicit host-owned inventory reservation adapter. The interactive console battle still preserves its legacy skill/item/effect flow, but guard and pass are now coordinated through a console compatibility adapter over the framework action executor. Full battle orchestration, AI/tactics behavior, negotiation/recruitment, inventory ownership, and production content reauthoring remain later tracks.

## Clean Status Lifecycle Foundation

Track I moves the approved status lifecycle rules into `BattleStatusLifecycleService`. The console `StatusRegistry` still exposes the same methods to battle and field callers, but ailment infliction, turn-start restrictions, turn-end effects, natural recovery, duration ticking, cleanup scopes, and stat-stage mutation now delegate through a console adapter where strict parity exists.

The preserved parity rules are:

- one active major ailment at a time through a shared exclusivity group;
- Poison deals 13% of maximum HP at turn end, minimum 1, and remains lethal;
- Sleep restores 10% maximum HP and 10% maximum SP at turn end;
- natural recovery chance is `20 + Luck / 2`;
- Panic has a 50% skip chance;
- Fear checks 15% flee or return-to-COMP first, then 40% skip;
- Guard clears at actor turn start and blocks ailment application;
- reserve actors suspend ailment ticking, poison damage, sleep recovery, and turn-end passive recovery;
- stat stages clamp at `-4..+4`, while legacy redundancy still treats `+3/-3` as already enough;
- zero evasion multipliers are valid for legacy immobilization-style ailments.

`convergence.status_lifecycle_demo` reauthors the 11 legacy ailments as clean content for tests and future hosts. The legacy `status_ailments.json` file remains unchanged and continues to feed ordinary console loading.

## Extension Mindset

When adding new gameplay content:

- Add raw data to JSON when the system is already data-driven.
- Add enum values only when new behavior branches are truly needed.
- Add new battle effect strategies when a skill category requires new rule logic.
- Add field/fusion/battle bridge options only after the engine behavior exists.
- Keep state mutation centralized in managers, engines, processors, or strategies rather than UI bridges.
