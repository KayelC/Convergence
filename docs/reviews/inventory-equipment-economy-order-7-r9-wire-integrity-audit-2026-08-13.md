# Inventory, Equipment, And Economy Order 7 R9 Wire-Integrity Audit

**Date:** 2026-08-13

**Scope:** O7-R9 only

**Authority:** [Order 7 source review](inventory-equipment-economy-order-7-source-review-2026-08-10.md)

**Starting commit:** `a9e8eef9` (`runtime: generalize recovery policy`)

**Starting verification:** 1,786 Framework tests, 182 DemoHost tests, and 7
ContentValidator tests passed in Release: 1,975 total, 0 failed, 0 skipped.

## Purpose

O7-R9 is a certification checkpoint, not a new mechanic. This audit rereads the
current post-R8 source before adding certification evidence. It asks whether the
approved equipment-instance, authored-slot, currency-ledger, pricing, stock, and
recovery authorities remain coherent when they meet at public and host wire
boundaries.

The audit applies the repository review standard: a defect must identify an
intended invariant, a realistic reachable path, a concrete consequence, and
reproducible evidence. A trusted host deliberately replacing Framework state is
not presented as a security vulnerability.

## Source-Traced Authority Map

| Concern | Current authority | Current boundary result |
|---|---|---|
| Equipment ownership | `RuntimeInventorySnapshot.OwnedEquipmentInstances` | Duplicate inventory instance IDs reject at construction and transition time. |
| Equipment placement | Each actor's `RuntimeEquipmentSnapshot` | Equip rejects missing or multiply assigned instances; aggregate save validation rejects cross-actor assignment and actor/equipment ID collisions. |
| Equipment meaning | `RuntimeEquipmentProfileResolver` plus selected `IEquipmentSlotLayoutPolicy` | Creation, composition, authorization, execution, and restore use the same resolved profile inputs. |
| Currency | `RuntimeCurrencyLedgerSnapshot` | Every transaction names a currency; invalid, missing, negative, duplicate, insufficient, and overflowing values reject without publishing a candidate. |
| Shop offer rules | `RuntimeShopOfferResolver` output | Pricing and stock are resolved once, but the resulting record can currently be reconstructed or altered after resolution. |
| Tracked stock | `RuntimeShopStockSnapshot` keyed by `(shopId, offerId)` | Missing, duplicate, negative, and unexpected tracked entries reject; accepted transactions return stock with inventory and currency as one result. |
| Recovery | `IRecoveryPolicy` plan plus `RecoveryService` execution | Actor and currency candidates are staged before the actor transaction commits; cancellation propagates without commit. |
| Save/restore | `RuntimeSaveValidator` followed by `RuntimeSessionRestoreService` | Validation covers content provenance, equipment ownership/placement, authored slots, stock, and typed currencies before aggregate actor restoration is exposed. |

## Finding O7-R9-F1: Resolved Shop Offers Can Be Forged After Resolution

**Severity:** Medium integration-authority defect

**Disposition:** Must fix before R9 certification

### Intended invariant

One catalog-backed `RuntimeShopOfferResolver` result is the transient authority
for offer identity, content, pricing, stock behavior, equipment slot, and item
stack limit. Menu assessment and execution must consume that same resolved
authority. Presentation code must not be able to rewrite one part while keeping
the other resolved parts.

### Reachable path

`RuntimeShopOfferSnapshot` is a public record with a public constructor and
public `init` accessors for every authority-bearing property. Any ordinary host
can therefore take a legitimate resolved offer and use `with` to replace
`ContentId`, `ContentKind`, `Pricing`, `Stock`, `EquipmentSlotId`,
`ItemStackLimit`, or `Identity`. The modified record is accepted directly by
`ShopTransactionService.Buy` or `Sell`; the service has no catalog from which to
re-resolve the altered fields.

For example, cloning an inexpensive item offer with a different item content ID
retains the inexpensive offer's price and stock identity while adding the new
item to inventory. This requires only a host mapping mistake; it does not require
malicious code or invalid player network input.

### Consequence

Inventory, currency, and stock can commit a mutually inconsistent transaction:
the stock entry and price belong to one authored offer while the granted or
removed content belongs to another. That contradicts the single resolved-offer
authority documented by O7-R6 and O7-R7.

### Correction

Keep offer creation inside Framework's resolver boundary and make every resolved
offer member get-only. The public resolver interface remains usable as a
decorator boundary: a host may wrap or delegate to the standard resolver, while
pricing, stock, and slot variation continue through their approved policy
interfaces. No new offer policy or authenticity mechanism is introduced.

