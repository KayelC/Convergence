# Dungeon Traversal Order 9 Roadmap

**Opened:** 19 September 2026

**Source baseline:** `d5f8845d`

**Status:** open; O9-D1 through O9-D6 approved, fresh-audit corrections
O9-C1 through O9-C3 complete, and O9-C4 pending.

**Source evidence:** [Order 9 source review](../reviews/dungeon-traversal-order-9-source-review-2026-09-19.md).

## Objective

Finish the optional, host-neutral dungeon traversal contract and document its
interaction with generic navigation, encounter preparation, and persistence.
Keep 3D movement, visible enemy placement, scene loading, UI, and save-file
encoding in the host. Do not equate a fixed floor with one forced encounter.

The Framework already supports immutable dungeon progress and policy-approved
node transitions. This roadmap addresses the concrete missing guards and the
unresolved meaning of authored floor metadata. It does not imply that Order 8
navigation or Order 13 persistence must be reopened.

The [confirmed dungeon-progress decision](../decisions/dungeon-progress-reporting.md)
records O9-D1 through O9-D6 as approved intent. It does not describe already
implemented runtime behavior.

## Approved Decisions

| ID | Approved behavior | Implementation boundary |
|---|---|---|
| O9-D1: progress authority | The game reports a checkpoint or boss outcome after success; Framework validates and records it. No mandatory battle-result proof. | Bosses may be resolved by battle, puzzle, or script. No progress is inferred from node entry. |
| O9-D2: eligible progress IDs | The host supplies an immutable list of eligible checkpoint/boss IDs, their dungeons, and allowed areas. | Framework checks reports against the list; do not expand dungeon JSON or add a speculative policy. |
| O9-D3: authored floors | Existing floor metadata is optional catalog information. It does not move nodes or start battles. | No new resolver until a real consumer needs it; hosts may inspect catalog definitions directly. |
| O9-D4: retained progress | Leaving retains progress but disables active traversal. On re-entry the host selects an entrance or unlocked checkpoint. | Never infer re-entry from the last visited node; validate checkpoint availability before adopting it. |
| O9-D5: host-incompatible saves | A host requiring an inside-dungeon position rejects a save missing that position before adoption. | Keep generic navigation-only field saves valid; no silent entrance guess or v19 wire change. |
| O9-D6: failure contract | Invalid IDs, legal route denial, and malfunctioning custom rules have distinct outcomes; failures leave state unchanged. | Typed diagnostics; cancellation and fatal failures propagate. |

The owner confirmed all six decisions on 19 September 2026. Precise public
signatures and diagnostic names are implementation details to review against
these decisions, not invitations to alter their semantics.

The [Order 8 decision](../decisions/navigation-and-host-adoption.md) already
settles that current logical location, not retained dungeon progress, governs
active save/menu context. Do not ask the owner to decide that again.

## Ordered Checkpoints

Each checkpoint has its own narrow commit and focused tests. If source work
reveals a new design choice outside the approved decisions, stop and ask the
owner rather than silently choosing a content or save model.

| Checkpoint | State | Work and acceptance evidence |
|---|---|---|
| O9-R1: opening review | complete | Source-first review, this roadmap, executable `partial`/`open` tracking, and correction of present-tense documentation that overclaims policy authority. No runtime change. |
| O9-R2: live traversal boundary | complete | Reject default/empty dungeon, node, and transition IDs before policy evaluation; define non-mutating typed policy-fault behavior. Test null/throwing policies, cancellation, source/dungeon mismatch precedence, and unchanged visited state under O9-D6. |
| O9-R3: public result authority | complete | Seal traversal/state-change result and event coherence, including malformed custom-service results and record cloning. Keep before/after snapshots and ordered events immutable; no silent fallback. |
| O9-R4: field/save/host boundary | complete | Cover all four navigation/progress combinations from the source review. Reject Training Annex's accepted-but-unusable inside-without-progress restore before adoption, preserve outside-with-retained-progress behavior and `CurrentSaveContext`, and prove host scene failure never adopts a traversal candidate. Re-entry explicitly chooses an entrance or unlocked checkpoint under O9-D4/D5. |
| O9-R5: checkpoint and boss recording | complete | Use the host-supplied immutable eligibility list and validate dungeon, ID, and allowed area before idempotent recording. Do not require battle proof or invent victory on traversal. Test loss, win, puzzle/script success, duplicate report, wrong dungeon/node, and malformed ID under O9-D1/D2. |
| O9-R6: authored floor and encounter contract | complete | Keep existing fixed-floor metadata optional and readable directly from the catalog; verify fixed encounter IDs, floor bounds, duplicate floor handling, empty pools, and multiple host triggers on one floor. Neither entry nor metadata access starts combat. Do not add a resolver or schema change without a newly demonstrated need. |
| O9-R7: audience documentation | written_pending_owner_confirmation | Reconcile the mechanics page; write a Godot/console developer guide and a technical state/sequence page. Show active versus retained progress, host scene adoption, trigger-to-preparation handoff, save validation, rejection, and boss/checkpoint reporting. All three audience entries remain `existing_unreviewed` until the post-correction audit and owner confirmation. |
| O9-R8: independent closure | pending | The 21 September audit and retained gate remain historical evidence, but the later 23 September fresh audit found two reachable authority gaps and reopened this active closure checkpoint. O9-C4 must independently review the corrected state. |

