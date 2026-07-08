# Phase 1-3 Code Review And Forward Direction

> **Status: Active implementation audit; CodeReview-1 through CodeReview-4 are implemented and ready as of 2026-07-08.** This report is derived from source, tests, builds, and executable demos. Resolved findings remain below as audit history. This document does not authorize legacy removal.

## Executive Verdict

The first three phases have produced a real framework-first vertical slice, not a facade over the legacy runtime.

The clean Training Annex path can load original content, create typed actors, apply progression policies, navigate generic locations and dungeon nodes, prepare encounters, execute field and battle actions, resolve combat and Press Turns, process ailments and passives, run deterministic enemy decisions, retain player knowledge, discard ordinary enemy knowledge, apply rewards, and validate save snapshots without starting the legacy `Database`.

The framework boundary is the strongest part of the implementation:

- `JRPG.Framework` builds with zero warnings.
- The console host references the framework; the framework does not reference the host.
- Framework public APIs remain free of console, filesystem, Godot, Newtonsoft, and legacy runtime types.
- Rules and state are generally expressed through typed definitions, commands, results, policies, and snapshots.
- Randomness, content text, commands, and event output are injectable.

The clean path is nevertheless still a proof harness rather than a production runtime. The original review identified four high-priority issues before Phase 4:

1. The clean host maintained two mutable actor-state representations and manually synchronized only parts of them. **Resolved by CodeReview-1.**
2. The battle target menu could not distinguish between multiple actors and always selected the first eligible target. **Resolved by CodeReview-2.**
3. Phase 3 restore matches actors by runtime ID without confirming that the saved entity is the actor being restored. **Resolved by CodeReview-3.**
4. The resource recalculation command calculated a result but did not apply that recalculated result to runtime state. **Resolved by the CodeReview-1 closure.**

CodeReview-1 and CodeReview-2 are ready: one actor state is authoritative, recalculation commits to it, dynamic menu rows carry typed identities, and Press Turn presentation consumes typed event state. CodeReview-3 is also ready: saved actor/entity/team mappings, saved creation context, Training Annex host dungeon-state checks, content-pack provenance, and atomic restore planning are implemented and covered by the final gate.

## Audit Scope

### Reviewed implementation

- Phase 1-01 through 1-10: clean playable spine.
- Phase 2-11 through 2-19: clean battle spine.
- Phase 3-20: interactive manual and suspend save/load candidate.
- Original `convergence.training_annex_slice` content used by the clean path.
- Framework contracts directly consumed by those phases.
- Focused framework and host tests supporting those phases.

### Repository state reviewed

- Branch: `track-12-recovery`.
- Committed HEAD and upstream: `372eeac` (`host: apply clean battle rewards`).
- Phase 1 and Phase 2 are committed.
- Phase 3-20 is present as an uncommitted worktree candidate: 16 modified files and 2 new files.
- No current `Data/Jsons` modification is present in the worktree.

This distinction matters. The report evaluates Phase 3-20, but does not describe it as a settled branch baseline.

## Verification Evidence

The following checks were run against the reviewed worktree:

| Check | Result |
| --- | --- |
| Focused Phase 1-3 tests | 63 passed, 0 failed, 0 skipped |
| Full solution tests | 806 passed, 0 failed, 0 skipped |
| Nonincremental solution build | succeeded |
| Framework warnings | 0 |
| Legacy console-host warnings | 98 existing nullable warnings |
| Clean battle demo | victory, exit code 0 |
| Clean field demo | all six actions completed, exit code 0 |
| Clean save demo | snapshot v2 round-trip and validation succeeded, exit code 0 |
| Training Annex runtime demo | catalog-to-save loop completed, exit code 0 |
| `git diff --check` | passed; only Git line-ending notices were printed |
| Framework boundary searches | no new host, Godot, Newtonsoft, or legacy runtime leakage found |
| `Data/Jsons` worktree check | clean |

Passing tests are important evidence, but not proof of completeness. Several findings below are cases where the sample shape allows an incorrect implementation to pass.

## Current Architecture From Code

