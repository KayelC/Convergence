# Problem: Fusion Independence

## Current State

The framework has fusion services for:

- inheritance policy evaluation;
- fusion result planning;
- transaction assessment;
- accidents and mutation hooks;
- Compendium snapshots and recall checks.

The Training Annex clean host currently proves a tiny original-content fusion path: clean result calculation, inheritance planning, non-mutating preview confirmation, duplicate-result rejection, and one atomic parent-consume/result-add transaction. This is still a sample proof, not a final game fusion design.

The console Cathedral still uses legacy datasets, live `Combatant` / `Persona` objects, legacy fusion adapters, and existing presentation flows.

## Problem

Fusion is especially sensitive because the legacy reference model resembles games the framework should not copy.

The framework needs generic fusion concepts, not ATLUS-style demon charts or private game lore.

## Needed Data

Minimum neutral fusion examples:

- two parent entity selectors;
- one direct result recipe;
- one race/rank-offset concept;
- one passive inheritance example;
- one active skill rejection example;
- one owner-exclusive rejection example if needed;
- one Compendium registration/recall sample.

Safe placeholder structure:

```text
sample_wisp + sample_brute -> sample_shell
sample_spirit + sample_beast -> sample_construct rank operation
```

## Decisions Still Needed

- Does the target game even use fusion as a core system?
- If yes, is fusion species-based, class-based, material-based, ritual-based, or something else?
- Should recipes be explicit, formula-driven, or host-provided?
- Should fusion accidents exist in the framework sample, or remain optional policy tests?
- How should fusion inheritance limits be chosen by a host?

## Recommended Next Step

Do not build a full fusion chart yet.

Add only a tiny neutral fusion sample when a clean fusion demo or test needs it. Keep it concept-first and explicitly detached from ATLUS race charts, demon names, and spell families.
