# Content Contract

## Ownership

JSON is a host-supplied authoring and import format. Framework public APIs consume text and diagnostic names; they do not discover files or understand `res://`. DTOs, converters, serializer options, and `JsonElement` remain internal to `Convergence.Serialization`.

## Active Content

- `content/reference`: minimal schema and catalog examples.
- `content/demos`: focused runtime demonstrations.
- `content/original/training-annex`: original end-to-end example content.

Every active manifest lists its documents in authored order. DemoHost preserves each pack's directory below its output `Content` root. A manifest is addressed relative to that root, while its document paths are resolved relative to the manifest directory. Resolution remains confined to the configured content root, so separate packs may safely use identical document filenames.

## Validation Layers

The active pre-release authoring contract is schema version `10`. Versions `1`
through `9` are intentionally unsupported after the Action Token,
catalyst-role, stat-modifier, explicit weapon-critical, bounded-hit-count, and
explicit passive-targeting, status-lifetime, and authored equipment-slot
migrations, plus stable shop-offer identity and tracked-stock authoring. The
validator reports an unsupported-schema diagnostic instead of
translating old documents. Active
example packs are version `0.10.0`, while the independently authored Training
Annex pack is version `0.10.1`; exact dependency versions advance with the
contract.

1. Draft 2020-12 schemas validate document structure independently of Framework code.
2. Strict deserialization validates the same fields and discriminators while mapping JSON into immutable definitions.
3. Semantic validation checks IDs, ranges, references, supported types, and explicit host registrations.
4. Catalog loading checks paths, dependencies, versions, direct visibility, external references, and canonical qualification.

## Schema v10

The authored schemas live under [`../schemas/content/v10`](../schemas/content/v10).
They use stable `urn:convergence:schema:content:v10:*` identifiers and reject
unknown properties. Every active document must declare the schema matching its
manifest document type:

| Manifest type | `$schema` |
|---|---|
| `skills` | `urn:convergence:schema:content:v10:skills` |
| `entities` | `urn:convergence:schema:content:v10:entities` |
| `races` | `urn:convergence:schema:content:v10:races` |
| `ailments` | `urn:convergence:schema:content:v10:ailments` |
| `items` | `urn:convergence:schema:content:v10:items` |
| `equipment` | `urn:convergence:schema:content:v10:equipment` |
| `shops` | `urn:convergence:schema:content:v10:shops` |
| `negotiations` | `urn:convergence:schema:content:v10:negotiations` |
| `encounters` | `urn:convergence:schema:content:v10:encounters` |
| `dungeons` | `urn:convergence:schema:content:v10:dungeons` |
| `fusion` | `urn:convergence:schema:content:v10:fusion` |
| `rulesets` | `urn:convergence:schema:content:v10:rulesets` |

Manifests use `urn:convergence:schema:content:v10:manifest`. Shared definitions
use `urn:convergence:schema:content:v10:shared` and are not content documents.

Every weapon basic attack must explicitly author its critical behavior with the
same `never` or `chance` definition used by typed damage effects. Schema v10
rejects the pre-release shape that omitted this decision; the runtime never
invents a weapon critical chance. A basic attack may additionally expose a
local `primaryEffectId` and append `secondaryEffects`; those secondary records
use the same typed effect union as skills and items.

Equipment records identify their authored layout position through `slotId`, a
normal `ContentId`, rather than a closed wire enum. JSON Schema validates that
identity but deliberately does not decide which slot/profile combinations a
game supports. Semantic validation delegates that decision to the selected
`IEquipmentSlotLayoutPolicy`. The supplied
`StandardEquipmentSlotLayoutPolicy` recognizes stable `weapon`, `armor`,
`boots`, and `accessory` IDs and requires exactly the matching standard profile;
a custom policy may author a different vocabulary or mapping without changing
the equipment wire shape.

An equipment definition may grant skill IDs. These references do not alter an
actor's learned skills or move-list capacity: the runtime exposes them only
while an owning instance is currently equipped. Active grants are executable
through canonical action authorization; passive grants participate in the
existing passive modifier and lifecycle services. Standard armor `defense` and
`evasion`, boots `evasion`, accessory stat modifiers, and weapon basic attacks
are resolved together into the actor's current equipment profile.

Every shop offer requires a shop-local `id`. Runtime and save state combine it
with the qualified containing shop ID, so menu order and offered content ID are
never durable identity. Unlimited stock has no quantity. Fixed limited stock
requires a positive `quantity` and binds `standard_shop_stock`; policy stock
requires a positive `quantity`, `stockPolicyId`, and optional policy parameters.
Duplicate local offer IDs and malformed tracked-stock shapes reject during
content validation.

Effects may expose a local `effectId` and later effects may declare a typed
`dependency` on an earlier ID in the same sequence. Local IDs cannot carry a
pack qualifier. Dependencies declare `succeeded` or `positive_damage` and a
`same_target` or `any_target` scope. Shared-contact damage requires a
same-target positive-damage dependency; independent damage performs its own hit
resolution. Semantic validation rejects duplicate IDs, missing or forward
sources, incompatible positive-damage sources, and malformed shared-contact
graphs.

