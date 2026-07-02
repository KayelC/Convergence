# Full Parity Capability Plan

> **Status: Active capability plan.** This document is the working plan for taking each protected capability from its current state to true framework parity. It supersedes the need to read archived track plans when deciding what to build next. It does not approve legacy removal, source archiving, namespace migration, or broad repository reshuffling.

## Why This Exists

The project now has many planning documents, but the actual protected work is tracked by one executable ledger:

```text
Convergence.Tests/Fixtures/Parity/recovery-baseline.json
```

That ledger contains **36 protected capabilities**. As of the current review:

- `3` are `clean_foundation`;
- `32` are `parallel_partial`;
- `1` is `legacy_only`;
- `0` are `clean_parity`;
- `0` have `removalAuthorized: true`.

The confusion comes from treating several plans as if they were separate workstreams:

- framework capability migration;
- clean console demo development;
- Godot host readiness;
- legacy archive review;
- original content authoring.

This document merges that thinking into one rule:

```text
One capability at a time.
Framework ownership first.
Clean console host proves it.
Godot remains a later interchangeable presentation over the same framework.
Legacy removal only after parity is proven.
```

## Relationship To The Clean Console Host Demo Plan

[Clean Console Host Demo Plan](clean-console-host-demo-plan.md) is not a competing plan.

It is the current proof harness for this plan.

For each capability, the desired pattern is:

```text
framework rule/state/result ownership
  -> original clean content when needed
  -> clean console presentation proof
  -> tests and docs
  -> parity ledger update
```

Later, Godot should be able to present the same framework capability because the rule logic lives below the presentation layer.

## What Counts As Full Parity

A capability reaches `clean_parity` only when all are true:

1. **Framework ownership** - the framework owns the rules, state, diagnostics, and result contracts.
2. **Clean content authority** - if the capability needs data, it uses original clean catalog content, not prototype `Data/Jsons`.
3. **Clean consumer** - at least one real clean consumer uses the framework directly, without legacy `Database`, `Combatant`, `Persona`, `SkillData`, `ItemData`, or string-effect authority.
4. **Presentation separation** - console presentation is host-owned and could theoretically be replaced by Godot presentation.
5. **Behavior disposition** - old behavior is either preserved intentionally or deliberately changed with documentation.
6. **Tests** - framework tests, clean host tests, and relevant legacy characterization tests pass.
7. **Docs** - active docs describe the new ownership and any approved behavior changes.
8. **Ledger update** - `recovery-baseline.json` records the new status, evidence, consumer migration, and removal decision.

`clean_parity` does not automatically mean deletion. Deletion or archive movement still requires:

- `consumerMigrated: true`;
- `removalAuthorized: true`;
- a dedicated archive/removal review.

## Status Meanings

| Status | Meaning |
| --- | --- |
| `legacy_only` | The protected behavior still belongs to the old console/prototype path. |
| `clean_foundation` | Framework definitions or services exist, but no real clean consumer proves full use yet. |
| `parallel_partial` | Framework services and/or adapters exist, but legacy authority or compatibility code is still part of the path. |
| `clean_parity` | Framework owns the capability and a migrated clean consumer proves it without hidden legacy dependency. |

## Standard Capability Work Loop

Each capability pass should follow this checklist:

1. Read the capability entry in `recovery-baseline.json`.
2. Identify the legacy authority being replaced or bypassed.
3. Decide whether the behavior should be preserved, changed, or dropped.
4. Add or adjust original clean content only if needed.
5. Implement the smallest framework change needed.
6. Add the smallest clean console proof needed.
7. Keep Godot compatibility by avoiding console/filesystem/serializer types in framework APIs.
8. Add focused tests.
9. Run the quality gate.
10. Update this plan and the parity ledger only if the evidence justifies it.

## Quality Gate For Every Capability Pass

Minimum gate:

```powershell
dotnet test JRPG.sln --no-restore
dotnet build JRPG.Framework/JRPG.Framework.csproj --no-restore --no-incremental /clp:Summary
dotnet build JRPG.sln --no-restore --no-incremental /clp:Summary
dotnet run --no-build -- --clean-battle-demo
dotnet run --no-build -- --clean-field-demo
dotnet run --no-build -- --clean-save-demo
dotnet run --no-build -- --clean-training-annex-demo
git diff --check
git status --short -- Data\Jsons
```

Add focused tests and additional demo commands for the specific capability.

## Capability Sequence

The sequence below is the recommended order. It is not a command to implement everything at once.

### Phase 0: Guardrails

Goal: keep the owner in control before implementation starts.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 00 | planning guardrails | active docs only | Use this document as the single capability spine. |

### Phase 1: Clean Playable Spine

Goal: build the smallest independent clean loop first.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 01 | `interactive_boot` | `parallel_partial` | Add a clean interactive entry that does not load legacy `Database`. |
| 02 | `actor_models` | `parallel_partial` | Prove clean runtime actors can represent the playable demo actor and enemies. |
| 03 | `resource_recalculation` | `parallel_partial` | Make clean HP/SP initialization and updates authoritative in the clean demo. |
| 04 | `stat_composition` | `parallel_partial` | Make clean stats and equipment/stat modifiers visible in the clean demo. |
| 05 | `growth_and_levels` | `parallel_partial` | Apply EXP and level/progression through framework services in the clean demo. |
| 06 | `moon_phase` | `legacy_only` | Decouple optional moon/session mechanics from clean runtime paths that do not use them. |
| 07 | `field_navigation` | `parallel_partial` | Provide optional generic location transitions while hosts own how travel is requested and presented. |
| 08 | `dungeon_traversal` | `parallel_partial` | Use clean dungeon state for floor/room/terminal/exit decisions. |
| 09 | `encounters` | `parallel_partial` | Start encounters from host-owned triggers over clean encounter definitions. |
| 10 | `field_items_and_skills` | `parallel_partial` | Use clean items/field skills through framework execution and host-owned inventory. |

