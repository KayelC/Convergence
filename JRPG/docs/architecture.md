# Architecture

> **Status: Current implementation reference.** Read [Framework State And Roadmap](framework-state-and-roadmap.md) first for the current project map. This document explains implementation shape, not the forward plan. Historical `Track X` labels below describe how existing code arrived here; new work is routed by numbered `futurePhase` entries in the parity ledger and [Full Parity Capability Plan](full-parity-capability-plan.md).

The solution is organized around gameplay subsystems with a physical host boundary. Existing `JRPGPrototype.*` namespaces are retained for source compatibility, but assembly ownership is explicit.

## Project Boundaries

### JRPG.Framework

`JRPG.Framework` is a `net9.0` class library containing the reusable clean path:

- immutable content definitions;
- serializer-neutral deserialization contracts, validation, and catalog construction;
- catalog-surface definitions for equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets;
- runtime identity, actor-state snapshots, versioned save snapshots, and transaction-safe mutation result contracts;
- typed action, skill, item, passive, targeting, and effect execution;
- catalog-backed actor hydration and automated battle orchestration;
- elemental, ailment, instant-death, knowledge, and optional turn-economy contracts, including Press Turn;
- typed fusion inheritance evaluation, result resolution, planning, transaction assessment, and Compendium state.
- serializer-neutral runtime save validation and checkpoint diagnostics.

The framework has no package references and does not access the console, filesystem, Godot, or the legacy static database. JSON implementation details remain internal to the content-loading subsystem.

### JRPG.ConsoleHost

`JRPG.ConsoleHost` remains the executable at the repository root. It owns:

- `Program` and ordinary interactive startup;
- the legacy database, runtime actors, gameplay conductors, and console workflows;
- `IGameIO`, menu rendering, colors, waits, and debug scenarios;
- filesystem-backed content acquisition and copied `Data/Jsons` content;
- the clean battle, field, save, and Training Annex demo policies and presentation.

The console host references the framework. The framework never references the console host.

### Host Contracts

Future clean hosts use cancellation-aware asynchronous contracts for content text, commands, events, and randomness. A host supplies JSON text through `IContentPackTextSource`, consumes or publishes ordered output through `IHostEventSink<TEvent>`, obtains typed choices through `IHostCommandSource<TCommand>`, and owns nondeterminism through `IRandomSource`.

The existing interactive prototype still uses synchronous `IGameIO`, but Track O1 adds console adapters over the clean host contracts. Those adapters are intentionally narrow: already-simple menus can return typed commands and startup can publish sidecar catalog warnings through the event sink without forcing full battle, fusion, shop, or preview-heavy screens through asynchronous host contracts yet.

Track P proves the same contracts are usable from a Godot-style host without adding GodotSharp or engine types to the framework. The test-only Godot contract adapters model `res://` resource loading, signal-style commands, event consumption, scene-instance mapping, deterministic randomness, and host-owned save snapshots while keeping all Godot responsibilities outside `JRPG.Framework`.

Track R turns the earlier host-owned save proof into framework persistence contracts. The framework now defines versioned runtime save snapshots and validation diagnostics; hosts still choose JSON, binary, Godot resources, cloud slots, or any other save-file format.

### Runtime State Boundary

Track D adds `JRPGPrototype.Logic.Runtime` as the framework home for mutable actor state that must eventually survive save, presentation, replay, and host migration boundaries. It defines stable runtime instance IDs distinct from content definition IDs, actor identity/display metadata, controller/team/owner links, deployment state, progression, resources, base/effective stats, learned/equipped skills, active form and stock references, equipment slots, battle statuses, analysis, and passive activation counts.

CodeReview-1 consolidates those fields and the clean battle fields into one canonical `RuntimeActorState`. `CatalogBattleActor` retains immutable catalog definition and loadout metadata but no longer has a second state paired with a save-oriented state set. Growth, resource transactions, field effects, battle effects/lifecycle, snapshots, and restore all operate on the same actor object. Runtime actor and target references use `RuntimeInstanceId`; authored records and registered vocabulary continue using `ContentId`.

The runtime state layer is deliberately composed from focused snapshots rather than one replacement `Combatant` class. `RuntimeActorSnapshot` exists only as the aggregate save/transaction boundary, and content definitions are always referenced by qualified `ContentId` instead of being duplicated into mutable state.

Track E adds framework progression policies for stat composition, HP/SP recalculation, EXP curves, level growth, random Persona stat growth, stat allocation, and rollback. The console `StatProcessor`, `GrowthProcessor`, and `Persona` growth methods now delegate through a console-owned compatibility adapter, preserving the existing live `Combatant` and `Persona` models while moving the rules into reusable framework services.

Track F adds framework party and stock transition services for active/reserve party membership, stock capacity, unified demon stock, active Persona swaps, and fusion inventory consume/replace operations. `PartyManager`, battle Persona swaps, field Persona swaps, and fusion inventory transactions now delegate through console-owned adapters with per-session runtime IDs. The old live lists remain the source of console object ownership until a later persistence/host migration replaces them.

