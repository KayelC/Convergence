# Refactor Roadmap: Prototype To Framework

## Purpose

This document is the phased migration plan for Convergence. It is not only a technical debt list for the current console prototype; it is a roadmap for turning JRPGPrototype into an engine-agnostic C# RPG systems framework.

The long-term north star is documented in [Project Vision](project-vision.md): Convergence should become reusable framework core logic, with the current console app becoming a prototype host, reference implementation, and sample adapter.

Use this roadmap to split future work into focused GitHub issues. Refactors should be behavior-preserving unless an issue explicitly changes gameplay.

## Roadmap Principles

- **Engine-agnostic core logic:** framework systems should not require console input, console colors, sleeps, or menu rendering.
- **Console as host/sample:** the current app should become one adapter over the framework, not the center of the architecture.
- **Deterministic state transitions:** battle, field, fusion, negotiation, party, and progression systems should move toward explicit states and transitions.
- **Commands, results, and events:** host apps should send commands and receive results/events instead of calling UI-heavy scripts.
- **Injectable randomness:** random-dependent systems should be reproducible for tests, simulations, and replays.
- **Validated content contracts:** JSON/content should fail early with clear diagnostics when references are invalid.
- **Ruleset modularity:** Press Turn should be polished first, while One More and future Avatar/DDS-style progression should become optional extensions.
- **IP separation:** mechanics should be generic and reusable; franchise-inspired datasets should become examples or testbeds, not framework requirements.

## Executive Risk Summary

The codebase already has useful subsystem boundaries, but it is still shaped like a console prototype. These are the main risks to framework migration.

| Priority | Risk | Why It Matters For The Framework | Representative Areas |
| --- | --- | --- | --- |
| P0 | No automated test project | Framework extraction needs regression protection before files move or APIs change. | `PressTurnEngine`, `CombatMath`, `ElementHelper`, `SkillData`, `PartyManager`, `MoonPhaseSystem` |
| P0 | Nullable-safety debt | Framework APIs need honest contracts; nullable noise hides real integration hazards. | `Data/*Data.cs`, bridge selection methods, messaging classes, fusion strategies |
| P1 | Console-host logic mixed with framework logic | Unity/Godot adapters cannot depend on console menus, sleeps, or direct user prompts. | `Program.cs`, `Services`, bridge classes, conductors |
| P1 | Oversized conductors and bridges | Large workflow/UI classes resist conversion into state machines and command handlers. | `BattleConductor`, `InteractionBridge`, `FieldConductor`, `StatusUIBridge`, `FusionConductor` |
| P2 | Global mutable `Database` access | Static data makes content validation, testing, swapping, and engine integration harder. | `Database`, factories, battle AI/effects, shop, fusion, dungeon |
| P2 | String-driven gameplay rules | Framework users need stable data contracts, not mechanics inferred from display text. | `StatusRegistry`, `BehaviorEngine`, `ActionProcessor`, `FieldServiceEngine`, `FusionCalculator` |
| P3 | Mixed gameplay state model | Public framework APIs need clear identity, ownership, snapshot, and transaction semantics. | `Combatant`, `Persona`, `PartyManager`, `CompendiumRegistry`, fusion strategies |

## Migration Phases

### Phase 0: Safety Net

**Goal:** protect current behavior before structural rewrites.

Initial work:

- Add a test project to the solution.
- Write regression tests around pure or near-pure systems:
  - `PressTurnEngine`
  - `CombatMath`
  - `ElementHelper`
  - `SkillData`
  - `PartyManager`
  - `MoonPhaseSystem`
- Reduce the highest-risk nullable warnings:
  - JSON DTO initialization/defaults.
  - event declarations and message args.
  - methods that intentionally return `null` for cancel/back.
  - APIs that intentionally accept null messages for pauses or special displays.

Success criteria:

- Tests run without interactive console input.
- Press Turn, parsing, party stock, moon phase, and basic formulas have coverage.
- Warning count trends downward without gameplay changes.

### Phase 1: Host/Core Boundary

**Goal:** identify what belongs to the reusable framework and what belongs to the console host.

Framework-core candidates, without moving code yet:

- `Core`
- `Entities`
- pure parts of `Logic/Core`
- `CombatMath`
- `PressTurnEngine`
- data DTOs and parsing helpers

Console-host candidates:

