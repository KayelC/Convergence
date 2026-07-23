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

`status_and_passive_lifecycle` is temporarily `partial` while Documentation
Order 4 completes its approved correction sequence. O4-R2 separates expiration
from typed removal/persistence, and O4-R3 supplies explicit lifecycle clocks,
team-to-phase mapping, round dispatch, and both reserve policies. Typed
transition evidence, live application hardening, passive targeting/liveness,
wire reconciliation, and audience closure remain tracked by the active Order 4
roadmap rather than being hidden behind the former `complete` label.

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
model, stage scaling, live skill choices, and save v12 restoration. Its
independent completion review found six reachable integration gaps; all are
corrected with isolated regression coverage. The only deferred capabilities
remain save-version migration between released contracts and full
deterministic replay.

Exactly-one item reservations, reservation validation, mandatory inventory
authority, and Framework-owned canonical skill, item, and basic-attack
authorization are implemented and covered by focused tests. The stat-modifier
migration has one canonical actor authority, three supplied policies, typed
effect and lifecycle integration, authored ruleset binding, save-v11
restoration, and cross-policy host evidence. The final source-first review found
no remaining reachable Order 1 defect, and the project owner confirmed the
public mechanics on 18 July 2026.

Order 2's first correction cycle established a coherent combat policy family.
Charge state is policy-owned; supplied hit/evasion uses
authored accuracy, explicit Agility coefficients, typed modifiers, exact
probability bounds, and no hidden Luck. Critical eligibility and chance are
separate replaceable policies. Instant defeat uses authored chance, explicit
resistance multipliers, bypass semantics, and one roll. Schema v6 requires
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
