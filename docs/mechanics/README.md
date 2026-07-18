# Mechanics And Player Rules

This section explains the player-visible rules that Convergence can provide. It is also the developer-facing authority for deciding which layer owns a rule.

Convergence is modular. A game may omit an optional module, replace an injected policy, or present the same result through a console, Godot scene, menu, map trigger, or another host. These documents therefore use three labels:

- **Framework rule:** behavior enforced by reusable Framework code.
- **Configured rule:** behavior selected by authored content, registrations, or an injected policy.
- **Host responsibility:** input, presentation, storage, scheduling, or composition performed by the game.

The documents describe the current supported contracts, not every possible JRPG design. DemoHost and Training Annex are examples rather than mandatory game rules.

Documentation maturity is tracked per capability and audience. Actor,
progression, party/roster, and typed action/effect rules have completed
collaborative review. The reviewed Order 1 scope includes the stat-modifier
family as used by typed effects; other capability entries remain
`existing_unreviewed` until they complete the process in the
[Documentation Design Pattern](../documentation-design-pattern.md).

## Rule Index

1. [Actors, Stats, Resources, And Progression](actors-progression-and-resources.md)
2. [Actions, Targeting, And Effects](actions-targeting-and-effects.md)
3. [Combat, Defenses, And Turn Economy](combat-defenses-and-turns.md)
4. [Ailments, Statuses, Passives, And Knowledge](status-passives-and-knowledge.md)
5. [Party, Rosters, Inventory, Equipment, And Economy](party-inventory-and-economy.md)
6. [Navigation, Dungeons, Encounters, Negotiation, And Rewards](world-encounters-and-rewards.md)
7. [Fusion, Inheritance, Acquisition, And Compendium](fusion-acquisition-and-compendium.md)
8. [Saving, Loading, And Suspend Saves](saving-loading-and-suspend.md)
9. [Stat Modifier Policies](stat-modifier-policies.md)

## Reading A Result

Framework services normally return an immutable result containing:

- the original state;
- the resulting state when accepted;
- a stable success or rejection code;
- ordered events or diagnostics;
- affected runtime or content IDs.

The host applies or presents accepted results. Rejected operations preserve the original state. Display names and descriptions may be shown to a player, but Framework behavior is selected by typed IDs and definitions rather than text matching.

## Optionality

Navigation, traversal, Action Token, ailments, passives, party rosters, economy, negotiation, fusion, Compendium, and persistence are optional modules. A developer enables a module by composing its service and supplying the required policy or content. No Moon Phase mechanic is required; a nullable moon-phase ID exists only for games that choose to use one.
