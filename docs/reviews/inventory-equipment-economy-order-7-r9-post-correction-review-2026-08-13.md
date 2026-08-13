# Inventory, Equipment, And Economy Order 7 R9 Post-Correction Review

**Date:** 2026-08-13

**Reviewed range:** `a9e8eef9..c1eafb52`

**Scope:** O7-R9 wire and cross-system integrity only

**Method:** fresh source, test, host, public-API, content-validation, and runtime
trace. The earlier R9 audit was not treated as implementation proof.

## Result

No remaining High, Medium, or Low actionable R9 defect was found.

The original reachable defect is corrected: a host can no longer construct a
resolved shop offer or rewrite its identity, content, pricing, stock, slot, or
stack-limit members after catalog-backed resolution. Undefined authored content
kinds and contradictory internal profiles are rejected before transaction
execution.

R9 is ready to close. Order 7 is not ready to close: O7-R10 three-audience
documentation and O7-R11 independent adversarial closure remain separate open
checkpoints.

## Source Areas Read

- inventory, equipment, currency, shop-offer, and shop-transaction contracts in
  `Runtime/ResourceManagementServices.cs`;
- pricing and stock policy binding and execution in
  `Runtime/ShopPricingPolicies.cs` and `Runtime/ShopStockPolicies.cs`;
- generic recovery planning, staging, commit, and rollback in
  `Runtime/RecoveryPolicies.cs`;
- equipment-profile resolution in `Runtime/RuntimeEquipmentProfiles.cs`;
- aggregate save validation in `Runtime/RuntimePersistenceSnapshots.cs`;
- dependency-ordered aggregate restoration in
  `Runtime/RuntimeSessionRestoration.cs`;
- Training Annex shop/recovery adoption, Clean Save serialization, Godot-shaped
  contract evidence, and the real Godot smoke consumer; and
- the deliberate public API baseline and all R9-focused regressions.

## Independent Invariant Trace

### Resolved Offer Authority

`RuntimeShopOfferSnapshot` is sealed, has no public constructor, and exposes no
public setter or init-only authority property. Standard resolution validates
offer identity, pricing, stock, content kind, catalog definition, slot profile,
and item/equipment shape before constructing the offer. Transaction services
receive that complete object rather than separate host-reconstructed values.

The remaining public record clone and deconstruction operations copy or expose
the same read-only values; neither provides a write path. Pricing and stock
variation remain available through their approved policy/factory boundaries.

### Equipment And Aggregate Identity

Inventory construction and transitions reject duplicate equipment instance
IDs. Equip rejects missing ownership and assignment to the same or another
actor. Save validation independently rejects duplicate ownership, missing or
multiply assigned instances, slot incompatibility, missing definitions, and
equipment IDs colliding with actor IDs. Aggregate restore runs this validation
before resolving profiles or invoking the actor factory, and exposes no session
when validation fails.

### Transaction Atomicity

Shop purchase and resale calculate immutable stock, inventory, and currency
candidates. An applied result is the only result carrying all candidate
after-states. Pricing, stock, inventory, or currency rejection returns every
original authority. Policy cancellation propagates before a result is adopted.

Recovery plans from an immutable actor snapshot, stages all actor changes in a
transaction clone, calculates a named-currency candidate, and commits the live
actor only after every step succeeds. Policy cancellation and every typed
rejection expose the original live actor and ledger.

### Save And Host Wires

Save contract v19 has one inventory owner for equipment instances, actor-local
loadout references by authored slot, a typed currency ledger, and durable stock
by `(shopId, offerId)`. Aggregate restore preserves those values together.
Clean Save and the Godot host own serialization; Framework exposes no serializer
or scene type. Training Annex adopts shop inventory, currency, and stock only
from an applied transaction and adopts recovery currency only from an applied
execution result.

All six active packs and all 36 documents validate under schema v10. R9 changes
neither persisted shape nor content schema, so save v19 and schema v10 correctly
remain current.

## Challenged Claims And Residual Boundaries

The review deliberately does not interpret immutable objects as a security
sandbox for trusted .NET code:

- the host must allocate runtime IDs that are globally fresh across its live
  session; the modular inventory service does not receive the actor aggregate;
- the host must adopt applied immutable results and may always discard or
  replace its own state deliberately;
- host-supplied policy and service implementations must obey their documented
  result contracts and side-effect-free planning requirements;
- concurrent command serialization and content-catalog replacement remain host
  lifecycle responsibilities; a replaced catalog requires newly resolved
  transient offers and save validation before continued use; and
- a presentation sink failure terminates the sample interaction; DemoHost does
  not claim a durable interactive transaction journal.

These are explicit integration responsibilities, not reachable mutations or
fallbacks inside the standard Framework path. Turning them into authentication,
global repositories, concurrency control, hot reload, or presentation recovery
would expand R9 beyond its approved authority boundary.

## Documentation Review

Current architecture, gameplay, roadmap, capability-matrix, and mechanics text
accurately distinguish:

- R9's implemented cross-system certification;
- `inventory_equipment_economy` remaining `partial`;
- the mechanics page remaining `existing_unreviewed`; and
- R10 audience completion and R11 independent closure remaining open.

No active document was found claiming that R9 completed all Order 7
documentation or closed the capability.

## Verification Reproduced

- strict Release solution build: 0 warnings, 0 errors;
- focused Framework equipment/slot/persistence/pricing/stock/recovery/resource/
  Godot-contract tests: 152 passed, 0 failed, 0 skipped;
- focused DemoHost save and Training Annex tests: 133 passed, 0 failed, 0
  skipped;
- complete solution: 1,789 Framework + 182 DemoHost + 7 ContentValidator =
  1,978 passed, 0 failed, 0 skipped;
- active content: 6 packs, 36 documents, 98 qualified definitions;
- all four noninteractive DemoHost modes and scripted Training Annex exit:
  passed;
- official Godot 4.7.1 headless smoke: `CONVERGENCE_GODOT_SMOKE_OK`, exit 0;
- Framework coverage: 90.19% lines, 76.71% branches; and
- repository formatting gate and `git diff --check`: passed.

## Closure Decision

O7-R9 is **complete**. The capability remains `partial`; proceed next with
O7-R10 only when explicitly instructed.
