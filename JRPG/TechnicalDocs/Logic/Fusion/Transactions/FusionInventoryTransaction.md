# FusionInventoryTransaction

Source: [`Logic/Fusion/Transactions/FusionInventoryTransaction.cs`](../../../../Logic/Fusion/Transactions/FusionInventoryTransaction.cs)

## Purpose

`FusionInventoryTransaction` centralizes repeated party, stock, and Persona
mutation steps used by Fusion strategies.

Despite the name, it does not manage item inventory. It manages roster ownership
and replacement semantics.

## Class Shape

```csharp
public static class FusionInventoryTransaction
{
    public static void ConsumeDemon(FusionContext context, Combatant demon)
    public static void ConsumePersona(Combatant owner, Persona persona)
    public static void ReplaceDemon(FusionContext context, Combatant oldDemon, Combatant newDemon)
    public static void ReplacePersona(Combatant owner, Persona oldPersona, Persona newPersona)
}
```

## `ConsumeDemon`

```csharp
if (context.Party.ActiveParty.Contains(demon))
{
    context.Party.ReturnDemon(context.Owner, demon);
}

context.Owner.DemonStock.Remove(demon);
```

The unified stock model means active demons can also exist in `DemonStock`.
Consumption first removes active deployment, then removes ownership.

## `ConsumePersona`

```csharp
if (owner.ActivePersona == persona)
{
    owner.ActivePersona = null;
}

owner.PersonaStock.Remove(persona);
```

This prevents the owner from keeping an active pointer to a consumed Persona.

## `ReplaceDemon`

Copies ownership/control metadata:

```csharp
newDemon.OwnerId = oldDemon.OwnerId;
newDemon.Controller = oldDemon.Controller;
newDemon.BattleControl = oldDemon.BattleControl;
```

Replaces active party reference when present:

```csharp
int activeIndex = context.Party.ActiveParty.IndexOf(oldDemon);
if (activeIndex != -1)
{
    context.Party.ActiveParty[activeIndex] = newDemon;
    newDemon.PartySlot = activeIndex;
    oldDemon.PartySlot = -1;
}
```

Replaces stock reference when present:

```csharp
int stockIndex = context.Owner.DemonStock.IndexOf(oldDemon);
if (stockIndex != -1)
{
    context.Owner.DemonStock[stockIndex] = newDemon;
}
else if (activeIndex == -1)
{
    context.Owner.DemonStock.Add(newDemon);
}
```

The fallback add only runs when the old demon was neither active nor in stock.
Normal unified-stock replacements should replace both active and stock
references when both exist.

## `ReplacePersona`

```csharp
if (owner.ActivePersona == oldPersona)
{
    owner.ActivePersona = newPersona;
}
else
{
    owner.PersonaStock.Remove(oldPersona);
    owner.PersonaStock.Add(newPersona);
}

owner.RecalculateResources();
```

Active Persona replacement does not also add to stock. Non-active Persona
replacement removes the old stock entry and adds the new one.

## State And Mutation

This helper directly mutates:

- `PartyManager.ActiveParty`,
- `owner.DemonStock`,
- `owner.ActivePersona`,
- `owner.PersonaStock`,
- demon control fields,
- party slots,
- owner resources.

## Invariants And Safety Rules

- Consumed active demons must leave active party and stock.
- Consumed active Personas must clear `ActivePersona`.
- Replaced active demons should preserve party slot.
- Replaced demons should keep ownership/control metadata.
- Replacements should recalculate owner resources.

## Tests And Verification

Covered by transaction regression tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs), especially active/stock replacement checks.

## Refactor Notes

Consider renaming this to `FusionRosterTransaction` later. The current behavior
is roster ownership, not inventory economy.
