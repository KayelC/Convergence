# Inventory, Equipment, And Economy Integration

## Purpose

This guide shows how a host composes Order 7 resource-management services and
adopts their immutable results. It covers equipment instance IDs, authored
slots, live equipment profiles, currency ledgers, resolved shop offers,
policy-owned stock, recovery, persistence, and Godot integration.

> **Review state:** `reviewed` after O7-R10 reconciled the mechanics,
> integration, and technical authorities with current source and tests. O7-R11
> remains the independent capability-closure audit.

## Authority Split

| Concern | Framework owns | Host owns |
|---|---|---|
| Inventory | quantities, equipment-instance ownership, immutable transitions | when to request a transition and which successful snapshot becomes session state |
| Equipment | slot-policy validation, exact-instance assignment, derived profile | globally fresh instance IDs, selection UI, coordinating profile recomposition |
| Currency | typed balances and checked credit/debit transitions | currency names, formatting, and adopting successful ledgers |
| Shops | offer resolution, pricing/stock policies, atomic inventory/currency/stock results | menus, confirmations, fresh equipment IDs, and current state storage |
| Recovery | policy planning, legal cleanup, staged actor/currency transaction | patient selection, presentation, and adopting the successful ledger |
| Persistence | serializer-neutral save shape, validation, aggregate restoration | file format, slots, paths, encryption, migrations, scene reconstruction |

Do not copy framework state into a second mutable shop, equipment, or wallet
model. Keep the current immutable snapshots in the host session and replace
them only from accepted results.

## Bind The Economy Ruleset

The standard composition path registers supplied policy factories and binds an
authored economy ruleset from the catalog:

```csharp
RuntimeRulesetPolicyFactoryRegistry factories =
    RuntimeRulesetPolicyFactoryRegistry.CreateStandard();

var resolver = new RuntimeRulesetBindingResolver(factories);
ResourceManagementRulesetServices resources = resolver
    .BindResourceManagementServices(catalog, economyRulesetId)
    .RequireService();
```

`ResourceManagementRulesetServices` contains:

- `Inventory`: item and equipment ownership transitions;
- `Equipment`: equip and unequip transitions;
- `Economy`: named-currency credits and debits;
- `ShopOffers`: authored-offer resolution;
- `Shop`: atomic purchase and resale transitions; and
- `Recovery`: the optional service selected by the ruleset.

Binding failure is a startup/content diagnostic. Do not catch it and silently
construct a different pricing, stock, or recovery rule.

### Registering Real Replacement Policies

Register additional `IShopPricingPolicyFactory`, `IShopStockPolicyFactory`, or
`IRecoveryPolicyFactory` implementations when a game has a real alternate
behavior:

```csharp
RuntimeRulesetPolicyFactoryRegistry factories =
    RuntimeRulesetPolicyFactoryRegistry.CreateStandard(
        shopPricing: [customPricingFactory],
        shopStock: [customStockFactory],
        recovery: [customRecoveryFactory]);
```

Authored policy IDs select those factories. Equipment ownership,
equipment-granted skill behavior, and currency identity are fixed data-model
rules, not policy extension points.

## Use One Slot Layout Everywhere

The standard ruleset bundle uses `StandardEquipmentSlotLayoutPolicy`. A game
with custom authored slot IDs must inject the same
`IEquipmentSlotLayoutPolicy` into every slot-sensitive service:

```csharp
IEquipmentSlotLayoutPolicy layout = customLayout;

var contentValidator = new SkillSystemContentValidator(layout);
var catalogLoader = new SkillSystemCatalogLoader(
    new SkillSystemJsonDeserializer(),
    contentValidator);
var equipmentTransitions = new EquipmentTransitionService(layout);
var equipmentProfiles = new RuntimeEquipmentProfileResolver(layout);
var shopOffers = new RuntimeShopOfferResolver(
    defaultPricing,
    pricingFactories,
    stockFactories,
    layout);
var saveValidator = new RuntimeSaveValidator(equipmentSlotLayout: layout);
```

Content semantic validation must use that layout as well. Mixing layouts can
make content valid in one boundary and incompatible in another. Pass
`saveValidator` to aggregate restoration and the same profile resolver to
actor composition and live equipment sources. The selected layout owns
slot/profile compatibility; it does not own inventory identity, combat math,
or granted-skill behavior.

## Allocate Equipment Instance IDs In The Host

An equipment definition describes a kind of equipment. An equipment instance
is one owned copy:

```csharp
var copy = new RuntimeEquipmentInstanceSnapshot(
    hostAllocatedInstanceId,
    equipmentDefinitionId);

InventoryTransitionResult added = resources.Inventory.AddEquipment(
    inventory,
    copy,
    authoredSlotId);

if (added.Applied)
{
    inventory = added.After;
}
```

