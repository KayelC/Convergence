# Battle Knowledge Order 5 Owner-Closure Audit

**Date:** 30 July 2026  
**Revision reviewed:** `76dcba4b` (`docs: close battle knowledge order 5`)  
**Disposition:** No unresolved realistic reachable finding; Order 5 is ready for owner closure

## Review Method

This was a fresh source-first review. Earlier Order 5 reports were not used as
implementation evidence. The review reconstructed the capability from current
Framework source, followed each integration into current host and persistence
code, inspected the executable tests that attack those boundaries, and only
then compared the result with the three active audience documents.

A reportable defect required all of the following:

1. an intended invariant established by source contracts or confirmed active
   mechanics;
2. a realistic path through a supported public or host integration surface;
3. a concrete incorrect mutation, disclosure, crash, or persistence result;
4. reproducible source or executable-test evidence.

Impossible record states requiring reflection, a host deliberately constructing
its own invalid result object, and alternative product designs were not promoted
to vulnerabilities.

## Findings

No high-, medium-, or low-severity Order 5 defect was found.

One suspected malformed-snapshot crash was investigated and rejected as a
false lead. `RuntimeKnowledgeSnapshot` uses the shared persistence collection
copier, which rejects null entries before persistent transitions, familiar
imports, or save validation can dereference them.

## Source Findings By Invariant

### 1. Persistent Player Knowledge Has One Authority

`RuntimeKnowledgeSnapshot` is the durable state shape. It separates elemental,
ailment, instant-defeat, and analyzed-defense records and defensively copies all
collections. `PersistentBattleKnowledgeTransitionService` validates both the
current and discovery snapshots before dictionary construction, applies typed
keys, and returns the original snapshot unchanged on rejection.

`PersistentBattleKnowledgeView` treats absent entries as unknown unless an
analyzed-defense marker proves the complete sparse profile. It does not store
the intrinsic `Almighty = Normal` rule as mutable knowledge.

Reviewed source:

- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs`
- `src/Convergence.Framework/Runtime/RuntimeKnowledgeIntegrity.cs`
- `src/Convergence.Framework/Knowledge/PersistentBattleKnowledge.cs`

### 2. Encounter Knowledge Is Profile-Exact And Temporary

Encounter facts are keyed by runtime target plus exact
`RuntimeCombatProfileIdentitySnapshot`. Snapshot construction rejects duplicate
typed keys and contradictory profile identities. A profile source or revision
change makes stale facts unreadable, and the profile transition removes every
elemental, ailment, instant-defeat, and Analyze entry for that target together.

Persistent fallback uses the active profile's source entity, so a Vessel does
not inherit knowledge from a previously selected Hosted Entity merely because
the battlefield runtime actor is the same.

Reviewed source:

- `src/Convergence.Framework/Knowledge/EncounterBattleKnowledge.cs`
- `src/Convergence.Framework/Runtime/RuntimeStateSnapshots.cs`
- `src/Convergence.Framework/Execution/BattleRuntimeState.cs`

### 3. Knowledge Comes From Typed Execution Evidence

Damage emits authored and effective affinity, contact state, and temporary
defense influences. A total miss teaches nothing. Ailment evidence exposes an
exact tier only for a coherent typed immunity result. Instant-defeat evidence
requires either a complete checked-resistance tuple or an explicit bypass tuple;
only confirmed checked immunity is learned.

The aggregate execution transition preflights source action, actor, enclosing
effect index, runtime target, and exact target profile for every observation and
Analyze result before applying any lower transition. A later rejection restores
both knowledge scopes to their original immutable snapshots.

Reviewed source:

- `src/Convergence.Framework/Execution/EffectExecutors.cs`
- `src/Convergence.Framework/Execution/ExecutionContracts.cs`
- `src/Convergence.Framework/Knowledge/BattleKnowledgeObservations.cs`
- `src/Convergence.Framework/Knowledge/BattleKnowledgeExecutionTransitions.cs`

### 4. Analyze Disclosure Is Policy-Controlled

`BattleAnalysisService` expands typed layers, requires one valid disclosure
decision per requested field, captures data only for `Disclosed` fields, and
reserves `Unavailable` for a resource that genuinely does not exist. Unknown
fields enter neither knowledge scope.

The supplied restricted policy is deliberately not an automatic boss detector.
Encounter composition selects a standard, restricted, or game-specific policy.
This matches the active documentation and preserves host neutrality: no display
name, description, or hard-coded boss ID controls disclosure.

Reviewed source:

- `src/Convergence.Framework/Knowledge/BattleAnalysis.cs`
- `src/Convergence.Framework/Execution/EffectExecutors.cs`

### 5. AI Learning Is Encounter-Local By Default

`AutomatedBattleRunner` creates an empty immutable knowledge snapshot per team
unless the host explicitly supplies a validated seed. Teammates share only that
team's discoveries during the run. The supplied deterministic selector uses
stable elemental facts, avoids known blocking affinities, and ignores facts
marked with temporary influences.

The runner uses an empty persistent snapshot and `EncounterOnly` transitions,
so ordinary AI learning cannot enter the player's save. Explicit seeds must
belong to participating teams and match live target profiles.

Reviewed source:

- `src/Convergence.Framework/Encounters/AutomatedBattleRunner.cs`
- `src/Convergence.Framework/Encounters/AutomatedBattleTurnRestrictionResolver.cs`

### 6. Familiarity Import Is Optional And Canonical

`FamiliarEntityKnowledgeService` validates the entire current snapshot before
policy evaluation, asks a typed policy which defense domains to import, and
routes discoveries through the canonical persistent transition. Acquisition,
Compendium registration, registered-entry synchronization, and explicit request
remain distinct sources.

The supplied standard policy imports all authored defense domains; the supplied
disabled policy imports none. Partial multi-entity imports are explicit: valid
entries may appear in `After` alongside diagnostics, and the host decides whether
to commit that partial result. The active developer guide describes both atomic
and partial host choices.

Reviewed source:

- `src/Convergence.Framework/Fusion/FamiliarKnowledgeImportPolicies.cs`
- `src/Convergence.Framework/Fusion/CompendiumRuntimeServices.cs`
- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexAcquisitionRegistrar.cs`
- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexCompendiumController.cs`

### 7. Save And Restore Preserve Only Durable Knowledge

Save contract v15 stores `RuntimeKnowledgeSnapshot`, validates typed-key
duplicates, identifiers, enum domains, catalog references, analyzed fields, and
the non-storable intrinsic element rule. Actor snapshots contain the active
combat-profile source and revision but no competing actor-local Analyze store.
Encounter knowledge is absent from the session save aggregate.

Training Annex restores the validated durable snapshot and starts each new
battle with fresh encounter state. The host JSON layer does not bypass Framework
validation.

Reviewed source:

- `src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs`
- `src/Convergence.Framework/Runtime/RuntimeSessionRestoration.cs`
- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexPersistenceController.cs`
- `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs`

