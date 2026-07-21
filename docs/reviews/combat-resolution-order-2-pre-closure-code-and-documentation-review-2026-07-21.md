# Combat Resolution Order 2 Pre-Closure Code And Documentation Review

**Review date:** 21 July 2026

**Reviewed branch:** `main`

**Reviewed revision:** `e26bdc5` (`docs: verify combat resolution closure corrections`)

**Method:** fresh inspection of current Framework source, public contracts,
schema v6, active content, focused tests, and active mechanics/developer/
technical documentation. Earlier reports and their conclusions were not used as
evidence that the current implementation was correct.

## Result

Order 2 should not be closed at this revision.

The supplied combat-resolution policy family is coherent and the central
damage path is healthy. The review nevertheless found two medium-severity
reachable defects at adjacent action and host-integration boundaries, plus one
low-severity authoring-contract mismatch. These are specific correction items,
not evidence that the combat architecture needs another redesign.

No security vulnerability was found. The host-contract finding concerns
silent rule corruption from a buggy integration, not untrusted player input.

The executable maturity record marks every directly affected capability
`partial`: `typed_action_and_effect_execution`, `combat_resolution`,
`turn_economy`, `encounter_orchestration`, `content_validation`,
`authored_schema_contracts`, and `host_contracts`. This is one bounded
correction sequence across shared contracts, not seven independent redesigns.

## Findings

### M1. Duplicate costs for one resource make zero-floor legality depend on author order

**Intended invariant:** all costs charged to one resource form one obligation.
If any component forbids reducing that resource to zero, the aggregate payment
must leave a positive remainder, regardless of document order.

**Reachable path:** schema v6 permits more than one cost entry with the same
`resourceId`, and semantic validation checks each cost without rejecting or
normalizing duplicate resource IDs. `SkillExecutor.ValidateCosts` aggregates
the numeric amount by resource, but tests the aggregate remainder using only
the `CanReduceToZero` flag of the entry currently being visited. Prepared-state
validation repeats the same order-dependent check, and commit applies every
prepared entry.

With SP `10`, these schema-valid costs are accepted:

```text
5 SP, canReduceToZero = false
5 SP, canReduceToZero = true
```

The first entry leaves `5` and passes. The second sees the aggregate remainder
`0`, but its own flag permits zero, so the action executes and spends all SP.
Reversing the two entries rejects the same aggregate cost. For an HP resource,
the same path can defeat an actor even though one authored component explicitly
forbids reaching zero.

**Consequence:** list order silently changes whether a skill is legal and can
bypass an authored nonzero resource floor.

**Evidence:**

- [`SkillExecutor.cs`](../../src/Convergence.Framework/Execution/SkillExecutor.cs),
  `ValidateCosts`, `ValidatePreparedState`, and `CommitCosts`;
- [`SkillSystemContentValidator.cs`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs),
  skill-cost validation;
- [`skills.schema.json`](../../schemas/content/v6/skills.schema.json), the
  unconstrained `costs` array.

**Required correction:** define same-resource semantics once. The recommended
default is to reject duplicate `resourceId` entries during semantic and runtime
preflight, because one `AmountDefinition` may already delegate a composite
formula to a registered handler. If deliberate additive components are to be
supported instead, aggregate both amount and permission, where any `false`
component prevents the total from reaching zero. Either choice requires
order-invariance tests and assessment/execution parity tests.

### M2. Public encounter turn contracts accept impossible shapes and silently reinterpret them

**Intended invariant:** a host-supplied action-consumption value either has one
valid, complete meaning or is rejected at construction/port entry. Invalid
enum values and missing payloads must not become a different legal turn cost.

**Reachable path:** `ActionTurnConsumption` is a public positional record. A
host can construct `Kind = TurnEconomy` with a null `TurnEconomy`, or use record
cloning to combine a kind with an incompatible payload. `TurnEconomyResolution`
also accepts undefined outcomes. `BattleEncounterCommandResult` accepts an
undefined command status or requested outcome without validation.

