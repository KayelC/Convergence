# FusionCalculator

Source: [`Logic/Fusion/FusionCalculator.cs`](../../../Logic/Fusion/FusionCalculator.cs)

## Purpose

`FusionCalculator` is the prediction and rules engine for Fusion. It does not
commit state changes. Instead, it answers questions such as:

- what operation does this parent pair produce,
- what target species ID should be used,
- whether an accident occurs,
- which skills can be inherited,
- which skills are exclusive,
- how many inheritance slots are available,
- how accident skill mutation should resolve.

## Runtime Role

The calculator sits before transaction execution. `FusionPlanFactory` calls it
to create a `FusionPlan`, and `FusionConductor` calls it during accident handling
to mutate selected accident skills.

The calculator is still coupled to static `Database` content and uses internal
randomness. It is a strong future framework-core candidate, but it needs
injectable data access and RNG before it becomes engine-agnostic.

## Key Collaborators

- `Database.FusionRecipes`: source for specific ID, race, and rank mappings.
- `Database.Personas`: source for race, rank, level, and result templates.
- `Database.Skills`: source for exclusivity, skill family, and skill rank.
- `IFusionMessenger`: publishes diagnostic trace messages.
- `FusionPlanFactory`: consumes prediction and inheritance outputs.
- `FusionConductor`: calls skill mutation during accidents.

## Important Methods / Members

Current class shape:

```csharp
public class FusionCalculator
{
    private readonly IGameIO _io;
    private readonly IFusionMessenger _messenger;
    private readonly Random _rnd = new Random();
    private readonly Dictionary<string, Dictionary<string, string>> _raceTable;

    public (FusionOperationType operation, string? targetEntityId, bool isAccident)
        CalculateResult(Combatant a, Combatant b, int moonPhase)

    public List<string> GetInheritableSkills(params Combatant[] parents)
    public List<string> GetExclusiveSkills(params Combatant[] parents)
    public string GetMutatedSkill(string originalSkillName)
    public int GetInheritanceSlotCount(params Combatant[] parents)
}
```

### `_raceTable`

An internal two-dimensional lookup table built from `Database.FusionRecipes`.
Despite the name, it stores both specific ID mappings and race mappings.

`LoadFusionTable` registers each recipe twice so parent order is commutative:

```text
A + B = result
B + A = result
```

Key implementation:

```csharp
foreach (var recipe in Database.FusionRecipes)
{
    RegisterMapping(recipe.ParentA, recipe.ParentB, recipe.Result);
    RegisterMapping(recipe.ParentB, recipe.ParentA, recipe.Result);
}
```

### `CalculateResult`

Predicts the operation and target ID for two combatants.

The current priority order is:

1. reject pairs with missing active Persona data,
2. roll fusion accident chance,
3. apply Mitama override,
4. look for specific ID recipe,
5. look for race recipe,
6. treat literal result IDs as create-new results,
7. treat `1` or `-1` as Element rank mutation,
8. treat other result strings as normal race fusion.

Accident chance:

- normal moon phase: `1%`,
- full moon phase `8`: `12%`.

Key implementation:

```csharp
int accidentThreshold = (moonPhase == 8) ? 12 : 1;
bool isAccident = _rnd.Next(0, 100) < accidentThreshold;
```

Mitama override:

- Mitama plus non-Mitama becomes `StatBoostFusion`,
- Mitama plus Mitama has no result,
- Element targets cannot receive Mitama boosts.

Key implementation:

```csharp
bool aIsMitama = raceA.Equals("Mitama", StringComparison.OrdinalIgnoreCase);
bool bIsMitama = raceB.Equals("Mitama", StringComparison.OrdinalIgnoreCase);

if (aIsMitama || bIsMitama)
{
    if (aIsMitama && bIsMitama)
    {
        return (FusionOperationType.NoFusionPossible, null, false);
    }

    Combatant target = aIsMitama ? b : a;

    if (target.ActivePersona.Race.Equals("Element", StringComparison.OrdinalIgnoreCase))
    {
        return (FusionOperationType.NoFusionPossible, null, false);
    }

    return (FusionOperationType.StatBoostFusion, target.SourceId.ToLower(), isAccident);
}
```

This branch must stay before recipe lookup. If recipe lookup ran first, Mitama
stat boosts could be swallowed by ordinary fusion-table behavior.

Recipe lookup:

```csharp
if (_raceTable.TryGetValue(idA, out var idBranch) &&
    idBranch.TryGetValue(idB, out resultString))
{
    // specific ID recipe
}
else if (_raceTable.TryGetValue(raceA, out var raceBranch) &&
    raceBranch.TryGetValue(raceB, out resultString))
{
    // race recipe
}
```

Specific ID recipes win over race recipes.

Rank mutation:

- `1` means rank up,
- `-1` means rank down,
- the non-Element parent is the target,
- the target result is found by matching race and target rank.

Key implementation:

```csharp
if (resultString == "1" || resultString == "-1")
{
    Combatant? parentToRank = null;
    if (!raceA.Equals("Element", StringComparison.OrdinalIgnoreCase)) parentToRank = a;
    else if (!raceB.Equals("Element", StringComparison.OrdinalIgnoreCase)) parentToRank = b;

    int rankDir = (resultString == "1") ? 1 : -1;
    int targetRank = parentToRank.ActivePersona.Rank + rankDir;

    var nextRankData = Database.Personas.Values.FirstOrDefault(p =>
        p.Race.Equals(parentToRank.ActivePersona.Race, StringComparison.OrdinalIgnoreCase) &&
        p.Rank == targetRank);
}
```

