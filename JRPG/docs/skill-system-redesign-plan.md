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

The current repository has three parallel representations:

| Representation | Current role | Key files |
| --- | --- | --- |
| Legacy runtime data | Actual source used by the console prototype | `Data/Jsons/skills_database.json`, `Data/Jsons/entity_database.json`, `Data/Database.cs`, `Data/SkillData.cs`, `Data/PersonaData.cs` |
| Legacy typed adapter | Maps string-driven runtime DTOs into experimental typed definitions | `Data/SkillDefinitionMapper.cs`, `Data/GameDataCatalog.cs`, `Data/DataValidation.cs` |
| Experimental clean schema | Test-only, not connected to runtime | `Data/Definitions/`, `Data/Schemas/`, `Data/Catalogs/GameDataCatalog.cs`, `Convergence.Tests/CleanDataMigrationTests.cs` |

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
- Track 0 changes documentation only. No runtime or gameplay code was modified.
- The redesign fixture JSON and `SkillSystemRedesignFixtureTests.cs` remain uncommitted for the dedicated reference-fixture track.

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

### Schema Questions That Must Be Closed

1. Whether `inheritanceGroupId` is a top-level skill field or nested under an `inheritance` object.
2. Whether `allowedSkillIds` overrides a denied group. Recommended answer: yes, as an explicit exception after owner/exclusivity checks.
3. Whether active skills may omit `menuGroup`. Recommended answer: no.
4. Whether passive skills must omit `menuGroup`. Recommended answer: yes.
5. How effect failure is represented when a multi-effect skill partially succeeds.
6. Whether effect conditions are evaluated once per action, once per target, or explicitly declare their scope.
7. How modifier stacking keys and priorities are declared.
8. Whether instant death has one resistance channel or Light/Dark-specific channels. Recommended v1 answer: one `instant_death` channel.
9. Whether affinity-changing passives replace or layer over base affinities. Recommended answer: deterministic priority with one effective result.

### Files

- Revise `docs/content-schema-v1-proposal.md`.
- Keep `docs/skill-system-gdd.md` as the normative behavior document.
- Add the eventual JSON Schema files under `Data/Schemas/SkillSystem/` rather than reusing ambiguous legacy filenames.

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
- All JSON examples parse.
- The open schema questions above have explicit answers.

## Track 2: Reference Fixtures

### Purpose

Provide tiny original datasets that become executable examples of the target contract. These fixtures are not migrated legacy content and must remain easy to inspect in code review.

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

`Convergence.Tests/CleanDataMigrationTests.cs` should be replaced or renamed because the work is no longer a data migration. Recommended replacement:

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
  SkillInheritanceDefinition Inheritance
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
ConditionEvaluationScope
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

## Track 4: Schema DTOs And Deserialization

### Purpose

Deserialize JSON into structural DTOs without executing rules or inferring behavior from text.

### Current Code To Replace

- `Data/Schemas/SkillDataSchema.cs`
- `Data/Schemas/EntityDataSchema.cs`
- `Data/Schemas/SchemaValidationResult.cs` may be retained and expanded if its shape remains useful.

### Proposed Location

```text
Data/SkillSystem/Schemas/
  SkillDocumentDto.cs
  SkillDto.cs
  EffectDtos.cs
  PassiveDtos.cs
  EntityDocumentDto.cs
  EntityDto.cs
  RaceDocumentDto.cs
  AilmentDocumentDto.cs
```

### JSON Technology

The project currently uses Newtonsoft.Json. Either continue with it during the redesign or adopt `System.Text.Json` deliberately in one isolated decision. Do not mix serializers across content types without a documented reason.

If Newtonsoft.Json remains:

- use a custom converter for effect and condition discriminators,
- reject unknown `type` values,
- preserve JSON paths for diagnostics,
- configure missing-member handling for strict content validation.

### Mapping Rules

- DTO-to-definition mapping may normalize casing and IDs.
- Mapping must not inspect `displayName` or `description` to choose behavior.
- Mapping must not derive passive inheritance from modifier filters.
- Mapping must reject an active skill with triggers/modifiers and a passive skill with active targeting/effects unless the final contract explicitly permits that shape.

