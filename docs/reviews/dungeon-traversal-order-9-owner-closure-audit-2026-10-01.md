# Dungeon Traversal Order 9 Owner-Closure Audit

**Audit date:** 1 October 2026  
**Reviewed source baseline:** `cc932dd3`  
**Capability:** `dungeon_traversal`  
**Status:** corrections and retained verification complete; explicit owner closure pending

## Method

This review began from current source and executable tests. Earlier Order 9
reviews, roadmaps, and summaries were not used to form the findings. They were
consulted only after the implementation trace was complete, when the three
audience pages and confirmed decision record were cross-checked against source.

The source-first pass covered:

- `RuntimeDungeonTraversalService`, its snapshots, registries, results, and
  events;
- `RuntimeFieldSnapshot`, save validation, and aggregate restoration;
- dungeon content validation and encounter preparation;
- Training Annex traversal, re-entry, save-context, validation, and both clean
  demo paths;
- the real Godot save codec and smoke host;
- the Godot-shaped integration contract tests; and
- traversal, persistence, content, DemoHost, and architecture tests.

The colleague's seven leads were treated as hypotheses and checked against the
current tree. The conclusions below replace, rather than inherit, any earlier
audit verdict.

## Closure Recommendation Before Corrections

**Reject closure pending documentation correction and a fresh retained gate.**

No realistic reachable defect was found in the public traversal transition or
live progress-reporting services. The implementation preserves typed,
non-mutating failure, validates retained live IDs before policy/registry work,
keeps progress reporting separate from encounter success, and leaves scene
adoption to the host.

The active documentation is not yet an exact account of that implementation.
One mechanics claim overstates the security/provenance guarantee of save
validation, the real Godot sample's persistence evidence is easy to confuse
with a test-only store, the eight approved decisions are tracked as six, and
several save and host-composition limits are not stated precisely enough for a
commercial integration contract.

## Findings

### O9-OWNER-M1: Save validation proves structural plausibility, not earned provenance

**Intended boundary.** Under O9-D1 through O9-D4, a trusted host reports
progress only after its success condition. Framework validates the report's
declared context; it does not independently prove what happened in a scene,
battle, puzzle, or script.

**Current source.** `RuntimeSaveValidator.ValidateRetainedProgress` builds its
eligible-area evidence from `RuntimeDungeonTraversalSnapshot.VisitedNodeIds`
in the same save being validated. It then accepts a retained progress ID when
the supplied registry has a matching kind and dungeon and at least one allowed
node is present in that saved history.

**Reachable path.** A host can construct or deserialize a snapshot containing
both an eligible visited node and its checkpoint/boss ID without first calling
the live traversal and progress services. Registry-backed validation accepts
that internally consistent shape. This is expected for a serializer-neutral
framework that trusts its host, but it is not proof that the progress was
earned.

**Documentation error.** The mechanics page says this validation “prevents a
save from granting progress that live play could not have recorded.” That is
stronger than the code. The correct claim is that validation rejects retained
progress that is inconsistent with the current registry and saved history.
Authenticity, tamper resistance, and historical proof require a host-owned
integrity/provenance mechanism.

**Correction.** State the trusted-host boundary in all three audience views and
the public API guidance. Rename the test that calls missing-history input
“forged,” and add executable evidence that a structurally plausible
host-constructed snapshot is intentionally accepted.

### O9-OWNER-M2: The real Godot sample does not persist Order 8 or Order 9 state

**Current source.** `GodotSaveDocument` contains actors, party roster,
inventory, currency, shop stock, and scene-instance records. It has no field,
navigation, or dungeon member. `GodotSaveCodec.DeserializeAndRestore` always
constructs `RuntimeSaveGameSnapshot` with `field: null`, and the smoke host
uses the ordinary validator.

`GodotIntegrationContractTests` separately contains a test-only in-memory
`GodotSaveSnapshotStore` that round-trips a `RuntimeFieldSnapshot`. That proves
the engine-neutral snapshot can sit inside host-owned storage; it does not
exercise the real sample codec, registry-backed save validation, or an actual
Godot scene restore.

