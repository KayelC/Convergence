# Status And Passive Lifecycle Order 4 Closure Review

**Review date:** 24 July 2026

**Reviewed revisions:** `fe3657a0` through `34d9a9e1`

**Capability:** `status_and_passive_lifecycle`

**Conclusion:** all reproduced findings are corrected; the final source and
documentation trace found no unresolved realistic defect in the supported
Order 4 scope

## Review Method

This review was performed from the current implementation, tests, schemas,
active content, and three audience documents. Earlier review conclusions and
checkpoint summaries were not treated as proof. The following paths were
traced directly:

- status lifetime construction and live timed-state mutation;
- ailment application, transitions, turn restrictions, recovery, and cleanup;
- explicit actor-turn, action, team-phase, round, and custom clocks;
- reserve lifecycle policy selection;
- passive targeting, recursion, activation counting, and transactions;
- encounter lifecycle adaptation and typed event mapping;
- persistence validation and aggregate restoration;
- schema-v7 DTO mapping and every active status-producing JSON record; and
- mechanics, developer, and technical lifecycle documentation.

A finding below is included only when there is an intended invariant, a
reachable public or authored path, a concrete consequence, and direct source
evidence. No conventional security vulnerability was found in this scope.

## Confirmed Strengths

- Application, transition, turn lifecycle, cleanup, passive dispatch, and
  encounter startup use staged actor state and commit atomically.
- Ailment behavior, conflicts, targeting, removal causes, and lifecycle clocks
  are typed rather than inferred from names or debug text.
- The canonical encounter port keeps team, phase, event, and round identities
  distinct and publishes ordered typed lifecycle evidence.
- Reserve state suspends by default, while the opt-in advancing policy accepts
  only an exact owning-team phase or round boundary.
- Passive fan-out, recursion protection, per-dispatch/per-target limits, and
  restored target identities are explicit and deterministic.
- Save contract v13 preserves lifetime and passive activation authority and
  validates it before aggregate restoration.
- The audience documents accurately explain the implemented runtime and
  explicitly disclose the current JSON authoring limitation.

## Findings And Correction Roadmap

### O4-R10-M1: Authored content cannot select status removal policy

**Severity:** Medium

**Invariant:** content-driven status behavior must be able to author both parts
of `StatusLifetimeDefinition`: its expiration clock and its removal profile.

**Reachable path:** schema-v7 ailment records expose only `defaultDuration`.
Status-producing effects expose only `duration`. `SkillSystemDtoMapper` then
silently chooses a supplied lifetime according to duration kind.

**Consequence:** all authored turn-counted ailments receive Field persistence,
and authored charge, shield, affinity Break, and affinity-override effects
receive fixed Deployment or Encounter persistence. A content author cannot
choose that one three-turn ailment ends at battle end while another survives,
even though the runtime contract supports the distinction. Hosts must bypass
the catalog with programmatic definitions to use an approved Order 4 rule.

**Evidence:**

- `Serialization/SchemaDtos.cs`: `AilmentDto.DefaultDuration` and effect
  `Duration` fields;
- `Serialization/SkillSystemDtoMapper.cs`:
  `MapAilmentLifetimeFromDuration`, `MapCharge`, `MapShield`,
  `MapBreakAffinity`, and `MapAffinity`;
- `schemas/content/v7/ailments.schema.json`; and
- `schemas/content/v7/shared.schema.json` status-producing effect definitions.

**Correction checkpoint:** publish schema v8 with one reusable authored
`lifetime` shape containing `expiration` and an explicit set of allowed typed
removal causes. Use it for ailment defaults and all status-producing effects.
Bump active packs to `0.8.0`, reject v7 through the existing pre-release clean
break policy, and test exact mapper preservation for every applicable status
family.

### O4-R10-M2: A parallel phase-end API bypasses reserve lifecycle policy

**Severity:** Medium

