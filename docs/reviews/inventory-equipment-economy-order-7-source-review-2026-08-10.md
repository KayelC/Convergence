# Inventory, Equipment, And Economy Order 7 Source Review And Approved Roadmap

**Date:** 10 August 2026

**Capability:** `inventory_equipment_economy`

**Source baseline:** `21b0a016` (`refactor(encounter-runner): stage 7 - move scheduled round loop`)

**Owner-decision status:** general authority principle and decisions O7-D1 through O7-D8 approved

**Implementation status:** O7-R1 through O7-R9 complete; O7-R10 audiences implemented and reconciled; O7-R10 final verification and O7-R11 pending

> **O7-R9 audit:** The independent pre-implementation wire-integrity audit is
> recorded in
> [inventory-equipment-economy-order-7-r9-wire-integrity-audit-2026-08-13.md](inventory-equipment-economy-order-7-r9-wire-integrity-audit-2026-08-13.md).
> It identifies one reachable resolved-offer authority defect, separates trusted
> host responsibilities from Framework guarantees, and defines the isolated R9
> correction and certification commits.
>
> The independent
> [R9 post-correction review](inventory-equipment-economy-order-7-r9-post-correction-review-2026-08-13.md)
> found no remaining actionable R9 defect and closes only R9. The capability
> remains partial for R10 and R11.

> **O7-R10 audit:** The source-traced audience audit and ordered documentation
> checkpoints are recorded in
> [inventory-equipment-economy-order-7-r10-documentation-audit-2026-08-13.md](inventory-equipment-economy-order-7-r10-documentation-audit-2026-08-13.md).
> No audience is promoted by the audit alone.

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
| O7-D5 | Runtime shop stock is stateful. Buying decrements limited stock. Standard selling does not replenish stock; a supplied policy may choose otherwise. | Policy family plus runtime state | Approved; implemented by O7-R7 |
| O7-D6 | The standard pricing policy uses authored purchase price and configurable resale percentage. Luck-adjusted pricing remains available as an optional supplied policy, not hidden standard behavior. | Policy family | Approved; implemented by O7-R6 |
| O7-D7 | Recovery is generic and resource-ID driven. Supply exactly one `StandardHospitalRecoveryPolicy` for full configured HP/SP recovery, legal ailment cures, and configured temporary-state cleanup. | Policy family, narrowly scoped | Approved; implemented by O7-R8 |
| O7-D8 | Replace the unnamed wallet with a currency ledger keyed by currency `ContentId`. Supply a convenience accessor that succeeds only when exactly one currency exists. | Direct data-model correction | Approved; implemented by O7-R5 |

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

#### O7-R6 Approved Extraction Design

The pricing boundary has two separate authored choices and must not collapse
them into one hidden formula:

1. every shop offer supplies one nonnegative whole authored purchase price;
2. the economy ruleset explicitly selects the default pricing policy and its
   configuration; and
3. a policy-shaped offer may explicitly select a registered pricing factory
   for that offer instead of the economy default.

A fixed price is shorthand for applying the economy ruleset's selected default
policy to the fixed authored purchase price. A policy-shaped price must carry
`purchasePrice` in its parameter object; the remaining parameters configure the
selected pricing factory. Missing, fractional, negative, overflowing, unknown,
or rejected policy configuration produces a typed offer-resolution diagnostic.
There is no fallback to the economy default after an explicit offer policy
fails.

The resolved runtime offer owns one immutable pricing profile containing the
authored purchase price and the bound policy. Menu quotes, transaction
assessment, and transaction execution all read that same profile. The host
must not calculate or reconstruct a second display price from content or actor
state.

The supplied policies are:

- `standard_shop_pricing`: purchase price is exactly the authored purchase
  price. Resale is `authored purchase price * resalePercentage`, with the
  nonnegative decimal result truncated toward zero before conversion to the
  supported integer domain. The default resale percentage is `0.50`; authored
  economy rulesets may configure it explicitly.
- `luck_adjusted_shop_pricing`: preserves the current optional formula exactly.
  Purchase uses `max(0.50, 1.00 - Luck * 0.01)` and resale uses
  `0.50 + Luck * 0.01`; each nonnegative decimal result is truncated toward
  zero. Negative Luck and integer overflow remain rejected.

`standard_economy` requires an explicit `pricingPolicyId` and accepts an
optional `pricingParameters` object. The active catalog-surface reference pack
selects `standard_shop_pricing`; Training Annex selects
`luck_adjusted_shop_pricing` so its existing Luck-sensitive prices and
transactions remain unchanged. Hosts may register another typed pricing
factory without replacing inventory, equipment, currency, shop-transaction,
or recovery services.

This checkpoint does not introduce stock mutation, stock identity, recovery
policy work, a currency policy, presentation rules, or a second transaction
path. O7-R7 remains the sole owner of durable stock changes.

### O7-R7: Make Shop Stock Stateful And Policy-Owned

