# Actions, Targeting, And Effects

## What An Action Represents

An action is a typed request made by one runtime actor. The supplied battle
surface includes basic attacks, skills, items, guard, pass, analyze, escape,
Hosted Entity selection, Companion deployment/recall/swap, negotiation,
tactics changes, and explicit host-special actions.

Convergence separates choosing an action from resolving it:

1. the host constructs a typed command;
2. the Framework assesses authority, availability, costs, and targets;
3. the host may present that assessment to a player or AI;
4. the Framework executes that exact, single-use assessment.

A rejected assessment consumes no resource, inventory item, actor state, or
turn. Cancelling before execution has the same no-mutation result.

## Which Actions An Actor May Use

The canonical battle-action facade, `BattleActionExecutor`, owns the approved
skill, item, and basic-attack authorization rules. A menu, AI adapter, or
script cannot make these actions legal merely by constructing a definition
with a matching display name or ID.

- A skill command must use the canonical catalog definition and that skill must
  be in the actor's equipped runtime skill set.
- An item command must use the canonical catalog definition before owned
  quantity can be reserved.
- A basic attack must match the actor's resolved action ID, damage profile, and
  targeting profile.
- A basic-attack profile may come from equipment, a natural attack, or another
  explicit game policy. Weapons are not mandatory.
- Authorization is checked during assessment and again immediately before
  execution. A skill removed after the menu was shown is therefore rejected.
- Temporary or scripted exceptions must use an explicit authorization policy
  or a host-mediated action. They are never inferred from text.

## Targeting

Authored targeting combines three independent choices:

- relation: `None`, `Self`, `Ally`, `Enemy`, or `Any`;
- selection: `None`, `Single`, `All`, or `Random`;
- life state: `Alive`, `Dead`, or `Any`.

Target count and whether the acting actor may be included are additional typed
constraints. The host may present legal targets through a Godot scene, cursor,
menu, AI routine, or script, but it submits runtime IDs rather than deciding
legality from names.

Resolved targets are captured once for an execution attempt. A random target
is not rolled again between assessment and mutation. Random selection also has
no hidden fallback: composition must provide an explicit random-target policy.
The supplied ordered policy is available only when a game deliberately wants
deterministic candidate order.

## Skill Costs

Active skills may declare resource costs and valid execution contexts. Passive
skills use lifecycle triggers rather than the active command shape.

Costs are calculated during assessment and rechecked before execution. They are
applied to staged actor state before effects, then published with the rest of
the action transaction. Assessment rejection, cancellation, stale-state
rejection, or an exception before commit spends nothing.

An executable skill still pays its cost when an authored effect reports an
ordinary failure, stops a target, stops the action, or interrupts after earlier
effects. Those are resolved execution outcomes, not a cancelled command.

## Items And Inventory

Items may be consumables or non-usable catalog records. Only a consumable with
a usage definition can enter the typed item-use pipeline.

One `ItemBattleActionCommand` always means one attempted use of one owned item.
The canonical battle-action facade requires an inventory port and follows this
transaction:

1. verify that one matching item is available;
2. reserve exactly one unit;
3. verify that the reservation is live, unfinished, and identifies that item
   and quantity;
4. execute effects against staged actor state;
5. commit the item only if at least one effect succeeds meaningfully;
6. otherwise roll back the reservation;
7. publish actor state only after the required inventory transition succeeds.

The host obtains the item definition from its loaded catalog. The canonical
authorization policy requires the exact catalog object, while the inventory
port independently validates ownership and reserves one unit by content ID.
Substituting another definition with the same ID rejects before reservation or
actor mutation.

Using a healing item at full health, curing no matching ailment, reviving a
living target, setting an unchanged value, or removing an absent status is a
known no-effect result. A multi-target item is consumed once if at least one
target receives a meaningful result.

`ItemExecutor` is also public as a lower-level typed-effect service. It does not
own inventory and must not be treated as the complete owned-item transaction.

## Typed Effects

The implemented effect vocabulary is:

- damage and instant death;
- ailment application and removal;
- resource restoration, reduction, and assignment;
- revival;
- stat-stage modification;
- charge and shield grants;
- affinity Break and affinity override;
- status removal;
- analysis and escape requests;
- explicitly registered custom effects.

Effects execute in authored order. Each effect may have a typed condition and
an authored failure policy: continue, stop processing that target, or stop the
action. Results retain the effect index, target runtime ID, outcome, resolved
value, related IDs, combat details, passive activations, and any host-action
request IDs.

Effect order alone does not make a later effect depend on an earlier one. A
later effect may instead name an earlier effect through a local `effectId` and
an explicit dependency:

- `succeeded` requires the source effect to have succeeded;
- `positive_damage` requires the source damage to have removed a positive
  amount of the intended target's vital resource;
- `same_target` checks that fact independently for each target; and
- `any_target` permits a source result from any target in the same action.

An unmet dependency produces a typed skipped result before the later effect's
condition or random chance is evaluated. It does not trigger that effect's
failure policy. This permits both dependent riders and deliberately independent
later effects without inferring intent from their position.

Current staged life state is checked after the dependency and before the
condition. A strike may therefore establish positive damage and defeat its
target, while a later Poison rider still skips because ailments require a
living target. Ordinary damage, absorption, and vital-resource restoration do
not revive; only an explicit revival effect may do so.

Dependent secondary damage has two authored contact modes. `independent`
performs its own hit check. `shared_contact` reuses the earlier positive-damage
contact, but still resolves its own element, affinity, power, charge category,
and Critical policy. Neither mode inherits the earlier Critical result.

Weapon basic attacks use the same sequence. Their primary damage may expose a
local ID and the profile may append ordered typed secondary effects. A Fire
weapon can therefore be authored as Fire-only, Physical with an ailment rider,
or Physical with a separate Fire component; names and descriptions never pick
one of those models.

Convergence does not currently provide a skill-grant effect. Skill acquisition
and move-list changes belong to the progression services.

## Conditions And Extension Handlers

Conditions are typed records evaluated against actor state, targets, resources,
elements, affinities, contexts, and battle metadata. Logical `all`, `any`, and
`not` definitions compose them. A battle-only condition evaluates false when a
field action does not supply the required battle metadata.

`party_size` counts living, deployed actors whose team matches the acting
actor's team. It does not count reserve or defeated actors, and it does not
measure roster ownership. Zero is a valid authored value for an empty
deployment.

Formula, custom-condition, custom-effect, and escape handlers are available
only through explicit registered IDs. No rule examines an action name,
description, category label, or free-form effect text.

Host-mediated commands and host-action requests deliberately stop at the
Framework boundary. Convergence reports what the host must do; it does not
perform or roll back the external operation.

## Mutation And Failure Boundaries

Skills, items, basic attacks, and shared typed effects run against cloned actor
state. The clones are copied back only after the execution path reaches its
commit boundary. An exception before that point leaves live actor state
unchanged.

An authored interruption or failure policy is not automatically a rollback.
It may preserve costs and successful effects that occurred earlier in the same
resolved action. This is intentional ordered-effect behavior.

Inventory atomicity depends on the host honoring the reservation contract:
`Reserve`, `Commit`, and `Rollback` must each be atomic and report rejection
without partial mutation. Arbitrary side effects performed by a custom or host
callback are outside the actor transaction and cannot be undone by the
Framework.

Stat-stage assessment, application, duration, removal, and cleanup use the
selected canonical policy service described in
[Stat Modifier Policies](stat-modifier-policies.md). Skill, item, passive, and
encounter-lifecycle paths all operate on that policy-owned state rather than a
second aggregate modifier model.
