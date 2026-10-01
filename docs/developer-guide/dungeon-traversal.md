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
that a battle occurred or that saved history was earned.

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
`InvalidField` can name current/transition scalar IDs, any of the three retained
history collections, or the reported `ProgressId`; see the technical reference
for the exact enum values and validation order.

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
Annex's `SelectDungeonEntry` is one host-local example of this rule. It is an
internal helper whose production caller supplies the fixed entrance; tests also
exercise an already-unlocked checkpoint. Its `ArgumentException` guards
programmer misuse. It is not the public typed traversal failure boundary and
should not be called with arbitrary player or network input. A reusable host
adapter accepting untrusted entry IDs should return its own typed, non-mutating
selection result first.

## Save And Restore

Save contract v19 permits no field state, navigation-only field state, or
navigation plus retained dungeon state. When a save retains unlocked
checkpoints or defeated bosses, construct the validator with the same immutable
registry used by the live traversal service:

```csharp
var saveValidator = RuntimeSaveValidator.CreateWithDungeonProgressRegistry(registry);
RuntimeSaveValidationResult validation = saveValidator.Validate(snapshot, catalog);
```

Validation rejects an undeclared progress ID, the wrong checkpoint/boss kind,
the wrong dungeon, or a record whose eligible area is absent from visited
history. Calling the ordinary `RuntimeSaveValidator` constructor remains valid
for absent field state, navigation-only state, or dungeon state with no retained
checkpoint/boss records. A save containing retained records returns
`DungeonProgressRegistryMissing` instead of trusting them. Passing a registry
does not make the dungeon module mandatory and does not infer progress from node
entry.

The retained-progress diagnostics are deliberately specific:

| Numeric value | Diagnostic | Meaning |
|---:|---|---|
| 99 | `DungeonProgressRegistryMissing` | Retained progress was supplied without registry authority. |
| 100 | `DungeonProgressUndeclared` | No declaration has the retained progress ID. |
| 101 | `DungeonProgressKindMismatch` | The ID is declared, but not as the saved checkpoint/boss kind. |
| 102 | `DungeonProgressDungeonMismatch` | The kind matches, but no declaration belongs to the saved dungeon. |
| 103 | `DungeonProgressEligibleAreaNotVisited` | No allowed node from the matching declaration appears in saved visited history. |

These checks establish structural consistency between a host-supplied save and
the current registry. They are not an anti-tamper receipt: a host-constructed
snapshot containing both an eligible visited node and its progress ID is
accepted even if it did not pass through the live service. Signatures,
encryption, trusted storage, or another provenance mechanism belong to the
host when the game must detect edited saves.

Save contract v19 did not change when registry-backed validation was added
because the serialized shape did not change. That does not make progress
declarations migration-stable. The validator also requires exact content-pack
identity and version; renamed, removed, retyped, moved, or retargeted progress
may reject an old save. A released game must provide a host-owned migration or
an explicit incompatible-save message rather than silently selecting a
replacement.

Framework validation also checks basic IDs and the catalog dungeon reference,
but it does not know your scene graph. Before adopting an aggregate restore,
the host must check any stronger requirements: for example, an inside location
that requires a current dungeon node, recognized node IDs, and available
scenes. A visual host must finish loading its scene before adopting the
candidate restored state. An outside location with retained dungeon progress
is valid and should use the outside save/menu context. `CurrentSaveContext` in
Training Annex demonstrates that the logical location, not mere progress
presence, chooses context.

Framework does not couple a navigation location ID to a dungeon/node pair. The
interactive Training Annex adds that host rule: only its staging-area and
annex-entrance locations are recognized, the inside location requires a dungeon
position, and only its named nodes are accepted. The noninteractive Training
Annex demo deliberately saves `training_annex_floor_2` beside `review_alcove`
to exercise generic Framework validation; that illustrative snapshot is not an
interactive Training Annex save fixture and its host validator would reject the
location ID.

Godot evidence also has two distinct layers. `GodotIntegrationContractTests`
use a test-only in-memory store to prove that a `RuntimeFieldSnapshot` can sit
inside host-owned storage. The real `Convergence.GodotHost` sample codec does
not yet serialize field, navigation, or dungeon state: it reconstructs the
aggregate with `Field == null` and uses the ordinary validator. Therefore its
headless smoke proves the wider host boundary, not Order 8/9 persistence or a
spatial dungeon restore.

See the [technical traversal contract](../technical/dungeon-traversal-runtime.md)
for ordering and the [mechanics overview](../mechanics/world-encounters-and-rewards.md)
for player-facing meaning. The [Godot integration contract](../godot-integration-contract.md)
describes the engine-neutral boundary; the current real Godot smoke sample is
not a field/dungeon persistence or spatial dungeon consumer.
