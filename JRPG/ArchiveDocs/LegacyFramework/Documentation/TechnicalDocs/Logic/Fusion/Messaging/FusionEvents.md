# FusionEvents

Source: [`Logic/Fusion/Messaging/FusionEvents.cs`](../../../../Logic/Fusion/Messaging/FusionEvents.cs)

## Purpose

`FusionEvents.cs` defines the event payload for Fusion messaging.

## Class Shape

```csharp
public class FusionMessageArgs : EventArgs
{
    public string? Message { get; }
    public ConsoleColor Color { get; }
    public int Delay { get; }
    public bool WaitForInput { get; }
    public bool ClearScreen { get; }
}
```

## Constructor

```csharp
public FusionMessageArgs(
    string? message,
    ConsoleColor color = ConsoleColor.Gray,
    int delay = 0,
    bool waitForInput = false,
    bool clearScreen = false)
{
    Message = message;
    Color = color;
    Delay = delay;
    WaitForInput = waitForInput;
    ClearScreen = clearScreen;
}
```

The payload is presentation-oriented. It carries text, color, pacing, input wait,
and clear-screen intent.

## State And Mutation

Immutable after construction.

## Invariants And Safety Rules

- `Message` may be null.
- `Delay` is interpreted by subscribers, not by the event itself.
- Color is currently console-shaped and should not be treated as framework-core
  domain data.

## Refactor Notes

For Unity/Godot adapters, this may evolve into domain events plus presentation
metadata rather than direct `ConsoleColor`.
