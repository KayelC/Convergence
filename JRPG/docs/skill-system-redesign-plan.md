# Skill System Redesign Plan

## Status

This document is the implementation plan for replacing the legacy and experimental skill-data systems with the model defined by the [Skill System GDD](skill-system-gdd.md).

The plan is intentionally incremental. The current console prototype must continue to build and run while the replacement is developed. Obsolete code and datasets are removed only after their consumers have migrated and automated checks prove they are unused.

## Authority And Decision Order

When implementation details conflict, use this order of authority:

1. [Skill System GDD](skill-system-gdd.md) for skill behavior, elements, affinities, resistances, passives, menu groups, and inheritance groups.
2. [Content Schema v1 Proposal](content-schema-v1-proposal.md) for the wider content-pack architecture and entity/content boundaries.
3. This plan for implementation order, file ownership, compatibility, testing, and removal gates.
4. Existing runtime code and legacy datasets as behavioral evidence, not as design authority.

The schema proposal still contains examples written before the final GDD decisions. Track 1 must reconcile those examples before schema code is treated as stable.

## Target Outcome

At completion, the framework should have one authoritative content path:

```text
JSON content pack
  -> structural deserialization
  -> schema validation
  -> cross-reference validation
  -> immutable GameDataCatalog
  -> runtime skill/entity adapters
  -> active effect executor and passive rule system
  -> battle, field, item, and fusion consumers
```

The completed system must provide:

- eight damage elements: `physical`, `fire`, `ice`, `electric`, `wind`, `light`, `dark`, and `almighty`,
- elemental affinities distinct from ailment and instant-death resistances,
- active and passive activation models,
- ordered active effects,
- triggered passive effects and passive rule modifiers,
- explicit menu and inheritance groups,
- a dedicated `passive` inheritance group,
- direct entity allow/deny inheritance rules,
- immutable definitions separated from mutable combat state,
- no gameplay behavior inferred from display names or descriptions,
- no `special` skill kind or generic behavior-driving tags,
- no runtime dependency on legacy static JSON DTOs after migration,
- test fixtures that demonstrate the approved model without importing the discarded datasets.

## Scope

This redesign includes:

- skill and entity schema contracts needed to execute and inherit skills,
- elemental affinity and ailment-resistance definitions,
- skill loading, validation, cataloguing, and lookup,
- active skill execution,
- passive triggers and rule modifiers,
- fusion inheritance eligibility,
- item reuse of the shared effect model where required by migrated consumers,
- removal of obsolete skill/entity migration scaffolding and datasets,
- documentation and automated tests supporting those changes.

This redesign does not require:

- final dungeon schema or Godot scene integration,
- production-scale content authoring,
- migration of the discarded legacy dataset,
- configurable growth, resource, or AI profiles,
- host presentation data such as portraits, models, animations, or resource paths,
- completing unrelated negotiation, shop, or dungeon redesign work.

## Baseline Facts

After Track 3, the branch has two intentional representations during migration:

| Representation | Current role | Key files |
| --- | --- | --- |
| Legacy runtime data | Actual source used by the console prototype | `Data/Jsons/skills_database.json`, `Data/Jsons/entity_database.json`, `Data/Database.cs`, `Data/SkillData.cs`, `Data/PersonaData.cs` |
| Redesign domain | Immutable definitions that implement the approved GDD vocabulary but are not yet loaded by runtime | `Data/Definitions/`, `Convergence.Tests/SkillSystem/` |

The discarded typed adapter and one-payload clean-schema experiment were removed in Track 3. They had no console runtime consumers.

The generated `skills_database_v2.json` and `entity_database_v2.json` are not loaded by `Database.LoadData`. They are obsolete source material once the redesign fixtures and contracts are established.

## Branch And Commit Strategy

The work should occur on `skill-system-redesign` branch.

The current documentation changes should become the first commit on that branch. Each track should then be represented by one or more focused commits. Avoid a single final cleanup commit containing schema, runtime, tests, and deletions together.

Recommended commit sequence:

1. `docs: establish skill redesign baseline`
2. `testdata: add redesign reference fixtures`
3. `data: replace experimental skill schema contracts`
4. `data: add graph validation and immutable catalog`
5. `battle: execute active skills from typed effects`
6. `battle: add passive triggers and rule modifiers`
7. `fusion: enforce inheritance group policies`
8. `runtime: migrate remaining skill consumers`
9. `cleanup: remove obsolete migration scaffolding and datasets`
10. `docs: record final architecture and migration result`

Every commit must compile. Commits that change behavior must add or update tests in the same commit.

## Track Summary

| Track | Name | Primary outcome | Depends on |
| --- | --- | --- | --- |
| 0 | Redesign Baseline | Protected branch and approved design baseline | None |
| 1 | Contract Reconciliation | One precise, contradiction-free schema design | Track 0 |
| 2 | Reference Fixtures | Minimal original content proving the risky rules | Track 1 |
| 3 | Domain Definitions | Immutable C# records matching the GDD | Tracks 1-2 |
| 4 | Schema DTOs And Deserialization | JSON shapes convert without behavior inference | Track 3 |
| 5 | Validation And Diagnostics | Invalid content is rejected before catalog exposure | Track 4 |
| 6 | Catalog And Loader | One immutable repository surface for new content | Track 5 |
| 7 | Combat Vocabulary | Eight elements and separate defense systems | Tracks 3-6 |
| 8 | Active Skill Execution | Ordered effects drive battle behavior | Tracks 6-7 |
| 9 | Passive Skill System | Triggers and modifiers replace name checks | Tracks 6-8 |
| 10 | Fusion Inheritance | Group policies support passive fusion fodder | Tracks 6 and 9 |
| 11 | Runtime Consumer Migration | Existing systems use typed definitions | Tracks 8-10 |
| 12 | Shared Effects Beyond Skills | Items and field actions reuse effects | Track 11 |
| 13 | Legacy Removal | Obsolete schemas, mappers, and data are deleted | Tracks 11-12 |
| 14 | Quality And Merge Gate | Branch is proven safe to merge into `main` | All tracks |

## Track 0: Redesign Baseline

### Purpose

Protect the stable prototype and give the redesign a reviewable history.

### Work

- Create `codex/skill-system-redesign` from current `main`.
- Carry the uncommitted GDD, schema proposal, plan, and fixture work onto the branch.
- Commit the approved documentation baseline before runtime implementation.
- Record the baseline test result and known warnings.

### Files

- `docs/skill-system-gdd.md`
- `docs/content-schema-v1-proposal.md`
- `docs/skill-system-redesign-plan.md`
- `docs/README.md`

### Exit Criteria

- The redesign branch exists.
- `main` remains unchanged.
- Design documents are committed and linked from the documentation index.
- `dotnet test JRPG.sln --no-restore` passes at the baseline commit.

### Baseline Verification Record