- give authored offers stable identity suitable for runtime state and saves;
- add immutable runtime stock snapshots for limited offers;
- introduce the stock-policy contract and standard implementation;
- decrement limited stock exactly once on a successful purchase;
- leave stock unchanged on every rejected or rolled-back purchase;
- keep standard sales non-replenishing;
- permit a supplied policy to replenish on sale; and
- commit inventory, currency, and stock as one atomic result.

#### O7-R7 Approved Extraction Design

Shop stock identity is the pair `(shopId, offerId)`. `shopId` identifies the
qualified catalog shop and `offerId` is a required, shop-local authored ID.
The offered item or equipment ID is not used as offer identity: one shop may
sell the same definition through more than one offer, and two shops may reuse
the same local offer ID without colliding. List position is presentation order
only and never enters runtime state or a save.

Content advances from schema v9 to v10 and active packs advance from `0.9.0`
to `0.10.0`. Every offer requires an `id`. A policy-shaped stock definition
requires both `stockPolicyId` and a positive authored `quantity`; its remaining
parameter object configures the selected factory. Unlimited stock omits a
quantity and policy. Fixed limited stock binds the supplied standard stock
policy automatically. Duplicate offer IDs within one shop, malformed policy
configuration, and unresolved stock-policy IDs are rejected before a runtime
offer is produced. There is no fallback after an explicitly selected stock
policy fails.

Each resolved runtime offer carries one immutable stock profile:

- unlimited offers are untracked and never create a quantity entry;
- fixed limited offers carry their authored initial quantity and the bound
  `standard_shop_stock` policy; and
- policy-shaped offers carry their authored initial quantity and one bound
  host-registered stock policy.

`IShopStockPolicy` receives only the operation and current remaining quantity
and returns a typed next-quantity result. `StandardShopStockPolicy` decrements
one unit for a successful purchase, rejects a purchase at zero, and leaves the
quantity unchanged on resale. A custom `IShopStockPolicyFactory` may supply a
different resale transition, including replenishment. Policy exceptions and
invalid returned quantities become typed rejection results; cancellation is
propagated rather than disguised as policy failure.

`RuntimeShopStockSnapshot` is the sole durable stock authority. It stores
immutable entries for limited and policy-controlled offers, keyed by the
composite shop/offer identity. Initial state is built from resolved runtime
offers. Save validation cross-checks every entry against the catalog, rejects
duplicate or negative entries, rejects stock for unlimited or missing offers,
and requires one entry for every limited or policy-controlled offer. Runtime
save contract v18 therefore advances sequentially to v19; Framework restore,
DemoHost JSON, and Godot-owned JSON all carry the same snapshot.

`IShopTransactionService.Buy` and `Sell` receive the current inventory,
currency ledger, and stock snapshot. They calculate immutable candidates and
return all three before/after pairs in one `ShopTransactionResult`. A purchase
commits exactly one stock transition only after pricing, inventory, currency,
and stock all accept. Any rejection, policy failure, or later-stage rollback
returns the original inventory, currency, and stock as every after-state.
Standard sales leave stock unchanged; a custom bound stock policy may return a
replenished candidate that commits atomically with inventory removal and the
currency credit.

Training Annex keeps one stock snapshot for the session, displays its live
quantity, threads it through repeated shop openings, and saves/restores it.
The clean save demo and Godot contract encode the same framework snapshot;
neither host reconstructs remaining stock from authored content after restore.

This checkpoint does not change pricing formulas, add a currency policy, add
recovery behavior, or start O7-R8. It adds no stock presentation framework and
does not make a shop transaction service own mutable live state.

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

#### O7-R8 Approved Extraction Design

Recovery becomes an optional, generic service within the resource-management
bundle. An economy ruleset that omits recovery configuration binds inventory,
equipment, currency, pricing, and stock normally and exposes no recovery
service. An economy ruleset that selects a recovery policy must provide its
configuration explicitly; there is no hidden HP/SP, currency, cost, or cleanup
fallback.

`IRecoveryPolicy` receives one immutable `RuntimeActorSnapshot` and returns a
typed treatment decision. A planned treatment names the actor resource IDs to
restore to their existing maxima, whether removable ailments should be cured,
the temporary-state categories to clear, and the nonnegative integer cost. The
policy does not mutate an actor, debit currency, or publish presentation. A
policy rejection, exception, null result, malformed plan, missing resource, or
numeric overflow is contained as a typed diagnostic before any live state can
change.

`StandardHospitalRecoveryPolicy` is the only supplied implementation. Its
factory accepts these explicit parameters:

- `currencyId`: the exact currency-ledger `ContentId` to debit;
- `resourceCosts`: a nonempty object mapping each resource `ContentId` to its
  nonnegative decimal cost per missing unit;
- `removeAilments`: whether the treatment requests legal ailment cures; and
- `temporaryStateKinds`: the distinct typed categories the treatment requests
  to clear: guard, stat modifiers, charges, shields, affinity overrides,
  affinity Breaks, or other statuses.

The standard cost is the sum of `missing amount * configured unit cost` for
all configured resources, truncated once toward zero after aggregation. The
calculation is checked and rejects a result outside the nonnegative integer
currency domain. A configured resource that is absent from the actor is a
typed rejection, not an ignored treatment component. Zero-cost ailment-only or
temporary-state-only treatment remains valid and preserves the former facility
behavior.

