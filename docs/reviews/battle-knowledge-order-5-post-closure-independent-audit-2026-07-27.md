# Battle Knowledge Order 5 Post-Closure Independent Audit

**Date:** 27 July 2026

**Revision reviewed:** `b5c4cdb4ba809de3178e0016e738b8e1a7a07b8a`

**Verdict:** reopened; three medium findings and one low finding remain

## Purpose

This audit re-read the current Battle Knowledge implementation, its focused
tests, host integration, public API baseline, and all three active audience
documents. Earlier reviews and summaries were not used as evidence for the
technical conclusions below. They were consulted only after the source review
to identify active status statements that now need correction.

The review standard is deliberately practical. A finding must identify an
intended invariant, a reachable supported path, a concrete consequence, and
source or executable evidence. Speculative misuse of trusted extension points
is not presented as a vulnerability.

## Scope Read From Source

The audit traced:

- persistent knowledge snapshots, transitions, and query views;
- encounter-local observations, analysis, and combined knowledge queries;
- effect-execution evidence and aggregate provenance preflight;
- automated team knowledge lifetime and sharing;
- familiar acquisition and Compendium imports;
- save validation and aggregate restoration;
- Training Annex player and enemy integration;
- the exported API baseline; and
- mechanics, developer, and technical Battle Knowledge documents.

## Findings

### O5-F2 - Medium: actor-local Analyze state is a public competing authority

**Invariant:** current-target analysis belongs to the canonical
`RuntimeEncounterKnowledgeSnapshot`; persistent defense knowledge belongs to
`RuntimeKnowledgeSnapshot`. Public runtime state should not expose a third
analysis authority that canonical execution neither writes nor restores.

**Reachable path:** `RuntimeActorState` publicly exposes `Reveal(...)` and
`GetAnalysis(...)`. A host can reasonably call `Reveal` after an Analyze
action, then serialize the actor through `ToSnapshot()`.

**Evidence:**

- [`BattleRuntimeState.cs`](../../src/Convergence.Framework/Execution/BattleRuntimeState.cs)
  exposes `GetAnalysis` at line 428 and `Reveal` at line 1008.
- The actor snapshot captures that dictionary at line 1464.
- [`EffectExecutors.cs`](../../src/Convergence.Framework/Execution/EffectExecutors.cs)
  lines 1025-1036 returns a typed `BattleAnalysisResult` but deliberately does
  not call `Reveal`.
- [`ActiveSkillExecutionTests.cs`](../../tests/Convergence.Framework.Tests/SkillSystem/ActiveSkillExecutionTests.cs)
  lines 2519-2540 explicitly proves canonical Analyze leaves actor-local
  analysis empty.
- [`RuntimePersistenceSnapshots.cs`](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs)
  lines 840-846 rejects every actor snapshot containing actor-local analysis
  with `ActorEncounterAnalysisCannotPersist`.

**Consequence:** a third-party host can use an apparently supported public API,
observe correct in-memory results, and then make every session save invalid.
The state is also invisible to the canonical `BattleKnowledgeView`, so UI,
strategy, and persistence can disagree.

**Required correction:** remove or internalize actor-local analysis state and
its public mutation/query APIs before `0.1.0`, then remove the obsolete actor
snapshot field and validation branch. If a future feature needs actor-owned
analysis, it must be integrated into the canonical encounter authority rather
than added as a parallel store.

### O5-F3 - Medium: three exported mutable stores form a disconnected authority

**Invariant:** public discovery writes must produce the immutable
`RuntimeKnowledgeSnapshot` consumed by `BattleKnowledgeView`, persistence,
familiar imports, and execution transitions.

**Reachable path:** a framework consumer can instantiate the exported
`ElementalAffinityKnowledge`, `AilmentResistanceKnowledge`, or
`InstantDeathResistanceKnowledge` classes and call their documented `Learn`
methods.

**Evidence:**

- `src/Convergence.Framework/Knowledge/CombatKnowledgeStores.cs` at reviewed
  revision `b5c4cdb4` lines 7-149 exported three mutable stores and their key
  types. O5-R16 subsequently retired that source file.
