# Inventory, Equipment, And Economy Order 7 Post-R15 Independent Audit

**Date:** 31 August 2026

**Reviewed implementation:** `ef4e129e`

**Capability:** `inventory_equipment_economy`

**Result:** corrections required; Order 7 is reopened

## Verdict

This audit found no Critical or High finding and no defect in the standard
in-repository catalog path. It found three realistic Medium extension-boundary
defects:

1. equipment profile resolution accepts a definition returned for the wrong
   equipment ID;
2. shop offer resolution can take an item stack limit from a definition
   returned for the wrong item ID; and
3. battle-action assessment and execution normalize cancellation raised during
   equipment-backed authorization into ordinary gameplay failure.

These are not remote-code-execution or player-input security vulnerabilities.
They require a faulty custom repository or policy implementation supplied
through a supported public extension contract. They still qualify as framework
defects because each path returns false authority or loses a documented host
control signal instead of rejecting the malformed extension result.

Order 7 must remain `partial` until O7-R16 through O7-R20 are complete and
independently reviewed.

## Review Method

This was a fresh source-first review. Earlier Order 7 reports and closure
summaries were not used as evidence for implementation health. The audit traced:

- inventory and equipment instance ownership, slot assignment, profile
  projection, actor composition, action authorization, and save restoration;
- typed currency, shop offer resolution, pricing, stock, purchase/resale, and
  recovery transactions;
- DemoHost and Godot-contract state adoption;
- adversarial Framework and DemoHost tests; and
- the current mechanics, developer, technical, capability, and documentation
  contracts.

Active mechanics and decision documentation was used only to identify intended
behavior. Source established current behavior. Tests were used after the source
trace to corroborate covered paths and expose missing adversarial cases.

## Findings

### O7-M1: Equipment Profile Resolution Accepts A Substituted Definition

**Severity:** Medium

**Intended invariant:** an owned equipment instance references one exact
equipment definition ID. The live equipment profile must derive its attack,
grants, Defense, Evasion, and modifiers from that definition and no other one.

**Reachable path:**

1. inventory owns an instance whose `DefinitionId` is `equipment_a`;
2. a custom `IEquipmentDefinitionRepository` is asked for `equipment_a` but,
   because of an adapter or hot-reload bug, returns definition `equipment_b`;
3. `RuntimeEquipmentProfileResolver` checks only the Boolean lookup result and
   null value;
4. if `equipment_b` is slot-compatible, its attack, grants, and numeric
   contributions enter the accepted profile.

**Source evidence:**

