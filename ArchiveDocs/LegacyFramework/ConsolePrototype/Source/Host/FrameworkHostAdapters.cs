using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host;

internal sealed class LegacyFileContentPackSource : IContentPackTextSource
{
    private readonly string _root;

    public LegacyFileContentPackSource(string root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public async ValueTask<ContentPackTextBundle> ReadAsync(
        ContentPackTextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string manifestFile = Path.Combine(_root, request.ManifestPath);
        string manifestText = await File.ReadAllTextAsync(manifestFile, cancellationToken).ConfigureAwait(false);
        var documents = new List<ContentDocumentText>(request.DocumentPaths.Count);
        foreach (string path in request.DocumentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = Path.Combine(_root, path);
            string text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            documents.Add(new ContentDocumentText(path, file, text));
        }

        return new ContentPackTextBundle(manifestFile, manifestText, documents);
    }
}

internal sealed class GameIoEventSink : IHostEventSink<string>
{
    private readonly IGameIO _io;

    public GameIoEventSink(IGameIO io)
    {
        _io = io ?? throw new ArgumentNullException(nameof(io));
    }

    public ValueTask PublishAsync(string hostEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _io.WriteLine(hostEvent);
        return ValueTask.CompletedTask;
    }
}

internal sealed class ConsoleHostCommandSource<TCommand> : IHostCommandSource<TCommand>
{
    private readonly IGameIO _io;

    public ConsoleHostCommandSource(IGameIO io)
    {
        _io = io ?? throw new ArgumentNullException(nameof(io));
    }

    public ValueTask<HostCommandReadResult<TCommand>> ReadAsync(
        HostCommandRequest<TCommand> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        List<string> labels = request.Options.Select(option => option.Label).ToList();
        List<bool> disabled = request.Options.Select(option => !option.IsEnabled).ToList();
        int selection = _io.RenderMenu(request.Prompt, labels, request.InitialIndex, disabled);
        return ValueTask.FromResult(selection < 0
            ? HostCommandReadResult<TCommand>.Cancelled()
            : HostCommandReadResult<TCommand>.Selected(request.Options[selection]));
    }
}

internal static class ConsoleHostCommandReader
{
    public static HostCommandReadResult<TCommand> Read<TCommand>(
        IGameIO io,
        string prompt,
        IEnumerable<HostCommandOption<TCommand>> options,
        int initialIndex = 0)
    {
        return new ConsoleHostCommandSource<TCommand>(io)
            .ReadAsync(new HostCommandRequest<TCommand>(prompt, options, initialIndex))
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }
}

internal readonly record struct ConsoleMenuSelection<TCommand>(TCommand Command, int Index);
