using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;

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

public sealed record HostCommandSelectionIdentity
{
    private HostCommandSelectionIdentity(ContentId? contentId, RuntimeInstanceId? runtimeInstanceId)
    {
        if ((contentId is null) == (runtimeInstanceId is null))
        {
            throw new ArgumentException("A command selection identity must contain exactly one typed ID.");
        }

        ContentId = contentId;
        RuntimeInstanceId = runtimeInstanceId;
    }

    public ContentId? ContentId { get; }
    public RuntimeInstanceId? RuntimeInstanceId { get; }

    public static HostCommandSelectionIdentity ForContent(ContentId contentId) =>
        new(contentId, null);

    public static HostCommandSelectionIdentity ForRuntimeInstance(RuntimeInstanceId runtimeInstanceId) =>
        new(null, runtimeInstanceId);
}

public sealed record HostCommandOption<TCommand>(
    TCommand Command,
    string Label,
    bool IsEnabled = true,
    string? Description = null,
    HostCommandSelectionIdentity? SelectionIdentity = null);

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
    private HostCommandReadResult(
        HostCommandReadStatus status,
        TCommand? command,
        HostCommandSelectionIdentity? selectionIdentity)
    {
        Status = status;
        Command = command;
        SelectionIdentity = selectionIdentity;
    }

    public HostCommandReadStatus Status { get; }
    public TCommand? Command { get; }
    public HostCommandSelectionIdentity? SelectionIdentity { get; }
    public bool IsSelected => Status == HostCommandReadStatus.Selected;

    public static HostCommandReadResult<TCommand> Selected(
        TCommand command,
        HostCommandSelectionIdentity? selectionIdentity = null) =>
        new(HostCommandReadStatus.Selected, command, selectionIdentity);

    public static HostCommandReadResult<TCommand> Selected(HostCommandOption<TCommand> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return Selected(option.Command, option.SelectionIdentity);
    }

    public static HostCommandReadResult<TCommand> Cancelled() =>
        new(HostCommandReadStatus.Cancelled, default, null);
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
