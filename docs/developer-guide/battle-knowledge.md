# Battle Knowledge Integration

## Integration Boundary

The framework owns typed observations, disclosure policy, immutable knowledge
transitions, identity validation, and save validation. A Godot or other host
owns presentation, the player's retained snapshot, encounter composition, and
the decision to enable optional familiarity imports.

Do not reconstruct knowledge from damage numbers, combat text, target
definitions, animations, or action display names. Executed effects already
carry the evidence that a knowledge policy is allowed to use.

Use only the immutable persistent and encounter snapshots described in this
guide. Runtime actors contain no separate Analyze store, and the public API
exposes no independent mutable discovery dictionary. Malformed host-supplied
snapshot domains reject through typed transition diagnostics before any
knowledge dictionary or result snapshot is constructed.

## State To Keep

A player-facing host normally keeps:

```csharp
RuntimeKnowledgeSnapshot playerKnowledge = new();
RuntimeEncounterKnowledgeSnapshot playerEncounterKnowledge =
    RuntimeEncounterKnowledgeSnapshot.Empty;
```

Each AI team receives its own `RuntimeEncounterKnowledgeSnapshot`. Never share
one snapshot between opposing teams. `RuntimeKnowledgeSnapshot` is the only one
of these two types that belongs in `RuntimeSaveGameSnapshot.Knowledge`.

Every live actor also exposes `CombatProfileIdentity`. It identifies the
runtime source actor, authored source entity, and profile revision currently
supplying combat-facing stats, defenses, skills, and passives. A self-sourced
actor points to itself. A composed Vessel points to its Active Hosted Entity.

## Apply Executed Evidence

After an action has executed successfully, pass its complete ordered effect
results to one coordinator. Build the authority envelope from the action the
framework accepted and the live encounter participants, not from nested
knowledge evidence:

```csharp
var transitionService = new BattleKnowledgeExecutionTransitionService();
var authority = new BattleKnowledgeExecutionAuthority(
    acceptedActionId,
    actingActor.InstanceId,
    encounterParticipants.Select(participant =>
        KeyValuePair.Create(
            participant.InstanceId,
            participant.State.CombatProfileIdentity)));

BattleKnowledgeExecutionTransitionResult knowledge = transitionService.Apply(
    new BattleKnowledgeExecutionTransitionRequest(
        playerKnowledge,
        playerEncounterKnowledge,
        authority,
        actionResult.Effects,
        BattleKnowledgePersistenceScope.EncounterAndPersistent));

if (knowledge.Status == BattleKnowledgeTransitionStatus.Rejected)
{
    // Treat diagnostics as a typed integration fault. Do not publish partial state.
    PresentKnowledgeFault(knowledge.Diagnostics);
    return;
}

playerKnowledge = knowledge.PersistentAfter;
playerEncounterKnowledge = knowledge.EncounterAfter;
PresentDiscoveries(knowledge.AcceptedObservations, knowledge.ProcessedAnalyses);
```

For ordinary AI, use `BattleKnowledgePersistenceScope.EncounterOnly` and an
empty persistent snapshot. The result is atomic: if any later effect conflicts
with an earlier runtime-target identity, both `After` snapshots are the original
`Before` snapshots.

Apply knowledge only after an execution result has been accepted. Assessment,
menu selection, cancellation, and rejected execution do not teach anything.
The target map should include every current participant that may appear in an
effect result, including the acting actor when an action can affect itself. It
is an identity authority, not a declaration that every participant was
targeted.

Before applying any lower transition, the coordinator preflights every
observation and Analyze result against:

- the enclosing effect index and runtime target;
- the accepted source action;
- the acting runtime actor; and
- the authoritative combat-profile source and revision for that runtime target.

A mismatch returns a stable typed diagnostic and the original persistent and
encounter snapshots. No earlier valid effect in the same batch is published.
This protects the public `ICustomEffectHandler` extension boundary as well as
ordinary framework executors.

Custom handlers should still construct evidence from their
`EffectExecutionContext`. The authority envelope is a fail-closed validation
boundary, not a substitute for correct handler implementation. The
`BattleKnowledgeExecutionTransitionRequest` constructor requires this envelope;
there is no authority-free overload.

For instant-defeat evidence, report one complete resistance shape. A bypassed
effect must omit the channel, authored resistance, and effective resistance. A
checked effect must provide all three. Supplying only part of a checked tuple is
an integration error rejected by `BattleKnowledgeObservation.InstantDeath`.

For ailment evidence, an `Immune` application status must carry
`ResistanceLevel.Immune` as its effective resistance. The public evidence
factory rejects a missing or contradictory effective value before a knowledge
transition can silently discard the observation.

## Query For UI Or Strategy

Use `BattleKnowledgeView` to combine the two scopes:

```csharp
IBattleKnowledgeView view = new BattleKnowledgeView(
    playerKnowledge,
    playerEncounterKnowledge);

if (view.TryGetElementalAffinity(
        targetInstanceId,
        targetActor.State.CombatProfileIdentity,
        DamageElement.Ice,
        out ElementalAffinity affinity,
        out BattleKnowledgeFactSource source,
        out BattleDefenseInfluence influences))
{
    targetPanel.ShowAffinity(affinity, source, influences);
}
else
{
    targetPanel.ShowUnknownAffinity();
}
```

The encounter value takes precedence. Always supply the target runtime ID and
its current `RuntimeCombatProfileIdentitySnapshot`. A stale profile never
reuses encounter facts. If no current encounter fact exists, the combined view
falls back to persistent knowledge keyed by the profile's source entity ID.