- Those types remain part of
  [`PublicAPI.Shipped.txt`](../../src/Convergence.Framework/PublicAPI.Shipped.txt).
- Product-source searches find no adapter from these dictionaries into
  `RuntimeKnowledgeSnapshot`; only isolated unit tests construct them.
- [`battle-knowledge-runtime.md`](../technical/battle-knowledge-runtime.md)
  correctly identifies `RuntimeKnowledgeSnapshot` as the sole durable
  authority and does not include these stores in its integration model.

**Consequence:** a developer can record a discovery through a public type and
find that canonical AI/UI queries do not see it, save/restore does not retain
it, and acquisition imports do not update it. Two features can therefore show
different knowledge for the same entity without any diagnostic.

**Required correction:** remove/internalize the stores and key types before
`0.1.0`. If mutable builders are useful later, expose one explicitly named
builder whose only output is a validated canonical snapshot.

### O5-F4 - Medium: clone-mutated enum values escape the persistent transition boundary

**Invariant:** malformed host-supplied knowledge snapshots must be rejected by
the public knowledge boundary with stable diagnostics; they must not be
accepted by a view or escape as an unrelated constructor exception.

**Reachable path:** the public snapshot entries are records with public `init`
properties. C# record cloning can replace a validated enum after construction:
`valid with { Element = (DamageElement)999 }`.

**Evidence:**

- [`RuntimePersistenceSnapshots.cs`](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs)
  lines 214-242 validates constructor inputs but leaves clone-writable `init`
  properties.
- [`PersistentBattleKnowledge.cs`](../../src/Convergence.Framework/Knowledge/PersistentBattleKnowledge.cs)
  lines 344-423 validates IDs and duplicate keys, but not `DamageElement`,
  `ElementalAffinity`, `ResistanceLevel`, `InstantDeathChannel`, or analyzed
  defense-field enums.
- `PersistentBattleKnowledgeView` relies on that incomplete validation at
  lines 118-142 and accepts malformed enum keys/values.
- An `Apply` call reaches reconstruction at lines 436-463 and throws
  `ArgumentOutOfRangeException` while constructing the result instead of
  returning a typed rejected transition.

**Reproduction:** construct one valid elemental entry, clone it with an
undefined `DamageElement`, place it in `RuntimeKnowledgeSnapshot`, then pass it
to `PersistentBattleKnowledgeView` and
`PersistentBattleKnowledgeTransitionService.Apply`. The view accepts it; the
transition throws during result reconstruction.

**Consequence:** a host-created or custom-deserialized snapshot can cross one
knowledge boundary and later crash another. Calling the aggregate save
validator first happens to catch the value, but the standalone public
transition and view contracts must enforce their own stated boundary.

**Required correction:** add typed invalid-domain diagnostics for every enum in
`ValidateSnapshot`, including analyzed defense fields. The transition must
return `Rejected` with unchanged snapshots, while the read-only view should
fail immediately with a clear `ArgumentException`. Add clone-bypass regression
tests for every enum-bearing entry.

### O5-F5 - Low: instant-defeat evidence permits incomplete checked-resistance tuples

**Invariant:** instant-defeat evidence must represent exactly one coherent
case: either resistance was bypassed and all resistance fields are absent, or
resistance was checked and channel, authored resistance, and effective
resistance are all present.

**Reachable path:** custom registered effects may construct public
`BattleKnowledgeObservation.InstantDeath(...)` evidence. Supplying only one or
two of the three checked-resistance fields currently succeeds.

**Evidence:**

- [`BattleKnowledgeObservations.cs`](../../src/Convergence.Framework/Knowledge/BattleKnowledgeObservations.cs)
  lines 219-277 exposes the constructor.
- The condition at line 245 rejects "all absent while checked," but accepts any
  partial non-empty tuple even though its diagnostic says a checked resistance
  must identify the channel and resistances.
- Canonical instant-defeat execution supplies a complete tuple, and the
  knowledge transition conservatively ignores incomplete evidence. Core
  shipped combat therefore does not learn a false fact.

