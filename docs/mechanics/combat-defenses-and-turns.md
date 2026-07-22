# Combat, Defenses, And Turn Economy

## Combat Rule Ownership

**Configured rule:** combat arithmetic is supplied through policies. Authored
damage rulesets bind to a neutral `CombatExecutionPolicySet`; the included
standard composition supplies damage, accuracy, critical, charge, chance,
ailment, instant-defeat, amount, and outcome policies. Reward yield and
initiative use separate interfaces. A game may replace one policy, register a
different authored composition, or inject all policies directly.

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

### Supplied standard damage formula

The supplied `ProductionCombatRuleset` calculates each landed hit as follows.
This is Convergence's ready-to-use default, not a requirement for custom combat
policies.

```text
attack stat = Strength for Physical; Magic for every other damage element

effective attack =
    attack stat
    * general outgoing-damage multiplier
    * Physical-or-magical outgoing stage multiplier

effective defense = max(1, target Vitality + target Defense)

base damage = damage formula scalar * sqrt(power * effective attack / effective defense)

resolved damage = floor(
    base damage
    * target incoming-damage multiplier
    * critical multiplier, when critical
    * guard multiplier, when guarding
    * Weak-or-Resist multiplier, when applicable
    * charge multiplier, when charged
    * random variance)
```

The standard scalar is `5`. Its default variance is `0.95..1.05`; Critical and
Weak are `1.5`; Resist and Guard are `0.5`. Null causes no damage, Repel applies
each landed hit to the attacker, and Absorb restores the target. Rule modifiers
from typed passives are applied to each hit at the execution boundary after the
damage policy returns and before the resource mutation is committed.

Damage never reads Luck. Equipment contributes to this formula only through
the runtime fields currently composed by the actor/equipment modules. Weapon
basic attacks may now compose ordered typed secondary effects. Full armor
defense/evasion, granted skills, and other equipment behavior remain separate
work.

One authored damage effect may request between `1` and `1024` hits. The supplied
standard policy applies a second, game-selected ceiling before hit-count
randomness, allocation, or actor mutation; that ceiling defaults to `64` and
may be authored lower or higher within the public `1..1024` range. This is a
safety boundary, not a claim that ordinary attacks should contain dozens of
hits. A rejected range does not partially execute.

## Accuracy And Evasion

`IHitResolutionPolicy` is the hit/evasion extension boundary. The supplied
`StandardHitResolutionPolicy` starts from the action's authored accuracy, adds
an attacker Agility contribution, subtracts a target Agility contribution, and
applies explicit passive and stage-based Accuracy/Evasion modifiers using
add-then-multiply stacking. Its coefficients and probability bounds are
ruleset parameters.

For the supplied policy, before explicit modifiers:

```text
accuracy score = authored accuracy + attacker Agility * 2
evasion score  = target Agility * 2
raw chance     = resolved accuracy score - resolved evasion score
final chance   = floor(clamp(raw chance, configured minimum, configured maximum))
```

The coefficient `2` on each side is the supplied default and is configurable.
Typed additive modifiers are combined before typed multiplicative modifiers.
The policy rolls once per attempted hit only when the final chance is between
zero and one hundred.

The result exposes authored accuracy, both Agility contributions, scores before
and after modifiers, raw and final chance, the random roll when one was needed,
and the rigid-state guarantee flag. Skills use their own authored accuracy;
basic attacks use the equipped or supplied basic-attack profile. Names and
descriptions never choose the source.

The supplied policy does not read Luck. Its standard range is `0..100`: zero
cannot hit and one hundred cannot miss. A game that wants Luck or another
formula supplies another hit policy rather than relying on a hidden modifier.

Authored accuracy and probability inputs are always inclusive `0..100` values.
This includes critical, instant-defeat, ailment, escape, chance-condition, and
resource-percentage-condition values. Invalid authored input is rejected before
target selection, costs, inventory reservation, randomness, mutation, or turn
use. A valid base chance may subsequently be multiplied by resistance or other
selected policy rules and clamped back into `0..100`; that derived clamp does
not make an invalid authored value valid.

## Critical Hits

Critical eligibility is separate from critical chance. The standard
`PhysicalOnlyCriticalEligibilityPolicy` allows only Physical damage whose
effect explicitly authors `chance`; the optional
`AllDamageCriticalEligibilityPolicy` permits any damage element with that same
explicit declaration. Guard blocks criticals. Rigid state guarantees one only
when the selected eligibility policy accepts the attack.

