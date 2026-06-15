# CathedralUIBridge

Source: [`Logic/Fusion/Bridges/CathedralUIBridge.cs`](../../../../Logic/Fusion/Bridges/CathedralUIBridge.cs)

## Purpose

`CathedralUIBridge` is the console-facing UI authority for the Cathedral. It
renders menus, labels, preview screens, skill details, and ritual narration, then
returns typed bridge result objects to `FusionConductor`.

It is a bridge, not a rule engine. It may know how to display disabled choices,
but the rule reasons should be supplied by planning/rules collaborators.

## Runtime Role

The conductor calls this bridge at every interactive Cathedral step:

```csharp
ShowCathedralMainMenu(...)
SelectRitualParticipant(...)
SelectInheritedSkills(...)
ConfirmRitual(...)
DisplayRitualSequence(...)
ShowCompendiumRecallMenu()
SelectDemonToRegister(...)
```

The bridge still depends on console-style `IGameIO.RenderMenu`, but it no
longer returns raw strings, magic integers, or ambiguous `null` for the migrated
Fusion flows.

## Key Collaborators

- `IGameIO`: rendering, menu selection, waits, clearing, key reads.
- `FieldUIState`: retained bridge state dependency for field UI integration.
- `CompendiumRegistry`: recall list and recall cost display.
- `Database.Skills`: skill detail text in inheritance hover callback.
- `Database.Personas`: result authority gate in ritual confirmation.
- `MoonPhaseSystem`: phase name for Cathedral header.
- `FusionBridgeResults`: typed result contracts.

## Important Methods / Members

Current class shape:

```csharp
public class CathedralUIBridge
{
    public FusionMainMenuResult ShowCathedralMainMenu(int moonPhase)
    public RitualParticipantSelectionResult<T> SelectRitualParticipant<T>(...)
    public SkillInheritanceSelectionResult SelectInheritedSkills(...)
    public RitualConfirmationResult ConfirmRitual(...)
    public void DisplayRitualSequence(bool isAccident)
    public CompendiumRecallResult ShowCompendiumRecallMenu()
    public CompendiumRegistrationSelectionResult SelectDemonToRegister(...)
}
```

### `ShowCathedralMainMenu`

Builds the Cathedral service menu and maps visible labels to typed actions:

```csharp
List<string> options = new List<string> { "Binary Fusion" };
List<FusionMainMenuAction> actions = new List<FusionMainMenuAction>
{
    FusionMainMenuAction.BinaryFusion
};

if (moonPhase == 8)
{
    options.Add("Sacrificial Fusion");
    actions.Add(FusionMainMenuAction.SacrificialFusion);
}

options.Add("Browse Compendium");
actions.Add(FusionMainMenuAction.BrowseCompendium);

options.Add("Register Demon");
actions.Add(FusionMainMenuAction.RegisterDemon);

options.Add("Back");
```

The Full Moon gate is presentation-visible here: sacrificial fusion is absent
unless `moonPhase == 8`.

Return mapping:

```csharp
int choice = _io.RenderMenu(header, options, 0);

if (choice == -1 || choice == options.Count - 1) return FusionMainMenuResult.Back;
return FusionMainMenuResult.Selected(actions[choice]);
```

`-1` is still a console menu sentinel, but it is converted immediately into a
typed `Back` result before reaching the conductor.

### `SelectRitualParticipant`

Generic participant picker for demons and Personas:

```csharp
var validChoices = pool.Where(x => !exclusions.Contains(x)).ToList();

if (!validChoices.Any())
{
    _io.WriteLine("No further candidates available for this ritual.", ConsoleColor.Red);
    _io.Wait(800);
    return RitualParticipantSelectionResult<T>.Unavailable;
}
```

Disabled entries are visible but not selectable:

```csharp
bool isDisabled = disabledReasons != null && disabledReasons.ContainsKey(item);
string disabledSuffix = isDisabled ? $" ({disabledReasons![item]})" : "";
disabledList.Add(isDisabled);
```

The conductor uses this for duplicate-result UX by passing owned-result disabled
reasons for second parent choices.

The result contract separates cancel from unavailable:

```csharp
if (choice == -1 || choice == labels.Count - 1)
    return RitualParticipantSelectionResult<T>.Canceled;

return RitualParticipantSelectionResult<T>.Selected(validChoices[choice]);
```

### `SelectInheritedSkills`

Runs the deterministic skill inheritance picker.

The method keeps local selected state:

```csharp
List<string> selected = new List<string>();

while (selected.Count < maxSlots)
{
    ...
}
```

Each skill is marked as picked, already known, exclusive, or available:

```csharp
bool isPicked = selected.Contains(skillName);
bool isAlreadyKnown = inherentSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase);
bool isExclusive = exclusivePool.Contains(skillName, StringComparer.OrdinalIgnoreCase);

string prefix = isPicked ? "[X]" : ((isAlreadyKnown || isExclusive) ? "[-]" : "[ ]");
```

Disabled selection logic:

```csharp
disabledList.Add(isPicked || isAlreadyKnown || isExclusive);
```

Confirming an empty list is valid:

```csharp
if (choice == labels.Count - 2)
{
    break;
}

return SkillInheritanceSelectionResult.Confirmed(selected);
```

Abort is distinct:

```csharp
if (choice == -1) return SkillInheritanceSelectionResult.Aborted;
if (choice == labels.Count - 1) return SkillInheritanceSelectionResult.Aborted;
```

### `ConfirmRitual`

Shows the projected result and returns the player's transaction decision.

The authority gate checks the result's base template level:

```csharp
int baseTemplateLevel = 0;
if (Database.Personas.TryGetValue(stagedDemon.SourceId.ToLower(), out var template))
{
    baseTemplateLevel = template.Level;
}

if (baseTemplateLevel > playerLevel)
{
    ...
    return RitualConfirmationResult.Forbidden;
}
```

This gate happens before the player can choose `Commence Ritual`. Sacrificial EXP
can push the staged final level higher after the base template is allowed.

Preview rendering branches by operation:

```csharp
switch (operationType)
{
    case FusionOperationType.CreateNewDemon:
        ...
        break;

    case FusionOperationType.RankUpParent:
    case FusionOperationType.RankDownParent:
    case FusionOperationType.StatBoostFusion:
        ...
        break;
}
```

Stat comparison for mutation/boost previews:

```csharp
foreach (StatType st in Enum.GetValues(typeof(StatType)))
{
    int originalVal = originalParent.GetStat(st);
    int stagedVal = stagedDemon.GetStat(st);
    ...
}
```

Return mapping:

```csharp
return choice switch
{
    0 => RitualConfirmationResult.Commence,
    1 => RitualConfirmationResult.Wait,
    _ => RitualConfirmationResult.Cancel
};
```

### `DisplayRitualSequence`

Pure presentation sequence with delays:

```csharp
_io.WriteLine("The sacrificial circle glows with a cold, blue light...");
_io.Wait(1200);
...
if (isAccident)
{
    _io.WriteLine("!!! WARNING: LUNAR INTERFERENCE DETECTED !!!", ConsoleColor.Red);
    _io.Wait(2000);
}
```

No gameplay state is changed here.

### `ShowCompendiumRecallMenu`

Gets registered entries from the Compendium and returns a typed recall result:

```csharp
var entries = _compendium.GetAllRegisteredDemons();

if (!entries.Any())
{
    ...
    return CompendiumRecallResult.Unavailable;
}
```

Menu labels include recall cost:

```csharp
int cost = _compendium.CalculateRecallCost(entry.SourceId);
labels.Add($"{entry.Name,-15} (Lv.{entry.Level}) {entry.ActivePersona?.Race} (Rk.{entry.ActivePersona?.Rank}) | {cost} M");
```

### `SelectDemonToRegister`

Filters the supplied party/stock pool down to demons:

```csharp
var demonsOnly = party.Where(c => c.Class == ClassType.Demon).ToList();

if (!demonsOnly.Any())
{
    ...
    return CompendiumRegistrationSelectionResult.Unavailable;
}
```

Cancel and selection are explicit:

```csharp
if (choice == -1 || choice == labels.Count - 1)
    return CompendiumRegistrationSelectionResult.Canceled;

return CompendiumRegistrationSelectionResult.Selected(demonsOnly[choice]);
```

## State And Mutation

This class mutates only local selection lists and console presentation state. It
does not mutate party, stock, economy, Compendium entries, or Fusion plans.

## Invariants And Safety Rules

- Convert `RenderMenu` sentinel values into typed results immediately.
- `Unavailable` must mean no legal candidates, not player cancel.
- Confirmed zero inheritance must stay different from abort.
- Disabled choices should remain visible when they teach the player why a choice
  is illegal.
- The bridge may render UI details, but transaction mutation belongs elsewhere.

## Tests And Verification

Result contracts are covered by
[`Convergence.Tests/FusionBridgeResultTests.cs`](../../../../Convergence.Tests/FusionBridgeResultTests.cs).

Manual smoke checks:

- Back from main Cathedral menu.
- Cancel participant selection.
- Confirm zero inherited skills.
- Use Wait from preview and return to inheritance.
- Confirm duplicate-result second parents are disabled.
- Empty Compendium returns unavailable behavior.

## Refactor Notes

This bridge is the first pattern for future Field and Battle bridge migrations.
The next framework step is not to remove console rendering yet, but to keep
compressing all console sentinel behavior into typed adapter-facing contracts.
