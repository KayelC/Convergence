# Stat Modifier Policy Runtime Authority

## Scope

This reference defines the implemented authority, state, transaction, lifecycle,
and restore invariants for the stat-modifier family. It covers all three
supplied policies through M1-8.

The central invariant is:

> One bound `IStatModifierPolicyService` is the only authority that may derive
> canonical modifier state for a live actor.

Neither an effect executor, lifecycle service, host, save codec, nor combat
resolver may directly edit a resolved stage or duration.

## Authority Flow

```mermaid
flowchart TD
    subgraph Content["Authored selection"]
        R["RulesetDefinition: stat_modifier"]
        F["Registered policy factory"]
    end
    subgraph Runtime["Framework authority"]
        B["RuntimeRulesetBindingResolver"]
        S["StatModifierPolicyService"]
        P["Selected IStatModifierPolicy"]
        A["RuntimeActorState.StatModifierState"]
    end
    subgraph Consumers["Canonical consumers"]
        E["Skill, item, and passive execution"]
        L["Battle lifecycle"]
        C["Combat stage projection"]
        V["Save validation and restore"]
    end

    R --> B
    F --> B
    B --> S
    S --> P
    E --> S
    L --> S
    S --> A
    A --> C
    A --> V
```

`RuntimeActorState.StatStages` is a read-only aggregate projection of canonical
policy state. It is not a second mutable authority.

## Neutral Immutable State

`RuntimeStatModifierStateSnapshot` contains:

- one qualified policy ID;
- ordered `RuntimeStatModifierTrackSnapshot` values.

Each track contains:

- a valid modifier-track ID;
- a resolved nonzero stage;
- one or more ordered contribution snapshots.

Each contribution contains:

- a globally unique positive sequence;
- a signed nonzero stage delta;
- optional typed duration;
- optional last-observed lifecycle boundary.

The neutral service validates IDs, duplicate tracks and sequences, duration
shape, boundary shape, integer arithmetic, and policy identity. The selected
policy then validates its own bounds, contribution count, projection, and
duration requirements.

Snapshots sort tracks by ID and contributions by sequence. Public collections
are read-only snapshots, so caller-owned arrays cannot mutate a retained result.

## Service Containment

`StatModifierPolicyService` wraps extension policy code:

```mermaid
flowchart TD
    Q["Immutable request"] --> N{"Neutral request and state valid?"}
    N -->|"no"| R["Rejected; Before == After"]
    N -->|"yes"| P["Invoke selected policy"]
    P -->|"throws"| R
    P -->|"rejects"| R
    P -->|"returns state"| V{"Policy result validates?"}
    V -->|"no"| R
    V -->|"yes"| D["Derive ordered diff events"]
    D --> T["Applied or Unchanged transition"]
```

Policy exceptions other than process-fatal memory exhaustion become
`PolicyFaulted` diagnostics. Rejection never exposes a partial `After` state.

`AssessApplication` and `Apply` evaluate the same immutable policy path. Skill
and item prepared assessments additionally compare actor revisions, prepared
targets, definitions, context, and active boundaries before execution.

## Policy State Machines

### Persistent Staged

One contribution stores the net stage. Application computes
`clamp(current + delta, minimum, maximum)`. Stored duration and lifecycle cursor
must be null. `Tick` is unchanged.

### Timed Exclusive

One contribution stores one of `-2`, `-1`, `+1`, or `+2` and one counted
duration.

```mermaid
stateDiagram-v2
    [*] --> Neutral
    Neutral --> Positive: apply +
    Neutral --> StrongPositive: apply ++
    Neutral --> Negative: apply -
    Neutral --> StrongNegative: apply --

    Positive --> Positive: + / restart timer
    Positive --> StrongPositive: ++ / replace
    StrongPositive --> StrongPositive: + / reject weaker
    Positive --> Neutral: - / cancel
    StrongPositive --> Positive: - / keep existing timer
    Positive --> Negative: -- / use incoming timer

    Negative --> Negative: - / restart timer
    Negative --> StrongNegative: -- / replace
    StrongNegative --> StrongNegative: - / reject weaker
    Negative --> Neutral: + / cancel
    StrongNegative --> Negative: + / keep existing timer
    Negative --> Positive: ++ / use incoming timer
```

