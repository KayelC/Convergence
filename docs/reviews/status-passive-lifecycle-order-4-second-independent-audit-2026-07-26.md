# Status And Passive Lifecycle Order 4 Second Independent Audit

Date: 26 July 2026

Reviewed revision: `ac09a025`

Verdict: **reopened; two medium runtime corrections and one documentation correction remain**

## Review Method

This audit treated prior reviews and completion summaries as historical records,
not evidence. It traced the current implementation from the public content and
runtime contracts through action execution, lifecycle transactions, encounter
integration, persistence, schemas, tests, and the mechanics, developer, and
technical documentation.

A concern was retained only when it had all four of these properties:

1. an intended invariant established by current code or active documentation;
2. a realistic path through a supported public or encounter extension point;
3. a concrete observable consequence; and
4. source evidence plus a reproducible probe or an existing executable test.

The throwaway probes described below were removed after reproduction. They are
not part of the reviewed tree.

## Findings

### O4-M1: round-end and successful battle-end cancellation can commit lifecycle state

**Invariant.** A cancellation observed before lifecycle commit must leave the
live actor graph unchanged. The active mechanics documentation makes this
atomicity promise for encounter lifecycle ingress.

**Reachable path.** A host lifecycle port can stage an actor mutation and signal
the supplied cancellation token immediately before returning normally. This is
a realistic scene-unload or host-shutdown sequence. The runner checks the token
before calling round-end and successful battle-end lifecycle, but does not
check it again after the port returns and before committing the corresponding
transaction:

- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1579-L1609)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1808-L1825)

