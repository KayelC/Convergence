# Convergence Documentation

This is the single active documentation root for the `track-12-recovery` branch. Historical plans, completed reviews, abandoned migration material, and generated technical notes live in [ArchiveDocs](../ArchiveDocs/README.md).

## Start Here

- [Current Repository Map](repository-map.md): what builds, what each top-level folder owns, and what is historical.
- [Phase 8 Product Boundary Plan](phase-8-product-boundary-plan.md): approved clean-project extraction and legacy-archive gate.
- [Framework State And Roadmap](framework-state-and-roadmap.md): current technical state and forward priorities.
- [Full Parity Capability Plan](full-parity-capability-plan.md): capability-by-capability implementation and evidence plan.
- [Project Vision](project-vision.md): framework-first product direction.

## Authority

Use documentation and implementation evidence in this order:

1. Current source code and automated tests define implemented behavior.
2. [Phase 8 Product Boundary Plan](phase-8-product-boundary-plan.md) defines the approved repository and product extraction currently being implemented.
3. [Framework State And Roadmap](framework-state-and-roadmap.md) is the current project map and forward plan.
4. [Full Parity Capability Plan](full-parity-capability-plan.md) remains the pre-extraction capability history until Phase 8 freezes it.
5. `Convergence.Tests/Fixtures/Parity/recovery-baseline.json` is the pre-extraction executable parity ledger.
6. [Framework Completion Problems](framework-completion/README.md) breaks remaining work into owner-reviewable problem areas.
7. [Skill System GDD](skill-system-gdd.md) is normative for approved skill behavior, vocabulary, resistance channels, passives, and inheritance.
8. [Architecture](architecture.md), [Gameplay Systems](gameplay-systems.md), and subsystem chapters explain implemented behavior.
9. [Godot Integration Contract](godot-integration-contract.md) defines the host boundary for Godot-style adapters.
10. [Project Vision](project-vision.md) provides long-term direction, not an implementation contract.

Archived documents are evidence and historical context only. They must not be used to approve implementation work without bringing the relevant decision back into active documentation.

## Active Documents

- [Current Repository Map](repository-map.md)
- [Phase 8 Product Boundary Plan](phase-8-product-boundary-plan.md)
- [Framework State And Roadmap](framework-state-and-roadmap.md)
- [Full Parity Capability Plan](full-parity-capability-plan.md)
- [Framework Completion Problems](framework-completion/README.md)
- [Repository Architecture Inventory (superseded)](repository-architecture-proposal.md)
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

At the Phase 8 starting point, the branch contains two runtime paths:

- `JRPG.ConsoleHost` remains the broad, playable reference implementation and owns legacy data loading, console interaction, clean demonstrations, and current compatibility consumers.
- `JRPG.Framework` is the reusable engine-neutral library and owns clean definitions, rules, runtime state, transitions, diagnostics, and host contracts.

The console host references the framework, never the reverse. Phase 8 replaces that transitional arrangement with a clean Framework and optional DemoHost, then preserves the prototype as non-built history only after the clean gate passes.

The completed O, Q, and T plans are archived under `ArchiveDocs/Planning`. Completed code-review snapshots are under `ArchiveDocs/Reviews`. They are useful history, not current planning authority.

## Documentation Maintenance

- Update active documents when behavior or ownership changes.
- Move superseded plans and completed review snapshots to `ArchiveDocs` instead of deleting them.
- Keep proposals out of active authority until their decisions are approved.
- Do not treat generated class walkthroughs as architectural authority.
- Prefer links to tests and source when describing implemented behavior.
