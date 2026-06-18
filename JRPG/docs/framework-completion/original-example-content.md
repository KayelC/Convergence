# Problem: Original Example Content

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

## Decisions Still Needed

- How flavorful should the neutral example pack be?
- Should the example content remain in `Data/Jsons`, or eventually move to a separate samples folder/package?
- Should placeholder examples be versioned as framework samples or test fixtures?
- Which sample behaviours should the future interactive clean demo expose first?

## Recommended Next Step

Use the expanded Training Annex as the reviewable framework sample pack.

The next implementation step should be a clean, non-legacy consumer that uses more of this sample content directly:

1. exercise `mixed_drill` and `shell_check`;
2. use `cleanse_drop`, `revival_pin`, and `focus_tea`;
3. demonstrate `focus_call`, `soften_guard`, and `toxin_touch`;
4. prove shop/equipment/negotiation/fusion sample records through small host flows when those presentation paths are ready.
