# Inventory, Equipment, And Economy Order 7 R20 Independent Closure Audit

**Date:** 9 September 2026

**Reviewed implementation:** `aaacbd97`

**Capability:** `inventory_equipment_economy`

**Result:** one correction required; Order 7 remains open

## Verdict

The O7-R16 through O7-R19 corrections hold in current source. Equipment and
shop definition identity are checked before foreign definition fields are
used, and action-authorization cancellation propagates without consuming the
prepared assessment or mutating gameplay state.

This fresh closure trace found one additional realistic Medium authority gap:
equipment-granted skill IDs can be resolved to definitions carrying different
IDs by a faulty custom `ISkillDefinitionRepository`. The substituted definition
can become a live equipment-granted passive during combat-profile composition
or aggregate session restoration.

This is not a defect in `GameDataCatalog` and is not a player-input security
vulnerability. It is a supported-extension robustness defect with a concrete
gameplay consequence: the Framework can grant and execute a passive that the
equipped item did not author. Order 7 therefore remains `partial`, and its
three audience documents remain `existing_unreviewed`, until O7-R21 through
O7-R23 are complete.

## Review Method

The current implementation was traced from source before consulting earlier
closure conclusions. The review followed:

- inventory ownership and equipped-instance references;
- slot-policy and equipment-profile resolution;
- live actor equipment application and combat-profile composition;
- skill authorization and passive registration;
- save validation and aggregate session restoration;
- DemoHost and Godot-contract resource state adoption;
- focused adversarial tests and the complete solution suite; and
- the mechanics, developer, and technical Order 7 documents.

The active documentation was used to identify intended behavior. Source
established actual behavior, and tests corroborated exercised paths.

## Confirmed Corrections

### O7-R16: Equipment Definition Identity

`RuntimeEquipmentProfileResolver` now compares a returned equipment
definition's ID with the owned instance's requested definition ID before slot
validation, grants, attack data, Defense, Evasion, or modifiers are read.
Direct profile resolution, actor application, battle authorization, and
aggregate restore share that rejection boundary.

### O7-R17: Shop Content Definition Identity

`RuntimeShopOfferResolver` now rejects returned item and equipment definitions
whose IDs differ from the authored offer content ID before stack-limit or slot
data is copied into a runtime offer.

### O7-R18: Authorization Cancellation

`BattleActionExecutor` propagates `OperationCanceledException` from assessment
and execution-time reauthorization. If cancellation occurs after the prepared
assessment token is claimed, the token is restored before cancellation leaves
the executor. Non-cancellation faults retain typed containment.

### O7-R19: Documentation Reconciliation

All three Order 7 audience pages describe the repository-identity and
cancellation rules above without claiming certification. Executable tracking
correctly remains `partial` / `existing_unreviewed` pending this audit.

## Finding

### O7-M4: Equipment-Granted Skill Resolution Accepts A Substituted Definition

**Severity:** Medium

**Intended invariant:** an equipment profile grants the exact skill IDs
authored by the equipped definitions. Resolving a grant must never replace the
authored skill with another catalog definition.

**Reachable path:**

1. an equipped instance grants skill `skill_a`;
2. a custom `ISkillDefinitionRepository` is asked for `skill_a` but returns
   definition `skill_b`;
3. `RuntimeActorCombatProfileCompositionService` checks only the Boolean
   lookup result and null value;
4. `RuntimeActorState.ApplyCombatProfile` accepts `skill_b` as an
   equipment-granted passive because the passive-definition collection is
   validated for valid and unique returned IDs, not against the requested
   grant IDs; and
5. the actor can execute `skill_b`'s passive lifecycle while no equipment
   definition authored that grant.

The aggregate restoration path has the same authority gap before composition:
`CatalogBattleActorFactory.Restore` resolves requested equipment-granted skill
IDs into a dictionary without verifying each returned definition's ID, then
passes the returned definitions into `RuntimeActorState.Restore`.

**Source evidence:**

