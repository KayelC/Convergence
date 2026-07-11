# Gameplay Systems

> **Status: Current implementation reference.** Read [Framework State And Roadmap](framework-state-and-roadmap.md) first for the current project map. This document describes current systems and may mention historical migration tracks. Those `Track X` labels are implementation history only; new work is routed by numbered `futurePhase` entries in the parity ledger and [Full Parity Capability Plan](full-parity-capability-plan.md).

This document explains the main player-facing systems and the code that implements them.

## Boot And Scenario Selection

`Program.cs` starts the application, loads all JSON data, creates shared state managers, and asks the player to select a scenario. Scenarios configure the player's class, active persona, persona stock, demon stock, level, resources, and debug paths.

Track O1 keeps that legacy startup path intact and adds a clean-catalog sidecar after `Database.LoadData`. The sidecar loads the retained clean reference/demo/catalog packs through the framework catalog loader for host-readiness checks; if that load fails, the console prints clean-catalog warnings and continues using the legacy runtime data.

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

1. `BattleConductor.StartBattle` announces enemies and creates a console adapter for `BattleEncounterRunner`.
2. `BattleEncounterRunner` resolves initiative through the adapter, then dispatches battle-start lifecycle.
3. Each active team begins a phase with one full turn icon per alive actor through `PressTurnEngine.StartPhase`.
4. Actors process turn-start restrictions once through the framework lifecycle boundary.
5. Player choices are collected by `InteractionBridge`; enemy choices are selected by `BehaviorEngine` through the framework turn handler.
6. `ActionProcessor` executes legacy attacks, skills, items, swaps, negotiation, and analysis as a host-owned action adapter.
7. Effects are resolved by `BattleEffectRegistry` strategies and `CombatMath`.
8. `BattleEncounterRunner` consumes, chains, passes, or terminates Press Turn icons from typed turn-consumption results.
9. Turn-end lifecycle runs after committed actions, passes, skips, and host-mediated turn-consuming commands.
10. Battle completion returns victory, defeat, escape, draw, cancellation, or fault to the console host.
11. Battle rewards, recruitment side effects, cleanup, and compendium registration remain console-owned after the runner reports the outcome.

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

The original clean Training Annex path now has a separate framework-first proof. `--clean-training-annex-play` exposes `Negotiate / Recruit`, presents clean prompt choices through the host, and resolves success/refusal/familiar outcomes through framework negotiation and recruitment services. A successful clean negotiation spends Macca through the bound economy service and adds Bramble Runner to Demon stock through `PartyStockTransitionService.AddDemonToStock`. This does not depend on legacy `questions.json` or `NegotiationEngine`, and it does not make authored demand records authoritative yet; demand-policy binding remains future work.

## Field, City, And Dungeon

`FieldConductor` owns non-combat navigation. It coordinates city services, inventory, status, party organization, dungeon entry, and fusion access.

Track M keeps the same console conductor and menus, but moves dungeon state transitions into the framework. `DungeonManager` now adapts legacy dungeon JSON into immutable runtime snapshots and delegates floor evaluation, movement, terminal unlocks, boss defeat state, barriers, dungeon exit, and random encounter selection to `RuntimeFieldDungeonService`.

Track O9 keeps the same visible dungeon traversal flow while adding typed console-host presentation results for floor actions, entry/terminal floor selection, movement, floor-entry events, barriers, boss requests, boss-defeat registration, and dungeon exits. Framework dungeon events are consumed deterministically, but structural events such as floor entry, terminal unlocks, encounter requests, and dungeon exit remain suppressed unless they replace an existing legacy message.

Field gameplay is split between:

- `FieldServiceEngine`: hospital restoration, field items, field skills, equipment, stat allocation, and progression side effects.
- `DungeonManager`: console compatibility facade over framework floor evaluation, fixed floor handling, random encounter generation, terminals, and boss state.
- `ExplorationProcessor`: field-side movement messages, entry triggers, battle handoff, and enemy hydration.
- Field bridges: menu rendering and choice collection. Plain field, city, inventory, status, dungeon, terminal, hospital-patient, and field-target menus now route through the framework host-command contracts before returning legacy-compatible values to existing conductors. Status summaries, Persona details, demon details, stock rows, organization rows, summon rows, and equipment slot labels now render through copied runtime projection data. Field item/skill selection, assessment, consumption decisions, party/stock organization choices, dungeon traversal events, and result narration now use typed console-host results while preserving legacy item/skill data, effect-string parsing, live party objects, `tartarus.json`, and battle handoff ownership. Rich preview menus outside the migrated status, field inventory, field party/stock, shop/hospital, and dungeon traversal surfaces remain legacy presentation surfaces.

Dungeon state is still stored in `DungeonState`: current dungeon ID, current floor, max floor reached, unlocked terminals, and defeated bosses. Direct debug/test warps preserve the legacy ability to move to a floor without increasing max-floor progress.

## Inventory, Equipment, Shops, And Economy

`InventoryManager` stores consumable quantities and owned equipment IDs. `EconomyManager` stores Macca. `ShopEngine` calculates buy/sell prices and executes transactions.

Track L moves the transaction rules behind those console facades into `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs`. `LegacyInventoryResourceAdapter` translates numeric legacy IDs, live equipment slots, and Macca state into immutable framework snapshots, then applies successful results back to `InventoryManager`, `EconomyManager`, and `Combatant`.

Phase 4-22 adds a clean equipment profile layer for original content. `RuntimeEquipmentProfileResolver` reads the actor's `RuntimeEquipmentSnapshot`, resolves equipped catalog definitions, exposes weapon basic-attack data, sums accessory stat modifiers, and reports missing or slot-mismatched equipment diagnostics. `--clean-training-annex-play` seeds and equips `practice_blade` and `focus_charm` through framework transactions, and manual Attack uses the equipped weapon profile rather than a hardcoded sample weapon.

Phase 4-23 makes the clean Training Annex economy policy explicit. Startup binds `standard_economy` and uses the returned resource-management service bundle for inventory, equipment, and wallet mutations; missing or invalid bindings stop startup instead of falling back. Battle rewards add Macca to the host-owned `RuntimeWalletSnapshot`, expose the typed before/after transaction, and persist the resulting balance through save/load. Wallet overflow, negative amounts, and insufficient funds are rejected without changing the original snapshot.

Phase 4-24 adds the first clean shop interaction for original content. Training Annex exposes `Training Supply`, lists authored item/equipment offers from the clean catalog, resolves fixed prices, stock gates, item stack limits, and equipment slots into runtime shop offers, and uses the same bound shop transaction service for row availability and execution. Successful purchases and sales mutate the clean inventory and wallet snapshots; purchased equipment can be equipped immediately through the clean equipment transition service. The default demo wallet remains empty, so funded purchase tests inject a starting wallet through the host boundary rather than hardcoding free money into the framework.

Phase 4-25 adds the clean Training Annex recovery facility. Treatment cost and eligibility come from the framework hospital service, successful treatment spends Macca through the bound economy service, restores HP/SP, removes removable ailments, and clears encounter-persistent clean battle state. Insufficient funds and no-restoration-needed states are disabled and non-mutating.

Phase 5-26 adds the first clean active/reserve party proof for original content. `--clean-training-annex-play` hydrates Annex Mentor as a reserve support actor, creates a `RuntimePartyStockSnapshot` with Echo Adept active and Mentor reserve through `PartyStockTransitionService`, exposes `Inspect Party`, and includes that party stock in save/load validation.

Phase 5-27 adds inspectable clean owned stock to the same original-content host. Training Annex hydrates an active-form Annex Mentor, a Persona-stock Bramble Runner, and Demon-stock Ashling/Ward Shell entries as clean runtime actors, stores those references in `RuntimePartyStockSnapshot.ActiveForm`, `PersonaStock`, and `DemonStock`, and exposes `Inspect Stock`. Save/load now accepts valid same-team stock references and rejects corrupted enemy-team party/stock references before mutation.

