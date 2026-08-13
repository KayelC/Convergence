# Product Roadmap

The [Production-Readiness Completion Roadmap](production-readiness-roadmap.md)
and its [consolidated source review](../reviews/convergence-production-readiness-consolidated-review-2026-07-16.md)
are complete and verified. The priorities below now govern forward development;
the completed release record remains active evidence for the guarded `0.1.0`
baseline.

## Current State

Phase 8 established the clean product boundary. Framework, DemoHost, tests, and generic content now build independently of the archived prototype. The matrix currently records 25 capabilities: 22 complete, 1 partial, and 2 deferred.

Documentation Order 7 is open under the owner-approved
[Inventory, Equipment, And Economy source review and roadmap](../reviews/inventory-equipment-economy-order-7-source-review-2026-08-10.md).
The current resource-management foundation remains usable. O7-R2 establishes
equipment-instance ownership and removes the duplicated root save authority;
O7-R3 replaces fixed slot enums with authored slot IDs governed by an explicit
layout policy while preserving the standard four-slot behavior. O7-R4 routes
equipped-only skill grants, armor Defense, and armor/boots Evasion through one
canonical equipment profile and the existing action/damage/hit authorities.
O7-R5 establishes an immutable currency ledger keyed by `ContentId` and
explicit currency selection for every transaction. O7-R6 binds exact-authored-price standard
pricing and optional Luck-adjusted pricing through typed factories selected by
the economy ruleset or an explicit offer. O7-R7 gives offers stable identity,
persists policy-owned stock under save contract v19, and threads that state
through DemoHost and Godot-owned serialization. O7-R8 supplies optional generic
recovery with explicit resources, legal cleanup, cost, and currency authority;
Training Annex and Godot evidence use the same bound service. O7-R9 certifies
the combined runtime and wire graph across save/restore, equipment, offers,
currencies, stock, recovery, DemoHost, and Godot, and seals resolved offers
against host-side reconstruction. O7-R10 completes and reconciles all three
audience documents. The capability remains `partial` until O7-R11 completes
the independent adversarial closure audit.

`encounter_orchestration` has a substantial implemented foundation under the owner-approved
[Order 6 source review and roadmap](../reviews/encounter-orchestration-order-6-source-review-2026-07-30.md).
O6-R1 through O6-R13L implemented modular team-phase and Agility
scheduling, bounded immediate follow-ups, lifecycle reconciliation, complete
structural events, validated terminal shapes, cancellation certification,
canonical asynchronous automated execution, frozen-graph event validation,
complete automated terminal outcomes, and all three audience documents. The earlier
[final closure review](../reviews/encounter-orchestration-order-6-final-closure-review-2026-07-30.md)
records the state at that revision. The later
[O6-R14 fresh owner-closure audit](../reviews/encounter-orchestration-order-6-fresh-owner-closure-audit-2026-08-04.md)
reopened the capability after reproducing four realistic supported paths that
the green suite did not cover. O6-R15 through O6-R18 corrected those bounded
runtime paths, and O6-R19 reconciled their active documentation. O6-R20 then
found one actorless ordinary-action event shape. O6-R21 closes that shape and
O6-R22 reconciles the resulting contract. The
[O6-R23 final closure review](../reviews/encounter-orchestration-order-6-r23-final-closure-review-2026-08-04.md)
then re-read the corrected implementation and all three audience documents,
found no unresolved realistic reachable defect, and returned the capability to
`complete` after the full local gate passed.

The
[O6-R24 post-R23 independent audit](../reviews/encounter-orchestration-order-6-post-r23-independent-audit-2026-08-04.md)
then re-read current source and found a bounded public-result inconsistency:
normal completion text reaches the fault-only result property. The implemented
encounter architecture remains intact. O6-R25 has corrected and tested the
runtime result shape, and O6-R26 reconciles the audience and public API
guidance. The
[O6-R27 final closure review](../reviews/encounter-orchestration-order-6-r27-final-closure-review-2026-08-04.md)
independently re-read the corrected source and documents, recorded trusted
host-port boundaries, and restored formal closure.

The later
[O6-R28 post-R27 independent audit](../reviews/encounter-orchestration-order-6-post-r27-independent-audit-2026-08-04.md)
reproduced a limited-action identity bypass and duplicate explicit-plus-defeat
departure cleanup. The established architecture remains usable, but these two
public supplied-component paths must be corrected under O6-R29 through O6-R32
before Order 6 closes again.

O6-R29 and O6-R30 have now corrected both paths: restricted commands use their
typed canonical identity, and an explicit Flee or Roster Recall reason owns
cleanup for its complete uninterrupted defeat period. O6-R31 reconciles the
active documents and executable matrices. The
[O6-R32 final closure review](../reviews/encounter-orchestration-order-6-r32-final-closure-review-2026-08-04.md)
independently reread current source and documentation, reran the adversarial
paths and complete release gate, and found no unresolved realistic reachable
defect. Order 6 is formally closed again.

