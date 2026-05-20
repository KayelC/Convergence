# Fusion Planning

Source folder: [`Logic/Fusion/Planning`](../../../../Logic/Fusion/Planning)

This folder contains the non-mutating ritual planning layer.

Detailed file docs:

- [FusionParticipant](FusionParticipant.md)
- [FusionPlan](FusionPlan.md)
- [FusionPlanFactory](FusionPlanFactory.md)

## Current Responsibility

Planning converts selected materials into a stable Fusion plan before the bridge
shows inheritance and preview screens. It normalizes Demon and Persona inputs,
calculates operation metadata, gathers inheritance pools, applies sacrificial
slot bonuses, and chooses the preview baseline.

## Review Focus

- Demon/Persona participant normalization,
- transient combatant creation for Personas,
- plan payload fields,
- inheritance pool construction,
- preview baseline selection.
