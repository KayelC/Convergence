# Combat Resolution Policies

## Purpose

This guide shows how a host binds Convergence's supplied combat policy family,
passes it into typed action execution, observes its evidence, and replaces
individual rule families without coupling game code to a concrete ruleset.

The Framework owns calculation and staged runtime mutation. A Godot, console,
or test host owns content text, policy registration, random input, command
selection, visuals, sound, and save-file encoding.

## Bind The Supplied Composition

Ruleset content identifies a registered policy factory. The host chooses which
factories exist; there is no fallback to an unregistered policy ID.

```csharp
var registry = RuntimeRulesetPolicyFactoryRegistry.CreateStandard();
var resolver = new RuntimeRulesetBindingResolver(registry);

StatRulesetServices stats = resolver
    .BindStatServices(catalog, statRulesetId)
    .RequireService();

IStatModifierPolicyService modifiers = resolver
    .BindStatModifierPolicy(catalog, statModifierRulesetId)
    .RequireService();

CombatExecutionPolicySet combat = resolver
    .BindCombatPolicies(
        catalog,
        combatRulesetId,
        random,
        stats.StageScalingPolicy)
    .RequireService();
```

Inspect `RulesetId`, `PolicyId`, `AuthoredParameters`, and
`EffectiveConfiguration` when reporting startup diagnostics. A rejected binding
contains typed diagnostics and must stop composition; silently constructing a
different ruleset would make authored content misleading.

The supplied combat factory accepts an explicit charge composition:

```json
{
  "parameters": {
    "chargePolicy": "disabled"
  }
}
```

Accepted values are `split`, `unified`, and `disabled`. Omitting the parameter
selects `split`. An unknown value or non-string value rejects the ruleset
binding; it never falls back silently.

## Build Execution Services

The neutral aggregate supplies the related policy authorities as one coherent
selection:

```csharp
var execution = new BattleExecutionServices(
    catalog,
    combat.Damage,
    combat.InstantDefeat,
    combat.Ailments,
    combat.Chance,
    combat.Amounts,
    skillTargetPolicy,
    runtimeTargetPolicy,
    modifiers,
    combat.Charges,
    actionOutcomes: combat.ActionOutcomes);
```

Both target policies are mandatory. Use a deterministic ordered policy only
when ordered selection is actually the game's rule or a test requirement.
Random target policies should consume the host's `IRandomSource` explicitly.

The aggregate exposes `HitResolution`, `CriticalEligibility`,
`CriticalChance`, and `InstantDefeatResolution` for diagnostics. Those
properties come from the same composed damage and instant-defeat executors that
`BattleExecutionServices` receives; they cannot be contradictory constructor
arguments.

## Execute And Present

Use `BattleActionExecutor.Assess` before committing an action. Present its
typed diagnostics and prepared target IDs, then call `ExecuteAsync` with the
same request and assessment. Cancellation or a rejected assessment does not
spend resources, consume inventory, remove charge state, or spend a turn.

For damage, inspect each `DamageHitExecutionEvidence` in the returned effect
results. It identifies:

- source action, actor, target, effect index, and hit index;
- authored accuracy, final hit chance, and hit roll;
- critical eligibility, chance, roll, and result;
- resolved affinity;
- charge kind and multiplier;
- independent or shared-contact mode and, for shared contact, the earlier
  effect ID/index that established contact;
- calculated hit damage; and
- the actual actor/resource delta applied to staged state.

Use these records to drive target cues, combat logs, hit animation, affinity
feedback, and Action Token presentation. Do not recalculate damage or parse a
debug message.

Complete-action pricing groups `DamageHits` by target across the entire effect
sequence. Do not price each effect separately. One component miss is not a
target evasion when another damage component lands on that target, and
`TurnEconomyResolution.Outcome` is authoritative even when `AnyCritical`
retains Critical evidence for presentation.

`EffectExecutionResult.ParticipatingCharge` identifies the exact modifier
receipt used by one damage effect. The ordered executor carries those receipts
to the selected `IChargePolicyService` only at the outer action boundary. A
custom damage-effect executor that resolves a charge must publish the returned
`ChargeDamageModifier` through this property if it wants the standard
whole-action consumption behavior.

## Bound Multi-Hit Work

Schema v8 and Framework semantic validation allow `1..1024` hits for one
damage effect. The supplied `standard_damage` policy adds
`maximumHitsPerDamageEffect`, which defaults to `64` and must itself remain
within `1..1024`.

The standard policy rejects an authored hit range whose maximum exceeds the
selected policy ceiling before it asks for a random hit count, allocates hit
storage, or mutates an actor. Raising the standard ceiling is therefore an
explicit game decision; it does not bypass the absolute content ceiling. A
custom `IDamageExecutionPolicy` remains replaceable, but content loaded through
the published schema is still constrained to `1024`.

## Author Percentages, Do Not Repair Them

Damage accuracy, critical chance, instant-defeat chance, ailment chance,
escape chance, nested chance conditions, and resource-percentage conditions
must be authored within inclusive `0..100`. Skill, item, basic-attack, and
escape assessments report `AuthoredPercentageOutOfRange` before targets,
costs, reservations, random draws, mutations, or turn economy are touched.

Direct code composition receives the same domain rule. Public supplied-policy
and lifecycle requests reject malformed authored values with an argument error;
they do not clamp them. Clamping belongs only after a valid authored base has
been transformed by selected resistance or modifier policy. Zero and one
hundred remain deterministic and do not request a random draw.

## Random Source Contract

Every `IRandomSource` implementation must return:

- `NextUnitDecimal()` in the half-open range `[0, 1)`; and
- `NextInt32(minimumInclusive, maximumExclusive)` in the requested half-open
  integer range.

