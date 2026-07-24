# Status And Passive Lifecycle Order 4 Closure Review

**Review date:** 24 July 2026

**Reviewed revision:** `fe3657a0`

**Capability:** `status_and_passive_lifecycle`

**Conclusion:** three correctable contract gaps remain before Order 4 can be
closed

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
