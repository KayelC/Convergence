# Inventory, Equipment, And Economy Order 7 R10 Documentation Review

**Date:** 13 August 2026

**Reviewed baseline:** `a08d216b` (`docs: correct order 7 integration claims`)

**Capability:** `inventory_equipment_economy`

**Checkpoint result:** O7-R10 complete; O7-R11 and Order 7 remain open

## Review Method

This review reread the current source, tests, host integrations, and all three
Order 7 audience documents. Earlier reports were used only to identify the
approved scope; they were not accepted as evidence that the implementation or
documentation was correct.

The source pass traced:

- inventory, equipment-instance, currency, shop, and transaction authority in
  `Runtime/ResourceManagementServices.cs`;
- profile derivation in `Runtime/RuntimeEquipmentProfiles.cs` and slot
  compatibility in `Runtime/EquipmentSlotLayouts.cs`;
- pricing, stock, and recovery in `Runtime/ShopPricingPolicies.cs`,
  `Runtime/ShopStockPolicies.cs`, and `Runtime/RecoveryPolicies.cs`;
- composition and combat consumption in
  `Runtime/RuntimeActorCombatProfileComposition.cs`,
  `Battle/ProductionCombatRuleset.cs`, and
  `Execution/BattleActionAuthorization.cs`;
- save validation and aggregate restoration in
  `Runtime/RuntimePersistenceSnapshots.cs` and
  `Runtime/RuntimeSessionRestoration.cs`;
- actor creation and execution synchronization in
  `Encounters/CatalogBattleActorFactory.cs` and
  `Execution/RuntimeActorExecutionTransaction.cs`;
- Training Annex shop/recovery adoption and the Godot save/smoke integration;
  and
- the mechanics, developer, and technical Order 7 documents.

## Findings

No unresolved High, Medium, or Low defect was found within O7-R10's
documentation scope. The fresh pass did find three factual/integration issues
before certification; all were corrected in `a08d216b` and guarded by
`DocumentationFoundationTests`.

### R10-C1: Recovery Policy Authority Was Described Too Broadly

The player page implied that a recovery policy could choose whether a selected
resource was restored fully. The actual `RecoveryTreatmentPlan` selects
resource IDs, while `RecoveryService` always restores each selected resource to
its current maximum. The page now says that the policy chooses *which resources
are fully restored*.

### R10-C2: Currency And Restore Validation Boundaries Were Imprecise

The technical page called transaction amounts positive even though zero is a
valid nonnegative amount and is required by zero-cost recovery. It also placed
currency-domain validation inside aggregate save validation. The immutable
`RuntimeCurrencyLedgerSnapshot` constructor actually rejects invalid IDs,
duplicates, and negative balances before an aggregate snapshot reaches restore.
The mechanics, developer, technical, and diagram wording now preserve that
ordering.

### R10-C3: Custom Slot Integration Omitted Two Required Boundaries

The developer guide showed one layout shared by transitions, profile
resolution, and shop offers, but omitted semantic content validation and save
validation. It now demonstrates the same layout in
`SkillSystemContentValidator` and `RuntimeSaveValidator` as well.

The standard resolver exposes one current basic attack because the standard
layout has one weapon slot. A custom layout supporting simultaneous weapon
profiles must provide an `IRuntimeEquipmentProfileResolver` with an explicit
selection rule. The guide now states that boundary instead of allowing slot
iteration order to become accidental game design.

## Source-To-Document Reconciliation

### Equipment Authority

`RuntimeInventorySnapshot` is the only owner of
`RuntimeEquipmentInstanceSnapshot` copies. Actor equipment stores non-owning
slot-to-instance references. Equip, sale, save validation, and aggregate restore
reject missing ownership, duplicate instance identity, incompatible slots,
cross-actor assignment, and actor/equipment ID collisions at their respective
boundaries. The three audience documents describe that same graph and do not
reinstate a second root equipment collection.

