# Decision: Stat Modifier Policy Family

Status: confirmed
Date: 2026-07-17

## Context

Convergence currently represents each modifier track as one signed aggregate
stage and one optional duration. Reapplication replaces the track duration,
while magnitude is clamped to `-4..+4`.

That is one accidental hybrid rather than a deliberately selected mechanic. It
cannot faithfully represent all approved designs:

- persistent staged modifiers;
- one timed non-stacking modifier;
- independently timed stage contributions.

It also causes the typed-effect layer to infer meaningful success from the
authored stage delta rather than the actual policy result.

## Decision

Stat-modifier application and lifecycle become a first-class optional policy
family governed by the
[Policy Family Design Pattern](../policy-family-design-pattern.md).

Convergence will supply three neutral reference implementations:

1. **Persistent staged policy**
   - signed stages move within configured minimum and maximum bounds;
   - supplied defaults use `-4..+4`;
   - modifiers do not expire naturally during an encounter;
   - explicit removal, actor departure policy, or encounter cleanup removes
     them;
   - an application at the same-direction cap reports no state change.

2. **Timed exclusive policy**
   - one modifier contribution occupies a track;
   - the contribution has an explicit duration and tick event;
   - same-direction and opposite-direction reapplication behavior are explicit
     configuration, never an executor assumption;
   - no independent stage stack is implied.

3. **Timed contribution policy**
   - each uncapped application creates independently timed signed
     contributions;
   - each retained contribution has its own duration;
   - contributions tick and expire independently;
   - the resolved stage is derived from active contributions and configured
     bounds;
   - a one-stage, three-turn application used once per turn can remain at three
      active stages because the oldest contribution expires as the newest is
      added.

### Confirmed Rolling-Duration Example

The timed contribution policy is the approved hybrid staged-duration model. It
must reproduce this exact example for one actor taking one action per turn. Each
use applies `+1` to the same attack track and creates a separate three-turn
contribution:

| Turn | Contributions after due expiry and the new application | Resolved stage |
|---:|---|---:|
| 1 | first contribution: 3 turns remaining | `+1` |
| 2 | first: 2; second: 3 | `+2` |
| 3 | first: 1; second: 2; third: 3 | `+3` |
| 4 | first expires; second: 1; third: 2; fourth: 3 | `+3` |

The fourth application does not produce `+4` in this sequence, and it does not
refresh the older contributions into one shared three-turn timer. The oldest
contribution expires on its own schedule while the newest begins its own
schedule.

Stage `+4` remains reachable when four contributions become active before one
expires, such as through additional actions from other actors or an authored
effect that contributes more than one stage. The selected duration tick event
defines the exact lifecycle boundary; it must advance every contribution from
its own application point consistently.

One selected policy owns assessment, application, ticking, removal, cleanup,
meaningful-success reporting, retained-state compatibility, and policy events.
No effect executor or public actor mutation method may provide a parallel rule.

`IStatStageScalingPolicy` remains separate. It maps the resolved stage to
damage, defense, hit, or evasion multipliers and does not decide how the stage
was accumulated or how long it remains.

Policy extensions operate on immutable snapshots and return immutable results.
Framework-owned services validate and atomically commit accepted transitions.

## Confirmed Runtime Consequences

- The current single-stage/single-duration snapshot is not sufficient for the
  timed-contribution policy and will be replaced by policy-neutral retained
  contributions.
- Retained battle status records the policy identity required to detect an
  incompatible restore.
- The save contract must advance when the retained shape changes.
- Effect results report actual state change, including applied magnitude and
  duration/contribution changes.
- Item consumption uses that actual result; an authored nonzero delta is not
  proof of a meaningful effect.
- Turn consumption remains a separate action/turn-economy concern.
- Content and ruleset binding select the policy explicitly; missing or invalid
  selection cannot fall back silently.

## Decisions Required Before Reference Defaults Are Final

The architecture is confirmed. These policy-specific defaults remain explicit
owner checkpoints rather than inferred behavior:

- whether timed-exclusive same-direction reapplication rejects, refreshes, or
  replaces;
- how timed-exclusive opposite-direction application behaves;
- whether a capped timed contribution rejects or refreshes the oldest matching
  contribution;
- whether opposite signed timed contributions coexist and net together or
  cancel in deterministic order;
- whether reserve deployment suspends each supplied timed policy by default.

The roadmap requires these answers before their corresponding policy commit.
Custom implementations may support other coherent choices regardless of the
supplied defaults.

## Alternatives

### Keep One Aggregate Stage And One Duration

Rejected. It cannot represent independent expirations and silently forces
reapplication to rewrite one shared timer.

### Add Flags To The Existing Actor Method

Rejected. A growing set of flags would combine incompatible mechanics in one
mutation method and leave lifecycle, saves, and events without one authority.

### Put Modifier Lifecycle Inside Stage Scaling

Rejected. Accumulation/expiry and numeric interpretation are separate design
questions and should remain independently replaceable.

### Let Hosts Implement Modifier State

Rejected. That would duplicate combat rules across Godot, DemoHost, and tests,
and would remove framework-owned atomicity and restoration guarantees.

## Consequences

This is deliberate pre-release contract work affecting execution, lifecycle,
runtime snapshots, save restoration, ruleset binding, API baselines, examples,
and documentation. It does not change turn economy, ailment policy, targeting,
or stat-stage multiplier tables by itself.

The implementation is split into isolated checkpoints under the
[Stat Modifier Policy Roadmap](../roadmap/stat-modifier-policy-roadmap.md).

## Evidence

- [Stat Modifier Policy Feasibility Review](../reviews/stat-modifier-policy-feasibility-review-2026-07-17.md)
- [Typed Action And Effect Execution Independent Review](../reviews/typed-action-and-effect-execution-order-1-independent-review-2026-07-17.md)
- `src/Convergence.Framework/Execution/BattleRuntimeState.cs`
- `src/Convergence.Framework/Execution/EffectExecutors.cs`
- `src/Convergence.Framework/Execution/BattleStatusLifecycle.cs`
- `src/Convergence.Framework/Runtime/RuntimeStateSnapshots.cs`
- `src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs`
- `src/Convergence.Framework/Runtime/StatStageScaling.cs`