## Fresh-Audit Correction Checkpoints

The [23 September fresh closure audit](../reviews/dungeon-traversal-order-9-fresh-closure-audit-2026-09-23.md)
supersedes the earlier no-gap conclusion without erasing its historical gate.

| Checkpoint | State | Work and acceptance evidence |
|---|---|---|
| O9-C1: complete live-state validity | complete | Validate current and retained IDs before traversal policy or progress-registry evaluation; return typed, non-mutating evidence for the first malformed field. |
| O9-C2: restored progress authority | complete | Validate retained checkpoint/boss records against the live immutable registry before save/restore adoption, while keeping games with no retained progress independent of the optional module. |
| O9-C3: fixtures and audience alignment | complete | Produce re-entry evidence through live traversal/progress services and align mechanics, developer, technical, API, capability, and coverage guidance with C1/C2. |
| O9-C4: post-correction independent closure | pending | Re-read current source and audience documentation, run and retain the release gate, and report realistic reachable findings. Owner confirmation remains required for closure. |

## Boundary Contract To Preserve

1. Navigation changes a logical location; dungeon traversal changes a
   meaningful dungeon node. Neither silently calls the other.
2. Outside navigation plus retained dungeon progress is legal. The host must
   not treat that progress as proof of current physical presence.
3. `RuntimeFieldSnapshot` remains v19-shaped during this Order unless a
   separately approved save-contract change is unavoidable. Wider independent
   nullability belongs to Order 13.
4. Godot owns spatial movement, collisions, Nodes, scene activation, and
   visible entities. Framework approval produces a candidate state; host work
   must succeed before adoption.
5. Encounter triggers are host-reported and may occur zero, one, or many times
   on a floor according to game rules. Battle preparation and victory reporting
   are explicit, separate operations.
6. Checkpoint/boss progress must never be inferred from merely entering a
   node. A host may report a non-battle success when the approved eligibility
   contract allows it.
7. Host-supplied policies may choose legal edges; the Framework must not hardcode
   Training Annex node names, stair menus, one-battle-per-floor assumptions,
   or a Godot scene graph.

## Test And Release Gate

- Focused Framework traversal, navigation, persistence, encounter-preparation,
  and catalog/content-validation tests.
- Training Annex scripted entry, traversal, leave, re-entry, manual/suspend
  save/load, inside-without-progress rejection or initialization, and
  outside-with-retained-progress regression tests.
- Godot-shaped scene-success/failure adoption and host-owned save-envelope
  tests. Run the real Godot headless smoke as a regression gate, but do not
  mislabel its current action/encounter/save flow as live dungeon navigation.
- Full `dotnet test Convergence.sln --no-restore --configuration Release`,
  strict nonincremental .NET 8 builds, format verification, content validator,
  five DemoHost modes, documentation links, API/schema/save-version checks if
  their contracts change, forbidden-reference search, and `git diff --check`.
- Retain raw command output and checksum manifest with
  `eng/Invoke-VerificationEvidence.ps1` for the final reviewed commit. Do not
  call Order 9 closed because focused tests alone pass.

## Completion Record

O9-R1 is documentation/tracking only. The 19 September opening review ran 101
focused Framework tests and three related DemoHost tests, all passing without
skips. After this opening record, the full Release solution passed 1,878
Framework, 186 DemoHost, and seven ContentValidator tests (2,071 total), with
zero failures or skips. Format verification, changed-document relative links,
and `git diff --check` passed. No runtime, schema, content, or save-wire file
changed. The owner subsequently confirmed O9-D1 through O9-D6. The remaining
checkpoints await implementation, adversarial review, and a retained release
gate; confirmation alone does not advance their state.

