# Neutral Vocabulary Migration Progress Report

Date: 2026-07-15

## Scope

This report audits the current `main` branch against the approved six-checkpoint Neutral Vocabulary, Vessel Roles, and Action Token migration and the five recovery findings recorded after the interrupted first implementation.

The report is based on the current commits, source, tests, content, documentation, and command results. It does not treat commit messages or earlier completion notes as proof by themselves.

The companion [code health report](neutral-vocabulary-migration-code-health-report-2026-07-15.md) records the implementation defects and design risks found during this review.

## Repository State

- Remote baseline: `origin/main` at `5d59ca3`.
- Local branch: `main`, 11 commits ahead of `origin/main`.
- Local head: `c3d96a4`.
- Staged files: 0.
- Unstaged files: 55.
- Unstaged change size: 522 insertions and 512 deletions.
- Committed migration size: 137 files changed, 5,039 insertions and 3,539 deletions.

The 11 entries are real local commits, not staged changes. Their timestamps span from 2026-07-14 22:10:53 to 2026-07-15 00:04:49, approximately 1 hour 54 minutes. The final five commits, from `b1dd5f3` through `c3d96a4`, were created between 23:55:25 and 00:04:49, approximately 9 minutes 24 seconds. That final burst explains why the resumed overnight activity appeared to run for only about nine minutes even though eleven commits were visible afterward.

## Commit Inventory

| Commit | Intended ownership | Observed result |
|---|---|---|
| `f23ed8a` | Checkpoint 1 | Adds the migration authority and updates the documentation index/roadmap. |
| `e66aa50` | Checkpoint 2 | Replaces the supplied Press Turn implementation with Action Token contracts, updates ruleset IDs and content schema/package versions, and updates consumers/tests. |
| `2cd01be` | Checkpoint 3 | Replaces form/persona/demon stock contracts with Vessel, Hosted Entity, Companion, and roster contracts; updates save and action contracts. |
| `604cc89` | Checkpoint 4 | Adds explicit stat-source policies, Vessel/Independent Actor/Owned Entity growth profiles, and the actor stat-composition service. |
| `a0c691d` | Recovery control | Records R1-R5 and the unfinished checkpoint list. |
| `d1f298a` | R1 | Adds composition-aware catalog restoration plus an explicit snapshot-preservation mode. |
| `b1dd5f3` | R2 | Adds actor-roster invariant validation to actor construction, composition, and save validation. |
| `72df7fd` | R3 | Makes the Training Annex roster swap commit the party snapshot only after player stat composition succeeds. |
| `ff960e6` | R4 | Renames the two identified residual diagnostic symbols. |
| `ec88188` | R5 | Documents `turn_economy` as the generic category and `standard_action_token` as the supplied policy. |
| `c3d96a4` | Checkpoint 4 audit | Adds an end-to-end Training Annex swap/growth/save/restore regression test and marks the audit complete in documentation. |

## Six-Checkpoint Status

| Checkpoint | Status | Evidence and remaining work |
|---|---|---|
| 1. Documentation boundary | Complete and committed | The migration decisions, breaking-change policy, approved terminology, and deferred equipment scope are recorded. |
| 2. Action Token turn economy | Implemented and committed | `ActionTokenTurnEconomy` is active; public Action Token outcomes and snapshots replace the retired names. Passing consumes a partial token first, otherwise converts one full token into one partial token. Focused tests and all current demos exercise it successfully. |
| 3. Actor ownership roles | Implemented and committed | Framework public contracts use Vessel, Hosted Entity, Companion, party roster, hosted-entity roster, and companion roster. Save contract version 7 contains the new fields. Active DemoHost and test fixture wording still require Checkpoint 5 cleanup. |
| 4. Vessel stat redesign | Substantially implemented, not accepted | Explicit stat sourcing, missing-hosted-entity policy, growth profiles, composition, restore recomposition, roster invariants, and a host integration test exist. The full suite is red, the public restore bypass remains questionable, custom non-core stat handling is incomplete, and growth plus recomposition is not one transaction. |
| 5. Neutral example vocabulary | In progress and uncommitted | The 55-file working tree renames content, DemoHost messages/IDs, and test fixtures. It is not safe to commit: one boundary test fails, other boundary checks were semantically weakened, residual retired terms remain, and active documentation has not been fully rewritten/archived. |
| 6. Terminology boundary | Not started | No token-aware active-tree terminology guard exists. Existing boundary tests are older, narrower checks and currently contain incorrect uncommitted replacements. |

