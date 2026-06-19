# Repository Architecture And Declutter Proposal

> **Status: Proposal.** This document analyzes the current repository layout and proposes a cleaner physical architecture. It does not approve moving files, deleting files, archiving live code, renaming namespaces, or changing runtime behavior.

## Why This Exists

The framework work created a real implementation boundary, but the repository still looks like the old monolithic console prototype:

- the console host project lives at the repository root;
- framework code lives in its own project folder;
- tests live in their own project folder;
- legacy data, clean demo data, original clean content, historical generated data, docs, archive material, and generated tooling output are all visible beside each other;
- the console project must explicitly exclude `Convergence.Tests` and `JRPG.Framework` from compilation because it is rooted above them.

That makes the project hard to own. The next cleanup should therefore be architectural and physical before it is behavioral.

## Inventory Method

This review used Git-tracked files as the source inventory, not transient filesystem output.

Command used:

```powershell
git -c safe.directory=C:/Users/kayel/Documents/GitHub/Convergence ls-files -- JRPG
```

The current `JRPG` subtree has **389 tracked files**.

Build outputs under `bin/` and `obj/` exist on disk, but they are ignored by the parent repository `.gitignore` and are not part of the tracked source architecture.

## Current Top-Level Layout

| Current Location | Tracked Files | Current Role | Problem |
| --- | ---: | --- | --- |
| `JRPG.ConsoleHost.csproj` | 1 | Root console executable project | Project is at the same level as source, tests, framework, docs, archive, and data. |
| `Program.cs` | 1 | Console-host entry point | Root-level executable file makes the root look like a project folder rather than a repo folder. |
| `Core/` | 3 | Legacy console-host core enums/results/helpers | Console-owned code is not inside a console-host project folder. |
| `Data/` | 62 | Legacy DTOs, legacy JSON, clean demo JSON, original clean content | Multiple content types are mixed under one name. |
| `Entities/` | 7 | Legacy live runtime actors and adapters | Console-owned runtime objects are root-level. |
| `Host/` | 10 | Console game host and clean demo hosts | Host code is root-level instead of under the console host project. |
| `Logic/` | 92 | Legacy console workflows, adapters, presentation bridges, compatibility facades | Largest root-level source area; mixes battle, field, fusion, adapters, presentation, and old runtime logic. |
| `Services/` | 3 | Console I/O and menu helpers | Console-only services are root-level. |
| `Properties/` | 1 | Console-host assembly info | Root-level project artifact. |
| `JRPG.Framework/` | 63 | Engine-neutral framework library | Mostly correct project boundary, but internal names still carry old history. |
| `Convergence.Tests/` | 73 | Test project, fixtures, parity ledgers | Good project boundary, but test files are mixed between root tests and subfolders. |
| `docs/` | 27 | Active documentation | Mostly cleaned up; needs this architecture proposal linked into the current doc order. |
| `ArchiveDocs/` | 44 | Historical plans and generated technical notes | Good as non-authoritative history; should not receive live source until archive gate passes. |
| `JRPG.sln` | 1 | Solution file | Correct root-level file. |
| `repomix-output.xml` | 1 | Generated repository bundle | Tracked generated artifact at root; should be archived or removed from active source after approval. |

## Current File Ownership Map

### Root Console Host

These files currently belong to the console host even though they sit at the repository root:

- `JRPG.ConsoleHost.csproj`
- `Program.cs`
- `Properties/AssemblyInfo.cs`
- `Core/CombatResult.cs`
- `Core/ElementHelper.cs`
- `Core/Enums.cs`
- `Services/ConsoleIO.cs`
- `Services/IGameIO.cs`
- `Services/MenuUI.cs`

These are not framework files. They should eventually move under a dedicated console-host project folder.

### Legacy Console Data And Prototype Content

`Data/*.cs` contains legacy DTOs and the static legacy database:

- `Data/AccessoryData.cs`
- `Data/AilmentData.cs`
- `Data/ArmorData.cs`
- `Data/BootData.cs`
- `Data/Database.cs`
- `Data/DungeonData.cs`
- `Data/ItemData.cs`
- `Data/NegotiationData.cs`
- `Data/PersonaData.cs`
- `Data/ShopData.cs`
- `Data/SkillData.cs`
- `Data/WeaponData.cs`