O9-R2 adds typed `InvalidRequest` (with the first invalid ID field) and
`PolicyFaulted` (exception or null decision) traversal outcomes. Existing
applied, mismatch, and legal policy-rejection outcomes retain their codes and
ordering. A malformed request is rejected before dungeon/source mismatch and
before policy evaluation; rejected and faulted results retain the exact before
snapshot and visited list. Cancellation and fatal memory failures still
propagate. This is a pre-release public constructor signature change for
`RuntimeDungeonTraversalResult`; source callers may omit the new optional
diagnostic arguments but binaries must be recompiled. Seven focused
traversal tests and the full Release suite passed: 1,881 Framework, 186
DemoHost, seven ContentValidator tests; zero failures/skips. Strict solution
build had zero warnings and `dotnet format --verify-no-changes` passed. R3
still owns public result/event coherence.

O9-R3 validates transition result code, request, before/after state, event
kind, event IDs, and rejection diagnostics together. Applied checkpoint/boss
results must change only the reported progress list; already-recorded results
must leave state unchanged. Event records no longer expose public `init`
setters, so cloning cannot rewrite event evidence. Malformed policy reason IDs
become typed `MalformedDecision` faults rather than escaping during event
construction. This pre-release API break requires custom event creators to use
the validated constructor and recompile. Eleven focused traversal tests and
the full Release suite passed: 1,885 Framework, 186 DemoHost, seven
ContentValidator tests; zero failures/skips. Strict solution build had zero
warnings and formatting verification passed.

O9-R4 leaves the generic `RuntimeFieldSnapshot` and save contract unchanged.
Training Annex now rejects a restored inside location without a dungeon
position before adopting any actor or field state. `CurrentSaveContext` still
depends on navigation alone in all four combinations of inside/outside and
present/absent progress. On entry, the host explicitly selects the entrance;
its entry helper also accepts the Review Checkpoint only when that checkpoint
was previously unlocked, preserving visited/unlocked/defeated progress without
resuming at the last node by accident. A Godot-shaped test keeps an approved
traversal result as a candidate until a scene load succeeds; failed loading
leaves the active node and visited list unchanged. Focused Training Annex (122)
and Godot contract (five) tests passed. The full Release suite passed 1,886
Framework, 189 DemoHost, and seven ContentValidator tests; zero failures or
skips. Strict solution build had zero warnings and formatting verification
passed. This checkpoint does not add a Godot scene API to Framework.

O9-R5 requires hosts to inject an immutable `RuntimeDungeonProgressRegistry`
alongside the traversal policy. Each declaration names checkpoint or boss,
progress ID, dungeon ID, and permitted node IDs; null/duplicate/invalid
declarations are rejected. `UnlockCheckpoint` and `RegisterBossDefeat` check
malformed IDs, unknown eligibility, wrong dungeon, and wrong area before
idempotence. Their typed results carry progress kind and ID, and rejected
reports retain the exact live snapshot with no event. Merely traversing a node
does not register victory; a lost encounter can issue no report, while a host
may report battle, puzzle, or script success without supplying battle proof.
Training Annex explicitly registers its Review Checkpoint at Review Alcove;
hosts without progress mechanics inject an empty registry. This is a
pre-release API break for the service and state-change result constructors;
integrators must recompile and inject their declarations. Fourteen focused
traversal tests and the full Release suite passed: 1,889 Framework, 189
DemoHost, seven ContentValidator tests; zero failures/skips. Strict solution
build had zero warnings and formatting verification passed.

O9-R6 keeps `DungeonDefinition` metadata optional and directly readable from
the catalog. Semantic validation now rejects duplicate fixed-floor numbers
within one block and unsupported fixed-floor kinds; existing range, encounter
reference, and required battle/boss encounter checks remain active. An empty
encounter pool is valid. Training Annex content tests read the authored fixed
encounter ID and prepare two separate host-triggered encounter plans with
different actor instance IDs without changing traversal progress or starting
a battle. No resolver, schema, runtime auto-encounter, or production content
change was introduced. Twenty-five focused content tests and the full Release
suite passed: 1,891 Framework, 189 DemoHost, seven ContentValidator tests;
zero failures/skips. Strict solution build had zero warnings and formatting
verification passed.

During O9-R7 source reconciliation, a host-only R5 follow-up was found:
`TrainingAnnexFieldPresenter` described every non-applied progress result as
"already unlocked," even when the new typed result was `NotEligible`,
`AreaMismatch`, or another rejection. The presenter now preserves its existing
already-recorded text only for `AlreadyRecorded` and names other rejection
codes without adopting state. One focused regression and the full Release
suite passed: 1,891 Framework, 190 DemoHost, seven ContentValidator tests;
zero failures/skips. Strict build had zero warnings and formatting passed.

