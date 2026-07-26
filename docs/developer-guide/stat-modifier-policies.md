# Using Stat Modifier Policies

## What A Host Must Choose

A host does not own live modifier rules or a second modifier dictionary. It
loads an authored `stat_modifier` ruleset, binds that ruleset to a registered
factory, and passes the returned `IStatModifierPolicyService` into the canonical
execution and lifecycle services.

The selected service owns application, assessment, duration, removal, cleanup,
diagnostics, and immutable retained state. `IStatStageScalingPolicy` remains a
separate choice for numeric combat impact.

## Author A Ruleset

Schema-v8 content selects a factory with the unqualified `policyId` field. A
bounded persistent or timed-contribution policy also requires both bounds:

```json
{
  "id": "standard_stat_modifiers",
  "displayName": "Persistent Stat Modifiers",
  "description": "Keeps bounded stages until explicit cleanup.",
  "category": "stat_modifier",
  "policyId": "persistent_staged",
  "parameters": {
    "minimumStage": -4,
    "maximumStage": 4
  }
}
```

The supplied factory IDs are:

| `policyId` | Parameters | Result |
|---|---|---|
| `persistent_staged` | `minimumStage`, `maximumStage` | bounded persistent net stage |
| `timed_exclusive` | none | one timed signal in `-2`, `-1`, `+1`, `+2` |
| `timed_contribution` | `minimumStage`, `maximumStage` | independently timed signed contributions |

Unknown parameters, missing bounds, a wrong ruleset category, and an
unregistered factory ID produce binding diagnostics. There is no hidden
fallback policy.

The supplied bounded policies may narrow either side of the standard
`-4..+4` domain, including asymmetrically, but they reject a minimum below `-4`
or a maximum above `+4`. That guarantee keeps them compatible with the supplied
stage-scaling tables. A game that needs a different stage domain should supply
both its own `IStatModifierPolicy` and its own `IStatStageScalingPolicy`; the
generic scaling request does not impose the standard four-stage range.

## Bind Once At Composition

```csharp
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Runtime;

var factories = RuntimeRulesetPolicyFactoryRegistry.CreateStandard();
var resolver = new RuntimeRulesetBindingResolver(factories);

RulesetBindingResult<IStatModifierPolicyService> binding =
    resolver.BindStatModifierPolicy(
        catalog,
        ContentId.Parse("my_pack:standard_stat_modifiers"));

IStatModifierPolicyService modifiers = binding.RequireService();
```

Bind once for the session or encounter scope that owns the actors. Do not bind
again per menu selection or effect. The same service must be supplied to:

- `BattleExecutionServices` for skills, items, passives, and typed effects;
- `BattleStatusLifecycleService` or `BattleStatusEncounterLifecyclePort` for
  duration and cleanup;
- `RuntimeSaveValidator` and `RuntimeSessionRestoreService` for policy-aware
  restore validation.

This keeps execution, capture, and restoration under one policy identity.

## Execution And Meaningful Success

`ModifyStatStageEffectDefinition` carries typed track IDs, a signed delta, and
an optional duration. The executor asks the selected policy to assess the
transition before committing costs or inventory.

```csharp
var effect = new ModifyStatStageEffectDefinition(
    [ContentId.Parse("attack")],
    stageDelta: 1,
    new TurnDurationDefinition(
        Value: 3,
        TickEventId: ContentId.Parse("owner_turn_end"),
        SuspendWhileReserve: true));
```

For persistent staged rules, the duration is ignored because the policy stores
no counted duration. Both timed policies require `TurnDurationDefinition`.
Supplying an incompatible shape returns typed rejection rather than silently
changing the authored effect.

Assessment and execution must use the same actor state, targets, active
lifecycle boundaries, and policy. Prepared skill assessment already enforces
that contract. If state becomes stale, execution rejects before mutation.

For items, Framework reserves one item before staged execution. It commits the
reservation only after at least one effect produces meaningful success. A
rejected inventory commit rolls actor state and the reservation back.

## Lifecycle Boundaries

Timed contributions decrement only when their authored event ID receives a
positive monotonic `StatModifierLifecycleBoundary` sequence.

```csharp
var boundary = new StatModifierLifecycleBoundary(
    ContentId.Parse("owner_turn_end"),
    sequence: 12);

BattleTurnEndLifecycleResult result = lifecycle.ProcessTurnEnd(
    new BattleTurnEndLifecycleRequest(
        actor,
        participants,
        contextId,
        ContentId.Parse("owner_turn_end"),
        battleKindId,
        statModifierBoundary: boundary),
    executionServices);
```