This is an Element-style rank operation. The Element is the catalyst; the
non-Element parent is the demon or Persona being changed.

Normal race fusion:

- uses parent template base levels, not current combatant levels,
- averages both base levels,
- adds a random offset from `1` to `5`,
- chooses the first demon in the result race whose level is at least that target,
- falls back to the highest member of the race,
- if the result matches a parent, shifts to the next tier when possible.

Key implementation:

```csharp
int avgBaseLevel = (templateA.Level + templateB.Level) / 2;
int targetLevel = avgBaseLevel + _rnd.Next(1, 6);

var racePool = Database.Personas.Values
    .Where(p => p.Race.Equals(resultString, StringComparison.OrdinalIgnoreCase))
    .OrderBy(p => p.Level)
    .ToList();

PersonaData resultData = isAccident
    ? racePool.First()
    : racePool.FirstOrDefault(p => p.Level >= targetLevel) ?? racePool.Last();

if (resultData.Id == templateA.Id || resultData.Id == templateB.Id)
{
    int idx = racePool.IndexOf(resultData);
    if (idx + 1 < racePool.Count) resultData = racePool[idx + 1];
}
```

This uses database template levels, not the current leveled-up combatant levels.
That matters for reverse-engineered SMT-style behavior.

### `GetInheritableSkills`

Builds a distinct skill pool from parent consolidated skills, excluding skills
whose `SkillData.IsExclusive()` check returns true.

This is the legal inheritance pool used for selection and slot calculation.

Key implementation:

```csharp
foreach (var skillName in p.GetConsolidatedSkills())
{
    if (Database.Skills.TryGetValue(skillName, out var skillData))
    {
        if (!skillData.IsExclusive())
        {
            pool.Add(skillName);
        }
    }
}

return pool.Distinct().ToList();
```

### `GetExclusiveSkills`

Builds a distinct pool of exclusive parent skills. These are not legal choices,
but they are displayed by the bridge as disabled entries so the player can see
why they cannot be selected.

### `GetMutatedSkill`

Used during fusion accidents. It tries to move a skill up or down by one rank
within the same skill family.

Important behavior:

- missing skill data returns the original skill,
- non-evolving skills return the original skill,
- nonnumeric ranks return the original skill,
- rank `1` cannot mutate downward,
- missing target family/rank returns the original skill.

Key implementation:

```csharp
if (!Database.Skills.TryGetValue(originalSkillName, out var current)) return originalSkillName;
if (!current.CanEvolve()) return originalSkillName;
if (!int.TryParse(current.Rank, out int currentRankInt)) return originalSkillName;

int direction = _rnd.Next(0, 2) == 0 ? 1 : -1;
if (currentRankInt == 1 && direction == -1) direction = 1;

int targetRank = currentRankInt + direction;

var mutation = Database.Skills.Values.FirstOrDefault(s =>
    s.Family.Equals(current.Family, StringComparison.OrdinalIgnoreCase) &&
    s.Rank == targetRank.ToString());

return mutation?.Name ?? originalSkillName;
```

### `GetInheritanceSlotCount`

Calculates inheritance slots from legal unique inherited skills only.

Current slot scale:

```text
1-6 skills   -> 1 slot
7-9 skills   -> 2 slots
10-13 skills -> 3 slots
14-18 skills -> 4 slots
19-23 skills -> 5 slots
24+ skills   -> 6 slots
```

Sacrificial fusion bonus slots are not added here. `FusionPlanFactory` adds that
bonus and caps the result.

## State And Mutation

`FusionCalculator` mutates only its private lookup table during construction and
uses its private `Random` instance during result prediction and skill mutation.

It does not mutate combatants, party state, stock, economy, or Compendium state.

## Invariants And Safety Rules

- Calculation must remain side-effect free for gameplay state.
- Recipe lookup must remain commutative.
- Mitama override must happen before generic recipe handling.
- Accident choice is part of prediction, but accident skill selection happens
  later after confirmation.
- Exclusive skills must not enter the legal inheritance pool.

## Data Dependencies

- `fusion_table.json` through `Database.FusionRecipes`.
- `entity_database.json` through `Database.Personas`.
- `skills_database.json` through `Database.Skills`.
- `MoonPhaseSystem.CurrentPhase` is passed in by callers rather than read here.

## Failure / Cancel / Edge Behavior

The calculator expresses failed combinations as:

```csharp
(FusionOperationType.NoFusionPossible, null, false)
```

It does not own user-facing cancel behavior. The conductor and bridge decide
whether to retry, return to selection, or exit the Cathedral menu.

## Tests And Verification

Relevant indirect coverage lives in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../Convergence.Tests/FusionBugRegressionTests.cs).

Important manual smoke checks:

- normal binary fusion chooses the expected result race,
- Element pairings rank up or down the correct non-Element parent,
- Mitama pairings produce stat boost instead of normal fusion,
- Mitama plus Mitama yields no result,
- inheritance excludes exclusive skills.

## Refactor Notes

This class should eventually receive two seams:

- injectable RNG for deterministic tests,
- repository/read-only data access instead of static `Database`.

Those seams should be introduced before moving Fusion into a future
`Convergence.Core` project.
