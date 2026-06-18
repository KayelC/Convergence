# Track T Framework Completion Roadmap

> **Status: Active forward plan.** Track T starts after Track R and the Track S archive gate. Its purpose is to finish missing framework capability through small clean vertical slices, not to archive active legacy code.

## Summary

The recovery branch now has a strong framework architecture, but the framework is not feature-complete. The console prototype and legacy data remain protected compatibility systems until a specific capability reaches real clean parity.

Track T moves production forward by building missing framework capability and original clean content. It does not mechanically convert the prototype `Data/Jsons` records, and it does not move active source into `ArchiveDocs/LegacyFramework`.

## Current Gap Map

### Framework-Ready Foundations

- Content contracts, strict deserialization, validation, catalog loading, and qualified IDs exist for all major content families.
- Runtime state snapshots, party/stock transitions, resource transactions, field/dungeon transitions, fusion/Compendium services, battle orchestration, action execution, status lifecycle, and persistence snapshots exist in the framework.
- Console-host adapters prove that the legacy prototype can consume framework services without breaking current behavior.
- Godot integration is contract-proven through test-only adapters, but no real Godot project or adapter package exists.

### Adapter-Backed Or Partial Systems

- Battle actions, enemy AI/tactics, negotiation, rewards, field actions, shops, hospital, dungeon traversal, fusion, Compendium, and presentation are still partly console-host compatibility flows.
- Many systems use framework services, but the live consumer is still the console prototype and its legacy objects.
- The clean demos prove slices, not full production gameplay.

### Incomplete Framework Authority

- Rulesets are defined as catalog content, but default combat, progression, economy, stock, reward, and dungeon policies are still mostly named code defaults rather than authored ruleset bindings.
- Production content authority is not established. Legacy `Data/Jsons` is prototype-only evidence, not shippable clean content.
- Interactive save/load menus and save-version migration tooling do not exist; Track R provides serializer-neutral snapshot contracts only.
- AI/tactics policy is not a complete authored framework system.
- A full clean production runtime loop with original content is not wired end to end.

### Archive Status

- `ArchiveDocs/LegacyFramework` is policy-only at the start of Track T.
- Every protected legacy capability remains `removalAuthorized: false`.
- Active compatibility code must stay in place until a later archive review proves a specific file is unreachable through migrated consumers.

## Track Sequence

### Track T1: Framework Completion Audit

Create and maintain this roadmap as the source of truth for remaining framework work.

Deliverables:

- Document the current gap map.
- Keep `ArchiveDocs/LegacyFramework` policy-only.
- Add tests proving the active roadmap exists and the archive-later rule is visible.
- Do not modify production JSON, migrate consumers, or archive source.

Exit gate:

- Full solution tests pass.
- Framework build remains warning-free.
- Clean battle, field, and save demos pass.
- `Data/Jsons` is unchanged.

### Track T2: Authored Ruleset Binding

Begin moving named default policies toward catalog-backed ruleset selection where the framework already has stable policy contracts.

Boundaries:

- Preserve current behavior unless a gameplay change is explicitly approved.
- Do not invent balance formulas in JSON without a corresponding framework policy contract.
- Do not make legacy prototype datasets authoritative clean production content.

Candidate surfaces:

- combat and reward ruleset selection;
- progression/resource policy selection;
- economy/shop policy selection;
- stock capacity and dungeon transition policy references.

### Track T3: Original Clean Content Vertical Slice

Create a tiny original clean content pack that is not mechanically derived from legacy data.

The first slice is `convergence.training_annex_slice` `0.1.0`, a neutral testbed pack with one playable actor, one enemy, one race, active/passive skills, one item, one encounter, one small dungeon segment, and standard ruleset records.

The slice should include enough content to prove:

- one playable actor and one enemy;
- active and passive skills;
- at least one item;
- one encounter;
- one dungeon or field segment;
- one reward path;
- one ruleset binding set.

The pack should remain intentionally small so behavior can be reviewed and owned.

### Track T4: Clean Runtime Consumer Slice

Wire the original clean content pack into a clean host path.

The slice should prove:

- catalog load;
- actor hydration;
- field or dungeon transition;
- battle execution;
- item or skill use;
- reward/progression update;
- save snapshot validation after the loop.

