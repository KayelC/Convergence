# Framework-Wide Source Code Review

**Review date:** 2026-07-13  
**Branch:** `track-12-recovery`  
**Reviewed commit:** `9c0d5d4e2a2e99bdc377a3d90fb64bc1bf8c34ff`  
**Verdict:** The framework has a strong typed foundation and a healthy build, but it is not ready to advance without correcting the state-integrity, battle-liveness, lifecycle, and modularity findings below.

## Review Method

This is a fresh review of the current checkout. Previous code-review reports and their conclusions were not used as evidence. The review examined the implementation under `JRPG.Framework`, the clean host boundary, and the tests that claim the corresponding behavior.

The review covered:

- content IDs, definitions, deserialization, validation, dependency loading, qualification, and catalogs;
- runtime actor state, snapshots, persistence validation, save policies, and restore paths;
- typed effects, targeting, conditions, costs, items, passives, ailments, affinities, combat math, Press Turn, battle orchestration, AI, and knowledge;
- progression, stats, resources, inventory, equipment, economy, party and stock transitions;
- generic navigation, dungeon traversal, encounter planning, and compatibility dungeon behavior;
- inheritance, fusion policies, planning, previews, transactions, Compendium, and familiar knowledge;
- framework/host dependency boundaries, cancellation, immutability, diagnostics, and test quality.

No runtime code was changed during this review.

## Severity Summary

- **Critical:** 0
- **High:** 0
- **Medium:** 11
- **Low:** 6

There is no evidence of a remote-code-execution or data-exfiltration vulnerability. This is an in-process game-rules library, so the principal risks are invalid state acceptance, partial mutation, nonterminating execution, inconsistent rules, and framework coupling.

These counts describe the source at reviewed commit `9c0d5d4`. Post-review corrections are recorded beneath the applicable finding and do not silently rewrite the original assessment.

## Medium Findings

### M1. Save validation and actor restoration do not enforce the same contract

**Correction status:** Corrected by **Review-Whole-1** on 2026-07-13 and verified against the current `track-12-recovery` working tree.

`RuntimeSaveValidator.ValidateActorCatalogReferences` validates actor skill and ailment catalog references, but it does not validate the actor-local structure that `RuntimeActorState.Restore` requires.

Confirmed false-valid inputs include:

- duplicate resource IDs;
- duplicate ailments, statuses, stat tracks, charges, shields, affinity overrides, or analysis targets;
- duplicate passive state or activation keys;
- passive state and activation references to unloaded passives;
- malformed actor-local form references;
- actor equipment that is missing, in the wrong slot, or not owned by the aggregate inventory.

The validator can therefore return `IsValid == true` and allow `RequireValidSnapshot()`, after which restoration throws or returns `SnapshotInvalid`. The mismatch is visible between:

- `RuntimePersistenceSnapshots.cs:378-409`;
- `BattleRuntimeState.cs:120` and `BattleRuntimeState.cs:568-630`;
- `PassiveRuntime.cs:84-125`.

**Impact:** A host can accept a corrupt or tampered save as valid and fail only during restore. Validation cannot currently serve as the promised restore gate.

**Required correction:** Define actor-snapshot integrity once and use it from both save validation and actor restoration. Add adversarial tests proving every validator-approved actor can be restored.

**Implemented correction:**

- `RuntimeActorSnapshotIntegrity` now defines the intrinsic restore contract once. Both `RuntimeSaveValidator` and `RuntimeActorState.Restore` use it.
- Validation rejects duplicate resources, learned/equipped skills, capabilities, ailments, statuses, stat tracks, charges, shields, affinity overrides, analysis targets/layers, passive states, and passive activation keys.
- Equipped skills must be learned. Passive state and activation entries must refer to passives actually loaded for restoration.
- Save-level graph checks now validate actor kinds, actor-local form references, form entity identity, actor equipment catalog/slot/ownership, and unique equipment assignment across actors.
- Existing `RuntimeSaveValidationCode` numeric values were preserved by appending the new stable codes instead of inserting them among older members.
- Save contract version `5` remains unchanged because no serialized field or meaning changed; invalid snapshots are rejected earlier.
- Adversarial tests prove corrupt structures receive precise actor-indexed paths, direct actor restoration uses the same guard, catalog restoration rejects malformed snapshots, and every actor in the representative validator-approved save restores successfully through `CatalogBattleActorFactory`.

