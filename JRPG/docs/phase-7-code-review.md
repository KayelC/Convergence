# Phase 7 Fresh Code Review And Readiness

Review date: 2026-07-11

Branch: `track-12-recovery`

Audited range: `d2e7922..d99a62d`

Audited implementation: Phase 7-30 through 7-35 and CodeReview7-1 through CodeReview7-5

## Review Method

This is a new source review of the current code. The previous Phase 7 report was not used as evidence for the verdict.

The review traced:

- clean fusion recipe mapping and result resolution;
- inheritance planning, selection, previews, accidents, and mutation;
- policy registration and policy context propagation;
- fusion transaction preparation, stale-state protection, actor construction, stock transitions, and rejection behavior;
- Compendium registration, recall, pricing, actor restoration, and familiar-entity knowledge import;
- party/stock identity rules and save validation used by Phase 7;
- Training Annex host integration;
- direct framework tests, host tests, architecture guards, and missing negative cases.

The Phase 7 range changes 48 files with 9,844 insertions and 456 deletions. The current branch and upstream were synchronized at `d99a62d` when this review began.

## Verdict

**Phase 7 is implemented and review-closed for Phase 8. All four medium findings and both low-priority findings are resolved.**

No high-severity issue was found. Fusion commits are atomic at the immutable snapshot boundary, stale preparations are rejected, Compendium registration and recall reject malformed entries before transaction work, and framework/host dependencies remain correctly separated.

The fresh audit found four medium-severity contract or persistence gaps that were not covered by the tests at review time. The rank-offset, preview-authority, Persona stock-capacity, and duplicate-knowledge findings are now resolved. The lower-priority `AddPartyMember` runtime-identity and Compendium pricing-policy gaps are also resolved. No source finding from this review remains deferred.

## Findings

### Medium, resolved 2026-07-12: Fusion previews could be constructed from unvalidated skill IDs

Source:

- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs:1007`
- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs:1058`
- `Host/CleanConsole/TrainingAnnex/TrainingAnnexFusionController.cs:664`
- `JRPG.Framework/Logic/Fusion/FusionTransactionServices.cs:624`

`FusionPreviewRequest` accepts a raw `IEnumerable<ContentId>`. `FusionPreviewService.CreatePreview(...)` copies those IDs into the preview without checking selection limits, duplicates, candidate membership, inheritance eligibility, or already-known status.

The Training Annex host avoids the bug by rebuilding a second `FusionInheritancePlan`, validating it, and only then calling the preview service. The transaction service also rechecks the selected IDs before preparation. That protects the current console path and final commits, but it does not protect the public framework preview API.

Impact:

- another host can display an impossible preview even though the final transaction would reject it;
- a Godot adapter would need to duplicate framework planning logic, as the Training Annex host currently does;
- preview and commit do not share one unforgeable selection boundary.

Required correction:

- make preview creation consume `ValidatedFusionInheritanceSelection`, or expose one framework planning/selection service that returns the validated token and preview together;
- remove host reconstruction of candidate selection rules;
- add direct tests for over-limit, duplicate, unknown, already-known, and ineligible preview requests.

Resolution:

- `FusionPlanningResult` now retains the authoritative evaluated inheritance plan with its final slot limit;
- `IFusionPlanningService.ValidateInheritanceSelection(...)` validates host selections against that retained plan and returns the existing opaque `ValidatedFusionInheritanceSelection` token;
- `FusionPreviewRequest` has one public constructor and requires that token, so raw skill-ID collections are no longer a public preview input;
- `FusionPreviewService` and `FusionTransactionService` share the same plan-membership rule, rejecting a token whose receiving entity, slot limit, duplicate state, or selected IDs do not match the requested plan;
- the Training Annex host no longer reconstructs candidates or invokes the low-level inheritance validator itself;
- direct regressions prove an impossible ID cannot produce a token, a token from another plan cannot produce a preview, and the public request surface has no raw-ID constructor. Existing inheritance-selection tests continue to cover over-limit, duplicate, unknown, already-known, and ineligible diagnostics.