Review-Whole-4 makes stock role membership authoritative at this boundary. Every demon-specific mutation requires the subject to exist in `DemonStock`; swap and return additionally require active deployment. Active demons still remain owned while deployed, while owners and ordinary party members cannot be removed, replaced, or consumed through demon commands. Rejected transitions retain the original immutable snapshot.

Track G adds `ProductionCombatRuleset` as the framework owner for production combat formulas: damage, hit/evasion, critical chance, instant-death success, initiative, rewards, affinity multipliers, guard and rigid-body handling, charge, drain, and reflection math. `CombatMath` and `DamageHandler` remain console-host facades, but their rule work now flows through `LegacyCombatPolicyAdapter` into the framework policy.

Track H adds `BattleActionExecutor` as the framework action facade over clean basic attacks, skills, items, guard, pass, analyze, escape, Persona/demon stock transitions, and host-mediated tactics/negotiation/special actions. It provides one shared assessment/execution path, structured action events, turn-consumption results, cancellation-before-mutation, and host-owned item reservation/commit ports. The console host now routes guard/pass through a compatibility adapter, and the clean field demo uses the facade for field skills and items.

Track I adds `BattleStatusLifecycleService` as the framework owner for clean ailment application, turn-start restrictions, turn-end status effects, natural recovery, duration ticking, cleanup scopes, and battle-start or turn-end passive dispatch. `StatusRegistry` remains the console-facing compatibility facade, but ailment infliction, turn-start, turn-end, and stat-stage mutation now route through `LegacyStatusLifecycleAdapter` where strict legacy parity exists. Cure parsing and redundancy checks stay in the console host until the old skill and item content is reauthored.

Review-Whole-3 closes the internal split that remained inside that boundary. `BattleAilmentApplicationService`, supplied through `BattleExecutionServices`, is the only clean authority for guard, resistance, passive replacement, chance, exclusivity, and duration decisions whether an ailment came from a skill, item, passive, or direct lifecycle call. Turn-start lifecycle returns a typed restriction with allowed actions and source ailments, resolves all active ailments through an injectable policy, and requires explicit handlers for custom behavior. Ailment and passive triggers share the ordered-effect executor. Stat-stage state is bounded at `-4..+4`, and the same range is enforced during save validation and restore.

Track J adds `BattleEncounterRunner` as the framework owner for the encounter state machine: initiative, battle-start lifecycle, team phases, actor turns, explicit turn-economy consumption, command orchestration, turn-end lifecycle, deployment refresh, completion, cancellation, faults, and ordered battle events. `BattleConductor` now acts as a console adapter over that runner, while `InteractionBridge`, `ActionProcessor`, `BehaviorEngine`, legacy content, and console presentation remain host-owned compatibility systems.

Track K adds framework negotiation/reward services for the conversation state machine, typed prompts, demand outcomes, familiar gifts, recruitment validation, and immutable battle reward calculation. The console `NegotiationEngine`, `BattleConductor`, and new legacy adapters translate `IGameIO`, live `Combatant` lists, inventory, economy, and compendium mutation into those framework results. Legacy `questions.json` remains the data source until production content is reauthored.

Track L adds framework resource-management services for inventory quantities, unique equipment-ID ownership, equipment equip/sale invariants, neutral currency transactions, Luck-based shop pricing, shop buy/sell transactions, and hospital restoration. `InventoryManager`, `EconomyManager`, `ShopEngine`, and the field service item/equipment/hospital mutation paths now delegate through `LegacyInventoryResourceAdapter`, which maps the console game's Macca terminology onto neutral framework balances, while `Database`, legacy DTOs, shop inspection text, and console menu presentation remain host-owned.

Phase 4-24 proves the same resource-management contracts on original clean content. `--clean-training-annex-play` reads the authored `training_supply` shop catalog, resolves each offer through `RuntimeShopOfferResolver`, assesses buy/sell rows with the bound `IShopTransactionService`, and applies successful inventory/wallet mutations through the `standard_economy` service bundle. Purchased equipment can immediately flow into `IEquipmentTransitionService` and update the actor's clean equipment profile. The console host still owns labels and command input; the framework owns the transaction rules.

Phase 4-25 adds the matching clean recovery proof. `--clean-training-annex-play` exposes `Recovery Facility`, captures the player as a `RuntimeHospitalPatientSnapshot`, assesses treatment with the bound `IHospitalRestorationService`, and applies only successful framework results to wallet, HP/SP, ailments, and encounter-persistent clean battle state. The legacy city hospital remains protected compatibility code.

Phase 5-26 adds active/reserve party ownership to the same original clean host. Training Annex hydrates Echo Adept as the active actor and Annex Mentor as a reserve support actor, then builds the live `RuntimePartyStockSnapshot` through `PartyStockTransitionService.AddPartyMember`. The host owns `Inspect Party` presentation; the framework owns the party snapshot and transition result. Manual and suspend saves persist and restore that live party stock with the actor roster.

