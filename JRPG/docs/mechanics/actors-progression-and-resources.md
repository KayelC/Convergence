# Actors, Stats, Resources, And Progression

## Actor Identity

**Framework rule:** every live actor has a `RuntimeInstanceId`, while its authored entity uses a `ContentId`. Multiple runtime actors may share one entity definition, but runtime instance IDs must be unique throughout the active party, forms, stocks, encounters, and saves.

An actor snapshot can contain identity, ownership, team, deployment, progression, resources, stats, skills, forms, equipment, battle status, passive activations, base resource values, vital-resource identity, and capabilities. Snapshots are immutable boundaries. Runtime actor state is mutable only through validated services or explicit state methods.

## Catalog Hydration

**Framework rule:** `CatalogBattleActorFactory` creates an actor from a qualified entity ID and runtime creation request. It loads base skills first, then all level unlocks available at the requested level in authored order. Repeated skill IDs are kept only at their first occurrence. Passive skills enter the passive collection; active skills enter the ordered action loadout.

Creation fails with typed diagnostics when the entity, level, skills, initialization, or restored state is invalid. It never substitutes a fallback actor.

## Stats

The standard stat vocabulary is Strength, Magic, Vitality, Agility, and Luck. Games may register their own typed stat IDs where the consuming policy supports them.

**Configured rule:** stat calculation belongs to `IStatResolutionPolicy`. The supplied standard policy supports base stats, equipment contributions, active-form contributions, caps, and stage modifiers. A host may bind a catalog ruleset or inject another policy.

Battle stage aliases map to typed stat tracks. A generic attack stage can affect both physical and magical offense, while defense and agility affect their corresponding calculations. Luck has no implicit buff/debuff alias unless a game adds one deliberately.

## Resources

Resources are addressed by `ContentId`; HP and SP are conventional registrations, not hardcoded presentation strings. Each resource has current and maximum values. Base resource values are retained separately so growth policies can recalculate maxima.

**Framework rule:** resource mutation is range-safe. Current values cannot remain below zero or above the maximum after an accepted operation. Recalculation policies explicitly decide whether current values are preserved, capped, or changed by the difference in maximum.

**Configured rule:** `IResourceGrowthPolicy` owns maximum-resource formulas. The supplied standard policy derives HP from base HP and Vitality and SP from base SP and Magic, with configurable or bound ruleset ownership.

## Experience And Levels

**Configured rule:** `IExperienceCurve` decides the experience required for each level. `ILevelGrowthPolicy` decides what a level grants, and `IRandomSource` supplies any random growth rolls. Framework progression services support multiple level gains from one award and return ordered level-up events.

Negative experience and invalid allocations are rejected without mutation. Stat allocation checks available points and caps before applying. Rollback restores the prior base stats and points, then recalculates resources through the same policy.

## Player-Facing Expectations

- A displayed level or stat comes from the current runtime snapshot, not from descriptive text.
- Equipment, forms, stages, and policies may change effective stats without rewriting base stats.
- Resource and growth formulas are game configuration. Training Annex values demonstrate one binding only.
- Save restoration validates numeric ranges and catalog references before a runtime actor is rebuilt.

