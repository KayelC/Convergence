# Encounter Rounds, Phases, And Turns

## Purpose

An encounter is the framework-owned loop that brings combat participants
together, decides when commands may be submitted, runs lifecycle boundaries,
spends battle opportunities, and reaches a typed outcome.

Convergence does not require one battle presentation or one actor-order model.
A Godot game may animate a timeline, a console may display menus, and an
automated simulation may choose commands without input. They can all use the
same encounter contract.

> **Current review status:** `reviewed`. O6-R23 independently traced the
> corrected runtime and confirmed this page's scheduling, turn-economy,
> reconciliation, cancellation, and terminal-state rules against executable
> evidence.

## Rule Ownership

- **Framework rule:** the encounter runner validates participants, executes
  structural boundaries, reconciles defeat and departure, validates command and
  outcome shapes, and publishes one ordered event stream.
- **Configured rule:** injected initiative, scheduling, turn-economy,
  lifecycle, completion, and post-command policies select the game's battle
  model.
- **Host responsibility:** scenes, menus, animation, input, target cursors,
  audio, rendering, and the decision to start an encounter.

Rewards, recruitment, exploration, and post-battle scene changes are separate
modules. An encounter can report an outcome without calculating or applying
those systems.

## The Encounter Structure

The supplied loop uses these concepts:

1. **Battle start:** participants are validated, teams are ordered, battle-start
   lifecycle work commits, and immediate defeat or departure is reconciled.
2. **Round:** a scheduler-defined outer cycle. A round can contain team phases
   or individual actor phases.
3. **Phase:** one turn-economy scope. It starts with a configured number of
   opportunities and ends when none remain or a command explicitly terminates
   it.
4. **Turn window:** one scheduled actor reaches turn start and may commit one
   command, unless lifecycle restrictions remove or redirect that opportunity.
5. **Battle end:** completion or a command requests a terminal outcome,
   battle-end lifecycle commits, and the final result is published.

The scheduler chooses *who receives the next window*. The turn economy decides
*whether the current phase still has an opportunity*. Neither authority may
silently perform the other's job.

## Supplied Scheduling Models

### Team Phase Round Robin

`TeamPhaseRoundRobinBattleEncounterSchedulePolicy`:

- visits teams in initiative order;
- starts one phase for each team;
- counts the team's currently deployed, living actors when the phase begins;
- rotates command windows across currently available actors on that team;
- refreshes availability after commands, defeat, recall, flee, or deployment
  changes;
- closes the round after all team phases close.

An optional post-command policy may retain the same actor when the turn economy
already reports another opportunity. It cannot create an opportunity. Its
consecutive immediate repeats are bounded by configuration so a faulty policy
cannot create an endless actor loop.

### Agility Ordered

`AgilityOrderedBattleEncounterSchedulePolicy`:

- resolves every available actor's configured ordering stat at round start;
- sorts from highest to lowest;
- delegates equal values to an injected tie-break policy;
- freezes that order for the current round;
- gives each scheduled actor a separate one-actor phase;
- applies changed Agility and newly deployed actors when the next round order
  is resolved.

If that actor's turn economy grants another opportunity, the same actor
continues in that one-actor phase. An actor who becomes unavailable before
their frozen slot is skipped.

These are supplied policies, not mandatory genres. A game may implement
`IBattleEncounterSchedulePolicy` for another deterministic structure.

## One Actor Window

For a normal committed command, the visible rule order is:

1. the actor's turn starts;
2. turn-start lifecycle runs;
3. restrictions, defeat, flee, and roster recall are reconciled;
4. the host or AI selects and executes a command;
5. the configured turn economy applies the command's typed consumption;
6. owner-turn-end lifecycle runs when the command consumes a turn;
7. resource, status, departure, and defeat changes are reconciled;
8. the actor's turn ends;
9. the scheduler chooses the next structural step.

A free action still closes its current command window, but it does not run
owner-turn-end lifecycle and does not spend the turn economy. Phase liveness
limits prevent an unlimited stream of free actions.

Encounter liveness has a second independent limit. A custom scheduler cannot
loop forever through empty round or phase boundaries without ever offering a
command: the configured encounter-wide structural-transition bound ends that
run as a typed fault. Games configure both limits rather than relying on one to
cover both responsibilities.

Turn-start restrictions may:

- allow ordinary action;
- skip the action;
- limit the available actions;
- require a physical or confusion action;
- flee the battle;
- recall a Companion to its roster.