The later
[O6-R33 post-R32 independent audit](../reviews/encounter-orchestration-order-6-post-r32-independent-audit-2026-08-05.md)
did not treat that closure as current proof. It reproduced no-cost
turn-economy mutation and scheduler round drift through the public extension
contracts, plus one battle-start cleanup wording ambiguity. O6-R34 through
O6-R37 form the bounded correction and closure sequence. O6-R34 now enforces
strict no-cost economy authority, O6-R35 enforces scheduler structural
continuity before later gameplay can commit, and O6-R36 reconciles all active
guidance with those contracts and the structural `BattleStarted` cleanup
boundary. The
[O6-R37 final closure review](../reviews/encounter-orchestration-order-6-r37-final-closure-review-2026-08-05.md)
independently reread the corrected source, hostile paths, supplied schedulers,
and documentation, passed the complete gate, and found no unresolved realistic
reachable defect. Order 6 is formally complete.

The later
[O6-R38 post-R37 independent audit](../reviews/encounter-orchestration-order-6-post-r37-independent-audit-2026-08-05.md)
reproduced two bounded but reachable scheduler defects and one safety-limit
terminology ambiguity. Order 6 is reopened only for O6-R39 through O6-R42:
stable team-ring selection, economy-aware transition validation, documentation
reconciliation, and an independent closure gate. No unrelated capability is
demoted.

O6-R39 now preserves one stable team-participant ring across departure,
defeat, deployment, and immediate-repeat scheduling. O6-R40 rejects another
command window as soon as accepted economy evidence reports exhaustion, before
turn-start lifecycle or handler mutation. O6-R41 reconciles the three audience
documents and the pre-release `MaximumCommands` API/wire name with its actual
accepted turn-window safety semantics. The
[O6-R42 final closure review](../reviews/encounter-orchestration-order-6-r42-final-closure-review-2026-08-05.md)
independently reread current source, adversarial tests, supplied policies, and
all three audience documents. No unresolved realistic reachable defect was
found, and every locally executable release gate passed. Order 6 is formally
complete and `encounter_orchestration` returns to `complete`.

The later
[O6-R43 post-R42 independent audit](../reviews/encounter-orchestration-order-6-post-r42-independent-audit-2026-08-05.md)
reopened Order 6 after tracing two supported combined failure paths in event
publication and command-fault finalization, plus one developer-guide contract
name error. O6-R44 through O6-R47 now form the bounded correction and closure
sequence. At that audit revision, `encounter_orchestration` returned to
`partial` pending that sequence.

O6-R44 and O6-R45 correct both combined-failure paths. O6-R46 reconciles the
three audience documents, public API guidance, XML guidance, and exact exported
interface name. The
[O6-R47 final closure review](../reviews/encounter-orchestration-order-6-r47-final-closure-review-2026-08-05.md)
independently reread the corrected implementation and documentation, passed the
complete local gate, and found no unresolved realistic reachable defect. Order
6 is formally complete and `encounter_orchestration` returns to `complete`.

`status_and_passive_lifecycle` is complete. The
[26 July fresh closure audit](../reviews/status-passive-lifecycle-order-4-fresh-closure-audit-2026-07-26.md)
reproduced stale execution by a removed ailment and definition-incoherent
passive activation restore keys. O4-R18 and O4-R19 corrected those runtime
paths, and O4-R20 reconciled the audience documentation. O4-R21 was the sole
closure gate named by that audit, but its source trace reproduced two
additional integration defects. The
[R21 extension audit](../reviews/status-passive-lifecycle-order-4-r21-extension-audit-2026-07-26.md)
governs the extension. O4-R22 through O4-R25 have now recorded and corrected
turn-start exact-instance scheduling, encounter-owned departure cleanup, and
all three documentation audiences. O4-R26 completed the independent review but
rejected closure after finding four additional passive and restore correctness
paths. The
[R26 correction audit](../reviews/status-passive-lifecycle-order-4-r26-correction-audit-2026-07-26.md)
governs O4-R27 through O4-R32. O4-R27 through O4-R30 corrected all four
runtime paths, and O4-R31 reconciled active guidance; the capability stays
current. The
[final O4-R32 closure review](../reviews/status-passive-lifecycle-order-4-final-closure-review-2026-07-26.md)
independently traced the corrected implementation and documentation without
finding an unresolved realistic reachable defect at that revision. A
[second independent audit](../reviews/status-passive-lifecycle-order-4-second-independent-audit-2026-07-26.md)
subsequently reproduced cancellation-before-commit at two lifecycle boundaries
and unevidenced mutation through a replacement passive dispatcher. It also
found that ailment combat-profile composition is omitted from all three
audience documents. O4-R33 through O4-R36 governed correction and closure.
O4-R33 added cancellation checks at every encounter lifecycle commit, O4-R34
requires executed passive evidence before replacement mutations commit, and
O4-R35 documented and tested exact ailment combat-profile composition. The
[O4-R36 closure review](../reviews/status-passive-lifecycle-order-4-r36-closure-review-2026-07-26.md)
then re-read the corrected source and all three audiences and closed Order 4
without an unresolved realistic reachable defect at that revision. The
[third independent audit](../reviews/status-passive-lifecycle-order-4-third-independent-audit-2026-07-26.md)
subsequently reproduced canonical cross-target timed-modifier failure and
shared phase-event clock divergence. O4-R38 corrected the runtime with one
sequence authority per lifecycle event ID, and O4-R39 reconciled all three
documentation audiences. The
[O4-R40 closure review](../reviews/status-passive-lifecycle-order-4-r40-closure-review-2026-07-26.md)
found no unresolved reachable defect at that revision. The
[fourth independent audit](../reviews/status-passive-lifecycle-order-4-fourth-independent-audit-2026-07-26.md)
then found one narrow programmatic flee-outcome validation defect and two stale
save-version labels. O4-R42 corrected the supported boundary, and O4-R43 plus
O4-R43A corrected all current-authority save-v13 guidance. The
[O4-R44 closure review](../reviews/status-passive-lifecycle-order-4-r44-closure-review-2026-07-26.md)
then independently re-read the corrected source and documentation and passed
the complete release gate without finding an unresolved realistic reachable
defect.