Schema v10 gives authored runtime status state an explicit `lifetime` object:

```json
{
  "expiration": {
    "type": "turns",
    "value": 3,
    "tick": "owner_turn_end",
    "suspendWhileReserve": true
  },
  "allowedRemovalCauses": [
    "cure_effect",
    "duration_expired",
    "battle_end"
  ]
}
```

`expiration` defines the clock. `allowedRemovalCauses` independently defines
which typed expiry, cure, cleanup, consumption, or scripted requests may remove
the state. Instant, turn, phase, and battle expirations must include
`duration_expired`; permanent state need not. Duplicate or unknown causes are
invalid. Ailments require `defaultLifetime`; apply-ailment overrides, charges,
and shields may provide `lifetime`; affinity Break and override effects require
it. Stat modifiers deliberately retain their `duration` field because their
accumulation and independently timed contribution policies own that separate
contract. Schema v10 rejects the former status-effect `duration` shape rather
than guessing a removal profile.

JSON Schema is the structural authoring contract: exact property names, enum
values, discriminated unions, required members, and basic numeric/string ranges.
Its local numeric constraints match semantic validation for nonnegative damage
power and amount values, `0..100` accuracy/chance fields, nonzero stat-stage
deltas, positive charge multipliers, positive turn durations, and damage-effect
hit counts within `1..1024`. The supplied standard combat policy applies its
own configurable `maximumHitsPerDamageEffect` ceiling, which defaults to `64`
and may not exceed the published `1024` authoring limit. Percentage
resource amounts are nonnegative but are not capped at one hundred; runtime
resource bounds remain authoritative when such an amount is applied.
Authored accuracy, critical, ailment, instant-defeat, escape, chance-condition,
and resource-percentage-condition values are rejected outside `0..100`; they
are never repaired by clamping. A selected runtime policy may clamp only a
derived chance produced from a valid authored base and explicit modifiers.
The Framework validator remains authoritative for graph rules that JSON Schema
cannot establish from one document, including dependency visibility, duplicate
IDs, catalog references, registrations, floor ranges, and operation-specific
host capabilities.

One skill may contain at most one cost entry for each resource ID. This keeps
assessment and affordability independent of authored list order. The
`party_size` condition accepts nonnegative values; it counts living deployed
participants on the acting actor's team, so zero is a meaningful empty-
deployment condition rather than malformed content.

The schema contract is independently exercised with `JsonSchema.Net` 9.2.2.
All active documents must pass both the schema and Framework deserialization,
validation, and catalog construction paths.

Every passive trigger explicitly authors a `targeting` object. Its `scope`
selects the owner, event-supplied targets, owner team, opposing teams, or all
participants; `lifeState` filters alive, defeated, or any actors; and
`includeReserveActors` decides whether undeployed actors may be selected.
Catalog qualification preserves this targeting unchanged. Schema v10 rejects
the earlier trigger shape instead of silently treating it as event-targeted.

The checked-in [Content Authoring Validator](content-authoring-validator.md)
executes those layers as one host-side command without adding filesystem or
JSON Schema dependencies to Framework.

Definitions and catalogs retain no serializer-owned values. Hosts may use another source format by mapping it into the same domain contracts or by supplying compatible JSON text.

Ruleset categories identify generic service families. Policy IDs are validated
as host vocabulary, then resolved through a host-supplied typed policy-factory
registry. Content registration alone does not install runtime behavior, and the
resolver never substitutes an unregistered policy. In particular,
`turn_economy` is the category for any `IBattleTurnEconomy` implementation;
`standard_action_token` is the policy ID for the optional Action Token
implementation supplied by Convergence. `stat_modifier` independently selects
how signed stat changes accumulate, expire, and clear; it does not select the
`stat` category's numeric stage-scaling table. The supported supplied policies
and parameters are normatively listed in
[Ruleset Policy Contracts](ruleset-policy-contracts.md).

Schema v10 accepts the neutral `general` value for `grant_charge` so a custom
combat composition can select `UnifiedChargePolicy`; the supplied split policy
continues to accept only `physical` and `magical`. The optional standard-damage
parameter `itemActionOutcomeBehavior` selects `normal` or `effect_driven`
turn-cost aggregation without changing any item effect definition.

Entity `skillUnlocks` are ordered authored progression records. Each row pairs a
positive level with a skill ID. Runtime growth evaluates the rows in document
order when an actor crosses those levels. Content does not decide what happens
when a move list is full; the host-selected
`IRuntimeMoveListCapacityPolicy` and skill-choice transaction own that runtime
rule.

Stat rulesets may provide `stageTables` to override supported supplied
track/channel mappings. Every authored table must define the complete `-4..+4`
stage range. Ruleset binding, rather than display text, determines which table
is used.

## Generic Content Policy

Checked-in active content must be generic, original, and small enough to review. It demonstrates contracts; it is not mandatory built-in game data. Historical prototype content is archived and never loaded by active projects.
