# Encounter Orchestration Integration

## Purpose

This guide shows how a host composes and runs `BattleEncounterRunner`. The
runner owns orchestration; the host supplies policies, commands, event
presentation, and any mapping between framework runtime IDs and scene objects.

Engine and UI hosts should always await `RunAsync`. The synchronous `Run`
method exists only as a compatibility convenience for non-UI callers.

> **Current review status:** `reviewed` and independently verified at O6-R42.
> Stable team-ring rotation, economy-aware scheduler validation, legitimate
> live-phase closure, and phase turn-window safety semantics match current
> source and tests.

## Required Composition

Create a `BattleEncounterServices` instance with:

| Contract | Responsibility |
|---|---|
| `IBattleEncounterInitiativePolicy` | Returns every participating team exactly once in initial order. |
| `IBattleEncounterSchedulePolicy` | Selects rounds, phases, command recipients, and structural progression. |
| `IBattleEncounterLifecyclePort` | Runs battle-start, turn-start, turn-end, phase-end, round-end, and battle-end lifecycle work. |
| `IBattleEncounterTurnHandler` | Reads a host or AI command and returns one typed command result. |
| `IBattleEncounterCompletionPolicy` | Evaluates terminal state after every reconciliation boundary. |
| `Func<IBattleTurnEconomy>` | Creates a fresh economy for each scheduled phase. |
| `BattlePhaseProgressPolicy` | Bounds accepted actor turn windows and consecutive free actions per phase. |
| `BattleEncounterProgressPolicy` | Bounds accepted structural scheduler transitions across the encounter. |
| `IBattleEncounterStateSynchronizer` | Optional host adapter for synchronizing external state with canonical participants. |
| `IBattleEncounterEventSink` | Optional asynchronous destination for animation, UI, logs, or telemetry. |

The reusable runtime state is `RuntimeActorState`. Construct each
`BattleEncounterParticipant` from that state and a display label. Runtime
instance IDs, not scene-node names, identify actors.

## Minimal Composition

```csharp
var services = new BattleEncounterServices(
    initiative: new ParticipantOrderInitiativePolicy(),
    schedule: new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
    lifecycle: lifecyclePort,
    turnHandler: turnHandler,
    completion: new LastTeamStandingCompletionPolicy(),
    turnEconomyFactory: () => new ActionTokenTurnEconomy(),
    phaseProgress: new BattlePhaseProgressPolicy(
        maximumCommands: 256,
        maximumConsecutiveFreeActions: 32),
    encounterProgress: new BattleEncounterProgressPolicy(
        maximumScheduleTransitions: 4096),
    synchronizer: NoopBattleEncounterStateSynchronizer.Instance,
    events: eventSink);

var request = new BattleEncounterRequest(
    participants,
    contextId,
    battleKindId,
    moonPhaseId: null,
    roundLimit: 100);

BattleEncounterResult result =
    await new BattleEncounterRunner().RunAsync(
        request,
        services,
        cancellationToken);
```

Inject the ruleset-bound turn-economy factory used by the game. The example
constructs Action Token directly only to show the interface shape.

## Choosing A Scheduler

### Team Phases

Use `TeamPhaseRoundRobinBattleEncounterSchedulePolicy` for phases shared by a
team. It scans the team's stable participant order from a retained ring cursor,
skipping unavailable actors without compacting the ring, while the phase
economy has opportunities.

To give the same actor an immediate follow-up without changing the economy,
configure:

```csharp
var extension = new BattleEncounterPostCommandScheduleExtension(
    postCommandPolicy,
    maximumConsecutiveImmediateRepeats: 1);

var schedule =
    new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(extension);
```

`IBattleEncounterPostCommandSchedulePolicy` receives immutable accepted-command
and economy evidence. It may return `RetainActor` or `FollowScheduler`. It
cannot mutate actors, execute a command, or mint an opportunity.

### Individual Agility Order

Use:

```csharp
var schedule = new AgilityOrderedBattleEncounterSchedulePolicy(
    agilityStatId,
    new EncounterOrderBattleEncounterScheduleTieBreakPolicy());
```

The scheduler requires every available actor to have a non-negative resolved
ordering stat. Equal values must be returned as an exact permutation by the
tie-break policy. The order is frozen for one round.

### Replacement Schedulers