- Baseline verified on June 12, 2026.
- Branch: `skill-system-redesign`.
- Starting commit: `fb6a7f798d341b49c963e7bb22378a4304468aa0` (`Skill System Redesign`).
- Baseline command: `dotnet test JRPG.sln --no-restore --filter "FullyQualifiedName!~SkillSystemRedesignFixtureTests"`.
- Baseline result: 237 passed, 0 failed, 0 skipped.
- The repository retains its pre-existing nullable-reference and DTO-initialization warnings; warning remediation is outside Track 0.
- The intended Track 0 scope was documentation-only, but the actual commit `e890843` also included the reference fixture JSON and `SkillSystemRedesignFixtureTests.cs` as Track 2 prework.
- No runtime or gameplay code was modified, and the tracked fixtures do not affect runtime loading.

## Track 1: Contract Reconciliation

### Purpose

Turn the GDD decisions into an exact schema contract before writing DTOs. This prevents implementing a second obsolete schema.

### Decisions To Encode

- Elements are exactly the eight GDD elements.
- `physical` replaces Slash, Strike, and Pierce as a damage element.
- Earth, Mind, Nerve, Curse, and `None` are not damage elements.
- Elemental affinities are `weak`, `normal`, `resist`, `null`, `repel`, and `absorb`.
- Almighty does not accept authored affinities and always resolves as normal.
- Ailment and instant-death resistance uses `vulnerable`, `normal`, `resistant`, and `immune`.
- Skill activation is `active` or `passive`.
- Active menu groups are `offense`, `ailment`, `recovery`, `buff`, `debuff`, and `utility`.
- Inheritance groups are the eight elements plus `recovery`, `ailment`, `support`, `utility`, and `passive`.
- Every passive has inheritance group `passive`, regardless of the element or mechanic it modifies.
- Active skills have ordered `effects`; passive skills have `triggers`, `modifiers`, or both.
- `special` is removed. Exceptional mechanics use registered `custom` effects.
- Entity inheritance uses one group allow/deny policy plus explicit skill exceptions.
- Skill mutation preserves the existing fusion-accident family/rank behavior through a separate nested `mutation` object with `familyId` and `tier`.
- Ailment resistance is keyed directly by ailment ID; ailment groups may support cures and modifiers but never select resistance.
- Eternal Rest uses the `ailment` inheritance group because Sleep is its defining prerequisite.
- Oracle and other Navigator abilities are outside the demon/Persona stock skill contract and are deferred to a dedicated future system.
- Basic weapon damage uses `physical`; Slash, Strike, and Pierce are not affinities.

### Finalized Schema Contract

- `inheritanceGroupId` is a top-level skill field. The nested `inheritance` object contains `isInheritable` and owner-exclusivity data.
- `allowedSkillIds` overrides group policy after non-inheritable, owner-exclusive, and explicit-block checks.
- Active skills require `menuGroup`; validation rejects `menuGroup` on passive skills.
- Effects use optional `onFailure`: `continue`, `stop_target`, or `stop_action`. Omission defaults to `continue`.
- Misses, failed chance rolls, and resistance prevention are failures. False `when` conditions are skipped, valid no-change effects are successful, and battle interruptions override failure policy.
- Effects and modifiers use one optional `when` expression tree. Effect conditions evaluate independently for each target.
- Modifier stacking is code-owned. JSON does not author stacking keys or priorities; applicable Boost/Amp-style damage multipliers compose multiplicatively.
- Hama and Mudo check separate `light` and `dark` instant-death resistance channels. Eternal Rest uses an explicit no-resistance mode after its Sleep condition passes.
- Elemental-affinity passives use the strongest response from base and passive replacements: `absorb > repel > null > resist > normal > weak`. Shields override it; when no shield applies, Break temporarily normalizes it.
- Mutation uses optional nested `mutation` metadata. Tiers start at one, family/tier pairs are unique, and only adjacent tiers in the same family are mutation targets.

### Files

- Revise `docs/content-schema-v1-proposal.md`.
- Keep `docs/skill-system-gdd.md` as the normative behavior document.
- Keep `docs/README.md` explicit about document authority and status.
- Reserve `Data/Schemas/SkillSystem/` for the eventual JSON Schema files; creating them belongs to the schema/validation tracks, not Track 1.

Proposed schema files:

```text
Data/Schemas/SkillSystem/
  skills.schema.json
  entities.schema.json
  races.schema.json
  ailments.schema.json
  manifest.schema.json
```

### Exit Criteria

- No schema example uses Slash, Strike, Pierce, Earth, Mind, Nerve, Curse, or `None` as an element.
- No schema example uses element-based inheritance for a passive.
- Recovery and curing are effects, not elements.
- Elemental affinity and ailment resistance are represented separately.
- Mutation metadata is separate from inheritance metadata.
- Navigator-only abilities do not appear as ordinary skills.
- All JSON examples parse.
- Every Track 1 contract decision is explicit in the GDD, proposal, and this plan.

### Track 1 Completion Record

- Completed on June 13, 2026 on branch `skill-system-redesign`, starting from commit `98bd805` (`Docs update`).
- Added explicit documentation authority and status labels.
- Corrected obsolete skill examples and resistance vocabulary in the schema proposal.
- Preserved current runtime and technical documents as labeled migration references rather than deleting them.
- Marked the older refactor roadmap as historical for skill-system work.
- Finalized inheritance placement and precedence, passive menu validation, effect failure semantics, condition shape, modifier stacking, instant-death channels, affinity precedence, and mutation constraints.
- Contract commit: `26a05f2` (`docs: finalize skill system contract`).
- Parsed 34 embedded JSON examples and all 4 redesign fixture documents successfully.
- `dotnet test JRPG.sln --no-restore`: 238 passed, 0 failed, 0 skipped.
- `git diff --check` passed.
- Targeted searches found no rejected element fields, `resistanceElementId`, `mutationTier`, Oracle handler example, or obsolete JSON `conditions` property in the target contract and fixture.
- The repository retains its pre-existing nullable-reference and DTO-initialization warnings; warning remediation remains outside Track 1.
- No runtime or gameplay code is changed by this pass.

## Track 2: Reference Fixtures

### Purpose

Provide tiny original datasets that become executable examples of the target contract. These fixtures are not migrated legacy content and must remain easy to inspect in code review. Track 1 fixes their wire vocabulary; later tracks add DTO and schema validation.

### Initial One-Entry Fixtures

The first fixtures intentionally exercise the passive-inheritance decision:

- `skill_system_redesign.skills.sample.json` contains one passive skill, Ice Boost.
- `skill_system_redesign.entities.sample.json` contains one fusion-fodder demon that denies the Ice inheritance group but owns Ice Boost.
- `skill_system_redesign.races.sample.json` contains the entity's single referenced race.

These files live in `Data/Jsons` initially because `JRPG.csproj` already copies top-level JSON files to the output directory, while `Database.LoadData` only loads explicitly named legacy files. This makes the fixtures available to xUnit without changing live runtime loading.

### Fixture Expansion

After schema code exists, expand the reference pack in small commits to cover:

- one physical damage skill,
- one elemental damage skill,
- one damage-plus-ailment skill,
- one recovery skill,
- one cure skill,
- one revival skill,
- one buff and one debuff,
- one utility skill,
- one triggered passive,
- one rule-modifier passive,
- one conditional instant-kill skill,
- one deliberately invalid file per major validation category.

### Test Usage

