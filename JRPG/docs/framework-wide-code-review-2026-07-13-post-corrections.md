# Framework-Wide Code Review After Corrections

Date: 2026-07-13

Branch: `track-12-recovery`

Reviewed commit: `2830a3acf42c5f16ef070f9cd369471da757ccae` (`Review-Whole-12`)

Latest committed correction baseline: `2181b7c` (`Review-Whole-15`); the M3 correction described below is in the current working tree.

## Review Rule

This is a fresh review of the current source and tests. Earlier review reports and their summaries were deliberately excluded while findings were formed. They were not used as a checklist or as evidence that a correction worked.

The review covered the framework project structure and public boundaries, definitions and content loading, catalog qualification, validation, actor state, persistence and restore, action/effect execution, battle orchestration and lifecycle, combat policies, party and stock transitions, field and dungeon state, inventory and economy, negotiation and rewards, fusion and Compendium, host contracts, and the corresponding tests.

## Verdict

**Phase 8 should not begin yet.**

The framework is substantially healthier than the earlier baseline. The reviewed corrections are present in the current source and the complete quality gate is green. The H1 lifecycle defect and M1-M3 authority/boundary defects identified by this review were corrected on 2026-07-13. No high- or medium-severity finding from this review remains open. The three low-severity public-boundary findings remain part of the gate established by this report and should be resolved before the runtime baseline is closed for Phase 8.

Recommended gate:

1. H1 and M1-M3 are complete.
2. Correct L1-L3 before closing the runtime baseline for Phase 8 or publishing the framework package.
3. The remaining findings do not block design discussion, but no new host layer should treat the current package as final until the gate is rerun.

## Findings

### H1. Authored duration kinds are accepted but not executed completely (corrected 2026-07-13)

**Evidence**

- The public definition vocabulary exposes `instant`, `turns`, `phase`, `battle`, and `permanent` durations in `JRPG.Framework/Data/Definitions/SharedPrimitives.cs:44`.
- Semantic validation accepts turn and phase duration data in `JRPG.Framework/Data/SkillSystem/Validation/SkillSystemContentValidator.cs:1546`.
- Stateful effects store the supplied duration for ailments, stat stages, charges, shields, affinity Break, and affinity overrides.
- Runtime ticking in `JRPG.Framework/Logic/Battle/Execution/BattleRuntimeState.cs:861` handles only `TurnDurationDefinition`. Every other duration returns without changing or expiring state.
- The status lifecycle public service has turn-start, turn-end, application, and cleanup operations, but no phase-duration operation in `JRPG.Framework/Logic/Battle/Execution/BattleStatusLifecycle.cs:377`.
- The encounter runner emits a phase-end lifecycle callback in `JRPG.Framework/Logic/Battle/Runtime/BattleEncounterRunner.cs:723`, but no canonical lifecycle implementation consumes that callback to expire `PhaseDurationDefinition` state.
- Existing tests prove that phase, battle, and permanent durations can deserialize or survive snapshots; they do not prove their runtime expiry semantics.

**Impact**

An authored instant or phase-limited status can remain active beyond its promised scope. For example, an instant affinity Break is stored in actor state and cannot expire through the current tick path. This is a contract violation shared by several effect families and can alter later actions, phases, and restored state.

**Required correction**

- Define executable semantics for all five duration kinds at one canonical lifecycle boundary.
- Ensure instant state expires at the approved effect/action boundary.
- Expire phase state only for its authored phase ID.
- Clear battle state at battle completion while preserving genuinely permanent state according to an explicit policy.
- Add lifecycle tests for every duration kind across ailments, stat stages, charges, shields, Break, affinity overrides, and other statuses.

**Resolution**

- `BattleDurationLifecycleService` is now the canonical action-end, phase-end, and cleanup boundary used by `BattleStatusLifecycleService`.
- `RuntimeActorState` expires instant and matching-phase state across all seven duration-bearing state families, ticks turn durations for generic other statuses, removes battle-scoped state at encounter cleanup, and preserves permanent state.
- `OrderedEffectExecutor` owns a nesting-aware action scope, so instant state remains available to later effects and nested trigger actions but expires before the next independent action.
- The Training Annex lifecycle port and `AutomatedBattleRunner` now consume encounter phase-end and battle-end duration boundaries.
- Focused regressions cover all five duration kinds across every state family, nested ordered actions, and a complete automated encounter boundary.

