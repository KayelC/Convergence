# Fusion Preview

Source folder: [`Logic/Fusion/Preview`](../../../../Logic/Fusion/Preview)

This folder contains non-mutating preview construction.

Detailed file docs:

- [FusionPreviewFactory](FusionPreviewFactory.md)

## Current Responsibility

The preview factory creates staged combatants that represent what the player is
about to receive. These previews must match execution math closely enough for
the UI to be trustworthy while never mutating real party, stock, sacrifice, or
Compendium state.

## Review Focus

- standard result preview,
- Mitama stat boost preview,
- rank mutation preview,
- sacrificial EXP preview,
- inherited skill application,
- level and stat projection rules.