### Phase 2: Clean Battle Spine

Goal: replace automated demo combat with a real clean battle loop.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 11 | `battle_actions` | `parallel_partial` | Let the clean demo choose clean attack/skill/item/guard/pass/analyze commands. |
| 12 | `typed_effects` | `parallel_partial` | Ensure demo battle effects are typed framework effects, not legacy strings. |
| 13 | `combat_math` | `parallel_partial` | Use framework combat policy directly for clean damage, accuracy, criticals, and instant-death checks where applicable. |
| 14 | `press_turn` | `parallel_partial` | Prove clean battle turn economy through player-driven commands. |
| 15 | `ailment_lifecycle` | `parallel_partial` | Add one clean ailment/status loop if still desired for the sample. |
| 16 | `passive_lifecycle` | `parallel_partial` | Prove passives through clean battle/turn lifecycle, not legacy startup parsing. |
| 17 | `enemy_ai_and_tactics` | `parallel_partial` | Add a minimal clean AI policy for demo enemies. |
| 18 | `battle_knowledge` | `parallel_partial` | Split persistent player knowledge from per-encounter AI knowledge, then save only player discoveries. |
| 19 | `battle_rewards` | `parallel_partial` | Apply clean rewards after battle through ruleset-bound services. |

### Phase 3: Save And Runtime Session

Goal: make the clean loop persistable.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 20 | `persistence_snapshots` | `clean_foundation` | Add interactive clean save/load policy over existing snapshots. |

This pass should include suspend-save policy if approved:

- save kind: manual, autosave, suspend;
- allowed save contexts;
- consume-after-load behavior for suspend saves;
- host-owned storage and serialization.

### Phase 4: Inventory, Equipment, Economy, Shops, Hospital

Goal: add non-battle resource systems to the clean demo.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 21 | `inventory_quantities` | `parallel_partial` | Make clean inventory quantities authoritative in the clean demo. |
| 22 | `equipment_ownership` | `parallel_partial` | Add clean ownership/equip behavior and stat/basic-attack impact. |
| 23 | `economy` | `parallel_partial` | Add clean wallet/Macca transactions. |
| 24 | `shops` | `parallel_partial` | Add a clean shop interaction over original shop content. |
| 25 | `hospital` | `parallel_partial` | Add clean restoration service only if the demo needs an equivalent recovery facility. |

### Phase 5: Party And Stock

Goal: support multi-actor ownership only when the demo needs it.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 26 | `active_and_reserve_party` | `parallel_partial` | Move clean demo party state through framework party snapshots/transitions. |
| 27 | `persona_and_demon_stock` | `parallel_partial` | Add clean stock only if the demo needs summonable/owned actors. |
| 28 | `party_operations` | `parallel_partial` | Add summon/return/swap/dismiss commands only after clean stock exists. |

These are not required for the first single-actor clean loop.

### Phase 6: Negotiation And Recruitment

Goal: add conversation/recruitment only if it remains part of the desired framework identity.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 29 | `negotiation_and_recruitment` | `parallel_partial` | Demonstrate clean negotiation/recruitment over original content. |

Before implementation, decide:

- whether negotiation is core or optional;
- whether recruitment adds stock members;
- whether failure/trick/flee outcomes remain part of the clean design.

### Phase 7: Fusion And Compendium

Goal: add fusion only after the owner approves the game-specific direction.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 30 | `fusion_result_calculation` | `parallel_partial` | Prove clean fusion result calculation over original content. |
| 31 | `fusion_slots_mutation_accidents` | `parallel_partial` | Add inheritance slots/mutation/accidents only if approved. |
| 32 | `fusion_preview_confirmation` | `parallel_partial` | Add clean preview and confirmation flow. |
| 33 | `fusion_transactions` | `parallel_partial` | Make clean fusion transactions atomic in runtime state. |
| 34 | `fusion_strategies` | `clean_foundation` | Replace strategy assumptions with approved framework policies. |
| 35 | `compendium` | `parallel_partial` | Add clean registration/recall/persistence if the design needs it. |

Fusion is deliberately late because it is design-heavy. Do not deepen SMT-style assumptions by default.

### Phase 8: Presentation And Archive Gate

Goal: verify host interchangeability and only then review archive eligibility.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 36 | `console_presentation` | `parallel_partial` | Ensure console presentation is only a host over framework commands/results. |
| 37 | archive review | no candidates | Review specific clean-parity capabilities for archive eligibility. |

Godot presentation should be able to reuse the same framework commands/results later, but a real Godot project is not required for console parity.

## Capability Detail Checklist

This section records what each protected capability still needs before it can become `clean_parity`.

### 01. `interactive_boot`

Current status: `parallel_partial`.

Full parity target:

- clean interactive entry exists;
- it loads original clean content only;
- it can start, run, and exit without legacy `Database`;
- ordinary legacy startup remains available until archive review.