Phase 5-27 extends that clean snapshot with owned active-form, Persona-stock, and Demon-stock references. Training Annex hydrates those stock entries as clean runtime actors, presents them through `Inspect Stock`, and validates saved stock references by actor ownership/team before restoring.

Phase 5-28 adds the first clean party/stock mutation proof on original content. Training Annex now presents `Party / Stock Operations` as a host-owned menu, then routes active-form swap, Demon-stock summon, active demon swap, return, replace, dismiss, and consume through `PartyStockTransitionService` via `TrainingAnnexPartyController`. The framework owns the transition rules and non-mutating diagnostics; the console host owns labels and sequencing. Recruitment, fusion-driven stock changes, battle COMP-style usage, and Godot presentation remain later work.

Phase 6-29 adds a clean negotiation/recruitment proof on original content. Training Annex now presents `Negotiate / Recruit`, maps answers and authored demand choices through host command contracts, runs `NegotiationSessionService`, validates recruitment through `RecruitmentTransactionService`, adds the recruited Bramble Runner through `PartyStockTransitionService.AddDemonToStock`, and debits neutral framework currency while presenting it as Macca. Review-Whole-8 makes all session thresholds, gates, familiar gifts, fallback demands, and demandless outcomes explicit `INegotiationSessionPolicy` decisions; the framework no longer supplies prototype defaults.

Track M originally added a floor-oriented compatibility state machine for the legacy console dungeon. Review-Whole-8 moves that implementation, `LegacyDungeonContentAdapter`, floor evaluation, terminal behavior, fallback encounters, and presentation-oriented events into `JRPG.ConsoleHost`. `JRPG.Framework` retains only generic, policy-injected navigation and dungeon-node traversal snapshots/services; it does not prescribe floors, lobbies, terminals, cities, menus, or encounter fallback content.

Track N adds framework fusion runtime services for recipe lookup, result operation selection, inheritance slot calculation, skill mutation, accident inheritance replacement, preview snapshots, transaction assessment, and Compendium registration/recall assessment. `FusionCalculator`, duplicate-result guards, and `CompendiumRegistry` now adapt legacy database/live object state into those services while keeping Cathedral menus and legacy transaction strategies intact. Compendium snapshots now deep-clone active Persona data instead of sharing live references.

Phase 7-30 adds a clean catalog fusion repository for original content. `CatalogFusionContentRepository` reads qualified `GameDataCatalog` fusion recipes, entities, and skills into the same framework fusion runtime contracts used by the legacy adapter. Phase 7-31 routes that catalog path through `FusionPlanningService` as well, proving inheritance slot calculation, passive/explicit-allowance filtering, display reason codes, and deterministic accident mutation evidence. Phase 7-32 adds a small Training Annex preview command. Phase 7-33 adds a Training Annex transaction proof, and CodeReview-7-3 makes that transaction framework-owned: `FusionTransactionService` prepares an immutable stock/actor decision from a validated inheritance token, injected transition service, typed owner kind, proposed identity, fixed result ownership, and optional retained stat-boost snapshot; after host confirmation it rejects stale state or returns one actor-backed atomic commit result. Duplicate participant identities, conflicting owned references, and mismatched preview/factory output reject before applied state is published. Phase 7-34 makes the strategy layer explicit: `FusionPolicyRegistry` is required by resolution/planning, policy context is optional host data, authored policy IDs are preserved from catalog recipes, and no default policy set is hidden inside the framework. CodeReview-7-4 retains that immutable context on each planning result and reuses it for accident mutation; standalone slot calculations distinguish explicit contextual evaluation from an intentional context-free overload. Neutral create/rank operations remain built in; stat boost, special results, accidents, mutation, sacrifice, and slot behavior are selected by registered policies. Legacy Moon Phase and catalyst assumptions are implemented only by `LegacyFusionStrategyPolicies` in the console host. CodeReview-7-1 preserves typed binary recipes and CodeReview-7-2 preserves global runtime identity. The Training Annex clean host still does not replace Cathedral rituals or Compendium presentation.

CodeReview-7-5 adds one internal Compendium entry-integrity authority shared by runtime registration, pre-transaction recall, and complete-save validation. It treats an empty stat block as catalog defaults, requires nonempty overrides to match authored entity stats, rejects duplicate/missing skills and negative values, and maps failures to serializer-neutral diagnostics before stock, actor, or wallet work. Host JSON remains outside the framework.

Review-Whole-5 closes the remaining fusion authority gaps. Schema validation rejects equal-specificity recipe overlaps instead of assigning meaning to document order, and the runtime resolver repeats the check for arbitrary repositories or unresolved cross-pack combinations. Accident inheritance accepts only the exact `FusionPlanningResult`: candidates, limits, mutation policy, and context are framework-derived, mutation outputs are revalidated, and the returned inheritance token is bound to that plan. Hosts still own presentation and confirmation, but cannot manufacture a legal pool or slot count.

