using JRPGPrototype.Data.SkillSystem.Catalog;

namespace JRPGPrototype.Hosting;

public sealed record ContentPackTextRequest
{
    public ContentPackTextRequest(string manifestPath, IEnumerable<string> documentPaths)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("A manifest logical path is required.", nameof(manifestPath));
        }

        ManifestPath = manifestPath;
        DocumentPaths = Array.AsReadOnly(
            (documentPaths ?? throw new ArgumentNullException(nameof(documentPaths))).ToArray());
    }

    public string ManifestPath { get; }
    public IReadOnlyList<string> DocumentPaths { get; }
}

public interface IContentPackTextSource
{
    ValueTask<ContentPackTextBundle> ReadAsync(
        ContentPackTextRequest request,
        CancellationToken cancellationToken = default);
}

public interface IHostEventSink<in TEvent>
{
    ValueTask PublishAsync(TEvent hostEvent, CancellationToken cancellationToken = default);
}

public sealed record HostCommandOption<TCommand>(
    TCommand Command,
    string Label,
    bool IsEnabled = true,
    string? Description = null);

public sealed record HostCommandRequest<TCommand>
{
    public HostCommandRequest(
        string prompt,
        IEnumerable<HostCommandOption<TCommand>> options,
        int initialIndex = 0)
    {
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        Options = Array.AsReadOnly(
            (options ?? throw new ArgumentNullException(nameof(options))).ToArray());
        InitialIndex = initialIndex;
    }

    public string Prompt { get; }
    public IReadOnlyList<HostCommandOption<TCommand>> Options { get; }
    public int InitialIndex { get; }
}

public enum HostCommandReadStatus
{
    Selected,
    Cancelled
}

public sealed record HostCommandReadResult<TCommand>
{
    private HostCommandReadResult(HostCommandReadStatus status, TCommand? command)
    {
        Status = status;
        Command = command;
    }

    public HostCommandReadStatus Status { get; }
    public TCommand? Command { get; }
    public bool IsSelected => Status == HostCommandReadStatus.Selected;

    public static HostCommandReadResult<TCommand> Selected(TCommand command) =>
        new(HostCommandReadStatus.Selected, command);

    public static HostCommandReadResult<TCommand> Cancelled() =>
        new(HostCommandReadStatus.Cancelled, default);
}

public interface IHostCommandSource<TCommand>
{
    ValueTask<HostCommandReadResult<TCommand>> ReadAsync(
        HostCommandRequest<TCommand> request,
        CancellationToken cancellationToken = default);
}

public interface IRandomSource
{
    int NextInt32(int minimumInclusive, int maximumExclusive);
    decimal NextUnitDecimal();
}
