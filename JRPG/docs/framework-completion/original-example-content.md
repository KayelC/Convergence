# Problem: Original Example Content

> **Status: Initial content pack completed.** The framework now has a neutral, original Training Annex sample pack. Future work should exercise more of that pack through clean runtime consumers rather than keep treating the pack itself as missing.

## Current State

The framework has clean content contracts and one small original slice: `convergence.training_annex_slice`.

That slice now proves a broader neutral sample surface:

- three races;
- five entities;
- ten skills covering damage, recovery, curing, ailment, buff/debuff, and passive recovery;
- three ailments;
- five items;
- four equipment records;
- one shop;
- one negotiation set;
- three encounters;
- one small dungeon with random, fixed battle, safe-room, and barrier floors;
- two concept-level fusion recipes;
- standard rulesets plus registered placeholder fusion policy IDs.

## Problem

The framework cannot feel independent while most behavior is demonstrated by either:

- legacy prototype data; or
- tiny technical demo packs.

The project needs neutral, open-source-safe example content that exercises framework concepts without becoming the owner's private game setting or copying ATLUS material.

The Training Annex sample is the first answer to that problem. It is deliberately plain and concept-driven; it should remain easy to inspect and safe to replace.

This problem is no longer blocked on creating a generic reference pack. The remaining issue is coverage: not every sample record is exercised by a clean runtime/demo path yet. Phase 4-24 exercises the sample shop and equipment purchase path through `--clean-training-annex-play`, Phase 4-25 exercises framework hospital restoration through the clean Recovery Facility, and Phase 5-29 exercises the `steady_sample` negotiation/recruitment path through clean framework services.

## Current Sample Data

The active sample pack uses generic placeholders:

- races: `annex_spirit`, `annex_beast`, `annex_construct`;
- entities: `echo_adept`, `ashling`, `bramble_runner`, `ward_shell`, `annex_mentor`;
- skills: `echo_strike`, `frost_tip`, `mend`, `clear_toxin`, `focus_call`, `soften_guard`, `toxin_touch`, `ash_spark`, `shell_bash`, `steady_breath`;
- ailments: `sample_poison`, `sample_sleep`, `sample_stun`;
- items: `annex_tonic`, `focus_tea`, `cleanse_drop`, `revival_pin`, `training_badge`;
- equipment: `practice_blade`, `padded_jacket`, `light_steps`, `focus_charm`;
- shop: `training_supply`;
- negotiation: `steady_sample`;
- encounters: `ashling_drill`, `mixed_drill`, `shell_check`;
- dungeon: `training_annex`;
- fusion recipes: `ashling_bramble_shell`, `spirit_beast_construct_rank`;
- rulesets: standard policies already supported by the framework.

The dungeon records are sample content, not a mandate that production exploration must start encounters on every floor transition. Fixed battle floors are useful for scripted events and regression tests. Production-facing hosts, especially Godot, should be able to start encounters from visible enemy entities, trigger volumes, patrols, or scripted scene events.

The first bridge for that exists now: `CatalogEncounterStartPlanner` lets a host-owned trigger choose an encounter ID and receive battle actor creation requests without placing scene objects inside the framework.

## Decisions Still Needed

- How flavorful should the neutral example pack be?
- Should the example content remain in `Data/Jsons`, or eventually move to a separate samples folder/package?
- Should placeholder examples be versioned as framework samples or test fixtures?
- Which sample behaviours should the future interactive clean demo expose first?

## Recommended Next Step

Use the expanded Training Annex as the reviewable framework sample pack.

The next implementation step should be a clean, non-legacy consumer pass that uses more of this sample content directly:

1. keep exercising `mixed_drill`, `shell_check`, `cleanse_drop`, `revival_pin`, `focus_tea`, `focus_call`, `soften_guard`, and `toxin_touch` through clean tests and demos;
2. grow the host-owned encounter-start proof toward a small interactive scene/trigger loop;
3. prove fusion sample records through a small host flow when that design is ready.