- `Program.cs`
- `Services/ConsoleIO.cs`
- `Services/MenuUI.cs`
- bridge classes that directly render menus or ask for input
- debug scenario selection and console test flows

Initial work:

- Mark/document console dependencies before extracting them.
- Move scenario/debug setup out of `Program.cs` into host-facing setup helpers.
- Prepare for a later split such as `Convergence.Core` plus a console sample project.
- Avoid creating the separate library project until tests and seams exist.

Success criteria:

- It is clear which code is framework logic and which code is console-host behavior.
- Startup/scenario code can change without touching core gameplay rules.
- Future project split has a mapped boundary.

### Phase 2: State Machine Foundations

**Goal:** move from script-like workflows toward explicit stateful systems.

Initial work:

- Define state/command/result shapes for major systems before rewriting conductors:
  - battle phase and actor turn flow
  - field/exploration navigation
  - fusion ritual flow
  - negotiation/recruitment flow
  - party and stock transactions
- Keep existing conductors operational while extracting small pure decision points.
- Prefer behavior-preserving wrappers before replacing full workflows.

Success criteria:

- Hosts can eventually drive systems by sending commands and observing results.
- Conductors begin to read like adapters over state transitions.
- State machine work can be tested without console UI.

### Phase 3: Data Contracts And Validation

**Goal:** make content-driven behavior explicit, validated, and swappable.

Initial work:

- Add normalized ID helpers or lightweight value objects for:
  - entity/persona/demon source IDs
  - skill IDs or canonical skill names
  - item IDs
  - equipment IDs
- Add validation after JSON loading:
  - missing referenced skills
  - invalid enemy IDs in dungeon pools
  - invalid equipment/shop IDs
  - fusion recipes pointing to unknown races or entities
  - invalid ailment restrictions or cure keywords
- Replace fragile string inference with typed metadata where practical.
- Introduce read-only data access seams before replacing static `Database` globally.

Success criteria:

- Bad content fails early with useful diagnostics.
- Framework users can swap datasets with confidence.
- Tests can use isolated data registries or fake repositories.

### Phase 4: UI And Engine Adapter Separation

**Goal:** make console UI one adapter among many.

Initial work:

- Shrink bridge classes into console-specific adapters.
- Extract repeated selection/menu patterns.
- Standardize cancel/back contracts:
  - nullable return means canceled/back.
  - non-nullable return should never return null.
  - complex flows should use explicit selection result objects.
- Replace direct waits/output in framework-facing logic with events/results.
- Prepare event streams usable by Unity/Godot UI, animation, logging, and audio layers.

Success criteria:

- Framework systems do not need `ConsoleIO` or `MenuUI`.
- Console behavior remains available as a sample host.
- Future engine adapters can render and animate framework events independently.

### Phase 5: Ruleset Modularity

**Goal:** make battle/progression variants configurable rather than hard-coded.

Initial work:

- Treat Press Turn as the first polished ruleset.
- Isolate turn-economy rules so One More can be introduced later without replacing the whole battle system.
- Keep Persona Users and Wild Cards compatible with the framework model.
- Reserve Avatar/DDS progression for a later extension after the entity/progression model is clearer.

Success criteria:

- Press Turn is complete, tested, and documented as v1 behavior.
- One More can be planned as an optional ruleset.
- Future Avatar/DDS mechanics have extension points instead of ad hoc branches.

### Phase 6: IP And Example Content Separation

**Goal:** separate reusable mechanics from franchise-inspired test content.

Initial work:

- Identify protected naming and branding in data, docs, and UI text.
- Keep current datasets as examples/testbeds only where appropriate.
- Plan generic naming for public framework mechanics.
- Make original or sample content packs optional.

Success criteria:

- Framework operation does not require protected IP content.
- Developers can bring their own original content.
- The project remains useful as portfolio work and as a foundation for future original IP.

## Issue Skeleton

Use this skeleton when converting roadmap work into GitHub issues.

```markdown
## Problem

What is hard to maintain, risky, unclear, console-bound, or blocking framework reuse?

## Why It Matters

What future framework, adapter, ruleset, or content work does this enable?

## Affected Areas

Files, systems, or gameplay flows likely touched.

## Framework Impact

How this changes reusable core logic, public APIs, or state/contracts.

## Host/App Impact

How this affects the current console prototype host.

## Adapter Impact

How this helps or affects future Unity/Godot/custom frontend adapters.

## Public API Risk

Low, medium, or high. Explain compatibility concerns.

## Suggested Refactor

The intended behavior-preserving change.

## Behavior Must Stay The Same

Specific gameplay/menu/data behavior that must not change.

## Suggested Tests

Unit, regression, scenario, or manual tests that should prove safety.

## Priority

P0, P1, P2, or P3.

## Dependencies / Should Happen Before

Other roadmap items that should land first.

## Notes For GitHub Issues

Implementation cautions, known edge cases, or follow-up ideas.
```