**Invariant:** explicit lifecycle clocks and the injected reserve policy are
the only authority for phase and round aging.

**Reachable path:** `IBattleDurationLifecycleService.ProcessPhaseEnd` remains
public beside `ProcessClock`. Its implementation calls `ProcessParticipants`
for every supplied actor and expires matching phase state without checking
deployment or `IBattleReserveLifecyclePolicy`.

**Consequence:** an integration using the apparent phase convenience method
ages reserve state under the default suspension policy. The canonical
encounter port is correct, but the framework exposes two public phase paths
with contradictory semantics.

**Evidence:**

- `Execution/BattleStatusLifecycle.cs`: `BattlePhaseEndLifecycleRequest`,
  `IBattleDurationLifecycleService.ProcessPhaseEnd`, and
  `BattleDurationLifecycleService.ProcessPhaseEnd`; and
- direct tests in `BattleStatusLifecycleTests` and
  `StatModifierExecutionIntegrationTests` that use the competing path instead
  of an explicit `TeamPhaseLifecycleClockBoundary`.

**Correction checkpoint:** remove the pre-release `ProcessPhaseEnd` contract
and request type. Convert direct tests and callers to `ProcessClock` with an
explicit team-phase boundary. Retain `ProcessActionEnd` because ordered action
execution owns that distinct boundary.

### O4-R10-L1: Battle duration can be configured never to expire

**Severity:** Low

**Invariant:** every finite clock-driven lifetime must permit
`StatusRemovalCause.DurationExpired`.

**Reachable path:** `StatusLifetimeDefinition` enforces that invariant for
Instant, Turn, and Phase durations, but omits `BattleDurationDefinition`. A
host can construct a battle duration whose removal profile allows neither
duration expiry nor battle-end removal and apply it through public runtime
state.

**Consequence:** `ExpireBattleDurations` skips the state and battle-end cleanup
also preserves it. A status authored in memory as lasting for the battle can
therefore survive indefinitely, contradicting its duration kind.

**Evidence:**

- `Content/StatusLifetimes.cs`: `StatusLifetimeDefinition` constructor; and
- `Execution/BattleRuntimeState.cs`: `ExpireBattleDurations` and the
  `DurationExpired` permission check in `ExpireDurations`.

**Correction checkpoint:** include `BattleDurationDefinition` in the finite
duration invariant and add constructor, live-state, cleanup, and persistence
regressions. Permanent duration remains the correct representation for state
without automatic clock expiry.

## Closure Sequence

1. Commit this independent review and correction roadmap.
2. Correct O4-R10-L1 as an isolated runtime-invariant commit.
3. Correct O4-R10-M2 as an isolated public lifecycle-contract commit.
4. Correct O4-R10-M1 as an isolated schema/content-authority commit.
5. Re-read current source and documents after the corrections.
6. If no finding remains, promote the three audience documents to reviewed,
   mark the capability complete, record executable evidence, and run the full
   release gate.

Order 4 remains open until step 6. Passing existing tests does not waive these
findings because the tests currently encode the competing phase API and fixed
schema mappings rather than challenging them.

## Post-Correction Re-Review At `8721a6b`

The three findings above are implemented:

- O4-R10-L1 now includes Battle duration in the finite-expiration invariant;
- O4-R10-M2 removed the competing phase-end API and routes phase and round
  advancement through explicit lifecycle clocks; and
- O4-R10-M1 published schema v8, maps exact authored removal causes, and moved
  all active clean packs to `0.8.0`.

A fresh trace of the corrected source found two additional public extension
boundary gaps. These are narrow integration defects rather than conventional
security vulnerabilities, but both violate established fail-closed runtime
invariants and therefore block final promotion.

### O4-R11-M1: Turn-restriction extension output is not validated

**Severity:** Medium

**Invariant:** every result crossing a host-supplied policy boundary must use
defined enum values and valid content IDs before staged lifecycle state is
committed.