The supplied `BattleStatusEncounterLifecyclePort` creates one monotonic
stat-modifier sequence per authored lifecycle event ID for the canonical
encounter runner and implements
`IBattleEncounterStatModifierBoundarySource`. Before each command, the runner
snapshots that source into
`BattleEncounterTurnRequest.ActiveStatModifierBoundaries`. A turn handler must
pass those values into the action's `EffectExecutionEnvironment`; the supplied
automated runner and DemoHost do this already. Direct action-end and phase-end
lifecycle APIs accept their own boundaries. If a custom scheduler uses another
clock, it must generate one monotonic sequence per event ID across every actor,
team, phase, and round occurrence that uses that ID. Per-actor or per-team
counters are valid only when those scopes use distinct event IDs. The scheduler
must expose the pending value to action execution, deliver that same value when
the application boundary closes, and commit the value only after successful
lifecycle processing. Cancelled or rejected processing retains the prior
committed value. Sequences must not be derived from frames, animations, or
button presses.

A boundary used during application should also be present in
`EffectExecutionEnvironment.ActiveStatModifierBoundaries`. The policy stamps
new state with that boundary, preventing same-boundary decrement. A later
boundary decrements once even if its numeric sequence is more than one greater;
the number is occurrence identity, not elapsed-time arithmetic.

For cross-target effects, stamp every target with the command's pending event
boundary. Turn-end processing still selects only the actor whose turn ended.
The next matching turn for another target therefore receives a later sequence
and decrements exactly once instead of comparing unrelated actor-local clocks.

## Present Results In Godot

Godot owns labels, icons, animation, and input. It should present typed
`StatModifierEvent` and `StatModifierDiagnostic` values:

- `AggregateStageChanged` updates a stage or signal display;
- `ContributionAdded`, `ContributionUpdated`, and `ContributionExpired` update
  duration indicators;
- `TrackRemoved` clears the indicator;
- `AlreadyInEffect` returns the player to command selection without charging a
  cost or consuming a turn.

Do not inspect display names to decide whether an effect is a buff, debuff,
reset, or timed effect. Use the transition code, event kind, track ID, and
diagnostic code.

## Removal And Cleanup

Use `StatModifierRemovalRequest` with one of these modes:

- `Positive` or `Negative`;
- `SelectedTracks` with track IDs;
- `SelectedContributions` with contribution sequences;
- `All`.

Typed `RemoveStatusEffectDefinition` routes positive and negative status
removal through this same authority. `BattleStatusLifecycleService.Cleanup`
maps swap, battle end, and field transition scopes to modifier cleanup. The
supplied policies preserve swap state and clear on the other terminal scopes.

## Persistence And Restore

Save contract v13 stores canonical modifier state in
`RuntimeBattleStatusSnapshot.StatModifiers`. Host JSON DTOs must preserve:

- the qualified policy ID;
- ordered tracks and resolved stages;
- contribution sequence and signed magnitude;
- counted duration and reserve-suspension flag;
- last lifecycle event ID and sequence.

Create both `RuntimeSaveValidator` and `RuntimeSessionRestoreService` with the
same `IRuntimeRulesetBindingResolver`. Restore first locates the authored
ruleset by saved policy ID, binds it, validates the retained state, and only
then creates live actors. A host must not reconstruct aggregate stages itself.

## Custom Policies

Custom behavior implements `IStatModifierPolicy`; content binding implements
`IRuntimeStatModifierRulesetPolicyFactory`. Keep these constraints:

- evaluate immutable requests and return immutable snapshots;
- never mutate `RuntimeActorState` inside a policy;
- return rejection rather than partial state after invalid input or faults;
- validate every retained state shape the policy can produce;
- use typed lifecycle boundaries for counted duration;
- preserve deterministic contribution and event ordering.

`RuntimeRulesetPolicyFactoryRegistry` accepts host-supplied factories by
category. The current `CreateStandard()` helper creates the supplied registry
as one immutable set; it does not mutate after construction.

## Host And Framework Ownership

| Framework owns | Host owns |
|---|---|
| authored policy binding | choosing the authored ruleset ID |
| canonical modifier state | icons, text, colors, and animation |
| assess/apply/tick/remove/cleanup | command and target presentation |
| costs and item transaction ordering | host save-file encoding |
| typed events and diagnostics | scene-node mapping by runtime ID |
| save validation and actor restoration | custom scheduler boundary creation |

## Evidence

- `tests/Convergence.Framework.Tests/SkillSystem/StatModifierExecutionIntegrationTests.cs`
- `tests/Convergence.Framework.Tests/Runtime/RuntimeRulesetBindingTests.cs`
- `tests/Convergence.Framework.Tests/Runtime/RuntimePersistenceSnapshotTests.cs`
- `tests/Convergence.Framework.Tests/Architecture/GodotReferenceConsumerBoundaryTests.cs`
- `samples/Convergence.GodotHost/Scripts/ConvergenceSmokeRoot.cs`

## Related Documentation

- [Player And Designer Rules](../mechanics/stat-modifier-policies.md)
- [Runtime Authority](../technical/stat-modifier-policy-runtime.md)
- [Ruleset Policy Contracts](../ruleset-policy-contracts.md)
- [Policy Family Design Pattern](../policy-family-design-pattern.md)
