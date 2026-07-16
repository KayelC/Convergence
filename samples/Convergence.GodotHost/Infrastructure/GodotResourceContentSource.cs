using Convergence.Catalog;
using Convergence.Hosting;
using Godot;
using GodotFileAccess = Godot.FileAccess;

namespace Convergence.GodotHost.Infrastructure;

internal sealed class GodotResourceContentSource : IContentPackTextSource
{
    private readonly string _resourceRoot;

    public GodotResourceContentSource(string resourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceRoot);
        string normalized = resourceRoot.Replace('\\', '/').TrimEnd('/');
        if (!normalized.StartsWith("res://", StringComparison.Ordinal))
        {
            throw new ArgumentException("A Godot resource root must begin with 'res://'.", nameof(resourceRoot));
        }

        _resourceRoot = normalized;
    }

    public ValueTask<ContentPackTextBundle> ReadAsync(
        ContentPackTextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string manifestLogicalPath = NormalizeLogicalPath(request.ManifestPath);
        string manifestResourcePath = ResolveResourcePath(manifestLogicalPath);
        string manifestJson = ReadText(manifestResourcePath);
        string manifestDirectory = LogicalDirectory(manifestLogicalPath);

        var documents = new List<ContentDocumentText>(request.DocumentPaths.Count);
        foreach (string documentPath in request.DocumentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string logicalPath = NormalizeLogicalPath(documentPath);
            string manifestRelativePath = string.IsNullOrEmpty(manifestDirectory)
                ? logicalPath
                : $"{manifestDirectory}/{logicalPath}";
            string resourcePath = ResolveResourcePath(manifestRelativePath);
            documents.Add(new ContentDocumentText(logicalPath, resourcePath, ReadText(resourcePath)));
        }

        return ValueTask.FromResult(new ContentPackTextBundle(
            manifestResourcePath,
            manifestJson,
            documents));
    }

    private string ResolveResourcePath(string logicalPath) => $"{_resourceRoot}/{logicalPath}";

    private static string ReadText(string resourcePath)
    {
        if (!GodotFileAccess.FileExists(resourcePath))
        {
            throw new FileNotFoundException($"Godot resource '{resourcePath}' was not found.", resourcePath);
        }

        using GodotFileAccess? file = GodotFileAccess.Open(resourcePath, GodotFileAccess.ModeFlags.Read);
        if (file is null)
        {
            throw new IOException(
                $"Godot could not open resource '{resourcePath}' ({GodotFileAccess.GetOpenError()}).");
        }

        return file.GetAsText();
    }

    private static string NormalizeLogicalPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains("://", StringComparison.Ordinal) ||
            normalized.Split('/').Any(segment =>
                segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException($"Content path '{path}' is not a confined logical path.", nameof(path));
        }

        return normalized;
    }

    private static string LogicalDirectory(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }
}