Review-Whole-6 makes clean action execution transactional at the framework-state boundary. Skills, items, basic attacks, analysis, and escape effects execute against complete staged `RuntimeActorState` clones; costs, resources, ailments, stages, charges, shields, overrides, analysis, and passive activation state are published only after the effect sequence succeeds. Item actions additionally require typed atomic host reservation transitions and publish actors only after inventory commit. Combat-rule construction and growth/resource arithmetic now reject invalid configuration or return stable overflow diagnostics rather than leaking partial state or accidental numeric exceptions.

Track O1 starts the interactive console-host migration without changing gameplay rules. `ConsoleGameHost` still loads the legacy `Database` first, then creates an `InteractiveConsoleHostContext` that attempts to load the retained clean content packs as a nonfatal sidecar catalog. Plain field, city, inventory, status, dungeon, terminal, hospital-patient, and field-target menus now pass through the framework host-command contracts via console adapters while preserving legacy return strings for existing conductors.

Track O2 moves read-only status presentation behind console-owned projection adapters. Human summaries, Persona details, demon details, stock rows, organization rows, summon rows, and equipment slot labels are now rendered from framework runtime snapshots plus copied legacy display data. Rich hover-preview menus outside status, battle commands, Cathedral prompts, and gameplay mutations remain legacy bridge surfaces.

Track O3 routes field item and field skill presentation through typed console-host selection and execution results. Item/skill menus, target cancellation, field-use assessment, consumption decisions, and ordered field presentation events now have explicit result contracts, while legacy `ItemData`, `SkillData`, production JSON, effect-string parsing, and visible field behavior remain console-host compatibility concerns.

Track O4 routes party organization, demon stock, Persona stock, and field-side summon/return/swap presentation through typed console-host results backed by the Track F party/stock adapter. The old `StatusUIBridge` wrappers, `PartyManager`, live `Combatant`/`Persona` lists, active plus owned demon stock invariant, messages, cancellation, and status-peek behavior remain intact.

Track O5 routes player battle command selection through a console-host command shell. The shell produces framework `BattleActionCommand` objects and assessments before handing back the legacy payloads needed by `ActionProcessor`, `PartyManager`, `NegotiationEngine`, and current battle helpers. Legacy attack, skill, item, and escape execution remain host-mediated; concrete framework commands are used for guard, pass, analyze, Persona swap, and COMP stock commands.

Track O6 routes framework battle encounter events through a console-host event presentation adapter. `BattleConductor` now supplies the runner with an event sink that records `Shown`, `Suppressed`, and `HostOwned` presentation results. Generic framework structural events stay quiet to preserve visible console narration, while migrated lifecycle-shell messages for skip, fear flee, return-to-COMP, enemy flee, and demon defeat return use typed presentation results.

Track O7 routes negotiation, recruitment, and victory reward presentation through typed console-host results. `NegotiationEngine` now exposes detailed prompt/event/outcome records around the framework session service, `BattleConductor` shares one negotiation outcome presenter across its battle paths, and reward totals are presented from immutable framework reward results before legacy mutation is applied.

Track O8 routes shop and hospital presentation through typed console-host results. `ShopUIBridge` now exposes explicit command, offer, confirmation, inspection, and transaction result shapes over the framework-backed shop transactions, while `ServiceUIBridge`, `FieldServiceEngine`, and `FieldConductor` present hospital selection and treatment from typed results over framework restoration transactions. Legacy shop data, pricing formulas, metadata repair, and hospital UI quirks remain host-owned.

Track O9 routes legacy dungeon traversal presentation through typed console-host results over the host-owned compatibility `RuntimeFieldDungeonService`. `DungeonManager` exposes detailed transition presentation results, `DungeonUIBridge` maps selected/back/unavailable choices and shown/suppressed runtime events, and `FieldConductor` consumes those results for movement, terminal warp, dungeon exits, barriers, floor entry, and boss defeat. Legacy `tartarus.json`, enemy hydration, battle handoff, visible text, menu order, and the compatibility service itself remain host-owned.

Track O10 routes Cathedral fusion and Compendium presentation through typed console-host results while preserving the visible ritual flow, legacy data, mutation ownership, accident/mutation odds, recall pricing, and deep Compendium snapshots.

Track P adds the Godot integration contract proof. It does not introduce a Godot project; it verifies that content loading, command input, event output, clean actor creation, deterministic battle execution, scene mapping, and snapshot save/restore are all possible through engine-neutral framework APIs.

Track R introduced `RuntimeSaveGameSnapshot` version `1`, typed battle knowledge snapshots, session progress, checkpoint breadcrumbs, save validation diagnostics, and `--clean-save-demo`. Phase 1-07 advanced that prerelease contract to version `2` for optional generic navigation, and Phase 1-08 now stores optional generic dungeon traversal state. The demo serializes through console-host-owned DTOs, proving the contract is portable without exposing serializer APIs from `JRPG.Framework`.

