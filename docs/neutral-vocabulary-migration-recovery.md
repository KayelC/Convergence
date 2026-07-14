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
| R1 | Medium | Catalog actor restoration accepts saved effective stats without recomposing a Vessel from its active Hosted Entity. | Add a framework-owned composition-aware restore boundary that rejects stale or inconsistent derived state. | Pending |
| R2 | Medium | Live stat composition validates roster ID syntax but not duplicate IDs or cross-role overlap. | Share and enforce actor-roster invariants before any live commit. | Pending |
| R3 | Low | DemoHost applies a roster transition before recomposing Vessel stats. A failed composition could leave the aggregate roster and actor state inconsistent. | Assess composition against the proposed roster and commit both changes only after success. | Pending |
| R4 | Low | Retired public names remain in diagnostics: `MissingParentForm` and `DuplicateActorFormReference`. | Rename the symbols and their active test/document references with no compatibility aliases. | Pending |
| R5 | Decision | Ruleset documents use the generic `turn_economy` category rather than an `action_token` category. | Keep the generic category intentionally because Action Token is optional, and document that `standard_action_token` is the supplied implementation ID. | Pending |

## Remaining Planned Checkpoints

- **Checkpoint 4 audit:** verify creation, growth, battle, equipment, roster changes, and restore all use one explicit Vessel stat-composition model. Add missing tests or code before declaring the checkpoint complete.
- **Checkpoint 5:** neutralize active example content, fixture IDs, messages, and documentation while preserving approved generic terms `Almighty` and `Ice Boost`.
- **Checkpoint 6:** add a token-aware terminology boundary over active source, tests, content, and documentation. Exclude `ArchiveDocs`, `bin`, and `obj`.

## Commit Discipline

Each recovery finding receives an independent green commit. Checkpoints 5 and 6 retain the planned commit subjects:

- `data: neutralize active example vocabulary`
- `test: enforce convergence terminology boundary`

No finding is marked complete until focused tests, the affected project build, and `git diff --check` pass. The final migration gate additionally requires the full test suite, zero-warning .NET 8 builds, all five DemoHost modes, scripted Training Annex coverage, documentation-link validation, and the terminology scan.
