# Inventory, Equipment, And Economy Order 7 Post-Correction Independent Audit

**Date:** 24 August 2026

**Reviewed commit:** `a21a6dcb` (`test: retain order 7 post-correction evidence`)

**Capability:** `inventory_equipment_economy`

**Verdict:** **reopened; O7-R12 and O7-R13 are complete, while the developer sample correction and fresh closure review remain**

## Review Method

This audit started from the current implementation, exported API, active
content, strict schemas, Framework and host tests, DemoHost/Godot consumers,
and the three active audience documents. Earlier Order 7 reports and completion
summaries were not used as evidence of correctness.

The source trace covered:

- inventory and exact equipment-instance ownership;
- authored equipment slots and policy-fault containment;
- live equipment profiles, actor composition, action authorization, and combat
  Defense/Evasion consumption;
- typed currency ledgers, Compendium transactions, pricing, shop stock, and
  atomic shop candidates;
- generic recovery planning and execution;
- save v19 validation, aggregate restoration, DemoHost serialization, and
  Godot-owned serialization; and
- the player, developer, technical, public-API, and roadmap documentation.

A finding qualifies only when it has an intended invariant, a supported and
realistic reachable path, a concrete consequence, and direct source evidence.
Impossible gameplay values, already-documented host responsibilities, and
alternative product designs are not reported as vulnerabilities.

## Findings

### M1. A malformed custom economy factory can bind an incomplete service bundle as success

**Correction status:** corrected by O7-R12 in `59039b57`.

**Classification:** medium robustness defect at a public host-extension
boundary; not a player-input security vulnerability.

**Intended invariant:** a successful economy-ruleset binding exposes a complete
usable `ResourceManagementRulesetServices` bundle. Malformed custom factory
output must become `PolicyFactoryFailure`, matching the typed containment
already supplied for exceptions, malformed diagnostics, pricing, stock,
recovery, and equipment-slot policies.

**Reachable path:**

