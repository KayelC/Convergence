# Fusion, Inheritance, Acquisition, And Compendium

## Fusion Is Optional

Fusion is an optional Framework module. Games that do not use fusion do not need to load fusion recipes or instantiate fusion services. Moon phases are not required by fusion; any phase-like condition must be explicitly supplied by a game that chooses it.

## Result Resolution

Fusion content uses typed parent selectors and result operations. Repository and policy services can resolve direct recipes, catalyst rank shifts, stat boosts, accidents, mutation, and game-specific hooks.

Planning is deterministic for the same content, parent snapshots, policies, and random source. Parent-pair matching is symmetric where the recipe says it is, and result statistics do not depend accidentally on caller parent order.

### Catalyst Rank Shifting

A `catalyst_rank_shift` recipe assigns one parent selector the `catalyst` role
and the other the `rank_shift_target` role. The target moves by the authored
`rankShift` within its own catalog race chart. The catalyst's rank never enters
the calculation, and caller parent order does not change either role.

The participant snapshot's race and rank must match its catalog entity. The
resolver then requires exactly one catalog entity at the shifted rank. A missing
rank or duplicate rank is a typed no-fusion result; the resolver never clamps to
a race endpoint. The resulting actor starts from the resolved catalog entity's
state. Custom registered result policies remain free to define a different,
explicit state-preservation rule.

## Inheritance

The receiving entity's typed inheritance rules determine whether a candidate skill may be selected. The evaluator applies this precedence:

1. Reject a non-inheritable skill.
2. Enforce exclusive-owner restrictions.
3. Apply explicit skill blocks.
4. Apply explicit skill allowances.
5. Apply the receiving entity's inheritance-group allow or deny policy.

An explicit allowance cannot override non-inheritable, owner-exclusive, or explicitly blocked status. Passive skills use the `passive` inheritance group, so blocking an active elemental group does not automatically block a passive related to that element.

Candidate order follows parent and loadout order with first-occurrence deduplication. Preview and commit reuse the same validated selection token, so a host cannot create a legal-looking preview from an impossible raw skill list.

## Fusion Transactions

Preparation validates parents, result identity, ownership, selection, capacity, and the expected before-state. Commit rejects a stale preparation and applies roster changes atomically. No parent is consumed before confirmation.

Fusion produces an acquired runtime actor. Fusion itself does not depend on the Compendium module.

## The General Acquisition Rule

If a game enables a Compendium, every successful mechanic that grants ownership of an eligible entity should call the same Framework operation:

`ICompendiumRuntimeService.RecordAcquisition(currentCompendium, acquiredActor)`

This includes fusion, negotiation recruitment, scripted grants, rewards that create permanent ownership, and future acquisition systems.

The acquisition rule is:

- If no entry exists, create the first Compendium snapshot and return `Added`.
- If an entry already exists, preserve that exact entry and return `AlreadyRegistered`.
- Automatic acquisition never updates levels, stats, skills, name, or progression in an existing entry.
- Invalid or ineligible actors return `InvalidEntry` with diagnostics and do not mutate the Compendium.

This operation is idempotent. Repeating the same acquisition cannot degrade or silently replace a player's saved record.

## Explicit Registration And Update

Updating is a separate, deliberate action:

`ICompendiumRuntimeService.RegisterActor(currentCompendium, selectedOwnedActor)`

Explicit registration adds a missing entry or replaces an existing entry with the selected actor's current immutable snapshot. It returns `Added` or `Updated`. A host should present the update choice clearly and only call it after player confirmation when the game design requires consent.

This separation gives the Compendium a meaningful loop:

- acquisition guarantees discovery;
- the saved record remains stable across later acquisitions;
- the player decides when to update the record;
- recall materializes the saved version rather than whichever copy was acquired most recently.

## Recall

Recall checks that the entry and catalog entity exist, the entity is eligible,
the player does not already own it, a destination roster slot is available, the
runtime ID is unique, and the explicitly selected currency balance can pay the
configured price.

Pricing is an injected policy. Recall can be free, unavailable, fixed-price, linear, or game-specific. Convergence does not require a currency name or formula.

On success, recall rebuilds an actor from the saved level, progression, base
stats, learned/equipped skills, and catalog defaults for other state. It places
the actor in the selected roster and debits the named currency atomically.
Battle status and temporary effects are not copied into the entry.

## Familiar Knowledge

Compendium registration and player battle knowledge are separate modules. A game may call `IFamiliarEntityKnowledgeService` for registered entities to reveal their authored elemental, ailment, and instant-death defenses.

DemoHost imports familiar knowledge after first acquisition, explicit registration, and recall. Enemy AI knowledge remains encounter-scoped. A different game may delay knowledge import or omit it entirely.

## Host Integration Checklist

- Call `RecordAcquisition` only after ownership was successfully granted.
- Persist the returned `After` state when the code is `Added`.
- Treat `AlreadyRegistered` as accepted but unchanged, not as an error.
- Never call `RegisterActor` implicitly from acquisition code.
- Use `RegisterActor` only for an explicit registrar/update workflow.
- Keep Compendium composition outside fusion and recruitment core services so all three modules remain optional.
