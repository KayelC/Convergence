# Dungeon Traversal Integration

## Choose The Boundaries

`RuntimeDungeonTraversalService` is an optional, stateless rules service. It
accepts an immutable `RuntimeDungeonTraversalSnapshot` and a requested
`RuntimeDungeonTraversalTransition`; it returns a candidate snapshot and typed
events. The host owns the live snapshot variable, the scene, and the moment of
adoption. Generic `RuntimeNavigationService` changes a logical location and
does not call traversal. A dungeon-only game need not construct navigation,
though a present v19 `RuntimeFieldSnapshot` still requires a logical location.

Choose stable `ContentId` values for your dungeon, meaningful nodes, and
transitions. A node is a game-defined boundary such as an entrance, room,
checkpoint area, or barrier, not every position traversed in a Godot scene.
The route policy is required and may consult game progression. The progress
registry is also required; pass an empty registry if this game has no
checkpoint/boss recording. It is a declaration of possible reports, not proof
that a battle occurred.

```csharp
using Convergence.Content;
using Convergence.Runtime;

ContentId dungeon = ContentId.Parse("example:depths");
ContentId entrance = ContentId.Parse("example:entrance");
ContentId hall = ContentId.Parse("example:hall");
ContentId checkpoint = ContentId.Parse("example:hall_checkpoint");
var enterHall = new RuntimeDungeonTraversalTransition(
    ContentId.Parse("example:enter_hall"), dungeon, entrance, hall);
var registry = new RuntimeDungeonProgressRegistry(
    [new RuntimeDungeonProgressEligibility(
        RuntimeDungeonProgressKind.Checkpoint, checkpoint, dungeon, [hall])]);
var traversal = new RuntimeDungeonTraversalService(new ExampleRoutePolicy(), registry);
RuntimeDungeonTraversalSnapshot current = new(dungeon, entrance);

sealed class ExampleRoutePolicy : IRuntimeDungeonTraversalPolicy
{
    public RuntimeDungeonTraversalPolicyDecision Evaluate(RuntimeDungeonTraversalPolicyRequest request) =>
        request.Transition.Id == ContentId.Parse("example:enter_hall")
            ? new RuntimeDungeonTraversalPolicyDecision(true)
            : new RuntimeDungeonTraversalPolicyDecision(false, ContentId.Parse("example:sealed"));
}
```

Supply a separate reverse transition if the player may return. The service
checks malformed IDs before dungeon/source mismatch and calls the policy only
for a valid request from the current node. A policy exception or null decision
is a typed `PolicyFaulted` result; `OperationCanceledException` and fatal
memory failures propagate. Use `Code`, `InvalidField`, `FaultKind`, and
`ReasonId` for UI mapping. `Message` is diagnostic text, not a gameplay rule.

## Request, Present, Adopt

A console selection, visual-novel hotspot, Godot area signal, or script may
make the same request. `Applied` means the Framework approved the logical
move; it does not mean a scene loaded. Use the result as a candidate:

```csharp
RuntimeDungeonTraversalResult candidate = traversal.Traverse(current, enterHall);
if (candidate.Applied && await TryLoadHostSceneAsync(candidate.After.CurrentNodeId))
{
    current = candidate.After;
}
// TryLoadHostSceneAsync belongs to the game. On failure, retain current.
```

Map the ordered `candidate.Events` to host presentation. An applied structural
event reports rule approval; do not announce a completed visual transition
until host scene work succeeds. A rejected transition leaves `After` equal to
`Before`. The Framework never manipulates Godot Nodes, scene paths, spatial
coordinates, animation, or UI.

## Report Progress Explicitly

After the game's success condition, call `UnlockCheckpoint` or
`RegisterBossDefeat` with the active snapshot and declared ID. A result is
`Applied`, `AlreadyRecorded`, `InvalidRequest`, `NotEligible`,
`DungeonMismatch`, or `AreaMismatch`. Eligibility is checked before
idempotence; a report from another area does not become valid merely because
its ID was previously recorded. Only adopt `After` on `Applied` (or retain the
unchanged state on `AlreadyRecorded`). A loss should make no success call. A
puzzle or script may make the same call as a battle win.

The registry constructor defensively copies declarations and their allowed
nodes. Rebuild the service with a different registry if the game's declared
set changes; use the route policy, not registry mutation, for dynamic door
availability. The Framework does not infer a boss defeat from node entry,
encounter preparation, or a battle ending.

## Floors, Encounters, And Re-Entry

If your pack includes `DungeonDefinition`, read its blocks, encounter pools,
and `FixedFloors` directly from `GameDataCatalog`. No runtime resolver maps
floor numbers to your scene nodes. A fixed floor may offer an encounter ID;
the host decides if and when a visible enemy or scripted trigger selects it.
Call the encounter planner/preparation service explicitly with a distinct
host trigger instance for each encounter. Zero, one, or many triggers may
occur on the same floor. Traversal itself never prepares actors or starts a
battle.

When leaving, keep the dungeon snapshot if progress should persist, but use
the logical navigation location to decide whether dungeon controls are
active. On re-entry, select an entrance or a checkpoint that is present in
`UnlockedCheckpointIds`, map it to a host-owned node, and construct a new
snapshot with the retained visited/checkpoint/boss lists. Do not silently use
`CurrentNodeId` from the last visit as the new entry point. The Training
Annex's `SelectDungeonEntry` is one host-local example of this rule.

## Save And Restore

Save contract v19 permits no field state, navigation-only field state, or
navigation plus retained dungeon state. Framework validation checks basic
IDs and catalog dungeon references, but it does not know your scene graph.
Before adopting an aggregate restore, the host must check any stronger
requirements: for example, an inside location that requires a current dungeon
node, recognized node IDs, and available scenes. A visual host must finish
loading its scene before adopting the candidate restored state. An outside
location with retained dungeon progress is valid and should use the outside
save/menu context. `CurrentSaveContext` in Training Annex demonstrates that
the logical location, not mere progress presence, chooses context.

See the [technical traversal contract](../technical/dungeon-traversal-runtime.md)
for ordering and the [mechanics overview](../mechanics/world-encounters-and-rewards.md)
for player-facing meaning. The [Godot integration contract](../godot-integration-contract.md)
describes the engine-neutral boundary; the current real Godot smoke sample is
not a full spatial dungeon consumer.