The constructor will become non-public and enforce the complete item/equipment
shape even for Framework-internal and friend-test construction. The resolver
will return a typed diagnostic for an undefined content kind rather than
constructing an unusable profile. The deliberate public API removals are updated
in `PublicAPI.Shipped.txt`.

## Trusted Host Boundaries, Not Framework Defects

### Runtime instance allocation

The host supplies a fresh equipment instance ID when purchasing equipment.
Inventory rejects an ID already owned by inventory. Aggregate save validation
also rejects an equipment ID colliding with an actor ID. The modular inventory
service does not receive the session's actor set and therefore cannot decide
global actor/equipment allocation by itself.

R9 will certify both sides explicitly:

- a stale or duplicate inventory instance cannot commit through equip, remove,
  sale, or purchase transitions; and
- an actor/equipment collision cannot pass save validation or aggregate restore.

The host remains responsible for allocating an ID not already used by another
live session object. Adding the entire actor session to every inventory request
would be a new aggregate architecture, not an R9 correction, and is not required
by the approved O7-D1 boundary.

### Host replacement of immutable snapshots

Framework transaction services are pure over immutable before-state values. A
host can always discard an applied result or deliberately manufacture different
state in its own code. R9 guarantees that Framework rejection results preserve
their supplied before-states and that Framework save validation rejects
contradictory aggregates. It does not claim to sandbox a trusted .NET host.

### Content hot reload

Resolved shop offers are transient authorities for one bound catalog/session.
Convergence does not currently advertise live content hot reload. A host that
replaces its catalog must rebuild its runtime offer set and validate durable
stock against that catalog before continuing. This is an integration lifecycle
responsibility, not a silent fallback in the transaction service.

## Ordered R9 Correction And Certification Checkpoints

Each checkpoint is an isolated green commit. O7-R10 must not begin during this
work.

### O7-R9-C0: Record The Independent Audit

- add this audit and link it from the active Order 7 source review;
- record the reproduced 1,975-test starting baseline; and
- make no runtime, content, schema, host, or test behavior change.

### O7-R9-C1: Seal Resolved Shop Offer Authority

- make resolved offer construction non-public;
- make all resolved offer properties get-only;
- validate item/equipment profile shape at construction;
- diagnose undefined authored content kinds during resolution;
- update the deliberate public API baseline; and
- add regressions proving post-resolution authority fields have no public write
  path and malformed profiles never reach transaction execution.

### O7-R9-C2: Certify Cross-System And Wire Integrity

- add focused cross-system tests covering save validation and aggregate restore
  with equipment instances, authored slots, typed currencies, and tracked stock
  present together;
- prove stale/forged instance, offer-selection, and currency references reject
  without changing inventory, equipment, actor, stock, or currency before-state;
- prove policy cancellation propagates with every immutable before-state intact;
- run strict validation for all six active packs and all 36 active documents;
- run all DemoHost modes and the real Godot 4.7.1 headless consumer;
- append the R9 completion record to the Order 7 source review; and
- synchronize only current R9 status/evidence documentation. Three-audience
  completion remains O7-R10.

### O7-R9-C3: Fresh Post-Correction Review

- reread the resulting source and tests without treating this audit as proof;
- verify the public surface, trusted-host boundaries, and all R9 claims;
- record any realistic reachable defect before R9 is marked complete; and
- leave Order 7 open for O7-R10 and O7-R11.

## Implementation Progress

- **C0 complete:** commit `69dbb3d4` records this audit and the reproduced
  1,975-test starting baseline without changing runtime behavior.
- **C1 complete:** commit `4be530f9` removes public resolved-offer construction
  and mutation, validates internal offer shape, and returns a typed diagnostic
  for undefined content kinds.
- **C2 complete:** commit `c1eafb52` certifies aggregate restore,
  rejection/cancellation atomicity, all active content, DemoHost, Godot,
  coverage, and complete-solution evidence recorded in the Order 7 source
  review.
- **C3 complete:** the
  [post-correction review](inventory-equipment-economy-order-7-r9-post-correction-review-2026-08-13.md)
  rereads the resulting source and documentation independently, finds no
  remaining actionable R9 defect, and closes R9 without starting R10.

## Scope Guard

R9 does not add a new gameplay policy, change prices, stock transitions,
recovery behavior, equipment combat math, content schema, save shape, or
presentation flow. It does not introduce security tokens for in-process trusted
hosts, restore legacy code, begin O7-R10 documentation completion, or perform
the O7-R11 independent closure audit.