H1 is resolved. M1-M3 were subsequently corrected; L1-L3 keep the Phase 8 gate open.

**H1 verification**

- Focused duration, active execution, and automated runtime tests: **77 passed, 0 failed, 0 skipped**.
- Full solution tests: **1,035 passed, 0 failed, 0 skipped**.
- `JRPG.Framework` nonincremental build: **0 warnings, 0 errors**.
- Full solution nonincremental build: **98 warnings, 0 errors**; the warning count is unchanged and remains isolated to compatibility-host nullable debt.
- Framework package: `JRPG.Framework.1.0.0.nupkg` created successfully; the existing missing-readme packaging advisory remains.
- Clean battle, field, save, and Training Annex demos: all exited `0`; battle and Training Annex outcomes remained victories, and save validation remained at zero diagnostics.
- Framework host/legacy forbidden-reference search and serializer-boundary search: no prohibited references found.
- `git diff --check`: passed. `Data/Jsons`: unchanged.

### M1. Assessment and execution can resolve random targets twice (corrected 2026-07-13)

**Evidence**

- `BattleActionExecutor.ExecuteAsync` calls `Assess(request)` before dispatching execution in `JRPG.Framework/Logic/Battle/Execution/BattleActionExecutor.cs:490`.
- Skill assessment resolves targets, then `ExecuteSkill` calls `SkillExecutor.Execute`; that method performs its own assessment again in `JRPG.Framework/Logic/Battle/Execution/SkillExecutor.cs:26`.
- Item execution has the same second-assessment behavior in `JRPG.Framework/Logic/Battle/Execution/ItemExecutor.cs:201`.
- Basic attack and other direct effect actions resolve once during action assessment and again in `ExecuteEffects` at `JRPG.Framework/Logic/Battle/Execution/BattleActionExecutor.cs:842`.
- Random selection invokes the host policy during resolution in `JRPG.Framework/Logic/Battle/Execution/ConditionAndTargetResolution.cs:211` and `RuntimeTargetResolver.cs:55`.
- Current tests use stable first/ordered target policies and do not assert that the policy is invoked once.

**Impact**

A stateful random policy can choose target A for the returned assessment and target B for execution. The host can display one target while the framework mutates another, and the random source advances more than once for a single command. This breaks assessment/execution parity and deterministic replay expectations.

**Required correction**

Resolve targets once into an immutable, request-bound assessment token and execute from that token, or collapse internal assessment and execution into one resolution transaction. Add call-count and alternating-target regressions for skills, items, basic attacks, and analyze/effect actions.

**Resolution**

- Skill, item, and battle-action assessments now snapshot ordered target IDs and untargeted state instead of retaining live resolved-target collections.
- Every assessment carries an executor-owned, logical-request-bound, single-use preparation token. Wrong-executor, wrong-request, and reused tokens return typed `AssessmentInvalid` diagnostics without mutation.
- One-call execution prepares once internally. Hosts that display an assessment can pass that exact assessment to the prepared-execution overload.
- Execution rebinds the prepared IDs into the staged actor transaction. Skill, item, basic-attack, analyze, escape/direct-effect, and automated-selector paths no longer invoke random selection during execution.
- The Training Annex battle and field adapters now execute the same assessment they present, and cancellation before execution leaves the token and item inventory untouched.

M1 is resolved. M2 and M3 were subsequently corrected; L1-L3 keep the Phase 8 gate open.

**M1 verification**

- Focused battle-action and automated-runtime regressions: **42 passed, 0 failed, 0 skipped**.
- Full solution tests: **1,042 passed, 0 failed, 0 skipped**.
- `JRPG.Framework` nonincremental build: **0 warnings, 0 errors**.
- Full solution nonincremental build: **98 warnings, 0 errors**; the warning count remains isolated to compatibility-host nullable debt.
- Framework package: created successfully.
- Clean battle, field, save, and Training Annex demos: all exited `0`; battle outcomes and save validation remained unchanged.
- Framework host/legacy forbidden-reference and serializer-boundary searches: no prohibited references found.
- Target-resolution search: resolver calls remain only in assessment methods; prepared execution consumes recorded IDs.
- `git diff --check`: passed. `Data/Jsons`: unchanged.

### M2. Runtime stat and base-resource numeric domains are not protected at the restore boundary (corrected 2026-07-13)

