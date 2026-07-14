# FusionMessenger

Source: [`Logic/Fusion/Messaging/FusionMessenger.cs`](../../../../Logic/Fusion/Messaging/FusionMessenger.cs)

## Purpose

`FusionMessenger` is the default publisher for Fusion messages.

## Class Shape

```csharp
public class FusionMessenger : IFusionMessenger
{
    public event EventHandler<FusionMessageArgs> OnMessagePublished;

    public void Publish(
        string? message,
        ConsoleColor color = ConsoleColor.Gray,
        int delay = 0,
        bool waitForInput = false,
        bool clearScreen = false)
}
```

## `Publish`

```csharp
OnMessagePublished?.Invoke(this, new FusionMessageArgs(
    message,
    color,
    delay,
    waitForInput,
    clearScreen));
```

The messenger does not render anything itself. It creates a `FusionMessageArgs`
payload and invokes subscribers if any exist.

## State And Mutation

The only state is the event subscriber list.

## Invariants And Safety Rules

- Publishing with no subscribers must be safe.
- The payload should preserve all presentation flags exactly.

## Tests And Verification

Currently covered indirectly by Fusion flows. Direct unit tests could subscribe
a fake handler and assert the payload.

## Refactor Notes

The event is currently non-nullable and produces a nullable warning. A later
nullable cleanup can declare it nullable or initialize it safely.