Track 3 removes `Convergence.Tests/CleanDataMigrationTests.cs` with the older experimental schema. Redesign tests use this target organization:

```text
Convergence.Tests/SkillSystem/
  SkillSchemaDeserializationTests.cs
  SkillSchemaValidationTests.cs
  EntitySchemaValidationTests.cs
  ReferencePackCatalogTests.cs
  PassiveInheritanceTests.cs
```

Tests should locate fixtures from `AppContext.BaseDirectory/Data/Jsons` until a dedicated test-content copy target is introduced.

### Exit Criteria

- Each initial fixture contains exactly one record.
- Every fixture uses only original names and content.
- The entity denies `ice` but references the passive Ice Boost skill.
- Loading fixtures does not affect `Database.LoadData` or the console prototype.
- Fixture JSON parses before schema implementation begins.

### Track 2 Completion Record

- Completed on June 13, 2026 on branch `skill-system-redesign`.
- The original one-entry reference pack and fixture test were introduced in commit `e890843` (`Added Samples and Update Plan`).
- Track 1 aligned Ice Boost with the finalized `when` condition contract in commit `687c66d` (`testdata: align redesign fixture with skill contract`).
- The completed pack retains exactly one skill, one entity, and one race; the manifest contains exactly the three corresponding document mappings.
- The fixture test now verifies schema versions, unique manifest types and paths, manifest file resolution and parsing, cross-document references, passive inheritance metadata, and the Ice-denying fusion-fodder invariant.
- Targeted fixture verification passed: 1 passed, 0 failed, 0 skipped.
- Full verification passed: 238 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore`.
- The existing nullable-reference and DTO-initialization warnings remain outside Track 2.
- The fixture JSON, runtime APIs, DTOs, schema validators, and `Database.LoadData` were not changed by the completion pass.
- Broader active-skill and invalid-content fixtures remain deferred until the schema and validation tracks exist.

## Track 3: Domain Definitions

### Purpose

Replace the one-kind/one-payload experimental model with immutable records that match the GDD.

### Current Code To Replace

- `Data/Definitions/SkillDefinitions.cs`
- `Data/Definitions/EntityDefinition.cs`
- Duplicate older definitions in `Data/SkillDefinitions.cs`

The current `SkillKind`, `SkillEffectPayload`, `PassiveSkillPayload`, and `SpecialSkillPayload` hierarchy cannot represent ordered effects or shared active/passive effects and should not be extended.

### Proposed Code Shape

Use immutable records and closed discriminated hierarchies:

```text
SkillDefinition
  ContentId Id
  string DisplayName
  string Description
  SkillActivation Activation
  SkillMenuGroup? MenuGroup
  InheritanceGroup InheritanceGroup
  SkillInheritanceDefinition Inheritance
  SkillMutationDefinition? Mutation
  IReadOnlyList<SkillCostDefinition> Costs
  TargetingDefinition? Targeting
  IReadOnlyList<EffectDefinition> Effects
  IReadOnlyList<PassiveTriggerDefinition> Triggers
  IReadOnlyList<RuleModifierDefinition> Modifiers
```

Core enums or value objects:

```text
DamageElement
ElementalAffinity
ResistanceLevel
SkillActivation
SkillMenuGroup
InheritanceGroup
EffectFailurePolicy
InstantDeathResistanceMode
InstantDeathChannel
```

Effect records should derive from `EffectDefinition`, for example:

```text
DamageEffectDefinition
InstantKillEffectDefinition
ApplyAilmentEffectDefinition
RestoreResourceEffectDefinition
RemoveAilmentEffectDefinition
ReviveEffectDefinition
ModifyStatStageEffectDefinition
GrantChargeEffectDefinition
GrantShieldEffectDefinition
OverrideAffinityEffectDefinition
RemoveStatusEffectDefinition
ReduceResourceEffectDefinition
SetResourceEffectDefinition
AnalyzeEffectDefinition
EscapeEffectDefinition
CustomEffectDefinition
```

Passive records should use ordinary effects for triggers and bounded modifier definitions for continuous rules.

### Implementation Guidance

- Prefer sealed records for immutable definitions.
- Prefer enums for the closed GDD vocabulary.
- Use `ContentId` or validated strings for content references.
- Keep runtime state such as remaining duration, current stacks, and source actor out of definitions.
- Avoid storing delegates in content definitions.
- Avoid a general dictionary of unvalidated parameters except inside registered custom handlers.

### Exit Criteria

- `SpecialSkillPayload` no longer exists in the new definition namespace.
- A definition can represent Salvation as two effects.
- A definition can represent Regenerate as a trigger using `restore_resource`.
- A definition can represent Ice Boost as a passive `damage_dealt` modifier filtered to Ice.
- Definitions do not reference legacy `SkillData` or `PersonaData`.

### Track 3 Implementation Scope

- Replace the definition namespace in place with immutable skill, entity, race, and ailment records.
- Add canonical `ContentId` values, closed GDD enums, typed conditions, ordered effects, passive triggers, and bounded modifiers.
- Remove the unused mapper/schema/catalog experiment and its two sample files and test classes.
- Preserve `Database`, `SkillData`, `PersonaData`, the generated v2 datasets, and all console runtime consumers.

### Track 3 Completion Record

- Completed on June 13, 2026 on branch `skill-system-redesign`.
- Added canonical `ContentId`, closed GDD vocabularies, typed targeting/amount/duration/condition primitives, the complete approved effect hierarchy, passive triggers and modifiers, and immutable skill definitions.
- Replaced the entity definition and added race and ailment definitions with separate elemental affinities, ailment resistances, instant-death resistances, inheritance policy, turn behavior, and recovery data.
- Every supplied collection is copied into a read-only snapshot; only registered custom handlers and formulas expose copied parameter dictionaries.
- Removed the unused mapper, validation, schema, and catalog experiment, its duplicate definitions, its two old sample files, and its two dedicated test classes.
- `Database`, `SkillData`, `PersonaData`, `Database.LoadData`, the generated v2 datasets, and all four redesign reference fixtures remain unchanged.
- Targeted domain verification passed: 22 passed, 0 failed, 0 skipped.
- Full verification passed: 243 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore`.
- `dotnet build JRPG.sln --no-restore` completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- All four redesign fixture documents parsed successfully, `git diff --check` passed, and source searches found no obsolete payload/mapper/schema symbols or legacy runtime types referenced by the new definition namespace.
- Completion commit: `data: replace experimental skill schema contracts`.

## Track 4: Schema DTOs And Deserialization

### Purpose

Deserialize JSON into structural DTOs without executing rules or inferring behavior from text.

### Starting Point

Track 3 removed the obsolete `Data/Schemas` experiment. This track creates new structural DTOs for the approved domain definitions without carrying forward the one-payload schema or its validator.

### Proposed Location

```text
Data/SkillSystem/Schemas/
  SchemaDtos.cs
  SchemaConverters.cs
  SkillSystemDtoMapper.cs
  SkillSystemJsonContext.cs
Data/SkillSystem/
  ContentDeserializationContracts.cs
  SkillSystemJsonDeserializer.cs
```

### JSON Technology

