# Convergence Framework Capability Matrix

## Purpose

This matrix reports the maturity of the reusable `Convergence.Framework` product. It replaces the recovery-era parity labels that compared every framework capability to the archived console prototype.

The executable source is [`../../tests/Convergence.Framework.Tests/Fixtures/framework-capability-matrix.json`](../../tests/Convergence.Framework.Tests/Fixtures/framework-capability-matrix.json). Tests require unique capability IDs, valid maturity states, framework test evidence, honest known gaps, and host neutrality.

## States

- `complete`: the framework owns a usable, host-neutral contract and implementation with direct tests.
- `partial`: a useful implementation exists, but a named part of the framework contract is unfinished.
- `deferred`: the capability is intentionally outside the current product or reserved for later work.

Demo coverage is recorded independently as `none`, `focused`, or `end_to_end`. A capability does not become incomplete merely because a particular host has not presented every feature.

## Current Reading

The matrix currently records 25 capabilities: 22 complete, 1 partial, and 2 deferred.

`inventory_equipment_economy` is `partial`. Its immutable transaction and
validation foundation is usable. O7-R2 gives each equipment copy a unique
runtime instance ID, makes inventory its sole owner, permits separate copies of
one definition, and removes the former root save equipment authority. O7-R3
replaces fixed slot identity with authored `ContentId` values under a selected
layout policy. O7-R4 derives granted skills, Defense, and Evasion from the same
live equipment profile used for weapon attacks and accessory modifiers. O7-R5
provides an immutable currency ledger keyed by `ContentId` and requires every
transaction to select a currency explicitly. Current source still does not
persist decremented shop stock, bind authored pricing policies, or generalize
the HP/SP-specific recovery shape. The
owner-approved
[Order 7 source review and roadmap](../reviews/inventory-equipment-economy-order-7-source-review-2026-08-10.md)
governs the direct data corrections, genuine policy seams, three-audience
documentation, and independent closure gate.

`encounter_orchestration` is `complete`. Its scheduler, lifecycle,
turn-economy, event, cancellation, and automated execution foundations remain
implemented. The owner-approved
[Order 6 roadmap](../reviews/encounter-orchestration-order-6-source-review-2026-07-30.md)
implemented replaceable team-phase and Agility scheduling, bounded phase and
encounter progress, bounded post-command actor selection, lifecycle-boundary
reconciliation, complete structural events, frozen-graph event identity,
validated completion shapes, certified cancellation paths, and canonical
asynchronous automated execution. The earlier
[final closure review](../reviews/encounter-orchestration-order-6-final-closure-review-2026-07-30.md)
records that revision. A later
[fresh owner-closure audit](../reviews/encounter-orchestration-order-6-fresh-owner-closure-audit-2026-08-04.md)
reproduced four supported but uncovered paths. O6-R15 through O6-R18 now
correct transition-aware defeat periods, zero-living-team completion,
automated untargeted and terminal skill results, and `ActionExecuted` actor
correlation. O6-R19 reconciled those contracts. The
[O6-R20 source review](../reviews/encounter-orchestration-order-6-r20-source-closure-review-2026-08-04.md)
then found that ordinary executed-action evidence could omit its actor. O6-R21
now permits actorless evidence only for `PartyRosterTransitioned`, and O6-R22
reconciled the active guidance. The
[O6-R23 final closure review](../reviews/encounter-orchestration-order-6-r23-final-closure-review-2026-08-04.md)
independently re-read the corrected source, found no unresolved realistic
reachable defect, and passed every locally executable release gate. The online
NuGet advisory lookup remains a connected CI check because the local
environment could not reach its advisory endpoint.

The later
[O6-R24 post-R23 independent audit](../reviews/encounter-orchestration-order-6-post-r23-independent-audit-2026-08-04.md)
reopened the capability after reproducing one normal terminal-result contract
defect: round-limit and custom completion text can populate `FaultMessage`
without a fault. O6-R25 now keeps normal text on `BattleEnded.DebugText` and
enforces fault-only result metadata. O6-R26 reconciles the three audience and
public API contracts. The
[O6-R27 final closure review](../reviews/encounter-orchestration-order-6-r27-final-closure-review-2026-08-04.md)
independently traced current source and documentation, corrected one
completion-policy wording ambiguity, and found no unresolved realistic
reachable runtime defect.