An `IBattleEncounterSchedulePolicy` owns immutable
`BattleEncounterScheduleStateSnapshot` values and returns one next structural
step at a time:

- `BattleEncounterRoundStartedScheduleStep`
- `BattleEncounterPhaseStartedScheduleStep`
- `BattleEncounterCommandWindowScheduleStep`
- `BattleEncounterPhaseEndedScheduleStep`
- `BattleEncounterRoundEndedScheduleStep`

The runner returns immutable completion evidence through
`BattleEncounterScheduleStepOutcome`. Preserve policy ID, encounter identity,
revision continuity, step sequence, and legal step/outcome pairings. An invalid
transition becomes `ScheduleTransitionInvalid`; an exception becomes
`ScheduleExecutionFailed`.

An active state must report `CompletedRounds == CurrentRound - 1`. Every
transition except completion must obey this structural matrix:

| Completed step | Legal next step |
|---|---|
| `RoundStarted` | `PhaseStarted` or `RoundEnded` |
| `PhaseStarted(team)` | `CommandWindow(team)` or `PhaseEnded(team)` |
| `CommandWindow(team)` | `CommandWindow(team)` or `PhaseEnded(team)` |
| `PhaseEnded` | `PhaseStarted` or `RoundEnded` |
| `RoundEnded` | the next round's `RoundStarted`, or schedule completion at the configured round limit |

Non-round-end steps preserve both round counters. Advancing after
`RoundEnded` increments the current round exactly once and marks the prior
round complete; completing after `RoundEnded` is legal only at the round limit.
The runner validates this before accepting the next cursor, so structural drift
cannot reach another command or lifecycle commit.

`TurnEconomyStarted` and `CommandCommitted` outcomes carry authoritative
`HasRemainingOpportunities` evidence. If that value is false, selecting another
`CommandWindow` is structurally invalid and faults before turn-start lifecycle
or the handler can run. A scheduler may still select `PhaseEnded` while the
economy is live when it has no eligible recipient; the supplied one-actor
Agility scheduler uses this route after its frozen actor becomes unavailable.

`BattleEncounterProgressPolicy` is a separate encounter-wide liveness guard.
It limits accepted scheduler transitions, including round and phase boundaries
that do not open a command window. `BattlePhaseProgressPolicy` instead limits
accepted turn windows and consecutive free actions inside one phase. Its
pre-release `MaximumCommands` property increments when an initially available
actor reaches `TurnStarted`, before turn-start lifecycle. An actor found
unavailable before that boundary does not increment it; an actor removed by the
committed lifecycle does. Supply both policies: neither can replace the other.

For turn economy, `ActionTurnConsumptionKind.None` is a strict no-cost
contract. `IBattleTurnEconomy.Apply` must return an exactly equal snapshot for
that kind. Any state movement becomes `TurnEconomyTransitionInvalid` before
turn-end lifecycle or economy evidence is accepted. Every nonterminal `None`
result advances the consecutive-free-action counter by its typed kind rather
than by comparing snapshots.

## Implementing The Turn Handler

`IBattleEncounterTurnHandler.ExecuteTurnAsync` receives:

- the scheduled actor and current participant list;
- the committed turn-start restriction;
- the accepted turn-economy snapshot;
- any active stat-modifier lifecycle boundaries.

The committed restriction is authoritative input, not advisory metadata.
`CanAct` permits the normal host command loop. Skip, limited-action,
forced-action, flee, and roster-recall outcomes must be enacted by the turn
handler or an explicitly composed restriction resolver. The runner validates
the returned command transaction, but it does not invent a replacement command
for a custom handler that ignores the restriction.

A manual host normally loops internally:

1. show commands permitted by the restriction;
2. assess the chosen typed `BattleActionCommand`;
3. allow Back to return to the same menu;
4. choose targets;
5. execute through `IBattleActionExecutor`;
6. map the execution to `BattleEncounterCommandResult.Executed`.

Do not return `Cancelled` for submenu Back. `Cancelled` terminates the entire
encounter. Do not return `Rejected` for an ordinary disabled menu option;
`Rejected` is treated as a host-contract fault.

The handler must not mutate the encounter economy. It returns
`ActionTurnConsumption`; the runner applies and validates it exactly once.

