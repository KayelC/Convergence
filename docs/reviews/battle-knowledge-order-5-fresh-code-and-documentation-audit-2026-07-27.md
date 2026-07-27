# Battle Knowledge Order 5 Fresh Code And Documentation Audit

**Date:** 27 July 2026

**Revision reviewed:** `792d3863`

**Branch:** `main`

**Disposition:** correction required before formal Order 5 closure

## Review Method

This audit was performed from current source, executable tests, public contracts,
and the three active Battle Knowledge audience documents. Earlier Order 5 review
reports were not used as behavioral authority or as a finding checklist.

The review traced:

- persistent entity knowledge and sparse analyzed-profile markers;
- encounter knowledge, runtime/entity identity, and combined query precedence;
- typed elemental, ailment, and instant-defeat observations;
- Analyze disclosure and knowledge promotion;
- the aggregate execution-to-knowledge transaction;
- custom-effect execution as a supported public extension boundary;
- automated team-local learning and optional seeds;
- familiarity import through acquisition and Compendium hooks;
- session save validation and restore; and
- DemoHost retention, battle reset, evidence presentation, and persistence.

An actionable finding in this report identifies an intended invariant, a
reachable supported path, a concrete consequence, and reproducible source
evidence. Trusted host-extension misuse is classified as an integration
correctness defect, not as a player-exploitable security vulnerability.

## Findings

### O5-F1 - Medium: custom effect knowledge evidence can escape authoritative execution provenance

#### Intended invariant

Knowledge accepted from an executed action must describe that action's actual
source, acting actor, effect, runtime target, and target entity. Custom handlers
may produce typed evidence, but they must not be able to associate an executed
effect with a different action, actor, or entity.

This is also the contract currently stated by the technical and developer
guidance: execution evidence is keyed by source action/effect and target
identities, and mismatched provenance should reject atomically.

#### Reachable path

1. A host registers the public `ICustomEffectHandler` extension point.
2. Its handler returns an `EffectExecutionResult` containing a publicly
   constructible `BattleKnowledgeObservation`.
3. The observation uses the actual enclosing effect index and runtime target ID,
   but supplies a different `SourceActionId`, `ActorId`, or `TargetEntityId`.
4. `CustomEffectExecutor` overwrites only the outer result's `EffectIndex` and
   `TargetId`.
5. `BattleKnowledgeExecutionTransitionService` checks only the nested effect
   index and runtime target ID. Its request does not carry an authoritative
   action ID, actor ID, or runtime-target-to-entity binding against which the
   remaining fields can be checked.
6. When that runtime target has no earlier encounter identity, the observation
   transition accepts the supplied entity ID and may promote the fact into
   persistent knowledge.

Analyze evidence has the same aggregate-boundary limitation. Its constructor is
internal, but a custom handler can obtain a public `BattleAnalysisResult` by
calling `BattleAnalysisService` for actor or target state other than the
executing context. The aggregate validates only the runtime target ID.

#### Consequence

- Persistent player knowledge can be written under the wrong entity definition.
- An encounter runtime target can be bound to the wrong entity, causing a later
  legitimate observation for the real entity to reject with an identity
  conflict.
- Accepted evidence can name the wrong acting actor or source action.
- DemoHost diagnostic presentation trusts the nested source action and entity
  identity, so the false attribution is externally visible.
- The combat action has already executed when knowledge integration faults. The
  knowledge snapshots remain atomic, but the enclosing battle cannot reinterpret
  the integration rejection as if the action had never happened.

The path requires a defective or dishonest host extension. It is therefore a
real supported-boundary robustness and game-correctness defect, not a network or
player-input security issue.

#### Source evidence

