using System.Collections.ObjectModel;
using JRPGPrototype.Host;

namespace Convergence.Tests.TestSupport;

internal sealed record GameIoMenuCall(
    string Header,
    IReadOnlyList<string> Options,
    int InitialIndex,
    IReadOnlyList<bool> DisabledOptions,
    bool SupportsStatusInspect = false);

internal sealed class ScriptedGameIO : IConsoleMenuDriver
{
    private readonly Queue<int> _menuSelections = new();
    private readonly List<GameIoMenuCall> _menus = new();

    public IReadOnlyList<GameIoMenuCall> Menus => new ReadOnlyCollection<GameIoMenuCall>(_menus);

    public ScriptedGameIO QueueMenu(params int[] selections)
    {
        foreach (int selection in selections)
        {
            _menuSelections.Enqueue(selection);
        }

        return this;
    }

    public int RenderMenu(
        string header,
        IReadOnlyList<string> options,
        int initialIndex,
        IReadOnlyList<bool> disabledOptions)
    {
        _menus.Add(new GameIoMenuCall(
            header,
            Array.AsReadOnly(options.ToArray()),
            initialIndex,
            Array.AsReadOnly(disabledOptions.ToArray())));

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

        if (selection >= 0 && disabledOptions[selection])
        {
            throw new InvalidOperationException(
                $"Scripted selection {selection} chose a disabled option in menu '{header}'.");
        }

        return selection;
    }

    public void AssertConsumed()
    {
        if (_menuSelections.Count != 0)
        {
            throw new InvalidOperationException(
                $"Scripted input was not fully consumed: menus={_menuSelections.Count}.");
        }
    }
}
