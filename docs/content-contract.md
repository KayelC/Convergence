# Content Contract

## Ownership

JSON is a host-supplied authoring and import format. Framework public APIs consume text and diagnostic names; they do not discover files or understand `res://`. DTOs, converters, serializer options, and `JsonElement` remain internal to `Convergence.Serialization`.

## Active Content

- `content/reference`: minimal schema and catalog examples.
- `content/demos`: focused runtime demonstrations.
- `content/original/training-annex`: original end-to-end example content.

Every active manifest lists its documents in authored order. DemoHost preserves each pack's directory below its output `Content` root. A manifest is addressed relative to that root, while its document paths are resolved relative to the manifest directory. Resolution remains confined to the configured content root, so separate packs may safely use identical document filenames.

## Validation Layers

The active pre-release authoring contract is schema version `5`. Versions `1`
through `4` are intentionally unsupported after the Action Token,
catalyst-role, stat-modifier, and explicit weapon-critical migrations. The validator reports an
unsupported-schema diagnostic instead of translating old documents. Active
example packs are version `0.5.0`; exact dependency versions advance with the
contract.

1. Draft 2020-12 schemas validate document structure independently of Framework code.
2. Strict deserialization validates the same fields and discriminators while mapping JSON into immutable definitions.
3. Semantic validation checks IDs, ranges, references, supported types, and explicit host registrations.
4. Catalog loading checks paths, dependencies, versions, direct visibility, external references, and canonical qualification.

## Schema v5

The authored schemas live under [`../schemas/content/v5`](../schemas/content/v5).
They use stable `urn:convergence:schema:content:v5:*` identifiers and reject
unknown properties. Every active document must declare the schema matching its
manifest document type:

| Manifest type | `$schema` |
|---|---|
| `skills` | `urn:convergence:schema:content:v5:skills` |
| `entities` | `urn:convergence:schema:content:v5:entities` |
| `races` | `urn:convergence:schema:content:v5:races` |
| `ailments` | `urn:convergence:schema:content:v5:ailments` |
| `items` | `urn:convergence:schema:content:v5:items` |
| `equipment` | `urn:convergence:schema:content:v5:equipment` |
| `shops` | `urn:convergence:schema:content:v5:shops` |
| `negotiations` | `urn:convergence:schema:content:v5:negotiations` |
| `encounters` | `urn:convergence:schema:content:v5:encounters` |
| `dungeons` | `urn:convergence:schema:content:v5:dungeons` |
| `fusion` | `urn:convergence:schema:content:v5:fusion` |
| `rulesets` | `urn:convergence:schema:content:v5:rulesets` |

Manifests use `urn:convergence:schema:content:v5:manifest`. Shared definitions
use `urn:convergence:schema:content:v5:shared` and are not content documents.

Every weapon basic attack must explicitly author its critical behavior with the
same `never` or `chance` definition used by typed damage effects. Schema v5
rejects the pre-release shape that omitted this decision; the runtime never
invents a weapon critical chance.

JSON Schema is the structural authoring contract: exact property names, enum
values, discriminated unions, required members, and basic numeric/string ranges.
The Framework validator remains authoritative for graph rules that JSON Schema
cannot establish from one document, including dependency visibility, duplicate
IDs, catalog references, registrations, floor ranges, and operation-specific
host capabilities.

The schema contract is independently exercised with `JsonSchema.Net` 9.2.2.
All active documents must pass both the schema and Framework deserialization,
validation, and catalog construction paths.

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

Schema v5 accepts the neutral `general` value for `grant_charge` so a custom
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
