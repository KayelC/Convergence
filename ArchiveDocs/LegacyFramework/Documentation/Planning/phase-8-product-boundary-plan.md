# Phase 8: Convergence Product Boundary And Legacy Archive

**Status:** Complete

**Starting branch:** `track-12-recovery`

**Baseline:** 1,113 tests passed, 0 failed, 0 skipped. The standalone `JRPG.Framework` build produced 0 warnings; the transitional full solution produced 100 legacy/package warnings and 0 errors.

## Checkpoint Status

| Checkpoint | Status | Evidence |
|---|---|---|
| 1. Boundary record | Complete | Commit `6dc5ab3`; recovery baseline recorded before movement. |
| 2. Clean projects | Complete | Commit `543b717`; Framework and DemoHost build independently on .NET 8 with 0 compiler warnings. |
| 3. Content and tests | Complete | Clean content is separated into 36 reference/demo/original documents; 548 Framework tests and 145 DemoHost tests pass with 0 failures and 0 skips. |
| 4. Namespace migration | Complete | Active Framework, DemoHost, and test sources use the `Convergence.*` namespaces and the product-area folder layout; 693 tests pass. |
| 5. Archive gate | Complete | The former executable, adapters, DTOs, prototype data, legacy tests, old solution, plans, reviews, and ledgers are preserved under `ArchiveDocs/LegacyFramework` and excluded from every active project. |

## Purpose

Phase 8 replaces the transitional root console project with an atomic clean product. The reusable assembly, clean example host, clean content, and clean tests become independently understandable and buildable. The retained prototype becomes non-built historical evidence.

This supersedes the earlier interpretation of Phase 8 as presentation work alone. Console presentation is now only one optional host implementation; it is not a prerequisite for framework ownership or Godot integration.

## Final Active Layout

```text
src/Convergence.Framework/
samples/Convergence.DemoHost/
tests/Convergence.Framework.Tests/
tests/Convergence.DemoHost.Tests/
content/reference/
content/demos/
content/original/training-annex/
docs/
ArchiveDocs/LegacyFramework/
Convergence.sln
```

## Ownership Rules

- `Convergence.Framework` owns definitions, content loading, validation, catalogs, rules, runtime state, transitions, diagnostics, and serializer-neutral host contracts.
- `Convergence.DemoHost` owns console input/output, filesystem content reads, host JSON save encoding, command routing, and example orchestration.
- Framework tests reference only `Convergence.Framework`.
- DemoHost tests reference only Framework and DemoHost.
- Clean content contains generic reference, demonstration, and Training Annex records only.
- Archived source, data, tests, plans, and ledgers are not referenced by active projects or active documentation authority.
- A Godot project references `src/Convergence.Framework/Convergence.Framework.csproj`; DemoHost is optional.

## Destination Inventory

| Current ownership | Destination | Reason |
|---|---|---|
| `JRPG.Framework/` | `src/Convergence.Framework/` | Reusable product assembly. |
| Clean `Host/Clean*` and Training Annex files | `samples/Convergence.DemoHost/` | Optional framework-native example host. |
| Framework host adapters without legacy types | DemoHost `Infrastructure/` | Filesystem, text output, random source, and console command ownership belong to the host. |
| Clean runtime/content tests | `tests/Convergence.Framework.Tests/` | Prove the library without host or legacy references. |
| Clean host and Training Annex tests | `tests/Convergence.DemoHost.Tests/` | Prove optional example integration. |
| 36 clean JSON documents | `content/reference`, `content/demos`, or `content/original/training-annex` | Keep generic content visibly separate from implementation. |
| Root `Core`, legacy DTOs, `Entities`, `Logic`, `Services`, old `Program`, and old host files | `ArchiveDocs/LegacyFramework/ConsolePrototype/Source/` | Retained non-built prototype history. |
| 14 prototype or generated JSON files | `ArchiveDocs/LegacyFramework/ConsolePrototype/Content/` | Prevent prototype data from appearing framework-required. |
| Legacy and mixed characterization tests | `ArchiveDocs/LegacyFramework/ConsolePrototype/Tests/` | Preserve evidence without allowing it to mask clean dependencies. |
| Recovery parity and production ledgers | `ArchiveDocs/LegacyFramework/Evidence/` | Freeze migration history after the product boundary changes. |

## Namespace And Assembly Identity