**Evidence**

- `RuntimeStatBlockSnapshot` accepts arbitrary decimal values in `JRPG.Framework/Logic/Runtime/RuntimeStateSnapshots.cs:181`.
- `RuntimeActorSnapshot` accepts arbitrary base-resource values in `RuntimeStateSnapshots.cs:443`.
- `RuntimeActorState` copies both collections without a numeric-domain check in `JRPG.Framework/Logic/Battle/Execution/BattleRuntimeState.cs:103`.
- Save validation delegates actor checks to `RuntimeActorSnapshotIntegrity` and catalog-reference checks, but does not validate stat or base-resource numeric domains in `JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs:424`.
- `StandardStatResolutionPolicy` converts a decimal directly to `int` in `JRPG.Framework/Logic/Runtime/ProgressionPolicies.cs:176`; an out-of-range value throws `OverflowException`.
- `StandardResourceGrowthPolicy` can calculate a negative maximum and then call `Math.Clamp` with an invalid range in `ProgressionPolicies.cs:314`.
- Natural ailment recovery also converts a runtime decimal stat to `int` in `JRPG.Framework/Logic/Battle/Execution/BattleStatusLifecycle.cs:718`.

**Impact**

A host-created or restored snapshot can pass `IRuntimeSaveValidator`, restore successfully, and later escape the diagnostic boundary with an arithmetic exception during ordinary stat, growth, or ailment processing.

**Required correction**

Define contract-only numeric invariants at the shared runtime snapshot boundary. At minimum, reject conversion-unsafe stat values and negative base resources using stable diagnostics before restore. Add hostile snapshot tests proving validation aggregates the errors and that standard policies cannot throw after `RequireValidSnapshot()` succeeds.

**Resolution**

- `RuntimeActorNumericDomain` defines one public representation-safety contract: base/effective stats are `0..Int32.MaxValue`, while base-resource values are nonnegative and retain the full positive decimal range.
- `RuntimeActorSnapshotIntegrity` applies that contract to every stat and base-resource entry. `RuntimeSaveValidator` maps failures to separate stable base-stat, effective-stat, and base-resource codes with precise actor paths and preserves deterministic aggregation order.
- `RuntimeActorState.Restore` rejects invalid snapshots through the same integrity service. Direct actor construction and progression replacement enforce the identical domain before publishing state.
- `StandardStatResolutionPolicy` uses saturating decimal composition and integer conversion. `StandardResourceGrowthPolicy` validates direct requests and calculates against its configured cap before potentially overflowing addition or multiplication. Natural ailment recovery clamps/saturates its probability arithmetic before integer conversion.
- Invalid persisted values are rejected rather than silently normalized. Fractional nonnegative stats remain valid, and no balance-specific upper limit was added to base resources.

M2 is resolved. M3 was subsequently corrected; L1-L3 keep the Phase 8 gate open.

**M2 verification**

- Focused persistence, actor-state, progression, and status-lifecycle tests: **87 passed, 0 failed, 0 skipped**.
- Full solution tests: **1,050 passed, 0 failed, 0 skipped**.
- `JRPG.Framework` nonincremental build: **0 warnings, 0 errors**.
- Full solution nonincremental build: **98 warnings, 0 errors**; the warning count is unchanged and remains isolated to compatibility-host nullable debt.
- Framework package: created successfully.
- Clean battle, field, save, and Training Annex demos: all exited `0`; battle outcomes remained victories and save validation remained at zero diagnostics.
- Hostile snapshot regression: six independent base-stat, effective-stat, and base-resource faults are returned in one ordered diagnostic result; both `RequireValidSnapshot()` and direct restore reject the snapshot.
- Accepted-boundary regression: `Int32.MaxValue` stats and `Decimal.MaxValue` base resources validate and restore, then complete standard stat/resource and natural-recovery processing without arithmetic exceptions.
- Framework host/legacy public-boundary tests remain green; refined runtime-source searches found no prohibited host/legacy references.
- `git diff --check`: passed. `Data/Jsons`: unchanged.

### M3. `AutomatedBattleRunner` bypasses the canonical status lifecycle and bound turn-economy policy (corrected 2026-07-13)

**Evidence**