### Medium, resolved 2026-07-12: Structured rank-offset previews depended on caller parent order

Source:

- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs:460`
- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs:824`
- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs:1084`

Recipe matching is intentionally parent-order neutral. However, `ResolveAuthoredRankOffset(...)` does not identify a transformed parent. `FusionPlanningService` therefore falls back to `request.FirstParent` as the preview baseline, and `FusionPreviewService` overlays that first parent's stats onto the ranked result.

Reversing the same valid parent pair can therefore produce the same resolved entity but different preview and committed stats. The current tests reverse parent order only at result-resolution level; they do not compare planning, preview, or transaction state.

Impact:

- a host's selection order can silently change the resulting actor state;
- the typed recipe appears symmetric while its runtime state is not;
- the Training Annex fixed ordering hides the defect.

Required correction:

- define one explicit structured rank-offset contract: either create the ranked entity from catalog defaults, or identify the transformed parent through a typed selector/policy result;
- never infer the transformed parent from argument position;
- prove forward and reversed parent order produce the same result identity and state where the recipe is symmetric.

Resolution:

- `FusionPlanningService` now carries parent state only when the resolved result explicitly supplies `TransformedParent`;
- neutral structured `rank_offset` recipes start from the resolved catalog entity's authored stats, making their preview and committed state parent-order neutral;
- policy-driven compatibility operations may still preserve a specifically identified transformed parent;
- direct forward/reversed transaction coverage proves identical catalog stats, while a separate policy regression proves explicit transformed-parent state still works.

### Medium, resolved 2026-07-12: Save validation enforced Demon stock capacity but not Persona stock capacity

Source:

- `JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs:407`
- `JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs:429`
- `JRPG.Framework/Logic/Runtime/PartyStockTransitions.cs:243`
- `JRPG.Framework/Logic/Fusion/CompendiumRuntimeServices.cs:340`

`PartyStockTransitionService.AddPersonaToStock(...)` applies the injected stock-capacity policy, and Compendium recall explicitly supports Persona stock. `RuntimeSaveValidator`, however, checks only `DemonStock.Count` against that policy.

A save can therefore pass validation with an over-capacity Persona stock even though the live transition service would reject creating that state.

Required correction:

- add a stable Persona-stock capacity diagnostic and path;
- evaluate both stock families through the same injected policy;
- add default and custom-capacity tests for Persona stock as well as Demon stock.

Resolution:

- `RuntimeSaveValidationCode.PersonaStockCapacityExceeded` was appended without renumbering existing diagnostic values;
- `RuntimeSaveValidator` now obtains the owner-level capacity once from its injected `IStockCapacityPolicy` and applies it independently to Demon and Persona stock;
- over-capacity Persona saves report the stable path `$.partyStock.personaStock`, while the existing Demon path and diagnostic remain unchanged;
- active form remains separate from Persona stock capacity, matching `PartyStockTransitionService.AddPersonaToStock(...)`;
- direct default-policy coverage rejects four Persona entries at owner level 1, while an injected capacity of four validates the same otherwise-valid save. Existing Demon default/custom coverage remains green.

### Medium, resolved 2026-07-12: Duplicate persisted knowledge could validate and then crash familiar import

Source:

- `JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs:92`
- `JRPG.Framework/Logic/Runtime/RuntimePersistenceSnapshots.cs:778`
- `JRPG.Framework/Logic/Fusion/CompendiumRuntimeServices.cs:703`

`RuntimeKnowledgeSnapshot` permits duplicate entries. `RuntimeSaveValidator.ValidateKnowledge(...)` verifies target and ailment references but does not verify uniqueness by:

- `(entityId, element)`;
- `(entityId, ailmentId)`;
- `(entityId, instantDeathChannel)`.

`FamiliarEntityKnowledgeService.Import(...)` immediately converts each collection with `ToDictionary(...)`. Duplicate keys therefore throw `ArgumentException` even when the snapshot previously passed save validation.

Impact:

- malformed or independently authored host save data can pass the advertised validation boundary and fail later as an exception;
- familiar knowledge import does not return its typed diagnostic result for this case.

Required correction:

- reject duplicate knowledge keys with stable validation codes and indexed paths, or define and apply an explicit deterministic merge policy before validation;
- add host-owned JSON corruption tests and direct import tests for all three knowledge channels.

Resolution:

- `RuntimeKnowledgeIntegrity` is the single internal authority for duplicate keys across elemental, ailment, and instant-death knowledge collections;
- `RuntimeSaveValidator` reports distinct stable codes with indexed paths for every duplicate occurrence after the first;
- malformed host-owned JSON remains deserializable into immutable snapshots, then fails the advertised framework validation boundary with all three actionable diagnostics;
- `FamiliarEntityKnowledgeService.Import(...)` runs the same integrity check before dictionary construction and returns an unchanged typed rejection with channel-specific codes and source indices;
- no merge policy is inferred: conflicting or identical duplicate entries are both rejected, preserving one unambiguous authored value per knowledge key;
- direct snapshot, host-owned JSON, and importer regressions cover all three channels and prove no exception or state mutation occurs.

### Low, resolved 2026-07-12: The global runtime-ID rule was not applied by `AddPartyMember`

Source:

- `JRPG.Framework/Logic/Runtime/RuntimePartyStockIdentityRules.cs:18`
- `JRPG.Framework/Logic/Runtime/PartyStockTransitions.cs:172`

Phase 7 added a centralized cross-role runtime-ID rule and originally applied it only when adding or replacing Demon and Persona stock entries. `AddPartyMember(...)` checked only active and reserve party collections.

That allowed a caller-authored party member to reuse an ID already assigned to the active form, Persona stock, or Demon stock. A later save validation could reject the graph, but the transition service should not construct it in the first place.

Resolution:

- `AddPartyMember(...)` now preserves `DuplicateOwned` for an ID already present in active or reserve party membership;
- it preserves the exact owner-reference plus active-party representation when an active slot is open;
- it consults `RuntimePartyStockIdentityRules` for every other collision and returns `RuntimeInstanceIdInUse` with an unchanged before/after snapshot when the active form, Persona stock, or Demon stock already uses the ID;
- direct party addition cannot manufacture the intentional active-plus-owned Demon overlap;
- `SummonDemon(...)` remains the explicit operation that activates an already-owned Demon while preserving its Demon-stock reference;
- focused regressions cover every cross-role collision and prove the deliberate summon overlap remains valid.

### Low, resolved 2026-07-12: Compendium recall pricing was a fixed framework formula and currency name

Source:

- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs:1229`
- `JRPG.Framework/Logic/Fusion/FusionRuntimeServices.cs:1237`

