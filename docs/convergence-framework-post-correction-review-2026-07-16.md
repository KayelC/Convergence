# Convergence Framework Post-Correction Code Health Review

**Review date:** 16 July 2026
**Reviewed branch:** `main`
**Reviewed through:** `77f81b1` before this report commit
**Product target:** guarded `0.1.0` pre-release on .NET 8

## Verdict

No unresolved High, Medium, or Low correctness defect was found in the active
product after the corrections recorded below. The reviewed Framework is in a
healthy state for continued production development and real Godot integration.

This is not a claim that the code can never contain another bug. It means the
review found no remaining reachable path that violates an intended invariant
with a concrete product consequence. Deferred save migration, deterministic
replay, richer Godot interaction, and broader documentation are explicit future
work rather than hidden release blockers.

## Review Method

This pass did not treat earlier reports as evidence of current behavior. It
reviewed the active source and executable gates directly:

- all 92 Framework C# files across the 15 tested source owners;
- public API and host-neutrality boundaries;
- content deserialization, qualification, validation, and catalog loading;
- runtime actor construction, composition, progression, resources, and restore;
- targeting, assessment, ordered effects, skills, items, and transactions;
- encounter cancellation, lifecycle ordering, turn economy, and event payloads;
- status, passive, ailment, knowledge, party, and roster state;
- inventory, equipment, economy, shop, and hospital arithmetic;
- fusion planning, catalyst rank shifting, inheritance, transaction, and Compendium state;
- navigation, dungeon traversal, save validation, and aggregate restoration;
- DemoHost path confinement, Godot project wiring, CI, coverage, trimming, and formatting.

Static searches were used to locate broad exception boundaries, synchronous
waits, unfinished markers, unchecked arithmetic, mutable collection surfaces,
forbidden dependencies, and host-specific types. Candidate concerns were kept
only when a reachable caller and concrete consequence could be demonstrated.

## Findings

There are no unresolved findings. Four concrete issues were discovered during
this fresh review and corrected in isolated commits before the final gate.

### F1. `BattleEnded` was not always the terminal encounter event

**Severity before correction:** Medium
**Commit:** `1947812 battle: keep encounter end event terminal`

The encounter runner committed battle-end lifecycle cleanup, published
`BattleEnded`, and only then appended the lifecycle events. The canonical status
lifecycle can return cleanup events, so a Godot host that transitions scenes on
`BattleEnded` could receive status/resource events after it had already treated
the encounter as finished.

Both successful and fault finalization now sequence all cleanup events before
the single terminal `BattleEnded` event. Regression tests exercise both paths
and require the terminal event to carry the final sequence number.

### F2. Actor initialization exposed a castable mutable dictionary

**Severity before correction:** Low
**Commit:** `d668767 runtime: seal actor initialization snapshots`

`BattleActorInitialization.BaseResourceValues` was typed as an
`IReadOnlyDictionary` but backed directly by `Dictionary`. A host policy could
cast and mutate the result after construction, undermining the immutable
initialization boundary used by catalog actor creation.

The property now uses the standard read-only snapshot helper. Tests prove both
detachment from the source dictionary and rejection of cast-based mutation.

### F3. Prepared non-skill actions did not revalidate target eligibility

**Severity before correction:** Medium
**Commit:** `2708e7e battle: revalidate prepared action targets`

The public action API permits a host to assess an action, present the decision,
and execute it later. Skills already revalidated prepared targets, but basic
attacks and items only checked that their target IDs still existed. A target
could become defeated, inactive, or otherwise ineligible between assessment and
execution and still receive the prepared action.

The shared runtime validator now rechecks activity, relation, self-targeting,
life state, selection shape, and target count without invoking random selection
again. Stale random targets are rejected rather than silently replaced. Items
also reject a prepared use that no longer has a meaningful effect, and the
battle action facade rejects stale items before inventory reservation.

### F4. Implemented schema contracts were mislabeled as partial

**Severity before correction:** Low project-ownership issue
**Commit:** `77f81b1 docs: complete implemented schema capability`

The capability matrix called authored schema contracts partial because possible
future content families do not yet have schemas. The strict v3 set already
covers every document family currently implemented and manifest coverage is
independently tested. Hypothetical future scope does not make an implemented
contract incomplete under the matrix's own definition.

The matrix now records 25 capabilities: 23 complete, zero partial, and two
explicitly deferred.

## External Review Corrections

