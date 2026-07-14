# Fusion Transactions

Source folder: [`Logic/Fusion/Transactions`](../../../../Logic/Fusion/Transactions)

This folder contains shared transaction helpers used by Fusion strategies.

Detailed file docs:

- [FusionInventoryTransaction](FusionInventoryTransaction.md)

## Current Responsibility

`FusionInventoryTransaction` centralizes repeated consume and replace operations
for demons and Personas. It keeps active party, demon stock, Persona stock,
controller fields, party slots, and resource recalculation consistent across
strategies.

## Review Focus

- demon consumption,
- Persona consumption,
- active demon replacement,
- stock replacement,
- active Persona replacement,
- unified stock model assumptions.
