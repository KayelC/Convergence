# Status And Passive Lifecycle Integration

## Integration Boundary

The status module is optional. A Godot or other host composes it when the game
uses ailments, lifecycle cleanup, or event-driven passive skills.

The framework owns:

- runtime status and passive state;
- application, transition, restriction, duration, recovery, and cleanup rules;
- staged mutation and rollback;
- deterministic ordered lifecycle results; and
- serializer-neutral snapshots.

The host owns:

- content text and registration;
- scene objects, input, animation, sound, and UI;
- the `IRandomSource` implementation;
- encounter IDs and lifecycle-clock mapping;
- storage and save-file encoding; and
- irreversible external effects performed by custom handlers.

## Required Composition

A canonical encounter composition needs:

1. a validated `GameDataCatalog` and the registrations used to build it;
2. `BattleExecutionServices`, including passive and stat-modifier services;
3. `BattleStatusLifecycleService` with random, transition, application-gate,
   turn-restriction, custom-handler, and reserve policies as needed;
4. `PassiveEventPolicyRegistry` for owner eligibility, re-entry, or activation
   limits;
5. explicit battle-start and owner-turn-end event IDs; and
6. `IBattleEncounterLifecycleClockPolicy` mapping each team to distinct phase
   and event IDs plus one round-end event ID.

The supplied baseline can be composed as follows:

```csharp
var lifecycleService = new BattleStatusLifecycleService(randomSource);

var clockPolicy = new ExplicitBattleEncounterLifecycleClockPolicy(
    [
        new BattleTeamPhaseClockDefinition(
            playerTeamId,
            playerPhaseId,
            playerPhaseEndEventId),
        new BattleTeamPhaseClockDefinition(
            enemyTeamId,
            enemyPhaseId,
            enemyPhaseEndEventId)
    ],
    roundEndEventId);

var lifecyclePort = new BattleStatusEncounterLifecyclePort(
    lifecycleService,
    executionServices,
    battleStartEventId,
    ownerTurnEndEventId,
    clockPolicy);
```

The encounter runner calls that port at battle start, turn start, owner turn
end, team phase end, round end, and battle end. A custom runner must preserve
the same semantic boundaries rather than ticking state ad hoc.

## Selecting Policies

### Ailment application gate

`GuardBlocksAilmentsApplicationGatePolicy` is the supplied standard. It rejects
an ailment while the target is guarding. Use
`AllowAilmentsApplicationGatePolicy` or an implementation of
`IBattleAilmentApplicationGatePolicy` when the game follows another rule.

The gate is evaluated before resistance and chance. It returns a typed decision
which appears in the lifecycle result.

### Ailment transition

Choose one `IBattleAilmentTransitionPolicy`:

- `StandardBattleAilmentTransitionPolicy`: refresh same, replace exclusive;
- `RejectExistingAilmentTransitionPolicy`: reject every existing conflict; or
- `RefreshExistingAilmentTransitionPolicy`: refresh same, reject exclusive.

Custom decisions are validated before mutation. A policy cannot claim an
incompatible operation/result shape.

### Turn restrictions

`MostRestrictiveBattleTurnPolicy` is the supplied resolver. It applies explicit
precedence and intersects equally strong limited-action sets. Implement
`IBattleTurnRestrictionPolicy` only when the game's conflict rule differs.

### Reserve aging

`SuspendReserveLifecyclePolicy` is the supplied default. To age reserve state,
inject `AdvanceReserveOnEncounterClockPolicy` with either one exact
`TeamPhase` event or one exact `Round` event. Actor-turn and action clocks are
rejected so a larger action economy cannot age reserve state faster.

`TurnDurationDefinition.SuspendWhileReserve` remains an individual status
override. A value of `true` suspends that status even when the aggregate reserve
policy permits the actor to advance.

## Authoring A Passive Trigger

Schema-v8 content must declare targeting explicitly:

```json
{
  "event": "owner_turn_end",
  "targeting": {
    "scope": "owner_team",
    "lifeState": "alive",
    "includeReserveActors": false
  },
  "effects": [
    {
      "type": "restore_resource",
      "resourceId": "hp",
      "amount": { "type": "flat", "value": 4 }
    }
  ]
}
```

Supported scopes are `owner`, `event_targets`, `owner_team`,
`opposing_teams`, and `all_participants`. Target life state is `alive`, `dead`,
or `any` according to the shared target contract.

The in-memory three-argument `PassiveTriggerDefinition` constructor exists for
programmatic definitions and defaults to event targets. Authored JSON does not
receive that fallback.

## Registering Passive Event Policies

Unregistered events use the non-reentrant, unlimited, per-dispatch,
`AllParticipants` default. `BattleStatusEncounterLifecyclePort` installs a
`DeployedOnly` policy for its battle-start event only when the host has not
already registered that event. Register `AllParticipants` before constructing
the port when reserve-owned battle-start passives are intended:

```csharp
var passivePolicies = new PassiveEventPolicyRegistry()
    .Register(
        battleStartEventId,
        new PassiveEventPolicy(PassiveOwnerEligibility.AllParticipants))
    .Register(
        reactionEventId,
        new PassiveEventPolicy(
            AllowReentry: true,
            ActivationLimitPerBattle: 2,
            ActivationCountingScope: PassiveActivationCountingScope.PerTarget,
            OwnerEligibility: PassiveOwnerEligibility.DeployedOnly));

var passiveDispatcher = new PassiveTriggerDispatcher(passivePolicies);
```