**Review-Whole-1 verification:** 23 focused persistence tests and 60 related state/fusion/Compendium/save-host tests passed. The full suite passed with **955 passed, 0 failed, 0 skipped**. The framework built with **0 warnings and 0 errors**; the solution retained **98 legacy console-host warnings and 0 errors**. All four noninteractive clean demos exited `0`, framework boundary searches were clean, and `Data/Jsons` was unchanged.

### M2. Battle orchestration can mutate before cancellation and can fail to terminate

**Correction status:** Corrected by **Review-Whole-2** on 2026-07-13 and verified against the current `track-12-recovery` working tree.

`BattleEncounterRunner.RunAsync` performs synchronization, resets passive activations, publishes actor events, determines initiative, and runs battle-start lifecycle before its first explicit cancellation check at line 462.

The phase loop also has no progress guard. A turn handler that repeatedly returns `ActionTurnConsumption.None` without a requested outcome leaves Press Turn icons unchanged forever. The round limit does not help because execution never leaves the current phase loop.

Relevant code:

- `BattleEncounterRunner.cs:429-452` performs work before the first check;
- `BattleEncounterRunner.cs:482-574` loops while icons remain;
- `BattleEncounterRunner.cs:630-648` intentionally leaves state unchanged for `None`.

Initiative output is only checked for being nonempty. Duplicate, missing, or unknown team IDs are not rejected and can cause duplicate or omitted phases.

**Impact:** Pre-cancelled requests can mutate state if injected ports do not independently check the token. A faulty or hostile host adapter can hang the battle indefinitely.

**Required correction:** Check cancellation before any mutation or publication, validate initiative as an exact permutation of participating teams, and add a typed per-phase progress/free-action policy.

**Implemented correction:**

- `BattleEncounterRunner.RunAsync` checks cancellation as its first operation and again immediately before synchronizers, passive resets, policy/port calls, turn-economy mutation, lifecycle calls, and event publication.
- Initiative is snapshotted and must be an exact, duplicate-free permutation of participating team IDs. Invalid output faults before synchronization, passive reset, or battle-start lifecycle.
- Every phase receives a mandatory `BattlePhaseProgressPolicy` with finite command and consecutive-free-action limits. Repeated `ActionTurnConsumption.None` results now fault deterministically instead of hanging inside a phase.
- The command limit independently bounds faulty custom economies that continually replenish actions. Non-`None` consumption that fails to advance economy state is also rejected as a typed battle fault.
- Adversarial tests cover pre-cancellation with zero port calls, cancellation between startup events and lifecycle, cancellation during economy creation and command handling, missing/duplicate/unknown initiative teams, repeated free actions, and a deliberately expanding custom economy.

**Review-Whole-2 verification:** 42 focused encounter/turn-economy/ruleset/presentation tests passed. The full suite passed with **965 passed, 0 failed, 0 skipped**. The framework built with **0 warnings and 0 errors**; the solution retained **98 legacy console-host warnings and 0 errors**. All four noninteractive clean demos exited `0`, framework boundary searches were clean, and `Data/Jsons` was unchanged.

### M3. Typed ailment effects bypass the lifecycle guard rule

**Correction status:** Corrected by **Review-Whole-3** on 2026-07-13 and verified against the current `track-12-recovery` working tree.

`BattleStatusLifecycleService.TryApplyAilment` correctly blocks ailments while the target is guarding. The standard typed effect path does not call that rule: `ApplyAilmentEffectExecutor` resolves resistance and chance, then directly applies the ailment.

Relevant code:

- `EffectExecutors.cs:193-223` bypasses `IsGuarding`;
- `BattleStatusLifecycle.cs:210-263` implements the guard block.

Existing tests cover direct lifecycle application and ordinary typed ailment execution separately, but not a typed skill or item targeting a guarding actor.

**Impact:** The same ailment has two contradictory outcomes depending on which public framework API applies it.

**Required correction:** Make one ailment-application service authoritative and route typed effects through it, including passive resistance replacements and authored chance.