Clean console proof:

- `--clean-training-annex-play` or equivalent starts the clean session.

Phase 1-01 result:

- `--clean-training-annex-play` now routes before the legacy `ConsoleGameHost` path.
- The command lives in the separated clean-console area under `Host/CleanConsole/TrainingAnnex/`.
- It loads only `convergence.training_annex_slice`, hydrates Echo Adept through `CatalogBattleActorFactory`, exposes inspect-session / inspect-actor / validate-startup-snapshot / exit commands, and validates a startup `RuntimeSaveGameSnapshot`.
- This is still `parallel_partial`: it proves clean boot/session ownership, but not field movement, battle commands, shops, fusion, negotiation, or archive eligibility.

### 02. `actor_models`

Current status: `parallel_partial`.

Full parity target:

- clean runtime actor snapshots cover playable actors and enemies needed by the clean loop;
- actor kind, identity, level, stats, resources, skills, defenses, and progression are framework-owned;
- no live `Combatant` or `Persona` is needed by the clean consumer.

Clean console proof:

- Training Annex playable actor and enemies are hydrated only from `GameDataCatalog`.

Phase 1-02 result:

- The clean Training Annex play host now builds a clean actor roster through `TrainingAnnexHostSupport`.
- Echo Adept plus Ashling, Bramble Runner, and Ward Shell are hydrated as framework `CatalogBattleActor` / runtime actor state from the clean catalog and encounter definitions.
- Actor inspection now shows player/enemy role, instance ID, level, resources, stats, active skills, and passives.
- Startup snapshot validation now covers the clean actor roster, not only the player actor.
- This is `parallel_partial`, not `clean_parity`: the original clean slice proves the framework actor path, but protected legacy actor categories such as Human, Persona User, Wild Card, and Operator are still legacy/adapter-owned.

### 03. `resource_recalculation`

Current status: `parallel_partial`.

Full parity target:

- framework owns HP/SP max calculation and current-resource policy;
- actor hydration and level-up/resource changes use the same policy;
- no legacy `GrowthProcessor` path is needed for clean actors.

Clean console proof:

- item use, battle damage, recovery, and reward/level flow leave valid resource snapshots.

Phase 1-03 result:

- The clean Training Annex play host now binds the catalog `standard_growth` ruleset before actor roster creation.
- Training Annex actor HP/SP initialization uses `StandardResourceGrowthPolicy` through `TrainingAnnexResourceInitializationPolicy`, not the older demo-only battle initializer.
- The play host keeps a framework `RuntimeActorStateSet` for each clean runtime actor and validates save snapshots from those runtime snapshots.
- The new `Recalculate Resources` command applies a clean runtime HP transaction to Echo Adept and reruns the framework resource policy with preserve-current semantics.
- This is still `parallel_partial`: the clean Training Annex path uses the framework resource policy directly, but protected legacy console consumers still retain their adapter-backed `GrowthProcessor`/`Combatant` resource paths.

### 04. `stat_composition`

Current status: `parallel_partial`.

Full parity target:

- framework stat policy owns base/effective stats;
- equipment/stat modifiers flow through clean runtime state;
- clean host displays effective stats without legacy `StatProcessor`.

Clean console proof:

- inspect/status menu shows clean stat values from runtime snapshots.

Phase 1-04 result:

- The clean Training Annex play host now binds the catalog `standard_stat` ruleset through `RuntimeRulesetBindingResolver`.
- The interactive menu includes `Resolve Stats`, which previews Echo Adept stat composition through `StandardStatResolutionPolicy` instead of legacy `StatProcessor`.
- The preview applies a runtime `attack +1` stage and proves the current standard aliases: Strength and Magic are boosted, while Vitality, Agility, and Luck remain unchanged.
- Actor inspection now separates base stats from effective runtime stats.
- Equipment stat impact is not forced in this pass because the current Training Annex actors are authored as `demon`, and the standard stat policy intentionally resolves demon stats from active-form stats rather than accessory modifiers. Clean equipment/stat impact remains owned by pass `equipment_ownership`.
- This remains `parallel_partial`: the clean Training Annex shell exposes framework stat composition, but broader production equipment/stat consumers are still protected compatibility paths.

### 05. `growth_and_levels`

Current status: `parallel_partial`.

Full parity target:

- framework EXP curve and level-up policy own progression;
- random growth, stat allocation, and level-up resource changes are host-random-source friendly;
- clean loop applies EXP after battle.

Clean console proof:

- victory changes EXP/progression in the clean session.

Phase 1-05 result:

- The framework now has `RuntimeProgressionTransactionService`, which applies a `LevelGrowthResult` to a `RuntimeActorStateSet` with before/after mutation evidence.
- The clean Training Annex play host now includes `Apply Victory EXP`.
- `Apply Victory EXP` calculates the current level requirement through the catalog-bound `standard_growth` services, applies that EXP through `StandardLevelGrowthPolicy`, and stores the resulting progression back into Echo Adept's runtime snapshot.
- The scripted clean play test proves Echo Adept advances from level 3 to level 4, lifetime EXP changes from 0 to 40, unspent stat points change from 2 to 3, and startup save validation sees the updated runtime snapshot.
- Because the Training Annex actor is currently authored as `demon`, this pass does not roll humanoid base HP/SP growth. That is current standard-policy behavior, not an omission.
- This remains `parallel_partial`: clean runtime progression now exists in the Training Annex shell, but protected legacy `GrowthProcessor` consumers remain in place.

