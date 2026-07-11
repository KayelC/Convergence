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

CodeReview-7-1 establishes the clean recipe boundary: schema v1 is explicitly binary, parent selectors retain entity/race kind through runtime resolution, mixed selector recipes are legal, and structured operations are authoritative. The legacy result token is now optional compatibility data and clean catalog recipes do not generate one.

CodeReview-7-2 establishes the clean recall identity boundary. Recall and stock transitions consult one complete party/stock ownership graph, reject cross-role runtime-ID reuse before mutation, and validate persisted reference/entity consistency. The deliberate active-party plus Demon-stock overlap remains explicit and tested.

CodeReview-7-3 establishes the clean fusion transaction boundary. The framework prepares consumption, placement, result ownership, and optional retained stat-boost state from a validated inheritance token and injected stock policy, then constructs the catalog result actor only after host confirmation. Typed Demon/Persona ownership, duplicate-participant rejection, learned/equipped skill preservation, stale-state rejection, actor failure, and rollback are framework outcomes; menus and identity generation remain host concerns.

CodeReview-7-4 establishes the strategy-context lifecycle. A planning result retains its immutable host/session context, accident mutation reuses that same context for every selected skill, and standalone slot calculation offers separate contextual and deliberately context-free entry points.

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

Phase 7-35 completed the separate clean Compendium proof without expanding the recipe chart. CodeReview-7-1 preserves typed recipes, CodeReview-7-2 enforces recall identity, CodeReview-7-3 centralizes commit coordination, and CodeReview-7-4 retains strategy context. Before Phase 8 begins, resolve CodeReview-7-5 in `docs/phase-7-code-review.md` by hardening Compendium entry/save validation. Keep future fusion policies concept-first and explicitly detached from ATLUS race charts, demon names, and spell families.
