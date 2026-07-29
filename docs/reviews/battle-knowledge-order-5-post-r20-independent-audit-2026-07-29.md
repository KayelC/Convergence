# Battle Knowledge Order 5 Post-R20 Independent Audit

**Date:** 29 July 2026

**Starting revision:** `aa97cd87`

**Disposition:** Reopen Order 5; do not formally close yet

## Review Method

This review traced the current implementation from source before reading the
three audience documents. Earlier review conclusions and roadmap summaries were
not accepted as evidence. The trace covered:

- execution-produced elemental, ailment, instant-defeat, and Analyze evidence;
- aggregate provenance preflight and immutable transitions;
- persistent and encounter query behavior;
- automated-team selection and battle-lifetime state;
- familiar acquisition and Compendium imports;
- save validation and host-owned JSON restoration;
- Vessel combat-profile composition and Hosted Entity switching; and
- mechanics, developer, and technical Battle Knowledge guidance.

Findings below identify an intended invariant, a reachable path, a concrete
consequence, and source evidence. They are data-integrity defects, not claims of
a remote security exploit.

## Findings

### H1. Knowledge identity remains the Vessel while its observed combat profile belongs to a Hosted Entity

**Intended invariant:** persistent facts must be keyed to the authored entity
profile that supplied the observed defenses. Encounter disclosures must stop
applying when that target changes combat profile.

**Reachable path:** `RuntimeActorCombatProfileCompositionService` copies an
active Hosted Entity's stats, defenses, learned/equipped skills, and passives
onto a Vessel. The Vessel nevertheless continues exposing its own immutable
`RuntimeActorState.EntityId`. Damage, ailment, instant-defeat, and Analyze
evidence all use that owner entity ID, and encounter knowledge has no combat
profile source or revision.

An enemy Vessel can therefore use Hosted Entity A, reveal A's Ice weakness,
then switch to Hosted Entity B. The existing encounter entry and Analyze
disclosure flags still identify the Vessel and remain valid according to the
current snapshot. This produces four incorrect outcomes:

1. A's authored defense may be persisted under the Vessel definition rather
   than A's entity definition.
2. A later battle can present or select against that stale persistent fact when
   the Vessel starts with B.
3. Encounter AI can continue using A's facts until each affected domain is
   observed again.
4. A prior Analyze disclosure can expose B's newly composed skills or stats
   without a new Analyze, because disclosure is stored only by runtime target.

This also prevents familiar knowledge from converging with battle evidence:
Compendium familiarity records A under A's definition ID, while observing the
same profile through a Vessel records it under the Vessel ID.

**Source evidence:**

- `Execution/BattleRuntimeState.cs:345` exposes actor identity as the only
  entity ID on runtime state.
- `Runtime/RuntimeActorCombatProfileComposition.cs:396-400` copies the source
  actor's defenses and move list into the Vessel without retaining a public
  source-profile identity on the live actor.
- `Execution/EffectExecutors.cs:238-247`, `:594-603`, and `:702-711` stamp
  observations with `target.EntityId`.
- `Knowledge/BattleAnalysis.cs:261-266` and `:309-314` use the same actor entity
  ID for policy and result identity.
- `Knowledge/EncounterBattleKnowledge.cs:36-64` stores Analyze disclosure by
  runtime target and entity ID only; no source instance or profile revision is
  present.
- `Knowledge/BattleKnowledgeExecutionTransitions.cs:47-86` can validate only
  the actor-entity binding supplied in its authority map, so its provenance
  checks faithfully enforce the wrong semantic identity for a composed Vessel.

**Severity:** High. This is a supported cross-capability path that can corrupt
durable player knowledge, misdirect supplied AI, and reveal a newly selected
profile without the required discovery action.

### M1. Public snapshots accept impossible Almighty affinity knowledge

**Intended invariant:** Almighty always resolves as `Normal` and is never stored
as elemental affinity knowledge.

