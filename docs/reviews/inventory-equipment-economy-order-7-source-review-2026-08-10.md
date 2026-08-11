# Inventory, Equipment, And Economy Order 7 Source Review And Approved Roadmap

**Date:** 10 August 2026

**Capability:** `inventory_equipment_economy`

**Source baseline:** `21b0a016` (`refactor(encounter-runner): stage 7 - move scheduled round loop`)

**Owner-decision status:** general authority principle and decisions O7-D1 through O7-D8 approved

**Implementation status:** O7-R1 through O7-R4 complete; O7-R5 through O7-R11 pending

## Purpose

This record is the active source of truth for Documentation Order 7. It
preserves the owner's approved ownership and policy boundaries, reports the
current source honestly, and turns the approved decisions into an ordered
implementation and review sequence.

This is not evidence that the target design already exists. Current behavior is
established by source and tests until the relevant checkpoint changes it. The
approved target behavior is established by the decision ledger below. Audience
documentation becomes current authority only after implementation, executable
evidence, owner review, and the final adversarial closure audit agree.

## Review Method

The opening review inspected current active source and tests rather than relying
on older reports. The inspected boundaries included:

- `ContentSurfaceDefinitions.cs` and the active equipment/shop DTO mapping;
- `ResourceManagementServices.cs`;
- `RuntimeEquipmentProfiles.cs`;
- `RuntimeStateSnapshots.cs` and `RuntimePersistenceSnapshots.cs`;
- `RuntimeRulesetPolicyFactories.cs`;
- `ProductionCombatRuleset.cs` and its hit/damage request contracts;
- `TrainingAnnexShopController.cs` and active Training Annex shop content;
- `ResourceManagementServiceTests` and persistence tests; and
- the active capability and documentation matrices.

## Source-Verified Starting Point

The existing implementation provides a useful host-neutral foundation:

- immutable inventory, equipment, wallet, shop, and restoration results;
- typed rejection codes and diagnostics;
- checked integer and decimal arithmetic at transaction boundaries;
- item reservation and commit/rollback support for battle actions;
- item stack, ownership, equipped-sale, affordability, and stock-availability
  checks;
- catalog-backed shop offer resolution for fixed prices and limited/unlimited
  stock definitions;
- weapon basic-attack profiles and accessory stat modifiers; and
- save validation for inventory/equipment definitions, slots, ownership, and
  actor equipment references.

The following limitations are also present in the current source and are the
reason Order 7 is open:

1. `RuntimeInventorySnapshot` stores equipment definition IDs under the fixed
   `EquipmentSlot` enum. Owning a definition therefore means owning at most one
   copy of it.
2. Actor equipment also maps the fixed enum directly to definition IDs. There
   is no runtime equipment-instance identity.
3. `RuntimeSaveGameSnapshot` stores a root equipment snapshot while every actor
   already stores its own equipment. Those authorities are validated
   independently and are not required to agree.
4. `EquipmentArmorProfileDefinition.Defense`, armor/boots `Evasion`, and
   `EquipmentDefinition.GrantedSkillIds` are authored and validated, but
   `RuntimeEquipmentProfileResolver` currently consumes only weapon basic
   attacks and accessory stat modifiers.
5. Limited stock is copied into `RuntimeShopOfferSnapshot.StockAvailable` and
   checked during purchase, but a successful purchase returns no updated stock
   state. Reopening the sample shop reconstructs the original quantity.
6. Policy-shaped price and stock definitions exist in content, but the standard
   shop resolver rejects both. The standard economy ruleset accepts no policy
   parameters and `ShopTransactionService` embeds Luck-based price arithmetic.
7. `RuntimeHospitalPatientSnapshot` hardcodes HP, SP, and two booleans. The
   restoration service embeds one cost formula and clears ailment/persistence
   flags without consulting typed resource IDs or legal ailment removal.
8. `RuntimeWalletSnapshot` stores one unnamed balance, so transactions cannot
   name which currency they debit or credit.

The active capability matrix previously reported this capability as `complete`
with no gaps. That statement did not match the source above or the approved
work. Order 7 reclassifies it as `partial` until the implementation and closure
gate are complete.

## Owner Decision Set