Every Framework-owned random consumer crosses the same validated boundary,
including combat, reward, initiative, negotiation, lifecycle, progression, and
fusion services. A Godot adapter should normalize engine random output before
returning it rather than returning a percentage such as `50` for fifty
percent. A contract violation fails with a clear `InvalidOperationException`
before an invalid value can index content or decide gameplay.

## Select Item Turn Outcomes

`IActionOutcomeAggregationPolicy` receives an immutable
`ActionOutcomeAggregationRequest`. Its `SourceKind` distinguishes Skill,
BasicAttack, Item, and other effect-backed actions without coupling the policy
to a particular turn economy.

The supplied standard policy makes non-escape items cost one normal turn. To
make offensive item outcomes follow their typed effects, author this optional
parameter on the selected `standard_damage` ruleset:

```json
{
  "itemActionOutcomeBehavior": "effect_driven"
}
```

The accepted values are `normal` and `effect_driven`; malformed or unknown
values reject ruleset binding. Direct composition can make the same choice:

```csharp
var outcomes = new StandardActionOutcomeAggregationPolicy(
    new StandardActionOutcomeAggregationPolicyConfig(
        ItemActionOutcomeBehavior.EffectDriven));
```

The policy changes only `TurnEconomyResolution`. Per-effect affinity, hit,
critical, interruption, and resource evidence remains truthful for host
presentation. Existing custom policies that implement the original
`Aggregate(effects)` method continue to work; source-aware policies may
implement the request overload.

## Replace A Policy Family

There are two integration levels.

### Direct execution composition

A host may pass any `IDamageExecutionPolicy` or
`IInstantDeathExecutionPolicy` directly to `BattleExecutionServices`. This is
appropriate for isolated tools and games that do not bind authored combat
rulesets.

### Authored ruleset composition

An authored combat factory implements `IRuntimeCombatRulesetPolicyFactory` and
returns a `CombatExecutionPolicySet`. Its damage executor implements
`ICombatDamageExecutionPolicy`, exposing the exact hit and critical policies it
uses. Its instant-defeat executor implements
`ICombatInstantDefeatExecutionPolicy`, exposing the exact resistance/chance
policy it uses.

The supplied `ProductionCombatRuleset` accepts replacement hit, critical
eligibility, critical chance, instant-defeat, and stat-stage policies through
its constructor. A custom factory can therefore reuse standard damage while
changing one explicit rule:

```csharp
var damage = new ProductionCombatRuleset(
    random,
    authoredConfig,
    stageScalingPolicy,
    hitPolicy: myHitPolicy,
    criticalEligibilityPolicy: new AllDamageCriticalEligibilityPolicy(),
    criticalChancePolicy: myCriticalPolicy,
    instantDefeatPolicy: myInstantDefeatPolicy);

return new RulesetBindingResult<CombatExecutionPolicySet>(
    new CombatExecutionPolicySet(
        definition.Id,
        definition.PolicyId,
        damage,
        myChargePolicy,
        damage,
        damage,
        damage,
        damage,
        myActionOutcomePolicy,
        definition.Parameters,
        effectiveConfiguration));
```

Register that factory in a host-created
`RuntimeRulesetPolicyFactoryRegistry`. Policy IDs are local content vocabulary;
the Framework does not infer a factory from a display name or description.

## Charge Policies And Saves

`SplitChargePolicy` stores separate Physical and Magical slots.
`UnifiedChargePolicy` stores one General slot. `DisabledChargePolicy` rejects
every charge grant and resolves neutral damage modifiers. Applying an occupied
slot under an enabled policy is a typed rejection rather than a refresh.

A resolved modifier is a participation receipt. The selected policy consumes
the exact retained charge represented by that receipt once after all targets
and hits have used it. It does not merely look up the damage element at action
end. Consequently, an uncharged damage effect followed by a grant keeps the
new charge, and a same-kind replacement is not mistaken for the earlier charge.
Custom damage executors must publish the exact modifier returned by
`ResolveDamageModifier`. The supplied policy base rejects a source-less charged
modifier before mutation; the enclosing staged action then rejects without
committing actor or target state.

Retained charge state includes its policy ID. Supply the same policies to
`ChargePolicyRegistry` during save validation and session restoration:

```csharp
ChargePolicyRegistry charges = ChargePolicyRegistry.CreateStandard();
```

The standard registry includes disabled, split, and unified policy IDs. Empty
disabled state can be validated and restored; a disabled snapshot containing a
charge slot is invalid. If a game changes charge semantics, it must either
reject the old state or provide an explicit save migration. It must not relabel
split state as unified state.

## Configuration Ownership

The standard damage ruleset exposes damage, variance, affinity, guard, hit,
instant-defeat, and item-action-outcome parameters. See
[Ruleset Policy Contracts](../ruleset-policy-contracts.md) for the complete
authored parameter table. Reward and initiative have separate interfaces and
ruleset categories; changing damage configuration does not silently change
their formulas.

Convergence supplies defaults so a game can start quickly. A developer remains
free to register different factories, construct policies directly, select the
supplied disabled charge policy, choose another turn economy, or avoid combat
entirely. Keeping an explicit policy object in a composed battle avoids nullable
rule branches while still making charge gameplay optional.

## Related Evidence

- [Combat, Defenses, And Turn Economy](../mechanics/combat-defenses-and-turns.md)
- [Combat Resolution Pipeline](../technical/combat-resolution-pipeline.md)
- [`RuntimeRulesetBindingTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/RuntimeRulesetBindingTests.cs)
- [`ProductionCombatRulesetTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/ProductionCombatRulesetTests.cs)
- [`ChargePolicyTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/ChargePolicyTests.cs)