The subsequent
[O6-R28 post-R27 independent audit](../reviews/encounter-orchestration-order-6-post-r27-independent-audit-2026-08-04.md)
reopened the capability after reproducing two supported contract defects. The
supplied automated restriction resolver can authorize a typed Guard, Pass,
Analyze, or Escape command from a different allowed action label, and an
explicitly fled or recalled actor that is also defeated can receive a second
Defeat cleanup in the same defeat period. O6-R29 through O6-R32 govern the
runtime corrections, documentation reconciliation, and fresh closure review.

O6-R29 now makes typed command identity authoritative for every command kind
supported by the supplied restriction resolver. O6-R30 preserves one explicit
Flee or Roster Recall cleanup reason for the entire current defeat period.
O6-R31 reconciled active audience and integration guidance. The
[O6-R32 final closure review](../reviews/encounter-orchestration-order-6-r32-final-closure-review-2026-08-04.md)
independently reread current source, adversarial tests, and documentation,
found no unresolved realistic reachable defect, and passed the complete
release gate. The capability and all three audience entries therefore return
to `complete` and `reviewed` at that revision.

The subsequent
[O6-R33 post-R32 independent audit](../reviews/encounter-orchestration-order-6-post-r32-independent-audit-2026-08-05.md)
reproduced two supported extension-boundary defects: `None` consumption can
change turn-economy state and evade free-action liveness, and a custom
scheduler can drift into another round far enough to commit a command before
typed rejection. O6-R34 now enforces immutable no-cost economy evidence and
kind-based nonterminal free-action liveness. O6-R35 now validates exact
round/completed-round and step continuity before another cursor is accepted.
O6-R36 reconciles the structural `BattleStarted` cleanup boundary and both
corrected contracts across the active audience and API guidance. The
[O6-R37 final closure review](../reviews/encounter-orchestration-order-6-r37-final-closure-review-2026-08-05.md)
independently traced current source, hostile tests, both supplied schedulers,
and all three audience documents. It found no unresolved realistic reachable
defect, so `encounter_orchestration` returns to `complete` and its audience
entries return to `reviewed`.

The later
[O6-R38 post-R37 independent audit](../reviews/encounter-orchestration-order-6-post-r37-independent-audit-2026-08-05.md)
did not treat that closure as current proof. It reproduced two reachable
orchestration defects: filtered availability can break stable team round-robin
rotation, and structural validation can accept another command window after
authoritative economy exhaustion, allowing handler mutation before a later
fault. O6-R39 through O6-R42 govern the bounded correction, documentation
reconciliation, and independent closure. The capability is therefore
`partial` and its three audience entries are `existing_unreviewed`.

O6-R39 has replaced compacted availability indexing with one stable team ring.
O6-R40 now correlates every proposed schedule transition with accepted economy
liveness before another command window can begin. O6-R41 reconciles the
mechanics, developer, technical, XML, API, and turn-economy guidance, returning
the three audience entries to `reviewed`. The
[O6-R42 final closure review](../reviews/encounter-orchestration-order-6-r42-final-closure-review-2026-08-05.md)
independently traced the corrected source and documents and passed every
locally executable release gate without finding another realistic reachable
defect. The capability therefore returns to `complete` with no known gap.

The subsequent
[O6-R43 post-R42 independent audit](../reviews/encounter-orchestration-order-6-post-r42-independent-audit-2026-08-05.md)
reconstructed the current runner and documentation without treating prior
closure as evidence. It found two supported combined failure paths: failed
event publication can erase canonical evidence and reuse its sequence, and a
battle-end cleanup failure can replace an earlier command fault as terminal
authority. O6-R44 through O6-R47 govern correction, documentation
reconciliation, and independent closure. `encounter_orchestration` is therefore
`partial` with those two named gaps.

O6-R44 now records canonical events before optional sink publication and never
reuses a failed event's sequence. O6-R45 routes faulted and rejected command
results through one primary-fault finalizer, preserving the command code when
battle-end cleanup also fails. O6-R46 reconciles all active guidance and the
exact restriction-action interface name. The
[O6-R47 final closure review](../reviews/encounter-orchestration-order-6-r47-final-closure-review-2026-08-05.md)
independently reread the corrected source and documents, passed the complete
local release gate, and found no remaining realistic reachable defect.
`encounter_orchestration` returns to `complete` with no known gap.

