# Fusion Module Technical Documentation

This folder documents `Logic/Fusion`, the Cathedral-style Fusion subsystem.
The current Fusion implementation supports normal fusion, sacrificial fusion,
rank mutation, Mitama stat boosts, fusion accidents, skill inheritance,
Compendium registration, and Compendium recall.

## Source Map

Detailed root-file docs:

- [CompendiumRegistry](CompendiumRegistry.md)
- [FusionCalculator](FusionCalculator.md)
- [FusionConductor](FusionConductor.md)
- [FusionContext](FusionContext.md)
- [FusionMutator](FusionMutator.md)

Mirrored subfolders:

- [Bridges](Bridges/README.md)
- [Messaging](Messaging/README.md)
- [Planning](Planning/README.md)
- [Preview](Preview/README.md)
- [Rules](Rules/README.md)
- [Strategies](Strategies/README.md)
- [Transactions](Transactions/README.md)

This folder is complete for the current `Logic/Fusion` source layout as of this
pass. Future code changes should update the matching technical document in the
same commit or follow-up documentation commit.

## Runtime Flow

At runtime, `FusionConductor` owns the Cathedral interaction loop. It asks
`CathedralUIBridge` for player choices, uses `FusionPlanFactory` and
`FusionCalculator` to prepare a ritual, asks `FusionPreviewFactory` to stage a
non-mutating preview, then passes a `FusionContext` to `FusionMutator` when the
player confirms.

`FusionMutator` is the transaction gate. It blocks duplicate create-results,
dispatches to an `IFusionStrategy`, and finalizes Compendium recall. Strategy
classes perform the actual party, stock, Persona, and stat mutations.

Current root flow in code form:

```csharp
FusionMainMenuResult choice = _uiBridge.ShowCathedralMainMenu(MoonPhaseSystem.CurrentPhase);

if (choice.Kind == FusionMenuResultKind.Back) return;

switch (choice.Action)
{
    case FusionMainMenuAction.BinaryFusion: PerformFusionRitual(isSacrificial: false); break;
    case FusionMainMenuAction.SacrificialFusion: PerformFusionRitual(isSacrificial: true); break;
    case FusionMainMenuAction.BrowseCompendium: HandleCompendiumRecall(); break;
    case FusionMainMenuAction.RegisterDemon: HandleRegistration(); break;
}
```

The technical docs in this folder should therefore be read as a call chain:

```text
FusionConductor
  -> CathedralUIBridge
  -> FusionPlanFactory
      -> FusionCalculator
  -> FusionPreviewFactory
  -> FusionMutator
      -> FusionStrategyRegistry
      -> IFusionStrategy implementation
      -> FusionInventoryTransaction
```

## Design Direction

Fusion is now refactor-ready, not engine-agnostic yet. It still depends on the
console bridge, static `Database`, and current entity model, but its major
responsibilities are separated enough to support later framework extraction.

Future passes should document one subfolder at a time before making another
large refactor.
