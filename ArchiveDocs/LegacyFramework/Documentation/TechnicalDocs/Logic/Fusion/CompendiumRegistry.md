# CompendiumRegistry

Source: [`Logic/Fusion/CompendiumRegistry.cs`](../../../Logic/Fusion/CompendiumRegistry.cs)

## Purpose

`CompendiumRegistry` is the in-memory storage authority for the Demonic
Compendium. It records demon snapshots by species ID, calculates recall costs,
and returns cloned recall entries so recalled demons do not directly reuse the
stored registry object.

This class is currently runtime storage only. It does not persist Compendium
data to disk.

## Runtime Role

The registry is used by the Cathedral flow for two player-facing actions:

- registration: save or update a demon snapshot,
- recall: inspect a registered demon and materialize a copy for a Macca cost.

`FusionConductor` owns the menu workflow around registration and recall.
`FusionMutator.FinalizeRecall` owns the final spend-and-add transaction.
`CompendiumRegistry` owns the stored snapshot and recall-cost calculation.

## Key Collaborators

- `IGameIO`: writes registration feedback and waits briefly after registration.
- `Database.ShopInventory`: optional source for base recall price.
- `Combatant`: snapshot shape used for registered entries.
- `FusionConductor`: calls registration, lookup, and recall-cost methods.
- `FusionMutator`: receives cloned recall entries and commits them to the owner.

## Important Methods / Members

Current class shape:

```csharp
public class CompendiumRegistry
{
    private readonly Dictionary<string, Combatant> _demonEntries;
    private readonly IGameIO _io;

    public void RegisterDemon(Combatant demon)
    public int CalculateRecallCost(string speciesId)
    public Combatant GetRecallEntry(string speciesId)
    public List<Combatant> GetAllRegisteredDemons()
    public bool HasEntry(string speciesId)
}
```

### `_demonEntries`

Stores registered snapshots in a case-insensitive dictionary. Keys are normalized
species IDs and values are cloned `Combatant` snapshots.

The dictionary represents species-level uniqueness. Registering another demon
with the same species ID updates the existing entry instead of adding a second
entry.

### `RegisterDemon`

Validates that the input is a demon combatant, resolves the normalized species
ID, clones the combatant, and either inserts or replaces the registry entry.

Key implementation:

```csharp
if (demon == null || demon.Class != ClassType.Demon)
{
    _io.WriteLine("Invalid entity. Only demons can be registered in the Compendium.", ConsoleColor.Red);
    return;
}

string speciesId = ResolveSpeciesId(demon);

Combatant snapshot = CloneCombatant(demon);
snapshot.SourceId = speciesId;

if (_demonEntries.ContainsKey(speciesId))
{
    _demonEntries[speciesId] = snapshot;
}
else
{
    _demonEntries.Add(speciesId, snapshot);
}
```

This is the Compendium's duplicate policy: the registry is unique by species ID,
so registration updates the saved snapshot rather than creating another entry.

Important behavior:

- non-demons are rejected,
- species IDs are normalized to lowercase,
- the stored snapshot has `SourceId` rewritten to the normalized species ID,
- existing entries are replaced in-place.

### `CalculateRecallCost`

Calculates the Macca cost for a registered entry. The formula is:

```text
base price + level premium + stat premium + skill premium
```

Current details:

- base price defaults to `2000`,
- shop inventory can override base price,
- level premium is `Level * 100`,
- stat premium is total stats times `50`,
- skill premium is consolidated skill count times `200`.

If the species is not registered, the method returns `0`.

Key implementation:

```csharp
int basePrice = 2000;
var shopEntry = Database.ShopInventory.FirstOrDefault(s =>
    s.Id.Equals(cleanId, StringComparison.OrdinalIgnoreCase));

if (shopEntry != null)
{
    basePrice = shopEntry.BasePrice;
}

int levelMod = snapshot.Level * 100;
int statsMod = snapshot.CharacterStats.Values.Sum() * 50;
int skillMod = snapshot.GetConsolidatedSkills().Count * 200;

return basePrice + levelMod + statsMod + skillMod;
```

