# Convergence Documentation

This is the single active documentation root for the `track-12-recovery` branch. Historical plans, completed reviews, abandoned migration material, and generated technical notes live in [ArchiveDocs](../ArchiveDocs/README.md).

## Start Here

- [Current Repository Map](repository-map.md): what builds, what each top-level folder owns, and what is historical.
- [Framework State And Roadmap](framework-state-and-roadmap.md): current technical state and forward priorities.
- [Full Parity Capability Plan](full-parity-capability-plan.md): capability-by-capability implementation and evidence plan.
- [Project Vision](project-vision.md): framework-first product direction.

## Authority

Use documentation and implementation evidence in this order:

1. Current source code and automated tests define implemented behavior.
2. [Framework State And Roadmap](framework-state-and-roadmap.md) is the current project map and forward plan.
3. [Full Parity Capability Plan](full-parity-capability-plan.md) is the active capability-by-capability implementation spine.
4. `Convergence.Tests/Fixtures/Parity/recovery-baseline.json` is the executable parity ledger for status, evidence, ownership, and numbered `futurePhase` routing.
5. [Framework Completion Problems](framework-completion/README.md) breaks remaining work into owner-reviewable problem areas.
6. [Repository Architecture Proposal](repository-architecture-proposal.md) describes the proposed physical cleanup. It is not yet an approved source-move operation.
7. [Clean Console Host Demo Plan](clean-console-host-demo-plan.md) defines the proof-harness details for the framework-first console demo.
8. [Skill System GDD](skill-system-gdd.md) is normative for approved skill behavior, vocabulary, resistance channels, passives, and inheritance.
9. [Architecture](architecture.md), [Gameplay Systems](gameplay-systems.md), and subsystem chapters explain the present console prototype and clean framework path.
10. [Godot Integration Contract](godot-integration-contract.md) defines the host boundary for Godot-style adapters.
11. [Project Vision](project-vision.md) provides long-term direction, not an implementation contract.

Archived documents are evidence and historical context only. They must not be used to approve implementation work without bringing the relevant decision back into active documentation.

## Active Documents

- [Current Repository Map](repository-map.md)
- [Framework State And Roadmap](framework-state-and-roadmap.md)
- [Full Parity Capability Plan](full-parity-capability-plan.md)
- [Framework Completion Problems](framework-completion/README.md)
- [Repository Architecture Proposal](repository-architecture-proposal.md)
- [Clean Console Host Demo Plan](clean-console-host-demo-plan.md)
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

Completed phase and framework-wide code reviews are retained under [`ArchiveDocs/Reviews`](../ArchiveDocs/Reviews). They are historical engineering evidence, not the current implementation queue.

## Current Project Shape

The branch intentionally contains two runtime paths:

- `JRPG.ConsoleHost` remains the broad, playable reference implementation and owns legacy data loading, console interaction, clean demonstrations, and current compatibility consumers.
- `JRPG.Framework` is the reusable engine-neutral library and owns clean definitions, rules, runtime state, transitions, diagnostics, and host contracts.

The console host references the framework, never the reverse. The clean path does not yet replace every console subsystem. A legacy subsystem may only be removed after its replacement reaches functional parity, its real consumer has migrated, and the supported host remains demonstrably usable.

The completed O, Q, and T plans are archived under `ArchiveDocs/Planning`. Completed code-review snapshots are under `ArchiveDocs/Reviews`. They are useful history, not current planning authority.

## Documentation Maintenance

- Update active documents when behavior or ownership changes.
- Move superseded plans and completed review snapshots to `ArchiveDocs` instead of deleting them.
- Keep proposals out of active authority until their decisions are approved.
- Do not treat generated class walkthroughs as architectural authority.
- Prefer links to tests and source when describing implemented behavior.