> ORDER 7 - OWNERSHIP AND AUTHORITY DECISIONS
>
> GENERAL PRINCIPLE (apply this to every decision below, and to any judgment
> call not explicitly covered here)
> Wrap something in an I<X>Policy / Standard<X>Policy pair - matching the
> existing convention already used for stat resolution, resource growth,
> roster capacity, navigation, and disclosure rules - only where different
> games would legitimately want different formulas or behavior. Where the
> current implementation is simply wrong (an identity model, a data shape),
> fix it directly instead of wrapping it in a policy interface. Do not
> introduce a policy for a design axis that has only one concrete behavior
> today and no known second use case - that's speculative abstraction, not
> modularity, and it costs real interface surface for no current benefit.
>
> DECISIONS
>
> 1. Equipment ownership - APPROVED, implement as a direct fix, not a policy.
>    Each equipped item becomes a runtime instance with its own ID, referencing
>    one equipment definition. Two actors can equip separate copies of the same
>    definition. This is a data-model correction, not a behavioral variability
>    point.
>
> 2. Equipment slots - APPROVED as a policy.
>    Slots become developer-authored content IDs. Introduce
>    IEquipmentSlotLayoutPolicy with a StandardEquipmentSlotLayoutPolicy default
>    that reproduces the current Weapon/Armor/Boots/Accessory layout exactly,
>    so no existing content breaks. Follow the same interface/default-impl
>    shape already used elsewhere in Runtime.
>
> 3. Equipment-granted skills - APPROVED behavior, NOT a policy.
>    Granted skills remain available only while equipped, do not become
>    learned, do not consume move-list slots. Implement this as a fixed rule.
>    Do not create a policy interface for this - there is no second behavior
>    to make pluggable right now. If a real second use case shows up later,
>    extract it then.
>
> 4. Defense/evasion ratings - APPROVED.
>    Right now, armor and boots can be authored with Defense/Evasion values,
>    but nothing in combat actually reads them - equipping different armor
>    currently has zero effect on a fight. Fix this by having equipment
>    contribute plain numbers into the SAME combat formula that already
>    handles base stats (ProductionCombatRuleset), not by writing a second,
>    equipment-specific formula for what Defense/Evasion means in combat.
>
>    Equipment is a SOURCE of stat contributions, not a second decision-maker
>    about what those contributions do. One formula owner, fed from multiple
>    sources (base stats, equipment, buffs, etc.) - not two formulas that can
>    quietly drift apart over time, which is the same class of problem as the
>    dual equipment-save-authority bug already flagged elsewhere in this doc.
>
> 5. Shop stock - APPROVED as a policy.
>    Buying decrements limited stock. Selling does not replenish it unless a
>    supplied policy says otherwise. Standard implementation matches current
>    (non-)replenishing behavior.
>
> 6. Pricing - APPROVED as a policy, and treat this as closing an existing
>    gap, not adding new scope.
>    Standard price = purchase price + configurable resale percentage.
>    Luck-adjusted pricing becomes an optional supplied policy rather than a
>    hidden internal formula the standard economy bundle can't be configured
>    around. This brings the economy bundle in line with the
>    IRuntimeEconomyRulesetPolicyFactory convention the rest of the framework
>    already follows - it isn't new complexity, it's fixing the one place that
>    fell out of step with your own pattern.
>
> 7. Recovery - APPROVED as a policy, scoped narrowly.
>    Rename generically, configure by resource ID, following the same shape as
>    the existing IResourceGrowthPolicy/StandardResourceGrowthPolicy pair.
>    Build exactly one StandardHospitalRecoveryPolicy that does what the
>    actual game needs: full HP/SP restore, cures only legally removable
>    ailments, clears configured temporary state. Do not build additional
>    sample policies for stamina, mana, multi-resource, or per-treatment-type
>    recovery - there is no current game requirement for them, and they can be
>    added later against a real need without disturbing this interface.
>
> 8. Currency - APPROVED, implement as a direct data-model fix, not a policy.
>    Typed currency ledger keyed by currency content ID. A single-currency
>    game populates one entry and should incur no added friction - confirm
>    this with a convenience accessor for "the" currency when only one is
>    defined, so the common case doesn't get more verbose for the sake of the
>    general case.
>
> SCOPE NOTE
> Order 7 remains open, per your own recommendation, until this decision set
> is implemented and adversarially audited the same way Orders 1-6 were -
> not just implemented and unit-tested. Proceed with updating any relevant documents that state these approvals so that we will have a source of truth / roadmap for Order 7 implementations.

## Approved Decision Ledger