The formula currently rewards stronger registered snapshots with higher recall
costs. That means a demon registered after leveling, stat boosting, or skill
inheritance should become more expensive to recall.

### `GetRecallEntry`

Returns a clone of the stored snapshot. This protects the registry entry from
being mutated when the recalled demon is added to stock, summoned, boosted, or
otherwise modified later.

Key implementation:

```csharp
if (_demonEntries.TryGetValue(cleanId, out var snapshot))
{
    return CloneCombatant(snapshot);
}

return null;
```

Current nullable caveat: the method returns `null` when no entry exists even
though the signature is `Combatant`. This is part of the existing nullable debt.

### `GetAllRegisteredDemons`

Returns registered snapshots ordered by level and then name. The returned list is
used by the UI layer for menu display.

### `HasEntry`

Checks whether a species ID exists after lowercasing the input.

### `CloneCombatant`

Creates the stored or recalled snapshot. It copies scalar combatant state, stats,
extra skills, controller fields, and `ActivePersona`, then recalculates resources
and restores HP/SP to maximum.

Key implementation:

```csharp
Combatant clone = new Combatant(original.Name, original.Class)
{
    SourceId = original.SourceId.ToLower(),
    Level = original.Level,
    Exp = original.Exp,
    StatPoints = original.StatPoints,
    BaseHP = original.BaseHP,
    BaseSP = original.BaseSP,
    OwnerId = original.OwnerId,
    BattleControl = original.BattleControl,
    Controller = original.Controller,
    ActivePersona = original.ActivePersona
};

foreach (var stat in original.CharacterStats)
{
    clone.CharacterStats[stat.Key] = stat.Value;
}

foreach (var skill in original.ExtraSkills)
{
    clone.ExtraSkills.Add(skill);
}

clone.RecalculateResources();
clone.CurrentHP = clone.MaxHP;
clone.CurrentSP = clone.MaxSP;
```

The clone is deep enough for scalar combatant state, stats, and extra skills.
It is not a full deep clone of nested Persona state because `ActivePersona` is
copied by reference.

Important caveat: `ActivePersona` is currently copied by reference. That is
acceptable for the current prototype but should be revisited before framework
extraction or persistent Compendium saves.

## State And Mutation

This class mutates only `_demonEntries` and the newly created clone objects.

It must not mutate the original combatant passed into `RegisterDemon`, and it
must not mutate the stored snapshot when `GetRecallEntry` is called.

## Invariants And Safety Rules

- Registry keys should remain species IDs, not instance IDs.
- Registration should update existing species entries rather than allowing
  duplicate Compendium rows.
- Recall should return a clone, not the stored object.
- Recall cost must be calculated from the registered snapshot, not live party
  state.

## Data Dependencies

- `Database.ShopInventory` for optional base recall prices.
- `Combatant.GetConsolidatedSkills()` for skill premium calculation.
- `Combatant.CharacterStats` for stat premium calculation.

## Failure / Cancel / Edge Behavior

- Invalid registration prints an error and returns.
- Missing recall entry returns `null`.
- Missing recall-cost entry returns `0`.
- Missing shop price falls back to `2000`.

Cancel and duplicate recall behavior are not owned here. They are handled by the
bridge and mutator layers.

## Tests And Verification

Relevant regression coverage lives in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../Convergence.Tests/FusionBugRegressionTests.cs).

Important manual smoke checks:

- register a demon, then recall it,
- recall should spend Macca only when allowed,
- duplicate recall should be blocked before spending Macca,
- recalled demons should not mutate the stored registry snapshot.

## Refactor Notes

Future framework work should separate three concepts that are currently combined:

- in-memory registry storage,
- snapshot clone policy,
- recall-cost formula.

The likely framework shape is a data/repository service for registry state, a
snapshot factory, and a recall pricing rule object. Persistence should be added
only after clone semantics are made explicit.