`CompendiumService` fixed the recall formula to base price plus level, stat, and skill terms, and its diagnostics named Macca. The runtime orchestration was injectable around actor, stock, resource, and economy services, but recall pricing itself was not a policy.

Resolution:

- `ICompendiumRecallPricingPolicy` now owns recall availability and cost calculation;
- `CompendiumService` has no hidden pricing default: without a policy, registration remains available but recall returns typed `RecallUnavailable` diagnostics;
- `FixedCompendiumRecallPricingPolicy(0)` supports free recall and `CompendiumRuntimeService` skips the payment port entirely for zero-cost recalls;
- `LinearCompendiumRecallPricingPolicy` accepts host-selected base, level, stat-point, and skill factors rather than embedding one formula;
- legacy Cathedral and Training Annex composition roots explicitly select the former `2000 + level * 100 + stat sum * 50 + skill count * 200` behavior, preserving their current output and costs;
- framework Compendium APIs and diagnostics use generic balance, currency, and payment terminology; Macca labels remain in the legacy/console presentation layer;
- focused tests use alternate coefficients to prove the policy inputs, not old constants, determine the result, and an architecture guard rejects host currency terminology in framework Compendium sources.

## Source-Derived Strengths

### Fusion content and resolution

- Catalog recipes retain typed entity/race parent selectors and structured result operations.
- Recipe matching handles parent order, mixed selector types, selector specificity, and structured-result authority.
- Missing accident, mutation, result, or compatibility policies fail with typed diagnostics before mutation.
- Framework fusion code contains no Moon Phase, named catalyst, display-name, description, or legacy DTO inference.