The first consumer is the noninteractive `--clean-training-annex-demo` host command. It loads only `convergence.training_annex_slice`, binds the slice's standard rulesets, hydrates `echo_adept` and `ashling`, traverses the tiny dungeon segment, uses `annex_tonic` through the clean action executor, runs a deterministic automated battle, applies rewards through the bound growth service, and validates a runtime save snapshot.

It does not replace the ordinary console prototype. It is the first original clean runtime consumer proof, not a parity promotion for legacy systems.

### Track T5: Archive Candidate Review

Review whether any specific legacy path is now unreachable because clean content and clean consumers own the behavior.

Rules:

- Archive only one narrow surface at a time.
- Preserve retired code under `ArchiveDocs/LegacyFramework/<track-or-gate>/<original-relative-path>`.
- Set `clean_parity`, `consumerMigrated: true`, and `removalAuthorized: true` only for the specific proven capability.
- Never archive a subsystem simply because a demo covers one example.

## Quality Gate

Every Track T subtrack must run:

- focused tests for the changed subsystem;
- `dotnet test JRPG.sln --no-restore`;
- `dotnet build JRPG.Framework/JRPG.Framework.csproj --no-restore --no-incremental /clp:Summary`;
- `dotnet run --no-build -- --clean-battle-demo`;
- `dotnet run --no-build -- --clean-field-demo`;
- `dotnet run --no-build -- --clean-save-demo`;
- `git diff --check`;
- framework forbidden-reference search;
- `git status --short -- Data/Jsons`.

## Assumptions

- The framework architecture is ready for continued production work, but the framework is not finished.
- Legacy code is preserved until specific clean parity is proven.
- Original clean content is preferred over direct conversion from the prototype data.
- The console host remains useful as compatibility evidence and as a demo host while Godot-facing contracts mature.

## Track T1 Completion

Track T1 is the roadmap and guardrail pass. It adds no runtime behavior, no production JSON conversion, and no archive movement.

- Added this roadmap as the active forward lane after Track R and the Track S archive gate.
- Added documentation links from the active docs index, architecture overview, production baseline, and framework parity plan.
- Added guardrail tests proving `ArchiveDocs/LegacyFramework` remains policy-only and the recovery parity ledger still authorizes no removals.
- Focused Track T tests passed: 4 passed, 0 failed, 0 skipped.
- Full solution tests passed: 725 passed, 0 failed, 0 skipped.
- Framework build passed: 0 warnings, 0 errors.
- Nonincremental solution build passed: 98 warnings, 0 errors.
- Clean demos passed: battle demo ended in player-team victory, field demo completed, and save demo restored 2 actors, 1 item stack, and dungeon floor 5.
- Quality gates passed: `git diff --check`, framework forbidden-reference search, and `git status --short -- Data/Jsons`.

## Track T2 Completion

Track T2 adds authored ruleset binding without changing runtime behavior or promoting prototype JSON into production authority.

- Added `RuntimeRulesetBindingResolver` and standard policy IDs under the framework runtime layer.
- The resolver binds catalog `RulesetDefinition` records to existing standard framework services for damage, rewards, stats, growth, stock capacity, economy/resource management, Press Turn, and moon-phase policy validation.
- Standard damage binding supports only the approved `weakMultiplier` and `resistMultiplier` parameters; unsupported parameters and bad values produce stable diagnostics instead of silent behavior changes.
- Hosts still supply randomness explicitly. The framework does not invent hidden random sources while binding combat rulesets.
- Production content remains unconverted, no gameplay consumer switches to clean catalog ruleset authority, and no legacy source is archived.
- Focused Track T2 tests passed: 3 passed, 0 failed, 0 skipped.
- Full solution tests passed: 728 passed, 0 failed, 0 skipped.
- Framework build passed: 0 warnings, 0 errors.

## Track T3 Completion

Track T3 adds the first original clean content seed without converting prototype data or switching a gameplay consumer.

