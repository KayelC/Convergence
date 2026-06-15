# Production Baseline

## Status

This document defines the recovery baseline after Track 12. It exists to prevent architectural cleanup from removing working gameplay before an equivalent replacement is usable.

The detailed implementation sequence and parity gates are defined by the [Framework Parity Migration Plan](framework-parity-migration-plan.md).

Track A was characterized from commit `fce33a9` on `track-12-recovery`. Its executable parity ledger is `Convergence.Tests/Fixtures/Parity/recovery-baseline.json`, covering 35 protected capabilities.

## Current Boundary

The project currently contains:

- `JRPG.ConsoleHost`, a broad interactive console prototype backed by the legacy database and runtime models,
- `JRPG.Framework`, a reusable class library containing immutable definitions, strict deserialization, validation, dependency-aware catalogs, typed combat vocabulary, active effects, passive rules, item execution, battle orchestration, and fusion inheritance planning,
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

## Track B Boundary

Track B began from `d97b244` and established a one-way assembly dependency: `JRPG.ConsoleHost` references `JRPG.Framework`; the framework never references the host.

- The framework builds independently with 0 warnings and has no external package dependency.
- The complete suite contains 479 passing tests with 0 skipped tests.
- The complete nonincremental solution build retains the existing 122 warnings, all in the console-host/legacy boundary.
- Root `dotnet run` remains the interactive executable path.
- The battle demo still ends in `Victory` for `player_team`; the field demo still completes all seven ordered events.
- Async content, command, event, and random contracts are available for future Godot and other hosts.
- The legacy interactive workflow remains on `IGameIO`; no capability is marked `clean_parity` or consumer-migrated merely because its clean code moved assemblies.

## Track C Boundary

Track C began from `46e9634` and completed the clean content catalog surface for every retained legacy JSON family without migrating runtime consumers.

- `GameDataCatalog` now has immutable repositories for equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets in addition to skills, entities, races, ailments, and items.
- The new definitions use strict `System.Text.Json` DTOs, validator-backed registrations, deterministic catalog qualification, and direct-dependency reference checks.
- `convergence.catalog_surface_sample` `0.1.0` provides one compact fixture pack: four equipment records, one shop, one negotiation set, one encounter, one dungeon, one fusion recipe, and eight ruleset policy records.
- The parity ledger marks affected content capabilities as `clean_foundation`, not `clean_parity`; no consumer-migration flag or removal authorization changed.
- Legacy datasets, `Database.LoadData`, ordinary interactive startup, and existing console workflows remain the playable source of truth until their later migration tracks.
- Three Track C catalog-surface tests bring the suite to 482 passing tests with 0 skipped tests. The nonincremental build remains at the existing 122 warnings.

## Track D Boundary

Track D began from `68175c8` and added the framework runtime-state foundation without migrating the interactive console consumers.

- `JRPGPrototype.Logic.Runtime` now owns stable runtime instance IDs, actor identity/display snapshots, controller/team/owner links, deployment state, progression, resource pools, stat blocks, skill loadouts, active form and stock references, equipment slots, battle statuses, analysis, passive activation counts, and transaction-safe mutation results.
- `RuntimeActorSnapshot` is the aggregate save/presentation/replay boundary. It references content by qualified `ContentId` and does not duplicate content definitions.
- Runtime state remains composed from focused snapshot records; it is not a new universal `Combatant` replacement.
- A narrow `RuntimeResourceTransactionService` proves before/after mutation reporting and rejection without partial state changes.
- The parity ledger now marks actor model, growth/progression state, stat/equipment state, active/reserve party state, and persona/demon stock state as `clean_foundation`. No consumer is marked migrated and no removal is authorized.
- Ten Track D runtime-state tests bring the suite to 492 passing tests with 0 skipped tests. The nonincremental build remains at the existing 122 warnings.
- The clean battle demo still ends in `Victory` for `player_team`; the clean field demo still completes all seven ordered events.

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

Track E may begin only while the two-project build, full suite, ordinary interactive startup, clean demos, parity ledger, and dataset assertions remain green. Full live battle sessions and exhaustive console traversal remain manual checks alongside the automated representative workflows.
