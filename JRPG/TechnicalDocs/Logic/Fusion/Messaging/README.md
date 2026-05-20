# Fusion Messaging

Source folder: [`Logic/Fusion/Messaging`](../../../../Logic/Fusion/Messaging)

This folder contains the Fusion message event model and the current console
logger.

Detailed file docs:

- [IFusionMessenger](IFusionMessenger.md)
- [FusionMessenger](FusionMessenger.md)
- [FusionEvents](FusionEvents.md)
- [FusionLogger](FusionLogger.md)

## Current Responsibility

Fusion logic publishes messages through `IFusionMessenger` instead of writing
directly to the console. `FusionLogger` subscribes to those messages and renders
them through `IGameIO`.

## Review Focus

- event payload shape,
- publisher/subscriber flow,
- console rendering boundary,
- future adapter implications for Unity/Godot hosts.
