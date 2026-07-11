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

The executable ledger now uses a `futurePhase` field rather than old lettered track labels. Those values point back to the numbered passes in this document, such as `4-21` for inventory quantities. If the ledger and this sequence disagree, stop and align them before implementation.

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
2. Confirm its `futurePhase` matches the numbered pass being worked.
3. Identify the legacy authority being replaced or bypassed.
4. Decide whether the behavior should be preserved, changed, or dropped.
5. Add or adjust original clean content only if needed.
6. Implement the smallest framework change needed.
7. Add the smallest clean console proof needed.
8. Keep Godot compatibility by avoiding console/filesystem/serializer types in framework APIs.
9. Add focused tests.
10. Run the quality gate.
11. Update this plan and the parity ledger only if the evidence justifies it.

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
| 27 | `persona_and_demon_stock` | `parallel_partial` | Implemented inspectable clean active-form, Persona-stock, and Demon-stock ownership in Training Annex. |
| 28 | `party_operations` | `parallel_partial` | Implemented Training Annex clean stock operations over framework transitions. |

These are not required for the first single-actor clean loop.

### Phase 6: Negotiation And Recruitment

Goal: add conversation/recruitment only if it remains part of the desired framework identity.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 29 | `negotiation_and_recruitment` | `parallel_partial` | Implemented Training Annex clean negotiation/recruitment over framework services. |

Owner decision recorded before implementation:

- negotiation remains an optional framework capability that a host/game may choose to use;
- this clean proof uses recruitment into Demon stock;
- refusal and familiar/duplicate paths remain supported because they are useful framework outcomes, not console-only quirks.

### Phase 7: Fusion And Compendium

Goal: add fusion only after the owner approves the game-specific direction.

| Pass | Capability | Current Status | Goal |
| ---: | --- | --- | --- |
| 30 | `fusion_result_calculation` | `parallel_partial` | Implemented clean fusion result calculation over original content. |
| 31 | `fusion_slots_mutation_accidents` | `parallel_partial` | Prove clean slots, typed inheritance display, and mutation/accident evidence. |
| 32 | `fusion_preview_confirmation` | `parallel_partial` | Add clean preview and confirmation flow. |
| 33 | `fusion_transactions` | `parallel_partial` | Make clean fusion transactions atomic in runtime state. |
| 34 | `fusion_strategies` | `parallel_partial` | Implemented explicit framework policies for fusion strategy decisions. |
| 35 | `compendium` | `parallel_partial` | Add clean registration/recall/persistence if the design needs it. |

Fusion is deliberately late because it is design-heavy. Do not deepen SMT-style assumptions by default.

Phase 7 review status: review-closed for the approved original-content scope. [Phase 7 Code Review And Readiness](phase-7-code-review.md) defines CodeReview-7-1 through CodeReview-7-5. CodeReview-7-1 preserves the authored binary recipe contract. CodeReview-7-2 enforces one global runtime-ID invariant. CodeReview-7-3 moves clean fusion preparation, parent consumption, result placement, actor construction, and rollback into an injected framework transaction service while leaving confirmation host-owned. CodeReview-7-4 preserves policy context through accident mutation and makes standalone slot-policy context explicit. CodeReview-7-5 rejects malformed Compendium entries at save, registration, and recall boundaries. Phase 8 may begin; capabilities remain `parallel_partial` and removal remains unauthorized.

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
- This remains `parallel_partial`: ailment/passive lifecycle, battle knowledge persistence, AI/tactics policy, escape, swaps, and reward application still remain later Phase 2 passes at this point in the history.

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
- `standard_reward` binds to the same combat ruleset. A successful manual battle records a reward preview; Phase 2-19 later applies that same result to EXP, Macca, and session progress.
- The clean battle summary records authored power/accuracy/critical mode beside the resolved hit, critical, affinity, value, effect outcome, and Press Turn outcome. Tests prove ruleset-bound Weak damage, misses, physical criticals, and magical critical rejection.
- Missing or incompatible combat/reward rulesets stop startup with typed binding diagnostics. There is no fallback to the temporary demo policies or legacy `CombatMath`/`DamageHandler`.
- This remains `parallel_partial`: the original clean battle now owns its combat policy path, but lifecycle-owned status interactions and reward application still remain later Phase 2 passes at this point in the history.

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
- This remains `parallel_partial`: clean original-content battles now expose and consume Press Turns, but lifecycle/passives, battle knowledge persistence, richer AI/tactics, escape/swaps, and reward application still remain later Phase 2 passes at this point in the history.
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
- future ownership/Compendium imports may update player knowledge before battle presentation: if the player has owned, recruited, fused, recalled, or registered a species/entity, the framework should be able to seed known affinities/resistances into the player knowledge base for UI hints without granting that memory to ordinary enemy AI.

Clean console proof:

- player actions and Analyze update persistent player knowledge for future UI hints;
- enemy AI learns within one battle but starts a fresh random encounter without prior discoveries;
- clean battle can reuse each knowledge scope through the correct consumer: UI/player-facing state reads player knowledge, enemy tactics read encounter-local AI knowledge.
- deferred proof: owning or registering a familiar entity in the Compendium can seed player-facing battle knowledge before target selection, so a later encounter can immediately show discovered affinity icons for that familiar entity.

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

Phase 2-19 result:

