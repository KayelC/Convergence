# Convergence Framework Whole-Codebase Review

Date: 2026-07-16

Reviewed commit: `c93842bb6f331ab1b7c8465c7ddc11ca064746d5`

Branch: `main`

## Post-Review Correction Status

Both findings were corrected and committed independently on 2026-07-16:

- M1: `1576f98` (`fix: reject malformed persistence snapshot entries`)
- M2: `2b9fb3f` (`fix: contain ruleset policy factory failures`)

The post-correction gate passed 940 tests with zero failures or skips, a strict
nonincremental build with zero warnings, formatting verification, the save demo, and the
Training Annex end-to-end demo. The findings below remain as the source evidence and
rationale for those corrections.

## Review Decision

The active Convergence product is structurally healthy and is suitable for continued
production development after two medium reliability defects are corrected. I found no
credible high-severity defect, gameplay-authority split, cross-project dependency leak,
or transaction bug that should force a rollback of the current architecture.

This is not a claim that the framework is finished. It means the implemented product
surface is coherent: content is validated before catalog construction, mutable actor
operations generally stage before commit, battle orchestration has typed fault and
liveness boundaries, save restoration is aggregate and dependency ordered, and active
hosts remain outside Framework.

The two findings are boundary failures. Neither changes combat rules or corrupts state,
but both can turn recoverable host/configuration errors into unhandled exceptions.

## Findings

### M1. Malformed save collection entries escape the typed validation boundary

**Status:** Corrected by `1576f98`.

**Invariant**

`IRuntimeSaveValidator.Validate` is the framework boundary that should turn malformed or
incompatible save snapshots into stable `RuntimeSaveValidationDiagnostic` records.

**Reachable path**

`RuntimeSaveGameSnapshot` snapshots `contentPacks`, `actors`, checkpoints, and other
collections without rejecting null elements. `ContentPackIdentity` also accepts a null
`Id` at runtime because its positional record has no guard. A host-owned deserializer can
therefore construct a snapshot containing a null collection entry even though nullable
annotations warn ordinary C# callers not to do so.

The validator then dereferences the entry before producing a diagnostic:

- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:282`
- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:342`
- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:669`
- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs:1447`
- `src/Convergence.Framework/Content/ContentDeserializationContracts.cs:26`

**Consequence**

A damaged or manually edited save can throw `NullReferenceException` or
`ArgumentNullException` instead of returning an invalid-save result. No live state is
committed, but a host that relies on the framework validator for graceful load rejection
can crash or fall out of its normal error flow.

**Reproduction**

A temporary test constructed a normal valid snapshot, replaced `ContentPacks` with
`new ContentPackIdentity[] { null! }`, and called `RuntimeSaveValidator.Validate`. The
call produced `NullReferenceException`. The reproduction passed as evidence and was then
removed; no temporary source remains in the worktree.

**Recommended correction**

Choose one consistent contract and test it across every save collection:

1. Prefer rejecting null elements in snapshot constructors with explicit argument
   exceptions, while host codecs convert those exceptions into load diagnostics; or
2. Add null-entry validation codes and make `RuntimeSaveValidator` tolerate malformed
   object graphs without dereferencing them.

For a persistence validation API, option 2 provides the stronger host experience. At a
minimum, `ContentPackIdentity` should validate a nonblank pack ID and every persistence
collection should reject or diagnose null members deterministically.

### M2. Exceptions from host-registered ruleset factories bypass typed binding results

**Status:** Corrected by `2b9fb3f`.

**Invariant**

`RuntimeRulesetBindingResolver` exposes a typed, diagnostic-based composition boundary
for host-supplied policy factories. The public diagnostic vocabulary includes
`RulesetBindingDiagnosticCode.PolicyFactoryFailure`.

**Reachable path**

After validating the ruleset and locating the registered factory, the resolver invokes
the host factory directly:

- `src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs:254`
- `src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs:266`
- `src/Convergence.Framework/Runtime/RuntimeRulesetBindings.cs:270`

The resolver handles a null/malformed returned result, but it does not catch an exception
thrown by `factory.Create`. This applies to every registered category because all binding
methods share the same generic `Bind` method.

**Consequence**

A custom policy factory that rejects an authored parameter by throwing, or contains an
ordinary implementation error, terminates ruleset binding with an exception. Hosts lose
the stable diagnostic path that standard factories and unsupported-policy cases provide.
This is especially relevant during game startup, where one custom ruleset can prevent a
host from presenting useful content diagnostics.

