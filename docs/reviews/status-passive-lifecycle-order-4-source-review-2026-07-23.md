# Status And Passive Lifecycle Order 4 Source Review And Roadmap

**Review date:** 23 July 2026

**Reviewed revision:** `c708f671`

**Capability:** `status_and_passive_lifecycle`

**Conclusion:** implemented foundation with substantial test coverage; owner
decisions are approved, O4-R2 is implemented pending the final independent
review, and the remaining correction sequence in this document is the
authoritative Order 4 implementation roadmap

## Purpose

This is the source-first opening review for Documentation Order 4. It does not
treat the capability matrix, an earlier migration report, or existing prose as
proof. The implementation, tests, active content, persistence boundary,
encounter adapter, DemoHost composition, and Godot reference consumer were
traced directly.

Following owner review, this document also records the normative design
decisions and ordered correction checkpoints. A decision marked approved is not
implemented merely because it appears here. Order 4 remains open until every
checkpoint is implemented, tested, documented, and independently re-reviewed.

The word *vulnerability* in this report means a reachable correctness or
integration weakness. No network, privilege-escalation, data-exfiltration, or
other conventional security vulnerability was found in this scope.

## Scope Read From Source

Order 4 currently includes:

- typed ailment definitions, groups, exclusivity, turn restrictions, modifiers,
  triggers, recovery, and durations;
- application gates for defeated targets, guard, resistance, and chance;
- turn-start guard clearing and restriction resolution;
- owner-turn-end passive dispatch, ailment effects, recovery, and duration
  ticking;
- action, phase, swap, battle-end, and field-transition lifecycle operations;
- charges, shields, affinity Break, affinity overrides, other timed statuses,
  and stat-modifier lifecycle integration;
- ordered passive dispatch, conditions, recursion suppression, activation
  counts, and transactional effect execution;
- encounter lifecycle adaptation and typed encounter events; and
- battle-status and passive-activation persistence and restoration.

Combat arithmetic, Action Token rules, actor scheduling, player knowledge,
presentation, and save-file serialization belong to other orders.

## Confirmed Strengths

The implementation is not an empty shell. The following behavior is present
and well supported:

- Ailment behavior is typed and never inferred from display names.
- Major-ailment exclusivity is enforced by an explicit content ID.
- Turn restrictions cover normal, skip, limited actions, chance skip,
  flee/recall, forced basic attack, confusion, and custom handlers.
- The most-restrictive policy combines simultaneous ailments deterministically.
- Guard, immunity, resistance, and authored chance participate in application.
- Turn-start and turn-end lifecycle work is staged through actor transactions.
- Throwing custom turn handlers, ailment effects, and passive effects roll back
  staged mutations in their canonical action/lifecycle paths.
- Owner-turn-end order is deterministic: passive triggers, ailment triggers,
  recovery/removal, then duration ticking.
- Passive order is deterministic by loadout, trigger, target, then effect.
- Recursive passive activation is suppressed by default.
- Instant, counted-turn, phase, battle, and permanent duration definitions are
  represented and persistence rejects malformed retained durations.
- The three supplied stat-modifier policies remain delegated to their own
  policy service rather than being reimplemented in the ailment lifecycle.
- Training Annex and the reference status pack use typed content and no legacy
  name parsing.

## Confirmed Findings

### O4-H1: Duration expiry and encounter persistence cannot be expressed independently

**Kind:** architecture/design blocker, not a hidden regression

**Correction status:** implemented pending independent review in O4-R2

`TurnDurationDefinition` expresses a counted event clock. However,
`RuntimeActorState.ClearEncounterStatuses` removes ailments only when their
duration kind is Instant, Phase, or Battle. A turn-counted ailment therefore
survives battle-end and field-transition cleanup. Existing tests explicitly
lock in that behavior.

The inverse is also limiting: `BattleDurationDefinition` clears at encounter
end, but it cannot also count down. Consequently, content cannot express the
common rule “expire after three owner turns or at battle end, whichever comes
first.”

**Reachable consequence:** a host must either preserve a three-turn ailment
outside its encounter, abandon its turn count, or add out-of-band cleanup. The
framework cannot currently represent the intended lifetime as one authoritative
state.

**Source evidence:**

- `Content/SharedPrimitives.cs`: the duration union stores one duration kind;
- `Execution/BattleRuntimeState.cs`: `ClearEncounterStatuses` omits turn
  durations; and