### 06. `moon_phase`

Current status: `legacy_only`.

Full parity target:

- moon/cycle data is optional host/session metadata;
- content that uses moon conditions declares that dependency;
- unrelated systems run without fake moon phase values;
- sacrificial fusion gates become policy-owned instead of hardcoded Full Moon assumptions.

Clean console proof:

- clean Training Annex loop runs without moon phase when no moon mechanic is used.

Phase 1-06 result:

- Training Annex no longer carries a `standard_moon_phase` ruleset.
- Training Annex catalog loading no longer registers moon phase IDs or the moon-phase policy.
- Training Annex runtime save/session snapshots omit `MoonPhaseId` instead of storing fake `new_moon` metadata.
- Clean automated battle and encounter requests now accept a missing moon phase, while existing moon-phase conditions still evaluate false unless a host supplies moon metadata.
- Legacy `MoonPhaseSystem`, Full Moon negotiation blocking, and legacy fusion Full Moon characterization remain untouched.
- This remains `legacy_only` for the protected moon-phase capability: the old moon mechanic has not been reimplemented as a clean optional feature; it has only been decoupled from clean paths that do not use it.

### 07. `field_navigation`

Current status: `parallel_partial`.

Full parity target:

- clean field/session navigation is framework state plus host commands;
- legacy `FieldConductor` is not part of the clean demo path.

Clean console proof:

- player navigates clean Training Annex menus over framework state.

Phase 1-07 result:

> **Correction before commit:** the first 1-07 draft modeled navigation as a fixed `City`/`Dungeon` switch. Review identified that as an inappropriate universal framework assumption, so it was replaced rather than preserved.

- The framework now has generic `ContentId` locations, explicit source/destination transitions, immutable transition results/events, and an injected `IRuntimeNavigationPolicy`.
- The framework applies navigation only when a host requests it. It does not render menus, move scene objects, define spatial controls, or assume cities and dungeons exist.
- The Training Annex console list is only a presentation adapter over two host-owned transitions: staging area to Annex entrance and back. A Godot trigger, VN hotspot, or script can issue the same requests.
- `RuntimeFieldSnapshot` holds generic navigation and optional dungeon traversal state. Save contract v2 allows the entire field module to be absent, navigation without a dungeon, or navigation combined with the optional dungeon module.
- Source mismatch and policy rejection preserve state. Reverse travel requires its own explicit transition.
- This remains `parallel_partial`: 1-07 proves the generic navigation boundary, while dungeon traversal and encounter triggers remain independent passes 1-08 and 1-09.

### 08. `dungeon_traversal`

Current status: `parallel_partial`.

Full parity target:

- dungeon progress, floor/room transitions, terminals, barriers, exits, and boss flags are framework-owned;
- host scene/trigger state chooses when to ask for an encounter.

Clean console proof:

- player enters, moves between arbitrary nodes, unlocks a checkpoint, receives a barrier rejection, and returns without traversal itself starting an encounter.

Phase 1-08 result:

- `RuntimeDungeonTraversalService` models arbitrary dungeon nodes with `ContentId` values. A node may represent a room, corridor, floor, scene, landmark, or any other host-defined place.
- Every move is an explicit source/destination transition checked by an injected `IRuntimeDungeonTraversalPolicy`; the framework has no hidden map, route, barrier, or progression assumptions.
- The traversal snapshot owns the current dungeon/node, visited nodes, unlocked checkpoints, and defeated boss IDs. Results and ordered events are immutable and deterministic.
- Checkpoint unlocks and boss-defeat registration are idempotent. Barrier behavior is demonstrated as a policy rejection that preserves the original state.
- The Training Annex console host presents node choices as lists, but a Godot doorway, collision trigger, scene script, or VN hotspot can request the same transition.
- Leaving the Annex is coordinated through the separate generic navigation service. Dungeon traversal does not automatically change outer-world location.
- The generic service never selects or starts encounters. The existing floor-oriented `RuntimeFieldDungeonService` remains an optional compatibility/sample module; host/entity-triggered encounters remain Phase 1-09.
- This remains `parallel_partial`: the generic framework mechanic and clean host proof exist, while the protected legacy dungeon consumer remains active and encounter authority is still separate.

### 09. `encounters`

Current status: `parallel_partial`.

Full parity target:

- encounter definitions and formations hydrate runtime actors through framework services;
- host-owned triggers can select encounters without relying on forced floor battles;
- random encounters remain optional policy, not mandatory exploration design.

Clean console proof:

- host trigger starts `ashling_drill` or another original clean encounter.

Phase 1-09 result:

- `CatalogEncounterPreparationService` accepts an explicit `RuntimeEncounterTriggerRequest`, resolves the selected catalog encounter/formation, and hydrates its members through `ICatalogBattleActorFactory` in authored order.
- The request carries a logical host trigger ID, qualified encounter ID, opponent team, local runtime-instance prefix, and optional explicit formation index. It contains no menu, scene, collision, floor, or Godot type.
- Preparation returns immutable result collections containing ordered, battle-ready actors, diagnostics, and trigger/actor/prepared events. The actor states are intentionally mutable for the upcoming battle. Planning or actor-creation failure rejects the complete preparation result without exposing a partial encounter.
- Traversal never calls encounter preparation. The Training Annex host prepares `ashling_drill` only when the player explicitly activates its host-owned Review Hall trigger.
- The sample host consumes that trigger after successful preparation. Other hosts may respawn it, gate it behind progression, or call it repeatedly; trigger lifecycle is not a framework rule.
- Random encounters remain opt-in. A developer may implement a host/ruleset policy that chooses when and which encounter request to submit; the framework performs no hidden random encounter roll.
- The prepared actors are ready for a host-owned battle handoff. Phase 2-11 now consumes that handoff through explicit clean battle actions; traversal still does not start battle by itself.
- This remains `parallel_partial`: original clean encounter preparation now exists, but the protected legacy encounter consumer and its hydration adapter are still active.

### 10. `field_items_and_skills`

Current status: `parallel_partial`.

Full parity target:

- field item and skill usage use framework execution environments;
- battle-only conditions evaluate correctly outside battle;
- inventory consumption is transaction-safe.

Clean console proof:

- clean field item/skill works in the Training Annex loop.

Phase 1-10 result:

- The clean Training Annex host now exposes separate inventory, item-selection, field-skill-selection, and target-selection menus. These are console presentation over framework commands, not framework UI.
- Annex Tonic and Mend execute through `BattleActionExecutor` in a typed `field` execution environment, reusing `ItemExecutor`, `SkillExecutor`, targeting, conditions, effects, and cost handling.
- Host inventory is represented by `RuntimeInventorySnapshot`. A console adapter implements `IItemActionInventory` with `InventoryTransitionService` reservations; successful meaningful item execution commits one item, while rejection or execution failure rolls the reservation back.
- Target selection completes before assessment, reservation, cost commitment, or effect mutation. Canceling target selection changes nothing.
- Using Annex Tonic at full HP is rejected as `NoApplicableEffect` and consumes nothing. A successful use restores HP and consumes exactly one. Mend restores HP and commits its authored SP cost.
- The console adapter synchronizes clean persistent actor resource snapshots with the action state before and after execution. Effect and consumption rules remain framework-owned.
- No legacy `ItemData`, `SkillData`, effect strings, `ActionProcessor`, field parser, or `Database` participates in this clean path.
- This remains `parallel_partial`: the original clean consumer exists, while protected ordinary-console field item/skill consumers still use compatibility paths.

### 11. `battle_actions`

Current status: `parallel_partial`.

Full parity target:

- attack, skill, item, guard, pass, analyze, swap, and escape commands are framework action commands;
- assessment and execution cannot disagree;
- clean consumer does not call legacy `ActionProcessor`.

Clean console proof:

- clean battle command menu uses `BattleActionExecutor` or framework encounter command ports.

Phase 2-11 result:

- The clean Training Annex play host now exposes `Start Prepared Battle` after the host-owned Ashling encounter trigger succeeds. The trigger still only prepares actors; battle starts only through the explicit command.
- The manual battle slice runs through `BattleEncounterRunner` with a host-side turn handler over `BattleActionExecutor`. Player commands produce concrete framework commands for Practice Blade basic attack, battle skills, Annex Tonic, guard, pass, and analyze.
- Practice Blade supplies the clean `BasicAttackBattleActionCommand` weapon profile. Frost Tip, Echo Strike, and level-unlocked Mend are selected from the actor's clean loadout/unlocks, filtered by `battle` availability.
- Annex Tonic reuses the reservation-backed `IItemActionInventory` path. Meaningful battle item execution commits one item; target/menu cancellation performs no assessment, reservation, turn consumption, or mutation.
- Demo enemies use deterministic authored-skill selection for this pass: first executable battle skill, otherwise pass. No legacy `ActionProcessor`, `SkillData`, `ItemData`, effect string, or `Database` participates.
- Persistent clean actor resources are synchronized before and after the battle so the session summary/save-facing state reflects battle damage, costs, healing, and defeat state.
- This remains `parallel_partial`: ailment/passive lifecycle, battle knowledge persistence, AI/tactics policy, escape, swaps, and rewards remain later Phase 2 passes.

### 12. `typed_effects`

Current status: `parallel_partial`.

Full parity target:

- all effects used by clean content are typed definitions;
- no effect behavior is inferred from display names or legacy strings;
- item and skill effects share the framework effect pipeline.

Clean console proof:

- demo skills/items use typed effects only.

Phase 2-12 result:

- The clean Training Annex battle summary now records host-level typed-effect evidence for executed battle commands: source action ID, effect index, schema-style effect kind, and typed operands such as damage element or resource ID.
- Practice Blade, Frost Tip, Echo Strike, Ash Spark, Annex Tonic, and Analyze are proven through this evidence. Guard and Pass remain effectless framework commands.
- Tests mutate Training Annex display names/descriptions through a test-only content source and prove battle outcome, resources, inventory, action IDs, and typed-effect evidence remain unchanged.
- The shell-facing Training Annex content is checked as concrete typed definitions, including damage, restore, cure, buff/debuff, ailment, and passive trigger effects.
- This remains `parallel_partial`: the demo still needs lifecycle/passive integration, battle knowledge persistence, AI/tactics policy, escape/swaps, and reward application before full battle parity.

### 13. `combat_math`

Current status: `parallel_partial`.

Full parity target:

- clean battle uses framework combat rules directly;
- numeric policies are ruleset-bound where approved;
- no legacy `CombatMath` or `DamageHandler` is required by clean battle.

