# Current Repository Map

**Status:** Active orientation guide
**Purpose:** Explain what the repository currently builds, what each top-level area owns, and which files are historical.

This document describes the repository as it exists today. It is not a proposed end-state layout. The longer-term physical reorganization is tracked separately in [repository-architecture-proposal.md](repository-architecture-proposal.md).

## The Short Version

The repository currently builds three projects:

| Project | Location | Purpose |
|---|---|---|
| `JRPG.Framework` | `JRPG.Framework/` | The reusable, engine-neutral .NET 8 framework. This is the code a Godot project is intended to reference. |
| `JRPG.ConsoleHost` | repository root | The compatibility console application, clean demonstrations, legacy runtime adapters, and legacy prototype. |
| `Convergence.Tests` | `Convergence.Tests/` | Framework, host, content-contract, regression, and legacy-characterization tests. |

The confusing part is that `JRPG.ConsoleHost.csproj` lives at the repository root. SDK-style C# projects compile source files recursively by default, so the root-level `Core/`, `Data/`, `Entities/`, `Host/`, `Logic/`, `Services/`, `Program.cs`, and `Properties/` folders are all part of the console-host project. They are not loose or abandoned files.

`JRPG.ConsoleHost.csproj` explicitly excludes `JRPG.Framework/**/*.cs` and `Convergence.Tests/**/*.cs`, then references the Framework project. The dependency direction is therefore:

```text
Convergence.Tests -> JRPG.ConsoleHost -> JRPG.Framework
                 \-------------------> JRPG.Framework
```

`JRPG.Framework` does not reference the console host or tests.

## Current Top-Level Ownership

| Path | Current owner and use | Keep? |
|---|---|---|
| `JRPG.Framework/` | Reusable framework definitions, content loading, runtime rules, battle, progression, fusion, persistence, and host-neutral contracts. | Yes. This is the primary product. |
| `Program.cs` | Selects clean demo/play commands or starts the ordinary compatibility console host. | Yes, while the console host remains supported. |
| `Core/` | Console-host startup and legacy data loading. | Yes, compatibility-only. |
| `Data/` excluding `Data/Jsons/` | Legacy console DTOs and host-side data types. | Yes, compatibility-only. |
| `Entities/` | Legacy live console actors, components, and equipment models. | Yes, compatibility-only. |
| `Host/` | Clean console demonstrations, Training Annex host, and console adapters over framework APIs. | Yes. Host-specific, not framework code. |
| `Logic/` | Legacy gameplay/UI code plus console compatibility adapters that route selected behavior into the framework. | Yes, while protected consumers remain active. |
| `Services/` | Console-host service composition and legacy service wrappers. | Yes, compatibility-only. |
| `Properties/` | Assembly metadata for the console host. | Yes. |
| `Convergence.Tests/` | All automated tests and test fixtures. | Yes. Not shipped as framework runtime code. |
| `Data/Jsons/` | Protected prototype data, historical generated evidence, and clean sample/original packs. | Yes for now; see the content map below. |
| `docs/` | Current, authoritative documentation and active plans. | Yes. |
| `ArchiveDocs/` | Historical plans, generated documents, completed reviews, and archive policy. | Keep as history; do not treat it as current design authority. |
| `JRPG.sln` | Builds the Framework, ConsoleHost, and tests together. | Yes. |
| `JRPG.ConsoleHost.csproj` | Root console-host project definition. Its root location is the main source of visual ambiguity. | Yes until an approved physical-move pass. |
| `bin/`, `obj/` | Generated build output. | Ignore. They are not tracked source. |
| `.git/`, `.codex/`, `.agents/` | Git and local development-tool state. | Ignore for product architecture. |

## What Is Actually The Framework?

For a framework consumer, the boundary is straightforward:

```text
JRPG.Framework/JRPG.Framework.csproj
```

Everything compiled by that project is intended to remain engine-neutral. It owns rules and state transitions, but not console rendering, Godot Nodes, filesystem discovery, or legacy DTOs.

The root console-host project is useful for compatibility and verification, but it is not part of the reusable framework assembly. A Godot project should not reference `JRPG.ConsoleHost.csproj`.

## Content Map

`Data/Jsons/` has not yet been physically split because current loaders, fixtures, project copy rules, and preservation tests refer to that location. Its files fall into three conceptual groups.

### Protected Prototype Content

These files feed or characterize the retained legacy console prototype:

```text
accessories.json
armor.json
boots.json
entity_database.json
fusion_table.json
items.json
questions.json
shop_inventory.json
skills_database.json
status_ailments.json
tartarus.json
weapons.json
```

They are not framework defaults and must not be presented as required game data. They remain protected evidence until their consumers are retired deliberately.

### Historical Generated Evidence

```text
entity_database_v2.json
skills_database_v2.json
```

These are not authoritative clean production content. Existing baseline and audit tests still record them, so moving them belongs to a dedicated content-layout pass.

### Clean Packs

Files whose names begin with the following pack prefixes are consumed through the clean content pipeline:

```text
catalog_surface_sample
clean_battle_demo
shared_effects_demo
skill_system_redesign
status_lifecycle_demo
training_annex_slice
```

`training_annex_slice` is the original framework-first content slice. The other packs are focused reference or demonstration content. None is a mandatory built-in game world.

## Documentation Map

There is one active documentation root: `docs/`.

| Path | Meaning |
|---|---|
| `docs/README.md` | Documentation index and authority rules. |
| `docs/repository-map.md` | This current-layout guide. |
| `docs/framework-state-and-roadmap.md` | Current framework state and forward priorities. |
| `docs/full-parity-capability-plan.md` | Detailed protected-capability roadmap and evidence. |
| `docs/framework-completion/` | Active problem-area backlog. It is a subfolder of active docs, not a separate documentation authority. |
| `docs/subsystems/` | Current subsystem reference material. |
| `ArchiveDocs/Planning/` | Superseded planning history. |
| `ArchiveDocs/TechnicalDocs/` | Historical/generated technical documentation. |
| `ArchiveDocs/Reviews/` | Completed code-review snapshots. Their findings are historical evidence, not the current task queue. |
| `ArchiveDocs/LegacyFramework/` | Archive policy and future approved legacy snapshots. It is intentionally policy-only today. |

## What Can Be Removed Today?

Only generated local output such as `bin/` and `obj/` is unambiguously disposable. No active source family or protected dataset currently qualifies for removal or archival merely because a clean framework equivalent exists.

The ordinary console path and its characterization tests still compile and call the compatibility code. Moving those files into `ArchiveDocs/` now would either break the build or silently reduce protected behavior coverage.

Completed review documents can safely leave the active `docs/` index because they do not compile and are retained under `ArchiveDocs/Reviews/`.

## Recommended Physical Cleanup

The clean end-state remains:

```text
src/JRPG.Framework/
src/JRPG.ConsoleHost/
tests/Convergence.Tests/
content/legacy-prototype/
content/historical-generated/
content/clean-reference/
content/clean-demos/
content/original/
docs/
ArchiveDocs/
```

That move should be performed as a dedicated, behavior-neutral repository-architecture pass with project-file updates, content-source updates, link checking, full builds, and the complete test suite. The current audit does not mix that broad path churn into ongoing framework work.

Until then, use this rule of thumb:

- Work in `JRPG.Framework/` for reusable rules and contracts.
- Work in `Host/` or other root host folders only for console integration and compatibility.
- Work in `Convergence.Tests/` for verification.
- Treat `docs/` as current and `ArchiveDocs/` as historical.
- Do not infer that a root source file is unused just because a clean counterpart exists.