**Implemented correction:** `BattleAilmentApplicationService` is now the single clean application authority. Direct lifecycle requests and `ApplyAilmentEffectExecutor` both delegate to the instance owned by `BattleExecutionServices`. The authority checks defeated state, guard, base ailment resistance, conditional passive resistance replacement, immunity, and the authored chance policy before applying exclusivity and duration state. `AilmentApplicationPolicyRequest` carries the effective authored chance directly instead of depending on a skill-effect DTO. The legacy adapter preserves its existing random behavior by supplying a host-owned chance policy rather than retaining a second application algorithm.

Adversarial tests execute a real typed ailment skill against a guarding actor, replace the application authority with a recording implementation, and retain the existing passive-resistance test. These tests would fail if the effect executor recreated application logic locally.

### M4. Authored ailment lifecycle variants are accepted but not fully executable

**Correction status:** Corrected by **Review-Whole-3** on 2026-07-13 and verified against the current `track-12-recovery` working tree.

The schema and validator accept more behavior than the lifecycle runtime preserves:

- only the first active ailment contributes a turn-start restriction (`BattleStatusLifecycle.cs:142`), even though multiple nonexclusive ailments are valid content;
- `LimitedActionsAilmentTurnBehaviorDefinition.AllowedActionIds` is discarded; the result contains only `LimitedAction` and cannot tell a command handler which actions are legal;
- `CustomAilmentTurnBehaviorDefinition` silently becomes `CanAct` (`BattleStatusLifecycle.cs:303-318`) and there is no runtime handler contract;
- ailment-owned trigger conditions are not evaluated (`BattleStatusLifecycle.cs:339-384` checks effect conditions only);
- `StopTarget` failure behavior is not honored by the ailment trigger loop;
- stat-stage mutation is unbounded (`BattleRuntimeState.cs:301-305`), despite the lifecycle contract retaining bounded stages.

**Impact:** Structurally valid, registered content can load successfully and then behave differently from what it authored.

**Required correction:** Return typed action restrictions, add a custom behavior execution port or reject custom behavior as unsupported, evaluate trigger conditions, share ordered-effect failure handling, define multiple-ailment precedence, and enforce the approved stage range.

**Implemented correction:**

- `BattleTurnStartRestriction` preserves the outcome, allowed action IDs, and source ailment IDs. `BattleEncounterTurnRequest` carries that complete restriction to the host turn handler rather than reducing it to an enum.
- `MostRestrictiveBattleTurnPolicy` evaluates every active ailment. The default precedence is return/flee, skip, forced confusion, forced physical, limited action, then can-act. Equally strong limited-action restrictions intersect their allow-lists; an empty intersection becomes skip. Hosts may inject another explicit policy.
- `ICustomAilmentTurnBehaviorHandler` is an explicit runtime port. Missing handlers produce an actionable exception and can no longer silently turn custom behavior into `CanAct`.
- Ailment trigger-level conditions are evaluated. Skill, item, passive, and ailment effects now share `OrderedEffectExecutor`, including distinct `StopTarget`, `StopAction`, and interruption semantics.
- Runtime stat stages saturate safely at the approved `-4..+4` range, including extreme integer deltas. Actor restore and complete-save validation reject out-of-range persisted stages at the precise authored snapshot path.

**Review-Whole-3 verification:** 100 focused ailment/effect/passive/encounter/persistence tests passed. The full suite passed with **975 passed, 0 failed, 0 skipped**. The framework built with **0 warnings and 0 errors**; the solution retained **98 legacy console-host warnings and 0 errors**. All four noninteractive clean demos exited `0`, framework boundary searches were clean, and `Data/Jsons` was unchanged.

### M5. Demon-stock commands can mutate non-demon party members

**Correction status:** Corrected by **Review-Whole-4** on 2026-07-13 and verified against the current `track-12-recovery` working tree.

Several methods named and typed as demon operations do not require the affected actor to exist in `DemonStock`:

- `SwapActiveDemon` checks only that the outgoing actor is active (`PartyStockTransitions.cs:311-334`);
- `ReturnDemon` can remove any active actor, including the owner (`PartyStockTransitions.cs:336-349`);
- `ReplaceDemon` accepts an old actor that is active but not owned in Demon Stock (`PartyStockTransitions.cs:366-399`);
- `ConsumeDemon` can consume any active actor (`PartyStockTransitions.cs:401-414`).

The current tests use correctly stock-owned demons and do not exercise owner or ordinary-party-member IDs.