The five accepted findings in
[`Convergence_Current_Version_Code_Review.md`](Convergence_Current_Version_Code_Review.md)
are verified:

1. GodotHost solution Release configuration emits its Release assembly.
2. Canonical damage resolution returns and consumes effective affinity exactly once.
3. Framework lifecycle-event payload mapping has one internal authority.
4. Capability totals are derived from the executable matrix and agree across active docs.
5. Repository instructions and tested source ownership now preserve API and module boundaries.

## Code Health

### Strong areas

- Framework remains host-neutral. No active Framework source references console,
  filesystem, Godot, Newtonsoft, archived runtime types, or host adapters.
- Mutating execution is staged through actor transactions; rejected skill, item,
  custom-effect, lifecycle, fusion, and restore paths retain their before-state.
- Random target selection is explicit, prepared once, single-use, and no longer
  rerolled or accepted after becoming ineligible.
- Encounter ports have typed fault containment, cancellation checkpoints,
  bounded phase progress, detached result snapshots, and a terminal end event.
- Save validation checks actor integrity, global runtime identity, party/roster
  role rules and capacities, catalog references, timed state, knowledge
  uniqueness, inventory, Compendium, and dungeon/navigation references.
- Fusion recipes identify catalyst and target roles explicitly, validate catalog
  race/rank truth, reject absent exact ranks, validate inheritance before preview,
  and commit prepared results without mutating inputs.
- Public arithmetic paths use checked, saturating, or typed-rejection behavior in
  combat, progression, negotiation, rewards, prices, resources, and fusion.
- Content has independent schema, deserialization, semantic, dependency,
  registration, and catalog gates.

### Maintainability signals

Several source files are large, and the public API is intentionally broad because
the framework exposes immutable definitions, requests, results, diagnostics, and
extension ports. These are review signals, not demonstrated defects. Splitting a
file or internalizing a type should be driven by real ownership boundaries and
Godot consumer experience rather than line counts alone.

XML documentation remains curated instead of exhaustive. Exact compatibility is
guarded by `PublicAPI.Shipped.txt`, source ownership by the tested inventory, and
consumer guidance by concept documents. Expanding the technical, developer, and
mechanics documentation remains worthwhile before a stable `1.0` release.

## Verification Results

- locked dependency restore and NuGet vulnerability audit: passed;
- strict nonincremental Release solution build: 0 warnings, 0 errors;
- tests: 955 passed, 0 failed, 0 skipped;
  - Framework: 785;
  - DemoHost: 163;
  - ContentValidator: 7;
- formatting: no changes required;
- Framework coverage: 91.31% lines, 75.31% branches;
- trimming analysis: 0 warnings, 0 errors;
- active content: 6 packs, 36 documents, 94 qualified definitions;
- schema, deserialization, semantic, dependency, registration, and catalog checks: passed;
- noninteractive battle, field, save, and Training Annex demos: exited successfully;
- scripted Training Annex interaction: covered by the DemoHost test suite and CI gate;
- GodotHost: built from the solution in Release configuration;
- Framework forbidden-reference search: clean;
- `git diff --check`: clean.

A Godot executable was not installed on this review machine, so the real engine
headless smoke was not repeated locally. The checked-in CI gate downloads the
pinned official Godot 4.7.1 .NET build, verifies its SHA-256, and runs that smoke
against the sample project. This is the one local-environment verification gap,
not an observed Framework failure.

## Residual Product Constraints

These are explicit boundaries, not code-review findings:

- `Convergence.GodotHost` is an integration proof with pre-submitted commands,
  not yet a playable asynchronous UI sample.
- The Godot save codec demonstrates host-owned serialization; it is not a full
  save-slot, backup, cloud, or corruption-recovery product.
- Save migration is deferred until a released save version actually needs a
  migration path.
- Checkpoint breadcrumbs are diagnostics, not deterministic replay authority.
- The synchronous encounter wrapper is compatibility-only; Godot and UI hosts
  must await `RunAsync`.
- Convergence is ready to continue production at `0.1.0`, but real consumer use
  should inform API reduction and documentation before declaring stable `1.0`.

## Recommendation

Move forward with production. The highest-value next work is not another broad
speculative vulnerability sweep. It is a small playable Godot vertical slice
that exercises pending command input, scene cancellation, event-driven
presentation, aggregate restoration, and original content. Any defect exposed by
that consumer should be corrected at the Framework boundary with the same
invariant/reachable-path/consequence/evidence standard used in this review.
