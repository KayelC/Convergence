# Neutral Vocabulary Migration Code Health Report

Date: 2026-07-15

## Review Scope

This is a fresh review of the 11 commits from `f23ed8a` through `c3d96a4` plus the current 55-file uncommitted Checkpoint 5 work. It reviews implementation code, tests, content, and the current command results. Earlier summaries and completion labels were used only as claims to verify.

See the companion [progress report](neutral-vocabulary-migration-progress-report-2026-07-15.md) for the checkpoint and recovery-finding ledger.

## Findings

### High: the committed roster invariant was not integrated with the complete test baseline

`RuntimeActorState` now correctly rejects an actor roster where the active hosted entity also appears in the inactive hosted-entity roster. The shared fixture in `RuntimeStateSnapshotTests`, however, defaults `hostedEntityRoster` to `[activeHostedEntity]`. Five unrelated snapshot/resource/progression tests therefore fail before reaching the behavior they are meant to test.

- Production enforcement: [`BattleRuntimeState.cs`](../src/Convergence.Framework/Execution/BattleRuntimeState.cs#L1277)
- Invalid fixture default: [`RuntimeStateSnapshotTests.cs`](../tests/Convergence.Framework.Tests/Runtime/RuntimeStateSnapshotTests.cs#L386)

This is an integration failure, not evidence that the invariant is wrong. The fix is to make the normal fixture valid, then preserve explicit coverage proving that active/inactive duplication is rejected. Until that is done, R2 and the Checkpoint 4 audit cannot be accepted as green.

### High: the uncommitted vocabulary pass inverted semantic boundary tests

The Checkpoint 5 working tree mechanically replaced historical forbidden vocabulary inside tests that were supposed to detect it:

- the fusion boundary now forbids `catalyst`, even though Catalyst is the approved neutral replacement, instead of continuing to forbid `mitama`;
- the Framework currency scan now checks for `Credits` instead of retaining the ban on `Macca`;
- the Framework neutrality list replaced old sample tokens instead of extending or preserving the historical guard;
- the wallet reflection test now checks that a `Credits` property is absent but no longer checks that a `Macca` property is absent.

Relevant locations:

- [`FrameworkBoundaryTests.cs`](../tests/Convergence.Framework.Tests/Architecture/FrameworkBoundaryTests.cs#L153)
- [`FrameworkNeutralityTests.cs`](../tests/Convergence.Framework.Tests/Architecture/FrameworkNeutralityTests.cs#L8)

The Catalyst inversion causes the sixth current test failure. The other replacements are more dangerous because they can pass while allowing retired terminology to return. These tests must be repaired before any Checkpoint 5 commit. Old forbidden tokens should remain protected; any new host-neutrality assertions should be additive and intentional.

### Medium: public catalog restore can bypass the new derived-state authority

`CatalogBattleActorRestoreRequest` publicly exposes `CatalogBattleActorRestoreMode.PreserveValidatedSnapshot`. Selecting it skips the stat-composition branch entirely. Fusion and Compendium use this mode for in-memory snapshots they already consider validated, but any framework consumer can select the same mode for stale save data.

- Public mode and request: [`CatalogBattleActorFactory.cs`](../src/Convergence.Framework/Encounters/CatalogBattleActorFactory.cs#L19)
- Bypass branch: [`CatalogBattleActorFactory.cs`](../src/Convergence.Framework/Encounters/CatalogBattleActorFactory.cs#L539)

That weakens R1's intended boundary: normal restore is safe by default, but the safety is caller-selectable. Prefer an internal preservation path for trusted framework transactions, or a separate internal factory method. Save-facing public restoration should always recompose derived state.

### Medium: Vessel composition discards registered non-core effective stats

`RuntimeActorStatCompositionService` creates a new effective-stat dictionary containing only the five `StandardProgressionIds.CoreStats`, then `ApplyStatComposition` replaces the actor's entire effective-stat dictionary with that result.

- Core-only construction: [`RuntimeActorStatComposition.cs`](../src/Convergence.Framework/Runtime/RuntimeActorStatComposition.cs#L192)
- Whole-dictionary replacement: [`BattleRuntimeState.cs`](../src/Convergence.Framework/Execution/BattleRuntimeState.cs#L916)

Convergence content and runtime snapshots can carry registered stat IDs beyond those five. A host using an additional stat can silently lose its effective value whenever Vessel composition runs. Either preserve non-core values, make the composed stat set explicit in a profile, or reject unsupported extra stats with a typed diagnostic. Silent deletion is not a safe framework default.

### Medium: DemoHost growth and Vessel recomposition are two separate commits

Training Annex applies the level-growth result directly to the live actor, returns to the main loop, and only then performs the general player stat-composition step. If the later equipment resolution or composition rejects, the host exits with the progression mutation already committed.

- Growth mutation: [`CleanTrainingAnnexPlayHost.cs`](../samples/Convergence.DemoHost/Hosts/TrainingAnnex/CleanTrainingAnnexPlayHost.cs#L2166)
- Later composition: [`CleanTrainingAnnexPlayHost.cs`](../samples/Convergence.DemoHost/Hosts/TrainingAnnex/CleanTrainingAnnexPlayHost.cs#L1326)

The normal valid path works and is covered, but the rejection path violates the migration's stated single-state/atomicity goal. A combined preview-and-commit operation, or rollback to the pre-growth snapshot on composition failure, would close the gap.

### Low: active documentation currently overstates completion

`neutral-vocabulary-migration-recovery.md` marks R1, R2, and the Checkpoint 4 audit complete. The full suite is red, the public preserve bypass remains, and the working tree has not completed Checkpoints 5 or 6. Completion labels should be revised to distinguish implementation from acceptance.

- [`neutral-vocabulary-migration-recovery.md`](neutral-vocabulary-migration-recovery.md)

### Low: retired terminology remains outside the archive

Checkpoint 5 is incomplete. Examples still include `switch_form`, `old_demon`, `old_persona`, `FamiliarDemon`, `active_form`, and framework diagnostic text such as `Party stock transition applied` and `Active form`. The old production-readiness audit also remains active while describing superseded symbols.

Examples:

- [`BattleActionExecutor.cs`](../src/Convergence.Framework/Execution/BattleActionExecutor.cs#L1027)
- [`RuntimePersistenceSnapshots.cs`](../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs#L842)
- [`RuntimeStateSnapshotTests.cs`](../tests/Convergence.Framework.Tests/Runtime/RuntimeStateSnapshotTests.cs#L117)
- [`TrainingAnnexNegotiationController.cs`](../samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexNegotiationController.cs#L374)

This is expected while Checkpoint 5 is in progress, but it confirms that Checkpoint 6 cannot truthfully be marked started or passing.

## Verified Strengths

- Action Token behavior is implemented directly behind `IBattleTurnEconomy`; no retired public alias remains in Framework.
- Pass precedence is correct: partial tokens are consumed first; without a partial token, one full token becomes partial.
- Weakness and critical outcomes convert full to partial, Miss/Null consume two available tokens, and Repel/Absorb terminate the phase.
- Framework ownership contracts use Vessel, Hosted Entity, Companion, and roster vocabulary. Exact retired public symbols were not found in active Framework source.
- Actor roster diagnostics are immutable and shared by construction, composition, and save validation.
- Vessel composition validates active hosted-entity identity, supports both missing-entity policies, resolves equipment and battle stages, and constructs replacement resources before mutating live state.
- Schema version 2 and save contract version 7 are active; version 1 content and version 6 saves are unsupported as planned.
- Training Annex uses `echo_adept` as a Vessel and `annex_mentor` as its active hosted entity.
- The targeted migration suite passes 107/107 tests.
- All 150 DemoHost tests pass.
- The .NET 8 solution builds with 0 warnings and 0 errors when run with correct filesystem permissions.
- Four noninteractive DemoHost modes exit successfully and use `action_token`; the save and Training Annex demos validate version 7 snapshots.
- `git diff --check` reports no whitespace errors.

## Test Health

| Gate | Result | Interpretation |
|---|---|---|
| Full Framework tests | 558 passed, 6 failed, 0 skipped | Not releasable. Five fixture integration failures plus one bad uncommitted boundary rule. |
| Full DemoHost tests | 150 passed, 0 failed, 0 skipped | Host behavior remains broadly stable under the current changes. |
| Focused migration tests | 107 passed, 0 failed, 0 skipped | Core Action Token, progression, restore, persistence, and Godot contracts are substantially healthy. |
| Nonincremental solution build | 0 warnings, 0 errors | Compile health is good. |
| Demo commands | 4/4 noninteractive commands exit 0 | Runtime examples remain operational. |

## Open Design Decisions

1. Make trusted snapshot preservation internal, or explicitly accept that public callers can bypass recomposition. The safer recommendation is internal-only.
2. Decide how custom registered stats participate in Vessel composition. The safer recommendation is an explicit composition profile plus preservation/rejection semantics, never silent removal.
3. Decide whether the atomic boundary belongs in a framework progression-plus-composition service or in each host. The safer recommendation is a framework transaction result that a host can commit as one state replacement.

## Health Verdict

The code is **structurally promising but not migration-complete**. The largest committed rewrites are coherent and compile cleanly, and targeted behavior is well covered. The current branch still fails its complete test gate, contains an unsafe unfinished mechanical vocabulary pass, and has three unresolved state-authority concerns. It should remain local and unpushed until those findings are corrected and Checkpoints 5 and 6 pass the full gate.
