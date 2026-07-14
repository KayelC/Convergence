# FusionLogger

Source: [`Logic/Fusion/Messaging/FusionLogger.cs`](../../../../Logic/Fusion/Messaging/FusionLogger.cs)

## Purpose

`FusionLogger` is the current console subscriber for Fusion messages. It turns
`FusionMessageArgs` into `IGameIO` calls.

## Class Shape

```csharp
public class FusionLogger
{
    private readonly IGameIO _io;

    public void Subscribe(IFusionMessenger messenger)
    public void Unsubscribe(IFusionMessenger messenger)
}
```

## Subscription

```csharp
public void Subscribe(IFusionMessenger messenger)
{
    messenger.OnMessagePublished += HandleFusionMessage;
}

public void Unsubscribe(IFusionMessenger messenger)
{
    messenger.OnMessagePublished -= HandleFusionMessage;
}
```

The conductor currently subscribes one logger during construction.

## `HandleFusionMessage`

Clear screen:

```csharp
if (e.ClearScreen)
{
    _io.Clear();
}
```

Write message:

```csharp
if (!string.IsNullOrEmpty(e.Message))
{
    _io.WriteLine(e.Message, e.Color);
}
```

Delay:

```csharp
if (e.Delay > 0)
{
    _io.Wait(e.Delay);
}
```

Forced acknowledgement:

```csharp
if (e.WaitForInput)
{
    _io.WriteLine("\nPress any key to continue...", ConsoleColor.Gray);
    _io.ReadKey();
}
```

## State And Mutation

This class mutates only console/UI presentation state through `IGameIO`.

## Invariants And Safety Rules

- Null/empty messages should still allow delay, clear, or wait behavior.
- Subscribe/unsubscribe should be paired if logger lifetime becomes longer than
  the Cathedral session.

## Refactor Notes

This is host adapter code, not framework core. A future engine host should
replace it with an adapter that maps Fusion messages to that engine's UI/event
model.
