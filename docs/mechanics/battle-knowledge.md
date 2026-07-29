# Battle Knowledge

## Purpose

Battle knowledge lets a game distinguish what the player or an AI side has
actually learned from what the framework can privately calculate. A host may
present known facts as icons, target annotations, analysis panels, or text.
Unknown facts remain unknown even though the target's definition exists in the
catalog.

The feature is optional. A game may execute battles without retaining any
knowledge, may keep only encounter knowledge, or may save player knowledge
between encounters.

## Two Scopes

Convergence has two deliberately separate knowledge scopes.

| Scope | Identity | Typical owner | Lifetime |
|---|---|---|---|
| Persistent entity knowledge | Combat-profile source entity definition ID | Player session | Included in a save until a game replaces or removes it |
| Encounter knowledge | Runtime target ID plus exact combat-profile identity | One battle team | Discarded after the encounter unless a host explicitly retains diagnostic evidence |

Persistent knowledge answers a question such as, "What did the player learn
about Ashling as an entity type?" Encounter knowledge answers, "What does this
team know about this particular Ashling right now?"

This distinction prevents a temporary shield, affinity override, Break,
guarding state, or passive replacement from rewriting a permanent entity
record.

### Vessels And Changing Combat Profiles

The actor visible on the battlefield is not always the entity supplying its
combat rules. A Vessel may use an Active Hosted Entity's stats, defenses,
skills, and passives. In that case:

- the Vessel runtime ID still identifies the target on the battlefield;
- encounter facts are bound to that runtime target's exact Hosted Entity
  profile, including its profile revision; and
- persistent facts are credited to the Hosted Entity definition that supplied
  the observed defense, not to the Vessel definition.

Selecting another Hosted Entity, restoring another source instance, or
successfully recomposing the profile invalidates that target's prior encounter
facts and Analyze disclosures. Persistent facts about the old source entity
remain valid for that entity. This prevents knowledge about one Hosted Entity
from being displayed as knowledge about another merely because both appeared
through the same Vessel.

## Knowledge Domains

Both scopes may describe:

- elemental affinities;
- ailment resistances; and
- instant-defeat resistances.

Encounter analysis may additionally disclose current HP, current SP, core
stats, and skills. Those values belong to the current runtime target and never
become persistent entity knowledge.

`Almighty` always resolves as `Normal` and is never a stored affinity fact.
Public encounter entries reject it, persistent transitions reject malformed
or cloned entries, save validation reports a typed diagnostic, and host-owned
save decoding cannot construct it through the standard snapshot constructor.
Analyze may still report its intrinsic `Normal` result without adding an
elemental entry.

## What Ordinary Actions Teach

The framework derives knowledge only from typed execution evidence.

| Executed outcome | Encounter knowledge | Persistent knowledge |
|---|---|---|
| Elemental hit makes contact | Effective affinity for that runtime target | Authored affinity only when no temporary defense influenced resolution |
| Every hit misses | Nothing | Nothing |
| Ailment is successfully applied | Nothing exact about the hidden tier | Nothing |
| Ailment fails its chance roll | Nothing exact about the hidden tier | Nothing |
| Ailment is explicitly blocked as immune | Effective immunity | Authored immunity only when the result was not temporarily modified |
| Instant defeat succeeds | No exact hidden tier is inferred | Nothing |
| Instant defeat fails a random roll | Nothing exact about the hidden tier | Nothing |
| Instant defeat is explicitly blocked by immunity | Effective immunity | Authored immunity only when the result was not temporarily modified |

A success does not prove whether an ailment target was vulnerable, normal, or
resistant. Likewise, an ordinary random failure does not prove resistance.
Convergence records an exact tier only when typed evidence establishes that
tier. A claimed ailment immunity is valid only when the application result and
the resolved effective resistance both identify immunity; contradictory custom
evidence is rejected instead of being silently ignored.

### Temporary Defense Example

Suppose the player already knows that an entity is naturally weak to Ice. A
particular instance receives a temporary Ice-repelling shield and is then hit
by Ice.