**Reachable path:** `BattleTurnStartRestriction` and
`CustomAilmentTurnBehaviorResult` accept an undefined
`BattleTurnStartOutcome`. `BattleStatusLifecycleService.ProcessTurnStart`
trusts the result returned by `IBattleTurnRestrictionPolicy`, clears Guard on
the staged actor, builds an event, and commits the transaction.

**Consequence:** a defective policy can return an undefined outcome as an
apparently accepted turn-start result. Guard clearing becomes live before the
encounter runner later faults while interpreting the impossible restriction.
Invalid limited-action or source IDs can likewise escape the policy boundary.

**Evidence:**

- `Execution/BattleStatusLifecycle.cs`:
  `BattleTurnStartRestriction`, `CustomAilmentTurnBehaviorResult`, and
  `ProcessTurnStartCore`; and
- the absence of a malformed custom-policy regression in
  `BattleStatusLifecycleTests`.

**Correction checkpoint:** validate outcomes and all supplied action/source
IDs in the immutable result constructors, then prove malformed custom behavior
and restriction policies roll back Guard and return no live mutation.

### O4-R11-L1: Ailment requests silently collapse runtime-ID collisions

**Severity:** Low

**Invariant:** one runtime instance ID identifies one actor object throughout
an execution transaction; a conflicting graph must reject deterministically.

**Reachable path:** `BattleAilmentApplicationRequest` snapshots participants
with `DistinctBy(participant.InstanceId)`. If two different actor objects use
the same ID, whichever object appears second is discarded before
`RuntimeActorExecutionTransaction` can enforce the global identity rule.

**Consequence:** application gates, conditions, and extension services can
observe an order-dependent participant graph instead of a typed rejection.
The same malformed graph is correctly rejected by other canonical execution
transactions.

**Evidence:**

- `Execution/BattleStatusLifecycle.cs`:
  `BattleAilmentApplicationRequest` participant capture; and
- `Execution/RuntimeActorExecutionTransaction.cs`: the collision check that
  the prior `DistinctBy` can bypass.

**Correction checkpoint:** preserve unique object references in the request,
reject null entries, and let the execution transaction reject two objects that
claim one runtime ID before any policy or mutation runs.

### Documentation corrections required at closure

- `docs/developer-guide/status-passive-lifecycle.md` still labels the passive
  targeting example as schema v7 even though active content now uses v8.
- `docs/mechanics/status-passive-lifecycle.md` overstates transactional scope
  by promising rollback after any event-sink publication failure. Framework
  actor mutation is transactional through policy, execution, lifecycle, and
  encounter ingress; an external sink is not a transactional resource and may
  fail after committed evidence is produced. The technical startup diagram
  must make the same boundary explicit.

After both runtime corrections, the lifecycle source and all three audience
documents must be re-read again before capability or documentation promotion.

## Follow-Up Finding From The Corrected Passive Trace

### O4-R11-M2: Passive-dispatch extension output can commit malformed evidence

**Severity:** Medium

**Invariant:** a host-supplied passive dispatcher must return structurally valid
typed activation evidence before its staged actor mutations can commit.

**Reachable path:** `IPassiveTriggerDispatcher` is a public extension boundary.
`PassiveTriggerExecutionResult` currently accepts undefined
`PassiveTriggerOutcome` values, negative trigger indexes, empty skill/event/target
IDs, and null collection members. `ProcessTurnEnd` maps every outcome other than
`Executed` to `PassiveEvaluated` and then commits the surrounding actor
transaction.

**Consequence:** a defective custom dispatcher can mutate staged actor state,
return semantically impossible activation evidence, and have both accepted as a
successful turn-end lifecycle result. The host then receives an apparently
ordinary evaluation event whose typed payload is invalid.

**Evidence:**

- `Execution/PassiveRuntime.cs`: `PassiveTriggerExecutionResult` and
  `PassiveTriggerDispatchResult` accept unvalidated extension output; and
