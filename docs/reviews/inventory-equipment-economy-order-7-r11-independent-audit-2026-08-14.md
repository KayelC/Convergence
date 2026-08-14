# Inventory, Equipment, And Economy Order 7 R11 Independent Audit

**Date:** 14 August 2026  
**Capability:** `inventory_equipment_economy`  
**Baseline:** `25c0a78a`  
**Result:** corrections required before Order 7 can close

## Review Method

This audit reread the current Framework, DemoHost, Godot sample, tests, strict
schemas, and all three Order 7 audience documents. Earlier reports were not
used as implementation proof. Each finding below identifies the intended
invariant, a reachable path, its concrete consequence, and reproducible source
or test evidence.

The clean baseline reproduced before any correction:

```text
Convergence.Framework.Tests: 1,794 passed, 0 failed, 0 skipped
Convergence.DemoHost.Tests: 182 passed, 0 failed, 0 skipped
Convergence.ContentValidator.Tests: 7 passed, 0 failed, 0 skipped
Total: 1,983 passed, 0 failed, 0 skipped
```

## Verified Healthy Areas

The audit found the following implemented invariants to be coherent:

- inventory owns exact immutable equipment instances and permits separate
  copies of one definition;
- equip, removal, sale, and save validation reject missing, duplicate,
  multiply assigned, equipped, and actor-colliding instance IDs;
- standard and custom authored slots retain instance-ownership checks;
- active equipment-granted skill authorization is resolved from the current
  loadout at assessment and execution, so stale prepared actions reject;
- shop results carry coordinated inventory, named-currency, and stock
  candidates, and every rejection preserves all three supplied before-states;
- currency ledgers reject duplicate IDs, negative balances, overflow, and
  missing named currencies through typed diagnostics;
- recovery stages actor cleanup and named-currency debit, removes only legally
  removable ailments, preserves protected state, and commits no partial live
  actor on rejection; and
- save v19 has one equipment ownership authority and validates the complete
  inventory/loadout graph before aggregate actor construction.

These strengths do not cancel the reachable authority defects below.

## Findings

### O7-R11-M1: Live equipment application is not atomic with actor composition

**Intended invariant:** one equipped-state change must atomically update the
actor's loadout and the derived Defense, Evasion, resources, combat-profile
identity, and equipment-granted passives. The same equipment state must mean
the same thing whether reached during creation, restore, or live play.

**Reachable path:** the Training Annex shop buys a catalog armor instance,
accepts `EquipmentTransitionService.Equip`, immediately calls
`RuntimeActorState.ReplaceEquipment`, and publishes an equipped-success event.
After the shop switch completes, the outer command loop does invoke canonical
composition. If profile resolution or composition rejects, however, that loop
returns a failure exit code without rolling the already-replaced loadout back.
The public composition service mutates the supplied actor and Framework exposes
no staged operation that can commit the loadout and composition together.

**Consequence:** the valid standard-content path eventually reaches a coherent
composition, but a rejected composition leaves the actor referencing the new
loadout while its effective Defense/Evasion and passive collection still
describe the previous one. The host has already announced success. Active
granted-skill authorization sees the fresh projection, so different combat
subsystems can observe different meanings for the same equipment snapshot on
that failure path.

**Evidence:**

- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexShopController.cs`
  commits `ReplaceEquipment` and publishes success before composition;
- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/CleanTrainingAnnexPlayHost.cs`
  composes only after the shop controller returns and has no rollback path;
- `src/Convergence.Framework/Execution/BattleRuntimeState.cs`
  `ReplaceEquipment` removes prior equipment passives but does not compose new
  numeric contributions or passives;
- `src/Convergence.Framework/Runtime/RuntimeActorCombatProfileComposition.cs`
  is the only canonical path that applies those derived values; and
- `CleanTrainingAnnexPlay_ShopBuysAndEquipsCatalogEquipment` asserts ownership
  and profile projection but not the live actor's composed stats/passives.

**Correction checkpoint O7-R11-C1:** add one fixed, non-policy Framework
operation that resolves an equipment profile and applies the candidate loadout
plus canonical actor composition through a staged actor transaction. Rejection
must leave the live actor unchanged. Route the Training Annex live equip path
through it and prove equip, unequip, rejection, Vessel composition, and passive
provenance.

