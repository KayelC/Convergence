# Encounter Orchestration Order 6 R27 Final Closure Review

**Date:** 4 August 2026
**Review basis:** current source, tests, and active audience documentation after O6-R25 and O6-R26
**Result:** no unresolved realistic reachable runtime defect found; one documentation precision correction applied during review

## Method

This review did not treat an earlier report or closure statement as proof. It
traced the current implementation from the public request and service
contracts through `BattleEncounterRunner`, both supplied schedulers, lifecycle
transactions, event ownership, automated composition, and terminal result
construction. It then cross-checked the mechanics, developer, technical, and
public API documents against those paths.

Primary source inspected:

- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs`
- `src/Convergence.Framework/Encounters/BattleEncounterEvents.cs`
- `src/Convergence.Framework/Encounters/BattleEncounterScheduling.cs`
- `src/Convergence.Framework/Encounters/AgilityOrderedBattleEncounterScheduling.cs`
- `src/Convergence.Framework/Encounters/BattleEncounterPostCommandScheduling.cs`
- `src/Convergence.Framework/Encounters/BattleEncounterLifecycleTransaction.cs`
- `src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs`
- `src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs`
- `src/Convergence.Framework/Encounters/AutomatedBattleTurnRestrictionResolver.cs`

Primary executable evidence inspected:

- `tests/Convergence.Framework.Tests/SkillSystem/BattleEncounterRunnerTests.cs`
- `tests/Convergence.Framework.Tests/SkillSystem/CatalogBattleRuntimeTests.cs`
- all tests under `tests/Convergence.Framework.Tests/Encounters`
- `tests/Convergence.Framework.Tests/Architecture/DocumentationFoundationTests.cs`

## Corrected Invariants Rechecked

### Terminal result authority

`BattleEncounterResult` now enforces one terminal shape at construction:

- `Victory` and `Defeat` require a winner;
- `Escape`, `Draw`, `Cancelled`, and `Faulted` reject a winner;
- only `Faulted` carries a defined `BattleEncounterFaultCode` and nonblank
  `FaultMessage`;
- every normal result carries neither fault field.

`FinishAsync` keeps a completion policy's optional message on
`BattleEnded.DebugText`. It does not copy that diagnostic text into fault
metadata. The round-limit draw follows the same path. True command and port
faults retain their stable code and message, and `AutomatedBattleRunner` maps
the already-validated canonical result without changing that distinction.

### Lifecycle and fault finalization

Battle-start, turn-start, owner-turn-end, phase-end, round-end, departure, and
battle-end lifecycle work is staged on detached actor state. Cancellation and
returned-event validation occur before commit. A fault attempts battle-end
lifecycle only when battle start previously succeeded, and that cleanup is
attempted at most once.

Event-sink failure does not recurse through the failed sink. The result retains
the canonical sequenced history and may append terminal fault evidence that the
sink did not receive. Hosts that require delivery acknowledgement must track
the sequence values they successfully consumed.

### Scheduling, economy, and liveness

The runner verifies unique participant IDs, an exact initiative team
permutation, frozen scheduler identity, legal step/outcome pairs, and command
actors belonging to their encounter team. Phase command/free-action limits and
the independent structural-transition limit contain non-progressing custom
policies. The runner alone applies `ActionTurnConsumption`; it checks economy
identity, snapshot type, state continuity, and explicit phase termination
before accepting the transition.

### Reconciliation and event identity

Departure cleanup reaches a bounded fixed point. Defeat cleanup and
announcement occur once per uninterrupted defeated period and become eligible
again only after recovery. Completion is evaluated after every committed
reconciliation boundary, including zero-living-team draws.

Port events use a fail-closed allow-list and are checked against the frozen
participant graph, including nested effect, resource, knowledge, passive, and
status evidence. Command-shaped evidence belongs to the scheduled actor;
actorless `ActionExecuted` evidence is reserved for the canonical
`PartyRosterTransitioned` shape.

## Documentation Precision Corrected During R27

The mechanics completion paragraph previously listed `Faulted` alongside
outcomes a replacement completion policy could return. The runtime correctly
rejects that shape because `BattleEncounterCompletion` has no typed fault-code
authority. The page now says that completion policies may return coherent
normal terminal outcomes, while only the runner's fault boundary creates
`Faulted` results.

The three audience pages also now identify custom turn handlers and state
synchronizers as trusted host mutation ports. The runner contains their
exceptions as typed faults, but it cannot undo arbitrary external side effects
performed by host code. Framework-provided action and lifecycle services use
their documented staged transactions; custom ports must provide equivalent
atomicity when they mutate state.

## Trusted Boundaries And Residual Risk

- `IBattleEncounterTurnHandler` and `IBattleEncounterStateSynchronizer` are
  host extension ports. Typed fault containment is not a transaction over
  arbitrary scene, network, or filesystem side effects performed inside them.
- `IBattleEncounterEventSink` is an asynchronous observer. A failed sink may
  receive only a prefix of the result's canonical event history.
- The synchronous `Run` methods remain compatibility helpers for non-UI code.
  Godot and other UI/engine hosts must await the asynchronous APIs.
- A custom scheduler, lifecycle port, turn economy, or completion policy is
  trusted to implement its declared semantics; the runner validates the
  structural boundaries that it can observe.

These are explicit extension boundaries, not unresolved Order 6 defects.

## Verification Evidence

| Gate | Result |
|---|---|
| Focused encounter, scheduler, automated, architecture, and documentation tests | 290 passed, 0 failed, 0 skipped |
| Full solution | 1,865 passed: 1,680 Framework, 178 DemoHost, 7 ContentValidator; 0 failed, 0 skipped |
| Framework coverage | 90.77% lines, 76.70% branches; 90%/70% gate passed |
| Strict Release solution build | 0 warnings, 0 errors |
| Framework trimming analysis | 0 warnings, 0 errors |
| Architecture and documentation boundary tests | 57 passed |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions passed |
| DemoHost | Four noninteractive modes and scripted Training Annex play exited successfully |
| Godot 4.7.1 headless smoke | `CONVERGENCE_GODOT_SMOKE_OK`, exit 0 with repository-local user-data paths |
| Formatting, diff, and forbidden-reference checks | Passed |

Godot printed its known nonfatal Windows root-certificate-store warning after
the successful smoke marker. It did not affect the sample, Framework, or exit
code. No dependency changed in this correction sequence; connected CI remains
the authoritative online dependency-advisory gate.

## Closure Decision

Order 6 is formally complete at this revision. The terminal-result correction,
event-delivery wording, conditional fault cleanup, and completion-policy fault
authority now agree across source, tests, public API guidance, and all three
audience documents. `encounter_orchestration` may return to `complete`, its
known-gap list may be cleared, and its mechanics, developer, and technical
entries may return to `reviewed`.

This does not claim that every battle format is built. Convergence supplies a
host-neutral encounter director with replaceable scheduling, turn economy,
lifecycle, command, completion, and observation policies. Additional battle
models remain extensions through those contracts.