`battle_knowledge` is `complete`. Persistent entity facts and encounter-local
runtime facts have separate immutable authorities; typed execution evidence and
Analyze results pass through one atomic framework transition; automated teams
share only their own encounter snapshot; familiar acquisition is optional and
policy-controlled; and session-save validation rejects duplicate, missing, or
encounter-only knowledge. Training Annex now exercises this path end to end
without inspecting hidden target defenses in the host. A fresh source audit
found that custom effect evidence could substitute its source action, acting
actor, or target entity while retaining a valid outer effect index and runtime
target. O5-R10 now requires an immutable accepted-action authority and
preflights all five provenance dimensions before any lower transition. O5-R11
covers valid and hostile registered custom handlers, every mismatch, immutable
authority construction, and whole-batch rollback. O5-R12 reconciles the public
guidance. The
[O5-R13 closure review](../reviews/battle-knowledge-order-5-provenance-closure-review-2026-07-27.md)
independently re-read the corrected source and audience documents, passed the
complete release gate, and found no unresolved realistic reachable Battle
Knowledge defect at that revision. A later
[post-closure independent audit](../reviews/battle-knowledge-order-5-post-closure-independent-audit-2026-07-27.md)
reopened the capability after tracing two exported competing authorities, a
clone-bypassed enum-validation path, and an incomplete instant-defeat evidence
shape. O5-R15 removed actor-local Analyze state and advanced the unreleased
save contract to v14; O5-R16 removed the disconnected mutable stores; O5-R17
closed the clone-bypassed enum boundary; O5-R18 enforced coherent
instant-defeat evidence; O5-R19 reconciled the public API and audience
documentation. O5-R20's source-first review found and corrected one sibling
custom ailment-evidence coherence defect, then passed every locally executable
code, content, host, coverage, and Godot gate without another realistic
reachable finding. The online dependency audit could not be refreshed in the
restricted local environment and remains a connected CI release gate. The
[O5-R20 closure review](../reviews/battle-knowledge-order-5-r20-final-closure-review-2026-07-29.md)
returned the capability to `complete` at that revision. A later independent
source trace found that composed Vessels attribute Hosted Entity defenses to the
owner entity and retain encounter disclosure across profile replacement. It
also found that public snapshots accept impossible Almighty affinity facts.
The
[post-R20 audit](../reviews/battle-knowledge-order-5-post-r20-independent-audit-2026-07-29.md)
therefore reopened the capability for O5-R21 through O5-R25.
O5-R21 now gives every actor a canonical combat-profile source and revision;
O5-R22 keys durable facts to the source entity and invalidates every encounter
domain on profile replacement; O5-R23 rejects stored Almighty facts at
construction, transition, view, automated-seed, host-decoding, and
save-validation boundaries. O5-R24 reconciles the three reviewed audience
documents and save-v15 guidance. The
[O5-R25 independent closure review](../reviews/battle-knowledge-order-5-r25-independent-closure-review-2026-07-29.md)
then re-read the corrected source, adversarial boundaries, host integration,
and all active audience documents without finding an unresolved realistic
reachable defect. The complete local release gate passed at that revision. A
later
[pre-closure independent audit](../reviews/battle-knowledge-order-5-pre-closure-independent-audit-2026-07-29.md)
confirmed the gameplay model and principal runtime paths but found that a
no-op familiar-knowledge import can report success for clone-malformed current
knowledge without invoking complete persistent validation. O5-R26 must close
that boundary. O5-R26 now preflights current knowledge before policy evaluation
and routes valid no-op imports through the injected transition. The
[O5-R27 final closure review](../reviews/battle-knowledge-order-5-r27-final-closure-review-2026-07-30.md)
found no remaining realistic reachable defect and passed the complete local
gate, returning Order 5 to `complete`.