O9-R7 reconciles the player-facing traversal section and adds a dedicated
developer integration guide and technical state/sequence reference. The three
views explain the approved transition candidate versus host adoption,
eligibility-checked progress reports, independent trigger-to-encounter
preparation, retained history versus active location, and host-specific
inside-save validation. The documentation matrix now points to all three
pages, with each entry `existing_unreviewed` pending independent O9-R8 audit
and explicit owner confirmation. Documentation coverage has 36 reviewed,
21 existing_unreviewed, 11 missing, and seven not_applicable entries. Focused
documentation coverage (10) and active-link (one) tests passed. The full
Release solution passed 1,891 Framework, 190 DemoHost, and seven
ContentValidator tests (2,088 total), with zero failures or skips. Strict
solution build reported zero warnings; format verification and `git diff
--check` passed. No runtime behavior, save wire, schema, or content changed
in this documentation checkpoint. R8 and owner review remain open.

O9-R8 independently re-read the live traversal and progress services,
navigation/field/save/restore boundary, floor validation, Training Annex,
Godot-shaped contracts, and all three audience pages. It found one stale
decision record and its matching architecture assertion (O9-DOC1), plus stale
capability gaps/counts (O9-TRACK2). Those were corrected in separate commits;
the final recheck found no further realistic reachable Order 9 defect or
documentation contradiction. `dungeon_traversal` is now `implemented` with
an empty known-gap list but `orderState: open`. The retained 23-command release
gate tested `0189a6c2` and passed: 1,891 Framework, 190 DemoHost, seven
ContentValidator tests; zero failures/skips/warnings; 90.36% Framework line
and 77.23% branch coverage; six active packs and 36 documents validated;
all five DemoHost modes and Godot 4.7.1 headless smoke passed. Raw outputs,
reviewed diff, and checksums are in
`artifacts/verification/o9-r8-verified/0189a6c26138cb6fe105899c906b13c9bc9a7d40`.
The [independent audit](../reviews/dungeon-traversal-order-9-independent-audit-2026-09-21.md)
records both findings and corrections. O9-R8 is not marked complete because
the three audience pages remain `existing_unreviewed` until explicit
project-owner confirmation. No Framework runtime, schema, or save-wire change
was made during R8; its source change was an architecture-test assertion.

## Fresh-Audit Correction Completion Record

O9-C1 (`76da9913`) validates every retained visited-node, checkpoint, and boss
ID before traversal policy or progress-registry evaluation. Invalid requests
identify the first malformed field, retain the exact before-state, emit no
event, and do not call the policy. Sixteen focused traversal tests passed. The
full Release suite passed 1,893 Framework, 190 DemoHost, and seven
ContentValidator tests (2,090 total), with zero failures or skips; strict build,
format verification, and `git diff --check` passed.

O9-C2 (`9390290d`) gives save and aggregate restore validation the same
immutable progress registry used by live reports. Retained progress now rejects
missing registry authority, undeclared IDs, wrong kinds, wrong dungeons, and
records without an eligible visited node. Games retaining no checkpoint/boss
records remain independent of the optional registry. Training Annex and the
clean save demo both supply their real declarations; the weaker host whitelist
is gone. Focused persistence (83) and DemoHost save/Training Annex (140) tests
passed. The full Release suite passed 1,895 Framework, 191 DemoHost, and seven
ContentValidator tests (2,093 total), with zero failures or skips; strict build,
format verification, both relevant demos, and `git diff --check` passed. Save
contract v19 remains unchanged because the snapshot shape did not change.

O9-C3 (this documentation/fixture commit) replaces the manually forged
checkpoint-entry fixture with state produced by entrance-to-hall-to-alcove
traversal, live checkpoint recording, and return-to-hall traversal. The test
therefore still proves that re-entry selection does not use the last node while
also proving the retained checkpoint was legally earned. Mechanics, developer,
technical, API, capability, coverage, and roadmap guidance now describe complete
live-history validation and registry-backed restore validation. The focused
host regression passed one test and the documentation/capability gate passed 32
tests. The full Release suite passed 1,895 Framework, 191 DemoHost, and seven
ContentValidator tests (2,093 total), with zero failures or skips. Strict build
reported zero warnings; format verification and `git diff --check` passed.
O9-C4 and explicit owner confirmation remain required; this record does not
close Order 9 or promote its three audience entries.
