# Order 2 Pre-Closure Audit Corrections Roadmap

**Status:** open; O2-R24 through O2-R28 implemented, O2-R29 pending

**Started:** 21 July 2026

**Capability:** `combat_resolution`

## Purpose

The fresh
[Order 2 Pre-Closure Code And Documentation Review](../reviews/combat-resolution-order-2-pre-closure-code-and-documentation-review-2026-07-21.md)
found three current-source discrepancies after revision `e26bdc5`. This roadmap
keeps those findings explicit until code, tests, schemas, documents, and a
source-first re-evaluation agree.

The post-R27 source recheck found one additional supported host-extension
boundary defect. Its evidence is recorded in the
[Post-R27 Source Review](../reviews/combat-resolution-order-2-post-r27-source-review-2026-07-21.md).

It does not redesign the approved damage formula, accuracy, criticals,
affinities, charges, multi-hit behavior, secondary-effect dependencies, or
Action Token strategy.

## Ordered Checkpoints

### O2-R24: Make aggregate skill-cost legality order-invariant

**State:** implemented pending review

- One skill may declare at most one cost for a given `resourceId`.
- Semantic content validation rejects repeated resources, including a local ID
  and its same-pack qualified alias.
- Programmatic runtime assessment rejects duplicates before target randomness,
  amount resolution, mutation, or turn consumption.
- Regression coverage exercises both list orders, mixed zero-floor flags, HP,
  SP, rejected execution, and unchanged actor/target state.

**Planned commit:** `execution: make aggregate skill costs order invariant`

### O2-R25: Validate host turn-consumption contracts

**State:** implemented pending review

- `ActionTurnConsumption` and `TurnEconomyResolution` now validate construction
  and expose get-only state, so record cloning cannot create a different shape.
- Encounter command results reject undefined statuses/outcomes, missing turn
  consumption, invalid winning-team IDs, and null event entries.
- Supplied economies handle every legal kind explicitly and throw rather than
  reinterpret an impossible future value.
- Encounter regression coverage proves malformed host construction becomes a
  typed turn-handler fault before economy or turn-end lifecycle mutation.

**Planned commit:** `battle: validate host turn consumption contracts`

### O2-R26: Resolve the party-size zero contract

**State:** implemented pending review

- `party_size = 0` is explicitly valid and means that the acting actor's team
  has no living deployed participant in the supplied battle context.
- Draft 2020-12 schema, semantic validation, runtime evaluation tests, and
  audience guidance now share that definition.
- Independent authoring-boundary tests reject negative counts.

**Planned commit:** `schema: align party size condition bounds`

### O2-R27: Reconcile documentation and re-evaluate closure

**State:** implementation and audience reconciliation complete; source
re-evaluation completed and found O2-M3

- Prepared skill cost amounts are intentionally quote-locked between single-use
  assessment and execution. Execution revalidates authored identity and current
  affordability but does not rerun formula or modifier policies.
- Mechanics, developer, technical, content-contract, decision, public-API, and
  roadmap text now describe the corrected source contract.
- Re-read the corrected implementation without treating this roadmap or prior
  reports as proof.
- Run focused tests, the complete suite, strict builds, formatting, active
  content validation, all DemoHost modes, Godot contracts/smoke, coverage,
  documentation links, boundary searches, and `git diff --check`.
- Return each affected capability to `complete` only if no realistic reachable
  defect remains in its reviewed paths.

**Planned commit:** `docs: reverify order 2 closure`

### O2-R28: Validate custom effect result contracts

**State:** complete

- Make `EffectExecutionResult` validate scalar assignments made by both its
  constructor and record cloning.
- Reject negative effect indexes, undefined execution/turn/optional enum
  values, invalid optional IDs, and null or invalid collection entries.
- Prove a malformed `ICustomEffectHandler` result becomes a typed pre-commit
  action rejection.
- Prove no actor, resource, charge, inventory, later effect, or turn-economy
  mutation escapes that rejection.
- Preserve every legal built-in and host-custom result shape.

`EffectExecutionResult` now validates constructor and record-clone assignments
for effect indexes, execution and turn outcomes, optional enum values, optional
runtime/content IDs, host request IDs, and reference collection entries. A
focused action regression executes a costed, three-effect skill whose middle
custom effect returns an undefined outcome. The action now returns the existing
typed rejection with no committed cost, target change, published effect, or
turn consumption.

Verification passed 157 focused tests and 1,450 solution tests with zero
failures or skips. The strict nonincremental Release build completed with zero
warnings and errors; formatting and `git diff --check` also passed.

**Planned commit:** `execution: validate custom effect results`

### O2-R29: Reconcile evidence and independently close Order 2

**State:** pending

- Re-read current combat and action source after O2-R28 without treating prior
  reports as implementation proof.
- Reproduce O2-R24 through O2-R28 at their public boundaries.
- Update all affected capability entries and active audience documentation from
  corrected source.
- Run the complete release gate and record exact evidence.

**Planned commit:** `docs: verify order 2 pre-closure corrections`

## Review Standard

Each correction must identify its intended invariant, exercise a realistic
supported path, prove unchanged state on rejection, and receive an isolated
green commit. Purely theoretical misuse and alternative game designs remain
separate from defects.
