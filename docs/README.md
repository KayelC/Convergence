<p align="center">
  <img src="assets/Convergence_Logo.png" alt="Convergence Framework logo" width="760">
</p>

# Convergence Documentation

This directory is the active documentation authority for Convergence Framework.

## Start Here

1. [Documentation Design Pattern](documentation-design-pattern.md): how documentation is researched, discussed, diagrammed, reviewed, and promoted.
2. [Policy Family Design Pattern](policy-family-design-pattern.md): how multiple coherent rules models share one neutral authority without hidden defaults.
3. [Project Vision](project-vision.md): what Convergence is and what it deliberately does not own.
4. [Repository Map](repository-map.md): where active product code, tests, content, and examples live.
5. [Architecture](architecture.md): dependency direction and module responsibilities.
6. [Gameplay Systems](gameplay-systems.md): implemented framework capabilities and composition points.
7. [Documentation Coverage](reference/documentation-coverage.md): honest audience-by-capability documentation status.
8. [Stat Modifier Policies](mechanics/stat-modifier-policies.md): confirmed persistent, timed-exclusive, and independently timed modifier rules.
9. [Combat Resolution Policies](developer-guide/combat-resolution-policies.md): bind or replace combat math, hit, critical, charge, instant-defeat, and outcome policies.
10. [Combat Resolution Pipeline](technical/combat-resolution-pipeline.md): staged execution, random boundaries, evidence, atomicity, and persistence.
11. [Turn Economy Policies](developer-guide/turn-economy-policies.md): select,
    bind, present, or replace Action Token and neutral standard actions.
12. [Turn Economy Runtime](technical/turn-economy-runtime.md): phase authority,
    liveness, typed events, and scheduling boundaries.
13. [Battle Knowledge](mechanics/battle-knowledge.md): confirmed
    discovery, Analyze, familiarity, AI scope, and persistence rules.
14. [Battle Knowledge Integration](developer-guide/battle-knowledge.md): using
    typed evidence and immutable knowledge in a Godot or other host.
15. [Encounter Rounds, Phases, And Turns](mechanics/encounter-rounds-phases-and-turns.md):
    player-visible scheduling, lifecycle, cancellation, and outcome rules.
16. [Encounter Orchestration Integration](developer-guide/encounter-orchestration.md):
    composing schedulers, commands, lifecycle, events, and completion.
17. [Encounter Orchestration Runtime](technical/encounter-orchestration-runtime.md):
    state-machine, transaction, reconciliation, and fault invariants.
18. [Party, Rosters, Inventory, Equipment, And Economy](mechanics/party-inventory-and-economy.md):
    player-visible ownership, equipment, shops, stock, currency, and recovery.
19. [Inventory, Equipment, And Economy Integration](developer-guide/inventory-equipment-and-economy.md):
    composing policies, instance IDs, transactions, saves, and Godot adoption.
20. [Inventory, Equipment, And Economy Runtime](technical/inventory-equipment-economy-runtime.md):
    authority graphs, atomic state machines, and save v19 restoration.

## Documentation Audiences

- [Mechanics And Player Rules](mechanics/README.md)
- [Developer Guide](developer-guide/README.md)
- [Technical Documentation](technical/README.md)
- [Design Decisions](decisions/README.md)

## Product And Integration

- [Godot Integration Contract](godot-integration-contract.md)
- [Content Contract](content-contract.md)
- [Content Authoring Validator](content-authoring-validator.md)
- [Ruleset Policy Contracts](ruleset-policy-contracts.md)
- [Public API Contract](public-api-contract.md)
- [Public API Namespaces](public-api-namespaces.md)
- [Framework Source Ownership](reference/framework-source-ownership.md)
- [Release Quality Gate](release-quality-gate.md)
- [Verification Evidence](verification-evidence.md)
- [Licensing](licensing.md)
- [Terminology Boundary](terminology-boundary.md)

## Status And Evidence

- [Roadmap And Capability Status](roadmap/README.md)
- [Review Records](reviews/README.md)

## Authority Rules

- Current source and automated tests define implemented behavior.
- Active documents describe ownership, supported integration, and future priorities.
- [`terminology-boundary.md`](terminology-boundary.md) defines the active vocabulary and executable enforcement boundary.
- Completed plans and superseded terminology are preserved under `ArchiveDocs/LegacyFramework/Documentation`; unfinished work must first be carried into an active roadmap with explicit status.
- Everything under `ArchiveDocs/LegacyFramework` is unsupported historical evidence. Active implementation must not depend on it.

## Maintenance

- Update active docs with behavior or ownership changes.
- Keep host-specific instructions out of framework contracts.
- Record deferred work explicitly in the capability matrix or roadmap.
- Archive superseded plans instead of allowing multiple documents to claim authority.
