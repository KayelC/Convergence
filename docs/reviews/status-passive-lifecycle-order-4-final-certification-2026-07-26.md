# Status And Passive Lifecycle Order 4 Final Certification

## Verdict

Order 4 is certified complete at `e979a973` plus this report revision.

The final bounded pass found no unresolved, realistic, reachable runtime defect
in the supported status, ailment, passive, duration-clock, encounter-lifecycle,
or save-and-restore boundaries. It found one documentation omission: active
`Instant` state is deliberately not a legal save checkpoint. That omission was
corrected across the mechanics, developer, and technical guides in `e979a973`.

This is the final immediate Order 4 audit. Later work may reopen the capability
only with new integration evidence or a reproducible contradiction in an
established mechanic. Repeating an open-ended search without new evidence is
not a release requirement.

## Review Method

The pass treated current source and executable behavior as authority. Earlier
review reports were not accepted as proof. The review traced:

- ailment application and exclusivity;
- turn-start restrictions and Guard clearing;
- turn-end Poison, Sleep, passive, and natural-recovery behavior;
- owner-turn, action, team-phase, round, event, and battle duration clocks;
- reserve-aging policy and authored `suspendWhileReserve` precedence;
- passive registration, activation, mutation evidence, and replacement
  dispatch;
- encounter-owned staging, cancellation, sequence commitment, departure
  cleanup, and battle cleanup; and
- runtime snapshot validation, public catalog restore, and aggregate
  restoration boundaries.

The relevant implementation paths include
[`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs),
[`BattleLifecycleClocks.cs`](../../src/Convergence.Framework/Execution/BattleLifecycleClocks.cs),
[`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs),
[`BattleStatusEncounterLifecyclePort.cs`](../../src/Convergence.Framework/Encounters/BattleStatusEncounterLifecyclePort.cs),
[`RuntimeActorSnapshotIntegrity.cs`](../../src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs),
and
[`CatalogBattleActorFactory.cs`](../../src/Convergence.Framework/Encounters/CatalogBattleActorFactory.cs).

No `TODO`, `FIXME`, `HACK`, or `NotImplementedException` marker was present in
the reviewed Order 4 runtime paths.

## Independent Sequence Evidence

[`StatusLifecycleCertificationTests.cs`](../../tests/Convergence.Framework.Tests/SkillSystem/StatusLifecycleCertificationTests.cs)
adds evidence that is different from the existing example-based tests:

1. Thirty-two fixed seeds each execute forty-eight deployment, round-clock,
   and refresh operations. The 1,536 operations are checked after every step
   against an independent reference model. The model proves that the supplied
   reserve-aging policy advances only opted-in state, while an authored
   `suspendWhileReserve` value still freezes that individual state.
2. A mixed actor snapshot exercises Guard, an ailment, counted status, Instant,
   Phase, Battle, and Permanent state, a shield, affinity override, affinity
   break, resources, and deployment.
3. A snapshot captured before outer action-end is rejected with the typed
   `RetainedDurationKindInvalid` diagnostic because it represents an action
   still in progress.
4. At every supported boundary after action-end, the actor is restored through
   the public catalog factory. The restored path then executes the remainder of
   the timeline and remains equivalent to the uninterrupted path after every
   operation.

The test actor uses the same HP/SP resource shape expected by standard catalog
composition. Public catalog restore normally recomposes a canonical combat
profile; exact aggregate save restoration uses the Framework's validated
snapshot path. The certification therefore does not compare an unsupported
hand-built resource shape to a catalog-normalized actor.

## Documentation Reconciliation

The three active audience documents now agree:

- the [mechanics guide](../mechanics/status-passive-lifecycle.md) explains that
  an `Instant` state lasts only through the outer ordered action and cannot be
  resumed from a save;
- the [developer guide](../developer-guide/status-passive-lifecycle.md) tells
  hosts to save after committed outer action-end; and
- the [technical guide](../technical/status-passive-lifecycle.md) records the
  `RetainedDurationKindInvalid` validation boundary and public restore result.

The review found no remaining contradiction between those guides and the
exercised source contracts.

## Deliberate Boundaries

The following are explicit product boundaries, not unresolved defects:

- Convergence restores committed runtime snapshots; it does not resume the
  middle of an ordered action or serialize an encounter scheduler continuation.
- Host-owned scene changes, animation, storage, and other external side effects
  remain outside Framework rollback.
- Custom policies and handlers are responsible for external effects they
  perform outside the staged runtime actor transaction.
- Presentation coverage remains focused. Framework capability completeness is
  not contingent on one console host rendering every lifecycle event.

## Verification Record

| Gate | Result |
|---|---|
| New certification tests | 2 passed |
| Certification plus capability-matrix tests | 5 passed |
| Broad focused Order 4 suite | 611 passed |
| Full Release solution suite | 1,679 passed, 0 failed, 0 skipped |
| Strict nonincremental Release build | 0 warnings, 0 errors |
| Content validation | 6 packs, 36 documents, 98 definitions |
| Framework coverage | 90.70% lines, 76.48% branches |
| Trimming analysis | 0 warnings, 0 errors |
| Locked restore and dependency audit | passed |
| Formatting verification | passed |
| Noninteractive DemoHost modes | all four passed |
| Scripted Training Annex play | exited 0 |
| Godot contract tests | 9 passed |
| Godot 4.7.1 headless smoke | `CONVERGENCE_GODOT_SMOKE_OK` |
| Architecture and documentation guards | 56 passed |

## Closure

The code is healthy within Order 4's confirmed contract. The final pass added
model-based and restore-equivalence evidence, corrected the only documentation
gap it exposed, and found no runtime issue requiring another correction cycle.
Order 5, battle knowledge, is the next collaborative documentation subject.