`Data/Jsons/` currently contains several different content classes:

- legacy prototype datasets: `skills_database.json`, `entity_database.json`, `items.json`, `weapons.json`, `armor.json`, `boots.json`, `accessories.json`, `status_ailments.json`, `fusion_table.json`, `questions.json`, `shop_inventory.json`, `tartarus.json`;
- historical generated datasets: `skills_database_v2.json`, `entity_database_v2.json`;
- redesign/reference fixtures: `skill_system_redesign.*.sample.json`;
- clean demo packs: `clean_battle_demo.*`, `shared_effects_demo.*`, `status_lifecycle_demo.*`, `catalog_surface_sample.*`;
- original clean content: `training_annex_slice.*`.

This folder is the biggest content ownership problem. It should eventually be split by purpose, not by file extension.

### Legacy Live Runtime Actors

These are console-host runtime models and compatibility adapters:

- `Entities/Combatant.cs`
- `Entities/Persona.cs`
- `Entities/Components/CombatantFactory.cs`
- `Entities/Components/DamageHandler.cs`
- `Entities/Components/GrowthProcessor.cs`
- `Entities/Components/LegacyProgressionAdapter.cs`
- `Entities/Components/StatProcessor.cs`

They are still active compatibility code. They are not archive candidates yet.

### Console Host Workflows And Presentation

`Host/` owns startup, scenarios, and noninteractive clean demo hosts:

- `Host/CleanBattleDemoHost.cs`
- `Host/CleanFieldDemoHost.cs`
- `Host/CleanSaveDemoHost.cs`
- `Host/CleanTrainingAnnexDemoHost.cs`
- `Host/ConsoleGameHost.cs`
- `Host/DebugScenarioRunner.cs`
- `Host/FrameworkHostAdapters.cs`
- `Host/InteractiveConsoleHostContext.cs`
- `Host/ScenarioFactory.cs`
- `Host/ScenarioSetupResult.cs`

`Logic/` is console-host application logic and adapter code:

- `Logic/Battle/**`: legacy battle execution, battle presentation bridges, compatibility adapters, messaging, legacy effects, status, AI, negotiation, and battle conductor.
- `Logic/Core/**`: party, inventory, economy, moon phase, runtime identity, and compatibility adapters.
- `Logic/Field/**`: field conductor, dungeon manager facade, field services, shop/hospital presentation, inventory/status/party-stock bridges, dungeon traversal presentation, and field messaging.
- `Logic/Fusion/**`: Cathedral conductor, fusion calculator/mutator, legacy fusion content adapter, compendium registry, presentation bridges, messaging, planning, preview, strategies, and transactions.

This area should move physically under the console-host project before any deeper cleanup.

### Framework Library

`JRPG.Framework/` is the reusable library and should remain the framework boundary.

Current areas:

- `JRPG.Framework/Data/Definitions/**`: immutable content definitions and shared vocabulary.
- `JRPG.Framework/Data/SkillSystem/**`: deserialization, schema DTOs, validation, catalog loading, semantic versioning, and content-pack construction.
- `JRPG.Framework/Logic/Battle/**`: clean battle rules, affinity/resistance resolution, Press Turn, knowledge stores, execution, action facade, status lifecycle, actor factory, automated battle, encounter runner, negotiation/rewards.
- `JRPG.Framework/Logic/Fusion/**`: fusion runtime services and inheritance evaluation.
- `JRPG.Framework/Logic/Runtime/**`: field/dungeon state machines, encounter start planning, progression, party/stock, resources, persistence snapshots, ruleset bindings, and runtime snapshots.
- `JRPG.Framework/Hosting/**`: engine-neutral host contracts.
- `JRPG.Framework/Core/HitType.cs`: legacy compatibility enum used by shared Press Turn behavior.
- `JRPG.Framework/Entities/Components/CombatDefenseProfile.cs`: clean combat defense state in an old namespace/location.

The framework boundary is broadly correct. The main issue is naming:

- `Data/SkillSystem` now loads far more than skills.
- `Entities/Components/CombatDefenseProfile.cs` is clean combat runtime data, not a legacy entity component.
- namespaces still use `JRPGPrototype.*`, which was intentionally retained for compatibility but now obscures the new architecture.