- Added `convergence.training_annex_slice` `0.1.0` under `Data/Jsons`.
- The pack contains `annex_spirit`, playable `echo_adept`, enemy `ashling`, active skills `echo_strike` and `ash_spark`, passive `steady_breath`, item `annex_tonic`, encounter `ashling_drill`, dungeon `training_annex`, and eight standard ruleset records.
- The pack is self-contained and uses no legacy prototype pack dependency.
- `OriginalCleanContentSliceTests` load only the Training Annex slice, prove catalog qualification, reject local-ID lookups, bind standard rulesets through `RuntimeRulesetBindingResolver`, and calculate a nonzero reward.
- Existing legacy/prototype `Data/Jsons` files remain unchanged; T3 adds only the new clean slice files.
- No runtime consumer switch, gameplay rule change, parity-ledger removal authorization, or source archive movement occurs in T3.
- Focused Track T3 tests passed: 3 passed, 0 failed, 0 skipped.
- Full solution tests passed: 731 passed, 0 failed, 0 skipped.
- Framework build passed: 0 warnings, 0 errors.
- Nonincremental solution build passed: 98 warnings, 0 errors.
- Clean demos passed: battle demo ended in player-team victory, field demo completed, and save demo restored 2 actors, 1 item stack, and dungeon floor 5.
- Quality gates passed: `git diff --check`, framework forbidden-reference search, and Data/Jsons delta review showing only the new Training Annex slice files.

## Track T4 Completion

Track T4 creates the first clean runtime consumer for original clean content.

- Added `--clean-training-annex-demo` through `CleanTrainingAnnexDemoHost`.
- The demo loads only `training_annex_slice.*.json`, binds damage, reward, growth, stat, Press Turn, stock-capacity, economy, and moon-phase rulesets through `RuntimeRulesetBindingResolver`, and never falls back to the legacy `Database`.
- The deterministic flow enters `training_annex`, ascends to floor 2, resolves `ashling_drill`, hydrates `ashling`, damages `echo_adept`, uses `annex_tonic` through `BattleActionExecutor`, commits one host-owned inventory quantity, runs automated battle to victory, calculates nonzero rewards, applies EXP, and validates a `RuntimeSaveGameSnapshot`.
- `CleanTrainingAnnexDemoHostTests` prove the content request is limited to the Training Annex pack, dungeon events occur in order, item consumption commits exactly once, battle wins without faulting, progression changes, and save validation succeeds.
- No production/prototype JSON is edited, no legacy consumer is switched to clean content authority, no parity-ledger capability is promoted to `clean_parity`, and no removal is authorized.
- Focused Track T4 tests passed: 2 passed, 0 failed, 0 skipped.
- Full solution tests passed: 733 passed, 0 failed, 0 skipped.
- Framework build passed: 0 warnings, 0 errors.
- Nonincremental solution build passed: 98 warnings, 0 errors.
- Demo verification passed: clean battle ended in player-team victory, clean field completed, clean save restored 2 actors, 1 item stack, and dungeon floor 5, and Training Annex completed with player-team victory, 1 EXP, 14 Macca, and 0 save diagnostics.
- Quality gates passed: `git diff --check`, refined framework forbidden-reference search, and `git status --short -- Data\Jsons`.

## Track T5 Completion

Track T5 reviews archive eligibility after the Training Annex runtime slice and deliberately archives nothing.

- Added `Convergence.Tests/Fixtures/Parity/archive-candidate-review.t5.json` as the machine-readable review record.
- The review covers all 36 protected recovery capabilities and records the current ledger summary: 3 `clean_foundation`, 32 `parallel_partial`, 1 `legacy_only`, and 0 `clean_parity`.
- Archive candidate count remains 0 and removal authorization count remains 0.
- `ArchiveCandidateReviewTests` prove every recovery capability is reviewed, the review matches the current ledger, archive eligibility requires `clean_parity` plus a migrated consumer plus explicit removal authorization, and `ArchiveDocs/LegacyFramework` remains policy-only.
- Clean demos and the Training Annex runtime slice are recorded as original-content proofs, not as migrated legacy consumers.
- No production/prototype JSON is edited, no parity-ledger status changes, no `removalAuthorized` flag changes, no runtime behavior changes, and no active source is archived.
- Focused Track T5 tests passed: 5 passed, 0 failed, 0 skipped.
- Full solution tests passed: 738 passed, 0 failed, 0 skipped.
- Build verification passed: framework build reported 0 warnings and 0 errors; nonincremental solution build reported 98 warnings and 0 errors.
- Demo verification passed: clean battle ended in player-team victory, clean field completed, clean save restored 2 actors, 1 item stack, and dungeon floor 5, and Training Annex completed with player-team victory, 1 EXP, 14 Macca, and 0 save diagnostics.
- Quality gates passed: `git diff --check`, refined framework forbidden-reference search, and `git status --short -- Data\Jsons`.
