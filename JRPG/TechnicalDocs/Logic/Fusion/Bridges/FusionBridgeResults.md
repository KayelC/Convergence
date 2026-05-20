# FusionBridgeResults

Source: [`Logic/Fusion/Bridges/FusionBridgeResults.cs`](../../../../Logic/Fusion/Bridges/FusionBridgeResults.cs)

## Purpose

`FusionBridgeResults.cs` defines the typed result vocabulary returned by
`CathedralUIBridge`. These records and enums replace ambiguous `null`, raw menu
indexes, and string command identifiers.

## Runtime Role

The conductor branches on these result kinds to decide whether to continue,
retry, commit, back out, or do nothing.

Example:

```csharp
FusionMainMenuResult choice = _uiBridge.ShowCathedralMainMenu(...);
if (choice.Kind == FusionMenuResultKind.Back) return;
```

## Key Result Types

### Main Menu

```csharp
public enum FusionMainMenuAction
{
    BinaryFusion,
    SacrificialFusion,
    BrowseCompendium,
    RegisterDemon
}

public enum FusionMenuResultKind
{
    Selected,
    Back
}

public sealed record FusionMainMenuResult(
    FusionMenuResultKind Kind,
    FusionMainMenuAction? Action = null)
```

Only `Selected` carries an action. `Back` is navigation, not failure.

### Ritual Confirmation

```csharp
public enum RitualConfirmationKind
{
    Commence,
    Wait,
    Cancel,
    Forbidden
}
```

Each state maps to a different conductor transition:

- `Commence`: execute transaction.
- `Wait`: keep parents and return to skill inheritance.
- `Cancel`: discard staged result and return to participant selection.
- `Forbidden`: level gate blocked the ritual before confirmation.

### Participant Selection

```csharp
public enum RitualParticipantSelectionKind
{
    Selected,
    Canceled,
    Unavailable
}

public sealed record RitualParticipantSelectionResult<T>(
    RitualParticipantSelectionKind Kind,
    T? Participant = null)
    where T : class
```

This distinguishes player navigation from a genuinely empty candidate pool.

### Compendium Recall

```csharp
public enum CompendiumRecallResultKind
{
    Selected,
    Back,
    Unavailable
}
```

Only `Selected` should advance into economy and recall materialization checks.

### Compendium Registration

```csharp
public enum CompendiumRegistrationSelectionKind
{
    Selected,
    Canceled,
    Unavailable
}
```

`Unavailable` means the Operator owns no registerable demons. `Canceled` means
the player left a populated registration picker.

### Skill Inheritance

```csharp
public enum SkillInheritanceSelectionKind
{
    Confirmed,
    Aborted
}

public sealed record SkillInheritanceSelectionResult(
    SkillInheritanceSelectionKind Kind,
    IReadOnlyList<string> Skills)
```

The key semantic distinction:

```csharp
SkillInheritanceSelectionResult.Confirmed(Array.Empty<string>())
```

means deliberate zero-inheritance, while:

```csharp
SkillInheritanceSelectionResult.Aborted
```

means the ritual should not proceed.

## State And Mutation

These records are immutable result payloads. They do not mutate game state.

## Invariants And Safety Rules

- Static factory properties should be used for non-payload states.
- Payloads are meaningful only for selected/confirmed states.
- Expected navigation should never be represented by `null`.
- Empty skill lists can be valid payloads.

## Tests And Verification

Covered directly by
[`Convergence.Tests/FusionBridgeResultTests.cs`](../../../../Convergence.Tests/FusionBridgeResultTests.cs).

Important assertions:

- back/cancel/unavailable kinds are distinct,
- selected results carry payloads,
- aborted skill inheritance carries an empty payload,
- confirmed empty skill inheritance remains confirmed.

## Refactor Notes

Keep result types subsystem-specific for now. A giant universal UI result
abstraction would be premature until Fusion, Field, and Battle have all proven
their own adapter contract shapes.
