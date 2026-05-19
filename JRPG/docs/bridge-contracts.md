# Bridge Contract Design Charter

## Purpose

This document defines the bridge contract direction for Convergence. It is a design charter for all bridge and adapter refactors, not a one-off cleanup of a single menu.

The current console prototype uses bridges to collect choices and render context through `IGameIO`. That pattern is useful, but the long-term framework needs clearer contracts between host presentation and gameplay systems. Bridge methods should make user intent explicit so future console, Unity, Godot, or custom adapters can drive the same framework behavior without copying console-shaped control flow.

## Why This Matters

Convergence is intended to become an engine-agnostic RPG systems framework. That means core gameplay logic should not depend on console rendering, console colors, blocking keyboard input, menu indices, sleeps, or magic sentinel values.

Future hosts should translate their own UI into framework-friendly commands and consume framework-friendly results. A Unity adapter might receive a button click. A Godot adapter might receive a signal. The console sample receives a keypress. Those hosts should not need to mimic `RenderMenu` return codes to use the framework.

Explicit bridge contracts also prepare the codebase for state machines. A state machine needs named commands, named results, and clear transitions. Ambiguous `null`, string commands, and magic integers make those transitions harder to test and reason about.

## Current Bridge Model

The current bridge model is centered on:

- `IGameIO`, the active input/output abstraction.
- `ConsoleIO`, the current terminal-backed implementation.
- `MenuUI`, the current keyboard menu renderer.
- bridge classes such as `InteractionBridge`, field bridges, and `CathedralUIBridge`.

This model already separates many menus from conductors, which is good. The problem is that the contracts are still prototype-shaped. Many bridge methods return raw strings, nullable objects, tuples, or integers that mean different things depending on the caller.

The bridge classes also sometimes mix responsibilities:

- rendering menu text
- reading selection input
- presenting previews or status screens
- checking whether an action is allowed
- returning a command or selected object
- signaling cancel/back/no candidate states

The migration should keep the useful bridge boundary while making the contracts more explicit.

## Problems To Eliminate

### Ambiguous Null

`null` currently has several possible meanings:

- the user canceled
- the user chose back
- no valid candidates exist
- a redundant action was rejected
- the selected data could not be found
- a flow failed before producing a result

Those are different outcomes and should eventually have different result states.

### String Commands

Several flows use strings as command identifiers, such as menu labels or action names. This makes behavior depend on display text and creates fragile links between UI copy and rules.

Display labels can stay as strings, but gameplay intent should be represented by named result kinds or typed commands.

### Weak Tuple Payloads

Tuple returns can be convenient, but they hide intent once flows become complex. For example, a tuple containing an action and optional combatants forces the reader to remember which fields matter for each action.

Result objects should name the meaning of each payload.

### Menu Sentinel Leakage

`RenderMenu` uses sentinel values such as `-1` for cancel and values such as `<= -10` for status inspect. Those are implementation details of the console menu renderer.

Bridge methods can interpret those values internally, but conductors and framework-facing flows should receive named outcomes like `Canceled`, `Back`, `Selected`, or `InspectRequested`.

### Mixed Presentation And Rule Validation

Bridges should present choices and collect intent. Core rules should decide whether commands are legal when that validation belongs to gameplay.

Some validation is acceptable inside bridges when it is purely UI affordance, such as disabling an option that the current screen should not allow. Deeper rule validation should move toward engines, processors, or state machines.

## Target Pattern

Bridge contracts should move toward small, explicit result shapes.

Recommended vocabulary:

- **Command:** what the player or host is asking to do.
- **Result:** what the bridge or workflow produced.
- **Result Kind:** a named state such as `Selected`, `Canceled`, `Back`, `Unavailable`, `Confirmed`, `Declined`, or `InspectRequested`.
- **Payload:** typed data attached to a result, such as a selected `Combatant`, `Persona`, `SkillData`, item ID, or action enum.
- **Cancel/Back:** expected navigation, not an error.
- **Unavailable:** no valid option exists.
- **Inspect/Preview:** secondary UI request that should not commit a gameplay action.
- **Confirm/Decline/Abort:** distinct decisions in staged workflows such as fusion.

The first implementations should use small subsystem-specific records or classes. Avoid creating a large universal bridge abstraction before multiple migrations prove what the shared shape should be.