`ActionTokenTurnEconomy.Apply` sends an incomplete `TurnEconomy` value through
its default branch and consumes a normal action. Undefined turn outcomes also
fall through to normal consumption. `StandardActionTurnEconomy` treats every
non-`None`, non-termination kind as one action. The encounter transition check
only verifies that a non-`None` request advanced the economy, so these silent
reinterpretations pass its validation. An undefined command status is treated
as executed, and an undefined requested outcome may be returned as the battle
outcome.

**Consequence:** a Godot or other host integration bug can change strategic
turn cost or return an undefined battle result without receiving an immediate,
typed rejection. This is a robustness and contract-integrity issue, not a
remote security exploit.

**Evidence:**

- [`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs),
  `ActionTurnConsumption` and `HostMediatedBattleActionCommand`;
- [`ExecutionContracts.cs`](../../src/Convergence.Framework/Execution/ExecutionContracts.cs),
  `TurnEconomyResolution`;
- [`ActionTokenTurnEconomy.cs`](../../src/Convergence.Framework/TurnEconomy/ActionTokenTurnEconomy.cs),
  `Apply` and `ConsumeAction`;
- [`BattleTurnEconomy.cs`](../../src/Convergence.Framework/TurnEconomy/BattleTurnEconomy.cs),
  `StandardActionTurnEconomy.Apply`;
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs),
  `BattleEncounterCommandResult` and `ValidateEconomyTransition`.

**Required correction:** replace freely combinable positional state with
validated construction, preserve validation through record cloning, and reject
undefined command statuses/outcomes at the encounter port boundary. Add focused
tests for each legal shape and each impossible combination.

### L1. Schema and semantic validation disagree about party size zero

**Intended invariant:** independent JSON Schema validation and Framework
semantic validation must agree on the numeric domain of an authored condition.

**Reachable path:** schema v6 permits `party_size.value = 0`, while
`SkillSystemContentValidator` requires the value to be positive. A schema-only
authoring tool therefore accepts a document that the Framework later rejects.

**Consequence:** content authors receive conflicting answers from two published
validation layers. No malformed content reaches a catalog, so this is an
authoring-contract defect rather than a runtime safety issue.

**Evidence:**

- [`shared.schema.json`](../../schemas/content/v6/shared.schema.json),
  `partySizeCondition.value`;
- [`SkillSystemContentValidator.cs`](../../src/Convergence.Framework/Validation/SkillSystemContentValidator.cs),
  `PartySizeConditionDefinition` validation;
- [`ConditionAndTargetResolution.cs`](../../src/Convergence.Framework/Execution/ConditionAndTargetResolution.cs),
  which counts deployed, living same-team participants.

**Required decision:** confirm whether zero means "no deployed living member of
this team" and should remain authorable, or whether the acting context always
requires at least one and schema should require `1`. Then align schema,
semantic validation, mechanics text, and boundary tests.

## Documentation Alignment

The three Order 2 audience documents accurately describe the central combat
pipeline: authored accuracy, Agility-derived evasion, critical eligibility and
chance, affinity outcomes, staged multi-hit mutation, ordered dependencies,
charge consumption, item outcome policy, and Action Token integration.

Two prose inconsistencies were corrected while recording this review:

- the decision record no longer says audience confirmation is pending after
  also saying all three audiences were confirmed;
- it now distinguishes "Framework supplies Split and Unified charge policies"
  from "the standard authored combat factory selects Split." A developer can
  select Unified through a custom combat factory or direct composition, but the
  standard factory does not select both simultaneously.

One owner-facing clarification remains open but is not classified as a defect:
skill assessment currently prepares a single-use cost amount, and execution
rechecks current affordability of that prepared amount rather than recomputing
passive cost modifiers. This agrees with `ISkillExecutor` and the technical
sequence diagram, while the mechanics phrase "rechecked before execution" can
be read either way. The project owner should confirm quote-locking before the
next documentation reconciliation.

## Healthy Paths Verified From Source

- The standard combat ruleset uses Strength for Physical and Magic for other
  damage, Vitality plus defense for mitigation, and explicit configured
  multipliers; it does not hide Luck in hit or critical calculations.
- Authored accuracy, critical chance, instant-defeat chance, ailment chance,
  escape chance, and nested chance conditions share an inclusive `0..100`
  boundary before random input or mutation.
- Every Framework random draw routes through the checked
  `RandomSourceContract`; negotiation and reward-adjacent selectors also use it.
- Multi-hit counts are bounded by schema and semantic validation and by the
  selected standard runtime ceiling before allocation or rolling.
- Each attempted damage hit records immutable accuracy, critical, affinity,
  charge, damage, and applied-resource evidence. Landed hits mutate staged state
  sequentially and later hits stop after defeat.
- Ordered dependencies are earlier-only. `positive_damage` requires a committed
  negative resource delta to that same target; Miss, Null, Repel, Absorb, and
  zero damage cannot trigger the rider.
- Independent and shared-contact secondary damage retain separate affinity,
  power, charge category, and critical resolution. Shared contact only avoids
  the second accuracy roll.
- Split and Unified charge policies reject duplicate grants, store policy
  identity, and consume matching state once after the complete committed outer
  action. Rejected or rolled-back actions do not publish consumption.
- Complete-action aggregation preserves the approved precedence. Items spend
  one normal action by supplied default, with effect-driven item pricing as an
  authored ruleset option.
- Action Token pass precedence is correct: an existing partial token is
  consumed first; only when no partial token exists does a full token become a
  partial token.
- Actor changes and skill costs execute on staged actor state. Ordinary
  rejection and pre-commit exceptions do not publish partial actor mutation.

## Verification Performed

The focused source-backed suite covering combat rules, actions, encounter
orchestration, schema, and semantic validation passed:

```text
480 passed, 0 failed, 0 skipped
```

The review and executable maturity records then passed the focused architecture
and documentation guard:

```text
21 passed, 0 failed, 0 skipped
```

The complete solution passed:

```text
Convergence.Framework.Tests:       1,259 passed
Convergence.DemoHost.Tests:          173 passed
Convergence.ContentValidator.Tests:    7 passed
Total:                              1,439 passed, 0 failed, 0 skipped
```

Additional gates passed:

- Framework Release build: `0` warnings, `0` errors;
- solution Release build: `0` warnings, `0` errors;
- `dotnet format --verify-no-changes`;
- trimming analysis with warnings as errors;
- Framework coverage: `90.53%` lines and `75.62%` branches;
- active content: `6` packs, `36` documents, and `98` qualified definitions;
- noninteractive battle, field, save, and Training Annex demos;
- scripted Training Annex coverage through DemoHost tests;
- Framework boundary, documentation-link, API, and Godot contract tests; and
- `git diff --check`.

The Godot 4.7.1 sample project also built with zero warnings. The local Windows
headless executable produced no output and did not terminate, so it was stopped
after one minute rather than left running. This machine-specific native smoke
is recorded as inconclusive; it is not represented as a passing gate. The
repository's Godot integration contract tests passed in the full suite.

Green existing tests confirm supported ordinary paths remain stable. They do
not negate the three findings: the missing regressions are precisely the
schema-valid duplicate-cost ordering, malformed host-contract shapes, and
schema/semantic party-size boundary described above.

## Closure Decision

The central combat mechanics are suitable to build on, but Order 2 is reopened
until M1, M2, and L1 are resolved and independently rechecked. The earlier
closure reports remain honest evidence for the revisions they reviewed; they
do not override findings in this later source inspection.

The active correction sequence is recorded in the
[Order 2 Pre-Closure Audit Corrections Roadmap](../roadmap/combat-resolution-order-2-pre-closure-audit-corrections-roadmap.md).
