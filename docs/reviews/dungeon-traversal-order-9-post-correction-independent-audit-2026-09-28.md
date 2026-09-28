# Dungeon Traversal Order 9 Post-Correction Independent Audit

**Audit date:** 28 September 2026  
**Reviewed source baseline:** `11e69b12`  
**Method:** fresh source, test, host-integration, and audience-document trace.
Earlier Order 9 reports were consulted only after findings were derived from
current code and current active documentation.

## Interim Verdict

The corrected runtime is coherent, and this pass found no unresolved realistic
reachable Framework or DemoHost defect in Order 9. One low-severity active
documentation/tracking contradiction prevents the closure gate from being
called complete. Order 9 and its three audience entries remain open.

## Finding

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
ordering. All three entries correctly remain `existing_unreviewed` pending this
post-correction gate and explicit owner confirmation.

## Verification At Audit Point

Before this audit document was created, the C3 baseline passed:

- focused live-produced re-entry fixture: 1/1;
- documentation/capability boundary gate: 32/32;
- full Release suite: 1,895 Framework, 191 DemoHost, seven ContentValidator
  tests (2,093 total), zero failed and zero skipped;
- strict nonincremental solution build: zero warnings and zero errors;
- format verification and `git diff --check`.

The retained release gate has not yet been claimed for C4. It must run on the
clean corrected commit after O9-C4-L1 is fixed. A green pre-fix gate would not
resolve the active-document contradiction.

## Correction Checkpoint

| Checkpoint | State | Exit condition |
|---|---|---|
| O9-C4-L1 | open | Active capability/decision evidence names the correction chain and pending C4 authority; executable documentation test passes. |
| O9-C4-GATE | pending | Fresh recheck finds no realistic reachable defect or contradiction and the retained release gate passes on the clean corrected commit. |