- The manual Training Annex battle still receives reward totals from the catalog-bound `standard_reward` service.
- On player victory, the clean play host now commits those totals to runtime state: EXP flows through `standard_growth`, Macca flows through `EconomyTransactionService`, and session counters/flags record the cleared Ashling drill.
- The post-battle save-facing snapshot includes live inventory, wallet, session progress, field state, and persistent player battle knowledge.
- Defeat, cancellation, and non-victory outcomes do not apply rewards.
- This remains `parallel_partial`: rewards are now clean for the Training Annex original-content loop, but legacy battle reward consumers and broader production reward content remain protected.

### 20. `persistence_snapshots`

Current status: `clean_foundation`.

Full parity target:

- framework snapshot and validator cover the clean session;
- save policy defines manual/autosave/suspend behavior;
- host owns actual storage and serialization;
- loading restores equivalent framework runtime state.

Clean console proof:

- clean demo can save, load, and optionally consume a suspend save.

Phase 3-20 result:

- `JRPG.Framework` now has serializer-neutral save policy contracts for `Manual` and `Suspend` records, save contexts, pending-host-action rejection, allowed-context checks, and suspend-load consumption instructions.
- `--clean-training-annex-play` exposes a host-owned `Save / Load` submenu with one in-memory manual slot and one in-memory suspend slot. The host serializes `RuntimeSaveRecord` through its own JSON codec, validates the restored snapshot against the live catalog, and applies restored runtime state only after deserialize, policy, validation, and Training Annex session restore all succeed.
- Manual load keeps the manual slot. Suspend load consumes the suspend slot only after successful restore. Malformed JSON, missing slots, context rejection, validation errors, and pending Ashling battle handoff attempts leave the current session unchanged.
- Restored Training Annex state includes actor runtime snapshots, resources synchronized back into clean battle actors, inventory, wallet, generic field/dungeon state, session counters/flags, host-owned prepared-battle menu state, and persistent player battle knowledge.
- This remains `clean_foundation`: the framework now has the policy and snapshot foundation plus one clean consumer proof, but permanent filesystem slots, save migration helpers, autosaves, battle saves, Godot save resources, and legacy prototype save/load remain outside this phase.

### 21. `inventory_quantities`

Current status: `parallel_partial`.

Full parity target:

- framework inventory snapshots and transactions own quantities, reservation, commit, rollback, and stack limits;
- clean host never mutates legacy `InventoryManager`.

Clean console proof:

- clean item use consumes inventory only on meaningful success.

Phase 4-21 result:

- `--clean-training-annex-play` field inventory now lists usable clean items from the live `RuntimeInventorySnapshot` rather than a hardcoded item row.
- Field item choices carry `HostCommandSelectionIdentity.ForContent(...)`, resolve back to catalog `ItemDefinition`s, and execute the selected item through the existing `BattleActionExecutor`/`ItemExecutor` field path.
- `TrainingAnnexItemActionInventory` remains the mutation bridge: it reserves through `InventoryTransitionService.ReserveItem`, commits only after meaningful execution, and leaves the snapshot unchanged on rejection, failure, no-effect use, or target cancellation.
- Focused tests prove Annex Tonic and Focus Tea are selected by content ID, non-usable key items and zero-quantity rows are not actionable, successful selected-item execution consumes only that item, and field skill execution no longer reports a fake item quantity.
- This remains `parallel_partial`: the original clean consumer has authoritative framework inventory quantities, but broader equipment, economy, shop, hospital, legacy inventory UI, and production content conversion remain later Phase 4 work.

### 22. `equipment_ownership`

Current status: `parallel_partial`.

Full parity target:

- clean equipment definitions, ownership, equip/unequip, and stat/basic-attack impact are framework-owned;
- clean content provides original equipment.

Clean console proof:

- player buys or equips sample equipment and sees stat/action impact.

Phase 4-22 result:

- `JRPG.Framework` now has `RuntimeEquipmentProfileResolver`, which resolves equipped definitions from `RuntimeEquipmentSnapshot` plus catalog repositories into a gameplay-facing equipment profile: equipped definitions, accessory stat modifiers, weapon basic-attack profile, and typed diagnostics for missing or slot-mismatched equipment.
- `EquipmentTransitionService.Equip(...)` now rejects re-equipping the same item into the same slot with `EquipmentAlreadyEquipped`, preserving the unchanged snapshot on rejection.
- `RuntimeActorState` exposes `ReplaceEquipment(...)` so clean hosts and restore paths can commit equipment snapshots onto the same canonical actor state used by battle, field effects, growth, and save snapshots.
- `--clean-training-annex-play` seeds original sample equipment through framework inventory/equipment transactions: `practice_blade` in the weapon slot and `focus_charm` in the accessory slot. The summary records both the raw equipment snapshot and the resolved profile.
- Manual clean basic attack now uses the actor's equipped weapon profile rather than a hardcoded `practice_blade` lookup. A test-only alternate `weighted_club` content source proves the action ID, display label, power, and accuracy all change when the equipped weapon changes.
- Framework tests prove accessory stat modifiers feed `StandardStatResolutionPolicy` for actor kinds that use equipment modifiers. Training Annex itself does not force visible stat changes here because Echo Adept is authored as `demon`, and `standard_stat` intentionally resolves demon stats from active-form stats rather than accessories.
- This remains `parallel_partial`: clean ownership/equip state and basic-attack impact are now framework-owned and demonstrable, but there is still no clean interactive equip/shop UI, no production equipment reauthoring, and no legacy equipment removal authorization.

### 23. `economy`

Current status: `parallel_partial`.

Full parity target:

- framework wallet/economy transactions own add/spend/rollback;
- pricing policy is ruleset-bound where approved.

Clean console proof:

- clean shop or reward loop changes wallet state.

Phase 4-23 result:

- `--clean-training-annex-play` now binds `convergence.training_annex_slice:standard_economy` before the session starts and exits with typed `economy` diagnostics when the ruleset is missing, miscategorized, or unsupported. There is no direct-service fallback.
- The bound `ResourceManagementRulesetServices` instance supplies the clean host's inventory, equipment, and economy transaction services, keeping the Phase 4 resource systems under one authored policy boundary.
- The host owns one live `RuntimeWalletSnapshot`, accepts a host-selected starting balance, applies battle Macca through `IEconomyTransactionService`, records the actual immutable `WalletTransactionResult`, and carries the resulting wallet through save validation and restore.
- `EconomyTransactionService.AddMacca(...)` now rejects integer overflow with `InvalidCurrencyAmount` and preserves the original wallet snapshot instead of throwing across the transaction boundary. Negative and insufficient-funds operations remain non-mutating typed rejections.
- Focused tests prove reward income adds to an injected balance rather than resetting or assuming zero, invalid economy bindings fail before any command read, and rejected/overflowing operations preserve the exact before-state.
- This remains `parallel_partial`: clean reward income is now policy-bound and authoritative, and Phase 4-24 adds real clean spending through catalog-backed shop transactions. Broader production economy parity still waits on content/consumer migration and removal authorization. No artificial spend command was added solely to satisfy this checkpoint.
- Phase 4-23 verification passed `73/73` focused tests and `830/830` full-suite tests with no skips. The framework build remained at `0` warnings, the solution remained at `98` pre-existing legacy-host warnings, all four noninteractive clean demos exited successfully, framework boundary searches were clean, and `Data/Jsons` was unchanged.

### 24. `shops`

Current status: `parallel_partial`.

Full parity target:

- clean shop catalogs, offer lists, pricing, stock policy, and transaction diagnostics are framework-owned;
- console only presents buy/sell choices.

Clean console proof:

- Training Annex shop sells at least one clean item/equipment record.

Phase 4-24 result:

- `--clean-training-annex-play` now exposes a `Training Supply` clean shop option over the authored `convergence.training_annex_slice:training_supply` shop catalog.
- The host resolves authored shop offers through `RuntimeShopOfferResolver`, which maps fixed prices, limited/unlimited stock gates, item stack limits, and equipment slots into runtime shop-offer snapshots without consulting legacy shop DTOs or display text.
- Buy and sell menus are presentation only. Their enabled/disabled rows are produced from the same bound `IShopTransactionService` assessment used for execution, so insufficient funds, duplicate equipment, equipped-sale blocks, stack limits, and unavailable stock cannot disagree between display and mutation.
- Successful purchases and sales mutate the clean host's live `RuntimeInventorySnapshot` and `RuntimeWalletSnapshot` through the bound `standard_economy` services. Equipment purchases can then be equipped through `IEquipmentTransitionService`, and the actor's clean `RuntimeEquipmentSnapshot` and resolved profile update immediately.
- The default Training Annex wallet remains `0`; funded purchase/equip tests inject a starting wallet through the host boundary. This preserves the previous economy baseline while proving real clean spending without adding a fake money command.
- This remains `parallel_partial`: clean original-content shop interactions now exist, but legacy shops still use their protected console path, clean stock-state persistence is minimal, hospital remains Phase 4-25, and no legacy shop files are removable.
- Phase 4-24 verification passed `84/84` focused tests and `836/836` full-suite tests with no skips.

### 25. `hospital`

Current status: `parallel_partial`.

Full parity target:

- restoration costs, payment, HP/SP restoration, ailment removal, and failure diagnostics are framework-owned;
- host presentation is replaceable.

Clean console proof:

- optional clean recovery facility demonstrates restoration if still desired.

Phase 4-25 result:

- `--clean-training-annex-play` now exposes a `Recovery Facility` clean option after `Training Supply`.
- The facility creates a `RuntimeHospitalPatientSnapshot` from the live clean actor, assesses treatment through the bound `IHospitalRestorationService`, and uses that same service for execution. Menu availability therefore cannot disagree with mutation.
- Successful treatment spends Macca through the bound `standard_economy` service, restores HP/SP, removes removable ailments, and clears encounter-persistent clean battle state through the framework lifecycle cleanup service.
- Failed or unnecessary treatment remains non-mutating: insufficient funds and no-restoration-needed rows are disabled and recorded by tests.
- This remains `parallel_partial`: the original clean host now proves recovery-facility behavior, but the protected legacy city hospital remains on its compatibility path and no legacy hospital files are removable.
- Phase 4-25 verification passed `87/87` focused tests and `839/839` full-suite tests with no skips.

### 26. `active_and_reserve_party`

Current status: `parallel_partial`.

Full parity target:

- party membership is represented by framework party snapshots;
- active/reserve changes use framework transition results;
- clean consumer does not mutate legacy party lists.

Clean console proof:

- Phase 5-26 result:
  - `--clean-training-annex-play` now hydrates Echo Adept as the active player actor and Annex Mentor as a reserve support actor from the original clean catalog.
  - The session builds a `RuntimePartyStockSnapshot` through `PartyStockTransitionService.AddPartyMember` instead of hand-mutating host lists.
  - The clean summary records the live party snapshot and transition evidence; the host can inspect party state through `Inspect Party`.
  - Manual/suspend save snapshots include the live party stock and restore it with the actor roster.
  - This remains `parallel_partial`: the original clean host proves active/reserve party ownership, but Persona/Demon stock and broader party operations remain later phases.
  - Phase 5-26 verification passed `88/88` focused tests and `843/843` full-suite tests with no skips.