The handler and optional state synchronizer are trusted host mutation ports.
The runner converts their exceptions to typed encounter faults, but it cannot
roll back arbitrary scene, network, filesystem, or other external side effects
performed inside host code. Use the framework action executor and staged
lifecycle services for framework state, and give custom mutation ports an
equivalent transaction boundary before they return.

### Supplied Automated Restriction Resolver

`AutomatedBattleTurnRestrictionResolver` accepts an
`IAutomatedRestrictedActionSource` for restrictions that require a command.
Each `AutomatedRestrictedActionSelection.ActionId` must identify its typed
command exactly:

| Command | Canonical action ID |
|---|---|
| `BasicAttackBattleActionCommand` | the command's `ActionId` |
| `SkillBattleActionCommand` | `Skill.Id` |
| `ItemBattleActionCommand` | `Item.Id` |
| `GuardBattleActionCommand` | `guard` |
| `PassBattleActionCommand` | `pass` |
| `AnalyzeBattleActionCommand` | `analyze` |
| `EscapeAttemptBattleActionCommand` | `escape` |

Construction rejects a mismatch, and the resolver independently revalidates
the identity before assessment or mutation. Limited-action restrictions compare
their allowed IDs with this canonical value. Supply a custom resolver for
other command kinds rather than assigning them a misleading fixed label.

## Lifecycle Composition

For the standard status and passive module, use
`BattleStatusEncounterLifecyclePort`. It stages lifecycle operations and
commits only after cancellation and returned-event validation.

The standard port also retains committed lifecycle sequence counters. Do not
share one instance between concurrently running encounters. Give each active
encounter exclusive access to its sequence authority. Sequential reuse
deliberately continues that authority; replacing it deliberately starts a new
clock stream and must agree with the lifetime of retained modifier state.

The runner calls lifecycle at these boundaries:

1. battle start;
2. actor turn start;
3. owner turn end for a turn-consuming command;
4. phase end;
5. round end;
6. battle end.

If the lifecycle also implements `IBattleEncounterDepartureLifecyclePort`, the
runner dispatches exact defeat, flee, and roster-recall cleanup. Departure is
reconciled to a bounded fixed point because one cleanup may make another actor
depart. Defeat bookkeeping covers one uninterrupted defeated period: repeated
reconciliation while the actor remains defeated does not duplicate cleanup or
announcement, recovery releases that bookkeeping, and a later defeat is
processed as a new period.

If an explicit Flee or Roster Recall commits while that actor is also defeated,
the explicit reason owns cleanup for the complete current defeat period. A
defeat announcement may still be published once, but the fixed-point pass does
not append a second Defeat cleanup. Recovery releases both cleanup and
announcement bookkeeping for a later period.

If timed stat modifiers need the currently active lifecycle sequence, implement
`IBattleEncounterStatModifierBoundarySource`. The runner snapshots those
boundaries into the command request; it does not invent their clock sequence.

## Event Sink

Implement:

```csharp
public ValueTask PublishAsync(
    BattleEncounterEvent battleEvent,
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    queue.Enqueue(battleEvent);
    return ValueTask.CompletedTask;
}
```

Switch on `battleEvent.Kind` and cast `battleEvent.Payload` to the matching
typed payload. Do not parse `DebugText`.

The runner owns structural events. Turn handlers and lifecycle ports may return
only:

- `CommandSelected`, `CommandPassed`, `ActionExecuted`, or `ActionRejected`;
- `EffectResolved`, `PassiveActivated`, `StatusChanged`, or `ResourceChanged`;
- `EncounterPresenceChanged`;
- `HostActionRequested`.

Returning a runner-owned kind such as `TurnStarted`, `TurnEconomyChanged`,
`BattleFaulted`, or `BattleEnded` faults the encounter. This preserves one
auditable source for structural ordering.

Every runtime actor or target ID in a port event must belong to the encounter's
frozen participant graph, including IDs nested inside effect, damage, resource,
knowledge, analysis, passive, and lifecycle evidence. Selected, passed,
rejected, and host-request command evidence must name the actor who owns the
current command window. `ActionExecuted` must also name that actor unless its
action event kind is `PartyRosterTransitioned`, the canonical transition that
has no acting participant. Actorless ordinary-action evidence is rejected while
the turn handler is executing and becomes a typed turn-handler fault before the
event is published. Presence-change events must report the participant's
actual encounter team. Combat-profile source identity is provenance rather than
a routing target, so a Vessel may still identify a non-deployed Hosted Entity as
its profile source.

