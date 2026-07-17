# Stat Modifier Policies

## Status And Purpose

This page records the confirmed player-visible design for Convergence stat
modifiers. The persistent staged policy is implemented. The timed-exclusive and
timed-contribution policies are confirmed designs being implemented through the
[Stat Modifier Policy Roadmap](../roadmap/stat-modifier-policy-roadmap.md).

Stat modifiers are optional. A game selects one coherent policy for a runtime
scope; Convergence does not force every game to use staged buffs, timed signals,
or timed stacking.

Two separate questions must not be confused:

1. **Modifier lifecycle:** how a change is applied, combined, timed, removed,
   and saved.
2. **Stage scaling:** what a resolved change does to damage, defense, accuracy,
   evasion, or another value.

A game may keep the same lifecycle policy while replacing the multiplier table.

## Policy One: Persistent Stages

The supplied persistent policy uses signed stages. Its reference range is
`-4..+4`, although a developer may configure different signed bounds.

- Repeated positive applications move toward the positive cap.
- Repeated negative applications move toward the negative cap.
- Opposite applications move the stage toward neutral and then toward the
  opposite side.
- An application that cannot change a capped stage is unchanged.
- Stages do not expire naturally during an encounter.
- Explicit removal, actor departure, encounter end, or field transition can
  clear them. Swapping an actor does not clear them.

Example:

```text
Neutral -> apply +2 -> +2
+2      -> apply -1 -> +1
+1      -> apply -1 -> neutral
Neutral -> apply -3 -> -3
```

## Policy Two: Timed Exclusive Signals

This policy allows one signal on a stat track. It is suitable for games where a
stat is either unchanged, changed, or strongly changed rather than built from
many independent applications.

| Internal value | Suggested signal | Player-facing meaning |
|---:|:---:|---|
| `-2` | `--` | strong negative change |
| `-1` | `-` | negative change |
| `0` | `~` | no change |
| `+1` | `+` | positive change |
| `+2` | `++` | strong positive change |

A game may display icons, arrows, colors, or words. The signals above explain
the rule; they do not require a particular user interface.

### Same-Direction Application

- Applying the same signal again resets its timer.
- Applying a stronger signal replaces the weaker signal and starts the new
  full timer.
- Applying a weaker signal while a stronger one is active is rejected as
  already in effect.

```text
Current + with 1 turn left; apply +  -> + with a fresh timer
Current +; apply ++                  -> ++ with a fresh timer
Current ++; try to apply +           -> rejected, unchanged
```

The weaker rejection is discovered before commitment. It does not spend a
resource or item, mutate the actor, consume an action, or consume turn economy.
The host can explain the reason and ask for another command.

### Opposite Application

Opposite signals offset arithmetically:

```text
+  plus -  -> neutral
++ plus -  -> +
+  plus -- -> -
```

If the existing effect remains stronger, the surviving signal keeps the
existing effect's remaining time. If the incoming effect becomes stronger, the
surviving signal uses the incoming effect's fresh time. Equal strengths remove
the signal and its timer.

This prevents a weak opposing effect from accidentally refreshing the stronger
effect that it was trying to reduce.

## Policy Three: Timed Contributions

This policy combines signed stages with independent timers. Every accepted
application creates one contribution. Contributions expire separately, and the
visible stage is the bounded sum of all active positive and negative
contributions.

An authored `+2` effect creates one `+2` contribution with one timer. It does
not secretly create two `+1` records.

### Rolling Three-Turn Example

One actor applies `+1` to the same track once per turn. Each application lasts
three owner-turn completions:

| Turn | Active remaining times after due expiry and application | Stage |
|---:|---|---:|
| 1 | `[3]` | `+1` |
| 2 | `[2, 3]` | `+2` |
| 3 | `[1, 2, 3]` | `+3` |
| 4 | `[1, 2, 3]` | `+3` |

On turn 4, the oldest contribution expires and the newest starts. The policy
does not merge them into one timer.

Stage `+4` remains reachable when four contributions overlap, such as through
additional actions or a stronger authored contribution.

At the same-direction cap, another application refreshes the oldest
contribution of that sign. It does not create an unlimited invisible fifth,
sixth, or later stack.

Positive and negative contributions coexist and net together:

```text
three active +1 contributions = +3
one active -1 contribution    = -1
resolved stage                = +2
```

If the negative contribution expires first, the stage returns to `+3` while the
positive contributions remain.

## Removing And Resetting Modifiers

All policies support generic typed removal. Content may request:

- remove positive modifiers;
- remove negative modifiers;
- remove selected stat tracks;
- remove selected timed contributions;
- remove all modifiers.

Targeting gives these operations their game-specific purpose. For example, a
game may author one effect that removes negative modifiers from allies and a
different effect that removes positive modifiers from enemies. Convergence does
not infer this behavior from the skill name or description.

## Duration Clocks

"Three turns" is ambiguous unless the game identifies which clock advances.
Convergence therefore treats the clock as part of the configured duration.

| Clock | Advances when |
|---|---|
| Owner turn | the affected actor completes one turn window |
| Team phase | the affected actor's team completes a phase |
| Round | all scheduled teams or actors complete a round |
| Action | any matching committed action completes |

The supplied timed-policy default is the owner-turn clock. Phase-oriented games
may select the team-phase clock. A game may supply another explicit lifecycle
clock through the policy boundary.

### What Counts As One Owner Turn

- A committed attack, skill, item, guard, pass, forced action, or skipped action
  completes a turn window.
- Cancelling command selection before commitment does not.
- A bonus action that continues the current turn window does not advance the
  owner-turn clock a second time.
- A genuinely new scheduled turn does advance it.

The battle scheduler identifies turn-window boundaries. A console menu or Godot
animation must not guess them from how many buttons were pressed or effects were
shown.

### Application During A Boundary

A newly applied modifier does not lose duration at the end of the same clock
boundary in which it was created.

```text
Actor's turn begins
Actor applies a three-turn modifier
The same turn completes -> modifier remains at 3
The actor's next turn completes -> modifier becomes 2
```

If another actor applies the modifier before the target's next turn, the target
receives that next turn under the modifier and its duration then decreases when
that turn completes.

Runtime contributions therefore remember the clock boundary in which they were
created. This is rule state, not presentation state.

### Reserve Actors

Each counted duration explicitly states whether it suspends while its owner is
in reserve:

- suspension enabled: matching clock events do not decrease it in reserve;
- suspension disabled: matching clock events continue to decrease it.

There is no hidden global reserve assumption.

## Related Documentation

- [Stat Modifier Policy Family Decision](../decisions/stat-modifier-policy-family.md)
- [Stat Modifier Policy Roadmap](../roadmap/stat-modifier-policy-roadmap.md)
- [Using Stat Modifier Policies](../developer-guide/stat-modifier-policies.md)
- [Stat Modifier Runtime Authority](../technical/stat-modifier-policy-runtime.md)
- [Actors, Stats, Resources, And Progression](actors-progression-and-resources.md)