### 27. `persona_and_demon_stock`

Current status: `parallel_partial`.

Full parity target:

- owned stock is framework runtime state;
- stock capacity and active form rules are framework-owned;
- no `Combatant`/`Persona` stock list is needed by the clean consumer.

Clean console proof:

- Phase 5-27 result:
  - `--clean-training-annex-play` now hydrates owned stock actors beside the active/reserve party: an active-form Annex Mentor, a Persona-stock Bramble Runner, and Demon-stock Ashling/Ward Shell entries.
  - The session stores those references in `RuntimePartyStockSnapshot.ActiveForm`, `PersonaStock`, and `DemonStock`.
  - The `Inspect Stock` command presents that framework-owned snapshot without adding stock mutation commands.
  - Manual/suspend saves include and restore the active form and stock lists; corrupted saves that place enemy-team actors into party/stock lists are rejected before mutation.
  - This checkpoint remained `parallel_partial`: clean owned stock existed in the original clean host, but mutation operations were not part of Phase 5-27.
  - Phase 5-27 verification passed `74/74` focused Training Annex host tests and `845/845` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all four noninteractive clean demos pass, and `Data/Jsons` is unchanged.

### 28. `party_operations`

Current status: `parallel_partial`.

Full parity target:

- summon, return, swap, dismiss, replace, and consume operations use framework transitions;
- failed commands return diagnostics without mutation;
- presentation is host-owned.

Clean console proof:

- Phase 5-28 result:
  - `--clean-training-annex-play` now exposes `Party / Stock Operations` as a host-owned menu over the existing clean `RuntimePartyStockSnapshot`.
  - Operations execute through `PartyStockTransitionService` via `TrainingAnnexPartyController`: swap active form, summon Ashling, swap active demon to Ward Shell, return active demon, replace Ward Shell with Bramble Runner, dismiss Ashling, and consume Bramble Runner.
  - The host records before/after transition evidence for active party count, reserve count, active form, Persona stock count, Demon stock count, stable transition code, and affected runtime IDs.
  - Rejected operations return framework diagnostics and preserve the original snapshot; tests prove invalid return and duplicate summon do not mutate state.
  - This remains `parallel_partial`: original clean content can mutate party/stock state manually, but recruitment, fusion, battle COMP usage, and Godot presentation are still separate capability work.
  - Phase 5-28 verification passed `76/76` focused Training Annex host tests and `847/847` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all four noninteractive clean demos pass, and `Data/Jsons` is unchanged.

### 29. `negotiation_and_recruitment`

Current status: `parallel_partial`.

Full parity target:

- framework owns negotiation state, prompts, demand outcomes, recruitment validation, and result diagnostics;
- original clean negotiation content exists;
- clean host presents prompts without legacy `questions.json`.

Clean console proof:

- Phase 6-29 result:
  - `--clean-training-annex-play` now exposes `Negotiate / Recruit` as a host-owned menu over the clean Training Annex party/stock, wallet, and roster state.
  - `TrainingAnnexNegotiationController` drives the flow through `NegotiationSessionService`, `RecruitmentTransactionService`, `PartyStockTransitionService.AddDemonToStock`, and the bound economy service. The host owns prompts and presentation; framework services own session outcome, recruitment validation, stock mutation rules, and Macca spending.
  - The clean Training Annex negotiation content now has enough authored prompt material for a real success path. The success path recruits `bramble_runner` into Demon stock, spends Macca through the economy service, and records typed evidence for outcome, reason, mood score, wallet before/after, stock before/after, and transition codes.
  - Refusal preserves wallet and stock, and a repeated familiar encounter follows the familiar path without duplicate recruitment.
  - This remains `parallel_partial`: authored demand records exist in content, but the current `NegotiationSessionService` still calculates the Macca demand internally. Binding demand amount/type selection directly to authored clean content remains future framework work.
  - Phase 6-29 verification passed `104/104` focused Training Annex, party-stock, parity-ledger, and original-content tests, plus `851/851` full-suite tests with no skips. The framework build has `0` warnings, the complete solution retains `98` pre-existing legacy-host warnings, all four noninteractive clean demos pass, `git diff --check` passes, and the framework forbidden-reference search returns no matches. The only `Data/Jsons` change is the clean Training Annex negotiation sample; protected legacy/prototype data remains untouched.

### 30. `fusion_result_calculation`

Current status: `parallel_partial`.

Full parity target:

- framework resolves fusion results from approved original content and policies;
- no legacy `fusion_table.json` authority is required.

Clean console proof:

- Phase 7-30 result:
  - `CatalogFusionContentRepository` now adapts qualified `GameDataCatalog` definitions into the framework `IFusionContentRepository` contract. The clean resolver can consume original catalog recipes directly instead of relying on `fusion_table.json` or the legacy `LegacyFusionContentAdapter`.
  - `FusionRecipeSnapshot` can carry a structured `FusionRecipeResultSnapshot`, so catalog-authored operations such as `create_entity` and `rank_offset` reach `FusionResultResolver` without being flattened into legacy string tokens.
  - `FusionResultResolver` still supports the legacy token path for compatibility, but structured catalog results now resolve explicit entity results and race/rank-offset results. The Training Annex sample proves `ashling + bramble_runner -> ward_shell` and `echo_adept + bramble_runner -> ward_shell` through the original clean pack.
  - `--clean-training-annex-play` exposes `Calculate Fusion Results`, a non-mutating proof command that records typed result evidence and prints the resolved result. It does not select inherited skills, mutate stock, spend resources, confirm rituals, or touch Compendium state.
  - This remains `parallel_partial`: the protected legacy Cathedral flow is still active, and later Phase 7 work still owns committed transactions, fusion strategy approval, and Compendium parity.

