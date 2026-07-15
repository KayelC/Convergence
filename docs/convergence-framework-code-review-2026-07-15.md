# Convergence Framework Code Review

Date: 2026-07-15

Branch: `main`

Reviewed commit: `0109dc8b1078025bcba77f5443facb8c3ad03275`

## Review Mandate

This is a fresh review of the active product after the Phase 8 restructuring and
neutral-vocabulary migration. Findings were derived from current source,
current tests, focused adversarial reproductions, builds, and executable demos.
Archived reviews and historical summaries were not used as implementation
evidence.

The active review surface was:

- `src/Convergence.Framework`
- `samples/Convergence.DemoHost`
- `tests/Convergence.Framework.Tests`
- `tests/Convergence.DemoHost.Tests`
- active content under `content`
- active project, solution, and documentation boundaries

`ArchiveDocs` and the local nested `JRPG` worktree were excluded because neither
is tracked by nor referenced from the active solution.

## Verdict

The product boundary is sound. `Convergence.Framework` is an independent .NET 8
library, the DemoHost is a one-way consumer, active content is catalog-backed,
and the principal runtime services show disciplined use of immutable snapshots,
typed diagnostics, staged execution, and host-neutral contracts.

The framework is suitable for continued development and controlled Godot
integration, but it is not ready for a stable public release yet. One confirmed
high-severity atomicity defect can corrupt live actor resources while reporting
rejection. Five medium-severity defects remain at assess/execute, restore,
cancellation, encounter-port, and arithmetic boundaries. A sixth medium issue
affects fusion rank resolution at extreme but currently accepted values.

The current automated suite is strong, but its passing result does not cover
these state-transition and hostile-domain branches. Each primary finding below
was confirmed from current code; seven were additionally reproduced with
temporary focused tests that were removed after verification.

## Correction Log

### H1 corrected on 2026-07-15

Status: Corrected after reviewed commit `0109dc8`

The resource recalculation boundary now rejects empty resource IDs when a
`RuntimeResourceSnapshot` is constructed, rejects null or duplicate entries in
`ResourceRecalculationResult`, and prepares every `BattleResourceState` before
clearing or replacing the actor's live resource dictionary.

Four permanent regressions cover:

- empty resource ID rejection;
- duplicate recalculation-result rejection;
- actor-incompatible recalculation rejection with all resources preserved;
- direct defensive-boundary validation proving a malformed later replacement
  cannot clear or partially replace live resources.

Correction verification:

- focused runtime snapshot tests: 20 passed;
- full solution: 732 passed, 0 failed, 0 skipped;
- nonincremental solution build: 0 warnings and 0 errors;
- battle, field, save, and Training Annex demos: successful;
- touched-file formatting verification: successful;
- active framework forbidden-reference search: clean;
- active content: unchanged.

M2 remains a separate arithmetic finding. The H1 correction intentionally does
not change `AddResource` overflow behavior.

### M1 corrected on 2026-07-15

Status: Corrected after reviewed commit `0109dc8`

Prepared skill execution now distinguishes immutable assessment decisions from
mutable execution prerequisites:

- the resolved cost amounts remain exactly those produced by assessment, so
  formula handlers and rule modifiers are not evaluated twice;
- prepared target IDs remain fixed, so random target policies are not invoked a
  second time;
- current resources are rechecked against the prepared costs immediately before
  staging mutation;
- the authored `CanReduceToZero` rule is preserved during that recheck;
- prepared targets are rechecked for current activity, relation, life state,
  selection shape, and target-count eligibility;
- stale rejection applies no effects, commits no cost, and consumes no battle
  turn through the action facade.

Four permanent regressions cover stale resource depletion, the nonzero resource
floor, a target becoming ineligible, and battle-action facade behavior. Existing
formula-count and random-target-count tests confirm assessment still resolves
each decision exactly once.

Correction verification:

- focused skill and battle-action tests: 57 passed;
- full solution: 736 passed, 0 failed, 0 skipped;
- nonincremental solution build: 0 warnings and 0 errors;
- battle, field, save, and Training Annex demos: successful;
- touched-file formatting verification: successful;
- active content: unchanged.

### M2 corrected on 2026-07-15

Status: Corrected after reviewed commit `0109dc8`

Public resource addition now routes through `CombatArithmetic.TryAdd` before a
new resource value reaches `SetResource`:

- decimal arithmetic overflow becomes a typed `ResourceValueOutOfRange`
  rejection at the affected resource path;
- representable but invalid extreme values continue through the existing
  resource-domain validation and receive the same stable rejection;
- both rejection paths retain identical before/after snapshots and leave the
  live actor unchanged.