- Encounter knowledge records `Repel` for that runtime instance.
- Persistent knowledge remains `Weak` for the entity definition.
- A target UI may show the encounter fact while the shield lasts.
- The temporary result cannot corrupt the saved entity record.

The observation remains encounter history after the temporary state expires;
its influence flags tell a host that it is not timeless. Convergence's supplied
deterministic AI therefore does not use temporarily influenced facts for later
weakness preference or defense avoidance. A game-specific strategy may use one
only while it can independently prove that the named influence is still active.

## Analyze

Analyze asks an injected disclosure policy about each requested field. The
standard policy discloses available fields. A restricted policy may return a
typed `Unknown` result without exposing the hidden value.

The approved boss profile hides:

- current HP;
- current SP;
- skills;
- elemental affinities;
- ailment resistances; and
- instant-defeat resistances.

Core stats are a separate field and the game may choose whether that boss
profile hides them. Constructing the supplied restricted policy without an
explicit field list hides every analysis field. Restriction is selected by
policy, not by an entity name, description, or hard-coded boss ID. Repeated
Analyze attempts cannot bypass the policy.

Disclosed defenses enter persistent entity knowledge. Disclosed current
resources, stats, and skills remain encounter-only. If a complete defense
profile is disclosed, omitted sparse entries are known to be `Normal`; the
persistent snapshot records that the corresponding profile was analyzed.

```mermaid
flowchart TD
    A["Analyze command resolves"] --> B["Disclosure policy decides each requested field"]
    B --> C{"Field status"}
    C -->|"Unknown"| D["Store no value"]
    C -->|"Unavailable"| E["Report unavailable; store no value"]
    C -->|"Disclosed current state"| F["Record encounter visibility"]
    C -->|"Disclosed authored defense"| G["Record encounter visibility and persistent entity fact"]
```

## Familiar Entities

A game may choose to reveal authored defenses when the player first acquires
or registers an entity. The supplied familiar-knowledge policy imports all
authored elemental, ailment, and instant-defeat defenses after an explicit
acquisition or Compendium hook.

The import is optional and replaceable. A disabled supplied policy imports
nothing. A custom policy may distinguish direct requests, acquisitions,
explicit Compendium registration, and synchronization from already registered
entries.

Familiarity affects player knowledge only. It never trains ordinary enemy AI.

A batch import may accept valid requested entities while reporting diagnostics
for invalid or missing entries. The returned `After` snapshot contains every
accepted import. A game that requires an all-or-nothing batch must reject that
snapshot when diagnostics are present; a game that accepts partial batches may
commit it after presenting or recording the diagnostics.

## AI Knowledge

Every team in an ordinary automated battle starts with empty encounter
knowledge. When one teammate establishes a fact, later teammates on that side
can use it during the same encounter. The final team snapshots are diagnostic
results; they are not automatically saved.

A boss or scripted encounter may receive an explicit team seed. The seed must
reference participating teams and match each target's current combat-profile
source and revision exactly. A seed containing stored Almighty affinity
knowledge is rejected before strategy selection. Supplying a valid seed does
not promote it into player knowledge and does not make it survive a later
ordinary encounter.

## Battle End

Persistent knowledge remains available to the player session. Encounter
knowledge is discarded. A host may keep an immutable final encounter snapshot
for logs or testing, but it must not treat that snapshot as the next battle's
starting state unless it deliberately supplies it as a new seed.

## Presentation Rules

- Query typed knowledge; never parse damage messages or animations.
- Query with the target's current combat-profile identity, not merely the
  battlefield actor's entity definition.
- Prefer an encounter fact over a persistent fact for the same current target.
- Treat a missing fact as unknown, not normal, unless a complete analyzed
  profile explicitly establishes the default.
- Show restricted Analyze fields as unknown without revealing their values.
- Do not expose framework-private catalog data merely because a host can access
  the catalog.

## Related Rules

- [Combat, Defenses, And Turn Economy](combat-defenses-and-turns.md)
- [Status And Passive Lifecycle](status-passive-lifecycle.md)
- [Fusion, Inheritance, Acquisition, And Compendium](fusion-acquisition-and-compendium.md)
- [Saving, Loading, And Suspend Saves](saving-loading-and-suspend.md)