**Impact:** A direct framework caller can remove, replace, or consume the protagonist or a normal party member through a demon API.

**Required correction:** Require Demon Stock ownership for every demon operation while preserving the intentional active-plus-owned overlap.

**Implemented correction:** `PartyStockTransitionService` now treats membership in `RuntimePartyStockSnapshot.DemonStock` as the role proof for every demon-specific mutation. `SwapActiveDemon` and `ReturnDemon` require both ownership and active deployment; `ReplaceDemon` and `ConsumeDemon` require ownership but continue to work for either active or standby demons. The active-only replacement fallback was removed, so replacing a normal party member can no longer manufacture a new Demon Stock entry. Rejections use the existing stable `NotOwned` or `NotActive` codes and return the exact unchanged input snapshot.

Adversarial tests call swap, return, replace, and consume against both the owner and an ordinary active ally. Positive tests retain active-plus-owned summon/swap/return behavior and prove standby demons may still be replaced or consumed. A stale console-adapter characterization that used `ReturnDemon` to remove an ordinary party member was corrected to assert rejection before continuing through a properly owned demon workflow.

**Review-Whole-4 verification:** 101 focused party/stock, adapter, battle-action, fusion, Compendium, and presentation tests passed. The full suite passed with **980 passed, 0 failed, 0 skipped**. The framework built with **0 warnings and 0 errors**; the solution retained **98 legacy console-host warnings and 0 errors**. All four noninteractive clean demos exited `0`, framework boundary searches were clean, and `Data/Jsons` was unchanged.

### M6. Fusion resolution has unresolved authority gaps

Two independent gaps remain:

1. Recipe matching is symmetric and sorted by selector specificity, but equal-specificity overlapping recipes are resolved by repository order (`FusionRuntimeServices.cs:533-565`). Content validation checks each recipe independently and does not reject an ambiguous pair (`SkillSystemContentValidator.cs:876-918`).
2. `CreateAccidentInheritance` accepts a caller-supplied list named `legalSkillIds` and a caller-supplied maximum, but does not prove either came from the plan (`FusionRuntimeServices.cs:933-960`). A host can therefore generate accident inheritance containing an ineligible or unrelated skill.

The normal preview and transaction paths now correctly require `ValidatedFusionInheritanceSelection`; the accident API has not reached the same authority level.

**Impact:** Equivalent content can yield different fusion results based on load order, and a host can bypass inheritance policy in accident generation.

**Required correction:** Reject ambiguous recipes during content validation or add explicit deterministic priority. Derive accident candidates and limits from `FusionPlanningResult` internally and return a validated selection.

### M7. The encounter runner hard-wires Press Turn instead of treating it as an optional module

**Correction status:** Corrected by **Review-Whole-2** on 2026-07-13 and verified against the current `track-12-recovery` working tree.

`BattleEncounterServices` always exposes a concrete `PressTurnEngine` factory and silently creates a standard engine when none is supplied (`BattleEncounterRunner.cs:337-363`). Every phase is then controlled by Press Turn icons (`BattleEncounterRunner.cs:475-482`).

The engine also still exposes:

- a legacy `HitType` overload (`PressTurnEngine.cs:57-109`);
- console-formatted icon text (`PressTurnEngine.cs:198-220`);
- SMT III-specific comments and assumptions.

`RuntimeRulesetBindingResolver` binds only the fixed `standard_press_turn` implementation (`RuntimeRulesetBindings.cs:218-230`).

**Impact:** Developers who do not want Press Turn cannot use the framework encounter runner without pretending to use it or replacing the runner. This conflicts with the framework-first modularity goal.

**Required correction:** Extract a generic turn-economy interface. Press Turn should be one optional implementation. Move icon formatting to hosts and retire the legacy overload from the clean contract.

**Implemented correction:**