`IRecoveryService` owns assessment and execution. Both paths create a staged
actor candidate, apply the selected plan through canonical runtime operations,
and stage the named currency debit. Assessment publishes no mutation.
Execution re-evaluates from current actor and ledger state, then commits the
live actor only after policy planning, resource restoration, status cleanup,
and currency debit all succeed. Every rejection returns the original actor and
currency snapshots. A host adopts the returned immutable currency ledger only
from an applied execution result.

Ailment treatment uses `StatusRemovalCause.RecoveryEvent`; therefore an
ailment is removed only when its authored `StatusRemovalProfileDefinition`
permits that cause. Selected non-modifier temporary states use the same
removal cause and retain protected entries. Stat modifiers continue through
their selected `IStatModifierPolicyService`; `StatModifierCleanupScope` gains a
typed recovery-event boundary so the recovery layer does not rewrite policy
state directly. Guard has no authored lifetime and is cleared only when the
selected recovery policy explicitly requests the guard category.

Training Annex explicitly binds the standard hospital policy to its qualified
Credits currency, restores `hp` at one Credit per missing unit and `sp` at five
Credits per missing unit, enables legal ailment cures, and selects every
currently supported temporary category. This reproduces the former full
HP/SP and `missing HP + missing SP * 5` behavior while replacing the lossy
patient booleans with the canonical actor state. Its presentation continues to
say Recovery Facility; that label is host-owned and does not rename the generic
Framework contract.

This checkpoint does not add another sample recovery policy, treatment-type
content, new resource definitions, presentation rules, pricing or stock
behavior, or a second formula for status removal. The runtime save contract
remains v19 and content remains schema v10 because actor resources, battle
status, and typed currency already have one durable shape. Training Annex may
advance its pack patch version when its authored economy parameters change;
that is content revision metadata, not a schema or save-contract change.

#### O7-R8 Verification Contract

Focused tests must prove generic non-HP/SP resource IDs, exact standard HP/SP
cost parity, fractional aggregation and overflow handling, free ailment-only
treatment, legal/protected ailment distinction, each temporary-state category,
stat-modifier policy delegation, missing resource/currency handling, malformed
and faulting custom policy containment, assessment purity, execution freshness,
and actor/currency rollback on every rejection. Ruleset tests must prove
optional omission, explicit standard binding, malformed parameter diagnostics,
and custom factory registration without adding a second supplied policy.

DemoHost tests must prove the Recovery Facility still displays the same quote,
restores the same HP/SP values, spends the same Credits, and records the actual
post-treatment ailment and temporary state rather than policy-intent booleans.
Godot-contract evidence must bind or exercise the same generic recovery service
without introducing Godot types into Framework. The full solution, active
content validation, all DemoHost modes, real Godot headless smoke, public API
baseline, formatting, coverage, forbidden-reference, and documentation-link
gates remain mandatory before O7-R8 can be recorded complete.

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

### O7-R5: Introduce Typed Currency Ledger Authority

- **Baseline and commit:** `0d7ec4c6..this checkpoint commit`; commit subject
  `runtime: add typed currency ledger`.
- **Actual destination:** `RuntimeCurrencyLedgerSnapshot` owns immutable
  balances keyed by qualified currency `ContentId`;
  `IEconomyTransactionService` exposes only explicit
  `(ledger, currencyId, amount)` credit/debit paths; shop, recovery, Compendium,
  reward, and negotiation transactions carry that ID through immutable results;
  save contract v18 and both sample codecs persist the complete ledger.
- **Changed Framework files:** `Runtime/ResourceManagementServices.cs`,
  `Fusion/CompendiumRuntimeServices.cs`,
  `Runtime/RuntimePersistenceSnapshots.cs`,
  `Runtime/RuntimeSessionRestoration.cs`, and `PublicAPI.Shipped.txt`.
- **Changed host files:** `CleanSaveDemoHost.cs`,
  `CleanTrainingAnnexDemoHost.cs`, the Training Annex play, acquisition,
  reward, Compendium, negotiation, persistence, recovery, shop, and shared
  support controllers, plus `GodotSaveCodec.cs` and
  `ConvergenceSmokeRoot.cs`.
- **Changed tests and documentation:** Framework resource, Compendium,
  persistence, ruleset, equipment, Battle Knowledge, enum-boundary,
  neutrality, and Godot-contract tests; DemoHost save and Training Annex tests;
  the capability/documentation matrices; and current architecture, save,
  mechanics, Godot, API, terminology, roadmap, and supporting lifecycle guides.
- **Focused tests:** 147 Framework currency/resource/Compendium/persistence/
  Godot/documentation tests and 128 DemoHost save/Training Annex tests passed:
  275 total, 0 failed, 0 skipped.
- **Full suite:** 1,732 Framework tests, 179 DemoHost tests, and 7
  ContentValidator tests passed: 1,918 total, 0 failed, 0 skipped.