The standard `AuthoredCriticalChancePolicy` uses the exact authored chance,
then applies explicit target vulnerability, actor, and passive Critical Chance
modifiers. The optional `AccuracyScaledCriticalChancePolicy` scales the
authored chance by `final hit chance / authored accuracy` first. Luck is absent
from both supplied policies, there is no hidden minimum, and critical chance is
rolled only after the corresponding hit lands. Schema-v6 weapon profiles must
author `never` or `chance`; basic attacks do not receive a runtime default.

## Elemental Defense

The elemental outcomes are Weak, Normal, Resist, Null, Repel, and Absorb. Missing entries resolve to Normal. Almighty always resolves normally and does not consult authored affinity maps.

Shield precedence comes before affinity. If no matching shield applies, Break may normalize the affinity. Passive affinity replacements use strongest-response precedence:

`Absorb > Repel > Null > Resist > Normal > Weak`

The Framework returns the resolved outcome. A host decides how to animate weakness, reflection, absorption, or immunity.

## Ailment And Instant-Death Defense

Ailment resistance is keyed by ailment `ContentId`. Instant-death resistance uses separate Light and Dark channels. Neither shares the elemental map.

Missing ailment or instant-death entries resolve to Normal. An explicit instant-death bypass mode ignores channel resistance but still delegates success probability to the active policy.

The supplied instant-defeat policy starts from the effect's authored chance and
applies configurable resistance multipliers. Its defaults are `1.5` for
Vulnerable, `1` for Normal, `0.5` for Resistant, and `0` for Immune. It then
performs at most one probability roll. Bypass uses multiplier `1` regardless of
the target's resistance, but it does not guarantee success. Luck is not part of
this supplied policy. A blocked or unsuccessful attempt is a normal-cost typed
no-effect result, not an accuracy miss.

## Guard, Charge, Shields, Overrides, And Break

Guard is executable runtime state and may reduce damage or normalize weakness according to the selected combat policy. Shields, affinity overrides, and Break have typed duration state. Duration ticking and cleanup are handled by the lifecycle service rather than display code.

Charge behavior is selected explicitly. `SplitChargePolicy` provides separate
Physical and Magical slots; `UnifiedChargePolicy` provides one General slot;
`DisabledChargePolicy` supplies no slots, rejects charge grants, and leaves
damage unmodified. The standard authored combat composition selects these with
`chargePolicy: "split"`, `"unified"`, or `"disabled"`. Omission preserves the
supplied `split` default.

Applying an occupied slot is rejected as already in effect. When a retained
charge supplies a damage modifier, that exact runtime charge becomes a
participant in the complete action. Its authored multiplier affects the damage
attempt, and the participating charge is consumed once after every matching hit
and target in the committed action has resolved. Miss, Null, Repel, and Absorb
consume it because the charge participated before those outcomes were known;
an unexecuted, skipped, cancelled, or rejected action does not.

A later grant is not retroactively part of an earlier uncharged damage attempt.
Likewise, clearing a participating slot and granting a same-kind replacement
creates a different runtime charge. The replacement remains unless a later
damage component actually uses it. Retained charge state includes its policy ID
so a host cannot restore it under different semantics by accident.

A split charge affects only its matching damage category. A mixed Physical and
magical action may therefore consume both split slots when it actually resolves
both categories. A unified charge affects every damage category in that one
action and is consumed once. Charge duration is a fallback expiry boundary:
turn, phase, battle, permanent, and immediate action-end shapes are supported.
The matching attack still consumes the charge before a later duration boundary
would expire it.

## Multi-Hit And Multi-Target Outcomes

Every attempted hit receives its own immutable evidence. Landed hits are
applied in authored order to staged actor state, so drain, reflection,
absorption, and defeat prevention occur at the hit that caused them. The host
may animate those facts one by one without recalculating combat.

Turn cost is decided once for the complete action, not once per hit. The
supplied aggregation policy treats a target as having evaded only when all
damage hits across every damage effect aimed at that target miss. A miss from
one component is not an evasion when another component lands on that same
target. Repeated misses against one target do not stack extra penalties. Repel
or Absorb terminates the phase; Null has the miss-style penalty; Weak or
Critical grants the configured benefit when no conflicting target evasion
exists; mixed Critical and evasion normalizes to normal cost. Critical remains
presentation evidence after that normalization, but it does not secretly
override the final action outcome. Another game may replace this aggregation
policy.

### Ordered secondary effects

A later effect may explicitly depend on an earlier local effect ID. The
standard on-hit rider requires positive committed damage to the same target.
Miss, Null, Repel, Absorb, or zero damage therefore cannot apply it. The rider
is attempted once per qualifying target, not once per landed source hit, and
its own chance is rolled only after the dependency succeeds.

