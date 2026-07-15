# Actions, Targeting, And Effects

## Action Types

The clean battle action surface includes basic attack, skill, item, guard, pass, analyze, Hosted Entity swap, Companion deploy, Companion recall, Companion swap, escape attempt, tactics change, negotiation, and host-special actions.

**Framework rule:** every action is assessed before execution. Assessment and execution use the same typed command and resolved targets. A rejected or cancelled assessment causes no cost, inventory consumption, effect mutation, or turn consumption.

## Targeting

Authored targeting defines relation, selection style, and optional count. Relations identify allies, opponents, self, or no target. Selection may be explicit, automatic, all, or random according to the definition.

**Framework rule:** resolved targets are captured once for an execution attempt. Random targets are not rolled again between assessment and mutation. Target order remains deterministic where authored or caller order is meaningful.

**Host responsibility:** a UI may use buttons, scene selection, a cursor, keyboard menus, or AI to choose among legal candidates. The host submits runtime IDs; it does not decide legality by reading names.

## Skill Costs

Active skills may declare resource costs and execution contexts. Passive skills do not use the active action shape.

**Framework rule:** costs are assessed before mutation and committed only for an executable action. Cost modifiers are resolved through typed passive rules and relevant damage elements. A failed or cancelled action does not spend the skill cost.

## Items And Consumption

Items may be consumable, key, material, or valuable. Only usable items carry contexts, targeting, and ordered effects. The Framework never owns a game's inventory object.

Item use follows reservation semantics:

1. The host or inventory port reserves one quantity.
2. Framework executes the typed item effects.
3. Consumption commits only when at least one applicable effect succeeds meaningfully.
4. Failure, cancellation, unavailability, or no effect rolls the reservation back.

Healing a full resource, curing no matching ailment, reviving a living target, setting an unchanged value, or removing an absent status is treated as known no effect. A multi-target item consumes once when at least one target receives a meaningful result.

## Typed Effects

Supported effects include damage, instant death, resource restoration or reduction, resource assignment, ailment application/removal, status removal, revival, stat-stage changes, shields, Break, charge, escape, analysis, affinity override, skill grants, custom handlers, and host action requests.

**Framework rule:** effects execute in authored order. Results carry the effect index, target runtime ID, outcome, value, related typed ID, affinity/critical information where applicable, passive activations, and host action requests.

Custom effects and host actions are explicit registered IDs. Framework never infers behavior from an action name, item description, category label, or effect text.

## Conditions

Conditions are typed definitions evaluated against the execution environment. They can inspect resources, states, elements, affinities, contexts, battle metadata, and logical compositions.

Battle-only conditions evaluate false when field execution does not provide battle metadata. Composite `all` and `any` conditions use their authored children. Custom conditions require an explicitly registered handler.

## Atomicity

Effect execution uses transaction boundaries. If an execution path is rejected or interrupted before commitment, live state is restored. Custom-handler failures cannot leave earlier changes partially applied. Ordered effect results remain available for presentation even when the host renders them later.
