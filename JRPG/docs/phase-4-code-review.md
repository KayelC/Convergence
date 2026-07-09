# Phase 4 Code Review And Readiness

> **Status: Active implementation audit; Phase 4-21 through Phase 4-25 are committed on `track-12-recovery` at `928288c` (`4-25`).** This report is derived from source, tests, builds, and executable demos. It does not authorize legacy removal and does not promote any protected capability to `clean_parity`.

## Executive Verdict

Phase 4 was implemented successfully as a framework-first resource-management slice for the original Training Annex content.

The clean host now proves inventory quantities, equipment ownership, wallet/economy mutation, catalog-backed shop transactions, and hospital-style recovery through framework-owned services. The implementation is not just console text: the real state changes flow through typed framework snapshots and services such as `RuntimeInventorySnapshot`, `RuntimeEquipmentSnapshot`, `RuntimeWalletSnapshot`, `IShopTransactionService`, `IHospitalRestorationService`, and ruleset-bound `ResourceManagementRulesetServices`.

No critical blocker was found. Phase 4 is suitable as a baseline for Phase 5, with two recommended quality follow-ups before the next major dependency is built on top of it:

1. Surface shop-offer resolution diagnostics in the clean host instead of silently omitting invalid offers.
2. Add a host-level recovery test proving live ailment and encounter-persistent status cleanup, not only HP/SP restoration and wallet spending.

Those follow-ups are not evidence that Phase 4 failed. They are the sort of sharp edges that become expensive if left invisible.

## Audit Scope

### Reviewed implementation

- Phase 4-21: inventory quantities.
- Phase 4-22: equipment ownership and equipped basic attacks.
- Phase 4-23: economy and wallet authority.
- Phase 4-24: Training Supply shop.
- Phase 4-25: Recovery Facility.
- Training Annex clean host code that consumes those systems.
- Framework resource-management and equipment-profile services.
- Focused framework and host tests supporting the Phase 4 slice.

### Repository state reviewed

- Branch: `track-12-recovery`.
- Current HEAD: `928288c` (`4-25`).
- Worktree: clean before this review document was added.
- `Data/Jsons`: unchanged.

## Verification Evidence

The Phase 4-25 gate recorded the following checks:

| Check | Result |
| --- | --- |
| Focused Phase 4 tests | 87 passed, 0 failed, 0 skipped |
| Full solution tests | 839 passed, 0 failed, 0 skipped |
| Framework nonincremental build | succeeded, 0 warnings |
| Full solution nonincremental build | succeeded, 98 existing legacy-host warnings |
| Clean battle demo | passed |
| Clean field demo | passed |
| Clean save demo | passed |
| Training Annex runtime/play coverage | passed |
| `git diff --check` | passed; only Git line-ending notices were present |
| Framework forbidden-reference search | no framework leaks of console, Godot, filesystem, Newtonsoft, `Database`, `SkillData`, `ItemData`, or legacy host types |
| `Data/Jsons` worktree check | clean |

Passing tests are not the same as proof of completion, but they are strong evidence that Phase 4 did not regress the framework boundary or existing host behavior.

## Current Phase 4 Architecture From Code

```text
Training Annex catalog content
        |
        v
RuntimeRulesetBindingResolver
        |
        v
ResourceManagementRulesetServices
        |
        +--> InventoryTransitionService
        +--> EquipmentTransitionService
        +--> EconomyTransactionService
        +--> ShopTransactionService
        +--> HospitalRestorationService
        |
        v
CleanTrainingAnnexPlayHost presentation
        |
        v
Runtime snapshots and save validation
```

This is the correct ownership direction:

- The framework owns immutable state shapes, transaction rules, service results, and diagnostics.
- The Training Annex host owns menu labels, demo starting inventory, demo starting equipment, command selection, and visible messages.
- Content IDs such as `annex_tonic`, `practice_blade`, `focus_charm`, and `training_supply` are host/content wiring, not framework assumptions.
- The framework still contains no dependency on legacy console systems.

## Phase 4-21 Review: Inventory Quantities

