# Framework-Wide Code Review: Third Pass

Date: 2026-07-14  
Branch: `track-12-recovery`  
Reviewed commit: `255d349` (`Review-Whole-19`)

## Review Method

This review was performed from the current source and tests. Earlier review reports and their summaries were not used as evidence.

The review covered:

- 86 framework C# source files, approximately 27,649 lines.
- Definitions, defensive collection handling, strict JSON mapping, validation, catalog construction, and qualification.
- Runtime actor state, persistence, progression, party and stock, inventory, equipment, economy, navigation, and dungeon transitions.
- Typed actions and effects, passive dispatch, status lifecycle, combat resolution, encounter orchestration, AI selection, Press Turn, and battle knowledge.
- Fusion, inheritance, transactions, Compendium, negotiation, recruitment, and rewards.
- Project references, framework neutrality, package output, deterministic randomness, and the current test suite.

No critical or high-severity finding was identified. Five medium findings and three low findings remain.

## Findings

### M1. Automated battles discard non-skip turn restrictions

`BattleEncounterRunner` correctly passes the complete typed turn-start restriction to the turn handler. The automated handler, however, converts every outcome other than `CanAct` into the same normal turn consumption.

Evidence:

- `BattleEncounterRunner.cs:638-668` obtains and forwards `BattleTurnStartRestriction`.
- `BattleStatusLifecycle.cs:204-213` defines distinct precedence and outcomes for limited action, forced physical, forced confusion, flee, and return-to-stock.
- `AutomatedBattleRunner.cs:467-479` treats every non-`CanAct` outcome as a generic consumed turn.

Impact:

- `Skip` happens to behave acceptably.
- `LimitedAction` incorrectly loses the entire turn.
- `ForcedPhysical` and `ForcedConfusion` execute no forced command.
- `FleeBattle` and `ReturnToStock` do not change deployment state, so the actor remains in battle.

This affects the public automated runner whenever authored ailments impose those restrictions. It does not affect a custom host turn handler that implements the restriction contract correctly.

Recommended correction: add a typed automated restriction resolver that chooses allowed or forced commands and applies flee/return deployment outcomes. Add one encounter-level test for every `BattleTurnStartOutcome`.

**Correction status (2026-07-14): implemented.**

- `AutomatedBattleRunner` now requires an explicit `IAutomatedBattleTurnRestrictionResolver` and forwards every non-`CanAct` restriction to it instead of collapsing the outcome.
- `AutomatedBattleTurnRestrictionResolver` consumes `Skip`, applies distinct flee and return-to-stock deployment transitions, and executes limited/forced commands through an injected `IAutomatedBattleRestrictionActionSource` plus the canonical `IBattleActionExecutor`.
- The action source supplies the registered action ID alongside the typed command, so limited actions are checked against authored allowed-action IDs without hardcoded command names. Skill, item, and basic-attack IDs are cross-checked against their command definitions before assessment or mutation. Forced physical requires a typed `BasicAttackBattleActionCommand`. Confusion targeting and command choice remain developer-policy decisions rather than framework guesses.
- A missing command policy, unavailable selection, disallowed action, failed assessment, or rejected execution produces an explicit battle fault. The old silent skip behavior is no longer available.
- Automated battle results now retain final deployment/active state and map `DeploymentChanged` events, allowing hosts and tests to observe flee and return-to-stock outcomes without inspecting mutable actors.
- Encounter-level tests at `CatalogBattleRuntimeTests.cs:528-758` cover `Skip`, allowed and rejected `LimitedAction`, `ForcedPhysical`, `ForcedConfusion`, `FleeBattle`, `ReturnToStock`, and the missing-policy fault path.

### M2. Restored timed state is not validated as runtime-valid

Authored content validation requires positive turn durations and registered tick/phase IDs, but snapshot restoration does not repeat the structural duration checks.

Evidence:

- `SharedPrimitives.cs:44-62` allows programmatic construction of turn and phase durations without constructor guards.
- `RuntimeActorSnapshotIntegrity.cs:42-212` validates duplicates, stats, resources, and references, but not duration kind or payload.
- `BattleRuntimeState.cs:892-948` restores every supplied duration directly.
- `BattleRuntimeState.cs:1118-1145` decrements turn values and silently expires zero or negative values.

Impact:

A host-owned save can pass malformed turn counts, default tick/phase IDs, or a duration kind that should not represent retained runtime state. Restoration accepts it, and later lifecycle behavior becomes silent or inconsistent instead of returning a save diagnostic.

Recommended correction: add shared runtime-duration integrity validation and map its diagnostics into `RuntimeSaveValidationResult`. Validate every timed-state collection, including ailments, statuses, stat stages, charges, shields, Breaks, and affinity overrides.

**Correction status (2026-07-14): implemented.**

- `RuntimeActorSnapshotIntegrity` now validates durations before any actor state is restored. The same integrity boundary is already consumed by direct actor restoration, catalog actor restoration, and aggregate save validation, so the three entry points cannot drift.
- The catalog now retains the immutable event and phase vocabularies supplied during content loading. Retained turn durations require a positive remaining count and a registered tick-event `ContentId`; retained phase durations require a registered phase `ContentId`. Battle and permanent durations remain valid retained state. Instant durations are rejected because the canonical lifecycle expires them at the action boundary.
- Validation covers ailments, other statuses, stat stages, charges, shields, affinity Breaks, and affinity overrides. Nullable duration fields remain valid where the runtime explicitly supports untimed state.
- Public save diagnostics now distinguish invalid retained kinds, invalid turn counts, invalid tick-event IDs, and invalid phase IDs, preserving the actor and authored collection path for host presentation.
- Regression coverage constructs malformed state in every timed collection, including empty and well-formed-but-unregistered IDs, verifies deterministic aggregate diagnostics, and proves malformed state is rejected by save validation, direct restore, and catalog restore. Separate coverage proves registered turn and phase state plus battle and permanent state still restore successfully.
- Verification passed 136 focused snapshot/lifecycle/catalog tests and all 1,069 solution tests. The framework build remains at 0 warnings; the complete solution retains its existing 100 legacy-host warnings. Battle, field, save, and Training Annex demos all completed successfully, and production content remained unchanged.

### M3. Combat arithmetic accepts values that can overflow during execution

Combat profiles and ruleset parameters reject negative values but do not establish upper numeric bounds. Several combat paths then use direct decimal multiplication.

Evidence:

- `ProductionCombatRuleset.cs:168-213` accepts any nonnegative stats and multipliers.
- `RuntimeRulesetBindings.cs:479-510` accepts any positive authored decimal multiplier.
- `ProductionCombatRuleset.cs:767-772` directly multiplies ailment modifiers while creating a runtime profile.
- `ProductionCombatRuleset.cs:401-421`, `542-547`, and `675-700` directly multiply damage, accuracy, charge, affinity, and formula values.
- The reward calculations in the same ruleset already use saturating helpers, demonstrating the safer pattern.

Impact:

Extreme but contract-valid authored multipliers, or several large ailment multipliers, can throw `OverflowException` instead of yielding a bounded result or validation diagnostic. A malformed content pack can therefore terminate combat.

Recommended correction: choose explicit numeric ceilings during content/ruleset validation, or consistently use checked saturating arithmetic at the runtime boundary. Add tests combining maximum runtime stats with large ruleset and ailment multipliers.

**Correction status (2026-07-14): implemented.**

- The framework now uses one internal `CombatArithmetic` policy for checked decimal addition, subtraction, multiplication, division, multiply-then-divide scaling, aggregation, integer bonus aggregation, and double-to-decimal formula conversion. An operation that exceeds the representable numeric domain saturates at the matching decimal or integer boundary instead of throwing or wrapping.
- No arbitrary balance ceiling was introduced. Catalog-authored ruleset multipliers and programmatic combat profiles retain their existing nonnegative contract, including extreme values; the execution boundary now contains those values safely.
- `ProductionCombatRuleset` applies the policy to base damage, charge, target damage, critical, guard, affinity, variance, hit/evasion, critical chance, instant death, ailment application, initiative, reward helpers, multi-hit totals, and ailment-derived profile modifiers. Critical-taken bonuses also saturate rather than wrapping.
- The shared typed-effect path now protects multi-hit aggregation, passive add-then-multiply stacks, percentage amount and resource-condition calculations, and resource restoration. Ordinary calculations keep their existing operation order; multiply-then-divide falls back to a bounded representation only when the original decimal product cannot be represented.
- Aggregate skill costs are intentionally different from damage: an unrepresentable total is rejected as insufficient before mutation instead of being saturated and accidentally treated as payable.
- Adversarial tests cover maximum combat profiles/configuration, maximum catalog-bound Weak and Resist multipliers, stacked maximum ailment modifiers and critical bonuses, two maximum damage hits, maximum percentage restoration, overflowing passive stacks, and an unrepresentable two-cost skill. Existing normal-value combat vectors remain unchanged.
- Verification passed 84 focused combat/ruleset/passive/effect tests and all 1,075 solution tests with no failures or skips. The framework build remains at 0 warnings; the complete solution retains its existing 100 legacy-host warnings. Battle, field, save, and Training Annex demos all completed successfully, and production content remained unchanged.

