# Order 2 Pre-Closure Audit Corrections Roadmap

**Status:** open

**Started:** 21 July 2026

**Capability:** `combat_resolution`

## Purpose

The fresh
[Order 2 Pre-Closure Code And Documentation Review](../reviews/combat-resolution-order-2-pre-closure-code-and-documentation-review-2026-07-21.md)
found three current-source discrepancies after revision `e26bdc5`. This roadmap
keeps those findings explicit until code, tests, schemas, documents, and a
source-first re-evaluation agree.

It does not redesign the approved damage formula, accuracy, criticals,
affinities, charges, multi-hit behavior, secondary-effect dependencies, or
Action Token strategy.

## Ordered Checkpoints

### O2-R24: Make aggregate skill-cost legality order-invariant

**State:** open; owner decision required before implementation

- Decide whether duplicate cost entries for one `resourceId` are rejected or
  deliberately aggregated.
- Recommended supplied contract: reject duplicate resource IDs in authored and
  programmatic preflight.
- If additive entries are retained, aggregate the permission as well as the
  amount: any component that forbids zero makes the complete obligation forbid
  zero.
- Test both list orders, mixed permission flags, HP and SP, assessment,
  prepared-state revalidation, commit, and rollback.

**Planned commit:** `execution: make aggregate skill costs order invariant`

### O2-R25: Validate host turn-consumption contracts

**State:** open

- Give `ActionTurnConsumption` validated legal shapes.
- Validate `TurnEconomyResolution.Outcome` and preserve the invariant through
  record cloning or remove unsafe cloning from the contract.
- Reject undefined encounter command statuses and requested outcomes.
- Prove malformed host results fault or reject before economy and lifecycle
  mutation; preserve every legal supplied and custom-economy path.

**Planned commit:** `battle: validate host turn consumption contracts`

### O2-R26: Resolve the party-size zero contract

**State:** open; owner decision required before implementation

- Confirm whether `party_size = 0` is a valid "no deployed living team member"
  condition.
- Align Draft 2020-12 schema, semantic validation, runtime semantics, and
  audience documentation with that decision.
- Add independent schema and semantic boundary tests for the selected minimum.

**Planned commit:** `schema: align party size condition bounds`

### O2-R27: Reconcile documentation and re-evaluate closure

**State:** open

- Confirm whether prepared skill cost amounts are intentionally quote-locked
  between single-use assessment and execution.
- Update mechanics, developer, technical, decision, API, and roadmap text from
  the corrected source.
- Re-read the corrected implementation without treating this roadmap or prior
  reports as proof.
- Run focused tests, the complete suite, strict builds, formatting, active
  content validation, all DemoHost modes, Godot contracts/smoke, coverage,
  documentation links, boundary searches, and `git diff --check`.
- Return each affected capability to `complete` only if no realistic reachable
  defect remains in its reviewed paths.

**Planned commit:** `docs: reverify order 2 closure`

## Review Standard

Each correction must identify its intended invariant, exercise a realistic
supported path, prove unchanged state on rejection, and receive an isolated
green commit. Purely theoretical misuse and alternative game designs remain
separate from defects.