**Reproduction**

A temporary `IRuntimeDamageRulesetPolicyFactory` throwing
`InvalidOperationException` was registered against a valid damage ruleset. Calling
`BindProductionCombatRuleset` propagated that exact exception instead of returning a
`PolicyFactoryFailure` diagnostic. The temporary test was removed after confirmation.

**Recommended correction**

Wrap the host factory invocation in the generic resolver. Preserve cancellation where a
future async factory boundary exists, and convert other exceptions into one
`PolicyFactoryFailure` diagnostic containing the ruleset ID, category, policy ID, and a
safe failure message. Add one parameterized regression covering all factory categories or
one shared-path test proving the generic boundary.

## Source Review

### Product and dependency boundary

- `Convergence.Framework` targets .NET 8, has no project dependency, is non-packable, and
  has no runtime package dependency. Compiler, API, and trimming packages are private
  development dependencies.
- DemoHost, GodotHost, and ContentValidator depend inward on Framework. Framework does
  not depend outward on a host or tool.
- The active solution excludes `ArchiveDocs`. Archived projects still contain old
  dependencies by design, but they do not enter the active build graph.
- Public API analyzers, XML documentation generation, trimming analysis, namespace
  boundary tests, and terminology guards are active.

### Content, schemas, and catalogs

- Content loading is text-source based and does not require Framework filesystem access.
- Manifest document paths are canonicalized and checked for traversal, duplicates,
  missing documents, unexpected documents, type mismatches, dependency visibility, and
  cross-pack references.
- DTO deserialization is strict and backed by source-generated System.Text.Json metadata.
- Draft 2020-12 schema v3 and semantic validation are separate, complementary gates.
- Catalog definitions snapshot nested collections and custom parameters, including cycle
  and depth protection for parameter graphs.
- No display-name or description inference was found in the clean execution path.

### Actor state, snapshots, and restoration

- Runtime actors have one mutable state authority. Snapshot construction and restoration
  preserve resources, progression, equipment, rosters, defenses, statuses, passives, and
  identity through typed records.
- Actor numeric domains, timed state, enum values, roster references, duplicate IDs,
  knowledge keys, inventory/equipment references, and catalog references are validated.
- Aggregate restoration validates first, resolves Vessel/Hosted Entity dependencies in
  order, detects cycles/missing dependencies, and exposes no partially restored session.
- Stat composition and growth stage changes before committing to the live actor.
- Finding M1 is the remaining malformed-object-graph hole at this boundary.

### Effects and action execution

- Skills and items share typed targeting, conditions, ordered effects, and execution
  policies while retaining separate cost/consumption ownership.
- Assessments are executor- and request-bound, single use, and revalidate mutable
  affordability before commit.
- Random targets are resolved once and retained in the prepared assessment.
- Effect execution stages actor mutations and commits only after the ordered pipeline
  completes. Custom lifecycle failures similarly operate against staged actors.
- Inventory reservations distinguish reserve, commit, and rollback, and item consumption
  remains host-owned through a narrow port.
- Record-cloning of `BattleActionAssessment.TurnConsumption` does not bypass execution:
  concrete execution paths derive authoritative consumption from the command/effects,
  rather than trusting that presentation property.

### Battle, lifecycle, and turn economy

- Encounter requests reject duplicate runtime IDs and invalid initiative permutations.
- Port exceptions are converted to typed encounter faults; cancellation remains distinct
  and battle-end lifecycle receives a bounded finalization attempt.
- Phase command and consecutive-free-action limits prevent non-progressing handlers from
  hanging the encounter.
- Lifecycle work covers battle start/end, owner turn end, timed durations, restrictions,
  reserve suspension, cleanup scopes, and transactional custom handlers.
- Automated battles use the canonical lifecycle and bound turn-economy services, including
  non-skip restricted actions through the injected resolver.
- Action Token passing correctly consumes a partial token first. A full token converts to
  partial only when no partial token exists.
- Encounter results contain participant snapshots rather than live mutable actors.

### Progression, inventory, economy, and facilities

- Vessel stat composition, missing-hosted-entity behavior, resource recalculation, growth,
  allocation, and rollback are explicit policies with checked/saturating arithmetic at
  exposed numeric boundaries.
- Inventory, equipment, wallet, shop, and recovery-facility operations return immutable
  before/after snapshots and avoid mutation on rejection.
