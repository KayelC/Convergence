# Ruleset Policy Contracts

## Composition Boundary

Ruleset records select a typed runtime policy by `category` and `policyId`.
The application host supplies a `RuntimeRulesetPolicyFactoryRegistry` to
`RuntimeRulesetBindingResolver`; the resolver never constructs an unregistered
policy or changes categories to make a lookup succeed.

A custom policy ID has two explicit registrations:

1. register the ID with `SkillSystemRegistrationBuilder.RegisterPolicy(...)`
   so content validation accepts it;
2. register a factory in the matching typed registry category so runtime
   binding can construct the service.

The eight factory interfaces keep service types separate:

| Ruleset category | Factory interface | Result |
|---|---|---|
| `damage` | `IRuntimeDamageRulesetPolicyFactory` | `ProductionCombatRuleset` |
| `reward` | `IRuntimeRewardRulesetPolicyFactory` | `IBattleRewardService` |
| `stat` | `IRuntimeStatRulesetPolicyFactory` | `StatRulesetServices` |
| `stat_modifier` | `IRuntimeStatModifierRulesetPolicyFactory` | `IStatModifierPolicyService` |
| `growth` | `IRuntimeGrowthRulesetPolicyFactory` | `GrowthRulesetServices` |
| `roster_capacity` | `IRuntimeRosterCapacityRulesetPolicyFactory` | `IRosterCapacityPolicy` |
| `economy` | `IRuntimeEconomyRulesetPolicyFactory` | `ResourceManagementRulesetServices` |
| `turn_economy` | `IRuntimeTurnEconomyRulesetPolicyFactory` | `BattleTurnEconomyRuleset` |

`StatRulesetServices` contains both the `IStatResolutionPolicy` used to resolve
raw actor stats and the `IStatStageScalingPolicy` used to interpret battle
stages. A custom stat factory must return the pair so neither responsibility is
silently inherited from the standard policy.

Stat-modifier accumulation and duration are independently selected from stat
resolution and numeric stage scaling. The selected modifier service owns
application, ticking, removal, cleanup, and retained-state compatibility;
`IStatStageScalingPolicy` owns only the multiplier assigned to the resolved
stage.

`RuntimeRulesetPolicyFactoryRegistry.CreateStandard()` is an explicit
convenience composition. A host may instead construct a registry from its own
factories. Duplicate or qualified factory policy IDs are rejected. Moon phase
has no standard runtime factory; a game that wants such a module owns its
policy and composition outside this supplied registry.

## Standard Damage

`standard_damage` accepts the following optional parameters. Omitted values use
the documented defaults of `ProductionCombatRulesetConfig`. Numeric values are
validated together, so an invalid or reversed range rejects the complete
binding rather than producing a partial service.

| Parameter | Type | Default | Constraint |
|---|---:|---:|---|
| `damageFormulaScalar` | decimal | `5.0` | positive |
| `damageVarianceMinimum` | decimal | `0.95` | nonnegative; no greater than maximum |
| `damageVarianceMaximum` | decimal | `1.05` | nonnegative; no less than minimum |
| `criticalDamageMultiplier` | decimal | `1.5` | nonnegative |
| `weakDamageMultiplier` | decimal | `1.5` | nonnegative |
| `resistDamageMultiplier` | decimal | `0.5` | nonnegative |
| `guardDamageMultiplier` | decimal | `0.5` | nonnegative |
| `defaultHitAccuracy` | integer | `95` | `0..100` |
| `hitChanceMinimum` | integer | `5` | `0..100`; no greater than maximum |
| `hitChanceMaximum` | integer | `99` | `0..100`; no less than minimum |
| `criticalChanceMinimum` | integer | `2` | `0..100`; no greater than maximum |
| `criticalChanceMaximum` | integer | `40` | `0..100`; no less than minimum |
| `criticalChanceBase` | integer | `5` | `0..100` |
| `instantDeathChanceMinimum` | integer | `5` | `0..100`; no greater than maximum |
| `instantDeathChanceMaximum` | integer | `95` | `0..100`; no less than minimum |
| `defaultInstantDeathChance` | integer | `40` | `0..100` |
| `enemiesPerLevelForExperience` | decimal | `50` | positive |
| `expectedStatLevelMultiplier` | decimal | `3` | nonnegative |
| `expectedStatBase` | decimal | `15` | nonnegative |
| `statDensityDivisor` | decimal | `100` | positive |
| `maximumStatDensityMultiplier` | decimal | `2` | positive |
| `currencyBaseMultiplier` | decimal | `0.25` | nonnegative |
| `currencyLuckMultiplier` | decimal | `5` | nonnegative |
| `currencyVarianceMinimum` | decimal | `0.9` | nonnegative; no greater than maximum |
| `currencyVarianceMaximum` | decimal | `1.1` | nonnegative; no less than minimum |
| `initiativeVarianceMinimum` | decimal | `0.9` | nonnegative; no greater than maximum |
| `initiativeVarianceMaximum` | decimal | `1.1` | nonnegative; no less than minimum |

Unknown parameters, nonnumeric values, and invalid combined configuration
produce typed `RulesetBindingDiagnostic` values. The standard factory does not
silently ignore them.

Charge is not a hidden parameter of `standard_damage`. A host supplies an
`IChargePolicyService` to `BattleExecutionServices`; the standard choices are
`SplitChargePolicy` and `UnifiedChargePolicy`. The authored `grant_charge`
effect supplies the multiplier, while the selected policy owns slot
compatibility and consumption. The later neutral combat-policy aggregate will
make that selection available through authored ruleset composition without
putting a global multiplier back into damage configuration.

## Standard Stats And Stage Tables

