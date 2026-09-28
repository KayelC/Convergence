# Dungeon Traversal Order 9 Post-Correction Independent Audit

**Audit date:** 28 September 2026  
**Reviewed source baseline:** `11e69b12`; corrected range verified through `299cbc15`
**Method:** fresh source, test, host-integration, and audience-document trace.
Earlier Order 9 reports were consulted only after findings were derived from
current code and current active documentation.

## Final Verdict

The corrected runtime is coherent, and this pass found no unresolved realistic
reachable Framework or DemoHost defect in Order 9. The two low-severity
quality-boundary defects found by this pass were corrected separately, and the
complete retained release gate then passed on the corrected clean commit.
O9-C4 is technically complete. Order 9 and its three audience entries remain
open only for explicit project-owner documentation confirmation.

## Findings And Corrections

### O9-C4-L1: active capability prose still presents a superseded gate as current

**Intended invariant.** The executable capability matrix, active roadmap, and
active capability narrative must identify the same current review authority.
Historical audits remain evidence but may not appear to be the latest closure
gate after a later audit reopens an Order.

**Reachable path.** The executable matrix correctly records
`dungeon_traversal` as `implemented` with `orderState: open`, and the active
Order 9 roadmap says O9-C4 is pending. However,
`docs/roadmap/framework-capability-matrix.md` still says the retained gate at
`0189a6c2` passed and that only owner documentation confirmation remains. It
does not mention the 23 September fresh audit or O9-C1 through O9-C3. The
confirmed decision record likewise ends its implementation evidence at
O9-R2 through O9-R6, omitting the later authority corrections.

**Consequence.** A maintainer reading the active capability narrative can
reasonably conclude that runtime review is already complete and only owner
confirmation remains. That contradicts the actual roadmap and can cause C4 to
be skipped after context loss. Runtime behavior is unaffected.

**Required correction.** Update the active capability narrative and decision
implementation evidence to identify the fresh audit, O9-C1/C2 authority fixes,
O9-C3 alignment, and pending O9-C4 gate. Add an executable documentation
assertion so the active narrative cannot silently regress to the superseded
closure claim.

**Correction.** Commit `511fc387` updates the active capability and decision
authority, and `FrameworkCapabilityMatrixTests` prevents the superseded gate
from being presented as current.

### O9-C4-L2: the retained local Godot gate relies on the engine's default user log path

**Intended invariant.** The canonical retained-evidence runner must execute the
real Godot smoke with all generated output confined to a known writable local
path. A host environment's inaccessible profile directory must not prevent the
sample project from loading or make the release gate irreproducible.

**Reachable path.** `Invoke-VerificationEvidence.ps1` invokes the selected
Godot executable with `--headless --path ...` but no explicit `--log-file`.
On the current Windows verification host, Godot cannot open
`user://logs/godot...log` and crashes with a native access violation before the
project begins. Redirecting the entire process `APPDATA` is not a valid runner
fix because it also changes the preceding .NET/NuGet environment. Running the
identical smoke with `--log-file` targeting the ignored repository-local
artifact directory reaches every `GODOT_*_OK` marker and exits zero.

**Consequence.** A correct Framework and Godot consumer can fail the retained
release gate before project execution. Repeated manual environment workarounds
also make the canonical command and its evidence differ, weakening the audit
trail. This does not indicate a dungeon-runtime defect.

**Required correction.** Give the canonical Godot smoke an explicit log file
inside `%EVIDENCE_ROOT%`, record that argument in the generated command, and
guard it with an executable evidence-runner contract test. Do not redirect the
whole gate's .NET user environment.

**Correction.** Commit `299cbc15` adds an explicit
`--log-file "%EVIDENCE_ROOT%\godot-smoke.log"` to the canonical smoke command,
documents the retained log, and guards the argument in
`VerificationEvidenceContractTests`. The final run used the normal .NET/NuGet
environment and completed the real Godot 4.7.1 smoke with exit code zero.

## Fresh Runtime Review

The following current behavior was traced directly from source and tests:

1. `RuntimeDungeonTraversalService` validates current dungeon/node, every
   retained visited/checkpoint/boss ID, and all transition IDs before dungeon
   matching, source matching, or route-policy evaluation. Invalid requests are
   typed, non-mutating, eventless, and expose the first invalid field.
