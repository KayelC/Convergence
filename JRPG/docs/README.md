# Convergence Documentation

This directory contains the active documentation for the Track 12 recovery branch and its framework-parity migration. Historical plans, abandoned migration material, and generated technical notes live in [ArchiveDocs](../ArchiveDocs/README.md).

## Authority

Use active documents in this order:

1. [Production Baseline](production-baseline.md) defines the current recovery boundary and migration safety rules.
2. [Framework Parity Migration Plan](framework-parity-migration-plan.md) defines the work and gates required to port all existing systems without feature loss.
3. [Track O Console Host Migration Plan](o-track-plan.md) defines the active O subtracks now that console-host migration is split into smaller passes.
4. [Skill System GDD](skill-system-gdd.md) is normative for approved skill behavior, vocabulary, resistance channels, passives, and inheritance.
5. Current source code and automated tests define implemented behavior.
6. [Architecture](architecture.md), [Gameplay Systems](gameplay-systems.md), and subsystem chapters explain the present console prototype and additive clean framework path.
7. [Project Vision](project-vision.md) provides long-term direction, not an implementation contract.

Archived documents are evidence and historical context only. They must not be used to approve implementation work without bringing the relevant decision back into active documentation.

## Active Documents

- [Production Baseline](production-baseline.md)
- [Framework Parity Migration Plan](framework-parity-migration-plan.md)
- [Track O Console Host Migration Plan](o-track-plan.md)
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