| ID | Approved decision | Implementation kind | Status |
|---|---|---|---|
| O7-G1 | Introduce a policy/default pair only for a real game-variable rule. Correct identity and data-shape errors directly. Do not add speculative policy surface. | Governing design rule | Approved |
| O7-D1 | Every owned equipment copy has a unique runtime instance ID and references one equipment definition. Different actors may equip different instances of the same definition. | Direct data-model correction | Approved; implemented by O7-R2 |
| O7-D2 | Equipment slot IDs are authored `ContentId` values. `IEquipmentSlotLayoutPolicy` owns valid layouts, and `StandardEquipmentSlotLayoutPolicy` supplies Weapon, Armor, Boots, and Accessory behavior. | Policy family | Approved; implemented by O7-R3 |
| O7-D3 | Equipped granted skills are available only while the granting instance is equipped. They are not learned and consume no move-list slot. | Fixed runtime rule | Approved; implemented by O7-R4 |
| O7-D4 | Equipment Defense and Evasion are numeric contributions to the existing `ProductionCombatRuleset` inputs. Equipment does not own a parallel damage or hit formula. | Direct integration correction | Approved; implemented by O7-R4 |
| O7-D5 | Runtime shop stock is stateful. Buying decrements limited stock. Standard selling does not replenish stock; a supplied policy may choose otherwise. | Policy family plus runtime state | Approved |
| O7-D6 | The standard pricing policy uses authored purchase price and configurable resale percentage. Luck-adjusted pricing remains available as an optional supplied policy, not hidden standard behavior. | Policy family | Approved |
| O7-D7 | Recovery is generic and resource-ID driven. Supply exactly one `StandardHospitalRecoveryPolicy` for full configured HP/SP recovery, legal ailment cures, and configured temporary-state cleanup. | Policy family, narrowly scoped | Approved |
| O7-D8 | Replace the unnamed wallet with a currency ledger keyed by currency `ContentId`. Supply a convenience accessor that succeeds only when exactly one currency exists. | Direct data-model correction | Approved |

## Target Authority Model

### Equipment

The inventory aggregate owns equipment-instance records. Each record contains a
unique runtime instance ID and a definition ID. An actor's equipment state maps
an authored slot ID to an owned equipment instance ID. Definition IDs describe
what an item is; instance IDs identify which copy is owned or equipped.

There must be one durable authority for equipment ownership and placement. The
separate root save equipment snapshot is retired rather than kept as a second
copy. Save validation proves that every equipped instance exists in inventory,
is referenced by at most one actor/slot, resolves to a definition, and is valid
for the selected slot layout.

### Equipment Effects

The canonical equipment profile resolves from equipped instance IDs through
their definitions. It contributes:

- the equipped basic-attack profile;
- equipped-only granted skills;
- existing typed stat modifiers;
- additive Defense contributions to the standard combat stat input; and
- additive Evasion contributions to the standard hit-resolution input.

The existing combat ruleset remains the sole owner of what Defense and Evasion
mean. Equipment composition only supplies values to that ruleset.

### Shops And Pricing

Authored offers need stable runtime identity before stock can be durable; stock
must never be keyed only by list position. A runtime shop-stock snapshot records
remaining quantities for limited offers. Purchase assessment and commit produce
inventory, currency, and stock before/after values as one atomic result.

Pricing policy selection is explicit. The standard policy preserves an authored
purchase price and derives resale value from a configurable percentage using one
documented deterministic rounding rule. The supplied Luck-adjusted policy keeps
the existing Luck-sensitive option for games that choose it.

### Recovery

The generic recovery service operates on typed actor resources, ailment state,
configured temporary state, and a named currency entry. Its selected policy
produces the quote and requested treatment plan. The service validates and
stages the complete actor/currency transition before publishing either side.

`StandardHospitalRecoveryPolicy` is the only supplied recovery behavior in this
order. Other game-specific recovery designs may implement the interface later;
Order 7 does not create speculative examples for them.

### Currency

The currency ledger stores nonnegative balances by currency `ContentId`.
Credit, debit, shop, recovery, and persistence requests name their currency.
For a ledger containing exactly one entry, a convenience member exposes that
entry without requiring the common single-currency game to repeat its ID at
every presentation boundary. Empty and multi-currency ledgers cannot pretend to
have one unambiguous default.

## Ordered Implementation Checkpoints

Each checkpoint must be an isolated green commit. A checkpoint is not complete
merely because focused tests pass; its source, public API, content/save wire
shape, active integrations, and affected documentation must remain coherent.

### O7-R1: Record Decisions And Correct Tracking

- add this source review and index it;
- mark `inventory_equipment_economy` as `partial` with source-verified gaps;
- record Order 7 as open in active roadmaps and audience tracking;
- add no runtime behavior.

### O7-R2: Establish Equipment Instance Ownership

- replace definition-ID-as-copy ownership with immutable equipment-instance
  records containing a runtime instance ID and definition ID;
- make inventory the sole owner of those instances;
- allow multiple instances to reference the same definition;
- make actor equipment reference instance IDs;
- reject missing, duplicated, or multiply equipped instance IDs atomically;
- remove the separate root save equipment authority and advance the unreleased
  save contract for the breaking shape change; and
- migrate Framework, DemoHost, Godot-contract, and persistence tests together.

This is a direct correction. No equipment-ownership policy is introduced.

