# Decision: Battle Action Ownership And Inventory Authority

Status: confirmed
Date: 2026-07-17

## Context

The Order 1 source reviews found that the typed battle-action facade accepted
item commands without inventory authority and accepted caller-supplied skill,
item, and basic-attack definitions without consistently proving canonical
authority. The public item command also accepted arbitrary positive quantities
even though the result described one use as `ConsumeOne`.

## Decision

### Inventory-Backed Item Actions

An `ItemBattleActionCommand` represents exactly one use of one owned consumable.
Execution through the canonical battle-action facade requires an
`IItemActionInventory` port.

The Framework must:

1. reject before mutation when no inventory port is supplied;
2. reserve exactly one unit;
3. verify that the returned reservation is live, uncompleted, and belongs to
   the requested item and quantity;
4. execute staged typed effects;
5. commit the reservation only when the item reports meaningful success; and
6. roll back the reservation and publish no actor mutation when execution or
   commitment is rejected.

`ItemExecutor` may remain a lower-level typed-effect service. Calling it directly
does not constitute an inventory transaction and must not be documented as the
canonical owned-item action path.

### Canonical Item Definitions

An item command must carry the exact canonical `ItemDefinition` returned by the
catalog repository supplied to `CatalogBattleActionAuthorizationPolicy`.
Matching an item ID is not sufficient: another definition object with the same
ID may contain substituted targeting or effects.

Canonical definition authority and inventory authority are independent checks:

- the authorization policy proves which authored item definition may execute;
- `IItemActionInventory` proves that one matching item is currently owned and
  coordinates its reservation, commit, or rollback.

Both checks run before actor mutation. Canonical item authority is rechecked
immediately before execution so a prepared assessment cannot outlive the
catalog-backed action surface that authorized it.

### Prepared Skill Cost Quotes

Skill assessment resolves one immutable cost quote per distinct resource ID.
Execution revalidates authored identity and current affordability, then commits
that quoted amount through the actor transaction. It deliberately does not
rerun formula handlers or resource-cost modifiers. This keeps the amount shown
by a host identical to the amount charged and ensures random/custom amount
policies execute once. A host that wants changed modifier state reflected must
discard the assessment and request another one.

### Actor Action Authorization

The Framework, not the presentation host, is the canonical authority for which
skills, items, and basic attacks an actor may execute.

- A skill action must reference a skill in the actor's authorized equipped
  action loadout or the skill-grant set derived from currently equipped
  inventory-owned instances.
- An item action must reference the canonical catalog definition; inventory
  ownership is then validated independently by the reservation port.
- A basic attack must reference the actor's resolved basic-attack profile.
- A resolved basic attack may originate from equipment, an authored natural
  attack, or another explicit game policy. Equipment is not mandatory.
- A host chooses among authorized actions and supplies presentation and target
  input. It cannot make an action legal by constructing an arbitrary typed
  definition.
- Temporary grants, scripted actions, and other exceptions require an explicit
  framework-recognized authorization path or a host-mediated action; they are
  not inferred from names or display text.

## Alternatives

- Trusting every host to filter actions was rejected because a Godot UI, AI
  adapter, or script error could bypass actor ownership rules.
- Allowing an inventory-less item command to mutate actors and asking the host
  to consume afterward was rejected because actor and inventory mutation would
  not be atomic.
- Treating the item command quantity as a batch-use count was rejected because
  one action executes the authored effects once and the public result reports
  `ConsumeOne`.
- Requiring every basic attack to come from a weapon was rejected because games
  may use natural or policy-supplied attacks.

## Consequences

- The focused breaking correction is implemented. Item commands are one-use
  commands and no longer expose arbitrary quantities.
- `BattleActionExecutor` requires explicit actor-action authority and validates
  canonical skill definitions, canonical item definitions, and resolved
  basic-attack profiles without trusting host display choices.
- Clean hosts build commands from the actor's authorized action surface and
  supply inventory for item actions.
- Failed authorization and reservation validation return typed diagnostics and
  consume no item, resource, effect mutation, or turn.
- Authorization is rechecked at execution so an assessment cannot outlive a
  changed equipped skill, canonical item definition, or basic-attack profile.

## Affected Documentation And Evidence

- [Actions, Targeting, And Effects](../mechanics/actions-targeting-and-effects.md)
- [Gameplay Systems](../gameplay-systems.md)
- [Documentation Completion Roadmap](../roadmap/documentation-completion-roadmap.md)
- [Order 1 Source Review](../reviews/typed-action-and-effect-execution-order-1-review-2026-07-17.md)
- [Typed Actions And Effects](../developer-guide/typed-actions-and-effects.md)
- [Typed Action And Effect Execution](../technical/typed-action-and-effect-execution.md)

Implementation evidence includes focused tests for missing inventory,
exactly-one consumption, malformed reservations, unowned skills, canonical
skill and item substitution, execution-time reauthorization, valid
natural/equipment basic attacks, and host-mediated exceptions. The original
corrections were committed separately as `14c7630`, `dd243fc`, `743396a`, and
`49d04ea`; canonical item authorization was completed by the later Order 1
closure corrections.