### O7-R11-M1: Aggregate restore lets a host re-declare fixed equipment-derived state

**Intended invariant:** inventory instances, actor loadouts, and catalog
definitions are the sole authorities for equipment-derived modifiers and
granted skills. Hosts choose actor stat-source behavior; they do not decide
what an equipped definition contributes.

**Reachable path:** `RuntimeActorRestoreProfile` publicly accepts
`EquipmentStatModifiers` and `EquipmentGrantedSkillIds`.
`RuntimeSessionRestoreService` forwards those host values to the actor factory
even though the aggregate already contains inventory, loadouts, and catalog.
The Clean Save and Godot sample profile resolvers return empty equipment data;
the Training Annex resolver independently rebuilds it.

**Consequence:** the same valid save can restore different effective stats or
passive grants according to host resolver behavior. Omitting or forging these
values does not change the saved equipment graph and can pass aggregate
validation, so restore is not authoritative over fixed equipment behavior.

**Evidence:**

- `src/Convergence.Framework/Runtime/RuntimeSessionRestoration.cs` defines and
  consumes the host-supplied equipment fields;
- `samples/Convergence.DemoHost/Hosts/CleanSaveDemoHost.cs` and
  `samples/Convergence.GodotHost/Scripts/ConvergenceSmokeRoot.cs` omit them;
- the Training Annex persistence resolver derives them independently; and
- save validation already constructs `RuntimeEquipmentProfileResolver`, proving
  the aggregate has sufficient canonical evidence.

**Correction checkpoint O7-R11-C2:** remove equipment-derived values from the
aggregate restore-profile contract. Make aggregate restoration resolve each
actor's equipment profile from current inventory, loadout, catalog, and the
explicitly selected equipment-profile resolver. Reject profile diagnostics or
faults before exposing a session. Keep the lower-level actor factory contract
available for callers that deliberately restore one actor without an aggregate.

### O7-R11-M2: Custom slot-policy faults escape typed Framework boundaries

**Intended invariant:** `IEquipmentSlotLayoutPolicy` is a supported extension
boundary. Cancellation propagates, while null, faulting, or malformed policy
results become typed diagnostics without partially changing state.

**Reachable path:** content validation, equip/unequip, profile resolution, shop
offer resolution, and save validation call `ValidateDefinition` or
`ValidateAssignment` directly. A developer-authored policy may throw or return
null, as other policy families explicitly defend against.

**Consequence:** a policy implementation defect can escape as an untyped
exception from catalog loading, a live equipment transition, shop startup, or
save validation. This contradicts the active technical documentation's stated
fault-containment rule.

**Evidence:** direct policy calls exist in
`SkillSystemContentValidator.cs`, `ResourceManagementServices.cs`,
`RuntimeEquipmentProfiles.cs`, and `RuntimePersistenceSnapshots.cs`; the slot
tests currently cover compatible/incompatible results but not null, fault, or
cancellation behavior.

**Correction checkpoint O7-R11-C3:** centralize slot-policy invocation in one
internal evaluator, preserve `OperationCanceledException`, normalize null or
undefined results, and convert other faults to an explicit `PolicyRejected`
result. Route every Framework slot-policy consumer through that evaluator and
add focused boundary tests.

### O7-R11-L1: Player recovery wording overstates atomic ailment rejection

**Intended invariant:** recovery applies every legal selected change while
leaving individually protected ailments untouched. A protected ailment does
not reject an otherwise useful resource restore or legal ailment cure.

**Contradiction:** `docs/mechanics/party-inventory-and-economy.md` currently
groups “protected ailments” with whole-transaction rejection. The standard
policy and tests instead preserve protected ailments while applying other legal
changes. A protected-only actor with no other change receives
`NoRecoveryNeeded`.

**Correction checkpoint O7-R11-C4:** correct the player wording and synchronize
the related developer/technical explanation where the R11 API corrections
change responsibilities.

### O7-R11-M3: Live item transitions accept an empty content identity

