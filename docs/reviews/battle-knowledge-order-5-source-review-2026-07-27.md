# Battle Knowledge Order 5 Source Review

Status: implementation in progress; O5-R1 through O5-R3 implemented and reviewed

Date: 2026-07-27

## Purpose

This report records the source-backed opening review for Documentation Order 5,
`battle_knowledge`, and the project owner's confirmed rules. It is the active
authority for the implementation roadmap below. A separate historical transcript
preserves the preceding discussion verbatim; this report restates its decisions
in Convergence terminology so active product documentation remains neutral.

Order 5 determines:

- what battle-defense facts may be known;
- how observations become knowledge;
- which facts persist between encounters;
- what ordinary enemy AI remembers;
- what Analyze may disclose;
- how prior ownership or Compendium registration imports familiarity; and
- how a host presents known and unknown information without becoming the rule
  authority.

Order 5 does not define a Godot interface. The framework owns typed knowledge,
discovery, scope, and disclosure results. A host decides whether those results
appear as icons, target annotations, panels, text, or another presentation.

## Source-Verified Starting Point

The current implementation already provides useful foundations:

- `ElementalAffinityKnowledge`, `AilmentResistanceKnowledge`, and
  `InstantDeathResistanceKnowledge` store exact typed facts by entity definition
  ID.
- `RuntimeKnowledgeSnapshot` persists the three knowledge domains and save
  validation checks catalog references and duplicate entries.
- the automated battle runner creates fresh elemental knowledge for each team;
- the Training Annex host separately retains player knowledge and discards
  ordinary encounter AI knowledge;
- Analyze records revealed layers against a runtime target instance; and
- familiar-entity import can copy authored defenses into player knowledge.

The entity-definition key is correct for persistent familiarity. Knowledge about
one instance of an entity may therefore be available when another instance of
the same authored entity appears later.

## Source-Verified Problems

### O5-H1: Discovery authority resides in DemoHost

The Training Annex adapter decides which execution outcomes reveal elemental,
ailment, and instant-defeat defenses. Another host would need to copy those
rules. Discovery must instead be a framework-owned typed transition so Godot,
console, and tests receive the same decision.

### O5-H2: Misses can reveal elemental affinities

Damage execution reports a resolved affinity even when every hit misses. Current
learning paths can record that affinity without contact. A miss must not reveal
the target's defense.

### O5-H3: Temporary defenses can corrupt persistent species knowledge

The combat affinity result is the effective encounter value. Guarding, shields,
breaks, overrides, and passive replacements may alter it. Current host logic can
save that temporary result as the entity's permanent authored affinity.

Persistent base knowledge and encounter-effective observations must therefore
be separate authorities. Temporary state must never overwrite a permanent
entity record.

### O5-H4: Ailment and instant-defeat attempts reveal hidden exact tiers

The Training Annex adapter reads the target's defense profile after success or
failure. A random failure does not prove an exact resistance tier. Ordinary
execution may disclose only what its typed evidence establishes.

### O5-M1: Analyze mixes encounter and persistent identities

Analyze stores layer flags by runtime instance ID inside actor battle state,
while persistent defense knowledge is keyed by entity definition ID. Runtime
instance state is appropriate for current resources, current skills, and
temporary conditions. It is not an appropriate permanent species record.

The current analysis markers are also captured in actor snapshots without a
defined battle-end invalidation rule. Order 5 must make encounter-only analysis
explicit and prevent stale target instances from becoming durable knowledge.

### O5-M2: AI consumes only one knowledge domain

The supplied deterministic selector accepts a concrete mutable elemental store.
It cannot consume ailment or instant-defeat knowledge through a common view.
Strategies should receive an aggregate read-only knowledge contract and remain
free to use only the domains relevant to that strategy.

### O5-M3: Knowledge scope is an implicit host convention