The lifecycle policy supplies the restriction. The encounter runner ensures
that every route uses the same command-result, reconciliation, event, and
completion boundaries.

Defeat cleanup and announcement happen once for each uninterrupted period in
which an actor is defeated. Merely observing the same defeated actor at another
reconciliation boundary does not repeat either operation. If the actor becomes
living again, that recovery closes the old defeat period; a later defeat starts
a new period and receives cleanup and announcement again.

## Back, Cancellation, Rejection, And Faults

These are different outcomes:

- **Menu Back:** the host remains inside the same command-selection window. No
  command result is returned, no resource or opportunity is spent, and the
  player may choose again.
- **Typed encounter cancellation:** the turn handler returns
  `BattleEncounterCommandResult.Cancelled`. The whole encounter ends as
  `Cancelled`, with battle-end cleanup exactly once and no turn cost.
- **Operational cancellation:** the host cancels the supplied
  `CancellationToken`. The runner propagates `OperationCanceledException`.
  Uncommitted lifecycle state is rolled back and the runner does not invent
  `TurnEnded` or `BattleEnded` events.
- **Rejected command:** a command handler violated the accepted encounter
  contract. The encounter ends as a typed `CommandRejected` fault rather than
  spending a turn or silently asking the player again.
- **Port fault:** an injected service throws or returns an invalid transition.
  The runner contains it as a stable `BattleEncounterFaultCode`. The runner
  attempts battle-end cleanup once only when battle start had already succeeded;
  a failure before battle start has no accepted encounter lifecycle to clean
  up. Either path reports a faulted result.

An ordinary unaffordable or invalid menu option should be rejected during the
host's assessment loop before it becomes an encounter command result.

## Completion And Outcomes

The supplied `LastTeamStandingCompletionPolicy` ends as soon as at most one
deployed, living team remains. Zero living teams produce an immediate `Draw`;
one living team produces `Victory` for that team; two or more keep the encounter
running. A replacement completion policy may select another rule, but its result
must be coherent:

- `Victory` and `Defeat` require one participating winning team;
- `Draw`, `Escape`, and `Cancelled` do not carry a winner;
- `Faulted` carries a stable fault code rather than a winner;
- incomplete evaluations cannot carry terminal metadata.

Normal terminal outcomes always have null `FaultMessage` and `FaultCode`
fields. A completion policy's optional message is diagnostic text on the
`BattleEnded` event, not fault evidence. Only `Faulted` returns both a stable
fault code and a nonblank fault message.

The configured round limit produces a draw after that many fully completed
rounds. The final result distinguishes:

- the last round that was reached; and
- the number of rounds whose round-end lifecycle fully committed.

This distinction matters when a battle ends halfway through a round.

## Ordered Battle Evidence

Every encounter result contains immutable participant snapshots and canonical
`BattleEncounterEvent` records with continuous sequence numbers.

Structural events include:

- actor and battle start;
- initiative;
- round, phase, and turn start;
- turn-economy changes;
- turn, phase, and round end;
- defeat, fault, and battle end.

Command and lifecycle events include selected or passed commands, executed
actions, resolved effects, resources, statuses, passives, presence changes,
rejections, and host-mediated requests.

Typed payloads are authoritative. Optional debug text is only useful for logs;
a UI must not parse it to decide mechanics.

The result owns the complete canonical sequenced history. When publication is
successful, the sink observes that same order. If the sink itself fails,
terminal fault evidence may be present only in the returned result because the
runner will not recursively trust the failed sink.

The runner verifies that command, effect, status, resource, knowledge, and
presence evidence refers to participants in this encounter. A command cannot
claim to belong to a different actor, and a presence event cannot silently
move an actor to a team that does not own them.

## What The Framework Does Not Force

The encounter module does not force:

- team phases instead of individual Agility order;
- Action Token instead of another `IBattleTurnEconomy`;
- a command menu instead of direct scene input;
- automatic combat when an exploration trigger is touched;
- rewards, recruitment, or scene transitions after victory;
- persistent AI knowledge between ordinary encounters.

Those choices remain explicit policy or host composition.

## Related Rules

- [Combat, Defenses, And Turn Economy](combat-defenses-and-turns.md)
- [Status And Passive Lifecycle](status-passive-lifecycle.md)
- [Battle Knowledge](battle-knowledge.md)
- [Actions, Targeting, And Effects](actions-targeting-and-effects.md)