### Exit Criteria

- All reference fixtures deserialize.
- Unknown discriminators fail with actionable errors.
- Display text changes do not alter the resulting definition.
- No legacy parser is called during clean-schema deserialization.

## Track 5: Validation And Diagnostics

### Purpose

Reject invalid content before any runtime service can obtain a catalog.

### Validation Layers

1. Structural validation: required fields, enums, ranges, mutually exclusive shapes.
2. Document validation: duplicate IDs and local consistency.
3. Cross-reference validation: skills, entities, races, ailments, groups, resources, and registered handlers.
4. Runtime capability validation: every effect, condition, trigger, and modifier has an implementation.

### Required Rules

- IDs are normalized and case-insensitively unique.
- Active skills require a menu group and active effect list.
- Passive skills use inheritance group `passive`.
- Passive skills define at least one trigger or modifier.
- Almighty is rejected from entity affinity maps.
- Entity affinity maps accept only the seven resistible damage elements.
- Ailment resistance maps cannot use `repel` or `absorb`.
- Effect chances and accuracy values stay within defined ranges.
- Costs cannot reduce the actor below allowed bounds.
- Entity skill references resolve.
- Entity allow and deny skill lists cannot contain the same ID.
- Fusion inheritance policies refer only to declared inheritance groups.
- Custom handler IDs must be registered with parameter validators.

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

Proposed location:

```text
Data/SkillSystem/Validation/
  ContentValidationError.cs
  SkillDocumentValidator.cs
  EntityDocumentValidator.cs
  ContentGraphValidator.cs
  RuntimeRegistrationValidator.cs
```

### Exit Criteria

- Invalid fixtures cover each required rule.
- Validation reports all independent errors in one pass where safe.
- The catalog cannot be created from invalid content.
- Tests assert error codes and paths, not only message fragments.

## Track 6: Catalog And Loader

### Purpose

Expose validated immutable content through one repository surface and remove ambiguity between the two current `GameDataCatalog` classes.

### Current Code To Consolidate

- `Data/GameDataCatalog.cs`
- `Data/Catalogs/GameDataCatalog.cs`
- `Data/Database.cs`

### Proposed Code Shape

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

Use a `ContentPackLoader` or `GameDataCatalogBuilder` to perform loading and validation. The catalog constructor should not read files.

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
- Loader tests prove deterministic file ordering and duplicate detection.

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
- Move instant-death checks away from Curse affinity.
- Ensure Almighty bypasses authored affinity lookup.
- Update battle knowledge to record elemental affinities separately from ailment knowledge.
- Decide how legacy basic-attack weapon types map into new `physical` damage during transition.

### Compatibility Risk

Changing `Core.Element` directly will break broad legacy code. Prefer introducing the new type beside the old enum, migrating typed consumers, then deleting the old enum in Track 13.

### Exit Criteria

- New skill definitions cannot express removed elements.
- New entity definitions cannot author Almighty affinity.
- Instant death uses `ResistanceLevel`, not elemental affinity.
- Damage tests cover all six affinity outcomes and Almighty behavior.

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
- Preserve Press Turn tests for weak, critical, miss, null, repel, and absorb outcomes.

### Exit Criteria

- At least one battle scenario executes entirely from a clean `SkillDefinition`.
- No migrated skill examines display text or category strings.
- Multi-effect execution produces deterministic ordered results.
- Existing legacy battle actions continue working until migrated.

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

- Fusion preview and transaction use the same evaluator.
- UI can explain why a skill is unavailable without reproducing rules.
- Tests cover deny-list, allow-list, explicit exceptions, exclusivity, and passive fusion fodder.
- Legacy `InheritanceType` is no longer consulted by migrated fusion paths.

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
- `Data/SkillDefinitionMapper.cs`
- `Convergence.Tests/SkillDefinitionMapperTests.cs`
- superseded files under `Data/Definitions/` and `Data/Schemas/`
- duplicate `Data/SkillDefinitions.cs`
- obsolete `Data/GameDataCatalog.cs`
- outdated `Convergence.Tests/CleanDataMigrationTests.cs`

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