The intended player-persistent and enemy-encounter-local split exists in the
Training Annex flow, but the framework contracts do not describe it. Ordinary
encounter AI must start fresh by default, teammates must share what their side
learns, and special encounters must be able to receive an explicit seed without
silently persisting it afterward.

### O5-L1: Public mutable stores do not reject invalid content IDs immediately

The knowledge stores validate enum values but allow default or otherwise invalid
content IDs into their dictionaries. Later snapshot construction may then fail
far from the original call. Public learning boundaries must reject malformed
entity and ailment IDs immediately.

### O5-DOC1: Capability evidence overstates completion

The capability matrix currently marks `battle_knowledge` complete even though
discovery authority remains host-owned and the persistent/temporary boundary is
unsafe. Order 5 must record the capability as partial while corrections are in
progress and promote it only after framework tests and all three documentation
audiences agree.

## Confirmed Decisions

The project owner approved the following decisions on 2026-07-27.

### O5-D1: Two knowledge scopes

Convergence will distinguish:

1. **Persistent entity knowledge**, keyed by entity definition ID and included
   in the player's save-facing session snapshot.
2. **Encounter knowledge**, normally keyed by runtime instance ID and discarded
   after an ordinary encounter.

Persistent knowledge describes authored natural defenses. Encounter knowledge
may describe current resources, current skills, and effective defenses affected
by temporary battle state.

### O5-D2: Contact and temporary-state rules

- A missed damage effect reveals nothing.
- A landed elemental effect may reveal the effective affinity for the current
  encounter.
- Persistent base knowledge may be updated from ordinary contact only when
  typed execution evidence proves that no temporary defense altered the result.
- Guarding, shields, breaks, overrides, and passive replacements must not alter
  the permanent entity record.

### O5-D3: Conservative resistance discovery

- An ailment result explicitly blocked as immune may reveal exact immunity.
- A successful ailment application does not reveal whether the hidden authored
  tier was vulnerable, normal, or resistant.
- A random ailment miss reveals no exact resistance.
- Instant-defeat observations follow the same conservative rule.
- Analyze and prior ownership may reveal exact authored tiers according to their
  explicit disclosure policies.

### O5-D4: Analyze persistence

- Authored defense information disclosed by Analyze is added to persistent
  player knowledge.
- Current resources, current stats, and current skills disclosed by Analyze are
  encounter-only.
- Encounter analysis uses runtime instance identity.
- Persistent defense knowledge uses entity definition identity.
- Hidden fields do not update either store merely because the framework or host
  can access the target's definition.

### O5-D5: Restricted Analyze policy for bosses and special targets

Analyze disclosure must be policy-controlled rather than inferred from a display
name, description, or hard-coded entity ID.

The approved supplied restricted behavior presents the following fields as
unknown:

- current HP;
- current SP;
- elemental affinities; and
- skills;
- ailment resistances; and
- instant-defeat resistances.

Restricted fields produce typed `Unknown` disclosure results and do not update
persistent or encounter knowledge. Repeated Analyze attempts cannot bypass the
restriction. A game may supply a different policy or selectively disclose other
fields for a particular encounter.

The current `AnalysisLayer.Stats` grouping is not sufficiently precise to
express HP and SP separately from every other stat. Implementation must add the
smallest typed disclosure model needed rather than relying on presentation text.
The project owner clarified on 2026-07-27 that the supplied restricted behavior
also hides ailment and instant-defeat defenses. These fields therefore remain
unknown and cannot update either knowledge scope through Analyze.

### O5-D6: Familiar ownership imports authored defenses

First ownership, recruitment, fusion, explicit Compendium registration, or
another approved acquisition path may import all authored defenses into the
player's persistent knowledge:

- elemental affinities;
- ailment resistances; and
- instant-defeat resistances.

The import affects player knowledge only. It does not seed ordinary enemy AI.
Acquisition hooks remain explicit so games that do not want this behavior can
omit or replace the policy.

### O5-D7: Encounter-local team AI

