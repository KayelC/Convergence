# Data Subsystem

## Purpose

`Data` defines the JSON-backed content model and the static runtime registry. It is the bridge between editable game content and live gameplay objects.

## Key Classes And Responsibilities

- `Database`: loads every JSON file and exposes static dictionaries/lists for gameplay systems.
- `SkillData`: skill cost, power, category, family/rank, exclusivity, and parsing helpers.
- `PersonaData`: entity template for personas and demons; converts templates into live `Persona` instances.
- `AilmentData`: status ailment rules such as restrictions, multipliers, cure keywords, and removal triggers.
- `ItemData`: consumable item metadata and effect value.
- `WeaponData`, `ArmorData`, `BootData`, `AccessoryData`: equipment metadata and combat modifiers.
- `ShopData`: shop entries, shop JSON roots, and category typing.
- `DungeonData`: dungeon, block, fixed floor, and encounter pool data.
- `NegotiationData`: personality-driven questions, answers, and familiar dialogue data.

## Main Runtime Flows

1. `Program.cs` calls `Database.LoadData(io)` before gameplay begins.
2. `Database` reads files from `AppDomain.CurrentDomain.BaseDirectory/Data/Jsons`.
3. JSON is deserialized with `Newtonsoft.Json`.
4. Skills, entities, ailments, items, equipment, shops, fusion recipes, negotiation questions, and dungeons are placed into static registries.
5. Factories and engines read those registries during battle, field, shop, dungeon, and fusion flows.

## Important State And Invariants

- Persona/entity IDs are normalized to lowercase during `entity_database.json` loading.
- `Database.Personas` is used for both personas and demons.
- Equipment loading uses reflection to read an `Id` property from generic DTOs.
- Shop inventory acts as both a storefront and metadata fallback for equipment/item display.
- `LoadData` must run before any code calls `CombatantFactory`, `DungeonManager`, `ShopEngine`, battle effect registries that inspect skills, or fusion calculators.

## JSON Dependencies

Current JSON files:

- `skills_database.json`
- `entity_database.json`
- `status_ailments.json`
- `fusion_table.json`
- `questions.json`
- `items.json`
- `weapons.json`
- `armor.json`
- `boots.json`
- `accessories.json`
- `shop_inventory.json`
- `tartarus.json`

The project file marks `Data/Jsons/*.json` as content copied to the output directory.

## Extension Points

- Add a skill by editing `skills_database.json`; if its category is new, register a battle effect strategy.
- Add a demon/persona by editing `entity_database.json`; fusion, dungeon encounters, compendium, and factory creation all consume these templates.
- Add equipment or shop goods by updating both equipment JSON and `shop_inventory.json` when player-facing shop metadata is needed.
- Add dungeon content through `tartarus.json` blocks, enemy pools, fixed floors, terminals, and boss IDs.
- Add negotiation content through `questions.json`, aligned with `PersonalityType`.

## Caveats

- Many DTO properties are non-nullable but populated by JSON, producing nullable build warnings.
- `Database` is global mutable state. Re-running loads without clearing every registry could accumulate or preserve prior values depending on the collection.
- Missing JSON files are reported to `IGameIO`, but loading continues; downstream systems may then see empty registries.
- String IDs and display names are both used in places. Prefer lowercase IDs for lookup and names for UI only.