- `BattleStatusLifecycleTests.BattleCleanup_ExpiresBattleStateAndPreservesEveryPermanentStateFamily`
  explicitly expects a turn-counted ailment to survive.

**Recommended direction:** separate the expiration clock from cleanup
persistence. A status lifetime should be able to declare both a counted or
event-driven expiry and the cleanup scopes that terminate it.

### O4-M1: The canonical lifecycle cannot honor `SuspendWhileReserve: false`

**Kind:** reachable framework correctness gap

Low-level duration ticking correctly checks `SuspendWhileReserve`. The
high-level `BattleStatusLifecycleService.ProcessTurnEndCore`, however, returns
immediately for every undeployed actor. The standard encounter port invokes
owner-turn-end only for the acting participant. There is no canonical boundary
that advances counted non-modifier state on reserve actors.

**Reachable consequence:** `SuspendWhileReserve: true` works, but the authored
`false` case is not operational through the supplied encounter lifecycle. A
developer can make it work only by calling mutable actor tick methods manually.

**Approved direction:** reserve ticking and field persistence are independent
rules. Add one typed lifecycle-clock operation that can be delivered to the
appropriate actor set and an injected reserve-lifecycle policy. Supply both:

- a suspension policy, selected by default, under which undeployed actors retain
  their exact remaining durations until redeployed; and
- an advancing policy that decrements reserve state only on an explicitly
  configured encounter clock, such as owning-team phase end or round end.

The advancing policy must not silently tick once per action because teams with
more actions would then age reserve state faster. Hosts do not dispatch an
encounter clock while in field state, so choosing reserve advancement does not
imply field-state decay. The lifecycle service, not presentation or direct
actor mutation, owns these decisions.

### O4-M2: Encounter phase expiry has an undocumented team-ID coupling, and no round clock

**Kind:** reachable integration defect plus documentation mismatch

`PhaseDurationDefinition` stores a registered phase ID. The standard
`BattleStatusEncounterLifecyclePort` receives a team ID from the encounter
runner and passes that team ID directly to `ProcessPhaseEnd` as the phase ID.
Team IDs and phase IDs are separate registered vocabularies.

**Reachable consequence:** an authored duration such as `player_phase` does not
expire when the acting team is `player_team`. It works only when a game happens
to use the same ID in both domains. The framework does not validate or document
that hidden equality requirement.

The active mechanics page also says state can expire by rounds. The encounter
runner publishes round-start events, but the public duration union has no round
duration and the canonical lifecycle port dispatches no round boundary.

**Recommended direction:** make phase/round lifecycle clocks explicit inputs or
inject a typed encounter-clock mapping policy. Do not infer a phase ID from a
team ID.

### O4-M3: Lifecycle event evidence is incomplete for an event-driven host

**Kind:** host integration defect

Several mutations are correct in state but absent or underspecified in the
event stream:

- `OrderedEffectExecutor` runs action-end expiry but discards the returned
  expiry events;
- cleanup emits one generic `CleanupApplied` event and does not identify the
  ailments, shields, charges, overrides, Breaks, or other statuses removed;
- turn-end and battle-start passive mapping emits the passive ID and resource
  changes, but drops non-resource effect results, trigger index, typed outcome,
  and event ID;
- exclusivity replacement does not emit the removed ailment ID; and
- application cannot distinguish a first application from a duration refresh.

**Reachable consequence:** a Godot host consuming ordered events cannot animate
or explain all accepted lifecycle mutations without diffing mutable actor state
or parsing `Detail`. That contradicts the broader typed-event design used by
actions and encounter orchestration.

**Recommended direction:** return immutable typed transition evidence from
application, expiry, cleanup, and passive dispatch, then map every committed
transition into the encounter stream. Debug text must remain optional.

### O4-M4: Direct timed-state mutation accepts state that restore later rejects

**Kind:** public runtime-boundary defect

Catalog content validation rejects non-positive turn durations, invalid tick
IDs, and unregistered phase IDs. Restore validates retained duration state.
Public live mutation methods such as `ApplyAilment`, `AddOtherStatus`, and
`OverrideAffinity` do not enforce the equivalent runtime-valid duration and ID
domain before storing it.

**Reachable consequence:** a host using the public runtime API can create a
live status that never ticks or cannot be saved/restored, even though the same
shape is rejected at the content and restore boundaries.

