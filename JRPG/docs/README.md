# Convergence Documentation

This directory contains the active documentation for the `track-12-recovery` branch. Historical plans, abandoned migration material, completed track journals, and generated technical notes live in [ArchiveDocs](../ArchiveDocs/README.md).

## Authority

Use active documents in this order:

1. Current source code and automated tests define implemented behavior.
2. [Framework State And Roadmap](framework-state-and-roadmap.md) is the current project map and forward plan.
3. [Full Parity Capability Plan](full-parity-capability-plan.md) is the active capability-by-capability implementation spine.
4. `Convergence.Tests/Fixtures/Parity/recovery-baseline.json` is the executable parity ledger for status, evidence, ownership, and numbered `futurePhase` routing.
5. [Phase 1-3 Code Review And Forward Direction](phase-1-3-code-review.md) is the code-derived stabilization audit that closed CodeReview-1 through CodeReview-4.
6. [Phase 4 Code Review And Readiness](phase-4-code-review.md) is the code-derived audit for inventory, equipment, economy, shops, and hospital before Phase 5.
7. [Phase 5-6 Code Review And Readiness](phase-5-6-code-review.md) is the code-derived audit for party, stock, negotiation, and recruitment before the next phase.
8. [Phase 7 Code Review And Readiness](phase-7-code-review.md) is the code-derived fusion and Compendium audit and owns the CodeReview-7 follow-up queue.
9. [Framework-Wide Code Review: Third Pass](framework-wide-code-review-2026-07-14-third-pass.md) is the current cross-cutting correction ledger, including the Godot/.NET 8 source-distribution decision.
10. [Framework Completion Problems](framework-completion/README.md) breaks the remaining work into owner-reviewable problem areas.
11. [Repository Architecture Proposal](repository-architecture-proposal.md) maps the current file layout and proposed declutter architecture.
12. [Clean Console Host Demo Plan](clean-console-host-demo-plan.md) defines the proof-harness details for the new framework-first console demo.
13. [Skill System GDD](skill-system-gdd.md) is normative for approved skill behavior, vocabulary, resistance channels, passives, and inheritance.
14. [Architecture](architecture.md), [Gameplay Systems](gameplay-systems.md), and subsystem chapters explain the present console prototype and additive clean framework path.
15. [Godot Integration Contract](godot-integration-contract.md) defines the host boundary for Godot-style adapters.
16. [Project Vision](project-vision.md) provides long-term direction, not an implementation contract.

Archived documents are evidence and historical context only. They must not be used to approve implementation work without bringing the relevant decision back into active documentation.

## Active Documents

- [Phase 1-3 Code Review And Forward Direction](phase-1-3-code-review.md)
- [Phase 4 Code Review And Readiness](phase-4-code-review.md)
- [Phase 5-6 Code Review And Readiness](phase-5-6-code-review.md)
- [Phase 7 Code Review And Readiness](phase-7-code-review.md)
- [Framework-Wide Code Review: Third Pass](framework-wide-code-review-2026-07-14-third-pass.md)
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

## Current Project Shape

The branch intentionally contains two paths:

- `JRPG.ConsoleHost` remains the broad, playable reference implementation and owns legacy data loading, console interaction, and current gameplay consumers.
- `JRPG.Framework` contains the reusable catalog-backed skill, effect, passive, item, battle-runtime, inheritance, and wider content-catalog foundations.

The console host references the framework, never the reverse. The clean path does not yet replace every console subsystem. A legacy subsystem may only be removed after its replacement reaches functional parity, its real consumer has migrated, and the interactive host remains demonstrably usable.

The completed O, Q, T, parity, and production-baseline track documents have been archived under `ArchiveDocs/Planning`. They are useful history, not current planning authority.

## Documentation Maintenance

- Update active documents when behavior or ownership changes.
- Move superseded plans to `ArchiveDocs` instead of deleting them.
- Keep proposals out of active documentation until their decisions are approved.
- Do not treat generated class walkthroughs as architectural authority.
- Prefer links to tests and source when describing implemented behavior.
