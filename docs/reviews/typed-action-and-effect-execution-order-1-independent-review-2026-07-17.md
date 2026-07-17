# Typed Action And Effect Execution: Independent Order 1 Review

**Date:** 17 July 2026
**Reviewed revision:** `09f28d9` (`main`)
**Scope:** current production source, active tests, clean host integrations, and
the confirmed Order 1 action-ownership decision

## Verdict

Order 1 is **not ready to remain formally closed**.

The canonical `BattleActionExecutor` path is well structured and its central
transaction guarantees are working: authorization is checked twice, prepared
targets are reused rather than selected twice, skill costs and actor effects are
staged, item inventory is reserved before execution, and live actor state is
published only after the required inventory transition succeeds.

This independent source review nevertheless found one high-severity authority
gap and one medium-severity effect/result error. Both are reachable through
public, supported contracts. They are correctness defects rather than remote
security vulnerabilities, but they contradict the approved Order 1 rules and
require focused corrections before the capability is called complete again.

## Findings

### H1. The public automated battle runner bypasses canonical action authorization

**Intended invariant**

The confirmed action-ownership decision says that a menu, AI adapter, or script
may choose among authorized actions but cannot make an arbitrary skill legal.
Equipped canonical skill identity must be checked by the framework-owned action
facade during assessment and immediately before execution.

**Reachable path**

1. A framework consumer implements the public `IBattleActionSelector` extension
   contract.
2. That selector uses the public `ISkillExecutor` to prepare any active
   `SkillDefinition`, including one that is not equipped by the acting actor or
   is not the catalog's canonical definition.
3. It returns that assessment through `BattleActionSelection`.
4. `AutomatedBattleRunner` trusts the selection and calls
   `ISkillExecutor.Execute` directly. It never invokes
   `IBattleActionAuthorizationPolicy` or `BattleActionExecutor`.

The runner also does not require `selection.Skill` to be the same skill as the
prepared assessment's request. Its command event can therefore identify one
skill while another skill actually executes.

**Consequence**

A custom AI policy can execute an unequipped, substituted, or test-constructed
skill in a framework-owned encounter. Costs and effects are committed normally,
and the emitted command event may misidentify the action. The supplied
`DeterministicBattleActionSelector` is safe because it enumerates the actor's
active loadout, but the public extension boundary does not preserve that rule.

**Evidence**

- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs)
  exposes `BattleActionSelection`, `IBattleActionSelector`, and an
  `ISkillExecutor`-based constructor.
- Its turn handler accepts the selector's skill and prepared assessment, then
  executes `prepared.Preparation.Request` directly without action
  authorization.
- [`BattleActionAuthorization.cs`](../../src/Convergence.Framework/Execution/BattleActionAuthorization.cs)
  contains the equipped/canonical checks that this path omits.
- [`battle-action-ownership-and-inventory-authority.md`](../decisions/battle-action-ownership-and-inventory-authority.md)
  explicitly applies the authorization rule to AI adapters as well as menus and
  scripts.
- Existing automated-runner tests exercise the supplied selector and malformed
  targets, but do not attempt an unequipped or substituted prepared skill.

**Required correction**

Route ordinary automated selections through `IBattleActionExecutor` using a
typed `SkillBattleActionCommand`, or introduce an equivalent authorization
boundary that assesses and reauthorizes the exact selected command. Do not fix
only the default selector: the invariant must hold for every public selector.
Add adversarial tests proving that an unequipped skill, a substituted definition,
and a mismatched selection/assessment cannot mutate resources or emit a false
command identity.

**Correction status:** `implemented_pending_review`. The current worktree
shares the catalog skill authorization implementation between
`CatalogBattleActionAuthorizationPolicy` and `CatalogBattleActor`, validates
the complete prepared automated selection, and faults before command
publication or mutation. Focused tests cover unequipped and substituted skills,
mismatched skill identity, actor, participant set, encounter environment, and
resolved targets. The correction gate passes 1,054 solution tests with zero
failures or skips, a zero-warning strict build, formatting verification, and
both automated DemoHost battle paths.

### M1. A capped stat-stage effect reports a change and can consume an item when no state changed

**Intended invariant**

An item reservation is committed only when at least one effect succeeds
meaningfully. A no-effect item use must roll back inventory and must not publish
staged actor changes.

**Reachable path**

1. A target is already at the supplied stat-stage cap (`+4` or `-4`).
2. A consumable item applies `ModifyStatStageEffectDefinition` farther in the
   same direction, with no duration change.
3. `RuntimeActorState.ChangeStatStage` correctly clamps the stage and returns an
   actual delta of zero.
4. `ModifyStatStageEffectExecutor` discards that return value and reports the
   authored nonzero `StageDelta` instead.
5. `ItemExecutor.IsMeaningfulSuccess` treats that nonzero value as meaningful,
   so the canonical item transaction commits one inventory unit even though the
   target's state is unchanged.

**Consequence**