**Intended invariant:** a successful live inventory transition must contain
only items identified by valid `ContentId` values. Invalid host command input
must return a typed rejection with the exact supplied inventory as both before
and after. This is distinct from decoded save snapshots, which deliberately may
represent malformed item keys long enough for aggregate save validation to
report their paths.

**Reachable path:** `InventoryTransitionService.AddItem` validates quantity and
stack arithmetic but never validates `itemId`. A host can therefore call
`AddItem(snapshot, default, 1)`, receive `Applied`, and store the empty ID as an
inventory key. `RemoveItem` and `ReserveItem` likewise treat an empty ID as an
ordinary lookup rather than invalid command input.

**Consequence:** a public gameplay transition can create live inventory that
the save validator later rejects as `InvalidContentId` and that no catalog item
can resolve. The failure is delayed beyond the command that introduced it, so
the host receives false success instead of a local typed diagnostic.

**Evidence:**

- `RuntimeInventorySnapshot` intentionally permits an empty item key so
  `RuntimeSaveValidator_AggregatesDefaultIdentifiersBeforeRestoreOrLookup` can
  aggregate a path-specific malformed-save diagnostic;
- `InventoryTransitionService.AddItem`, `.RemoveItem`, and `.ReserveItem` have
  no `itemId.IsValid` boundary check; and
- currency and equipment command boundaries already reject their corresponding
  invalid identities before producing accepted state.

**Correction checkpoint O7-R11-C5:** add a distinct
`ResourceTransactionCode.InvalidItemId`, reject empty IDs atomically in all
three live item operations, retain permissive malformed-snapshot construction
for aggregate validation, and document/test the boundary distinction.

## Trusted Host Boundary: Concurrent Candidate Adoption

`ShopTransactionService` is deliberately stateless. It calculates an immutable
candidate from the exact inventory, currency, and stock snapshots supplied by
the host; it does not own a lockable live session. Two concurrent requests
using the same before-snapshots can therefore both calculate acceptable
candidates. The Framework guarantee is atomicity *within one result*, not
automatic compare-and-swap across host session storage.

The supported boundary is:

1. serialize shop mutations per session, or compare-and-swap all three current
   authorities against the result's before-snapshots;
2. adopt inventory, currency, and stock from one accepted result together; and
3. reject/retry a candidate whose before-snapshots are no longer current.

This is not converted into a mutable Framework repository or a speculative
policy. O7-R11-C4 will make the boundary explicit in developer and technical
documentation and add executable evidence for sequential depletion and stale
same-before candidates.

The same trusted-host principle applies to complete collision evidence:
`EquipmentTransitionService.Equip` and equipment resale can reject
cross-actor use only from the complete actor-loadout collection supplied by the
host. Aggregate save validation independently rechecks the whole graph.

## Correction Order

| Checkpoint | State | Work | Commit boundary |
|---|---|---|---|
| O7-R11-A | complete | Preserve this audit and correction roadmap | documentation only |
| O7-R11-C1 | complete | Add atomic canonical live equipment application and migrate DemoHost | runtime + focused tests + affected docs/API evidence |
| O7-R11-C2 | complete | Make aggregate restore derive equipment contributions | runtime + host/Godot/persistence tests + API evidence |
| O7-R11-C3 | complete | Contain custom slot-policy faults at every Framework boundary | runtime + validation/transition/profile/save tests + API evidence |
| O7-R11-C4 | complete | Correct recovery wording and document trusted host concurrency/collision boundaries | three-audience docs + executable documentation/boundary tests |
| O7-R11-C5 | open | Reject invalid IDs at live item-transition boundaries while preserving malformed-save diagnostics | runtime + focused tests + affected docs/API evidence |
| O7-R11-C6 | open | Freshly re-read post-correction source/docs and run the durable release gate | closure report + tracking/evidence only |

Each correction receives its own green commit. Order 7 remains `partial` until
O7-R11-C6 finds no remaining realistic reachable defect or documentation
contradiction and the complete release gate passes.

## Scope Guard

These corrections add no presentation UI, proprietary content, new combat
formula, granted-skill policy, currency policy, mutable shop repository, or
Order 8 work. Equipment ownership, profile derivation, and currency identity
remain fixed rules. Custom slot layout remains the only equipment policy
surface changed by this audit.