`BattleKnowledgeExecutionTransitionService` rebinds every authoritative target
profile before applying execution evidence. If a host needs target panels or AI
state cleared immediately after a successful Hosted Entity swap, before another
action executes, call `IBattleKnowledgeTargetProfileTransitionService` with the
new live profile and replace the encounter snapshot with its `After` result.
That transition removes the target's elemental, ailment, instant-defeat, and
Analyze entries as one immutable update.

## Configure Analyze

Analyze resolution uses the `IBattleAnalysisService` configured in
`BattleExecutionServices`. Select a disclosure policy while composing battle
services:

```csharp
var hiddenForBoss = new RestrictedBattleAnalysisDisclosurePolicy(
[
    BattleAnalysisField.CurrentHp,
    BattleAnalysisField.CurrentSp,
    BattleAnalysisField.Skills,
    BattleAnalysisField.ElementalAffinities,
    BattleAnalysisField.AilmentResistances,
    BattleAnalysisField.InstantDeathResistances
]);

IBattleAnalysisService analysis = new BattleAnalysisService(hiddenForBoss);
```

Supply that instance through the `BattleAnalysis` init property while creating
`BattleExecutionServices`. The exact construction helper is host-owned; the
important contract is that the intended service is present before execution. A
disclosure policy must return one `Disclosed` or `Unknown`
decision for every requested field. The analysis service alone may convert a
disclosed SP request to `Unavailable` when the target has no SP resource.

Do not select boss restrictions by parsing a name or description. Encounter
composition should choose the policy or analysis service explicitly.

## Automated Battles

`AutomatedBattleRequest` starts every participating team with empty encounter
knowledge unless `TeamKnowledgeSeeds` are supplied. The runner:

1. gives a team's aggregate read-only view to its selector;
2. applies typed effect evidence after a successful action;
3. shares the updated snapshot with later teammates on that team; and
4. returns immutable final snapshots in `AutomatedBattleResult.TeamKnowledge`.

Seeds must use a participating team ID and exact target combat-profile
identities that match current participants. A different source instance,
source entity, or revision is stale and rejects. Stored Almighty affinity facts
also reject before a selector receives the seed. The runner never adds those
snapshots to a save.

The supplied deterministic selector scores only facts with
`BattleDefenseInfluence.None`. A temporary encounter observation remains useful
presentation evidence, but the snapshot alone cannot prove that its shield,
Break, override, guard, or conditional passive is still active on a later turn.
If a custom selector wants to act on temporary observations, it must compare the
returned influence flags with authoritative live state before trusting them.

## Familiarity Import

Acquisition and Compendium code must opt in explicitly:

```csharp
var familiarity = new FamiliarEntityKnowledgeService(
    catalog,
    new StandardFamiliarKnowledgeImportPolicy());

FamiliarKnowledgeImportResult imported = familiarity.Import(
    playerKnowledge,
    [acquiredEntityId],
    FamiliarKnowledgeImportSource.Acquisition);

if (imported.IsSuccess)
{
    playerKnowledge = imported.After;
}
```

Use `DisabledFamiliarKnowledgeImportPolicy` when acquisition should not reveal
defenses, or implement `IFamiliarKnowledgeImportPolicy` for a game-specific
rule. The import service does not acquire, recruit, register, or fuse an entity;
the host calls it after the owning transaction succeeds.

The service validates the complete current `RuntimeKnowledgeSnapshot` before
it enumerates requested entities or invokes `IFamiliarKnowledgeImportPolicy`.
Malformed current state returns the same `Before` and `After` snapshot, no
imported entity IDs, and typed diagnostics; the policy is not called. Empty,
disabled, or unavailable imports still pass through the injected persistent
transition authority, so a no-op cannot bypass validation.

For a multi-entity request, `IsSuccess` means that the complete batch produced
no diagnostics. It does not mean that `After` is unchanged when one requested
entity fails. Valid requested entities are imported and listed in
`ImportedEntityIds`; malformed or missing entries produce diagnostics. The
example above chooses atomic host behavior by retaining `Before` whenever
`IsSuccess` is false.

A host that accepts valid entries from a partial batch may instead commit
`imported.After` and then log or present every entry in `imported.Diagnostics`
through its own event surface. Make that choice explicitly rather than assuming
that a diagnostic always means an unchanged snapshot.

## Save And Restore

- Save `RuntimeKnowledgeSnapshot` in `RuntimeSaveGameSnapshot.Knowledge`.
- Do not save `RuntimeEncounterKnowledgeSnapshot` for an ordinary session save.
- Clear or discard encounter snapshots at battle end.
- Validate before restore with `RuntimeSaveValidator`.

Validation rejects duplicate facts, duplicate analyzed profiles, malformed
enum or analysis-field domains, stored Almighty affinity facts, and missing
entity or ailment references.
Actor snapshots contain no Analyze state: canonical Analyze writes
`RuntimeEncounterKnowledgeSnapshot`, which an ordinary session save
intentionally discards at encounter end.

## Godot Responsibilities

Godot should:

- map `RuntimeInstanceId` values to current Nodes;
- choose analysis presentation and encounter policy composition;
- render typed known, unknown, and unavailable fields;
- retain the player's persistent snapshot in its save envelope;
- discard ordinary encounter snapshots after battle; and
- keep catalog definitions private from UI paths that should show unknown data.

Godot should not:

- inspect definitions to bypass disclosure policy;
- infer affinity from text, color, or damage magnitude;
- reuse enemy encounter knowledge in a later ordinary battle; or
- apply partial transition results after a rejection.

## Related References

- [Battle Knowledge](../mechanics/battle-knowledge.md)
- [Typed Actions And Effects](typed-actions-and-effects.md)
- [Combat Resolution Policies](combat-resolution-policies.md)
- [Battle Knowledge Runtime Authority](../technical/battle-knowledge-runtime.md)