### M4. Reward and negotiation aggregates can overflow or wrap

Individual reward yields now saturate, but the service aggregates them with `int` sums. Negotiation also accepts unbounded answer scores and positive demand weights.

Evidence:

- `BattleNegotiationAndRewards.cs:855-881` uses `Enumerable.Sum<int>` for enemy EXP and currency totals.
- `BattleNegotiationAndRewards.cs:700-714` sums demand weights in checked `int` arithmetic and then uses an unchecked cumulative total.
- `BattleNegotiationAndRewards.cs:408-434` accumulates mood scores with unchecked `int` addition.
- `SkillSystemContentValidator.cs:746-775` validates required negotiation content and positive weights but does not bound scores or aggregate weights.

Impact:

Two individually saturated enemy rewards can throw during total calculation. Large demand weights can throw before selection, while large answer scores can wrap mood from positive to negative or vice versa.

Recommended correction: aggregate with `long` or saturating arithmetic, validate total authored demand weight, and define an explicit score domain. Add multi-enemy and multi-answer boundary tests.

### M5. Lifecycle custom-handler failures can leave partial live mutations

Skill and item execution stage actor mutations and reject on handler failure. Battle-start and turn-end passive lifecycle execution instead runs directly against live actor state.

Evidence:

- `PassiveRuntime.cs:554-575` records activation before executing trigger effects.
- `EffectExecutors.cs:449-463` invokes registered custom effect handlers directly.
- `BattleStatusLifecycle.cs:575-619` dispatches passives and ailment triggers against the live actor before later lifecycle work.
- `BattleStatusEncounterLifecyclePort.cs:30-88` exposes that live path to encounters.
- `BattleEncounterRunner.cs:571-574`, `639-668`, and `731-739` does not convert lifecycle exceptions into a typed fault or restore prior state.

Impact:

If a registered lifecycle handler throws after an earlier effect succeeds, resources/statuses and activation counts can remain changed while the encounter call escapes without a result. This is inconsistent with the atomic skill/item boundary.

Recommended correction: execute each lifecycle dispatch through staged participant state, commit only after all effects succeed, and convert extension failures into a typed encounter fault. Decide whether one failing trigger rolls back only that trigger or the complete lifecycle step, then encode that policy in tests.

### L1. Some public lifecycle result collections can still be mutated

Most newer result types snapshot supplied collections. A few positional records still store caller-owned `IReadOnlyList` references directly.

Evidence:

- `PassiveRuntime.cs:430-440` exposes raw effect and activation lists.
- `BattleStatusLifecycle.cs:225-227` exposes raw turn-end events and activations.
- `BattleStatusLifecycle.cs:269-274` exposes a raw ailment event list.

Impact:

Callers can supply a `List<T>`, mutate it after construction, or replace it through record cloning. This changes historical result evidence after the operation completed. Internal framework paths often pass read-only arrays, but the public contract does not enforce that invariant.

Recommended correction: replace positional collection records with constructors and `init` accessors that defensively snapshot, matching `EffectExecutionResult`.

### L2. Default value-type IDs bypass constructor normalization

`ContentId` and `RuntimeInstanceId` are structs, so `default` remains representable even though their public constructors reject empty values.

Evidence:

- `ContentId.cs:5-38` maps the default value to an empty string and reports it as unqualified.
- `RuntimeStateSnapshots.cs:8-39` has the same default-state behavior for runtime IDs.
- `SkillSystemContentValidator.cs:320-340` checks only whether a record ID is qualified, so a default ID is not rejected as empty.
- `DefinitionQualifier.cs:5-8` later attempts to parse the empty local ID and can throw outside catalog diagnostics.