**Recommended direction:** centralize a runtime duration-domain guard and call
it from every public timed-state mutator. Catalog registration checks still
belong to catalog-aware services; basic ID, enum, null, and positive-count
invariants belong at the live mutation boundary.

### O4-M5: Direct lifecycle ailment application is not atomic around extension policies

**Kind:** extension-boundary atomicity defect

Canonical skill and item execution operates on staged actor clones, so ailment
policy failures roll back there. The public
`BattleStatusLifecycleService.TryApplyAilment` path delegates directly to
`IBattleAilmentApplicationService` with live mutable actors. An injected
`IAilmentApplicationPolicy` receives those actors and may mutate one before
returning false or throwing.

**Reachable consequence:** a buggy host policy can reject or fault an ailment
while leaving unrelated partial actor mutation behind.

**Recommended direction:** make the public lifecycle application path stage all
participants and commit only an accepted result. The lower-level service can
remain an internal/staged primitive if that ownership is made explicit.

### O4-M6: Passive activation reset can partially mutate a cancelled battle start

**Kind:** adjacent Order 6 encounter atomicity defect

`BattleEncounterRunner` resets each participant's per-battle passive activation
counts on the live actor while publishing `ActorCreated` events. Cancellation
is checked inside that loop. If cancellation or event publication occurs after
one participant but before the rest, reset state can differ between actors even
though battle-start lifecycle has not committed.

**Reachable consequence:** a cancelled pre-start encounter may change persisted
passive activation bookkeeping for only part of the party.

**Recommended direction:** stage the reset for all participants and commit it at
the same accepted battle-start boundary as lifecycle startup. This correction
must be coordinated with Order 6 rather than hidden inside presentation code.

### O4-L1: Passive event policies accept contradictory liveness configuration

**Kind:** low-severity public configuration defect

`PassiveEventPolicy` accepts a negative activation limit. Such a policy silently
suppresses every activation because the current count is always greater than or
equal to the negative limit. It also permits `AllowReentry: true` with no finite
activation limit, allowing a re-dispatching custom handler to recurse without a
framework liveness bound.

**Recommended direction:** require positive finite limits when supplied. Decide
whether re-entry must always carry a finite limit or whether a separately
registered recursion/liveness policy is preferable.

### O4-L2: `ForcedPhysical` does not name the rule it actually represents

**Kind:** low-severity public-contract ambiguity

Authored content declares `ForcedBasicAttackAilmentTurnBehaviorDefinition`, and
the automated resolver requires the host-selected typed basic attack. The
public outcome is named `BattleTurnStartOutcome.ForcedPhysical`, while active
documentation says “forced physical action.” Another host can reasonably
interpret that as permission to choose any physical skill.

**Recommended direction:** use `ForcedBasicAttack` consistently before a stable
release, with no compatibility alias unless an actual released API requires
one.

## Approved Owner Decisions

These are not classified as defects because the current code implements one
coherent choice. Owner review has now selected the intended replacement
contracts. Every decision remains pending implementation.

### Decision status

Owner review on 23 July 2026 established the following status. Approval here
confirms the recommended framework direction; it does not mark the work as
implemented.

| Decision | Status | Approved direction |
|---|---|---|
| O4-D1 | Approved, pending implementation | Injected ailment transition policy with typed new, refresh, reject, and replacement outcomes. |
| O4-D2 | Approved, pending implementation | Injected ailment application-gate policy; guarding against ailments remains one supplied policy rather than an unconditional rule. |
| O4-D3 | Implemented, pending independent review | Replaced the ambiguous removal Boolean with typed removal causes and supplied removal profiles shared across status families. |
| O4-D4 | Approved, pending implementation | Typed passive targeting plus an explicit activation-counting scope. |
| O4-D5 | Implemented, pending independent review | Typed departure reasons and independent lifetime/removal policy preserve field state and prevent recall or swapping from becoming a free cure. |
| O4-D6 | Implemented, pending independent review | Zero stat multiplier provides fixed natural-recovery chance; negative multipliers reject atomically. |

### O4-D1: Ailment reapplication and exclusivity

Current behavior is unconditional replacement:

- applying the same ailment resets its duration and succeeds;
- applying another ailment in the same exclusivity group removes the old one;
- `IsRemovable: false` does not prevent exclusivity replacement; and
- no transition result distinguishes new, refreshed, or replaced state.