The redesigned content path uses `System.Text.Json` with source-generated metadata. Newtonsoft.Json remains only in the unchanged legacy `Database` loader until legacy removal.

This is a framework-boundary decision rather than a Godot integration decision. Hosts provide JSON text and receive immutable definitions through serializer-neutral interfaces. DTOs, converters, serializer options, `JsonElement`, filesystem access, `res://`, and Godot types remain internal or host-owned. Source generation is required so the import path remains compatible with trimming and future AOT exports.

Structural parsing is strict and case-sensitive. It rejects unknown properties, enum values, discriminators, ambiguous union shapes, comments, trailing commas, and wrong JSON token types while preserving diagnostic source names and JSON paths.

### Mapping Rules

- DTO-to-definition mapping may normalize casing and IDs.
- Mapping must not inspect `displayName` or `description` to choose behavior.
- Mapping must not derive passive inheritance from modifier filters.
- Mapping must reject an active skill with triggers/modifiers and a passive skill with active targeting/effects unless the final contract explicitly permits that shape.
- Active skill availability maps to immutable context IDs; passive skills omit it. Presence, nonempty contexts, and context registration are Track 5 validation rules.

### Exit Criteria

- All reference fixtures deserialize.
- Unknown discriminators fail with actionable errors.
- Display text changes do not alter the resulting definition.
- No legacy parser is called during clean-schema deserialization.
- Public framework APIs expose no serializer, legacy DTO, filesystem, or Godot types.
- Every concrete schema DTO resolves through source-generated metadata.

### Completion Record

- Added serializer-neutral manifest and document-loading contracts plus `SkillSystemJsonDeserializer`; public signatures expose immutable definitions and diagnostic values only.
- Added internal source-generated DTO metadata, strict converters, and DTO-to-domain mapping for skills, entities, races, ailments, all approved effects, conditions, amounts, durations, passive modifiers, and ailment turn behaviours.
- Added immutable active-skill availability using extensible context IDs. Presence, nonempty contexts, and context registration remain Track 5 validation responsibilities.
- The redesigned path uses `System.Text.Json`; Newtonsoft.Json and `Database.LoadData` remain unchanged for legacy content only.
- Schema-focused verification passed: 25 passed, 0 failed, 0 skipped.
- Combined fixture, domain-definition, and schema verification passed: 48 passed, 0 failed, 0 skipped.
- Full verification passed: 268 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore --nologo`.
- `dotnet build JRPG.sln --no-restore --nologo -t:Rebuild` completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- Strict-reader tests cover unknown properties and discriminators, exact property and enum casing, wrong token types, explicit nulls, comments, trailing commas, and ambiguous condition nodes with source-aware diagnostics.
- Public-boundary and metadata tests confirm that serializer/Godot/legacy types do not leak and that every concrete schema DTO resolves without reflection fallback.
- Completion commit: `data: add portable skill schema deserialization`.

## Track 5: Validation And Diagnostics

### Purpose

Reject invalid content before any runtime service can obtain a catalog.

### Validation Layers

1. Track 4 structural deserialization: JSON fields, token types, enums, discriminators, and union shapes.
2. Track 5 document validation: schema versions, manifest coverage, local IDs, duplicates, and local consistency.
3. Track 5 semantic validation: ranges, active/passive shapes, references, inheritance, mutation families, and explicit host registrations.
4. Track 6 catalog validation: pack qualification, dependencies, external references, and immutable catalog construction.

### Required Rules

- IDs are normalized and case-insensitively unique.
- Active skills require a menu group and active effect list.
- Passive skills use inheritance group `passive`.
- Passive skills reject `menuGroup`; active skills require it.
- Passive skills define at least one trigger or modifier.
- Almighty is rejected from entity affinity maps.
- Entity affinity maps accept only the seven resistible damage elements.
- Ailment resistance maps cannot use `repel` or `absorb`.
- Instant-death resistance maps accept only the fixed `light` and `dark` channels and the four resistance levels.
- `instant_kill` requires a discriminated resistance check using a Light/Dark channel or explicit `none` mode.
- Effects accept only `continue`, `stop_target`, or `stop_action`; omission means `continue`.
- Effects and modifiers use one `when` tree and reject the obsolete `conditions` array.
- Effect chances and accuracy values stay within `0` through `100`, inclusive.
- Counts, turn durations, entity levels, ranks, and mutation tiers are positive.
- Amounts and powers are nonnegative; multiplicative modifiers and ailment multipliers are positive.
- Balance ceilings remain outside schema validation.
- Costs cannot reduce the actor below allowed bounds.
- Entity skill references resolve.
- Entity allow and deny skill lists cannot contain the same ID.
- Explicit skill allowance cannot override non-inheritable or owner-exclusive restrictions.
- Fusion inheritance policies refer only to declared inheritance groups.
- Mutation tiers are positive, family/tier pairs are unique, and mutation families contain no ambiguous duplicate tier.
- Custom handler and formula IDs must be registered with parameter validators.
- Local and same-pack-qualified content references resolve in this track; external qualified content references are deferred to Track 6.
- Host capability references must be registered even when qualified.

### Diagnostics Shape

Each error should carry:

```text
pack ID
source file
record type
record ID
JSON path
error code
human-readable message
optional suggestion
```

### Files

```text
Data/SkillSystem/Validation/
  ValidationContracts.cs
  SkillSystemRegistrationSnapshot.cs
  SkillSystemContentValidator.cs

Convergence.Tests/SkillSystem/
  ContentValidationTests.cs
  Fixtures/Validation/
```

### Exit Criteria

- Invalid fixtures cover each required rule.
- Validation reports all independent errors in one pass where safe.
- The catalog cannot be created from invalid content.
- Tests assert error codes and paths, not only message fragments.

### Track 5 Completion Record

- Completed on June 13, 2026 on branch `skill-system-redesign`, starting from commit `1815144` (`data: add portable skill schema deserialization`).
- Added serializer-neutral validation requests, source-document provenance, stable error codes, aggregated diagnostics, validation exceptions, and a `ValidatedSkillSystemContentPack` token that only successful validation can create.
- Added immutable explicit host registrations for contexts, resources, stats, modifier tracks, events, phases, entity kinds, alignments, negotiation personalities, ailment groups, battle kinds, moon phases, capabilities, actions, statuses, escape rules, supported definition types, formulas, and custom handlers. Validation supplies no hidden defaults.
- Implemented document, identity, range, shape, reference, inheritance, entity-assignment, ailment, registration, parameter, and mutation-family validation across the complete Track 3 vocabulary.
- Local and same-pack-qualified references resolve to the same target identity. External qualified content references are deferred to Track 6, while host capability references always require explicit registration.
- Numeric checks are contract-only: `0` through `100` probabilities, positive counts/durations/levels/ranks/tiers, nonnegative amounts/powers, positive multipliers, and valid minimum/maximum relationships. No balance ceilings were introduced.
- Added a four-document structurally valid but semantically invalid fixture pack and 16 focused tests covering successful reference-pack validation, aggregated errors, stable paths/codes/provenance, deterministic authored ordering, registrations, parameter validators, same-pack aliases, external deferral, mutation and inheritance rules, immutable snapshots, and API boundaries.
- Focused validation verification passed: 16 passed, 0 failed, 0 skipped.
- Combined skill-system verification passed: 64 passed, 0 failed, 0 skipped.
- Full verification passed: 284 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore --nologo`.
- `dotnet build JRPG.sln --no-restore --nologo -t:Rebuild` completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- `git diff --check` passed; boundary searches found no serializer, Godot, or legacy DTO dependencies in validation; `Database.LoadData`, `Data/Database.cs`, and `JRPG.csproj` remain unchanged.
- Catalog construction, pack dependency graphs, ID qualification, and external-reference resolution remain deferred to Track 6.
- Completion commit: `data: add skill system validation`.

