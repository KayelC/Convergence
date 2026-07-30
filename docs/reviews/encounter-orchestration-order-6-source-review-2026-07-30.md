# Encounter Orchestration Order 6 Source Review And Approved Roadmap

**Date:** 30 July 2026  
**Capability:** `encounter_orchestration`  
**Source baseline:** `f72d36f0` (`Order 5 Completed`)  
**Owner-decision status:** all eight discussion decisions approved  
**Implementation status:** approved and planned; no Order 6 runtime correction
has been implemented by this record

## Purpose

This record preserves the complete owner discussion that opened Documentation
Order 6. It is both:

- a source-review record of the encounter orchestration implementation at the
  stated baseline; and
- the approved roadmap authority for the Order 6 implementation and
  documentation cycle.

The verbatim discussion is retained below so later implementation work cannot
quietly reinterpret the owner's decisions. The approved decision ledger and
ordered checkpoints after the transcript translate that discussion into an
auditable development sequence.

## Review Method

The source review was performed from current active code and tests. Archived
reviews and historical summaries were not used as implementation evidence.
The inspected active boundaries included:

- `BattleEncounterRunner`;
- `BattleEncounterEvents`;
- `BattleEncounterLifecycleTransaction`;
- `BattleStatusEncounterLifecyclePort`;
- `AutomatedBattleRunner`;
- `AutomatedBattleTurnRestrictionResolver`;
- `BattleEncounterRunnerTests`;
- the active capability and documentation matrices; and
- the current mechanics, developer, and technical guidance that touches
  encounter scheduling.

The focused encounter verification passed 115 tests with no failures or skips.

## Verbatim Discussion

### Owner Opening Request

> Excellent, let's proceed with Discussing Order 6 and what it entails, explain it to me, highlight it's problems, and dialogue with me so that we may discuss how we can move forward with it's development.

### Source Review And Recommendations