Phase 3-20 adds the first interactive clean save/load product flow over those contracts. `JRPG.Framework` owns manual/suspend save policy concepts, context assessment, pending-host-action rejection, immutable `RuntimeSaveRecord` metadata, and suspend consumption instructions. `--clean-training-annex-play` owns the `Save / Load` menu, host JSON, in-memory manual/suspend slots, and Training Annex-specific restore of actor snapshots, inventory, wallet, field/dungeon state, session progress, and persistent player battle knowledge. Suspend slots are consumed only after successful restore; malformed or invalid records leave the running session untouched.

CodeReview-1 advances the pre-release save contract to version `4`. Actor snapshots include the vital-resource ID, exact typed durations, capability IDs, passive enabled/disabled state, and passive activation counts; catalog restore reconstructs complete canonical actors without invoking creation defaults. Resource recalculation also commits to that actor before success is reported. CodeReview-3 advances the pre-release save contract to version `5`: `RuntimeSaveGameSnapshot` records loaded content-pack IDs and exact versions, `RuntimeSaveValidator` compares that provenance against the current `GameDataCatalog`, and the Training Annex host rejects saved actors or dungeon state that do not match the expected host session before replacing live state. This remains a pre-release contract; no production save migration is required yet.

CodeReview-4 splits the Training Annex clean host by responsibility without changing behavior. `CleanTrainingAnnexPlayHost` remains the session coordinator, while `TrainingAnnexPersistenceController` owns manual/suspend save and restore planning, `TrainingAnnexFieldPresenter` owns Training Annex field/dungeon transition messages, and `TrainingAnnexBattleRewardApplicator` owns the host-side transaction boundary for applying clean reward previews to progression and wallet state.

CodeReview-2 adds typed dynamic selection identity to the engine-neutral host command boundary. A menu option may identify authored content with `ContentId` or a runtime actor with `RuntimeInstanceId`, while existing command enums continue describing the broad action. The Training Annex battle shell therefore discovers executable skills and owned battle items instead of mapping known IDs to enum members, and multi-enemy target rows preserve the selected actor. `BattleEncounterEvent` now carries a typed `BattleTurnEconomySnapshot`; a Press Turn host receives `PressTurnEconomySnapshot`, while event messages remain presentation and are not parsed for state.

Complete AI/tactics policy, full fusion strategy replacement, permanent save slots, autosaves, save-version migration tooling, authored negotiation content, legacy item/equipment/dungeon content reauthoring, and production ruleset authority remain later migration tracks. Track T2 adds conservative catalog ruleset binding for existing standard policies, Track T3 adds the self-contained `convergence.training_annex_slice` original clean content seed, and Track T4 adds a noninteractive Training Annex runtime consumer. Phase 1-01 adds `--clean-training-annex-play`, the first separated clean-console interactive boot/session shell over that pack. Phase 1-02 extends that shell with a clean player/enemy actor roster hydrated from catalog and encounter definitions. Phase 1-03 binds the shell to `standard_growth` for clean HP/SP initialization and resource recalculation. Phase 1-04 binds the shell to `standard_stat` for a clean stat-composition preview. Phase 1-05 applies clean victory EXP and level progression through the same catalog-bound growth services. Phase 1-06 removes fake Moon Phase requirements from Training Annex content, registrations, runtime battle metadata, and save/session snapshots when no moon mechanic is used. Phase 1-07 adds generic, policy-driven outer-location transitions without assuming a menu, spatial model, city, or dungeon. Phase 1-08 adds a separate optional dungeon-node service with injected traversal policy, visited nodes, checkpoints, barriers, and boss flags; it does not start encounters. Phase 1-09 adds explicit host-triggered catalog encounter preparation and ordered actor hydration, still without coupling encounters to traversal. Phase 1-10 adds typed field item/skill execution and transaction-safe host inventory consumption through the shared action facade. Phase 2-11 consumes a prepared encounter through manual clean battle actions: Practice Blade attack, battle skills, Annex Tonic, guard, pass, and analyze all execute through framework action commands. Phase 2-12 adds typed-effect evidence to that clean battle summary, proving the shell uses typed effect definitions and not display text or legacy effect strings. Phase 4-21 removes the remaining hardcoded field-item assumption from the clean host: inventory menus now enumerate field-usable catalog items from `RuntimeInventorySnapshot`, selected rows carry content IDs, and the selected item is the only quantity committed after meaningful execution. Phase 4-22 adds `RuntimeEquipmentProfileResolver` and routes Training Annex basic attacks through the actor's equipped weapon profile; ownership/equip mutation uses framework transactions, while host presentation remains replaceable. Phase 4-23 binds the clean session's inventory, equipment, and wallet services through `standard_economy`; reward Macca produces an immutable transaction result, and invalid mutations preserve the original wallet. Field and dungeon persistence are independently optional in save contract v2. The Track E/F/G/H/I/J/K/L/M/N/O interactive consumers still run through named defaults and compatibility adapters until original clean content is wired deliberately into broader play.

