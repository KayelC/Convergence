# Framework Data Map

> **Status: Planning reference.** This is not game lore and not production content. It maps the neutral concept data the framework needs so the project can grow without using private IP or ATLUS-owned material.

## Purpose

The framework should not ship with someone's actual game setting. It should provide reusable concepts, schemas, validation, and example packs that prove behavior.

Use generic placeholder content for tests and demos. Downstream games should replace it with their own authored content.

## Data Categories Needed

| Family | What It Represents | Current Slice | Placeholder Direction |
| --- | --- | --- | --- |
| Packs and manifests | Pack identity, version, document list, dependencies | Training Annex has one pack | `convergence.framework_example` with small self-contained documents |
| Rulesets | Named policy choices for damage, rewards, growth, stats, Press Turn, stock, economy, and optional mechanics | Seven standard Training Annex rulesets; moon phase is absent because the sample does not use a moon mechanic; fusion recipes reference registered host policy IDs separately | `standard_damage`, `standard_reward`, `standard_growth`, etc. |
| Races / classifications | Broad entity families used by entities, fusion, negotiation, and presentation | `annex_spirit`, `annex_beast`, `annex_construct` | `sample_spirit`, `sample_beast`, `sample_construct` |
| Entities | Playable actors, enemies, recruitables, fusion-eligible templates | `echo_adept`, `ashling`, `bramble_runner`, `ward_shell`, `annex_mentor` | `sample_guardian`, `sample_wisp`, `sample_brute` |
| Skills | Active and passive behavior definitions | Damage, recovery, cure, ailment, buff/debuff, and passive examples | physical hit, fire spell, ice spell, heal, poison, buff, passive boost |
| Ailments / statuses | Status definitions, turn behavior, recovery, modifiers | `sample_poison`, `sample_sleep`, `sample_stun` | poison, sleep, stun as neutral examples |
| Items | Consumables, key items, materials, valuables | HP, SP, cure, revive, and key-item examples | tonic, antidote, revival item, escape item, key token |
| Equipment | Weapons, armor, boots, accessories, stat modifiers, basic attacks | `practice_blade`, `padded_jacket`, `light_steps`, `focus_charm` | training blade, padded vest, light boots, focus charm |
| Shops | Offers, prices, stock policies, availability | `training_supply` | supply kiosk selling tonic and training blade |
| Negotiation | Question sets, answers, demands, familiar dialogue | `steady_sample` | calm personality with two prompts, one demand outcome, and familiar dialogue |
| Encounters | Formations, enemy levels, reward policy hooks | `ashling_drill`, `mixed_drill`, `shell_check` | single enemy, two-enemy group, boss formation |
| Dungeons / fields | Blocks, floors, fixed floors, encounter pools, terminals, barriers | `training_annex` with random, safe, fixed battle, and barrier floors | three-floor training area with safe room and boss gate |
| Fusion recipes | Parent selectors, result operations, accidents/mutation hooks | Two Training Annex recipes execute for result calculation; mutation metadata now proves clean slot/planning and accident-mutation evidence | concept recipes using generic entities only |
| Compendium state | Runtime snapshots and recall rules | Framework services exist, content is thin | use generic species IDs and placeholder recall pricing |
| Rewards | EXP/Macca-like reward policies and parameters | Bound standard reward service | generic experience and currency values |
| Host registrations | Contexts, resources, stats, events, handlers, policies | Tests register explicit values | documented registration sets per demo |
| Save snapshots | Runtime actor, party, inventory, field, knowledge, session state | Clean save demo and Training Annex validate snapshots | host-owned save sample with generic IDs |

## Minimum Useful Example Pack

The next neutral example pack should stay small but cover more behavior than one basic fight.

Recommended contents:

- 1 manifest;
- 8 rulesets;
- 3 races;
- 5 entities;
- 10-14 skills;
- 3 ailments;
- 5 items;
- 4 equipment records;
- 1 shop;
- 1 negotiation personality;
- 3 encounters;
- 1 tiny dungeon;
- 2-3 fusion recipes.