**Consequence:** a supported custom effect can emit internally inconsistent
evidence that passes construction and is silently discarded. This is an
extension-contract defect, not a player-triggered crash or corrupted canonical
combat path.

**Required correction:** enforce the two valid tuple shapes at construction
and cover every partial combination with focused tests.

## Confirmed Correct Behavior

The fresh trace found the following Order 5 behavior sound and aligned with the
confirmed mechanics document:

- persistent player knowledge is keyed by entity definition;
- encounter knowledge is keyed by runtime target plus entity identity;
- temporary guard, shield, break, override, and passive influence never become
  persistent authored defense facts;
- misses teach nothing, while typed confirmed immunity can teach exact ailment
  or instant-defeat resistance;
- Analyze disclosure can hide HP, SP, skills, elemental affinities, ailment
  resistances, and instant-defeat resistances for bosses or special targets;
- ordinary enemy teams begin each encounter with fresh team-local knowledge
  unless the host explicitly supplies a seed;
- persistent player discoveries survive later encounters and save/restore;
- familiarity imports are optional, acquisition-triggered, and player-scoped;
- only persistent knowledge enters the session save; and
- aggregate execution integration preflights action, actor, effect, runtime
  target, and target entity provenance before committing either snapshot.

No High-severity gameplay-authority, security, cross-team leakage, or atomicity
defect was reproduced.

## Documentation Review

The mechanics document accurately describes the intended gameplay and remains
`reviewed`.

The developer and technical documents correctly describe the canonical
immutable authorities, but they previously presented the exported surface as
if no competing actor-local or mutable store existed. The technical failure
containment statement also overstated enum rejection at transition boundaries.
Those two audiences return to `existing_unreviewed` until O5-F2 through O5-F4
are corrected and the supported API is reconciled.

## Correction Roadmap

| Checkpoint | Work | Exit condition |
|---|---|---|
| O5-R15 | Retire actor-local Analyze state | Canonical Analyze and encounter snapshots are the only current-target analysis authority; actor saves cannot acquire obsolete analysis state. |
| O5-R16 | Retire disconnected mutable knowledge stores | No exported discovery API can write knowledge outside `RuntimeKnowledgeSnapshot` transitions. |
| O5-R17 | Close cloned-enum validation | Every enum-bearing persistent entry rejects malformed clones through typed transition diagnostics and a clear view boundary. |
| O5-R18 | Tighten instant-defeat evidence tuples | Only complete checked tuples or complete bypass tuples can be constructed. |
| O5-R19 | Reconcile API baseline and audience documentation | Public API, developer guide, technical reference, and matrix describe one knowledge authority. |
| O5-R20 | Fresh closure audit | Re-read corrected source and docs, run the complete release gate, and find no unresolved realistic reachable Order 5 defect. |

Order 5 must remain `partial` until O5-R20 passes. Each correction should be an
isolated green commit so a behavior change cannot be hidden inside API or
documentation cleanup.

## Verification Record

- Focused Framework Battle Knowledge tests: **215 passed**, 0 failed, 0 skipped.
- Focused DemoHost knowledge/Analyze tests: **8 passed**, 0 failed, 0 skipped.
- Full solution: **1,741 passed**, 0 failed, 0 skipped.
- Nonincremental solution build: succeeded with **0 compiler warnings** and 0
  errors. A later sandbox rerun emitted only `NU1900` because the restricted
  session could not reach NuGet's vulnerability endpoint; the approved network
  retry was unavailable due the desktop approval-usage limit.
- Formatting verification: passed.

The green suite proves the intended paths remain stable. It does not invalidate
the findings: O5-F2 and O5-F3 concern competing public authorities that current
integration tests intentionally do not use; O5-F4 uses record-clone input not
currently covered; O5-F5 concerns partial custom evidence tuples.

## Closure Decision

Order 5 is not ready for formal closure at this revision. Its gameplay model
and canonical integrated path are healthy, but the pre-release public surface
still exposes two competing knowledge authorities, one incomplete standalone
validation boundary, and one incoherent custom-evidence shape. These are
bounded and correctable; they do not require a Battle Knowledge redesign.
