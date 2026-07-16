# Content Contract

## Ownership

JSON is a host-supplied authoring and import format. Framework public APIs consume text and diagnostic names; they do not discover files or understand `res://`. DTOs, converters, serializer options, and `JsonElement` remain internal to `Convergence.Serialization`.

## Active Content

- `content/reference`: minimal schema and catalog examples.
- `content/demos`: focused runtime demonstrations.
- `content/original/training-annex`: original end-to-end example content.

Every active manifest lists its documents in authored order. DemoHost preserves each pack's directory below its output `Content` root. A manifest is addressed relative to that root, while its document paths are resolved relative to the manifest directory. Resolution remains confined to the configured content root, so separate packs may safely use identical document filenames.

## Validation Layers

The active pre-release authoring contract is schema version `3`. Versions `1` and `2` are intentionally unsupported after the Action Token and catalyst-role migrations; the validator reports an unsupported-schema diagnostic instead of translating old documents.

1. Draft 2020-12 schemas validate document structure independently of Framework code.
2. Strict deserialization validates the same fields and discriminators while mapping JSON into immutable definitions.
3. Semantic validation checks IDs, ranges, references, supported types, and explicit host registrations.
4. Catalog loading checks paths, dependencies, versions, direct visibility, external references, and canonical qualification.

## Schema v3

The authored schemas live under [`../schemas/content/v3`](../schemas/content/v3).
They use stable `urn:convergence:schema:content:v3:*` identifiers and reject
unknown properties. Every active document must declare the schema matching its
manifest document type:

| Manifest type | `$schema` |
|---|---|
| `skills` | `urn:convergence:schema:content:v3:skills` |
| `entities` | `urn:convergence:schema:content:v3:entities` |
| `races` | `urn:convergence:schema:content:v3:races` |
| `ailments` | `urn:convergence:schema:content:v3:ailments` |
| `items` | `urn:convergence:schema:content:v3:items` |
| `equipment` | `urn:convergence:schema:content:v3:equipment` |
| `shops` | `urn:convergence:schema:content:v3:shops` |
| `negotiations` | `urn:convergence:schema:content:v3:negotiations` |
| `encounters` | `urn:convergence:schema:content:v3:encounters` |
| `dungeons` | `urn:convergence:schema:content:v3:dungeons` |
| `fusion` | `urn:convergence:schema:content:v3:fusion` |
| `rulesets` | `urn:convergence:schema:content:v3:rulesets` |

Manifests use `urn:convergence:schema:content:v3:manifest`. Shared definitions
use `urn:convergence:schema:content:v3:shared` and are not content documents.

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
implementation supplied by Convergence. The supported built-in parameters are
normatively listed in [Ruleset Policy Contracts](ruleset-policy-contracts.md).

## Generic Content Policy

Checked-in active content must be generic, original, and small enough to review. It demonstrates contracts; it is not mandatory built-in game data. Historical prototype content is archived and never loaded by active projects.
