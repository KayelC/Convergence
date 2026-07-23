# Ailments, Statuses, Passives, And Knowledge

## Ailments And Status State

Ailments are content records identified by `ContentId`. Their behavior is authored explicitly through turn behavior, resistance, duration, recovery, modifiers, triggers, and exclusivity groups.

**Framework rule:** ailment behavior is never inferred from the ailment's name. A game can author poison-like damage, sleep-like recovery, action restrictions, forced actions, fleeing, roster recall, or custom behavior without reserving a display label.

An exclusivity group can enforce that only one major ailment in that group is active. Other groups may coexist if content and policy allow it.

Reapplication and exclusivity are selected by an ailment-transition policy.
The supplied standard refreshes the same ailment and replaces a different
ailment in the same exclusivity group. Supplied alternatives may reject
reapplication or refresh the same ailment while rejecting a different
exclusive ailment. Results distinguish first application, refresh,
replacement, and rejection and identify every affected ailment.

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

Timed state can expire by actor turns, team phases, rounds, actions, battle
cleanup, or another explicit lifecycle clock. A duration can suspend while the
actor is in reserve. Stat-modifier clock boundaries and same-boundary
application protection are defined in
[Stat Modifier Policies](stat-modifier-policies.md); their runtime migration is
tracked separately from existing ailment, charge, shield, Clean Break, and
affinity-override duration handling. Team identity, authored phase identity,
and lifecycle event identity are distinct values; the encounter composition
must map them explicitly.

Reserve state retains its exact remaining lifetime by default. A game may
instead select the supplied advancing policy for one exact owning-team phase
event or one round event. That policy never ages reserve state once per action,
and a status authored with `SuspendWhileReserve` remains suspended even under
the advancing policy. Field state does not advance unless a host deliberately
dispatches a lifecycle clock. Typed departure reasons let a host request
battle-end, deployment-swap, defeat, flee, roster-recall, or field-transition
cleanup without parsing status names.

Lifecycle results preserve typed before/after duration evidence and one typed
removal cause for each removed state. Action results carry action-end expiry;
hosts do not need to diff the actor or infer a change from effect display text.

## Passive Skills

Passive skills are ordered catalog definitions attached to an actor's `BattlePassiveCollection`. Active skills cannot enter this collection, and duplicate passive IDs are rejected.

Passives may provide triggers and rule modifiers. Trigger execution order is loadout, trigger, target, then effect order. The passive owner is the condition actor; event-selected actors are targets.

Recursive activation of the same trigger is suppressed unless the registered event policy permits re-entry. Per-battle activation limits are tracked by actor, passive skill, and trigger. Enabling, disabling, adding, or removing a passive takes effect immediately.

Lifecycle events report both executed and rejected passive evaluations with
their trigger index, event ID, typed outcome, and complete effect results.
Resource recovery is therefore not the only passive effect visible to an
event-driven host.

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