## Recovery Finding Status

| Finding | Current assessment | Evidence |
|---|---|---|
| R1: restore recomposition | Partially complete; reopen before acceptance | Normal catalog restore recomposes derived stats and has focused tests. However, the public `PreserveValidatedSnapshot` mode lets any caller bypass recomposition, and Training Annex restores the Vessel initially as actor-sourced before recomposing it later in the command loop. |
| R2: live roster invariants | Production rule implemented; integration incomplete | The shared invariant service correctly rejects duplicates and role collisions. Five existing snapshot tests now fail because their shared fixture places the active hosted entity in the inactive hosted-entity roster. The invariant should remain; the fixture and regression coverage need correction. |
| R3: atomic roster/Vessel host commit | Complete for the targeted roster swap | Training Annex composes against `operationResult.After`, assigns the party roster only after composition succeeds, and has a rejection regression test. |
| R4: two residual diagnostic symbols | Complete for the named symbols | `MissingParentForm` and `DuplicateActorFormReference` were replaced without aliases. Other retired strings and fixture names remain Checkpoint 5 work. |
| R5: generic turn-economy category | Complete | Ruleset content uses `turn_economy`; the supplied implementation is selected by `standard_action_token`. |

## Verification Results

### Authoritative current results

- Nonincremental .NET 8 solution build: succeeded, 0 warnings, 0 errors.
- Full solution tests: 714 total, 708 passed, 6 failed, 0 skipped.
  - Framework: 564 total, 558 passed, 6 failed.
  - DemoHost: 150 total, 150 passed.
- Focused migration contracts: 107 passed, 0 failed, 0 skipped.
  - Includes Action Token, progression/composition, catalog battle runtime, persistence, and Godot contract coverage.
- `git diff --check`: passed.
- `--clean-battle-demo`: exit 0, victory.
- `--clean-field-demo`: exit 0.
- `--clean-save-demo`: exit 0, save contract version 7 validated.
- `--clean-training-annex-demo`: exit 0, Action Token battle victory, Credits reward, save validation passed.
- Interactive Training Annex: covered by the 150 passing DemoHost tests; a separate manual piped-console run was not used for this audit.

An initial sandboxed nonincremental build emitted 356 access-denied warnings while trying to replace files outside the configured writable root. The same command rerun outside that sandbox completed with 0 warnings. The 0-warning result is authoritative; the first warning count was an execution-environment artifact.

### Current failures

Five failures share one invalid fixture in `RuntimeStateSnapshotTests`: the active hosted entity is also inserted into `HostedEntityRoster`. The sixth failure is the uncommitted `FrameworkFusionSources_DoNotEncodeLegacyCatalystOrMoonPhaseStrategies` check, which now incorrectly forbids the newly approved `catalyst` vocabulary.

## Recommended Recovery Order

Do not push or declare the migration complete in the current state.

1. Restore the green committed baseline by correcting the invalid actor-roster fixture and adding a dedicated rejection test for active/inactive hosted-entity duplication. Do not weaken the production invariant.
2. Repair the mechanically inverted boundary tests. Historical forbidden terms must remain forbidden; new neutral terms may be checked separately according to the Framework/DemoHost boundary.
3. Decide and close the three Checkpoint 4 risks documented in the code health report: public preserve-mode access, non-core stat preservation, and growth/recomposition transactionality.
4. Finish Checkpoint 5 deliberately, reviewing each rename by meaning rather than by global text substitution. Archive or rewrite stale active documentation as planned.
5. Add Checkpoint 6 as a new token-aware terminology test that scans active source, tests, content, and docs while excluding `ArchiveDocs`, `bin`, and `obj`.
6. Rerun the complete gate and only then create the planned Checkpoint 5 and Checkpoint 6 commits.

## Overall Verdict

The migration is real and substantial, not an empty series of commits. Action Token, generalized ownership roles, schema/save version changes, and the Vessel stat model all exist in working code. The branch is nevertheless **not ready to push or call complete**. Checkpoints 1-3 are in good shape, Checkpoint 4 needs correction and acceptance, Checkpoint 5 is unfinished in the working tree, and Checkpoint 6 has not begun.