- [`RuntimeEquipmentProfiles.cs`](../../src/Convergence.Framework/Runtime/RuntimeEquipmentProfiles.cs#L182)
  obtains the requested ID from the owned instance and accepts the returned
  definition without checking `definition.Id == equipmentId`.
- The returned definition becomes the source of grants, basic attack, Defense,
  Evasion, and accessory modifiers beginning at
  [`RuntimeEquipmentProfiles.cs`](../../src/Convergence.Framework/Runtime/RuntimeEquipmentProfiles.cs#L218).
- The acquisition boundary already demonstrates the intended defensive pattern:
  [`ResourceManagementServices.cs`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L686)
  rejects the same requested/returned identity mismatch.

**Concrete consequence:** an instance owned as `equipment_a` can authorize a
skill granted only by `equipment_b`, use `equipment_b`'s basic attack, or apply
its combat contributions. Actor application and battle authorization then use
that false profile as authority. The standard `GameDataCatalog` cannot produce
this mismatch, but custom repositories are a supported public boundary.

**Reproduction:** supply a repository that returns a compatible weapon B when
queried for owned weapon A, then resolve A's equipped instance. The current
result has no diagnostic and exposes B's `GrantedSkillIds` and `BasicAttack`.

**Required correction:** reject requested/returned equipment ID mismatch with a
typed profile diagnostic before any returned definition field is read. Add
focused coverage through direct profile resolution, actor application,
execution authorization, and aggregate restore.

### O7-M2: Shop Item Offers Can Adopt Another Item's Stack Limit

**Severity:** Medium

**Intended invariant:** a resolved shop offer's content ID, catalog definition,
and executable constraints must describe the same authored item.

**Reachable path:**

1. an authored offer references item `item_a`;
2. a custom `IItemDefinitionRepository` returns `item_b` for the `item_a`
   lookup;
3. `RuntimeShopOfferResolver` retains content ID `item_a` but copies
   `item_b.StackLimit` into the runtime offer;
4. purchase adds `item_a` using the copied limit from `item_b`.

**Source evidence:**

- [`ResourceManagementServices.cs`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L1325)
  accepts any non-null item returned for the requested offer ID and copies its
  stack limit without an identity check.
- [`ResourceManagementServices.cs`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L1377)
  constructs the runtime offer with the authored content ID and that detached
  stack limit.
- [`ResourceManagementServices.cs`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L1824)
  later adds the authored content ID using the runtime offer's stack limit.

**Concrete consequence:** a permissive substituted definition can let purchases
of `item_a` exceed `item_a`'s authored stack limit. The corresponding equipment
lookup also accepts substituted definitions during offer resolution, although
equipment acquisition later performs an additional identity check and rejects
the purchase.

**Reproduction:** author item A with stack limit 1, return item B with stack
limit 99 for A's lookup, resolve the offer, and buy into inventory already
holding A. The current runtime offer exposes 99 and the purchase can accept a
second A.

**Required correction:** require the returned item/equipment definition ID to
equal the offer content ID and return a typed offer-resolution diagnostic on
mismatch. Test both content kinds and prove the item stack limit cannot cross
definition identity.

### O7-M3: Equipment-Backed Authorization Converts Cancellation Into Gameplay Failure

**Severity:** Medium

**Intended invariant:** `OperationCanceledException` is a host-control signal.
It must propagate through policy and action boundaries rather than become a
rejected gameplay command.

**Reachable path:**

1. a skill or basic attack resolves the actor's current equipment profile;
2. the selected `IEquipmentSlotLayoutPolicy` throws
   `OperationCanceledException` while validating that profile;
3. the equipment resolver correctly rethrows cancellation;
4. `BattleActionExecutor` catches it as a general `Exception` during assessment
   or execution-time reauthorization and returns `ExecutionFailed` instead.

**Source evidence:**

- Equipment-derived skill authorization resolves the current profile in
  [`BattleActionAuthorization.cs`](../../src/Convergence.Framework/Execution/BattleActionAuthorization.cs#L177),
  while equipment-derived basic attacks resolve it in
  [`BattleActionAuthorization.cs`](../../src/Convergence.Framework/Execution/BattleActionAuthorization.cs#L122).
- [`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L519)
  wraps all assessment exceptions, including cancellation, as
  `BattleActionDiagnosticCode.ExecutionFailed`.
- [`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L650)
  repeats that normalization during execution-time reauthorization.
- The direct slot-policy boundary already propagates cancellation and is covered
  by `EquipmentSlotLayoutTests`; the missing coverage is the canonical action
  facade that consumes the profile.

**Concrete consequence:** a host cancellation request can be displayed or
handled as an ordinary unusable action. An encounter adapter may continue its
gameplay flow instead of unwinding cancellation, and the host loses the ability
to distinguish user/system cancellation from a rule rejection.

**Documentation contradiction:**
[`typed-actions-and-effects.md`](../developer-guide/typed-actions-and-effects.md#L156)
states that cancellation is a host signal, and
[`inventory-equipment-and-economy.md`](../developer-guide/inventory-equipment-and-economy.md#L112)
states that slot-policy cancellation propagates. Those are the approved intent;
the executor currently violates it.

**Required correction:** add explicit cancellation rethrow behavior before the
general assessment and reauthorization catches. Cover direct assessment,
execution-time reauthorization, and the encounter-facing adapter path, while
proving no resource, inventory, actor, or turn mutation occurs.

## Confirmed Healthy Boundaries

The fresh trace also confirmed the following current strengths:

- `RuntimeInventorySnapshot` remains the sole equipment owner; actor loadouts
  hold only instance references and save v19 has no second root owner.
- equipment instance IDs, actor/equipment collisions, missing ownership, local
  duplicates, and cross-actor multiply-equipped state are rejected atomically;
- the selected slot policy is shared across content validation, acquisition,
  transitions, profile resolution, shops, save validation, and restoration;
- equipment grants remain derived, do not enter learned skills, and are
  rechecked before execution;
- Defense and Evasion are additive inputs to the canonical combat formulas,
  with an exact no-equipment zero contribution;
- currency operations name an explicit `ContentId`, preserve unrelated
  balances, and use checked arithmetic;
- shop purchase/resale stage inventory, currency, and stock as one immutable
  candidate; rejected operations preserve all three before-states;
- recovery stages actor cleanup and currency debit before committing live actor
  state; and
- aggregate restore validates the complete equipment/currency/stock graph before
  exposing a session.

No evidence was found that these standard-path guarantees regressed.

## Documentation Cross-Check

The three Order 7 audience documents accurately describe the approved target
mechanics and the standard in-repository path. They do not need to be rewritten
to make either defect look intentional. Their `reviewed` certification and the
active `complete` capability statements are stale after this audit, however.

Until the corrections are implemented and re-reviewed:

- `inventory_equipment_economy` is `partial`;
- mechanics, developer, and technical Order 7 entries are
  `existing_unreviewed`;
- the audience prose remains the intended contract; and
- O7-R15 remains historical evidence for its reviewed revision, not current
  closure authority.

## Executed Evidence

The unmodified `ef4e129e` baseline produced:

- focused Order 7 Framework tests: 182 passed, 0 failed, 0 skipped;
- focused Training Annex DemoHost tests: 117 passed, 0 failed, 0 skipped; and
- full solution: 1,847 Framework + 184 DemoHost + 7 ContentValidator = 2,038
  passed, 0 failed, 0 skipped.

The green suite corroborates the standard path. It does not invalidate these
findings because no current test supplies a wrong-ID repository through profile
or shop resolution, and no action-facade test combines equipment-backed
authorization with a cancelling slot policy.

## Correction Roadmap

Each checkpoint is an isolated commit and receives focused adversarial tests,
the full solution gate, strict build, formatting, documentation checks, and
`git diff --check`.

### O7-R16: Enforce Equipment Definition Identity

- Reject profile lookups whose returned definition ID differs from the owned
  instance's definition ID.
- Add a distinct typed diagnostic and public API baseline update if required.
- Prove direct profile, actor application, battle authorization, and restore all
  reject without applying substituted grants or combat contributions.

### O7-R17: Enforce Shop Content Definition Identity

- Reject item and equipment lookups whose returned ID differs from the offer's
  authored content ID.
- Add typed offer-resolution diagnostics.
- Prove a substituted item cannot contribute its stack limit and a substituted
  equipment definition cannot produce a resolved offer.

### O7-R18: Preserve Authorization Cancellation

- Rethrow `OperationCanceledException` from assessment and execution-time
  authorization while retaining typed containment for non-cancellation faults.
- Prove assessment and reauthorization cancellation preserve actor, resource,
  inventory, assessment, and turn state.
- Prove the encounter-facing action path observes cancellation rather than an
  ordinary gameplay rejection.

### O7-R19: Reconcile Active Documentation And Tracking

- Document canonical repository-identity validation and action cancellation at
  all three audience levels where applicable.
- Re-run code examples and documentation contract tests.
- Keep the capability `partial` and audience entries `existing_unreviewed`
  until independent closure.

### O7-R20: Fresh Independent Closure

- Re-read current Framework, persistence, DemoHost, Godot-contract, tests, and
  all three audience documents without treating this report as implementation
  evidence.
- Run and retain the complete release gate.
- Return Order 7 to `complete`/`reviewed` only if no realistic reachable defect
  or documentation contradiction remains.

## Closure Decision

**Order 7 is not ready for formal closure at `ef4e129e`.** The standard resource
management implementation is substantial and healthy, but the three public
extension-boundary gaps above must be corrected before the capability can be
certified complete.
