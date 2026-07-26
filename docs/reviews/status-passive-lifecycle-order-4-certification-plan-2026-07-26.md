# Status And Passive Lifecycle Order 4 Certification Plan

## Purpose

This is the final bounded certification pass for Order 4. It is not another
open-ended search for unusual malformed inputs, and it does not assume that a
larger test count is evidence of better behavior.

Order 4 remains complete unless this pass reproduces a defect through a
supported Framework boundary. A qualifying defect must have:

1. an invariant established by current source contracts or confirmed mechanics;
2. a realistic call sequence available to a host or extension;
3. a concrete state-integrity, gameplay, restoration, or integration effect;
   and
4. deterministic evidence that can become a regression test.

Impossible domain values, unsupported mid-encounter persistence, external side
effects performed by custom code, and alternate product designs are not
closure blockers. They must be identified separately if observed.

## Certification Checkpoints

### O4-C1: protocol

- record the bounded method and closure threshold;
- keep the existing O4-R44 result intact as historical review evidence; and
- avoid changing capability or documentation maturity before new evidence
  exists.

### O4-C2: sequence evidence

Add deterministic tests that provide evidence different from the existing
example-based cases:

- execute many seeded deployment and round-clock sequences against an
  independent reference model;
- prove the supplied reserve-aging policy advances only opted-in state and the
  authored `suspendWhileReserve` flag still wins;
- prove snapshots containing uncommitted action-scoped state are rejected
  before action-end rather than being misrepresented as valid save points;
- restore an actor at every supported boundary after action-end;
- prove the restored path remains equivalent to an uninterrupted path for
  Guard, ailments, counted status, phase state, battle state, permanent state,
  shields, affinity overrides, affinity breaks, resources, and deployment; and
- report the seed and step when a model assertion fails so the sequence is
  reproducible.

The tests use fixed seeds and public lifecycle operations. They do not use wall
clock time, nondeterministic scheduling, or test-only changes to Framework
rules.

### O4-C3: final certification

- re-read the exercised source rather than accepting prior reports as proof;
- cross-check mechanics, developer, and technical state-machine documents;
- run focused tests and the complete release gate;
- classify any concern using the threshold above; and
- publish one final health verdict.

If no qualifying defect remains, Order 4 stays closed and should not receive
another immediate audit. It may be reopened later only when new integration or
mechanic work provides reproducible contrary evidence.

## Required Verification

- new deterministic certification tests;
- all focused Order 4 tests;
- full Release solution tests with zero skips;
- strict nonincremental Release build with warnings as errors;
- formatting verification;
- content validation;
- Framework coverage and trimming gates;
- DemoHost modes and scripted Training Annex play;
- Godot contract tests and headless smoke;
- documentation, API, and product-boundary guards; and
- `git diff --check` plus a clean worktree after each commit.
