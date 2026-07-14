# FusionMutator

Source: [`Logic/Fusion/FusionMutator.cs`](../../../Logic/Fusion/FusionMutator.cs)

## Purpose

`FusionMutator` is the state-mutation authority for Fusion. It is the boundary
between a planned/previewed ritual and committed gameplay state.

It owns:

- strategy dispatch for confirmed fusion transactions,
- duplicate create-result protection,
- Compendium recall finalization,
- economy spending for recall.

## Runtime Role

`FusionConductor` creates a `FusionContext` only after confirmation and sends it
to `FusionMutator.ExecuteFusionTransaction`. The mutator checks final guards and
then delegates to the strategy registered for the requested operation.

For Compendium recall, the conductor passes a cloned snapshot and calculated
cost into `FinalizeRecall`; the mutator validates Macca and duplicate ownership
before adding the recalled entity.

## Key Collaborators

- `FusionStrategyRegistry`: maps `FusionOperationType` to `IFusionStrategy`.
- `FusionOwnershipRules`: detects already-owned create-results.
- `PartyManager`: party and stock operations.
- `EconomyManager`: Macca spend checks.
- `IFusionMessenger`: transaction feedback.
- `FusionContext`: confirmed transaction payload.

## Important Methods / Members

Current class shape:

```csharp
public class FusionMutator
{
    public List<Combatant> GetFusibleDemonPool(Combatant owner)
    public List<Persona> GetFusiblePersonaPool(Combatant owner)
    public void ExecuteFusionTransaction(FusionContext context, FusionOperationType type)
    public bool FinalizeRecall(Combatant owner, Combatant snapshot, int cost)
}
```

Important state:

```csharp
private readonly PartyManager _partyManager;
private readonly EconomyManager _economy;
private readonly IFusionMessenger _messenger;
private readonly FusionStrategyRegistry _registry;
private readonly FusionOwnershipRules _ownershipRules;
```

### Constructor

Stores party, economy, and messenger references, then creates a strategy registry
and ownership rules helper.

The registry is currently constructed directly. A future framework version may
inject strategies or rulesets instead.

Key implementation:

```csharp
_registry = new FusionStrategyRegistry();
_ownershipRules = new FusionOwnershipRules(partyManager);
```

### `GetFusibleDemonPool`

Returns a copy of `owner.DemonStock`.

This is preserved compatibility API from the unified stock model. The conductor
currently builds its own participant pool from active party plus stock, so this
method is not the main Cathedral selection path.

### `GetFusiblePersonaPool`

Returns the active Persona plus Persona stock, with duplicates removed.

Like `GetFusibleDemonPool`, this is compatibility API and may later become part
of a roster query service.

### `ExecuteFusionTransaction`

Commits a confirmed ritual.

Execution order:

1. If operation is `CreateNewDemon`, check duplicate ownership.
2. Return immediately if duplicate result is already owned.
3. Fetch the matching strategy from the registry.
4. Execute the strategy if found.
5. Publish a system error if no strategy exists.

The duplicate check is deliberately repeated here even though the UI disables
known duplicate second-parent options. The UI check improves player experience;
the mutator check protects transaction safety.

Key implementation:

```csharp
public void ExecuteFusionTransaction(FusionContext context, FusionOperationType type)
{
    if (type == FusionOperationType.CreateNewDemon && IsDuplicateFusionResult(context))
    {
        return;
    }

    var strategy = _registry.GetStrategy(type);
    if (strategy != null)
    {
        strategy.Execute(context);
    }
    else
    {
        _messenger.Publish($"[System Error] No strategy found for {type}", ConsoleColor.Red);
    }
}
```

This is the single most important safety point in the current Fusion execution
path. If duplicate create-result detection returns true, no strategy executes,
so no materials are consumed.

### `IsDuplicateFusionResult`

Uses `FusionOwnershipRules.TryGetOwnedCreateResult` and publishes the shared
transaction abort message when the result is already owned.

This keeps the mutator guard and bridge disabled reason aligned around one
rules source.

Key implementation:

```csharp
if (_ownershipRules.TryGetOwnedCreateResult(
    context.Owner,
    context.ResultId,
    out FusionOwnedResult ownedResult))
{
    context.Messenger.Publish(ownedResult.TransactionAbortMessage, ConsoleColor.Red, 1000);
    return true;
}

return false;
```