## Documentation Cross-Examination

The following active documents agree with the source:

- `docs/mechanics/battle-knowledge.md`
- `docs/developer-guide/battle-knowledge.md`
- `docs/technical/battle-knowledge-runtime.md`

Specifically, they correctly describe:

- persistent source-entity knowledge versus target/profile encounter knowledge;
- temporary-defense containment;
- miss, ailment, and instant-defeat discovery limits;
- policy-selected boss and special-target Analyze restrictions;
- fresh ordinary AI knowledge with explicit optional seeds;
- optional familiarity import and partial-batch semantics;
- save v15 ownership and encounter-state disposal; and
- Godot/host presentation responsibilities.

The capability and documentation matrices remain accurate: `battle_knowledge`
is `complete`, host-neutral, optional, covered end to end, and has no known gap.

## Verification Evidence

| Gate | Result |
|---|---|
| Focused Framework knowledge/catalog/persistence tests | 233 passed, 0 failed, 0 skipped |
| Focused DemoHost knowledge/Analyze/Compendium tests | 9 passed, 0 failed, 0 skipped |
| Documentation and architecture tests | 60 passed, 0 failed, 0 skipped |
| Full solution tests | 1,762 passed: 1,578 Framework, 177 DemoHost, 7 ContentValidator; 0 failed, 0 skipped |
| Strict Release solution build | 0 warnings, 0 errors |
| Formatting | `dotnet format --verify-no-changes` passed |
| Framework coverage, exact Release configuration | 90.76% lines, 76.49% branches; 90%/70% gate passed |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions passed |
| DemoHost smoke | Four noninteractive modes and scripted Training Annex exit passed |
| Godot 4.7.1 headless smoke | Passed after redirecting engine user-data paths into the writable repository artifacts directory |
| Repository hygiene before report edit | clean, synchronized `main`; `git diff --check` passed |

The first local Godot launch failed before project load because the engine could
not create `user://logs` under the sandboxed Windows profile. Redirecting
`APPDATA` and `LOCALAPPDATA` to writable artifact directories produced the full
`CONVERGENCE_GODOT_SMOKE_OK` result. Godot also printed a nonfatal Windows root
certificate-store warning after the successful smoke. Neither event traversed
Order 5 code.

## Residual Risk And Scope

- A host must actually retain the player's persistent snapshot and discard
  ordinary encounter snapshots. The Framework provides the contracts and
  transitions but does not own a game's save slots or scene lifetime.
- A host must explicitly select restricted Analyze policy for a boss or special
  encounter. Automatic boss inference would contradict the approved host-neutral
  design.
- Registered custom effect handlers and custom policies are trusted extension
  code. Their returned evidence is validated before knowledge mutation, but the
  Framework cannot make arbitrary external side effects transactional.
- Online dependency auditing remains a connected CI/release concern and was not
  required to evaluate Order 5 gameplay authority.

These are integration responsibilities or extension trust boundaries, not
unresolved Order 5 implementation defects.

## Closure Decision

Order 5 may be formally closed. The current code implements the confirmed
Battle Knowledge design, the active documentation describes that implementation,
and the complete local verification gate passes. Order 6,
`encounter_orchestration`, may proceed when the owner chooses.