Phase 5-28 adds clean party/stock operations to that host. `Party / Stock Operations` demonstrates active-form swap, Demon-stock summon, active demon swap, return, replace, dismiss, and consume through framework `PartyStockTransitionService` results. Successful operations update the live clean snapshot, rejected operations preserve the original snapshot, and the host records before/after evidence for active party, reserve, active form, Persona stock, Demon stock, affected IDs, and transition code. This still does not complete recruitment, fusion transactions, battle COMP usage, or Godot-facing presentation.

Track O8 keeps those rules unchanged and adds typed console-host presentation results for shop command selection, buy/sell offers, confirmation, transaction messages, hospital patient selection, and hospital treatment display. Shop and hospital menus still use legacy `Database` records, metadata repair, exact labels, and current HP/SP-based hospital eligibility behavior.

Important rules:

- Items are quantity-based.
- Equipment ownership is stored as unique ID lists by category; Track L intentionally does not introduce per-copy equipment instances.
- Clean equipped weapon profiles own basic-attack metadata for original-content consumers.
- Clean accessory stat modifiers feed stat resolution for actor kinds whose stat policy uses equipment. They are not forced onto demon actors unless a ruleset/stat policy chooses that behavior.
- Buy prices decrease with Luck down to a 50% multiplier: `(int)(basePrice * max(0.5, 1.0 - Luck * 0.01))`.
- Sell prices start at 50% and increase with Luck: `(int)(basePrice * (0.50 + Luck * 0.01))`.
- Missing sell metadata still falls back to base price `100`.
- Framework shop transactions reject duplicate equipment, unavailable stock, insufficient Macca, and selling currently equipped gear before mutation.
- Equipment metadata can be patched from shop entries if JSON-loaded objects are missing names or IDs.
- Equipping recalculates resources because accessories and defensive equipment can affect derived stats and pools.
- Hospital restoration costs `missing HP * 1 + missing SP * 5`, fully restores HP/SP, removes the active ailment, and clears encounter-persistent buffs/breaks only after payment succeeds.

## Fusion And Compendium

`FusionConductor` runs the Cathedral workflow. Track N keeps the same Cathedral menus and live `Combatant`/`Persona` participants, but `FusionCalculator` now adapts those participants into framework fusion services for result resolution, slot calculation, skill mutation, and duplicate-result checks. Track O10 routes Cathedral menus, participant selection, inheritance selection, ritual confirmation, transaction feedback, and Compendium recall/register presentation through typed console-host results. `FusionMutator` and the existing strategies still apply confirmed transactions to the legacy stock models.

Phase 7-30 adds the first original clean fusion result proof. `CatalogFusionContentRepository` adapts `GameDataCatalog` fusion/entity/skill definitions directly into the framework fusion runtime, and the Training Annex host can calculate non-mutating results from clean catalog recipes. Phase 7-31 extends the same proof into inheritance planning: the host records slot counts, selectable skills, already-known/blocked reason codes, passive-fodder eligibility, and a deterministic mutation/accident sample from authored Training Annex skill metadata. Phase 7-32 adds non-mutating preview confirmation: the host presents inherited-skill rows, the framework validates the selected skills, and `FusionPreviewService` creates a Ward Shell preview without changing party/stock, inventory, wallet, Compendium, or parent actors. Phase 7-33 adds committed transaction evidence: duplicate Ward Shell ownership rejects before mutation, and the valid path consumes Ashling/Bramble Runner, hydrates `fusion_ward_shell_1`, and adds the result to Demon stock through framework transitions. Phase 7-34 makes strategy rules modular: hosts explicitly register slot, sacrifice, accident, mutation, catalyst/result, and compatibility policies. Fusion can run without Moon Phase metadata, sacrifice can be disabled or progression-gated, and stat boosts use typed catalyst IDs and policy results rather than names. Phase 7-35 adds clean Compendium registration/recall as an atomic framework transaction and persists the result through the Training Annex save path. CodeReview-7-1 preserves authored entity/race selector kinds, supports mixed selectors, explicitly limits schema v1 recipes to two parents, and keeps legacy result strings out of clean catalog recipes.

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

