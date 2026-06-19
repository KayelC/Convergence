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
| 02 | `actor_models` | `clean_foundation` | Prove clean runtime actors can represent the playable demo actor and enemies. |
| 03 | `resource_recalculation` | `parallel_partial` | Make clean HP/SP initialization and updates authoritative in the clean demo. |
| 04 | `stat_composition` | `parallel_partial` | Make clean stats and equipment/stat modifiers visible in the clean demo. |
| 05 | `growth_and_levels` | `parallel_partial` | Apply EXP and level/progression through framework services in the clean demo. |
| 06 | `moon_phase` | `legacy_only` | Decouple optional moon/session mechanics from clean runtime paths that do not use them. |
| 07 | `field_navigation` | `parallel_partial` | Let the clean demo move through a small field/dungeon loop. |
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
| 18 | `battle_knowledge` | `parallel_partial` | Persist and use clean battle knowledge in the demo session. |
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

Current status: `clean_foundation`.

Full parity target:

- clean runtime actor snapshots cover playable actors and enemies needed by the clean loop;
- actor kind, identity, level, stats, resources, skills, defenses, and progression are framework-owned;
- no live `Combatant` or `Persona` is needed by the clean consumer.

Clean console proof:

- Training Annex playable actor and enemies are hydrated only from `GameDataCatalog`.

### 03. `stat_composition`

Current status: `parallel_partial`.

Full parity target:

- framework stat policy owns base/effective stats;
- equipment/stat modifiers flow through clean runtime state;
- clean host displays effective stats without legacy `StatProcessor`.

Clean console proof:

- inspect/status menu shows clean stat values from runtime snapshots.

### 04. `growth_and_levels`

Current status: `parallel_partial`.

Full parity target:

- framework EXP curve and level-up policy own progression;
- random growth, stat allocation, and level-up resource changes are host-random-source friendly;
- clean loop applies EXP after battle.

Clean console proof:

- victory changes EXP/progression in the clean session.

### 05. `resource_recalculation`

Current status: `parallel_partial`.

Full parity target:

- framework owns HP/SP max calculation and current-resource policy;
- actor hydration and level-up/resource changes use the same policy;
- no legacy `GrowthProcessor` path is needed for clean actors.

Clean console proof:

- item use, battle damage, recovery, and reward/level flow leave valid resource snapshots.

### 06. `active_and_reserve_party`

Current status: `parallel_partial`.

Full parity target:

- party membership is represented by framework party snapshots;
- active/reserve changes use framework transition results;
- clean consumer does not mutate legacy party lists.

Clean console proof:

- only needed once the clean demo has more than one party actor.

### 07. `persona_and_demon_stock`

Current status: `parallel_partial`.

Full parity target:

- owned stock is framework runtime state;
- stock capacity and active form rules are framework-owned;
- no `Combatant`/`Persona` stock list is needed by the clean consumer.

Clean console proof:

- only needed if recruitment/fusion/summon features enter the clean demo.

### 08. `party_operations`

Current status: `parallel_partial`.

Full parity target:

- summon, return, swap, dismiss, replace, and consume operations use framework transitions;
- failed commands return diagnostics without mutation;
- presentation is host-owned.

Clean console proof:

- only needed after clean stock exists.

### 09. `persistence_snapshots`

Current status: `clean_foundation`.

Full parity target:

- framework snapshot and validator cover the clean session;
- save policy defines manual/autosave/suspend behavior;
- host owns actual storage and serialization;
- loading restores equivalent framework runtime state.

Clean console proof:

- clean demo can save, load, and optionally consume a suspend save.

### 10. `combat_math`

Current status: `parallel_partial`.

Full parity target:

- clean battle uses framework combat rules directly;
- numeric policies are ruleset-bound where approved;
- no legacy `CombatMath` or `DamageHandler` is required by clean battle.

Clean console proof:

- Training Annex battle damage/accuracy/critical/reward values come from framework services.

### 11. `press_turn`

Current status: `parallel_partial`.

Full parity target:

- clean battle loop consumes typed action outcomes through framework Press Turn rules;
- host only presents the current turn state;
- legacy battle conductor is not in the clean battle path.

Clean console proof:

- player commands visibly affect Press Turn state in the clean battle loop.

### 12. `battle_actions`

Current status: `parallel_partial`.

Full parity target:

- attack, skill, item, guard, pass, analyze, swap, and escape commands are framework action commands;
- assessment and execution cannot disagree;
- clean consumer does not call legacy `ActionProcessor`.

Clean console proof:

- clean battle command menu uses `BattleActionExecutor` or framework encounter command ports.

### 13. `typed_effects`

Current status: `parallel_partial`.

Full parity target:

- all effects used by clean content are typed definitions;
- no effect behavior is inferred from display names or legacy strings;
- item and skill effects share the framework effect pipeline.

Clean console proof:

- demo skills/items use typed effects only.

### 14. `ailment_lifecycle`

Current status: `parallel_partial`.

Full parity target:

- clean ailment definitions, application, ticking, recovery, and exclusivity are framework-owned;
- content declares ailment behavior explicitly;
- clean battle lifecycle uses framework service directly.

Clean console proof:

- optional first ailment sample can apply, tick, recover, and display outcome.

### 15. `passive_lifecycle`

Current status: `parallel_partial`.

Full parity target:

- passives trigger from framework lifecycle events;
- rule modifiers apply without skill-name checks;
- activation limits and cleanup are framework-owned.

Clean console proof:

- Training Annex passive recovery or modifier runs through clean lifecycle.

### 16. `enemy_ai_and_tactics`

Current status: `parallel_partial`.

Full parity target:

- clean enemy choice policy is framework-owned or host-injected through framework contracts;
- tactics/direct control are typed commands, not console-only branches;
- ailment forced actions share lifecycle outcomes.

Clean console proof:

- demo enemy chooses deterministic legal actions without legacy `BehaviorEngine`.

### 17. `battle_knowledge`

Current status: `parallel_partial`.

Full parity target:

- elemental, ailment, and instant-death knowledge live in framework snapshots;
- analyze/discovery updates are clean runtime events;
- persistence includes knowledge state.

Clean console proof:

- clean battle discovers and reuses a known weakness/resistance.

### 18. `negotiation_and_recruitment`

Current status: `parallel_partial`.

Full parity target:

- framework owns negotiation state, prompts, demand outcomes, recruitment validation, and result diagnostics;
- original clean negotiation content exists;
- clean host presents prompts without legacy `questions.json`.

Clean console proof:

- only after owner confirms negotiation is part of the clean framework sample.

### 19. `battle_rewards`

Current status: `parallel_partial`.

Full parity target:

- framework reward services calculate EXP/Macca or equivalent reward outputs;
- ruleset/content binding is explicit;
- clean loop applies rewards to runtime session state.

Clean console proof:

- Training Annex victory applies nonzero rewards and records session progress.

### 20. `inventory_quantities`

Current status: `parallel_partial`.

Full parity target:

- framework inventory snapshots and transactions own quantities, reservation, commit, rollback, and stack limits;
- clean host never mutates legacy `InventoryManager`.

Clean console proof:

- clean item use consumes inventory only on meaningful success.

### 21. `equipment_ownership`

Current status: `parallel_partial`.

Full parity target:

- clean equipment definitions, ownership, equip/unequip, and stat/basic-attack impact are framework-owned;
- clean content provides original equipment.

Clean console proof:

- player buys or equips sample equipment and sees stat/action impact.

### 22. `economy`

Current status: `parallel_partial`.

Full parity target:

- framework wallet/economy transactions own add/spend/rollback;
- pricing policy is ruleset-bound where approved.

Clean console proof:

- clean shop or reward loop changes wallet state.

### 23. `shops`

Current status: `parallel_partial`.

Full parity target:

- clean shop catalogs, offer lists, pricing, stock policy, and transaction diagnostics are framework-owned;
- console only presents buy/sell choices.

Clean console proof:

- Training Annex shop sells at least one clean item/equipment record.

### 24. `hospital`

Current status: `parallel_partial`.

Full parity target:

- restoration costs, payment, HP/SP restoration, ailment removal, and failure diagnostics are framework-owned;
- host presentation is replaceable.

Clean console proof:

- optional clean recovery facility demonstrates restoration if still desired.

### 25. `field_items_and_skills`

Current status: `parallel_partial`.

Full parity target:

- field item and skill usage use framework execution environments;
- battle-only conditions evaluate correctly outside battle;
- inventory consumption is transaction-safe.

Clean console proof:

- clean field item/skill works in the Training Annex loop.

### 26. `field_navigation`

Current status: `parallel_partial`.

Full parity target:

- clean field/session navigation is framework state plus host commands;
- legacy `FieldConductor` is not part of the clean demo path.

Clean console proof:

- player navigates clean Training Annex menus over framework state.

### 27. `dungeon_traversal`

Current status: `parallel_partial`.

Full parity target:

- dungeon progress, floor/room transitions, terminals, barriers, exits, and boss flags are framework-owned;
- host scene/trigger state chooses when to ask for an encounter.

Clean console proof:

- player enters, moves, returns, and triggers encounter from clean dungeon state.

### 28. `encounters`

Current status: `parallel_partial`.

Full parity target:

- encounter definitions and formations hydrate runtime actors through framework services;
- host-owned triggers can select encounters without relying on forced floor battles;
- random encounters remain optional policy, not mandatory exploration design.

Clean console proof:

- host trigger starts `ashling_drill` or another original clean encounter.

### 29. `moon_phase`

Current status: `legacy_only`.

Full parity target:

- moon/cycle data is optional host/session metadata;
- content that uses moon conditions declares that dependency;
- unrelated systems run without fake moon phase values;
- sacrificial fusion gates become policy-owned instead of hardcoded Full Moon assumptions.

Clean console proof:

- clean Training Annex loop runs without moon phase when no moon mechanic is used.

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