## Track 6: Portable Catalog And Loader

### Purpose

Turn host-supplied JSON text into one validated, dependency-resolved, immutable catalog without introducing filesystem or engine dependencies.

### Starting State

- Track 4 provides portable strict deserialization through `ISkillSystemDocumentDeserializer`.
- Track 5 provides semantic pack validation and the non-constructible `ValidatedSkillSystemContentPack` token.
- `Data/Database.cs` remains the legacy runtime source during consumer migration and is not changed by this track.

### Implemented Code Shape

```text
GameDataCatalog
  IReadOnlyDictionary<ContentId, SkillDefinition> Skills
  IReadOnlyDictionary<ContentId, EntityDefinition> Entities
  IReadOnlyDictionary<ContentId, RaceDefinition> Races
  IReadOnlyDictionary<ContentId, AilmentDefinition> Ailments
```

Repository interfaces may remain where they improve consumer isolation:

```text
ISkillDefinitionRepository
IEntityDefinitionRepository
IAilmentDefinitionRepository
IRaceDefinitionRepository
```

`ContentDocumentText`, `ContentPackTextBundle`, and `SkillSystemCatalogLoadRequest` are immutable host inputs. `ISkillSystemCatalogLoader` and `SkillSystemCatalogLoader` parse manifests and documents, run Track 5, validate dependencies and external references, qualify definitions, and return `CatalogLoadResult`. `RequireCatalog()` throws `CatalogLoadException` when diagnostics exist.

`SemanticVersion` implements strict SemVer 2.0 parsing, comparison, and exact value equality without a package dependency. Manifest dependencies use typed `{ id, version }` objects. Schema v1 requires exact version equality, including prerelease and build metadata.

The loader uses caller bundle order and manifest document order. It rejects noncanonical logical paths, duplicate/missing/unexpected documents, unsupported document types, duplicate packs, invalid dependency graphs, transitive-only references, missing or mistyped external records, and invalid cross-pack explicit inheritance allowances.

Catalog definitions contain canonical qualified record IDs and content-record references. Mutation-family IDs are qualified. Host vocabulary IDs remain unchanged. Repository interfaces require qualified IDs and expose only immutable dictionaries.

### Compatibility

During migration, the console host may construct both:

- the legacy static `Database` for unmigrated systems,
- the new immutable catalog for migrated skill-system consumers.

Do not create a reverse adapter that converts new definitions back into legacy string DTOs. Migrate consumers forward instead.

### Exit Criteria

- Only one clean `GameDataCatalog` type remains.
- The reference fixture pack creates a catalog.
- Repository lookups use IDs, not display names.
- No catalog exposes mutable collections.
- Loader tests prove deterministic manifest ordering, canonical path enforcement, exact versions, dependency cycles, direct-only visibility, qualification, external reference checks, and duplicate detection.
- Public catalog APIs expose no JSON serializer, filesystem, Godot, Newtonsoft, or legacy DTO types.
- `Database.LoadData` and legacy runtime consumers remain unchanged.

### Track 6 Completion Record

- Completed on June 14, 2026 on branch `skill-system-redesign`, starting from commit `69e5463` (`data: add skill system validation`).
- Added framework-owned strict SemVer 2.0 parsing and typed exact manifest dependencies. Build metadata participates in dependency equality while SemVer ordering follows standard precedence.
- Added immutable host text bundles, serializer-neutral aggregate load diagnostics, `ISkillSystemCatalogLoader`, `SkillSystemCatalogLoader`, and one immutable `GameDataCatalog` implementing qualified-ID repositories for skills, entities, races, and ailments.
- Implemented canonical logical path enforcement, manifest-ordered document loading, Track 4 deserialization, Track 5 validation, duplicate pack detection, exact dependency checks, deterministic topological ordering, cycle detection, and direct-dependency-only visibility.
- Implemented cross-pack target/type resolution and inheritance checks, then cloned validated definitions into canonical catalog definitions with qualified record IDs, mutation-family IDs, and content-record references. Host registration IDs remain unchanged.
- Added 25 focused catalog-loader and SemVer verification cases covering the reference fixture, two-pack loading, ordering, exact versions, build metadata, malformed content, paths, duplicates, missing dependencies, cycles, transitive-only access, external targets, inheritance, qualification, immutability, and public API portability.
- Focused catalog verification passed: 25 passed, 0 failed, 0 skipped.
- Combined skill-system verification passed: 89 passed, 0 failed, 0 skipped.
- Full verification passed: 309 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore --nologo`.
- `dotnet build JRPG.sln --no-restore --nologo -t:Rebuild` completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- `git diff --check` passed; catalog boundary searches found no JSON serializer, filesystem, Godot, Newtonsoft, or legacy DTO dependencies; `Data/Database.cs` and `JRPG.csproj` remain unchanged.
- Runtime execution and legacy consumer migration remain deferred to later tracks.
- Completion commit: `data: add skill system catalog loader`.

## Track 7: Combat Vocabulary

### Purpose

Make the core combat vocabulary match the GDD before active effects depend on it.

### Current Files

- `Core/Enums.cs`
- `Core/ElementHelper.cs`
- `Entities/Persona.cs`
- `Entities/Components/DamageHandler.cs`
- `Logic/Battle/CombatMath.cs`
- `Logic/Battle/Engines/BattleKnowledge.cs`
- affinity displays in `Logic/Battle/Bridges/InteractionBridge.cs`

### Work

- Introduce the eight-element `DamageElement` vocabulary.
- Collapse legacy Slash, Strike, and Pierce damage into `physical` for new content.
- Separate `ElementalAffinity` from `ResistanceLevel`.
- Move instant-death checks away from Curse affinity into Light/Dark `ResistanceLevel` channels, with explicit bypass support for conditional mechanics such as Eternal Rest.
- Ensure Almighty bypasses authored affinity lookup.
- Update battle knowledge to record elemental affinities separately from ailment knowledge.
- Map legacy basic-attack weapon types into new `physical` damage during transition while retaining weapon-type metadata only where presentation or equipment rules need it.

### Compatibility Risk

Changing `Core.Element` directly will break broad legacy code. Prefer introducing the new type beside the old enum, migrating typed consumers, then deleting the old enum in Track 13.

### Exit Criteria

- New skill definitions cannot express removed elements.
- New entity definitions cannot author Almighty affinity.
- Instant death uses `ResistanceLevel`, not elemental affinity.
- Hama/Mudo channel selection and Eternal Rest bypass behavior are covered independently.
- Damage tests cover all six affinity outcomes and Almighty behavior.

### Completion Record

- Added an immutable `CombatDefenseProfile` with separate elemental, ailment, and instant-death defense maps and normal defaults for missing entries.
- Added clean elemental-affinity, ailment-resistance, and instant-death resolvers. Elemental resolution implements approved shield, Break, Almighty, and passive-precedence rules without importing legacy guard, rigid-body, or numeric multiplier behavior.
- Instant-death resolution is contract-only: Hama and Mudo select Light/Dark `ResistanceLevel` channels, while Eternal Rest's explicit no-channel check produces a bypass result. Chance modifiers remain deferred to Track 8.
- Added separate elemental-affinity, ailment-resistance, and instant-death knowledge stores with immutable snapshots. `BattleKnowledge` exposes them alongside its unchanged legacy registry.
- Added the explicit `LegacyCombatVocabularyAdapter`. Slash, Strike, and Pierce map to clean Physical; Elec maps to Electric; Earth, Mind, Nerve, Curse, and None have no clean damage-element mapping.
- Added an independent clean defense profile to `Persona` and a clean Physical basic-attack element to `Combatant`. Legacy affinity maps, weapon elements, damage math, executors, AI, datasets, and console displays remain unchanged.
- The legacy entity dataset was not converted: 51 of 304 records contain conflicting Slash, Strike, and Pierce affinities, so collapsing it would invent behavior.
- Focused Track 7 verification passed: 54 passed, 0 failed, 0 skipped.
- Full verification passed: 363 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore --nologo`.
- Rebuild verification completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- Completion commit: `combat: add typed combat vocabulary`.

