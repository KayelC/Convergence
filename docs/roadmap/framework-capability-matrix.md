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

The source-first Order 2 review reopened `combat_resolution` as partial. Charge
state is now policy-owned, and the supplied hit/evasion authority consumes
authored Accuracy/Evasion modifiers with explicit Agility coefficients, exact
probability bounds, deterministic evidence, and no hidden Luck contribution.
Critical eligibility and chance are now independent replaceable policies;
supplied exact-authored and accuracy-scaled chance authorities consume explicit
Critical Chance modifiers without Luck or hidden minimums. Schema v5 also
requires every weapon basic attack to declare its critical behavior.
Instant-defeat resistance, per-hit evidence/outcome aggregation, and the
neutral authored combat-policy aggregate still require implementation before
the capability can return to complete.

## Authority

The matrix is product evidence, not permission to remove history. Archived parity ledgers remain migration records under `ArchiveDocs/LegacyFramework` after the Phase 8 archive gate, and active implementation decisions come from current Framework code, tests, and architecture documentation.
