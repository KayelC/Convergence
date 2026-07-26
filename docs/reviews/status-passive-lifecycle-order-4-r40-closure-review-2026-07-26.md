# Status And Passive Lifecycle Order 4 R40 Closure Review

Date: 26 July 2026

Reviewed revision: `e697b87`

Verdict: **closed; no unresolved realistic reachable defect found**

## Review Independence

This review did not treat an earlier audit, completion statement, or roadmap as
proof. It re-read the current implementation and followed live state from
authored definitions through execution, lifecycle mutation, encounter clocks,
persistence, and presentation-neutral evidence. The earlier reports were used
only after the source trace to identify which active maturity records required
reconciliation.

A concern qualified as a finding only when it had all four of these properties:

1. an intended invariant established by current code or confirmed active
   documentation;
2. a realistic path through a supported Framework contract;
3. a concrete gameplay, state-integrity, host-integration, or crash consequence;
4. reproducible source or executable evidence.

No concern met that threshold after the corrected source and tests were read.

## Source Scope

The review traced these current authorities:

- ailment definitions, lifetime/removal profiles, triggers, restrictions, and
  combat modifiers;
- active ailment, other-status, guard, shield, charge, affinity, stat-modifier,
  and passive-activation runtime state;
- ailment gate, application, replacement, refresh, resistance, chance, and
  exclusivity transitions;
- turn-start scheduling and restriction precedence;
- turn-end passive dispatch, ailment triggers, recovery, duration advancement,
  and stat-modifier ticking;
- action, actor-turn, team-phase, round, and custom lifecycle clocks;
- reserve suspension and explicit reserve-advancement policies;
- cleanup and encounter departure causes;
- passive target freezing, activation limits, recursion control, replacement
  dispatch, and typed execution evidence;
- encounter lifecycle transactions, cancellation points, event mapping, and
  departure integration;
- actor snapshot integrity, save validation, restore validation, and immutable
  public lifecycle results; and
- both supplied timed stat-modifier policies and their lifecycle cursor rules.

The central corrected files were:

- [`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs)
- [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs)
- [`BattleLifecycleClocks.cs`](../../src/Convergence.Framework/Execution/BattleLifecycleClocks.cs)
- [`StatModifierExecution.cs`](../../src/Convergence.Framework/Execution/StatModifierExecution.cs)
- [`TimedExclusiveStatModifierPolicy.cs`](../../src/Convergence.Framework/Runtime/TimedExclusiveStatModifierPolicy.cs)
- [`TimedContributionStatModifierPolicy.cs`](../../src/Convergence.Framework/Runtime/TimedContributionStatModifierPolicy.cs)
- [`RuntimeActorSnapshotIntegrity.cs`](../../src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs)

## Corrected Sequence Authority

The canonical encounter port now stores one committed sequence per lifecycle
event ID. It no longer hides actor-local owner-turn or team-local phase counters
behind a public boundary containing only event ID and sequence.

The resulting invariant is coherent:

1. `GetActiveStatModifierBoundaries` peeks the next owner-turn event sequence
   without consuming it.
2. Effect execution carries that pending boundary to every selected target.
3. Successful owner-turn completion uses the same sequence and then commits it.
4. Only the acting actor is ticked at actor-turn end, regardless of how many
   targets retained the application boundary.
5. A later target turn receives a greater sequence and decrements once. A
   larger numeric gap remains one observed target occurrence, not multiple
   elapsed duration units.
6. Team phases and rounds advance the same authority when they share an event
   ID. Distinct event IDs remain independent.
7. A pre-cancelled or rejected lifecycle operation does not advance actor state
   or the committed sequence.

The supplied timed policies already compare monotonic identity rather than
subtracting sequence deltas. The corrected port therefore resolves both the
cross-target fault and shared-event divergence without changing authored
duration values or public contracts.

## Regression Evidence

[`StatModifierExecutionIntegrationTests.cs`](../../tests/Convergence.Framework.Tests/SkillSystem/StatModifierExecutionIntegrationTests.cs)
now proves the corrected integration for both timed-exclusive and
timed-contribution policies:

- self application retains the complete duration at its own application
  boundary;
- ally buffs and enemy debuffs use the command's pending event boundary;
- source and target may have unequal prior turn histories;
- an unrelated actor may advance the event sequence between application and
  the target's next turn without causing extra decrement;
- shared team-phase event IDs advance ordinary statuses and stat modifiers at
  the same rate;
- a phase and round sharing one event ID use one sequence stream; and
- cancellation preserves the pending owner-turn boundary and live duration.

These are integration tests through the real lifecycle port, skill executor,
target resolver, runtime actor state, and supplied policies. They do not replace
the clock service with a test double.

## Broader Lifecycle Health

The fresh trace found the surrounding lifecycle internally coherent:

- application is staged before guard, resistance, chance, refresh, or
  exclusivity mutation commits;
- turn-start and turn-end schedules identify exact ailment instances, so a
  removed or replaced instance cannot act later from a stale enumeration;
- lifecycle handlers and passive replacement dispatchers cannot commit partial
  actor graphs when execution, evidence validation, or event validation fails;
- reserve behavior separates deployment selection from authored
  `suspendWhileReserve` behavior;
- cleanup causes respect each status lifetime's removal profile;
- passive activation keys, target counts, trigger indexes, and event IDs are
  validated during restoration;
- ailment exclusivity and retained duration domains are validated before a
  runtime actor is exposed; and
- public event/result collections remain defensive immutable snapshots.

## Documentation Review

The three audience layers now agree with the implementation:

- [mechanics](../mechanics/status-passive-lifecycle.md) explains what players
  observe, including target-specific duration advancement and shared events;
- [developer guidance](../developer-guide/status-passive-lifecycle.md) defines
  event-keyed sequence ownership, propagation, commit, cancellation, and custom
  scheduler duties; and
- [technical documentation](../technical/status-passive-lifecycle.md) separates
  sequence identity from actor/phase/round participant selection and diagrams
  the corrected flow.

The corresponding stat-modifier pages provide the exact same-boundary and
cross-target examples, explain sequence gaps, and prohibit local counters behind
a shared event ID. Mermaid flowcharts use bounded vertical layouts and match the
source transition order.

## Deliberate Boundaries, Not Findings

The following remain explicit product boundaries rather than hidden defects:

- host-side irreversible operations cannot be rolled back by Framework actor
  transactions;
- custom lifecycle event producers remain responsible for valid typed payloads;
- field time advances battle state only when a host explicitly dispatches a
  configured clock;
- mid-command encounter suspension and deterministic replay remain deferred;
  and
- a custom lifecycle scheduler must provide its own event-keyed sequence
  authority when it does not use the canonical port.

## Verification

- Focused corrected integration class: **40 passed**, 0 failed, 0 skipped.
- Focused lifecycle, encounter, and documentation gates: passed.
- Full solution: **1,673 passed** (1,493 Framework, 173 DemoHost, 7 content
  validator), 0 failed, 0 skipped.
- Framework coverage: **90.69% lines**, **76.44% branches**.
- Strict Release nonincremental build: **0 warnings, 0 errors**.
- `dotnet format --verify-no-changes`: passed.
- Active content: **6 packs, 36 documents, 98 qualified definitions** passed
  schema, deserialization, semantic, dependency, registration, and catalog
  validation.
- All four noninteractive DemoHost modes exited `0`.
- Scripted Training Annex play accepted `Exit` and terminated without pending
  input.
- The official Godot 4.7.1 .NET headless smoke emitted
  `CONVERGENCE_GODOT_SMOKE_OK` after running content, action, modifier,
  encounter, save, and rejection paths.
- API, schema, terminology, documentation-link, source-boundary, and archive
  guards passed inside the full suite.
- `git diff --check` passed.

The first sandbox-confined local Godot launch could not create its `user://`
log and the engine crashed before project startup. Re-running the same official
binary with access to its host-owned user directory completed successfully.
That failure was an execution-sandbox limitation, not a Framework or sample
failure.

## Closure

Order 4 is formally complete at this reviewed revision. The executable
Framework Capability Matrix may promote `status_and_passive_lifecycle` from
`partial` to `complete`; its mechanics, developer-guide, and technical entries
may return from `existing_unreviewed` to `reviewed`. No removal or archive
authorization follows from this documentation milestone.

Order 5, `battle_knowledge`, becomes the next collaborative documentation
subject.