## Track 8: Active Skill Execution

### Purpose

Replace category-based effect dispatch and string parsing with ordered typed effects.

### Current Files

- `Logic/Battle/ActionProcessor.cs`
- `Logic/Battle/Effects/BattleEffectRegistry.cs`
- files under `Logic/Battle/Effects/`
- `Logic/Battle/CombatMath.cs`
- `Logic/Battle/Engines/StatusRegistry.cs`
- `Logic/Battle/Results/`

### Proposed Runtime Types

```text
SkillExecutionRequest
ResolvedTargetSet
EffectExecutionContext
IEffectExecutor<TDefinition>
EffectExecutionResult
SkillExecutionResult
EffectExecutorRegistry
```

The executor should:

1. validate context, costs, and targeting,
2. resolve targets,
3. commit costs according to the chosen policy,
4. execute effects in order,
5. apply per-effect conditions and failure policy,
6. return structured results used by Press Turn and presentation.

### Migration Order

1. `damage`
2. `restore_resource`
3. `remove_ailment`
4. `revive`
5. `modify_stat_stage`
6. `apply_ailment`
7. remaining utility and resource effects
8. registered custom effects last

### Tests

- Unit test every executor independently.
- Add composition tests such as damage plus ailment and heal plus cure.
- Add result-order tests.
- Add partial-failure tests.
- Verify `continue`, `stop_target`, and `stop_action`, including skipped conditions and valid no-change effects.
- Verify Repel-style interruptions override authored failure policy.
- Preserve Press Turn tests for weak, critical, miss, null, repel, and absorb outcomes.

### Exit Criteria

- At least one battle scenario executes entirely from a clean `SkillDefinition`.
- No migrated skill examines display text or category strings.
- Multi-effect execution produces deterministic ordered results.
- Existing legacy battle actions continue working until migrated.

### Completion Record

- Added a clean `BattleActorState` independent from legacy `Combatant`, with typed resources and separate ailment, stat-stage, charge, shield, affinity-override, status, and analysis stores.
- Added `ISkillExecutor`, immutable execution requests/results, resolved target sets, per-effect contexts, typed diagnostics, Press Turn outcomes, and an exact-type `EffectExecutorRegistry`.
- Execution now performs atomic preflight, strict target resolution, single-pass cost resolution, cost commitment, per-target condition evaluation, and deterministic effect-then-target ordering.
- Implemented all 16 approved active effects. The default registry contains no display-name, description, menu-group, category-string, JSON, Godot, or legacy DTO dispatch.
- Added explicit host policy contracts for damage, instant death, ailments, chance, power/formula amounts, random targeting, escape eligibility, custom conditions, and custom effects. No legacy balance formula was adopted as a framework default.
- Added `continue`, `stop_target`, and `stop_action` behavior. False conditions skip; valid no-change effects succeed; Repel and Absorb interrupt regardless of authored failure policy.
- Temporary affinity overrides now participate in the Track 7 resolver below matching shields and Break, without mutating authored defense profiles.
- The legacy console `ActionProcessor`, `Combatant`, `CombatMath`, effect registry, datasets, and consumers remain behaviorally unchanged for later migration tracks.
- Focused Track 8 verification passed: 27 passed, 0 failed, 0 skipped.
- Full verification passed: 390 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore --nologo --verbosity quiet`.
- Rebuild verification completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- Completion commit: `combat: add typed active skill execution`.

## Track 9: Passive Skill System

### Purpose

Replace passive name checks with data-defined triggers and bounded rule modifiers.

### Current Name-Driven Areas

- `Logic/Battle/CombatMath.cs` for Boost, Amp, Driver, Magic Ability, Arms Master, and similar checks.
- `Logic/Battle/Engines/StatusRegistry.cs` for Auto-Kaja, Regenerate, Invigorate, and ailment recovery.
- `Logic/Battle/ActionProcessor.cs` for skill-cost passives.
- `Entities/Combatant.cs` and components for endure, counter, and state checks.

### Proposed Services

```text
PassiveTriggerDispatcher
RuleModifierResolver
RuleModifierRegistry
StackingPolicyRegistry
```

The dispatcher subscribes to typed gameplay events. The modifier resolver gathers applicable modifiers for one calculation and applies deterministic stacking rules.

Stacking policies are registered by modifier type in code. Content does not provide stacking groups or numeric priority. Numeric modifiers resolve as `(base + sum(add)) * product(multiply)`. Elemental-affinity resolution uses `absorb > repel > null > resist > normal > weak`, after which shields take priority and Break normalizes only when no shield applies. Ailment resistance is a dedicated replacement keyed by ailment ID and uses `immune > resistant > normal > vulnerable`.

### Initial Passive Coverage

1. Ice Boost-style `damage_dealt` modifier.
2. Regenerate-style `owner_turn_end` trigger.
3. Auto-Tarukaja-style `battle_start` trigger.
4. Arms Master-style `resource_cost` modifier.
5. Resist Poison-style `ailment_resistance` modifier.
6. Endure-style `owner_would_be_defeated` trigger.

### Tests

- Ice Boost affects only Ice damage.
- Ice Boost remains inheritance group `passive`.
- Multiple modifiers follow explicit stacking policy.
- Trigger order is deterministic.
- A passive cannot recursively trigger itself without an explicit allowance.
- Removing or disabling a passive stops its effects immediately.

### Exit Criteria

- New passive execution contains no skill-name comparisons.
- Triggered and continuous passives both use typed definitions.
- The reference Ice Boost fixture changes Ice damage in a focused test.

### Implemented Contract

- `BattlePassiveCollection` preserves loadout order, accepts passive definitions only, rejects duplicates, and applies enable, disable, add, and remove operations immediately.
- `PassiveTriggerDispatcher` processes passive, trigger, target, and effect order; reuses Track 8 effect execution; treats the owner as condition actor; and reports nested activation results without changing Press Turn aggregation.
- Event policies own recursion and activation limits. Same-trigger recursion is suppressed by default, and `owner_would_be_defeated` is limited to one activation per trigger per battle.
- `RuleModifierResolver`, `RuleModifierRegistry`, and `StackingPolicyRegistry` consume conditions through one shared calculation context. Cost contexts include every typed damage element on the skill.
- Ice Boost, Arms Master, Resist Poison, Regenerate, Auto-Tarukaja, affinity replacement, and one-use Endure behavior are integrated into the clean battle path.
- `ailment_resistance` is a dedicated schema/domain modifier with `ailmentId` and `ResistanceLevel`; its content reference is validated and catalog-qualified.
- The legacy console battle path remains unchanged. Ailment-owned triggers, passive duration expiration, basic-attack modifier consumption, and other unintegrated modifier consumers remain deferred.

### Completion Record

- Added the ordered actor-owned passive collection, shared condition calculation context, typed trigger dispatcher, event policies, modifier registry, and stacking-policy registry.
- Integrated Ice Boost, Arms Master, Resist Poison, Regenerate, Auto-Tarukaja, passive affinity replacements, and one-use Endure into the clean Track 7-8 battle path without skill-name comparisons.
- Replaced numeric `ailment_resistance` with the dedicated `{ type, ailmentId, resistance }` modifier throughout domain definitions, strict deserialization, validation, qualification, cross-pack checking, runtime resolution, and documentation.
- Nested passive activations are preserved in effect and skill execution results; Press Turn aggregation remains based on the original active effect outcomes.
- Focused Track 9 runtime verification passed: 12 passed, 0 failed, 0 skipped. The catalog qualification test for typed ailment resistance also passed.
- Full verification passed: 402 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore --nologo --verbosity quiet`.
- Rebuild verification completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- Completion commit: `battle: add passive triggers and rule modifiers`.