## Initial Backlog

### P0: Create Tests For Pure Rules

**Problem:** core mechanics are not protected by automated tests.

**Why It Matters:** framework extraction and state machine work need confidence.

**Affected Areas:** solution/project setup, `PressTurnEngine`, `CombatMath`, `ElementHelper`, `SkillData`, `PartyManager`, `MoonPhaseSystem`.

**Framework Impact:** creates a regression baseline for future public framework APIs.

**Host/App Impact:** should not change console behavior.

**Adapter Impact:** tests prove rules independent of UI adapters.

**Public API Risk:** low.

**Suggested Refactor:** add a test project and write deterministic tests for pure rules.

**Behavior Must Stay The Same:** current formulas, turn icon rules, parser behavior, stock limits, and moon phase cycling.

**Suggested Tests:** Press Turn outcome matrix, element/category parsing, skill cost parsing, stock capacity thresholds, moon phase wraparound.

**Priority:** P0.

**Dependencies / Should Happen Before:** should happen before structural rewrites.

**Notes For GitHub Issues:** avoid tests that need live console input.

### P0: Document And Mark Console Dependencies

**Problem:** framework candidates and console-host code are not clearly separated.

**Why It Matters:** future engine adapters need to know what must be removed from core logic.

**Affected Areas:** `Program.cs`, `Services`, bridge classes, conductors, loggers.

**Framework Impact:** identifies code that cannot remain in core framework assemblies.

**Host/App Impact:** preserves the current console app as a sample host.

**Adapter Impact:** creates the first adapter boundary map for Unity/Godot.

**Public API Risk:** low.

**Suggested Refactor:** document and annotate host-only responsibilities before moving files.

**Behavior Must Stay The Same:** all current menus and debug scenarios.

**Suggested Tests:** documentation review, build verification.

**Priority:** P0.

**Dependencies / Should Happen Before:** before splitting projects or moving bridges.

**Notes For GitHub Issues:** this can be documentation plus small comments, not a structural rewrite.

### P1: Extract Startup And Scenario Host Responsibilities

**Problem:** `Program.cs` mixes bootstrap, scenario selection, debug tools, Monte Carlo simulation, and gameplay launch.

**Why It Matters:** framework initialization and sample-host scenario setup should be separate concepts.

**Affected Areas:** `Program.cs`, scenario setup code, debug/test scenario helpers.

**Framework Impact:** clarifies what a host must configure before using framework systems.

**Host/App Impact:** console prompts and scenarios should remain available.

**Adapter Impact:** future engine samples can implement their own host setup.

**Public API Risk:** low to medium.

**Suggested Refactor:** extract scenario configuration and debug/test scenario execution into host-facing helper types.

**Behavior Must Stay The Same:** numbered scenarios and debug paths.

**Suggested Tests:** build plus manual scenario launch smoke test; later fake-IO scenario tests.

**Priority:** P1.

**Dependencies / Should Happen Before:** test project preferred.

**Notes For GitHub Issues:** keep prompts identical during the first extraction.

### P1: Standardize Command, Result, And Cancel Contracts

**Problem:** many workflows use implicit nulls, string choices, and UI returns as control flow.

**Why It Matters:** framework systems need explicit commands/results for state machines and adapters.

**Affected Areas:** `InteractionBridge`, field bridges, `CathedralUIBridge`, conductors.

**Framework Impact:** begins replacing UI-shaped control flow with framework-friendly contracts.

**Host/App Impact:** console bridge behavior should remain the same.

**Adapter Impact:** future adapters can translate engine input into commands instead of mimicking console menus.

**Public API Risk:** medium.

**Suggested Refactor:** introduce small result types for selection/cancel where useful, and make nullable returns explicit elsewhere.

**Behavior Must Stay The Same:** cancel/back behavior, menu order, selected action semantics.

**Suggested Tests:** fake `IGameIO` bridge tests where practical; compile-time nullable cleanup.

**Priority:** P1.

