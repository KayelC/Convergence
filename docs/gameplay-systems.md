# Gameplay Systems

This document summarizes the reusable systems currently implemented by `Convergence.Framework`. Presentation and game-specific composition belong to the host.

## Content And Catalog

The framework provides immutable definitions for skills, entities, races, ailments, items, equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets. Strict `System.Text.Json` DTOs are mapped into serializer-neutral definitions, semantically validated with explicit registrations, dependency-checked, qualified, and exposed through immutable repositories.

Runtime rulesets resolve through a host-supplied typed policy-factory registry.
The standard composition is explicit and replaceable; unknown policy IDs fail
without selecting a fallback. See [Ruleset Policy Contracts](ruleset-policy-contracts.md).

## Actors, Stats, Resources, And Growth

Catalog actor factories hydrate identity, level, stats, defenses, resources,
learned skills, active skills, and passives. Runtime snapshots preserve
canonical actor state.

The standard Vessel composition service takes core stats, defenses, active
skills, and passives from the Active Hosted Entity selected by the canonical
party roster. The Vessel retains its own progression, current resources,
equipment, status, affiliation, and encounter presence. Composition rejects
atomically when source identity, ownership, skills, stats, or resources are
invalid.

Progression services own stat resolution, stage scaling, experience curves,
level growth, resource recalculation, allocation, and rollback through injected
policies. Live level growth processes authored skill unlocks. A full move list
creates a persisted pending replace-or-forget choice rather than dropping the
skill or cancelling the level.

## Actions And Effects

Typed commands cover basic attacks, skills, items, guard, pass, analyze, escape, Hosted Entity swaps, Companion deployment/recall, and host-mediated actions. Skills and items share targeting, condition evaluation, ordered effects, diagnostics, and transaction-safe inventory decisions. Behavior comes from typed definitions, not display text.

The canonical battle-action facade now requires an inventory reservation for
one item use, validates the returned reservation before effect execution, and
publishes staged actor changes only after the required inventory transition
succeeds. It also authorizes equipped canonical skills and resolved
basic-attack profiles during assessment and immediately before execution.
Action-outcome policy receives the source kind and immutable effect facts:
skills and basic attacks are effect-driven, while the supplied item default is
one normal turn with an authored effect-driven option.
Lower-level `SkillExecutor` and `ItemExecutor` services remain available for
callers that deliberately own the omitted loadout or inventory boundary.

## Combat And Turn Economy

Combat rules resolve damage, accuracy, criticals, elemental affinity, ailment
resistance, instant-death channels, chance, and power through bound policies.
Every supplied random draw validates the host's promised range before becoming
authoritative. Almighty, shields, Break, affinity replacement, and separated
resistance channels are explicit typed rules.

Turn economy is a separate optional policy family. The standard registry binds
either neutral `standard_actions` or `standard_action_token`, with explicit
finite phase limits. The encounter runner validates one immutable economy
snapshot chain and emits typed phase and before/after transition payloads.
Economies count action opportunities; encounter orchestration still owns team
and actor scheduling, while hosts own token presentation.

## Status And Passives

The optional lifecycle service handles battle start, turn restrictions, turn
end, explicit action/actor/phase/round clocks, reserve suspension or configured
reserve advancement, typed departure cleanup, staged ailment application, and
passive trigger dispatch. Turn-start behavior and owner-turn-end triggers both
use boundary-start exact-instance schedules, so custom status mutation cannot
execute stale or newly added slots. The canonical runner composed with the
supplied lifecycle port dispatches flee, roster-recall, and newly observed
defeat cleanup before completion.
Expiration and permitted removal causes are separate. Custom-handler execution
is transactional. Passive targeting and activation counting are explicit, and
target eligibility is frozen before passive effects can change the staged actor
graph. Supplied passive event policies never replace an explicit host
registration. Committed lifecycle changes reach hosts as typed events. Save
restore requires one enabled/disabled state per equipped passive and rejects
active ailments that conflict within one exclusivity group. See
[Status And Passive Lifecycle](mechanics/status-passive-lifecycle.md) and its
[integration guide](developer-guide/status-passive-lifecycle.md).