## Track 10: Fusion Inheritance

### Purpose

Implement the approved group-policy model and prove the fusion-fodder use case.

### Current Files

- `Logic/Fusion/Rules/`
- `Logic/Fusion/Strategies/`
- `Logic/Fusion/Planning/`
- `Logic/Fusion/Preview/`
- `Logic/Fusion/Transactions/`
- `Entities/Persona.cs` legacy `InheritanceType`

### Proposed Service

```text
FusionInheritanceEvaluator
  Evaluate(entityDefinition, skillDefinition) -> InheritanceDecision
```

`InheritanceDecision` should include an allowed flag and reason code:

```text
allowed
skill_not_inheritable
owner_exclusive
explicitly_blocked
explicitly_allowed
group_denied
group_not_allowed
```

### Precedence

1. Reject `isInheritable: false`.
2. Enforce owner exclusivity.
3. Reject explicit skill block.
4. Permit explicit skill allowance.
5. Evaluate the entity's group policy.

### Required Scenario

```text
Parent/fodder entity denies inheritance group `ice`.
Active Ice skill has inheritance group `ice` and is rejected.
Ice Boost has activation `passive` and inheritance group `passive` and is allowed.
The selected Ice Boost can be inherited by the child if the child's own policy permits passive skills.
```

### Exit Criteria

- Clean inheritance preview and final selection validation use the same evaluator.
- UI can explain why a skill is unavailable without reproducing rules.
- Tests cover deny-list, allow-list, explicit exceptions, exclusivity, and passive fusion fodder.
- The caller supplies the nonnegative inherited-skill selection limit; no slot formula is adopted in this track.
- Legacy `InheritanceType` is not consulted by the clean fusion path.
- The Cathedral planner, preview, transaction, datasets, and console UI remain unchanged for Track 11.

### Completion Record

- Completed on June 14, 2026 on branch `skill-system-redesign`, starting from commit `b6f2766` (`battle: add passive triggers and rule modifiers`).
- Added the typed `FusionInheritanceEvaluator` with the approved precedence and stable `allowed`, `skill_not_inheritable`, `owner_exclusive`, `explicitly_blocked`, `explicitly_allowed`, `group_denied`, and `group_not_allowed` reason codes.
- Added immutable inheritance requests, first-occurrence candidate planning, already-known availability, caller-owned nonnegative selection limits, aggregate selection diagnostics, and a validated-selection token that only successful validation can create.
- Final selection re-evaluates each selected candidate through the evaluator instance that created the preview plan. Duplicate, unknown, already-known, policy-ineligible, and over-limit selections cannot produce a validated result; zero slots and empty selection remain valid.
- Proved the two-generation fusion-fodder scenario: an Ice-denying recipient rejects active Ice while accepting passive-group Ice Boost, which a later child may inherit when its own policy permits passive skills.
- The clean path depends only on immutable definitions and `ContentId`. It does not inspect display text, descriptions, effect payloads, legacy inheritance strings, `Database`, `SkillData`, `PersonaData`, JSON serializers, Godot, or filesystem APIs.
- Focused Track 10 verification passed: 18 passed, 0 failed, 0 skipped.
- Full verification passed: 420 passed, 0 failed, 0 skipped using `dotnet test JRPG.sln --no-restore --no-build --nologo --verbosity quiet`.
- Rebuild verification completed with 0 errors and the repository's existing 122 nullable-reference and DTO-initialization warnings.
- `git diff --check` and clean-boundary searches passed. The Cathedral planner, preview, transaction, UI, legacy datasets, and `Entities/Persona.cs` remain unchanged for Track 11.
- Completion commit: `fusion: enforce inheritance group policies`.

## Track 11: Runtime Consumer Migration

### Purpose

Move all skill and entity consumers to the catalog before deleting compatibility code.

### Consumer Checklist

- `Logic/Battle/ActionProcessor.cs`
- `Logic/Battle/Engines/BehaviorEngine.cs`
- `Logic/Battle/Engines/StatusRegistry.cs`
- `Logic/Battle/Bridges/InteractionBridge.cs`
- `Entities/Components/DamageHandler.cs`
- `Entities/Components/GrowthProcessor.cs` where skill unlocks are applied
- entity/persona factories under `Entities/Components/`
- field skill usage under `Logic/Field/Bridges/InventoryUIBridge.cs`
- fusion planning and preview services
- analysis and battle-knowledge displays

### Migration Rule

Each consumer receives the narrowest repository or service interface it needs. It must not access static `Database.Skills` or parse legacy `SkillData` strings after migration.

### AI Considerations

`BehaviorEngine` should evaluate typed facts:

- menu group,
- effects,
- targeting,
- costs,
- known affinities and resistances.

It should not search category names or description text.

### Exit Criteria

- Repository search finds no migrated subsystem reading `Database.Skills` or `Database.Personas`.
- Runtime factories hydrate from `EntityDefinition`.
- Skill unlock lists preserve multiple skills at the same level.
- The console host can run a clean-schema battle scenario.