`status_and_passive_lifecycle` is `complete`. The
[26 July fresh closure audit](../reviews/status-passive-lifecycle-order-4-fresh-closure-audit-2026-07-26.md)
reproduced two bounded defects from current source. O4-R18 now skips a scheduled
ailment trigger when that exact active instance has been removed or replaced,
and O4-R19 validates restored passive activation keys against the equipped
passive's authored trigger index and event. O4-R20 reconciled all three
documentation audiences. O4-R21 then reproduced live turn-start ailment
enumeration failure and omitted encounter-owned departure cleanup. O4-R22
through O4-R25 recorded and corrected those paths and reconciled all three
audiences. O4-R26 completed the independent review but rejected closure after
finding four additional passive and restore correctness paths. The
[R26 correction audit](../reviews/status-passive-lifecycle-order-4-r26-correction-audit-2026-07-26.md)
governs O4-R27 through O4-R32. O4-R27 through O4-R30 corrected frozen passive
eligibility, exact passive-state restore coverage, ailment exclusivity restore,
and explicit defeat-prevention policy composition. O4-R31 reconciled active
guidance. The
[final O4-R32 closure review](../reviews/status-passive-lifecycle-order-4-final-closure-review-2026-07-26.md)
then re-read current source, tests, schemas, content, and all three audiences
without finding an unresolved realistic reachable defect at that revision. A
[second independent audit](../reviews/status-passive-lifecycle-order-4-second-independent-audit-2026-07-26.md)
then reproduced cancellation-before-commit at round and successful battle end,
plus unevidenced mutation through a replacement passive dispatcher. O4-R33
through O4-R36 governed the corrections and fresh closure gate. Demo coverage
remains `focused`.
O4-R33 corrected every encounter lifecycle commit boundary, O4-R34 made
executed activation evidence mandatory for replacement mutation commits, and
O4-R35 added exact ailment combat-profile documentation and ordinary-value
composition coverage. The
[O4-R36 closure review](../reviews/status-passive-lifecycle-order-4-r36-closure-review-2026-07-26.md)
found no remaining realistic reachable defect in that corrected revision. The
[third independent audit](../reviews/status-passive-lifecycle-order-4-third-independent-audit-2026-07-26.md)
then reproduced a canonical cross-target timed-modifier fault and a separate
shared phase-event clock divergence. The root cause was that the encounter port
used actor-local and team-local sequence counters while the public timed
modifier boundary identifies a clock only by event ID and sequence. O4-R38 now
uses one committed sequence stream per lifecycle event ID across actors, teams,
phases, and rounds. O4-R39 reconciled mechanics, integration, and technical
guidance. The
[O4-R40 closure review](../reviews/status-passive-lifecycle-order-4-r40-closure-review-2026-07-26.md)
completed a fresh source, documentation, coverage, host, and Godot gate without
an unresolved realistic reachable defect at that revision. The
[fourth independent audit](../reviews/status-passive-lifecycle-order-4-fourth-independent-audit-2026-07-26.md)
subsequently found one narrow supported-boundary defect: programmatic
`ChanceSkipOrFlee` content can pass an undefined Companion flee outcome through
semantic validation and silently resolve it as battle escape. O4-R42 now
rejects that value in semantic validation and direct lifecycle execution.
O4-R43 reconciled all three audiences and corrected the stale save-v10 labels
to v13. O4-R43A corrected two additional current-authority labels found during
closure preflight. The
[O4-R44 closure review](../reviews/status-passive-lifecycle-order-4-r44-closure-review-2026-07-26.md)
then re-read the corrected source, schemas, tests, and all three audiences,
passed the complete release gate, and found no unresolved realistic reachable
defect. The subsequent
[bounded final certification](../reviews/status-passive-lifecycle-order-4-final-certification-2026-07-26.md)
added 1,536 model-checked deployment and clock operations plus public-restore
equivalence at every supported lifecycle boundary. It corrected the documented
save-checkpoint rule for action-scoped `Instant` state and found no qualifying
runtime defect. Demo coverage remains `focused`.

Documentation Order 3 reopened `turn_economy` after a source-first review found
that a malformed custom economy could execute one command before its initial
snapshot/liveness contradiction was detected. O3-R1 through O3-R5 now enforce
initial and continuous phase snapshot authority, validate typed event payloads,
bind the neutral standard-actions economy, and cover the complete transition
and liveness matrix. O3-R6 reconciles all three documentation audiences. The
confirmed Action Token transition table remains unchanged, and
`turn_economy` has returned to `complete`. O3-R7 independently verified the
corrected source and complete release gate under the
[Turn Economy Order 3 Roadmap](turn-economy-order-3-roadmap.md); no unresolved
reachable mechanic defect remained.

A later source-first recheck at `e6949d7b` reproduced three public integration
defects at the command-window boundary: retained economy mutation can be
double-applied, an executed/cancelled result can spend a turn and tick
lifecycle, and record cloning can invalidate turn-consumption contracts.
O3-R8 through O3-R10 corrected those boundaries with isolated regression
coverage. O3-R11 reconciled the public guidance, accepted API baseline, content
wording, and executable evidence, so `turn_economy` has returned to `complete`.
The supplied transition table itself was unchanged.

The owner-closure audit at `7aa3467e` reopened `turn_economy` for one shared
event-boundary defect. Command and lifecycle ports can publish structural
encounter kinds that look identical to runner-owned phase, turn-economy,
fault, and battle-end events. The runtime state remains correct, but event
sinks cannot prove provenance. The capability is `partial` until O3-R13
enforces event ownership and O3-R14 rechecks the source and audience guidance.

O3-R13 now applies a fail-closed allow-list at both command and lifecycle
ingress and preserves one runner-owned source for structural events. O3-R14
re-read the corrected path, reconciled all three audience documents, and found
no remaining reachable Order 3 defect. `turn_economy` is complete again.

