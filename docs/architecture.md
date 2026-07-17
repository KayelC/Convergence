# Architecture

## Product Boundary

`Convergence.Framework` is a dependency-free .NET 8 class library. It owns game rules and state transitions but never owns a presentation technology, filesystem, scene graph, save-file encoding, or game-specific content source.

`Convergence.DemoHost` is an optional reference consumer. It demonstrates how a host supplies content, commands, events, randomness, inventory reservations, and save serialization. It is not required by Framework and is not a compatibility layer.

`Convergence.GodotHost` is a separate Godot 4.7.1 .NET reference consumer. It references Framework source, reads canonical example content through `res://`, maps runtime IDs to Nodes, and keeps engine APIs and JSON save encoding outside the reusable assembly.

Session restore is aggregate and framework-owned. Hosts decode their save envelope, then supply the snapshot, catalog, actor factory, actor-profile resolver, validator, and any real version-migration steps. Framework restores dependencies in order and returns no live session until every actor and aggregate invariant succeeds; scene/node reconstruction and host-context application happen afterward.

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

Host-supplied JSON is checked against the strict Draft 2020-12 contracts in
`schemas/content/v3` before Framework deserialization and semantic catalog
validation. JSON Schema owns document shape; Framework validation owns graph,
dependency-visibility, registration, and host-capability rules. This keeps the
reusable assembly free of schema-evaluation and filesystem dependencies while
giving authoring tools an independent contract.

```text
host text source
    -> deserializer
    -> semantic validator + explicit registrations
    -> dependency-aware catalog loader
    -> immutable GameDataCatalog
    -> runtime factories and services
```

Hosts provide all JSON text and diagnostic source names. The framework validates pack versions, paths, records, references, host vocabulary, dependency visibility, and qualification. Runtime services consume catalog definitions, never serializer-owned values.

Ruleset records are bound through a host-supplied
`RuntimeRulesetPolicyFactoryRegistry`. Each category has its own typed factory
interface, so an authored policy cannot be resolved as an unrelated service.
The supplied standard registry is opt-in; unknown policies fail with typed
diagnostics and no hidden standard fallback. See
[Ruleset Policy Contracts](ruleset-policy-contracts.md).

## Runtime Flow

Runtime actors are identified by `RuntimeInstanceId` and content records by
`ContentId`. Actor state, party and rosters, inventory, equipment, wallet,
navigation, traversal, Compendium, knowledge, and session state have immutable
snapshot boundaries.

Actor authority is split deliberately. Individual actor state owns identity,
progression, resources, equipment, skills, status, affiliation, and encounter
presence. `RuntimePartyRosterSnapshot` exclusively owns active/reserve
placement, Hosted Entity ownership, Companion ownership, and the Active Hosted
Entity selection.

The standard Vessel model atomically composes core stats, defenses, active
skills, and passives from the selected Hosted Entity while retaining the
Vessel's identity, progression, resources, equipment, status, affiliation, and
presence. Source progression, pending skill decisions, dependent Vessel
recomposition, and aggregate restoration share that authority model. See
[Actors And Runtime State](developer-guide/actors-and-runtime-state.md) and
[Runtime Actor State And Restoration](technical/runtime-actor-state-and-restoration.md).

Action execution reuses typed targeting, conditions, effects, lifecycle rules, and turn economy. Encounter orchestration accepts host command and event ports. Every encounter event carries a kind-specific immutable payload for initiative, rounds, teams, actors, commands, effects, turn economy, deployment, faults, or outcomes. Optional debug text is diagnostic only; hosts localize and present the typed payload rather than parsing prose. Hosts remain responsible for selecting when an encounter begins and how resulting events are presented.

## Optional Modules

Navigation, dungeon traversal, Action Token, ailments/passives, party and rosters, economy, negotiation, fusion, Compendium, and persistence are independently composable. A developer does not need to register or instantiate a module that their game does not use.

Moon-phase IDs remain nullable vocabulary for games that choose such a mechanic. The supplied ruleset registry has no moon-phase factory, and DemoHost does not require or bind a moon-phase system.

## Distribution

The supported distribution is a Git checkout, submodule, subtree, or copied source tree plus a `ProjectReference` to `src/Convergence.Framework/Convergence.Framework.csproj`. Framework is non-packable until a separate release decision establishes package versioning and compatibility policy.

## Pre-Release Contract Boundary

The active product uses the neutral contracts defined by the [Terminology Boundary](terminology-boundary.md). Content schema version `3` and runtime save contract version `9` are deliberate pre-release breaks with no compatibility aliases. Save v9 persists pending skill choices, derives Active Hosted Entity restoration from the canonical party roster, and derives roster capacity from the saved owner actor instead of duplicating an owner level in the roster snapshot. Save v8 and earlier require an explicit host-supplied migration step. A token-aware architecture test scans active source, tests, content, and documentation so archived vocabulary cannot re-enter the product unnoticed.

Assembly version `0.1.0` is guarded by a checked-in textual API baseline. The
[Public API Contract](public-api-contract.md) identifies the supported
composition entry points and the pre-release compatibility policy. Build-only
API analyzers and compiler tooling are private development dependencies; the
compiled framework retains no runtime package dependency.

Framework is marked trimming-aware and builds with the pinned .NET 8 ILLink analyzer. The [Release Quality Gate](release-quality-gate.md) combines locked dependency auditing, API and documentation checks, schema/catalog validation, coverage thresholds, DemoHost modes, and a checksum-verified Godot headless run.