The host must allocate IDs that are globally fresh across actors and equipment
instances in the session. Framework transition and restore boundaries reject
duplicate IDs, cross-actor assignments, missing ownership, and actor/equipment
ID collisions; they do not provide a process-global ID generator.

When purchasing equipment, pass the fresh ID to `Shop.Buy`. Item purchases pass
`null`. When selling equipment, identify the exact owned instance rather than
only its definition.

## Equip One Exact Owned Copy

An equip request needs inventory ownership, the actor's current loadout, the
owned slot, the target slot, and every other actor's loadout:

```csharp
EquipmentTransitionResult result = resources.Equipment.Equip(
    inventory,
    actor.ToSnapshot().Equipment,
    equipmentInstanceId,
    ownedSlotId,
    targetSlotId,
    otherActors.Select(other => other.ToSnapshot().Equipment));

if (!result.Applied)
{
    Present(result.Diagnostics);
    return;
}
```

Passing other loadouts is load-bearing: it prevents one instance from being
equipped by two actors. The returned `After` is the candidate loadout; the
service does not silently mutate the actor.

Before presenting the change as complete, pass the candidate loadout to
`RuntimeActorEquipmentApplicationService`. It resolves the canonical equipment
profile, composes the complete actor on an execution clone, and commits the
loadout plus composed actor state only when both operations accept:

```csharp
var equipmentApplication = new RuntimeActorEquipmentApplicationService(
    actorComposition,
    profileResolver);

RuntimeActorEquipmentApplicationResult applied = equipmentApplication.Apply(
    new RuntimeActorEquipmentApplicationRequest(
        actor,
        inventory,
        result.After,
        catalog,
        RuntimeStatSourceKind.ActiveHostedEntity,
        MissingHostedEntityBehavior.RejectStatResolution,
        partyRoster,
        runtimeActors));

if (!applied.Applied)
{
    Present(applied.Diagnostics);
    return;
}
```

On rejection, `Before` and `After` both describe the unchanged live actor. The
equipment transition remains a pure candidate operation; the application
service is the live actor commit boundary.

Do not manually copy grants into learned skills. Do not independently add
armor Defense or boots Evasion in battle code.

## Resolve One Live Equipment Profile

Use one resolver for menus, composition, basic attacks, action authorization,
passives, and battle execution:

```csharp
IRuntimeEquipmentProfileResolver profileResolver =
    new RuntimeEquipmentProfileResolver(layout);

RuntimeEquipmentProfile profile = profileResolver.Resolve(
    inventory,
    actor.ToSnapshot().Equipment,
    catalog);

if (profile.Diagnostics.Count != 0)
{
    Present(profile.Diagnostics);
    return;
}
```

For battle services that resolve by live actor, create
`RuntimeActorEquipmentProfileSource` from the current inventory and catalog.
Rebuild that source whenever the session adopts a new inventory snapshot. It is
a projection over the supplied immutable inventory, not a mutable cache.

The profile supplies basic attack data, stat modifiers, and current grants.
Canonical action execution rechecks authorization, so an action prepared while
a grant existed rejects if the granting instance is unequipped before
execution.

The standard resolver models one current weapon basic attack, matching the
standard four-slot layout. A custom layout that permits multiple simultaneous
weapon profiles must supply an `IRuntimeEquipmentProfileResolver` with an
explicit basic-attack selection rule rather than relying on slot iteration
order.

## Initialize Currency And Shop State

Create a typed ledger even for a one-currency game:

```csharp
RuntimeCurrencyLedgerSnapshot currencies =
    RuntimeCurrencyLedgerSnapshot.Single(currencyId, initialBalance);

RuntimeCurrencyBalanceSnapshot onlyCurrency = currencies.GetSingleCurrency();
```

`GetSingleCurrency()` is convenience, not an implicit transaction path. It
throws a typed `RuntimeCurrencyLedgerException` for an empty or multi-currency
ledger. Every credit, debit, shop transaction, and recovery plan still names a
currency ID.

Resolve authored offers through the bound resolver, then derive initial stock
from those resolved offers:

```csharp
RuntimeShopOfferSnapshot offer = resources.ShopOffers
    .Resolve(shop.Id, authoredOffer, catalog, catalog)
    .RequireOffer();

RuntimeShopStockSnapshot stock =
    RuntimeShopStockSnapshot.CreateInitial([offer]);
```

Do not manufacture or alter a resolved offer in presentation code. Re-resolve
offers after replacing the catalog. Durable stock identity is the qualified
shop ID plus the offer's shop-local ID, not menu position or content ID.

## Assess And Apply Shop Transactions

Price helpers use the same resolved pricing profile as execution:

```csharp
int displayedPrice = resources.Shop.CalculateBuyPrice(offer, buyerLuck);

ShopTransactionResult purchase = resources.Shop.Buy(
    inventory,
    currencies,
    stock,
    currencyId,
    offer,
    buyerLuck,
    purchasedEquipmentInstanceId);
```

Adopt all three after-snapshots together:

```csharp
if (purchase.Applied)
{
    inventory = purchase.AfterInventory;
    currencies = purchase.AfterCurrencyLedger;
    stock = purchase.AfterStock;
}
else
{
    Present(purchase.Diagnostics);
}
```

Never adopt only inventory, currency, or stock from a shop result. A rejected
result returns the original three authorities. Execute against current state
after confirmation; a price shown earlier is presentation evidence, not
permission to bypass revalidation.

For resale, pass every actor loadout so the exact instance cannot be sold while
equipped. Item resale passes no equipment instance ID; equipment resale passes
the exact instance ID.

## Assess And Apply Recovery

Recovery is absent when the economy ruleset does not select a policy:

```csharp
IRecoveryService recovery = resources.Recovery
    ?? throw new InvalidOperationException("This game has no recovery service.");

RecoveryTransactionResult assessment =
    recovery.Assess(actor, currencies, statModifiers);
```

Use the assessment for eligibility and price presentation. Do not replace it
with a host-side health check: an actor at full resources may still need a
legal ailment or temporary-state cleanup.
Its after-ledger is hypothetical and must not be adopted; assessment does not
commit the staged actor either.

After confirmation, execute against current state:

```csharp
RecoveryTransactionResult execution =
    recovery.Recover(actor, currencies, statModifiers);

if (execution.Applied)
{
    currencies = execution.AfterCurrencyLedger;
}
else
{
    Present(execution.Diagnostics);
}
```

`Recover` commits the staged actor only after policy, cleanup, and debit all
accept. The host must adopt the returned ledger from the same successful
result. Rejection preserves both authorities. Cancellation remains an
`OperationCanceledException`; do not display it as a gameplay refusal.

## Save And Restore

Save contract v19 stores the current inventory, currency ledger, shop stock,
actors with equipment-instance references, and the rest of the session
aggregate. The host serializes the snapshot but does not split those
authorities into independent save records.

Validate and restore the complete aggregate through
`IRuntimeSessionRestoreService`. Do not recreate actors first and attach
equipment afterward. Host decoding constructs the immutable currency ledger,
which rejects invalid IDs, duplicate IDs, and negative balances. Aggregate
validation then checks instance uniqueness, ownership, slot compatibility,
cross-actor assignment, stock, catalog references, and selected policies before
any partial live session is exposed.

A non-current contract version needs an explicit host-supplied migration.
Convergence does not guess how an old wallet or equipment shape maps into v19.

## Godot Integration

Godot code should keep framework state in a session/service object, not on
individual UI controls:

1. Map `RuntimeInstanceId` values to host-owned Nodes.
2. Translate input and signals into typed transition requests.
3. Present immutable assessments and diagnostics.
4. Execute against current session snapshots after confirmation.
5. Adopt every successful after-snapshot together.
6. Re-resolve/recompose affected actor profiles before refreshing battle or
   equipment UI.
7. Serialize `RuntimeSaveGameSnapshot` inside the Godot-owned save format.
8. Reconnect Nodes by runtime ID only after aggregate restore succeeds.

No `Node`, `Resource`, `res://` path, serializer type, or scene handle enters
`Convergence.Framework`.

## Integration Checklist

- One current inventory snapshot owns every equipment instance.
- Equipment IDs are globally fresh and stable across save/restore.
- One selected slot layout is used by validation, transitions, offers, and
  profile resolution.
- Other actor loadouts are supplied to equip and sale checks.
- Equipment grants remain derived; learned skills are never edited for them.
- Equipment profile sources are rebuilt after inventory replacement.
- Every currency operation names its currency.
- Resolved offers, not raw definitions or menu rows, reach shop transactions.
- Inventory, currency, and stock after-snapshots are adopted together.
- Recovery assessment and execution use the same bound service.
- Save and restore treat the session as one authority graph.

## Related Guidance

- [Player Mechanics](../mechanics/party-inventory-and-economy.md)
- [Typed Actions And Effects](typed-actions-and-effects.md)
- [Actors And Runtime State](actors-and-runtime-state.md)
- [Ruleset Policy Contracts](../ruleset-policy-contracts.md)
- [Content Contract](../content-contract.md)
- [Content Authoring Validator](../content-authoring-validator.md)
- [Godot Integration Contract](../godot-integration-contract.md)
- [Public API Contract](../public-api-contract.md)
