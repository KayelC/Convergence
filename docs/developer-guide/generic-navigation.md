# Generic Navigation Integration

## Purpose And Ownership

Navigation is an optional logical transition service. Framework validates a
request, checks its current source, and asks an injected
`IRuntimeNavigationPolicy` whether travel is allowed. The game owns location and
transition IDs, policy composition, input triggers, scenes, movement,
localization, and adoption of the returned snapshot. No catalog location
definition or route graph is required.

`RuntimeNavigationSnapshot` stores a `ContentId` current location.
`RuntimeNavigationTransition` contains its own ID, source, and destination.
`RuntimeNavigationService.Navigate` returns `RuntimeNavigationResult` with a
typed code, immutable `Before`/`After` snapshots, and ordered structural
events. The service itself does not keep or mutate a session.

## Compose A Policy

The policy is mandatory when constructing the supplied service. This example
allows a single authored route; a real game can consult its own story state.
The reverse direction must be a separate transition.

```csharp
using Convergence.Content;
using Convergence.Runtime;

var staging = ContentId.Parse("example:staging_area");
var entrance = ContentId.Parse("example:annex_entrance");
var enter = new RuntimeNavigationTransition(
    ContentId.Parse("example:enter_annex"), staging, entrance);
var navigation = new RuntimeNavigationService(new AnnexAccessPolicy(enter));
var current = new RuntimeNavigationSnapshot(staging);

sealed class AnnexAccessPolicy(RuntimeNavigationTransition allowedTransition) : IRuntimeNavigationPolicy
{
    public RuntimeNavigationPolicyDecision Evaluate(RuntimeNavigationPolicyRequest request) =>
        request.Transition == allowedTransition
            ? new RuntimeNavigationPolicyDecision(true)
            : new RuntimeNavigationPolicyDecision(
                false, ContentId.Parse("example:route_locked"));
}
```

Policies can change their answer as game progress changes. They should use
typed IDs and host-owned facts, not UI labels. `Navigate` does not ask a
policy when IDs are invalid or the current location does not match the
transition source. Policy programming faults become `PolicyFaulted` results;
operational cancellation is not converted into such a result.

## Trigger, Present, Adopt

The same pattern works for a console option, visual-novel hotspot, or Godot
door/area signal. A host can read a selected transition through
`IHostCommandSource<RuntimeNavigationTransition>`, or invoke the service from
its own signal callback. Cancellation before the request performs no travel.

```csharp
RuntimeNavigationResult result = navigation.Navigate(current, enter);
foreach (RuntimeNavigationEvent navigationEvent in result.Events)
{
    await hostEventSink.PublishAsync(navigationEvent, cancellationToken);
}

if (result.Applied && await hostSceneWork.TryPresentAsync(
        result.After.CurrentLocationId, cancellationToken))
{
    current = result.After;
}
// Otherwise retain current. The framework has not moved a Node or scene.
```

`hostEventSink` and `hostSceneWork` above are illustrative host-owned adapters,
not framework services. The host maps `RuntimeNavigationTransitionCode`,
`ReasonId`, `InvalidField`, and `FaultKind` to localized presentation. `Message`
is diagnostic text only. An `Applied` event records logical rule approval even
if subsequent host scene work fails; it must not be interpreted as a scene-load
completion event.

For a visual-novel overworld plus 3D dungeon, the location selection can
request an entrance transition. Inside the 3D scene, Godot owns continuous
movement and calls the separate dungeon traversal service only at meaningful
node or checkpoint boundaries. Traversal never starts an encounter by itself.

## Save And Restore

The optional save v19 `Field` aggregate requires navigation when present and
may contain retained dungeon traversal progress. It supports no field,
navigation only, or navigation with dungeon state; it does not encode a
dungeon-only field. A game using only traversal can provide a stable neutral
logical location without constructing the navigation service. Save validation
checks that the location ID is nonempty, not that it names a catalog entry.
Aggregate restore preserves the saved logical location without asking the
travel policy to authorize historical movement. Godot stores its scene path,
transform, and visual context outside Framework and applies those after
successful restore.

The [technical navigation state machine](../technical/generic-navigation-runtime.md)
records validation order and result invariants. The
[Godot integration contract](../godot-integration-contract.md) distinguishes
test-only trigger evidence from the current real smoke sample.
