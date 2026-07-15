# Neutral Vocabulary Migration Recovery

## Purpose

This document preserves the findings discovered after the interrupted neutral-vocabulary migration so they remain auditable outside the chat history. The migration began from `5d59ca3`; all six planned checkpoints and the recovery findings are complete.

1. `f23ed8a` - documentation boundary
2. `e66aa50` - Action Token turn economy
3. `2cd01be` - Vessel, Hosted Entity, Companion, and roster vocabulary
4. `604cc89` - explicit Vessel stat sourcing and growth profiles

The record was archived after the final terminology boundary passed its complete verification gate.

## Recovery Findings

| ID | Severity | Finding | Required correction | Status |
|---|---|---|---|---|
| R1 | Medium | Catalog actor restoration accepts saved effective stats without recomposing a Vessel from its active Hosted Entity. | Add a framework-owned composition-aware restore boundary that rejects stale or inconsistent derived state. | Complete - public catalog restoration always recomposes through the injected stat service. Only trusted Framework fusion and Compendium transactions can use the internal validated-snapshot preservation path. Training Annex restores its Vessel once with the saved Hosted Entity and equipment context before publishing success. |
| R2 | Medium | Live stat composition validates roster ID syntax but not duplicate IDs or cross-role overlap. | Share and enforce actor-roster invariants before any live commit. | Complete - one framework invariant service guards construction, stat-composition commits, direct runtime restoration, and save validation against duplicates, active-roster duplication, and hosted/companion role collisions. Valid shared fixtures obey the invariant, and a dedicated restore regression protects it. |
| R3 | Low | DemoHost applies a roster transition before recomposing Vessel stats. A failed composition could leave the aggregate roster and actor state inconsistent. | Assess composition against the proposed roster and commit both changes only after success. | Complete - the host composes against the immutable proposed roster first, records whether the operation committed, and leaves both roster and player snapshots unchanged on rejection. |
| R4 | Low | Retired public names remain in diagnostics: `MissingParentForm` and `DuplicateActorFormReference`. | Rename the symbols and their active test/document references with no compatibility aliases. | Complete - the public codes are now `MissingParentActorState` and `DuplicateActorRosterReference`; adjacent framework diagnostics use Hosted Entity and roster language. |
| R5 | Decision | Ruleset documents use the generic `turn_economy` category rather than an `action_token` category. | Keep the generic category intentionally because Action Token is optional, and document that `standard_action_token` is the supplied implementation ID. | Complete - active migration, mechanics, and content-contract documentation now distinguish the generic category from the supplied policy ID. |

## Checkpoint Completion

- **Checkpoint 4 audit (complete):** creation, growth, battle, equipment, roster changes, and restore now use one explicit Vessel stat-composition model. Public restore cannot bypass recomposition; roster invariants guard live and restored state; standard core-stat composition preserves registered non-core effective stats; and level growth plus recomposition stages on a clone before one live commit. End-to-end host regressions prove a Hosted Entity swap survives growth and save restoration, while rejected recomposition leaves progression, resources, stats, and roster state unchanged.
- **Checkpoint 5 (complete):** active example content, fixture IDs, ownership diagnostics, DemoHost messages, and mechanics documentation now use Credits, Sample Depths, Battle Exit Charm, Return Beacon, Recovery Pulse, Catalyst, Last Stand, Vessel, Hosted Entity, Companion, and roster vocabulary. Retail shop stock remains intentionally named stock. `Almighty` and `Ice Boost` remain the approved generic exceptions. Superseded readiness and interrupted-run reports moved into the non-built archive. The checkpoint passed 722 tests (570 Framework and 152 DemoHost), with no failures or skips; the nonincremental .NET 8 build reported zero warnings and zero errors; all five DemoHost modes exited successfully.
- **Checkpoint 6 (complete):** a token-aware boundary now scans active source, tests, content, documentation, project files, root Markdown/JSON files, and their relative paths. It distinguishes identifier segments from incidental words, checks exact wire values and multiword references, and excludes `ArchiveDocs`, `bin`, and `obj`. Six focused regressions cover positive detection, approved vocabulary, retail stock terminology, path inclusion/exclusion, path scanning, and deterministic locations. The final gate passed 728 tests (576 Framework and 152 DemoHost), with no failures or skips; the nonincremental .NET 8 build reported zero warnings and zero errors; all five DemoHost modes succeeded; 27 active Markdown files passed local-link validation; Framework boundary searches returned zero matches; and active content remained unchanged.

## Commit Discipline

Each recovery finding receives an independent green commit. Checkpoints 5 and 6 retain the planned commit subjects:

- `data: neutralize active example vocabulary`
- `test: enforce convergence terminology boundary`

No finding is marked complete until focused tests, the affected project build, and `git diff --check` pass. The final migration gate additionally requires the full test suite, zero-warning .NET 8 builds, all five DemoHost modes, scripted Training Annex coverage, documentation-link validation, and the terminology scan.