Track S is an archive gate, not a proof that the framework is finished. Legacy files may move to `ArchiveDocs/LegacyFramework` only after the parity ledger marks the corresponding capability `clean_parity`, `consumerMigrated: true`, and `removalAuthorized: true`. Until then, the console adapters and legacy datasets remain active compatibility code while production continues on the new architecture.

Track T is the build-forward lane after that archive gate. It keeps active legacy code in place while the framework gains missing production authority through authored ruleset binding, original clean content, and end-to-end clean runtime slices. The first original content seed is the Training Annex slice; Track T4 wires it into `--clean-training-annex-demo`, proving catalog load, ruleset binding, dungeon traversal, item execution, automated battle, rewards/progression, and save validation without replacing ordinary console startup. Phase 1-01 adds `--clean-training-annex-play` under `Host/CleanConsole/TrainingAnnex/`, proving clean interactive boot, actor inspection, startup snapshot validation, and exit without legacy `Database` startup. Phase 1-02 proves Echo Adept and the Training Annex enemy roster can be represented as clean runtime actors without legacy live objects. Phase 1-03 proves the play shell can initialize and update HP/SP through framework resource policies and runtime snapshots. Phase 1-04 proves the play shell can resolve and present clean stat composition through the catalog-bound `standard_stat` policy. Phase 1-05 proves the play shell can apply clean EXP/level progression and persist it into the runtime snapshot. Phase 1-06 proves clean Training Annex paths can omit moon metadata entirely unless content explicitly opts into moon-phase conditions. Phase 1-07 makes generic outer navigation opt-in. Phase 1-08 adds equally generic, optional dungeon-node traversal while leaving scene movement, presentation, and encounter triggers to the host. Phase 2-11 adds the first player-driven clean battle action shell over a prepared Ashling encounter, and Phase 2-12 records typed-effect evidence for that shell without adding new content.

Phase 2-13 removes the temporary combat-policy boundary from the original Training Annex consumer. The host binds `standard_damage` once, shares that `ProductionCombatRuleset` across typed effect execution, and binds `standard_reward` to the same policy for reward calculation. Ruleset failures stop startup instead of selecting demo or legacy fallbacks.

Phase 2-14 removes the last implicit Press Turn assumption from the original Training Annex battle consumer. The play host binds `standard_press_turn` from catalog ruleset content and records/presents before-and-after icon state for committed clean actions. Review-Whole-2 later generalizes the runner itself: `BattleEncounterServices` now requires an explicit `IBattleTurnEconomy` factory and finite `BattlePhaseProgressPolicy`; Press Turn is one implementation, and `StandardActionTurnEconomy` supports encounters that do not opt into it. The framework owns economy state and liveness; hosts own formatting.

Phase 2-15 starts consuming framework status lifecycle from the original Training Annex battle path. The play host now uses a `BattleStatusLifecycleService`-backed lifecycle port for ailment application evidence, turn-start restrictions, turn-end poison ticks, cure/removal, recovery, and expiry. Passive dispatch remains intentionally disabled in that port until the separate passive lifecycle phase.

Phase 2-16 completes that lifecycle port's passive boundary. The host no longer substitutes a no-op dispatcher: battle-start and owner-turn-end events use the loaded actor's `BattlePassiveCollection`, framework `PassiveTriggerDispatcher`, typed conditions/modifiers, and shared effect executors. The host only maps resulting lifecycle and encounter events for presentation; activation ordering, recursion protection, per-battle limits, and modifier resolution remain framework-owned.

Phase 2-17 removes enemy skill-choice rules from the Training Annex host. The clean consumer injects framework `IBattleActionSelector`/`DeterministicBattleActionSelector`, which filters typed catalog skills through shared execution assessment and preserves authored order for equal scores. The host still decides that the player is directly controlled and enemies use the strategy, and it only presents/records the typed decision. Persistent knowledge and configurable tactics remain separate later capabilities.

Phase 2-18 adds scoped clean battle knowledge to that path. The Training Annex host keeps persistent player elemental, ailment, and instant-death knowledge for save-facing discovery state, while each manual battle receives fresh encounter AI knowledge for tactical decisions inside that battle only. Player actions and Analyze update the player snapshot used by future UI hints; enemy observations update only the encounter-local AI store. The host still owns presentation; knowledge storage and validation use framework snapshot contracts.

Phase 2-19 completes the first clean reward application path for original content. After a player victory, the Training Annex host applies the bound `standard_reward` totals through framework growth and economy services, updates session-progress counters/flags, and validates saves against the live post-battle wallet, inventory, field state, and player knowledge. Cancellation and non-victory outcomes do not mutate rewards.

## Layers And Patterns

### Conductors

Conductors own high-level workflows and decide which subsystem should act next.

- `FieldConductor` runs the city, dungeon, inventory, status, party organization, and fusion entry loops. Dungeon entry, terminal return, explicit dungeon exit, barrier feedback, floor entry, and boss-defeat registration now pass through framework-backed dungeon transition presentation results.
- `BattleConductor` adapts the console battle into the framework encounter runner, applies framework reward results through a console adapter, and keeps cleanup flow host-owned.
- `FusionConductor` runs Cathedral menus, participant selection, result staging, inheritance choice, confirmation, accidents, and compendium actions through typed console-host presentation results where Track O10 migrated the surface.

