# Typed Action And Effect Execution Order 1 Post-Correction Review

**Review date:** 17 July 2026

**Reviewed branch:** `main`

**Reviewed range:** `8af71e1..36c8ea8`

**Capability:** `typed_action_and_effect_execution`

## Verdict

No open code finding remains in the approved Order 1 contract. O1-M1, O1-M2,
O1-D1, and O1-D2 are implemented correctly and have direct negative and
positive coverage. The capability is suitable for `complete` implementation
status.

One DemoHost false-positive discovered during the review was corrected in
`38c47d9`: field-demo actors were marked as not deployed, so targeted actions
were rejected even though the host printed a success footer. The corrected demo
now proves skill recovery, item recovery, ailment removal, revival, escape, and
host-action requests through actual executed results.

The mechanics, developer, and technical documents accurately describe current
source. Following the final documentation audit and project-owner confirmation,
all three audience entries have completed collaborative review.

## Review Method

This pass inspected current source and tests rather than relying on the first
review's conclusions. It traced:

- every production and sample construction of `BattleActionExecutor`;
- `CatalogBattleActionAuthorizationPolicy` skill and basic-attack decisions;
- assessment ownership, single-use consumption, and execution-time
  reauthorization;
- prepared-target rebinding and stale-state rejection;
- skill cost staging and ordered effect outcomes;
- item availability, reserve, reservation validation, commit, rollback,
  cancellation, and actor-state publication;
- `RuntimeActorExecutionTransaction` nested skill/item mutation behavior;
- DemoHost field, battle, Training Annex, and Godot sample composition;
- public API baseline and host-neutrality gates;
- focused, full-solution, strict-build, formatting, demo, link, and terminology
  verification.

## Verified Invariants

### O1-M1: One command consumes one item

`ItemBattleActionCommand` has no quantity property. The canonical facade uses
the fixed quantity one for availability, reservation, validation, and commit.
The regression starts with two units and proves one remains after one meaningful
execution.

### O1-M2: Reservation identity is checked before effects

The facade rejects null, wrong-item, wrong-quantity, already-committed, and
already-rolled-back reservations before item effects can publish actor state.
Live mismatched reservations receive a rollback attempt. Commit rejection and
effect exceptions also leave live actor state unchanged.

### O1-D1: Inventory authority is mandatory

An item assessment without `IItemActionInventory` receives
`ItemInventoryRequired`, cannot execute, consumes no turn, and mutates no actor.
`ItemExecutor` remains intentionally lower-level and has no inventory claim.

### O1-D2: Actor action authority is Framework-owned

Skills require both an equipped runtime ID and the exact canonical repository
definition. Basic attacks require a profile and matching source ID, damage
definition, and targeting. Authorization runs during assessment and again
before dispatch. Regression coverage removes an equipped skill and removes a
basic-attack profile after assessment; both stale commands reject without
damage or turn consumption.

## Review Finding Corrected

### R1: Field demo success did not prove targeted execution

**Severity:** medium for verification integrity, not a Framework rule defect.

**Invariant:** a successful field-effects demo must execute its advertised
targeted effects.

**Reachable path:** both field actors were created with `IsDeployed: false`.
`RuntimeTargetResolver` correctly excludes non-deployed actors, so recovery,
cure, and revival commands rejected. The application test asserted only the
final success footer.

**Consequence:** the release gate could remain green while the demo proved none
of its principal targeted operations.

**Correction:** `38c47d9` creates the active field party as deployed and asserts
the exact executed output for recovery skill, recovery item, ailment cure, and
revival. The Framework target resolver was not changed.

## Explicit Trusted Boundaries

These are intentional contracts, not unresolved findings:

- `IItemActionInventory` is a trusted host transaction port. Convergence checks
  its observable reservation identity and lifecycle but cannot inspect hidden
  adapter state.
- Item ownership is validated by content ID. The host supplies the
  `ItemDefinition`; the current facade does not perform an independent item
  repository identity check. Catalog-backed hosts are documented to use the
  catalog definition.
- Custom authorization policies may intentionally grant actions that the
  supplied catalog policy rejects.
- Direct `SkillExecutor` and `ItemExecutor` calls deliberately omit loadout or
  inventory authority respectively.
- External side effects performed by custom/host callbacks are outside the
  Framework actor transaction.

None of these boundaries contradicts the owner-approved Order 1 decision. A
future change to canonical item-definition identity would be a separate policy
decision rather than a hidden bug fix.

## Verification

The final verification gate records:

- 872 Framework tests, 168 DemoHost tests, and 7 validator tests;
- 1,047 total passing tests, zero failures, and zero skips;
- strict .NET 8 Release build with zero warnings and zero errors;
- formatting verification and `git diff --check` clean;
- clean battle, field, save, and Training Annex demos successful;
- scripted Training Annex coverage retained by DemoHost tests;
- active documentation links and terminology boundary checks green;
- Framework public API remains free of console, filesystem, serializer, Godot,
  DemoHost, and archived runtime types;
- the Godot project compiled in the strict solution build and its contract tests
  passed. The local Windows 4.7.1 engine smoke was attempted twice but the
  native executable faulted before project load, including with log redirection;
  this environment failure is not reported as a successful runtime smoke.

## Completion State

- O1-M1: `verified`
- O1-M2: `verified`
- O1-D1: `verified`
- O1-D2: `verified`
- O1-DOC: `verified`

Order 1 implementation and documentation are complete. The mechanics,
developer-guide, and technical coverage states are promoted from
`existing_unreviewed` to `reviewed`; Order 2, `combat_resolution`, is next.
