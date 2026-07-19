# Combat, Defenses, And Turn Economy

## Combat Rule Ownership

**Configured rule:** combat arithmetic is supplied through policies. The included `ProductionCombatRuleset` handles damage, accuracy, criticals, chance rolls, ailment application, instant death, initiative support, and reward calculations. A game may bind authored ruleset records or inject another implementation.

The exact formula and multipliers are therefore not universal Convergence rules. They are part of the selected ruleset. Training Annex binds the supplied standard ruleset for repeatable examples. A host explicitly supplies the factory registry used to resolve authored policy IDs; no unregistered policy falls back to a built-in. The full standard parameter surface is documented in [Ruleset Policy Contracts](../ruleset-policy-contracts.md).

## Damage Flow

Typed damage identifies power, accuracy, damage element, critical mode, and hit count. Physical damage uses the physical offense path; magical elements use the magical offense path under the supplied policy.

A damage attempt resolves in this order at a high level:

1. Resolve legal targets and effect conditions.
2. Resolve shields, Break, passive overrides, and the target's starting
   elemental affinity.
3. Ask the active damage policy to resolve hit count, accuracy, critical state,
   arithmetic, guard, and the effective affinity in one operation.
4. Apply typed outgoing and incoming effect modifiers once.
5. Apply damage, reflection, absorption, or nullification to runtime resources.
6. Return typed effect and turn-economy outcomes from the policy's effective
   affinity and hit results.

`IDamageExecutionPolicy.Resolve` returns an immutable
`DamagePolicyResolution`. Its `Hits` and `ResolvedAffinity` form one
authoritative result; hosts should not split damage into separate raw and
application passes.

Arithmetic is checked or saturating at public boundaries so extreme authored/runtime values do not wrap into negative damage or rewards.

## Accuracy And Evasion

`IHitResolutionPolicy` is the hit/evasion extension boundary. The supplied
`StandardHitResolutionPolicy` starts from the action's authored accuracy, adds
an attacker Agility contribution, subtracts a target Agility contribution, and
applies explicit passive and stage-based Accuracy/Evasion modifiers using
add-then-multiply stacking. Its coefficients and probability bounds are
ruleset parameters.

The result exposes authored accuracy, both Agility contributions, scores before
and after modifiers, raw and final chance, the random roll when one was needed,
and the rigid-state guarantee flag. Skills use their own authored accuracy;
basic attacks use the equipped or supplied basic-attack profile. Names and
descriptions never choose the source.

The supplied policy does not read Luck. Its standard range is `0..100`: zero
cannot hit and one hundred cannot miss. A game that wants Luck or another
formula supplies another hit policy rather than relying on a hidden modifier.

## Elemental Defense

The elemental outcomes are Weak, Normal, Resist, Null, Repel, and Absorb. Missing entries resolve to Normal. Almighty always resolves normally and does not consult authored affinity maps.

Shield precedence comes before affinity. If no matching shield applies, Break may normalize the affinity. Passive affinity replacements use strongest-response precedence:

`Absorb > Repel > Null > Resist > Normal > Weak`

The Framework returns the resolved outcome. A host decides how to animate weakness, reflection, absorption, or immunity.

## Ailment And Instant-Death Defense

Ailment resistance is keyed by ailment `ContentId`. Instant-death resistance uses separate Light and Dark channels. Neither shares the elemental map.

Missing ailment or instant-death entries resolve to Normal. An explicit instant-death bypass mode ignores channel resistance but still delegates success probability to the active policy.

## Guard, Charge, Shields, Overrides, And Break

Guard is executable runtime state and may reduce damage or normalize weakness according to the selected combat policy. Shields, affinity overrides, and Break have typed duration state. Duration ticking and cleanup are handled by the lifecycle service rather than display code.

Charge behavior is selected explicitly. `SplitChargePolicy` provides separate
Physical and Magical slots; `UnifiedChargePolicy` provides one General slot.
Applying an occupied slot is rejected as already in effect. A matching charge
uses its authored multiplier on final damage and is consumed once after every
matching hit and target in the committed action has resolved. Miss, Null,
Repel, and Absorb consume it; an unexecuted or rejected action does not. Retained
charge state includes its policy ID so a host cannot restore it under different
semantics by accident.

## Turn Economy

`IBattleTurnEconomy` is the reusable turn interface. Action Token is one optional implementation, not a mandatory battle model.

In ruleset content, `turn_economy` is the generic category and `standard_action_token` is the supplied policy ID. The category is intentionally not named `action_token`, because another host may bind a different implementation of the same turn-economy contract.

The standard Action Token ruleset requires authored phase-liveness limits for
maximum commands and consecutive free actions. They prevent a malformed or
hostile command source from keeping one phase alive forever; the resolver does
not hide default limits.

The supplied Action Token behavior is:

- A phase starts with one full token per active living actor.
- A normal action consumes one partial token first, otherwise one full token.
- Passing consumes an existing partial token before touching any full token. Only when no partial token exists does passing convert one full token to partial.
- Weakness or Critical converts a full token to a partial token; if only partial tokens remain, it consumes one.
- Miss or Null consumes up to two tokens.
- Repel or Absorb ends the phase.
- A free action consumes no token.
- A terminate-phase result clears all remaining tokens.

**Host responsibility:** presentation may use icons, pips, text, or no visible turn meter. The state change comes from Framework snapshots and events.

## Encounter Loop

The encounter runner owns initiative, battle start, team phases, actor turns, lifecycle calls, command execution, turn-economy application, participant refresh, defeat checks, cancellation, round limits, faults, and ordered events.

Each ordered event has a payload matched to its event kind. For example, initiative contains the ordered team IDs, an effect contains the immutable effect result, an Action Token change contains before/after snapshots plus the applied consumption, and battle end contains the outcome, winning team, completed rounds, and optional fault code. `DebugText` may aid logs but is optional and must not be parsed as a gameplay contract.

The runner ends with victory, defeat, escape, draw/round limit, host cancellation, or fault. Rewards and recruitment are separate services; an encounter does not silently grant either.
