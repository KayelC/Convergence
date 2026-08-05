# Encounter Orchestration Order 6 Post-R37 Independent Audit

## Review Basis

This review was performed on 5 August 2026 against `main` at `0af87d93`.
Historical review conclusions were not treated as evidence. The audit began
from the current encounter source, its public contracts, executable tests, and
the three active audience documents.

The source trace covered:

- encounter request validation, initiative, scheduling, lifecycle
  transactions, command execution, turn economy, reconciliation, completion,
  cancellation, fault finalization, events, and detached results;
- both supplied schedulers and the post-command scheduling extension;
- automated restriction handling and automated encounter composition;
- hostile extension tests for invalid schedules, economy drift, event
  ownership, cancellation, lifecycle rollback, completion, and liveness; and
- the mechanics, developer, and technical encounter documents.

The ordinary focused encounter selection passed 280 tests. The complete
solution passed 1,876 tests: 1,691 Framework, 178 DemoHost, and 7 content
validator tests, with zero failures and zero skips. Local restore emitted only
`NU1900` because the NuGet advisory endpoint was unreachable; compilation and
test execution succeeded.

Two disposable regression probes were added, run, and removed before this
record was written. They did not alter the repository baseline.

## Findings

### O6-M1: Schedule transitions do not enforce exhausted-economy liveness

**Severity:** Medium

`BattleEncounterScheduleStepOutcome.CommandCommitted` carries authoritative
before/after economy snapshots and `HasRemainingOpportunities`. The runner
passes that evidence to the scheduling policy, but the structural validator
receives only the old state, completed step, and proposed transition. It checks
that `CommandWindow -> CommandWindow` is structurally legal for the same team;
it does not reject that transition when the accepted command outcome says no
opportunities remain.

A disposable custom scheduler returned a second command window after a normal
one-action economy reached zero. The second handler ran and mutated the enemy
before economy application faulted. The probe expected one handler call and
observed two. The encounter eventually returned a typed fault, but the
unbudgeted second command mutation remained live.

This is a realistic framework extension-boundary failure, not a security
exploit. Custom schedulers are an advertised integration point, and a simple
liveness mistake can execute gameplay before rejection.

**Relevant source:**

- `src/Convergence.Framework/Encounters/BattleEncounterScheduling.cs`,
  `BattleEncounterScheduleAdvanceRequest.ValidateOutcome` and
  `BattleEncounterScheduleStructuralValidator.ValidateAdvance`;
- `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs`,
  `BattleEncounterScheduleCursor.Advance` and the command loop.

**Required correction:** pass the completed typed outcome into structural
transition validation. A phase start or committed command whose accepted
economy evidence reports no remaining opportunities must not advance to a
command window. The transition must be rejected before turn start, lifecycle,
or the command handler can run. A phase with remaining economy may still close
when its scheduler has no eligible recipient, as the supplied one-actor
Agility policy requires; that exception must be explicit in documentation and
tests rather than inferred from debug text.

### O6-M2: Team round-robin selection uses a compacted availability index

**Severity:** Medium

`TeamPhaseRoundRobinBattleEncounterSchedulePolicy` stores a numeric next-actor
offset, but selects that offset modulo the *currently available* actor array.
When an earlier actor leaves, the array compacts while the cursor retains its
old offset.

The defect is hidden by the existing two-player tests. A disposable three-actor
probe selected `A`, made `A` unavailable before its command, then advanced with
`ActorUnavailable(A)`. Stable round-robin order should select `B`; the supplied
policy selected `C`. With three original opportunities and only `B` and `C`
remaining, the resulting order can become `C, B, C`, giving one remaining
actor two command windows while the other receives one.

Turn-start flee, roster recall, defeat, and deployment changes are supported
ways to reach this path. The behavior contradicts the documented promise to
rotate across currently available actors while refreshing availability.

**Relevant source:**

- `src/Convergence.Framework/Encounters/BattleEncounterScheduling.cs`,
  `TeamPhaseRoundRobinBattleEncounterSchedulePolicy.SelectCommandOrPhaseEnd`.

**Required correction:** treat participant order on the active team as a
stable ring. Starting from the stored ring cursor, scan forward to the next
available actor and advance the cursor past that stable slot. Do not index the
cursor into a filtered array. Cover three-or-more-actor departure, defeat,
new-deployment, and immediate-retention cases.

### O6-L1: The phase safety-limit name obscures turn-window semantics

**Severity:** Low documentation/API precision

