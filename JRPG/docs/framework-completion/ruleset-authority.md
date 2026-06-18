# Problem: Ruleset Authority

## Current State

The framework has ruleset definitions and a binding resolver for standard policy IDs.

The current clean Training Annex slice binds standard rulesets for damage, rewards, growth, stats, Press Turn, stock capacity, economy, and moon phase.

Moon Phase is now flagged as a design concern rather than a settled baseline. It should become an optional host capability, not a required framework pillar. See [Optional Mechanics](optional-mechanics.md).

Many runtime services still rely on named default configurations rather than fully authored ruleset parameters.

## Problem

Rulesets should eventually describe what policy a host/content pack chooses, but they should not silently hide gameplay changes.

## Needed Data

Generic ruleset examples:

- `standard_damage`;
- `standard_rewards`;
- `standard_growth`;
- `standard_stats`;
- `standard_press_turn`;
- `standard_stock_capacity`;
- `standard_economy`;
- `standard_moon_phase` only when a game deliberately opts into moon/cycle mechanics.

Optional future examples:

- `simple_damage`;
- `no_press_turn`;
- `fixed_rewards`;
- `low_growth`;

## Decisions Still Needed

- Which numeric knobs belong in authored rulesets?
- Which policies must remain code-owned?
- Should rulesets be per-pack, per-save, per-difficulty, or host-selected?
- How are incompatible rulesets reported to hosts?
- Which mechanics are optional host capabilities rather than baseline rulesets?

## Recommended Next Step

Keep standard rulesets conservative.

Only add a new authored parameter when there is a clear test proving it changes exactly one intended behavior.

Do not make sample content bind optional mechanics merely to satisfy current APIs. If a clean runtime path needs placeholder world state, prefer decoupling that API over deepening the placeholder.
