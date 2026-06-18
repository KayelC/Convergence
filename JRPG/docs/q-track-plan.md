# Track Q Production Content Reauthoring Plan

> **Status: Active working plan.** Track Q is too large and too decision-heavy to complete as one commit. This file breaks it into auditable passes so production data can move to the clean catalog without silent drops, inferred behavior, or accidental legacy removal.

## Purpose

Track Q moves retained production content from the legacy `Data/Jsons` files into the approved clean catalog schemas only after the matching framework consumers exist.

This is not a bulk conversion script track. The legacy data contains conflicts, missing references, duplicate display names, support-only skills, and mechanics that were historically inferred from prose or C# branches. Track Q must make those decisions explicit before any real gameplay consumer becomes clean-catalog authoritative.

## Q1 Baseline

Q1 starts from the Track P baseline on `track-12-recovery`.

Q1 adds:

- this plan;
- `Convergence.Tests/Fixtures/ProductionContent/production-content-ledger.json`;
- tests that prove every protected legacy content family and clean catalog family is represented;
- documentation updates explaining that Q1 performs no production conversion and no consumer switch.

Q1 deliberately does not:

- edit production `Data/Jsons` content;
- convert records into new production packs;
- switch gameplay consumers away from `Database`;
- mark any capability as `clean_parity`;
- authorize removal of legacy files, DTOs, mappers, or compatibility adapters.

## Migration Rules

Every Q subtrack must obey these rules:

- Freeze the legacy record count and identifiers before converting a family.
- Classify every record as retain, split, replace, defer, or intentionally omit.
- Resolve ambiguous mechanics manually; do not infer behavior from names, descriptions, prose, or old effect strings.
- Preserve every mappable record.
- Omit only genuinely unmappable records, and report them with reasons.
- Keep old migration artifacts as evidence only; they are not authoritative Track Q output.
- Validate converted data structurally and semantically through the clean content pipeline.
- Compare reference graphs and record unresolved references.
- Run representative runtime coverage using converted records before switching a consumer.
- Keep the legacy source file until the matching consumer is clean-catalog authoritative and the removal gate is explicitly approved.

## Mandatory Reports

Every converted family must produce these reports:

- `record_counts`: legacy counts, converted counts, retained counts, omitted counts, and split/replacement counts.
- `id_mapping`: source identifiers, clean content IDs, collision handling, and qualified IDs after catalog loading.
- `omitted_records`: every omitted record and the reason it was not converted.
- `unresolved_references`: missing, ambiguous, deferred, and external references.
- `behavior_decisions`: manual decisions for mechanics that cannot be derived safely.
- `conflicts`: data conflicts, duplicate display names, incompatible affinities, or incompatible schema targets.
- `runtime_coverage`: tests or demos proving converted records drive the migrated consumer.

## Special Decision Buckets

Track Q has four known decision buckets that must remain explicit:

- `physical_affinity_conflicts`: legacy Slash, Strike, and Pierce values cannot be blindly collapsed into Physical for entity defense authoring.
- `navigator_support_skills`: Oracle, navigator, and support-only abilities need a support-system contract instead of normal demon-stock active skills.
- `special_registered_handlers`: special skills need typed effects or registered custom handlers before conversion.
- `demo_vs_production_content`: franchise-inspired demo packs are examples; production packs must be separated from framework-required fixtures.

## Subtracks

### Q1: Audit Ledger And Conversion Rules

Status: In progress.

Goal:

- create the Track Q control plane;
- record all production families, legacy owners, clean schema targets, future subtracks, report obligations, manual decisions, and removal gates;
- keep production JSON untouched.

Acceptance:

- ledger tests cover every protected legacy content file and every clean catalog family;
- old v2 migration files and archived `migration_report.md` are marked historical only;
- no family is marked converted, consumer-switched, removable, or `clean_parity`;
- the recovery dataset counts and known integrity findings remain recorded.

### Q2: Production Skills And Shared Effects

Goal:

- convert production skill records into clean skill definitions and shared effect definitions;
- separate active skills, passive skills, host-special support actions, and deferred unsupported records.

Primary inputs:

- `Data/Jsons/skills_database.json`;
- historical evidence from `Data/Jsons/skills_database_v2.json`;
- historical evidence from `ArchiveDocs/Planning/migration_report.md`.

Key decisions:

- duplicate display names such as Feral Claw, Life Aid, and Trafuri;
- support-only and navigator-style skills;
- special effects that need registered handlers;
- explicit costs, targeting, availability, effects, passives, modifiers, and inheritance groups.

Consumer switch:

- no switch until battle and field action consumers can execute the converted production skill pack without legacy `SkillData` effect-string inference.

### Q3: Entities, Races, Affinities, Ailments, And Inheritance

Goal:

- convert production entities, races, defenses, base skills, unlocks, ailment definitions, and inheritance policies.

Primary inputs:

- `Data/Jsons/entity_database.json`;
- `Data/Jsons/status_ailments.json`;
- historical evidence from `Data/Jsons/entity_database_v2.json`.

Key decisions:

- manual resolution of physical affinity conflicts;
- unresolved base-skill and learned-skill references;
- race extraction and negotiation defaults;
- elemental affinities, ailment resistances, and instant-death resistances as separate channels;
- inheritance allow/deny rules without using old inheritance-family shortcuts.

Consumer switch:

- no switch until factories, battle actor hydration, dungeon encounter hydration, fusion, negotiation, and compendium consumers can use catalog entities.

### Q4: Items, Equipment, Shops, And Hospital Content

Goal:

- convert consumables, valuables, equipment, shop offers, prices, usage effects, and hospital-policy references into clean content where schemas exist.

Primary inputs:

- `Data/Jsons/items.json`;
- `Data/Jsons/weapons.json`;
- `Data/Jsons/armor.json`;
- `Data/Jsons/boots.json`;
- `Data/Jsons/accessories.json`;
- `Data/Jsons/shop_inventory.json`.

Key decisions:

- equipment ID ownership remains unique unless a future policy introduces per-copy instances;
- shop metadata repair must become explicit data or explicit compatibility behavior;
- field item effects must stop relying on legacy prose parsing before consumer switch.

Consumer switch:

- no switch until inventory, equipment, shop, hospital, field item, and battle item paths all use catalog-backed definitions.

### Q5: Negotiation, Rewards, Compendium, And Fusion Recipes

Goal:

- convert negotiation personalities, questions, familiar dialogue, demands, reward policy references, fusion recipes, and Compendium content hooks.

Primary inputs:

- `Data/Jsons/questions.json`;
- `Data/Jsons/fusion_table.json`;
- entity and skill outputs from Q2 and Q3.

Key decisions:

- demands and familiar gifts as typed effects or registered host actions;
- fusion recipe operands and operation hooks;
- accident, mutation, rank, and Mitama policy IDs;
- recall pricing and reward formulas as named policy references, not hidden constants.

Consumer switch:

- no switch until negotiation, recruitment, reward payout, Cathedral fusion, mutation, and Compendium recall can run against catalog data.

### Q6: Dungeons, Encounters, Rulesets, And Runtime Coverage

Goal:

- convert dungeon structure, encounter pools, boss floors, fixed floor events, transition rules, and ruleset policy references.

Primary inputs:

- `Data/Jsons/tartarus.json`;
- converted entity outputs from Q3;
- converted ruleset policy decisions from Q2-Q5.

Key decisions:

- encounter IDs and weighted formations;
- empty-pool fallbacks;
- boss and barrier transition policy IDs;
- ruleset records that bind named code policies without pretending all constants are authored JSON.

Consumer switch:

- no switch until field dungeon traversal and encounter hydration can use the converted dungeon and encounter pack.

### Q7: Catalog Authority Switch And Final Omission Reports

Goal:

- switch one production consumer family at a time from legacy `Database` content to validated clean catalog content;
- produce the final Track Q omission and compatibility reports.

Acceptance:

- all retained production content loads through the clean catalog;
- every switched consumer has representative runtime tests;
- legacy source files remain present until a later removal gate;
- no silent drops, inferred defaults, or unreported compatibility fallbacks remain.

## Verification Gate

Every Q subtrack must run:

- focused tests for the changed family or ledger;
- `dotnet test JRPG.sln --no-restore`;
- `dotnet build JRPG.sln --no-restore --no-incremental /clp:Summary`;
- `dotnet run --no-build -- --clean-battle-demo`;
- `dotnet run --no-build -- --clean-field-demo`;
- `git diff --check`;
- framework forbidden-reference searches;
- `git status --short -- Data\Jsons`.

## Completion Log

### Q1 Completion

Q1 completed on `track-12-recovery`.

- Added the production content ledger with 10 tracked production families, 12 protected legacy content files, 12 clean schema families, 7 mandatory report types, 4 manual-decision buckets, and 3 historical-only migration artifacts.
- Recorded known unresolved-reference findings: 56 unresolved base-skill references, 120 unresolved learned-skill references, 1 casing-only skill reference, 1 unresolved dungeon enemy-pool reference, 0 unresolved dungeon boss references, 0 unresolved shop references, and 0 invalid fusion operands.
- Focused Q1 verification passed: `ProductionContentLedgerTests`, `RecoveryParityLedgerTests`, and `RecoveryDatasetBaselineTests` reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 713 passed, 0 failed, 0 skipped.
- Nonincremental build passed: `dotnet build JRPG.sln --no-restore --no-incremental /clp:Summary` reported 98 warnings and 0 errors.
- Demo verification passed: the clean battle demo ended in player-team victory, and the clean field demo completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no Godot/console/filesystem/Newtonsoft/legacy DTO/static database leaks, and `Data/Jsons` had no modified files.
- No production content was converted, no consumer switched to clean production content, and no legacy removal was authorized.