```text
Training Annex JSON
        |
        v
SkillSystemCatalogLoader -> GameDataCatalog -> ruleset bindings
        |                                        |
        v                                        v
actor/encounter factories              framework policies
        |                                        |
        +------------> clean host <--------------+
                           |
          commands --------+-------- events
                           |
                           v
          action executor / encounter runner / lifecycle
                           |
                           v
            runtime snapshots and host-owned saves
```

The ownership direction is sound:

- Content supplies authored definitions and policy references.
- Framework services own validation, calculations, transitions, and typed outcomes.
- The host owns menus, labels, content file access, save serialization, and deterministic demo choices.

The main deviation is actor state. `TrainingAnnexRuntimeActor` currently holds both a mutable `CatalogBattleActor.State` and a mutable `RuntimeActorStateSet`. Field actions, battles, growth, and saves do not all operate on the same state object.

## Phase 1 Review: Clean Playable Spine

### What was actually built

1. **Interactive boot**
   - `--clean-training-annex-play` loads only the Training Annex manifest and documents.
   - Missing content returns diagnostics without reading player commands.
   - It does not call legacy `Database` startup.

2. **Actor models**
   - `CatalogBattleActorFactory` hydrates Echo Adept and three enemy models from catalog definitions.
   - `RuntimeActorSnapshot` carries identity, ownership, deployment, progression, resources, stats, skills, equipment, status, and activation state.

3. **Resources, stats, and growth**
   - Growth and stat policies bind through catalog rulesets.
   - HP/SP initialization is supplied by the host policy and represented in framework state.
   - EXP and level growth mutate `RuntimeActorStateSet` through framework transactions.

4. **Optional mechanics**
   - The Training Annex content and runtime do not require Moon Phase metadata.
   - This proves absence of the mechanic rather than forcing a neutral value.

5. **Navigation and dungeon traversal**
   - `RuntimeNavigationService` moves between arbitrary `ContentId` locations through an injected policy.
   - `RuntimeDungeonTraversalService` separately handles arbitrary dungeon/node transitions, checkpoints, barriers, and boss flags.
   - Neither service starts encounters automatically.

6. **Encounter preparation**
   - A host trigger explicitly chooses an encounter and formation.
   - `CatalogEncounterPreparationService` plans and hydrates all actors atomically in authored order.

7. **Field actions**
   - Field skills and items use the shared typed battle-action/effect pipeline.
   - Item reservation commits only after meaningful execution and rolls back otherwise.

### Strong design choices

- Navigation and dungeon traversal are optional and separate.
- Travel availability is policy-injected rather than inferred from location names.
- Encounter triggers are host-owned, which fits both console menus and Godot scene entities.
- Content loading and host presentation remain outside framework APIs.
- The field item path uses the same effect definitions and transaction semantics as battle.

### Phase 1 limitations

- The interactive menu is still a developer capability harness. Commands such as `Resolve Stats`, `Apply Victory EXP`, and `Validate Startup Snapshot` expose subsystem proofs rather than a coherent game loop.
- The roster eagerly hydrates one model for every sample enemy, while a prepared encounter creates a separate set of battle actors. Saved enemy models are therefore sample inspection state, not active encounter state.
- Skill, item, transition, and actor IDs are intentionally Training Annex-specific in the host. That is acceptable for a sample, but this host must not become the reusable host abstraction.

## Phase 2 Review: Clean Battle Spine

### What was actually built

1. **Manual commands**
   - Attack, skill, item, guard, pass, and analyze become typed `BattleActionCommand` instances.
   - Assessment runs before execution.
   - Back/cancel returns without mutation or turn consumption.

2. **Typed effects**
   - Skills and items use concrete effect definitions.
   - Display-name mutation tests show that combat behavior is not selected from names or descriptions.

3. **Combat policy**
   - `standard_damage` binds one `ProductionCombatRuleset` used for damage, hit checks, criticals, affinity, ailments, chance, power, and instant death.
   - `standard_reward` uses the same combat ruleset.
   - Invalid combat bindings stop startup instead of falling back.