Shared-contact secondary damage avoids a second accuracy roll after that gate.
It is not a hidden copy of the primary hit: its element, affinity, power,
charge, and Critical definition remain independent. Ordinary independent
secondary damage performs a separate hit check. Current target life state is
rechecked between components, so later hostile or restorative effects cannot
silently revive a target defeated by the primary component.

### Skills, basic attacks, and offensive items

The action-outcome policy receives both the action source and its ordered typed
effect results. Under the supplied standard policy, skills and basic attacks
use the effect-driven mapping above. Non-escape items instead spend one normal
turn by default, even when an item effect reports Weak, Critical, Miss, Null,
Repel, or Absorb.

This normal item cost does not rewrite the effect. A reflected item still
reports Repel, an absorbed item still reports Absorb, and an interrupted effect
still stops its remaining effect sequence. The distinction controls only the
complete action's turn cost. A game may author
`itemActionOutcomeBehavior: "effect_driven"` on its standard damage ruleset or
provide another action-outcome policy. Escape items retain their explicit
no-turn escape contract.

## Turn Economy

`IBattleTurnEconomy` is the reusable opportunity-counting interface. Action
Token is one optional implementation, not a mandatory battle model. The
supplied `standard_actions` policy is the neutral alternative: one priced
action for each actor present at phase start.

In ruleset content, `turn_economy` is the generic category. The supplied policy
IDs are:

- `standard_actions`: one ordinary action per phase-start actor;
- `standard_action_token`: full and partial Action Token behavior.

Both require authored maximum-command and consecutive-free-action limits.
These finite values protect the encounter from a policy or command source that
never ends its phase. They are safety bounds, not hidden balance rules.

### Standard actions

Normal, Pass, and effect-derived consumption each spend one action. A free
action spends none, and explicit phase termination removes every remaining
action. Weakness, Critical, Miss, Null, Repel, and Absorb do not receive special
pricing under this neutral economy.

Explicit phase termination is stronger than policy-specific pricing. Every
turn economy, including a game-supplied replacement, must end with no action
opportunities when it receives that command. The encounter faults instead of
granting another command if a replacement policy claims otherwise.

### Action Token

| Result | Token change |
|---|---|
| Phase start | Gain one full token per active living actor. |
| Normal | Consume a partial token first, otherwise a full token. |
| Pass | Consume a partial token first; only when none exists, convert a full token to partial. |
| Weakness or Critical | Convert a full token to partial; if only partial remains, consume one. |
| Miss or Null | Consume up to two tokens, partial first. |
| Repel or Absorb | End the phase. |
| Free action | Do not change supplied token state. |
| Explicit termination | End the phase. |

The pass order is a strategic rule, not an implementation detail:

```text
[partial, full] --pass--> [full]
[full]          --pass--> [partial]
[partial]       --pass--> []
```

Passing cannot manufacture another partial token while one already exists.

A two-actor example is:

```text
phase start       [full, full]
strike weakness   [partial, full]
pass              [full]
normal action     []
```

### What turn economy does not decide

Turn economy changes the opportunity pool after a command. It does not choose
initiative, teams, or the next actor. The current encounter schedule uses team
phases and rotates active actors after each executed command window, including
a free command. A free command does not complete owner-turn-end lifecycle, but
it does not automatically grant the same actor another command.

An agility-ordered battle or immediate same-actor bonus system therefore needs
a future encounter-scheduling policy in addition to an economy. Swapping only
`IBattleTurnEconomy` does not claim to implement that schedule.

**Host responsibility:** presentation may use icons, pips, text, or no visible
turn meter. Godot reads typed phase and before/after transition payloads. It
must not derive rules by parsing optional debug text.

See [Turn Economy Policies](../developer-guide/turn-economy-policies.md) for
integration and [Turn Economy Runtime](../technical/turn-economy-runtime.md)
for state authority and fault containment.

## Encounter Loop

The encounter runner owns initiative, battle start, team phases, actor turns, lifecycle calls, command execution, turn-economy application, participant refresh, defeat checks, cancellation, round limits, faults, and ordered events.

Each ordered event has a payload matched to its event kind. For example, initiative contains the ordered team IDs, an effect contains the immutable effect result, an Action Token change contains before/after snapshots plus the applied consumption, and battle end contains the outcome, winning team, completed rounds, and optional fault code. `DebugText` may aid logs but is optional and must not be parsed as a gameplay contract.

Structural events come from the encounter runner only. Action and lifecycle
extensions may report command, effect, status, resource, deployment, and
host-action details, but they cannot invent a phase change, Action Token
change, defeat, fault, or battle ending. This keeps the state shown by a host
consistent with the state the runner actually accepted.

The runner ends with victory, defeat, escape, draw/round limit, host cancellation, or fault. Rewards and recruitment are separate services; an encounter does not silently grant either.
