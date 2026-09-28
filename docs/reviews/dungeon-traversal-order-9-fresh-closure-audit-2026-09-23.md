# Dungeon Traversal Order 9 Fresh Closure Audit

**Audit date:** 23 September 2026  
**Source baseline:** `d6608d95c7432d7b4eedea224bed09cd5d84c8b4`  
**Audit method:** fresh source, test, host-integration, and audience-document
review. Earlier Order 9 reports and completion summaries were not used as
evidence for the findings below.

## Verdict

Order 9 should remain open. Its central design is coherent and most of the
implementation is strong, but two reachable authority gaps remain:

- live traversal can approve a transition while carrying malformed retained
  history into the candidate state;
- restored checkpoint or boss progress is not checked against the same
  eligibility registry that governs live reports.

There are no high-severity findings. This audit records two medium findings
and one low documentation/tracking finding. They are closure blockers because
both medium findings concern who is allowed to create authoritative dungeon
state, not cosmetic hardening.

## Findings

### O9-M1: valid transitions preserve malformed retained history

**Intended invariant.** O9-D6 says invalid live dungeon IDs produce typed,
non-mutating failures. The technical contract also says live requests validate
IDs at the service boundary.

**Reachable path.** `RuntimeDungeonTraversalSnapshot` accepts the visited-node,
checkpoint, and boss collections without checking each `ContentId`. The live
request validator checks only current dungeon, current node, transition,
transition dungeon, source node, and destination node. With those six values
valid, `Traverse` calls `MoveTo`, which carries all three malformed collections
into an `Applied` result.

Relevant source:

- `src/Convergence.Framework/Runtime/DungeonTraversal.cs:58-72`
- `src/Convergence.Framework/Runtime/DungeonTraversal.cs:86-108`
- `src/Convergence.Framework/Runtime/DungeonTraversal.cs:516-528`
- `src/Convergence.Framework/Runtime/DungeonTraversal.cs:740-771`

**Reproduction.** A temporary .NET 8 audit probe constructed a snapshot with
valid current IDs and a default `ContentId` in each retained collection, then
requested an allowed valid move. The unedited result was:

```text
Code=Applied
InvalidVisitedRetained=True
InvalidCheckpointRetained=True
InvalidBossRetained=True
Destination=audit:room
```

The probe was removed after execution and did not modify an active project.

**Consequence.** A public caller, custom host decoder, or earlier faulty state
producer can receive a successful transition whose history cannot pass save
validation. `IsCheckpointUnlocked(default)` and `IsBossDefeated(default)` can
also report true on that malformed state. The route policy is called even
though the state it receives is not runtime-valid.

**Required correction.** Validate every retained node, checkpoint, and boss ID
before dungeon/source matching or policy evaluation. Return a typed
`InvalidRequest` that identifies the first malformed collection and leaves the
exact state unchanged. Apply the same complete snapshot validity rule to
progress-report requests and public result coherence checks. Do not move this
validation into a host or add a policy; it is a Framework state invariant.

**Required tests.** Cover one malformed value in each retained collection for
`Traverse`, `UnlockCheckpoint`, and `RegisterBossDefeat`; prove the policy is
not called, no event is emitted, and no collection changes.

### O9-M2: restore can bypass progress eligibility authority

**Intended invariant.** O9-D1 and O9-D2 make the supplied progress registry the
authority for checkpoint and boss IDs, their dungeon, and the areas where they
can be earned. Re-entry may select only an actually unlocked checkpoint.

**Reachable path.** Framework save validation checks retained progress IDs only
for syntactic validity and checks only the dungeon ID against the catalog. It
does not receive or consult `RuntimeDungeonProgressRegistry`. Aggregate restore
then exposes the saved field snapshot unchanged. Training Annex adds a host
check, but that check only recognizes the checkpoint ID; it does not prove the
checkpoint has a matching registry declaration or that an allowed node was
ever visited. `SelectDungeonEntry` trusts membership in
`UnlockedCheckpointIds` alone.

Relevant source and existing executable evidence:

- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:630-644`
- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:1713-1727`
- `src/Convergence.Framework/Runtime/RuntimeSessionRestoration.cs:225-232`
- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexPersistenceController.cs:471-537`
- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexHostSupport.cs:168-197`
- `tests/Convergence.DemoHost.Tests/Host/CleanTrainingAnnexPlayHostTests.cs:4441-4457`

The existing entry test constructs a retained snapshot whose current and only
visited node is Review Hall while claiming the Review Checkpoint. That
checkpoint is declared as earnable only in Review Alcove. The helper accepts
the claim and selects Review Alcove anyway. This state cannot be produced by
the live progress service, but it can arrive through a malformed, stale, or
host-decoded save.

**Consequence.** Restore is a second authority for progress. A syntactically
valid save can grant a checkpoint or boss record that live gameplay would have
rejected. In Training Annex this can authorize checkpoint entry. A generic host
following the current developer guide may accept undeclared progress entirely,
because the guide mentions scene/node checks but not registry reconciliation.

**Required correction.** Add a Framework-owned saved-progress validation path
using the same immutable registry. For every retained checkpoint and boss, it
must verify the matching kind, progress ID, and dungeon declaration and verify
that at least one declared eligible node is present in visited history. Wire
that validation into aggregate save/restore composition without making the
optional dungeon module mandatory for games that store no dungeon progress.
Training Annex must pass its existing registry instead of maintaining a weaker
parallel whitelist. The correction must preserve the approved trusted-host
reporting model: it must not require a battle receipt or infer progress from
node entry.