### O7-R3: Author Equipment Slot Layouts

- replace `EquipmentSlot` wire/runtime identity with authored `ContentId` slot
  IDs;
- add `IEquipmentSlotLayoutPolicy` and
  `StandardEquipmentSlotLayoutPolicy`;
- provide stable standard IDs for Weapon, Armor, Boots, and Accessory;
- validate definition/slot/profile compatibility through the selected policy;
- update active content and strict schemas in one explicit pre-release schema
  revision; and
- preserve the current four-slot behavior under the standard policy.

### O7-R4: Complete Equipment Combat Contributions

- resolve granted skills from currently equipped instances only;
- merge them into canonical action authorization without writing them into
  learned skills or move-list slots;
- remove availability immediately when the granting instance is unequipped;
- feed armor Defense into the canonical damage-defense input;
- feed armor/boots Evasion into the canonical hit-resolution input;
- retain weapon basic attacks and existing accessory modifiers; and
- prove actor creation, Vessel composition, equipment changes, battle
  assessment/execution, and restore resolve the same equipment profile.

No granted-skill policy and no equipment-specific combat formula are added.

### O7-R5: Introduce Typed Currency Ledger Authority

- replace the unnamed wallet balance with immutable balances keyed by currency
  `ContentId`;
- require every credit/debit transaction to identify its currency;
- add the single-currency convenience accessor with explicit empty/multiple
  rejection;
- migrate shops, recovery, Compendium/economy consumers, saves, DemoHost, and
  Godot-contract evidence;
- validate duplicate IDs, negative balances, overflow, and missing referenced
  currency entries; and
- advance the unreleased save contract for the breaking shape change.

This is a direct correction. No currency policy is introduced.

### O7-R6: Bind Explicit Pricing Policies

- introduce the typed pricing-policy contract and standard implementation;
- make authored purchase price exact under the standard policy;
- make resale percentage configurable and define deterministic rounding;
- supply the current Luck-adjusted formula as a separately selected policy;
- let the standard economy ruleset factory bind the selected policy and its
  parameters;
- replace unsupported-policy rejection with typed factory resolution; and
- preserve overflow, negative-input, affordability, and rollback guarantees.

### O7-R7: Make Shop Stock Stateful And Policy-Owned

- give authored offers stable identity suitable for runtime state and saves;
- add immutable runtime stock snapshots for limited offers;
- introduce the stock-policy contract and standard implementation;
- decrement limited stock exactly once on a successful purchase;
- leave stock unchanged on every rejected or rolled-back purchase;
- keep standard sales non-replenishing;
- permit a supplied policy to replenish on sale; and
- commit inventory, currency, and stock as one atomic result.

### O7-R8: Generalize Recovery Through One Supplied Policy

- replace the HP/SP-specific patient DTO boundary with typed resource and actor
  state inputs;
- introduce the generic recovery policy contract;
- supply only `StandardHospitalRecoveryPolicy`;
- configure the restored resource IDs, removable ailment handling, temporary
  state cleanup, cost inputs, and currency ID;
- cure only ailments the canonical ailment-removal boundary permits;
- stage actor and currency changes atomically; and
- preserve current full-restore behavior under the standard configuration.

### O7-R9: Certify Cross-System And Wire Integrity

- load every active content pack under the revised schema;
- validate saves and aggregate restoration against equipment instances,
  authored slots, stock state, and typed currencies;
- prove rejected and cancelled operations preserve every before-state;
- prove stale or forged instance/offer/currency references cannot commit;
- run all DemoHost modes and Godot headless integration; and
- update the public API baseline only for deliberate approved contracts.

### O7-R10: Complete Three-Audience Documentation

- revise the player-facing mechanics page around observable ownership,
  equipping, granted skills, prices, stock, and recovery;
- add a developer guide for policy composition, instance IDs, transaction
  application, saves, and Godot integration;
- add a technical authority/state-machine document with atomic transaction and
  restore diagrams;
- cross-link current API and content-authoring references; and
- promote no audience entry until source, tests, diagrams, and owner intent
  agree.

### O7-R11: Independent Adversarial Closure Audit

- reread current source without treating this roadmap as implementation proof;
- exercise duplicate equipment copies, forged/stale instance IDs, slot-policy
  rejection, equipped-skill removal, combat contribution parity, stock races,
  rejected multi-ledger transactions, legal/illegal ailment cures, and restore;
- review all three audience documents against the code;
- run the complete local release gate; and
- return the capability to `complete` only if no realistic reachable defect or
  documentation contradiction remains.

## Per-Checkpoint Verification

Every implementation checkpoint requires, as applicable:

- focused Order 7 tests;
- the complete `dotnet test Convergence.sln --no-restore` suite;
- nonincremental .NET 8 Framework and solution builds with zero warnings;
- strict content schema, semantic validation, and catalog construction;
- API-baseline and XML-documentation checks;
- all DemoHost modes and scripted Training Annex coverage;
- Godot headless smoke when an integration contract changes;
- documentation links, matrix synchronization, terminology, and format gates;
- framework forbidden-reference checks; and
- `git diff --check`.

Tests must cover both accepted and hostile paths. For every rejected atomic
operation, inventory/equipment, actor state, stock, and currency before/after
snapshots must prove that no partial mutation escaped.

## Scope Guard

Order 7 does not add presentation UI, proprietary content, a second equipment
combat formula, speculative granted-skill behavior, speculative recovery sample
policies, or a currency policy. It does not start Order 8 navigation work.

Breaking pre-release content, save, and API changes required by the approved
data-model corrections are made explicitly and coherently. They are not hidden
behind compatibility aliases that would preserve two competing authorities.

## Closure Rule

Order 7 remains open until O7-R2 through O7-R10 are implemented and O7-R11
independently audits the result. Unit tests alone do not close it. The final
closure record must identify intended invariants, realistic reachable paths,
concrete consequences, reproducible evidence, and any trusted host boundaries.

## Checkpoint Completion Record

### O7-R1: Record Decisions And Correct Tracking

- **Baseline and commit:** `3947226f..803c2f38`; commit
  `803c2f38` (`docs: approve order 7 ownership authority roadmap`).
- **Actual destination:** this source review, the capability/documentation
  matrices, active product and documentation roadmaps, and current baseline
  mechanics/gameplay guidance.
- **Changed files:** `docs/gameplay-systems.md`,
  `docs/mechanics/party-inventory-and-economy.md`, `docs/reviews/README.md`,
  this source review, `docs/roadmap/documentation-completion-roadmap.md`,
  `docs/roadmap/framework-capability-matrix.md`,
  `docs/roadmap/product-roadmap.md`,
  `tests/Convergence.Framework.Tests/Fixtures/documentation-coverage-matrix.json`,
  and
  `tests/Convergence.Framework.Tests/Fixtures/framework-capability-matrix.json`.
- **Verification:** the reproduced post-R1 baseline was 1,703 Framework tests,
  178 DemoHost tests, and 7 ContentValidator tests: 1,888 total, 0 failed,
  0 skipped. The commit changed no runtime or sample-host source.
- **Tracking evidence:** `inventory_equipment_economy` is `partial`, Order 7
  is explicitly open, and O7-G1/O7-D1 through O7-D8 are retained as approved
  authority rather than reported as implemented behavior.

### O7-R2: Establish Equipment Instance Ownership

- **Baseline and commit:** `803c2f38..this checkpoint commit`; commit subject
  `runtime: establish equipment instance ownership`.
- **Actual destination:** `RuntimeEquipmentInstanceSnapshot` and
  `RuntimeInventorySnapshot.OwnedEquipmentInstances` own equipment copies;
  `RuntimeEquipmentSnapshot.EquippedInstanceIds` stores actor loadout
  references; save contract v16 stores only inventory ownership plus actor
  references.
- **Changed Framework files:**
  `Execution/BattleActionAuthorization.cs`, `Execution/BattleRuntimeState.cs`,
  `PublicAPI.Shipped.txt`, `Runtime/ResourceManagementServices.cs`,
  `Runtime/RuntimeActorSnapshotIntegrity.cs`,
  `Runtime/RuntimeEquipmentProfiles.cs`,
  `Runtime/RuntimePersistenceSnapshots.cs`,
  `Runtime/RuntimeSessionRestoration.cs`, and
  `Runtime/RuntimeStateSnapshots.cs` under `src/Convergence.Framework`.
- **Changed host files:** `CleanSaveDemoHost.cs`,
  `CleanTrainingAnnexDemoHost.cs`, `CleanTrainingAnnexPlayHost.cs`,
  `TrainingAnnexBattleActionAdapter.cs`, `TrainingAnnexHostSupport.cs`,
  `TrainingAnnexPersistenceController.cs`, and
  `TrainingAnnexShopController.cs` under `samples/Convergence.DemoHost`, plus
  `Infrastructure/GodotSaveCodec.cs` and
  `Scripts/ConvergenceSmokeRoot.cs` under `samples/Convergence.GodotHost`.
- **Changed test files:** `CleanSaveDemoHostTests.cs`,
  `CleanTrainingAnnexPlayHostTests.cs`, `CleanSaveTestFixture.cs`,
  `GodotIntegrationContractTests.cs`,
  `BattleKnowledgeExecutionTransitionTests.cs`,
  `CompendiumRuntimeServiceTests.cs`, `ProgressionPolicyTests.cs`,
  `ResourceManagementServiceTests.cs`, `RuntimeEnumBoundaryTests.cs`,
  `RuntimePersistenceSnapshotTests.cs`, `RuntimeStateSnapshotTests.cs`, the
  new `EquipmentInstanceOwnershipTests.cs`, and both active matrix fixtures.