- **Single-currency comparison:** the complete pre-change and post-change
  64-line Training Annex demo outputs are byte-for-byte identical, both with
  SHA-256
  `960062028CDE0FCAA6BA80C7B6B02CD3609B5E245A6F2B7BB5C4094573353BE3`.
  Both award 1 EXP and 14 Credits and validate the resulting save with zero
  diagnostics. The save demo advances from v17 to v18 and changes only the
  expected host-owned JSON ledger shape; actor, inventory, dungeon, validation,
  restore, and terminal outcomes remain unchanged.

#### Explicit O7-R5 Parity-Hazard Evidence

1. **Single-currency behavior remains identical.** Training Annex constructs
   exactly one canonical `credits` entry and all consumers explicitly select
   it. The byte-identical demo comparison above preserves balances, the
   14-Credit reward, and every transaction/outcome event. Focused shop,
   recovery, negotiation, Compendium, reward, save, and host tests preserve
   their pre-migration applied/rejected outcomes.
2. **The convenience accessor rejects ambiguity explicitly.**
   `GetSingleCurrency()` returns the ID and balance only when the ledger has
   exactly one entry. Empty and multi-currency ledgers throw
   `RuntimeCurrencyLedgerException` with distinct `EmptyCurrencyLedger` and
   `AmbiguousCurrencyLedger` diagnostics; neither path selects a default.
3. **Every transaction names its currency.** The public economy interface has
   only three-argument credit/debit methods taking ledger, `ContentId`, and
   amount. A reflection regression pins that shape, the public API baseline
   removes the old implicit contracts, and active-source search finds no
   `RuntimeWalletSnapshot`, `WalletTransactionResult`, or two-argument
   compatibility path.
4. **Invalid ledger and transaction states have distinct typed diagnostics.**
   Duplicate IDs reject with `DuplicateCurrencyId`, negative balances with
   `NegativeCurrencyBalance`, checked credit overflow with `NumericOverflow`,
   and a transaction naming an absent entry with `CurrencyNotFound`. Each test
   also proves the original immutable ledger remains unchanged.
5. **Save contract advances sequentially to v18.** O7-R4 left v17 current.
   `CurrentContractVersion` is now 18, save construction, aggregate restoration,
   DemoHost JSON, and Godot JSON use the ledger shape, and validation explicitly
   rejects v17 alongside older unsupported versions. There is no version skip
   or collision.

#### O7-R5 Consumer-Migration Checklist

- **Shops:** `Buy` and `Sell` receive the currency ID and return before/after
  ledgers; inventory changes remain atomic when payment rejects.
- **Recovery:** restoration names its currency and returns the same ledger on
  no-op, insufficient-funds, missing-currency, and other rejected paths.
- **Compendium/economy:** recall requests/results carry the selected currency;
  battle rewards and negotiation donations use explicit credit/debit calls.
- **Saves:** the aggregate root stores one ledger authority under save v18;
  validation and restoration preserve every entry.
- **DemoHost:** Training Annex uses one canonical Credits ID across shops,
  recovery, Compendium, negotiation, rewards, summaries, and save capture;
  Clean Save serializes currency IDs and balances rather than a scalar.
- **Godot:** the reference codec serializes/deserializes ordered currency
  entries, smoke evidence restores Credits, and the Godot-shaped contract test
  round-trips two distinct currencies without placing Godot or serializer types
  in Framework.
- **Scope guard held:** no currency policy, pricing-policy, stock-policy, or
  recovery-policy work was introduced. O7-R6 remains pending.

### O7-R6: Bind Explicit Pricing Policies

- **Baseline and commit:** `44538eab..this checkpoint commit`; commit subject
  `runtime: bind explicit shop pricing policies`.
- **Actual destination:** `IShopPricingPolicy` owns runtime purchase/resale
  calculation; `ShopPricingPolicyFactoryRegistry` resolves supplied policy IDs;
  `RuntimeShopPricingProfile` carries one authored purchase price and bound
  policy through offer resolution, assessment, display, and execution; and the
  `standard_economy` ruleset factory explicitly selects the default policy and
  its parameters.
- **Changed Framework files:** the new
  `Runtime/ShopPricingPolicies.cs`, `Runtime/ResourceManagementServices.cs`,
  `Runtime/RuntimeRulesetBindings.cs`,
  `Runtime/RuntimeRulesetPolicyFactories.cs`, and `PublicAPI.Shipped.txt`.
- **Changed content and host files:** both active economy ruleset documents and
  the active registration fixture; DemoHost clean-save/Training Annex binding
  and shop integration; and the Godot smoke registration surface. Training
  Annex explicitly selects `luck_adjusted_shop_pricing`; the catalog-surface
  reference pack explicitly selects `standard_shop_pricing` with a `0.50`
  resale percentage.
- **Changed tests and documentation:** the new `ShopPricingPolicyTests`,
  resource-management and ruleset-binding coverage, active-content/catalog/
  persistence registration fixtures, DemoHost shop regressions, source and
  capability/documentation inventories, and current architecture, gameplay,
  economy, ruleset, and roadmap guidance.