`combat_resolution` is complete. O2-R36 through O2-R38 corrected exact charge
participation, added supplied disabled composition, and reconciled the audience
documents. O2-R39 found one supported custom-executor receipt-integrity defect;
O2-R40 corrected it, and O2-R41 completed a fresh source and release-gate
closure with no unresolved reachable defect. Order 3 is complete under the
[Turn Economy Order 3 Roadmap](turn-economy-order-3-roadmap.md). Its source-first
review preserved the confirmed Action Token rules while reopening the custom
economy phase-authority and authored replacement surface for correction. O3-R1
through O3-R6 have corrected that surface and reconciled all three audiences;
O3-R7 independently re-read the corrected source, passed the full release gate,
and closed that revision without an unresolved reachable mechanic defect. A
later recheck found three narrower command-boundary integration defects. O3-R8
through O3-R10 corrected them, and O3-R11 reconciled the audience documents,
API contract, content wording, executable matrices, and release evidence.
O3-R12 then completed a fresh post-correction source and documentation review
without finding another realistic reachable defect.

A later fresh closure audit found that the two supplied economies correctly
honored explicit phase termination, while a custom economy could leave actions
remaining and still pass encounter transition validation. O3-R16 now enforces
the universal termination command before lifecycle and event commitment, and
O3-R17 reconciled the corrected source with all three documentation audiences.
The project owner confirmed the final contract on 23 July 2026. Order 3 is
complete. Order 4 subsequently completed its first correction sequence, but a
later independent audit reopened its narrower evidence and policy boundaries.

