# Host/Core Boundary Map

> **Archived:** This extraction map describes an earlier planning stage and is not a current migration contract.

> **Status: Strategic planning reference.** This boundary map guides future extraction but does not override subsystem GDDs or approved schema contracts.

## Purpose

This document maps the current console prototype into future Convergence framework responsibilities. It is a planning aid for the eventual split between reusable core logic and host-specific presentation.

The goal is not to move code yet. The goal is to classify the current codebase so future refactors can be made deliberately.

## Boundary Definitions

- **Framework Core:** reusable rules, state, data contracts, calculations, and transactions that should run without console input.
- **Console Host:** current executable startup, debug scenario setup, direct console rendering, blocking input, delays, and sample-app flow.
- **Adapter Boundary:** translation layer between framework events/commands and a host UI, such as console, Unity, Godot, or custom tooling.
- **Transitional:** code that contains useful framework logic but is currently shaped around console workflows or direct user interaction.

## Folder Classification

| Area | Classification | Why |
| --- | --- | --- |
| `Core` | Framework Core | Defines shared enums, result types, and parsing helpers used by all systems. |
| `Data` | Mostly Framework Core | DTOs and content contracts belong in core, but static `Database` loading needs a future repository/validation boundary. |
| `Entities` | Framework Core | `Combatant`, `Persona`, and entity processors represent reusable gameplay state and rules. |
| `Logic/Core` | Framework Core | Party, inventory, economy, and moon phase rules are reusable service/state systems. |
| `Logic/Battle/CombatMath.cs` | Framework Core | Pure battle calculations should be reusable and testable. |
| `Logic/Battle/Engines` | Mostly Framework Core | Press Turn, status, behavior, negotiation, and knowledge systems are reusable but need RNG/data seams. |
| `Logic/Battle/Effects` | Framework Core | Effect strategies are rule implementations, not presentation. |
| `Logic/Battle/Messaging` | Adapter Boundary | Message/event shape is useful, but console-oriented delay/color concerns should be separated later. |
| `Logic/Battle/Bridges` | Console Host / Adapter | `InteractionBridge` is currently console-menu presentation and should not live in framework core. |
| `Logic/Battle/BattleConductor.cs` | Transitional | Contains core encounter flow, but currently drives UI and console-shaped decisions. |
| `Logic/Field/Engines` | Mostly Framework Core | Shop, service, and exploration rules are reusable but still depend on field UI and static data in places. |
| `Logic/Field/Dungeon` | Framework Core | Dungeon state and floor processing are reusable rule/state logic. |
| `Logic/Field/Messaging` | Adapter Boundary | Good event concept, but host presentation details should remain outside core. |
| `Logic/Field/Bridges` | Console Host / Adapter | Menu rendering and status displays are host-specific. |
| `Logic/Field/FieldConductor.cs` | Transitional | Coordinates field state but is still shaped as a blocking console menu loop. |
| `Logic/Field/State` | Adapter Boundary | `FieldUIState` is presentation-state oriented and should stay with host/adapters unless generalized. |
| `Logic/Fusion` | Mixed | Calculator, mutator, context, strategies, and compendium are core; conductor and bridge are transitional/host-facing. |
| `Logic/Fusion/Messaging` | Adapter Boundary | Event stream pattern is reusable, but output details should be host-controlled later. |
| `Services` | Console Host / Adapter | `IGameIO` is the current console abstraction; `ConsoleIO` and `MenuUI` are host-specific. |
| `Program.cs` | Console Host | Thin executable entry point for the sample app. Startup, scenario selection, debug routes, and executable flow now live in `Host`. |
| `Host` | Console Host | Owns the current console app startup sequence, prototype scenario setup, and debug/test scenario runners. |

## Core Candidates

These areas are the best first candidates for future `Convergence.Core` extraction once tests and seams are stronger:

- `Core`
- `Entities`
- `Entities/Components`
- data DTOs in `Data`
- pure managers in `Logic/Core`
- `Logic/Battle/CombatMath.cs`
- `Logic/Battle/Engines/PressTurnEngine.cs`
- `Logic/Battle/Effects`
- `Logic/Field/Dungeon`
- `Logic/Fusion/Strategies`
- `Logic/Fusion/FusionCalculator.cs`
- `Logic/Fusion/FusionMutator.cs`
- `Logic/Fusion/FusionContext.cs`
- `Logic/Fusion/CompendiumRegistry.cs`