Conductors should remain workflow coordinators. When adding new rules, prefer placing rule logic in an engine, processor, strategy, or registry.

### Engines And Processors

Engines and processors own deterministic rules or bounded state mutations.

- `CombatMath` is the console compatibility facade for framework production combat policies.
- `PressTurnEngine` owns the full/blinking turn icon state machine.
- `StatusRegistry` is the console compatibility facade for ailment application, turn-start restrictions, passive startup effects, buff/debuff handling, cures, and redundancy checks. Migrated lifecycle decisions delegate into the framework through `LegacyStatusLifecycleAdapter`.
- `ActionProcessor` executes attacks, skills, items, persona swaps, and analysis by delegating to effect strategies; migrated guard/pass coordination now passes through the framework action facade.
- `FieldServiceEngine` and `ShopEngine` are console compatibility facades over framework-backed resource-management transactions for inventory, equipment, shops, and hospital restoration. They still own legacy item/skill effects, metadata repair, messages, and dungeon traversal coordination.
- `ExplorationProcessor` remains the console host for field-side messages, battle handoff, encounter hydration, and duplicate enemy display suffixes; movement and floor evaluation are delegated through the framework-backed dungeon manager and mapped into typed presentation events before visible console output.
- `FusionCalculator`, `FusionMutator`, and fusion strategies remain console compatibility facades; fusion prediction and Compendium rule checks now route through framework services where Track N migrated them, and O10 exposes detailed presentation/transaction results without changing legacy mutation ownership.
- `StatProcessor`, `GrowthProcessor`, `DamageHandler`, and `CombatantFactory` keep entity logic outside the `Combatant` data shell.

### Bridges

Bridges are interactive console UI adapters. They turn game state into menus and return user choices to conductors.

- Battle: `InteractionBridge`.
- Field: `ServiceUIBridge`, `DungeonUIBridge`, `InventoryUIBridge`, `StatusUIBridge`, `ShopUIBridge`.
- Fusion: `CathedralUIBridge`, backed by `FusionCompendiumPresentationResults` for detailed Cathedral and Compendium presentation records.

Bridge code is allowed to know about menu layout and display strings. Rule code should not depend on bridge behavior except through returned choices.

### Messengers And Loggers

Battle, field, and fusion each use a small event-style messaging layer.

- Messengers publish structured message args.
- Loggers subscribe and render to `IGameIO`.
- Conductors unsubscribe loggers when leaving long-lived loops to avoid duplicate messages and leaks.

This keeps engines mostly independent from console rendering while still allowing narration, pauses, colors, and analysis displays.

### Static Database

`Database` is the console host's legacy content registry. It loads JSON files from `Data/Jsons` into static dictionaries and lists:

- skills, entities/personas/demons, ailments, items, dungeons, equipment, fusion recipes, negotiation questions, and shop inventory.

Legacy runtime systems assume `Database.LoadData(io)` has completed before factories, shops, battle effects, dungeon traversal, or fusion logic are used. Framework services instead receive immutable definitions or a validated `GameDataCatalog`.

The clean catalog now has a definition surface for every retained legacy content family. That surface is not a data migration: the legacy datasets still load through `Database`, while clean fixtures prove target authoring contracts for later consumer migration tracks.

During ordinary console startup, the clean catalog is loaded as a sidecar after the legacy database. Sidecar failures are reported as clean-catalog warnings and do not stop the prototype because the retained legacy datasets remain the gameplay authority until production content is reauthored.

Track Q1 started the production-content audit pass. Track Q2 amends the boundary: legacy `Data/Jsons` records are prototype-only evidence, not approved commercial/shippable framework content and not a direct conversion queue. `docs/q-track-plan.md` and `Convergence.Tests/Fixtures/ProductionContent/production-content-ledger.json` now define the original-content policy, manual-decision buckets, legacy file coverage, clean schema targets, and removal gates. Track T3 adds `convergence.training_annex_slice` as original clean catalog content, and Track T4 proves it can run through a clean noninteractive host. `Database` remains the ordinary interactive gameplay authority until a later consumer switch is explicitly verified.

Track T5 reviews archive eligibility after that clean runtime slice. The review records 0 archive candidates and 0 removal authorizations, so `ArchiveDocs/LegacyFramework` remains policy-only and active compatibility code stays in the console host.

## Runtime Dependency Shape

`Program.cs` belongs to `JRPG.ConsoleHost` and creates the initial legacy services:

- `IGameIO` for console access.
- `InventoryManager` and `EconomyManager` for persistent player resources.
- `DungeonState` for Tartarus progress.
- `CompendiumRegistry` for demon snapshots.
- `BattleKnowledge` for affinity discovery memory.
- `Combatant` for the player and test scenario state.

