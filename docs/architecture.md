# Architecture

## Product Boundary

`Convergence.Framework` is a dependency-free .NET 8 class library. It owns game rules and state transitions but never owns a presentation technology, filesystem, scene graph, save-file encoding, or game-specific content source.

`Convergence.DemoHost` is an optional reference consumer. It demonstrates how a host supplies content, commands, events, randomness, inventory reservations, and save serialization. It is not required by Framework and is not a compatibility layer.

## Core Principles

- Framework first: reusable rules are designed without console or engine assumptions.
- Explicit authority: hosts request transitions; services return immutable results and diagnostics.
- Serializer-neutral APIs: JSON DTOs and converters remain internal to `Convergence.Serialization`.
- Host-owned presentation: display names may be rendered but never determine behavior.
- Injected policy: optional mechanics and game-specific decisions are supplied through policies or registrations.
- Deterministic testing: randomness enters through `IRandomSource`.
- Atomic mutation: assessment, execution tokens, reservations, and rollback boundaries prevent partial state changes.
- Portable persistence: framework snapshots describe runtime state; the host owns the wire format.

## Content Flow

```text
host text source
    -> deserializer
    -> semantic validator + explicit registrations
    -> dependency-aware catalog loader
    -> immutable GameDataCatalog
    -> runtime factories and services
```

Hosts provide all JSON text and diagnostic source names. The framework validates pack versions, paths, records, references, host vocabulary, dependency visibility, and qualification. Runtime services consume catalog definitions, never serializer-owned values.

## Runtime Flow

Runtime actors are identified by `RuntimeInstanceId` and content records by `ContentId`. Actor state, party and rosters, inventory, equipment, wallet, navigation, traversal, Compendium, knowledge, and session state have immutable snapshot boundaries.

Action execution reuses typed targeting, conditions, effects, lifecycle rules, and turn economy. Encounter orchestration accepts host command and event ports. Hosts remain responsible for selecting when an encounter begins and how resulting events are presented.

## Optional Modules

Navigation, dungeon traversal, Action Token, ailments/passives, party and rosters, economy, negotiation, fusion, Compendium, and persistence are independently composable. A developer does not need to register or instantiate a module that their game does not use.

Moon-phase IDs remain nullable vocabulary for games that choose such a mechanic. DemoHost does not require or bind a moon-phase system.

## Distribution

The supported distribution is a Git checkout, submodule, subtree, or copied source tree plus a `ProjectReference` to `src/Convergence.Framework/Convergence.Framework.csproj`. Framework is non-packable until a separate release decision establishes package versioning and compatibility policy.

## Pre-Release Contract Boundary

The active product uses the neutral contracts defined by the [Terminology Boundary](terminology-boundary.md). Content schema version `2` and runtime save contract version `7` are deliberate pre-release breaks with no compatibility aliases. A token-aware architecture test scans active source, tests, content, and documentation so archived vocabulary cannot re-enter the product unnoticed.