The Compendium now uses framework state/recall contracts for registration, overwrite, recall pricing, and recall assessment while retaining the existing console registry API. O10 exposes detailed presentation records for registration, recall menu selection, recall assessment, and recall transaction outcomes. Registered entries are deep snapshots, including active Persona state; mutating a recalled clone no longer mutates the stored entry. Recall cost remains base price fallback `2000`, plus `level * 100`, stat sum `* 50`, and skill count `* 200`.

Phase 7-35 adds the clean runtime counterpart. `CompendiumRuntimeService` snapshots a clean actor's qualified entity ID, progression, integral base stats, learned skills, and equipped skills; catalog reconstruction restores those durable values while clearing transient battle/equipment state and refilling recalculated resources. Recall checks duplicate entity ownership, selected Demon/Persona stock capacity, and Macca before returning any changed snapshot. Host code applies the returned party/wallet state only when the result is `Applied`.

CodeReview-7-2 adds a separate runtime-identity guard before recall can create an actor or charge the wallet. The proposed recalled ID must be absent from owner, active party, reserve party, active form, Persona stock, and Demon stock. Stock addition/replacement commands enforce the same rule. Existing duplicates in the destination still report `DuplicateOwned`; cross-role reuse reports the distinct typed identity-collision code. Save validation applies the same ownership model and checks that each reference's entity ID matches its actor snapshot. The intended active-party plus Demon-stock representation remains valid.

## Clean Catalog Surface

Track C adds framework definitions, strict deserialization, validation, catalog qualification, repositories, and one small fixture pack for the content families that were still legacy-only: equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets.

This is a schema and catalog foundation only. It does not migrate `Database`, shop transactions, dungeon traversal, negotiation sessions, equipment ownership, fusion calculation, or economy state. Those systems remain protected by the legacy characterization tests until their dedicated runtime tracks connect real consumers to the clean definitions.

Track Q1 adds the production-content audit ledger before any real content conversion. Track Q2 amends the direction: the legacy `Data/Jsons` records are prototype-only and not approved as commercial/shippable clean content. The ledger still accounts for skills, entities, races, ailments, items, equipment, shops, negotiations, encounters, dungeons, fusion recipes, and rulesets, and it keeps old v2 migration outputs as historical evidence only. The active `convergence.training_annex_slice` sample is the first original clean content seed. It now contains neutral examples for races, entities, skills, ailments, items, equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets. `--clean-training-annex-demo` loads only that pack and proves a complete clean noninteractive loop: ruleset binding, actor hydration, dungeon traversal, item use, automated battle, rewards, progression, and save validation. `--clean-training-annex-play` is the first clean interactive boot/session shell: it loads only that pack, hydrates Echo Adept plus Ashling, Bramble Runner, and Ward Shell as clean runtime actors, initializes and recalculates HP/SP through `standard_growth`, previews `standard_stat` stat composition, applies clean victory EXP/level progression, lets the host inspect session and actor state, validates a startup snapshot, and exits without legacy `Database` startup. Phase 1-07 adds generic `ContentId` outer navigation with explicit transitions and injected access policy. Phase 1-08 adds a separate optional dungeon traversal snapshot over arbitrary node IDs, explicit policy-checked moves, visited nodes, checkpoints, barriers, and boss flags. Phase 1-09 adds explicit host-triggered encounter preparation: the host chooses an encounter/formation, and the framework hydrates ordered actors without traversal starting combat. Phase 1-10 adds clean field item/skill menus over `BattleActionExecutor`, typed field availability, target cancellation, cost handling, and reservation-backed host inventory consumption. Phase 2-11 adds manual clean battle actions after explicit encounter preparation: Practice Blade basic attack, battle skills, Annex Tonic, guard, pass, and analyze are selected by the console host and executed by framework battle/action services. Phase 2-12 adds typed-effect evidence for those commands and tests that renamed display text does not change behavior. Phase 4-21 makes the clean field inventory authoritative over its own quantities: the menu enumerates field-usable items from `RuntimeInventorySnapshot`, selected rows carry catalog content IDs, Focus Tea and Annex Tonic execute through the same item pipeline, and only the selected item is consumed after meaningful execution. Its console rows are presentation only; a Godot doorway, collision trigger, enemy body, patrol, VN hotspot, or script can issue the same requests. Games may omit navigation, dungeon state, random encounters, inventory, or battle entirely. The Training Annex path deliberately omits Moon Phase metadata now: no `standard_moon_phase` ruleset, no `new_moon` registration, and no fake moon phase in clean save/session snapshots unless future content opts into a moon mechanic. Production gameplay still reads the legacy `Data/Jsons` files through `Database` until broader original clean consumers are explicitly switched.

