# Battle Knowledge Order 5 Discussion Transcript

Status: historical verbatim record

Date: 2026-07-27

This file preserves the opening Order 5 discussion word for word. It is
historical evidence, not active implementation authority. The normalized active
decision and implementation roadmap live in the current documentation set.

## Assistant

````text
**Order 5: Battle Knowledge**

In plain terms, Order 5 governs what the player and enemy AI know about combatants:

- elemental weaknesses and resistances;
- ailment resistance;
- instant-defeat resistance;
- what Analyze reveals;
- how knowledge is learned;
- whether it lasts for one battle or persists between battles;
- how prior ownership or Compendium registration grants familiarity.

A Godot interface would later read this typed knowledge to show symbols such as `Weak`, `Resist`, or `?` while selecting a target. The framework owns the facts and discovery rules; Godot owns how they are displayed.

**What Already Exists**

The framework has three knowledge stores in [CombatKnowledgeStores.cs](C:/Users/kayel/Documents/GitHub/Convergence/src/Convergence.Framework/Knowledge/CombatKnowledgeStores.cs):

- `ElementalAffinityKnowledge`
- `AilmentResistanceKnowledge`
- `InstantDeathResistanceKnowledge`

Knowledge is keyed by entity definition ID. This means learning about one Ashling can apply to another Ashling in a later encounter, which matches your intent.

Knowledge also has save contracts in [RuntimePersistenceSnapshots.cs](C:/Users/kayel/Documents/GitHub/Convergence/src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs). Therefore persistent player knowledge can already survive save and restore.

The automated battle runner creates fresh elemental knowledge for each team at the beginning of a battle. This broadly implements:

- player knowledge can persist;
- ordinary enemy AI starts fresh;
- AI learns while fighting;
- a special encounter could eventually be seeded with prior knowledge.

The familiar-entity service can import authored defenses after ownership or Compendium registration.

Those are useful foundations. Order 5 does not need to begin from zero.

**The Important Problems**

1. **The DemoHost currently owns the actual learning rules**

The decision to learn from an attack, Analyze, ailment attempt, or instant-defeat attempt lives mainly in [TrainingAnnexBattleActionAdapter.cs](C:/Users/kayel/Documents/GitHub/Convergence/samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs:171).

That means a Godot developer would currently need to recreate this logic. This conflicts with the framework-first direction. The framework should receive typed combat evidence and decide what becomes knowledge.

2. **A missed attack can reveal an affinity**

Damage execution provides a resolved affinity even when every hit misses. Both the automated runner and Training Annex can record it.

In gameplay terms:

> The player completely misses with an Ice attack, yet somehow learns that the enemy is weak to Ice.

That should not happen. Actual contact should be required.

3. **Temporary defenses can corrupt permanent knowledge**

The affinity returned by combat is the currently effective affinity. It includes:

- guarding;
- temporary shields;
- affinity breaks;
- affinity overrides;
- passive replacements.

The current host can save that result as the species’ permanent affinity.

For example:

> An Ashling temporarily has a Fire-reflecting shield. The player attacks it, sees `Repel`, and the save permanently records all Ashlings as naturally repelling Fire.

Persistent species knowledge must be kept separate from temporary encounter knowledge.

4. **Ailment and instant-defeat attempts reveal too much**

Training Annex currently reads the target’s hidden defense profile after virtually any success or failure.

A failed poison attempt might simply be an unlucky roll. It does not prove whether the target is Normal, Resistant, or Immune. Yet the host currently records the exact hidden resistance.

This is accidental omniscience.

5. **Analyze has a second, competing knowledge model**

Analyze stores revealed layers directly on the acting actor, keyed by the target’s runtime instance ID:

```text
Player analyzed enemy instance #17
```

Persistent knowledge instead uses the entity definition ID:

```text
Player knows facts about the Ashling species
```

Both identities are useful, but they serve different purposes:

- runtime instance knowledge: current HP, current skills, temporary state;
- entity knowledge: persistent natural affinities and resistances.

Currently those responsibilities are mixed. Analysis markers can also enter actor saves even though their target may have ceased to exist after battle.

6. **AI only consumes elemental knowledge**