Clean console proof:

- Training Annex battle damage/accuracy/critical/reward values come from framework services.

Phase 2-13 result:

- The Training Annex session binds `standard_damage` once through `RuntimeRulesetBindingResolver` and supplies the resulting `ProductionCombatRuleset` to damage, instant-death, ailment, chance, and power execution policies. Only deterministic target selection remains host-owned.
- `standard_reward` binds to the same combat ruleset. A successful manual battle records a non-mutating reward preview; applying EXP and Macca remains Phase 2-19.
- The clean battle summary records authored power/accuracy/critical mode beside the resolved hit, critical, affinity, value, effect outcome, and Press Turn outcome. Tests prove ruleset-bound Weak damage, misses, physical criticals, and magical critical rejection.
- Missing or incompatible combat/reward rulesets stop startup with typed binding diagnostics. There is no fallback to the temporary demo policies or legacy `CombatMath`/`DamageHandler`.
- This remains `parallel_partial`: the original clean battle now owns its combat policy path, but lifecycle-owned status interactions and reward application remain later Phase 2 passes.

### 14. `press_turn`

Current status: `parallel_partial`.

Full parity target:

- clean battle loop consumes typed action outcomes through framework Press Turn rules;
- host only presents the current turn state;
- legacy battle conductor is not in the clean battle path.

Clean console proof:

- player commands visibly affect Press Turn state in the clean battle loop.

Phase 2-14 result:

- `--clean-training-annex-play` now binds `standard_press_turn` from the Training Annex ruleset document before the session starts. Missing, wrong-category, or unsupported Press Turn rulesets stop startup with binding diagnostics instead of falling back to an implicit engine.
- The prepared Ashling battle passes the bound `PressTurnEngine` factory into `BattleEncounterRunner`; the host no longer assumes a hard-coded turn engine for that clean path.
- The manual battle summary records host-side Press Turn evidence for each committed action: actor, action, before icons, turn-consumption kind, resolved Press Turn outcome, and after icons.
- The clean console output presents current and updated icon counts while suppressing unrelated framework structural events.
- This remains `parallel_partial`: clean original-content battles now expose and consume Press Turns, but lifecycle/passives, battle knowledge persistence, richer AI/tactics, escape/swaps, and reward application remain later Phase 2 passes.
- Verification: focused Training Annex tests passed `29/29`; full suite passed `786/786` with no skips; framework build remained `0` warnings; solution build remained at `98` legacy warnings; clean battle, field, save, and Training Annex demos passed; `Data/Jsons` was unchanged.

### 15. `ailment_lifecycle`

Current status: `parallel_partial`.

Full parity target:

- clean ailment definitions, application, ticking, recovery, and exclusivity are framework-owned;
- content declares ailment behavior explicitly;
- clean battle lifecycle uses framework service directly.

Clean console proof:

- optional first ailment sample can apply, tick, recover, and display outcome.

Phase 2-15 implementation note:

- `--clean-training-annex-play` now routes the prepared manual battle through `BattleStatusLifecycleService` via a Training Annex lifecycle port instead of the previous no-op lifecycle port.
- The clean battle summary records lifecycle evidence for action-owned ailment application/removal and framework turn-start/turn-end events such as poison resource ticks, skip restrictions, recovery, removal, and expiry.
- Toxin Touch and Clear Toxin are recognized by the clean battle skill shell when an actor actually knows those skills. The default Training Annex JSON remains unchanged; focused tests use test-only in-memory content variants to exercise poison, stun, and cure paths.
- Passive trigger dispatch is intentionally suppressed inside this 2-15 lifecycle port so Phase 2-16 remains the owner of passive lifecycle proof.
- Verification: focused Training Annex tests passed `32/32`; lifecycle-focused tests passed `39/39`; full suite passed `789/789`; framework build stayed at `0` warnings and solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all completed successfully.

### 16. `passive_lifecycle`

Current status: `parallel_partial`.

Full parity target:

- passives trigger from framework lifecycle events;
- rule modifiers apply without skill-name checks;
- activation limits and cleanup are framework-owned.

Clean console proof:

- Training Annex passive recovery or modifier runs through clean lifecycle.

Phase 2-16 implementation note:

- `--clean-training-annex-play` now uses the real framework passive dispatcher during battle startup and owner turn end; the temporary 2-15 no-op dispatcher has been removed.
- The existing `Steady Breath` content trigger restores HP through `PassiveTriggerDispatcher`, the shared typed effect pipeline, and `BattleStatusLifecycleService` after committed owner actions.
- Battle-start passives use the same dispatcher, while `BattleEncounterRunner` remains responsible for resetting per-battle activation counts before startup dispatch.
- Passive activations are exposed as typed lifecycle evidence and `PassiveActivated` encounter events. Back/cancel selections do not dispatch owner-turn-end passives because they do not commit a turn.
- A test-only passive modifier doubles only Physical damage through `EffectElementConditionDefinition` after its display name and description are replaced, proving the clean path does not infer passive behavior from text.
- Status remains `parallel_partial`: the clean slice proves framework-owned triggers and modifiers, but broader passive content, defeat-prevention presentation in the clean host, and the remaining Phase 2 battle capabilities are still pending. Legacy removal remains unauthorized.
- Verification: focused Training Annex/passive/lifecycle tests passed `53/53`; the full suite passed `791/791`; the framework build stayed at `0` warnings and the solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all completed successfully.

