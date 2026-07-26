# Status And Passive Lifecycle Order 4 Third Independent Audit

Date: 26 July 2026

Reviewed revision: `26498370`

Verdict: **reopened; one high runtime correction, one medium runtime
correction, and one documentation reconciliation remain**

## Review Method

This audit treated earlier reports, roadmaps, and completion statements as
historical context only. Findings were derived by tracing current source from
content definitions through live actor state, action execution, status
lifecycle, encounter integration, persistence, schemas, and the mechanics,
developer, and technical documentation.

A concern was retained only when it had:

1. an intended invariant established by current code or active documentation;
2. a realistic path through a supported public contract;
3. a concrete observable consequence; and
4. reproducible source or executable evidence.

Two temporary regression probes were used to verify the findings below. Both
were removed before the green verification gate and have no working-tree diff.

## Findings

### O4-H2: actor-local owner-turn sequences make cross-target timed modifiers unsafe

**Invariant.** `StatModifierLifecycleBoundary` identifies one counted clock by
event ID plus a positive monotonic sequence. An application made during the
currently active boundary is stamped with that identity so only completion of
that exact boundary is ignored. A later occurrence must decrement once. This is
the documented foundation for timed buffs and debuffs.

**Reachable path.** The canonical encounter lifecycle port does not create one
sequence stream for the `owner_turn_end` event. It stores a separate counter for
each acting actor and exposes that actor's next value to the complete action:

- [`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs#L21-L22)
- [`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs#L90-L119)

`EffectExecutionEnvironment` then supplies that same action-wide boundary to
every stat-modifier target. `StatModifierExecution` stamps it onto the target's
retained state without identifying which actor owned the sequence:

- [`ExecutionContracts.cs`](../../src/Convergence.Framework/Execution/ExecutionContracts.cs#L597-L601)
- [`StatModifierExecution.cs`](../../src/Convergence.Framework/Execution/StatModifierExecution.cs#L57-L74)

When another actor later completes its own turn, the port emits that actor's
local sequence. The timed policies correctly reject an older value and treat an
equal value as the already-observed application boundary:

- [`TimedExclusiveStatModifierPolicy.cs`](../../src/Convergence.Framework/Runtime/TimedExclusiveStatModifierPolicy.cs#L169-L201)
- [`TimedContributionStatModifierPolicy.cs`](../../src/Convergence.Framework/Runtime/TimedContributionStatModifierPolicy.cs#L192-L227)

This path is ordinary gameplay. Ally buffs, enemy debuffs, and multi-target
stage effects all apply timed state to an actor other than the command owner.
Action Token phases can also let one actor accumulate more owner-turn events
than another.

**Consequence.** If source and target counters happen to be equal, the target's
first later turn is mistaken for the application boundary and the modifier
lasts one target turn too long. If the source counter is ahead, the target's
turn-end lifecycle rejects the older boundary and the encounter enters its
typed fault path. The problem affects both supplied timed modifier policies;
persistent staged modifiers are not clocked and are unaffected.

**Reproduction.** A temporary integration probe used the real
`BattleStatusEncounterLifecyclePort` and timed-contribution policy. It completed
one source turn, obtained the source's next `owner_turn_end` boundary at
sequence `2`, applied a three-turn attack modifier to another actor, and then
processed that target's first turn end. The target received sequence `1` and
the call failed with:

```text
Stat-modifier lifecycle transition was rejected:
Lifecycle boundaries must be delivered in monotonic order.
```

The stack passed through `TimedContributionStatModifierPolicy.Tick`,
`BattleDurationLifecycleService.TickModifiers`, and
`BattleStatusEncounterLifecyclePort.ProcessTurnEndAsync`. The probe was removed
after reproduction.

### O4-M7: team-local phase sequences can reuse one public boundary identity

**Invariant.** Ordinary counted statuses and timed stat modifiers authored to
the same lifecycle event must observe the same number of clock occurrences.
The event ID plus sequence must not identify two different phase completions as
one occurrence.

**Reachable path.** `ExplicitBattleEncounterLifecycleClockPolicy` requires one
mapping per team but accepts the same lifecycle event ID in several mappings:
[`BattleEncounterLifecycleClocks.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterLifecycleClocks.cs#L49-L70).
The canonical port then counts phase sequences by team ID, not by lifecycle
event ID:
[`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs#L123-L157).

Two teams mapped to `shared_phase_end` therefore both emit
`StatModifierLifecycleBoundary(shared_phase_end, 1)`. Ordinary timed status
state is advanced whenever `ProcessClock` receives the matching event, while a
timed modifier treats the second pair as an idempotent duplicate:

- [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs#L974-L997)
- [`StatModifierPolicies.cs`](../../src/Convergence.Framework/Runtime/StatModifierPolicies.cs#L1031-L1036)
- [`TimedContributionStatModifierPolicy.cs`](../../src/Convergence.Framework/Runtime/TimedContributionStatModifierPolicy.cs#L219-L229)

**Consequence.** Two status families authored to one event silently age at
different rates. With other clock-domain collisions, an older sequence can
instead reject the complete lifecycle transition.

**Reproduction.** A temporary focused probe applied a two-occurrence ordinary
status and a two-occurrence timed stat contribution to one actor, both using
`shared_phase_end`. It processed player and enemy phase boundaries, each at
sequence `1`, matching the values currently produced by the accepted policy
shape. The ordinary status expired after the two events; the modifier retained
one turn and stage `+1`. The probe was removed after reproduction.

### O4-D7: the audience documents do not expose the sequence-scope requirement

The three documentation audiences correctly describe event-plus-sequence
idempotence, explicit team/phase/event IDs, and same-boundary application. The
developer guide also recommends distinct team event IDs. They do not explain
that the canonical owner-turn counter is actor-local while an action boundary
is stamped onto every target, and they do not identify the resulting
cross-target restriction.

The technical statement that the port owns per-actor owner-turn sequences
matches current code but not the required cross-target behavior:
[`stat-modifier-policy-runtime.md`](../technical/stat-modifier-policy-runtime.md#L225-L235).
The capability and documentation matrices therefore overstate closure when
they report no known gap.

Documentation should be reconciled only after the corrected sequence authority
is chosen. The intended rule is that application and expiration consume one
coherent monotonic occurrence stream for the authored lifecycle event,
regardless of which actor was the source or target.

## Verified Healthy Paths

The source trace found no separate realistic reachable defect in:

- ailment gate, resistance, chance, refresh, exclusivity, and replacement;
- exact-instance turn-start and turn-end ailment schedules;
- supplied restriction precedence, recovery, reserve suspension, and cleanup;
- Instant, counted-turn, Phase, Battle, Permanent, and custom clock handling;
- staged ailment application, turn lifecycle, cleanup, and encounter
  cancellation-before-commit;
- passive target freezing, activation limits, recursion control, replacement
  dispatcher evidence validation, and atomic execution;
- typed lifecycle transition/event collections and specialized passive event
  mapping;
- status lifetime, ailment exclusivity, passive enabled state, activation-key,
  and per-target activation restore validation;
- schema-v8 status lifetime and passive target contracts; or
- ailment combat-profile composition and saturating arithmetic.

The existing trust boundaries also remain accurately bounded. Framework actor
transactions cannot roll back irreversible host-side work, generic status
event mapping does not claim to prove every custom field combination, and
mid-command encounter suspension remains deferred rather than silently
supported.

## Documentation Alignment

Apart from O4-D7, the mechanics, developer, and technical pages agree with the
implementation on application, turn restrictions, exact-instance scheduling,
turn-end order, reserve behavior, duration kinds, cleanup causes, passive
targeting/counting, atomic rollback, persistence, and combat-profile
composition.

The sequence defect crosses the Order 4 status lifecycle and the Order 1 stat
modifier policy surface. The affected documentation entries are returned to
`existing_unreviewed` together because correcting only one audience would leave
the public integration contract ambiguous.

## Correction Roadmap

| Checkpoint | Status | Scope | Required evidence |
|---|---|---|---|
| O4-R37 | Complete | Record this independent audit and reopen the executable maturity records | Review document, matrix consistency, and clean baseline |
| O4-R38 | Complete | Replace actor-local and team-local stat-modifier clock identities with one coherent monotonic sequence authority per lifecycle event | Cross-target ally/enemy modifiers, uneven actor-turn counts, same-boundary self application, shared team event, shared phase/round event, and cancellation regressions |
| O4-R39 | Complete | Reconcile mechanics, developer, and technical guidance | Exact source/target duration examples, sequence ownership, custom scheduler requirements, diagrams, and coverage-matrix review |
| O4-R40 | Complete | Fresh closure review | New source trace, full release gate, documentation links, and matrix promotion only if no reachable defect remains |

At audit time, until O4-R40 passed, `status_and_passive_lifecycle` was `partial`,
its three documentation entries were `existing_unreviewed`, and Order 5 did
not become the active collaborative subject.

## Verification At Audit Time

- Focused lifecycle, passive, persistence, encounter, and stat-modifier gate:
  **356 passed**, 0 failed, 0 skipped.
- Full solution: **1,663 passed** (1,483 Framework, 173 DemoHost, 7 content
  validator), 0 failed, 0 skipped.
- Strict Release nonincremental solution build: **0 warnings, 0 errors**.
- `dotnet format --verify-no-changes`: passed.
- Active content validation: **6 packs, 36 documents, 98 qualified
  definitions** passed schema, deserialization, semantic, dependency,
  registration, and catalog checks.
- The four noninteractive DemoHost modes exited `0`.
- Architecture, documentation-link, API, schema, and forbidden-reference tests
  passed inside the full suite.
- The two deliberate failing probes were removed before the green gate and the
  reviewed source/test tree is unchanged.

## Closure Recommendation

Do **not** formally close Order 4 at revision `26498370`. The broader lifecycle
implementation is healthy, but O4-H2 is reachable through ordinary timed ally
buffs or enemy debuffs and can fault a canonical encounter. Complete O4-R38
through O4-R40 before promotion.

## Correction Outcome

O4-R38 corrected sequence authority, O4-R39 reconciled all three audience
documents, and the
[O4-R40 closure review](status-passive-lifecycle-order-4-r40-closure-review-2026-07-26.md)
re-read the corrected implementation without finding an unresolved realistic
reachable defect. The recommendation above remains the correct verdict for the
originally reviewed revision; Order 4 is complete at the O4-R40 revision.
