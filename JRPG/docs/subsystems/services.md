# Services Subsystem

## Purpose

`Services` abstracts console I/O and shared menu rendering so gameplay logic does not call `Console` directly. This makes the prototype easier to test, redirect, or eventually port to another UI.

## Key Classes And Responsibilities

- `IGameIO`: the I/O contract used by conductors, bridges, loggers, and some setup code.
- `ConsoleIO`: concrete console implementation for text, colors, key input, cursor visibility, waits, clear screen, and menu rendering.
- `MenuUI`: static menu renderer used by `ConsoleIO.RenderMenu`.

## Main Runtime Flows

1. `Program.cs` creates `IGameIO io = new ConsoleIO()`.
2. Shared systems receive `IGameIO` through constructors.
3. Bridges use `RenderMenu`, `ReadKey`, and text output to collect choices.
4. Loggers use `WriteLine`, `Wait`, colors, and analysis display hooks to present subsystem messages.
5. Engines generally avoid direct I/O unless they need feedback during setup or use an injected messenger.

## Important State And Invariants

- `IGameIO.RenderMenu` is the only abstracted menu operation. It supports disabled options, highlight callbacks, initial selection, and optional status inspection.
- `ConsoleIO` owns direct `Console` interaction; gameplay systems should not.
- `Wait` is part of the I/O contract, so pacing is considered presentation behavior.

## Data Dependencies

Services do not depend on JSON. They render strings and options produced by gameplay modules.

## Extension Points

- Add a new UI backend by implementing `IGameIO`.
- Add common menu behavior in `MenuUI` or `ConsoleIO.RenderMenu` rather than duplicating key loops in bridges.
- Add richer rendering through bridges/loggers first, keeping engines UI-agnostic.

## Caveats

- The app is synchronous and console-key driven.
- Some non-service code still writes setup/debug text directly through `IGameIO`; that is acceptable, but direct `Console` use should remain isolated in `ConsoleIO` and `MenuUI`.
