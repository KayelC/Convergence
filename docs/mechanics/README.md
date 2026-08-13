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
The Order 2 combat pages are owner-confirmed and source-reviewed through
complete-action aggregation, ordered dependencies, staged life-state checks,
secondary damage contact, and host result validation. The final pre-closure
correction review found no remaining reachable defect in that supported scope;
their coverage entries remain `reviewed` and the implementation gate is closed.
Order 3 has now reconciled the Action Token and neutral standard-actions rules,
pass precedence, phase liveness, and the boundary between opportunity counting
and actor scheduling across all three audiences.
Order 4 now has dedicated mechanics, developer, and technical lifecycle pages.
They remain pending independent source closure because documentation is not
promoted merely for existing.
Order 6 now has a dedicated encounter-loop mechanics page covering both
supplied schedulers, lifecycle ordering, cancellation, outcomes, and canonical
event evidence. O6-R13L rechecked it at that revision; O6-R14 later reproduced
repeated-defeat and zero-survivor paths that reopened its coverage entry.
O6-R15, O6-R16, O6-R19, and O6-R21 corrected and reconciled those rules.
O6-R23 independently re-read the corrected source and returned the page to
`reviewed`. O6-R24 later found that normal completion text crosses the
fault-result boundary and that the mechanics fault-cleanup sentence omits its
battle-start condition. The page was `existing_unreviewed` until O6-R25 through
O6-R27 complete. O6-R25 and O6-R26 corrected the runtime result shape and all
three audience statements; O6-R27 independently re-read the result and restored
this page to `reviewed` at that revision. O6-R33 subsequently reproduced
no-cost economy mutation and scheduler round drift, so this page returns to
`existing_unreviewed`. O6-R34 and O6-R35 corrected both runtime boundaries,
O6-R36 reconciled the page, and O6-R37 independently traced current source and
restored the page to `reviewed`.
O6-R38 later reopened the page after finding stable-ring and economy-liveness
defects. O6-R39 and O6-R40 correct those paths, and O6-R41 reconciles the page
with the exact phase-end and accepted turn-window rules. It is `reviewed` again
and the O6-R42 independent review formally closes the capability.
The later O6-R43 source audit reopens it for canonical event retention and
primary command-fault authority; the page is `existing_unreviewed` until
O6-R44 through O6-R47 are complete.

O6-R44 preserves canonical event identity when optional sink publication
fails, O6-R45 preserves the primary command fault when cleanup also fails, and
O6-R46 reconciles the rule text. The
[O6-R47 final closure review](../reviews/encounter-orchestration-order-6-r47-final-closure-review-2026-08-05.md)
independently traces the corrected implementation. This page is `reviewed`,
and Order 6 is formally complete.
Order 7 now has a reviewed player-facing page for exact-copy equipment
ownership, equipped-only grants, typed currencies, policy-shaped prices and
stock, atomic transactions, recovery, and save v19. O7-R11 remains the separate
capability-closure audit.

## Rule Index

1. [Actors, Stats, Resources, And Progression](actors-progression-and-resources.md)
2. [Actions, Targeting, And Effects](actions-targeting-and-effects.md)
3. [Combat, Defenses, And Turn Economy](combat-defenses-and-turns.md)
4. [Status And Passive Lifecycle](status-passive-lifecycle.md)
5. [Battle Knowledge](battle-knowledge.md)
6. [Encounter Rounds, Phases, And Turns](encounter-rounds-phases-and-turns.md)
7. [Party, Rosters, Inventory, Equipment, And Economy](party-inventory-and-economy.md)
8. [Navigation, Dungeons, Encounters, Negotiation, And Rewards](world-encounters-and-rewards.md)
9. [Fusion, Inheritance, Acquisition, And Compendium](fusion-acquisition-and-compendium.md)
10. [Saving, Loading, And Suspend Saves](saving-loading-and-suspend.md)
11. [Stat Modifier Policies](stat-modifier-policies.md)

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
