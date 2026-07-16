using Convergence.Catalog;
using Convergence.Hosting;

namespace Convergence.DemoHost;

internal sealed class FileContentPackSource : IContentPackTextSource
{
    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly StringComparison _pathComparison;

    public FileContentPackSource(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.GetFullPath(root);
        _rootPrefix = Path.EndsInDirectorySeparator(_root)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public async ValueTask<ContentPackTextBundle> ReadAsync(
        ContentPackTextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string manifestFile = ResolveContentFile(request.ManifestPath, _root);
        string manifestDirectory = Path.GetDirectoryName(manifestFile) ?? _root;
        (string LogicalPath, string FilePath)[] resolvedDocuments = request.DocumentPaths
            .Select(path => (path, ResolveContentFile(path, manifestDirectory)))
            .ToArray();

        string manifestText = await File.ReadAllTextAsync(manifestFile, cancellationToken).ConfigureAwait(false);
        var documents = new List<ContentDocumentText>(resolvedDocuments.Length);
        foreach ((string logicalPath, string filePath) in resolvedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            documents.Add(new ContentDocumentText(logicalPath, filePath, text));
        }

        return new ContentPackTextBundle(manifestFile, manifestText, documents);
    }

    private string ResolveContentFile(string logicalPath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(logicalPath))
        {
            throw new ArgumentException("A content logical path is required.", nameof(logicalPath));
        }

        string normalizedPath = logicalPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (IsRootedLogicalPath(logicalPath, normalizedPath))
        {
            throw OutsideContentRoot(logicalPath);
        }

        string resolvedPath = Path.GetFullPath(normalizedPath, baseDirectory);
        if (!resolvedPath.StartsWith(_rootPrefix, _pathComparison))
        {
            throw OutsideContentRoot(logicalPath);
        }

        return resolvedPath;
    }

    private static bool IsRootedLogicalPath(string logicalPath, string normalizedPath) =>
        Path.IsPathRooted(normalizedPath) ||
        logicalPath[0] is '/' or '\\' ||
        (logicalPath.Length >= 2 && char.IsAsciiLetter(logicalPath[0]) && logicalPath[1] == ':');

    private static UnauthorizedAccessException OutsideContentRoot(string logicalPath) =>
        new($"Content logical path '{logicalPath}' is outside the configured content root.");
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

internal interface IConsoleMenuDriver
{
    int RenderMenu(
        string header,
        IReadOnlyList<string> options,
        int initialIndex,
        IReadOnlyList<bool> disabledOptions);
}

internal sealed class ConsoleHostCommandSource<TCommand> : IHostCommandSource<TCommand>
{
    private readonly IConsoleMenuDriver _menu;

    public ConsoleHostCommandSource(IConsoleMenuDriver menu)
    {
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
    }

    public ValueTask<HostCommandReadResult<TCommand>> ReadAsync(
        HostCommandRequest<TCommand> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string[] labels = request.Options.Select(option => option.Label).ToArray();
        bool[] disabled = request.Options.Select(option => !option.IsEnabled).ToArray();
        int selection = _menu.RenderMenu(request.Prompt, labels, request.InitialIndex, disabled);
        return ValueTask.FromResult(selection < 0
            ? HostCommandReadResult<TCommand>.Cancelled()
            : HostCommandReadResult<TCommand>.Selected(request.Options[selection]));
    }
}

internal sealed class TextReaderCommandSource<TCommand> : IHostCommandSource<TCommand>
{
    private readonly TextReader _reader;
    private readonly TextWriter _writer;

    public TextReaderCommandSource(TextReader reader, TextWriter writer)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async ValueTask<HostCommandReadResult<TCommand>> ReadAsync(
        HostCommandRequest<TCommand> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await _writer.WriteLineAsync(request.Prompt.AsMemory(), cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < request.Options.Count; index++)
        {
            HostCommandOption<TCommand> option = request.Options[index];
            string state = option.IsEnabled ? string.Empty : " [Unavailable]";
            await _writer.WriteLineAsync($"{index + 1}. {option.Label}{state}".AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _writer.WriteAsync("> ".AsMemory(), cancellationToken).ConfigureAwait(false);
            string? input = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (input is null || input.Equals("back", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                return HostCommandReadResult<TCommand>.Cancelled();
            }

            if (int.TryParse(input, out int selection) &&
                selection >= 1 && selection <= request.Options.Count &&
                request.Options[selection - 1].IsEnabled)
            {
                return HostCommandReadResult<TCommand>.Selected(request.Options[selection - 1]);
            }

            await _writer.WriteLineAsync("Select an available option number, or enter 'back'.".AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }
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