**Required tests.** Reject undeclared, wrong-dungeon, wrong-kind, and
never-visited-eligible-area progress before any restored state is adopted;
accept legitimate live-produced progress and outside-location saves retaining
that progress. Replace the current impossible-state entry fixture with a
snapshot produced by the live service.

### O9-L1: active documentation and tracking overstate the current gate

The audience pages are substantially accurate about navigation/traversal
separation, host scene adoption, explicit encounter preparation, and trusted
progress reporting. They do not describe the two bypasses above:

- `docs/technical/dungeon-traversal-runtime.md:12-13` broadly claims that live
  requests and saves validate IDs, while its six-ID flow omits retained state;
- `docs/developer-guide/dungeon-traversal.md:110-121` does not tell integrators
  to reconcile restored progress with the current progress registry;
- `tests/Convergence.Framework.Tests/Fixtures/framework-capability-matrix.json:231-239`
  records `implemented` with no known gaps;
- `docs/roadmap/documentation-completion-roadmap.md:749-755` says no Framework
  contract gap remains;
- the documentation coverage matrix still says the independent audit is
  pending, while the Order 9 roadmap says an earlier audit passed.

**Required correction.** Keep Order 9 and all three audience entries open.
Record O9-M1 and O9-M2 as known gaps while their corrections are in progress.
After the runtime and restore fixes, update all three audience views together,
then run a new independent closure audit before changing either capability or
documentation status.

## Confirmed Strengths And Non-Findings

The audit deliberately did not classify the following approved boundaries as
bugs:

1. Navigation and dungeon traversal remain separate optional services.
   Retained progress does not establish current physical location.
2. Godot owns continuous movement, scenes, collision, visible enemies, and the
   decision to adopt an approved candidate after scene work succeeds.
3. Traversal does not automatically prepare an encounter or start combat.
   Fixed-floor and encounter-pool content remains optional catalog metadata.
4. Reverse movement requires a separately requested transition; barriers are
   supplied policy decisions.
5. Invalid current/request IDs, dungeon/source mismatch, legal denial, null or
   throwing policies, cancellation, and fatal failures have distinct behavior.
6. Traversal and progress results defend event ordering, before/after
   coherence, and collection immutability, including custom result creation.
7. Live progress reports are checked against kind, ID, dungeon, and current
   area before idempotence. No battle-only proof is required because approved
   progress may come from battle, puzzle, or script.
8. Leaving with retained progress and selecting a new entry point on re-entry
   is coherent. The last visited node is not silently resumed.
9. The real Godot smoke sample is not presented as a complete spatial dungeon
   game; Godot-shaped contract tests prove only the engine-neutral adoption
   boundary promised by current documentation.
10. Independent navigation/dungeon nullability remains an Order 13 question;
    no v20 save-wire change is justified by this audit alone.

## Verification Performed

The focused traversal suite passed from the audited baseline:

```text
Test Run Successful.
Total tests: 14
     Passed: 14
 Total time: 1.5944 Seconds
```

The full solution also passed before this report was written:

```text
Convergence.ContentValidator.Tests: 7 passed, 0 failed, 0 skipped
Convergence.Framework.Tests: 1,891 passed, 0 failed, 0 skipped
Convergence.DemoHost.Tests: 190 passed, 0 failed, 0 skipped
Total: 2,088 passed, 0 failed, 0 skipped
```

These green tests do not invalidate O9-M1 or O9-M2: the focused suite does not
exercise malformed retained collections, and one DemoHost test currently
asserts acceptance of the impossible checkpoint state described in O9-M2.

## Correction Roadmap

| Checkpoint | Work | Exit condition |
|---|---|---|
| O9-C1 | Enforce complete live snapshot validity and typed non-mutating rejection. | Malformed history cannot reach policy evaluation or an applied/progress result. |
| O9-C2 | Reconcile saved checkpoint/boss progress against the supplied registry before restore adoption. | Restore has the same progress authority as live reporting, while no-progress games remain optional. |
| O9-C3 | Replace impossible fixtures and align mechanics, developer, technical, capability, and coverage tracking. | Tests demonstrate legitimate live-produced progress; documents state the actual boundary. |
| O9-C4 | Fresh independent code/document review and retained release gate. | No realistic reachable Order 9 defect remains; owner confirmation may then close the Order. |

Each correction should be isolated in its own commit and reviewed before the
next begins. Order 9 must not be marked closed merely because the pre-correction
suite is green.

## Correction Progress

- **O9-C1 — complete (`76da9913`):** complete live snapshot validation now
  rejects malformed visited-node, checkpoint, and boss history before policy
  or registry evaluation and carries typed first-invalid-field evidence.
- **O9-C2 — complete (`9390290d`):** save and aggregate restore validation now
  accepts the live immutable progress registry and rejects missing authority,
  undeclared IDs, wrong kinds, wrong dungeons, and progress without an eligible
  visited area. Training Annex and the clean save demo supply their registries.
- **O9-C3 — complete (this documentation/fixture commit):** the re-entry
  fixture now earns its checkpoint through live traversal and progress services;
  all active audience, API, capability, coverage, and roadmap guidance reflects
  C1/C2. The complete Release suite passed 2,093 tests with zero failures or
  skips, and strict build/format/diff gates passed.
- **O9-C4 — pending:** perform a fresh source/document review and retained
  release gate. Order 9 and all three audience entries remain open meanwhile.