2. Progress reports apply the same complete snapshot validation before the
   reported ID and immutable registry. Declaration kind, dungeon, and current
   eligible area are checked before idempotence. Node entry alone records no
   progress.
3. Public traversal and progress result constructors reject contradictory
   before/after/event/diagnostic evidence. Snapshot and event collections are
   defensively copied and cannot be rewritten through record cloning.
4. `RuntimeSaveValidator.CreateWithDungeonProgressRegistry` validates every
   retained checkpoint and boss against declaration ID, kind, dungeon, and an
   eligible node in visited history. Missing registry authority rejects retained
   progress. Saves with no retained checkpoint/boss records remain independent
   of this optional module.
5. `RuntimeSessionRestoreService` runs that validator before resolving profiles
   or creating actors, so invalid retained progress exposes no partial restored
   session. It does not replay traversal or manufacture a progress receipt.
6. Training Annex save, load, aggregate restore, startup validation, and the
   clean save demo supply their actual progress registries. Training Annex keeps
   node/scene compatibility as a host concern and no longer maintains a weaker
   progress whitelist.
7. Navigation location still determines active field/save context. Outside
   location plus retained progress remains legal; re-entry selects an entrance
   or legally unlocked checkpoint rather than silently resuming the last node.
8. Godot-shaped adoption keeps an applied traversal result as a candidate until
   scene work succeeds. The real Godot smoke sample remains accurately scoped
   as a broader contract proof rather than a spatial dungeon implementation.
9. Authored dungeon floors remain optional catalog metadata. Validation covers
   floor range, duplicate floor numbers, kind, required encounter references,
   and registrations; traversal does not resolve floors or start encounters.

## Documentation Review

The mechanics, developer, and technical pages agree on the corrected authority:

- hosts request and adopt traversal separately from navigation and scene work;
- live retained history is validated before policy evaluation;
- saved checkpoint/boss records require the same registry used by live reports;
- eligible visited history is evidence for restore validation, not a battle
  receipt;
- no-progress games remain free of the optional registry at save validation;
- independent navigation/dungeon nullability remains an Order 13 question.

Their Mermaid transition, progress, and host-adoption flows match current call
ordering. The post-correction gate is complete; all three entries correctly
remain `existing_unreviewed` pending explicit owner confirmation.

## Final Verification

The canonical 23-command gate tested clean commit
`299cbc158e8cb91c7a9f2e786b1685d457aa02b9` and reviewed
`d6608d95c7432d7b4eedea224bed09cd5d84c8b4..299cbc158e8cb91c7a9f2e786b1685d457aa02b9`.
It recorded:

- focused Framework gate: 274/274;
- focused DemoHost gate: 142/142;
- architecture gate: 66/66;
- full Release suite: 1,895 Framework, 191 DemoHost, seven ContentValidator
  tests (2,093 total), zero failed and zero skipped;
- Framework coverage: 90.38% lines and 77.32% branches;
- six packs, 36 documents, and 98 qualified definitions validated;
- locked restore/vulnerability audit, formatting, strict Framework and solution
  builds, trimming analysis, and `git diff --check` passed with zero warnings;
- all five DemoHost modes passed; and
- the real Godot 4.7.1 build and headless smoke emitted
  `CONVERGENCE_GODOT_SMOKE_OK` and exited zero.

The successful raw bundle, exact reviewed diff, command wrappers, outputs,
Godot log, compressed coverage, manifest, and checksums are retained at
`artifacts/verification/o9-c4-verified/299cbc158e8cb91c7a9f2e786b1685d457aa02b9`.
Three failed attempts are also retained and explicitly labeled as failures:
the first exposed O9-C4-L2 at `18-godot-smoke`; two subsequent experiments that
redirected the whole user environment failed NuGet audit/build commands and
proved why the correction had to be Godot-specific.

## Correction Checkpoint

| Checkpoint | State | Exit condition |
|---|---|---|
| O9-C4-L1 | verified | Active capability/decision evidence names the correction chain; the executable documentation test guards both documents. |
| O9-C4-L2 | verified | The canonical Godot command confines its log to the retained evidence bundle and an executable contract test prevents removal of that argument. |
| O9-C4-GATE | verified | Fresh recheck found no realistic reachable defect or contradiction, and all 23 retained-gate commands passed on `299cbc15`. |