> **Order 6 In Plain English**
>
> Order 6 is the battle director. It does not calculate damage or decide what a skill does. Instead, it coordinates the systems completed in Orders 1–5:
>
> 1. Accept battle participants.
> 2. Determine initiative.
> 3. Run battle-start passives.
> 4. Start rounds and team phases.
> 5. Choose whose command window opens.
> 6. Run turn-start ailments/restrictions.
> 7. Ask the host or AI for a command.
> 8. Execute the command through the action system.
> 9. Apply Action Token consumption.
> 10. Run turn-end, phase-end, and round-end lifecycle.
> 11. Detect defeat, escape, cancellation, or victory.
> 12. Publish typed events for Godot or another host.
>
> Godot remains responsible for menus, animations, scene objects, and deciding whether a human or AI supplies the command.
>
> **What Already Works**
>
> The existing `BattleEncounterRunner.cs` is substantial and generally defensive:
>
> - duplicate actor IDs are rejected;
> - initiative must return every participating team exactly once;
> - lifecycle mutations are staged transactionally;
> - cancellation is checked before important commits;
> - host-port exceptions become typed encounter faults;
> - Action Token state is protected from unauthorized mutation;
> - infinite free-action and command loops are bounded;
> - final actor results are detached snapshots;
> - lifecycle, action, defeat, and terminal events are ordered;
> - the automated runner now uses the canonical encounter runner.
>
> The focused encounter gate passed: **115 tests, 0 failures, 0 skipped**.
>
> **Confirmed Problems And Decisions**
>
> 1. **Scheduling is currently hardcoded**
>
> The loop directly performs team phases and rotates actors round-robin in `BattleEncounterRunner.cs`.
>
> That supports the current phase-based battle model, but cannot support:
>
> - agility-ordered actors across both teams;
> - an immediate bonus action for the same actor;
> - interruption-based scheduling;
> - a different definition of when an actor turn begins or ends.
>
> My recommendation is to introduce an encounter-scheduling policy family:
>
> - `TeamPhaseRoundRobin`: preserves current behavior and remains the supplied default.
> - `AgilityOrdered`: actors act according to resolved Agility across teams.
> - A generic post-command scheduling decision that can later support a Bonus Action/One-More-style policy without rewriting the runner again.
>
> Turn economy would continue deciding **how many opportunities remain**. Scheduling would decide **who receives the next opportunity**.
>
> 2. **Lifecycle completion is not checked after every mutating boundary**
>
> After turn-start lifecycle commits, the runner proceeds directly to the command handler. After phase-end and round-end lifecycle commits, it proceeds to the next phase or round.
>
> A valid custom lifecycle port could defeat or undeploy an actor at one of those boundaries. The actor could then receive a command, or the encounter could continue after a team has already lost.
>
> I recommend one canonical reconciliation operation after every committed lifecycle boundary:
>
> - synchronize participants;
> - process departures;
> - announce newly defeated actors;
> - evaluate completion;
> - terminate immediately when appropriate.
>
> This is a real correctness correction, not optional hardening.
>
> 3. **The structural event stream is incomplete**
>
> `BattleEncounterEvents.cs` has `TurnStarted` and `RoundStarted`, but no corresponding `TurnEnded` or `RoundEnded`.
>
> Also, `CompletedRounds` is set when a round starts. Winning halfway through round one therefore reports one “completed” round.
>
> I recommend adding explicit end events and separating:
>
> - current/final round number;
> - number of fully completed rounds.
>
> That gives Godot reliable animation and UI boundaries without inferring them from the next event.
>
> 4. **Completion results need stronger validation**
>
> `BattleEncounterCompletion` currently permits contradictory shapes, such as:
>
> - complete victory without a winning team;
> - incomplete result with a winner;
> - undefined outcome enum values;
> - a winner that is not a participating team.
>
> These should become stable typed faults at the completion-policy boundary.
>
> 5. **Cancellation has two different meanings**
>
> Current behavior is:
>
> - `BattleEncounterCommandResult.Cancelled()` intentionally ends the encounter and runs battle-end cleanup.
> - Cancelling the .NET `CancellationToken` throws `OperationCanceledException` and aborts the operation.
>
> I recommend preserving this distinction:
>
> - menu `Back` remains inside the host’s command-selection loop;
> - intentional “end encounter” returns typed cancellation;
> - scene shutdown/application cancellation uses the token and aborts.
>
> The missing piece is precise documentation.
>
> 6. **Command rejection currently faults the encounter**
>
> This is deliberate and defensible. A normal invalid player selection should never leave the turn handler. The host should show the reason and ask again. If the handler returns `Rejected`, it means assessment and execution disagreed or the host violated the command contract.
>
> I recommend retaining this rule and documenting it prominently.
>
> 7. **The automated wrapper duplicates and loses encounter information**
>
> `AutomatedBattleRunner.cs` exposes a synchronous wrapper and maps typed encounter events into a smaller message-heavy event model.
>
> I recommend retaining automated battle as a convenience service but modernizing it to:
>
> - expose an asynchronous path;
> - preserve canonical typed events;
> - avoid maintaining a second incomplete event vocabulary.
>
> 8. **The capability matrix overstates completion**
>
> The matrix calls encounter orchestration `complete` with no gaps, while the source and active turn-economy documentation explicitly defer scheduler modularity. Meanwhile, its documentation coverage correctly remains unreviewed/missing.
>
> Order 6 should temporarily classify the capability as `partial`, then restore `complete` only after the agreed scheduler scope, correctness fixes, and three-audience documentation are finished.
>
> **Recommended Scope**
>
> I recommend Order 6 implement:
>
> - a replaceable scheduling contract;
> - the existing team-phase scheduler as the default;
> - an agility-ordered supplied scheduler;
> - a future-compatible bonus-action scheduling seam;
> - post-lifecycle reconciliation at every boundary;
> - complete structural events;
> - validated completion results;
> - clarified cancellation/rejection semantics;
> - canonical asynchronous automated execution;
> - dedicated mechanics, developer, and technical documentation with state diagrams.
>
> That would make encounter orchestration genuinely modular rather than merely well-tested for one battle structure. No files were changed during this discussion.

### Owner Approval

> 1. approved
>
> 2. approved
>
> 3. approved
>
> 4. approved
>
> 5. approved
>
> 6. approved
>
> 7. approved
>
> 8. approved
>
> You are to generate a report with all of that was discussed, word for word, I'd like this entire prompt to be saved, of course your recommendations would now reflected their approved state. Proceed accordingly.

## Approved Decision Ledger