The `typed_action_and_effect_execution` inventory and actor-action authority
rules are confirmed, implemented, source-reviewed, and owner-approved in
[Battle Action Ownership And Inventory Authority](../decisions/battle-action-ownership-and-inventory-authority.md),
including canonical skill, item, and basic-attack authority. The completed
stat-modifier family supplies persistent staged, timed exclusive, and
independently timed contribution policies. The closure record is maintained
under [Completed Order 1](documentation-completion-roadmap.md#completed-order-1).

The [Terminology Boundary](../terminology-boundary.md) checkpoint is complete. Active contracts use Action Token, Vessel, Hosted Entity, Companion, roster, schema-v10, and save-v19 vocabulary; an executable token-aware guard prevents retired names from returning outside the historical archive. Vessel combat profiles now come from an explicit source policy, aggregate restoration derives the Active Hosted Entity from the canonical party roster, and retained stat modifiers, charges, and per-target passive activation keys bind to their authored policies during validation and restore.

## Completed Actor Design Correction

The source-based collaborative actor review identified confirmed product
direction for complete Hosted Entity combat composition, runtime skill
unlocking, one authoritative roster aggregate, unambiguous encounter presence,
explicit command authority, and meaningful configurable stage magnitude.

The ordered work and its owner decision lock are recorded in the
[Actor Composition, Progression, Roster, And Stage Roadmap](actor-composition-progression-roster-roadmap.md).
D1-D6 are approved and all eight checkpoints are implemented. Current source,
tests, reviewed audience documentation, Training Annex evidence, and save
contract v11 establish the corrected design direction.

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
destination does not exist. Schema v10 retains the explicit catalyst/target
shape introduced by schema v3 and authored stat-modifier policy selection, and
adds bounded damage hit counts.

Authored rulesets now resolve through an explicit host-supplied typed factory
registry. The standard damage factory exposes every existing combat setting,
roster tiers and Action Token liveness are authored, and the supplied fixed
growth/stat/reward/economy policies can be replaced by registering another
factory. Moon phase remains absent from the standard composition.

## Completed Release Foundations

The strict Draft 2020-12 schema-v10 set now covers every implemented content
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
entry becomes `reviewed`. Existing prose must not be bulk-promoted. Orders 1
and 2 are complete and owner-confirmed. O2-R30 through O2-R34 corrected
direct-action registration preflight, instant-defeat terminology, and current
documentation drift, then independently rechecked the corrected source. The
capability matrix records `turn_economy` as complete after its isolated runtime,
test, and audience-documentation checkpoints. O3-R7 completed the final
independent source and release-gate verification at that revision. A later
recheck found and O3-R8 through O3-R11 corrected three command-boundary
integration defects before returning the capability and all three audience
documents to complete/reviewed. O3-R12 independently confirmed that closure
from current code. O3-R15 later found the custom explicit-termination gap;
O3-R16 and O3-R17 corrected and independently reconciled it. Order 4, status
and passive lifecycle, completed O4-R22 through O4-R31. O4-R26's fresh
review found four additional passive and restore correctness paths; O4-R27
through O4-R30 corrected them, O4-R31 reconciled their guidance, and O4-R32
independently closed that corrected revision. The second independent audit
reopened and O4-R33 through O4-R36 corrected that revision. The third
independent audit has now reopened Order 4 under O4-R37 through O4-R40 for
lifecycle sequence authority. O4-R38 and O4-R39 corrected the implementation
and guidance, and O4-R40 independently closed that revision. The fourth
independent audit reopened Order 4 under O4-R41 through O4-R44 for one
programmatic validation defect and save-version documentation drift. O4-R42,
O4-R43, and O4-R43A corrected those paths, and O4-R44 independently closed the
sequence. Order 5 (`battle_knowledge`) implemented its core framework authority,
host integration, persistence validation, and three audience documents through
O5-R8. A later fresh source audit reopened one supported custom-effect
provenance boundary: aggregate knowledge integration validates effect index and
runtime target, but not authoritative source action, acting actor, or target
entity. O5-R10 now adds and enforces that complete authority, O5-R11 supplies
adversarial and valid-extension regression coverage, and O5-R12 reconciles the
integration guidance. The capability remained `partial` pending O5-R13.
O5-R13 has now completed the independent source, documentation, host, coverage,
and Godot verification without finding an unresolved realistic reachable
defect at that revision. The
[post-closure independent audit](../reviews/battle-knowledge-order-5-post-closure-independent-audit-2026-07-27.md)
then re-read the current exported surface and reopened `battle_knowledge` as
`partial`. O5-R15 removed
actor-local Analyze state and advanced the unreleased save contract to v14;
O5-R16 removed the disconnected mutable stores; O5-R17 closed the
clone-bypassed enum boundary; O5-R18 enforced coherent instant-defeat evidence.
O5-R19 reconciled the public API and audience documentation. O5-R20 found and
corrected one sibling ailment-evidence coherence defect, then independently
rechecked the corrected source, host integration, saves, tests, and all three
audience documents. It was complete at that revision. The later
[post-R20 independent audit](../reviews/battle-knowledge-order-5-post-r20-independent-audit-2026-07-29.md)
found a reachable composed-profile identity defect and an impossible Almighty
snapshot boundary. `battle_knowledge` was therefore reopened for O5-R21
through O5-R25.
O5-R21 through O5-R23 have now implemented canonical combat-profile identity,
source-entity persistence with exact-profile encounter invalidation, and the
intrinsic-Almighty storage boundary. O5-R24 reconciles mechanics, developer,
technical, public-API, actor, and save guidance. The
[O5-R25 independent closure review](../reviews/battle-knowledge-order-5-r25-independent-closure-review-2026-07-29.md)
then traced the corrected source and integrations without finding an unresolved
realistic reachable defect and passed the complete locally executable release
gate at that revision. The later
[pre-closure independent audit](../reviews/battle-knowledge-order-5-pre-closure-independent-audit-2026-07-29.md)
confirmed the approved mechanics but reproduced one low-severity false-success
path in familiar-import validation. O5-R26 now validates current knowledge
before policy evaluation and prevents no-op imports from bypassing the
transition authority. The
[O5-R27 final closure review](../reviews/battle-knowledge-order-5-r27-final-closure-review-2026-07-30.md)
found no remaining realistic reachable defect and passed every local gate.
`battle_knowledge` is complete and Order 5 is formally closed. Order 6
encounter orchestration has completed implementation and documentation through
O6-R12; independent closure review O6-R13 is the active subject.

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
