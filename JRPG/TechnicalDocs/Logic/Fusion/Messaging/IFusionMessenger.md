# IFusionMessenger

Source: [`Logic/Fusion/Messaging/IFusionMessenger.cs`](../../../../Logic/Fusion/Messaging/IFusionMessenger.cs)

## Purpose

`IFusionMessenger` defines the Fusion message publication contract. Fusion logic
uses it to publish feedback without directly writing to the console.

## Interface Shape

```csharp
public interface IFusionMessenger
{
    event EventHandler<FusionMessageArgs> OnMessagePublished;

    void Publish(
        string? message,
        ConsoleColor color = ConsoleColor.Gray,
        int delay = 0,
        bool waitForInput = false,
        bool clearScreen = false);
}
```

## Runtime Role

Strategies, calculator traces, mutator guards, and conductor flow events call
`Publish`. `FusionLogger` subscribes to `OnMessagePublished` and renders through
`IGameIO`.

## State And Mutation

The interface has no state. Implementations control event dispatch.

## Invariants And Safety Rules

- Fusion logic should publish through this interface instead of calling console
  APIs directly.
- `message` may be null. The logger treats null as no text while still honoring
  delay, clear, or wait flags.

## Refactor Notes

Future engine adapters can subscribe different presentation layers to the same
event contract or replace this with a richer domain event stream.
