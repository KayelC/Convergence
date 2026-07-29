# Battle Knowledge Order 5 R25 Independent Closure Review

**Date:** 29 July 2026

**Reviewed revision:** `abf1c832`

**Disposition:** Order 5 closed; `battle_knowledge` is `complete`

## Review Method

This closure review treated the current source as authority. It re-traced the
Battle Knowledge implementation after O5-R21 through O5-R24 without accepting
the earlier audit, correction notes, or test names as proof of correctness.
The review followed data from actor composition through battle execution,
knowledge transitions, automated selection, familiarity imports, persistence,
restoration, and host integration.

The reviewed implementation included:

- immutable persistent and encounter knowledge snapshots;
- combat-profile identity creation, replacement, and restoration;
- observation and Analyze evidence emitted by typed effect execution;
- aggregate provenance validation and atomic knowledge transition;
- player and automated-team knowledge ownership;
- familiar-entity imports from Compendium or acquisition flows;
- save validation, host-owned JSON decoding, and aggregate session restore; and
- mechanics, developer, technical, API, actor-state, and save guidance.

A potential issue was recorded only when a supported caller could reach it,
the behavior violated a confirmed invariant, and the consequence was concrete.
No such unresolved issue remained.

## Verified Runtime Authority

### Persistent knowledge

Persistent facts are keyed by the content ID of the entity that supplies the
current combat profile. For an independent actor that is the actor's own
entity. For a Vessel, it is the active Hosted Entity. Temporary actor state,
equipment overrides, shields, and encounter observations do not overwrite
these durable facts.

Intrinsic Almighty affinity remains Normal and is never stored. Public
snapshot construction, transition services, views, automated seeds,
host-owned JSON decoding, and save validation reject attempted stored Almighty
facts before they can enter strategy or presentation.

### Encounter knowledge

Encounter facts are keyed by runtime target and the target's exact immutable
combat-profile identity: source actor, optional source Hosted Entity, and
revision. Recomposition advances the revision. A profile replacement therefore
cannot inherit Analyze disclosure or observed defenses from the previous
profile, even when the same runtime actor remains deployed.

The explicit profile-rebind transition removes elemental, ailment,
instant-defeat, and Analyze state together. Views also require exact profile
identity, so a stale snapshot is ignored rather than displayed while cleanup
is pending.

### Execution integration

Knowledge evidence is accepted only against an immutable execution authority
containing the accepted action, acting actor, effect positions, runtime
targets, target entities, and target combat profiles. The aggregate service
preflights the complete evidence batch before invoking any lower transition.
A malformed registered custom effect therefore cannot partially commit valid
facts before a later forged fact is rejected.

Elemental, ailment, instant-defeat, and Analyze evidence use the same canonical
transition boundary. Almighty observations may resolve as intrinsic Normal for
presentation but are omitted from persistent storage.

### Ownership and lifetime

Player knowledge and automated-team knowledge remain separate authorities.
Ordinary automated teams begin each encounter without learned facts and share
only their own encounter snapshot during that battle. A host may provide an
explicit seed for a special encounter, but the seed must identify a current
participant and its exact combat profile.

Familiarity import remains optional and policy-controlled. When enabled, it
imports authored defenses for the acquired or registered entity into the
player's persistent snapshot through the canonical persistent transition. It
does not teach automated opponents or copy temporary runtime defenses.

### Persistence and restoration

Save contract v15 persists durable player knowledge and actor combat-profile
identity. Encounter-local knowledge is deliberately absent. Validation rejects
duplicate facts, undefined enums, missing catalog targets, encounter-only
entries, impossible Almighty facts, and incoherent actor/profile references.
Aggregate restoration resolves actor dependencies before accepting the saved
profile identity and commits no partial session on rejection.

## Adversarial Review Results

The following boundaries were exercised or re-read specifically for hostile or
stale input:

- Hosted Entity swap after prior observation or Analyze;
- stale profile revision with the same runtime target;
- mismatched source action, acting actor, effect index, target entity, runtime
  target, or target profile in custom effect evidence;
- mixed valid and invalid evidence in one execution result;
- malformed persistent, encounter, automated-seed, and save snapshots;
- attempted Almighty storage through every public construction path;
- duplicate and cross-domain knowledge entries;
- familiar imports for absent, unknown, and policy-rejected entities; and
- save restoration with unresolved actor or Hosted Entity references.

No realistic reachable High, Medium, or Low correctness finding remained after
this trace. Unsupported nested Vessel composition was considered separately;
the current roster and restoration contracts model one owner with one active
Hosted Entity, so nested composition is not a supported path and is not being
misreported as a vulnerability.

## Documentation Review

The three Battle Knowledge audiences agree with the implementation:

- mechanics explains what the player and automated opponents may know;
- developer guidance explains profile-aware integration, optional familiarity,
  Analyze policy, seeds, and save ownership; and
- technical guidance identifies the immutable authorities, provenance checks,
  transition order, revision invalidation, and persistence boundary.

The actor-state, save-contract, public-API, roadmap, and capability records also
use the same source-entity/profile-revision model. No active document treats
actor identity alone as sufficient encounter authority or permits stored
Almighty affinity facts.

## Verification

- Focused Framework Battle Knowledge/profile/save tests: **278 passed**, 0
  failed, 0 skipped.
- Focused DemoHost Training Annex/save tests: **126 passed**, 0 failed, 0
  skipped.
- Full Debug solution: **1,761 passed** (`1,577` Framework, `177` DemoHost,
  `7` ContentValidator), 0 failed, 0 skipped.
- Full Release solution: **1,761 passed**, 0 failed, 0 skipped.
- Strict Debug and Release nonincremental builds: **0 warnings, 0 errors**.
- Formatting verification: passed.
- Framework coverage: **90.75% lines, 76.46% branches**; both release
  thresholds passed.
- Content validation: **6 packs, 36 documents, 98 qualified definitions**;
  schema, deserialization, semantic, dependency, registration, and catalog
  checks passed.
- Clean battle, field, save, and Training Annex noninteractive demos: passed
  with exit code `0`.
- Managed Godot integration contracts: **9 passed**. The Godot sample also
  compiled in the strict Release solution build.
- Architecture and documentation guards: **56 passed**.
- Framework trimming analysis: **0 warnings, 0 errors**.

The native Godot 4.7.1 Windows smoke was not repeated because the locally
installed engine has a previously reproduced native signal/access crash before
project execution. Managed contracts and sample compilation passed; connected
CI remains authoritative for the native engine smoke.

The online NuGet vulnerability lookup could not be run from the restricted
local environment without approval to disclose dependency metadata. Locked
restore remains part of connected CI. This external availability limitation
does not weaken the Battle Knowledge runtime evidence above.

## Closure Decision

O5-R21 through O5-R24 correct the two findings that reopened Order 5, and this
independent source-first pass found no unresolved realistic reachable defect.
The release-quality gates available locally are green. Order 5 is formally
closed and `battle_knowledge` returns from `partial` to `complete`.

Order 6, encounter orchestration, is now the next collaborative documentation
and implementation review subject. This closure does not claim that every
future host must expose the same Battle Knowledge presentation; it certifies
the reusable host-neutral authority and contracts in `Convergence.Framework`.
