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
