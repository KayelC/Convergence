# Encounter Orchestration Order 6 Post-Refactor Owner-Closure Audit

Date: 9 August 2026  
Branch: `main`  
Reviewed revision: `21b0a016`  
Result: **no unresolved realistic reachable runtime defect found; Order 6 is ready for owner closure**

## Review Method

This was a fresh source-first review of the current Order 6 implementation
after the staged encounter-runner extraction. Earlier reviews, roadmaps, and
completion claims were not accepted as correctness evidence. They were read
only after the runtime trace, to compare the current code with the documented
design.

The review traced:

- request validation, initiative, battle start, and terminal short-circuiting;
- both supplied schedule policies and the structural schedule validator;
- phase-scoped turn-economy creation, authority checks, and liveness bounds;
- turn-start restrictions, command statuses, turn-end lifecycle, and schedule
  advancement;
- lifecycle transactions, departure reconciliation, completion, cancellation,
  event publication, and fault finalization;
- automated battle selection, prepared-assessment authority, restrictions,
  outcome mapping, and encounter knowledge;
- immutable event and result contracts; and
- the mechanics, developer, and technical Order 6 documentation.

Tests were then run to reproduce the contracts found in source. Passing tests
were not used as a substitute for reading the implementation.

## Findings

No high-, medium-, or low-severity runtime defect was reproduced in the
reviewed Order 6 scope. No contradiction was found between the current runtime
behavior and the three active audience documents.

### Documentation provenance note, not a defect

The audience-page review banners still cite O6-R47 or O6-R51. Those statements
remain true, and the later Stage 0-7 work was deliberately behavior-preserving,
so this is not inaccurate documentation. The post-refactor roadmap records the
structural extraction separately. When the owner formally closes Order 6, the
three banners and documentation-matrix reasons may be updated to cite this
audit as the newest source-first confirmation. That is traceability upkeep,
not a prerequisite runtime correction.

## Current Runtime Trace

### Request and battle start

`BattleEncounterRunner.RunCoreAsync` is now a 27-line orchestrator. It checks
cancellation and arguments, constructs explicit run state and context, runs
the battle-start phase, returns an early terminal result when appropriate, and
otherwise delegates to the scheduled-round loop.

The battle-start phase rejects duplicate runtime instance IDs before the first
encounter port call. Initiative receives detached participant snapshots and
must return every participating team exactly once. Actor passive reset,
`ActorCreated`, `BattleStarted`, initiative evidence, staged battle-start
lifecycle, reconciliation, completion, and schedule creation retain distinct
ordered boundaries.

Source:

- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L890)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L944)

### Schedule authority and liveness

Scheduling and turn economy remain separate authorities. Schedule requests
contain detached identity and availability evidence. Every accepted transition
must preserve policy and encounter identity, increment revision and sequence
exactly once, obey legal step pairings, and move round counters only at round
end. Exhausted accepted economy evidence cannot open another command window.

The runner also consumes an encounter-wide structural-transition budget before
each schedule start or advance. Within a phase, accepted turn windows and
consecutive free actions have separate limits. These bounds cover both
structural loops and no-cost command loops without making either supplied
scheduler mandatory.

Source:

- [`BattleEncounterScheduling.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterScheduling.cs#L562)
- [`BattleEncounterScheduling.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterScheduling.cs#L842)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1197)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2019)

### One scheduled command window

An unavailable scheduled actor advances the schedule without beginning a turn
or incrementing the accepted-window counter. An accepted window publishes
`TurnStarted`, stages turn-start lifecycle against a cloned participant graph,
checks economy authority, commits once, publishes validated lifecycle events,
and reconciles before a handler can act.

The handler cannot mutate the retained turn economy. Its port-owned events are
validated and recorded before status interpretation. `Cancelled`, `Faulted`,
and `Rejected` do not apply economy or owner-turn-end lifecycle. Only
`Executed` applies typed consumption. `None` must preserve exact economy state
and counts toward free-action liveness; other consumption stages and commits
turn-end lifecycle before `TurnEconomyChanged`. Reconciliation and
`TurnEnded` precede terminal evaluation or schedule advancement.

Source:

- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1354)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1595)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1650)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1722)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1767)

