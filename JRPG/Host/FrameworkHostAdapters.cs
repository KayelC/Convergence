using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host;

internal sealed class FileContentPackSource : IContentPackTextSource
{
    private readonly string _root;

    public FileContentPackSource(string root)
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

internal sealed class TextWriterEventSink : IHostEventSink<string>
{
    private readonly TextWriter _writer;

    public TextWriterEventSink(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async ValueTask PublishAsync(string hostEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _writer.WriteLineAsync(hostEvent.AsMemory(), cancellationToken).ConfigureAwait(false);
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
            : HostCommandReadResult<TCommand>.Selected(request.Options[selection].Command));
    }
}

internal sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource()
        : this(new Random())
    {
    }

    public SystemRandomSource(int seed)
        : this(new Random(seed))
    {
    }

    internal SystemRandomSource(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public int NextInt32(int minimumInclusive, int maximumExclusive) =>
        _random.Next(minimumInclusive, maximumExclusive);

    public decimal NextUnitDecimal() => (decimal)_random.NextDouble();
}