**Consequence.** An integrator can read “Godot contract” evidence as proof that
the shipped sample persists traversal when it does not. This is an evidence
and adoption-guidance defect, not a Framework runtime defect.

**Correction.** Name the two evidence layers explicitly. The real sample must
be described as a neutral `Field == null` save path until it actually serializes
and validates field/dungeon state.

### O9-OWNER-L1: Registry composition and content-evolution compatibility need an explicit contract

The ordinary `RuntimeSaveValidator` deliberately has no dungeon-progress
registry. It remains valid for absent field state, navigation-only state, or a
dungeon snapshot with empty checkpoint/boss lists. A save with retained
checkpoint/boss IDs is rejected with `DungeonProgressRegistryMissing`; callers
must use `CreateWithDungeonProgressRegistry` with the same declarations as
live traversal.

This is discoverable in the developer and API pages, and all current live
callers are correctly composed. The real Godot sample is not an exception; it
restores `Field == null`. The docs nevertheless need the exact 99-103
diagnostics and a direct warning that registry/content evolution can reject an
old save. Content-pack identity/version is already validated exactly. Renaming,
removing, changing kind/dungeon, or retargeting retained progress therefore
requires host-owned migration or explicit incompatibility handling. The save
contract remained v19 because the serialized shape did not change; that does
not promise semantic compatibility across changed content/registries.

### O9-OWNER-L2: Framework-valid and Training-Annex-valid field combinations are different

`RuntimeFieldSnapshot` supports three structural shapes: no field aggregate;
navigation without dungeon state; and navigation with dungeon state. A field
aggregate always requires navigation. Framework does not couple a navigation
location ID to a dungeon/node pair.

The interactive Training Annex adds stronger rules: it recognizes only the
staging-area and annex-entrance navigation IDs, requires a dungeon position for
the inside location, and recognizes only its named nodes. Outside plus retained
dungeon progress remains legal. The noninteractive Training Annex demo uses
`training_annex_floor_2` with `review_alcove`; Framework accepts that illustrative
pair, but the interactive Training Annex validator would reject it.

The docs describe the generic/host split but do not disclose this concrete
sample difference. It must be explicit so one demo is not presented as a save
fixture for the other.

### O9-OWNER-L3: Eight approved decisions are mislabeled as six

The confirmed decision body contains eight distinct owner-approved statements,
and O9-D8 is the typed non-mutating failure rule. The active decision preamble,
roadmap table, product roadmap, documentation roadmap, and architecture test
compress or label them as O9-D1 through O9-D6. This is a tracking defect that
can lose design intent after context loss.

The active record must use O9-D1 through O9-D8 consistently without changing
their meaning.

### O9-OWNER-L4: One diagram and one test name overstate their evidence

The technical progress diagram asks only whether “Dungeon, node, and progress
IDs” are valid, while the service also validates every retained visited-node,
checkpoint, and boss ID first. The test named
`RuntimeSessionRestoreService_AcceptsLiveProducedProgressOutsideDungeonAndRejectsForgedProgressAtomically`
rejects a checkpoint whose eligible node is absent; it does not test forged
history that includes the eligible node. Both should be made exact.

## Colleague Leads Reconciled

| Lead | Verdict from current source |
|---|---|
| 1. Eligible area comes from the same save | **Confirmed as a documentation/trust-boundary defect, not a live traversal defect.** It proves structural plausibility, not provenance. |
| 2. Default validator rejects retained progress | **Confirmed and intentional fail-closed behavior.** Current callers are correctly composed; discoverability and exact diagnostics need strengthening. No v19 shape change occurred. |
| 3. Content changes can invalidate saves | **Confirmed known limit.** Exact pack-version checks and registry diagnostics reject incompatibility; migration remains host-owned/deferred. |
| 4. Real Godot codec omits field state | **Confirmed.** The field round-trip belongs to a test-only store, not the real sample codec. |
| 5. Demo navigation/dungeon pairing | **Confirmed sample-boundary gap in docs.** Framework accepts independent IDs; the interactive host imposes stronger pairing rules. |
| 6. `SelectDungeonEntry` throws | **Not a D8 runtime defect on the current reachable path.** It is an internal host helper called from fixed, previously offered choices; bad input is programmer misuse. D8 governs public Framework traversal/progress results. If the helper becomes public or receives untrusted IDs, it should become a typed host result first. |
| 7. Audience docs may lag codes and behavior | **Confirmed in part.** Core transition/progress behavior is accurate, but the provenance claim, D1-D8 labels, diagnostic details, Godot evidence, compatibility limit, and progress diagram require correction. |