Example direction:

```csharp
public enum FusionMenuResultKind
{
    Selected,
    Back,
    Unavailable
}

public sealed record FusionMenuResult(
    FusionMenuResultKind Kind,
    FusionMenuAction? Action = null);
```

The exact names should be decided during the implementation issue for each subsystem. The important pattern is that user intent is explicit and payloads are typed.

## Design Rules

- Cancel/back must be explicit after a bridge method is migrated.
- Empty or no-candidate states must be explicit.
- Confirmation outcomes must not be represented by magic integers.
- Display labels should not be gameplay command identifiers.
- Bridges may render UI, previews, and status screens.
- Core rules should not depend on console rendering, colors, sleeps, or cursor state.
- Menu sentinel values may exist inside `MenuUI` and `IGameIO` implementations, but should not leak into conductors.
- Result types should be small and subsystem-specific at first.
- Do not build a giant universal abstraction before the pattern stabilizes.
- Preserve current behavior while changing contracts.

## First Migration: Fusion Bridge

Fusion should be the first bridge migration target.

Why fusion first:

- `CathedralUIBridge` contains meaningful staged choices.
- Fusion has cancel/back/confirm flows that benefit from explicit results.
- It is smaller and less volatile than the battle bridge.
- It is complex enough to prove the pattern before applying it elsewhere.

Recommended migration order:

1. `ShowCathedralMainMenu` - completed
   - Replace string command results with a typed action/result.
   - Preserve current labels and menu order.
2. `ConfirmRitual` - completed
   - Replace integer outcomes with named confirmation results.
   - Preserve current meanings: commence, wait, cancel, forbidden.
3. `SelectRitualParticipant` - completed
   - Replace nullable participant returns with selected/back/unavailable states.
4. `ShowCompendiumRecallMenu` - completed
   - Separate selected recall target from back and empty compendium.
5. `SelectDemonToRegister` - completed
   - Separate selected demon from cancel and no valid demons.
6. `SelectInheritedSkills`
   - Separate confirmed empty selection from aborted fusion.

`FusionConductor` should only change as needed to consume the new result states. Fusion rules, ritual outcomes, costs, inheritance, compendium behavior, and menu text should remain unchanged.

## Later Migrations

Field bridges should follow after fusion proves the pattern. Field flows have many menu surfaces, but many are straightforward selections that can reuse the same design vocabulary.

Battle should come later. `InteractionBridge` has the highest long-term payoff, but it is also the most sensitive bridge because it touches turn economy, targeting, negotiation, persona actions, COMP actions, items, tactics, status inspect, and redundant-action handling.

Battle migration should wait until:

- fusion result contracts are proven
- bridge tests or fake `IGameIO` helpers are available
- cancel/back semantics are already documented by a smaller subsystem

## Acceptance Checklist

Use this checklist for each bridge contract migration:

- User intent is represented by named result states.
- Expected cancel/back behavior does not use raw `null`.
- No-candidate and unavailable states are distinguishable from cancel.
- Display text is not the gameplay command contract.
- Conductors receive typed outcomes instead of menu sentinel values.
- Console menu labels and user-facing behavior remain unchanged.
- Existing tests pass.
- Any changed flow receives either automated coverage or a manual smoke-test note.
- The result shape would make sense for a Unity or Godot adapter.

## Next Implementation Issue

The next coding issue after this document should be:

**Introduce explicit fusion bridge result contracts.**

Initial scope:

- Add small fusion-specific result types.
- Migrate `ShowCathedralMainMenu`.
- Migrate `ConfirmRitual`.
- Update `FusionConductor` only as needed to consume the new result states.
- Leave battle and field bridges untouched.

This keeps the first contract refactor meaningful but small. It should prove the pattern without dragging the whole adapter layer into one issue.

## Progress Notes

- The first fusion bridge contract migration introduced typed results for the Cathedral main menu and ritual confirmation flow.
- `ShowCathedralMainMenu` no longer returns display strings as commands.
- `ConfirmRitual` no longer returns magic integers for commence, wait, cancel, or forbidden outcomes.
- `SelectRitualParticipant` now distinguishes selected participants from canceled and unavailable states.
- Inherited skill selection, compendium recall, and demon registration still use the older nullable-return pattern and remain the next fusion bridge candidates.