An event-sink exception becomes `EventPublicationFailed`. During fault
finalization, a second sink failure stops further publication but preserves
the immutable returned event evidence. The result therefore owns the canonical
sequenced history; it is identical to successful sink delivery during ordinary
operation, but sink-failure finalization may append evidence that the failed
sink never received. A host that needs delivery acknowledgement must track the
sequence numbers it actually consumed rather than infer delivery from the
returned result.

## Completion Results

`IBattleEncounterCompletionPolicy.Evaluate` receives detached participant
snapshots captured after synchronization, departure cleanup, and defeat
announcement. Return:

- `IsComplete = false` with no terminal metadata; or
- a coherent `BattleEncounterOutcome`.

`Victory` and `Defeat` require a participating `WinningTeamId`. `Draw`,
`Escape`, `Cancelled`, and `Faulted` cannot identify a winner. A policy should
not produce `Faulted`; execution faults are owned by the runner's typed fault
boundary.

`BattleEncounterCompletion.Message` is optional non-authoritative debug text
for the terminal `BattleEnded` event. It is not copied into
`BattleEncounterResult.FaultMessage`. Every normal result has null
`FaultMessage` and `FaultCode`; a `Faulted` result has both. The automated
runner preserves the same distinction.

The supplied `LastTeamStandingCompletionPolicy` completes immediately when at
most one deployed, living team remains: zero produces `Draw`, one produces
`Victory` for that team, and two or more remains incomplete.

## Cancellation And Fault Handling

Pass the Godot scene, task, or application cancellation token to `RunAsync`.
The runner checks cancellation before port calls and before staged lifecycle
commits.

- Token cancellation propagates `OperationCanceledException`.
- A typed command cancellation returns a normal `Cancelled` encounter result.
- Port exceptions return a `Faulted` result with `BattleEncounterFaultCode`.
- Command rejection returns `CommandRejected` and consumes no turn.

Fault finalization attempts battle-end lifecycle exactly once after the
structural `BattleStarted` event has been accepted and, when configured,
successfully published. That boundary occurs before battle-start lifecycle, so
a failure inside battle-start lifecycle still receives cleanup. A fault before
the structural event is accepted receives none.

Do not catch operational cancellation and convert it to a gameplay outcome
unless the game explicitly owns that higher-level policy.

## Automated Battles

`AutomatedBattleRunner.RunAsync` is a focused composition over the same
canonical encounter runner. It:

- uses `IBattleActionSelector`;
- executes catalog-authorized skills;
- shares encounter-only knowledge within each team;
- applies the configured lifecycle and turn economy;
- preserves a null command target for valid untargeted skills;
- publishes every ordered host-action request returned by skill execution;
- maps a successful skill escape request to the canonical `Escape` outcome;
- returns the complete `IReadOnlyList<BattleEncounterEvent>`.

Its top-level outcome preserves the canonical result exactly as `Victory`,
`Defeat`, `Escape`, `Draw`, `Faulted`, or `Cancelled`. Hosts do not need to
infer terminal meaning from event text or collapse escape/cancellation into a
draw.

`DeterministicBattleActionSelector` uses authored skill order, assessments, and
available knowledge. It is reference behavior, not a full game AI.

## Godot Mapping

A Godot host commonly keeps:

```text
RuntimeInstanceId -> Node
```

When an event arrives, look up its actor or target runtime ID and enqueue the
appropriate scene animation. The Node never enters Framework state.

Use an asynchronous signal-backed command source inside the turn handler. A
scene exit or application shutdown cancels the encounter token. Scene changes
after `Victory`, `Escape`, or `Draw` remain host-owned.

## Integration Checklist

- Runtime participant IDs are unique.
- Initiative returns an exact team permutation.
- Scheduler and turn economy are selected independently.
- Phase and encounter progress policies are both configured.
- Command Back remains inside the host selection loop.
- The turn handler or restriction resolver enacts every committed non-CanAct
  restriction.
- Restricted automated selections use the typed command's canonical action ID.
- The handler returns consumption but never applies it.
- Lifecycle and command events use only port-owned kinds.
- Port event identities belong to the frozen participant graph.
- The sink consumes typed payloads, not debug text.
- UI and engine code awaits `RunAsync`.
- Rewards and recruitment run after the encounter result through their own
  services.
