# FusionPreviewFactory

Source: [`Logic/Fusion/Preview/FusionPreviewFactory.cs`](../../../../Logic/Fusion/Preview/FusionPreviewFactory.cs)

## Purpose

`FusionPreviewFactory` creates staged, non-mutating combatants for the ritual
confirmation screen. Its previews should mirror execution math closely enough
that the player can trust the projected result.

## Class Shape

```csharp
public sealed class FusionPreviewFactory
{
    public Combatant? CreatePreview(FusionPlan plan, IReadOnlyList<string> chosenSkills)
    public Combatant? CreatePreview(...)
    public static Combatant GetStatBoostTarget(Combatant parentA, Combatant parentB)
    public static Combatant GetStatBoostMitama(Combatant parentA, Combatant parentB)
}
```

## `CreatePreview(FusionPlan, chosenSkills)`

Delegates the plan payload into the fuller overload:

```csharp
return CreatePreview(
    plan.Operation,
    plan.TargetId,
    plan.FirstParent,
    plan.SecondParent,
    plan.Sacrifice,
    chosenSkills);
```

## `CreatePreview(...)`

Template lookup:

```csharp
if (!Database.Personas.TryGetValue(targetId.ToLower(), out PersonaData? template))
{
    return null;
}
```

Preview level:

```csharp
int previewLevel = operation == FusionOperationType.StatBoostFusion
    ? GetStatBoostTarget(parentA, parentB).Level
    : template.Level;
```

This is the Mitama preview fix: stat boost preview uses the target's current
level, not the base template level.

Initial staged combatant:

```csharp
Combatant staged = CombatantFactory.CreatePlayerDemon(targetId, previewLevel);
staged.ExtraSkills.Clear();
staged.ExtraSkills.AddRange(chosenSkills);
```

Stat boost preview copies target state, then applies Mitama boost:

```csharp
Combatant target = GetStatBoostTarget(parentA, parentB);
Combatant mitama = GetStatBoostMitama(parentA, parentB);

staged.Exp = target.Exp;
staged.LifetimeEarnedExp = target.LifetimeEarnedExp;
foreach (var stat in target.CharacterStats) staged.CharacterStats[stat.Key] = stat.Value;
foreach (var mod in target.ActivePersona!.StatModifiers) staged.ActivePersona!.StatModifiers[mod.Key] = mod.Value;

ApplyPreviewBoost(staged, mitama.ActivePersona!.Name);
staged.RecalculateResources();
```

Rank mutation preview carries stat modifiers from the original non-Element
parent:

```csharp
Combatant original = parentA.ActivePersona!.Race != "Element" ? parentA : parentB;
foreach (var mod in original.ActivePersona!.StatModifiers)
{
    staged.ActivePersona!.StatModifiers[mod.Key] = mod.Value;
}
staged.RecalculateResources();
```

Sacrifice preview applies EXP to the staged clone only:

```csharp
if (sacrifice != null)
{
    int transferXP = (int)(sacrifice.CombatantView.LifetimeEarnedExp / 1.5);
    staged.GainExp(transferXP);
}
```

## Static Helpers

```csharp
public static Combatant GetStatBoostTarget(Combatant parentA, Combatant parentB)
{
    return parentA.ActivePersona?.Race == "Mitama" ? parentB : parentA;
}

public static Combatant GetStatBoostMitama(Combatant parentA, Combatant parentB)
{
    return parentA.ActivePersona?.Race == "Mitama" ? parentA : parentB;
}
```

These helpers are used by planning, preview, and tests to keep Mitama parent
order handling consistent.

## `ApplyPreviewBoost`

Mitama boost table:

```csharp
case "Ara Mitama": boosts.Add(StatType.St, 2); boosts.Add(StatType.Ag, 1); break;
case "Nigi Mitama": boosts.Add(StatType.Ma, 2); boosts.Add(StatType.Lu, 1); break;
case "Kusi Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Ag, 1); break;
case "Saki Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Lu, 1); break;
```

Cap logic:

```csharp
int current = mods.GetValueOrDefault(entry.Key, 0);
mods[entry.Key] = System.Math.Min(40, current + entry.Value);
```

## State And Mutation

Only the staged preview object is mutated. Real parents, sacrifice, party,
stock, economy, and Compendium are not touched.

## Invariants And Safety Rules

- Return `null` when target template is missing.
- Do not mutate source participants.
- Stat boost preview must preserve target level and state.
- Sacrifice preview must not consume or mutate the sacrifice.
- Preview boost math should mirror `StatBoostStrategy`.

## Tests And Verification

Covered by Fusion preview tests in
[`Convergence.Tests/FusionBugRegressionTests.cs`](../../../../Convergence.Tests/FusionBugRegressionTests.cs).

Manual smoke checks:

- Mitama preview level matches target current level.
- Mitama stats preview correctly.
- Sacrificial preview shows EXP level-up without consuming sacrifice.

## Refactor Notes

Boost tables are duplicated with `StatBoostStrategy`. They should eventually be
centralized into a shared Mitama rule object before engine extraction.