- [`ICustomEffectHandler`](../../src/Convergence.Framework/Execution/ExecutionPolicies.cs#L457-L462)
  is a public supported extension point returning `EffectExecutionResult`.
- [`CustomEffectExecutor`](../../src/Convergence.Framework/Execution/EffectExecutors.cs#L1058-L1072)
  normalizes only the enclosing effect index and runtime target.
- [`BattleKnowledgeObservation`](../../src/Convergence.Framework/Knowledge/BattleKnowledgeObservations.cs#L43-L139)
  carries source action, actor, runtime target, target entity, and effect index.
- [`BattleKnowledgeExecutionTransitionRequest`](../../src/Convergence.Framework/Knowledge/BattleKnowledgeExecutionTransitions.cs#L49-L73)
  carries no authoritative action/actor/target-entity context.
- [`BattleKnowledgeExecutionTransitionService`](../../src/Convergence.Framework/Knowledge/BattleKnowledgeExecutionTransitions.cs#L148-L221)
  validates nested effect index and runtime target only.
- [`TrainingAnnexBattleActionAdapter`](../../samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs#L742-L782)
  presents accepted nested source and entity identities.
- [`BattleKnowledgeExecutionTransitionTests`](../../tests/Convergence.Framework.Tests/Knowledge/BattleKnowledgeExecutionTransitionTests.cs#L143-L225)
  cover effect-index and runtime-target mismatch, but not source-action,
  acting-actor, or target-entity mismatch.

#### Required correction

Add an authoritative execution context to the aggregate transition request, or
an equivalent typed accepted-action evidence envelope, containing:

- expected source action ID;
- expected acting actor ID; and
- authoritative runtime-target-to-entity bindings for every effect target.

Validate every observation and Analyze result against that context before any
lower transition runs. Return stable diagnostics for action, actor, and entity
mismatches, preserve original `Before` snapshots on every rejection, and add
valid and invalid custom-handler regression cases.

Central aggregate validation is preferable to relying only on
`CustomEffectExecutor`, because `EffectExecutionResult` and the knowledge
transition are both public host-facing contracts.

## Documentation Review

### Mechanics audience

[`docs/mechanics/battle-knowledge.md`](../mechanics/battle-knowledge.md) matches
the current intended rules:

- player knowledge persists by entity definition;
- team knowledge is scoped to one encounter unless explicitly seeded;
- temporary defense results cannot overwrite permanent facts;
- misses and ambiguous chance outcomes do not infer hidden resistance tiers;
- Analyze disclosure is policy-controlled, including the approved restricted
  boss profile;
- familiarity import is explicit and optional; and
- encounter knowledge is not part of the save contract.

The finding does not change those player-facing rules. This audience remains
`reviewed`.

### Developer audience

[`docs/developer-guide/battle-knowledge.md`](../developer-guide/battle-knowledge.md)
correctly explains state ownership, query precedence, Analyze composition,
familiarity hooks, AI seeds, and persistence. Its integration example cannot
provide authoritative source/actor/entity context because the current request
contract does not accept it. Its statement that mismatched custom provenance is
rejected is therefore incomplete. This audience returns to
`existing_unreviewed` until O5-F1 is corrected and the example is updated.

### Technical audience

[`docs/technical/battle-knowledge-runtime.md`](../technical/battle-knowledge-runtime.md)
correctly describes snapshot authority, atomic lower transitions, discovery
rules, Analyze, AI sharing, familiarity, and save validation. Lines 58 through
63 overstate the custom-handler guard: current checks prevent effect-index and
runtime-target substitution, but not source-action, acting-actor, or
target-entity substitution. This audience returns to `existing_unreviewed`.

## Verified Behavior Without A Qualifying Finding

The following current paths were traced and found consistent with the active
mechanics:

- snapshot constructors defensively copy public collections;
- duplicate knowledge keys and conflicting runtime/entity identities reject;
- execution-to-knowledge updates return original snapshot references on batch
  rejection;
- elemental contact records effective encounter affinity and promotes authored
  affinity only without temporary influences;
- ailment and instant-defeat knowledge require exact immunity evidence;
- `Almighty` is consistently excluded from stored elemental discovery;
- Analyze stores current resources, stats, and skills only in encounter state;
- disclosed authored defense profiles may persist, including sparse known-normal
  markers;
- restricted fields contain no hidden values;
- ordinary automated teams start fresh, remain isolated, and never write player
  persistent knowledge;
- explicit automated seeds validate team and target identity;
- the supplied selector ignores facts influenced by temporary defenses;
- familiarity import is policy-controlled and uses the canonical persistent
  transition;
- save validation covers duplicate keys, entity references, ailment references,
  enum domains, and the exclusion of encounter-only analysis state; and
- DemoHost starts each battle with fresh encounter knowledge while retaining and
  saving only player persistent knowledge.

## Test Coverage Assessment

Current tests are broad across persistent facts, observations, Analyze,
aggregate atomicity, automated learning, familiarity, save validation, restore,
and DemoHost retention. The missing regression seam is focused:

| Missing case | Required result |
|---|---|
| Observation source action differs from accepted action | Typed rejection; no state change |
| Observation actor differs from executing actor | Typed rejection; no state change |
| Observation entity differs from authoritative target entity | Typed rejection; no state change |
| Analyze actor or entity differs from execution context | Typed rejection; no state change |
| Earlier valid evidence followed by one provenance mismatch | Entire knowledge batch returns original snapshots |
| Valid custom evidence matching the authority envelope | Accepted without changing ordinary effect behavior |

## Correction Roadmap

| Checkpoint | State | Outcome |
|---|---|---|
| O5-R9 | `implemented_pending_review` | Record this fresh source audit and honestly reopen affected status entries. |
| O5-R10 | `open` | Add authoritative action, actor, and target-entity context plus stable mismatch diagnostics. |
| O5-R11 | `open` | Add custom observation and Analyze provenance regressions, including aggregate rollback. |
| O5-R12 | `open` | Reconcile developer and technical guidance, public API evidence, and maturity matrices. |
| O5-R13 | `open` | Re-read corrected source and run the complete release gate before closure is reconsidered. |

## Health Verdict

Battle Knowledge has a strong, coherent core and its approved gameplay rules are
substantially implemented. The persistent/encounter separation, conservative
learning, policy-controlled Analyze, AI lifetime, familiarity, and persistence
boundaries are not being reopened as design questions.

Formal Order 5 closure is nevertheless premature at this revision. O5-F1 is a
bounded but reachable defect in a documented public extension path, and two
integration-facing documents overstate the protection currently provided. The
capability should remain `partial` until O5-R10 through O5-R13 are complete.

## Verification

The review-status changes passed the following gates:

- focused Battle Knowledge, Analyze, persistence, capability-matrix, and
  documentation tests: **46 passed, 0 failed, 0 skipped**;
- full `Convergence.sln` test run: **1,731 passed, 0 failed, 0 skipped**
  (`1,549` Framework, `175` DemoHost, and `7` ContentValidator);
- strict nonincremental Release build with warnings as errors: **0 warnings,
  0 errors**;
- `dotnet format --verify-no-changes`: passed;
- clean battle, field, save, and Training Annex demos: all exited `0`;
- scripted Training Annex interaction: covered by the green DemoHost suite;
- capability/documentation fixture and active-link checks: covered by the green
  Framework suite; and
- `git diff --check`: passed.

These gates establish that the audit and honest status correction do not break
the current product. They do not erase O5-F1; the missing provenance regression
must first fail against the current contract and pass after O5-R10/O5-R11.
