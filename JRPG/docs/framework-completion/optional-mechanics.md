# Problem: Optional Mechanics

## Current State

The framework is meant to be modular, but some mechanics still look more mandatory than they should.

Moon Phase is the clearest example:

- legacy console gameplay has a static `MoonPhaseSystem`;
- legacy negotiation can be blocked at Full Moon;
- legacy fusion accident and sacrificial-fusion gates use moon phase;
- clean sample/demo paths still register and pass `new_moon` in places that do not truly need a moon system;
- the ruleset surface includes `standard_moon_phase`, which makes Moon Phase look like part of the baseline framework.

Some clean APIs already point in the right direction. `EffectExecutionEnvironment` treats moon phase as optional metadata, and conditions can evaluate false when metadata is absent.

## Problem

A reusable framework should not force every game to have the same world-cycle mechanics.

A developer should be able to build a game with:

- no moon phase system;
- a moon phase system;
- a calendar system;
- story-gated mechanics;
- dungeon-state-gated mechanics;
- item-unlocked mechanics;
- difficulty-gated mechanics;
- or any other host-owned progression gate.

The framework should provide extension points and policy contracts, not make one inspiration-driven mechanic feel required.

## Framework-First Principle

Future work should optimize for the reusable framework first.

Legacy compatibility work is still useful when it protects existing behavior, but it should not become the main design driver. If a legacy mechanic is likely to become redundant, do not deepen framework dependency on it merely to serve the old console path.

Compatibility adapters should be treated as temporary bridges, not as the architecture's center of gravity.

## Moon Phase Direction

Moon Phase should become an optional host capability.

The framework should eventually treat it as:

- optional environment/session metadata;
- optional validation registration when content uses moon-phase conditions;
- optional host state in save snapshots;
- optional content/ruleset concept only when a game chooses to author it.

The framework should not require a host to invent `new_moon` just to run battle, field, fusion, or save flows that do not care about moon phase.

## Sacrificial Fusion Direction

Sacrificial fusion should not be inherently tied to Full Moon.

The better framework concept is:

```text
sacrificial fusion availability = host/content policy
```

That policy could depend on:

- story progress;
- tutorial completion;
- dungeon floor;
- possession of a key item;
- difficulty mode;
- current calendar state;
- moon phase;
- or no gate at all.

Moon Phase can be one possible implementation of a gate, not the gate.

## Do Not Remove Yet

Do not remove or archive `MoonPhaseSystem` immediately.

Reasons:

- legacy console behavior still uses it;
- characterization tests still protect Full Moon negotiation and fusion behavior;
- removing it now would mix modular framework cleanup with legacy behavior deletion;
- the archive gate still requires clean parity and explicit removal authorization.

The next useful change is decoupling, not deletion.

## Recommended Next Step

When this problem is implemented, keep it narrow:

1. make clean battle/demo/runtime requests accept missing moon phase wherever the mechanic is not used;
2. remove `standard_moon_phase` from neutral sample content that does not demonstrate a moon mechanic;
3. keep `MoonPhaseCondition` available for games that opt in;
4. make fusion availability use a named policy/gate instead of hardcoding Full Moon in framework-facing services;
5. leave legacy console moon behavior intact until its owning consumer is replaced.

No broad modular feature-toggle system is required yet. The immediate goal is to stop optional mechanics from looking mandatory.
