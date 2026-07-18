# Order 1 Code And Documentation Review

**Review date:** 18 July 2026  
**Reviewed revision:** `6fef7ea`  
**Capability:** `typed_action_and_effect_execution`, including the joined
stat-modifier policy scope  
**Review method:** fresh source, test, schema, sample-host, and active-document
inspection; earlier reports were not accepted as implementation evidence

## Verdict

Order 1 is architecturally sound and well tested, but it is not ready for
formal promotion to `complete` or `reviewed` yet.

The review found three reachable implementation defects and three active
documentation discrepancies. None affects the current default Training Annex
path, which explains why the complete test suite remains green. They do affect
valid public composition paths or authored conditions and therefore need
correction before Order 1 closes.

No blocking repository failure, warning, skipped test, mutable-result leak,
random-target double resolution, skill-cost mutation leak, item reservation
quantity error, automated skill authorization bypass, or timed-modifier
boundary regression was found.

## Implementation Findings

### M1. Configurable modifier bounds can create combat-invalid state

**Invariant:** a successfully bound stat-modifier policy must not produce state
that the standard combat runtime rejects merely because the authored bounds are
valid for that policy.

**Reachable path:**

1. Author a `persistent_staged` or `timed_contribution` ruleset with bounds such
   as `-5..+5`.
2. The bounded factory accepts the integer parameters and both supplied policy
   constructors accept every negative minimum and positive maximum.
3. Apply enough positive state to resolve stage `+5`.
4. Execute damage through `ProductionCombatRuleset`.
5. Combat projects the actor state into `RuntimeStatStageSnapshot`, then
   `StatStageScalingRequest` rejects every value outside the fixed `-4..+4`
   range before the selected scaling policy can resolve it.

**Consequence:** authored ruleset binding succeeds, modifier execution succeeds,
and later combat faults on the resulting state. A custom scaling policy cannot
repair this path because the fixed-range request constructor rejects first.

**Evidence:**