| ID | Approved decision | Status |
|---|---|---|
| O6-D1 | Replace the hardcoded scheduling loop with a policy family. Supply the existing team-phase round-robin behavior as the default, supply agility-ordered scheduling, and include a post-command extension seam suitable for a future immediate bonus-action policy. Turn economy continues to count opportunities; scheduling chooses recipients and boundaries. | Approved |
| O6-D2 | Reconcile encounter state after every committed lifecycle boundary by synchronizing participants, processing departures, announcing new defeats, evaluating completion, and terminating immediately when required. | Approved |
| O6-D3 | Add explicit turn-end and round-end structural events. Distinguish the final/current round number from the number of fully completed rounds. | Approved |
| O6-D4 | Validate completion-policy results and convert contradictory or invalid completion shapes into stable typed encounter faults. | Approved |
| O6-D5 | Preserve the distinction between typed encounter cancellation and `CancellationToken` abortion. Menu Back remains inside host selection; intentional encounter cancellation performs battle-end cleanup; operational cancellation throws and aborts. | Approved |
| O6-D6 | Preserve command rejection as an encounter fault. Ordinary invalid player selections must be retried inside the host turn handler and must never escape as a rejected encounter command. | Approved |
| O6-D7 | Retain automated battle as a convenience service, add canonical asynchronous execution, preserve canonical typed events, and remove the second lossy message-authoritative event vocabulary. | Approved |
| O6-D8 | Correct the capability matrix to `partial` while Order 6 is open. Restore `complete` only after implementation, all three audience documents, verification, and independent closure review agree. | Approved |

## Ordered Implementation Checkpoints

Each runtime correction and each final documentation/review boundary should
remain an isolated green commit.

### O6-R1: Record The Approved Source Review

- preserve the verbatim owner discussion;
- index this review;
- make this record the active Order 6 roadmap authority; and
- change no runtime behavior.

### O6-R2: Correct Capability Truthfulness

- move `encounter_orchestration` from `complete` to `partial`;
- record the fixed scheduler, incomplete boundary reconciliation, incomplete
  structural event stream, and automated-wrapper duplication as known gaps;
- leave unrelated capability states unchanged.

### O6-R3: Establish Scheduling Contracts

- introduce immutable scheduler state, command-window, transition, and
  diagnostic contracts;
- separate scheduling authority from `IBattleTurnEconomy`;
- require explicit scheduler injection rather than hidden fallback;
- ensure scheduler policies receive detached or narrowly mutable inputs;
- preserve finite liveness bounds.

### O6-R4: Preserve Team-Phase Round-Robin

- move the current team-order, phase, and actor-rotation behavior behind the
  supplied `TeamPhaseRoundRobin` scheduler;
- prove existing encounter behavior and Action Token integration remain
  unchanged;
- define deployment, defeat, recall, and newly deployed actor behavior during
  a live phase.

### O6-R5: Supply Agility-Ordered Scheduling

- resolve one deterministic cross-team actor order from runtime stats;
- define tie-breaking through an explicit injected policy or random source;
- skip actors who become defeated or undeployed before their command window;
- define when newly deployed actors first become eligible;
- keep lifecycle clock boundaries explicit rather than inferred from UI.

### O6-R6: Add The Bonus-Action Scheduling Seam

- let a scheduler inspect the accepted post-command transition without owning
  action execution or turn-economy mutation;
- permit a future policy to retain the same actor for an immediate command
  window;
- do not silently turn the supplied Action Token economy into a bonus-action
  scheduler;
- prove the default policies cannot manufacture unbounded command windows.

### O6-R7: Reconcile Every Lifecycle Boundary

- centralize post-boundary synchronization, departure cleanup, defeat
  announcement, and completion evaluation;
- call it after battle start, turn start, turn end, phase end, and round end
  whenever mutation has committed;
- prevent a defeated or undeployed actor from entering a command window;
- preserve transactional rollback when a lifecycle operation or cancellation
  occurs before commit.

### O6-R8: Complete Structural Event Contracts

- add typed `TurnEnded` and `RoundEnded` payloads;
- define exact ordering relative to lifecycle and turn-economy events;
- report final/current round separately from fully completed rounds;
- keep debug text optional and non-authoritative;
- preserve runner ownership of structural events.

### O6-R9: Validate Completion And Terminal Shapes

