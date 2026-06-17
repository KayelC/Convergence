# Field Subsystem

> **Status: Current implementation reference.** This chapter describes the current console field and dungeon flows.

## Purpose

`Logic/Field` implements non-combat gameplay: city menus, dungeon traversal, shops, hospital restoration, inventory usage, field skill usage, status menus, equipment, stat allocation, party organization, and entry into fusion.

## Key Classes And Responsibilities

- `FieldConductor`: root field-loop orchestrator.
- `FieldServiceEngine`: field-side rules for shops, equipment, restoration, items, skills, stat allocation, persona swaps, terminals, and boss defeat registration.
- `ExplorationProcessor`: floor movement messages, warp execution, floor-entry presentation, battle handoff, and encounter preparation.
- `DungeonManager`: console compatibility facade over framework dungeon state interpretation, fixed floor processing, random encounters, terminals, and boss state checks.
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

Dungeon entry checks unlocked terminals. If only floor 1 is available, it warps directly to the entrance; otherwise the player selects an entry point. Track M keeps that menu flow, but routes entry, movement, terminal returns, explicit dungeon exit, and boss defeat through framework transition results.

Track O9 adds typed console-host presentation results over those transition results. `DungeonManager` exposes detailed transition records, `DungeonUIBridge` maps runtime events to `Shown`, `Suppressed`, or selection results, and `FieldConductor` consumes those records for movement, terminal warp, Goho-M/explicit exits, barriers, floor entry, and boss-defeat registration.

During exploration:

1. `DungeonManager.ProcessCurrentFloorDetailed` adapts a framework floor snapshot into a legacy `DungeonFloorResult` plus ordered mapped presentation events.
2. `DungeonUIBridge` shows available floor actions and returns typed selected/back/unavailable results before legacy wrappers translate them for older callers.
3. `ExplorationProcessor` handles ascension, descension, or warp and publishes only the existing movement/warp-visible messages.
4. `ProcessFloorEntryDetailed` unlocks terminals and identifies safe rooms, battles, bosses, and block ends while suppressing structural runtime events that do not have a legacy visible message.
5. Encounters are prepared by hydrating enemy IDs through `CombatantFactory`.

### City Services And Shops

`FieldServiceEngine.OpenShop` delegates to `ShopUIBridge` and `ShopEngine`. Track L keeps that console flow, but `ShopEngine` now delegates buy/sell assessment and mutation to framework resource-management services through `LegacyInventoryResourceAdapter`.

Track O8 keeps the same visible shop flow while adding typed console-host presentation results for shop command selection, buy/sell offers, confirmation, inspection, transaction success, and transaction failure. The UI still reads legacy `Database.ShopInventory` and equipment/item metadata, but mutation and failure decisions are presented from framework-backed transaction results.

- Buying checks Macca, applies Luck-based discounts, and adds item/equipment ownership.
- Selling removes inventory/equipment and grants Luck-scaled Macca.
- Duplicate equipment purchases, insufficient Macca, unavailable stock, and equipped-item sales are rejected before mutation.
- Item/equipment inventory and Macca changes are applied atomically from immutable before/after snapshots.
- Equipment metadata can be repaired from shop entries before display or possession.

### Hospital, Items, And Skills

Hospital restoration costs 1 Macca per missing HP and 5 Macca per missing SP. Treatment fully restores HP/SP, removes ailments, and clears encounter-persistent battle state.

Track L routes the hospital cost/payment/restoration decision through the framework. The engine still permits ailment-only treatment at zero cost, while the current hospital UI continues to mark full HP/SP patients as healthy to preserve visible menu behavior until the field state machine is migrated.

Track O8 adds typed hospital patient-selection and treatment-presentation results. `FieldConductor` now publishes success/failure from those results, preserving the existing “fully restored” and “could not complete treatment” messages.

Field item/skill usage now routes through typed console-host field selection and execution results before adapting back to the legacy conductor signals. The legacy item and skill data, effect-string parsing, and visible messages remain unchanged.

- Redundant heals/cures are blocked.
- `Goho-M` resets dungeon state to the entry and returns `RequestDungeonExit`.
- Field skills spend SP only when the effect applies.
- Field item consumption now uses the same framework-backed inventory transaction adapter after a meaningful legacy item effect succeeds.

### Status, Equipment, And Party Organization

Status bridges render character/persona/demon details, stat allocation, equipment slots, persona stock, demon stock, and organization menus. Track O4 gives Persona stock, demon stock, organization slots, and summon/replace menus typed selected/back/unavailable results while keeping the existing wrapper methods for current callers.

`FieldServiceEngine` mutates stats/equipment/persona swaps while `PartyManager` mutates active party and demon deployment. Field-side Persona swaps, demon summons, demon returns, and active demon swaps now expose typed presentation results carrying the Track F transition code, affected runtime IDs, and ordered messages; the underlying live objects, stock overlap, HP/SP capping, and menu text remain unchanged.

## Important State And Invariants

- `DungeonState.UnlockedTerminals` starts with floor 1.
- `DungeonState.DefeatedBosses` prevents defeated fixed-floor bosses from respawning.
- `RuntimeFieldDungeonService` treats floor 1 as a safe lobby with terminal; `DungeonManager` exposes the same legacy result.
- Random encounters currently produce 1 to 3 enemies from the current block pool.
- Framework dungeon events such as floor entry, terminal unlock, encounter request, dungeon exit, and action rejection are recorded for presentation tests but not printed unless they replace an existing legacy message.
- Visible dungeon traversal messages preserved by O9 are `Ascending...`, `Descending...`, `The air here is calm.`, `!!! POWERFUL SHADOW DETECTED !!!`, `The path is sealed.`, and `The Guardian has been defeated!`.
- `FieldServiceEngine` owns field-side resource mutation, not UI bridges.
- `FieldConductor` creates the `FusionConductor`, sharing party, economy, UI state, and compendium.

## Data Dependencies

- `Database.Dungeons` and `tartarus.json` drive floor ranges, fixed floors, terminals, bosses, and enemy pools.
- `Database.ShopInventory` drives shop offerings and metadata fallback.
- `Database.Items`, equipment dictionaries, and skill data drive inventory, equipment, and field skill behavior.
- Enemy IDs from dungeon data must resolve through `CombatantFactory`.

## Extension Points

- Add new city services in `ServiceUIBridge`, then handle them in `FieldConductor.OpenCityMenu` or `FieldServiceEngine`.
- Add new dungeon floor types by updating `DungeonData`, the legacy content adapter, framework floor-kind handling, and `DungeonUIBridge`.
- Add new field-use item behavior in `FieldServiceEngine.ExecuteItemUsage`.
- Add new field skill behavior in `ExecuteSkillUsage` or shared status/effect helpers.
- Add new shop categories only after extending `ShopCategory`, inventory storage, shop UI, and equipment/data loading.

## Caveats

- Field and battle both use `StatusRegistry` behavior; changing status semantics can affect both contexts.
- Shop/equipment metadata repair is a defensive workaround for incomplete loaded metadata.
- Some bridge methods return nullable selections while signatures are non-nullable, producing build warnings.