Approved framework shape: an injected transition policy with supplied `Reject`,
`Refresh`, and `Replace` strategies, plus typed affected IDs and outcomes for a
new application, refresh, replacement, and rejection. The supplied standard
retains current behavior by refreshing the same ailment and replacing a
different ailment in the same exclusivity group. A game selects another policy
instead of inheriting that rule invisibly.

### O4-D2: Guard as an ailment gate

Current behavior makes guard block every ailment before the selected ailment
chance policy runs. A developer cannot author a guard-piercing ailment or a game
where guard affects damage only.

Approved framework shape: move guard handling into an injected ailment
application-gate policy. Supply the current guard-blocks-ailments behavior as
the standard policy while allowing a game to select damage-only guard or an
authored/policy-controlled guard-piercing rule.

### O4-D3: Meaning of `IsRemovable`

Current `false` blocks explicit cures, remove-on-event recovery, and natural
recovery. It does not block duration expiry, exclusivity replacement, or
encounter cleanup. Charges, shields, Breaks, and affinity overrides have no
equivalent flag and are always removable by their broad status-kind operation.

Approved decision: replace the ambiguous Boolean with typed removal causes
shared across applicable status families. The minimum cause vocabulary is:

- cure effect;
- natural recovery;
- authored recovery event;
- duration expiry;
- exclusivity replacement;
- deployment swap;
- defeat;
- flee;
- roster recall;
- battle end;
- field transition; and
- explicit scripted removal.

Supply at least three reusable profiles:

- **standard removal**, which permits ordinary recovery, expiry, replacement,
  and configured cleanup;
- **uncurable**, which blocks cure effects and ordinary recovery while still
  allowing expiry and configured cleanup; and
- **protected**, which permits only its explicitly selected causes, such as
  expiry or scripted removal.

Duration expiry is not a cure, and cleanup persistence is not inferred from
cure resistance. Charges, shields, affinity state, stat modifiers, ailments,
and other statuses must use the same typed cause vocabulary wherever removal
protection is applicable. Selected removal must not require clearing every
status of a broad family.

### O4-D4: Passive trigger targeting and activation counting

`PassiveTriggerDefinition` does not author targeting. The dispatcher can
accept multiple targets, but the canonical battle-start and owner-turn-end
ports always pass only the owner. Party-wide opening or support passives are
therefore not representable through the supplied lifecycle composition.

Activation limits are counted inside the target loop and keyed by owner,
skill, trigger, and event, not target. A limit of one across two requested
targets executes only for the first target.

Approved decision: give triggers explicit typed targeting or an injected
event-target policy. Activation counting must be an explicit policy choice,
including at least per-dispatch and per-target scopes. The supplied standard
counts one successful trigger dispatch as one activation even when it fans out
to multiple targets; target order must never decide which targets receive an
otherwise valid party-wide activation.

### O4-D5: Cleanup behavior by departure reason

Current swap cleanup clears guard and non-permanent charges/shields but
preserves ailments, other statuses, affinity Breaks/overrides, and stat
modifiers. Battle end and field transition share encounter cleanup. Defeat,
flee, and roster recall do not have distinct status cleanup scopes.

Approved decision: add typed departure reasons and resolve cleanup through
lifetime/removal policy, not a host-side list of status names. The supplied
standard behavior is:

| Boundary | Standard behavior |
|---|---|
| Active Hosted Entity change | Perform no actor-status cleanup. The Vessel remains deployed; composition changes are not actor departure. |
| Deployment swap | Clear guard and non-permanent charges/shields. Preserve ailments, stat modifiers, affinity changes, and other statuses; their clocks follow the reserve policy. |
| Voluntary Companion recall | Use deployment-swap behavior so recall is not a free cure. |
| Ailment-forced recall | Preserve the triggering ailment unless that ailment explicitly permits removal on recall. |
| Defeat | Clear guard, charges, shields, and battle-only modifier/affinity/other state. Preserve ailments or other state explicitly authored to survive defeat. |
| Flee | Clear battle-only state and preserve state authored to remain outside the encounter. |
| Battle end | Remove encounter-only state and preserve field-persistent state. |
| Field transition | Remove only state whose lifetime permits field-transition cleanup; movement is not an implicit cure. |

Defeat-triggered recall uses the defeat reason rather than being flattened into
voluntary recall. Recovery facilities and safe areas remain explicit services,
not hidden field-transition cleanup.