The stat-modifier policy family separately supplies persistent staged,
timed-exclusive signal, and independently timed contribution policies. Timed
state retains explicit lifecycle-boundary cursors for same-boundary protection
and idempotent ticking. Skill, item, passive, removal, lifecycle, cleanup,
ruleset binding, save validation, and aggregate restore paths now use that one
immutable policy authority. The combat stage-scaling policy remains a separate
replaceable choice.

## Encounters, AI, Knowledge, And Rewards

The encounter runner owns structural validation, lifecycle dispatch,
turn-economy application, reconciliation, cancellation, outcomes, and ordered
events. Initiative and scheduling are injected separately. The supplied
team-phase policy rotates active actors inside team phases; the supplied
Agility policy freezes one descending actor order per round and gives each
actor a one-actor phase. A bounded post-command extension may retain an actor
only when an economy opportunity already remains.

Lifecycle mutation is staged at battle start, turn start, owner turn end,
phase end, round end, actor departure, and battle end. Synchronization,
departure cleanup, defeat announcement, and completion are reconciled after
every committed lifecycle boundary. Command handlers return typed consumption
but cannot mutate the retained economy. Menu Back stays inside the host command
loop; typed encounter cancellation, operational token cancellation, rejection,
and faults are distinct contracts.

The supplied last-team-standing completion policy returns an immediate draw
when no deployed living team remains and victory when exactly one remains.
Defeat cleanup and announcement occur once per uninterrupted defeated period;
recovery permits a later defeat to begin a new period.

Events expose immutable typed payloads instead of making debug messages
authoritative, so Godot, console, and test hosts can map the same event to
different presentation. `TurnEnded` and `RoundEnded` close committed structural
boundaries, while `BattleEnded` reports both the final round reached and fully
completed round count. Normal completion detail remains optional event debug
text; `FaultMessage` and `FaultCode` appear only on faulted results. Strategy
ports allow deterministic or host-defined
action selection. See
[Encounter Rounds, Phases, And Turns](mechanics/encounter-rounds-phases-and-turns.md)
and its [integration guide](developer-guide/encounter-orchestration.md).

Battle knowledge has two explicit authorities. `RuntimeKnowledgeSnapshot`
stores persistent entity-definition facts for a player session, while
`RuntimeEncounterKnowledgeSnapshot` stores current-target facts by runtime ID.
Executed effects carry typed observations, and one atomic transition service
applies ordinary contact and Analyze evidence without allowing hosts to inspect
hidden defenses. Automated teammates share one encounter snapshot per side;
ordinary runs start fresh, while scripted encounters may supply validated
seeds. Familiar acquisition imports are optional and policy-controlled.
Negotiation and reward services return immutable outcomes without owning
presentation.

## Party, Rosters, Inventory, And Economy

The party aggregate is the only authority for active/reserve placement, Hosted
Entity ownership, Companion ownership, and Active Hosted Entity selection. An
active Hosted Entity remains in its roster; a deployed Companion remains owned
while also occupying an active-party slot. Encounter presence remains separate
actor state.

Transition services enforce runtime-ID uniqueness, approved overlap roles,
roster capacity, deploy/recall/swap/consume behavior, item stacks,
inventory-owned equipment-instance identity, equip compatibility, typed
currency-ledger arithmetic, policy-bound shop pricing and stock, shop transactions, and
restoration transactions. Hosts own UI and durable inventory storage.