- [`RuntimeActorCombatProfileComposition.cs`](../../src/Convergence.Framework/Runtime/RuntimeActorCombatProfileComposition.cs#L772)
  resolves every equipment-granted skill and appends the returned definition
  without comparing `skill.Id` with the requested `skillId`.
- [`BattleRuntimeState.cs`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs#L1289)
  validates the returned equipment-passive definitions as a collection but
  cannot recover the original requested IDs at that boundary.
- [`CatalogBattleActorFactory.cs`](../../src/Convergence.Framework/Encounters/CatalogBattleActorFactory.cs#L617)
  combines saved, pending, and equipment-granted IDs, then accepts any non-null
  definition returned for each lookup.
- [`RuntimeSessionRestoration.cs`](../../src/Convergence.Framework/Runtime/RuntimeSessionRestoration.cs#L583)
  supplies the equipment profile's grant IDs to that restore path.

**Concrete consequence:** a faulty adapter or hot-reload repository can grant
a different passive, including its lifecycle effects, while the saved and
equipped authorities still name only the original skill. The standard catalog
cannot produce the mismatch, but custom repositories are an intentional public
composition boundary.

**Required correction:** reject requested/returned skill identity mismatches
before any returned definition enters actor construction, combat-profile
composition, passive registration, or aggregate restoration. Return distinct
typed diagnostics and prove all rejection paths preserve the live actor and
aggregate restore atomicity.

## Confirmed Healthy Boundaries

The source trace found no regression in these Order 7 guarantees:

- inventory remains the sole owner of immutable equipment instances;
- equipped actors retain instance references rather than duplicate ownership;
- missing, duplicate, and multiply-equipped instance IDs reject atomically;
- custom slot layouts remain policy-owned while the standard layout preserves
  Weapon, Armor, Boots, and Accessory behavior;
- equipment grants remain derived and consume no learned move-list slot;
- Defense and Evasion remain additive inputs to the canonical combat formulas;
- named-currency ledger operations preserve unrelated balances and reject
  invalid arithmetic;
- shop purchase/resale stage inventory, currency, and stock together;
- recovery stages currency and actor cleanup before committing either; and
- save v19 validates the complete inventory/equipment/currency/shop graph
  before aggregate restoration exposes a session.

No separate equipment combat formula, granted-skill policy, currency policy,
or duplicate save authority was found.

## Documentation Cross-Check

The mechanics, developer, and technical Order 7 documents remain accurate for
the standard in-repository catalog. Their statement that repository lookups
cannot lend another definition's authority is incomplete for equipment-granted
skills, however. O7-R22 must state that exact skill identity is enforced during
actor construction, composition, and restore after O7-R21 implements it.

Until then:

- `inventory_equipment_economy` remains `partial`;
- all three Order 7 audience entries remain `existing_unreviewed`; and
- O7-R15 and the earlier closure reports remain historical evidence for their
  reviewed revisions, not current closure authority.

## Executed Baseline Evidence

The unmodified `aaacbd97` baseline produced:

- 1,855 Framework tests passed;
- 184 DemoHost tests passed;
- 7 ContentValidator tests passed; and
- 2,046 total tests passed with 0 failed and 0 skipped.

This green baseline confirms the standard path. It does not cover a custom
skill repository returning the wrong definition ID for an equipment grant.

## Correction Roadmap

### O7-R21: Enforce Actor Skill Definition Identity

- require exact requested/returned skill IDs in catalog actor creation and
  restore;
- require the same identity in combat-profile composition, including
  equipment-granted passives;
- add distinct typed diagnostics and update the guarded public API baseline;
- prove direct composition and actor creation/restore reject substitution;
- prove equipment application leaves the live actor unchanged; and
- prove aggregate restore returns no session and no partially restored actor.

### O7-R22: Reconcile Documentation And Tracking

- document the exact skill-identity rule in all applicable Order 7 audiences;
- update executable documentation synchronization evidence;
- keep capability and audience certification pending until fresh closure.

### O7-R23: Fresh Independent Closure And Release Gate

- re-read current Framework, persistence, DemoHost, Godot-contract, tests, and
  all three audience documents without using this report as implementation
  evidence;
- retain the complete release-gate output in the repository; and
- promote to `complete` / `reviewed` only if no realistic reachable Order 7
  defect or active documentation contradiction remains.

## Closure Decision

**Order 7 is not ready for formal closure at `aaacbd97`.** O7-R16 through
O7-R19 are healthy, but O7-M4 must be corrected and independently rechecked.

## Correction Status

O7-R21 is implemented by `57d0d101`: exact skill identity is now enforced in
actor construction, restore, skill views, and combat-profile composition, with
typed diagnostics and atomic rejection evidence. O7-R22 reconciles the three
audience documents and executable tracking with that correction. Order 7 still
remains `partial` / `existing_unreviewed` until O7-R23 performs a new
source-first closure review and retains the complete release gate.
