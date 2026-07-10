# Problem: Fusion Independence

## Current State

The framework has fusion services for:

- inheritance policy evaluation;
- fusion result planning;
- transaction assessment;
- explicit accident, mutation, slot, sacrifice, catalyst/result, and compatibility policies;
- Compendium snapshots and recall checks.

The Training Annex clean host currently proves a tiny original-content fusion path: clean result calculation, inheritance planning, non-mutating preview confirmation, duplicate-result rejection, one atomic parent-consume/result-add transaction, and an explicitly selected neutral strategy policy set. This is still a sample proof, not a final game fusion design.

`JRPG.Framework` does not prescribe Moon Phase gates, a fixed sacrifice bonus, a fixed inheritance table, catalyst race names, or mutation/accident odds. Hosts supply a `FusionPolicyRegistry`. The old prototype rules are isolated in the console-only `LegacyFusionStrategyPolicies` compatibility configuration.

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
- Does the final game opt into accidents and mutation, and which registered policies should it use?
- Which inheritance-slot and sacrifice policies should the final game select?
- Does the final game need a typed catalyst/stat-boost policy at all?

## Recommended Next Step

Do not build a full fusion chart yet.

Complete the separate Compendium proof in Phase 7-35 without expanding the recipe chart. Keep future fusion policies concept-first and explicitly detached from ATLUS race charts, demon names, and spell families.
