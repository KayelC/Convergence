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
- calculated hit damage; and
- the actual actor/resource delta applied to staged state.

Use these records to drive target cues, combat logs, hit animation, affinity
feedback, and Action Token presentation. Do not recalculate damage or parse a
debug message.

## Random Source Contract

Every `IRandomSource` implementation must return:

- `NextUnitDecimal()` in the half-open range `[0, 1)`; and
- `NextInt32(minimumInclusive, maximumExclusive)` in the requested half-open
  integer range.

The supplied combat, reward, initiative, and hit-count policies reject values
outside those ranges. A Godot adapter should normalize engine random output
before returning it rather than returning a percentage such as `50` for
fifty percent.

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
`UnifiedChargePolicy` stores one General slot. Applying an occupied slot is a
typed rejection rather than a refresh. A resolved matching damage action
consumes the slot once after all targets and hits have used it.

Retained charge state includes its policy ID. Supply the same policies to
`ChargePolicyRegistry` during save validation and session restoration:

```csharp
ChargePolicyRegistry charges = ChargePolicyRegistry.CreateStandard();
```

If a game changes charge semantics, it must either reject the old state or
provide an explicit save migration. It must not relabel split state as unified
state.

## Configuration Ownership

The standard damage ruleset exposes damage, variance, affinity, guard, hit,
and instant-defeat parameters. See
[Ruleset Policy Contracts](../ruleset-policy-contracts.md) for the complete
authored parameter table. Reward and initiative have separate interfaces and
ruleset categories; changing damage configuration does not silently change
their formulas.

Convergence supplies defaults so a game can start quickly. A developer remains
free to register different factories, construct policies directly, omit
charges, choose another turn economy, or avoid combat entirely.

## Related Evidence

- [Combat, Defenses, And Turn Economy](../mechanics/combat-defenses-and-turns.md)
- [Combat Resolution Pipeline](../technical/combat-resolution-pipeline.md)
- [`RuntimeRulesetBindingTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/RuntimeRulesetBindingTests.cs)
- [`ProductionCombatRulesetTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/ProductionCombatRulesetTests.cs)
- [`ChargePolicyTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/ChargePolicyTests.cs)
