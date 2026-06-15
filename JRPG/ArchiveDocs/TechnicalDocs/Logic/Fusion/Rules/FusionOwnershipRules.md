# FusionOwnershipRules

Source: [`Logic/Fusion/Rules/FusionOwnershipRules.cs`](../../../../Logic/Fusion/Rules/FusionOwnershipRules.cs)

## Purpose

`FusionOwnershipRules` centralizes duplicate-result ownership checks for Fusion.
It is used by both the Cathedral UI pre-check and the mutator transaction guard.

## Class Shape

```csharp
public sealed class FusionOwnershipRules
{
    private readonly PartyManager _partyManager;

    public bool TryGetOwnedCreateResult(Combatant owner, string resultId, out FusionOwnedResult ownedResult)
    public Dictionary<object, string> BuildOwnedDuplicateResultReasons(...)
    public static bool TryGetDirectFusionResultId(...)
}

public readonly struct FusionOwnedResult
{
    public string ResultId { get; }
    public string DisplayName { get; }
    public string DisabledReason { get; }
    public string TransactionAbortMessage { get; }
}
```

## `TryGetOwnedCreateResult`

Normalizes the result ID and resolves a display name:

```csharp
string lookupId = resultId.ToLower();
string displayName = resultId;
Database.Personas.TryGetValue(lookupId, out PersonaData? template);
if (template != null)
{
    displayName = template.Name;
}
```

Operator duplicate check:

```csharp
if (owner.Class == ClassType.Operator && _partyManager.IsDemonOwned(owner, lookupId))
{
    ownedResult = new FusionOwnedResult(
        lookupId,
        displayName,
        $"Owned Result: {displayName}",
        "Fusion aborted: that demon is already in your party or COMP.");
    return true;
}
```

Wild Card duplicate check:

```csharp
if (owner.Class == ClassType.WildCard &&
    template != null &&
    _partyManager.IsPersonaOwned(owner, template.Name))
{
    ownedResult = new FusionOwnedResult(
        lookupId,
        template.Name,
        $"Owned Result: {template.Name}",
        "Fusion aborted: that Persona is already in your stock.");
    return true;
}
```

The returned `FusionOwnedResult` carries both UI text and transaction abort text
so both layers use the same source of truth.

## `BuildOwnedDuplicateResultReasons`

Builds disabled reasons for candidate second parents:

```csharp
var disabledReasons = new Dictionary<object, string>();
var excluded = exclusions.ToHashSet();
FusionParticipant parentA = FusionParticipant.From(firstParent);

foreach (object candidate in pool)
{
    if (excluded.Contains(candidate)) continue;

    FusionParticipant parentB = FusionParticipant.From(candidate);
    if (!TryGetDirectFusionResultId(parentA.CombatantView, parentB.CombatantView, out string? resultId)) continue;
    if (resultId == null) continue;
    if (!TryGetOwnedCreateResult(owner, resultId, out FusionOwnedResult ownedResult)) continue;

    disabledReasons[candidate] = ownedResult.DisabledReason;
}
```

This method is intentionally conservative. It blocks guaranteed direct create
results only. It does not call `FusionCalculator` because that would roll
accident probability during menu navigation.

## `TryGetDirectFusionResultId`

Checks only direct authored recipes:

```csharp
string? resultString =
    FindFusionRecipeResult(parentA.SourceId, parentB.SourceId) ??
    FindFusionRecipeResult(parentA.ActivePersona.Race, parentB.ActivePersona.Race);

if (string.IsNullOrEmpty(resultString))
{
    return false;
}

string lookupId = resultString.ToLower();
if (!Database.Personas.ContainsKey(lookupId))
{
    return false;
}

resultId = lookupId;
return true;
```

Non-literal recipe results such as rank mutation signals are ignored here.

## State And Mutation

This class does not mutate owner, party, stock, or Database state. It only
builds result objects and dictionaries.

## Invariants And Safety Rules

- The mutator guard remains final authority.
- UI duplicate prevention must not roll accidents.
- Operator duplicate checks use species/source ownership.
- Wild Card duplicate checks use Persona template name ownership.
- Disabled reason and abort reason should stay centralized here.

## Tests And Verification

Covered by ownership and duplicate transaction tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

## Refactor Notes

This file is a good candidate for future data-contract hardening. Result IDs
should eventually become typed IDs rather than raw strings.