### What was built

The Training Annex field inventory now uses the live clean `RuntimeInventorySnapshot` instead of a hardcoded item path. Item use flows through `TrainingAnnexItemActionInventory`, which implements `IItemActionInventory` by reserving items through `IInventoryTransitionService`.

Relevant code:

- `Host/CleanConsole/TrainingAnnex/CleanTrainingAnnexPlayHost.cs`
  - builds the starting inventory through `BuildInitialInventory(...)`.
  - presents only field-usable catalog items that are present in the inventory snapshot.
  - writes the updated snapshot back after successful item actions.
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexFieldActionAdapter.cs`
  - `TrainingAnnexItemActionInventory.Reserve(...)` uses framework reservations.
  - reservation commit replaces the live immutable inventory snapshot.

### Why it is correct

The item executor receives typed item definitions and a reservation-backed inventory port. A failed, canceled, or no-effect action does not commit item consumption. This is exactly the framework-first model we wanted: the host owns inventory presentation, but the framework owns inventory mutation rules.

### Remaining limitation

This is clean Training Annex behavior, not production legacy parity. The legacy console inventory path still exists and remains protected.

## Phase 4-22 Review: Equipment Ownership

### What was built

Phase 4-22 added clean equipment ownership and equip state to the Training Annex path. The host seeds owned equipment through framework inventory transitions, equips valid items through `IEquipmentTransitionService`, and resolves equipped profiles through `RuntimeEquipmentProfileResolver`.

Relevant code:

- `JRPG.Framework/Logic/Runtime/RuntimeEquipmentProfiles.cs`
  - resolves equipped definitions by slot.
  - reports missing and slot-mismatched equipment diagnostics.
  - exposes a weapon basic attack profile and accessory stat modifiers.
- `Host/CleanConsole/TrainingAnnex/CleanTrainingAnnexPlayHost.cs`
  - seeds `practice_blade` and `focus_charm`.
  - equips requested/owned equipment through framework services.
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs`
  - clean basic attacks use the current equipment profile.

### Why it is correct

The equipped weapon is not flavor text. Tests prove swapping the equipped weapon changes the clean basic attack profile, and framework tests prove accessory stat modifiers feed stat resolution. That gives the framework a real equipment-owned combat/stat seam.

### Remaining limitation

Equipment effects are intentionally small. This phase proves ownership, equip state, stat modifiers, and weapon basic attacks. It does not complete production equipment design, equipment instances, durability, shops with persistent stock, or Godot presentation.

## Phase 4-23 Review: Economy

### What was built

The Training Annex now requires the catalog-authored `standard_economy` ruleset binding before the interactive session starts. The bound `ResourceManagementRulesetServices` provide inventory, equipment, economy, shop, and hospital services. Victory Macca and shop/hospital costs mutate a live `RuntimeWalletSnapshot`.

Relevant code:

- `JRPG.Framework/Logic/Runtime/RuntimeRulesetBindings.cs`
  - `BindResourceManagementServices(...)` creates the framework resource-management service set.
- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs`
  - `EconomyTransactionService` applies typed wallet mutations and rejects invalid/overflowing operations without mutating state.
- `Host/CleanConsole/TrainingAnnex/CleanTrainingAnnexPlayHost.cs`
  - refuses startup when `standard_economy` cannot bind.
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexBattleRewardApplicator.cs`
  - applies battle Macca through the bound economy service.

### Why it is correct

There is no fallback to legacy Macca logic in the clean path. Economy mutation is a typed service boundary, and the host summary records actual wallet transaction evidence. This is the right shape for later Godot use because Godot can present the wallet however it wants while consuming the same transaction results.

### Remaining limitation

The clean Training Annex proves wallet authority, but legacy shops, legacy hospital, and legacy rewards are still separate compatibility paths. This is therefore `parallel_partial`, not full parity.

## Phase 4-24 Review: Shops

### What was built

