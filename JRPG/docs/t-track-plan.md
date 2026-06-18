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

The first consumer may be noninteractive or lightly interactive. It should not replace the ordinary console prototype until parity is proven.

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