These should eventually avoid:

- direct console calls
- blocking input
- sleeps/delays
- display colors
- direct static global data where a repository seam would be cleaner
- non-injectable randomness where deterministic tests matter

## Console Host Candidates

These areas should remain with the current console sample app or a future `Convergence.ConsoleSample` project:

- `Program.cs`
- `Host/ConsoleGameHost.cs`
- `Host/ScenarioFactory.cs`
- `Host/DebugScenarioRunner.cs`
- scenario/debug setup and Monte Carlo simulation entrypoints
- `Services/ConsoleIO.cs`
- `Services/MenuUI.cs`
- console bridge classes that render menus or status text
- logger classes if they remain responsible for colors, waits, and console-specific output

The console host should eventually:

- create/load content repositories
- configure player/scenario/sample state
- translate keyboard/menu choices into framework commands
- render framework events/results to the console
- remain useful as a demo and regression playground

## Adapter Boundary Candidates

These areas are likely to become adapter-facing contracts:

- battle, field, and fusion message args
- conductor outputs after they are converted into explicit results/events
- `IGameIO`, or a successor abstraction that separates input, output, timing, and rendering
- selection/cancel result objects for menus and targets
- future command/result types for battle, field, party, fusion, and negotiation flows

For Unity/Godot, adapters should own:

- input devices
- menus and UI widgets
- animation timing
- scene transitions
- audio and visual effects
- save/load UX

Convergence core should own:

- legal commands
- state mutation
- rules validation
- deterministic outcomes
- event/result descriptions for hosts to render

## Transitional Hotspots

These files should be refactored carefully because they mix framework behavior with host flow:

- `Program.cs`
  - Mixes bootstrap, scenario setup, debug test routes, and gameplay entry.
  - First likely refactor: extract scenario setup into host-only helper types.
- `BattleConductor.cs`
  - Owns real battle flow but currently calls UI and interprets player-facing choices.
  - Future direction: battle state machine plus console adapter.
- `FieldConductor.cs`
  - Owns field/dungeon/city flow but runs as a blocking menu loop.
  - Future direction: field state machine plus host commands.
- `FusionConductor.cs`
  - Owns meaningful ritual flow but is closely tied to Cathedral UI steps.
  - Future direction: fusion workflow service with staged result/confirmation commands.
- Bridge classes
  - Contain useful display knowledge but should not own rule decisions long-term.

## First Refactor After This Map

The safest next production refactor is:

**Extract startup and scenario setup out of `Program.cs` into console-host helper classes.**

Status: completed as the first host cleanup refactor. `Program.cs` now delegates to `Host/ConsoleGameHost.cs`, while scenario setup and debug flows live in `Host/ScenarioFactory.cs` and `Host/DebugScenarioRunner.cs`.

Why this next:

- It is host-side, not core-side.
- It reduces risk before touching battle/field/fusion conductors.
- It clarifies what a host must configure before entering framework systems.
- It keeps the current console app working while moving toward the future sample-host shape.

Created files:

- `Host/ConsoleGameHost.cs`
- `Host/ScenarioFactory.cs`
- `Host/DebugScenarioRunner.cs`
- `Host/ScenarioSetupResult.cs`

`Host/ScenarioDefinition.cs` remains a possible future refinement if scenario metadata becomes richer than the current numbered prototype menu.

## Acceptance Checklist For Future Boundaries

When deciding whether code belongs in core, ask:

- Can it run without console input?
- Can it be tested without a live terminal?
- Does it mutate gameplay state or only render it?
- Would Unity/Godot need this behavior unchanged?
- Does it depend on timing, colors, cursor state, or menu text?
- Does it need deterministic RNG for reliable tests?
- Does it require static global content, or could it accept a repository/service?

If the answer points to state/rules/contracts, it belongs near core. If it points to input/rendering/timing, it belongs in the host or adapter.