The clean Training Annex exposes a `Training Supply` shop over catalog-authored shop offers. Offers are resolved into `RuntimeShopOfferSnapshot`s, buy/sell menus assess availability through `IShopTransactionService`, and execution uses the same transaction service. Successful equipment purchases can be immediately equipped through `IEquipmentTransitionService`.

Relevant code:

- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs`
  - `RuntimeShopOfferResolver` maps authored item/equipment offers into runtime offers.
  - `ShopTransactionService.Buy(...)` and `Sell(...)` own pricing, inventory mutation, wallet mutation, duplicate-equipment rejection, and equipped-sale blocking.
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexShopController.cs`
  - builds buy/sell menus from resolved catalog offers.
  - assesses and executes through the same transaction service.
  - applies successful inventory, wallet, and equipment changes back to live clean state.

### Why it is correct

The menu and the mutation path are aligned. Disabled rows are based on framework transaction assessment, and selected rows execute through the same transaction service. That is a meaningful improvement over presentation guessing.

### Remaining limitation

Clean shop stock is not persistent yet. `RuntimeShopOfferSnapshot.StockAvailable` is checked during a buy, but there is no `RuntimeShopStateSnapshot` that decrements and persists limited stock across visits. That is acceptable for the current Training Annex proof, but it should be designed before production shops rely on limited stock.

## Phase 4-25 Review: Hospital

### What was built

The clean Training Annex exposes a `Recovery Facility`. It captures the live player as a `RuntimeHospitalPatientSnapshot`, assesses treatment through `IHospitalRestorationService`, displays availability from that assessment, executes through the same service, spends Macca, restores HP/SP, removes ailments, and clears encounter-persistent clean status with `BattleStatusLifecycleService.Cleanup(FieldTransition)`.

Relevant code:

- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs`
  - `HospitalRestorationService.Restore(...)` calculates cost, rejects no-op treatment, spends Macca atomically, and returns before/after patient and wallet snapshots.
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexRecoveryFacilityController.cs`
  - captures live resource state.
  - applies the successful restoration back to the live `RuntimeActorState`.
  - removes ailments and invokes field-transition cleanup for encounter-persistent statuses.

### Why it is correct

The important bit is that assessment and execution are the same framework operation. Menu availability cannot approve something the mutation path rejects under the same state. Successful treatment is applied to the same authoritative clean actor state used by battle, field actions, save validation, and summaries.

### Remaining limitation

Host-level tests currently prove HP/SP restoration, wallet spending, insufficient-funds behavior, and no-restoration behavior. Framework tests prove ailment and encounter-persistence cleanup in the service result. The live Training Annex host should still get one focused test proving ailments/statuses are actually removed from the actor object after treatment.

## Findings

### Medium: Invalid shop offers are silently omitted from the clean host

`TrainingAnnexShopController.ResolveShopOffers(...)` resolves each authored offer, but failed resolutions return `null` and are removed by `.OfType<TrainingAnnexResolvedShopOffer>()`.

Code evidence:

- `Host/CleanConsole/TrainingAnnex/TrainingAnnexShopController.cs:339`
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexShopController.cs:340`
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexShopController.cs:342`
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexShopController.cs:352`

Why this matters:

The framework resolver has useful diagnostics for missing equipment definitions, unsupported price policies, invalid fixed prices, and unsupported stock policies. The Training Annex host currently hides those diagnostics by dropping the bad offer. With the present valid sample content this does not break behavior, but it could make future content errors look like a mysteriously empty shop row.

Recommended resolution:

Before or during early Phase 5, return a detailed shop resolution result from `ResolveShopOffers(...)`. Either fail opening the shop with diagnostics or publish those diagnostics clearly before suppressing invalid rows. Add a test-only malformed shop offer to prove diagnostics are visible and no fallback transaction happens.

### Medium: Recovery live cleanup needs one stronger host-level test

`HospitalRestorationService` correctly returns `HasAilment = false` and `HasEncounterPersistence = false` on successful restoration. The Training Annex controller also removes ailments and calls cleanup on the live actor:

- `Host/CleanConsole/TrainingAnnex/TrainingAnnexRecoveryFacilityController.cs:154`
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexRecoveryFacilityController.cs:158`
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexRecoveryFacilityController.cs:159`

The host tests added for Phase 4-25 focus on HP/SP, wallet, insufficient funds, and no-op treatment. They do not yet create a live ailment or encounter-persistent status and prove it is removed from the actor.

Recommended resolution:

Add a focused host/controller test that puts an ailment plus one encounter-persistent status on the Training Annex player, runs recovery, and asserts the live `RuntimeActorState` is clean afterward. This closes the gap between framework result coverage and host mutation coverage.

### Medium, deferred by design: Clean shop stock is not persistent

`ShopTransactionService.Buy(...)` checks `RuntimeShopOfferSnapshot.StockAvailable`, but the result does not include a changed shop-stock state:

- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs:492`
- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs:498`
- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs:721`
- `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs:729`

