# Status And Passive Lifecycle Runtime

## Scope

This reference documents the internal authority, ordering, transaction, event,
and persistence invariants for ailments, timed state, cleanup, and passive
triggers. Stat-modifier policy internals are documented separately in
[Stat Modifier Policy Runtime Authority](stat-modifier-policy-runtime.md).

## Authority Map

```mermaid
flowchart TD
    C["Typed content definitions"] --> A["RuntimeActorState"]
    A --> L["BattleStatusLifecycleService"]
    P["Injected lifecycle policies"] --> L
    E["BattleExecutionServices"] --> L
    L --> T["RuntimeActorExecutionTransaction"]
    T --> R["Immutable lifecycle results"]
    R --> M["BattleStatusLifecycleEventMapper"]
    M --> B["Typed BattleEncounterEvent payloads"]
    B --> H["Host presentation and scene mapping"]
```

`RuntimeActorState` is the live authority. Definitions specify behavior;
policies decide configurable conflicts; lifecycle services stage mutations;
results and encounter events expose committed evidence. Host presentation is
downstream and never rule authority.

## Lifetime Model

`StatusLifetimeDefinition` contains:

1. one `DurationDefinition` expiration; and
2. one immutable `StatusRemovalProfileDefinition`.

Duration and persistence are deliberately independent. Clock-driven Instant,
Turn, Phase, and Battle durations must permit `DurationExpired`; construction
rejects a profile that would make their clock impossible to complete.

The live duration guard rejects null values, non-positive counted turns,
invalid event IDs, invalid phase IDs, and undefined duration kinds before any
public timed-state mutator stores them.

The same lifetime shape is used by ailments, charges, shields, affinity Break,
affinity overrides, and other timed status state.

Instant expiration belongs to the end of the outermost
`OrderedEffectExecutor` scope. A passive or ailment trigger therefore has one
action-end boundary for its complete ordered sequence; nested execution does
not expire state early. The executor returns those completion events separately
from effect-owned events so callers can retain the exact expiry evidence
without duplicating grants or other effect events.

## Ailment Application Transaction

```mermaid
flowchart TD
    A["Create transaction over source, target, participants"] --> B["Validate lifetime and request"]
    B --> C{"Target defeated?"}
    C -- "Yes" --> X["Return rejection; do not commit"]
    C -- "No" --> D["Application gate policy"]
    D --> E{"Gate accepted?"}
    E -- "No" --> X
    E -- "Yes" --> F["Resistance and passive modifiers"]
    F --> G{"Immune?"}
    G -- "Yes" --> X
    G -- "No" --> H["Chance policy"]
    H --> I{"Roll accepted?"}
    I -- "No" --> X
    I -- "Yes" --> J["Transition policy"]
    J --> K{"Decision shape valid and accepted?"}
    K -- "No" --> X
    K -- "Yes" --> L["Apply / refresh / replace staged state"]
    L --> M["Build typed transition and events"]
    M --> N["Commit all staged actors"]
```

The public direct-application path and typed effect path share this staged
authority. Extension policies receive staged actors. A false result, malformed
decision, exception, or transition rejection cannot publish partial live actor
mutation.

For exclusivity replacement, each removed ailment must allow
`ExclusivityReplacement`. Otherwise the transition rejects with
`ReplacementProtected`.

## Turn-Start Resolution

`ProcessTurnStart` creates a one-actor transaction and follows this order:

1. clear Guard and add a typed `GuardCleared` event;
2. resolve each active ailment's turn behavior;
3. validate custom-handler results;
4. combine restrictions through `IBattleTurnRestrictionPolicy`;
5. add one typed restriction event; and
6. commit.

The supplied resolver ranks recall/flee, skip, confusion, basic attack,
limited actions, then normal action. Equal limited-action restrictions are
intersected. Deterministic source-ID ordering resolves equal non-limited ties.

Chance-skip and flee behavior use the injected `IRandomSource`. Invalid random
values fail at the host-random boundary rather than indexing or selecting an
unrelated outcome.

## Owner-Turn-End Pipeline

```mermaid
flowchart TD
    A["Begin staged owner + participant graph"] --> B{"Owner deployed?"}
    B -- "No" --> Z["Return empty owner-turn result"]
    B -- "Yes" --> C["Dispatch owner-turn passive triggers"]
    C --> D["Execute ailment triggers in active-state order"]
    D --> E["Resolve authored recovery event or natural recovery"]
    E --> F["Advance matching status durations"]
    F --> G["Advance matching stat-modifier boundary"]
    G --> H["Commit and return ordered evidence"]
```

If an ailment trigger's ordered effect pipeline stops, later ailment triggers
do not run. Recovery and duration processing still follow, because those are
separate lifecycle steps rather than later effects in the stopped pipeline.