- Shop price, reward, negotiation, resource, and experience aggregates contain overflow
  handling. No reachable wraparound path was found in the reviewed standard policies.
- Equipment remains intentionally partial where documented; that is product scope, not a
  hidden implementation claim.

### Party rosters, fusion, acquisition, and Compendium

- Runtime IDs are checked globally across party, Hosted Entity, and Companion roles.
- Roster capacity, overlap, deployment, recall, replacement, and ownership invariants are
  centralized rather than inferred by hosts.
- Fusion recipes use authored catalyst/target roles. Rank shifting follows exact catalog
  race ranks, rejects missing destinations, and does not clamp.
- Parent matching, strategy specificity, ambiguity handling, inheritance planning,
  selection validation, preview, and transaction preparation share typed authority.
- Compendium acquisition adds a first record without overwriting an existing record.
  Recall validates price, ownership, capacity, actor construction, and wallet state before
  returning applied snapshots.
- Familiar battle knowledge validates duplicate keys before dictionary construction.

### Hosts, tooling, and release engineering

- DemoHost owns console input/output, filesystem access, random sources, and host JSON.
  Its content source confines logical paths to the configured root and resolves documents
  relative to their manifest.
- GodotHost owns Nodes, `res://` access, command signals, scene-instance mapping, and its
  sample save representation. Framework remains free of Godot references.
- ContentValidator independently runs schema, deserialization, semantic, dependency,
  registration, and catalog checks.
- CI performs locked/audited restore, formatting, warning-as-error builds, API checks,
  architecture tests, all tests, coverage thresholds, content validation, DemoHost modes,
  a real Godot 4.7.1 headless smoke, and trimming analysis.
- `SECURITY.md` defines the supported pre-release scope and private reporting route.

## Verification Results

| Gate | Result |
|---|---|
| Full solution tests | 930 passed, 0 failed, 0 skipped |
| Framework tests | 760 passed |
| DemoHost tests | 163 passed |
| ContentValidator tests | 7 passed |
| Strict nonincremental solution build | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | Passed |
| Framework trimming analysis | 0 warnings, 0 errors |
| Framework coverage | 90.64% lines, 74.91% branches |
| Content validation | 6 packs, 36 documents, 94 definitions passed |
| Battle demo | Victory, exit 0 |
| Field demo | Completed, exit 0 |
| Save demo | Contract v7 restored and validated, exit 0 |
| Training Annex demo | Full clean slice completed, exit 0 |
| Scripted Training Annex play | Covered by passing DemoHost tests |
| `git diff --check` before report creation | Passed |

The Godot sample compiled successfully in the strict solution build. A local Godot engine
was not available on `PATH`, so this review did not rerun the real engine headless smoke.
The checked-in CI gate does run the pinned Godot 4.7.1 executable after SHA-256
verification.

## Residual Limits, Not Defects

- Convergence is `0.1.0` pre-release software. Its API baseline protects reviewed patch
  evolution, but it is not a `1.0` compatibility promise.
- Framework runtime state is designed for host-controlled sequencing. It does not claim
  that one mutable actor may be modified concurrently from multiple threads.
- Host save-file syntax and malformed-JSON presentation are host responsibilities. The
  framework owns snapshot shape, validation, migration seams, and restore behavior.
- GodotHost is a reference consumer and smoke proof, not a reusable UI framework or a
  complete game project.
- DemoHost is sample/reference software. Its large Training Annex flow is useful test
  coverage but should not become the architectural home of new framework rules.
- Full equipment semantics and other roadmap-deferred modules remain future product work.

## Completion

M1 now rejects null persistence collection members, dictionary members, and invalid pack
identities before an invalid snapshot graph can reach validation or restoration. M2 now
maps non-cancellation exceptions from every registered policy-factory category to
`PolicyFactoryFailure`; `OperationCanceledException` remains distinct.

The remaining release action from this review is to let the checked-in CI gate rerun its
real Godot smoke. No architectural rewrite is justified by the review or its corrections.

## Final Assessment

The codebase is in substantially better condition than a typical pre-release framework of
this breadth. Its strongest characteristic is that rule ownership is now visible in code:
typed definitions, policies, staged execution, immutable results, host ports, and catalog
validation align instead of competing with a hidden legacy runtime.

The two findings weakened advertised typed error handling but did not undermine the
framework's combat model, persistence architecture, host neutrality, or current clean
vertical slices. Both are now corrected in the commits recorded above.