### Tests

`Convergence.Tests/` contains:

- architecture and boundary tests;
- host/demo tests;
- parity and characterization tests;
- runtime framework tests;
- skill/content/catalog tests;
- fixtures and ledgers;
- test support utilities.

The project boundary is fine. The internal organization can improve:

- old root-level tests should move under `ConsoleHost/Legacy` or `Framework` categories;
- clean framework runtime tests should live under `Framework/Runtime`;
- content/catalog tests should live under `Framework/Content`;
- host presentation tests should live under `ConsoleHost/Presentation`;
- parity ledgers should stay explicit and separate.

### Active Docs And Archive Docs

`docs/` is the active documentation set and should stay small and current.

`ArchiveDocs/` is historical and non-authoritative. It currently contains:

- old planning tracks;
- discarded migration reports;
- generated technical fusion docs;
- `ArchiveDocs/LegacyFramework`, which is policy-only until a capability reaches true clean parity.

This split is healthy. The main addition needed is this repository architecture map.

### Generated Root Artifact

`repomix-output.xml` is tracked at the repository root.

This is not source code, not active documentation, and not runtime content. It should not live beside the solution and project files.

Recommended future handling:

1. confirm whether anyone still uses it;
2. if it is only historical, move it to `ArchiveDocs/Generated/repomix-output.xml`;
3. if it can be regenerated, remove it from tracked source after approval and document the generation command.

## Main Findings

### 1. The Root Is Doing Too Many Jobs

The root currently acts as:

- repository root;
- console-host project root;
- content root;
- docs root;
- archive root;
- framework sibling;
- tests sibling;
- generated-artifact location.

That is why the project feels scattered.

### 2. The Console Host Needs Its Own Physical Project Folder

The console host is already conceptually separate from the framework, but physically it is not.

The current `JRPG.ConsoleHost.csproj` has to say:

```xml
<Compile Remove="Convergence.Tests\**\*.cs" />
<Compile Remove="JRPG.Framework\**\*.cs" />
```

That is a sign the project file is too high in the tree.

### 3. Content Needs Purpose-Based Folders

`Data/Jsons` mixes:

- active legacy prototype data;
- discarded generated migration data;
- clean reference fixtures;
- clean demos;
- original framework sample content.

Those categories have different meanings. Keeping them together makes old prototype data look equal to original clean framework content.

### 4. Framework Naming Lags Behind Framework Scope

The framework is no longer just a skill system. The folder name `Data/SkillSystem` is historically understandable, but it now hides catalog loading for items, equipment, encounters, dungeons, fusion, rulesets, and more.

### 5. Archive Discipline Is Good, But Should Stay Strict

`ArchiveDocs/LegacyFramework` should not become a dumping ground. The current policy is correct:

- no active source moves there until the parity ledger allows it;
- historical docs belong there;
- live compatibility code stays active until replaced.

### 6. Tests Are Strong, But Their Layout Mirrors The History

The test suite protects the project well, but some tests still live at the test root because they predate the newer framework/host split.

That is not a behavior problem, but it is an ownership problem.

## Proposed Target Architecture

This is the recommended final physical layout for the `JRPG` subtree:

```text
JRPG/
  JRPG.sln
  README.md

  src/
    JRPG.Framework/
      JRPG.Framework.csproj
      Common/
      Content/
        Definitions/
        Loading/
        Schemas/
        Validation/
        Catalog/
      Runtime/
        Actors/
        Battle/
        Field/
        Fusion/
        Progression/
        Resources/
        Persistence/
        Rulesets/
      Hosting/

    JRPG.ConsoleHost/
      JRPG.ConsoleHost.csproj
      Program.cs
      Legacy/
        Core/
        Data/
        Entities/
        Battle/
        Field/
        Fusion/
        Services/
      Adapters/
        Framework/
        Content/
      Presentation/
        Battle/
        Field/
        Fusion/
      Demos/
        CleanBattle/
        CleanField/
        CleanSave/
        TrainingAnnex/

  tests/
    Convergence.Tests/
      Convergence.Tests.csproj
      Architecture/
      Framework/
        Content/
        Runtime/
        Battle/
        Fusion/
        Persistence/
      ConsoleHost/
        Legacy/
        Presentation/
        Demos/
      Parity/
      Fixtures/
      TestSupport/

  content/
    legacy-prototype/
      json/
    historical-generated/
      json/
    clean-reference/
      skill-system-redesign/
    clean-demos/
      battle/
      shared-effects/
      status-lifecycle/
      catalog-surface/
    original/
      training-annex/

  docs/
    README.md
    framework-state-and-roadmap.md
    repository-architecture-proposal.md
    framework-completion/
    subsystems/

  ArchiveDocs/
    README.md
    Planning/
    TechnicalDocs/
    Generated/
    LegacyFramework/
```