- validate every completion outcome enum;
- reject incomplete results that carry terminal metadata;
- require valid participating winner IDs where the selected outcome requires
  one;
- reject contradictory fault, winner, and outcome combinations;
- surface stable fault codes rather than allowing invalid terminal state.

### O6-R10: Certify Cancellation And Rejection Semantics

- test menu-back retry behavior at the host command boundary;
- test intentional typed encounter cancellation and battle-end cleanup;
- test operational cancellation rollback and propagated
  `OperationCanceledException`;
- retain rejected commands as host-contract faults;
- document these three paths independently.

### O6-R11: Modernize Automated Battle

- add an asynchronous automated execution path;
- return or expose canonical typed encounter events;
- retire the duplicate lossy `BattleRuntimeEvent` mapping from active
  authority;
- keep deterministic selection and encounter-only AI knowledge;
- keep synchronous execution only if it remains an explicitly documented
  compatibility convenience outside engine/UI usage.

### O6-R12: Complete The Three Audience Documents

- add or reconcile player-facing encounter-loop mechanics;
- add a developer integration guide for scheduler, lifecycle, command,
  completion, cancellation, fault, and event ports;
- add a technical state-machine document with scheduler and transaction
  diagrams;
- update the active architecture, public API, roadmap, and capability matrices;
- do not promote documentation entries before source and diagrams agree.

### O6-R13: Independent Closure Review

- reread current source without relying on this review's conclusions;
- cross-check all three audience documents;
- run focused scheduler, lifecycle, event, cancellation, completion, automated
  battle, Godot, and host tests;
- run the complete release gate;
- restore `encounter_orchestration` to `complete` only if no realistic reachable
  defect or unresolved approved decision remains.

## Required Verification

At each applicable checkpoint:

- run focused encounter tests;
- run the full solution;
- run strict Debug and Release builds with zero warnings;
- run formatting verification;
- run all DemoHost modes and scripted Training Annex play where affected;
- run Godot headless smoke when host contracts change;
- run documentation links and coverage-matrix checks;
- run the Framework neutrality and forbidden-reference checks;
- run `git diff --check`.

## Checkpoint Progress