- ordinary enemy AI begins each encounter with fresh knowledge;
- knowledge learned by one teammate is shared with that team for the remainder
  of the encounter;
- ordinary encounter knowledge is not saved or carried into the next battle;
- bosses and scripted encounters may receive an explicit host-supplied seed;
  and
- the framework never silently promotes enemy knowledge into persistent player
  knowledge or vice versa.

### O5-D8: Conflict authority

Temporary effective observations cannot overwrite authored persistent facts.
Exact authored disclosures from Analyze or familiar import may refresh the
matching persistent fact against the currently loaded catalog. Knowledge changes
must return immutable before/after snapshots, ordered evidence, and stable typed
diagnostics.

## Implementation Roadmap

| Checkpoint | State | Required outcome |
|---|---|---|
| O5-R1 | `implemented_reviewed` | Add immutable aggregate knowledge views, transitions, diagnostics, and immediate identifier validation. |
| O5-R2 | `implemented_reviewed` | Add typed observation evidence that distinguishes contact, authored base defense, effective defense, and temporary modification. |
| O5-R3 | `implemented_reviewed` | Separate persistent entity knowledge from encounter analysis and define battle-end cleanup. |
| O5-R4 | `pending` | Add policy-controlled Analyze disclosure, including typed restricted/unknown boss fields. |
| O5-R5 | `pending` | Route automated AI through encounter-local aggregate read-only knowledge with optional explicit seeding. |
| O5-R6 | `pending` | Route familiar acquisition and Compendium imports through the approved persistent knowledge policy. |
| O5-R7 | `pending` | Update save validation, DemoHost integration, capability evidence, mechanics, developer, and technical documentation. |
| O5-R8 | `pending` | Perform a fresh source and documentation review, run the complete quality gate, and obtain owner confirmation before closure. |

Each implementation checkpoint receives its own staged commit. Order 5 remains
open until the final source review finds no unresolved realistic defect and the
project owner confirms the documented player-facing rules.

## Checkpoint Review Record

### O5-R1

`RuntimeKnowledgeSnapshot` now has a read-only aggregate view and an atomic
persistent transition service. Invalid identifiers and duplicate discovery
keys reject without changing the before snapshot. The checkpoint review found
no unresolved realistic defect; 1,505 Framework tests passed with zero skips,
and the Framework built nonincrementally with zero warnings.

### O5-R2

Typed effect results now carry immutable `BattleKnowledgeObservation` evidence
for elemental contact, ailments, and instant defeat. The evidence keeps
authored and effective defenses separate and names guard, shield, Break,
override, and passive influence. An all-hit miss remains an explicit miss
observation rather than a discovery. The checkpoint review corrected one
pre-commit ambiguity by separating instant-defeat outcome from resistance
bypass state. The reviewed implementation passed 1,506 Framework tests with
zero skips and built nonincrementally with zero warnings.

### O5-R3

`RuntimeEncounterKnowledgeSnapshot` now owns runtime-instance-scoped defense and
analysis visibility separately from save-facing entity knowledge. The standard
observation transition learns landed effective elemental defenses for the
encounter, promotes an unmodified authored defense only when permitted, and
learns ailment or instant-defeat tiers from explicitly confirmed immunity only.
Encounter facts retain temporary-influence metadata and take precedence in the
aggregate view without overwriting persistent facts. Runtime-ID/entity-ID
conflicts reject atomically, and battle-end cleanup returns an empty encounter
snapshot.

The checkpoint review found and corrected a typed-reason gap in the existing
instant-defeat port: a random failure against an immune definition could not be
distinguished from an explicit resistance block. `ProductionCombatRuleset` now
implements `ITypedInstantDeathExecutionPolicy`; untyped custom policies remain
conservative and cannot claim exact immunity. Duplicate observations are
deduplicated with last-observation authority. The reviewed checkpoint passed
1,517 Framework tests with zero skips and built nonincrementally with zero
warnings.
