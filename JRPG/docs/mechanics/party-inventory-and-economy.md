# Party, Stock, Inventory, Equipment, And Economy

## Party And Ownership Graph

Framework party state distinguishes active party members, reserve members, an active form, Persona-like form stock, and demon-like owned stock. These are generic runtime roles; a game may use only the roles it needs.

**Framework rule:** one runtime instance ID cannot occupy incompatible ownership roles or appear as a duplicate entry. Active/reserve membership, form ownership, and stock identity are validated on transitions and restore.

## Party And Stock Commands

Transition services support adding a member, swapping active and reserve positions, summoning an owned actor, swapping active actors, returning an actor to stock, dismissing, replacing, and consuming stock entries, plus active-form swap/consume/replace operations.

Each request returns `Before`, `After`, a stable code, diagnostics, and ordered affected IDs. Rejection preserves `Before` exactly.

**Configured rule:** maximum party size and stock capacity come from a policy. Capacity may be unlimited or tiered by level. Convergence does not require a particular number of party or stock slots.

## Inventory

Items are stored as typed content IDs and integer quantities. Stack limits come from clean item definitions when provided. Negative quantities, missing ownership, overflow, and stack-limit violations are rejected.

Equipment ownership is unique by equipment ID in the current model. There are no per-copy equipment instances. Games needing randomized or individually enhanced equipment would need an additional instance model.

## Equipment

Equipment definitions use typed slots and slot-specific profiles. Equip and unequip transitions validate ownership and slot compatibility. Selling equipped equipment can be blocked by the transaction service.

Basic attack profiles may come from equipped weapon data, but a host can supply another clean basic-attack profile. Presentation metadata does not decide damage behavior.

## Wallet And Economy

Currency is represented by a nonnegative wallet balance. Credit and debit operations are atomic, checked for overflow, and reject insufficient funds or negative transaction values.

The Framework does not require the term Macca, gold, credits, or any other player-facing currency name. DemoHost labels its sample currency; a game owns its terminology.

## Shops

Shop definitions identify offered items/equipment, categories, price or pricing-policy IDs, stock policy, and availability. Runtime transactions assess ownership, stock, stack limits, equipped-sale restrictions, and wallet balance before mutation.

Buy and sell prices are policy outcomes. The supplied standard/example policies can use base price and actor stats, but a game may provide fixed, regional, reputation-based, or other pricing.

## Recovery Facilities

Hospital/restoration services assess a cost, payment, resource restoration, ailment removal, and encounter-persistence cleanup as one transaction. A host decides which actors can be selected and how the facility is presented.

An ailment-only treatment may be valid even when HP/SP are full if the selected policy permits it. A game should not duplicate eligibility logic in its UI; it should present the assessment result.