### `FinalizeRecall`

Commits Compendium recall.

Execution order:

1. Reject if Macca is insufficient.
2. Reject Operator recall if the demon species is already owned.
3. Reject Wild Card recall if the Persona is already owned.
4. Spend Macca.
5. For Operators, add snapshot to `DemonStock`.
6. Try to summon into active party if space exists.
7. For Wild Cards, copy the snapshot's consolidated skills into the recalled
   Persona and add it to `PersonaStock`.

The method returns `true` only when recall succeeds.

Key implementation:

```csharp
if (_economy.Macca < cost)
{
    _messenger.Publish("Recall Aborted: Insufficient Macca.", ConsoleColor.Red);
    return false;
}

if (owner.Class == ClassType.Operator && _partyManager.IsDemonOwned(owner, snapshot.SourceId))
{
    _messenger.Publish($"{snapshot.Name} is already in your party or COMP.", ConsoleColor.Red, 1000);
    return false;
}

if (owner.Class == ClassType.WildCard && snapshot.ActivePersona != null &&
    _partyManager.IsPersonaOwned(owner, snapshot.ActivePersona.Name))
{
    _messenger.Publish($"{snapshot.ActivePersona.Name} is already in your Persona stock.", ConsoleColor.Red, 1000);
    return false;
}
```

These checks happen before Macca is spent.

Operator recall mutation:

```csharp
owner.DemonStock.Add(snapshot);

if (!_partyManager.SummonDemon(owner, snapshot))
{
    _messenger.Publish($"{snapshot.Name} was sent to the COMP.", ConsoleColor.Gray, 600);
}
```

The recalled demon enters stock first, then the party manager tries to place it
into the active party if room exists.

Wild Card recall mutation:

```csharp
Persona essence = snapshot.ActivePersona;

var combinedSkills = snapshot.GetConsolidatedSkills();
essence.SkillSet.Clear();
foreach (var s in combinedSkills)
{
    essence.SkillSet.Add(s);
}

owner.PersonaStock.Add(essence);
```

The snapshot's consolidated combatant skills are copied back into the recalled
Persona's skill set before it enters Persona stock.

## State And Mutation

This class can mutate:

- owner demon stock,
- owner Persona stock,
- owner active party through `PartyManager`,
- economy Macca,
- recalled Persona skill sets,
- transaction state indirectly through strategies.

It does not create ritual previews and should not be called before player
confirmation.

## Invariants And Safety Rules

- Duplicate create-result fusion must not consume parents.
- Duplicate recall must not spend Macca.
- Recall spends Macca only after ownership checks pass.
- Strategy execution should happen only through a confirmed `FusionContext`.
- Missing strategy should report an error rather than silently doing nothing.

## Data Dependencies

`FusionMutator` has minimal direct database dependency. Most data lookup happens
inside strategies or earlier planning layers.

It depends on `PartyManager` ownership semantics, especially:

- `IsDemonOwned`,
- `IsPersonaOwned`,
- `SummonDemon`.

## Failure / Cancel / Edge Behavior

The mutator does not model cancel/back behavior. By the time it runs, the player
has confirmed a transaction.

Failure paths:

- duplicate fusion result returns without consuming materials,
- missing strategy publishes an error,
- insufficient Macca returns false,
- duplicate recall returns false,
- failed summon after Operator recall leaves the demon in COMP stock.

## Tests And Verification

Relevant tests live in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../Convergence.Tests/FusionBugRegressionTests.cs).

Important tested behaviors:

- duplicate create-result does not consume parents,
- standard fusion consumes expected parents,
- stat boost consumes Mitama catalyst,
- recall blocks duplicates and does not spend Macca when blocked.

Important manual smoke checks:

- try duplicate-result fusion after selecting a valid first parent,
- recall an already-owned Compendium entry,
- perform normal fusion and verify parent consumption,
- perform Mitama stat boost and verify catalyst consumption.

## Refactor Notes

`FusionMutator` is close to a future transaction service, but it still depends on
current entity, party, economy, and strategy shapes. Before framework extraction,
transaction requests should become typed commands and strategy results should
become explicit success/failure objects instead of relying only on mutation plus
messenger output.
