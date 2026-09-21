# Dungeon Traversal Order 9 Independent Audit

**Initial reviewed implementation:** `7c61792a`

**Post-correction gate revision:** `0189a6c2`
**Date:** 21 September 2026  
**State:** independent review passed after corrections; owner documentation confirmation pending.

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

At the reviewed base, the executable capability matrix labeled
`dungeon_traversal` `partial` and listed four known gaps. Its live-input,
eligible-progress, and incompatible Training Annex save gaps were corrected
in O9-R2, R5, and R4. The remaining
"fixed-floor metadata is not consumed by runtime traversal" item is not a
gap under approved O9-D3: metadata is intentionally optional catalog
information and no resolver is planned without a consuming need. This fixture
misleads readers about what remains to build and contradicts the matrix's
explicit separation of implementation maturity from Order closure. Change the
implementation to `implemented` with no known gaps while keeping `orderState`
`open` and all three audience entries `existing_unreviewed`. Reconcile the
active count/roadmap prose and its architecture assertion together.

**Correction:** `0189a6c2` records `implemented` with no known Framework
contract gaps, retains `orderState: open`, and keeps all three audience entries
`existing_unreviewed`. Its architecture guard and active roadmap counts were
updated together. The full solution passed 1,891 Framework, 190 DemoHost, and
seven ContentValidator tests; the strict build had zero warnings.

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

## Post-Correction Code And Document Recheck

The corrected decision record now describes eligibility-checked reporting,
and its test checks the implemented registry rather than a retired sentence.
The capability fixture now distinguishes an implemented optional traversal
contract from an open collaborative Order. The mechanics page, integration
guide, and technical diagrams agree with the re-read source on request
precedence, non-mutating failures, host scene adoption, optional floor
metadata, explicit encounter preparation, outside retained progress, and
host-specific inside-save rejection. No realistic reachable runtime defect or
remaining source/document contradiction was found in the Order 9 scope after
these corrections. The real Godot sample still does not provide live spatial
dungeon traversal; the test-only scene-adoption contract is not presented as
that sample feature.

The final retained gate tested clean commit `0189a6c2` and passed all 23
commands, including locked NuGet audit, strict zero-warning builds, format,
architecture and full tests, 90.36% Framework line/77.23% branch coverage,
active-content validation, all five DemoHost modes, real Godot 4.7.1 headless
smoke, trimming analysis, and reviewed-range diff. Full test totals were
1,891 Framework, 190 DemoHost, seven ContentValidator, zero failures/skips.
The checksum manifest and raw outputs are retained at
`artifacts/verification/o9-r8-verified/0189a6c26138cb6fe105899c906b13c9bc9a7d40`.
Two earlier failed attempts are also retained with their exact network-audit
and stale-assertion failures; they are not substituted for this successful
gate.

## Closure Gate

The implementation, independent source/document recheck, and retained release
gate are complete. The project owner still needs to confirm or correct the
three Order 9 audience pages. Until then each remains `existing_unreviewed`
and Order 9 remains `open`, despite `implementationState: implemented`.