Why this matters:

Unlimited shops and one-shot proof shops are fine. Production limited-stock shops will need a runtime shop-state snapshot if stock is meant to decrease and persist after purchase.

Recommended resolution:

Do not force this into Phase 4 retroactively. Track it as a future shop-completion item before production shop content depends on finite stock.

### Low: `CleanTrainingAnnexPlayHostTests` is becoming too large and index-heavy

The host test file now contains more than 3,000 lines and many scripted menu paths depend on numeric selection indices. The tests are valuable, but the shape is becoming harder for a human to own.

Why this matters:

Large index-driven scripts can fail for harmless menu ordering changes or, worse, obscure exactly which command is being tested. We already have typed command identities in the host-command layer; the tests should lean on them more.

Recommended resolution:

Split the Training Annex host tests by capability area when the next major test addition occurs:

- inventory/equipment;
- economy/shop/hospital;
- battle;
- save/load;
- navigation/dungeon.

Add helper methods that select by command identity or label instead of raw numeric indices where possible.

### Low: Recovery controller constructs its lifecycle cleanup service internally

`TrainingAnnexRecoveryFacilityController.ApplyRestoration(...)` creates `new BattleStatusLifecycleService(new TrainingAnnexMinimumRandomSource())` directly.

Code evidence:

- `Host/CleanConsole/TrainingAnnex/TrainingAnnexRecoveryFacilityController.cs:159`

Why this matters:

The current cleanup operation is deterministic, so this is not a functional bug. Still, most of the clean host has moved toward injected services. Keeping this hidden construction makes future lifecycle-policy changes harder to test.

Recommended resolution:

If the recovery facility grows beyond the current simple proof, inject `IBattleStatusLifecycleService` into the controller.

## Hardcoding Review

No problematic framework hardcoding was found in Phase 4.

There are host/content-specific defaults in the Training Annex host:

- starting `Annex Tonic` quantity;
- starting `Practice Blade` and `Focus Charm`;
- the `Training Supply` shop ID;
- the single-player `Recovery Facility` target.

Those are acceptable because Training Annex is an original sample host. They do not leak into `JRPG.Framework`, and they do not determine framework rules. The rules still come from catalog definitions, ruleset binding, and framework services.

The one place that needs attention is not hardcoding but diagnostic loss: invalid shop offers are currently hidden from the host by omission.

## Readiness For Phase 5

Phase 4 is ready to stand as the current clean-resource baseline.

I would not call it full parity, because:

- legacy inventory, shop, hospital, and economy consumers still exist;
- no protected legacy capability has `removalAuthorized: true`;
- the Training Annex is a clean original-content proof, not a production replacement for the legacy console workflows;
- limited shop stock and broader production shop semantics remain incomplete.

I would proceed to Phase 5 after either:

1. implementing the two recommended follow-ups, or
2. explicitly accepting them as Phase 5-adjacent cleanup items.

My preference is to fix them now because they are small, contained, and strengthen trust in the next phase:

1. Make invalid shop-offer diagnostics visible.
2. Add the recovery live-status cleanup host test.

After that, Phase 5 can build on a cleaner foundation instead of inheriting invisible edge cases.
