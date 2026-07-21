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

The matrix currently records 25 capabilities: 23 complete, 0 partial, and 2 deferred.

The actor composition correction established the intended source and ownership
model, stage scaling, live skill choices, and save v11 restoration. Its
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

## Authority

The matrix is product evidence, not permission to remove history. Archived parity ledgers remain migration records under `ArchiveDocs/LegacyFramework` after the Phase 8 archive gate, and active implementation decisions come from current Framework code, tests, and architecture documentation.
