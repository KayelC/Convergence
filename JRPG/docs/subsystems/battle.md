# Battle Subsystem

> **Status: Current implementation reference.** This chapter describes legacy battle execution; the Skill System GDD defines the redesign target.

## Purpose

`Logic/Battle` implements encounters: action choice, enemy AI, skill and item execution, Press Turn icon economy, damage, affinities, ailments, passives, negotiation, rewards, and knowledge discovery.

## Key Classes And Responsibilities

- `BattleConductor`: root encounter orchestrator.
- `ActionProcessor`: executes attacks, skills, items, persona swaps, and analysis.
- `CombatMath`: console compatibility facade for framework-owned production combat policies.
- `ProductionCombatRuleset`: framework policy for damage, hit/evasion, criticals, instant death, initiative, EXP, Macca, affinity multipliers, guard, rigid-body, charge, drain, and reflection math.
- `LegacyCombatPolicyAdapter`: console adapter that translates live `Combatant`/`Persona` state into clean ruleset requests.
- `PressTurnEngine`: SMT-style full/blinking turn icon state machine.
- `StatusRegistry`: ailments, cures, passives, turn-start restrictions, buffs/debuffs, redundancy checks.
- `BehaviorEngine`: AI action and target selection.
- `BattleKnowledge`: affinity discovery memory.
- `NegotiationEngine`: demon conversation, demands, and recruitment outcomes.
- `InteractionBridge`: player-facing battle menus and target/skill/item selection.
- `BattleEffectRegistry` and `IBattleEffect` implementations: category-specific skill and item effects.
- `BattleMessenger` and `BattleLogger`: battle event publication and console rendering.

## Main Runtime Flows

### Encounter Start

`BattleConductor.StartBattle` announces enemies, calculates average agility for both sides, rolls initiative, applies initial Auto-Kaja passives to the side that acts first, refreshes the HUD, and enters the phase loop.

### Phase Loop

Each phase:

1. Builds the current live actor list.
2. Resets per-phase swap flags.
3. Starts turn icons with one full icon per live actor.
4. Iterates actors while icons remain.
5. Processes ailments and restrictions at turn start.
6. Runs player input or AI behavior.
7. Applies action results and consumes Press Turn icons.
8. Checks encounter completion before flipping sides.

### Action Execution

`ActionProcessor` centralizes action costs and dispatch.

- Attacks route through a damage effect based on weapon element.
- Skills parse HP/SP costs, apply Arms Master/Spell Master reductions, spend resources, and dispatch to an effect strategy by category.
- Items dispatch by item type, with special handling for `Traesto Gem`.
- Analysis records every affinity for a target in `BattleKnowledge`.
- Persona swaps preserve current HP/SP as flat values but cap them to new maxima.

### Press Turn Outcomes

`PressTurnEngine` enforces these rules:

- Weakness or critical: convert one full icon to blinking if possible, otherwise consume a blinking icon.
- Normal action: consume a blinking icon first, otherwise a full icon.
- Pass: full becomes blinking; blinking is consumed.
- Miss or null: consume two icons.
- Repel or absorb: terminate the phase.

### Battle End

`BattleConductor` resolves win/loss/escape, rewards, recruitment side effects, compendium auto-registration, and battle-state cleanup. It also unsubscribes the logger from the messenger.

## Important State And Invariants

- `BattleEnded`, `PlayerWon`, `Escaped`, and `TraestoUsed` are encounter-level conductor flags.
- Active party state comes from `PartyManager`; enemies are a local list owned by the conductor.
- `_sessionRecruitedIds` prevents repeated recruitment of the same entity in a single encounter.
- `BattleKnowledge` persists across battles for the session.
- `StatusRegistry.ProcessTurnStart` is the authority for ailment-driven action restrictions.
- `CombatMath.GetEffectiveAffinity` delegates to the clean resolver through the legacy adapter. Shields precede breaks and affinity; Almighty/None normalize to Normal; guarding normalizes Weak; rigid-body physical states normalize physical resistance.

## Data Dependencies

- Skills and item effects are driven by `Database.Skills` and `Database.Items`.
- Ailment definitions are driven by `Database.Ailments`.
- Enemy combatants are hydrated from `Database.Personas`.
- Negotiation uses `Database.NegotiationQuestions`.
- Rewards use enemy levels/stats through `CombatMath`, which delegates to `ProductionCombatRuleset`.

## Extension Points

- Add a new skill category by implementing `IBattleEffect` and registering it in `BattleEffectRegistry`.
- Add new ailment behavior in `StatusRegistry`, and update `status_ailments.json`.
- Add new AI behavior in `BehaviorEngine` after rule support exists in processors/effects.
- Add new battle UI commands in `InteractionBridge` and handle them in `BattleConductor.ExecuteAction`.
- Add new battle messages through `IBattleMessenger` rather than direct console calls.

## Caveats

- Battle has several string-driven checks for skill names, effects, categories, and passive names.
- The current build has nullable warnings in battle messages, bridge returns, and some action paths.
- There is no automated test harness for Press Turn edge cases, ailments, or AI decisions.