The fault-finalization battle-end path already performs the missing post-port
check, demonstrating the intended boundary:
[`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1704-L1717).

**Consequence.** The caller receives `OperationCanceledException`, but staged
round-end or battle-end status/resource cleanup has already become live. The
associated lifecycle events are not necessarily published, so runtime state and
host presentation can disagree.

**Reproduction.** A temporary round-end regression port changed HP from `10` to
`1`, canceled the supplied token, and returned normally. `RunAsync` threw for
cancellation, while the live actor retained HP `1`. Expected rollback to `10`
failed. Source inspection shows the same commit ordering in the normal
battle-end path.

**Correction checkpoint: O4-R33.** Check cancellation after each asynchronous
lifecycle port returns and after its event collection is snapshotted, but before
transaction commit. Add direct round-end and successful battle-end cancellation
regressions and audit every lifecycle commit site for the same ordering.

### O4-M2: a replacement passive dispatcher can commit mutation without activation evidence

**Invariant.** A committed passive mutation must have coherent typed activation
evidence. A non-executed or empty result must not publish unexplained actor
state.

**Reachable path.** `IPassiveTriggerDispatcher` is a supported public extension
point. `ValidatingPassiveTriggerDispatcher` correctly stages the actor graph and
validates every activation that is returned, but an empty activation collection
vacuously passes validation before the staged transaction commits:

- [`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs#L869-L903)
- [`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs#L1022-L1079)

**Consequence.** A buggy replacement dispatcher can change HP, status, passive
counts, or another runtime field and return `PassiveTriggerDispatchResult.Empty`.
The mutation becomes live although encounter events, UI, save diagnostics, and
other consumers receive no activation explaining it.

**Reproduction.** A temporary regression dispatcher set the staged owner's HP
from `100` to `1` and returned `PassiveTriggerDispatchResult.Empty`. Dispatch
returned an empty activation list and the live owner retained HP `1`; the
expected unchanged value `100` failed.

**Correction checkpoint: O4-R34.** Capture the staged graph before replacement
dispatch. Reject graph mutation when the result contains no executed activation,
including results containing only rejected/non-executed activations. Preserve
the existing trusted extension boundary: the framework validates evidence
coherence and atomic publication, but does not pretend it can prove the semantic
correctness of arbitrary host policy code. Add focused empty-evidence and
non-executed-evidence rollback tests.

### O4-D1: ailment combat-modifier composition is implemented but absent from the three audiences

`ProductionCombatRuleset.CreateCombatantProfile` composes every active
ailment's damage-dealt, damage-taken, evasion, critical-chance-taken, and rigid
body modifiers. Multipliers compose multiplicatively with saturating arithmetic,
critical bonuses use saturating addition, and rigid body uses logical OR:
[`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L1128-L1189).
The extreme-value behavior has direct regression coverage in
[`ProductionCombatRulesetTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/ProductionCombatRulesetTests.cs#L737-L775).

The Order 4 mechanics, developer, and technical documents explain authored
ailment modifiers but do not state that the production combat profile consumes
them or define their stacking rules. This is documentation drift, not a runtime
defect, but it prevents a developer or player-facing rules writer from deriving
the actual combat effect from the reviewed documentation.

**Correction checkpoint: O4-R35.** Document the player-facing meaning, host
integration boundary, exact composition order, saturating arithmetic, and rigid
body rule across all three audiences. Add a representative non-extreme
composition test so the contract is not evidenced only by overflow defense.

## Verified Healthy Paths

The fresh source trace found no separate reachable defect in these areas:

- ailment gate, resistance, chance, exclusivity, refresh, and typed rejection;
- exact-instance scheduling at turn start and turn end;
- supplied turn restrictions, recovery, lethal damage, cleanup, and reserve
  suspension policy;
- action, actor-turn, team-phase, round, custom, battle, and permanent lifetime
  handling;
- standard passive targeting, counting, recursion limits, and transactional
  execution;
- battle-start, turn, phase, round, departure, and battle-end lifecycle routing;
- persistence of status lifetimes, passive enablement, activation counts, and
  restore-time definition/exclusivity validation;
- strict schema-v8 status and passive content validation; and
- immutable lifecycle result/event collections.

## Documentation Alignment

The three audience documents agree with the implementation on application,
exclusivity, exact-instance scheduling, turn-end order, reserve suspension,
cleanup causes, passive eligibility/counting, persistence, and staged rollback.
They are reopened only because the combat-modifier rule is missing and the two
atomicity statements are stronger than the current extension/encounter paths.

## Correction Roadmap

| Checkpoint | Scope | Closure evidence |
|---|---|---|
| O4-R33 | Cancellation-before-commit at round end and successful battle end | Focused runner regressions plus lifecycle commit-site audit |
| O4-R34 | Replacement passive mutation requires executed activation evidence | Empty and non-executed evidence rollback tests |
| O4-R35 | Reconcile ailment combat modifiers in all three audiences | Representative composition test and documentation review |
| O4-R36 | Fresh source and documentation closure review | Full solution, builds, demos, links, formatting, and matrix promotion |

Until O4-R36 passes, `status_and_passive_lifecycle` remains `partial`, its three
documentation entries remain `existing_unreviewed`, and Order 5 does not become
the active collaborative subject.

## Verification At Audit Time

- Initial lifecycle/passive/persistence/schema gate: **349 passed**, 0 failed,
  0 skipped.
- Expanded Order 4, encounter, combat-profile, and documentation gate:
  **511 passed**, 0 failed, 0 skipped.
- Full solution: **1,657 passed** (1,477 Framework, 173 DemoHost, 7 content
  validator), 0 failed, 0 skipped.
- Debug and strict Release nonincremental builds: **0 warnings, 0 errors**.
- `dotnet format --verify-no-changes`: passed.
- Active content validation: **6 packs, 36 documents, 98 qualified
  definitions** passed schema, deserialization, semantic, dependency,
  registration, and catalog checks.
- The four noninteractive DemoHost modes exited `0`; scripted Training Annex
  input remained covered by the passing DemoHost suite.
- `git diff --check` passed, and active content/schema files were unchanged.

The two deliberate failing probes were removed before the green gates. Their
test files have no working-tree diff.
