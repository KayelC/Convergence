# Combat Resolution Order 2 Completion Review

**Review date:** 19 July 2026

**Scope:** current Framework combat resolution after O2-C1 through O2-C6

**Method:** source-first; roadmaps and earlier reports were not accepted as
implementation proof

**Subsequent status:** an independent source review on 20 July 2026 reopened
Order 2 for focused corrections. See the
[independent review](combat-resolution-order-2-independent-review-2026-07-20.md)
and [correction roadmap](../roadmap/combat-resolution-order-2-corrections-roadmap.md).
The completion conclusions below remain evidence for the revision reviewed on
19 July and are not the current closure decision.

## Review Standard

A defect was recorded only when the current code showed:

1. an intended invariant expressed by current contracts or confirmed design;
2. a reachable call path through a public or active integration boundary;
3. a concrete behavior, integrity, or maintenance consequence; and
4. reproducible source or test evidence.

Alternative game designs, future equipment work, impossible domain values, and
the known local Godot executable startup problem were not inflated into combat
vulnerabilities.

## Source Trace

The review traced these paths directly:

- authored ruleset definition -> registered factory -> neutral combat policy
  aggregate -> DemoHost and Godot execution services;
- skill/basic attack authorization -> prepared targets -> staged execution;
- defense and affinity resolution -> charge lookup -> hit -> critical ->
  standard arithmetic -> per-hit runtime mutation;
- Null, Repel, Absorb, drain, defeat prevention, and multi-hit sequencing;
- instant-defeat resistance -> probability -> staged lethal mutation -> defeat
  prevention;
- ordered effect failure/interruption -> action outcome aggregation -> turn
  economy;
- charge apply, duplicate rejection, action consumption, lifecycle expiry,
  snapshot validation, and session restoration; and
- schema-v5 basic-attack critical metadata and active content loading.

The primary files reviewed were the policy and execution sources named in
[Combat Resolution Pipeline](../technical/combat-resolution-pipeline.md), plus
`SkillExecutor`, `BattleActionExecutor`, runtime persistence/restoration, the
standard ruleset factory, Training Annex host composition, and the Godot smoke
consumer.

## Corrected Findings

### R1: Active validation still targeted retired schema v4

- **Invariant:** release validation must validate the same schema contract the
  Framework and active content use.
- **Reachable path:** the quality workflow and README validator command invoked
  `schemas/content/v4` after active content had advanced to v5.
- **Consequence:** local/CI instructions could fail or validate the wrong
  contract rather than proving current content.
- **Correction:** workflow, README, and product-boundary regression now require
  v5 and reject the retired path.
- **Commit:** `eb75fa0 quality: validate active content schema`

### R2: Several host-random consumers trusted invalid unit values

- **Invariant:** `IRandomSource.NextUnitDecimal()` is a normalized unit value in
  `[0, 1)` and integer output respects the requested half-open range.
- **Reachable path:** a custom Godot or test adapter could return a percentage
  such as `50`, or an out-of-range hit-count index.
- **Consequence:** variance, ailment, initiative, reward, or hit-count behavior
  could silently leave its authored domain.
- **Correction:** the hosting contract documents exact ranges; standard damage
  variance, ailment, initiative, reward, and hit-count policies reject invalid
  values; focused tests exercise every corrected consumer.
- **Commit:** `33447a2 runtime: validate supplied random inputs`

### R3: The combat aggregate could advertise policies it did not execute

- **Invariant:** policy authorities exposed by an authored combat composition
  must be the authorities called during combat.
- **Reachable path:** the public `CombatExecutionPolicySet` constructor accepted
  one damage executor and unrelated hit/critical objects, or one instant-defeat
  executor and an unrelated resolution policy.
- **Consequence:** diagnostics and host inspection could identify a custom rule
  while gameplay silently used a different rule hidden inside the executor.
- **Correction:** composed damage and instant-defeat interfaces now expose
  their own actual sub-policies; the aggregate derives its descriptive
  properties from those executors. The former mismatch cannot be constructed.
  A regression forces a custom miss policy through real standard damage
  resolution.
- **Commit:** `291c93d runtime: enforce coherent combat composition`

### R4: Tracked contract assets could remain stale in build outputs

- **Invariant:** an ordinary build must execute and test the current tracked
  content, schema, and ledger contracts.
- **Reachable path:** `CopyToOutputDirectory=PreserveNewest` compares only file
  timestamps. After a Git operation gives an obsolete output copy a later
  timestamp than its corrected source, MSBuild leaves the obsolete copy in
  place.
- **Consequence:** five DemoHost tests loaded an old equipment document without
  schema-v5 critical metadata even though the tracked source was correct. A
  manual clean concealed the problem but did not prevent it from recurring.
- **Correction:** tracked JSON contracts now use `Always` copying in the
  Framework-test, DemoHost, and DemoHost-test projects. A product-boundary test
  prevents those assets from returning to timestamp-only copying. The formerly
  failing DemoHost suite passes without a prior clean.
- **Commit:** `021c5a8 quality: refresh copied contract assets`

## Current Health

No additional reachable Order 2 defect remained after the four corrections.
The current implementation has:

- one coherent policy authority per combat rule;
- exact authored hit, critical, and instant-defeat inputs with explicit
  configurable modifiers;
- no hidden Luck contribution in supplied combat probability or damage;
- split and unified charge policies with action-scoped consumption and
  restorable policy identity;
- sequential staged multi-hit mutation and immutable evidence;
- replaceable action-level outcome aggregation;
- checked or saturating arithmetic and validated host randomness; and
- actor-state atomicity across costs, effects, charge consumption, and failure.

The source-first focused review gate passed `240` combat/action/persistence
tests. The final solution gate passed `1,302` tests: `1,122` Framework, `173`
DemoHost, and `7` ContentValidator tests, with zero failures and zero skips.
Framework and complete-solution Release builds also passed with zero warnings.

## Deliberate Boundaries

These are not unresolved Order 2 defects:

- armor defense/evasion, equipment-granted skills, and typed secondary
  equipment effects belong to the equipment capability;
- ailment duration and passive lifecycle ordering belong to the lifecycle
  capability;
- turn economies other than Action Token use the existing replacement
  interface and require separate policy implementations;
- player UI and animation consume typed facts but remain host-owned;
- full deterministic replay remains deferred; and
- the local Godot 4.7.1 Windows executable's pre-project native startup crash
  is an environment result, not evidence that combat code executed or failed.

## Documentation Gate

Consumer, developer, and technical documentation has been written from this
trace. It remains `existing_unreviewed` until the project owner confirms the
plain-language behavior. That owner confirmation, not this report alone, closes
O2-C7.
