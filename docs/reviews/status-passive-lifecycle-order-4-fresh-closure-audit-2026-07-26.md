# Status And Passive Lifecycle Order 4 Fresh Closure Audit

## Review Identity

- Review date: 26 July 2026
- Reviewed revision: `8dbe3edc06f40760f245c165a066fa4a53b922a4`
- Branch: `main`
- Scope: authored ailments and passives, live runtime state, lifecycle policies,
  effect execution, encounter integration, save validation and restoration,
  schema-v8 content, tests, and the three active documentation audiences
- Method: current source, tests, content, schemas, and executable probes were
  inspected directly. Earlier Order 4 reports were not used as evidence for the
  findings below.

## Verdict

Order 4 is **not ready for formal closure at this revision**.

The underlying design is healthy: lifecycle mutations are staged, reserve aging
is explicit, cleanup causes are typed, encounter clocks are distinct, passive
extensions are validated, content is host-neutral, and the complete release gate
is green. The audit nevertheless reproduced two reachable correctness defects:

1. an ailment removed by an earlier ailment trigger can still execute later in
   the same owner-turn-end boundary; and
2. save validation accepts passive activation counters that do not identify an
   authored trigger on the referenced passive.

No security vulnerability, data exfiltration path, framework-to-host dependency
leak, or general transaction failure was found. Both defects are bounded
gameplay and restore-parity issues, but both violate documented Order 4
authority and require correction before closure.

## Findings

### O4-M1: A removed ailment can still execute its queued trigger

**Severity:** Medium

**Intended invariant:** only ailments that remain active when their turn-end
slot is reached may execute an authored trigger. A trigger added during that
boundary should wait until the next matching boundary.

**Reachable path:**

1. One actor has two nonexclusive ailments in stable insertion order.
2. The first ailment has an `owner_turn_end` trigger containing a typed
   `remove_ailment` effect that removes the second ailment.
3. The second ailment has its own `owner_turn_end` damage, recovery, status, or
   modifier effect.
4. `ExecuteAilmentTriggers` snapshots every `ActiveAilmentState` before running
   the first trigger.
5. The first trigger removes the second ailment from live staged state.
6. The loop later executes the stale `ActiveAilmentState` captured for the
   removed ailment.

**Source evidence:**

- [`BattleStatusLifecycle.cs`](../../src/Convergence.Framework/Execution/BattleStatusLifecycle.cs)
  snapshots `actor.Ailments.Values` at line 1585 and subsequently executes each
  captured definition without rechecking current membership.
- [`EffectExecutors.cs`](../../src/Convergence.Framework/Execution/EffectExecutors.cs)
  removes ailments immediately from staged state in
  `RemoveAilmentEffectExecutor`.
- [`shared.schema.json`](../../schemas/content/v8/shared.schema.json) permits
  `remove_ailment` in the shared effect union used by ailment triggers, so this
  is an authored and supported path rather than an impossible direct-code state.

**Reproduction evidence:** a temporary, uncommitted probe applied a remover
ailment before a recovery ailment. The remover deleted the second ailment, but
the removed ailment still restored HP from `50` to `70`. The probe expected
unchanged HP and failed. The probe was removed after reproduction.

**Consequence:** trigger behavior depends on insertion order and a cured status
can still damage, heal, or otherwise mutate combat once after removal. This
contradicts the mechanics document's statement that active ailment triggers run
at owner turn end.

**Required correction:**

- snapshot the ordered ailment IDs present at boundary start;
- re-resolve each ID from current staged actor state immediately before its
  triggers execute;
- skip IDs removed or replaced before their turn;
- do not execute ailments newly added after the boundary snapshot; and
- add regression coverage for removal, replacement, addition, stop behavior,
  event ordering, and rollback.

### O4-M2: Passive activation restore keys are not definition-coherent

**Severity:** Medium

**Intended invariant:** every restored passive activation count must reference
an equipped passive, an existing trigger index on that passive, and the exact
event authored by that trigger.

**Reachable path:**

1. A valid save actor equips a passive skill.
2. Its `RuntimePassiveActivationSnapshot` uses an out-of-range trigger index or
   an event ID different from the authored trigger.
3. `RuntimeActorSnapshotIntegrity.ValidatePassives` verifies only that the skill
   is loaded and that the complete activation key is not duplicated.
4. `BattlePassiveCollection.RestoreActivations` accepts the key because the
   passive skill is loaded.
5. Later dispatch checks the real authored key, so the restored count is dead
   state and does not enforce the intended activation limit.

**Source evidence:**

- [`RuntimeActorSnapshotIntegrity.cs`](../../src/Convergence.Framework/Runtime/RuntimeActorSnapshotIntegrity.cs)
  validates loaded skill membership at lines 777-836 but does not receive the
  passive definitions needed to check trigger index and event.
- [`PassiveRuntime.cs`](../../src/Convergence.Framework/Execution/PassiveRuntime.cs)
  restores any nonduplicate key for a loaded passive at lines 108-128.
- [`RuntimePersistenceSnapshots.cs`](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs)
  reduces catalog passives to an ID set before calling actor integrity
  validation at lines 758-769.

**Reproduction evidence:** a temporary, uncommitted probe added activation
`triggerIndex: 99` and `event: owner_turn_end` to an equipped passive that has
no authored trigger. `RuntimeSaveValidator.Validate` returned `IsValid == true`.
The probe expected rejection and failed. The probe was removed after
reproduction.