### O4-D6: Fixed natural-recovery chance

Runtime arithmetic treats a non-positive stat multiplier as “use only the base
chance,” but semantic validation rejects a zero multiplier. Standard content
therefore cannot author a fixed natural-recovery chance independent of an
actor stat.

Approved decision: allow zero as a valid stat multiplier for fixed natural
recovery chance and continue rejecting negative multipliers. Custom recovery
policies remain replaceable, but fixed chance must not require one.

## Documentation Findings

The current audience documentation is correctly still marked
`existing_unreviewed`. It should not be promoted yet.

Specific drift found during this review:

- `status-passives-and-knowledge.md` says durations support rounds, but the
  public duration model and canonical lifecycle do not.
- The same page implies reserve ticking follows the authored suspension flag,
  while the supplied high-level lifecycle suppresses every reserve turn-end.
- “forced physical action” is broader than the implemented forced basic attack.
- The combined status/knowledge page crosses Order 4 and Order 5 ownership and
  makes each capability harder to review independently.
- No dedicated developer guide explains lifecycle composition, required event
  IDs, phase/team mapping, custom handler transaction expectations, or typed
  event consumption.
- No dedicated technical reference diagrams the complete apply, turn-start,
  turn-end, action-end, phase-end, cleanup, and passive-dispatch state machines.

## Approved Correction Roadmap

This review is the authoritative Order 4 correction roadmap. Checkpoint R1 is
completed by the owner decisions recorded above; completion of R1 does not
change runtime behavior.

1. **O4-R1 - Record owner decisions. Complete.** This review defines D1-D6,
   reserve-clock behavior, and the supplied cleanup defaults.
2. **O4-R2 - Separate expiry clocks from persistence. Implemented, pending
   independent review.** H1, D3, D5, and D6 now use a runtime lifetime contract
   that combines an expiration clock with independent typed removal
   permissions. Supplied deployment, encounter, field, persistent, uncurable,
   and protected compositions cover typed departure cleanup, consumption, and
   fixed natural recovery without treating persistence as cure resistance.
3. **O4-R3 - Add canonical lifecycle clock dispatch. Implemented, pending
   independent review.** M1 and M2 now use typed actor-turn, action,
   team-phase, round, and host-defined boundaries. The encounter adapter
   requires an explicit team-to-phase clock policy and the runner delivers one
   round boundary after every complete set of team phases. Reserve state
   suspends by default; the supplied advancing policy accepts only an exact
   owning-team phase or round event and still honors status-level
   `SuspendWhileReserve`.
4. **O4-R4 - Complete typed lifecycle transitions and events. Implemented,
   pending independent review.** M3 and D1 now surface first application,
   refresh, exclusive replacement, typed rejection, passive evaluation and
   effects, duration advancement/expiry, and cleanup removals as ordered,
   immutable evidence. Skill, item, basic-attack, automated-battle, and
   encounter adapters carry the same committed lifecycle events without
   re-inferring effects from content definitions or parsing debug text.
5. **O4-R5 - Harden live mutation and ailment application.** Resolve M4, M5,
   and D2 through shared duration validation, staged public application, and
   the injected application gate.
6. **O4-R6 - Complete passive policy semantics.** Resolve L1 and D4 by
   validating liveness and implementing typed targeting and activation scope.
7. **O4-R7 - Integrate encounter startup and public terminology.** Resolve M6
   atomically with Order 6 boundaries and replace `ForcedPhysical` with the
   accurate `ForcedBasicAttack` contract from L2.
8. **O4-R8 - Reconcile schema, clean content, persistence, DemoHost, and Godot
   proof.** Change schema/save versions only when the implemented wire shape
   requires it, with no hidden fallback.
9. **O4-R9 - Write and collaboratively review all three audience documents.**
   Produce the technical lifecycle/state-machine reference, developer
   composition guide, and player-facing mechanics page.
10. **O4-R10 - Perform a fresh source and documentation closure review.** Verify
    every approved decision from current code rather than this roadmap.

Each implementation checkpoint should be isolated, tested, and committed
separately under the established workflow.

## Test Gaps To Close

Focused correction work should add evidence for:

- counted expiry combined with battle/field cleanup;
- both values of reserve suspension through the canonical lifecycle;
- distinct team and phase IDs and an explicit round clock;
- complete action-end, cleanup, ailment-replacement, and passive-effect events;
- direct timed-state mutation rejection before live state changes;
- mutating/throwing ailment policy rollback through the public lifecycle;
- cancellation during passive reset at battle start;
- negative/reentrant passive policy validation;
- party-wide passive targeting and approved activation-limit scope;
- same-ailment refresh and exclusive replacement strategies;
- each approved cleanup cause; and
- fixed natural recovery if zero multiplier is approved.

## Verification Evidence

The review itself changed documentation only. Its source conclusions were
checked against the current executable baseline:

- focused lifecycle, passive, encounter-runner, and persistence tests:
  202 passed, 0 failed, 0 skipped;
- documentation foundation, synchronization, and product-boundary tests:
  22 passed, 0 failed, 0 skipped;
- complete solution: 1,531 passed, 0 failed, 0 skipped;
- strict nonincremental Release solution build: 0 warnings, 0 errors; and
- `git diff --check`: passed.

Those original green tests did not invalidate the findings above. At review
time, battle cleanup preserved every turn-counted ailment, low-level reserve
ticking was tested separately from the canonical lifecycle, and phase tests
used matching phase/team IDs. The O4-R2 record below supersedes the first of
those observations; O4-R3 still owns the clock-dispatch findings.

### O4-R2 implementation gate

The first correction checkpoint was verified after implementation:

- focused lifetime, cleanup, charge, persistence, and host-codec coverage passed;
- complete solution: 1,544 passed, 0 failed, 0 skipped;
- strict nonincremental Release solution build: 0 warnings, 0 errors;
- clean battle, field, save, and Training Annex demos exited successfully;
- formatting verification, Framework forbidden-reference search, content-tree
  status, and `git diff --check` passed; and
- runtime save contract v12 round-trips expiration and allowed removal causes
  through both host-owned save codecs.

### O4-R3 implementation gate

The canonical clock checkpoint adds no inferred route from team identity to
phase identity. Focused tests prove actor-only clocks, action expiry, distinct
team/phase/event IDs, round dispatch frequency, host-defined boundaries,
default reserve suspension, opt-in owning-team phase advancement, opt-in round
advancement, and status-level reserve suspension. The implementation gate
passed with:

- focused lifecycle-clock, status-lifecycle, encounter-runner, catalog-runtime,
  and stat-modifier integration coverage: 246 passed, 0 failed, 0 skipped;
- complete solution: 1,559 passed, 0 failed, 0 skipped;
- strict nonincremental Release solution build: 0 warnings, 0 errors;
- all four noninteractive DemoHost modes and scripted Training Annex exit:
  successful;
- public API baseline, formatting verification, source inventory, Framework
  architecture tests, content-tree status, and `git diff --check`: passed.

### O4-R4 implementation gate

The typed-transition checkpoint adds an injected ailment transition policy
with supplied reject, refresh, replace, and standard strategies. The standard
strategy retains refresh-same and replace-exclusive behavior. Accepted and
rejected transitions carry affected ailment IDs and ordered before/after
changes. Action-end expiration, selected cleanup, passive evaluation, full
passive effects, and source identity now survive through action and encounter
results. Malformed passive events fail before an incomplete encounter payload
is published.

The implementation gate passed with:

- focused lifecycle, action, and mapper coverage: 154 passed; the broader
  lifecycle/encounter integration filter passed 149 (0 failed, 0 skipped in
  either run);
- complete solution: 1,565 passed, 0 failed, 0 skipped;
- strict nonincremental Release solution build: 0 warnings, 0 errors;
- all four noninteractive DemoHost modes and scripted Training Annex exit:
  successful;
- active content validation: 6 packs, 36 documents, and 98 qualified
  definitions passed schema, semantic, dependency, registration, and catalog
  checks; and
- formatting verification, 55 architecture/boundary tests, source inventory,
  content-tree status, and `git diff --check`: passed.

## Current Closure Decision

Order 4 is **design-approved and implementation is in progress**. O4-R2 through
O4-R4 are implemented pending the fresh O4-R10 review. The existing
implementation is useful and largely transactional, but the capability
matrix correctly remains `partial` until R5-R10 close the remaining application,
passive, startup, persistence, and audience-documentation work. No code should
be removed, and the documentation coverage entries should remain
`existing_unreviewed` until R2-R10 and the audience review are complete.
