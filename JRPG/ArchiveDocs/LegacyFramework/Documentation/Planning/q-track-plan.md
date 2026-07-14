# Track Q Original Content Policy And Legacy Dataset Boundary

> **Status: Active working plan.** Track Q no longer means "port the legacy datasets." The old production-data migration sequence is suspended because the retained legacy `Data/Jsons` content is prototype-only and not approved as commercial/shippable framework content.

## Purpose

Track Q now protects the framework from accidentally adopting non-shippable legacy content as clean catalog authority.

Q1 remains useful because it records what legacy data exists, how much of it exists, which clean schema families could represent equivalent original content later, and which consumers still depend on `Database`. Q2 amends the plan: those records are audit evidence only, not a backlog of content to port.

The clean framework remains validated by reference, demo, and test packs. Original game content should be authored when design requires it; it should not be derived from ATLUS-style prototype data.

## Current Boundary

- Legacy `Data/Jsons` files remain console-prototype data.
- Legacy `Data/Jsons` files are not commercial production content.
- The clean catalog may continue using reference/demo/test packs for framework verification.
- No legacy content family is approved for direct clean-catalog conversion.
- No gameplay consumer switches to clean production content as part of Q2.
- No removal of legacy files is authorized by Q2.

## Policy Rules

Every future content track must obey these rules:

- Treat the Q1 ledger as audit evidence, not a conversion queue.
- Do not port legacy records directly into clean content packs.
- Author original records for any shippable content pack.
- Preserve framework schemas, validators, and runtime consumers as reusable infrastructure.
- Keep proprietary or franchise-derived names, descriptions, structures, and authored records out of original content packs.
- Keep old v2 migration artifacts as historical evidence only.
- Use legacy datasets only for prototype characterization until a later removal decision replaces the console prototype content.
- Require explicit approval before any original content pack becomes gameplay authority.

## Suspended Migration Sequence

The Q1 plan previously listed Q2-Q7 as family-by-family production conversion. That sequence is suspended.

The suspended sequence remains useful as a map of systems that would need original content later:

- skills and shared effects;
- entities, races, affinities, ailments, and inheritance;
- items, equipment, shops, and hospital content;
- negotiation, rewards, compendium, and fusion recipes;
- dungeons, encounters, and rulesets;
- eventual per-consumer catalog authority switches.

None of those categories should be filled by copying or mechanically transforming the current legacy data.

## Original Content Path

When original content is needed, create it through a new original-content track:

1. Define the design goal for the pack.
2. Author records directly against the clean schema.
3. Keep names, descriptions, effects, identities, and encounter structures original.
4. Validate through the existing strict schema, deserializer, validator, and catalog loader.
5. Add runtime coverage for the consumer that will use it.
6. Switch only that consumer, only after the original pack passes its gate.

The first original content pack is not required by Q2. Current reference/demo/test packs already prove the clean framework contracts.

## Q1: Audit Ledger And Conversion Rules

Status: Complete.

Q1 added:

- this planning file;
- `Convergence.Tests/Fixtures/ProductionContent/production-content-ledger.json`;
- tests proving every protected legacy content family and clean catalog family was represented;
- documentation explaining that Q1 performed no production conversion and no consumer switch.

Q1 also recorded known legacy integrity findings:

- 56 unresolved base-skill references;
- 120 unresolved learned-skill references;
- 1 casing-only skill reference;
- 1 unresolved dungeon enemy-pool reference;
- 0 unresolved dungeon boss references;
- 0 unresolved shop references;
- 0 invalid fusion operands.

## Q2: Legacy Content Boundary Amendment

Status: Complete.

Goal:

- mark legacy datasets as prototype-only and not commercially approved;
- mark direct conversion from legacy data to clean production packs as paused;
- require original replacement content before any clean catalog authority switch;
- keep Q1 counts and findings as audit evidence only;
- keep `Data/Jsons` unchanged.

Acceptance:

- the production-content ledger records the prototype-only policy at root and family level;
- ledger tests fail if any legacy family is marked shippable, converted, consumer-switched, clean authority, or removal-authorized;
- docs no longer describe Q2 as skill conversion or Track Q as mandatory legacy data migration;
- full tests, build, demos, whitespace checks, forbidden-reference search, and `Data/Jsons` preservation checks pass.

## Verification Gate

Every Track Q amendment or original-content track must run:

- focused tests for the changed ledger/docs or content family;
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

### Q2 Completion

Q2 completed on `track-12-recovery`.

- Updated the production-content ledger with a root content policy and per-family legacy content policies.
- Marked legacy-backed families as `prototype_only_legacy_authority`; the ruleset family remains `original_policy_binding_required`.
- Replaced old Q2-Q7 conversion ownership with `future_original_content`, keeping Q1 counts and integrity findings as audit evidence.
- Updated ledger tests so any legacy family marked shippable, converted, consumer-switched, clean-authoritative, or removal-authorized fails the test suite.
- Updated architecture, gameplay, production baseline, and parity-plan docs to state that direct legacy conversion is paused and future clean authority requires original content.
- Focused Q2 checks passed: `ProductionContentLedgerTests`, `RecoveryParityLedgerTests`, and `RecoveryDatasetBaselineTests` reported 8 passed, 0 failed, 0 skipped.
- Full verification passed: `dotnet test JRPG.sln --no-restore` reported 713 passed, 0 failed, 0 skipped; the nonincremental solution build passed with 98 warnings and 0 errors.
- Demo verification passed: `dotnet run --no-build -- --clean-battle-demo` ended in player-team victory, and `dotnet run --no-build -- --clean-field-demo` completed successfully.
- Quality gates passed: `git diff --check` reported no whitespace errors, the framework forbidden-reference search found no Godot/console/filesystem/Newtonsoft/legacy DTO/static database leaks, and `Data/Jsons` had no modified files.
- No production content was converted, no consumer switched to clean production content, no framework public API changed, and no legacy removal was authorized.