Order 7 is complete. O7-R2 gives each equipment copy a unique runtime instance
ID, permits separate copies of one definition, makes inventory the sole owner,
and removes the former root save equipment authority. O7-R3 makes equipment
slot identity authored and policy-validated. O7-R4 derives weapon attacks,
accessory modifiers, granted skills, armor Defense, and armor/boots Evasion
through one equipment profile; canonical action authorization and automated
selection resolve that profile live, while actor composition and restoration
consume its numeric contributions. O7-R5 replaces the unnamed balance with an
immutable ledger keyed by currency `ContentId`; every shop, recovery,
Compendium, reward, and negotiation transaction now names its currency.
O7-R6 makes authored purchase price exact under the supplied standard pricing
policy, makes resale percentage configurable with truncation toward zero,
retains Luck adjustment only as an explicitly selected optional policy, and
resolves offer-level policy configuration through typed host-registerable
factories without fallback. The same resolved offer profile drives assessment
and execution. O7-R7 gives every offer a stable shop-local identity, persists
limited quantities under the qualified shop/offer pair, decrements standard
stock only on successful purchases, and permits explicit custom resale
replenishment while committing inventory, currency, and stock atomically.
O7-R8 replaces the HP/SP patient boundary with an optional generic recovery
policy. It plans from complete immutable actor state, restores explicitly
configured resources, cures only ailments that permit recovery events, clears
selected temporary categories through canonical authorities, and commits actor
state only after the named-currency debit succeeds. Training Annex explicitly
binds the supplied standard policy to preserve its established HP/SP quote.
O7-R9 certifies these authorities together: aggregate restoration accepts one
coherent equipment/slot/currency/stock graph and exposes no partial session on
rejection; rejected and cancelled transactions preserve every supplied
before-state; and resolved shop offers cannot be publicly constructed or
rewritten after catalog-backed resolution.
The owner-approved
[Order 7 roadmap](reviews/inventory-equipment-economy-order-7-source-review-2026-08-10.md)
defines the direct authority corrections and narrowly justified policy seams;
the R15-reviewed
[player mechanics](mechanics/party-inventory-and-economy.md),
[developer integration](developer-guide/inventory-equipment-and-economy.md),
and [technical authority](technical/inventory-equipment-economy-runtime.md)
pages provide the detailed contracts. The
[O7-R15 final closure review](reviews/inventory-equipment-economy-order-7-r15-final-closure-review-2026-08-24.md)
is the current independent source, documentation, and release-gate closure
evidence.

## Navigation, Traversal, And Encounter Preparation

Generic navigation uses arbitrary `ContentId` locations and injected access policy. Optional dungeon traversal uses arbitrary node IDs and injected traversal policy. Neither service prescribes scenes, menus, floors, or automatic battles. Hosts explicitly trigger authored encounters; preparation services hydrate ordered runtime actors from catalog formations.

## Fusion, Inheritance, And Compendium

Fusion services resolve typed recipes and strategy policies, build deterministic candidate plans, validate inherited skill selections, construct previews, and assess transactions. Inheritance precedence is typed and shared between preview and commit. Compendium services distinguish first acquisition from explicit updates: `RecordAcquisition` adds a missing entry but preserves an existing snapshot, while `RegisterActor` is the deliberate add-or-update operation. Recall pricing and familiar-knowledge import remain separately configurable.

## Persistence

Versioned snapshots cover actors, party and rosters, inventory-owned equipment instances, actor loadout references, typed currency balances, durable tracked shop stock, optional field/traversal state, Compendium, knowledge, session progress, and checkpoint breadcrumbs. Validation rejects inconsistent IDs, references, numeric domains, timed state, capacities, and catalog provenance before restore. Aggregate restoration resolves actor profiles, restores Hosted Entity dependencies before Vessels, and exposes no partial session on rejection. Hosts own serialization, slots, suspend-save storage, scene reconstruction, and UI.

## Demonstration Coverage

DemoHost provides focused battle, field, and save demonstrations plus the original Training Annex end-to-end slice. The [capability matrix](roadmap/framework-capability-matrix.md) records whether each framework area is complete, partial, or deferred independently from demo breadth.

Detailed actor integration is documented in
[Actors And Runtime State](developer-guide/actors-and-runtime-state.md), with
maintainer invariants in
[Runtime Actor State And Restoration](technical/runtime-actor-state-and-restoration.md).
Inventory/economy composition is documented in
[Inventory, Equipment, And Economy Integration](developer-guide/inventory-equipment-and-economy.md),
with its authority and transaction state machines in
[Inventory, Equipment, And Economy Runtime](technical/inventory-equipment-economy-runtime.md).