### 31. `fusion_slots_mutation_accidents`

Current status: `parallel_partial`.

Full parity target:

- inheritance slots, mutation, and accidents are explicit framework policies;
- passive inheritance rules remain typed;
- mutation/accident features are included only if approved.

Clean console proof:

- Phase 7-31 result:
  - Training Annex skills now include generic mutation metadata for the `training_physical` family: `echo_strike` is tier 1 and `shell_bash` is tier 2. This is original example content, not legacy or private-IP data.
  - `Calculate Fusion Results` now also records `TrainingAnnexFusionPlanningEvidence`: result entity, natural skills, pickable inherited skills, display reason codes, ordinary slot count, sacrificial slot count, accident inherited skills, and mutation source/result IDs.
  - The proof uses `FusionPlanningService` over `CatalogFusionContentRepository`. It shows Ward Shell's base skills are already known, Frost Tip can pass through explicit allowance even though Ward Shell does not generally allow Ice, passive `Steady Breath` remains selectable, and ailment `Toxin Touch` is rejected by typed group policy.
  - A deterministic accident sample mutates `Echo Strike -> Shell Bash` through authored mutation tiers. No display name, description, legacy fusion table, or legacy Cathedral class determines that behavior.
  - This remains `parallel_partial`: no inherited-skill selection menu, stock mutation, ritual confirmation, accident application to a committed result, or Compendium update exists in the clean Training Annex path yet.
  - Verification: focused fusion tests passed `3/3`; parity/roadmap guard tests passed `6/6`; full suite passed `862/862` with no skips; framework build remained `0` warnings; solution build remained at `98` legacy warnings; clean battle, field, save, and Training Annex demos passed; framework forbidden-reference search returned no matches; protected legacy JSON stayed unchanged. The only `Data/Jsons` change is `training_annex_slice.skills.json`, adding original clean mutation metadata.

### 32. `fusion_preview_confirmation`

Current status: `parallel_partial`.

Full parity target:

- preview and final commit share the same framework planning result;
- selected inheritance is validated before commit;
- console only presents choices.

Clean console proof:

- Phase 7-32 result:
  - `--clean-training-annex-play` now has a `Preview Fusion Result` proof command.
  - The command builds a sacrificial clean catalog plan from Echo Adept, Bramble Runner, and Ashling through `CatalogFusionContentRepository`, `FusionResultResolver`, and `FusionPlanningService`.
  - The console host presents inherited-skill choices, but the framework remains the rule authority: selection is rechecked through `FusionInheritanceSelectionValidator` before any preview is built.
  - The current sample selects `Frost Tip`, `Echo Strike`, and passive `Steady Breath`; shows `Shell Bash` as `already_known`; and shows `Toxin Touch` plus `Ash Spark` as `group_not_allowed`.
  - `FusionPreviewService` creates the preview snapshot for Ward Shell with natural skills `Shell Bash` and `Soften Guard`, then the host asks for confirmation.
  - Confirmation is intentionally non-mutating. Runtime party/stock state, inventory, wallet, Compendium, and parent actors are not changed in this phase.
  - This remains `parallel_partial`: Phase 7-33 still owns atomic clean fusion transactions, parent consumption, result ownership, rollback, and any Compendium updates.
  - Verification: focused 7-32 tests passed `3/3`; parity/roadmap guard tests passed as part of the `13/13` focused guard run; full suite passed `864/864` with no skips; framework build remained `0` warnings; solution build remained at `98` legacy warnings; clean battle, field, save, and Training Annex demos passed; framework forbidden-reference search returned no matches; protected legacy JSON stayed unchanged. Direct redirected CLI smoke for `--clean-training-annex-play` still hits the known `ConsoleIO` cursor-handle limitation, so scripted interactive coverage remains test-owned through `ScriptedGameIO`.

### 33. `fusion_transactions`

Current status: `parallel_partial`.

Full parity target:

- parent consumption, result ownership, stock updates, and rollback are framework transaction decisions;
- no legacy `FusionMutator` is needed by the clean consumer.

Clean console proof:

- Phase 7-33 result:
  - `--clean-training-annex-play` now has a `Commit Fusion Transaction` proof command.
  - The command uses the direct clean catalog recipe `ashling + bramble_runner -> ward_shell`, validates inherited-skill selection with the same planner/selection validator used by preview, and assesses commit legality through `FusionTransactionService`.
  - The default Training Annex state already owns Ward Shell, so the transaction is rejected with a typed `DuplicateResult` diagnostic and no runtime state mutation. This is intentional coverage for pre-mutation rejection.
  - If the host first replaces owned Ward Shell with the prepared Bramble Runner candidate, the same command can commit atomically: Ashling and Bramble Runner are consumed through `PartyStockTransitionService`, a new `fusion_ward_shell_1` actor is hydrated through `CatalogBattleActorFactory`, and the resulting Demon stock contains that new Ward Shell reference.
  - The committed result uses the preview's natural and inherited skill IDs as a typed runtime skill snapshot; the actor is restored through the catalog actor factory so skill references are validated rather than copied as loose strings.
  - Training Annex save validation now includes dynamic fused actors in the roster, so the fused result does not become a dangling stock reference.