- `IBattleTurnEconomy` and immutable `BattleTurnEconomySnapshot` now form the encounter boundary. `BattleEncounterServices` requires an explicit economy factory and no longer silently constructs Press Turn.
- `StandardActionTurnEconomy` supplies a neutral one-action-per-actor option. `PressTurnEngine` is an optional implementation selected explicitly by the existing `standard_press_turn` ruleset.
- `RuntimeRulesetBindingResolver.BindTurnEconomy` returns `BattleTurnEconomyRuleset`, pairing the selected economy factory with its finite phase-progress policy without exposing a concrete engine to the runner.
- Encounter turn requests and events now carry generic typed economy state. Press Turn uses `PressTurnEconomySnapshot` for full/blinking counts; the framework event message is generic.
- Console icon formatting moved to `PressTurnIconFormatter`. The legacy `HitType` enum moved back to the console host, and the clean `PressTurnEngine` no longer exposes a `HitType` overload or console-formatted text.
- Tests prove the same encounter runner works with `StandardActionTurnEconomy`, while all Press Turn outcome and host-presentation behavior remains covered.

**Review-Whole-2 verification:** Included in the 42 focused and **965 total** passing tests recorded under M2. The warning, demo, boundary, and content-preservation results are identical.

### M8. Custom parameters are only shallowly immutable and are not type-safe for direct callers

`DefinitionCollections.SnapshotParameters` copies only the outer dictionary (`DefinitionCollections.cs:29-33`). A caller can supply nested mutable lists or dictionaries, mutate them later, and alter an already validated definition or catalog. Direct callers can also store `JsonElement`, Godot objects, or arbitrary host objects in the public `object?` values.

The JSON path is safer because `SkillSystemDtoMapper.MapJsonValue` recursively converts JSON into immutable CLR values (`SkillSystemDtoMapper.cs:626-640`). That protection does not apply to definitions constructed directly in C#.

**Impact:** Domain immutability and serializer/engine neutrality depend on how a definition was created.

**Required correction:** Recursively copy and validate the allowed parameter value algebra at the definition boundary, or replace `object?` with a closed serializer-neutral parameter value union.

### M9. Skill and item execution are not atomic when extension points fail

`SkillExecutor` commits resource costs before executing effects (`SkillExecutor.cs:26-46`). If a custom effect, formula, damage policy, or condition handler throws, costs remain spent and no typed result is returned.

`BattleActionExecutor.ExecuteItem` has the inverse ordering problem: item effects mutate actor state before the inventory reservation commits (`BattleActionExecutor.cs:644-731`). If the host reservation commit throws or rejects, rollback cannot undo the healing, damage, status, escape, or host request already produced.

**Impact:** Host/plugin failures can leave partially committed gameplay state, despite the framework exposing assessment and transaction-style APIs.

**Required correction:** Define a no-fail commit contract after successful reservation, or stage effect mutations and commit inventory plus actor state atomically. Convert extension failures into typed diagnostics.

### M10. Public policy/configuration numeric boundaries are insufficiently validated

Several public APIs can throw unexpectedly or produce invalid results from otherwise constructible inputs:

- `ProductionCombatRuleset` accepts an unvalidated public config (`ProductionCombatRuleset.cs:186-192`);
- zero divisors fail reward calculations (`ProductionCombatRuleset.cs:493-508`);
- reversed clamp ranges fail (`ProductionCombatRuleset.cs:591-592`);
- `hits.Maximum + 1` can overflow (`ProductionCombatRuleset.cs:559-566`);
- runtime actor combat profiles hardcode level `1` (`ProductionCombatRuleset.cs:594-632`);
- `StandardLevelGrowthPolicy` does not validate `statCap` and uses unchecked experience addition (`ProgressionPolicies.cs:455-495`);
- `CubicExperienceCurve` converts an unbounded floating-point cube to `long` (`ProgressionPolicies.cs:345-355`);
- inventory addition can throw from `checked` instead of returning its typed rejection (`ResourceManagementServices.cs:232-253`);
- hospital cost can overflow `MissingSp * 5` (`ResourceManagementServices.cs:924-940`).

**Impact:** Public framework services that normally return diagnostics can instead throw or calculate negative/nonsensical values at boundary inputs.

**Required correction:** Validate policy construction, use checked/saturating arithmetic deliberately, and translate boundary failures into stable diagnostic results.

### M11. Compatibility mechanics and project terminology still live inside clean framework code

The generic replacements are sound, but older framework modules still encode prototype decisions:

- `RuntimeFieldDungeonService` hardcodes a legacy `E_slime` fallback as `legacy_455f736c696d65` (`FieldDungeonStateMachines.cs:603-618`);
- the same service owns lobby, clock, terminal, city, and presentation labels rather than generic traversal concepts;
- negotiation has built-in Full Moon blocking, Demon Stock terminology, Macca terminology, Medicine ID `101`, fixed gift percentages, and a fallback demand formula (`BattleNegotiationAndRewards.cs:191-243`, `313-329`, `405-445`, `494-538`);
- inventory/wallet/economy APIs expose `Macca` directly (`ResourceManagementServices.cs:92-105`, `355-404`);
- standard ruleset binding returns `LegacyStockCapacityPolicy` (`RuntimeRulesetBindings.cs:182-194`).

**Impact:** The framework remains reusable technically, but these modules prescribe prototype/IP-inspired mechanics instead of providing neutral concepts and injected policies.

**Required correction:** Keep the newer generic navigation/traversal services as the clean authority. Move compatibility policy, IDs, messages, currencies, and fallback content into the legacy host or optional policy packages.

## Low Findings

### L1. AI affinity scoring is incomplete for multi-target actions

`DeterministicBattleActionSelector.Score` examines only the first resolved target (`AutomatedBattleRunner.cs:129-163`). An all-target or random-target skill can therefore hit another target known to Null, Repel, or Absorb the element even though the selector claims to avoid those outcomes.

### L2. Affinity conditions disagree with passive-resolved combat affinity

`HasAffinityConditionDefinition` calls `GetElementalAffinity` without passive replacements (`ConditionAndTargetResolution.cs:62-63`). Damage execution resolves passive affinity replacements first (`EffectExecutors.cs:80-85`). A condition and the damage it gates can therefore see different affinities.

### L3. Catalog actor creation accepts inconsistent level state and can escape its diagnostic boundary

Unlocks and initialization use `request.Level`, while runtime progression may use a different `request.Progression.Level` (`CatalogBattleActorFactory.cs:166-175`, `199-245`). Duplicate resources returned by an initialization policy are not checked before constructing `RuntimeActorState`, so they can throw outside the factory's typed result.

### L4. Battle encounter results contain live mutable participants

`BattleEncounterResult` copies participant references, not final actor snapshots (`BattleEncounterRunner.cs:76-88`, `114-135`). Mutating an actor after the run changes what an earlier result appears to contain. `AutomatedBattleResult` already demonstrates the safer final-snapshot pattern.

### L5. The synchronous encounter wrapper can deadlock a single-threaded host

`BattleEncounterRunner.Run` blocks on `RunAsync` (`BattleEncounterRunner.cs:376-377`) while internal awaits capture the current context. A Godot/UI host should use `RunAsync`; the synchronous wrapper should be explicitly compatibility-only or the async implementation should avoid context capture.

### L6. Break exists as a resolver argument but not as an executable runtime state

`ElementalAffinityResolver` supports `isBroken` (`ElementalAffinityResolver.cs:8-31`), but there is no typed Break status/effect and production execution never passes `true`. Break is currently a manually invocable resolver branch, not content-executable framework behavior.

## Verified Strengths

The following claims are supported by current source and verification, not documentation summaries:

- `System.Text.Json` deserialization is strict: case-sensitive names, unknown-member rejection, no comments, and no trailing commas.
- Source-generated serializer metadata is used for the schema boundary.
- Catalog loading rejects noncanonical paths, duplicates, missing/unexpected documents, dependency cycles, transitive-only references, and wrong-type external references.
- Content validation aggregates deterministic diagnostics and keeps elemental, ailment, and instant-death defenses separate.
- Catalog IDs are canonically qualified and local-ID repository lookups are rejected.
- Ordinary definition and snapshot collections are defensively copied; the exception is nested custom parameter values described in M8.
- Generic navigation and dungeon traversal are policy-injected and do not force menus, scenes, stairs, or encounters.
- Persistent player knowledge and encounter-local AI knowledge are separated in the clean Training Annex host.
- Recent fusion protections are present: rank-offset parent order is neutral, previews require validated inheritance, transaction preparation is checked, and Compendium pricing is injectable.
- Runtime party/stock identity overlap rules and save checks cover active/reserve duplication, active-form duplication, Demon and Persona capacity, and familiar-knowledge duplicate keys.
- Framework source contains no direct console, filesystem, Godot, Newtonsoft, legacy `Database`, legacy DTO, `Combatant`, `Persona`, or `IGameIO` references.

## Verification Results

Commands were run from the reviewed checkout:

- `dotnet test JRPG.sln --no-restore --logger "console;verbosity=minimal"`
  - **951 passed, 0 failed, 0 skipped**.
- `dotnet build JRPG.Framework/JRPG.Framework.csproj --no-restore --no-incremental /clp:Summary`
  - **0 warnings, 0 errors**.
- `dotnet build JRPG.sln --no-restore --no-incremental /clp:Summary`
  - **98 warnings, 0 errors**.
  - The warnings are in legacy console-host code; representative groups are nullable legacy DTO properties, legacy fusion strategies, and legacy UI bridges.
- `dotnet run --no-build -- --clean-battle-demo`
  - Exit `0`, clean victory.
- `dotnet run --no-build -- --clean-field-demo`
  - Exit `0`, shared field effects completed.
- `dotnet run --no-build -- --clean-save-demo`
  - Exit `0`, host JSON round-trip and save validation completed.
- `dotnet run --no-build -- --clean-training-annex-demo`
  - Exit `0`, catalog, rulesets, actor hydration, dungeon event, encounter, item, battle, reward, growth, and save validation completed.
- `git diff --check`
  - Clean after this report was added.
- Framework forbidden-reference search
  - No production framework matches.
- `git status --short -- Data/Jsons`
  - Clean; content was not modified.

The green suite is valuable evidence, but it does not invalidate the findings above. The missing adversarial combinations are precisely where several defects remain.

## Recommended Correction Order

Do not begin another feature phase before the first five correction groups are complete.

1. **Review-Whole-1: Persistence/restore parity** - completed and verified 2026-07-13.
   - Actor snapshot integrity is centralized and shared by validation and restoration.
   - Actor-local resources, statuses, passives, forms, equipment, and ownership are validated.
   - Representative validator-approved actors restore through the catalog factory; adversarial false-valid cases are rejected before restore.
2. **Review-Whole-2: Encounter cancellation, liveness, and turn economy** - completed and verified 2026-07-13.
   - Cancellation precedes encounter mutation/publication, and initiative must be an exact team permutation.
   - Mandatory finite phase-progress limits prevent nonprogressing and self-replenishing loops.
   - `IBattleTurnEconomy` is the runner boundary; Press Turn and neutral standard actions are explicit implementations.
3. **Review-Whole-3: Ailment authority and lifecycle completeness** - completed and verified 2026-07-13.
   - Unify typed ailment application.
   - Preserve limited-action IDs and custom behavior execution.
   - Fix trigger conditions/failure policies and stat-stage bounds.
4. **Review-Whole-4: Party/stock role invariants** - completed and verified 2026-07-13.
   - Every demon-specific mutation requires Demon Stock ownership.
   - Owner and ordinary-party-member attacks on the demon API are rejected without mutation.
   - Active-plus-owned demons and legitimate standby replacement/consumption remain supported.
5. **Review-Whole-5: Fusion authority**
   - Reject ambiguous recipes.
   - Make accident inheritance plan-derived and validated.
6. **Review-Whole-6: Atomic execution and boundary arithmetic**
   - Stage mutations or define no-fail commits.
   - Validate combat/growth/resource configuration and overflow paths.
7. **Review-Whole-7: Definition immutability and runtime condition consistency**
   - Close the custom parameter value algebra.
   - Align passive-resolved affinities and multi-target knowledge scoring.
8. **Review-Whole-8: Framework neutrality**
   - Isolate legacy dungeon/negotiation/economy terminology and policy.
   - Keep generic navigation, traversal, content, and host contracts as the reusable authority.

After these corrections, rerun this review against source and adversarial tests before moving into the next planned capability phase.

## Final Assessment

The codebase is not in a failed state. Its clean architecture has real substance: typed content, strict loading, catalog qualification, immutable snapshots, host-neutral contracts, original content, clean runtime demos, and a warning-free framework build all exist and work.

However, the framework is not yet internally complete. Review-Whole-1 through Review-Whole-4 have corrected save/restore contract mismatch, battle-loop liveness, mandatory Press Turn coupling, contradictory ailment paths, incomplete authored ailment execution, and Demon Stock role authorization. The most important remaining risks are fusion authority, atomic execution, unchecked boundary arithmetic, definition immutability, and project-specific mechanics inside supposedly generic runtime services. Those should be corrected before adding more breadth.