- **Focused tests:** 89 Framework pricing/resource/ruleset tests and 5 DemoHost
  shop tests passed: 94 total, 0 failed, 0 skipped. The dedicated
  `ShopPricingPolicyTests` class contributes 18 passing policy/factory/runtime
  boundary tests.
- **Full suite:** 1,750 Framework tests, 179 DemoHost tests, and 7
  ContentValidator tests passed: 1,936 total, 0 failed, 0 skipped.
- **Build and integration:** strict nonincremental Framework, solution, and
  trim-aware Release Framework builds passed with 0 warnings and 0 errors;
  formatting and `git diff --check` passed. The authoring validator loaded 6
  packs, 36 documents, and 98 qualified definitions. All four noninteractive
  DemoHost modes and scripted Training Annex exit passed. The real Godot 4.7.1
  smoke consumer emitted `CONVERGENCE_GODOT_SMOKE_OK`, restored save v18, and
  exited 0.
- **Contract versions:** pricing profiles are transient runtime authority, so
  this checkpoint deliberately retains content schema v9, active pack version
  `0.9.0`, and runtime save contract v18. The existing ruleset parameter-object
  schema already expresses the newly required policy selection; no saved-state
  or wire-document shape was added.

#### Explicit O7-R6 Authority Evidence

1. **Standard pricing is exact and configurable.** `standard_shop_pricing`
   returns the authored purchase price unchanged. Resale multiplies that price
   by the configured nonnegative percentage, defaults to `0.50`, and truncates
   the nonnegative decimal result toward zero. Focused tests pin exact purchase,
   default/configured resale, fractional truncation, and numeric overflow.
2. **Luck pricing is optional and behavior-compatible.** The old formula exists
   only in `luck_adjusted_shop_pricing`: purchase uses
   `max(0.50, 1 - Luck * 0.01)` and resale uses `0.50 + Luck * 0.01`, both with
   truncation toward zero. Training Annex explicitly selects it, preserving the
   established 47-Credit Annex Tonic purchase, 28-Credit resale, 112-Credit
   Practice Blade purchase, and 84-Credit Padded Jacket purchase.
3. **Authored policy selection is typed and has no fallback.** Fixed-price
   offers use the economy ruleset's bound default. Policy-shaped offers require
   one whole nonnegative `purchasePrice`, remove that operand before binding the
   remaining factory parameters, and resolve the explicitly named registered
   factory. Missing, malformed, unknown, throwing, mismatched, and silently
   rejecting factories produce typed diagnostics; an explicit offer failure
   never falls back to the economy default.
4. **One pricing profile is authoritative.** `RuntimeShopOfferResolver` creates
   the immutable profile once. `ShopTransactionService` uses that profile for
   quote, assessment, and execution, while the Training Annex menus display the
   transaction assessment's `Price`; no host-side price formula or reconstruction
   remains.
5. **Input and transaction safety remain intact.** Negative authored prices,
   negative Luck, fractional/overflowing policy purchase prices, calculation
   overflow, and custom runtime-policy failure are rejected before inventory or
   currency changes. Affordability and inventory rejection retain equal
   before/after snapshots; a later currency rejection does not expose the
   tentative immutable inventory result. Cancellation is propagated rather than
   disguised as pricing failure.
6. **The standard economy factory is extensible without becoming implicit.** It
   requires `pricingPolicyId`, accepts an optional `pricingParameters` object,
   maps pricing diagnostics to precise ruleset parameter paths, and accepts
   host-registered typed factories through `CreateStandard(shopPricing)`.
7. **Scope guard held.** No durable shop-stock identity or mutation, sale
   replenishment, recovery policy, currency policy, save shape, schema family,
   or presentation framework was introduced. O7-R7 remains the sole owner of
   stateful stock behavior, and O7-R8 remains the sole owner of recovery policy.

### O7-R7: Make Shop Stock Stateful And Policy-Owned

- **Baseline and commit:** `455f64b4..this checkpoint commit`; commit subject
  `runtime: persist policy-owned shop stock`.
- **Actual destination:** authored `ShopOfferDefinition.Id` values combine with
  each qualified shop ID in `RuntimeShopOfferIdentity`; resolved offers carry
  one immutable `RuntimeShopStockProfile`; `RuntimeShopStockSnapshot` is the
  sole durable quantity authority; and `ShopTransactionService` returns one
  atomic inventory/currency/stock result.
- **Changed Framework files:** the new `Runtime/ShopStockPolicies.cs`, plus
  content definitions and qualification, schema DTO/mapping/validation,
  resource-management transactions, standard economy ruleset factories,
  save validation and aggregate restore, and the deliberate public API
  baseline update.
- **Changed wire/content files:** strict Draft 2020-12 schemas under
  `schemas/content/v10`; all 36 active documents now declare schema v10; all
  6 active manifests use pack version `0.10.0` with exact revised dependencies;
  every shop offer has a required local ID; policy stock has a positive
  quantity; and active host registrations include `standard_shop_stock`.
