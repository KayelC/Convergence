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

The seven factory interfaces keep service types separate:

| Ruleset category | Factory interface | Result |
|---|---|---|
| `damage` | `IRuntimeDamageRulesetPolicyFactory` | `ProductionCombatRuleset` |
| `reward` | `IRuntimeRewardRulesetPolicyFactory` | `IBattleRewardService` |
| `stat` | `IRuntimeStatRulesetPolicyFactory` | `IStatResolutionPolicy` |
| `growth` | `IRuntimeGrowthRulesetPolicyFactory` | `GrowthRulesetServices` |
| `roster_capacity` | `IRuntimeRosterCapacityRulesetPolicyFactory` | `IRosterCapacityPolicy` |
| `economy` | `IRuntimeEconomyRulesetPolicyFactory` | `ResourceManagementRulesetServices` |
| `turn_economy` | `IRuntimeTurnEconomyRulesetPolicyFactory` | `BattleTurnEconomyRuleset` |

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
| `chargeMultiplier` | decimal | `1.9` | nonnegative |
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

`standard_reward`, `standard_stat`, `standard_growth`, and
`standard_economy` currently accept no parameters and reject unknown ones.
Their formulas remain the supplied fixed implementations for `0.1.0`.
They are nevertheless replaceable: author another registered `policyId` and
provide the matching typed factory. This keeps replacement explicit without
pretending that unsupported tuning values are implemented.