| Checkpoint | State | Evidence |
|---|---|---|
| O6-R1 | Complete | This approved source review is indexed by the active review and roadmap documents. |
| O6-R2 | Complete | The executable capability matrix records `encounter_orchestration` as `partial`, names all four approved gaps, and the active matrix summaries report 22 complete, 1 partial, and 2 deferred capabilities. |
| O6-R3 | Complete | Immutable scheduling start/advance requests, detached participant projections, policy-owned state, structural steps, command outcomes, stable rejection diagnostics, and transition validation establish the host-extensible scheduling boundary without granting it action or turn-economy mutation authority. Eight focused contract tests and the existing encounter suite verify collection immutability, graph identity, sequence continuity, and outcome-shape rejection. |
| O6-R4 | Complete | `TeamPhaseRoundRobinBattleEncounterSchedulePolicy` now owns the supplied team-phase and active-actor rotation order. `BattleEncounterRunner` requires an explicitly injected scheduler, supplies detached participant snapshots and accepted turn-economy evidence, refreshes eligibility after commands, and converts scheduler exceptions or invalid transitions into typed encounter faults. Focused policy and runner tests preserve phase order, Action Token behavior, unavailable-actor handling, deployment refresh, round limits, and injected scheduler authority. |
| O6-R5 | Complete | `AgilityOrderedBattleEncounterSchedulePolicy` freezes a descending effective-Agility order across teams at each round boundary and opens one explicit actor phase per scheduled participant. The Agility stat ID and equal-value tie-break policy are injected, invalid or missing ordering data is rejected without state mutation, actors unavailable before their slot are skipped, mid-round deployments wait until the next round, and changed Agility takes effect when the next round order is resolved. Focused policy and runner tests inspect actual command-window actor IDs rather than inferring order from team events. |
| O6-R6 | Complete | `IBattleEncounterPostCommandSchedulePolicy` can inspect immutable evidence from an accepted command and request that the same actor receive an already-existing opportunity. The supplied team scheduler invokes the extension only while the turn economy reports remaining opportunities, preserves ordinary rotation when no extension or a follow-scheduler decision is supplied, and rejects invalid decisions or repeats beyond the explicitly configured finite cap without advancing state. The extension cannot execute actions, mutate economy snapshots, or manufacture an opportunity after exhaustion. |
| O6-R7 | Complete | One runner-owned reconciliation operation now synchronizes live participants, processes departure cleanup to a bounded fixed point, re-synchronizes cleanup mutations, announces each defeat once, and evaluates completion after battle start, turn start, committed command/turn end, phase end, and round end. A defeated or undeployed actor cannot enter the command handler after turn-start mutation, while completion policies receive only the last actor that actually committed a command. Focused boundary tests prove immediate termination and defeat-before-battle-end ordering; the pre-existing cancellation and failure tests continue to prove lifecycle transaction rollback. |
| O6-R8 | Complete | The canonical event stream now closes committed, unavailable, and encounter-terminating actor turns with typed `TurnEnded` payloads after turn-end lifecycle, accepted Action Token mutation, and state reconciliation. It emits `RoundEnded` only after round-end lifecycle and reconciliation have committed. `BattleEnded` separately reports the final round reached and the count of fully completed rounds, so a result reached midway through round one records final round 1 and completed rounds 0. Existing enum values remain stable, debug text remains optional, and ports cannot forge either runner-owned end event. |
| O6-R9 | Complete | The completion-policy boundary now rejects undefined outcomes, terminal metadata on incomplete results, missing or unexpected winners, invalid winner IDs, non-participating winners, and policy-produced faults that lack a typed fault code. Victory and defeat require one participating winning team; draw, escape, and cancellation forbid one. Contradictions become `CompletionEvaluationFailed` without opening a command window. Command terminal results also reject undefined outcomes, require winners where appropriate, and fault unknown encounter teams before turn-economy or turn-end mutation. `BattleEnded` payload construction independently rejects contradictory outcome, winner, and fault-code shapes. |
| O6-R10 | Complete | Executable evidence now distinguishes all three cancellation/retry paths. DemoHost submenu Back returns to the same actor's command loop and can execute a later command without spending resources or Action Tokens on the abandoned selection. A typed `BattleEncounterCommandResult.Cancelled` emits a terminating turn boundary, skips turn-end and economy mutation, commits battle-end cleanup exactly once, and returns a `Cancelled` result. Cancelling the supplied token propagates `OperationCanceledException`, rolls back staged lifecycle work, and emits no fabricated `TurnEnded` or `BattleEnded`. A rejected command remains a host-contract fault, consumes no turn, runs cleanup, and now emits both `ActionRejected` and the previously missing typed `BattleFaulted(CommandRejected)` event. |
| O6-R11 | Complete | `AutomatedBattleRunner.RunAsync` now delegates directly to the canonical asynchronous encounter runner, accepts operational cancellation, and returns the complete ordered `BattleEncounterEvent` stream without resequencing or lossy event-kind translation. DemoHost awaits this path. The duplicate `BattleRuntimeEvent` vocabulary has left the active source and public API. Focused tests prove canonical structural payloads remain visible, pre-cancelled execution performs no participant mutation, deterministic selection and encounter-local knowledge remain intact, and the explicitly documented synchronous compatibility wrapper restores a caller's synchronization context. |
| O6-R12 | Complete | Dedicated mechanics, developer, and technical pages now distinguish initiative, scheduling, turn economy, lifecycle, commands, completion, cancellation, faults, events, and host presentation. They document both supplied schedulers, bounded post-command retention, lifecycle transaction and reconciliation order, canonical automated execution, and Godot integration. Architecture, public API, gameplay, roadmaps, indexes, and executable coverage evidence point to the same contract. Entries remain `existing_unreviewed` until independent closure. |
| O6-R13 | Corrections required | The independent source audit passed a 278-test focused baseline but found six reachable closure defects. O6-R13A through O6-R13G in the [independent closure audit](encounter-orchestration-order-6-independent-closure-audit-2026-07-30.md) now govern correction and fresh closure. |

## Closure Rule

Order 6 is not complete merely because the current team-phase runner works.
Closure requires the approved scheduler modularity, lifecycle reconciliation,
event completeness, completion validation, cancellation/rejection contract,
automated-runner modernization, and all three audience documents to agree with
current source and executable evidence.
