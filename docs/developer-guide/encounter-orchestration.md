# Encounter Orchestration Integration

## Purpose

This guide shows how a host composes and runs `BattleEncounterRunner`. The
runner owns orchestration; the host supplies policies, commands, event
presentation, and any mapping between framework runtime IDs and scene objects.

Engine and UI hosts should always await `RunAsync`. The synchronous `Run`
method exists only as a compatibility convenience for non-UI callers.

> **Current review status:** O6-R14 found incomplete automated handling for
> untargeted and terminal skill results. Use the automated convenience runner
> only for its currently covered targeted actions until O6-R17 is complete.

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
| `BattlePhaseProgressPolicy` | Bounds total commands and consecutive free actions per phase. |
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
team. It rotates through available actors while the phase economy has
opportunities.

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

`BattleEncounterProgressPolicy` is a separate encounter-wide liveness guard.
It limits accepted scheduler transitions, including round and phase boundaries
that do not open a command window. `BattlePhaseProgressPolicy` instead limits
commands and consecutive free actions inside one phase. Supply both policies:
neither can replace the other.

## Implementing The Turn Handler

`IBattleEncounterTurnHandler.ExecuteTurnAsync` receives:

- the scheduled actor and current participant list;
- the committed turn-start restriction;
- the accepted turn-economy snapshot;
- any active stat-modifier lifecycle boundaries.

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
depart.

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
knowledge, analysis, passive, and lifecycle evidence. Command events must name
the actor who owns the current command window, and presence-change events must
report that actor's actual encounter team. Combat-profile source identity is
provenance rather than a routing target, so a Vessel may still identify a
non-deployed Hosted Entity as its profile source.

An event-sink exception becomes `EventPublicationFailed`. During fault
finalization, a second sink failure stops further publication but preserves
the immutable returned event evidence.

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

The supplied `LastTeamStandingCompletionPolicy` ends when one deployed, living
team remains.

## Cancellation And Fault Handling

Pass the Godot scene, task, or application cancellation token to `RunAsync`.
The runner checks cancellation before port calls and before staged lifecycle
commits.

- Token cancellation propagates `OperationCanceledException`.
- A typed command cancellation returns a normal `Cancelled` encounter result.
- Port exceptions return a `Faulted` result with `BattleEncounterFaultCode`.
- Command rejection returns `CommandRejected` and consumes no turn.

Do not catch operational cancellation and convert it to a gameplay outcome
unless the game explicitly owns that higher-level policy.

## Automated Battles

`AutomatedBattleRunner.RunAsync` is a focused composition over the same
canonical encounter runner. It:

- uses `IBattleActionSelector`;
- executes catalog-authorized skills;
- shares encounter-only knowledge within each team;
- applies the configured lifecycle and turn economy;
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
- The handler returns consumption but never applies it.
- Lifecycle and command events use only port-owned kinds.
- Port event identities belong to the frozen participant graph.
- The sink consumes typed payloads, not debug text.
- UI and engine code awaits `RunAsync`.
- Rewards and recruitment run after the encounter result through their own
  services.
