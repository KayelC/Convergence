# Inventory, Equipment, And Economy Order 7 R10 Documentation Audit

**Date:** 13 August 2026

**Capability:** `inventory_equipment_economy`

**Source baseline:** `23cf50c1` (`docs: close order 7 wire integrity review`)

**Checkpoint:** O7-R10, three-audience documentation

**Review state:** complete; source, tests, and all three audiences reconciled

## Purpose

This audit translates the implemented Order 7 source into the three active
documentation audiences without treating the earlier roadmap or R9 report as
implementation proof. It rereads the current runtime, content, save, host, and
test surfaces and records the exact documentation work needed before O7-R10 can
promote any audience entry to `reviewed`.

O7-R10 is a documentation checkpoint. It must not add presentation behavior,
change runtime rules, alter schema v10 or save v19, or begin the independent
O7-R11 closure audit.

## Current Source Authority

The source inspected for this audit establishes these authorities:

| Concern | Current authority | Executable evidence |
|---|---|---|
| Owned equipment copies | `RuntimeInventorySnapshot` owns immutable `RuntimeEquipmentInstanceSnapshot` values keyed by unique `RuntimeInstanceId`; actor loadouts hold only instance references | `EquipmentInstanceOwnershipTests`, persistence tests |
| Slot vocabulary and compatibility | `IEquipmentSlotLayoutPolicy`; `StandardEquipmentSlotLayoutPolicy` supplies the standard weapon, armor, boots, and accessory IDs | `EquipmentSlotLayoutTests`, content-validation tests |
| Current equipment projection | `RuntimeEquipmentProfileResolver` resolves one profile from inventory ownership, actor references, catalog definitions, and the selected slot layout | resource-management, actor-composition, battle-action, and persistence tests |
| Equipped skill grants | The resolved profile feeds canonical action authorization and passive composition; grants are not copied into learned or move-list state | equipment-combat and typed-action tests |
| Combat contributions | Armor Defense and armor/boots Evasion become ordinary additive stat inputs before the existing damage and hit policies run | equipment-combat contribution tests |
| Currency | `RuntimeCurrencyLedgerSnapshot` owns nonnegative balances keyed by currency `ContentId`; every transaction names its currency | currency-ledger, shop, recovery, persistence, and host tests |
| Shop offers and prices | `IRuntimeShopOfferResolver` binds authored offers through selected pricing and stock policies; `ShopTransactionService` revalidates current immutable state | pricing, stock, resource-management, and wire-integrity tests |
| Limited stock | `RuntimeShopStockSnapshot` owns remaining quantities by qualified shop ID plus shop-local offer ID | stock-policy, save/restore, DemoHost, and Godot-contract tests |
| Recovery | `IRecoveryPolicy` plans treatment; `RecoveryService` stages actor cleanup and the named-currency debit and commits atomically | recovery-policy, lifecycle, persistence, DemoHost, and Godot tests |
| Persistence | Save contract v19 stores inventory-owned equipment instances, actor loadout references, typed currency balances, and policy-owned shop stock | `RuntimePersistenceSnapshotTests` and aggregate-restore tests |

The selected economy ruleset is bound once through
`RuntimeRulesetBindingResolver.BindResourceManagementServices`. The returned
`ResourceManagementRulesetServices` bundle exposes inventory, equipment,
economy, offer resolution, shop transactions, and optional recovery. Hosts
adopt only successful immutable result snapshots; they do not mutate framework
snapshots in place.

## Documentation Findings

### O7-R10-F1: Player Mechanics Mix Observable Rules With Integration Detail

**Severity:** documentation quality

[`party-inventory-and-economy.md`](../mechanics/party-inventory-and-economy.md)
contains substantially correct post-R9 facts, but much of the page is expressed
in runtime type names and service choreography. A player-facing page should
lead with what a player can observe:

- separate copies of one item may be owned and equipped by different actors;
- buying equipment does not silently equip it;
- an equipped copy cannot be sold or removed as though it were free;
- equipment-granted skills exist only while that exact copy is equipped;
- armor and boots affect the same combat calculations as other stats;
- displayed prices and stock can vary by the game's selected policies;
- failed purchases, sales, and recovery attempts change nothing; and
- recovery restores only the resources and removable state configured by the
  game.

Implementation names belong in the developer and technical pages. The revised
mechanics page must also distinguish fixed framework rules from game-selected
policy behavior.

### O7-R10-F2: The Developer Integration Guide Is Missing

**Severity:** missing audience

The documentation coverage matrix correctly records no Order 7 developer
guide. Existing guides mention isolated pieces, such as equipment-granted
skills in `typed-actions-and-effects.md`, but no current page shows the complete
composition path:

1. bind `ResourceManagementRulesetServices`;
2. retain globally unique equipment instance IDs;
3. resolve one live equipment profile for menus, composition, authorization,
   battle execution, and restore;
4. resolve authored shop offers before a transaction;
5. pass the explicit currency and current inventory/currency/stock snapshots;
6. adopt all successful after-snapshots together and none on rejection;
7. assess and execute optional recovery without duplicating its formula; and
8. serialize save snapshots and reconnect Godot Nodes only in the host.

A dedicated developer guide is required, including accepted and rejected code
paths and explicit host responsibilities.

### O7-R10-F3: The Dedicated Technical Authority Reference Is Missing

**Severity:** missing audience

The matrix points technical coverage at the broad gameplay overview. That page
cannot replace a maintainer reference for Order 7's ownership graph and atomic
transitions. The dedicated technical page must define:

- inventory as sole equipment-instance owner;
- actor loadouts as non-owning instance references;
- unique-instance, slot-layout, cross-actor assignment, and save invariants;
- the one equipment-profile derivation path;
- immutable inventory/currency/stock transaction boundaries;
- pricing and stock policy authority;
- recovery planning, staged cleanup, debit, and rollback;
- save v19 validation and dependency-ordered aggregate restoration; and
- the trusted-host boundary for fresh IDs and after-snapshot adoption.

It must include readable diagrams for equipment equip/unequip, shop
assessment/execution, recovery, and save/restore authority.

### O7-R10-F4: Active API And Technical References Contain Stale Current-Version Labels

**Severity:** factual documentation defect

The current wire contract is save v19, but active text still describes the
current aggregate as v18 in parts of `public-api-contract.md`,
`technical/battle-knowledge-runtime.md`, and
`technical/stat-modifier-policy-runtime.md`. Version 18 introduced the typed
currency ledger; version 19 retained it and added durable shop stock. Historical
statements about what v18 introduced are valid, but current-contract statements
must name v19 and distinguish that history explicitly.

The new audience pages must cross-link the current public API, content contract,
ruleset policy, persistence, content-validator, and Godot integration guidance
so readers do not need to infer this boundary from unrelated pages.

## Ordered O7-R10 Documentation Checkpoints

Each checkpoint receives its own green commit.

| Checkpoint | State | Work | Promotion rule |
|---|---|---|---|
| O7-R10-D0 | complete | Add and index this source-traced audit | No audience promotion |
| O7-R10-D1 | complete | Rewrite the player mechanics page around observable behavior and game-selected variation | Mechanics remained unreviewed until final reconciliation |
| O7-R10-D2 | complete | Add the developer integration guide with concrete composition and host-owned adoption examples | Developer guide remained unreviewed until final reconciliation |
| O7-R10-D3 | complete | Add the technical authority/state-machine reference and diagrams | Technical remained unreviewed until final reconciliation |
| O7-R10-D4 | complete | Correct stale version labels, add API/content/Godot cross-links, synchronize indexes, and promote all three matrix entries only after source/test/doc agreement | All three audiences are `reviewed`; capability remains `partial` for O7-R11 |
| O7-R10-D5 | complete | Freshly reread source and all three documents, run the complete applicable gate, and append the R10 completion record | O7-R10 complete; O7-R11 remains open |

## Verification Contract

Every documentation commit must pass the focused documentation/architecture
tests, Markdown link and terminology gates, formatting checks, and
`git diff --check`. The final R10 checkpoint additionally runs:

- the complete `dotnet test Convergence.sln --no-restore` suite;
- strict nonincremental Framework and solution builds;
- all active content through schema, semantic, dependency, registration, and
  catalog construction validation;
- all DemoHost modes plus scripted Training Annex play;
- the real Godot 4.7.1 headless smoke path;
- API-baseline, XML-documentation, forbidden-reference, and coverage gates; and
- a fresh source-to-document reconciliation that does not treat this audit as
  proof.

## Scope Guard

O7-R10 does not change runtime behavior, add policy interfaces, revise active
content or wire versions, add presentation UI, or mark
`inventory_equipment_economy` complete. Only O7-R11 may perform the independent
adversarial closure audit and return the capability to `complete`.

## Completion Result

The fresh
[O7-R10 documentation review](inventory-equipment-economy-order-7-r10-documentation-review-2026-08-13.md)
corrected three source-to-document precision issues, found no remaining
actionable R10 contradiction, and passed the complete local gate. All three
audience entries are now `reviewed`. The capability remains `partial` and Order
7 remains open for O7-R11.