**Reachable path:** ordinary observation and familiarity transitions omit
Almighty, but the public encounter-entry and persistent snapshot types accept an
Almighty key with any affinity. A host can provide such an encounter seed, and a
host-owned or damaged save can provide such a persistent entry. Neither
`RuntimeSaveValidator` nor `PersistentBattleKnowledgeView` rejects it.

The supplied selector can then avoid or prefer an Almighty action based on a
fact that combat resolution can never honor. UI queries can also report an
impossible Weak, Resist, Null, Repel, or Absorb affinity.

**Source evidence:**

- `Knowledge/EncounterBattleKnowledge.cs:55-78` validates that the element and
  affinity enums are defined but does not reject `DamageElement.Almighty`.
- `Knowledge/PersistentBattleKnowledge.cs:147-160` returns a stored Almighty
  entry before applying known-Normal fallback semantics.
- `Knowledge/PersistentBattleKnowledge.cs:346-377` validates enum shape and
  duplicate keys but accepts Almighty in an existing snapshot.
- `Runtime/RuntimePersistenceSnapshots.cs:1558-1561` validates only the
  knowledge target for elemental entries.
- `Knowledge/PersistentBattleKnowledge.cs:270-275` proves the intended rule by
  ignoring new Almighty discoveries, but this does not protect pre-existing,
  seeded, cloned, or deserialized state.

**Severity:** Medium. The defect does not change damage resolution, but it lets
public host input and saved state violate a framework-owned invariant and alter
AI/presentation decisions.

## Verified Healthy Areas

The fresh trace found the following current behavior coherent:

- persistent player facts and encounter-local facts are separate immutable
  authorities;
- ordinary unseeded AI teams start fresh and never write player persistence;
- teammates share only their own team's encounter snapshot;
- execution evidence is preflighted against action, actor, effect index,
  runtime target, and the currently supplied entity binding before any lower
  transition runs;
- a later rejected effect returns both aggregate knowledge scopes to their
  original snapshots;
- misses and probabilistic ailment/instant-defeat failures do not infer hidden
  resistance tiers;
- temporary shields, Breaks, overrides, guarding, and passive changes cannot
  overwrite durable authored facts;
- boss Analyze restrictions are policy-controlled and cover HP, SP, skills,
  elemental, ailment, and instant-defeat fields; core-stat disclosure remains
  an explicit game decision;
- familiarity import is optional, player-only, catalog-authored, and routed
  through the canonical persistent transition; and
- save v14 contains persistent knowledge but excludes ordinary encounter
  knowledge and current-target Analyze state.

## Documentation Review

The three audience documents describe the approved lifetime, conservative
discovery, boss Analyze, familiarity, and save rules accurately. They are not
currently complete enough to remain `reviewed` because:

- all three treat actor entity identity as if it were always the authored
  combat-profile identity;
- the developer example builds execution authority from `participant.EntityId`,
  which is insufficient for a composed Vessel;
- the technical claim that one runtime ID cannot silently change entity meaning
  does not cover a profile source change under the same actor ID; and
- the mechanics statement that Almighty is not stored is not enforced at the
  public snapshot and restore boundaries.

Until H1 and M1 are corrected and documented, mechanics, developer, and
technical Battle Knowledge coverage return to `existing_unreviewed`.

## Missing Regression Coverage

Current tests cover ordinary discovery, temporary defenses, hostile provenance,
Analyze restrictions, team isolation, familiar imports, duplicate save keys,
and JSON round trips. They do not cover:

- a Vessel observed with Hosted Entity A and recomposed to Hosted Entity B;
- invalidation of previous current-field Analyze disclosure after profile
  replacement;
- durable facts being keyed to the Hosted Entity that supplied defenses; or
- Almighty entries entering through encounter seeds, persistent snapshots, and
  host-owned save JSON.

The green suite therefore confirms existing scenarios but does not disprove
these findings.

## Correction Roadmap

### O5-R21: Canonical combat-profile knowledge identity

- Add immutable runtime profile identity containing source runtime ID, source
  entity definition ID, and a revision/generation.
- Self-sourced actors use their own IDs. Hosted composition atomically replaces
  the profile identity together with stats, defenses, skills, and passives.