Impact:

The strict JSON path cannot create this state, but programmatic definitions or a custom public deserializer can. The loader can therefore escape its typed diagnostic boundary during qualification. Runtime snapshot APIs have the same representable empty-ID risk where a boundary does not explicitly check it.

Recommended correction: add `IsValid`/`IsEmpty` semantics and reject default IDs at every public validation boundary, especially content identity, runtime actor identity, and persistence.

### L3. The library package is buildable but not publication-ready

`JRPG.Framework.csproj` currently declares only target framework, implicit usings, and nullable analysis. `dotnet pack` therefore produced an implicit `1.0.0` package containing only the DLL and emitted a missing-readme notice. The repository also has no `global.json`, so this review used installed SDK `10.0.301` to build the `net9.0` target even though SDK `9.0.315` is installed.

Impact:

Runtime behavior is unaffected, but public package identity, versioning policy, license, repository metadata, readme, symbols/source, and SDK reproducibility are not yet controlled.

Recommended correction: complete package metadata and deterministic package settings before publishing. Add an intentional SDK selection policy, either a pinned `global.json` with controlled roll-forward or a documented multi-SDK build matrix.

## Verified Strengths

- Framework source builds independently with 0 warnings and has no external package dependency.
- No framework production source references console APIs, filesystem APIs, Godot, Newtonsoft.Json, legacy `Database`, or legacy DTO types.
- No hidden runtime randomness, wall-clock reads, generated identities, unfinished markers, or warning suppressions were found in framework source.
- Definition and snapshot collection handling is generally defensive, including recursive custom-parameter copying.
- JSON parsing is strict and serializer details remain isolated from domain and host-facing contracts.
- Validation and catalog loading enforce pack identity, paths, direct dependency visibility, qualification, and broad semantic references.
- Runtime actor state is canonical across battle, progression, resources, equipment, forms, and persistence.
- Skill and item execution use single-use assessments, prepared targets, staged participant mutation, and commit-after-success semantics.
- Encounter orchestration validates participant identity, initiative permutations, turn-economy progress, free-action limits, command limits, and result snapshots.
- Fusion planning and transactions enforce plan authority, inheritance validation, stale-state checks, parent-order-neutral rank operations, and stock identity rules.
- Compendium entries and familiar-knowledge imports have deep snapshot and duplicate-key validation.

## Quality Gate

- Focused runtime tests: 123 passed, 0 failed, 0 skipped.
- Complete solution tests: 1,059 passed, 0 failed, 0 skipped.
- Framework nonincremental build: 0 warnings, 0 errors.
- Solution nonincremental build: 100 warnings, 0 errors. These warnings are in the retained console-host compatibility surface; framework build remains warning-free.
- Package: `JRPG.Framework.1.0.0.nupkg` produced successfully; publication metadata remains incomplete as noted in L3.
- Clean battle, field, save, and Training Annex demos: all exited 0.
- Framework forbidden-reference and unfinished-marker searches: no matches.
- `git diff --check`: passed.
- `Data/Jsons`: unchanged.
- Worktree before this report: clean and synchronized at `255d349`.

NuGet vulnerability metadata could not be refreshed because network access to `api.nuget.org` was unavailable. Exact packages were restored from the machine's local cache. This does not affect `JRPG.Framework`, which has no package references, but the console/test dependency audit remains an external verification gap.

## Production Readiness Verdict

I am comfortable moving forward with production development and Phase 8. The framework has coherent ownership boundaries, strong deterministic tests, clean engine neutrality, and no critical architectural defect requiring another rewrite.

I am not comfortable labeling the framework a stable public production release yet. M1 through M5 should be corrected first because they concern authored-content safety, lifecycle correctness, and extension-boundary atomicity. L1 and L2 should follow before declaring public contracts stable. L3 must be completed before publishing a package.

Recommended order:

1. Correct automated turn restrictions.
2. Close restore-time duration validation.
3. Define and enforce numeric domains across combat, rewards, and negotiation.
4. Make lifecycle dispatch atomic and fault-aware.
5. Finish immutable result and non-default ID contracts.
6. Complete package/release metadata.

After those corrections, repeat focused adversarial tests and the full gate. No broad redesign or renewed legacy migration is warranted by this review.
