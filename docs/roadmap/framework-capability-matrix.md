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

The matrix currently records 25 capabilities: 21 complete, 2 partial, and 2 deferred.

The actor composition correction established the intended source and ownership
model, stage scaling, live skill choices, and save v10 restoration. Its
independent completion review found six reachable integration gaps; all are
corrected with isolated regression coverage. The only deferred capabilities
remain save-version migration between released contracts and full
deterministic replay.

Exactly-one item reservations, reservation validation, mandatory inventory
authority, and Framework-owned skill/basic-attack authorization remain
implemented and covered by focused tests. Typed action/effect execution and
status/passive lifecycle are temporarily `partial`, however, because the
current direct stat-stage mutation and one-stage/one-duration state cannot
implement the approved stat-modifier policy family without competing
authorities or lossy restoration. The active
[Stat Modifier Policy Roadmap](stat-modifier-policy-roadmap.md) governs that
correction. Its neutral authority and all three supplied policies are complete;
production effect, lifecycle, ruleset, and persistence integration remain open.

## Authority

The matrix is product evidence, not permission to remove history. Archived parity ledgers remain migration records under `ArchiveDocs/LegacyFramework` after the Phase 8 archive gate, and active implementation decisions come from current Framework code, tests, and architecture documentation.