- This remains `parallel_partial`: clean Training Annex proves transaction ownership and rollback, but Compendium registration/recall integration and full fusion strategy approval remain Phase 7-34/7-35 work.
- Verification: focused 7-33 and guard tests passed `14/14`, focused transaction tests passed `3/3`, and the full suite passed `867/867` with no skips. Framework build remained `0` warnings, solution build remained at `98` pre-existing legacy-host warnings, clean battle/field/save/Training Annex demos passed, `git diff --check` reported only line-ending normalization warnings, framework forbidden-reference search returned no matches, and protected `Data/Jsons` content stayed unchanged.

CodeReview-7-3 transaction amendment:

- `FusionTransactionService.Prepare(...)` now consumes a validated inheritance token and the real party/stock snapshot; loose selected IDs and caller-authored ownership/capacity booleans are no longer accepted;
- preparation derives duplicate-result, identity, participant/entity, capacity, consumption, and placement decisions through an injected `IPartyStockTransitionService` and returns an immutable prepared token without constructing an actor;
- typed owner kind selects Demon or Persona consumption and placement;
- `Commit(...)` rejects stale party snapshots, constructs/restores the catalog result actor, and returns one typed result containing before/after state, actor/snapshot, consumed IDs, transition evidence, and diagnostics;
- actor-creation failure and transition rejection preserve the original authoritative party state;
- the verified preparation token also fixes result team/controller ownership and retained stat-boost state before confirmation, rejects duplicate participant identities and conflicting owned references, and preserves separate learned/equipped skills;
- rejected commits report no applied consumption or transitions; planned evidence remains explicitly available from the prepared token;
- Training Annex obtains `standard_stock_capacity` from the catalog ruleset binding and no longer creates a legacy capacity policy or executes stock transitions inside its fusion controller;
- the host remains responsible only for proposed identity, presentation, confirmation, and applying an `Applied` result;
- the capability remains `parallel_partial`; its Phase 7 review findings are closed and it no longer gates Phase 8.
- Verification after the post-interruption source audit: the broad fusion, stock, persistence, host, boundary, and roadmap gate passed `198/198`; the full suite passed `930/930` with no failures or skips. The framework build remained at `0` warnings, the solution retained `98` protected legacy-host warnings, all four clean demos passed, boundary and diff checks passed, and `Data/Jsons` remained unchanged.

### 34. `fusion_strategies`

Current status: `parallel_partial`.

Full parity target:

- standard/rank/sacrificial/stat-boost policies are approved framework concepts;
- unsupported legacy-inspired strategies remain absent or optional.

Clean console proof:

- Training Annex result calculation, inheritance planning, preview, and transaction paths explicitly register their accident, mutation, inheritance-slot, and sacrifice policies.

Phase 7-34 result:

- `FusionPolicyRegistry` is now a required dependency of clean fusion resolution and planning. It owns explicit accident, mutation, result-operation, combination, inheritance-slot, sacrifice, and legacy-token compatibility policies; the framework provides no hidden default registry.
- `FusionPolicyContext` carries optional host/session flags and numeric facts. A policy may use story progress, difficulty, a custom cycle, or another host fact without the framework knowing what that fact means. Fusion requests no longer require a Moon Phase integer.
- `FusionResultResolver` implements the neutral authored operations `create_entity` and `rank_offset`. Authored `stat_boost` and `special` operations require a registered result policy. Unstructured legacy result tokens require an explicitly supplied compatibility policy.
- `TieredFusionInheritanceSlotPolicy`, `FixedFusionSacrificePolicy`, percentage/contextual accident policies, adjacent-tier mutation, and typed catalyst stat boosts are reusable opt-in implementations. Hosts may replace or omit them.
- The framework no longer contains `mitama`, `element`-catalyst, Full Moon, catalyst-name, fixed `+2` sacrifice, fixed slot-table, or fixed 20% mutation assumptions. `FusionPreviewService` consumes typed policy-produced stat results instead of inspecting IDs or display names.
- `CatalogFusionContentRepository` preserves authored accident IDs, mutation IDs, result-policy IDs, and result parameters in runtime recipe snapshots. Missing policy registrations fail with typed diagnostics before gameplay falls back or mutates state.
- CodeReview-7-4 stores the immutable policy context on `FusionPlanningResult` and reuses it for every accident mutation. The standalone slot-count helper now exposes an explicit contextual overload while retaining its original context-free overload deliberately.
- `--clean-training-annex-play` explicitly selects a neutral sample policy set: 1% accident chance, 20% adjacent-tier mutation, the reviewed slot tiers, and an enabled two-slot sacrifice bonus. Its fusion evidence records the policy IDs and bonus used.
- The old Cathedral behavior remains available only through `LegacyFusionStrategyPolicies` in the console host. That adapter preserves the existing Moon Phase accident odds, legacy result tokens, catalyst stat boosts, rank handling, and external 20% mutation roll without teaching those concepts to `JRPG.Framework`.
- This remains `parallel_partial`: the clean original-content consumer is policy-driven and legacy calculations delegate through a compatibility policy, but the Cathedral's live transaction strategy classes remain active and Compendium work remains Phase 7-35. No removal is authorized.
- Verification: focused strategy-policy tests passed `9/9`, protected fusion compatibility tests passed `63/63`, all Training Annex host tests passed `86/86`, and the full suite passed `877/877` with no skips. The framework build remained at `0` warnings; the solution retained `98` pre-existing legacy-host warnings. Clean battle, field, save, and Training Annex demos passed. `git diff --check` passed with line-ending normalization notices only, framework forbidden-reference and legacy-fusion-token searches returned no matches, and `Data/Jsons` remained unchanged.
- CodeReview-7-4 verification: focused strategy/transaction tests passed `27/27`, the broad Phase 7 gate passed `200/200`, and the full suite passed `932/932` with no failures or skips. The framework build remained at `0` warnings, the solution retained `98` protected legacy-host warnings, all four clean demos passed, boundary and diff checks passed, and `Data/Jsons` remained unchanged.