- **Changed host files:** Clean Save and the Godot reference codec serialize
  composite offer identity plus remaining quantity. Training Annex creates one
  session stock snapshot, selects by offer ID rather than offered content ID,
  commits transaction stock results, displays remaining quantity, and restores
  saved stock without rebuilding it from authored defaults.
- **Changed tests and documentation:** the new `ShopStockPolicyTests`, expanded
  resource, ruleset, persistence, schema, catalog, Godot-contract, Clean Save,
  and Training Annex coverage; source/capability/documentation inventories;
  and current architecture, gameplay, content-contract, ruleset, save, Godot,
  mechanics, quality-gate, and roadmap guidance.
- **Focused tests:** 336 Framework stock/resource/ruleset/persistence/schema/
  catalog/Godot tests and 131 DemoHost save/Training Annex tests passed: 467
  total, 0 failed, 0 skipped. The dedicated `ShopStockPolicyTests` class
  contributes 9 policy, identity, transaction, binding, and save-boundary tests.
- **Full suite:** 1,768 Framework tests, 182 DemoHost tests, and 7
  ContentValidator tests passed: 1,957 total, 0 failed, 0 skipped.
- **Build and integration:** strict nonincremental Release solution and
  trim-aware Framework builds passed with 0 warnings and 0 errors; formatting
  and `git diff --check` passed. The authoring validator loaded 6 packs, 36
  documents, and 98 qualified definitions. All four noninteractive DemoHost
  modes and scripted Training Annex exit passed. The real Godot 4.7.1 headless
  consumer emitted `CONVERGENCE_GODOT_SMOKE_OK`, restored 3 actors, 250 Credits,
  and one shop-stock entry under save contract v19, then exited 0.
- **Contract versions:** this deliberate wire/state change advances content
  schema v9 to v10, active packs `0.9.0` to `0.10.0`, and runtime save contract
  v18 sequentially to v19. No compatibility alias, fallback, or version skip
  preserves the retired copied-stock shape.
- **Fresh review corrections before commit:** the source review removed ignored
  `parameters` from unlimited/fixed v10 stock shapes, added mapper-level defense
  for schema-bypassing callers, rejected forged durable entries for unlimited
  runtime offers, and corrected the v10 shared-schema title. Focused regressions
  reproduce each corrected path; no additional realistic R7 defect remained.

#### Explicit O7-R7 Authority Evidence

1. **Stable composite offer identity.** Stock is keyed by qualified `shopId`
   plus required shop-local `offerId`; offered content ID and list position are
   never stock identity. Tests prove the same local ID in different shops and
   repeated offers of the same content do not collide, while a true duplicate
   composite identity is rejected.
2. **Purchase decrement and rejection behavior.** The standard policy decrements
   one unit exactly once for each committed purchase and rejects zero stock.
   Insufficient currency, full inventory, missing/duplicate/negative stock,
   unexpected stock on an unlimited offer, and throwing/null policy results all
   return the original inventory, currency ledger, and stock snapshot.
3. **Resale behavior is explicitly policy-owned.** `StandardShopStockPolicy`
   leaves remaining quantity unchanged on resale. The focused custom policy
   proves a host-registered alternative can replenish one unit without changing
   the transaction service or introducing a hidden resale rule.
4. **Three-authority atomicity.** Pricing, stock, inventory, and currency each
   produce immutable candidates. Only the applied `ShopTransactionResult`
   exposes all three after-states; every rejection deliberately reports equal
   before/after authorities, so a tentative candidate cannot leak as committed
   state.
5. **Save and restore use one stock authority.** Save contract v19 contains
   `RuntimeShopStockSnapshot`; validation cross-checks every entry with catalog
   shop/offer definitions and requires exactly one entry per tracked offer.
   Framework aggregate restore, DemoHost JSON, the Godot-shaped contract test,
   and the real Godot smoke path all preserve exact remaining quantities.
6. **Scope guard held.** No recovery policy or O7-R8 behavior, currency policy,
   pricing formula, mutable shop repository, stock presentation framework, or
   second transaction path was introduced. Order 7 remains open for O7-R8
   through O7-R11.

### O7-R8: Generalize Recovery Through One Supplied Policy

- **Baseline and commit:** `bf30fb51..this checkpoint commit`; commit subject
  `runtime: generalize recovery policy`.
- **Actual destination:** the new `Runtime/RecoveryPolicies.cs` owns immutable
  policy planning, typed factory binding, assessment, staged actor cleanup,
  explicit named-currency debit, and atomic execution. The HP/SP-specific
  patient DTO and hospital service were removed from
  `Runtime/ResourceManagementServices.cs` rather than retained as a second
  authority or compatibility path.
- **Changed Framework files:** the new recovery contract and service, the
  resource-management ruleset bundle and standard economy factory, the typed
  `RecoveryEvent` stat-modifier cleanup scope, the public API baseline, and the
  framework source inventory.