### 17. `enemy_ai_and_tactics`

Current status: `parallel_partial`.

Full parity target:

- clean enemy choice policy is framework-owned or host-injected through framework contracts;
- tactics/direct control are typed commands, not console-only branches;
- ailment forced actions share lifecycle outcomes.

Clean console proof:

- demo enemy chooses deterministic legal actions without legacy `BehaviorEngine`.

Phase 2-17 implementation note:

- The Training Annex manual battle no longer contains its own loop over enemy skills. It injects framework `DeterministicBattleActionSelector` through the `IBattleActionSelector` contract.
- The selector evaluates typed active-skill definitions in authored loadout order, resolves targets from typed targeting, and uses the same `SkillExecutor.Assess` path that execution uses. Unavailable, invalid-target, and unaffordable skills are not legal candidates.
- Equal-scored legal skills preserve authored order. If no legal skill exists, the selector returns a typed Pass decision and the action executes through `BattleActionExecutor`.
- The clean battle summary records immutable AI decision evidence: actor instance/entity IDs, selected/pass status, action ID, target IDs, and assessment success. Display names are presentation only.
- Turn-start lifecycle restrictions run before strategy selection. A skipped actor produces no AI decision or action.
- Enemy knowledge is intentionally empty and session-local in this pass. Learning and persistence remain Phase 2-18 rather than being hidden inside the AI implementation.
- Status remains `parallel_partial`: the original-content enemy path is framework-selected, but configurable tactics/direct-control switching and the protected legacy `BehaviorEngine` consumer are still active work. No removal is authorized.
- Verification: focused Training Annex/framework-selector tests passed `38/38`; the full suite passed `794/794`; the framework build stayed at `0` warnings and the solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all completed successfully.

### 18. `battle_knowledge`

Current status: `parallel_partial`.

Full parity target:

- elemental, ailment, and instant-death knowledge live in framework snapshots;
- analyze/discovery updates are clean runtime events;
- persistence includes player-owned knowledge state;
- AI/encounter knowledge is scoped to the current battle unless a host explicitly supplies a special persistent source, such as a boss or scripted encounter.

Clean console proof:

- player actions and Analyze update persistent player knowledge for future UI hints;
- enemy AI learns within one battle but starts a fresh random encounter without prior discoveries;
- clean battle can reuse each knowledge scope through the correct consumer: UI/player-facing state reads player knowledge, enemy tactics read encounter-local AI knowledge.

Phase 2-18 implementation note:

- `--clean-training-annex-play` now owns persistent player battle knowledge over the framework `ElementalAffinityKnowledge`, `AilmentResistanceKnowledge`, and `InstantDeathResistanceKnowledge` stores.
- Each manual battle creates a fresh encounter AI knowledge state. Enemy strategy reads only that encounter-local elemental store, so ordinary enemy learning is discarded when the battle ends.
- Player-owned damage actions learn elemental affinity from typed `EffectExecutionResult` data. Player Analyze learns elemental, ailment, and instant-death channels from the target's typed defense profile and catalog ailments.
- Enemy-owned observations update encounter AI evidence and the last-encounter AI snapshot only. They are not written to the player/save-facing `RuntimeKnowledgeSnapshot`.
- The clean battle summary exposes player knowledge evidence, encounter AI knowledge evidence, the persistent player `RuntimeKnowledgeSnapshot`, and the last encounter AI snapshot for tests.
- Framework enemy selection now receives encounter-local AI knowledge. Tests force Ashling to learn that Echo Adept resists Fire, verify the selector switches to a different legal skill within that battle, and verify that discovery is absent from saved player knowledge.
- No Training Annex JSON, framework public API, legacy `BattleKnowledge`, or production `Data/Jsons` file changed.
- Status remains `parallel_partial`: original clean content now learns and reuses knowledge, but legacy battle consumers still have their protected knowledge path and no removal is authorized.
- Verification: focused Training Annex tests passed `39/39`; the full suite passed `796/796`; the framework build stayed at `0` warnings and the solution build stayed at `98` existing legacy warnings. Clean battle, field, save, and Training Annex demos all completed successfully.

### 19. `battle_rewards`

Current status: `parallel_partial`.

Full parity target:

- framework reward services calculate EXP/Macca or equivalent reward outputs;
- ruleset/content binding is explicit;
- clean loop applies rewards to runtime session state.

Clean console proof:

- Training Annex victory applies nonzero rewards and records session progress.

### 20. `persistence_snapshots`

Current status: `clean_foundation`.

Full parity target:

- framework snapshot and validator cover the clean session;
- save policy defines manual/autosave/suspend behavior;
- host owns actual storage and serialization;
- loading restores equivalent framework runtime state.

Clean console proof:

- clean demo can save, load, and optionally consume a suspend save.

### 21. `inventory_quantities`

Current status: `parallel_partial`.

Full parity target:

- framework inventory snapshots and transactions own quantities, reservation, commit, rollback, and stack limits;
- clean host never mutates legacy `InventoryManager`.

Clean console proof:

- clean item use consumes inventory only on meaningful success.

### 22. `equipment_ownership`

Current status: `parallel_partial`.

Full parity target:

- clean equipment definitions, ownership, equip/unequip, and stat/basic-attack impact are framework-owned;
- clean content provides original equipment.

