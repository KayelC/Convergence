# Combat Resolution Order 2 Final Pre-Closure Corrections Review

**Review date:** 21 July 2026

**Reviewed branch:** `main`

**Reviewed revision:** `53c8b51` (`execution: validate custom effect results`)

**Method:** fresh source inspection of the current combat and action pipeline,
followed by focused reproductions and the complete release gate. Earlier review
summaries were used only to identify former paths to reproduce; their claims
were not accepted as proof of current behavior.

## Result

Order 2 is ready to close.

No remaining realistic, reachable defect was found in the reviewed combat
resolution, ordered-effect, action-outcome, Action Token, or host result
boundaries. O2-R24 through O2-R28 are implemented and verified from current
source. O2-R29 reconciles that implementation with all active audience and
capability records.

No security vulnerability was found. Trusted hosts can still implement custom
policies incorrectly or perform external side effects that Framework actor
transactions cannot roll back. Those are documented integration boundaries,
not silent defects in the supplied combat pipeline.

## Current-Source Findings

There are no open findings in the Order 2 scope.

The post-R27 review found one final supported extension defect: a custom effect
could return undefined execution and turn-economy enum values. At this revision
`EffectExecutionResult` validates construction and record cloning. The malformed
handler reproduction now returns a typed rejected action before publishing its
prepared cost, earlier staged restoration, later effect, result evidence, or
turn consumption.

The adjacent extension outputs were rechecked rather than generalized into
speculative hardening:

- damage output is constrained by `DamagePolicyResolution` and
  `DamageHitResolution`;
- random target selections are checked against eligibility, count, and unique
  runtime IDs;
- action turn consumption and economy resolution validate their complete
  payload shapes;
- encounter command status, requested outcome, winning team, turn value, and
  event entries are validated at the port boundary;
- custom effect failures and malformed result construction occur inside the
  staged actor transaction and become typed pre-commit rejection.

## Mechanics Revalidated

- The supplied damage policy uses Strength for Physical attacks, Magic for
  other damage, and Vitality plus defense for mitigation. Luck is not silently
  inserted into hit or critical math.
- Authored Accuracy and Agility-derived Evasion feed the selected hit policy.
  Critical eligibility and critical chance remain independent replaceable
  policies.
- Affinity, guard, critical, charge, variance, and multi-hit application occur
  once in the documented order, with saturating boundary arithmetic.
- Split and Unified charge policies reject duplicate charge state and consume
  the matching charge once after a committed defense attempt.
- Multi-hit damage mutates staged targets sequentially and stops after defeat.
  Each attempted hit retains immutable evidence.
- Ordered dependencies are earlier-only. `positive_damage` requires a committed
  negative resource delta for the applicable target. Shared-contact damage
  reuses contact but resolves its own element, affinity, power, critical rules,
  charge category, and hits.
- Complete-action aggregation preserves the approved priority. Supplied item
  actions spend one normal action unless effect-driven item pricing is selected.
- Action Token passing consumes an existing partial token first; otherwise it
  converts one full token to one partial token.
- Repeated costs for one resource reject before target randomness or amount
  resolution. Prepared costs are single-use quote-locked amounts whose authored
  identity and current affordability are revalidated before commit.
- `party_size = 0` consistently means no living deployed participant on the
  acting actor's team in the supplied battle context.

## Verification Evidence

Focused action and result-contract verification passed:

```text
167 passed, 0 failed, 0 skipped
```

The complete solution passed:

```text
Convergence.Framework.Tests:       1,270 passed
Convergence.DemoHost.Tests:          173 passed
Convergence.ContentValidator.Tests:    7 passed
Total:                              1,450 passed, 0 failed, 0 skipped
```

Additional gates passed:

- strict nonincremental Release solution build: zero warnings and errors;
- Framework trimming analysis with warnings as errors;
- `dotnet format --verify-no-changes`;
- Framework coverage: `90.54%` lines and `75.74%` branches;
- all 6 active packs, 36 documents, and 98 qualified definitions passed schema,
  deserialization, semantic, dependency, registration, and catalog checks;
- battle, field, save, and Training Annex noninteractive DemoHost modes;
- scripted Training Annex behavior through the 173 DemoHost tests;
- Framework architecture, API, documentation-link, forbidden-reference, Godot
  contract, and Godot sample build checks through the complete solution/build;
- `git diff --check`.

The native Windows Godot executable was not relaunched during this combat-only
closure because this machine has previously produced an engine-level access
violation/hang in headless automation. The managed Godot contract tests and
sample build are green. This machine-specific native smoke remains transparent
release-environment work, not a hidden combat-resolution failure.

## Closure Decision

Promote `typed_action_and_effect_execution`, `combat_resolution`, and
`host_contracts` to `complete`. The four capabilities corrected by O2-R24
through O2-R27 remain complete. Order 2 documentation is reconciled and its
formal gate is closed.

This does not mean every Convergence feature is finished. Save-version
migration and deterministic replay remain explicitly deferred capabilities,
and later documentation orders still review other mechanics. It means the
implemented Order 2 combat-resolution family has no known open gap in its
current supported scope.