- `AutomatedBattleRunner` is a public framework service in `JRPG.Framework/Logic/Battle/Runtime/AutomatedBattleRunner.cs:320`.
- It constructs its own encounter services, including a direct `new PressTurnEngine()`, at `AutomatedBattleRunner.cs:350`.
- Its private lifecycle port always reports `CanAct`, dispatches only `battle_start` and `owner_turn_end` passive events, and makes phase-end and battle-end lifecycle calls no-ops at `AutomatedBattleRunner.cs:423`.
- It therefore does not run canonical guard clearing, ailment restrictions, poison/sleep processing, natural recovery, duration ticking, or battle-end status cleanup supplied by `BattleStatusLifecycleService`.

**Impact**

Two public clean battle entry points can produce different rules for the same actors and definitions. A host selecting the convenience runner can silently lose authored ailments and lifecycle behavior, while also bypassing its catalog-bound Press Turn factory.

**Required correction**

Either inject the canonical lifecycle and turn-economy factory into `AutomatedBattleRunner`, or explicitly demote the type to a narrowly named demo/test helper outside the authoritative framework surface. Add parity tests that run the same encounter through both supported entry points.

**Resolution**

- `BattleStatusEncounterLifecyclePort` is now the reusable framework adapter from `BattleStatusLifecycleService` and `BattleExecutionServices` into the encounter lifecycle contract. Hosts explicitly supply the registered battle-start and owner-turn-end event IDs.
- `AutomatedBattleRunner` now requires an `IBattleEncounterLifecyclePort` and a bound `BattleTurnEconomyRuleset`. Its old three-argument constructor, private reduced lifecycle, private duration-only fallback, direct `new PressTurnEngine()`, and hardcoded phase-progress limits were removed.
- The runner delegates lifecycle callbacks and turn-economy construction to those dependencies exactly as `BattleEncounterRunner` does. Typed status/restriction events and immutable turn-economy snapshots remain visible in `AutomatedBattleResult`.
- Clean battle, Training Annex, and Godot-shaped composition roots now provide lifecycle and turn-economy authority explicitly. Training Annex reuses the `standard_press_turn` binding it already validated instead of discarding it.
- A parity regression runs equivalent guarded, ailment-bearing encounters through `AutomatedBattleRunner` and direct `BattleEncounterRunner` composition. Both paths clear guard, apply the same restriction and turn-end damage, tick the same authored duration, perform the same battle cleanup, create the same number of economies, and expose the same Press Turn state sequence.

M3 is resolved. No high- or medium-severity finding remains; L1-L3 keep the Phase 8 gate open.

**M3 verification**

- Focused catalog battle runtime tests: **30 passed, 0 failed, 0 skipped**, including constructor-boundary, lifecycle/factory, non-Press-Turn, and direct-runner parity regressions.
- Full solution tests: **1,054 passed, 0 failed, 0 skipped**.
- `JRPG.Framework` nonincremental build: **0 warnings, 0 errors**.
- Full solution nonincremental build: **98 warnings, 0 errors**; warnings remain isolated to compatibility-host nullable debt.
- Framework package: `JRPG.Framework.1.0.0.nupkg` created successfully.
- Clean battle, field, save, and Training Annex demos: all exited `0`; both battle demos ended in player-team victories and both save validations returned zero diagnostics.
- Framework host/legacy forbidden-reference search: no prohibited production references found. The automated runner contains no direct Press Turn, no-op lifecycle, private lifecycle, or hardcoded phase-progress construction.
- `git diff --check`: passed. `Data/Jsons`: unchanged.

### L1. Encounter requests do not reject duplicate runtime instance IDs

`BattleEncounterRequest` snapshots participants but does not enforce unique `RuntimeInstanceId` values in `JRPG.Framework/Logic/Battle/Runtime/BattleEncounterRunner.cs:95`; `RunAsync` checks only round limit and nonempty participants at line 440. Events and target identity become ambiguous, and `AutomatedBattleTurnHandler` later uses `Single` by instance ID at `AutomatedBattleRunner.cs:513`. Reject duplicate participant identities at the encounter boundary with a typed fault/diagnostic and add a regression test.

### L2. `EffectExecutionResult` collection immutability can be bypassed with record cloning

The constructor defensively snapshots passive activations and host requests, but both collection properties remain public `init` properties in `JRPG.Framework/Logic/Battle/Execution/ExecutionContracts.cs:56`. External code can use `with` to replace either property with a mutable collection. Make the collection properties get-only or route cloning through a constructor that snapshots replacements; test mutation after cloning.