### 35. `compendium`

Current status: `parallel_partial`.

Full parity target:

- registration, recall, pricing, snapshot isolation, persistence, and stock checks are framework-owned;
- clean content identifies species/entities explicitly.
- Compendium/ownership state can optionally seed player battle knowledge for familiar entities: recruited, fused, recalled, or previously registered entities may expose known elemental affinities, ailment resistances, and instant-death channels to the player's knowledge snapshot.

Clean console proof:

- select any owned Compendium-eligible Training Annex actor through a typed runtime identity, then add or update its immutable entry;
- recall a selected registered entity through catalog actor hydration, the selected Demon/Persona stock policy, and an atomic wallet transaction;
- persist and restore registered entries and dynamically recalled actors through the clean save path;
- import typed familiar-entity defenses into persistent player knowledge after clean recruitment, fusion, registration, or recall without importing them into encounter-local enemy AI knowledge.

Phase 7-35 result:

- `CompendiumRuntimeService` now owns clean actor registration and recall orchestration. Registration resolves the actor's qualified catalog entity, enforces `compendiumEligible`, and snapshots level, EXP, lifetime EXP, unspent stat points, integral base stats, learned skills, and equipped skills into immutable `CompendiumEntrySnapshot` data.
- Recall is one immutable transaction result. It checks entry existence, catalog eligibility, duplicate ownership by entity ID, destination stock capacity, and currency before returning changed party/wallet snapshots. Actor reconstruction uses `CatalogBattleActorFactory`, restores registered progression/stats/skills, resets transient battle/equipment state, and restores resources at their recalculated maxima.
- Recall placement is caller-selected through `CompendiumRecallStockKind`; the framework supports both Demon and Persona stock. `PartyStockTransitionService.AddPersonaToStock` closes the previous missing stock primitive.
- `FamiliarEntityKnowledgeService` is explicitly invoked rather than hidden. It imports all non-Almighty elemental affinities, catalog ailment resistances, and Light/Dark instant-death resistances from typed entity definitions. Missing defense entries resolve to the framework's typed Normal value. The caller chooses which familiar entity IDs count, so games may use Compendium registration, current ownership, or another approved policy.
- The Training Annex `Compendium` menu uses typed runtime/content selection IDs. Registering Ashling proves player knowledge can know its Ice weakness before a fresh attack; recruitment and committed fusion also import the newly owned entity, and recall reuses the same boundary. Encounter AI receives none of this persistent import.
- Compendium state, learned/equipped skill distinctions, unspent stat points, wallet/stock changes, and recalled catalog actors survive manual/suspend save round trips. Save validation rejects duplicate Compendium entity records, ineligible/missing entities, duplicate or missing skills, equipped skills that were not learned, negative stat values, and incomplete or unknown authored stat overrides.
- CodeReview-7-5 centralizes those invariants in a serializer-neutral framework validator. Registration and direct recall use the same checks before stock simulation, actor construction, pricing, or wallet mutation; host-owned JSON corruption tests cover the actual deserialize-and-validate path.
- This remains `parallel_partial`: the clean original-content path owns the complete Compendium transaction proof, but `CompendiumRegistry`, the Cathedral presentation, and legacy production consumers remain active and are not authorized for removal.
- Verification: the focused Phase 7-35, boundary, and protected-legacy gate passed `148/148`; the full suite passed `893/893` with no failures or skips. The framework build remained at `0` warnings, while the full solution retained `98` pre-existing legacy-host warnings. Clean battle, field, save, and Training Annex demos completed successfully; `git diff --check` passed with line-ending notices only; the framework forbidden-reference search returned no matches; and `Data/Jsons` remained unchanged.

CodeReview-7-2 integrity amendment:

- one centralized identity rule enumerates owner, active party, reserve party, active form, Persona stock, and Demon stock;
- Compendium recall rejects a proposed runtime ID already used in any of those locations before actor creation, stock placement, cost assessment, or wallet mutation;
- Demon/Persona stock additions and replacements reject cross-role runtime-ID reuse with a typed transition code;
- save validation rejects illegal cross-list identity reuse and party/stock references whose entity ID disagrees with the referenced actor snapshot;
- active party plus Demon-stock overlap for the same owned demon remains legal by explicit rule rather than by omission;
- the capability remains `parallel_partial`, but its required Phase 7 review findings are closed and Phase 8 may begin.
- CodeReview-7-5 verification: focused Compendium/persistence/host JSON tests passed `49/49`, the expanded Phase 7 gate passed `233/233`, and the full suite passed `937/937`. Framework build remained at `0` warnings, the solution retained `98` protected legacy-host warnings, all four clean demos passed, boundary and diff checks passed, and `Data/Jsons` remained unchanged.
- Verification: the focused identity, recall, stock, persistence, host, and ledger gate passed `166/166`; the full suite passed `915/915` with no failures or skips. The framework build remained at `0` warnings, the solution retained `98` protected legacy-host warnings, all four clean demos passed, boundary and diff checks passed, and `Data/Jsons` remained unchanged.