## Track 12: Shared Effects Beyond Skills

### Purpose

Prevent items and field actions from developing a second behavior language.

### Current Files

- `Data/ItemData.cs`
- `Logic/Field/Bridges/InventoryUIBridge.cs`
- `Logic/Field/Engines/FieldServiceEngine.cs`
- item handling in `Logic/Battle/ActionProcessor.cs`

### Work

- Represent usable items with targeting and ordered effects.
- Reuse `restore_resource`, `remove_ailment`, `revive`, and escape effects.
- Keep item consumption policy separate from effect execution.
- Let host-specific dungeon exit requests remain adapter outcomes until dungeon design is finalized.

### Exit Criteria

- Healing and cure logic is shared between skills and items.
- Item behavior is not selected by display name or legacy type strings.
- Field and battle contexts validate effect availability explicitly.

## Track 13: Legacy Removal

### Purpose

Delete obsolete code only after replacement consumers and tests exist.

### Removal Candidates

Generated and experimental migration artifacts:

- `Data/Jsons/skills_database_v2.json`
- `Data/Jsons/entity_database_v2.json`
- `migration_report.md`

The mapper, duplicate definitions, experimental schemas/catalogs, old sample files, and their tests were already removed in Track 3 because they were not used by the console runtime.

Legacy runtime artifacts, removed only after all consumers migrate:

- `Data/Jsons/skills_database.json`
- `Data/Jsons/entity_database.json`
- skill/entity portions of `Data/Database.cs`
- `Data/SkillData.cs`
- relevant portions of `Data/PersonaData.cs`
- `Core/ElementHelper.cs`
- the legacy `Element` and `Affinity` enums when no consumers remain.

### Removal Gate For Every File

Before deletion:

1. Use `rg` to prove there are no production references.
2. Verify tests do not preserve obsolete behavior accidentally.
3. Delete the file and compile immediately.
4. Run focused tests, then the full suite.
5. Record replacement ownership in documentation.

Do not delete legacy data merely because the new catalog can load fixtures. Delete it only when the actual console host and all required tests use the replacement path.

### Exit Criteria

- Only one skill/entity schema implementation remains.
- Only one clean catalog remains.
- No legacy mapper or generated v2 dataset remains.
- No runtime code parses skill names, descriptions, categories, or effect strings for behavior.
- Static `Database` no longer owns migrated content types.

## Track 14: Quality And Merge Gate

### Purpose

Prove that the branch is safer and clearer than `main` before merging.

### Automated Gates

- `dotnet build JRPG.sln --no-restore`
- `dotnet test JRPG.sln --no-restore`
- JSON parsing for every reference fixture.
- JSON Schema validation when schema files exist.
- `git diff --check`
- repository searches for banned legacy access and behavior inference.

Suggested search gate:

```text
Database.Skills
Database.Personas
SkillDefinitionMapper
InheritanceType
SpecialSkillPayload
FromCategory
Contains("Boost")
Contains("Amp")
Contains("Cure")
```

Each result must either be removed or documented as an intentionally unmigrated compatibility path. The final merge gate permits no remaining compatibility path for skills or entities.

### Behavioral Gates

- Physical and each magical element execute correctly.
- Almighty always receives a normal affinity result.
- Elemental affinity and ailment resistance do not affect each other.
- Recovery, cure, and revival compose correctly.
- Buff and debuff use positive and negative stat-stage changes.
- Damage-plus-ailment composition preserves effect order.
- Passive trigger and modifier behavior is deterministic.
- Ice-denying fusion fodder can inherit Ice Boost but not an active Ice skill.
- Fusion preview and committed fusion produce the same inheritance result.
- Display-name changes do not alter behavior.

### Documentation Gates

Update:

- `docs/architecture.md`
- `docs/gameplay-systems.md`
- `docs/subsystems/core.md`
- `docs/subsystems/data.md`
- `docs/subsystems/entities.md`
- `docs/subsystems/battle.md`
- `docs/subsystems/fusion.md`
- `docs/refactor-roadmap.md`
- `docs/content-schema-v1-proposal.md`, promoting approved portions from proposal to contract or replacing it with final schema documentation.

### Merge Criteria

The redesign branch may merge into `main` only when:

1. All track exit criteria are met.
2. The full test suite passes.
3. The console host can execute at least one battle and one fusion using clean content definitions.
4. No skill or entity runtime consumer depends on legacy datasets.
5. Obsolete migration files have been removed.
6. Documentation describes the implemented system rather than the transitional architecture.
7. The final diff has been reviewed for unrelated changes.

## Test Matrix

| Area | Unit tests | Integration tests | Contract tests |
| --- | --- | --- | --- |
| Deserialization | discriminator conversion | load reference documents | JSON shape and strict unknown fields |
| Validation | individual rules | complete graph validation | stable error codes and paths |
| Catalog | ID lookup and immutability | build from reference pack | duplicate and load-order behavior |
| Damage | math and affinity outcomes | execute skill in battle | eight-element vocabulary |
| Ailments | chance and resistance | apply and cure | resistance vocabulary |
| Recovery | amount calculations | heal, cure, revive composition | resource references |
| Buffs/debuffs | stage bounds | apply, remove, expire | modifier track references |
| Passives | trigger/modifier resolution | battle lifecycle events | passive inheritance invariant |
| Fusion | evaluator precedence | preview and commit parity | allow/deny group policy |
| Cleanup | not applicable | console smoke test | banned-reference searches |

## Risk Register

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Broad `Element` enum replacement breaks legacy systems | High | Introduce new vocabulary beside legacy types and migrate consumers before deletion. |
| A third schema implementation is accidentally created | High | Replace the test-only experimental schema in place and remove duplicate definitions early in Track 3. |
| Passive system becomes another string registry | High | Use closed modifier kinds, typed filters, and registered handlers only for exceptions. |
| Effect composition produces ambiguous partial success | High | Resolve failure policy and result ordering in Track 1, then test it in Track 8. |
| Fusion preview disagrees with committed fusion | High | Use one `FusionInheritanceEvaluator` in both paths. |
| Legacy data is deleted before runtime migration | High | Enforce the per-file removal gate in Track 13. |
| Reference fixtures become production content | Medium | Keep fixtures small, original, clearly named `.sample.json`, and covered as contract examples. |
| Godot concerns leak into framework definitions | Medium | Keep presentation and dungeon navigation outside this plan and catalog. |
| Custom effects become a miscellaneous escape hatch | Medium | Require registration, parameter validation, tests, and design review for each custom handler. |
| Long-running branch becomes difficult to merge | Medium | Keep focused commits, regularly merge or rebase from `main`, and preserve green tests. |

## Definition Of Done

The redesign is done when a developer can add a skill without modifying dispatch code for ordinary behavior, understand its execution and inheritance from the content record alone, and rely on validation to reject unsupported combinations.

The following example must be naturally expressible and fully tested:

```text
A fusion-fodder demon denies active Ice inheritance.
It can still learn and carry Ice Boost because Ice Boost is Passive.
Fusion can pass Ice Boost to a child that permits Passive inheritance.
Neither the modifier's Ice filter nor the skill's display name changes its inheritance group.
```
