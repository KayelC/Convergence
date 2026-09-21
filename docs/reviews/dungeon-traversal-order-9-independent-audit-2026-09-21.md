# Dungeon Traversal Order 9 Independent Audit

**Reviewed implementation:** `7c61792a`  
**Date:** 21 September 2026  
**State:** correction required; Order 9 remains open.

## Review Method

This pass re-read current source and executable tests, then compared the three
audience pages and confirmed decision with the implemented behavior. Earlier
review summaries were not treated as runtime evidence. The trace covered
`DungeonTraversal.cs`, generic navigation, the field and save snapshots,
aggregate restore, dungeon content validation, Training Annex entry/menu/save
flows, Godot-shaped adoption tests, and the real Godot sample boundary.

## Finding O9-DOC1: Decision Record Describes Retired Behavior

**Severity:** documentation correctness, not a runtime vulnerability.

The confirmed [dungeon progress decision](../decisions/dungeon-progress-reporting.md)
still says an unvalidated host assertion matches the current service and that
the dungeon developer/technical pages remain scheduled. The current
`RuntimeDungeonTraversalService.RecordProgress` checks report IDs, the injected
eligibility registry, dungeon, and allowed node before idempotence or mutation.
O9-R7 already added the developer and technical pages. A developer reading the
active decision record could wrongly conclude that a progress report still
accepts any ID from any node. Update only the stale implementation and
documentation-status language; preserve the confirmed design decision and
historical alternatives.

**Correction:** `cd59b6f6` updates the decision record. The corresponding
architecture assertion was still pinned to its retired phrase, so
`c3a7356e` changes that gate to assert the implemented registry and pending
audience state instead. The raw failed intermediate gate is retained under
`artifacts/verification/o9-r8-online-failed-20260921T144015Z`.

## Finding O9-TRACK2: Capability Matrix Retains Closed Gaps

**Severity:** progress-reporting correctness, not a runtime vulnerability.

The executable capability matrix still labels `dungeon_traversal` `partial`
and lists four known gaps. Its live-input, eligible-progress, and incompatible
Training Annex save gaps were corrected in O9-R2, R5, and R4. The remaining
"fixed-floor metadata is not consumed by runtime traversal" item is not a
gap under approved O9-D3: metadata is intentionally optional catalog
information and no resolver is planned without a consuming need. This fixture
misleads readers about what remains to build and contradicts the matrix's
explicit separation of implementation maturity from Order closure. Change the
implementation to `implemented` with no known gaps while keeping `orderState`
`open` and all three audience entries `existing_unreviewed`. Reconcile the
active count/roadmap prose and its architecture assertion together.

## Source And Boundary Evidence

- `RuntimeDungeonTraversalService.Traverse` validates six request IDs before
  mismatch and policy evaluation; ordinary denial and policy fault are typed,
  non-mutating results. Cancellation and fatal memory failure propagate.
- `RuntimeDungeonProgressRegistry` copies and validates declarations;
  `RecordProgress` checks kind/ID, dungeon, and allowed node before checking
  whether progress was already recorded. Transition approval alone does not
  report a checkpoint or boss result.
- Traversal and progress result constructors check state/event coherence and
  copy ordered event lists. The snapshot constructor copies progress lists.
- `RuntimeFieldSnapshot` keeps navigation required when field state exists and
  dungeon progress optional. Save v19 validates IDs and catalog dungeon
  identity; it does not prove a host scene exists. Aggregate restore retains
  the validated field snapshot without replaying travel.
- Training Annex chooses an entrance on re-entry, retains outside progress,
  derives `CurrentSaveContext` from logical navigation, and rejects an inside
  save lacking a dungeon position before adopting restored state. Godot-shaped
  tests keep an approved candidate unapplied when scene loading fails. The
  real Godot sample does not implement spatial dungeon traversal; the audience
  pages do not claim it does.
- Dungeon content validation checks fixed-floor range, duplicates within a
  block, kind, and encounter references. Metadata is not a runtime encounter
  trigger. Tests prepare distinct host-triggered encounters from one floor.

No additional realistic reachable Order 9 runtime defect was identified in
these paths at `7c61792a`. This is bounded evidence, not a claim that future
Godot scenes, content, or host policies have been implemented or certified.

## Closure Gate

O9-DOC1 is corrected. Correct O9-TRACK2 in a separate commit, re-read the
affected audience and tracking claims, run the retained release gate against
the resulting clean revision, and obtain explicit project-owner confirmation
of the three Order 9 audience pages. A 23-command interim gate against
`c3a7356e` passed with 1,891 Framework, 190 DemoHost, and seven validator
tests, 90.36% Framework line coverage, 77.23% branch coverage, zero build
warnings, and real Godot 4.7.1 headless smoke. Its raw checked evidence is
`artifacts/verification/o9-r8-final/c3a7356e012cf85cf14830798bfa0e2f53737338`.
Because O9-TRACK2 changes the executable matrix after that gate, rerun the
retained gate before treating it as final evidence.
Until then all three remain `existing_unreviewed`, the implementation matrix
stays `partial`, and Order 9 stays `open`.