Phase 2-13 makes the Training Annex clean battle a direct catalog-ruleset consumer. Damage, accuracy, criticals, affinities, ailment and instant-death checks, chance rolls, and power amounts share one bound `ProductionCombatRuleset`; victory calculates a reward preview from the same bound combat ruleset.

Phase 2-14 makes the same clean battle a catalog-bound Press Turn consumer. `standard_press_turn` must bind successfully before the Training Annex play session starts, and the bound factory is supplied to `BattleEncounterRunner`. The clean host records/presents icon counts before player commands and after committed actions, including Weak/Critical chaining, Miss/Null penalties, Repel/Absorb phase termination, Pass conversion, and normal consumption. This remains host presentation over framework rules, not a legacy `BattleConductor` dependency.

Phase 2-15 connects that clean Training Annex battle to framework status lifecycle. The clean host now records ailment application and cure evidence from typed actions, lets `BattleStatusLifecycleService` process turn-start restrictions and turn-end ailment triggers/recovery/duration ticks, and presents lifecycle resource/status events. This pass intentionally suppresses passive trigger dispatch so passive lifecycle remains the next separate capability.

Phase 2-16 removes that suppression. The original-content clean battle now dispatches authored `battle_start` and `owner_turn_end` passive events through `PassiveTriggerDispatcher`; `Steady Breath` restores HP through the same typed effect executor used by active actions. Typed rule modifiers are resolved from passive definitions and conditions rather than names or descriptions, and canceled selections do not activate passives because no turn is committed.

Phase 2-17 routes original-content enemy turns through framework `DeterministicBattleActionSelector`. It considers only battle-available typed skills that pass `SkillExecutor.Assess`, selects typed targets, preserves authored loadout order for ties, and returns Pass when no legal command exists. Lifecycle restrictions are resolved first. The clean host records and presents the decision but does not own the selection rule. Battle knowledge is deliberately not persisted or learned until Phase 2-18.

Phase 2-18 gives the Training Annex clean battle scoped knowledge. Player damage effects learn elemental affinity from resolved typed effect results, Analyze learns elemental/ailment/instant-death defenses from the target's typed profile, and the save-facing summary carries the resulting player `RuntimeKnowledgeSnapshot`. Enemy AI receives a separate encounter-local elemental store, can learn and adjust during the current battle, and discards that knowledge afterward unless a future host intentionally supplies special persistent knowledge.

Phase 7-35 implements the optional player-knowledge import boundary. `FamiliarEntityKnowledgeService` accepts explicit familiar entity IDs and imports their typed elemental, ailment, and instant-death defenses from the catalog into a new player knowledge snapshot. Training Annex invokes it after clean recruitment, committed fusion, Compendium registration, and recall. The service is not automatic framework global state, so a developer may omit it or choose a different familiarity policy. Ordinary enemy AI still receives a fresh per-encounter knowledge store.

Phase 2-19 applies the Training Annex victory reward to clean runtime state. Player EXP flows through the bound `standard_growth` policy, Macca flows through framework economy transactions, and session progress records the cleared Ashling drill alongside victory/EXP/Macca counters. The post-battle snapshot sees the live wallet, inventory, field state, and persistent player knowledge. Legacy battle reward payout remains protected until a separate consumer switch proves parity.

## Clean Runtime State Foundation

Track D adds a framework runtime-state surface beside the legacy `Combatant` and `Persona` models. It gives actor identity, controller/team/owner relationships, active/reserve/deployed state, progression, resources, stats, skill loadouts, active form references, persona/demon stock references, equipment slots, battle statuses, analysis, and passive activation counts typed snapshot homes.