The weaker same-sign rejection is `AlreadyInEffect`. Equal opposite magnitudes
remove the complete track.

### Timed Contributions

Every accepted application normally appends one contribution. The projection
is:

```text
resolved = clamp(sum(contribution.StageDelta), minimum, maximum)
```

At a same-direction cap, application refreshes the lowest-sequence contribution
of that sign instead of appending hidden state. Selected-contribution removal
can therefore remove one timed application without removing the complete
track.

## Ordered Effect Transaction

For an active skill or item, modifier execution occurs inside the same staged
actor transaction as all other effects:

```mermaid
sequenceDiagram
    participant Host
    participant Action as BattleActionExecutor
    participant Inventory
    participant Effects as Skill/Item Executor
    participant Modifier as StatModifierPolicyService
    participant Actor as Live RuntimeActorState

    Host->>Action: assess typed command
    Action->>Effects: assess ordered effects on clones
    Effects->>Modifier: assess immutable transition
    Modifier-->>Effects: accepted, unchanged, or rejected
    Effects-->>Action: prepared result
    Host->>Action: execute prepared assessment
    Action->>Inventory: reserve one item when applicable
    Action->>Effects: execute against staged actors
    Effects->>Modifier: apply same transition inputs
    Modifier-->>Effects: immutable state and events
    alt all commits succeed
        Action->>Actor: commit staged actor revisions
        Action->>Inventory: commit reservation
    else any execution or inventory commit rejects
        Action->>Inventory: roll back reservation
        Action-->>Host: rejected; live actors unchanged
    end
```

A modifier effect reports meaningful success only when its canonical transition
changes state. Typed removal follows the same path. Ordered effect failure rolls
back earlier modifier changes because only staged actor clones were changed.

## Lifecycle Boundary Model

Counted duration uses `TurnDurationDefinition` and
`StatModifierLifecycleBoundary(eventId, sequence)`.

The complete boundary identity is an event ID + positive monotonic boundary sequence.

- Event ID chooses the clock.
- Sequence is positive and monotonic within that clock's scope.
- A matching active boundary stamps application or refresh.
- Completion of the same sequence does not decrement the contribution.
- Repeating the latest sequence is idempotent.
- An older sequence rejects the complete tick.
- A later matching sequence decrements once or expires at one.
- A nonmatching event leaves the contribution unchanged.
- Reserve suspension records the later sequence without decrementing.

```mermaid
flowchart TD
    T["Receive lifecycle boundary"] --> M{"Event ID matches duration?"}
    M -->|"no"| U["Unchanged"]
    M -->|"yes"| O{"Sequence older than cursor?"}
    O -->|"yes"| R["Reject complete tick"]
    O -->|"no"| S{"Same sequence already observed?"}
    S -->|"Applied in this exact boundary or already observed"| U
    S -->|"no"| D{"Reserve suspension active?"}
    D -->|"yes"| C["Advance cursor only"]
    D -->|"no"| V{"Remaining value is 1?"}
    V -->|"yes"| X["Expire contribution"]
    V -->|"no"| K["Decrement once and advance cursor"]
```

The canonical `BattleStatusEncounterLifecyclePort` owns per-actor owner-turn
sequences and supplies `owner_turn_end` to turn-end lifecycle processing. The
lower-level duration lifecycle also accepts action-end and phase-end boundary
collections from schedulers that define those clocks.

## Lifecycle Commit And Cleanup

Lifecycle work uses staged encounter or actor transactions. Modifier ticks,
passive effects, ailment effects, and resource effects commit together. A custom
handler fault cannot leave only the modifier portion live.

All supplied policies use the same cleanup rules:

| Scope | Result |
|---|---|
| `Swap` | preserve state |
| `ActorDeparture` | clear all modifier state |
| `EncounterEnd` | clear all modifier state |
| `FieldTransition` | clear all modifier state |

## Events

Events are derived from validated before/after snapshots in deterministic track
ID and contribution-sequence order:

1. removed or expired contributions;
2. added contributions;
3. updated contributions;
4. aggregate-stage change;
5. track removal.

`BattleStatusLifecycleEventMapper` carries modifier event payloads into the
encounter event stream. Debug messages are optional; typed IDs, values, and
event kinds are authoritative.

## Ruleset Binding

`RulesetCategory.StatModifier` is independent from `RulesetCategory.Stat`.
The latter binds stat resolution and scaling; the former binds modifier
lifecycle state.

`RuntimeRulesetBindingResolver.BindStatModifierPolicy` validates:

- qualified ruleset lookup;
- `stat_modifier` category;
- registered unqualified policy-factory ID;
- exact allowed parameter names;
- required bounds and coherent signed range;
- factory result and diagnostics.

The standard registry supplies `persistent_staged`, `timed_exclusive`, and
`timed_contribution`. There is no policy fallback after binding failure.

## Save And Aggregate Restore

Save contract v10 serializes canonical state under
`RuntimeBattleStatusSnapshot.StatModifiers`. Validation performs neutral checks,
catalog lookup of the saved qualified policy ID, authored factory binding, and
selected-policy compatibility validation.

```mermaid
flowchart TD
    S["RuntimeSaveGameSnapshot v10"] --> N["Neutral save validation"]
    N --> C{"Policy ruleset exists in catalog?"}
    C -->|"no"| R["Reject aggregate restore"]
    C -->|"yes"| B["Bind authored stat-modifier policy"]
    B -->|"diagnostics"| R
    B --> V["Validate every retained actor state"]
    V -->|"incompatible"| R
    V --> A["Restore actors in dependency order"]
    A --> Q["Revalidate restoration resolver policy"]
    Q -->|"mismatch"| R
    Q -->|"valid"| L["Publish complete live session"]
```

No actor is exposed when aggregate validation fails. Host JSON and Godot save
codecs encode the snapshot but do not reinterpret policy state.

## Source And Test Evidence

Primary source:

- `src/Convergence.Framework/Runtime/StatModifierPolicies.cs`
- `src/Convergence.Framework/Runtime/PersistentStagedStatModifierPolicy.cs`
- `src/Convergence.Framework/Runtime/TimedExclusiveStatModifierPolicy.cs`
- `src/Convergence.Framework/Runtime/TimedContributionStatModifierPolicy.cs`
- `src/Convergence.Framework/Execution/StatModifierExecution.cs`
- `src/Convergence.Framework/Execution/BattleStatusLifecycle.cs`
- `src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs`
- `src/Convergence.Framework/Runtime/RuntimeRulesetPolicyFactories.cs`
- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs`

Executable evidence:

- `tests/Convergence.Framework.Tests/Runtime/StatModifierPolicyContractTests.cs`
- `tests/Convergence.Framework.Tests/Runtime/PersistentStagedStatModifierPolicyTests.cs`
- `tests/Convergence.Framework.Tests/Runtime/TimedExclusiveStatModifierPolicyTests.cs`
- `tests/Convergence.Framework.Tests/Runtime/TimedContributionStatModifierPolicyTests.cs`
- `tests/Convergence.Framework.Tests/SkillSystem/StatModifierExecutionIntegrationTests.cs`
- `tests/Convergence.Framework.Tests/Runtime/RuntimePersistenceSnapshotTests.cs`

## Related Documentation

- [Mechanics](../mechanics/stat-modifier-policies.md)
- [Developer Integration](../developer-guide/stat-modifier-policies.md)
- [Typed Action And Effect Execution](typed-action-and-effect-execution.md)
- [Runtime Actor State And Restoration](runtime-actor-state-and-restoration.md)
