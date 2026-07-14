# Fusion Rules

Source folder: [`Logic/Fusion/Rules`](../../../../Logic/Fusion/Rules)

This folder contains shared Fusion rule helpers that are not UI-specific and do
not directly commit transactions.

Detailed file docs:

- [FusionOwnershipRules](FusionOwnershipRules.md)

## Current Responsibility

`FusionOwnershipRules` centralizes duplicate-result ownership checks so the
Cathedral UI and final mutator guard do not drift apart.

## Review Focus

- Operator demon ownership detection,
- Wild Card Persona ownership detection,
- duplicate disabled reason text,
- transaction abort message text,
- direct fusion-result pre-check limits.