**Consequence:** a malformed or incorrectly serialized mid-battle save can pass
validation while losing passive activation-limit continuity. Aggregate restore
therefore cannot guarantee that all exposed passive bookkeeping matches the
catalog it claims to use.

**Required correction:**

- validate activation keys against equipped `SkillDefinition` values, not only
  skill IDs;
- reject an out-of-range trigger index;
- reject an event ID that does not equal the authored trigger event;
- retain the existing saved-actor check for per-target keys; and
- add valid per-dispatch and per-target round-trip coverage plus invalid
  no-trigger, wrong-index, and wrong-event cases.

The event policy registry remains host-supplied. This correction should not
infer per-dispatch versus per-target policy from content unless that policy is
also available at restore validation time.

### O4-L1: Active roadmap and maturity records disagree about Order 4

**Severity:** Low

The runtime findings make the current `complete` capability state and empty
known-gap list inaccurate until corrections land. Independently,
[`docs/roadmap/README.md`](../roadmap/README.md) says Order 4 "is next", while
the documentation roadmap and coverage page say Order 4 is formally complete
and Order 5 is next.

This is project-state drift, not a runtime defect. The active matrices and
roadmaps are reopened by this audit so they again describe one state.

## Confirmed Healthy Boundaries

The following areas were traced and did not produce a realistic reachable
finding:

- ailment application stages source, target, and participant state and commits
  only accepted transitions;
- guarding, resistance, chance, same-ailment refresh, exclusivity replacement,
  and protected replacement are separate typed decisions;
- turn-start custom handler exceptions and malformed results do not commit
  Guard clearing or actor mutation;
- owner-turn-end order is passives, ailments, recovery, then durations;
- reserve actors do not receive owner-turn-end processing, and reserve aging
  requires an explicit team-phase or round policy;
- Instant, counted-turn, phase, battle, and permanent lifetimes use explicit
  clocks and independent removal profiles;
- charges, shields, affinity Breaks, affinity overrides, other statuses,
  ailments, and stat modifiers participate in typed expiry and cleanup;
- lifecycle cleanup distinguishes deployment swap, defeat, flee, roster recall,
  battle end, and field transition;
- passive target resolution, owner eligibility, recursion, per-dispatch and
  per-target limits, result coherence, and transaction rollback are explicit;
- encounter startup, turn, phase, round, and battle-end lifecycle ingress use
  staged participant graphs;
- lifecycle collections remain immutable through constructor input and record
  cloning;
- schema-v8 ailments use explicit behavior, targeting, lifetime, removal, and
  recovery data; and
- Framework remains free of console, filesystem, serializer, Godot, and
  archived product dependencies.

## Documentation Cross-Examination

The three audience documents correctly describe:

- module optionality and host responsibility;
- application and transition order;
- turn-start restriction precedence;
- owner-turn-end ordering;
- explicit duration clocks and reserve suspension;
- lifetime/removal independence;
- cleanup causes;
- passive targeting, owner eligibility, recursion, and counting;
- framework-state atomicity versus irreversible host side effects; and
- serializer-neutral persistence.

Two clarifications must accompany the runtime corrections:

1. owner-turn-end trigger documentation must state that membership is
   snapshotted for ordering, removed/replaced ailments are rechecked before
   execution, and newly added ailments wait for the next boundary; and
2. persistence documentation must state that passive activation keys are
   validated against the referenced passive's authored trigger and event.

## Correction Roadmap

| Checkpoint | Work | Completion evidence |
|---|---|---|
| O4-R18 | Revalidate current ailment membership before each snapshotted trigger slot. | Focused removal, replacement, addition, ordering, stop, and rollback tests. |
| O4-R19 | Validate restored passive activation keys against equipped passive definitions. | Focused valid round trips and wrong-skill/index/event rejection tests. |
| O4-R20 | Reconcile mechanics, developer, technical, roadmap, capability, and documentation matrices. | Documentation matrix tests and active link checks. |
| O4-R21 | Re-read the corrected source without using this report as implementation evidence and run the complete release gate. | New closure review with no unresolved reachable Order 4 defect. |

Order 5 remains paused until O4-R21.

## Verification

The committed tree, after removing the two temporary probes, passed:

- focused Order 4 tests: `275/275`;
- full solution: `1,629/1,629`:
  - Framework tests: `1,449`;
  - DemoHost tests: `173`;
  - ContentValidator tests: `7`;
  - skipped: `0`;
- strict Release solution build: `0` warnings, `0` errors;
- formatting verification: clean;
- Framework coverage: `90.67%` lines, `76.25%` branches;
- active content validation: `6` packs, `36` documents, `98` qualified
  definitions;
- all four noninteractive DemoHost modes: successful;
- scripted Training Annex play: successful;
- real Godot 4.7.1 headless smoke: `CONVERGENCE_GODOT_SMOKE_OK`;
- `git diff --check`: clean; and
- worktree before this documentation commit: clean and synchronized with
  `origin/main`.

The first sandboxed Godot process could not write `user://logs` and crashed
inside the engine. Running the same official executable with normal user-log
access passed. This is an execution-environment constraint, not an Order 4
framework finding.

## Closure Decision

Order 4 is reopened at O4-R18. Its architecture is suitable for correction and
continued development, but formal closure would be premature until O4-R18
through O4-R21 are complete.
