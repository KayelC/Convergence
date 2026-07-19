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