The fresh closure audit at `6e1169b5` found one remaining supported custom
policy defect: `TerminatePhase` could change a custom economy while leaving
positive actions. O3-R16 now rejects that transition before owner-turn-end
lifecycle or accepted event publication, while leaving both supplied policies
unchanged. O3-R17 re-read the corrected source and all three audience
documents. `turn_economy` is complete again with no named gap.

The actor composition correction established the intended source and ownership
model, stage scaling, live skill choices, and save v15 restoration. Its
independent completion review found six reachable integration gaps; all are
corrected with isolated regression coverage. The only deferred capabilities
remain save-version migration between released contracts and full
deterministic replay.

Exactly-one item reservations, reservation validation, mandatory inventory
authority, and Framework-owned canonical skill, item, and basic-attack
authorization are implemented and covered by focused tests. The stat-modifier
migration has one canonical actor authority, three supplied policies, typed
effect and lifecycle integration, authored ruleset binding, save-v14
restoration, and cross-policy host evidence. The final source-first review found
no remaining reachable Order 1 defect, and the project owner confirmed the
public mechanics on 18 July 2026.

Order 2's first correction cycle established a coherent combat policy family.
Charge state is policy-owned; supplied hit/evasion uses
authored accuracy, explicit Agility coefficients, typed modifiers, exact
probability bounds, and no hidden Luck. Critical eligibility and chance are
separate replaceable policies. Instant defeat uses authored chance, explicit
resistance multipliers, bypass semantics, and one roll. Schema v9 requires
weapon basic attacks to declare critical behavior.

Every attempted damage hit now carries immutable accuracy, critical, affinity,
charge, damage, and applied-resource evidence. Landed hits mutate staged actor
state sequentially, and only committed critical hits affect the action result.
A source-aware action policy derives one turn-economy result from the complete
effect set: supplied items use normal cost by default while effect-driven item
behavior is an authored option. Authored binding returns a coherent neutral
combat aggregate, unified charge is authorable, public combat vocabulary is
validated, and all supplied random draws cross one checked host boundary.

A broader 21 July source review reopened the capability after finding defects
where per-hit facts become one action outcome and where earlier effects change
later life-state eligibility. O2-R7 through O2-R16 corrected those paths,
introduced explicit effect dependencies and shared-contact damage, composed
weapon secondary effects, aligned schema ranges, and passed a fresh source
review. A later closure review found two reachable safety-boundary defects.
O2-R18 now limits active content to `1..1024` hits and applies the standard
policy's configurable default ceiling of `64` before random selection or
allocation. O2-R19 now rejects every authored combat percentage outside
`0..100` before targets, costs, randomness, mutation, or turn use, while
preserving clamping only for policy-derived chances. O2-R22 aligned the final
schema-only resource-percentage condition, and O2-R23's fresh source trace
found no further reachable defect at that revision. A subsequent pre-closure
audit found order-dependent duplicate resource costs, invalid host
turn-consumption shapes, and a party-size schema/semantic mismatch. O2-R24
through O2-R27 corrected and independently rechecked those paths. The post-R27
source trace found one remaining supported extension-boundary defect: a custom
effect result could carry undefined execution and turn-economy outcomes into
the ordered pipeline. O2-R28 now rejects malformed result construction and
record cloning inside the staged execution boundary. O2-R29's current-source
recheck found no remaining reachable defect in the reviewed paths, so typed
action execution, combat resolution, and host contracts return to `complete`.
O2-R30 through O2-R34 later unified registration preflight across skills, items,
basic attacks, and escape; corrected current documentation drift; and completed
a fresh source review with no unresolved reachable defect.

The later 22 July closure-readiness review reopened `combat_resolution` as
`partial`. O2-R36 now finalizes exact participating charge receipts and
preserves later grants or same-kind replacements. O2-R37 supplies authored
disabled, split, and unified composition, and O2-R38 reconciles all three
documentation audiences. O2-R39's independent source review then found a
custom-executor receipt-integrity defect: a source-less charged modifier was
accepted as a wildcard. O2-R40 now rejects such input before mutation, and its
custom-executor regression proves staged rollback. O2-R41's fresh source trace
and complete release gate found no remaining reachable defect in the corrected
scope, so `combat_resolution` is `complete`.

## Authority

The matrix is product evidence, not permission to remove history. Archived parity ledgers remain migration records under `ArchiveDocs/LegacyFramework` after the Phase 8 archive gate, and active implementation decisions come from current Framework code, tests, and architecture documentation.