- **Changed active documentation:** `architecture.md`, `project-vision.md`,
  `public-api-contract.md`, `terminology-boundary.md`,
  `gameplay-systems.md`, `godot-integration-contract.md`, the saving,
  party/economy, and actor mechanics pages, the actor/stat/status developer
  guides, the actor/stat/status/knowledge technical guides, the two affected
  combat decisions, the three active product/documentation/capability
  roadmaps, the completed actor roadmap's current-state banner, and this
  completion record.
- **Focused tests:** 93 Framework equipment/resource/persistence/Godot-contract
  tests and 127 DemoHost save/Training Annex tests passed; 0 failed and 0
  skipped. The dedicated `EquipmentInstanceOwnershipTests` class contains 7
  passing ownership-boundary tests.
- **Full suite:** 1,710 Framework tests, 178 DemoHost tests, and 7
  ContentValidator tests passed: 1,895 total, 0 failed, 0 skipped.
- **Build and integration:** the nonincremental .NET 8 solution build passed
  with 0 warnings and 0 errors; formatting and `git diff --check` passed; all
  four noninteractive DemoHost modes and scripted Training Annex exit passed;
  6 packs, 36 documents, and 98 definitions passed content validation; the
  real Godot 4.7.1 headless consumer emitted `CONVERGENCE_GODOT_SMOKE_OK`,
  restored 3 actors under save contract v16, and exited 0.

#### Explicit O7-R2 Authority Evidence

1. **Instance IDs are unique.** Inventory construction rejects one equipment
   instance ID in multiple entries; add/purchase rejects a previously owned
   instance ID; save validation rejects equipment IDs colliding with actor
   runtime IDs. Separate instance IDs may reference one definition and may be
   equipped by separate actors.
2. **Missing, duplicate, and multiply-equipped instances reject atomically.**
   Transition tests assert the exact original `Before` and `After` snapshots
   for missing equip, duplicate add, already-equipped assignment, and equipped
   removal. Save tests reject a missing actor loadout reference and one
   instance assigned to two actors. No rejected operation exposes a partially
   changed inventory, actor equipment snapshot, or wallet.
3. **The root save equipment authority is gone, not dormant.**
   `RuntimeSaveGameSnapshot` has no `Equipment` constructor parameter or
   property; `RuntimeRestoredSession` has no root `Equipment` property; the
   public API baseline removes all three contracts. A reflection regression
   verifies their physical absence and save contract v16.
4. **All four requested surfaces migrated.** Framework transition/profile/save
   tests use instance ownership; DemoHost JSON and Training Annex tests retain
   exact instance IDs through purchase, equip, sale, save, and restore; the
   Godot-shaped contract stores inventory instances plus actor references; and
   persistence tests validate v16 ownership, catalog resolution, missing
   references, multiple assignments, and removal of the root authority.
5. **Scope guard held.** No equipment-ownership policy was introduced.
   `EquipmentSlot`, combat contribution gaps, shop-stock behavior, pricing,
   recovery, and currency behavior remain for O7-R3 through O7-R8.

### O7-R3: Author Equipment Slot Layouts

- **Baseline and commit:** `147265ff..this checkpoint commit`; commit subject
  `runtime: author equipment slot layouts`.
- **Actual destination:** `EquipmentDefinition.SlotId`, inventory and actor
  equipment dictionaries keyed by `ContentId`, the
  `IEquipmentSlotLayoutPolicy` boundary, and the supplied
  `StandardEquipmentSlotLayoutPolicy` with stable `weapon`, `armor`, `boots`,
  and `accessory` IDs. Save contract v17 persists the authored key shape.
- **Changed Framework files:** `Catalog/DefinitionQualifier.cs`,
  `Content/ContentSurfaceDefinitions.cs`,
  `Content/SkillSystemJsonDeserializer.cs`, `PublicAPI.Shipped.txt`, the new
  `Runtime/EquipmentSlotLayouts.cs`, `Runtime/ResourceManagementServices.cs`,
  `Runtime/RuntimeActorSnapshotIntegrity.cs`,
  `Runtime/RuntimeEquipmentProfiles.cs`,
  `Runtime/RuntimePersistenceSnapshots.cs`,
  `Runtime/RuntimeStateSnapshots.cs`, `Serialization/SchemaDtos.cs`,
  `Serialization/SkillSystemDtoMapper.cs`, and
  `Validation/SkillSystemContentValidator.cs`.