This is not full legacy removal. The interactive console still mutates `Combatant`, `Persona`, `InventoryManager`, `PartyManager`, `StatusRegistry`, and the Cathedral services. The runtime snapshots are the portable state boundary, and Tracks E through O progressively moved rule and presentation surfaces onto framework adapters without deleting the live console objects.

Track R adds the first complete framework save/checkpoint contract over those runtime snapshots. CodeReview-1 advances the prerelease `RuntimeSaveGameSnapshot` to version `4`; CodeReview-3 advances it to version `5` by adding exact content-pack provenance. It stores actors, their explicit vital-resource IDs, exact typed status durations, capability IDs, passive enabled/disabled state and activation counts, party/stock, inventory, equipped items, wallet, optional generic navigation/dungeon traversal, Compendium state, battle knowledge, session progress, loaded content-pack IDs/versions, optional host context, and checkpoint breadcrumbs by value while referencing catalog content by qualified IDs. `IRuntimeSaveValidator` checks the save against a `GameDataCatalog` and reports stable diagnostics for duplicate runtime IDs, missing actor links, missing catalog content, malformed knowledge targets, invalid checkpoint ordering, and missing or mismatched content-pack provenance.

The save-file format remains host-owned. `--clean-save-demo` proves a console host can serialize the framework snapshot through `System.Text.Json`, deserialize it, validate it, and exit without input. The Training Annex manual/suspend load path proves complete catalog-backed actor reconstruction.

Phase 3-20 adds the first interactive clean save/load layer to `--clean-training-annex-play`. The framework provides manual and suspend save policy checks, stable diagnostics, save-record metadata, and a flag telling the host whether a suspend save should be consumed after successful restore. The console host owns the menu, JSON, and in-memory slots. Manual load keeps its slot; suspend load consumes its slot only after JSON deserialize, framework validation, and Training Annex session restore succeed. Saves and loads are rejected while an Ashling encounter is prepared but unresolved so the host does not persist a half-handoff battle state.

Restored Training Annex state includes complete catalog-backed canonical actors, inventory, wallet, generic navigation/dungeon state, session counters/flags, and persistent player battle knowledge. Growth, field actions, battle execution, summaries, saves, and restore all use the same `RuntimeActorState`; no HP/SP copy loop remains. CodeReview-3 adds host compatibility checks before restore: the saved actor instance must still be the expected entity/kind/team for its Training Annex role, the saved creation context must have been save-eligible, the saved dungeon nodes/checkpoints must belong to the Training Annex host flow, and content-pack versions must match the loaded catalog. Permanent save files, autosaves, battle saves, Godot save resources, save-version migrations, and legacy prototype save/load remain outside this phase.

CodeReview-4 keeps the same Training Annex play behavior but separates clean host ownership seams. Persistence and restore now live in `TrainingAnnexPersistenceController`; field/dungeon transition presentation lives in `TrainingAnnexFieldPresenter`; and post-battle reward application lives in `TrainingAnnexBattleRewardApplicator`. The main play host remains the command/session coordinator.

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

Ruleset JSON does not own these parameters as production authority yet. Track T2 can bind a catalog `standard_stat` or `standard_growth` ruleset to these existing framework policies, but no production consumer has switched from named defaults to authored ruleset selection.

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

Track O4 gives the field party/stock organization screens explicit console-host result contracts for selected/back/unavailable states, demon stock commands, summon targets, Persona stock actions, and mutation presentation events. The mutation path still applies through the Track F adapter and the live `PartyManager`, preserving active party capacity, active slot behavior, active plus owned demon overlap, return-to-COMP behavior, Persona swap HP/SP capping, and the existing console messages.

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

Legacy skill-name checks for Boost/Amp/Driver, Dodge/Evade, Vidyaraja's Blessing, Apt Pupil, Rebellion, Arms Master, and Spell Master remain adapter or console-effect concerns until Track H/I content migration gives them fully typed definitions. Track T2 can bind a catalog `standard_damage` ruleset to `ProductionCombatRuleset`; only `weakMultiplier` and `resistMultiplier` are currently supported authored parameters, and production consumers have not switched to catalog-selected combat rulesets.