Clean console proof:

- player buys or equips sample equipment and sees stat/action impact.

### 23. `economy`

Current status: `parallel_partial`.

Full parity target:

- framework wallet/economy transactions own add/spend/rollback;
- pricing policy is ruleset-bound where approved.

Clean console proof:

- clean shop or reward loop changes wallet state.

### 24. `shops`

Current status: `parallel_partial`.

Full parity target:

- clean shop catalogs, offer lists, pricing, stock policy, and transaction diagnostics are framework-owned;
- console only presents buy/sell choices.

Clean console proof:

- Training Annex shop sells at least one clean item/equipment record.

### 25. `hospital`

Current status: `parallel_partial`.

Full parity target:

- restoration costs, payment, HP/SP restoration, ailment removal, and failure diagnostics are framework-owned;
- host presentation is replaceable.

Clean console proof:

- optional clean recovery facility demonstrates restoration if still desired.

### 26. `active_and_reserve_party`

Current status: `parallel_partial`.

Full parity target:

- party membership is represented by framework party snapshots;
- active/reserve changes use framework transition results;
- clean consumer does not mutate legacy party lists.

Clean console proof:

- only needed once the clean demo has more than one party actor.

### 27. `persona_and_demon_stock`

Current status: `parallel_partial`.

Full parity target:

- owned stock is framework runtime state;
- stock capacity and active form rules are framework-owned;
- no `Combatant`/`Persona` stock list is needed by the clean consumer.

Clean console proof:

- only needed if recruitment/fusion/summon features enter the clean demo.

### 28. `party_operations`

Current status: `parallel_partial`.

Full parity target:

- summon, return, swap, dismiss, replace, and consume operations use framework transitions;
- failed commands return diagnostics without mutation;
- presentation is host-owned.

Clean console proof:

- only needed after clean stock exists.

### 29. `negotiation_and_recruitment`

Current status: `parallel_partial`.

Full parity target:

- framework owns negotiation state, prompts, demand outcomes, recruitment validation, and result diagnostics;
- original clean negotiation content exists;
- clean host presents prompts without legacy `questions.json`.

Clean console proof:

- only after owner confirms negotiation is part of the clean framework sample.

### 30. `fusion_result_calculation`

Current status: `parallel_partial`.

Full parity target:

- framework resolves fusion results from approved original content and policies;
- no legacy `fusion_table.json` authority is required.

Clean console proof:

- optional after fusion design approval.

### 31. `fusion_slots_mutation_accidents`

Current status: `parallel_partial`.

Full parity target:

- inheritance slots, mutation, and accidents are explicit framework policies;
- passive inheritance rules remain typed;
- mutation/accident features are included only if approved.

Clean console proof:

- optional after fusion design approval.

### 32. `fusion_preview_confirmation`

Current status: `parallel_partial`.

Full parity target:

- preview and final commit share the same framework planning result;
- selected inheritance is validated before commit;
- console only presents choices.

Clean console proof:

- optional after fusion design approval.

### 33. `fusion_transactions`

Current status: `parallel_partial`.

Full parity target:

- parent consumption, result ownership, stock updates, and rollback are framework transaction decisions;
- no legacy `FusionMutator` is needed by the clean consumer.

Clean console proof:

- optional after fusion design approval.

### 34. `fusion_strategies`

Current status: `clean_foundation`.

Full parity target:

- standard/rank/sacrificial/stat-boost policies are approved framework concepts;
- unsupported legacy-inspired strategies remain absent or optional.

Clean console proof:

- optional after fusion design approval.

### 35. `compendium`

Current status: `parallel_partial`.

Full parity target:

- registration, recall, pricing, snapshot isolation, persistence, and stock checks are framework-owned;
- clean content identifies species/entities explicitly.

Clean console proof:

- optional after fusion/recruitment design approval.

### 36. `console_presentation`

Current status: `parallel_partial`.

Full parity target:

- console presentation is a host over framework commands, events, diagnostics, and snapshots;
- it owns text, menus, colors, waits, and formatting only;
- it can be replaced by Godot presentation without replacing framework logic.

Clean console proof:

- every clean demo capability uses host presentation boundaries, not legacy rule logic.

## First Recommended Implementation Pass

Do not start with all 36.

Start with:

```text
Pass 01: interactive_boot
```

Scope:

- add the clean interactive console entry;
- load only original clean Training Annex content;
- create a clean session shell;
- add minimal inspect/enter/exit commands;
- no battle/shop/fusion/negotiation yet.

Why first:

- every later capability needs a clean consumer to prove it;
- without this shell, capabilities remain framework services without a player-facing proof;
- this keeps us on one plan instead of jumping between "console demo" and "capability parity."

## Update Rules

When a pass completes:

1. update this document's capability section with actual evidence;
2. update `recovery-baseline.json` only if the capability status truly changes;
3. update [Framework State And Roadmap](framework-state-and-roadmap.md);
4. update [Clean Console Host Demo Plan](clean-console-host-demo-plan.md) only if the host iteration details change;
5. do not change `removalAuthorized` unless a separate archive gate is approved.

## Current Non-Negotiables

- No direct prototype data conversion.
- No legacy source archive.
- No broad namespace migration.
- No Godot project requirement before the console proof.
- No optional mechanic should become mandatory merely because the sample needs a placeholder.
- No capability reaches `clean_parity` without a clean consumer.
