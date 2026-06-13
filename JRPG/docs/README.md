# JRPGPrototype Documentation

This documentation describes the JRPG console prototype as both a gameplay system and a C# codebase. It is written for future developers, designers, and writers who need to understand how player-facing features map to concrete classes, data files, and runtime flows.

## Project At A Glance

- Runtime: .NET 9 console executable with nullable reference types enabled.
- Data: JSON content in `Data/Jsons`, loaded through the static `Database` class with `Newtonsoft.Json`.
- Presentation: console I/O is abstracted behind `IGameIO`, with `ConsoleIO` as the current implementation.
- Architecture: conductors orchestrate workflows, engines/processors own rules, bridges own menus, and messengers/loggers separate logic events from console output.
- Validation baseline: `dotnet build --no-restore` and the xUnit project in `Convergence.Tests` provide the current build and regression checks. Pre-existing nullable and DTO initialization warnings remain.

## Documentation Authority

Documentation is divided by purpose so current implementation notes cannot silently override approved redesign decisions.

| Status | Documents | How to use them |
| --- | --- | --- |
| Normative target | [Skill System GDD](skill-system-gdd.md) | Approved skill behavior, vocabulary, inheritance, resistance, and mutation decisions. |
| Execution plan | [Skill System Redesign Plan](skill-system-redesign-plan.md) | Track order, compatibility strategy, test gates, and removal criteria. It must conform to the GDD. |
| Draft contract | [Content Schema v1 Proposal](content-schema-v1-proposal.md) | Wider content architecture and schema candidates. Only reconciled, explicitly approved sections are implementation-ready. |
| Strategic direction | [Project Vision](project-vision.md), [Host/Core Boundary](host-core-boundary.md), [Bridge Contracts](bridge-contracts.md) | Long-term framework goals and design guidance. |
| Current implementation reference | [Architecture](architecture.md), [Gameplay Systems](gameplay-systems.md), subsystem chapters, and `TechnicalDocs/` | Describes the console prototype as it exists. Treat legacy behavior as migration evidence, not target design. |
| Historical material | [Refactor Roadmap](refactor-roadmap.md) and `../migration_report.md` | Earlier planning and discarded-data migration context. Neither defines the redesign target. |

When documents conflict, use the GDD for skill-system behavior, the reconciled schema contract for data shape, and the redesign plan for implementation order. Current runtime documentation never overrides an approved target decision.

## Recommended Reading Order

1. [Project Vision](project-vision.md) records the long-term direction for Convergence as a reusable RPG systems framework.
2. [Skill System GDD](skill-system-gdd.md) defines the approved target elements, taxonomy, effects, passives, inheritance, resistance, and mutation rules.
3. [Skill System Redesign Plan](skill-system-redesign-plan.md) defines implementation tracks, test gates, compatibility strategy, and cleanup criteria.
4. [Content Schema v1 Proposal](content-schema-v1-proposal.md) is the draft replacement content model. Its status section identifies what remains unresolved.
5. [Architecture](architecture.md) and [Gameplay Systems](gameplay-systems.md) explain the current console implementation.
6. [Host/Core Boundary](host-core-boundary.md) and [Bridge Contracts](bridge-contracts.md) provide broader framework design guidance.
7. [Refactor Roadmap](refactor-roadmap.md) is retained as historical planning context.
8. Current subsystem chapters:
   - [Core](subsystems/core.md)
   - [Data](subsystems/data.md)
   - [Entities](subsystems/entities.md)
   - [Services](subsystems/services.md)
   - [Battle](subsystems/battle.md)
   - [Field](subsystems/field.md)
   - [Fusion](subsystems/fusion.md)

## Runtime Flow

`Program.cs` is the executable entry point. It initializes `IGameIO`, loads JSON content through `Database.LoadData`, creates shared managers, builds a player scenario, and then either jumps into debug/test scenarios or enters the field loop through `FieldConductor`.

Most gameplay flows follow the same shape:

1. A conductor owns the high-level loop.
2. Bridges collect user choices through `IGameIO`.
3. Engines/processors apply rules and mutate state.
4. Messengers publish events.
5. Loggers render those events to the console.

## Documentation Convention

Each subsystem chapter uses the same structure:

- Purpose and player-facing concept.
- Key classes and responsibilities.
- Main runtime flows.
- Important state and invariants.
- JSON or data dependencies.
- Extension points and common modification paths.
- Known caveats observed in the current implementation.

This keeps future iterations predictable: when a module changes, update the concept, code responsibilities, and flow notes together.