- [`RuntimeRulesetPolicyFactories.cs`](../../src/Convergence.Framework/Runtime/RuntimeRulesetPolicyFactories.cs#L586)
- [`PersistentStagedStatModifierPolicy.cs`](../../src/Convergence.Framework/Runtime/PersistentStagedStatModifierPolicy.cs#L10)
- [`TimedContributionStatModifierPolicy.cs`](../../src/Convergence.Framework/Runtime/TimedContributionStatModifierPolicy.cs#L11)
- [`ProductionCombatRuleset.cs`](../../src/Convergence.Framework/Battle/ProductionCombatRuleset.cs#L696)
- [`StatStageScaling.cs`](../../src/Convergence.Framework/Runtime/StatStageScaling.cs#L97)

**Missing regression:** binding tests prove narrower custom bounds such as
`-2..+2` and `-3..+3`, but no test crosses the fixed combat domain.

**Required decision:** either constrain supplied modifier-policy bounds to the
supported combat range with binding diagnostics, or make the scaling request
and tables genuinely policy-domain aware. The active documentation currently
advertises configurable bounds, so silently retaining the mismatch is not an
acceptable third option.

### M2. A negative stage satisfies the typed `has_buff` condition

**Invariant:** a condition named and serialized as `has_buff` must identify
positive modifier state, not any nonzero modifier state.

**Reachable path:**

1. Apply stage `-1` to a target's attack track.
2. Execute an effect conditioned by `target_has_buff` for that track.
3. `RuntimeActorState.HasBuff` returns true because it checks `Stage != 0`.
4. The conditioned effect executes even though the target is debuffed.

**Consequence:** authored conditional actions can trigger under the opposite
combat state. The current condition test covers only a positive stage, so it
does not expose this case.

**Evidence:**

- [`BattleRuntimeState.cs`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs#L335)
- [`ConditionAndTargetResolution.cs`](../../src/Convergence.Framework/Execution/ConditionAndTargetResolution.cs#L57)
- [`Conditions.cs`](../../src/Convergence.Framework/Content/Conditions.cs#L54)
- [`shared.schema.json`](../../schemas/content/v4/shared.schema.json#L225)
- [`ActiveSkillExecutionTests.cs`](../../tests/Convergence.Framework.Tests/SkillSystem/ActiveSkillExecutionTests.cs#L774)

**Required correction:** make `has_buff` require a positive resolved stage and
add positive, neutral, and negative regression cases. A separate typed
`has_debuff` or sign-aware modifier condition may be designed independently;
the existing wire value must first stop reporting the wrong sign.

### M3. Canonical item actions accept substituted same-ID definitions

**Invariant:** the canonical owned-item action boundary should execute the
authored item represented by the owned inventory ID, not arbitrary caller
effects attached to that ID.

**Reachable path:**

1. The inventory owns `pack:medicine`.
2. A host bug constructs another immutable `ItemDefinition` with ID
   `pack:medicine` but different targeting or effects.
3. The standard authorization policy authorizes every command other than skills
   and basic attacks without an item repository check.
4. Inventory availability and reservation validate only the item ID and
   quantity.
5. `ItemExecutor` executes the substituted definition.

**Consequence:** a UI, AI adapter, or script bug can bypass authored item
content while still consuming a legitimately owned item. This is a correctness
and content-authority defect, not a hostile-host security boundary.

**Evidence:**

- [`BattleActionAuthorization.cs`](../../src/Convergence.Framework/Execution/BattleActionAuthorization.cs#L144)
- [`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L665)
- [`CatalogContracts.cs`](../../src/Convergence.Framework/Catalog/CatalogContracts.cs#L117)

The current mechanics, developer, and technical pages disclose the trust, so
the prose is honest. The design nevertheless differs from the canonical skill
and basic-attack authority established for interchangeable Godot and sample
hosts.

**Required decision:** extend canonical action authorization with item catalog
identity, while retaining `ItemExecutor` as the deliberately lower-level
definition-driven effect service.

## Documentation Findings

### D1. Three action pages still describe the completed M1 integration as future work

The following active text says stat-stage execution is still migrating or that
M1-5 remains outstanding:

- [`actions-targeting-and-effects.md`](../mechanics/actions-targeting-and-effects.md#L159)
- [`typed-actions-and-effects.md`](../developer-guide/typed-actions-and-effects.md#L177)
- [`typed-action-and-effect-execution.md`](../technical/typed-action-and-effect-execution.md#L201)

Current source already routes modifier assessment, execution, meaningful
success, lifecycle, and restore through the selected policy service. These
paragraphs should describe the implemented integration and link to the policy
reference without future-tense qualification.

### D2. The stat-modifier technical reference overstates stale-state checks and reverses item commit order

[`stat-modifier-policy-runtime.md`](../technical/stat-modifier-policy-runtime.md#L100)
says prepared actions compare actor revisions. The action path has no general
actor revision. It uses single-use assessment ownership, exact request and
definition identity, target rebinding, current authorization, current resource
affordability, current modifier applicability, and matching lifecycle
boundaries.

The transaction diagram in the same file shows actor publication before item
reservation commit. Actual item execution commits the required reservation
first and publishes staged actor state afterward so a rejected inventory commit
cannot leave actor effects live. The diagram must match
[`BattleActionExecutor.cs`](../../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L937).

### D3. The active roadmap's current documentation totals do not match the executable matrix

[`documentation-completion-roadmap.md`](../roadmap/documentation-completion-roadmap.md#L135)
reports the current totals as 11 reviewed, 38 existing-unreviewed, 19 missing,
and 7 not-applicable. The executable matrix and
[`documentation-coverage.md`](../reference/documentation-coverage.md#L31) report
11, 39, 18, and 7 respectively.

The roadmap's earlier 37/20 table is explicitly a historical starting state and
is not defective. Only the paragraph that claims to describe the current state
needs correction.

## Confirmed Healthy Behavior

The source and tests support these current claims:

- battle, skill, and item assessments are executor-owned and single-use;
- random targets resolve once during assessment and are rebound, not rerolled;
- equipped skills and resolved basic attacks are authorized at assessment and
  immediately before execution;
- automated selectors cannot grant themselves arbitrary skills;
- skill costs and actor effects are staged and publish together;
- one item action reserves exactly one unit and publishes actor effects only
  after the required inventory transition succeeds;
- ordered typed effects honor conditions, target/action stop policies, and
  interruption;
- public result collections are defensive snapshots;
- persistent, timed-exclusive, and timed-contribution policies are distinct,
  immutable authorities;
- independently timed contributions reproduce the approved rolling-duration
  example;
- same-boundary application protection, idempotent ticks, stale-boundary
  rejection, and reserve suspension are implemented;
- skill, item, passive, encounter lifecycle, save validation, and aggregate
  restore use policy-owned modifier state.

## Verification

Executed against revision `6fef7ea`:

- focused Order 1 tests: 316 passed, 0 failed, 0 skipped;
- documentation and product-boundary tests: 22 passed, 0 failed, 0 skipped;
- full solution: 1,202 passed, 0 failed, 0 skipped;
  - Framework: 1,023;
  - DemoHost: 172;
  - ContentValidator: 7;
- nonincremental Release solution build: 0 warnings, 0 errors;
- `dotnet format --verify-no-changes`: passed.

These gates validate the covered behavior. They do not cover same-ID item
substitution, negative `has_buff`, or policy bounds outside `-4..+4`.

## Completion Recommendation

Keep these states unchanged for now:

- `O1-IR-H1`: `implemented_pending_review` may be promoted after the combined
  correction review because automated authorization itself passed this audit;
- `O1-IR-M1`: remain `implemented_pending_review` until M1 and M2 above are
  corrected;
- `O1-DOC`: remain `written_pending_review` until D1 through D3 and the chosen
  resolutions for M1/M3 are reflected across all audiences;
- capability implementation: remain `partial`;
- audience documentation: remain `existing_unreviewed`.

After isolated corrections and regressions, repeat the focused source review,
documentation comparison, full verification, and project-owner confirmation.
Only then should Order 1 be formally closed and Order 2 begin.