- **Changed wire/content files:** the strict Draft 2020-12 set under
  `schemas/content/v9`, all 36 active documents under `content`, the
  ContentValidator and CI schema roots, and all active pack identities. The
  equipment wire property is `slotId`; active packs are version `0.9.0` and
  declare schema v9.
- **Changed host files:** DemoHost save, Training Annex boot/restore/shop
  integration, and Godot save/smoke integration now serialize and route slot
  IDs without enum parsing. Standard DemoHost labels preserve the existing
  Weapon/Armor/Boots/Accessory presentation.
- **Changed tests and tracking:** the new `EquipmentSlotLayoutTests`, expanded
  `EquipmentInstanceOwnershipTests`, resource, persistence, schema, catalog,
  Godot-contract, DemoHost, source-inventory, capability, and documentation
  synchronization coverage, plus current architecture, content-contract,
  terminology, mechanics, quality-gate, and roadmap guidance.
- **Focused tests:** 271 Framework slot-layout, equipment-instance,
  resource-management, persistence, schema, active-content, and Godot-contract
  tests passed; 0 failed and 0 skipped. The dedicated slot-layout class contains
  3 passing policy/key-boundary tests, and the equipment-ownership class
  contains 8 passing instance-authority tests.
- **Full suite:** 1,719 Framework tests, 178 DemoHost tests, and 7
  ContentValidator tests passed: 1,904 total, 0 failed, 0 skipped.
- **Build and content integration:** the nonincremental .NET 8 solution build
  passed with 0 warnings and 0 errors; formatting and `git diff --check`
  passed. The authoring validator loaded 6 packs, 36 documents, and 98
  qualified definitions through schema v9, strict deserialization, semantic
  validation, dependency/registration checks, and catalog construction. All
  five DemoHost modes passed, including scripted Training Annex exit, and the
  real Godot 4.7.1 headless smoke path restored save contract v17 successfully.

#### Explicit O7-R3 Parity-Hazard Evidence

1. **Standard four-slot decisions are unchanged.** The supplied policy exposes
   exactly `weapon`, `armor`, `boots`, and `accessory`. A complete 4-by-4
   slot/profile matrix proves that each existing matching profile is accepted,
   every former mismatch is rejected, unsupported IDs are rejected, and only
   same-ID assignments are valid. All existing standard-policy packs load.
2. **O7-R2 atomic rejection remains intact under `ContentId` keys.** Inventory
   and actor snapshot construction reject one instance ID repeated under two
   authored slot IDs. Missing equip, duplicate add, already-equipped assign,
   multiply-equipped save state, and equipped removal retain identical
   `Before`/`After` snapshots on rejection.
3. **O7-R2 cross-actor collision detection remains intact.** `Equip` still
   checks the target actor and every supplied other-actor equipment snapshot by
   instance ID before constructing the result. Both the standard layout and a
   custom `weapon` to `main_hand` mapping reject a collision atomically.
4. **Save contract advances sequentially to v17.** The preceding current
   contract was v16. `CurrentContractVersion` is now 17, constructor/API/host
   codecs use 17, and validation explicitly rejects v16 alongside the earlier
   unsupported pre-release versions. No version collision or skip occurred.
5. **Compatibility belongs to the selected policy.** Content semantic
   validation, shop-offer resolution, equipment transitions, equipment-profile
   resolution, and save validation all call the injected layout policy. A
   custom policy accepts a layout that the standard policy rejects, including
   transition and save validation. Active source contains no `EquipmentSlot`
   enum or implicit enum/profile comparison; JSON Schema validates `slotId`
   structure while semantic policy owns vocabulary and compatibility.
6. **Scope guard held.** O7-R3 does not resolve granted skills, feed
   armor/boots contributions into combat, or alter pricing, shop stock,
   recovery, currency, or their policy surfaces. Those remain O7-R4 through
   O7-R8 work.

### O7-R4: Complete Equipment Combat Contributions

- **Baseline and commit:** `8dc47f8a..this checkpoint commit`; commit subject
  `runtime: complete equipment combat contributions`.
- **Actual destination:** `RuntimeEquipmentProfileResolver` resolves one
  immutable profile from the actor's currently equipped instance IDs and the
  inventory-owned definitions. `IRuntimeActorEquipmentProfileSource` exposes
  that same live profile to basic attacks, canonical action authorization,
  automated selection, actor/Vessel composition, save validation, and
  aggregate restoration. Armor contributes Defense and Evasion, boots
  contribute Evasion, active granted skills remain live authorization inputs,
  and passive granted skills enter the existing passive lifecycle without
  entering learned or equipped move-list state.