Natural recovery computes:

`floor(baseChance + positiveStat * statMultiplier)`

and clamps the policy-derived result to `0..100`. Authored base chance must be
`0..100`; the multiplier cannot be negative. Overflow saturates to 100.

## Explicit Clock State Machine

```mermaid
flowchart TD
    A["Encounter emits semantic boundary"] --> B{"Boundary kind"}
    B -- "Actor turn" --> C["Select exactly the matching actor"]
    B -- "Action" --> D["Evaluate instant expiry across participants"]
    B -- "Team phase" --> E["Use explicit team, phase, and event IDs"]
    B -- "Round" --> F["Use explicit round-end event ID"]
    B -- "Custom" --> G["Match authored custom event ID"]
    C --> H["Evaluate deployed actor state"]
    D --> H
    E --> I["Evaluate deployed and policy-approved reserve state"]
    F --> I
    G --> H
    I --> J{"Duration suspends while reserve?"}
    J -- "Yes" --> K["Retain exact remaining state"]
    J -- "No" --> H
    H --> L["Emit before/after tick or expiry evidence"]
```

Actor-turn selection faults if a matching runtime ID is absent or ambiguous.
Team-phase mapping comes from `IBattleEncounterLifecycleClockPolicy`; team IDs
are never treated as phase IDs. Reserve advancement accepts only TeamPhase or
Round policies and, for TeamPhase, only the reserve actor's owning team.

Boundary sequence values support idempotent stat-modifier lifecycle handling.
They do not make field time advance automatically.

## Cleanup Transaction

```mermaid
flowchart TD
    A["Cleanup request with departure reason"] --> B["Clone actor state"]
    B --> C["Clear Guard"]
    C --> D{"Battle end?"}
    D -- "Yes" --> E["Expire BattleDuration state"]
    D -- "No" --> F["Continue"]
    E --> F
    F --> G["Map departure reason to removal cause"]
    G --> H["Remove only state whose profile allows cause"]
    H --> I["Apply stat-modifier cleanup scope"]
    I --> J["Build exact removals, expiries, and cleanup event"]
    J --> K["Commit atomically"]
```

Battle-end duration expiry occurs before general battle-end removal so events
retain the correct cause. Cleanup operates on all timed-state families and the
selected stat-modifier service. Unknown or rejected modifier transitions abort
the transaction.

Direct status-removal effects use `RemoveNonModifierStatuses`, which returns
one `BattleStatusRemovalResult` per committed charge, shield, affinity Break,
affinity override, or other-status removal. `RemoveStatusEffectExecutor`
projects each transition into a typed `StatusRemoved` lifecycle event.
Protected and missing state returns no transition and no event.

## Passive Dispatcher Authority

### Target resolution

The surrounding execution transaction first requires every distinct actor
object to have a unique runtime ID. Two different objects claiming one ID are
rejected before target policies run. `PassiveTriggerTargetResolver` then
evaluates the typed scope over that validated graph, filters by life state and
reserve inclusion, and normalizes repeated references while preserving
encounter order.

`PassiveEventPolicy.OwnerEligibility` is evaluated before target resolution.
Generic unregistered events use `AllParticipants`.
`BattleStatusEncounterLifecyclePort` registers `DeployedOnly` for battle start
when no host policy exists; a prior explicit `AllParticipants` registration is
preserved. Owner eligibility and target reserve inclusion are independent
decisions.

### Dispatch order

The nested order is:

1. enabled passive loadout order;
2. trigger index;
3. resolved target order; and
4. ordered effect index.

The passive owner is the condition actor. The resolved target is the condition
target and effect target.

### Recursion and counts

The active recursion key is:

`owner instance + passive skill + trigger index + event ID`

Re-entry is suppressed unless the event policy permits it. A reentrant policy
must carry a finite positive activation limit.

Per-dispatch activation keys omit target identity. The first condition-accepted
target records one activation and the full eligible fan-out proceeds. Per-target
keys include the target runtime ID and are checked independently. A
condition-not-met target does not record an activation.

### Atomic dispatch

`BattleExecutionServices` wraps its supplied or replacement dispatcher in one
canonical validating transaction over owner, participants, and event targets.
Before commit, the wrapper captures enabled passive definitions and requires
each returned activation to match the requested event, an authored trigger
index, and a target selected by that trigger over the participant graph. It
rejects duplicate activation evidence, out-of-range or mismatched authored
effect evidence, foreign actor IDs, and effects attached to a non-executed
outcome.