`RuntimeEquipmentProfileResolver` derives definitions, grants, one basic
attack, and plain stat contributions from the current inventory and loadout.
Granted skills are not written into learned or move-list state. Armor Defense
and armor/boots Evasion enter actor composition as ordinary values consumed by
the existing production damage and hit policies; no equipment-specific combat
formula exists.

### Economy And Shop Authority

Every currency transaction names a `ContentId`. The immutable ledger owns
nonnegative balances and provides a strict one-entry convenience accessor that
rejects empty and multi-currency ledgers. Shop offers are resolved through the
selected pricing, stock, and slot-layout policies before execution.

Purchase and sale return one immutable result covering inventory, currency, and
stock. The host adopts all three after-snapshots only when `Applied` is true.
Later rejection reconstructs equal before-state rather than leaking an earlier
candidate transition. The mechanics page explains the observable outcome; the
developer guide explains adoption; the technical page records the authority
and operation order.

### Recovery And Restore Authority

Recovery policy output names currency, cost, fully restored resources, ailment
cleanup, and temporary-state cleanup. Execution stages changes on a detached
actor, performs canonical removal checks, calculates the named-currency debit,
and commits the actor only after every step succeeds. Assessment exposes a
hypothetical after-ledger that must not be adopted. Cancellation remains an
`OperationCanceledException`.

Save contract v19 stores inventory-owned equipment copies, actor references,
the typed currency ledger, and durable shop stock in one aggregate. Aggregate
restoration validates before actor creation and exposes either one complete
session or diagnostics, never a partial session. Host save serialization and
Godot Node mapping remain outside Framework.

## Trusted Host And Extension Boundaries

These are documented integration obligations, not hidden Framework claims:

- the host allocates runtime IDs fresh across actors and equipment instances;
- the host adopts complete successful immutable results rather than individual
  candidate snapshots;
- replacing an inventory snapshot requires rebuilding any
  `RuntimeActorEquipmentProfileSource` that captured the old snapshot; and
- a game with multiple simultaneous weapon profiles supplies an explicit
  custom equipment-profile resolver.

O7-R11 must still adversarially test these and the runtime invariants defined in
the source roadmap. R10 documentation maturity is not runtime closure proof.

## Verification

- Focused Order 7/documentation coverage: 218 Framework tests and 133 DemoHost
  tests passed; 351 total, 0 failed, 0 skipped.
- Full suite: 1,790 Framework tests, 182 DemoHost tests, and 7 ContentValidator
  tests passed; 1,979 total, 0 failed, 0 skipped.
- Framework and complete solution Release builds passed with 0 warnings and
  0 errors. The Debug Godot sample build also passed with 0 warnings and
  0 errors.
- Formatting changed 0 of 279 files. The 58-test Release architecture,
  documentation-link, API, terminology, and boundary suite passed.
- The active-content validator accepted 6 packs, 36 documents, and 98 qualified
  definitions under schema v10.
- All four noninteractive DemoHost modes and scripted Training Annex exit
  succeeded.
- The official local Godot 4.7.1 headless consumer emitted
  `CONVERGENCE_GODOT_SMOKE_OK`, restored save v19 with 3 actors, 205 Credits,
  and one stock entry, and exited 0. The sandboxed first launch could not create
  Godot's user log; the identical invocation succeeded once normal user-directory
  access was permitted.
- Framework coverage passed at 90.19% lines and 76.71% branches, above the
  required 90% and 70% thresholds.
- Trimming analysis, `dotnet format --verify-no-changes`, architecture boundary
  tests, and `git diff --check` passed.

## Conclusion

The player, developer, and technical Order 7 documents now agree with current
source and executable evidence. O7-R10 is complete. The capability correctly
remains `partial`: O7-R11 is the separate independent adversarial audit that may
close Order 7 only if it finds no realistic reachable runtime defect or
documentation contradiction.
