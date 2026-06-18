# Convergence Documentation

This directory contains the active documentation for the Track 12 recovery branch and its framework-parity migration. Historical plans, abandoned migration material, and generated technical notes live in [ArchiveDocs](../ArchiveDocs/README.md).

## Authority

Use active documents in this order:

1. [Production Baseline](production-baseline.md) defines the current recovery boundary and migration safety rules.
2. [Framework Parity Migration Plan](framework-parity-migration-plan.md) defines the work and gates required to port all existing systems without feature loss.
3. [Track T Framework Completion Roadmap](t-track-plan.md) defines the active build-forward lane after the archive gate.
4. [Track O Console Host Migration Plan](o-track-plan.md) remains the completed split plan for console-host presentation migration.
5. [Godot Integration Contract](godot-integration-contract.md) defines the Track P host boundary for Godot-style adapters.
6. [Skill System GDD](skill-system-gdd.md) is normative for approved skill behavior, vocabulary, resistance channels, passives, and inheritance.
7. Current source code and automated tests define implemented behavior.
8. [Architecture](architecture.md), [Gameplay Systems](gameplay-systems.md), and subsystem chapters explain the present console prototype and additive clean framework path.
9. [Project Vision](project-vision.md) provides long-term direction, not an implementation contract.

Archived documents are evidence and historical context only. They must not be used to approve implementation work without bringing the relevant decision back into active documentation.

## Active Documents

- [Production Baseline](production-baseline.md)
- [Framework Parity Migration Plan](framework-parity-migration-plan.md)
- [Track T Framework Completion Roadmap](t-track-plan.md)
- [Track O Console Host Migration Plan](o-track-plan.md)
- [Godot Integration Contract](godot-integration-contract.md)
- [Project Vision](project-vision.md)
- [Skill System GDD](skill-system-gdd.md)
- [Architecture](architecture.md)
- [Gameplay Systems](gameplay-systems.md)
- [Core](subsystems/core.md)
- [Data](subsystems/data.md)
- [Entities](subsystems/entities.md)
- [Services](subsystems/services.md)
- [Battle](subsystems/battle.md)
- [Field](subsystems/field.md)
- [Fusion](subsystems/fusion.md)

## Current Project Shape

The branch intentionally contains two paths:

- `JRPG.ConsoleHost` remains the broad, playable reference implementation and owns legacy data loading, console interaction, and current gameplay consumers.
- `JRPG.Framework` contains the reusable catalog-backed skill, effect, passive, item, battle-runtime, inheritance, and wider content-catalog foundations.

The console host references the framework, never the reverse. The clean path does not yet replace every console subsystem. A legacy subsystem may only be removed after its replacement reaches functional parity, its real consumer has migrated, and the interactive host remains demonstrably usable.

## Documentation Maintenance

- Update active documents when behavior or ownership changes.
- Move superseded plans to `ArchiveDocs` instead of deleting them.
- Keep proposals out of active documentation until their decisions are approved.
- Do not treat generated class walkthroughs as architectural authority.
- Prefer links to tests and source when describing implemented behavior.
