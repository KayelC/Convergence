# Content Contract

## Ownership

JSON is a host-supplied authoring and import format. Framework public APIs consume text and diagnostic names; they do not discover files or understand `res://`. DTOs, converters, serializer options, and `JsonElement` remain internal to `Convergence.Serialization`.

## Active Content

- `content/reference`: minimal schema and catalog examples.
- `content/demos`: focused runtime demonstrations.
- `content/original/training-annex`: original end-to-end example content.

Every active manifest lists its documents in authored order. DemoHost flattens these files into its output `Content` directory as a host deployment choice; logical manifest paths remain canonical.

## Validation Layers

The active pre-release authoring contract is schema version `3`. Versions `1` and `2` are intentionally unsupported after the Action Token and catalyst-role migrations; the validator reports an unsupported-schema diagnostic instead of translating old documents.

1. Strict deserialization validates JSON structure, fields, and discriminators.
2. Semantic validation checks IDs, ranges, references, supported types, and explicit host registrations.
3. Catalog loading checks paths, dependencies, versions, direct visibility, external references, and canonical qualification.

Definitions and catalogs retain no serializer-owned values. Hosts may use another source format by mapping it into the same domain contracts or by supplying compatible JSON text.

Ruleset categories identify generic service families. In particular, `turn_economy` is the category for any `IBattleTurnEconomy` implementation; `standard_action_token` is the policy ID for the optional Action Token implementation supplied by Convergence.

## Generic Content Policy

Checked-in active content must be generic, original, and small enough to review. It demonstrates contracts; it is not mandatory built-in game data. Historical prototype content is archived and never loaded by active projects.