The framework defines ailment and instant-defeat knowledge, but its supplied deterministic AI only reads elemental affinity knowledge.

That is not necessarily wrong for a simple strategy, but the AI should receive one read-only knowledge view and choose which domains it understands. It should not be coupled directly to one mutable store.

7. **The capability matrix is overly optimistic**

[framework-capability-matrix.json](C:/Users/kayel/Documents/GitHub/Convergence/tests/Convergence.Framework.Tests/Fixtures/framework-capability-matrix.json:130) currently calls `battle_knowledge` complete.

I disagree with that classification. Storage and persistence are implemented, but rule authority still leaks into DemoHost and the temporary/permanent distinction is unsafe. Order 5 should temporarily mark it partial until these rules become framework-owned and tested.

**Recommended Model**

I recommend two explicit knowledge scopes.

**Persistent entity knowledge**

Saved between battles and keyed by entity definition ID.

It contains natural authored facts:

- base elemental affinities;
- base ailment resistances;
- base instant-defeat resistances.

This is what the player interface uses when encountering another member of a previously studied species.

**Encounter knowledge**

Temporary and normally keyed by runtime instance ID.

It contains battle-specific observations:

- a currently active shield;
- a temporary affinity override;
- analyzed current stats;
- analyzed current skills;
- effective defenses observed during this encounter.

Ordinary enemy AI receives a fresh encounter store. Bosses or scripted encounters may receive an explicitly seeded store. Nothing is carried forward automatically.

**Recommended Default Discovery Rules**

- A missed damage effect reveals nothing.
- A landed elemental effect reveals the effective affinity for that encounter.
- It updates persistent base knowledge only when no temporary defense altered the result.
- An ailment result of `Immune` reveals exact immunity.
- A successful or randomly missed ailment does not reveal the exact resistance tier.
- Instant-defeat attempts follow the same conservative rule.
- Analyze permanently records the authored defense layers it reveals.
- Analyze of current stats and skills remains encounter-only.
- First ownership, recruitment, or fusion imports authored knowledge into the player store only.
- Enemy teammates share what their side learns during the encounter.
- Enemy knowledge is discarded after an ordinary battle.
- Temporary observations can never overwrite natural persistent knowledge.

I also recommend adding an explicit `InstantDefeatResistance` analysis layer. At present it can only be revealed indirectly through `Full`.

**Proposed Checkpoints**

1. Record the approved discovery and persistence rules.
2. Introduce framework-owned immutable knowledge transitions and typed observation evidence.
3. Separate persistent entity knowledge from encounter-instance analysis.
4. Correct damage, ailment, and instant-defeat discovery.
5. Route automated AI through an aggregate read-only knowledge contract.
6. Move familiar/Compendium discovery behind the same framework policy.
7. Update save validation, DemoHost, tests, mechanics documentation, developer guidance, and technical diagrams.
8. Perform an independent source and documentation review before closing Order 5.

**Decisions Needed**

My recommendations are:

1. **Ownership reveals all authored defenses**, not only elemental affinities. This includes ailments and instant-defeat resistance because an owned entity can normally be inspected fully.
2. **Analyze defense information persists**, while analyzed current stats and skills last only for that encounter.
3. **Ordinary ailment/instant-defeat attempts use conservative discovery**: exact immunity may be learned, but random success or failure does not expose the exact hidden tier.
4. **AI knowledge is team-shared and encounter-local by default**, with explicit optional seeding for bosses.

These defaults preserve your original Ashling example without allowing misses, shields, or bad random rolls to poison the player’s permanent knowledge.
````

## Project Owner

```text
1. Approved.

2. Approved, this answers the question I was going to ask that Skills such as Null Ice shouldn't alter the knowledge's permanent record,

3. Approved

4. Approved

I'd like a policy where when Analyze is used on a Boss the player is unable to cheese their way through, thus just like in the Persona Games, both HP, SP, Affinities and Skills would appear as '???'

You are to generate a report with all of that was discussed, word for word, I'd like this entire prompt to be saved, of course the 'Decisions Needed' would now reflected their approved state. Proceed accordingly.
```

## Project Owner Addendum

```text
Apologies for my oversight you may add the boss ailment and instant-defeat disclosure.
```
