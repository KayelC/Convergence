# Fusion Bridges

Source folder: [`Logic/Fusion/Bridges`](../../../../Logic/Fusion/Bridges)

This folder contains the console-facing Cathedral bridge and Fusion result
contract types.

Detailed file docs:

- [CathedralUIBridge](CathedralUIBridge.md)
- [FusionBridgeResults](FusionBridgeResults.md)

## Current Responsibility

The bridge translates player-facing menu input into typed Fusion workflow
results. It still renders console UI through `IGameIO`, but it no longer forces
the conductor to interpret raw menu strings, sentinel integers, or ambiguous
`null` values for the migrated Cathedral flows.

## Review Focus

- menu result contracts,
- cancel/back/unavailable semantics,
- disabled option handling,
- skill inheritance selection,
- ritual confirmation states,
- Compendium recall and registration selection.
