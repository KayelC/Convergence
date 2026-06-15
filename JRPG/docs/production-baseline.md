# Production Baseline

## Status

This document defines the recovery baseline after Track 12. It exists to prevent architectural cleanup from removing working gameplay before an equivalent replacement is usable.

The detailed implementation sequence and parity gates are defined by the [Framework Parity Migration Plan](framework-parity-migration-plan.md).

## Current Boundary

The project currently contains:

- a broad interactive console prototype backed by the legacy database and runtime models,
- an additive clean content pipeline with immutable definitions, strict deserialization, validation, dependency-aware catalogs, typed combat vocabulary, active effects, passive rules, item execution, and fusion inheritance planning,
- deterministic clean battle and field demonstrations used as technical smoke tests.

The clean path is a framework foundation. It is not yet a feature-complete replacement for the interactive prototype.

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
