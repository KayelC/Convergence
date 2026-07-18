# Product Roadmap

The [Production-Readiness Completion Roadmap](production-readiness-roadmap.md)
and its [consolidated source review](../reviews/convergence-production-readiness-consolidated-review-2026-07-16.md)
are complete and verified. The priorities below now govern forward development;
the completed release record remains active evidence for the guarded `0.1.0`
baseline.

## Current State

Phase 8 established the clean product boundary. Framework, DemoHost, tests, and generic content now build independently of the archived prototype. The matrix currently records 25 capabilities: 23 complete, 0 partial, and 2 deferred.

The `typed_action_and_effect_execution` inventory and actor-action authority
rules are confirmed, implemented, source-reviewed, and owner-approved in
[Battle Action Ownership And Inventory Authority](../decisions/battle-action-ownership-and-inventory-authority.md),
including canonical skill, item, and basic-attack authority. The completed
stat-modifier family supplies persistent staged, timed exclusive, and
independently timed contribution policies. The closure record is maintained
under [Completed Order 1](documentation-completion-roadmap.md#completed-order-1).

The [Terminology Boundary](../terminology-boundary.md) checkpoint is complete. Active contracts use Action Token, Vessel, Hosted Entity, Companion, roster, schema-v4, and save-v10 vocabulary; an executable token-aware guard prevents retired names from returning outside the historical archive. Vessel combat profiles now come from an explicit source policy, aggregate restoration derives the Active Hosted Entity from the canonical party roster, and retained stat modifiers bind to their authored policy during validation and restore.

## Completed Actor Design Correction

The source-based collaborative actor review identified confirmed product
direction for complete Hosted Entity combat composition, runtime skill
unlocking, one authoritative roster aggregate, unambiguous encounter presence,
explicit command authority, and meaningful configurable stage magnitude.

The ordered work and its owner decision lock are recorded in the
[Actor Composition, Progression, Roster, And Stage Roadmap](actor-composition-progression-roster-roadmap.md).
D1-D6 are approved and all eight checkpoints are implemented. Current source,
tests, reviewed audience documentation, Training Annex evidence, and save
contract v10 establish the corrected design direction.

The subsequent
[Actor Runtime Completion Code Review](../reviews/actor-runtime-completion-code-review-2026-07-16.md)
found five medium integration gaps and one low direct-restore inconsistency.
All six are corrected with isolated commits and regression coverage. The
review did not invalidate the D1-D6 design.

## Completed Actor Runtime Review Corrections

Correct the completion-review findings in order:

1. **complete:** remove the duplicated roster owner level and derive capacity
   from the current owner actor;
2. **complete:** unify complete live/save party aggregate validation;
3. **complete:** apply move-list capacity consistently during creation, growth,
   and restore;
4. **complete:** add a stale-state precondition to prepared growth;
5. **complete:** align direct actor restore with aggregate pending-skill
   validation;
6. **complete:** route the Godot reference save through aggregate restoration.

## Completed Semantic Correction

Catalyst rank shifting now uses explicit authored catalyst and target roles. It
moves the target by an exact offset within that target's catalog race, rejects
stale participant rank data, and returns a typed no-fusion result when an exact
destination does not exist. Schema v4 retains the explicit catalyst/target
shape introduced by schema v3 and adds authored stat-modifier policy selection.

Authored rulesets now resolve through an explicit host-supplied typed factory
registry. The standard damage factory exposes every existing combat setting,
roster tiers and Action Token liveness are authored, and the supplied fixed
growth/stat/reward/economy policies can be replaced by registering another
factory. Moon phase remains absent from the standard composition.

## Completed Release Foundations

The strict Draft 2020-12 schema-v4 set now covers every implemented content
family, the authoring validator CLI combines schema and semantic gates, the
0.1 API has a textual baseline, and a real Godot 4.7.1 sample proves source
integration. The consolidated quality gate and independent final review are
complete. The review demonstrated one encounter resource-event defect,
corrected it with exact signed mutation records, and found no unresolved release
blocker.

## Priority 1: Collaborative Documentation Completion

Complete the active
[Documentation Completion Roadmap](documentation-completion-roadmap.md) one
capability at a time. Source and tests establish current behavior; the project
owner confirms intended mechanics and extension boundaries before an audience
entry becomes `reviewed`. Existing prose must not be bulk-promoted. Order 1 is
complete; Order 2, `combat_resolution`, is next.

## Priority 2: Persistence Evolution

Define save-contract migration only when a released contract actually requires it. Full deterministic replay remains optional; checkpoint breadcrumbs are currently diagnostics rather than replay authority.

## Priority 3: Example Breadth

Expand original example content only when it demonstrates a framework contract or reveals a missing reusable rule. DemoHost remains optional reference software, not the product architecture driver.

## Decision Rule

New work should answer one of these questions:

1. Does Framework lack a reusable rule or contract?
2. Does a real host expose an integration gap?
3. Does authoring need clearer validation or tooling?
4. Is a public API ready to stabilize?

Presentation-only work belongs in a host project and should not delay framework completion.
