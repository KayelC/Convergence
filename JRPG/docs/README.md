# JRPGPrototype Documentation

This documentation describes the JRPG console prototype as both a gameplay system and a C# codebase. It is written for future developers, designers, and writers who need to understand how player-facing features map to concrete classes, data files, and runtime flows.

## Project At A Glance

- Runtime: .NET 9 console executable with nullable reference types enabled.
- Data: JSON content in `Data/Jsons`, loaded through the static `Database` class with `Newtonsoft.Json`.
- Presentation: console I/O is abstracted behind `IGameIO`, with `ConsoleIO` as the current implementation.
- Architecture: conductors orchestrate workflows, engines/processors own rules, bridges own menus, and messengers/loggers separate logic events from console output.
- Validation baseline: `dotnet build --no-restore` succeeds, currently with nullable and DTO initialization warnings. There is no separate test project.

## Recommended Reading Order

1. [Architecture](architecture.md) explains the codebase shape and recurring patterns.
2. [Gameplay Systems](gameplay-systems.md) explains the player-facing systems in implementation terms.
3. [Project Vision](project-vision.md) records the long-term direction for Convergence as a reusable RPG systems framework.
4. [Host/Core Boundary](host-core-boundary.md) maps current code into future framework, host, adapter, and transitional responsibilities.
5. [Bridge Contracts](bridge-contracts.md) defines the shared command/result/cancel pattern for future bridge and adapter refactors.
6. [Refactor Roadmap](refactor-roadmap.md) lays out the migration path from console prototype to reusable framework.
7. [Skill System GDD](skill-system-gdd.md) defines the target elements, skill taxonomy, effects, passives, and inheritance groups.
8. [Skill System Redesign Plan](skill-system-redesign-plan.md) defines the implementation tracks, test gates, compatibility strategy, and cleanup criteria.
9. [Content Schema v1 Proposal](content-schema-v1-proposal.md) proposes the replacement content model to approve before further data migration.
10. Subsystem chapters:
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