`standard_stat` uses `StandardStatResolutionPolicy` and
`StandardStatStageScalingPolicy`. With no parameters it uses the confirmed
`-4..+4` supplied tables documented in
[Actors, Stats, Resources, And Progression](mechanics/actors-progression-and-resources.md).

Its only optional parameter is `stageTables`. Each row must define:

- `trackId`: one supported unqualified modifier-track ID;
- `channel`: `physical_damage_dealt`, `magical_damage_dealt`,
  `damage_taken`, `hit_chance`, or `evasion`;
- `multipliers`: exactly one positive decimal multiplier for every integer
  stage from `-4` through `+4`.

Example ruleset fragment:

```json
{
  "id": "fast_physical_stats",
  "displayName": "Fast Physical Stages",
  "description": "Overrides physical attack stages only.",
  "category": "stat",
  "policyId": "standard_stat",
  "parameters": {
    "stageTables": [
      {
        "trackId": "physical_attack",
        "channel": "physical_damage_dealt",
        "multipliers": [
          { "stage": -4, "multiplier": 0.4 },
          { "stage": -3, "multiplier": 0.55 },
          { "stage": -2, "multiplier": 0.7 },
          { "stage": -1, "multiplier": 0.85 },
          { "stage": 0, "multiplier": 1.0 },
          { "stage": 1, "multiplier": 1.3 },
          { "stage": 2, "multiplier": 1.6 },
          { "stage": 3, "multiplier": 1.9 },
          { "stage": 4, "multiplier": 2.2 }
        ]
      }
    ]
  }
}
```

Unspecified supported tables retain their supplied defaults. Duplicate
track/channel overrides, incomplete stage ranges, unsupported mappings,
nonpositive multipliers, unknown parameters, or malformed values reject the
complete binding.

For direct code composition, a host may construct:

```csharp
var customTable = new StatStageScalingTable(
    ContentId.Parse("physical_attack"),
    StatStageScalingChannel.PhysicalDamageDealt,
    authoredMultipliers);

IStatStageScalingPolicy stages =
    new StandardStatStageScalingPolicy([customTable]);
```

A game that needs another stage range or a formula rather than tables should
implement `IStatStageScalingPolicy`. A completely different stat service can be
selected through a custom `IRuntimeStatRulesetPolicyFactory`:

```csharp
public sealed class MyStatFactory : IRuntimeStatRulesetPolicyFactory
{
    public ContentId PolicyId => ContentId.Parse("my_stat_policy");

    public RulesetBindingResult<StatRulesetServices> Create(
        RulesetDefinition definition)
    {
        // Validate every authored parameter before returning services.
        return new RulesetBindingResult<StatRulesetServices>(
            new StatRulesetServices(
                new MyStatResolutionPolicy(),
                new MyStageScalingPolicy()));
    }
}
```

Register `my_stat_policy` in both content registration and the `stat` factory
collection of `RuntimeRulesetPolicyFactoryRegistry`. The resolver never falls
back to `standard_stat` when a custom policy is missing or rejected.

## Supplied Stat Modifier Policies

Rulesets in the `stat_modifier` category select one lifecycle policy. The
runtime policy identity is the qualified ruleset-record ID, so two packs using
the same supplied factory with different configuration cannot restore each
other's state accidentally.

| `policyId` | Parameters | Meaning |
|---|---|---|
| `persistent_staged` | required integer `minimumStage` and `maximumStage` | Signed stages accumulate to authored bounds and do not expire naturally. |
| `timed_exclusive` | none | One timed weak or strong signal occupies each track. |
| `timed_contribution` | required integer `minimumStage` and `maximumStage` | Independently timed signed contributions resolve to an authored bounded aggregate. |

For bounded policies, `minimumStage` must be negative and `maximumStage` must
be positive. Missing bounds, unknown parameters, unsupported policy IDs, and
wrong-category records reject the complete binding with typed diagnostics.
Durations remain authored on effects; a factory never infers policy choice or
duration from an effect shape, display name, or description.

Training Annex explicitly binds `standard_stat_modifiers`, whose policy is
`persistent_staged` with bounds `-4..+4`. Hosts that want timed signals or
timed contributions select another authored ruleset record before creating
battle execution services.

## Standard Roster Capacity

`standard_roster_capacity` requires a nonempty `tiers` list. Every tier has:

- `rosterKind`: `hosted_entity` or `companion`;
- `minimumLevel`: a positive integer;
- `capacity`: a nonnegative integer.

Each represented roster kind must begin at level `1`, and minimum levels must
be unique within that kind. The framework supplies no hidden capacity curve.

## Standard Action Token

`standard_action_token` requires both liveness parameters:

| Parameter | Type | Constraint |
|---|---:|---|
| `maximumCommands` | integer | positive |
| `maximumConsecutiveFreeActions` | integer | nonnegative and lower than `maximumCommands` |

These are safety limits for one encounter phase. They are authored because a
host's command model determines reasonable bounds. The supplied examples use
`256` and `32`; those values are not resolver fallbacks.

## Fixed Supplied Policies

`standard_reward`, `standard_growth`, and `standard_economy` currently accept
no parameters and reject unknown ones.
Their formulas remain the supplied fixed implementations for `0.1.0`.
They are nevertheless replaceable: author another registered `policyId` and
provide the matching typed factory. This keeps replacement explicit without
pretending that unsupported tuning values are implemented.

## Related Guidance

- [Actors And Runtime State](developer-guide/actors-and-runtime-state.md)
- [Runtime Actor State And Restoration](technical/runtime-actor-state-and-restoration.md)
- [Confirmed Actor Decision](decisions/actor-composition-progression-and-rosters.md)
