# Inventory, Equipment, And Economy Order 7 Fresh Closure Review

**Date:** 14 August 2026  
**Reviewed commit:** `49db0396` (`test: retain order 7 closure evidence`)  
**Review status:** **Order 7 is not ready for formal closure.**  
**Runtime findings:** 1 high, 2 medium  
**Documentation findings:** 1 low

## Review Method

This review began from the active source tree, exported public API, active
content and schemas, executable tests, DemoHost/Godot integration code, and
current three-audience documentation. Earlier Order 7 reports and completion
summaries were not used as proof of correctness.

The source trace covered:

- immutable inventory, equipment-instance, currency, and durable-stock state;
- live item/equipment, equip/unequip, currency, shop, and recovery transitions;
- equipment profile resolution, actor composition, action authorization, and
  combat Defense/Evasion consumption;
- catalog ruleset binding and all four Order 7 policy extension boundaries;
- save validation, aggregate restore, DemoHost adoption, and Godot-owned save
  serialization; and
- mechanics, developer, technical, public-API, architecture, and roadmap
  documentation.

Findings below identify an intended invariant, a reachable path, the concrete
consequence, and the correction required. Host misuse that the documented
contract already rejects, impossible domain values, and unimplemented product
alternatives are not presented as vulnerabilities.

## Findings

### H1. Public loadout mutation can bypass the canonical equipment authority

**Intended invariant:** one inventory-owned equipment instance may be equipped
by at most one actor, and a loadout change must update the actor's loadout,
derived skills/passives, Defense/Evasion contributions, resources, and combat
profile as one accepted state.

**Reachable paths:**

