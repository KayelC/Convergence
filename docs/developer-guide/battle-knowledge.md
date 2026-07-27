# Battle Knowledge Integration

## Integration Boundary

The framework owns typed observations, disclosure policy, immutable knowledge
transitions, identity validation, and save validation. A Godot or other host
owns presentation, the player's retained snapshot, encounter composition, and
the decision to enable optional familiarity imports.

Do not reconstruct knowledge from damage numbers, combat text, target
definitions, animations, or action display names. Executed effects already
carry the evidence that a knowledge policy is allowed to use.

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

## Apply Executed Evidence

After an action has executed successfully, pass its complete ordered effect
results to one coordinator:

```csharp
var transitionService = new BattleKnowledgeExecutionTransitionService();

BattleKnowledgeExecutionTransitionResult knowledge = transitionService.Apply(
    new BattleKnowledgeExecutionTransitionRequest(
        playerKnowledge,
        playerEncounterKnowledge,
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
Custom effect evidence must report the same effect index as its enclosing
`EffectExecutionResult`; mismatched provenance is rejected without changing
either knowledge scope.

## Query For UI Or Strategy

Use `BattleKnowledgeView` to combine the two scopes:

```csharp
IBattleKnowledgeView view = new BattleKnowledgeView(
    playerKnowledge,
    playerEncounterKnowledge);

if (view.TryGetElementalAffinity(
        targetInstanceId,
        targetEntityId,
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

The encounter value takes precedence. Always supply both runtime and entity
identity; a mismatch is an integration error rather than permission to reuse a
fact for another entity.

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

Seeds must use a participating team ID and target runtime/entity identities
that match current participants. The runner never adds those snapshots to a
save.

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

## Save And Restore

- Save `RuntimeKnowledgeSnapshot` in `RuntimeSaveGameSnapshot.Knowledge`.
- Do not save `RuntimeEncounterKnowledgeSnapshot` for an ordinary session save.
- Clear or discard encounter snapshots at battle end.
- Validate before restore with `RuntimeSaveValidator`.

Validation rejects duplicate facts, duplicate analyzed profiles, missing entity
or ailment references, and actor-local analysis keyed by encounter runtime IDs.
The older `RuntimeBattleStatusSnapshot.Analysis` shape can describe live actor
state, but it is not a valid session-save field because the save aggregate does
not restore an encounter containing those target IDs.

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

- [Battle Knowledge](../mechanics/status-passives-and-knowledge.md)
- [Typed Actions And Effects](typed-actions-and-effects.md)
- [Combat Resolution Policies](combat-resolution-policies.md)
- [Battle Knowledge Runtime Authority](../technical/battle-knowledge-runtime.md)