- **Changed content and host files:** Training Annex explicitly configures the
  supplied standard hospital policy, qualified Credits currency, HP/SP unit
  costs, legal ailment treatment, and all supported temporary-state categories.
  DemoHost consumes the bound generic service without reconstructing a patient
  DTO or applying cleanup itself. The real Godot reference consumer executes
  recovery, adopts its returned ledger, saves that ledger, and restores it.
- **Changed tests and documentation:** the new `RecoveryPolicyTests`, expanded
  stat-modifier, ruleset-binding, active-content, Godot-contract, and DemoHost
  recovery coverage; capability/documentation/source inventories; and current
  architecture, gameplay, economy, ruleset, content, Godot, and roadmap
  guidance.
- **Focused tests:** 185 Framework recovery/stat-modifier/ruleset/content/Godot
  tests and 117 DemoHost Training Annex tests passed: 302 total, 0 failed, 0
  skipped. The dedicated `RecoveryPolicyTests` class contributes 13 generic
  planning, cleanup, rollback, factory, cancellation, and no-op-boundary tests.
- **Full suite:** 1,786 Framework tests, 182 DemoHost tests, and 7
  ContentValidator tests passed: 1,975 total, 0 failed, 0 skipped.
- **Build and integration:** strict nonincremental Release Framework and
  solution builds, the Debug Godot sample build, API/XML analysis, formatting,
  and `git diff --check` passed with 0 warnings and 0 errors. The authoring
  validator loaded 6 packs, 36 documents, and 98 qualified definitions. All
  four noninteractive DemoHost modes and scripted Training Annex exit passed.
  The real Godot 4.7.1 headless consumer emitted
  `GODOT_RECOVERY_OK cost=45 credits=205 guard=false`, saved and restored the
  adopted 205-Credit ledger, emitted `CONVERGENCE_GODOT_SMOKE_OK`, and exited
  0. Godot's known Windows root-certificate-store warning remained nonfatal.
- **Coverage:** the final Framework coverage gate passed at 90.19% lines and
  76.67% branches against required minima of 90% and 70%.
- **Contract versions:** recovery reuses existing typed actor resources,
  battle-status state, currency ledger, and ruleset parameter objects. Runtime
  save contract remains v19 and content schema remains v10. Training Annex
  advances only its authored pack patch from `0.10.0` to `0.10.1`; all active
  content still validates under schema v10.

#### Explicit O7-R8 Authority And Safety Evidence

1. **Recovery is generic, optional, and explicitly bound.** An economy ruleset
   without recovery parameters exposes `Recovery = null`. Selecting recovery
   requires paired `recoveryPolicyId` and `recoveryParameters`; missing,
   unknown, malformed, throwing, mismatched, and silently rejecting factory
   paths return typed diagnostics. A host-registered factory is accepted, while
   only `StandardHospitalRecoveryPolicy` is supplied by Framework.
2. **Configured resources and costs are authoritative.** The standard policy
   accepts arbitrary resource `ContentId` values and calculates one checked
   aggregate before truncation. Focused tests pin generic Stamina/Focus input,
   fractional aggregation, the established `missing HP + missing SP * 5`
   120-Credit quote, absent resources, duplicate normalized resource IDs, and
   decimal/integer overflow. No Framework recovery path names HP, SP, or
   Credits implicitly.
3. **Canonical cleanup boundaries remain authoritative.** Ailments and
   non-modifier temporary statuses are removed only when their authored
   lifetime permits `StatusRemovalCause.RecoveryEvent`. Protected state remains.
   Persistent staged, timed contribution, and timed exclusive modifier policies
   each own `StatModifierCleanupScope.RecoveryEvent`; nonempty modifier state
   requires its matching service, while an empty modifier snapshot is a true
   no-op and does not create a false dependency.
4. **Assessment, execution, and rollback are atomic.** Assessment operates on a
   staged clone and cannot mutate the live actor or caller-owned immutable
   ledger. Execution re-plans from current state, stages resource/status changes
   and the named-currency debit, and commits the actor only after every step
   accepts. Insufficient or missing currency, protected/no-op state, missing
   resources, policy rejection/fault/null, modifier-policy mismatch, and staged
   mutation failure all return equal before/after authorities. Cancellation is
   rethrown rather than disguised as a gameplay rejection.
5. **Active hosts consume one result authority.** Training Annex presents the
   typed assessment, executes the same service, adopts currency only from an
   applied result, and derives evidence from actual before/after actor snapshots.
   Godot-shaped tests prove no scene/serializer type enters Framework. The real
   Godot sample restores all configured resources, clears guard, checks the
   returned cost instead of duplicating its formula, adopts the resulting
   ledger, and round-trips that exact balance through host-owned JSON.
6. **Retired and out-of-scope paths remain absent.** Active-source search finds
   no `RuntimeHospitalPatientSnapshot`, `HospitalRestorationResult`,
   `IHospitalRestorationService`, or `HospitalRestorationService`. No second
   supplied recovery policy, treatment content family, presentation framework,
   pricing/stock change, schema revision, save revision, or O7-R9 certification
   work was introduced. Order 7 remains open for O7-R9 through O7-R11.

### O7-R9: Certify Cross-System And Wire Integrity

