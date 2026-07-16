# Convergence Documentation

This directory is the active documentation authority for Convergence Framework.

## Start Here

1. [Project Vision](project-vision.md): what Convergence is and what it deliberately does not own.
2. [Repository Map](repository-map.md): where active product code, tests, content, and examples live.
3. [Architecture](architecture.md): dependency direction and module responsibilities.
4. [Gameplay Systems](gameplay-systems.md): implemented framework capabilities and composition points.
5. [Capability Matrix](framework-capability-matrix.md): executable maturity state and known gaps.
6. [Production-Readiness Record](production-readiness-roadmap.md): verified completion state for every carried-forward audit finding.
7. [Product Roadmap](roadmap.md): forward priorities after the production-readiness gate.
8. [Godot Integration Contract](godot-integration-contract.md): how an engine host consumes the framework.
9. [Release Quality Gate](release-quality-gate.md): locked restore, API/schema checks, coverage, hosts, trimming, and security verification.
10. [Public API Contract](public-api-contract.md): supported composition surface and `0.1` compatibility policy.
11. [Public API Namespaces](public-api-namespaces.md): namespace ownership.
12. [Framework Source Ownership](reference/framework-source-ownership.md): tested file, namespace, and public-surface ownership.
13. [Content Contract](content-contract.md): clean content organization and loading authority.
14. [Content Authoring Validator](content-authoring-validator.md): complete schema-to-catalog validation from the command line.
15. [Ruleset Policy Contracts](ruleset-policy-contracts.md): typed factory composition and supplied parameter contracts.
16. [Mechanics And Player Rules](mechanics/README.md): detailed rules, optional modules, and host responsibilities.
17. [Licensing](licensing.md): public noncommercial permissions, commercial licensing, ownership, and contributions.
18. [Terminology Boundary](terminology-boundary.md): the active Action Token, Vessel, roster, schema-v3, save-v7, and vocabulary-enforcement contract.
19. [Production-Readiness Consolidated Review](convergence-production-readiness-consolidated-review-2026-07-16.md): source-based checkpoint review, correction evidence, release-gate results, residual constraints, and final `0.1.0` verdict.
20. [Pre-Roadmap Code Review](convergence-framework-code-review-2026-07-15.md): historical source review and correction log that led into the completed production-readiness roadmap.
21. [Current External Review Reconciliation](Convergence_Current_Version_Code_Review.md): independently supplied observations checked against the live source, accepted corrections, challenged claims, and the current corrective sequence.

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