### Strategies and inheritance

- Inheritance precedence is typed and keeps passive skills in the passive group.
- Slot, sacrifice, accident, mutation, result, combination, and compatibility behavior are explicit injected policies.
- `FusionPolicyContext` is immutable and is retained by planning and accident mutation.
- Mutation uses authored family/tier metadata rather than skill names.

### Transactions

- `PreparedFusionTransaction` and successful inheritance tokens have internal constructors.
- Preparation simulates immutable party/stock transitions and constructs no actor.
- Commit uses reference-identity stale-state checks against immutable snapshots.
- Actor creation failure, stale preparation, mismatched factory output, duplicate result, duplicate participant, identity collision, and stock rejection publish no applied state.
- Demon and Persona transaction paths use typed stock operations.
- Learned and equipped skill ordering is retained separately.

### Compendium

- Registration snapshots progression, stats, learned/equipped skills, and display data from the runtime actor.
- Shared entry-integrity validation runs at save, registration, and recall boundaries.
- Recall checks integrity and runtime identity before stock simulation, actor creation, pricing, or wallet mutation.
- Actor restoration uses catalog definitions and the injected resource-growth policy.
- Familiar defense import is explicit and updates player-persistent knowledge without modifying encounter-local AI knowledge.

### Host boundary

- Framework APIs expose no console, filesystem, serializer, Godot, Newtonsoft, `Database`, `Combatant`, `Persona`, or legacy DTO types.
- Legacy Moon/catalyst behavior is isolated in `Logic/Fusion/LegacyFusionStrategyPolicies.cs` under the console host.
- Training Annex uses original qualified catalog content and immutable framework results.

## Test Quality Assessment

The tests are not merely asserting console text produced by the same method:

- strategy tests use custom repositories, policy registries, contexts, and random sources;
- transaction tests use real party/stock transitions plus failure and mismatched actor factories;
- Compendium tests track actor-factory and economy invocation counts to prove rejection order;
- persistence tests mutate host-owned JSON and validate stable framework diagnostics;
- architecture tests scan exported API types and framework source;
- host tests verify complete state before and after cancellation, rejection, save/load, fusion, and recall.

No skipped tests or obvious always-true assertions were found in the reviewed Phase 7 suites.

Every medium- and low-priority scenario identified by this review is now covered. No source finding remains open.

## Verification

| Gate | Current result |
| --- | --- |
| Focused rank-offset correction tests | `2/2` passed |
| Focused preview-authority correction tests | `3/3` passed |
| Focused Persona stock-capacity correction test | `1/1` passed |
| Focused persistence and party/stock tests | `51/51` passed |
| Focused duplicate-knowledge correction tests | `3/3` passed |
| Focused persistence, Compendium, and host-save tests | `53/53` passed |
| Focused party/stock transition tests after global-ID correction | `23/23` passed |
| Focused Compendium policy, host, boundary, and legacy compatibility tests | `143/143` passed |
| Focused fusion and compatibility regression tests | `95/95` passed |
| Full solution tests | `951/951` passed, `0` skipped |
| Nonincremental framework build | `0` warnings, `0` errors |
| Nonincremental solution build | `98` protected legacy-host warnings, `0` errors |
| Clean battle demo | passed |
| Clean field demo | passed |
| Clean save demo | passed, save contract v5 |
| Training Annex runtime demo | passed |
| Framework forbidden-reference checks | passed |
| Protected `Data/Jsons` changes during review | none |

Green verification establishes that supported paths remain stable. It does not invalidate the source findings above because those paths are currently absent from the test matrix.

## Phase 8 Gate

All four medium findings and both low-priority findings from the fresh Phase 7 review are resolved. Phase 8 may begin.

Review closure does not imply `clean_parity`: all affected capabilities remain `parallel_partial`, and every `removalAuthorized` flag remains `false`.