- Restore must either retain this identity or deterministically reconstruct it
  before the actor can execute or be observed.
- Rejection must leave both actor state and profile identity unchanged.

### O5-R22: Profile-aware evidence and encounter invalidation

- Key persistent facts by the profile source entity ID.
- Bind observations, Analyze results, execution authority, team seeds, and
  queries to the current profile identity.
- On a profile revision change, invalidate that target's prior encounter
  defenses and current-field disclosures before later selection or
  presentation.
- Cover different source entities and different instances/revisions of the same
  source entity.

### O5-R23: Forbid impossible Almighty knowledge

- Reject Almighty in public encounter knowledge entries and persistent current
  snapshots.
- Add stable transition and save diagnostics so record-cloned or deserialized
  invalid state cannot bypass constructor checks.
- Prove malformed automated seeds, direct views, transitions, and host-owned
  saves fail before strategy or presentation can consume them.

### O5-R24: Documentation reconciliation

- Update mechanics, developer, technical, API, save, and actor-composition
  guidance with profile identity, swap invalidation, and Almighty boundaries.
- Restore all three Battle Knowledge audience entries to `reviewed` only after
  source and tests agree.

### O5-R25: Independent closure review

- Re-read the corrected source without accepting this report as proof.
- Exercise the focused and complete release gates.
- Return `battle_knowledge` to `complete` only if no realistic reachable defect
  remains.

## Verification At This Revision

- Focused Framework knowledge/Analyze/Compendium/automated tests: **82 passed**,
  0 failed, 0 skipped.
- Focused DemoHost Training Annex/save tests: **124 passed**, 0 failed, 0 skipped.
- Full solution: **1,745 passed** (`1,563` Framework, `175` DemoHost, `7`
  ContentValidator), 0 failed, 0 skipped.
- Strict nonincremental solution build: **0 warnings, 0 errors**.
- `dotnet format --verify-no-changes`: passed.
- Framework coverage: **90.80% lines, 76.50% branches**; both release
  thresholds passed.
- Content validation: **6 packs, 36 documents, 98 qualified definitions**;
  schema, deserialization, semantic, dependency, registration, and catalog
  checks passed.
- Clean battle, field, save, and Training Annex noninteractive demos: passed
  with exit code `0`.
- The Godot sample compiled in the strict solution build. The local Godot
  4.7.1 Windows executable crashed inside the native engine with signal 11
  before loading the project, including when given a workspace log path. This
  is the already-reproducible local engine/runtime failure, not a passing smoke
  result; the managed Godot contract tests remain part of the green suite and
  connected CI remains authoritative for the real engine smoke.

These green gates establish baseline health. They do not close H1 or M1 because
the required profile-switch and impossible-Almighty cases are not represented
in the current suite.

## Closure Decision

Order 5 should **not** be formally closed at revision `aa97cd87`. The core is
well-factored and substantially correct, but H1 violates the central meaning of
entity knowledge in the now-supported Vessel/Hosted Entity model, and M1 leaves
an intrinsic combat rule unenforced at public state boundaries.

## Correction Progress

- **O5-R21 implemented:** runtime actors and save v15 now retain an immutable
  combat-profile source actor, source entity, and revision.
- **O5-R22 implemented:** observations, Analyze, queries, execution authority,
  and automated seeds use exact profile identity; durable facts use the source
  entity; profile replacement invalidates every encounter domain.
- **O5-R23 implemented:** Almighty cannot be stored as affinity knowledge;
  constructors, transitions, views, automated seeds, host JSON decoding, and
  save validation reject malformed input before strategy or presentation.
- **O5-R24 implemented:** the mechanics, developer, technical, actor, save,
  public API, and progress documents now describe the corrected contracts.
- **O5-R25 verified:** the
  [independent closure review](battle-knowledge-order-5-r25-independent-closure-review-2026-07-29.md)
  re-read the corrected implementation and integrations, found no unresolved
  realistic reachable defect, passed every locally executable release gate,
  and returned `battle_knowledge` to `complete`.