Two permanent theory cases cover positive and negative decimal extremes.

Correction verification:

- focused runtime snapshot tests: 22 passed;
- full solution: 738 passed, 0 failed, 0 skipped;
- nonincremental solution build: 0 warnings and 0 errors;
- battle, field, save, and Training Annex demos: successful;
- touched-file formatting verification: successful;
- active framework forbidden-reference search: clean;
- active content: unchanged.

## Findings

### H1. Rejected resource recalculation can partially mutate the live actor

Severity: High

Affected boundary: runtime mutation and restore/growth integration

Correction status: Corrected on 2026-07-15; original finding retained below as
review evidence.

`RuntimeResourceTransactionService.ApplyRecalculation` catches an
`ArgumentException` from resource replacement and returns a rejected result.
However, `RuntimeActorState.ReplaceResources` clears the existing resource map
before every replacement resource has been constructed and validated.

Relevant code:

- `src/Convergence.Framework/Runtime/RuntimeStateSnapshots.cs:610`
- `src/Convergence.Framework/Execution/BattleRuntimeState.cs:953`

A malformed public `ResourceRecalculationResult` containing one valid resource
followed by an invalid resource ID therefore produces this sequence:

1. The original HP/SP collection is cleared.
2. The valid replacement is inserted.
3. Constructing the invalid replacement throws.
4. The service reports `Rejected`.
5. The actor remains partially mutated and may have lost its SP resource.

The focused reproduction started with HP `72/120` and SP `18/44`. The operation
reported rejection but left only HP `1/1` on the live actor.

Required correction:

- Materialize and validate the complete replacement collection before clearing
  live state.
- Commit the prepared resource map only after every replacement succeeds.
- Validate resource IDs when public recalculation results are constructed, while
  retaining defensive validation at the mutation boundary.
- Add a regression proving rejection preserves byte-for-byte equivalent actor
  state.

### M1. Prepared skill assessments can become stale before execution

Severity: Medium

Affected boundary: `Assess`/`Execute` parity

Correction status: Corrected on 2026-07-15; original finding retained below as
review evidence.

`SkillExecutor.Execute(request, assessment)` verifies assessment ownership,
request equivalence, target rebinding, and one-use consumption. It does not
revalidate current cost affordability after state may have changed.

Relevant code:

- `src/Convergence.Framework/Execution/SkillExecutor.cs:40`
- `src/Convergence.Framework/Execution/SkillExecutor.cs:74`
- `src/Convergence.Framework/Execution/SkillExecutor.cs:290`

The focused reproduction assessed a 10-SP skill while the actor had 10 SP,
changed the actor to 0 SP, and then executed the prepared assessment. Execution
succeeded, damaged the target, and committed no effective cost because the
staged subtraction clamped at zero.

This matters to a frame-based host: assessment may drive a menu or targeting UI,
then execution may occur after another transition changes actor state.

Required correction:

- Keep the prepared target IDs and random target decision stable.
- Revalidate mutable execution preconditions, especially resource costs, against
  current state immediately before staging mutation.
- Return a stable stale-assessment or insufficient-resource diagnostic without
  consuming cost or applying effects.
- Consider an actor-state revision or explicit reservation token if more
  assess/execute state dependencies are introduced.

### M2. Public resource addition can escape through decimal overflow

Correction status: Corrected on 2026-07-15; the original finding is retained
below as review evidence.

Severity: Medium

Affected boundary: runtime numeric input

`RuntimeResourceTransactionService.AddResource` evaluates
`resource.Current + delta` before `SetResource` can validate the result.
`decimal.MaxValue + 1m` throws `OverflowException` rather than producing a typed
rejection.

Relevant code:

- `src/Convergence.Framework/Runtime/RuntimeStateSnapshots.cs:569`

Normal authored gameplay values are far smaller, but this is a public framework
boundary and should remain total for rejected numeric input.

Required correction:

- Perform checked addition through the framework numeric-domain helpers.
- Convert overflow into a stable `ResourceValueOutOfRange` or dedicated numeric
  diagnostic.
- Prove both positive and negative extreme inputs preserve live state.

### M3. Undefined enum values pass actor and save boundaries

Severity: Medium

Affected boundary: actor creation, snapshot restoration, and persisted state

.NET permits casting arbitrary integers to enums. Current actor snapshot
integrity validates identifiers, uniqueness, stat ranges, durations, and several
cross-record rules, but it does not consistently reject undefined values for
deployment, charge kind, shield kind, affinity values, analysis layers,
knowledge values, or checkpoint kinds.

Relevant code:

- `src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs:48`
- `src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs:176`
- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:130`
- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:187`
- `src/Convergence.Framework/Encounters/CatalogBattleActorFactory.cs:9`

The focused reproduction restored an actor snapshot with deployment value `999`.
The integrity validator returned no diagnostics. Catalog actor creation likewise
accepts a caller-supplied deployment enum without a domain check.

Required correction:

- Centralize enum-domain validation rather than scattering ad hoc checks.
- Enforce it in public constructors/requests and again at restore boundaries.
- Cover actor status enums, affinity/knowledge enums, checkpoint kinds, and
  creation-request deployment.
- Add tests for every public enum-bearing snapshot family using undefined values.

### M4. Negotiation cancellation is reported as gameplay failure or refusal

Severity: Medium

Affected boundary: host cancellation and negotiation outcomes

The negotiation model defines a `Cancelled` outcome, but an ordinary answer-menu
cancellation returns `Failure` with a cancelled reason. Demand cancellation is
combined with explicit refusal and returned as `CurrencyRefused` or
`ItemRefused`.

Relevant code:

- `src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs:358`
- `src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs:428`
- `src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs:659`
- `src/Convergence.Framework/Encounters/BattleNegotiationAndRewards.cs:694`

The focused answer-selection reproduction expected `Cancelled` but received
`Failure`. The method also does not check an already-cancelled token before
running gate or familiar-target logic.

Required correction:

- Return `NegotiationOutcomeKind.Cancelled` for ordinary host/menu cancellation.
- Keep explicit demand refusal as a gameplay refusal.
- Throw `OperationCanceledException` for token cancellation, beginning at method
  entry and at mutation or host-call boundaries.
- Test cancellation before the session, during answer selection, and during both
  demand types.

### M5. Encounter port exceptions escape the typed fault boundary

Severity: Medium

Affected boundary: encounter orchestration and host integration

Lifecycle callback failures are translated into a faulted
`BattleEncounterResult`, but exceptions from several other injected ports are
not contained consistently. These include initiative, participant
synchronization, turn-economy creation/state changes, turn handling, and
completion evaluation.

Relevant code:

- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:539`
- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:625`
- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:708`
- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:876`
- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs:884`

A focused initiative policy that threw `InvalidOperationException` escaped the
runner instead of returning `BattleEncounterOutcome.Faulted`. A Godot host would
therefore lose the normal result, ordered fault/end events, and predictable
cleanup boundary.

Required correction:

- Add centralized non-cancellation exception containment around injected ports.
- Preserve `OperationCanceledException` when the supplied token is cancelled.
- Return stable fault codes identifying the failed port.
- Ensure battle-end lifecycle and result snapshot behavior are explicit for each
  fault stage.

### M6. Fusion rank-offset arithmetic can wrap for accepted ranks

Severity: Medium

Affected boundary: fusion result resolution

Fusion participant rank accepts any nonnegative `int`, while rank-offset
resolution computes the average and offset with unchecked `int` arithmetic.

Relevant code:

- `src/Convergence.Framework/Fusion/FusionRuntimeServices.cs:475`

At extreme accepted ranks, `(a.Rank + b.Rank) / 2` and the subsequent offset can
wrap negative. The focused reproduction selected the lowest race member when the
mathematically correct result should have selected the highest member.

Required correction:

- Calculate rank aggregates using `long` or checked domain helpers.
- Either define and validate a supported rank maximum or return a typed overflow
  diagnostic.
- Test parent-order symmetry and both positive and negative offsets at bounds.

### L1. DemoHost file loading does not confine paths to its content root

Severity: Low

Affected boundary: sample host safety

`FileContentPackSource` combines the configured root with caller-supplied logical
paths and reads the result without resolving and checking root containment.

Relevant code:

- `samples/Convergence.DemoHost/Infrastructure/FrameworkHostAdapters.cs:15`

Current DemoHost commands supply constant trusted paths, so this is not an
exploitable route in the shipped command surface. It is still a poor integration
example for developers who may reuse the adapter with externally selected pack
paths.

Required correction:

- Resolve the root and requested path with `Path.GetFullPath`.
- Reject paths outside the normalized root.
- Preserve canonical logical paths in framework requests and diagnostics.
- Test rooted paths, `..` traversal, mixed separators, and valid nested files.

### L2. The active solution does not currently satisfy a formatting gate

Severity: Low

Affected boundary: maintainability and release automation

`dotnet format Convergence.sln --no-restore --verify-no-changes` reports 594
whitespace diagnostics. The largest concentration is the Training Annex sample
host, followed by `EffectExecutors.cs` and `ItemExecutor.cs`.

Diagnostic distribution:

- 542 in `samples/Convergence.DemoHost/Hosts/TrainingAnnex/CleanTrainingAnnexPlayHost.cs`
- 31 in `src/Convergence.Framework/Execution/EffectExecutors.cs`
- 17 in `src/Convergence.Framework/Execution/ItemExecutor.cs`
- 4 across test files

There is also no active `.github` CI directory, and the project files do not
promote compiler warnings to errors. Local builds currently produce zero
warnings, so this is a release-process gap rather than a present compilation
defect.

Required correction:

- Apply one isolated formatter-only change after behavioral corrections.
- Add a reproducible CI gate for restore, test, build, format verification,
  terminology checks, and DemoHost smoke runs.
- Enable warnings-as-errors in CI, even if local developer builds remain less
  strict.

## Verified Strengths

The review also confirmed these important properties in current code:

- The active solution has a one-way dependency from DemoHost to Framework.
- Framework targets .NET 8, uses C# 12, is dependency-free, and is not coupled to
  package distribution.
- Active framework source contains no console, filesystem, Godot, Newtonsoft,
  legacy database, legacy actor, legacy DTO, `IGameIO`, or adapter dependencies.
- Content loading uses strict typed DTOs, canonical qualified IDs, exact pack
  dependencies, graph validation, and explicit registration snapshots.
- Ruleset binding is explicit and diagnostic; runtime consumers do not silently
  fall back to legacy or demo rules.
- Actor/effect execution generally stages mutations and commits only after
  ordered effects complete successfully.
- Lifecycle custom-handler work is transaction-wrapped.
- Encounter requests reject duplicate runtime IDs, initiative must return an
  exact team permutation, and command/free-action limits protect phase liveness.
- Encounter results contain immutable participant snapshots rather than live
  mutable actor references.
- Combat arithmetic uses bounded helpers in the main damage, reward, and
  negotiation aggregates.
- Save validation has substantial referential and roster invariants.
- Fusion previews require validated inheritance selections and preserve preview
  authority.
- Compendium knowledge import validates duplicate entries before dictionary
  construction.
- Action Token passing follows the approved strategy: consume an existing
  partial token first; only convert a full token when no partial token exists.
- Definition and result collections are generally copied into read-only
  snapshots.

## Verification Results

| Gate | Result |
|---|---|
| `dotnet test Convergence.sln --no-restore` | 728 passed, 0 failed, 0 skipped |
| Framework tests | 576 passed |
| DemoHost tests | 152 passed |
| Framework nonincremental build | 0 warnings, 0 errors |
| Solution nonincremental build | 0 warnings, 0 errors |
| `--clean-battle-demo` | Success; player-team victory |
| `--clean-field-demo` | Success |
| `--clean-save-demo` | Success; save contract v7; 0 validation diagnostics |
| `--clean-training-annex-demo` | Success; victory; save validation succeeded |
| Scripted `--clean-training-annex-play` exit | Success without hanging |
| Framework line coverage | 90.78% |
| Framework branch coverage | 74.21% |
| DemoHost line coverage | 86.18% |
| DemoHost branch coverage | 65.86% |
| `git diff --check` | Clean before this report |
| Framework forbidden-reference scan | No active boundary leak found |
| Active solution archive-reference scan | No archive project/reference found |
| `dotnet format --verify-no-changes` | Failed with 594 whitespace diagnostics |
| Active CI configuration | None found |

High line coverage and a green suite are meaningful strengths, but the focused
reproductions demonstrate why branch, transition, and hostile-domain tests are
still required at every public mutation boundary.

## Recommended Correction Order

This is an ordered correction set, not a new feature roadmap:

1. Resource boundary: H1 and M2 corrected.
2. Prepared assessment freshness: M1 corrected.
3. Enum and persisted-domain validation: M3.
4. Negotiation cancellation semantics: M4.
5. Encounter injected-port containment: M5.
6. Fusion rank arithmetic: M6.
7. DemoHost root confinement, formatting, and CI: L1 and L2.

The H1 atomicity, M1 stale-assessment, and M2 resource-overflow defects are
corrected. M3 through M5 should be resolved before a public Godot integration
is described as production-ready. M6 should be resolved before arbitrary
developer-authored fusion ranks are treated as supported.

## Readiness Decision

- Continue framework development: Yes.
- Continue controlled Godot prototyping: Yes, with trusted content and known
  integration inputs.
- Call the architecture stable enough to build on: Yes.
- Publish a stable production release: No, not before the remaining medium
  findings are fixed and regression-tested.
- Reopen or restore the legacy product: No. None of these findings invalidates
  the clean product boundary.

This review changed no production behavior. Its only repository artifacts are
this report and its documentation index link.