## Proposed Current-To-Target Mapping

| Current Location | Target Location | Timing |
| --- | --- | --- |
| `JRPG.ConsoleHost.csproj` | `src/JRPG.ConsoleHost/JRPG.ConsoleHost.csproj` | Early physical cleanup. |
| `Program.cs` | `src/JRPG.ConsoleHost/Program.cs` | Same pass as console project move. |
| `Core/` | `src/JRPG.ConsoleHost/Legacy/Core/` | Same pass as console project move. |
| `Services/` | `src/JRPG.ConsoleHost/Legacy/Services/` | Same pass as console project move. |
| `Entities/` | `src/JRPG.ConsoleHost/Legacy/Entities/` | Same pass as console project move. |
| `Data/*.cs` | `src/JRPG.ConsoleHost/Legacy/Data/` | Same pass as console project move. |
| `Host/` | `src/JRPG.ConsoleHost/Demos/` and `src/JRPG.ConsoleHost/Legacy/Host/` | Split host demos from ordinary legacy host. |
| `Logic/Battle/` | `src/JRPG.ConsoleHost/Legacy/Battle/` plus `Presentation/Battle/` and `Adapters/Framework/` | Later console-host internal cleanup. |
| `Logic/Core/` | `src/JRPG.ConsoleHost/Legacy/Core/` plus `Adapters/Framework/` | Later console-host internal cleanup. |
| `Logic/Field/` | `src/JRPG.ConsoleHost/Legacy/Field/` plus `Presentation/Field/` | Later console-host internal cleanup. |
| `Logic/Fusion/` | `src/JRPG.ConsoleHost/Legacy/Fusion/` plus `Presentation/Fusion/` | Later console-host internal cleanup. |
| `JRPG.Framework/` | `src/JRPG.Framework/` | Physical move first; namespace cleanup later. |
| `JRPG.Framework/Data/SkillSystem/` | `src/JRPG.Framework/Content/` | Rename after physical move, not during it. |
| `JRPG.Framework/Entities/Components/CombatDefenseProfile.cs` | `src/JRPG.Framework/Runtime/Battle/CombatDefenseProfile.cs` | Framework internal cleanup after move. |
| `Data/Jsons/*legacy*` | `content/legacy-prototype/json/` | Content split pass. |
| `Data/Jsons/*_v2.json` | `content/historical-generated/json/` or `ArchiveDocs/Generated/` | Historical cleanup pass. |
| `Data/Jsons/skill_system_redesign.*` | `content/clean-reference/skill-system-redesign/` | Content split pass. |
| `Data/Jsons/*demo*` and `catalog_surface_sample.*` | `content/clean-demos/` | Content split pass. |
| `Data/Jsons/training_annex_slice.*` | `content/original/training-annex/` | Content split pass. |
| `Convergence.Tests/` | `tests/Convergence.Tests/` | Test project move after source project moves. |
| root-level test files inside `Convergence.Tests/` | `tests/Convergence.Tests/ConsoleHost/Legacy/` or `Framework/` | Test internal cleanup. |
| `repomix-output.xml` | `ArchiveDocs/Generated/repomix-output.xml` or removed after approval | Generated artifact cleanup. |

## Migration Strategy

Do not do this as one giant move. The safe order is:

### Pass 1: Document And Guard

Purpose:

- approve this architecture proposal;
- record the target structure;
- add this doc to active documentation;
- do not move code yet.

Verification:

- `git diff --check`.

### Pass 2: Move The Console Host Into `src/`

Purpose:

- create `src/JRPG.ConsoleHost/`;
- move `JRPG.ConsoleHost.csproj`, `Program.cs`, `Core`, `Data`, `Entities`, `Host`, `Logic`, `Services`, and `Properties` under it;
- update solution and project references;
- preserve namespaces and behavior;
- keep `Data/Jsons` path behavior working through project content includes.

Non-goal:

- no namespace renames;
- no content split;
- no legacy archive.

Verification:

- full test suite;
- all clean demos;
- ordinary startup characterization;
- `git diff --check`.

### Pass 3: Move The Framework Into `src/`

Purpose:

- move `JRPG.Framework/` to `src/JRPG.Framework/`;
- update solution and project references;
- preserve all namespaces.

Non-goal:

- no internal folder renames yet.

Verification:

- framework build;
- full solution build;
- full test suite;
- framework boundary tests.

### Pass 4: Move Tests Into `tests/`

Purpose:

- move `Convergence.Tests/` to `tests/Convergence.Tests/`;
- update solution and project references;
- keep fixtures working.

Verification:

- full test suite.

### Pass 5: Split Content By Purpose

Purpose:

- move legacy prototype JSON, clean reference content, clean demos, original content, and historical generated data into separate content folders;
- update console-host copy rules and host content source paths;
- keep legacy `Database.LoadData` behavior intact.

Non-goal:

- no content rewriting;
- no schema changes;
- no production conversion.

Verification:

- dataset preservation tests;
- clean catalog tests;
- all clean demos;
- ordinary startup characterization.

### Pass 6: Framework Internal Naming Cleanup

Purpose:

- rename framework folders to match current scope:
  - `Data/SkillSystem` -> `Content`;
  - `Entities/Components/CombatDefenseProfile.cs` -> battle/runtime location;
- optionally begin namespace cleanup from `JRPGPrototype.*` to a future approved namespace.

Non-goal:

- no behavior changes.

Verification:

- public API boundary tests;
- full test suite.

### Pass 7: Test Layout Cleanup

Purpose:

- move test files into ownership-based folders;
- keep test names and assertions stable.

Verification:

- full test suite.

### Pass 8: Generated Artifact Cleanup

Purpose:

- move or remove `repomix-output.xml` after approval.

Verification:

- no runtime/test dependency on the file;
- docs updated with regeneration/archive policy.

## Priority Markers

| Cleanup Item | Priority | Reason |
| --- | --- | --- |
| Approve repository architecture proposal | `P0` | Needed before moving files. |
| Move console host into `src/JRPG.ConsoleHost` | `P1` | Biggest source of repo confusion. |
| Move framework into `src/JRPG.Framework` | `P1` | Makes project structure standard and clearer. |
| Move tests into `tests/Convergence.Tests` | `P1` | Makes solution shape easier to scan. |
| Split `Data/Jsons` by content purpose | `P1` | Prevents legacy prototype data from looking like framework-owned production content. |
| Move/retire `repomix-output.xml` | `P1` | Root generated artifact is noise. |
| Rename framework `Data/SkillSystem` internals | `P2` | Helpful, but safer after physical moves. |
| Move `CombatDefenseProfile` to clean battle/runtime folder | `P2` | Clarifies framework ownership. |
| Reorganize test internals | `P2` | Improves navigation, but less urgent than project layout. |
| Namespace migration away from `JRPGPrototype.*` | `P3` | Valuable eventually, but high churn and not required for immediate ownership. |
| Archive live legacy code | `Blocked` | Requires `clean_parity`, migrated consumers, tests, and explicit removal authorization. |

## Rules For The Declutter Work

- Move files before renaming namespaces.
- Do not mix physical moves with gameplay changes.
- Do not split content and source projects in the same pass.
- Do not archive live compatibility code during repository declutter.
- Do not delete historical material without explicit approval.
- Keep old namespaces temporarily if that avoids unnecessary churn.
- After every pass, run focused path checks, full tests, builds, demos, and `git diff --check`.
- Update active docs when ownership changes.

## Recommended Immediate Next Step

Review and approve or amend this target architecture.

If approved, the first implementation pass should be:

```text
Repository Declutter Pass 1:
Move the console host into src/JRPG.ConsoleHost without changing namespaces or behavior.
```

That is the highest-value cleanup because it removes the root/project confusion and eliminates the need for the console project to exclude sibling projects from compilation.