This is enough to test battle, recovery, ailment, buff/debuff, item use, shop purchase, dungeon movement, rewards, and a minimal fusion concept without becoming a full game.

## Generic Placeholder Examples

### Races

- `sample_spirit`: magical neutral entity family.
- `sample_beast`: physical neutral entity family.
- `sample_construct`: defensive neutral entity family.

### Entities

- `sample_runner`: fast player-side test actor.
- `sample_wisp`: weak magical enemy.
- `sample_brute`: physical enemy.
- `sample_shell`: defensive enemy.
- `sample_mentor`: non-enemy test/support entity.

### Skills

- `training_strike`: physical damage.
- `spark_bolt`: electric damage.
- `frost_tip`: ice damage.
- `ember_note`: fire damage.
- `mend`: HP restore.
- `clear_toxin`: poison removal.
- `toxin_touch`: poison application.
- `focus_call`: attack buff.
- `soften_guard`: defense debuff.
- `steady_breath`: passive owner-turn-end recovery.
- `ice_focus`: passive ice damage modifier.
- `endure_once`: passive defeat interception.

### Ailments

- `sample_poison`: turn-end HP loss.
- `sample_sleep`: skip action and recover over time.
- `sample_stun`: skip or limited action.

### Items

- `minor_tonic`: HP restore.
- `cleanse_drop`: poison cure.
- `revival_pin`: revive.
- `exit_token`: host-requested dungeon exit.
- `training_key`: key item with no direct usage.

### Equipment

- `practice_blade`: weapon, physical basic attack.
- `padded_jacket`: armor, small defense.
- `light_steps`: boots, small evasion.
- `focus_charm`: accessory, small magic or luck modifier.

### Shop

- `training_supply`: sells `minor_tonic`, `cleanse_drop`, and `practice_blade`.

### Encounters

- `wisp_drill`: one `sample_wisp`.
- `mixed_drill`: one `sample_wisp` and one `sample_brute`.
- `shell_check`: one `sample_shell` as a sturdier encounter.

### Dungeon

- `training_annex_expanded`: small three-to-five-floor dungeon with one safe floor, one random encounter floor, and one fixed encounter floor.

### Fusion

Fusion should remain generic until the project owner decides the game's fusion identity.

Safe placeholder examples:

- `ashling` + `bramble_runner` -> `ward_shell` through an explicit `create_entity` recipe;
- `annex_spirit` + `annex_beast` -> rank-offset result in `annex_construct`;
- a recipe that proves passive inheritance remains separate from elemental active-skill inheritance.

Phase 7-30 proves the first two examples execute through `CatalogFusionContentRepository` and `FusionResultResolver` without legacy `fusion_table.json` authority. Passive inheritance and transaction flow remain later fusion passes.

Do not imitate ATLUS race charts, names, demons, inheritance labels, or fusion tables.

## What Should Not Be Added

- No ATLUS names, demons, races, spell names, or fusion tables.
- No private game lore unless deliberately approved.
- No direct conversion of legacy `Data/Jsons` into framework production content.
- No balance-heavy dataset before the rules are agreed.
- No placeholder content marked as shippable production data.

## Immediate Data Need

The framework now has a richer neutral example pack, not a giant production database.

Next, use that sample data more deeply:

1. route more sample skills through the clean runtime demo;
2. exercise ailment application and curing;
3. run the fixed `shell_check` encounter;
4. keep expanding shop/equipment/recovery facilities only when real gameplay needs more than the current Training Supply and Recovery Facility proofs;
5. prove the concept fusion recipes without deciding final fusion identity.

Phase 4-24 proves the first shop/equipment host flow: Training Supply offers are catalog-authored, resolved into runtime shop offers, bought/sold through bound economy services, and purchased equipment can be equipped immediately. Phase 4-25 adds the matching clean recovery proof: the Recovery Facility assesses treatment through the framework hospital service, spends Macca, restores resources, and leaves rejected rows non-mutating.

Keep every addition small enough for the owner to review and understand.
