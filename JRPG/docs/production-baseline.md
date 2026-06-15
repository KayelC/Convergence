# Production Baseline

## Status

This document defines the recovery baseline after Track 12. It exists to prevent architectural cleanup from removing working gameplay before an equivalent replacement is usable.

The detailed implementation sequence and parity gates are defined by the [Framework Parity Migration Plan](framework-parity-migration-plan.md).

Track A was characterized from commit `fce33a9` on `track-12-recovery`. Its executable parity ledger is `Convergence.Tests/Fixtures/Parity/recovery-baseline.json`, covering 35 protected capabilities.

## Current Boundary

The project currently contains:

- a broad interactive console prototype backed by the legacy database and runtime models,
- an additive clean content pipeline with immutable definitions, strict deserialization, validation, dependency-aware catalogs, typed combat vocabulary, active effects, passive rules, item execution, and fusion inheritance planning,
- deterministic clean battle and field demonstrations used as technical smoke tests.

The clean path is a framework foundation. It is not yet a feature-complete replacement for the interactive prototype.

## Measured Recovery Baseline

- 470 tests pass with 0 skipped tests, including 22 Track A baseline and workflow cases.
- A nonincremental build completes with 122 warnings and 0 errors. Track A did not increase the warning count.
- The clean battle demo ends in `Victory` for `player_team`.
- The clean field demo completes recovery, cure, revival, battle escape, and dungeon-exit request flows without input.
- Ordinary interactive startup remains executable and reaches scenario selection, field navigation, and session exit through scripted characterization.

The parity ledger records the protected owner, evidence, status, unresolved decisions, intended migration track, and possible future removal files for every capability. A listed removal file is evidence only and does not authorize deletion.

## Dataset Preservation Facts

The legacy data is intentionally unchanged. Track A records:

- 420 authored skills in 3 duplicate-name groups; the legacy dictionary exposes 417 unique names,
- 304 entities, 11 ailments, 14 items, 26 weapons, 3 armor records, 3 boots, and 3 accessories,
- 460 fusion recipes and 30 shop entries,
- 1 dungeon containing 6 blocks,
- 8 negotiation personalities, 40 questions, and 8 familiar-dialogue sets.

Known unresolved findings are 56 base-skill references, 120 learned-skill references, 1 casing-only skill reference mismatch, and 1 dungeon enemy-pool reference. Dungeon boss references, shop references, and fusion operands are otherwise resolved under the preserved legacy rules.

These findings characterize the current datasets. They neither approve the old schemas nor silently correct their anomalies.

## Migration Rule

No working subsystem is removed merely because a cleaner API exists.

A legacy subsystem may be retired only when all of the following are true:

1. Its required player-facing behavior is listed and approved.
2. The clean replacement implements that behavior.
3. Automated tests cover the replacement and important legacy regressions.
4. A real host consumes the clean replacement.
5. The interactive prototype or its successor remains usable after migration.
6. Data required by the replacement has an approved schema and authored fixtures.
7. Removal is reviewed as a dedicated change rather than bundled into unrelated work.

## Production Sequence

Future work should migrate one vertical slice at a time:

```text
characterize existing behavior
  -> approve intended rules
  -> implement clean replacement
  -> connect a real host consumer
  -> verify functional parity
  -> retire only the replaced code
```

Battle, field exploration, party management, inventory, shops, negotiation, growth, fusion, dungeons, and persistence each require their own parity decision.

## Documentation Rule

- The [Skill System GDD](skill-system-gdd.md) remains normative for approved skill-system decisions.
- Source and tests define what Track 12 currently implements.
- Historical plans and discarded proposals are stored in [ArchiveDocs](../ArchiveDocs/README.md).
- New proposals should begin as focused discussion documents and become active contracts only after approval.

## Branch Safety

The completed `skill-system-redesign` branch remains an architectural reference. It must not replace the playable line merely because its internal contracts are cleaner. Work on this recovery branch should preserve the interactive prototype until a deliberate successor exists.

Track B may begin only from a baseline where the full suite, ordinary interactive startup, clean demos, parity ledger, and dataset assertions remain green. Full live battle sessions and exhaustive console traversal remain manual checks alongside the automated representative workflows.
