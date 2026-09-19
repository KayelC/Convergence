# Dungeon Traversal Order 9 Roadmap

**Opened:** 19 September 2026

**Source baseline:** `d5f8845d`

**Status:** open; planning and owner decisions precede runtime changes.

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

The [proposed dungeon-progress decision](../decisions/dungeon-progress-reporting.md)
records why explicit host reporting is preferred and which details still need
owner confirmation.

## Decisions To Confirm Before Runtime Edits

| ID | Working direction | Status / required answer |
|---|---|---|
| O9-D1: progress authority | A game reports a checkpoint or boss outcome explicitly; Framework checks the report and records it. No mandatory battle-result proof. | Direction discussed and used for planning; confirm the exact eligibility evidence before O9-R5. |
| O9-D2: eligible progress IDs | Checkpoint and boss IDs need a trustworthy dungeon/context declaration. | Decide whether a host-supplied immutable registry is sufficient or authored dungeon content needs a small extension. Do not invent a policy for a simple identity/shape correction. |
| O9-D3: authored floors | Floor metadata does not automatically move nodes or start battles. | Confirm whether Order 9 should add a read-only fixed-floor resolver or leave the current floor content independent until a real host consumes it. |
| O9-D4: retained progress | Leaving a dungeon retains progress but makes traversal inactive until re-entry. | Confirm whether re-entry resumes the last node or starts at an entrance/checkpoint selected by the host. The current Training Annex retains state, but this should not become a hidden Framework default. |
| O9-D5: host-incompatible saves | Framework keeps navigation-only field saves valid; a host requiring dungeon state must handle its own inside-without-progress case. | Recommend typed host rejection before adoption; confirm whether Training Annex should reject or explicitly initialize an entrance snapshot. |
| O9-D6: failure contract | Invalid IDs and programming faults are distinct from a legal policy denial. | Recommend navigation-grade typed, non-mutating results; operational cancellation propagates. Confirm precise public result shape before changing APIs. |

The [Order 8 decision](../decisions/navigation-and-host-adoption.md) already
settles that current logical location, not retained dungeon progress, governs
active save/menu context. Do not ask the owner to decide that again.

## Ordered Checkpoints

Each checkpoint has its own narrow commit and focused tests. Stop for owner
confirmation if a later checkpoint requires an unresolved decision above; do
not silently choose a content or save model mid-implementation.

| Checkpoint | State | Work and acceptance evidence |
|---|---|---|
| O9-R1: opening review | complete | Source-first review, this roadmap, executable `partial`/`open` tracking, and correction of present-tense documentation that overclaims policy authority. No runtime change. |
| O9-R2: live traversal boundary | pending | Reject default/empty dungeon, node, and transition IDs before policy evaluation; define non-mutating typed policy-fault behavior. Test null/throwing policies, cancellation, source/dungeon mismatch precedence, and unchanged visited state. Requires O9-D6 confirmation. |
| O9-R3: public result authority | pending | Seal traversal/state-change result and event coherence, including malformed custom-service results and record cloning. Keep before/after snapshots and ordered events immutable; no silent fallback. |
| O9-R4: field/save/host boundary | pending | Cover all four navigation/progress combinations from the source review. Correct Training Annex's accepted-but-unusable inside-without-progress restore, preserve outside-with-retained-progress behavior and `CurrentSaveContext`, and prove host scene failure never adopts a traversal candidate. Requires O9-D4/D5 confirmation. |
| O9-R5: checkpoint and boss recording | pending | Implement the chosen eligibility declaration and validate dungeon, ID, and allowed context before idempotent recording. Do not require battle proof or invent victory on traversal. Test loss, win, puzzle/script success, duplicate report, wrong dungeon/node, and malformed ID according to O9-D1/D2. |
| O9-R6: authored floor and encounter contract | pending | Resolve O9-D3. If a resolver is approved, make it read-only and explicit; verify fixed encounter IDs, floor bounds, duplicate floor handling, empty pools, and multiple host triggers on one floor. Neither entering a floor nor resolving metadata starts combat. Preserve existing clean packs with an explicit schema/version decision if content shape changes. |
| O9-R7: audience documentation | pending | Reconcile the mechanics page; write a Godot/console developer guide and a technical state/sequence page. Show active versus retained progress, host scene adoption, trigger-to-preparation handoff, save validation, rejection, and boss/checkpoint reporting. Promote coverage entries only after source verification and owner review. |
| O9-R8: independent closure | pending | Fresh code and document review across traversal, navigation, `RuntimeFieldSnapshot`, `CurrentSaveContext`, save validator/restore, content, DemoHost, and Godot contract. Run and retain the full release gate. Close only if no concrete gap remains and all applicable audience entries are `reviewed`. |

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
changed. The remaining checkpoints are proposals pending the owner decisions
above, implementation, adversarial review, and a retained release gate.
