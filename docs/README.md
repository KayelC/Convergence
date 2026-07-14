# Convergence Documentation

This directory is the active documentation authority for Convergence Framework.

## Start Here

1. [Project Vision](project-vision.md): what Convergence is and what it deliberately does not own.
2. [Repository Map](repository-map.md): where active product code, tests, content, and examples live.
3. [Architecture](architecture.md): dependency direction and module responsibilities.
4. [Gameplay Systems](gameplay-systems.md): implemented framework capabilities and composition points.
5. [Capability Matrix](framework-capability-matrix.md): executable maturity state and known gaps.
6. [Roadmap](roadmap.md): current forward priorities after the Phase 8 product boundary.
7. [Godot Integration Contract](godot-integration-contract.md): how an engine host consumes the framework.
8. [Public API Namespaces](public-api-namespaces.md): namespace ownership.
9. [Content Contract](content-contract.md): clean content organization and loading authority.
10. [Mechanics And Player Rules](mechanics/README.md): detailed rules, optional modules, and host responsibilities.
11. [Licensing](licensing.md): public noncommercial permissions, commercial licensing, ownership, and contributions.

## Authority Rules

- Current source and automated tests define implemented behavior.
- Active documents describe ownership, supported integration, and future priorities.
- [`phase-8-product-boundary-plan.md`](phase-8-product-boundary-plan.md) is the completed restructuring record.
- Everything under [`ArchiveDocs/LegacyFramework`](../ArchiveDocs/LegacyFramework) is unsupported historical evidence. Active implementation must not depend on it.

## Maintenance

- Update active docs with behavior or ownership changes.
- Keep host-specific instructions out of framework contracts.
- Record deferred work explicitly in the capability matrix or roadmap.
- Archive superseded plans instead of allowing multiple documents to claim authority.