Phase 2-13 switches the original Training Annex clean consumer to this binding. Only `weakMultiplier` and `resistMultiplier` are authored parameters today; the remaining formula defaults remain code-owned policy configuration. Protected legacy battle consumers remain adapter-backed and no removal is authorized.

## Clean Action Execution Foundation

Track H adds a framework-owned action facade for the clean path. `BattleActionExecutor` accepts typed commands for basic attack, skill, item, guard, pass, analyze, escape, Persona swap, demon summon/return/swap, tactics change, negotiation, and host-special actions. Assessment and execution share the same services, so eligibility, target resolution, cost checks, item availability, and party-stock transitions cannot drift apart inside the clean API.

The framework action result reports:

- ordered action events for presentation adapters;
- effect results and host-action request IDs;
- item consumption decisions and whether a host reservation committed;
- party-stock transition results;
- Press Turn consumption, normal consumption, pass consumption, phase termination, or free/no-turn actions.

Field recovery skills and items in the clean field demo now use this action facade with an explicit host-owned inventory reservation adapter. Track O5 connects the interactive player battle menu to the action vocabulary through a console-host command shell. Attack, Skill, Item, Escape, Tactics, and Talk remain host-mediated legacy commands, while Guard, Pass, Analyze, Persona swap, demon summon, demon return, and demon swap use concrete framework command types and assessment before the console applies the existing mutation path.

Track O6 connects framework battle encounter events to a console-host presentation adapter. The adapter consumes every event deterministically, suppresses generic structural narration that would change the visible console output, and routes migrated lifecycle-shell messages such as skip, flee, return-to-COMP, and demon defeat return through typed presentation results. Richer AI/tactics migration, production content reauthoring, and legacy skill/item execution replacement remain later tracks.

CodeReview-2 hardens the original clean battle consumer. Dynamic target rows carry `RuntimeInstanceId`; skill and item rows carry `ContentId`; and the host resolves those typed identities rather than display labels, row position, or content-specific enum cases. Press Turn changes carry `PressTurnStateSnapshot`, leaving message wording free for localization or another presentation host.

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

## Framework Battle Orchestration Foundation

Track J moves the encounter loop itself into `BattleEncounterRunner`. The framework now owns the ordered battle state machine, including initiative policy, battle-start lifecycle, phase setup, turn-start lifecycle, command execution boundary, Press Turn consumption, turn-end lifecycle, phase-end cleanup, participant refresh, completion checks, cancellation, typed faults, and serializer-neutral battle events.

The ordinary console battle now runs through this framework runner. The console adapter still owns the live `Combatant` objects, `InteractionBridge` menus, `ActionProcessor` legacy skill and item execution, `BehaviorEngine` AI heuristics, `NegotiationEngine`, reward payout, data files, message colors, waits, and final cleanup. This is adapter-first migration: the flow is reusable, but the legacy content and effect semantics are still protected until their later tracks.

Track O7 keeps that adapter-first boundary and adds typed presentation records for negotiation prompts, demand prompts, negotiation events, recruitment outcomes, and battle reward display. The framework still owns the negotiation/reward service results, while the console host keeps `questions.json`, legacy mutation, message wording, menu order, waits, and automatic compendium registration hooks.

Track J does not migrate negotiation, recruitment, EXP/Macca reward ownership, inventory/equipment ownership, production skill reauthoring, or complete AI policy authoring. Those remain Track K and later work.

## Archive Candidate Review

Track T5 confirms that the clean Training Annex runtime slice is a forward-production proof, not a legacy retirement proof.

- No protected gameplay capability is `clean_parity`.
- No protected gameplay capability has `removalAuthorized: true`.
- `ArchiveDocs/LegacyFramework` remains policy-only.
- Legacy console compatibility code remains active until a specific consumer migration proves the old path is unreachable.

## Extension Mindset

When adding new gameplay content:

- Add raw data to JSON when the system is already data-driven.
- Add enum values only when new behavior branches are truly needed.
- Add new battle effect strategies when a skill category requires new rule logic.
- Add field/fusion/battle bridge options only after the engine behavior exists.
- Keep state mutation centralized in managers, engines, processors, or strategies rather than UI bridges.
