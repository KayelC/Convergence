# Neutral Vocabulary Migration Recovery

## Purpose

This document preserves the findings discovered after the interrupted neutral-vocabulary migration so they remain auditable outside the chat history. The migration began from `5d59ca3` and currently contains four completed checkpoints:

1. `f23ed8a` - documentation boundary
2. `e66aa50` - Action Token turn economy
3. `2cd01be` - Vessel, Hosted Entity, Companion, and roster vocabulary
4. `604cc89` - explicit Vessel stat sourcing and growth profiles

The active branch must not consider the migration complete until every finding below and checkpoints 5 and 6 are implemented and verified.

## Recovery Findings

| ID | Severity | Finding | Required correction | Status |
|---|---|---|---|---|
| R1 | Medium | Catalog actor restoration accepts saved effective stats without recomposing a Vessel from its active Hosted Entity. | Add a framework-owned composition-aware restore boundary that rejects stale or inconsistent derived state. | Complete - catalog restoration now requires an explicit restore request; persistence recomposes through the injected stat service, while already-validated fusion and Compendium transaction snapshots use an explicit preservation mode. |
| R2 | Medium | Live stat composition validates roster ID syntax but not duplicate IDs or cross-role overlap. | Share and enforce actor-roster invariants before any live commit. | Complete - one framework invariant service now guards construction, stat-composition commits, and save validation against duplicates, active-roster duplication, and hosted/companion role collisions. |
| R3 | Low | DemoHost applies a roster transition before recomposing Vessel stats. A failed composition could leave the aggregate roster and actor state inconsistent. | Assess composition against the proposed roster and commit both changes only after success. | Complete - the host composes against the immutable proposed roster first, records whether the operation committed, and leaves both roster and player snapshots unchanged on rejection. |
| R4 | Low | Retired public names remain in diagnostics: `MissingParentForm` and `DuplicateActorFormReference`. | Rename the symbols and their active test/document references with no compatibility aliases. | Complete - the public codes are now `MissingParentActorState` and `DuplicateActorRosterReference`; adjacent framework diagnostics use Hosted Entity and roster language. |
| R5 | Decision | Ruleset documents use the generic `turn_economy` category rather than an `action_token` category. | Keep the generic category intentionally because Action Token is optional, and document that `standard_action_token` is the supplied implementation ID. | Complete - active migration, mechanics, and content-contract documentation now distinguish the generic category from the supplied policy ID. |

## Remaining Planned Checkpoints

- **Checkpoint 4 audit (complete):** creation, growth, battle, equipment, roster changes, and restore now use one explicit Vessel stat-composition model. The recovery fixes closed raw restore, invalid live roster, and split DemoHost commit paths; an end-to-end host regression proves a Hosted Entity swap survives intervening growth and save restoration with the canonical derived stats and resources.
- **Checkpoint 5:** neutralize active example content, fixture IDs, messages, and documentation while preserving approved generic terms `Almighty` and `Ice Boost`.
- **Checkpoint 6:** add a token-aware terminology boundary over active source, tests, content, and documentation. Exclude `ArchiveDocs`, `bin`, and `obj`.

## Commit Discipline

Each recovery finding receives an independent green commit. Checkpoints 5 and 6 retain the planned commit subjects:

- `data: neutralize active example vocabulary`
- `test: enforce convergence terminology boundary`

No finding is marked complete until focused tests, the affected project build, and `git diff --check` pass. The final migration gate additionally requires the full test suite, zero-warning .NET 8 builds, all five DemoHost modes, scripted Training Annex coverage, documentation-link validation, and the terminology scan.