### Lifecycle and reconciliation

Framework lifecycle work is transactional. Participant states and lifecycle
clock state are staged and committed together; disposal restores the lifecycle
checkpoint when no commit occurs. Cancellation is checked before commits.

Reconciliation synchronizes host-backed state, selects one departure reason per
actor, runs departure lifecycle over one staged graph, resynchronizes after
mutation, repeats to a participant-count-bounded fixed point, announces each
uninterrupted defeat period once, and evaluates completion over detached
snapshots. Recovery releases both defeat authorities for a later defeat
period.

Source:

- [`BattleEncounterLifecycleTransaction.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterLifecycleTransaction.cs#L12)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L1931)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2092)

### Event, cancellation, fault, and result authority

Canonical events have typed payloads whose shape must match their event kind.
Port events are checked against the frozen participant graph and scheduled
actor. The runner assigns sequence numbers and records each event before sink
publication, so a failed observer cannot erase evidence or reuse a sequence.

Operational cancellation remains an exception boundary: the supplied token is
checked around ports and commits, and no synthetic terminal result is invented.
Typed command cancellation remains a gameplay result. Port failures become
stable typed faults. Fault finalization records the primary fault, attempts
battle-end lifecycle at most once after the structural battle-start boundary,
retains cleanup failure as secondary evidence, progressively disables a failed
event sink, and records one terminal `BattleEnded(Faulted)` event.

`BattleEncounterResult` contains detached actor snapshots and an immutable event
collection rather than live mutable participants.

Source:

- [`BattleEncounterEvents.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterEvents.cs#L500)
- [`BattleEncounterEvents.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterEvents.cs#L900)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L140)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2228)
- [`BattleEncounterRunner.cs`](../../src/Convergence.Framework/Encounters/BattleEncounterRunner.cs#L2629)

### Automated composition

`AutomatedBattleRunner` composes the canonical encounter runner instead of
maintaining a second battle loop. The selected action carries the assessment
prepared by the configured executor. Before execution, the adapter checks the
skill definition, actor, participant graph, environment, targets, lifecycle
boundaries, and executor authority. Restriction-only turns use the canonical
typed action identity and the same execution services. Terminal outcomes and
team-local encounter knowledge are mapped without resequencing encounter
events.

Source:

- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L506)
- [`AutomatedBattleRunner.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs#L646)
- [`AutomatedBattleTurnRestrictionResolver.cs`](../../src/Convergence.Framework/Encounters/AutomatedBattleTurnRestrictionResolver.cs#L178)

## Documentation Cross-Examination

| Audience | Current state | Source-aligned result |
|---|---|---|
| Mechanics | `reviewed` | Round, phase, actor-window, Back, typed cancellation, operational cancellation, defeat-period, and outcome behavior match source. |
| Developer guide | `reviewed` | Required composition, scheduler replacement rules, handler ownership, lifecycle responsibilities, event observation, async cancellation, automated composition, and Godot mapping match source. |
| Technical | `reviewed` | Authority map, scheduler protocol, command transaction, reconciliation fixed point, event ordering, terminal validation, cleanup, and snapshot boundaries match source. |

The diagrams were followed branch by branch against current source. In
particular, the command diagram correctly publishes validated command events
before the four-way command-status decision, routes only `Executed` into turn
economy, and skips owner-turn-end lifecycle for `None` consumption.

Reviewed documents:

- [`encounter-rounds-phases-and-turns.md`](../mechanics/encounter-rounds-phases-and-turns.md)
- [`encounter-orchestration.md`](../developer-guide/encounter-orchestration.md)
- [`encounter-orchestration-runtime.md`](../technical/encounter-orchestration-runtime.md)

## Adversarial Test Evidence

The focused source-adjacent tests cover:

- duplicate IDs before ports or mutation;
- malformed schedule transitions and exhausted-economy continuation;
- immutable terminal participant snapshots;
- cancellation before startup and at every lifecycle/handler boundary;
- event publication failure before and after committed lifecycle work;
- primary-fault preservation when publication or battle-end cleanup also fails;
- repeated defeat periods and bounded departure reconciliation;
- economy mutation before handler, during events, and during lifecycle;
- free-action and structural-transition liveness;
- both supplied scheduler models and post-command extension behavior;
- typed event shape, ownership, and immutable evidence;
- prepared automated assessments, restriction identity, terminal outcomes, and
  encounter knowledge.

Primary suites:

- [`BattleEncounterRunnerTests.cs`](../../tests/Convergence.Framework.Tests/SkillSystem/BattleEncounterRunnerTests.cs)
- [`BattleEncounterSchedulingContractTests.cs`](../../tests/Convergence.Framework.Tests/Encounters/BattleEncounterSchedulingContractTests.cs)
- [`TeamPhaseRoundRobinScheduleTests.cs`](../../tests/Convergence.Framework.Tests/Encounters/TeamPhaseRoundRobinScheduleTests.cs)
- [`AgilityOrderedBattleEncounterScheduleTests.cs`](../../tests/Convergence.Framework.Tests/Encounters/AgilityOrderedBattleEncounterScheduleTests.cs)
- [`BattleEncounterPostCommandSchedulingTests.cs`](../../tests/Convergence.Framework.Tests/Encounters/BattleEncounterPostCommandSchedulingTests.cs)
- [`BattleEncounterEventContractTests.cs`](../../tests/Convergence.Framework.Tests/Encounters/BattleEncounterEventContractTests.cs)
- [`CatalogBattleRuntimeTests.cs`](../../tests/Convergence.Framework.Tests/SkillSystem/CatalogBattleRuntimeTests.cs)

## Trusted Boundaries And Residual Risk

- Custom turn handlers, lifecycle ports without checkpoint support, and state
  synchronizers remain trusted mutation extensions. The runner contains their
  exceptions and validates returned framework evidence, but cannot roll back
  arbitrary scene, network, filesystem, or other host side effects.
- Runtime actors are deliberately live encounter state. Hosts must not run two
  concurrent mutation loops over the same actor graph.
- The synchronous `Run` wrapper is a compatibility entry point. UI and engine
  hosts should await `RunAsync`.
- `EncounterRunContext` still concentrates substantial orchestration code in
  one private nested implementation type. The Stage 0-7 extraction removed the
  1,640-line closure-heavy method and made state ownership explicit, but future
  feature work should prefer adding cohesive collaborators rather than growing
  this context indefinitely.

These are explicit integration and maintenance constraints. None produced a
realistic reachable defect in the reviewed framework-owned paths.

## Verification

- Runner-specific focus: **168 passed**, **0 failed**, **0 skipped**.
- Broader Order 6 focus: **299 passed**, **0 failed**, **0 skipped**.
- Full solution: **1,888 passed**, **0 failed**, **0 skipped**:
  - Framework: **1,703**;
  - DemoHost: **178**;
  - ContentValidator: **7**.
- Strict nonincremental solution build: **0 warnings**, **0 errors**.
- Formatting verification: passed.
- Aggregate Stage 0-7 diff check: passed.

## Closure Recommendation

The current implementation preserves one explicit authority for scheduling,
turn economy, lifecycle mutation, event history, completion, and automated
composition. The post-O6 extraction removed closure-owned mutable state without
changing those contracts. Current executable evidence reproduces every
reviewed hostile boundary, and all three audience documents describe the code
that now exists.

**Order 6 is ready for the owner's formal closure.**