## Runtime Defect Assessment

No code defect requiring a runtime, schema, or save-wire change is confirmed by
this pass. In particular:

- accepting structurally plausible host-supplied save history is consistent
  with the approved trusted-host model;
- the ordinary validator's registry-missing rejection is deliberate;
- strict rejection after content/registry changes is honest pre-release
  compatibility behavior in the absence of a migration; and
- `SelectDungeonEntry` is an internal sample precondition helper, not the public
  typed traversal boundary.

The documentation corrections must not be used to imply tamper resistance that
the runtime does not provide.

## Required Correction And Verification Sequence

1. Reconcile D1-D8 across the decision, roadmap, tracking prose, and executable
   architecture assertions.
2. Correct the three audience pages: structural plausibility versus provenance,
   exact diagnostics, content-evolution/migration limits, generic versus
   Training Annex combinations, the internal helper boundary, and actual versus
   test-only Godot evidence.
3. Update related API/Godot guidance and coverage reasons without promoting the
   three audience entries.
4. Correct the misleading persistence-test name and add a trust-boundary
   regression.
5. Run and retain the complete release gate from the corrected clean commit.
6. Re-read the resulting diff and only then issue an owner-closure
   recommendation.

## Correction And Retained-Gate Record

O9-C6 completed the required truth correction in commit `13456815`. The
reviewed change set contains documentation, executable documentation
assertions, a corrected persistence-test name, and one regression proving the
trusted-host boundary. It contains no Framework runtime, schema, content, or
save-wire change.

O9-C7 then ran the canonical 23-command retained gate against clean commit
`1345681594ee8d9bf34233877f7e73bc2745c9b8`, reviewing
`cc932dd375dd9a3d4e6f75d3755201119c4a350f..1345681594ee8d9bf34233877f7e73bc2745c9b8`.
Every command exited zero:

- focused Framework, DemoHost, and architecture runs passed 275, 142, and 66
  tests respectively;
- the full solution passed 1,896 Framework, 191 DemoHost, and seven
  ContentValidator tests (2,094 total), with zero failures and zero skips;
- Framework line coverage was 90.38% and branch coverage was 77.32%;
- strict Framework, solution, Godot, and trimming builds completed with zero
  warnings;
- six packs, 36 documents, and 98 qualified definitions passed schema,
  deserialization, semantic, dependency, registration, and catalog checks;
- all five DemoHost modes and the real Godot 4.7.1 headless smoke completed;
  and
- format verification, `git diff --check`, reviewed-range capture, and
  evidence checksums completed.

The raw outputs, reviewed diff, manifest, coverage payload, and SHA-256 file
are retained at
[`artifacts/verification/o9-c7-owner-closure/1345681594ee8d9bf34233877f7e73bc2745c9b8`](../../artifacts/verification/o9-c7-owner-closure/1345681594ee8d9bf34233877f7e73bc2745c9b8/README.md).

## Final Closure Recommendation

**Approve owner closure.** The fresh source review found no realistic reachable
Order 9 runtime defect. Every confirmed documentation and evidence-boundary
error was corrected, the corrected diff was re-read, and the complete retained
gate is green. The remaining limitations are now explicit product boundaries:
save validation is structural rather than provenance proof, registry/content
changes may require host-owned migration or rejection, the real Godot sample
does not yet persist field state, and concrete hosts may impose stricter
navigation/dungeon pairing than Framework.

This recommendation does not itself exercise the owner's closure authority.
`dungeon_traversal` remains `implemented`, Order 9 remains `open`, and all
three audience entries remain `existing_unreviewed` until the project owner
explicitly confirms closure.