- **Baseline and commits:** `a9e8eef9..c1eafb52`; audit commit
  `69dbb3d4` (`docs: define order 7 wire integrity certification`), authority
  correction commit `4be530f9` (`runtime: seal resolved shop offer authority`),
  and this cross-system certification commit.
- **Actual destination:** `RuntimeShopOfferResolver` remains the only creator of
  complete runtime offers; resolved offer members are externally read-only;
  save validation and aggregate restoration certify equipment ownership,
  authored slots, typed currencies, and tracked stock together; transaction
  regressions certify equal immutable before-state on rejection or cancellation.
- **Changed Framework files:** `Runtime/ShopPricingPolicies.cs`, its resource
  resolver call site, and the deliberate public API baseline. The resolver now
  returns a typed `UnsupportedContentKind` diagnostic and malformed item versus
  equipment profiles cannot be constructed inside the Framework boundary.
- **Changed tests:** resource-management offer-authority regressions, aggregate
  persistence/restore certification, shop rejection and cancellation
  certification, and recovery cancellation certification. Framework, DemoHost,
  ContentValidator, and Godot integration surfaces were all exercised without
  changing host presentation or wire formats.
- **Focused tests:** 152 Framework equipment/slot/persistence/pricing/stock/
  recovery/resource/Godot-contract tests and 133 DemoHost save/Training Annex
  tests passed: 285 total, 0 failed, 0 skipped.
- **Full suite:** 1,789 Framework tests, 182 DemoHost tests, and 7
  ContentValidator tests passed: 1,978 total, 0 failed, 0 skipped.
- **Build and integration:** strict nonincremental Release solution and Debug
  Godot sample builds passed with 0 warnings and 0 errors; formatting and
  `git diff --check` passed. The authoring validator loaded all 6 active packs,
  36 documents, and 98 qualified definitions. All four noninteractive DemoHost
  modes and scripted Training Annex exit passed. The official Godot 4.7.1
  headless sample emitted `CONVERGENCE_GODOT_SMOKE_OK`, restored save v19 with
  3 actors, 205 Credits, and one stock entry, and exited 0.
- **Coverage:** Framework coverage passed at 90.19% lines and 76.71% branches
  against required minima of 90% and 70%.
- **Contract versions:** this checkpoint tightens transient runtime-offer API
  authority and adds certification evidence only. Runtime save contract remains
  v19, content schema remains v10, and active pack versions remain unchanged.

#### Explicit O7-R9 Cross-System And Wire Evidence

1. **Every active content pack is valid.** Independent schema, deserialization,
   semantic, dependency, registration, and catalog construction checks passed
   for all 6 active packs, 36 documents, and 98 qualified definitions under
   schema v10.
2. **The saved aggregate has one coherent authority graph.** Aggregate restore
   preserves the exact immutable inventory, currency-ledger, and shop-stock
   values; the restored actor references the inventory-owned equipment
   instance under its authored slot; the expected Credits balance and tracked
   offer quantity survive together. An equipment instance ID colliding with an
   actor ID is rejected before any actor factory call or partial session is
   exposed.
3. **Rejected and cancelled operations preserve every before-state.** Shop
   stock tests prove missing currency and policy cancellation leave inventory,
   currency, and stock unchanged. Recovery cancellation leaves both the live
   actor and supplied currency ledger unchanged. Cancellation remains an
   `OperationCanceledException`, not a gameplay rejection.
4. **Stale or forged references cannot commit.** Duplicate, missing, equipped,
   multiply assigned, and actor-colliding equipment instance IDs are rejected
   by transition or aggregate boundaries. A resolved shop offer has no public
   constructor or writable authority member, undefined content kinds resolve to
   a typed diagnostic, and contradictory item/equipment shapes reject before a
   transaction can consume them. A transaction naming a currency absent from
   the ledger returns typed `CurrencyNotFound` with equal before/after state.
5. **All host surfaces consume the same contracts.** Clean Save, Training Annex
   demo/play tests, the Godot-shaped contract tests, and the real Godot sample
   all preserve instance-owned equipment, authored slots, named currency, and
   tracked stock. Hosts serialize and adopt immutable results; no host-specific
   economy authority enters Framework.
6. **The public API change is deliberate and narrow.** Only resolved-offer
   construction and `init` mutation are removed; the typed unsupported-kind
   diagnostic is added. Pricing, stock, recovery, currency, save, content, and
   host contracts otherwise retain their post-R8 shapes.
7. **Trusted-host and scope boundaries remain explicit.** Hosts still allocate
   globally fresh runtime IDs, adopt successful immutable results, and rebuild
   transient offers after replacing a catalog. R9 does not add an authenticity
   token, mutable repository, hot-reload protocol, new gameplay policy, schema
   change, save change, or R10 audience-document promotion.

#### Fresh R9 Review

The post-correction review reread the current transaction, restore, host, API,
and documentation paths without accepting the pre-implementation audit as
proof. It found no remaining High, Medium, or Low actionable R9 defect. R9 is
complete; O7-R10 and O7-R11 remain open, and the capability remains `partial`.