A developer-authored buff or debuff consumable can be lost at the stage cap
without producing the state change that justified consumption. The result also
misreports the magnitude whenever clamping applies partially, such as applying
`+2` at stage `+3`: the actor changes by `+1`, while the effect reports `+2`.

**Evidence**

- [`BattleRuntimeState.cs`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs)
  clamps stages to `-4..+4` and returns the actual applied delta.
- [`EffectExecutors.cs`](../../src/Convergence.Framework/Execution/EffectExecutors.cs)
  ignores that return and reports `definition.StageDelta`.
- [`ItemExecutor.cs`](../../src/Convergence.Framework/Execution/ItemExecutor.cs)
  treats any successful nonzero `Value` as meaningful; stat-stage effects are
  otherwise considered applicable without checking the cap.
- Current status-effect tests prove store separation but do not assert clamped
  effect values or item consumption at either cap.

**Required correction**

Do not patch only the executor's reported value. The project owner confirmed
that stat modifiers are a policy family with three supplied models: persistent
stages, one timed exclusive modifier, and independently timed contributions.
The current one-stage/one-duration actor state cannot represent all three.

The correction is therefore governed by the
[Stat Modifier Policy Roadmap](../roadmap/stat-modifier-policy-roadmap.md). It
must establish one immutable and atomic Framework authority, implement each
reference policy in its own checkpoint, route effect execution and lifecycle
work through that authority, bind selection explicitly, persist compatible
state, and then complete fresh code and documentation reviews. Item consumption
must ultimately use the selected policy's actual `StateChanged` result.

**Correction status:** `open`. M1-0, the owner decision, feasibility review,
and implementation roadmap, is complete. M1-1 through M1-8 and the final review
gates remain pending. The reachable capped-item defect remains until canonical
runtime integration is complete.

## Healthy Areas Verified

The following behavior was traced through current source and supported by
focused tests:

- `BattleActionExecutor` owns the canonical skill and basic-attack authorization
  policy and rechecks it immediately before execution.
- Assessments belong to one executor and one request, are single-use, and cannot
  be replayed after successful consumption.
- Random targets are resolved during assessment and rebound by runtime ID during
  execution; execution does not select them again.
- Prepared target relation, deployment, life-state, and count constraints are
  revalidated before mutation.
- Skill costs and ordered effects execute against cloned actor state and publish
  together only after successful completion.
- Item actions require an inventory port, reserve exactly one unit, validate the
  reservation's item ID, quantity, and lifecycle, and roll back rejected or
  cancelled uses.
- Item actor-state publication occurs only after inventory commit; ordinary
  reachable actor commit operations were not found to expose a partial
  publication path.
- Effect conditions, authored order, stop-target, stop-action, interruption, and
  action-duration cleanup are handled by the ordered effect executor.
- Result collections and definition collections examined in this scope are
  defensively snapshotted.
- Host-mediated commands return typed requests without pretending that external
  host work participates in framework rollback.
- Framework action code remains neutral to console, filesystem, Godot, and
  serializer APIs.

## Verification

| Gate | Result |
|---|---|
| Focused action, effect, and automated-battle tests | 132 passed, 0 failed, 0 skipped |
| Complete solution tests | 1,047 passed, 0 failed, 0 skipped |
| Framework tests | 872 passed |
| DemoHost tests | 168 passed |
| ContentValidator tests | 7 passed |
| Strict Release solution build | succeeded, 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | passed |
| Clean battle demo | succeeded; player-team victory |
| Clean field demo | succeeded; typed skill and item path completed |
| Training Annex runtime demo | succeeded; battle victory and save validation |
| `git diff --check` before this report | passed |
| Production worktree before this report | clean |

The green suite demonstrates substantial regression coverage. It does not
disprove either finding because the two adversarial cases are absent from the
current tests.

## Readiness Decision

The Order 1 documentation review was genuinely completed, but its final product
status was promoted too early. H1 has been corrected and remains pending an
independent completion review. M1 has become the approved multi-policy runtime
program described above. Until M1 is implemented and both corrections are
independently retested:

- `typed_action_and_effect_execution` should be treated as reopened;
- the implementation should not be described as fully complete;
- the three audience documents remain useful but are no longer reviewed
  authority for stat-modifier application, duration, or item applicability;
- Order 2 documentation work may be planned, but calling Order 1 polished and
  final would be inaccurate.

No additional high-, medium-, or low-severity Order 1 defect was substantiated
by this review. Potential alternate item-consumption policies and broader host
UX choices were not promoted into findings without a confirmed invariant and a
reachable consequence.

## Review Method

This was a fresh source-first review. Earlier review conclusions were not used
as evidence. The audit traced current production calls from public command and
selector contracts through authorization, assessment ownership, target
resolution, lower-level skill/item execution, ordered effects, actor
transactions, inventory reservations, encounter integration, clean hosts, and
current tests. Active decision and mechanics documents were used only to
identify approved intent against which the current code was judged.
