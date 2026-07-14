# Entities Subsystem

> **Status: Current implementation reference.** This chapter describes live prototype entities and may differ from future immutable content contracts.

## Purpose

`Entities` defines live gameplay actors and their attached progression, stats, combat state, equipment, persona/demon stock, and skill state. It also contains component-style processors that keep heavy calculations out of the entity data shells.

## Key Classes And Responsibilities

- `Combatant`: universal actor type for player, humans, operators, demons, enemies, and party members.
- `Persona`: live persona/demon mask data containing affinities, stat modifiers, skill set, learned skills, level, EXP, and race/rank metadata.
- `CombatantFactory`: hydrates enemies and allied demons from `PersonaData`.
- `StatProcessor`: compatibility facade for framework stat composition from class, character stats, persona influence, accessories, and buffs/debuffs.
- `GrowthProcessor`: compatibility facade for framework EXP, level-ups, stat points, resource recalculation, and stat rollback.
- `PartyManager`: compatibility facade for framework active/reserve party and demon-stock transitions.
- `DamageHandler`: applies damage to a target, resolving guard, affinities, rigid-body criticals, absorb/repel/null, and ailment removal triggers.

## Main Runtime Flows

### Actor Creation

Enemies and allied demons are created through `CombatantFactory`.

- Enemy creation accepts direct IDs or `E_`-prefixed IDs, attaches a persona, scales it to level, grants all eligible template skills, and uses enemy resource scaling.
- Allied demon creation uses the persona template, scales to target level, and uses higher allied resource scaling.
- Missing templates produce a fallback `Glitch` combatant.

### Stat Calculation

`Combatant.GetStat` delegates to `StatProcessor`, which now delegates to framework progression policies through the console-owned legacy adapter.

- Demons use `ActivePersona.StatModifiers` at full value.
- Operators use character stats and accessories, with no persona stat influence.
- Persona Users and Wild Cards use character stats plus weighted persona influence.
- Raw stats are capped at 40 before battle buff/debuff multipliers.

### Growth

`GrowthProcessor.GainExp` delegates to framework level-growth policies through the console-owned legacy adapter. The preserved behavior still adds lifetime and current EXP, loops through level-ups using `1.5 * Level^3` requirements, gives humanoids randomized base HP/SP on level-up, and leaves demons without those base-resource rolls.

### Damage Application

Battle effects calculate raw damage first, then `DamageHandler.ApplyDamage` applies target-side interactions:

- guarding damage reduction and weakness suppression
- physical auto-critical against rigid-body ailments
- affinity damage multipliers and special outcomes
- HP mutation
- ailment removal triggers such as waking on hit

## Important State And Invariants

- `Combatant.ActivePersona` is central for demons and persona users.
- `CharacterStats` represent base allocated stats before persona influence.
- `Buffs` stores Kaja/Nda-style tracks using keys such as `PhysAtk`, `MagAtk`, `Defense`, and `Agility`.
- `BrokenAffinities` stores temporary elemental break state.
- `PersonaStock` and `DemonStock` live on the owning `Combatant`.
- The unified demon stock model keeps active demons in `DemonStock`; `ActiveParty` holds references to deployed demons. Track F now preserves this through framework transition results applied back to the same live object references.
- `ClearTransientBattleState`, `ClearEncounterPersistence`, and `CleanupBattleState` distinguish between stance/shield cleanup and encounter-wide state cleanup.

## Data Dependencies

- `CombatantFactory` requires `Database.Personas`.
- Skill consolidation depends on `ActivePersona.SkillSet` plus `ExtraSkills`.
- Affinity lookup depends on persona affinity maps built from `PersonaData`.
- Equipment and accessory stats depend on data DTOs loaded into `Database`.

## Extension Points

- Add new actor state to `Combatant` only when it truly applies across live battle/field systems.
- Add new stat formulas to the framework progression policy/configuration, then expose them through `StatProcessor` only as a compatibility adapter.
- Add new progression rules to the framework progression policy/configuration, then expose them through `GrowthProcessor` only as a compatibility adapter.
- Add new party or stock transition rules to the framework party/stock service, then expose them through `PartyManager` only as a compatibility adapter.
- Add new target-side damage interactions in `DamageHandler` if they affect all offensive actions.
- Add factory variants if enemy/allied/player actor creation diverges further.

## Caveats

- `Combatant` is intentionally broad, so unrelated systems can appear adjacent in the same class.
- The protected `CompendiumRegistry` deep-clones legacy `ActivePersona` state. The clean Phase 7-35 path stores immutable catalog-identified progression/stat/skill data and reconstructs a fresh runtime actor on recall; neither path retains a live actor reference inside its Compendium entry.
- Some ownership and identity checks compare names or source IDs; use canonical lowercase IDs where possible.
