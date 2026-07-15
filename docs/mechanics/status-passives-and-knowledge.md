# Ailments, Statuses, Passives, And Knowledge

## Ailments And Status State

Ailments are content records identified by `ContentId`. Their behavior is authored explicitly through turn behavior, resistance, duration, recovery, modifiers, triggers, and exclusivity groups.

**Framework rule:** ailment behavior is never inferred from the ailment's name. A game can author poison-like damage, sleep-like recovery, action restrictions, forced actions, fleeing, roster recall, or custom behavior without reserving a display label.

An exclusivity group can enforce that only one major ailment in that group is active. Other groups may coexist if content and policy allow it.

## Turn Restrictions

The lifecycle can return these typed turn-start outcomes:

- can act normally;
- skip the turn;
- use a limited action set;
- use a forced physical action;
- use a forced confusion action;
- flee the battle;
- recall to roster.

Manual and automated encounters consume these outcomes through the same command path. AI does not bypass an authored restriction.

## Duration Kinds

Timed state can expire by actor turns, phase ticks, battle cleanup, or another authored duration contract. A duration can suspend while the actor is in reserve. The runtime tracks and validates ailment, stat-stage, charge, shield, Break, and affinity-override durations.

Reserve suspension prevents configured turn-end effects and ticking while the actor is not deployed. Cleanup scopes let a host request battle-end, swap, or field-transition removal without parsing status names.

## Passive Skills

Passive skills are ordered catalog definitions attached to an actor's `BattlePassiveCollection`. Active skills cannot enter this collection, and duplicate passive IDs are rejected.

Passives may provide triggers and rule modifiers. Trigger execution order is loadout, trigger, target, then effect order. The passive owner is the condition actor; event-selected actors are targets.

Recursive activation of the same trigger is suppressed unless the registered event policy permits re-entry. Per-battle activation limits are tracked by actor, passive skill, and trigger. Enabling, disabling, adding, or removing a passive takes effect immediately.

Numeric rule modifiers resolve as:

`(base + sum of additions) * product of multipliers`

Affinity and ailment-resistance replacements use typed precedence instead of numeric text conventions.

## Battle Knowledge

Knowledge is separated into elemental affinity, ailment resistance, and instant-death resistance stores. The keys include the known entity ID and the relevant element, ailment, or channel, so the three domains cannot collide.

Almighty discoveries are ignored because Almighty always resolves normally.

**Framework capability:** knowledge snapshots can be persisted or scoped to an encounter. The intended player model demonstrated by Training Annex is:

- enemy AI starts ordinary encounters with fresh encounter knowledge and learns only during that encounter;
- player knowledge persists between encounters when the host includes it in saves;
- analysis and observed outcomes can reveal typed defenses;
- familiar entities registered in the Compendium can import their authored defenses into player knowledge;
- AI knowledge and player knowledge are separate stores.

Bosses or special encounters may receive preloaded knowledge because the host owns encounter composition.

## Presentation

A Godot host can use player knowledge to annotate target cursors, element icons, analysis panels, or bestiary pages. The host reads typed knowledge entries. It does not infer a weakness from damage text or an animation.