- Assembly/project: `Convergence.Framework`.
- Public root namespace: `Convergence`.
- Main namespaces: `Convergence.Content`, `Convergence.Hosting`, `Convergence.Battle`, `Convergence.Runtime`, and `Convergence.Fusion`.
- Demo assembly/project and namespace: `Convergence.DemoHost`.
- No `JRPGPrototype.*` compatibility shims or type forwards remain in active code.
- Namespace and assembly changes do not alter JSON wire contracts or save contract versions.

## DemoHost Contract

One executable supports:

```text
--clean-training-annex-play
--clean-training-annex-demo
--clean-battle-demo
--clean-field-demo
--clean-save-demo
--help
```

No arguments or unknown arguments print usage and return nonzero. The interactive host uses `IHostCommandSource<T>` and `IHostEventSink<T>` adapters owned by DemoHost. It does not use `IGameIO`, legacy DTOs, `Database`, legacy actors, or adapters.

## Progress Model

The recovery parity ledger measured coexistence with the old console prototype. It is frozen as history and replaced by a clean capability matrix with these independent fields:

- implementation: `complete`, `partial`, or `deferred`;
- framework test evidence;
- demo coverage: `none`, `focused`, or `end_to_end`;
- host neutrality;
- optional-module status;
- known gaps.

Legacy coexistence no longer prevents a framework capability from being represented accurately.

## Archive Gate

Legacy files may leave the active tree only after the clean solution builds and tests without the root console project. The final active source must contain no references to `ArchiveDocs`, `JRPGPrototype`, `JRPG.ConsoleHost`, Newtonsoft.Json, `Database`, legacy actors/DTOs, `IGameIO`, or `Legacy*Adapter` types.

The old Moon Phase console implementation is archived. Nullable moon-phase metadata remains an optional framework extension point; DemoHost does not register, bind, or require it.

## Verification

- Record the current 1,113-test recovery baseline before movement.
- Build Framework and DemoHost independently on .NET 8 with zero compiler warnings.
- Run every clean test with no failures or skips and record the new exact total.
- Run all five DemoHost modes and scripted interactive coverage.
- Validate every active content document through its manifest or a contract test.
- Preserve the Godot host-contract proof without engine types entering Framework.
- Run active-source forbidden-reference searches, active-doc link validation, archive-reference searches, and `git diff --check`.

## Commit Sequence

1. `docs: define convergence product boundary`
2. `architecture: extract convergence framework and demo host`
3. `test: separate clean framework and demo coverage`
4. `refactor: adopt convergence public namespaces`
5. `cleanup: archive legacy console prototype`

## Completion Record

Phase 8 completed on `track-12-recovery` on 2026-07-14.

- Active product: one dependency-free `net8.0` Framework library, one optional DemoHost, and two clean test projects in `Convergence.sln`.
- Clean verification: 553 Framework tests and 145 DemoHost tests passed, for 698 total with 0 failures and 0 skips.
- Build verification: Framework, DemoHost, and the complete solution each built nonincrementally with 0 warnings and 0 errors.
- Content verification: 36 active JSON files across 6 manifests have unique filenames and exactly one manifest owner.
- Archive inventory: 132 former source/project files, 36 legacy test/project files, and 14 prototype JSON files are retained outside the active build.
- Runtime verification: battle, field, save, and Training Annex automated demos exited successfully; the interactive Training Annex host accepted scripted input and exited normally; `--help` reported the complete command surface.
- Host-boundary verification: the Godot-shaped contract proof passed, active Framework source contains no console/filesystem/Godot/Newtonsoft/legacy dependency, and DemoHost neither registers nor binds Moon Phase.
- Repository verification: active documentation links passed, forbidden-reference searches returned no production matches, and `git diff --check` reported no whitespace errors.

The 1,113-test figure above remains the frozen pre-archive recovery baseline. It is not the clean product test count: legacy characterization and adapter tests are intentionally retained as non-built historical evidence.

### Git-Root Promotion

The final repository-entry-point correction was completed on `main` on
2026-07-14:

- `Convergence.sln`, `global.json`, the clean README, and all active product
  directories now live directly at the Git root;
- the superseded root `JRPG.sln`, README, `Documentation/`, and
  `Old Files Archive/` are preserved under
  `ArchiveDocs/LegacyRepository`;
- active product-boundary tests verify that the product root and Git root match
  whenever Git metadata is present, every solution project resolves from that
  root, and the retired entry points remain archived;
- the old nested `JRPG/` path contains no tracked product files and is not part
  of a clean checkout.
