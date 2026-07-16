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

The matrix currently records 25 capabilities: 20 complete, 3 partial, and 2 deferred.

The actor composition correction established the intended source and ownership
model, stage scaling, live skill choices, and save v8 restoration. Its
independent completion review then identified three integration areas that
remain partial: stale prepared growth, capacity-aware creation/restore, and
party capacity/validation authority. The only deferred capabilities remain
save-version migration between released contracts and full deterministic
replay.

## Authority

The matrix is product evidence, not permission to remove history. Archived parity ledgers remain migration records under `ArchiveDocs/LegacyFramework` after the Phase 8 archive gate, and active implementation decisions come from current Framework code, tests, and architecture documentation.
