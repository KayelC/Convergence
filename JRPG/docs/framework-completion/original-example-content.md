# Problem: Original Example Content

## Current State

The framework has clean content contracts and one small original slice: `convergence.training_annex_slice`.

That slice proves the pipeline works, but it is intentionally tiny:

- one playable actor;
- one enemy;
- one race;
- a few skills;
- one recovery item;
- one encounter;
- one small dungeon;
- standard rulesets.

## Problem

The framework cannot feel independent while most behavior is demonstrated by either:

- legacy prototype data; or
- tiny technical demo packs.

The project needs neutral, open-source-safe example content that exercises framework concepts without becoming the owner's private game setting or copying ATLUS material.

## Needed Data

Use generic placeholders:

- races: `sample_spirit`, `sample_beast`, `sample_construct`;
- entities: `sample_runner`, `sample_wisp`, `sample_brute`, `sample_shell`;
- skills: physical, elemental, healing, cure, ailment, buff, debuff, passive boost;
- items: tonic, cure item, revive item, escape token, key token;
- equipment: one weapon, armor, boots, accessory;
- encounters: one solo, one mixed, one fixed stronger encounter;
- dungeon: tiny multi-floor test area;
- rulesets: standard policies already supported.

## Decisions Still Needed

- How flavorful should the neutral example pack be?
- Should the example content remain in `Data/Jsons`, or eventually move to a separate samples folder/package?
- Should placeholder examples be versioned as framework samples or test fixtures?

## Recommended Next Step

Expand the Training Annex into a slightly richer neutral example pack before writing broad migration code.

Keep the first expansion small:

1. add one ailment skill and ailment definition;
2. add one buff or debuff skill;
3. add one more enemy;
4. add one more encounter;
5. update the clean Training Annex demo to prove the new behavior.
