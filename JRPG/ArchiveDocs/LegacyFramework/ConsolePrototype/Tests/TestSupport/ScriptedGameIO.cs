using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JRPGPrototype.Services;

namespace Convergence.Tests.TestSupport;

internal sealed record GameIoWrite(string Text, ConsoleColor Color, bool EndsLine);

internal sealed record GameIoMenuCall(
    string Header,
    IReadOnlyList<string> Options,
    int InitialIndex,
    IReadOnlyList<bool> DisabledOptions,
    bool SupportsStatusInspect);

internal sealed class ScriptedGameIO : IGameIO
{
    private readonly Queue<ConsoleKeyInfo> _keys = new();
    private readonly Queue<string> _lines = new();
    private readonly Queue<int> _menuSelections = new();
    private readonly List<GameIoWrite> _writes = new();
    private readonly List<GameIoMenuCall> _menus = new();
    private readonly List<int> _waits = new();

    public IReadOnlyList<GameIoWrite> Writes => new ReadOnlyCollection<GameIoWrite>(_writes);
    public IReadOnlyList<GameIoMenuCall> Menus => new ReadOnlyCollection<GameIoMenuCall>(_menus);
    public IReadOnlyList<int> Waits => new ReadOnlyCollection<int>(_waits);
    public int ClearCount { get; private set; }

    public string CombinedOutput => string.Join(
        Environment.NewLine,
        _writes.ConvertAll(write => write.Text));

    public ScriptedGameIO QueueKey(char keyChar, ConsoleKey key = ConsoleKey.NoName)
    {
        _keys.Enqueue(new ConsoleKeyInfo(keyChar, key, false, false, false));
        return this;
    }

    public ScriptedGameIO QueueLine(string line)
    {
        _lines.Enqueue(line);
        return this;
    }

    public ScriptedGameIO QueueMenu(params int[] selections)
    {
        foreach (int selection in selections)
        {
            _menuSelections.Enqueue(selection);
        }

        return this;
    }

    public void WriteLine(string message, ConsoleColor color = ConsoleColor.White) =>
        _writes.Add(new GameIoWrite(message, color, true));

    public void Write(string message, ConsoleColor color = ConsoleColor.White) =>
        _writes.Add(new GameIoWrite(message, color, false));

    public void Clear() => ClearCount++;

    public void Wait(int milliseconds) => _waits.Add(milliseconds);

    public string ReadLine()
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("No scripted line input was available.");
        }

        return _lines.Dequeue();
    }

    public ConsoleKeyInfo ReadKey(bool intercept = true)
    {
        if (_keys.Count == 0)
        {
            throw new InvalidOperationException("No scripted key input was available.");
        }

        return _keys.Dequeue();
    }

    public void SetForegroundColor(ConsoleColor color)
    {
    }

    public void SetBackgroundColor(ConsoleColor color)
    {
    }

    public void ResetColor()
    {
    }

    public void SetCursorVisible(bool visible)
    {
    }

    public int RenderMenu(
        string header,
        List<string> options,
        int initialIndex,
        List<bool>? disabledOptions = null,
        Action<int>? onHighlight = null,
        bool supportStatusInspect = false)
    {
        _menus.Add(new GameIoMenuCall(
            header,
            new ReadOnlyCollection<string>(new List<string>(options)),
            initialIndex,
            new ReadOnlyCollection<bool>(disabledOptions is null ? [] : new List<bool>(disabledOptions)),
            supportStatusInspect));

        if (_menuSelections.Count == 0)
        {
            throw new InvalidOperationException($"No scripted selection was available for menu '{header}'.");
        }

        int selection = _menuSelections.Dequeue();
        if (selection < -1 || selection >= options.Count)
        {
            throw new InvalidOperationException(
                $"Scripted selection {selection} is outside menu '{header}' with {options.Count} options.");
        }

        if (selection >= 0 && disabledOptions is not null &&
            selection < disabledOptions.Count && disabledOptions[selection])
        {
            throw new InvalidOperationException(
                $"Scripted selection {selection} chose a disabled option in menu '{header}'.");
        }

        return selection;
    }

    public void AssertConsumed()
    {
        if (_keys.Count != 0 || _lines.Count != 0 || _menuSelections.Count != 0)
        {
            throw new InvalidOperationException(
                $"Scripted input was not fully consumed: keys={_keys.Count}, lines={_lines.Count}, menus={_menuSelections.Count}.");
        }
    }
}
