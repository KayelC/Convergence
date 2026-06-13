# Field Subsystem

> **Status: Current implementation reference.** This chapter describes the current console field and dungeon flows.

## Purpose

`Logic/Field` implements non-combat gameplay: city menus, dungeon traversal, shops, hospital restoration, inventory usage, field skill usage, status menus, equipment, stat allocation, party organization, and entry into fusion.

## Key Classes And Responsibilities

- `FieldConductor`: root field-loop orchestrator.
- `FieldServiceEngine`: field-side rules for shops, equipment, restoration, items, skills, stat allocation, persona swaps, terminals, and boss defeat registration.
- `ExplorationProcessor`: floor movement, warp execution, floor-entry triggers, terminal unlocks, and encounter preparation.
- `DungeonManager`: dungeon state interpretation, fixed floor processing, random encounters, terminals, and boss state checks.
- `DungeonState`: persistent dungeon progress.
- `ShopEngine`: purchase/sale pricing and inventory/economy mutations.
- Field bridges: `ServiceUIBridge`, `DungeonUIBridge`, `InventoryUIBridge`, `StatusUIBridge`, and `ShopUIBridge`.
- Field messaging: `FieldMessenger`, `FieldLogger`, and `FieldMessageArgs`.
- `FieldUIState`: shared UI state for menus/bridges.

## Main Runtime Flows

### Main Field Loop

`FieldConductor.NavigateMenus` repeatedly asks `ServiceUIBridge` for the top-level choice:

- Explore Tartarus.
- City Services.
- Inventory.
- Status.
- Organize Party.
- Exit Game.

The conductor routes choices to private workflow methods and exits if the player dies or chooses exit.

### Dungeon Entry And Traversal

Dungeon entry checks unlocked terminals. If only floor 1 is available, it warps directly to the entrance; otherwise the player selects an entry point.

During exploration:

1. `DungeonManager.ProcessCurrentFloor` produces a `DungeonFloorResult`.
2. `DungeonUIBridge` shows available floor actions.
3. `ExplorationProcessor` handles ascension, descension, or warp.
4. `ProcessFloorEntry` unlocks terminals and identifies safe rooms, battles, bosses, and block ends.
5. Encounters are prepared by hydrating enemy IDs through `CombatantFactory`.

### City Services And Shops

`FieldServiceEngine.OpenShop` delegates to `ShopUIBridge` and `ShopEngine`.

- Buying checks Macca, applies Luck-based discounts, and adds item/equipment ownership.
- Selling removes inventory/equipment and grants Luck-scaled Macca.
- Equipment metadata can be repaired from shop entries before display or possession.

### Hospital, Items, And Skills

Hospital restoration costs 1 Macca per missing HP and 5 Macca per missing SP. Treatment fully restores HP/SP, removes ailments, and clears encounter-persistent battle state.

Field item/skill usage uses explicit effect gates:

- Redundant heals/cures are blocked.
- `Goho-M` resets dungeon state to the entry and returns `RequestDungeonExit`.
- Field skills spend SP only when the effect applies.

### Status, Equipment, And Party Organization

Status bridges render character/persona/demon details, stat allocation, equipment slots, persona stock, demon stock, and organization menus. `FieldServiceEngine` mutates stats/equipment/persona swaps while `PartyManager` mutates active party and demon deployment.

## Important State And Invariants

- `DungeonState.UnlockedTerminals` starts with floor 1.
- `DungeonState.DefeatedBosses` prevents defeated fixed-floor bosses from respawning.
- `DungeonManager` treats floor 1 as a safe lobby with terminal.
- Random encounters currently produce 1 to 3 enemies from the current block pool.
- `FieldServiceEngine` owns field-side resource mutation, not UI bridges.
- `FieldConductor` creates the `FusionConductor`, sharing party, economy, UI state, and compendium.

## Data Dependencies

- `Database.Dungeons` and `tartarus.json` drive floor ranges, fixed floors, terminals, bosses, and enemy pools.
- `Database.ShopInventory` drives shop offerings and metadata fallback.
- `Database.Items`, equipment dictionaries, and skill data drive inventory, equipment, and field skill behavior.
- Enemy IDs from dungeon data must resolve through `CombatantFactory`.

## Extension Points

- Add new city services in `ServiceUIBridge`, then handle them in `FieldConductor.OpenCityMenu` or `FieldServiceEngine`.
- Add new dungeon floor types by updating `DungeonData`, `DungeonManager`, `ExplorationProcessor`, and `DungeonUIBridge`.
- Add new field-use item behavior in `FieldServiceEngine.ExecuteItemUsage`.
- Add new field skill behavior in `ExecuteSkillUsage` or shared status/effect helpers.
- Add new shop categories only after extending `ShopCategory`, inventory storage, shop UI, and equipment/data loading.

## Caveats

- Field and battle both use `StatusRegistry` behavior; changing status semantics can affect both contexts.
- Shop/equipment metadata repair is a defensive workaround for incomplete loaded metadata.
- Some bridge methods return nullable selections while signatures are non-nullable, producing build warnings.