4. **Press Turn**
   - `standard_press_turn` supplies the framework `PressTurnEngine` factory.
   - The encounter runner owns icon consumption and phase termination.

5. **Ailment and passive lifecycle**
   - Turn-start restrictions and turn-end status processing use `BattleStatusLifecycleService`.
   - Battle-start and owner-turn-end passives use `PassiveTriggerDispatcher`.
   - Steady Breath demonstrates a typed passive restoration effect.

6. **Enemy strategy**
   - `DeterministicBattleActionSelector` assesses authored active skills, considers encounter-local elemental knowledge, avoids known Null/Repel/Absorb, prefers known weakness, penalizes resistance, and preserves authored order for ties.

7. **Battle knowledge**
   - Player knowledge persists across battles and into saves.
   - Enemy AI receives a new knowledge state for each ordinary battle and discards it afterward.
   - Analyze learns elemental, ailment, and instant-death defenses.

8. **Rewards**
   - Victory reward calculation uses the bound reward service.
   - EXP flows through growth policy and Macca through economy transactions.
   - Session counters and the Ashling completion flag are recorded.

### Strong design choices

- The same assessment path is used by selectors and execution.
- Item use remains reservation-backed.
- Enemy randomness and combat randomness are injectable.
- AI knowledge and player knowledge have the intended different lifetimes.
- The framework emits typed action/effect results; the host owns text.
- Invalid ruleset bindings fail visibly rather than selecting hidden defaults.

### Phase 2 limitations

- The enemy policy is deliberately minimal. It does not yet provide configurable tactics, healing priorities, support behavior, switching, escape, or boss-specific persistent knowledge.
- The clean battle menu only knows the Training Annex skill and item IDs.
- There is no multi-member player party, stock, swapping, negotiation, or escape in this clean loop.
- Player knowledge is stored but no target-hover UI consumes it yet.

## Phase 3 Review: Save And Runtime Session

### What the uncommitted candidate adds

- Framework `RuntimeSaveKind`, context, policy, diagnostics, save-record metadata, and suspend-consumption contracts.
- Manual and suspend save assessment for host-registered contexts.
- Rejection while an encounter handoff is pending.
- Host-owned `System.Text.Json` serialization of `RuntimeSaveRecord`.
- One in-memory manual slot and one in-memory suspend slot.
- Validation before save and before restore.
- Restore of actors, inventory, wallet, navigation, dungeon state, session progress, host battle flags, and persistent player knowledge.
- Suspend deletion only after deserialization, validation, and host restore all succeed.
- Malformed JSON leaves the current session unchanged.

### What Phase 3 does not yet provide

- Disk-backed slots or a Godot save resource.
- Autosaves.
- Saving during battle.
- Save migrations beyond rejecting unsupported contract versions.
- Content-pack/version provenance sufficient to explain compatibility.
- Checksum, corruption recovery, backup slots, or atomic file replacement.
- A general restore coordinator shared by hosts.

The `clean_foundation` status is accurate. This is a sound contract proof with important restore-integrity work still required.

## Findings

### High: two mutable actor-state authorities can diverge

**Evidence**