The field subsystem then creates the party manager, dungeon manager, bridges, service engines, exploration processor, and fusion conductor. Battle conductors are created as encounters occur, using the current party, enemies, inventory, economy, knowledge, and compendium.

## Data Flow

1. JSON files define content templates.
2. `Database` hydrates templates into static registries.
3. Factories convert templates into live `Combatant` or `Persona` instances.
4. Managers hold persistent state such as stock, inventory, economy, dungeon progress, and compendium entries.
5. Conductors move state through gameplay loops.
6. Engines and processors calculate outcomes and mutate state.
7. Bridges and loggers present results through `IGameIO`.

## Design Constraints

- `Combatant` is intentionally broad because it represents humans, operators, demons, enemies, and party members.
- Demons use `ActivePersona` as their stat and affinity source; their own character stats are reset to zero by the factory.
- Operators use demon stock and active party references; Wild Cards use active persona plus persona stock.
- The protected legacy console path has no production save-file system. The framework has versioned serializer-neutral save snapshots and validation, while each host owns its save-file format and storage.
- The console UI is abstracted, but the current interactive workflow remains menu-driven and synchronous.
- Filesystem, console, delays, and legacy Newtonsoft loading are host-only concerns.
- Framework public APIs expose no console, filesystem, serializer, Godot, or legacy runtime types.
- Host cancellation is distinct from an ordinary menu cancellation in the async command contract.
- Runtime snapshots are serializer-neutral contracts. A host may persist them, but the framework does not prescribe a save file format in Track D.

Phase 7-35 completes the original-content Compendium runtime proof without coupling it to the Cathedral. `CompendiumRuntimeService` registers durable clean actor data by qualified catalog entity ID, reconstructs recalled actors through `CatalogBattleActorFactory`, and returns atomic stock/wallet snapshots. Recall pricing is a separate optional `ICompendiumRecallPricingPolicy`: no policy leaves registration available but disables recall, a fixed zero-cost policy provides free recall without invoking payment, and the configurable linear helper or a custom policy supports game-owned pricing. Currency names remain host presentation. `FamiliarEntityKnowledgeService` is a separate opt-in boundary: a host chooses which registered or owned entity IDs seed persistent player knowledge. It never writes to encounter-local enemy AI knowledge. `RuntimeKnowledgeIntegrity` rejects duplicate elemental, ailment, or instant-death keys before save restore or familiar import can consume them. Training Annex presentation and host-generated recall instance IDs remain outside the framework.

CodeReview-7-2 makes runtime identity a framework invariant rather than a host naming convention. `RuntimePartyStockIdentityRules` is the shared internal ownership graph used by party/stock transitions, Compendium recall, and save validation. A runtime ID may identify only one actor. Direct party-member, Demon-stock, and Persona-stock additions reject an ID already used by an incompatible ownership role. The graph explicitly permits the exact owner reference to occupy an active-party slot and the same owned demon to appear in active party and Demon stock; `SummonDemon` is the explicit transition that creates the latter overlap. Every persisted party/stock reference must also identify the same catalog entity as its actor snapshot. Save validation applies the injected `IStockCapacityPolicy` independently to Demon and Persona stock, matching the live transition services.

Review-Whole-7 makes direct C# definitions obey the same immutability boundary as JSON-authored definitions. Custom parameter dictionaries recursively normalize and freeze only null, Boolean, string, `Int64`-representable integer, decimal, ordered-list, and string-keyed-object values. Serializer nodes and host objects cannot enter catalog definitions through this extension surface. Runtime affinity queries that affect conditions or damage share `RuleModifierResolver`; cycle tracking remains private to the resolver rather than leaking mutable evaluation state into public condition records.

Review-Whole-8 removes the remaining prototype assumptions from clean framework authority. Wallet and reward APIs use neutral currency terminology; negotiation requires an injected session policy; stock capacity is either explicitly tiered or deliberately unlimited; and the floor/menu-oriented dungeon module lives only in the console host. The legacy host still presents Macca, Full Moon, Medicine, Demon Stock, and its historical stock curve through adapters, while another host can supply entirely different policies and vocabulary.

The L3 correction makes catalog actor creation a closed diagnostic boundary. `CatalogBattleActorCreationRequest.Level` and an optional progression snapshot must agree; that single level drives unlocks, initialization, and the resulting actor state. Invalid policy output, including null initialization and duplicate resource IDs, produces typed `CatalogBattleActorDiagnostic` values rather than leaking collection-construction exceptions. Existing encounter, fusion, Compendium, persistence, Godot-contract, and clean-host consumers all use the same corrected factory contract.

## Caveats

- Nullable warnings are present across DTOs, events, and some return paths. Many come from JSON-populated classes without required constructors.
- `Database` is global mutable state. This is simple for a prototype but makes test isolation harder.
- Some systems compare names or string IDs directly. Normalize IDs to lowercase where possible and be careful when adding new content.
- The automated suite covers framework contracts, legacy characterization, datasets, host adapters, and deterministic demos. Full live battles and exhaustive long-form console traversal remain manual checks.
