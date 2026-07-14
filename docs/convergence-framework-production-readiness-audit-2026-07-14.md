# Convergence Framework Production Readiness Audit

**Date:** 2026-07-14  
**Reviewed product:** `Convergence.Framework`, `Convergence.DemoHost`, clean tests, clean content, and the repository entry point  
**Authority used:** current source code, project files, tests, content, build output, and executable behavior

This is a fresh audit. Earlier reviews, migration ledgers, completion summaries, and roadmap status labels were not used as evidence for the findings below. Active documentation was checked only after the code findings, to identify release-facing contradictions and broken links.

## Executive Verdict

`Convergence.Framework` is a substantial and generally healthy pre-release rules framework. It is not a hollow facade around the old console game. The active library independently implements content loading and validation, catalogs, actor state, typed effects, battle lifecycle, turn economy, progression, party and stock transitions, economy, navigation, dungeon traversal, encounters, negotiation, rewards, fusion, Compendium behavior, battle knowledge, and save snapshots.

It is **not ready to call finished or production-stable yet**.

The main need is no longer more gameplay breadth. The next work is to make the implemented scope safe and supportable as a public product:

1. repair the repository's public entry point and licensing story;
2. fix a small number of real mutation and orchestration defects;
3. stabilize and document the very large public API and JSON authoring contract;
4. complete the data-driven policy and persistence composition boundaries;
5. prove the primary host with a real Godot project;
6. establish repeatable CI, compatibility, AOT, and performance gates.

The right description today is **advanced alpha / pre-release candidate**, not prototype and not finished v1.

## What Was Inspected

- 84 active Framework C# files, approximately 29,733 lines.
- 23 DemoHost C# files, approximately 9,953 lines.
- 34 Framework test files and 9 DemoHost test files.
- 36 active clean content documents.
- Project dependency direction, public API boundaries, content deserialization, validation, catalog loading, runtime mutation, battle execution, lifecycle, turn economy, persistence, fusion, Compendium, knowledge, host contracts, and sample integration.
- Repository-root solution, README, license, archive layout, and build entry points.
- Release build, all tests, coverage, every noninteractive DemoHost command, local documentation links, forbidden-reference checks, and Git whitespace/state checks.

## Implemented Framework Surface

The following is present in current code, independent of the archived legacy prototype:

- **Content:** strict source-generated `System.Text.Json` deserialization for manifests, skills, entities, races, ailments, items, equipment, shops, negotiation, encounters, dungeons, fusion recipes, and rulesets. Unknown fields, comments, trailing commas, malformed discriminators, and unsupported shapes are rejected in [`SkillSystemJsonDeserializer.cs`](../src/Convergence.Framework/Content/SkillSystemJsonDeserializer.cs#L10).
- **Validation and catalog:** aggregated semantic diagnostics, explicit host registrations, dependency validation, cross-pack reference resolution, canonical qualification, and immutable repositories.
- **Runtime state:** immutable snapshots plus mutable actor state with identity, progression, resources, stats, skills, forms, equipment, status, passive activations, capabilities, and typed restore integrity checks.
- **Execution:** typed conditions, targeting, costs, effects, items, passives, rule modifiers, custom handlers, host requests, action assessments, one-use assessment tokens, and staged actor transactions.
- **Battle:** defense resolution, combat rules, ailments, passives, lifecycle, Press Turn and generic turn-economy contracts, manual action execution, automated action selection, encounter orchestration, negotiation, recruitment decisions, rewards, and typed knowledge stores.
- **World runtime:** optional generic navigation, optional dungeon traversal, encounter preparation, party/stock transitions, inventory, equipment, wallet, shops, restoration, growth, and ruleset binding.
- **Fusion and Compendium:** inheritance policy evaluation, planning, preview validation, recipe/result policies, transactions, acquisition registration, recall, pricing policy injection, and familiar-knowledge import.
- **Persistence:** contract-versioned aggregate snapshots, extensive validation, manual/suspend save policy, host context, checkpoints, content-pack identity validation, and actor restoration.
- **Hosting:** serializer-neutral content, command, event, and random-source contracts. Framework source contains no console, filesystem, Godot, Newtonsoft, or archived-runtime dependency.

## Findings

### Blocker 1: The Git Repository Still Opens As The Retired Product - Resolved

The original audit found the clean product under `JRPG/`, while the Git root
contained a stale solution that referenced the deleted `JRPG/JRPG.csproj` and a
README for the retired prototype. That layout caused the root build to fail with
`MSB3202` and presented the wrong product to developers.

The clean [`Convergence.sln`](../Convergence.sln), [`README.md`](../README.md),
`global.json`, source, samples, tests, content, and active documentation now live
directly at the Git root. The superseded solution, README, `Documentation/`, and
`Old Files Archive/` are preserved under
[`ArchiveDocs/LegacyRepository`](../ArchiveDocs/LegacyRepository/README.md) and
are excluded from the active build.

**Resolution:** the repository has one obvious clone-build-test path:
`dotnet build Convergence.sln` and `dotnet test Convergence.sln`. A product
boundary regression test verifies that the active product root equals the Git
root when Git metadata is present and that all solution projects resolve there.

### Blocker 2: The Current License Conflicts With The Intended Developer Audience

The repository does have a license: [`LICENSE.md`](../LICENSE.md). It is Creative Commons BY-NC-SA 4.0. Its `NonCommercial` restriction prevents commercial use, and its `ShareAlike` terms impose downstream conditions. That conflicts with the stated goal of a reusable framework that independent Godot developers can integrate into their games.

This is not a missing-file problem; it is a product decision problem. The framework cannot honestly be presented as generally usable open-source game middleware until the intended software license is chosen and applied clearly to code, sample content, and documentation.

**Required action:** choose a software license compatible with the actual distribution goal, record copyright ownership, distinguish code and sample-content licensing if necessary, and put that decision at the repository root before release.

### Blocker 3: The Public Contract Is Too Large And Uncontrolled For A Stable Release

The source currently contains 781 public type declarations but only 45 XML-documentation lines. [`Convergence.Framework.csproj`](../src/Convergence.Framework/Convergence.Framework.csproj#L3) enables nullable analysis but has no explicit pre-release version, XML documentation output, analyzer policy, warnings-as-errors policy, or public API compatibility baseline.

Source distribution does not remove compatibility concerns. A developer still writes code against these names and constructors. Without an intentional supported surface, every internal cleanup becomes a breaking change.

The JSON side has the same problem. DTOs accept `$schema`, but there are zero active machine-readable schema artifacts and the `$schema` value is not mapped into the domain or validated. Structural truth currently exists only in converters and tests.

**Required action:** classify public types as stable, experimental, or internal; reduce accidental public surface; add API compatibility baselines; generate XML API documentation; publish authored JSON Schemas for every supported document; and add a standalone authoring-validation command or tool.

### Blocker 4: Godot Compatibility Is Proven By A Fake, Not By Godot

[`GodotIntegrationContractTests.cs`](../tests/Convergence.Framework.Tests/Hosting/GodotIntegrationContractTests.cs#L58) is useful: it proves host-supplied resource text, signal-shaped commands, event sinks, scene-handle mapping, and host-owned saves can use existing contracts. However, all Godot-shaped adapters are private test classes using strings and in-memory collections; no Godot assembly or Godot project compiles the framework.

This proves architectural neutrality, not real host compatibility. It cannot reveal Godot project-reference behavior, export/AOT issues, engine lifecycle integration, main-thread marshaling mistakes, or platform-specific build failures.

**Required action:** add a separate minimal Godot 4 C# sample outside Framework. It should project-reference Framework, load one clean pack from `res://`, map one scene actor by `RuntimeInstanceId`, execute one action/encounter, and round-trip one host-owned save. Build it in the release gate without allowing Godot types into Framework.

### Blocker 5: There Is No Repeatable Repository-Level Release Gate

No active CI workflow, `Directory.Build.props`, `.editorconfig`, API compatibility check, analyzer configuration, benchmark project, trimming/AOT smoke test, or security policy exists at the Git root. The current Release build is clean, but that quality depends on a person remembering the right nested solution and commands.

**Required action:** establish CI on the supported .NET 8 baseline for restore, build, tests, coverage thresholds, content validation, DemoHost smoke runs, forbidden dependencies, API compatibility, documentation links, and the real Godot sample. Make warnings fail the build after analyzer policy is selected.

### High 1: Encounter Cancellation Or Event Publication Can Split One Turn's Commit

The encounter runner calls the host turn handler at [`BattleEncounterRunner.cs:708`](../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L708). That handler may execute and commit actor changes. The runner then publishes returned events, checks cancellation, applies turn economy, runs turn-end lifecycle, and publishes more events at lines 720-815.

If cancellation is requested, or an event sink throws, after the action commits but before turn economy and lifecycle complete, the actor mutation remains live while `RunAsync` exits without a coherent encounter result. Existing tests cover pre-cancellation and cancellation during a non-mutating handler, but not cancellation or sink failure after a committed action.

**Required action:** define the atomic boundary explicitly. Prefer staging the command's actor changes with turn economy and turn-end lifecycle, then committing before publication. At minimum, make cancellation effective only at documented safe points and convert presentation-sink failures into typed host faults without corrupting authoritative encounter state.

### High 2: The Core Party/Stock API Still Hardcodes One Franchise-Shaped Model

The framework is serializer- and host-neutral, but this part of its public domain language is not yet game-neutral. `RuntimePartyStockSnapshot` always contains `ActiveForm`, `PersonaStock`, and `DemonStock` in [`PartyStockTransitions.cs`](../src/Convergence.Framework/Runtime/PartyStockTransitions.cs#L52). Public commands include `PersonaSwapBattleActionCommand` in [`BattleActionExecutor.cs`](../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L202), and persistence/fusion/Compendium APIs repeat those roles.

These collections may be left empty, so they do not force behavior at runtime. However, they do force every developer to accept a particular ownership vocabulary and aggregate shape. That conflicts with the intended modular framework model, where a game may have parties without equippable forms, summons without a Persona-like stock, or entirely different owned-actor roles.

The supposedly neutral Training Annex sample also uses `sample_macca` and prints `Macca` in [`training_annex_slice.negotiations.json`](../content/original/training-annex/training_annex_slice.negotiations.json#L50) and [`CleanTrainingAnnexDemoHost.cs`](../samples/Convergence.DemoHost/Hosts/CleanTrainingAnnexDemoHost.cs#L272).

**Required action:** resolve this before public API freeze. Either split party, active-form, and summon-stock state into optional modules, or generalize owned-actor collections around registered role IDs and policies. Rename supplied sample terminology to neutral terms such as form, summon, and credits. Specialized game terminology can remain host-owned or live in an optional compatibility/example module.

### Medium 1: Resource Addition Can Escape Its Typed Diagnostic Boundary

[`RuntimeResourceTransactionService.AddResource`](../src/Convergence.Framework/Runtime/RuntimeStateSnapshots.cs#L569) calculates `resource.Current + delta` directly. `decimal.MaxValue + 1` throws before `SetResource` can return `ResourceValueOutOfRange`. Other combat arithmetic already uses saturating or checked helpers.

**Required action:** use checked/saturating arithmetic consistently and return a stable overflow/range diagnostic with unchanged state. Add maximum-value regression tests.

### Medium 2: Stat Allocation Can Produce An Invalid Snapshot Or Throw Near The Numeric Ceiling

[`StatAllocationService.Allocate`](../src/Convergence.Framework/Runtime/ProgressionPolicies.cs#L790) checks the caller-supplied stat cap, then directly increments base and effective values at lines 814-817. `RuntimeStatBlockSnapshot` itself is a permissive transport snapshot. A value just below `int.MaxValue` can pass the cap check, exceed `RuntimeActorNumericDomain.MaximumStatValue`, and either survive for unrelated stats or throw during resource recalculation for Magic/Vitality.

**Required action:** validate the request's IDs, cap, input numeric domain, and calculated output before constructing an applied result. Catch arithmetic/rule-policy failures and preserve the original snapshots on rejection.

### Medium 3: Navigation And Dungeon Services Can Apply Invalid Default IDs

`ContentId` deliberately makes `default(ContentId)` invalid, but positional runtime snapshots and transitions do not enforce validity. [`RuntimeNavigationService.Navigate`](../src/Convergence.Framework/Runtime/NavigationTransitions.cs#L93) and [`RuntimeDungeonTraversalService`](../src/Convergence.Framework/Runtime/DungeonTraversal.cs#L177) compare IDs and apply transitions without validating them. Matching default source IDs plus an allowing policy can create invalid destination, checkpoint, or boss state.

Save validation may catch the state later, but runtime services should not create it.

**Required action:** add typed invalid-identifier rejection codes and validate all transition/state-change IDs before consulting policies or mutating snapshots.

### Medium 4: Authored Rulesets Are Labels For Mostly Fixed Built-Ins

The architecture supports injection through policy interfaces and `IRuntimeRulesetBindingResolver`, so developers are not forced to use the standard rules. However, the supplied catalog-bound resolver accepts only exact standard policy IDs at [`RuntimeRulesetBindings.cs:133`](../src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs#L133). The production combat configuration exposes roughly thirty settings in [`ProductionCombatRuleset.cs`](../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L8), while the authored binder recognizes only `weakMultiplier` and `resistMultiplier` at [`RuntimeRulesetBindings.cs:327`](../src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs#L327). Growth, stats, rewards, economy, and Press Turn reject all authored parameters.

This is modular in code but not yet fully data-driven.

**Required action:** add a host-supplied policy-factory registry keyed by policy ID, define validated parameter contracts for supplied built-ins, and decide which rules are catalog-configurable versus code-only. Optional systems such as Moon Phase should remain absent unless a host registers and binds them.

### Medium 5: Encounter Events Still Leak English Presentation And Omit Typed Data

`BattleEncounterEvent` has a typed kind and a few optional IDs, but requires an English `Message` at [`BattleEncounterRunner.cs:57`](../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L57). Round number, initiative order, and acting team are currently present only in formatted strings at lines 563-635. `BattleEncounterFaultCode` has only two values even though the runner can fail for initiative, turn-economy, command, lifecycle, and liveness reasons.

A Godot host cannot build a complete localized or animated interface from structured values without parsing framework text.

**Required action:** make events structurally complete: typed round, team, order, action, outcome, and fault metadata. Retain a debug message as optional convenience, never as the only source of state.

### Medium 6: Save Validation Is Comprehensive, But Aggregate Restore Is Host-Reimplemented

`RuntimeSaveValidator` performs extensive aggregate checks and currently accepts only contract version 6 at [`RuntimePersistenceSnapshots.cs:293`](../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs#L293). Framework can restore individual actors through `CatalogBattleActorFactory`, but it has no reusable aggregate restore coordinator.

The DemoHost therefore reimplements actor classification, party checks, actor restoration, field restoration, inventory, wallet, session, Compendium, knowledge, and host-context publication in [`TrainingAnnexPersistenceController.cs:222`](../samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexPersistenceController.cs#L222). Another host must repeat this ordering correctly.

There is also no version-migration seam; every version other than the current one is rejected. That is acceptable before the first stable save contract, but not after users create durable saves.

**Required action:** add an optional framework aggregate restore service that returns restored actor state plus immutable session snapshots and typed diagnostics. Keep scene objects and host context host-owned. Before stabilizing the first save wire contract, add an explicit migration interface or formally commit to breaking pre-release saves.

### Low 1: Runtime Random Targeting Has A Hidden Ordered Fallback

[`BattleExecutionServices`](../src/Convergence.Framework/Execution/ExecutionPolicies.cs#L115) silently substitutes `OrderedRuntimeTargetSelectionPolicy` when no runtime-random policy is supplied. Authored `random` item/basic/effect targeting then chooses the first candidates, which is deterministic but not random. The skill resolver also contains an always-true runtime-type check at [`ConditionAndTargetResolution.cs:220`](../src/Convergence.Framework/Execution/ConditionAndTargetResolution.cs#L220), leaving its alternate branch unreachable.

**Required action:** require an explicit runtime-random target policy, or provide one backed by the required `IRandomSource`; remove the unreachable branch.

### Low 2: DemoHost Flattens Every Content Filename

[`Convergence.DemoHost.csproj:19`](../samples/Convergence.DemoHost/Convergence.DemoHost.csproj#L19) copies all content as `Content/<filename>`, discarding source directories. Current basenames are unique, so today it works. Two future packs using the same logical filename would collide silently at build/output time.

**Required action:** preserve pack-relative directories or generate a collision-checked content index.

## Health Assessment

### Strong Areas

- The active dependency graph is clean: DemoHost depends on Framework; Framework depends on no host or external package.
- Strict deserialization and semantic validation are unusually thorough for this stage.
- Definitions, result collections, catalog repositories, and save snapshots are defensively copied.
- Skill, item, passive, and lifecycle execution use staged actor transactions; known failure paths generally preserve live state.
- Encounter liveness guards, duplicate participant checks, typed lifecycle transactions, participant-result snapshots, and turn-economy validation are present.
- Numeric overflow protection exists in combat, rewards, negotiation, and several progression paths.
- Save validation covers content packs, actor identities, numeric domains, party/stock roles and capacities, equipment, field state, Compendium, knowledge, durations, and references.
- Fusion preview/selection/transaction authority and Compendium snapshot integrity are materially stronger than a demo-only implementation.
- Host neutrality is enforced by source and reflection tests.
- Test depth is high: Framework-only tests cover 91.4% of lines and 74.6% of branches.

### Areas That Are Not Required For A First Stable Release

These should not distract from the work above:

- reproducing every archived prototype behavior;
- migrating proprietary or legacy game data;
- adding more console presentation;
- adding Moon Phase or any other optional mechanic by default;
- NuGet packaging;
- full deterministic replay;
- a large original game campaign;
- every possible JRPG subsystem.

A finished framework needs a stable, documented, composable supported scope. It does not need to contain every mechanic a future game might invent.

## Recommended Production Sequence

### 1. Repair The Product Boundary

- Promote Convergence solution, README, global configuration, docs, content, source, samples, and tests to the Git root.
- Archive the stale root solution, old README, old documentation, and old archive under one clearly non-built historical tree.
- Decide and apply the intended software/content licenses.

**Exit:** cloning the repository presents one product and one successful root build command.

### 2. Correctness Stabilization

- Fix resource-add overflow.
- Harden stat allocation domains and atomic rejection.
- reject invalid navigation/dungeon IDs.
- define encounter cancellation, event-sink failure, and command-commit atomicity.
- require honest random-target policy ownership.

**Exit:** targeted regressions plus the full suite pass; no known state-consistency defect remains.

### 3. Stabilize Public And Authoring Contracts

- Decide the supported v1 public namespace/type surface.
- Generalize or split the hardcoded Persona/Demon stock roles before their names and save fields become stable contracts.
- Internalize accidental implementation types.
- add XML API documentation and API compatibility baselines.
- author JSON Schemas and a standalone content-validation tool.
- set an explicit pre-release assembly/version policy.

**Exit:** a developer can author content and integrate code without reading implementation files.

### 4. Finish Modularity And Host Data Contracts

- add policy-factory registration for authored rulesets;
- expose the intended built-in policy parameters;
- make battle and negotiation events structurally complete and localization-safe;
- document thread-safety and mutation-serialization requirements.

**Exit:** hosts select optional mechanics and policies explicitly, and presentation never parses rule text.

### 5. Finish Persistence Composition

- add aggregate session restore support;
- define the first stable save-contract policy and migration seam;
- test manual and suspend restore through actor, party, field, knowledge, Compendium, inventory, and host reattachment.

**Exit:** two independent hosts can restore the same framework snapshot without duplicating restoration rules.

### 6. Build The Real Godot Reference Consumer

- add a minimal Godot 4 C# sample using a source project reference;
- load clean content from `res://`;
- bridge commands/events/runtime IDs;
- execute an encounter and save/restore;
- add desktop build plus one export/AOT smoke gate supported by the chosen Godot baseline.

**Exit:** the primary host target is demonstrated by the actual engine, not only a shaped unit test.

### 7. Release Candidate Hardening

- add CI, analyzers, warnings-as-errors, documentation-link checks, API compatibility, coverage thresholds, and content-schema validation;
- add catalog-load, large-roster, battle-loop, fusion, and save-validation benchmarks;
- add targeted property/fuzz tests for IDs, SemVer, JSON discriminators, arithmetic boundaries, and state transitions;
- run a release-candidate soak through Framework and the Godot sample.

**Exit:** tagged source is reproducible, documented, measured, and supportable.

## Verification Results

- Release tests: **704 passed, 0 failed, 0 skipped**.
  - Framework: 556.
  - DemoHost: 148.
- Framework coverage from Framework tests: **91.37% line, 74.56% branch**.
- DemoHost coverage from DemoHost tests: **88.28% line, 66.95% branch**.
- Clean nonincremental solution build: **0 warnings, 0 errors**.
- `--clean-battle-demo`: exit 0, player victory.
- `--clean-field-demo`: exit 0, all shared-effect cases completed.
- `--clean-save-demo`: exit 0, contract v6 round-trip and validation completed.
- `--clean-training-annex-demo`: exit 0, catalog, traversal, item, battle, reward, growth, and save completed.
- `--help`: exit 0.
- Active local Markdown links: all resolved.
- `git diff --check`: passed before this report was added.
- Framework forbidden-reference boundary tests: passed.
- Active content: 36 JSON files; manifest/reference contract tests passed.
- Git worktree was clean and synchronized before this report.
- Original repository-root legacy solution build: **failed as expected** because `JRPG/JRPG.csproj` no longer existed. Blocker 1 subsequently archived that solution and promoted the clean solution to the Git root.

### Blocker 1 Resolution Verification

- Git-root nonincremental solution build: **0 warnings, 0 errors**.
- Clean tests from the Git-root solution: **705 passed, 0 failed, 0 skipped**.
  - Framework: 557.
  - DemoHost: 148.
- Product-boundary regression tests: **5 passed**, including the Git-root
  identity and solution-project resolution checks.
- DemoHost smoke modes: battle, field, save, Training Annex, and help all exited
  successfully from the Git root.
- Active documentation: **22 Markdown files** checked; all local links resolve.
- Active source and project archive-reference search: no matches.
- Active content remains 36 manifest-owned JSON documents; only repository paths
  changed.

## Final Recommendation

Do not begin another broad mechanics phase yet. Begin with **Product Boundary And Correctness Stabilization** using sections 1 and 2 above. Once those pass, move directly into API/schema stabilization and the real Godot consumer.

That path turns the framework already built into a product. Adding more systems before doing it would increase the public surface and make stabilization harder without bringing Convergence closer to a trustworthy release.