- `TrainingAnnexRuntimeActor` stores both `CatalogBattleActor Actor` and `RuntimeActorStateSet RuntimeState` in [TrainingAnnexHostSupport.cs](../Host/CleanConsole/TrainingAnnex/TrainingAnnexHostSupport.cs#L14).
- Field and battle adapters copy current resource values into and out of the two states in [TrainingAnnexFieldActionAdapter.cs](../Host/CleanConsole/TrainingAnnex/TrainingAnnexFieldActionAdapter.cs#L97) and [TrainingAnnexBattleActionAdapter.cs](../Host/CleanConsole/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs#L537).
- `BattleResourceState.Maximum` is immutable, while synchronization calls only `SetResource` for the current value.

**Impact**

Level growth or restore can change maximum HP/SP in `RuntimeActorStateSet`, but the battle actor retains the old maximum. A later synchronization can clamp the restored current value to that stale maximum. Stats, status, skills, and deployment also require ongoing manual synchronization as more systems are added.

**Required correction**

Choose one mutable runtime state authority. Catalog actor data should be immutable definitions/loadout metadata; battle, field, growth, and persistence should operate on one framework runtime actor state or on explicit projections rebuilt from that state.

#### CodeReview-1 resolution

Initial consolidation completed on 2026-07-03; snapshot and recalculation closure completed on 2026-07-07:

- `RuntimeActorState` is now the sole mutable clean actor representation. It owns identity, ownership, deployment, progression, current and maximum resources, base resource values, base/effective stats, skills, capabilities, forms, equipment, defenses, statuses, analysis, passive enablement, and passive activation counts.
- `CatalogBattleActor` now contributes immutable entity/loadout metadata and exposes that one state. The duplicate `RuntimeActorStateSet` and `BattleActorState` types were removed.
- Training Annex field actions, battles, resource transactions, growth transactions, summaries, saves, and restore now read or mutate `CatalogBattleActor.State` directly. The HP/SP synchronization loops were deleted.
- Actor and target identities now use `RuntimeInstanceId` throughout clean action, effect, lifecycle, encounter, and event contracts. `ContentId` remains the identity type for authored content and host vocabulary.
- Save contract version `4` records the vital resource explicitly and preserves typed duration variants, affinity overrides, analysis, capability IDs, passive enablement, and passive activation counts. Catalog restore rebuilds a complete actor from the snapshot and catalog definitions without rerunning creation-time initialization policy.
- Resource recalculation now commits the policy result through `RuntimeResourceTransactionService.ApplyRecalculation` before the host reports success. Its regression test changes maximum values, so merely printing the policy result can no longer pass.
- Regression tests prove resource, growth, and lifecycle services mutate one object; complete snapshot restore preserves non-resource state; and restore does not silently reapply initialization defaults.

This resolves the first stabilization item and its recalculation closure. It does not resolve the separate target-selection, typed Press Turn event, or restore identity-validation findings below.

CodeReview-1 final verification: 810 tests passed with no failures or skips; focused closure coverage passed 64 tests; the framework nonincremental build produced 0 warnings; the solution nonincremental build retained 98 legacy console-host nullable warnings; clean battle, field, save-v4, and Training Annex demos all exited successfully. The closure changes no content files.

### High (resolved by CodeReview-2): the battle target menu always returned the first target

**Evidence**

- Every enemy option is encoded as the same `CleanTrainingAnnexPlayCommand.TargetEnemy` value in [TrainingAnnexBattleActionAdapter.cs](../Host/CleanConsole/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs#L1115).
- After any target option is selected, `SelectTargetAsync` returns `eligible[0].InstanceId` in the same file at line 890.

**Impact**

The current one-enemy Ashling battle passes. A two-enemy encounter would display multiple rows while every row attacks the first enemy. This is a real false positive in the current host coverage.

**Required correction**

Use a target command payload that contains the selected `RuntimeInstanceId` or `ContentId`. Dynamic menu rows must not collapse to one enum value. Add a two-enemy integration test that selects the second target.

**Resolution**

`HostCommandOption<TCommand>` and `HostCommandReadResult<TCommand>` now carry an optional `HostCommandSelectionIdentity` containing exactly one typed `ContentId` or `RuntimeInstanceId`. Training Annex target rows carry the participant runtime ID, skill rows carry the authored skill ID, and item rows carry the owned catalog item ID. The two-enemy regression selects Bramble Runner in the second row and proves the resulting damage targets its runtime instance rather than the first Ashling.

### High: restore can combine one catalog actor with another actor's saved identity

**Evidence**

- `TryRestoreActor` looks up only the current runtime instance ID, then replaces `RuntimeState` with the retrieved snapshot in [CleanTrainingAnnexPlayHost.cs](../Host/CleanConsole/TrainingAnnex/CleanTrainingAnnexPlayHost.cs#L1307).
- `RuntimeSaveValidator` verifies that the saved entity exists in the catalog, but does not compare it with the host actor expected for that runtime ID in [RuntimePersistenceSnapshots.cs](../JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs#L251).

**Impact**

A structurally valid record can assign an Ashling entity snapshot to the `echo_adept` runtime ID. The host then retains Echo Adept's `CatalogBattleActor` while its `RuntimeState` identifies and contains a different entity. This produces split identity and compounds the dual-state problem.

**Required correction**

Host restore must verify instance ID, entity definition ID, actor kind, and any host-required roster role before mutation. Prefer a framework restore plan that validates the complete mapping first and applies it atomically second.

### High (resolved by CodeReview-1 closure): resource recalculation was reported as applied without applying the recalculation

**Evidence**

- `RecalculatePlayerResourcesAsync` subtracts HP through a transaction, calls the growth policy's `Recalculate`, prints the returned values, and returns `true` in [CleanTrainingAnnexPlayHost.cs](../Host/CleanConsole/TrainingAnnex/CleanTrainingAnnexPlayHost.cs#L1930).
- The returned `ResourceRecalculationResult` is never written back into `RuntimeActorStateSet`.

**Impact**

The current test passes because the recalculated maximum happens to equal the existing maximum. If stats or base resources changed, the command would print the new maximum while the saved/runtime actor kept the old one.

**Required correction**

Add a resource-recalculation transaction that replaces the resource snapshot after validation. Test a case where the maximum actually changes.

**Resolution**

`RecalculatePlayerResourcesAsync` now applies the returned `ResourceRecalculationResult` to the canonical actor through `RuntimeResourceTransactionService.ApplyRecalculation` and reports success only after that mutation succeeds. The regression test replaces HP and SP maxima with different values and asserts the live actor changed, removing the former false-positive condition.

### Medium (resolved by CodeReview-2): Press Turn presentation parsed framework message text

**Evidence**

- `BattleEncounterEvent` has no typed Press Turn state payload in [BattleEncounterRunner.cs](../JRPG.Framework/Logic/Battle/Runtime/BattleEncounterRunner.cs#L51).
- `TrainingAnnexPressTurnEventSink` parses the English string `Press Turn: X full, Y blinking.` in [TrainingAnnexBattleActionAdapter.cs](../Host/CleanConsole/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs#L1608).

**Impact**

Presentation breaks if punctuation, wording, or localization changes. This contradicts the otherwise strong rule that host behavior should not infer meaning from display text.

**Required correction**

Add the smallest serializer-neutral typed metadata to the event, such as a `PressTurnStateSnapshot`, and let the host format it.

**Resolution**

`BattleEncounterEvent` now carries an optional immutable `PressTurnStateSnapshot`. `BattleEncounterRunner` supplies it for every `PressTurnChanged` event, and the Training Annex event sink reads only that typed state. A regression supplies deliberately unparseable/localized message text and proves icon evidence and presentation still use the typed counts.

### Medium: load policy validates the current context but not the saved record context

**Evidence**

- `RuntimeSavePolicyService.AssessLoad` validates the supplied current context and save kind, but never validates `record.Context` in [RuntimeSavePolicies.cs](../JRPG.Framework/Logic/Runtime/RuntimeSavePolicies.cs#L137).

**Impact**

A record marked as created during a pending action or in a disallowed context can be loaded later from an allowed context. The current host only creates valid records, but persistent/external storage makes record metadata untrusted input.

**Required correction**

Validate both creation metadata and current load context, with separate diagnostics when useful.

### Medium (resolved by CodeReview-3 host boundary): dungeon save validation checks only the dungeon ID

**Evidence**

- `ValidateField` verifies only that `DungeonTraversal.DungeonId` exists in the catalog in [RuntimePersistenceSnapshots.cs](../JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs#L423).

**Impact**

Unknown current nodes, visited nodes, checkpoint IDs, and boss IDs can pass validation. A host can restore a state that its traversal policy cannot interpret.

**Required correction**

Either validate against an explicit runtime dungeon graph supplied by the host, or clearly classify node/checkpoint/boss IDs as host-owned and require a host restore validator. The current mixed catalog/host boundary is incomplete.

### Medium (resolved by CodeReview-3): reward application is not atomic

**Evidence**

- `ApplyPreparedBattleRewardAsync` mutates progression first and then attempts the wallet transaction in [CleanTrainingAnnexPlayHost.cs](../Host/CleanConsole/TrainingAnnex/CleanTrainingAnnexPlayHost.cs#L1854).

**Impact**

If wallet application rejects after progression succeeds, EXP remains committed while the method reports failure and does not record reward session progress. Valid current rewards make that unlikely, but the transaction boundary is structurally unsafe.

**Required correction**

Assess every mutation first, then commit one aggregate result, or preserve a rollback snapshot.

### Medium (resolved by CodeReview-2 for the clean battle shell): the clean host was content-driven in execution but hardcoded in selection

**Evidence**

- The skill menu maps five known skill IDs to five enum values in [TrainingAnnexBattleActionAdapter.cs](../Host/CleanConsole/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs#L979).
- The item menu exposes only Annex Tonic.
- Practice Blade and several transition IDs are fixed host constants.

**Impact**

Adding a valid skill or item to the pack does not automatically make it usable. This is acceptable for a narrow reference demo, but not for the future interchangeable console/Godot host goal.

**Required correction**

Keep Training Annex defaults, but use command payloads carrying catalog IDs. Generate menus from executable definitions and inventory rather than a closed enum per content record.

**Resolution**

The battle shell now generates skill rows from battle-available actor definitions and item rows from owned battle-usable catalog items. It resolves the selected typed content ID directly and no longer contains a skill-ID-to-enum switch or an Annex-Tonic-only item menu. Test-only content exposes Focus Call without an enum case, and test inventory exposes Focus Tea as a second item; both execute through the existing typed action pipeline.

CodeReview-2 final verification: 814 tests passed with no failures or skips; focused command/event/parity coverage passed 67 tests; the framework nonincremental build produced 0 warnings; the solution nonincremental build retained 98 legacy console-host nullable warnings; clean battle, field, save-v4, and Training Annex demos all exited successfully. Framework boundary and obsolete-parser searches were empty, and `Data/Jsons` was unchanged.

### Medium (partially resolved by CodeReview-4): host and test concentration is now impeding review

CodeReview-4 split the Training Annex host's persistence, field/navigation presentation, and battle-reward application seams into focused collaborators. Current sizes after the split:

- `CleanTrainingAnnexPlayHost.cs`: 1,460 lines.
- `TrainingAnnexPersistenceController.cs`: 472 lines.
- `TrainingAnnexFieldPresenter.cs`: 109 lines.
- `TrainingAnnexBattleRewardApplicator.cs`: 114 lines.
- `TrainingAnnexBattleActionAdapter.cs`: 1,650 lines.
- `CleanTrainingAnnexPlayHostTests.cs`: larger than before because CodeReview-4 adds focused seam tests.

The host is now more clearly a coordinator: persistence/restore planning lives in `TrainingAnnexPersistenceController`, field transition messages live in `TrainingAnnexFieldPresenter`, and reward application lives in `TrainingAnnexBattleRewardApplicator`. The remaining concentration is battle selection/telemetry and the large host test file. That cleanup can happen opportunistically, but it no longer blocks Phase 4.

CodeReview-4 final verification is recorded in the completion section below.

### Medium: completion metadata has drifted across plans and the parity ledger

- The parity ledger still uses old future-track letters such as `E`, `H`, and `R` while the active plan uses numbered phases.
- `consumerMigrated` is not consistently interpretable: some clean-host consumers mark it true while other equally real clean consumers mark it false.
- Phase 3 documentation says implemented even though its code is still uncommitted.
- Verification notes are duplicated across several active documents and can appear out of order.

The ledger remains useful as protection evidence, but should not be used as an automatic completion score until these terms are defined once.

### Low (resolved by CodeReview-3): save compatibility lacks explicit content provenance

`RuntimeSaveGameSnapshot` stores a framework version and contract version, but not the loaded pack IDs and exact versions that authored the referenced records. The Training Annex host currently writes a hardcoded framework version `0.1.0`.

Before permanent saves, add content-pack provenance or an equivalent host compatibility manifest and define what `FrameworkVersion` represents.

## Test Quality Assessment

### What the tests do well

- Exercise real catalog loading rather than hand-building every definition.
- Mutate display names/descriptions to detect text-driven behavior.
- Inject deterministic random sequences for hit, miss, critical, and lifecycle cases.
- Test missing and wrong-category ruleset failures.
- Test item reservation commit and rollback.
- Test cancellation as non-mutation.
- Test player-versus-enemy battle-knowledge lifetime.
- Test malformed save JSON and suspend consumption ordering.
- Verify framework public boundaries by reflection and source searches.
- Preserve legacy characterization while clean work proceeds.

### Where the tests currently give false confidence

1. **Single enemy only** hides the target-selection defect.
2. **Unchanged resource maximum** hides the unapplied recalculation and stale battle maximum.
3. **Same roster on save/load** hides actor identity mismatch.
4. **Exact Press Turn message assertions** reinforce string parsing instead of protecting a typed contract.
5. **One giant scripted host test file** couples behavior to menu indices and makes semantic coverage harder to see.
6. **No persistent file store** means partial-write, replacement, backup, and crash behavior remain untested by design.

### Required regression additions

- Two enemies, explicitly select the second, and verify only it changes.
- Level/stat change that increases maximum HP/SP, then battle and save/load.
- Saved actor with matching instance ID but wrong entity ID must reject atomically.
- Saved record with invalid creation context must reject.
- Invalid dungeon node/checkpoint/boss state must be rejected by the appropriate validator.
- Typed Press Turn state must survive message/localization changes.
- Reward commit failure must leave both wallet and progression unchanged.

## Capability Confidence Matrix

| Capability | Implemented evidence | Review confidence | Main remaining issue |
| --- | --- | --- | --- |
| Interactive boot | Clean catalog-only command runs | High | Still a capability harness |
| Actor models | Catalog actors and runtime snapshots | Medium | Dual mutable state |
| Resource recalculation | Policy calculation and snapshot transactions | Low | Recalculation result is not applied |
| Stat composition | Typed stat policy preview | Medium | Preview is not broader equipment gameplay |
| Growth and levels | Framework progression mutation | Medium | Battle state maximum can become stale |
| Optional Moon Phase | Clean slice omits it | High | Legacy path still owns its old behavior |
| Generic navigation | Arbitrary IDs and injected policy | High | No route/content authoring surface yet |
| Dungeon traversal | Generic nodes and policy | Medium-high | Save graph validation incomplete |
| Encounter preparation | Explicit trigger and atomic hydration | High | No reusable encounter lifecycle state |
| Field items/skills | Shared typed execution and reservations | High for one actor | Dynamic selection and broader inventory remain |
| Battle actions | Typed commands and shared executor | Medium | Multi-target selection is wrong |
| Typed effects | Definition-driven behavior | High | Host evidence mapper must track new variants |
| Combat math | Bound framework combat policy | High for covered vocabulary | Many defaults remain code-owned |
| Press Turn | Framework-owned turn economy | Medium | Host parses message text |
| Ailment lifecycle | Clean lifecycle port works | Medium-high | Narrow content and single battle shape |
| Passive lifecycle | Typed trigger dispatch works | Medium-high | Narrow passive coverage |
| Enemy AI/tactics | Deterministic typed skill selector | Medium | Minimal strategy only |
| Battle knowledge | Correct player/enemy scope | High | No player-facing hint UI or compendium import yet |
| Battle rewards | Framework calculation/application | Medium | Aggregate commit is not atomic |
| Persistence | Snapshot, policy, JSON proof, restore | Medium-low | Uncommitted and integrity gaps remain |

## One Project Vision

The project should use the following ownership rule consistently:

### Framework owns

- typed content definitions and catalog lookup;
- validation and diagnostics;
- one authoritative runtime state model;
- combat, lifecycle, progression, inventory, economy, party, fusion, and persistence rules;
- commands, policies, assessments, transactions, events, and immutable snapshots;
- no presentation wording, file paths, scenes, or engine objects.

### Content owns

- IDs, stats, skills, effects, affinities, encounters, items, equipment, and policy selection;
- approved configurable parameters;
- no current runtime/save state.

### Host owns

- input devices and menus;
- Godot nodes/scenes or console text;
- asset mapping and animation;
- content text acquisition;
- save-file serialization and storage;
- mapping typed framework results to presentation;
- no combat inference from names, messages, or descriptions.

### Non-negotiable rule

For any concept, there must be one authoritative typed state or rule result. Hosts may project or present it, but must not keep a second mutable copy that needs manual reconciliation.

## Recommended Next Implementation Order

Do not create another roadmap or lettered track. Use the existing phase plan and insert one stabilization checkpoint before Phase 4:

1. **Unify clean actor state authority. Completed and ready after the CodeReview-1 closure.**
   - Decide the canonical actor runtime representation.
   - Remove current-only copy loops.
   - Prove growth, battle, field use, and restore share one state.

2. **Fix dynamic command identity and typed event metadata. Completed and ready in CodeReview-2.**
   - Target, skill, and item selections carry IDs.
   - Press Turn events carry typed state.

3. **Harden Phase 3 restore and commit it separately. Completed and ready in CodeReview-3.**
   - Validate actor identity mappings and saved contexts.
   - Define dungeon host validation and content provenance.
   - Add atomic restore planning.
   - Final verification: focused restore/parity coverage passed 106 tests; the full suite passed 819 tests with no failures or skips; `JRPG.Framework` built with 0 warnings; the solution build retained 98 existing legacy console-host warnings; clean battle, field, save-v5, and Training Annex demos exited successfully; `Data/Jsons` remained unchanged.

4. **Split the clean host by responsibility. Completed and ready in CodeReview-4.**
   - The host remains the session coordinator.
   - Field/navigation presentation moved to `TrainingAnnexFieldPresenter`.
   - Persistence/save/load/restore planning moved to `TrainingAnnexPersistenceController`.
   - Battle reward application moved to `TrainingAnnexBattleRewardApplicator`.
   - Focused tests now cover save host-context snapshot construction, field/dungeon presentation messages, and reward rejection without progression mutation.
   - Remaining cleanup is optional: battle adapter/test-file concentration can be reduced later without blocking Phase 4.
   - Final verification: focused CodeReview-4/parity coverage passed 99 tests; the full suite passed 822 tests with no failures or skips; `JRPG.Framework` built with 0 warnings; the solution build retained 98 existing legacy console-host warnings; clean battle, field, save-v5, and Training Annex demos exited successfully; `Data/Jsons` remained unchanged.

5. **Resume the existing Phase 4 order.**
   - Inventory quantities.
   - Equipment ownership and stat/basic-attack impact.
   - Economy.
   - Shops.
   - Optional hospital/restoration facility.

Phase 4 should consume the corrected single runtime state and dynamic command model. It should not add another parallel state representation.

## Definition Of Done For Future Capabilities

A capability may advance beyond `parallel_partial` only when:

1. The framework owns the rule through typed APIs.
2. The clean consumer uses that rule directly.
3. State has one authority and transactions are atomic.
4. At least one adversarial test changes the sample shape: multiple targets, changed names, changed order, changed maxima, invalid IDs, or failed commit.
5. The host can present the result without parsing framework message text.
6. Save and restore include the capability when it owns persistent state.
7. Godot could issue the same command and consume the same result without console types.
8. Documentation records what remains, not merely that a pass number was reached.

## Bottom Line

The three phases are valuable and largely point in the right direction. They prove that the reusable framework can support an original, non-legacy gameplay slice and that console presentation can sit outside it.

They do not yet prove a production-ready independent game runtime. The immediate problem is not missing breadth; it is state and contract integrity at the joins between progression, battle, presentation, and persistence. Correct those joins now, then continue the existing capability plan without inventing another planning system.