Re-entry without a finite positive limit is rejected at construction. A
per-dispatch limit records one successful fan-out; a per-target limit records
one count for each target. Condition failures do not consume a count. Owner
eligibility is evaluated before target resolution. It is independent from
`PassiveTriggerTargetingDefinition.IncludeReserveActors`, which controls
reserve targets rather than reserve owners.

## Applying An Ailment Directly

Use `IBattleStatusLifecycleService.TryApplyAilment` when application is not
already part of a typed skill or item effect. Supply every participant that a
condition, passive modifier, or transaction may inspect.

```csharp
BattleAilmentApplicationResult result = lifecycleService.TryApplyAilment(
    new BattleAilmentApplicationRequest(
        sourceActor,
        targetActor,
        ailment,
        chance: 70,
        participants: encounterActors,
        battleKindId: battleKindId),
    executionServices);
```

Inspect `result.Status`, `result.GateDecision`, `result.Transition`, and
`result.Events`. Gate rejection detail is carried by the typed gate decision;
same-ailment, exclusivity, and replacement detail is carried by the transition;
ordered presentation evidence is carried by the events. The result has no
`Diagnostics` collection. Do not mutate the target first and treat the service
as a validator. The service stages all involved actors and commits only an
accepted result.

Skill and item effect execution already uses this canonical application path.

## Lifecycle Clocks

Use typed boundaries instead of passing loosely related IDs:

- `ActorTurnLifecycleClockBoundary` advances one matching actor-turn event;
- `ActionLifecycleClockBoundary` explicitly expires Instant state when a host
  executes outside the standard outermost ordered-effect scope;
- `TeamPhaseLifecycleClockBoundary` carries distinct event, team, and phase
  IDs;
- `RoundLifecycleClockBoundary` advances one authored round-end event; and
- `CustomLifecycleClockBoundary` advances a host-defined event without hidden
  encounter meaning.

A `BattleLifecycleClockRequest` applies one boundary atomically over a fixed
participant set. Duplicate object references are removed. Associated
stat-modifier boundaries must be valid and unique by event ID.

Do not dispatch battle clocks while the game is in field state unless the game
design intentionally ages that state there.

## Cleanup And Departure

Call `Cleanup` with the actual departure reason:

```csharp
BattleStatusLifecycleResult result = lifecycleService.Cleanup(
    new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.Flee),
    executionServices.StatModifiers);
```

Deployment swap, defeat, flee, roster recall, battle end, and field transition
are distinct causes. The status's removal profile decides whether that cause is
allowed. Do not substitute battle-end cleanup for every scene transition.

Changing an Active Hosted Entity does not mean the Vessel departed. Compose the
new profile without calling actor-departure cleanup.

## Consuming Events

`BattleStatusLifecycleEvent` contains typed transition fields for:

- application gates and ailment apply/refresh/replace results;
- duration before/after transitions;
- exact status removals and causes;
- stat-modifier events;
- passive event, trigger index, target, outcome, and effect result; and
- cleanup departure reason.

`StatusRemoved` events carry `RemovalTransition`, including the exact status
family, removed ID, and cause. Passive and ailment trigger activations retain
their outer-scope completion events, so an Instant grant and its expiry are
both observable even though the state no longer exists after dispatch.

`BattleStatusLifecycleEventMapper` converts committed status events into typed
`BattleEncounterEvent` payloads. Treat debug text as optional. A Godot host can
map runtime IDs to Nodes and map event kinds to animations without parsing the
message.

## Custom Handlers And Side Effects

Custom ailment turn handlers, custom effect handlers, and replacement passive
dispatchers execute against staged framework actors. A passive dispatcher may
choose its evaluation algorithm, but returned activations are checked against
the request's enabled skills, authored trigger indexes and event, eligible
participant targets, and effect/outcome shape. Returning malformed data or
throwing prevents staged actor state from committing.

The framework cannot roll back a file write, network call, scene deletion, or
other external host side effect. Custom handlers should therefore return a
typed decision first and defer irreversible host work until the accepted event
is published.

## Persistence

Save contract v13 preserves:

- active ailments and other timed state with expiration and removal profile;
- stat-modifier and charge-policy state;
- enabled passive IDs;
- per-battle activation counts; and
- optional per-target activation IDs.

Validation rejects malformed durations, invalid enums and IDs, duplicate
activation keys, and per-target activation IDs that do not reference a saved
actor. Aggregate restore validates before exposing restored session state.

The host owns JSON or another save encoding. Preserve all typed fields rather
than rebuilding state from icons or display names.

## JSON Lifetime Authoring

Schema-v8 JSON constructs the same `StatusLifetimeDefinition` available to
programmatic integrations. Ailments use `defaultLifetime`; applicable typed
effects use `lifetime`. Each contains `expiration` and
`allowedRemovalCauses`, so content does not acquire hidden Deployment,
Encounter, or Field persistence from its duration kind.

For a finite Instant, Turn, Phase, or Battle expiration, include
`duration_expired`. Permanent state may instead list only explicit removal
causes such as `scripted_removal`. Keep stat-modifier `duration` records
separate: their selected accumulation policy owns those timers.

## Verification References

- `BattleStatusLifecycleTests`
- `BattleAilmentTransitionTests`
- `BattleLifecycleClockTests`
- `PassiveRuntimeTests`
- `BattleStatusLifecycleEventMapperTests`
- `BattleEncounterRunnerTests`
- `RuntimePersistenceContractTests`
- `GodotIntegrationContractTests`

See [Status And Passive Lifecycle](../mechanics/status-passive-lifecycle.md)
for player-visible rules and
[Status And Passive Lifecycle Technical Reference](../technical/status-passive-lifecycle.md)
for mutation and event invariants.