1. [`ResourceManagementRulesetServices`](../../src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs#L106)
   is a public positional record. Its generated init properties permit record
   cloning, and neither construction nor cloning rejects a null required
   subservice.
2. A registered `IRuntimeEconomyRulesetPolicyFactory` can therefore return a
   non-null bundle such as `validServices with { Inventory = null! }` inside a
   result with no diagnostics.
3. The generic binder at
   [`RuntimeRulesetBindingResolver.Bind`](../../src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs#L266)
   validates only the top-level result/service/diagnostic shape. At lines
   345-347 it returns that incomplete bundle as successful.
4. `RequireService()` succeeds, and the first ordinary use such as
   `resources.Inventory.AddItem(...)` throws `NullReferenceException` outside
   the typed ruleset-binding boundary.

The supplied standard economy factory always builds a complete bundle, so the
normal Training Annex and Godot paths are unaffected. The defect is still
reachable through the advertised custom economy-factory seam, including from
nullable-oblivious .NET code or record cloning. Existing malformed-economy
tests cover invalid diagnostics and thrown factories, but not a non-null
service bundle with an invalid internal shape.

**Consequence:** startup can report a custom economy ruleset as successfully
bound even though the returned service aggregate cannot be used. The failure
is delayed and untyped, weakening the same extension-boundary guarantee Order 7
otherwise applies consistently.

**Required correction:** replace the positional aggregate with a sealed,
get-only shape whose constructor validates all five required services while
retaining nullable `Recovery`. Ensure a malformed custom factory result is
translated into the existing `PolicyFactoryFailure` diagnostic rather than
escaping or binding successfully. Preserve cancellation propagation and valid
custom factories.

Required regressions:

- constructor and record-cloning routes cannot create an incomplete bundle;
- every required subservice is covered;
- the resolver returns one `PolicyFactoryFailure` and no service for malformed
  custom output;
- valid custom and standard economy bundles still bind by identity; and
- `OperationCanceledException` still propagates.

### L1. The three audience documents still describe O7-R11 as future work

**Correction status:** corrected by O7-R13. The pages remain
`existing_unreviewed` until O7-R15 rechecks their complete authority.

**Intended invariant:** active audience documents must report the same review
state as the executable capability and product roadmaps.

**Direct contradiction:**

- [`inventory-equipment-economy-runtime.md`](../technical/inventory-equipment-economy-runtime.md#L12)
  and
  [`inventory-equipment-and-economy.md`](../developer-guide/inventory-equipment-and-economy.md#L10)
  say O7-R11 remains to be performed;
- [`party-inventory-and-economy.md`](../mechanics/party-inventory-and-economy.md#L3)
  says the capability remains `partial` until O7-R11; while
- the executable capability matrix and product roadmap currently record O7-R11
  and its post-correction gate as complete.

**Consequence:** a developer reading the active audience pages receives a
different maturity answer from active tracking. Runtime mechanics are not
affected, but formal documentation closure is not truthful while the
contradiction remains.

**Required correction:** after M1 is corrected, update all three callouts to
the new reviewed revision and closure result together. Keep the pages
`existing_unreviewed` until that source-to-document reconciliation is
performed.

### L2. The documented shop purchase example does not compile against the current API

**Intended invariant:** developer-guide examples must use the supported public
contract and show every load-bearing authority required by an operation.

**Direct contradiction:** the purchase example at
[`inventory-equipment-and-economy.md`](../developer-guide/inventory-equipment-and-economy.md#L300)
passes seven arguments to `resources.Shop.Buy`. The current
[`IShopTransactionService.Buy`](../../src/Convergence.Framework/Runtime/ResourceManagementServices.cs#L1622)
requires eight: the final `RuntimeEquipmentAcquisitionContext?` carries the
equipment repository and complete live actor-ID evidence. No compatibility
overload or default parameter exists.

**Consequence:** copied sample code does not compile. More importantly, the
example omits the authority that prevents an equipment purchase from accepting
a missing definition, incompatible slot, or actor/equipment runtime-ID
collision.

**Required correction:** show an equipment purchase with both a fresh instance
ID and a `RuntimeEquipmentAcquisitionContext`, and explicitly show item
purchases passing `null` for both optional equipment arguments. Add an
executable documentation guard for the current eight-argument contract so this
sample cannot silently drift again.

## Source-Verified Health

No additional realistic reachable Order 7 defect was found in the following
paths:

1. **Ownership:** inventory remains the sole owner of immutable equipment
   instances. Live transitions and aggregate validation reject duplicate,
   missing, actor-colliding, and multiply equipped instance IDs.
2. **Slots:** `ContentId` slot identity and the selected
   `IEquipmentSlotLayoutPolicy` are used by content validation, acquisition,
   equip/unequip, profile resolution, offer resolution, and save validation.
3. **Equipment contribution:** one profile resolver derives weapon attacks,
   temporary grants, Defense, Evasion, and accessory modifiers. Canonical actor
   application stages on a clone and commits only after complete evidence
   passes. No-equipment contributions remain a true zero-value no-op.
4. **Authorization:** learned skills are not modified by equipment grants, and
   action execution performs a fresh authorization check against the current
   equipment source.
5. **Currency:** every transaction names a currency; construction and
   transitions reject invalid, duplicate, missing, negative, and overflowing
   states. The single-currency accessor explicitly rejects empty and ambiguous
   ledgers.
6. **Shops:** resolved offers are read-only, pricing and stock policies are
   explicit, and accepted purchases/resales return inventory, currency, and
   stock candidates together. Rejections retain all supplied before-state.
7. **Recovery:** assessment is hypothetical; execution stages actor cleanup and
   commits only after the named-currency debit succeeds. Protected state and
   cancellation retain their typed meanings.
8. **Persistence and hosts:** save v19 has one equipment owner, actor loadout
   references, currency balances, and stock. Validation precedes aggregate
   restore, which exposes no partial session. DemoHost and Godot carry the same
   authorities without moving serializer or engine types into Framework.

The documented requirements that a host supply complete current actor/loadout
evidence and serialize or compare-and-swap immutable shop candidates remain
intentional stateless integration boundaries, not Framework defects.

## Fresh Verification

Executed against unmodified commit `a21a6dcb`:

| Gate | Result |
|---|---|
| Focused Order 7 Framework tests | 236 passed; 0 failed; 0 skipped |
| Full `dotnet test Convergence.sln --no-restore` | 1,833 Framework + 184 DemoHost + 7 ContentValidator = 2,024 passed; 0 failed; 0 skipped |
| Strict nonincremental solution build with warnings as errors | passed; 0 warnings; 0 errors |
| `dotnet format --verify-no-changes` | passed; 0 files changed |
| `git diff --check` | passed |

Green tests demonstrate the supported standard paths and existing adversarial
coverage. They do not invalidate M1 because no test constructs an incomplete
non-null `ResourceManagementRulesetServices` bundle, and they do not compile
Markdown samples. L1 and L2 were established by direct source/document
comparison.

## Correction Roadmap

| Checkpoint | Status | Work | Completion gate |
|---|---|---|---|
| O7-R12 | Complete (`59039b57`) | Harden `ResourceManagementRulesetServices` and contain malformed custom economy bundles. | Focused ruleset tests prove every required service, typed failure, valid custom/standard binding, and cancellation. |
| O7-R13 | Complete | Reconcile the mechanics, developer, and technical review-state callouts. | All three pages identify the same current correction state and no stale O7-R11-future wording remains. |
| O7-R14 | Pending | Correct and guard the `Shop.Buy` developer example. | The example carries acquisition context, distinguishes item/equipment calls, and a documentation contract test pins the public signature. |
| O7-R15 | Pending | Perform a fresh source/document re-evaluation and full release gate. | No unresolved realistic reachable defect or documentation contradiction; capability and three audience entries may return to `complete`/`reviewed`. |

Each checkpoint is an isolated commit. Runtime save contract v19 and content
schema v10 need not change: M1 hardens a public construction/binding boundary,
and L1/L2 are documentation corrections.

## Closure Verdict

Order 7's owner-approved mechanics are substantially implemented and its
standard Framework, DemoHost, and Godot paths are healthy. O7-R12 has removed
the false-success custom economy-bundle path, and O7-R13 has reconciled the
three stale review-state callouts. Formal closure is still premature because
the developer purchase example remains incompatible with the public API and a
fresh source/document closure review has not yet been performed.

`inventory_equipment_economy` remains `partial`. Its mechanics, developer, and
technical documentation entries remain `existing_unreviewed` until O7-R14 and
O7-R15 are completed and independently rechecked.

## Correction Completion Record

### O7-R12: Harden Economy Service Bundles

- **Commit:** `59039b57` (`runtime: validate economy service bundles`).
- **Runtime correction:** `ResourceManagementRulesetServices` is now a sealed,
  get-only aggregate. Its constructor rejects a missing inventory, equipment,
  economy, shop-offer, or shop service; recovery remains intentionally
  optional.
- **Binding correction:** a custom economy factory that attempts any incomplete
  construction now throws inside the factory invocation and is contained as
  one `RulesetBindingDiagnosticCode.PolicyFactoryFailure`. Cancellation remains
  exempt from that catch and propagates unchanged.
- **API correction:** record cloning, init setters, deconstruction, and
  value-equality members were removed from the pre-release API baseline. The
  validated constructor and six get-only properties are the supported shape.
- **Evidence:** 67 focused `RuntimeRulesetBindingTests` passed, including every
  required member, non-record/get-only shape, typed malformed-factory failure,
  valid custom identity, standard binding, and economy cancellation. The full
  suite passed 1,846 Framework + 184 DemoHost + 7 ContentValidator tests:
  2,037 total, 0 failed, 0 skipped. Strict Release build, formatting, and diff
  checks passed with 0 warnings and 0 errors.
- **Versions:** save contract v19 and content schema v10 are unchanged.

### O7-R13: Reconcile Audience Review State

- **Baseline:** `59039b57`; this checkpoint's documentation commit records the
  final hash.
- **Audience correction:** the mechanics, developer, and technical pages now
  agree that O7-R11 completed against `a21a6dcb`, the independent audit at
  `6f4b2f0c` reopened the capability, O7-R12 corrected its runtime finding, and
  O7-R14/O7-R15 remain.
- **Tracking correction:** the executable capability matrix removes the fixed
  runtime defect and stale-callout defect, retaining only the developer sample
  gap. All three documentation entries remain `existing_unreviewed` pending
  the sample correction and fresh closure audit.
- **Scope:** no runtime, save, schema, content, host, or gameplay behavior
  changes in O7-R13.