- `Execution/BattleStatusLifecycle.cs`: `ProcessTurnEndCore` and
  `AddPassiveEvents` trust the returned activation outcome and identifiers.

**Correction checkpoint:** validate every scalar and collection member in the
passive result records, including record-clone setters, and prove a malformed
custom dispatcher cannot commit staged resource mutation through turn-end or
battle-start lifecycle ingress.

## Final Correction Record

The post-correction findings were implemented as isolated commits:

- `b294c5cb` validates turn-restriction and custom turn-behavior outcomes,
  identifiers, and clone setters before staged Guard clearing can commit;
- `bbe47dfe` preserves distinct participant objects so the canonical execution
  transaction rejects runtime-ID collisions before an ailment policy runs; and
- `34d9a9e1` validates passive activation outcomes, identifiers, trigger
  indexes, effect members, dispatch members, and clone setters before lifecycle
  ingress can commit staged mutations.

Focused regressions prove malformed custom restrictions and passive dispatchers
leave live actors unchanged, and conflicting ailment participant graphs never
reach a host policy. The closing documentation correction also distinguishes
Framework actor-state transactions from external event-sink side effects.

## Final Source And Documentation Re-Review

The closing trace re-read the current lifetime definitions, live timed-state
mutators, ailment transaction, turn-start and turn-end lifecycle, explicit
clocks, cleanup, passive dispatcher, encounter lifecycle port, persistence
validation, schema-v8 mapper, active content, tests, and all three audience
documents. It did not use the earlier conclusion as proof.

The supported invariant now holds end to end:

1. definitions author expiration separately from permitted removal causes;
2. every finite duration has a reachable expiry cause;
3. live state and restored state reject malformed durations;
4. actor, action, phase, round, and custom boundaries advance through one
   explicit clock model with configured reserve behavior;
5. ailment and passive extension outputs validate before commit;
6. lifecycle mutations and activation counts commit atomically within
   Framework actor state; and
7. immutable typed results and events expose the committed transition without
   treating external host publication as a transactional resource.

No additional realistic reachable defect was reproduced. Order 4 may therefore
promote after the complete release gate recorded below.

## Complete Release Gate

The final corrected revision passed:

- focused lifecycle, immutability, encounter, schema, persistence, and
  documentation-contract coverage: 436 passed, 0 failed, 0 skipped;
- complete solution: 1,618 passed, 0 failed, 0 skipped (1,438 Framework, 173
  DemoHost, and 7 ContentValidator tests);
- locked dependency restore with NuGet vulnerability auditing: passed;
- strict nonincremental Release build with warnings as errors: 0 warnings and
  0 errors, including the Godot reference project;
- formatting verification and trimming analysis: passed with 0 warnings;
- Framework coverage: 90.69% lines and 76.22% branches, above the 90%/70%
  release thresholds;
- active content validation: 6 packs, 36 documents, and 98 qualified
  definitions;
- all four noninteractive DemoHost modes and PTY-scripted Training Annex exit:
  successful;
- real Godot 4.7.1 .NET headless smoke: exit 0 with
  `CONVERGENCE_GODOT_SMOKE_OK` and save contract 13; and
- architecture, API, documentation-link, forbidden-reference, active-content,
  and `git diff --check` gates: passed.

The first sandboxed Godot attempt could not create `user://logs` and entered
the engine's native Windows crash handler. That blocked process was terminated
and the same checked-in executable was rerun with normal user-data access; the
real smoke then completed successfully. This environmental first attempt is not
counted as a product pass.

## Closure Decision

Order 4 is formally complete. The capability matrix records
`status_and_passive_lifecycle` as `complete`; mechanics, developer-guide, and
technical documentation are `reviewed`. The feature remains an optional,
host-neutral Framework module with focused rather than end-to-end presentation
coverage. Order 5, `battle_knowledge`, is next.