### L3. Public shop price arithmetic accepts unchecked extreme inputs

`ShopTransactionService.CalculateBuyPrice` and `CalculateSellPrice` use `double` and unchecked casts to `int` in `JRPG.Framework/Logic/Runtime/ResourceManagementServices.cs:719`. `RuntimeShopOfferSnapshot` is publicly constructible without validating base price, and arbitrary Luck values can create overflow/sentinel prices. Use decimal/checked arithmetic and stable rejection or an explicit clamping contract. Normal catalog-authored pricing tests do not cover this public boundary.

## Verified Current Health

The following conclusions come from direct source inspection rather than prior review claims:

- The framework is a dependency-free `net9.0` class library. Newtonsoft remains in the compatibility console host only.
- Public framework boundaries remain serializer-, filesystem-, console-, Godot-, and legacy-runtime neutral.
- Definitions and most runtime result/snapshot types defensively copy supplied collections; nested custom parameters are normalized away from serializer-owned values.
- `ContentId`, semantic versioning, manifest traversal, direct dependency visibility, qualification, catalog repositories, and semantic validation have clear typed boundaries.
- Actor execution uses staged runtime copies and commits only after successful effect execution.
- Encounter results contain detached actor snapshots rather than live mutable participants.
- The synchronous encounter wrapper avoids capturing a host synchronization context, and asynchronous framework paths consistently avoid context capture.
- Break is represented in executable actor state, participates in affinity resolution, ticks with turn durations, persists through snapshots, and is included in cleanup.
- Party/stock transitions enforce role, capacity, ownership, and global identity invariants while retaining intentional active-and-owned demon overlap.
- Persistence validates content-pack versions, actor/content references, party/stock roles and capacities, equipment ownership, knowledge uniqueness, restored catalog provenance, and actor stat/base-resource numeric domains.
- Fusion planning, preview, and transaction authority use typed validated selections and immutable snapshots. Rank-offset results are parent-order independent.
- Compendium registration, familiar knowledge import, recall pricing policy, and duplicate knowledge handling are framework-owned and typed.
- No production framework `TODO`, `FIXME`, or `NotImplementedException` markers were found.

## Quality Gate Results

- Full test suite after the M3 correction: **1,054 passed, 0 failed, 0 skipped**.
- `JRPG.Framework` nonincremental build: **0 warnings, 0 errors**.
- Full solution nonincremental build: **98 warnings, 0 errors**. The warnings are compatibility-host/test nullable debt, not framework compilation warnings.
- Framework package: produced `JRPG.Framework.1.0.0.nupkg` successfully. NuGet warns that the package has no readme; it should not be treated as publication-ready metadata yet.
- `--clean-battle-demo`: exit `0`, player-team victory.
- `--clean-field-demo`: exit `0`, all shared item/field effect cases completed.
- `--clean-save-demo`: exit `0`, save contract version 6 restored with zero diagnostics.
- `--clean-training-annex-demo`: exit `0`, original-content battle victory, rewards applied, save validated.
- Framework forbidden-reference search: no prohibited production references found.
- `git diff --check`: passed with the M3 implementation and report update present.
- `Data/Jsons`: unchanged.
- The committed baseline was synchronized with `origin/track-12-recovery`; the current working tree contains the reviewed M3 correction and has not been committed by this task.

## Phase 8 Readiness Gate

The corrected framework is a credible foundation, but **it is not ready to enter Phase 8 as a closed runtime baseline today**. The immediate correction order should be:

1. ~~Complete duration lifecycle authority (H1).~~ Corrected on 2026-07-13; final gate verification is recorded with the implementation.
2. ~~Make action assessment and execution share one resolved target set (M1).~~ Corrected on 2026-07-13.
3. ~~Enforce runtime numeric domains before restore/policy execution (M2).~~ Corrected on 2026-07-13.
4. ~~Unify the automated battle convenience path with canonical lifecycle and turn economy (M3).~~ Corrected on 2026-07-13.
5. Address L1-L3, rerun this gate, and only then begin Phase 8-36 host interchangeability.

This conclusion does not invalidate the previous correction work. That work fixed substantial defects and is present in the current source. The remaining findings are narrower, but they cross public contracts and should be resolved before presentation interchangeability becomes the next layer built on top.