### 36. `console_presentation`

Current status: `parallel_partial`.

Full parity target:

- console presentation is a host over framework commands, events, diagnostics, and snapshots;
- it owns text, menus, colors, waits, and formatting only;
- it can be replaced by Godot presentation without replacing framework logic.

Clean console proof:

- every clean demo capability uses host presentation boundaries, not legacy rule logic.

## Completed Stabilization Checkpoints Before Phase 4

### CodeReview-1: Canonical Actor State (completed)

The first post-Phase-3 stabilization pass establishes one clean actor state authority:

- `RuntimeActorState` owns persistent and battle-time actor state.
- `CatalogBattleActor` supplies immutable definition/loadout metadata around that state.
- Growth, resources, field actions, battles, snapshots, and restore use the same object.
- Runtime actor/target references use `RuntimeInstanceId`, while content references remain `ContentId`.
- Save contract version `4` preserves the vital resource, exact duration modes, affinity overrides, analysis, capability IDs, passive enabled/disabled state, and passive activations.
- Current-only copy loops and the duplicate `RuntimeActorStateSet`/`BattleActorState` types are removed.
- Resource recalculation applies its policy result to that canonical actor before reporting success.

**Readiness:** ready. Final closure verification passed 810 tests with no failures or skips; the framework build has 0 warnings. The next work is the already-reviewed dynamic command identity and typed event metadata correction, not another actor-state pass.

This corrects architectural integrity; it does not by itself promote a protected legacy capability to `clean_parity` or authorize removal.

### CodeReview-2: Dynamic Commands And Typed Press Turn Events (completed)

- Host command options/results can carry a typed content ID or runtime-instance ID without changing existing coarse command enums.
- Training Annex target selection returns the selected participant, including the second actor in a multi-enemy formation.
- Battle skill menus are generated from executable actor definitions; battle item menus are generated from owned battle-usable catalog items.
- `BattleEncounterEvent.PressTurnState` carries icon counts, so hosts never parse presentation text to recover rule state.

**Readiness:** ready. Focused command/event coverage passed 67 tests and the full suite passed 814 tests with no failures or skips. This does not promote legacy battle consumers to `clean_parity`; it removes a correctness defect from the original clean consumer.

### CodeReview-3: Restore Hardening And Save Provenance (implemented)

- Save contract version `5` records exact content-pack IDs and versions through serializer-neutral `ContentPackIdentity` values.
- `GameDataCatalog` exposes the loaded content-pack identities, and `RuntimeSaveValidator` rejects missing, duplicate, unknown, or version-mismatched save provenance.
- `RuntimeSavePolicyService.AssessLoad` now validates both the current load context and the saved record's original creation context.
- Training Annex restore validates the expected actor instance, entity definition, actor kind, and team before constructing the restored session.
- Training Annex restore validates host-owned dungeon/navigation state against the locations, nodes, and checkpoints this clean host actually supports.
- Restore remains planned before mutation: the live session is updated only after framework validation and host compatibility checks all pass.
- Battle reward application now assesses wallet mutation before committing progression, so a failed currency transaction cannot leave EXP half-applied.

**Readiness:** ready. Focused persistence/Training Annex restore coverage passed 62 tests; focused restore/parity coverage passed 106 tests; the full suite passed 819 tests with no failures or skips; `JRPG.Framework` built with 0 warnings; the solution build retained 98 existing legacy console-host warnings; clean battle, field, save-v5, and Training Annex demos exited successfully. This hardens the clean save/load proof but still does not promote protected legacy save/load to `clean_parity` or authorize legacy removal.

### CodeReview-4: Clean Host Responsibility Split (implemented)

- `CleanTrainingAnnexPlayHost` remains the coordinator for boot, command flow, and session summary.
- Save/load policy checks, snapshot construction, save-slot access, restore planning, actor identity checks, and Training Annex save compatibility checks moved to `TrainingAnnexPersistenceController`.
- Navigation/dungeon transition presentation moved to `TrainingAnnexFieldPresenter`.
- Post-battle reward application and reward progress flags moved to `TrainingAnnexBattleRewardApplicator`.
- Focused seam tests prove persistence host-context flags, field/dungeon presentation messages, and reward wallet rejection without progression mutation.

**Readiness:** ready. Focused CodeReview-4/parity coverage passed 99 tests; the full suite passed 822 tests with no failures or skips; `JRPG.Framework` built with 0 warnings; the solution build retained 98 existing legacy console-host warnings; clean battle, field, save-v5, and Training Annex demos exited successfully. The split does not add gameplay behavior, does not promote legacy capabilities to `clean_parity`, and does not authorize legacy removal.

Do not start with all 36.

Resume with:

```text
Phase 4-23: economy
```

Scope:

- make clean wallet ownership, add/spend, and rollback behavior demonstrable in the Training Annex clean path;
- keep Macca mutation in framework economy transactions;
- preserve host-owned presentation and avoid legacy `EconomyManager` authority in the clean consumer;
- do not start shops, hospital, fusion, negotiation, or archive work in the same pass.

Why first:

- Phase 4-21 established trustworthy clean item quantities and selected-item consumption;
- Phase 4-22 established clean equipment ownership/equip state and equipped basic-attack impact;
- shops and hospital build naturally on trustworthy inventory, equipment, and wallet transaction models.

## Update Rules

When a pass completes:

1. update this document's capability section with actual evidence;
2. update `recovery-baseline.json` only if the capability status, evidence, ownership, or `futurePhase` truly changes;
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