`BattlePhaseProgressPolicy.MaximumCommands` increments after a scheduled actor
is initially available but before turn-start lifecycle. If lifecycle then
undeploys that actor, no command handler runs, yet the safety budget has been
spent. A disposable probe with a limit of one therefore faulted before a
second actor could execute a command.

The technical turn-economy table already says this counter observes every
command window, so the runtime behavior is defensible as a liveness guard. The
public property, XML summary, mechanics text, and developer guide repeatedly
call it a command limit, which can lead a policy author to configure it as a
limit on executed commands.

**Required correction:** preserve the safety behavior unless design work finds
a reason to split the counters, but define it consistently as an accepted turn
window that reaches turn-start lifecycle. Clarify that an unavailable actor
detected before turn start does not count, while departure committed by
turn-start lifecycle does count. A future breaking API may rename the property;
this correction does not require one.

## Documentation Cross-Examination

The three audience documents accurately describe most current behavior:
asynchronous host use, lifecycle transactions, cancellation, typed outcomes,
fixed-point departure reconciliation, frozen result snapshots, event
ownership, Agility ordering, and automated execution all agree with source and
tests.

They currently overstate three points:

1. The mechanics page says the team scheduler rotates through currently
   available actors, but O6-M2 breaks stable rotation after list compaction.
2. The mechanics page says the economy decides whether a phase still has an
   opportunity and neither scheduler nor economy may silently perform the
   other's job, but O6-M1 permits another command window after accepted
   exhaustion.
3. Phase-limit terminology alternates between commands and command windows,
   producing O6-L1. The mechanics page should also state that a scheduler may
   close a live one-actor phase when its sole eligible recipient becomes
   unavailable.

Until the runtime findings and wording are corrected, the three audience
entries are `existing_unreviewed` rather than `reviewed`.

## Confirmed Healthy Boundaries

The audit did not find a new defect in these current paths:

- duplicate participant rejection and initiative permutation validation;
- Agility-order freezing and deterministic tie-break validation;
- lifecycle graph staging, checkpoint rollback, and cancellation-before-commit;
- no-cost economy immutability, free-action and structural liveness bounds;
- departure reason ownership, repeated-defeat periods, and zero-team draws;
- command/result shape validation and normal-versus-fault metadata;
- frozen participant results and canonical continuous event sequencing;
- nested event actor/target ownership and scheduled-actor correlation; and
- automated action authorization, prepared assessments, restrictions, and
  encounter-local team knowledge.

Trusted host mutation ports remain deliberately outside general rollback. That
documented boundary does not excuse O6-M1 because the runner already possesses
the accepted exhaustion evidence before invoking the second handler.

## Correction Roadmap

| Checkpoint | State | Work |
|---|---|---|
| O6-R38 | `complete` | Record this independent source and documentation audit; reopen only encounter orchestration tracking. |
| O6-R39 | `complete` | Replaced filtered-array indexing with stable-ring round-robin selection and adversarial three-or-more-actor tests. |
| O6-R40 | `complete` | Correlated structural schedule transitions with accepted economy liveness before another command window can run. |
| O6-R41 | `pending` | Reconcile phase-end exceptions and phase-window safety-limit terminology across mechanics, developer, technical, XML, and API guidance. |
| O6-R42 | `pending` | Independently reread corrected source and documents, run the complete release gate, and decide formal closure. |

## Correction Progress

O6-R39 now selects within the stable participant order for the acting team. It
scans forward from the stored ring cursor, skips unavailable slots without
compacting the ring, and advances past the selected stable slot. Focused tests
cover three-actor departure before a command, departure after a committed
command, new deployment, and immediate actor retention followed by normal
rotation. The focused team-schedule and post-command suite passes 16 tests.

O6-R40 now passes each accepted typed step outcome into structural transition
validation. A phase-start or committed-command outcome reporting no remaining
opportunities cannot select another command window. Contract tests cover both
exhausted boundary kinds. An end-to-end hostile scheduler test proves the
runner returns `ScheduleTransitionInvalid` before a second turn starts or a
second handler mutation occurs.

O6-L1 and audience reconciliation remain open through O6-R41, followed by the
independent O6-R42 closure gate. Order 6 therefore remains reopened.

## Closure Decision

Order 6 is **not ready for formal closure** at `0af87d93`.
`encounter_orchestration` returns to `partial`; no unrelated capability is
demoted. The architecture remains sound and the findings are bounded, but both
runtime defects affect advertised supplied or extension behavior and require
correction before the order can close.