**Dependencies / Should Happen Before:** tests and nullable cleanup.

**Notes For GitHub Issues:** avoid changing labels and contracts in the same issue when possible.

### P2: Begin Data Access Abstraction

**Problem:** many systems use static `Database` directly.

**Why It Matters:** framework users need swappable content sources and tests need isolated data.

**Affected Areas:** factories, battle AI/effects, shops, dungeon, fusion, field inventory/status bridges.

**Framework Impact:** introduces data seams for future `Convergence.Core`.

**Host/App Impact:** console app can still load the existing JSON files.

**Adapter Impact:** engine hosts can eventually provide content repositories from assets/resources.

**Public API Risk:** medium.

**Suggested Refactor:** add read-only repository interfaces or wrappers gradually, subsystem by subsystem.

**Behavior Must Stay The Same:** current JSON loading and lookup behavior.

**Suggested Tests:** data-loading smoke tests and isolated fake repository tests.

**Priority:** P2.

**Dependencies / Should Happen Before:** tests and content validation.

**Notes For GitHub Issues:** avoid a whole-codebase rewrite.

### P2: Make Randomness Injectable

**Problem:** random-dependent behavior is hard-coded in multiple systems.

**Why It Matters:** deterministic tests, replays, simulations, and adapters need controlled randomness.

**Affected Areas:** `CombatMath`, `BehaviorEngine`, `StatusRegistry`, `DungeonManager`, `FusionCalculator`, `GrowthProcessor`.

**Framework Impact:** improves testability and reproducibility.

**Host/App Impact:** default random behavior should remain unchanged.

**Adapter Impact:** engine hosts can seed or control randomness for debugging and replay.

**Public API Risk:** medium.

**Suggested Refactor:** introduce an RNG abstraction or injectable `Random` wrapper with default behavior.

**Behavior Must Stay The Same:** probability rules and default randomness.

**Suggested Tests:** deterministic tests for initiative, ailments, dungeon encounters, fusion accidents, and skill mutation.

**Priority:** P2.

**Dependencies / Should Happen Before:** test project.

**Notes For GitHub Issues:** migrate one subsystem at a time.

### P3: Split Executable And Framework Projects Later

**Problem:** the current project is a single executable.

**Why It Matters:** a real framework should be consumable independently from the console sample.

**Affected Areas:** solution structure, project files, namespaces, content copying, references.

**Framework Impact:** creates the future reusable `Convergence.Core` assembly.

**Host/App Impact:** console project becomes a sample app referencing the framework.

**Adapter Impact:** Unity/Godot adapters can reference the same core project.

**Public API Risk:** high.

**Suggested Refactor:** delay the split until tests, host/core boundary mapping, and data seams exist.

**Behavior Must Stay The Same:** console sample should still run current scenarios after the split.

**Suggested Tests:** full build, test suite, scenario smoke tests, JSON content load verification.

**Priority:** P3.

**Dependencies / Should Happen Before:** Phase 0, Phase 1, and early Phase 3.

**Notes For GitHub Issues:** do not start here; project splitting is a payoff step, not the first move.

## First Engineering Iteration

The first implementation iteration after this roadmap should be intentionally modest:

1. Add a test project.
2. Cover pure rules and parsers.
3. Document host/core boundaries in code or docs.
4. Avoid moving production code into a new framework project yet.

This gives Convergence a stable foundation before larger architectural changes.

## Progress Notes

- The current solution has one executable project targeting `net9.0`.
- The current docs and roadmap assume the console app remains the working prototype host for now.
- Phase 0 has started with a first xUnit regression test project focused on pure framework-candidate systems.
- Phase 1 boundary mapping has started in [Host/Core Boundary](host-core-boundary.md).
- Bridge contract planning has started in [Bridge Contracts](bridge-contracts.md), with fusion identified as the first migration target.
- The first fusion bridge contract migration replaced display-string menu commands and integer ritual confirmation outcomes with typed result states.
- Fusion participant selection now uses typed selected/canceled/unavailable result states.
- The first Phase 1 host cleanup has extracted startup, scenario setup, and debug runners out of `Program.cs` into `Host`.
- `dotnet build --no-restore --no-incremental` has previously surfaced nullable warning debt; quick non-restore builds may show fewer warnings because outputs are already current.
- Commands that restore packages may attempt to read user-level NuGet config depending on environment permissions.
- Update this roadmap whenever a refactor issue lands, is split, or changes direction.