The standard `PassiveTriggerDispatcher` retains its own transaction so it is
also safe when used directly. Effects run against staged actors. Activation
counts and all effect mutations commit together. A thrown effect or malformed
replacement result leaves actor state and activation counts unchanged. Skill
execution translates such an exception into its typed `ExecutionFailed`
rejection; lifecycle ports propagate it to their encounter fault boundary.

## Encounter Startup Atomicity

```mermaid
sequenceDiagram
    participant R as Encounter Runner
    participant T as Startup Transaction
    participant L as Lifecycle Port
    participant S as Event Sink

    R->>T: Clone complete participant graph
    R->>T: Reset staged per-battle passive counts
    R->>S: Publish actor, battle-start, and initiative events
    alt cancellation or initial publication failure
        R-->>T: Discard staged graph
    else initial events accepted
        R->>L: Dispatch staged battle-start passives
        L-->>R: Validate and return typed lifecycle events
        alt cancellation or lifecycle failure
            R-->>T: Discard staged graph
        else lifecycle accepted
            R->>T: Commit all participants once
            R->>S: Publish lifecycle evidence
            Note over R,S: A later sink failure faults the encounter; it cannot roll back committed state
        end
    end
```

No participant's activation counters or battle-start mutations become live
until lifecycle validation succeeds and the complete staged graph commits.
External event publication is deliberately outside the actor transaction. Host
sinks must not assume that throwing can undo framework state or prior host side
effects.

## Event Evidence

Lifecycle events do not make `Detail` authoritative. Typed fields carry:

- gate decisions;
- apply, refresh, replace, and rejection transitions;
- exact before/after duration values;
- exact removal cause and removed ID;
- stat-modifier transition data;
- passive source, event, trigger, target, outcome, and full effect result; and
- cleanup reason.

`BattleStatusLifecycleEventMapper` validates the specialized passive and effect
payload combinations it maps. Generic status events are wrapped in
`BattleStatusChangedEventPayload`; the mapper does not claim to revalidate
every possible generic status-field combination. Outermost action-completion
expiry is retained in active action results and in passive or ailment trigger
results instead of being discarded.

## Persistence And Restore

Runtime save contract v13 serializes the status lifetime rather than only the
remaining number. This preserves expiration kind, event or phase identity,
reserve behavior, and allowed removal causes.

Passive activation snapshots preserve:

`skill + trigger index + event + optional target + count`

The optional target is present only for per-target accounting. Save validation
requires it to reference a saved actor. Duplicate keys and invalid counts are
rejected before aggregate restoration. Restored state is normalized through
the same runtime validity guards as live state.

## Failure Containment

Framework transactions cover framework actor state. They do not compensate
external host operations. Integration code must observe this sequence:

1. ask the framework to assess or execute against staged state;
2. receive an immutable accepted result;
3. publish or apply host-side presentation; and
4. perform external irreversible work only at an explicitly owned host commit
   boundary.

Custom handlers should therefore be deterministic rule adapters, not scene or
storage mutators.

## Authored Lifetime Boundary

Schema v8 maps authored lifetime policy without inference. Each ailment
`defaultLifetime` and each applicable status-producing effect `lifetime`
contains an expiration definition plus the exact typed removal causes allowed
for that state. `SkillSystemDtoMapper.MapStatusLifetime` preserves both parts
when constructing `StatusLifetimeDefinition`.

Finite Instant, Turn, Phase, and Battle expirations must permit
`DurationExpired`; both JSON Schema and the runtime definition constructor
enforce that invariant. Permanent state may omit automatic expiry. Duplicate
or unknown causes are rejected by the authoring contract. Stat modifiers keep
their separate `duration` shape because the selected stat-modifier policy owns
contribution expiry rather than `StatusLifetimeDefinition`.

## Source And Test Evidence

Primary source areas:

- `Content/StatusLifetimes.cs`
- `Content/AilmentDefinition.cs`
- `Content/Passives.cs`
- `Execution/BattleAilmentApplicationGates.cs`
- `Execution/BattleAilmentTransitions.cs`
- `Execution/BattleLifecycleClocks.cs`
- `Execution/BattleRuntimeState.cs`
- `Execution/BattleStatusLifecycle.cs`
- `Execution/OrderedEffectExecutor.cs`
- `Execution/PassiveRuntime.cs`
- `Encounters/BattleEncounterLifecycleClocks.cs`
- `Encounters/BattleStatusEncounterLifecyclePort.cs`
- `Runtime/RuntimePersistenceSnapshots.cs`

Primary executable evidence:

- `BattleStatusLifecycleTests`
- `BattleAilmentTransitionTests`
- `BattleLifecycleClockTests`
- `PassiveRuntimeTests`
- `BattleStatusLifecycleEventMapperTests`
- `BattleEncounterRunnerTests`
- `RuntimePersistenceContractTests`
- `GodotIntegrationContractTests`