- **Changed Framework files:** `Battle/ProductionCombatRuleset.cs`,
  `Encounters/AutomatedBattleRunner.cs`,
  `Encounters/CatalogBattleActorFactory.cs`,
  `Execution/BattleActionAuthorization.cs`, `Execution/BattleRuntimeState.cs`,
  `PublicAPI.Shipped.txt`, `Runtime/ProgressionPolicies.cs`,
  `Runtime/RuntimeActorCombatProfileComposition.cs`,
  `Runtime/RuntimeEquipmentProfiles.cs`,
  `Runtime/RuntimePersistenceSnapshots.cs`,
  `Runtime/RuntimeSessionRestoration.cs`, and
  `Runtime/RuntimeSkillProgression.cs` under `src/Convergence.Framework`.
- **Changed host files:** Training Annex play state, battle action adapter,
  shared composition support, and persistence controller under
  `samples/Convergence.DemoHost/Hosts/TrainingAnnex` now construct and reuse
  the canonical live equipment-profile source. No presentation behavior or
  Order 7 currency work was added.
- **Changed tests and documentation:** focused Framework coverage in
  `ProductionCombatRulesetTests`, `ProgressionPolicyTests`,
  `ResourceManagementServiceTests`, `BattleActionExecutorTests`,
  `CatalogBattleRuntimeTests`, and `PassiveSkillRuntimeTests`; DemoHost restore
  coverage in `CleanTrainingAnnexPlayHostTests`; current capability and
  documentation matrices; and the active architecture, content-contract,
  ruleset, player-mechanics, developer, technical, and roadmap pages describing
  equipment authority and combat contribution.
- **Focused tests:** 7 Framework equipment-profile/combat/authorization tests
  and 2 DemoHost equipment/restore tests passed: 9 total, 0 failed, 0 skipped.
- **Full suite:** 1,725 Framework tests, 179 DemoHost tests, and 7
  ContentValidator tests passed: 1,911 total, 0 failed, 0 skipped.
- **Build and content integration:** the nonincremental .NET 8 solution build
  passed with 0 warnings and 0 errors; formatting and `git diff --check`
  passed. The authoring validator loaded 6 packs, 36 documents, and 98
  qualified definitions under schema v9. All four noninteractive DemoHost
  modes and scripted Training Annex exit passed.

#### Explicit O7-R4 Parity-Hazard Evidence

1. **No-equipment output is an exact no-op.** `ProductionCombatStats` defaults
   Defense and Evasion to decimal zero. The existing damage denominator adds
   that zero to Vitality; the hit path passes the original evasion-modifier
   collection directly when Evasion is zero. A field-by-field regression proves
   implicit-zero and explicit-zero damage/hit results are equal (50 damage and
   80 final hit chance in the controlled fixture). The pre-change and
   post-change clean battle demo also produced the same ordered damage values:
   7.5, 13, 35.625, 35.625, 13, and 9.250.
2. **Granted-skill availability is live and instance-scoped.** Both assessment
   and execution resolve the actor's current equipment instance IDs through
   `IRuntimeActorEquipmentProfileSource`. A command assessed while equipped is
   rejected without mutation if the granting instance is unequipped before
   execution; re-equipping authorizes it; a second unequip rejects it again.
   Automated selection likewise changes from selected to pass immediately.
3. **Granted skills never become learned skills or consume move-list slots.**
   Active grants are authorization inputs only. Passive grants are tracked as
   equipment provenance in `RuntimeActorState` and merged into the existing
   passive collection, while `RuntimeSkillStateSnapshot.LearnedSkillIds` and
   `.EquippedSkillIds` remain unchanged across equip, unequip, and rapid
   re-equip. Focused tests assert both collections remain empty for the granted
   active skill and exclude the restored granted passive.
4. **Defense and Evasion are additive inputs to the existing formulas.** The
   equipment resolver emits `defense` and `evasion` numeric stat contributions.
   Actor composition adds them to the normal effective-stat block;
   `ProductionCombatRuleset` reads those normal stats into its existing damage
   denominator and hit-policy modifier pipeline. No equipment-specific combat
   formula, threshold, policy, or special-case branch was introduced.
5. **All lifecycle surfaces use the same equipment profile.** Actor creation
   exposes its live profile through `CatalogBattleActor`; Vessel composition,
   equipment changes, manual and automated assessment, execution, save
   validation, and restoration consume the same resolver output. The Training
   Annex save/restore regression proves one equipped armor instance resolves to
   the same Defense (6), Evasion (1), and granted passive before and after
   restoration, while the skill remains absent from the move list.
6. **Scope guard held.** No granted-skill policy, equipment-specific combat
   formula, currency ledger, pricing, shop-stock, or recovery work was added.
   O7-R5 remains unstarted.