1. [`RuntimeActorState.ReplaceEquipment`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs#L456)
   is exported in `PublicAPI.Shipped.txt`. It replaces the loadout and removes
   equipment-granted passives, but it does not re-resolve equipment,
   recompose effective stats/resources, or inspect another actor's loadout.
   Calling it after armor is equipped can therefore leave the old armor
   Defense/Evasion in `Stats` after the loadout is empty.
2. [`RuntimeActorEquipmentApplicationService.Apply`](../../src/Convergence.Framework/Runtime/RuntimeActorCombatProfileComposition.cs#L240)
   accepts a raw candidate loadout. Although its request carries
   `RuntimeActors`, the service uses those actors only during stat-source
   composition. It does not reject an equipment instance already referenced by
   another actor before committing the staged actor at line 327. A host can
   therefore call the documented live commit boundary directly and assign one
   instance to two actors.

The normal DemoHost path first calls `EquipmentTransitionService.Equip`, so it
does not trigger the second route. That does not make the exported bypass safe
for a Godot or third-party host. Aggregate save validation eventually detects
the duplicate assignment, but only after invalid live state has already been
committed.

**Consequence:** combat can use stale equipment-derived stats, or the live
session can contain a multiply equipped instance that cannot be saved or
restored. This directly violates the fixed ownership rule and the documented
claim that `RuntimeActorEquipmentApplicationService` is the live actor commit
boundary.

**Required correction:** make raw equipment replacement an internal staging
operation, route DemoHost initialization and tests through the canonical
application path, and make that application path prove complete cross-actor
assignment uniqueness before committing. The proof may be complete current
actor/loadout evidence or a typed validated transition authority, but it must
not rely on callers remembering an undocumented prerequisite.

Required regressions:

- direct public loadout replacement is no longer available;
- unequipping armor removes its Defense/Evasion in the same commit;
- application rejects another actor's equipped instance with unchanged actor
  state; and
- actor creation, live change, battle authorization, and restore still derive
  one identical profile.

### M1. Live equipment acquisition can create inventory that only save validation rejects

**Intended invariant:** accepted live equipment acquisition creates a catalog-
and-layout-compatible inventory instance whose runtime ID is globally distinct
from actor IDs.

**Reachable path:**
[`InventoryTransitionService.AddEquipment`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L585)
checks only whether the supplied slot ID is syntactically valid and whether the
equipment instance ID already exists in inventory. It has no catalog, selected
slot-layout policy, or current actor-ID evidence. It therefore returns
`Applied` when:

- an equipment definition is stored under an incompatible authored slot;
- the definition is absent from the catalog; or
- the equipment instance ID equals a live actor runtime ID.

The first two shapes reject later during profile resolution/save validation,
and the collision rejects at
[`RuntimeSaveValidator.ValidateInventory`](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs#L1415).
The accepted live inventory is nevertheless unusable or unsaveable in the
intervening session. This is the equipment analogue of the invalid live item-ID
path already closed by the current `InvalidItemId` transition result.

The developer guide correctly tells hosts to allocate globally fresh IDs, but
it also states that Framework transition and restore boundaries reject
actor/equipment collisions. The current transition API cannot make that
promise with its inputs.

**Consequence:** a reward, drop, scripted grant, or custom shop path can report
successful acquisition and only fail when the actor tries to equip the item or
the player saves. That moves a content/identity error away from the command
that introduced it and can strand a live session.

**Required correction:** add one catalog-, layout-, and session-ID-aware live
equipment acquisition boundary and use it for public host integrations. Keep
permissive decoded snapshots available for aggregate diagnostics, but do not
accept the same malformed shape as a live command. If the low-level inventory
transition remains public, its narrower guarantees must be named and
documented without claiming global validation.

Required regressions:

- missing definitions, slot/profile mismatch, and actor-ID collision reject
  with distinct typed diagnostics and unchanged inventory;
- two instances of one valid definition remain legal; and
- shop purchase, scripted acquisition, DemoHost, and restore use the same
  validated acquisition contract.

### M2. Pricing and stock factory results do not uphold the typed fault boundary

**Intended invariant:** malformed host policy factories fail through typed
diagnostics; non-cancellation faults do not escape into offer resolution.

**Reachable path:**
[`ShopPricingPolicyBindingResult`](../../src/Convergence.Framework/Runtime/ShopPricingPolicies.cs#L199)
and
[`ShopStockPolicyBindingResult`](../../src/Convergence.Framework/Runtime/ShopStockPolicies.cs#L172)
copy factory diagnostics without rejecting null entries, undefined diagnostic
codes, blank messages, or contradictory policy-plus-diagnostic results. Their
registries return a failed result when its diagnostic collection is nonempty.
The offer resolver then dereferences every diagnostic at
[`ResolvePrice`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L1335)
and [`BindStock`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L1398).

A registered host factory can therefore return a failed binding containing a
null diagnostic. The registry accepts it, and offer resolution throws
`NullReferenceException` instead of returning the promised typed policy
diagnostic. The recovery factory boundary already enforces the correct
either-policy-or-diagnostics shape in `RecoveryPolicyBindingResult`.

**Consequence:** one malformed third-party pricing or stock factory can crash
catalog-backed economy binding or shop-offer resolution. This is a robustness
defect at an advertised extension boundary, not a player-input security
vulnerability.

**Required correction:** give pricing and stock binding results the same
validated, immutable either/or contract used by recovery. Preserve
`OperationCanceledException`; normalize all other factory/result-shape failures
to their existing `PolicyFactoryFailure` diagnostics.

Required regressions cover null diagnostics, undefined codes, blank messages,
policy-plus-diagnostic, no-policy/no-diagnostic, cancellation, and valid custom
factories for both pricing and stock.

### L1. The active restoration guide still declares save contract v15

[`runtime-actor-state-and-restoration.md`](../technical/runtime-actor-state-and-restoration.md#L303)
labels the active section `Save Contract V15` and states that
`CurrentContractVersion` is 15. Active source declares version 19, while
architecture, terminology, DemoHost/Godot codecs, and tests all use v19.

**Consequence:** a developer following the active technical guide can reject a
current save or author the wrong host wire contract. The documentation
synchronization test currently checks architecture, terminology, and player
save guidance, but not this active technical authority, so all tests pass while
the contradiction remains.

**Required correction:** rewrite this section for v19, explicitly describing
the v16 equipment-instance authority, v17 authored slots, v18 currency ledger,
and v19 durable shop stock changes that remain current. Extend the compiled-
version documentation guard to every active document that claims the current
save version.

## Code Health That Held Up

The review did not find a defect in these Order 7 areas:

- inventory owns immutable equipment instances and permits separate copies of
  one definition;
- standard and custom authored slot layouts use one fault-contained evaluator;
- equipment grants are derived from the current profile and never enter learned
  move-list state;
- armor Defense and armor/boots Evasion enter the existing combat formulas as
  additive stat inputs, with no equipment-specific formula;
- named-currency credit/debit paths are explicit, overflow-safe, and atomic;
- standard pricing preserves authored purchase price and configured resale;
- durable limited stock uses stable shop/offer identity and is committed with
  inventory and currency as one candidate result;
- standard recovery stages actor cleanup, named-currency debit, and live actor
  commit without partial mutation on expected rejection;
- save v19 validates the complete inventory/loadout/stock graph and aggregate
  restore re-derives equipment profiles before actor construction; and
- DemoHost adopts complete shop results and runs equipment application before
  presenting equip success.

## Documentation Cross-Examination

| Subject | Code/document alignment |
|---|---|
| Equipment instance ownership | Correct in player, developer, technical, API, save, and restore descriptions; H1/M1 expose public paths that do not yet enforce the complete promise live. |
| Authored slot layouts | Aligned. The standard economy bundle deliberately uses the standard four-slot layout; custom layouts require one shared manually assembled service set. |
| Granted skills | Aligned for canonical paths: equipped-only, immediately re-resolved, never learned. |
| Defense and Evasion | Aligned for canonically composed actors and true zero-equipment no-op. H1 permits stale values through a public bypass. |
| Currency ledger | Aligned: every transaction names a currency and the one-currency helper rejects empty/ambiguous ledgers. |
| Pricing and stock policies | Formula/stock behavior aligns; M2 contradicts the documented promise that malformed policy faults remain typed. |
| Recovery | Aligned: configured resources, legally removable ailments, configured temporary state, named currency, and atomic expected rejection. |
| Persistence | Runtime behavior is v19 and coherent; L1 leaves one active technical document at v15. |
| Roadmap status | Correctly remains `partial`/open pending an independent closure decision. No status promotion is justified by this review. |

## Verification Performed

| Gate | Result |
|---|---|
| Focused Order 7 tests | 198 passed, 0 failed, 0 skipped |
| Architecture/documentation boundary tests | 62 passed, 0 failed, 0 skipped |
| Full `dotnet test Convergence.sln --no-restore` | 1,807 Framework + 184 DemoHost + 7 ContentValidator = 1,998 passed; 0 failed; 0 skipped |
| Strict Release solution build | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | passed |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions passed schema, deserialization, semantic, dependency, registration, and catalog checks |
| Noninteractive DemoHost modes | battle, field, save v19, and Training Annex completed with exit code 0 |
| Worktree before this report | clean at `49db0396` |

These green gates establish broad regression health. They do not exercise the
public bypasses in H1/M1, the malformed binding-result shape in M2, or the stale
technical-version statement in L1; those absences explain why the suite can be
green while this review still withholds closure.

## Closure Verdict

Order 7 has a strong implementation core, but formal closure would be premature.
The fixed equipment-ownership rule still has exported mutation paths that can
create stale or multiply assigned live state, live acquisition can accept state
that only save validation rejects, the pricing/stock extension boundary is less
defensive than its documented contract, and one active technical guide names an
obsolete save version.

Correct H1, M1, M2, and L1 in isolated commits, add the missing adversarial
tests and documentation guard, then perform one source-first closure recheck.
Only that recheck should promote `inventory_equipment_economy` from `partial`
to `complete` and close Order 7.
